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

	public static string FindWhisperCliPath()
	{
		string appDir = AppDomain.CurrentDomain.BaseDirectory;
		string[] candidates = new string[]
		{
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
			Arguments = $"-i \"{inputMedia}\" -vn -ar 16000 -ac 1 -c:a pcm_s16le \"{tempWav}\" -y",
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};

		using var proc = Process.Start(psi);
		if (proc == null) throw new InvalidOperationException("无法启动 FFmpeg 音频提取进程");

		using (ct.Register(() =>
		{
			try { if (!proc.HasExited) proc.Kill(); } catch { }
		}))
		{
			await Task.Run(() => proc.WaitForExit(), ct);
		}

		if (proc.ExitCode == 0 && File.Exists(tempWav))
		{
			logCallback?.Invoke($"[音频提取完成] 临时音轨就绪: {tempWav}");
			return tempWav;
		}

		throw new InvalidOperationException($"FFmpeg 音频提取失败，退出码: {proc.ExitCode}");
	}

	public static async Task<List<SubtitleSegment>> TranscribeAsync(
		string audioWavPath,
		string whisperCliPath,
		string modelPath,
		string language,
		Action<string> logCallback,
		CancellationToken ct)
	{
		var segments = new List<SubtitleSegment>();
		string whisperDir = Path.GetDirectoryName(whisperCliPath);

		string relativeModel = modelPath;
		if (modelPath.StartsWith(whisperDir, StringComparison.OrdinalIgnoreCase))
		{
			relativeModel = modelPath.Substring(whisperDir.Length).TrimStart('\\', '/');
		}

		string langArg = string.IsNullOrWhiteSpace(language) || language.Equals("auto", StringComparison.OrdinalIgnoreCase)
			? "-l auto"
			: $"-l {language}";

		var args = new StringBuilder();
		args.Append($"-m \"{relativeModel}\" ");
		args.Append($"-f \"{audioWavPath}\" ");
		args.Append($"{langArg} ");
		args.Append("-pp --no-prints ");

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

	public static async Task<string> TranslateToChineseAsync(string text, string sourceLanguage = "auto")
	{
		if (string.IsNullOrWhiteSpace(text)) return string.Empty;

		try
		{
			string srcLang = sourceLanguage.ToLowerInvariant() switch
			{
				"english" or "en" => "en",
				"portuguese" or "pt" => "pt",
				"spanish" or "es" => "es",
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
				if (parsed is System.Collections.ArrayList root && root.Count > 0)
				{
					if (root[0] is System.Collections.ArrayList sentences)
					{
						var sb = new StringBuilder();
						foreach (object item in sentences)
						{
							if (item is System.Collections.ArrayList partList && partList.Count > 0)
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
		logCallback?.Invoke($"[神经翻译] 正在翻译 {segments.Count} 句字幕为简体中文 (支持英文/葡萄牙语/西班牙语)...");

		int batchSize = 10;
		for (int i = 0; i < segments.Count; i += batchSize)
		{
			if (ct.IsCancellationRequested) break;
			var chunk = segments.Skip(i).Take(batchSize).ToList();

			var combinedText = string.Join("\n", chunk.Select(s => s.OriginalText));
			string translatedCombined = await TranslateToChineseAsync(combinedText, sourceLanguage);
			string[] translatedLines = translatedCombined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			if (translatedLines.Length == chunk.Count)
			{
				for (int j = 0; j < chunk.Count; j++)
				{
					chunk[j].TranslatedText = translatedLines[j].Trim();
				}
			}
			else
			{
				foreach (var seg in chunk)
				{
					if (ct.IsCancellationRequested) break;
					seg.TranslatedText = await TranslateToChineseAsync(seg.OriginalText, sourceLanguage);
				}
			}

			logCallback?.Invoke($"[翻译进度] 已完成: {Math.Min(i + batchSize, segments.Count)} / {segments.Count} 句");
			await Task.Delay(50, ct);
		}

		logCallback?.Invoke("[神经翻译完成] 全部中文译文字幕对照就绪！");
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
}
