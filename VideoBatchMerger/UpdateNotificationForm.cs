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
		Width = 620;
		Height = 490;
		MinimumSize = new Size(540, 420);
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
			Text = $"当前版本: V{currentVer}  ➔  最新版本: V{newVer}  (发布日期: {dateStr})",
			Font = new Font("Microsoft YaHei UI", 9f),
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
			Height = 64,
			BackColor = Color.FromArgb(20, 26, 38),
			Padding = new Padding(16, 12, 16, 12)
		};

		Button btnDownload = new Button
		{
			Text = "🚀 立即前往下载新版",
			Size = new Size(160, 36),
			Location = new Point(bottomBar.ClientSize.Width - 176, 14),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 99, 235),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			Cursor = Cursors.Hand
		};
		btnDownload.FlatAppearance.BorderSize = 0;
		btnDownload.Click += delegate
		{
			try
			{
				string url = string.IsNullOrEmpty(downloadUrl) ? "https://github.com/100142armin-lgtm/VideoBatchStudio/releases/latest" : downloadUrl;
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "打开浏览器失败: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		};
		bottomBar.Controls.Add(btnDownload);

		Button btnCopyLink = new Button
		{
			Text = "📋 复制发布链接",
			Size = new Size(120, 36),
			Location = new Point(bottomBar.ClientSize.Width - 308, 14),
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
				string url = string.IsNullOrEmpty(downloadUrl) ? "https://github.com/100142armin-lgtm/VideoBatchStudio/releases/latest" : downloadUrl;
				Clipboard.SetText(url);
				MessageBox.Show(this, "下载链接已复制到剪贴板！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch { }
		};
		bottomBar.Controls.Add(btnCopyLink);

		Button btnRemindLater = new Button
		{
			Text = "稍后提醒",
			Size = new Size(92, 36),
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
