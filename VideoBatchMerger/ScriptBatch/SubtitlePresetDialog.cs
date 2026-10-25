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

		public SubtitlePresetDialog(ScriptBatchSettings settings, Action onPresetSelected)
		{
			_settings = settings;
			_onPresetSelected = onPresetSelected;

			Text = "🎨 爆款与 Canva 花字字幕预设库 (一键套用 · 支持二次微调描边/颜色/底框)";
			Size = new Size(1120, 740);
			MinimumSize = new Size(960, 620);
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
			mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185f)); // 左侧分类导航
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
				Text = "字幕特效预设库",
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

			string[] categories = { "全部", "爆款推荐", "Canva花字", "纯净立体", "底框样式", "特殊动效" };
			foreach (var cat in categories)
			{
				string labelText = cat switch
				{
					"全部" => "🌟 全部预设",
					"爆款推荐" => "🔥 爆款推荐 (葡语/海外)",
					"Canva花字" => "🎨 Canva 花字特效",
					"纯净立体" => "✨ 纯净立体",
					"底框样式" => "🏷️ 胶囊底框",
					_ => "⚡ 特殊动效"
				};

				var btnCat = new Button
				{
					Text = labelText,
					Width = 160,
					Height = 38,
					FlatStyle = FlatStyle.Flat,
					BackColor = cat == _activeCategory ? AccentBlue : Color.Transparent,
					ForeColor = cat == _activeCategory ? Color.White : TextMuted,
					Cursor = Cursors.Hand,
					TextAlign = ContentAlignment.MiddleLeft,
					Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold),
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
				Width = 210,
				Height = 305,
				BackColor = CardBg,
				Margin = new Padding(8, 8, 8, 12),
				Padding = new Padding(8)
			};

			// 模拟 9:16 手机微型预览窗
			var miniCanvas = new MiniSubtitleBox(preset)
			{
				Dock = DockStyle.Top,
				Height = 190,
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
				Text = $"• {preset.TagText} ({preset.FontFamily})",
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
				Text = isSelected ? "✓ 当前已选用 (点击重置为此预设)" : "选用此预设",
				Dock = DockStyle.Bottom,
				Height = 28,
				BackColor = isSelected ? Color.FromArgb(16, 185, 129) : AccentBlue,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand,
				Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold)
			};
			btnApply.FlatAppearance.BorderSize = 0;
			btnApply.Click += (_, _) =>
			{
				ApplyPreset(preset);
				DialogResult = DialogResult.OK;
				Close();
			};
			infoPanel.Controls.Add(btnApply);
			lblName.SendToBack();

			card.Controls.Add(infoPanel);
			infoPanel.BringToFront();
			return card;
		}

		private void ApplyPreset(SubtitlePreset preset)
		{
			preset.ApplyToSettings(_settings);
			_settings.Save();
			_onPresetSelected?.Invoke();
		}

		// 微型 9:16 手机屏幕高保真渲染控件
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
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

				int w = ClientSize.Width;
				int h = ClientSize.Height;
				if (w <= 10 || h <= 10) return;

				using (var bgBrush = _preset.DisplayMode == 5
					? new LinearGradientBrush(ClientRectangle, Color.FromArgb(117, 199, 239), Color.FromArgb(235, 244, 231), 90f)
					: new LinearGradientBrush(ClientRectangle, Color.FromArgb(24, 36, 58), Color.FromArgb(10, 15, 28), 90f))
				{
					g.FillRectangle(bgBrush, ClientRectangle);
				}

				using (var borderPen = new Pen(Color.FromArgb(40, 56, 84), 1f))
				{
					g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
				}

				string fontFamilyName = SubtitlePresetCatalog.ResolveInstalledFont(_preset.FontFamily);
				Color textCol = _preset.PreviewTextColor;
				Color strokeCol = _preset.PreviewStrokeColor;
				Color shadowCol = _preset.PreviewShadowColor;
				float strokeW = _preset.OutlineWidth > 0 ? Math.Max(1.8f, _preset.OutlineWidth * 0.55f) : 0f;

				if (_preset.DisplayMode == 5)
				{
					using var font = new Font(fontFamilyName, 14.5f, FontStyle.Bold, GraphicsUnit.Pixel);
					string first = "DIOS";
					string rest = " TE BENDIGA";
					float firstW = g.MeasureString(first, font).Width;
					float restW = g.MeasureString(rest, font).Width;
					float x = (w - firstW - restW) / 2f;
					float y = h * 0.49f;
					DrawOutlinedWordAt(g, first, font, x, y, _preset.PreviewHighlightTextColor, strokeCol, strokeW, shadowCol);
					DrawOutlinedWordAt(g, rest, font, x + firstW, y, textCol, strokeCol, strokeW, shadowCol);
				}
				else if (_preset.DisplayMode == 4)
				{
					// 上滚字幕模式预览 (图4/图5)
					float fontSize = 11.5f;
					using var font = new Font(fontFamilyName, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
					string[] lines = { "Acalma o coração,", "enxuga a lágrima", "e renova a fé" };
					float lineH = fontSize * 1.38f;
					float startY = h * 0.34f;

					if (_preset.ScrollPanelEnabled)
					{
						Color panelBase = ScriptBatchSettings.ParseToColor(_preset.ScrollPanelColorHex, Color.FromArgb(234, 230, 223));
						int alpha = Math.Max(30, Math.Min(245, (int)Math.Round(_preset.ScrollPanelOpacity * 2.55)));
						var panelRect = new RectangleF(w * 0.07f, startY - 8f, w * 0.86f, lines.Length * lineH + 16f);
						using var panelBrush = new SolidBrush(Color.FromArgb(alpha, panelBase));
						using var panelPath = GetRoundedRectPath(panelRect, 6f);
						g.FillPath(panelBrush, panelPath);
					}

					for (int i = 0; i < lines.Length; i++)
					{
						DrawOutlinedCenteredLine(g, lines[i], font, w / 2f, startY + i * lineH, textCol, strokeCol, strokeW, shadowCol);
					}
				}
				else if (_preset.DisplayMode == 3)
				{
					// 逐词底框高亮模式预览 (图1/图2/图6：带描边 + 彩色底框)
					float fontSize = 14.5f;
					using var font = new Font(fontFamilyName, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
					string word1 = "Em nome";
					string word2 = "de";
					string word3 = "Jesus";

					float line1Y = h * 0.45f;
					float line2Y = line1Y + fontSize * 1.35f;

					DrawOutlinedCenteredLine(g, word1, font, w / 2f, line1Y, textCol, strokeCol, strokeW, shadowCol);

					var sz2 = g.MeasureString(word2, font);
					var sz3 = g.MeasureString(word3, font);
					float gap = 4f;
					float totalW = sz2.Width + gap + sz3.Width;
					float startX = (w - totalW) / 2f;

					// 绘制高亮词底框 (Layer 0)
					if (_preset.HighlightBoxEnabled)
					{
						var boxRect = new RectangleF(startX - 4f, line2Y - 2f, sz2.Width + 8f, fontSize + 6f);
						int boxAlpha = Math.Max(40, Math.Min(255, (int)Math.Round(_preset.HighlightBoxOpacity * 2.55)));
						using var boxBrush = new SolidBrush(Color.FromArgb(boxAlpha, _preset.PreviewHighlightBgColor));
						using var boxPath = GetRoundedRectPath(boxRect, 4f);
						g.FillPath(boxBrush, boxPath);
					}

					// 绘制带粗描边的高亮词与普通词 (Layer 1)
					DrawOutlinedWordAt(g, word2, font, startX, line2Y, _preset.PreviewHighlightTextColor, strokeCol, strokeW, shadowCol);
					DrawOutlinedWordAt(g, word3, font, startX + sz2.Width + gap, line2Y, textCol, strokeCol, strokeW, shadowCol);
				}
				else
				{
					// Canva 花字 / 纯净立体 / 整句胶囊底框 / 逐词发光
					float fontSize = _preset.Category == "Canva花字" ? 17f : 14.5f;
					using var font = new Font(fontFamilyName, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
					string sampleLine1 = _preset.Category == "Canva花字" ? "CANVA" : "PARE TUDO";
					string sampleLine2 = _preset.Category == "Canva花字" ? "POP STYLE" : "AGORA";

					float line1Y = h * 0.44f;
					float line2Y = line1Y + fontSize * 1.32f;

					if (_preset.HasBox)
					{
						var sz1 = g.MeasureString(sampleLine1, font);
						var sz2 = g.MeasureString(sampleLine2, font);
						float maxW = Math.Max(sz1.Width, sz2.Width);
						var boxRect = new RectangleF((w - maxW) / 2f - 10f, line1Y - 5f, maxW + 20f, fontSize * 2.65f + 10f);
						using var boxBrush = new SolidBrush(_preset.PreviewBoxColor);
						using var boxPath = GetRoundedRectPath(boxRect, 6f);
						g.FillPath(boxBrush, boxPath);
					}

					Color line2Color = _preset.DisplayMode == 1 ? _preset.PreviewHighlightTextColor : textCol;
					DrawOutlinedCenteredLine(g, sampleLine1, font, w / 2f, line1Y, textCol, strokeCol, strokeW, shadowCol);
					DrawOutlinedCenteredLine(g, sampleLine2, font, w / 2f, line2Y, line2Color, strokeCol, strokeW, shadowCol);
				}
			}

			private static void DrawOutlinedCenteredLine(Graphics g, string text, Font font, float centerX, float topY, Color fillCol, Color strokeCol, float strokeW, Color shadowCol)
			{
				using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
				using var path = new GraphicsPath();
				path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new PointF(centerX, topY), sf);

				if (shadowCol != Color.Transparent && shadowCol.A > 0)
				{
					using var shMatrix = new Matrix();
					shMatrix.Translate(1.5f, 1.8f);
					using var shPath = (GraphicsPath)path.Clone();
					shPath.Transform(shMatrix);
					using var shBrush = new SolidBrush(Color.FromArgb(160, shadowCol));
					g.FillPath(shBrush, shPath);
				}

				if (strokeW > 0.1f && strokeCol != Color.Transparent)
				{
					using var pen = new Pen(strokeCol, strokeW) { LineJoin = LineJoin.Round };
					g.DrawPath(pen, path);
				}

				using var brush = new SolidBrush(fillCol);
				g.FillPath(brush, path);
			}

			private static void DrawOutlinedWordAt(Graphics g, string text, Font font, float leftX, float topY, Color fillCol, Color strokeCol, float strokeW, Color shadowCol)
			{
				using var path = new GraphicsPath();
				path.AddString(text, font.FontFamily, (int)font.Style, font.Size, new PointF(leftX, topY), StringFormat.GenericDefault);

				if (shadowCol != Color.Transparent && shadowCol.A > 0)
				{
					using var shMatrix = new Matrix();
					shMatrix.Translate(1.5f, 1.8f);
					using var shPath = (GraphicsPath)path.Clone();
					shPath.Transform(shMatrix);
					using var shBrush = new SolidBrush(Color.FromArgb(160, shadowCol));
					g.FillPath(shBrush, shPath);
				}

				if (strokeW > 0.1f && strokeCol != Color.Transparent)
				{
					using var pen = new Pen(strokeCol, strokeW) { LineJoin = LineJoin.Round };
					g.DrawPath(pen, path);
				}

				using var brush = new SolidBrush(fillCol);
				g.FillPath(brush, path);
			}

			private static GraphicsPath GetRoundedRectPath(RectangleF rect, float radius)
			{
				var path = new GraphicsPath();
				float d = radius * 2;
				if (d > rect.Width) d = rect.Width;
				if (d > rect.Height) d = rect.Height;
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
