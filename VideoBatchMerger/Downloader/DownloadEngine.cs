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

namespace VideoBatchMerger.Downloader;

/// <summary>
/// 核心下载引擎：负责组件探查、平台与元数据智能解析、yt-dlp 驱动与 Google Drive 双模直链加速
/// </summary>
public sealed class DownloadEngine
{
	private static readonly Regex GoogleDriveIdRegex = new Regex(@"(?:/file/d/|id=)([a-zA-Z0-9_-]{20,})", RegexOptions.Compiled);
	private static readonly Regex YtDlpProgressRegex = new Regex(@"\[download\]\s+(\d+(?:\.\d+)?)%\s+of\s+~?([^\s]+)\s+at\s+([^\s]+)\s+ETA\s+([^\s]+)", RegexOptions.Compiled);
	private static readonly Regex YtDlp100ProgressRegex = new Regex(@"\[download\]\s+100%\s+of\s+~?([^\s]+)", RegexOptions.Compiled);
	private static readonly Regex YtDlpDestinationRegex = new Regex(@"\[(?:download|Merger)\]\s+(?:Destination:\s+|Merging formats into\s+""?)(.+?)""?$", RegexOptions.Compiled);

	public static string FindYtDlpPath()
	{
		string appDir = AppDomain.CurrentDomain.BaseDirectory;
		string p1 = Path.Combine(appDir, "yt-dlp.exe");
		if (File.Exists(p1)) return p1;

		string p2 = @"D:\工具\视频拼接\VideoBatchStudio\yt-dlp.exe";
		if (File.Exists(p2)) return p2;

		string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string p3 = Path.Combine(localApp, @"Microsoft\WinGet\Packages\yt-dlp.yt-dlp_Microsoft.Winget.Source_8wekyb3d8bbwe\yt-dlp.exe");
		if (File.Exists(p3)) return p3;

		string p4 = Path.Combine(localApp, @"Microsoft\WinGet\Links\yt-dlp.exe");
		if (File.Exists(p4)) return p4;

		// Check PATH
		string envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
		foreach (string dir in envPath.Split(Path.PathSeparator))
		{
			try
			{
				string candidate = Path.Combine(dir.Trim(), "yt-dlp.exe");
				if (File.Exists(candidate)) return candidate;
			}
			catch { }
		}

		return string.Empty;
	}

	public static string FindFFmpegPath()
	{
		string appDir = AppDomain.CurrentDomain.BaseDirectory;
		string p1 = Path.Combine(appDir, "ffmpeg.exe");
		if (File.Exists(p1)) return p1;

		string p2 = @"D:\工具\视频拼接\VideoBatchStudio\ffmpeg.exe";
		if (File.Exists(p2)) return p2;

		string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string p3 = Path.Combine(localApp, @"Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.1-full_build\bin\ffmpeg.exe");
		if (File.Exists(p3)) return p3;

		string p4 = Path.Combine(localApp, @"Microsoft\WinGet\Packages\yt-dlp.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg.exe");
		if (File.Exists(p4)) return p4;

		string envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
		foreach (string dir in envPath.Split(Path.PathSeparator))
		{
			try
			{
				string candidate = Path.Combine(dir.Trim(), "ffmpeg.exe");
				if (File.Exists(candidate)) return candidate;
			}
			catch { }
		}

		return string.Empty;
	}

	public static string FindFFprobePath()
	{
		string appDir = AppDomain.CurrentDomain.BaseDirectory;
		string p1 = Path.Combine(appDir, "ffprobe.exe");
		if (File.Exists(p1)) return p1;

		string p2 = @"D:\工具\视频拼接\VideoBatchStudio\ffprobe.exe";
		if (File.Exists(p2)) return p2;

		string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string p3 = Path.Combine(localApp, @"Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.1-full_build\bin\ffprobe.exe");
		if (File.Exists(p3)) return p3;

		string p4 = Path.Combine(localApp, @"Microsoft\WinGet\Packages\yt-dlp.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffprobe.exe");
		if (File.Exists(p4)) return p4;

		string envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
		foreach (string dir in envPath.Split(Path.PathSeparator))
		{
			try
			{
				string candidate = Path.Combine(dir.Trim(), "ffprobe.exe");
				if (File.Exists(candidate)) return candidate;
			}
			catch { }
		}

		// 若未单独安装 ffprobe.exe，自动回退使用已就绪的内置 ffmpeg.exe（软件全流程通过 ffmpeg -i 完成流探测）
		string ffmpegFallback = FindFFmpegPath();
		if (!string.IsNullOrEmpty(ffmpegFallback) && File.Exists(ffmpegFallback)) return ffmpegFallback;

		return string.Empty;
	}

	public static (string Platform, string SourceType) IdentifyPlatform(string url)
	{
		if (string.IsNullOrWhiteSpace(url)) return ("未知平台", "未知类型");
		string lower = url.ToLowerInvariant();

		if (lower.Contains("facebook.com") || lower.Contains("fb.watch") || lower.Contains("fb.com"))
		{
			if (lower.Contains("/reel/") || lower.Contains("reel_id"))
				return ("Facebook", "Reel短视频");
			return ("Facebook", "单视频");
		}
		if (lower.Contains("youtube.com") || lower.Contains("youtu.be"))
		{
			if (lower.Contains("/shorts/"))
				return ("YouTube", "Shorts短视频");
			if (lower.Contains("list="))
				return ("YouTube", "播放列表");
			return ("YouTube", "单视频");
		}
		if (lower.Contains("tiktok.com"))
		{
			return ("TikTok", "短视频");
		}
		if (lower.Contains("drive.google.com"))
		{
			return ("Google Drive", "云端文件");
		}
		if (lower.Contains("instagram.com"))
		{
			if (lower.Contains("/reel/")) return ("Instagram", "Reel短视频");
			return ("Instagram", "帖子视频");
		}
		if (lower.Contains("twitter.com") || lower.Contains("x.com"))
		{
			return ("X (Twitter)", "推文视频");
		}
		if (lower.Contains("bilibili.com"))
		{
			return ("Bilibili", "视频");
		}

		return ("通用流媒体", "网络视频");
	}

	/// <summary>
	/// 仅快速解析元数据（标题、时长等），不执行视频下载
	/// </summary>
	public static async Task<bool> ParseMetadataAsync(
		DownloadItem item,
		string ytDlpPath,
		string proxyUrl,
		Action<string> logCallback,
		CancellationToken ct)
	{
		if (item == null || string.IsNullOrWhiteSpace(item.Url)) return false;

		// 针对 Google Drive 特殊处理：若 yt-dlp 就绪，优先查询真实标题与时长
		if (item.Platform == "Google Drive")
		{
			var match = GoogleDriveIdRegex.Match(item.Url);
			if (match.Success)
			{
				string fileId = match.Groups[1].Value;
				item.Title = $"Google_Drive_{fileId}";
				item.DurationStr = "云端网盘";
				item.Status = "已识别";
				if (string.IsNullOrEmpty(ytDlpPath) || !File.Exists(ytDlpPath))
				{
					logCallback?.Invoke($"[解析] 识别 Google Drive 文件 ID: {fileId}");
					return true;
				}
				// 若 yt-dlp 可用，继续向下执行获取网盘文件的真实完整文件名
			}
		}

		if (string.IsNullOrEmpty(ytDlpPath) || !File.Exists(ytDlpPath))
		{
			item.Title = Path.GetFileName(item.Url);
			item.Status = "等待组件就绪";
			return false;
		}

		try
		{
			item.Status = "解析中...";
			logCallback?.Invoke($"[解析] 正在获取媒体元数据: {item.Url}");

			var psi = new ProcessStartInfo
			{
				FileName = ytDlpPath,
				Arguments = BuildYtDlpArgs(item.Url, quality: "best", outputDir: "", proxyUrl: proxyUrl, parseOnly: true),
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};

			using var proc = new Process { StartInfo = psi };
			proc.Start();

			string output = await proc.StandardOutput.ReadToEndAsync();
			string error = await proc.StandardError.ReadToEndAsync();
			await Task.Run(() => proc.WaitForExit(), ct);

			if (!string.IsNullOrWhiteSpace(output))
			{
				string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
				if (lines.Length > 0)
				{
					string line = lines[0].Trim();
					string[] parts = line.Split('|');
					if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
					{
						item.Title = parts[0].Trim();
					}
					if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
					{
						item.DurationStr = parts[1].Trim();
					}
					if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
					{
						item.FileSizeStr = parts[2].Trim();
					}
				}
				item.Status = "就绪";
				logCallback?.Invoke($"[解析成功] 标题: {item.Title} | 时长: {item.DurationStr}");
				return true;
			}
			else
			{
				item.Status = "解析失败";
				logCallback?.Invoke($"[解析提示] 无法提取标题，下载时将自动识别。{error}");
				return false;
			}
		}
		catch (Exception ex)
		{
			item.Status = "解析异常";
			logCallback?.Invoke($"[解析异常] {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// 执行完整下载任务
	/// </summary>
	public static async Task<bool> DownloadAsync(
		DownloadItem item,
		string ytDlpPath,
		string ffmpegPath,
		string outputDir,
		string quality,
		bool resume,
		bool skipExisting,
		bool subfolderByPlatform,
		string proxyUrl,
		Action<string> logCallback,
		CancellationToken ct)
	{
		if (item == null || string.IsNullOrWhiteSpace(item.Url)) return false;

		string targetDir = outputDir;
		if (subfolderByPlatform && !string.IsNullOrWhiteSpace(item.Platform))
		{
			targetDir = Path.Combine(outputDir, SanitizeFolderName(item.Platform));
		}
		Directory.CreateDirectory(targetDir);

		// 检查 Google Drive 优先使用专业提取引擎（包含大文件流媒体源），若未就绪则使用直链引擎
		if (item.Platform == "Google Drive")
		{
			var match = GoogleDriveIdRegex.Match(item.Url);
			if (match.Success)
			{
				string fileId = match.Groups[1].Value;
				// 若 yt-dlp 可用，优先使用 yt-dlp 的原生 GoogleDrive 提取器，可完美下载原始完整高质量视频并自动命名
				if (!string.IsNullOrEmpty(ytDlpPath) && File.Exists(ytDlpPath))
				{
					logCallback?.Invoke($"[Google Drive] 启用高保真云端提取引擎: {fileId}");
					// 继续执行下面的 yt-dlp 处理流程
				}
				else
				{
					logCallback?.Invoke($"[Google Drive] 启动内置直连加速通道: {fileId}");
					bool gdriveOk = await DownloadGoogleDriveDirectAsync(item, fileId, targetDir, proxyUrl, logCallback, ct);
					if (gdriveOk) return true;
				}
			}
		}

		if (string.IsNullOrEmpty(ytDlpPath) || !File.Exists(ytDlpPath))
		{
			item.Status = "缺少 yt-dlp 组件";
			logCallback?.Invoke("[错误] 未找到 yt-dlp.exe，请先点击顶部【检测/安装组件】！");
			return false;
		}

		if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
		{
			ffmpegPath = FindFFmpegPath();
		}

		item.Status = "下载中";
		item.ProgressPercent = 0;
		logCallback?.Invoke($"[启动下载] [{item.Platform}] {item.Url}");

		string args = BuildYtDlpArgs(
			item.Url,
			quality: quality,
			outputDir: targetDir,
			proxyUrl: proxyUrl,
			parseOnly: false,
			resume: resume,
			skipExisting: skipExisting,
			ffmpegPath: ffmpegPath
		);

		var psi = new ProcessStartInfo
		{
			FileName = ytDlpPath,
			Arguments = args,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};

		using var proc = new Process { StartInfo = psi };
		string detectedFilePath = string.Empty;

		proc.OutputDataReceived += (s, e) =>
		{
			if (string.IsNullOrWhiteSpace(e.Data)) return;
			string line = e.Data.Trim();
			logCallback?.Invoke(line);

			// 检查目标文件输出路径
			var destMatch = YtDlpDestinationRegex.Match(line);
			if (destMatch.Success)
			{
				string p = destMatch.Groups[1].Value.Trim();
				if (!string.IsNullOrEmpty(p))
				{
					detectedFilePath = Path.IsPathRooted(p) ? p : Path.Combine(targetDir, p);
					item.SaveFilePath = detectedFilePath;
					item.Title = Path.GetFileNameWithoutExtension(detectedFilePath);
				}
			}

			// 检查进度
			var prgMatch = YtDlpProgressRegex.Match(line);
			if (prgMatch.Success)
			{
				if (double.TryParse(prgMatch.Groups[1].Value, out double pct))
				{
					item.ProgressPercent = (int)Math.Min(100, Math.Max(0, pct));
				}
				item.FileSizeStr = prgMatch.Groups[2].Value;
				item.SpeedStr = prgMatch.Groups[3].Value;
				item.Status = $"下载中 {item.ProgressPercent}% ({item.SpeedStr})";
			}
			else if (YtDlp100ProgressRegex.IsMatch(line))
			{
				item.ProgressPercent = 100;
				item.Status = "处理合并中...";
			}
			else if (line.Contains("has already been downloaded"))
			{
				item.ProgressPercent = 100;
				item.Status = "已完成 (已存在)";
			}
		};

		proc.ErrorDataReceived += (s, e) =>
		{
			if (string.IsNullOrWhiteSpace(e.Data)) return;
			string line = e.Data.Trim();
			logCallback?.Invoke($"[STDERR] {line}");
			if (line.Contains("ERROR:") || line.Contains("HTTP Error"))
			{
				item.ErrorMessage = line;
			}
		};

		proc.Start();
		proc.BeginOutputReadLine();
		proc.BeginErrorReadLine();

		// 支持取消
		using (ct.Register(() =>
		{
			try
			{
				if (!proc.HasExited) proc.Kill();
			}
			catch { }
		}))
		{
			await Task.Run(() => proc.WaitForExit(), ct);
		}

		if (ct.IsCancellationRequested)
		{
			item.Status = "已停止";
			logCallback?.Invoke($"[任务已停止] {item.Url}");
			return false;
		}

		if (proc.ExitCode == 0)
		{
			item.ProgressPercent = 100;
			item.Status = "成功";
			if (string.IsNullOrEmpty(item.SaveFilePath) && !string.IsNullOrEmpty(detectedFilePath))
			{
				item.SaveFilePath = detectedFilePath;
			}
			if (string.IsNullOrEmpty(item.SaveFilePath) || !File.Exists(item.SaveFilePath))
			{
				try
				{
					// 尝试按文件 ID 或最新生成时间定位落盘视频
					var di = new DirectoryInfo(targetDir);
					var files = di.GetFiles("*.*", SearchOption.TopDirectoryOnly)
						.Where(f => f.Extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
						            f.Extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
						            f.Extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
						            f.Extension.Equals(".webm", StringComparison.OrdinalIgnoreCase))
						.OrderByDescending(f => f.LastWriteTime)
						.ToList();

					if (files.Count > 0)
					{
						item.SaveFilePath = files[0].FullName;
						item.Title = Path.GetFileNameWithoutExtension(item.SaveFilePath);
					}
				}
				catch { }
			}

			if (quality != "audio" && !string.IsNullOrEmpty(item.SaveFilePath) && File.Exists(item.SaveFilePath))
			{
				await EnsureUniversalH264VideoAsync(item, ffmpegPath, logCallback, ct);
			}

			item.Status = "成功";
			logCallback?.Invoke($"[下载完成] 成功保存: {item.SaveFilePath}");
			return true;
		}
		else
		{
			if (item.Platform == "Google Drive")
			{
				var gMatch = GoogleDriveIdRegex.Match(item.Url);
				if (gMatch.Success)
				{
					string gFileId = gMatch.Groups[1].Value;
					logCallback?.Invoke($"[Google Drive] yt-dlp 抓取受限(403)，自动切换内置直接下载通道重试: {gFileId}...");
					bool directOk = await DownloadGoogleDriveDirectAsync(item, gFileId, targetDir, proxyUrl, logCallback, ct);
					if (directOk) return true;
				}
			}

			item.Status = "下载失败";
			logCallback?.Invoke($"[下载失败] 进程退出码: {proc.ExitCode} {item.ErrorMessage}");
			if (item.Platform == "Google Drive" || (item.ErrorMessage != null && item.ErrorMessage.Contains("403")))
			{
				logCallback?.Invoke("💡 [排查指引] 该链接返回 403 Forbidden。通常是因为谷歌网盘分享权限被设为了【受限】。请在 Google 云端硬盘中右键该文件 -> 点击【共享】-> 将常规访问权限从【受限】改为【知道链接的任何人】，即可免登录直接下载！");
			}
			return false;
		}
	}

	/// <summary>
	/// Google Drive 专属大文件确认与直接下载
	/// </summary>
	private static async Task<bool> DownloadGoogleDriveDirectAsync(
		DownloadItem item,
		string fileId,
		string outputDir,
		string proxyUrl,
		Action<string> logCallback,
		CancellationToken ct)
	{
		try
		{
			var cookieJar = new CookieContainer();
			var handler = new HttpClientHandler { CookieContainer = cookieJar };
			if (!string.IsNullOrWhiteSpace(proxyUrl))
			{
				handler.Proxy = new WebProxy(proxyUrl);
				handler.UseProxy = true;
			}
			else
			{
				handler.UseProxy = true;
				handler.Proxy = WebRequest.GetSystemWebProxy();
			}

			using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(60) };
			client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

			string initUrl = $"https://drive.google.com/uc?export=download&id={fileId}";
			logCallback?.Invoke($"[Google Drive] 发起初始握手: {initUrl}");

			var response = await client.GetAsync(initUrl, HttpCompletionOption.ResponseHeadersRead, ct);

			// 检查是否出现 HTML 页面（病毒扫描、大文件确认表单等）
			if (response.Content.Headers.ContentType?.MediaType?.Contains("html") == true)
			{
				string html = await response.Content.ReadAsStringAsync();
				if (html.Contains("accounts.google.com") || html.Contains("Sign in to your Google Account") || html.Contains("ServiceLogin") || response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
				{
					logCallback?.Invoke($"💡 [Google Drive 权限诊断提示]：下载失败(401/403)，该网盘文件当前处于【受限】状态，未开放公开访问。\n👉 解决方案：请在 Google 云端硬盘中右键该文件 -> 选择【共享】-> 将常规访问权限从【受限】更改为【知道链接的任何人可查看】，即可免密直接高速下载！");
					item.Status = "权限受限 (需改为公开)";
					return false;
				}

				// 策略 A: 提取现代 Google Drive 下载表单 (action + hidden inputs)
				var formMatch = Regex.Match(html, @"<form[^>]*action=""([^""]*)""");
				string formAction = formMatch.Success ? formMatch.Groups[1].Value : "https://drive.usercontent.google.com/download";

				var inputMatches = Regex.Matches(html, @"<input[^>]*name=""([^""]*)""[^>]*value=""([^""]*)""");
				if (inputMatches.Count > 0)
				{
					var queryParams = new List<string>();
					foreach (Match inp in inputMatches)
					{
						string k = inp.Groups[1].Value;
						string v = inp.Groups[2].Value;
						queryParams.Add($"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}");
					}
					string downloadUrl = $"{formAction}?{string.Join("&", queryParams)}";
					logCallback?.Invoke($"[Google Drive] 成功解析大文件直接下载流通道，正在拉取数据...");
					response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
				}
				else
				{
					var confirmMatch = Regex.Match(html, @"confirm=([0-9a-zA-Z_]+)");
					if (confirmMatch.Success)
					{
						string token = confirmMatch.Groups[1].Value;
						string downloadUrl = $"https://drive.google.com/uc?export=download&confirm={token}&id={fileId}";
						logCallback?.Invoke($"[Google Drive] 检测到确认令牌: {token}，正在重定向下载...");
						response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
					}
				}
			}

			string fileName = $"GoogleDrive_{fileId}.mp4";
			if (response.Content.Headers.ContentDisposition?.FileName != null)
			{
				string headerName = response.Content.Headers.ContentDisposition.FileName.Trim('"', '\'');
				if (!string.IsNullOrWhiteSpace(headerName))
				{
					fileName = headerName;
				}
			}
			fileName = SanitizeFileName(fileName);
			if (!Path.HasExtension(fileName) || fileName.EndsWith("."))
			{
				fileName += ".mp4";
			}
			else if (!fileName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) &&
			         !fileName.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) &&
			         !fileName.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) &&
			         !fileName.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) &&
			         !fileName.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
			{
				fileName += ".mp4";
			}

			string targetFile = Path.Combine(outputDir, fileName);
			item.SaveFilePath = targetFile;
			item.Title = Path.GetFileNameWithoutExtension(targetFile);

			long? totalBytes = response.Content.Headers.ContentLength;
			if (totalBytes.HasValue)
			{
				item.FileSizeStr = $"{totalBytes.Value / (1024.0 * 1024.0):F1} MB";
			}

			logCallback?.Invoke($"[Google Drive] 开始接收数据流: {fileName} ({item.FileSizeStr})");

			using var inputStream = await response.Content.ReadAsStreamAsync();
			using var outputStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.Read);

			byte[] buffer = new byte[64 * 1024];
			long downloadedBytes = 0;
			var sw = Stopwatch.StartNew();
			long lastBytes = 0;
			double lastTime = 0;

			int read;
			while ((read = await inputStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
			{
				await outputStream.WriteAsync(buffer, 0, read, ct);
				downloadedBytes += read;

				if (sw.ElapsedMilliseconds - lastTime > 400)
				{
					double speedBytesPerSec = (downloadedBytes - lastBytes) / ((sw.ElapsedMilliseconds - lastTime) / 1000.0);
					item.SpeedStr = $"{speedBytesPerSec / (1024 * 1024):F2} MB/s";
					lastBytes = downloadedBytes;
					lastTime = sw.ElapsedMilliseconds;

					if (totalBytes.HasValue && totalBytes.Value > 0)
					{
						item.ProgressPercent = (int)Math.Min(100, (downloadedBytes * 100.0) / totalBytes.Value);
						item.Status = $"下载中 {item.ProgressPercent}% ({item.SpeedStr})";
					}
					else
					{
						item.Status = $"已下载 {downloadedBytes / (1024 * 1024):F1} MB ({item.SpeedStr})";
					}
				}
			}

			item.ProgressPercent = 100;
			item.Status = "成功";
			logCallback?.Invoke($"[Google Drive] 下载完成: {targetFile}");
			return true;
		}
		catch (Exception ex)
		{
			logCallback?.Invoke($"[Google Drive 直链提示] {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// 检测落盘视频是否为 AV1 (av01) 或 VP9 等 Windows 默认播放器不兼容的编码；
	/// 若是，则自动调用内置 FFmpeg 极速转码为全平台通用的 H.264 (yuv420p) + AAC MP4，杜绝“有声无画”现象。
	/// </summary>
	private static async Task EnsureUniversalH264VideoAsync(
		DownloadItem item,
		string ffmpegPath,
		Action<string> logCallback,
		CancellationToken ct)
	{
		if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath)) return;
		if (item == null || string.IsNullOrEmpty(item.SaveFilePath) || !File.Exists(item.SaveFilePath)) return;

		try
		{
			var probePsi = new ProcessStartInfo
			{
				FileName = ffmpegPath,
				Arguments = $"-hide_banner -i {ScriptBatch.ScriptBatchComposer.QuoteArg(item.SaveFilePath)}",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
				StandardErrorEncoding = Encoding.UTF8
			};

			string info;
			using (var probeProc = new Process { StartInfo = probePsi })
			{
				probeProc.Start();
				info = await probeProc.StandardError.ReadToEndAsync();
				await Task.Run(() => probeProc.WaitForExit(8000), ct);
			}

			var vMatch = Regex.Match(info ?? string.Empty, @"Stream\s+#\d+:\d+.*?Video:\s*([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
			if (!vMatch.Success) return;

			string vcodec = vMatch.Groups[1].Value.ToLowerInvariant();
			if (vcodec == "h264" || vcodec == "avc1")
			{
				return;
			}

			item.Status = "转码H.264画面中...";
			logCallback?.Invoke($"[兼容转码] 检测到原始视频流为 {vcodec.ToUpperInvariant()} 编码，正在自动转换为全平台通用的 H.264 高清画面...");

			string dir = Path.GetDirectoryName(item.SaveFilePath) ?? Path.GetTempPath();
			string nameNoExt = Path.GetFileNameWithoutExtension(item.SaveFilePath);
			string tempOut = Path.Combine(dir, $"{nameNoExt}.h264_temp_{Guid.NewGuid():N}.mp4");

			var convPsi = new ProcessStartInfo
			{
				FileName = ffmpegPath,
				Arguments = $"-hide_banner -y -i {ScriptBatch.ScriptBatchComposer.QuoteArg(item.SaveFilePath)} -map 0:v:0 -map 0:a:0? -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart {ScriptBatch.ScriptBatchComposer.QuoteArg(tempOut)}",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
				StandardErrorEncoding = Encoding.UTF8
			};

			using (var convProc = new Process { StartInfo = convPsi })
			{
				convProc.Start();
				var errTask = convProc.StandardError.ReadToEndAsync();
				using (ct.Register(() =>
				{
					try { if (!convProc.HasExited) convProc.Kill(); } catch { }
				}))
				{
					await Task.Run(() => convProc.WaitForExit(), ct);
				}
				await errTask;

				if (convProc.ExitCode == 0 && File.Exists(tempOut) && new FileInfo(tempOut).Length > 1024)
				{
					string finalMp4 = Path.ChangeExtension(item.SaveFilePath, ".mp4");
					try
					{
						File.Delete(item.SaveFilePath);
					}
					catch { }
					if (File.Exists(finalMp4))
					{
						try { File.Delete(finalMp4); } catch { }
					}
					File.Move(tempOut, finalMp4);
					item.SaveFilePath = finalMp4;
					item.FileSizeStr = $"{new FileInfo(finalMp4).Length / (1024.0 * 1024.0):F1} MB";
					logCallback?.Invoke($"[兼容转码完成] 已转换为标准 H.264 + AAC 编码 ({item.FileSizeStr})，所有播放器均可正常播放画面与声音。");
				}
				else
				{
					try { if (File.Exists(tempOut)) File.Delete(tempOut); } catch { }
				}
			}
		}
		catch (Exception ex)
		{
			logCallback?.Invoke($"[兼容转码提示] {ex.Message}");
		}
	}

	private static string BuildYtDlpArgs(
		string url,
		string quality,
		string outputDir,
		string proxyUrl,
		bool parseOnly,
		bool resume = true,
		bool skipExisting = true,
		string ffmpegPath = "")
	{
		var sb = new StringBuilder();

		if (parseOnly)
		{
			sb.Append("--no-playlist --encoding UTF-8 --dump-single-json --js-runtimes node --extractor-args \"youtube:player_client=default,mweb,web\" --print \"%(title)s|%(duration_string)s|%(filesize_approx_str)s\" ");
		}
		else
		{
			sb.Append("--no-playlist --newline --encoding UTF-8 ");
			if (resume) sb.Append("-c ");
			if (skipExisting) sb.Append("--no-overwrites ");
			// 网络传输与分片重试防御（解决长视频 403 Forbidden / 片段中断）
			sb.Append("--retries 15 --fragment-retries 20 --file-access-retries 10 ");
			sb.Append("--extractor-args \"youtube:player_client=default,mweb,web\" ");

			// 自动挂载 node.js 运行时以应对复杂的 YouTube 签名加密挑战
			sb.Append("--js-runtimes node ");

			// 画质与编码选择：优先选取 H.264 (avc1) + AAC (m4a) 标准编码，同时保留包含完整原声的高清流并合并为 mp4
			switch (quality)
			{
				case "1080p":
					sb.Append("-S \"res:1080,vcodec:h264,acodec:m4a\" --merge-output-format mp4 -f \"bv*[height<=1920]+ba/b[height<=1920]/bv*+ba/b\" ");
					break;
				case "720p":
					sb.Append("-S \"res:720,vcodec:h264,acodec:m4a\" --merge-output-format mp4 -f \"bv*[height<=1280]+ba/b[height<=1280]/bv*+ba/b\" ");
					break;
				case "audio":
					sb.Append("-x --audio-format mp3 ");
					break;
				default: // best
					sb.Append("-S \"res,vcodec:h264,acodec:m4a\" --merge-output-format mp4 -f \"bv*+ba/b\" ");
					break;
			}

			// 输出文件名模版
			string outTemplate = Path.Combine(outputDir, "%(title).100B [%(id)s].%(ext)s");
			sb.Append($"--output {ScriptBatch.ScriptBatchComposer.QuoteArg(outTemplate)} ");

			if (!string.IsNullOrWhiteSpace(ffmpegPath) && File.Exists(ffmpegPath))
			{
				string ffmpegDir = Path.GetDirectoryName(ffmpegPath);
				sb.Append($"--ffmpeg-location {ScriptBatch.ScriptBatchComposer.QuoteArg(ffmpegDir)} ");
			}
		}

		if (!string.IsNullOrWhiteSpace(proxyUrl))
		{
			string safeProxy = proxyUrl.Replace("\"", "").Replace("\r", "").Replace("\n", "").Trim();
			if (!string.IsNullOrWhiteSpace(safeProxy))
			{
				sb.Append($"--proxy {ScriptBatch.ScriptBatchComposer.QuoteArg(safeProxy)} ");
			}
		}

		// 安全防御：清洗 URL 中的双引号与换行符，并使用 -- 终止选项解析，彻底杜绝 CLI 参数注入
		string cleanUrl = (url ?? string.Empty).Replace("\"", "").Replace("\r", "").Replace("\n", "").Trim();
		sb.Append($"-- \"{cleanUrl}\"");
		return sb.ToString();
	}

	private static string SanitizeFileName(string name)
	{
		string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
		string sanitized = Regex.Replace(name, "[" + invalidChars + "]", "_");
		return string.IsNullOrWhiteSpace(sanitized) ? "downloaded_video.mp4" : sanitized;
	}

	private static string SanitizeFolderName(string name)
	{
		if (string.IsNullOrWhiteSpace(name)) return "Videos";
		string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
		string sanitized = Regex.Replace(name, "[" + invalidChars + "]", "_").Trim('.', ' ');
		return string.IsNullOrWhiteSpace(sanitized) ? "Videos" : sanitized;
	}
}
