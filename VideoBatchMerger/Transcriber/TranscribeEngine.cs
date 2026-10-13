using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VideoBatchMerger.Transcriber;

public static class TranscribeEngine
{
	private static readonly HttpClient s_httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
	private static readonly Regex s_timeRegex = new Regex(@"\[(\d{2}):(\d{2}):(\d{2})\.(\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})\.(\d{3})\]\s*(.*)", RegexOptions.Compiled);

	// ===== 模型目录：名称 / 文件名 / 下载地址 =====
	public sealed class ModelInfo
	{
		public string Key;
		public string FileName;
		public string Display;
		public string SizeText;
		public string Url => $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{FileName}";
	}

	public static readonly ModelInfo[] Models =
	{
		new ModelInfo { Key = "base",  FileName = "ggml-base.bin",  Display = "极速 Base",  SizeText = "148MB" },
		new ModelInfo { Key = "small", FileName = "ggml-small.bin", Display = "标准 Small", SizeText = "488MB" },
		new ModelInfo { Key = "turbo", FileName = "ggml-large-v3-turbo-q5_0.bin", Display = "高精 Large-v3 Turbo (显卡推荐)", SizeText = "574MB" },
	};

	public const string CudaBuildUrl = "https://github.com/ggml-org/whisper.cpp/releases/download/v1.9.0/whisper-cublas-12.4.0-bin-x64.zip";

	public static string GetCudaInstallDir() => Path.Combine(GetWhisperInstallDir(), "cuda");

	/// <summary>显卡版 whisper-cli（含 ggml-cuda.dll）路径，不存在返回空</summary>
	public static string FindCudaWhisperCliPath()
	{
		string cli = Path.Combine(GetCudaInstallDir(), "whisper-cli.exe");
		if (File.Exists(cli) && File.Exists(Path.Combine(GetCudaInstallDir(), "ggml-cuda.dll"))) return cli;
		string dCli = @"D:\工具\视频拼接\VideoBatchStudio\whisper\cuda\whisper-cli.exe";
		string dCuda = @"D:\工具\视频拼接\VideoBatchStudio\whisper\cuda\ggml-cuda.dll";
		if (File.Exists(dCli) && File.Exists(dCuda)) return dCli;
		return string.Empty;
	}

	public static bool IsCudaBuild(string cliPath)
	{
		if (string.IsNullOrEmpty(cliPath)) return false;
		string dir = Path.GetDirectoryName(cliPath) ?? "";
		return File.Exists(Path.Combine(dir, "ggml-cuda.dll"));
	}

	/// <summary>获取可用的 whisper-cli（优先显卡版 whisper\cuda，不存在则降级为 CPU 版）</summary>
	public static string FindWhisperCliPath()
	{
		string cuda = FindCudaWhisperCliPath();
		if (!string.IsNullOrEmpty(cuda) && File.Exists(cuda)) return cuda;

		string appDir = AppDomain.CurrentDomain.BaseDirectory;
		string[] candidates = new string[]
		{
			Path.Combine(appDir, "whisper", "cuda", "whisper-cli.exe"),
			@"D:\工具\视频拼接\VideoBatchStudio\whisper\cuda\whisper-cli.exe",
			Path.Combine(appDir, "whisper", "whisper-cli.exe"),
			Path.Combine(appDir, "whisper-cli.exe"),
			Path.Combine(appDir, "whisper", "main.exe"),
			@"D:\工具\视频拼接\VideoBatchStudio\whisper\whisper-cli.exe",
			@"D:\工具\视频拼接\VideoBatchStudio\whisper\main.exe"
		};

		foreach (var path in candidates)
		{
			if (File.Exists(path)) return path;
		}

		string envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
		foreach (string dir in envPath.Split(Path.PathSeparator))
		{
			try
			{
				string candidate = Path.Combine(dir.Trim(), "whisper-cli.exe");
				if (File.Exists(candidate)) return candidate;
			}
			catch { }
		}

		return string.Empty;
	}

	public static string GetWhisperInstallDir()
	{
		return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");
	}

	public static string FindModelPath(string modelName)
	{
		string appDir = AppDomain.CurrentDomain.BaseDirectory;
		string fileName = modelName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ? modelName : $"ggml-{modelName}.bin";

		string[] searchDirs = new string[]
		{
			Path.Combine(appDir, "whisper", "models"),
			Path.Combine(appDir, "whisper"),
			Path.Combine(appDir, "models"),
			@"D:\工具\视频拼接\VideoBatchStudio\whisper\models",
			@"D:\工具\视频拼接\VideoBatchStudio\whisper"
		};

		foreach (var dir in searchDirs)
		{
			string candidate = Path.Combine(dir, fileName);
			if (File.Exists(candidate)) return candidate;
		}

		return string.Empty;
	}

	public static bool IsCudaAvailable()
	{
		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = "nvidia-smi",
				Arguments = "--query-gpu=name --format=csv,noheader",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			using var proc = Process.Start(psi);
			if (proc != null)
			{
				string output = proc.StandardOutput.ReadToEnd();
				proc.WaitForExit(3000);
				return !string.IsNullOrWhiteSpace(output) && output.Contains("NVIDIA");
			}
		}
		catch { }
		return false;
	}

	/// <summary>流式下载大文件（写入 .part，完成后改名），带进度日志</summary>
	public static async Task DownloadFileAsync(string url, string destPath, Action<string> logCallback, CancellationToken ct)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(destPath));
		string part = destPath + ".part";
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		using var client = new HttpClient { Timeout = TimeSpan.FromHours(2) };
		client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 VideoBatchStudio");
		using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
		resp.EnsureSuccessStatusCode();
		long total = resp.Content.Headers.ContentLength ?? -1;
		using (var src = await resp.Content.ReadAsStreamAsync())
		using (var dst = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
		{
			byte[] buf = new byte[1 << 16];
			long done = 0;
			int lastPct = -1;
			int n;
			while ((n = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0)
			{
				await dst.WriteAsync(buf, 0, n, ct);
				done += n;
				if (total > 0)
				{
					int pct = (int)(done * 100 / total);
					if (pct / 10 != lastPct / 10)
					{
						lastPct = pct;
						logCallback?.Invoke($"[下载进度] {Path.GetFileName(destPath)}  {pct}%  ({done / 1048576}MB / {total / 1048576}MB)");
					}
				}
			}
		}
		if (File.Exists(destPath)) File.Delete(destPath);
		File.Move(part, destPath);
	}

	/// <summary>下载并安装 NVIDIA 显卡加速版 whisper 引擎到 whisper\cuda</summary>
	public static async Task<bool> InstallCudaBuildAsync(Action<string> logCallback, CancellationToken ct)
	{
		string cudaDir = GetCudaInstallDir();
		string zip = Path.Combine(GetWhisperInstallDir(), "_cuda_dl.zip");
		string tmpExtract = Path.Combine(GetWhisperInstallDir(), "_cuda_extract");
		try
		{
			if (!File.Exists(zip) || new FileInfo(zip).Length < 10_000_000)
			{
				logCallback?.Invoke("[显卡引擎] 正在下载 NVIDIA CUDA 加速版 Whisper 引擎...");
				await DownloadFileAsync(CudaBuildUrl, zip, logCallback, ct);
			}
			if (Directory.Exists(tmpExtract)) Directory.Delete(tmpExtract, true);
			Directory.CreateDirectory(tmpExtract);
			string fullTmpExtract = Path.GetFullPath(tmpExtract);
			if (!fullTmpExtract.EndsWith(Path.DirectorySeparatorChar.ToString()))
				fullTmpExtract += Path.DirectorySeparatorChar;

			using (var archive = System.IO.Compression.ZipFile.OpenRead(zip))
			{
				foreach (var entry in archive.Entries)
				{
					string entryDest = Path.GetFullPath(Path.Combine(fullTmpExtract, entry.FullName));
					if (!entryDest.StartsWith(fullTmpExtract, StringComparison.OrdinalIgnoreCase))
					{
						throw new InvalidOperationException("检测到异常压缩包路径，存在 Zip Slip 路径穿越风险");
					}
					if (string.IsNullOrEmpty(entry.Name))
					{
						Directory.CreateDirectory(entryDest);
						continue;
					}
					Directory.CreateDirectory(Path.GetDirectoryName(entryDest));
					using (var es = entry.Open())
					using (var os = File.Create(entryDest))
					{
						es.CopyTo(os);
					}
				}
			}
			string cli = Directory.GetFiles(tmpExtract, "whisper-cli.exe", SearchOption.AllDirectories).FirstOrDefault();
			if (cli == null) throw new InvalidOperationException("压缩包中未找到 whisper-cli.exe");
			string srcDir = Path.GetDirectoryName(cli);
			Directory.CreateDirectory(cudaDir);
			foreach (var f in Directory.GetFiles(srcDir))
			{
				File.Copy(f, Path.Combine(cudaDir, Path.GetFileName(f)), true);
			}
			logCallback?.Invoke("[显卡引擎] CUDA 加速引擎安装完成。");
			return !string.IsNullOrEmpty(FindCudaWhisperCliPath());
		}
		catch (Exception ex)
		{
			logCallback?.Invoke($"[显卡引擎安装失败] {ex.Message}（将继续使用 CPU 模式）");
			return false;
		}
		finally
		{
			try { if (Directory.Exists(tmpExtract)) Directory.Delete(tmpExtract, true); } catch { }
			try { if (File.Exists(zip)) File.Delete(zip); } catch { }
		}
	}

	public sealed class OnlineAudioResult
	{
		public string TempDir;
		public string MediaPath;
		public string Title;
	}

	/// <summary>
	/// 在线链接只拉取“音频流”到临时目录（不保存视频到下载文件夹），转录后由调用方删除 TempDir。
	/// TikTok 的独立音频流可能只有平台背景音乐，因此 TikTok 取“音画合一”的最小流，保证包含人声。
	/// </summary>
	public static async Task<OnlineAudioResult> FetchOnlineAudioAsync(string url, string ytDlpPath, string ffmpegPath, Action<string> logCallback, CancellationToken ct)
	{
		if (string.IsNullOrEmpty(ytDlpPath) || !File.Exists(ytDlpPath))
			throw new InvalidOperationException("未找到 yt-dlp.exe 组件");

		string tempDir = Path.Combine(Path.GetTempPath(), "VBS_Transcribe", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);

		bool isTikTok = url.IndexOf("tiktok.com", StringComparison.OrdinalIgnoreCase) >= 0;
		string format = isTikTok
			? "w[acodec!=none][vcodec!=none]/b[acodec!=none]/b"
			: "ba[acodec!=none]/w[acodec!=none]/b";

		var sb = new StringBuilder();
		sb.Append("--no-playlist --newline --encoding UTF-8 --no-simulate --progress ");
		sb.Append("--retries 10 --fragment-retries 10 ");
		sb.Append("--extractor-args \"youtube:player_client=default,mweb,web\" --js-runtimes node ");
		sb.Append($"-f \"{format}\" ");
		sb.Append("--print \"before_dl:VBS_TITLE:%(title)s\" ");
		sb.Append("--print \"after_move:VBS_FILE:%(filepath)s\" ");
		sb.Append($"--output \"{Path.Combine(tempDir, "media.%(ext)s")}\" ");
		if (!string.IsNullOrWhiteSpace(ffmpegPath) && File.Exists(ffmpegPath))
			sb.Append($"--ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\" ");
		string cleanUrl = (url ?? string.Empty).Replace("\"", "").Replace("\r", "").Replace("\n", "").Trim();
		sb.Append($"-- \"{cleanUrl}\"");

		var result = new OnlineAudioResult { TempDir = tempDir };
		string lastErr = "";
		var psi = new ProcessStartInfo
		{
			FileName = ytDlpPath,
			Arguments = sb.ToString(),
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};
		using var proc = new Process { StartInfo = psi };
		int lastPct = -1;
		proc.OutputDataReceived += (s, e) =>
		{
			if (string.IsNullOrWhiteSpace(e.Data)) return;
			string line = e.Data.Trim();
			if (line.StartsWith("VBS_TITLE:")) { result.Title = line.Substring(10).Trim(); logCallback?.Invoke($"[在线解析] {result.Title}"); return; }
			if (line.StartsWith("VBS_FILE:")) { result.MediaPath = line.Substring(9).Trim(); return; }
			var m = Regex.Match(line, @"\[download\]\s+(\d+(?:\.\d+)?)%");
			if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double p))
			{
				int pct = (int)p;
				if (pct / 25 != lastPct / 25) { lastPct = pct; logCallback?.Invoke($"[音频拉取] {pct}%"); }
			}
		};
		proc.ErrorDataReceived += (s, e) =>
		{
			if (string.IsNullOrWhiteSpace(e.Data)) return;
			if (e.Data.Contains("ERROR")) { lastErr = e.Data.Trim(); logCallback?.Invoke($"[STDERR] {lastErr}"); }
		};
		proc.Start();
		proc.BeginOutputReadLine();
		proc.BeginErrorReadLine();
		using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
		{
			await Task.Run(() => proc.WaitForExit(), ct);
		}

		if (string.IsNullOrEmpty(result.MediaPath) || !File.Exists(result.MediaPath))
		{
			// 兜底：临时目录是独立的，里面只有本任务的文件，不会误取其他视频
			result.MediaPath = Directory.GetFiles(tempDir)
				.Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase))
				.OrderByDescending(f => new FileInfo(f).Length)
				.FirstOrDefault();
		}

		if (proc.ExitCode != 0 || string.IsNullOrEmpty(result.MediaPath))
		{
			TryDeleteDir(tempDir);
			throw new InvalidOperationException(string.IsNullOrEmpty(lastErr) ? $"在线音频拉取失败 (退出码 {proc.ExitCode})" : lastErr);
		}

		if (string.IsNullOrWhiteSpace(result.Title)) result.Title = url;
		long kb = new FileInfo(result.MediaPath).Length / 1024;
		logCallback?.Invoke($"[音频拉取完成] 临时音频 {kb}KB（转录后自动删除，不占用磁盘）");
		return result;
	}

	public static void TryDeleteDir(string dir)
	{
		try { if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
	}

	/// <summary>清理以往异常退出残留的临时转录文件</summary>
	public static void CleanupStaleTempFiles()
	{
		try
		{
			string root = Path.Combine(Path.GetTempPath(), "VBS_Transcribe");
			if (Directory.Exists(root))
			{
				foreach (var d in Directory.GetDirectories(root))
				{
					if (Directory.GetCreationTime(d) < DateTime.Now.AddHours(-6)) TryDeleteDir(d);
				}
			}
			foreach (var f in Directory.GetFiles(Path.GetTempPath(), "whisper_audio_*.wav"))
			{
				try { if (File.GetCreationTime(f) < DateTime.Now.AddHours(-6)) File.Delete(f); } catch { }
			}
		}
		catch { }
	}

	public static async Task<string> Extract16kWavAsync(string inputMedia, string ffmpegPath, Action<string> logCallback, CancellationToken ct)
	{
		if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
		{
			ffmpegPath = Downloader.DownloadEngine.FindFFmpegPath();
		}

		string tempWav = Path.Combine(Path.GetTempPath(), $"whisper_audio_{Guid.NewGuid():N}.wav");
		logCallback?.Invoke($"[音频提取] 正在提取 16kHz 高精单声道音轨: {Path.GetFileName(inputMedia)}");

		var psi = new ProcessStartInfo
		{
			FileName = ffmpegPath,
			Arguments = $"-nostdin -threads 4 -i {QuoteArg(inputMedia)} -vn -ar 16000 -ac 1 -c:a pcm_s16le {QuoteArg(tempWav)} -y",
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = false,
			RedirectStandardError = true,
			StandardErrorEncoding = Encoding.UTF8
		};

		using var proc = new Process { StartInfo = psi };
		var errSb = new StringBuilder();
		proc.ErrorDataReceived += (s, e) =>
		{
			if (!string.IsNullOrWhiteSpace(e.Data))
			{
				errSb.AppendLine(e.Data);
			}
		};

		proc.Start();
		proc.BeginErrorReadLine();

		using (ct.Register(() =>
		{
			try { if (!proc.HasExited) proc.Kill(); } catch { }
		}))
		{
			await Task.Run(() => proc.WaitForExit(), ct);
		}

		if (proc.ExitCode == 0 && File.Exists(tempWav))
		{
			long sizeKb = new FileInfo(tempWav).Length / 1024;
			logCallback?.Invoke($"[音频提取完成] 音轨提取就绪 ({sizeKb}KB): {Path.GetFileName(tempWav)}");
			return tempWav;
		}

		string errDetail = errSb.ToString().Trim();
		if (errDetail.Length > 200) errDetail = errDetail.Substring(errDetail.Length - 200);
		throw new InvalidOperationException(string.IsNullOrEmpty(errDetail) ? $"FFmpeg 音频提取失败 (退出码 {proc.ExitCode})" : errDetail);
	}

	public static async Task<List<SubtitleSegment>> TranscribeAsync(
		string audioWavPath,
		string whisperCliPath,
		string modelPath,
		string language,
		Action<string> logCallback,
		CancellationToken ct,
		Action<int> progressCallback = null)
	{
		var segments = new List<SubtitleSegment>();
		string whisperDir = Path.GetDirectoryName(whisperCliPath);

		string relativeModel = modelPath;
		try
		{
			string dirWithSlash = whisperDir.EndsWith("\\") || whisperDir.EndsWith("/") ? whisperDir : whisperDir + "\\";
			Uri fromUri = new Uri(dirWithSlash);
			Uri toUri = new Uri(modelPath);
			if (string.Equals(fromUri.Scheme, toUri.Scheme, StringComparison.OrdinalIgnoreCase))
			{
				Uri relUri = fromUri.MakeRelativeUri(toUri);
				string relStr = Uri.UnescapeDataString(relUri.ToString()).Replace('/', '\\');
				if (!string.IsNullOrEmpty(relStr))
				{
					relativeModel = relStr;
				}
			}
		}
		catch { }

		string langArg = string.IsNullOrWhiteSpace(language) || language.Equals("auto", StringComparison.OrdinalIgnoreCase)
			? "-l auto"
			: $"-l {language}";

		var args = new StringBuilder();
		args.Append($"-m {QuoteArg(relativeModel)} ");
		args.Append($"-f {QuoteArg(audioWavPath)} ");
		args.Append($"{langArg} ");
		// -pp 打印进度，-sns 抑制非语音字符，-nth 0.60 过滤无声与背景杂音
		args.Append("-pp --no-prints -sns -nth 0.60 ");

		logCallback?.Invoke($"[Whisper 转录启动] 启动本地模型推理内核: {Path.GetFileName(modelPath)}");

		var psi = new ProcessStartInfo
		{
			FileName = whisperCliPath,
			Arguments = args.ToString(),
			WorkingDirectory = whisperDir,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};

		using var proc = new Process { StartInfo = psi };

		proc.OutputDataReceived += (s, e) =>
		{
			if (string.IsNullOrWhiteSpace(e.Data)) return;
			string line = e.Data.Trim();

			var match = s_timeRegex.Match(line);
			if (match.Success)
			{
				try
				{
					int sh = int.Parse(match.Groups[1].Value);
					int sm = int.Parse(match.Groups[2].Value);
					int ss = int.Parse(match.Groups[3].Value);
					int sms = int.Parse(match.Groups[4].Value);

					int eh = int.Parse(match.Groups[5].Value);
					int em = int.Parse(match.Groups[6].Value);
					int es = int.Parse(match.Groups[7].Value);
					int ems = int.Parse(match.Groups[8].Value);

					string text = match.Groups[9].Value.Trim();
					if (IsHallucinationOrJunk(text))
					{
						return; // 过滤重复字符乱码与无声幻觉
					}

					lock (segments)
					{
						segments.Add(new SubtitleSegment
						{
							Index = segments.Count + 1,
							StartTime = new TimeSpan(0, sh, sm, ss, sms),
							EndTime = new TimeSpan(0, eh, em, es, ems),
							OriginalText = text
						});
					}
					logCallback?.Invoke($"[{match.Groups[1].Value}:{match.Groups[2].Value}:{match.Groups[3].Value}] {text}");
				}
				catch { }
			}
		};

		proc.ErrorDataReceived += (s, e) =>
		{
			if (string.IsNullOrWhiteSpace(e.Data)) return;
			string line = e.Data.Trim();
			var pm = Regex.Match(line, @"progress\s*=\s*(\d+)%");
			if (pm.Success && int.TryParse(pm.Groups[1].Value, out int pVal))
			{
				progressCallback?.Invoke(pVal);
			}
			if (line.Contains("progress") || line.Contains("error") || line.Contains("warning"))
			{
				logCallback?.Invoke($"[引擎内核] {line}");
			}
		};

		proc.Start();
		proc.BeginOutputReadLine();
		proc.BeginErrorReadLine();

		using (ct.Register(() =>
		{
			try { if (!proc.HasExited) proc.Kill(); } catch { }
		}))
		{
			await Task.Run(() => proc.WaitForExit(), ct);
		}

		logCallback?.Invoke($"[转录结束] 成功提取到 {segments.Count} 句原生字幕");
		return segments;
	}

	public static bool IsHallucinationOrJunk(string s)
	{
		if (string.IsNullOrWhiteSpace(s)) return true;
		string trimmed = s.Trim();
		if (trimmed.Length == 1 && (trimmed == "." || trimmed == "," || trimmed == "。" || trimmed == "，" || trimmed == "-")) return true;

		// 1. 单字符重复5次以上 (如 ᶠᶠᶠᶠᶠ, ......., vvvvv, වවවව)
		if (Regex.IsMatch(trimmed, @"(.)\1{4,}")) return true;

		// 2. 短词重复3次以上 (如 you you you you, the the the the)
		if (Regex.IsMatch(trimmed, @"(\b\w+\b\s*)\1{3,}", RegexOptions.IgnoreCase)) return true;

		// 3. 静音或无声片段的经典幻觉词
		string lower = trimmed.ToLowerInvariant();
		if (lower == "ご視聴ありがとうございました" ||
			lower == "[끝]" ||
			lower == "[subscribe]" ||
			lower == "subscribe" ||
			lower == "thank you for watching" ||
			lower == "thanks for watching" ||
			lower == "subtitles by" ||
			lower == "you")
		{
			return true;
		}

		return false;
	}

	public static string FormatAmbientSound(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return string.Empty;
		string lower = text.ToLowerInvariant().Trim();
		if (lower.Contains("music") || lower == "[music]") return "[背景音乐]";
		if (lower.Contains("birds chirping") || lower.Contains("birds")) return "(鸟鸣环境音)";
		if (lower.Contains("child crying") || lower.Contains("crying")) return "(婴儿哭声)";
		if (lower.Contains("airplane") || lower.Contains("engine roaring") || lower.Contains("roaring")) return "(引擎轰鸣声)";
		if (lower.Contains("applause")) return "(掌声)";
		if (lower.Contains("laughter")) return "(笑声)";
		if (lower.Contains("cheering")) return "(欢呼声)";
		if (lower.Contains("silence")) return "(静音)";
		return string.Empty;
	}

	public static async Task<string> TranslateToChineseAsync(string text, string sourceLanguage = "auto")
	{
		if (string.IsNullOrWhiteSpace(text)) return string.Empty;

		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			string srcLang = sourceLanguage?.ToLowerInvariant() switch
			{
				"english" or "en" => "en",
				"portuguese" or "pt" => "pt",
				"spanish" or "es" => "es",
				"french" or "fr" => "fr",
				"german" or "de" => "de",
				"russian" or "ru" => "ru",
				"japanese" or "ja" => "ja",
				_ => "auto"
			};

			string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={srcLang}&tl=zh-CN&dt=t&q={Uri.EscapeDataString(text)}";
			var request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

			var response = await s_httpClient.SendAsync(request);
			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
				object parsed = serializer.DeserializeObject(json);
				if (parsed is System.Collections.IList root && root.Count > 0)
				{
					if (root[0] is System.Collections.IList sentences)
					{
						var sb = new StringBuilder();
						foreach (object item in sentences)
						{
							if (item is System.Collections.IList partList && partList.Count > 0)
							{
								string part = partList[0] as string;
								if (!string.IsNullOrEmpty(part)) sb.Append(part);
							}
						}
						string translated = sb.ToString().Trim();
						if (!string.IsNullOrEmpty(translated)) return translated;
					}
				}
			}
		}
		catch { }

		return text;
	}

	public static async Task TranslateSegmentsAsync(List<SubtitleSegment> segments, string sourceLanguage, Action<string> logCallback, CancellationToken ct)
	{
		if (segments == null || segments.Count == 0) return;
		logCallback?.Invoke($"[AI智能翻译] 正在翻译 {segments.Count} 句字幕为简体中文 (支持英文/葡萄牙语/西班牙语)...");

		int batchSize = 10;
		for (int i = 0; i < segments.Count; i += batchSize)
		{
			if (ct.IsCancellationRequested) break;
			var chunk = segments.Skip(i).Take(batchSize).ToList();

			// 优先映射环境音标
			foreach (var seg in chunk)
			{
				string ambient = FormatAmbientSound(seg.OriginalText);
				if (!string.IsNullOrEmpty(ambient)) seg.TranslatedText = ambient;
			}

			var needTrans = chunk.Where(s => string.IsNullOrEmpty(s.TranslatedText)).ToList();
			if (needTrans.Count > 0)
			{
				var combinedText = string.Join("\n", needTrans.Select(s => s.OriginalText));
				string translatedCombined = await TranslateToChineseAsync(combinedText, sourceLanguage);
				string[] translatedLines = translatedCombined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

				if (translatedLines.Length == needTrans.Count)
				{
					for (int j = 0; j < needTrans.Count; j++)
					{
						needTrans[j].TranslatedText = translatedLines[j].Trim();
					}
				}
				else
				{
					foreach (var seg in needTrans)
					{
						if (ct.IsCancellationRequested) break;
						seg.TranslatedText = await TranslateToChineseAsync(seg.OriginalText, sourceLanguage);
						await Task.Delay(40, ct);
					}
				}
			}

			logCallback?.Invoke($"[翻译进度] 已完成: {Math.Min(i + batchSize, segments.Count)} / {segments.Count} 句");
			await Task.Delay(50, ct);
		}

		logCallback?.Invoke("[AI智能翻译完成] 全部中文译文字幕对照就绪！");
	}

	public static string ExportToSrt(IEnumerable<SubtitleSegment> segments, bool includeOriginal, bool includeTranslated)
	{
		var sb = new StringBuilder();
		int idx = 1;
		foreach (var seg in segments)
		{
			sb.AppendLine(idx.ToString());
			sb.AppendLine($"{FormatSrtTime(seg.StartTime)} --> {FormatSrtTime(seg.EndTime)}");

			if (includeOriginal && !string.IsNullOrWhiteSpace(seg.OriginalText))
			{
				sb.AppendLine(seg.OriginalText);
			}
			if (includeTranslated && !string.IsNullOrWhiteSpace(seg.TranslatedText))
			{
				sb.AppendLine(seg.TranslatedText);
			}
			sb.AppendLine();
			idx++;
		}
		return sb.ToString();
	}

	public static string ExportToTxt(IEnumerable<SubtitleSegment> segments, bool includeOriginal, bool includeTranslated)
	{
		var sb = new StringBuilder();
		foreach (var seg in segments)
		{
			if (includeOriginal && includeTranslated)
			{
				sb.AppendLine($"[{seg.TimeRangeStr}]");
				sb.AppendLine($"原: {seg.OriginalText}");
				sb.AppendLine($"译: {seg.TranslatedText}");
				sb.AppendLine();
			}
			else if (includeTranslated)
			{
				sb.AppendLine(seg.TranslatedText);
			}
			else
			{
				sb.AppendLine(seg.OriginalText);
			}
		}
		return sb.ToString();
	}

	private static string FormatSrtTime(TimeSpan ts)
	{
		return string.Format("{0:00}:{1:00}:{2:00},{3:000}",
			(int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds);
	}

	public static string QuoteArg(string value)
	{
		if (string.IsNullOrEmpty(value)) return "\"\"";
		var sb = new StringBuilder("\"", value.Length + 16);
		for (int i = 0; i < value.Length; i++)
		{
			int backslashCount = 0;
			while (i < value.Length && value[i] == '\\')
			{
				backslashCount++;
				i++;
			}
			if (i == value.Length)
			{
				sb.Append('\\', backslashCount * 2);
				break;
			}
			if (value[i] == '"')
			{
				sb.Append('\\', backslashCount * 2 + 1);
				sb.Append('"');
			}
			else
			{
				sb.Append('\\', backslashCount);
				sb.Append(value[i]);
			}
		}
		sb.Append('"');
		return sb.ToString();
	}
}
