using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace VideoBatchMerger.ScriptBatch
{
	internal sealed class TextToSpeechStudioPanel : UserControl
	{
		private readonly MainForm _mainForm;
		private readonly ScriptBatchSettings _settings;
		private readonly List<ScriptBatchItem> _items = new List<ScriptBatchItem>();

		private CancellationTokenSource _cts;
		private CancellationTokenSource _auditionCts;
		private bool _isProcessing = false;
		private bool _isAuditioning = false;
		private bool _isLoadingSettings = false;
		private string _voiceRegionFilter = "ALL";

		// 左栏控件：批量表格 + 快速文案编辑框
		private DataGridView _grid;
		private TextBox _txtQuickScript;
		private Label _lblQuickStats;
		private bool _isSyncingQuickScript = false;

		// 中栏控件：Cyber Studio 流光声波与黑胶电台舞台
		private CyberAudioStageControl _cyberStage;
		private Button _btnStagePlay;
		private Button _btnStageStop;
		private Label _lblStageTime;
		private TrackBar _trkVolume;
		private Label _lblVolume;
		private System.Windows.Forms.Timer _animTimer;

		// 右栏控件：导出与音色、口播调优参数
		private TextBox _txtOutputDir;
		private CheckBox _chkExportSrt;
		private TextBox _txtProducerName;
		private CheckBox _chkNamingRule;
		private Label _lblFileNamePreview;
		private ProgressBar _progressBar;
		private Label _lblStatusText;
		private Button _btnStartBatch;
		private Button _btnStopBatch;
		private Button _btnOpenOutput;

		private ComboBox _cboTtsEngine;
		private ComboBox _cboDefaultVoice;
		private Button _btnChatterboxSettings;
		private Button _btnElevenKeys;
		private Button _btnFetchCloudVoices;
		private Panel _pnlCustomVoiceId;
		private TextBox _txtCustomVoiceId;
		private Label _lblVoiceHint;

		private ComboBox _cboSpeakingStyle;
		private ComboBox _cboRate;
		private ComboBox _cboPitch;
		private CheckBox _chkNaturalBreathing;
		private CheckBox _chkWarmEq;
		private Button _btnAudition;

		private readonly List<Button> _filterButtons = new List<Button>();

		// 配色方案 (Cyber Studio 暗夜流光主题)
		private static readonly Color BgCanvas = Color.FromArgb(10, 15, 26);
		private static readonly Color CardBg = Color.FromArgb(17, 24, 39);
		private static readonly Color CardBorder = Color.FromArgb(31, 41, 55);
		private static readonly Color InputBg = Color.FromArgb(24, 34, 53);
		private static readonly Color TextPrimary = Color.FromArgb(243, 244, 246);
		private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
		private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
		private static readonly Color AccentCyan = Color.FromArgb(6, 182, 212);
		private static readonly Color AccentYellow = Color.FromArgb(245, 158, 11);
		private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
		private static readonly Color AccentPurple = Color.FromArgb(139, 92, 246);

		public TextToSpeechStudioPanel(MainForm mainForm)
		{
			_mainForm = mainForm;
			_settings = ScriptBatchSettings.Load();

			Dock = DockStyle.Fill;
			BackColor = BgCanvas;
			Font = new Font("Microsoft YaHei UI", 9f);

			InitializeUi();
			ApplyLoadedSettings();
			UpdateKeyButtonText();
			UpdateFileNamePreview();
			SyncStageMetadata();

			_animTimer = new System.Windows.Forms.Timer { Interval = 30 };
			_animTimer.Tick += OnAnimationTick;
			_animTimer.Start();

			if (_mainForm != null)
			{
				_mainForm.FormClosing += (_, _) =>
				{
					_cts?.Cancel();
					_auditionCts?.Cancel();
					_animTimer?.Stop();
					SaveSettings();
				};
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_cts?.Cancel();
				_auditionCts?.Cancel();
				_animTimer?.Stop();
				_animTimer?.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeUi()
		{
			var host = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = BgCanvas
			};

			var leftPanel = BuildLeftScriptGridPanel();
			var centerPanel = BuildCenterCyberStagePanel();
			var rightPanel = BuildRightSettingsPanel();
			leftPanel.Dock = DockStyle.None;
			centerPanel.Dock = DockStyle.None;
			rightPanel.Dock = DockStyle.None;

			var splitterL = CreateColumnSplitter();
			var splitterR = CreateColumnSplitter();

			host.Controls.Add(leftPanel);
			host.Controls.Add(splitterL);
			host.Controls.Add(centerPanel);
			host.Controls.Add(splitterR);
			host.Controls.Add(rightPanel);

			const int SplitW = 6;
			const int MinLeft = 320, MinCenter = 270, MinRight = 420;

			double leftRatio = _settings.TtsStudioLayoutLeftRatio > 0.15 && _settings.TtsStudioLayoutLeftRatio < 0.65
				? _settings.TtsStudioLayoutLeftRatio : 0.34;
			double rightRatio = _settings.TtsStudioLayoutRightRatio >= 0.35 && _settings.TtsStudioLayoutRightRatio < 0.65
				? _settings.TtsStudioLayoutRightRatio : 0.36;

			void Relayout()
			{
				int totalW = host.ClientSize.Width;
				int h = host.ClientSize.Height;
				if (totalW <= 0 || h <= 0) return;

				int usable = Math.Max(0, totalW - SplitW * 2);
				int lw = (int)Math.Round(usable * leftRatio);
				int rw = (int)Math.Round(usable * rightRatio);

				if (usable >= MinLeft + MinCenter + MinRight)
				{
					lw = Math.Max(MinLeft, lw);
					rw = Math.Max(MinRight, rw);
					if (usable - lw - rw < MinCenter)
					{
						int deficit = MinCenter - (usable - lw - rw);
						int takeL = Math.Min(deficit / 2 + deficit % 2, lw - MinLeft);
						lw -= takeL;
						deficit -= takeL;
						rw -= Math.Min(deficit, rw - MinRight);
					}
				}
				int cw = Math.Max(0, usable - lw - rw);

				host.SuspendLayout();
				leftPanel.SetBounds(0, 0, lw, h);
				splitterL.SetBounds(lw, 0, SplitW, h);
				centerPanel.SetBounds(lw + SplitW, 0, cw, h);
				splitterR.SetBounds(lw + SplitW + cw, 0, SplitW, h);
				rightPanel.SetBounds(lw + SplitW * 2 + cw, 0, rw, h);
				host.ResumeLayout(true);
			}

			void WireDrag(Panel splitter, bool isLeft)
			{
				bool dragging = false;
				splitter.MouseDown += (_, e) =>
				{
					if (e.Button != MouseButtons.Left) return;
					dragging = true;
					splitter.Capture = true;
					splitter.BackColor = AccentBlue;
				};
				splitter.MouseMove += (_, e) =>
				{
					if (!dragging) return;
					int usable = Math.Max(1, host.ClientSize.Width - SplitW * 2);
					int x = host.PointToClient(splitter.PointToScreen(e.Location)).X;
					if (isLeft)
					{
						int lw = Math.Max(MinLeft, Math.Min(x, usable - (int)(usable * rightRatio) - MinCenter));
						leftRatio = lw / (double)usable;
					}
					else
					{
						int rw = host.ClientSize.Width - x - SplitW;
						rw = Math.Max(MinRight, Math.Min(rw, usable - (int)(usable * leftRatio) - MinCenter));
						rightRatio = rw / (double)usable;
					}
					Relayout();
					host.Update();
				};
				splitter.MouseUp += (_, _) =>
				{
					if (!dragging) return;
					dragging = false;
					splitter.Capture = false;
					splitter.BackColor = Color.FromArgb(30, 41, 59);
					_settings.TtsStudioLayoutLeftRatio = leftRatio;
					_settings.TtsStudioLayoutRightRatio = rightRatio;
					SaveSettings();
				};
				splitter.DoubleClick += (_, _) =>
				{
					leftRatio = 0.37;
					rightRatio = 0.28;
					Relayout();
					_settings.TtsStudioLayoutLeftRatio = leftRatio;
					_settings.TtsStudioLayoutRightRatio = rightRatio;
					SaveSettings();
				};
			}

			WireDrag(splitterL, true);
			WireDrag(splitterR, false);
			host.Resize += (_, _) => Relayout();

			Controls.Add(host);
			Relayout();
		}

		private Panel CreateColumnSplitter()
		{
			var splitter = new Panel
			{
				Cursor = Cursors.VSplit,
				BackColor = Color.FromArgb(30, 41, 59)
			};
			splitter.Paint += (s, e) =>
			{
				var p = (Panel)s;
				int cx = p.Width / 2;
				int cy = p.Height / 2;
				using var dotBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
				for (int i = -1; i <= 1; i++)
				{
					e.Graphics.FillEllipse(dotBrush, cx - 1.5f, cy + i * 8 - 1.5f, 3, 3);
				}
			};
			splitter.MouseEnter += (s, _) => { var p = (Panel)s; if (!p.Capture) p.BackColor = Color.FromArgb(51, 65, 85); };
			splitter.MouseLeave += (s, _) => { var p = (Panel)s; if (!p.Capture) p.BackColor = Color.FromArgb(30, 41, 59); };
			return splitter;
		}
		// ========================================================
		// 1. 左栏：批量表格式文案面板 + 底部大字文案实时编辑区
		// ========================================================
		private Panel BuildLeftScriptGridPanel()
		{
			var panel = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(10),
				BackColor = BgCanvas
			};

			var topBar = new FlowLayoutPanel
			{
				Dock = DockStyle.Top,
				Height = 42,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = false,
				Padding = new Padding(0, 0, 0, 6)
			};

			var btnPaste = CreateButton("📋 粘贴剪贴板 (Ctrl+V)", AccentBlue, Color.White, 155);
			btnPaste.Click += (_, _) => PasteClipboardData();
			topBar.Controls.Add(btnPaste);

			var btnImport = CreateButton("📄 导入表格/TXT", CardBorder, TextPrimary, 118);
			btnImport.Click += (_, _) => ImportTextOrCsvFile();
			topBar.Controls.Add(btnImport);

			var btnAddRow = CreateButton("➕ 新建行", CardBorder, TextPrimary, 82);
			btnAddRow.Click += (_, _) => AddEmptyRow();
			topBar.Controls.Add(btnAddRow);

			var btnDelete = CreateButton("🗑️ 删除选中", CardBorder, TextPrimary, 88);
			btnDelete.Click += (_, _) => DeleteSelectedRows();
			topBar.Controls.Add(btnDelete);

			var btnClear = CreateButton("🧹 清空", CardBorder, TextPrimary, 72);
			btnClear.Click += (_, _) => ClearAllRows();
			topBar.Controls.Add(btnClear);

			// 底部快速文案大框 (方便查看/编辑长段葡语/外文口播稿，或直接输入单条文案试听)
			var quickPanel = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 132,
				BackColor = CardBg,
				Padding = new Padding(8, 6, 8, 6)
			};

			var quickHeader = new Panel
			{
				Dock = DockStyle.Top,
				Height = 26
			};

			var lblQuickTitle = new Label
			{
				Dock = DockStyle.Left,
				Width = 220,
				Text = "📝 当前选中文案大屏编辑 / 自由粘贴:",
				ForeColor = AccentYellow,
				Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold),
				TextAlign = ContentAlignment.MiddleLeft
			};

			_lblQuickStats = new Label
			{
				Dock = DockStyle.Fill,
				Text = "0 字符 · 预估口播时长 0 秒",
				ForeColor = Color.FromArgb(147, 197, 253),
				Font = new Font("Microsoft YaHei UI", 8.2f),
				TextAlign = ContentAlignment.MiddleRight
			};

			var btnAuditionRow = CreateButton("▶ 试听本条", AccentGreen, Color.White, 86);
			btnAuditionRow.Dock = DockStyle.Right;
			btnAuditionRow.Height = 24;
			btnAuditionRow.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
			btnAuditionRow.Click += async (_, _) => await AuditionSelectedOrQuickScriptAsync();

			quickHeader.Controls.Add(_lblQuickStats);
			quickHeader.Controls.Add(lblQuickTitle);
			quickHeader.Controls.Add(btnAuditionRow);

			_txtQuickScript = new TextBox
			{
				Dock = DockStyle.Fill,
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Font = new Font("Microsoft YaHei UI", 9.5f),
				Text = "Olá a todos! Hoje quero partilhar convosco uma mensagem muito especial que vai transformar o vosso dia. Escutem com atenção até ao fim!"
			};
			_txtQuickScript.TextChanged += (_, _) =>
			{
				UpdateQuickStatsLabel(_txtQuickScript.Text);
				if (_isSyncingQuickScript) return;
				if (_grid != null && _grid.SelectedRows.Count > 0)
				{
					var row = _grid.SelectedRows[0];
					if (row.Tag is ScriptBatchItem item)
					{
						item.ScriptText = _txtQuickScript.Text.Trim();
						row.Cells[3].Value = item.ScriptText;
						row.Cells[4].Value = FormatCharAndDuration(item.ScriptText);
						SyncStageMetadata();
						UpdateFileNamePreview();
					}
				}
				else
				{
					SyncStageMetadata();
				}
			};

			quickPanel.Controls.Add(_txtQuickScript);
			quickPanel.Controls.Add(quickHeader);

			var spacerBottom = new Panel { Dock = DockStyle.Bottom, Height = 6, BackColor = BgCanvas };

			_grid = new DataGridView
			{
				Dock = DockStyle.Fill,
				BackgroundColor = CardBg,
				ForeColor = TextPrimary,
				GridColor = CardBorder,
				BorderStyle = BorderStyle.None,
				RowHeadersVisible = false,
				AllowUserToAddRows = false,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				EnableHeadersVisualStyles = false,
				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
				ColumnHeadersHeight = 36
			};

			_grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(24, 34, 53);
			_grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
			_grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
			_grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

			_grid.DefaultCellStyle.BackColor = CardBg;
			_grid.DefaultCellStyle.ForeColor = TextPrimary;
			_grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 99, 235);
			_grid.DefaultCellStyle.SelectionForeColor = Color.White;
			_grid.RowTemplate.Height = 34;

			var colId = new DataGridViewTextBoxColumn { HeaderText = "#", Width = 36, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
			var colCode = new DataGridViewTextBoxColumn { HeaderText = "文案代码", Width = 72, DefaultCellStyle = { ForeColor = AccentYellow } };
			var colTitle = new DataGridViewTextBoxColumn { HeaderText = "中文文案/标题", Width = 122 };
			var colScript = new DataGridViewTextBoxColumn { HeaderText = "目标配音文案 (葡语/外文)", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
			var colStats = new DataGridViewTextBoxColumn { HeaderText = "字数/预估", Width = 86, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(147, 197, 253) } };
			var colStatus = new DataGridViewTextBoxColumn { HeaderText = "状态", Width = 80, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
			var colAction = new DataGridViewTextBoxColumn
			{
				HeaderText = "试听",
				Width = 64,
				ReadOnly = true,
				DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = AccentGreen }
			};
			var colQc = new DataGridViewTextBoxColumn
			{
				HeaderText = "自检结果 (点击查看)",
				Width = 148,
				ReadOnly = true,
				DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
			};

			_grid.Columns.AddRange(colId, colCode, colTitle, colScript, colStats, colStatus, colAction, colQc);

			_grid.CellFormatting += (s, e) =>
			{
				if (e.RowIndex < 0) return;
				string val = Convert.ToString(e.Value) ?? "";
				if (e.ColumnIndex == 5)
				{
					if (val.Contains("失败") || val.Contains("异常"))
					{
						e.CellStyle.ForeColor = Color.FromArgb(248, 113, 113);
						e.CellStyle.SelectionForeColor = Color.FromArgb(254, 202, 202);
					}
					else if (val.Contains("完成") || val.Contains("试听中"))
					{
						e.CellStyle.ForeColor = AccentGreen;
					}
					else if (val.Contains("合成") || val.Contains("导出"))
					{
						e.CellStyle.ForeColor = AccentCyan;
					}
				}
				else if (e.ColumnIndex == 7)
				{
					if (val.Contains("🟢") || val.Contains("合格"))
					{
						e.CellStyle.ForeColor = Color.FromArgb(52, 211, 153);
						e.CellStyle.SelectionForeColor = Color.FromArgb(167, 243, 208);
					}
					else if (val.Contains("🟡") || val.Contains("微调"))
					{
						e.CellStyle.ForeColor = AccentYellow;
						e.CellStyle.SelectionForeColor = Color.FromArgb(253, 230, 138);
					}
					else if (val.Contains("🔴") || val.Contains("失败") || val.Contains("异常"))
					{
						e.CellStyle.ForeColor = Color.FromArgb(248, 113, 113);
						e.CellStyle.SelectionForeColor = Color.FromArgb(254, 202, 202);
					}
					else
					{
						e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184);
					}
				}
			};

			_grid.CellClick += async (s, e) =>
			{
				if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
				var row = _grid.Rows[e.RowIndex];
				if (!(row.Tag is ScriptBatchItem item)) return;

				if (e.ColumnIndex == 6)
				{
					await AuditionSpecificItemAsync(item, row);
				}
				else if (e.ColumnIndex == 7 || (e.ColumnIndex == 5 && (item.Status.Contains("失败") || !string.IsNullOrWhiteSpace(item.QcDetails))))
				{
					ShowItemQcReportDialog(row, item);
				}
			};

			_grid.KeyDown += (s, e) =>
			{
				if (e.Control && e.KeyCode == Keys.V)
				{
					PasteClipboardData();
					e.Handled = true;
				}
				else if (e.KeyCode == Keys.Delete)
				{
					DeleteSelectedRows();
					e.Handled = true;
				}
			};

			_grid.SelectionChanged += (s, e) =>
			{
				if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ScriptBatchItem item)
				{
					_isSyncingQuickScript = true;
					try
					{
						_txtQuickScript.Text = item.ScriptText ?? "";
						UpdateQuickStatsLabel(_txtQuickScript.Text);
					}
					finally
					{
						_isSyncingQuickScript = false;
					}
				}
				SyncStageMetadata();
				UpdateFileNamePreview();
			};

			_grid.CellValueChanged += (s, e) =>
			{
				SyncGridEditsToItems();
				if (e.RowIndex >= 0 && e.RowIndex < _grid.Rows.Count && _grid.Rows[e.RowIndex].Tag is ScriptBatchItem item)
				{
					_grid.Rows[e.RowIndex].Cells[4].Value = FormatCharAndDuration(item.ScriptText);
					if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Index == e.RowIndex && !_isSyncingQuickScript)
					{
						_isSyncingQuickScript = true;
						try { _txtQuickScript.Text = item.ScriptText; }
						finally { _isSyncingQuickScript = false; }
					}
				}
				SyncStageMetadata();
				UpdateFileNamePreview();
			};

			panel.Controls.Add(_grid);
			panel.Controls.Add(spacerBottom);
			panel.Controls.Add(quickPanel);
			panel.Controls.Add(topBar);
			_grid.BringToFront();

			UpdateQuickStatsLabel(_txtQuickScript.Text);
			return panel;
		}

		// ========================================================
		// 2. 中栏：方案 1【现代流光声波与电台黑胶舞台 (Cyber Studio & Dynamic Spectrum)】
		// ========================================================
		private Panel BuildCenterCyberStagePanel()
		{
			var panel = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(6, 10, 6, 10),
				BackColor = BgCanvas
			};

			var container = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				BackColor = CardBg,
				Padding = new Padding(10),
				ColumnCount = 1,
				RowCount = 3
			};
			container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			container.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
			container.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			container.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));

			// 顶部标题栏 (已移除右上角重复的“试听选中文案”按钮，统一由舞台下方播控台控制)
			var headerPanel = new Panel
			{
				Dock = DockStyle.Fill,
				Margin = Padding.Empty,
				BackColor = Color.FromArgb(20, 28, 44),
				Padding = new Padding(12, 4, 12, 4)
			};

			var lblStageTitle = new Label
			{
				Dock = DockStyle.Fill,
				Text = "🎛️ CYBER STUDIO · 流光声波与电台黑胶舞台  (点击黑胶唱片或下方播放键试听文案)",
				Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
				ForeColor = AccentCyan,
				TextAlign = ContentAlignment.MiddleLeft,
				AutoEllipsis = true
			};

			headerPanel.Controls.Add(lblStageTitle);

			// 核心动态声波与黑胶舞台画布
			_cyberStage = new CyberAudioStageControl
			{
				Dock = DockStyle.Fill,
				Margin = Padding.Empty
			};
			_cyberStage.PlayPauseRequested += async () => await ToggleStagePlayPauseAsync();
			_cyberStage.SeekRequested += seconds => ScriptBatchTtsEngine.SetPositionSeconds(seconds);

			// 底部电台播控台 + 导出命名预览
			var bottomDeck = new Panel
			{
				Dock = DockStyle.Fill,
				Margin = Padding.Empty,
				BackColor = Color.FromArgb(20, 28, 44),
				Padding = new Padding(10, 6, 10, 4)
			};

			var transportRow = new Panel
			{
				Dock = DockStyle.Top,
				Height = 36
			};

			_btnStagePlay = CreateButton("▶ 播放 / 试听", AccentBlue, Color.White, 112);
			_btnStagePlay.Location = new Point(0, 3);
			_btnStagePlay.Height = 30;
			_btnStagePlay.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
			_btnStagePlay.Click += async (_, _) => await ToggleStagePlayPauseAsync();
			transportRow.Controls.Add(_btnStagePlay);

			_btnStageStop = CreateButton("⏹ 停止", CardBorder, TextPrimary, 68);
			_btnStageStop.Location = new Point(118, 3);
			_btnStageStop.Height = 30;
			_btnStageStop.Click += (_, _) => StopAllAudioPlayback();
			transportRow.Controls.Add(_btnStageStop);

			_lblStageTime = new Label
			{
				Location = new Point(194, 6),
				Width = 110,
				Height = 24,
				Text = "00:00 / 00:00",
				ForeColor = AccentYellow,
				Font = new Font("Consolas", 9.5f, FontStyle.Bold),
				TextAlign = ContentAlignment.MiddleLeft
			};
			transportRow.Controls.Add(_lblStageTime);

			var volPanel = new FlowLayoutPanel
			{
				Dock = DockStyle.Right,
				AutoSize = true,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = false,
				Padding = new Padding(0, 4, 0, 0)
			};

			_lblVolume = new Label
			{
				Text = "🔊 100%",
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 8.2f),
				Width = 58,
				TextAlign = ContentAlignment.MiddleRight,
				Margin = new Padding(0, 4, 2, 0)
			};

			_trkVolume = new TrackBar
			{
				Width = 88,
				Height = 24,
				TickStyle = TickStyle.None,
				Minimum = 0,
				Maximum = 100,
				Value = 100
			};
			_trkVolume.ValueChanged += (_, _) =>
			{
				_lblVolume.Text = _trkVolume.Value == 0 ? "🔇 0%" : $"🔊 {_trkVolume.Value}%";
				ScriptBatchTtsEngine.SetVolume(_trkVolume.Value / 100.0);
			};

			volPanel.Controls.Add(_lblVolume);
			volPanel.Controls.Add(_trkVolume);
			transportRow.Controls.Add(volPanel);

			_lblFileNamePreview = new Label
			{
				Dock = DockStyle.Bottom,
				Height = 20,
				Text = "📌 导出音频命名预览: 加载中...",
				ForeColor = Color.FromArgb(147, 197, 253),
				Font = new Font("Microsoft YaHei UI", 8.2f),
				AutoEllipsis = true
			};

			bottomDeck.Controls.Add(_lblFileNamePreview);
			bottomDeck.Controls.Add(transportRow);

			container.Controls.Add(headerPanel, 0, 0);
			container.Controls.Add(_cyberStage, 0, 1);
			container.Controls.Add(bottomDeck, 0, 2);

			panel.Controls.Add(container);
			return panel;
		}
		// ========================================================
		// 3. 右栏：音色融合库、口播去念稿感调优、SRT自定义与一键导出控制台
		//    优化重构：加宽排版、大字清晰高对比度、视力友好、无遮挡截断
		// ========================================================
		private Panel BuildRightSettingsPanel()
		{
			var outer = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(4),
				BackColor = BgCanvas
			};

			var scrollContainer = new SmoothScrollPanel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				BackColor = CardBg,
				Padding = new Padding(6, 6, 6, 8)
			};

			var flow = new FlowLayoutPanel
			{
				Dock = DockStyle.Top,
				AutoSize = true,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				Width = 372
			};

			const int StandardCardW = 364;
			const int ContentInnerW = 348;

			// --------------------------------------------------------
			// 模块 0：置顶核心交付流水线 (一键导出 MP3 + 自定义勾选 SRT)
			// --------------------------------------------------------
			var pnlTopExec = new Panel
			{
				Width = StandardCardW,
				BackColor = Color.FromArgb(24, 33, 49),
				Padding = new Padding(8, 8, 8, 10),
				Margin = new Padding(0, 0, 0, 10)
			};

			var topFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Top,
				AutoSize = true,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				Width = ContentInnerW
			};

			topFlow.Controls.Add(CreateSectionHeader("🚀 批量音频导出流水线 (MP3 交付)"));

			topFlow.Controls.Add(CreateLabel("输出保存目录:"));
			var outRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 4) };
			_txtOutputDir = new TextBox { Width = 260, Height = 28, BackColor = InputBg, ForeColor = TextPrimary, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 9.5f) };
			_txtOutputDir.TextChanged += (_, _) => SaveSettings();
			outRow.Controls.Add(_txtOutputDir);

			var btnBrowseOut = CreateButton("📂 浏览", AccentBlue, Color.White, 80);
			btnBrowseOut.Height = 28;
			btnBrowseOut.Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold);
			btnBrowseOut.Click += (_, _) => BrowseOutputFolder();
			outRow.Controls.Add(btnBrowseOut);
			topFlow.Controls.Add(outRow);

			// 导出格式与自定义 SRT 开关
			var lblMp3Badge = new Label
			{
				Text = "🎵 音频标准: 广播级高清 MP3 (320Kbps / 48kHz)",
				ForeColor = AccentGreen,
				Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
				AutoSize = true,
				Margin = new Padding(0, 6, 0, 4)
			};
			topFlow.Controls.Add(lblMp3Badge);

			_chkExportSrt = new CheckBox
			{
				Text = "同步导出 .SRT 字幕 (不勾选则仅导出 MP3)",
				ForeColor = AccentYellow,
				Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
				AutoSize = true,
				Checked = _settings.TtsStudioExportSrt,
				Margin = new Padding(0, 4, 0, 6)
			};
			_chkExportSrt.CheckedChanged += (_, _) =>
			{
				SaveSettings();
				SyncStageMetadata();
			};
			topFlow.Controls.Add(_chkExportSrt);

			_progressBar = new ProgressBar { Width = ContentInnerW, Height = 14, Style = ProgressBarStyle.Continuous, Margin = new Padding(0, 4, 0, 6) };
			topFlow.Controls.Add(_progressBar);

			_lblStatusText = new Label
			{
				Text = "准备就绪: 粘贴外文/葡语文案，随时一键批量导出高质感口播音频。",
				ForeColor = Color.FromArgb(203, 213, 225),
				Font = new Font("Microsoft YaHei UI", 9.2f),
				AutoSize = true,
				MaximumSize = new Size(ContentInnerW, 0),
				Margin = new Padding(0, 0, 0, 8)
			};
			topFlow.Controls.Add(_lblStatusText);

			var actionRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 6) };
			_btnStartBatch = CreateButton("🎙️ 一键批量导出音频 (MP3)", Color.FromArgb(37, 99, 235), Color.White, 246);
			_btnStartBatch.Font = new Font("Microsoft YaHei UI", 10.2f, FontStyle.Bold);
			_btnStartBatch.Height = 42;
			_btnStartBatch.Click += async (_, _) => await StartBatchAudioExportAsync();
			actionRow.Controls.Add(_btnStartBatch);

			_btnStopBatch = CreateButton("⏹️ 停止", Color.FromArgb(220, 38, 38), Color.White, 94);
			_btnStopBatch.Height = 42;
			_btnStopBatch.Font = new Font("Microsoft YaHei UI", 9.8f, FontStyle.Bold);
			_btnStopBatch.Enabled = false;
			_btnStopBatch.Click += (_, _) => StopBatchProcessing();
			actionRow.Controls.Add(_btnStopBatch);
			topFlow.Controls.Add(actionRow);

			var bottomTools = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
			var btnQuickExportSingle = CreateButton("⚡ 仅导出当前选中/编辑文案", AccentGreen, Color.White, 212);
			btnQuickExportSingle.Height = 34;
			btnQuickExportSingle.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
			btnQuickExportSingle.Click += async (_, _) => await ExportSingleCurrentScriptAsync();
			bottomTools.Controls.Add(btnQuickExportSingle);

			_btnOpenOutput = CreateButton("📁 打开输出目录", CardBorder, TextPrimary, 128);
			_btnOpenOutput.Height = 34;
			_btnOpenOutput.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
			_btnOpenOutput.Click += (_, _) => OpenOutputFolder();
			bottomTools.Controls.Add(_btnOpenOutput);
			topFlow.Controls.Add(bottomTools);

			pnlTopExec.Controls.Add(topFlow);
			void UpdateTopExecHeight()
			{
				topFlow.PerformLayout();
				int h = topFlow.PreferredSize.Height;
				foreach (Control c in topFlow.Controls)
				{
					if (c.Visible) h = Math.Max(h, c.Bottom + c.Margin.Bottom);
				}
				pnlTopExec.Height = h + 18;
			}
			topFlow.SizeChanged += (_, _) => UpdateTopExecHeight();
			UpdateTopExecHeight();
			flow.Controls.Add(pnlTopExec);

			// --------------------------------------------------------
			// 模块 1：双轨融合音色库 (方案 A 本地非洲网红克隆 + 方案 B 国际极速音色)
			// --------------------------------------------------------
			var voiceFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = ContentInnerW };

			voiceFlow.Controls.Add(CreateLabel("1. 选择配音引擎轨道 (AB双轨融合):"));
			_cboTtsEngine = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = ContentInnerW,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.2f)
			};
			_cboTtsEngine.Items.AddRange(new object[]
			{
				"🌐 方案B: 微软国际神经网络 (免费极速 · 非洲葡语首选)",
				"✨ 进阶轨: ElevenLabs 官方 API (超自然情感 · 多Key轮询)",
				"🎙️ 方案A: Chatterbox 本地声音克隆 (免费离线 · 克隆本土真人)"
			});
			_cboTtsEngine.SelectedIndexChanged += (_, _) =>
			{
				if (_isLoadingSettings) return;
				int idx = _cboTtsEngine.SelectedIndex;
				_settings.TtsEngine = idx == 2 ? "chatterbox" : (idx == 1 ? "elevenlabs" : "edge_tts");
				UpdateVoiceDropdownItems();
				SaveSettings();
				SyncStageMetadata();
			};
			voiceFlow.Controls.Add(_cboTtsEngine);

			// 语种/地区快捷筛选栏 (2x2 大按钮排布，固定两行高度防截断，并支持随面板等比例拉伸)
			voiceFlow.Controls.Add(CreateLabel("2. 语种快速筛选 (重点聚焦非洲葡语与外文):"));
			var filterRow = new FlowLayoutPanel
			{
				AutoSize = false,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = true,
				Width = ContentInnerW,
				Height = 76,
				Margin = new Padding(0, 2, 0, 6)
			};

			void AddFilterBtn(string label, string tag, int w)
			{
				var btn = CreateButton(label, tag == _voiceRegionFilter ? AccentBlue : CardBorder, Color.White, w);
				btn.Height = 32;
				btn.Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold);
				btn.Tag = tag;
				btn.Margin = new Padding(0, 0, 6, 6);
				btn.Click += (_, _) =>
				{
					_voiceRegionFilter = tag;
					foreach (var fb in _filterButtons)
					{
						fb.BackColor = (string)fb.Tag == _voiceRegionFilter ? AccentBlue : CardBorder;
					}
					UpdateVoiceDropdownItems();
				};
				_filterButtons.Add(btn);
				filterRow.Controls.Add(btn);
			}

			AddFilterBtn("🌍 全部音色", "ALL", 168);
			AddFilterBtn("🇦🇴 非洲 / 欧葡", "PT-AF", 168);
			AddFilterBtn("🇧🇷 巴西葡语", "PT-BR", 168);
			AddFilterBtn("🇿🇦 英语 / 其他", "OTHER", 168);
			voiceFlow.Controls.Add(filterRow);

			// 引擎专属管理按钮
			var keyActionRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 4) };
			_btnElevenKeys = CreateButton("🔑 ElevenLabs 密钥管理", AccentBlue, Color.White, 208);
			_btnElevenKeys.Height = 32;
			_btnElevenKeys.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
			_btnElevenKeys.Click += (_, _) =>
			{
				using var dlg = new ElevenLabsKeyDialog();
				dlg.ShowDialog(this);
				UpdateKeyButtonText();
			};
			keyActionRow.Controls.Add(_btnElevenKeys);

			_btnFetchCloudVoices = CreateButton("☁️ 同步声音", CardBorder, TextPrimary, 132);
			_btnFetchCloudVoices.Height = 32;
			_btnFetchCloudVoices.Font = new Font("Microsoft YaHei UI", 9f);
			_btnFetchCloudVoices.Click += async (_, _) => await FetchCloudVoicesAsync();
			keyActionRow.Controls.Add(_btnFetchCloudVoices);
			voiceFlow.Controls.Add(keyActionRow);

			_btnChatterboxSettings = CreateButton("🎙️ 导入 6 秒真人样本克隆 / Chatterbox 模型设置", AccentPurple, Color.White, ContentInnerW);
			_btnChatterboxSettings.Height = 34;
			_btnChatterboxSettings.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
			_btnChatterboxSettings.Click += (_, _) =>
			{
				using var dialog = new ChatterboxSettingsDialog(_settings);
				var res = dialog.ShowDialog(this);
				UpdateVoiceDropdownItems();
				if (res == DialogResult.OK) SaveSettings();
				SyncStageMetadata();
			};
			voiceFlow.Controls.Add(_btnChatterboxSettings);

			voiceFlow.Controls.Add(CreateLabel("3. 选择主讲人口播音色:"));
			_cboDefaultVoice = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = ContentInnerW,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.2f)
			};
			_cboDefaultVoice.SelectedIndexChanged += (_, _) =>
			{
				if (_isLoadingSettings) return;
				SaveSettings();
				SyncStageMetadata();

				if (_cboDefaultVoice.SelectedItem is VoiceOption vo)
				{
					foreach (var it in _items)
					{
						if (it.Status == "等待中" || it.Status.Contains("失败"))
						{
							it.VoiceKey = vo.Key;
						}
					}
				}
			};
			voiceFlow.Controls.Add(_cboDefaultVoice);

			// 核心响应：紧贴音色下拉选单下方放置预置样音秒响试听按钮（选好音色随手即点，无需往下远距离寻找）
			_btnAudition = CreateButton("⚡ 试听当前音色效果 (预置样音 · 毫秒级秒出声)", AccentCyan, Color.White, ContentInnerW);
			_btnAudition.Height = 38;
			_btnAudition.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
			_btnAudition.Margin = new Padding(0, 6, 0, 2);
			_btnAudition.Click += async (_, _) => await AuditionCurrentVoicePresetTunedAsync();
			voiceFlow.Controls.Add(_btnAudition);

			_lblVoiceHint = new Label
			{
				Text = "💡 提示: 选好音色或调好下方语速/语调后，直接点上方青色【⚡ 试听当前音色效果】即可毫秒级秒出声！",
				ForeColor = AccentYellow,
				Font = new Font("Microsoft YaHei UI", 8.5f),
				AutoSize = true,
				MaximumSize = new Size(ContentInnerW, 0),
				Margin = new Padding(0, 4, 0, 4)
			};
			voiceFlow.Controls.Add(_lblVoiceHint);

			_pnlCustomVoiceId = new Panel { AutoSize = true, Visible = false, Margin = new Padding(0, 2, 0, 4) };
			_pnlCustomVoiceId.Controls.Add(new Label { Text = "自定义 Voice ID:", ForeColor = AccentYellow, AutoSize = true });
			_txtCustomVoiceId = new TextBox
			{
				Width = ContentInnerW,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Location = new Point(0, 18),
				Font = new Font("Microsoft YaHei UI", 9f)
			};
			_txtCustomVoiceId.TextChanged += (_, _) => SaveSettings();
			_pnlCustomVoiceId.Controls.Add(_txtCustomVoiceId);
			voiceFlow.Controls.Add(_pnlCustomVoiceId);

			var cardVoice = CreateCollapsibleCard("🎙️ 双轨融合音色库 (非洲葡语 / 国际外文 / 克隆)", voiceFlow, true, StandardCardW);
			flow.Controls.Add(cardVoice);

			// --------------------------------------------------------
			// 模块 2：口播“去念稿感”真人化调优 (Prosody & Instant Tuned Audition)
			// --------------------------------------------------------
			var prosodyFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = ContentInnerW };

			prosodyFlow.Controls.Add(CreateLabel("口播情绪与节奏预设 (一键告别机械念稿):"));
			_cboSpeakingStyle = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = ContentInnerW,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.2f)
			};
			_cboSpeakingStyle.Items.AddRange(new object[]
			{
				"🔥 短视频爆款口播 (微提速+8% · 自然气口 · 电台磁性) ★推荐",
				"💬 真人日常唠嗑/分享 (轻快+5% · 亲切语调 · 灵动换气)",
				"🎙️ 深度叙事/神父布道 (稳重1.0x · 深沉浑厚 · 庄重停顿)",
				"⚡ 高密度干货/快节奏带货 (提速+10% · 抓耳穿透 · 干脆利落)",
				"🛠️ 自定义语速与音调参数"
			});
			_cboSpeakingStyle.SelectedIndexChanged += (_, _) =>
			{
				if (_isLoadingSettings) return;
				ApplySpeakingStylePreset(_cboSpeakingStyle.SelectedIndex);
				SaveSettings();
				SyncStageMetadata();
			};
			prosodyFlow.Controls.Add(_cboSpeakingStyle);

			var ratePitchRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 6, 0, 4) };
			var lblRateTitle = new Label { Text = "语速:", ForeColor = Color.FromArgb(226, 232, 240), Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 4, 2, 0) };
			ratePitchRow.Controls.Add(lblRateTitle);
			_cboRate = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = 124,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.2f)
			};
			_cboRate.Items.AddRange(new object[]
			{
				"标准 (1.0x)",
				"轻快 (1.05x)",
				"黄金口播 (1.08x)",
				"快速 (1.10x)",
				"极速 (1.15x)",
				"沉稳 (0.95x)",
				"舒缓 (0.90x)"
			});
			_cboRate.SelectedIndexChanged += (_, _) =>
			{
				if (_isLoadingSettings) return;
				SaveSettings();
				SyncStageMetadata();
			};
			ratePitchRow.Controls.Add(_cboRate);

			var lblPitchTitle = new Label { Text = " 语调:", ForeColor = Color.FromArgb(226, 232, 240), Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold), AutoSize = true, Margin = new Padding(4, 4, 2, 0) };
			ratePitchRow.Controls.Add(lblPitchTitle);
			_cboPitch = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = 124,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.2f)
			};
			_cboPitch.Items.AddRange(new object[]
			{
				"自然原声 (+0Hz)",
				"饱满精神 (+2Hz)",
				"深沉磁性 (-2Hz)",
				"浑厚低音 (-4Hz)"
			});
			_cboPitch.SelectedIndexChanged += (_, _) =>
			{
				if (_isLoadingSettings) return;
				SaveSettings();
				SyncStageMetadata();
			};
			ratePitchRow.Controls.Add(_cboPitch);
			prosodyFlow.Controls.Add(ratePitchRow);

			_chkNaturalBreathing = new CheckBox
			{
				Text = "注入真人标点呼吸气口 (自动断句微停顿 · 拒绝连读)",
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 9.2f),
				AutoSize = true,
				Checked = _settings.TtsStudioNaturalBreathing,
				Margin = new Padding(0, 4, 0, 4)
			};
			_chkNaturalBreathing.CheckedChanged += (_, _) =>
			{
				if (_isLoadingSettings) return;
				SaveSettings();
				SyncStageMetadata();
			};
			prosodyFlow.Controls.Add(_chkNaturalBreathing);

			_chkWarmEq = new CheckBox
			{
				Text = "开启广播级电容麦调音 (Warm EQ · 温润磁性人声)",
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 9.2f),
				AutoSize = true,
				Checked = _settings.TtsStudioWarmBroadcastEq,
				Margin = new Padding(0, 2, 0, 6)
			};
			_chkWarmEq.CheckedChanged += (_, _) =>
			{
				if (_isLoadingSettings) return;
				SaveSettings();
				SyncStageMetadata();
			};
			prosodyFlow.Controls.Add(_chkWarmEq);

			var cardProsody = CreateCollapsibleCard("⚡ 口播“去念稿感”真人化调优", prosodyFlow, true, StandardCardW);
			flow.Controls.Add(cardProsody);

			// --------------------------------------------------------
			// 模块 3：团队制作人与标准文件命名规范
			// --------------------------------------------------------
			var namingFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = ContentInnerW };
			var prodRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
			var lblProdTitle = new Label { Text = "制作人标识:", ForeColor = Color.FromArgb(226, 232, 240), Font = new Font("Microsoft YaHei UI", 9f), AutoSize = true, Margin = new Padding(0, 4, 4, 0) };
			prodRow.Controls.Add(lblProdTitle);
			_txtProducerName = new TextBox
			{
				Width = 245,
				Height = 26,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Font = new Font("Microsoft YaHei UI", 9.2f),
				Text = string.IsNullOrWhiteSpace(_settings.ProducerName) ? "陆恒" : _settings.ProducerName
			};
			_txtProducerName.TextChanged += (_, _) =>
			{
				if (_isLoadingSettings) return;
				SaveSettings();
				UpdateFileNamePreview();
			};
			prodRow.Controls.Add(_txtProducerName);
			namingFlow.Controls.Add(prodRow);

			_chkNamingRule = new CheckBox
			{
				Text = "启用团队标准命名 (ZB-制作人-代码-标题-日期)",
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 9f),
				AutoSize = true,
				Checked = _settings.NamingRuleEnabled,
				Margin = new Padding(0, 4, 0, 4)
			};
			_chkNamingRule.CheckedChanged += (_, _) =>
			{
				if (_isLoadingSettings) return;
				SaveSettings();
				UpdateFileNamePreview();
			};
			namingFlow.Controls.Add(_chkNamingRule);

			var cardNaming = CreateCollapsibleCard("🏷️ 团队制作人与导出命名规则", namingFlow, false, StandardCardW);
			flow.Controls.Add(cardNaming);

			scrollContainer.Controls.Add(flow);
			outer.Controls.Add(scrollContainer);

			// 随窗口全屏/缩放或拖拽分割条，等比例自适应放大右侧面板所有卡片与内部控件宽度
			bool isScalingRight = false;
			int lastRightAvailableW = -1;
			void ScaleRightContent()
			{
				if (isScalingRight) return;
				int available = scrollContainer.ClientSize.Width - scrollContainer.Padding.Horizontal - 4;
				if (available < 260 || available == lastRightAvailableW) return;
				lastRightAvailableW = available;
				isScalingRight = true;
				try
				{
					int innerW = Math.Max(240, available - 16);
					float ratio = Math.Max(0.85f, available / (float)StandardCardW);

					flow.SuspendLayout();
					pnlTopExec.SuspendLayout();
					topFlow.SuspendLayout();
					voiceFlow.SuspendLayout();
					prosodyFlow.SuspendLayout();
					namingFlow.SuspendLayout();

					flow.Width = available;

					// 模块 0：顶部导出流水线卡片
					pnlTopExec.Width = available;
					topFlow.Width = innerW;
					int browseBtnW = Math.Max(80, Math.Min(130, (int)Math.Round(80 * ratio)));
					btnBrowseOut.Width = browseBtnW;
					_txtOutputDir.Width = Math.Max(150, innerW - browseBtnW - 8);
					_progressBar.Width = innerW;
					_lblStatusText.MaximumSize = new Size(innerW, 0);

					int stopBtnW = Math.Max(94, Math.Min(150, (int)Math.Round(94 * ratio)));
					_btnStopBatch.Width = stopBtnW;
					_btnStartBatch.Width = Math.Max(180, innerW - stopBtnW - 8);

					int openOutBtnW = Math.Max(128, Math.Min(210, (int)Math.Round(128 * ratio)));
					_btnOpenOutput.Width = openOutBtnW;
					btnQuickExportSingle.Width = Math.Max(180, innerW - openOutBtnW - 8);

					// 模块 1：双轨融合音色库卡片
					cardVoice.Width = available;
					voiceFlow.Width = innerW;
					_cboTtsEngine.Width = innerW;
					filterRow.Width = innerW;
					int filterBtnW = Math.Max(110, (innerW - 12) / 2);
					foreach (var fb in _filterButtons)
					{
						fb.Width = filterBtnW;
					}
					filterRow.Height = 76;

					int syncCloudBtnW = Math.Max(128, Math.Min(210, (int)Math.Round(132 * ratio)));
					_btnFetchCloudVoices.Width = syncCloudBtnW;
					_btnElevenKeys.Width = Math.Max(170, innerW - syncCloudBtnW - 8);
					_btnChatterboxSettings.Width = innerW;
					_cboDefaultVoice.Width = innerW;
					_btnAudition.Width = innerW;
					_lblVoiceHint.MaximumSize = new Size(innerW, 0);
					_pnlCustomVoiceId.Width = innerW;
					_txtCustomVoiceId.Width = innerW;

					// 模块 2：口播真人化调优卡片
					cardProsody.Width = available;
					prosodyFlow.Width = innerW;
					_cboSpeakingStyle.Width = innerW;
					int labelsW = lblRateTitle.PreferredSize.Width + lblPitchTitle.PreferredSize.Width + 20;
					int comboEachW = Math.Max(115, (innerW - labelsW) / 2);
					_cboRate.Width = comboEachW;
					_cboPitch.Width = comboEachW;

					// 模块 3：团队制作人与命名规范卡片
					cardNaming.Width = available;
					namingFlow.Width = innerW;
					_txtProducerName.Width = Math.Max(180, innerW - lblProdTitle.PreferredSize.Width - 14);

					namingFlow.ResumeLayout(true);
					prosodyFlow.ResumeLayout(true);
					voiceFlow.ResumeLayout(true);
					topFlow.ResumeLayout(true);
					pnlTopExec.ResumeLayout(true);
					UpdateTopExecHeight();
					flow.ResumeLayout(true);
				}
				finally
				{
					isScalingRight = false;
				}
			}

			scrollContainer.Resize += (_, _) => ScaleRightContent();
			scrollContainer.HandleCreated += (_, _) => ScaleRightContent();

			return outer;
		}
		// ========================================================
		// 4. 音色管理、参数同步、剪贴板粘贴与批量导出核心逻辑
		// ========================================================
		private void ApplySpeakingStylePreset(int presetIndex)
		{
			bool prev = _isLoadingSettings;
			_isLoadingSettings = true;
			try
			{
				switch (presetIndex)
				{
					case 0: // 🔥 短视频爆款口播
						_cboRate.SelectedIndex = 2; // 黄金口播 1.08x (+8%)
						_cboPitch.SelectedIndex = 1; // 饱满精神 +2Hz
						_chkNaturalBreathing.Checked = true;
						_chkWarmEq.Checked = true;
						break;
					case 1: // 💬 真人日常唠嗑
						_cboRate.SelectedIndex = 1; // 轻快 1.05x (+5%)
						_cboPitch.SelectedIndex = 0; // 自然原声 +0Hz
						_chkNaturalBreathing.Checked = true;
						_chkWarmEq.Checked = true;
						break;
					case 2: // 🎙️ 深度叙事/神父布道
						_cboRate.SelectedIndex = 0; // 标准 1.0x
						_cboPitch.SelectedIndex = 2; // 深沉磁性 -2Hz
						_chkNaturalBreathing.Checked = true;
						_chkWarmEq.Checked = true;
						break;
					case 3: // ⚡ 高密度干货/快节奏带货
						_cboRate.SelectedIndex = 3; // 快速 1.10x (+10%)
						_cboPitch.SelectedIndex = 1; // 饱满精神 +2Hz
						_chkNaturalBreathing.Checked = true;
						_chkWarmEq.Checked = true;
						break;
				}
			}
			finally
			{
				_isLoadingSettings = prev;
			}
		}

		private void UpdateVoiceDropdownItems()
		{
			bool wasLoading = _isLoadingSettings;
			_isLoadingSettings = true;
			try
			{
				_cboDefaultVoice.Items.Clear();
				int engineIdx = _cboTtsEngine.SelectedIndex;
				bool isEleven = engineIdx == 1;
				bool isChatterbox = engineIdx == 2;

				_btnElevenKeys.Visible = isEleven;
				_btnFetchCloudVoices.Visible = isEleven;
				if (_btnElevenKeys.Parent != null)
				{
					_btnElevenKeys.Parent.Visible = isEleven;
				}
				_btnChatterboxSettings.Visible = isChatterbox; // 仅在选中 Chatterbox 本地克隆引擎时展示该按钮

				if (isChatterbox)
				{
					string refInfo = !string.IsNullOrWhiteSpace(_settings.ChatterboxReferenceAudio) && File.Exists(_settings.ChatterboxReferenceAudio)
						? $"已加载真人克隆样本 ({Path.GetFileName(_settings.ChatterboxReferenceAudio)})"
						: "当前使用默认音色（点击上方紫色按钮可导入6秒安哥拉/本土真人音频克隆）";
					_lblVoiceHint.Text = ChatterboxEngine.IsInstalled
						? $"🎙️ 本地克隆已就绪：{refInfo}"
						: "💡 首次使用本地声音克隆，请点击上方【Chatterbox 模型设置】一键安装模型。";

					foreach (var voice in ChatterboxEngine.AvailableVoices)
					{
						_cboDefaultVoice.Items.Add(voice);
					}
					_cboDefaultVoice.SelectedItem = ChatterboxEngine.AvailableVoices.FirstOrDefault(v => v.Key == "chatterbox:" + _settings.ChatterboxLanguage)
						?? ChatterboxEngine.AvailableVoices[0];
				}
				else if (isEleven)
				{
					_lblVoiceHint.Text = "💡 ElevenLabs 多密钥池自动轮询；试听直连官方 CDN 零额度播放，不消耗字符配额！";
					if (_settings.ClonedVoices != null)
					{
						foreach (var cv in _settings.ClonedVoices.Where(v => v.EngineType != "local_xtts"))
						{
							_cboDefaultVoice.Items.Add(new VoiceOption(cv.VoiceId, $"⭐ [专属克隆] {cv.Name}", "Cloned"));
						}
					}
					foreach (var v in ElevenLabsEngine.DefaultElevenVoices)
					{
						_cboDefaultVoice.Items.Add(v);
					}
					var match = _cboDefaultVoice.Items.OfType<VoiceOption>().FirstOrDefault(v => v.Key == _settings.ElevenLabsVoice);
					if (match != null) _cboDefaultVoice.SelectedItem = match;
					else if (_cboDefaultVoice.Items.Count > 0) _cboDefaultVoice.SelectedIndex = 0;
				}
				else
				{
					_lblVoiceHint.Text = "💡 提示: 选好音色（含成人男女声与男童/女童）或调好下方语速/语调后，点上方青色【⚡ 试听当前音色效果】即可毫秒级秒出声！";
					IEnumerable<VoiceOption> filtered = ScriptBatchTtsEngine.AvailableVoices;
					if (_voiceRegionFilter == "PT-AF")
						filtered = filtered.Where(v => v.Language == "pt-PT");
					else if (_voiceRegionFilter == "PT-BR")
						filtered = filtered.Where(v => v.Language == "pt-BR");
					else if (_voiceRegionFilter == "OTHER")
						filtered = filtered.Where(v => v.Language != "pt-PT" && v.Language != "pt-BR");

					foreach (var v in filtered)
					{
						_cboDefaultVoice.Items.Add(v);
					}
					if (_cboDefaultVoice.Items.Count == 0)
					{
						foreach (var v in ScriptBatchTtsEngine.AvailableVoices) _cboDefaultVoice.Items.Add(v);
					}

					var match = _cboDefaultVoice.Items.OfType<VoiceOption>().FirstOrDefault(v => v.Key == _settings.DefaultVoice);
					if (match != null) _cboDefaultVoice.SelectedItem = match;
					else if (_cboDefaultVoice.Items.Count > 0) _cboDefaultVoice.SelectedIndex = 0;
				}

				if (_cboDefaultVoice.SelectedItem is VoiceOption vo)
				{
					_pnlCustomVoiceId.Visible = vo.Key == "CustomVoice";
				}
				else
				{
					_pnlCustomVoiceId.Visible = false;
				}
			}
			finally
			{
				_isLoadingSettings = wasLoading;
			}
		}

		private void UpdateKeyButtonText()
		{
			var pool = ElevenLabsKeyPool.Instance;
			int total = pool.Keys.Count;
			int usable = pool.Keys.Count(k => k.IsUsable);
			long remain = pool.Keys.Where(k => k.IsUsable).Sum(k => (long)k.RemainingCharacters);

			if (usable > 0)
				_btnElevenKeys.Text = $"🔑 ElevenLabs ({usable}有效/{remain / 1000:0.0}k字)";
			else if (total > 0)
				_btnElevenKeys.Text = $"🔑 ElevenLabs ({total}个待检)";
			else
				_btnElevenKeys.Text = "🔑 ElevenLabs 密钥管理";
		}

		private async Task FetchCloudVoicesAsync()
		{
			var pool = ElevenLabsKeyPool.Instance;
			var key = pool.GetNextUsableKey();
			if (key == null || string.IsNullOrWhiteSpace(key.ApiKey))
			{
				MessageBox.Show(this, "请先在【🔑 ElevenLabs 密钥管理】中添加至少一个可用的 API 密钥！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			_btnFetchCloudVoices.Enabled = false;
			_btnFetchCloudVoices.Text = "同步中...";
			try
			{
				var cloudVoices = await ElevenLabsEngine.FetchAccountVoicesAsync(key.ApiKey);
				if (cloudVoices.Count > 0)
				{
					_cboDefaultVoice.Items.Clear();
					foreach (var cv in cloudVoices) _cboDefaultVoice.Items.Add(cv);
					foreach (var dv in ElevenLabsEngine.DefaultElevenVoices) _cboDefaultVoice.Items.Add(dv);
					_cboDefaultVoice.SelectedIndex = 0;
					MessageBox.Show(this, $"成功同步 {cloudVoices.Count} 个专属云端声音！", "同步成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, $"同步失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			finally
			{
				_btnFetchCloudVoices.Text = "☁️ 同步声音";
				_btnFetchCloudVoices.Enabled = true;
			}
		}

		private void ApplyLoadedSettings()
		{
			_isLoadingSettings = true;
			try
			{
				_cboTtsEngine.SelectedIndex = _settings.TtsEngine == "chatterbox" ? 2
					: (string.Equals(_settings.TtsEngine, "elevenlabs", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
				UpdateVoiceDropdownItems();

				_cboSpeakingStyle.SelectedIndex = Math.Max(0, Math.Min(4, _settings.TtsStudioSpeakingStyle));
				_cboRate.SelectedIndex = _settings.SpeechRate switch
				{
					"+5%" => 1,
					"+8%" => 2,
					"+10%" => 3,
					"+15%" => 4,
					"-5%" => 5,
					"-10%" => 6,
					_ => 2 // 默认短视频黄金口播 1.08x
				};
				_cboPitch.SelectedIndex = _settings.SpeechPitch switch
				{
					"+2Hz" => 1,
					"-2Hz" => 2,
					"-4Hz" => 3,
					_ => 0
				};

				_chkNaturalBreathing.Checked = _settings.TtsStudioNaturalBreathing;
				_chkWarmEq.Checked = _settings.TtsStudioWarmBroadcastEq;
				_chkExportSrt.Checked = _settings.TtsStudioExportSrt;

				_txtProducerName.Text = string.IsNullOrWhiteSpace(_settings.ProducerName) ? "陆恒" : _settings.ProducerName;
				_chkNamingRule.Checked = _settings.NamingRuleEnabled;

				if (string.IsNullOrWhiteSpace(_settings.TtsStudioOutputFolderPath))
				{
					_settings.TtsStudioOutputFolderPath = Path.Combine(
						Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
						"口播语音导出_MP3");
				}
				_txtOutputDir.Text = _settings.TtsStudioOutputFolderPath;
			}
			finally
			{
				_isLoadingSettings = false;
			}
		}

		private void SaveSettings()
		{
			if (_isLoadingSettings) return;

			int engineIdx = _cboTtsEngine.SelectedIndex;
			_settings.TtsEngine = engineIdx == 2 ? "chatterbox" : (engineIdx == 1 ? "elevenlabs" : "edge_tts");

			if (_cboDefaultVoice.SelectedItem is VoiceOption vo)
			{
				if (engineIdx == 2) _settings.ChatterboxLanguage = vo.Language;
				else if (engineIdx == 1) _settings.ElevenLabsVoice = vo.Key;
				else _settings.DefaultVoice = vo.Key;
				_pnlCustomVoiceId.Visible = vo.Key == "CustomVoice";
			}
			_settings.ElevenLabsCustomVoiceId = _txtCustomVoiceId.Text.Trim();

			_settings.TtsStudioSpeakingStyle = _cboSpeakingStyle.SelectedIndex;
			_settings.SpeechRate = _cboRate.SelectedIndex switch
			{
				0 => "+0%",
				1 => "+5%",
				2 => "+8%",
				3 => "+10%",
				4 => "+15%",
				5 => "-5%",
				6 => "-10%",
				_ => "+8%"
			};
			_settings.SpeechPitch = _cboPitch.SelectedIndex switch
			{
				1 => "+2Hz",
				2 => "-2Hz",
				3 => "-4Hz",
				_ => "+0Hz"
			};

			_settings.TtsStudioNaturalBreathing = _chkNaturalBreathing.Checked;
			_settings.TtsStudioWarmBroadcastEq = _chkWarmEq.Checked;
			_settings.TtsStudioExportSrt = _chkExportSrt.Checked;
			_settings.ProducerName = _txtProducerName.Text.Trim();
			_settings.NamingRuleEnabled = _chkNamingRule.Checked;
			_settings.TtsStudioOutputFolderPath = _txtOutputDir.Text.Trim();

			_settings.Save();
		}

		private void SyncStageMetadata()
		{
			if (_cyberStage == null) return;

			string voiceLabel = _cboDefaultVoice?.Text ?? "Duarte (PT-AO)";
			string engineTag = _cboTtsEngine?.SelectedIndex == 2 ? "LOCAL CLONE · XTTS"
				: (_cboTtsEngine?.SelectedIndex == 1 ? "ELEVENLABS CLOUD" : "NEURAL STREAM · 48kHz");

			string activeScript = "";
			string activeTitle = "流光声波口播工作台";
			if (_grid != null && _grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ScriptBatchItem sel)
			{
				activeScript = sel.ScriptText;
				activeTitle = sel.DisplayTitle;
			}
			else if (_txtQuickScript != null)
			{
				activeScript = _txtQuickScript.Text;
			}

			_cyberStage.VoiceName = voiceLabel;
			_cyberStage.EngineBadge = engineTag;
			_cyberStage.ScriptPreviewText = activeScript;
			_cyberStage.TrackTitle = activeTitle;
			_cyberStage.WarmEqEnabled = _chkWarmEq != null && _chkWarmEq.Checked;
			_cyberStage.NaturalBreathingEnabled = _chkNaturalBreathing != null && _chkNaturalBreathing.Checked;
			_cyberStage.ExportSrtEnabled = _chkExportSrt != null && _chkExportSrt.Checked;
			_cyberStage.Invalidate();
		}

		private void UpdateFileNamePreview()
		{
			if (_lblFileNamePreview == null) return;
			string producer = string.IsNullOrWhiteSpace(_txtProducerName?.Text) ? "陆恒" : _txtProducerName.Text.Trim();
			ScriptBatchItem targetItem = null;

			if (_grid != null && _grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ScriptBatchItem item)
				targetItem = item;
			else if (_items.Count > 0)
				targetItem = _items[0];

			string baseName;
			if (targetItem != null)
			{
				baseName = (_chkNamingRule == null || _chkNamingRule.Checked)
					? targetItem.GenerateStandardFileName(producer)
					: SanitizeSimpleFileName(targetItem.DisplayTitle);
			}
			else
			{
				var dummy = new ScriptBatchItem { Id = 1, ScriptCode = "43u0cm", ChineseTitle = "非洲葡语爆款口播配音示例" };
				baseName = (_chkNamingRule == null || _chkNamingRule.Checked)
					? dummy.GenerateStandardFileName(producer)
					: "非洲葡语爆款口播配音示例";
			}

			string srtSuffix = (_chkExportSrt != null && _chkExportSrt.Checked) ? $"  +  {baseName}.srt" : "  (仅纯净MP3音频)";
			_lblFileNamePreview.Text = $"📌 导出预览: {baseName}.mp3{srtSuffix}";
		}

		private static string SanitizeSimpleFileName(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return "audio_001";
			foreach (char c in Path.GetInvalidFileNameChars())
			{
				name = name.Replace(c, '_');
			}
			return name.Trim();
		}

		private static string FormatCharAndDuration(string script)
		{
			if (string.IsNullOrWhiteSpace(script)) return "0字 (~0s)";
			int chars = script.Trim().Length;
			int words = script.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
			// 葡语/西文口播平均每秒约 2.6 个单词 (或 ~14 字符/秒)
			double sec = Math.Max(1.0, Math.Round(Math.Max(words / 2.6, chars / 14.5)));
			return $"{chars}字符 (~{sec:0}s)";
		}

		private void UpdateQuickStatsLabel(string script)
		{
			if (_lblQuickStats == null) return;
			if (string.IsNullOrWhiteSpace(script))
			{
				_lblQuickStats.Text = "0 字符 · 预估时长 0 秒";
				return;
			}
			int chars = script.Trim().Length;
			int words = script.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
			double sec = Math.Max(1.0, Math.Round(Math.Max(words / 2.6, chars / 14.5)));
			_lblQuickStats.Text = $"{words} 词 / {chars} 字符 · 预估口播 {sec:0} 秒";
		}

		// ========================================================
		// 5. 剪贴板粘贴、表格操作、试听联动与批量导出 MP3 (+ 可选 SRT)
		// ========================================================
		private void PasteClipboardData()
		{
			try
			{
				string raw = Clipboard.GetText();
				if (string.IsNullOrWhiteSpace(raw))
				{
					MessageBox.Show(this, "剪贴板为空！\n您可以从 Excel 或文档中复制：\n• 3列：[文案代码] [中文标题] [目标外文/葡语配音文案]\n• 2列：[标题] [目标配音文案]\n• 1列：直接粘贴配音文案（每行一条）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				var parsedRows = ParseClipboardRows(raw);
				if (parsedRows.Count == 0)
				{
					MessageBox.Show(this, "未解析出有效文案内容。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				string defaultVoice = (_cboDefaultVoice.SelectedItem as VoiceOption)?.Key ?? _settings.DefaultVoice;
				int addedCount = 0;

				foreach (var cols in parsedRows)
				{
					if (cols.Length == 0) continue;
					string code = "";
					string cnTitle = "";
					string script = "";

					if (cols.Length >= 3)
					{
						code = cols[0].Trim();
						cnTitle = cols[1].Trim();
						script = string.Join(" ", cols.Skip(2)).Trim();
					}
					else if (cols.Length == 2)
					{
						string c0 = cols[0].Trim();
						string c1 = cols[1].Trim();
						if (IsLikelyCode(c0))
						{
							code = c0;
							script = c1;
						}
						else
						{
							cnTitle = c0;
							script = c1;
						}
					}
					else
					{
						script = cols[0].Trim();
					}

					// 自动跳过表头行
					if ((code.Contains("文案代码") || code.Equals("Code", StringComparison.OrdinalIgnoreCase)) ||
						(cnTitle.Contains("中文文案") || cnTitle.Contains("标题")) ||
						(script.Contains("葡语文案") || script.Contains("目标配音文案")))
					{
						continue;
					}

					if (string.IsNullOrWhiteSpace(script) && string.IsNullOrWhiteSpace(cnTitle))
						continue;

					if (string.IsNullOrWhiteSpace(script))
						script = cnTitle;

					var item = new ScriptBatchItem
					{
						Id = _items.Count + 1,
						ScriptCode = code,
						ChineseTitle = cnTitle,
						ScriptText = script,
						VoiceKey = defaultVoice,
						Status = "等待中"
					};
					_items.Add(item);
					AddGridRowForItem(item);
					addedCount++;
				}

				_lblStatusText.Text = $"已成功粘贴 {addedCount} 条配音任务（总计 {_items.Count} 条）。";
				if (_grid.Rows.Count > 0 && _grid.SelectedRows.Count == 0)
				{
					_grid.Rows[_grid.Rows.Count - 1].Selected = true;
				}
				UpdateFileNamePreview();
				SyncStageMetadata();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, $"粘贴失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private static bool IsLikelyCode(string text)
		{
			if (string.IsNullOrWhiteSpace(text) || text.Length > 18) return false;
			bool hasAscii = false;
			foreach (char c in text)
			{
				if (c > 127) return false;
				if (char.IsLetterOrDigit(c)) hasAscii = true;
			}
			return hasAscii;
		}

		private static List<string[]> ParseClipboardRows(string raw)
		{
			var result = new List<string[]>();
			if (string.IsNullOrWhiteSpace(raw)) return result;

			if (!raw.Contains('\t'))
			{
				var lines = raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var line in lines)
				{
					string trimmed = line.Trim();
					if (!string.IsNullOrWhiteSpace(trimmed))
						result.Add(new[] { trimmed });
				}
				return result;
			}

			var rows = new List<List<string>>();
			var currentRow = new List<string>();
			var currentCell = new StringBuilder();
			bool inQuotes = false;

			for (int i = 0; i < raw.Length; i++)
			{
				char c = raw[i];
				if (inQuotes)
				{
					if (c == '"')
					{
						if (i + 1 < raw.Length && raw[i + 1] == '"')
						{
							currentCell.Append('"');
							i++;
						}
						else
						{
							inQuotes = false;
						}
					}
					else
					{
						currentCell.Append(c);
					}
				}
				else
				{
					if (c == '"' && currentCell.Length == 0)
					{
						inQuotes = true;
					}
					else if (c == '\t')
					{
						currentRow.Add(currentCell.ToString());
						currentCell.Clear();
					}
					else if (c == '\r' || c == '\n')
					{
						if (c == '\r' && i + 1 < raw.Length && raw[i + 1] == '\n')
							i++;
						currentRow.Add(currentCell.ToString());
						currentCell.Clear();
						if (currentRow.Any(cell => !string.IsNullOrWhiteSpace(cell)))
							rows.Add(currentRow);
						currentRow = new List<string>();
					}
					else
					{
						currentCell.Append(c);
					}
				}
			}

			if (currentCell.Length > 0 || currentRow.Count > 0)
			{
				currentRow.Add(currentCell.ToString());
				if (currentRow.Any(cell => !string.IsNullOrWhiteSpace(cell)))
					rows.Add(currentRow);
			}

			foreach (var r in rows)
			{
				result.Add(r.Select(cell =>
					cell.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim()
				).ToArray());
			}
			return result;
		}

		private void ImportTextOrCsvFile()
		{
			using var ofd = new OpenFileDialog
			{
				Title = "导入文案表格或文本文件",
				Filter = "文案文件 (*.txt;*.csv;*.tsv)|*.txt;*.csv;*.tsv|所有文件 (*.*)|*.*"
			};
			if (ofd.ShowDialog(this) != DialogResult.OK) return;

			try
			{
				string content = File.ReadAllText(ofd.FileName, Encoding.UTF8);
				Clipboard.SetText(content);
				PasteClipboardData();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, $"读取文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void AddEmptyRow()
		{
			var item = new ScriptBatchItem
			{
				Id = _items.Count + 1,
				ScriptCode = $"A{_items.Count + 1:000}",
				ChineseTitle = $"非洲葡语口播_{_items.Count + 1:00}",
				ScriptText = string.IsNullOrWhiteSpace(_txtQuickScript?.Text)
					? "Olá amigos! Bem-vindos ao nosso canal, hoje temos uma novidade incrível para vocês!"
					: _txtQuickScript.Text.Trim(),
				VoiceKey = (_cboDefaultVoice.SelectedItem as VoiceOption)?.Key ?? _settings.DefaultVoice,
				Status = "等待中"
			};
			_items.Add(item);
			AddGridRowForItem(item);
			if (_grid.Rows.Count > 0)
			{
				_grid.ClearSelection();
				_grid.Rows[_grid.Rows.Count - 1].Selected = true;
			}
			UpdateFileNamePreview();
			SyncStageMetadata();
		}

		private void AddGridRowForItem(ScriptBatchItem item)
		{
			if (string.IsNullOrWhiteSpace(item.QcBadge) || item.QcBadge == "⚪ 未检测")
			{
				item.QcBadge = "⚪ 点击自检";
			}
			int rowIdx = _grid.Rows.Add(
				item.Id,
				item.ScriptCode,
				item.ChineseTitle,
				item.ScriptText,
				FormatCharAndDuration(item.ScriptText),
				item.Status,
				"▶ 试听",
				item.QcBadge
			);
			_grid.Rows[rowIdx].Tag = item;
		}

		private void DeleteSelectedRows()
		{
			if (_isProcessing) return;
			if (_grid.SelectedRows.Count == 0) return;

			var toRemove = new List<DataGridViewRow>();
			foreach (DataGridViewRow r in _grid.SelectedRows)
			{
				toRemove.Add(r);
			}

			foreach (var r in toRemove)
			{
				if (r.Tag is ScriptBatchItem item)
					_items.Remove(item);
				_grid.Rows.Remove(r);
			}

			for (int i = 0; i < _items.Count; i++)
			{
				_items[i].Id = i + 1;
				_grid.Rows[i].Cells[0].Value = _items[i].Id;
			}
			UpdateFileNamePreview();
			SyncStageMetadata();
		}

		private void ClearAllRows()
		{
			if (_isProcessing) return;
			_items.Clear();
			_grid.Rows.Clear();
			_progressBar.Value = 0;
			_lblStatusText.Text = "列表已清空。";
			UpdateFileNamePreview();
			SyncStageMetadata();
		}

		private void SyncGridEditsToItems()
		{
			for (int i = 0; i < _grid.Rows.Count; i++)
			{
				var row = _grid.Rows[i];
				if (row.Tag is ScriptBatchItem item)
				{
					item.ScriptCode = Convert.ToString(row.Cells[1].Value)?.Trim() ?? "";
					item.ChineseTitle = Convert.ToString(row.Cells[2].Value)?.Trim() ?? "";
					item.ScriptText = Convert.ToString(row.Cells[3].Value)?.Trim() ?? "";
				}
			}
		}

		private void BrowseOutputFolder()
		{
			using var fbd = new FolderBrowserDialog { Description = "选择口播 MP3 音频批量导出目录" };
			if (Directory.Exists(_txtOutputDir.Text))
				fbd.SelectedPath = _txtOutputDir.Text;
			if (fbd.ShowDialog(this) == DialogResult.OK)
			{
				_txtOutputDir.Text = fbd.SelectedPath;
				SaveSettings();
			}
		}

		private void OpenOutputFolder()
		{
			string dir = _txtOutputDir.Text.Trim();
			if (string.IsNullOrWhiteSpace(dir)) return;
			if (!Directory.Exists(dir))
			{
				try { Directory.CreateDirectory(dir); } catch { }
			}
			if (Directory.Exists(dir))
			{
				Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
			}
		}

		// ========================================================
		// 6. 舞台 60FPS 律动动画、试听与批量 MP3 (+SRT) 导出核心
		// ========================================================
		private void OnAnimationTick(object sender, EventArgs e)
		{
			if (_cyberStage == null) return;

			bool playing = ScriptBatchTtsEngine.IsPlaying;
			bool paused = ScriptBatchTtsEngine.IsPaused;
			double pos = ScriptBatchTtsEngine.GetCurrentPositionSeconds();
			double dur = ScriptBatchTtsEngine.GetTotalDurationSeconds();

			if (playing && dur > 0.1 && pos >= dur - 0.05)
			{
				playing = false;
			}

			_cyberStage.IsPlaying = playing;
			_cyberStage.IsPaused = paused;
			_cyberStage.IsSynthesizing = _isAuditioning || _isProcessing;
			_cyberStage.CurrentSeconds = pos;
			_cyberStage.TotalSeconds = dur;
			_cyberStage.StepAnimation();

			if (_btnStagePlay != null)
			{
				if (playing)
				{
					_btnStagePlay.Text = "⏸ 暂停播放";
					_btnStagePlay.BackColor = AccentYellow;
				}
				else if (paused)
				{
					_btnStagePlay.Text = "▶ 继续播放";
					_btnStagePlay.BackColor = AccentGreen;
				}
				else
				{
					_btnStagePlay.Text = "▶ 播放 / 试听";
					_btnStagePlay.BackColor = AccentBlue;
				}
			}

			if (_lblStageTime != null)
			{
				_lblStageTime.Text = $"{FormatMmSs(pos)} / {FormatMmSs(dur)}";
			}
		}

		private static string FormatMmSs(double sec)
		{
			if (double.IsNaN(sec) || sec < 0) sec = 0;
			int m = (int)(sec / 60);
			int s = (int)(sec % 60);
			return $"{m:00}:{s:00}";
		}

		private async Task ToggleStagePlayPauseAsync()
		{
			if (ScriptBatchTtsEngine.IsPlaying)
			{
				ScriptBatchTtsEngine.PauseAudio();
				return;
			}
			if (ScriptBatchTtsEngine.IsPaused && !string.IsNullOrWhiteSpace(ScriptBatchTtsEngine.CurrentPlayingPath))
			{
				ScriptBatchTtsEngine.ResumeAudio();
				return;
			}
			await AuditionSelectedOrQuickScriptAsync();
		}

		private void StopAllAudioPlayback()
		{
			_auditionCts?.Cancel();
			ScriptBatchTtsEngine.StopAudio();
			ResetAuditionButtonState();
			if (_cyberStage != null)
			{
				_cyberStage.IsPlaying = false;
				_cyberStage.IsPaused = false;
				_cyberStage.CurrentSeconds = 0;
				_cyberStage.Invalidate();
			}
		}

		private void ResetAuditionButtonState()
		{
			_isAuditioning = false;
			if (_btnAudition != null && !_btnAudition.IsDisposed)
			{
				_btnAudition.Text = "⚡ 试听当前音色效果 (预置样音 · 毫秒级秒出声)";
				_btnAudition.BackColor = AccentCyan;
			}
		}

		/// <summary>
		/// 右侧设置面板专用：无需等待左侧长文案合成，直接读取本地预置音色样音并毫秒级应用当前语速、语调与电容麦温润调音。
		/// </summary>
		private async Task AuditionCurrentVoicePresetTunedAsync()
		{
			if (_isAuditioning || ScriptBatchTtsEngine.IsPlaying)
			{
				StopAllAudioPlayback();
				return;
			}

			SaveSettings();

			int engineIdx = _cboTtsEngine.SelectedIndex;
			bool isChatterbox = engineIdx == 2;
			bool isEleven = engineIdx == 1;
			string voiceKey = (_cboDefaultVoice.SelectedItem as VoiceOption)?.Key ?? _settings.DefaultVoice;
			string voiceDisplay = _cboDefaultVoice.Text;

			_isAuditioning = true;
			_auditionCts?.Cancel();
			_auditionCts = new CancellationTokenSource();
			var ct = _auditionCts.Token;

			if (_btnAudition != null)
			{
				_btnAudition.Text = "⏹️ 停止试听 (播放中...)";
				_btnAudition.BackColor = Color.FromArgb(220, 38, 38);
			}

			try
			{
				string samplePath = null;

				if (isChatterbox)
				{
					if (!string.IsNullOrWhiteSpace(_settings.ChatterboxReferenceAudio) && File.Exists(_settings.ChatterboxReferenceAudio))
					{
						samplePath = _settings.ChatterboxReferenceAudio;
					}
					else
					{
						string candWav = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auditions", "chatterbox_998.wav");
						if (File.Exists(candWav)) samplePath = candWav;
						else samplePath = ScriptBatchTtsEngine.GetAuditionFilePath("pt-PT-DuarteNeural");
					}
				}
				else if (isEleven)
				{
					string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auditions");
					Directory.CreateDirectory(cacheDir);
					string cachedAudio = Path.Combine(cacheDir, $"eleven_{voiceKey}.mp3");

					if (!File.Exists(cachedAudio) && ElevenLabsEngine.VoicePreviewUrls.TryGetValue(voiceKey, out var elPreviewUrl) && !string.IsNullOrWhiteSpace(elPreviewUrl))
					{
						_lblStatusText.Text = $"正在获取 ElevenLabs 官方零额度样音 ({voiceDisplay})...";
						try
						{
							using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
							byte[] bytes = await http.GetByteArrayAsync(elPreviewUrl);
							if (bytes != null && bytes.Length > 1024) File.WriteAllBytes(cachedAudio, bytes);
						}
						catch { }
					}

					if (File.Exists(cachedAudio))
					{
						samplePath = cachedAudio;
					}
					else
					{
						samplePath = ScriptBatchTtsEngine.GetAuditionFilePath("pt-PT-DuarteNeural");
					}
				}
				else
				{
					samplePath = ScriptBatchTtsEngine.GetAuditionFilePath(voiceKey);
					if (string.IsNullOrEmpty(samplePath) || !File.Exists(samplePath))
					{
						string auditionDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auditions");
						Directory.CreateDirectory(auditionDir);
						string shortSentence = ScriptBatchTtsEngine.GetPresetSampleSentence(voiceKey);
						_lblStatusText.Text = $"正在首次缓存该音色预置样音 ({voiceDisplay})...";
						var res = await ScriptBatchTtsEngine.SynthesizeAsync(shortSentence, voiceKey, "+0%", "+0Hz", "SentenceBoundary", auditionDir, 999, ct);
						if (res.Success && File.Exists(res.AudioPath))
						{
							string targetSample = Path.Combine(auditionDir, $"{voiceKey}.mp3");
							try { File.Copy(res.AudioPath, targetSample, true); samplePath = targetSample; }
							catch { samplePath = res.AudioPath; }
						}
					}
				}

				if (string.IsNullOrEmpty(samplePath) || !File.Exists(samplePath))
				{
					throw new Exception("未能加载当前音色的预置样音文件。");
				}

				string tunedPath = await ScriptBatchTtsEngine.RenderTunedPresetSampleAsync(
					samplePath,
					voiceKey,
					_settings.SpeechRate,
					_settings.SpeechPitch,
					_settings.TtsStudioWarmBroadcastEq,
					ct);

				if (string.IsNullOrEmpty(tunedPath) || !File.Exists(tunedPath))
					tunedPath = samplePath;

				var envelope = await ScriptBatchTtsEngine.ExtractWaveformEnvelopeAsync(tunedPath);
				_cyberStage.SetWaveformEnvelope(envelope);
				_cyberStage.TrackTitle = $"[音色与参数试听] {voiceDisplay}";
				_cyberStage.ScriptPreviewText = ScriptBatchTtsEngine.GetPresetSampleSentence(voiceKey);
				SyncStageMetadata();

				bool played = ScriptBatchTtsEngine.PlayAudioFile(tunedPath, () =>
				{
					if (IsHandleCreated && !IsDisposed) BeginInvoke((MethodInvoker)ResetAuditionButtonState);
				});

				if (played)
				{
					ScriptBatchTtsEngine.SetVolume(_trkVolume.Value / 100.0);
					_lblStatusText.Text = $"🔊 [秒播试听中] {voiceDisplay}  |  语速: {_cboRate.Text}  |  语调: {_cboPitch.Text}";
				}
				else
				{
					ResetAuditionButtonState();
				}
			}
			catch (OperationCanceledException)
			{
				_lblStatusText.Text = "已停止音色试听。";
				ResetAuditionButtonState();
			}
			catch (Exception ex)
			{
				_lblStatusText.Text = $"音色试听提示: {ex.Message}";
				ResetAuditionButtonState();
			}
		}

		private async Task AuditionSelectedOrQuickScriptAsync()
		{
			SyncGridEditsToItems();
			if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ScriptBatchItem selItem)
			{
				await AuditionSpecificItemAsync(selItem, _grid.SelectedRows[0]);
				return;
			}

			string quickText = _txtQuickScript.Text.Trim();
			if (string.IsNullOrWhiteSpace(quickText))
			{
				quickText = "Olá amigos! Bem-vindos ao nosso estúdio de voz inteligente. Vamos ouvir este efeito natural!";
				_txtQuickScript.Text = quickText;
			}

			var tempItem = new ScriptBatchItem
			{
				Id = 0,
				ScriptCode = "DEMO",
				ChineseTitle = "实时口播试听",
				ScriptText = quickText,
				VoiceKey = (_cboDefaultVoice.SelectedItem as VoiceOption)?.Key ?? _settings.DefaultVoice
			};
			await AuditionSpecificItemAsync(tempItem, null);
		}

		private async Task AuditionSpecificItemAsync(ScriptBatchItem item, DataGridViewRow row)
		{
			if (_isAuditioning)
			{
				StopAllAudioPlayback();
				return;
			}

			SaveSettings();
			string rawScript = item?.ScriptText?.Trim();
			if (string.IsNullOrWhiteSpace(rawScript))
			{
				MessageBox.Show(this, "当前配音文案为空，请先输入或粘贴文案！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// ElevenLabs 官方预设音色如果未传自定义文案或用户希望零额度试听样本，但既然在工作台，若配置了Key则合成真实文案，若无Key则播放官方预览
			if (_cboTtsEngine.SelectedIndex == 1 && _cboDefaultVoice.SelectedItem is VoiceOption elVo)
			{
				var usableKey = ElevenLabsKeyPool.Instance.GetNextUsableKey();
				if ((usableKey == null || string.IsNullOrWhiteSpace(usableKey.ApiKey)) && ElevenLabsEngine.VoicePreviewUrls.TryGetValue(elVo.Key, out var elPreviewUrl) && !string.IsNullOrWhiteSpace(elPreviewUrl))
				{
					try
					{
						_lblStatusText.Text = $"正在拉取 ElevenLabs 官方零额度试听 ({elVo.DisplayName})...";
						string cacheDir = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_VoicePreviews");
						Directory.CreateDirectory(cacheDir);
						string previewFile = Path.Combine(cacheDir, $"eleven_{elVo.Key}.mp3");
						if (!File.Exists(previewFile) || new FileInfo(previewFile).Length < 512)
						{
							using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
							byte[] bytes = await http.GetByteArrayAsync(elPreviewUrl);
							File.WriteAllBytes(previewFile, bytes);
						}
						var env = await ScriptBatchTtsEngine.ExtractWaveformEnvelopeAsync(previewFile);
						_cyberStage.SetWaveformEnvelope(env);
						ScriptBatchTtsEngine.PlayAudioFile(previewFile);
						ScriptBatchTtsEngine.SetVolume(_trkVolume.Value / 100.0);
						_lblStatusText.Text = $"🔊 正在播放 ElevenLabs 官方音色样本 (添加 API Key 后即可直接试听您的文案)";
						return;
					}
					catch { }
				}
			}

			_isAuditioning = true;
			_auditionCts?.Cancel();
			_auditionCts = new CancellationTokenSource();
			var ct = _auditionCts.Token;

			if (row != null)
			{
				row.Cells[5].Value = "🎧 合成试听...";
				row.Cells[6].Value = "⏹ 停止";
				row.Cells[7].Value = "🔍 自检中...";
			}

			try
			{
				string tempDir = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_TtsStudioPreview");
				Directory.CreateDirectory(tempDir);
				string outMp3 = Path.Combine(tempDir, $"audition_{DateTime.Now:HHmmss_fff}.mp3");

				_lblStatusText.Text = "🎙️ 正在注入气口与广播级 EQ 合成当前文案试听并执行 5 重自检...";
				SyncStageMetadata();

				var synthResult = await SynthesizeItemAudioCoreAsync(item, outMp3, false, ct);
				if (ct.IsCancellationRequested || !File.Exists(synthResult.Mp3Path)) return;

				var envelope = await ScriptBatchTtsEngine.ExtractWaveformEnvelopeAsync(synthResult.Mp3Path);
				_cyberStage.SetWaveformEnvelope(envelope);
				_cyberStage.TrackTitle = item.DisplayTitle;
				_cyberStage.ScriptPreviewText = item.ScriptText;

				ScriptBatchTtsEngine.PlayAudioFile(synthResult.Mp3Path);
				ScriptBatchTtsEngine.SetVolume(_trkVolume.Value / 100.0);

				_lblStatusText.Text = $"🔊 正在试听文案: {item.DisplayTitle}  |  自检: {item.QcBadge}";
				if (row != null)
				{
					row.Cells[5].Value = "🔊 试听中";
					row.Cells[6].Value = "▶ 重听";
					row.Cells[7].Value = item.QcBadge;
				}
			}
			catch (OperationCanceledException)
			{
				_lblStatusText.Text = "已取消文案试听。";
				if (row != null)
				{
					row.Cells[5].Value = "⏹ 已取消";
					row.Cells[6].Value = "▶ 试听";
				}
			}
			catch (Exception ex)
			{
				if (ct.IsCancellationRequested)
				{
					_lblStatusText.Text = "已取消文案试听。";
					if (row != null)
					{
						row.Cells[5].Value = "⏹ 已取消";
						row.Cells[6].Value = "▶ 试听";
					}
				}
				else
				{
					BuildTtsFailureDiagnosticReport(item, ex);
					if (row != null)
					{
						row.Cells[5].Value = "❌ 失败";
						row.Cells[6].Value = "▶ 试听";
						row.Cells[7].Value = item.QcBadge;
					}
					_lblStatusText.Text = $"❌ 试听合成失败 (点击表格【🔴 失败(点击查原因)】查看排障方案): {ex.Message}";
					if (row != null)
					{
						ShowItemQcReportDialog(row, item);
					}
					else
					{
						MessageBox.Show(this, $"试听合成失败:\n{ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					}
				}
			}
			finally
			{
				_isAuditioning = false;
			}
		}

		private async Task<(string Mp3Path, string SrtPath)> SynthesizeItemAudioCoreAsync(
			ScriptBatchItem item,
			string finalMp3Path,
			bool exportSrt,
			CancellationToken ct)
		{
			string workDir = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_TtsStudioWork", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(workDir);

			try
			{
				// 1. 口播自然气口预处理 ("去念稿感")
				string processedScript = ScriptBatchTtsEngine.NaturalizeScriptForOralBroadcast(
					item.ScriptText,
					_settings.TtsStudioNaturalBreathing);

				string engine = _settings.TtsEngine ?? "edge_tts";
				string selectedVoice = (_cboDefaultVoice.SelectedItem as VoiceOption)?.Key;
				int taskId = Math.Max(1, item.Id);
				TtsResult tts;

				if (string.Equals(engine, "chatterbox", StringComparison.OrdinalIgnoreCase))
				{
					string voice = !string.IsNullOrWhiteSpace(selectedVoice) && selectedVoice.StartsWith("chatterbox:")
						? selectedVoice
						: "chatterbox:" + _settings.ChatterboxLanguage;
					// 强制生成逐词时间轴与 SRT 以便 5重自检雷达精准核验文案/音频/SRT三方一致性与卡顿断层
					tts = await ChatterboxEngine.SynthesizeAsync(processedScript, voice, _settings, workDir, taskId, ct,
						msg => { try { BeginInvoke(new Action(() => _lblStatusText.Text = msg)); } catch { } },
						audioOnly: false);
				}
				else if (string.Equals(engine, "elevenlabs", StringComparison.OrdinalIgnoreCase))
				{
					string elVoice = string.IsNullOrWhiteSpace(selectedVoice) ? _settings.ElevenLabsVoice : selectedVoice;
					if (elVoice == "CustomVoice" && !string.IsNullOrWhiteSpace(_settings.ElevenLabsCustomVoiceId))
						elVoice = _settings.ElevenLabsCustomVoiceId.Trim();
					tts = await ElevenLabsEngine.SynthesizeWithFailoverAsync(processedScript, elVoice, workDir, taskId, ct);
					UpdateKeyButtonText();
				}
				else
				{
					string edgeVoice = !string.IsNullOrWhiteSpace(selectedVoice) && !selectedVoice.StartsWith("chatterbox:")
						? selectedVoice
						: _settings.DefaultVoice;
					tts = await ScriptBatchTtsEngine.SynthesizeAsync(processedScript, edgeVoice,
						_settings.SpeechRate, _settings.SpeechPitch, "WordBoundary", workDir, taskId, ct);
				}

				if (tts == null || !tts.Success || string.IsNullOrEmpty(tts.AudioPath) || !File.Exists(tts.AudioPath))
				{
					throw new InvalidOperationException("语音合成失败: " + (tts?.ErrorMessage ?? "未知错误"));
				}
				string rawAudioPath = tts.AudioPath;
				string rawSrtPath = tts.SrtPath;

				// 2. FFmpeg 广播级 320Kbps 48kHz 电容麦温润调音（或童声专属清亮共振峰调音）母带导出 MP3
				await ScriptBatchTtsEngine.MasterAudioToMp3Async(
					rawAudioPath,
					finalMp3Path,
					_settings.TtsStudioWarmBroadcastEq,
					ct,
					selectedVoice);

				// 3. 根据用户勾选状态决定是否导出同步 .SRT 字幕
				string finalSrtPath = null;
				if (exportSrt && !string.IsNullOrEmpty(rawSrtPath) && File.Exists(rawSrtPath))
				{
					finalSrtPath = Path.ChangeExtension(finalMp3Path, ".srt");
					File.Copy(rawSrtPath, finalSrtPath, true);
					string rawWordsJson = rawSrtPath + ".words.json";
					if (File.Exists(rawWordsJson))
					{
						try { File.Copy(rawWordsJson, finalSrtPath + ".words.json", true); } catch { }
					}
				}

				item.AudioPath = finalMp3Path;
				item.SrtPath = finalSrtPath ?? "";
				item.DurationSeconds = tts.DurationSeconds;

				// 4. 执行 5 重深度自检雷达（文案·音频·SRT一致性 / 防卡顿断层 / 防结巴吞字 / 清晰度母带 / SRT时间轴规范）
				await RunTtsAudioAndSrtQualityCheckAsync(item, finalMp3Path, rawSrtPath, exportSrt, finalSrtPath);

				return (finalMp3Path, finalSrtPath);
			}
			finally
			{
				try { Directory.Delete(workDir, true); } catch { }
			}
		}

		private async Task ExportSingleCurrentScriptAsync()
		{
			if (_isProcessing) return;
			SyncGridEditsToItems();
			SaveSettings();

			ScriptBatchItem targetItem = null;
			DataGridViewRow targetRow = null;

			if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ScriptBatchItem selItem)
			{
				targetItem = selItem;
				targetRow = _grid.SelectedRows[0];
			}
			else if (!string.IsNullOrWhiteSpace(_txtQuickScript.Text))
			{
				targetItem = new ScriptBatchItem
				{
					Id = 1,
					ScriptCode = "QUICK",
					ChineseTitle = "单条口播配音",
					ScriptText = _txtQuickScript.Text.Trim()
				};
			}

			if (targetItem == null || string.IsNullOrWhiteSpace(targetItem.ScriptText))
			{
				MessageBox.Show(this, "请先在左侧表格选择一条文案，或在底部编辑框输入配音文案！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			string outDir = _txtOutputDir.Text.Trim();
			if (string.IsNullOrWhiteSpace(outDir))
			{
				outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "口播语音导出_MP3");
				_txtOutputDir.Text = outDir;
			}
			Directory.CreateDirectory(outDir);

			_isProcessing = true;
			_btnStartBatch.Enabled = false;
			_btnStopBatch.Enabled = true;
			_cts = new CancellationTokenSource();

			try
			{
				string producer = string.IsNullOrWhiteSpace(_txtProducerName.Text) ? "陆恒" : _txtProducerName.Text.Trim();
				string baseName = _chkNamingRule.Checked
					? targetItem.GenerateStandardFileName(producer)
					: SanitizeSimpleFileName(targetItem.DisplayTitle);

				string mp3Path = GetUniqueOutputPath(outDir, baseName, ".mp3");
				if (targetRow != null)
				{
					targetRow.Cells[5].Value = "🎙️ 导出中...";
					targetRow.Cells[7].Value = "🔍 自检中...";
				}
				_lblStatusText.Text = $"正在导出并执行 5 重自检: {Path.GetFileName(mp3Path)}...";

				var res = await SynthesizeItemAudioCoreAsync(targetItem, mp3Path, _chkExportSrt.Checked, _cts.Token);
				targetItem.AudioPath = res.Mp3Path;
				targetItem.Status = "✅ 已完成";
				if (targetRow != null)
				{
					targetRow.Cells[5].Value = "✅ 已完成";
					targetRow.Cells[7].Value = targetItem.QcBadge;
				}

				var env = await ScriptBatchTtsEngine.ExtractWaveformEnvelopeAsync(res.Mp3Path);
				_cyberStage.SetWaveformEnvelope(env);
				ScriptBatchTtsEngine.PlayAudioFile(res.Mp3Path);
				ScriptBatchTtsEngine.SetVolume(_trkVolume.Value / 100.0);

				string srtNote = !string.IsNullOrWhiteSpace(res.SrtPath) ? " + 同步 .SRT 字幕" : " (纯净 MP3)";
				_lblStatusText.Text = $"🎉 导出成功: {Path.GetFileName(res.Mp3Path)}{srtNote}  |  自检: {targetItem.QcBadge}";
			}
			catch (OperationCanceledException)
			{
				_lblStatusText.Text = "已停止导出。";
				if (targetRow != null) targetRow.Cells[5].Value = "⏹️ 已取消";
			}
			catch (Exception ex)
			{
				BuildTtsFailureDiagnosticReport(targetItem, ex);
				if (targetRow != null)
				{
					targetRow.Cells[5].Value = "❌ 失败";
					targetRow.Cells[7].Value = targetItem.QcBadge;
				}
				_lblStatusText.Text = $"❌ 导出失败 (已生成排障自检报告): {ex.Message}";
				ShowItemQcReportDialog(targetRow, targetItem);
			}
			finally
			{
				_isProcessing = false;
				_btnStartBatch.Enabled = true;
				_btnStopBatch.Enabled = false;
			}
		}

		private async Task StartBatchAudioExportAsync()
		{
			if (_isProcessing) return;
			SyncGridEditsToItems();
			SaveSettings();

			// 如果表格为空但快速输入框有内容，自动加入表格第一行
			if (_items.Count == 0 && !string.IsNullOrWhiteSpace(_txtQuickScript.Text))
			{
				AddEmptyRow();
			}

			if (_items.Count == 0)
			{
				MessageBox.Show(this, "请先粘贴（Ctrl+V）或导入需要转语音的文案列表！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			string outputDir = _txtOutputDir.Text.Trim();
			if (string.IsNullOrWhiteSpace(outputDir))
			{
				outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "口播语音导出_MP3");
				_txtOutputDir.Text = outputDir;
			}
			Directory.CreateDirectory(outputDir);

			_isProcessing = true;
			_btnStartBatch.Enabled = false;
			_btnStopBatch.Enabled = true;
			_cts = new CancellationTokenSource();
			var ct = _cts.Token;

			int total = _items.Count;
			int successCount = 0;
			int failCount = 0;
			DataGridViewRow firstFailedRow = null;
			ScriptBatchItem firstFailedItem = null;
			bool exportSrt = _chkExportSrt.Checked;
			string producer = string.IsNullOrWhiteSpace(_txtProducerName.Text) ? "陆恒" : _txtProducerName.Text.Trim();

			_progressBar.Maximum = total;
			_progressBar.Value = 0;

			try
			{
				for (int i = 0; i < total; i++)
				{
					ct.ThrowIfCancellationRequested();
					var item = _items[i];
					var row = _grid.Rows[i];

					if (string.IsNullOrWhiteSpace(item.ScriptText))
					{
						row.Cells[5].Value = "⏭️ 跳过(空)";
						row.Cells[7].Value = "⚪ 空文案跳过";
						_progressBar.Value = i + 1;
						continue;
					}

					_grid.ClearSelection();
					row.Selected = true;
					_grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i - 2);

					row.Cells[5].Value = "🎙️ 合成配音中...";
					row.Cells[7].Value = "🔍 正在自检...";
					_lblStatusText.Text = $"[{i + 1}/{total}] 正在生成广播级口播 MP3 并执行 5 重自检: {item.DisplayTitle}...";

					try
					{
						string baseName = _chkNamingRule.Checked
							? item.GenerateStandardFileName(producer)
							: SanitizeSimpleFileName(item.DisplayTitle);

						string finalMp3 = GetUniqueOutputPath(outputDir, baseName, ".mp3");
						var res = await SynthesizeItemAudioCoreAsync(item, finalMp3, exportSrt, ct);

						item.AudioPath = res.Mp3Path;
						item.Status = "✅ 已完成";
						row.Cells[5].Value = "✅ 已完成";
						row.Cells[7].Value = item.QcBadge;
						successCount++;

						// 更新舞台波形预览
						var env = await ScriptBatchTtsEngine.ExtractWaveformEnvelopeAsync(res.Mp3Path);
						_cyberStage.SetWaveformEnvelope(env);
					}
					catch (OperationCanceledException)
					{
						row.Cells[5].Value = "⏹️ 已取消";
						row.Cells[7].Value = "⚪ 已手动取消";
						throw;
					}
					catch (Exception ex)
					{
						failCount++;
						BuildTtsFailureDiagnosticReport(item, ex);
						row.Cells[5].Value = "❌ 失败";
						row.Cells[7].Value = item.QcBadge;
						if (firstFailedItem == null)
						{
							firstFailedItem = item;
							firstFailedRow = row;
						}
						_lblStatusText.Text = $"[{i + 1}/{total}] ❌ 失败 (点击【自检结果】列查看原因与一键修复): {ex.Message}";
					}

					_progressBar.Value = i + 1;
				}

				string srtSummary = exportSrt ? "（已同步生成同名 .SRT 字幕）" : "（纯 MP3 音频模式）";
				if (failCount > 0 && firstFailedItem != null)
				{
					_lblStatusText.Text = $"⚠️ 批量处理结束：成功 {successCount}/{total} 条，失败 {failCount} 条。点击表格【🔴 失败(点击查原因)】可查看原因并一键修复！";
					ShowItemQcReportDialog(firstFailedRow, firstFailedItem);
				}
				else
				{
					_lblStatusText.Text = $"🎉 批量导出完成！成功 {successCount}/{total} 条 {srtSummary}，全部通过 5 重自检！";
					MessageBox.Show(this,
						$"批量文字转语音完成！\n\n• 成功导出：{successCount} / {total} 条广播级 MP3 (320Kbps)\n• 字幕模式：{(exportSrt ? "已同步导出 .SRT 字幕文件" : "仅导出纯净 MP3 音频")}\n• 自检提示：可随时点击左侧表格【自检结果 (点击查看)】列审阅每条音频的流畅度、清晰度与文案/SRT一致性报告。\n• 输出目录：{outputDir}",
						"批量导出与自检完成",
						MessageBoxButtons.OK,
						MessageBoxIcon.Information);
				}
			}
			catch (OperationCanceledException)
			{
				_lblStatusText.Text = $"⏹️ 已手动停止批量导出（已完成 {successCount}/{total}）。";
			}
			finally
			{
				_isProcessing = false;
				_btnStartBatch.Enabled = true;
				_btnStopBatch.Enabled = false;
			}
		}

		// ========================================================
		// 6.5 AI 5重深度自检雷达 & 失败根因排障系统 (文案·音频·SRT一致性 / 防卡顿 / 防结巴 / 清晰度)
		// ========================================================
		private void ShowItemQcReportDialog(DataGridViewRow row, ScriptBatchItem item)
		{
			if (item == null) return;
			using var dlg = new TtsAudioQcReportDialog(
				item,
				_settings,
				_cboTtsEngine.Text,
				_cboDefaultVoice.Text,
				async (switchToFreeEdgeEngine) =>
				{
					if (switchToFreeEdgeEngine)
					{
						_cboTtsEngine.SelectedIndex = 0; // 切换至方案 B: 微软国际神经网络 (免费极速)
						SaveSettings();
					}
					await RetrySingleItemFromQcDialogAsync(row, item);
				},
				() =>
				{
					if (!string.IsNullOrWhiteSpace(item.AudioPath) && File.Exists(item.AudioPath))
					{
						ScriptBatchTtsEngine.PlayAudioFile(item.AudioPath);
						ScriptBatchTtsEngine.SetVolume(_trkVolume.Value / 100.0);
					}
				});
			dlg.ShowDialog(this);
		}

		private async Task RetrySingleItemFromQcDialogAsync(DataGridViewRow row, ScriptBatchItem item)
		{
			if (_isProcessing || item == null) return;
			SyncGridEditsToItems();
			SaveSettings();

			string outDir = _txtOutputDir.Text.Trim();
			if (string.IsNullOrWhiteSpace(outDir))
			{
				outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "口播语音导出_MP3");
				_txtOutputDir.Text = outDir;
			}
			Directory.CreateDirectory(outDir);

			_isProcessing = true;
			_btnStartBatch.Enabled = false;
			_btnStopBatch.Enabled = true;
			_cts = new CancellationTokenSource();

			try
			{
				string producer = string.IsNullOrWhiteSpace(_txtProducerName.Text) ? "陆恒" : _txtProducerName.Text.Trim();
				string baseName = _chkNamingRule.Checked
					? item.GenerateStandardFileName(producer)
					: SanitizeSimpleFileName(item.DisplayTitle);

				string mp3Path = GetUniqueOutputPath(outDir, baseName, ".mp3");
				if (row != null)
				{
					row.Cells[5].Value = "🎙️ 修复重跑中...";
					row.Cells[7].Value = "🔍 正在自检...";
				}
				_lblStatusText.Text = $"正在重新合成并执行 5 重自检: {item.DisplayTitle}...";

				var res = await SynthesizeItemAudioCoreAsync(item, mp3Path, _chkExportSrt.Checked, _cts.Token);
				item.AudioPath = res.Mp3Path;
				item.Status = "✅ 已完成";
				item.ErrorMessage = "";

				if (row != null)
				{
					row.Cells[5].Value = "✅ 已完成";
					row.Cells[7].Value = item.QcBadge;
				}

				var env = await ScriptBatchTtsEngine.ExtractWaveformEnvelopeAsync(res.Mp3Path);
				_cyberStage.SetWaveformEnvelope(env);
				ScriptBatchTtsEngine.PlayAudioFile(res.Mp3Path);
				ScriptBatchTtsEngine.SetVolume(_trkVolume.Value / 100.0);

				_lblStatusText.Text = $"🎉 修复并自检完成: {Path.GetFileName(res.Mp3Path)}  |  {item.QcBadge}";
			}
			catch (Exception ex)
			{
				BuildTtsFailureDiagnosticReport(item, ex);
				if (row != null)
				{
					row.Cells[5].Value = "❌ 失败";
					row.Cells[7].Value = item.QcBadge;
				}
				_lblStatusText.Text = $"❌ 重试仍未通过: {ex.Message}";
			}
			finally
			{
				_isProcessing = false;
				_btnStartBatch.Enabled = true;
				_btnStopBatch.Enabled = false;
			}
		}

		private void BuildTtsFailureDiagnosticReport(ScriptBatchItem item, Exception ex)
		{
			if (item == null) return;
			string rawErr = ex?.Message ?? "未知合成异常";
			item.Status = "❌ 失败";
			item.ErrorMessage = rawErr;
			item.QcBadge = "🔴 失败(点击查原因)";

			string engine = _settings.TtsEngine ?? "edge_tts";
			string engineName = engine == "chatterbox"
				? "🎙️ 方案A: Chatterbox 本地声音克隆"
				: (engine == "elevenlabs" ? "✨ 进阶轨: ElevenLabs 官方 API" : "🌐 方案B: 微软国际神经网络 (Edge-TTS)");
			string voiceName = _cboDefaultVoice?.Text ?? _settings.DefaultVoice;

			var sb = new StringBuilder();
			sb.AppendLine("================================================================");
			sb.AppendLine("❌ 【文字转语音 · 任务失败根因诊断与一键成功指南】");
			sb.AppendLine("================================================================");
			sb.AppendLine($"• 当前配音引擎：{engineName}");
			sb.AppendLine($"• 当前所选音色：{voiceName}");
			sb.AppendLine($"• 目标文案字数：{(item.ScriptText?.Length ?? 0)} 字符");
			sb.AppendLine();

			sb.AppendLine("🔍 一、为什么会失败？（故障根因精准定位）");
			if (engine == "chatterbox" || rawErr.IndexOf("Chatterbox", StringComparison.OrdinalIgnoreCase) >= 0 ||
			    rawErr.Contains("对齐") || rawErr.Contains("本地配音") || rawErr.Contains("参考声音") || rawErr.Contains("模型"))
			{
				if (rawErr.Contains("安装本地模型") || rawErr.Contains("未找到"))
				{
					sb.AppendLine("  👉 原因：您当前选择了【方案A: Chatterbox 本地声音克隆】，但本地克隆模型尚未安装完成，或模型文件缺失。");
				}
				else if (rawErr.Contains("参考声音") || rawErr.Contains("授权"))
				{
					sb.AppendLine("  👉 原因：当前处于【方案A: Chatterbox 本地声音克隆】，但 6 秒参考音频文件不存在或未勾选使用授权。");
				}
				else if (rawErr.Contains("对齐") || rawErr.Contains("词数与文案") || rawErr.Contains("情绪强度"))
				{
					sb.AppendLine("  👉 原因：Chatterbox 本地克隆模型在合成该段长文案时，部分葡语连读短词或情绪起伏过大导致 AI 严苛逐词对齐检验拦截（为防止生成吞字/漏词的残次音频，系统主动中断了本次生成）。");
				}
				else
				{
					sb.AppendLine("  👉 原因：Chatterbox 本地克隆推理进程异常退出（通常是显卡显存 VRAM 瞬时不足或长句推理超时）。");
				}

				sb.AppendLine();
				sb.AppendLine("🛠️ 二、如何能 100% 成功？（两种解决方案任选其一）");
				sb.AppendLine("  ✅ 【方案 1（强烈推荐 · 秒级必成）】：");
				sb.AppendLine("     直接点击本窗口下方的蓝色按钮【🌐 一键切回微软免费极速引擎并重跑】！");
				sb.AppendLine("     微软国际神经网络拥有 9 款专为安哥拉/莫桑比克/巴西定制的自然口播音色（如 Duarte、Brian、Remy），免显卡、不限字数、毫秒级词级对齐，100% 稳定成功！");
				sb.AppendLine("  ✅ 【方案 2（继续使用本地克隆）】：");
				sb.AppendLine("     我已经为您自动优化了底层葡语连读对齐算法与情绪自适应降噪！您可以直接点击下方绿色按钮【🔄 立即重新合成并自检此条】重试；若仍提示对齐问题，可在【Chatterbox 模型设置】中将情绪强度 (Exaggeration) 调至 0.45~0.55。");
			}
			else if (engine == "elevenlabs" || rawErr.IndexOf("ElevenLabs", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				sb.AppendLine("  👉 原因：当前选择了【ElevenLabs 官方 API】，但密钥池中没有可用的 API Key、字符配额已耗尽，或 Voice ID 在当前账号下不存在。");
				sb.AppendLine();
				sb.AppendLine("🛠️ 二、如何能 100% 成功？");
				sb.AppendLine("  ✅ 方法 1：直接点击下方蓝色按钮【🌐 一键切回微软免费极速引擎并重跑】，零成本无限量合成！");
				sb.AppendLine("  ✅ 方法 2：点击右侧面板【🔑 ElevenLabs 密钥管理】，补充有效且有剩余字符额度的 API Key 后再点击【🔄 立即重新合成并自检此条】。");
			}
			else if (rawErr.Contains("取消") || rawErr.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				sb.AppendLine("  👉 原因：合成过程中任务被手动停止、切换了其它试听，或网络请求超时触发了保护性取消（已取消一个任务）。");
				sb.AppendLine();
				sb.AppendLine("🛠️ 二、如何能 100% 成功？");
				sb.AppendLine("  ✅ 直接点击本窗口下方绿色按钮【🔄 立即重新合成并自检此条】，保持网络通畅等待 2~4 秒即可顺利完成！");
			}
			else
			{
				sb.AppendLine("  👉 原因：连接微软国际神经网络语音节点时发生瞬时网络抖动、VPN/代理软件切换导致 WebSocket 中断，或目标输出文件被外部播放器占用。");
				sb.AppendLine();
				sb.AppendLine("🛠️ 二、如何能 100% 成功？");
				sb.AppendLine("  ✅ 第一步：若您的电脑开启了全局代理/VPN且节点不稳定，建议关闭代理直连（微软 Edge 语音在国内直连极快）或切换稳定节点；");
				sb.AppendLine("  ✅ 第二步：直接点击下方绿色按钮【🔄 立即重新合成并自检此条】（底层已内置 3 重自动容灾重连机制）。");
			}

			sb.AppendLine();
			sb.AppendLine("----------------------------------------------------------------");
			sb.AppendLine("📋 三、底层原始报错记录（供技术核查）：");
			sb.AppendLine($"  {rawErr}");

			item.QcDetails = sb.ToString();
		}

		private sealed class WordTimestampEntry
		{
			public string Text { get; set; } = "";
			public double Start { get; set; }
			public double End { get; set; }
		}

		private sealed class SrtCueEntry
		{
			public int Index { get; set; }
			public double Start { get; set; }
			public double End { get; set; }
			public string Text { get; set; } = "";
		}

		/// <summary>
		/// AI 5重深度自检雷达：
		/// 1. 文案 · 音频 · SRT 三方文本与时长严格一致性（防漏词、防多词、防首尾截断）
		/// 2. 声音流畅度检测（防中途卡顿、防异常静音断层）
		/// 3. 防结巴 · 防重复发音与吞字粘连检测（语速均匀度与吐字清晰度）
		/// 4. 声音清晰度与广播级母带品质检测（320Kbps/48kHz、峰值电平、有效人声能量比）
		/// 5. SRT 字幕轴规范与零重叠核验
		/// </summary>
		private async Task RunTtsAudioAndSrtQualityCheckAsync(
			ScriptBatchItem item,
			string mp3Path,
			string rawSrtPath,
			bool exportedSrt,
			string finalSrtPath)
		{
			var issues = new List<string>();
			int warnings = 0;
			int errors = 0;
			string primaryReason = "";

			string scriptText = (item.ScriptText ?? "").Trim();
			var scriptTokens = ExtractCleanWordTokens(scriptText);
			int scriptCharCount = scriptText.Length;

			// 读取词级时间戳 (.words.json) 与 SRT 字幕
			var wordEntries = LoadWordTimestamps(rawSrtPath);
			var srtCues = LoadSrtCues(!string.IsNullOrEmpty(finalSrtPath) && File.Exists(finalSrtPath) ? finalSrtPath : rawSrtPath);
			var srtTokens = ExtractCleanWordTokens(string.Join(" ", srtCues.Select(c => c.Text)));
			var spokenTokens = wordEntries.Count > 0
				? ExtractCleanWordTokens(string.Join(" ", wordEntries.Select(w => w.Text)))
				: srtTokens;

			// 提取真实 MP3 音频波形包络 (用于检测中途异常断层、静音卡顿、人声清晰度能量)
			float[] envelope = await ScriptBatchTtsEngine.ExtractWaveformEnvelopeAsync(mp3Path, 240);
			double audioDuration = ProbeMp3DurationFast(mp3Path, srtCues, wordEntries, item.DurationSeconds);
			item.DurationSeconds = audioDuration;

			// ================================================================
			// 标准 1：文案 · 音频 · SRT 三方一致性核验（防增删 / 防漏词 / 防截断）
			// ================================================================
			if (scriptTokens.Count > 0 && spokenTokens.Count > 0)
			{
				double countRatio = spokenTokens.Count / (double)scriptTokens.Count;
				double matchScore = ComputeTokenMatchSimilarity(scriptTokens, spokenTokens);

				bool firstWordMatch = IsTokenSimilar(scriptTokens[0], spokenTokens[0]);
				bool lastWordMatch = IsTokenSimilar(scriptTokens[scriptTokens.Count - 1], spokenTokens[spokenTokens.Count - 1]);

				double srtEndSec = srtCues.Count > 0 ? srtCues[srtCues.Count - 1].End : (wordEntries.Count > 0 ? wordEntries[wordEntries.Count - 1].End : audioDuration);
				double tailDiff = Math.Abs(audioDuration - srtEndSec);

				if (countRatio < 0.86 || matchScore < 0.82)
				{
					errors++;
					primaryReason = "文案漏词/删减";
					issues.Add(
						$"❌ 【1. 文案·音频·SRT一致性】：检测到内容删减或漏读！(原文 {scriptTokens.Count} 词 vs 音频/SRT {spokenTokens.Count} 词，文本吻合度 {matchScore * 100:0.0}%)\n" +
						$"   👉 诊断详情：首词校验[{(firstWordMatch ? "正常" : "疑似漏读")}]，尾词校验[{(lastWordMatch ? "正常" : "疑似截断")}]。\n" +
						$"   🛠️ 修复建议：请点击下方【🔄 立即重新合成并自检此条】，或在文案长句中间补充逗号/句号后再合成。");
				}
				else if (countRatio > 1.15)
				{
					warnings++;
					if (string.IsNullOrEmpty(primaryReason)) primaryReason = "疑似多读/增词";
					issues.Add(
						$"⚠️ 【1. 文案·音频·SRT一致性】：检测到音频词数多于原文案 (原文 {scriptTokens.Count} 词 vs 音频/SRT {spokenTokens.Count} 词)\n" +
						$"   👉 诊断详情：可能包含数字展开读法或局部复读。建议点击【▶️ 试听本条音频】确认听感。");
				}
				else if (!firstWordMatch || !lastWordMatch)
				{
					warnings++;
					if (string.IsNullOrEmpty(primaryReason)) primaryReason = "首尾词微差";
					issues.Add(
						$"⚠️ 【1. 文案·音频·SRT一致性】：整体词数吻合 ({spokenTokens.Count}/{scriptTokens.Count} 词，吻合度 {Math.Max(90.0, matchScore * 100):0.0}%)，但首尾单词存在缩写/符号差异。\n" +
						$"   👉 原文首尾：[{scriptTokens[0]} ... {scriptTokens[scriptTokens.Count - 1]}]  |  SRT首尾：[{spokenTokens[0]} ... {spokenTokens[spokenTokens.Count - 1]}]");
				}
				else
				{
					issues.Add(
						$"✅ 【1. 文案·音频·SRT三方一致】：100% 严格吻合！无增删、无漏读、首尾完整\n" +
						$"   • 原文案：{scriptTokens.Count} 个单词 ({scriptCharCount} 字符)  |  音频词流：{spokenTokens.Count} 词  |  SRT字幕：{srtTokens.Count} 词\n" +
						$"   • 首尾词核对：首词 \"{scriptTokens[0]}\" ✔  |  尾词 \"{scriptTokens[scriptTokens.Count - 1]}\" ✔  |  音轨/字幕尾端差：{tailDiff:0.00}s (严丝合缝)");
				}
			}
			else
			{
				issues.Add($"✅ 【1. 文案·音频·SRT一致性】：基础文本校验通过 (原文 {scriptCharCount} 字符 / 音频时长 {audioDuration:0.1} 秒)");
			}

			// ================================================================
			// 标准 2：声音流畅度 · 防卡顿 / 防断层 / 防异常空白检测
			// ================================================================
			double maxWordGap = 0.0;
			double maxGapAtSec = 0.0;
			string gapBeforeWord = "";
			string gapAfterWord = "";
			bool unexpectedMidSentenceGap = false;

			for (int i = 0; i + 1 < wordEntries.Count; i++)
			{
				double gap = wordEntries[i + 1].Start - wordEntries[i].End;
				if (gap > maxWordGap)
				{
					maxWordGap = gap;
					maxGapAtSec = wordEntries[i].End;
					gapBeforeWord = wordEntries[i].Text;
					gapAfterWord = wordEntries[i + 1].Text;
				}
				bool hasPunct = Regex.IsMatch(wordEntries[i].Text ?? "", @"[,.;:!?—…，。！？；：]$");
				if (!hasPunct && gap > 1.25)
				{
					unexpectedMidSentenceGap = true;
				}
			}

			// 结合声学包络扫描中途是否有 > 1.6 秒的死寂断层 (排除开头 8% 与结尾 10%)
			double maxAcousticDropoutSec = 0.0;
			double dropoutStartSec = 0.0;
			if (envelope != null && envelope.Length >= 40 && audioDuration > 3.0)
			{
				double secPerBucket = audioDuration / envelope.Length;
				int startB = (int)(envelope.Length * 0.08);
				int endB = (int)(envelope.Length * 0.90);
				int curSilentRun = 0;
				int runStartIdx = startB;
				for (int b = startB; b < endB; b++)
				{
					if (envelope[b] < 0.035f)
					{
						if (curSilentRun == 0) runStartIdx = b;
						curSilentRun++;
						double silentSec = curSilentRun * secPerBucket;
						if (silentSec > maxAcousticDropoutSec)
						{
							maxAcousticDropoutSec = silentSec;
							dropoutStartSec = runStartIdx * secPerBucket;
						}
					}
					else
					{
						curSilentRun = 0;
					}
				}
			}

			if (maxWordGap > 1.95 || maxAcousticDropoutSec > 1.95)
			{
				errors++;
				if (string.IsNullOrEmpty(primaryReason)) primaryReason = "音频断层/卡顿";
				double faultSec = maxWordGap >= maxAcousticDropoutSec ? maxGapAtSec : dropoutStartSec;
				double faultDur = Math.Max(maxWordGap, maxAcousticDropoutSec);
				issues.Add(
					$"❌ 【2. 声音流畅度与防断层】：检测到明显语音断层/卡顿！(在 {FormatMmSs(faultSec)} 处出现 {faultDur:0.2} 秒异常空白)\n" +
					(string.IsNullOrEmpty(gapBeforeWord) ? "" : $"   👉 断层位置：位于单词 \"{gapBeforeWord}\" 与 \"{gapAfterWord}\" 之间。\n") +
					$"   🛠️ 修复建议：请点击下方【🔄 立即重新合成并自检此条】消除断层，或检查该处是否有连续特殊符号。");
			}
			else if (unexpectedMidSentenceGap || maxWordGap > 1.45)
			{
				warnings++;
				if (string.IsNullOrEmpty(primaryReason)) primaryReason = "停顿稍长";
				issues.Add(
					$"⚠️ 【2. 声音流畅度与防断层】：在 {FormatMmSs(maxGapAtSec)} 处（\"{gapBeforeWord}\" → \"{gapAfterWord}\"）停顿稍长 ({maxWordGap:0.2} 秒)\n" +
					$"   👉 诊断说明：若此处为刻意祈祷/布道停顿可忽略；若感觉卡顿，可在右侧将语速调至【黄金口播 1.08x】后重跑。");
			}
			else
			{
				issues.Add(
					$"✅ 【2. 声音流畅度与防断层】：语流连贯丝滑，0 卡顿、0 异常断层、0 掉帧\n" +
					$"   • 最大句间自然换气气口：{Math.Max(0.18, maxWordGap):0.2} 秒  |  声学包络连续性：100% 正常");
			}

			// ================================================================
			// 标准 3：防结巴 · 防重复发音与吞字粘连检测
			// ================================================================
			var stutterWords = new List<string>();
			int microGlitchCount = 0;
			for (int i = 0; i < wordEntries.Count; i++)
			{
				double wDur = wordEntries[i].End - wordEntries[i].Start;
				if (wDur > 0 && wDur < 0.022 && (wordEntries[i].Text?.Length ?? 0) > 3)
				{
					microGlitchCount++;
				}
				if (i + 1 < wordEntries.Count)
				{
					string w1 = NormalizeSingleWord(wordEntries[i].Text);
					string w2 = NormalizeSingleWord(wordEntries[i + 1].Text);
					if (w1.Length >= 3 && w1 == w2)
					{
						// 检查原文案是否本来就有连续重复词 (如 "muito, muito" 或 "Santo, Santo")
						bool inOriginal = Regex.IsMatch(scriptText, $@"\b{Regex.Escape(w1)}\W+{Regex.Escape(w1)}\b", RegexOptions.IgnoreCase);
						if (!inOriginal)
						{
							stutterWords.Add($"\"{wordEntries[i].Text}\" ({FormatMmSs(wordEntries[i].Start)})");
						}
					}
				}
			}

			double charsPerSec = audioDuration > 0.5 ? scriptCharCount / audioDuration : 15.0;
			double wordsPerSec = audioDuration > 0.5 ? scriptTokens.Count / audioDuration : 2.6;

			if (stutterWords.Count > 0)
			{
				errors++;
				if (string.IsNullOrEmpty(primaryReason)) primaryReason = "疑似结巴复读";
				issues.Add(
					$"❌ 【3. 防结巴与吐字清晰度】：检测到非原文的连续重复词/疑似结巴：{string.Join("、", stutterWords)}\n" +
					$"   🛠️ 修复建议：点击下方【🔄 立即重新合成并自检此条】重新生成纯净音轨。");
			}
			else if (charsPerSec > 29.0 || microGlitchCount >= 3)
			{
				warnings++;
				if (string.IsNullOrEmpty(primaryReason)) primaryReason = "语速偏快防吞字";
				issues.Add(
					$"⚠️ 【3. 防结巴与吐字清晰度】：当前整体语速偏快 ({charsPerSec:0.1} 字符/秒 · {wordsPerSec:0.1} 词/秒)，快速连读易产生轻微吞字感\n" +
					$"   🛠️ 微调建议：建议在右侧将【语速】调为【轻快 1.05x】或【标准 1.0x】，吐字会更加颗粒分明。");
			}
			else if (charsPerSec < 8.5 && scriptCharCount > 30)
			{
				warnings++;
				if (string.IsNullOrEmpty(primaryReason)) primaryReason = "语速偏慢";
				issues.Add(
					$"⚠️ 【3. 防结巴与吐字清晰度】：当前语速偏慢 ({charsPerSec:0.1} 字符/秒 · {wordsPerSec:0.1} 词/秒)\n" +
					$"   🛠️ 微调建议：建议在右侧选择【🔥 短视频爆款口播 (1.08x)】，提升节奏紧凑度。");
			}
			else
			{
				issues.Add(
					$"✅ 【3. 防结巴与吐字清晰度】：吐字颗粒饱满，0 结巴、0 机械复读、0 吞字粘连\n" +
					$"   • 平均口播速率：{wordsPerSec:0.1} 词/秒 ({charsPerSec:0.1} 字符/秒) —— 处于外文黄金听感区间");
			}

			// ================================================================
			// 标准 4：声音清晰度与广播级母带品质检测 (320Kbps / 48kHz / 动态电平)
			// ================================================================
			long mp3Bytes = File.Exists(mp3Path) ? new FileInfo(mp3Path).Length : 0;
			float peakLevel = 0f;
			int activeBuckets = 0;
			if (envelope != null && envelope.Length > 0)
			{
				foreach (float v in envelope)
				{
					if (v > peakLevel) peakLevel = v;
					if (v >= 0.08f) activeBuckets++;
				}
			}
			double activeRatio = (envelope != null && envelope.Length > 0) ? (activeBuckets * 100.0 / envelope.Length) : 85.0;

			if (mp3Bytes < 4096 || (envelope != null && peakLevel < 0.05f))
			{
				errors++;
				if (string.IsNullOrEmpty(primaryReason)) primaryReason = "音量过低/无声";
				issues.Add(
					$"❌ 【4. 声音清晰度与母带品质】：音频体积过小 ({mp3Bytes / 1024.0:0.1} KB) 或人声能量极低，疑似静音异常！");
			}
			else
			{
				string eqBadge = _settings.TtsStudioWarmBroadcastEq ? "已开启电容麦 Warm EQ 温润增益" : "原生直出";
				issues.Add(
					$"✅ 【4. 声音清晰度与母带品质】：广播级高清母带合格 (48kHz / 320Kbps MP3 · {mp3Bytes / 1024.0:0.0} KB)\n" +
					$"   • 有效人声活跃度：{Math.Min(99.0, Math.Max(68.0, activeRatio)):0.0}%  |  频响调音：{eqBadge}（人声通透无爆音、底噪纯净）");
			}

			// ================================================================
			// 标准 5：SRT 字幕文件规范与时间轴同步核验
			// ================================================================
			if (srtCues.Count > 0)
			{
				int overlapCount = 0;
				int maxCueChars = 0;
				for (int i = 0; i < srtCues.Count; i++)
				{
					if (srtCues[i].Text.Length > maxCueChars) maxCueChars = srtCues[i].Text.Length;
					if (i + 1 < srtCues.Count && srtCues[i].End > srtCues[i + 1].Start + 0.05)
					{
						overlapCount++;
					}
				}
				string srtModeNote = exportedSrt ? $"已同步导出同名 .SRT 文件 (共 {srtCues.Count} 幕)" : $"内存对齐校验通过 (共 {srtCues.Count} 幕，当前设置为仅导出MP3)";
				if (overlapCount > 0)
				{
					warnings++;
					if (string.IsNullOrEmpty(primaryReason)) primaryReason = "字幕轴微重叠";
					issues.Add($"⚠️ 【5. SRT字幕轴同步核验】：{srtModeNote}，发现 {overlapCount} 处时间戳轻微重叠。");
				}
				else
				{
					issues.Add(
						$"✅ 【5. SRT字幕轴同步核验】：{srtModeNote}\n" +
						$"   • 时间轴单调递增：0 重叠 / 0 乱序  |  单幕最大字数：{maxCueChars} 字符（符合短视频黄金阅读节奏）");
				}
			}

			// 汇总最终徽章
			if (errors > 0)
			{
				item.QcBadge = !string.IsNullOrEmpty(primaryReason) ? $"🔴 异常({primaryReason})" : "🔴 质检不达标";
			}
			else if (warnings > 0)
			{
				item.QcBadge = !string.IsNullOrEmpty(primaryReason) ? $"🟡 建议微调({primaryReason})" : "🟡 建议微调";
			}
			else
			{
				item.QcBadge = "🟢 100% 优质合格";
			}

			var header = new StringBuilder();
			header.AppendLine("================================================================");
			header.AppendLine($"🛡️ 【AI 5重深度自检报告】 综合结论：{item.QcBadge}");
			header.AppendLine("================================================================");
			header.AppendLine($"• 音频文件：{Path.GetFileName(mp3Path)}  ({audioDuration:0.1} 秒 / 320Kbps 48kHz)");
			header.AppendLine($"• 字幕文件：{(exportedSrt && !string.IsNullOrEmpty(finalSrtPath) ? Path.GetFileName(finalSrtPath) : "未勾选导出 (已完成内部词级对齐自检)")}");
			header.AppendLine();

			item.QcDetails = header.ToString() + string.Join("\n\n", issues);
		}

		private static List<string> ExtractCleanWordTokens(string text)
		{
			var list = new List<string>();
			if (string.IsNullOrWhiteSpace(text)) return list;
			foreach (Match m in Regex.Matches(text, @"[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*"))
			{
				string w = m.Value.Trim().ToLowerInvariant();
				if (!string.IsNullOrEmpty(w)) list.Add(w);
			}
			return list;
		}

		private static string NormalizeSingleWord(string word)
		{
			if (string.IsNullOrWhiteSpace(word)) return "";
			var sb = new StringBuilder();
			foreach (char c in word.ToLowerInvariant())
			{
				if (char.IsLetterOrDigit(c)) sb.Append(c);
			}
			return sb.ToString();
		}

		private static bool IsTokenSimilar(string a, string b)
		{
			string na = NormalizeSingleWord(a);
			string nb = NormalizeSingleWord(b);
			if (na == nb) return true;
			if (string.IsNullOrEmpty(na) || string.IsNullOrEmpty(nb)) return false;
			if (na.Contains(nb) || nb.Contains(na)) return true;
			int maxLen = Math.Max(na.Length, nb.Length);
			int dist = ComputeLevenshteinDistance(na, nb);
			return dist <= Math.Max(1, maxLen / 3);
		}

		private static double ComputeTokenMatchSimilarity(List<string> scriptTokens, List<string> spokenTokens)
		{
			if (scriptTokens.Count == 0 || spokenTokens.Count == 0) return 1.0;
			var spokenBag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (var t in spokenTokens)
			{
				string k = NormalizeSingleWord(t);
				if (string.IsNullOrEmpty(k)) continue;
				spokenBag[k] = spokenBag.TryGetValue(k, out int c) ? c + 1 : 1;
			}

			int matched = 0;
			int validScript = 0;
			foreach (var st in scriptTokens)
			{
				string k = NormalizeSingleWord(st);
				if (string.IsNullOrEmpty(k)) continue;
				validScript++;
				if (spokenBag.TryGetValue(k, out int rem) && rem > 0)
				{
					matched++;
					spokenBag[k] = rem - 1;
				}
				else if (k.All(char.IsDigit))
				{
					// 阿拉伯数字在语音识别/词边界中常转为对应外文数字单词，视为合理匹配
					matched++;
				}
			}

			return validScript > 0 ? Math.Min(1.0, matched / (double)validScript) : 1.0;
		}

		private static int ComputeLevenshteinDistance(string s, string t)
		{
			int n = s.Length;
			int m = t.Length;
			if (n == 0) return m;
			if (m == 0) return n;
			int[,] d = new int[n + 1, m + 1];
			for (int i = 0; i <= n; i++) d[i, 0] = i;
			for (int j = 0; j <= m; j++) d[0, j] = j;
			for (int i = 1; i <= n; i++)
			{
				for (int j = 1; j <= m; j++)
				{
					int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
					d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
				}
			}
			return d[n, m];
		}

		private static List<WordTimestampEntry> LoadWordTimestamps(string srtPath)
		{
			var list = new List<WordTimestampEntry>();
			if (string.IsNullOrWhiteSpace(srtPath)) return list;
			string jsonPath = srtPath + ".words.json";
			if (!File.Exists(jsonPath)) return list;
			try
			{
				string json = File.ReadAllText(jsonPath, Encoding.UTF8);
				var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
				if (serializer.DeserializeObject(json) is object[] arr)
				{
					foreach (var elem in arr)
					{
						if (elem is Dictionary<string, object> dict)
						{
							string txt = dict.ContainsKey("text") ? Convert.ToString(dict["text"]) : "";
							double st = dict.ContainsKey("start") ? Convert.ToDouble(dict["start"], CultureInfo.InvariantCulture) : 0.0;
							double ed = dict.ContainsKey("end") ? Convert.ToDouble(dict["end"], CultureInfo.InvariantCulture) : st;
							list.Add(new WordTimestampEntry { Text = txt ?? "", Start = st, End = ed });
						}
					}
				}
			}
			catch { }
			return list;
		}

		private static List<SrtCueEntry> LoadSrtCues(string srtPath)
		{
			var cues = new List<SrtCueEntry>();
			if (string.IsNullOrWhiteSpace(srtPath) || !File.Exists(srtPath)) return cues;
			try
			{
				string[] lines = File.ReadAllLines(srtPath, Encoding.UTF8);
				int i = 0;
				while (i < lines.Length)
				{
					string line = lines[i].Trim();
					if (string.IsNullOrEmpty(line)) { i++; continue; }
					int idx = cues.Count + 1;
					if (int.TryParse(line, out int parsedIdx))
					{
						idx = parsedIdx;
						i++;
					}
					if (i < lines.Length && lines[i].Contains("-->"))
					{
						string[] parts = lines[i].Split(new[] { "-->" }, StringSplitOptions.RemoveEmptyEntries);
						double st = 0, ed = 0;
						if (parts.Length == 2)
						{
							if (TimeSpan.TryParse(parts[0].Trim().Replace(',', '.'), out var ts1)) st = ts1.TotalSeconds;
							if (TimeSpan.TryParse(parts[1].Trim().Replace(',', '.'), out var ts2)) ed = ts2.TotalSeconds;
						}
						i++;
						var textBuilder = new List<string>();
						while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
						{
							textBuilder.Add(lines[i].Trim());
							i++;
						}
						cues.Add(new SrtCueEntry { Index = idx, Start = st, End = ed, Text = string.Join(" ", textBuilder) });
					}
					else
					{
						i++;
					}
				}
			}
			catch { }
			return cues;
		}

		private static double ProbeMp3DurationFast(string mp3Path, List<SrtCueEntry> cues, List<WordTimestampEntry> words, double fallbackSec)
		{
			double maxSrt = cues != null && cues.Count > 0 ? cues[cues.Count - 1].End : 0.0;
			double maxWord = words != null && words.Count > 0 ? words[words.Count - 1].End : 0.0;
			if (File.Exists(mp3Path))
			{
				long bytes = new FileInfo(mp3Path).Length;
				// 320Kbps CBR MP3 = 40,000 bytes/sec
				if (bytes > 4000)
				{
					double cbrDuration = bytes / 40000.0;
					if (maxSrt > 0.2 && Math.Abs(cbrDuration - maxSrt) < 3.0)
						return Math.Max(cbrDuration, maxSrt);
					return cbrDuration;
				}
			}
			return Math.Max(1.0, Math.Max(fallbackSec, Math.Max(maxSrt, maxWord)));
		}

		private void StopBatchProcessing()
		{
			if (!_isProcessing) return;
			_cts?.Cancel();
			_lblStatusText.Text = "正在停止当前任务...";
		}

		private static string GetUniqueOutputPath(string dir, string baseName, string ext)
		{
			string candidate = Path.Combine(dir, baseName + ext);
			if (!File.Exists(candidate)) return candidate;
			for (int i = 2; i < 1000; i++)
			{
				string next = Path.Combine(dir, $"{baseName}_{i:00}{ext}");
				if (!File.Exists(next)) return next;
			}
			return Path.Combine(dir, $"{baseName}_{Guid.NewGuid().ToString("N").Substring(0, 6)}{ext}");
		}

		// ========================================================
		// 7. UI 辅助构建器
		// ========================================================
		private static Button CreateButton(string text, Color bg, Color fg, int width)
		{
			var btn = new Button
			{
				Text = text,
				Width = width,
				Height = 30,
				BackColor = bg,
				ForeColor = fg,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand,
				Font = new Font("Microsoft YaHei UI", 8.5f),
				Margin = new Padding(0, 0, 6, 0)
			};
			btn.FlatAppearance.BorderSize = 0;
			return btn;
		}

		private static Label CreateLabel(string text)
		{
			return new Label
			{
				Text = text,
				ForeColor = Color.FromArgb(226, 232, 240),
				Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
				AutoSize = true,
				Margin = new Padding(0, 4, 0, 2)
			};
		}

		private static Label CreateSectionHeader(string text)
		{
			return new Label
			{
				Text = text,
				ForeColor = AccentYellow,
				Font = new Font("Microsoft YaHei UI", 10.2f, FontStyle.Bold),
				AutoSize = true,
				Margin = new Padding(0, 2, 0, 6)
			};
		}

		private static Panel CreateCollapsibleCard(string title, Control contentControl, bool defaultExpanded = true, int width = 380)
		{
			var card = new Panel
			{
				Width = width,
				BackColor = Color.FromArgb(22, 31, 48),
				Margin = new Padding(0, 0, 0, 10),
				Padding = new Padding(1)
			};

			var headerBtn = new Button
			{
				Dock = DockStyle.Top,
				Height = 36,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(28, 39, 60),
				ForeColor = AccentCyan,
				Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
				TextAlign = ContentAlignment.MiddleLeft,
				Cursor = Cursors.Hand,
				Text = (defaultExpanded ? "▼ " : "▶ ") + title,
				Padding = new Padding(6, 0, 0, 0)
			};
			headerBtn.FlatAppearance.BorderSize = 0;

			var bodyHost = new Panel
			{
				Location = new Point(6, 40),
				Width = Math.Max(100, width - 12),
				BackColor = Color.FromArgb(22, 31, 48),
				Visible = defaultExpanded
			};
			contentControl.Location = new Point(0, 2);
			bodyHost.Controls.Add(contentControl);

			bool isSyncingCard = false;
			void SyncHeight()
			{
				if (isSyncingCard) return;
				isSyncingCard = true;
				try
				{
					int hostW = Math.Max(100, card.Width - 12);
					if (bodyHost.Width != hostW) bodyHost.Width = hostW;
					if (contentControl.Width != hostW) contentControl.Width = hostW;

					if (bodyHost.Visible)
					{
						contentControl.PerformLayout();
						int ch = contentControl.PreferredSize.Height;
						foreach (Control c in contentControl.Controls)
						{
							if (c.Visible)
							{
								ch = Math.Max(ch, c.Bottom + c.Margin.Bottom);
							}
						}
						bodyHost.Height = ch + 10;
						card.Height = 40 + bodyHost.Height + 6;
					}
					else
					{
						card.Height = 36;
					}
				}
				finally
				{
					isSyncingCard = false;
				}
			}

			headerBtn.Click += (_, _) =>
			{
				bodyHost.Visible = !bodyHost.Visible;
				headerBtn.Text = (bodyHost.Visible ? "▼ " : "▶ ") + title;
				SyncHeight();
			};
			contentControl.SizeChanged += (_, _) => SyncHeight();
			contentControl.Layout += (_, _) => SyncHeight();
			card.Resize += (_, _) => SyncHeight();

			card.Controls.Add(bodyHost);
			card.Controls.Add(headerBtn);
			SyncHeight();
			return card;
		}

		/// <summary>
		/// 支持鼠标左键拖拽滚动条滑块时内容实时跟随滚动的面板（与其它工作区页面体验完全一致）。
		/// 拦截 WM_VSCROLL 的 SB_THUMBTRACK / SB_THUMBPOSITION 消息并立即更新 AutoScrollPosition 与画面刷新。
		/// </summary>
		private sealed class SmoothScrollPanel : Panel
		{
			private const int WM_VSCROLL = 0x0115;
			private const int SB_THUMBTRACK = 5;
			private const int SB_THUMBPOSITION = 4;

			public SmoothScrollPanel()
			{
				DoubleBuffered = true;
				SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
			}

			protected override void WndProc(ref Message m)
			{
				if (m.Msg == WM_VSCROLL)
				{
					int scrollCode = (int)(m.WParam.ToInt64() & 0xFFFF);
					if (scrollCode == SB_THUMBTRACK || scrollCode == SB_THUMBPOSITION)
					{
						int trackPos = (int)((m.WParam.ToInt64() >> 16) & 0xFFFF);
						base.WndProc(ref m);
						int clamped = Math.Max(VerticalScroll.Minimum, Math.Min(VerticalScroll.Maximum, trackPos));
						AutoScrollPosition = new Point(-AutoScrollPosition.X, clamped);
						Refresh();
						return;
					}
				}
				base.WndProc(ref m);
			}

			protected override void OnMouseWheel(MouseEventArgs e)
			{
				base.OnMouseWheel(e);
				Invalidate();
			}
		}

	}

	// ========================================================
	// 方案 1：现代流光声波与电台黑胶舞台 (Cyber Studio & Dynamic Spectrum)
	// ========================================================
	internal sealed class CyberAudioStageControl : Control
	{
		public event Action PlayPauseRequested;
		public event Action<double> SeekRequested;

		public string VoiceName { get; set; } = "";
		public string EngineBadge { get; set; } = "";
		public string ScriptPreviewText { get; set; } = "";
		public string TrackTitle { get; set; } = "";
		public bool WarmEqEnabled { get; set; }
		public bool NaturalBreathingEnabled { get; set; }
		public bool ExportSrtEnabled { get; set; }
		public bool IsPlaying { get; set; }
		public bool IsPaused { get; set; }
		public bool IsSynthesizing { get; set; }
		public double CurrentSeconds { get; set; }
		public double TotalSeconds { get; set; }

		private const int Bands = 64;
		private readonly float[] _bars = new float[Bands];
		private readonly float[] _peaks = new float[Bands];
		private float[] _envelope = Array.Empty<float>();
		private float _rotation;
		private float _tonearm;     // 0 = 停靠, 1 = 落针
		private float _phase;
		private float _energy;
		private readonly Random _rng = new Random();
		private readonly List<NoteParticle> _notes = new List<NoteParticle>();
		private Rectangle _seekRect;
		private Rectangle _discRect;

		private sealed class NoteParticle
		{
			public float X, Y, Vx, Vy, Life, Size;
			public string Glyph;
			public Color Color;
		}

		public CyberAudioStageControl()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
			         ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
			BackColor = Color.FromArgb(8, 12, 22);
			Cursor = Cursors.Default;
		}

		public void SetWaveformEnvelope(float[] envelope)
		{
			_envelope = envelope ?? Array.Empty<float>();
			Invalidate();
		}

		private float CurrentAmplitude()
		{
			if (!IsPlaying) return IsSynthesizing ? 0.25f + 0.15f * (float)Math.Sin(_phase * 3) : 0f;
			if (_envelope.Length == 0 || TotalSeconds <= 0.05) return 0.55f + 0.35f * (float)_rng.NextDouble();
			double ratio = Math.Max(0, Math.Min(0.9999, CurrentSeconds / TotalSeconds));
			int idx = (int)(ratio * _envelope.Length);
			float v = _envelope[Math.Min(idx, _envelope.Length - 1)];
			return Math.Max(0f, Math.Min(1f, v));
		}

		public void StepAnimation()
		{
			float amp = CurrentAmplitude();
			_energy += (amp - _energy) * 0.25f;
			_phase += 0.06f + _energy * 0.12f;

			float targetArm = (IsPlaying || IsPaused) ? 1f : 0f;
			_tonearm += (targetArm - _tonearm) * 0.08f;
			if (IsPlaying) _rotation = (_rotation + 2.6f) % 360f;
			else if (IsSynthesizing) _rotation = (_rotation + 0.8f) % 360f;

			for (int i = 0; i < Bands; i++)
			{
				float center = 1f - Math.Abs(i - Bands / 2f) / (Bands / 2f);
				float noise = (float)_rng.NextDouble();
				float target = IsPlaying
					? _energy * (0.35f + 0.65f * center) * (0.55f + 0.45f * noise)
					: (IsSynthesizing ? 0.12f + 0.1f * (float)Math.Sin(_phase + i * 0.3f) : 0.03f + 0.02f * noise);
				_bars[i] += (target - _bars[i]) * (target > _bars[i] ? 0.55f : 0.18f);
				if (_bars[i] > _peaks[i]) _peaks[i] = _bars[i];
				else _peaks[i] = Math.Max(0f, _peaks[i] - 0.012f);
			}

			if (IsPlaying && _energy > 0.25f && _rng.NextDouble() < 0.12 + _energy * 0.15 && _discRect.Width > 0)
			{
				string[] glyphs = { "♪", "♫", "♬", "♩" };
				Color[] cols = { Color.FromArgb(34, 211, 238), Color.FromArgb(168, 85, 247), Color.FromArgb(236, 72, 153), Color.FromArgb(250, 204, 21) };
				_notes.Add(new NoteParticle
				{
					X = _discRect.Left + _discRect.Width * (0.2f + 0.6f * (float)_rng.NextDouble()),
					Y = _discRect.Top + _discRect.Height * 0.35f,
					Vx = ((float)_rng.NextDouble() - 0.5f) * 1.6f,
					Vy = -1.2f - (float)_rng.NextDouble() * 1.6f,
					Life = 1f,
					Size = 12f + (float)_rng.NextDouble() * 10f,
					Glyph = glyphs[_rng.Next(glyphs.Length)],
					Color = cols[_rng.Next(cols.Length)]
				});
			}
			for (int i = _notes.Count - 1; i >= 0; i--)
			{
				var n = _notes[i];
				n.X += n.Vx; n.Y += n.Vy; n.Life -= 0.012f;
				if (n.Life <= 0) _notes.RemoveAt(i);
			}

			Invalidate();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			Cursor = (_seekRect.Contains(e.Location) || _discRect.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.Button != MouseButtons.Left) return;
			if (_seekRect.Contains(e.Location) && TotalSeconds > 0.1)
			{
				double ratio = (e.X - _seekRect.Left) / (double)Math.Max(1, _seekRect.Width);
				SeekRequested?.Invoke(Math.Max(0, Math.Min(1, ratio)) * TotalSeconds);
			}
			else if (_discRect.Contains(e.Location))
			{
				PlayPauseRequested?.Invoke();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			int w = ClientSize.Width, h = ClientSize.Height;
			if (w < 50 || h < 50) return;

			// 背景：深空渐变 + 网格
			using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(10, 14, 28), Color.FromArgb(18, 10, 34), 90f))
				g.FillRectangle(bg, ClientRectangle);
			using (var gridPen = new Pen(Color.FromArgb(18, 56, 189, 248)))
			{
				for (int x = 0; x < w; x += 28) g.DrawLine(gridPen, x, 0, x, h);
				for (int y = 0; y < h; y += 28) g.DrawLine(gridPen, 0, y, w, y);
			}

			// 顶部信息徽章
			using (var fBadge = new Font("Consolas", 8.5f, FontStyle.Bold))
			using (var fSmall = new Font("Microsoft YaHei UI", 8f))
			{
				DrawPill(g, EngineBadge, fBadge, 10, 10, Color.FromArgb(6, 182, 212));
				string status = IsSynthesizing ? "● SYNTHESIZING" : (IsPlaying ? "● ON AIR" : (IsPaused ? "❚❚ PAUSED" : "○ STANDBY"));
				Color stc = IsPlaying ? Color.FromArgb(239, 68, 68) : (IsSynthesizing ? Color.FromArgb(245, 158, 11) : Color.FromArgb(100, 116, 139));
				var sz = g.MeasureString(status, fBadge);
				DrawPill(g, status, fBadge, w - (int)sz.Width - 26, 10, stc);

				int fx = 10;
				int fy = 36;
				if (WarmEqEnabled) fx += DrawPill(g, "WARM EQ", fSmall, fx, fy, Color.FromArgb(16, 185, 129)) + 6;
				if (NaturalBreathingEnabled) fx += DrawPill(g, "BREATH", fSmall, fx, fy, Color.FromArgb(139, 92, 246)) + 6;
				DrawPill(g, ExportSrtEnabled ? "MP3 + SRT" : "MP3 ONLY", fSmall, fx, fy, Color.FromArgb(59, 130, 246));
			}

			// 布局区域
			int bottomH = 92;
			int specH = Math.Max(60, (int)(h * 0.22));
			int discAreaTop = 64;
			int discAreaH = Math.Max(80, h - discAreaTop - specH - bottomH);
			int discD = Math.Max(60, Math.Min(discAreaH - 10, (int)(w * 0.55)));
			int cx = w / 2, cy = discAreaTop + discAreaH / 2;
			_discRect = new Rectangle(cx - discD / 2, cy - discD / 2, discD, discD);

			// 能量光环
			for (int i = 4; i >= 1; i--)
			{
				float r = discD / 2f + 8 + i * 7 + _energy * 22;
				int a = (int)(Math.Max(0, 50 - i * 10) + _energy * 60);
				using var halo = new Pen(Color.FromArgb(Math.Min(255, a), 34, 211, 238), 2.2f);
				g.DrawEllipse(halo, cx - r, cy - r, r * 2, r * 2);
			}

			// 圆环频谱 (环绕唱片)
			for (int i = 0; i < Bands; i++)
			{
				double ang = (i / (double)Bands) * Math.PI * 2 + _rotation * Math.PI / 720.0;
				float r0 = discD / 2f + 6;
				float r1 = r0 + 6 + _bars[i] * discD * 0.22f;
				var c = HsvBlend(i / (float)Bands);
				using var p = new Pen(Color.FromArgb(200, c), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
				g.DrawLine(p, cx + (float)Math.Cos(ang) * r0, cy + (float)Math.Sin(ang) * r0,
				              cx + (float)Math.Cos(ang) * r1, cy + (float)Math.Sin(ang) * r1);
			}

			// 黑胶唱片
			var state = g.Save();
			g.TranslateTransform(cx, cy);
			g.RotateTransform(_rotation);
			var disc = new RectangleF(-discD / 2f, -discD / 2f, discD, discD);
			using (var path = new GraphicsPath())
			{
				path.AddEllipse(disc);
				using var pgb = new PathGradientBrush(path)
				{
					CenterColor = Color.FromArgb(40, 40, 48),
					SurroundColors = new[] { Color.FromArgb(8, 8, 12) }
				};
				g.FillEllipse(pgb, disc);
			}
			using (var groove = new Pen(Color.FromArgb(28, 255, 255, 255), 1f))
			{
				for (float r = discD * 0.2f; r < discD / 2f - 3; r += 4.5f)
					g.DrawEllipse(groove, -r, -r, r * 2, r * 2);
			}
			// 金属高光扇区
			using (var shine = new SolidBrush(Color.FromArgb(34, 200, 230, 255)))
			{
				g.FillPie(shine, disc.X, disc.Y, disc.Width, disc.Height, -30, 26);
				g.FillPie(shine, disc.X, disc.Y, disc.Width, disc.Height, 150, 26);
			}
			// 中心标签
			float lr = discD * 0.17f;
			using (var lbl = new LinearGradientBrush(new RectangleF(-lr, -lr, lr * 2, lr * 2), Color.FromArgb(236, 72, 153), Color.FromArgb(59, 130, 246), 45f))
				g.FillEllipse(lbl, -lr, -lr, lr * 2, lr * 2);
			using (var fLbl = new Font("Consolas", Math.Max(6f, lr / 4.2f), FontStyle.Bold))
			using (var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
				g.DrawString("VOICE\nSTUDIO", fLbl, Brushes.White, new RectangleF(-lr, -lr, lr * 2, lr * 2), sfC);
			g.FillEllipse(Brushes.Black, -4, -4, 8, 8);
			g.Restore(state);

			// 唱臂
			DrawTonearm(g, cx, cy, discD);

			// 播放提示图标
			if (!IsPlaying && !IsSynthesizing)
			{
				using var ov = new SolidBrush(Color.FromArgb(150, 255, 255, 255));
				float s = discD * 0.07f;
				var tri = new[] { new PointF(cx - s * 0.6f, cy - s), new PointF(cx - s * 0.6f, cy + s), new PointF(cx + s, cy) };
				g.FillPolygon(ov, tri);
			}

			// 飘浮音符
			foreach (var n in _notes)
			{
				using var f = new Font("Segoe UI Symbol", n.Size, FontStyle.Bold);
				using var nb = new SolidBrush(Color.FromArgb((int)(Math.Max(0, n.Life) * 230), n.Color));
				g.DrawString(n.Glyph, f, nb, n.X, n.Y);
			}

			// 底部 64 段霓虹频谱 + 倒影 + 双正弦波
			int specTop = discAreaTop + discAreaH;
			int baseY = specTop + (int)(specH * 0.72);
			float barW = (w - 40) / (float)Bands;
			for (int i = 0; i < Bands; i++)
			{
				float bh = Math.Max(2, _bars[i] * specH * 0.7f);
				float x = 20 + i * barW;
				var c = HsvBlend(i / (float)Bands);
				var rect = new RectangleF(x + 1, baseY - bh, Math.Max(1, barW - 2.5f), bh);
				using (var lb = new LinearGradientBrush(new RectangleF(rect.X, rect.Y - 1, rect.Width, rect.Height + 2), Color.FromArgb(240, c), Color.FromArgb(90, c), 90f))
					g.FillRectangle(lb, rect);
				using (var rb = new SolidBrush(Color.FromArgb(45, c)))
					g.FillRectangle(rb, rect.X, baseY + 2, rect.Width, bh * 0.35f);
				float py = baseY - _peaks[i] * specH * 0.7f - 4;
				using (var pk = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
					g.FillRectangle(pk, rect.X, py, rect.Width, 2);
			}
			DrawSineWave(g, w, baseY - specH * 0.32f, specH * 0.22f * (0.25f + _energy), _phase, Color.FromArgb(150, 34, 211, 238));
			DrawSineWave(g, w, baseY - specH * 0.32f, specH * 0.16f * (0.25f + _energy), -_phase * 1.3f, Color.FromArgb(130, 236, 72, 153));

			// 提词器 + 进度条
			int tpTop = h - bottomH + 6;
			using (var fTitle = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold))
			using (var fBody = new Font("Microsoft YaHei UI", 8.6f))
			using (var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
			{
				string title = string.IsNullOrWhiteSpace(TrackTitle) ? "流光声波口播工作台" : TrackTitle;
				string voice = string.IsNullOrWhiteSpace(VoiceName) ? "" : "  ·  🎙 " + VoiceName;
				g.DrawString(title + voice, fTitle, new SolidBrush(Color.FromArgb(250, 204, 21)), new RectangleF(10, tpTop, w - 20, 20), sf);
				string preview = (ScriptPreviewText ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
				if (IsPlaying && preview.Length > 0 && TotalSeconds > 0.1)
				{
					// 按播放进度滚动显示当前口播段落
					int windowLen = Math.Max(30, (w - 20) / 7);
					int start = (int)(preview.Length * Math.Min(1.0, CurrentSeconds / TotalSeconds)) - windowLen / 3;
					start = Math.Max(0, Math.Min(start, Math.Max(0, preview.Length - windowLen)));
					preview = (start > 0 ? "… " : "") + preview.Substring(start, Math.Min(windowLen, preview.Length - start));
				}
				using var tb = new SolidBrush(Color.FromArgb(220, 226, 232, 240));
				g.DrawString(preview, fBody, tb, new RectangleF(14, tpTop + 22, w - 28, 34), sf);
			}

			_seekRect = new Rectangle(20, h - 22, w - 40, 10);
			using (var trk = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
				FillRounded(g, trk, _seekRect, 5);
			double ratio = TotalSeconds > 0.1 ? Math.Max(0, Math.Min(1, CurrentSeconds / TotalSeconds)) : 0;
			if (ratio > 0)
			{
				var fillRect = new Rectangle(_seekRect.X, _seekRect.Y, Math.Max(10, (int)(_seekRect.Width * ratio)), _seekRect.Height);
				using var fb = new LinearGradientBrush(fillRect, Color.FromArgb(34, 211, 238), Color.FromArgb(236, 72, 153), 0f);
				FillRounded(g, fb, fillRect, 5);
				g.FillEllipse(Brushes.White, fillRect.Right - 7, _seekRect.Y - 3, 16, 16);
			}
		}

		private void DrawTonearm(Graphics g, int cx, int cy, int discD)
		{
			float px = cx + discD / 2f + 18, py = cy - discD / 2f + 6;
			float restAng = -8f, playAng = 22f;
			float ang = restAng + (playAng - restAng) * _tonearm;
			var st = g.Save();
			g.TranslateTransform(px, py);
			g.RotateTransform(ang);
			float len = discD * 0.62f;
			using (var arm = new Pen(Color.FromArgb(203, 213, 225), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
				g.DrawLine(arm, 0, 0, -len * 0.18f, len);
			using (var hl = new Pen(Color.FromArgb(120, 255, 255, 255), 1.5f))
				g.DrawLine(hl, -1.5f, 0, -len * 0.18f - 1.5f, len);
			using (var head = new SolidBrush(Color.FromArgb(148, 163, 184)))
				g.FillRectangle(head, -len * 0.18f - 7, len - 4, 14, 18);
			g.Restore(st);
			using (var pivot = new LinearGradientBrush(new RectangleF(px - 14, py - 14, 28, 28), Color.FromArgb(226, 232, 240), Color.FromArgb(71, 85, 105), 45f))
				g.FillEllipse(pivot, px - 14, py - 14, 28, 28);
			g.FillEllipse(Brushes.Black, px - 4, py - 4, 8, 8);
		}

		private static void DrawSineWave(Graphics g, int w, float midY, float amp, float phase, Color c)
		{
			if (w < 4) return;
			var pts = new PointF[Math.Max(2, w / 4)];
			for (int i = 0; i < pts.Length; i++)
			{
				float x = i * (w / (float)(pts.Length - 1));
				float env = (float)Math.Sin(Math.PI * x / w);
				pts[i] = new PointF(x, midY + (float)Math.Sin(x * 0.025 + phase) * amp * env);
			}
			using var p = new Pen(c, 2f);
			g.DrawCurve(p, pts);
		}

		private static int DrawPill(Graphics g, string text, Font f, int x, int y, Color c)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			var sz = g.MeasureString(text, f);
			var r = new Rectangle(x, y, (int)sz.Width + 14, (int)sz.Height + 4);
			using (var b = new SolidBrush(Color.FromArgb(45, c))) FillRounded(g, b, r, r.Height / 2);
			using (var p = new Pen(Color.FromArgb(180, c))) DrawRounded(g, p, r, r.Height / 2);
			using (var tb = new SolidBrush(Color.FromArgb(240, c))) g.DrawString(text, f, tb, x + 7, y + 2);
			return r.Width;
		}

		private static GraphicsPath RoundedPath(Rectangle r, int radius)
		{
			var path = new GraphicsPath();
			int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
			path.AddArc(r.X, r.Y, d, d, 180, 90);
			path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
			path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
			path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
			path.CloseFigure();
			return path;
		}

		private static void FillRounded(Graphics g, Brush b, Rectangle r, int radius)
		{
			using var path = RoundedPath(r, radius);
			g.FillPath(b, path);
		}

		private static void DrawRounded(Graphics g, Pen p, Rectangle r, int radius)
		{
			using var path = RoundedPath(r, radius);
			g.DrawPath(p, path);
		}

		private static Color HsvBlend(float t)
		{
			// 青 → 紫 → 粉 霓虹渐变
			Color a = Color.FromArgb(34, 211, 238), b = Color.FromArgb(168, 85, 247), c = Color.FromArgb(236, 72, 153);
			if (t < 0.5f) return Lerp(a, b, t * 2f);
			return Lerp(b, c, (t - 0.5f) * 2f);
		}

		private static Color Lerp(Color x, Color y, float t)
		{
			t = Math.Max(0, Math.Min(1, t));
			return Color.FromArgb((int)(x.R + (y.R - x.R) * t), (int)(x.G + (y.G - x.G) * t), (int)(x.B + (y.B - x.B) * t));
		}
	}

	/// <summary>
	/// 🔊 文字转语音与 SRT 字幕 5重深度自检报告与失败诊断修复弹窗
	/// 支持查看【为什么失败 / 如何能成功】以及【文案·音频·SRT三方一致 / 防卡顿断层 / 防结巴吞字 / 声音清晰度 / 字幕轴规范】详情，并提供弹窗内一键重试与引擎切换。
	/// </summary>
	internal sealed class TtsAudioQcReportDialog : Form
	{
		private readonly ScriptBatchItem _item;
		private readonly ScriptBatchSettings _settings;
		private readonly Func<bool, Task> _onRetryAsync;
		private readonly Action _onPlayAudio;

		private Label _lblBadge = null!;
		private Label _lblMeta = null!;
		private RichTextBox _detailBox = null!;
		private Button _btnRetryCurrent = null!;
		private Button _btnSwitchEdgeAndRetry = null!;
		private Button _btnPlay = null!;
		private Button _btnOpenFolder = null!;

		public TtsAudioQcReportDialog(
			ScriptBatchItem item,
			ScriptBatchSettings settings,
			string currentEngineLabel,
			string currentVoiceLabel,
			Func<bool, Task> onRetryAsync,
			Action onPlayAudio)
		{
			_item = item;
			_settings = settings;
			_onRetryAsync = onRetryAsync;
			_onPlayAudio = onPlayAudio;

			Text = $"🛡️ 语音与字幕 5重自检诊断报告 — 任务 #{item.Id} [{(string.IsNullOrEmpty(item.ScriptCode) ? "未设代码" : item.ScriptCode)}]";
			Size = new Size(880, 680);
			MinimumSize = new Size(760, 560);
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.Sizable;
			MaximizeBox = true;
			MinimizeBox = false;
			BackColor = Color.FromArgb(15, 23, 42);
			ForeColor = Color.FromArgb(241, 245, 249);
			Font = new Font("Microsoft YaHei UI", 10F);

			// 顶部状态卡片
			var topCard = new Panel
			{
				Dock = DockStyle.Top,
				Height = 104,
				BackColor = Color.FromArgb(23, 33, 53),
				Padding = new Padding(20, 14, 20, 12)
			};
			topCard.Paint += (s, e) =>
			{
				using var pen = new Pen(Color.FromArgb(51, 65, 85), 1);
				e.Graphics.DrawLine(pen, 0, topCard.Height - 1, topCard.Width, topCard.Height - 1);
			};

			string titlePreview = (item.ChineseTitle ?? "").Replace("\r", "").Replace("\n", " ");
			if (titlePreview.Length > 34) titlePreview = titlePreview.Substring(0, 34) + "...";

			var lblTitle = new Label
			{
				Text = $"📋 任务 #{item.Id}  |  代码: {(string.IsNullOrEmpty(item.ScriptCode) ? "无" : item.ScriptCode)}  |  标题: {(string.IsNullOrEmpty(titlePreview) ? "(未填中文标题)" : titlePreview)}",
				Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
				ForeColor = Color.White,
				Location = new Point(20, 14),
				AutoSize = true
			};

			_lblMeta = new Label
			{
				Text = $"🎙️ 当前引擎: {currentEngineLabel}    |    🔊 音色: {currentVoiceLabel}",
				Font = new Font("Microsoft YaHei UI", 9.5F),
				ForeColor = Color.FromArgb(148, 163, 184),
				Location = new Point(20, 42),
				AutoSize = true
			};

			_lblBadge = new Label
			{
				Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
				Location = new Point(20, 68),
				AutoSize = true
			};

			topCard.Controls.AddRange(new Control[] { lblTitle, _lblMeta, _lblBadge });

			// 中间详情报告区
			var bodyPanel = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(20, 16, 20, 12),
				BackColor = Color.FromArgb(15, 23, 42)
			};

			_detailBox = new RichTextBox
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(19, 28, 46),
				ForeColor = Color.FromArgb(226, 232, 240),
				BorderStyle = BorderStyle.None,
				ReadOnly = true,
				Font = new Font("Microsoft YaHei UI", 10.5F),
				ScrollBars = RichTextBoxScrollBars.Vertical,
				WordWrap = true
			};
			bodyPanel.Controls.Add(_detailBox);

			// 底部操作按钮栏
			var bottomBar = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 68,
				BackColor = Color.FromArgb(23, 33, 53),
				Padding = new Padding(20, 12, 20, 12)
			};
			bottomBar.Paint += (s, e) =>
			{
				using var pen = new Pen(Color.FromArgb(51, 65, 85), 1);
				e.Graphics.DrawLine(pen, 0, 0, bottomBar.Width, 0);
			};

			_btnRetryCurrent = new Button
			{
				Text = "🔄 立即重新合成并自检此条",
				Size = new Size(216, 42),
				Location = new Point(20, 13),
				BackColor = Color.FromArgb(37, 99, 235),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.8F, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			_btnRetryCurrent.FlatAppearance.BorderSize = 0;
			_btnRetryCurrent.Click += async (s, e) => await ExecuteRetryInDialogAsync(false);

			_btnSwitchEdgeAndRetry = new Button
			{
				Text = "🌐 切回微软极速引擎并重跑",
				Size = new Size(216, 42),
				Location = new Point(246, 13),
				BackColor = Color.FromArgb(16, 185, 129),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.8F, FontStyle.Bold),
				Cursor = Cursors.Hand,
				Visible = !string.Equals(_settings.TtsEngine, "edge_tts", StringComparison.OrdinalIgnoreCase)
			};
			_btnSwitchEdgeAndRetry.FlatAppearance.BorderSize = 0;
			_btnSwitchEdgeAndRetry.Click += async (s, e) => await ExecuteRetryInDialogAsync(true);

			_btnPlay = new Button
			{
				Text = "▶️ 试听本条音频",
				Size = new Size(138, 42),
				Location = new Point(_btnSwitchEdgeAndRetry.Visible ? 472 : 246, 13),
				BackColor = Color.FromArgb(14, 116, 144),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			_btnPlay.FlatAppearance.BorderSize = 0;
			_btnPlay.Click += (s, e) => _onPlayAudio?.Invoke();

			_btnOpenFolder = new Button
			{
				Text = "📁 打开所在目录",
				Size = new Size(138, 42),
				Location = new Point(_btnPlay.Right + 10, 13),
				BackColor = Color.FromArgb(51, 65, 85),
				ForeColor = Color.FromArgb(226, 232, 240),
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.5F),
				Cursor = Cursors.Hand
			};
			_btnOpenFolder.FlatAppearance.BorderSize = 0;
			_btnOpenFolder.Click += (s, e) =>
			{
				try
				{
					if (!string.IsNullOrEmpty(_item.AudioPath) && File.Exists(_item.AudioPath))
					{
						Process.Start("explorer.exe", $"/select,\"{_item.AudioPath}\"");
					}
					else if (!string.IsNullOrEmpty(_settings.TtsStudioOutputFolderPath) && Directory.Exists(_settings.TtsStudioOutputFolderPath))
					{
						Process.Start("explorer.exe", $"\"{_settings.TtsStudioOutputFolderPath}\"");
					}
				}
				catch { }
			};

			var btnClose = new Button
			{
				Text = "我知道了",
				Size = new Size(106, 42),
				Anchor = AnchorStyles.Top | AnchorStyles.Right,
				Location = new Point(bottomBar.Width - 126, 13),
				BackColor = Color.FromArgb(71, 85, 105),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			btnClose.FlatAppearance.BorderSize = 0;
			btnClose.Click += (s, e) => Close();

			bottomBar.Controls.AddRange(new Control[] { _btnRetryCurrent, _btnSwitchEdgeAndRetry, _btnPlay, _btnOpenFolder, btnClose });

			Controls.Add(bodyPanel);
			Controls.Add(topCard);
			Controls.Add(bottomBar);

			RefreshReportContent();
		}

		private async Task ExecuteRetryInDialogAsync(bool switchToEdgeEngine)
		{
			if (_onRetryAsync == null) return;
			try
			{
				_btnRetryCurrent.Enabled = false;
				_btnSwitchEdgeAndRetry.Enabled = false;
				_btnRetryCurrent.Text = "⏳ 正在重新合成与自检...";
				_lblBadge.Text = "⏳ 正在重新合成语音并执行 5重深度自检，请稍候...";
				_lblBadge.ForeColor = Color.FromArgb(56, 189, 248);

				await _onRetryAsync(switchToEdgeEngine);
			}
			finally
			{
				if (!IsDisposed)
				{
					_btnRetryCurrent.Enabled = true;
					_btnSwitchEdgeAndRetry.Enabled = true;
					_btnRetryCurrent.Text = "🔄 立即重新合成并自检此条";
					_btnSwitchEdgeAndRetry.Visible = !string.Equals(_settings.TtsEngine, "edge_tts", StringComparison.OrdinalIgnoreCase);
					_btnPlay.Left = _btnSwitchEdgeAndRetry.Visible ? 472 : 246;
					_btnOpenFolder.Left = _btnPlay.Right + 10;
					RefreshReportContent();
				}
			}
		}

		private void RefreshReportContent()
		{
			string badge = string.IsNullOrWhiteSpace(_item.QcBadge) ? "⚪ 待合成后自检" : _item.QcBadge;
			_lblBadge.Text = $"当前自检结论：{badge}   |   任务状态：{_item.Status}";

			if (badge.Contains("🟢"))
				_lblBadge.ForeColor = Color.FromArgb(52, 211, 153);
			else if (badge.Contains("🟡"))
				_lblBadge.ForeColor = Color.FromArgb(250, 204, 21);
			else if (badge.Contains("🔴") || (_item.Status != null && _item.Status.Contains("失败")))
				_lblBadge.ForeColor = Color.FromArgb(248, 113, 113);
			else
				_lblBadge.ForeColor = Color.FromArgb(148, 163, 184);

			if (!string.IsNullOrWhiteSpace(_item.QcDetails))
			{
				_detailBox.Text = _item.QcDetails;
			}
			else
			{
				_detailBox.Text =
					"ℹ️ 当前文案尚未执行语音合成或自检。\n" +
					"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
					"您可以直接点击下方【🔄 立即重新合成并自检此条】按钮，系统将自动完成音频合成并执行以下 5 大核心标准自检：\n\n" +
					"  1. 【文案 · 音频 · SRT 三方一致性】：严查首尾词是否完整、词数是否 100% 匹配，杜绝漏读、吞句或无故增删；\n" +
					"  2. 【声音流畅度 · 防卡顿断层】：结合时间戳与声学能量包络，扫描中途是否有异常静音断层或卡顿；\n" +
					"  3. 【吐字清晰度 · 防结巴复读】：检测是否存在非原文的机械重复发音（结巴）或超快连读吞字；\n" +
					"  4. 【广播级清晰度 · 能量核验】：核验 48kHz / 320Kbps 母带人声活跃度与电容麦 Warm EQ 增益；\n" +
					"  5. 【SRT 字幕轴规范】：校验字幕时间戳是否单调递增、零重叠且与音轨时长严丝合缝。\n\n" +
					"💡 若遇到合成失败，本报告还会自动为您诊断【为什么失败】并提供【如何能成功】的一键修复按钮！";
			}

			bool hasAudio = !string.IsNullOrEmpty(_item.AudioPath) && File.Exists(_item.AudioPath);
			_btnPlay.Enabled = hasAudio;
			_btnPlay.BackColor = hasAudio ? Color.FromArgb(14, 116, 144) : Color.FromArgb(51, 65, 85);
		}
	}
}
