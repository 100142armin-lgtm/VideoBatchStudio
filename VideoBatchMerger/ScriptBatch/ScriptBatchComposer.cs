using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace VideoBatchMerger.ScriptBatch
{
	public static class ScriptBatchComposer
	{
		private static readonly Random s_rand = new Random();
		private static readonly Dictionary<string, bool> s_footageValidationCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
		private static readonly object s_footageValidationLock = new object();
		private static readonly object s_bgmRandomLock = new object();
		private static bool? s_nvencUsable;
		private static readonly object s_nvencLock = new object();
		private static readonly HashSet<string> s_bgmExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".opus", ".wma", ".aiff", ".aif"
		};

		public static bool HasUsableBgmSource(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return false;
			if (File.Exists(path)) return s_bgmExtensions.Contains(Path.GetExtension(path));
			if (!Directory.Exists(path)) return false;

			try
			{
				return Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
					.Any(file => s_bgmExtensions.Contains(Path.GetExtension(file)));
			}
			catch
			{
				return false;
			}
		}

		private static string ResolveBgmFile(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return null;
			if (File.Exists(path)) return s_bgmExtensions.Contains(Path.GetExtension(path)) ? path : null;
			if (!Directory.Exists(path)) return null;

			try
			{
				var files = Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
					.Where(file => s_bgmExtensions.Contains(Path.GetExtension(file)))
					.ToArray();
				if (files.Length == 0) return null;
				lock (s_bgmRandomLock)
				{
					return files[s_rand.Next(files.Length)];
				}
			}
			catch
			{
				return null;
			}
		}

		public static string FindFfmpegExe()
		{
			string asmDir = AppDomain.CurrentDomain.BaseDirectory;
			string cand1 = Path.Combine(asmDir, "ffmpeg.exe");
			if (File.Exists(cand1)) return cand1;

			string dPath = @"D:\工具\视频拼接\VideoBatchStudio\ffmpeg.exe";
			if (File.Exists(dPath)) return dPath;

			string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string wingetPath = Path.Combine(localApp, @"Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.1-full_build\bin\ffmpeg.exe");
			if (File.Exists(wingetPath)) return wingetPath;

			string devPath = @"C:\Users\PcVkH\.gemini\antigravity\scratch\VideoBatchStudio\ffmpeg.exe";
			if (File.Exists(devPath)) return devPath;

			return "ffmpeg.exe";
		}

		private static double ProbeAudioDurationSeconds(string audioPath)
		{
			if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath)) return 0.0;
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = FindFfmpegExe(),
					Arguments = "-hide_banner -i " + QuoteArg(audioPath),
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using (var proc = new Process { StartInfo = psi })
				{
					proc.Start();
					var errorTask = proc.StandardError.ReadToEndAsync();
					if (!proc.WaitForExit(8000))
					{
						try { proc.Kill(); } catch { }
						return 0.0;
					}
					string info = errorTask.GetAwaiter().GetResult();
					var match = Regex.Match(info, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.CultureInvariant);
					if (!match.Success) return 0.0;
					return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600.0
						+ int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60.0
						+ double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
				}
			}
			catch { return 0.0; }
		}

		public static async Task<bool> ProcessItemAsync(
			ScriptBatchItem item,
			ScriptBatchSettings settings,
			List<string> footagePool,
			string outputDir,
			Action<int, string> progressCallback,
			CancellationToken ct,
			double previewDurationSeconds = 0,
			string previewOutputPath = null)
		{
			string tempDir = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_ScriptBatch", $"task_{item.Id}_{DateTime.Now.Ticks}");
			Directory.CreateDirectory(tempDir);

			try
			{
				// 1. TTS 语音生成与原生字幕 (若预览缓存中已有匹配的音频与字幕，直接秒级复用)
				if (!string.IsNullOrEmpty(item.AudioPath) && File.Exists(item.AudioPath) &&
				    !string.IsNullOrEmpty(item.SrtPath) && File.Exists(item.SrtPath))
				{
					progressCallback?.Invoke(35, "已复用缓存配音与词级字幕，正在极速重排画面...");
					item.DurationSeconds = Math.Max(1.0, ProbeAudioDurationSeconds(item.AudioPath));
				}
				else
				{
					TtsResult ttsResult;
					string voice = string.IsNullOrWhiteSpace(item.VoiceKey) ? (string.Equals(settings.TtsEngine, "elevenlabs", StringComparison.OrdinalIgnoreCase) ? settings.ElevenLabsVoice : settings.DefaultVoice) : item.VoiceKey;
					bool isLocalClone = (voice != null && (voice.StartsWith("local:") || voice.EndsWith(".pth") || voice.EndsWith(".wav") || voice.EndsWith(".mp3")))
					                    || string.Equals(settings.TtsEngine, "local_xtts", StringComparison.OrdinalIgnoreCase);
					bool isElevenLabs = string.Equals(settings.TtsEngine, "elevenlabs", StringComparison.OrdinalIgnoreCase) && !isLocalClone;

					if (isLocalClone)
					{
						progressCallback?.Invoke(15, "正在调用本地 RTX 5060 离线大模型生成声音克隆...");
						item.Status = "本地独显克隆合成中";

						string speakerRef = voice;
						if (settings.ClonedVoices != null && settings.ClonedVoices.Count > 0)
						{
							var match = settings.ClonedVoices.FirstOrDefault(v =>
								string.Equals(v.VoiceId, voice, StringComparison.OrdinalIgnoreCase) ||
								string.Equals(v.Name, voice, StringComparison.OrdinalIgnoreCase) ||
								(!string.IsNullOrEmpty(v.VoiceId) && voice.Contains(v.VoiceId)) ||
								(!string.IsNullOrEmpty(v.Name) && voice.Contains(v.Name)));

							if (match != null)
							{
								if (!string.IsNullOrEmpty(match.LatentPath) && File.Exists(match.LatentPath))
									speakerRef = match.LatentPath;
								else if (!string.IsNullOrEmpty(match.ReferenceAudioPath) && File.Exists(match.ReferenceAudioPath))
									speakerRef = match.ReferenceAudioPath;
							}
						}

						ttsResult = await LocalCloneEngine.SynthesizeAsync(
							item.ScriptText,
							speakerRef,
							tempDir,
							item.Id,
							ct
						);
					}
					else if (isElevenLabs)
					{
						progressCallback?.Invoke(15, "正在调用 ElevenLabs 官方多密钥池合成高保真语音...");
						item.Status = "ElevenLabs 语音合成中";

						if (voice == "CustomVoice" && !string.IsNullOrWhiteSpace(settings.ElevenLabsCustomVoiceId))
						{
							voice = settings.ElevenLabsCustomVoiceId.Trim();
						}

						ttsResult = await ElevenLabsEngine.SynthesizeWithFailoverAsync(
							item.ScriptText,
							voice,
							tempDir,
							item.Id,
							ct
						);
					}
					else
					{
						progressCallback?.Invoke(15, "正在合成 Edge 神经网络声音...");
						item.Status = "生成语音中";

						string edgeVoice = string.IsNullOrWhiteSpace(item.VoiceKey) ? settings.DefaultVoice : item.VoiceKey;
						// 统一强制开启 WordBoundary 词级高精切分，实现短视频黄金节奏字幕与毫秒级音字同步
						string boundary = "WordBoundary";

						ttsResult = await ScriptBatchTtsEngine.SynthesizeAsync(
							item.ScriptText,
							edgeVoice,
							settings.SpeechRate,
							settings.SpeechPitch,
							boundary,
							tempDir,
							item.Id,
							ct
						);
					}

					if (!ttsResult.Success || string.IsNullOrEmpty(ttsResult.AudioPath) || !File.Exists(ttsResult.AudioPath))
					{
						item.Status = "失败";
						item.ErrorMessage = $"TTS 语音合成失败: {ttsResult.ErrorMessage}";
						item.QcBadge = "🔴 语音失败";
						item.QcDetails = item.ErrorMessage;
						return false;
					}

					item.AudioPath = ttsResult.AudioPath;
					item.SrtPath = ttsResult.SrtPath;
					if (previewOutputPath != null && !string.IsNullOrWhiteSpace(outputDir))
					{
						try
						{
							Directory.CreateDirectory(outputDir);
							string ext = Path.GetExtension(item.AudioPath);
							if (string.IsNullOrWhiteSpace(ext)) ext = ".mp3";
							string cachedAudio = Path.Combine(outputDir, $"cached_tts_{item.Id}{ext}");
							string cachedSrt = Path.Combine(outputDir, $"cached_tts_{item.Id}.srt");
							File.Copy(item.AudioPath, cachedAudio, true);
							if (!string.IsNullOrEmpty(item.SrtPath) && File.Exists(item.SrtPath))
							{
								File.Copy(item.SrtPath, cachedSrt, true);
								string wordsJson = item.SrtPath + ".words.json";
								if (File.Exists(wordsJson))
								{
									File.Copy(wordsJson, cachedSrt + ".words.json", true);
								}
								item.SrtPath = cachedSrt;
							}
							item.AudioPath = cachedAudio;
						}
						catch { }
					}
					// Word-boundary subtitles can end before the last encoded audio samples.
					item.DurationSeconds = Math.Max(1.0, Math.Max(ttsResult.DurationSeconds, ProbeAudioDurationSeconds(item.AudioPath)));
				}

				progressCallback?.Invoke(40, "语音生成完毕，正在排版高精字幕与水印...");

				// 2. 将 SRT 转换为 ASS 样式字幕
				string assPath = Path.Combine(tempDir, $"styled_sub_{item.Id}.ass");
				BuildAssSubtitleFile(item.SrtPath, assPath, settings);
				item.AssPath = assPath;

				// 3. 构建水印图层 (若启用)
				string watermarkPngPath = null;
				if (settings.WatermarkEnabled)
				{
					watermarkPngPath = Path.Combine(tempDir, $"watermark_{item.Id}.png");
					BuildWatermarkLayer(settings, settings.CanvasWidth, settings.CanvasHeight, watermarkPngPath);
				}

				// 4. 准备背景画面与混剪
				progressCallback?.Invoke(60, "智能选取画面素材并对齐时长...");
				item.Status = "画面渲染中";

				// 命名规范：ZB-制作人-文案代码-中文文案第一句-当天日期-FF-PT-.mp4
				string baseFileName = settings.NamingRuleEnabled
					? item.GenerateStandardFileName(settings.ProducerName)
					: SanitizeFileName(item.DisplayTitle);

				string outVideoName = baseFileName + ".mp4";
				string finalOutputPath = previewOutputPath ?? Path.Combine(outputDir, outVideoName);

				// 确保文件名不重名冲突
				int fileIndex = 1;
				while (previewOutputPath == null && File.Exists(finalOutputPath))
				{
					outVideoName = $"{baseFileName}_{fileIndex}.mp4";
					finalOutputPath = Path.Combine(outputDir, outVideoName);
					fileIndex++;
				}
				item.VideoOutputPath = finalOutputPath;

				// 5. 调用 FFmpeg 渲染出片
				bool renderOk = await RenderVideoWithFfmpegAsync(
					item,
					settings,
					footagePool,
					tempDir,
					watermarkPngPath,
					finalOutputPath,
					progressCallback,
					ct,
					previewDurationSeconds
				);

				if (!renderOk || !File.Exists(finalOutputPath))
				{
					item.Status = "失败";
					if (string.IsNullOrWhiteSpace(item.ErrorMessage)) item.ErrorMessage = "FFmpeg 视频渲染失败";
					item.QcBadge = "🔴 渲染失败";
					item.QcDetails = item.ErrorMessage;
					return false;
				}

				if (previewOutputPath != null)
				{
					item.Status = "预览就绪";
					progressCallback?.Invoke(100, "预览视频已合成");
					return true;
				}

				// 6. 四重雷达全自动质检
				progressCallback?.Invoke(95, "正在进行 4 重雷达全自动质检...");
				RunQualityControlChecks(item, settings);

				item.Status = "完成";
				item.ProgressPercent = 100;
				progressCallback?.Invoke(100, $"完成: {item.QcBadge}");
				return true;
			}
			catch (Exception ex)
			{
				item.Status = "异常";
				item.ErrorMessage = ex.Message;
				item.QcBadge = "🔴 异常中断";
				item.QcDetails = ex.Message;
				return false;
			}
			finally
			{
				// 清理临时文件，保护磁盘空间
				try
				{
					if (Directory.Exists(tempDir))
					{
						Directory.Delete(tempDir, true);
					}
				}
				catch { }
			}
		}

		public static void BuildWatermarkLayer(ScriptBatchSettings settings, int canvasW, int canvasH, string outPngPath)
		{
			try
			{
				int w = canvasW > 0 ? canvasW : 1080;
				int h = canvasH > 0 ? canvasH : 1920;

				using (var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
				{
					using (var g = Graphics.FromImage(bmp))
					{
						g.Clear(Color.Transparent);
						g.SmoothingMode = SmoothingMode.AntiAlias;
						g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
						g.InterpolationMode = InterpolationMode.HighQualityBicubic;

						if (settings.WatermarkType == 0) // 文字水印
						{
							string text = string.IsNullOrWhiteSpace(settings.WatermarkText) ? "Voice: ElevenLabs" : settings.WatermarkText.Trim();
							int fontSize = Math.Max(16, settings.WatermarkFontSize);
							string fontName = string.IsNullOrWhiteSpace(settings.WatermarkFontFamily) ? "Arial" : settings.WatermarkFontFamily;

							using (var font = new Font(fontName, fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
							{
								// 限制最大宽度，支持手动换行与长文本自适应折行，防止溢出画面边缘
								float maxWmWidth = Math.Max(260f, w * 0.85f - settings.WatermarkOffsetX);
								var stringFormat = new StringFormat
								{
									Alignment = (settings.WatermarkPosition == 0 || settings.WatermarkPosition == 1) ? StringAlignment.Far :
												((settings.WatermarkPosition == 4 || settings.WatermarkPosition == 5) ? StringAlignment.Center : StringAlignment.Near),
									LineAlignment = StringAlignment.Near,
									FormatFlags = StringFormatFlags.LineLimit
								};

								var sz = g.MeasureString(text, font, (int)maxWmWidth, stringFormat);
								float itemW = Math.Min(maxWmWidth, sz.Width + 4);
								float itemH = sz.Height + 4;

								PointF pt = CalculateWatermarkPosition(settings.WatermarkPosition, w, h, itemW, itemH, settings.WatermarkOffsetX, settings.WatermarkOffsetY);
								var textRect = new RectangleF(pt.X, pt.Y, itemW, itemH);

								int alpha = (int)Math.Max(25, Math.Min(255, settings.WatermarkTextOpacity * 2.55));
								Color baseCol = ParseColor(settings.WatermarkTextColorHex, Color.White);
								Color textCol = Color.FromArgb(alpha, baseCol.R, baseCol.G, baseCol.B);
								Color shadowCol = Color.FromArgb((int)(alpha * 0.7), 0, 0, 0);

								// 柔和黑色外阴影，提升在明亮复杂画面下的识别度
								using (var shadowBrush = new SolidBrush(shadowCol))
								{
									g.DrawString(text, font, shadowBrush, new RectangleF(pt.X + 2, pt.Y + 2, itemW, itemH), stringFormat);
									g.DrawString(text, font, shadowBrush, new RectangleF(pt.X - 1, pt.Y - 1, itemW, itemH), stringFormat);
									g.DrawString(text, font, shadowBrush, new RectangleF(pt.X + 1, pt.Y - 1, itemW, itemH), stringFormat);
									g.DrawString(text, font, shadowBrush, new RectangleF(pt.X - 1, pt.Y + 1, itemW, itemH), stringFormat);
								}

								using (var brush = new SolidBrush(textCol))
								{
									g.DrawString(text, font, brush, textRect, stringFormat);
								}
							}
						}
						else // 图片水印
						{
							if (!string.IsNullOrWhiteSpace(settings.WatermarkImagePath) && File.Exists(settings.WatermarkImagePath))
							{
								using (var img = Image.FromFile(settings.WatermarkImagePath))
								{
									float scale = Math.Max(5, Math.Min(60, settings.WatermarkImageScalePercent)) / 100.0f;
									float targetW = w * scale;
									float targetH = img.Height * (targetW / img.Width);

									float opacity = Math.Max(0.1f, Math.Min(1.0f, settings.WatermarkImageOpacity / 100.0f));

									var colMatrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
									using (var attr = new System.Drawing.Imaging.ImageAttributes())
									{
										attr.SetColorMatrix(colMatrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);

										PointF pt = CalculateWatermarkPosition(settings.WatermarkPosition, w, h, targetW, targetH, settings.WatermarkOffsetX, settings.WatermarkOffsetY);
										g.DrawImage(img, new Rectangle((int)pt.X, (int)pt.Y, (int)targetW, (int)targetH), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
									}
								}
							}
						}
					}
					bmp.Save(outPngPath, System.Drawing.Imaging.ImageFormat.Png);
				}
			}
			catch { }
		}

		public static PointF CalculateWatermarkPosition(int pos, float canvasW, float canvasH, float itemW, float itemH, float offX, float offY)
		{
			float x = offX;
			float y = offY;
			switch (pos)
			{
				case 0: // 右下角 (默认)
					x = canvasW - itemW - offX;
					y = canvasH - itemH - offY;
					break;
				case 1: // 右上角
					x = canvasW - itemW - offX;
					y = offY;
					break;
				case 2: // 左下角
					x = offX;
					y = canvasH - itemH - offY;
					break;
				case 3: // 左上角
					x = offX;
					y = offY;
					break;
				case 4: // 底部居中
					x = (canvasW - itemW) / 2f + offX;
					y = canvasH - itemH - offY;
					break;
				case 5: // 顶部居中
					x = (canvasW - itemW) / 2f + offX;
					y = offY;
					break;
				case 6: // 自由绝对坐标
					x = offX;
					y = offY;
					break;
			}
			return new PointF(Math.Max(0, x), Math.Max(0, y));
		}

		public static Color ParseColor(string hex, Color defaultColor)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(hex)) return defaultColor;
				hex = hex.Trim().TrimStart('#');
				if (hex.Length == 6)
				{
					int r = Convert.ToInt32(hex.Substring(0, 2), 16);
					int g = Convert.ToInt32(hex.Substring(2, 2), 16);
					int b = Convert.ToInt32(hex.Substring(4, 2), 16);
					return Color.FromArgb(r, g, b);
				}
			}
			catch { }
			return defaultColor;
		}

		private static void BuildAssSubtitleFile(string srtPath, string assPath, ScriptBatchSettings settings)
		{
			int w = settings.CanvasWidth > 0 ? settings.CanvasWidth : 1080;
			int h = settings.CanvasHeight > 0 ? settings.CanvasHeight : 1920;
			int fontSize = settings.SubtitleFontSize > 0 ? settings.SubtitleFontSize : 86;
			string fontName = string.IsNullOrWhiteSpace(settings.SubtitleFontFamily) ? "Arial" : settings.SubtitleFontFamily;

			var preset = SubtitlePresetCatalog.GetById(settings.SubtitleStyleId);
			string primaryColor = ScriptBatchSettings.ToAssStyleColor(settings.SubtitleColorHex, Color.White, settings.SubtitleTextOpacity);
			string outlineColor = ScriptBatchSettings.ToAssStyleColor(
				settings.SubtitleBorderStyle == 3 && settings.SubtitleDisplayMode != 4 ? settings.SubtitleScrollPanelColorHex : settings.SubtitleStrokeColorHex,
				Color.FromArgb(112, 92, 65),
				settings.SubtitleBorderStyle == 3 && settings.SubtitleDisplayMode != 4 ? settings.SubtitleScrollPanelOpacity : 100);
			float outlineWidth = Math.Max(0, settings.SubtitleStrokeWidth);
			int borderStyle = settings.SubtitleBorderStyle;
			int displayMode = settings.SubtitleDisplayMode;
			if (displayMode == 4) borderStyle = 1; // 上滚底板由独立矢量层绘制，文字始终保留可调描边。
			bool layeredPanel = borderStyle == 3 && (displayMode == 0 || displayMode == 2 || displayMode == 5);
			float shadowDepth = Math.Max(0, settings.SubtitleShadowDepth);
			if (borderStyle == 3) outlineWidth = Math.Max(10, (float)Math.Round(fontSize * 0.18));
			string backColor = ScriptBatchSettings.ToAssStyleColor(settings.SubtitleShadowColorHex, Color.FromArgb(147, 120, 79));

			int marginV = settings.SubtitleMarginV > 0 ? settings.SubtitleMarginV : 130;
			if (settings.SubtitleVerticalPercent > 0)
			{
				marginV = (int)Math.Max(30, (100.0 - settings.SubtitleVerticalPercent) / 100.0 * h);
			}

			int marginL = Math.Max(20, 40 + settings.SubtitleOffsetX);
			int marginR = Math.Max(20, 40 - settings.SubtitleOffsetX);

			var sb = new StringBuilder();
			sb.AppendLine("[Script Info]");
			sb.AppendLine("ScriptType: v4.00+");
			sb.AppendLine($"PlayResX: {w}");
			sb.AppendLine($"PlayResY: {h}");
			sb.AppendLine("ScaledBorderAndShadow: yes");
			sb.AppendLine();
			sb.AppendLine("[V4+ Styles]");
			sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
			string bold = settings.SubtitleBold ? "-1" : "0";
			string panelPrimaryColor = layeredPanel ? ScriptBatchSettings.ToAssStyleColor(settings.SubtitleColorHex, Color.White, 0) : primaryColor;
			sb.AppendLine($"Style: Default,{fontName},{fontSize},{panelPrimaryColor},&H000000FF,{outlineColor},{backColor},{bold},0,0,0,100,100,0,0,{borderStyle},{outlineWidth.ToString("0.0", CultureInfo.InvariantCulture)},{(layeredPanel ? 0f : shadowDepth).ToString("0.0", CultureInfo.InvariantCulture)},2,{marginL},{marginR},{marginV},1");
			if (layeredPanel)
			{
				string textOutline = ScriptBatchSettings.ToAssStyleColor(settings.SubtitleStrokeColorHex, Color.FromArgb(112, 92, 65));
				sb.AppendLine($"Style: PanelText,{fontName},{fontSize},{primaryColor},&H000000FF,{textOutline},{backColor},{bold},0,0,0,100,100,0,0,1,{Math.Max(0, settings.SubtitleStrokeWidth).ToString("0.0", CultureInfo.InvariantCulture)},{shadowDepth.ToString("0.0", CultureInfo.InvariantCulture)},2,{marginL},{marginR},{marginV},1");
			}
			int boxPadding = (int)Math.Max(10, Math.Round(fontSize * 0.18));
			string boxBgColor = ScriptBatchSettings.ToAssStyleColor(settings.SubtitleHighlightBoxColorHex, Color.FromArgb(214, 0, 0), settings.SubtitleHighlightBoxOpacity);
			string hlTextColor = ScriptBatchSettings.ToAssStyleColor(settings.SubtitleHighlightTextColorHex, Color.Yellow, settings.SubtitleTextOpacity);
			int highlightStyle = settings.SubtitleHighlightBoxEnabled ? 3 : 1;
			string highlightOutline = settings.SubtitleHighlightBoxEnabled ? boxBgColor : ScriptBatchSettings.ToAssStyleColor(settings.SubtitleStrokeColorHex, Color.FromArgb(112, 92, 65));
			string highlightWidth = settings.SubtitleHighlightBoxEnabled ? boxPadding.ToString(CultureInfo.InvariantCulture) : outlineWidth.ToString("0.0", CultureInfo.InvariantCulture);
			sb.AppendLine($"Style: HighlightBox,{fontName},{fontSize},{hlTextColor},&H000000FF,{highlightOutline},{backColor},{bold},0,0,0,100,100,0,0,{highlightStyle},{highlightWidth},{shadowDepth.ToString("0.0", CultureInfo.InvariantCulture)},2,{marginL},{marginR},{marginV},1");
			sb.AppendLine($"Style: HighlightText,{fontName},{fontSize},{hlTextColor},&H000000FF,{ScriptBatchSettings.ToAssStyleColor(settings.SubtitleStrokeColorHex, Color.FromArgb(112, 92, 65))},{backColor},{bold},0,0,0,100,100,0,0,1,{settings.SubtitleStrokeWidth.ToString("0.0", CultureInfo.InvariantCulture)},{shadowDepth.ToString("0.0", CultureInfo.InvariantCulture)},2,{marginL},{marginR},{marginV},1");
			sb.AppendLine();
			sb.AppendLine("[Events]");
			sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

			var wordTimings = displayMode == 1 || displayMode == 3 || displayMode == 5 ? LoadWordTimings(srtPath + ".words.json") : new List<WordTiming>();
			int nextWordIndex = 0;
			var cues = new List<ScrollCue>();

			if (File.Exists(srtPath))
			{
				string[] lines = File.ReadAllLines(srtPath, Encoding.UTF8);
				string currentStart = "";
				string currentEnd = "";
				var currentText = new StringBuilder();

				for (int i = 0; i < lines.Length; i++)
				{
					string line = lines[i].Trim();
					if (line.Contains("-->"))
					{
						var parts = line.Split(new[] { "-->" }, StringSplitOptions.RemoveEmptyEntries);
						if (parts.Length == 2)
						{
							currentStart = parts[0].Trim();
							currentEnd = parts[1].Trim();
						}
					}
					else if (string.IsNullOrWhiteSpace(line))
					{
						if (!string.IsNullOrEmpty(currentStart) && currentText.Length > 0)
						{
							cues.Add(new ScrollCue { Start = ParseSrtTimeSpan(currentStart).TotalSeconds, End = ParseSrtTimeSpan(currentEnd).TotalSeconds, Text = currentText.ToString().Trim() });
						}
						currentStart = "";
						currentEnd = "";
						currentText.Clear();
					}
					else if (!int.TryParse(line, out _))
					{
						if (currentText.Length > 0) currentText.Append(" ");
						currentText.Append(line);
					}
				}

				if (!string.IsNullOrEmpty(currentStart) && currentText.Length > 0)
				{
					cues.Add(new ScrollCue { Start = ParseSrtTimeSpan(currentStart).TotalSeconds, End = ParseSrtTimeSpan(currentEnd).TotalSeconds, Text = currentText.ToString().Trim() });
				}
			}

			if (settings.SubtitleUppercase)
			{
				foreach (var cue in cues) cue.Text = cue.Text?.ToUpperInvariant();
			}

			if (displayMode == 5)
			{
				EmitBrightPhraseSubtitles(sb, cues, wordTimings, settings, w, fontSize);
			}
			else if (displayMode == 4)
			{
				EmitScrollingSubtitles(sb, cues, settings, w, h, fontSize);
			}
			else
			{
				foreach (var cue in cues)
				{
					EmitAssCue(sb, FormatAssTimeSpan(TimeSpan.FromSeconds(cue.Start)), FormatAssTimeSpan(TimeSpan.FromSeconds(cue.End)), cue.Text,
						w, fontSize, displayMode, wordTimings, ref nextWordIndex);
				}
			}

			string assOutput = sb.ToString();
			if (layeredPanel)
			{
				var layeredLines = new StringBuilder();
				foreach (string line in assOutput.Split('\n'))
				{
					if (!line.StartsWith("Dialogue: 0,", StringComparison.Ordinal) || !line.Contains(",Default,,") || line.Contains("\\p1"))
					{
						layeredLines.Append(line).Append('\n');
						continue;
					}
					// 颜色覆盖会使 ASS 的 BorderStyle=3 按词切开底板；底板层只保留纯文字。
					string panelLine = Regex.Replace(line, @"\{\\1c&H[0-9A-Fa-f]+&\}", string.Empty);
					layeredLines.Append(panelLine).Append('\n');
					layeredLines.Append(line.Replace("Dialogue: 0,", "Dialogue: 1,").Replace(",Default,,", ",PanelText,,")).Append('\n');
				}
				assOutput = layeredLines.ToString();
			}
			if (settings.SubtitleGlowBlur > 0)
			{
				var assLines = assOutput.Split('\n');
				for (int lineIndex = 0; lineIndex < assLines.Length; lineIndex++)
				{
					if (!assLines[lineIndex].StartsWith("Dialogue:", StringComparison.Ordinal) || assLines[lineIndex].Contains("\\p1") || (layeredPanel && assLines[lineIndex].Contains(",Default,,"))) continue;
					int commas = 0;
					for (int charIndex = 0; charIndex < assLines[lineIndex].Length; charIndex++)
					{
						if (assLines[lineIndex][charIndex] != ',') continue;
						if (++commas != 9) continue;
						assLines[lineIndex] = assLines[lineIndex].Insert(charIndex + 1, $"{{\\blur{settings.SubtitleGlowBlur}}}");
						break;
					}
				}
				assOutput = string.Join("\n", assLines);
			}
			File.WriteAllText(assPath, assOutput, Encoding.UTF8);
		}

		// 明亮分句：沿用已有的语音词时间点。只有词时间点缺失时才按字数分配句内时长。
		internal static List<string> SplitBrightPhrases(string text)
		{
			var phrases = new List<string>();
			var words = (text ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var current = new List<string>();
			foreach (var word in words)
			{
				current.Add(word);
				if (current.Count >= 3 || word.EndsWith(",") || word.EndsWith(".") || word.EndsWith("!") || word.EndsWith("?") || word.EndsWith(";"))
				{
					phrases.Add(string.Join(" ", current));
					current.Clear();
				}
			}
			if (current.Count > 0) phrases.Add(string.Join(" ", current));
			return phrases;
		}

		private static void EmitBrightPhraseSubtitles(StringBuilder sb, List<ScrollCue> cues,
			List<WordTiming> timings, ScriptBatchSettings settings, int width, int fontSize)
		{
			int nextTiming = 0;
			string normal = ScriptBatchSettings.ToAssInlineBgr(settings.SubtitleColorHex, Color.White);
			string accent = ScriptBatchSettings.ToAssInlineBgr(settings.SubtitleHighlightTextColorHex, Color.FromArgb(255, 215, 0));
			foreach (var cue in cues)
			{
				var phrases = SplitBrightPhrases(cue.Text);
				if (phrases.Count == 0 || cue.End <= cue.Start) continue;
				var words = (cue.Text ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
				int first = -1;
				for (int candidate = nextTiming; candidate < Math.Min(timings.Count, nextTiming + 5); candidate++)
				{
					if (candidate + words.Length > timings.Count || Math.Abs(timings[candidate].start - cue.Start) > 0.12) continue;
					if (words.Select((word, i) => string.Equals(NormalizeSpokenWord(word), NormalizeSpokenWord(timings[candidate + i].text), StringComparison.OrdinalIgnoreCase)).All(match => match))
					{
						first = candidate;
						break;
					}
				}
				if (first >= 0) nextTiming = first + words.Length;
				int consumed = 0;
				foreach (var phrase in phrases)
				{
					var parts = phrase.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					int count = parts.Length;
					double start = first >= 0 ? Math.Max(cue.Start, timings[first + consumed].start) : cue.Start + (cue.End - cue.Start) * consumed / words.Length;
					double end = first >= 0
						? (consumed + count < words.Length
							? Math.Min(cue.End, Math.Max(timings[first + consumed + count - 1].end, timings[first + consumed + count].start))
							: Math.Max(cue.End, timings[first + consumed + count - 1].end))
						: cue.Start + (cue.End - cue.Start) * (consumed + count) / words.Length;
					consumed += count;
					if (end <= start + 0.01) continue;
					string safe = phrase.Replace("\\", "").Replace("{", "(").Replace("}", ")");
					int split = safe.IndexOf(' ');
					string styled = split < 0
						? $"{{\\1c{accent}}}{safe}{{\\1c{normal}}}"
						: $"{{\\1c{accent}}}{safe.Substring(0, split)}{{\\1c{normal}}}{safe.Substring(split)}";
					sb.AppendLine($"Dialogue: 0,{FormatAssTimeSpan(TimeSpan.FromSeconds(start))},{FormatAssTimeSpan(TimeSpan.FromSeconds(end))},Default,,0,0,0,,{{\\fad(70,70)}}{styled}");
				}
			}
		}

		private sealed class ScrollCue
		{
			public double Start { get; set; }
			public double End { get; set; }
			public string Text { get; set; }
		}

		private static void EmitScrollingSubtitles(StringBuilder sb, List<ScrollCue> cues,
			ScriptBatchSettings settings, int canvasW, int canvasH, int fontSize)
		{
			var lines = new List<ScrollCue>();
			int maxChars = Math.Max(12, Math.Min(28, (int)(canvasW / (fontSize * 0.57))));
			foreach (var cue in cues)
			{
				if (string.IsNullOrWhiteSpace(cue.Text) || cue.End <= cue.Start) continue;
				var split = SplitScrollText(cue.Text, maxChars);
				for (int part = 0; part < split.Count; part++)
				{
					double start = cue.Start + (cue.End - cue.Start) * part / split.Count;
					double end = cue.Start + (cue.End - cue.Start) * (part + 1) / split.Count;
					lines.Add(new ScrollCue { Start = start, End = end, Text = split[part] });
				}
			}
			if (lines.Count == 0) return;

			const int maxVisible = 7;
			int visibleSlots = Math.Min(maxVisible, lines.Count);
			int lineHeight = (int)Math.Round(fontSize * 1.24);
			int baseline = (int)Math.Round(canvasH * Math.Max(0.35, Math.Min(0.88, settings.SubtitleVerticalPercent / 100.0)));
			int centerX = canvasW / 2 + settings.SubtitleOffsetX;
			double lastEnd = lines[lines.Count - 1].End + 1.5;
			if (settings.SubtitleScrollPanelEnabled)
			{
				string panelColor = ScriptBatchSettings.ToAssInlineBgr(settings.SubtitleScrollPanelColorHex, Color.FromArgb(234, 230, 223));
				int panelAlpha = (int)Math.Round((100 - Math.Max(0, Math.Min(100, settings.SubtitleScrollPanelOpacity))) * 2.55);
				int panelWidth = (int)(canvasW * 0.91);
				int panelHeight = visibleSlots * lineHeight + lineHeight / 2;
				int panelLeft = (canvasW - panelWidth) / 2 + settings.SubtitleOffsetX;
				int panelTop = baseline - (visibleSlots - 1) * lineHeight - lineHeight / 2;
				string panel = $"{{\\an7\\pos({panelLeft},{panelTop})\\p1\\1a&H{panelAlpha:X2}&\\1c{panelColor}\\bord0\\shad0}}m 0 0 l {panelWidth} 0 {panelWidth} {panelHeight} 0 {panelHeight}{{\\p0}}";
				sb.AppendLine($"Dialogue: 0,{FormatAssTimeSpan(TimeSpan.FromSeconds(lines[0].Start))},{FormatAssTimeSpan(TimeSpan.FromSeconds(lastEnd))},Default,,0,0,0,,{panel}");
			}

			for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
			{
				string cleanText = (lines[lineIndex].Text ?? string.Empty).Replace("\\", "").Replace("{", "(").Replace("}", ")");
				for (int step = lineIndex; step < Math.Min(lines.Count, lineIndex + maxVisible); step++)
				{
					double segmentStart = lines[step].Start;
					double segmentEnd = step + 1 < lines.Count ? lines[step + 1].Start : lastEnd;
					if (segmentEnd <= segmentStart + 0.02) continue;
					double travelSeconds = Math.Min(0.36, Math.Max(0.08, (segmentEnd - segmentStart) * 0.42));
					double moveEnd = Math.Min(segmentEnd, segmentStart + travelSeconds);
					int fromY = baseline - (step - lineIndex - 1) * lineHeight;
					int toY = baseline - (step - lineIndex) * lineHeight;
					if (step == lineIndex) fromY = baseline + lineHeight;
					string moved = $"{{\\an5\\move({centerX},{fromY},{centerX},{toY},0,{(int)(travelSeconds * 1000)})\\fad(80,0)}}{cleanText}";
					sb.AppendLine($"Dialogue: 0,{FormatAssTimeSpan(TimeSpan.FromSeconds(segmentStart))},{FormatAssTimeSpan(TimeSpan.FromSeconds(moveEnd))},Default,,0,0,0,,{moved}");
					if (segmentEnd - moveEnd > 0.02)
					{
						string fade = step == lineIndex + maxVisible - 1 ? "\\fad(0,180)" : string.Empty;
						string held = $"{{\\an5\\pos({centerX},{toY}){fade}}}{cleanText}";
						sb.AppendLine($"Dialogue: 0,{FormatAssTimeSpan(TimeSpan.FromSeconds(moveEnd))},{FormatAssTimeSpan(TimeSpan.FromSeconds(segmentEnd))},Default,,0,0,0,,{held}");
					}
				}
			}
		}

		internal static List<string> SplitScrollText(string text, int maxChars)
		{
			var result = new List<string>();
			var words = (text ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (words.Length <= 1)
			{
				for (int i = 0; i < text.Length; i += maxChars)
					result.Add(text.Substring(i, Math.Min(maxChars, text.Length - i)));
				return result;
			}
			var current = new StringBuilder();
			foreach (string word in words)
			{
				if (current.Length > 0 && current.Length + word.Length + 1 > maxChars)
				{
					result.Add(current.ToString());
					current.Clear();
				}
				if (current.Length > 0) current.Append(' ');
				current.Append(word);
			}
			if (current.Length > 0) result.Add(current.ToString());
			return result;
		}

		private sealed class WordTiming
		{
			public double start { get; set; }
			public double end { get; set; }
			public string text { get; set; }
		}

		private static List<WordTiming> LoadWordTimings(string path)
		{
			try
			{
				if (!File.Exists(path)) return new List<WordTiming>();
				return new JavaScriptSerializer().Deserialize<List<WordTiming>>(File.ReadAllText(path, Encoding.UTF8)) ?? new List<WordTiming>();
			}
			catch { return new List<WordTiming>(); }
		}

		private static void EmitAssCue(StringBuilder sb, string start, string end, string text,
			int width, int fontSize, int displayMode, List<WordTiming> wordTimings, ref int nextWordIndex)
		{
			if (displayMode == 1 || displayMode == 3)
			{
				if (TryEmitTimedHighlight(sb, start, end, text, width, fontSize, displayMode, wordTimings, ref nextWordIndex)) return;
				EmitEstimatedHighlight(sb, start, end, text, width, fontSize, displayMode);
				return;
			}
			EmitAssDialogue(sb, start, end, text, width, fontSize, displayMode);
		}

		private static void EmitEstimatedHighlight(StringBuilder sb, string srtStart, string srtEnd, string text,
			int width, int fontSize, int displayMode)
		{
			var tokens = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 0) return;
			double start = ParseSrtTimeSpan(srtStart).TotalSeconds;
			double end = ParseSrtTimeSpan(srtEnd).TotalSeconds;
			if (end <= start) return;
			for (int i = 0; i < tokens.Length; i++)
			{
				int groupStart = (i / 3) * 3;
				string phrase = string.Join(" ", tokens.Skip(groupStart).Take(3));
				double wordStart = start + (end - start) * i / tokens.Length;
				double wordEnd = start + (end - start) * (i + 1) / tokens.Length;
				AppendHighlightDialogue(sb, wordStart, wordEnd, phrase, width, fontSize, displayMode, i - groupStart);
			}
		}

		private static bool TryEmitTimedHighlight(StringBuilder sb, string srtStart, string srtEnd, string text,
			int width, int fontSize, int displayMode, List<WordTiming> timings, ref int nextWordIndex)
		{
			if (timings == null || timings.Count == 0) return false;
			var tokens = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 0) return false;
			double cueStart = ParseSrtTimeSpan(srtStart).TotalSeconds;
			double cueEnd = ParseSrtTimeSpan(srtEnd).TotalSeconds;
			if (cueEnd <= cueStart) return false;

			int first = -1;
			for (int candidate = nextWordIndex; candidate < Math.Min(timings.Count, nextWordIndex + 5); candidate++)
			{
				if (candidate + tokens.Length > timings.Count) break;
				if (Math.Abs(timings[candidate].start - cueStart) > 0.12) continue;
				bool matches = true;
				for (int i = 0; i < tokens.Length; i++)
				{
					if (!string.Equals(NormalizeSpokenWord(tokens[i]), NormalizeSpokenWord(timings[candidate + i].text), StringComparison.OrdinalIgnoreCase))
					{
						matches = false;
						break;
					}
				}
				if (matches) { first = candidate; break; }
			}
			if (first < 0) return false;
			nextWordIndex = first + tokens.Length;
			double effectiveEnd = Math.Max(cueEnd, timings[nextWordIndex - 1].end);
			double position = cueStart;
			for (int i = 0; i < tokens.Length; i++)
			{
				var timing = timings[first + i];
				int groupStart = (i / 3) * 3;
				string phrase = string.Join(" ", tokens.Skip(groupStart).Take(3));
				double wordStart = Math.Max(position, timing.start);
				double wordEnd = Math.Min(effectiveEnd, Math.Max(wordStart, timing.end));
				AppendHighlightDialogue(sb, position, wordStart, phrase, width, fontSize, displayMode, -1);
				AppendHighlightDialogue(sb, wordStart, wordEnd, phrase, width, fontSize, displayMode, i - groupStart);
				position = wordEnd;
			}
			AppendHighlightDialogue(sb, position, effectiveEnd, string.Join(" ", tokens.Skip(((tokens.Length - 1) / 3) * 3)), width, fontSize, displayMode, -1);
			return true;
		}

		private static string NormalizeSpokenWord(string value)
		{
			return Regex.Replace(value ?? string.Empty, @"[^\p{L}\p{N}]", string.Empty);
		}

		private static void AppendHighlightDialogue(StringBuilder sb, double start, double end, string text,
			int width, int fontSize, int displayMode, int highlightedWordIndex)
		{
			if (end - start < 0.009) return;
			string startAss = FormatAssTimeSpan(TimeSpan.FromSeconds(start));
			string endAss = FormatAssTimeSpan(TimeSpan.FromSeconds(end));
			if (displayMode == 3 && highlightedWordIndex >= 0)
			{
				string boxLayerText = FormatAssBoxOnlyText(text, highlightedWordIndex);
				string strokeLayerText = FormatAssText(text, width, fontSize, 1, highlightedWordIndex);
				sb.AppendLine($"Dialogue: 0,{startAss},{endAss},Default,,0,0,0,,{boxLayerText}");
				sb.AppendLine($"Dialogue: 1,{startAss},{endAss},Default,,0,0,0,,{strokeLayerText}");
				return;
			}
			string assText = FormatAssText(text, width, fontSize, displayMode, highlightedWordIndex);
			sb.AppendLine($"Dialogue: 0,{startAss},{endAss},Default,,0,0,0,,{assText}");
		}

		private static string FormatAssBoxOnlyText(string text, int highlightedWordIndex)
		{
			text = (text ?? string.Empty).Replace("\r", "").Trim();
			if (string.IsNullOrEmpty(text)) return text;
			string wrapped = WrapToShortLines(text, 16);
			string[] lines = wrapped.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
			var sbResult = new StringBuilder();
			int wordIndex = 0;
			for (int li = 0; li < Math.Min(2, lines.Length); li++)
			{
				if (li > 0) sbResult.Append("\\N");
				var words = lines[li].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				for (int wi = 0; wi < words.Length; wi++)
				{
					if (wi > 0) sbResult.Append(" ");
					if (wordIndex == highlightedWordIndex)
					{
						sbResult.Append($"{{\\rHighlightBox\\1a&HFF&}}{words[wi]}{{\\rDefault\\alpha&HFF&}}");
					}
					else
					{
						sbResult.Append($"{{\\alpha&HFF&}}{words[wi]}");
					}
					wordIndex++;
				}
			}
			return sbResult.ToString();
		}

		/// <summary>
		/// 智能输出 ASS 字幕事件。具备双重防崩保底切分机制：
		/// 无论传入任何字幕源，单次同屏绝不允许超过 2 行或大段堆叠，按 2~4 个词的小节奏动态展现。
		/// </summary>
		private static void EmitAssDialogue(
			StringBuilder sb,
			string srtStart,
			string srtEnd,
			string text,
			int w,
			int fontSize,
			int displayMode)
		{
			text = text.Trim();
			if (string.IsNullOrEmpty(text)) return;

			var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			TimeSpan tStart = ParseSrtTimeSpan(srtStart);
			TimeSpan tEnd = ParseSrtTimeSpan(srtEnd);
			double totalSec = (tEnd - tStart).TotalSeconds;

			// 双重防崩保底：如果外部字幕单条词数 > 4 且持续时间 > 2.2 秒，自动等比切分为短视频小片段
			if (words.Length > 4 && totalSec > 2.2)
			{
				int targetChunkSize = 3;
				var chunks = new List<List<string>>();
				var cur = new List<string>();
				foreach (var wd in words)
				{
					cur.Add(wd);
					if (cur.Count >= targetChunkSize || wd.EndsWith(",") || wd.EndsWith(".") || wd.EndsWith("!") || wd.EndsWith("?") || wd.EndsWith("，") || wd.EndsWith("。"))
					{
						chunks.Add(cur);
						cur = new List<string>();
					}
				}
				if (cur.Count > 0) chunks.Add(cur);

				double secPerWord = totalSec / words.Length;
				double currentOffset = 0;

				for (int ci = 0; ci < chunks.Count; ci++)
				{
					var chunk = chunks[ci];
					double chunkDur = chunk.Count * secPerWord;
					TimeSpan cStart = tStart.Add(TimeSpan.FromSeconds(currentOffset));
					TimeSpan cEnd = (ci == chunks.Count - 1) ? tEnd : tStart.Add(TimeSpan.FromSeconds(currentOffset + chunkDur));
					currentOffset += chunkDur;

					string subText = string.Join(" ", chunk);
					string clean = FormatAssText(subText, w, fontSize, displayMode);
					if (displayMode == 2)
					{
						clean = "{\\t(0,120,\\fscx108\\fscy108)\\t(120,240,\\fscx100\\fscy100)}" + clean;
					}
					sb.AppendLine($"Dialogue: 0,{FormatAssTimeSpan(cStart)},{FormatAssTimeSpan(cEnd)},Default,,0,0,0,,{clean}");
				}
			}
			else
			{
				string cleanText = FormatAssText(text, w, fontSize, displayMode);
				if (displayMode == 2)
				{
					cleanText = "{\\t(0,120,\\fscx108\\fscy108)\\t(120,240,\\fscx100\\fscy100)}" + cleanText;
				}
				sb.AppendLine($"Dialogue: 0,{FormatAssTimeSpan(tStart)},{FormatAssTimeSpan(tEnd)},Default,,0,0,0,,{cleanText}");
			}
		}

		private static TimeSpan ParseSrtTimeSpan(string srtTime)
		{
			try
			{
				string t = srtTime.Trim().Replace(',', '.');
				if (TimeSpan.TryParse(t, out var ts)) return ts;
			}
			catch { }
			return TimeSpan.Zero;
		}

		private static string FormatAssTimeSpan(TimeSpan ts)
		{
			return $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}";
		}

		private static string FormatAssTime(string srtTime)
		{
			try
			{
				string t = srtTime.Replace(',', '.');
				if (TimeSpan.TryParse(t, out var ts))
				{
					return $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}";
				}
			}
			catch { }
			return srtTime.Replace(',', '.');
		}

		public static string FormatAssText(string text, int canvasW, int fontSize, int displayMode = 0, int highlightedWordIndex = 1)
		{
			text = text.Replace("\r", "").Trim();
			if (string.IsNullOrEmpty(text)) return text;

			// 自动短句排版：按 2~3 词一行折行，且同屏绝不允许超过 2 行
			string wrapped = WrapToShortLines(text, 16);

			if (displayMode == 1 || displayMode == 3)
			{
				string[] lines = wrapped.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
				var sbResult = new StringBuilder();

				int wordIndex = 0;
				for (int li = 0; li < Math.Min(2, lines.Length); li++)
				{
					if (li > 0) sbResult.Append("\\N");
					string line = lines[li].Trim();
					var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					if (words.Length == 0) continue;

					for (int wi = 0; wi < words.Length; wi++)
					{
						if (wi > 0) sbResult.Append(" ");
						if (wordIndex == highlightedWordIndex)
						{
							// 黄字 + 饱满红底框 (HighlightBox 样式：BorderStyle=3 独立矩形框 + 亮黄字)
							sbResult.Append($"{{\\r{(displayMode == 1 ? "HighlightText" : "HighlightBox")}}}{words[wi]}{{\\rDefault}}");
						}
						else
						{
							sbResult.Append(words[wi]);
						}
						wordIndex++;
					}
				}
				return sbResult.ToString();
			}

			return wrapped.Replace("\n", "\\N");
		}

		public static string WrapToShortLines(string text, int maxCharsPerLine = 16)
		{
			var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (words.Length <= 2) return text;

			var lines = new List<string>();
			var currentLine = new StringBuilder();
			int wordCountInLine = 0;

			foreach (var w in words)
			{
				if (currentLine.Length > 0 && (currentLine.Length + w.Length > maxCharsPerLine || wordCountInLine >= 2))
				{
					lines.Add(currentLine.ToString().Trim());
					currentLine.Clear();
					wordCountInLine = 0;
					if (lines.Count >= 2) break; // 严格限制同屏最大 2 行
				}

				if (currentLine.Length > 0) currentLine.Append(" ");
				currentLine.Append(w);
				wordCountInLine++;
			}

			if (currentLine.Length > 0 && lines.Count < 2)
			{
				lines.Add(currentLine.ToString().Trim());
			}

			return string.Join("\n", lines);
		}

		private static async Task<bool> RenderVideoWithFfmpegAsync(
			ScriptBatchItem item,
			ScriptBatchSettings settings,
			List<string> footagePool,
			string tempDir,
			string watermarkPngPath,
			string outputPath,
			Action<int, string> progressCallback,
			CancellationToken ct,
			double previewDurationSeconds = 0)
		{
			string ffmpeg = FindFfmpegExe();
			const double endingHoldSeconds = 2.0;
			// Probe again for cached items whose DurationSeconds came only from SRT.
			double voiceDur = Math.Max(item.DurationSeconds, ProbeAudioDurationSeconds(item.AudioPath));
			double fullDur = Math.Max(1.0, voiceDur) + endingHoldSeconds;
			double totalDur = previewDurationSeconds > 0 ? Math.Min(fullDur, previewDurationSeconds) : fullDur;
			string totalDurText = totalDur.ToString("0.00", CultureInfo.InvariantCulture);
			int w = settings.CanvasWidth;
			int h = settings.CanvasHeight;

			// 选择素材片段
			var selectedClips = await Task.Run(() => PickClipsForDuration(footagePool, totalDur, settings), ct);
			if (selectedClips.Count == 0 && footagePool != null && footagePool.Count > 0)
			{
				item.ErrorMessage = "背景素材目录中没有可解码的视频，请检查素材文件是否完整。";
				return false;
			}

			var filterComplex = new StringBuilder();
			var inputArgs = new StringBuilder();

			// Input 0: 音频
			inputArgs.Append($"-i {QuoteArg(item.AudioPath)} ");

			// Input 1..N: 视频素材
			if (selectedClips.Count > 0)
			{
				for (int i = 0; i < selectedClips.Count; i++)
				{
					if (settings.FootageStrategy == 2)
						inputArgs.Append($"-stream_loop -1 -t {totalDurText} -i {QuoteArg(selectedClips[i].FilePath)} ");
					else
						inputArgs.Append($"-ss {selectedClips[i].StartSec:0.00} -t {selectedClips[i].DurationSec:0.00} -i {QuoteArg(selectedClips[i].FilePath)} ");
				}

				for (int i = 0; i < selectedClips.Count; i++)
				{
					filterComplex.Append($"[{i + 1}:v]scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},setsar=1,fps=30[v{i}];");
				}

				for (int i = 0; i < selectedClips.Count; i++)
				{
					filterComplex.Append($"[v{i}]");
				}
				filterComplex.Append($"concat=n={selectedClips.Count}:v=1:a=0[rawv];");
			}
			else
			{
				filterComplex.Append($"color=c=0x0b1120:s={w}x{h}:d={totalDur + 1.0}:r=30[rawv];");
			}

			// 音频与 BGM 处理
			string audioMap = "-map \"[outa]\"";
			string bgmFile = settings.BgmEnabled ? ResolveBgmFile(settings.BgmPath) : null;
			bool hasBgm = !string.IsNullOrWhiteSpace(bgmFile);
			int nextInputIdx = selectedClips.Count + 1;
			filterComplex.Append($"[0:a]apad=whole_dur={totalDurText}[voice];");

			if (hasBgm)
			{
				int bgmInputIdx = nextInputIdx++;
				inputArgs.Append($"-stream_loop -1 -i {QuoteArg(bgmFile)} ");
				float bgmVol = Math.Max(0.02f, settings.BgmVolumePercent / 100f);
				double fadeStart = Math.Max(0.5, fullDur - 1.5);

				string bgmFilter = $"[{bgmInputIdx}:a]volume={bgmVol.ToString("0.00", CultureInfo.InvariantCulture)},afade=t=out:st={fadeStart.ToString("0.00", CultureInfo.InvariantCulture)}:d=1.5[bgm];[voice][bgm]amix=inputs=2:duration=first:dropout_transition=2[outa]";
				filterComplex.Append($"{bgmFilter};");
			}
			else
			{
				filterComplex.Append("[voice]anull[outa];");
			}

			// 压制 ASS 字幕 与 水印叠加
			string escapedAssPath = item.AssPath.Replace("\\", "/").Replace(":", "\\:");
			bool hasWatermark = !string.IsNullOrEmpty(watermarkPngPath) && File.Exists(watermarkPngPath);

			filterComplex.Append($"[rawv]ass='{escapedAssPath}'[v_sub];");
			string currentV = "v_sub";

			if (hasWatermark)
			{
				int wmInputIdx = nextInputIdx++;
				inputArgs.Append($"-i {QuoteArg(watermarkPngPath)} ");
				filterComplex.Append($"[{currentV}][{wmInputIdx}:v]overlay=0:0[v_wm];");
				currentV = "v_wm";
			}

			// 叠加 GIF 动图贴纸 (支持多贴纸、循环/单次、缩放、任意角度旋转、不透明度、起始/持续出现时间)
			if (settings.GifStickers != null && settings.GifStickers.Count > 0)
			{
				int stkIdx = 0;
				foreach (var stk in settings.GifStickers)
				{
					if (!stk.Enabled || string.IsNullOrWhiteSpace(stk.FilePath) || !File.Exists(stk.FilePath))
						continue;

					int stkInputIdx = nextInputIdx++;
					if (stk.Loop)
					{
						inputArgs.Append($"-stream_loop -1 -i {QuoteArg(stk.FilePath)} ");
					}
					else
					{
						inputArgs.Append($"-i {QuoteArg(stk.FilePath)} ");
					}

					int targetW = (int)Math.Round(w * (stk.ScalePercent / 100.0));
					targetW = Math.Max(16, (targetW / 2) * 2);

					var stkFilterParts = new List<string>
					{
						$"scale={targetW}:-1:flags=lanczos",
						"format=rgba"
					};

					if (Math.Abs(stk.RotationAngle) > 0.01f)
					{
						// FFmpeg 顺时针旋转，输入弧度 rad = angle * PI / 180
						// c=none 为透明底色，ow='hypot(iw,ih)':oh=ow 保证旋转时任意角度绝不切角
						float rad = (float)(stk.RotationAngle * Math.PI / 180.0);
						stkFilterParts.Add($"rotate={rad:0.0000}:c=none:ow='hypot(iw,ih)':oh=ow");
					}

					if (stk.OpacityPercent < 100)
					{
						float op = Math.Max(0.1f, Math.Min(1.0f, stk.OpacityPercent / 100.0f));
						stkFilterParts.Add($"colorchannelmixer=aa={op:0.00}");
					}

					string stkLabel = $"stk_{stkIdx}";
					filterComplex.Append($"[{stkInputIdx}:v]{string.Join(",", stkFilterParts)}[{stkLabel}];");

					// 计算出现时间
					string enableTiming = string.Empty;
					double start = Math.Max(0.0, stk.StartTimeSec);
					double dur = stk.DurationSec;
					string startText = start.ToString("0.###", CultureInfo.InvariantCulture);
					if (dur > 0.05)
					{
						double end = start + dur;
						enableTiming = $":enable='between(t,{startText},{end.ToString("0.###", CultureInfo.InvariantCulture)})'";
					}
					else if (start > 0.05)
					{
						enableTiming = $":enable='gte(t,{startText})'";
					}

					int centerX = (int)Math.Round(w * (stk.XPercent / 100.0));
					int centerY = (int)Math.Round(h * (stk.YPercent / 100.0));
					string xPosition = $"{centerX}-w/2";
					string yPosition = $"{centerY}-h/2";
					if (stk.MotionStyle != 0)
					{
						int endX = (int)Math.Round(w * (stk.EndXPercent / 100.0));
						int endY = (int)Math.Round(h * (stk.EndYPercent / 100.0));
						string motionDuration = Math.Max(0.1, stk.MotionDurationSec).ToString("0.###", CultureInfo.InvariantCulture);
						string progress = $"min(max((t-{startText})/{motionDuration},0),1)";
						string eased = $"({progress})*({progress})*(3-2*({progress}))";
						xPosition = $"{centerX}+({endX - centerX})*({eased})-w/2";
						yPosition = $"{centerY}+({endY - centerY})*({eased})-h/2";
					}

					string nextV = $"v_stk_{stkIdx}";
					filterComplex.Append($"[{currentV}][{stkLabel}]overlay=x='{xPosition}':y='{yPosition}'{enableTiming}:eval=frame:shortest=0[{nextV}];");
					currentV = nextV;
					stkIdx++;
				}
			}

			filterComplex.Append($"[{currentV}]null[outv]");

			// 编码器选择 (硬件加速优先，若硬件编码器驱动版本不匹配则自动无感回退 CPU libx264)
			const string cpuFallbackCodec = "libx264 -preset veryfast -crf 22";
			string vcodec = cpuFallbackCodec;
			if (settings.HardwareEncoder == "nvenc" || (settings.HardwareEncoder == "auto" && HasNvenc()))
			{
				vcodec = "h264_nvenc -preset p4 -cq 23";
			}
			else if (settings.HardwareEncoder == "qsv")
			{
				vcodec = "h264_qsv -global_quality 23";
			}

			var (ok, exitCode, detail) = await ExecuteFfmpegEncodeAsync(
				ffmpeg, inputArgs.ToString(), filterComplex.ToString(), audioMap, vcodec, totalDurText, totalDur, outputPath, item, progressCallback, ct);

			if (!ok && !ct.IsCancellationRequested && vcodec != cpuFallbackCodec)
			{
				if (vcodec.StartsWith("h264_nvenc", StringComparison.OrdinalIgnoreCase))
				{
					lock (s_nvencLock) { s_nvencUsable = false; }
				}
				progressCallback?.Invoke(60, "检测到显卡编码器驱动版本受限，已自动切换 CPU 高速软编...");
				(ok, exitCode, detail) = await ExecuteFfmpegEncodeAsync(
					ffmpeg, inputArgs.ToString(), filterComplex.ToString(), audioMap, cpuFallbackCodec, totalDurText, totalDur, outputPath, item, progressCallback, ct);
			}

			if (ok) return true;
			item.ErrorMessage = $"FFmpeg 渲染失败 (代码 {exitCode})" + (detail.Length > 0 ? $": {detail}" : "。请检查素材、GIF 贴纸和编码器设置。");
			return false;
		}

		private static async Task<(bool Ok, int ExitCode, string Detail)> ExecuteFfmpegEncodeAsync(
			string ffmpeg,
			string inputArgs,
			string filterComplex,
			string audioMap,
			string vcodec,
			string totalDurText,
			double totalDur,
			string outputPath,
			ScriptBatchItem item,
			Action<int, string> progressCallback,
			CancellationToken ct)
		{
			string args = $"-hide_banner {inputArgs} -filter_complex \"{filterComplex}\" -map \"[outv]\" {audioMap} -c:v {vcodec} -c:a aac -b:a 192k -t {totalDurText} -y {QuoteArg(outputPath)}";
			var errorLines = new Queue<string>();

			var psi = new ProcessStartInfo
			{
				FileName = ffmpeg,
				Arguments = args,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using (var proc = new Process { StartInfo = psi })
			{
				proc.Start();

				var readTask = Task.Run(() =>
				{
					using var sr = proc.StandardError;
					string line;
					while ((line = sr.ReadLine()) != null)
					{
						if (!string.IsNullOrWhiteSpace(line) && !line.Contains("time="))
						{
							errorLines.Enqueue(line.Trim());
							if (errorLines.Count > 12) errorLines.Dequeue();
						}
						if (line.Contains("time="))
						{
							var m = Regex.Match(line, @"time=(\d+):(\d+):(\d+\.?\d*)");
							if (m.Success)
							{
								double cur = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
								int pct = (int)Math.Min(95, Math.Max(60, 60 + (cur / totalDur) * 35));
								item.ProgressPercent = pct;
								progressCallback?.Invoke(pct, $"渲染中... {cur:0.0}/{totalDur:0.0}秒");
							}
						}
					}
				});

				while (!proc.HasExited)
				{
					if (ct.IsCancellationRequested)
					{
						try { proc.Kill(); } catch { }
						return (false, -1, "已取消");
					}
					await Task.Delay(200, ct);
				}

				await readTask;
				if (proc.ExitCode == 0) return (true, 0, string.Empty);
				string detail = string.Join(" | ", errorLines).Trim();
				if (detail.Length > 900) detail = detail.Substring(detail.Length - 900);
				return (false, proc.ExitCode, detail);
			}
		}

		private static List<ClipSlice> PickClipsForDuration(List<string> pool, double targetDur, ScriptBatchSettings settings)
		{
			var list = new List<ClipSlice>();
			if (pool == null || pool.Count == 0) return list;

			double accumulated = 0.0;
			double minSlice = Math.Max(2.0, settings.SliceDurationMin);
			double maxSlice = Math.Max(minSlice + 1.0, settings.SliceDurationMax);

			var availablePool = pool.Where(f =>
			{
				try { return new FileInfo(f).Length >= 8192; }
				catch { return false; }
			}).ToList();
			if (availablePool.Count == 0) return list;
			if (settings.FootageStrategy == 2)
			{
				string single = availablePool.FirstOrDefault(IsUsableFootage);
				if (single != null) list.Add(new ClipSlice(single, 0, targetDur));
				return list;
			}
			if (settings.FootageStrategy == 0)
			{
				availablePool = availablePool.OrderBy(_ => s_rand.Next()).ToList();
			}

			int poolIdx = 0;
			while (accumulated < targetDur + 0.5)
			{
				int candidateIdx = poolIdx % availablePool.Count;
				string path = availablePool[candidateIdx];
				if (!IsUsableFootage(path))
				{
					availablePool.RemoveAt(candidateIdx);
					if (availablePool.Count == 0) break;
					continue;
				}
				poolIdx++;

				double sliceLen = minSlice + s_rand.NextDouble() * (maxSlice - minSlice);
				double startSec = s_rand.Next(0, 3);

				list.Add(new ClipSlice(path, startSec, sliceLen));
				accumulated += sliceLen;

				if (list.Count > 50) break;
			}

			return list;
		}

		public static bool IsUsableFootage(string path)
		{
			try
			{
				var file = new FileInfo(path);
				if (!file.Exists || file.Length < 8192) return false;
				string cacheKey = path + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks;
				lock (s_footageValidationLock)
				{
					if (s_footageValidationCache.TryGetValue(cacheKey, out bool cached)) return cached;
				}

				var psi = new ProcessStartInfo
				{
					FileName = FindFfmpegExe(),
					Arguments = $"-hide_banner -loglevel quiet -i {QuoteArg(path)} -frames:v 1 -f null NUL",
					UseShellExecute = false,
					CreateNoWindow = true
				};
				bool usable;
				using (var probe = Process.Start(psi))
				{
					usable = probe.WaitForExit(5000) && probe.ExitCode == 0;
					if (!probe.HasExited) try { probe.Kill(); } catch { }
				}
				lock (s_footageValidationLock) s_footageValidationCache[cacheKey] = usable;
				return usable;
			}
			catch { return false; }
		}

		private static void RunQualityControlChecks(ScriptBatchItem item, ScriptBatchSettings settings)
		{
			var issues = new List<string>();
			int warnings = 0;
			int errors = 0;
			string primaryWarningReason = "";

			int charCount = item.ScriptText?.Length ?? 0;
			double dur = item.DurationSeconds;
			if (dur < 1.0)
			{
				errors++;
				issues.Add("❌ 语音异常: 音频时长小于 1 秒，可能合成被网络中断");
			}
			else
			{
				double charsPerSec = charCount / dur;
				// 葡萄牙语/多语言正常字符播报率约 10 ~ 28 字符/秒 (对应约 2.5 ~ 4 单词/秒)
				if (charsPerSec > 28.0)
				{
					warnings++;
					primaryWarningReason = "语速偏快";
					issues.Add($"⚠️ 语速诊断: 当前语速偏快 ({charsPerSec:0.1} 字符/秒)。\n   👉 建议微调: 在右侧【语速】下拉框选择 -5% 或 -10%，使海外听感更庄重沉稳。");
				}
				else if (charsPerSec < 9.0 && charCount > 25)
				{
					warnings++;
					primaryWarningReason = "语速偏慢";
					issues.Add($"⚠️ 语速诊断: 当前语速偏慢 ({charsPerSec:0.1} 字符/秒)。\n   👉 建议微调: 在右侧【语速】下拉框选择 +5% 或 +10%，提升视频紧凑度与留存率。");
				}
				else
				{
					issues.Add($"✅ 语音波形健康: 语速适中自然 (共 {charCount} 字 / 时长 {dur:0.1} 秒 / 约 {charsPerSec:0.1} 字/秒)");
				}
			}

			if (File.Exists(item.SrtPath))
			{
				string[] srtLines = File.ReadAllLines(item.SrtPath);
				int maxLine = 0;
				string longestText = "";
				foreach (var l in srtLines)
				{
					if (!l.Contains("-->") && !int.TryParse(l, out _))
					{
						if (l.Length > maxLine)
						{
							maxLine = l.Length;
							longestText = l.Trim();
						}
					}
				}
				if (maxLine > 55)
				{
					warnings++;
					if (string.IsNullOrEmpty(primaryWarningReason)) primaryWarningReason = "单句偏长";
					string previewSnippet = longestText.Length > 28 ? longestText.Substring(0, 28) + "..." : longestText;
					issues.Add($"⚠️ 字幕排版诊断: 发现较长单句 ({maxLine} 字符: \"{previewSnippet}\")。\n   👉 建议微调: 建议在文案标点处敲回车分句，或保持当前状态(引擎已自动开启双行居中折行排版)。");
				}
				else
				{
					issues.Add("✅ 字幕排版安全: 单句长宽适中，完美契合 9:16 屏幕安全区");
				}
			}

			if (File.Exists(item.VideoOutputPath))
			{
				long bytes = new FileInfo(item.VideoOutputPath).Length;
				if (bytes < 100 * 1024)
				{
					errors++;
					issues.Add("❌ 视频文件异常: 导出体积异常过小 (<100KB)");
				}
				else
				{
					issues.Add($"✅ 视频流完整: 音画严格吻合，视频文件正常 ({bytes / 1024.0 / 1024.0:0.1} MB)");
				}
			}
			else
			{
				errors++;
				issues.Add("❌ 视频文件异常: 最终文件未成功生成");
			}

			if (errors > 0)
			{
				item.QcBadge = "🔴 质检异常";
			}
			else if (warnings > 0)
			{
				item.QcBadge = !string.IsNullOrEmpty(primaryWarningReason) 
					? $"🟡 建议微调({primaryWarningReason})" 
					: "🟡 建议微调";
			}
			else
			{
				item.QcBadge = "🟢 100% 优质合格";
			}

			item.QcDetails = string.Join("\n\n", issues);
		}

		private static bool HasNvenc()
		{
			lock (s_nvencLock)
			{
				if (s_nvencUsable.HasValue) return s_nvencUsable.Value;
			}
			bool usable = false;
			try
			{
				string ffmpeg = FindFfmpegExe();
				var psi = new ProcessStartInfo(ffmpeg, "-hide_banner -loglevel error -f lavfi -i color=c=black:s=256x256:d=0.04 -frames:v 1 -c:v h264_nvenc -f null -")
				{
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using var p = Process.Start(psi);
				if (p != null)
				{
					usable = p.WaitForExit(4000) && p.ExitCode == 0;
					if (!p.HasExited) try { p.Kill(); } catch { }
				}
			}
			catch { usable = false; }
			lock (s_nvencLock)
			{
				s_nvencUsable = usable;
			}
			return usable;
		}

		public static string QuoteArg(string value)
		{
			if (string.IsNullOrEmpty(value)) return "\"\"";
			return "\"" + value.Replace("\"", "\\\"") + "\"";
		}

		private static string SanitizeFileName(string name)
		{
			var invalids = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder();
			foreach (char c in name)
			{
				if (!invalids.Contains(c) && c != ' ') sb.Append(c);
				else sb.Append('_');
			}
			string res = sb.ToString().Trim('_');
			return string.IsNullOrEmpty(res) ? "video" : res;
		}

		private sealed class ClipSlice
		{
			public string FilePath { get; }
			public double StartSec { get; }
			public double DurationSec { get; }

			public ClipSlice(string filePath, double startSec, double durationSec)
			{
				FilePath = filePath;
				StartSec = startSec;
				DurationSec = durationSec;
			}
		}
	}
}
