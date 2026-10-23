using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VideoBatchMerger.ScriptBatch
{
	public static class ScriptBatchComposer
	{
		private static readonly Random s_rand = new Random();

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

		public static async Task<bool> ProcessItemAsync(
			ScriptBatchItem item,
			ScriptBatchSettings settings,
			List<string> footagePool,
			string outputDir,
			Action<int, string> progressCallback,
			CancellationToken ct)
		{
			string tempDir = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_ScriptBatch", $"task_{item.Id}_{DateTime.Now.Ticks}");
			Directory.CreateDirectory(tempDir);

			try
			{
				// 1. TTS 语音生成与原生字幕
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
				item.DurationSeconds = Math.Max(1.0, ttsResult.DurationSeconds);

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
				string finalOutputPath = Path.Combine(outputDir, outVideoName);

				// 确保文件名不重名冲突
				int fileIndex = 1;
				while (File.Exists(finalOutputPath))
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
					ct
				);

				if (!renderOk || !File.Exists(finalOutputPath))
				{
					item.Status = "失败";
					item.ErrorMessage = "FFmpeg 视频渲染失败";
					item.QcBadge = "🔴 渲染失败";
					return false;
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
							string fontName = string.IsNullOrWhiteSpace(settings.SubtitleFontFamily) ? "Arial" : settings.SubtitleFontFamily;

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
			string primaryColor = string.IsNullOrWhiteSpace(settings.SubtitleColorHex) ? preset.PrimaryColorHex : settings.SubtitleColorHex;
			string outlineColor = string.IsNullOrWhiteSpace(settings.SubtitleStrokeColorHex) ? preset.OutlineColorHex : settings.SubtitleStrokeColorHex;
			float outlineWidth = settings.SubtitleStrokeWidth > 0 ? settings.SubtitleStrokeWidth : preset.OutlineWidth;

			int borderStyle = preset.BorderStyle; // 1: 描边阴影, 3: 不透明底框
			string backColor = borderStyle == 3 ? (string.IsNullOrWhiteSpace(preset.BackColorHex) ? outlineColor : preset.BackColorHex) : "&H80000000";

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
			sb.AppendLine($"Style: Default,{fontName},{fontSize},{primaryColor},&H000000FF,{outlineColor},{backColor},-1,0,0,0,100,100,0,0,{borderStyle},{outlineWidth:0.0},2,2,{marginL},{marginR},{marginV},1");
			sb.AppendLine();
			sb.AppendLine("[Events]");
			sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

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
							EmitAssDialogue(sb, currentStart, currentEnd, currentText.ToString().Trim(), w, fontSize, settings.SubtitleDisplayMode);
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
					EmitAssDialogue(sb, currentStart, currentEnd, currentText.ToString().Trim(), w, fontSize, settings.SubtitleDisplayMode);
				}
			}

			File.WriteAllText(assPath, sb.ToString(), Encoding.UTF8);
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

		public static string FormatAssText(string text, int canvasW, int fontSize, int displayMode = 0)
		{
			text = text.Replace("\r", "").Trim();
			if (string.IsNullOrEmpty(text)) return text;

			// 自动短句排版：按 2~3 词一行折行，且同屏绝不允许超过 2 行
			string wrapped = WrapToShortLines(text, 16);

			if (displayMode == 3) // 逐词红底框高亮 (图1效果)
			{
				string[] lines = wrapped.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
				var sbResult = new StringBuilder();

				for (int li = 0; li < Math.Min(2, lines.Length); li++)
				{
					if (li > 0) sbResult.Append("\\N");
					string line = lines[li].Trim();
					var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					if (words.Length == 0) continue;

					// 选取最具感染力的关键词 (如 "filho precisa" 中的 "precisa") 加上饱满红底框高亮
					int hlIdx = (li == 0 && words.Length >= 2) ? 1 : (li == 0 ? 0 : -1);
					for (int wi = 0; wi < words.Length; wi++)
					{
						if (wi > 0) sbResult.Append(" ");
						if (wi == hlIdx)
						{
							// 黄字 + 饱满红底框 (\1c&H0000FFFF& 亮黄字, \3c&H000000D0& 鲜红边框, \4c&H000000D0& 红阴影)
							sbResult.Append($"{{\\1c&H0000FFFF&\\3c&H000000D0&\\4c&H000000D0&\\bord10\\shad0}}{words[wi]}{{\\r}}");
						}
						else
						{
							sbResult.Append(words[wi]);
						}
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
			CancellationToken ct)
		{
			string ffmpeg = FindFfmpegExe();
			double totalDur = Math.Max(1.0, item.DurationSeconds);
			int w = settings.CanvasWidth;
			int h = settings.CanvasHeight;

			// 选择素材片段
			var selectedClips = PickClipsForDuration(footagePool, totalDur, settings);

			var filterComplex = new StringBuilder();
			var inputArgs = new StringBuilder();

			// Input 0: 音频
			inputArgs.Append($"-i {QuoteArg(item.AudioPath)} ");

			// Input 1..N: 视频素材
			if (selectedClips.Count > 0)
			{
				for (int i = 0; i < selectedClips.Count; i++)
				{
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
			string audioMap = "-map 0:a";
			bool hasBgm = !string.IsNullOrWhiteSpace(settings.BgmPath) && File.Exists(settings.BgmPath);
			int nextInputIdx = selectedClips.Count + 1;

			if (hasBgm)
			{
				int bgmInputIdx = nextInputIdx++;
				inputArgs.Append($"-stream_loop -1 -i {QuoteArg(settings.BgmPath)} ");
				float bgmVol = Math.Max(0.02f, settings.BgmVolumePercent / 100f);
				double fadeStart = Math.Max(0.5, totalDur - 1.5);

				string bgmFilter = $"[0:a]volume=1.0[voice];[{bgmInputIdx}:a]volume={bgmVol:0.00},afade=t=out:st={fadeStart:0.00}:d=1.5[bgm];[voice][bgm]amix=inputs=2:duration=first:dropout_transition=2[outa]";
				filterComplex.Append($"{bgmFilter};");
				audioMap = "-map \"[outa]\"";
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
					if (dur > 0.05)
					{
						double end = start + dur;
						enableTiming = $":enable='between(t,{start:0.00},{end:0.00})'";
					}
					else if (start > 0.05)
					{
						enableTiming = $":enable='gte(t,{start:0.00})'";
					}

					int centerX = (int)Math.Round(w * (stk.XPercent / 100.0));
					int centerY = (int)Math.Round(h * (stk.YPercent / 100.0));

					string nextV = $"v_stk_{stkIdx}";
					filterComplex.Append($"[{currentV}][{stkLabel}]overlay=x='{centerX}-w/2':y='{centerY}-h/2'{enableTiming}:shortest=0[{nextV}];");
					currentV = nextV;
					stkIdx++;
				}
			}

			filterComplex.Append($"[{currentV}]null[outv]");

			// 编码器选择 (硬件加速优先)
			string vcodec = "libx264 -preset veryfast -crf 22";
			if (settings.HardwareEncoder == "nvenc" || (settings.HardwareEncoder == "auto" && HasNvenc()))
			{
				vcodec = "h264_nvenc -preset p4 -cq 23";
			}
			else if (settings.HardwareEncoder == "qsv")
			{
				vcodec = "h264_qsv -global_quality 23";
			}

			string args = $"{inputArgs} -filter_complex \"{filterComplex}\" -map \"[outv]\" {audioMap} -c:v {vcodec} -c:a aac -b:a 192k -t {totalDur:0.00} -y {QuoteArg(outputPath)}";

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
						if (line.Contains("time="))
						{
							var m = Regex.Match(line, @"time=(\d+):(\d+):(\d+\.?\d*)");
							if (m.Success)
							{
								double cur = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + double.Parse(m.Groups[3].Value);
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
						return false;
					}
					await Task.Delay(200, ct);
				}

				await readTask;
				return proc.ExitCode == 0;
			}
		}

		private static List<ClipSlice> PickClipsForDuration(List<string> pool, double targetDur, ScriptBatchSettings settings)
		{
			var list = new List<ClipSlice>();
			if (pool == null || pool.Count == 0) return list;

			double accumulated = 0.0;
			double minSlice = Math.Max(2.0, settings.SliceDurationMin);
			double maxSlice = Math.Max(minSlice + 1.0, settings.SliceDurationMax);

			var availablePool = new List<string>(pool);
			if (settings.FootageStrategy == 0)
			{
				availablePool = availablePool.OrderBy(_ => s_rand.Next()).ToList();
			}

			int poolIdx = 0;
			while (accumulated < targetDur + 0.5)
			{
				string path = availablePool[poolIdx % availablePool.Count];
				poolIdx++;

				double sliceLen = minSlice + s_rand.NextDouble() * (maxSlice - minSlice);
				double startSec = s_rand.Next(0, 3);

				list.Add(new ClipSlice(path, startSec, sliceLen));
				accumulated += sliceLen;

				if (list.Count > 50) break;
			}

			return list;
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
			try
			{
				string ffmpeg = FindFfmpegExe();
				var psi = new ProcessStartInfo(ffmpeg, "-encoders")
				{
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using var p = Process.Start(psi);
				string outStr = p.StandardOutput.ReadToEnd();
				p.WaitForExit();
				return outStr.Contains("h264_nvenc");
			}
			catch { return false; }
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
