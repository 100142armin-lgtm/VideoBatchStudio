using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace VideoBatchMerger.ScriptBatch
{
	internal sealed class QcReportDialog : Form
	{
		private readonly ScriptBatchItem _item;

		private static readonly Color DarkBg = Color.FromArgb(15, 23, 42);
		private static readonly Color CardBg = Color.FromArgb(30, 41, 59);
		private static readonly Color BorderColor = Color.FromArgb(51, 65, 85);
		private static readonly Color TextWhite = Color.FromArgb(248, 250, 252);
		private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
		private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
		private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
		private static readonly Color AccentRed = Color.FromArgb(239, 68, 68);
		private static readonly Color AccentYellow = Color.FromArgb(245, 158, 11);

		public QcReportDialog(ScriptBatchItem item)
		{
			_item = item ?? throw new ArgumentNullException(nameof(item));

			Text = "📋 AI 4重质检雷达 · 全景诊断报告";
			Size = new Size(760, 580);
			MinimumSize = new Size(650, 480);
			StartPosition = FormStartPosition.CenterParent;
			BackColor = DarkBg;
			ForeColor = TextWhite;
			Font = new Font("Microsoft YaHei UI", 9.5f);
			ShowIcon = false;

			InitializeComponents();
		}

		private void InitializeComponents()
		{
			var mainLayout = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 1,
				RowCount = 3,
				Padding = new Padding(18)
			};
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));  // 顶部状态卡
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 中部诊断详情文本区
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));  // 底部操作区

			// 1. 顶部状态卡
			var topCard = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = CardBg,
				Padding = new Padding(16, 12, 16, 12)
			};
			var lblTitle = new Label
			{
				Text = $"任务 #{_item.Id}  |  文案代码: {(_item.ScriptCode ?? "无")}  |  标题: {_item.ChineseTitle}",
				Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
				ForeColor = TextWhite,
				AutoSize = true,
				Location = new Point(16, 12)
			};
			var lblBadge = new Label
			{
				Text = string.IsNullOrEmpty(_item.QcBadge) ? "⚪ 未执行质检" : _item.QcBadge,
				Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold),
				ForeColor = GetBadgeColor(_item.QcBadge),
				AutoSize = true,
				Location = new Point(16, 40)
			};
			topCard.Controls.Add(lblTitle);
			topCard.Controls.Add(lblBadge);
			mainLayout.Controls.Add(topCard, 0, 0);

			// 2. 中部诊断详情文本区
			var detailBox = new RichTextBox
			{
				Dock = DockStyle.Fill,
				BackColor = CardBg,
				ForeColor = TextWhite,
				BorderStyle = BorderStyle.None,
				Font = new Font("Microsoft YaHei UI", 10f),
				ReadOnly = true,
				Margin = new Padding(0, 10, 0, 10)
			};

			string details = _item.QcDetails;
			if (string.IsNullOrWhiteSpace(details))
			{
				details = "【质检说明】\n本条目尚未执行全链路质检检测。\n\n批量成片渲染时，AI 4重质检雷达将全自动执行以下 4 项全景检测：\n" +
				          "1. 🎙️ 语音波形健康度与语速检测 (CPS 标准区间 12~17 字符/秒)\n" +
				          "2. 📝 字幕排版与视界安全区检测 (自动排查超长单句并智能折行)\n" +
				          "3. 🎞️ 视频渲染与音画对齐完整性检测 (严格核对时长与体积)\n" +
				          "4. 💡 专属优化实操建议 (针对语速/排版给出具体参数调节方案)";
			}
			detailBox.Text = details;
			mainLayout.Controls.Add(detailBox, 0, 1);

			// 3. 底部操作按钮
			var bottomFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.RightToLeft,
				Margin = new Padding(0)
			};

			var btnClose = new Button
			{
				Text = "我知道了",
				Width = 110,
				Height = 36,
				BackColor = AccentBlue,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand,
				DialogResult = DialogResult.OK
			};
			btnClose.FlatAppearance.BorderSize = 0;
			bottomFlow.Controls.Add(btnClose);

			if (File.Exists(_item.VideoOutputPath))
			{
				var btnOpenDir = new Button
				{
					Text = "📁 打开所在目录",
					Width = 130,
					Height = 36,
					BackColor = BorderColor,
					ForeColor = TextWhite,
					FlatStyle = FlatStyle.Flat,
					Cursor = Cursors.Hand,
					Margin = new Padding(0, 0, 10, 0)
				};
				btnOpenDir.FlatAppearance.BorderSize = 0;
				btnOpenDir.Click += (_, _) =>
				{
					try
					{
						Process.Start("explorer.exe", $"/select,\"{_item.VideoOutputPath}\"");
					}
					catch { }
				};
				bottomFlow.Controls.Add(btnOpenDir);

				var btnPlay = new Button
				{
					Text = "▶️ 播放成品视频",
					Width = 130,
					Height = 36,
					BackColor = AccentGreen,
					ForeColor = Color.White,
					FlatStyle = FlatStyle.Flat,
					Cursor = Cursors.Hand,
					Margin = new Padding(0, 0, 10, 0)
				};
				btnPlay.FlatAppearance.BorderSize = 0;
				btnPlay.Click += (_, _) =>
				{
					try
					{
						Process.Start(new ProcessStartInfo(_item.VideoOutputPath) { UseShellExecute = true });
					}
					catch { }
				};
				bottomFlow.Controls.Add(btnPlay);
			}

			mainLayout.Controls.Add(bottomFlow, 0, 2);
			Controls.Add(mainLayout);
		}

		private static Color GetBadgeColor(string badge)
		{
			if (string.IsNullOrEmpty(badge)) return TextMuted;
			if (badge.Contains("合格")) return AccentGreen;
			if (badge.Contains("微调")) return AccentYellow;
			if (badge.Contains("异常") || badge.Contains("失败")) return AccentRed;
			return TextWhite;
		}
	}
}
