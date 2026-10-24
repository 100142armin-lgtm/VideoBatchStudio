using System;
using System.Collections.Generic;
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

		public override string ToString()
		{
			string status = Enabled ? "✅" : "⏸️";
			string timeStr = DurationSec > 0.05 ? $"{StartTimeSec:0.#}s~{StartTimeSec + DurationSec:0.#}s" : $"{StartTimeSec:0.#}s~全程";
			return $"{status} {Name} ({XPercent:0}%, {YPercent:0}%) 大小:{ScalePercent:0}% 旋转:{RotationAngle:0}° [{timeStr}]";
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
		public int FootageStrategy { get; set; } = 0; // 0: 随机去重, 1: 顺序轮询

		// 字幕样式与排版
		public string SubtitleFontFamily { get; set; } = "Arial";
		public int SubtitleFontSize { get; set; } = 86; // 86 px (1080x1920 标配大屏清晰字号)
		public string SubtitleStyleId { get; set; } = "sacred_warm_gold";
		public string SubtitleColorHex { get; set; } = "&H00FFFFFF"; // ASS BGR: Pure White
		public string SubtitleStrokeColorHex { get; set; } = "&H00155095"; // Warm Golden Rim
		public float SubtitleStrokeWidth { get; set; } = 1.5f;
		public int SubtitleMarginV { get; set; } = 130;
		public int SubtitleVerticalPercent { get; set; } = 82; // 15% (top) ~ 88% (bottom)
		public int SubtitleOffsetX { get; set; } = 0; // -500 ~ +500 (水平偏移，0为居中)
		public double LayoutLeftRatio { get; set; } = 0.38; // 文案成片三栏布局：左栏占总宽比例 (可拖拽记忆)
		public double LayoutRightRatio { get; set; } = 0.28; // 文案成片三栏布局：右栏占总宽比例 (可拖拽记忆)
		public int SubtitleBorderStyle { get; set; } = 1; // 1: 描边阴影, 3: 不透明底框 (Box)
		public int SubtitleDisplayMode { get; set; } = 0; // 0: 句级自然居中, 1: 词级逐词弹跳, 2: 动感回弹弹出

		// 视频加水印系统 (文字水印 & 图片水印)
		public bool WatermarkEnabled { get; set; } = false;
		public int WatermarkType { get; set; } = 0; // 0: 文字水印, 1: 图片水印
		public string WatermarkText { get; set; } = "Voice: ElevenLabs";
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

		public string FootageFolderPath { get; set; } = string.Empty;
		public string OutputFolderPath { get; set; } = string.Empty;
		public string HardwareEncoder { get; set; } = "auto"; // auto, nvenc, qsv, cpu

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
						if (loaded.SubtitleFontSize <= 60) loaded.SubtitleFontSize = 86;
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
