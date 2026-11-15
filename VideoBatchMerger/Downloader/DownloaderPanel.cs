using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoBatchMerger.Downloader;

/// <summary>
/// 视频下载终端控制台（极客科技流风格，支持 Facebook Reel、YouTube、TikTok、Google Drive 双模直链等）
/// </summary>
internal sealed class DownloaderPanel : UserControl
{
	private readonly MainForm _mainForm;
	private readonly List<DownloadItem> _items = new List<DownloadItem>();
	private CancellationTokenSource _cts;
	private bool _isDownloading;

	// 配色系统（深空科技矩阵暗夜主题）
	private static readonly Color BgCanvas = Color.FromArgb(10, 15, 26);
	private static readonly Color CardBg = Color.FromArgb(17, 26, 44);
	private static readonly Color CardAltBg = Color.FromArgb(14, 21, 37);
	private static readonly Color CardBorder = Color.FromArgb(32, 50, 82);
	private static readonly Color InputBg = Color.FromArgb(9, 14, 24);
	private static readonly Color TextPrimary = Color.FromArgb(241, 245, 249);
	private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
	private static readonly Color CyanNeon = Color.FromArgb(56, 189, 248);
	private static readonly Color EmeraldNeon = Color.FromArgb(52, 211, 153);

	// UI 控件
	private Label _envBadgeTitle;
	private Label _envTagYtDlp;
	private Label _envTagFFmpeg;
	private Label _envTagFFprobe;
	private Button _btnCheckEnv;
	private Button _btnInstallYtDlp;

	private TextBox _txtOutputDir;
	private Button _btnBrowseDir;
	private Button _btnOpenFolderQuick;
	private ComboBox _cboQuality;
	private ComboBox _cboThreads;
	private ComboBox _cboProxy;
	private TextBox _txtCustomProxy;
	private CheckBox _chkResume;
	private CheckBox _chkSkipExisting;
	private CheckBox _chkSubfolderByPlatform;
	private CheckBox _chkPlaylistFolder;

	private TextBox _txtUrls;
	private bool _hasDefaultSampleUrls = true;
	private Label _urlCountBadge;
	private Button _btnReadClipboard;
	private Button _btnClearUrls;
	private Button _btnParsePreview;
	private Button _btnStartDownload;
	private Button _btnStopDownload;

	private ListView _listView;
	private ProgressBar _progressBar;
	private Label _progressTextLabel;
	private RichTextBox _logBox;
	private Label _statusLabel;
	private System.Windows.Forms.Timer _refreshTimer;

	public DownloaderPanel(MainForm mainForm)
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
		Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);

		Panel rootPanel = new Panel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12),
			BackColor = BgCanvas
		};

		// 1. 顶部 Header 状态栏
		Panel headerHud = CreateHeaderHud();
		headerHud.Dock = DockStyle.Top;

		// 2. 核心参数设置栏
		Panel settingsBar = CreateSettingsBar();
		settingsBar.Dock = DockStyle.Top;

		// 3. 中间内容分栏 (左输入 410px，右侧 Matrix 列表与终端 Console)
		SplitContainer splitMain = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Vertical,
			FixedPanel = FixedPanel.Panel1,
			SplitterDistance = 410,
			SplitterWidth = 8,
			BackColor = BgCanvas
		};

		Panel leftPanel = CreateLeftInputPanel();
		leftPanel.Dock = DockStyle.Fill;
		splitMain.Panel1.Controls.Add(leftPanel);

		// 右侧上下分割 (上表格 60%，下日志 40%)
		SplitContainer splitRight = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			FixedPanel = FixedPanel.None,
			SplitterDistance = 330,
			SplitterWidth = 8,
			BackColor = BgCanvas
		};

		Panel rightTopPanel = CreateRightTablePanel();
		rightTopPanel.Dock = DockStyle.Fill;
		splitRight.Panel1.Controls.Add(rightTopPanel);

		Panel rightBottomPanel = CreateRightLogPanel();
		rightBottomPanel.Dock = DockStyle.Fill;
		splitRight.Panel2.Controls.Add(rightBottomPanel);

		splitMain.Panel2.Controls.Add(splitRight);

		// 4. 底部微型状态栏
		Panel bottomStatusBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 26,
			BackColor = CardAltBg,
			Padding = new Padding(12, 3, 12, 3)
		};
		bottomStatusBar.Paint += (s, e) =>
		{
			using var pen = new Pen(CardBorder);
			e.Graphics.DrawLine(pen, 0, 0, bottomStatusBar.Width, 0);
		};

		_statusLabel = new Label
		{
			Text = "⚡ ULTRA STREAM ENGINE · 核心下载调度器已就绪",
			ForeColor = TextMuted,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font("Microsoft YaHei UI", 8.5f)
		};
		bottomStatusBar.Controls.Add(_statusLabel);

		// 组装主界面（先加Fill，再加Top）
		Panel centerWorkspace = new Panel { Dock = DockStyle.Fill, BackColor = BgCanvas };
		centerWorkspace.Controls.Add(splitMain);
		centerWorkspace.Controls.Add(settingsBar);
		centerWorkspace.Controls.Add(headerHud);

		rootPanel.Controls.Add(centerWorkspace);
		rootPanel.Controls.Add(bottomStatusBar);
		Controls.Add(rootPanel);

		// 确保左侧面板保持 410px 宽度
		splitMain.Panel1MinSize = 350;
		centerWorkspace.Resize += delegate
		{
			if (splitMain.Width > 600)
			{
				try
				{
					splitMain.SplitterDistance = 410;
				}
				catch { }
			}
		};

		splitRight.Resize += delegate
		{
			if (splitRight.Height > 300)
			{
				try
				{
					splitRight.SplitterDistance = Math.Max(200, (int)(splitRight.Height * 0.58));
				}
				catch { }
			}
		};

		// UI 刷新定时器
		_refreshTimer = new System.Windows.Forms.Timer { Interval = 350 };
		_refreshTimer.Tick += (s, e) =>
		{
			if (_isDownloading)
			{
				UpdateListViewRows();
			}
		};
		_refreshTimer.Start();

		ResumeLayout(false);
	}

	/// <summary>
	/// 顶部 HUD 环境与状态横幅
	/// </summary>
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

		Panel leftInfo = new Panel { Dock = DockStyle.Left, Width = 520, BackColor = Color.Transparent };

		Label lblIcon = new Label
		{
			Text = "⚡",
			Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold),
			ForeColor = CyanNeon,
			Location = new Point(0, 6),
			Size = new Size(24, 24),
			TextAlign = ContentAlignment.MiddleCenter
		};
		leftInfo.Controls.Add(lblIcon);

		_envBadgeTitle = new Label
		{
			Text = "ULTRA STREAM · 全网多平台极速提取终端",
			Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
			ForeColor = TextPrimary,
			Location = new Point(28, 4),
			AutoSize = true
		};
		leftInfo.Controls.Add(_envBadgeTitle);

		Label lblSub = new Label
		{
			Text = "FACEBOOK REEL · YOUTUBE 4K · TIKTOK · GOOGLE DRIVE 网盘双模直连",
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

		_envTagYtDlp = CreateTechPill("yt-dlp", "检测中", EmeraldNeon);
		_envTagFFmpeg = CreateTechPill("FFmpeg", "检测中", EmeraldNeon);
		_envTagFFprobe = CreateTechPill("FFprobe", "检测中", EmeraldNeon);

		flowPills.Controls.Add(_envTagYtDlp);
		flowPills.Controls.Add(_envTagFFmpeg);
		flowPills.Controls.Add(_envTagFFprobe);

		_btnInstallYtDlp = MakeTechButton("⚡ 一键配置组件", 112, 30, Color.FromArgb(37, 99, 235), async (s, e) => await InstallOrUpdateYtDlpAsync());
		_btnInstallYtDlp.Margin = new Padding(8, 0, 4, 0);
		flowPills.Controls.Add(_btnInstallYtDlp);

		_btnCheckEnv = MakeTechButton("⟳ 刷新环境", 86, 30, Color.FromArgb(28, 42, 68), (s, e) => RefreshEnvironmentStatus());
		_btnCheckEnv.Margin = new Padding(4, 0, 0, 0);
		flowPills.Controls.Add(_btnCheckEnv);

		p.Controls.Add(flowPills);
		return p;
	}

	/// <summary>
	/// 核心设置参数控制条
	/// </summary>
	private Panel CreateSettingsBar()
	{
		Panel p = new Panel
		{
			Height = 78,
			BackColor = CardAltBg,
			Margin = new Padding(0, 0, 0, 8),
			Padding = new Padding(12, 8, 12, 8)
		};
		p.Paint += (s, e) => DrawTechBorder(e.Graphics, p.ClientRectangle, CardBorder, 6);

		// 第一行：保存目录与常规操作
		Label lblDir = new Label { Text = "保存目录:", AutoSize = true, Location = new Point(12, 13), ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		p.Controls.Add(lblDir);

		string defaultDownloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "视频批量下载");
		_txtOutputDir = new TextBox
		{
			Text = defaultDownloadPath,
			Location = new Point(78, 10),
			Width = 320,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			BorderStyle = BorderStyle.FixedSingle,
			Font = new Font("Segoe UI", 9f)
		};
		p.Controls.Add(_txtOutputDir);

		_btnBrowseDir = MakeTechButton("浏览...", 58, 25, Color.FromArgb(28, 42, 68), (s, e) =>
		{
			using var fbd = new FolderBrowserDialog { SelectedPath = _txtOutputDir.Text };
			if (fbd.ShowDialog(this) == DialogResult.OK)
			{
				_txtOutputDir.Text = fbd.SelectedPath;
			}
		});
		_btnBrowseDir.Location = new Point(404, 9);
		p.Controls.Add(_btnBrowseDir);

		_btnOpenFolderQuick = MakeTechButton("打开目录", 72, 25, Color.FromArgb(28, 42, 68), (s, e) => OpenOutputDirectory());
		_btnOpenFolderQuick.Location = new Point(468, 9);
		p.Controls.Add(_btnOpenFolderQuick);

		// 右侧流式对齐容器：画质规格、并发数、网络代理 (使用 FlowLayoutPanel 保证永远不拥挤重叠)
		FlowLayoutPanel flowRightSettings = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			AutoSize = true,
			Location = new Point(552, 7),
			Height = 32,
			BackColor = Color.Transparent
		};

		Label lblQuality = new Label { Text = "画质规格:", AutoSize = true, Margin = new Padding(0, 5, 4, 0), ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		flowRightSettings.Controls.Add(lblQuality);

		_cboQuality = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Width = 145,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			FlatStyle = FlatStyle.Flat,
			Margin = new Padding(0, 2, 12, 0)
		};
		_cboQuality.Items.AddRange(new object[] { "最高画质 (4K/1080P)", "1080P 高清优先", "720P 极速标清", "仅提取音频 (MP3)" });
		_cboQuality.SelectedIndex = 0;
		flowRightSettings.Controls.Add(_cboQuality);

		Label lblThreads = new Label { Text = "并发线程:", AutoSize = true, Margin = new Padding(0, 5, 4, 0), ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		flowRightSettings.Controls.Add(lblThreads);

		_cboThreads = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Width = 80,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			FlatStyle = FlatStyle.Flat,
			Margin = new Padding(0, 2, 12, 0)
		};
		_cboThreads.Items.AddRange(new object[] { "1 线程", "2 线程", "4 线程", "8 线程" });
		_cboThreads.SelectedIndex = 2;
		flowRightSettings.Controls.Add(_cboThreads);

		Label lblProxy = new Label { Text = "网络代理:", AutoSize = true, Margin = new Padding(0, 5, 4, 0), ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold) };
		flowRightSettings.Controls.Add(lblProxy);

		_cboProxy = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Width = 115,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			FlatStyle = FlatStyle.Flat,
			Margin = new Padding(0, 2, 4, 0)
		};
		_cboProxy.Items.AddRange(new object[] { "自动 (系统代理)", "直连 (无代理)", "自定义代理" });
		_cboProxy.SelectedIndex = 0;
		flowRightSettings.Controls.Add(_cboProxy);

		_txtCustomProxy = new TextBox
		{
			Text = "http://127.0.0.1:7890",
			Width = 135,
			BackColor = InputBg,
			ForeColor = TextPrimary,
			BorderStyle = BorderStyle.FixedSingle,
			Visible = false,
			Margin = new Padding(0, 2, 0, 0)
		};
		_cboProxy.SelectedIndexChanged += (s, e) =>
		{
			_txtCustomProxy.Visible = (_cboProxy.SelectedIndex == 2);
		};
		flowRightSettings.Controls.Add(_txtCustomProxy);

		p.Controls.Add(flowRightSettings);

		// 自适应布局：当窗口宽度发生变化时，动态拉伸保存目录输入框
		p.Resize += (s, e) =>
		{
			int availableForPath = flowRightSettings.Left - 165;
			if (availableForPath > 180)
			{
				_txtOutputDir.Width = availableForPath;
				_btnBrowseDir.Left = _txtOutputDir.Right + 6;
				_btnOpenFolderQuick.Left = _btnBrowseDir.Right + 6;
			}
		};

		// 第二行：复选芯片（整齐间隔）
		FlowLayoutPanel flowCheckboxes = new FlowLayoutPanel
		{
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			AutoSize = true,
			Location = new Point(74, 45),
			Height = 28,
			BackColor = Color.Transparent
		};

		_chkResume = CreateCyberCheckBox("⚡ 断点续传加速", true, 0, 0);
		_chkResume.Margin = new Padding(0, 0, 16, 0);
		flowCheckboxes.Controls.Add(_chkResume);

		_chkSkipExisting = CreateCyberCheckBox("⏭ 已下载自动跳过", true, 0, 0);
		_chkSkipExisting.Margin = new Padding(0, 0, 16, 0);
		flowCheckboxes.Controls.Add(_chkSkipExisting);

		_chkSubfolderByPlatform = CreateCyberCheckBox("📁 平台分类独立目录", true, 0, 0);
		_chkSubfolderByPlatform.Margin = new Padding(0, 0, 16, 0);
		flowCheckboxes.Controls.Add(_chkSubfolderByPlatform);

		_chkPlaylistFolder = CreateCyberCheckBox("📑 剧集/合集分文件夹", true, 0, 0);
		_chkPlaylistFolder.Margin = new Padding(0, 0, 16, 0);
		flowCheckboxes.Controls.Add(_chkPlaylistFolder);

		p.Controls.Add(flowCheckboxes);

		return p;
	}

	/// <summary>
	/// 左侧输入与控制核心面板
	/// </summary>
	private Panel CreateLeftInputPanel()
	{
		Panel p = new Panel
		{
			BackColor = CardBg,
			Padding = new Padding(12)
		};
		p.Paint += (s, e) => DrawTechBorder(e.Graphics, p.ClientRectangle, CardBorder, 6);

		Panel titleRow = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent };

		Label lblTitle = new Label
		{
			Text = "📥 目标视频 / 播放列表链接",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = CyanNeon,
			Location = new Point(0, 0),
			AutoSize = true
		};
		titleRow.Controls.Add(lblTitle);

		_urlCountBadge = new Label
		{
			Text = "5 个示例链接",
			Font = new Font("Segoe UI", 8f, FontStyle.Bold),
			ForeColor = EmeraldNeon,
			BackColor = Color.FromArgb(20, 45, 40),
			Padding = new Padding(6, 2, 6, 2),
			Location = new Point(200, -2),
			AutoSize = true
		};
		titleRow.Controls.Add(_urlCountBadge);

		FlowLayoutPanel platformBadges = new FlowLayoutPanel
		{
			Location = new Point(0, 24),
			Size = new Size(385, 22),
			WrapContents = false,
			BackColor = Color.Transparent
		};
		platformBadges.Controls.Add(CreateMiniPlatformTag("YouTube", Color.FromArgb(220, 38, 38)));
		platformBadges.Controls.Add(CreateMiniPlatformTag("Facebook Reel", Color.FromArgb(37, 99, 235)));
		platformBadges.Controls.Add(CreateMiniPlatformTag("TikTok", Color.FromArgb(219, 39, 119)));
		platformBadges.Controls.Add(CreateMiniPlatformTag("Google Drive", Color.FromArgb(202, 138, 4)));
		platformBadges.Controls.Add(CreateMiniPlatformTag("全网解析", Color.FromArgb(71, 85, 105)));
		titleRow.Controls.Add(platformBadges);

		_txtUrls = new SmartUrlTextBox(HandleSmartPasteIntoTextBox)
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
		_txtUrls.Text = "https://www.facebook.com/reel/1116404280831830\r\nhttps://www.youtube.com/watch?v=XqJMIE6_9gY\r\nhttps://www.youtube.com/shorts/bqLBK7GuPCk\r\nhttps://www.tiktok.com/@lucia.gove/video/7617254633341308178\r\nhttps://drive.google.com/file/d/1msP8BcsLRbFe5G8b67roWlkJTYN5fIoL/view";
		_hasDefaultSampleUrls = true;
		_txtUrls.TextChanged += (s, e) =>
		{
			int count = GetUrlsFromInput().Length;
			_urlCountBadge.Text = $"{count} 个待下载链接";
			_urlCountBadge.ForeColor = count > 0 ? EmeraldNeon : TextMuted;
		};

		Panel urlContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 10), BackColor = Color.Transparent };
		urlContainer.Controls.Add(_txtUrls);
		p.Controls.Add(urlContainer);

		Panel btnBottomArea = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 90,
			BackColor = Color.Transparent
		};

		// 行 1: 辅助操作 (剪贴板、清空、元数据解析)
		_btnReadClipboard = MakeTechButton("📋 粘贴剪贴板", 125, 30, Color.FromArgb(28, 42, 68), (s, e) => ReadClipboardUrls());
		_btnReadClipboard.Location = new Point(0, 4);
		btnBottomArea.Controls.Add(_btnReadClipboard);

		_btnClearUrls = MakeTechButton("🗑 清空", 70, 30, Color.FromArgb(28, 42, 68), (s, e) =>
		{
			_hasDefaultSampleUrls = false;
			_txtUrls.Clear();
		});
		_btnClearUrls.Location = new Point(132, 4);
		btnBottomArea.Controls.Add(_btnClearUrls);

		_btnParsePreview = MakeTechButton("🔍 解析元数据", 110, 30, Color.FromArgb(28, 42, 68), async (s, e) => await ParseUrlsAsync());
		_btnParsePreview.Location = new Point(208, 4);
		btnBottomArea.Controls.Add(_btnParsePreview);

		// 行 2: 核心下载引擎启动/停止 (更清爽开阔)
		_btnStartDownload = MakeTechButton("▶ 启动批量极速下载", 205, 36, Color.FromArgb(37, 99, 235), async (s, e) => await StartDownloadAsync());
		_btnStartDownload.Location = new Point(0, 44);
		_btnStartDownload.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
		btnBottomArea.Controls.Add(_btnStartDownload);

		_btnStopDownload = MakeTechButton("⏹ 终止任务", 100, 36, Color.FromArgb(185, 28, 28), (s, e) => StopDownload());
		_btnStopDownload.Location = new Point(213, 44);
		btnBottomArea.Controls.Add(_btnStopDownload);

		p.Controls.Add(urlContainer);
		p.Controls.Add(btnBottomArea);
		p.Controls.Add(titleRow);
		return p;
	}

	/// <summary>
	/// 右上方任务矩阵列表区
	/// </summary>
	private Panel CreateRightTablePanel()
	{
		Panel p = new Panel
		{
			BackColor = CardBg,
			Padding = new Padding(12)
		};
		p.Paint += (s, e) => DrawTechBorder(e.Graphics, p.ClientRectangle, CardBorder, 6);

		Panel headerRow = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Color.Transparent };
		Label lblHeader = new Label
		{
			Text = "📊 下载任务矩阵 (右键条目可直通【视频拼屏】/【剪辑工作台】)",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = CyanNeon,
			Dock = DockStyle.Left,
			AutoSize = true
		};
		headerRow.Controls.Add(lblHeader);

		_progressTextLabel = new Label
		{
			Text = "准备就绪",
			Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
			ForeColor = TextMuted,
			Dock = DockStyle.Right,
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleRight
		};
		headerRow.Controls.Add(_progressTextLabel);
		p.Controls.Add(headerRow);

		_listView = new ListView
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
		SetDoubleBuffered(_listView);

		_listView.Columns.Add("#", 38);
		_listView.Columns.Add("平台", 92);
		_listView.Columns.Add("来源", 80);
		_listView.Columns.Add("标题", 280);
		_listView.Columns.Add("时长/大小", 95);
		_listView.Columns.Add("进度", 75);
		_listView.Columns.Add("速度", 85);
		_listView.Columns.Add("状态", 115);

		_listView.ContextMenuStrip = CreateTableContextMenu();
		p.Controls.Add(_listView);

		_progressBar = new ProgressBar
		{
			Dock = DockStyle.Bottom,
			Height = 6,
			Minimum = 0,
			Maximum = 100,
			Value = 0
		};
		p.Controls.Add(_progressBar);

		return p;
	}

	/// <summary>
	/// 右下方终端控制台
	/// </summary>
	private Panel CreateRightLogPanel()
	{
		Panel p = new Panel
		{
			BackColor = CardBg,
			Padding = new Padding(12)
		};
		p.Paint += (s, e) => DrawTechBorder(e.Graphics, p.ClientRectangle, CardBorder, 6);

		Panel titleRow = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent };
		Label lblLog = new Label
		{
			Text = "💻 极速内核终端控制台 (Terminal Output)",
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
			ForeColor = TextMuted,
			Dock = DockStyle.Left,
			AutoSize = true
		};
		titleRow.Controls.Add(lblLog);

		Button btnClearLog = MakeTechButton("清空终端", 72, 24, Color.FromArgb(28, 42, 68), (s, e) => _logBox.Clear());
		btnClearLog.Dock = DockStyle.Right;
		titleRow.Controls.Add(btnClearLog);
		p.Controls.Add(titleRow);

		_logBox = new RichTextBox
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(6, 9, 16),
			ForeColor = EmeraldNeon,
			BorderStyle = BorderStyle.None,
			Font = new Font("Consolas", 9f),
			ReadOnly = true
		};
		p.Controls.Add(_logBox);

		return p;
	}

	// ====================== 控件外观与样式辅助函数 ======================

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
		Label lbl = new Label
		{
			Text = $"● {module}: {status}",
			Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold),
			ForeColor = dotColor,
			BackColor = Color.FromArgb(20, 32, 54),
			Padding = new Padding(6, 4, 6, 4),
			Margin = new Padding(3, 0, 3, 0),
			AutoSize = true
		};
		return lbl;
	}

	private Label CreateMiniPlatformTag(string name, Color bg)
	{
		Label lbl = new Label
		{
			Text = name,
			Font = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Bold),
			ForeColor = Color.White,
			BackColor = bg,
			Padding = new Padding(4, 1, 4, 1),
			Margin = new Padding(0, 0, 4, 0),
			AutoSize = true
		};
		return lbl;
	}

	private CheckBox CreateCyberCheckBox(string text, bool check, int x, int y)
	{
		return new CheckBox
		{
			Text = text,
			Checked = check,
			Location = new Point(x, y),
			AutoSize = true,
			ForeColor = Color.FromArgb(203, 213, 225),
			Font = new Font("Microsoft YaHei UI", 9f),
			Cursor = Cursors.Hand
		};
	}

	/// <summary>
	/// 科技感玻璃质感自绘按钮
	/// </summary>
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

	private sealed class CyberColorTable : ProfessionalColorTable
	{
		public override Color ToolStripDropDownBackground => Color.FromArgb(14, 21, 37); // #0e1525
		public override Color ImageMarginGradientBegin => Color.FromArgb(14, 21, 37);
		public override Color ImageMarginGradientMiddle => Color.FromArgb(14, 21, 37);
		public override Color ImageMarginGradientEnd => Color.FromArgb(14, 21, 37);
		public override Color MenuBorder => Color.FromArgb(32, 50, 82); // #203252
		public override Color MenuItemBorder => Color.FromArgb(56, 189, 248); // #38bdf8 霓虹青高亮边框
		public override Color MenuItemSelected => Color.FromArgb(30, 58, 95); // #1e3a5f 科技感选中背景
		public override Color MenuItemSelectedGradientBegin => Color.FromArgb(30, 58, 95);
		public override Color MenuItemSelectedGradientEnd => Color.FromArgb(30, 58, 95);
		public override Color MenuStripGradientBegin => Color.FromArgb(14, 21, 37);
		public override Color MenuStripGradientEnd => Color.FromArgb(14, 21, 37);
		public override Color SeparatorDark => Color.FromArgb(32, 50, 82);
		public override Color SeparatorLight => Color.FromArgb(15, 23, 42);
	}

	private sealed class CyberDarkMenuRenderer : ToolStripProfessionalRenderer
	{
		public CyberDarkMenuRenderer() : base(new CyberColorTable()) { }

		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
		{
			// 确保无论普通还是鼠标悬停(Hover)状态，文字永远保持高对比度亮白或高亮青色，绝不被系统反色冲淡
			if (e.Item.Selected)
			{
				e.TextColor = Color.FromArgb(241, 245, 249); // 高对比度极光亮白
			}
			else
			{
				e.TextColor = Color.FromArgb(226, 232, 240);
			}
			base.OnRenderItemText(e);
		}

		protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
		{
			e.ArrowColor = e.Item.Selected ? Color.FromArgb(56, 189, 248) : Color.FromArgb(148, 163, 184);
			base.OnRenderArrow(e);
		}
	}

	private ContextMenuStrip CreateTableContextMenu()
	{
		ContextMenuStrip cms = new ContextMenuStrip();
		var renderer = new CyberDarkMenuRenderer();
		cms.Renderer = renderer;
		cms.BackColor = Color.FromArgb(14, 21, 37);
		cms.ForeColor = Color.FromArgb(241, 245, 249);
		cms.Font = new Font("Microsoft YaHei UI", 9f);

		var itemSplitScreen = new ToolStripMenuItem("▦ 发送此视频至【视频拼屏】");
		itemSplitScreen.DropDown.Renderer = renderer;
		itemSplitScreen.DropDown.BackColor = Color.FromArgb(14, 21, 37);
		itemSplitScreen.DropDown.ForeColor = Color.FromArgb(241, 245, 249);
		itemSplitScreen.DropDownItems.Add("自动填充到当前选中区域", null, (s, e) => SendSelectedToSplitScreen(-1));
		itemSplitScreen.DropDownItems.Add("指定分配至【区域 1】", null, (s, e) => SendSelectedToSplitScreen(0));
		itemSplitScreen.DropDownItems.Add("指定分配至【区域 2】", null, (s, e) => SendSelectedToSplitScreen(1));
		itemSplitScreen.DropDownItems.Add("指定分配至【区域 3】", null, (s, e) => SendSelectedToSplitScreen(2));
		itemSplitScreen.DropDownItems.Add("指定分配至【区域 4】", null, (s, e) => SendSelectedToSplitScreen(3));
		cms.Items.Add(itemSplitScreen);

		cms.Items.Add("🎬 发送此视频至【视频剪辑】工作台", null, (s, e) => SendSelectedToCutEdit());
		cms.Items.Add("📦 发送此视频至【批量合并】素材库", null, (s, e) => SendSelectedToMerge());
		cms.Items.Add("🎙️ 发送此视频至【智能字幕】转录", null, (s, e) => SendSelectedToTranscribe());
		cms.Items.Add(new ToolStripSeparator());
		cms.Items.Add("📂 在文件资源管理器中定位", null, (s, e) => LocateSelectedFile());
		cms.Items.Add("▶ 打开播放此视频", null, (s, e) => PlaySelectedFile());

		return cms;
	}

	// ====================== 核心下载调度与流转业务 ======================

	public void RefreshEnvironmentStatus()
	{
		string ytdlp = DownloadEngine.FindYtDlpPath();
		string ffmpeg = DownloadEngine.FindFFmpegPath();
		string ffprobe = DownloadEngine.FindFFprobePath();

		bool ytdlpOk = !string.IsNullOrEmpty(ytdlp) && File.Exists(ytdlp);
		bool ffmpegOk = !string.IsNullOrEmpty(ffmpeg) && File.Exists(ffmpeg);
		bool ffprobeOk = !string.IsNullOrEmpty(ffprobe) && File.Exists(ffprobe);

		_envTagYtDlp.Text = ytdlpOk ? "● yt-dlp: 就绪" : "● yt-dlp: 未检测到";
		_envTagYtDlp.ForeColor = ytdlpOk ? EmeraldNeon : Color.FromArgb(248, 113, 113);

		_envTagFFmpeg.Text = ffmpegOk ? "● FFmpeg: 就绪" : "● FFmpeg: 缺失";
		_envTagFFmpeg.ForeColor = ffmpegOk ? EmeraldNeon : Color.FromArgb(248, 113, 113);

		_envTagFFprobe.Text = ffprobeOk ? "● FFprobe: 就绪" : "● FFprobe: 缺失";
		_envTagFFprobe.ForeColor = ffprobeOk ? EmeraldNeon : Color.FromArgb(248, 113, 113);

		if (ytdlpOk && ffmpegOk)
		{
			_envBadgeTitle.Text = "ULTRA STREAM · 全网多平台极速提取终端 [高清就绪]";
			_btnInstallYtDlp.Text = "✓ 组件已完备";
		}
		else
		{
			_envBadgeTitle.Text = "ULTRA STREAM · 全网多平台极速提取终端 [环境待配置]";
			_btnInstallYtDlp.Text = "⚡ 一键配置组件";
		}
	}

	private async Task InstallOrUpdateYtDlpAsync()
	{
		LogMessage("[组件配置] 正在检测本地或官方源中的 yt-dlp.exe...");
		string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
		string ytdlpExisting = DownloadEngine.FindYtDlpPath();

		if (!string.IsNullOrEmpty(ytdlpExisting) && File.Exists(ytdlpExisting))
		{
			LogMessage($"[组件配置] 本地已找到可用组件 ({ytdlpExisting})！正在刷新状态...");
			RefreshEnvironmentStatus();
			MessageBox.Show(this, "yt-dlp 核心组件已就绪，可直接解析并下载各主流平台视频！", "环境正常", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		try
		{
			LogMessage("[组件配置] 正在通过高速镜像源下载最新版 yt-dlp.exe (约 15MB)...");
			bool ok = await Transcriber.TranscribeEngine.DownloadWithMirrorAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", targetPath, LogMessage, CancellationToken.None);

			if (!ok || !File.Exists(targetPath))
			{
				LogMessage("[组件配置] 镜像直连失败，尝试启动系统 winget 安装通道...");
				var psi = new ProcessStartInfo
				{
					FileName = "winget",
					Arguments = "install yt-dlp.yt-dlp --accept-source-agreements --accept-package-agreements",
					UseShellExecute = false,
					CreateNoWindow = true
				};
				using var proc = Process.Start(psi);
				await Task.Run(() => proc?.WaitForExit());
			}

			RefreshEnvironmentStatus();
			string finalPath = DownloadEngine.FindYtDlpPath();
			if (!string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
			{
				MessageBox.Show(this, "yt-dlp 视频解析组件已成功安装并就绪！", "配置完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			else
			{
				MessageBox.Show(this, "yt-dlp 组件安装未能完成，请检查网络连接（或代理设置）后重试。", "配置未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}
		catch (Exception ex)
		{
			LogMessage($"[组件配置异常] {ex.Message}");
			MessageBox.Show(this, $"组件配置出现异常: {ex.Message}\n请检查网络连接后重试！", "配置失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}

	private void ReadClipboardUrls()
	{
		if (ExtractClipboardUrls(out List<string> extractedUrls, out string fallbackText, out string diagMsg))
		{
			string toInsert = extractedUrls.Count > 0
				? string.Join("\r\n", extractedUrls)
				: fallbackText.Trim();

			if (!string.IsNullOrWhiteSpace(toInsert))
			{
				if (_hasDefaultSampleUrls || string.IsNullOrWhiteSpace(_txtUrls.Text))
				{
					_hasDefaultSampleUrls = false;
					_txtUrls.Text = toInsert;
				}
				else
				{
					_txtUrls.AppendText("\r\n" + toInsert);
				}

				if (extractedUrls.Count > 0)
					LogMessage(diagMsg);
				else
					LogMessage("[剪贴板] 成功从剪贴板读取并添加内容。");
			}
		}
	}

	private bool HandleSmartPasteIntoTextBox()
	{
		if (!ExtractClipboardUrls(out List<string> extractedUrls, out string _, out string diagMsg))
			return false;

		if (extractedUrls.Count == 0)
			return false;

		string joined = string.Join("\r\n", extractedUrls);
		if (_hasDefaultSampleUrls)
		{
			_hasDefaultSampleUrls = false;
			_txtUrls.Text = joined;
			_txtUrls.SelectionStart = _txtUrls.TextLength;
			_txtUrls.SelectionLength = 0;
		}
		else if (_txtUrls.SelectionLength > 0)
		{
			_txtUrls.SelectedText = joined;
		}
		else if (string.IsNullOrWhiteSpace(_txtUrls.Text))
		{
			_txtUrls.Text = joined;
			_txtUrls.SelectionStart = _txtUrls.TextLength;
		}
		else
		{
			int sel = _txtUrls.SelectionStart;
			bool needLeadingNewline = sel > 0 && _txtUrls.Text[sel - 1] != '\n' && _txtUrls.Text[sel - 1] != '\r';
			_txtUrls.SelectedText = (needLeadingNewline ? "\r\n" : "") + joined;
		}

		LogMessage(diagMsg);
		return true;
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool OpenClipboard(IntPtr hWndNewOwner);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool CloseClipboard();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr GetClipboardData(uint uFormat);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
	private static extern uint RegisterClipboardFormat(string lpszFormat);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr GlobalLock(IntPtr hMem);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GlobalUnlock(IntPtr hMem);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern UIntPtr GlobalSize(IntPtr hMem);

	private static bool ExtractClipboardUrls(out List<string> extractedUrls, out string rawFallbackText, out string diagnosticSummary)
	{
		extractedUrls = new List<string>();
		rawFallbackText = string.Empty;
		diagnosticSummary = string.Empty;

		try
		{
			IDataObject dataObj = Clipboard.GetDataObject();
			if (dataObj == null)
				return false;

			var orderedUrls = new List<string>();

			// 辅助方法：从一段已解码文本中提取所有候选视频链接（在单个单元格内部去重，但跨单元格保留顺序与数量）
			List<string> ExtractUrlsFromCellFragment(string fragment)
			{
				var cellUrls = new List<string>();
				var cellSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				if (string.IsNullOrWhiteSpace(fragment))
					return cellUrls;

				void TryAddCellUrl(string rawCandidate)
				{
					string cleaned = NormalizeAndUnwrapClipboardUrl(rawCandidate);
					if (!string.IsNullOrWhiteSpace(cleaned) && !IsIgnoredClipboardMetaUrl(cleaned))
					{
						if (cellSet.Add(cleaned))
							cellUrls.Add(cleaned);
					}
				}

				// 先对 HTML 实体 (&quot; &amp; &#39;) 与常见 JSON 转义 (\/ \u0026 \u003d) 解码，
				// 确保 Google Sheets 的 data-sheets-hyperlinkruns="{&quot;1&quot;:0,&quot;2&quot;:&quot;https://...&quot;}" 能够完整还原
				string decoded = DecodeHtmlAndJsonEscapes(fragment);

				// 1) 提取常见超链接属性
				foreach (Match m in Regex.Matches(decoded, @"(?:href|data-href|data-sheets-hyperlink|data-url|data-chip-url)\s*=\s*[""'](?<u1>[^""']+)[""']|(?:href|data-href|data-sheets-hyperlink|data-url|data-chip-url)\s*=\s*(?<u2>[^\s>""']+)", RegexOptions.IgnoreCase))
				{
					string val = m.Groups["u1"].Success ? m.Groups["u1"].Value : m.Groups["u2"].Value;
					TryAddCellUrl(val);
				}

				// 2) 提取 HYPERLINK("https://...", "...") 公式
				foreach (Match m in Regex.Matches(decoded, @"HYPERLINK\s*\(\s*[""'](?<u>https?://[^""']+)[""']", RegexOptions.IgnoreCase))
				{
					TryAddCellUrl(m.Groups["u"].Value);
				}

				// 3) 兜底扫描该单元格内所有 https?:// 链接（覆盖 Google Sheets 巧克力胶囊 data-sheets-hyperlinkruns / JSON 载荷 / 混排文本）
				foreach (Match m in Regex.Matches(decoded, @"https?://[^\x00-\x20""'<>\\})\]]+", RegexOptions.IgnoreCase))
				{
					TryAddCellUrl(m.Value);
				}

				return cellUrls;
			}

			// 1. 优先直接从 Win32 剪贴板读取原始 UTF-8 HTML 字节流，彻底规避 .NET Framework 4.8 DataObject.GetData(DataFormats.Html) 中文字符截断 Bug
			string htmlContent = TryReadRawClipboardFormatUtf8("HTML Format")
				?? TryReadClipboardFormatAsUtf8String(dataObj, DataFormats.Html)
				?? TryReadClipboardFormatAsUtf8String(dataObj, "HTML Format")
				?? TryReadClipboardFormatAsUtf8String(dataObj, "text/html");

			int tdCellCount = 0;
			if (!string.IsNullOrWhiteSpace(htmlContent))
			{
				// 剥离 CF_HTML 头部元信息（避免误把 SourceURL:https://docs.google.com/spreadsheets/... 当成下载链接）
				string htmlBody = StripCfHtmlHeader(htmlContent);

				// 按表格单元格 <td>...</td> 逐格提取：
				// 同一单元格内 data-sheets-hyperlink 与 <a href> 重复的链接会自动合并为 1 条；
				// 不同单元格之间即使链接相同也按用户复制的单元格数量保留，不会吞掉行数！
				MatchCollection tdMatches = Regex.Matches(htmlBody, @"<td\b[^>]*>(?:(?!</?td\b)[\s\S])*?</td>", RegexOptions.IgnoreCase);
				if (tdMatches.Count > 0)
				{
					tdCellCount = tdMatches.Count;
					foreach (Match td in tdMatches)
					{
						List<string> cellUrls = ExtractUrlsFromCellFragment(td.Value);
						orderedUrls.AddRange(cellUrls);
					}
				}
				else
				{
					orderedUrls.AddRange(ExtractUrlsFromCellFragment(htmlBody));
				}
			}

			// 2. 如果存在部分 Google Sheets 巧克力胶囊（如云盘文件芯片/特殊芯片）未在 HTML <td> 中写出 URL，
			//    则进一步读取 Chromium 专属剪贴板格式 (Chromium Web Custom MIME Data Format，内部为 UTF-16LE JSON) 及其他自定义格式
			if (orderedUrls.Count == 0 || (tdCellCount > 0 && orderedUrls.Count < tdCellCount))
			{
				var existingSet = new HashSet<string>(orderedUrls, StringComparer.OrdinalIgnoreCase);
				void AddSupplementalUrl(string rawCandidate)
				{
					string cleaned = NormalizeAndUnwrapClipboardUrl(rawCandidate);
					if (!string.IsNullOrWhiteSpace(cleaned) && !IsIgnoredClipboardMetaUrl(cleaned))
					{
						if (existingSet.Add(cleaned))
							orderedUrls.Add(cleaned);
					}
				}

				// 检查 Chromium Web Custom MIME Data Format 及所有包含 google/sheets/custom/html/rtf 的格式
				var candidateFormats = new List<string>(dataObj.GetFormats());
				if (!candidateFormats.Contains("Chromium Web Custom MIME Data Format"))
					candidateFormats.Add("Chromium Web Custom MIME Data Format");

				foreach (string fmt in candidateFormats)
				{
					if (string.Equals(fmt, DataFormats.Html, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(fmt, "HTML Format", StringComparison.OrdinalIgnoreCase) ||
						string.Equals(fmt, DataFormats.Text, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(fmt, DataFormats.UnicodeText, StringComparison.OrdinalIgnoreCase) ||
						string.Equals(fmt, DataFormats.OemText, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					foreach (string customText in TryReadClipboardFormatBothEncodings(dataObj, fmt))
					{
						if (string.IsNullOrWhiteSpace(customText))
							continue;

						string decodedCustom = DecodeHtmlAndJsonEscapes(customText);
						foreach (Match m in Regex.Matches(decodedCustom, @"https?://[^\x00-\x20""'<>\\})\]]+", RegexOptions.IgnoreCase))
						{
							AddSupplementalUrl(m.Value);
						}
					}
				}
			}

			// 3. 解析纯文本（处理纯文本多行网址、多列带标题网址，或补充漏网的裸链接）
			if (Clipboard.ContainsText())
			{
				rawFallbackText = Clipboard.GetText() ?? string.Empty;
				if (orderedUrls.Count == 0)
				{
					foreach (string line in rawFallbackText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
					{
						List<string> lineUrls = ExtractUrlsFromCellFragment(line);
						orderedUrls.AddRange(lineUrls);
					}
				}
			}

			extractedUrls = orderedUrls;
			int distinctCount = new HashSet<string>(orderedUrls, StringComparer.OrdinalIgnoreCase).Count;
			if (tdCellCount > 0)
			{
				if (distinctCount < orderedUrls.Count)
					diagnosticSummary = $"[智能粘贴] 检测到表格 {tdCellCount} 个单元格，已完整提取 {orderedUrls.Count} 条视频链接（含 {orderedUrls.Count - distinctCount} 条相同链接，已按原表格行数完整保留）。";
				else
					diagnosticSummary = $"[智能粘贴] 检测到表格 {tdCellCount} 个单元格，已完整提取 {orderedUrls.Count} 条真实视频链接。";
			}
			else
			{
				diagnosticSummary = $"[智能粘贴] 自动从剪贴板中提取了 {orderedUrls.Count} 条真实视频链接。";
			}
			return extractedUrls.Count > 0 || !string.IsNullOrWhiteSpace(rawFallbackText);
		}
		catch
		{
			return false;
		}
	}

	private static string DecodeHtmlAndJsonEscapes(string input)
	{
		if (string.IsNullOrEmpty(input))
			return string.Empty;

		string s = WebUtility.HtmlDecode(input);
		// 二次 HtmlDecode 应对双重转义（如 data-sheets-formula 或嵌套 JSON 属性中的 &amp;quot;）
		if (s.IndexOf('&') >= 0)
			s = WebUtility.HtmlDecode(s);

		s = s.Replace(@"\/", "/")
			 .Replace(@"\u0026", "&")
			 .Replace(@"\u0026", "&")
			 .Replace(@"\u003d", "=")
			 .Replace(@"\u003D", "=")
			 .Replace(@"\u003f", "?")
			 .Replace(@"\u003F", "?")
			 .Replace(@"\u0022", "\"");
		return s;
	}

	private static string TryReadRawClipboardFormatUtf8(string formatName)
	{
		byte[] bytes = TryReadRawClipboardBytes(formatName);
		if (bytes == null || bytes.Length == 0)
			return null;
		return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
	}

	private static byte[] TryReadRawClipboardBytes(string formatName)
	{
		uint fmtId = RegisterClipboardFormat(formatName);
		if (fmtId == 0)
			return null;

		if (!OpenClipboard(IntPtr.Zero))
			return null;

		try
		{
			IntPtr hGlobal = GetClipboardData(fmtId);
			if (hGlobal == IntPtr.Zero)
				return null;

			uint size = (uint)GlobalSize(hGlobal);
			if (size == 0)
				return null;

			IntPtr ptr = GlobalLock(hGlobal);
			if (ptr == IntPtr.Zero)
				return null;

			try
			{
				byte[] buffer = new byte[size];
				Marshal.Copy(ptr, buffer, 0, (int)size);
				return buffer;
			}
			finally
			{
				GlobalUnlock(hGlobal);
			}
		}
		catch
		{
			return null;
		}
		finally
		{
			CloseClipboard();
		}
	}

	private static List<string> TryReadClipboardFormatBothEncodings(IDataObject dataObj, string format)
	{
		var results = new List<string>();
		try
		{
			byte[] bytes = TryReadRawClipboardBytes(format);
			if (bytes == null && dataObj != null && dataObj.GetDataPresent(format))
			{
				object raw = dataObj.GetData(format);
				if (raw is MemoryStream ms)
				{
					bytes = ms.ToArray();
				}
				else if (raw is Stream stream && stream.CanRead)
				{
					using var copy = new MemoryStream();
					stream.CopyTo(copy);
					bytes = copy.ToArray();
				}
				else if (raw is byte[] arr)
				{
					bytes = arr;
				}
				else if (raw is string str)
				{
					results.Add(str);
					return results;
				}
			}

			if (bytes != null && bytes.Length > 0)
			{
				results.Add(Encoding.UTF8.GetString(bytes));
				// Chromium Web Custom MIME Data Format 使用 Pickle UTF-16LE 存储 Google Sheets 内部剪贴板 JSON
				results.Add(Encoding.Unicode.GetString(bytes));
			}
		}
		catch
		{
		}
		return results;
	}

	private static string TryReadClipboardFormatAsUtf8String(IDataObject dataObj, string format)
	{
		try
		{
			if (!dataObj.GetDataPresent(format))
				return null;

			object raw = dataObj.GetData(format);
			if (raw is MemoryStream ms)
			{
				byte[] bytes = ms.ToArray();
				return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
			}
			if (raw is Stream stream && stream.CanRead)
			{
				using var copy = new MemoryStream();
				stream.CopyTo(copy);
				return Encoding.UTF8.GetString(copy.ToArray()).TrimEnd('\0');
			}
			if (raw is byte[] byteArr)
			{
				return Encoding.UTF8.GetString(byteArr).TrimEnd('\0');
			}
			if (raw is string str)
			{
				return str;
			}
		}
		catch
		{
		}
		return null;
	}

	private static string StripCfHtmlHeader(string cfHtml)
	{
		if (string.IsNullOrEmpty(cfHtml))
			return string.Empty;

		int idxHtml = cfHtml.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
		if (idxHtml >= 0)
			return cfHtml.Substring(idxHtml);

		int idxFragment = cfHtml.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
		if (idxFragment >= 0)
			return cfHtml.Substring(idxFragment);

		int idxFirstTag = cfHtml.IndexOf('<');
		if (idxFirstTag >= 0)
			return cfHtml.Substring(idxFirstTag);

		return cfHtml;
	}

	private static string NormalizeAndUnwrapClipboardUrl(string rawUrl)
	{
		if (string.IsNullOrWhiteSpace(rawUrl))
			return null;

		string url = DecodeHtmlAndJsonEscapes(rawUrl.Trim());
		url = url.Trim('"', '\'', ' ', '\t', '\r', '\n', ')', ']', '}', '>', ';', ',');

		if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
		{
			url = "https://" + url;
		}

		if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
			!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		// 解包 Google Sheets / Google 跳转链接: https://www.google.com/url?q=https%3A%2F%2Fwww.youtube.com%2F...&sa=D...
		// 以及 Facebook 跳转链接: https://l.facebook.com/l.php?u=https%3A%2F%2F...
		try
		{
			if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
			{
				string host = uri.Host ?? string.Empty;
				if ((host.EndsWith("google.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Equals("/url", StringComparison.OrdinalIgnoreCase)) ||
					(host.EndsWith("facebook.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Equals("/l.php", StringComparison.OrdinalIgnoreCase)))
				{
					string query = uri.Query.TrimStart('?');
					foreach (string part in query.Split('&'))
					{
						int eq = part.IndexOf('=');
						if (eq <= 0) continue;
						string key = part.Substring(0, eq);
						string val = part.Substring(eq + 1);
						if ((key.Equals("q", StringComparison.OrdinalIgnoreCase) ||
							 key.Equals("url", StringComparison.OrdinalIgnoreCase) ||
							 key.Equals("u", StringComparison.OrdinalIgnoreCase)) &&
							!string.IsNullOrWhiteSpace(val))
						{
							string decoded = Uri.UnescapeDataString(val);
							if (decoded.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
								decoded.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
							{
								return decoded.Trim();
							}
						}
					}
				}
			}
		}
		catch
		{
		}

		return url;
	}

	private static bool IsIgnoredClipboardMetaUrl(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return true;

		if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
			return true;

		string host = uri.Host ?? string.Empty;
		// 过滤 XML/Office/W3C 命名空间与 Google 静态图标资源链接
		if (host.EndsWith("schemas.openxmlformats.org", StringComparison.OrdinalIgnoreCase) ||
			host.EndsWith("schemas.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
			host.EndsWith("w3.org", StringComparison.OrdinalIgnoreCase) ||
			host.EndsWith("gstatic.com", StringComparison.OrdinalIgnoreCase) ||
			host.EndsWith("googleapis.com", StringComparison.OrdinalIgnoreCase) ||
			host.Equals("drive-thirdparty.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		// 过滤 Google Smart Chip 的 16x16 网站小图标链接
		if (host.EndsWith("google.com", StringComparison.OrdinalIgnoreCase) &&
			uri.AbsolutePath.StartsWith("/s2/favicons", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		// 过滤 Google Sheets 表格页面自身链接（保留 drive.google.com 视频链接）
		if (host.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase) &&
			uri.AbsolutePath.StartsWith("/spreadsheets/", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return false;
	}

	private async Task ParseUrlsAsync()
	{
		string[] urls = GetUrlsFromInput();
		if (urls.Length == 0)
		{
			MessageBox.Show(this, "请先在左侧输入或粘贴视频链接！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		_items.Clear();
		_listView.Items.Clear();

		for (int i = 0; i < urls.Length; i++)
		{
			var (platform, sourceType) = DownloadEngine.IdentifyPlatform(urls[i]);
			var item = new DownloadItem
			{
				Index = i + 1,
				Url = urls[i],
				Platform = platform,
				SourceType = sourceType,
				Title = "等待解析...",
				Status = "待解析"
			};
			_items.Add(item);

			ListViewItem lvi = new ListViewItem(item.Index.ToString());
			lvi.SubItems.Add(item.Platform);
			lvi.SubItems.Add(item.SourceType);
			lvi.SubItems.Add(item.Title);
			lvi.SubItems.Add(item.DurationStr);
			lvi.SubItems.Add("0%");
			lvi.SubItems.Add("--");
			lvi.SubItems.Add(item.Status);
			lvi.Tag = item;
			_listView.Items.Add(lvi);
		}

		string ytdlp = DownloadEngine.FindYtDlpPath();
		string proxy = GetEffectiveProxy();

		LogMessage($"[批量解析] 开始解析 {urls.Length} 个链接元数据...");
		using var cts = new CancellationTokenSource();

		foreach (var item in _items)
		{
			await DownloadEngine.ParseMetadataAsync(item, ytdlp, proxy, LogMessage, cts.Token);
			UpdateListViewRows();
		}
		LogMessage("[批量解析] 解析任务结束。");
	}

	private async Task StartDownloadAsync()
	{
		if (_isDownloading) return;

		string[] urls = GetUrlsFromInput();
		if (urls.Length == 0)
		{
			MessageBox.Show(this, "请在左侧输入需要下载的视频链接！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		// 检查当前任务列表与左侧输入框中的链接是否完全一致，若有任何变动则立即以最新输入的链接为准重建任务队列并刷新视图
		bool urlsChanged = (_items.Count != urls.Length);
		if (!urlsChanged)
		{
			for (int i = 0; i < urls.Length; i++)
			{
				if (!string.Equals(_items[i].Url, urls[i], StringComparison.OrdinalIgnoreCase))
				{
					urlsChanged = true;
					break;
				}
			}
		}

		if (urlsChanged)
		{
			_items.Clear();
			_listView.Items.Clear();
			for (int i = 0; i < urls.Length; i++)
			{
				var (platform, sourceType) = DownloadEngine.IdentifyPlatform(urls[i]);
				_items.Add(new DownloadItem
				{
					Index = i + 1,
					Url = urls[i],
					Platform = platform,
					SourceType = sourceType,
					Title = $"任务 #{i + 1}",
					Status = "准备中"
				});
			}
			UpdateListViewRows();
		}

		string ytdlp = DownloadEngine.FindYtDlpPath();
		string ffmpeg = DownloadEngine.FindFFmpegPath();
		string outDir = _txtOutputDir.Text.Trim();
		if (string.IsNullOrEmpty(outDir))
		{
			outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "视频批量下载");
			_txtOutputDir.Text = outDir;
		}

		string quality = _cboQuality.SelectedIndex switch
		{
			1 => "1080p",
			2 => "720p",
			3 => "audio",
			_ => "best"
		};

		bool resume = _chkResume.Checked;
		bool skipExisting = _chkSkipExisting.Checked;
		bool subfolderByPlatform = _chkSubfolderByPlatform.Checked;
		string proxy = GetEffectiveProxy();

		_isDownloading = true;
		_btnStartDownload.Enabled = false;
		_btnStopDownload.Enabled = true;
		_cts = new CancellationTokenSource();

		LogMessage($"[下载任务启动] 共 {_items.Count} 个任务，输出路径: {outDir}");
		_statusLabel.Text = $"正在下载视频中... (0/{_items.Count})";

		int concurrency = Math.Max(1, _cboThreads.SelectedIndex switch { 0 => 1, 1 => 2, 2 => 4, _ => 8 });
		using var semaphore = new SemaphoreSlim(concurrency);

		var tasks = _items.Select(async item =>
		{
			await semaphore.WaitAsync(_cts.Token);
			try
			{
				if (_cts.IsCancellationRequested) return;
				await DownloadEngine.DownloadAsync(
					item, ytdlp, ffmpeg, outDir, quality, resume, skipExisting,
					subfolderByPlatform, proxy, LogMessage, _cts.Token);
			}
			catch (Exception ex)
			{
				item.Status = "异常中断";
				item.ErrorMessage = ex.Message;
				LogMessage($"[异常] {item.Url}: {ex.Message}");
			}
			finally
			{
				semaphore.Release();
				UpdateListViewRows();
			}
		});

		try
		{
			await Task.WhenAll(tasks);
		}
		catch { }

		_isDownloading = false;
		_btnStartDownload.Enabled = true;
		_btnStopDownload.Enabled = false;

		int successCount = _items.Count(i => i.Status == "成功" || i.Status.Contains("已完成"));
		LogMessage($"[任务全部结束] 成功: {successCount} / 共: {_items.Count}");
		_statusLabel.Text = $"视频下载任务结束：成功 {successCount} 个文件。可随时一键发送至【视频拼屏】或【视频剪辑】。";
	}

	private void StopDownload()
	{
		if (_isDownloading && _cts != null)
		{
			_cts.Cancel();
			LogMessage("[停止] 正在中止下载任务...");
			_statusLabel.Text = "下载任务已手动中止。";
		}
	}

	private void OpenOutputDirectory()
	{
		string dir = _txtOutputDir.Text.Trim();
		if (!Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}
		Process.Start("explorer.exe", ScriptBatch.ScriptBatchComposer.QuoteArg(dir));
	}

	private string[] GetUrlsFromInput()
	{
		var results = new List<string>();

		foreach (string rawLine in _txtUrls.Lines)
		{
			string line = rawLine?.Trim();
			if (string.IsNullOrWhiteSpace(line))
				continue;

			if (line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
				line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				string normalized = NormalizeAndUnwrapClipboardUrl(line) ?? line;
				results.Add(normalized);
				continue;
			}

			// 兼容一行中带有表格标题/序号前缀或制表符的情况（如: "视频1\thttps://www.youtube.com/watch?v=xxx"）
			var lineSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Match m in Regex.Matches(line, @"https?://[^\s""'<>]+", RegexOptions.IgnoreCase))
			{
				string normalized = NormalizeAndUnwrapClipboardUrl(m.Value);
				if (!string.IsNullOrWhiteSpace(normalized) && lineSeen.Add(normalized))
					results.Add(normalized);
			}
		}

		return results.ToArray();
	}

	private string GetEffectiveProxy()
	{
		if (_cboProxy.SelectedIndex == 1) return "";
		if (_cboProxy.SelectedIndex == 2) return _txtCustomProxy.Text.Trim();
		try
		{
			var proxy = WebRequest.GetSystemWebProxy();
			var testUri = new Uri("https://www.youtube.com");
			var proxyUri = proxy.GetProxy(testUri);
			if (proxyUri != null && proxyUri != testUri)
			{
				return proxyUri.ToString();
			}
		}
		catch { }
		return "";
	}

	private void UpdateListViewRows()
	{
		if (InvokeRequired)
		{
			BeginInvoke(new Action(UpdateListViewRows));
			return;
		}

		while (_listView.Items.Count > _items.Count)
		{
			_listView.Items.RemoveAt(_listView.Items.Count - 1);
		}

		for (int i = 0; i < _items.Count; i++)
		{
			var item = _items[i];
			ListViewItem lvi;
			if (i < _listView.Items.Count)
			{
				lvi = _listView.Items[i];
			}
			else
			{
				lvi = new ListViewItem(item.Index.ToString());
				lvi.SubItems.Add(item.Platform);
				lvi.SubItems.Add(item.SourceType);
				lvi.SubItems.Add(item.Title);
				lvi.SubItems.Add(item.DurationStr);
				lvi.SubItems.Add("0%");
				lvi.SubItems.Add("--");
				lvi.SubItems.Add(item.Status);
				_listView.Items.Add(lvi);
			}

			lvi.Tag = item;
			SetSubItemText(lvi, 0, item.Index.ToString());
			SetSubItemText(lvi, 1, item.Platform);
			SetSubItemText(lvi, 2, item.SourceType);
			SetSubItemText(lvi, 3, item.Title);
			string durOrSize = string.IsNullOrEmpty(item.FileSizeStr) ? item.DurationStr : $"{item.DurationStr} / {item.FileSizeStr}";
			SetSubItemText(lvi, 4, durOrSize);
			SetSubItemText(lvi, 5, $"{item.ProgressPercent}%");
			SetSubItemText(lvi, 6, item.SpeedStr);
			SetSubItemText(lvi, 7, item.Status);

			Color targetColor = TextPrimary;
			if (item.Status == "成功" || item.Status.Contains("已完成"))
				targetColor = EmeraldNeon;
			else if (item.Status.Contains("失败") || item.Status.Contains("异常"))
				targetColor = Color.FromArgb(248, 113, 113);
			else if (item.Status.Contains("下载中"))
				targetColor = CyanNeon;

			if (lvi.ForeColor != targetColor) lvi.ForeColor = targetColor;
		}

		if (_items.Count > 0)
		{
			int avgPct = (int)_items.Average(it => it.ProgressPercent);
			int clamped = Math.Max(0, Math.Min(100, avgPct));
			if (_progressBar.Value != clamped) _progressBar.Value = clamped;
			string progText = $"总进度: {avgPct}% ({_items.Count(it => it.Status == "成功" || it.Status.Contains("已完成"))}/{_items.Count})";
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

	// ====================== 核心流转联动 (拼屏/剪辑/合并) ======================

	private void SendDownloadedToSplitScreen()
	{
		var downloadedFiles = _items
			.Where(it => !string.IsNullOrEmpty(it.SaveFilePath) && File.Exists(it.SaveFilePath))
			.Select(it => it.SaveFilePath)
			.ToList();

		if (downloadedFiles.Count == 0)
		{
			string outDir = _txtOutputDir.Text.Trim();
			if (Directory.Exists(outDir))
			{
				var recent = Directory.GetFiles(outDir, "*.mp4", SearchOption.AllDirectories)
					.OrderByDescending(f => File.GetLastWriteTime(f))
					.Take(4)
					.ToList();
				downloadedFiles.AddRange(recent);
			}
		}

		if (downloadedFiles.Count == 0)
		{
			MessageBox.Show(this, "当前尚无下载成功的视频文件，请先下载视频！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		for (int i = 0; i < downloadedFiles.Count; i++)
		{
			_mainForm.AddSplitScreenVideoPaths(new string[] { downloadedFiles[i] }, i);
		}

		_mainForm.SwitchToWorkspace(MainForm.WorkspaceSplitScreen);
		MessageBox.Show(this, $"已将 {downloadedFiles.Count} 个下载视频成功分发至【视频拼屏】工作台并就绪排版！", "流转成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void SendSelectedToSplitScreen(int targetRegion)
	{
		if (_listView.SelectedItems.Count == 0)
		{
			MessageBox.Show(this, "请在列表中选中要发送的视频！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		var item = _listView.SelectedItems[0].Tag as DownloadItem;
		if (item == null || string.IsNullOrEmpty(item.SaveFilePath) || !File.Exists(item.SaveFilePath))
		{
			MessageBox.Show(this, "该条目视频尚未下载完成或文件不存在！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		int reg = (targetRegion >= 0) ? targetRegion : _mainForm.SplitScreenSelectedRegion;
		_mainForm.AddSplitScreenVideoPaths(new string[] { item.SaveFilePath }, reg);
		_mainForm.SwitchToWorkspace(MainForm.WorkspaceSplitScreen);
	}

	private void SendDownloadedToCutEditor()
	{
		var first = _items.FirstOrDefault(it => !string.IsNullOrEmpty(it.SaveFilePath) && File.Exists(it.SaveFilePath));
		if (first == null)
		{
			MessageBox.Show(this, "当前尚无下载成功的视频！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		_mainForm.LoadVideoIntoCutEditor(first.SaveFilePath, clearExisting: true);
		_mainForm.SwitchToWorkspace(MainForm.WorkspaceCutEdit);
	}

	private void SendSelectedToCutEdit()
	{
		if (_listView.SelectedItems.Count == 0) return;
		var item = _listView.SelectedItems[0].Tag as DownloadItem;
		if (item != null && File.Exists(item.SaveFilePath))
		{
			_mainForm.LoadVideoIntoCutEditor(item.SaveFilePath, clearExisting: true);
			_mainForm.SwitchToWorkspace(MainForm.WorkspaceCutEdit);
		}
	}

	private void SendDownloadedToMerge()
	{
		var downloadedFiles = _items
			.Where(it => !string.IsNullOrEmpty(it.SaveFilePath) && File.Exists(it.SaveFilePath))
			.Select(it => it.SaveFilePath)
			.ToList();

		if (downloadedFiles.Count == 0)
		{
			MessageBox.Show(this, "当前尚无下载成功的视频！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		_mainForm.AddVideosToMergeList(downloadedFiles);
		_mainForm.SwitchToWorkspace(MainForm.WorkspaceMerge);
	}

	private void SendSelectedToMerge()
	{
		if (_listView.SelectedItems.Count == 0) return;
		var item = _listView.SelectedItems[0].Tag as DownloadItem;
		if (item != null && File.Exists(item.SaveFilePath))
		{
			_mainForm.AddVideosToMergeList(new[] { item.SaveFilePath });
			_mainForm.SwitchToWorkspace(MainForm.WorkspaceMerge);
		}
	}

	private void SendSelectedToTranscribe()
	{
		if (_listView.SelectedItems.Count == 0) return;
		var item = _listView.SelectedItems[0].Tag as DownloadItem;
		if (item != null)
		{
			if (File.Exists(item.SaveFilePath))
			{
				_mainForm.AddVideosToTranscribeList(new[] { item.SaveFilePath });
			}
			else if (!string.IsNullOrWhiteSpace(item.Url))
			{
				_mainForm.AddUrlsToTranscribeList(new[] { item.Url });
			}
		}
	}

	private void LocateSelectedFile()
	{
		if (_listView.SelectedItems.Count == 0) return;
		var item = _listView.SelectedItems[0].Tag as DownloadItem;
		if (item != null && File.Exists(item.SaveFilePath))
		{
			Process.Start("explorer.exe", $"/select,{ScriptBatch.ScriptBatchComposer.QuoteArg(item.SaveFilePath)}");
		}
	}

	private void PlaySelectedFile()
	{
		if (_listView.SelectedItems.Count == 0) return;
		var item = _listView.SelectedItems[0].Tag as DownloadItem;
		if (item != null && File.Exists(item.SaveFilePath))
		{
			Process.Start(new ProcessStartInfo(item.SaveFilePath) { UseShellExecute = true });
		}
	}

	/// <summary>
	/// 支持拦截 Ctrl+V / 右键粘贴 (WM_PASTE) 并自动提取表格超链接与 Google Sheets 巧克力胶囊链接的智能输入框
	/// </summary>
	private sealed class SmartUrlTextBox : TextBox
	{
		private const int WM_PASTE = 0x0302;
		private readonly Func<bool> _onSmartPaste;

		public SmartUrlTextBox(Func<bool> onSmartPaste)
		{
			_onSmartPaste = onSmartPaste;
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == WM_PASTE && _onSmartPaste != null)
			{
				try
				{
					if (_onSmartPaste())
						return;
				}
				catch
				{
				}
			}
			base.WndProc(ref m);
		}
	}
}
