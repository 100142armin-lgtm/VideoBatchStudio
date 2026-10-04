using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace VideoBatchMerger;

/// <summary>
/// 团队自动检查更新与版本升级管理系统
/// 自动从 GitHub 仓库获取最新版本发布信息，并在发现新版本时提醒团队成员
/// </summary>
internal static class UpdateChecker
{
	public const string RepoUrl = "https://github.com/100142armin-lgtm/VideoBatchStudio";
	public const string RawVersionUrl = "https://raw.githubusercontent.com/100142armin-lgtm/VideoBatchStudio/main/version.json";
	public const string GitHubReleaseApiUrl = "https://api.github.com/repos/100142armin-lgtm/VideoBatchStudio/releases/latest";

	private static bool _isChecking = false;
	private static string _lastNotifiedVersion = null;

	/// <summary>
	/// 异步检查更新
	/// </summary>
	/// <param name="owner">父窗口</param>
	/// <param name="currentVersion">当前版本号，如 "8.1.0"</param>
	/// <param name="isManual">是否为用户手动点击“检查更新”</param>
	public static void CheckForUpdatesAsync(Form owner, string currentVersion, bool isManual)
	{
		if (_isChecking) return;
		_isChecking = true;

		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				// Enable TLS 1.2 and TLS 1.3
				try
				{
					ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
				}
				catch { }

				string json = null;

				// 1. Try fetching from raw version.json (Fast, no API rate limit)
				try
				{
					HttpWebRequest req = (HttpWebRequest)WebRequest.Create(RawVersionUrl);
					req.Method = "GET";
					req.UserAgent = "VideoBatchStudio-Updater/" + currentVersion;
					req.Timeout = 6000;
					req.ReadWriteTimeout = 6000;

					using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
					using (Stream stream = resp.GetResponseStream())
					using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
					{
						json = reader.ReadToEnd();
					}
				}
				catch
				{
					// Fallback to GitHub Release API
					try
					{
						HttpWebRequest reqApi = (HttpWebRequest)WebRequest.Create(GitHubReleaseApiUrl);
						reqApi.Method = "GET";
						reqApi.UserAgent = "VideoBatchStudio-Updater/" + currentVersion;
						reqApi.Timeout = 6000;
						reqApi.ReadWriteTimeout = 6000;

						using (HttpWebResponse respApi = (HttpWebResponse)reqApi.GetResponse())
						using (Stream streamApi = respApi.GetResponseStream())
						using (StreamReader readerApi = new StreamReader(streamApi, Encoding.UTF8))
						{
							json = readerApi.ReadToEnd();
						}
					}
					catch { }
				}

				if (string.IsNullOrEmpty(json))
				{
					if (isManual && owner != null && !owner.IsDisposed)
					{
						owner.BeginInvoke((MethodInvoker)delegate
						{
							MessageBox.Show(owner, "检查更新失败，未能连接到更新服务器。\n\n请检查网络连接或直接访问 GitHub 项目主页：\n" + RepoUrl, "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						});
					}
					return;
				}

				// Parse version info from json
				string remoteVersion = ExtractJsonString(json, "version");
				if (string.IsNullOrEmpty(remoteVersion))
				{
					remoteVersion = ExtractJsonString(json, "tag_name");
				}

				if (string.IsNullOrEmpty(remoteVersion))
				{
					if (isManual && owner != null && !owner.IsDisposed)
					{
						owner.BeginInvoke((MethodInvoker)delegate
						{
							MessageBox.Show(owner, "未能获取到有效的最新版本信息。\n\n项目主页：\n" + RepoUrl, "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
						});
					}
					return;
				}

				remoteVersion = remoteVersion.TrimStart('v', 'V');
				string releaseTitle = ExtractJsonString(json, "title");
				if (string.IsNullOrEmpty(releaseTitle)) releaseTitle = ExtractJsonString(json, "name");

				string releaseDate = ExtractJsonString(json, "releaseDate");
				if (string.IsNullOrEmpty(releaseDate)) releaseDate = ExtractJsonString(json, "published_at");
				if (!string.IsNullOrEmpty(releaseDate) && releaseDate.Length >= 10)
				{
					releaseDate = releaseDate.Substring(0, 10);
				}

				string downloadUrl = ExtractJsonString(json, "downloadUrl");
				if (string.IsNullOrEmpty(downloadUrl)) downloadUrl = ExtractJsonString(json, "html_url");
				if (string.IsNullOrEmpty(downloadUrl)) downloadUrl = RepoUrl + "/releases/latest";
				if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var validatedUri) ||
				    (validatedUri.Scheme != Uri.UriSchemeHttp && validatedUri.Scheme != Uri.UriSchemeHttps))
				{
					downloadUrl = RepoUrl + "/releases/latest";
				}

				List<string> changelog = ExtractJsonArray(json, "changelog");
				if (changelog.Count == 0)
				{
					string body = ExtractJsonString(json, "body");
					if (!string.IsNullOrEmpty(body))
					{
						string[] lines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
						foreach (string line in lines)
						{
							string tr = line.Trim();
							if (!string.IsNullOrEmpty(tr) && (tr.StartsWith("-") || tr.StartsWith("*") || tr.StartsWith("•")))
							{
								changelog.Add(tr.TrimStart('-', '*', '•', ' '));
							}
						}
					}
				}

				bool isNewer = IsNewerVersion(remoteVersion, currentVersion);

				if (isNewer)
				{
					// If automatic check, avoid popping up repeatedly for the same version in one session
					if (!isManual && string.Equals(_lastNotifiedVersion, remoteVersion, StringComparison.OrdinalIgnoreCase))
					{
						return;
					}
					_lastNotifiedVersion = remoteVersion;

					if (owner != null && !owner.IsDisposed)
					{
						owner.BeginInvoke((MethodInvoker)delegate
						{
							if (owner.IsDisposed) return;
							using (var form = new UpdateNotificationForm(currentVersion, remoteVersion, releaseTitle, releaseDate, changelog, downloadUrl))
							{
								form.ShowDialog(owner);
							}
						});
					}
				}
				else if (isManual && owner != null && !owner.IsDisposed)
				{
					owner.BeginInvoke((MethodInvoker)delegate
					{
						MessageBox.Show(owner, $"当前已是最新版本 (V{currentVersion})，无需更新！\n\n已是团队最新稳定版。", "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
					});
				}
			}
			catch (Exception ex)
			{
				if (isManual && owner != null && !owner.IsDisposed)
				{
					owner.BeginInvoke((MethodInvoker)delegate
					{
						MessageBox.Show(owner, "检查更新时发生异常: " + ex.Message + "\n\n项目主页：\n" + RepoUrl, "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					});
				}
			}
			finally
			{
				_isChecking = false;
			}
		});
	}

	/// <summary>
	/// 比较版本号：若 remote > local 返回 true
	/// </summary>
	public static bool IsNewerVersion(string remoteVer, string localVer)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(remoteVer) || string.IsNullOrWhiteSpace(localVer))
				return false;

			string[] rParts = remoteVer.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
			string[] lParts = localVer.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries);

			int maxLen = Math.Max(rParts.Length, lParts.Length);
			for (int i = 0; i < maxLen; i++)
			{
				int rVal = 0;
				int lVal = 0;

				if (i < rParts.Length)
				{
					int.TryParse(Regex.Match(rParts[i], @"\d+").Value, out rVal);
				}
				if (i < lParts.Length)
				{
					int.TryParse(Regex.Match(lParts[i], @"\d+").Value, out lVal);
				}

				if (rVal > lVal) return true;
				if (rVal < lVal) return false;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private static string ExtractJsonString(string json, string key)
	{
		try
		{
			var match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]*)\"");
			if (match.Success)
			{
				return match.Groups[1].Value.Replace("\\n", "\n").Replace("\\r", "").Replace("\\\"", "\"");
			}
		}
		catch { }
		return null;
	}

	private static List<string> ExtractJsonArray(string json, string key)
	{
		List<string> result = new List<string>();
		try
		{
			var match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\\[([^\\]]*)\\]", RegexOptions.Singleline);
			if (match.Success)
			{
				string arrayContent = match.Groups[1].Value;
				var itemMatches = Regex.Matches(arrayContent, "\"([^\"]*)\"");
				foreach (Match im in itemMatches)
				{
					string val = im.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\"").Trim();
					if (!string.IsNullOrEmpty(val))
					{
						result.Add(val);
					}
				}
			}
		}
		catch { }
		return result;
	}
}
