using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace VideoBatchMerger.ScriptBatch
{
	internal sealed class SubtitlePresetDialog : Form
	{
		private readonly ScriptBatchSettings _settings;
		private readonly Action _onPresetSelected;

		private FlowLayoutPanel _galleryFlow;
		private string _activeCategory = "全部";

		private static readonly Color DarkBg = Color.FromArgb(13, 17, 28);
		private static readonly Color CardBg = Color.FromArgb(20, 27, 44);
		private static readonly Color CardBorder = Color.FromArgb(35, 48, 74);
		private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
		private static readonly Color TextWhite = Color.FromArgb(248, 250, 252);
		private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
		private static readonly Color AccentYellow = Color.FromArgb(245, 158, 11);

		public SubtitlePresetDialog(ScriptBatchSettings settings, Action onPresetSelected)
		{
			_settings = settings;
			_onPresetSelected = onPresetSelected;

			Text = "🎨 动态字幕预设库 (短视频爆款底框 · 卡拉OK高亮 · 特殊动效)";
			Size = new Size(1100, 720);
			MinimumSize = new Size(950, 600);
			StartPosition = FormStartPosition.CenterParent;
			BackColor = DarkBg;
			ForeColor = TextWhite;
			Font = new Font("Microsoft YaHei UI", 9f);
			ShowIcon = false;

			InitializeComponents();
			RenderGallery();
		}

		private void InitializeComponents()
		{
			var mainLayout = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 2,
				RowCount = 1
			};
			mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f)); // 左侧分类导航
			mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));  // 右侧预设卡片流式网格

			// 1. 左侧导航分类栏
			var navPanel = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(16, 22, 38),
				Padding = new Padding(12, 16, 12, 16)
			};

			var lblNavTitle = new Label
			{
				Text = "动态字幕预设库",
				Dock = DockStyle.Top,
				Height = 32,
				Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
				ForeColor = Color.White
			};
			navPanel.Controls.Add(lblNavTitle);

			var navFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				Padding = new Padding(0, 10, 0, 0)
			};

			string[] categories = { "全部", "底框样式", "基础字幕", "卡拉OK高亮", "特殊动效" };
			foreach (var cat in categories)
			{
				var btnCat = new Button
				{
					Text = cat == "全部" ? "🌟 全部预设" : (cat == "底框样式" ? "🏷️ 底框样式" : (cat == "基础字幕" ? "📄 基础字幕" : (cat == "卡拉OK高亮" ? "🎤 卡拉OK高亮" : "⚡ 特殊动效"))),
					Width = 155,
					Height = 38,
					FlatStyle = FlatStyle.Flat,
					BackColor = cat == _activeCategory ? AccentBlue : Color.Transparent,
					ForeColor = cat == _activeCategory ? Color.White : TextMuted,
					Cursor = Cursors.Hand,
					TextAlign = ContentAlignment.MiddleLeft,
					Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
					Margin = new Padding(0, 0, 0, 6)
				};
				btnCat.FlatAppearance.BorderSize = 0;
				btnCat.Click += (s, _) =>
				{
					_activeCategory = cat;
					foreach (Control c in navFlow.Controls)
					{
						if (c is Button b)
						{
							bool isCur = b == s;
							b.BackColor = isCur ? AccentBlue : Color.Transparent;
							b.ForeColor = isCur ? Color.White : TextMuted;
						}
					}
					RenderGallery();
				};
				navFlow.Controls.Add(btnCat);
			}

			navPanel.Controls.Add(navFlow);
			// Fill 控件必须位于 Z 序最前，才会在标题 (Top) 停靠之后再占用剩余空间，避免「全部预设」被标题遮挡
			navFlow.BringToFront();
			mainLayout.Controls.Add(navPanel, 0, 0);

			// 2. 右侧卡片瀑布流
			var galleryContainer = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				Padding = new Padding(16)
			};

			_galleryFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Top,
				AutoSize = true,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = true,
				Padding = new Padding(0)
			};

			galleryContainer.Controls.Add(_galleryFlow);
			mainLayout.Controls.Add(galleryContainer, 1, 0);

			Controls.Add(mainLayout);
		}

		private void RenderGallery()
		{
			_galleryFlow.Controls.Clear();

			var items = _activeCategory == "全部"
				? SubtitlePresetCatalog.Presets
				: SubtitlePresetCatalog.Presets.Where(p => p.Category == _activeCategory).ToList();

			foreach (var preset in items)
			{
				var card = BuildPresetCard(preset);
				_galleryFlow.Controls.Add(card);
			}
		}

		private Panel BuildPresetCard(SubtitlePreset preset)
		{
			var card = new Panel
			{
				Width = 205,
				Height = 295,
				BackColor = CardBg,
				Margin = new Padding(8, 8, 8, 12),
				Padding = new Padding(8)
			};

			// 模拟 9:16 手机微型预览窗
			var miniCanvas = new MiniSubtitleBox(preset)
			{
				Dock = DockStyle.Top,
				Height = 185,
				BackColor = Color.FromArgb(8, 12, 22)
			};
			card.Controls.Add(miniCanvas);

			var infoPanel = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(0, 6, 0, 0)
			};

			var lblName = new Label
			{
				Text = preset.Name,
				Dock = DockStyle.Top,
				Height = 20,
				Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
				ForeColor = Color.White,
				AutoEllipsis = true
			};
			infoPanel.Controls.Add(lblName);

			var lblTag = new Label
			{
				Text = $"• {preset.TagText}",
				Dock = DockStyle.Top,
				Height = 18,
				Font = new Font("Microsoft YaHei UI", 7.5f),
				ForeColor = TextMuted,
				AutoEllipsis = true
			};
			infoPanel.Controls.Add(lblTag);

			bool isSelected = string.Equals(_settings.SubtitleStyleId, preset.Id, StringComparison.OrdinalIgnoreCase);

			var btnApply = new Button
			{
				Text = isSelected ? "✓ 当前已选用" : "选用此预设",
				Dock = DockStyle.Bottom,
				Height = 28,
				BackColor = isSelected ? Color.FromArgb(16, 185, 129) : AccentBlue,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand,
				Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold)
			};
			btnApply.FlatAppearance.BorderSize = 0;
			btnApply.Click += (_, _) =>
			{
				ApplyPreset(preset);
				DialogResult = DialogResult.OK;
				Close();
			};
			infoPanel.Controls.Add(btnApply);
			// 保证名称在最上、标签在其下
			lblName.SendToBack();

			card.Controls.Add(infoPanel);
			// 信息区 (Fill) 置于 Z 序最前，避免被预览画布遮挡导致名称/标签不可见
			infoPanel.BringToFront();
			return card;
		}

		private void ApplyPreset(SubtitlePreset preset)
		{
			_settings.SubtitleStyleId = preset.Id;
			_settings.SubtitleColorHex = preset.PrimaryColorHex;
			_settings.SubtitleStrokeColorHex = preset.OutlineColorHex;
			_settings.SubtitleStrokeWidth = preset.OutlineWidth;
			_settings.SubtitleFontSize = preset.FontSize;
			_settings.SubtitleDisplayMode = preset.DisplayMode;
			_settings.Save();

			_onPresetSelected?.Invoke();
		}

		// 微型 9:16 手机屏幕渲染控件
		private sealed class MiniSubtitleBox : Control
		{
			private readonly SubtitlePreset _preset;

			public MiniSubtitleBox(SubtitlePreset preset)
			{
				_preset = preset;
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				base.OnPaint(e);
				var g = e.Graphics;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

				int w = ClientSize.Width;
				int h = ClientSize.Height;
				if (w <= 10 || h <= 10) return;

				// 绘制深黑微弱粒子科技网格
				using (var bgBrush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(14, 20, 36), Color.FromArgb(7, 10, 18), 90f))
				{
					g.FillRectangle(bgBrush, ClientRectangle);
				}

				using (var borderPen = new Pen(Color.FromArgb(40, 56, 84), 1f))
				{
					g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
				}

				// 绘制样例字幕
				string sampleText = "示例 字幕 动画 预览";
				float fontSize = 9.5f;
				using var font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Bold);

				var sz = g.MeasureString(sampleText, font);
				float textX = (w - sz.Width) / 2f;
				float textY = h * 0.72f;

				// 若有底框 (BorderStyle == 3)
				if (_preset.HasBox)
				{
					float boxPadX = 8f;
					float boxPadY = 4f;
					var boxRect = new RectangleF(textX - boxPadX, textY - boxPadY, sz.Width + boxPadX * 2, sz.Height + boxPadY * 2);

					using var boxBrush = new SolidBrush(_preset.PreviewBoxColor);
					// 绘制圆角底框
					using var path = GetRoundedRectPath(boxRect, 4f);
					g.FillPath(boxBrush, path);
				}
				else
				{
					// 绘制描边
					using var strokeBrush = new SolidBrush(_preset.PreviewStrokeColor);
					g.DrawString(sampleText, font, strokeBrush, textX + 1.2f, textY + 1.2f);
					g.DrawString(sampleText, font, strokeBrush, textX - 1.2f, textY - 1.2f);
					g.DrawString(sampleText, font, strokeBrush, textX + 1.2f, textY - 1.2f);
					g.DrawString(sampleText, font, strokeBrush, textX - 1.2f, textY + 1.2f);
				}

				using var textBrush = new SolidBrush(_preset.PreviewTextColor);
				g.DrawString(sampleText, font, textBrush, textX, textY);
			}

			private static GraphicsPath GetRoundedRectPath(RectangleF rect, float radius)
			{
				var path = new GraphicsPath();
				float d = radius * 2;
				path.AddArc(rect.X, rect.Y, d, d, 180, 90);
				path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
				path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
				path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
				path.CloseFigure();
				return path;
			}
		}
	}
}
