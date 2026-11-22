using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VideoBatchMerger.Downloader;
using VideoBatchMerger.ScriptBatch;
using VideoBatchMerger.Transcriber;

namespace VideoBatchMerger.SmartCompose
{
	public sealed class SmartComposeCallbacks
	{
		public Action<int, string, int> OnStageUpdate; // (stageIndex 1..6, liveDetailText, percent 0..100)
		public Action<PlainLogEntry> OnLog;
		public Action OnTaskRowRefresh;
	}

	public sealed class SmartComposeProduceResult
	{
		public bool ReadyForUpload { get; set; }
		public bool AlreadyCompleted { get; set; }
		public string FinalOutputPath { get; set; } = string.Empty;
		public string WorkDir { get; set; } = string.Empty;
		public bool CloudUploadOnlyTemp { get; set; }
		public int ClipCount { get; set; }
		public bool AssBurned { get; set; }
		public double ProduceSeconds { get; set; }
	}

	public static class SmartComposeEngine
	{
		private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v", ".ts"
		};

		private static readonly Regex SilenceStartRegex = new Regex(@"silence_start:\s*([0-9.]+)", RegexOptions.Compiled);
		private static readonly Regex SilenceEndRegex = new Regex(@"silence_end:\s*([0-9.]+)", RegexOptions.Compiled);
		private static readonly Regex DurationRegex = new Regex(@"Duration:\s*(\d{2}):(\d{2}):(\d{2}\.\d+)", RegexOptions.Compiled);
		private static readonly Regex MeanVolumeRegex = new Regex(@"mean_volume:\s*([-\d.]+)\s*dB", RegexOptions.Compiled);
		private static readonly Regex MaxVolumeRegex = new Regex(@"max_volume:\s*([-\d.]+)\s*dB", RegexOptions.Compiled);
		private static readonly Regex TimeProgressRegex = new Regex(@"time=(\d{2}):(\d{2}):(\d{2}\.\d+)", RegexOptions.Compiled);

		private sealed class ClipAnalysis
		{
			public string FilePath;
			public string FileName;
			public double OriginalDuration;
			public List<WordPoint> Words = new List<WordPoint>();
			public List<(double Start, double End)> KeptRanges = new List<(double Start, double End)>();
			public double TrimmedSeconds;
			public string RecognizedText;
			public int ScriptOrderScore = int.MaxValue;
			public int OriginalIndex;
		}

		private sealed class WordPoint
		{
			public string Text;
			public double Start;
			public double End;
		}

		/// <summary>
		/// 扫描本地文件夹生成批量任务：若目录下有子文件夹则每个子文件夹为 1 条任务；否则当前目录直接作为 1 条任务
		/// </summary>
		public static List<SmartComposeTaskItem> ScanLocalFolderTasks(string rootDir)
		{
			var tasks = new List<SmartComposeTaskItem>();
			if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir)) return tasks;

			var subDirs = Directory.GetDirectories(rootDir)
				.OrderBy(d => Path.GetFileName(d), Comparer<string>.Create(GoogleWorkspaceClient.CompareNatural))
				.ToList();

			var candidateDirs = new List<string>();
			foreach (var sd in subDirs)
			{
				if (GetSortedVideosInDir(sd).Count > 0) candidateDirs.Add(sd);
			}
			if (candidateDirs.Count == 0 && GetSortedVideosInDir(rootDir).Count > 0)
			{
				candidateDirs.Add(rootDir);
			}

			int idx = 0;
			foreach (var dir in candidateDirs)
			{
				var videos = GetSortedVideosInDir(dir);
				if (videos.Count == 0) continue;

				string script = string.Empty;
				var txtFile = Directory.GetFiles(dir, "*.txt", SearchOption.TopDirectoryOnly)
					.OrderBy(f => Path.GetFileName(f).IndexOf("文案", StringComparison.OrdinalIgnoreCase) >= 0 ||
					              Path.GetFileName(f).IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
					.FirstOrDefault();
				if (!string.IsNullOrEmpty(txtFile))
				{
					try { script = File.ReadAllText(txtFile, Encoding.UTF8).Trim(); } catch { }
				}

				idx++;
				tasks.Add(new SmartComposeTaskItem
				{
					Index = idx,
					SheetRowNumber = 0,
					TaskName = Path.GetFileName(dir),
					StandardScript = script,
					LocalSourceFolder = dir,
					ClipCount = videos.Count,
					Status = "待处理"
				});
			}
			return tasks;
		}

		private static List<string> GetSortedVideosInDir(string dir)
		{
			if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return new List<string>();
			var list = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
				.Where(f => VideoExtensions.Contains(Path.GetExtension(f)))
				.ToList();
			list.Sort((a, b) => GoogleWorkspaceClient.CompareNatural(Path.GetFileName(a), Path.GetFileName(b)));
			return list;
		}

		/// <summary>
		/// 阶段 2~5.5：下载素材 -> 静音/AI 识别 -> 剪气口拼接 -> 烧录字幕 -> 5 维成品质检。
		/// 若质检合格且开启云端上传，立即返回 ReadyForUpload=true 交给后台异步上传队列，主工位 0 秒等待开跑下一条！
		/// </summary>
		public static async Task<SmartComposeProduceResult> ProduceAndQcTaskAsync(
			SmartComposeTaskItem task,
			SmartComposeSettings settings,
			ScriptBatchSettings subtitleSettings,
			SmartComposeCallbacks cb,
			CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			SweepExpiredWorkDirs();
			string workDir = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"VideoBatchStudio",
				"SmartComposeWork",
				SanitizeName($"{task.Index}_{task.TaskName}")
			);
			Directory.CreateDirectory(workDir);

			string currentStage = "准备中";
			task.QcSummary = "待检";
			task.QcPassed = null;
			task.QcReport = null;
			task.ErrorPlainMessage = string.Empty;
			try
			{
				// ========================================================
				// 阶段 2：准备 / 下载视频片段素材
				// ========================================================
				currentStage = "下载素材";
				task.StageIndex = 2;
				task.StageText = "正在获取视频片段...";
				task.Status = "下载素材中";
				cb?.OnStageUpdate?.Invoke(2, $"正在为《{task.TaskName}》拉取视频片段...", 10);
				cb?.OnTaskRowRefresh?.Invoke();

				var clipFiles = await AcquireClipsAsync(task, settings, workDir, cb, ct);
				if (clipFiles.Count == 0)
				{
					throw new InvalidOperationException("未找到任何可用的视频片段（MP4/MOV/MKV），请检查素材文件夹或链接。");
				}
				task.ClipCount = clipFiles.Count;
				cb?.OnTaskRowRefresh?.Invoke();

				string ffmpegPath = DownloadEngine.FindFFmpegPath();
				if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
				{
					throw new InvalidOperationException("未找到内置 FFmpeg 组件，无法剪切和合成视频。");
				}

				// ========================================================
				// 阶段 3：静音/气口扫描（极速模式跳过逐段 Whisper，留待成片一次性识别，提速 2 倍）
				// ========================================================
				currentStage = "AI 识别";
				task.StageIndex = 3;
				bool useFastSinglePass = settings.FastSinglePassWhisper;
				task.StageText = useFastSinglePass ? "正在极速扫描气口..." : "正在 AI 听写与气口检测...";
				task.Status = useFastSinglePass ? "气口扫描中" : "AI 识别中";
				cb?.OnStageUpdate?.Invoke(3, useFastSinglePass
					? $"极速模式：正在扫描 {clipFiles.Count} 个片段的静音与气口..."
					: $"正在用 Whisper 逐词识别 {clipFiles.Count} 个片段...", 32);
				cb?.OnTaskRowRefresh?.Invoke();

				string whisperCli = TranscribeEngine.FindWhisperCliPath();
				string modelPath = ResolveWhisperModelPath(settings.WhisperModel);
				bool canWhisper = !string.IsNullOrEmpty(whisperCli) && File.Exists(whisperCli) &&
				                  !string.IsNullOrEmpty(modelPath) && File.Exists(modelPath);

				if (!canWhisper && (settings.BurnSubtitlesEnabled || settings.RemoveFillersEnabled || settings.AlignAndReorderByScript))
				{
					cb?.OnLog?.Invoke(new PlainLogEntry
					{
						Level = PlainLogLevel.Warning,
						Stage = "AI 识别",
						Title = $"未检测到本地 Whisper 引擎或模型（{settings.WhisperModel}），将跳过逐词字幕识别，仅执行静音气口裁切与拼接。",
						SelfFixTip = "点击软件顶部【组件/模型】一键安装 Whisper 通用识别引擎与 Base 模型即可启用 AI 精准字幕。"
					});
				}

				string perClipWhisper = (!useFastSinglePass && canWhisper) ? whisperCli : null;
				string perClipModel = (!useFastSinglePass && canWhisper) ? modelPath : null;

				var analyses = new List<ClipAnalysis>();
				for (int i = 0; i < clipFiles.Count; i++)
				{
					ct.ThrowIfCancellationRequested();
					string file = clipFiles[i];
					int pct = 30 + (int)((i + 1) * 24.0 / clipFiles.Count);
					task.ProgressPercent = pct;
					cb?.OnStageUpdate?.Invoke(3, useFastSinglePass
						? $"正在扫描第 {i + 1}/{clipFiles.Count} 段气口：{Path.GetFileName(file)}"
						: $"正在识别第 {i + 1}/{clipFiles.Count} 个片段：{Path.GetFileName(file)}", pct);
					cb?.OnTaskRowRefresh?.Invoke();

					var analysis = await AnalyzeSingleClipAsync(file, i, ffmpegPath, perClipWhisper, perClipModel, settings, task.StandardScript, ct);
					analyses.Add(analysis);
				}

				// ========================================================
				// 阶段 4：剪掉停顿/气口/语气词 + 按标准文案对齐排序
				// ========================================================
				currentStage = "剪气口对齐";
				task.StageIndex = 4;
				task.StageText = "正在精剪气口并核对文案...";
				task.Status = "剪气口对齐中";
				task.ProgressPercent = 58;
				cb?.OnStageUpdate?.Invoke(4, $"正在裁剪停顿气口并准备拼接...", 58);
				cb?.OnTaskRowRefresh?.Invoke();

				if (!useFastSinglePass && settings.AlignAndReorderByScript && !string.IsNullOrWhiteSpace(task.StandardScript) && analyses.Count > 1)
				{
					ReorderClipsByStandardScript(analyses, task.StandardScript);
				}

				double totalTrimmed = analyses.Sum(a => a.TrimmedSeconds);
				task.TrimmedSeconds = Math.Round(totalTrimmed, 2);

				string recognizedMergedText = string.Join(" ", analyses.Select(a => a.RecognizedText).Where(s => !string.IsNullOrWhiteSpace(s)));

				// ========================================================
				// 阶段 5：FFmpeg 精剪拼接 + 整片一次 Whisper 对齐 + 烧录爆款字幕库
				// ========================================================
				currentStage = "烧录字幕成片";
				task.StageIndex = 5;
				task.StageText = "正在合成并烧录爆款字幕...";
				task.Status = "合成字幕中";
				task.ProgressPercent = 66;
				cb?.OnStageUpdate?.Invoke(5, $"正在拼接 {analyses.Count} 个精剪片段、AI 对齐并烧录字幕...", 66);
				cb?.OnTaskRowRefresh?.Invoke();

				bool cloudUploadOnlyTemp = settings.SourceMode == 0 && settings.AutoUploadToDrive && !settings.KeepLocalCopyInCloudMode;
				string finalFileName = BuildOutputFileName(task, settings);
				string finalOutputPath;
				if (cloudUploadOnlyTemp)
				{
					finalOutputPath = Path.Combine(workDir, finalFileName);
				}
				else
				{
					string outDir = string.IsNullOrWhiteSpace(settings.LocalOutputDir)
						? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "智能合成成品")
						: settings.LocalOutputDir;
					Directory.CreateDirectory(outDir);
					finalOutputPath = EnsureUniqueOutputPath(Path.Combine(outDir, finalFileName));
				}

				var subCopy = CloneSubtitleSettings(subtitleSettings, settings.TargetWidth, settings.TargetHeight);
				string watermarkPngPath = null;
				if (subCopy.WatermarkEnabled)
				{
					string wmCandidate = Path.Combine(workDir, "watermark_layer.png");
					ScriptBatchComposer.BuildWatermarkLayer(subCopy, settings.TargetWidth, settings.TargetHeight, wmCandidate);
					if (File.Exists(wmCandidate) && new FileInfo(wmCandidate).Length > 100)
					{
						watermarkPngPath = wmCandidate;
					}
				}

				var (mergedWords, assPath, finalRawRecognized) = await RenderFinalVideoAsync(analyses, task.StandardScript, subCopy, watermarkPngPath, finalOutputPath, ffmpegPath, canWhisper ? whisperCli : null, canWhisper ? modelPath : null, settings, workDir, p =>
				{
					int totalPct = 66 + (int)(p * 0.18);
					task.ProgressPercent = totalPct;
					cb?.OnStageUpdate?.Invoke(5, $"正在合成与烧录字幕 ({p}%)...", totalPct);
				}, ct);

				if (!string.IsNullOrWhiteSpace(finalRawRecognized))
				{
					recognizedMergedText = finalRawRecognized;
				}
				task.OutputFilePath = finalOutputPath;

				// ========================================================
				// 阶段 5.5：5 维成品质检门禁（标准文案 ↔ 音频 ↔ 字幕 ↔ 画面 ↔ 音画同步）
				// ========================================================
				if (settings.QcEnabled)
				{
					currentStage = "成品质检";
					task.StageText = "正在执行 5 维成品质检...";
					task.Status = "成品质检中";
					task.ProgressPercent = 86;
					cb?.OnStageUpdate?.Invoke(5, $"正在对《{task.TaskName}》进行 5 维成品质检...", 86);
					cb?.OnTaskRowRefresh?.Invoke();

					int subWordCount = mergedWords?.Count ?? 0;
					double lastSubEnd = subWordCount > 0 ? mergedWords[subWordCount - 1].End : 0;
					string finalSubText = subWordCount > 0 ? string.Join(" ", mergedWords.Select(w => w.Text)) : string.Empty;
					var qc = await RunQualityCheckAsync(finalOutputPath, task.StandardScript, recognizedMergedText, subWordCount, lastSubEnd, settings.BurnSubtitlesEnabled, ffmpegPath, settings, ct, finalSubText);
					task.QcReport = qc;
					task.QcPassed = qc.Passed;
					task.QcSummary = qc.ShortBadge;
					cb?.OnTaskRowRefresh?.Invoke();

					if (!qc.Passed && settings.QcBlockUploadOnFail)
					{
						sw.Stop();
						string reasonJoin = string.Join("；", qc.FailureReasons);
						task.Status = "质检拦截";
						task.StageText = qc.ShortBadge;
						task.ErrorPlainMessage = reasonJoin;
						task.ElapsedText = $"{sw.Elapsed.TotalSeconds:0.0}秒";

						if (settings.SourceMode == 0 && settings.AutoWriteBackSheet && task.SheetRowNumber > 1)
						{
							try
							{
								await GoogleWorkspaceClient.UpdateSheetRowAsync(settings, task.SheetRowNumber, null, $"质检拦截: {reasonJoin}", ct);
							}
							catch { }
						}

						string missingHint = qc.MissingWords.Count > 0
							? $"（疑似漏读/偏差词：{string.Join("、", qc.MissingWords.Take(10))}）"
							: string.Empty;

						cb?.OnLog?.Invoke(new PlainLogEntry
						{
							Level = PlainLogLevel.Warning,
							Stage = "成品质检拦截",
							Title = $"《{task.TaskName}》未通过成品质检，已自动拦截禁止上传云盘！",
							PlainCause = $"{reasonJoin}{missingHint}",
							SelfFixTip = "请在左侧任务列表【双击】本行查看完整 5 维质检报告与文案比对、播放本地成品核对；如确认无误可点【人工复核通过·强制上传并回填】放行。"
						});
						cb?.OnTaskRowRefresh?.Invoke();
						CleanupHeavyIntermediatesOnly(workDir);
						return new SmartComposeProduceResult { ReadyForUpload = false, AlreadyCompleted = false };
					}
				}
				else
				{
					task.QcSummary = "已跳过";
					task.QcPassed = null;
				}

				// 立刻释放占空间的中间片段，只保留最终待上传的 MP4，方便后台上传同时继续合成下一条
				CleanupHeavyIntermediatesOnly(workDir);
				sw.Stop();

				if (settings.SourceMode == 0 && settings.AutoUploadToDrive)
				{
					task.StageIndex = 6;
					task.ProgressPercent = 88;
					task.Status = "排队上传中";
					task.StageText = "质检合格 · 已移交后台上传队列";
					cb?.OnTaskRowRefresh?.Invoke();

					return new SmartComposeProduceResult
					{
						ReadyForUpload = true,
						AlreadyCompleted = false,
						FinalOutputPath = finalOutputPath,
						WorkDir = workDir,
						CloudUploadOnlyTemp = cloudUploadOnlyTemp,
						ClipCount = analyses.Count,
						AssBurned = assPath != null,
						ProduceSeconds = sw.Elapsed.TotalSeconds
					};
				}

				if (settings.SourceMode == 0 && settings.AutoWriteBackSheet && task.SheetRowNumber > 1)
				{
					await GoogleWorkspaceClient.UpdateSheetRowAsync(settings, task.SheetRowNumber, finalOutputPath, "已完成", ct);
				}

				task.ProgressPercent = 100;
				task.StageIndex = 6;
				task.StageText = "全部完成";
				task.Status = "已完成";
				task.ElapsedText = $"{sw.Elapsed.TotalSeconds:0.0}秒";
				cb?.OnStageUpdate?.Invoke(6, $"《{task.TaskName}》已全部完成！", 100);
				cb?.OnTaskRowRefresh?.Invoke();

				cb?.OnLog?.Invoke(new PlainLogEntry
				{
					Level = PlainLogLevel.Success,
					Stage = "全部完成",
					Title = $"第 {task.Index} 条任务《{task.TaskName}》合成成功！（耗时 {task.ElapsedText}）",
					PlainCause = $"共处理 {analyses.Count} 个视频片段 → 自动剪掉 {task.TrimmedSeconds:0.0} 秒停顿与气口 → {(assPath != null ? "已烧录爆款字幕" : "无字幕合成")} → 已保存到本地 {Path.GetFileName(finalOutputPath)}"
				});

				CleanupWorkDirCompletely(workDir);
				return new SmartComposeProduceResult { ReadyForUpload = false, AlreadyCompleted = true };
			}
			catch (OperationCanceledException)
			{
				task.Status = "已停止";
				task.StageText = "用户手动停止";
				cb?.OnTaskRowRefresh?.Invoke();
				throw;
			}
			catch (Exception ex)
			{
				sw.Stop();
				var plain = TranslateErrorToPlainLog(currentStage, task.TaskName, ex);
				task.Status = "失败";
				task.StageText = plain.Title;
				task.ErrorPlainMessage = plain.PlainCause;
				task.ElapsedText = $"{sw.Elapsed.TotalSeconds:0.0}秒";
				cb?.OnLog?.Invoke(plain);
				cb?.OnTaskRowRefresh?.Invoke();

				if (settings.SourceMode == 0 && settings.AutoWriteBackSheet && task.SheetRowNumber > 1)
				{
					try
					{
						await GoogleWorkspaceClient.UpdateSheetRowAsync(settings, task.SheetRowNumber, null, $"失败: {plain.PlainCause}", CancellationToken.None);
					}
					catch { }
				}
				return new SmartComposeProduceResult { ReadyForUpload = false, AlreadyCompleted = false };
			}
		}

		/// <summary>
		/// 阶段 6（独立后台异步通道）：连续流式上传成品至谷歌云盘 + 回填谷歌表格 + 自动清盘
		/// </summary>
		public static async Task<bool> UploadAndFinalizeTaskAsync(
			SmartComposeTaskItem task,
			SmartComposeProduceResult prod,
			SmartComposeSettings settings,
			SmartComposeCallbacks cb,
			CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();
			try
			{
				task.StageIndex = 6;
				task.Status = "后台上传中";
				double fileMb = File.Exists(prod.FinalOutputPath) ? new FileInfo(prod.FinalOutputPath).Length / (1024.0 * 1024.0) : 0;
				task.StageText = $"后台上传中 0% ({fileMb:0.0} MB)";
				cb?.OnLog?.Invoke(new PlainLogEntry
				{
					Level = PlainLogLevel.Info,
					Stage = "后台上传",
					Title = $"《{task.TaskName}》质检合格，已进入后台极速直传通道（{fileMb:0.0} MB），主线已无缝开跑下一条！"
				});
				cb?.OnTaskRowRefresh?.Invoke();

				string shareUrl = await GoogleWorkspaceClient.UploadFileAndShareAsync(
					settings,
					prod.FinalOutputPath,
					settings.TargetDriveFolderUrl,
					(upPct, mbPerSec) =>
					{
						task.ProgressPercent = 88 + (int)(upPct * 0.10);
						task.StageText = mbPerSec > 0.01
							? $"后台上传 {upPct}% ({mbPerSec:0.0} MB/s)"
							: $"后台上传 {upPct}%";
						cb?.OnTaskRowRefresh?.Invoke();
					},
					ct
				);
				task.UploadedDriveUrl = shareUrl;

				if (settings.AutoWriteBackSheet && task.SheetRowNumber > 1)
				{
					task.StageText = "正在回填谷歌表格...";
					cb?.OnTaskRowRefresh?.Invoke();
					await GoogleWorkspaceClient.UpdateSheetRowAsync(settings, task.SheetRowNumber, shareUrl, "已完成", ct);
				}

				if (prod.CloudUploadOnlyTemp)
				{
					try { if (File.Exists(prod.FinalOutputPath)) File.Delete(prod.FinalOutputPath); } catch { }
					task.OutputFilePath = string.Empty;
				}

				sw.Stop();
				double totalSec = prod.ProduceSeconds + sw.Elapsed.TotalSeconds;
				task.ProgressPercent = 100;
				task.StageIndex = 6;
				task.StageText = "全部完成";
				task.Status = "已完成";
				task.ElapsedText = $"{totalSec:0.0}秒";
				cb?.OnTaskRowRefresh?.Invoke();

				string uploadInfo = prod.CloudUploadOnlyTemp ? " → 已上传云盘并回填表格（仅存云端）" : " → 已上传云盘并在本地保留一份";
				cb?.OnLog?.Invoke(new PlainLogEntry
				{
					Level = PlainLogLevel.Success,
					Stage = "全部完成",
					Title = $"第 {task.Index} 条任务《{task.TaskName}》已上传回填完成！（合成 {prod.ProduceSeconds:0.0}s + 上传 {sw.Elapsed.TotalSeconds:0.0}s）",
					PlainCause = $"共处理 {prod.ClipCount} 个视频片段 → 剪掉 {task.TrimmedSeconds:0.0} 秒气口 → {(prod.AssBurned ? "已烧录爆款字幕" : "无字幕合成")}{uploadInfo}"
				});

				CleanupWorkDirCompletely(prod.WorkDir);
				return true;
			}
			catch (OperationCanceledException)
			{
				task.Status = "已停止";
				task.StageText = "用户手动停止";
				cb?.OnTaskRowRefresh?.Invoke();
				return false;
			}
			catch (Exception ex)
			{
				sw.Stop();
				var plain = TranslateErrorToPlainLog("上传并回填", task.TaskName, ex);
				task.Status = "失败";
				task.StageText = plain.Title;
				task.ErrorPlainMessage = plain.PlainCause;
				cb?.OnLog?.Invoke(plain);
				cb?.OnTaskRowRefresh?.Invoke();

				if (settings.SourceMode == 0 && settings.AutoWriteBackSheet && task.SheetRowNumber > 1)
				{
					try
					{
						await GoogleWorkspaceClient.UpdateSheetRowAsync(settings, task.SheetRowNumber, null, $"失败: {plain.PlainCause}", CancellationToken.None);
					}
					catch { }
				}
				return false;
			}
		}

		/// <summary>
		/// 兼容入口：单条任务顺序执行（合成 + 上传）
		/// </summary>
		public static async Task<bool> ProcessTaskAsync(
			SmartComposeTaskItem task,
			SmartComposeSettings settings,
			ScriptBatchSettings subtitleSettings,
			SmartComposeCallbacks cb,
			CancellationToken ct)
		{
			var prod = await ProduceAndQcTaskAsync(task, settings, subtitleSettings, cb, ct);
			if (prod.ReadyForUpload)
			{
				return await UploadAndFinalizeTaskAsync(task, prod, settings, cb, ct);
			}
			return prod.AlreadyCompleted;
		}

		/// <summary>
		/// 5 维成品质检：1.音频与标准文案一致度 2.字幕时间轴有效性 3.音频音量健康度(防静音) 4.视频流与文件完整性 5.音画时长同步
		/// </summary>
		public static async Task<SmartComposeQcReport> RunQualityCheckAsync(
			string videoPath,
			string standardScript,
			string recognizedAudioText,
			int subtitleWordCount,
			double lastSubtitleEndSec,
			bool burnSubtitlesEnabled,
			string ffmpegPath,
			SmartComposeSettings settings,
			CancellationToken ct,
			string subtitleHeadText = null)
		{
			var report = new SmartComposeQcReport
			{
				StandardScriptText = standardScript ?? string.Empty,
				RecognizedAudioText = recognizedAudioText ?? string.Empty,
				MeanVolumeDb = -91.0,
				MaxVolumeDb = -91.0
			};

			// 维度 3/4/5：通过 FFmpeg volumedetect 一次扫描获取画面流、音频流、视频时长、音频时长、音量分贝
			bool fileOk = !string.IsNullOrWhiteSpace(videoPath) && File.Exists(videoPath) && new FileInfo(videoPath).Length >= 20480;
			bool hasVideoStream = false;
			bool hasAudioStream = false;
			double videoDur = 0;
			double audioDur = 0;

			if (fileOk && !string.IsNullOrWhiteSpace(ffmpegPath) && File.Exists(ffmpegPath))
			{
				try
				{
					string args = $"-hide_banner -i {ScriptBatchComposer.QuoteArg(videoPath)} -af \"volumedetect\" -f null -";
					var psi = new ProcessStartInfo
					{
						FileName = ffmpegPath,
						Arguments = args,
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardError = true,
						StandardErrorEncoding = Encoding.UTF8
					};
					using var proc = new Process { StartInfo = psi };
					proc.Start();
					string stderr = await proc.StandardError.ReadToEndAsync();
					using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
					{
						await Task.Run(() => proc.WaitForExit(45000), ct);
					}

					stderr = stderr ?? string.Empty;
					hasVideoStream = stderr.IndexOf("Video:", StringComparison.OrdinalIgnoreCase) >= 0;
					hasAudioStream = stderr.IndexOf("Audio:", StringComparison.OrdinalIgnoreCase) >= 0;

					var dm = DurationRegex.Match(stderr);
					if (dm.Success)
					{
						videoDur = int.Parse(dm.Groups[1].Value) * 3600
							+ int.Parse(dm.Groups[2].Value) * 60
							+ double.Parse(dm.Groups[3].Value, CultureInfo.InvariantCulture);
					}

					var timeMatches = TimeProgressRegex.Matches(stderr);
					if (timeMatches.Count > 0)
					{
						var lastTm = timeMatches[timeMatches.Count - 1];
						audioDur = int.Parse(lastTm.Groups[1].Value) * 3600
							+ int.Parse(lastTm.Groups[2].Value) * 60
							+ double.Parse(lastTm.Groups[3].Value, CultureInfo.InvariantCulture);
					}
					else
					{
						audioDur = videoDur;
					}

					var meanM = MeanVolumeRegex.Match(stderr);
					if (meanM.Success && double.TryParse(meanM.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double mv))
						report.MeanVolumeDb = mv;

					var maxM = MaxVolumeRegex.Match(stderr);
					if (maxM.Success && double.TryParse(maxM.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double xv))
						report.MaxVolumeDb = xv;
				}
				catch { }
			}

			report.VideoDurationSec = Math.Round(videoDur, 2);
			report.AudioDurationSec = Math.Round(audioDur, 2);

			// 维度 4：视频流与文件完整性
			report.VideoIntegrityPass = fileOk && hasVideoStream && videoDur >= 1.5;
			if (!report.VideoIntegrityPass)
				report.FailureReasons.Add(!fileOk ? "成品文件损坏或过小" : (!hasVideoStream ? "成品缺少视频画面轨" : $"视频时长过短({videoDur:0.0}s)"));

			// 维度 3：音频音量健康度（防静音/丢音）
			report.AudioHealthPass = hasAudioStream && report.MaxVolumeDb > -45.0 && report.MeanVolumeDb > -55.0;
			if (!report.AudioHealthPass)
				report.FailureReasons.Add(!hasAudioStream ? "成品缺少音频轨" : $"音频疑似静音/无声(峰值 {report.MaxVolumeDb:0.0}dB)");

			// 维度 5：音画时长同步（误差 <= 0.6s）
			report.AvSyncPass = hasAudioStream && videoDur > 0 && Math.Abs(videoDur - audioDur) <= 0.6;
			if (!report.AvSyncPass && hasAudioStream)
				report.FailureReasons.Add($"音画时长不同步(画面 {videoDur:0.0}s vs 音频 {audioDur:0.0}s)");

			// 维度 2：字幕时间轴、画面长度与全片（前/中/后三段）内容核验
			if (!burnSubtitlesEnabled)
			{
				report.SubtitleSyncPass = true;
			}
			else
			{
				bool hasWords = subtitleWordCount > 0;
				double lastWordEnd = hasWords ? lastSubtitleEndSec : 0;
				bool timeOk = hasWords && (videoDur <= 0 || lastWordEnd <= videoDur + 1.5);
				var badSections = new List<string>();
				if (timeOk && !string.IsNullOrWhiteSpace(standardScript) && !string.IsNullOrWhiteSpace(subtitleHeadText))
				{
					var stdAll = TokenizeForQc(standardScript, out _);
					var subAll = TokenizeForQc(subtitleHeadText, out _);
					if (stdAll.Count >= 12 && subAll.Count >= 6)
					{
						// 1. 首句锚点核验（防开头吞句）
						var stdHead = stdAll.Take(10).ToList();
						var subHeadSet = new HashSet<string>(subAll.Take(Math.Min(subAll.Count, 20)), StringComparer.OrdinalIgnoreCase);
						int headHits = stdHead.Count(t => subHeadSet.Contains(t));
						if (headHits < Math.Max(2, (int)Math.Ceiling(stdHead.Count * 0.4)))
							badSections.Add("首句不匹配");

						// 2. 全片前/中/后三段分区核验（防中段漏句、错位或结尾截断）
						string[] secNames = { "前段", "中段", "后段" };
						for (int sec = 0; sec < 3; sec++)
						{
							int sStart = stdAll.Count * sec / 3;
							int sEnd = stdAll.Count * (sec + 1) / 3;
							int sCount = Math.Max(1, sEnd - sStart);

							// 允许 ±12% 语速弹性窗口，避免正常语速变化误报
							int wStart = Math.Max(0, (int)Math.Floor(subAll.Count * (sec / 3.0 - 0.12)));
							int wEnd = Math.Min(subAll.Count, (int)Math.Ceiling(subAll.Count * ((sec + 1) / 3.0 + 0.12)));
							var subWin = new HashSet<string>(subAll.Skip(wStart).Take(Math.Max(1, wEnd - wStart)), StringComparer.OrdinalIgnoreCase);

							int hits = 0;
							for (int k = sStart; k < sEnd; k++)
							{
								if (subWin.Contains(stdAll[k])) hits++;
							}
							int secPct = (int)Math.Round(hits * 100.0 / sCount);
							if (secPct < 60)
							{
								badSections.Add($"{secNames[sec]}({secPct}%)");
							}
						}
					}
				}
				report.SubtitleSyncPass = timeOk && badSections.Count == 0;
				if (!timeOk)
					report.FailureReasons.Add(!hasWords ? "未生成有效字幕时间轴" : $"字幕超时溢出({lastWordEnd:0.0}s > {videoDur:0.0}s)");
				else if (badSections.Count > 0)
					report.FailureReasons.Add($"字幕内容存在错位或漏段：{string.Join("、", badSections)}");
			}

			// 维度 1：音频识别文本 ↔ 标准文案一致度
			int minThreshold = settings != null ? Math.Max(50, Math.Min(100, settings.QcMinScriptMatchPercent)) : 85;
			if (string.IsNullOrWhiteSpace(standardScript))
			{
				report.ScriptMatchPercent = 100;
				report.ScriptAudioPass = true;
			}
			else if (string.IsNullOrWhiteSpace(recognizedAudioText))
			{
				report.ScriptMatchPercent = 0;
				report.ScriptAudioPass = false;
				report.FailureReasons.Add("音频未识别出有效口播语音");
			}
			else
			{
				var stdTokens = TokenizeForQc(standardScript, out var stdOriginals);
				var recTokens = TokenizeForQc(recognizedAudioText, out _);

				if (stdTokens.Count == 0)
				{
					report.ScriptMatchPercent = 100;
					report.ScriptAudioPass = true;
				}
				else
				{
					var recCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
					foreach (var rt in recTokens)
					{
						recCounts[rt] = recCounts.TryGetValue(rt, out int c) ? c + 1 : 1;
					}

					int matched = 0;
					var missingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					for (int i = 0; i < stdTokens.Count; i++)
					{
						string tok = stdTokens[i];
						if (recCounts.TryGetValue(tok, out int cnt) && cnt > 0)
						{
							matched++;
							recCounts[tok] = cnt - 1;
						}
						else if (tok.Length >= 5 && recCounts.Any(kv => kv.Value > 0 && (kv.Key.StartsWith(tok.Substring(0, 4), StringComparison.OrdinalIgnoreCase) || tok.StartsWith(kv.Key.Substring(0, Math.Min(4, kv.Key.Length)), StringComparison.OrdinalIgnoreCase) && Math.Abs(kv.Key.Length - tok.Length) <= 2)))
						{
							matched++;
						}
						else
						{
							string orig = stdOriginals[i];
							if (missingSet.Add(tok) && report.MissingWords.Count < 15)
							{
								report.MissingWords.Add(orig);
							}
						}
					}

					report.ScriptMatchPercent = (int)Math.Round(matched * 100.0 / stdTokens.Count);
					report.ScriptAudioPass = report.ScriptMatchPercent >= minThreshold;
					if (!report.ScriptAudioPass)
					{
						report.FailureReasons.Add($"文案与音频一致度仅 {report.ScriptMatchPercent}% (低于合格线 {minThreshold}%)");
					}
				}
			}

			report.Passed = report.ScriptAudioPass
				&& report.SubtitleSyncPass
				&& report.AudioHealthPass
				&& report.VideoIntegrityPass
				&& report.AvSyncPass;
			return report;
		}

		/// <summary>
		/// 人工复核通过后强制上传云盘并回填表格（按云端保留设置自动决定是否清理临时文件）
		/// </summary>
		public static async Task<bool> ForceUploadAndWriteBackAsync(
			SmartComposeTaskItem task,
			SmartComposeSettings settings,
			SmartComposeCallbacks cb,
			CancellationToken ct)
		{
			if (task == null || string.IsNullOrWhiteSpace(task.OutputFilePath) || !File.Exists(task.OutputFilePath))
			{
				throw new FileNotFoundException("未找到本地待上传成品文件，无法执行人工放行上传。");
			}

			string localFile = task.OutputFilePath;
			task.StageIndex = 6;
			task.StageText = "人工放行 · 正在上传云盘...";
			task.Status = "上传云盘中";
			task.ProgressPercent = 88;
			cb?.OnStageUpdate?.Invoke(6, $"人工放行：正在上传《{Path.GetFileName(localFile)}》到谷歌云盘...", 88);
			cb?.OnTaskRowRefresh?.Invoke();

			string shareUrl = await GoogleWorkspaceClient.UploadFileAndShareAsync(
				settings,
				localFile,
				settings.TargetDriveFolderUrl,
				upPct =>
				{
					int p = 88 + (int)(upPct * 0.10);
					task.ProgressPercent = p;
					cb?.OnStageUpdate?.Invoke(6, $"人工放行上传中 ({upPct}%)...", p);
				},
				ct
			);
			task.UploadedDriveUrl = shareUrl;

			if (settings.AutoWriteBackSheet && task.SheetRowNumber > 1)
			{
				task.StageText = "人工放行 · 正在回填表格...";
				cb?.OnStageUpdate?.Invoke(6, $"正在将成品链接回填到表格第 {task.SheetRowNumber} 行...", 98);
				await GoogleWorkspaceClient.UpdateSheetRowAsync(settings, task.SheetRowNumber, shareUrl, "人工放行-已完成", ct);
			}

			bool cloudUploadOnlyTemp = settings.SourceMode == 0 && settings.AutoUploadToDrive && !settings.KeepLocalCopyInCloudMode;
			if (cloudUploadOnlyTemp)
			{
				try { if (File.Exists(localFile)) File.Delete(localFile); } catch { }
				task.OutputFilePath = string.Empty;
			}

			string taskWorkDir = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"VideoBatchStudio",
				"SmartComposeWork",
				SanitizeName($"{task.Index}_{task.TaskName}")
			);
			CleanupWorkDirCompletely(taskWorkDir);

			task.ProgressPercent = 100;
			task.StageIndex = 6;
			task.StageText = "人工放行已上传";
			task.Status = "已完成";
			task.QcPassed = true;
			task.QcSummary = "✅ 人工放行";
			cb?.OnStageUpdate?.Invoke(6, $"《{task.TaskName}》已人工放行并上传回填完成！", 100);
			cb?.OnTaskRowRefresh?.Invoke();

			cb?.OnLog?.Invoke(new PlainLogEntry
			{
				Level = PlainLogLevel.Success,
				Stage = "人工放行",
				Title = $"《{task.TaskName}》经人工复核放行，已上传云盘并回填表格！",
				PlainCause = cloudUploadOnlyTemp ? "已上传谷歌云盘并清理本地临时文件（仅存云端）。" : $"已上传谷歌云盘并保留本地成品：{Path.GetFileName(localFile)}"
			});
			return true;
		}

		private static void CleanupWorkDirCompletely(string workDir)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(workDir) && Directory.Exists(workDir))
				{
					Directory.Delete(workDir, true);
				}
			}
			catch { }
		}

		private static void CleanupHeavyIntermediatesOnly(string workDir)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(workDir) || !Directory.Exists(workDir)) return;
				foreach (var f in Directory.GetFiles(workDir, "seg_*.mp4"))
				{
					try { File.Delete(f); } catch { }
				}
				foreach (var name in new[] { "merged_clean.mp4", "concat_list.txt" })
				{
					string p = Path.Combine(workDir, name);
					if (File.Exists(p)) { try { File.Delete(p); } catch { } }
				}
			}
			catch { }
		}

		private static void SweepExpiredWorkDirs()
		{
			try
			{
				string root = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"VideoBatchStudio",
					"SmartComposeWork"
				);
				if (!Directory.Exists(root)) return;
				DateTime cutoff = DateTime.Now.AddDays(-3);
				foreach (string dir in Directory.GetDirectories(root))
				{
					try
					{
						if (Directory.GetLastWriteTime(dir) < cutoff)
						{
							Directory.Delete(dir, true);
						}
					}
					catch { }
				}
			}
			catch { }
		}

		private static string StripDiacriticsAndPunct(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return string.Empty;
			string formD = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
			var sb = new StringBuilder(formD.Length);
			foreach (char ch in formD)
			{
				var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
				if (cat == UnicodeCategory.NonSpacingMark) continue;
				if (char.IsLetterOrDigit(ch)) sb.Append(ch);
			}
			return sb.ToString();
		}

		private static List<string> TokenizeForQc(string text, out List<string> originals)
		{
			var tokens = new List<string>();
			originals = new List<string>();
			if (string.IsNullOrWhiteSpace(text)) return tokens;

			// 检测是否主要是无空格 CJK 文本
			int cjkCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
			if (cjkCount > text.Length * 0.35)
			{
				foreach (char c in text)
				{
					if (c >= 0x4E00 && c <= 0x9FFF)
					{
						string s = c.ToString();
						tokens.Add(s);
						originals.Add(s);
					}
				}
				return tokens;
			}

			foreach (string raw in Regex.Split(text.Trim(), @"\s+"))
			{
				string norm = StripDiacriticsAndPunct(raw);
				if (string.IsNullOrEmpty(norm)) continue;
				tokens.Add(norm);
				originals.Add(raw.Trim(',', '.', '!', '?', ';', ':', '"', '\'', '“', '”', '。', '，'));
			}
			return tokens;
		}

		private static async Task<List<string>> AcquireClipsAsync(
			SmartComposeTaskItem task,
			SmartComposeSettings settings,
			string workDir,
			SmartComposeCallbacks cb,
			CancellationToken ct)
		{
			if (!string.IsNullOrWhiteSpace(task.LocalSourceFolder) && Directory.Exists(task.LocalSourceFolder))
			{
				return GetSortedVideosInDir(task.LocalSourceFolder);
			}

			string clipDir = Path.Combine(workDir, "clips");
			Directory.CreateDirectory(clipDir);
			var results = new List<string>();
			int fileIdx = 0;

			foreach (string rawUrl in task.SourceUrls)
			{
				ct.ThrowIfCancellationRequested();
				string driveId = GoogleWorkspaceClient.ExtractDriveId(rawUrl, out bool isFolder);
				if (!string.IsNullOrWhiteSpace(driveId) && isFolder)
				{
					var files = await GoogleWorkspaceClient.ListFolderVideosAsync(settings, driveId, ct);
					if (files.Count == 0)
					{
						throw new InvalidOperationException("该谷歌云盘文件夹里没有找到任何视频文件，请确认已上传 MP4/MOV 片段。");
					}
					// 3 路并行下载云盘片段（保持原顺序），大幅减少 21 个小文件逐个握手的往返耗时
					var paths = new string[files.Count];
					var dlJobs = new List<Task>();
					var dlSlots = new SemaphoreSlim(3, 3);
					int dlDone = 0;
					int needCount = 0;
					for (int i = 0; i < files.Count; i++)
					{
						var f = files[i];
						fileIdx++;
						string localPath = Path.Combine(clipDir, $"{fileIdx:D3}_{SanitizeName(f.Name)}");
						string sigPath = localPath + ".sig";
						string remoteSig = $"{f.Id}|{f.Size}|{f.Md5Checksum}|{f.ModifiedTime}";
						paths[i] = localPath;

						bool needDownload = !File.Exists(localPath)
							|| new FileInfo(localPath).Length < 2048
							|| (f.Size > 0 && new FileInfo(localPath).Length != f.Size)
							|| !File.Exists(sigPath)
							|| !string.Equals(File.ReadAllText(sigPath).Trim(), remoteSig, StringComparison.Ordinal);

						if (!needDownload) continue;
						needCount++;
						string fid = f.Id;
						int totalFiles = files.Count;
						dlJobs.Add(Task.Run(async () =>
						{
							await dlSlots.WaitAsync(ct).ConfigureAwait(false);
							try
							{
								await GoogleWorkspaceClient.DownloadDriveFileAsync(settings, fid, localPath, null, ct).ConfigureAwait(false);
								try { File.WriteAllText(sigPath, remoteSig, new UTF8Encoding(false)); } catch { }
								int d = Interlocked.Increment(ref dlDone);
								string yieldHint = GoogleWorkspaceClient.IsUploadRunning ? "（后台上传已自动让行下载）" : string.Empty;
								cb?.OnStageUpdate?.Invoke(2, $"正在 3 路并行下载云盘片段 {d}/{needCount}{yieldHint}", 12 + (int)(d * 16.0 / Math.Max(1, needCount)));
							}
							finally
							{
								dlSlots.Release();
							}
						}, ct));
					}
					if (dlJobs.Count > 0)
					{
						cb?.OnStageUpdate?.Invoke(2, $"正在 3 路并行下载云盘片段 0/{needCount}...", 12);
						await Task.WhenAll(dlJobs);
					}
					results.AddRange(paths);
				}
				else if (!string.IsNullOrWhiteSpace(driveId))
				{
					fileIdx++;
					string localPath = Path.Combine(clipDir, $"{fileIdx:D3}_{driveId}.mp4");
					cb?.OnStageUpdate?.Invoke(2, $"正在下载最新云盘视频片段 #{fileIdx}...", 18);
					await GoogleWorkspaceClient.DownloadDriveFileAsync(settings, driveId, localPath, null, ct);
					results.Add(localPath);
				}
				else
				{
					// 其他平台直链（YouTube/FB/TikTok/IG）复用 DownloadEngine
					var dlItem = new DownloadItem { Url = rawUrl, Platform = DownloadEngine.IdentifyPlatform(rawUrl).Platform };
					bool ok = await DownloadEngine.DownloadAsync(
						dlItem,
						DownloadEngine.FindYtDlpPath(),
						DownloadEngine.FindFFmpegPath(),
						clipDir,
						"1080p",
						true,
						true,
						false,
						settings.ProxyUrl,
						null,
						ct
					);
					if (ok && !string.IsNullOrEmpty(dlItem.SaveFilePath) && File.Exists(dlItem.SaveFilePath))
					{
						results.Add(dlItem.SaveFilePath);
					}
				}
			}

			return results;
		}

		private static async Task<ClipAnalysis> AnalyzeSingleClipAsync(
			string videoPath,
			int index,
			string ffmpegPath,
			string whisperCli,
			string modelPath,
			SmartComposeSettings settings,
			string standardScript,
			CancellationToken ct)
		{
			var analysis = new ClipAnalysis
			{
				FilePath = videoPath,
				FileName = Path.GetFileName(videoPath),
				OriginalIndex = index
			};

			// 1. 探测时长与静音区间 (silencedetect)
			var (dur, silences) = await ProbeDurationAndSilencesAsync(videoPath, ffmpegPath, settings.SilenceThresholdDb, settings.MinSilenceDurationSec, ct);
			analysis.OriginalDuration = Math.Max(0.2, dur);

			// 2. Whisper 逐词识别
			if (!string.IsNullOrEmpty(whisperCli) && !string.IsNullOrEmpty(modelPath))
			{
				string wavPath = null;
				try
				{
					wavPath = await TranscribeEngine.Extract16kWavAsync(videoPath, ffmpegPath, null, ct);
					// 绝对不要把标准文案开头 220 字传给 Whisper --prompt，否则 whisper.cpp 会误以为开头已读过而直接跳过前几句！
					var segs = await TranscribeEngine.TranscribeWithWordsAsync(
						wavPath,
						whisperCli,
						modelPath,
						settings.WhisperLanguage,
						string.Empty,
						null,
						ct
					);

					var sbText = new StringBuilder();
					foreach (var seg in segs)
					{
						if (seg.Words != null && seg.Words.Count > 0)
						{
							foreach (var w in seg.Words)
							{
								string wt = (w.Text ?? string.Empty).Trim();
								if (string.IsNullOrEmpty(wt)) continue;
								analysis.Words.Add(new WordPoint { Text = wt, Start = w.StartSec, End = Math.Max(w.StartSec + 0.04, w.EndSec) });
								if (sbText.Length > 0) sbText.Append(' ');
								sbText.Append(wt);
							}
						}
						else if (!string.IsNullOrWhiteSpace(seg.OriginalText))
						{
							var tokens = seg.OriginalText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
							double segStart = seg.StartTime.TotalSeconds;
							double segDur = Math.Max(0.1, seg.EndTime.TotalSeconds - segStart);
							for (int ti = 0; ti < tokens.Length; ti++)
							{
								double ws = segStart + segDur * ti / tokens.Length;
								double we = segStart + segDur * (ti + 1) / tokens.Length;
								analysis.Words.Add(new WordPoint { Text = tokens[ti], Start = ws, End = we });
							}
							if (sbText.Length > 0) sbText.Append(' ');
							sbText.Append(seg.OriginalText.Trim());
						}
					}
					analysis.RecognizedText = sbText.ToString();
				}
				catch { }
				finally
				{
					try { if (!string.IsNullOrEmpty(wavPath) && File.Exists(wavPath)) File.Delete(wavPath); } catch { }
				}
			}

			// 3. 计算需要裁剪掉的区间（静音长停顿 + 语气词）
			var cutIntervals = new List<(double Start, double End)>();
			if (settings.RemoveSilenceEnabled)
			{
				double pad = Math.Max(0.02, Math.Min(0.20, settings.KeepEdgePaddingSec));
				foreach (var s in silences)
				{
					double cs = s.Start <= 0.05 ? 0.0 : s.Start + pad;
					double ce = s.End >= analysis.OriginalDuration - 0.05 ? analysis.OriginalDuration : s.End - pad;
					if (ce - cs >= 0.08)
					{
						cutIntervals.Add((cs, ce));
					}
				}
			}

			if (settings.RemoveFillersEnabled && analysis.Words.Count > 0 && !string.IsNullOrWhiteSpace(settings.FillerWordsCsv))
			{
				var fillers = new HashSet<string>(
					settings.FillerWordsCsv.Split(new[] { ',', '，', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(x => NormalizeWord(x)),
					StringComparer.OrdinalIgnoreCase);

				foreach (var wd in analysis.Words)
				{
					string norm = NormalizeWord(wd.Text);
					if (!string.IsNullOrEmpty(norm) && fillers.Contains(norm) && (wd.End - wd.Start) >= 0.08)
					{
						cutIntervals.Add((wd.Start, wd.End));
					}
				}
			}

			// 4. 将裁剪区间反转为“保留区间 (KeptRanges)”
			analysis.KeptRanges = InvertToKeptRanges(analysis.OriginalDuration, cutIntervals);
			double keptDur = analysis.KeptRanges.Sum(r => r.End - r.Start);
			analysis.TrimmedSeconds = Math.Max(0, analysis.OriginalDuration - keptDur);
			return analysis;
		}

		private static List<(double Start, double End)> InvertToKeptRanges(double totalDur, List<(double Start, double End)> cuts)
		{
			if (cuts.Count == 0) return new List<(double, double)> { (0.0, totalDur) };

			var sorted = cuts
				.Select(c => (Start: Math.Max(0, c.Start), End: Math.Min(totalDur, c.End)))
				.Where(c => c.End > c.Start + 0.04)
				.OrderBy(c => c.Start)
				.ToList();

			var merged = new List<(double Start, double End)>();
			foreach (var c in sorted)
			{
				if (merged.Count == 0 || c.Start > merged[merged.Count - 1].End + 0.05)
					merged.Add(c);
				else
					merged[merged.Count - 1] = (merged[merged.Count - 1].Start, Math.Max(merged[merged.Count - 1].End, c.End));
			}

			var kept = new List<(double Start, double End)>();
			double cursor = 0.0;
			foreach (var c in merged)
			{
				if (c.Start - cursor >= 0.12)
				{
					kept.Add((cursor, c.Start));
				}
				cursor = Math.Max(cursor, c.End);
			}
			if (totalDur - cursor >= 0.12)
			{
				kept.Add((cursor, totalDur));
			}

			// 保底：若全片都是轻声音乐/误判全切，则保留整段避免丢片段
			if (kept.Count == 0) kept.Add((0.0, totalDur));
			return kept;
		}

		private static void ReorderClipsByStandardScript(List<ClipAnalysis> analyses, string standardScript)
		{
			var scriptTokens = TokenizeNormalized(standardScript);
			if (scriptTokens.Count < 3) return;

			foreach (var clip in analyses)
			{
				var clipTokens = TokenizeNormalized(clip.RecognizedText);
				if (clipTokens.Count == 0)
				{
					clip.ScriptOrderScore = int.MaxValue - 1000 + clip.OriginalIndex;
					continue;
				}

				int bestPos = int.MaxValue - 1000 + clip.OriginalIndex;
				int bestMatches = 0;
				int winSize = Math.Min(scriptTokens.Count, Math.Max(clipTokens.Count + 4, 8));
				var clipSet = new HashSet<string>(clipTokens, StringComparer.OrdinalIgnoreCase);

				for (int i = 0; i <= scriptTokens.Count - Math.Min(3, scriptTokens.Count); i++)
				{
					int hits = 0;
					int end = Math.Min(scriptTokens.Count, i + winSize);
					for (int j = i; j < end; j++)
					{
						if (clipSet.Contains(scriptTokens[j])) hits++;
					}
					if (hits > bestMatches)
					{
						bestMatches = hits;
						bestPos = i;
					}
				}
				clip.ScriptOrderScore = bestMatches >= 2 ? bestPos : (int.MaxValue - 1000 + clip.OriginalIndex);
			}

			analyses.Sort((a, b) =>
			{
				int cmp = a.ScriptOrderScore.CompareTo(b.ScriptOrderScore);
				return cmp != 0 ? cmp : a.OriginalIndex.CompareTo(b.OriginalIndex);
			});
		}

		private static List<WordPoint> BuildMergedWordTimeline(List<ClipAnalysis> analyses)
		{
			var timeline = new List<WordPoint>();
			double timelineOffset = 0.0;

			foreach (var clip in analyses)
			{
				foreach (var range in clip.KeptRanges)
				{
					double rangeLen = range.End - range.Start;
					foreach (var wd in clip.Words)
					{
						double mid = (wd.Start + wd.End) * 0.5;
						if (mid >= range.Start && mid <= range.End)
						{
							double ns = timelineOffset + Math.Max(0, wd.Start - range.Start);
							double ne = timelineOffset + Math.Min(rangeLen, Math.Max(0.04, wd.End - range.Start));
							if (ne <= ns + 0.02) ne = ns + 0.05;
							timeline.Add(new WordPoint { Text = wd.Text, Start = ns, End = ne });
						}
					}
					timelineOffset += rangeLen;
				}
			}
			return timeline;
		}

		/// <summary>
		/// 动态规划锚点词对齐：只用标准文案修正 Whisper 识别的拼写/重音/标点，绝对不改动每个词的真实口播时间戳！
		/// 即使音频中间漏掉整段或重复某句，也不会把后续所有字幕拉偏，彻底杜绝“时快时慢、音字错位”。
		/// </summary>
		private static void AlignTimelineTextToStandardScript(List<WordPoint> timeline, string standardScript)
		{
			var stdWords = Regex.Split((standardScript ?? string.Empty).Trim(), @"\s+")
				.Where(w => !string.IsNullOrWhiteSpace(w))
				.ToList();
			if (stdWords.Count == 0 || timeline.Count == 0) return;

			int n = timeline.Count;
			int m = stdWords.Count;
			if ((long)n * m > 4_000_000) return; // 超长安全保护

			var normRec = new string[n];
			for (int i = 0; i < n; i++) normRec[i] = StripDiacriticsAndPunct(timeline[i].Text);
			var normStd = new string[m];
			for (int j = 0; j < m; j++) normStd[j] = StripDiacriticsAndPunct(stdWords[j]);

			int cols = m + 1;
			var dp = new int[(n + 1) * cols];
			var trace = new byte[(n + 1) * cols]; // 1 = diag, 2 = up (skip rec), 3 = left (skip std)

			const int gapPenalty = -1;
			for (int i = 1; i <= n; i++) { dp[i * cols] = i * gapPenalty; trace[i * cols] = 2; }
			for (int j = 1; j <= m; j++) { dp[j] = j * gapPenalty; trace[j] = 3; }

			for (int i = 1; i <= n; i++)
			{
				int rowOff = i * cols;
				int prevRowOff = (i - 1) * cols;
				string a = normRec[i - 1];
				for (int j = 1; j <= m; j++)
				{
					int matchScore = ScoreWordAlignment(a, normStd[j - 1]);
					int diag = dp[prevRowOff + (j - 1)] + matchScore;
					int up = dp[prevRowOff + j] + gapPenalty;
					int left = dp[rowOff + (j - 1)] + gapPenalty;

					if (diag >= up && diag >= left)
					{
						dp[rowOff + j] = diag;
						trace[rowOff + j] = 1;
					}
					else if (up >= left)
					{
						dp[rowOff + j] = up;
						trace[rowOff + j] = 2;
					}
					else
					{
						dp[rowOff + j] = left;
						trace[rowOff + j] = 3;
					}
				}
			}

			// 回溯：仅对成功锚定匹配的词替换标准文案拼写，时间戳保持原样一毫秒不动
			int ci = n, cj = m;
			while (ci > 0 && cj > 0)
			{
				byte dir = trace[ci * cols + cj];
				if (dir == 1)
				{
					if (ScoreWordAlignment(normRec[ci - 1], normStd[cj - 1]) > 0)
					{
						timeline[ci - 1].Text = stdWords[cj - 1];
					}
					ci--;
					cj--;
				}
				else if (dir == 2)
				{
					ci--;
				}
				else
				{
					cj--;
				}
			}
		}

		private static int ScoreWordAlignment(string a, string b)
		{
			if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return -3;
			if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return 4;
			int maxLen = Math.Max(a.Length, b.Length);
			if (maxLen < 3) return -3;
			int dist = LevenshteinDistance(a, b);
			double sim = 1.0 - (double)dist / maxLen;
			if (sim >= 0.65) return 2;
			return -3;
		}

		private static int LevenshteinDistance(string a, string b)
		{
			int la = a.Length, lb = b.Length;
			var prev = new int[lb + 1];
			var curr = new int[lb + 1];
			for (int j = 0; j <= lb; j++) prev[j] = j;
			for (int i = 1; i <= la; i++)
			{
				curr[0] = i;
				char ca = a[i - 1];
				for (int j = 1; j <= lb; j++)
				{
					int cost = ca == b[j - 1] ? 0 : 1;
					int del = prev[j] + 1;
					int ins = curr[j - 1] + 1;
					int sub = prev[j - 1] + cost;
					curr[j] = Math.Min(Math.Min(del, ins), sub);
				}
				var tmp = prev; prev = curr; curr = tmp;
			}
			return prev[lb];
		}

		private static void WriteSrtAndWordsJson(string srtPath, List<WordPoint> words)
		{
			var sbSrt = new StringBuilder();
			var jsonList = new List<Dictionary<string, object>>();
			int cueIndex = 0;

			int i = 0;
			while (i < words.Count)
			{
				var chunk = new List<WordPoint>();
				while (i < words.Count && chunk.Count < 6)
				{
					chunk.Add(words[i]);
					string t = words[i].Text;
					i++;
					if (t.EndsWith(".") || t.EndsWith("!") || t.EndsWith("?") || t.EndsWith("。") || t.EndsWith("！") || t.EndsWith("？") ||
					    (chunk.Count >= 3 && (t.EndsWith(",") || t.EndsWith("，") || t.EndsWith(";"))))
					{
						break;
					}
				}
				if (chunk.Count == 0) continue;

				cueIndex++;
				double start = chunk[0].Start;
				double end = Math.Max(start + 0.1, chunk[chunk.Count - 1].End);
				sbSrt.AppendLine(cueIndex.ToString());
				sbSrt.AppendLine($"{FormatSrtTs(start)} --> {FormatSrtTs(end)}");
				sbSrt.AppendLine(string.Join(" ", chunk.Select(c => c.Text)));
				sbSrt.AppendLine();
			}

			foreach (var w in words)
			{
				jsonList.Add(new Dictionary<string, object>
				{
					["start"] = Math.Round(w.Start, 3),
					["end"] = Math.Round(w.End, 3),
					["text"] = w.Text
				});
			}

			File.WriteAllText(srtPath, sbSrt.ToString(), new UTF8Encoding(false));
			File.WriteAllText(srtPath + ".words.json", new JavaScriptSerializer().Serialize(jsonList), new UTF8Encoding(false));
		}

		private static async Task<(List<WordPoint> CalibratedWords, string AssPath, string RawRecognizedText)> RenderFinalVideoAsync(
			List<ClipAnalysis> analyses,
			string standardScript,
			ScriptBatchSettings subCopy,
			string watermarkPngPath,
			string outputPath,
			string ffmpegPath,
			string whisperCli,
			string modelPath,
			SmartComposeSettings settings,
			string workDir,
			Action<int> progressCallback,
			CancellationToken ct)
		{
			int w = settings.TargetWidth > 0 ? settings.TargetWidth : 1080;
			int h = settings.TargetHeight > 0 ? settings.TargetHeight : 1920;
			double fadeSec = Math.Max(0.0, Math.Min(0.06, settings.AudioCrossfadeMs / 1000.0));

			var segFiles = new List<string>();
			var renderedSegments = new List<(ClipAnalysis Clip, double RangeStart, double RangeEnd, double ActualDur)>();
			int segCounter = 0;
			int totalRanges = Math.Max(1, analyses.Sum(a => a.KeptRanges.Count));

			// 1. 逐段精准裁切气口并统一分辨率/帧率/音频采样率，同时捕获每段真实编码时长，彻底杜绝长视频累计漂移
			foreach (var clip in analyses)
			{
				foreach (var range in clip.KeptRanges)
				{
					ct.ThrowIfCancellationRequested();
					segCounter++;
					string segOut = Path.Combine(workDir, $"seg_{segCounter:D4}.mp4");
					double dur = Math.Max(0.1, range.End - range.Start);
					string af = fadeSec > 0.005 && dur > fadeSec * 3
						? $"afade=t=in:st=0:d={fadeSec.ToString("0.000", CultureInfo.InvariantCulture)},afade=t=out:st={(dur - fadeSec).ToString("0.000", CultureInfo.InvariantCulture)}:d={fadeSec.ToString("0.000", CultureInfo.InvariantCulture)},aresample=44100"
						: "aresample=44100";

					string vf = $"scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},setsar=1,fps=30";
					string args = $"-hide_banner -y -ss {range.Start.ToString("0.000", CultureInfo.InvariantCulture)} -t {dur.ToString("0.000", CultureInfo.InvariantCulture)} -i {ScriptBatchComposer.QuoteArg(clip.FilePath)} " +
					              $"-vf \"{vf}\" -af \"{af}\" -c:v libx264 -preset ultrafast -crf 17 -pix_fmt yuv420p -c:a aac -b:a 192k -ar 44100 -ac 2 {ScriptBatchComposer.QuoteArg(segOut)}";

					var (ok, stderr) = await RunFfmpegCaptureAsync(ffmpegPath, args, ct);
					if (ok && File.Exists(segOut) && new FileInfo(segOut).Length > 1024)
					{
						double actualDur = ParseLastTimeFromStderr(stderr);
						if (actualDur < 0.05) actualDur = dur;
						segFiles.Add(segOut);
						renderedSegments.Add((clip, range.Start, range.End, actualDur));
					}
					progressCallback?.Invoke((int)(segCounter * 45.0 / totalRanges));
				}
			}

			if (segFiles.Count == 0)
				throw new InvalidOperationException("精剪片段生成失败，请检查原始视频编码是否完整。");

			// 2. 拼接所有精剪片段
			string concatListPath = Path.Combine(workDir, "concat_list.txt");
			var listLines = segFiles.Select(f => $"file '{f.Replace("'", "'\\''")}'");
			File.WriteAllLines(concatListPath, listLines, new UTF8Encoding(false));

			string mergedTemp = Path.Combine(workDir, "merged_clean.mp4");
			string concatArgs = $"-hide_banner -y -f concat -safe 0 -i {ScriptBatchComposer.QuoteArg(concatListPath)} -c copy {ScriptBatchComposer.QuoteArg(mergedTemp)}";
			var (concatOk, concatErr) = await RunFfmpegCaptureAsync(ffmpegPath, concatArgs, ct);
			if (!concatOk)
			{
				throw new InvalidOperationException("拼接精剪视频片段失败。");
			}
			progressCallback?.Invoke(58);

			// 2.5 直接对拼接好的成片音轨执行一次 Whisper 逐词时间轴对齐（毫秒级咬合真实人声，彻底消灭多段拼接漂移与时快时慢）
			var mergedWords = new List<WordPoint>();
			string rawFinalRecognized = string.Empty;
			if ((settings.BurnSubtitlesEnabled || settings.QcEnabled) && !string.IsNullOrEmpty(whisperCli) && !string.IsNullOrEmpty(modelPath))
			{
				string mergedWav = null;
				try
				{
					mergedWav = await TranscribeEngine.Extract16kWavAsync(mergedTemp, ffmpegPath, null, ct);
					// 传空 prompt 避免 whisper.cpp --carry-initial-prompt 误跳过视频开头 220 字
					var finalSegs = await TranscribeEngine.TranscribeWithWordsAsync(
						mergedWav,
						whisperCli,
						modelPath,
						settings.WhisperLanguage,
						string.Empty,
						null,
						ct
					);
					foreach (var seg in finalSegs)
					{
						if (seg.Words != null && seg.Words.Count > 0)
						{
							foreach (var wpt in seg.Words)
							{
								string wt = (wpt.Text ?? string.Empty).Trim();
								if (string.IsNullOrEmpty(wt)) continue;
								mergedWords.Add(new WordPoint { Text = wt, Start = wpt.StartSec, End = Math.Max(wpt.StartSec + 0.04, wpt.EndSec) });
							}
						}
						else if (!string.IsNullOrWhiteSpace(seg.OriginalText))
						{
							var tokens = seg.OriginalText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
							double segStart = seg.StartTime.TotalSeconds;
							double segDur = Math.Max(0.1, seg.EndTime.TotalSeconds - segStart);
							for (int ti = 0; ti < tokens.Length; ti++)
							{
								double ws = segStart + segDur * ti / tokens.Length;
								double we = segStart + segDur * (ti + 1) / tokens.Length;
								mergedWords.Add(new WordPoint { Text = tokens[ti], Start = ws, End = we });
							}
						}
					}
				}
				catch { }
				finally
				{
					try { if (!string.IsNullOrEmpty(mergedWav) && File.Exists(mergedWav)) File.Delete(mergedWav); } catch { }
				}
			}

			// 若成片直出听写为空，则回退使用逐段真实时长校准时间轴
			double timelineOffset = 0.0;
			if (mergedWords.Count == 0)
			{
				foreach (var seg in renderedSegments)
				{
					double expectedLen = Math.Max(0.05, seg.RangeEnd - seg.RangeStart);
					double actualLen = Math.Max(0.05, seg.ActualDur);
					double scale = actualLen / expectedLen;
					foreach (var wd in seg.Clip.Words)
					{
						double mid = (wd.Start + wd.End) * 0.5;
						if (mid >= seg.RangeStart && mid <= seg.RangeEnd)
						{
							double relStart = Math.Max(0, (wd.Start - seg.RangeStart) * scale);
							double relEnd = Math.Min(actualLen, Math.Max(relStart + 0.04, (wd.End - seg.RangeStart) * scale));
							mergedWords.Add(new WordPoint { Text = wd.Text, Start = timelineOffset + relStart, End = timelineOffset + relEnd });
						}
					}
					timelineOffset += actualLen;
				}
			}
			else
			{
				timelineOffset = renderedSegments.Sum(s => s.ActualDur);
			}

			if (mergedWords.Count > 0)
			{
				rawFinalRecognized = string.Join(" ", mergedWords.Select(wpt => wpt.Text));
			}

			if (!string.IsNullOrWhiteSpace(standardScript) && mergedWords.Count > 0)
			{
				AlignTimelineTextToStandardScript(mergedWords, standardScript);
			}

			double realMergedDur = ParseLastTimeFromStderr(concatErr);
			double maxAllowedEnd = realMergedDur > 0.5 ? realMergedDur : timelineOffset;
			if (mergedWords.Count > 0 && maxAllowedEnd > 0.5)
			{
				double lastEnd = mergedWords[mergedWords.Count - 1].End;
				if (lastEnd > maxAllowedEnd)
				{
					double clampScale = Math.Max(0.5, (maxAllowedEnd - 0.05) / lastEnd);
					foreach (var wpt in mergedWords)
					{
						wpt.Start = Math.Min(maxAllowedEnd - 0.06, wpt.Start * clampScale);
						wpt.End = Math.Min(maxAllowedEnd - 0.02, Math.Max(wpt.Start + 0.04, wpt.End * clampScale));
					}
				}
			}

			string assPath = null;
			if (settings.BurnSubtitlesEnabled && mergedWords.Count > 0)
			{
				string srtPath = Path.Combine(workDir, "aligned_subtitles.srt");
				WriteSrtAndWordsJson(srtPath, mergedWords);
				assPath = Path.Combine(workDir, "aligned_subtitles.ass");
				ScriptBatchComposer.BuildAssSubtitleFile(srtPath, assPath, subCopy);
			}
			progressCallback?.Invoke(62);

			// 3. 烧录爆款 ASS 字幕 + 可选水印叠加 + 可选人声响度均衡 (-14 LUFS) + 可选 BGM 混音（优先 NVENC 显卡加速，自动回退 CPU）
			bool hasAss = !string.IsNullOrEmpty(assPath) && File.Exists(assPath);
			bool hasWm = !string.IsNullOrEmpty(watermarkPngPath) && File.Exists(watermarkPngPath);
			bool hasBgm = settings.BgmEnabled && !string.IsNullOrWhiteSpace(settings.BgmPath) && File.Exists(settings.BgmPath);
			bool hasLoudnorm = settings.LoudnormEnabled;

			if (!hasAss && !hasWm && !hasBgm && !hasLoudnorm)
			{
				File.Copy(mergedTemp, outputPath, true);
				progressCallback?.Invoke(100);
				return (mergedWords, assPath, rawFinalRecognized);
			}

			var inputsSb = new StringBuilder();
			inputsSb.Append($"-i {ScriptBatchComposer.QuoteArg(mergedTemp)} ");
			int nextIdx = 1;
			int wmIdx = -1;
			int bgmIdx = -1;
			if (hasWm)
			{
				wmIdx = nextIdx++;
				inputsSb.Append($"-i {ScriptBatchComposer.QuoteArg(watermarkPngPath)} ");
			}
			if (hasBgm)
			{
				bgmIdx = nextIdx++;
				inputsSb.Append($"-stream_loop -1 -i {ScriptBatchComposer.QuoteArg(settings.BgmPath)} ");
			}

			var fc = new StringBuilder();
			string curV = "0:v";
			if (hasAss)
			{
				string escapedAss = EscapeFfmpegFilterPath(assPath);
				fc.Append($"[{curV}]ass='{escapedAss}'[vsub];");
				curV = "vsub";
			}
			if (hasWm)
			{
				string curVBracket = curV == "0:v" ? "[0:v]" : $"[{curV}]";
				fc.Append($"{curVBracket}[{wmIdx}:v]overlay=0:0:format=auto[vout];");
				curV = "vout";
			}
			string mapV = curV == "0:v" ? "0:v" : $"[{curV}]";

			string mapA = "0:a?";
			if (hasLoudnorm && hasBgm)
			{
				double bgmVol = Math.Max(0.02, Math.Min(0.8, settings.BgmVolumePercent / 100.0));
				fc.Append($"[0:a]loudnorm=I=-14:TP=-1.5:LRA=11[anorm];[{bgmIdx}:a]volume={bgmVol.ToString("0.00", CultureInfo.InvariantCulture)}[bgm];[anorm][bgm]amix=inputs=2:duration=first:dropout_transition=2[aout];");
				mapA = "[aout]";
			}
			else if (hasLoudnorm)
			{
				fc.Append("[0:a]loudnorm=I=-14:TP=-1.5:LRA=11[aout];");
				mapA = "[aout]";
			}
			else if (hasBgm)
			{
				double bgmVol = Math.Max(0.02, Math.Min(0.8, settings.BgmVolumePercent / 100.0));
				fc.Append($"[{bgmIdx}:a]volume={bgmVol.ToString("0.00", CultureInfo.InvariantCulture)}[bgm];[0:a][bgm]amix=inputs=2:duration=first:dropout_transition=2[aout];");
				mapA = "[aout]";
			}

			string fcStr = fc.ToString().TrimEnd(';');
			string filterPart = string.IsNullOrEmpty(fcStr) ? string.Empty : $"-filter_complex \"{fcStr}\" ";
			string mapPart = $"-map \"{mapV}\" -map \"{mapA}\" ";
			string inputs = inputsSb.ToString().TrimEnd();

			// 按画质/速度三档选择编码码率（0=极速 3.0M, 1=均衡 4.5M, 2=臻彩 6.5M）
			string nvencRate = settings.ExportBitratePreset switch
			{
				1 => "-rc vbr -cq 24 -b:v 4500k -maxrate 6200k -bufsize 12000k",
				2 => "-rc vbr -cq 21 -b:v 6500k -maxrate 8500k -bufsize 16000k",
				_ => "-rc vbr -cq 26 -b:v 3000k -maxrate 4200k -bufsize 8000k"
			};
			string cpuRate = settings.ExportBitratePreset switch
			{
				1 => "-crf 23 -maxrate 6200k -bufsize 12000k",
				2 => "-crf 20 -maxrate 8500k -bufsize 16000k",
				_ => "-crf 25 -maxrate 4200k -bufsize 8000k"
			};

			string nvencCmd = $"-hide_banner -y {inputs} {filterPart}{mapPart}-c:v h264_nvenc -preset p4 {nvencRate} -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart {ScriptBatchComposer.QuoteArg(outputPath)}";
			bool rendered = await RunFfmpegAsync(ffmpegPath, nvencCmd, ct);
			if (!rendered)
			{
				string cpuCmd = $"-hide_banner -y {inputs} {filterPart}{mapPart}-c:v libx264 -preset veryfast {cpuRate} -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart {ScriptBatchComposer.QuoteArg(outputPath)}";
				rendered = await RunFfmpegAsync(ffmpegPath, cpuCmd, ct);
			}

			if (!rendered || !File.Exists(outputPath) || new FileInfo(outputPath).Length < 2048)
			{
				throw new InvalidOperationException("最终烧录字幕或导出成品 MP4 失败，请检查字幕字体与磁盘空间。");
			}
			progressCallback?.Invoke(100);
			return (mergedWords, assPath, rawFinalRecognized);
		}

		private static async Task<(double Duration, List<(double Start, double End)> Silences)> ProbeDurationAndSilencesAsync(
			string videoPath,
			string ffmpegPath,
			int thresholdDb,
			double minDurSec,
			CancellationToken ct)
		{
			var silences = new List<(double Start, double End)>();
			double duration = 0;

			string args = $"-hide_banner -i {ScriptBatchComposer.QuoteArg(videoPath)} -af \"silencedetect=noise={thresholdDb}dB:d={minDurSec.ToString("0.00", CultureInfo.InvariantCulture)}\" -f null -";
			var psi = new ProcessStartInfo
			{
				FileName = ffmpegPath,
				Arguments = args,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
				StandardErrorEncoding = Encoding.UTF8
			};

			using var proc = new Process { StartInfo = psi };
			proc.Start();
			string stderr = await proc.StandardError.ReadToEndAsync();
			using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
			{
				await Task.Run(() => proc.WaitForExit(30000), ct);
			}

			var dm = DurationRegex.Match(stderr ?? string.Empty);
			if (dm.Success)
			{
				int hh = int.Parse(dm.Groups[1].Value);
				int mm = int.Parse(dm.Groups[2].Value);
				double ss = double.Parse(dm.Groups[3].Value, CultureInfo.InvariantCulture);
				duration = hh * 3600 + mm * 60 + ss;
			}

			double? openStart = null;
			foreach (string line in (stderr ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
			{
				var sm = SilenceStartRegex.Match(line);
				if (sm.Success && double.TryParse(sm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double sv))
				{
					openStart = sv;
					continue;
				}
				var em = SilenceEndRegex.Match(line);
				if (em.Success && double.TryParse(em.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ev))
				{
					if (openStart.HasValue)
					{
						silences.Add((openStart.Value, ev));
						openStart = null;
					}
				}
			}
			if (openStart.HasValue && duration > openStart.Value)
			{
				silences.Add((openStart.Value, duration));
			}

			return (duration, silences);
		}

		private static async Task<bool> RunFfmpegAsync(string ffmpegPath, string args, CancellationToken ct)
		{
			var (ok, _) = await RunFfmpegCaptureAsync(ffmpegPath, args, ct);
			return ok;
		}

		private static async Task<(bool Ok, string Stderr)> RunFfmpegCaptureAsync(string ffmpegPath, string args, CancellationToken ct)
		{
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = ffmpegPath,
					Arguments = args,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true,
					StandardErrorEncoding = Encoding.UTF8
				};
				using var proc = new Process { StartInfo = psi };
				proc.Start();
				var errTask = proc.StandardError.ReadToEndAsync();
				using (ct.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
				{
					await Task.Run(() => proc.WaitForExit(), ct);
				}
				string stderr = await errTask;
				return (proc.ExitCode == 0, stderr ?? string.Empty);
			}
			catch
			{
				return (false, string.Empty);
			}
		}

		private static double ParseLastTimeFromStderr(string stderr)
		{
			if (string.IsNullOrEmpty(stderr)) return 0;
			var matches = TimeProgressRegex.Matches(stderr);
			if (matches.Count == 0) return 0;
			var last = matches[matches.Count - 1];
			return int.Parse(last.Groups[1].Value) * 3600
				+ int.Parse(last.Groups[2].Value) * 60
				+ double.Parse(last.Groups[3].Value, CultureInfo.InvariantCulture);
		}

		private static ScriptBatchSettings CloneSubtitleSettings(ScriptBatchSettings src, int w, int h)
		{
			var json = new JavaScriptSerializer().Serialize(src ?? ScriptBatchSettings.Load());
			var copy = new JavaScriptSerializer().Deserialize<ScriptBatchSettings>(json) ?? new ScriptBatchSettings();
			copy.CanvasWidth = w;
			copy.CanvasHeight = h;
			return copy;
		}

		private static string ResolveWhisperModelPath(string modelKey)
		{
			string key = string.IsNullOrWhiteSpace(modelKey) ? "base" : modelKey.Trim().ToLowerInvariant();
			string p = TranscribeEngine.FindModelPath(key);
			if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
			foreach (string fallback in new[] { "base", "small", "turbo", "large" })
			{
				string fp = TranscribeEngine.FindModelPath(fallback);
				if (!string.IsNullOrEmpty(fp) && File.Exists(fp)) return fp;
			}
			return string.Empty;
		}

		private static string EscapeFfmpegFilterPath(string path)
		{
			return (path ?? string.Empty).Replace("\\", "/").Replace(":", "\\:").Replace("'", "\\'");
		}

		private static string FormatSrtTs(double sec)
		{
			var ts = TimeSpan.FromSeconds(Math.Max(0, sec));
			return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
		}

		private static string NormalizeWord(string s)
		{
			return Regex.Replace((s ?? string.Empty).Trim().ToLowerInvariant(), @"[^\p{L}\p{N}]", string.Empty);
		}

		private static List<string> TokenizeNormalized(string text)
		{
			return Regex.Split(text ?? string.Empty, @"\s+")
				.Select(NormalizeWord)
				.Where(w => w.Length >= 2)
				.ToList();
		}

		private static string BuildOutputFileName(SmartComposeTaskItem task, SmartComposeSettings settings)
		{
			string raw = !string.IsNullOrWhiteSpace(task?.TaskName)
				? task.TaskName
				: (!string.IsNullOrWhiteSpace(task?.TaskSeq) ? task.TaskSeq : "视频成片");
			string baseName = SanitizeName(raw);
			if (baseName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
				return baseName;
			return baseName + ".mp4";
		}

		private static string EnsureUniqueOutputPath(string path)
		{
			if (!File.Exists(path)) return path;
			string dir = Path.GetDirectoryName(path) ?? string.Empty;
			string stem = Path.GetFileNameWithoutExtension(path);
			string ext = Path.GetExtension(path);
			for (int i = 2; i < 1000; i++)
			{
				string candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
				if (!File.Exists(candidate)) return candidate;
			}
			return Path.Combine(dir, $"{stem}_{Guid.NewGuid().ToString("N").Substring(0, 6)}{ext}");
		}

		private static string SanitizeName(string name)
		{
			string s = Regex.Replace(name ?? "video", @"[\\/:*?""<>|\r\n]+", "_").Trim();
			return string.IsNullOrWhiteSpace(s) ? "video" : (s.Length > 60 ? s.Substring(0, 60) : s);
		}

		/// <summary>
		/// 将技术异常自动翻译为三段式大白话排障指南，方便运营/剪辑人员自行处理
		/// </summary>
		public static PlainLogEntry TranslateErrorToPlainLog(string stage, string taskName, Exception ex)
		{
			string raw = ex?.Message ?? "未知错误";
			string lower = raw.ToLowerInvariant();

			if (lower.Contains("api has not been used") || lower.Contains("service_disabled") || lower.Contains("not enabled"))
			{
				return new PlainLogEntry
				{
					Level = PlainLogLevel.Error,
					Stage = stage,
					Title = $"《{taskName}》Google Cloud 项目尚未启用 Sheets 或 Drive API",
					PlainCause = "您导入的凭据所属 Google Cloud 项目还没有开启 Google Sheets API 或 Google Drive API。",
					SelfFixTip = "请打开 Google Cloud Console -> API 和服务 -> 库，搜索并启用「Google Sheets API」与「Google Drive API」，等待 1 分钟后重试。",
					RawDetail = raw
				};
			}
			if (lower.Contains("401") || lower.Contains("invalid_grant") || lower.Contains("token") || lower.Contains("授权"))
			{
				return new PlainLogEntry
				{
					Level = PlainLogLevel.Error,
					Stage = stage,
					Title = $"《{taskName}》Google 账号授权已失效或未绑定",
					PlainCause = "当前绑定的 Gmail 登录凭证已过期或尚未授权。",
					SelfFixTip = "点击左侧【绑定 Google 账号】按钮重新在浏览器点一次允许即可恢复。",
					RawDetail = raw
				};
			}
			if (lower.Contains("403") || lower.Contains("permission") || lower.Contains("forbidden") || lower.Contains("not found") || lower.Contains("404"))
			{
				return new PlainLogEntry
				{
					Level = PlainLogLevel.Error,
					Stage = stage,
					Title = $"《{taskName}》无法访问谷歌云盘或谷歌表格（无权限/链接失效）",
					PlainCause = "目标素材文件夹、成品文件夹或任务表格没有对当前绑定的 Gmail 账号开放权限，或链接已失效。",
					SelfFixTip = "请在 Google 云端硬盘/表格中把对应文件夹与表格共享（编辑权限）给当前绑定的 Gmail 账号，再重试。",
					RawDetail = raw
				};
			}
			if (lower.Contains("quota") || lower.Contains("storage"))
			{
				return new PlainLogEntry
				{
					Level = PlainLogLevel.Error,
					Stage = stage,
					Title = $"《{taskName}》谷歌云盘空间已满，无法上传成品",
					PlainCause = "当前 Gmail 云盘剩余存储空间不足。",
					SelfFixTip = "请清理谷歌云盘回收站/旧视频，或更换有空间的 Gmail 账号重新绑定。",
					RawDetail = raw
				};
			}
			if (lower.Contains("timeout") || lower.Contains("unable to connect") || lower.Contains("network") || lower.Contains("host") || lower.Contains("ssl"))
			{
				return new PlainLogEntry
				{
					Level = PlainLogLevel.Error,
					Stage = stage,
					Title = $"《{taskName}》网络连接超时，无法连接 Google 服务器",
					PlainCause = "电脑代理/梯子未开启、节点不稳定或网络中断。",
					SelfFixTip = "请检查代理软件是否正常运行（必要时在左侧填写本地代理地址如 http://127.0.0.1:7890），确认浏览器能打开 Google 后点启动。",
					RawDetail = raw
				};
			}
			if (lower.Contains("disk") || lower.Contains("space") || lower.Contains("磁盘"))
			{
				return new PlainLogEntry
				{
					Level = PlainLogLevel.Error,
					Stage = stage,
					Title = $"《{taskName}》本地硬盘空间不足",
					PlainCause = "成品输出目录或系统盘剩余空间不够存放临时视频与成品。",
					SelfFixTip = "请清理电脑磁盘空间，或在左侧把成品输出目录改到空间充足的硬盘分区。",
					RawDetail = raw
				};
			}

			return new PlainLogEntry
			{
				Level = PlainLogLevel.Error,
				Stage = stage,
				Title = $"《{taskName}》在「{stage}」阶段执行失败",
				PlainCause = raw,
				SelfFixTip = "请先核对素材链接是否有效、网络是否畅通；若仍无法解决，点右上角【一键复制排障报告】发给技术支持。",
				RawDetail = raw
			};
		}
	}
}
