using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VideoBatchMerger
{
	public class MergeTitleStyle
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string FontFamily { get; set; } = "Microsoft YaHei UI";
		public FontStyle FontStyle { get; set; } = FontStyle.Bold;
		public Color MainTextColor { get; set; } = Color.White;
		public Color SubTextColor { get; set; } = Color.FromArgb(220, 220, 220);
		public Color StrokeColor { get; set; } = Color.Transparent;
		public float StrokeWidthRatio { get; set; } = 0f;
		public string BannerType { get; set; } = "None"; // None, YellowCard, RedAlertCard, NavyBar, MatteBlackCard, NeonCyanCard, MintGlassCard, LuxuryGoldCard, CrimsonSealCard, UrbanStreetCard, TechGlassCard
		public Color BannerBgColor { get; set; } = Color.Transparent;
		public Color BannerBorderColor { get; set; } = Color.Transparent;
		public float BannerBorderWidth { get; set; } = 0f;
		public Color ShadowColor { get; set; } = Color.FromArgb(160, 0, 0, 0);
		public PointF ShadowOffset { get; set; } = new PointF(2f, 3f);
	}

	public static class MergeTitleStyleCatalog
	{
		private static readonly List<MergeTitleStyle> _styles = new List<MergeTitleStyle>
		{
			new MergeTitleStyle
			{
				Id = "tiktok_yellow",
				Name = "🌟 【抖音/快手爆款】醒目黄底纯黑大字 (TikTok Black on Yellow Card)",
				FontFamily = "Microsoft YaHei UI",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.FromArgb(15, 23, 42),
				SubTextColor = Color.FromArgb(30, 41, 59),
				StrokeColor = Color.Transparent,
				StrokeWidthRatio = 0f,
				BannerType = "YellowCard",
				BannerBgColor = Color.FromArgb(253, 224, 71),
				BannerBorderColor = Color.FromArgb(245, 158, 11),
				BannerBorderWidth = 3f,
				ShadowColor = Color.FromArgb(120, 0, 0, 0),
				ShadowOffset = new PointF(0f, 4f)
			},
			new MergeTitleStyle
			{
				Id = "red_alert",
				Name = "🚨 【热点/警示】红底白字高光卡片 (Breaking Alert Red Banner)",
				FontFamily = "Microsoft YaHei UI",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.White,
				SubTextColor = Color.FromArgb(254, 226, 226),
				StrokeColor = Color.FromArgb(153, 27, 27),
				StrokeWidthRatio = 0.04f,
				BannerType = "RedAlertCard",
				BannerBgColor = Color.FromArgb(225, 29, 72),
				BannerBorderColor = Color.FromArgb(254, 202, 202),
				BannerBorderWidth = 3f,
				ShadowColor = Color.FromArgb(140, 0, 0, 0),
				ShadowOffset = new PointF(0f, 4f)
			},
			new MergeTitleStyle
			{
				Id = "cinematic_white",
				Name = "🎬 【经典影视】极简纯白大字 + 柔和电影阴影 (Cinematic Clean White)",
				FontFamily = "Microsoft YaHei UI",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.White,
				SubTextColor = Color.FromArgb(226, 232, 240),
				StrokeColor = Color.FromArgb(40, 0, 0, 0),
				StrokeWidthRatio = 0.03f,
				BannerType = "None",
				ShadowColor = Color.FromArgb(200, 0, 0, 0),
				ShadowOffset = new PointF(3f, 4f)
			},
			new MergeTitleStyle
			{
				Id = "youtube_bold_pop",
				Name = "💥 【欧美油管/INS】醒目立体双重描边大字 (YouTube Bold Pop White)",
				FontFamily = "Impact",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.White,
				SubTextColor = Color.FromArgb(254, 240, 138),
				StrokeColor = Color.FromArgb(15, 23, 42),
				StrokeWidthRatio = 0.10f,
				BannerType = "None",
				ShadowColor = Color.FromArgb(220, 0, 0, 0),
				ShadowOffset = new PointF(4f, 6f)
			},
			new MergeTitleStyle
			{
				Id = "variety_cartoon",
				Name = "🍭 【综艺花字】粉紫梦幻渐变 + 鲜艳双描边 (Variety Show Pop Cartoon)",
				FontFamily = "Microsoft YaHei UI",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.FromArgb(255, 245, 157),
				SubTextColor = Color.White,
				StrokeColor = Color.FromArgb(192, 38, 211),
				StrokeWidthRatio = 0.08f,
				BannerType = "VarietyCard",
				BannerBgColor = Color.FromArgb(220, 24, 24, 38),
				BannerBorderColor = Color.FromArgb(244, 114, 182),
				BannerBorderWidth = 2.5f,
				ShadowColor = Color.FromArgb(160, 0, 0, 0),
				ShadowOffset = new PointF(3f, 4f)
			},
			new MergeTitleStyle
			{
				Id = "news_lower_third",
				Name = "📰 【深度纪实/解说】深蓝专业底栏 + 金色副标 (News Navy Lower-Third)",
				FontFamily = "Microsoft YaHei UI",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.White,
				SubTextColor = Color.FromArgb(250, 204, 21),
				StrokeColor = Color.Transparent,
				StrokeWidthRatio = 0f,
				BannerType = "NavyBar",
				BannerBgColor = Color.FromArgb(230, 15, 23, 42),
				BannerBorderColor = Color.FromArgb(56, 189, 248),
				BannerBorderWidth = 2f,
				ShadowColor = Color.FromArgb(140, 0, 0, 0),
				ShadowOffset = new PointF(0f, 3f)
			},
			new MergeTitleStyle
			{
				Id = "cyberpunk_neon",
				Name = "⚡ 【赛博电竞】荧光青绿字 + 蓝紫暗夜发光 (Cyberpunk Neon Cyan)",
				FontFamily = "Microsoft YaHei UI",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.FromArgb(52, 211, 153),
				SubTextColor = Color.FromArgb(167, 139, 250),
				StrokeColor = Color.FromArgb(17, 24, 39),
				StrokeWidthRatio = 0.07f,
				BannerType = "NeonCyanCard",
				BannerBgColor = Color.FromArgb(220, 10, 15, 26),
				BannerBorderColor = Color.FromArgb(34, 211, 238),
				BannerBorderWidth = 3f,
				ShadowColor = Color.FromArgb(180, 0, 0, 0),
				ShadowOffset = new PointF(0f, 4f)
			},
			new MergeTitleStyle
			{
				Id = "traditional_chinese",
				Name = "🏛️ 【国风古韵】赤红印章底 + 典雅金宋韵 (Traditional Vermilion & Gold)",
				FontFamily = "SimSun",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.FromArgb(254, 240, 138),
				SubTextColor = Color.FromArgb(254, 226, 226),
				StrokeColor = Color.FromArgb(80, 0, 0, 0),
				StrokeWidthRatio = 0.02f,
				BannerType = "CrimsonSealCard",
				BannerBgColor = Color.FromArgb(230, 153, 27, 27),
				BannerBorderColor = Color.FromArgb(217, 119, 6),
				BannerBorderWidth = 2.5f,
				ShadowColor = Color.FromArgb(140, 0, 0, 0),
				ShadowOffset = new PointF(2f, 3f)
			},
			new MergeTitleStyle
			{
				Id = "luxury_gold",
				Name = "👑 【黑金轻奢】香槟金字 + 磨砂炭黑底卡 (Luxury Champagne Gold)",
				FontFamily = "Georgia",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.FromArgb(253, 230, 138),
				SubTextColor = Color.FromArgb(203, 213, 225),
				StrokeColor = Color.Transparent,
				StrokeWidthRatio = 0f,
				BannerType = "MatteBlackCard",
				BannerBgColor = Color.FromArgb(225, 17, 24, 39),
				BannerBorderColor = Color.FromArgb(234, 179, 8),
				BannerBorderWidth = 2.5f,
				ShadowColor = Color.FromArgb(150, 0, 0, 0),
				ShadowOffset = new PointF(0f, 4f)
			},
			new MergeTitleStyle
			{
				Id = "street_punch",
				Name = "🔥 【潮流街头】醒目斜体黄字 + 粗旷重阴影 (Street Urban Italic Punch)",
				FontFamily = "Arial Black",
				FontStyle = FontStyle.Bold | FontStyle.Italic,
				MainTextColor = Color.FromArgb(250, 204, 21),
				SubTextColor = Color.White,
				StrokeColor = Color.FromArgb(15, 23, 42),
				StrokeWidthRatio = 0.09f,
				BannerType = "UrbanStreetCard",
				BannerBgColor = Color.FromArgb(220, 15, 23, 42),
				BannerBorderColor = Color.FromArgb(239, 68, 68),
				BannerBorderWidth = 3f,
				ShadowColor = Color.FromArgb(220, 0, 0, 0),
				ShadowOffset = new PointF(5f, 6f)
			},
			new MergeTitleStyle
			{
				Id = "mint_glass",
				Name = "🌿 【日系清新/VLOG】薄荷绿微透底板 + 森林墨绿字 (Fresh Mint Glass)",
				FontFamily = "Microsoft YaHei UI",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.FromArgb(6, 78, 59),
				SubTextColor = Color.FromArgb(20, 83, 45),
				StrokeColor = Color.Transparent,
				StrokeWidthRatio = 0f,
				BannerType = "MintGlassCard",
				BannerBgColor = Color.FromArgb(215, 209, 250, 229),
				BannerBorderColor = Color.FromArgb(110, 231, 183),
				BannerBorderWidth = 2.5f,
				ShadowColor = Color.FromArgb(100, 0, 0, 0),
				ShadowOffset = new PointF(0f, 3f)
			},
			new MergeTitleStyle
			{
				Id = "blockbuster_sans",
				Name = "🎥 【大片巨幕】经典全大写无衬线巨字 (Blockbuster Sans Bold)",
				FontFamily = "Impact",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.White,
				SubTextColor = Color.FromArgb(226, 232, 240),
				StrokeColor = Color.FromArgb(20, 20, 20),
				StrokeWidthRatio = 0.05f,
				BannerType = "None",
				ShadowColor = Color.FromArgb(220, 0, 0, 0),
				ShadowOffset = new PointF(3f, 4f)
			},
			new MergeTitleStyle
			{
				Id = "tech_glass",
				Name = "💎 【未来科技】毛玻璃透明底板 + 冰晶蓝描边 (Glassmorphism Ice Blue)",
				FontFamily = "Microsoft YaHei UI",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.FromArgb(224, 242, 254),
				SubTextColor = Color.FromArgb(186, 230, 253),
				StrokeColor = Color.Transparent,
				StrokeWidthRatio = 0f,
				BannerType = "TechGlassCard",
				BannerBgColor = Color.FromArgb(195, 12, 74, 110),
				BannerBorderColor = Color.FromArgb(56, 189, 248),
				BannerBorderWidth = 2.5f,
				ShadowColor = Color.FromArgb(160, 0, 0, 0),
				ShadowOffset = new PointF(0f, 4f)
			},
			new MergeTitleStyle
			{
				Id = "impact_outline",
				Name = "✏️ 【纯白无衬线】加厚纯黑描边 (Impact White Black Outline)",
				FontFamily = "Impact",
				FontStyle = FontStyle.Bold,
				MainTextColor = Color.White,
				SubTextColor = Color.FromArgb(254, 240, 138),
				StrokeColor = Color.Black,
				StrokeWidthRatio = 0.12f,
				BannerType = "None",
				ShadowColor = Color.FromArgb(200, 0, 0, 0),
				ShadowOffset = new PointF(3f, 4f)
			}
		};

		public static IReadOnlyList<MergeTitleStyle> Styles => _styles;

		public static MergeTitleStyle GetStyle(string id)
		{
			var s = _styles.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
			return s ?? _styles[0];
		}

		public static string CleanFilenameForTitle(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw)) return "";
			string name = Path.GetFileNameWithoutExtension(raw);
			if (string.IsNullOrWhiteSpace(name)) return "";

			// Strip common redundant suffixes: _1080p, _720p, _4k, _副本, _剪辑版, _compressed, etc.
			name = Regex.Replace(name, @"[\-_ ]?(1080[pP]|720[pP]|4[kK]|2[kK]|HD|FHD|UHD|标清|高清|超清|原画|剪辑版|副本|compressed|trimmed)$", "", RegexOptions.IgnoreCase);
			name = Regex.Replace(name, @"^\d+[\.、\-_ ]+", ""); // Strip leading track numbers like "01. " or "01 - "
			return name.Trim();
		}

		public static string ResolveTitleText(string template, IReadOnlyList<string> groupFiles, int groupIndex)
		{
			if (string.IsNullOrWhiteSpace(template)) return "";

			string result = template;
			string firstFile = (groupFiles != null && groupFiles.Count > 0) ? groupFiles[0] : "";
			string cleanName = CleanFilenameForTitle(firstFile);

			result = result.Replace("{文件名}", cleanName);
			result = result.Replace("{filename}", cleanName);
			result = result.Replace("{name}", cleanName);

			string idxStr = (groupIndex + 1).ToString();
			result = result.Replace("{序号}", idxStr);
			result = result.Replace("{index}", idxStr);
			result = result.Replace("{idx}", idxStr);

			string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
			result = result.Replace("{日期}", dateStr);
			result = result.Replace("{date}", dateStr);

			return result.Trim();
		}

		public static Bitmap RenderTitleBitmap(MergeTitlePlan plan, int targetW, int targetH, string resolvedMainText, string resolvedSubText)
		{
			if (targetW <= 0) targetW = 1080;
			if (targetH <= 0) targetH = 1920;

			Bitmap bmp = new Bitmap(targetW, targetH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			using (Graphics g = Graphics.FromImage(bmp))
			{
				g.Clear(Color.Transparent);
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;

				if (string.IsNullOrWhiteSpace(resolvedMainText)) return bmp;

				MergeTitleStyle style = GetStyle(plan.StyleId);

				float refDim = Math.Min(targetW, targetH);
				float resScale = refDim / 1080f;
				float userScale = Math.Max(0.2f, Math.Min(2.5f, plan.FontSizeScale));
				float baseFontSize = (targetH >= targetW ? 92f : 64f) * resScale * userScale;
				if (baseFontSize < 14f * resScale) baseFontSize = 14f * resScale;

				// Resolve Font
				Font fontMain = CreateSafeFont(style.FontFamily, baseFontSize, style.FontStyle);
				float maxAllowedW = targetW * 0.82f; // Safe margin: 82% of target width

				List<string> lines = WrapTextToBalancedLines(g, resolvedMainText, fontMain, maxAllowedW);

				// Safety Auto-Downscale Loop:
				// As long as any line exceeds maxAllowedW, or lines > 3, reduce font size until it fits perfectly!
				float minAllowedFontSize = 12f * resScale;
				while (baseFontSize > minAllowedFontSize)
				{
					bool anyLineOverflows = false;
					foreach (var l in lines)
					{
						if (g.MeasureString(l, fontMain).Width > maxAllowedW)
						{
							anyLineOverflows = true;
							break;
						}
					}

					if (!anyLineOverflows && lines.Count <= 3)
					{
						break;
					}

					baseFontSize -= Math.Max(1.5f * resScale, baseFontSize * 0.05f);
					fontMain.Dispose();
					fontMain = CreateSafeFont(style.FontFamily, baseFontSize, style.FontStyle);
					lines = WrapTextToBalancedLines(g, resolvedMainText, fontMain, maxAllowedW);
				}

				using (fontMain)
				{
					if (lines.Count == 0) lines.Add(resolvedMainText);

					float mainLineHeight = fontMain.GetHeight(g) * 1.18f;
					float mainBlockHeight = lines.Count * mainLineHeight;

					bool hasSub = !string.IsNullOrWhiteSpace(resolvedSubText);
					float subFontSize = Math.Max(12f * resScale, baseFontSize * 0.44f);
					float subBlockHeight = 0f;
					Font fontSub = null;
					if (hasSub)
					{
						fontSub = CreateSafeFont(style.FontFamily, subFontSize, FontStyle.Bold);
						subBlockHeight = fontSub.GetHeight(g) * 1.25f + 16f * resScale;
					}

					try
					{
						float totalBlockH = mainBlockHeight + subBlockHeight;
						float maxLineWidth = 0f;
						foreach (string line in lines)
						{
							float lw = g.MeasureString(line, fontMain).Width;
							if (lw > maxLineWidth) maxLineWidth = lw;
						}
						if (hasSub && fontSub != null)
						{
							float sw = g.MeasureString(resolvedSubText, fontSub).Width + 40f * resScale;
							if (sw > maxLineWidth) maxLineWidth = sw;
						}
						maxLineWidth = Math.Min(targetW * 0.94f, maxLineWidth);

						// Determine Center Y based on position
						float targetCenterY;
						string pos = plan.Position ?? "中间分割线";
						if (pos.Contains("中间分割线") || pos.Contains("分割线"))
						{
							targetCenterY = targetH * 0.50f;
						}
						else if (pos.Contains("偏上")) // "居中偏上"
						{
							targetCenterY = targetH * 0.25f;
						}
						else if (pos.Contains("偏下")) // "居中偏下"
						{
							targetCenterY = targetH * 0.75f;
						}
						else if (pos.Contains("顶部") || pos.Contains("顶端"))
						{
							targetCenterY = targetH * 0.12f;
						}
						else if (pos.Contains("底部"))
						{
							targetCenterY = targetH * 0.86f;
						}
						else if (pos.Contains("居中") || pos.Contains("中央"))
						{
							targetCenterY = targetH * 0.50f;
						}
						else
						{
							targetCenterY = targetH * 0.50f;
						}

						targetCenterY += plan.CustomOffsetY * resScale;
						float targetCenterX = targetW / 2f;
						float blockTopY = targetCenterY - totalBlockH / 2f;
						blockTopY = Math.Max(12f * resScale, Math.Min(targetH - totalBlockH - 12f * resScale, blockTopY));

						// Draw Banner Background if any
						DrawBanner(g, style, targetW, targetH, targetCenterX, blockTopY, totalBlockH, maxLineWidth, resScale);

						// Draw Main Lines
						float strokeW = baseFontSize * style.StrokeWidthRatio;
						for (int i = 0; i < lines.Count; i++)
						{
							string line = lines[i];
							SizeF sz = g.MeasureString(line, fontMain);
							float lx = targetCenterX - sz.Width / 2f;
							float ly = blockTopY + i * mainLineHeight;

							using (GraphicsPath path = new GraphicsPath())
							{
								path.AddString(line, fontMain.FontFamily, (int)fontMain.Style, fontMain.SizeInPoints * (g.DpiY / 72f), new PointF(lx, ly), StringFormat.GenericTypographic);

								// Shadow
								if (style.ShadowColor.A > 0)
								{
									using (Matrix m = new Matrix())
									{
										m.Translate(style.ShadowOffset.X * resScale, style.ShadowOffset.Y * resScale);
										using (GraphicsPath shadowPath = (GraphicsPath)path.Clone())
										{
											shadowPath.Transform(m);
											using (Brush sb = new SolidBrush(style.ShadowColor))
											{
												g.FillPath(sb, shadowPath);
											}
										}
									}
								}

								// Stroke
								if (strokeW > 0.5f && style.StrokeColor.A > 0)
								{
									using (Pen pen = new Pen(style.StrokeColor, strokeW))
									{
										pen.LineJoin = LineJoin.Round;
										g.DrawPath(pen, path);
									}
								}

								// Text Fill
								using (Brush tb = new SolidBrush(style.MainTextColor))
								{
									g.FillPath(tb, path);
								}
							}
						}

						// Draw Subtitle if present
						if (hasSub && fontSub != null)
						{
							float subY = blockTopY + mainBlockHeight + 12f * resScale;
							SizeF subSz = g.MeasureString(resolvedSubText, fontSub);
							float subX = targetCenterX - subSz.Width / 2f;

							using (GraphicsPath subPath = new GraphicsPath())
							{
								subPath.AddString(resolvedSubText, fontSub.FontFamily, (int)fontSub.Style, fontSub.SizeInPoints * (g.DpiY / 72f), new PointF(subX, subY), StringFormat.GenericTypographic);

								if (style.ShadowColor.A > 0)
								{
									using (Matrix m = new Matrix())
									{
										m.Translate(style.ShadowOffset.X * 0.7f * resScale, style.ShadowOffset.Y * 0.7f * resScale);
										using (GraphicsPath sp = (GraphicsPath)subPath.Clone())
										{
											sp.Transform(m);
											using (Brush sb = new SolidBrush(style.ShadowColor))
											{
												g.FillPath(sb, sp);
											}
										}
									}
								}

								using (Brush subBrush = new SolidBrush(style.SubTextColor))
								{
									g.FillPath(subBrush, subPath);
								}
							}
						}
					}
					finally
					{
						fontSub?.Dispose();
					}
				}
			}
			return bmp;
		}

		private static void DrawBanner(Graphics g, MergeTitleStyle style, int w, int h, float centerX, float topY, float blockH, float maxLineW, float resScale)
		{
			if (string.Equals(style.BannerType, "None", StringComparison.OrdinalIgnoreCase) || style.BannerBgColor.A == 0)
				return;

			if (style.BannerType == "NavyBar")
			{
				int barH = (int)(blockH + 60f * resScale);
				int barY = (int)Math.Max(0, topY - 30f * resScale);
				using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, barY, w, barH), Color.FromArgb(235, 15, 23, 42), Color.FromArgb(235, 30, 58, 138), LinearGradientMode.Horizontal))
				{
					g.FillRectangle(lgb, 0, barY, w, barH);
				}
				using (Pen cyanLine = new Pen(style.BannerBorderColor, style.BannerBorderWidth * resScale))
				{
					g.DrawLine(cyanLine, 0, barY, w, barY);
					g.DrawLine(cyanLine, 0, barY + barH, w, barY + barH);
				}
				return;
			}

			// Rounded Card Banners
			float cardW = Math.Min(w * 0.94f, maxLineW + 72f * resScale);
			float cardH = blockH + 38f * resScale;
			RectangleF rect = new RectangleF(centerX - cardW / 2f, topY - 19f * resScale, cardW, cardH);
			int radius = (int)(18f * resScale);

			using (GraphicsPath cardPath = CreateRoundedRectanglePath(Rectangle.Round(rect), radius))
			{
				// Shadow for card
				using (Matrix m = new Matrix())
				{
					m.Translate(0, 4f * resScale);
					using (GraphicsPath sp = (GraphicsPath)cardPath.Clone())
					{
						sp.Transform(m);
						using (Brush sb = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
						{
							g.FillPath(sb, sp);
						}
					}
				}

				using (Brush bgBrush = new SolidBrush(style.BannerBgColor))
				{
					g.FillPath(bgBrush, cardPath);
				}

				if (style.BannerBorderWidth > 0.5f && style.BannerBorderColor.A > 0)
				{
					using (Pen bp = new Pen(style.BannerBorderColor, style.BannerBorderWidth * resScale))
					{
						g.DrawPath(bp, cardPath);
					}
				}
			}
		}

		private static Font CreateSafeFont(string preferredFamily, float size, FontStyle style)
		{
			try
			{
				return new Font(preferredFamily, size, style);
			}
			catch
			{
				try
				{
					return new Font("Microsoft YaHei UI", size, style);
				}
				catch
				{
					return new Font(FontFamily.GenericSansSerif, size, style);
				}
			}
		}

		private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
		{
			GraphicsPath path = new GraphicsPath();
			if (radius <= 0)
			{
				path.AddRectangle(rect);
				return path;
			}
			int diameter = radius * 2;
			Rectangle arc = new Rectangle(rect.Location, new Size(diameter, diameter));
			path.AddArc(arc, 180, 90);
			arc.X = rect.Right - diameter;
			path.AddArc(arc, 270, 90);
			arc.Y = rect.Bottom - diameter;
			path.AddArc(arc, 0, 90);
			arc.X = rect.Left;
			path.AddArc(arc, 90, 90);
			path.CloseFigure();
			return path;
		}

		private static List<string> WrapTextToBalancedLines(Graphics g, string text, Font font, float maxWidth)
		{
			List<string> result = new List<string>();
			if (string.IsNullOrWhiteSpace(text)) return result;

			text = text.Trim();
			float totalW = g.MeasureString(text, font).Width;
			if (totalW <= maxWidth)
			{
				result.Add(text);
				return result;
			}

			// Check if text has spaces (Western words like Portuguese / English / Spanish)
			bool hasSpaces = text.Contains(" ");
			if (hasSpaces)
			{
				string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				StringBuilder currentLine = new StringBuilder();

				foreach (string word in words)
				{
					string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
					float testW = g.MeasureString(testLine, font).Width;

					if (testW <= maxWidth)
					{
						currentLine.Append(currentLine.Length == 0 ? word : " " + word);
					}
					else
					{
						if (currentLine.Length > 0)
						{
							result.Add(currentLine.ToString());
							currentLine.Clear();
						}

						// If single word itself exceeds maxWidth, hard break by characters
						if (g.MeasureString(word, font).Width > maxWidth)
						{
							StringBuilder wordPart = new StringBuilder();
							for (int ci = 0; ci < word.Length; ci++)
							{
								string testPart = wordPart.ToString() + word[ci];
								if (g.MeasureString(testPart, font).Width <= maxWidth)
								{
									wordPart.Append(word[ci]);
								}
								else
								{
									if (wordPart.Length > 0)
									{
										result.Add(wordPart.ToString());
										wordPart.Clear();
									}
									wordPart.Append(word[ci]);
								}
							}
							if (wordPart.Length > 0)
							{
								currentLine.Append(wordPart.ToString());
							}
						}
						else
						{
							currentLine.Append(word);
						}
					}
				}

				if (currentLine.Length > 0)
				{
					result.Add(currentLine.ToString());
				}
			}
			else
			{
				// Chinese / CJK characters without spaces
				StringBuilder currentLine = new StringBuilder();
				for (int i = 0; i < text.Length; i++)
				{
					char c = text[i];
					string testLine = currentLine.ToString() + c;
					float testW = g.MeasureString(testLine, font).Width;

					if (testW <= maxWidth)
					{
						currentLine.Append(c);
					}
					else
					{
						if (currentLine.Length > 0)
						{
							result.Add(currentLine.ToString());
							currentLine.Clear();
						}
						currentLine.Append(c);
					}
				}
				if (currentLine.Length > 0)
				{
					result.Add(currentLine.ToString());
				}
			}

			// If wrapped into 2 lines, try to balance their lengths for aesthetics
			if (result.Count == 2 && hasSpaces)
			{
				float w1 = g.MeasureString(result[0], font).Width;
				float w2 = g.MeasureString(result[1], font).Width;
				if (Math.Abs(w1 - w2) > maxWidth * 0.25f)
				{
					string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					int bestSplit = -1;
					float minDiff = float.MaxValue;
					for (int si = 1; si < words.Length; si++)
					{
						string l1 = string.Join(" ", words.Take(si));
						string l2 = string.Join(" ", words.Skip(si));
						float cw1 = g.MeasureString(l1, font).Width;
						float cw2 = g.MeasureString(l2, font).Width;
						if (cw1 <= maxWidth && cw2 <= maxWidth)
						{
							float diff = Math.Abs(cw1 - cw2);
							if (diff < minDiff)
							{
								minDiff = diff;
								bestSplit = si;
							}
						}
					}
					if (bestSplit > 0)
					{
						result[0] = string.Join(" ", words.Take(bestSplit));
						result[1] = string.Join(" ", words.Skip(bestSplit));
					}
				}
			}

			return result;
		}
	}
}
