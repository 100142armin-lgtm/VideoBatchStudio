using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoBatchMerger;

/// <summary>
/// 极简全自动静默热更新弹窗
/// 仅保留【稍后提醒】与【立即更新】两个核心按钮，用户确认后全自动流式下载、解压覆盖并重启
/// </summary>
internal sealed class UpdateNotificationForm : Form
{
	private readonly string _currentVer;
	private readonly string _newVer;
	private readonly string _fastPortableZipUrl;
	private readonly string _portableZipUrl;

	private Label _lblStatus;
	private ProgressBar _pbProgress;
	private Button _btnUpdateNow;
	private Button _btnCancel;
	private bool _isUpdating = false;
	private CancellationTokenSource _cts;

	public UpdateNotificationForm(
		string currentVer,
		string newVer,
		string releaseTitle,
		string releaseDate,
		IEnumerable<string> changelog,
		string downloadUrl,
		string fastPortableZipUrl = null,
		string portableZipUrl = null)
	{
		_currentVer = currentVer;
		_newVer = newVer;
		_fastPortableZipUrl = string.IsNullOrWhiteSpace(fastPortableZipUrl)
			? $"https://ghfast.top/https://github.com/100142armin-lgtm/VideoBatchStudio/releases/download/v{newVer}/VideoBatchStudio_Portable.zip"
			: IsApprovedUpdateUrl(fastPortableZipUrl) ? fastPortableZipUrl : $"https://ghfast.top/https://github.com/100142armin-lgtm/VideoBatchStudio/releases/download/v{newVer}/VideoBatchStudio_Portable.zip";
		_portableZipUrl = string.IsNullOrWhiteSpace(portableZipUrl)
			? $"https://github.com/100142armin-lgtm/VideoBatchStudio/releases/download/v{newVer}/VideoBatchStudio_Portable.zip"
			: IsApprovedUpdateUrl(portableZipUrl) ? portableZipUrl : $"https://github.com/100142armin-lgtm/VideoBatchStudio/releases/download/v{newVer}/VideoBatchStudio_Portable.zip";

		Text = $"🎉 发现新版本 V{newVer} · 视频批处理工具升级提示";
		Width = 680;
		Height = 490;
		MinimumSize = new Size(600, 420);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(15, 20, 28);
		ForeColor = Color.White;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		KeyPreview = true;

		// 1. 顶部状态横幅
		Panel headerPanel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 68,
			BackColor = Color.FromArgb(24, 32, 46),
			Padding = new Padding(20, 12, 20, 8)
		};

		Label bannerTitle = new Label
		{
			Text = $"🚀 发现新版本 V{newVer} 已经发布！",
			Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
			ForeColor = Color.FromArgb(56, 189, 248),
			AutoSize = true,
			Location = new Point(18, 12)
		};
		headerPanel.Controls.Add(bannerTitle);

		string dateStr = string.IsNullOrEmpty(releaseDate) ? DateTime.Now.ToString("yyyy-MM-dd") : releaseDate;
		Label bannerSub = new Label
		{
			Text = $"当前版本: V{currentVer}  ➔  最新版本: V{newVer} ({dateStr}) · 点击立即更新将全自动静默升级",
			Font = new Font("Microsoft YaHei UI", 8.75f),
			ForeColor = Color.FromArgb(148, 163, 184),
			AutoSize = true,
			Location = new Point(20, 38)
		};
		headerPanel.Controls.Add(bannerSub);
		Controls.Add(headerPanel);

		// 2. 底部极简操作栏（仅 2 个按钮 + 进度条）
		Panel bottomBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 84,
			BackColor = Color.FromArgb(20, 26, 38),
			Padding = new Padding(20, 8, 20, 12)
		};

		_lblStatus = new Label
		{
			Text = "💡 点击【立即更新】，软件将自动静默下载、覆盖旧版并重启生效。",
			Font = new Font("Microsoft YaHei UI", 9f),
			ForeColor = Color.FromArgb(148, 163, 184),
			Location = new Point(20, 10),
			Size = new Size(bottomBar.ClientSize.Width - 40, 18),
			Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
			AutoEllipsis = true
		};
		bottomBar.Controls.Add(_lblStatus);

		_pbProgress = new ProgressBar
		{
			Location = new Point(20, 30),
			Size = new Size(bottomBar.ClientSize.Width - 40, 8),
			Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
			Minimum = 0,
			Maximum = 100,
			Value = 0,
			Visible = false
		};
		bottomBar.Controls.Add(_pbProgress);

		_btnCancel = new Button
		{
			Text = "稍后提醒",
			Size = new Size(95, 36),
			Location = new Point(20, 42),
			Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(30, 41, 59),
			ForeColor = Color.FromArgb(148, 163, 184),
			Font = new Font("Microsoft YaHei UI", 9f),
			Cursor = Cursors.Hand
		};
		_btnCancel.FlatAppearance.BorderSize = 0;
		_btnCancel.Click += delegate { Close(); };
		bottomBar.Controls.Add(_btnCancel);

		_btnUpdateNow = new Button
		{
			Text = "🚀 立即更新",
			Size = new Size(160, 36),
			Location = new Point(bottomBar.ClientSize.Width - 180, 42),
			Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(16, 185, 129),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
			Cursor = Cursors.Hand
		};
		_btnUpdateNow.FlatAppearance.BorderSize = 0;
		_btnUpdateNow.Click += async (s, e) => await StartAutoUpdateAsync();
		bottomBar.Controls.Add(_btnUpdateNow);

		Controls.Add(bottomBar);

		// 3. 中间内容区：更新日志
		Panel contentPanel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(15, 20, 28),
			Padding = new Padding(20, 14, 20, 8)
		};

		Label lblChangeTitle = new Label
		{
			Text = string.IsNullOrEmpty(releaseTitle) ? "💡 本次更新内容：" : $"💡 {releaseTitle}：",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(226, 232, 240),
			AutoSize = true,
			Location = new Point(20, 14)
		};
		contentPanel.Controls.Add(lblChangeTitle);

		TextBox tbChangelog = new TextBox
		{
			Location = new Point(20, 38),
			Size = new Size(contentPanel.ClientSize.Width - 40, contentPanel.ClientSize.Height - 48),
			Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
			BackColor = Color.FromArgb(20, 26, 38),
			ForeColor = Color.FromArgb(226, 232, 240),
			Font = new Font("Microsoft YaHei UI", 9.25f),
			BorderStyle = BorderStyle.FixedSingle,
			ReadOnly = true,
			Multiline = true,
			ScrollBars = ScrollBars.Vertical
		};

		StringBuilder sb = new StringBuilder();
		if (changelog != null && changelog.Any())
		{
			foreach (var item in changelog)
			{
				sb.AppendLine("• " + item);
				sb.AppendLine();
			}
		}
		else
		{
			sb.AppendLine("• 包含多项核心性能优化、功能增强与稳定性提升。");
			sb.AppendLine();
			sb.AppendLine("• 建议团队全体成员及时升级体验新版功能！");
		}
		tbChangelog.Text = sb.ToString();

		contentPanel.Controls.Add(tbChangelog);
		Controls.Add(contentPanel);

		contentPanel.BringToFront();
		FormClosing += OnFormClosing;
		KeyDown += (s, e) => { if (!_isUpdating && e.KeyCode == Keys.Escape) Close(); };
	}

	private void OnFormClosing(object sender, FormClosingEventArgs e)
	{
		if (_isUpdating)
		{
			var dr = MessageBox.Show(this, "新版本正在静默下载中，确定要取消本次升级吗？", "取消更新", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
			if (dr == DialogResult.No)
			{
				e.Cancel = true;
				return;
			}
			try
			{
				_cts?.Cancel();
			}
			catch { }
		}
	}

	/// <summary>
	/// 用户确认后，全自动静默下载、解压覆盖并重启
	/// </summary>
	private async Task StartAutoUpdateAsync()
	{
		if (_isUpdating) return;
		_isUpdating = true;
		_cts = new CancellationTokenSource();

		_btnUpdateNow.Enabled = false;
		_btnUpdateNow.Text = "⏳ 正在更新...";
		_btnCancel.Enabled = false;
		_pbProgress.Visible = true;
		_pbProgress.Value = 0;
		_lblStatus.ForeColor = Color.FromArgb(56, 189, 248);
		_lblStatus.Text = "🚀 正在连接高速通道下载更新包...";

		string tempZip = Path.Combine(Path.GetTempPath(), $"vbs_update_{_newVer}_{DateTime.Now.Ticks}.zip");
		string tempExtractDir = Path.Combine(Path.GetTempPath(), $"vbs_extract_{_newVer}_{DateTime.Now.Ticks}");

		try
		{
			// 1. 静默下载更新包
			await DownloadWithFallbackAsync(_fastPortableZipUrl, _portableZipUrl, tempZip, _cts.Token);

			_lblStatus.Text = "📦 下载完成，正在解压并准备自动重启...";
			_pbProgress.Value = 100;

			// 2. 解压并在后台启动替换脚本
			await Task.Run(() =>
			{
				if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
				Directory.CreateDirectory(tempExtractDir);

				ExtractZipSafely(tempZip, tempExtractDir);

				string exeFound = Directory.GetFiles(tempExtractDir, "VideoBatchStudio.exe", SearchOption.AllDirectories).FirstOrDefault();
				if (string.IsNullOrEmpty(exeFound))
				{
					throw new Exception("更新包中未检测到可执行程序 VideoBatchStudio.exe！");
				}

				string sourceFolder = Path.GetDirectoryName(exeFound);
				string currentAppDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');

				ApplyUpdateFilesInPlace(sourceFolder, currentAppDir);
				TryCleanFiles(tempZip, tempExtractDir);

				string targetExe = Path.Combine(currentAppDir, "VideoBatchStudio.exe");
				if (File.Exists(targetExe))
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = targetExe,
						WorkingDirectory = currentAppDir,
						UseShellExecute = true
					});
				}
			});

			_lblStatus.Text = "🎉 升级成功！正在自动重启软件...";
			await Task.Delay(800);

			Application.Exit();
			Environment.Exit(0);
		}
		catch (OperationCanceledException)
		{
			_isUpdating = false;
			_lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
			_lblStatus.Text = "💡 已取消本次升级。";
			_btnUpdateNow.Enabled = true;
			_btnUpdateNow.Text = "🚀 立即更新";
			_btnCancel.Enabled = true;
			_pbProgress.Visible = false;
			TryCleanFiles(tempZip, tempExtractDir);
		}
		catch (Exception ex)
		{
			_isUpdating = false;
			_lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
			_lblStatus.Text = $"❌ 自动升级失败: {ex.Message}";
			_btnUpdateNow.Enabled = true;
			_btnUpdateNow.Text = "🔄 重试更新";
			_btnCancel.Enabled = true;
			TryCleanFiles(tempZip, tempExtractDir);
		}
	}

	private static bool IsApprovedUpdateUrl(string value)
	{
		if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
		string host = uri.Host.TrimEnd('.').ToLowerInvariant();
		return host == "github.com" || host == "ghfast.top" || host == "ghproxy.net" || host == "objects.githubusercontent.com";
	}

	private static void ApplyUpdateFilesInPlace(string sourceFolder, string targetFolder)
	{
		string srcRoot = Path.GetFullPath(sourceFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string dstRoot = Path.GetFullPath(targetFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		foreach (string srcFile in Directory.GetFiles(srcRoot, "*", SearchOption.AllDirectories))
		{
			string relPath = srcFile.Substring(srcRoot.Length);
			if (relPath.StartsWith("auditions" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
				relPath.StartsWith("auditions/", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string fileName = Path.GetFileName(relPath);
			if (fileName.EndsWith("keys.json", StringComparison.OrdinalIgnoreCase) ||
				fileName.EndsWith("settings.json", StringComparison.OrdinalIgnoreCase) ||
				fileName.EndsWith(".key", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string destFile = Path.GetFullPath(Path.Combine(dstRoot, relPath));
			if (!destFile.StartsWith(dstRoot, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string destDir = Path.GetDirectoryName(destFile);
			if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

			try
			{
				File.Copy(srcFile, destFile, true);
			}
			catch (IOException)
			{
				string backupPath = destFile + ".old_update";
				try
				{
					if (File.Exists(backupPath)) File.Delete(backupPath);
					File.Move(destFile, backupPath);
					File.Copy(srcFile, destFile, true);
				}
				catch { }
			}
		}
	}

	private static void ExtractZipSafely(string zipPath, string destination)
	{
		string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
		using (ZipArchive archive = ZipFile.OpenRead(zipPath))
		{
			foreach (ZipArchiveEntry entry in archive.Entries)
			{
				string entryName = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
				string target = Path.GetFullPath(Path.Combine(root, entryName));
				if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
					throw new InvalidDataException("更新包包含越界压缩路径，已拒绝解压。");
				if (string.IsNullOrEmpty(entry.Name))
				{
					Directory.CreateDirectory(target);
					continue;
				}
				string parent = Path.GetDirectoryName(target);
				if (string.IsNullOrEmpty(parent) || !parent.StartsWith(root, StringComparison.OrdinalIgnoreCase))
					throw new InvalidDataException("更新包包含异常文件路径，已拒绝解压。");
				Directory.CreateDirectory(parent);
				using (Stream input = entry.Open())
				using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
				{
					input.CopyTo(output);
				}
			}
		}
	}

	private async Task DownloadWithFallbackAsync(string fastUrl, string fallbackUrl, string destinationFile, CancellationToken ct)
	{
		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
		}
		catch { }

		bool downloadSuccess = false;
		Exception lastError = null;

		if (!string.IsNullOrEmpty(fastUrl))
		{
			try
			{
				await PerformDownloadStreamAsync(fastUrl, destinationFile, ct);
				downloadSuccess = true;
			}
			catch (OperationCanceledException) { throw; }
			catch (Exception ex)
			{
				lastError = ex;
				if (File.Exists(destinationFile)) File.Delete(destinationFile);
			}
		}

		if (!downloadSuccess && !string.IsNullOrEmpty(fallbackUrl))
		{
			ct.ThrowIfCancellationRequested();
			Invoke((MethodInvoker)delegate
			{
				_lblStatus.Text = "节点响应超时，正在切换备用节点下载...";
				_lblStatus.ForeColor = Color.FromArgb(245, 158, 11);
			});

			try
			{
				await PerformDownloadStreamAsync(fallbackUrl, destinationFile, ct);
				downloadSuccess = true;
			}
			catch (OperationCanceledException) { throw; }
			catch (Exception ex)
			{
				lastError = ex;
				if (File.Exists(destinationFile)) File.Delete(destinationFile);
			}
		}

		if (!downloadSuccess)
		{
			throw new Exception(lastError?.Message ?? "无法连接到更新服务器，请检查网络设置！");
		}
	}

	private async Task PerformDownloadStreamAsync(string url, string destinationFile, CancellationToken ct)
	{
		var req = (HttpWebRequest)WebRequest.Create(url);
		req.Method = "GET";
		req.UserAgent = "Mozilla/5.0 (Windows NT 10; Win64; x64) VideoBatchStudio AutoUpdater";
		req.Timeout = 20000;
		req.ReadWriteTimeout = 30000;
		req.Proxy = WebRequest.DefaultWebProxy;
		req.Proxy.Credentials = CredentialCache.DefaultCredentials;

		using (var response = (HttpWebResponse)await req.GetResponseAsync())
		{
			if (response.StatusCode != HttpStatusCode.OK)
			{
				throw new Exception($"服务器返回异常状态: {(int)response.StatusCode}");
			}

			long totalBytes = response.ContentLength;
			using (var stream = response.GetResponseStream())
			using (var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				byte[] buffer = new byte[65536];
				long totalRead = 0;
				int bytesRead;
				var sw = Stopwatch.StartNew();
				long lastBytes = 0;
				double lastSeconds = 0;
				double speedMB = 0;

				while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
				{
					await fs.WriteAsync(buffer, 0, bytesRead, ct);
					totalRead += bytesRead;

					double elapsed = sw.Elapsed.TotalSeconds;
					if (elapsed - lastSeconds >= 0.4)
					{
						speedMB = (totalRead - lastBytes) / (1024.0 * 1024.0) / (elapsed - lastSeconds);
						lastBytes = totalRead;
						lastSeconds = elapsed;

						int percent = totalBytes > 0 ? (int)(totalRead * 100 / totalBytes) : 0;
						percent = Math.Max(0, Math.Min(100, percent));

						string downloadedMb = (totalRead / (1024.0 * 1024.0)).ToString("0.0");
						string totalMb = totalBytes > 0 ? (totalBytes / (1024.0 * 1024.0)).ToString("0.0") : "未知";

						BeginInvoke((MethodInvoker)delegate
						{
							_pbProgress.Value = percent;
							_lblStatus.ForeColor = Color.FromArgb(56, 189, 248);
							_lblStatus.Text = $"正在静默下载: {percent}% ({downloadedMb} MB / {totalMb} MB) · {speedMB:0.0} MB/s";
						});
					}
				}
			}
		}
	}

	private static void TryCleanFiles(string zipFile, string extractDir)
	{
		try
		{
			if (!string.IsNullOrEmpty(zipFile) && File.Exists(zipFile)) File.Delete(zipFile);
		}
		catch { }
		try
		{
			if (!string.IsNullOrEmpty(extractDir) && Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
		}
		catch { }
	}
}
