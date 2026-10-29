using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace VideoBatchMerger.ScriptBatch
{
    public static class ChatterboxEngine
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static Process _worker;
        private static StreamWriter _input;
        private static DateTime _lastUseUtc;
        private static int _keepAliveMinutes = 15;
        private static readonly System.Threading.Timer IdleTimer = new System.Threading.Timer(_ =>
        {
            if (!Gate.Wait(0)) return;
            try { if (DateTime.UtcNow - _lastUseUtc > TimeSpan.FromMinutes(Volatile.Read(ref _keepAliveMinutes))) StopWorker(); }
            finally { Gate.Release(); }
        }, null, 60000, 60000);

        public static readonly List<VoiceOption> AvailableVoices = new List<VoiceOption>
        {
            new VoiceOption("chatterbox:pt", "葡萄牙语 · 模型默认 / 已选参考声音", "pt"),
            new VoiceOption("chatterbox:es", "西班牙语 · 模型默认 / 已选参考声音", "es"),
            new VoiceOption("chatterbox:en", "英语 · 模型默认 / 已选参考声音", "en"),
            new VoiceOption("chatterbox:fr", "法语 · 模型默认 / 已选参考声音", "fr"),
            new VoiceOption("chatterbox:de", "德语 · 模型默认 / 已选参考声音", "de"),
            new VoiceOption("chatterbox:it", "意大利语 · 模型默认 / 已选参考声音", "it")
        };

        static ChatterboxEngine()
        {
            System.Windows.Forms.Application.ApplicationExit += (_, _) => StopWorker();
        }

        public static string RuntimeRoot
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chatterbox_runtime");
            }
        }

        public static string PythonPath => Path.Combine(RuntimeRoot, "python", "python.exe");
        public static bool IsInstalled => File.Exists(PythonPath) && File.Exists(Path.Combine(RuntimeRoot, "ready.json"))
            && File.Exists(Path.Combine(RuntimeRoot, "models", "t3_mtl23ls_v3.safetensors"));

        private static string ScriptPath(string name)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScriptBatch", name);
            if (File.Exists(path)) return path;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VideoBatchMerger", "ScriptBatch", name);
        }

        private static string Quote(string value)
        {
            // Correct Windows command-line quoting, including quotes and trailing backslashes.
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char ch in value ?? "")
            {
                if (ch == '\\') { slashes++; continue; }
                if (ch == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                result.Append(ch);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        private static ProcessStartInfo PythonStart(string arguments)
        {
            var start = new ProcessStartInfo(PythonPath, "-u " + arguments)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = RuntimeRoot
            };
            start.EnvironmentVariables["PYTHONUTF8"] = "1";
            start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            start.EnvironmentVariables["PATH"] = Path.GetDirectoryName(ScriptBatchComposer.FindFfmpegExe())
                + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            return start;
        }

        private static void StopWorker()
        {
            var worker = _worker;
            _worker = null;
            try { if (worker != null && !worker.HasExited) worker.Kill(); } catch { }
            try { worker?.Dispose(); } catch { }
            try { _input?.Dispose(); } catch { }
            _input = null;
        }

        public static void ConfigureIdleTimeout(int minutes)
        {
            Interlocked.Exchange(ref _keepAliveMinutes, Math.Max(3, Math.Min(30, minutes)));
        }

        public static bool TryReleaseModel()
        {
            if (!Gate.Wait(0)) return false;
            try { StopWorker(); return true; }
            finally { Gate.Release(); }
        }

        private static string AuditionCachePath(object voiceParameters)
        {
            // Include worker and model installation identities so upgrades invalidate older auditions.
            var serializer = new JavaScriptSerializer();
            string workerHash;
            using (var hash = SHA256.Create())
            using (var input = File.OpenRead(ScriptPath("chatterbox_worker.py")))
                workerHash = BitConverter.ToString(hash.ComputeHash(input));
            var content = serializer.Serialize(new { schema = 2, voiceParameters, workerHash,
                installed = File.GetLastWriteTimeUtc(Path.Combine(RuntimeRoot, "ready.json")).Ticks });
            using (var hash = SHA256.Create())
                return Path.Combine(RuntimeRoot, "audition_cache", BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "") + ".wav");
        }

        private static TtsResult ReadCachedAudition(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    if (new string(reader.ReadChars(4)) != "RIFF") return null;
                    reader.ReadUInt32();
                    if (new string(reader.ReadChars(4)) != "WAVE") return null;
                    uint bytesPerSecond = 0;
                    while (stream.Position + 8 <= stream.Length)
                    {
                        string chunk = new string(reader.ReadChars(4));
                        uint size = reader.ReadUInt32();
                        long next = stream.Position + size;
                        if (next > stream.Length) return null;
                        if (chunk == "fmt " && size >= 16)
                        {
                            if (reader.ReadUInt16() != 1) return null; // Our cache contains PCM WAV only.
                            reader.ReadUInt16(); reader.ReadUInt32(); bytesPerSecond = reader.ReadUInt32();
                        }
                        else if (chunk == "data" && bytesPerSecond > 0 && size > 0)
                            return new TtsResult { Success = true, AudioPath = path, DurationSeconds = size / (double)bytesPerSecond };
                        stream.Position = next + (size % 2);
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        private static void StoreCachedAudition(string source, string destination)
        {
            try
            {
                string folder = Path.GetDirectoryName(destination);
                Directory.CreateDirectory(folder);
                string temporary = destination + ".part";
                File.Copy(source, temporary, true);
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(temporary, destination);
                long bytes = 0;
                int count = 0;
                foreach (var file in new DirectoryInfo(folder).GetFiles("*.wav").OrderByDescending(f => f.LastWriteTimeUtc))
                {
                    bytes += file.Length; count++;
                    if ((count > 64 || bytes > 64L * 1024 * 1024) && !string.Equals(file.FullName, destination, StringComparison.OrdinalIgnoreCase))
                        try { file.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public static async Task<TtsResult> SynthesizeAsync(string text, string voice, ScriptBatchSettings settings,
            string outputDir, int taskId, CancellationToken ct, Action<string> progress = null, bool audioOnly = false)
        {
            if (!IsInstalled) return new TtsResult { ErrorMessage = "请在【Chatterbox 设置 / 安装】中安装本地模型。" };
            if (!string.IsNullOrWhiteSpace(settings.ChatterboxReferenceAudio)
                && (!settings.ChatterboxReferenceAuthorized || !File.Exists(settings.ChatterboxReferenceAudio)))
                return new TtsResult { ErrorMessage = "请重新选择有效的参考声音，并确认拥有使用授权。" };
            ct.ThrowIfCancellationRequested();
            ConfigureIdleTimeout(settings.ChatterboxKeepAliveMinutes);
            string language = voice != null && voice.StartsWith("chatterbox:", StringComparison.Ordinal)
                ? voice.Substring(11) : settings.ChatterboxLanguage;
            string reference = settings.ChatterboxReferenceAudio ?? "";
            double exaggeration = settings.ChatterboxExaggeration, cfg = settings.ChatterboxCfg, rate = 1;
            if (double.TryParse((settings.SpeechRate ?? "+0%").TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                rate += percent / 100;
            string cachePath = null;
            if (audioOnly)
            {
                string referenceHash = "";
                try
                {
                    if (reference.Length > 0)
                        using (var hash = SHA256.Create())
                        using (var input = File.OpenRead(reference)) referenceHash = BitConverter.ToString(hash.ComputeHash(input));
                    cachePath = AuditionCachePath(new { text, language, referenceHash, exaggeration, cfg, rate, seed = 42 });
                    var cached = ReadCachedAudition(cachePath);
                    if (cached != null) { progress?.Invoke("已复用相同声音参数的试听，立即播放。" ); return cached; }
                }
                catch (IOException) { cachePath = null; }
                catch (UnauthorizedAccessException) { cachePath = null; }
            }
            await Gate.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                if (cachePath != null)
                {
                    var cached = ReadCachedAudition(cachePath);
                    if (cached != null) { progress?.Invoke("已复用相同声音参数的试听，立即播放。"); return cached; }
                }
                Directory.CreateDirectory(outputDir);
                if (_worker == null || _worker.HasExited)
                {
                    StopWorker();
                    var start = PythonStart(Quote(ScriptPath("chatterbox_worker.py")) + " --root " + Quote(RuntimeRoot));
                    start.RedirectStandardInput = true;
                    _worker = Process.Start(start);
                    _input = new StreamWriter(_worker.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true };
                    _worker.ErrorDataReceived += (_, e) => { /* Drain logs; do not expose user scripts in UI. */ };
                    _worker.BeginErrorReadLine();
                }
                string id = Guid.NewGuid().ToString("N");
                string audio = Path.Combine(outputDir, "chatterbox_" + taskId + ".wav");
                string srt = Path.Combine(outputDir, "chatterbox_" + taskId + ".srt");
                var serializer = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 };
                var request = new
                {
                    id, text, language, reference, exaggeration, cfg,
                    rate, audio, srt, seed = 42, ffmpeg = ScriptBatchComposer.FindFfmpegExe(), audio_only = audioOnly
                };
                await _input.WriteLineAsync(serializer.Serialize(request));
                await _input.FlushAsync();
                DateTime deadline = DateTime.UtcNow.AddMinutes(30);
                while (true)
                {
                    var read = _worker.StandardOutput.ReadLineAsync();
                    while (!read.IsCompleted)
                    {
                        if (ct.IsCancellationRequested) { StopWorker(); ct.ThrowIfCancellationRequested(); }
                        if (DateTime.UtcNow > deadline) { StopWorker(); return new TtsResult { ErrorMessage = "本地配音超过 30 分钟，已停止。请缩短文案后重试。" }; }
                        await Task.Delay(100, CancellationToken.None);
                    }
                    string line = await read;
                    if (line == null) { StopWorker(); return new TtsResult { ErrorMessage = "本地配音进程退出。请检查模型安装或显存占用后重试。" }; }
                    Dictionary<string, object> message;
                    try { message = serializer.Deserialize<Dictionary<string, object>>(line); } catch { continue; }
                    if (message == null || !message.ContainsKey("id") || !string.Equals(message["id"]?.ToString(), id, StringComparison.Ordinal)) continue;
                    if (message.ContainsKey("progress")) { progress?.Invoke(message["progress"]?.ToString()); continue; }
                    bool success = message.ContainsKey("success") && Convert.ToBoolean(message["success"]);
                    var result = new TtsResult
                    {
                        Success = success && File.Exists(audio) && (audioOnly || (File.Exists(srt) && File.Exists(srt + ".words.json"))),
                        AudioPath = audio, SrtPath = audioOnly ? "" : srt,
                        DurationSeconds = message.ContainsKey("duration") ? Convert.ToDouble(message["duration"], CultureInfo.InvariantCulture) : 0,
                        ErrorMessage = message.ContainsKey("error") ? message["error"]?.ToString() : (success ? "" : "本地配音未完成")
                    };
                    if (result.Success && cachePath != null) StoreCachedAudition(audio, cachePath);
                    return result;
                }
            }
            catch (OperationCanceledException) { StopWorker(); throw; }
            catch (Exception ex) { StopWorker(); return new TtsResult { ErrorMessage = "Chatterbox: " + ex.Message }; }
            finally { _lastUseUtc = DateTime.UtcNow; Gate.Release(); }
        }

        private static async Task DownloadAsync(string url, string target, string sha256, CancellationToken ct)
        {
            if (File.Exists(target) && VerifyHash(target, sha256)) return;
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = File.Create(target + ".part"))
                    await input.CopyToAsync(output, 81920, ct);
            }
            if (!VerifyHash(target + ".part", sha256)) throw new InvalidDataException("下载文件校验失败，请重试。");
            if (File.Exists(target)) File.Delete(target);
            File.Move(target + ".part", target);
        }

        private static bool VerifyHash(string file, string expected)
        {
            using (var hash = SHA256.Create())
            using (var input = File.OpenRead(file))
                return string.Equals(BitConverter.ToString(hash.ComputeHash(input)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task RunInstallProcessAsync(string args, Action<string> log, CancellationToken ct)
        {
            using (var process = new Process { StartInfo = PythonStart(args) })
            {
                process.OutputDataReceived += (_, e) => { if (e.Data != null) log?.Invoke(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) log?.Invoke(e.Data); };
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                try { while (!process.HasExited) { ct.ThrowIfCancellationRequested(); await Task.Delay(200, ct); } }
                catch
                {
                    try
                    {
                        using (var killer = Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe"),
                            "/PID " + process.Id.ToString(CultureInfo.InvariantCulture) + " /T /F")
                            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                            killer?.WaitForExit(4000);
                    }
                    catch { try { process.Kill(); } catch { } }
                    throw;
                }
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("模型安装未完成，请查看安装日志后重试。");
            }
        }

        public static async Task InstallAsync(Action<string> log, CancellationToken ct)
        {
            await Gate.WaitAsync(ct);
            try
            {
                StopWorker();
                Directory.CreateDirectory(RuntimeRoot);
                string ready = Path.Combine(RuntimeRoot, "ready.json");
                if (File.Exists(ready)) File.Delete(ready);
                log?.Invoke("从 Python 官方下载独立运行环境…");
                string archive = Path.Combine(RuntimeRoot, "python.zip");
                await DownloadAsync("https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip", archive,
                    "4acbed6dd1c744b0376e3b1cf57ce906f9dc9e95e68824584c8099a63025a3c3", ct);
                Components.ComponentManager.ExtractSafe(archive, Path.GetDirectoryName(PythonPath));
                File.WriteAllText(Path.Combine(RuntimeRoot, "python", "python312._pth"), "python312.zip\n.\nLib\\site-packages\nimport site\n");
                string pip = Path.Combine(RuntimeRoot, "get-pip.py");
                await DownloadAsync("https://raw.githubusercontent.com/pypa/get-pip/37ae3c4daaf63a0c203176251fb99c0e238562b5/public/get-pip.py", pip,
                    "f4448a01e3ab2914c65f4ca59d4cacba6b2dbde19a483fbac46587f8bbaff424", ct);
                await RunInstallProcessAsync(Quote(pip) + " --disable-pip-version-check", log, ct);
                await RunInstallProcessAsync(Quote(ScriptPath("chatterbox_install.py")) + " --root " + Quote(RuntimeRoot), log, ct);
            }
            finally { Gate.Release(); }
        }

        public static async Task<string> ImportReferenceAsync(string source, CancellationToken ct)
        {
            string folder = Path.Combine(RuntimeRoot, "voices");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".wav");
            var start = new ProcessStartInfo(ScriptBatchComposer.FindFfmpegExe(), "-y -v error -i " + Quote(source)
                + " -t 12 -vn -ar 24000 -ac 1 -c:a pcm_s16le " + Quote(path))
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            using (var process = Process.Start(start))
            {
                var error = process.StandardError.ReadToEndAsync();
                try { while (!process.HasExited) { ct.ThrowIfCancellationRequested(); await Task.Delay(100, ct); } }
                catch { try { process.Kill(); } catch { } throw; }
                await error;
                if (process.ExitCode != 0 || !File.Exists(path) || new FileInfo(path).Length < 24000 * 2 * 3)
                    throw new InvalidOperationException("参考声音需至少 3 秒清晰人声，建议使用 6–12 秒、无背景音乐的授权音频。");
            }
            return path;
        }
    }
}
