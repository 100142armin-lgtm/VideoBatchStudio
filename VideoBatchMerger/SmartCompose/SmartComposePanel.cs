using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using VideoBatchMerger.ScriptBatch;

namespace VideoBatchMerger.SmartCompose
{
	public sealed class SmartComposePanel : UserControl
	{
		// 修复 WinForms 鼠标拖拽滚动条滑块时内容不实时刷新的经典 Bug
		private sealed class LiveScrollPanel : Panel
		{
			public LiveScrollPanel()
			{
				AutoScroll = true;
				DoubleBuffered = true;
				SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
			}

			protected override void OnHandleCreated(EventArgs e)
			{
				base.OnHandleCreated(e);
				SetScrollState(ScrollStateFullDrag, true);
			}

			protected override void WndProc(ref Message m)
			{
				bool isScroll = m.Msg == 0x0115 || m.Msg == 0x0114; // WM_VSCROLL / WM_HSCROLL
				bool isThumbTrack = isScroll && (m.WParam.ToInt64() & 0xffff) == 5; // SB_THUMBTRACK
				if (isScroll) SetScrollState(ScrollStateFullDrag, true);
				base.WndProc(ref m);
				if (isThumbTrack)
				{
					PerformLayout();
					Update();
				}
			}
		}

		private static readonly Color BgDark = Color.FromArgb(13, 17, 28);
		private static readonly Color CardBg = Color.FromArgb(20, 27, 44);
		private static readonly Color CardHeaderBg = Color.FromArgb(26, 36, 58);
		private static readonly Color CardBorder = Color.FromArgb(38, 52, 80);
		private static readonly Color InputBg = Color.FromArgb(15, 23, 42);
		private static readonly Color TextPrimary = Color.FromArgb(241, 245, 249);
		private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
		private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
		private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
		private static readonly Color AccentAmber = Color.FromArgb(245, 158, 11);
		private static readonly Color AccentRed = Color.FromArgb(239, 68, 68);

		private readonly SmartComposeSettings _settings;
		private ScriptBatchSettings _subtitleSettings;
		private readonly List<SmartComposeTaskItem> _tasks = new List<SmartComposeTaskItem>();
		private readonly List<PlainLogEntry> _logs = new List<PlainLogEntry>();

		private CancellationTokenSource _cts;
		private bool _isRunning;
		private bool _isLoadingUi;
		private readonly Stopwatch _batchClock = new Stopwatch();
		private readonly System.Windows.Forms.Timer _clockTimer;
		private readonly System.Windows.Forms.Timer _watchTimer;
		private DateTime _lastWatchCheckUtc = DateTime.MinValue;
		private bool _isWatchChecking;
		private int _doneCount;
		private int _failCount;
		private PlainLogLevel? _logFilter;

		// Top Radar & Workspace Mode Switch
		private Button _btnModeCloud;
		private Button _btnModeLocal;
		private readonly Label[] _stageBadges = new Label[6];
		private readonly Label[] _stageArrows = new Label[5];
		private Label _lblLiveAction;
		private Label _lblGlobalStats;
		private ProgressBar _prgGlobal;
		private Button _btnStart;
		private Button _btnStop;

		// Left Tabs (Tasks + Plain Logs)
		private TabControl _leftTabs;
		private DataGridView _gridTasks;
		private RichTextBox _rtbLogs;
		private Button _btnFilterAll;
		private Button _btnFilterOk;
		private Button _btnFilterErr;

		// Middle 60FPS Phone Preview (WebView2)
		private WebView2 _webViewPreview;
		private bool _webViewReady;
		private Label _lblPreviewHint;
		private readonly string _previewCacheDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"VideoBatchStudio",
			"SmartComposePreview"
		);
		private string _currentPreviewVideoUrl = string.Empty;
		private string _currentPreviewTaskKey = string.Empty;
		private int _previewLoadSeq;

		// Right Collapsible Cards Container
		private Panel _cardCloudWrapper;
		private Panel _cardLocalWrapper;
		private Label _lblGoogleAuthStatus;
		private TextBox _txtProxy;
		private TextBox _txtSheetUrl;
		private ComboBox _cboSheetTab;
		private ComboBox _cboColScript;
		private ComboBox _cboColSource;
		private ComboBox _cboColOutput;
		private ComboBox _cboColStatus;
		private ComboBox _cboColTaskSeq;
		private ComboBox _cboColTaskName;
		private Button _btnDeleteSelected;
		private Button _btnClearTasks;
		private Button _btnRetryUpload;
		private ComboBox _cboChannelProfile;
		private TextBox _txtTargetDriveFolder;
		private CheckBox _chkAutoUpload;
		private CheckBox _chkAutoWriteBack;
		private CheckBox _chkKeepLocalCopy;
		private CheckBox _chkAutoWatch;
		private NumericUpDown _numWatchMinutes;
		private TextBox _txtLocalInputDir;
		private TextBox _txtLocalOutputDir;

		// 成品质检门禁 Controls
		private CheckBox _chkQcEnabled;
		private CheckBox _chkQcBlockUpload;
		private NumericUpDown _numQcMinMatch;

		// AI Cut & Subtitle Controls
		private ComboBox _cboLang;
		private ComboBox _cboModel;
		private CheckBox _chkRemoveSilence;
		private NumericUpDown _numSilenceDb;
		private NumericUpDown _numMinSilence;
		private CheckBox _chkRemoveFillers;
		private TextBox _txtFillers;
		private CheckBox _chkAlignScript;
		private CheckBox _chkFastSinglePass;
		private CheckBox _chkBurnSubs;
		private Label _lblSubPresetSummary;
		private NumericUpDown _numSubSize;
		private NumericUpDown _numSubPosY;
		private NumericUpDown _numSubOffX;

		// Watermark Controls
		private CheckBox _chkWmEnabled;
		private ComboBox _cboWmType;
		private Panel _pnlWmText;
		private Panel _pnlWmImage;
		private TextBox _txtWmText;
		private NumericUpDown _numWmFontSize;
		private NumericUpDown _numWmTextOpacity;
		private TextBox _txtWmImgPath;
		private NumericUpDown _numWmImgScale;
		private NumericUpDown _numWmImgOpacity;
		private ComboBox _cboWmPos;
		private NumericUpDown _numWmOffX;
		private NumericUpDown _numWmOffY;

		// Audio Loudnorm, Bitrate Preset & BGM Controls
		private CheckBox _chkLoudnorm;
		private ComboBox _cboBitratePreset;
		private CheckBox _chkBgm;
		private TextBox _txtBgmPath;
		private NumericUpDown _numBgmVol;

		public SmartComposePanel()
		{
			_settings = SmartComposeSettings.Load();
			_subtitleSettings = _settings.SubtitleConfig ?? (_settings.SubtitleConfig = ScriptBatchSettings.Load());

			Dock = DockStyle.Fill;
			BackColor = BgDark;
			ForeColor = TextPrimary;
			Font = new Font("Microsoft YaHei UI", 9f);

			_clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
			_clockTimer.Tick += (_, _) => UpdateGlobalStatsLabel();

			_watchTimer = new System.Windows.Forms.Timer { Interval = 30000 };
			_watchTimer.Tick += async (_, _) => await OnAutoWatchTickAsync();
			_watchTimer.Start();

			BuildLayout();
			LoadSettingsToUi();
			HandleCreated += (_, _) => _ = InitWebPreviewAsync();

			AppendPlainLog(new PlainLogEntry
			{
				Level = PlainLogLevel.Info,
				Stage = "系统就绪",
				Title = "【🤖 智能合成】工作区已就绪！支持「☁️ 云端工作区」与「📂 本地工作区」独立切换。",
				PlainCause = "中间 9:16 手机屏支持实时预览与直接鼠标拖拽字幕/水印位置，右侧所有设置均自动永久记忆。"
			});
		}

		private void BuildLayout()
		{
			var root = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				RowCount = 2,
				ColumnCount = 1,
				BackColor = BgDark
			};
			root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110f));
			root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

			root.Controls.Add(BuildTopRadarPanel(), 0, 0);

			// 三栏布局（与文案成片同款：左栏任务与日志 | 中栏 9:16 手机实时预览 | 右栏可折叠参数抽屉）
			var outerSplit = new SplitContainer
			{
				Dock = DockStyle.Fill,
				Orientation = Orientation.Vertical,
				SplitterWidth = 5,
				BackColor = BgDark,
				Panel1MinSize = 0,
				Panel2MinSize = 0
			};
			var rightSplit = new SplitContainer
			{
				Dock = DockStyle.Fill,
				Orientation = Orientation.Vertical,
				SplitterWidth = 5,
				BackColor = BgDark,
				Panel1MinSize = 0,
				Panel2MinSize = 0
			};

			outerSplit.Panel1.BackColor = BgDark;
			outerSplit.Panel2.BackColor = BgDark;
			rightSplit.Panel1.BackColor = BgDark;
			rightSplit.Panel2.BackColor = BgDark;

			outerSplit.Panel1.Controls.Add(BuildLeftWorkspacePanel());
			rightSplit.Panel1.Controls.Add(BuildCenterPreviewPanel());
			rightSplit.Panel2.Controls.Add(BuildRightConfigPanel());
			outerSplit.Panel2.Controls.Add(rightSplit);

			bool layoutInitialized = false;
			void ApplySafeSplitters()
			{
				if (outerSplit.Width < 500) return;
				if (!layoutInitialized)
				{
					int leftW = Math.Max(300, Math.Min((int)(outerSplit.Width * 0.42), (int)(outerSplit.Width * _settings.LayoutLeftRatio)));
					try { outerSplit.SplitterDistance = leftW; } catch { }

					int remainW = Math.Max(300, outerSplit.Width - leftW - outerSplit.SplitterWidth);
					int rightW = Math.Max(310, Math.Min(420, (int)(outerSplit.Width * _settings.LayoutRightRatio)));
					int midW = Math.Max(220, remainW - rightW - rightSplit.SplitterWidth);
					try { rightSplit.SplitterDistance = midW; } catch { }
					layoutInitialized = true;
				}
			}
			outerSplit.SizeChanged += (_, _) => ApplySafeSplitters();
			outerSplit.SplitterMoved += (_, _) =>
			{
				if (layoutInitialized && outerSplit.Width > 500)
				{
					_settings.LayoutLeftRatio = Math.Max(0.22, Math.Min(0.55, (double)outerSplit.SplitterDistance / outerSplit.Width));
					_settings.Save();
				}
			};
			rightSplit.SplitterMoved += (_, _) =>
			{
				if (layoutInitialized && outerSplit.Width > 500)
				{
					int rW = Math.Max(260, rightSplit.Width - rightSplit.SplitterDistance - rightSplit.SplitterWidth);
					_settings.LayoutRightRatio = Math.Max(0.20, Math.Min(0.45, (double)rW / outerSplit.Width));
					_settings.Save();
				}
			};

			root.Controls.Add(outerSplit, 0, 1);
			Controls.Add(root);
		}

		private Panel BuildTopRadarPanel()
		{
			var top = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = CardBg,
				Padding = new Padding(12, 8, 12, 8)
			};

			var row1 = new Panel { Dock = DockStyle.Top, Height = 44 };

			// 左侧：云端 / 本地工作区独立切换按钮
			var modeFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Left,
				Width = 248,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = false,
				Padding = new Padding(0, 4, 0, 0)
			};
			_btnModeCloud = MakeBtn("☁️ 云端工作区", AccentBlue, Color.White, 118, 32);
			_btnModeCloud.Font = new Font("Microsoft YaHei UI", 8.8f, FontStyle.Bold);
			_btnModeCloud.Click += (_, _) => SwitchWorkspaceMode(0);
			_btnModeLocal = MakeBtn("📂 本地工作区", CardBorder, TextMuted, 118, 32);
			_btnModeLocal.Font = new Font("Microsoft YaHei UI", 8.8f, FontStyle.Bold);
			_btnModeLocal.Click += (_, _) => SwitchWorkspaceMode(1);
			modeFlow.Controls.Add(_btnModeCloud);
			modeFlow.Controls.Add(_btnModeLocal);

			// 中间：6 阶段流水灯
			var stagesFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = false,
				Padding = new Padding(8, 6, 0, 0)
			};
			string[] stageNames = { "① 读取表格", "② 下载素材", "③ AI 识别", "④ 剪气口对齐", "⑤ 烧录字幕成片", "⑥ 上传并回填" };
			for (int i = 0; i < stageNames.Length; i++)
			{
				var badge = new Label
				{
					Text = stageNames[i],
					AutoSize = true,
					Padding = new Padding(8, 4, 8, 4),
					Margin = new Padding(0, 0, 4, 0),
					BackColor = Color.FromArgb(30, 41, 59),
					ForeColor = TextMuted,
					Font = new Font("Microsoft YaHei UI", 8.4f, FontStyle.Bold)
				};
				_stageBadges[i] = badge;
				stagesFlow.Controls.Add(badge);
				if (i < stageNames.Length - 1)
				{
					var arrow = new Label
					{
						Text = "→",
						AutoSize = true,
						ForeColor = Color.FromArgb(71, 85, 105),
						Margin = new Padding(0, 5, 4, 0)
					};
					_stageArrows[i] = arrow;
					stagesFlow.Controls.Add(arrow);
				}
			}

			// 右侧：一键启动 & 停止
			var actionFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Right,
				Width = 228,
				FlowDirection = FlowDirection.RightToLeft,
				WrapContents = false,
				Padding = new Padding(0, 4, 0, 0)
			};
			_btnStop = MakeBtn("⏹ 停止", AccentRed, Color.White, 82, 34);
			_btnStop.Enabled = false;
			_btnStop.Click += (_, _) => StopPipeline();
			_btnStart = MakeBtn("▶ 一键启动", AccentGreen, Color.White, 130, 34);
			_btnStart.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
			_btnStart.Click += async (_, _) => await StartPipelineAsync();
			actionFlow.Controls.Add(_btnStop);
			actionFlow.Controls.Add(_btnStart);

			row1.Controls.Add(stagesFlow);
			row1.Controls.Add(modeFlow);
			row1.Controls.Add(actionFlow);

			// Row 2: 实时心跳状态 + 进度条 + 统计
			var row2 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
			_lblLiveAction = new Label
			{
				Text = "🟢 当前状态：空闲待命（右侧参数设置完自动记忆，点【▶ 一键启动】即可工作）",
				Dock = DockStyle.Top,
				Height = 22,
				ForeColor = Color.FromArgb(125, 211, 252),
				Font = new Font("Microsoft YaHei UI", 8.8f, FontStyle.Bold),
				AutoEllipsis = true
			};
			var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 18 };
			_prgGlobal = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Value = 0, Style = ProgressBarStyle.Continuous };
			_lblGlobalStats = new Label
			{
				Text = "总进度: 0/0 | 成功: 0 | 失败: 0 | 耗时: 00:00",
				Dock = DockStyle.Right,
				Width = 300,
				TextAlign = ContentAlignment.MiddleRight,
				ForeColor = TextMuted,
				Font = new Font("Microsoft YaHei UI", 8.4f)
			};
			bottomBar.Controls.Add(_prgGlobal);
			bottomBar.Controls.Add(_lblGlobalStats);
			row2.Controls.Add(_lblLiveAction);
			row2.Controls.Add(bottomBar);

			top.Controls.Add(row2);
			top.Controls.Add(row1);
			return top;
		}

		private Control BuildLeftWorkspacePanel()
		{
			_leftTabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold) };

			var tabTasks = new TabPage("📋 任务队列") { BackColor = BgDark, ForeColor = TextPrimary, Padding = new Padding(6) };
			var taskTool = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, FlowDirection = FlowDirection.LeftToRight };
			var btnRefreshTasks = MakeBtn("🔄 拉取", AccentBlue, Color.White, 76, 30);
			btnRefreshTasks.Click += async (_, _) => await RefreshTasksAsync();
			_btnRetryUpload = MakeBtn("🔁 重试上传", AccentGreen, Color.White, 92, 30);
			_btnRetryUpload.Click += async (_, _) => await RetrySelectedUploadAsync();
			_btnDeleteSelected = MakeBtn("🗑 删除", Color.FromArgb(127, 29, 29), Color.White, 72, 30);
			_btnDeleteSelected.Click += (_, _) => DeleteSelectedTasks();
			_btnClearTasks = MakeBtn("🧹 清空", CardBorder, TextPrimary, 72, 30);
			_btnClearTasks.Click += (_, _) => ClearAllTasks();
			var btnOpenOut = MakeBtn("📂 成品", CardBorder, TextPrimary, 72, 30);
			btnOpenOut.Click += (_, _) =>
			{
				string dir = _txtLocalOutputDir.Text.Trim();
				if (!string.IsNullOrEmpty(dir))
				{
					Directory.CreateDirectory(dir);
					Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
				}
			};
			var btnOpenCache = MakeBtn("🗂 缓存", CardBorder, TextPrimary, 72, 30);
			btnOpenCache.Click += (_, _) =>
			{
				string cacheDir = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"VideoBatchStudio",
					"SmartComposeWork"
				);
				Directory.CreateDirectory(cacheDir);
				Process.Start(new ProcessStartInfo { FileName = cacheDir, UseShellExecute = true });
			};
			taskTool.Controls.Add(btnRefreshTasks);
			taskTool.Controls.Add(_btnRetryUpload);
			taskTool.Controls.Add(_btnDeleteSelected);
			taskTool.Controls.Add(_btnClearTasks);
			taskTool.Controls.Add(btnOpenOut);
			taskTool.Controls.Add(btnOpenCache);

			_gridTasks = new DataGridView
			{
				Dock = DockStyle.Fill,
				BackgroundColor = BgDark,
				GridColor = CardBorder,
				BorderStyle = BorderStyle.None,
				RowHeadersVisible = false,
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				ReadOnly = true,
				MultiSelect = true,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				EnableHeadersVisualStyles = false
			};
			_gridTasks.ColumnHeadersDefaultCellStyle.BackColor = CardBg;
			_gridTasks.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(147, 197, 253);
			_gridTasks.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 8.6f, FontStyle.Bold);
			_gridTasks.ColumnHeadersHeight = 32;
			_gridTasks.DefaultCellStyle.BackColor = Color.FromArgb(17, 24, 39);
			_gridTasks.DefaultCellStyle.ForeColor = TextPrimary;
			_gridTasks.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 64, 110);
			_gridTasks.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 8.5f);
			_gridTasks.RowTemplate.Height = 30;
			_gridTasks.SelectionChanged += (_, _) =>
			{
				PushStateToWebPreview();
				_ = LoadSelectedTaskPreviewVideoAsync();
			};
			_gridTasks.CellClick += OnTaskGridCellClick;
			_gridTasks.CellDoubleClick += OnTaskGridCellDoubleClick;
			_gridTasks.CellMouseEnter += (_, ev) =>
			{
				if (ev.RowIndex >= 0 && ev.RowIndex < _tasks.Count && ev.ColumnIndex == _gridTasks.Columns["Result"].Index)
				{
					var t = _tasks[ev.RowIndex];
					bool clickable = !string.IsNullOrWhiteSpace(t.UploadedDriveUrl) || (!string.IsNullOrWhiteSpace(t.OutputFilePath) && File.Exists(t.OutputFilePath));
					_gridTasks.Cursor = clickable ? Cursors.Hand : Cursors.Default;
				}
				else
				{
					_gridTasks.Cursor = Cursors.Default;
				}
			};
			_gridTasks.CellMouseLeave += (_, _) => _gridTasks.Cursor = Cursors.Default;

			_gridTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Idx", HeaderText = "序号", FillWeight = 8 });
			_gridTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "任务名称", FillWeight = 18 });
			_gridTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Clips", HeaderText = "片段", FillWeight = 8 });
			_gridTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stage", HeaderText = "当前阶段", FillWeight = 18 });
			_gridTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Prog", HeaderText = "进度", FillWeight = 8 });
			_gridTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Trim", HeaderText = "剪气口", FillWeight = 9 });
			_gridTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qc", HeaderText = "成品质检", FillWeight = 16 });
			_gridTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", FillWeight = 11 });
			_gridTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "Result", HeaderText = "成品链接/文件", FillWeight = 18 });

			tabTasks.Controls.Add(_gridTasks);
			tabTasks.Controls.Add(taskTool);

			var tabLogs = new TabPage("📜 运行日志与排障") { BackColor = BgDark, ForeColor = TextPrimary, Padding = new Padding(6) };
			var logTool = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, FlowDirection = FlowDirection.LeftToRight };
			_btnFilterAll = MakeBtn("全部", AccentBlue, Color.White, 62, 28);
			_btnFilterOk = MakeBtn("✅ 成功", CardBorder, TextPrimary, 76, 28);
			_btnFilterErr = MakeBtn("❌ 报错", CardBorder, TextPrimary, 76, 28);
			var btnCopyReport = MakeBtn("📋 复制排障报告", Color.FromArgb(79, 70, 229), Color.White, 128, 28);
			var btnClearLogs = MakeBtn("🗑 清空", CardBorder, TextMuted, 64, 28);

			_btnFilterAll.Click += (_, _) => SetLogFilter(null);
			_btnFilterOk.Click += (_, _) => SetLogFilter(PlainLogLevel.Success);
			_btnFilterErr.Click += (_, _) => SetLogFilter(PlainLogLevel.Error);
			btnCopyReport.Click += (_, _) => CopyDiagnosticReport();
			btnClearLogs.Click += (_, _) => { _logs.Clear(); RenderLogs(); };

			logTool.Controls.Add(_btnFilterAll);
			logTool.Controls.Add(_btnFilterOk);
			logTool.Controls.Add(_btnFilterErr);
			logTool.Controls.Add(btnCopyReport);
			logTool.Controls.Add(btnClearLogs);

			_rtbLogs = new RichTextBox
			{
				Dock = DockStyle.Fill,
				ReadOnly = true,
				BackColor = Color.FromArgb(10, 15, 26),
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Font = new Font("Microsoft YaHei UI", 9.2f),
				WordWrap = true
			};

			tabLogs.Controls.Add(_rtbLogs);
			tabLogs.Controls.Add(logTool);

			_leftTabs.TabPages.Add(tabTasks);
			_leftTabs.TabPages.Add(tabLogs);
			return _leftTabs;
		}

		private Control BuildCenterPreviewPanel()
		{
			var pnl = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(10, 14, 24),
				Padding = new Padding(6)
			};

			var header = new Panel { Dock = DockStyle.Top, Height = 28 };
			_lblPreviewHint = new Label
			{
				Text = "📱 9:16 实时可视化预览（鼠标直接拖拽字幕/水印位置）",
				Dock = DockStyle.Fill,
				ForeColor = Color.FromArgb(147, 197, 253),
				Font = new Font("Microsoft YaHei UI", 8.6f, FontStyle.Bold),
				TextAlign = ContentAlignment.MiddleCenter
			};
			header.Controls.Add(_lblPreviewHint);

			_webViewPreview = new WebView2
			{
				Dock = DockStyle.Fill,
				DefaultBackgroundColor = Color.FromArgb(10, 14, 24)
			};

			pnl.Controls.Add(_webViewPreview);
			pnl.Controls.Add(header);
			return pnl;
		}

		private Control BuildRightConfigPanel()
		{
			var scroll = new LiveScrollPanel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(8, 8, 12, 16),
				BackColor = BgDark
			};

			var stack = new FlowLayoutPanel
			{
				Location = new Point(8, 8),
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				Width = 330,
				BackColor = BgDark
			};
			scroll.SizeChanged += (_, _) =>
			{
				int targetW = Math.Max(290, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 20);
				if (Math.Abs(stack.Width - targetW) > 4)
				{
					stack.SuspendLayout();
					stack.Width = targetW;
					foreach (Control c in stack.Controls)
					{
						c.Width = targetW;
						if (c.Controls.Count > 0) c.Controls[0].Width = targetW;
					}
					stack.ResumeLayout(true);
				}
			};

			// 0. 频道/矩阵方案预设卡片（一键切换整套表格/云盘/字幕/水印配置，团队多号 0 错传）
			var profBody = MakeBodyFlow();
			var profRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 2) };
			_cboChannelProfile = MakeCombo(174);
			_cboChannelProfile.SelectedIndexChanged += (_, _) =>
			{
				if (_isLoadingUi) return;
				ApplySelectedChannelProfile();
			};
			var btnSaveProf = MakeBtn("💾 保存", AccentBlue, Color.White, 64, 26);
			btnSaveProf.Click += (_, _) => SaveCurrentChannelProfile();
			var btnDelProf = MakeBtn("🗑", Color.FromArgb(127, 29, 29), Color.White, 34, 26);
			btnDelProf.Click += (_, _) => DeleteSelectedChannelProfile();
			profRow.Controls.Add(_cboChannelProfile);
			profRow.Controls.Add(btnSaveProf);
			profRow.Controls.Add(btnDelProf);
			profBody.Controls.Add(profRow);
			stack.Controls.Add(CreateCollapsibleCard("🎯 频道/矩阵方案一键切换", profBody, expanded: true));

			// 1A. 云端工作区卡片（仅在云端模式显示）
			var cloudBody = MakeBodyFlow();
			_lblGoogleAuthStatus = new Label { Text = "⚪ 尚未绑定 Google 账号", AutoSize = true, ForeColor = AccentAmber, Margin = new Padding(0, 2, 0, 6) };
			cloudBody.Controls.Add(_lblGoogleAuthStatus);

			var authBtns = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 6) };
			var btnImportSecret = MakeBtn("📄 导入凭据 JSON", CardBorder, TextPrimary, 134, 28);
			btnImportSecret.Click += (_, _) => ImportGoogleClientSecret();
			var btnAuthGoogle = MakeBtn("🔗 绑定 Gmail 账号", AccentBlue, Color.White, 152, 28);
			btnAuthGoogle.Click += async (_, _) => await AuthorizeGoogleAsync();
			authBtns.Controls.Add(btnImportSecret);
			authBtns.Controls.Add(btnAuthGoogle);
			cloudBody.Controls.Add(authBtns);

			_txtProxy = AddLabeledTextBox(cloudBody, "本地代理 (可选，如 http://127.0.0.1:7890):", 295);
			_txtSheetUrl = AddLabeledTextBox(cloudBody, "谷歌任务表链接 (含 #gid 自动定位页签):", 295);

			var sheetRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 2, 0, 6) };
			var btnLoadHeaders = MakeBtn("🔍 读取表头", AccentBlue, Color.White, 112, 26);
			btnLoadHeaders.Click += async (_, _) => await LoadSheetHeadersAsync(preferUrlGid: true);
			_cboSheetTab = MakeCombo(175);
			_cboSheetTab.SelectedIndexChanged += async (_, _) =>
			{
				if (_isLoadingUi) return;
				SaveSettingsFromUi();
				if (_cboSheetTab.SelectedItem != null && !string.IsNullOrWhiteSpace(_txtSheetUrl.Text))
				{
					await LoadSheetHeadersAsync(preferUrlGid: false);
				}
			};
			sheetRow.Controls.Add(btnLoadHeaders);
			sheetRow.Controls.Add(_cboSheetTab);
			cloudBody.Controls.Add(sheetRow);

			_cboColScript = AddLabeledCombo(cloudBody, "标准文案列:", 295);
			_cboColSource = AddLabeledCombo(cloudBody, "素材链接列 (云盘文件夹 / 视频链接):", 295);
			_cboColOutput = AddLabeledCombo(cloudBody, "成品链接回填列:", 295);
			_cboColStatus = AddLabeledCombo(cloudBody, "状态回填列:", 295);
			_cboColTaskSeq = AddLabeledCombo(cloudBody, "任务序号列 (可选):", 295);
			_cboColTaskName = AddLabeledCombo(cloudBody, "任务名称 / 成品命名列 (可选):", 295);

			_txtTargetDriveFolder = AddLabeledTextBox(cloudBody, "云端成品目标文件夹链接:", 295);
			_chkAutoUpload = MakeCheck("完成后自动上传回谷歌云盘", true);
			_chkAutoWriteBack = MakeCheck("自动回填云盘分享链接与状态到表格", true);
			_chkKeepLocalCopy = MakeCheck("云端上传同时在本地保留一份成品", false);
			cloudBody.Controls.Add(_chkAutoUpload);
			cloudBody.Controls.Add(_chkAutoWriteBack);
			cloudBody.Controls.Add(_chkKeepLocalCopy);

			var watchRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 2, 0, 2) };
			_chkAutoWatch = MakeCheck("⏰ 自动值守：每", false);
			_numWatchMinutes = MakeNumeric(3, 120, 10, 0, 50);
			watchRow.Controls.Add(_chkAutoWatch);
			watchRow.Controls.Add(_numWatchMinutes);
			watchRow.Controls.Add(new Label { Text = "分钟巡检新任务", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(2, 4, 0, 0) });
			cloudBody.Controls.Add(watchRow);
			_cardCloudWrapper = CreateCollapsibleCard("☁️ 谷歌表格与云端回填配置", cloudBody, expanded: true);
			stack.Controls.Add(_cardCloudWrapper);

			// 1B. 本地工作区卡片（仅在本地模式显示）
			var localBody = MakeBodyFlow();
			localBody.Controls.Add(new Label
			{
				Text = "选一个总目录（每个子文件夹=1条任务，含10秒片段+文案.txt），或直接选单个素材文件夹：",
				AutoSize = false,
				Width = 295,
				Height = 36,
				ForeColor = TextMuted
			});
			var localInRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			_txtLocalInputDir = MakeTextBox(215);
			_txtLocalInputDir.Leave += (_, _) => SaveSettingsFromUi();
			var btnBrowseIn = MakeBtn("浏览...", AccentBlue, Color.White, 70, 25);
			btnBrowseIn.Click += (_, _) =>
			{
				using var dlg = new FolderBrowserDialog { Description = "选择本地 AI 片段文件夹" };
				if (dlg.ShowDialog(this) == DialogResult.OK)
				{
					_txtLocalInputDir.Text = dlg.SelectedPath;
					SaveSettingsFromUi();
					LoadLocalTasks();
				}
			};
			localInRow.Controls.Add(_txtLocalInputDir);
			localInRow.Controls.Add(btnBrowseIn);
			localBody.Controls.Add(localInRow);
			_cardLocalWrapper = CreateCollapsibleCard("📂 本地素材批量目录", localBody, expanded: true);
			stack.Controls.Add(_cardLocalWrapper);

			// 2. AI 识别与气口精剪卡片
			var cutBody = MakeBodyFlow();
			var langRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 6) };
			langRow.Controls.Add(new Label { Text = "语种:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 4, 4, 0) });
			_cboLang = MakeCombo(105);
			_cboLang.Items.AddRange(new object[] { "auto (自动)", "pt (葡语)", "en (英语)", "zh (中文)", "es (西语)" });
			_cboLang.SelectedIndexChanged += (_, _) => SaveSettingsFromUi();
			langRow.Controls.Add(_cboLang);
			langRow.Controls.Add(new Label { Text = "模型:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(8, 4, 4, 0) });
			_cboModel = MakeCombo(92);
			_cboModel.Items.AddRange(new object[] { "base", "small", "turbo", "large" });
			_cboModel.SelectedIndexChanged += (_, _) => SaveSettingsFromUi();
			langRow.Controls.Add(_cboModel);
			cutBody.Controls.Add(langRow);

			_chkRemoveSilence = MakeCheck("自动剪除首尾空白与长停顿/气口", true);
			cutBody.Controls.Add(_chkRemoveSilence);

			var silRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(8, 0, 0, 6) };
			silRow.Controls.Add(new Label { Text = "阈值dB:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 4, 3, 0) });
			_numSilenceDb = MakeNumeric(-60, -15, -34, 0, 56);
			silRow.Controls.Add(_numSilenceDb);
			silRow.Controls.Add(new Label { Text = "停顿>秒:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(8, 4, 3, 0) });
			_numMinSilence = MakeNumeric(0.15m, 2.0m, 0.35m, 2, 60);
			_numMinSilence.Increment = 0.05m;
			silRow.Controls.Add(_numMinSilence);
			cutBody.Controls.Add(silRow);

			_chkRemoveFillers = MakeCheck("自动挖剪多余语气词/口癖（防爆音）", true);
			cutBody.Controls.Add(_chkRemoveFillers);
			_txtFillers = AddLabeledTextBox(cutBody, "语气词黑名单 (逗号分隔):", 295);
			_chkAlignScript = MakeCheck("按标准文案自动排序片段并强制纠错", true);
			cutBody.Controls.Add(_chkAlignScript);
			_chkFastSinglePass = MakeCheck("极速模式：先剪气口拼接，整片只跑 1 次 AI 识别（提速 2 倍）", true);
			cutBody.Controls.Add(_chkFastSinglePass);
			stack.Controls.Add(CreateCollapsibleCard("✂️ AI 识别与气口精剪", cutBody, expanded: false));

			// 3. 爆款字幕视觉模板卡片
			var subBody = MakeBodyFlow();
			_chkBurnSubs = MakeCheck("烧录【爆款字幕库】动态花字字幕", true);
			subBody.Controls.Add(_chkBurnSubs);
			_lblSubPresetSummary = new Label { Text = "当前预设：...", AutoSize = true, ForeColor = Color.FromArgb(125, 211, 252), Margin = new Padding(0, 2, 0, 6) };
			subBody.Controls.Add(_lblSubPresetSummary);

			var subBtns = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 6) };
			var btnPickPreset = MakeBtn("🎨 爆款字幕预设库", AccentBlue, Color.White, 142, 28);
			btnPickPreset.Click += (_, _) =>
			{
				using var dlg = new SubtitlePresetDialog(_subtitleSettings, OnSubtitleSettingsChanged);
				dlg.ShowDialog(this);
				OnSubtitleSettingsChanged();
			};
			var btnEditStyle = MakeBtn("✎ 自定义参数", CardBorder, TextPrimary, 142, 28);
			btnEditStyle.Click += (_, _) =>
			{
				using var dlg = new SubtitleStyleEditorDialog(_subtitleSettings, OnSubtitleSettingsChanged);
				dlg.ShowDialog(this);
				PersistSubtitleConfig();
				OnSubtitleSettingsChanged();
			};
			subBtns.Controls.Add(btnPickPreset);
			subBtns.Controls.Add(btnEditStyle);
			subBody.Controls.Add(subBtns);

			var subPosRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			subPosRow.Controls.Add(new Label { Text = "字号:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 4, 2, 0) });
			_numSubSize = MakeNumeric(24, 160, 86, 0, 56, () => { _subtitleSettings.SubtitleFontSize = (int)_numSubSize.Value; OnSubtitleSettingsChanged(); });
			subPosRow.Controls.Add(_numSubSize);
			subPosRow.Controls.Add(new Label { Text = "Y%:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(6, 4, 2, 0) });
			_numSubPosY = MakeNumeric(15, 88, 82, 0, 52, () => { _subtitleSettings.SubtitleVerticalPercent = (int)_numSubPosY.Value; OnSubtitleSettingsChanged(); });
			subPosRow.Controls.Add(_numSubPosY);
			subPosRow.Controls.Add(new Label { Text = "X:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(6, 4, 2, 0) });
			_numSubOffX = MakeNumeric(-350, 350, 0, 0, 56, () => { _subtitleSettings.SubtitleOffsetX = (int)_numSubOffX.Value; OnSubtitleSettingsChanged(); });
			subPosRow.Controls.Add(_numSubOffX);
			subBody.Controls.Add(subPosRow);
			stack.Controls.Add(CreateCollapsibleCard("🎨 爆款字幕视觉模板与位置", subBody, expanded: true));

			// 4. 视频水印设置卡片（与文案成片同款文字/图片水印 + 实时拖拽联动）
			var wmBody = MakeBodyFlow();
			_chkWmEnabled = new CheckBox { Text = "启用视频水印叠加", AutoSize = true, ForeColor = TextPrimary, Margin = new Padding(0, 2, 0, 6) };
			_chkWmEnabled.CheckedChanged += (_, _) => { if (!_isLoadingUi) { _subtitleSettings.WatermarkEnabled = _chkWmEnabled.Checked; PersistSubtitleConfig(); PushStateToWebPreview(); } };
			wmBody.Controls.Add(_chkWmEnabled);

			var wmTypeRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 6) };
			wmTypeRow.Controls.Add(new Label { Text = "水印类型:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 4, 4, 0) });
			_cboWmType = MakeCombo(210);
			_cboWmType.Items.AddRange(new object[] { "文字水印", "图片水印 (PNG/JPG)" });
			_cboWmType.SelectedIndexChanged += (_, _) =>
			{
				UpdateWatermarkTypeVisibility();
				if (!_isLoadingUi) { _subtitleSettings.WatermarkType = _cboWmType.SelectedIndex; PersistSubtitleConfig(); PushStateToWebPreview(); }
			};
			wmTypeRow.Controls.Add(_cboWmType);
			wmBody.Controls.Add(wmTypeRow);

			// 文字水印子面板
			_pnlWmText = MakeBodyFlow();
			_pnlWmText.Controls.Add(new Label { Text = "水印文字内容:", AutoSize = true, ForeColor = TextMuted });
			_txtWmText = MakeTextBox(290);
			_txtWmText.TextChanged += (_, _) => { if (!_isLoadingUi) { _subtitleSettings.WatermarkText = _txtWmText.Text; PersistSubtitleConfig(); PushStateToWebPreview(); } };
			_pnlWmText.Controls.Add(_txtWmText);
			var wmTextRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			wmTextRow.Controls.Add(new Label { Text = "字号:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 4, 3, 0) });
			_numWmFontSize = MakeNumeric(16, 72, 28, 0, 58, () => { _subtitleSettings.WatermarkFontSize = (int)_numWmFontSize.Value; PersistSubtitleConfig(); PushStateToWebPreview(); });
			wmTextRow.Controls.Add(_numWmFontSize);
			wmTextRow.Controls.Add(new Label { Text = "不透明度%:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(8, 4, 3, 0) });
			_numWmTextOpacity = MakeNumeric(10, 100, 80, 0, 58, () => { _subtitleSettings.WatermarkTextOpacity = (int)_numWmTextOpacity.Value; PersistSubtitleConfig(); PushStateToWebPreview(); });
			wmTextRow.Controls.Add(_numWmTextOpacity);
			var btnWmColor = MakeBtn("文字色", CardBorder, TextPrimary, 60, 24);
			btnWmColor.Click += (_, _) =>
			{
				using var cd = new ColorDialog { Color = ScriptBatchSettings.ParseToColor(_subtitleSettings.WatermarkTextColorHex, Color.White), FullOpen = true };
				if (cd.ShowDialog(this) == DialogResult.OK)
				{
					_subtitleSettings.WatermarkTextColorHex = ScriptBatchSettings.ToHtmlHex(cd.Color);
					PersistSubtitleConfig();
					PushStateToWebPreview();
				}
			};
			wmTextRow.Controls.Add(btnWmColor);
			_pnlWmText.Controls.Add(wmTextRow);
			wmBody.Controls.Add(_pnlWmText);

			// 图片水印子面板
			_pnlWmImage = MakeBodyFlow();
			var wmImgFileRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			_txtWmImgPath = MakeTextBox(215);
			var btnWmImg = MakeBtn("选图...", CardBorder, TextPrimary, 68, 25);
			btnWmImg.Click += (_, _) =>
			{
				using var dlg = new OpenFileDialog { Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp|所有文件|*.*" };
				if (dlg.ShowDialog(this) == DialogResult.OK)
				{
					_txtWmImgPath.Text = dlg.FileName;
					_subtitleSettings.WatermarkImagePath = dlg.FileName;
					PersistSubtitleConfig();
					PushStateToWebPreview();
				}
			};
			wmImgFileRow.Controls.Add(_txtWmImgPath);
			wmImgFileRow.Controls.Add(btnWmImg);
			_pnlWmImage.Controls.Add(wmImgFileRow);
			var wmImgOptRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			wmImgOptRow.Controls.Add(new Label { Text = "缩放%:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 4, 3, 0) });
			_numWmImgScale = MakeNumeric(5, 50, 15, 0, 58, () => { _subtitleSettings.WatermarkImageScalePercent = (int)_numWmImgScale.Value; PersistSubtitleConfig(); PushStateToWebPreview(); });
			wmImgOptRow.Controls.Add(_numWmImgScale);
			wmImgOptRow.Controls.Add(new Label { Text = "不透明度%:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(8, 4, 3, 0) });
			_numWmImgOpacity = MakeNumeric(10, 100, 85, 0, 58, () => { _subtitleSettings.WatermarkImageOpacity = (int)_numWmImgOpacity.Value; PersistSubtitleConfig(); PushStateToWebPreview(); });
			wmImgOptRow.Controls.Add(_numWmImgOpacity);
			_pnlWmImage.Controls.Add(wmImgOptRow);
			wmBody.Controls.Add(_pnlWmImage);

			// 水印位置与偏移
			var wmPosRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 0) };
			_cboWmPos = MakeCombo(112);
			_cboWmPos.Items.AddRange(new object[] { "右下角", "右上角", "左下角", "左上角", "底部居中", "顶部居中", "自由坐标" });
			_cboWmPos.SelectedIndexChanged += (_, _) => { if (!_isLoadingUi) { _subtitleSettings.WatermarkPosition = _cboWmPos.SelectedIndex; PersistSubtitleConfig(); PushStateToWebPreview(); } };
			wmPosRow.Controls.Add(_cboWmPos);
			wmPosRow.Controls.Add(new Label { Text = "X:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(6, 4, 2, 0) });
			_numWmOffX = MakeNumeric(0, 1000, 35, 0, 56, () => { _subtitleSettings.WatermarkOffsetX = (int)_numWmOffX.Value; PersistSubtitleConfig(); PushStateToWebPreview(); });
			wmPosRow.Controls.Add(_numWmOffX);
			wmPosRow.Controls.Add(new Label { Text = "Y:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(6, 4, 2, 0) });
			_numWmOffY = MakeNumeric(0, 1800, 40, 0, 58, () => { _subtitleSettings.WatermarkOffsetY = (int)_numWmOffY.Value; PersistSubtitleConfig(); PushStateToWebPreview(); });
			wmPosRow.Controls.Add(_numWmOffY);
			wmBody.Controls.Add(wmPosRow);

			stack.Controls.Add(CreateCollapsibleCard("💧 视频水印设置 (文字 / 图片)", wmBody, expanded: false));

			// 4.5 成品质检门禁卡片（上传前 5 维自动质检：文案↔音频↔字幕↔画面↔音画同步）
			var qcBody = MakeBodyFlow();
			_chkQcEnabled = MakeCheck("上传前自动执行 5 维成品质检", true);
			_chkQcBlockUpload = MakeCheck("质检不通过时自动拦截，不上传云盘", true);
			qcBody.Controls.Add(_chkQcEnabled);
			qcBody.Controls.Add(_chkQcBlockUpload);

			var qcRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 2, 0, 4) };
			qcRow.Controls.Add(new Label { Text = "文案↔音频一致合格线 (%):", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 4, 6, 0) });
			_numQcMinMatch = MakeNumeric(50, 100, 85, 0, 58);
			qcRow.Controls.Add(_numQcMinMatch);
			qcBody.Controls.Add(qcRow);

			qcBody.Controls.Add(new Label
			{
				Text = "提示：双击左侧任务行「成品质检」可查看详情报告、播放成品并支持人工复核放行上传。",
				AutoSize = false,
				Width = 295,
				Height = 32,
				ForeColor = TextMuted
			});
			stack.Controls.Add(CreateCollapsibleCard("🛡️ 成品质检门禁 (文案/音频/字幕)", qcBody, expanded: true));

			// 5. 音频均衡、画质档位、BGM 与本地输出目录卡片
			var exportBody = MakeBodyFlow();
			_chkLoudnorm = MakeCheck("🔊 全片人声音量自动均衡 (-14 LUFS，声音平稳饱满)", true);
			exportBody.Controls.Add(_chkLoudnorm);

			exportBody.Controls.Add(new Label { Text = "⚙️ 导出画质 / 上传速度档位:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 2, 0, 2) });
			_cboBitratePreset = MakeCombo(295);
			_cboBitratePreset.Items.AddRange(new object[]
			{
				"极速上传 (3.0Mbps · 3分钟约 80MB · 推荐窄上行)",
				"均衡高清 (4.5Mbps · 3分钟约 115MB · 推荐常规宽带)",
				"臻彩高画质 (6.5Mbps · 3分钟约 160MB · 推荐光纤)"
			});
			_cboBitratePreset.SelectedIndexChanged += (_, _) => SaveSettingsFromUi();
			exportBody.Controls.Add(_cboBitratePreset);

			_chkBgm = MakeCheck("叠加背景音乐 (BGM)", false);
			exportBody.Controls.Add(_chkBgm);
			var bgmRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 6) };
			_txtBgmPath = MakeTextBox(152);
			var btnBgm = MakeBtn("选音乐", CardBorder, TextPrimary, 62, 25);
			btnBgm.Click += (_, _) =>
			{
				using var dlg = new OpenFileDialog { Filter = "音频文件|*.mp3;*.wav;*.m4a;*.aac;*.flac|所有文件|*.*" };
				if (dlg.ShowDialog(this) == DialogResult.OK) { _txtBgmPath.Text = dlg.FileName; SaveSettingsFromUi(); }
			};
			bgmRow.Controls.Add(_txtBgmPath);
			bgmRow.Controls.Add(btnBgm);
			bgmRow.Controls.Add(new Label { Text = "音量%:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(4, 4, 2, 0) });
			_numBgmVol = MakeNumeric(2, 50, 12, 0, 48);
			bgmRow.Controls.Add(_numBgmVol);
			exportBody.Controls.Add(bgmRow);

			exportBody.Controls.Add(new Label { Text = "本地成品保存目录:", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 2, 0, 2) });
			var outRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			_txtLocalOutputDir = MakeTextBox(215);
			_txtLocalOutputDir.Leave += (_, _) => SaveSettingsFromUi();
			var btnOut = MakeBtn("浏览...", CardBorder, TextPrimary, 70, 25);
			btnOut.Click += (_, _) =>
			{
				using var dlg = new FolderBrowserDialog { Description = "选择本地成品保存目录" };
				if (dlg.ShowDialog(this) == DialogResult.OK) { _txtLocalOutputDir.Text = dlg.SelectedPath; SaveSettingsFromUi(); }
			};
			outRow.Controls.Add(_txtLocalOutputDir);
			outRow.Controls.Add(btnOut);
			exportBody.Controls.Add(outRow);

			stack.Controls.Add(CreateCollapsibleCard("🎵 音频均衡、画质档位与导出", exportBody, expanded: false));

			scroll.Controls.Add(stack);
			return scroll;
		}

		private Panel CreateCollapsibleCard(string title, FlowLayoutPanel body, bool expanded)
		{
			var card = new FlowLayoutPanel
			{
				Width = 325,
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				BackColor = CardBg,
				Margin = new Padding(0, 0, 0, 8),
				Padding = new Padding(0)
			};

			var headerBtn = new Button
			{
				Text = (expanded ? "▼ " : "▶ ") + title,
				Width = 325,
				Height = 34,
				FlatStyle = FlatStyle.Flat,
				BackColor = CardHeaderBg,
				ForeColor = Color.FromArgb(147, 197, 253),
				TextAlign = ContentAlignment.MiddleLeft,
				Padding = new Padding(8, 0, 0, 0),
				Margin = new Padding(0),
				Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			headerBtn.FlatAppearance.BorderSize = 0;

			body.AutoSize = true;
			body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			body.Padding = new Padding(10, 8, 10, 10);
			body.Margin = new Padding(0);
			body.Visible = expanded;

			headerBtn.Click += (_, _) =>
			{
				body.Visible = !body.Visible;
				headerBtn.Text = (body.Visible ? "▼ " : "▶ ") + title;
			};

			card.Controls.Add(headerBtn);
			card.Controls.Add(body);
			return card;
		}

		private async Task InitWebPreviewAsync()
		{
			try
			{
				string userDataDir = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"VideoBatchStudio",
					"WebView2SmartCompose"
				);
				var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
				await _webViewPreview.EnsureCoreWebView2Async(env);
				_webViewPreview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
				_webViewPreview.CoreWebView2.Settings.AreDevToolsEnabled = false;

				string webPreviewDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "web_preview");
				if (!Directory.Exists(webPreviewDir)) return;

				Directory.CreateDirectory(_previewCacheDir);
				_webViewPreview.CoreWebView2.SetVirtualHostNameToFolderMapping(
					"app-smart-preview.local", webPreviewDir, CoreWebView2HostResourceAccessKind.DenyCors);
				// 媒体虚拟主机仅放临时预览 MP4，需允许 app-smart-preview.local 通过 CORS 读取视频帧绘制到 Canvas
				_webViewPreview.CoreWebView2.SetVirtualHostNameToFolderMapping(
					"app-smart-media.local", _previewCacheDir, CoreWebView2HostResourceAccessKind.Allow);
				_webViewPreview.WebMessageReceived -= OnWebViewMessageReceived;
				_webViewPreview.WebMessageReceived += OnWebViewMessageReceived;
				_webViewPreview.NavigationCompleted += (_, _) =>
				{
					_webViewReady = true;
					PushStateToWebPreview();
				};
				_webViewPreview.CoreWebView2.Navigate("https://app-smart-preview.local/preview.html");
			}
			catch { }
		}

		private void OnWebViewMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
		{
			try
			{
				if (string.IsNullOrEmpty(e.Source) ||
				    !e.Source.StartsWith("https://app-smart-preview.local/", StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
				string raw = e.TryGetWebMessageAsString();
				var msg = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(raw);
				if (msg == null || !msg.ContainsKey("action")) return;
				string action = msg["action"]?.ToString();
				var payload = msg.ContainsKey("payload") ? msg["payload"] as Dictionary<string, object> : null;
				if (payload == null) return;

				if (action == "subtitle_drag")
				{
					int subX = Convert.ToInt32(payload["x"]);
					int subPct = Convert.ToInt32(payload["percent"]);
					_subtitleSettings.SubtitleOffsetX = Math.Max(-350, Math.Min(350, subX));
					_subtitleSettings.SubtitleVerticalPercent = Math.Max(15, Math.Min(88, subPct));
					PersistSubtitleConfig();
					RunOnUi(() =>
					{
						_isLoadingUi = true;
						try
						{
							_numSubOffX.Value = Math.Max(_numSubOffX.Minimum, Math.Min(_numSubOffX.Maximum, _subtitleSettings.SubtitleOffsetX));
							_numSubPosY.Value = Math.Max(_numSubPosY.Minimum, Math.Min(_numSubPosY.Maximum, _subtitleSettings.SubtitleVerticalPercent));
						}
						finally { _isLoadingUi = false; }
					});
				}
				else if (action == "watermark_drag")
				{
					int wmX = Convert.ToInt32(payload["x"]);
					int wmY = Convert.ToInt32(payload["y"]);
					_subtitleSettings.WatermarkOffsetX = Math.Max(0, Math.Min(1000, wmX));
					_subtitleSettings.WatermarkOffsetY = Math.Max(0, Math.Min(1800, wmY));
					PersistSubtitleConfig();
					RunOnUi(() =>
					{
						_isLoadingUi = true;
						try
						{
							_numWmOffX.Value = Math.Max(_numWmOffX.Minimum, Math.Min(_numWmOffX.Maximum, _subtitleSettings.WatermarkOffsetX));
							_numWmOffY.Value = Math.Max(_numWmOffY.Minimum, Math.Min(_numWmOffY.Maximum, _subtitleSettings.WatermarkOffsetY));
						}
						finally { _isLoadingUi = false; }
					});
				}
			}
			catch { }
		}

		private void PushStateToWebPreview()
		{
			if (!_webViewReady || _webViewPreview?.CoreWebView2 == null) return;
			try
			{
				string sampleText = "Nos dias dezesseis, dezessete e dezoito, Deus vai abrir uma porta.";
				if (_gridTasks != null && _gridTasks.SelectedRows.Count > 0)
				{
					int rowIdx = _gridTasks.SelectedRows[0].Index;
					if (rowIdx >= 0 && rowIdx < _tasks.Count && !string.IsNullOrWhiteSpace(_tasks[rowIdx].StandardScript))
					{
						string full = _tasks[rowIdx].StandardScript.Trim();
						sampleText = full.Length > 80 ? full.Substring(0, 80) : full;
					}
				}
				if (_subtitleSettings.SubtitleUppercase) sampleText = sampleText.ToUpperInvariant();

				string wmImgDataUrl = string.Empty;
				if (_subtitleSettings.WatermarkType == 1 && !string.IsNullOrWhiteSpace(_subtitleSettings.WatermarkImagePath) && File.Exists(_subtitleSettings.WatermarkImagePath))
				{
					try
					{
						string ext = Path.GetExtension(_subtitleSettings.WatermarkImagePath).TrimStart('.').ToLowerInvariant();
						if (ext == "jpg") ext = "jpeg";
						wmImgDataUrl = $"data:image/{ext};base64,{Convert.ToBase64String(File.ReadAllBytes(_subtitleSettings.WatermarkImagePath))}";
					}
					catch { }
				}

				var dto = new Dictionary<string, object>
				{
					["subtitleText"] = _settings.BurnSubtitlesEnabled ? sampleText : string.Empty,
					["subtitleStyleId"] = _subtitleSettings.SubtitleStyleId,
					["subtitleDisplayMode"] = _subtitleSettings.SubtitleDisplayMode,
					["subtitleScrollPanelEnabled"] = _subtitleSettings.SubtitleScrollPanelEnabled,
					["subtitleHighlightBgColor"] = _subtitleSettings.SubtitleHighlightBoxColorHex,
					["subtitleHighlightTextColor"] = _subtitleSettings.SubtitleHighlightTextColorHex,
					["subtitleHighlightBoxEnabled"] = _subtitleSettings.SubtitleHighlightBoxEnabled,
					["subtitleHighlightBoxOpacity"] = _subtitleSettings.SubtitleHighlightBoxOpacity,
					["subtitleTextColor"] = _subtitleSettings.SubtitleColorHex,
					["subtitleTextOpacity"] = _subtitleSettings.SubtitleTextOpacity,
					["subtitleStrokeColor"] = _subtitleSettings.SubtitleStrokeColorHex,
					["subtitleStrokeWidth"] = _subtitleSettings.SubtitleStrokeWidth,
					["subtitleShadowColor"] = _subtitleSettings.SubtitleShadowColorHex,
					["subtitleShadowDepth"] = _subtitleSettings.SubtitleShadowDepth,
					["subtitleGlowBlur"] = _subtitleSettings.SubtitleGlowBlur,
					["subtitleBorderStyle"] = _subtitleSettings.SubtitleBorderStyle,
					["subtitlePanelColor"] = _subtitleSettings.SubtitleScrollPanelColorHex,
					["subtitlePanelOpacity"] = _subtitleSettings.SubtitleScrollPanelOpacity,
					["subtitleFontSize"] = _subtitleSettings.SubtitleFontSize,
					["subtitleVerticalPercent"] = _subtitleSettings.SubtitleVerticalPercent,
					["subtitleOffsetX"] = _subtitleSettings.SubtitleOffsetX,
					["subtitleFontFamily"] = _subtitleSettings.SubtitleFontFamily,
					["subtitleBold"] = _subtitleSettings.SubtitleBold,
					["subtitleItalic"] = _subtitleSettings.SubtitleItalic,
					["subtitleCursorStyle"] = _subtitleSettings.SubtitleCursorStyle,
					["subtitleVisibleLines"] = Math.Max(1, Math.Min(3, _subtitleSettings.SubtitleVisibleLines)),

					["watermarkEnabled"] = _subtitleSettings.WatermarkEnabled,
					["watermarkType"] = _subtitleSettings.WatermarkType,
					["watermarkText"] = _subtitleSettings.WatermarkText,
					["watermarkFontSize"] = _subtitleSettings.WatermarkFontSize,
					["watermarkFontFamily"] = _subtitleSettings.WatermarkFontFamily,
					["watermarkTextColorHex"] = _subtitleSettings.WatermarkTextColorHex,
					["watermarkTextOpacity"] = _subtitleSettings.WatermarkTextOpacity,
					["watermarkImagePath"] = _subtitleSettings.WatermarkImagePath,
					["watermarkImageDataUrl"] = wmImgDataUrl,
					["watermarkImageScalePercent"] = _subtitleSettings.WatermarkImageScalePercent,
					["watermarkImageOpacity"] = _subtitleSettings.WatermarkImageOpacity,
					["watermarkPosition"] = _subtitleSettings.WatermarkPosition,
					["watermarkOffsetX"] = _subtitleSettings.WatermarkOffsetX,
					["watermarkOffsetY"] = _subtitleSettings.WatermarkOffsetY,
					["bgVideoUrl"] = _currentPreviewVideoUrl ?? string.Empty
				};

				string json = new JavaScriptSerializer().Serialize(dto);
				_webViewPreview.CoreWebView2.ExecuteScriptAsync($"window.updateFromHost({json});");
			}
			catch { }
		}

		private void SwitchWorkspaceMode(int mode)
		{
			_settings.SourceMode = mode;
			_settings.Save();
			ApplyWorkspaceIsolationUi();
		}

		private void ApplyWorkspaceIsolationUi()
		{
			bool isCloud = _settings.SourceMode == 0;
			_btnModeCloud.BackColor = isCloud ? AccentBlue : CardBorder;
			_btnModeCloud.ForeColor = isCloud ? Color.White : TextMuted;
			_btnModeLocal.BackColor = !isCloud ? AccentBlue : CardBorder;
			_btnModeLocal.ForeColor = !isCloud ? Color.White : TextMuted;

			// 严格隔离：选云端时不显示本地输入卡片，选本地时不显示任何云端设置与云端阶段灯！
			_cardCloudWrapper.Visible = isCloud;
			_cardLocalWrapper.Visible = !isCloud;
			_stageBadges[0].Visible = isCloud;
			_stageArrows[0].Visible = isCloud;
			_stageBadges[5].Visible = isCloud;
			_stageArrows[4].Visible = isCloud;
			_stageBadges[1].Text = isCloud ? "② 下载素材" : "① 读取素材";
		}

		private void LoadSettingsToUi()
		{
			_isLoadingUi = true;
			try
			{
				UpdateAuthStatusLabel();
				_txtProxy.Text = _settings.ProxyUrl ?? string.Empty;
				_txtSheetUrl.Text = _settings.SheetUrl ?? string.Empty;

				_cboSheetTab.Items.Clear();
				if (_settings.CachedSheetTabs != null && _settings.CachedSheetTabs.Count > 0)
				{
					foreach (var t in _settings.CachedSheetTabs) _cboSheetTab.Items.Add(t);
					int idx = _settings.CachedSheetTabs.IndexOf(_settings.SheetTabName);
					_cboSheetTab.SelectedIndex = idx >= 0 ? idx : 0;
				}
				else if (!string.IsNullOrWhiteSpace(_settings.SheetTabName))
				{
					_cboSheetTab.Items.Add(_settings.SheetTabName);
					_cboSheetTab.SelectedIndex = 0;
				}

				PopulateColumnsFromCacheOrDefaults();
				SelectComboByPrefix(_cboColScript, _settings.ColScript);
				SelectComboByPrefix(_cboColSource, _settings.ColSourceUrl);
				SelectComboByPrefix(_cboColOutput, _settings.ColOutputLink);
				SelectComboByPrefix(_cboColStatus, _settings.ColStatus);
				SelectComboByPrefix(_cboColTaskSeq, _settings.ColTaskSeq);
				SelectComboByPrefix(_cboColTaskName, _settings.ColTaskName);

				_txtTargetDriveFolder.Text = _settings.TargetDriveFolderUrl ?? string.Empty;
				_chkAutoUpload.Checked = _settings.AutoUploadToDrive;
				_chkAutoWriteBack.Checked = _settings.AutoWriteBackSheet;
				_chkKeepLocalCopy.Checked = _settings.KeepLocalCopyInCloudMode;
				_chkAutoWatch.Checked = _settings.AutoWatchEnabled;
				_numWatchMinutes.Value = Math.Max(_numWatchMinutes.Minimum, Math.Min(_numWatchMinutes.Maximum, _settings.AutoWatchIntervalMinutes));
				_txtLocalInputDir.Text = _settings.LocalInputRootDir ?? string.Empty;
				_txtLocalOutputDir.Text = _settings.LocalOutputDir ?? string.Empty;

				// 成品质检门禁回填
				_chkQcEnabled.Checked = _settings.QcEnabled;
				_chkQcBlockUpload.Checked = _settings.QcBlockUploadOnFail;
				_numQcMinMatch.Value = Math.Max(_numQcMinMatch.Minimum, Math.Min(_numQcMinMatch.Maximum, _settings.QcMinScriptMatchPercent));

				SelectComboByPrefix(_cboLang, _settings.WhisperLanguage);
				SelectComboByPrefix(_cboModel, _settings.WhisperModel);
				_chkRemoveSilence.Checked = _settings.RemoveSilenceEnabled;
				_numSilenceDb.Value = Math.Max(_numSilenceDb.Minimum, Math.Min(_numSilenceDb.Maximum, _settings.SilenceThresholdDb));
				_numMinSilence.Value = Math.Max(_numMinSilence.Minimum, Math.Min(_numMinSilence.Maximum, (decimal)_settings.MinSilenceDurationSec));
				_chkRemoveFillers.Checked = _settings.RemoveFillersEnabled;
				_txtFillers.Text = _settings.FillerWordsCsv ?? string.Empty;
				_chkAlignScript.Checked = _settings.AlignAndReorderByScript;
				_chkFastSinglePass.Checked = _settings.FastSinglePassWhisper;
				_chkBurnSubs.Checked = _settings.BurnSubtitlesEnabled;

				_numSubSize.Value = Math.Max(_numSubSize.Minimum, Math.Min(_numSubSize.Maximum, _subtitleSettings.SubtitleFontSize));
				_numSubPosY.Value = Math.Max(_numSubPosY.Minimum, Math.Min(_numSubPosY.Maximum, _subtitleSettings.SubtitleVerticalPercent));
				_numSubOffX.Value = Math.Max(_numSubOffX.Minimum, Math.Min(_numSubOffX.Maximum, _subtitleSettings.SubtitleOffsetX));

				// 水印回填
				_chkWmEnabled.Checked = _subtitleSettings.WatermarkEnabled;
				_cboWmType.SelectedIndex = Math.Max(0, Math.Min(1, _subtitleSettings.WatermarkType));
				_txtWmText.Text = string.IsNullOrWhiteSpace(_subtitleSettings.WatermarkText) ? "Voice: ElevenLabs" : _subtitleSettings.WatermarkText;
				_numWmFontSize.Value = Math.Max(_numWmFontSize.Minimum, Math.Min(_numWmFontSize.Maximum, _subtitleSettings.WatermarkFontSize));
				_numWmTextOpacity.Value = Math.Max(_numWmTextOpacity.Minimum, Math.Min(_numWmTextOpacity.Maximum, _subtitleSettings.WatermarkTextOpacity));
				_txtWmImgPath.Text = _subtitleSettings.WatermarkImagePath ?? string.Empty;
				_numWmImgScale.Value = Math.Max(_numWmImgScale.Minimum, Math.Min(_numWmImgScale.Maximum, _subtitleSettings.WatermarkImageScalePercent));
				_numWmImgOpacity.Value = Math.Max(_numWmImgOpacity.Minimum, Math.Min(_numWmImgOpacity.Maximum, _subtitleSettings.WatermarkImageOpacity));
				_cboWmPos.SelectedIndex = Math.Max(0, Math.Min(6, _subtitleSettings.WatermarkPosition));
				_numWmOffX.Value = Math.Max(_numWmOffX.Minimum, Math.Min(_numWmOffX.Maximum, _subtitleSettings.WatermarkOffsetX));
				_numWmOffY.Value = Math.Max(_numWmOffY.Minimum, Math.Min(_numWmOffY.Maximum, _subtitleSettings.WatermarkOffsetY));
				UpdateWatermarkTypeVisibility();

				// 音频均衡、画质档位与 BGM 回填
				_chkLoudnorm.Checked = _settings.LoudnormEnabled;
				_cboBitratePreset.SelectedIndex = Math.Max(0, Math.Min(2, _settings.ExportBitratePreset));
				_chkBgm.Checked = _settings.BgmEnabled;
				_txtBgmPath.Text = _settings.BgmPath ?? string.Empty;
				_numBgmVol.Value = Math.Max(_numBgmVol.Minimum, Math.Min(_numBgmVol.Maximum, _settings.BgmVolumePercent));

				RefreshChannelProfileCombo();
				RefreshSubtitleSummary();
				ApplyWorkspaceIsolationUi();
			}
			finally
			{
				_isLoadingUi = false;
			}
		}

		private void SaveSettingsFromUi()
		{
			if (_isLoadingUi) return;
			_settings.ProxyUrl = _txtProxy.Text.Trim();
			_settings.SheetUrl = _txtSheetUrl.Text.Trim();
			_settings.SheetTabName = _cboSheetTab.SelectedItem?.ToString() ?? _settings.SheetTabName;
			_settings.ColScript = _cboColScript.SelectedItem?.ToString() ?? string.Empty;
			_settings.ColSourceUrl = _cboColSource.SelectedItem?.ToString() ?? string.Empty;
			_settings.ColOutputLink = _cboColOutput.SelectedItem?.ToString() ?? string.Empty;
			_settings.ColStatus = _cboColStatus.SelectedItem?.ToString() ?? string.Empty;
			_settings.ColTaskSeq = _cboColTaskSeq.SelectedItem?.ToString() ?? string.Empty;
			_settings.ColTaskName = _cboColTaskName.SelectedItem?.ToString() ?? string.Empty;
			_settings.TargetDriveFolderUrl = _txtTargetDriveFolder.Text.Trim();
			_settings.AutoUploadToDrive = _chkAutoUpload.Checked;
			_settings.AutoWriteBackSheet = _chkAutoWriteBack.Checked;
			_settings.KeepLocalCopyInCloudMode = _chkKeepLocalCopy.Checked;
			_settings.AutoWatchEnabled = _chkAutoWatch.Checked;
			_settings.AutoWatchIntervalMinutes = (int)_numWatchMinutes.Value;
			_settings.LocalInputRootDir = _txtLocalInputDir.Text.Trim();
			_settings.LocalOutputDir = _txtLocalOutputDir.Text.Trim();

			_settings.QcEnabled = _chkQcEnabled.Checked;
			_settings.QcBlockUploadOnFail = _chkQcBlockUpload.Checked;
			_settings.QcMinScriptMatchPercent = (int)_numQcMinMatch.Value;
			_settings.SubtitleConfig = _subtitleSettings;

			string langRaw = _cboLang.SelectedItem?.ToString() ?? "auto";
			_settings.WhisperLanguage = langRaw.Split(' ')[0].Trim();
			_settings.WhisperModel = _cboModel.SelectedItem?.ToString() ?? "base";
			_settings.RemoveSilenceEnabled = _chkRemoveSilence.Checked;
			_settings.SilenceThresholdDb = (int)_numSilenceDb.Value;
			_settings.MinSilenceDurationSec = (double)_numMinSilence.Value;
			_settings.RemoveFillersEnabled = _chkRemoveFillers.Checked;
			_settings.FillerWordsCsv = _txtFillers.Text.Trim();
			_settings.AlignAndReorderByScript = _chkAlignScript.Checked;
			_settings.FastSinglePassWhisper = _chkFastSinglePass.Checked;
			_settings.BurnSubtitlesEnabled = _chkBurnSubs.Checked;

			_settings.LoudnormEnabled = _chkLoudnorm.Checked;
			_settings.ExportBitratePreset = Math.Max(0, _cboBitratePreset.SelectedIndex);
			_settings.BgmEnabled = _chkBgm.Checked;
			_settings.BgmPath = _txtBgmPath.Text.Trim();
			_settings.BgmVolumePercent = (int)_numBgmVol.Value;
			_settings.Save();
			PushStateToWebPreview();
		}

		private void PersistSubtitleConfig()
		{
			_settings.SubtitleConfig = _subtitleSettings;
			_settings.Save();
		}

		private void OnSubtitleSettingsChanged()
		{
			PersistSubtitleConfig();
			_isLoadingUi = true;
			try
			{
				_numSubSize.Value = Math.Max(_numSubSize.Minimum, Math.Min(_numSubSize.Maximum, _subtitleSettings.SubtitleFontSize));
				_numSubPosY.Value = Math.Max(_numSubPosY.Minimum, Math.Min(_numSubPosY.Maximum, _subtitleSettings.SubtitleVerticalPercent));
				_numSubOffX.Value = Math.Max(_numSubOffX.Minimum, Math.Min(_numSubOffX.Maximum, _subtitleSettings.SubtitleOffsetX));
			}
			finally { _isLoadingUi = false; }
			RefreshSubtitleSummary();
			PushStateToWebPreview();
		}

		private void UpdateWatermarkTypeVisibility()
		{
			bool isText = _cboWmType.SelectedIndex == 0;
			_pnlWmText.Visible = isText;
			_pnlWmImage.Visible = !isText;
		}

		private void UpdateAuthStatusLabel()
		{
			if (_settings.HasGoogleAuth)
			{
				string mail = string.IsNullOrWhiteSpace(_settings.GoogleAccountEmail) ? "已授权" : _settings.GoogleAccountEmail;
				_lblGoogleAuthStatus.Text = $"🟢 已绑定：{mail}";
				_lblGoogleAuthStatus.ForeColor = AccentGreen;
			}
			else if (!string.IsNullOrWhiteSpace(_settings.GoogleClientId))
			{
				_lblGoogleAuthStatus.Text = "🟡 已导入凭据，请点【绑定 Gmail 账号】";
				_lblGoogleAuthStatus.ForeColor = AccentAmber;
			}
			else
			{
				_lblGoogleAuthStatus.Text = "⚪ 未绑定（导入 JSON 后绑定 Gmail）";
				_lblGoogleAuthStatus.ForeColor = TextMuted;
			}
		}

		private void RefreshSubtitleSummary()
		{
			var preset = SubtitlePresetCatalog.GetById(_subtitleSettings.SubtitleStyleId);
			_lblSubPresetSummary.Text = $"当前预设：{preset.Name} ({_subtitleSettings.SubtitleFontSize}px)";
		}

		private void ImportGoogleClientSecret()
		{
			using var dlg = new OpenFileDialog
			{
				Title = "选择 Google Cloud 桌面应用凭据 (client_secret_*.json)",
				Filter = "JSON 凭据文件|*.json|所有文件|*.*"
			};
			if (dlg.ShowDialog(this) != DialogResult.OK) return;

			if (GoogleWorkspaceClient.TryImportClientSecretJson(dlg.FileName, _settings, out string err))
			{
				UpdateAuthStatusLabel();
				AppendPlainLog(new PlainLogEntry
				{
					Level = PlainLogLevel.Success,
					Stage = "账号授权",
					Title = "已成功导入 Google Cloud 桌面应用凭据！",
					PlainCause = "现在点击【🔗 绑定 Gmail 账号】并在浏览器点一次允许即可完成绑定。"
				});
			}
			else
			{
				AppendPlainLog(new PlainLogEntry
				{
					Level = PlainLogLevel.Error,
					Stage = "账号授权",
					Title = "导入 Google 凭据 JSON 失败",
					PlainCause = err,
					SelfFixTip = "请确认下载的是 Google Cloud 桌面应用 OAuth 凭据 JSON 文件。"
				});
			}
		}

		private async Task AuthorizeGoogleAsync()
		{
			SaveSettingsFromUi();
			try
			{
				UpdateRadar(1, "请在弹出的浏览器中登录您的 Gmail 并点击允许授权...", 15);
				using var authCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
				string email = await Task.Run(() => GoogleWorkspaceClient.AuthorizeInteractiveAsync(_settings, authCts.Token));
				UpdateAuthStatusLabel();
				UpdateRadar(0, $"🟢 Google 账号已绑定：{email}", 0);
				AppendPlainLog(new PlainLogEntry
				{
					Level = PlainLogLevel.Success,
					Stage = "账号授权",
					Title = $"已成功绑定 Google 账号：{email}",
					PlainCause = "授权凭证已永久记忆，后续自动续期免登录。"
				});
			}
			catch (Exception ex)
			{
				UpdateRadar(0, "🔴 Google 账号绑定未完成", 0);
				AppendPlainLog(SmartComposeEngine.TranslateErrorToPlainLog("账号授权", "Google OAuth", ex));
				_leftTabs.SelectedIndex = 1;
			}
		}

		private async Task LoadSheetHeadersAsync(bool preferUrlGid = true)
		{
			SaveSettingsFromUi();
			try
			{
				UpdateRadar(1, "正在连接谷歌表格读取工作表与第一行表头...", 35);
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				var (tabs, selectedTab, headers) = await Task.Run(() => GoogleWorkspaceClient.ReadSheetTabsAndHeadersAsync(_settings, _settings.SheetTabName, cts.Token, preferUrlGid));

				_settings.CachedSheetTabs = tabs.ToList();
				_settings.CachedSheetHeaders = headers.Select(h => h.Display).ToList();

				_isLoadingUi = true;
				try
				{
					_cboSheetTab.Items.Clear();
					foreach (var t in tabs) _cboSheetTab.Items.Add(t);
					if (_cboSheetTab.Items.Count > 0)
					{
						int idx = tabs.IndexOf(selectedTab);
						_cboSheetTab.SelectedIndex = idx >= 0 ? idx : 0;
					}

					var combos = new[] { _cboColScript, _cboColSource, _cboColOutput, _cboColStatus, _cboColTaskSeq, _cboColTaskName };
					foreach (var cbo in combos)
					{
						cbo.Items.Clear();
						cbo.Items.Add("（不使用）");
						foreach (var h in headers) cbo.Items.Add(h.Display);
					}

					AutoSelectHeader(_cboColScript, headers, _settings.ColScript, "葡语文案", "文案", "script", "文本");
					AutoSelectHeader(_cboColSource, headers, _settings.ColSourceUrl, "成片素材", "素材", "文件夹", "视频链接", "folder");
					AutoSelectHeader(_cboColOutput, headers, _settings.ColOutputLink, "成品链接", "成品", "输出", "成片", "output");
					AutoSelectHeader(_cboColStatus, headers, _settings.ColStatus, "制作状态", "状态", "status");
					AutoSelectHeader(_cboColTaskSeq, headers, _settings.ColTaskSeq, "任务序号", "序号", "编号", "id");
					AutoSelectHeader(_cboColTaskName, headers, _settings.ColTaskName, "任务名称", "成品名", "文件名", "标题", "名称");
				}
				finally { _isLoadingUi = false; }

				SaveSettingsFromUi();
				UpdateRadar(0, $"🟢 已读取「{selectedTab}」表头（共 {headers.Count} 列），配置已自动记住！", 0);
				AppendPlainLog(new PlainLogEntry
				{
					Level = PlainLogLevel.Success,
					Stage = "读取表格",
					Title = $"已读取页签「{selectedTab}」的 {headers.Count} 列表头并自动保存记忆"
				});
			}
			catch (Exception ex)
			{
				UpdateRadar(0, "🔴 读取表格表头失败", 0);
				AppendPlainLog(SmartComposeEngine.TranslateErrorToPlainLog("读取表格", "表格表头", ex));
				_leftTabs.SelectedIndex = 1;
			}
		}

		private async Task RefreshTasksAsync()
		{
			SaveSettingsFromUi();
			if (_settings.SourceMode == 1)
			{
				LoadLocalTasks();
				return;
			}

			try
			{
				UpdateRadar(1, "正在从谷歌表格拉取待处理任务行...", 50);
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
				var list = await Task.Run(() => GoogleWorkspaceClient.ReadPendingTasksAsync(_settings, cts.Token));
				_tasks.Clear();
				_tasks.AddRange(list);
				_currentPreviewTaskKey = string.Empty;
				RenderTaskGrid();
				PushStateToWebPreview();
				_ = LoadSelectedTaskPreviewVideoAsync();
				UpdateRadar(0, $"🟢 已拉取 {_tasks.Count} 条待处理任务，点【▶ 一键启动】即可工作！", 0);
				AppendPlainLog(new PlainLogEntry
				{
					Level = PlainLogLevel.Success,
					Stage = "读取表格",
					Title = $"已从「{_settings.SheetTabName}」拉取 {_tasks.Count} 条待处理任务（已跳过“已完成”的行）"
				});
			}
			catch (Exception ex)
			{
				UpdateRadar(0, "🔴 拉取表格任务失败", 0);
				AppendPlainLog(SmartComposeEngine.TranslateErrorToPlainLog("读取表格", "任务列表", ex));
				_leftTabs.SelectedIndex = 1;
			}
		}

		private void LoadLocalTasks()
		{
			string dir = _txtLocalInputDir.Text.Trim();
			var list = SmartComposeEngine.ScanLocalFolderTasks(dir);
			_tasks.Clear();
			_tasks.AddRange(list);
			_currentPreviewTaskKey = string.Empty;
			RenderTaskGrid();
			PushStateToWebPreview();
			_ = LoadSelectedTaskPreviewVideoAsync();
			UpdateRadar(0, $"🟢 已扫描到本地 {_tasks.Count} 组素材任务，点【▶ 一键启动】即可批量合成！", 0);
		}

		private void DeleteSelectedTasks()
		{
			if (_isRunning) return;
			if (_gridTasks.SelectedRows.Count == 0)
			{
				MessageBox.Show(this, "请先在任务列表中选中要删除的行（按住 Ctrl/Shift 可多选）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			var indices = new List<int>();
			foreach (DataGridViewRow row in _gridTasks.SelectedRows)
			{
				if (row.Index >= 0 && row.Index < _tasks.Count) indices.Add(row.Index);
			}
			indices.Sort((a, b) => b.CompareTo(a));
			foreach (int idx in indices) _tasks.RemoveAt(idx);
			for (int i = 0; i < _tasks.Count; i++) _tasks[i].Index = i + 1;

			if (_tasks.Count == 0)
			{
				_currentPreviewVideoUrl = string.Empty;
				_currentPreviewTaskKey = string.Empty;
			}
			RenderTaskGrid();
			UpdateGlobalStatsLabel();
			PushStateToWebPreview();
			_ = LoadSelectedTaskPreviewVideoAsync();
		}

		private void ClearAllTasks()
		{
			if (_isRunning || _tasks.Count == 0) return;
			if (MessageBox.Show(this, $"确定要清空当前队列中的全部 {_tasks.Count} 条任务吗？\n（仅清空软件列表，不会影响您的谷歌表格或本地文件）", "清空任务确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				return;

			_tasks.Clear();
			_doneCount = 0;
			_failCount = 0;
			_currentPreviewVideoUrl = string.Empty;
			_currentPreviewTaskKey = string.Empty;
			RenderTaskGrid();
			UpdateGlobalStatsLabel();
			PushStateToWebPreview();
		}

		private async Task RetrySelectedUploadAsync()
		{
			if (_isRunning) return;
			if (_settings.SourceMode != 0)
			{
				MessageBox.Show(this, "当前处于本地工作区，无需上传云盘。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			var targets = new List<SmartComposeTaskItem>();
			foreach (DataGridViewRow row in _gridTasks.SelectedRows)
			{
				if (row.Index >= 0 && row.Index < _tasks.Count)
				{
					var t = _tasks[row.Index];
					if (string.IsNullOrWhiteSpace(t.UploadedDriveUrl) && !string.IsNullOrWhiteSpace(t.OutputFilePath) && File.Exists(t.OutputFilePath))
					{
						targets.Add(t);
					}
				}
			}

			if (targets.Count == 0)
			{
				MessageBox.Show(this, "请选中已有本地成品且尚未上传成功的任务行（例如上传失败或质检拦截的任务），即可跳过合成直接重试上传并回填。", "仅重试上传提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			_btnRetryUpload.Enabled = false;
			_btnStart.Enabled = false;
			try
			{
				var cb = new SmartComposeCallbacks
				{
					OnStageUpdate = (stage, msg, pct) => RunOnUi(() => UpdateRadar(stage, msg, pct)),
					OnLog = entry => RunOnUi(() => AppendPlainLog(entry)),
					OnTaskRowRefresh = () => RunOnUi(RenderTaskGrid)
				};
				int okCount = 0;
				foreach (var t in targets)
				{
					try
					{
						await SmartComposeEngine.ForceUploadAndWriteBackAsync(t, _settings, cb, CancellationToken.None);
						okCount++;
					}
					catch (Exception ex)
					{
						AppendPlainLog(SmartComposeEngine.TranslateErrorToPlainLog("重试上传", t.TaskName, ex));
					}
				}
				UpdateRadar(0, $"🟢 重试上传结束：成功 {okCount}/{targets.Count} 条！", 100);
			}
			finally
			{
				_btnRetryUpload.Enabled = true;
				_btnStart.Enabled = true;
				RenderTaskGrid();
			}
		}

		private async Task OnAutoWatchTickAsync()
		{
			if (_isRunning || _isWatchChecking || !_settings.AutoWatchEnabled || _settings.SourceMode != 0 || !_settings.HasGoogleAuth)
				return;

			int intervalMin = Math.Max(3, _settings.AutoWatchIntervalMinutes);
			if ((DateTime.UtcNow - _lastWatchCheckUtc).TotalMinutes < intervalMin)
				return;

			_isWatchChecking = true;
			_lastWatchCheckUtc = DateTime.UtcNow;
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));
				var list = await Task.Run(() => GoogleWorkspaceClient.ReadPendingTasksAsync(_settings, cts.Token));
				if (list.Count > 0)
				{
					_tasks.Clear();
					_tasks.AddRange(list);
					_currentPreviewTaskKey = string.Empty;
					RenderTaskGrid();
					PushStateToWebPreview();
					_ = LoadSelectedTaskPreviewVideoAsync();
					AppendPlainLog(new PlainLogEntry
					{
						Level = PlainLogLevel.Info,
						Stage = "自动值守",
						Title = $"⏰ 自动巡检发现 {list.Count} 条新待处理任务，正在自动启动批量合成！"
					});
					await StartPipelineAsync();
				}
				else
				{
					UpdateRadar(0, $"🟢 自动值守待命中（上次巡检 {DateTime.Now:HH:mm} 暂无新任务，{intervalMin} 分钟后再次巡检）", 0);
				}
			}
			catch { }
			finally
			{
				_isWatchChecking = false;
			}
		}

		private void RefreshChannelProfileCombo()
		{
			_cboChannelProfile.Items.Clear();
			_cboChannelProfile.Items.Add("（当前未选方案 · 点右侧保存）");
			if (_settings.ChannelProfiles != null)
			{
				foreach (var p in _settings.ChannelProfiles)
				{
					_cboChannelProfile.Items.Add(p.Name);
				}
			}
			int idx = 0;
			if (!string.IsNullOrWhiteSpace(_settings.ActiveProfileName) && _settings.ChannelProfiles != null)
			{
				int found = _settings.ChannelProfiles.FindIndex(p => string.Equals(p.Name, _settings.ActiveProfileName, StringComparison.OrdinalIgnoreCase));
				if (found >= 0) idx = found + 1;
			}
			_cboChannelProfile.SelectedIndex = idx;
		}

		private void SaveCurrentChannelProfile()
		{
			SaveSettingsFromUi();
			string defaultName = !string.IsNullOrWhiteSpace(_settings.ActiveProfileName)
				? _settings.ActiveProfileName
				: (!string.IsNullOrWhiteSpace(_settings.SheetTabName) ? _settings.SheetTabName : $"频道方案{_settings.ChannelProfiles.Count + 1}");

			string name = PromptSimpleInput("保存频道 / 矩阵方案", "请输入方案名称（如：葡语情感号-邱晨）：", defaultName);
			if (string.IsNullOrWhiteSpace(name)) return;
			name = name.Trim();

			var ser = new JavaScriptSerializer();
			var subClone = ser.Deserialize<ScriptBatchSettings>(ser.Serialize(_subtitleSettings)) ?? new ScriptBatchSettings();
			var profile = new SmartComposeChannelProfile
			{
				Name = name,
				SourceMode = _settings.SourceMode,
				SheetUrl = _settings.SheetUrl,
				SheetTabName = _settings.SheetTabName,
				ColTaskSeq = _settings.ColTaskSeq,
				ColTaskName = _settings.ColTaskName,
				ColScript = _settings.ColScript,
				ColSourceUrl = _settings.ColSourceUrl,
				ColOutputLink = _settings.ColOutputLink,
				ColStatus = _settings.ColStatus,
				TargetDriveFolderUrl = _settings.TargetDriveFolderUrl,
				AutoUploadToDrive = _settings.AutoUploadToDrive,
				AutoWriteBackSheet = _settings.AutoWriteBackSheet,
				KeepLocalCopyInCloudMode = _settings.KeepLocalCopyInCloudMode,
				LocalInputRootDir = _settings.LocalInputRootDir,
				LocalOutputDir = _settings.LocalOutputDir,
				WhisperLanguage = _settings.WhisperLanguage,
				WhisperModel = _settings.WhisperModel,
				FastSinglePassWhisper = _settings.FastSinglePassWhisper,
				BurnSubtitlesEnabled = _settings.BurnSubtitlesEnabled,
				LoudnormEnabled = _settings.LoudnormEnabled,
				ExportBitratePreset = _settings.ExportBitratePreset,
				QcEnabled = _settings.QcEnabled,
				QcBlockUploadOnFail = _settings.QcBlockUploadOnFail,
				QcMinScriptMatchPercent = _settings.QcMinScriptMatchPercent,
				BgmEnabled = _settings.BgmEnabled,
				BgmPath = _settings.BgmPath,
				BgmVolumePercent = _settings.BgmVolumePercent,
				SubtitleConfig = subClone
			};

			int existing = _settings.ChannelProfiles.FindIndex(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
			if (existing >= 0) _settings.ChannelProfiles[existing] = profile;
			else _settings.ChannelProfiles.Add(profile);
			_settings.ActiveProfileName = name;
			_settings.Save();

			_isLoadingUi = true;
			try { RefreshChannelProfileCombo(); } finally { _isLoadingUi = false; }

			AppendPlainLog(new PlainLogEntry
			{
				Level = PlainLogLevel.Success,
				Stage = "频道方案",
				Title = $"已保存频道方案「{name}」（含表格列、云盘目录、字幕样式、水印与画质档位）"
			});
		}

		private void ApplySelectedChannelProfile()
		{
			int sel = _cboChannelProfile.SelectedIndex;
			if (sel <= 0 || _settings.ChannelProfiles == null || sel - 1 >= _settings.ChannelProfiles.Count) return;
			var p = _settings.ChannelProfiles[sel - 1];

			_settings.ActiveProfileName = p.Name;
			_settings.SourceMode = p.SourceMode;
			_settings.SheetUrl = p.SheetUrl;
			_settings.SheetTabName = p.SheetTabName;
			_settings.ColTaskSeq = p.ColTaskSeq;
			_settings.ColTaskName = p.ColTaskName;
			_settings.ColScript = p.ColScript;
			_settings.ColSourceUrl = p.ColSourceUrl;
			_settings.ColOutputLink = p.ColOutputLink;
			_settings.ColStatus = p.ColStatus;
			_settings.TargetDriveFolderUrl = p.TargetDriveFolderUrl;
			_settings.AutoUploadToDrive = p.AutoUploadToDrive;
			_settings.AutoWriteBackSheet = p.AutoWriteBackSheet;
			_settings.KeepLocalCopyInCloudMode = p.KeepLocalCopyInCloudMode;
			_settings.LocalInputRootDir = p.LocalInputRootDir;
			_settings.LocalOutputDir = p.LocalOutputDir;
			_settings.WhisperLanguage = p.WhisperLanguage;
			_settings.WhisperModel = p.WhisperModel;
			_settings.FastSinglePassWhisper = p.FastSinglePassWhisper;
			_settings.BurnSubtitlesEnabled = p.BurnSubtitlesEnabled;
			_settings.LoudnormEnabled = p.LoudnormEnabled;
			_settings.ExportBitratePreset = p.ExportBitratePreset;
			_settings.QcEnabled = p.QcEnabled;
			_settings.QcBlockUploadOnFail = p.QcBlockUploadOnFail;
			_settings.QcMinScriptMatchPercent = p.QcMinScriptMatchPercent;
			_settings.BgmEnabled = p.BgmEnabled;
			_settings.BgmPath = p.BgmPath;
			_settings.BgmVolumePercent = p.BgmVolumePercent;

			if (p.SubtitleConfig != null)
			{
				var ser = new JavaScriptSerializer();
				_subtitleSettings = ser.Deserialize<ScriptBatchSettings>(ser.Serialize(p.SubtitleConfig)) ?? new ScriptBatchSettings();
				_settings.SubtitleConfig = _subtitleSettings;
			}
			_settings.Save();
			LoadSettingsToUi();
			PushStateToWebPreview();

			AppendPlainLog(new PlainLogEntry
			{
				Level = PlainLogLevel.Success,
				Stage = "频道方案",
				Title = $"已一键切换至频道方案「{p.Name}」！"
			});
		}

		private void DeleteSelectedChannelProfile()
		{
			int sel = _cboChannelProfile.SelectedIndex;
			if (sel <= 0 || _settings.ChannelProfiles == null || sel - 1 >= _settings.ChannelProfiles.Count) return;
			string name = _settings.ChannelProfiles[sel - 1].Name;
			if (MessageBox.Show(this, $"确定删除频道方案「{name}」吗？", "删除方案", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				return;

			_settings.ChannelProfiles.RemoveAt(sel - 1);
			if (string.Equals(_settings.ActiveProfileName, name, StringComparison.OrdinalIgnoreCase))
				_settings.ActiveProfileName = string.Empty;
			_settings.Save();
			_isLoadingUi = true;
			try { RefreshChannelProfileCombo(); } finally { _isLoadingUi = false; }
		}

		private string PromptSimpleInput(string title, string prompt, string defaultVal)
		{
			using var dlg = new Form
			{
				Text = title,
				Size = new Size(400, 155),
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				StartPosition = FormStartPosition.CenterParent,
				BackColor = CardBg,
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 9.2f)
			};
			var lbl = new Label { Text = prompt, Left = 16, Top = 14, Width = 350, AutoSize = false, Height = 22 };
			var tb = new TextBox { Text = defaultVal ?? string.Empty, Left = 16, Top = 42, Width = 350, BackColor = InputBg, ForeColor = TextPrimary, BorderStyle = BorderStyle.FixedSingle };
			var btnOk = MakeBtn("确定保存", AccentBlue, Color.White, 88, 28);
			btnOk.Left = 184; btnOk.Top = 78; btnOk.DialogResult = DialogResult.OK;
			var btnCancel = MakeBtn("取消", CardBorder, TextPrimary, 84, 28);
			btnCancel.Left = 282; btnCancel.Top = 78; btnCancel.DialogResult = DialogResult.Cancel;
			dlg.Controls.AddRange(new Control[] { lbl, tb, btnOk, btnCancel });
			dlg.AcceptButton = btnOk;
			dlg.CancelButton = btnCancel;
			return dlg.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
		}

		private async Task LoadSelectedTaskPreviewVideoAsync()
		{
			if (_tasks.Count == 0)
			{
				if (!string.IsNullOrEmpty(_currentPreviewVideoUrl))
				{
					_currentPreviewVideoUrl = string.Empty;
					_currentPreviewTaskKey = string.Empty;
					PushStateToWebPreview();
				}
				return;
			}

			int rowIdx = _gridTasks.SelectedRows.Count > 0 ? _gridTasks.SelectedRows[0].Index : 0;
			if (rowIdx < 0 || rowIdx >= _tasks.Count) rowIdx = 0;
			var task = _tasks[rowIdx];

			string key = !string.IsNullOrWhiteSpace(task.LocalSourceFolder)
				? "local|" + task.LocalSourceFolder
				: "cloud|" + (task.SourceUrls.FirstOrDefault() ?? string.Empty);

			if (string.IsNullOrWhiteSpace(key) || key == "cloud|" || key == _currentPreviewTaskKey) return;
			_currentPreviewTaskKey = key;
			int seq = Interlocked.Increment(ref _previewLoadSeq);

			try
			{
				RunOnUi(() => _lblPreviewHint.Text = $"📱 正在加载《{task.TaskName}》首段素材用于实时预览...");
				Directory.CreateDirectory(_previewCacheDir);
				string localPreviewFile = null;

				if (!string.IsNullOrWhiteSpace(task.LocalSourceFolder) && Directory.Exists(task.LocalSourceFolder))
				{
					var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".mkv", ".webm", ".m4v" };
					var firstVid = Directory.GetFiles(task.LocalSourceFolder, "*.*", SearchOption.TopDirectoryOnly)
						.Where(f => exts.Contains(Path.GetExtension(f)))
						.OrderBy(f => Path.GetFileName(f), Comparer<string>.Create(GoogleWorkspaceClient.CompareNatural))
						.FirstOrDefault();
					if (!string.IsNullOrEmpty(firstVid))
					{
						string hash = Math.Abs(firstVid.GetHashCode()).ToString("X8");
						string dest = Path.Combine(_previewCacheDir, $"prev_{hash}{Path.GetExtension(firstVid)}");
						if (!File.Exists(dest) || new FileInfo(dest).Length != new FileInfo(firstVid).Length)
						{
							await Task.Run(() => File.Copy(firstVid, dest, true));
						}
						localPreviewFile = dest;
					}
				}
				else if (task.SourceUrls.Count > 0 && _settings.HasGoogleAuth)
				{
					string rawUrl = task.SourceUrls[0];
					string driveId = GoogleWorkspaceClient.ExtractDriveId(rawUrl, out bool isFolder);
					if (!string.IsNullOrWhiteSpace(driveId))
					{
						using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
						string targetFileId = driveId;
						if (isFolder)
						{
							var files = await Task.Run(() => GoogleWorkspaceClient.ListFolderVideosAsync(_settings, driveId, cts.Token));
							if (files.Count > 0) targetFileId = files[0].Id;
							else targetFileId = string.Empty;
						}
						if (!string.IsNullOrWhiteSpace(targetFileId))
						{
							string dest = Path.Combine(_previewCacheDir, $"prev_{targetFileId}.mp4");
							if (!File.Exists(dest) || new FileInfo(dest).Length < 2048)
							{
								await Task.Run(() => GoogleWorkspaceClient.DownloadDriveFileAsync(_settings, targetFileId, dest, null, cts.Token));
							}
							if (File.Exists(dest) && new FileInfo(dest).Length >= 2048)
							{
								localPreviewFile = dest;
							}
						}
					}
				}

				// 仅保留最近 4 个预览缓存视频，绝不占磁盘
				try
				{
					var cached = new DirectoryInfo(_previewCacheDir).GetFiles("prev_*.*")
						.OrderByDescending(f => f.LastWriteTimeUtc)
						.Skip(4)
						.ToList();
					foreach (var old in cached) { try { old.Delete(); } catch { } }
				}
				catch { }

				if (seq != _previewLoadSeq) return;

				if (!string.IsNullOrEmpty(localPreviewFile) && File.Exists(localPreviewFile))
				{
					string fileName = Uri.EscapeDataString(Path.GetFileName(localPreviewFile));
					_currentPreviewVideoUrl = $"https://app-smart-media.local/{fileName}?v={DateTime.UtcNow.Ticks}";
					RunOnUi(() =>
					{
						_lblPreviewHint.Text = "📱 9:16 实时素材预览（点下方【▶ 动效】播放素材，鼠标直接拖拽字幕/水印）";
						PushStateToWebPreview();
					});
				}
				else
				{
					RunOnUi(() => _lblPreviewHint.Text = "📱 9:16 实时可视化预览（鼠标直接拖拽字幕/水印位置）");
				}
			}
			catch
			{
				if (seq == _previewLoadSeq)
				{
					RunOnUi(() => _lblPreviewHint.Text = "📱 9:16 实时可视化预览（鼠标直接拖拽字幕/水印位置）");
				}
			}
		}

		private async Task StartPipelineAsync()
		{
			if (_isRunning) return;
			SaveSettingsFromUi();

			if (_tasks.Count == 0)
			{
				await RefreshTasksAsync();
				if (_tasks.Count == 0)
				{
					MessageBox.Show(this, "当前没有待处理任务！请先选择本地目录或刷新谷歌表格任务列表。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
			}

			_isRunning = true;
			_doneCount = 0;
			_failCount = 0;
			_btnStart.Enabled = false;
			_btnStop.Enabled = true;
			_btnDeleteSelected.Enabled = false;
			_btnClearTasks.Enabled = false;
			_btnRetryUpload.Enabled = false;
			_cts = new CancellationTokenSource();
			_batchClock.Restart();
			_clockTimer.Start();

			AppendPlainLog(new PlainLogEntry
			{
				Level = PlainLogLevel.Info,
				Stage = "批量启动",
				Title = $"开始执行批量智能合成队列（共 {_tasks.Count} 条任务）..."
			});

			try
			{
				var cb = new SmartComposeCallbacks
				{
					OnStageUpdate = (stage, msg, pct) => RunOnUi(() => UpdateRadar(stage, msg, pct)),
					OnLog = entry => RunOnUi(() => AppendPlainLog(entry)),
					OnTaskRowRefresh = () => RunOnUi(RenderTaskGrid)
				};

				var uploadSlots = new SemaphoreSlim(1, 1); // 后台单通道有序上传，既让主线 0 等待连续合成，又避免抢占上行带宽拖死素材下载
				var backgroundUploads = new List<Task>();

				for (int i = 0; i < _tasks.Count; i++)
				{
					if (_cts.IsCancellationRequested) break;
					var task = _tasks[i];
					if (task.Status == "已完成") { Interlocked.Increment(ref _doneCount); continue; }

					var prod = await SmartComposeEngine.ProduceAndQcTaskAsync(task, _settings, _subtitleSettings, cb, _cts.Token);
					if (prod.ReadyForUpload)
					{
						var capturedTask = task;
						var capturedProd = prod;
						var token = _cts.Token;
						var uploadJob = Task.Run(async () =>
						{
							await uploadSlots.WaitAsync(token).ConfigureAwait(false);
							try
							{
								bool upOk = await SmartComposeEngine.UploadAndFinalizeTaskAsync(capturedTask, capturedProd, _settings, cb, token).ConfigureAwait(false);
								if (upOk) Interlocked.Increment(ref _doneCount);
								else Interlocked.Increment(ref _failCount);
								RunOnUi(UpdateGlobalStatsLabel);
							}
							finally
							{
								uploadSlots.Release();
							}
						}, token);
						backgroundUploads.Add(uploadJob);
					}
					else
					{
						if (prod.AlreadyCompleted) Interlocked.Increment(ref _doneCount);
						else Interlocked.Increment(ref _failCount);
						RunOnUi(UpdateGlobalStatsLabel);
					}
				}

				if (backgroundUploads.Count > 0)
				{
					int pendingUps = backgroundUploads.Count(u => !u.IsCompleted);
					if (pendingUps > 0)
					{
						UpdateRadar(6, $"所有视频合成完毕！正在等待最后 {pendingUps} 条后台云盘上传与回填收尾...", 92);
					}
					await Task.WhenAll(backgroundUploads);
				}

				string summary = $"本批次结束：成功 {_doneCount} 条，失败 {_failCount} 条，总耗时 {_batchClock.Elapsed:mm\\:ss}。";
				UpdateRadar(0, $"🎉 {summary}", 100);
				AppendPlainLog(new PlainLogEntry
				{
					Level = _failCount == 0 ? PlainLogLevel.Success : PlainLogLevel.Warning,
					Stage = "批次完成",
					Title = summary,
					SelfFixTip = _failCount > 0 ? "可点【❌ 报错】筛选查看大白话失败原因与处理办法。" : string.Empty
				});
			}
			catch (OperationCanceledException)
			{
				UpdateRadar(0, "⏹ 已手动停止当前流水线。", 0);
				AppendPlainLog(new PlainLogEntry { Level = PlainLogLevel.Warning, Stage = "手动停止", Title = "用户手动停止了批量任务。" });
			}
			finally
			{
				_isRunning = false;
				_batchClock.Stop();
				_clockTimer.Stop();
				_btnStart.Enabled = true;
				_btnStop.Enabled = false;
				_btnDeleteSelected.Enabled = true;
				_btnClearTasks.Enabled = true;
				_btnRetryUpload.Enabled = true;
				UpdateGlobalStatsLabel();
				RenderTaskGrid();
			}
		}

		private void StopPipeline()
		{
			try { _cts?.Cancel(); } catch { }
			_btnStop.Enabled = false;
			UpdateRadar(0, "正在安全停止当前任务...", _prgGlobal.Value);
		}

		private void UpdateRadar(int activeStageIndex, string liveMessage, int progressPercent)
		{
			for (int i = 0; i < _stageBadges.Length; i++)
			{
				int stageNum = i + 1;
				if (activeStageIndex <= 0)
				{
					_stageBadges[i].BackColor = Color.FromArgb(30, 41, 59);
					_stageBadges[i].ForeColor = TextMuted;
				}
				else if (stageNum < activeStageIndex)
				{
					_stageBadges[i].BackColor = Color.FromArgb(16, 85, 65);
					_stageBadges[i].ForeColor = Color.FromArgb(167, 243, 208);
				}
				else if (stageNum == activeStageIndex)
				{
					_stageBadges[i].BackColor = AccentBlue;
					_stageBadges[i].ForeColor = Color.White;
				}
				else
				{
					_stageBadges[i].BackColor = Color.FromArgb(30, 41, 59);
					_stageBadges[i].ForeColor = TextMuted;
				}
			}
			_lblLiveAction.Text = activeStageIndex > 0 ? $"🔵 正在工作：{liveMessage}" : liveMessage;
			_prgGlobal.Value = Math.Max(0, Math.Min(100, progressPercent));
			UpdateGlobalStatsLabel();
		}

		private void UpdateGlobalStatsLabel()
		{
			_lblGlobalStats.Text = $"总进度: {_doneCount + _failCount}/{_tasks.Count} | 成功: {_doneCount} | 失败: {_failCount} | 耗时: {_batchClock.Elapsed:mm\\:ss}";
		}

		private void RenderTaskGrid()
		{
			_gridTasks.Rows.Clear();
			foreach (var t in _tasks)
			{
				string res = !string.IsNullOrWhiteSpace(t.UploadedDriveUrl)
					? t.UploadedDriveUrl
					: (!string.IsNullOrWhiteSpace(t.OutputFilePath) ? Path.GetFileName(t.OutputFilePath) : t.ErrorPlainMessage);

				int rowIdx = _gridTasks.Rows.Add(
					!string.IsNullOrWhiteSpace(t.TaskSeq) ? t.TaskSeq : t.Index.ToString(),
					t.TaskName,
					t.ClipCount > 0 ? $"{t.ClipCount} 段" : "--",
					t.StageText,
					$"{t.ProgressPercent}%",
					t.TrimmedSeconds > 0.05 ? $"-{t.TrimmedSeconds:0.0}s" : "--",
					string.IsNullOrWhiteSpace(t.QcSummary) ? "待检" : t.QcSummary,
					t.Status,
					res ?? string.Empty
				);

				var row = _gridTasks.Rows[rowIdx];
				if (t.Status == "已完成") row.DefaultCellStyle.ForeColor = Color.FromArgb(110, 231, 183);
				else if (t.Status == "质检拦截") row.DefaultCellStyle.ForeColor = Color.FromArgb(253, 224, 71);
				else if (t.Status == "失败") row.DefaultCellStyle.ForeColor = Color.FromArgb(252, 165, 165);
				else if (t.StageIndex > 0) row.DefaultCellStyle.ForeColor = Color.FromArgb(147, 197, 253);

				if (t.QcPassed == true) row.Cells["Qc"].Style.ForeColor = Color.FromArgb(110, 231, 183);
				else if (t.QcPassed == false) row.Cells["Qc"].Style.ForeColor = Color.FromArgb(252, 165, 165);

				bool hasUrl = IsSafeHttpUrl(t.UploadedDriveUrl);
				bool hasFile = !string.IsNullOrWhiteSpace(t.OutputFilePath) && File.Exists(t.OutputFilePath);
				if (hasUrl || hasFile)
				{
					var resCell = row.Cells["Result"];
					resCell.Style.ForeColor = Color.FromArgb(96, 165, 250);
					resCell.Style.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Underline);
					resCell.ToolTipText = hasUrl ? $"单击直接打开云盘成品：\n{t.UploadedDriveUrl}" : $"单击播放本地成品：\n{t.OutputFilePath}";
				}
			}
		}

		private static bool IsSafeHttpUrl(string url)
		{
			return !string.IsNullOrWhiteSpace(url)
				&& Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri uri)
				&& (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
		}

		private void OnTaskGridCellClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < 0 || e.RowIndex >= _tasks.Count) return;
			if (e.ColumnIndex != _gridTasks.Columns["Result"].Index) return;
			var task = _tasks[e.RowIndex];
			if (IsSafeHttpUrl(task.UploadedDriveUrl))
			{
				try { Process.Start(new ProcessStartInfo { FileName = task.UploadedDriveUrl.Trim(), UseShellExecute = true }); } catch { }
			}
			else if (!string.IsNullOrWhiteSpace(task.OutputFilePath) && File.Exists(task.OutputFilePath))
			{
				try { Process.Start(new ProcessStartInfo { FileName = task.OutputFilePath, UseShellExecute = true }); } catch { }
			}
		}

		private void OnTaskGridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < 0 || e.RowIndex >= _tasks.Count) return;
			var task = _tasks[e.RowIndex];
			if (task.QcReport != null)
			{
				ShowQcDetailDialog(task);
			}
			else if (!string.IsNullOrWhiteSpace(task.OutputFilePath) && File.Exists(task.OutputFilePath))
			{
				try { Process.Start(new ProcessStartInfo { FileName = task.OutputFilePath, UseShellExecute = true }); } catch { }
			}
			else
			{
				MessageBox.Show(this, $"任务《{task.TaskName}》尚未生成成品质检报告，请先执行合成。", "成品质检提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		private void ShowQcDetailDialog(SmartComposeTaskItem task)
		{
			var r = task.QcReport;
			if (r == null) return;

			using var dlg = new Form
			{
				Text = $"🛡️ 成品质检报告 - {task.TaskName}",
				Size = new Size(720, 610),
				MinimumSize = new Size(640, 520),
				StartPosition = FormStartPosition.CenterParent,
				BackColor = BgDark,
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 9.2f)
			};

			var topBanner = new Label
			{
				Dock = DockStyle.Top,
				Height = 46,
				BackColor = r.Passed ? Color.FromArgb(16, 70, 55) : Color.FromArgb(100, 32, 36),
				ForeColor = Color.White,
				Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
				TextAlign = ContentAlignment.MiddleLeft,
				Padding = new Padding(14, 0, 14, 0),
				Text = r.Passed
					? $"✅ 质检合格！文案与音频一致度 {r.ScriptMatchPercent}%（合格线 {_settings.QcMinScriptMatchPercent}%）"
					: $"❌ 质检拦截：{string.Join("；", r.FailureReasons)}"
			};

			var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 14, 10) };

			var checksSb = new StringBuilder();
			checksSb.AppendLine($"1. 文案 ↔ 音频一致度：{(r.ScriptAudioPass ? "✅ 合格" : "❌ 不达标")}  （实际 {r.ScriptMatchPercent}% / 合格线 {_settings.QcMinScriptMatchPercent}%）");
			checksSb.AppendLine($"2. 字幕时间轴有效性：{(r.SubtitleSyncPass ? "✅ 合格" : "❌ 异常")}");
			checksSb.AppendLine($"3. 音频音量健康度：{(r.AudioHealthPass ? "✅ 合格" : "❌ 疑似静音/无声")}  （平均 {r.MeanVolumeDb:0.0} dB / 峰值 {r.MaxVolumeDb:0.0} dB）");
			checksSb.AppendLine($"4. 视频画面与完整性：{(r.VideoIntegrityPass ? "✅ 合格" : "❌ 损坏/过短")}  （画面时长 {r.VideoDurationSec:0.00} 秒）");
			checksSb.AppendLine($"5. 音画时长同步检查：{(r.AvSyncPass ? "✅ 合格" : "❌ 音画错位")}  （画面 {r.VideoDurationSec:0.00} 秒 vs 音频 {r.AudioDurationSec:0.00} 秒）");
			if (r.MissingWords.Count > 0)
			{
				checksSb.AppendLine();
				checksSb.AppendLine($"⚠️ 疑似漏读 / 偏差词：{string.Join("、", r.MissingWords)}");
			}

			var txtChecks = new TextBox
			{
				Dock = DockStyle.Top,
				Height = r.MissingWords.Count > 0 ? 150 : 120,
				Multiline = true,
				ReadOnly = true,
				ScrollBars = ScrollBars.Vertical,
				BackColor = CardBg,
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Font = new Font("Microsoft YaHei UI", 9.2f),
				Text = checksSb.ToString().TrimEnd()
			};

			var compareTable = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 2,
				RowCount = 2,
				Padding = new Padding(0, 10, 0, 0)
			};
			compareTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			compareTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			compareTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
			compareTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

			compareTable.Controls.Add(new Label { Text = "📄 标准文案 (表格/TXT)", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(147, 197, 253), Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) }, 0, 0);
			compareTable.Controls.Add(new Label { Text = "🎙️ AI 实际听写音频文本", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(167, 243, 208), Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) }, 1, 0);

			var txtStd = new TextBox
			{
				Dock = DockStyle.Fill,
				Multiline = true,
				ReadOnly = true,
				ScrollBars = ScrollBars.Vertical,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Text = string.IsNullOrWhiteSpace(r.StandardScriptText) ? "（未提供标准文案）" : r.StandardScriptText
			};
			var txtRec = new TextBox
			{
				Dock = DockStyle.Fill,
				Multiline = true,
				ReadOnly = true,
				ScrollBars = ScrollBars.Vertical,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Text = string.IsNullOrWhiteSpace(r.RecognizedAudioText) ? "（未识别到语音文本）" : r.RecognizedAudioText
			};
			compareTable.Controls.Add(txtStd, 0, 1);
			compareTable.Controls.Add(txtRec, 1, 1);

			body.Controls.Add(compareTable);
			body.Controls.Add(txtChecks);

			var footer = new FlowLayoutPanel
			{
				Dock = DockStyle.Bottom,
				Height = 52,
				FlowDirection = FlowDirection.RightToLeft,
				BackColor = CardBg,
				Padding = new Padding(12, 10, 12, 8)
			};

			var btnClose = MakeBtn("关闭", CardBorder, TextPrimary, 84, 32);
			btnClose.Click += (_, _) => dlg.Close();
			footer.Controls.Add(btnClose);

			bool hasLocalFile = !string.IsNullOrWhiteSpace(task.OutputFilePath) && File.Exists(task.OutputFilePath);

			if (_settings.SourceMode == 0 && string.IsNullOrWhiteSpace(task.UploadedDriveUrl) && hasLocalFile)
			{
				var btnForceUpload = MakeBtn("☁️ 人工复核通过 · 强制上传并回填", AccentGreen, Color.White, 236, 32);
				btnForceUpload.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
				btnForceUpload.Click += async (_, _) =>
				{
					btnForceUpload.Enabled = false;
					btnForceUpload.Text = "正在上传并回填...";
					try
					{
						var cb = new SmartComposeCallbacks
						{
							OnStageUpdate = (stage, msg, pct) => RunOnUi(() => UpdateRadar(stage, msg, pct)),
							OnLog = entry => RunOnUi(() => AppendPlainLog(entry)),
							OnTaskRowRefresh = () => RunOnUi(RenderTaskGrid)
						};
						await SmartComposeEngine.ForceUploadAndWriteBackAsync(task, _settings, cb, CancellationToken.None);
						MessageBox.Show(dlg, "已人工放行并成功上传到谷歌云盘且回填表格！", "上传成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
						dlg.Close();
					}
					catch (Exception ex)
					{
						btnForceUpload.Enabled = true;
						btnForceUpload.Text = "☁️ 人工复核通过 · 强制上传并回填";
						MessageBox.Show(dlg, $"上传失败：{ex.Message}", "上传出错", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				};
				footer.Controls.Add(btnForceUpload);
			}

			if (IsSafeHttpUrl(task.UploadedDriveUrl))
			{
				var btnOpenDrive = MakeBtn("🌐 打开云盘成品", AccentBlue, Color.White, 138, 32);
				btnOpenDrive.Click += (_, _) =>
				{
					try { Process.Start(new ProcessStartInfo { FileName = task.UploadedDriveUrl.Trim(), UseShellExecute = true }); } catch { }
				};
				footer.Controls.Add(btnOpenDrive);
			}

			if (hasLocalFile)
			{
				var btnPlay = MakeBtn("▶ 播放本地成品", AccentBlue, Color.White, 132, 32);
				btnPlay.Click += (_, _) =>
				{
					try { Process.Start(new ProcessStartInfo { FileName = task.OutputFilePath, UseShellExecute = true }); } catch { }
				};
				footer.Controls.Add(btnPlay);
			}

			dlg.Controls.Add(body);
			dlg.Controls.Add(topBanner);
			dlg.Controls.Add(footer);
			dlg.ShowDialog(this);
		}

		private void AppendPlainLog(PlainLogEntry entry)
		{
			_logs.Add(entry);
			if (_logs.Count > 500) _logs.RemoveAt(0);
			if (_logFilter == null || _logFilter == entry.Level) AppendSingleLogToRichText(entry);
		}

		private void SetLogFilter(PlainLogLevel? level)
		{
			_logFilter = level;
			_btnFilterAll.BackColor = level == null ? AccentBlue : CardBorder;
			_btnFilterOk.BackColor = level == PlainLogLevel.Success ? AccentGreen : CardBorder;
			_btnFilterErr.BackColor = level == PlainLogLevel.Error ? AccentRed : CardBorder;
			RenderLogs();
		}

		private void RenderLogs()
		{
			_rtbLogs.Clear();
			foreach (var e in _logs)
			{
				if (_logFilter == null || _logFilter == e.Level) AppendSingleLogToRichText(e);
			}
		}

		private void AppendSingleLogToRichText(PlainLogEntry entry)
		{
			Color col = entry.Level switch
			{
				PlainLogLevel.Success => Color.FromArgb(110, 231, 183),
				PlainLogLevel.Warning => Color.FromArgb(253, 224, 71),
				PlainLogLevel.Error => Color.FromArgb(252, 165, 165),
				_ => Color.FromArgb(186, 230, 253)
			};
			_rtbLogs.SelectionStart = _rtbLogs.TextLength;
			_rtbLogs.SelectionLength = 0;
			_rtbLogs.SelectionColor = col;
			_rtbLogs.AppendText(entry.ToReadableText() + "\n");
			_rtbLogs.ScrollToCaret();
		}

		private void CopyDiagnosticReport()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"=== VideoBatchStudio 智能合成排障报告 ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
			sb.AppendLine($"工作区: {(_settings.SourceMode == 0 ? "云端工作区" : "本地工作区")} | 账号: {_settings.GoogleAccountEmail}");
			sb.AppendLine($"统计: 总计 {_tasks.Count} / 成功 {_doneCount} / 失败 {_failCount}");
			sb.AppendLine();
			foreach (var e in _logs.Skip(Math.Max(0, _logs.Count - 80)))
			{
				sb.Append(e.ToReadableText());
				if (!string.IsNullOrWhiteSpace(e.RawDetail)) sb.AppendLine($"    [技术详情] {e.RawDetail}");
			}
			try
			{
				Clipboard.SetText(sb.ToString());
				MessageBox.Show(this, "已复制排障报告到剪贴板！", "复制成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch { }
		}

		private void PopulateColumnsFromCacheOrDefaults()
		{
			var combos = new[] { _cboColScript, _cboColSource, _cboColOutput, _cboColStatus, _cboColTaskSeq, _cboColTaskName };
			foreach (var cbo in combos)
			{
				cbo.Items.Clear();
				cbo.Items.Add("（不使用）");
				if (_settings.CachedSheetHeaders != null && _settings.CachedSheetHeaders.Count > 0)
				{
					foreach (var h in _settings.CachedSheetHeaders) cbo.Items.Add(h);
				}
				else
				{
					for (int i = 0; i < 12; i++) cbo.Items.Add($"{GoogleWorkspaceClient.ColumnIndexToLetter(i)} 列");
				}
				cbo.SelectedIndex = 0;
			}
		}

		private static void AutoSelectHeader(ComboBox cbo, List<SheetColumnHeader> headers, string saved, params string[] keywords)
		{
			if (!string.IsNullOrWhiteSpace(saved) && SelectComboByPrefix(cbo, saved)) return;
			foreach (var kw in keywords)
			{
				var match = headers.FirstOrDefault(h => !string.IsNullOrWhiteSpace(h.HeaderText) &&
					h.HeaderText.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
				if (match != null)
				{
					SelectComboByPrefix(cbo, match.ColumnLetter + " 列");
					return;
				}
			}
			if (cbo.Items.Count > 0) cbo.SelectedIndex = 0;
		}

		private static bool SelectComboByPrefix(ComboBox cbo, string target)
		{
			if (string.IsNullOrWhiteSpace(target) || cbo.Items.Count == 0) return false;
			string prefix = target.Split(' ')[0].Trim();
			for (int i = 0; i < cbo.Items.Count; i++)
			{
				string item = cbo.Items[i]?.ToString() ?? string.Empty;
				if (item.Equals(target, StringComparison.OrdinalIgnoreCase) || item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					cbo.SelectedIndex = i;
					return true;
				}
			}
			return false;
		}

		private void RunOnUi(Action action)
		{
			if (IsDisposed) return;
			if (InvokeRequired) BeginInvoke(action);
			else action();
		}

		private FlowLayoutPanel MakeBodyFlow() => new FlowLayoutPanel
		{
			AutoSize = true,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			BackColor = CardBg
		};

		private TextBox AddLabeledTextBox(FlowLayoutPanel parent, string label, int width)
		{
			parent.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 2, 0, 2) });
			var tb = MakeTextBox(width);
			tb.Leave += (_, _) => SaveSettingsFromUi();
			parent.Controls.Add(tb);
			return tb;
		}

		private ComboBox AddLabeledCombo(FlowLayoutPanel parent, string label, int width)
		{
			parent.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 2, 0, 2) });
			var cbo = MakeCombo(width);
			cbo.SelectedIndexChanged += (_, _) => SaveSettingsFromUi();
			parent.Controls.Add(cbo);
			return cbo;
		}

		private TextBox MakeTextBox(int width) => new TextBox
		{
			Width = width,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			BorderStyle = BorderStyle.FixedSingle,
			Margin = new Padding(0, 0, 0, 6)
		};

		private ComboBox MakeCombo(int width) => new ComboBox
		{
			Width = width,
			DropDownStyle = ComboBoxStyle.DropDownList,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			FlatStyle = FlatStyle.Flat,
			Margin = new Padding(0, 0, 0, 6)
		};

		private NumericUpDown MakeNumeric(decimal min, decimal max, decimal val, int decimals, int width, Action onChange = null)
		{
			var n = new NumericUpDown
			{
				Minimum = min,
				Maximum = max,
				DecimalPlaces = decimals,
				Value = Math.Max(min, Math.Min(max, val)),
				Width = width,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				Margin = new Padding(0, 0, 4, 4)
			};
			n.ValueChanged += (_, _) =>
			{
				if (_isLoadingUi) return;
				if (onChange != null) onChange();
				else SaveSettingsFromUi();
			};
			return n;
		}

		private CheckBox MakeCheck(string text, bool isChecked)
		{
			var chk = new CheckBox { Text = text, Checked = isChecked, AutoSize = true, ForeColor = TextPrimary, Margin = new Padding(0, 2, 0, 5) };
			chk.CheckedChanged += (_, _) => SaveSettingsFromUi();
			return chk;
		}

		private Button MakeBtn(string text, Color bg, Color fg, int width, int height)
		{
			var btn = new Button
			{
				Text = text,
				Width = width,
				Height = height,
				BackColor = bg,
				ForeColor = fg,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand,
				Margin = new Padding(0, 0, 5, 0)
			};
			btn.FlatAppearance.BorderSize = 0;
			return btn;
		}
	}
}
