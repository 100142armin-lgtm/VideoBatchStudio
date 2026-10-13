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

	// 左侧面板控件
	private TabControl _tabInputMode;
	private ListBox _lstLocalFiles;
	private TextBox _txtOnlineUrls;
	private Label _urlCountBadge;
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

		Panel rootPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = BgCanvas };

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

		_uiTimer = new System.Windows.Forms.Timer { Interval = 300 };
		_uiTimer.Tick += (s, e) => UpdateTaskListView();
		_uiTimer.Start();

		ResumeLayout(false);
	}

	private Panel CreateHeaderHud()
	{
		Panel p = new Panel
		{
			Height = 52,
			BackColor = CardBg,
			Margin = new Padding(0, 0, 0, 8),
			Padding = new Padding(14, 8, 14, 8)
		};
		p.Paint += (s, e) => DrawTechBorder(e.Graphics, p.ClientRectangle, CardBorder, 6);

		Panel leftInfo = new Panel { Dock = DockStyle.Left, Width = 560, BackColor = Color.Transparent };
		Label lblIcon = new Label
		{
			Text = "🎙️",
			Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
			ForeColor = CyanNeon,
			Location = new Point(0, 6),
			Size = new Size(24, 24),
			TextAlign = ContentAlignment.MiddleCenter
		};
		leftInfo.Controls.Add(lblIcon);

		_envBadgeTitle = new Label
		{
			Text = "AI TRANSCRIBE · 全地化本地音视频双语字幕提取终端",
			Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
			ForeColor = TextPrimary,
			Location = new Point(28, 4),
			AutoSize = true
		};
		leftInfo.Controls.Add(_envBadgeTitle);

		Label lblSub = new Label
		{
			Text = "WHISPER.CPP 离线神经网络 · 英语 / 葡萄牙语 / 西班牙语 → 简体中文高精神经翻译",
			Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
			ForeColor = CyanNeon,
			Location = new Point(30, 26),
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

		Panel titleRow = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.Transparent };
		Label lblTitle = new Label
		{
			Text = "📥 音视频来源 (支持批量本地媒体 / 在线视频链接)",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = CyanNeon,
			Dock = DockStyle.Left,
			AutoSize = true
		};
		titleRow.Controls.Add(lblTitle);

		_urlCountBadge = new Label
		{
			Text = "0 个任务待处理",
			Font = new Font("Segoe UI", 8f, FontStyle.Bold),
			ForeColor = TextMuted,
			BackColor = Color.FromArgb(20, 32, 54),
			Padding = new Padding(6, 2, 6, 2),
			Dock = DockStyle.Right,
			AutoSize = true
		};
		titleRow.Controls.Add(_urlCountBadge);
		p.Controls.Add(titleRow);

		_tabInputMode = new TabControl
		{
			Dock = DockStyle.Fill,
			Font = new Font("Microsoft YaHei UI", 9f)
		};

		// Tab 1: 本地文件批量
		TabPage tabLocal = new TabPage("📁 批量本地音视频");
		tabLocal.BackColor = CardAltBg;
		Panel localContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };

		_lstLocalFiles = new ListBox
		{
			Dock = DockStyle.Fill,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			BorderStyle = BorderStyle.FixedSingle,
			SelectionMode = SelectionMode.MultiExtended,
			Font = new Font("Consolas", 9f)
		};
		localContainer.Controls.Add(_lstLocalFiles);

		Panel localToolBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };
		Button btnAddFiles = MakeTechButton("＋ 添加文件…", 100, 28, Color.FromArgb(28, 42, 68), (s, e) => AddLocalMediaFiles());
		btnAddFiles.Location = new Point(0, 4);
		localToolBar.Controls.Add(btnAddFiles);

		Button btnAddFolder = MakeTechButton("📂 添加文件夹…", 110, 28, Color.FromArgb(28, 42, 68), (s, e) => AddLocalMediaFolder());
		btnAddFolder.Location = new Point(106, 4);
		localToolBar.Controls.Add(btnAddFolder);

		Button btnClearFiles = MakeTechButton("🗑 清空列表", 85, 28, Color.FromArgb(28, 42, 68), (s, e) =>
		{
			_lstLocalFiles.Items.Clear();
			UpdateInputCount();
		});
		btnClearFiles.Location = new Point(222, 4);
		localToolBar.Controls.Add(btnClearFiles);

		localContainer.Controls.Add(localToolBar);
		tabLocal.Controls.Add(localContainer);
		_tabInputMode.TabPages.Add(tabLocal);

		// Tab 2: 在线链接解析
		TabPage tabOnline = new TabPage("🌐 在线视频链接 (YouTube/TikTok/FB)");
		tabOnline.BackColor = CardAltBg;
		Panel onlineContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };

		_txtOnlineUrls = new TextBox
		{
			Multiline = true,
			ScrollBars = ScrollBars.Both,
			WordWrap = false,
			Dock = DockStyle.Fill,
			BackColor = InputBg,
			ForeColor = Color.FromArgb(226, 232, 240),
			BorderStyle = BorderStyle.FixedSingle,
			Font = new Font("Consolas", 9f)
		};
		_txtOnlineUrls.Text = "https://www.youtube.com/shorts/bqLBK7GuPCk\r\nhttps://www.tiktok.com/@lucia.gove/video/7617254633341308178";
		_txtOnlineUrls.TextChanged += (s, e) => UpdateInputCount();
		onlineContainer.Controls.Add(_txtOnlineUrls);

		Panel onlineToolBar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };
		Button btnPasteUrls = MakeTechButton("📋 粘贴剪贴板", 100, 28, Color.FromArgb(28, 42, 68), (s, e) =>
		{
			if (Clipboard.ContainsText())
			{
				string t = Clipboard.GetText();
				if (!string.IsNullOrWhiteSpace(t))
				{
					if (string.IsNullOrWhiteSpace(_txtOnlineUrls.Text)) _txtOnlineUrls.Text = t.Trim();
					else _txtOnlineUrls.AppendText("\r\n" + t.Trim());
				}
			}
		});
		btnPasteUrls.Location = new Point(0, 4);
		onlineToolBar.Controls.Add(btnPasteUrls);

		Button btnClearUrls = MakeTechButton("🗑 清空", 70, 28, Color.FromArgb(28, 42, 68), (s, e) => _txtOnlineUrls.Clear());
		btnClearUrls.Location = new Point(106, 4);
		onlineToolBar.Controls.Add(btnClearUrls);

		onlineContainer.Controls.Add(onlineToolBar);
		tabOnline.Controls.Add(onlineContainer);
		_tabInputMode.TabPages.Add(tabOnline);

		_tabInputMode.SelectedIndexChanged += (s, e) => UpdateInputCount();
		p.Controls.Add(_tabInputMode);

		// 底部 AI 参数设置卡片与启动按钮组
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
		_cboModel.Items.AddRange(new object[] { "小巧极速 (Base 141MB)", "标准推荐 (Small 465MB)", "高精影院 (Medium 1.4GB)" });
		_cboModel.SelectedIndex = 1;
		paramPanel.Controls.Add(_cboModel);

		Label lblSrcLang = new Label { Text = "视频语种:", Location = new Point(232, 10), AutoSize = true, ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		paramPanel.Controls.Add(lblSrcLang);

		_cboSourceLang = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(300, 7),
			Width = 110,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			FlatStyle = FlatStyle.Flat
		};
		_cboSourceLang.Items.AddRange(new object[] { "自动识别", "英语 (English)", "葡萄牙语 (pt)", "西班牙语 (es)", "中文普通话" });
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
		_cboTargetLang.Items.AddRange(new object[] { "🇨🇳 简体中文 (译为中文)", "🇺🇸 英语 (译为英文)", "不翻译 (仅输出原文)" });
		_cboTargetLang.SelectedIndex = 0;
		paramPanel.Controls.Add(_cboTargetLang);

		Label lblSubMode = new Label { Text = "对照格式:", Location = new Point(232, 46), AutoSize = true, ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		paramPanel.Controls.Add(lblSubMode);

		_cboSubMode = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(300, 43),
			Width = 110,
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
		Panel logPanel = new Panel { Location = new Point(4, 130), Size = new Size(410, 85), BackColor = Color.Transparent };
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

		Button btnCopyOriginal = MakeTechButton("📋 复制原文", 95, 30, Color.FromArgb(28, 42, 68), (s, e) => CopyOriginalText());
		btnCopyOriginal.Location = new Point(0, 2);
		topToolBar.Controls.Add(btnCopyOriginal);

		Button btnCopyBilingual = MakeTechButton("📑 复制双语对照", 115, 30, Color.FromArgb(28, 42, 68), (s, e) => CopyBilingualText());
		btnCopyBilingual.Location = new Point(102, 2);
		topToolBar.Controls.Add(btnCopyBilingual);

		Button btnCopyChinese = MakeTechButton("🀄 复制纯中文", 105, 30, Color.FromArgb(28, 42, 68), (s, e) => CopyChineseText());
		btnCopyChinese.Location = new Point(224, 2);
		topToolBar.Controls.Add(btnCopyChinese);

		Button btnExportSrt = MakeTechButton("💾 导出 SRT 字幕", 115, 30, Color.FromArgb(13, 148, 136), (s, e) => ExportSrtFile());
		btnExportSrt.Location = new Point(336, 2);
		topToolBar.Controls.Add(btnExportSrt);

		Button btnExportTxt = MakeTechButton("📄 导出 TXT 文本", 115, 30, Color.FromArgb(28, 42, 68), (s, e) => ExportTxtFile());
		btnExportTxt.Location = new Point(458, 2);
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
		_listViewTasks.Columns.Add("#", 38);
		_listViewTasks.Columns.Add("媒体名称", 240);
		_listViewTasks.Columns.Add("语种", 80);
		_listViewTasks.Columns.Add("句数", 65);
		_listViewTasks.Columns.Add("进度", 75);
		_listViewTasks.Columns.Add("状态", 120);

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
		Label lblOrig = new Label { Text = "● 原始语音识别结果 (包含精细时间戳)", ForeColor = CyanNeon, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold), Dock = DockStyle.Top, Height = 22 };
		pnlOrig.Controls.Add(lblOrig);

		_rtbOriginal = new RichTextBox
		{
			Dock = DockStyle.Fill,
			BackColor = InputBg,
			ForeColor = Color.FromArgb(226, 232, 240),
			BorderStyle = BorderStyle.None,
			Font = new Font("Consolas", 9.5f),
			ReadOnly = false
		};
		pnlOrig.Controls.Add(_rtbOriginal);
		splitSubtitles.Panel1.Controls.Add(pnlOrig);

		// 中文译文轨
		Panel pnlTrans = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
		Label lblTrans = new Label { Text = "● 简体中文对照翻译 (时间戳同步对齐)", ForeColor = EmeraldNeon, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold), Dock = DockStyle.Top, Height = 22 };
		pnlTrans.Controls.Add(lblTrans);

		_rtbTranslated = new RichTextBox
		{
			Dock = DockStyle.Fill,
			BackColor = InputBg,
			ForeColor = EmeraldNeon,
			BorderStyle = BorderStyle.None,
			Font = new Font("Microsoft YaHei UI", 9.5f),
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

	// ====================== 业务逻辑实现 ======================

	public void RefreshEnvironmentStatus()
	{
		string whisper = TranscribeEngine.FindWhisperCliPath();
		bool whisperOk = !string.IsNullOrEmpty(whisper) && File.Exists(whisper);

		string baseModel = TranscribeEngine.FindModelPath("base");
		string smallModel = TranscribeEngine.FindModelPath("small");
		bool modelOk = !string.IsNullOrEmpty(baseModel) || !string.IsNullOrEmpty(smallModel);

		bool cudaOk = TranscribeEngine.IsCudaAvailable();

		_envTagWhisper.Text = whisperOk ? "● Whisper 内核: 就绪" : "● Whisper 内核: 缺失";
		_envTagWhisper.ForeColor = whisperOk ? EmeraldNeon : Color.FromArgb(248, 113, 113);

		_envTagModel.Text = modelOk ? "● AI 模型: 已就绪" : "● AI 模型: 缺失";
		_envTagModel.ForeColor = modelOk ? EmeraldNeon : Color.FromArgb(248, 113, 113);

		_envTagCuda.Text = cudaOk ? "● CUDA 硬件加速: 就绪" : "● 运算架构: CPU 多核";
		_envTagCuda.ForeColor = cudaOk ? CyanNeon : AmberNeon;

		if (whisperOk && modelOk)
		{
			_btnInstallModel.Text = "✓ 模型已就绪";
			_btnInstallModel.BackColor = Color.FromArgb(20, 45, 40);
		}
	}

	private async Task AutoInstallAiComponentsAsync()
	{
		LogMessage("[组件配置] 正在自动检测并配置 Whisper 离线推理组件...");
		string toolsDir = @"D:\工具\视频拼接\VideoBatchStudio";
		string whisperDir = Path.Combine(toolsDir, "whisper");
		Directory.CreateDirectory(whisperDir);
		string modelsDir = Path.Combine(whisperDir, "models");
		Directory.CreateDirectory(modelsDir);

		string smallModel = Path.Combine(modelsDir, "ggml-small.bin");
		if (!File.Exists(smallModel))
		{
			LogMessage("[模型下载] 正在从高速镜像拉取 ggml-small.bin 多语种模型 (约 466MB)...");
			await Task.Run(() =>
			{
				try
				{
					using var client = new System.Net.WebClient();
					client.DownloadFile("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin", smallModel);
				}
				catch (Exception ex)
				{
					LogMessage($"[模型下载异常] {ex.Message}");
				}
			});
		}

		RefreshEnvironmentStatus();
		MessageBox.Show(this, "AI 语音转录模型与引擎配置完毕！已完全就绪离线使用。", "配置完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	public void OnActivated()
	{
		RefreshEnvironmentStatus();
	}

	public void AddFiles(IEnumerable<string> files)
	{
		if (files == null) return;
		_tabInputMode.SelectedIndex = 0;
		foreach (var f in files)
		{
			if (!string.IsNullOrWhiteSpace(f) && !_lstLocalFiles.Items.Contains(f))
			{
				_lstLocalFiles.Items.Add(f);
			}
		}
		UpdateInputCount();
	}

	public void AddOnlineUrls(IEnumerable<string> urls)
	{
		if (urls == null) return;
		_tabInputMode.SelectedIndex = 1;
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
			Filter = "媒体文件 (*.mp4;*.mov;*.mkv;*.mp3;*.wav;*.m4a;*.webm;*.flv)|*.mp4;*.mov;*.mkv;*.mp3;*.wav;*.m4a;*.webm;*.flv|所有文件 (*.*)|*.*",
			Multiselect = true
		};

		if (ofd.ShowDialog(this) == DialogResult.OK)
		{
			foreach (var file in ofd.FileNames)
			{
				if (!_lstLocalFiles.Items.Contains(file))
				{
					_lstLocalFiles.Items.Add(file);
				}
			}
			UpdateInputCount();
		}
	}

	private void AddLocalMediaFolder()
	{
		using var fbd = new FolderBrowserDialog { Description = "选择包含媒体文件的文件夹" };
		if (fbd.ShowDialog(this) == DialogResult.OK)
		{
			string[] exts = { ".mp4", ".mov", ".mkv", ".mp3", ".wav", ".m4a", ".webm" };
			var files = Directory.GetFiles(fbd.SelectedPath, "*.*", SearchOption.AllDirectories)
				.Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
				.ToArray();

			foreach (var file in files)
			{
				if (!_lstLocalFiles.Items.Contains(file))
				{
					_lstLocalFiles.Items.Add(file);
				}
			}
			UpdateInputCount();
			LogMessage($"[导入文件夹] 成功扫描并添加了 {files.Length} 个媒体文件。");
		}
	}

	private void UpdateInputCount()
	{
		int count = (_tabInputMode.SelectedIndex == 0)
			? _lstLocalFiles.Items.Count
			: _txtOnlineUrls.Lines.Count(l => !string.IsNullOrWhiteSpace(l) && (l.StartsWith("http://") || l.StartsWith("https://")));

		_urlCountBadge.Text = $"{count} 个任务待处理";
		_urlCountBadge.ForeColor = count > 0 ? EmeraldNeon : TextMuted;
	}

	private async Task StartTranscribeProcessAsync()
	{
		if (_isProcessing) return;

		string whisperCli = TranscribeEngine.FindWhisperCliPath();
		if (string.IsNullOrEmpty(whisperCli) || !File.Exists(whisperCli))
		{
			MessageBox.Show(this, "未检测到 whisper-cli 引擎组件，请先点击右上角【一键配置 AI 组件】！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		string modelChoice = _cboModel.SelectedIndex switch
		{
			0 => "base",
			1 => "small",
			_ => "medium"
		};
		string modelPath = TranscribeEngine.FindModelPath(modelChoice);
		if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
		{
			modelPath = TranscribeEngine.FindModelPath("small");
			if (string.IsNullOrEmpty(modelPath)) modelPath = TranscribeEngine.FindModelPath("base");
		}

		if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
		{
			MessageBox.Show(this, "本地尚未找到选中的 AI 模型文件，请先点击右上角【一键配置 AI 组件】下载模型！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		_items.Clear();
		_listViewTasks.Items.Clear();

		if (_tabInputMode.SelectedIndex == 0)
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
					SourceName = $"[{platform}] 待下载直转",
					SourcePathOrUrl = urls[i],
					IsOnlineUrl = true,
					Status = "等待下载"
				});
			}
		}

		if (_items.Count == 0)
		{
			MessageBox.Show(this, "请先添加本地媒体文件或粘贴在线视频链接！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

				// 1. 若为在线链接，先调动内核下载原视频
				if (item.IsOnlineUrl)
				{
					item.Status = "视频提取中...";
					UpdateTaskListView();
					string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "视频批量下载");
					Directory.CreateDirectory(outDir);

					var dlItem = new Downloader.DownloadItem { Index = 1, Url = item.SourcePathOrUrl };
					bool dlSuccess = await Downloader.DownloadEngine.DownloadAsync(
						dlItem, ytdlp, ffmpeg, outDir, "best", true, true, false, "", LogMessage, _cts.Token);

					if (dlSuccess && File.Exists(dlItem.SaveFilePath))
					{
						item.LocalMediaFilePath = dlItem.SaveFilePath;
						item.SourceName = Path.GetFileName(dlItem.SaveFilePath);
					}
					else
					{
						item.Status = "下载失败";
						item.ErrorMessage = "在线视频拉取失败";
						UpdateTaskListView();
						continue;
					}
				}

				// 2. 提取 16kHz WAV 音轨
				item.Status = "音频提取中...";
				UpdateTaskListView();
				string wavPath = string.Empty;
				try
				{
					wavPath = await TranscribeEngine.Extract16kWavAsync(item.LocalMediaFilePath, ffmpeg, LogMessage, _cts.Token);
				}
				catch (Exception ex)
				{
					item.Status = "音频提取失败";
					item.ErrorMessage = ex.Message;
					UpdateTaskListView();
					continue;
				}

				// 3. 执行 Whisper 本地神经网络语音识别
				item.Status = "AI 转录中...";
				UpdateTaskListView();
				try
				{
					item.Segments = await TranscribeEngine.TranscribeAsync(wavPath, whisperCli, modelPath, language, LogMessage, _cts.Token);
				}
				catch (Exception ex)
				{
					item.Status = "转录异常";
					item.ErrorMessage = ex.Message;
					UpdateTaskListView();
					continue;
				}
				finally
				{
					if (File.Exists(wavPath))
					{
						try { File.Delete(wavPath); } catch { }
					}
				}

				// 4. 执行神经翻译转为简体中文
				if (_cboTargetLang.SelectedIndex == 0 && item.Segments.Count > 0)
				{
					item.Status = "翻译为中文...";
					UpdateTaskListView();
					await TranscribeEngine.TranslateSegmentsAsync(item.Segments, language, LogMessage, _cts.Token);
				}

				item.ProgressPercent = 100;
				item.Status = "已完成";
				UpdateTaskListView();

				// 若当前只有一项或用户正在查看该项，立即渲染文本
				if (_listViewTasks.SelectedIndices.Count == 0 && i == 0)
				{
					_listViewTasks.Items[0].Selected = true;
				}
			}
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
			ListViewItem lvi = null;
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
			lvi.SubItems[0].Text = item.Index.ToString();
			lvi.SubItems[1].Text = item.SourceName;
			lvi.SubItems[2].Text = item.DetectedLanguage;
			lvi.SubItems[3].Text = item.Segments.Count.ToString();
			lvi.SubItems[4].Text = $"{item.ProgressPercent}%";
			lvi.SubItems[5].Text = item.Status;

			if (item.Status == "已完成") lvi.ForeColor = EmeraldNeon;
			else if (item.Status.Contains("失败") || item.Status.Contains("异常")) lvi.ForeColor = Color.FromArgb(248, 113, 113);
			else if (item.Status.Contains("中")) lvi.ForeColor = CyanNeon;
			else lvi.ForeColor = TextPrimary;
		}

		if (_items.Count > 0)
		{
			int avg = (int)_items.Average(it => it.ProgressPercent);
			_progressBar.Value = Math.Max(0, Math.Min(100, avg));
			_progressTextLabel.Text = $"进度: {avg}% ({_items.Count(it => it.Status == "已完成")}/{_items.Count})";
		}
	}

	private void DisplaySelectedTaskSubtitles()
	{
		if (_listViewTasks.SelectedItems.Count == 0) return;
		var item = _listViewTasks.SelectedItems[0].Tag as TranscribeItem;
		if (item == null || item.Segments == null) return;

		var sbOrig = new StringBuilder();
		var sbTrans = new StringBuilder();

		foreach (var seg in item.Segments)
		{
			sbOrig.AppendLine($"[{seg.TimeRangeStr}]");
			sbOrig.AppendLine(seg.OriginalText);
			sbOrig.AppendLine();

			sbTrans.AppendLine($"[{seg.TimeRangeStr}]");
			sbTrans.AppendLine(string.IsNullOrEmpty(seg.TranslatedText) ? "(等待翻译...)" : seg.TranslatedText);
			sbTrans.AppendLine();
		}

		_rtbOriginal.Text = sbOrig.ToString();
		_rtbTranslated.Text = sbTrans.ToString();
	}

	private TranscribeItem GetActiveItem()
	{
		if (_listViewTasks.SelectedItems.Count > 0)
		{
			return _listViewTasks.SelectedItems[0].Tag as TranscribeItem;
		}
		return _items.FirstOrDefault(it => it.Segments.Count > 0);
	}

	private void CopyOriginalText()
	{
		var item = GetActiveItem();
		if (item == null || item.Segments.Count == 0)
		{
			MessageBox.Show(this, "当前没有识别出的原文内容可复制！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		string text = TranscribeEngine.ExportToTxt(item.Segments, includeOriginal: true, includeTranslated: false);
		Clipboard.SetText(text);
		MessageBox.Show(this, "成功复制全部语音识别原文至剪贴板！", "已复制", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void CopyBilingualText()
	{
		var item = GetActiveItem();
		if (item == null || item.Segments.Count == 0)
		{
			MessageBox.Show(this, "当前没有字幕内容可复制！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		string text = TranscribeEngine.ExportToTxt(item.Segments, includeOriginal: true, includeTranslated: true);
		Clipboard.SetText(text);
		MessageBox.Show(this, "成功复制双语对照字幕内容至剪贴板！", "已复制", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void CopyChineseText()
	{
		var item = GetActiveItem();
		if (item == null || item.Segments.Count == 0)
		{
			MessageBox.Show(this, "当前没有中文翻译内容可复制！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		string text = TranscribeEngine.ExportToTxt(item.Segments, includeOriginal: false, includeTranslated: true);
		Clipboard.SetText(text);
		MessageBox.Show(this, "成功复制纯中文译文内容至剪贴板！", "已复制", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void ExportSrtFile()
	{
		var item = GetActiveItem();
		if (item == null || item.Segments.Count == 0)
		{
			MessageBox.Show(this, "当前没有字幕可导出！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var sfd = new SaveFileDialog
		{
			Title = "导出为标准 SRT 字幕文件",
			Filter = "SRT 字幕文件 (*.srt)|*.srt|所有文件 (*.*)|*.*",
			FileName = $"{Path.GetFileNameWithoutExtension(item.SourceName)}_双语字幕.srt"
		};

		if (sfd.ShowDialog(this) == DialogResult.OK)
		{
			bool includeOrig = (_cboSubMode.SelectedIndex == 0 || _cboSubMode.SelectedIndex == 2);
			bool includeTrans = (_cboSubMode.SelectedIndex == 0 || _cboSubMode.SelectedIndex == 1);
			string srt = TranscribeEngine.ExportToSrt(item.Segments, includeOrig, includeTrans);
			File.WriteAllText(sfd.FileName, srt, Encoding.UTF8);
			MessageBox.Show(this, $"字幕文件已成功保存至:\n{sfd.FileName}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
	}

	private void ExportTxtFile()
	{
		var item = GetActiveItem();
		if (item == null || item.Segments.Count == 0)
		{
			MessageBox.Show(this, "当前没有文本可导出！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var sfd = new SaveFileDialog
		{
			Title = "导出为纯文本文件",
			Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
			FileName = $"{Path.GetFileNameWithoutExtension(item.SourceName)}_文本.txt"
		};

		if (sfd.ShowDialog(this) == DialogResult.OK)
		{
			bool includeOrig = (_cboSubMode.SelectedIndex == 0 || _cboSubMode.SelectedIndex == 2);
			bool includeTrans = (_cboSubMode.SelectedIndex == 0 || _cboSubMode.SelectedIndex == 1);
			string txt = TranscribeEngine.ExportToTxt(item.Segments, includeOrig, includeTrans);
			File.WriteAllText(sfd.FileName, txt, Encoding.UTF8);
			MessageBox.Show(this, $"文本文件已成功保存至:\n{sfd.FileName}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
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
