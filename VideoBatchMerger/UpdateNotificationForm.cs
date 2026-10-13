using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VideoBatchMerger;

/// <summary>
/// 现代化暗黑风格新版本更新提醒弹窗
/// 支持呈现新功能列表、版本对比、一键跳转 GitHub 下载与更新日志
/// </summary>
internal sealed class UpdateNotificationForm : Form
{
	public UpdateNotificationForm(string currentVer, string newVer, string releaseTitle, string releaseDate, IEnumerable<string> changelog, string downloadUrl)
	{
		Text = $"🎉 发现新版本 V{newVer} · 视频批处理工具升级提示";
		Width = 720;
		Height = 520;
		MinimumSize = new Size(640, 460);
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
			Text = $"当前版本: V{currentVer}  ➔  最新版本: V{newVer} ({dateStr}) · 推荐下载内置全部离线 AI 模型的完整免配置便携包",
			Font = new Font("Microsoft YaHei UI", 8.75f),
			ForeColor = Color.FromArgb(148, 163, 184),
			AutoSize = true,
			Location = new Point(20, 40)
		};
		headerPanel.Controls.Add(bannerSub);
		Controls.Add(headerPanel);

		// 2. Bottom Buttons Bar
		Panel bottomBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 68,
			BackColor = Color.FromArgb(20, 26, 38),
			Padding = new Padding(16, 14, 16, 14)
		};

		string tagUrl = $"https://github.com/100142armin-lgtm/VideoBatchStudio/releases/tag/v{newVer}";
		string portableZipUrl = $"https://github.com/100142armin-lgtm/VideoBatchStudio/releases/download/v{newVer}/VideoBatchStudio_Portable.zip";
		string fastPortableZipUrl = $"https://ghfast.top/https://github.com/100142armin-lgtm/VideoBatchStudio/releases/download/v{newVer}/VideoBatchStudio_Portable.zip";
		string exeUrl = $"https://github.com/100142armin-lgtm/VideoBatchStudio/releases/download/v{newVer}/VideoBatchStudio.exe";

		Button btnFastDownload = new Button
		{
			Text = "⚡ 国内极速下载便携包",
			Size = new Size(175, 38),
			Location = new Point(bottomBar.ClientSize.Width - 190, 14),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(16, 185, 129),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			Cursor = Cursors.Hand
		};
		btnFastDownload.FlatAppearance.BorderSize = 0;
		btnFastDownload.Click += delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo(fastPortableZipUrl) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "打开极速下载链接失败: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		};
		bottomBar.Controls.Add(btnFastDownload);

		Button btnDownload = new Button
		{
			Text = "📦 官方原包",
			Size = new Size(100, 38),
			Location = new Point(bottomBar.ClientSize.Width - 298, 14),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 99, 235),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 9f),
			Cursor = Cursors.Hand
		};
		btnDownload.FlatAppearance.BorderSize = 0;
		btnDownload.Click += delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo(portableZipUrl) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "打开官方下载链接失败: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		};
		bottomBar.Controls.Add(btnDownload);

		Button btnOpenPage = new Button
		{
			Text = "🌐 发布页面",
			Size = new Size(95, 38),
			Location = new Point(bottomBar.ClientSize.Width - 400, 14),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(30, 41, 59),
			ForeColor = Color.FromArgb(226, 232, 240),
			Font = new Font("Microsoft YaHei UI", 9f),
			Cursor = Cursors.Hand
		};
		btnOpenPage.FlatAppearance.BorderSize = 0;
		btnOpenPage.Click += delegate
		{
			try
			{
				Process.Start(new ProcessStartInfo(tagUrl) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "打开发布主页失败: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		};
		bottomBar.Controls.Add(btnOpenPage);

		Button btnCopyLink = new Button
		{
			Text = "📋 复制链接",
			Size = new Size(90, 38),
			Location = new Point(bottomBar.ClientSize.Width - 498, 14),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(30, 41, 59),
			ForeColor = Color.FromArgb(226, 232, 240),
			Font = new Font("Microsoft YaHei UI", 9f),
			Cursor = Cursors.Hand
		};
		btnCopyLink.FlatAppearance.BorderSize = 0;
		btnCopyLink.Click += delegate
		{
			try
			{
				string text = $"VideoBatchStudio V{newVer} 最新版本下载地址：\r\n" +
				              $"1. 国内极速下载便携包 (含离线模型/解压即用):\r\n   {fastPortableZipUrl}\r\n\r\n" +
				              $"2. GitHub 官方原包下载:\r\n   {portableZipUrl}\r\n\r\n" +
				              $"3. 独立程序 EXE:\r\n   {exeUrl}\r\n\r\n" +
				              $"4. 官方发布主页:\r\n   {tagUrl}";
				Clipboard.SetText(text);
				MessageBox.Show(this, $"V{newVer} 最新版本的全部高速与官方下载链接已复制到剪贴板！可以直接发送给团队成员。", "复制成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch { }
		};
		bottomBar.Controls.Add(btnCopyLink);

		Button btnRemindLater = new Button
		{
			Text = "稍后提醒",
			Size = new Size(82, 38),
			Location = new Point(16, 14),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(30, 41, 59),
			ForeColor = Color.FromArgb(148, 163, 184),
			Font = new Font("Microsoft YaHei UI", 9f),
			Cursor = Cursors.Hand
		};
		btnRemindLater.FlatAppearance.BorderSize = 0;
		btnRemindLater.Click += delegate { Close(); };
		bottomBar.Controls.Add(btnRemindLater);

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

		// Docking order
		contentPanel.BringToFront();
		KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
	}
}
