using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoBatchMerger.Downloader;

/// <summary>
/// 视频下载板块完整用户控件（支持 Facebook Reel、YouTube、Shorts、TikTok、Google Drive 双模直链加速等）
/// </summary>
internal sealed class DownloaderPanel : UserControl
{
	private readonly MainForm _mainForm;
	private readonly List<DownloadItem> _items = new List<DownloadItem>();
	private CancellationTokenSource _cts;
	private bool _isDownloading;

	// UI 控件
	private Label _envStatusLabel;
	private Button _btnCheckEnv;
	private Button _btnInstallYtDlp;

	private TextBox _txtOutputDir;
	private Button _btnBrowseDir;
	private ComboBox _cboQuality;
	private ComboBox _cboThreads;
	private ComboBox _cboProxy;
	private TextBox _txtCustomProxy;
	private CheckBox _chkResume;
	private CheckBox _chkSkipExisting;
	private CheckBox _chkSubfolderByPlatform;
	private CheckBox _chkPlaylistFolder;

	private TextBox _txtUrls;
	private Button _btnReadClipboard;
	private Button _btnClearUrls;
	private Button _btnParsePreview;
	private Button _btnStartDownload;
	private Button _btnStopDownload;
	private Button _btnOpenFolder;

	private Button _btnSendAllToSplitScreen;
	private Button _btnSendToCutEdit;
	private Button _btnSendToMerge;

	private ListView _listView;
	private ProgressBar _progressBar;
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
		BackColor = Color.FromArgb(12, 16, 24);
		ForeColor = Color.FromArgb(226, 232, 240);
		Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);

		// 主容器（分层）
		Panel rootPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(14) };

		// 1. 顶部环境检测条 (Top Environment Bar)
		Panel envBar = CreateEnvironmentBar();
		envBar.Dock = DockStyle.Top;

		// 2. 核心设置区 (Settings Panel)
		Panel settingsBar = CreateSettingsBar();
		settingsBar.Dock = DockStyle.Top;

		// 3. 中间内容分栏 (SplitContainer: 左输入 420px 固定，右结果与日志自适应展开)
		SplitContainer splitMain = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Vertical,
			FixedPanel = FixedPanel.Panel1,
			SplitterDistance = 420,
			SplitterWidth = 8,
			BackColor = Color.FromArgb(12, 16, 24)
		};

		Panel leftPanel = CreateLeftInputPanel();
		leftPanel.Dock = DockStyle.Fill;
		splitMain.Panel1.Controls.Add(leftPanel);

		// 右侧上下分割 (上表格，下日志)
		SplitContainer splitRight = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			FixedPanel = FixedPanel.Panel1,
			SplitterDistance = 280,
			SplitterWidth = 8,
			BackColor = Color.FromArgb(12, 16, 24)
		};

		Panel rightTopPanel = CreateRightTablePanel();
		rightTopPanel.Dock = DockStyle.Fill;
		splitRight.Panel1.Controls.Add(rightTopPanel);

		Panel rightBottomPanel = CreateRightLogPanel();
		rightBottomPanel.Dock = DockStyle.Fill;
		splitRight.Panel2.Controls.Add(rightBottomPanel);

		splitMain.Panel2.Controls.Add(splitRight);

		splitMain.Resize += delegate
		{
			if (splitMain.Width > 700)
			{
				try { splitMain.SplitterDistance = 420; } catch { }
			}
		};

		// 4. 底部状态栏
		Panel bottomStatusBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 28,
			BackColor = Color.FromArgb(18, 24, 34),
			Padding = new Padding(12, 4, 12, 4)
		};
		_statusLabel = new Label
		{
			Text = "视频下载引擎已就绪（支持 Facebook Reel、YouTube、Shorts、TikTok、Google Drive 网盘等全平台）",
			ForeColor = Color.FromArgb(148, 163, 184),
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		};
		bottomStatusBar.Controls.Add(_statusLabel);

		// 组装根容器
		Panel centerWorkspace = new Panel { Dock = DockStyle.Fill };
		centerWorkspace.Controls.Add(splitMain);
		centerWorkspace.Controls.Add(settingsBar);
		centerWorkspace.Controls.Add(envBar);

		Controls.Add(centerWorkspace);
		Controls.Add(bottomStatusBar);

		// UI 刷新定时器
		_refreshTimer = new System.Windows.Forms.Timer { Interval = 250 };
		_refreshTimer.Tick += (s, e) => UpdateListViewRows();
		_refreshTimer.Start();

		ResumeLayout(false);
	}

	private Panel CreateEnvironmentBar()
	{
		Panel p = new Panel
		{
			Height = 44,
			BackColor = Color.FromArgb(18, 24, 34),
			Padding = new Padding(12, 6, 12, 6),
			Margin = new Padding(0, 0, 0, 8)
		};

		_envStatusLabel = new Label
		{
			Text = "检测组件环境中...",
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
			ForeColor = Color.FromArgb(52, 211, 153),
			AutoSize = true,
			Location = new Point(12, 12)
		};
		p.Controls.Add(_envStatusLabel);

		_btnInstallYtDlp = new Button
		{
			Text = "一键配置组件",
			Size = new Size(110, 28),
			Location = new Point(p.ClientSize.Width - 230, 8),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 99, 235),
			ForeColor = Color.White,
			Cursor = Cursors.Hand
		};
		_btnInstallYtDlp.FlatAppearance.BorderSize = 0;
		_btnInstallYtDlp.Click += async (s, e) => await InstallOrUpdateYtDlpAsync();
		p.Controls.Add(_btnInstallYtDlp);

		_btnCheckEnv = new Button
		{
			Text = "检测环境",
			Size = new Size(84, 28),
			Location = new Point(p.ClientSize.Width - 100, 8),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 45, 60),
			ForeColor = Color.FromArgb(226, 232, 240),
			Cursor = Cursors.Hand
		};
		_btnCheckEnv.FlatAppearance.BorderSize = 0;
		_btnCheckEnv.Click += (s, e) => RefreshEnvironmentStatus();
		p.Controls.Add(_btnCheckEnv);

		return p;
	}

	private Panel CreateSettingsBar()
	{
		Panel p = new Panel
		{
			Height = 84,
			BackColor = Color.FromArgb(20, 26, 38),
			Padding = new Padding(12, 8, 12, 8),
			Margin = new Padding(0, 6, 0, 8)
		};

		// 第一行：下载目录与画质
		Label lblDir = new Label { Text = "下载目录:", AutoSize = true, Location = new Point(12, 12), ForeColor = Color.FromArgb(148, 163, 184) };
		p.Controls.Add(lblDir);

		string defaultDownloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "视频批量下载");
		_txtOutputDir = new TextBox
		{
			Text = defaultDownloadPath,
			Location = new Point(80, 9),
			Width = 380,
			BackColor = Color.FromArgb(12, 16, 24),
			ForeColor = Color.White,
			BorderStyle = BorderStyle.FixedSingle
		};
		p.Controls.Add(_txtOutputDir);

		_btnBrowseDir = new Button
		{
			Text = "选择",
			Size = new Size(60, 24),
			Location = new Point(466, 8),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 45, 60),
			ForeColor = Color.FromArgb(226, 232, 240),
			Cursor = Cursors.Hand
		};
		_btnBrowseDir.FlatAppearance.BorderSize = 0;
		_btnBrowseDir.Click += (s, e) =>
		{
			using var fbd = new FolderBrowserDialog { SelectedPath = _txtOutputDir.Text };
			if (fbd.ShowDialog(this) == DialogResult.OK)
			{
				_txtOutputDir.Text = fbd.SelectedPath;
			}
		};
		p.Controls.Add(_btnBrowseDir);

		Label lblQuality = new Label { Text = "画质:", AutoSize = true, Location = new Point(540, 12), ForeColor = Color.FromArgb(148, 163, 184) };
		p.Controls.Add(lblQuality);

		_cboQuality = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(580, 9),
			Width = 140,
			BackColor = Color.FromArgb(12, 16, 24),
			ForeColor = Color.White
		};
		_cboQuality.Items.AddRange(new object[] { "最佳质量 (推荐)", "1080P 高清", "720P 标清", "仅提取音频 (MP3)" });
		_cboQuality.SelectedIndex = 0;
		p.Controls.Add(_cboQuality);

		Label lblThreads = new Label { Text = "并发:", AutoSize = true, Location = new Point(735, 12), ForeColor = Color.FromArgb(148, 163, 184) };
		p.Controls.Add(lblThreads);

		_cboThreads = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(775, 9),
			Width = 90,
			BackColor = Color.FromArgb(12, 16, 24),
			ForeColor = Color.White
		};
		_cboThreads.Items.AddRange(new object[] { "1 线程", "2 线程", "4 线程", "8 线程" });
		_cboThreads.SelectedIndex = 2; // 4 线程
		p.Controls.Add(_cboThreads);

		Label lblProxy = new Label { Text = "网络代理:", AutoSize = true, Location = new Point(880, 12), ForeColor = Color.FromArgb(148, 163, 184) };
		p.Controls.Add(lblProxy);

		_cboProxy = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Location = new Point(945, 9),
			Width = 150,
			BackColor = Color.FromArgb(12, 16, 24),
			ForeColor = Color.White
		};
		_cboProxy.Items.AddRange(new object[] { "系统代理 (推荐)", "直连 (无代理)", "自定义代理" });
		_cboProxy.SelectedIndex = 0;
		_cboProxy.SelectedIndexChanged += (s, e) =>
		{
			_txtCustomProxy.Visible = (_cboProxy.SelectedIndex == 2);
		};
		p.Controls.Add(_cboProxy);

		_txtCustomProxy = new TextBox
		{
			Text = "http://127.0.0.1:7890",
			Location = new Point(1105, 9),
			Width = 150,
			BackColor = Color.FromArgb(12, 16, 24),
			ForeColor = Color.White,
			BorderStyle = BorderStyle.FixedSingle,
			Visible = false
		};
		p.Controls.Add(_txtCustomProxy);

		// 第二行：复选框
		_chkResume = new CheckBox
		{
			Text = "断点续传",
			Checked = true,
			Location = new Point(80, 48),
			AutoSize = true,
			ForeColor = Color.FromArgb(203, 213, 225)
		};
		p.Controls.Add(_chkResume);

		_chkSkipExisting = new CheckBox
		{
			Text = "已完成则跳过",
			Checked = true,
			Location = new Point(180, 48),
			AutoSize = true,
			ForeColor = Color.FromArgb(203, 213, 225)
		};
		p.Controls.Add(_chkSkipExisting);

		_chkSubfolderByPlatform = new CheckBox
		{
			Text = "按平台分文件夹",
			Checked = true,
			Location = new Point(310, 48),
			AutoSize = true,
			ForeColor = Color.FromArgb(203, 213, 225)
		};
		p.Controls.Add(_chkSubfolderByPlatform);

		_chkPlaylistFolder = new CheckBox
		{
			Text = "播放列表分子文件夹",
			Checked = true,
			Location = new Point(450, 48),
			AutoSize = true,
			ForeColor = Color.FromArgb(203, 213, 225)
		};
		p.Controls.Add(_chkPlaylistFolder);

		return p;
	}

	private Panel CreateLeftInputPanel()
	{
		Panel p = new Panel
		{
			BackColor = Color.FromArgb(18, 24, 34),
			Padding = new Padding(12)
		};

		Label lblTitle = new Label
		{
			Text = "视频 / 播放列表链接 (支持批量混排)",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(56, 189, 248),
			Dock = DockStyle.Top,
			Height = 26
		};
		p.Controls.Add(lblTitle);

		_txtUrls = new TextBox
		{
			Multiline = true,
			ScrollBars = ScrollBars.Both,
			WordWrap = false,
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(10, 14, 20),
			ForeColor = Color.FromArgb(226, 232, 240),
			BorderStyle = BorderStyle.FixedSingle,
			Font = new Font("Consolas", 9f)
		};
		_txtUrls.Text = "https://www.facebook.com/reel/1116404280831830\r\nhttps://www.youtube.com/watch?v=XqJMIE6_9gY\r\nhttps://www.youtube.com/shorts/bqLBK7GuPCk\r\nhttps://www.tiktok.com/@lucia.gove/video/7617254633341308178\r\nhttps://drive.google.com/file/d/1msP8BcsLRbFe5G8b67roWlkJTYN5fIoL/view";

		Panel urlContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 8) };
		urlContainer.Controls.Add(_txtUrls);
		p.Controls.Add(urlContainer);

		// 下方按钮组容器 (FlowLayoutPanel)
		Panel btnBottomArea = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 140,
			Padding = new Padding(0, 4, 0, 0)
		};

		// 行 1: 从剪贴板读取、清空、解析预览
		_btnReadClipboard = MakeActionBtn("从剪贴板读取", 110, Color.FromArgb(37, 45, 60), (s, e) => ReadClipboardUrls());
		_btnReadClipboard.Location = new Point(0, 6);
		btnBottomArea.Controls.Add(_btnReadClipboard);

		_btnClearUrls = MakeActionBtn("清空链接", 80, Color.FromArgb(37, 45, 60), (s, e) => _txtUrls.Clear());
		_btnClearUrls.Location = new Point(116, 6);
		btnBottomArea.Controls.Add(_btnClearUrls);

		_btnParsePreview = MakeActionBtn("解析预览", 84, Color.FromArgb(37, 45, 60), async (s, e) => await ParseUrlsAsync());
		_btnParsePreview.Location = new Point(202, 6);
		btnBottomArea.Controls.Add(_btnParsePreview);

		// 行 2: 开始下载、停止、打开文件夹
		_btnStartDownload = MakeActionBtn("▶ 开始下载", 110, Color.FromArgb(37, 99, 235), async (s, e) => await StartDownloadAsync());
		_btnStartDownload.Location = new Point(0, 44);
		_btnStartDownload.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
		btnBottomArea.Controls.Add(_btnStartDownload);

		_btnStopDownload = MakeActionBtn("停止", 74, Color.FromArgb(220, 38, 38), (s, e) => StopDownload());
		_btnStopDownload.Location = new Point(116, 44);
		btnBottomArea.Controls.Add(_btnStopDownload);

		_btnOpenFolder = MakeActionBtn("打开文件夹", 90, Color.FromArgb(37, 45, 60), (s, e) => OpenOutputDirectory());
		_btnOpenFolder.Location = new Point(196, 44);
		btnBottomArea.Controls.Add(_btnOpenFolder);

		// 行 3: 专属流转大按钮 (一键发送至 视频拼屏、发送至 视频剪辑)
		_btnSendAllToSplitScreen = MakeActionBtn("▦ 一键发送至 视频拼屏", 175, Color.FromArgb(8, 145, 178), (s, e) => SendDownloadedToSplitScreen());
		_btnSendAllToSplitScreen.Location = new Point(0, 88);
		_btnSendAllToSplitScreen.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		btnBottomArea.Controls.Add(_btnSendAllToSplitScreen);

		_btnSendToCutEdit = MakeActionBtn("🎬 发送至剪辑", 96, Color.FromArgb(124, 58, 237), (s, e) => SendDownloadedToCutEditor());
		_btnSendToCutEdit.Location = new Point(182, 88);
		_btnSendToCutEdit.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		btnBottomArea.Controls.Add(_btnSendToCutEdit);

		_btnSendToMerge = MakeActionBtn("📦 送合并", 74, Color.FromArgb(37, 45, 60), (s, e) => SendDownloadedToMerge());
		_btnSendToMerge.Location = new Point(284, 88);
		btnBottomArea.Controls.Add(_btnSendToMerge);

		p.Controls.Add(btnBottomArea);
		return p;
	}

	private Panel CreateRightTablePanel()
	{
		Panel p = new Panel
		{
			BackColor = Color.FromArgb(18, 24, 34),
			Padding = new Padding(12)
		};

		Label lblHeader = new Label
		{
			Text = "预览 / 进度 (可右键发送至【视频拼屏】或【视频剪辑】)",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(56, 189, 248),
			Dock = DockStyle.Top,
			Height = 26
		};
		p.Controls.Add(lblHeader);

		_listView = new ListView
		{
			Dock = DockStyle.Fill,
			View = View.Details,
			FullRowSelect = true,
			GridLines = true,
			BackColor = Color.FromArgb(10, 14, 20),
			ForeColor = Color.FromArgb(226, 232, 240),
			BorderStyle = BorderStyle.FixedSingle,
			HeaderStyle = ColumnHeaderStyle.Nonclickable
		};

		_listView.Columns.Add("#", 38);
		_listView.Columns.Add("平台", 86);
		_listView.Columns.Add("来源", 78);
		_listView.Columns.Add("标题", 260);
		_listView.Columns.Add("时长/大小", 84);
		_listView.Columns.Add("进度", 70);
		_listView.Columns.Add("速度", 80);
		_listView.Columns.Add("状态", 110);

		// 右键快捷菜单
		_listView.ContextMenuStrip = CreateTableContextMenu();
		p.Controls.Add(_listView);

		_progressBar = new ProgressBar
		{
			Dock = DockStyle.Bottom,
			Height = 8,
			Minimum = 0,
			Maximum = 100,
			Value = 0
		};
		p.Controls.Add(_progressBar);

		return p;
	}

	private ContextMenuStrip CreateTableContextMenu()
	{
		ContextMenuStrip cms = new ContextMenuStrip();
		cms.BackColor = Color.FromArgb(20, 26, 38);
		cms.ForeColor = Color.White;

		var itemSplitScreen = new ToolStripMenuItem("▦ 发送此视频至【视频拼屏】");
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

	private Panel CreateRightLogPanel()
	{
		Panel p = new Panel
		{
			BackColor = Color.FromArgb(18, 24, 34),
			Padding = new Padding(12)
		};

		Panel titleRow = new Panel { Dock = DockStyle.Top, Height = 26 };
		Label lblLog = new Label
		{
			Text = "实时终端控制台日志",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(148, 163, 184),
			Dock = DockStyle.Left,
			AutoSize = true
		};
		titleRow.Controls.Add(lblLog);

		Button btnClearLog = new Button
		{
			Text = "清空日志",
			Size = new Size(68, 22),
			Dock = DockStyle.Right,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 45, 60),
			ForeColor = Color.FromArgb(203, 213, 225),
			Cursor = Cursors.Hand
		};
		btnClearLog.FlatAppearance.BorderSize = 0;
		btnClearLog.Click += (s, e) => _logBox.Clear();
		titleRow.Controls.Add(btnClearLog);
		p.Controls.Add(titleRow);

		_logBox = new RichTextBox
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(8, 12, 18),
			ForeColor = Color.FromArgb(52, 211, 153),
			BorderStyle = BorderStyle.None,
			Font = new Font("Consolas", 9f),
			ReadOnly = true
		};
		p.Controls.Add(_logBox);

		return p;
	}

	private Button MakeActionBtn(string text, int width, Color backColor, EventHandler onClick)
	{
		Button b = new Button
		{
			Text = text,
			Size = new Size(width, 32),
			FlatStyle = FlatStyle.Flat,
			BackColor = backColor,
			ForeColor = Color.White,
			Cursor = Cursors.Hand
		};
		b.FlatAppearance.BorderSize = 0;
		b.Click += onClick;
		return b;
	}

	// ====================== 核心业务逻辑 ======================

	public void RefreshEnvironmentStatus()
	{
		string ytdlp = DownloadEngine.FindYtDlpPath();
		string ffmpeg = DownloadEngine.FindFFmpegPath();
		string ffprobe = DownloadEngine.FindFFprobePath();

		bool ytdlpOk = !string.IsNullOrEmpty(ytdlp) && File.Exists(ytdlp);
		bool ffmpegOk = !string.IsNullOrEmpty(ffmpeg) && File.Exists(ffmpeg);
		bool ffprobeOk = !string.IsNullOrEmpty(ffprobe) && File.Exists(ffprobe);

		string text = "组件检测: ";
		text += ytdlpOk ? "✓ yt-dlp " : "✗ yt-dlp(未配置) ";
		text += ffmpegOk ? "✓ ffmpeg " : "✗ ffmpeg ";
		text += ffprobeOk ? "✓ ffprobe" : "✗ ffprobe";

		if (ytdlpOk && ffmpegOk)
		{
			_envStatusLabel.Text = $"高清环境就绪 | {text}";
			_envStatusLabel.ForeColor = Color.FromArgb(52, 211, 153);
			_btnInstallYtDlp.Text = "组件已就绪";
			_btnInstallYtDlp.BackColor = Color.FromArgb(37, 45, 60);
		}
		else
		{
			_envStatusLabel.Text = $"环境未完备 | {text}";
			_envStatusLabel.ForeColor = Color.FromArgb(248, 113, 113);
			_btnInstallYtDlp.Text = "一键配置组件";
			_btnInstallYtDlp.BackColor = Color.FromArgb(37, 99, 235);
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
		if (urls.Length == 0 && _items.Count == 0)
		{
			MessageBox.Show(this, "请在左侧输入需要下载的视频链接！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		if (_items.Count == 0 || urls.Length != _items.Count)
		{
			_items.Clear();
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

		if (successCount > 0)
		{
			var dlg = MessageBox.Show(
				this,
				$"下载完成！共成功下载 {successCount} 个视频。\n\n是否立即一键将下载的视频发送到【视频拼屏】工作台？",
				"下载成功",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Question);

			if (dlg == DialogResult.Yes)
			{
				SendDownloadedToSplitScreen();
			}
		}
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
		if (_cboProxy.SelectedIndex == 1) return ""; // 直连
		if (_cboProxy.SelectedIndex == 2) return _txtCustomProxy.Text.Trim(); // 自定义
		// 系统代理
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
				lvi.ForeColor = Color.FromArgb(52, 211, 153);
			else if (item.Status.Contains("失败") || item.Status.Contains("异常"))
				lvi.ForeColor = Color.FromArgb(248, 113, 113);
			else if (item.Status.Contains("下载中"))
				lvi.ForeColor = Color.FromArgb(56, 189, 248);
			else
				lvi.ForeColor = Color.FromArgb(226, 232, 240);
		}

		if (_items.Count > 0)
		{
			int avgPct = (int)_items.Average(it => it.ProgressPercent);
			_progressBar.Value = Math.Max(0, Math.Min(100, avgPct));
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

	/// <summary>
	/// 一键将所有下载成功的视频推送到【视频拼屏】工作台并智能分配区域
	/// </summary>
	private void SendDownloadedToSplitScreen()
	{
		var downloadedFiles = _items
			.Where(it => !string.IsNullOrEmpty(it.SaveFilePath) && File.Exists(it.SaveFilePath))
			.Select(it => it.SaveFilePath)
			.ToList();

		if (downloadedFiles.Count == 0)
		{
			// 尝试从输出目录查找最新视频
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

		// 智能分配：每个区域填入 1 个视频（如果有 4 个视频，分别填入区域 1、2、3、4）
		for (int i = 0; i < downloadedFiles.Count; i++)
		{
			_mainForm.AddSplitScreenVideoPaths(new string[] { downloadedFiles[i] }, i);
		}

		// 切换到【视频拼屏】Tab (Tab 索引 5)
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
