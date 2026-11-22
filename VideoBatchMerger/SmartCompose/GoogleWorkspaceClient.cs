using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace VideoBatchMerger.SmartCompose
{
	public sealed class DriveVideoFileInfo
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string MimeType { get; set; } = string.Empty;
		public long Size { get; set; }
		public string Md5Checksum { get; set; } = string.Empty;
		public string ModifiedTime { get; set; } = string.Empty;
	}

	public static class GoogleWorkspaceClient
	{
		static GoogleWorkspaceClient()
		{
			try
			{
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
				ServicePointManager.DefaultConnectionLimit = 16;
			}
			catch { }
		}

		private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };

		private static readonly Regex SheetIdRegex = new Regex(@"/spreadsheets/d/([a-zA-Z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex SheetGidRegex = new Regex(@"[#?&]gid=(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex DriveFolderRegex = new Regex(@"/folders/([a-zA-Z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex DriveFileRegex = new Regex(@"(?:/file/d/|/d/|[?&]id=)([a-zA-Z0-9_-]{15,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex UrlExtractRegex = new Regex(@"https?://[^\s""'<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public static bool TryImportClientSecretJson(string jsonPath, SmartComposeSettings settings, out string error)
		{
			error = string.Empty;
			try
			{
				string raw = File.ReadAllText(jsonPath, Encoding.UTF8);
				var root = Json.DeserializeObject(raw) as Dictionary<string, object>;
				if (root == null) { error = "文件不是有效的 JSON 格式。"; return false; }

				Dictionary<string, object> node = null;
				if (root.TryGetValue("installed", out object instObj) && instObj is Dictionary<string, object> instDict)
					node = instDict;
				else if (root.TryGetValue("web", out object webObj) && webObj is Dictionary<string, object> webDict)
					node = webDict;
				else
					node = root;

				string cid = GetString(node, "client_id");
				string csec = GetString(node, "client_secret");
				if (string.IsNullOrWhiteSpace(cid) || string.IsNullOrWhiteSpace(csec))
				{
					error = "JSON 中未找到 client_id 或 client_secret，请确认下载的是 Google Cloud 桌面应用 OAuth 凭据。";
					return false;
				}
				settings.GoogleClientId = cid.Trim();
				settings.GoogleClientSecret = csec.Trim();
				settings.Save();
				return true;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				return false;
			}
		}

		public static string ExtractSpreadsheetId(string urlOrId)
		{
			if (string.IsNullOrWhiteSpace(urlOrId)) return string.Empty;
			string trimmed = urlOrId.Trim();
			var m = SheetIdRegex.Match(trimmed);
			if (m.Success) return m.Groups[1].Value;
			if (Regex.IsMatch(trimmed, @"^[a-zA-Z0-9_-]{20,}$")) return trimmed;
			return string.Empty;
		}

		public static string ExtractDriveId(string urlOrId, out bool isFolder)
		{
			isFolder = false;
			if (string.IsNullOrWhiteSpace(urlOrId)) return string.Empty;
			string trimmed = urlOrId.Trim();
			var fm = DriveFolderRegex.Match(trimmed);
			if (fm.Success)
			{
				isFolder = true;
				return fm.Groups[1].Value;
			}
			var dm = DriveFileRegex.Match(trimmed);
			if (dm.Success)
			{
				isFolder = false;
				return dm.Groups[1].Value;
			}
			if (Regex.IsMatch(trimmed, @"^[a-zA-Z0-9_-]{18,}$"))
			{
				isFolder = true;
				return trimmed;
			}
			return string.Empty;
		}

		public static string ColumnIndexToLetter(int zeroBasedIndex)
		{
			int dividend = zeroBasedIndex + 1;
			string name = string.Empty;
			while (dividend > 0)
			{
				int modulo = (dividend - 1) % 26;
				name = Convert.ToChar('A' + modulo) + name;
				dividend = (dividend - modulo) / 26;
			}
			return name;
		}

		public static int ParseColumnLetterToIndex(string columnSpec)
		{
			if (string.IsNullOrWhiteSpace(columnSpec)) return -1;
			string s = columnSpec.Trim();
			// 兼容 "A 列 · 文案" 或纯 "A" / "AB"
			var m = Regex.Match(s, @"^([A-Za-z]{1,3})\b");
			if (!m.Success) return -1;
			string letters = m.Groups[1].Value.ToUpperInvariant();
			int sum = 0;
			foreach (char c in letters)
			{
				sum = sum * 26 + (c - 'A' + 1);
			}
			return sum - 1;
		}

		private static HttpClient CreateHttpClient(string proxyUrl, int timeoutSeconds = 30, bool allowAutoRedirect = true)
		{
			if (ServicePointManager.DefaultConnectionLimit < 64)
				ServicePointManager.DefaultConnectionLimit = 64;
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			ServicePointManager.Expect100Continue = false;
			ServicePointManager.UseNagleAlgorithm = false;
			var handler = new HttpClientHandler
			{
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
				AllowAutoRedirect = allowAutoRedirect
			};
			if (!string.IsNullOrWhiteSpace(proxyUrl))
			{
				handler.Proxy = new WebProxy(proxyUrl.Trim());
				handler.UseProxy = true;
			}
			var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(Math.Max(10, timeoutSeconds)) };
			client.DefaultRequestHeaders.UserAgent.ParseAdd("VideoBatchStudio-SmartCompose/1.0");
			return client;
		}

		private static int GetFreeLoopbackPort()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			int port = ((IPEndPoint)listener.LocalEndpoint).Port;
			listener.Stop();
			return port;
		}

		/// <summary>
		/// OAuth 2.0 桌面端浏览器一键授权（支持普通 Gmail 个人云盘读写 & 谷歌表格回填）
		/// </summary>
		public static async Task<string> AuthorizeInteractiveAsync(SmartComposeSettings settings, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(settings.GoogleClientId) || string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
			{
				throw new InvalidOperationException("请先导入 Google Cloud 桌面应用 client_secret.json 凭据文件。");
			}

			int port = GetFreeLoopbackPort();
			string redirectUri = $"http://127.0.0.1:{port}/";
			string state = Guid.NewGuid().ToString("N");
			string scope = Uri.EscapeDataString("https://www.googleapis.com/auth/drive https://www.googleapis.com/auth/spreadsheets https://www.googleapis.com/auth/userinfo.email");
			string authUrl =
				$"https://accounts.google.com/o/oauth2/v2/auth" +
				$"?client_id={Uri.EscapeDataString(settings.GoogleClientId)}" +
				$"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
				$"&response_type=code" +
				$"&scope={scope}" +
				$"&access_type=offline" +
				$"&prompt=consent" +
				$"&state={state}";

			using var listener = new HttpListener();
			listener.Prefixes.Add(redirectUri);
			listener.Start();

			Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

			string code = null;
			using (ct.Register(() => { try { listener.Stop(); } catch { } }))
			{
				var context = await listener.GetContextAsync();
				string err = context.Request.QueryString["error"];
				string returnedState = context.Request.QueryString["state"];
				code = context.Request.QueryString["code"];

				string html = string.IsNullOrEmpty(err) && !string.IsNullOrEmpty(code)
					? "<html><meta charset='utf-8'><body style='font-family:Microsoft YaHei;background:#0f172a;color:#e2e8f0;text-align:center;padding:60px;'><h2>✅ Google 账号授权成功！</h2><p>您可以关闭此网页，直接返回 VideoBatchStudio 使用【智能合成】。</p></body></html>"
					: $"<html><meta charset='utf-8'><body style='font-family:Microsoft YaHei;background:#0f172a;color:#fca5a5;text-align:center;padding:60px;'><h2>❌ 授权未完成</h2><p>{WebUtility.HtmlEncode(err ?? "未收到授权码")}</p></body></html>";

				byte[] respBytes = Encoding.UTF8.GetBytes(html);
				context.Response.ContentType = "text/html; charset=utf-8";
				context.Response.ContentLength64 = respBytes.Length;
				await context.Response.OutputStream.WriteAsync(respBytes, 0, respBytes.Length);
				context.Response.Close();

				if (!string.IsNullOrEmpty(err))
					throw new InvalidOperationException($"Google 授权被取消或拒绝: {err}");
				if (string.IsNullOrEmpty(code) || !string.Equals(returnedState, state, StringComparison.Ordinal))
					throw new InvalidOperationException("Google 授权回调校验失败，请重试。");
			}

			using var http = CreateHttpClient(settings.ProxyUrl, 25);
			var form = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["code"] = code,
				["client_id"] = settings.GoogleClientId,
				["client_secret"] = settings.GoogleClientSecret,
				["redirect_uri"] = redirectUri,
				["grant_type"] = "authorization_code"
			});

			var tokenResp = await http.PostAsync("https://oauth2.googleapis.com/token", form, ct).ConfigureAwait(false);
			string tokenBody = await tokenResp.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!tokenResp.IsSuccessStatusCode)
			{
				throw new InvalidOperationException($"换取 Google 令牌失败 ({(int)tokenResp.StatusCode}): {tokenBody}");
			}

			var tokenDict = Json.DeserializeObject(tokenBody) as Dictionary<string, object>;
			settings.GoogleAccessToken = GetString(tokenDict, "access_token");
			string refresh = GetString(tokenDict, "refresh_token");
			if (!string.IsNullOrWhiteSpace(refresh)) settings.GoogleRefreshToken = refresh;
			int expiresIn = GetInt(tokenDict, "expires_in", 3500);
			settings.GoogleTokenExpiryUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, expiresIn - 120);

			// 获取绑定邮箱
			try
			{
				using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
				req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.GoogleAccessToken);
				var userResp = await http.SendAsync(req, ct);
				if (userResp.IsSuccessStatusCode)
				{
					var userDict = Json.DeserializeObject(await userResp.Content.ReadAsStringAsync()) as Dictionary<string, object>;
					settings.GoogleAccountEmail = GetString(userDict, "email");
				}
			}
			catch { }

			settings.Save();
			return string.IsNullOrWhiteSpace(settings.GoogleAccountEmail) ? "已授权 Google 账号" : settings.GoogleAccountEmail;
		}

		public static async Task<string> EnsureValidAccessTokenAsync(SmartComposeSettings settings, CancellationToken ct)
		{
			if (!settings.HasGoogleAuth)
			{
				throw new InvalidOperationException("尚未绑定 Google 账号，请先点击【绑定 Google 账号】。");
			}

			long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			if (!string.IsNullOrWhiteSpace(settings.GoogleAccessToken) && settings.GoogleTokenExpiryUnix > now)
			{
				return settings.GoogleAccessToken;
			}

			if (string.IsNullOrWhiteSpace(settings.GoogleRefreshToken))
			{
				throw new InvalidOperationException("Google 授权已过期且缺少刷新令牌，请点击【重新绑定 Google 账号】。");
			}

			using var http = CreateHttpClient(settings.ProxyUrl, 25);
			var form = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["client_id"] = settings.GoogleClientId,
				["client_secret"] = settings.GoogleClientSecret,
				["refresh_token"] = settings.GoogleRefreshToken,
				["grant_type"] = "refresh_token"
			});
			var resp = await http.PostAsync("https://oauth2.googleapis.com/token", form, ct).ConfigureAwait(false);
			string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!resp.IsSuccessStatusCode)
			{
				throw new InvalidOperationException($"自动续期 Google 授权失败 ({(int)resp.StatusCode})，请重新绑定账号: {body}");
			}
			var dict = Json.DeserializeObject(body) as Dictionary<string, object>;
			settings.GoogleAccessToken = GetString(dict, "access_token");
			int expiresIn = GetInt(dict, "expires_in", 3500);
			settings.GoogleTokenExpiryUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Math.Max(60, expiresIn - 120);
			settings.Save();
			return settings.GoogleAccessToken;
		}

		public static string ExtractSheetGid(string url)
		{
			if (string.IsNullOrWhiteSpace(url)) return string.Empty;
			var m = SheetGidRegex.Match(url);
			return m.Success ? m.Groups[1].Value : string.Empty;
		}

		public static async Task<(List<string> Tabs, string SelectedTab, List<SheetColumnHeader> Headers)> ReadSheetTabsAndHeadersAsync(
			SmartComposeSettings settings,
			string preferredTabName,
			CancellationToken ct,
			bool preferUrlGid = false)
		{
			string spreadsheetId = ExtractSpreadsheetId(settings.SheetUrl);
			if (string.IsNullOrWhiteSpace(spreadsheetId))
				throw new InvalidOperationException("请输入有效的 Google 表格链接。");

			string token = await EnsureValidAccessTokenAsync(settings, ct).ConfigureAwait(false);
			using var http = CreateHttpClient(settings.ProxyUrl, 25);

			// 1. 获取所有工作表名称与 sheetId (gid)
			using var metaReq = new HttpRequestMessage(HttpMethod.Get,
				$"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}?fields=sheets.properties(sheetId,title)");
			metaReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			var metaResp = await http.SendAsync(metaReq, ct).ConfigureAwait(false);
			string metaBody = await metaResp.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!metaResp.IsSuccessStatusCode)
				throw new InvalidOperationException($"读取谷歌表格结构失败 ({(int)metaResp.StatusCode}): {metaBody}");

			var metaDict = Json.DeserializeObject(metaBody) as Dictionary<string, object>;
			var tabs = new List<string>();
			string gidMatchedTab = string.Empty;
			string urlGid = ExtractSheetGid(settings.SheetUrl);

			if (metaDict != null && metaDict.TryGetValue("sheets", out object sheetsObj) && sheetsObj is object[] sheetsArr)
			{
				foreach (var s in sheetsArr)
				{
					if (s is Dictionary<string, object> sd &&
					    sd.TryGetValue("properties", out object pObj) && pObj is Dictionary<string, object> pd)
					{
						string title = GetString(pd, "title");
						string sheetId = GetString(pd, "sheetId");
						if (!string.IsNullOrWhiteSpace(title))
						{
							tabs.Add(title);
							if (!string.IsNullOrEmpty(urlGid) && string.Equals(sheetId, urlGid, StringComparison.Ordinal))
							{
								gidMatchedTab = title;
							}
						}
					}
				}
			}
			if (tabs.Count == 0) tabs.Add("Sheet1");

			string activeTab;
			if (preferUrlGid && !string.IsNullOrWhiteSpace(gidMatchedTab))
				activeTab = gidMatchedTab;
			else if (!string.IsNullOrWhiteSpace(preferredTabName) && tabs.Contains(preferredTabName))
				activeTab = preferredTabName;
			else if (!string.IsNullOrWhiteSpace(gidMatchedTab))
				activeTab = gidMatchedTab;
			else
				activeTab = tabs[0];

			// 2. 读取第 1 行表头 (1:1)
			string range = Uri.EscapeDataString($"'{activeTab.Replace("'", "''")}'!1:1");
			using var headReq = new HttpRequestMessage(HttpMethod.Get,
				$"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}/values/{range}");
			headReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			var headResp = await http.SendAsync(headReq, ct).ConfigureAwait(false);
			string headBody = await headResp.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!headResp.IsSuccessStatusCode)
				throw new InvalidOperationException($"读取表格表头失败 ({(int)headResp.StatusCode}): {headBody}");

			var headers = new List<SheetColumnHeader>();
			var headDict = Json.DeserializeObject(headBody) as Dictionary<string, object>;
			if (headDict != null && headDict.TryGetValue("values", out object valsObj) && valsObj is object[] rows && rows.Length > 0 && rows[0] is object[] firstRow)
			{
				for (int i = 0; i < firstRow.Length; i++)
				{
					headers.Add(new SheetColumnHeader
					{
						ColumnIndex = i,
						ColumnLetter = ColumnIndexToLetter(i),
						HeaderText = (firstRow[i]?.ToString() ?? string.Empty).Trim()
					});
				}
			}
			// 至少补齐到 12 列，方便空白列也能被选为“成品链接回填列”或“状态回填列”
			int targetColCount = Math.Max(12, headers.Count + 2);
			for (int i = headers.Count; i < targetColCount; i++)
			{
				headers.Add(new SheetColumnHeader
				{
					ColumnIndex = i,
					ColumnLetter = ColumnIndexToLetter(i),
					HeaderText = string.Empty
				});
			}

			return (tabs, activeTab, headers);
		}

		/// <summary>
		/// 读取谷歌表格待处理任务行（使用 includeGridData=true 完整提取单元格内的隐藏超链接与巧克力胶囊 Smart Chips）
		/// </summary>
		public static async Task<List<SmartComposeTaskItem>> ReadPendingTasksAsync(SmartComposeSettings settings, CancellationToken ct)
		{
			string spreadsheetId = ExtractSpreadsheetId(settings.SheetUrl);
			if (string.IsNullOrWhiteSpace(spreadsheetId))
				throw new InvalidOperationException("请先填写有效的 Google 表格链接。");

			int colScript = ParseColumnLetterToIndex(settings.ColScript);
			int colSource = ParseColumnLetterToIndex(settings.ColSourceUrl);
			int colOut = ParseColumnLetterToIndex(settings.ColOutputLink);
			int colSeq = ParseColumnLetterToIndex(settings.ColTaskSeq);
			int colTask = ParseColumnLetterToIndex(settings.ColTaskName);
			int colStatus = ParseColumnLetterToIndex(settings.ColStatus);

			if (colSource < 0)
				throw new InvalidOperationException("请先在左侧选择「素材链接列」（点击【读取表头】即可下拉选择）。");

			string token = await EnsureValidAccessTokenAsync(settings, ct).ConfigureAwait(false);
			using var http = CreateHttpClient(settings.ProxyUrl, 45);

			string tabName = string.IsNullOrWhiteSpace(settings.SheetTabName) ? "Sheet1" : settings.SheetTabName;
			string range = Uri.EscapeDataString($"'{tabName.Replace("'", "''")}'!A1:AZ500");
			string url = $"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}?ranges={range}&includeGridData=true&fields=sheets.data.rowData.values(formattedValue,hyperlink,textFormatRuns,chipRuns)";

			using var req = new HttpRequestMessage(HttpMethod.Get, url);
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
			string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!resp.IsSuccessStatusCode)
				throw new InvalidOperationException($"拉取表格任务失败 ({(int)resp.StatusCode}): {body}");

			var result = new List<SmartComposeTaskItem>();
			var root = Json.DeserializeObject(body) as Dictionary<string, object>;
			if (root == null || !root.TryGetValue("sheets", out object sheetsObj) || !(sheetsObj is object[] sheetsArr) || sheetsArr.Length == 0)
				return result;

			var firstSheet = sheetsArr[0] as Dictionary<string, object>;
			if (firstSheet == null || !firstSheet.TryGetValue("data", out object dataObj) || !(dataObj is object[] dataArr) || dataArr.Length == 0)
				return result;

			var grid = dataArr[0] as Dictionary<string, object>;
			if (grid == null || !grid.TryGetValue("rowData", out object rowsObj) || !(rowsObj is object[] rowData))
				return result;

			int seq = 0;
			// 第 0 行是表头，从第 1 行开始读取任务
			for (int r = 1; r < rowData.Length; r++)
			{
				var rowDict = rowData[r] as Dictionary<string, object>;
				if (rowDict == null || !rowDict.TryGetValue("values", out object valsObj) || !(valsObj is object[] cells))
					continue;

				string statusVal = colStatus >= 0 && colStatus < cells.Length ? ExtractCellText(cells[colStatus]) : string.Empty;
				bool statusDone = !string.IsNullOrWhiteSpace(statusVal) &&
				    (statusVal.Contains("已完成") || statusVal.Contains("人工放行") || statusVal.Equals("完成", StringComparison.OrdinalIgnoreCase) ||
				     statusVal.Equals("Done", StringComparison.OrdinalIgnoreCase) || statusVal.Equals("OK", StringComparison.OrdinalIgnoreCase));

				// 同时检查「成品链接回填列」：若已有回填链接（http/https），直接跳过不再重复抓取
				var existingOutUrls = colOut >= 0 && colOut < cells.Length ? ExtractCellUrls(cells[colOut]) : new List<string>();
				if (statusDone || existingOutUrls.Count > 0)
				{
					continue;
				}

				var sourceUrls = colSource < cells.Length ? ExtractCellUrls(cells[colSource]) : new List<string>();
				if (sourceUrls.Count == 0) continue;

				string scriptText = colScript >= 0 && colScript < cells.Length ? ExtractCellText(cells[colScript]) : string.Empty;
				string taskSeq = colSeq >= 0 && colSeq < cells.Length ? ExtractCellText(cells[colSeq]) : string.Empty;
				string taskName = colTask >= 0 && colTask < cells.Length ? ExtractCellText(cells[colTask]) : string.Empty;
				if (string.IsNullOrWhiteSpace(taskName))
				{
					taskName = !string.IsNullOrWhiteSpace(taskSeq) ? taskSeq : $"第{r + 1}行任务";
				}

				seq++;
				result.Add(new SmartComposeTaskItem
				{
					Index = seq,
					SheetRowNumber = r + 1,
					TaskSeq = taskSeq.Trim(),
					TaskName = taskName.Trim(),
					StandardScript = scriptText.Trim(),
					SourceUrls = sourceUrls,
					Status = "待处理"
				});
			}
			return result;
		}

		private static string ExtractCellText(object cellObj)
		{
			if (cellObj is Dictionary<string, object> dict)
			{
				return GetString(dict, "formattedValue").Trim();
			}
			return string.Empty;
		}

		private static List<string> ExtractCellUrls(object cellObj)
		{
			var urls = new List<string>();
			if (!(cellObj is Dictionary<string, object> dict)) return urls;

			void AddIfValid(string u)
			{
				if (string.IsNullOrWhiteSpace(u)) return;
				string clean = u.Trim().TrimEnd(')', ']', ',', ';');
				if ((clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
				    !urls.Contains(clean, StringComparer.OrdinalIgnoreCase))
				{
					urls.Add(clean);
				}
			}

			// 1. 单元格级超链接
			AddIfValid(GetString(dict, "hyperlink"));

			// 2. Google Sheets 巧克力胶囊 (chipRuns -> chip -> richLinkProperties -> uri)
			if (dict.TryGetValue("chipRuns", out object chipsObj) && chipsObj is object[] chipRuns)
			{
				foreach (var cr in chipRuns)
				{
					if (cr is Dictionary<string, object> crd &&
					    crd.TryGetValue("chip", out object chipObj) && chipObj is Dictionary<string, object> chip &&
					    chip.TryGetValue("richLinkProperties", out object rlpObj) && rlpObj is Dictionary<string, object> rlp)
					{
						AddIfValid(GetString(rlp, "uri"));
					}
				}
			}

			// 3. 单元格局部富文本超链接 (textFormatRuns -> format -> link -> uri)
			if (dict.TryGetValue("textFormatRuns", out object runsObj) && runsObj is object[] runs)
			{
				foreach (var run in runs)
				{
					if (run is Dictionary<string, object> rd &&
					    rd.TryGetValue("format", out object fmtObj) && fmtObj is Dictionary<string, object> fmt &&
					    fmt.TryGetValue("link", out object linkObj) && linkObj is Dictionary<string, object> link)
					{
						AddIfValid(GetString(link, "uri"));
					}
				}
			}

			// 4. 单元格纯文本里的裸 URL（支持换行放多个链接）
			string text = GetString(dict, "formattedValue");
			if (!string.IsNullOrWhiteSpace(text))
			{
				foreach (Match m in UrlExtractRegex.Matches(text))
				{
					AddIfValid(m.Value);
				}
			}
			return urls;
		}

		public static async Task UpdateSheetRowAsync(
			SmartComposeSettings settings,
			int rowNumber,
			string outputDriveUrl,
			string statusText,
			CancellationToken ct)
		{
			if (rowNumber <= 1) return;
			string spreadsheetId = ExtractSpreadsheetId(settings.SheetUrl);
			if (string.IsNullOrWhiteSpace(spreadsheetId)) return;

			string token = await EnsureValidAccessTokenAsync(settings, ct).ConfigureAwait(false);
			string tabName = string.IsNullOrWhiteSpace(settings.SheetTabName) ? "Sheet1" : settings.SheetTabName;
			string escapedTab = tabName.Replace("'", "''");

			int colOut = ParseColumnLetterToIndex(settings.ColOutputLink);
			int colStatus = ParseColumnLetterToIndex(settings.ColStatus);

			using var http = CreateHttpClient(settings.ProxyUrl, 30);

			async Task WriteCellAsync(int colIdx, string value)
			{
				if (colIdx < 0 || value == null) return;
				string letter = ColumnIndexToLetter(colIdx);
				string range = Uri.EscapeDataString($"'{escapedTab}'!{letter}{rowNumber}");
				string payload = Json.Serialize(new Dictionary<string, object>
				{
					["values"] = new object[] { new object[] { value } }
				});
				using var req = new HttpRequestMessage(HttpMethod.Put,
					$"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}/values/{range}?valueInputOption=USER_ENTERED");
				req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
				req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
				using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
				if (!resp.IsSuccessStatusCode)
				{
					string err = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
					throw new InvalidOperationException($"回填表格 {letter}{rowNumber} 失败 ({(int)resp.StatusCode}): {err}");
				}
			}

			if (!string.IsNullOrWhiteSpace(outputDriveUrl) && colOut >= 0)
			{
				string safeOut = EscapeSpreadsheetFormula(outputDriveUrl);
				await WriteCellAsync(colOut, safeOut).ConfigureAwait(false);
			}
			if (!string.IsNullOrWhiteSpace(statusText) && colStatus >= 0)
			{
				await WriteCellAsync(colStatus, EscapeSpreadsheetFormula(statusText)).ConfigureAwait(false);
			}
		}

		private static string EscapeSpreadsheetFormula(string value)
		{
			if (string.IsNullOrEmpty(value)) return value;
			char c = value[0];
			if (c == '=' || c == '+' || c == '-' || c == '@' || c == '\t' || c == '\r')
			{
				return "'" + value;
			}
			return value;
		}

		/// <summary>
		/// 列出 Google Drive 文件夹里的所有视频文件（按自然文件名排序）
		/// </summary>
		public static async Task<List<DriveVideoFileInfo>> ListFolderVideosAsync(
			SmartComposeSettings settings,
			string folderId,
			CancellationToken ct)
		{
			string token = await EnsureValidAccessTokenAsync(settings, ct).ConfigureAwait(false);
			using var http = CreateHttpClient(settings.ProxyUrl, 30);

			string query = $"'{folderId}' in parents and trashed = false";
			string url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name,mimeType,size,md5Checksum,modifiedTime)&pageSize=200&supportsAllDrives=true&includeItemsFromAllDrives=true";

			using var req = new HttpRequestMessage(HttpMethod.Get, url);
			req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
			string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!resp.IsSuccessStatusCode)
				throw new InvalidOperationException($"读取谷歌云盘文件夹失败 ({(int)resp.StatusCode}): {body}");

			var list = new List<DriveVideoFileInfo>();
			var dict = Json.DeserializeObject(body) as Dictionary<string, object>;
			if (dict != null && dict.TryGetValue("files", out object filesObj) && filesObj is object[] arr)
			{
				foreach (var item in arr)
				{
					if (!(item is Dictionary<string, object> fd)) continue;
					string name = GetString(fd, "name");
					string mime = GetString(fd, "mimeType");
					string ext = Path.GetExtension(name ?? string.Empty).ToLowerInvariant();
					bool isVideo = mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
					               ext == ".mp4" || ext == ".mov" || ext == ".mkv" || ext == ".webm" || ext == ".avi" || ext == ".m4v";
					if (!isVideo) continue;
					long.TryParse(GetString(fd, "size"), out long sz);
					list.Add(new DriveVideoFileInfo
					{
						Id = GetString(fd, "id"),
						Name = name,
						MimeType = mime,
						Size = sz,
						Md5Checksum = GetString(fd, "md5Checksum"),
						ModifiedTime = GetString(fd, "modifiedTime")
					});
				}
			}

			list.Sort((a, b) => CompareNatural(a.Name, b.Name));
			return list;
		}

		internal static int CompareNatural(string a, string b)
		{
			a = a ?? string.Empty;
			b = b ?? string.Empty;
			int ia = 0, ib = 0;
			while (ia < a.Length && ib < b.Length)
			{
				if (char.IsDigit(a[ia]) && char.IsDigit(b[ib]))
				{
					long na = 0, nb = 0;
					while (ia < a.Length && char.IsDigit(a[ia])) { na = na * 10 + (a[ia] - '0'); ia++; }
					while (ib < b.Length && char.IsDigit(b[ib])) { nb = nb * 10 + (b[ib] - '0'); ib++; }
					if (na != nb) return na.CompareTo(nb);
				}
				else
				{
					int cmp = char.ToUpperInvariant(a[ia]).CompareTo(char.ToUpperInvariant(b[ib]));
					if (cmp != 0) return cmp;
					ia++;
					ib++;
				}
			}
			return a.Length.CompareTo(b.Length);
		}

		// ===== 上传/下载带宽协调（防止后台上传占满上行导致下载 ACK 回包被堵死） =====
		private static int _activeDownloads;
		private static int _activeUploads;
		private static double _uploadBpsEstimate = 150 * 1024; // 未让行时实测的上行速度（自动学习）

		public static bool IsDownloadRunning => Volatile.Read(ref _activeDownloads) > 0;
		public static bool IsUploadRunning => Volatile.Read(ref _activeUploads) > 0;

		public static async Task DownloadDriveFileAsync(
			SmartComposeSettings settings,
			string fileId,
			string destFilePath,
			Action<int> progressCallback,
			CancellationToken ct)
		{
			Interlocked.Increment(ref _activeDownloads);
			try
			{
				await DownloadDriveFileCoreAsync(settings, fileId, destFilePath, progressCallback, ct).ConfigureAwait(false);
			}
			finally
			{
				Interlocked.Decrement(ref _activeDownloads);
			}
		}

		private static async Task DownloadDriveFileCoreAsync(
			SmartComposeSettings settings,
			string fileId,
			string destFilePath,
			Action<int> progressCallback,
			CancellationToken ct)
		{
			string dir = Path.GetDirectoryName(destFilePath);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

			Exception lastErr = null;
			for (int attempt = 1; attempt <= 3; attempt++)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					string token = await EnsureValidAccessTokenAsync(settings, ct).ConfigureAwait(false);
					using var http = CreateHttpClient(settings.ProxyUrl, 60);

					string url = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media&supportsAllDrives=true";
					using var req = new HttpRequestMessage(HttpMethod.Get, url);
					req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

					using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
					if (!resp.IsSuccessStatusCode)
					{
						string err = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
						throw new InvalidOperationException($"下载云盘视频失败 ({(int)resp.StatusCode}): {err}");
					}

					long? total = resp.Content.Headers.ContentLength;
					string tempPart = destFilePath + ".part";
					using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
					using (var dst = new FileStream(tempPart, FileMode.Create, FileAccess.Write, FileShare.Read))
					{
						byte[] buf = new byte[64 * 1024];
						long copied = 0;
						while (true)
						{
							ct.ThrowIfCancellationRequested();
							// 30 秒单次读包停滞看门狗：彻底杜绝 .NET 4.8 网络抖动导致 ReadAsync 无限期假死！
							var readTask = src.ReadAsync(buf, 0, buf.Length, ct);
							var timeoutTask = Task.Delay(30000, ct);
							var finished = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
							if (finished != readTask)
							{
								try { resp.Dispose(); } catch { }
								throw new TimeoutException("下载云盘片段超过 30 秒无数据响应（连接已自动重置并重试）。");
							}

							int read = await readTask.ConfigureAwait(false);
							if (read <= 0) break;

							await dst.WriteAsync(buf, 0, read, ct).ConfigureAwait(false);
							copied += read;
							if (total.HasValue && total.Value > 0)
							{
								progressCallback?.Invoke((int)Math.Min(100, copied * 100 / total.Value));
							}
						}
					}

					if (File.Exists(destFilePath)) { try { File.Delete(destFilePath); } catch { } }
					File.Move(tempPart, destFilePath);
					return;
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					lastErr = ex;
					try { if (File.Exists(destFilePath + ".part")) File.Delete(destFilePath + ".part"); } catch { }
					if (attempt < 3)
					{
						await Task.Delay(1500 * attempt, ct).ConfigureAwait(false);
					}
				}
			}

			throw lastErr ?? new InvalidOperationException("下载云盘视频片段失败（重试 3 次仍未成功）。");
		}

		private sealed class ProgressStreamContent : HttpContent
		{
			private readonly string _filePath;
			private readonly long _totalLength;
			private readonly Action<int, double> _progress;
			private readonly CancellationToken _ct;

			public ProgressStreamContent(string filePath, long totalLength, Action<int, double> progress, CancellationToken ct)
			{
				_filePath = filePath;
				_totalLength = totalLength;
				_progress = progress;
				_ct = ct;
				Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
				Headers.ContentLength = totalLength;
			}

			protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
			{
				const int bufSize = 128 * 1024;
				const int yieldChunk = 16 * 1024;
				byte[] buffer = new byte[bufSize];
				long sent = 0;
				var sw = Stopwatch.StartNew();
				long lastReportMs = 0;

				// 让行节流窗口
				bool wasYielding = false;
				var yieldSw = new Stopwatch();
				long yieldBytes = 0;

				// 未让行时实测上行速度窗口
				var measureSw = Stopwatch.StartNew();
				long measureBytes = 0;

				Interlocked.Increment(ref _activeUploads);
				try
				{
					using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufSize, useAsync: true);
					while (true)
					{
						bool yieldToDownload = IsDownloadRunning;
						int chunk = yieldToDownload ? yieldChunk : buffer.Length;
						int read = await fs.ReadAsync(buffer, 0, chunk, _ct).ConfigureAwait(false);
						if (read <= 0) break;

						await stream.WriteAsync(buffer, 0, read, _ct).ConfigureAwait(false);
						sent += read;

						if (yieldToDownload)
						{
							// 有素材在下载时：上传自动降速到实测上行的 45%，给下载的 ACK 回包留出通道，避免下载被拖到龟速
							if (!wasYielding)
							{
								wasYielding = true;
								yieldBytes = 0;
								yieldSw.Restart();
							}
							yieldBytes += read;
							double limitBps = Math.Max(20 * 1024, Volatile.Read(ref _uploadBpsEstimate) * 0.45);
							double expectedMs = yieldBytes * 1000.0 / limitBps;
							double actualMs = yieldSw.ElapsedMilliseconds;
							if (expectedMs > actualMs)
							{
								await Task.Delay((int)Math.Min(1500, expectedMs - actualMs), _ct).ConfigureAwait(false);
							}
						}
						else
						{
							if (wasYielding)
							{
								wasYielding = false;
								measureBytes = 0;
								measureSw.Restart();
							}
							measureBytes += read;
							if (measureSw.ElapsedMilliseconds >= 3000)
							{
								double bps = measureBytes * 1000.0 / Math.Max(1, measureSw.ElapsedMilliseconds);
								double old = Volatile.Read(ref _uploadBpsEstimate);
								Volatile.Write(ref _uploadBpsEstimate, Math.Max(16 * 1024, old * 0.6 + bps * 0.4));
								measureBytes = 0;
								measureSw.Restart();
							}
						}

						long nowMs = sw.ElapsedMilliseconds;
						if (nowMs - lastReportMs >= 350 || sent >= _totalLength)
						{
							lastReportMs = nowMs;
							int pct = (int)Math.Min(99, sent * 100 / Math.Max(1, _totalLength));
							double sec = Math.Max(0.1, nowMs / 1000.0);
							double mbPerSec = (sent / (1024.0 * 1024.0)) / sec;
							_progress?.Invoke(pct, mbPerSec);
						}
					}
				}
				finally
				{
					Interlocked.Decrement(ref _activeUploads);
				}
			}

			protected override bool TryComputeLength(out long length)
			{
				length = _totalLength;
				return true;
			}
		}

		/// <summary>
		/// 上传成品 MP4 到指定的 Gmail 谷歌云盘文件夹（连续流式直传，零 308 分块停顿），自动开放只读分享权限并返回分享链接
		/// </summary>
		public static Task<string> UploadFileAndShareAsync(
			SmartComposeSettings settings,
			string localFilePath,
			string targetFolderUrlOrId,
			Action<int> progressCallback,
			CancellationToken ct)
		{
			return UploadFileAndShareAsync(settings, localFilePath, targetFolderUrlOrId,
				progressCallback != null ? (Action<int, double>)((p, _) => progressCallback(p)) : null, ct);
		}

		public static async Task<string> UploadFileAndShareAsync(
			SmartComposeSettings settings,
			string localFilePath,
			string targetFolderUrlOrId,
			Action<int, double> progressWithSpeedCallback,
			CancellationToken ct)
		{
			if (!File.Exists(localFilePath))
				throw new FileNotFoundException("找不到待上传的成品文件。", localFilePath);

			string folderId = ExtractDriveId(targetFolderUrlOrId, out _);
			long fileLength = new FileInfo(localFilePath).Length;
			var meta = new Dictionary<string, object>
			{
				["name"] = Path.GetFileName(localFilePath)
			};
			if (!string.IsNullOrWhiteSpace(folderId))
			{
				meta["parents"] = new[] { folderId };
			}

			Exception lastErr = null;
			for (int attempt = 1; attempt <= 3; attempt++)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					string token = await EnsureValidAccessTokenAsync(settings, ct).ConfigureAwait(false);
					using var http = CreateHttpClient(settings.ProxyUrl, 1800, allowAutoRedirect: false);

					// 1. 发起可恢复上传会话 (Resumable Upload)
					Uri uploadUri;
					using (var initReq = new HttpRequestMessage(HttpMethod.Post,
						"https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&supportsAllDrives=true&fields=id,webViewLink"))
					{
						initReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
						initReq.Headers.Add("X-Upload-Content-Type", "video/mp4");
						initReq.Headers.Add("X-Upload-Content-Length", fileLength.ToString());
						initReq.Content = new StringContent(Json.Serialize(meta), Encoding.UTF8, "application/json");

						using var initResp = await http.SendAsync(initReq, ct).ConfigureAwait(false);
						if (!initResp.IsSuccessStatusCode)
						{
							string err = await initResp.Content.ReadAsStringAsync().ConfigureAwait(false);
							int code = (int)initResp.StatusCode;
							var ex = new InvalidOperationException($"初始化云盘上传失败 ({code}): {err}");
							if (code == 401 || code == 403 || code == 404) throw ex;
							throw ex;
						}
						uploadUri = initResp.Headers.Location;
					}

					if (uploadUri == null)
						throw new InvalidOperationException("Google Drive 未返回上传会话地址。");

					// 2. 单次连续 TCP 流式直传整个 MP4（零 308 停顿，实时回报进度与 MB/s 网速）
					string uploadedFileId = string.Empty;
					string webViewLink = string.Empty;
					using (var putReq = new HttpRequestMessage(HttpMethod.Put, uploadUri))
					{
						putReq.Content = new ProgressStreamContent(localFilePath, fileLength, progressWithSpeedCallback, ct);
						using var putResp = await http.SendAsync(putReq, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
						int status = (int)putResp.StatusCode;
						string respBody = await putResp.Content.ReadAsStringAsync().ConfigureAwait(false);
						if (status == 200 || status == 201)
						{
							var respDict = Json.DeserializeObject(respBody) as Dictionary<string, object>;
							uploadedFileId = GetString(respDict, "id");
							webViewLink = GetString(respDict, "webViewLink");
						}
						else
						{
							throw new InvalidOperationException($"上传成品到云盘失败 ({status}): {respBody}");
						}
					}

					if (string.IsNullOrWhiteSpace(uploadedFileId))
						throw new InvalidOperationException("云盘上传未返回文件 ID。");

					// 3. 设为“知道链接的任何人可查看”
					try
					{
						using var permReq = new HttpRequestMessage(HttpMethod.Post,
							$"https://www.googleapis.com/drive/v3/files/{uploadedFileId}/permissions?supportsAllDrives=true");
						permReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
						permReq.Content = new StringContent(Json.Serialize(new Dictionary<string, object>
						{
							["role"] = "reader",
							["type"] = "anyone"
						}), Encoding.UTF8, "application/json");
						using var permResp = await http.SendAsync(permReq, ct).ConfigureAwait(false);
					}
					catch { }

					progressWithSpeedCallback?.Invoke(100, 0);
					return !string.IsNullOrWhiteSpace(webViewLink)
						? webViewLink
						: $"https://drive.google.com/file/d/{uploadedFileId}/view?usp=sharing";
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch (Exception ex)
				{
					lastErr = ex;
					string msg = ex.Message ?? string.Empty;
					// 权限/空间/404 属于确定性错误，不再无谓重试；网络抖动/5xx/超时则自动退避重试最多 3 次
					if (msg.Contains("(401)") || msg.Contains("(403)") || msg.Contains("(404)") || msg.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						throw;
					}
					if (attempt < 3)
					{
						await Task.Delay(3000 * attempt, ct).ConfigureAwait(false);
					}
				}
			}

			throw lastErr ?? new InvalidOperationException("上传成品到云盘失败（自动重试 3 次仍未成功）。");
		}

		private static string GetString(Dictionary<string, object> dict, string key)
		{
			if (dict != null && dict.TryGetValue(key, out object v) && v != null)
				return v.ToString();
			return string.Empty;
		}

		private static int GetInt(Dictionary<string, object> dict, string key, int fallback)
		{
			if (dict != null && dict.TryGetValue(key, out object v) && v != null && int.TryParse(v.ToString(), out int parsed))
				return parsed;
			return fallback;
		}
	}
}
