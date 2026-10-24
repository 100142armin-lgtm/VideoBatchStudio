using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace VideoBatchMerger.ScriptBatch
{
	public sealed class SubtitlePreset
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string Category { get; set; } // "全部", "纯净立体", "底框样式", "卡拉OK高亮", "特殊动效"
		public string TagText { get; set; }

		public string PrimaryColorHex { get; set; }   // ASS BGR format
		public string OutlineColorHex { get; set; }
		public string BackColorHex { get; set; }
		public string HighlightTextColorHex { get; set; } = "&H0000FFFF";
		public int BorderStyle { get; set; } = 1;      // 1: 描边阴影/立体, 3: 不透明底框 (Box)
		public float OutlineWidth { get; set; } = 1.5f;
		public float ShadowDepth { get; set; } = 3.5f;
		public int FontSize { get; set; } = 86;
		public int DisplayMode { get; set; } = 0;      // 0: 句级自然居中, 1: 逐词卡拉OK高亮, 2: 动感回弹弹出, 3: 逐词红底框高亮

		// GDI+ 预览绘制颜色辅助 (全面剔除纯黑与暗黑描边)
		public Color PreviewTextColor { get; set; } = Color.White;
		public Color PreviewStrokeColor { get; set; } = Color.Transparent;
		public Color PreviewShadowColor { get; set; } = Color.FromArgb(128, 64, 16);
		public Color PreviewBoxColor { get; set; } = Color.Transparent;
		public Color PreviewHighlightBgColor { get; set; } = Color.FromArgb(136, 24, 24);
		public Color PreviewHighlightTextColor { get; set; } = Color.FromArgb(255, 215, 0);
		public bool HasBox => BorderStyle == 3 && PreviewBoxColor != Color.Transparent;
		public bool IsPure3D => BorderStyle == 1 && ShadowDepth >= 2f;
	}

	public static class SubtitlePresetCatalog
	{
		public static readonly List<SubtitlePreset> Presets = new List<SubtitlePreset>
		{
			// ==========================================
			// 1. 纯净立体 (专为信仰赛道定制 · 0黑边 · 圣光与浮雕质感)
			// ==========================================
			new SubtitlePreset
			{
				Id = "sacred_warm_gold",
				Name = "晨曦圣光·暖金立体",
				Category = "纯净立体",
				TagText = "纯净立体 · 暖金圣光",
				PrimaryColorHex = "&H00FFFFFF", // 纯白字
				OutlineColorHex = "&H00155095", // 暖琥珀微立体缘 (ASS BGR: 955015)
				BackColorHex = "&H30104080",    // 暖金3D立体落影
				BorderStyle = 1,
				OutlineWidth = 1.5f,
				ShadowDepth = 4.0f,
				FontSize = 86,
				DisplayMode = 0,
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.FromArgb(149, 80, 21),
				PreviewShadowColor = Color.FromArgb(128, 64, 16)
			},
			new SubtitlePreset
			{
				Id = "sacred_embossed_white",
				Name = "圣洁经文·无边浮雕",
				Category = "纯净立体",
				TagText = "纯净立体 · 圣洁浮雕",
				PrimaryColorHex = "&H00F8FDFF", // 象牙圣洁白
				OutlineColorHex = "&H00F8FDFF", // 0描边，同色融入
				BackColorHex = "&H30103565",    // 浅焦糖3D微浮雕厚度
				BorderStyle = 1,
				OutlineWidth = 0f,              // 彻底无描边
				ShadowDepth = 4.5f,
				FontSize = 86,
				DisplayMode = 0,
				PreviewTextColor = Color.FromArgb(255, 253, 248),
				PreviewStrokeColor = Color.Transparent,
				PreviewShadowColor = Color.FromArgb(101, 53, 16)
			},
			new SubtitlePreset
			{
				Id = "box_royal_crimson",
				Name = "皇家救赎·酒红与金",
				Category = "纯净立体",
				TagText = "纯净立体 · 皇家酒红金",
				PrimaryColorHex = "&H00FFFFFF", // 主句纯白立体字 (无黑边)
				OutlineColorHex = "&H00155095", // 暖金微立体
				BackColorHex = "&H00181888",    // 庄严皇家酒红底框 (ASS BGR: 881818)
				HighlightTextColorHex = "&H0000D7FF", // 圣洁亮金字
				BorderStyle = 1,
				OutlineWidth = 1.2f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 3,                // 词级红底框高亮
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.FromArgb(149, 80, 21),
				PreviewShadowColor = Color.FromArgb(128, 64, 16),
				PreviewHighlightBgColor = Color.FromArgb(136, 24, 24),
				PreviewHighlightTextColor = Color.FromArgb(255, 215, 0)
			},
			new SubtitlePreset
			{
				Id = "box_red_badge",
				Name = "经典红底框·立体纯白",
				Category = "纯净立体",
				TagText = "纯净立体 · 词级红框",
				PrimaryColorHex = "&H00FFFFFF", // 基础纯白
				OutlineColorHex = "&H00155095", // 暖金立体微边缘 (彻底告别黑边)
				BackColorHex = "&H000000D0",    // 经典饱满红框
				HighlightTextColorHex = "&H0000FFFF", // 亮黄字
				BorderStyle = 1,
				OutlineWidth = 1.2f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 3,
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.FromArgb(149, 80, 21),
				PreviewShadowColor = Color.FromArgb(128, 64, 16),
				PreviewHighlightBgColor = Color.FromArgb(208, 0, 0),
				PreviewHighlightTextColor = Color.FromArgb(255, 255, 0)
			},
			new SubtitlePreset
			{
				Id = "sacred_soft_glow",
				Name = "天使晨光·柔光金辉",
				Category = "纯净立体",
				TagText = "纯净立体 · 晨曦金辉",
				PrimaryColorHex = "&H008DF4FF", // 暖阳亮金字 (ASS BGR: FFF48D)
				OutlineColorHex = "&H000050A0", // 焦糖金色微边缘
				BackColorHex = "&H30002060",    // 浓琥珀3D落影
				BorderStyle = 1,
				OutlineWidth = 1.5f,
				ShadowDepth = 3.8f,
				FontSize = 86,
				DisplayMode = 0,
				PreviewTextColor = Color.FromArgb(255, 244, 141),
				PreviewStrokeColor = Color.FromArgb(160, 80, 0),
				PreviewShadowColor = Color.FromArgb(96, 32, 0)
			},
			new SubtitlePreset
			{
				Id = "sacred_ethereal_sky",
				Name = "柔光天籁·晨雾淡蓝",
				Category = "纯净立体",
				TagText = "纯净立体 · 天籁空灵",
				PrimaryColorHex = "&H00FFFFFF", // 纯白字
				OutlineColorHex = "&H00D09040", // 天空晴蓝微边缘 (ASS BGR: 4090D0)
				BackColorHex = "&H30805020",    // 空灵蔚蓝立体落影
				BorderStyle = 1,
				OutlineWidth = 1.5f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 0,
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.FromArgb(64, 144, 208),
				PreviewShadowColor = Color.FromArgb(32, 80, 128)
			},

			// ==========================================
			// 2. 底框样式 (保留现有非黑色系底框)
			// ==========================================
			new SubtitlePreset
			{
				Id = "box_caramel_orange",
				Name = "焦糖橙_黄高亮",
				Category = "底框样式",
				TagText = "底框样式 · 温暖悬浮",
				PrimaryColorHex = "&H0000F5FF", // 黄字
				OutlineColorHex = "&H000055D4", // 橙底框
				BackColorHex = "&H000055D4",
				BorderStyle = 3,
				OutlineWidth = 8f,
				ShadowDepth = 0,
				FontSize = 86,
				PreviewTextColor = Color.FromArgb(255, 245, 0),
				PreviewStrokeColor = Color.FromArgb(212, 85, 0),
				PreviewBoxColor = Color.FromArgb(220, 212, 85, 0)
			},
			new SubtitlePreset
			{
				Id = "box_ocean_blue",
				Name = "深海蓝_黄高亮",
				Category = "底框样式",
				TagText = "底框样式 · 宁静深海",
				PrimaryColorHex = "&H0000F2FF", // 亮黄字
				OutlineColorHex = "&H00C04000", // 深海蓝框 (ASS BGR: 0040C0)
				BackColorHex = "&H00C04000",
				BorderStyle = 3,
				OutlineWidth = 8f,
				ShadowDepth = 0,
				FontSize = 86,
				PreviewTextColor = Color.FromArgb(255, 242, 0),
				PreviewStrokeColor = Color.FromArgb(0, 64, 192),
				PreviewBoxColor = Color.FromArgb(230, 0, 64, 192)
			},
			new SubtitlePreset
			{
				Id = "box_neon_pink_blue",
				Name = "霓虹蓝粉渐变框",
				Category = "底框样式",
				TagText = "底框样式 · 唯美氛围",
				PrimaryColorHex = "&H00FFFFFF",
				OutlineColorHex = "&H00C00080",
				BackColorHex = "&H00C00080",
				BorderStyle = 3,
				OutlineWidth = 8f,
				ShadowDepth = 0,
				FontSize = 86,
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.FromArgb(128, 0, 192),
				PreviewBoxColor = Color.FromArgb(225, 128, 0, 192)
			},
			new SubtitlePreset
			{
				Id = "box_dark_purple",
				Name = "暗夜紫_黄高亮",
				Category = "底框样式",
				TagText = "底框样式 · 庄重雅致",
				PrimaryColorHex = "&H0000F0FF",
				OutlineColorHex = "&H00600030",
				BackColorHex = "&H00600030",
				BorderStyle = 3,
				OutlineWidth = 8f,
				ShadowDepth = 0,
				FontSize = 86,
				PreviewTextColor = Color.FromArgb(255, 240, 0),
				PreviewStrokeColor = Color.FromArgb(48, 0, 96),
				PreviewBoxColor = Color.FromArgb(225, 48, 0, 96)
			},

			// ==========================================
			// 3. 卡拉OK高亮 (纯净立体律动)
			// ==========================================
			new SubtitlePreset
			{
				Id = "karaoke_sacred_gold",
				Name = "晨曦金光·逐词流动",
				Category = "卡拉OK高亮",
				TagText = "卡拉OK高亮 · 金光律动",
				PrimaryColorHex = "&H00FFFFFF", // 白底立体
				OutlineColorHex = "&H00155095",
				BackColorHex = "&H30104080",
				BorderStyle = 1,
				OutlineWidth = 1.5f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 1,
				PreviewTextColor = Color.FromArgb(255, 215, 0),
				PreviewStrokeColor = Color.FromArgb(149, 80, 21),
				PreviewShadowColor = Color.FromArgb(128, 64, 16)
			},

			// ==========================================
			// 4. 特殊动效 (无黑边立体动效)
			// ==========================================
			new SubtitlePreset
			{
				Id = "pop_bounce_sacred",
				Name = "圣光跃动·立体弹出",
				Category = "特殊动效",
				TagText = "动态弹出 · 纯净立体",
				PrimaryColorHex = "&H00FFFFFF",
				OutlineColorHex = "&H00155095",
				BackColorHex = "&H30104080",
				BorderStyle = 1,
				OutlineWidth = 1.5f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 2,
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.FromArgb(149, 80, 21),
				PreviewShadowColor = Color.FromArgb(128, 64, 16)
			},
			new SubtitlePreset
			{
				Id = "typewriter_sacred",
				Name = "经文显现·打字机立体",
				Category = "特殊动效",
				TagText = "打字机 · 圣洁文字",
				PrimaryColorHex = "&H00FFFFFF",
				OutlineColorHex = "&H00155095",
				BackColorHex = "&H30104080",
				BorderStyle = 1,
				OutlineWidth = 1.5f,
				ShadowDepth = 3.5f,
				FontSize = 86,
				DisplayMode = 1,
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.FromArgb(149, 80, 21),
				PreviewShadowColor = Color.FromArgb(128, 64, 16)
			}
		};

		public static SubtitlePreset GetById(string id)
		{
			return Presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Presets[0];
		}
	}
}
