using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
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
		_refreshTimer = new System.Windows.Forms.Timer { Interval = 250 };
		_refreshTimer.Tick += (s, e) => UpdateListViewRows();
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

		_txtUrls = new TextBox
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

		_btnClearUrls = MakeTechButton("🗑 清空", 70, 30, Color.FromArgb(28, 42, 68), (s, e) => _txtUrls.Clear());
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
		string backupPath = @"D:\工具\视频拼接\VideoBatchStudio\yt-dlp.exe";

		if (File.Exists(targetPath) || File.Exists(backupPath))
		{
			LogMessage("[组件配置] 本地已找到可用组件！正在刷新系统状态...");
			RefreshEnvironmentStatus();
			MessageBox.Show(this, "核心组件已就绪，可直接解析并下载各平台视频！", "环境正常", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		try
		{
			LogMessage("[组件配置] 正在启动 winget 自动安装通道...");
			var psi = new ProcessStartInfo
			{
				FileName = "winget",
				Arguments = "install yt-dlp.yt-dlp --accept-source-agreements --accept-package-agreements",
				UseShellExecute = false,
				CreateNoWindow = true
			};
			using var proc = Process.Start(psi);
			await Task.Run(() => proc?.WaitForExit());

			RefreshEnvironmentStatus();
			MessageBox.Show(this, "组件安装完成！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		catch (Exception ex)
		{
			LogMessage($"[组件配置异常] {ex.Message}");
		}
	}

	private void ReadClipboardUrls()
	{
		if (Clipboard.ContainsText())
		{
			string text = Clipboard.GetText();
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (string.IsNullOrWhiteSpace(_txtUrls.Text))
					_txtUrls.Text = text.Trim();
				else
					_txtUrls.AppendText("\r\n" + text.Trim());
				LogMessage("[剪贴板] 成功从剪贴板读取并添加链接。");
			}
		}
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
		Process.Start("explorer.exe", $"\"{dir}\"");
	}

	private string[] GetUrlsFromInput()
	{
		return _txtUrls.Lines
			.Select(l => l.Trim())
			.Where(l => !string.IsNullOrWhiteSpace(l) && (l.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || l.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
			.Distinct()
			.ToArray();
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
			ListViewItem lvi = null;
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
			lvi.SubItems[0].Text = item.Index.ToString();
			lvi.SubItems[1].Text = item.Platform;
			lvi.SubItems[2].Text = item.SourceType;
			lvi.SubItems[3].Text = item.Title;
			lvi.SubItems[4].Text = string.IsNullOrEmpty(item.FileSizeStr) ? item.DurationStr : $"{item.DurationStr} / {item.FileSizeStr}";
			lvi.SubItems[5].Text = $"{item.ProgressPercent}%";
			lvi.SubItems[6].Text = item.SpeedStr;
			lvi.SubItems[7].Text = item.Status;

			if (item.Status == "成功" || item.Status.Contains("已完成"))
				lvi.ForeColor = EmeraldNeon;
			else if (item.Status.Contains("失败") || item.Status.Contains("异常"))
				lvi.ForeColor = Color.FromArgb(248, 113, 113);
			else if (item.Status.Contains("下载中"))
				lvi.ForeColor = CyanNeon;
			else
				lvi.ForeColor = TextPrimary;
		}

		if (_items.Count > 0)
		{
			int avgPct = (int)_items.Average(it => it.ProgressPercent);
			_progressBar.Value = Math.Max(0, Math.Min(100, avgPct));
			_progressTextLabel.Text = $"总进度: {avgPct}% ({_items.Count(it => it.Status == "成功" || it.Status.Contains("已完成"))}/{_items.Count})";
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

	private void LocateSelectedFile()
	{
		if (_listView.SelectedItems.Count == 0) return;
		var item = _listView.SelectedItems[0].Tag as DownloadItem;
		if (item != null && File.Exists(item.SaveFilePath))
		{
			Process.Start("explorer.exe", $"/select,\"{item.SaveFilePath}\"");
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
}
