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
		new ModelInfo { Key = "large", FileName = "ggml-large-v3-q5_0.bin", Display = "最高精度 Large-v3 (口音/噪音最稳)", SizeText = "1.08GB" },
	};

	public const string CudaBuildUrl = "https://github.com/ggml-org/whisper.cpp/releases/download/v1.9.0/whisper-cublas-12.4.0-bin-x64.zip";
	public const string CpuBuildUrl = "https://github.com/ggml-org/whisper.cpp/releases/download/v1.9.0/whisper-bin-x64.zip";

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

	/// <summary>流式下载大文件（写入 .part，完成后改名），带进度日志并全兼容包括 HTTP 308 在内的重定向</summary>
	public static async Task DownloadFileAsync(string url, string destPath, Action<string> logCallback, CancellationToken ct)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(destPath));
		string part = destPath + ".part";
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

		using var handler = new HttpClientHandler
		{
			AllowAutoRedirect = false, // 显式禁用内置重定向，以兼容 .NET 4.8 原生不支持的 HTTP 308 (Permanent Redirect)
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
		};
		using var client = new HttpClient(handler) { Timeout = TimeSpan.FromHours(2) };
		client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 VideoBatchStudio");

		string currentUrl = url;
		HttpResponseMessage resp = null;
		int redirectCount = 0;
		const int maxRedirects = 10;

		try
		{
			while (true)
			{
				ct.ThrowIfCancellationRequested();

				var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
				resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

				int statusCode = (int)resp.StatusCode;
				if (statusCode == 301 || statusCode == 302 || statusCode == 303 || statusCode == 307 || statusCode == 308)
				{
					if (++redirectCount > maxRedirects)
					{
						throw new InvalidOperationException($"超过最大重定向次数限制 ({maxRedirects})");
					}

					var location = resp.Headers.Location;
					if (location == null)
					{
						throw new InvalidOperationException($"HTTP {statusCode} 重定向未提供有效 Location 标头");
					}

					Uri nextUri = location.IsAbsoluteUri ? location : new Uri(new Uri(currentUrl), location);
					currentUrl = nextUri.ToString();

					resp.Dispose();
					resp = null;
					continue;
				}

				resp.EnsureSuccessStatusCode();
				break;
			}

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
		catch
		{
			try { if (File.Exists(part)) File.Delete(part); } catch { }
			throw;
		}
		finally
		{
			resp?.Dispose();
		}
	}

	/// <summary>多镜像容灾大文件流式下载（自动尝试高速加速镜像与官方直连）</summary>
	public static async Task<bool> DownloadWithMirrorAsync(string primaryUrl, string destPath, Action<string> logCallback, CancellationToken ct)
	{
		var urls = new List<string>();
		if (primaryUrl.Contains("github.com"))
		{
			urls.Add($"https://ghfast.top/{primaryUrl}");
			urls.Add($"https://ghproxy.net/{primaryUrl}");
			urls.Add(primaryUrl);
		}
		else if (primaryUrl.Contains("huggingface.co") || primaryUrl.Contains("hf-mirror.com"))
		{
			string hfMirror = primaryUrl.Contains("hf-mirror.com")
				? primaryUrl
				: primaryUrl.Replace("huggingface.co", "hf-mirror.com");
			string hfOfficial = primaryUrl.Contains("huggingface.co")
				? primaryUrl
				: primaryUrl.Replace("hf-mirror.com", "huggingface.co");

			urls.Add(hfMirror);
			urls.Add(hfOfficial);
		}
		else
		{
			urls.Add(primaryUrl);
		}

		foreach (var u in urls)
		{
			if (ct.IsCancellationRequested) break;
			try
			{
				string host = "";
				try { host = new Uri(u).Host; } catch { host = u; }
				logCallback?.Invoke($"[网络连接] 正在连接下载通道: {host} ...");
				await DownloadFileAsync(u, destPath, logCallback, ct);
				if (File.Exists(destPath) && new FileInfo(destPath).Length > 10000)
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				logCallback?.Invoke($"[通道切换] 下载通道异常 ({ex.Message})，正在自动切换备用加速通道...");
			}
		}

		return File.Exists(destPath) && new FileInfo(destPath).Length > 10000;
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
				logCallback?.Invoke("[显卡引擎] 正在下载 NVIDIA CUDA 加速版 Whisper 引擎 (约 60MB)...");
				bool ok = await DownloadWithMirrorAsync(CudaBuildUrl, zip, logCallback, ct);
				if (!ok) throw new InvalidOperationException("显卡加速引擎下载未完成，请检查网络");
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
			logCallback?.Invoke("[显卡引擎] CUDA 加速引擎安装完成！");
			return !string.IsNullOrEmpty(FindCudaWhisperCliPath());
		}
		catch (Exception ex)
		{
			logCallback?.Invoke($"[显卡引擎安装失败] {ex.Message}（自动降级为通用 CPU 模式）");
			return false;
		}
		finally
		{
			try { if (Directory.Exists(tmpExtract)) Directory.Delete(tmpExtract, true); } catch { }
			try { if (File.Exists(zip)) File.Delete(zip); } catch { }
		}
	}

	/// <summary>下载并安装通用 CPU 版 whisper 离线推理引擎到 whisper 目录（全机型免显卡兼容）</summary>
	public static async Task<bool> InstallCpuBuildAsync(Action<string> logCallback, CancellationToken ct)
	{
		string whisperDir = GetWhisperInstallDir();
		string zip = Path.Combine(whisperDir, "_cpu_dl.zip");
		string tmpExtract = Path.Combine(whisperDir, "_cpu_extract");
		try
		{
			if (!File.Exists(zip) || new FileInfo(zip).Length < 3_000_000)
			{
				logCallback?.Invoke("[CPU引擎] 正在下载通用 CPU 版 Whisper 离线推理引擎 (约 15MB)...");
				bool ok = await DownloadWithMirrorAsync(CpuBuildUrl, zip, logCallback, ct);
				if (!ok) throw new InvalidOperationException("CPU 引擎下载未能成功完成，请检查网络连接");
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

			string cli = Directory.GetFiles(tmpExtract, "whisper-cli.exe", SearchOption.AllDirectories).FirstOrDefault()
				?? Directory.GetFiles(tmpExtract, "main.exe", SearchOption.AllDirectories).FirstOrDefault();
			if (cli == null) throw new InvalidOperationException("压缩包中未找到 whisper-cli.exe 或 main.exe");

			string srcDir = Path.GetDirectoryName(cli);
			Directory.CreateDirectory(whisperDir);
			foreach (var f in Directory.GetFiles(srcDir))
			{
				File.Copy(f, Path.Combine(whisperDir, Path.GetFileName(f)), true);
			}

			string mainExe = Path.Combine(whisperDir, "main.exe");
			string whisperCliExe = Path.Combine(whisperDir, "whisper-cli.exe");
			if (File.Exists(mainExe) && !File.Exists(whisperCliExe))
			{
				File.Copy(mainExe, whisperCliExe, true);
			}

			logCallback?.Invoke("[CPU引擎] 通用 CPU 推理引擎已安装就绪！");
			return !string.IsNullOrEmpty(FindWhisperCliPath());
		}
		catch (Exception ex)
		{
			logCallback?.Invoke($"[CPU引擎安装失败] {ex.Message}");
			return false;
		}
		finally
		{
			try { if (Directory.Exists(tmpExtract)) Directory.Delete(tmpExtract, true); } catch { }
			try { if (File.Exists(zip)) File.Delete(zip); } catch { }
		}
	}

	/// <summary>高速下载多语种 AI 语音识别模型</summary>
	public static async Task<bool> DownloadModelAsync(string modelKeyOrFileName, Action<string> logCallback, CancellationToken ct)
	{
		string fileName = modelKeyOrFileName;
		if (!fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
		{
			var m = Models.FirstOrDefault(x => x.Key.Equals(modelKeyOrFileName, StringComparison.OrdinalIgnoreCase));
			if (m != null) fileName = m.FileName;
			else fileName = $"ggml-{modelKeyOrFileName}.bin";
		}

		string modelsDir = Path.Combine(GetWhisperInstallDir(), "models");
		Directory.CreateDirectory(modelsDir);
		string dest = Path.Combine(modelsDir, fileName);

		if (File.Exists(dest) && new FileInfo(dest).Length > 10_000_000)
		{
			logCallback?.Invoke($"[模型检测] 本地已存在模型文件: {fileName}");
			return true;
		}

		logCallback?.Invoke($"[模型拉取] 正在从高速镜像拉取 {fileName} 语音模型...");
		string url = $"https://hf-mirror.com/ggerganov/whisper.cpp/resolve/main/{fileName}";
		bool ok = await DownloadWithMirrorAsync(url, dest, logCallback, ct);
		if (ok && File.Exists(dest) && new FileInfo(dest).Length > 10_000_000)
		{
			logCallback?.Invoke($"[模型就绪] {fileName} 下载并校验成功！");
			return true;
		}
		return false;
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

		string normLang = NormalizeWhisperLanguage(language);
		string langArg = string.IsNullOrWhiteSpace(normLang) || normLang.Equals("auto", StringComparison.OrdinalIgnoreCase)
			? "-l auto"
			: $"-l {normLang}";

		var args = new StringBuilder();
		args.Append($"-m {QuoteArg(relativeModel)} ");
		args.Append($"-f {QuoteArg(audioWavPath)} ");
		args.Append($"{langArg} ");
		// -pp 打印进度，-sns 抑制非语音字符，-nth 0.85 提升人声识别容错率，防止漏识别轻声/连读/背景音下的人声
		args.Append("-pp --no-prints -sns -nth 0.85 ");

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

	// ======================================================================
	// 逐词时间戳转录（-ojf）：句子按标点/停顿/字数重新断句，字幕与口型对齐
	// ======================================================================

	/// <summary>模型 Key（large / turbo / small / base）对应文件查找（修复 turbo 文件名与 key 不一致）</summary>
	public static string FindModelPathByKey(string key)
	{
		if (string.IsNullOrWhiteSpace(key)) return string.Empty;
		var m = Models.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
		return FindModelPath(m != null ? m.FileName : key);
	}

	/// <summary>按精度从高到低挑选本机已有的最佳模型</summary>
	public static string FindBestAvailableModelPath(out string modelKey)
	{
		foreach (var key in new[] { "large", "turbo", "small", "base" })
		{
			string p = FindModelPathByKey(key);
			if (!string.IsNullOrEmpty(p) && File.Exists(p))
			{
				modelKey = key;
				return p;
			}
		}
		modelKey = string.Empty;
		return string.Empty;
	}

	/// <summary>标准化语种代号为 Whisper CLI 原生支持的 2 位 ISO 语言代码（例如将 pt-ao 规范化为 pt，避免 whisper-cli 报错未知语种）</summary>
	public static string NormalizeWhisperLanguage(string lang)
	{
		if (string.IsNullOrWhiteSpace(lang)) return "auto";
		string lower = lang.Trim().ToLowerInvariant();
		if (lower == "pt-ao" || lower == "pt_ao" || lower == "ao") return "pt";
		if (lower == "zh-cn" || lower == "zh_cn" || lower == "cn") return "zh";
		if (lower == "zh-tw" || lower == "zh_tw") return "zh";
		if (lower == "en-us" || lower == "en-gb") return "en";
		return lower;
	}

	/// <summary>构造初始提示词：优先使用用户标准文案（专有名词/地名/人名），否则给葡语(安哥拉/莫桑比克)默认提示</summary>
	public static string BuildInitialPrompt(string langCode, string userScript)
	{
		if (!string.IsNullOrWhiteSpace(userScript))
		{
			string one = Regex.Replace(userScript.Trim(), @"\s+", " ");
			return one.Length > 320 ? one.Substring(0, 320) : one;
		}
		if (string.Equals(langCode, "pt", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(langCode, "pt-ao", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(langCode, "ao", StringComparison.OrdinalIgnoreCase))
		{
			return "Conversa em português de Angola e Moçambique, com pontuação correta.";
		}
		return string.Empty;
	}

	// whisper-cli 通过 ANSI 命令行接收参数：仅保留能被系统代码页无损编码的词，避免提示词乱码误导模型
	private static string SanitizePromptForAnsi(string prompt)
	{
		if (string.IsNullOrWhiteSpace(prompt)) return string.Empty;
		var enc = Encoding.Default;
		var keep = new List<string>();
		foreach (var w in prompt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
		{
			try
			{
				if (enc.GetString(enc.GetBytes(w)) == w) keep.Add(w);
			}
			catch { }
		}
		return string.Join(" ", keep).Trim();
	}

	private static string ToRelativeModelPath(string modelPath, string whisperDir)
	{
		try
		{
			string dirWithSlash = whisperDir.EndsWith("\\") || whisperDir.EndsWith("/") ? whisperDir : whisperDir + "\\";
			Uri fromUri = new Uri(dirWithSlash);
			Uri toUri = new Uri(modelPath);
			if (string.Equals(fromUri.Scheme, toUri.Scheme, StringComparison.OrdinalIgnoreCase))
			{
				string relStr = Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString()).Replace('/', '\\');
				if (!string.IsNullOrEmpty(relStr)) return relStr;
			}
		}
		catch { }
		return modelPath;
	}

	private static readonly Regex s_jsonTokenRegex = new Regex(
		"\"text\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\",\\s*\"timestamps\":\\s*\\{[^}]*\\},\\s*\"offsets\":\\s*\\{\\s*\"from\":\\s*(-?\\d+),\\s*\"to\":\\s*(-?\\d+)\\s*\\},\\s*\"id\":\\s*(\\d+),\\s*\"p\":\\s*(-?[0-9.eE+\\-]+)",
		RegexOptions.Compiled);

	private sealed class RawToken
	{
		public byte[] Bytes;
		public int From;
		public int To;
		public double P;
	}

	// JSON 文件按 Latin-1 读取以保留原始字节，再按“词”整体做 UTF-8 解码，避免多字节字符被拆在两个 token 中变成乱码
	private static byte[] UnescapeJsonToBytes(string latin1)
	{
		var buf = new List<byte>(latin1.Length);
		for (int i = 0; i < latin1.Length; i++)
		{
			char c = latin1[i];
			if (c != '\\' || i + 1 >= latin1.Length)
			{
				buf.Add((byte)c);
				continue;
			}
			char n = latin1[++i];
			switch (n)
			{
				case 'n': buf.Add((byte)'\n'); break;
				case 't': buf.Add((byte)'\t'); break;
				case 'r': buf.Add((byte)'\r'); break;
				case 'b': buf.Add((byte)'\b'); break;
				case 'f': buf.Add((byte)'\f'); break;
				case '"': buf.Add((byte)'"'); break;
				case '\\': buf.Add((byte)'\\'); break;
				case '/': buf.Add((byte)'/'); break;
				case 'u':
					if (i + 4 < latin1.Length && int.TryParse(latin1.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out int cp))
					{
						buf.AddRange(Encoding.UTF8.GetBytes(((char)cp).ToString()));
						i += 4;
					}
					break;
				default: buf.Add((byte)n); break;
			}
		}
		return buf.ToArray();
	}

	private static List<RawToken> ParseJsonFullTokens(string jsonPath)
	{
		var list = new List<RawToken>();
		string raw = Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(jsonPath));
		foreach (Match m in s_jsonTokenRegex.Matches(raw))
		{
			string rawText = m.Groups[1].Value;
			if (rawText.StartsWith("[_") && rawText.EndsWith("]")) continue; // [_BEG_] [_TT_xxx] 等特殊 token
			int.TryParse(m.Groups[2].Value, out int from);
			int.TryParse(m.Groups[3].Value, out int to);
			double.TryParse(m.Groups[5].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double p);
			list.Add(new RawToken { Bytes = UnescapeJsonToBytes(rawText), From = from, To = to, P = p });
		}
		return list;
	}

	private static bool IsPunctOnly(byte[] bytes)
	{
		string tx = Encoding.UTF8.GetString(bytes).Trim();
		if (tx.Length == 0 || tx.IndexOf('\uFFFD') >= 0) return false;
		foreach (char ch in tx)
		{
			if (!char.IsPunctuation(ch)) return false;
		}
		return true;
	}

	private static List<WordTiming> BuildWordsFromTokens(List<RawToken> tokens, bool perToken)
	{
		var words = new List<WordTiming>();
		List<byte> curBytes = null;
		double curStart = 0, curEnd = 0, probSum = 0;
		int probN = 0;

		void Flush()
		{
			if (curBytes == null) return;
			string text = Encoding.UTF8.GetString(curBytes.ToArray()).Trim();
			if (text.Length > 0)
			{
				double s = curStart / 1000.0;
				double e = Math.Max(curEnd / 1000.0, s + 0.03);
				words.Add(new WordTiming { Text = text, StartSec = s, EndSec = e, Prob = probN > 0 ? probSum / probN : 1.0 });
			}
			curBytes = null;
			probSum = 0;
			probN = 0;
		}

		foreach (var tok in tokens)
		{
			bool startsWithSpace = tok.Bytes.Length > 0 && tok.Bytes[0] == 0x20;
			bool punct = IsPunctOnly(tok.Bytes);
			bool startNew = curBytes == null || perToken || (startsWithSpace && !punct);
			if (startNew)
			{
				Flush();
				curBytes = new List<byte>(tok.Bytes);
				curStart = tok.From;
				curEnd = tok.To;
				probSum = 0;
				probN = 0;
				if (!punct) { probSum += tok.P; probN++; }
			}
			else
			{
				curBytes.AddRange(tok.Bytes);
				if (!punct)
				{
					curEnd = Math.Max(curEnd, tok.To);
					probSum += tok.P;
					probN++;
				}
			}
		}
		Flush();
		return words;
	}

	private static bool EndsSentence(string t)
	{
		if (string.IsNullOrEmpty(t)) return false;
		char c = t[t.Length - 1];
		return c == '.' || c == '?' || c == '!' || c == '…' || c == '。' || c == '！' || c == '？';
	}

	private static bool EndsClause(string t)
	{
		if (string.IsNullOrEmpty(t)) return false;
		char c = t[t.Length - 1];
		return c == ',' || c == ';' || c == ':' || c == '，' || c == '；' || c == '：' || c == '、';
	}

	/// <summary>把逐词序列重新断成适合短视频阅读的字幕行（句末标点 / 停顿≥0.75s / 逗号后超长 / 行宽上限 / 时长上限）</summary>
	private static List<SubtitleSegment> BuildSegmentsFromWords(List<WordTiming> words, bool noSpaces, int maxChars)
	{
		var lines = new List<List<WordTiming>>();
		var cur = new List<WordTiming>();
		int curChars = 0;

		for (int i = 0; i < words.Count; i++)
		{
			var w = words[i];
			int add = w.Text.Length + (cur.Count > 0 && !noSpaces ? 1 : 0);
			if (cur.Count > 0 && curChars + add > maxChars)
			{
				lines.Add(cur);
				cur = new List<WordTiming>();
				curChars = 0;
				add = w.Text.Length;
			}
			cur.Add(w);
			curChars += add;

			bool last = i == words.Count - 1;
			double gap = last ? 99 : words[i + 1].StartSec - w.EndSec;
			double lineDur = w.EndSec - cur[0].StartSec;
			if (last || gap >= 0.75 || EndsSentence(w.Text) || (EndsClause(w.Text) && curChars >= (maxChars * 2 / 3)) || lineDur >= 7.0)
			{
				lines.Add(cur);
				cur = new List<WordTiming>();
				curChars = 0;
			}
		}

		// 合并过短的孤行（≤2词、<1s，且与下一行紧邻）
		for (int i = 0; i < lines.Count - 1; i++)
		{
			var a = lines[i];
			var b = lines[i + 1];
			double aDur = a[a.Count - 1].EndSec - a[0].StartSec;
			double gapAB = b[0].StartSec - a[a.Count - 1].EndSec;
			int chars = a.Sum(x => x.Text.Length + 1) + b.Sum(x => x.Text.Length + 1);
			if (a.Count <= 2 && aDur < 1.0 && gapAB < 0.3 && chars <= maxChars && !EndsSentence(a[a.Count - 1].Text))
			{
				a.AddRange(b);
				lines.RemoveAt(i + 1);
				i--;
			}
		}

		var result = new List<SubtitleSegment>();
		string prevText = null;
		double prevEnd = -1;
		for (int i = 0; i < lines.Count; i++)
		{
			var ln = lines[i];
			string text = string.Join(noSpaces ? "" : " ", ln.Select(x => x.Text)).Trim();
			if (IsHallucinationOrJunk(text)) continue;
			double start = ln[0].StartSec;
			double end = ln[ln.Count - 1].EndSec;
			double conf = ln.Average(x => x.Prob);
			if (conf < 0.02) continue; // 仅过滤接近0的极度无意义乱码，保留所有弱音与口音正常对话
			// 重复循环幻觉：与上一句完全相同且几乎紧邻
			if (prevText != null && string.Equals(prevText, text, StringComparison.OrdinalIgnoreCase) && start - prevEnd < 0.6) continue;
			prevText = text;
			prevEnd = end;

			double nextStart = (i + 1 < lines.Count) ? lines[i + 1][0].StartSec : double.MaxValue;
			double tail = Math.Min(0.12, Math.Max(0.0, nextStart - end - 0.02));
			result.Add(new SubtitleSegment
			{
				Index = result.Count + 1,
				StartTime = TimeSpan.FromSeconds(start),
				EndTime = TimeSpan.FromSeconds(end + tail),
				OriginalText = text,
				Words = ln.Select(x => new WordTiming { Text = x.Text, StartSec = x.StartSec, EndSec = x.EndSec, Prob = x.Prob }).ToList(),
				Confidence = conf
			});
		}
		return result;
	}

	/// <summary>
	/// 带真实逐词时间戳的转录：使用 whisper-cli -ojf；支持初始提示词(专名/口音语境)偏置；
	/// 返回按短视频阅读习惯断句的字幕，每句携带 Words(真实词时间) 与 Confidence(置信度)。
	/// </summary>
	public static async Task<List<SubtitleSegment>> TranscribeWithWordsAsync(
		string audioWavPath,
		string whisperCliPath,
		string modelPath,
		string language,
		string initialPrompt,
		Action<string> logCallback,
		CancellationToken ct,
		Action<int> progressCallback = null)
	{
		string whisperDir = Path.GetDirectoryName(whisperCliPath);
		string relativeModel = ToRelativeModelPath(modelPath, whisperDir);
		string rawLang = string.IsNullOrWhiteSpace(language) ? "auto" : language.Trim().ToLowerInvariant();
		string lang = NormalizeWhisperLanguage(rawLang);
		string outBase = Path.Combine(Path.GetTempPath(), "whisper_words_" + Guid.NewGuid().ToString("N"));
		string jsonPath = outBase + ".json";

		var args = new StringBuilder();
		args.Append($"-m {QuoteArg(relativeModel)} ");
		args.Append($"-f {QuoteArg(audioWavPath)} ");
		args.Append($"-l {lang} ");
		args.Append($"-ojf -of {QuoteArg(outBase)} ");
		// -pp 打印进度，-sns 抑制非语音字符，-nth 0.85 提升人声识别容错率，防止漏识别轻声/连读/背景音下的人声
		args.Append("-pp -sns -nth 0.85 ");
		string promptToUse = !string.IsNullOrWhiteSpace(initialPrompt) ? initialPrompt : BuildInitialPrompt(rawLang, null);
		string safePrompt = SanitizePromptForAnsi(promptToUse);
		if (!string.IsNullOrWhiteSpace(safePrompt))
		{
			// 注意：不能同时加 -mc 0，否则提示词会被截断到 1 个 token
			args.Append($"--prompt {QuoteArg(safePrompt)} --carry-initial-prompt ");
		}

		logCallback?.Invoke($"[Whisper 逐词转录] 模型: {Path.GetFileName(modelPath)} | 语种: {lang}" + (string.IsNullOrEmpty(safePrompt) ? "" : " | 已注入提示词"));

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
		proc.OutputDataReceived += (s, e) => { };
		var errSb = new StringBuilder();
		proc.ErrorDataReceived += (s, e) =>
		{
			if (string.IsNullOrWhiteSpace(e.Data)) return;
			string d = e.Data.Trim();
			var pm = Regex.Match(d, @"progress\s*=\s*(\d+)%");
			if (pm.Success && int.TryParse(pm.Groups[1].Value, out int pVal)) progressCallback?.Invoke(pVal);
			if (d.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				errSb.AppendLine(d);
				logCallback?.Invoke($"[Whisper内核提示] {d}");
			}
		};

		proc.Start();
		proc.BeginOutputReadLine();
		proc.BeginErrorReadLine();

		using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
		{
			await Task.Run(() => proc.WaitForExit(), ct);
		}

		// 自动容灾降级：若显卡版启动异常或未生成有效 json 输出，自动使用 -ng (CPU 模式) 重新推理
		if (!File.Exists(jsonPath) && IsCudaBuild(whisperCliPath) && !ct.IsCancellationRequested)
		{
			logCallback?.Invoke("[GPU异常/无输出] 正在自动切换至 CPU 高兼容模式重新转录...");
			var cpuArgs = new StringBuilder();
			cpuArgs.Append($"-m {QuoteArg(relativeModel)} ");
			cpuArgs.Append($"-f {QuoteArg(audioWavPath)} ");
			cpuArgs.Append($"-l {lang} ");
			cpuArgs.Append($"-ojf -of {QuoteArg(outBase)} ");
			cpuArgs.Append("-ng -pp -sns -nth 0.85 ");
			if (!string.IsNullOrWhiteSpace(safePrompt))
			{
				cpuArgs.Append($"--prompt {QuoteArg(safePrompt)} --carry-initial-prompt ");
			}

			var cpuPsi = new ProcessStartInfo
			{
				FileName = whisperCliPath,
				Arguments = cpuArgs.ToString(),
				WorkingDirectory = whisperDir,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};

			using var cpuProc = new Process { StartInfo = cpuPsi };
			cpuProc.OutputDataReceived += (s, e) => { };
			cpuProc.ErrorDataReceived += (s, e) =>
			{
				if (string.IsNullOrWhiteSpace(e.Data)) return;
				var pm = Regex.Match(e.Data, @"progress\s*=\s*(\d+)%");
				if (pm.Success && int.TryParse(pm.Groups[1].Value, out int pVal)) progressCallback?.Invoke(pVal);
			};
			cpuProc.Start();
			cpuProc.BeginOutputReadLine();
			cpuProc.BeginErrorReadLine();
			using (ct.Register(() => { try { if (!cpuProc.HasExited) cpuProc.Kill(); } catch { } }))
			{
				await Task.Run(() => cpuProc.WaitForExit(), ct);
			}
		}

		var segments = new List<SubtitleSegment>();
		try
		{
			if (File.Exists(jsonPath))
			{
				bool perToken = lang == "zh" || lang == "ja" || lang == "ko";
				var tokens = ParseJsonFullTokens(jsonPath);
				var words = BuildWordsFromTokens(tokens, perToken);
				segments = BuildSegmentsFromWords(words, perToken, perToken ? 22 : 44);
			}
			else
			{
				string errStr = errSb.ToString().Trim();
				if (!string.IsNullOrEmpty(errStr))
				{
					logCallback?.Invoke($"[Whisper未能生成字幕] {errStr}");
				}
			}
		}
		finally
		{
			try { if (File.Exists(jsonPath)) File.Delete(jsonPath); } catch { }
		}

		logCallback?.Invoke($"[逐词转录结束] 共 {segments.Count} 句字幕（含真实词时间戳）");
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

	public static bool HasChinese(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return false;
		foreach (char c in text)
		{
			if (c >= 0x4E00 && c <= 0x9FA5) return true;
		}
		return false;
	}

	private static string MapSourceLang(string sourceLanguage)
	{
		return sourceLanguage?.ToLowerInvariant() switch
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
	}

	/// <summary>多源神经网络翻译（自动容灾：Google Chrome 引擎 -> MyMemory 国际通道 -> 网易有道 -> Google GTX）</summary>
	public static async Task<string> TranslateToChineseAsync(string text, string sourceLanguage = "auto")
	{
		if (string.IsNullOrWhiteSpace(text)) return string.Empty;
		if (HasChinese(text) && text.Length > 2) return text;

		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

		// 通道 1: Google Chrome 扩展专用 API (dict-chrome-ex，高并发不被 429 拦截)
		string res1 = await TryGoogleTranslateAsync(text, "dict-chrome-ex", sourceLanguage);
		if (!string.IsNullOrEmpty(res1) && (HasChinese(res1) || res1 != text))
		{
			return res1;
		}

		// 通道 2: MyMemory 国际神经网络翻译 (直连海外与国内通用网络)
		string res2 = await TryMyMemoryTranslateAsync(text, sourceLanguage);
		if (!string.IsNullOrEmpty(res2) && (HasChinese(res2) || res2 != text))
		{
			return res2;
		}

		// 通道 3: 网易有道神经网络翻译引擎 (国内极速直连)
		string res3 = await TryYoudaoTranslateAsync(text);
		if (!string.IsNullOrEmpty(res3) && (HasChinese(res3) || res3 != text))
		{
			return res3;
		}

		// 通道 4: Google GTX 备用通道
		string res4 = await TryGoogleTranslateAsync(text, "gtx", sourceLanguage);
		if (!string.IsNullOrEmpty(res4) && (HasChinese(res4) || res4 != text))
		{
			return res4;
		}

		return null;
	}

	private static async Task<string> TryGoogleTranslateAsync(string text, string client, string sourceLanguage)
	{
		try
		{
			string srcLang = MapSourceLang(sourceLanguage);
			string url = $"https://translate.googleapis.com/translate_a/single?client={client}&sl={srcLang}&tl=zh-CN&dt=t&q={Uri.EscapeDataString(text)}";
			var request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
			var response = await s_httpClient.SendAsync(request, cts.Token);
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
		return null;
	}

	private static async Task<string> TryMyMemoryTranslateAsync(string text, string sourceLanguage)
	{
		try
		{
			string srcLang = MapSourceLang(sourceLanguage);
			if (srcLang == "auto") srcLang = "es";
			string url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={srcLang}|zh-CN";
			var request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.Add("User-Agent", "Mozilla/5.0 VideoBatchStudio");

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
			var response = await s_httpClient.SendAsync(request, cts.Token);
			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
				var dict = serializer.DeserializeObject(json) as System.Collections.IDictionary;
				if (dict != null && dict.Contains("responseData"))
				{
					var responseData = dict["responseData"] as System.Collections.IDictionary;
					if (responseData != null && responseData.Contains("translatedText"))
					{
						string translated = responseData["translatedText"] as string;
						if (!string.IsNullOrEmpty(translated) && !translated.Contains("MYMEMORY WARNING"))
						{
							return System.Net.WebUtility.HtmlDecode(translated.Trim());
						}
					}
				}
			}
		}
		catch { }
		return null;
	}

	private static async Task<string> TryYoudaoTranslateAsync(string text)
	{
		try
		{
			string url = "https://aidemo.youdao.com/trans";
			var content = new FormUrlEncodedContent(new[]
			{
				new KeyValuePair<string, string>("q", text),
				new KeyValuePair<string, string>("from", "Auto"),
				new KeyValuePair<string, string>("to", "zh-CHS")
			});

			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
			var response = await s_httpClient.PostAsync(url, content, cts.Token);
			if (response.IsSuccessStatusCode)
			{
				string json = await response.Content.ReadAsStringAsync();
				var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
				var dict = serializer.DeserializeObject(json) as System.Collections.IDictionary;
				if (dict != null && dict.Contains("translation"))
				{
					if (dict["translation"] is System.Collections.IList transList && transList.Count > 0)
					{
						string translated = transList[0] as string;
						if (!string.IsNullOrEmpty(translated)) return translated.Trim();
					}
				}
			}
		}
		catch { }
		return null;
	}

	public static async Task TranslateSegmentsAsync(List<SubtitleSegment> segments, string sourceLanguage, Action<string> logCallback, CancellationToken ct)
	{
		if (segments == null || segments.Count == 0) return;
		logCallback?.Invoke($"[AI智能翻译] 正在翻译 {segments.Count} 句字幕为简体中文 (多通道神经网络自适应容灾)...");

		// 1. 优先映射环境音标
		foreach (var seg in segments)
		{
			string ambient = FormatAmbientSound(seg.OriginalText);
			if (!string.IsNullOrEmpty(ambient)) seg.TranslatedText = ambient;
		}

		var needTrans = segments.Where(s => string.IsNullOrEmpty(s.TranslatedText)).ToList();
		int successCount = 0;

		// 2. 逐句进行高可靠翻译（确保每句精准对应，避免合并请求被频控拦截）
		for (int i = 0; i < needTrans.Count; i++)
		{
			if (ct.IsCancellationRequested) break;
			var seg = needTrans[i];

			string trans = await TranslateToChineseAsync(seg.OriginalText, sourceLanguage);
			if (!string.IsNullOrEmpty(trans) && (HasChinese(trans) || trans != seg.OriginalText))
			{
				seg.TranslatedText = trans;
				successCount++;
			}
			else
			{
				// 临时兜底
				seg.TranslatedText = seg.OriginalText;
			}

			if ((i + 1) % 5 == 0 || i == needTrans.Count - 1)
			{
				logCallback?.Invoke($"[翻译进度] 已完成: {i + 1} / {needTrans.Count} 句 (成功译出中文: {successCount} 句)");
			}

			await Task.Delay(35, ct);
		}

		if (successCount > 0)
		{
			logCallback?.Invoke($"[AI智能翻译完成] 全部中文译文字幕对照就绪！(共成功翻译 {successCount}/{needTrans.Count} 句)");
		}
		else
		{
			logCallback?.Invoke("[翻译警告] 所有在线翻译通道当前均无响应，请检查电脑网络连接！");
		}
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
