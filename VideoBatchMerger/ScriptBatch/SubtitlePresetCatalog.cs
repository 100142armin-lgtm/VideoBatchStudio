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
		public string Category { get; set; } // "全部", "底框样式", "基础字幕", "卡拉OK高亮", "特殊动效"
		public string TagText { get; set; }

		public string PrimaryColorHex { get; set; }   // ASS BGR format
		public string OutlineColorHex { get; set; }
		public string BackColorHex { get; set; }
		public int BorderStyle { get; set; } = 1;      // 1: 描边阴影, 3: 不透明底框 (Box)
		public float OutlineWidth { get; set; } = 3.5f;
		public int ShadowDepth { get; set; } = 2;
		public int FontSize { get; set; } = 48;
		public int DisplayMode { get; set; } = 0;      // 0: 句级自然居中, 1: 逐词卡拉OK高亮, 2: 动感回弹弹出, 3: 逐词红底框高亮

		// GDI+ 预览绘制颜色辅助
		public Color PreviewTextColor { get; set; }
		public Color PreviewStrokeColor { get; set; }
		public Color PreviewBoxColor { get; set; } = Color.Transparent;
		public Color PreviewHighlightBgColor { get; set; } = Color.FromArgb(208, 0, 0);
		public Color PreviewHighlightTextColor { get; set; } = Color.FromArgb(255, 255, 0);
		public bool HasBox => BorderStyle == 3 && PreviewBoxColor != Color.Transparent;
	}

	public static class SubtitlePresetCatalog
	{
		public static readonly List<SubtitlePreset> Presets = new List<SubtitlePreset>
		{
			// ==========================================
			// 1. 底框样式 (2024 短视频爆款自适应底框)
			// ==========================================
			new SubtitlePreset
			{
				Id = "box_red_badge",
				Name = "经典红底框_黄字白文 (爆款神父)",
				Category = "底框样式",
				TagText = "爆款推荐 · 词级红框",
				PrimaryColorHex = "&H00FFFFFF", // 基础白字
				OutlineColorHex = "&H00000000", // 立体黑边
				BackColorHex = "&H000000D0",    // ASS BGR: #D00000 (红底框)
				BorderStyle = 1,
				OutlineWidth = 7.5f,
				ShadowDepth = 2,
				FontSize = 86,
				DisplayMode = 3, // 3: 逐词红底框高亮
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.Black,
				PreviewHighlightBgColor = Color.FromArgb(208, 0, 0),
				PreviewHighlightTextColor = Color.FromArgb(255, 255, 0)
			},
			new SubtitlePreset
			{
				Id = "box_caramel_orange",
				Name = "焦糖橙_黄高亮",
				Category = "底框样式",
				TagText = "底框样式 · 悬浮播放",
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
				TagText = "底框样式 · 科技高级",
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
				Id = "box_neon_green_black",
				Name = "荧光绿底框_黑字",
				Category = "底框样式",
				TagText = "底框样式 · 极度吸睛",
				PrimaryColorHex = "&H00000000", // 纯黑字
				OutlineColorHex = "&H0032FF00", // 荧光绿 (BGR: 00FF32)
				BackColorHex = "&H0032FF00",
				BorderStyle = 3,
				OutlineWidth = 9f,
				ShadowDepth = 0,
				FontSize = 86,
				PreviewTextColor = Color.Black,
				PreviewStrokeColor = Color.FromArgb(0, 255, 50),
				PreviewBoxColor = Color.FromArgb(240, 0, 255, 50)
			},
			new SubtitlePreset
			{
				Id = "box_card_news",
				Name = "黑底白字 (新闻)",
				Category = "底框样式",
				TagText = "底框样式 · 权威纪实",
				PrimaryColorHex = "&H00FFFFFF", // 白字
				OutlineColorHex = "&H00181818", // 纯黑底框
				BackColorHex = "&H00181818",
				BorderStyle = 3,
				OutlineWidth = 8f,
				ShadowDepth = 0,
				FontSize = 82,
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.FromArgb(24, 24, 24),
				PreviewBoxColor = Color.FromArgb(220, 24, 24, 24)
			},
			new SubtitlePreset
			{
				Id = "box_neon_pink_blue",
				Name = "霓虹蓝粉渐变框",
				Category = "底框样式",
				TagText = "底框样式 · 潮流时尚",
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
				TagText = "底框样式 · 沉稳神秘",
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
			// 2. 基础字幕 (经典极简 · 百搭耐看)
			// ==========================================
			new SubtitlePreset
			{
				Id = "viral_yellow",
				Name = "黄字黑边 (经典爆款)",
				Category = "基础字幕",
				TagText = "基础字幕 · 流量标配",
				PrimaryColorHex = "&H0000F5FF", // 醒目黄
				OutlineColorHex = "&H00000000", // 纯黑边
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 5.5f,
				ShadowDepth = 2,
				FontSize = 86,
				PreviewTextColor = Color.FromArgb(255, 245, 0),
				PreviewStrokeColor = Color.Black
			},
			new SubtitlePreset
			{
				Id = "white_stroke",
				Name = "默认白字 (立体黑边)",
				Category = "基础字幕",
				TagText = "基础字幕 · 极简百搭",
				PrimaryColorHex = "&H00FFFFFF", // 纯白
				OutlineColorHex = "&H00000000",
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 4.5f,
				ShadowDepth = 2,
				FontSize = 78,
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.Black
			},
			new SubtitlePreset
			{
				Id = "mint_green",
				Name = "荧光薄荷绿 (清新黑边)",
				Category = "基础字幕",
				TagText = "基础字幕 · 活力清新",
				PrimaryColorHex = "&H0070FF80", // 翠绿
				OutlineColorHex = "&H00000000",
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 4.8f,
				ShadowDepth = 2,
				FontSize = 82,
				PreviewTextColor = Color.FromArgb(128, 255, 112),
				PreviewStrokeColor = Color.Black
			},
			new SubtitlePreset
			{
				Id = "luxury_gold",
				Name = "重金大字 (强对比排版)",
				Category = "基础字幕",
				TagText = "基础字幕 · 庄重大气",
				PrimaryColorHex = "&H0050D0FF", // 香槟金
				OutlineColorHex = "&H00000000",
				BackColorHex = "&HA0000000",
				BorderStyle = 1,
				OutlineWidth = 6.0f,
				ShadowDepth = 3,
				FontSize = 90,
				PreviewTextColor = Color.FromArgb(255, 208, 80),
				PreviewStrokeColor = Color.Black
			},
			new SubtitlePreset
			{
				Id = "cyber_cyan",
				Name = "极简纯文+电光青高亮",
				Category = "基础字幕",
				TagText = "基础字幕 · 科技未来",
				PrimaryColorHex = "&H00FFFF00", // 电光青 (BGR: 00FFFF)
				OutlineColorHex = "&H00000000",
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 4.8f,
				ShadowDepth = 2,
				FontSize = 84,
				PreviewTextColor = Color.FromArgb(0, 255, 255),
				PreviewStrokeColor = Color.Black
			},

			// ==========================================
			// 3. 卡拉OK高亮 (逐词律动 · 抓眼球)
			// ==========================================
			new SubtitlePreset
			{
				Id = "hormozi_style",
				Name = "Hormozi 风格字幕",
				Category = "卡拉OK高亮",
				TagText = "卡拉OK高亮 · 海外千万播放",
				PrimaryColorHex = "&H0000F2FF", // 鲜明黄
				OutlineColorHex = "&H00000000",
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 7.0f,
				ShadowDepth = 2,
				FontSize = 94,
				DisplayMode = 1, // 逐词高亮跳动
				PreviewTextColor = Color.FromArgb(255, 242, 0),
				PreviewStrokeColor = Color.Black
			},
			new SubtitlePreset
			{
				Id = "karaoke_blue",
				Name = "黑边_电蓝高亮",
				Category = "卡拉OK高亮",
				TagText = "卡拉OK高亮 · 悬浮播放",
				PrimaryColorHex = "&H00FFA020", // 电蓝 (BGR: 20A0FF)
				OutlineColorHex = "&H00000000",
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 5.2f,
				ShadowDepth = 2,
				FontSize = 86,
				DisplayMode = 1,
				PreviewTextColor = Color.FromArgb(32, 160, 255),
				PreviewStrokeColor = Color.Black
			},
			new SubtitlePreset
			{
				Id = "karaoke_pink",
				Name = "黑边_粉红高亮",
				Category = "卡拉OK高亮",
				TagText = "卡拉OK高亮 · 悬浮播放",
				PrimaryColorHex = "&H00C040FF", // 粉红 (BGR: FF40C0)
				OutlineColorHex = "&H00000000",
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 5.2f,
				ShadowDepth = 2,
				FontSize = 86,
				DisplayMode = 1,
				PreviewTextColor = Color.FromArgb(255, 64, 192),
				PreviewStrokeColor = Color.Black
			},
			new SubtitlePreset
			{
				Id = "karaoke_mint",
				Name = "黑边_薄荷高亮",
				Category = "卡拉OK高亮",
				TagText = "卡拉OK高亮 · 悬浮播放",
				PrimaryColorHex = "&H00A0FF70", // 薄荷
				OutlineColorHex = "&H00000000",
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 5.2f,
				ShadowDepth = 2,
				FontSize = 86,
				DisplayMode = 1,
				PreviewTextColor = Color.FromArgb(112, 255, 160),
				PreviewStrokeColor = Color.Black
			},

			// ==========================================
			// 4. 特殊动效 (缩放弹出 · 逐字动画)
			// ==========================================
			new SubtitlePreset
			{
				Id = "pop_bounce",
				Name = "逐词弹出 (动感回弹)",
				Category = "特殊动效",
				TagText = "逐词动态 · 悬浮播放",
				PrimaryColorHex = "&H0000F5FF",
				OutlineColorHex = "&H00000000",
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 5.5f,
				ShadowDepth = 2,
				FontSize = 86,
				DisplayMode = 2, // 动感弹出
				PreviewTextColor = Color.FromArgb(255, 245, 0),
				PreviewStrokeColor = Color.Black
			},
			new SubtitlePreset
			{
				Id = "typewriter",
				Name = "打字机模式",
				Category = "特殊动效",
				TagText = "特殊动画 · 悬浮播放",
				PrimaryColorHex = "&H00FFFFFF",
				OutlineColorHex = "&H00000000",
				BackColorHex = "&H80000000",
				BorderStyle = 1,
				OutlineWidth = 4.8f,
				ShadowDepth = 2,
				FontSize = 82,
				DisplayMode = 1,
				PreviewTextColor = Color.White,
				PreviewStrokeColor = Color.Black
			}
		};

		public static SubtitlePreset GetById(string id)
		{
			return Presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Presets[0];
		}
	}
}
