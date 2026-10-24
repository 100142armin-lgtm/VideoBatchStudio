using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace VideoBatchMerger.ScriptBatch
{
	public sealed class VoiceOption
	{
		public string Key { get; }
		public string DisplayName { get; }
		public string Language { get; }
		public string Gender { get; }
		public string Mood { get; }

		public VoiceOption(string key, string displayName, string language = "Custom", string gender = "AI", string mood = "Custom")
		{
			Key = key;
			DisplayName = displayName;
			Language = language;
			Gender = gender;
			Mood = mood;
		}

		public override string ToString() => DisplayName;
	}

	public sealed class TtsResult
	{
		public bool Success { get; set; }
		public string AudioPath { get; set; } = string.Empty;
		public string SrtPath { get; set; } = string.Empty;
		public double DurationSeconds { get; set; } = 0.0;
		public string ErrorMessage { get; set; } = string.Empty;
	}

	public static class ScriptBatchTtsEngine
	{
		[DllImport("winmm.dll", CharSet = CharSet.Auto)]
		private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

		private static System.Windows.Media.MediaPlayer s_mediaPlayer = null;
		private static EventHandler s_currentMediaEndedHandler = null;

		public static readonly List<VoiceOption> AvailableVoices = new List<VoiceOption>
		{
			// 🇦🇴 🇲🇿 非洲葡语语系专区 (重点丰富非洲葡语偏好的欧葡/国际葡语高质感声音，男声阵容强化)
			new VoiceOption("pt-PT-DuarteNeural", "🇦🇴 莫桑/安哥拉标音 - Duarte 【深沉威严·力量男声 (非洲官方葡语)】★首选", "pt-PT", "男声", "深沉威严"),
			new VoiceOption("en-US-BrianMultilingualNeural", "🇦🇴 非洲国际葡语 - Brian 【极富磁性·浑厚力量男声】★推荐", "pt-PT", "男声", "磁性浑厚"),
			new VoiceOption("en-US-AndrewMultilingualNeural", "🇦🇴 非洲国际葡语 - Andrew 【激情昂扬·演说力量男声】", "pt-PT", "男声", "激情昂扬"),
			new VoiceOption("en-AU-WilliamMultilingualNeural", "🇦🇴 非洲国际葡语 - William 【成熟稳健·叙事男声】", "pt-PT", "男声", "成熟稳健"),
			new VoiceOption("pt-PT-RaquelNeural", "🇦🇴 莫桑/安哥拉标音 - Raquel 【纯正典雅·清澈女声】", "pt-PT", "女声", "纯正典雅"),

			// 🇧🇷 巴西葡语精选 (布道与温暖叙事)
			new VoiceOption("pt-BR-AntonioNeural", "🇧🇷 巴西葡语 - Antonio 【雄浑激昂·布道男声】★推荐", "pt-BR", "男声", "雄浑激昂"),
			new VoiceOption("pt-BR-FranciscaNeural", "🇧🇷 巴西葡语 - Francisca 【深情坚定·感染力女声】", "pt-BR", "女声", "深情坚定"),
			new VoiceOption("pt-BR-ThalitaMultilingualNeural", "🇧🇷 巴西葡语 - Thalita 【自然现代·亲切女声】", "pt-BR", "女声", "自然亲切"),

			// 🇺🇸 英语精选
			new VoiceOption("en-US-GuyNeural", "🇺🇸 英语 - Guy 【雄浑厚重·力量男声】", "en-US", "男声", "雄浑厚重"),
			new VoiceOption("en-US-ChristopherNeural", "🇺🇸 英语 - Christopher 【极富张力·演说男声】", "en-US", "男声", "激情昂扬"),
			new VoiceOption("en-US-JennyNeural", "🇺🇸 英语 - Jenny 【真挚知性·女声】", "en-US", "女声", "真挚知性"),

			// 🇨🇳 中文精选
			new VoiceOption("zh-CN-YunxiNeural", "🇨🇳 中文 - 云希 【爆款穿透·热情男声】", "zh-CN", "男声", "热情穿透"),
			new VoiceOption("zh-CN-YunjianNeural", "🇨🇳 中文 - 云健 【沉稳有力·纪录片男声】", "zh-CN", "男声", "沉稳有力")
		};

		public static string GetAuditionFilePath(string voiceKey)
		{
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string cand1 = Path.Combine(baseDir, "auditions", $"{voiceKey}.mp3");
			if (File.Exists(cand1)) return cand1;

			string cand2 = Path.Combine(baseDir, "ScriptBatch", "auditions", $"{voiceKey}.mp3");
			if (File.Exists(cand2)) return cand2;

			string devPath = Path.Combine(@"C:\Users\PcVkH\.gemini\antigravity\scratch\VideoBatchStudio\auditions", $"{voiceKey}.mp3");
			if (File.Exists(devPath)) return devPath;

			string dPath = Path.Combine(@"D:\工具\视频拼接\VideoBatchStudio\auditions", $"{voiceKey}.mp3");
			if (File.Exists(dPath)) return dPath;

			return string.Empty;
		}

		public static void StopAudio()
		{
			try
			{
				if (s_mediaPlayer != null)
				{
					if (s_currentMediaEndedHandler != null)
					{
						s_mediaPlayer.MediaEnded -= s_currentMediaEndedHandler;
						s_currentMediaEndedHandler = null;
					}
					s_mediaPlayer.Stop();
					s_mediaPlayer.Close();
				}
			}
			catch { }

			try
			{
				mciSendString("close sb_audition", null, 0, IntPtr.Zero);
			}
			catch { }
		}

		public static bool PlayAudioFile(string path, Action onEnded = null)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
			StopAudio();

			// 优先使用 PresentationCore MediaPlayer 在软件进程内后台原生播放
			try
			{
				if (s_mediaPlayer == null)
				{
					s_mediaPlayer = new System.Windows.Media.MediaPlayer();
				}

				if (onEnded != null)
				{
					s_currentMediaEndedHandler = (s, e) =>
					{
						StopAudio();
						onEnded?.Invoke();
					};
					s_mediaPlayer.MediaEnded += s_currentMediaEndedHandler;
				}

				s_mediaPlayer.Open(new Uri(Path.GetFullPath(path)));
				s_mediaPlayer.Play();
				return true;
			}
			catch
			{
				// 兜底使用 Windows 系统底层 MCI 无窗静默播放
				try
				{
					mciSendString($"open \"{Path.GetFullPath(path)}\" type mpegvideo alias sb_audition", null, 0, IntPtr.Zero);
					mciSendString("play sb_audition", null, 0, IntPtr.Zero);
					return true;
				}
				catch
				{
					return false;
				}
			}
		}

		public static bool PlayInstantAudition(string voiceKey, Action onEnded = null)
		{
			string path = GetAuditionFilePath(voiceKey);
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
			return PlayAudioFile(path, onEnded);
		}

		public static string FindPythonExe()
		{
			string direct = @"C:\Users\PcVkH\AppData\Local\Programs\Python\Python312\python.exe";
			if (File.Exists(direct)) return direct;

			string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string standardPy312 = Path.Combine(localApp, @"Programs\Python\Python312\python.exe");
			if (File.Exists(standardPy312)) return standardPy312;

			string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
			foreach (string p in pathEnv.Split(Path.PathSeparator))
			{
				if (string.IsNullOrWhiteSpace(p)) continue;
				try
				{
					string cand = Path.Combine(p.Trim(), "python.exe");
					if (File.Exists(cand)) return cand;
				}
				catch { }
			}
			return "python.exe";
		}

		public static string FindWorkerScript()
		{
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string cand1 = Path.Combine(baseDir, "ScriptBatch", "edge_tts_worker.py");
			if (File.Exists(cand1)) return cand1;

			string cand2 = Path.Combine(baseDir, "edge_tts_worker.py");
			if (File.Exists(cand2)) return cand2;

			string devPath = @"C:\Users\PcVkH\.gemini\antigravity\scratch\VideoBatchStudio\VideoBatchMerger\ScriptBatch\edge_tts_worker.py";
			if (File.Exists(devPath)) return devPath;

			return cand1;
		}

		public static async Task<TtsResult> SynthesizeAsync(
			string text,
			string voice,
			string rate,
			string pitch,
			string boundary,
			string outputDir,
			int taskId,
			CancellationToken ct)
		{
			var result = new TtsResult();
			if (string.IsNullOrWhiteSpace(text))
			{
				result.ErrorMessage = "文案内容为空";
				return result;
			}

			Directory.CreateDirectory(outputDir);
			string textFile = Path.Combine(outputDir, $"script_{taskId}.txt");
			string audioFile = Path.Combine(outputDir, $"audio_{taskId}.mp3");
			string srtFile = Path.Combine(outputDir, $"sub_{taskId}.srt");

			File.WriteAllText(textFile, text.Trim(), Encoding.UTF8);

			string pyExe = FindPythonExe();
			string workerScript = FindWorkerScript();

			if (!File.Exists(workerScript))
			{
				result.ErrorMessage = $"未找到 TTS 核心脚本: {workerScript}";
				return result;
			}

			string args = $"\"{workerScript}\" --text-file \"{textFile}\" --voice \"{voice}\" --rate \"{rate}\" --pitch \"{pitch}\" --boundary \"{boundary}\" --output-audio \"{audioFile}\" --output-srt \"{srtFile}\"";

			var psi = new ProcessStartInfo
			{
				FileName = pyExe,
				Arguments = args,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};

			try
			{
				using (var proc = new Process { StartInfo = psi })
				{
					var outputBuilder = new StringBuilder();
					var errorBuilder = new StringBuilder();

					proc.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
					proc.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

					proc.Start();
					proc.BeginOutputReadLine();
					proc.BeginErrorReadLine();

					while (!proc.HasExited)
					{
						if (ct.IsCancellationRequested)
						{
							try { proc.Kill(); } catch { }
							result.ErrorMessage = "用户取消了生成任务";
							return result;
						}
						await Task.Delay(100, ct);
					}
						// HasExited can become true before asynchronous stdout/stderr callbacks finish.
						// WaitForExit also drains those callbacks, so the worker's final JSON is available.
						proc.WaitForExit();

					string outStr = outputBuilder.ToString().Trim();
					string errStr = errorBuilder.ToString().Trim();

					if (proc.ExitCode != 0)
					{
						result.ErrorMessage = $"TTS 进程执行失败 (Code {proc.ExitCode}): {errStr}\n{outStr}";
						return result;
					}

					string jsonLine = string.Empty;
					foreach (var line in outStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
					{
						if (line.Trim().StartsWith("{") && line.Trim().EndsWith("}"))
						{
							jsonLine = line.Trim();
							break;
						}
					}

					if (string.IsNullOrEmpty(jsonLine))
					{
						result.ErrorMessage = $"未能获取 TTS 输出数据: {outStr}";
						return result;
					}

					var serializer = new JavaScriptSerializer();
					var dict = serializer.Deserialize<Dictionary<string, object>>(jsonLine);

					if (dict != null && dict.ContainsKey("success") && Convert.ToBoolean(dict["success"]))
					{
						result.Success = true;
						result.AudioPath = audioFile;
						result.SrtPath = srtFile;

						result.DurationSeconds = ParseDurationFromSrt(srtFile);
						if (result.DurationSeconds <= 0.1 && File.Exists(audioFile))
						{
							long bytes = new FileInfo(audioFile).Length;
							result.DurationSeconds = Math.Max(1.0, bytes / 16000.0);
						}
					}
					else
					{
						result.ErrorMessage = dict != null && dict.ContainsKey("error") ? dict["error"]?.ToString() : "TTS 合成未知错误";
					}
				}
			}
			catch (Exception ex)
			{
				result.ErrorMessage = $"调用 TTS 异常: {ex.Message}";
			}

			return result;
		}

		private static double ParseDurationFromSrt(string srtPath)
		{
			if (!File.Exists(srtPath)) return 0.0;
			try
			{
				string[] lines = File.ReadAllLines(srtPath, Encoding.UTF8);
				double maxSeconds = 0.0;
				foreach (string line in lines)
				{
					if (line.Contains("-->"))
					{
						string[] parts = line.Split(new[] { "-->" }, StringSplitOptions.RemoveEmptyEntries);
						if (parts.Length == 2)
						{
							string endStr = parts[1].Trim().Replace(',', '.');
							if (TimeSpan.TryParse(endStr, out var ts))
							{
								if (ts.TotalSeconds > maxSeconds)
								{
									maxSeconds = ts.TotalSeconds;
								}
							}
						}
					}
				}
				return maxSeconds;
			}
			catch
			{
				return 0.0;
			}
		}
	}
}
