using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace VideoBatchMerger.ScriptBatch
{
	public sealed class ElevenLabsKeyItem
	{
		public string ApiKey { get; set; } = string.Empty;
		public string Status { get; set; } = "未检测";
		public string Tier { get; set; } = "free";
		public int CharacterCount { get; set; } = 0;
		public int CharacterLimit { get; set; } = 10000;
		public string StatusDetail { get; set; } = "尚未检测";
		public DateTime LastChecked { get; set; } = DateTime.MinValue;

		public int RemainingCharacters => Math.Max(0, CharacterLimit - CharacterCount);

		public bool IsUsable => Status == "有效" && RemainingCharacters > 50;

		public string MaskedKey
		{
			get
			{
				if (string.IsNullOrWhiteSpace(ApiKey)) return string.Empty;
				string clean = ApiKey.Trim();
				if (clean.Length <= 8) return clean;
				return clean.Substring(0, 4) + "..." + clean.Substring(clean.Length - 4);
			}
		}
	}

	public sealed class ElevenLabsKeyPool
	{
		private static readonly object s_lock = new object();
		private static ElevenLabsKeyPool s_instance;

		private static readonly string SettingsPath = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"elevenlabs_keys.json"
		);

		private static readonly HttpClient s_http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };

		public List<ElevenLabsKeyItem> Keys { get; } = new List<ElevenLabsKeyItem>();

		public static ElevenLabsKeyPool Instance
		{
			get
			{
				if (s_instance == null)
				{
					lock (s_lock)
					{
						if (s_instance == null)
						{
							s_instance = Load();
						}
					}
				}
				return s_instance;
			}
		}

		static ElevenLabsKeyPool()
		{
			try
			{
				// 启用现代 TLS 1.2 与 1.3 协议
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
			}
			catch { }
		}

		public static ElevenLabsKeyPool Load()
		{
			var pool = new ElevenLabsKeyPool();
			try
			{
				string path = SettingsPath;
				if (!File.Exists(path))
				{
					string alt = Path.Combine(@"C:\Users\PcVkH\.gemini\antigravity\scratch\VideoBatchStudio", "elevenlabs_keys.json");
					if (File.Exists(alt)) path = alt;
				}

				if (File.Exists(path))
				{
					string json = File.ReadAllText(path, Encoding.UTF8);
					var serializer = new JavaScriptSerializer();
					var items = serializer.Deserialize<List<ElevenLabsKeyItem>>(json);
					if (items != null)
					{
						pool.Keys.AddRange(items);
					}
				}
			}
			catch
			{
				// Ignore parse errors on load
			}
			return pool;
		}

		public void Save()
		{
			lock (s_lock)
			{
				try
				{
					var serializer = new JavaScriptSerializer();
					string json = serializer.Serialize(Keys);
					File.WriteAllText(SettingsPath, json, Encoding.UTF8);

					string alt = Path.Combine(@"C:\Users\PcVkH\.gemini\antigravity\scratch\VideoBatchStudio", "elevenlabs_keys.json");
					try { File.WriteAllText(alt, json, Encoding.UTF8); } catch { }
				}
				catch { }
			}
		}

		public int AddKeys(string rawInput)
		{
			if (string.IsNullOrWhiteSpace(rawInput)) return 0;
			int added = 0;
			var delimiters = new[] { '\r', '\n', ',', ';', '\t' };
			var tokens = rawInput.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

			foreach (var token in tokens)
			{
				string clean = token.Trim().Trim('"', '\'');
				if (string.IsNullOrWhiteSpace(clean) || clean.Length < 10) continue;

				if (!Keys.Any(k => string.Equals(k.ApiKey, clean, StringComparison.OrdinalIgnoreCase)))
				{
					Keys.Add(new ElevenLabsKeyItem
					{
						ApiKey = clean,
						Status = "未检测",
						StatusDetail = "待检测"
					});
					added++;
				}
			}

			if (added > 0) Save();
			return added;
		}

		public bool RemoveKey(string key)
		{
			int idx = Keys.FindIndex(k => string.Equals(k.ApiKey, key, StringComparison.OrdinalIgnoreCase));
			if (idx >= 0)
			{
				Keys.RemoveAt(idx);
				Save();
				return true;
			}
			return false;
		}

		public void ClearAll()
		{
			Keys.Clear();
			Save();
		}

		public async Task<bool> CheckKeyAsync(ElevenLabsKeyItem item)
		{
			if (item == null || string.IsNullOrWhiteSpace(item.ApiKey)) return false;

			// 若之前在合成中已被官方标记为风控拦截，保留受限状态，避免 /v1/user 虚假返回200误导为可用
			if (item.Status == "风控拦截")
			{
				item.LastChecked = DateTime.Now;
				item.CharacterCount = item.CharacterLimit;
				item.StatusDetail = "🔴 官方风控拦截: 检测到同IP/代理创建多免费账号，Free权限已被禁用 (建议升级Starter或更换网络)";
				Save();
				return false;
			}

			try
			{
				using (var req = new HttpRequestMessage(HttpMethod.Get, "https://api.elevenlabs.io/v1/user"))
				{
					req.Headers.Add("xi-api-key", item.ApiKey.Trim());
					using (var resp = await s_http.SendAsync(req))
					{
						item.LastChecked = DateTime.Now;
						string body = await resp.Content.ReadAsStringAsync();

						if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
						{
							if (body.Contains("detected_unusual_activity"))
							{
								item.Status = "风控拦截";
								item.StatusDetail = "🔴 官方风控拦截: 检测到同IP/代理创建多免费账号，Free权限已被禁用";
							}
							else
							{
								// 兼容 VideoKit：部分免费 Key 访问 /user 接口受限，但不影响直接生成 TTS
								item.Status = "有效";
								item.StatusDetail = $"🟢 免费 Key (已就绪 · 未开放查询详情)";
							}
							Save();
							return item.Status == "有效";
						}

						if (resp.IsSuccessStatusCode)
						{
							var serializer = new JavaScriptSerializer();
							var dict = serializer.Deserialize<Dictionary<string, object>>(body);

							Dictionary<string, object> subDict = null;
							if (dict != null && dict.ContainsKey("subscription") && dict["subscription"] is Dictionary<string, object> sd)
							{
								subDict = sd;
							}
							else
							{
								subDict = dict;
							}

							int count = subDict != null && subDict.ContainsKey("character_count") ? Convert.ToInt32(subDict["character_count"]) : 0;
							int limit = subDict != null && subDict.ContainsKey("character_limit") ? Convert.ToInt32(subDict["character_limit"]) : 10000;
							string tier = subDict != null && subDict.ContainsKey("tier") ? subDict["tier"]?.ToString() ?? "free" : "free";
							string statusStr = subDict != null && subDict.ContainsKey("status") ? subDict["status"]?.ToString() ?? "active" : "active";

							item.CharacterCount = count;
							item.CharacterLimit = limit;
							item.Tier = tier;

							int remain = Math.Max(0, limit - count);
							if (remain <= 0 || statusStr.Equals("past_due", StringComparison.OrdinalIgnoreCase))
							{
								item.Status = "配额用尽";
								item.StatusDetail = $"🟡 配额已耗尽 (0 / {limit:N0} 字符 | {tier})";
							}
							else
							{
								item.Status = "有效";
								item.StatusDetail = $"🟢 正常可用 (剩余 {remain:N0} / {limit:N0} 字符 | {tier})";
							}

							Save();
							return item.Status == "有效";
						}
						else if ((int)resp.StatusCode == 429)
						{
							item.Status = "超频";
							item.StatusDetail = "🟡 请求过于频繁 (HTTP 429 Rate Limit)，稍后自动恢复";
							Save();
							return false;
						}
						else
						{
							item.Status = "检测异常";
							item.StatusDetail = $"⚠️ HTTP {(int)resp.StatusCode}: {body}";
							Save();
							return false;
						}
					}
				}
			}
			catch (Exception ex)
			{
				item.LastChecked = DateTime.Now;
				item.Status = "网络错误";
				item.StatusDetail = $"⚠️ 连接失败: {ex.Message}";
				return false;
			}
		}

		public async Task CheckAllAsync(Action<int, int, ElevenLabsKeyItem> progressCallback = null)
		{
			for (int i = 0; i < Keys.Count; i++)
			{
				var keyItem = Keys[i];
				await CheckKeyAsync(keyItem);
				progressCallback?.Invoke(i + 1, Keys.Count, keyItem);
			}
			Save();
		}

		public ElevenLabsKeyItem GetNextUsableKey()
		{
			lock (s_lock)
			{
				// 1. 严格优先返回已确认为“有效”且剩余额度 > 50 的 Key
				var valid = Keys.Where(k => k.Status == "有效" && k.RemainingCharacters > 50)
								.OrderByDescending(k => k.RemainingCharacters)
								.FirstOrDefault();

				if (valid != null) return valid;

				// 2. 若无已检测有效的，才尝试尚未检测的 Key (坚决排除风控拦截、失效、配额用尽)
				return Keys.FirstOrDefault(k => k.Status == "未检测" && k.RemainingCharacters > 50);
			}
		}

		public string GetNextAvailableKey()
		{
			return GetNextUsableKey()?.ApiKey;
		}

		public void ReportKeyDepleted(string apiKey, string reason)
		{
			// 如果是声音 ID 不存在 (HTTP 404 / voice_not_found)，属于声音配置问题，不应把有效 API Key 判定为额度用尽
			if (!string.IsNullOrEmpty(reason) && (reason.Contains("voice_not_found") || reason.Contains("404")))
			{
				return;
			}

			lock (s_lock)
			{
				var item = Keys.FirstOrDefault(k => string.Equals(k.ApiKey, apiKey, StringComparison.OrdinalIgnoreCase));
				if (item != null)
				{
					if (reason.Contains("detected_unusual_activity"))
					{
						item.Status = "风控拦截";
						item.CharacterCount = item.CharacterLimit; // 冻结额度，避免误导为还有10000字可用
						item.StatusDetail = "🔴 官方风控拦截: 检测到同IP/代理创建多免费账号，Free权限已被禁用 (建议升级Starter或更换网络)";
					}
					else if (reason.Contains("401") || reason.ToLower().Contains("unauthorized") || reason.ToLower().Contains("invalid_api_key"))
					{
						item.Status = "失效";
						item.StatusDetail = "🔴 密钥已被官方注销或失效";
					}
					else if (reason.Contains("429") || reason.ToLower().Contains("rate"))
					{
						item.Status = "超频";
						item.StatusDetail = "🟡 请求频率过高 (HTTP 429)，已自动切换其他密钥";
					}
					else
					{
						item.Status = "配额用尽";
						item.StatusDetail = $"🟡 {reason}";
					}
					Save();
				}
			}
		}
	}
}
