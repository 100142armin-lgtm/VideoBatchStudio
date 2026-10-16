using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoBatchMerger.Transcriber;

internal sealed class TranscribePanel : UserControl
{
	private readonly MainForm _mainForm;
	private readonly List<TranscribeItem> _items = new();
	private CancellationTokenSource _cts;
	private bool _isProcessing;

	// 赛博科技配色体系
	private static readonly Color BgCanvas = Color.FromArgb(7, 11, 20);
	private static readonly Color CardBg = Color.FromArgb(13, 20, 36);
	private static readonly Color CardAltBg = Color.FromArgb(16, 26, 46);
	private static readonly Color CardBorder = Color.FromArgb(28, 44, 76);
	private static readonly Color InputBg = Color.FromArgb(9, 14, 24);
	private static readonly Color TextPrimary = Color.FromArgb(241, 245, 249);
	private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
	private static readonly Color CyanNeon = Color.FromArgb(56, 189, 248);
	private static readonly Color EmeraldNeon = Color.FromArgb(52, 211, 153);
	private static readonly Color AmberNeon = Color.FromArgb(251, 191, 36);

	// HUD 状态指示
	private Label _envBadgeTitle;
	private Label _envTagWhisper;
	private Label _envTagModel;
	private Label _envTagCuda;
	private Button _btnInstallModel;
	private Button _btnRefreshEnv;

	// 左侧面板：分段胶囊按钮与双面板
	private Button _btnModeLocal;
	private Button _btnModeOnline;
	private Panel _panelLocalMode;
	private Panel _panelOnlineMode;
	private int _currentInputMode = 0; // 0: 本地媒体, 1: 在线链接
	private ListBox _lstLocalFiles;
	private TextBox _txtOnlineUrls;
	private Label _urlCountBadge;

	// 参数设置
	private ComboBox _cboModel;
	private ComboBox _cboSourceLang;
	private ComboBox _cboTargetLang;
	private ComboBox _cboSubMode;
	private Button _btnStartTranscribe;
	private Button _btnStopTranscribe;

	// 右侧监视区控件
	private ListView _listViewTasks;
	private RichTextBox _rtbOriginal;
	private RichTextBox _rtbTranslated;
	private RichTextBox _logBox;
	private ProgressBar _progressBar;
	private Label _progressTextLabel;
	private Label _statusLabel;
	private System.Windows.Forms.Timer _uiTimer;

	public TranscribePanel(MainForm mainForm)
	{
		_mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
		InitializeComponent();
		RefreshEnvironmentStatus();
	}

	private void InitializeComponent()
	{
		SuspendLayout();
		Dock = DockStyle.Fill;
		BackColor = BgCanvas;
		ForeColor = TextPrimary;
		Font = new Font("Microsoft YaHei UI", 9f);
		AllowDrop = true;

		Panel rootPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = BgCanvas };
		AttachDragDrop(rootPanel);

		// 1. 顶部 Header HUD 状态栏
		Panel headerHud = CreateHeaderHud();
		headerHud.Dock = DockStyle.Top;

		// 2. 主工作区左右 SplitContainer
		SplitContainer splitMain = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Vertical,
			SplitterWidth = 6,
			BackColor = CardBorder,
			Panel1MinSize = 100,
			Panel2MinSize = 100
		};
		splitMain.Resize += (s, e) =>
		{
			if (splitMain.Width > 800)
			{
				try { splitMain.SplitterDistance = Math.Max(380, (int)(splitMain.Width * 0.36)); } catch { }
			}
		};
		splitMain.Panel1.BackColor = BgCanvas;
		splitMain.Panel2.BackColor = BgCanvas;
		splitMain.Panel1.Padding = new Padding(0, 0, 4, 0);
		splitMain.Panel2.Padding = new Padding(4, 0, 0, 0);
		AttachDragDrop(splitMain);

		// 左侧配置与输入面板
		Panel leftPanel = CreateLeftInputPanel();
		splitMain.Panel1.Controls.Add(leftPanel);

		// 右侧双轨字幕编辑器与实时矩阵
		Panel rightPanel = CreateRightViewerPanel();
		splitMain.Panel2.Controls.Add(rightPanel);

		// 3. 底部全域发光状态栏
		Panel bottomStatusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = CardBg, Padding = new Padding(8, 4, 8, 4) };
		bottomStatusBar.Paint += (s, e) =>
		{
			using var pen = new Pen(CardBorder);
			e.Graphics.DrawLine(pen, 0, 0, bottomStatusBar.Width, 0);
		};
		_statusLabel = new Label
		{
			Text = "⚡ AI 离线高精语音识别与双语翻译引擎已就绪",
			ForeColor = TextMuted,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font("Microsoft YaHei UI", 8.5f)
		};
		bottomStatusBar.Controls.Add(_statusLabel);

		// 组装主界面
		Panel centerWorkspace = new Panel { Dock = DockStyle.Fill, BackColor = BgCanvas };
		centerWorkspace.Controls.Add(splitMain);
		centerWorkspace.Controls.Add(headerHud);

		rootPanel.Controls.Add(centerWorkspace);
		rootPanel.Controls.Add(bottomStatusBar);
		Controls.Add(rootPanel);

		centerWorkspace.Resize += delegate
		{
			if (splitMain.Width > 750)
			{
				try { splitMain.SplitterDistance = 450; } catch { }
			}
		};

		_uiTimer = new System.Windows.Forms.Timer { Interval = 350 };
		_uiTimer.Tick += (s, e) =>
		{
			if (_isProcessing)
			{
				UpdateTaskListView();
			}
		};
		_uiTimer.Start();

		AttachDragDropRecursively(this);

		ResumeLayout(false);
	}

	private Panel CreateHeaderHud()
	{
		Panel p = new Panel
		{
			Height = 56,
			BackColor = CardBg,
			Margin = new Padding(0, 0, 0, 8),
			Padding = new Padding(14, 8, 14, 8)
		};
		p.Paint += (s, e) => DrawTechBorder(e.Graphics, p.ClientRectangle, CardBorder, 6);

		Panel leftInfo = new Panel { Dock = DockStyle.Left, Width = 600, BackColor = Color.Transparent };

		_envBadgeTitle = new Label
		{
			Text = "⚡ AI 智能字幕工作台",
			Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold),
			ForeColor = TextPrimary,
			Location = new Point(2, 4),
			AutoSize = true
		};
		leftInfo.Controls.Add(_envBadgeTitle);

		Label lblSub = new Label
		{
			Text = "Whisper 本地离线神经网络 · 英语 / 葡语 / 西语等多语种高精转录 · 简体中文实时双语对照",
			Font = new Font("Microsoft YaHei UI", 8.25f),
			ForeColor = CyanNeon,
			Location = new Point(4, 30),
			AutoSize = true
		};
		leftInfo.Controls.Add(lblSub);
		p.Controls.Add(leftInfo);

		FlowLayoutPanel flowPills = new FlowLayoutPanel
		{
			Dock = DockStyle.Right,
			AutoSize = true,
			WrapContents = false,
			FlowDirection = FlowDirection.LeftToRight,
			Padding = new Padding(0, 4, 0, 0),
			BackColor = Color.Transparent
		};

		_envTagWhisper = CreateTechPill("Whisper 内核", "检测中", EmeraldNeon);
		_envTagModel = CreateTechPill("AI 模型", "检测中", EmeraldNeon);
		_envTagCuda = CreateTechPill("硬件加速", "检测中", EmeraldNeon);

		flowPills.Controls.Add(_envTagWhisper);
		flowPills.Controls.Add(_envTagModel);
		flowPills.Controls.Add(_envTagCuda);

		_btnInstallModel = MakeTechButton("⚡ 一键配置 AI 组件", 130, 30, Color.FromArgb(37, 99, 235), async (s, e) => await AutoInstallAiComponentsAsync());
		_btnInstallModel.Margin = new Padding(8, 0, 4, 0);
		flowPills.Controls.Add(_btnInstallModel);

		_btnRefreshEnv = MakeTechButton("⟳ 刷新环境", 86, 30, Color.FromArgb(28, 42, 68), (s, e) => RefreshEnvironmentStatus());
		_btnRefreshEnv.Margin = new Padding(4, 0, 0, 0);
		flowPills.Controls.Add(_btnRefreshEnv);

		p.Controls.Add(flowPills);
		return p;
	}

	private Panel CreateLeftInputPanel()
	{
		Panel p = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = CardBg,
			Padding = new Padding(12)
		};
		p.Paint += (s, e) => DrawTechBorder(e.Graphics, p.ClientRectangle, CardBorder, 6);
		AttachDragDrop(p);

		// 1. 顶部模式切换分段胶囊栏与计数药丸
		Panel switchBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.Transparent };

		_btnModeLocal = MakeSegmentButton("📁 本地媒体文件 (支持拖入)", (s, e) => SwitchInputMode(0));
		_btnModeLocal.Location = new Point(0, 2);
		_btnModeLocal.Width = 175;
		_btnModeLocal.Tag = "active";
		switchBar.Controls.Add(_btnModeLocal);

		_btnModeOnline = MakeSegmentButton("🌐 在线视频链接 (自动解析)", (s, e) => SwitchInputMode(1));
		_btnModeOnline.Location = new Point(182, 2);
		_btnModeOnline.Width = 175;
		_btnModeOnline.Tag = "normal";
		switchBar.Controls.Add(_btnModeOnline);

		_urlCountBadge = new Label
		{
			Text = "0 个任务",
			Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
			ForeColor = TextMuted,
			BackColor = Color.FromArgb(20, 32, 54),
			Padding = new Padding(6, 4, 6, 4),
			Dock = DockStyle.Right,
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleRight
		};
		switchBar.Controls.Add(_urlCountBadge);
		p.Controls.Add(switchBar);

		// 2. 模式内容容器 (中间充填)
		Panel inputContentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 6) };

		// --- 模式 A: 本地文件容器 ---
		_panelLocalMode = new Panel { Dock = DockStyle.Fill, BackColor = CardAltBg, Padding = new Padding(8) };
		AttachDragDrop(_panelLocalMode);

		Panel localToolBar = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };
		Button btnAddFiles = MakeTechButton("＋ 添加文件…", 95, 28, Color.FromArgb(28, 42, 68), (s, e) => AddLocalMediaFiles());
		btnAddFiles.Location = new Point(0, 2);
		localToolBar.Controls.Add(btnAddFiles);

		Button btnAddFolder = MakeTechButton("📂 添加文件夹…", 105, 28, Color.FromArgb(28, 42, 68), (s, e) => AddLocalMediaFolder());
		btnAddFolder.Location = new Point(102, 2);
		localToolBar.Controls.Add(btnAddFolder);

		Button btnClearFiles = MakeTechButton("🗑 清空列表", 85, 28, Color.FromArgb(28, 42, 68), (s, e) =>
		{
			_lstLocalFiles.Items.Clear();
			UpdateInputCount();
		});
		btnClearFiles.Location = new Point(214, 2);
		localToolBar.Controls.Add(btnClearFiles);
		_panelLocalMode.Controls.Add(localToolBar);

		Label lblDropHint = new Label
		{
			Text = "[拖拽提示] 支持鼠标直接拖拽音视频文件或文件夹至此区域 (或 Ctrl+V 粘贴)",
			ForeColor = CyanNeon,
			Dock = DockStyle.Top,
			Height = 24,
			Font = new Font("Microsoft YaHei UI", 8.25f),
			TextAlign = ContentAlignment.MiddleLeft
		};
		_panelLocalMode.Controls.Add(lblDropHint);

		_lstLocalFiles = new ListBox
		{
			Dock = DockStyle.Fill,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			BorderStyle = BorderStyle.None,
			SelectionMode = SelectionMode.MultiExtended,
			Font = new Font("Consolas", 9f)
		};
		AttachDragDrop(_lstLocalFiles);
		_panelLocalMode.Controls.Add(_lstLocalFiles);
		_panelLocalMode.Controls.Add(lblDropHint);
		_panelLocalMode.Controls.Add(localToolBar);
		localToolBar.SendToBack();
		lblDropHint.SendToBack();

		// --- 模式 B: 在线链接容器 ---
		_panelOnlineMode = new Panel { Dock = DockStyle.Fill, BackColor = CardAltBg, Padding = new Padding(8), Visible = false };
		AttachDragDrop(_panelOnlineMode);

		Panel onlineToolBar = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };
		Button btnPasteUrls = MakeTechButton("📋 快捷粘贴剪贴板链接", 155, 28, Color.FromArgb(28, 42, 68), (s, e) => PasteUrlsFromClipboard());
		btnPasteUrls.Location = new Point(0, 2);
		onlineToolBar.Controls.Add(btnPasteUrls);

		Button btnClearUrls = MakeTechButton("🗑 清空链接", 85, 28, Color.FromArgb(28, 42, 68), (s, e) =>
		{
			_txtOnlineUrls.Clear();
			UpdateInputCount();
		});
		btnClearUrls.Location = new Point(162, 2);
		onlineToolBar.Controls.Add(btnClearUrls);

		Label lblOnlineHint = new Label
		{
			Text = "[链接列表] 每行输入一个视频链接 (支持 YouTube / TikTok / Facebook / IG 等):",
			ForeColor = EmeraldNeon,
			Dock = DockStyle.Top,
			Height = 24,
			Font = new Font("Microsoft YaHei UI", 8.25f),
			TextAlign = ContentAlignment.MiddleLeft
		};

		_txtOnlineUrls = new TextBox
		{
			Multiline = true,
			ScrollBars = ScrollBars.Both,
			WordWrap = false,
			Dock = DockStyle.Fill,
			BackColor = InputBg,
			ForeColor = Color.FromArgb(226, 232, 240),
			BorderStyle = BorderStyle.None,
			Font = new Font("Consolas", 9f)
		};
		_txtOnlineUrls.Text = "https://www.youtube.com/shorts/bqLBK7GuPCk\r\nhttps://www.tiktok.com/@lucia.gove/video/7617254633341308178";
		_txtOnlineUrls.TextChanged += (s, e) => UpdateInputCount();
		AttachDragDrop(_txtOnlineUrls);

		_panelOnlineMode.Controls.Add(_txtOnlineUrls);
		_panelOnlineMode.Controls.Add(lblOnlineHint);
		_panelOnlineMode.Controls.Add(onlineToolBar);
		onlineToolBar.SendToBack();
		lblOnlineHint.SendToBack();

		inputContentPanel.Controls.Add(_panelOnlineMode);
		inputContentPanel.Controls.Add(_panelLocalMode);
		p.Controls.Add(inputContentPanel);
		p.Controls.Add(switchBar);
		switchBar.SendToBack();

		// 3. 底部 AI 参数设置卡片与启动按钮组
		Panel paramPanel = new Panel { Dock = DockStyle.Bottom, Height = 220, BackColor = Color.Transparent };

		// 第一行参数：转录模型选择与源语言
		Label lblModel = new Label { Text = "转录模型:", Location = new Point(4, 10), AutoSize = true, ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		paramPanel.Controls.Add(lblModel);

		_cboModel = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(72, 7),
			Width = 150,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			FlatStyle = FlatStyle.Flat
		};
		_cboModel.Items.AddRange(new object[] { "极速模式 (Base 148MB)", "标准推荐 (Small 488MB)", "高精影院 (Turbo 574MB)" });
		_cboModel.SelectedIndex = 1;
		paramPanel.Controls.Add(_cboModel);

		Label lblSrcLang = new Label { Text = "视频语种:", Location = new Point(232, 10), AutoSize = true, ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		paramPanel.Controls.Add(lblSrcLang);

		_cboSourceLang = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(300, 7),
			Width = 140,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			FlatStyle = FlatStyle.Flat
		};
		_cboSourceLang.Items.AddRange(new object[] { "自动识别 (推荐)", "英语 (English)", "葡萄牙语 (Português)", "西班牙语 (Español)", "中文普通话 (Chinese)", "法语 (Français)", "德语 (Deutsch)", "俄语 (Русский)", "日语 (Japanese)" });
		_cboSourceLang.SelectedIndex = 0;
		paramPanel.Controls.Add(_cboSourceLang);

		// 第二行参数：目标翻译语言与字幕模式
		Label lblTargetLang = new Label { Text = "目标翻译:", Location = new Point(4, 46), AutoSize = true, ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		paramPanel.Controls.Add(lblTargetLang);

		_cboTargetLang = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(72, 43),
			Width = 150,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			FlatStyle = FlatStyle.Flat
		};
		_cboTargetLang.Items.AddRange(new object[] { "[中] 简体中文 (译为中文)", "[英] 英语 (译为英文)", "不翻译 (仅输出原文)" });
		_cboTargetLang.SelectedIndex = 0;
		paramPanel.Controls.Add(_cboTargetLang);

		Label lblSubMode = new Label { Text = "对照格式:", Location = new Point(232, 46), AutoSize = true, ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		paramPanel.Controls.Add(lblSubMode);

		_cboSubMode = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(300, 43),
			Width = 140,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			FlatStyle = FlatStyle.Flat
		};
		_cboSubMode.Items.AddRange(new object[] { "双语严格对照", "仅保留中文", "仅保留原文" });
		_cboSubMode.SelectedIndex = 0;
		paramPanel.Controls.Add(_cboSubMode);

		// 启动与停止按钮
		_btnStartTranscribe = MakeTechButton("▶ 启动 AI 语音转录与翻译", 240, 38, Color.FromArgb(37, 99, 235), async (s, e) => await StartTranscribeProcessAsync());
		_btnStartTranscribe.Location = new Point(4, 85);
		_btnStartTranscribe.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
		paramPanel.Controls.Add(_btnStartTranscribe);

		_btnStopTranscribe = MakeTechButton("⏹ 终止任务", 110, 38, Color.FromArgb(185, 28, 28), (s, e) => StopTranscribeProcess());
		_btnStopTranscribe.Location = new Point(252, 85);
		_btnStopTranscribe.Enabled = false;
		paramPanel.Controls.Add(_btnStopTranscribe);

		// 日志输出框
		Panel logPanel = new Panel { Location = new Point(4, 130), Size = new Size(440, 85), BackColor = Color.Transparent };
		_logBox = new RichTextBox
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(6, 9, 16),
			ForeColor = EmeraldNeon,
			BorderStyle = BorderStyle.None,
			Font = new Font("Consolas", 8.5f),
			ReadOnly = true
		};
		logPanel.Controls.Add(_logBox);
		paramPanel.Controls.Add(logPanel);

		paramPanel.Resize += delegate
		{
			logPanel.Width = paramPanel.ClientSize.Width - 8;
		};

		p.Controls.Add(paramPanel);
		return p;
	}

	private Panel CreateRightViewerPanel()
	{
		Panel p = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = CardBg,
			Padding = new Padding(12)
		};
		p.Paint += (s, e) => DrawTechBorder(e.Graphics, p.ClientRectangle, CardBorder, 6);

		// 顶部快捷工具栏 (包含用户指定需要的：复制原文、复制双语、复制纯中文、导出SRT、导出TXT)
		Panel rightHeader = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Transparent };
		Panel topToolBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };

		Button btnCopyOriginal = MakeTechButton("复制原文", 86, 28, Color.FromArgb(28, 42, 68), (s, e) => CopyOriginalText());
		btnCopyOriginal.Location = new Point(0, 3);
		topToolBar.Controls.Add(btnCopyOriginal);

		Button btnCopyChinese = MakeTechButton("复制纯中文", 96, 28, Color.FromArgb(28, 42, 68), (s, e) => CopyChineseText());
		btnCopyChinese.Location = new Point(92, 3);
		topToolBar.Controls.Add(btnCopyChinese);

		Button btnCopyBilingual = MakeTechButton("复制双语对照", 108, 28, Color.FromArgb(28, 42, 68), (s, e) => CopyBilingualText());
		btnCopyBilingual.Location = new Point(194, 3);
		topToolBar.Controls.Add(btnCopyBilingual);

		Button btnExportSrt = MakeTechButton("导出 SRT 字幕", 106, 28, Color.FromArgb(13, 148, 136), (s, e) => ExportSrtFile());
		btnExportSrt.Location = new Point(308, 3);
		topToolBar.Controls.Add(btnExportSrt);

		Button btnExportTxt = MakeTechButton("导出 TXT 文本", 106, 28, Color.FromArgb(28, 42, 68), (s, e) => ExportTxtFile());
		btnExportTxt.Location = new Point(420, 3);
		topToolBar.Controls.Add(btnExportTxt);

		_progressTextLabel = new Label
		{
			Text = "准备就绪",
			ForeColor = TextMuted,
			Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
			Dock = DockStyle.Right,
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleRight
		};
		topToolBar.Controls.Add(_progressTextLabel);
		rightHeader.Controls.Add(topToolBar);

		// 任务进度条
		_progressBar = new ProgressBar
		{
			Dock = DockStyle.Bottom,
			Height = 4,
			Style = ProgressBarStyle.Continuous,
			Value = 0
		};
		rightHeader.Controls.Add(_progressBar);

		// 中间切分为：上方任务列表矩阵 + 下方双轨字幕监视器
		SplitContainer splitRightContent = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			SplitterWidth = 6,
			BackColor = CardBorder,
			Panel1MinSize = 50,
			Panel2MinSize = 50
		};
		splitRightContent.Resize += (s, e) =>
		{
			if (splitRightContent.Height > 300)
			{
				try { splitRightContent.SplitterDistance = Math.Min(160, (int)(splitRightContent.Height * 0.28)); } catch { }
			}
		};
		splitRightContent.Panel1.BackColor = CardAltBg;
		splitRightContent.Panel2.BackColor = CardAltBg;

		// 上方：任务矩阵
		_listViewTasks = new ListView
		{
			Dock = DockStyle.Fill,
			View = View.Details,
			FullRowSelect = true,
			GridLines = true,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			BorderStyle = BorderStyle.None,
			HeaderStyle = ColumnHeaderStyle.Nonclickable,
			Font = new Font("Microsoft YaHei UI", 9f)
		};
		SetDoubleBuffered(_listViewTasks);
		_listViewTasks.Columns.Add("#", 38);
		_listViewTasks.Columns.Add("媒体名称", 240);
		_listViewTasks.Columns.Add("语种", 80);
		_listViewTasks.Columns.Add("句数", 65);
		_listViewTasks.Columns.Add("进度", 75);
		_listViewTasks.Columns.Add("状态", 140);

		_listViewTasks.SelectedIndexChanged += (s, e) => DisplaySelectedTaskSubtitles();
		splitRightContent.Panel1.Controls.Add(_listViewTasks);

		// 下方：双轨高清晰卡片监视器 (左侧/上方原文，右侧/下方中文)
		SplitContainer splitSubtitles = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			SplitterWidth = 6,
			BackColor = CardBorder
		};
		splitSubtitles.Panel1.BackColor = CardBg;
		splitSubtitles.Panel2.BackColor = CardBg;

		// 识别原文轨
		Panel pnlOrig = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
		Label lblOrig = new Label { Text = "● 原始外文语音识别结果 (纯文本 · 按视频归类)", ForeColor = CyanNeon, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold), Dock = DockStyle.Top, Height = 22 };
		pnlOrig.Controls.Add(lblOrig);

		_rtbOriginal = new RichTextBox
		{
			Dock = DockStyle.Fill,
			BackColor = InputBg,
			ForeColor = Color.FromArgb(226, 232, 240),
			BorderStyle = BorderStyle.None,
			Font = new Font("Consolas", 9.5f),
			ScrollBars = RichTextBoxScrollBars.Vertical,
			ReadOnly = false
		};
		pnlOrig.Controls.Add(_rtbOriginal);
		splitSubtitles.Panel1.Controls.Add(pnlOrig);

		// 中文译文轨
		Panel pnlTrans = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
		Label lblTrans = new Label { Text = "● 简体中文对照翻译 (纯文本 · 按视频归类)", ForeColor = EmeraldNeon, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold), Dock = DockStyle.Top, Height = 22 };
		pnlTrans.Controls.Add(lblTrans);

		_rtbTranslated = new RichTextBox
		{
			Dock = DockStyle.Fill,
			BackColor = InputBg,
			ForeColor = EmeraldNeon,
			BorderStyle = BorderStyle.None,
			Font = new Font("Microsoft YaHei UI", 9.5f),
			ScrollBars = RichTextBoxScrollBars.Vertical,
			ReadOnly = false
		};
		pnlTrans.Controls.Add(_rtbTranslated);
		splitSubtitles.Panel2.Controls.Add(pnlTrans);

		splitRightContent.Panel2.Controls.Add(splitSubtitles);
		p.Controls.Add(splitRightContent);
		p.Controls.Add(rightHeader);
		rightHeader.SendToBack();

		return p;
	}

	private void SwitchInputMode(int modeIndex)
	{
		_currentInputMode = modeIndex;
		_panelLocalMode.Visible = (modeIndex == 0);
		_panelOnlineMode.Visible = (modeIndex == 1);
		_btnModeLocal.Tag = (modeIndex == 0) ? "active" : "normal";
		_btnModeOnline.Tag = (modeIndex == 1) ? "active" : "normal";
		_btnModeLocal.Invalidate();
		_btnModeOnline.Invalidate();
		UpdateInputCount();
	}

	private Button MakeSegmentButton(string text, EventHandler onClick)
	{
		Button b = new Button
		{
			Text = text,
			Height = 32,
			FlatStyle = FlatStyle.Flat,
			ForeColor = TextPrimary,
			Cursor = Cursors.Hand,
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
			Margin = new Padding(0)
		};
		b.FlatAppearance.BorderSize = 0;
		b.Click += onClick;
		b.Paint += (s, e) =>
		{
			Graphics g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle bounds = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
			bool isActive = (b.Tag as string) == "active";
			Color bg = isActive ? Color.FromArgb(20, 36, 64) : Color.FromArgb(14, 21, 37);
			Color border = isActive ? CyanNeon : Color.FromArgb(30, 48, 80);
			Color fore = isActive ? CyanNeon : TextMuted;

			using var path = CreateRoundedPath(bounds, 4);
			using var brush = new SolidBrush(bg);
			g.FillPath(brush, path);
			using var pen = new Pen(border, isActive ? 1.5f : 1f);
			g.DrawPath(pen, path);

			TextRenderer.DrawText(g, b.Text, b.Font, bounds, fore,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
		};
		return b;
	}

	private void AttachDragDropRecursively(Control root)
	{
		if (root == null) return;
		root.AllowDrop = true;
		root.DragEnter -= OnFileDragEnter;
		root.DragEnter += OnFileDragEnter;
		root.DragDrop -= OnFileDragDrop;
		root.DragDrop += OnFileDragDrop;
		foreach (Control child in root.Controls)
		{
			AttachDragDropRecursively(child);
		}
	}

	private void AttachDragDrop(Control c)
	{
		AttachDragDropRecursively(c);
	}

	private void OnFileDragEnter(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text))
		{
			e.Effect = DragDropEffects.Copy;
		}
	}

	private void OnFileDragDrop(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
			if (files != null && files.Length > 0)
			{
				SwitchInputMode(0);
				AddFiles(files);
			}
		}
		else if (e.Data.GetDataPresent(DataFormats.Text))
		{
			string text = (string)e.Data.GetData(DataFormats.Text);
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (text.Contains("http://") || text.Contains("https://"))
				{
					SwitchInputMode(1);
					PasteUrlsFromText(text);
				}
			}
		}
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == (Keys.Control | Keys.V))
		{
			if (Clipboard.ContainsText())
			{
				string text = Clipboard.GetText();
				if (!string.IsNullOrWhiteSpace(text) && (text.Contains("http://") || text.Contains("https://")))
				{
					SwitchInputMode(1);
					PasteUrlsFromText(text);
					return true;
				}
			}
			else if (Clipboard.ContainsFileDropList())
			{
				var files = Clipboard.GetFileDropList().Cast<string>();
				SwitchInputMode(0);
				AddFiles(files);
				return true;
			}
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	private void PasteUrlsFromClipboard()
	{
		if (Clipboard.ContainsText())
		{
			PasteUrlsFromText(Clipboard.GetText());
		}
		else
		{
			LogMessage("[链接粘贴] 剪贴板中未检测到文本内容。");
			_statusLabel.Text = "剪贴板中未检测到文本内容";
		}
	}

	private void PasteUrlsFromText(string text)
	{
		if (string.IsNullOrWhiteSpace(text)) return;
		var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(l => l.Trim())
			.Where(l => l.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || l.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			.ToList();

		if (lines.Count == 0)
		{
			// 如果没有直接以 http 开头，尝试提取其中的 URL
			var matches = System.Text.RegularExpressions.Regex.Matches(text, @"https?://[^\s""'<>]+");
			foreach (System.Text.RegularExpressions.Match m in matches)
			{
				lines.Add(m.Value.Trim());
			}
		}

		if (lines.Count > 0)
		{
			var current = _txtOnlineUrls.Lines.ToList();
			int added = 0;
			foreach (var u in lines)
			{
				if (!current.Contains(u))
				{
					current.Add(u);
					added++;
				}
			}
			_txtOnlineUrls.Lines = current.ToArray();
			UpdateInputCount();
			LogMessage($"[链接粘贴] 成功添加 {added} 个在线视频链接。");
			_statusLabel.Text = $"✓ 成功粘贴添加 {added} 个在线视频链接";
		}
		else
		{
			LogMessage("[链接粘贴] 未能从剪贴板提取到有效的网页视频链接。");
			_statusLabel.Text = "未能从剪贴板提取到有效视频链接";
		}
	}

	// ====================== 业务逻辑实现 ======================

	public void RefreshEnvironmentStatus()
	{
		string whisper = TranscribeEngine.FindWhisperCliPath();
		bool whisperOk = !string.IsNullOrEmpty(whisper) && File.Exists(whisper);
		bool isCuda = TranscribeEngine.IsCudaBuild(whisper);
		bool gpuHardware = TranscribeEngine.IsCudaAvailable();

		string baseModel = TranscribeEngine.FindModelPath("base");
		string smallModel = TranscribeEngine.FindModelPath("small");
		bool modelOk = !string.IsNullOrEmpty(baseModel) || !string.IsNullOrEmpty(smallModel);

		_envTagWhisper.Text = whisperOk
			? (isCuda ? "● Whisper 内核: GPU加速版" : "● Whisper 内核: CPU版")
			: "● Whisper: 未找到";
		_envTagWhisper.ForeColor = whisperOk ? EmeraldNeon : AmberNeon;

		_envTagModel.Text = modelOk ? "● AI 模型: 已就绪" : "● AI 模型: 缺失";
		_envTagModel.ForeColor = modelOk ? EmeraldNeon : AmberNeon;

		_envTagCuda.Text = isCuda
			? "● NVIDIA GPU 硬件加速: 生效中"
			: (gpuHardware ? "● 检测到NVIDIA显卡 (未配置CUDA内核)" : "● CPU 模式: 运行中");
		_envTagCuda.ForeColor = isCuda ? CyanNeon : (gpuHardware ? AmberNeon : TextMuted);

		if (whisperOk && modelOk && isCuda)
		{
			_btnInstallModel.Text = "✓ GPU组件与模型已就绪";
			_btnInstallModel.BackColor = Color.FromArgb(28, 42, 68);
		}
		else if (whisperOk && modelOk)
		{
			_btnInstallModel.Text = "✓ 模型已就绪";
			_btnInstallModel.BackColor = Color.FromArgb(28, 42, 68);
		}
		else
		{
			_btnInstallModel.Text = "⚡ 一键配置 AI 组件";
			_btnInstallModel.BackColor = Color.FromArgb(37, 99, 235);
		}
	}

	private async Task AutoInstallAiComponentsAsync()
	{
		_btnInstallModel.Enabled = false;
		_btnInstallModel.Text = "⏳ 正在配置 AI 组件...";
		using var installCts = new CancellationTokenSource();
		try
		{
			LogMessage("========================================");
			LogMessage("[组件配置] 开始自动安装与检测 AI 语音转录环境...");

			// 1. 检查或安装 Whisper CLI 推理引擎内核
			string whisperCli = TranscribeEngine.FindWhisperCliPath();
			if (string.IsNullOrEmpty(whisperCli) || !File.Exists(whisperCli))
			{
				bool hasNvidia = TranscribeEngine.IsCudaAvailable();
				bool engineInstalled = false;
				if (hasNvidia)
				{
					LogMessage("[引擎配置] 检测到系统配备 NVIDIA 独立显卡，正在优先拉取 GPU CUDA 硬件加速内核...");
					engineInstalled = await TranscribeEngine.InstallCudaBuildAsync(LogMessage, installCts.Token);
				}

				if (!engineInstalled)
				{
					LogMessage("[引擎配置] 正在拉取通用 CPU 版 Whisper 离线推理引擎 (全机型免显卡兼容)...");
					engineInstalled = await TranscribeEngine.InstallCpuBuildAsync(LogMessage, installCts.Token);
				}

				if (!engineInstalled)
				{
					throw new Exception("Whisper 推理内核自动拉取失败，请检查网络连接或开启代理后重试！");
				}
			}
			else
			{
				LogMessage($"[引擎配置] 本地已存在 Whisper 内核: {whisperCli}");
			}

			// 2. 检查或下载 AI 语音模型 (优先 Base 模型快速建立基础能力，140MB 极速下载)
			string baseModel = TranscribeEngine.FindModelPath("base");
			string smallModel = TranscribeEngine.FindModelPath("small");

			if (string.IsNullOrEmpty(baseModel) && string.IsNullOrEmpty(smallModel))
			{
				LogMessage("[模型配置] 正在从高速镜像拉取 ggml-base.bin 多语种基础模型 (约 141MB，英/葡/西全语种)...");
				bool modelOk = await TranscribeEngine.DownloadModelAsync("base", LogMessage, installCts.Token);
				if (!modelOk)
				{
					throw new Exception("AI 语音转录模型下载失败，请检查网络连接后重试。");
				}
			}
			else
			{
				LogMessage("[模型配置] 本地已检测到可用 AI 语音模型。");
			}

			RefreshEnvironmentStatus();

			whisperCli = TranscribeEngine.FindWhisperCliPath();
			baseModel = TranscribeEngine.FindModelPath("base");
			smallModel = TranscribeEngine.FindModelPath("small");

			if (!string.IsNullOrEmpty(whisperCli) && (!string.IsNullOrEmpty(baseModel) || !string.IsNullOrEmpty(smallModel)))
			{
				string typeName = TranscribeEngine.IsCudaBuild(whisperCli) ? "GPU 硬件加速版" : "CPU 通用版";
				LogMessage($"[组件配置] AI 语音转录模型与引擎配置完毕 ({typeName})，已完全就绪！");
				_statusLabel.Text = $"✓ AI 语音转录模型与引擎 ({typeName}) 已配置就绪";
			}
			else
			{
				LogMessage("[组件配置] AI 组件配置未能完整就绪，请检查网络后重试。");
				_statusLabel.Text = "⚠ AI 组件配置未完成，请查看日志";
			}
		}
		catch (Exception ex)
		{
			LogMessage($"[配置异常] {ex.Message}");
			_statusLabel.Text = $"⚠ AI 组件配置异常: {ex.Message}";
		}
		finally
		{
			_btnInstallModel.Enabled = true;
			RefreshEnvironmentStatus();
		}
	}

	public void OnActivated()
	{
		RefreshEnvironmentStatus();
	}

	public void AddFiles(IEnumerable<string> files)
	{
		if (files == null) return;
		SwitchInputMode(0);
		string[] exts = { ".mp4", ".mov", ".mkv", ".mp3", ".wav", ".m4a", ".webm", ".avi", ".flv", ".wmv", ".aac", ".ogg" };
		int added = 0;

		foreach (var f in files)
		{
			if (string.IsNullOrWhiteSpace(f)) continue;
			if (Directory.Exists(f))
			{
				var subFiles = Directory.GetFiles(f, "*.*", SearchOption.AllDirectories)
					.Where(sf => exts.Contains(Path.GetExtension(sf).ToLowerInvariant()));
				foreach (var sf in subFiles)
				{
					if (!_lstLocalFiles.Items.Contains(sf))
					{
						_lstLocalFiles.Items.Add(sf);
						added++;
					}
				}
			}
			else if (File.Exists(f))
			{
				string ext = Path.GetExtension(f).ToLowerInvariant();
				if (exts.Contains(ext) && !_lstLocalFiles.Items.Contains(f))
				{
					_lstLocalFiles.Items.Add(f);
					added++;
				}
			}
		}
		UpdateInputCount();
		if (added > 0) LogMessage($"[导入文件] 成功添加 {added} 个媒体文件。");
	}

	public void AddOnlineUrls(IEnumerable<string> urls)
	{
		if (urls == null) return;
		SwitchInputMode(1);
		var current = _txtOnlineUrls.Lines.ToList();
		foreach (var u in urls)
		{
			if (!string.IsNullOrWhiteSpace(u) && !current.Contains(u))
			{
				current.Add(u);
			}
		}
		_txtOnlineUrls.Lines = current.ToArray();
		UpdateInputCount();
	}

	private void AddLocalMediaFiles()
	{
		using var ofd = new OpenFileDialog
		{
			Title = "选择音视频文件（支持多选）",
			Filter = "媒体文件 (*.mp4;*.mov;*.mkv;*.mp3;*.wav;*.m4a;*.webm;*.flv;*.avi)|*.mp4;*.mov;*.mkv;*.mp3;*.wav;*.m4a;*.webm;*.flv;*.avi|所有文件 (*.*)|*.*",
			Multiselect = true
		};

		if (ofd.ShowDialog(this) == DialogResult.OK)
		{
			AddFiles(ofd.FileNames);
		}
	}

	private void AddLocalMediaFolder()
	{
		using var fbd = new FolderBrowserDialog { Description = "选择包含音视频媒体文件的文件夹" };
		if (fbd.ShowDialog(this) == DialogResult.OK)
		{
			AddFiles(new[] { fbd.SelectedPath });
		}
	}

	private void UpdateInputCount()
	{
		int count = (_currentInputMode == 0)
			? _lstLocalFiles.Items.Count
			: _txtOnlineUrls.Lines.Count(l => !string.IsNullOrWhiteSpace(l) && (l.StartsWith("http://") || l.StartsWith("https://")));

		_urlCountBadge.Text = $"{count} 个任务";
		_urlCountBadge.ForeColor = count > 0 ? EmeraldNeon : TextMuted;
	}

	private async Task StartTranscribeProcessAsync()
	{
		if (_isProcessing) return;
		_cts = new CancellationTokenSource();

		string whisperCli = TranscribeEngine.FindWhisperCliPath();
		if (string.IsNullOrEmpty(whisperCli) || !File.Exists(whisperCli))
		{
			LogMessage("[环境检测] 尚未检测到 Whisper 离线推理引擎，正在自动配置 AI 组件...");
			_statusLabel.Text = "正在自动配置 AI 推理引擎与模型...";
			await AutoInstallAiComponentsAsync();
			whisperCli = TranscribeEngine.FindWhisperCliPath();
			if (string.IsNullOrEmpty(whisperCli) || !File.Exists(whisperCli))
			{
				return;
			}
		}

		string ffmpegCheck = Downloader.DownloadEngine.FindFFmpegPath();
		if (string.IsNullOrEmpty(ffmpegCheck) || !File.Exists(ffmpegCheck))
		{
			LogMessage("[环境检测] 未检测到音视频核心 ffmpeg.exe，无法提取音轨。");
			_statusLabel.Text = "⚠ 未检测到 ffmpeg.exe 组件，无法提取音轨";
			return;
		}

		if (_currentInputMode == 1)
		{
			string ytdlpCheck = Downloader.DownloadEngine.FindYtDlpPath();
			if (string.IsNullOrEmpty(ytdlpCheck) || !File.Exists(ytdlpCheck))
			{
				LogMessage("[环境检测] 转录在线视频需要 yt-dlp 组件辅助拉取音频流，请先配置组件。");
				_statusLabel.Text = "⚠ 缺少 yt-dlp 组件，无法拉取在线视频音轨";
				return;
			}
		}

		string modelChoice = _cboModel.SelectedIndex switch
		{
			0 => "base",
			1 => "small",
			_ => "turbo"
		};
		string modelPath = TranscribeEngine.FindModelPath(modelChoice);
		if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
		{
			LogMessage($"[模型拉取] 本地未找到 {modelChoice} 模型，正在通过高速镜像自动下载...");
			bool dlOk = await TranscribeEngine.DownloadModelAsync(modelChoice, LogMessage, _cts.Token);
			if (dlOk)
			{
				modelPath = TranscribeEngine.FindModelPath(modelChoice);
			}
			else
			{
				modelPath = TranscribeEngine.FindModelPath("small") ?? TranscribeEngine.FindModelPath("base");
			}
		}

		if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
		{
			LogMessage("[模型检测] 本地尚未找到可用的 AI 模型文件，正在自动配置基础模型...");
			_statusLabel.Text = "正在自动下载基础 AI 模型...";
			await AutoInstallAiComponentsAsync();
			modelPath = TranscribeEngine.FindModelPath(modelChoice) ?? TranscribeEngine.FindModelPath("small") ?? TranscribeEngine.FindModelPath("base");
			if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
			{
				return;
			}
		}

		_items.Clear();
		_listViewTasks.Items.Clear();

		if (_currentInputMode == 0)
		{
			for (int i = 0; i < _lstLocalFiles.Items.Count; i++)
			{
				string path = _lstLocalFiles.Items[i].ToString();
				_items.Add(new TranscribeItem
				{
					Index = i + 1,
					SourceName = Path.GetFileName(path),
					SourcePathOrUrl = path,
					LocalMediaFilePath = path,
					IsOnlineUrl = false,
					Status = "待处理"
				});
			}
		}
		else
		{
			var urls = _txtOnlineUrls.Lines
				.Select(l => l.Trim())
				.Where(l => !string.IsNullOrWhiteSpace(l) && (l.StartsWith("http://") || l.StartsWith("https://")))
				.Distinct()
				.ToArray();

			for (int i = 0; i < urls.Length; i++)
			{
				var (platform, _) = Downloader.DownloadEngine.IdentifyPlatform(urls[i]);
				_items.Add(new TranscribeItem
				{
					Index = i + 1,
					SourceName = $"[{platform}] 在线音轨提取",
					SourcePathOrUrl = urls[i],
					IsOnlineUrl = true,
					Status = "等待下载"
				});
			}
		}

		if (_items.Count == 0)
		{
			LogMessage("[提示] 请先添加本地媒体文件或粘贴在线视频链接。");
			_statusLabel.Text = "请先添加本地媒体文件或粘贴在线视频链接";
			return;
		}

		UpdateTaskListView();

		_isProcessing = true;
		_btnStartTranscribe.Enabled = false;
		_btnStopTranscribe.Enabled = true;
		_cts = new CancellationTokenSource();

		string ffmpeg = Downloader.DownloadEngine.FindFFmpegPath();
		string ytdlp = Downloader.DownloadEngine.FindYtDlpPath();
		string language = _cboSourceLang.SelectedIndex switch
		{
			1 => "en",
			2 => "pt",
			3 => "es",
			4 => "zh",
			5 => "fr",
			6 => "de",
			7 => "ru",
			8 => "ja",
			_ => "auto"
		};

		LogMessage($"[任务开始] 共 {_items.Count} 个音视频任务，加载模型: {Path.GetFileName(modelPath)}");

		try
		{
			for (int i = 0; i < _items.Count; i++)
			{
				if (_cts.IsCancellationRequested) break;
				var item = _items[i];
				_statusLabel.Text = $"正在转录处理 ({i + 1}/{_items.Count}): {item.SourceName}";
				item.Status = "处理中";
				UpdateTaskListView();

				string tempAudioDir = null;
				string wavPath = string.Empty;

				try
				{
					// 1. 若为在线链接，仅拉取纯音频流到独立临时目录（不保存视频，不占磁盘空间）
					if (item.IsOnlineUrl)
					{
						item.Status = "音轨拉取中...";
						UpdateTaskListView();
						try
						{
							var audioRes = await TranscribeEngine.FetchOnlineAudioAsync(item.SourcePathOrUrl, ytdlp, ffmpeg, LogMessage, _cts.Token);
							tempAudioDir = audioRes.TempDir;
							item.LocalMediaFilePath = audioRes.MediaPath;
							item.SourceName = SanitizeTitle(audioRes.Title);
						}
						catch (Exception ex)
						{
							item.Status = "在线提取失败";
							item.ErrorMessage = ex.Message;
							UpdateTaskListView();
							continue;
						}
					}

					// 2. 提取 16kHz WAV 音轨
					item.Status = "音频提取中...";
					item.ProgressPercent = 5;
					UpdateTaskListView();
					try
					{
						wavPath = await TranscribeEngine.Extract16kWavAsync(item.LocalMediaFilePath, ffmpeg, LogMessage, _cts.Token);
						item.ProgressPercent = 15;
						UpdateTaskListView();
					}
					catch (Exception ex)
					{
						item.Status = "音频提取失败";
						item.ErrorMessage = ex.Message;
						UpdateTaskListView();
						continue;
					}

					// 自动智能语种推断：若用户选择“自动识别”，但文件名带有明显语种标识（如 -PT 为葡萄牙语，-ES 为西班牙语，-EN 为英语）
					string effectiveLang = language;
					if (string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase))
					{
						string nameUpper = (item.SourceName ?? string.Empty).ToUpperInvariant();
						if (nameUpper.Contains("-PT") || nameUpper.Contains("_PT") || nameUpper.EndsWith("PT")) effectiveLang = "pt";
						else if (nameUpper.Contains("-ES") || nameUpper.Contains("_ES") || nameUpper.EndsWith("ES")) effectiveLang = "es";
						else if (nameUpper.Contains("-EN") || nameUpper.Contains("_EN") || nameUpper.EndsWith("EN")) effectiveLang = "en";
						else if (nameUpper.Contains("-FR") || nameUpper.Contains("_FR") || nameUpper.EndsWith("FR")) effectiveLang = "fr";
					}
					item.DetectedLanguage = effectiveLang.ToUpperInvariant();

					// 3. 执行 Whisper 本地神经网络语音识别
					item.Status = "AI 转录中...";
					UpdateTaskListView();
					try
					{
						item.Segments = await TranscribeEngine.TranscribeAsync(
							wavPath, whisperCli, modelPath, effectiveLang, LogMessage, _cts.Token,
							pct =>
							{
								item.ProgressPercent = Math.Min(80, 15 + (int)(pct * 0.65));
								item.Status = $"AI 转录中 ({pct}%)";
								if (IsHandleCreated)
								{
									try { BeginInvoke(new Action(UpdateTaskListView)); } catch { }
								}
							});
					}
					catch (Exception ex)
					{
						item.Status = "转录异常";
						item.ErrorMessage = ex.Message;
						UpdateTaskListView();
						continue;
					}

					// 4. 判断是否有人声字幕
					if (item.Segments == null || item.Segments.Count == 0)
					{
						item.Status = "未检测到人声对白";
						item.ErrorMessage = "音轨未检测到清晰人声，已识别为纯背景音、环境声或静音素材";
						LogMessage($"[转录检测] 该视频未提取到人声字幕 (识别为纯背景音/无对白/静音素材)。");
						UpdateTaskListView();
						continue;
					}

					item.ProgressPercent = 80;
					UpdateTaskListView();

					// 5. 自动翻译为简体中文
					if (_cboTargetLang.SelectedIndex == 0)
					{
						item.Status = "AI智能翻译中...";
						item.ProgressPercent = 85;
						UpdateTaskListView();
						try
						{
							await TranscribeEngine.TranslateSegmentsAsync(item.Segments, effectiveLang, LogMessage, _cts.Token);
						}
						catch (Exception ex)
						{
							LogMessage($"[翻译提示] 在线翻译暂不可用，已保留原文: {ex.Message}");
						}
					}

					item.Status = "已完成";
					item.ProgressPercent = 100;
					UpdateTaskListView();

					// 刷新字幕视窗
					DisplaySelectedTaskSubtitles();
				}
				catch (Exception itemEx)
				{
					item.Status = "处理异常";
					item.ErrorMessage = itemEx.Message;
					LogMessage($"[任务异常] {item.SourceName}: {itemEx.Message}");
					UpdateTaskListView();
				}
				finally
				{
					// 清理临时文件，确保不占用磁盘空间
					try
					{
						if (!string.IsNullOrEmpty(wavPath) && File.Exists(wavPath)) File.Delete(wavPath);
						if (!string.IsNullOrEmpty(tempAudioDir)) TranscribeEngine.TryDeleteDir(tempAudioDir);
					}
					catch { }
				}
			}
		}
		catch (OperationCanceledException)
		{
			LogMessage("[操作已取消] 用户中止了转录进程。");
		}
		catch (Exception ex)
		{
			LogMessage($"[处理流程异常] {ex.Message}");
		}
		finally
		{
			_isProcessing = false;
			_btnStartTranscribe.Enabled = true;
			_btnStopTranscribe.Enabled = false;
			_statusLabel.Text = $"全部任务转录与翻译流程已结束，共 {_items.Count(it => it.Status == "已完成")} 个文件成功！";
			LogMessage("[流程结束] 全部音视频转录与中文翻译任务已完成。");

			// 自动选中第 1 个任务并展示双轨字幕，保证文本框不为空白
			if (_listViewTasks.Items.Count > 0 && _listViewTasks.SelectedItems.Count == 0)
			{
				_listViewTasks.Items[0].Selected = true;
			}
			DisplaySelectedTaskSubtitles();
		}
	}

	private void StopTranscribeProcess()
	{
		if (_isProcessing && _cts != null)
		{
			_cts.Cancel();
			LogMessage("[中断] 正在停止当前转录任务...");
			_statusLabel.Text = "转录任务已手动中止。";
		}
	}

	private void UpdateTaskListView()
	{
		if (InvokeRequired)
		{
			BeginInvoke(new Action(UpdateTaskListView));
			return;
		}

		while (_listViewTasks.Items.Count > _items.Count)
		{
			_listViewTasks.Items.RemoveAt(_listViewTasks.Items.Count - 1);
		}

		for (int i = 0; i < _items.Count; i++)
		{
			var item = _items[i];
			ListViewItem lvi;
			if (i < _listViewTasks.Items.Count)
			{
				lvi = _listViewTasks.Items[i];
			}
			else
			{
				lvi = new ListViewItem(item.Index.ToString());
				lvi.SubItems.Add(item.SourceName);
				lvi.SubItems.Add(item.DetectedLanguage);
				lvi.SubItems.Add("0");
				lvi.SubItems.Add("0%");
				lvi.SubItems.Add(item.Status);
				_listViewTasks.Items.Add(lvi);
			}

			lvi.Tag = item;
			SetSubItemText(lvi, 0, item.Index.ToString());
			SetSubItemText(lvi, 1, item.SourceName);
			SetSubItemText(lvi, 2, item.DetectedLanguage);
			SetSubItemText(lvi, 3, item.Segments.Count.ToString());
			SetSubItemText(lvi, 4, $"{item.ProgressPercent}%");
			SetSubItemText(lvi, 5, item.Status);

			Color targetColor = TextPrimary;
			if (item.Status == "已完成") targetColor = EmeraldNeon;
			else if (item.Status == "未检测到人声对白") targetColor = AmberNeon;
			else if (item.Status.Contains("失败") || item.Status.Contains("异常")) targetColor = Color.FromArgb(248, 113, 113);
			else if (item.Status.Contains("中")) targetColor = CyanNeon;

			if (lvi.ForeColor != targetColor) lvi.ForeColor = targetColor;
		}

		if (_items.Count > 0)
		{
			int avg = (int)_items.Average(it => it.ProgressPercent);
			int clamped = Math.Max(0, Math.Min(100, avg));
			if (_progressBar.Value != clamped) _progressBar.Value = clamped;
			string progText = $"进度: {avg}% ({_items.Count(it => it.Status == "已完成")}/{_items.Count})";
			if (_progressTextLabel.Text != progText) _progressTextLabel.Text = progText;
		}
	}

	private static void SetSubItemText(ListViewItem item, int subIndex, string text)
	{
		if (item == null || subIndex < 0) return;
		while (item.SubItems.Count <= subIndex)
		{
			item.SubItems.Add(string.Empty);
		}
		if (item.SubItems[subIndex].Text != text)
		{
			item.SubItems[subIndex].Text = text;
		}
	}

	public static void SetDoubleBuffered(Control control)
	{
		if (control == null) return;
		try
		{
			typeof(Control).InvokeMember("DoubleBuffered",
				System.Reflection.BindingFlags.SetProperty |
				System.Reflection.BindingFlags.Instance |
				System.Reflection.BindingFlags.NonPublic,
				null, control, new object[] { true });
		}
		catch { }
	}

	private void DisplaySelectedTaskSubtitles()
	{
		var completedItems = _items.Where(it => it.Segments != null && it.Segments.Count > 0).ToList();
		if (completedItems.Count == 0)
		{
			var any = _items.FirstOrDefault();
			if (any != null && !string.IsNullOrEmpty(any.ErrorMessage))
			{
				_rtbOriginal.Text = $"[{any.ErrorMessage}]";
				_rtbTranslated.Text = "[无需翻译]";
			}
			else
			{
				_rtbOriginal.Clear();
				_rtbTranslated.Clear();
			}
			return;
		}

		var sbOrig = new StringBuilder();
		var sbTrans = new StringBuilder();

		for (int i = 0; i < completedItems.Count; i++)
		{
			var item = completedItems[i];
			if (i > 0)
			{
				sbOrig.AppendLine();
				sbOrig.AppendLine();
				sbTrans.AppendLine();
				sbTrans.AppendLine();
			}

			// 多视频归类展示时标注视频标题，单视频时直接清晰呈现对白
			if (completedItems.Count > 1)
			{
				sbOrig.AppendLine($"【{item.SourceName}】");
				sbTrans.AppendLine($"【{item.SourceName}】");
			}

			foreach (var seg in item.Segments)
			{
				if (!string.IsNullOrWhiteSpace(seg.OriginalText))
				{
					sbOrig.AppendLine(seg.OriginalText.Trim());
				}
				string trans = !string.IsNullOrWhiteSpace(seg.TranslatedText) ? seg.TranslatedText.Trim() : (TranscribeEngine.HasChinese(seg.OriginalText) ? seg.OriginalText.Trim() : "[等待翻译...]");
				if (!string.IsNullOrWhiteSpace(trans))
				{
					sbTrans.AppendLine(trans);
				}
			}
		}

		_rtbOriginal.Text = sbOrig.ToString().TrimEnd();
		_rtbTranslated.Text = sbTrans.ToString().TrimEnd();

		// 如果列表中有选中的任务且不是第一项，滚动至该视频名称；若为第1项或无选中，平稳停留在顶部
		if (_listViewTasks.SelectedItems.Count > 0 && _listViewTasks.SelectedIndices[0] > 0)
		{
			var selItem = _listViewTasks.SelectedItems[0].Tag as TranscribeItem;
			if (selItem != null)
			{
				ScrollToTaskHeader(selItem.SourceName);
			}
		}
		else
		{
			_rtbOriginal.SelectionStart = 0;
			_rtbOriginal.SelectionLength = 0;
			_rtbOriginal.ScrollToCaret();
			_rtbTranslated.SelectionStart = 0;
			_rtbTranslated.SelectionLength = 0;
			_rtbTranslated.ScrollToCaret();
		}
	}

	private void ScrollToTaskHeader(string sourceName)
	{
		if (string.IsNullOrEmpty(sourceName)) return;
		string header = $"【{sourceName}】";
		ScrollBoxToText(_rtbOriginal, header);
		ScrollBoxToText(_rtbTranslated, header);
	}

	private void ScrollBoxToText(RichTextBox box, string text)
	{
		if (box == null || string.IsNullOrEmpty(text)) return;
		int idx = box.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase);
		if (idx >= 0)
		{
			box.SelectionStart = idx;
			box.SelectionLength = 0;
			box.ScrollToCaret();
		}
	}

	private List<TranscribeItem> GetTargetItems()
	{
		var completed = _items.Where(it => it.Segments != null && it.Segments.Count > 0).ToList();
		if (completed.Count == 0) return new List<TranscribeItem>();

		// 只有当用户显式按住 Ctrl/Shift 多选了部分任务时（选中数 > 1 且 < 总完成数），才只针对选中的项；
		// 只要用户单选了某一行或没选，默认全量汇聚复制全部已完成的视频，确保一个视频都不会漏掉！
		if (_listViewTasks.SelectedItems.Count > 1 && _listViewTasks.SelectedItems.Count < completed.Count)
		{
			var selected = _listViewTasks.SelectedItems.Cast<ListViewItem>()
				.Select(lvi => lvi.Tag as TranscribeItem)
				.Where(it => it != null && it.Segments != null && it.Segments.Count > 0)
				.ToList();
			if (selected.Count > 0) return selected;
		}

		return completed;
	}

	private static string FormatSpreadsheetCell(string text)
	{
		if (string.IsNullOrEmpty(text)) return "\"\"";
		string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
		string escaped = normalized.Replace("\"", "\"\"");
		return $"\"{escaped}\"";
	}

	private bool TrySetClipboardText(string text)
	{
		if (string.IsNullOrEmpty(text)) return false;
		try
		{
			Clipboard.SetDataObject(text, true, 5, 50);
			return true;
		}
		catch
		{
			try
			{
				Clipboard.SetText(text);
				return true;
			}
			catch (Exception ex)
			{
				LogMessage($"[剪贴板提示] 写入剪贴板受阻: {ex.Message}");
				_statusLabel.Text = "⚠ 剪贴板正被其他程序占用，请稍后重试";
				return false;
			}
		}
	}

	private void CopyOriginalText()
	{
		var targets = GetTargetItems();
		if (targets.Count == 0)
		{
			LogMessage("[复制原文] 当前没有识别出的原文内容可复制。");
			_statusLabel.Text = "当前没有识别出的原文内容可复制";
			return;
		}

		var sb = new StringBuilder();
		for (int i = 0; i < targets.Count; i++)
		{
			var item = targets[i];
			var lines = new List<string>();
			lines.Add($"【{item.SourceName}】");
			foreach (var seg in item.Segments)
			{
				if (!string.IsNullOrWhiteSpace(seg.OriginalText))
				{
					lines.Add(seg.OriginalText.Trim());
				}
			}
			string videoCellText = string.Join("\n", lines);
			if (i > 0) sb.Append("\r\n");
			sb.Append(FormatSpreadsheetCell(videoCellText));
		}

		if (TrySetClipboardText(sb.ToString().Trim()))
		{
			int totalLines = targets.Sum(t => t.Segments.Count);
			LogMessage($"[复制原文] 已复制全部 {targets.Count} 个视频共 {totalLines} 句识别原文至剪贴板。");
			_statusLabel.Text = $"✓ 已复制 {targets.Count} 个视频共 {totalLines} 句识别原文";
		}
	}

	private void CopyChineseText()
	{
		var targets = GetTargetItems();
		if (targets.Count == 0)
		{
			LogMessage("[复制纯中文] 当前没有中文翻译内容可复制。");
			_statusLabel.Text = "当前没有中文翻译内容可复制";
			return;
		}

		var sb = new StringBuilder();
		for (int i = 0; i < targets.Count; i++)
		{
			var item = targets[i];
			var lines = new List<string>();
			lines.Add($"【{item.SourceName}】");
			foreach (var seg in item.Segments)
			{
				string trans = !string.IsNullOrWhiteSpace(seg.TranslatedText) ? seg.TranslatedText.Trim() : seg.OriginalText.Trim();
				if (!string.IsNullOrWhiteSpace(trans))
				{
					lines.Add(trans);
				}
			}
			string videoCellText = string.Join("\n", lines);
			if (i > 0) sb.Append("\r\n");
			sb.Append(FormatSpreadsheetCell(videoCellText));
		}

		if (TrySetClipboardText(sb.ToString().Trim()))
		{
			int totalLines = targets.Sum(t => t.Segments.Count);
			LogMessage($"[复制纯中文] 已复制全部 {targets.Count} 个视频共 {totalLines} 句纯中文译文至剪贴板。");
			_statusLabel.Text = $"✓ 已复制 {targets.Count} 个视频共 {totalLines} 句纯中文译文";
		}
	}

	private void CopyBilingualText()
	{
		var targets = GetTargetItems();
		if (targets.Count == 0)
		{
			LogMessage("[复制双语] 当前没有双语字幕内容可复制。");
			_statusLabel.Text = "当前没有双语字幕内容可复制";
			return;
		}

		var sb = new StringBuilder();
		for (int i = 0; i < targets.Count; i++)
		{
			var item = targets[i];
			var origLines = new List<string>();
			origLines.Add($"【{item.SourceName}】");
			var transLines = new List<string>();
			transLines.Add($"【{item.SourceName}】");

			foreach (var seg in item.Segments)
			{
				if (!string.IsNullOrWhiteSpace(seg.OriginalText))
				{
					origLines.Add(seg.OriginalText.Trim());
				}
				string trans = !string.IsNullOrWhiteSpace(seg.TranslatedText) ? seg.TranslatedText.Trim() : seg.OriginalText.Trim();
				if (!string.IsNullOrWhiteSpace(trans))
				{
					transLines.Add(trans);
				}
			}

			string origCell = FormatSpreadsheetCell(string.Join("\n", origLines));
			string transCell = FormatSpreadsheetCell(string.Join("\n", transLines));

			if (i > 0) sb.Append("\r\n");
			// TSV 制表符 \t 分隔左列(外文)与右列(中文)，实现 A1 外文、B1 中文并排对照
			sb.Append(origCell).Append("\t").Append(transCell);
		}

		if (TrySetClipboardText(sb.ToString().Trim()))
		{
			int totalLines = targets.Sum(t => t.Segments.Count);
			LogMessage($"[复制双语] 已复制全部 {targets.Count} 个视频共 {totalLines} 句双语对照至剪贴板。");
			_statusLabel.Text = $"✓ 已复制 {targets.Count} 个视频共 {totalLines} 句双语对照";
		}
	}

	private void ExportSrtFile()
	{
		var targets = GetTargetItems();
		if (targets.Count == 0)
		{
			LogMessage("[导出SRT] 当前没有字幕可导出。");
			_statusLabel.Text = "当前没有字幕可导出";
			return;
		}

		using var sfd = new SaveFileDialog
		{
			Title = "导出为标准 SRT 字幕文件",
			Filter = "SRT 字幕文件 (*.srt)|*.srt|所有文件 (*.*)|*.*",
			FileName = targets.Count == 1
				? $"{SanitizeFileName(targets[0].SourceName)}_字幕.srt"
				: $"批量视频字幕_{DateTime.Now:yyyyMMdd_HHmm}.srt"
		};

		if (sfd.ShowDialog(this) == DialogResult.OK)
		{
			bool includeOrig = (_cboSubMode.SelectedIndex == 0 || _cboSubMode.SelectedIndex == 2);
			bool includeTrans = (_cboSubMode.SelectedIndex == 0 || _cboSubMode.SelectedIndex == 1);
			var sb = new StringBuilder();
			foreach (var item in targets)
			{
				sb.Append(TranscribeEngine.ExportToSrt(item.Segments, includeOrig, includeTrans));
			}
			File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
			LogMessage($"[导出SRT] 字幕文件已成功保存至: {sfd.FileName}");
			_statusLabel.Text = $"✓ SRT 字幕已保存: {Path.GetFileName(sfd.FileName)}";
		}
	}

	private void ExportTxtFile()
	{
		var targets = GetTargetItems();
		if (targets.Count == 0)
		{
			LogMessage("[导出TXT] 当前没有文本可导出。");
			_statusLabel.Text = "当前没有文本可导出";
			return;
		}

		using var sfd = new SaveFileDialog
		{
			Title = "导出为带时间戳的双语字幕 TXT 文本",
			Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
			FileName = targets.Count == 1
				? $"{SanitizeFileName(targets[0].SourceName)}_字幕对照.txt"
				: $"批量视频字幕对照_{DateTime.Now:yyyyMMdd_HHmm}.txt"
		};

		if (sfd.ShowDialog(this) == DialogResult.OK)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < targets.Count; i++)
			{
				var item = targets[i];
				if (i > 0) sb.AppendLine().AppendLine();
				sb.AppendLine($"========================================");
				sb.AppendLine($"媒体文件: {item.SourceName}");
				sb.AppendLine($"========================================");
				string txt = TranscribeEngine.ExportToTxt(item.Segments, includeOriginal: true, includeTranslated: true);
				sb.AppendLine(txt);
			}
			File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
			LogMessage($"[导出TXT] TXT 对照字幕文件已成功保存至: {sfd.FileName}");
			_statusLabel.Text = $"✓ TXT 字幕已保存: {Path.GetFileName(sfd.FileName)}";
		}
	}

	private static string SanitizeTitle(string title)
	{
		if (string.IsNullOrWhiteSpace(title)) return "在线音轨";
		string clean = title.Replace("\r", " ").Replace("\n", " ").Trim();
		return string.IsNullOrEmpty(clean) ? "在线音轨" : clean;
	}

	private static string SanitizeFileName(string name)
	{
		if (string.IsNullOrWhiteSpace(name)) return "批量视频";
		char[] invalid = Path.GetInvalidFileNameChars();
		var sb = new StringBuilder();
		foreach (char c in name)
		{
			sb.Append(invalid.Contains(c) ? '_' : c);
		}
		string res = sb.ToString().Trim();
		if (res.Length > 60) res = res.Substring(0, 60);
		return string.IsNullOrEmpty(res) ? "批量视频" : res;
	}

	private void LogMessage(string msg)
	{
		if (string.IsNullOrWhiteSpace(msg)) return;
		if (InvokeRequired)
		{
			BeginInvoke(new Action<string>(LogMessage), msg);
			return;
		}

		string line = $"[{DateTime.Now:HH:mm:ss}] {msg}\r\n";
		_logBox.AppendText(line);
		_logBox.SelectionStart = _logBox.TextLength;
		_logBox.ScrollToCaret();
	}

	// ====================== 科技感样式绘制辅助 ======================

	private static void DrawTechBorder(Graphics g, Rectangle bounds, Color borderCol, int radius)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		Rectangle r = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
		using var path = CreateRoundedPath(r, radius);
		using var pen = new Pen(borderCol, 1f);
		g.DrawPath(pen, path);
	}

	private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
	{
		GraphicsPath path = new GraphicsPath();
		if (rect.Width <= 0 || rect.Height <= 0) return path;
		int d = radius * 2;
		if (d > rect.Width) d = rect.Width;
		if (d > rect.Height) d = rect.Height;
		Rectangle arc = new Rectangle(rect.X, rect.Y, d, d);
		path.AddArc(arc, 180, 90);
		arc.X = rect.Right - d;
		path.AddArc(arc, 270, 90);
		arc.Y = rect.Bottom - d;
		path.AddArc(arc, 0, 90);
		arc.X = rect.Left;
		path.AddArc(arc, 90, 90);
		path.CloseFigure();
		return path;
	}

	private Label CreateTechPill(string module, string status, Color dotColor)
	{
		return new Label
		{
			Text = $"● {module}: {status}",
			Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold),
			ForeColor = dotColor,
			BackColor = Color.FromArgb(20, 32, 54),
			Padding = new Padding(6, 4, 6, 4),
			Margin = new Padding(3, 0, 3, 0),
			AutoSize = true
		};
	}

	private Button MakeTechButton(string text, int width, int height, Color baseColor, EventHandler onClick)
	{
		Button b = new Button
		{
			Text = text,
			Size = new Size(width, height),
			FlatStyle = FlatStyle.Flat,
			BackColor = baseColor,
			ForeColor = Color.White,
			Cursor = Cursors.Hand,
			Font = new Font("Microsoft YaHei UI", 9f)
		};
		b.FlatAppearance.BorderSize = 0;
		b.Click += onClick;

		bool isHover = false;
		bool isPressed = false;

		b.MouseEnter += (s, e) => { isHover = true; b.Invalidate(); };
		b.MouseLeave += (s, e) => { isHover = false; isPressed = false; b.Invalidate(); };
		b.MouseDown += (s, e) => { isPressed = true; b.Invalidate(); };
		b.MouseUp += (s, e) => { isPressed = false; b.Invalidate(); };

		b.Paint += (s, e) =>
		{
			Graphics g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle bounds = new Rectangle(0, 0, b.Width - 1, b.Height - 1);

			Color topGrad, botGrad, borderColor;
			if (isPressed)
			{
				topGrad = ControlPaint.Dark(baseColor, 0.2f);
				botGrad = baseColor;
				borderColor = CyanNeon;
			}
			else if (isHover)
			{
				topGrad = ControlPaint.Light(baseColor, 0.3f);
				botGrad = baseColor;
				borderColor = CyanNeon;
			}
			else
			{
				topGrad = ControlPaint.Light(baseColor, 0.15f);
				botGrad = ControlPaint.Dark(baseColor, 0.1f);
				borderColor = Color.FromArgb(baseColor.R / 2, baseColor.G / 2 + 30, baseColor.B / 2 + 50);
			}

			using var path = CreateRoundedPath(bounds, 5);
			using (var lgb = new LinearGradientBrush(bounds, topGrad, botGrad, LinearGradientMode.Vertical))
			{
				g.FillPath(lgb, path);
			}

			using (var specPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1f))
			{
				g.DrawLine(specPen, bounds.Left + 3, bounds.Top + 1, bounds.Right - 3, bounds.Top + 1);
			}

			using (var borderPen = new Pen(borderColor, 1f))
			{
				g.DrawPath(borderPen, path);
			}

			TextRenderer.DrawText(g, b.Text, b.Font, bounds, Color.White,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		};

		return b;
	}
}
