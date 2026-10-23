using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoBatchMerger.ScriptBatch
{
	internal sealed class VoiceCloneDialog : Form
	{
		private readonly ScriptBatchSettings _settings;
		private readonly Action _onVoiceUpdated;

		private TextBox _txtSourceFile;
		private Button _btnBrowse;
		private Button _btnAuditionSample;
		private TextBox _txtVoiceName;
		private TextBox _txtDescription;
		private Button _btnCloneLocal;
		private Button _btnCloneCloud;
		private TextBox _txtManualVoiceId;
		private Button _btnSaveManual;
		private DataGridView _grid;
		private Label _lblStatus;
		private ProgressBar _progress;

		private string _extractedAudioPath = string.Empty;
		private bool _isPlayingSample = false;
		private bool _isAuditioningCloned = false;

		private static readonly Color DarkBg = Color.FromArgb(15, 23, 42);
		private static readonly Color CardBg = Color.FromArgb(30, 41, 59);
		private static readonly Color InputBg = Color.FromArgb(20, 27, 45);
		private static readonly Color CardBorder = Color.FromArgb(51, 65, 85);
		private static readonly Color TextWhite = Color.FromArgb(248, 250, 252);
		private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
		private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
		private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
		private static readonly Color AccentRed = Color.FromArgb(239, 68, 68);
		private static readonly Color AccentYellow = Color.FromArgb(245, 158, 11);
		private static readonly Color AccentPurple = Color.FromArgb(168, 85, 247);

		public VoiceCloneDialog(ScriptBatchSettings settings, Action onVoiceUpdated)
		{
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));
			_onVoiceUpdated = onVoiceUpdated;

			Text = "🎙️ 声音克隆工坊 · 本地 RTX 5060 离线独显克隆 + 云端克隆双引擎";
			Size = new Size(950, 720);
			MinimumSize = new Size(840, 580);
			StartPosition = FormStartPosition.CenterParent;
			BackColor = DarkBg;
			ForeColor = TextWhite;
			Font = new Font("Microsoft YaHei UI", 9.25f);
			ShowIcon = false;

			InitializeComponents();
			RefreshGrid();
		}

		private void InitializeComponents()
		{
			var mainLayout = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 1,
				RowCount = 4,
				Padding = new Padding(16)
			};
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180f)); // 顶部：采样视频提取与克隆动作
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f));  // 手动绑定已有 Voice ID
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // 中部：本地克隆声音档案库
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45f));  // 底部：状态栏与完成按钮

			// ==========================================
			// 1. 顶部：母本参考采样提取与克隆触发
			// ==========================================
			var pnlExtract = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = CardBg,
				Padding = new Padding(14, 10, 14, 10)
			};

			var lblHeader = new Label
			{
				Text = "⚡ 本地 RTX 5060 离线声音克隆 (完全免费 · 零门槛 · 纯本地硬件复刻神父原声)",
				Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
				ForeColor = AccentGreen,
				AutoSize = true,
				Location = new Point(12, 8)
			};
			pnlExtract.Controls.Add(lblHeader);

			// 文件选择行
			var lblFile = new Label { Text = "参考视频/音频:", ForeColor = TextMuted, AutoSize = true, Location = new Point(12, 36) };
			_txtSourceFile = new TextBox
			{
				Location = new Point(125, 33),
				Width = 490,
				BackColor = InputBg,
				ForeColor = TextWhite,
				BorderStyle = BorderStyle.FixedSingle
			};
			_btnBrowse = new Button
			{
				Text = "📂 浏览文件...",
				Location = new Point(622, 31),
				Width = 105,
				Height = 26,
				BackColor = CardBorder,
				ForeColor = TextWhite,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand
			};
			_btnBrowse.FlatAppearance.BorderSize = 0;
			_btnBrowse.Click += (_, _) => BrowseSourceFile();

			_btnAuditionSample = new Button
			{
				Text = "🔊 试听采样",
				Location = new Point(733, 31),
				Width = 95,
				Height = 26,
				BackColor = AccentBlue,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand
			};
			_btnAuditionSample.FlatAppearance.BorderSize = 0;
			_btnAuditionSample.Click += (_, _) => ToggleAuditionSample();

			pnlExtract.Controls.Add(lblFile);
			pnlExtract.Controls.Add(_txtSourceFile);
			pnlExtract.Controls.Add(_btnBrowse);
			pnlExtract.Controls.Add(_btnAuditionSample);

			// 声音名称与描述行
			var lblName = new Label { Text = "克隆声音名称:", ForeColor = TextMuted, AutoSize = true, Location = new Point(12, 70) };
			_txtVoiceName = new TextBox
			{
				Location = new Point(125, 67),
				Width = 190,
				BackColor = InputBg,
				ForeColor = Color.White,
				BorderStyle = BorderStyle.FixedSingle,
				Text = "神父讲道原声(本地独显版)"
			};

			var lblDesc = new Label { Text = "语气风格描述:", ForeColor = TextMuted, AutoSize = true, Location = new Point(325, 70) };
			_txtDescription = new TextBox
			{
				Location = new Point(420, 67),
				Width = 280,
				BackColor = InputBg,
				ForeColor = TextWhite,
				BorderStyle = BorderStyle.FixedSingle,
				Text = "葡语神父深沉祈祷播报，语速平稳厚重"
			};

			pnlExtract.Controls.Add(lblName);
			pnlExtract.Controls.Add(_txtVoiceName);
			pnlExtract.Controls.Add(lblDesc);
			pnlExtract.Controls.Add(_txtDescription);

			// 克隆按钮栏（双模式）
			var btnRow = new FlowLayoutPanel
			{
				Location = new Point(125, 102),
				Size = new Size(760, 42),
				FlowDirection = FlowDirection.LeftToRight
			};

			_btnCloneLocal = new Button
			{
				Text = "🚀 本地 RTX 5060 一键克隆 (完全免费·离线·无视配额)",
				Width = 360,
				Height = 36,
				BackColor = AccentGreen,
				ForeColor = Color.White,
				Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand,
				Margin = new Padding(0, 0, 10, 0)
			};
			_btnCloneLocal.FlatAppearance.BorderSize = 0;
			_btnCloneLocal.Click += async (_, _) => await StartLocalCloneAsync();
			btnRow.Controls.Add(_btnCloneLocal);

			_btnCloneCloud = new Button
			{
				Text = "☁️ ElevenLabs 云端克隆 (需付费Key)",
				Width = 250,
				Height = 36,
				BackColor = CardBorder,
				ForeColor = TextMuted,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand
			};
			_btnCloneCloud.FlatAppearance.BorderSize = 0;
			_btnCloneCloud.Click += async (_, _) => await StartCloudCloneAsync();
			btnRow.Controls.Add(_btnCloneCloud);

			pnlExtract.Controls.Add(btnRow);

			var lblCloneTip = new Label
			{
				Text = "💡 本地克隆使用您自带的 RTX 5060 独显加速，约 2 秒即可完成母本声学特征提取，且永久离线可用！",
				ForeColor = AccentYellow,
				Font = new Font("Microsoft YaHei UI", 8f),
				Location = new Point(125, 146),
				AutoSize = true
			};
			pnlExtract.Controls.Add(lblCloneTip);

			mainLayout.Controls.Add(pnlExtract, 0, 0);

			// ==========================================
			// 2. 手动绑定已有 Voice ID 快捷栏
			// ==========================================
			var pnlManual = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(24, 33, 48),
				Padding = new Padding(14, 8, 14, 8),
				Margin = new Padding(0, 6, 0, 6)
			};
			var lblManual = new Label
			{
				Text = "已有 Voice ID 手动快捷绑定 (官网自建或团队共享 ID):",
				ForeColor = TextWhite,
				AutoSize = true,
				Location = new Point(12, 10)
			};
			_txtManualVoiceId = new TextBox
			{
				Location = new Point(12, 32),
				Width = 320,
				BackColor = InputBg,
				ForeColor = AccentYellow,
				BorderStyle = BorderStyle.FixedSingle
			};
			_btnSaveManual = new Button
			{
				Text = "💾 绑定保存此 Voice ID",
				Location = new Point(340, 30),
				Width = 170,
				Height = 26,
				BackColor = AccentBlue,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand
			};
			_btnSaveManual.FlatAppearance.BorderSize = 0;
			_btnSaveManual.Click += (_, _) => SaveManualVoiceId();

			pnlManual.Controls.Add(lblManual);
			pnlManual.Controls.Add(_txtManualVoiceId);
			pnlManual.Controls.Add(_btnSaveManual);
			mainLayout.Controls.Add(pnlManual, 0, 1);

			// ==========================================
			// 3. 中部：本地克隆声音档案库 DataGridView
			// ==========================================
			_grid = new DataGridView
			{
				Dock = DockStyle.Fill,
				BackgroundColor = CardBg,
				ForeColor = TextWhite,
				GridColor = CardBorder,
				BorderStyle = BorderStyle.None,
				RowHeadersVisible = false,
				AllowUserToAddRows = false,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				EnableHeadersVisualStyles = false,
				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
				ColumnHeadersHeight = 34,
				RowTemplate = { Height = 32 }
			};
			_grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(24, 34, 53);
			_grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
			_grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
			_grid.DefaultCellStyle.BackColor = CardBg;
			_grid.DefaultCellStyle.ForeColor = TextWhite;
			_grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 99, 235);
			_grid.DefaultCellStyle.SelectionForeColor = Color.White;

			var colCurrent = new DataGridViewTextBoxColumn { HeaderText = "当前状态", Width = 90, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
			var colEngine = new DataGridViewTextBoxColumn { HeaderText = "引擎类型", Width = 110, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
			var colName = new DataGridViewTextBoxColumn { HeaderText = "声音名称", Width = 170, ReadOnly = true };
			var colId = new DataGridViewTextBoxColumn { HeaderText = "标识 ID / 特征", Width = 160, ReadOnly = true, DefaultCellStyle = { ForeColor = AccentYellow } };
			var colDesc = new DataGridViewTextBoxColumn { HeaderText = "风格描述", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true };
			var colDate = new DataGridViewTextBoxColumn { HeaderText = "添加时间", Width = 95, ReadOnly = true };

			var btnSetCurrent = new DataGridViewButtonColumn
			{
				HeaderText = "设为默认",
				Text = "⭐ 选用",
				UseColumnTextForButtonValue = true,
				Width = 75,
				FlatStyle = FlatStyle.Flat
			};
			btnSetCurrent.DefaultCellStyle.BackColor = AccentGreen;
			btnSetCurrent.DefaultCellStyle.ForeColor = Color.White;

			var btnAudition = new DataGridViewButtonColumn
			{
				HeaderText = "试听",
				Text = "🔊 试听",
				UseColumnTextForButtonValue = true,
				Width = 70,
				FlatStyle = FlatStyle.Flat
			};
			btnAudition.DefaultCellStyle.BackColor = AccentBlue;
			btnAudition.DefaultCellStyle.ForeColor = Color.White;

			var btnDel = new DataGridViewButtonColumn
			{
				HeaderText = "删除",
				Text = "🗑️",
				UseColumnTextForButtonValue = true,
				Width = 55,
				FlatStyle = FlatStyle.Flat
			};
			btnDel.DefaultCellStyle.BackColor = Color.FromArgb(70, 30, 30);
			btnDel.DefaultCellStyle.ForeColor = AccentRed;

			_grid.Columns.AddRange(colCurrent, colEngine, colName, colId, colDesc, colDate, btnSetCurrent, btnAudition, btnDel);
			_grid.CellContentClick += async (s, e) => await OnGridCellActionAsync(e.RowIndex, e.ColumnIndex);

			mainLayout.Controls.Add(_grid, 0, 2);

			// ==========================================
			// 4. 底部：状态与关闭
			// ==========================================
			var bottomPanel = new Panel { Dock = DockStyle.Fill };
			_lblStatus = new Label
			{
				Text = "就绪。推荐优先点击绿色按钮使用【本地 RTX 5060 一键克隆】，完全离线且无需付费 API Key！",
				ForeColor = TextMuted,
				AutoSize = true,
				Location = new Point(0, 12)
			};
			_progress = new ProgressBar
			{
				Location = new Point(510, 10),
				Width = 200,
				Height = 18,
				Visible = false,
				Style = ProgressBarStyle.Marquee
			};
			var btnClose = new Button
			{
				Text = "完成 / 关闭",
				Location = new Point(720, 4),
				Width = 110,
				Height = 32,
				BackColor = AccentBlue,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand,
				DialogResult = DialogResult.OK
			};
			btnClose.FlatAppearance.BorderSize = 0;

			bottomPanel.Controls.Add(_lblStatus);
			bottomPanel.Controls.Add(_progress);
			bottomPanel.Controls.Add(btnClose);
			mainLayout.Controls.Add(bottomPanel, 0, 3);

			Controls.Add(mainLayout);
		}

		private void BrowseSourceFile()
		{
			using var ofd = new OpenFileDialog
			{
				Title = "选择用于克隆母本声音的参考视频或音频文件",
				Filter = "音视频文件 (*.mp4;*.mov;*.mkv;*.mp3;*.wav;*.m4a)|*.mp4;*.mov;*.mkv;*.mp3;*.wav;*.m4a|视频文件 (*.mp4;*.mov;*.mkv)|*.mp4;*.mov;*.mkv|音频文件 (*.mp3;*.wav;*.m4a)|*.mp3;*.wav;*.m4a|所有文件 (*.*)|*.*"
			};
			if (ofd.ShowDialog(this) == DialogResult.OK)
			{
				_txtSourceFile.Text = ofd.FileName;
				string baseName = Path.GetFileNameWithoutExtension(ofd.FileName);
				if (!string.IsNullOrWhiteSpace(baseName))
				{
					_txtVoiceName.Text = $"{baseName}_独显克隆原声";
				}
				_extractedAudioPath = string.Empty;
				_lblStatus.Text = $"已选中参考文件: {Path.GetFileName(ofd.FileName)}";
				_lblStatus.ForeColor = AccentGreen;
			}
		}

		private void ToggleAuditionSample()
		{
			if (_isPlayingSample)
			{
				ScriptBatchTtsEngine.StopAudio();
				_isPlayingSample = false;
				_btnAuditionSample.Text = "🔊 试听采样";
				_btnAuditionSample.BackColor = AccentBlue;
				_lblStatus.Text = "已停止试听";
				return;
			}

			string file = _txtSourceFile.Text.Trim();
			if (!File.Exists(file))
			{
				MessageBox.Show(this, "请先选择有效的参考视频或音频文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			string audioFile = EnsureExtractedAudio(file);
			if (string.IsNullOrEmpty(audioFile) || !File.Exists(audioFile))
			{
				MessageBox.Show(this, "提取采样音频失败，请确保 FFmpeg 可用且视频包含有效音频流。", "提取失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			_isPlayingSample = true;
			_btnAuditionSample.Text = "⏹️ 停止试听";
			_btnAuditionSample.BackColor = AccentRed;
			_lblStatus.Text = "正在播放提取的采样原声音频...";

			ScriptBatchTtsEngine.PlayAudioFile(audioFile, () =>
			{
				if (IsHandleCreated)
				{
					BeginInvoke((MethodInvoker)(() =>
					{
						_isPlayingSample = false;
						_btnAuditionSample.Text = "🔊 试听采样";
						_btnAuditionSample.BackColor = AccentBlue;
						_lblStatus.Text = "采样试听结束";
					}));
				}
			});
		}

		private string EnsureExtractedAudio(string sourceFile)
		{
			if (!File.Exists(sourceFile)) return null;

			string ext = Path.GetExtension(sourceFile).ToLowerInvariant();
			if (ext == ".wav" || ext == ".mp3")
			{
				return sourceFile;
			}

			if (!string.IsNullOrEmpty(_extractedAudioPath) && File.Exists(_extractedAudioPath))
			{
				return _extractedAudioPath;
			}

			string tempAudio = Path.Combine(Path.GetTempPath(), $"clone_sample_{DateTime.Now.Ticks}.wav");
			string ffmpeg = ScriptBatchComposer.FindFfmpegExe();

			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = ffmpeg,
					Arguments = $"-y -i {ScriptBatchComposer.QuoteArg(sourceFile)} -vn -acodec pcm_s16le -ar 24000 -ac 1 {ScriptBatchComposer.QuoteArg(tempAudio)}",
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardError = true
				};
				using var proc = Process.Start(psi);
				proc.WaitForExit(15000);
				if (File.Exists(tempAudio) && new FileInfo(tempAudio).Length > 1000)
				{
					_extractedAudioPath = tempAudio;
					return tempAudio;
				}
			}
			catch { }

			return null;
		}

		private async Task StartLocalCloneAsync()
		{
			string src = _txtSourceFile.Text.Trim();
			if (!File.Exists(src))
			{
				MessageBox.Show(this, "请先选择参考视频或音频文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			string vName = _txtVoiceName.Text.Trim();
			if (string.IsNullOrEmpty(vName))
			{
				MessageBox.Show(this, "请输入克隆声音的名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			_btnCloneLocal.Enabled = false;
			_btnCloneCloud.Enabled = false;
			_progress.Visible = true;
			_lblStatus.Text = "正在使用 RTX 5060 独立显卡提取声学特征并生成本地模型缓存...";
			_lblStatus.ForeColor = AccentYellow;

			try
			{
				string sampleAudio = EnsureExtractedAudio(src);
				if (string.IsNullOrEmpty(sampleAudio) || !File.Exists(sampleAudio))
				{
					MessageBox.Show(this, "提取采样音频失败，请检查音视频文件！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				var cloneRes = await LocalCloneEngine.ExtractSpeakerLatentAsync(vName, sampleAudio);
				if (cloneRes.Success && !string.IsNullOrEmpty(cloneRes.LatentPath))
				{
					string safeId = $"local:{Path.GetFileNameWithoutExtension(cloneRes.LatentPath)}";
					var newItem = new ClonedVoiceItem
					{
						Name = vName,
						VoiceId = safeId,
						Description = _txtDescription.Text.Trim(),
						CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
						EngineType = "local_xtts",
						LatentPath = cloneRes.LatentPath,
						ReferenceAudioPath = sampleAudio
					};

					_settings.ClonedVoices.Insert(0, newItem);
					_settings.TtsEngine = "local_xtts";
					_settings.DefaultVoice = newItem.VoiceId;
					_settings.Save();

					RefreshGrid();
					_onVoiceUpdated?.Invoke();

					_lblStatus.Text = $"🎉 本地 RTX 5060 声音克隆成功！已自动设为当前默认音色";
					_lblStatus.ForeColor = AccentGreen;
					MessageBox.Show(this, $"恭喜！声音【{vName}】已由本地 RTX 5060 独显克隆完成！\n\n模型特征: {Path.GetFileName(cloneRes.LatentPath)}\n已自动设为您当前的成片音色！\n\n✅ 100% 离线纯本地运行\n✅ 完全免费，无需 API Key 与任何订阅\n✅ 无限字数批量生成，尽情出片！", "本地声音克隆成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				else
				{
					_lblStatus.Text = "本地克隆未成功，已记录错误详情";
					_lblStatus.ForeColor = AccentRed;
					MessageBox.Show(this, $"本地克隆未成功:\n\n{cloneRes.ErrorMessage}", "克隆提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, $"克隆过程发生异常: {ex.Message}", "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			finally
			{
				_btnCloneLocal.Enabled = true;
				_btnCloneCloud.Enabled = true;
				_progress.Visible = false;
			}
		}

		private async Task StartCloudCloneAsync()
		{
			string src = _txtSourceFile.Text.Trim();
			if (!File.Exists(src))
			{
				MessageBox.Show(this, "请先选择参考视频或音频文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			string vName = _txtVoiceName.Text.Trim();
			if (string.IsNullOrEmpty(vName))
			{
				MessageBox.Show(this, "请输入克隆声音的名称！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			string key = ElevenLabsKeyPool.Instance.GetNextAvailableKey();
			if (string.IsNullOrEmpty(key))
			{
				MessageBox.Show(this, "未配置可用的 ElevenLabs API 密钥！\n请先在主界面点击【🔑 ElevenLabs 密钥管理】添加可用 Key。", "密钥未就绪", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			_btnCloneLocal.Enabled = false;
			_btnCloneCloud.Enabled = false;
			_progress.Visible = true;
			_lblStatus.Text = "正在使用 FFmpeg 提取采样并上传至 ElevenLabs 克隆模型...";
			_lblStatus.ForeColor = AccentYellow;

			try
			{
				string sampleAudio = EnsureExtractedAudio(src);
				if (string.IsNullOrEmpty(sampleAudio) || !File.Exists(sampleAudio))
				{
					MessageBox.Show(this, "提取采样音频失败，请检查音视频文件！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				var cloneRes = await ElevenLabsEngine.CloneVoiceAsync(vName, _txtDescription.Text.Trim(), sampleAudio, key);
				if (cloneRes.Success && !string.IsNullOrEmpty(cloneRes.VoiceId))
				{
					var newItem = new ClonedVoiceItem
					{
						Name = vName,
						VoiceId = cloneRes.VoiceId,
						Description = _txtDescription.Text.Trim(),
						CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
						EngineType = "elevenlabs"
					};

					_settings.ClonedVoices.Insert(0, newItem);
					_settings.TtsEngine = "elevenlabs";
					_settings.ElevenLabsVoice = newItem.VoiceId;
					_settings.Save();

					RefreshGrid();
					_onVoiceUpdated?.Invoke();

					_lblStatus.Text = $"🎉 云端克隆成功！已自动设为当前默认音色 (ID: {newItem.VoiceId})";
					_lblStatus.ForeColor = AccentGreen;
					MessageBox.Show(this, $"声音【{vName}】已在 ElevenLabs 云端克隆成功并同步保存！\n\nVoice ID: {newItem.VoiceId}\n已自动设为您当前的成片音色！", "云端声音克隆成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				else
				{
					_lblStatus.Text = "云端克隆未成功，已记录错误详情";
					_lblStatus.ForeColor = AccentRed;
					MessageBox.Show(this, $"克隆请求未成功:\n\n{cloneRes.ErrorMessage}", "克隆提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, $"克隆过程发生异常: {ex.Message}", "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			finally
			{
				_btnCloneLocal.Enabled = true;
				_btnCloneCloud.Enabled = true;
				_progress.Visible = false;
			}
		}

		private void SaveManualVoiceId()
		{
			string vid = _txtManualVoiceId.Text.Trim();
			if (string.IsNullOrEmpty(vid))
			{
				MessageBox.Show(this, "请输入有效的 ElevenLabs Voice ID！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			string vName = _txtVoiceName.Text.Trim();
			if (string.IsNullOrEmpty(vName)) vName = $"自定义声音_{vid.Substring(0, Math.Min(6, vid.Length))}";

			var existing = _settings.ClonedVoices.FirstOrDefault(v => string.Equals(v.VoiceId, vid, StringComparison.OrdinalIgnoreCase));
			if (existing != null)
			{
				existing.Name = vName;
				existing.Description = _txtDescription.Text.Trim();
			}
			else
			{
				_settings.ClonedVoices.Insert(0, new ClonedVoiceItem
				{
					Name = vName,
					VoiceId = vid,
					Description = _txtDescription.Text.Trim(),
					CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
					EngineType = "elevenlabs"
				});
			}

			_settings.TtsEngine = "elevenlabs";
			_settings.ElevenLabsVoice = vid;
			_settings.Save();
			_txtManualVoiceId.Clear();

			RefreshGrid();
			_onVoiceUpdated?.Invoke();

			_lblStatus.Text = $"已绑定并保存 Voice ID: {vid}，并已设为当前默认音色";
			_lblStatus.ForeColor = AccentGreen;
			MessageBox.Show(this, $"Voice ID 【{vid}】已成功绑定保存！\n声音名称: {vName}\n已设为当前默认音色。", "绑定成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void RefreshGrid()
		{
			_grid.Rows.Clear();
			if (_settings.ClonedVoices == null) _settings.ClonedVoices = new System.Collections.Generic.List<ClonedVoiceItem>();

			foreach (var item in _settings.ClonedVoices)
			{
				bool isCurrent = string.Equals(_settings.ElevenLabsVoice, item.VoiceId, StringComparison.OrdinalIgnoreCase) ||
				                 string.Equals(_settings.DefaultVoice, item.VoiceId, StringComparison.OrdinalIgnoreCase);
				string currentTag = isCurrent ? "🟢 使用中" : "⚪ 空闲";
				string engineTag = item.EngineType == "local_xtts" ? "⚡ 本地独显" : "☁️ ElevenLabs";
				int rIdx = _grid.Rows.Add(currentTag, engineTag, item.Name, item.VoiceId, item.Description, item.CreatedAt);
				_grid.Rows[rIdx].Tag = item;
				if (isCurrent)
				{
					_grid.Rows[rIdx].DefaultCellStyle.BackColor = Color.FromArgb(20, 45, 35);
				}
				if (item.EngineType == "local_xtts")
				{
					_grid.Rows[rIdx].Cells[1].Style.ForeColor = AccentGreen;
				}
				else
				{
					_grid.Rows[rIdx].Cells[1].Style.ForeColor = AccentBlue;
				}
			}
		}

		private async Task OnGridCellActionAsync(int rowIndex, int colIndex)
		{
			if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return;
			if (!(_grid.Rows[rowIndex].Tag is ClonedVoiceItem item)) return;

			if (colIndex == 6) // 设为默认
			{
				if (item.EngineType == "local_xtts")
				{
					_settings.TtsEngine = "local_xtts";
					_settings.DefaultVoice = item.VoiceId;
				}
				else
				{
					_settings.TtsEngine = "elevenlabs";
					_settings.ElevenLabsVoice = item.VoiceId;
				}
				_settings.Save();
				RefreshGrid();
				_onVoiceUpdated?.Invoke();
				_lblStatus.Text = $"已切换当前生成音色为: 【{item.Name}】({(item.EngineType == "local_xtts" ? "本地独显" : "云端API")})";
				_lblStatus.ForeColor = AccentGreen;
			}
			else if (colIndex == 7) // 试听
			{
				if (_isAuditioningCloned)
				{
					ScriptBatchTtsEngine.StopAudio();
					_isAuditioningCloned = false;
					_lblStatus.Text = "已停止试听";
					return;
				}

				_isAuditioningCloned = true;
				_lblStatus.Text = $"正在使用【{item.Name}】合成试听语段...";
				_lblStatus.ForeColor = AccentYellow;

				try
				{
					string sampleText = "Senhora que está segurando um telefone, talvez eu não saiba o seu nome, mas você é abençoada.";
					string tempDir = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_CloneAudition");
					TtsResult res;

					if (item.EngineType == "local_xtts")
					{
						string speakerRef = !string.IsNullOrEmpty(item.LatentPath) && File.Exists(item.LatentPath)
							? item.LatentPath
							: item.ReferenceAudioPath;
						res = await LocalCloneEngine.SynthesizeAsync(sampleText, speakerRef, tempDir, 888, CancellationToken.None);
					}
					else
					{
						res = await ElevenLabsEngine.SynthesizeWithFailoverAsync(sampleText, item.VoiceId, tempDir, 888, CancellationToken.None);
					}

					if (res.Success && File.Exists(res.AudioPath))
					{
						_lblStatus.Text = $"🔊 正在试听【{item.Name}】原声播报效果...";
						ScriptBatchTtsEngine.PlayAudioFile(res.AudioPath, () =>
						{
							if (IsHandleCreated)
							{
								BeginInvoke((MethodInvoker)(() =>
								{
									_isAuditioningCloned = false;
									_lblStatus.Text = "试听播放完成";
									_lblStatus.ForeColor = TextWhite;
								}));
							}
						});
					}
					else
					{
						_isAuditioningCloned = false;
						_lblStatus.Text = $"试听合成未成功: {res.ErrorMessage}";
						_lblStatus.ForeColor = AccentRed;
						MessageBox.Show(this, $"试听合成失败:\n{res.ErrorMessage}", "试听失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					}
				}
				catch (Exception ex)
				{
					_isAuditioningCloned = false;
					_lblStatus.Text = $"试听异常: {ex.Message}";
				}
			}
			else if (colIndex == 8) // 删除
			{
				var dr = MessageBox.Show(this, $"确定要从软件中移除克隆声音【{item.Name}】吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
				if (dr == DialogResult.Yes)
				{
					_settings.ClonedVoices.Remove(item);
					if (string.Equals(_settings.ElevenLabsVoice, item.VoiceId, StringComparison.OrdinalIgnoreCase) ||
					    string.Equals(_settings.DefaultVoice, item.VoiceId, StringComparison.OrdinalIgnoreCase))
					{
						_settings.TtsEngine = "edge_tts";
						_settings.DefaultVoice = "pt-PT-DuarteNeural";
					}
					_settings.Save();
					RefreshGrid();
					_onVoiceUpdated?.Invoke();
					_lblStatus.Text = $"已移除克隆声音【{item.Name}】";
				}
			}
		}
	}
}
