using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
		public static string GetSynthesisCacheKey(ScriptBatchSettings settings, string text, string voice)
		{
			string referenceStamp = "";
			if (File.Exists(settings.ChatterboxReferenceAudio))
			{
				var file = new FileInfo(settings.ChatterboxReferenceAudio);
				referenceStamp = file.Length + ":" + file.LastWriteTimeUtc.Ticks;
			}
			var serialized = new JavaScriptSerializer().Serialize(new
			{
				settings.TtsEngine, voice, text = text?.Trim(), settings.SpeechRate, settings.SpeechPitch,
				settings.SpeechVolume, settings.ElevenLabsCustomVoiceId,
				settings.ChatterboxLanguage, settings.ChatterboxExaggeration, settings.ChatterboxCfg,
				settings.ChatterboxReferenceAudio, settings.ChatterboxReferenceAuthorized, referenceStamp
			});
			using (var hash = SHA256.Create())
				return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(serialized))).Replace("-", "");
		}

		[DllImport("winmm.dll", CharSet = CharSet.Auto)]
		private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

		private static System.Windows.Media.MediaPlayer s_mediaPlayer = null;
		private static EventHandler s_currentMediaEndedHandler = null;
		private static double s_volume = 1.0;

		public static bool IsPlaying { get; private set; }
		public static bool IsPaused { get; private set; }
		public static string CurrentPlayingPath { get; private set; } = string.Empty;

		public static readonly List<VoiceOption> AvailableVoices = new List<VoiceOption>
		{
			// 🇦🇴 🇲🇿 非洲葡语语系专区 (重点丰富非洲葡语偏好的欧葡/国际葡语高质感声音，男声与自然口播阵容强化)
			new VoiceOption("pt-PT-DuarteNeural", "🇦🇴 莫桑/安哥拉标音 - Duarte 【深沉威严·力量男声 (非洲官方葡语)】★首选", "pt-PT", "男声", "深沉威严"),
			new VoiceOption("en-US-BrianMultilingualNeural", "🇦🇴 非洲国际葡语 - Brian 【极富磁性·浑厚力量男声】★推荐", "pt-PT", "男声", "磁性浑厚"),
			new VoiceOption("en-US-AndrewMultilingualNeural", "🇦🇴 非洲国际葡语 - Andrew 【激情昂扬·演说力量男声】", "pt-PT", "男声", "激情昂扬"),
			new VoiceOption("fr-FR-RemyMultilingualNeural", "🇦🇴 非洲国际葡语 - Remy 【欧陆温润·真实口播男声】★自然", "pt-PT", "男声", "温润口播"),
			new VoiceOption("en-AU-WilliamMultilingualNeural", "🇦🇴 非洲国际葡语 - William 【成熟稳健·叙事男声】", "pt-PT", "男声", "成熟稳健"),
			new VoiceOption("en-US-EmmaMultilingualNeural", "🇦🇴 非洲国际葡语 - Emma 【亲切自然·日常唠嗑女声】★推荐", "pt-PT", "女声", "自然唠嗑"),
			new VoiceOption("en-US-AvaMultilingualNeural", "🇦🇴 非洲国际葡语 - Ava 【年轻活力·带货口播女声】", "pt-PT", "女声", "年轻活力"),
			new VoiceOption("pt-PT-RaquelNeural", "🇦🇴 莫桑/安哥拉标音 - Raquel 【纯正典雅·清澈女声】", "pt-PT", "女声", "纯正典雅"),
			new VoiceOption("fr-FR-VivienneMultilingualNeural", "🇦🇴 非洲国际葡语 - Vivienne 【欧陆知性·温柔口播女声】", "pt-PT", "女声", "知性温柔"),
			new VoiceOption("pt-PT-BoyChildNeural", "🇦🇴 非洲国际葡语 - Lucas 【纯真清脆·阳光祈祷男童】★童声", "pt-PT", "男童", "纯真男童"),
			new VoiceOption("pt-PT-GirlChildNeural", "🇦🇴 非洲国际葡语 - Sofia 【甜美乖巧·天使治愈女童】★童声", "pt-PT", "女童", "甜美女童"),

			// 🇧🇷 巴西葡语精选 (布道、带货与温暖叙事 + 男童女童)
			new VoiceOption("pt-BR-AntonioNeural", "🇧🇷 巴西葡语 - Antonio 【雄浑激昂·布道男声】★推荐", "pt-BR", "男声", "雄浑激昂"),
			new VoiceOption("de-DE-FlorianMultilingualNeural", "🇧🇷 国际葡语 - Florian 【沉稳磁性·短视频叙事男声】", "pt-BR", "男声", "沉稳磁性"),
			new VoiceOption("pt-BR-FranciscaNeural", "🇧🇷 巴西葡语 - Francisca 【深情坚定·感染力女声】", "pt-BR", "女声", "深情坚定"),
			new VoiceOption("pt-BR-ThalitaMultilingualNeural", "🇧🇷 巴西葡语 - Thalita 【自然现代·亲切女声】", "pt-BR", "女声", "自然亲切"),
			new VoiceOption("pt-BR-BoyChildNeural", "🇧🇷 巴西葡语 - Miguel 【活泼灵动·清澈故事男童】★童声", "pt-BR", "男童", "活泼男童"),
			new VoiceOption("pt-BR-GirlChildNeural", "🇧🇷 巴西葡语 - Leticia 【清澈软萌·纯真祝福女童】★童声", "pt-BR", "女童", "软萌女童"),

			// 🇿🇦 🇺🇸 🇫🇷 🇪🇸 非洲英语与国际外语精选 + 国际童声
			new VoiceOption("en-ZA-LukeNeural", "🇿🇦 非洲英语 (南非) - Luke 【纯正非英·磁性口播男声】", "en-ZA", "男声", "纯正非英"),
			new VoiceOption("en-ZA-LeahNeural", "🇿🇦 非洲英语 (南非) - Leah 【纯正非英·亲切口播女声】", "en-ZA", "女声", "纯正非英"),
			new VoiceOption("en-US-GuyNeural", "🇺🇸 英语 - Guy 【雄浑厚重·力量男声】", "en-US", "男声", "雄浑厚重"),
			new VoiceOption("en-US-ChristopherNeural", "🇺🇸 英语 - Christopher 【极富张力·演说男声】", "en-US", "男声", "激情昂扬"),
			new VoiceOption("en-US-JennyNeural", "🇺🇸 英语 - Jenny 【真挚知性·女声】", "en-US", "女声", "真挚知性"),
			new VoiceOption("en-US-AnaNeural", "🇺🇸 国际英语/多语 - Ana 【微软原生纯真女童·童声故事】★原生童声", "en-US", "女童", "原生女童"),
			new VoiceOption("en-US-BoyChildNeural", "🇺🇸 英语 - Leo 【清亮活泼·阳光故事男童】★童声", "en-US", "男童", "清亮男童"),
			new VoiceOption("fr-FR-HenriNeural", "🇫🇷 国际法语 (西非通用) - Henri 【浑厚叙事·口播男声】", "fr-FR", "男声", "浑厚叙事"),
			new VoiceOption("es-MX-JorgeNeural", "🇲🇽 拉美西语 - Jorge 【热情活力·爆款口播男声】", "es-MX", "男声", "热情活力"),

			// 🇨🇳 中文精选 + 原生男童女童
			new VoiceOption("zh-CN-YunxiNeural", "🇨🇳 中文 - 云希 【爆款穿透·热情男声】", "zh-CN", "男声", "热情穿透"),
			new VoiceOption("zh-CN-YunjianNeural", "🇨🇳 中文 - 云健 【沉稳有力·纪录片男声】", "zh-CN", "男声", "沉稳有力"),
			new VoiceOption("zh-CN-YunxiaNeural", "🇨🇳 中文 - 云夏 【微软原生活泼男童·故事/动画】★原生童声", "zh-CN", "男童", "原生男童"),
			new VoiceOption("zh-CN-XiaoshuangNeural", "🇨🇳 中文 - 晓双 【微软原生乖巧女童·清脆童声】★原生童声", "zh-CN", "女童", "原生女童")
		};

		public static bool IsChildVoice(string voiceKey)
		{
			if (string.IsNullOrWhiteSpace(voiceKey)) return false;
			return voiceKey.IndexOf("Child", StringComparison.OrdinalIgnoreCase) >= 0 ||
			       string.Equals(voiceKey, "en-US-AnaNeural", StringComparison.OrdinalIgnoreCase) ||
			       string.Equals(voiceKey, "zh-CN-YunxiaNeural", StringComparison.OrdinalIgnoreCase) ||
			       string.Equals(voiceKey, "zh-CN-XiaoshuangNeural", StringComparison.OrdinalIgnoreCase);
		}

		public static string GetAuditionFilePath(string voiceKey)
		{
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string cand1 = Path.Combine(baseDir, "auditions", $"{voiceKey}.mp3");
			if (File.Exists(cand1)) return cand1;

			string cand2 = Path.Combine(baseDir, "ScriptBatch", "auditions", $"{voiceKey}.mp3");
			if (File.Exists(cand2)) return cand2;

			// 若为新增童声音色且本地尚未生成独立缓存，映射至同语种高保真母本以便离线毫秒级变调试听
			string fallbackKey = voiceKey switch
			{
				"pt-PT-BoyChildNeural" => "fr-FR-RemyMultilingualNeural",
				"pt-PT-GirlChildNeural" => "en-US-AvaMultilingualNeural",
				"pt-BR-BoyChildNeural" => "de-DE-FlorianMultilingualNeural",
				"pt-BR-GirlChildNeural" => "pt-BR-ThalitaMultilingualNeural",
				"en-US-BoyChildNeural" => "en-US-BrianMultilingualNeural",
				"en-US-AnaNeural" => "en-US-JennyNeural",
				"zh-CN-YunxiaNeural" => "zh-CN-YunxiNeural",
				"zh-CN-XiaoshuangNeural" => "zh-CN-YunxiNeural",
				_ => string.Empty
			};

			if (!string.IsNullOrEmpty(fallbackKey))
			{
				string f1 = Path.Combine(baseDir, "auditions", $"{fallbackKey}.mp3");
				if (File.Exists(f1)) return f1;
				string f2 = Path.Combine(baseDir, "ScriptBatch", "auditions", $"{fallbackKey}.mp3");
				if (File.Exists(f2)) return f2;
			}

			return string.Empty;
		}

		public static void StopAudio()
		{
			IsPlaying = false;
			IsPaused = false;
			CurrentPlayingPath = string.Empty;
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

		public static void PauseAudio()
		{
			if (!IsPlaying || IsPaused) return;
			try
			{
				s_mediaPlayer?.Pause();
				IsPaused = true;
			}
			catch { }
		}

		public static void ResumeAudio()
		{
			if (!IsPlaying || !IsPaused) return;
			try
			{
				s_mediaPlayer?.Play();
				IsPaused = false;
			}
			catch { }
		}

		public static void SetVolume(double volume0To1)
		{
			s_volume = Math.Max(0.0, Math.Min(1.0, volume0To1));
			try
			{
				if (s_mediaPlayer != null)
				{
					s_mediaPlayer.Volume = s_volume;
				}
			}
			catch { }
		}

		public static double GetCurrentPositionSeconds()
		{
			try
			{
				if (s_mediaPlayer != null && IsPlaying)
				{
					return s_mediaPlayer.Position.TotalSeconds;
				}
			}
			catch { }
			return 0.0;
		}

		public static double GetTotalDurationSeconds()
		{
			try
			{
				if (s_mediaPlayer != null && s_mediaPlayer.NaturalDuration.HasTimeSpan)
				{
					return s_mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
				}
			}
			catch { }
			return 0.0;
		}

		public static void SetPositionSeconds(double seconds)
		{
			try
			{
				if (s_mediaPlayer != null && IsPlaying)
				{
					double total = GetTotalDurationSeconds();
					if (total > 0.1)
					{
						seconds = Math.Max(0.0, Math.Min(total - 0.05, seconds));
					}
					s_mediaPlayer.Position = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
				}
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

				s_mediaPlayer.Volume = s_volume;
				s_currentMediaEndedHandler = (s, e) =>
				{
					StopAudio();
					onEnded?.Invoke();
				};
				s_mediaPlayer.MediaEnded += s_currentMediaEndedHandler;

				s_mediaPlayer.Open(new Uri(Path.GetFullPath(path)));
				s_mediaPlayer.Play();
				IsPlaying = true;
				IsPaused = false;
				CurrentPlayingPath = path;
				return true;
			}
			catch
			{
				// 兜底使用 Windows 系统底层 MCI 无窗静默播放
				try
				{
					mciSendString($"open \"{Path.GetFullPath(path)}\" type mpegvideo alias sb_audition", null, 0, IntPtr.Zero);
					mciSendString("play sb_audition", null, 0, IntPtr.Zero);
					IsPlaying = true;
					IsPaused = false;
					CurrentPlayingPath = path;
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
			string portable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "edge_runtime", "python", "python.exe");
			if (File.Exists(portable)) return portable;

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

			string safeBoundary = string.Equals(boundary, "WordBoundary", StringComparison.OrdinalIgnoreCase)
				? "WordBoundary"
				: "SentenceBoundary";
			string args = $"{ScriptBatchComposer.QuoteArg(workerScript)} --text-file {ScriptBatchComposer.QuoteArg(textFile)} --voice {ScriptBatchComposer.QuoteArg(voice ?? "pt-BR-AntonioNeural")} --rate {ScriptBatchComposer.QuoteArg(rate ?? "+0%")} --pitch {ScriptBatchComposer.QuoteArg(pitch ?? "+0Hz")} --boundary {ScriptBatchComposer.QuoteArg(safeBoundary)} --output-audio {ScriptBatchComposer.QuoteArg(audioFile)} --output-srt {ScriptBatchComposer.QuoteArg(srtFile)}";

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
						try
						{
							await Task.Delay(100, ct);
						}
						catch (OperationCanceledException)
						{
							try { proc.Kill(); } catch { }
							result.ErrorMessage = "用户取消了生成任务";
							return result;
						}
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

		/// <summary>
		/// 针对口播与短视频外文（尤其是非洲葡语/欧葡/巴葡/英语）进行自然气口与断句优化，
		/// 消除无标点连读、生硬换行导致的“机械念稿感”，让神经网络模型自然触发换气与语调起伏。
		/// </summary>
		public static string NaturalizeScriptForOralBroadcast(string rawText, bool enableBreathingPause)
		{
			if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
			string text = rawText.Trim();
			if (!enableBreathingPause) return text;

			// 将多行无标点换行转化为自然句读停顿
			string[] rawLines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
			var sb = new StringBuilder();
			for (int i = 0; i < rawLines.Length; i++)
			{
				string line = rawLines[i].Trim();
				if (string.IsNullOrEmpty(line)) continue;
				sb.Append(line);
				char last = line[line.Length - 1];
				bool hasEndPunc = last == '.' || last == '!' || last == '?' || last == ';' || last == ':' ||
				                  last == ',' || last == '。' || last == '！' || last == '？' || last == '；' || last == '，' || last == '…';
				if (!hasEndPunc && i < rawLines.Length - 1)
				{
					sb.Append(". ");
				}
				else if (i < rawLines.Length - 1)
				{
					sb.Append(" ");
				}
			}

			string normalized = sb.ToString();
			// 确保西文标点后带有标准空格，以便神经网络触发自然的短语尾音延展与换气气口
			normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"([,.;:!?])([^\s\d""'])", "$1 $2");
			normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s{2,}", " ");
			return normalized.Trim();
		}

		/// <summary>
		/// 将合成的语音转换为标准广播级 320Kbps 48kHz MP3，并可选注入电容麦温润人声 EQ（或童声专属清亮共振峰调音）与动态压缩。
		/// </summary>
		public static async Task<bool> MasterAudioToMp3Async(string inputAudioPath, string outputMp3Path, bool warmBroadcastEq, CancellationToken ct, string voiceKey = null)
		{
			if (string.IsNullOrWhiteSpace(inputAudioPath) || !File.Exists(inputAudioPath)) return false;
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputMp3Path)));

			string ffmpeg = ScriptBatchComposer.FindFfmpegExe();
			bool isChild = IsChildVoice(voiceKey);
			string afFilter = "";
			if (isChild)
			{
				// 童声专属清亮共振峰调音：滤除成人胸腔低频浑浊感 (210Hz)，提亮童声清脆度 (3200Hz)
				afFilter = "-af \"highpass=f=135,equalizer=f=215:width_type=o:width=1.2:g=-2.2,equalizer=f=3200:width_type=o:width=1.4:g=2.2,acompressor=threshold=-18dB:ratio=2.0:attack=12:release=160:makeup=1.6dB\"";
			}
			else if (warmBroadcastEq)
			{
				afFilter = "-af \"highpass=f=70,equalizer=f=175:width_type=o:width=1.2:g=2.0,equalizer=f=3400:width_type=o:width=1.4:g=1.6,acompressor=threshold=-18dB:ratio=2.2:attack=15:release=180:makeup=1.5dB\"";
			}

			string args = $"-y -loglevel error -i {ScriptBatchComposer.QuoteArg(inputAudioPath)} {afFilter} -ar 48000 -ac 2 -c:a libmp3lame -b:a 320k {ScriptBatchComposer.QuoteArg(outputMp3Path)}";

			try
			{
				bool ok = await Task.Run(() =>
				{
					var psi = new ProcessStartInfo
					{
						FileName = ffmpeg,
						Arguments = args,
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardError = true
					};
					using (var proc = Process.Start(psi))
					{
						if (proc == null) return false;
						while (!proc.WaitForExit(100))
						{
							if (ct.IsCancellationRequested)
							{
								try { proc.Kill(); } catch { }
								return false;
							}
						}
						return proc.ExitCode == 0 && File.Exists(outputMp3Path) && new FileInfo(outputMp3Path).Length > 256;
					}
				}, ct);

				if (ok) return true;

				// 若带 EQ 滤镜在极简版 FFmpeg 下不支持，自动回退为无滤镜 320Kbps 标准重编码
				if (warmBroadcastEq && !ct.IsCancellationRequested)
				{
					string fallbackArgs = $"-y -loglevel error -i {ScriptBatchComposer.QuoteArg(inputAudioPath)} -ar 48000 -ac 2 -c:a libmp3lame -b:a 320k {ScriptBatchComposer.QuoteArg(outputMp3Path)}";
					bool fallbackOk = await Task.Run(() =>
					{
						var psi = new ProcessStartInfo
						{
							FileName = ffmpeg,
							Arguments = fallbackArgs,
							UseShellExecute = false,
							CreateNoWindow = true
						};
						using (var proc = Process.Start(psi))
						{
							if (proc == null) return false;
							proc.WaitForExit(15000);
							return proc.ExitCode == 0 && File.Exists(outputMp3Path) && new FileInfo(outputMp3Path).Length > 256;
						}
					}, ct);
					if (fallbackOk) return true;
				}
			}
			catch { }

			// 终极兜底：直接复制原音频文件
			try
			{
				if (!string.Equals(Path.GetFullPath(inputAudioPath), Path.GetFullPath(outputMp3Path), StringComparison.OrdinalIgnoreCase))
				{
					File.Copy(inputAudioPath, outputMp3Path, true);
				}
				return File.Exists(outputMp3Path);
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// 提取音频文件的真实振幅包络数据（用于驱动舞台中央的流光黑胶与 64 段动态频谱实时律动）。
		/// </summary>
		public static async Task<float[]> ExtractWaveformEnvelopeAsync(string audioPath, int bucketCount = 240)
		{
			if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath) || bucketCount <= 8)
				return null;

			return await Task.Run(() =>
			{
				try
				{
					string ffmpeg = ScriptBatchComposer.FindFfmpegExe();
					var psi = new ProcessStartInfo
					{
						FileName = ffmpeg,
						Arguments = $"-loglevel quiet -i {ScriptBatchComposer.QuoteArg(audioPath)} -ac 1 -ar 1000 -f s16le -",
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardOutput = true
					};

					using (var proc = Process.Start(psi))
					{
						if (proc == null) return null;
						using (var ms = new MemoryStream())
						{
							proc.StandardOutput.BaseStream.CopyTo(ms);
							proc.WaitForExit(5000);
							byte[] raw = ms.ToArray();
							int sampleCount = raw.Length / 2;
							if (sampleCount < bucketCount) return null;

							float[] buckets = new float[bucketCount];
							int samplesPerBucket = Math.Max(1, sampleCount / bucketCount);
							float maxPeak = 0.01f;

							for (int b = 0; b < bucketCount; b++)
							{
								int start = b * samplesPerBucket;
								int end = Math.Min(sampleCount, start + samplesPerBucket);
								double sumSq = 0;
								int count = 0;
								for (int s = start; s < end; s++)
								{
									short val = BitConverter.ToInt16(raw, s * 2);
									float norm = Math.Abs(val) / 32768f;
									sumSq += norm * norm;
									count++;
								}
								float rms = count > 0 ? (float)Math.Sqrt(sumSq / count) : 0f;
								buckets[b] = rms;
								if (rms > maxPeak) maxPeak = rms;
							}

							for (int b = 0; b < bucketCount; b++)
							{
								buckets[b] = Math.Min(1.0f, (buckets[b] / maxPeak) * 1.15f);
							}
							return buckets;
						}
					}
				}
				catch
				{
					return null;
				}
			});
		}

		public static string GetPresetSampleSentence(string voiceKey)
		{
			if (string.IsNullOrWhiteSpace(voiceKey))
				return "Olá amigos! Que Deus abençoe a sua família hoje e sempre.";
			if (voiceKey.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
				return "大家好，欢迎来到智能口播配音工坊，现在听到的是实时调音效果。";
			if (voiceKey.StartsWith("es-", StringComparison.OrdinalIgnoreCase))
				return "Hola a todos, hoy quiero compartir un mensaje muy especial con ustedes.";
			if (voiceKey.StartsWith("fr-FR-Henri", StringComparison.OrdinalIgnoreCase))
				return "Bonjour à tous, bienvenue dans notre studio vocal professionnel.";
			if (voiceKey.StartsWith("en-ZA", StringComparison.OrdinalIgnoreCase) ||
			    voiceKey.StartsWith("en-US-Guy", StringComparison.OrdinalIgnoreCase) ||
			    voiceKey.StartsWith("en-US-Christopher", StringComparison.OrdinalIgnoreCase) ||
			    voiceKey.StartsWith("en-US-Jenny", StringComparison.OrdinalIgnoreCase) ||
			    voiceKey.StartsWith("en-US-Ana", StringComparison.OrdinalIgnoreCase) ||
			    voiceKey.StartsWith("en-US-BoyChild", StringComparison.OrdinalIgnoreCase))
				return "Hello everyone! Welcome back to our channel, listen carefully to this message.";
			return "Senhora que está segurando um telefone, talvez eu não saiba o seu nome, mas você é abençoada.";
		}

		/// <summary>
		/// 基于本地预置样音文件，离线毫秒级（~50ms）应用右侧设置菜单中的【语速】、【语调】与【广播级电容麦调音】，
		/// 实现“无需等待文案合成，点试听键立即听见当前设置参数下的真实声音”。
		/// </summary>
		public static async Task<string> RenderTunedPresetSampleAsync(
			string baseSamplePath,
			string voiceKey,
			string rateStr,
			string pitchStr,
			bool warmBroadcastEq,
			CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(baseSamplePath) || !File.Exists(baseSamplePath))
				return baseSamplePath;

			double rate = rateStr switch
			{
				"+5%" => 1.05,
				"+8%" => 1.08,
				"+10%" => 1.10,
				"+15%" => 1.15,
				"-5%" => 0.95,
				"-10%" => 0.90,
				_ => 1.00
			};

			double pitchFactor = pitchStr switch
			{
				"+2Hz" => 1.025,
				"-2Hz" => 0.975,
				"-4Hz" => 0.945,
				_ => 1.000
			};

			bool isChild = IsChildVoice(voiceKey);
			string baseFileKey = Path.GetFileNameWithoutExtension(baseSamplePath);
			bool usingFallbackAdultSample = isChild && !string.Equals(baseFileKey, voiceKey, StringComparison.OrdinalIgnoreCase);
			if (usingFallbackAdultSample)
			{
				bool isGirl = voiceKey.IndexOf("Girl", StringComparison.OrdinalIgnoreCase) >= 0 ||
				              voiceKey.IndexOf("Ana", StringComparison.OrdinalIgnoreCase) >= 0 ||
				              voiceKey.IndexOf("Xiaoshuang", StringComparison.OrdinalIgnoreCase) >= 0;
				pitchFactor *= isGirl ? 1.28 : 1.24;
				rate *= 1.04;
			}

			// 若全是默认原声且未开 EQ 且非童声母本变调，直接 0 毫秒返回原预置样音
			if (!isChild && Math.Abs(rate - 1.0) < 0.005 && Math.Abs(pitchFactor - 1.0) < 0.005 && !warmBroadcastEq)
			{
				return baseSamplePath;
			}

			string cacheDir = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_PresetTunedCache");
			Directory.CreateDirectory(cacheDir);
			string safeVoice = string.IsNullOrWhiteSpace(voiceKey) ? "voice" : Path.GetFileNameWithoutExtension(voiceKey);
			string tag = $"{safeVoice}_r{(int)(rate * 100)}_p{(int)(pitchFactor * 1000)}_eq{(warmBroadcastEq ? 1 : 0)}_fb{(usingFallbackAdultSample ? 1 : 0)}";
			string outPath = Path.Combine(cacheDir, $"{tag}.mp3");

			if (File.Exists(outPath) && new FileInfo(outPath).Length > 1024)
			{
				return outPath;
			}

			string ffmpeg = ScriptBatchComposer.FindFfmpegExe();
			var filters = new List<string>();

			// 变调不改速 + 变速不改调组合: asetrate 改变音高，atempo 补偿速度并叠加目标语速
			double tempoComp = rate / pitchFactor;
			tempoComp = Math.Max(0.5, Math.Min(2.0, tempoComp));
			if (Math.Abs(pitchFactor - 1.0) >= 0.005)
			{
				string pStr = pitchFactor.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
				filters.Add($"asetrate=24000*{pStr},aresample=48000");
			}
			if (Math.Abs(tempoComp - 1.0) >= 0.005)
			{
				string tStr = tempoComp.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
				filters.Add($"atempo={tStr}");
			}
			if (isChild)
			{
				filters.Add("highpass=f=135,equalizer=f=215:width_type=o:width=1.2:g=-2.2,equalizer=f=3200:width_type=o:width=1.4:g=2.2,acompressor=threshold=-18dB:ratio=2.0:attack=12:release=160:makeup=1.6dB");
			}
			else if (warmBroadcastEq)
			{
				filters.Add("highpass=f=70,equalizer=f=175:width_type=o:width=1.2:g=2.0,equalizer=f=3400:width_type=o:width=1.4:g=1.6,acompressor=threshold=-18dB:ratio=2.2:attack=15:release=180:makeup=1.5dB");
			}

			string afArg = filters.Count > 0 ? $"-af \"{string.Join(",", filters)}\"" : "";
			string args = $"-y -loglevel error -i {ScriptBatchComposer.QuoteArg(baseSamplePath)} {afArg} -ar 48000 -ac 2 -c:a libmp3lame -b:a 256k {ScriptBatchComposer.QuoteArg(outPath)}";

			try
			{
				bool ok = await Task.Run(() =>
				{
					var psi = new ProcessStartInfo
					{
						FileName = ffmpeg,
						Arguments = args,
						UseShellExecute = false,
						CreateNoWindow = true
					};
					using (var proc = Process.Start(psi))
					{
						if (proc == null) return false;
						proc.WaitForExit(4000);
						return proc.ExitCode == 0 && File.Exists(outPath) && new FileInfo(outPath).Length > 512;
					}
				}, ct);
				if (ok) return outPath;
			}
			catch { }

			return baseSamplePath;
		}
	}
}
