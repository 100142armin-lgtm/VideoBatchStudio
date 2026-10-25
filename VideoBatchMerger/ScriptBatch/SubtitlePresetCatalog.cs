using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;

namespace VideoBatchMerger.ScriptBatch
{
	public sealed class FontOptionItem
	{
		public string FontFamily { get; set; }
		public string DisplayName { get; set; }

		public FontOptionItem(string fontFamily, string displayName)
		{
			FontFamily = fontFamily;
			DisplayName = displayName;
		}

		public override string ToString() => DisplayName;
	}

	public sealed class SubtitlePreset
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string Category { get; set; } // "爆款推荐", "Canva花字", "纯净立体", "底框样式", "特殊动效"
		public string TagText { get; set; }

		public string FontFamily { get; set; } = "Arial Rounded MT Bold";
		public int FontSize { get; set; } = 86;
		public int DisplayMode { get; set; } = 0;      // 0: 句级自然居中, 1: 逐词卡拉OK高亮, 2: 动感回弹弹出, 3: 逐词底框高亮, 4: 随语音逐行上滚
		public int BorderStyle { get; set; } = 1;      // 1: 描边阴影/立体, 3: 整句胶囊底框 (Box)

		public string PrimaryColorHex { get; set; } = "#FFFFFF";
		public int TextOpacity { get; set; } = 100;
		public string OutlineColorHex { get; set; } = "#0A1172";
		public float OutlineWidth { get; set; } = 6.0f;
		public string ShadowColorHex { get; set; } = "#050838";
		public float ShadowDepth { get; set; } = 3.0f;
		public int GlowBlur { get; set; } = 0;

		public string HighlightTextColorHex { get; set; } = "#FFFF00";
		public bool HighlightBoxEnabled { get; set; } = true;
		public string HighlightBoxColorHex { get; set; } = "#D60000";
		public int HighlightBoxOpacity { get; set; } = 96;

		public bool ScrollPanelEnabled { get; set; } = true;
		public string ScrollPanelColorHex { get; set; } = "#EAE6DF";
		public int ScrollPanelOpacity { get; set; } = 62;

		public string BackColorHex { get; set; } = "#050838";

		// GDI+ 预览绘制颜色辅助 (兼容动态读取)
		public Color PreviewTextColor
		{
			get => Color.FromArgb(Math.Max(0, Math.Min(255, (int)Math.Round(TextOpacity * 2.55))),
				ScriptBatchSettings.ParseToColor(PrimaryColorHex, Color.White));
			set => PrimaryColorHex = ScriptBatchSettings.ToHtmlHex(value);
		}

		public Color PreviewStrokeColor
		{
			get => OutlineWidth <= 0.05f ? Color.Transparent : ScriptBatchSettings.ParseToColor(OutlineColorHex, Color.Transparent);
			set => OutlineColorHex = ScriptBatchSettings.ToHtmlHex(value);
		}

		public Color PreviewShadowColor
		{
			get => ShadowDepth <= 0.05f && GlowBlur <= 0 ? Color.Transparent : ScriptBatchSettings.ParseToColor(ShadowColorHex, Color.FromArgb(128, 64, 16));
			set => ShadowColorHex = ScriptBatchSettings.ToHtmlHex(value);
		}

		public Color PreviewBoxColor
		{
			get => BorderStyle == 3
				? Color.FromArgb(Math.Max(30, Math.Min(255, (int)Math.Round(ScrollPanelOpacity * 2.55))), ScriptBatchSettings.ParseToColor(ScrollPanelColorHex, Color.FromArgb(212, 85, 0)))
				: Color.Transparent;
			set
			{
				ScrollPanelColorHex = ScriptBatchSettings.ToHtmlHex(value);
				ScrollPanelOpacity = (int)Math.Round(value.A / 2.55);
			}
		}

		public Color PreviewHighlightBgColor
		{
			get => ScriptBatchSettings.ParseToColor(HighlightBoxColorHex, Color.FromArgb(214, 0, 0));
			set => HighlightBoxColorHex = ScriptBatchSettings.ToHtmlHex(value);
		}

		public Color PreviewHighlightTextColor
		{
			get => Color.FromArgb(Math.Max(0, Math.Min(255, (int)Math.Round(TextOpacity * 2.55))),
				ScriptBatchSettings.ParseToColor(HighlightTextColorHex, Color.FromArgb(255, 255, 0)));
			set => HighlightTextColorHex = ScriptBatchSettings.ToHtmlHex(value);
		}

		public bool HasBox => BorderStyle == 3;
		public bool IsPure3D => BorderStyle == 1 && ShadowDepth >= 2f;

		public void ApplyToSettings(ScriptBatchSettings settings)
		{
			if (settings == null) return;
			settings.SubtitleStyleId = Id;
			settings.SubtitleFontFamily = SubtitlePresetCatalog.ResolveInstalledFont(FontFamily);
			settings.SubtitleFontSize = FontSize;
			settings.SubtitleDisplayMode = DisplayMode;
			settings.SubtitleBorderStyle = BorderStyle;

			settings.SubtitleColorHex = ScriptBatchSettings.NormalizeToHtmlHex(PrimaryColorHex, Color.White);
			settings.SubtitleTextOpacity = TextOpacity > 0 ? TextOpacity : 100;
			settings.SubtitleStrokeColorHex = ScriptBatchSettings.NormalizeToHtmlHex(OutlineColorHex, Color.FromArgb(10, 17, 114));
			settings.SubtitleStrokeWidth = OutlineWidth;
			settings.SubtitleShadowColorHex = ScriptBatchSettings.NormalizeToHtmlHex(ShadowColorHex, Color.FromArgb(5, 8, 56));
			settings.SubtitleShadowDepth = ShadowDepth;
			settings.SubtitleGlowBlur = GlowBlur;

			settings.SubtitleHighlightTextColorHex = ScriptBatchSettings.NormalizeToHtmlHex(HighlightTextColorHex, Color.Yellow);
			settings.SubtitleHighlightBoxEnabled = HighlightBoxEnabled;
			settings.SubtitleHighlightBoxColorHex = ScriptBatchSettings.NormalizeToHtmlHex(HighlightBoxColorHex, Color.FromArgb(214, 0, 0));
			settings.SubtitleHighlightBoxOpacity = HighlightBoxOpacity > 0 ? HighlightBoxOpacity : 96;

			settings.SubtitleScrollPanelEnabled = ScrollPanelEnabled;
			settings.SubtitleScrollPanelColorHex = ScriptBatchSettings.NormalizeToHtmlHex(ScrollPanelColorHex, Color.FromArgb(234, 230, 223));
			settings.SubtitleScrollPanelOpacity = ScrollPanelOpacity > 0 ? ScrollPanelOpacity : 62;
		}
	}

	public static class SubtitlePresetCatalog
	{
		private static HashSet<string> s_installedFontNames;

		public static readonly List<FontOptionItem> CuratedFonts = new List<FontOptionItem>
		{
			new FontOptionItem("Arial Rounded MT Bold", "Arial Rounded MT Bold (爆款圆润粗体)"),
			new FontOptionItem("Arial Black", "Arial Black (超粗醒目黑体)"),
			new FontOptionItem("Impact", "Impact (美式短视频冲击体)"),
			new FontOptionItem("Segoe UI Black", "Segoe UI Black (现代几何重黑)"),
			new FontOptionItem("Cooper Black", "Cooper Black (复古圆润波普)"),
			new FontOptionItem("Forte", "Forte (Canva 街头涂鸦手写)"),
			new FontOptionItem("Showcard Gothic", "Showcard Gothic (Canva 摇滚波普体)"),
			new FontOptionItem("Snap ITC", "Snap ITC (Canva 奇趣综艺花字)"),
			new FontOptionItem("Georgia", "Georgia (典雅西方经文衬线)"),
			new FontOptionItem("Bodoni MT Black", "Bodoni MT Black (时尚高定粗衬线)"),
			new FontOptionItem("Comic Sans MS", "Comic Sans MS (轻松亲和手写)"),
			new FontOptionItem("Trebuchet MS", "Trebuchet MS (清晰人文无衬线)"),
			new FontOptionItem("Verdana", "Verdana (宽体高辨识度)"),
			new FontOptionItem("Tahoma", "Tahoma (紧凑有力无衬线)"),
			new FontOptionItem("Arial", "Arial (经典通用无衬线)"),
			new FontOptionItem("Microsoft YaHei UI", "微软雅黑 (中英双语高清)")
		};

		public static HashSet<string> GetInstalledFontNames()
		{
			if (s_installedFontNames != null) return s_installedFontNames;
			var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				using var col = new InstalledFontCollection();
				foreach (var ff in col.Families)
				{
					if (!string.IsNullOrWhiteSpace(ff.Name))
						set.Add(ff.Name);
				}
			}
			catch { }
			s_installedFontNames = set;
			return s_installedFontNames;
		}

		public static string ResolveInstalledFont(string preferred)
		{
			if (string.IsNullOrWhiteSpace(preferred)) return "Arial";
			var installed = GetInstalledFontNames();
			if (installed.Contains(preferred)) return preferred;
			if (installed.Contains("Arial Rounded MT Bold")) return "Arial Rounded MT Bold";
			if (installed.Contains("Arial Black")) return "Arial Black";
			return "Arial";
		}

		public static readonly List<SubtitlePreset> Presets = new List<SubtitlePreset>
		{
			// ==========================================
			// 1. 爆款推荐 (1:1 复刻海外葡语/宗教/短视频爆款截图)
			// ==========================================
			new SubtitlePreset
			{
				Id = "viral_blue_red_yellow",
				Name = "皇室深蓝·红框黄字",
				Category = "爆款推荐",
				TagText = "爆款图1 · 粗蓝边红框黄字",
				FontFamily = "Arial Rounded MT Bold",
				FontSize = 90,
				DisplayMode = 3,
				BorderStyle = 1,
				PrimaryColorHex = "#FFFFFF",
				TextOpacity = 100,
				OutlineColorHex = "#0A1172",
				OutlineWidth = 6.5f,
				ShadowColorHex = "#050838",
				ShadowDepth = 3.0f,
				GlowBlur = 0,
				HighlightTextColorHex = "#FFFF00",
				HighlightBoxEnabled = true,
				HighlightBoxColorHex = "#D60000",
				HighlightBoxOpacity = 96,
				ScrollPanelEnabled = false,
				ScrollPanelColorHex = "#202020",
				ScrollPanelOpacity = 60
			},
			new SubtitlePreset
			{
				Id = "viral_brown_red_green",
				Name = "暖白咖啡·红框翠绿",
				Category = "爆款推荐",
				TagText = "爆款图2 · 浓咖边红框绿字",
				FontFamily = "Arial Rounded MT Bold",
				FontSize = 90,
				DisplayMode = 3,
				BorderStyle = 1,
				PrimaryColorHex = "#FFF3EB",
				TextOpacity = 100,
				OutlineColorHex = "#2B160E",
				OutlineWidth = 6.5f,
				ShadowColorHex = "#1A0C06",
				ShadowDepth = 3.0f,
				GlowBlur = 0,
				HighlightTextColorHex = "#00FF33",
				HighlightBoxEnabled = true,
				HighlightBoxColorHex = "#D60000",
				HighlightBoxOpacity = 96,
				ScrollPanelEnabled = false,
				ScrollPanelColorHex = "#2B160E",
				ScrollPanelOpacity = 60
			},
			new SubtitlePreset
			{
				Id = "viral_brazil_yellow_green",
				Name = "葡语爆款·黄框翠绿",
				Category = "爆款推荐",
				TagText = "爆款图6 · 深蓝边黄框绿字",
				FontFamily = "Arial Rounded MT Bold",
				FontSize = 90,
				DisplayMode = 3,
				BorderStyle = 1,
				PrimaryColorHex = "#FFFFFF",
				TextOpacity = 100,
				OutlineColorHex = "#0B1054",
				OutlineWidth = 6.5f,
				ShadowColorHex = "#05072A",
				ShadowDepth = 3.0f,
				GlowBlur = 0,
				HighlightTextColorHex = "#00E626",
				HighlightBoxEnabled = true,
				HighlightBoxColorHex = "#FFEE00",
				HighlightBoxOpacity = 96,
				ScrollPanelEnabled = false,
				ScrollPanelColorHex = "#0B1054",
				ScrollPanelOpacity = 60
			},
			new SubtitlePreset
			{
				Id = "viral_scroll_brown_frosted",
				Name = "暖棕立体·磨砂白底板",
				Category = "爆款推荐",
				TagText = "爆款图4 · 棕边白字磨砂底上滚",
				FontFamily = "Arial Rounded MT Bold",
				FontSize = 76,
				DisplayMode = 4,
				BorderStyle = 1,
				PrimaryColorHex = "#FFFFFF",
				TextOpacity = 100,
				OutlineColorHex = "#5A2D0C",
				OutlineWidth = 5.5f,
				ShadowColorHex = "#2E1505",
				ShadowDepth = 2.5f,
				GlowBlur = 0,
				HighlightTextColorHex = "#FFD700",
				HighlightBoxEnabled = false,
				HighlightBoxColorHex = "#5A2D0C",
				HighlightBoxOpacity = 90,
				ScrollPanelEnabled = true,
				ScrollPanelColorHex = "#EAE6DF",
				ScrollPanelOpacity = 62
			},
			new SubtitlePreset
			{
				Id = "viral_scroll_red_white",
				Name = "圣洁红字·纯白厚边",
				Category = "爆款推荐",
				TagText = "爆款图5 · 鲜红字纯白厚边上滚",
				FontFamily = "Arial Rounded MT Bold",
				FontSize = 78,
				DisplayMode = 4,
				BorderStyle = 1,
				PrimaryColorHex = "#EA001E",
				TextOpacity = 100,
				OutlineColorHex = "#FFFFFF",
				OutlineWidth = 6.0f,
				ShadowColorHex = "#400008",
				ShadowDepth = 2.0f,
				GlowBlur = 0,
				HighlightTextColorHex = "#FFFF00",
				HighlightBoxEnabled = false,
				HighlightBoxColorHex = "#FFFFFF",
				HighlightBoxOpacity = 95,
				ScrollPanelEnabled = false,
				ScrollPanelColorHex = "#FFFFFF",
				ScrollPanelOpacity = 55
			},
			new SubtitlePreset
			{
				Id = "viral_impact_yellow_glow",
				Name = "冲击粗黑·黄白外发光",
				Category = "爆款推荐",
				TagText = "爆款图7 · Impact黄白双色发光",
				FontFamily = "Impact",
				FontSize = 94,
				DisplayMode = 1,
				BorderStyle = 1,
				PrimaryColorHex = "#FFFFFF",
				TextOpacity = 100,
				OutlineColorHex = "#1C1408",
				OutlineWidth = 4.5f,
				ShadowColorHex = "#FFD700",
				ShadowDepth = 2.5f,
				GlowBlur = 6,
				HighlightTextColorHex = "#FFEE00",
				HighlightBoxEnabled = false,
				HighlightBoxColorHex = "#D60000",
				HighlightBoxOpacity = 95,
				ScrollPanelEnabled = false,
				ScrollPanelColorHex = "#1C1408",
				ScrollPanelOpacity = 60
			},

			// ==========================================
			// 2. Canva 花字特效 (图3 Canva 爆款花字模板库)
			// ==========================================
			new SubtitlePreset
			{
				Id = "canva_custom_paint",
				Name = "Canva 街头涂鸦·活力橙",
				Category = "Canva花字",
				TagText = "Canva同款 · 涂鸦橙配纯白厚边",
				FontFamily = "Forte",
				FontSize = 92,
				DisplayMode = 2,
				BorderStyle = 1,
				PrimaryColorHex = "#FF6B1A",
				TextOpacity = 100,
				OutlineColorHex = "#FFFFFF",
				OutlineWidth = 6.0f,
				ShadowColorHex = "#802800",
				ShadowDepth = 3.5f,
				GlowBlur = 0,
				HighlightTextColorHex = "#FFEE00",
				HighlightBoxEnabled = false,
				HighlightBoxColorHex = "#FF6B1A",
				HighlightBoxOpacity = 95
			},
			new SubtitlePreset
			{
				Id = "canva_rock_pop",
				Name = "Canva 摇滚波普·冰川青",
				Category = "Canva花字",
				TagText = "Canva同款 · 波普白字冰川青边",
				FontFamily = "Showcard Gothic",
				FontSize = 88,
				DisplayMode = 2,
				BorderStyle = 1,
				PrimaryColorHex = "#FFFFFF",
				TextOpacity = 100,
				OutlineColorHex = "#00A8C6",
				OutlineWidth = 6.0f,
				ShadowColorHex = "#004D5E",
				ShadowDepth = 4.0f,
				GlowBlur = 0,
				HighlightTextColorHex = "#FFEE00",
				HighlightBoxEnabled = false,
				HighlightBoxColorHex = "#00A8C6",
				HighlightBoxOpacity = 95
			},
			new SubtitlePreset
			{
				Id = "canva_static_neon",
				Name = "Canva 暗夜胶囊·荧光绿",
				Category = "Canva花字",
				TagText = "Canva同款 · 暗夜胶囊底框荧光绿",
				FontFamily = "Arial Black",
				FontSize = 84,
				DisplayMode = 0,
				BorderStyle = 3,
				PrimaryColorHex = "#C6FF00",
				TextOpacity = 100,
				OutlineColorHex = "#12161F",
				OutlineWidth = 0f,
				ShadowColorHex = "#12161F",
				ShadowDepth = 0f,
				GlowBlur = 0,
				HighlightTextColorHex = "#FFFFFF",
				HighlightBoxEnabled = true,
				HighlightBoxColorHex = "#12161F",
				HighlightBoxOpacity = 94,
				ScrollPanelEnabled = true,
				ScrollPanelColorHex = "#12161F",
				ScrollPanelOpacity = 94
			},
			new SubtitlePreset
			{
				Id = "canva_spooky_lime",
				Name = "Canva 奇趣波普·撞色橙绿",
				Category = "Canva花字",
				TagText = "Canva同款 · 奇趣字体橙红配荧光绿",
				FontFamily = "Snap ITC",
				FontSize = 86,
				DisplayMode = 2,
				BorderStyle = 1,
				PrimaryColorHex = "#FF4500",
				TextOpacity = 100,
				OutlineColorHex = "#CCFF00",
				OutlineWidth = 5.5f,
				ShadowColorHex = "#2D3800",
				ShadowDepth = 3.5f,
				GlowBlur = 0,
				HighlightTextColorHex = "#FFFF00",
				HighlightBoxEnabled = false,
				HighlightBoxColorHex = "#2D3800",
				HighlightBoxOpacity = 95
			},
			new SubtitlePreset
			{
				Id = "canva_golden_serif",
				Name = "Canva 典雅衬线·复古流金",
				Category = "Canva花字",
				TagText = "Canva同款 · 经典衬线厚重金棕边",
				FontFamily = "Georgia",
				FontSize = 88,
				DisplayMode = 0,
				BorderStyle = 1,
				PrimaryColorHex = "#FFFDF5",
				TextOpacity = 100,
				OutlineColorHex = "#5C3A08",
				OutlineWidth = 5.0f,
				ShadowColorHex = "#2B1800",
				ShadowDepth = 3.5f,
				GlowBlur = 0,
				HighlightTextColorHex = "#FFD700",
				HighlightBoxEnabled = false,
				HighlightBoxColorHex = "#881818",
				HighlightBoxOpacity = 95
			},

			// ==========================================
			// 3. 纯净立体 (专为信仰赛道定制 · 圣光与浮雕质感)
			// ==========================================
			new SubtitlePreset
			{
				Id = "sacred_warm_gold",
				Name = "晨曦圣光·暖金立体",
				Category = "纯净立体",
				TagText = "纯净立体 · 暖金圣光",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFFFFF",
				OutlineColorHex = "#955015",
				ShadowColorHex = "#804010",
				BorderStyle = 1,
				OutlineWidth = 2.0f,
				ShadowDepth = 4.0f,
				FontSize = 86,
				DisplayMode = 0,
				HighlightTextColorHex = "#FFD700",
				HighlightBoxColorHex = "#881818"
			},
			new SubtitlePreset
			{
				Id = "sacred_embossed_white",
				Name = "圣洁经文·无边浮雕",
				Category = "纯净立体",
				TagText = "纯净立体 · 圣洁浮雕",
				FontFamily = "Georgia",
				PrimaryColorHex = "#FFFDF8",
				OutlineColorHex = "#FFFDF8",
				ShadowColorHex = "#653510",
				BorderStyle = 1,
				OutlineWidth = 0f,
				ShadowDepth = 4.5f,
				FontSize = 86,
				DisplayMode = 0,
				HighlightTextColorHex = "#FFD700",
				HighlightBoxColorHex = "#881818"
			},
			new SubtitlePreset
			{
				Id = "box_royal_crimson",
				Name = "皇家救赎·酒红与金",
				Category = "纯净立体",
				TagText = "纯净立体 · 皇家酒红金",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFFFFF",
				OutlineColorHex = "#955015",
				ShadowColorHex = "#804010",
				HighlightTextColorHex = "#FFD700",
				HighlightBoxEnabled = true,
				HighlightBoxColorHex = "#881818",
				HighlightBoxOpacity = 96,
				BorderStyle = 1,
				OutlineWidth = 2.5f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 3
			},
			new SubtitlePreset
			{
				Id = "box_red_badge",
				Name = "经典红底框·立体纯白",
				Category = "纯净立体",
				TagText = "纯净立体 · 词级红框",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFFFFF",
				OutlineColorHex = "#955015",
				ShadowColorHex = "#804010",
				HighlightTextColorHex = "#FFFF00",
				HighlightBoxEnabled = true,
				HighlightBoxColorHex = "#D00000",
				HighlightBoxOpacity = 96,
				BorderStyle = 1,
				OutlineWidth = 2.5f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 3
			},
			new SubtitlePreset
			{
				Id = "sacred_soft_glow",
				Name = "天使晨光·柔光金辉",
				Category = "纯净立体",
				TagText = "纯净立体 · 晨曦金辉",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFF48D",
				OutlineColorHex = "#A05000",
				ShadowColorHex = "#602000",
				BorderStyle = 1,
				OutlineWidth = 2.0f,
				ShadowDepth = 3.8f,
				GlowBlur = 4,
				FontSize = 86,
				DisplayMode = 0,
				HighlightTextColorHex = "#FFFFFF"
			},
			new SubtitlePreset
			{
				Id = "sacred_ethereal_sky",
				Name = "柔光天籁·晨雾淡蓝",
				Category = "纯净立体",
				TagText = "纯净立体 · 天籁空灵",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFFFFF",
				OutlineColorHex = "#4090D0",
				ShadowColorHex = "#205080",
				BorderStyle = 1,
				OutlineWidth = 2.5f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 0,
				HighlightTextColorHex = "#FFEE00"
			},

			// ==========================================
			// 4. 底框样式 (胶囊底框样式)
			// ==========================================
			new SubtitlePreset
			{
				Id = "box_caramel_orange",
				Name = "焦糖橙_黄高亮",
				Category = "底框样式",
				TagText = "底框样式 · 温暖悬浮",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFF500",
				OutlineColorHex = "#D45500",
				ScrollPanelColorHex = "#D45500",
				ScrollPanelOpacity = 90,
				ScrollPanelEnabled = true,
				BorderStyle = 3,
				OutlineWidth = 0f,
				ShadowDepth = 0f,
				FontSize = 86,
				DisplayMode = 0
			},
			new SubtitlePreset
			{
				Id = "box_ocean_blue",
				Name = "深海蓝_黄高亮",
				Category = "底框样式",
				TagText = "底框样式 · 宁静深海",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFF200",
				OutlineColorHex = "#0040C0",
				ScrollPanelColorHex = "#0040C0",
				ScrollPanelOpacity = 92,
				ScrollPanelEnabled = true,
				BorderStyle = 3,
				OutlineWidth = 0f,
				ShadowDepth = 0f,
				FontSize = 86,
				DisplayMode = 0
			},
			new SubtitlePreset
			{
				Id = "box_neon_pink_blue",
				Name = "霓虹紫红胶囊框",
				Category = "底框样式",
				TagText = "底框样式 · 唯美氛围",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFFFFF",
				OutlineColorHex = "#8000C0",
				ScrollPanelColorHex = "#8000C0",
				ScrollPanelOpacity = 90,
				ScrollPanelEnabled = true,
				BorderStyle = 3,
				OutlineWidth = 0f,
				ShadowDepth = 0f,
				FontSize = 86,
				DisplayMode = 0
			},
			new SubtitlePreset
			{
				Id = "box_dark_purple",
				Name = "暗夜紫_黄高亮",
				Category = "底框样式",
				TagText = "底框样式 · 庄重雅致",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFF000",
				OutlineColorHex = "#300060",
				ScrollPanelColorHex = "#300060",
				ScrollPanelOpacity = 90,
				ScrollPanelEnabled = true,
				BorderStyle = 3,
				OutlineWidth = 0f,
				ShadowDepth = 0f,
				FontSize = 86,
				DisplayMode = 0
			},

			// ==========================================
			// 5. 特殊动效与卡拉OK律动
			// ==========================================
			new SubtitlePreset
			{
				Id = "karaoke_sacred_gold",
				Name = "晨曦金光·逐词流动",
				Category = "特殊动效",
				TagText = "卡拉OK高亮 · 金光律动",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFFFFF",
				OutlineColorHex = "#955015",
				ShadowColorHex = "#804010",
				HighlightTextColorHex = "#FFD700",
				HighlightBoxEnabled = false,
				BorderStyle = 1,
				OutlineWidth = 3.0f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 1
			},
			new SubtitlePreset
			{
				Id = "pop_bounce_sacred",
				Name = "圣光跃动·立体弹出",
				Category = "特殊动效",
				TagText = "动态弹出 · 纯净立体",
				FontFamily = "Arial Rounded MT Bold",
				PrimaryColorHex = "#FFFFFF",
				OutlineColorHex = "#955015",
				ShadowColorHex = "#804010",
				HighlightTextColorHex = "#FFD700",
				BorderStyle = 1,
				OutlineWidth = 3.0f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 2
			},
			new SubtitlePreset
			{
				Id = "bright_phrase_gold",
				Name = "明亮分句·圣洁金白",
				Category = "特殊动效",
				TagText = "随语音分句 · 暖金重点词",
				FontFamily = "Arial Black",
				FontSize = 86,
				DisplayMode = 5,
				PrimaryColorHex = "#FFFFFF",
				HighlightTextColorHex = "#FFD45B",
				OutlineColorHex = "#92734B",
				OutlineWidth = 2.5f,
				ShadowColorHex = "#C6A578",
				ShadowDepth = 1.5f,
				HighlightBoxEnabled = false
			},
			new SubtitlePreset
			{
				Id = "bright_phrase_sky",
				Name = "明亮分句·晴空蓝白",
				Category = "特殊动效",
				TagText = "随语音分句 · 天蓝重点词",
				FontFamily = "Arial Black",
				FontSize = 86,
				DisplayMode = 5,
				PrimaryColorHex = "#FFFFFF",
				HighlightTextColorHex = "#48BDE7",
				OutlineColorHex = "#577D9A",
				OutlineWidth = 2.5f,
				ShadowColorHex = "#A3C8D8",
				ShadowDepth = 1.5f,
				HighlightBoxEnabled = false
			},
			new SubtitlePreset
			{
				Id = "bright_phrase_rose",
				Name = "明亮分句·柔和玫瑰",
				Category = "特殊动效",
				TagText = "随语音分句 · 玫瑰金重点词",
				FontFamily = "Arial Black",
				FontSize = 86,
				DisplayMode = 5,
				PrimaryColorHex = "#FFFDF8",
				HighlightTextColorHex = "#E8A381",
				OutlineColorHex = "#9B7280",
				OutlineWidth = 2.5f,
				ShadowColorHex = "#D1A5AF",
				ShadowDepth = 1.5f,
				HighlightBoxEnabled = false
			},
			new SubtitlePreset
			{
				Id = "typewriter_sacred",
				Name = "经文显现·打字机立体",
				Category = "特殊动效",
				TagText = "打字机 · 圣洁文字",
				FontFamily = "Georgia",
				PrimaryColorHex = "#FFFFFF",
				OutlineColorHex = "#955015",
				ShadowColorHex = "#804010",
				HighlightTextColorHex = "#FFD700",
				BorderStyle = 1,
				OutlineWidth = 2.5f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 1
			}
		};

		public static SubtitlePreset GetById(string id)
		{
			return Presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Presets[0];
		}

		public static SubtitlePreset CreatePreviewPreset(ScriptBatchSettings settings)
		{
			var source = GetById(settings.SubtitleStyleId);
			return new SubtitlePreset
			{
				Id = source.Id,
				Name = source.Name,
				Category = source.Category,
				FontFamily = settings.SubtitleFontFamily,
				FontSize = settings.SubtitleFontSize,
				DisplayMode = settings.SubtitleDisplayMode,
				BorderStyle = settings.SubtitleBorderStyle,
				PrimaryColorHex = settings.SubtitleColorHex,
				TextOpacity = settings.SubtitleTextOpacity,
				OutlineColorHex = settings.SubtitleStrokeColorHex,
				OutlineWidth = settings.SubtitleStrokeWidth,
				ShadowColorHex = settings.SubtitleShadowColorHex,
				ShadowDepth = settings.SubtitleShadowDepth,
				GlowBlur = settings.SubtitleGlowBlur,
				HighlightTextColorHex = settings.SubtitleHighlightTextColorHex,
				HighlightBoxEnabled = settings.SubtitleHighlightBoxEnabled,
				HighlightBoxColorHex = settings.SubtitleHighlightBoxColorHex,
				HighlightBoxOpacity = settings.SubtitleHighlightBoxOpacity,
				ScrollPanelEnabled = settings.SubtitleScrollPanelEnabled,
				ScrollPanelColorHex = settings.SubtitleScrollPanelColorHex,
				ScrollPanelOpacity = settings.SubtitleScrollPanelOpacity,
				BackColorHex = source.BackColorHex
			};
		}
	}
}
