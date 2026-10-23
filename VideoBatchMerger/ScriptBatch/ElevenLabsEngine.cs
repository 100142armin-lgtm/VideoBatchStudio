using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace VideoBatchMerger.ScriptBatch
{
	public static class ElevenLabsEngine
	{
		private static readonly HttpClient s_httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

		// 官方精选男声（特别针对非洲/莫桑比克/安哥拉葡萄牙语、英语、布道与激励视频）
		public static readonly List<VoiceOption> DefaultElevenVoices = new List<VoiceOption>
		{
			new VoiceOption("pNInz6obpgDQGcFmaJgB", "✨ Adam (原版母本音色 · 经典深沉男声) ★首选推荐", "Multilingual", "男声", "深沉磁性"),
			new VoiceOption("Idqyq11u9Zkm7d6j0bU3", "✨ Marcus (沉稳权威 · 浑厚播报男声) ★推荐", "Multilingual", "男声", "权威沉稳"),
			new VoiceOption("JBFqnCBsd6RMkjVDRZzb", "✨ George (温暖沙哑 · 传道叙事男声)", "Multilingual", "男声", "温暖叙事"),
			new VoiceOption("ErXwobaYiN019PkySvjV", "✨ Antoni (起伏生动 · 情感丰富男声)", "Multilingual", "男声", "生动传神"),
			new VoiceOption("onwK4e9ZLuTAKqWW03F9", "✨ Daniel (英伦绅士 · 广播质感男声)", "Multilingual", "男声", "端庄深厚"),
			new VoiceOption("IKne3meq5aSn9XLyUdCD", "✨ Charlie (自然自信 · 随性讲述男声)", "Multilingual", "男声", "随性自然"),
			new VoiceOption("N2lVS1w4EtoT3dr4eOWO", "✨ Callum (激昂坚定 · 爆发力男声)", "Multilingual", "男声", "力量激昂"),
			new VoiceOption("TX3LPaxmHKxFdv7VOQHJ", "✨ Liam (年轻现代 · 清晰坚定男声)", "Multilingual", "男声", "清澈坚定"),
			new VoiceOption("CustomVoice", "🔧 自定义克隆 Voice ID (在下方输入框填写)", "Custom", "自定义", "专属克隆")
		};

		// 官方预录无损试听 CDN 映射表（试听 100% 免费，直连官方 CDN，绝不消耗任何 API 字符配额）
		public static readonly Dictionary<string, string> VoicePreviewUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "pNInz6obpgDQGcFmaJgB", "https://storage.googleapis.com/eleven-public-prod/previews/models/pNInz6obpgDQGcFmaJgB.mp3" }, // Adam
			{ "Idqyq11u9Zkm7d6j0bU3", "https://storage.googleapis.com/eleven-public-prod/previews/models/Idqyq11u9Zkm7d6j0bU3.mp3" }, // Marcus
			{ "JBFqnCBsd6RMkjVDRZzb", "https://storage.googleapis.com/eleven-public-prod/previews/models/JBFqnCBsd6RMkjVDRZzb.mp3" }, // George
			{ "ErXwobaYiN019PkySvjV", "https://storage.googleapis.com/eleven-public-prod/previews/models/ErXwobaYiN019PkySvjV.mp3" }, // Antoni
			{ "onwK4e9ZLuTAKqWW03F9", "https://storage.googleapis.com/eleven-public-prod/previews/models/onwK4e9ZLuTAKqWW03F9.mp3" }, // Daniel
			{ "IKne3meq5aSn9XLyUdCD", "https://storage.googleapis.com/eleven-public-prod/previews/models/IKne3meq5aSn9XLyUdCD.mp3" }, // Charlie
			{ "N2lVS1w4EtoT3dr4eOWO", "https://storage.googleapis.com/eleven-public-prod/previews/models/N2lVS1w4EtoT3dr4eOWO.mp3" }, // Callum
			{ "TX3LPaxmHKxFdv7VOQHJ", "https://storage.googleapis.com/eleven-public-prod/previews/models/TX3LPaxmHKxFdv7VOQHJ.mp3" }, // Liam
			{ "VR6AewLTigWG4xSOukaG", "https://storage.googleapis.com/eleven-public-prod/previews/models/VR6AewLTigWG4xSOukaG.mp3" }, // Arnold
			{ "2EiwWnXFnvU5JabPnv8n", "https://storage.googleapis.com/eleven-public-prod/previews/models/2EiwWnXFnvU5JabPnv8n.mp3" }  // Clyde
		};

		static ElevenLabsEngine()
		{
			try
			{
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
			}
			catch { }
		}

		public static async Task<List<VoiceOption>> FetchAccountVoicesAsync(string apiKey)
		{
			var list = new List<VoiceOption>();
			if (string.IsNullOrWhiteSpace(apiKey)) return list;

			try
			{
				using (var req = new HttpRequestMessage(HttpMethod.Get, "https://api.elevenlabs.io/v1/voices"))
				{
					req.Headers.Add("xi-api-key", apiKey.Trim());
					using (var resp = await s_httpClient.SendAsync(req))
					{
						if (!resp.IsSuccessStatusCode) return list;
						string body = await resp.Content.ReadAsStringAsync();

						var serializer = new JavaScriptSerializer();
						var dict = serializer.Deserialize<Dictionary<string, object>>(body);
						if (dict != null && dict.ContainsKey("voices") && dict["voices"] is System.Collections.ArrayList arr)
						{
							foreach (var item in arr)
							{
								if (item is Dictionary<string, object> vDict)
								{
									string vid = vDict.ContainsKey("voice_id") ? vDict["voice_id"]?.ToString() : "";
									string name = vDict.ContainsKey("name") ? vDict["name"]?.ToString() : "";
									string category = vDict.ContainsKey("category") ? vDict["category"]?.ToString() : "cloned";
									string previewUrl = vDict.ContainsKey("preview_url") ? vDict["preview_url"]?.ToString() : null;

									if (!string.IsNullOrEmpty(vid) && !string.IsNullOrEmpty(previewUrl))
									{
										VoicePreviewUrls[vid] = previewUrl;
									}

									if (!string.IsNullOrEmpty(vid) && !string.IsNullOrEmpty(name))
									{
										string display = $"🌟 [{category}] {name} (ID: {vid.Substring(0, Math.Min(6, vid.Length))}...)";
										list.Add(new VoiceOption(vid, display, "Custom", "AI", category));
									}
								}
							}
						}
					}
				}
			}
			catch { }
			return list;
		}

		public static async Task<TtsResult> SynthesizeWithFailoverAsync(
			string text,
			string voiceId,
			string outputDir,
			int taskId,
			CancellationToken ct)
		{
			var pool = ElevenLabsKeyPool.Instance;
			var triedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			while (true)
			{
				if (ct.IsCancellationRequested)
				{
					return new TtsResult { ErrorMessage = "用户取消了生成任务" };
				}

				// 获取下一个可用的 Key (严格保障：已确认为“有效”且额度充足的 Key 绝对优先！)
				var candidateKey = pool.Keys
					.Where(k => !triedKeys.Contains(k.ApiKey) && k.Status == "有效" && k.RemainingCharacters > 50)
					.OrderByDescending(k => k.RemainingCharacters)
					.FirstOrDefault();

				if (candidateKey == null)
				{
					// 若没有已检测有效的，才尝试剩余尚未测试的 Key (坚决排除风控拦截、失效、配额用尽)
					candidateKey = pool.Keys
						.Where(k => !triedKeys.Contains(k.ApiKey) && k.Status == "未检测" && k.RemainingCharacters > 50)
						.OrderByDescending(k => k.RemainingCharacters)
						.FirstOrDefault();
				}

				if (candidateKey == null)
				{
					return new TtsResult
					{
						Success = false,
						ErrorMessage = "所有 ElevenLabs 密钥均已失效或配额耗尽！请点击【🔑 ElevenLabs 密钥管理】添加可用密钥，或切换回 Edge-TTS 免费引擎。"
					};
				}

				triedKeys.Add(candidateKey.ApiKey);

				// 尝试当前 Key 合成
				var result = await SynthesizeInternalAsync(text, voiceId, candidateKey.ApiKey, outputDir, taskId, ct);
				if (result.Success)
				{
					// 更新当前 Key 扣除字符数估算
					candidateKey.CharacterCount += text.Length;
					pool.Save();
					return result;
				}

				// 发生错误，判断是否为配额/失效/风控，记录并无缝切换下一个 Key
				string err = result.ErrorMessage ?? "";
				if (err.Contains("voice_not_found"))
				{
					return new TtsResult
					{
						Success = false,
						ErrorMessage = $"指定的 ElevenLabs Voice ID 【{voiceId}】在您的账户中不存在。\n请点击右侧【🎙️ 声音克隆工坊】核对 Voice ID 或重新克隆绑定！"
					};
				}

				pool.ReportKeyDepleted(candidateKey.ApiKey, err);
				// 继续下一次循环尝试下一个可用 Key (轮询容灾)
			}
		}

		private static async Task<TtsResult> SynthesizeInternalAsync(
			string text,
			string voiceId,
			string apiKey,
			string outputDir,
			int taskId,
			CancellationToken ct)
		{
			var res = new TtsResult();
			Directory.CreateDirectory(outputDir);
			string audioPath = Path.Combine(outputDir, $"eleven_{taskId}.mp3");
			string srtPath = Path.Combine(outputDir, $"eleven_sub_{taskId}.srt");

			string targetVoice = string.IsNullOrWhiteSpace(voiceId) ? "pNInz6obpgDQGcFmaJgB" : voiceId.Trim();
			if (targetVoice == "CustomVoice") targetVoice = "pNInz6obpgDQGcFmaJgB";

			// 优先尝试 with-timestamps 高精度时间轴端点
			string url = $"https://api.elevenlabs.io/v1/text-to-speech/{targetVoice}/with-timestamps";
			var payload = new
			{
				text = text,
				model_id = "eleven_multilingual_v2",
				voice_settings = new
				{
					stability = 0.50,
					similarity_boost = 0.78,
					style = 0.0,
					use_speaker_boost = true
				}
			};

			var serializer = new JavaScriptSerializer();
			string jsonBody = serializer.Serialize(payload);

			try
			{
				using (var req = new HttpRequestMessage(HttpMethod.Post, url))
				{
					req.Headers.Add("xi-api-key", apiKey);
					req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

					using (var response = await s_httpClient.SendAsync(req, ct))
					{
						if (response.IsSuccessStatusCode)
						{
							string respJson = await response.Content.ReadAsStringAsync();
							var dict = serializer.Deserialize<Dictionary<string, object>>(respJson);

							if (dict != null && dict.ContainsKey("audio_base64"))
							{
								string b64 = dict["audio_base64"]?.ToString();
								if (!string.IsNullOrEmpty(b64))
								{
									byte[] mp3Bytes = Convert.FromBase64String(b64);
									File.WriteAllBytes(audioPath, mp3Bytes);

									// 解析时间轴对齐数据生成完美 SRT 字幕
									if (dict.ContainsKey("alignment") && dict["alignment"] is Dictionary<string, object> align)
									{
										BuildSrtFromAlignment(align, text, srtPath);
									}
									else
									{
										BuildFallbackSrt(text, mp3Bytes.Length / 16000.0, srtPath);
									}

									res.Success = true;
									res.AudioPath = audioPath;
									res.SrtPath = srtPath;
									res.DurationSeconds = ParseDurationFromSrt(srtPath);
									if (res.DurationSeconds <= 0.5)
									{
										res.DurationSeconds = Math.Max(1.0, mp3Bytes.Length / 16000.0);
									}
									return res;
								}
							}
						}

						// 若 with-timestamps 返回 400 或不支持，降级到标准 text-to-speech 流端点
						if ((int)response.StatusCode == 400 || (int)response.StatusCode == 404)
						{
							return await SynthesizeStreamFallbackAsync(text, targetVoice, apiKey, audioPath, srtPath, ct);
						}

						string errResp = await response.Content.ReadAsStringAsync();
						res.ErrorMessage = $"ElevenLabs HTTP {(int)response.StatusCode}: {errResp}";
						return res;
					}
				}
			}
			catch (Exception ex)
			{
				res.ErrorMessage = $"ElevenLabs API 请求异常: {ex.Message}";
				return res;
			}
		}

		private static async Task<TtsResult> SynthesizeStreamFallbackAsync(
			string text,
			string voiceId,
			string apiKey,
			string audioPath,
			string srtPath,
			CancellationToken ct)
		{
			var res = new TtsResult();
			string url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";

			var payload = new
			{
				text = text,
				model_id = "eleven_multilingual_v2",
				voice_settings = new { stability = 0.5, similarity_boost = 0.75 }
			};

			var serializer = new JavaScriptSerializer();
			string jsonBody = serializer.Serialize(payload);

			using (var req = new HttpRequestMessage(HttpMethod.Post, url))
			{
				req.Headers.Add("xi-api-key", apiKey);
				req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

				using (var resp = await s_httpClient.SendAsync(req, ct))
				{
					if (resp.IsSuccessStatusCode)
					{
						byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
						File.WriteAllBytes(audioPath, bytes);

						double dur = Math.Max(1.0, bytes.Length / 16000.0);
						BuildFallbackSrt(text, dur, srtPath);

						res.Success = true;
						res.AudioPath = audioPath;
						res.SrtPath = srtPath;
						res.DurationSeconds = dur;
						return res;
					}

					res.ErrorMessage = $"ElevenLabs Fallback HTTP {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}";
					return res;
				}
			}
		}

		private static void BuildSrtFromAlignment(Dictionary<string, object> align, string originalText, string srtPath)
		{
			try
			{
				var charsList = align["characters"] as System.Collections.ArrayList;
				var startsList = align["character_start_times_seconds"] as System.Collections.ArrayList;
				var endsList = align["character_end_times_seconds"] as System.Collections.ArrayList;

				if (charsList == null || startsList == null || endsList == null || charsList.Count == 0)
				{
					BuildFallbackSrt(originalText, 5.0, srtPath);
					return;
				}

				var sb = new StringBuilder();
				int subIndex = 1;

				var curWord = new StringBuilder();
				double phraseStart = Convert.ToDouble(startsList[0]);
				double phraseEnd = Convert.ToDouble(endsList[0]);

				int count = Math.Min(charsList.Count, Math.Min(startsList.Count, endsList.Count));
				int phraseCharCount = 0;

				for (int i = 0; i < count; i++)
				{
					string ch = charsList[i]?.ToString() ?? "";
					double st = Convert.ToDouble(startsList[i]);
					double et = Convert.ToDouble(endsList[i]);

					if (phraseCharCount == 0)
					{
						phraseStart = st;
					}

					curWord.Append(ch);
					phraseEnd = et;
					phraseCharCount++;

					// 分句断句标点: . , ! ? ; : 换行 或 单词长度超过一定限制
					bool isPunctuation = ch == "." || ch == "!" || ch == "?" || ch == ";" || ch == "\n";
					bool isSoftComma = (ch == "," || ch == "，") && phraseCharCount > 18;
					bool isTooLong = phraseCharCount > 35 && (ch == " " || ch == "\t");

					if (isPunctuation || isSoftComma || isTooLong)
					{
						string line = curWord.ToString().Trim();
						if (!string.IsNullOrEmpty(line))
						{
							sb.AppendLine(subIndex.ToString());
							sb.AppendLine($"{FormatSrtTime(phraseStart)} --> {FormatSrtTime(phraseEnd)}");
							sb.AppendLine(line);
							sb.AppendLine();
							subIndex++;
						}
						curWord.Clear();
						phraseCharCount = 0;
					}
				}

				// 剩余未输出的短句
				if (curWord.Length > 0)
				{
					string line = curWord.ToString().Trim();
					if (!string.IsNullOrEmpty(line))
					{
						sb.AppendLine(subIndex.ToString());
						sb.AppendLine($"{FormatSrtTime(phraseStart)} --> {FormatSrtTime(phraseEnd)}");
						sb.AppendLine(line);
						sb.AppendLine();
					}
				}

				File.WriteAllText(srtPath, sb.ToString(), Encoding.UTF8);
			}
			catch
			{
				BuildFallbackSrt(originalText, 5.0, srtPath);
			}
		}

		private static void BuildFallbackSrt(string text, double totalDur, string srtPath)
		{
			var sb = new StringBuilder();
			var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			if (lines.Length == 0) lines = new[] { text };

			double slice = totalDur / lines.Length;
			for (int i = 0; i < lines.Length; i++)
			{
				double st = i * slice;
				double et = (i + 1) * slice;
				sb.AppendLine((i + 1).ToString());
				sb.AppendLine($"{FormatSrtTime(st)} --> {FormatSrtTime(et)}");
				sb.AppendLine(lines[i].Trim());
				sb.AppendLine();
			}
			File.WriteAllText(srtPath, sb.ToString(), Encoding.UTF8);
		}

		private static string FormatSrtTime(double seconds)
		{
			var ts = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
			return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
		}

		public static async Task<(bool Success, string VoiceId, string ErrorMessage)> CloneVoiceAsync(
			string voiceName,
			string description,
			string audioFilePath,
			string apiKey = null)
		{
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				apiKey = ElevenLabsKeyPool.Instance.GetNextAvailableKey();
			}

			if (string.IsNullOrWhiteSpace(apiKey))
			{
				return (false, null, "未配置可用的 ElevenLabs API 密钥，请先在【ElevenLabs密钥】管理中添加有效Key。");
			}

			if (!File.Exists(audioFilePath))
			{
				return (false, null, $"音频采样文件不存在: {audioFilePath}");
			}

			try
			{
				string url = "https://api.elevenlabs.io/v1/voices/add";
				using (var content = new MultipartFormDataContent())
				{
					content.Add(new StringContent(voiceName.Trim()), "name");
					if (!string.IsNullOrWhiteSpace(description))
					{
						content.Add(new StringContent(description.Trim()), "description");
					}

					byte[] fileBytes = File.ReadAllBytes(audioFilePath);
					var fileContent = new ByteArrayContent(fileBytes);
					string ext = Path.GetExtension(audioFilePath).ToLowerInvariant();
					string mediaType = ext == ".wav" ? "audio/wav" : "audio/mpeg";
					fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
					content.Add(fileContent, "files", Path.GetFileName(audioFilePath));

					using (var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content })
					{
						req.Headers.Add("xi-api-key", apiKey.Trim());
						using (var resp = await s_httpClient.SendAsync(req))
						{
							string respBody = await resp.Content.ReadAsStringAsync();
							if (resp.IsSuccessStatusCode)
							{
								var serializer = new JavaScriptSerializer();
								var dict = serializer.Deserialize<Dictionary<string, object>>(respBody);
								if (dict != null && dict.ContainsKey("voice_id"))
								{
									string vid = dict["voice_id"]?.ToString();
									return (true, vid, null);
								}
								return (false, null, "接口响应成功但未返回 voice_id");
							}

							if ((int)resp.StatusCode == 402 || respBody.Contains("tier") || respBody.Contains("plan") || respBody.Contains("permission"))
							{
								return (false, null, "【ElevenLabs 限制】当前使用的 API Key 账号为纯免费版或无声音克隆权限。\nElevenLabs 官方 Instant Voice Cloning 功能需要 Starter Plan 及以上账号权限。\n\n👉 解决方案：\n1. 可在官网上升级任意一个低成本 Starter 账号获取 Key 进行克隆；\n2. 或者如果已有已克隆好的 Voice ID，可直接在本窗口【手动录入VoiceID】一键绑定使用！");
							}

							return (false, null, $"ElevenLabs API 报错 ({(int)resp.StatusCode}): {respBody}");
						}
					}
				}
			}
			catch (Exception ex)
			{
				return (false, null, $"网络或调用异常: {ex.Message}");
			}
		}

		private static double ParseDurationFromSrt(string srtPath)
		{
			if (!File.Exists(srtPath)) return 0.0;
			try
			{
				string[] lines = File.ReadAllLines(srtPath, Encoding.UTF8);
				double maxSec = 0.0;
				foreach (var l in lines)
				{
					if (l.Contains("-->"))
					{
						var parts = l.Split(new[] { "-->" }, StringSplitOptions.RemoveEmptyEntries);
						if (parts.Length == 2)
						{
							string endStr = parts[1].Trim().Replace(',', '.');
							if (TimeSpan.TryParse(endStr, out var ts))
							{
								if (ts.TotalSeconds > maxSec) maxSec = ts.TotalSeconds;
							}
						}
					}
				}
				return maxSec;
			}
			catch { return 0.0; }
		}
	}
}
