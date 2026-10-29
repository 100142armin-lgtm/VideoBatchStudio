using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace VideoBatchMerger.ScriptBatch
{
	public sealed class ClonedVoiceItem
	{
		public string Name { get; set; } = string.Empty;
		public string VoiceId { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string CreatedAt { get; set; } = string.Empty;
		public string EngineType { get; set; } = "local_xtts"; // "local_xtts" 或 "elevenlabs"
		public string ReferenceAudioPath { get; set; } = string.Empty;
		public string LatentPath { get; set; } = string.Empty;
	}

	public sealed class GifStickerItem
	{
		public string Id { get; set; } = Guid.NewGuid().ToString("N");
		public string Name { get; set; } = "贴纸";
		public string FilePath { get; set; } = string.Empty;
		public bool Enabled { get; set; } = true;
		public float XPercent { get; set; } = 50f; // 0% ~ 100% 中心横向坐标 (50% 为居中)
		public float YPercent { get; set; } = 50f; // 0% ~ 100% 中心纵向坐标 (50% 为居中)
		public float ScalePercent { get; set; } = 25f; // 占画布宽度比例 (5% ~ 90%)
		public float RotationAngle { get; set; } = 0f; // 旋转角度 (-180° ~ 180°)
		public int OpacityPercent { get; set; } = 100; // 不透明度 (10% ~ 100%)
		public bool Loop { get; set; } = true; // 是否循环播放
		public double StartTimeSec { get; set; } = 0.0; // 出现起始时间 (秒，0.0 为开头即出现)
		public double DurationSec { get; set; } = 0.0; // 持续显示时间 (秒，0.0 为全程显示)
		public int MotionStyle { get; set; } = 0; // 0: 固定, 1: 向上飞, 2: 向右飞, 3: 向左飞, 4: 自定义
		public float EndXPercent { get; set; } = 50f;
		public float EndYPercent { get; set; } = 20f;
		public double MotionDurationSec { get; set; } = 3.0;

		public override string ToString()
		{
			string status = Enabled ? "✅" : "⏸️";
			string timeStr = DurationSec > 0.05 ? $"{StartTimeSec:0.#}s~{StartTimeSec + DurationSec:0.#}s" : $"{StartTimeSec:0.#}s~全程";
			string motion = MotionStyle == 0 ? "固定" : $"飞行 {MotionDurationSec:0.#}s";
			return $"{status} {Name} ({XPercent:0}%, {YPercent:0}%) {motion} 大小:{ScalePercent:0}% 旋转:{RotationAngle:0}° [{timeStr}]";
		}
	}

	public static class GifStickerMotion
	{
		public static void Evaluate(GifStickerItem sticker, double videoTimeSeconds, out float xPercent, out float yPercent)
		{
			xPercent = sticker.XPercent;
			yPercent = sticker.YPercent;
			if (sticker.MotionStyle == 0) return;
			double duration = Math.Max(0.1, sticker.MotionDurationSec);
			double progress = Math.Max(0.0, Math.Min(1.0, (videoTimeSeconds - sticker.StartTimeSec) / duration));
			double eased = progress * progress * (3.0 - 2.0 * progress);
			xPercent += (float)((sticker.EndXPercent - sticker.XPercent) * eased);
			yPercent += (float)((sticker.EndYPercent - sticker.YPercent) * eased);
		}
	}

	public sealed class ScriptBatchSettings
	{
		private static readonly string SettingsFilePath = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"script_batch_settings.json"
		);

		private static readonly string AppDataSettingsFilePath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"VideoBatchStudio",
			"script_batch_settings.json"
		);

		// 语音引擎与参数
		public string TtsEngine { get; set; } = "edge_tts"; // "edge_tts" 或 "elevenlabs"
		public string DefaultVoice { get; set; } = "pt-PT-DuarteNeural";
		public string ElevenLabsVoice { get; set; } = "pNInz6obpgDQGcFmaJgB"; // 默认 Adam (原版母本音色)
		public string ElevenLabsCustomVoiceId { get; set; } = string.Empty;
		public string ChatterboxLanguage { get; set; } = "pt";
		public string ChatterboxReferenceAudio { get; set; } = string.Empty;
		public bool ChatterboxReferenceAuthorized { get; set; } = false;
		public double ChatterboxExaggeration { get; set; } = 0.65;
		public double ChatterboxCfg { get; set; } = 0.35;
		public int ChatterboxKeepAliveMinutes { get; set; } = 15;
		public List<ClonedVoiceItem> ClonedVoices { get; set; } = new List<ClonedVoiceItem>();

		public string SpeechRate { get; set; } = "+0%";
		public string SpeechPitch { get; set; } = "+0Hz";
		public string SpeechVolume { get; set; } = "+0%";

		// 团队人员与命名规则
		public string ProducerName { get; set; } = "陆恒"; // 制作人员名字/团队标识 (如 "陆恒" 或 "Neusa 8389-陆恒")
		public bool NamingRuleEnabled { get; set; } = true;

		// 视频画面尺寸与剪辑
		public int CanvasWidth { get; set; } = 1080;
		public int CanvasHeight { get; set; } = 1920;

		public double SliceDurationMin { get; set; } = 3.5;
		public double SliceDurationMax { get; set; } = 5.0;
		public int FootageStrategy { get; set; } = 0; // 0: 随机拼接, 1: 顺序拼接, 2: 单素材循环且批次去重

		// 字幕样式与排版 (全自由定制 + 永久记忆)
		public string SubtitleFontFamily { get; set; } = "Arial Rounded MT Bold";
		public int SubtitleFontSize { get; set; } = 86; // 36 ~ 140 px 连续可调
		public bool SubtitleBold { get; set; } = true;
		public bool SubtitleUppercase { get; set; } = false;
		public string SubtitleStyleId { get; set; } = "viral_blue_red_yellow";
		public string SubtitleColorHex { get; set; } = "#FFFFFF"; // 主文字颜色 (#RRGGBB 或 &H00BBGGRR)
		public int SubtitleTextOpacity { get; set; } = 100; // 主文字不透明度 20% ~ 100%
		public string SubtitleStrokeColorHex { get; set; } = "#0A1172"; // 描边颜色
		public float SubtitleStrokeWidth { get; set; } = 6.0f; // ASS 轮廓向外扩展的像素数，0 ~ 40 px
		public string SubtitleShadowColorHex { get; set; } = "#050838"; // 阴影/外发光颜色
		public float SubtitleShadowDepth { get; set; } = 3.0f; // 立体阴影距离 0 ~ 10
		public int SubtitleGlowBlur { get; set; } = 0; // 外发光柔和度 0 ~ 15

		// 逐词高亮与底框参数 (Mode 1 & Mode 3)
		public string SubtitleHighlightTextColorHex { get; set; } = "#FFFF00"; // 高亮词文字颜色
		public bool SubtitleHighlightBoxEnabled { get; set; } = true; // 是否启用逐词底框
		public string SubtitleHighlightBoxColorHex { get; set; } = "#D60000"; // 逐词底框颜色
		public int SubtitleHighlightBoxOpacity { get; set; } = 96; // 逐词底框不透明度 20% ~ 100%

		// 背景大底板 / 整句底框参数 (Mode 4 上滚底板 & BorderStyle=3 整句胶囊框)
		public int SubtitleBorderStyle { get; set; } = 1; // 1: 描边立体, 3: 整句胶囊底框 (Box)
		public int SubtitleDisplayMode { get; set; } = 3; // 0: 居中, 1: 逐词高亮, 2: 回弹弹出, 3: 逐词底框高亮, 4: 随语音逐行上滚
		public bool SubtitleScrollPanelEnabled { get; set; } = true; // 上滚字幕半透明底板开关
		public string SubtitleScrollPanelColorHex { get; set; } = "#EAE6DF"; // 背景大底板/整句底框颜色
		public int SubtitleScrollPanelOpacity { get; set; } = 62; // 背景大底板/整句底框不透明度 10% ~ 95%

		public int SubtitleMarginV { get; set; } = 130;
		public int SubtitleVerticalPercent { get; set; } = 82; // 15% (top) ~ 88% (bottom)
		public int SubtitleOffsetX { get; set; } = 0; // -500 ~ +500 (水平偏移，0为居中)
		public double LayoutLeftRatio { get; set; } = 0.38; // 文案成片三栏布局：左栏占总宽比例 (可拖拽记忆)
		public double LayoutRightRatio { get; set; } = 0.28; // 文案成片三栏布局：右栏占总宽比例 (可拖拽记忆)
		public int PreviewDurationSeconds { get; set; } = 5; // 0: 完整视频，或 5/8/10 秒

		// 视频加水印系统 (文字水印 & 图片水印)
		public bool WatermarkEnabled { get; set; } = false;
		public int WatermarkType { get; set; } = 0; // 0: 文字水印, 1: 图片水印
		public string WatermarkText { get; set; } = "Voice: ElevenLabs";
		public string WatermarkFontFamily { get; set; } = "Arial";
		public string WatermarkTextColorHex { get; set; } = "#FFFFFF";
		public int WatermarkTextOpacity { get; set; } = 80; // 10% ~ 100%
		public int WatermarkFontSize { get; set; } = 28;    // 16 ~ 72 px
		public string WatermarkImagePath { get; set; } = string.Empty;
		public int WatermarkImageScalePercent { get; set; } = 15; // 5% ~ 50%
		public int WatermarkImageOpacity { get; set; } = 85;      // 10% ~ 100%
		public int WatermarkPosition { get; set; } = 0; // 0: 右下角, 1: 右上角, 2: 左下角, 3: 左上角, 4: 底部居中, 5: 顶部居中
		public int WatermarkOffsetX { get; set; } = 35;
		public int WatermarkOffsetY { get; set; } = 40;

		// 背景音乐与素材池
		public bool BgmEnabled { get; set; } = true;
		public string BgmPath { get; set; } = string.Empty;
		public int BgmVolumePercent { get; set; } = 12;

		// GIF 动图贴纸 (支持多贴纸、旋转、缩放、出现时间)
		public List<GifStickerItem> GifStickers { get; set; } = new List<GifStickerItem>();

		// 视频标题独立图层，不读取字幕或水印参数。
		public MergeTitlePlan ScriptTitlePlan { get; set; } = new MergeTitlePlan
		{
			Enabled = false,
			StyleId = "cinematic_white",
			MainTitleTemplate = "今日的话语",
			Position = "居中偏上",
			DurationSeconds = 4.0
		};

		public string FootageFolderPath { get; set; } = string.Empty;
		public string OutputFolderPath { get; set; } = string.Empty;
		public string HardwareEncoder { get; set; } = "auto"; // auto, nvenc, qsv, cpu

		/// <summary>
		/// 兼容解析 #RRGGBB 与 ASS &H00BBGGRR 颜色字符串为 GDI+ Color
		/// </summary>
		public static Color ParseToColor(string hexOrAss, Color fallback)
		{
			if (string.IsNullOrWhiteSpace(hexOrAss)) return fallback;
			try
			{
				string s = hexOrAss.Trim();
				if (s.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
				{
					s = s.Substring(2).TrimEnd('&');
					if (s.Length == 8)
					{
						int b = int.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						int g = int.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						int r = int.Parse(s.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						return Color.FromArgb(255, r, g, b);
					}
					if (s.Length == 6)
					{
						int b = int.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						int g = int.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						int r = int.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						return Color.FromArgb(255, r, g, b);
					}
				}
				else
				{
					s = s.TrimStart('#');
					if (s.Length == 6)
					{
						int r = int.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						int g = int.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						int b = int.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						return Color.FromArgb(255, r, g, b);
					}
					if (s.Length == 8)
					{
						int r = int.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						int g = int.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						int b = int.Parse(s.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						return Color.FromArgb(255, r, g, b);
					}
				}
			}
			catch { }
			return fallback;
		}

		public static string ToHtmlHex(Color c)
		{
			return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
		}

		public static string NormalizeToHtmlHex(string hexOrAss, Color fallback)
		{
			return ToHtmlHex(ParseToColor(hexOrAss, fallback));
		}

		/// <summary>
		/// 转换为 ASS Style 定义中的 &HAABBGGRR 格式 (AA: 00=完全不透明, FF=完全透明)
		/// </summary>
		public static string ToAssStyleColor(string hexOrAss, Color fallback, int opacityPercent = 100)
		{
			Color c = ParseToColor(hexOrAss, fallback);
			int clamped = Math.Max(0, Math.Min(100, opacityPercent));
			int assAlpha = (int)Math.Round((100 - clamped) * 2.55);
			return $"&H{assAlpha:X2}{c.B:X2}{c.G:X2}{c.R:X2}";
		}

		/// <summary>
		/// 转换为 ASS 行内覆盖标签中的 &HBBGGRR& 格式
		/// </summary>
		public static string ToAssInlineBgr(string hexOrAss, Color fallback)
		{
			Color c = ParseToColor(hexOrAss, fallback);
			return $"&H{c.B:X2}{c.G:X2}{c.R:X2}&";
		}

		/// <summary>
		/// 转换为 ASS 行内透明度标签中的 &HAA& 格式 (AA: 00=不透明, FF=全透明)
		/// </summary>
		public static string ToAssInlineAlpha(int opacityPercent)
		{
			int clamped = Math.Max(0, Math.Min(100, opacityPercent));
			int assAlpha = (int)Math.Round((100 - clamped) * 2.55);
			return $"&H{assAlpha:X2}&";
		}

		public static ScriptBatchSettings Load()
		{
			try
			{
				var candidates = new[]
				{
					AppDataSettingsFilePath,
					SettingsFilePath,
					Path.Combine(@"D:\工具\视频拼接\VideoBatchStudio", "script_batch_settings.json"),
					Path.Combine(@"C:\Users\PcVkH\.gemini\antigravity\scratch\VideoBatchStudio", "script_batch_settings.json")
				};

				string bestPath = null;
				DateTime bestTime = DateTime.MinValue;

				foreach (var p in candidates)
				{
					try
					{
						if (File.Exists(p))
						{
							var fi = new FileInfo(p);
							if (fi.Length > 20 && fi.LastWriteTime > bestTime)
							{
								bestTime = fi.LastWriteTime;
								bestPath = p;
							}
						}
					}
					catch { }
				}

				if (!string.IsNullOrEmpty(bestPath) && File.Exists(bestPath))
				{
					string json = File.ReadAllText(bestPath);
					var serializer = new JavaScriptSerializer();
					var loaded = serializer.Deserialize<ScriptBatchSettings>(json);
					if (loaded != null)
					{
						if (loaded.SubtitleVerticalPercent <= 0) loaded.SubtitleVerticalPercent = 82;
						if (string.IsNullOrWhiteSpace(loaded.ProducerName)) loaded.ProducerName = "陆恒";
						if (string.IsNullOrWhiteSpace(loaded.TtsEngine)) loaded.TtsEngine = "edge_tts";
						if (loaded.SubtitleFontSize < 24) loaded.SubtitleFontSize = 86;
						if (loaded.SubtitleTextOpacity <= 0) loaded.SubtitleTextOpacity = 100;
						if (loaded.SubtitleHighlightBoxOpacity <= 0) loaded.SubtitleHighlightBoxOpacity = 95;
						if (loaded.SubtitleScrollPanelOpacity <= 0) loaded.SubtitleScrollPanelOpacity = 62;
						if (string.IsNullOrWhiteSpace(loaded.SubtitleFontFamily)) loaded.SubtitleFontFamily = "Arial Rounded MT Bold";
						if (string.IsNullOrWhiteSpace(loaded.WatermarkFontFamily)) loaded.WatermarkFontFamily = "Arial";
						if (loaded.ScriptTitlePlan == null) loaded.ScriptTitlePlan = new ScriptBatchSettings().ScriptTitlePlan;
						return loaded;
					}
				}
			}
			catch
			{
				// Fallback to default on any parse error
			}
			return new ScriptBatchSettings();
		}

		public void Save()
		{
			try
			{
				var serializer = new JavaScriptSerializer();
				string json = serializer.Serialize(this);

				var targets = new[]
				{
					SettingsFilePath,
					AppDataSettingsFilePath,
					Path.Combine(@"D:\工具\视频拼接\VideoBatchStudio", "script_batch_settings.json"),
					Path.Combine(@"C:\Users\PcVkH\.gemini\antigravity\scratch\VideoBatchStudio", "script_batch_settings.json")
				};

				foreach (var t in targets)
				{
					try
					{
						string dir = Path.GetDirectoryName(t);
						if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
						{
							Directory.CreateDirectory(dir);
						}
						File.WriteAllText(t, json);
					}
					catch { }
				}
			}
			catch
			{
				// Ignore file write error silently
			}
		}
	}
}
