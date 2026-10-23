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
/// 现代化暗黑风格新版本全自动更新弹窗
/// 支持呈现新功能列表、版本对比、一键静默极速下载、自动解压覆盖并重启
/// </summary>
internal sealed class UpdateNotificationForm : Form
{
	private readonly string _currentVer;
	private readonly string _newVer;
	private readonly string _tagUrl;
	private readonly string _fastPortableZipUrl;
	private readonly string _portableZipUrl;

	private Label _lblStatus;
	private ProgressBar _pbProgress;
	private Button _btnAutoUpdate;
	private Button _btnOpenPage;
	private Button _btnRemindLater;
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
		_tagUrl = string.IsNullOrWhiteSpace(downloadUrl) ? $"https://github.com/100142armin-lgtm/VideoBatchStudio/releases/tag/v{newVer}" : downloadUrl;
		_fastPortableZipUrl = string.IsNullOrWhiteSpace(fastPortableZipUrl)
			? $"https://ghfast.top/https://github.com/100142armin-lgtm/VideoBatchStudio/releases/download/v{newVer}/VideoBatchStudio_Portable.zip"
			: fastPortableZipUrl;
		_portableZipUrl = string.IsNullOrWhiteSpace(portableZipUrl)
			? $"https://github.com/100142armin-lgtm/VideoBatchStudio/releases/download/v{newVer}/VideoBatchStudio_Portable.zip"
			: portableZipUrl;

		Text = $"🎉 发现新版本 V{newVer} · 视频批处理工具";
		Width = 720;
		Height = 540;
		MinimumSize = new Size(640, 480);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(15, 20, 28);
		ForeColor = Color.White;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		KeyPreview = true;

		// 1. Header Banner
		Panel headerPanel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 72,
			BackColor = Color.FromArgb(24, 32, 46),
			Padding = new Padding(20, 12, 20, 10)
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
			Text = $"当前版本: V{currentVer}  ➔  最新版本: V{newVer} ({dateStr}) · 支持一键无感全自动升级",
			Font = new Font("Microsoft YaHei UI", 8.75f),
			ForeColor = Color.FromArgb(148, 163, 184),
			AutoSize = true,
			Location = new Point(20, 40)
		};
		headerPanel.Controls.Add(bannerSub);
		Controls.Add(headerPanel);

		// 2. Bottom Controls Bar
		Panel bottomBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 96,
			BackColor = Color.FromArgb(20, 26, 38),
			Padding = new Padding(16, 8, 16, 12)
		};

		_lblStatus = new Label
		{
			Text = "💡 点击【一键自动升级】，软件将自动极速下载、静默覆盖并重新启动，无需手动解压。",
			Font = new Font("Microsoft YaHei UI", 9f),
			ForeColor = Color.FromArgb(148, 163, 184),
			Location = new Point(16, 10),
			Size = new Size(bottomBar.ClientSize.Width - 32, 20),
			Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
			AutoEllipsis = true
		};
		bottomBar.Controls.Add(_lblStatus);

		_pbProgress = new ProgressBar
		{
			Location = new Point(16, 32),
			Size = new Size(bottomBar.ClientSize.Width - 32, 8),
			Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
			Minimum = 0,
			Maximum = 100,
			Value = 0,
			Visible = false
		};
		bottomBar.Controls.Add(_pbProgress);

		_btnRemindLater = new Button
		{
			Text = "稍后提醒",
			Size = new Size(84, 38),
			Location = new Point(16, 48),
			Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(30, 41, 59),
			ForeColor = Color.FromArgb(148, 163, 184),
			Font = new Font("Microsoft YaHei UI", 9f),
			Cursor = Cursors.Hand
		};
		_btnRemindLater.FlatAppearance.BorderSize = 0;
		_btnRemindLater.Click += delegate { Close(); };
		bottomBar.Controls.Add(_btnRemindLater);

		_btnOpenPage = new Button
		{
			Text = "🌐 发布主页",
			Size = new Size(100, 38),
			Location = new Point(bottomBar.ClientSize.Width - 315, 48),
			Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(30, 41, 59),
			ForeColor = Color.FromArgb(226, 232, 240),
			Font = new Font("Microsoft YaHei UI", 9f),
			Cursor = Cursors.Hand
		};
		_btnOpenPage.FlatAppearance.BorderSize = 0;
		_btnOpenPage.Click += delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo(_tagUrl) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "打开发布主页失败: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		};
		bottomBar.Controls.Add(_btnOpenPage);

		_btnAutoUpdate = new Button
		{
			Text = "⚡ 一键自动升级 (推荐)",
			Size = new Size(190, 38),
			Location = new Point(bottomBar.ClientSize.Width - 206, 48),
			Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(16, 185, 129),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			Cursor = Cursors.Hand
		};
		_btnAutoUpdate.FlatAppearance.BorderSize = 0;
		_btnAutoUpdate.Click += async (s, e) => await StartAutoUpdateAsync();
		bottomBar.Controls.Add(_btnAutoUpdate);

		Controls.Add(bottomBar);

		// 3. Central Content: Changelog Details
		Panel contentPanel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(15, 20, 28),
			Padding = new Padding(20, 16, 20, 10)
		};

		Label lblChangeTitle = new Label
		{
			Text = string.IsNullOrEmpty(releaseTitle) ? "💡 本次更新内容与新功能说明：" : $"💡 {releaseTitle}：",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(226, 232, 240),
			AutoSize = true,
			Location = new Point(20, 16)
		};
		contentPanel.Controls.Add(lblChangeTitle);

		TextBox tbChangelog = new TextBox
		{
			Location = new Point(20, 44),
			Size = new Size(contentPanel.ClientSize.Width - 40, contentPanel.ClientSize.Height - 56),
			Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
			BackColor = Color.FromArgb(20, 26, 38),
			ForeColor = Color.FromArgb(226, 232, 240),
			Font = new Font("Microsoft YaHei UI", 9.5f),
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
			var dr = MessageBox.Show(this, "新版本正在后台极速下载中，确定要取消本次自动升级吗？", "取消更新", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
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
	/// 执行一键全自动下载、解压覆盖与重启
	/// </summary>
	private async Task StartAutoUpdateAsync()
	{
		if (_isUpdating) return;
		_isUpdating = true;
		_cts = new CancellationTokenSource();

		_btnAutoUpdate.Enabled = false;
		_btnAutoUpdate.Text = "⏳ 正在准备下载...";
		_btnRemindLater.Enabled = false;
		_pbProgress.Visible = true;
		_pbProgress.Value = 0;
		_lblStatus.ForeColor = Color.FromArgb(56, 189, 248);
		_lblStatus.Text = "🚀 正在连接高速下载节点...";

		string tempZip = Path.Combine(Path.GetTempPath(), $"vbs_update_{_newVer}_{DateTime.Now.Ticks}.zip");
		string tempExtractDir = Path.Combine(Path.GetTempPath(), $"vbs_extract_{_newVer}_{DateTime.Now.Ticks}");

		try
		{
			// 1. 下载更新包
			await DownloadWithFallbackAsync(_fastPortableZipUrl, _portableZipUrl, tempZip, _cts.Token);

			_lblStatus.Text = "📦 下载完成，正在解压更新包准备就绪...";
			_pbProgress.Value = 100;

			// 2. 解压更新包并在后台准备重启脚本
			await Task.Run(() =>
			{
				if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
				Directory.CreateDirectory(tempExtractDir);

				ZipFile.ExtractToDirectory(tempZip, tempExtractDir);

				// 智能探测 VideoBatchStudio.exe 所在层级
				string exeFound = Directory.GetFiles(tempExtractDir, "VideoBatchStudio.exe", SearchOption.AllDirectories).FirstOrDefault();
				if (string.IsNullOrEmpty(exeFound))
				{
					throw new Exception("更新包中未检测到核心可执行文件 VideoBatchStudio.exe！");
				}

				string sourceFolder = Path.GetDirectoryName(exeFound);
				string currentAppDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');

				// 3. 生成独立后台升级批处理脚本
				string updaterBat = Path.Combine(Path.GetTempPath(), $"vbs_updater_{DateTime.Now.Ticks}.bat");
				string batScript =
					"@echo off\r\n" +
					"chcp 65001 >nul\r\n" +
					"title 正在升级 VideoBatchStudio...\r\n" +
					"echo ========================================================\r\n" +
					$"echo   正在将 VideoBatchStudio 升级至最新版本 V{_newVer}\r\n" +
					"echo   请稍候，升级覆盖完成后将自动重新启动软件...\r\n" +
					"echo ========================================================\r\n" +
					"timeout /t 2 /nobreak >nul\r\n" +
					$":: 覆盖新文件，但绝不覆盖或删除用户的私有密钥与配置文件\r\n" +
					$"robocopy \"{sourceFolder}\" \"{currentAppDir}\" /E /IS /IT /XF \"*keys.json\" \"*settings.json\" \"*.key\" /XD \"auditions\" /R:2 /W:1 >nul\r\n" +
					"if %ERRORLEVEL% GEQ 8 (\r\n" +
					$"    xcopy /y /e /h /r \"{sourceFolder}\\*\" \"{currentAppDir}\\\" >nul\r\n" +
					")\r\n" +
					"echo 升级成功，正在启动新版本...\r\n" +
					$"start \"\" \"{currentAppDir}\\VideoBatchStudio.exe\"\r\n" +
					"timeout /t 2 /nobreak >nul\r\n" +
					$"rd /s /q \"{tempExtractDir}\" 2>nul\r\n" +
					$"del \"{tempZip}\" 2>nul\r\n" +
					"del \"%~f0\" 2>nul\r\n" +
					"exit\r\n";

				File.WriteAllText(updaterBat, batScript, new UTF8Encoding(false));

				// 启动后台更新脚本并安全退出主程序
				var psi = new ProcessStartInfo
				{
					FileName = "cmd.exe",
					Arguments = $"/c \"{updaterBat}\"",
					CreateNoWindow = true,
					UseShellExecute = false
				};
				Process.Start(psi);
			});

			_lblStatus.Text = "🎉 升级完成！正在重新启动软件...";
			await Task.Delay(800);

			// 安全退出当前旧进程
			Application.Exit();
			Environment.Exit(0);
		}
		catch (OperationCanceledException)
		{
			_isUpdating = false;
			_lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
			_lblStatus.Text = "💡 已取消本次更新。";
			_btnAutoUpdate.Enabled = true;
			_btnAutoUpdate.Text = "⚡ 一键自动升级 (推荐)";
			_btnRemindLater.Enabled = true;
			_pbProgress.Visible = false;
			TryCleanFiles(tempZip, tempExtractDir);
		}
		catch (Exception ex)
		{
			_isUpdating = false;
			_lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
			_lblStatus.Text = $"❌ 自动升级失败: {ex.Message}，您可点击【发布主页】手动下载。";
			_btnAutoUpdate.Enabled = true;
			_btnAutoUpdate.Text = "🔄 重试自动升级";
			_btnRemindLater.Enabled = true;
			TryCleanFiles(tempZip, tempExtractDir);
		}
	}

	/// <summary>
	/// 带国内 CDN 极速加速与 GitHub 官方源自动容灾回退的下载器
	/// </summary>
	private async Task DownloadWithFallbackAsync(string fastUrl, string fallbackUrl, string destinationFile, CancellationToken ct)
	{
		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
		}
		catch { }

		bool downloadSuccess = false;
		Exception lastError = null;

		// 1. 优先尝试极速镜像节点
		if (!string.IsNullOrEmpty(fastUrl))
		{
			try
			{
				await PerformDownloadStreamAsync(fastUrl, destinationFile, "极速节点", ct);
				downloadSuccess = true;
			}
			catch (OperationCanceledException) { throw; }
			catch (Exception ex)
			{
				lastError = ex;
				if (File.Exists(destinationFile)) File.Delete(destinationFile);
			}
		}

		// 2. 若极速节点失败或不存在，回退至官方原包
		if (!downloadSuccess && !string.IsNullOrEmpty(fallbackUrl))
		{
			ct.ThrowIfCancellationRequested();
			Invoke((MethodInvoker)delegate
			{
				_lblStatus.Text = "极速节点响应超时，正在切换至 GitHub 官方备用节点下载...";
				_lblStatus.ForeColor = Color.FromArgb(245, 158, 11);
			});

			try
			{
				await PerformDownloadStreamAsync(fallbackUrl, destinationFile, "官方备用节点", ct);
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
			throw new Exception(lastError?.Message ?? "无法连接到更新服务器，请检查网络或代理设置！");
		}
	}

	/// <summary>
	/// 流式下载并实时反馈进度与速率
	/// </summary>
	private async Task PerformDownloadStreamAsync(string url, string destinationFile, string nodeName, CancellationToken ct)
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
				throw new Exception($"服务器返回异常状态码: {(int)response.StatusCode} {response.StatusDescription}");
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
							_lblStatus.Text = $"正在通过{nodeName}极速下载: {percent}% ({downloadedMb} MB / {totalMb} MB) · {speedMB:0.0} MB/s";
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
