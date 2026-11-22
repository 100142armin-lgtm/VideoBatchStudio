using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using VideoBatchMerger.ScriptBatch;

namespace VideoBatchMerger.SmartCompose
{
	public sealed class SmartComposeSettings
	{
		private static readonly string AppDataPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"VideoBatchStudio",
			"smart_compose_settings.json"
		);

		// 任务来源模式：0 = 谷歌表格云端闭环, 1 = 本地文件夹批量
		public int SourceMode { get; set; } = 0;

		// Google OAuth 2.0 桌面授权 (普通 Gmail 个人云盘 & 谷歌表格读写)
		public string GoogleClientId { get; set; } = string.Empty;
		public string GoogleClientSecret { get; set; } = string.Empty;
		public string GoogleAccessToken { get; set; } = string.Empty;
		public string GoogleRefreshToken { get; set; } = string.Empty;
		public long GoogleTokenExpiryUnix { get; set; } = 0;
		public string GoogleAccountEmail { get; set; } = string.Empty;

		// 谷歌表格与动态列映射 (自由指定)
		public string SheetUrl { get; set; } = string.Empty;
		public string SheetTabName { get; set; } = string.Empty;
		public string ColTaskSeq { get; set; } = string.Empty;
		public string ColTaskName { get; set; } = string.Empty;
		public string ColScript { get; set; } = string.Empty;
		public string ColSourceUrl { get; set; } = string.Empty;
		public string ColOutputLink { get; set; } = string.Empty;
		public string ColStatus { get; set; } = string.Empty;
		public List<string> CachedSheetTabs { get; set; } = new List<string>();
		public List<string> CachedSheetHeaders { get; set; } = new List<string>();

		// 成片命名设置 (与文案成片同规范)
		public string ProducerName { get; set; } = "陆恒";
		public bool NamingRuleEnabled { get; set; } = true;

		// 三栏布局记忆比例
		public double LayoutLeftRatio { get; set; } = 0.37;
		public double LayoutRightRatio { get; set; } = 0.31;

		// 云端上传与回填
		public string TargetDriveFolderUrl { get; set; } = string.Empty;
		public bool AutoUploadToDrive { get; set; } = true;
		public bool AutoWriteBackSheet { get; set; } = true;
		public bool KeepLocalCopyInCloudMode { get; set; } = false; // 云端模式下是否同时在本地保留一份成品
		public bool AutoWatchEnabled { get; set; } = false; // 无人值守自动巡检模式
		public int AutoWatchIntervalMinutes { get; set; } = 10;
		public string ProxyUrl { get; set; } = string.Empty;

		// 频道/矩阵方案预设（一键切换整套表格/云盘/字幕/水印配置）
		public string ActiveProfileName { get; set; } = string.Empty;
		public List<SmartComposeChannelProfile> ChannelProfiles { get; set; } = new List<SmartComposeChannelProfile>();

		// 成品质检门禁 (5 维自动质检)
		public bool QcEnabled { get; set; } = true;
		public bool QcBlockUploadOnFail { get; set; } = true;
		public int QcMinScriptMatchPercent { get; set; } = 85;

		// 独立的字幕与水印参数快照（永久记忆在 smart_compose_settings.json 中，不被其他板块覆盖）
		public ScriptBatchSettings SubtitleConfig { get; set; } = null;

		// 本地文件夹批量模式
		public string LocalInputRootDir { get; set; } = string.Empty;
		public string LocalOutputDir { get; set; } = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
			"智能合成成品"
		);

		// AI 识别与精剪参数
		public string WhisperLanguage { get; set; } = "auto";
		public string WhisperModel { get; set; } = "base";
		public bool RemoveSilenceEnabled { get; set; } = true;
		public int SilenceThresholdDb { get; set; } = -34;
		public double MinSilenceDurationSec { get; set; } = 0.35;
		public double KeepEdgePaddingSec { get; set; } = 0.08;
		public bool RemoveFillersEnabled { get; set; } = true;
		public string FillerWordsCsv { get; set; } = "uh,um,erm,ah,eh,hmm,嗯,啊,呃,那个";
		public bool AlignAndReorderByScript { get; set; } = true;
		public bool FastSinglePassWhisper { get; set; } = true; // 极速模式：先剪静音拼接，整片只跑 1 次 Whisper（省去 20 次重复加载模型）
		public int AudioCrossfadeMs { get; set; } = 25;

		// 画面、字幕、人声均衡、画质档位与 BGM
		public int TargetWidth { get; set; } = 1080;
		public int TargetHeight { get; set; } = 1920;
		public bool BurnSubtitlesEnabled { get; set; } = true;
		public bool LoudnormEnabled { get; set; } = true; // 全片人声 -14 LUFS 响度均衡
		public int ExportBitratePreset { get; set; } = 0; // 0: 极速 3.0Mbps, 1: 均衡 4.5Mbps, 2: 臻彩 6.5Mbps
		public bool BgmEnabled { get; set; } = false;
		public string BgmPath { get; set; } = string.Empty;
		public int BgmVolumePercent { get; set; } = 12;

		public bool HasGoogleAuth => !string.IsNullOrWhiteSpace(GoogleRefreshToken) || !string.IsNullOrWhiteSpace(GoogleAccessToken);

		public static SmartComposeSettings Load()
		{
			try
			{
				if (File.Exists(AppDataPath))
				{
					string json = File.ReadAllText(AppDataPath, Encoding.UTF8);
					var loaded = new JavaScriptSerializer().Deserialize<SmartComposeSettings>(json);
					if (loaded != null)
					{
						if (string.IsNullOrWhiteSpace(loaded.LocalOutputDir))
						{
							loaded.LocalOutputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "智能合成成品");
						}
						if (loaded.TargetWidth <= 0) loaded.TargetWidth = 1080;
						if (loaded.TargetHeight <= 0) loaded.TargetHeight = 1920;
						if (loaded.MinSilenceDurationSec < 0.1) loaded.MinSilenceDurationSec = 0.35;
						if (string.IsNullOrWhiteSpace(loaded.WhisperLanguage)) loaded.WhisperLanguage = "auto";
						if (string.IsNullOrWhiteSpace(loaded.WhisperModel)) loaded.WhisperModel = "base";
						if (string.IsNullOrWhiteSpace(loaded.ProducerName)) loaded.ProducerName = "陆恒";
						if (loaded.CachedSheetTabs == null) loaded.CachedSheetTabs = new List<string>();
						if (loaded.CachedSheetHeaders == null) loaded.CachedSheetHeaders = new List<string>();
						if (loaded.ChannelProfiles == null) loaded.ChannelProfiles = new List<SmartComposeChannelProfile>();
						if (loaded.AutoWatchIntervalMinutes < 3 || loaded.AutoWatchIntervalMinutes > 120) loaded.AutoWatchIntervalMinutes = 10;
						if (loaded.ExportBitratePreset < 0 || loaded.ExportBitratePreset > 2) loaded.ExportBitratePreset = 0;
						if (loaded.QcMinScriptMatchPercent < 50 || loaded.QcMinScriptMatchPercent > 100) loaded.QcMinScriptMatchPercent = 85;
						if (loaded.SubtitleConfig == null) loaded.SubtitleConfig = ScriptBatchSettings.Load();
						return loaded;
					}
				}
			}
			catch { }
			var fresh = new SmartComposeSettings();
			fresh.SubtitleConfig = ScriptBatchSettings.Load();
			return fresh;
		}

		public void Save()
		{
			try
			{
				string dir = Path.GetDirectoryName(AppDataPath);
				if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}
				string json = new JavaScriptSerializer().Serialize(this);
				File.WriteAllText(AppDataPath, json, new UTF8Encoding(false));
			}
			catch { }
		}
	}

	public sealed class SmartComposeChannelProfile
	{
		public string Name { get; set; } = string.Empty;
		public int SourceMode { get; set; }
		public string SheetUrl { get; set; } = string.Empty;
		public string SheetTabName { get; set; } = string.Empty;
		public string ColTaskSeq { get; set; } = string.Empty;
		public string ColTaskName { get; set; } = string.Empty;
		public string ColScript { get; set; } = string.Empty;
		public string ColSourceUrl { get; set; } = string.Empty;
		public string ColOutputLink { get; set; } = string.Empty;
		public string ColStatus { get; set; } = string.Empty;
		public string TargetDriveFolderUrl { get; set; } = string.Empty;
		public bool AutoUploadToDrive { get; set; } = true;
		public bool AutoWriteBackSheet { get; set; } = true;
		public bool KeepLocalCopyInCloudMode { get; set; }
		public string LocalInputRootDir { get; set; } = string.Empty;
		public string LocalOutputDir { get; set; } = string.Empty;
		public string WhisperLanguage { get; set; } = "auto";
		public string WhisperModel { get; set; } = "base";
		public bool FastSinglePassWhisper { get; set; } = true;
		public bool BurnSubtitlesEnabled { get; set; } = true;
		public bool LoudnormEnabled { get; set; } = true;
		public int ExportBitratePreset { get; set; }
		public bool QcEnabled { get; set; } = true;
		public bool QcBlockUploadOnFail { get; set; } = true;
		public int QcMinScriptMatchPercent { get; set; } = 85;
		public bool BgmEnabled { get; set; }
		public string BgmPath { get; set; } = string.Empty;
		public int BgmVolumePercent { get; set; } = 12;
		public ScriptBatchSettings SubtitleConfig { get; set; }
	}

	public sealed class SheetColumnHeader
	{
		public int ColumnIndex { get; set; } // 0-based
		public string ColumnLetter { get; set; } = "A";
		public string HeaderText { get; set; } = string.Empty;

		public string Display => string.IsNullOrWhiteSpace(HeaderText)
			? $"{ColumnLetter} 列 (未命名)"
			: $"{ColumnLetter} 列 · {HeaderText}";

		public override string ToString() => Display;
	}

	public sealed class SmartComposeTaskItem
	{
		public int Index { get; set; }
		public int SheetRowNumber { get; set; } // 1-based row in Google Sheet (0 for local folder)
		public string TaskSeq { get; set; } = string.Empty; // 表格里的「任务序号」
		public string TaskName { get; set; } = string.Empty;
		public string StandardScript { get; set; } = string.Empty;
		public List<string> SourceUrls { get; set; } = new List<string>();
		public string LocalSourceFolder { get; set; } = string.Empty;

		public int ClipCount { get; set; }
		public int StageIndex { get; set; } // 0: 待处理, 1: 读取表格, 2: 下载素材, 3: AI识别, 4: 剪气口对齐, 5: 烧录字幕成片, 6: 上传并回填
		public string StageText { get; set; } = "等待开始";
		public int ProgressPercent { get; set; }
		public double TrimmedSeconds { get; set; }
		public string Status { get; set; } = "待处理";
		public string OutputFilePath { get; set; } = string.Empty;
		public string UploadedDriveUrl { get; set; } = string.Empty;
		public string ErrorPlainMessage { get; set; } = string.Empty;
		public string ElapsedText { get; set; } = "--";

		// 成品质检结果
		public string QcSummary { get; set; } = "待检";
		public bool? QcPassed { get; set; }
		public SmartComposeQcReport QcReport { get; set; }
	}

	public sealed class SmartComposeQcReport
	{
		public bool Passed { get; set; }
		public int ScriptMatchPercent { get; set; }
		public bool ScriptAudioPass { get; set; }
		public bool SubtitleSyncPass { get; set; }
		public bool AudioHealthPass { get; set; }
		public bool VideoIntegrityPass { get; set; }
		public bool AvSyncPass { get; set; }

		public double VideoDurationSec { get; set; }
		public double AudioDurationSec { get; set; }
		public double MeanVolumeDb { get; set; }
		public double MaxVolumeDb { get; set; }
		public string StandardScriptText { get; set; } = string.Empty;
		public string RecognizedAudioText { get; set; } = string.Empty;
		public List<string> MissingWords { get; set; } = new List<string>();
		public List<string> FailureReasons { get; set; } = new List<string>();

		public string ShortBadge => Passed
			? $"✅ 合格 ({ScriptMatchPercent}%)"
			: $"❌ 拦截: {(FailureReasons.Count > 0 ? FailureReasons[0] : "质检未通过")}";
	}

	public enum PlainLogLevel
	{
		Info,
		Success,
		Warning,
		Error
	}

	public sealed class PlainLogEntry
	{
		public DateTime Timestamp { get; set; } = DateTime.Now;
		public PlainLogLevel Level { get; set; } = PlainLogLevel.Info;
		public string Stage { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string PlainCause { get; set; } = string.Empty;
		public string SelfFixTip { get; set; } = string.Empty;
		public string RawDetail { get; set; } = string.Empty;

		public string ToReadableText()
		{
			string icon = Level switch
			{
				PlainLogLevel.Success => "✅",
				PlainLogLevel.Warning => "⚠️",
				PlainLogLevel.Error => "❌",
				_ => "ℹ️"
			};
			var sb = new StringBuilder();
			sb.Append($"{icon} [{Timestamp:HH:mm:ss}] ");
			if (!string.IsNullOrWhiteSpace(Stage)) sb.Append($"[{Stage}] ");
			sb.AppendLine(Title);
			if (!string.IsNullOrWhiteSpace(PlainCause))
				sb.AppendLine($"    原因说明：{PlainCause}");
			if (!string.IsNullOrWhiteSpace(SelfFixTip))
				sb.AppendLine($"    👉 怎么解决：{SelfFixTip}");
			return sb.ToString();
		}
	}
}
