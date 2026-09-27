using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoBatchMerger;

internal sealed class MainForm : Form
{
	private static readonly Color CanvasColor = Color.FromArgb(246, 248, 252);

	private static readonly Color SurfaceColor = Color.White;

	private static readonly Color InkColor = Color.FromArgb(31, 41, 55);

	private static readonly Color MutedColor = Color.FromArgb(100, 116, 139);

	private static readonly Color BorderColor = Color.FromArgb(222, 229, 238);

	private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);

	private static readonly Color AccentHoverColor = Color.FromArgb(29, 78, 216);

	private static readonly Color HeaderColor = Color.FromArgb(25, 52, 87);

	private static readonly object WatermarkFontLock = new object();

	private static PrivateFontCollection _watermarkPrivateFonts;

	private static bool _watermarkFontsLoaded;

	private static readonly string[] VideoExtensions = new string[14]
	{
		".mp4", ".mov", ".mkv", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".mts",
		".m2ts", ".3gp", ".mpg", ".mpeg"
	};

	private static readonly string[] ImageExtensions = new string[5] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

	private static readonly string[] AudioExtensions = new string[7] { ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".wma" };

	private readonly List<string> _videos = new List<string>();

	private readonly List<string> _splitVideos = new List<string>();

	private readonly List<string> _watermarkVideos = new List<string>();

	private readonly List<string> _watermarkSourceImages = new List<string>();

	private readonly Dictionary<string, VideoAdjustmentSettings> _mergeVideoAdjustments = new Dictionary<string, VideoAdjustmentSettings>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, VideoAdjustmentSettings> _splitVideoAdjustments = new Dictionary<string, VideoAdjustmentSettings>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, VideoAdjustmentSettings> _watermarkVideoAdjustments = new Dictionary<string, VideoAdjustmentSettings>(StringComparer.OrdinalIgnoreCase);

	private Dictionary<string, VideoAdjustmentSettings> _activeMergeVideoAdjustments = new Dictionary<string, VideoAdjustmentSettings>(StringComparer.OrdinalIgnoreCase);

	private Dictionary<string, VideoAdjustmentSettings> _activeSplitVideoAdjustments = new Dictionary<string, VideoAdjustmentSettings>(StringComparer.OrdinalIgnoreCase);

	private Dictionary<string, VideoAdjustmentSettings> _activeWatermarkVideoAdjustments = new Dictionary<string, VideoAdjustmentSettings>(StringComparer.OrdinalIgnoreCase);

	private bool _activeMergeFillCanvas = true;

	private OutputFrameSettings _activeMergeOutputFrame = new OutputFrameSettings();

	private OutputFrameSettings _activeSplitOutputFrame = new OutputFrameSettings();

	private OutputFrameSettings _activeWatermarkOutputFrame = new OutputFrameSettings();

	private VideoAdjustmentSettings _copiedVideoAdjustment;

	private readonly object _processLock = new object();

	private Process _currentProcess;

	private volatile bool _cancelRequested;

	private bool _isRunning;

	private string _latestMergeOutputFolder;

	private string _latestSplitOutputFolder;

	private string _latestWatermarkOutputFolder;

	private string _latestImageVideoOutputFolder;

	private readonly List<string> _slideshowImages = new List<string>();

	private ListView _videoList;

	private VideoAdjustmentEditor _mergeAdjustmentEditor;

	private NumericUpDown _groupSize;

	private NumericUpDown _maxRepeatsPerSource;

	private NumericUpDown _maxOutputCount;

	private ComboBox _combinationStartMode;

	private CheckBox _limitGroupDuration;

	private NumericUpDown _maxGroupDuration;

	private ComboBox _overlongVideoMode;

	private ComboBox _mergeCanvasFitMode;

	private OutputFrameControls _mergeOutputFrameControls;

	private CheckBox _similaritySort;

	private ComboBox _similarityMode;

	private NumericUpDown _similarityThreshold;

	private Button _shuffleButton;

	private Button _crossFolderButton;

	private CheckedListBox _transitionEffects;

	private Button _transitionSelectAllButton;

	private Button _transitionClearButton;

	private ComboBox _transitionOrder;

	private NumericUpDown _transitionDuration;

	private CheckBox _transitionLockSingleEffect;

	private TextBox _outputFolder;

	private Button _addButton;

	private Button _removeButton;

	private Button _upButton;

	private Button _downButton;

	private Button _clearButton;

	private Button _browseOutputButton;

	private Button _startButton;

	private Button _cancelButton;

	private Button _openOutputButton;

	private Button _mergeResetButton;

	private CheckBox _autoFallback;

	private ProgressBar _progressBar;

	private Label _statusLabel;

	private Label _countLabel;

	private TabControl _tabs;

	private ListView _splitVideoList;

	private VideoAdjustmentEditor _splitAdjustmentEditor;

	private Button _splitAddButton;

	private Button _splitRemoveButton;

	private Button _splitClearButton;

	private NumericUpDown _splitSeconds;

	private ComboBox _splitMode;

	private ComboBox _splitTailMode;

	private NumericUpDown _splitEqualParts;

	private Label _splitSecondsLabel;

	private Label _splitTailLabel;

	private Label _splitPartsLabel;

	private Label _splitModeHint;

	private TextBox _splitOutputFolder;

	private Button _splitBrowseOutputButton;

	private Button _splitOpenOutputButton;

	private Button _splitResetButton;

	private Button _splitStartButton;

	private Button _splitCancelButton;

	private ProgressBar _splitProgressBar;

	private Label _splitStatusLabel;

	private Label _splitCountLabel;

	private OutputFrameControls _splitOutputFrameControls;

	private CheckBox _watermarkOnMerge;

	private CheckBox _watermarkOnSplit;

	private CheckBox _watermarkOnSplitScreen;

	private TabControl _watermarkLayerTabs;

	private CheckedListBox _watermarkStayEffectPool;

	private Button _watermarkStaySelectAllButton;

	private Button _watermarkStayRandomButton;

	private Button _watermarkStayClearButton;

	private readonly CheckBox[] _textWatermarkEnabled = new CheckBox[3];

	private readonly TextBox[] _watermarkText = new TextBox[3];

	private readonly NumericUpDown[] _watermarkFontSize = new NumericUpDown[3];

	private readonly NumericUpDown[] _textWatermarkMaxWidth = new NumericUpDown[3];

	private readonly NumericUpDown[] _textWatermarkSafeMargin = new NumericUpDown[3];

	private readonly CheckBox[] _textWatermarkAllowOverflow = new CheckBox[3];

	private readonly CheckBox[] _textWatermarkAboveImages = new CheckBox[3];

	private readonly ComboBox[] _watermarkFontFamily = new ComboBox[3];

	private readonly CheckBox[] _watermarkFontBold = new CheckBox[3];

	private readonly CheckBox[] _watermarkFontItalic = new CheckBox[3];

	private readonly Button[] _watermarkColorButton = new Button[3];

	private readonly ComboBox[] _textWatermarkAlignment = new ComboBox[3];

	private readonly CheckBox[] _textWatermarkOutlineEnabled = new CheckBox[3];

	private readonly Button[] _textWatermarkOutlineColorButton = new Button[3];

	private readonly NumericUpDown[] _textWatermarkOutlineWidth = new NumericUpDown[3];

	private readonly NumericUpDown[] _textWatermarkOpacity = new NumericUpDown[3];

	private readonly ComboBox[] _textWatermarkPosition = new ComboBox[3];

	private readonly NumericUpDown[] _textWatermarkOffsetX = new NumericUpDown[3];

	private readonly NumericUpDown[] _textWatermarkOffsetY = new NumericUpDown[3];

	private readonly Button[] _textWatermarkCenter = new Button[3];

	private readonly Button[] _textWatermarkTopCenter = new Button[3];

	private readonly Button[] _textWatermarkBottomCenter = new Button[3];

	private readonly NumericUpDown[] _textWatermarkStart = new NumericUpDown[3];

	private readonly CheckBox[] _textWatermarkShowUntilEnd = new CheckBox[3];

	private readonly NumericUpDown[] _textWatermarkEnd = new NumericUpDown[3];

	private readonly ComboBox[] _textWatermarkEntryEffect = new ComboBox[3];

	private readonly NumericUpDown[] _textWatermarkEntryDuration = new NumericUpDown[3];

	private readonly ComboBox[] _textWatermarkExitEffect = new ComboBox[3];

	private readonly NumericUpDown[] _textWatermarkExitDuration = new NumericUpDown[3];

	private readonly ComboBox[] _textWatermarkStayEffect = new ComboBox[3];

	private readonly NumericUpDown[] _textWatermarkStayIntensity = new NumericUpDown[3];

	private readonly NumericUpDown[] _textWatermarkStayPeriod = new NumericUpDown[3];

	private readonly NumericUpDown[] _textWatermarkStayPause = new NumericUpDown[3];

	private readonly CheckBox[] _textWatermarkBackgroundEnabled = new CheckBox[3];

	private readonly Button[] _textWatermarkBackgroundColorButton = new Button[3];

	private readonly NumericUpDown[] _textWatermarkBackgroundOpacity = new NumericUpDown[3];

	private readonly NumericUpDown[] _textWatermarkBackgroundPaddingX = new NumericUpDown[3];

	private readonly NumericUpDown[] _textWatermarkBackgroundPaddingY = new NumericUpDown[3];

	private readonly NumericUpDown[] _textWatermarkBackgroundRadius = new NumericUpDown[3];

	private readonly ComboBox[] _textWatermarkBackgroundStyle = new ComboBox[3];

	private readonly CheckBox[] _imageWatermarkEnabled = new CheckBox[3];

	private readonly ListBox[] _imageWatermarkList = new ListBox[3];

	private readonly List<WatermarkSettings>[] _imageWatermarkItems = new List<WatermarkSettings>[3];

	private readonly int[] _imageWatermarkEditingIndex = new int[3] { -1, -1, -1 };

	private readonly TextBox[] _imageWatermarkPath = new TextBox[3];

	private readonly Button[] _imageWatermarkBrowse = new Button[3];

	private readonly Button[] _imageWatermarkRemove = new Button[3];

	private readonly Button[] _imageWatermarkClear = new Button[3];

	private readonly Button[] _imageWatermarkApplyAll = new Button[3];

	private readonly NumericUpDown[] _imageWatermarkRandomCount = new NumericUpDown[3];

	private readonly ComboBox[] _imageWatermarkAssignmentMode = new ComboBox[3];

	private readonly ComboBox[] _imageWatermarkPlaybackMode = new ComboBox[3];

	private readonly ComboBox[] _imageWatermarkSwitchEffect = new ComboBox[3];

	private readonly NumericUpDown[] _imageWatermarkSwitchDuration = new NumericUpDown[3];

	private readonly NumericUpDown[] _imageWatermarkSwitchInterval = new NumericUpDown[3];

	private readonly NumericUpDown[] _imageWatermarkScale = new NumericUpDown[3];

	private readonly NumericUpDown[] _imageWatermarkOpacity = new NumericUpDown[3];

	private readonly ComboBox[] _imageWatermarkPosition = new ComboBox[3];

	private readonly NumericUpDown[] _imageWatermarkOffsetX = new NumericUpDown[3];

	private readonly NumericUpDown[] _imageWatermarkOffsetY = new NumericUpDown[3];

	private readonly CheckBox[] _imageWatermarkAllowOverflow = new CheckBox[3];

	private readonly Button[] _imageWatermarkCenter = new Button[3];

	private readonly NumericUpDown[] _imageWatermarkStart = new NumericUpDown[3];

	private readonly CheckBox[] _imageWatermarkShowUntilEnd = new CheckBox[3];

	private readonly NumericUpDown[] _imageWatermarkEnd = new NumericUpDown[3];

	private readonly ComboBox[] _imageWatermarkEntryEffect = new ComboBox[3];

	private readonly NumericUpDown[] _imageWatermarkEntryDuration = new NumericUpDown[3];

	private readonly ComboBox[] _imageWatermarkExitEffect = new ComboBox[3];

	private readonly NumericUpDown[] _imageWatermarkExitDuration = new NumericUpDown[3];

	private readonly ComboBox[] _imageWatermarkStayEffect = new ComboBox[3];

	private readonly NumericUpDown[] _imageWatermarkStayIntensity = new NumericUpDown[3];

	private readonly NumericUpDown[] _imageWatermarkStayPeriod = new NumericUpDown[3];

	private readonly NumericUpDown[] _imageWatermarkStayPause = new NumericUpDown[3];

	private readonly NumericUpDown[] _imageWatermarkSlideDuration = new NumericUpDown[3];

	private Label _watermarkHint;

	private readonly Color[] _watermarkColor = new Color[3]
	{
		Color.White,
		Color.White,
		Color.White
	};

	private readonly Color[] _watermarkBackgroundColor = new Color[3]
	{
		Color.Black,
		Color.Black,
		Color.Black
	};

	private readonly Color[] _watermarkOutlineColor = new Color[3]
	{
		Color.Black,
		Color.Black,
		Color.Black
	};

	private ListView _watermarkVideoList;

	private TabControl _watermarkSourceTabs;

	private ListBox _watermarkSourceImageList;

	private Button _watermarkSourceImageAddButton;

	private Button _watermarkSourceImageRemoveButton;

	private Button _watermarkSourceImageClearButton;

	private Label _watermarkSourceImageCountLabel;

	private VideoAdjustmentEditor _watermarkAdjustmentEditor;

	private Button _watermarkVideoAddButton;

	private Button _watermarkVideoRemoveButton;

	private Button _watermarkVideoClearButton;

	private Label _watermarkVideoCountLabel;

	private TextBox _watermarkOutputFolder;

	private Button _watermarkOutputBrowseButton;

	private Button _watermarkOutputOpenButton;

	private Button _watermarkStartButton;

	private Button _watermarkCancelButton;

	private ProgressBar _watermarkProgressBar;

	private Label _watermarkStatusLabel;

	private Button _watermarkResetButton;

	private OutputFrameControls _watermarkOutputFrameControls;

	private Button _globalResetButton;

	private BgmControls _mergeBgm;

	private BgmControls _splitBgm;

	private BgmControls _watermarkBgm;

	private BgmControls _imageBgm;

	private BgmPlan _activeMergeBgmPlan = new BgmPlan();

	private BgmPlan _activeSplitBgmPlan = new BgmPlan();

	private BgmPlan _activeWatermarkBgmPlan = new BgmPlan();

	private ListBox _slideshowImageList;

	private Button _imageAddButton;

	private Button _imageRemoveButton;

	private Button _imageUpButton;

	private Button _imageDownButton;

	private Button _imageShuffleButton;

	private Button _imageClearButton;

	private Button _imageResetButton;

	private Label _imageCountLabel;

	private NumericUpDown _imageDuration;

	private NumericUpDown _imageOutputCount;

	private NumericUpDown _imageTargetDuration;

	private ComboBox _imageResolution;

	private ComboBox _imageLayoutPreset;

	private ComboBox _imageTileAnimation;

	private NumericUpDown _imageTileAnimationDuration;

	private CheckedListBox _imageLayoutPool;

	private Button _imageLayoutSelectAllButton;

	private Button _imageLayoutClearButton;

	private ComboBox _imageLayoutOrder;

	private CheckedListBox _imageMotionPool;

	private Button _imageMotionSelectAllButton;

	private Button _imageMotionClearButton;

	private ComboBox _imageMotionOrder;

	private CheckBox _imageSmartEnhance;

	private CheckedListBox _imageTransitions;

	private Button _imageTransitionSelectAllButton;

	private Button _imageTransitionClearButton;

	private ComboBox _imageTransitionOrder;

	private NumericUpDown _imageTransitionDuration;

	private TextBox _imageOutputFolder;

	private Button _imageOutputBrowseButton;

	private Button _imageOutputOpenButton;

	private Button _imageStartButton;

	private Button _imageCancelButton;

	private ProgressBar _imageProgressBar;

	private Label _imageStatusLabel;

	private readonly List<SplitScreenLayoutDefinition> _splitScreenLayouts = new List<SplitScreenLayoutDefinition>();

	private readonly List<string>[] _splitScreenRegionVideos = new List<string>[9]
	{
		new List<string>(),
		new List<string>(),
		new List<string>(),
		new List<string>(),
		new List<string>(),
		new List<string>(),
		new List<string>(),
		new List<string>(),
		new List<string>()
	};

	private readonly int[] _splitScreenPreviewIndices = new int[9];

	private readonly Dictionary<string, SplitScreenRegionSettings[]> _splitScreenRegionSettingsByLayout = new Dictionary<string, SplitScreenRegionSettings[]>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, bool[]> _splitScreenEnabledRegionsByLayout = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, bool[]> _splitScreenBorderRegionsByLayout = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, int> _splitScreenMainRegionByLayout = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, SplitScreenVideoViewSettings> _splitScreenVideoViewSettings = new Dictionary<string, SplitScreenVideoViewSettings>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, Image> _splitScreenPreviewImages = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _splitScreenPendingPreviews = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly object _splitScreenPreviewLock = new object();

	private SplitScreenLayoutDefinition _activeSplitScreenLayout;

	private int _splitScreenSelectedRegion;

	private TabControl _splitScreenPresetTabs;

	private ListView _splitScreenQuickPresets;

	private ListView _splitScreenGridPresets;

	private ListView _splitScreenPipPresets;

	private PictureBox _splitScreenPreview;

	private Control _splitScreenPreviewHost;

	private bool _splitScreenPreviewLayoutBusy;

	private Button _splitScreenPlayPreviewButton;

	private ComboBox _splitScreenPreviewDuration;

	private Button _mergePreviewButton;

	private ComboBox _splitScreenRegionSelector;

	private ListBox _splitScreenRegionList;

	private Button _splitScreenAddButton;

	private Button _splitScreenRemoveButton;

	private Button _splitScreenClearButton;

	private Button _splitScreenMoveUpButton;

	private Button _splitScreenMoveDownButton;

	private Label _splitScreenRegionCountLabel;

	private ComboBox _splitScreenAssignmentMode;

	private ComboBox _splitScreenDurationMode;

	private ComboBox _splitScreenMainRegion;

	private ComboBox _splitScreenCanvas;

	private NumericUpDown _splitScreenDuration;

	private NumericUpDown _splitScreenOutputCount;

	private ComboBox _splitScreenAudioRegion;

	private NumericUpDown _splitScreenRegionVolume;

	private ComboBox _splitScreenBorderPreset;

	private ComboBox _splitScreenBorderScope;

	private Button _splitScreenBorderRegionsButton;

	private NumericUpDown _splitScreenBorderWidth;

	private Button _splitScreenBorderColorButton;

	private ComboBox _splitScreenWatermarkLayer;

	private ComboBox _splitScreenWatermarkPosition;

	private NumericUpDown _splitScreenWatermarkOffsetX;

	private NumericUpDown _splitScreenWatermarkOffsetY;

	private Button _splitScreenWatermarkRefreshButton;

	private readonly List<int> _splitScreenWatermarkLayerKeys = new List<int>();

	private ComboBox _splitScreenPipShape;

	private ComboBox _splitScreenPipAspect;

	private ComboBox _splitScreenPipPosition;

	private NumericUpDown _splitScreenPipSize;

	private NumericUpDown _splitScreenPipOffsetX;

	private NumericUpDown _splitScreenPipOffsetY;

	private NumericUpDown _splitScreenVideoScale;

	private NumericUpDown _splitScreenVideoCropX;

	private NumericUpDown _splitScreenVideoCropY;

	private Button _splitScreenVideoApplyRegionButton;

	private Button _splitScreenVideoResetButton;

	private CheckBox _splitScreenRegionEnabled;

	private Button _splitScreenRestoreRegionsButton;

	private TextBox _splitScreenOutputFolder;

	private Button _splitScreenOutputBrowseButton;

	private Button _splitScreenOutputOpenButton;

	private Button _splitScreenResetButton;

	private Button _splitScreenStartButton;

	private Button _splitScreenCancelButton;

	private ProgressBar _splitScreenProgressBar;

	private Label _splitScreenStatusLabel;

	private BgmControls _splitScreenBgm;

	private ListBox _splitScreenBgmList;

	private BgmPlan _activeSplitScreenBgmPlan = new BgmPlan();

	private Color _splitScreenBorderColor = Color.White;

	private string _latestSplitScreenOutputFolder;

	private bool _loadingSplitScreenRegionControls;

	private bool _loadingSplitScreenWatermarkControls;

	private Bitmap _splitScreenWatermarkPreviewOverlay;

	private bool _splitScreenWatermarkPreviewLoaded;

	public MainForm()
	{
		Text = "视频批处理工具 V7.5";
		base.StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(1180, 1040);
		Rectangle rectangle = ((Screen.PrimaryScreen == null) ? new Rectangle(0, 0, 1366, 768) : Screen.PrimaryScreen.WorkingArea);
		base.Size = new Size(Math.Min(1680, Math.Max(MinimumSize.Width, rectangle.Width - 16)), Math.Min(1100, Math.Max(MinimumSize.Height, rectangle.Height - 12)));
		if (rectangle.Width < 1660 || rectangle.Height < 1080)
		{
			base.WindowState = FormWindowState.Maximized;
		}
		Font = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point);
		BackColor = CanvasColor;
		DoubleBuffered = true;
		try
		{
			base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
			base.Icon = SystemIcons.Application;
		}
		AllowDrop = true;
		for (int i = 0; i < 3; i++)
		{
			_imageWatermarkItems[i] = new List<WatermarkSettings>();
		}
		InitializeSplitScreenLayouts();
		BuildInterface();
		HookEvents();
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
		if (string.IsNullOrWhiteSpace(folderPath))
		{
			folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		}
		_outputFolder.Text = Path.Combine(folderPath, "批量合并输出");
		_splitOutputFolder.Text = Path.Combine(folderPath, "批量拆分输出");
		_watermarkOutputFolder.Text = Path.Combine(folderPath, "水印输出");
		_imageOutputFolder.Text = Path.Combine(folderPath, "图片成片输出");
		_splitScreenOutputFolder.Text = Path.Combine(folderPath, "视频拼屏输出");
		bool flag = LoadUserSettings();
		UpdateListView(null);
		UpdateSplitListView(null);
		UpdateWatermarkVideoList(null);
		UpdateWatermarkSourceImageList();
		UpdateSlideshowImageList();
		UpdateSplitModeUi();
		UpdateMergePlanningUi();
		UpdateWatermarkUi();
		UpdateWatermarkSourceModeUi();
		UpdateImageLayoutUi();
		RefreshSplitScreenRegionUi();
		RefreshSplitScreenPreview();
		UpdateSplitScreenPlanningUi();
		LoadSplitScreenWatermarksIntoPreview(userInitiated: false);
		if (flag)
		{
			_statusLabel.Text = "已恢复上次使用的参数设置；视频列表不会自动恢复。";
		}
	}

	private void BuildInterface()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 2;
		tableLayoutPanel.Margin = new Padding(0);
		tableLayoutPanel.Padding = new Padding(0);
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		base.Controls.Add(tableLayoutPanel);
		Panel header = new Panel();
		header.Dock = DockStyle.Fill;
		header.Margin = new Padding(0);
		header.BackColor = HeaderColor;
		Panel panel = new Panel();
		panel.Dock = DockStyle.Bottom;
		panel.Height = 3;
		panel.BackColor = Color.FromArgb(59, 130, 246);
		Panel value = panel;
		header.Controls.Add(value);
		Label label = new Label();
		label.AutoSize = true;
		label.Location = new Point(24, 11);
		label.Font = new Font("Microsoft YaHei UI", 16.5f, FontStyle.Bold);
		label.ForeColor = Color.White;
		label.Text = "视频批处理工具 V7.5  ·  合并 / 拆分 / 水印 / 视频拼屏 / 图片成片 / BGM";
		header.Controls.Add(label);
		_globalResetButton = MakeButton("重置全部设置", 126);
		_globalResetButton.Location = new Point(0, 14);
		_globalResetButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_globalResetButton.BackColor = Color.FromArgb(255, 247, 237);
		_globalResetButton.ForeColor = Color.FromArgb(154, 52, 18);
		header.Controls.Add(_globalResetButton);
		header.Resize += delegate
		{
			_globalResetButton.Left = Math.Max(28, header.ClientSize.Width - _globalResetButton.Width - 30);
		};
		tableLayoutPanel.Controls.Add(header, 0, 0);
		_tabs = new TabControl();
		_tabs.Dock = DockStyle.Fill;
		_tabs.Margin = new Padding(10, 7, 10, 10);
		_tabs.Font = new Font("Microsoft YaHei UI", 10f);
		_tabs.TabPages.Add(BuildMergeTab());
		_tabs.TabPages.Add(BuildSplitTab());
		_tabs.TabPages.Add(BuildWatermarkTab());
		_tabs.TabPages.Add(BuildSplitScreenTab());
		_tabs.TabPages.Add(BuildImageVideoTab());
		tableLayoutPanel.Controls.Add(_tabs, 0, 1);
		ApplyModernVisualStyle(this);
	}

	private TabPage BuildMergeTab()
	{
		TabPage tabPage = new TabPage("批量合并");
		tabPage.BackColor = Color.FromArgb(245, 247, 250);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 3;
		tableLayoutPanel.Margin = new Padding(0);
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 473f));
		tabPage.Controls.Add(tableLayoutPanel);
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.Margin = new Padding(0);
		panel.Padding = new Padding(12, 10, 12, 6);
		panel.BackColor = Color.White;
		Panel panel2 = panel;
		_addButton = MakeButton("＋ 添加视频", 106);
		_removeButton = MakeButton("移除", 68);
		_upButton = MakeButton("上移", 62);
		_downButton = MakeButton("下移", 62);
		_shuffleButton = MakeButton("随机打乱", 88);
		_crossFolderButton = MakeButton("来源文件夹交叉", 126);
		_clearButton = MakeButton("清空", 62);
		_mergeResetButton = MakeButton("重置本页参数", 112);
		_countLabel = MakeCountLabel();
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.WrapContents = false;
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		flowLayoutPanel2.Controls.Add(_addButton);
		flowLayoutPanel2.Controls.Add(_removeButton);
		flowLayoutPanel2.Controls.Add(_upButton);
		flowLayoutPanel2.Controls.Add(_downButton);
		flowLayoutPanel2.Controls.Add(_shuffleButton);
		flowLayoutPanel2.Controls.Add(_crossFolderButton);
		flowLayoutPanel2.Controls.Add(_clearButton);
		flowLayoutPanel2.Controls.Add(_mergeResetButton);
		flowLayoutPanel2.Controls.Add(_countLabel);
		panel2.Controls.Add(flowLayoutPanel2);
		tableLayoutPanel.Controls.Add(panel2, 0, 0);
		Panel panel3 = MakeListHost();
		_videoList = MakeVideoList();
		_mergeAdjustmentEditor = MakeVideoAdjustmentEditor();
		InstallVideoListArea(panel3, _videoList, _mergeAdjustmentEditor);
		tableLayoutPanel.Controls.Add(panel3, 0, 1);
		Panel panel4 = new Panel();
		panel4.Dock = DockStyle.Fill;
		panel4.Margin = new Padding(0);
		panel4.BackColor = Color.White;
		panel4.Size = new Size(base.ClientSize.Width, 473);
		Panel panel5 = panel4;
		panel5.Controls.Add(MakeLabel("每组最多数量", 20, 20));
		_groupSize = new NumericUpDown
		{
			Location = new Point(125, 16),
			Width = 76,
			Minimum = 1m,
			Maximum = 100m,
			Value = 3m
		};
		panel5.Controls.Add(_groupSize);
		_limitGroupDuration = new CheckBox
		{
			AutoSize = true,
			Checked = false,
			Location = new Point(230, 18),
			Text = "限制每个输出总时长"
		};
		panel5.Controls.Add(_limitGroupDuration);
		_maxGroupDuration = new NumericUpDown
		{
			Location = new Point(392, 16),
			Width = 88,
			Minimum = 5m,
			Maximum = 86400m,
			Value = 120m
		};
		panel5.Controls.Add(_maxGroupDuration);
		panel5.Controls.Add(MakeLabel("秒", 486, 20));
		panel5.Controls.Add(MakeLabel("截取规则", 535, 20));
		_overlongVideoMode = new ComboBox
		{
			Location = new Point(604, 16),
			Width = 290,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_overlongVideoMode.Items.AddRange(new object[2] { "组内平均截取开头，统一成品时长", "组内每个视频随机截取一段，统一成品时长" });
		_overlongVideoMode.SelectedIndex = 0;
		panel5.Controls.Add(_overlongVideoMode);
		_similaritySort = new CheckBox
		{
			AutoSize = true,
			Checked = false,
			Location = new Point(20, 56),
			Text = "按相似画面自动排序"
		};
		panel5.Controls.Add(_similaritySort);
		panel5.Controls.Add(MakeLabel("分析方式", 190, 57));
		_similarityMode = new ComboBox
		{
			Location = new Point(260, 52),
			Width = 180,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_similarityMode.Items.AddRange(new object[2] { "快速分析（1 个画面）", "精细分析（3 个画面，推荐）" });
		_similarityMode.SelectedIndex = 1;
		panel5.Controls.Add(_similarityMode);
		panel5.Controls.Add(MakeLabel("相似要求", 466, 57));
		_similarityThreshold = new NumericUpDown
		{
			Location = new Point(538, 52),
			Width = 72,
			Minimum = 20m,
			Maximum = 95m,
			Value = 65m
		};
		panel5.Controls.Add(_similarityThreshold);
		panel5.Controls.Add(MakeLabel("%（数值越高，归类越严格）", 616, 57));
		panel5.Controls.Add(MakeLabel("每视频额外重复", 20, 91));
		_maxRepeatsPerSource = new NumericUpDown
		{
			Location = new Point(138, 86),
			Width = 66,
			Minimum = 0m,
			Maximum = 100m,
			Value = 0m
		};
		panel5.Controls.Add(_maxRepeatsPerSource);
		panel5.Controls.Add(MakeLabel("次（0=禁止重复）", 211, 91));
		panel5.Controls.Add(MakeLabel("最多输出总数", 350, 91));
		_maxOutputCount = new NumericUpDown
		{
			Location = new Point(455, 86),
			Width = 76,
			Minimum = 0m,
			Maximum = 10000m,
			Value = 0m
		};
		panel5.Controls.Add(_maxOutputCount);
		panel5.Controls.Add(MakeLabel("个（0=不限）", 538, 91));
		panel5.Controls.Add(MakeLabel("组合起点", 660, 91));
		_combinationStartMode = new ComboBox
		{
			Location = new Point(731, 86),
			Width = 166,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_combinationStartMode.Items.AddRange(new object[2] { "按列表顺序", "随机起点组合" });
		_combinationStartMode.SelectedIndex = 0;
		panel5.Controls.Add(_combinationStartMode);
		panel5.Controls.Add(MakeLabel("转场（可多选）", 20, 122));
		_transitionEffects = new CheckedListBox
		{
			Location = new Point(128, 111),
			Size = new Size(330, 110),
			CheckOnClick = true,
			IntegralHeight = false,
			MultiColumn = true,
			ColumnWidth = 162
		};
		_transitionEffects.Items.AddRange(((IEnumerable<TransitionSpec>)CreateTransitionCatalog(1.0)).Select((Func<TransitionSpec, object>)((TransitionSpec x) => x.DisplayName)).ToArray());
		panel5.Controls.Add(_transitionEffects);
		_transitionSelectAllButton = MakeButton("全选", 58);
		_transitionSelectAllButton.Location = new Point(468, 112);
		panel5.Controls.Add(_transitionSelectAllButton);
		_transitionClearButton = MakeButton("清空", 58);
		_transitionClearButton.Location = new Point(468, 150);
		panel5.Controls.Add(_transitionClearButton);
		panel5.Controls.Add(MakeLabel("使用方式", 548, 122));
		_transitionOrder = new ComboBox
		{
			Location = new Point(618, 117),
			Width = 142,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_transitionOrder.Items.AddRange(new object[2] { "按列表顺序循环", "每次随机选择" });
		_transitionOrder.SelectedIndex = 0;
		panel5.Controls.Add(_transitionOrder);
		panel5.Controls.Add(MakeLabel("转场时间", 548, 160));
		_transitionDuration = new NumericUpDown
		{
			Location = new Point(618, 155),
			Width = 72,
			Minimum = 0.1m,
			Maximum = 30m,
			Value = 1m,
			Increment = 0.1m,
			DecimalPlaces = 1,
			TextAlign = HorizontalAlignment.Center
		};
		panel5.Controls.Add(_transitionDuration);
		panel5.Controls.Add(MakeLabel("秒", 696, 160));
		_autoFallback = new CheckBox
		{
			AutoSize = true,
			Checked = true,
			Location = new Point(780, 121),
			Text = "参数不一致时自动兼容转换"
		};
		panel5.Controls.Add(_autoFallback);
		_transitionLockSingleEffect = new CheckBox
		{
			AutoSize = true,
			Checked = false,
			Location = new Point(780, 159),
			Text = "同一成品锁定一种转场"
		};
		panel5.Controls.Add(_transitionLockSingleEffect);
		_mergeOutputFrameControls = InstallOutputFrameControls(panel5, 20, 236);
		panel5.Controls.Add(MakeLabel("画面填充", 810, 241));
		_mergeCanvasFitMode = new ComboBox
		{
			Location = new Point(880, 236),
			Width = 240,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_mergeCanvasFitMode.Items.AddRange(new object[2] { "自动放大铺满（推荐，居中裁边）", "完整显示（可能出现黑边）" });
		_mergeCanvasFitMode.SelectedIndex = 0;
		panel5.Controls.Add(_mergeCanvasFitMode);
		_mergeBgm = InstallBgmControls(panel5, 20, 268);
		panel5.Controls.Add(MakeLabel("总输出目录", 20, 317));
		_outputFolder = new TextBox
		{
			Location = new Point(110, 313),
			Width = 708,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		panel5.Controls.Add(_outputFolder);
		_browseOutputButton = MakeButton("选择…", 72);
		_browseOutputButton.Location = new Point(830, 310);
		_browseOutputButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		panel5.Controls.Add(_browseOutputButton);
		_openOutputButton = MakeButton("打开", 64);
		_openOutputButton.Location = new Point(910, 310);
		_openOutputButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		panel5.Controls.Add(_openOutputButton);
		_progressBar = new ProgressBar
		{
			Location = new Point(20, 356),
			Height = 18,
			Width = 954,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		panel5.Controls.Add(_progressBar);
		_statusLabel = new Label
		{
			Location = new Point(20, 381),
			Size = new Size(954, 24),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			ForeColor = Color.FromArgb(77, 87, 101),
			AutoEllipsis = true,
			Text = "就绪：可拖入视频文件或包含视频的文件夹"
		};
		panel5.Controls.Add(_statusLabel);
		_startButton = MakePrimaryButton("开始批量合并", 20, 417, 150);
		panel5.Controls.Add(_startButton);
		_cancelButton = MakeButton("取消", 90);
		_cancelButton.Location = new Point(180, 417);
		_cancelButton.Height = 42;
		_cancelButton.Enabled = false;
		panel5.Controls.Add(_cancelButton);
		_mergePreviewButton = MakeButton("▶ 播放合并预览 (第1组)", 175);
		_mergePreviewButton.Location = new Point(280, 417);
		_mergePreviewButton.Height = 42;
		panel5.Controls.Add(_mergePreviewButton);
		Label label = MakeLabel("每批会自动创建“日期_批量合并_编号”文件夹；原视频不会被修改。", 465, 431);
		label.ForeColor = Color.FromArgb(110, 119, 132);
		panel5.Controls.Add(label);
		tableLayoutPanel.Controls.Add(panel5, 0, 2);
		UpdateMergePlanningUi();
		return tabPage;
	}

	private TabPage BuildSplitTab()
	{
		TabPage tabPage = new TabPage("视频拆分");
		tabPage.BackColor = Color.FromArgb(245, 247, 250);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 3;
		tableLayoutPanel.Margin = new Padding(0);
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 334f));
		tabPage.Controls.Add(tableLayoutPanel2);
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.Margin = new Padding(0);
		panel.Padding = new Padding(12, 10, 12, 6);
		panel.BackColor = Color.White;
		Panel panel2 = panel;
		_splitAddButton = MakeButton("＋ 添加视频", 106);
		_splitRemoveButton = MakeButton("移除", 68);
		_splitClearButton = MakeButton("清空", 62);
		_splitResetButton = MakeButton("重置本页参数", 112);
		_splitCountLabel = MakeCountLabel();
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.WrapContents = false;
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		flowLayoutPanel2.Controls.Add(_splitAddButton);
		flowLayoutPanel2.Controls.Add(_splitRemoveButton);
		flowLayoutPanel2.Controls.Add(_splitClearButton);
		flowLayoutPanel2.Controls.Add(_splitResetButton);
		flowLayoutPanel2.Controls.Add(_splitCountLabel);
		panel2.Controls.Add(flowLayoutPanel2);
		tableLayoutPanel2.Controls.Add(panel2, 0, 0);
		Panel panel3 = MakeListHost();
		_splitVideoList = MakeVideoList();
		_splitAdjustmentEditor = MakeVideoAdjustmentEditor();
		InstallVideoListArea(panel3, _splitVideoList, _splitAdjustmentEditor);
		tableLayoutPanel2.Controls.Add(panel3, 0, 1);
		Panel panel4 = new Panel();
		panel4.Dock = DockStyle.Fill;
		panel4.Margin = new Padding(0);
		panel4.BackColor = Color.White;
		panel4.Size = new Size(base.ClientSize.Width, 334);
		Panel panel5 = panel4;
		panel5.Controls.Add(MakeLabel("拆分方式", 20, 20));
		_splitMode = new ComboBox
		{
			Location = new Point(98, 16),
			Width = 150,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitMode.Items.AddRange(new object[2] { "按固定秒数", "按平均等份" });
		_splitMode.SelectedIndex = 0;
		panel5.Controls.Add(_splitMode);
		_splitSecondsLabel = MakeLabel("每段秒数", 278, 20);
		panel5.Controls.Add(_splitSecondsLabel);
		_splitSeconds = new NumericUpDown
		{
			Location = new Point(354, 16),
			Width = 100,
			DecimalPlaces = 1,
			Increment = 0.5m,
			Minimum = 0.5m,
			Maximum = 86400m,
			Value = 60m
		};
		panel5.Controls.Add(_splitSeconds);
		_splitTailLabel = MakeLabel("尾段处理", 486, 20);
		panel5.Controls.Add(_splitTailLabel);
		_splitTailMode = new ComboBox
		{
			Location = new Point(562, 16),
			Width = 204,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitTailMode.Items.AddRange(new object[2] { "不足时长并入上一段", "不足时长直接舍弃" });
		_splitTailMode.SelectedIndex = 0;
		panel5.Controls.Add(_splitTailMode);
		_splitPartsLabel = MakeLabel("平均份数", 278, 20);
		panel5.Controls.Add(_splitPartsLabel);
		_splitEqualParts = new NumericUpDown
		{
			Location = new Point(354, 16),
			Width = 100,
			Minimum = 2m,
			Maximum = 100m,
			Value = 3m
		};
		panel5.Controls.Add(_splitEqualParts);
		_splitModeHint = MakeLabel("尾部不足设定秒数时并入上一段，不会生成短尾片段。", 98, 54);
		_splitModeHint.ForeColor = Color.FromArgb(105, 114, 128);
		panel5.Controls.Add(_splitModeHint);
		_splitOutputFrameControls = InstallOutputFrameControls(panel5, 20, 78);
		_splitBgm = InstallBgmControls(panel5, 20, 116);
		panel5.Controls.Add(MakeLabel("总输出目录", 20, 158));
		_splitOutputFolder = new TextBox
		{
			Location = new Point(98, 154),
			Width = 720,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		panel5.Controls.Add(_splitOutputFolder);
		_splitBrowseOutputButton = MakeButton("选择…", 72);
		_splitBrowseOutputButton.Location = new Point(830, 151);
		_splitBrowseOutputButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		panel5.Controls.Add(_splitBrowseOutputButton);
		_splitOpenOutputButton = MakeButton("打开", 64);
		_splitOpenOutputButton.Location = new Point(910, 151);
		_splitOpenOutputButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		panel5.Controls.Add(_splitOpenOutputButton);
		_splitProgressBar = new ProgressBar
		{
			Location = new Point(20, 196),
			Height = 18,
			Width = 954,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		panel5.Controls.Add(_splitProgressBar);
		_splitStatusLabel = new Label
		{
			Location = new Point(20, 221),
			Size = new Size(954, 24),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			ForeColor = Color.FromArgb(77, 87, 101),
			AutoEllipsis = true,
			Text = "就绪：每个视频会按所选规则分别拆分"
		};
		panel5.Controls.Add(_splitStatusLabel);
		_splitStartButton = MakePrimaryButton("开始批量拆分", 20, 263, 150);
		panel5.Controls.Add(_splitStartButton);
		_splitCancelButton = MakeButton("取消", 90);
		_splitCancelButton.Location = new Point(180, 263);
		_splitCancelButton.Height = 42;
		_splitCancelButton.Enabled = false;
		panel5.Controls.Add(_splitCancelButton);
		Label label = MakeLabel("每批自动创建“日期_视频拆分_编号”文件夹；可在“水印处理”中勾选水印。", 292, 277);
		label.ForeColor = Color.FromArgb(110, 119, 132);
		panel5.Controls.Add(label);
		tableLayoutPanel2.Controls.Add(panel5, 0, 2);
		UpdateSplitModeUi();
		return tabPage;
	}

	private TabPage BuildWatermarkTab()
	{
		TabPage tabPage = new TabPage("水印处理");
		tabPage.BackColor = Color.FromArgb(245, 247, 250);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 4;
		tableLayoutPanel.Margin = new Padding(0);
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 150f));
		tabPage.Controls.Add(tableLayoutPanel2);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.Padding = new Padding(12, 4, 8, 3);
		flowLayoutPanel.BackColor = Color.White;
		flowLayoutPanel.WrapContents = false;
		flowLayoutPanel.AutoScroll = true;
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		Label label = new Label();
		label.AutoSize = true;
		label.Text = "水印用于：";
		label.Margin = new Padding(0, 12, 8, 0);
		label.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		Label value = label;
		_watermarkOnMerge = new CheckBox
		{
			AutoSize = true,
			Text = "合并输出",
			Margin = new Padding(0, 11, 12, 0)
		};
		_watermarkOnSplit = new CheckBox
		{
			AutoSize = true,
			Text = "拆分输出",
			Margin = new Padding(0, 11, 12, 0)
		};
		_watermarkOnSplitScreen = new CheckBox
		{
			AutoSize = true,
			Text = "视频拼屏",
			Margin = new Padding(0, 11, 12, 0)
		};
		Label label2 = new Label();
		label2.AutoSize = true;
		label2.Text = "停留动画：";
		label2.ForeColor = Color.FromArgb(34, 92, 190);
		label2.Margin = new Padding(0, 12, 6, 0);
		Label value2 = label2;
		_watermarkStayEffectPool = new CheckedListBox
		{
			Width = 278,
			Height = 60,
			CheckOnClick = true,
			MultiColumn = true,
			ColumnWidth = 90,
			IntegralHeight = false,
			Margin = new Padding(0, 0, 6, 0)
		};
		_watermarkStayEffectPool.Items.AddRange(new object[9] { "轻微漂浮", "呼吸缩放", "轻柔摇摆", "持续旋转", "律动弹跳", "轻微抖动", "闪烁", "金色斜向扫光", "星光粒子" });
		_watermarkStaySelectAllButton = MakeButton("全选", 52);
		_watermarkStaySelectAllButton.Height = 28;
		_watermarkStaySelectAllButton.Margin = new Padding(0, 8, 6, 0);
		_watermarkStayRandomButton = MakeButton("全层随机动画", 106);
		_watermarkStayRandomButton.Height = 28;
		_watermarkStayRandomButton.Margin = new Padding(0, 8, 6, 0);
		_watermarkStayClearButton = MakeButton("清除效果", 82);
		_watermarkStayClearButton.Height = 28;
		_watermarkStayClearButton.Margin = new Padding(0, 8, 6, 0);
		_watermarkResetButton = MakeButton("重置本页参数", 112);
		_watermarkResetButton.Height = 28;
		_watermarkResetButton.Margin = new Padding(10, 8, 0, 0);
		flowLayoutPanel2.Controls.Add(value);
		flowLayoutPanel2.Controls.Add(_watermarkOnMerge);
		flowLayoutPanel2.Controls.Add(_watermarkOnSplit);
		flowLayoutPanel2.Controls.Add(_watermarkOnSplitScreen);
		flowLayoutPanel2.Controls.Add(value2);
		flowLayoutPanel2.Controls.Add(_watermarkStayRandomButton);
		flowLayoutPanel2.Controls.Add(_watermarkStayClearButton);
		flowLayoutPanel2.Controls.Add(_watermarkResetButton);
		tableLayoutPanel2.Controls.Add(flowLayoutPanel2, 0, 0);
		_watermarkLayerTabs = new TabControl
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(8, 3, 8, 3)
		};
		for (int i = 0; i < 3; i++)
		{
			_watermarkLayerTabs.TabPages.Add(BuildTextWatermarkLayer(i));
		}
		for (int j = 0; j < 3; j++)
		{
			_watermarkLayerTabs.TabPages.Add(BuildImageWatermarkLayer(j));
		}
		tableLayoutPanel2.Controls.Add(_watermarkLayerTabs, 0, 1);
		_watermarkSourceTabs = new TabControl
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(8, 0, 8, 0),
			Padding = new Point(16, 4)
		};
		TabPage tabPage2 = new TabPage("原视频加水印");
		tabPage2.BackColor = Color.FromArgb(245, 247, 250);
		TabPage tabPage3 = tabPage2;
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
		tableLayoutPanel3.Dock = DockStyle.Fill;
		tableLayoutPanel3.ColumnCount = 1;
		tableLayoutPanel3.RowCount = 2;
		tableLayoutPanel3.Margin = new Padding(0);
		tableLayoutPanel3.BackColor = Color.FromArgb(245, 247, 250);
		TableLayoutPanel tableLayoutPanel4 = tableLayoutPanel3;
		tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Absolute, 43f));
		tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		FlowLayoutPanel flowLayoutPanel3 = new FlowLayoutPanel();
		flowLayoutPanel3.Dock = DockStyle.Fill;
		flowLayoutPanel3.Padding = new Padding(12, 7, 12, 3);
		flowLayoutPanel3.WrapContents = false;
		flowLayoutPanel3.BackColor = Color.White;
		FlowLayoutPanel flowLayoutPanel4 = flowLayoutPanel3;
		Label label3 = new Label();
		label3.AutoSize = true;
		label3.Text = "直接加水印：";
		label3.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		label3.Margin = new Padding(0, 7, 10, 0);
		Label value3 = label3;
		_watermarkVideoAddButton = MakeButton("＋ 添加原视频", 116);
		_watermarkVideoRemoveButton = MakeButton("移除", 66);
		_watermarkVideoClearButton = MakeButton("清空", 62);
		_watermarkVideoCountLabel = MakeCountLabel();
		flowLayoutPanel4.Controls.Add(value3);
		flowLayoutPanel4.Controls.Add(_watermarkVideoAddButton);
		flowLayoutPanel4.Controls.Add(_watermarkVideoRemoveButton);
		flowLayoutPanel4.Controls.Add(_watermarkVideoClearButton);
		flowLayoutPanel4.Controls.Add(_watermarkVideoCountLabel);
		tableLayoutPanel4.Controls.Add(flowLayoutPanel4, 0, 0);
		Panel panel = MakeListHost();
		panel.Padding = new Padding(8, 4, 8, 5);
		_watermarkVideoList = MakeVideoList();
		_watermarkAdjustmentEditor = MakeVideoAdjustmentEditor();
		InstallVideoListArea(panel, _watermarkVideoList, _watermarkAdjustmentEditor);
		tableLayoutPanel4.Controls.Add(panel, 0, 1);
		tabPage3.Controls.Add(tableLayoutPanel4);
		_watermarkSourceTabs.TabPages.Add(tabPage3);
		TabPage tabPage4 = new TabPage("原图片加水印");
		tabPage4.BackColor = Color.FromArgb(245, 247, 250);
		TabPage tabPage5 = tabPage4;
		TableLayoutPanel tableLayoutPanel5 = new TableLayoutPanel();
		tableLayoutPanel5.Dock = DockStyle.Fill;
		tableLayoutPanel5.ColumnCount = 1;
		tableLayoutPanel5.RowCount = 2;
		tableLayoutPanel5.Margin = new Padding(0);
		TableLayoutPanel tableLayoutPanel6 = tableLayoutPanel5;
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Absolute, 43f));
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		FlowLayoutPanel flowLayoutPanel5 = new FlowLayoutPanel();
		flowLayoutPanel5.Dock = DockStyle.Fill;
		flowLayoutPanel5.Padding = new Padding(12, 7, 12, 3);
		flowLayoutPanel5.WrapContents = false;
		flowLayoutPanel5.BackColor = Color.White;
		FlowLayoutPanel flowLayoutPanel6 = flowLayoutPanel5;
		Label label4 = new Label();
		label4.AutoSize = true;
		label4.Text = "图片批量加水印：";
		label4.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		label4.Margin = new Padding(0, 7, 10, 0);
		Label value4 = label4;
		_watermarkSourceImageAddButton = MakeButton("＋ 添加原图片", 116);
		_watermarkSourceImageRemoveButton = MakeButton("移除", 66);
		_watermarkSourceImageClearButton = MakeButton("清空", 62);
		_watermarkSourceImageCountLabel = MakeCountLabel();
		flowLayoutPanel6.Controls.Add(value4);
		flowLayoutPanel6.Controls.Add(_watermarkSourceImageAddButton);
		flowLayoutPanel6.Controls.Add(_watermarkSourceImageRemoveButton);
		flowLayoutPanel6.Controls.Add(_watermarkSourceImageClearButton);
		flowLayoutPanel6.Controls.Add(_watermarkSourceImageCountLabel);
		flowLayoutPanel6.Controls.Add(new Label
		{
			AutoSize = true,
			Text = "输出 PNG；保留原尺寸和透明通道",
			ForeColor = MutedColor,
			Margin = new Padding(18, 8, 0, 0)
		});
		tableLayoutPanel6.Controls.Add(flowLayoutPanel6, 0, 0);
		_watermarkSourceImageList = new ListBox
		{
			Dock = DockStyle.Fill,
			AllowDrop = true,
			HorizontalScrollbar = true,
			Font = new Font("Microsoft YaHei UI", 9f),
			ItemHeight = 24,
			Margin = new Padding(8, 3, 8, 6),
			SelectionMode = SelectionMode.MultiExtended
		};
		tableLayoutPanel6.Controls.Add(_watermarkSourceImageList, 0, 1);
		tabPage5.Controls.Add(tableLayoutPanel6);
		_watermarkSourceTabs.TabPages.Add(tabPage5);
		tableLayoutPanel2.Controls.Add(_watermarkSourceTabs, 0, 2);
		Panel output = new Panel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(0),
			BackColor = Color.White
		};
		_watermarkOutputFrameControls = InstallOutputFrameControls(output, 18, 2);
		output.Controls.Add(MakeLabel("总输出目录", 18, 35));
		_watermarkOutputFolder = new TextBox
		{
			Location = new Point(96, 31),
			Width = 720
		};
		output.Controls.Add(_watermarkOutputFolder);
		_watermarkOutputBrowseButton = MakeButton("选择…", 72);
		_watermarkOutputBrowseButton.Location = new Point(828, 28);
		_watermarkOutputBrowseButton.Height = 26;
		output.Controls.Add(_watermarkOutputBrowseButton);
		_watermarkOutputOpenButton = MakeButton("打开", 64);
		_watermarkOutputOpenButton.Location = new Point(908, 28);
		_watermarkOutputOpenButton.Height = 26;
		output.Controls.Add(_watermarkOutputOpenButton);
		_watermarkBgm = InstallBgmControls(output, 18, 57);
		_watermarkBgm.Add.Height = 26;
		_watermarkBgm.Clear.Height = 26;
		_watermarkProgressBar = new ProgressBar
		{
			Location = new Point(18, 86),
			Width = 954,
			Height = 12
		};
		output.Controls.Add(_watermarkProgressBar);
		_watermarkStatusLabel = new Label
		{
			Location = new Point(18, 101),
			Width = 954,
			Height = 18,
			ForeColor = Color.FromArgb(77, 87, 101),
			AutoEllipsis = true,
			Text = "启用至少一个水印图层，然后添加原视频即可直接导出"
		};
		output.Controls.Add(_watermarkStatusLabel);
		_watermarkStartButton = MakePrimaryButton("直接加水印导出", 18, 116, 165);
		_watermarkStartButton.Height = 31;
		output.Controls.Add(_watermarkStartButton);
		_watermarkCancelButton = MakeButton("取消", 88);
		_watermarkCancelButton.Location = new Point(194, 116);
		_watermarkCancelButton.Height = 31;
		_watermarkCancelButton.Enabled = false;
		output.Controls.Add(_watermarkCancelButton);
		_watermarkHint = MakeLabel("透明 PNG 会保留透明通道；每批独立文件夹只保存成品。", 298, 122);
		_watermarkHint.ForeColor = Color.FromArgb(105, 114, 128);
		_watermarkHint.AutoEllipsis = true;
		_watermarkHint.Width = 674;
		output.Controls.Add(_watermarkHint);
		output.Resize += delegate
		{
			LayoutWatermarkOutputPanel(output);
		};
		tableLayoutPanel2.Controls.Add(output, 0, 3);
		LayoutWatermarkOutputPanel(output);
		return tabPage;
	}

	private void LayoutWatermarkOutputPanel(Panel output)
	{
		if (output != null && _watermarkOutputFolder != null)
		{
			int num = Math.Max(360, output.ClientSize.Width - 18);
			_watermarkOutputOpenButton.Left = Math.Max(288, num - _watermarkOutputOpenButton.Width);
			_watermarkOutputBrowseButton.Left = Math.Max(208, _watermarkOutputOpenButton.Left - 8 - _watermarkOutputBrowseButton.Width);
			_watermarkOutputFolder.Width = Math.Max(100, _watermarkOutputBrowseButton.Left - 12 - _watermarkOutputFolder.Left);
			_watermarkProgressBar.Width = Math.Max(100, num - _watermarkProgressBar.Left);
			_watermarkStatusLabel.Width = Math.Max(100, num - _watermarkStatusLabel.Left);
			_watermarkHint.Width = Math.Max(100, num - _watermarkHint.Left);
		}
	}

	private TabPage BuildImageVideoTab()
	{
		TabPage tabPage = new TabPage("图片成片");
		tabPage.BackColor = Color.FromArgb(245, 247, 250);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 3;
		tableLayoutPanel.Margin = new Padding(0);
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 550f));
		tabPage.Controls.Add(tableLayoutPanel2);
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.Margin = new Padding(0);
		panel.Padding = new Padding(12, 10, 12, 6);
		panel.BackColor = Color.White;
		Panel panel2 = panel;
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.WrapContents = false;
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		_imageAddButton = MakeButton("＋ 添加图片", 106);
		_imageRemoveButton = MakeButton("移除", 66);
		_imageUpButton = MakeButton("上移", 62);
		_imageDownButton = MakeButton("下移", 62);
		_imageShuffleButton = MakeButton("随机打乱", 88);
		_imageClearButton = MakeButton("清空", 62);
		_imageResetButton = MakeButton("重置本页参数", 112);
		_imageCountLabel = MakeCountLabel();
		flowLayoutPanel2.Controls.Add(_imageAddButton);
		flowLayoutPanel2.Controls.Add(_imageRemoveButton);
		flowLayoutPanel2.Controls.Add(_imageUpButton);
		flowLayoutPanel2.Controls.Add(_imageDownButton);
		flowLayoutPanel2.Controls.Add(_imageShuffleButton);
		flowLayoutPanel2.Controls.Add(_imageClearButton);
		flowLayoutPanel2.Controls.Add(_imageResetButton);
		flowLayoutPanel2.Controls.Add(_imageCountLabel);
		panel2.Controls.Add(flowLayoutPanel2);
		tableLayoutPanel2.Controls.Add(panel2, 0, 0);
		Panel panel3 = new Panel();
		panel3.Dock = DockStyle.Fill;
		panel3.Margin = new Padding(0);
		panel3.Padding = new Padding(12, 8, 12, 10);
		panel3.BackColor = Color.FromArgb(245, 247, 250);
		Panel panel4 = panel3;
		_slideshowImageList = new ListBox
		{
			Dock = DockStyle.Fill,
			AllowDrop = true,
			HorizontalScrollbar = true,
			Font = new Font("Microsoft YaHei UI", 10f),
			ItemHeight = 27
		};
		panel4.Controls.Add(_slideshowImageList);
		tableLayoutPanel2.Controls.Add(panel4, 0, 1);
		Panel panel5 = new Panel();
		panel5.Dock = DockStyle.Fill;
		panel5.Margin = new Padding(0);
		panel5.BackColor = Color.White;
		panel5.Size = new Size(base.ClientSize.Width, 550);
		Panel panel6 = panel5;
		panel6.Controls.Add(MakeLabel("每页基础时长", 20, 20));
		_imageDuration = new NumericUpDown
		{
			Location = new Point(112, 16),
			Width = 72,
			Minimum = 0.5m,
			Maximum = 120m,
			Value = 3m,
			DecimalPlaces = 1,
			Increment = 0.5m
		};
		panel6.Controls.Add(_imageDuration);
		panel6.Controls.Add(MakeLabel("秒", 190, 20));
		panel6.Controls.Add(MakeLabel("输出数量", 226, 20));
		_imageOutputCount = new NumericUpDown
		{
			Location = new Point(294, 16),
			Width = 64,
			Minimum = 1m,
			Maximum = 1000m,
			Value = 1m
		};
		panel6.Controls.Add(_imageOutputCount);
		panel6.Controls.Add(MakeLabel("个", 363, 20));
		panel6.Controls.Add(MakeLabel("每个成品", 398, 20));
		_imageTargetDuration = new NumericUpDown
		{
			Location = new Point(468, 16),
			Width = 72,
			Minimum = 1m,
			Maximum = 86400m,
			Value = 30m,
			DecimalPlaces = 1,
			Increment = 1m
		};
		panel6.Controls.Add(_imageTargetDuration);
		panel6.Controls.Add(MakeLabel("秒", 546, 20));
		panel6.Controls.Add(MakeLabel("画面尺寸", 584, 20));
		_imageResolution = new ComboBox
		{
			Location = new Point(654, 16),
			Width = 178,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_imageResolution.Items.AddRange(new object[4] { "1920×1080（横屏）", "1080×1920（竖屏）", "1080×1080（方形）", "1280×720（横屏）" });
		_imageResolution.SelectedIndex = 0;
		panel6.Controls.Add(_imageResolution);
		_imageSmartEnhance = new CheckBox
		{
			AutoSize = true,
			Text = "智能校色＋轻柔美化",
			Location = new Point(850, 18),
			Checked = false
		};
		panel6.Controls.Add(_imageSmartEnhance);
		_imageLayoutPreset = new ComboBox
		{
			Location = new Point(-2000, -2000),
			Width = 224,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Visible = false
		};
		_imageLayoutPreset.Items.AddRange(new object[8] { "单图轮播（原模式）", "3 张·杂志三联拼图", "4 张·主图焦点拼图", "5 张·一大四小拼图", "6 张·经典六宫格", "7 张·主图加六图", "8 张·不规则画廊拼图", "9 张·经典九宫格" });
		_imageLayoutPreset.SelectedIndex = 0;
		panel6.Controls.Add(_imageLayoutPreset);
		_imageTileAnimation = new ComboBox
		{
			Location = new Point(-2000, -2000),
			Width = 152,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Visible = false
		};
		_imageTileAnimation.Items.AddRange(new object[6] { "直接显示", "交错淡入淡出", "从左滑入后淡出", "上浮进入后淡出", "向右滑出", "向下沉出" });
		_imageTileAnimation.SelectedIndex = 1;
		panel6.Controls.Add(_imageTileAnimation);
		panel6.Controls.Add(MakeLabel("每组排版（可多选）", 20, 60));
		_imageLayoutPool = new CheckedListBox
		{
			Location = new Point(144, 50),
			Size = new Size(474, 102),
			CheckOnClick = true,
			MultiColumn = true,
			ColumnWidth = 232,
			IntegralHeight = false
		};
		_imageLayoutPool.Items.AddRange(((IEnumerable<ImageLayoutChoice>)CreateImageLayoutCatalog()).Select((Func<ImageLayoutChoice, object>)((ImageLayoutChoice x) => x.DisplayName)).ToArray());
		_imageLayoutPool.SetItemChecked(0, value: true);
		panel6.Controls.Add(_imageLayoutPool);
		_imageLayoutSelectAllButton = MakeButton("全选", 58);
		_imageLayoutSelectAllButton.Location = new Point(626, 51);
		panel6.Controls.Add(_imageLayoutSelectAllButton);
		_imageLayoutClearButton = MakeButton("清空", 58);
		_imageLayoutClearButton.Location = new Point(626, 89);
		panel6.Controls.Add(_imageLayoutClearButton);
		_imageLayoutOrder = new ComboBox
		{
			Location = new Point(694, 51),
			Width = 154,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_imageLayoutOrder.Items.AddRange(new object[2] { "每组随机选择", "按勾选顺序循环" });
		_imageLayoutOrder.SelectedIndex = 0;
		panel6.Controls.Add(_imageLayoutOrder);
		Label label = MakeLabel("一个成品内可先四宫格、再九宫格；每一组单独选排版。", 694, 91);
		label.Size = new Size(340, 48);
		label.ForeColor = MutedColor;
		panel6.Controls.Add(label);
		panel6.Controls.Add(MakeLabel("展示律动（可多选）", 20, 174));
		_imageMotionPool = new CheckedListBox
		{
			Location = new Point(144, 164),
			Size = new Size(474, 102),
			CheckOnClick = true,
			MultiColumn = true,
			ColumnWidth = 155,
			IntegralHeight = false
		};
		_imageMotionPool.Items.AddRange(new object[9] { "轻微放大", "呼吸缩放", "轻柔漂移", "微微律动", "交错淡入淡出", "从左滑入后淡出", "上浮进入后淡出", "向右滑出", "向下沉出" });
		_imageMotionPool.SetItemChecked(0, value: true);
		panel6.Controls.Add(_imageMotionPool);
		_imageMotionSelectAllButton = MakeButton("全选", 58);
		_imageMotionSelectAllButton.Location = new Point(626, 165);
		panel6.Controls.Add(_imageMotionSelectAllButton);
		_imageMotionClearButton = MakeButton("清空", 58);
		_imageMotionClearButton.Location = new Point(626, 203);
		panel6.Controls.Add(_imageMotionClearButton);
		_imageMotionOrder = new ComboBox
		{
			Location = new Point(694, 165),
			Width = 154,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_imageMotionOrder.Items.AddRange(new object[2] { "每组随机选择", "按勾选顺序循环" });
		_imageMotionOrder.SelectedIndex = 0;
		panel6.Controls.Add(_imageMotionOrder);
		panel6.Controls.Add(MakeLabel("效果时长", 694, 207));
		_imageTileAnimationDuration = new NumericUpDown
		{
			Location = new Point(760, 202),
			Width = 64,
			Minimum = 0.1m,
			Maximum = 3m,
			Value = 0.6m,
			DecimalPlaces = 1,
			Increment = 0.1m
		};
		panel6.Controls.Add(_imageTileAnimationDuration);
		panel6.Controls.Add(MakeLabel("秒", 829, 207));
		panel6.Controls.Add(MakeLabel("组间转场（可多选）", 20, 288));
		_imageTransitions = new CheckedListBox
		{
			Location = new Point(144, 278),
			Size = new Size(474, 92),
			CheckOnClick = true,
			MultiColumn = true,
			ColumnWidth = 150,
			IntegralHeight = false
		};
		_imageTransitions.Items.AddRange(((IEnumerable<TransitionSpec>)CreateTransitionCatalog(1.0)).Select((Func<TransitionSpec, object>)((TransitionSpec x) => x.DisplayName)).ToArray());
		panel6.Controls.Add(_imageTransitions);
		_imageTransitionSelectAllButton = MakeButton("全选", 58);
		_imageTransitionSelectAllButton.Location = new Point(626, 279);
		panel6.Controls.Add(_imageTransitionSelectAllButton);
		_imageTransitionClearButton = MakeButton("清空", 58);
		_imageTransitionClearButton.Location = new Point(626, 317);
		panel6.Controls.Add(_imageTransitionClearButton);
		panel6.Controls.Add(MakeLabel("使用方式", 694, 285));
		_imageTransitionOrder = new ComboBox
		{
			Location = new Point(764, 280),
			Width = 142,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_imageTransitionOrder.Items.AddRange(new object[2] { "按列表顺序循环", "每次随机选择" });
		_imageTransitionOrder.SelectedIndex = 0;
		panel6.Controls.Add(_imageTransitionOrder);
		panel6.Controls.Add(MakeLabel("转场时间", 694, 325));
		_imageTransitionDuration = new NumericUpDown
		{
			Location = new Point(764, 320),
			Width = 76,
			Minimum = 0.1m,
			Maximum = 10m,
			Value = 1m,
			DecimalPlaces = 1,
			Increment = 0.1m
		};
		panel6.Controls.Add(_imageTransitionDuration);
		panel6.Controls.Add(MakeLabel("秒", 846, 325));
		_imageBgm = InstallBgmControls(panel6, 20, 378);
		panel6.Controls.Add(MakeLabel("总输出目录", 20, 420));
		_imageOutputFolder = new TextBox
		{
			Location = new Point(110, 416),
			Width = 708,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		panel6.Controls.Add(_imageOutputFolder);
		_imageOutputBrowseButton = MakeButton("选择…", 72);
		_imageOutputBrowseButton.Location = new Point(830, 413);
		_imageOutputBrowseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		panel6.Controls.Add(_imageOutputBrowseButton);
		_imageOutputOpenButton = MakeButton("打开", 64);
		_imageOutputOpenButton.Location = new Point(910, 413);
		_imageOutputOpenButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		panel6.Controls.Add(_imageOutputOpenButton);
		_imageProgressBar = new ProgressBar
		{
			Location = new Point(20, 450),
			Height = 16,
			Width = 954,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		panel6.Controls.Add(_imageProgressBar);
		_imageStatusLabel = new Label
		{
			Location = new Point(20, 469),
			Size = new Size(954, 22),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			ForeColor = Color.FromArgb(77, 87, 101),
			AutoEllipsis = true,
			Text = "可生成多个统一时长的成品；每组独立随机排版并配合轻柔律动。"
		};
		panel6.Controls.Add(_imageStatusLabel);
		_imageStartButton = MakePrimaryButton("开始图片成片", 20, 500, 150);
		panel6.Controls.Add(_imageStartButton);
		_imageCancelButton = MakeButton("取消", 90);
		_imageCancelButton.Location = new Point(180, 500);
		_imageCancelButton.Height = 42;
		_imageCancelButton.Enabled = false;
		panel6.Controls.Add(_imageCancelButton);
		Label label2 = MakeLabel("每批创建独立“日期_图片成片_编号”文件夹；原图片不会被修改。", 292, 514);
		label2.ForeColor = MutedColor;
		panel6.Controls.Add(label2);
		tableLayoutPanel2.Controls.Add(panel6, 0, 2);
		return tabPage;
	}

	private void InitializeSplitScreenLayouts()
	{
		_splitScreenLayouts.Clear();
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "top_half",
			DisplayName = "上 1 / 下 1",
			Category = "常用分屏",
			Regions = new RectangleF[2]
			{
				new RectangleF(0f, 0f, 1f, 0.5f),
				new RectangleF(0f, 0.5f, 1f, 0.5f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "top_third",
			DisplayName = "上 1/3 / 下 2/3",
			Category = "常用分屏",
			Regions = new RectangleF[2]
			{
				new RectangleF(0f, 0f, 1f, 1f / 3f),
				new RectangleF(0f, 1f / 3f, 1f, 2f / 3f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "bottom_third",
			DisplayName = "上 2/3 / 下 1/3",
			Category = "常用分屏",
			Regions = new RectangleF[2]
			{
				new RectangleF(0f, 0f, 1f, 2f / 3f),
				new RectangleF(0f, 2f / 3f, 1f, 1f / 3f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "three_rows",
			DisplayName = "纵向三等分",
			Category = "常用分屏",
			Regions = new RectangleF[3]
			{
				new RectangleF(0f, 0f, 1f, 1f / 3f),
				new RectangleF(0f, 1f / 3f, 1f, 1f / 3f),
				new RectangleF(0f, 2f / 3f, 1f, 1f / 3f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "left_right",
			DisplayName = "左右二分",
			Category = "常用分屏",
			Regions = new RectangleF[2]
			{
				new RectangleF(0f, 0f, 0.5f, 1f),
				new RectangleF(0.5f, 0f, 0.5f, 1f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "top1_bottom2",
			DisplayName = "上 1 / 下 2",
			Category = "多格拼屏",
			Regions = new RectangleF[3]
			{
				new RectangleF(0f, 0f, 1f, 0.5f),
				new RectangleF(0f, 0.5f, 0.5f, 0.5f),
				new RectangleF(0.5f, 0.5f, 0.5f, 0.5f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "top1_bottom3",
			DisplayName = "上 1 / 下 3",
			Category = "多格拼屏",
			Regions = new RectangleF[4]
			{
				new RectangleF(0f, 0f, 1f, 0.5f),
				new RectangleF(0f, 0.5f, 1f / 3f, 0.5f),
				new RectangleF(1f / 3f, 0.5f, 1f / 3f, 0.5f),
				new RectangleF(2f / 3f, 0.5f, 1f / 3f, 0.5f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "top1_bottom4",
			DisplayName = "上 1 / 下 4",
			Category = "多格拼屏",
			Regions = new RectangleF[5]
			{
				new RectangleF(0f, 0f, 1f, 0.5f),
				new RectangleF(0f, 0.5f, 0.5f, 0.25f),
				new RectangleF(0.5f, 0.5f, 0.5f, 0.25f),
				new RectangleF(0f, 0.75f, 0.5f, 0.25f),
				new RectangleF(0.5f, 0.75f, 0.5f, 0.25f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "grid4",
			DisplayName = "四宫格",
			Category = "多格拼屏",
			Regions = new RectangleF[4]
			{
				new RectangleF(0f, 0f, 0.5f, 0.5f),
				new RectangleF(0.5f, 0f, 0.5f, 0.5f),
				new RectangleF(0f, 0.5f, 0.5f, 0.5f),
				new RectangleF(0.5f, 0.5f, 0.5f, 0.5f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "grid6",
			DisplayName = "六宫格",
			Category = "多格拼屏",
			Regions = new RectangleF[6]
			{
				new RectangleF(0f, 0f, 0.5f, 1f / 3f),
				new RectangleF(0.5f, 0f, 0.5f, 1f / 3f),
				new RectangleF(0f, 1f / 3f, 0.5f, 1f / 3f),
				new RectangleF(0.5f, 1f / 3f, 0.5f, 1f / 3f),
				new RectangleF(0f, 2f / 3f, 0.5f, 1f / 3f),
				new RectangleF(0.5f, 2f / 3f, 0.5f, 1f / 3f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "grid9",
			DisplayName = "九宫格",
			Category = "多格拼屏",
			Regions = (from i in Enumerable.Range(0, 9)
				select new RectangleF((float)(i % 3) / 3f, (float)(i / 3) / 3f, 1f / 3f, 1f / 3f)).ToArray()
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "pip",
			DisplayName = "全屏背景＋小窗",
			Category = "画中画",
			PictureInPicture = true,
			Regions = new RectangleF[2]
			{
				new RectangleF(0f, 0f, 1f, 1f),
				new RectangleF(0.64f, 0.64f, 0.3f, 0.3f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "pip_double",
			DisplayName = "全屏背景＋双小窗",
			Category = "画中画",
			PictureInPicture = true,
			Regions = new RectangleF[3]
			{
				new RectangleF(0f, 0f, 1f, 1f),
				new RectangleF(0.05f, 0.66f, 0.27f, 0.27f),
				new RectangleF(0.68f, 0.66f, 0.27f, 0.27f)
			}
		});
		_splitScreenLayouts.Add(new SplitScreenLayoutDefinition
		{
			Id = "pip_triple",
			DisplayName = "全屏背景＋三小窗",
			Category = "画中画",
			PictureInPicture = true,
			Regions = new RectangleF[4]
			{
				new RectangleF(0f, 0f, 1f, 1f),
				new RectangleF(0.04f, 0.7f, 0.27f, 0.25f),
				new RectangleF(0.365f, 0.7f, 0.27f, 0.25f),
				new RectangleF(0.69f, 0.7f, 0.27f, 0.25f)
			}
		});
		_activeSplitScreenLayout = _splitScreenLayouts.FirstOrDefault();
		foreach (SplitScreenLayoutDefinition splitScreenLayout in _splitScreenLayouts)
		{
			EnsureSplitScreenLayoutSettings(splitScreenLayout);
		}
	}

	private void EnsureSplitScreenLayoutSettings(SplitScreenLayoutDefinition definition)
	{
		if (definition == null)
		{
			return;
		}
		if (!_splitScreenRegionSettingsByLayout.TryGetValue(definition.Id, out var value))
		{
			value = new SplitScreenRegionSettings[9];
			for (int i = 0; i < value.Length; i++)
			{
				value[i] = new SplitScreenRegionSettings();
			}
			if (definition.PictureInPicture)
			{
				string[] array = ((definition.RegionCount == 2) ? new string[1] { "右下" } : ((definition.RegionCount == 3) ? new string[2] { "左下", "右下" } : new string[3] { "左下", "底部居中", "右下" }));
				for (int j = 1; j < definition.RegionCount; j++)
				{
					value[j].PipPosition = array[Math.Min(j - 1, array.Length - 1)];
					value[j].PipSizePercent = Math.Max(12, Math.Min(65, (int)Math.Round(definition.Regions[j].Width * 100f)));
				}
			}
			_splitScreenRegionSettingsByLayout[definition.Id] = value;
		}
		if (!_splitScreenEnabledRegionsByLayout.TryGetValue(definition.Id, out var value2))
		{
			value2 = new bool[9];
			for (int k = 0; k < definition.RegionCount; k++)
			{
				value2[k] = true;
			}
			_splitScreenEnabledRegionsByLayout[definition.Id] = value2;
		}
		if (!_splitScreenBorderRegionsByLayout.TryGetValue(definition.Id, out var value3))
		{
			value3 = new bool[9];
			for (int l = 0; l < definition.RegionCount; l++)
			{
				value3[l] = true;
			}
			_splitScreenBorderRegionsByLayout[definition.Id] = value3;
		}
		if (_splitScreenMainRegionByLayout.ContainsKey(definition.Id))
		{
			return;
		}
		int value4 = 0;
		float num = -1f;
		for (int m = 0; m < definition.RegionCount; m++)
		{
			float num2 = definition.Regions[m].Width * definition.Regions[m].Height;
			if (num2 > num)
			{
				num = num2;
				value4 = m;
			}
		}
		_splitScreenMainRegionByLayout[definition.Id] = value4;
	}

	private SplitScreenRegionSettings[] GetActiveSplitScreenRegionSettings()
	{
		EnsureSplitScreenLayoutSettings(_activeSplitScreenLayout);
		if (_activeSplitScreenLayout != null)
		{
			return _splitScreenRegionSettingsByLayout[_activeSplitScreenLayout.Id];
		}
		return new SplitScreenRegionSettings[9];
	}

	private bool[] GetActiveSplitScreenEnabledRegions()
	{
		EnsureSplitScreenLayoutSettings(_activeSplitScreenLayout);
		if (_activeSplitScreenLayout != null)
		{
			return _splitScreenEnabledRegionsByLayout[_activeSplitScreenLayout.Id];
		}
		return new bool[9];
	}

	private bool[] GetActiveSplitScreenBorderRegions()
	{
		EnsureSplitScreenLayoutSettings(_activeSplitScreenLayout);
		if (_activeSplitScreenLayout != null)
		{
			return _splitScreenBorderRegionsByLayout[_activeSplitScreenLayout.Id];
		}
		return new bool[9];
	}

	private int GetActiveSplitScreenMainRegion()
	{
		EnsureSplitScreenLayoutSettings(_activeSplitScreenLayout);
		if (_activeSplitScreenLayout == null || !_splitScreenMainRegionByLayout.TryGetValue(_activeSplitScreenLayout.Id, out var value))
		{
			return 0;
		}
		return value;
	}

	private string GetSplitScreenVideoViewKey(int region, string path)
	{
		return ((_activeSplitScreenLayout == null) ? "layout" : _activeSplitScreenLayout.Id) + "|" + region.ToString(CultureInfo.InvariantCulture) + "|" + (path ?? "");
	}

	private SplitScreenVideoViewSettings GetSplitScreenVideoViewSettings(int region, string path)
	{
		string splitScreenVideoViewKey = GetSplitScreenVideoViewKey(region, path);
		if (!_splitScreenVideoViewSettings.TryGetValue(splitScreenVideoViewKey, out var value))
		{
			value = new SplitScreenVideoViewSettings();
			_splitScreenVideoViewSettings[splitScreenVideoViewKey] = value;
		}
		return value;
	}

	private TabPage BuildSplitScreenTab()
	{
		TabPage tabPage = new TabPage("视频拼屏");
		tabPage.BackColor = Color.FromArgb(245, 247, 250);
		TabPage tabPage2 = tabPage;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 2;
		tableLayoutPanel.Margin = new Padding(0);
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 382f));
		tabPage2.Controls.Add(tableLayoutPanel2);
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
		tableLayoutPanel3.Dock = DockStyle.Fill;
		tableLayoutPanel3.ColumnCount = 3;
		tableLayoutPanel3.RowCount = 1;
		tableLayoutPanel3.Margin = new Padding(8, 7, 8, 4);
		tableLayoutPanel3.BackColor = Color.FromArgb(245, 247, 250);
		TableLayoutPanel tableLayoutPanel4 = tableLayoutPanel3;
		tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330f));
		tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390f));
		tableLayoutPanel2.Controls.Add(tableLayoutPanel4, 0, 0);
		GroupBox groupBox = new GroupBox();
		groupBox.Text = "① 选择拼屏模板";
		groupBox.Dock = DockStyle.Fill;
		groupBox.Margin = new Padding(0, 0, 7, 0);
		groupBox.Padding = new Padding(8);
		groupBox.BackColor = Color.White;
		GroupBox groupBox2 = groupBox;
		_splitScreenPresetTabs = new TabControl
		{
			Dock = DockStyle.Fill
		};
		_splitScreenQuickPresets = CreateSplitScreenPresetList("常用分屏");
		_splitScreenGridPresets = CreateSplitScreenPresetList("多格拼屏");
		_splitScreenPipPresets = CreateSplitScreenPresetList("画中画");
		AddSplitScreenPresetPage(_splitScreenPresetTabs, "常用", _splitScreenQuickPresets);
		AddSplitScreenPresetPage(_splitScreenPresetTabs, "多格", _splitScreenGridPresets);
		AddSplitScreenPresetPage(_splitScreenPresetTabs, "画中画", _splitScreenPipPresets);
		groupBox2.Controls.Add(_splitScreenPresetTabs);
		tableLayoutPanel4.Controls.Add(groupBox2, 0, 0);
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.Margin = new Padding(0, 0, 7, 0);
		panel.Padding = new Padding(10);
		panel.BackColor = Color.White;
		Panel panel2 = panel;
		Label label = new Label();
		label.Dock = DockStyle.Top;
		label.Height = 25;
		label.Text = "② 实时首帧预览（点区域可切换素材）";
		label.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		label.ForeColor = InkColor;
		Label value = label;
		Panel previewSurface = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(22, 28, 38)
		};
		_splitScreenPreviewHost = previewSurface;
		_splitScreenPreview = new SplitScreenPreviewBox
		{
			Dock = DockStyle.None,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left),
			BackColor = Color.FromArgb(22, 28, 38),
			SizeMode = PictureBoxSizeMode.CenterImage,
			AllowDrop = true,
			Cursor = Cursors.Hand
		};
		previewSurface.Controls.Add(_splitScreenPreview);
		Panel splitScreenPreviewBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 38,
			BackColor = Color.FromArgb(246, 248, 252),
			Padding = new Padding(4, 4, 4, 2)
		};
		_splitScreenPlayPreviewButton = MakePrimaryButton("▶ 播放拼屏预览 (带声音)", 0, 0, 185);
		_splitScreenPlayPreviewButton.Height = 30;
		_splitScreenPlayPreviewButton.Dock = DockStyle.Left;

		_splitScreenPreviewDuration = new ComboBox
		{
			Width = 105,
			Height = 28,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Dock = DockStyle.Left
		};
		_splitScreenPreviewDuration.Items.AddRange(new object[] { "5 秒预览", "8 秒预览", "10 秒预览", "完整成品" });
		_splitScreenPreviewDuration.SelectedIndex = 0;

		Label previewTip = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = Color.FromArgb(100, 110, 125),
			Font = new Font("Microsoft YaHei UI", 8.5f),
			Text = "  秒级合成当前区域视频、取景与配乐试看",
			AutoEllipsis = true
		};

		splitScreenPreviewBar.Controls.Add(previewTip);
		splitScreenPreviewBar.Controls.Add(_splitScreenPreviewDuration);
		splitScreenPreviewBar.Controls.Add(_splitScreenPlayPreviewButton);

		panel2.Controls.Add(previewSurface);
		panel2.Controls.Add(splitScreenPreviewBar);
		panel2.Controls.Add(value);
		previewSurface.SendToBack();
		previewSurface.Resize += delegate
		{
			LayoutSplitScreenPreview(previewSurface);
		};
		tableLayoutPanel4.Resize += delegate
		{
			LayoutSplitScreenPreview(previewSurface);
		};
		tabPage2.Enter += delegate
		{
			try
			{
				BeginInvoke((Action)delegate
				{
					LayoutSplitScreenPreview(previewSurface);
				});
			}
			catch
			{
			}
		};
		tableLayoutPanel4.Controls.Add(panel2, 1, 0);
		GroupBox groupBox3 = new GroupBox();
		groupBox3.Text = "③ 给编号区域添加视频";
		groupBox3.Dock = DockStyle.Fill;
		groupBox3.Margin = new Padding(0);
		groupBox3.Padding = new Padding(10);
		groupBox3.BackColor = Color.White;
		GroupBox groupBox4 = groupBox3;
		TableLayoutPanel tableLayoutPanel5 = new TableLayoutPanel();
		tableLayoutPanel5.Dock = DockStyle.Fill;
		tableLayoutPanel5.ColumnCount = 1;
		tableLayoutPanel5.RowCount = 6;
		tableLayoutPanel5.Margin = new Padding(0);
		TableLayoutPanel tableLayoutPanel6 = tableLayoutPanel5;
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Absolute, 39f));
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Absolute, 43f));
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
		tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.WrapContents = false;
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		flowLayoutPanel2.Controls.Add(new Label
		{
			AutoSize = true,
			Text = "当前区域",
			Margin = new Padding(0, 8, 8, 0),
			Font = new Font(Font, FontStyle.Bold)
		});
		_splitScreenRegionSelector = new ComboBox
		{
			Width = 138,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Margin = new Padding(0, 4, 8, 0)
		};
		_splitScreenRegionCountLabel = new Label
		{
			AutoSize = true,
			ForeColor = MutedColor,
			Margin = new Padding(4, 8, 0, 0)
		};
		flowLayoutPanel2.Controls.Add(_splitScreenRegionSelector);
		flowLayoutPanel2.Controls.Add(_splitScreenRegionCountLabel);
		tableLayoutPanel6.Controls.Add(flowLayoutPanel2, 0, 0);
		FlowLayoutPanel flowLayoutPanel3 = new FlowLayoutPanel();
		flowLayoutPanel3.Dock = DockStyle.Fill;
		flowLayoutPanel3.WrapContents = false;
		FlowLayoutPanel flowLayoutPanel4 = flowLayoutPanel3;
		_splitScreenAddButton = MakeButton("＋ 添加视频", 104);
		_splitScreenRemoveButton = MakeButton("移除", 62);
		_splitScreenClearButton = MakeButton("清空", 62);
		flowLayoutPanel4.Controls.Add(_splitScreenAddButton);
		flowLayoutPanel4.Controls.Add(_splitScreenRemoveButton);
		flowLayoutPanel4.Controls.Add(_splitScreenClearButton);
		tableLayoutPanel6.Controls.Add(flowLayoutPanel4, 0, 1);
		_splitScreenRegionList = new ListBox
		{
			Dock = DockStyle.Fill,
			SelectionMode = SelectionMode.MultiExtended,
			HorizontalScrollbar = true,
			AllowDrop = true,
			IntegralHeight = false
		};
		tableLayoutPanel6.Controls.Add(_splitScreenRegionList, 0, 2);
		FlowLayoutPanel flowLayoutPanel5 = new FlowLayoutPanel();
		flowLayoutPanel5.Dock = DockStyle.Fill;
		flowLayoutPanel5.WrapContents = false;
		FlowLayoutPanel flowLayoutPanel6 = flowLayoutPanel5;
		_splitScreenMoveUpButton = MakeButton("上移", 64);
		_splitScreenMoveDownButton = MakeButton("下移", 64);
		flowLayoutPanel6.Controls.Add(_splitScreenMoveUpButton);
		flowLayoutPanel6.Controls.Add(_splitScreenMoveDownButton);
		tableLayoutPanel6.Controls.Add(flowLayoutPanel6, 0, 3);
		Panel panel3 = new Panel();
		panel3.Dock = DockStyle.Fill;
		panel3.BackColor = Color.FromArgb(248, 250, 252);
		Panel panel4 = panel3;
		panel4.Controls.Add(new Label
		{
			AutoSize = true,
			Text = "所选视频画面",
			Location = new Point(0, 7),
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
		});
		panel4.Controls.Add(MakeLabel("缩放", 0, 37));
		_splitScreenVideoScale = new NumericUpDown
		{
			Location = new Point(42, 33),
			Width = 64,
			Minimum = 1.0m,
			Maximum = 3.0m,
			Value = 1.0m,
			DecimalPlaces = 1,
			Increment = 0.1m
		};
		panel4.Controls.Add(_splitScreenVideoScale);
		panel4.Controls.Add(MakeLabel("倍", 111, 37));
		panel4.Controls.Add(MakeLabel("水平取景", 145, 37));
		_splitScreenVideoCropX = new NumericUpDown
		{
			Location = new Point(210, 33),
			Width = 58,
			Minimum = -100m,
			Maximum = 100m,
			Value = 0m
		};
		panel4.Controls.Add(_splitScreenVideoCropX);
		panel4.Controls.Add(MakeLabel("垂直取景", 0, 70));
		_splitScreenVideoCropY = new NumericUpDown
		{
			Location = new Point(65, 66),
			Width = 58,
			Minimum = -100m,
			Maximum = 100m,
			Value = 0m
		};
		panel4.Controls.Add(_splitScreenVideoCropY);
		panel4.Controls.Add(MakeLabel("区域音量", 145, 70));
		_splitScreenRegionVolume = new NumericUpDown
		{
			Location = new Point(210, 66),
			Width = 58,
			Minimum = 0m,
			Maximum = 300m,
			Value = 100m
		};
		panel4.Controls.Add(_splitScreenRegionVolume);
		panel4.Controls.Add(MakeLabel("%", 272, 70));
		_splitScreenVideoApplyRegionButton = MakeButton("应用到本区域全部", 132);
		_splitScreenVideoApplyRegionButton.Location = new Point(0, 96);
		_splitScreenVideoApplyRegionButton.Height = 29;
		panel4.Controls.Add(_splitScreenVideoApplyRegionButton);
		_splitScreenVideoResetButton = MakeButton("重置所选", 86);
		_splitScreenVideoResetButton.Location = new Point(140, 96);
		_splitScreenVideoResetButton.Height = 29;
		panel4.Controls.Add(_splitScreenVideoResetButton);
		_splitScreenRegionEnabled = new CheckBox
		{
			AutoSize = true,
			Checked = true,
			Text = "保留此区域",
			Location = new Point(238, 101)
		};
		panel4.Controls.Add(_splitScreenRegionEnabled);
		tableLayoutPanel6.Controls.Add(panel4, 0, 4);
		Panel panel5 = new Panel();
		panel5.Dock = DockStyle.Fill;
		Panel panel6 = panel5;
		panel6.Controls.Add(new Label
		{
			AutoSize = true,
			Text = "多成品分配",
			Location = new Point(0, 7)
		});
		_splitScreenAssignmentMode = new ComboBox
		{
			Location = new Point(88, 3),
			Width = 142,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenAssignmentMode.Items.AddRange(new object[2] { "按顺序对应", "随机对应" });
		_splitScreenAssignmentMode.SelectedIndex = 0;
		panel6.Controls.Add(_splitScreenAssignmentMode);
		panel6.Controls.Add(new Label
		{
			AutoSize = false,
			Location = new Point(0, 31),
			Size = new Size(355, 22),
			ForeColor = MutedColor,
			Text = "每个成品从本区域候选中选 1 条；短片自动循环补足。"
		});
		tableLayoutPanel6.Controls.Add(panel6, 0, 5);
		groupBox4.Controls.Add(tableLayoutPanel6);
		tableLayoutPanel4.Controls.Add(groupBox4, 2, 0);
		Panel settings = new Panel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(8, 3, 8, 7),
			BackColor = Color.White
		};
		settings.Controls.Add(MakeLabel("输出画面", 18, 18));
		_splitScreenCanvas = new ComboBox
		{
			Location = new Point(86, 14),
			Width = 188,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenCanvas.Items.AddRange(new object[4] { "1080×1920（9:16 竖屏）", "1920×1080（16:9 横屏）", "1080×1080（1:1 方形）", "720×1280（9:16 快速）" });
		_splitScreenCanvas.SelectedIndex = 0;
		settings.Controls.Add(_splitScreenCanvas);
		settings.Controls.Add(MakeLabel("时长方式", 294, 18));
		_splitScreenDurationMode = new ComboBox
		{
			Location = new Point(362, 14),
			Width = 184,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenDurationMode.Items.AddRange(new object[2] { "固定成品时长", "跟随主区域逐条完整导出" });
		_splitScreenDurationMode.SelectedIndex = 0;
		settings.Controls.Add(_splitScreenDurationMode);
		settings.Controls.Add(MakeLabel("主区域", 566, 18));
		_splitScreenMainRegion = new ComboBox
		{
			Location = new Point(620, 14),
			Width = 130,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		settings.Controls.Add(_splitScreenMainRegion);
		settings.Controls.Add(MakeLabel("时长", 770, 18));
		_splitScreenDuration = new NumericUpDown
		{
			Location = new Point(808, 14),
			Width = 72,
			Minimum = 1m,
			Maximum = 86400m,
			Value = 30m,
			DecimalPlaces = 1,
			Increment = 0.5m
		};
		settings.Controls.Add(_splitScreenDuration);
		settings.Controls.Add(MakeLabel("秒", 886, 18));
		settings.Controls.Add(MakeLabel("数量", 918, 18));
		_splitScreenOutputCount = new NumericUpDown
		{
			Location = new Point(958, 14),
			Width = 62,
			Minimum = 1m,
			Maximum = 1000m,
			Value = 1m
		};
		settings.Controls.Add(_splitScreenOutputCount);
		settings.Controls.Add(MakeLabel("声音", 18, 56));
		_splitScreenAudioRegion = new ComboBox
		{
			Location = new Point(62, 52),
			Width = 160,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		settings.Controls.Add(_splitScreenAudioRegion);
		_splitScreenResetButton = MakeButton("重置本页参数", 112);
		_splitScreenResetButton.Location = new Point(1038, 11);
		settings.Controls.Add(_splitScreenResetButton);
		settings.Controls.Add(MakeLabel("当前区域边框", 242, 56));
		_splitScreenBorderPreset = new ComboBox
		{
			Location = new Point(338, 52),
			Width = 126,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenBorderPreset.Items.AddRange(new object[5] { "极细白边", "无边框", "极细黑边", "香槟金细边", "圆角卡片" });
		_splitScreenBorderPreset.SelectedIndex = 1;
		settings.Controls.Add(_splitScreenBorderPreset);
		settings.Controls.Add(MakeLabel("粗细", 476, 56));
		_splitScreenBorderWidth = new NumericUpDown
		{
			Location = new Point(514, 52),
			Width = 58,
			Minimum = 0m,
			Maximum = 80m,
			Value = 0m
		};
		settings.Controls.Add(_splitScreenBorderWidth);
		_splitScreenBorderColorButton = MakeButton("边框颜色", 88);
		_splitScreenBorderColorButton.Location = new Point(582, 49);
		settings.Controls.Add(_splitScreenBorderColorButton);
		_splitScreenBorderScope = new ComboBox
		{
			Location = new Point(624, 52),
			Width = 138,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenBorderScope.Items.AddRange(new object[3] { "全部保留区域", "仅当前区域", "自定义区域" });
		_splitScreenBorderScope.SelectedIndex = 1;
		_splitScreenBorderScope.Visible = false;
		settings.Controls.Add(_splitScreenBorderScope);
		_splitScreenBorderRegionsButton = MakeButton("应用边框到全部区域", 154);
		_splitScreenBorderRegionsButton.Location = new Point(682, 49);
		settings.Controls.Add(_splitScreenBorderRegionsButton);
		_splitScreenRestoreRegionsButton = MakeButton("恢复全部区域", 110);
		_splitScreenRestoreRegionsButton.Location = new Point(848, 49);
		settings.Controls.Add(_splitScreenRestoreRegionsButton);
		settings.Controls.Add(MakeLabel("所选小窗", 18, 94));
		_splitScreenPipShape = new ComboBox
		{
			Location = new Point(86, 90),
			Width = 118,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenPipShape.Items.AddRange(new object[5] { "圆角矩形", "圆形", "椭圆形", "六边形", "八边形" });
		_splitScreenPipShape.SelectedIndex = 0;
		settings.Controls.Add(_splitScreenPipShape);
		_splitScreenPipAspect = new ComboBox
		{
			Location = new Point(214, 90),
			Width = 82,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenPipAspect.Items.AddRange(new object[4] { "1:1", "9:16", "16:9", "自由" });
		_splitScreenPipAspect.SelectedIndex = 0;
		settings.Controls.Add(_splitScreenPipAspect);
		_splitScreenPipPosition = new ComboBox
		{
			Location = new Point(306, 90),
			Width = 130,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenPipPosition.Items.AddRange(new object[10] { "左上", "顶部居中", "右上", "左侧居中", "画面中央", "右侧居中", "左下", "底部居中", "右下", "拼接线中央" });
		_splitScreenPipPosition.SelectedIndex = 8;
		settings.Controls.Add(_splitScreenPipPosition);
		settings.Controls.Add(MakeLabel("大小", 450, 94));
		_splitScreenPipSize = new NumericUpDown
		{
			Location = new Point(488, 90),
			Width = 58,
			Minimum = 12m,
			Maximum = 75m,
			Value = 32m
		};
		settings.Controls.Add(_splitScreenPipSize);
		settings.Controls.Add(MakeLabel("横移", 562, 94));
		_splitScreenPipOffsetX = new NumericUpDown
		{
			Location = new Point(600, 90),
			Width = 58,
			Minimum = -50m,
			Maximum = 50m,
			Value = 0m
		};
		settings.Controls.Add(_splitScreenPipOffsetX);
		settings.Controls.Add(MakeLabel("纵移", 674, 94));
		_splitScreenPipOffsetY = new NumericUpDown
		{
			Location = new Point(712, 90),
			Width = 58,
			Minimum = -50m,
			Maximum = 50m,
			Value = 0m
		};
		settings.Controls.Add(_splitScreenPipOffsetY);
		Label label2 = MakeLabel("只调整当前点选的小窗；各区域视频的缩放和取景在右侧设置。", 790, 94);
		label2.ForeColor = MutedColor;
		label2.Width = 430;
		settings.Controls.Add(label2);
		settings.Controls.Add(MakeLabel("拼屏水印", 18, 132));
		_splitScreenWatermarkLayer = new ComboBox
		{
			Location = new Point(82, 128),
			Width = 210,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		settings.Controls.Add(_splitScreenWatermarkLayer);
		settings.Controls.Add(MakeLabel("安全位置", 306, 132));
		_splitScreenWatermarkPosition = new ComboBox
		{
			Location = new Point(374, 128),
			Width = 126,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenWatermarkPosition.Items.AddRange(new object[9] { "左上角", "顶部居中", "右上角", "左侧居中", "分割线中间", "右侧居中", "左下角", "底部居中", "右下角" });
		_splitScreenWatermarkPosition.SelectedIndex = 4;
		settings.Controls.Add(_splitScreenWatermarkPosition);
		settings.Controls.Add(MakeLabel("X微调", 512, 132));
		_splitScreenWatermarkOffsetX = new NumericUpDown
		{
			Location = new Point(560, 128),
			Width = 66,
			Minimum = -4000m,
			Maximum = 4000m,
			Value = 0m
		};
		settings.Controls.Add(_splitScreenWatermarkOffsetX);
		settings.Controls.Add(MakeLabel("Y微调", 640, 132));
		_splitScreenWatermarkOffsetY = new NumericUpDown
		{
			Location = new Point(688, 128),
			Width = 66,
			Minimum = -4000m,
			Maximum = 4000m,
			Value = 0m
		};
		settings.Controls.Add(_splitScreenWatermarkOffsetY);
		_splitScreenWatermarkRefreshButton = MakeButton("加载/刷新水印预览", 146);
		_splitScreenWatermarkRefreshButton.Location = new Point(768, 125);
		settings.Controls.Add(_splitScreenWatermarkRefreshButton);
		Label label3 = MakeLabel("加载后位置微调会实时预览；动画仅在最终视频中完整播放。", 926, 132);
		label3.ForeColor = MutedColor;
		label3.Width = 430;
		settings.Controls.Add(label3);
		_splitScreenBgm = InstallBgmControls(settings, 18, 164);
		_splitScreenBgmList = new ListBox
		{
			Location = new Point(18, 200),
			Size = new Size(740, 40),
			IntegralHeight = false,
			HorizontalScrollbar = true,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		settings.Controls.Add(_splitScreenBgmList);
		settings.Controls.Add(MakeLabel("音乐池（可多首）", 770, 210));
		settings.Controls.Add(MakeLabel("总输出目录", 18, 252));
		_splitScreenOutputFolder = new TextBox
		{
			Location = new Point(106, 248),
			Width = 710,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		settings.Controls.Add(_splitScreenOutputFolder);
		_splitScreenOutputBrowseButton = MakeButton("选择…", 72);
		_splitScreenOutputBrowseButton.Location = new Point(828, 245);
		_splitScreenOutputBrowseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		settings.Controls.Add(_splitScreenOutputBrowseButton);
		_splitScreenOutputOpenButton = MakeButton("打开", 64);
		_splitScreenOutputOpenButton.Location = new Point(908, 245);
		_splitScreenOutputOpenButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		settings.Controls.Add(_splitScreenOutputOpenButton);
		_splitScreenProgressBar = new ProgressBar
		{
			Location = new Point(18, 285),
			Height = 15,
			Width = 954,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		settings.Controls.Add(_splitScreenProgressBar);
		_splitScreenStatusLabel = new Label
		{
			Location = new Point(18, 305),
			Size = new Size(954, 22),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			ForeColor = MutedColor,
			AutoEllipsis = true,
			Text = "先选模板，再给每个编号区域添加至少一个视频。"
		};
		settings.Controls.Add(_splitScreenStatusLabel);
		_splitScreenStartButton = MakePrimaryButton("开始视频拼屏", 18, 330, 150);
		settings.Controls.Add(_splitScreenStartButton);
		_splitScreenCancelButton = MakeButton("取消", 88);
		_splitScreenCancelButton.Location = new Point(180, 330);
		_splitScreenCancelButton.Height = 42;
		_splitScreenCancelButton.Enabled = false;
		settings.Controls.Add(_splitScreenCancelButton);
		Label label4 = MakeLabel("主区域模式会逐条完整导出主视频；其他区域自动顺序或随机循环配合。", 286, 344);
		label4.ForeColor = MutedColor;
		settings.Controls.Add(label4);
		settings.Resize += delegate
		{
			LayoutSplitScreenOutputPanel(settings);
		};
		tableLayoutPanel2.Controls.Add(settings, 0, 1);
		WireSplitScreenEvents();
		if (_splitScreenQuickPresets.Items.Count > 0)
		{
			_splitScreenQuickPresets.Items[0].Selected = true;
		}
		UpdateSplitScreenAudioChoices();
		UpdateSplitScreenPipUi();
		LayoutSplitScreenOutputPanel(settings);
		return tabPage2;
	}

	private void AddSplitScreenPresetPage(TabControl tabs, string title, ListView list)
	{
		TabPage tabPage = new TabPage(title);
		tabPage.BackColor = Color.White;
		tabPage.Padding = new Padding(3);
		TabPage tabPage2 = tabPage;
		tabPage2.Controls.Add(list);
		tabs.TabPages.Add(tabPage2);
	}

	private ListView CreateSplitScreenPresetList(string category)
	{
		ImageList imageList = new ImageList();
		imageList.ImageSize = new Size(122, 76);
		imageList.ColorDepth = ColorDepth.Depth32Bit;
		ImageList imageList2 = imageList;
		ListView listView = new ListView();
		listView.Dock = DockStyle.Fill;
		listView.View = View.LargeIcon;
		listView.MultiSelect = false;
		listView.HideSelection = false;
		listView.BorderStyle = BorderStyle.None;
		listView.LargeImageList = imageList2;
		listView.BackColor = Color.White;
		listView.TileSize = new Size(136, 106);
		listView.Alignment = ListViewAlignment.Top;
		listView.AutoArrange = true;
		ListView listView2 = listView;
		foreach (SplitScreenLayoutDefinition item in _splitScreenLayouts.Where((SplitScreenLayoutDefinition x) => x.Category == category))
		{
			imageList2.Images.Add(item.Id, DrawSplitScreenTemplateThumbnail(item, imageList2.ImageSize));
			ListViewItem listViewItem = new ListViewItem(item.DisplayName, item.Id);
			listViewItem.Tag = item;
			ListViewItem value = listViewItem;
			listView2.Items.Add(value);
		}
		return listView2;
	}

	private Bitmap DrawSplitScreenTemplateThumbnail(SplitScreenLayoutDefinition definition, Size size)
	{
		Bitmap bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.Clear(Color.FromArgb(246, 248, 252));
		RectangleF rectangleF = FitRectangle(new Size(9, 16), new RectangleF(8f, 5f, size.Width - 16, size.Height - 10));
		using (Brush brush = new SolidBrush(Color.FromArgb(30, 41, 59)))
		{
			graphics.FillRectangle(brush, rectangleF);
		}
		RectangleF[] splitScreenNormalizedRects = GetSplitScreenNormalizedRects(definition);
		for (int i = 0; i < splitScreenNormalizedRects.Length; i++)
		{
			RectangleF rectangleF2 = NormalizedToRectangle(splitScreenNormalizedRects[i], rectangleF);
			using (Brush brush2 = new SolidBrush(Color.FromArgb(78 + i * 19 % 70, 116 + i * 17 % 90, 205 - i * 11 % 80)))
			{
				graphics.FillRectangle(brush2, rectangleF2);
			}
			using (Pen pen = new Pen(Color.White, 2f))
			{
				graphics.DrawRectangle(pen, rectangleF2.X, rectangleF2.Y, Math.Max(1f, rectangleF2.Width - 1f), Math.Max(1f, rectangleF2.Height - 1f));
			}
			using Font font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
			StringFormat stringFormat = new StringFormat();
			stringFormat.Alignment = StringAlignment.Center;
			stringFormat.LineAlignment = StringAlignment.Center;
			using StringFormat format = stringFormat;
			graphics.DrawString((i + 1).ToString(CultureInfo.InvariantCulture), font, Brushes.White, rectangleF2, format);
		}
		return bitmap;
	}

	private static RectangleF FitRectangle(Size aspect, RectangleF bounds)
	{
		float num = (float)aspect.Width / (float)aspect.Height;
		float num2 = bounds.Width;
		float num3 = num2 / num;
		if (num3 > bounds.Height)
		{
			num3 = bounds.Height;
			num2 = num3 * num;
		}
		return new RectangleF(bounds.X + (bounds.Width - num2) / 2f, bounds.Y + (bounds.Height - num3) / 2f, num2, num3);
	}

	private static RectangleF NormalizedToRectangle(RectangleF normalized, RectangleF canvas)
	{
		return new RectangleF(canvas.X + normalized.X * canvas.Width, canvas.Y + normalized.Y * canvas.Height, normalized.Width * canvas.Width, normalized.Height * canvas.Height);
	}

	private void LayoutSplitScreenOutputPanel(Panel panel)
	{
		if (panel != null && _splitScreenOutputFolder != null)
		{
			int num = Math.Max(560, panel.ClientSize.Width - 18);
			_splitScreenOutputOpenButton.Left = Math.Max(414, num - _splitScreenOutputOpenButton.Width);
			_splitScreenOutputBrowseButton.Left = Math.Max(334, _splitScreenOutputOpenButton.Left - 8 - _splitScreenOutputBrowseButton.Width);
			_splitScreenOutputFolder.Width = Math.Max(180, _splitScreenOutputBrowseButton.Left - 12 - _splitScreenOutputFolder.Left);
			_splitScreenProgressBar.Width = Math.Max(200, num - _splitScreenProgressBar.Left);
			_splitScreenStatusLabel.Width = Math.Max(200, num - _splitScreenStatusLabel.Left);
			if (_splitScreenBgmList != null)
			{
				_splitScreenBgmList.Width = Math.Max(360, num - _splitScreenBgmList.Left - 250);
			}
		}
	}

	private void LayoutSplitScreenPreview(Control host)
	{
		if (host == null || _splitScreenPreview == null)
		{
			return;
		}
		Rectangle rectangle = new Rectangle(0, 0, Math.Max(20, host.ClientSize.Width), Math.Max(20, host.ClientSize.Height));
		if (rectangle.Width >= 20 && rectangle.Height >= 20)
		{
			if (_splitScreenPreview.Bounds != rectangle)
			{
				_splitScreenPreviewLayoutBusy = true;
				_splitScreenPreview.Bounds = rectangle;
				_splitScreenPreviewLayoutBusy = false;
			}
			RefreshSplitScreenPreview();
		}
	}

	private void WireSplitScreenEvents()
	{
		base.Shown += delegate
		{
			LayoutSplitScreenPreview(_splitScreenPreviewHost);
		};
		_tabs.SelectedIndexChanged += delegate
		{
			if (_tabs.SelectedTab != null && _tabs.SelectedTab.Text == "视频拼屏")
			{
				try
				{
					BeginInvoke((Action)delegate
					{
						LayoutSplitScreenPreview(_splitScreenPreviewHost);
						LoadSplitScreenWatermarksIntoPreview(userInitiated: false);
					});
				}
				catch
				{
				}
			}
		};
		EventHandler value = delegate(object sender, EventArgs e)
		{
			if (sender is ListView listView && listView.SelectedItems.Count != 0 && listView.SelectedItems[0].Tag is SplitScreenLayoutDefinition definition)
			{
				SelectSplitScreenLayout(definition, listView);
			}
		};
		_splitScreenQuickPresets.SelectedIndexChanged += value;
		_splitScreenGridPresets.SelectedIndexChanged += value;
		_splitScreenPipPresets.SelectedIndexChanged += value;
		_splitScreenRegionSelector.SelectedIndexChanged += delegate
		{
			if (_splitScreenRegionSelector.SelectedIndex >= 0)
			{
				_splitScreenSelectedRegion = _splitScreenRegionSelector.SelectedIndex;
				RefreshSplitScreenRegionUi();
				LoadSplitScreenRegionControls();
				RefreshSplitScreenPreview();
			}
		};
		_splitScreenRegionList.SelectedIndexChanged += delegate
		{
			int splitScreenSelectedRegion = _splitScreenSelectedRegion;
			if (splitScreenSelectedRegion >= 0 && splitScreenSelectedRegion < _splitScreenPreviewIndices.Length && _splitScreenRegionList.SelectedIndex >= 0)
			{
				_splitScreenPreviewIndices[splitScreenSelectedRegion] = _splitScreenRegionList.SelectedIndex;
				LoadSplitScreenRegionControls();
				RefreshSplitScreenPreview();
			}
		};
		_splitScreenAddButton.Click += delegate
		{
			AddSplitScreenVideosFromDialog();
		};
		_splitScreenRemoveButton.Click += delegate
		{
			RemoveSelectedSplitScreenVideos();
		};
		_splitScreenClearButton.Click += delegate
		{
			ClearCurrentSplitScreenRegion();
		};
		_splitScreenMoveUpButton.Click += delegate
		{
			MoveSplitScreenVideo(-1);
		};
		_splitScreenMoveDownButton.Click += delegate
		{
			MoveSplitScreenVideo(1);
		};
		_splitScreenCanvas.SelectedIndexChanged += delegate
		{
			if (_splitScreenWatermarkPreviewLoaded)
			{
				LoadSplitScreenWatermarksIntoPreview(userInitiated: false);
			}
			else
			{
				RefreshSplitScreenPreview();
			}
		};
		_splitScreenDurationMode.SelectedIndexChanged += delegate
		{
			UpdateSplitScreenPlanningUi();
		};
		_splitScreenMainRegion.SelectedIndexChanged += delegate
		{
			if (!_loadingSplitScreenRegionControls && _activeSplitScreenLayout != null && _splitScreenMainRegion.SelectedIndex >= 0)
			{
				int selectedIndex = _splitScreenMainRegion.SelectedIndex;
				GetActiveSplitScreenEnabledRegions()[selectedIndex] = true;
				_splitScreenMainRegionByLayout[_activeSplitScreenLayout.Id] = selectedIndex;
				RefreshSplitScreenRegionSelector();
				RefreshSplitScreenPreview();
				UpdateSplitScreenPlanningUi();
			}
		};
		_splitScreenBorderWidth.ValueChanged += delegate
		{
			SaveSplitScreenSelectedRegionSettings();
			RefreshSplitScreenPreview();
		};
		_splitScreenBorderPreset.SelectedIndexChanged += delegate
		{
			ApplySplitScreenBorderPreset();
		};
		_splitScreenBorderRegionsButton.Click += delegate
		{
			ApplySplitScreenBorderToAllRegions();
		};
		_splitScreenRestoreRegionsButton.Click += delegate
		{
			RestoreAllSplitScreenRegions();
		};
		_splitScreenBorderColorButton.Click += delegate
		{
			using ColorDialog colorDialog = new ColorDialog
			{
				Color = _splitScreenBorderColor,
				FullOpen = true
			};
			if (colorDialog.ShowDialog(this) == DialogResult.OK)
			{
				_splitScreenBorderColor = colorDialog.Color;
				UpdateSplitScreenBorderColorButton();
				SaveSplitScreenSelectedRegionSettings();
				RefreshSplitScreenPreview();
			}
		};
		EventHandler value2 = delegate
		{
			SaveSplitScreenSelectedRegionSettings();
			UpdateSplitScreenPipUi();
			RefreshSplitScreenPreview();
		};
		_splitScreenPipShape.SelectedIndexChanged += value2;
		_splitScreenPipAspect.SelectedIndexChanged += value2;
		_splitScreenPipPosition.SelectedIndexChanged += value2;
		_splitScreenPipSize.ValueChanged += value2;
		_splitScreenPipOffsetX.ValueChanged += value2;
		_splitScreenPipOffsetY.ValueChanged += value2;
		_splitScreenRegionVolume.ValueChanged += delegate
		{
			SaveSplitScreenSelectedRegionSettings();
		};
		EventHandler value3 = delegate
		{
			SaveSplitScreenSelectedVideoViewSettings();
			RefreshSplitScreenPreview();
		};
		_splitScreenVideoScale.ValueChanged += value3;
		_splitScreenVideoCropX.ValueChanged += value3;
		_splitScreenVideoCropY.ValueChanged += value3;
		_splitScreenVideoApplyRegionButton.Click += delegate
		{
			ApplySplitScreenVideoViewToRegion();
		};
		_splitScreenVideoResetButton.Click += delegate
		{
			ResetSelectedSplitScreenVideoView();
		};
		_splitScreenRegionEnabled.CheckedChanged += delegate
		{
			SetSelectedSplitScreenRegionEnabled();
		};
		_splitScreenPreview.MouseClick += OnSplitScreenPreviewClick;
		_splitScreenPreview.Resize += delegate
		{
			RefreshSplitScreenPreview();
		};
		_splitScreenPreview.DragEnter += OnDragEnter;
		_splitScreenPreview.DragDrop += OnSplitScreenPreviewDragDrop;
		_splitScreenRegionList.DragEnter += OnDragEnter;
		_splitScreenRegionList.DragDrop += OnSplitScreenRegionDragDrop;
		_splitScreenOutputBrowseButton.Click += delegate
		{
			ChooseSplitScreenOutputFolder();
		};
		_splitScreenOutputOpenButton.Click += delegate
		{
			OpenFolder(_splitScreenOutputFolder.Text, _latestSplitScreenOutputFolder);
		};
		_splitScreenResetButton.Click += delegate
		{
			ResetSplitScreenParameters(ask: true);
		};
		_splitScreenStartButton.Click += delegate
		{
			StartSplitScreenRender();
		};
		_splitScreenPlayPreviewButton.Click += delegate
		{
			StartSplitScreenPreview();
		};
		_splitScreenCancelButton.Click += delegate
		{
			CancelMerge();
		};
		_splitScreenBgm.Add.Click += delegate
		{
			RefreshSplitScreenBgmList();
		};
		_splitScreenBgm.Clear.Click += delegate
		{
			RefreshSplitScreenBgmList();
		};
		_splitScreenWatermarkLayer.SelectedIndexChanged += delegate
		{
			LoadSplitScreenWatermarkControls();
		};
		_splitScreenWatermarkPosition.SelectedIndexChanged += delegate
		{
			ApplySplitScreenWatermarkControls();
		};
		_splitScreenWatermarkOffsetX.ValueChanged += delegate
		{
			ApplySplitScreenWatermarkControls();
		};
		_splitScreenWatermarkOffsetY.ValueChanged += delegate
		{
			ApplySplitScreenWatermarkControls();
		};
		_splitScreenWatermarkRefreshButton.Click += delegate
		{
			LoadSplitScreenWatermarksIntoPreview(userInitiated: true);
		};
	}

	private void SelectSplitScreenLayout(SplitScreenLayoutDefinition definition, ListView source)
	{
		if (definition == null)
		{
			return;
		}
		_activeSplitScreenLayout = definition;
		EnsureSplitScreenLayoutSettings(definition);
		ListView[] array = new ListView[3] { _splitScreenQuickPresets, _splitScreenGridPresets, _splitScreenPipPresets };
		foreach (ListView listView in array)
		{
			if (listView != null && listView != source)
			{
				ListViewItem[] array2 = listView.SelectedItems.Cast<ListViewItem>().ToArray();
				foreach (ListViewItem listViewItem in array2)
				{
					listViewItem.Selected = false;
				}
			}
		}
		_splitScreenSelectedRegion = Math.Max(0, Math.Min(_splitScreenSelectedRegion, definition.RegionCount - 1));
		RefreshSplitScreenRegionSelector();
		UpdateSplitScreenAudioChoices();
		UpdateSplitScreenMainRegionChoices();
		LoadSplitScreenRegionControls();
		UpdateSplitScreenPlanningUi();
		UpdateSplitScreenPipUi();
		RefreshSplitScreenRegionUi();
		RefreshSplitScreenPreview();
		_splitScreenStatusLabel.Text = "已选择“" + definition.DisplayName + "”，请给 " + definition.RegionCount + " 个编号区域添加视频。";
	}

	private void RefreshSplitScreenRegionSelector()
	{
		if (_splitScreenRegionSelector != null && _activeSplitScreenLayout != null)
		{
			int selectedIndex = Math.Max(0, Math.Min(_splitScreenSelectedRegion, _activeSplitScreenLayout.RegionCount - 1));
			_splitScreenRegionSelector.BeginUpdate();
			_splitScreenRegionSelector.Items.Clear();
			bool[] activeSplitScreenEnabledRegions = GetActiveSplitScreenEnabledRegions();
			int activeSplitScreenMainRegion = GetActiveSplitScreenMainRegion();
			for (int i = 0; i < _activeSplitScreenLayout.RegionCount; i++)
			{
				_splitScreenRegionSelector.Items.Add("区域 " + (i + 1) + ((i == activeSplitScreenMainRegion) ? "（主）" : "") + ((!activeSplitScreenEnabledRegions[i]) ? "（已移除）" : ""));
			}
			_splitScreenRegionSelector.EndUpdate();
			if (_splitScreenRegionSelector.Items.Count > 0)
			{
				_splitScreenRegionSelector.SelectedIndex = selectedIndex;
			}
		}
	}

	private void RefreshSplitScreenRegionUi()
	{
		if (_splitScreenRegionList == null || _activeSplitScreenLayout == null)
		{
			return;
		}
		int num = (_splitScreenSelectedRegion = Math.Max(0, Math.Min(_splitScreenSelectedRegion, _activeSplitScreenLayout.RegionCount - 1)));
		List<string> list = _splitScreenRegionVideos[num];
		_splitScreenRegionList.BeginUpdate();
		_splitScreenRegionList.Items.Clear();
		foreach (string item in list)
		{
			_splitScreenRegionList.Items.Add(Path.GetFileName(item));
		}
		_splitScreenRegionList.EndUpdate();
		int num2 = ((list.Count == 0) ? (-1) : Math.Max(0, Math.Min(_splitScreenPreviewIndices[num], list.Count - 1)));
		_splitScreenPreviewIndices[num] = Math.Max(0, num2);
		if (num2 >= 0)
		{
			_splitScreenRegionList.SelectedIndex = num2;
		}
		if (_splitScreenRegionCountLabel != null)
		{
			_splitScreenRegionCountLabel.Text = "共 " + list.Count + " 个候选";
		}
		LoadSplitScreenRegionControls();
	}

	private void UpdateSplitScreenAudioChoices()
	{
		if (_splitScreenAudioRegion != null && _activeSplitScreenLayout != null)
		{
			int selectedIndex = _splitScreenAudioRegion.SelectedIndex;
			_splitScreenAudioRegion.Items.Clear();
			for (int i = 0; i < _activeSplitScreenLayout.RegionCount; i++)
			{
				_splitScreenAudioRegion.Items.Add("区域 " + (i + 1) + " 原声");
			}
			_splitScreenAudioRegion.Items.Add("全部区域混音");
			_splitScreenAudioRegion.Items.Add("全部静音");
			_splitScreenAudioRegion.SelectedIndex = ((selectedIndex >= 0 && selectedIndex < _splitScreenAudioRegion.Items.Count) ? selectedIndex : 0);
		}
	}

	private void UpdateSplitScreenMainRegionChoices()
	{
		if (_splitScreenMainRegion != null && _activeSplitScreenLayout != null)
		{
			int activeSplitScreenMainRegion = GetActiveSplitScreenMainRegion();
			_loadingSplitScreenRegionControls = true;
			_splitScreenMainRegion.Items.Clear();
			for (int i = 0; i < _activeSplitScreenLayout.RegionCount; i++)
			{
				_splitScreenMainRegion.Items.Add("区域 " + (i + 1));
			}
			_splitScreenMainRegion.SelectedIndex = Math.Max(0, Math.Min(activeSplitScreenMainRegion, _splitScreenMainRegion.Items.Count - 1));
			_loadingSplitScreenRegionControls = false;
		}
	}

	private void UpdateSplitScreenPlanningUi()
	{
		if (_splitScreenDurationMode == null)
		{
			return;
		}
		bool flag = _splitScreenDurationMode.SelectedIndex == 1;
		_splitScreenDuration.Enabled = !flag && !_isRunning;
		_splitScreenOutputCount.Enabled = !flag && !_isRunning;
		if (flag && _activeSplitScreenLayout != null)
		{
			int activeSplitScreenMainRegion = GetActiveSplitScreenMainRegion();
			int num = ((activeSplitScreenMainRegion >= 0 && activeSplitScreenMainRegion < _splitScreenRegionVideos.Length) ? _splitScreenRegionVideos[activeSplitScreenMainRegion].Count : 0);
			if (num > 0)
			{
				_splitScreenOutputCount.Value = Math.Max(_splitScreenOutputCount.Minimum, Math.Min(_splitScreenOutputCount.Maximum, num));
			}
		}
	}

	private void UpdateSplitScreenPipUi()
	{
		if (_splitScreenPipShape != null)
		{
			bool flag = _activeSplitScreenLayout != null && _activeSplitScreenLayout.PictureInPicture && _splitScreenSelectedRegion > 0 && GetActiveSplitScreenEnabledRegions()[_splitScreenSelectedRegion];
			_splitScreenPipShape.Enabled = flag && !_isRunning;
			_splitScreenPipAspect.Enabled = flag && !_isRunning;
			_splitScreenPipPosition.Enabled = flag && !_isRunning;
			_splitScreenPipSize.Enabled = flag && !_isRunning;
			_splitScreenPipOffsetX.Enabled = flag && !_isRunning;
			_splitScreenPipOffsetY.Enabled = flag && !_isRunning;
		}
	}

	private string GetSelectedSplitScreenVideoPath()
	{
		int splitScreenSelectedRegion = _splitScreenSelectedRegion;
		if (splitScreenSelectedRegion < 0 || splitScreenSelectedRegion >= _splitScreenRegionVideos.Length)
		{
			return null;
		}
		List<string> list = _splitScreenRegionVideos[splitScreenSelectedRegion];
		if (list.Count == 0)
		{
			return null;
		}
		int val = ((_splitScreenRegionList == null) ? _splitScreenPreviewIndices[splitScreenSelectedRegion] : _splitScreenRegionList.SelectedIndex);
		val = Math.Max(0, Math.Min(val, list.Count - 1));
		return list[val];
	}

	private void LoadSplitScreenRegionControls()
	{
		if (_activeSplitScreenLayout != null && _splitScreenRegionVolume != null)
		{
			int num = Math.Max(0, Math.Min(_splitScreenSelectedRegion, _activeSplitScreenLayout.RegionCount - 1));
			SplitScreenRegionSettings splitScreenRegionSettings = GetActiveSplitScreenRegionSettings()[num];
			bool[] activeSplitScreenEnabledRegions = GetActiveSplitScreenEnabledRegions();
			string selectedSplitScreenVideoPath = GetSelectedSplitScreenVideoPath();
			SplitScreenVideoViewSettings splitScreenVideoViewSettings = (string.IsNullOrWhiteSpace(selectedSplitScreenVideoPath) ? new SplitScreenVideoViewSettings() : GetSplitScreenVideoViewSettings(num, selectedSplitScreenVideoPath));
			_loadingSplitScreenRegionControls = true;
			_splitScreenPipSize.Value = ClampDecimal(splitScreenRegionSettings.PipSizePercent, _splitScreenPipSize);
			_splitScreenPipOffsetX.Value = ClampDecimal(splitScreenRegionSettings.PipOffsetXPercent, _splitScreenPipOffsetX);
			_splitScreenPipOffsetY.Value = ClampDecimal(splitScreenRegionSettings.PipOffsetYPercent, _splitScreenPipOffsetY);
			SelectSplitScreenComboText(_splitScreenPipShape, splitScreenRegionSettings.PipShape, 0);
			SelectSplitScreenComboText(_splitScreenPipAspect, splitScreenRegionSettings.PipAspect, 0);
			SelectSplitScreenComboText(_splitScreenPipPosition, splitScreenRegionSettings.PipPosition, 8);
			_splitScreenRegionVolume.Value = ClampDecimal(splitScreenRegionSettings.VolumePercent, _splitScreenRegionVolume);
			_splitScreenBorderPreset.SelectedIndex = Math.Max(0, Math.Min(splitScreenRegionSettings.BorderPresetIndex, _splitScreenBorderPreset.Items.Count - 1));
			_splitScreenBorderWidth.Value = ClampDecimal(splitScreenRegionSettings.BorderWidth, _splitScreenBorderWidth);
			_splitScreenBorderColor = splitScreenRegionSettings.BorderColor;
			UpdateSplitScreenBorderColorButton();
			_splitScreenVideoScale.Value = ClampDecimal((decimal)splitScreenVideoViewSettings.ScaleRatio, _splitScreenVideoScale);
			_splitScreenVideoCropX.Value = ClampDecimal(splitScreenVideoViewSettings.CropXPercent, _splitScreenVideoCropX);
			_splitScreenVideoCropY.Value = ClampDecimal(splitScreenVideoViewSettings.CropYPercent, _splitScreenVideoCropY);
			_splitScreenRegionEnabled.Checked = activeSplitScreenEnabledRegions[num];
			_loadingSplitScreenRegionControls = false;
			bool flag = !string.IsNullOrWhiteSpace(selectedSplitScreenVideoPath);
			_splitScreenVideoScale.Enabled = flag && !_isRunning;
			_splitScreenVideoCropX.Enabled = flag && !_isRunning;
			_splitScreenVideoCropY.Enabled = flag && !_isRunning;
			_splitScreenVideoApplyRegionButton.Enabled = flag && !_isRunning;
			_splitScreenVideoResetButton.Enabled = flag && !_isRunning;
			UpdateSplitScreenPipUi();
		}
	}

	private static void SelectSplitScreenComboText(ComboBox combo, string text, int fallback)
	{
		if (combo != null)
		{
			int num = combo.FindStringExact(text ?? "");
			combo.SelectedIndex = ((num >= 0) ? num : Math.Max(0, Math.Min(fallback, combo.Items.Count - 1)));
		}
	}

	private void SaveSplitScreenSelectedRegionSettings()
	{
		if (!_loadingSplitScreenRegionControls && _activeSplitScreenLayout != null && _splitScreenRegionVolume != null)
		{
			int num = Math.Max(0, Math.Min(_splitScreenSelectedRegion, _activeSplitScreenLayout.RegionCount - 1));
			SplitScreenRegionSettings splitScreenRegionSettings = GetActiveSplitScreenRegionSettings()[num];
			splitScreenRegionSettings.PipSizePercent = decimal.ToInt32(_splitScreenPipSize.Value);
			splitScreenRegionSettings.PipOffsetXPercent = decimal.ToInt32(_splitScreenPipOffsetX.Value);
			splitScreenRegionSettings.PipOffsetYPercent = decimal.ToInt32(_splitScreenPipOffsetY.Value);
			splitScreenRegionSettings.PipShape = Convert.ToString(_splitScreenPipShape.SelectedItem, CultureInfo.InvariantCulture);
			splitScreenRegionSettings.PipAspect = Convert.ToString(_splitScreenPipAspect.SelectedItem, CultureInfo.InvariantCulture);
			splitScreenRegionSettings.PipPosition = Convert.ToString(_splitScreenPipPosition.SelectedItem, CultureInfo.InvariantCulture);
			splitScreenRegionSettings.VolumePercent = decimal.ToInt32(_splitScreenRegionVolume.Value);
			splitScreenRegionSettings.BorderPresetIndex = _splitScreenBorderPreset.SelectedIndex;
			splitScreenRegionSettings.BorderWidth = decimal.ToInt32(_splitScreenBorderWidth.Value);
			splitScreenRegionSettings.BorderColor = _splitScreenBorderColor;
		}
	}

	private void SaveSplitScreenSelectedVideoViewSettings()
	{
		if (!_loadingSplitScreenRegionControls && _activeSplitScreenLayout != null)
		{
			string selectedSplitScreenVideoPath = GetSelectedSplitScreenVideoPath();
			if (!string.IsNullOrWhiteSpace(selectedSplitScreenVideoPath))
			{
				SplitScreenVideoViewSettings splitScreenVideoViewSettings = GetSplitScreenVideoViewSettings(_splitScreenSelectedRegion, selectedSplitScreenVideoPath);
				splitScreenVideoViewSettings.ScaleRatio = (double)_splitScreenVideoScale.Value;
				splitScreenVideoViewSettings.CropXPercent = decimal.ToInt32(_splitScreenVideoCropX.Value);
				splitScreenVideoViewSettings.CropYPercent = decimal.ToInt32(_splitScreenVideoCropY.Value);
			}
		}
	}

	private void ApplySplitScreenVideoViewToRegion()
	{
		string selectedSplitScreenVideoPath = GetSelectedSplitScreenVideoPath();
		if (string.IsNullOrWhiteSpace(selectedSplitScreenVideoPath))
		{
			return;
		}
		SplitScreenVideoViewSettings splitScreenVideoViewSettings = GetSplitScreenVideoViewSettings(_splitScreenSelectedRegion, selectedSplitScreenVideoPath).Clone();
		foreach (string item in _splitScreenRegionVideos[_splitScreenSelectedRegion])
		{
			_splitScreenVideoViewSettings[GetSplitScreenVideoViewKey(_splitScreenSelectedRegion, item)] = splitScreenVideoViewSettings.Clone();
		}
		_splitScreenStatusLabel.Text = "当前缩放和取景位置已应用到本区域全部视频。";
		RefreshSplitScreenPreview();
	}

	private void ResetSelectedSplitScreenVideoView()
	{
		string selectedSplitScreenVideoPath = GetSelectedSplitScreenVideoPath();
		if (!string.IsNullOrWhiteSpace(selectedSplitScreenVideoPath))
		{
			_splitScreenVideoViewSettings[GetSplitScreenVideoViewKey(_splitScreenSelectedRegion, selectedSplitScreenVideoPath)] = new SplitScreenVideoViewSettings();
			LoadSplitScreenRegionControls();
			RefreshSplitScreenPreview();
		}
	}

	private void SetSelectedSplitScreenRegionEnabled()
	{
		if (_loadingSplitScreenRegionControls || _activeSplitScreenLayout == null)
		{
			return;
		}
		bool[] activeSplitScreenEnabledRegions = GetActiveSplitScreenEnabledRegions();
		int splitScreenSelectedRegion = _splitScreenSelectedRegion;
		if (!_splitScreenRegionEnabled.Checked && activeSplitScreenEnabledRegions.Count((bool x) => x) <= 1)
		{
			_loadingSplitScreenRegionControls = true;
			_splitScreenRegionEnabled.Checked = true;
			_loadingSplitScreenRegionControls = false;
			MessageBox.Show(this, "至少需要保留一个区域。", "不能删除最后区域", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		activeSplitScreenEnabledRegions[splitScreenSelectedRegion] = _splitScreenRegionEnabled.Checked;
		int activeSplitScreenMainRegion = GetActiveSplitScreenMainRegion();
		if (!activeSplitScreenEnabledRegions[activeSplitScreenMainRegion])
		{
			activeSplitScreenMainRegion = Array.FindIndex(activeSplitScreenEnabledRegions, 0, _activeSplitScreenLayout.RegionCount, (bool x) => x);
			_splitScreenMainRegionByLayout[_activeSplitScreenLayout.Id] = Math.Max(0, activeSplitScreenMainRegion);
		}
		RefreshSplitScreenRegionSelector();
		UpdateSplitScreenMainRegionChoices();
		RefreshSplitScreenPreview();
	}

	private void RestoreAllSplitScreenRegions()
	{
		if (_activeSplitScreenLayout != null)
		{
			bool[] activeSplitScreenEnabledRegions = GetActiveSplitScreenEnabledRegions();
			for (int i = 0; i < _activeSplitScreenLayout.RegionCount; i++)
			{
				activeSplitScreenEnabledRegions[i] = true;
			}
			LoadSplitScreenRegionControls();
			RefreshSplitScreenRegionSelector();
			RefreshSplitScreenPreview();
		}
	}

	private void ChooseSplitScreenBorderRegions()
	{
		if (_activeSplitScreenLayout == null)
		{
			return;
		}
		bool[] activeSplitScreenBorderRegions = GetActiveSplitScreenBorderRegions();
		using (Form form = new Form())
		{
			form.Text = "选择需要边框的区域";
			form.StartPosition = FormStartPosition.CenterParent;
			form.FormBorderStyle = FormBorderStyle.FixedDialog;
			form.MinimizeBox = false;
			form.MaximizeBox = false;
			form.ClientSize = new Size(330, 110 + _activeSplitScreenLayout.RegionCount * 27);
			CheckedListBox checkedListBox = new CheckedListBox();
			checkedListBox.Location = new Point(14, 14);
			checkedListBox.Size = new Size(300, _activeSplitScreenLayout.RegionCount * 27 + 4);
			checkedListBox.CheckOnClick = true;
			CheckedListBox checkedListBox2 = checkedListBox;
			for (int i = 0; i < _activeSplitScreenLayout.RegionCount; i++)
			{
				checkedListBox2.Items.Add("区域 " + (i + 1), activeSplitScreenBorderRegions[i]);
			}
			form.Controls.Add(checkedListBox2);
			Button button = new Button();
			button.Text = "确定";
			button.DialogResult = DialogResult.OK;
			button.Location = new Point(142, checkedListBox2.Bottom + 12);
			button.Width = 80;
			Button button2 = button;
			Button button3 = new Button();
			button3.Text = "取消";
			button3.DialogResult = DialogResult.Cancel;
			button3.Location = new Point(232, checkedListBox2.Bottom + 12);
			button3.Width = 80;
			Button button4 = button3;
			form.Controls.Add(button2);
			form.Controls.Add(button4);
			form.AcceptButton = button2;
			form.CancelButton = button4;
			if (form.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
			for (int j = 0; j < _activeSplitScreenLayout.RegionCount; j++)
			{
				activeSplitScreenBorderRegions[j] = checkedListBox2.GetItemChecked(j);
			}
		}
		_splitScreenBorderScope.SelectedIndex = 2;
		RefreshSplitScreenPreview();
	}

	private void RefreshSplitScreenBgmList()
	{
		if (_splitScreenBgmList == null || _splitScreenBgm == null)
		{
			return;
		}
		_splitScreenBgmList.BeginUpdate();
		_splitScreenBgmList.Items.Clear();
		foreach (string file in _splitScreenBgm.Files)
		{
			_splitScreenBgmList.Items.Add(Path.GetFileName(file));
		}
		_splitScreenBgmList.EndUpdate();
	}

	private void RefreshSplitScreenWatermarkLayerChoices()
	{
		if (_splitScreenWatermarkLayer == null || _textWatermarkEnabled[0] == null)
		{
			return;
		}
		int num = -1;
		if (_splitScreenWatermarkLayer.SelectedIndex >= 0 && _splitScreenWatermarkLayer.SelectedIndex < _splitScreenWatermarkLayerKeys.Count)
		{
			num = _splitScreenWatermarkLayerKeys[_splitScreenWatermarkLayer.SelectedIndex];
		}
		_loadingSplitScreenWatermarkControls = true;
		_splitScreenWatermarkLayer.BeginUpdate();
		_splitScreenWatermarkLayer.Items.Clear();
		_splitScreenWatermarkLayerKeys.Clear();
		for (int i = 0; i < 3; i++)
		{
			if (_textWatermarkEnabled[i].Checked)
			{
				string text = (_watermarkText[i].Text ?? "").Trim();
				if (text.Length > 12)
				{
					text = text.Substring(0, 12) + "…";
				}
				_splitScreenWatermarkLayer.Items.Add("文字水印 " + (i + 1) + ((text.Length > 0) ? (" · " + text) : ""));
				_splitScreenWatermarkLayerKeys.Add(i);
			}
		}
		for (int j = 0; j < 3; j++)
		{
			if (_imageWatermarkEnabled[j].Checked && _imageWatermarkItems[j].Count != 0)
			{
				_splitScreenWatermarkLayer.Items.Add("图片水印 " + (j + 1) + " · " + ((_imageWatermarkPlaybackMode[j].SelectedIndex == 1) ? "轮播 " : "候选 ") + _imageWatermarkItems[j].Count + " 张");
				_splitScreenWatermarkLayerKeys.Add(3 + j);
			}
		}
		_splitScreenWatermarkLayer.EndUpdate();
		int num2 = ((num < 0) ? (-1) : _splitScreenWatermarkLayerKeys.IndexOf(num));
		if (num2 < 0 && _splitScreenWatermarkLayer.Items.Count > 0)
		{
			num2 = 0;
		}
		_splitScreenWatermarkLayer.SelectedIndex = num2;
		_loadingSplitScreenWatermarkControls = false;
		LoadSplitScreenWatermarkControls();
	}

	private void LoadSplitScreenWatermarksIntoPreview(bool userInitiated)
	{
		if (_isRunning || _splitScreenPreview == null)
		{
			return;
		}
		RefreshSplitScreenWatermarkLayerChoices();
		WatermarkProfile profile = CaptureWatermarkProfile(enabled: true);
		string value = ValidateWatermark(profile);
		if (!string.IsNullOrWhiteSpace(value))
		{
			if (_splitScreenWatermarkPreviewOverlay != null)
			{
				_splitScreenWatermarkPreviewOverlay.Dispose();
				_splitScreenWatermarkPreviewOverlay = null;
			}
			_splitScreenWatermarkPreviewLoaded = false;
			RefreshSplitScreenPreview();
			if (userInitiated)
			{
				MessageBox.Show(this, value, "没有可预览的水印", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			}
			return;
		}
		string text = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_WatermarkPreview_" + Guid.NewGuid().ToString("N"));
		try
		{
			Directory.CreateDirectory(text);
			WatermarkProfile profile2 = CreateWatermarkAssignments(profile, 1, expandSlideshows: false)[0];
			Size splitScreenCanvasSize = GetSplitScreenCanvasSize();
			string filename = PrepareCompositeWatermarkPng(profile2, splitScreenCanvasSize.Width, splitScreenCanvasSize.Height, text);
			Bitmap splitScreenWatermarkPreviewOverlay;
			using (Image original = Image.FromFile(filename))
			{
				splitScreenWatermarkPreviewOverlay = new Bitmap(original);
			}
			Bitmap splitScreenWatermarkPreviewOverlay2 = _splitScreenWatermarkPreviewOverlay;
			_splitScreenWatermarkPreviewOverlay = splitScreenWatermarkPreviewOverlay;
			_splitScreenWatermarkPreviewLoaded = true;
			splitScreenWatermarkPreviewOverlay2?.Dispose();
			RefreshSplitScreenPreview();
			if (_splitScreenStatusLabel != null)
			{
				_splitScreenStatusLabel.Text = "已把 " + WatermarkLayerCount(profile2) + " 个水印图层加载到首帧预览；位置微调会同步刷新。" + (_watermarkOnSplitScreen.Checked ? "" : " 导出时请在水印页勾选“视频拼屏”。");
			}
		}
		catch (Exception ex)
		{
			if (userInitiated)
			{
				MessageBox.Show(this, "水印预览加载失败：\n" + ex.Message, "无法加载预览", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}
		finally
		{
			try
			{
				if (Directory.Exists(text))
				{
					Directory.Delete(text, recursive: true);
				}
			}
			catch
			{
			}
		}
	}

	private void ClearSplitScreenWatermarkPreview()
	{
		if (_splitScreenWatermarkPreviewOverlay != null)
		{
			_splitScreenWatermarkPreviewOverlay.Dispose();
			_splitScreenWatermarkPreviewOverlay = null;
		}
		_splitScreenWatermarkPreviewLoaded = false;
	}

	private static string QuickWatermarkPositionToStored(string position)
	{
		if (!(position == "分割线中间"))
		{
			return position;
		}
		return "画面中央";
	}

	private static string StoredWatermarkPositionToQuick(string position)
	{
		if (!(position == "画面中央"))
		{
			return position;
		}
		return "分割线中间";
	}

	private void LoadSplitScreenWatermarkControls()
	{
		if (_splitScreenWatermarkLayer == null || _splitScreenWatermarkPosition == null)
		{
			return;
		}
		int selectedIndex = _splitScreenWatermarkLayer.SelectedIndex;
		bool flag = selectedIndex >= 0 && selectedIndex < _splitScreenWatermarkLayerKeys.Count;
		_loadingSplitScreenWatermarkControls = true;
		if (flag)
		{
			int num = _splitScreenWatermarkLayerKeys[selectedIndex];
			string position;
			int num2;
			int num3;
			if (num < 3)
			{
				position = ComboText(_textWatermarkPosition[num]);
				num2 = decimal.ToInt32(_textWatermarkOffsetX[num].Value);
				num3 = decimal.ToInt32(_textWatermarkOffsetY[num].Value);
			}
			else
			{
				int num4 = num - 3;
				SaveImageWatermarkSelection(num4);
				WatermarkSettings watermarkSettings = _imageWatermarkItems[num4].FirstOrDefault();
				position = ((watermarkSettings == null) ? "右下角" : watermarkSettings.Position);
				num2 = watermarkSettings?.OffsetX ?? 0;
				num3 = watermarkSettings?.OffsetY ?? 0;
			}
			SelectComboText(_splitScreenWatermarkPosition, StoredWatermarkPositionToQuick(position), 4);
			_splitScreenWatermarkOffsetX.Value = ClampDecimal(num2, _splitScreenWatermarkOffsetX);
			_splitScreenWatermarkOffsetY.Value = ClampDecimal(num3, _splitScreenWatermarkOffsetY);
		}
		_splitScreenWatermarkPosition.Enabled = flag && !_isRunning;
		_splitScreenWatermarkOffsetX.Enabled = flag && !_isRunning;
		_splitScreenWatermarkOffsetY.Enabled = flag && !_isRunning;
		_loadingSplitScreenWatermarkControls = false;
	}

	private void ApplySplitScreenWatermarkControls()
	{
		if (_loadingSplitScreenWatermarkControls || _isRunning || _splitScreenWatermarkLayer == null)
		{
			return;
		}
		int selectedIndex = _splitScreenWatermarkLayer.SelectedIndex;
		if (selectedIndex < 0 || selectedIndex >= _splitScreenWatermarkLayerKeys.Count)
		{
			return;
		}
		int num = _splitScreenWatermarkLayerKeys[selectedIndex];
		string position = QuickWatermarkPositionToStored(ComboText(_splitScreenWatermarkPosition));
		int num2 = decimal.ToInt32(_splitScreenWatermarkOffsetX.Value);
		int num3 = decimal.ToInt32(_splitScreenWatermarkOffsetY.Value);
		if (num < 3)
		{
			SelectComboText(_textWatermarkPosition[num], position, 4);
			_textWatermarkOffsetX[num].Value = ClampDecimal(num2, _textWatermarkOffsetX[num]);
			_textWatermarkOffsetY[num].Value = ClampDecimal(num3, _textWatermarkOffsetY[num]);
			_textWatermarkAllowOverflow[num].Checked = false;
			if (_textWatermarkSafeMargin[num].Value < 3m)
			{
				_textWatermarkSafeMargin[num].Value = 3m;
			}
		}
		else
		{
			int num4 = num - 3;
			SaveImageWatermarkSelection(num4);
			foreach (WatermarkSettings item in _imageWatermarkItems[num4])
			{
				item.Position = position;
				item.OffsetX = num2;
				item.OffsetY = num3;
				item.AllowOverflow = false;
			}
			SelectComboText(_imageWatermarkPosition[num4], position, 4);
			_imageWatermarkOffsetX[num4].Value = ClampDecimal(num2, _imageWatermarkOffsetX[num4]);
			_imageWatermarkOffsetY[num4].Value = ClampDecimal(num3, _imageWatermarkOffsetY[num4]);
			_imageWatermarkAllowOverflow[num4].Checked = false;
			SaveImageWatermarkSelection(num4);
		}
		if (_splitScreenStatusLabel != null)
		{
			_splitScreenStatusLabel.Text = "已更新所选水印位置；水印页与拼屏页共用此参数，最后一次调整生效。";
		}
		if (_splitScreenWatermarkPreviewLoaded)
		{
			LoadSplitScreenWatermarksIntoPreview(userInitiated: false);
		}
	}

	private void ApplySplitScreenBorderPreset()
	{
		if (_splitScreenBorderPreset == null || _splitScreenBorderPreset.SelectedIndex < 0 || _loadingSplitScreenRegionControls)
		{
			return;
		}
		switch (_splitScreenBorderPreset.SelectedIndex)
		{
		case 0:
			_splitScreenBorderWidth.Value = 6m;
			_splitScreenBorderColor = Color.White;
			break;
		case 1:
			_splitScreenBorderWidth.Value = 0m;
			_splitScreenBorderColor = Color.Transparent;
			break;
		case 2:
			_splitScreenBorderWidth.Value = 6m;
			_splitScreenBorderColor = Color.Black;
			break;
		case 3:
			_splitScreenBorderWidth.Value = 8m;
			_splitScreenBorderColor = Color.FromArgb(212, 175, 55);
			break;
		case 4:
			_splitScreenBorderWidth.Value = 10m;
			_splitScreenBorderColor = Color.White;
			if (_splitScreenPipShape != null)
			{
				_splitScreenPipShape.SelectedIndex = 0;
			}
			break;
		}
		UpdateSplitScreenBorderColorButton();
		SaveSplitScreenSelectedRegionSettings();
		RefreshSplitScreenPreview();
	}

	private void ApplySplitScreenBorderToAllRegions()
	{
		if (_activeSplitScreenLayout != null)
		{
			SaveSplitScreenSelectedRegionSettings();
			SplitScreenRegionSettings splitScreenRegionSettings = GetActiveSplitScreenRegionSettings()[_splitScreenSelectedRegion];
			SplitScreenRegionSettings[] activeSplitScreenRegionSettings = GetActiveSplitScreenRegionSettings();
			for (int i = 0; i < _activeSplitScreenLayout.RegionCount; i++)
			{
				activeSplitScreenRegionSettings[i].BorderPresetIndex = splitScreenRegionSettings.BorderPresetIndex;
				activeSplitScreenRegionSettings[i].BorderWidth = splitScreenRegionSettings.BorderWidth;
				activeSplitScreenRegionSettings[i].BorderColor = splitScreenRegionSettings.BorderColor;
			}
			_splitScreenStatusLabel.Text = "当前区域的边框样式、粗细和颜色已复制到全部区域。";
			RefreshSplitScreenPreview();
		}
	}

	private void UpdateSplitScreenBorderColorButton()
	{
		if (_splitScreenBorderColorButton != null)
		{
			_splitScreenBorderColorButton.BackColor = ((_splitScreenBorderColor.A == 0) ? Color.White : _splitScreenBorderColor);
			int num = (_splitScreenBorderColor.R * 299 + _splitScreenBorderColor.G * 587 + _splitScreenBorderColor.B * 114) / 1000;
			_splitScreenBorderColorButton.ForeColor = ((num < 130) ? Color.White : InkColor);
		}
	}

	private void AddSplitScreenVideosFromDialog()
	{
		if (_isRunning)
		{
			return;
		}
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Title = "给区域 " + (_splitScreenSelectedRegion + 1) + " 添加候选视频（可多选）";
		openFileDialog.Filter = "视频文件|*.mp4;*.mov;*.mkv;*.avi;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.mts;*.m2ts;*.3gp;*.mpg;*.mpeg|所有文件|*.*";
		openFileDialog.Multiselect = true;
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			AddSplitScreenVideoPaths(openFileDialog.FileNames, _splitScreenSelectedRegion);
		}
	}

	private void AddSplitScreenVideoPaths(IEnumerable<string> paths, int region)
	{
		if (_isRunning || region < 0 || region >= _splitScreenRegionVideos.Length)
		{
			return;
		}
		List<string> list = new List<string>();
		foreach (string item in paths ?? Enumerable.Empty<string>())
		{
			try
			{
				if (File.Exists(item) && IsVideo(item))
				{
					list.Add(Path.GetFullPath(item));
				}
				else if (Directory.Exists(item))
				{
					list.AddRange(Directory.GetFiles(item, "*", SearchOption.TopDirectoryOnly).Where(IsVideo));
				}
			}
			catch
			{
			}
		}
		list.Sort(new NaturalPathComparer());
		HashSet<string> hashSet = new HashSet<string>(_splitScreenRegionVideos[region], StringComparer.OrdinalIgnoreCase);
		int num = 0;
		foreach (string item2 in list)
		{
			string fullPath = Path.GetFullPath(item2);
			if (hashSet.Add(fullPath))
			{
				_splitScreenRegionVideos[region].Add(fullPath);
				QueueSplitScreenThumbnail(fullPath);
				num++;
			}
		}
		_splitScreenSelectedRegion = region;
		if (_splitScreenRegionSelector != null && region < _splitScreenRegionSelector.Items.Count)
		{
			_splitScreenRegionSelector.SelectedIndex = region;
		}
		RefreshSplitScreenRegionUi();
		RefreshSplitScreenPreview();
		UpdateSplitScreenPlanningUi();
		_splitScreenStatusLabel.Text = ((num > 0) ? ("区域 " + (region + 1) + " 已添加 " + num + " 个候选视频，正在提取首帧预览。") : "没有发现新的受支持视频。");
	}

	private void RemoveSelectedSplitScreenVideos()
	{
		int splitScreenSelectedRegion = _splitScreenSelectedRegion;
		if (_isRunning || splitScreenSelectedRegion < 0 || splitScreenSelectedRegion >= _splitScreenRegionVideos.Length)
		{
			return;
		}
		int[] array = (from int x in _splitScreenRegionList.SelectedIndices
			orderby x descending
			select x).ToArray();
		int[] array2 = array;
		foreach (int num2 in array2)
		{
			if (num2 >= 0 && num2 < _splitScreenRegionVideos[splitScreenSelectedRegion].Count)
			{
				_splitScreenRegionVideos[splitScreenSelectedRegion].RemoveAt(num2);
			}
		}
		RefreshSplitScreenRegionUi();
		RefreshSplitScreenPreview();
		UpdateSplitScreenPlanningUi();
	}

	private void ClearCurrentSplitScreenRegion()
	{
		if (!_isRunning)
		{
			int splitScreenSelectedRegion = _splitScreenSelectedRegion;
			if (splitScreenSelectedRegion >= 0 && splitScreenSelectedRegion < _splitScreenRegionVideos.Length)
			{
				_splitScreenRegionVideos[splitScreenSelectedRegion].Clear();
				_splitScreenPreviewIndices[splitScreenSelectedRegion] = 0;
				RefreshSplitScreenRegionUi();
				RefreshSplitScreenPreview();
				UpdateSplitScreenPlanningUi();
			}
		}
	}

	private void MoveSplitScreenVideo(int delta)
	{
		int splitScreenSelectedRegion = _splitScreenSelectedRegion;
		if (!_isRunning && splitScreenSelectedRegion >= 0 && splitScreenSelectedRegion < _splitScreenRegionVideos.Length)
		{
			int selectedIndex = _splitScreenRegionList.SelectedIndex;
			int num = selectedIndex + delta;
			if (selectedIndex >= 0 && num >= 0 && num < _splitScreenRegionVideos[splitScreenSelectedRegion].Count)
			{
				string item = _splitScreenRegionVideos[splitScreenSelectedRegion][selectedIndex];
				_splitScreenRegionVideos[splitScreenSelectedRegion].RemoveAt(selectedIndex);
				_splitScreenRegionVideos[splitScreenSelectedRegion].Insert(num, item);
				_splitScreenPreviewIndices[splitScreenSelectedRegion] = num;
				RefreshSplitScreenRegionUi();
			}
		}
	}

	private void OnSplitScreenRegionDragDrop(object sender, DragEventArgs e)
	{
		if (!_isRunning && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
		{
			AddSplitScreenVideoPaths(paths, _splitScreenSelectedRegion);
		}
	}

	private void OnSplitScreenPreviewDragDrop(object sender, DragEventArgs e)
	{
		if (!_isRunning && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
		{
			Point point = _splitScreenPreview.PointToClient(new Point(e.X, e.Y));
			int num = HitTestSplitScreenRegion(point);
			if (num < 0)
			{
				num = _splitScreenSelectedRegion;
			}
			AddSplitScreenVideoPaths(paths, num);
		}
	}

	private void OnSplitScreenPreviewClick(object sender, MouseEventArgs e)
	{
		int num = HitTestSplitScreenRegion(e.Location);
		if (num >= 0)
		{
			_splitScreenSelectedRegion = num;
			if (_splitScreenRegionSelector != null && num < _splitScreenRegionSelector.Items.Count)
			{
				_splitScreenRegionSelector.SelectedIndex = num;
			}
			RefreshSplitScreenRegionUi();
			RefreshSplitScreenPreview();
		}
	}

	private int HitTestSplitScreenRegion(Point point)
	{
		if (_splitScreenPreview == null || _activeSplitScreenLayout == null)
		{
			return -1;
		}
		RectangleF splitScreenPreviewCanvas = GetSplitScreenPreviewCanvas();
		if (!splitScreenPreviewCanvas.Contains(point))
		{
			return -1;
		}
		RectangleF[] splitScreenNormalizedRects = GetSplitScreenNormalizedRects(_activeSplitScreenLayout);
		for (int num = splitScreenNormalizedRects.Length - 1; num >= 0; num--)
		{
			if (NormalizedToRectangle(splitScreenNormalizedRects[num], splitScreenPreviewCanvas).Contains(point))
			{
				return num;
			}
		}
		return -1;
	}

	private void ChooseSplitScreenOutputFolder()
	{
		if (_isRunning)
		{
			return;
		}
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "选择拼屏视频的保存位置";
		folderBrowserDialog.SelectedPath = _splitScreenOutputFolder.Text;
		folderBrowserDialog.ShowNewFolderButton = true;
		if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
		{
			_splitScreenOutputFolder.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private void ResetSplitScreenParameters(bool ask)
	{
		if (!_isRunning && (!ask || MessageBox.Show(this, "重置视频拼屏参数？已添加到各区域的视频会保留。", "重置本页参数", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes))
		{
			ResetSplitScreenParametersCore();
			SaveUserSettings();
			_splitScreenStatusLabel.Text = "拼屏参数已重置；各区域的视频素材已保留。";
		}
	}

	private void ResetSplitScreenParametersCore()
	{
		ClearSplitScreenWatermarkPreview();
		_splitScreenCanvas.SelectedIndex = 0;
		_splitScreenDurationMode.SelectedIndex = 0;
		_splitScreenDuration.Value = 30m;
		_splitScreenOutputCount.Value = 1m;
		_splitScreenAssignmentMode.SelectedIndex = 0;
		_splitScreenBorderPreset.SelectedIndex = 1;
		_splitScreenBorderScope.SelectedIndex = 1;
		_splitScreenPipShape.SelectedIndex = 0;
		_splitScreenPipAspect.SelectedIndex = 0;
		_splitScreenPipPosition.SelectedIndex = 8;
		_splitScreenPipSize.Value = 32m;
		_splitScreenPipOffsetX.Value = 0m;
		_splitScreenPipOffsetY.Value = 0m;
		_splitScreenRegionSettingsByLayout.Clear();
		_splitScreenEnabledRegionsByLayout.Clear();
		_splitScreenBorderRegionsByLayout.Clear();
		_splitScreenMainRegionByLayout.Clear();
		_splitScreenVideoViewSettings.Clear();
		foreach (SplitScreenLayoutDefinition splitScreenLayout in _splitScreenLayouts)
		{
			EnsureSplitScreenLayoutSettings(splitScreenLayout);
		}
		if (_splitScreenBgm != null)
		{
			_splitScreenBgm.VolumePercent.Value = 30m;
			_splitScreenBgm.AssignmentMode.SelectedIndex = 0;
			UpdateBgmCount(_splitScreenBgm);
		}
		SplitScreenLayoutDefinition splitScreenLayoutDefinition = _splitScreenLayouts.FirstOrDefault();
		if (splitScreenLayoutDefinition != null)
		{
			SelectSplitScreenLayout(splitScreenLayoutDefinition, _splitScreenQuickPresets);
		}
		if (_splitScreenQuickPresets.Items.Count > 0)
		{
			_splitScreenQuickPresets.Items[0].Selected = true;
		}
		_splitScreenOutputFolder.Text = DefaultOutputFolder("视频拼屏输出");
		_latestSplitScreenOutputFolder = null;
		_splitScreenProgressBar.Value = 0;
		RefreshSplitScreenBgmList();
	}

	private RectangleF[] GetSplitScreenNormalizedRects(SplitScreenLayoutDefinition definition)
	{
		if (definition == null || definition.Regions == null)
		{
			return new RectangleF[0];
		}
		RectangleF[] array = definition.Regions.Select((RectangleF regionValue) => regionValue).ToArray();
		if (!definition.PictureInPicture || array.Length < 2)
		{
			return array;
		}
		EnsureSplitScreenLayoutSettings(definition);
		SplitScreenRegionSettings[] array2 = _splitScreenRegionSettingsByLayout[definition.Id];
		Size splitScreenCanvasSize = GetSplitScreenCanvasSize();
		for (int num = 1; num < array.Length; num++)
		{
			ref RectangleF reference = ref array[num];
			reference = BuildSplitScreenPipRectangle(array2[num], splitScreenCanvasSize);
		}
		return array;
	}

	private static RectangleF BuildSplitScreenPipRectangle(SplitScreenRegionSettings settings, Size output)
	{
		if (settings == null)
		{
			settings = new SplitScreenRegionSettings();
		}
		float num = (float)output.Width / (float)Math.Max(1, output.Height);
		float num2 = Math.Max(0.12f, Math.Min(0.75f, (float)settings.PipSizePercent / 100f));
		float val = 1f;
		switch (settings.PipAspect)
		{
		case "9:16":
			val = 0.5625f;
			break;
		case "16:9":
			val = 1.7777778f;
			break;
		case "自由":
			val = 1.3333334f;
			break;
		}
		string pipShape = settings.PipShape;
		if (pipShape == "圆形")
		{
			val = 1f;
		}
		float num3 = Math.Max(0.08f, Math.Min(0.8f, num2 * num / Math.Max(0.1f, val)));
		float num4 = 0.04f;
		float num5 = num4;
		float num6 = num4;
		switch (settings.PipPosition)
		{
		case "顶部居中":
			num5 = (1f - num2) / 2f;
			break;
		case "右上":
			num5 = 1f - num2 - num4;
			break;
		case "左侧居中":
			num6 = (1f - num3) / 2f;
			break;
		case "画面中央":
			num5 = (1f - num2) / 2f;
			num6 = (1f - num3) / 2f;
			break;
		case "右侧居中":
			num5 = 1f - num2 - num4;
			num6 = (1f - num3) / 2f;
			break;
		case "左下":
			num6 = 1f - num3 - num4;
			break;
		case "底部居中":
			num5 = (1f - num2) / 2f;
			num6 = 1f - num3 - num4;
			break;
		case "右下":
			num5 = 1f - num2 - num4;
			num6 = 1f - num3 - num4;
			break;
		case "拼接线中央":
			num5 = (1f - num2) / 2f;
			num6 = 0.5f - num3 / 2f;
			break;
		}
		num5 += (float)settings.PipOffsetXPercent / 100f;
		num6 += (float)settings.PipOffsetYPercent / 100f;
		num5 = Math.Max(0f, Math.Min(1f - num2, num5));
		num6 = Math.Max(0f, Math.Min(1f - num3, num6));
		return new RectangleF(num5, num6, num2, num3);
	}

	private Size GetSplitScreenCanvasSize()
	{
		return ((_splitScreenCanvas != null) ? _splitScreenCanvas.SelectedIndex : 0) switch
		{
			1 => new Size(1920, 1080), 
			2 => new Size(1080, 1080), 
			3 => new Size(720, 1280), 
			_ => new Size(1080, 1920), 
		};
	}

	private RectangleF GetSplitScreenPreviewCanvas()
	{
		if (_splitScreenPreview == null)
		{
			return RectangleF.Empty;
		}
		Size splitScreenCanvasSize = GetSplitScreenCanvasSize();
		RectangleF bounds = new RectangleF(12f, 12f, Math.Max(10, _splitScreenPreview.ClientSize.Width - 24), Math.Max(10, _splitScreenPreview.ClientSize.Height - 24));
		return FitRectangle(splitScreenCanvasSize, bounds);
	}

	private bool IsSplitScreenBorderRegion(int region)
	{
		if (_activeSplitScreenLayout == null || region < 0 || region >= _activeSplitScreenLayout.RegionCount)
		{
			return false;
		}
		bool[] activeSplitScreenEnabledRegions = GetActiveSplitScreenEnabledRegions();
		SplitScreenRegionSettings[] activeSplitScreenRegionSettings = GetActiveSplitScreenRegionSettings();
		if (activeSplitScreenEnabledRegions[region] && activeSplitScreenRegionSettings[region].BorderWidth > 0)
		{
			return activeSplitScreenRegionSettings[region].BorderColor.A > 0;
		}
		return false;
	}

	private void RefreshSplitScreenPreview()
	{
		if (_splitScreenPreview == null || _splitScreenPreview.IsDisposed || _activeSplitScreenLayout == null || _splitScreenBorderWidth == null || _splitScreenCanvas == null)
		{
			return;
		}
		if (!_splitScreenPreviewLayoutBusy && _splitScreenPreviewHost != null && _splitScreenPreviewHost.ClientSize.Width > 100)
		{
			Rectangle rectangle = new Rectangle(0, 0, Math.Max(20, _splitScreenPreviewHost.ClientSize.Width), Math.Max(20, _splitScreenPreviewHost.ClientSize.Height));
			if (_splitScreenPreview.Bounds != rectangle)
			{
				_splitScreenPreviewLayoutBusy = true;
				_splitScreenPreview.Bounds = rectangle;
				_splitScreenPreviewLayoutBusy = false;
			}
		}
		int num = Math.Max(160, _splitScreenPreview.ClientSize.Width);
		int num2 = Math.Max(160, _splitScreenPreview.ClientSize.Height);
		Bitmap image = new Bitmap(num, num2, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(image))
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.Clear(Color.FromArgb(22, 28, 38));
			RectangleF splitScreenPreviewCanvas = GetSplitScreenPreviewCanvas();
			using (Brush brush = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
			{
				graphics.FillRectangle(brush, splitScreenPreviewCanvas.X + 5f, splitScreenPreviewCanvas.Y + 6f, splitScreenPreviewCanvas.Width, splitScreenPreviewCanvas.Height);
			}
			using (Brush brush2 = new SolidBrush(Color.Black))
			{
				graphics.FillRectangle(brush2, splitScreenPreviewCanvas);
			}
			RectangleF[] splitScreenNormalizedRects = GetSplitScreenNormalizedRects(_activeSplitScreenLayout);
			bool[] activeSplitScreenEnabledRegions = GetActiveSplitScreenEnabledRegions();
			SplitScreenRegionSettings[] activeSplitScreenRegionSettings = GetActiveSplitScreenRegionSettings();
			int activeSplitScreenMainRegion = GetActiveSplitScreenMainRegion();
			for (int i = 0; i < splitScreenNormalizedRects.Length; i++)
			{
				RectangleF rectangleF = NormalizedToRectangle(splitScreenNormalizedRects[i], splitScreenPreviewCanvas);
				int borderWidth = activeSplitScreenRegionSettings[i].BorderWidth;
				Color borderColor = activeSplitScreenRegionSettings[i].BorderColor;
				float val = (float)borderWidth * splitScreenPreviewCanvas.Width / Math.Max(1f, GetSplitScreenCanvasSize().Width);
				val = Math.Max((borderWidth > 0) ? 1f : 0f, val);
				bool flag = IsSplitScreenBorderRegion(i);
				RectangleF rectangleF2 = rectangleF;
				if (flag && (!_activeSplitScreenLayout.PictureInPicture || i > 0))
				{
					rectangleF2.Inflate((0f - val) / 2f, (0f - val) / 2f);
				}
				if (rectangleF2.Width < 2f || rectangleF2.Height < 2f)
				{
					continue;
				}
				string splitScreenPreviewPath = GetSplitScreenPreviewPath(i);
				Image cachedSplitScreenPreview = GetCachedSplitScreenPreview(splitScreenPreviewPath);
				bool flag2 = _activeSplitScreenLayout.PictureInPicture && i > 0;
				GraphicsState gstate = graphics.Save();
				GraphicsPath graphicsPath = null;
				if (flag2)
				{
					graphicsPath = CreateSplitScreenShapePath(rectangleF2, activeSplitScreenRegionSettings[i].PipShape);
					graphics.SetClip(graphicsPath);
				}
				if (!activeSplitScreenEnabledRegions[i])
				{
					using (Brush brush3 = new SolidBrush(Color.FromArgb(185, 55, 65, 78)))
					{
						graphics.FillRectangle(brush3, rectangleF2);
					}
					using Font font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
					StringFormat stringFormat = new StringFormat();
					stringFormat.Alignment = StringAlignment.Center;
					stringFormat.LineAlignment = StringAlignment.Center;
					using StringFormat format = stringFormat;
					graphics.DrawString("已移除此区域", font, Brushes.Gainsboro, rectangleF2, format);
				}
				else if (cachedSplitScreenPreview != null)
				{
					DrawImageCover(graphics, cachedSplitScreenPreview, rectangleF2, GetSplitScreenVideoViewSettings(i, splitScreenPreviewPath));
				}
				else
				{
					using (Brush brush4 = new SolidBrush((i == _splitScreenSelectedRegion) ? Color.FromArgb(49, 73, 115) : Color.FromArgb(43, 55, 72)))
					{
						graphics.FillRectangle(brush4, rectangleF2);
					}
					string s = (string.IsNullOrWhiteSpace(splitScreenPreviewPath) ? ("拖入区域 " + (i + 1)) : "正在读取首帧…");
					using (Font font2 = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular))
					{
						StringFormat stringFormat2 = new StringFormat();
						stringFormat2.Alignment = StringAlignment.Center;
						stringFormat2.LineAlignment = StringAlignment.Center;
						using StringFormat format2 = stringFormat2;
						graphics.DrawString(s, font2, Brushes.WhiteSmoke, rectangleF2, format2);
					}
					if (!string.IsNullOrWhiteSpace(splitScreenPreviewPath))
					{
						QueueSplitScreenThumbnail(splitScreenPreviewPath);
					}
				}
				graphics.Restore(gstate);
				if (flag2 && flag && borderWidth > 0)
				{
					using Pen pen = new Pen(borderColor, Math.Max(2f, val));
					pen.Alignment = PenAlignment.Inset;
					graphics.DrawPath(pen, graphicsPath);
				}
				else if (!flag2 && flag && borderWidth > 0)
				{
					using Pen pen2 = new Pen(borderColor, Math.Max(1f, val));
					pen2.Alignment = PenAlignment.Inset;
					graphics.DrawRectangle(pen2, rectangleF.X, rectangleF.Y, Math.Max(1f, rectangleF.Width - 1f), Math.Max(1f, rectangleF.Height - 1f));
				}
				graphicsPath?.Dispose();
				Color color = ((i == _splitScreenSelectedRegion) ? Color.FromArgb(56, 189, 248) : Color.FromArgb(170, 255, 255, 255));
				using (Pen pen3 = new Pen(color, (i == _splitScreenSelectedRegion) ? 3f : 1f))
				{
					pen3.Alignment = PenAlignment.Inset;
					if (flag2)
					{
						using GraphicsPath path = CreateSplitScreenShapePath(rectangleF, activeSplitScreenRegionSettings[i].PipShape);
						graphics.DrawPath(pen3, path);
					}
					else
					{
						graphics.DrawRectangle(pen3, rectangleF.X, rectangleF.Y, Math.Max(1f, rectangleF.Width - 1f), Math.Max(1f, rectangleF.Height - 1f));
					}
				}
				DrawSplitScreenRegionBadge(graphics, rectangleF, (i == activeSplitScreenMainRegion) ? "主" : (i + 1).ToString(CultureInfo.InvariantCulture), i == _splitScreenSelectedRegion);
			}
			if (_splitScreenWatermarkPreviewLoaded && _splitScreenWatermarkPreviewOverlay != null)
			{
				graphics.DrawImage(_splitScreenWatermarkPreviewOverlay, splitScreenPreviewCanvas, new RectangleF(0f, 0f, _splitScreenWatermarkPreviewOverlay.Width, _splitScreenWatermarkPreviewOverlay.Height), GraphicsUnit.Pixel);
				for (int j = 0; j < splitScreenNormalizedRects.Length; j++)
				{
					RectangleF rectangleF3 = NormalizedToRectangle(splitScreenNormalizedRects[j], splitScreenPreviewCanvas);
					bool flag3 = _activeSplitScreenLayout.PictureInPicture && j > 0;
					Color color2 = ((j == _splitScreenSelectedRegion) ? Color.FromArgb(56, 189, 248) : Color.FromArgb(170, 255, 255, 255));
					using (Pen pen4 = new Pen(color2, (j == _splitScreenSelectedRegion) ? 3f : 1f))
					{
						pen4.Alignment = PenAlignment.Inset;
						if (flag3)
						{
							using GraphicsPath path2 = CreateSplitScreenShapePath(rectangleF3, activeSplitScreenRegionSettings[j].PipShape);
							graphics.DrawPath(pen4, path2);
						}
						else
						{
							graphics.DrawRectangle(pen4, rectangleF3.X, rectangleF3.Y, Math.Max(1f, rectangleF3.Width - 1f), Math.Max(1f, rectangleF3.Height - 1f));
						}
					}
					DrawSplitScreenRegionBadge(graphics, rectangleF3, (j == activeSplitScreenMainRegion) ? "主" : (j + 1).ToString(CultureInfo.InvariantCulture), j == _splitScreenSelectedRegion);
				}
			}
		}
		Image image2 = _splitScreenPreview.Image;
		_splitScreenPreview.Image = image;
		image2?.Dispose();
	}

	private string GetSplitScreenPreviewPath(int region)
	{
		if (region < 0 || region >= _splitScreenRegionVideos.Length)
		{
			return null;
		}
		List<string> list = _splitScreenRegionVideos[region];
		if (list.Count == 0)
		{
			return null;
		}
		int index = Math.Max(0, Math.Min(_splitScreenPreviewIndices[region], list.Count - 1));
		return list[index];
	}

	private Image GetCachedSplitScreenPreview(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		lock (_splitScreenPreviewLock)
		{
			Image value;
			return _splitScreenPreviewImages.TryGetValue(path, out value) ? value : null;
		}
	}

	private void QueueSplitScreenThumbnail(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return;
		}
		lock (_splitScreenPreviewLock)
		{
			if (_splitScreenPreviewImages.ContainsKey(path) || _splitScreenPendingPreviews.Contains(path))
			{
				return;
			}
			_splitScreenPendingPreviews.Add(path);
		}
		Task.Run(delegate
		{
			Image image = ExtractSplitScreenFirstFrame(path);
			lock (_splitScreenPreviewLock)
			{
				_splitScreenPendingPreviews.Remove(path);
				if (image != null)
				{
					if (_splitScreenPreviewImages.TryGetValue(path, out var value))
					{
						value.Dispose();
					}
					_splitScreenPreviewImages[path] = image;
				}
			}
			try
			{
				if (!base.IsDisposed && base.IsHandleCreated)
				{
					BeginInvoke(new Action(RefreshSplitScreenPreview));
				}
			}
			catch
			{
			}
		});
	}

	private static Image ExtractSplitScreenFirstFrame(string path)
	{
		string text = FindFfmpeg();
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		string text2 = Path.Combine(Path.GetTempPath(), "vbs_preview_" + Guid.NewGuid().ToString("N") + ".png");
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.FileName = text;
			processStartInfo.Arguments = "-hide_banner -loglevel error -y -ss 0.2 -i " + QuoteArg(path) + " -frames:v 1 -vf " + QuoteArg("scale=640:-2:flags=lanczos") + " " + QuoteArg(text2);
			processStartInfo.UseShellExecute = false;
			processStartInfo.CreateNoWindow = true;
			processStartInfo.RedirectStandardError = false;
			processStartInfo.RedirectStandardOutput = false;
			ProcessStartInfo startInfo = processStartInfo;
			using (Process process = Process.Start(startInfo))
			{
				if (process == null || !process.WaitForExit(30000) || process.ExitCode != 0 || !File.Exists(text2))
				{
					return null;
				}
			}
			using Image original = Image.FromFile(text2);
			return new Bitmap(original);
		}
		catch
		{
			return null;
		}
		finally
		{
			TryDelete(text2);
		}
	}

	private static void DrawImageCover(Graphics g, Image image, RectangleF destination)
	{
		DrawImageCover(g, image, destination, null);
	}

	private static void DrawImageCover(Graphics g, Image image, RectangleF destination, SplitScreenVideoViewSettings view)
	{
		if (image != null && image.Width > 0 && image.Height > 0)
		{
			float num = destination.Width / Math.Max(1f, destination.Height);
			float num2 = (float)image.Width / (float)image.Height;
			RectangleF srcRect;
			if (num2 > num)
			{
				float num3 = (float)image.Height * num;
				srcRect = new RectangleF(((float)image.Width - num3) / 2f, 0f, num3, image.Height);
			}
			else
			{
				float num4 = (float)image.Width / num;
				srcRect = new RectangleF(0f, ((float)image.Height - num4) / 2f, image.Width, num4);
			}
			if (view != null)
			{
				float num5 = (float)Math.Max(1.0, Math.Min(3.0, view.ScaleRatio));
				float num6 = srcRect.Width / num5;
				float num7 = srcRect.Height / num5;
				float num8 = Math.Max(0f, (float)image.Width - num6);
				float num9 = Math.Max(0f, (float)image.Height - num7);
				float num10 = (float)(view.CropXPercent + 100) / 200f;
				float num11 = (float)(view.CropYPercent + 100) / 200f;
				srcRect = new RectangleF(num8 * num10, num9 * num11, num6, num7);
			}
			g.DrawImage(image, destination, srcRect, GraphicsUnit.Pixel);
		}
	}

	private static void DrawSplitScreenRegionBadge(Graphics g, RectangleF region, string text, bool selected)
	{
		float num = Math.Max(22f, Math.Min(34f, Math.Min(region.Width, region.Height) * 0.18f));
		RectangleF rectangleF = new RectangleF(region.X + 7f, region.Y + 7f, num, num);
		using (Brush brush = new SolidBrush(selected ? Color.FromArgb(37, 99, 235) : Color.FromArgb(185, 15, 23, 42)))
		{
			g.FillEllipse(brush, rectangleF);
		}
		using Font font = new Font("Microsoft YaHei UI", Math.Max(8f, num * 0.4f), FontStyle.Bold);
		StringFormat stringFormat = new StringFormat();
		stringFormat.Alignment = StringAlignment.Center;
		stringFormat.LineAlignment = StringAlignment.Center;
		using StringFormat format = stringFormat;
		g.DrawString(text ?? "", font, Brushes.White, rectangleF, format);
	}

	private static GraphicsPath CreateSplitScreenShapePath(RectangleF bounds, string shape)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		switch (shape)
		{
		case "圆形":
		case "椭圆形":
			graphicsPath.AddEllipse(bounds);
			return graphicsPath;
		case "六边形":
		case "八边形":
		{
			int num3 = ((shape == "六边形") ? 6 : 8);
			PointF[] array = new PointF[num3];
			float num4 = bounds.X + bounds.Width / 2f;
			float num5 = bounds.Y + bounds.Height / 2f;
			for (int i = 0; i < num3; i++)
			{
				double num6 = -Math.PI / 2.0 + (double)i * Math.PI * 2.0 / (double)num3;
				ref PointF reference = ref array[i];
				reference = new PointF(num4 + (float)Math.Cos(num6) * bounds.Width / 2f, num5 + (float)Math.Sin(num6) * bounds.Height / 2f);
			}
			graphicsPath.AddPolygon(array);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}
		default:
		{
			float num = Math.Max(4f, Math.Min(bounds.Width, bounds.Height) * 0.09f);
			float num2 = num * 2f;
			graphicsPath.AddArc(bounds.X, bounds.Y, num2, num2, 180f, 90f);
			graphicsPath.AddArc(bounds.Right - num2, bounds.Y, num2, num2, 270f, 90f);
			graphicsPath.AddArc(bounds.Right - num2, bounds.Bottom - num2, num2, num2, 0f, 90f);
			graphicsPath.AddArc(bounds.X, bounds.Bottom - num2, num2, num2, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}
		}
	}

	private SplitScreenRenderPlan CaptureSplitScreenRenderPlan()
	{
		Size splitScreenCanvasSize = GetSplitScreenCanvasSize();
		bool[] enabledRegions = GetActiveSplitScreenEnabledRegions().Take(_activeSplitScreenLayout.RegionCount).ToArray();
		bool[] array = new bool[_activeSplitScreenLayout.RegionCount];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = IsSplitScreenBorderRegion(i);
		}
		SplitScreenRegionSettings[] array2 = (from x in GetActiveSplitScreenRegionSettings().Take(_activeSplitScreenLayout.RegionCount)
			select x.Clone()).ToArray();
		int selectedIndex = _splitScreenAudioRegion.SelectedIndex;
		bool mixAllAudio = selectedIndex == _activeSplitScreenLayout.RegionCount;
		SplitScreenRenderPlan splitScreenRenderPlan = new SplitScreenRenderPlan();
		splitScreenRenderPlan.Layout = _activeSplitScreenLayout;
		splitScreenRenderPlan.Width = splitScreenCanvasSize.Width;
		splitScreenRenderPlan.Height = splitScreenCanvasSize.Height;
		splitScreenRenderPlan.DurationSeconds = (double)_splitScreenDuration.Value;
		splitScreenRenderPlan.OutputCount = decimal.ToInt32(_splitScreenOutputCount.Value);
		splitScreenRenderPlan.RandomAssignment = _splitScreenAssignmentMode.SelectedIndex == 1;
		splitScreenRenderPlan.AudioRegion = ((selectedIndex >= 0 && selectedIndex < _activeSplitScreenLayout.RegionCount) ? selectedIndex : (-1));
		splitScreenRenderPlan.BorderWidth = decimal.ToInt32(_splitScreenBorderWidth.Value);
		splitScreenRenderPlan.BorderColor = _splitScreenBorderColor;
		splitScreenRenderPlan.PipShape = Convert.ToString(_splitScreenPipShape.SelectedItem, CultureInfo.InvariantCulture);
		splitScreenRenderPlan.PipAspect = Convert.ToString(_splitScreenPipAspect.SelectedItem, CultureInfo.InvariantCulture);
		splitScreenRenderPlan.PipPosition = Convert.ToString(_splitScreenPipPosition.SelectedItem, CultureInfo.InvariantCulture);
		splitScreenRenderPlan.PipSizePercent = decimal.ToInt32(_splitScreenPipSize.Value);
		splitScreenRenderPlan.PipOffsetXPercent = decimal.ToInt32(_splitScreenPipOffsetX.Value);
		splitScreenRenderPlan.PipOffsetYPercent = decimal.ToInt32(_splitScreenPipOffsetY.Value);
		splitScreenRenderPlan.FollowMainDuration = _splitScreenDurationMode.SelectedIndex == 1;
		splitScreenRenderPlan.MainRegion = GetActiveSplitScreenMainRegion();
		splitScreenRenderPlan.MixAllAudio = mixAllAudio;
		splitScreenRenderPlan.EnabledRegions = enabledRegions;
		splitScreenRenderPlan.BorderRegions = array;
		splitScreenRenderPlan.RegionSettings = array2;
		splitScreenRenderPlan.RegionVolumePercents = array2.Select((SplitScreenRegionSettings x) => x.VolumePercent).ToArray();
		splitScreenRenderPlan.VideoViewSettings = new SplitScreenVideoViewSettings[_activeSplitScreenLayout.RegionCount];
		return splitScreenRenderPlan;
	}

	private static SplitScreenRenderPlan CloneSplitScreenRenderPlan(SplitScreenRenderPlan source)
	{
		SplitScreenRenderPlan splitScreenRenderPlan = new SplitScreenRenderPlan();
		splitScreenRenderPlan.Layout = source.Layout;
		splitScreenRenderPlan.Width = source.Width;
		splitScreenRenderPlan.Height = source.Height;
		splitScreenRenderPlan.DurationSeconds = source.DurationSeconds;
		splitScreenRenderPlan.OutputCount = source.OutputCount;
		splitScreenRenderPlan.RandomAssignment = source.RandomAssignment;
		splitScreenRenderPlan.AudioRegion = source.AudioRegion;
		splitScreenRenderPlan.BorderWidth = source.BorderWidth;
		splitScreenRenderPlan.BorderColor = source.BorderColor;
		splitScreenRenderPlan.PipShape = source.PipShape;
		splitScreenRenderPlan.PipAspect = source.PipAspect;
		splitScreenRenderPlan.PipPosition = source.PipPosition;
		splitScreenRenderPlan.PipSizePercent = source.PipSizePercent;
		splitScreenRenderPlan.PipOffsetXPercent = source.PipOffsetXPercent;
		splitScreenRenderPlan.PipOffsetYPercent = source.PipOffsetYPercent;
		splitScreenRenderPlan.FollowMainDuration = source.FollowMainDuration;
		splitScreenRenderPlan.MainRegion = source.MainRegion;
		splitScreenRenderPlan.MixAllAudio = source.MixAllAudio;
		splitScreenRenderPlan.EnabledRegions = ((source.EnabledRegions == null) ? null : source.EnabledRegions.ToArray());
		splitScreenRenderPlan.BorderRegions = ((source.BorderRegions == null) ? null : source.BorderRegions.ToArray());
		splitScreenRenderPlan.RegionVolumePercents = ((source.RegionVolumePercents == null) ? null : source.RegionVolumePercents.ToArray());
		splitScreenRenderPlan.RegionSettings = ((source.RegionSettings == null) ? null : source.RegionSettings.Select((SplitScreenRegionSettings x) => x.Clone()).ToArray());
		splitScreenRenderPlan.VideoViewSettings = ((source.VideoViewSettings == null) ? null : source.VideoViewSettings.Select((SplitScreenVideoViewSettings x) => (x != null) ? x.Clone() : new SplitScreenVideoViewSettings()).ToArray());
		return splitScreenRenderPlan;
	}

	private async void StartSplitScreenPreview()
	{
		if (_isRunning)
		{
			MessageBox.Show(this, "当前有正在执行的渲染或导出任务，请稍候再试。", "任务进行中", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		if (_activeSplitScreenLayout == null)
		{
			MessageBox.Show(this, "请先选择一个拼屏模板。", "还没有模板", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		bool[] activeSplitScreenEnabledRegions = GetActiveSplitScreenEnabledRegions();
		for (int i = 0; i < _activeSplitScreenLayout.RegionCount; i++)
		{
			if (activeSplitScreenEnabledRegions[i] && _splitScreenRegionVideos[i].Count == 0)
			{
				_splitScreenSelectedRegion = i;
				_splitScreenRegionSelector.SelectedIndex = i;
				MessageBox.Show(this, "区域 " + (i + 1) + " 还没有视频。请给开启的编号区域各添加至少一个视频后再预览。", "素材未放完整", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
		}
		string ffmpeg = FindFfmpeg();
		if (string.IsNullOrWhiteSpace(ffmpeg))
		{
			MessageBox.Show(this, "没有找到 FFmpeg。请保持便携版文件完整。", "缺少 FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}

		SplitScreenRenderPlan plan = CaptureSplitScreenRenderPlan();
		RectangleF[] normalizedRects = GetSplitScreenNormalizedRects(_activeSplitScreenLayout);
		string[] sources = new string[plan.Layout.RegionCount];
		for (int j = 0; j < plan.Layout.RegionCount; j++)
		{
			if (plan.EnabledRegions[j])
			{
				string p = GetSplitScreenPreviewPath(j);
				if (string.IsNullOrWhiteSpace(p) || !File.Exists(p))
				{
					MessageBox.Show(this, "区域 " + (j + 1) + " 选中的视频文件不存在。", "素材异常", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					return;
				}
				sources[j] = p;
			}
		}

		double previewSec = 5.0;
		int durationIdx = _splitScreenPreviewDuration.SelectedIndex;
		if (durationIdx == 1) previewSec = 8.0;
		else if (durationIdx == 2) previewSec = 10.0;
		else if (durationIdx == 3)
		{
			if (plan.FollowMainDuration && plan.MainRegion >= 0 && plan.MainRegion < sources.Length && !string.IsNullOrWhiteSpace(sources[plan.MainRegion]))
			{
				VideoInfo vi = Probe(ffmpeg, sources[plan.MainRegion]);
				previewSec = vi.DurationSeconds > 0.05 ? vi.DurationSeconds : plan.DurationSeconds;
			}
			else
			{
				previewSec = plan.DurationSeconds;
			}
		}

		SplitScreenRenderPlan previewPlan = CloneSplitScreenRenderPlan(plan);
		previewPlan.DurationSeconds = Math.Max(1.0, previewSec);
		if (previewPlan.Width > 1280 || previewPlan.Height > 1280)
		{
			previewPlan.Width = (previewPlan.Width / 2) & ~1;
			previewPlan.Height = (previewPlan.Height / 2) & ~1;
		}
		for (int m = 0; m < sources.Length; m++)
		{
			previewPlan.VideoViewSettings[m] = (string.IsNullOrWhiteSpace(sources[m]) ? new SplitScreenVideoViewSettings() : GetSplitScreenVideoViewSettings(m, sources[m]).Clone());
		}

		WatermarkProfile splitScreenWatermark = CaptureWatermarkProfile(_watermarkOnSplitScreen.Checked);
		BgmPlan bgmPlan = CaptureBgmPlan(_splitScreenBgm);

		string tempPreviewFile = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_SplitPreview_" + Guid.NewGuid().ToString("N") + ".mp4");

		_splitScreenPlayPreviewButton.Enabled = false;
		_splitScreenPlayPreviewButton.Text = "⏳ 正在合成预览...";
		_splitScreenStatusLabel.Text = "正在生成拼屏动态试看 (包含画面裁剪与音频)...";

		string error = null;
		bool ok = false;

		try
		{
			await Task.Run(() =>
			{
				ok = RenderSplitScreenOutput(ffmpeg, previewPlan, normalizedRects, sources, tempPreviewFile, delegate { }, out error, isFastPreview: true);
				if (ok && File.Exists(tempPreviewFile))
				{
					if (splitScreenWatermark.Enabled)
					{
						ApplyWatermarkToSplitScreenOutput(ffmpeg, tempPreviewFile, splitScreenWatermark, previewPlan.DurationSeconds, delegate { }, out _);
					}
					if (bgmPlan.Enabled && bgmPlan.Files != null && bgmPlan.Files.Count > 0)
					{
						string bgmPath = SelectBgm(bgmPlan, 0, new Random());
						if (File.Exists(bgmPath))
						{
							ApplyBackgroundMusic(ffmpeg, tempPreviewFile, bgmPath, bgmPlan.VolumePercent, delegate { }, out _);
						}
					}
				}
			});
		}
		catch (Exception ex)
		{
			ok = false;
			error = ex.Message;
		}
		finally
		{
			_splitScreenPlayPreviewButton.Enabled = true;
			_splitScreenPlayPreviewButton.Text = "▶ 播放拼屏预览 (带声音)";
		}

		if (ok && File.Exists(tempPreviewFile))
		{
			_splitScreenStatusLabel.Text = "拼屏效果预览已生成，正在播放...";
			using (VideoPreviewForm previewForm = new VideoPreviewForm(tempPreviewFile, $"视频拼屏 - 效果试看 ({previewSec:0}秒)"))
			{
				previewForm.ShowDialog(this);
			}
			_splitScreenStatusLabel.Text = "视频拼屏待命中。可点击播放预览或开始批量拼屏导出。";
		}
		else
		{
			_splitScreenStatusLabel.Text = "生成预览失败：" + error;
			MessageBox.Show(this, "生成拼屏预览失败：\n" + error, "预览失败", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private void StartSplitScreenRender()
	{
		if (_isRunning)
		{
			return;
		}
		if (_activeSplitScreenLayout == null)
		{
			MessageBox.Show(this, "请先选择一个拼屏模板。", "还没有模板", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		bool[] activeSplitScreenEnabledRegions = GetActiveSplitScreenEnabledRegions();
		for (int i = 0; i < _activeSplitScreenLayout.RegionCount; i++)
		{
			if (activeSplitScreenEnabledRegions[i] && _splitScreenRegionVideos[i].Count == 0)
			{
				_splitScreenSelectedRegion = i;
				_splitScreenRegionSelector.SelectedIndex = i;
				MessageBox.Show(this, "区域 " + (i + 1) + " 还没有视频。请给所有编号区域各添加至少一个视频。", "素材未放完整", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
		}
		string ffmpeg = FindFfmpeg();
		if (string.IsNullOrWhiteSpace(ffmpeg))
		{
			MessageBox.Show(this, "没有找到 FFmpeg。请保持便携版文件完整。", "缺少 FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		string outputParent = _splitScreenOutputFolder.Text.Trim();
		if (outputParent.Length == 0)
		{
			MessageBox.Show(this, "请选择视频拼屏总输出目录。", "缺少输出目录", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		try
		{
			Directory.CreateDirectory(outputParent);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "无法创建输出目录：\n" + ex.Message, "输出目录不可用", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		SplitScreenRenderPlan plan = CaptureSplitScreenRenderPlan();
		WatermarkProfile splitScreenWatermark = CaptureWatermarkProfile(_watermarkOnSplitScreen.Checked);
		string value = ValidateWatermark(splitScreenWatermark);
		if (!string.IsNullOrWhiteSpace(value))
		{
			MessageBox.Show(this, value, "拼屏水印设置不完整", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		RectangleF[] normalizedRects = GetSplitScreenNormalizedRects(_activeSplitScreenLayout);
		List<string>[] regionSnapshot = new List<string>[plan.Layout.RegionCount];
		for (int j = 0; j < plan.Layout.RegionCount; j++)
		{
			regionSnapshot[j] = _splitScreenRegionVideos[j].Where(File.Exists).ToList();
		}
		if (plan.MainRegion < 0 || plan.MainRegion >= plan.Layout.RegionCount || !plan.EnabledRegions[plan.MainRegion] || regionSnapshot[plan.MainRegion].Count == 0)
		{
			MessageBox.Show(this, "主区域必须保留并至少包含一个有效视频。", "主区域不可用", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		if (plan.FollowMainDuration)
		{
			plan.OutputCount = regionSnapshot[plan.MainRegion].Count;
		}
		List<WatermarkProfile> splitScreenWatermarkAssignments = CreateWatermarkAssignments(splitScreenWatermark, plan.OutputCount);
		_activeSplitScreenBgmPlan = CaptureBgmPlan(_splitScreenBgm);
		_cancelRequested = false;
		_splitScreenProgressBar.Value = 0;
		_splitScreenStatusLabel.Text = "正在准备视频拼屏…";
		SetRunningState(running: true);
		Task.Run(delegate
		{
			string batchFolder = null;
			int succeeded = 0;
			int failed = 0;
			string lastError = null;
			try
			{
				batchFolder = CreateBatchOutputFolder(outputParent, "视频拼屏");
				_latestSplitScreenOutputFolder = batchFolder;
				Random random = new Random(Environment.TickCount * 31 + plan.OutputCount);
				Random random2 = new Random(Environment.TickCount * 17 + 971);
				for (int k = 0; k < plan.OutputCount && !_cancelRequested; k++)
				{
					string[] array = new string[plan.Layout.RegionCount];
					for (int l = 0; l < array.Length; l++)
					{
						if (plan.EnabledRegions[l])
						{
							List<string> list = regionSnapshot[l];
							int index = ((plan.FollowMainDuration && l == plan.MainRegion) ? (k % list.Count) : (plan.RandomAssignment ? random.Next(list.Count) : (k % list.Count)));
							array[l] = list[index];
						}
					}
					SplitScreenRenderPlan splitScreenRenderPlan = CloneSplitScreenRenderPlan(plan);
					if (plan.FollowMainDuration)
					{
						VideoInfo videoInfo = Probe(ffmpeg, array[plan.MainRegion]);
						if (videoInfo.DurationSeconds <= 0.05)
						{
							failed++;
							lastError = "无法读取主区域视频时长：" + Path.GetFileName(array[plan.MainRegion]);
							continue;
						}
						splitScreenRenderPlan.DurationSeconds = videoInfo.DurationSeconds;
					}
					for (int m = 0; m < array.Length; m++)
					{
						splitScreenRenderPlan.VideoViewSettings[m] = (string.IsNullOrWhiteSpace(array[m]) ? new SplitScreenVideoViewSettings() : GetSplitScreenVideoViewSettings(m, array[m]).Clone());
					}
					string text = FindAvailableSplitScreenOutput(batchFolder, k + 1);
					int completedBefore = k;
					bool enabled = splitScreenWatermark.Enabled;
					double renderPortion = ((!enabled) ? (_activeSplitScreenBgmPlan.Enabled ? 0.86 : 1.0) : (_activeSplitScreenBgmPlan.Enabled ? 0.72 : 0.82));
					double watermarkPortion = ((!enabled) ? 0.0 : (_activeSplitScreenBgmPlan.Enabled ? 0.16 : 0.18));
					double bgmStart = renderPortion + watermarkPortion;
					string error2;
					if (!RenderSplitScreenOutput(ffmpeg, splitScreenRenderPlan, normalizedRects, array, text, delegate(double p)
					{
						ReportSplitScreenProgress(completedBefore, plan.OutputCount, p * renderPortion, "正在渲染第 " + (completedBefore + 1) + "/" + plan.OutputCount + " 个拼屏视频");
					}, out var error))
					{
						failed++;
						lastError = error;
						TryDelete(text);
					}
					else if (enabled && !_cancelRequested && !ApplyWatermarkToSplitScreenOutput(ffmpeg, text, splitScreenWatermarkAssignments[Math.Min(k, splitScreenWatermarkAssignments.Count - 1)], splitScreenRenderPlan.DurationSeconds, delegate(double p)
					{
						ReportSplitScreenProgress(completedBefore, plan.OutputCount, renderPortion + p * watermarkPortion, "正在为第 " + (completedBefore + 1) + "/" + plan.OutputCount + " 个拼屏成品添加水印");
					}, out error2))
					{
						failed++;
						lastError = "拼屏水印失败：" + error2;
						TryDelete(text);
					}
					else
					{
						if (_activeSplitScreenBgmPlan.Enabled && !_cancelRequested)
						{
							string bgmPath = SelectBgm(_activeSplitScreenBgmPlan, k, random2);
							if (!ApplyBackgroundMusic(ffmpeg, text, bgmPath, _activeSplitScreenBgmPlan.VolumePercent, delegate(double p)
							{
								ReportSplitScreenProgress(completedBefore, plan.OutputCount, bgmStart + p * (1.0 - bgmStart), "正在为第 " + (completedBefore + 1) + "/" + plan.OutputCount + " 个成品添加 BGM");
							}, out var error3))
							{
								failed++;
								lastError = "BGM 添加失败：" + error3;
								TryDelete(text);
								continue;
							}
						}
						if (!ValidateExactDurationOutput(ffmpeg, text, splitScreenRenderPlan.DurationSeconds, out var reason))
						{
							failed++;
							lastError = reason;
							TryDelete(text);
						}
						else
						{
							succeeded++;
							ReportSplitScreenProgress(k + 1, plan.OutputCount, 0.0, "已完成 " + (k + 1) + "/" + plan.OutputCount + " 个拼屏视频");
						}
					}
				}
			}
			catch (Exception ex2)
			{
				failed++;
				lastError = ex2.Message;
			}
			try
			{
				BeginInvoke((Action)delegate
				{
					SetRunningState(running: false);
					_splitScreenProgressBar.Value = ((plan.OutputCount != 0) ? Math.Min(100, (int)Math.Round((double)(succeeded + failed) * 100.0 / (double)plan.OutputCount)) : 0);
					if (_cancelRequested)
					{
						_splitScreenStatusLabel.Text = "视频拼屏已取消；已经完成的成品会保留。";
					}
					else
					{
						_splitScreenStatusLabel.Text = "视频拼屏完成：成功 " + succeeded + "，失败 " + failed + "。";
						MessageBoxIcon messageBoxIcon = ((failed == 0) ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation);
						string text2 = "视频拼屏完成。\n\n成功：" + succeeded + "\n失败：" + failed + (plan.FollowMainDuration ? "\n时长：跟随主区域，每条主视频完整导出一次" : ("\n每个成品：" + FfmpegNumber(plan.DurationSeconds) + " 秒"));
						if (!string.IsNullOrWhiteSpace(lastError))
						{
							text2 = text2 + "\n\n最近错误：" + LastUsefulLines(lastError, 5);
						}
						ShowCompletionAndOpenFolder(text2, (failed == 0) ? "拼屏完成" : "拼屏完成（有失败）", messageBoxIcon, outputParent, batchFolder);
					}
				});
			}
			catch
			{
			}
		});
	}

	private void ReportSplitScreenProgress(int completedOutputs, int totalOutputs, double currentProgress, string status)
	{
		double num = ((double)completedOutputs + Math.Max(0.0, Math.Min(1.0, currentProgress))) / (double)Math.Max(1, totalOutputs);
		int percent = Math.Max(0, Math.Min(100, (int)Math.Round(num * 100.0)));
		try
		{
			if (!base.IsDisposed && base.IsHandleCreated)
			{
				BeginInvoke((Action)delegate
				{
					_splitScreenProgressBar.Value = percent;
					_splitScreenStatusLabel.Text = status + "（" + percent + "%）";
				});
			}
		}
		catch
		{
		}
	}

	private bool ApplyWatermarkToSplitScreenOutput(string ffmpeg, string outputPath, WatermarkProfile profile, double expectedDuration, Action<double> progress, out string error)
	{
		error = null;
		string text = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_SplitWatermark_" + Guid.NewGuid().ToString("N"));
		string text2 = Path.Combine(text, "watermarked.mp4");
		try
		{
			Directory.CreateDirectory(text);
			for (int i = 1; i <= 3; i++)
			{
				if (_cancelRequested)
				{
					break;
				}
				TryDelete(text2);
				WatermarkProfile profile2 = ((i == 3) ? MakeSafeWatermarkProfile(profile) : profile);
				VideoInfo videoInfo = Probe(ffmpeg, outputPath);
				if (!videoInfo.HasVideo || videoInfo.Width <= 0 || videoInfo.Height <= 0)
				{
					error = "无法读取刚生成的拼屏成品。";
					return false;
				}
				List<PreparedWatermarkLayer> layers;
				try
				{
					layers = PrepareWatermarkLayers(profile2, videoInfo.Width, videoInfo.Height, text, expectedDuration);
				}
				catch (Exception ex)
				{
					error = "准备水印失败：" + ex.Message;
					return false;
				}
				StringBuilder stringBuilder = new StringBuilder("-hide_banner -y -i ").Append(QuoteArg(outputPath));
				AppendWatermarkInputs(stringBuilder, layers);
				List<string> list = new List<string>();
				list.Add("[0:v]setpts=PTS-STARTPTS[base]");
				List<string> list2 = list;
				string currentVideo = "base";
				AppendAnimatedWatermarkFilters(list2, ref currentVideo, 1, layers);
				list2.Add("[" + currentVideo + "]trim=duration=" + FfmpegNumber(expectedDuration) + ",setpts=PTS-STARTPTS,format=yuv420p[vout]");
				stringBuilder.Append(" -filter_complex ").Append(QuoteArg(string.Join(";", list2.ToArray()))).Append(" -map [vout] -map 0:a:0? -c:v libx264 -preset fast -crf 17 -pix_fmt yuv420p")
					.Append(" -metadata:s:v:0 rotate=0 -c:a copy -t ")
					.Append(FfmpegNumber(expectedDuration))
					.Append(" -movflags +faststart -progress pipe:1 -nostats ")
					.Append(QuoteArg(text2));
				int num = RunFfmpeg(ffmpeg, stringBuilder.ToString(), expectedDuration, progress, out var errorText);
				string reason = null;
				if (num == 0 && ValidateExactDurationOutput(ffmpeg, text2, expectedDuration, out reason))
				{
					File.Copy(text2, outputPath, overwrite: true);
					TryDelete(text2);
					return true;
				}
				error = ((num == 0) ? reason : errorText);
			}
			return false;
		}
		catch (Exception ex2)
		{
			error = ex2.Message;
			return false;
		}
		finally
		{
			try
			{
				if (Directory.Exists(text))
				{
					Directory.Delete(text, recursive: true);
				}
			}
			catch
			{
			}
		}
	}

	private bool RenderSplitScreenOutput(string ffmpeg, SplitScreenRenderPlan plan, RectangleF[] normalizedRects, string[] sources, string outputPath, Action<double> progress, out string error, bool isFastPreview = false)
	{
		error = null;
		string text = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_Pip_" + Guid.NewGuid().ToString("N"));
		try
		{
			Directory.CreateDirectory(text);
			StringBuilder stringBuilder = new StringBuilder("-hide_banner -y ");
			int[] array = Enumerable.Repeat(-1, sources.Length).ToArray();
			int num = 0;
			for (int i = 0; i < sources.Length; i++)
			{
				if ((plan.EnabledRegions == null || plan.EnabledRegions[i]) && !string.IsNullOrWhiteSpace(sources[i]))
				{
					array[i] = num++;
					stringBuilder.Append("-stream_loop -1 -i ").Append(QuoteArg(sources[i])).Append(" ");
				}
			}
			int[] array2 = Enumerable.Repeat(-1, sources.Length).ToArray();
			int[] array3 = Enumerable.Repeat(-1, sources.Length).ToArray();
			List<Rectangle> list = new List<Rectangle>();
			for (int j = 0; j < normalizedRects.Length; j++)
			{
				SplitScreenRegionSettings splitScreenRegionSettings = ((plan.RegionSettings != null && j < plan.RegionSettings.Length) ? plan.RegionSettings[j] : new SplitScreenRegionSettings());
				list.Add(ToSplitScreenPixelRectangle(normalizedRects[j], plan.Width, plan.Height, (plan.BorderRegions != null && j < plan.BorderRegions.Length && plan.BorderRegions[j]) ? splitScreenRegionSettings.BorderWidth : 0));
			}
			if (plan.Layout.PictureInPicture)
			{
				for (int k = 1; k < list.Count; k++)
				{
					if (array[k] >= 0)
					{
						SplitScreenRegionSettings splitScreenRegionSettings2 = ((plan.RegionSettings != null && k < plan.RegionSettings.Length) ? plan.RegionSettings[k] : new SplitScreenRegionSettings());
						int borderWidth = ((plan.BorderRegions != null && k < plan.BorderRegions.Length && plan.BorderRegions[k]) ? splitScreenRegionSettings2.BorderWidth : 0);
						CreateSplitScreenPipAssets(text, k, list[k].Width, list[k].Height, splitScreenRegionSettings2.PipShape, borderWidth, splitScreenRegionSettings2.BorderColor, out var maskPath, out var borderPath);
						array2[k] = num++;
						stringBuilder.Append("-loop 1 -framerate 30 -i ").Append(QuoteArg(maskPath)).Append(" ");
						array3[k] = num++;
						stringBuilder.Append("-loop 1 -framerate 30 -i ").Append(QuoteArg(borderPath)).Append(" ");
					}
				}
			}
			List<string> list2 = new List<string>();
			string text2 = FfmpegNumber(plan.DurationSeconds);
			for (int l = 0; l < sources.Length; l++)
			{
				if (array[l] >= 0)
				{
					Rectangle rectangle = list[l];
					SplitScreenVideoViewSettings splitScreenVideoViewSettings = ((plan.VideoViewSettings != null && l < plan.VideoViewSettings.Length && plan.VideoViewSettings[l] != null) ? plan.VideoViewSettings[l] : new SplitScreenVideoViewSettings());
					double num2 = Math.Max(1.0, Math.Min(3.0, splitScreenVideoViewSettings.ScaleRatio));
					int num3 = MakeEven(Math.Max(rectangle.Width, (int)Math.Ceiling((double)rectangle.Width * num2)));
					int num4 = MakeEven(Math.Max(rectangle.Height, (int)Math.Ceiling((double)rectangle.Height * num2)));
					string text3 = "(in_w-out_w)*" + FfmpegNumber((double)(splitScreenVideoViewSettings.CropXPercent + 100) / 200.0);
					string text4 = "(in_h-out_h)*" + FfmpegNumber((double)(splitScreenVideoViewSettings.CropYPercent + 100) / 200.0);
					list2.Add("[" + array[l] + ":v:0]fps=30,scale=" + num3 + ":" + num4 + ":force_original_aspect_ratio=increase:flags=lanczos,crop=" + rectangle.Width + ":" + rectangle.Height + ":" + text3 + ":" + text4 + ",setsar=1,trim=duration=" + text2 + ",setpts=PTS-STARTPTS,format=" + ((plan.Layout.PictureInPicture && l > 0) ? "rgb24" : "yuv420p") + "[sv" + l + "]");
				}
			}
			string text5;
			if (plan.Layout.PictureInPicture)
			{
				if (array[0] >= 0)
				{
					text5 = "sv0";
				}
				else
				{
					list2.Add("color=c=black:s=" + plan.Width + "x" + plan.Height + ":d=" + text2 + ":r=30[base]");
					text5 = "base";
				}
				for (int m = 1; m < sources.Length; m++)
				{
					if (array[m] >= 0)
					{
						Rectangle rectangle2 = list[m];
						string text6 = "pm" + m;
						string text7 = "pip" + m;
						string text8 = "pb" + m;
						list2.Add("[" + array2[m] + ":v:0]scale=" + rectangle2.Width + ":" + rectangle2.Height + ",format=gray[" + text6 + "]");
						list2.Add("[sv" + m + "][" + text6 + "]alphamerge[" + text7 + "]");
						list2.Add("[" + array3[m] + ":v:0]scale=" + rectangle2.Width + ":" + rectangle2.Height + ",format=rgba[" + text8 + "]");
						string text9 = "ovp" + m;
						list2.Add("[" + text5 + "][" + text7 + "]overlay=x=" + rectangle2.X + ":y=" + rectangle2.Y + ":shortest=0:eof_action=repeat[" + text9 + "]");
						string text10 = "ovb" + m;
						list2.Add("[" + text9 + "][" + text8 + "]overlay=x=" + rectangle2.X + ":y=" + rectangle2.Y + ":shortest=0:eof_action=repeat[" + text10 + "]");
						text5 = text10;
					}
				}
			}
			else
			{
				list2.Add("color=c=black:s=" + plan.Width + "x" + plan.Height + ":d=" + text2 + ":r=30[base]");
				text5 = "base";
				for (int n = 0; n < sources.Length; n++)
				{
					if (array[n] >= 0)
					{
						Rectangle rectangle3 = list[n];
						string text11 = "ov" + n;
						list2.Add("[" + text5 + "][sv" + n + "]overlay=x=" + rectangle3.X + ":y=" + rectangle3.Y + ":shortest=0:eof_action=repeat[" + text11 + "]");
						text5 = text11;
					}
				}
			}
			for (int num5 = 0; num5 < normalizedRects.Length; num5++)
			{
				if (plan.BorderRegions != null && num5 < plan.BorderRegions.Length && plan.BorderRegions[num5] && (!plan.Layout.PictureInPicture || num5 <= 0))
				{
					SplitScreenRegionSettings splitScreenRegionSettings3 = ((plan.RegionSettings != null && num5 < plan.RegionSettings.Length) ? plan.RegionSettings[num5] : new SplitScreenRegionSettings());
					if (splitScreenRegionSettings3.BorderWidth > 0 && splitScreenRegionSettings3.BorderColor.A > 0)
					{
						Rectangle rectangle4 = ToSplitScreenPixelRectangle(normalizedRects[num5], plan.Width, plan.Height, 0);
						string text12 = "rb" + num5;
						list2.Add("[" + text5 + "]drawbox=x=" + rectangle4.X + ":y=" + rectangle4.Y + ":w=" + rectangle4.Width + ":h=" + rectangle4.Height + ":color=" + ColorToFfmpeg(splitScreenRegionSettings3.BorderColor) + ":t=" + splitScreenRegionSettings3.BorderWidth + "[" + text12 + "]");
						text5 = text12;
					}
				}
			}
			list2.Add("[" + text5 + "]trim=duration=" + text2 + ",setpts=PTS-STARTPTS,format=yuv420p[outv]");
			bool flag = false;
			string value = null;
			if (plan.MixAllAudio)
			{
				List<string> list3 = new List<string>();
				for (int num6 = 0; num6 < sources.Length; num6++)
				{
					if (array[num6] >= 0 && Probe(ffmpeg, sources[num6]).HasAudio)
					{
						string text13 = "sa" + num6;
						int num7 = ((plan.RegionVolumePercents != null && num6 < plan.RegionVolumePercents.Length) ? plan.RegionVolumePercents[num6] : 100);
						list2.Add("[" + array[num6] + ":a:0]volume=" + FfmpegNumber((double)num7 / 100.0) + ",atrim=duration=" + text2 + ",asetpts=PTS-STARTPTS,aresample=async=1:first_pts=0,apad=pad_dur=" + text2 + ",atrim=duration=" + text2 + "[" + text13 + "]");
						list3.Add("[" + text13 + "]");
					}
				}
				if (list3.Count > 0)
				{
					flag = true;
					value = "outa";
					if (list3.Count == 1)
					{
						list2.Add(list3[0] + "anull[outa]");
					}
					else
					{
						list2.Add(string.Join("", list3.ToArray()) + "amix=inputs=" + list3.Count + ":duration=longest:dropout_transition=0,atrim=duration=" + text2 + "[outa]");
					}
				}
			}
			else if (plan.AudioRegion >= 0 && plan.AudioRegion < sources.Length && array[plan.AudioRegion] >= 0 && Probe(ffmpeg, sources[plan.AudioRegion]).HasAudio)
			{
				flag = true;
				value = "outa";
				int num8 = ((plan.RegionVolumePercents != null && plan.AudioRegion < plan.RegionVolumePercents.Length) ? plan.RegionVolumePercents[plan.AudioRegion] : 100);
				list2.Add("[" + array[plan.AudioRegion] + ":a:0]volume=" + FfmpegNumber((double)num8 / 100.0) + ",atrim=duration=" + text2 + ",asetpts=PTS-STARTPTS,aresample=async=1:first_pts=0,apad=pad_dur=" + text2 + ",atrim=duration=" + text2 + "[outa]");
			}
			stringBuilder.Append("-filter_complex ").Append(QuoteArg(string.Join(";", list2.ToArray()))).Append(" -map [outv] ");
			if (flag)
			{
				stringBuilder.Append("-map [").Append(value).Append("] -c:a aac -b:a 192k ");
			}
			else
			{
				stringBuilder.Append("-an ");
			}
			string presetArg = isFastPreview ? "-preset ultrafast -crf 26" : "-preset fast -crf 17";
			stringBuilder.Append("-c:v libx264 " + presetArg + " -pix_fmt yuv420p -r 30 -metadata:s:v:0 rotate=0 -t ").Append(text2).Append(" -movflags +faststart -progress pipe:1 -nostats ")
				.Append(QuoteArg(outputPath));
			if (RunFfmpeg(ffmpeg, stringBuilder.ToString(), plan.DurationSeconds, progress, out error) != 0 || !File.Exists(outputPath))
			{
				error = "拼屏渲染失败：" + LastUsefulLines(error, 10);
				return false;
			}
			if (!isFastPreview && !ValidateExactDurationOutput(ffmpeg, outputPath, plan.DurationSeconds, out var reason))
			{
				error = reason;
				return false;
			}
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
		finally
		{
			try
			{
				if (Directory.Exists(text))
				{
					Directory.Delete(text, recursive: true);
				}
			}
			catch
			{
			}
		}
	}

	private static Rectangle ToSplitScreenPixelRectangle(RectangleF normalized, int canvasWidth, int canvasHeight, int borderWidth)
	{
		int num = (int)Math.Round(normalized.Left * (float)canvasWidth);
		int num2 = (int)Math.Round(normalized.Top * (float)canvasHeight);
		int num3 = (int)Math.Round(normalized.Right * (float)canvasWidth);
		int num4 = (int)Math.Round(normalized.Bottom * (float)canvasHeight);
		int num5 = borderWidth / 2;
		int num6 = borderWidth / 2;
		int num7 = borderWidth - num5;
		int num8 = borderWidth - num6;
		num += num5;
		num2 += num6;
		num3 -= num7;
		num4 -= num8;
		num = Math.Max(0, Math.Min(canvasWidth - 2, num));
		num2 = Math.Max(0, Math.Min(canvasHeight - 2, num2));
		int num9 = MakeEven(Math.Max(2, Math.Min(canvasWidth - num, num3 - num)));
		int num10 = MakeEven(Math.Max(2, Math.Min(canvasHeight - num2, num4 - num2)));
		if (num + num9 > canvasWidth)
		{
			num9 = MakeEven(canvasWidth - num);
		}
		if (num2 + num10 > canvasHeight)
		{
			num10 = MakeEven(canvasHeight - num2);
		}
		return new Rectangle(num, num2, Math.Max(2, num9), Math.Max(2, num10));
	}

	private static void CreateSplitScreenPipAssets(string folder, int index, int width, int height, string shape, int borderWidth, Color borderColor, out string maskPath, out string borderPath)
	{
		maskPath = Path.Combine(folder, "mask_" + index + ".png");
		borderPath = Path.Combine(folder, "border_" + index + ".png");
		using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
		{
			using Graphics graphics = Graphics.FromImage(bitmap);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.Clear(Color.Black);
			using (GraphicsPath path = CreateSplitScreenShapePath(new RectangleF(0f, 0f, width, height), shape))
			{
				graphics.FillPath(Brushes.White, path);
			}
			bitmap.Save(maskPath, ImageFormat.Png);
		}
		using Bitmap bitmap2 = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		using Graphics graphics2 = Graphics.FromImage(bitmap2);
		graphics2.SmoothingMode = SmoothingMode.AntiAlias;
		graphics2.Clear(Color.Transparent);
		if (borderWidth > 0 && borderColor.A > 0)
		{
			float num = Math.Max(1f, (float)borderWidth / 2f);
			RectangleF bounds = new RectangleF(num, num, Math.Max(2f, (float)width - num * 2f), Math.Max(2f, (float)height - num * 2f));
			using GraphicsPath path2 = CreateSplitScreenShapePath(bounds, shape);
			using Pen pen = new Pen(borderColor, Math.Max(1f, borderWidth));
			graphics2.DrawPath(pen, path2);
		}
		bitmap2.Save(borderPath, ImageFormat.Png);
	}

	private static string ColorToFfmpeg(Color color)
	{
		return "0x" + color.R.ToString("X2", CultureInfo.InvariantCulture) + color.G.ToString("X2", CultureInfo.InvariantCulture) + color.B.ToString("X2", CultureInfo.InvariantCulture);
	}

	private static string FindAvailableSplitScreenOutput(string folder, int index)
	{
		string text = "拼屏视频_" + index.ToString("000", CultureInfo.InvariantCulture);
		string text2 = Path.Combine(folder, text + ".mp4");
		int num = 2;
		while (File.Exists(text2))
		{
			text2 = Path.Combine(folder, text + "_" + num + ".mp4");
			num++;
		}
		return text2;
	}

	private TabPage BuildTextWatermarkLayer(int index)
	{
		TabPage tabPage = new TabPage("文字水印 " + (index + 1));
		tabPage.BackColor = Color.White;
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
		flowLayoutPanel.WrapContents = false;
		flowLayoutPanel.AutoScroll = true;
		flowLayoutPanel.Padding = new Padding(12, 6, 8, 4);
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		FlowLayoutPanel flowLayoutPanel3 = MakeWatermarkRow();
		_textWatermarkEnabled[index] = new CheckBox
		{
			AutoSize = true,
			Text = "启用文字 " + (index + 1),
			Margin = new Padding(0, 6, 14, 0)
		};
		flowLayoutPanel3.Controls.Add(_textWatermarkEnabled[index]);
		flowLayoutPanel3.Controls.Add(MakeFlowLabel("文字"));
		_watermarkText[index] = new TextBox
		{
			Width = 238,
			Text = ((index == 0) ? "我的视频" : ""),
			Margin = new Padding(4, 2, 10, 0)
		};
		flowLayoutPanel3.Controls.Add(_watermarkText[index]);
		flowLayoutPanel3.Controls.Add(MakeFlowLabel("字号"));
		_watermarkFontSize[index] = MakeNumber(36m, 4m, 2000m, 72);
		flowLayoutPanel3.Controls.Add(_watermarkFontSize[index]);
		flowLayoutPanel3.Controls.Add(MakeFlowLabel("字体"));
		_watermarkFontFamily[index] = new ComboBox
		{
			Width = 178,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Margin = new Padding(4, 2, 8, 0)
		};
		_watermarkFontFamily[index].Items.AddRange(new object[5] { "Noto Sans SC（开放字体）", "Noto Serif SC（开放字体）", "微软雅黑（系统字体）", "黑体（系统字体）", "宋体（系统字体）" });
		_watermarkFontFamily[index].SelectedIndex = 0;
		flowLayoutPanel3.Controls.Add(_watermarkFontFamily[index]);
		_watermarkFontBold[index] = new CheckBox
		{
			AutoSize = true,
			Text = "加粗",
			Checked = true,
			Margin = new Padding(0, 6, 8, 0)
		};
		_watermarkFontItalic[index] = new CheckBox
		{
			AutoSize = true,
			Text = "斜体",
			Margin = new Padding(0, 6, 8, 0)
		};
		flowLayoutPanel3.Controls.Add(_watermarkFontBold[index]);
		flowLayoutPanel3.Controls.Add(_watermarkFontItalic[index]);
		_watermarkColorButton[index] = MakeButton("文字颜色", 88);
		_watermarkColorButton[index].BackColor = _watermarkColor[index];
		flowLayoutPanel3.Controls.Add(_watermarkColorButton[index]);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel3);
		FlowLayoutPanel flowLayoutPanel4 = MakeWatermarkRow();
		flowLayoutPanel4.Controls.Add(MakeFlowLabel("透明度"));
		_textWatermarkOpacity[index] = MakeNumber(70m, 5m, 100m, 72);
		flowLayoutPanel4.Controls.Add(_textWatermarkOpacity[index]);
		flowLayoutPanel4.Controls.Add(MakeFlowLabel("%    位置"));
		_textWatermarkPosition[index] = MakePositionCombo();
		flowLayoutPanel4.Controls.Add(_textWatermarkPosition[index]);
		flowLayoutPanel4.Controls.Add(MakeFlowLabel("X 微调"));
		_textWatermarkOffsetX[index] = MakeNumber(0m, -9999m, 9999m, 72);
		flowLayoutPanel4.Controls.Add(_textWatermarkOffsetX[index]);
		flowLayoutPanel4.Controls.Add(MakeFlowLabel("Y 微调"));
		_textWatermarkOffsetY[index] = MakeNumber(0m, -9999m, 9999m, 72);
		flowLayoutPanel4.Controls.Add(_textWatermarkOffsetY[index]);
		_textWatermarkTopCenter[index] = MakeButton("顶部居中", 82);
		flowLayoutPanel4.Controls.Add(_textWatermarkTopCenter[index]);
		_textWatermarkCenter[index] = MakeButton("画面居中", 82);
		flowLayoutPanel4.Controls.Add(_textWatermarkCenter[index]);
		_textWatermarkBottomCenter[index] = MakeButton("底部居中", 82);
		flowLayoutPanel4.Controls.Add(_textWatermarkBottomCenter[index]);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel4);
		FlowLayoutPanel flowLayoutPanel5 = MakeWatermarkRow();
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("从"));
		_textWatermarkStart[index] = MakeTimeNumber(0m, 0m, 86400m, 82);
		flowLayoutPanel5.Controls.Add(_textWatermarkStart[index]);
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("秒开始"));
		_textWatermarkShowUntilEnd[index] = new CheckBox
		{
			AutoSize = true,
			Checked = true,
			Text = "显示到底",
			Margin = new Padding(12, 6, 10, 0)
		};
		flowLayoutPanel5.Controls.Add(_textWatermarkShowUntilEnd[index]);
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("到"));
		_textWatermarkEnd[index] = MakeTimeNumber(5m, 0m, 86400m, 82);
		flowLayoutPanel5.Controls.Add(_textWatermarkEnd[index]);
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("秒结束"));
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("文字范围"));
		_textWatermarkMaxWidth[index] = MakeNumber(85m, 20m, 100m, 64);
		flowLayoutPanel5.Controls.Add(_textWatermarkMaxWidth[index]);
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("%"));
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("安全边距"));
		_textWatermarkSafeMargin[index] = MakeNumber(3m, 0m, 20m, 58);
		flowLayoutPanel5.Controls.Add(_textWatermarkSafeMargin[index]);
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("%"));
		_textWatermarkAboveImages[index] = new CheckBox
		{
			AutoSize = true,
			Text = "置于图片水印上方",
			Margin = new Padding(10, 6, 0, 0)
		};
		flowLayoutPanel5.Controls.Add(_textWatermarkAboveImages[index]);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel5);
		FlowLayoutPanel flowLayoutPanel6 = MakeWatermarkRow();
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("入场方式"));
		_textWatermarkEntryEffect[index] = MakeEntryEffectCombo();
		flowLayoutPanel6.Controls.Add(_textWatermarkEntryEffect[index]);
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("时长"));
		_textWatermarkEntryDuration[index] = MakeTimeNumber(1m, 0.1m, 10m, 76);
		flowLayoutPanel6.Controls.Add(_textWatermarkEntryDuration[index]);
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("秒    退场方式"));
		_textWatermarkExitEffect[index] = MakeExitEffectCombo();
		flowLayoutPanel6.Controls.Add(_textWatermarkExitEffect[index]);
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("时长"));
		_textWatermarkExitDuration[index] = MakeTimeNumber(1m, 0.1m, 10m, 76);
		flowLayoutPanel6.Controls.Add(_textWatermarkExitDuration[index]);
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("秒"));
		_textWatermarkAllowOverflow[index] = new CheckBox
		{
			AutoSize = true,
			Text = "允许超出屏幕",
			Margin = new Padding(14, 6, 0, 0)
		};
		flowLayoutPanel6.Controls.Add(_textWatermarkAllowOverflow[index]);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel6);
		FlowLayoutPanel flowLayoutPanel7 = MakeWatermarkRow();
		_textWatermarkBackgroundEnabled[index] = new CheckBox
		{
			AutoSize = true,
			Text = "文字背景",
			Margin = new Padding(0, 6, 10, 0)
		};
		flowLayoutPanel7.Controls.Add(_textWatermarkBackgroundEnabled[index]);
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("样式"));
		_textWatermarkBackgroundStyle[index] = new ComboBox
		{
			Width = 132,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Margin = new Padding(4, 2, 8, 0)
		};
		_textWatermarkBackgroundStyle[index].Items.AddRange(new object[2] { "整块背景", "逐行包裹文字" });
		_textWatermarkBackgroundStyle[index].SelectedIndex = 0;
		flowLayoutPanel7.Controls.Add(_textWatermarkBackgroundStyle[index]);
		_textWatermarkBackgroundColorButton[index] = MakeButton("背景颜色", 84);
		_textWatermarkBackgroundColorButton[index].BackColor = _watermarkBackgroundColor[index];
		flowLayoutPanel7.Controls.Add(_textWatermarkBackgroundColorButton[index]);
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("透明度"));
		_textWatermarkBackgroundOpacity[index] = MakeNumber(55m, 0m, 100m, 66);
		flowLayoutPanel7.Controls.Add(_textWatermarkBackgroundOpacity[index]);
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("%  横向扩边"));
		_textWatermarkBackgroundPaddingX[index] = MakeNumber(16m, 0m, 200m, 64);
		flowLayoutPanel7.Controls.Add(_textWatermarkBackgroundPaddingX[index]);
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("纵向扩边"));
		_textWatermarkBackgroundPaddingY[index] = MakeNumber(8m, 0m, 100m, 64);
		flowLayoutPanel7.Controls.Add(_textWatermarkBackgroundPaddingY[index]);
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("圆角"));
		_textWatermarkBackgroundRadius[index] = MakeNumber(12m, 0m, 100m, 64);
		flowLayoutPanel7.Controls.Add(_textWatermarkBackgroundRadius[index]);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel7);
		FlowLayoutPanel flowLayoutPanel8 = MakeWatermarkRow();
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("停留动画"));
		_textWatermarkStayEffect[index] = MakeStayEffectCombo();
		flowLayoutPanel8.Controls.Add(_textWatermarkStayEffect[index]);
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("强度"));
		_textWatermarkStayIntensity[index] = MakeNumber(30m, 0m, 100m, 70);
		flowLayoutPanel8.Controls.Add(_textWatermarkStayIntensity[index]);
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("%    周期"));
		_textWatermarkStayPeriod[index] = MakeTimeNumber(2m, 0.2m, 20m, 76);
		flowLayoutPanel8.Controls.Add(_textWatermarkStayPeriod[index]);
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("秒    循环间歇"));
		_textWatermarkStayPause[index] = MakeTimeNumber(0m, 0m, 60m, 76);
		flowLayoutPanel8.Controls.Add(_textWatermarkStayPause[index]);
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("秒"));
		flowLayoutPanel2.Controls.Add(flowLayoutPanel8);
		FlowLayoutPanel flowLayoutPanel9 = MakeWatermarkRow();
		flowLayoutPanel9.Controls.Add(MakeFlowLabel("文字对齐"));
		_textWatermarkAlignment[index] = new ComboBox
		{
			Width = 92,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Margin = new Padding(4, 2, 8, 0)
		};
		_textWatermarkAlignment[index].Items.AddRange(new object[3] { "左对齐", "居中对齐", "右对齐" });
		_textWatermarkAlignment[index].SelectedIndex = 0;
		flowLayoutPanel9.Controls.Add(_textWatermarkAlignment[index]);
		_textWatermarkOutlineEnabled[index] = new CheckBox
		{
			AutoSize = true,
			Text = "文字描边",
			Margin = new Padding(2, 6, 6, 0)
		};
		flowLayoutPanel9.Controls.Add(_textWatermarkOutlineEnabled[index]);
		_textWatermarkOutlineColorButton[index] = MakeButton("描边颜色", 84);
		_textWatermarkOutlineColorButton[index].BackColor = _watermarkOutlineColor[index];
		_textWatermarkOutlineColorButton[index].ForeColor = Color.White;
		flowLayoutPanel9.Controls.Add(_textWatermarkOutlineColorButton[index]);
		flowLayoutPanel9.Controls.Add(MakeFlowLabel("粗细"));
		_textWatermarkOutlineWidth[index] = MakeNumber(2m, 1m, 20m, 54);
		flowLayoutPanel9.Controls.Add(_textWatermarkOutlineWidth[index]);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel9);
		Label label = new Label();
		label.AutoSize = true;
		label.Text = "文字可居中对齐并描边；背景可选整块或逐行包裹。未勾选置顶时文字显示在图片水印下方。";
		label.ForeColor = Color.FromArgb(105, 114, 128);
		label.Margin = new Padding(2, 3, 0, 0);
		Label value = label;
		flowLayoutPanel2.Controls.Add(value);
		tabPage.Controls.Add(flowLayoutPanel2);
		return tabPage;
	}

	private TabPage BuildImageWatermarkLayer(int index)
	{
		TabPage tabPage = new TabPage("图片水印 " + (index + 1));
		tabPage.BackColor = Color.White;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 2;
		tableLayoutPanel.RowCount = 1;
		tableLayoutPanel.Margin = new Padding(0);
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tabPage.Controls.Add(tableLayoutPanel2);
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.Padding = new Padding(8);
		panel.AutoScroll = true;
		panel.BackColor = Color.FromArgb(248, 249, 251);
		Panel panel2 = panel;
		_imageWatermarkEnabled[index] = new CheckBox
		{
			AutoSize = true,
			Text = "启用图片库 " + (index + 1),
			Location = new Point(10, 9)
		};
		panel2.Controls.Add(_imageWatermarkEnabled[index]);
		_imageWatermarkList[index] = new ListBox
		{
			Location = new Point(10, 36),
			Size = new Size(230, 100),
			HorizontalScrollbar = true
		};
		panel2.Controls.Add(_imageWatermarkList[index]);
		_imageWatermarkBrowse[index] = MakeButton("批量导入…", 96);
		_imageWatermarkBrowse[index].Location = new Point(10, 143);
		panel2.Controls.Add(_imageWatermarkBrowse[index]);
		_imageWatermarkRemove[index] = MakeButton("移除", 58);
		_imageWatermarkRemove[index].Location = new Point(112, 143);
		panel2.Controls.Add(_imageWatermarkRemove[index]);
		_imageWatermarkClear[index] = MakeButton("清空", 58);
		_imageWatermarkClear[index].Location = new Point(176, 143);
		panel2.Controls.Add(_imageWatermarkClear[index]);
		panel2.Controls.Add(MakeLabel("参与候选数量", 10, 181));
		_imageWatermarkRandomCount[index] = new NumericUpDown
		{
			Location = new Point(112, 177),
			Width = 72,
			Minimum = 1m,
			Maximum = 1m,
			Value = 1m
		};
		panel2.Controls.Add(_imageWatermarkRandomCount[index]);
		panel2.Controls.Add(MakeLabel("参与方式", 10, 211));
		_imageWatermarkAssignmentMode[index] = new ComboBox
		{
			Location = new Point(82, 206),
			Width = 142,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_imageWatermarkAssignmentMode[index].Items.AddRange(new object[2] { "随机参与", "按列表顺序参与" });
		_imageWatermarkAssignmentMode[index].SelectedIndex = 0;
		panel2.Controls.Add(_imageWatermarkAssignmentMode[index]);
		tableLayoutPanel2.Controls.Add(panel2, 0, 0);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
		flowLayoutPanel.WrapContents = false;
		flowLayoutPanel.AutoScroll = true;
		flowLayoutPanel.Padding = new Padding(12, 5, 8, 3);
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		FlowLayoutPanel flowLayoutPanel3 = MakeWatermarkRow();
		flowLayoutPanel3.Controls.Add(MakeFlowLabel("当前图片"));
		_imageWatermarkPath[index] = new TextBox
		{
			Width = 570,
			ReadOnly = true,
			Margin = new Padding(4, 2, 8, 0)
		};
		flowLayoutPanel3.Controls.Add(_imageWatermarkPath[index]);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel3);
		FlowLayoutPanel flowLayoutPanel4 = MakeWatermarkRow();
		flowLayoutPanel4.Controls.Add(MakeFlowLabel("缩放宽度"));
		_imageWatermarkScale[index] = MakeNumber(20m, 3m, 100m, 70);
		flowLayoutPanel4.Controls.Add(_imageWatermarkScale[index]);
		flowLayoutPanel4.Controls.Add(MakeFlowLabel("%    透明度"));
		_imageWatermarkOpacity[index] = MakeNumber(70m, 5m, 100m, 70);
		flowLayoutPanel4.Controls.Add(_imageWatermarkOpacity[index]);
		flowLayoutPanel4.Controls.Add(MakeFlowLabel("%    位置"));
		_imageWatermarkPosition[index] = MakePositionCombo();
		flowLayoutPanel4.Controls.Add(_imageWatermarkPosition[index]);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel4);
		FlowLayoutPanel flowLayoutPanel5 = MakeWatermarkRow();
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("X 微调"));
		_imageWatermarkOffsetX[index] = MakeNumber(0m, -9999m, 9999m, 68);
		flowLayoutPanel5.Controls.Add(_imageWatermarkOffsetX[index]);
		flowLayoutPanel5.Controls.Add(MakeFlowLabel("Y 微调"));
		_imageWatermarkOffsetY[index] = MakeNumber(0m, -9999m, 9999m, 68);
		flowLayoutPanel5.Controls.Add(_imageWatermarkOffsetY[index]);
		_imageWatermarkCenter[index] = MakeButton("一键居中", 88);
		flowLayoutPanel5.Controls.Add(_imageWatermarkCenter[index]);
		_imageWatermarkAllowOverflow[index] = new CheckBox
		{
			AutoSize = true,
			Text = "允许超出屏幕",
			Margin = new Padding(8, 6, 8, 0)
		};
		flowLayoutPanel5.Controls.Add(_imageWatermarkAllowOverflow[index]);
		_imageWatermarkApplyAll[index] = MakeButton("当前设置应用到全部", 152);
		flowLayoutPanel5.Controls.Add(_imageWatermarkApplyAll[index]);
		flowLayoutPanel2.Controls.Add(flowLayoutPanel5);
		FlowLayoutPanel flowLayoutPanel6 = MakeWatermarkRow();
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("从"));
		_imageWatermarkStart[index] = MakeTimeNumber(0m, 0m, 86400m, 82);
		flowLayoutPanel6.Controls.Add(_imageWatermarkStart[index]);
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("秒开始"));
		_imageWatermarkShowUntilEnd[index] = new CheckBox
		{
			AutoSize = true,
			Checked = true,
			Text = "显示到底",
			Margin = new Padding(12, 6, 10, 0)
		};
		flowLayoutPanel6.Controls.Add(_imageWatermarkShowUntilEnd[index]);
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("到"));
		_imageWatermarkEnd[index] = MakeTimeNumber(5m, 0m, 86400m, 82);
		flowLayoutPanel6.Controls.Add(_imageWatermarkEnd[index]);
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("秒结束"));
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("本图停留"));
		_imageWatermarkSlideDuration[index] = MakeTimeNumber(3m, 0.5m, 86400m, 76);
		flowLayoutPanel6.Controls.Add(_imageWatermarkSlideDuration[index]);
		flowLayoutPanel6.Controls.Add(MakeFlowLabel("秒"));
		flowLayoutPanel2.Controls.Add(flowLayoutPanel6);
		FlowLayoutPanel flowLayoutPanel7 = MakeWatermarkRow();
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("入场方式"));
		_imageWatermarkEntryEffect[index] = MakeEntryEffectCombo();
		flowLayoutPanel7.Controls.Add(_imageWatermarkEntryEffect[index]);
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("时长"));
		_imageWatermarkEntryDuration[index] = MakeTimeNumber(1m, 0.1m, 10m, 76);
		flowLayoutPanel7.Controls.Add(_imageWatermarkEntryDuration[index]);
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("秒    退场方式"));
		_imageWatermarkExitEffect[index] = MakeExitEffectCombo();
		flowLayoutPanel7.Controls.Add(_imageWatermarkExitEffect[index]);
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("时长"));
		_imageWatermarkExitDuration[index] = MakeTimeNumber(1m, 0.1m, 10m, 76);
		flowLayoutPanel7.Controls.Add(_imageWatermarkExitDuration[index]);
		flowLayoutPanel7.Controls.Add(MakeFlowLabel("秒"));
		flowLayoutPanel2.Controls.Add(flowLayoutPanel7);
		FlowLayoutPanel flowLayoutPanel8 = MakeWatermarkRow();
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("停留动画"));
		_imageWatermarkStayEffect[index] = MakeStayEffectCombo();
		flowLayoutPanel8.Controls.Add(_imageWatermarkStayEffect[index]);
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("强度"));
		_imageWatermarkStayIntensity[index] = MakeNumber(30m, 0m, 100m, 70);
		flowLayoutPanel8.Controls.Add(_imageWatermarkStayIntensity[index]);
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("%    周期"));
		_imageWatermarkStayPeriod[index] = MakeTimeNumber(2m, 0.2m, 20m, 76);
		flowLayoutPanel8.Controls.Add(_imageWatermarkStayPeriod[index]);
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("秒    循环间歇"));
		_imageWatermarkStayPause[index] = MakeTimeNumber(0m, 0m, 60m, 76);
		flowLayoutPanel8.Controls.Add(_imageWatermarkStayPause[index]);
		flowLayoutPanel8.Controls.Add(MakeFlowLabel("秒"));
		flowLayoutPanel2.Controls.Add(flowLayoutPanel8);
		FlowLayoutPanel flowLayoutPanel9 = MakeWatermarkRow();
		flowLayoutPanel9.Controls.Add(MakeFlowLabel("图片库播放"));
		_imageWatermarkPlaybackMode[index] = new ComboBox
		{
			Width = 126,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Margin = new Padding(4, 2, 10, 0)
		};
		_imageWatermarkPlaybackMode[index].Items.AddRange(new object[2] { "单张参与", "多张轮播" });
		_imageWatermarkPlaybackMode[index].SelectedIndex = 0;
		flowLayoutPanel9.Controls.Add(_imageWatermarkPlaybackMode[index]);
		flowLayoutPanel9.Controls.Add(MakeFlowLabel("换图效果"));
		_imageWatermarkSwitchEffect[index] = new ComboBox
		{
			Width = 126,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Margin = new Padding(4, 2, 10, 0)
		};
		_imageWatermarkSwitchEffect[index].Items.AddRange(new object[4] { "交叉淡化", "柔和缩放", "滑动切换", "直接切换" });
		_imageWatermarkSwitchEffect[index].SelectedIndex = 0;
		flowLayoutPanel9.Controls.Add(_imageWatermarkSwitchEffect[index]);
		flowLayoutPanel9.Controls.Add(MakeFlowLabel("过渡"));
		_imageWatermarkSwitchDuration[index] = MakeTimeNumber(0.6m, 0.1m, 5m, 76);
		_imageWatermarkSwitchDuration[index].Increment = 0.1m;
		flowLayoutPanel9.Controls.Add(_imageWatermarkSwitchDuration[index]);
		flowLayoutPanel9.Controls.Add(MakeFlowLabel("秒    换图间隔"));
		_imageWatermarkSwitchInterval[index] = MakeTimeNumber(0m, 0m, 60m, 76);
		_imageWatermarkSwitchInterval[index].Increment = 0.1m;
		flowLayoutPanel9.Controls.Add(_imageWatermarkSwitchInterval[index]);
		flowLayoutPanel9.Controls.Add(MakeFlowLabel("秒"));
		flowLayoutPanel2.Controls.Add(flowLayoutPanel9);
		Label label = new Label();
		label.AutoSize = true;
		label.Text = "轮播按左侧数量/方式循环；入场和退场控制整组首尾，起止时间以列表第一张为准。";
		label.ForeColor = Color.FromArgb(105, 114, 128);
		label.Margin = new Padding(2, 2, 0, 0);
		Label value = label;
		flowLayoutPanel2.Controls.Add(value);
		tableLayoutPanel2.Controls.Add(flowLayoutPanel2, 1, 0);
		return tabPage;
	}

	private FlowLayoutPanel MakeWatermarkRow()
	{
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Width = 960;
		flowLayoutPanel.Height = 36;
		flowLayoutPanel.WrapContents = false;
		flowLayoutPanel.Margin = new Padding(0, 0, 0, 1);
		return flowLayoutPanel;
	}

	private Label MakeFlowLabel(string text)
	{
		Label label = new Label();
		label.AutoSize = true;
		label.Text = text;
		label.Margin = new Padding(0, 7, 4, 0);
		return label;
	}

	private NumericUpDown MakeNumber(decimal value, decimal minimum, decimal maximum, int width)
	{
		NumericUpDown numericUpDown = new NumericUpDown();
		numericUpDown.Value = value;
		numericUpDown.Minimum = minimum;
		numericUpDown.Maximum = maximum;
		numericUpDown.Width = width;
		numericUpDown.Margin = new Padding(4, 2, 10, 0);
		return numericUpDown;
	}

	private NumericUpDown MakeTimeNumber(decimal value, decimal minimum, decimal maximum, int width)
	{
		NumericUpDown numericUpDown = new NumericUpDown();
		numericUpDown.Value = value;
		numericUpDown.Minimum = minimum;
		numericUpDown.Maximum = maximum;
		numericUpDown.DecimalPlaces = 1;
		numericUpDown.Increment = 0.5m;
		numericUpDown.Width = width;
		numericUpDown.Margin = new Padding(4, 2, 10, 0);
		return numericUpDown;
	}

	private ComboBox MakeEntryEffectCombo()
	{
		ComboBox comboBox = new ComboBox();
		comboBox.Width = 148;
		comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		comboBox.Margin = new Padding(4, 2, 10, 0);
		ComboBox comboBox2 = comboBox;
		comboBox2.Items.AddRange(new object[10] { "直接出现", "淡入", "放大进入", "从左滑入", "从上滑入", "从右滑入", "从下浮入", "底部弹出", "弹跳进入", "旋转进入" });
		comboBox2.SelectedIndex = 1;
		return comboBox2;
	}

	private ComboBox MakeStayEffectCombo()
	{
		ComboBox comboBox = new ComboBox();
		comboBox.Width = 148;
		comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		comboBox.Margin = new Padding(4, 2, 10, 0);
		ComboBox comboBox2 = comboBox;
		comboBox2.Items.AddRange(new object[10] { "无", "轻微漂浮", "呼吸缩放", "轻柔摇摆", "持续旋转", "律动弹跳", "轻微抖动", "闪烁", "金色斜向扫光", "星光粒子" });
		comboBox2.SelectedIndex = 0;
		return comboBox2;
	}

	private ComboBox MakeExitEffectCombo()
	{
		ComboBox comboBox = new ComboBox();
		comboBox.Width = 138;
		comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		comboBox.Margin = new Padding(4, 2, 10, 0);
		ComboBox comboBox2 = comboBox;
		comboBox2.Items.AddRange(new object[5] { "直接消失", "淡出", "缩小退出", "向右滑出", "向下滑出" });
		comboBox2.SelectedIndex = 1;
		return comboBox2;
	}

	private ComboBox MakePositionCombo()
	{
		ComboBox comboBox = new ComboBox();
		comboBox.Width = 116;
		comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		comboBox.Margin = new Padding(4, 2, 10, 0);
		ComboBox comboBox2 = comboBox;
		comboBox2.Items.AddRange(new object[9] { "右下角", "左下角", "右上角", "左上角", "画面中央", "顶部居中", "底部居中", "左侧居中", "右侧居中" });
		comboBox2.SelectedIndex = 0;
		return comboBox2;
	}

	private Panel MakeListHost()
	{
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.Margin = new Padding(0);
		panel.Padding = new Padding(14, 10, 14, 12);
		panel.BackColor = CanvasColor;
		return panel;
	}

	private ListView MakeVideoList()
	{
		ListView list = new ListView
		{
			Dock = DockStyle.Fill,
			View = View.Details,
			FullRowSelect = true,
			GridLines = false,
			HideSelection = false,
			AllowDrop = true,
			OwnerDraw = true,
			BorderStyle = BorderStyle.FixedSingle,
			BackColor = SurfaceColor,
			ForeColor = InkColor
		};
		list.SmallImageList = new ImageList
		{
			ImageSize = new Size(1, 33),
			ColorDepth = ColorDepth.Depth32Bit
		};
		list.Columns.Add("顺序", 52, HorizontalAlignment.Center);
		list.Columns.Add("文件名", 280, HorizontalAlignment.Left);
		list.Columns.Add("所在文件夹", 280, HorizontalAlignment.Left);
		list.Columns.Add("水平翻转", 76, HorizontalAlignment.Center);
		list.Columns.Add("缩放", 68, HorizontalAlignment.Center);
		list.Columns.Add("速率", 68, HorizontalAlignment.Center);
		list.Columns.Add("音量", 68, HorizontalAlignment.Center);
		list.Columns.Add("倒放", 62, HorizontalAlignment.Center);
		list.Columns.Add("横屏裁中", 78, HorizontalAlignment.Center);
		list.Columns.Add("重置", 58, HorizontalAlignment.Center);
		list.DrawColumnHeader += DrawVideoListColumnHeader;
		list.DrawItem += delegate(object sender, DrawListViewItemEventArgs e)
		{
			if (((ListView)sender).View != View.Details)
			{
				e.DrawDefault = true;
			}
		};
		Label label = new Label();
		label.Name = "VideoListEmptyHint";
		label.AutoSize = true;
		label.Text = "可拖可双击添加视频";
		label.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);
		label.ForeColor = Color.FromArgb(148, 163, 184);
		label.BackColor = SurfaceColor;
		label.Enabled = false;
		Label value = label;
		list.Controls.Add(value);
		list.Resize += delegate
		{
			PositionVideoListEmptyHint(list);
		};
		list.HandleCreated += delegate
		{
			PositionVideoListEmptyHint(list);
		};
		PositionVideoListEmptyHint(list);
		return list;
	}

	private static void PositionVideoListEmptyHint(ListView list)
	{
		if (list != null && list.Controls["VideoListEmptyHint"] is Label label)
		{
			label.Visible = list.Items.Count == 0;
			label.Location = new Point(Math.Max(12, (list.ClientSize.Width - label.PreferredWidth) / 2), Math.Max(36, (list.ClientSize.Height - label.PreferredHeight) / 2));
			if (label.Visible)
			{
				label.BringToFront();
			}
		}
	}

	private VideoAdjustmentEditor MakeVideoAdjustmentEditor()
	{
		VideoAdjustmentEditor videoAdjustmentEditor = new VideoAdjustmentEditor();
		videoAdjustmentEditor.Root = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.White,
			Padding = new Padding(10, 7, 8, 4),
			Margin = new Padding(0, 7, 0, 0)
		};
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.WrapContents = false;
		flowLayoutPanel.AutoScroll = true;
		flowLayoutPanel.Margin = new Padding(0);
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		Label label = new Label();
		label.AutoSize = true;
		label.Text = "所选视频调整";
		label.Font = new Font(Font, FontStyle.Bold);
		label.Margin = new Padding(0, 8, 12, 0);
		Label value = label;
		videoAdjustmentEditor.HorizontalFlip = new CheckBox
		{
			AutoSize = true,
			Text = "水平翻转",
			Margin = new Padding(0, 7, 12, 0)
		};
		videoAdjustmentEditor.ReversePlayback = new CheckBox
		{
			AutoSize = true,
			Text = "倒放",
			Margin = new Padding(0, 7, 12, 0)
		};
		videoAdjustmentEditor.CenterCropPortrait = new CheckBox
		{
			AutoSize = true,
			Text = "横屏裁中间(9:16)",
			Checked = true,
			Margin = new Padding(0, 7, 12, 0)
		};
		videoAdjustmentEditor.ScaleRatio = MakeAdjustmentNumber(1m, 0.1m, 3m, 0.1m, 1);
		videoAdjustmentEditor.SpeedRatio = MakeAdjustmentNumber(1m, 0.1m, 3m, 0.1m, 1);
		videoAdjustmentEditor.VolumePercent = MakeAdjustmentNumber(100m, 0m, 300m, 5m, 0);
		videoAdjustmentEditor.ApplySelected = MakeButton("应用到所选", 96);
		videoAdjustmentEditor.ApplySelected.Height = 30;
		videoAdjustmentEditor.ApplySelected.Margin = new Padding(8, 1, 8, 0);
		videoAdjustmentEditor.ApplyAll = MakeButton("应用到全部", 96);
		videoAdjustmentEditor.ApplyAll.Height = 30;
		videoAdjustmentEditor.ApplyAll.Margin = new Padding(0, 1, 8, 0);
		videoAdjustmentEditor.CopyAdjustment = MakeButton("复制参数", 82);
		videoAdjustmentEditor.CopyAdjustment.Height = 30;
		videoAdjustmentEditor.CopyAdjustment.Margin = new Padding(0, 1, 6, 0);
		videoAdjustmentEditor.PasteAdjustment = MakeButton("粘贴参数", 82);
		videoAdjustmentEditor.PasteAdjustment.Height = 30;
		videoAdjustmentEditor.PasteAdjustment.Margin = new Padding(0, 1, 0, 0);
		flowLayoutPanel2.Controls.Add(value);
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.HorizontalFlip);
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.ReversePlayback);
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.CenterCropPortrait);
		flowLayoutPanel2.Controls.Add(MakeInlineAdjustmentLabel("缩放"));
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.ScaleRatio);
		flowLayoutPanel2.Controls.Add(MakeInlineAdjustmentLabel("倍"));
		flowLayoutPanel2.Controls.Add(MakeInlineAdjustmentLabel("速率"));
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.SpeedRatio);
		flowLayoutPanel2.Controls.Add(MakeInlineAdjustmentLabel("倍"));
		flowLayoutPanel2.Controls.Add(MakeInlineAdjustmentLabel("音量"));
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.VolumePercent);
		flowLayoutPanel2.Controls.Add(MakeInlineAdjustmentLabel("%"));
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.ApplySelected);
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.ApplyAll);
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.CopyAdjustment);
		flowLayoutPanel2.Controls.Add(videoAdjustmentEditor.PasteAdjustment);
		videoAdjustmentEditor.Root.Controls.Add(flowLayoutPanel2);
		return videoAdjustmentEditor;
	}

	private OutputFrameControls InstallOutputFrameControls(Control host, int x, int y)
	{
		OutputFrameControls controls = new OutputFrameControls();
		host.Controls.Add(MakeLabel("输出画幅", x, y + 5));
		controls.AspectMode = new ComboBox
		{
			Location = new Point(x + 70, y),
			Width = 220,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		controls.AspectMode.Items.AddRange(new object[3] { "自动（沿用每行设置）", "9:16 竖屏统一导出", "16:9 横屏统一导出" });
		controls.AspectMode.SelectedIndex = 0;
		host.Controls.Add(controls.AspectMode);
		host.Controls.Add(MakeLabel("截取位置", x + 308, y + 5));
		controls.CropAnchor = new ComboBox
		{
			Location = new Point(x + 378, y),
			Width = 142,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		controls.CropAnchor.SelectedIndex = -1;
		host.Controls.Add(controls.CropAnchor);
		controls.CustomLabel = MakeLabel("自定义", x + 532, y + 5);
		host.Controls.Add(controls.CustomLabel);
		controls.CustomPositionPercent = new NumericUpDown
		{
			Location = new Point(x + 588, y),
			Width = 66,
			Minimum = 0m,
			Maximum = 100m,
			Value = 50m,
			Increment = 1m,
			TextAlign = HorizontalAlignment.Center
		};
		host.Controls.Add(controls.CustomPositionPercent);
		controls.Hint = MakeLabel("%", x + 659, y + 5);
		controls.Hint.ForeColor = MutedColor;
		host.Controls.Add(controls.Hint);
		controls.AspectMode.SelectedIndexChanged += delegate
		{
			UpdateOutputFrameControls(controls, rebuildAnchorItems: true);
		};
		controls.CropAnchor.SelectedIndexChanged += delegate
		{
			UpdateOutputFrameControls(controls, rebuildAnchorItems: false);
		};
		UpdateOutputFrameControls(controls, rebuildAnchorItems: true);
		return controls;
	}

	private void UpdateOutputFrameControls(OutputFrameControls controls, bool rebuildAnchorItems)
	{
		if (controls == null || controls.AspectMode == null)
		{
			return;
		}
		int num = Math.Max(0, controls.AspectMode.SelectedIndex);
		if (rebuildAnchorItems)
		{
			int selectedIndex = controls.CropAnchor.SelectedIndex;
			controls.CropAnchor.BeginUpdate();
			controls.CropAnchor.Items.Clear();
			switch (num)
			{
			case 2:
				controls.CropAnchor.Items.AddRange(new object[4] { "中间部分", "上三分", "下三分", "自定义位置" });
				break;
			case 1:
				controls.CropAnchor.Items.AddRange(new object[4] { "中间部分", "左三分", "右三分", "自定义位置" });
				break;
			default:
				controls.CropAnchor.Items.AddRange(new object[4] { "中间部分", "前侧区域", "后侧区域", "自定义位置" });
				break;
			}
			controls.CropAnchor.EndUpdate();
			controls.CropAnchor.SelectedIndex = ((selectedIndex >= 0 && selectedIndex < 4) ? selectedIndex : 0);
		}
		bool flag = num == 1 || num == 2;
		bool flag2 = flag && controls.CropAnchor.SelectedIndex == 3;
		controls.CropAnchor.Enabled = flag && !_isRunning;
		controls.CustomLabel.Visible = flag2;
		controls.CustomPositionPercent.Visible = flag2;
		controls.Hint.Visible = flag2;
		controls.CustomPositionPercent.Enabled = flag2 && !_isRunning;
		if (flag2)
		{
			controls.Hint.Text = ((num == 2) ? "%（0上/100下）" : "%（0左/100右）");
		}
	}

	private static OutputFrameSettings CaptureOutputFrameSettings(OutputFrameControls controls)
	{
		if (controls == null)
		{
			return new OutputFrameSettings();
		}
		OutputFrameSettings outputFrameSettings = new OutputFrameSettings();
		outputFrameSettings.AspectMode = Math.Max(0, controls.AspectMode.SelectedIndex);
		outputFrameSettings.CropAnchor = Math.Max(0, controls.CropAnchor.SelectedIndex);
		outputFrameSettings.CustomPositionPercent = decimal.ToInt32(controls.CustomPositionPercent.Value);
		return outputFrameSettings;
	}

	private static int ResolveCropPositionPercent(OutputFrameSettings settings)
	{
		settings = settings ?? new OutputFrameSettings();
		if (settings.CropAnchor == 1)
		{
			return 0;
		}
		if (settings.CropAnchor == 2)
		{
			return 100;
		}
		if (settings.CropAnchor == 3)
		{
			return Math.Max(0, Math.Min(100, settings.CustomPositionPercent));
		}
		return 50;
	}

	private static NumericUpDown MakeAdjustmentNumber(decimal value, decimal minimum, decimal maximum, decimal increment, int decimalPlaces)
	{
		NumericUpDown numericUpDown = new NumericUpDown();
		numericUpDown.Value = value;
		numericUpDown.Minimum = minimum;
		numericUpDown.Maximum = maximum;
		numericUpDown.Increment = increment;
		numericUpDown.DecimalPlaces = decimalPlaces;
		numericUpDown.Width = ((decimalPlaces > 0) ? 58 : 66);
		numericUpDown.Margin = new Padding(0, 3, 2, 0);
		return numericUpDown;
	}

	private static Label MakeInlineAdjustmentLabel(string text)
	{
		Label label = new Label();
		label.AutoSize = true;
		label.Text = text;
		label.Margin = new Padding(4, 8, 4, 0);
		return label;
	}

	private static void InstallVideoListArea(Panel host, ListView list, VideoAdjustmentEditor editor)
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 2;
		tableLayoutPanel.Margin = new Padding(0);
		tableLayoutPanel.Padding = new Padding(0);
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f));
		list.Margin = new Padding(0);
		tableLayoutPanel2.Controls.Add(list, 0, 0);
		tableLayoutPanel2.Controls.Add(editor.Root, 0, 1);
		host.Controls.Add(tableLayoutPanel2);
	}

	private Label MakeCountLabel()
	{
		Label label = new Label();
		label.AutoSize = true;
		label.ForeColor = Color.FromArgb(90, 99, 112);
		label.Margin = new Padding(14, 9, 0, 0);
		return label;
	}

	private BgmControls InstallBgmControls(Control host, int x, int y)
	{
		BgmControls controls = new BgmControls();
		Label label = MakeLabel("BGM 配乐", x, y + 5);
		label.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		host.Controls.Add(label);
		controls.Add = MakeButton("添加音乐…", 86);
		controls.Add.Location = new Point(x + 72, y);
		host.Controls.Add(controls.Add);
		controls.Clear = MakeButton("清空", 56);
		controls.Clear.Location = new Point(x + 164, y);
		host.Controls.Add(controls.Clear);
		controls.CountLabel = MakeLabel("0 首", x + 228, y + 6);
		controls.CountLabel.ForeColor = MutedColor;
		host.Controls.Add(controls.CountLabel);
		Label value = MakeLabel("分配", x + 286, y + 6);
		host.Controls.Add(value);
		controls.AssignmentMode = new ComboBox
		{
			Location = new Point(x + 326, y + 1),
			Width = 112,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		controls.AssignmentMode.Items.AddRange(new object[2] { "随机对应", "按顺序循环" });
		controls.AssignmentMode.SelectedIndex = 0;
		host.Controls.Add(controls.AssignmentMode);
		Label value2 = MakeLabel("音量", x + 452, y + 6);
		host.Controls.Add(value2);
		controls.VolumePercent = new NumericUpDown
		{
			Location = new Point(x + 492, y + 1),
			Width = 66,
			Minimum = 0m,
			Maximum = 100m,
			Value = 30m
		};
		host.Controls.Add(controls.VolumePercent);
		host.Controls.Add(MakeLabel("%（与原声混合；多首可随机或顺序对应）", x + 564, y + 6));
		controls.Add.Click += delegate
		{
			ChooseBgmFiles(controls);
		};
		controls.Clear.Click += delegate
		{
			if (!_isRunning)
			{
				controls.Files.Clear();
				UpdateBgmCount(controls);
			}
		};
		return controls;
	}

	private void ChooseBgmFiles(BgmControls controls)
	{
		if (_isRunning)
		{
			return;
		}
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Title = "选择一首或多首 BGM 配乐";
		openFileDialog.Filter = "音频文件|*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.wma|所有文件|*.*";
		openFileDialog.Multiselect = true;
		if (openFileDialog.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		string[] fileNames = openFileDialog.FileNames;
		foreach (string path in fileNames)
		{
			string full = Path.GetFullPath(path);
			if (!controls.Files.Any((string x) => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)))
			{
				controls.Files.Add(full);
			}
		}
		UpdateBgmCount(controls);
	}

	private static void UpdateBgmCount(BgmControls controls)
	{
		controls.CountLabel.Text = controls.Files.Count + " 首";
		controls.Clear.Enabled = controls.Files.Count > 0;
	}

	private static BgmPlan CaptureBgmPlan(BgmControls controls)
	{
		BgmPlan bgmPlan = new BgmPlan();
		bgmPlan.RandomAssignment = controls.AssignmentMode.SelectedIndex == 0;
		bgmPlan.VolumePercent = decimal.ToInt32(controls.VolumePercent.Value);
		BgmPlan bgmPlan2 = bgmPlan;
		bgmPlan2.Files.AddRange(controls.Files.Where(File.Exists));
		return bgmPlan2;
	}

	private static string SelectBgm(BgmPlan plan, int outputIndex, Random random)
	{
		if (plan == null || !plan.Enabled)
		{
			return null;
		}
		if (!plan.RandomAssignment)
		{
			return plan.Files[Math.Abs(outputIndex) % plan.Files.Count];
		}
		return plan.Files[random.Next(plan.Files.Count)];
	}

	private Label MakeLabel(string text, int x, int y)
	{
		Label label = new Label();
		label.AutoSize = true;
		label.Location = new Point(x, y);
		label.Text = text;
		return label;
	}

	private Button MakePrimaryButton(string text, int x, int y, int width)
	{
		Button button = MakeButton(text, width);
		button.Location = new Point(x, y);
		button.Height = 42;
		button.BackColor = Color.FromArgb(34, 92, 190);
		button.ForeColor = Color.White;
		button.FlatAppearance.BorderSize = 0;
		return button;
	}

	private Button MakeButton(string text, int width)
	{
		Button button = new Button();
		button.Text = text;
		button.Width = width;
		button.Height = 34;
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderColor = Color.FromArgb(205, 211, 220);
		button.BackColor = Color.White;
		button.Margin = new Padding(0, 0, 8, 0);
		button.Cursor = Cursors.Hand;
		return button;
	}

	private void ApplyModernVisualStyle(Control root)
	{
		foreach (Control control in root.Controls)
		{
			Button button = control as Button;
			if (button != null)
			{
				bool primary = button.ForeColor == Color.White || button.Text.StartsWith("开始", StringComparison.Ordinal) || button.Text.StartsWith("直接加水印", StringComparison.Ordinal);
				bool globalReset = button.Text == "重置全部设置";
				button.FlatStyle = FlatStyle.Flat;
				button.FlatAppearance.BorderSize = ((!primary) ? 1 : 0);
				button.FlatAppearance.BorderColor = (globalReset ? Color.FromArgb(251, 146, 60) : BorderColor);
				button.BackColor = (primary ? AccentColor : (globalReset ? Color.FromArgb(255, 247, 237) : Color.FromArgb(248, 250, 252)));
				button.ForeColor = (primary ? Color.White : (globalReset ? Color.FromArgb(154, 52, 18) : InkColor));
				button.Font = new Font("Microsoft YaHei UI", 9f, primary ? FontStyle.Bold : FontStyle.Regular);
				Color normal = button.BackColor;
				button.MouseEnter += delegate
				{
					if (button.Enabled)
					{
						button.BackColor = (primary ? AccentHoverColor : (globalReset ? Color.FromArgb(255, 237, 213) : Color.FromArgb(238, 243, 249)));
					}
				};
				button.MouseLeave += delegate
				{
					button.BackColor = normal;
				};
			}
			else if (control is TextBox)
			{
				TextBox textBox = (TextBox)control;
				textBox.BorderStyle = BorderStyle.FixedSingle;
				textBox.BackColor = SurfaceColor;
				textBox.ForeColor = InkColor;
			}
			else if (control is ComboBox)
			{
				ComboBox comboBox = (ComboBox)control;
				comboBox.FlatStyle = FlatStyle.Flat;
				comboBox.BackColor = SurfaceColor;
				comboBox.ForeColor = InkColor;
			}
			else if (control is NumericUpDown)
			{
				NumericUpDown numericUpDown = (NumericUpDown)control;
				numericUpDown.BorderStyle = BorderStyle.FixedSingle;
				numericUpDown.BackColor = SurfaceColor;
				numericUpDown.ForeColor = InkColor;
			}
			else if (control is CheckedListBox)
			{
				((CheckedListBox)control).BorderStyle = BorderStyle.FixedSingle;
				control.BackColor = SurfaceColor;
				control.ForeColor = InkColor;
			}
			else if (control is ListBox)
			{
				((ListBox)control).BorderStyle = BorderStyle.FixedSingle;
				control.BackColor = SurfaceColor;
				control.ForeColor = InkColor;
			}
			else if (control is TabControl)
			{
				TabControl tabControl = (TabControl)control;
				tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
				tabControl.SizeMode = TabSizeMode.Fixed;
				tabControl.ItemSize = ((tabControl == _tabs) ? new Size(148, 38) : ((tabControl == _splitScreenPresetTabs) ? new Size(92, 32) : new Size(150, 32)));
				tabControl.DrawItem += DrawModernTab;
			}
			else if (control is Label)
			{
				Label label = (Label)control;
				if (label.ForeColor != Color.White && label.ForeColor.R > 45)
				{
					label.ForeColor = ((label.ForeColor.B > label.ForeColor.R + 80) ? AccentColor : MutedColor);
				}
			}
			ApplyModernVisualStyle(control);
		}
	}

	private void DrawModernTab(object sender, DrawItemEventArgs e)
	{
		if (!(sender is TabControl tabControl) || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
		{
			return;
		}
		bool flag = e.Index == tabControl.SelectedIndex;
		Rectangle bounds = e.Bounds;
		using (Brush brush = new SolidBrush(flag ? SurfaceColor : Color.FromArgb(238, 242, 247)))
		{
			e.Graphics.FillRectangle(brush, bounds);
		}
		if (flag)
		{
			using Brush brush2 = new SolidBrush(AccentColor);
			e.Graphics.FillRectangle(brush2, bounds.Left + 10, bounds.Bottom - 3, Math.Max(4, bounds.Width - 20), 3);
		}
		using Font font = new Font("Microsoft YaHei UI", (tabControl == _tabs) ? 10f : 9f, flag ? FontStyle.Bold : FontStyle.Regular);
		TextRenderer.DrawText(e.Graphics, tabControl.TabPages[e.Index].Text, font, bounds, flag ? InkColor : MutedColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}

	private void HookEvents()
	{
		_addButton.Click += delegate
		{
			AddFromDialog(forSplit: false);
		};
		_removeButton.Click += delegate
		{
			RemoveSelected();
		};
		_upButton.Click += delegate
		{
			MoveSelected(-1);
		};
		_downButton.Click += delegate
		{
			MoveSelected(1);
		};
		_shuffleButton.Click += delegate
		{
			ShuffleMergeList();
		};
		_crossFolderButton.Click += delegate
		{
			InterleaveMergeListBySourceFolder();
		};
		_clearButton.Click += delegate
		{
			if (!_isRunning)
			{
				_videos.Clear();
				_mergeVideoAdjustments.Clear();
				UpdateListView(null);
			}
		};
		_mergeResetButton.Click += delegate
		{
			ResetMergePage();
		};
		_browseOutputButton.Click += delegate
		{
			ChooseOutputFolder();
		};
		_openOutputButton.Click += delegate
		{
			OpenOutputFolder();
		};
		_startButton.Click += delegate
		{
			StartMerge();
		};
		_cancelButton.Click += delegate
		{
			CancelMerge();
		};
		_mergePreviewButton.Click += delegate
		{
			StartMergePreview();
		};
		_transitionSelectAllButton.Click += delegate
		{
			SetAllTransitionChecks(value: true);
		};
		_transitionClearButton.Click += delegate
		{
			SetAllTransitionChecks(value: false);
		};
		_limitGroupDuration.CheckedChanged += delegate
		{
			UpdateMergePlanningUi();
		};
		_similaritySort.CheckedChanged += delegate
		{
			UpdateMergePlanningUi();
		};
		_maxOutputCount.ValueChanged += delegate
		{
			UpdateMergePlanningUi();
		};
		_splitAddButton.Click += delegate
		{
			AddFromDialog(forSplit: true);
		};
		_splitRemoveButton.Click += delegate
		{
			RemoveSplitSelected();
		};
		_splitClearButton.Click += delegate
		{
			if (!_isRunning)
			{
				_splitVideos.Clear();
				_splitVideoAdjustments.Clear();
				UpdateSplitListView(null);
			}
		};
		_splitResetButton.Click += delegate
		{
			ResetSplitPage();
		};
		_splitBrowseOutputButton.Click += delegate
		{
			ChooseSplitOutputFolder();
		};
		_splitOpenOutputButton.Click += delegate
		{
			OpenFolder(_splitOutputFolder.Text, _latestSplitOutputFolder);
		};
		_splitStartButton.Click += delegate
		{
			StartSplit();
		};
		_splitCancelButton.Click += delegate
		{
			CancelMerge();
		};
		_splitMode.SelectedIndexChanged += delegate
		{
			UpdateSplitModeUi();
		};
		_watermarkOnMerge.CheckedChanged += delegate
		{
			UpdateWatermarkUi();
		};
		_watermarkOnSplit.CheckedChanged += delegate
		{
			UpdateWatermarkUi();
		};
		_watermarkOnSplitScreen.CheckedChanged += delegate
		{
			UpdateWatermarkUi();
		};
		_watermarkStaySelectAllButton.Click += delegate
		{
			SetAllWatermarkStayEffectChecks(value: true);
		};
		_watermarkStayRandomButton.Click += delegate
		{
			RandomlyAssignWatermarkStayEffects();
		};
		_watermarkStayClearButton.Click += delegate
		{
			ClearWatermarkStayEffects();
		};
		for (int num = 0; num < 3; num++)
		{
			int capturedTextIndex = num;
			_textWatermarkEnabled[num].CheckedChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_textWatermarkShowUntilEnd[num].CheckedChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_textWatermarkBackgroundEnabled[num].CheckedChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_textWatermarkOutlineEnabled[num].CheckedChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_textWatermarkAllowOverflow[num].CheckedChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_textWatermarkStayEffect[num].SelectedIndexChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_watermarkColorButton[num].Click += delegate
			{
				ChooseWatermarkColor(capturedTextIndex);
			};
			_textWatermarkBackgroundColorButton[num].Click += delegate
			{
				ChooseWatermarkBackgroundColor(capturedTextIndex);
			};
			_textWatermarkOutlineColorButton[num].Click += delegate
			{
				ChooseWatermarkOutlineColor(capturedTextIndex);
			};
			_textWatermarkCenter[num].Click += delegate
			{
				CenterWatermarkLayer(_textWatermarkPosition[capturedTextIndex], _textWatermarkOffsetX[capturedTextIndex], _textWatermarkOffsetY[capturedTextIndex]);
			};
			_textWatermarkTopCenter[num].Click += delegate
			{
				SetWatermarkPosition(_textWatermarkPosition[capturedTextIndex], _textWatermarkOffsetX[capturedTextIndex], _textWatermarkOffsetY[capturedTextIndex], "顶部居中");
			};
			_textWatermarkBottomCenter[num].Click += delegate
			{
				SetWatermarkPosition(_textWatermarkPosition[capturedTextIndex], _textWatermarkOffsetX[capturedTextIndex], _textWatermarkOffsetY[capturedTextIndex], "底部居中");
			};
		}
		for (int num2 = 0; num2 < 3; num2++)
		{
			int capturedIndex = num2;
			_imageWatermarkEnabled[num2].CheckedChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_imageWatermarkShowUntilEnd[num2].CheckedChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_imageWatermarkStayEffect[num2].SelectedIndexChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_imageWatermarkPlaybackMode[num2].SelectedIndexChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_imageWatermarkSwitchEffect[num2].SelectedIndexChanged += delegate
			{
				UpdateWatermarkUi();
			};
			_imageWatermarkBrowse[num2].Click += delegate
			{
				ChooseWatermarkImages(capturedIndex);
			};
			_imageWatermarkRemove[num2].Click += delegate
			{
				RemoveSelectedWatermarkImage(capturedIndex);
			};
			_imageWatermarkClear[num2].Click += delegate
			{
				ClearWatermarkImages(capturedIndex);
			};
			_imageWatermarkApplyAll[num2].Click += delegate
			{
				ApplyCurrentImageSettingsToAll(capturedIndex);
			};
			_imageWatermarkList[num2].SelectedIndexChanged += delegate
			{
				ChangeSelectedWatermarkImage(capturedIndex);
			};
			_imageWatermarkCenter[num2].Click += delegate
			{
				CenterWatermarkLayer(_imageWatermarkPosition[capturedIndex], _imageWatermarkOffsetX[capturedIndex], _imageWatermarkOffsetY[capturedIndex]);
			};
		}
		_watermarkVideoAddButton.Click += delegate
		{
			AddWatermarkVideosFromDialog();
		};
		_watermarkVideoRemoveButton.Click += delegate
		{
			RemoveWatermarkVideosSelected();
		};
		_watermarkVideoClearButton.Click += delegate
		{
			if (!_isRunning)
			{
				_watermarkVideos.Clear();
				_watermarkVideoAdjustments.Clear();
				UpdateWatermarkVideoList(null);
			}
		};
		_watermarkSourceImageAddButton.Click += delegate
		{
			AddWatermarkSourceImagesFromDialog();
		};
		_watermarkSourceImageRemoveButton.Click += delegate
		{
			RemoveSelectedWatermarkSourceImages();
		};
		_watermarkSourceImageClearButton.Click += delegate
		{
			if (!_isRunning)
			{
				_watermarkSourceImages.Clear();
				UpdateWatermarkSourceImageList();
			}
		};
		_watermarkSourceTabs.SelectedIndexChanged += delegate
		{
			UpdateWatermarkSourceModeUi();
		};
		_watermarkResetButton.Click += delegate
		{
			ResetWatermarkPage();
		};
		_watermarkOutputBrowseButton.Click += delegate
		{
			ChooseWatermarkOutputFolder();
		};
		_watermarkOutputOpenButton.Click += delegate
		{
			OpenFolder(_watermarkOutputFolder.Text, _latestWatermarkOutputFolder);
		};
		_watermarkStartButton.Click += delegate
		{
			StartDirectWatermark();
		};
		_watermarkCancelButton.Click += delegate
		{
			CancelMerge();
		};
		_globalResetButton.Click += delegate
		{
			ResetAllSettings();
		};
		_imageAddButton.Click += delegate
		{
			AddSlideshowImagesFromDialog();
		};
		_imageRemoveButton.Click += delegate
		{
			RemoveSelectedSlideshowImages();
		};
		_imageUpButton.Click += delegate
		{
			MoveSlideshowImage(-1);
		};
		_imageDownButton.Click += delegate
		{
			MoveSlideshowImage(1);
		};
		_imageShuffleButton.Click += delegate
		{
			ShuffleSlideshowImages();
		};
		_imageClearButton.Click += delegate
		{
			if (!_isRunning)
			{
				_slideshowImages.Clear();
				UpdateSlideshowImageList();
			}
		};
		_imageResetButton.Click += delegate
		{
			ResetImageVideoPage();
		};
		_imageLayoutSelectAllButton.Click += delegate
		{
			SetAllImageLayoutChecks(value: true);
		};
		_imageLayoutClearButton.Click += delegate
		{
			SetAllImageLayoutChecks(value: false);
		};
		_imageMotionSelectAllButton.Click += delegate
		{
			SetAllImageMotionChecks(value: true);
		};
		_imageMotionClearButton.Click += delegate
		{
			SetAllImageMotionChecks(value: false);
		};
		_imageTransitionSelectAllButton.Click += delegate
		{
			SetAllImageTransitionChecks(value: true);
		};
		_imageTransitionClearButton.Click += delegate
		{
			SetAllImageTransitionChecks(value: false);
		};
		_imageLayoutPreset.SelectedIndexChanged += delegate
		{
			UpdateImageLayoutUi();
		};
		_imageLayoutPool.ItemCheck += delegate
		{
			if (base.IsHandleCreated)
			{
				BeginInvoke(new MethodInvoker(UpdateImageLayoutUi));
			}
			else
			{
				UpdateImageLayoutUi();
			}
		};
		_imageMotionPool.ItemCheck += delegate
		{
			if (base.IsHandleCreated)
			{
				BeginInvoke(new MethodInvoker(UpdateImageLayoutUi));
			}
			else
			{
				UpdateImageLayoutUi();
			}
		};
		_imageLayoutOrder.SelectedIndexChanged += delegate
		{
			UpdateImageLayoutUi();
		};
		_imageMotionOrder.SelectedIndexChanged += delegate
		{
			UpdateImageLayoutUi();
		};
		_imageOutputCount.ValueChanged += delegate
		{
			UpdateImageLayoutUi();
		};
		_imageTargetDuration.ValueChanged += delegate
		{
			UpdateImageLayoutUi();
		};
		_imageOutputBrowseButton.Click += delegate
		{
			ChooseImageOutputFolder();
		};
		_imageOutputOpenButton.Click += delegate
		{
			OpenFolder(_imageOutputFolder.Text, _latestImageVideoOutputFolder);
		};
		_imageStartButton.Click += delegate
		{
			StartImageVideo();
		};
		_imageCancelButton.Click += delegate
		{
			CancelMerge();
		};
		HookVideoAdjustmentEditor(_videoList, _videos, _mergeVideoAdjustments, _mergeAdjustmentEditor, UpdateListView, delegate(string message)
		{
			_statusLabel.Text = message;
		});
		HookVideoAdjustmentEditor(_splitVideoList, _splitVideos, _splitVideoAdjustments, _splitAdjustmentEditor, UpdateSplitListView, delegate(string message)
		{
			_splitStatusLabel.Text = message;
		});
		HookVideoAdjustmentEditor(_watermarkVideoList, _watermarkVideos, _watermarkVideoAdjustments, _watermarkAdjustmentEditor, UpdateWatermarkVideoList, delegate(string message)
		{
			_watermarkStatusLabel.Text = message;
		});
		_videoList.DragEnter += OnDragEnter;
		_videoList.DragDrop += OnDragDrop;
		_splitVideoList.DragEnter += OnDragEnter;
		_splitVideoList.DragDrop += OnDragDrop;
		_watermarkVideoList.DragEnter += OnDragEnter;
		_watermarkVideoList.DragDrop += OnDragDrop;
		_videoList.MouseDoubleClick += delegate(object sender, MouseEventArgs e)
		{
			if (!_isRunning && _videoList.HitTest(e.Location).Item == null)
			{
				AddFromDialog(forSplit: false);
			}
		};
		_splitVideoList.MouseDoubleClick += delegate(object sender, MouseEventArgs e)
		{
			if (!_isRunning && _splitVideoList.HitTest(e.Location).Item == null)
			{
				AddFromDialog(forSplit: true);
			}
		};
		_watermarkVideoList.MouseDoubleClick += delegate(object sender, MouseEventArgs e)
		{
			if (!_isRunning && _watermarkVideoList.HitTest(e.Location).Item == null)
			{
				AddWatermarkVideosFromDialog();
			}
		};
		_watermarkSourceImageList.DragEnter += OnDragEnter;
		_watermarkSourceImageList.DragDrop += OnWatermarkSourceImageDragDrop;
		_slideshowImageList.DragEnter += OnDragEnter;
		_slideshowImageList.DragDrop += OnImageDragDrop;
		base.DragEnter += OnDragEnter;
		base.DragDrop += OnDragDrop;
		base.FormClosing += OnFormClosing;
		base.Resize += delegate
		{
			ResizeColumns();
		};
	}

	private void ResizeColumns()
	{
		ResizeListColumns(_videoList);
		ResizeListColumns(_splitVideoList);
		ResizeListColumns(_watermarkVideoList);
	}

	private static void ResizeListColumns(ListView list)
	{
		if (list != null && list.Columns.Count >= 7)
		{
			int num = list.Columns[0].Width;
			for (int i = 3; i < list.Columns.Count; i++)
			{
				num += list.Columns[i].Width;
			}
			int num2 = Math.Max(360, list.ClientSize.Width - num - 24);
			list.Columns[1].Width = Math.Max(180, (int)((double)num2 * 0.48));
			list.Columns[2].Width = Math.Max(180, num2 - list.Columns[1].Width);
		}
	}

	private void OnDragEnter(object sender, DragEventArgs e)
	{
		if (!_isRunning && e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			e.Effect = DragDropEffects.Copy;
		}
		else
		{
			e.Effect = DragDropEffects.None;
		}
	}

	private void OnDragDrop(object sender, DragEventArgs e)
	{
		if (!_isRunning && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
		{
			if (sender == _splitScreenRegionList || sender == _splitScreenPreview || (_tabs != null && _tabs.SelectedIndex == 3))
			{
				AddSplitScreenVideoPaths(paths, _splitScreenSelectedRegion);
			}
			else if (sender == _slideshowImageList || (_tabs != null && _tabs.SelectedIndex == 4))
			{
				AddSlideshowImagePaths(paths);
			}
			else if (sender == _watermarkSourceImageList || (_tabs != null && _tabs.SelectedIndex == 2 && _watermarkSourceTabs != null && _watermarkSourceTabs.SelectedIndex == 1))
			{
				AddWatermarkSourceImagePaths(paths);
			}
			else if (sender == _watermarkVideoList || (_tabs != null && _tabs.SelectedIndex == 2))
			{
				AddWatermarkVideoPaths(paths);
			}
			else
			{
				AddPaths(paths, sender == _splitVideoList || (_tabs != null && _tabs.SelectedIndex == 1));
			}
		}
	}

	private void OnImageDragDrop(object sender, DragEventArgs e)
	{
		if (!_isRunning && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
		{
			AddSlideshowImagePaths(paths);
		}
	}

	private void OnWatermarkSourceImageDragDrop(object sender, DragEventArgs e)
	{
		if (!_isRunning && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
		{
			AddWatermarkSourceImagePaths(paths);
		}
	}

	private void AddFromDialog(bool forSplit)
	{
		if (_isRunning)
		{
			return;
		}
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Title = (forSplit ? "选择要拆分的视频（可多选）" : "选择要合并的视频（可多选）");
		openFileDialog.Filter = "视频文件|*.mp4;*.mov;*.mkv;*.avi;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.mts;*.m2ts;*.3gp;*.mpg;*.mpeg|所有文件|*.*";
		openFileDialog.Multiselect = true;
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			AddPaths(openFileDialog.FileNames, forSplit);
		}
	}

	private void AddPaths(IEnumerable<string> paths, bool forSplit)
	{
		List<string> list = new List<string>();
		foreach (string path in paths)
		{
			try
			{
				if (File.Exists(path) && IsVideo(path))
				{
					list.Add(Path.GetFullPath(path));
				}
				else if (Directory.Exists(path))
				{
					list.AddRange(Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).Where(IsVideo));
				}
			}
			catch
			{
			}
		}
		list.Sort(new NaturalPathComparer());
		List<string> list2 = (forSplit ? _splitVideos : _videos);
		HashSet<string> hashSet = new HashSet<string>(list2, StringComparer.OrdinalIgnoreCase);
		int count = list2.Count;
		foreach (string item in list)
		{
			string fullPath = Path.GetFullPath(item);
			if (hashSet.Add(fullPath))
			{
				list2.Add(fullPath);
				Dictionary<string, VideoAdjustmentSettings> dictionary = (forSplit ? _splitVideoAdjustments : _mergeVideoAdjustments);
				if (!dictionary.ContainsKey(fullPath))
				{
					VideoAdjustmentEditor editor = (forSplit ? _splitAdjustmentEditor : _mergeAdjustmentEditor);
					dictionary[fullPath] = CaptureVideoAdjustmentEditor(editor);
				}
			}
		}
		int num = list2.Count - count;
		if (forSplit)
		{
			UpdateSplitListView(null);
			_splitStatusLabel.Text = ((num > 0) ? ("已添加 " + num + " 个视频。") : "没有发现新的受支持视频。");
			return;
		}
		UpdateListView(null);
		_statusLabel.Text = ((num > 0) ? ("已添加 " + num + " 个视频，可拖动更多文件继续添加。" + (_similaritySort.Checked ? " 当前仍启用自动相似排序；点击任一手动排序按钮会自动切换为手动模式。" : "")) : "没有发现新的受支持视频。");
	}

	private static bool IsVideo(string path)
	{
		string ext = Path.GetExtension(path);
		return VideoExtensions.Any((string x) => x.Equals(ext, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsImage(string path)
	{
		string ext = Path.GetExtension(path);
		return ImageExtensions.Any((string x) => x.Equals(ext, StringComparison.OrdinalIgnoreCase));
	}

	private void AddSlideshowImagesFromDialog()
	{
		if (_isRunning)
		{
			return;
		}
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Title = "选择要合成为视频的图片（可多选）";
		openFileDialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件|*.*";
		openFileDialog.Multiselect = true;
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			AddSlideshowImagePaths(openFileDialog.FileNames);
		}
	}

	private void AddSlideshowImagePaths(IEnumerable<string> paths)
	{
		List<string> list = new List<string>();
		foreach (string path in paths)
		{
			try
			{
				if (File.Exists(path) && IsImage(path))
				{
					list.Add(Path.GetFullPath(path));
				}
				else if (Directory.Exists(path))
				{
					list.AddRange(Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).Where(IsImage));
				}
			}
			catch
			{
			}
		}
		list.Sort(new NaturalPathComparer());
		HashSet<string> hashSet = new HashSet<string>(_slideshowImages, StringComparer.OrdinalIgnoreCase);
		foreach (string item in list)
		{
			if (hashSet.Add(item))
			{
				_slideshowImages.Add(item);
			}
		}
		UpdateSlideshowImageList();
		_imageStatusLabel.Text = ((list.Count > 0) ? "已添加图片；可调整顺序、随机打乱并选择转场。" : "没有发现新的受支持图片。");
	}

	private void UpdateSlideshowImageList()
	{
		_slideshowImageList.BeginUpdate();
		_slideshowImageList.Items.Clear();
		for (int i = 0; i < _slideshowImages.Count; i++)
		{
			_slideshowImageList.Items.Add((i + 1).ToString("000") + "    " + Path.GetFileName(_slideshowImages[i]) + "    —    " + Path.GetDirectoryName(_slideshowImages[i]));
		}
		_slideshowImageList.EndUpdate();
		_imageCountLabel.Text = "共 " + _slideshowImages.Count + " 张图片";
	}

	private void AddWatermarkSourceImagesFromDialog()
	{
		if (_isRunning)
		{
			return;
		}
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Title = "选择要批量添加水印的原图片（可多选）";
		openFileDialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件|*.*";
		openFileDialog.Multiselect = true;
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			AddWatermarkSourceImagePaths(openFileDialog.FileNames);
		}
	}

	private void AddWatermarkSourceImagePaths(IEnumerable<string> paths)
	{
		if (_isRunning || paths == null)
		{
			return;
		}
		List<string> list = new List<string>();
		foreach (string path in paths)
		{
			try
			{
				if (File.Exists(path) && IsImage(path))
				{
					list.Add(Path.GetFullPath(path));
				}
				else if (Directory.Exists(path))
				{
					list.AddRange(Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).Where(IsImage));
				}
			}
			catch
			{
			}
		}
		list.Sort(new NaturalPathComparer());
		HashSet<string> hashSet = new HashSet<string>(_watermarkSourceImages, StringComparer.OrdinalIgnoreCase);
		foreach (string item in list)
		{
			if (hashSet.Add(item))
			{
				_watermarkSourceImages.Add(item);
			}
		}
		UpdateWatermarkSourceImageList();
		_watermarkStatusLabel.Text = ((list.Count > 0) ? "已添加原图片；启用水印图层后即可批量导出 PNG。" : "没有发现新的受支持图片。");
	}

	private void UpdateWatermarkSourceImageList()
	{
		if (_watermarkSourceImageList != null)
		{
			_watermarkSourceImageList.BeginUpdate();
			_watermarkSourceImageList.Items.Clear();
			for (int i = 0; i < _watermarkSourceImages.Count; i++)
			{
				_watermarkSourceImageList.Items.Add((i + 1).ToString("000") + "    " + Path.GetFileName(_watermarkSourceImages[i]) + "    —    " + Path.GetDirectoryName(_watermarkSourceImages[i]));
			}
			_watermarkSourceImageList.EndUpdate();
			_watermarkSourceImageCountLabel.Text = "共 " + _watermarkSourceImages.Count + " 张图片";
		}
	}

	private void RemoveSelectedWatermarkSourceImages()
	{
		if (_isRunning || _watermarkSourceImageList.SelectedIndices.Count == 0)
		{
			return;
		}
		foreach (int item in from int x in _watermarkSourceImageList.SelectedIndices
			orderby x descending
			select x)
		{
			_watermarkSourceImages.RemoveAt(item);
		}
		UpdateWatermarkSourceImageList();
	}

	private void UpdateWatermarkSourceModeUi()
	{
		if (_watermarkSourceTabs != null && _watermarkStartButton != null)
		{
			bool flag = _watermarkSourceTabs.SelectedIndex == 1;
			_watermarkStartButton.Text = (flag ? "图片加水印导出" : "直接加水印导出");
			_watermarkStatusLabel.Text = (flag ? "图片导出会应用文字/图片水印的样式、位置与透明度；时间动画和 BGM 不用于静态图片。" : "启用至少一个水印图层，然后添加原视频即可直接导出");
			if (!_isRunning)
			{
				SetBgmControlsEnabled(_watermarkBgm, !flag);
				SetOutputFrameControlsEnabled(_watermarkOutputFrameControls, !flag);
			}
			UpdateWatermarkUi();
		}
	}

	private void UpdateImageLayoutUi()
	{
		if (_imageLayoutPool != null && _imageMotionPool != null && _imageStatusLabel != null)
		{
			_imageTileAnimationDuration.Enabled = !_isRunning && _imageMotionPool.CheckedIndices.Count > 0;
			int count = _imageLayoutPool.CheckedIndices.Count;
			int count2 = _imageMotionPool.CheckedIndices.Count;
			_imageStatusLabel.Text = "将生成 " + _imageOutputCount.Value + " 个、每个 " + _imageTargetDuration.Value.ToString("0.#", CultureInfo.InvariantCulture) + " 秒的成品；已选 " + count + " 种排版、" + count2 + " 种展示律动。";
		}
	}

	private ImageVideoLayoutPlan CaptureImageVideoLayoutPlan()
	{
		int num = Math.Max(0, _imageLayoutPreset.SelectedIndex);
		ImageVideoLayoutPlan imageVideoLayoutPlan = new ImageVideoLayoutPlan();
		imageVideoLayoutPlan.ImagesPerPage = ((num == 0) ? 1 : (num + 2));
		imageVideoLayoutPlan.DisplayName = ((_imageLayoutPreset.SelectedItem == null) ? "单图轮播" : _imageLayoutPreset.SelectedItem.ToString());
		imageVideoLayoutPlan.TileAnimation = ((_imageTileAnimation.SelectedItem == null) ? "直接显示" : _imageTileAnimation.SelectedItem.ToString());
		imageVideoLayoutPlan.TileAnimationDuration = decimal.ToDouble(_imageTileAnimationDuration.Value);
		imageVideoLayoutPlan.SmartEnhance = _imageSmartEnhance.Checked;
		imageVideoLayoutPlan.RandomLayouts = _imageLayoutOrder.SelectedIndex == 0;
		imageVideoLayoutPlan.RandomMotions = _imageMotionOrder.SelectedIndex == 0;
		imageVideoLayoutPlan.OutputCount = decimal.ToInt32(_imageOutputCount.Value);
		imageVideoLayoutPlan.TargetDurationSeconds = decimal.ToDouble(_imageTargetDuration.Value);
		ImageVideoLayoutPlan imageVideoLayoutPlan2 = imageVideoLayoutPlan;
		List<ImageLayoutChoice> list = CreateImageLayoutCatalog();
		foreach (int checkedIndex in _imageLayoutPool.CheckedIndices)
		{
			if (checkedIndex >= 0 && checkedIndex < list.Count)
			{
				imageVideoLayoutPlan2.LayoutChoices.Add(list[checkedIndex]);
			}
		}
		if (imageVideoLayoutPlan2.LayoutChoices.Count == 0)
		{
			int[] array = new int[8] { 0, 1, 3, 4, 5, 6, 7, 8 };
			int index = array[Math.Min(num, array.Length - 1)];
			imageVideoLayoutPlan2.LayoutChoices.Add(list[index]);
		}
		foreach (object checkedItem in _imageMotionPool.CheckedItems)
		{
			imageVideoLayoutPlan2.MotionEffects.Add(checkedItem.ToString());
		}
		if (imageVideoLayoutPlan2.MotionEffects.Count == 0)
		{
			imageVideoLayoutPlan2.MotionEffects.Add("直接显示");
		}
		imageVideoLayoutPlan2.ImagesPerPage = imageVideoLayoutPlan2.LayoutChoices[0].ImagesPerPage;
		imageVideoLayoutPlan2.DisplayName = string.Join("、", imageVideoLayoutPlan2.LayoutChoices.Select((ImageLayoutChoice x) => x.DisplayName).ToArray());
		imageVideoLayoutPlan2.TileAnimation = imageVideoLayoutPlan2.MotionEffects[0];
		return imageVideoLayoutPlan2;
	}

	private static List<ImageLayoutChoice> CreateImageLayoutCatalog()
	{
		List<ImageLayoutChoice> list = new List<ImageLayoutChoice>();
		list.Add(new ImageLayoutChoice
		{
			DisplayName = "单图轮播",
			ImagesPerPage = 1,
			LayoutKind = 0
		});
		list.Add(new ImageLayoutChoice
		{
			DisplayName = "3 张·杂志三联",
			ImagesPerPage = 3,
			LayoutKind = 1
		});
		list.Add(new ImageLayoutChoice
		{
			DisplayName = "4 张·经典四宫格",
			ImagesPerPage = 4,
			LayoutKind = 2
		});
		list.Add(new ImageLayoutChoice
		{
			DisplayName = "4 张·主图焦点",
			ImagesPerPage = 4,
			LayoutKind = 3
		});
		list.Add(new ImageLayoutChoice
		{
			DisplayName = "5 张·一大四小",
			ImagesPerPage = 5,
			LayoutKind = 4
		});
		list.Add(new ImageLayoutChoice
		{
			DisplayName = "6 张·经典六宫格",
			ImagesPerPage = 6,
			LayoutKind = 5
		});
		list.Add(new ImageLayoutChoice
		{
			DisplayName = "7 张·主图加六图",
			ImagesPerPage = 7,
			LayoutKind = 6
		});
		list.Add(new ImageLayoutChoice
		{
			DisplayName = "8 张·不规则画廊",
			ImagesPerPage = 8,
			LayoutKind = 7
		});
		list.Add(new ImageLayoutChoice
		{
			DisplayName = "9 张·经典九宫格",
			ImagesPerPage = 9,
			LayoutKind = 8
		});
		return list;
	}

	private void RemoveSelectedSlideshowImages()
	{
		if (_isRunning || _slideshowImageList.SelectedIndices.Count == 0)
		{
			return;
		}
		foreach (int item in from int x in _slideshowImageList.SelectedIndices
			orderby x descending
			select x)
		{
			_slideshowImages.RemoveAt(item);
		}
		UpdateSlideshowImageList();
	}

	private void MoveSlideshowImage(int direction)
	{
		if (!_isRunning && _slideshowImageList.SelectedIndex >= 0)
		{
			int selectedIndex = _slideshowImageList.SelectedIndex;
			int num = selectedIndex + direction;
			if (num >= 0 && num < _slideshowImages.Count)
			{
				string value = _slideshowImages[selectedIndex];
				_slideshowImages[selectedIndex] = _slideshowImages[num];
				_slideshowImages[num] = value;
				UpdateSlideshowImageList();
				_slideshowImageList.SelectedIndex = num;
			}
		}
	}

	private void ShuffleSlideshowImages()
	{
		if (!_isRunning && _slideshowImages.Count >= 2)
		{
			Random random = new Random(Environment.TickCount * 31 + Guid.NewGuid().GetHashCode());
			for (int num = _slideshowImages.Count - 1; num > 0; num--)
			{
				int index = random.Next(num + 1);
				string value = _slideshowImages[num];
				_slideshowImages[num] = _slideshowImages[index];
				_slideshowImages[index] = value;
			}
			UpdateSlideshowImageList();
			_imageStatusLabel.Text = "已随机打乱图片顺序。";
		}
	}

	private void SetAllImageTransitionChecks(bool value)
	{
		if (!_isRunning)
		{
			for (int i = 0; i < _imageTransitions.Items.Count; i++)
			{
				_imageTransitions.SetItemChecked(i, value);
			}
		}
	}

	private void SetAllImageLayoutChecks(bool value)
	{
		if (!_isRunning)
		{
			for (int i = 0; i < _imageLayoutPool.Items.Count; i++)
			{
				_imageLayoutPool.SetItemChecked(i, value);
			}
			UpdateImageLayoutUi();
		}
	}

	private void SetAllImageMotionChecks(bool value)
	{
		if (!_isRunning)
		{
			for (int i = 0; i < _imageMotionPool.Items.Count; i++)
			{
				_imageMotionPool.SetItemChecked(i, value);
			}
			UpdateImageLayoutUi();
		}
	}

	private void ChooseImageOutputFolder()
	{
		if (_isRunning)
		{
			return;
		}
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "选择图片成片的保存位置";
		folderBrowserDialog.SelectedPath = _imageOutputFolder.Text;
		folderBrowserDialog.ShowNewFolderButton = true;
		if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
		{
			_imageOutputFolder.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private void AddWatermarkVideosFromDialog()
	{
		if (_isRunning)
		{
			return;
		}
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Title = "选择要直接添加水印的原视频（可多选）";
		openFileDialog.Filter = "视频文件|*.mp4;*.mov;*.mkv;*.avi;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.mts;*.m2ts;*.3gp;*.mpg;*.mpeg|所有文件|*.*";
		openFileDialog.Multiselect = true;
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			AddWatermarkVideoPaths(openFileDialog.FileNames);
		}
	}

	private void AddWatermarkVideoPaths(IEnumerable<string> paths)
	{
		List<string> list = new List<string>();
		foreach (string path in paths)
		{
			try
			{
				if (File.Exists(path) && IsVideo(path))
				{
					list.Add(Path.GetFullPath(path));
				}
				else if (Directory.Exists(path))
				{
					list.AddRange(Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).Where(IsVideo));
				}
			}
			catch
			{
			}
		}
		list.Sort(new NaturalPathComparer());
		HashSet<string> hashSet = new HashSet<string>(_watermarkVideos, StringComparer.OrdinalIgnoreCase);
		int count = _watermarkVideos.Count;
		foreach (string item in list)
		{
			string fullPath = Path.GetFullPath(item);
			if (hashSet.Add(fullPath))
			{
				_watermarkVideos.Add(fullPath);
				if (!_watermarkVideoAdjustments.ContainsKey(fullPath))
				{
					_watermarkVideoAdjustments[fullPath] = CaptureVideoAdjustmentEditor(_watermarkAdjustmentEditor);
				}
			}
		}
		UpdateWatermarkVideoList(null);
		int num = _watermarkVideos.Count - count;
		_watermarkStatusLabel.Text = ((num > 0) ? ("已添加 " + num + " 个原视频，可直接加水印导出。") : "没有发现新的受支持视频。");
	}

	private void RemoveWatermarkVideosSelected()
	{
		if (_isRunning || _watermarkVideoList.SelectedIndices.Count == 0)
		{
			return;
		}
		List<int> list = (from int x in _watermarkVideoList.SelectedIndices
			orderby x descending
			select x).ToList();
		foreach (int item in list)
		{
			_watermarkVideoAdjustments.Remove(_watermarkVideos[item]);
			_watermarkVideos.RemoveAt(item);
		}
		UpdateWatermarkVideoList(null);
	}

	private void RemoveSelected()
	{
		if (_isRunning || _videoList.SelectedIndices.Count == 0)
		{
			return;
		}
		List<int> list = (from int x in _videoList.SelectedIndices
			orderby x descending
			select x).ToList();
		foreach (int item in list)
		{
			_mergeVideoAdjustments.Remove(_videos[item]);
			_videos.RemoveAt(item);
		}
		UpdateListView(null);
	}

	private void RemoveSplitSelected()
	{
		if (_isRunning || _splitVideoList.SelectedIndices.Count == 0)
		{
			return;
		}
		List<int> list = (from int x in _splitVideoList.SelectedIndices
			orderby x descending
			select x).ToList();
		foreach (int item in list)
		{
			_splitVideoAdjustments.Remove(_splitVideos[item]);
			_splitVideos.RemoveAt(item);
		}
		UpdateSplitListView(null);
	}

	private void ShuffleMergeList()
	{
		if (_isRunning)
		{
			return;
		}
		if (_videos.Count < 2)
		{
			_statusLabel.Text = "至少需要 2 个视频才能随机打乱。";
			return;
		}
		bool flag = SwitchToManualSortingMode();
		List<string> second = new List<string>(_videos);
		Random random = new Random(Environment.TickCount * 31 + Guid.NewGuid().GetHashCode());
		for (int num = _videos.Count - 1; num > 0; num--)
		{
			int index = random.Next(num + 1);
			string value = _videos[num];
			_videos[num] = _videos[index];
			_videos[index] = value;
		}
		if (_videos.SequenceEqual(second))
		{
			string item = _videos[0];
			_videos.RemoveAt(0);
			_videos.Add(item);
		}
		UpdateListView(null);
		_statusLabel.Text = (flag ? "已关闭自动相似排序，并" : "已") + "随机打乱视频顺序；请确认列表后再开始合并。";
	}

	private void InterleaveMergeListBySourceFolder()
	{
		if (_isRunning)
		{
			return;
		}
		if (_videos.Count < 2)
		{
			_statusLabel.Text = "至少需要 2 个视频才能按来源文件夹交叉排序。";
			return;
		}
		bool flag = SwitchToManualSortingMode();
		List<string> list = new List<string>();
		Dictionary<string, Queue<string>> dictionary = new Dictionary<string, Queue<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (string video in _videos)
		{
			string text = Path.GetDirectoryName(video) ?? "";
			if (!dictionary.TryGetValue(text, out var value))
			{
				value = (dictionary[text] = new Queue<string>());
				list.Add(text);
			}
			value.Enqueue(video);
		}
		if (list.Count < 2)
		{
			_statusLabel.Text = (flag ? "已关闭自动相似排序；" : "") + "当前视频都来自同一个文件夹，不需要交叉排序。";
			return;
		}
		List<string> list2 = new List<string>();
		bool flag2;
		do
		{
			flag2 = false;
			foreach (string item in list)
			{
				Queue<string> queue2 = dictionary[item];
				if (queue2.Count != 0)
				{
					list2.Add(queue2.Dequeue());
					flag2 = true;
				}
			}
		}
		while (flag2);
		_videos.Clear();
		_videos.AddRange(list2);
		UpdateListView(null);
		_statusLabel.Text = (flag ? "已关闭自动相似排序；" : "") + "已按 " + list.Count + " 个来源文件夹交叉排序。";
	}

	private void MoveSelected(int direction)
	{
		if (_isRunning || _videoList.SelectedIndices.Count == 0)
		{
			return;
		}
		bool flag = SwitchToManualSortingMode();
		HashSet<int> hashSet = new HashSet<int>(_videoList.SelectedIndices.Cast<int>());
		if (direction < 0)
		{
			for (int i = 1; i < _videos.Count; i++)
			{
				if (hashSet.Contains(i) && !hashSet.Contains(i - 1))
				{
					string value = _videos[i - 1];
					_videos[i - 1] = _videos[i];
					_videos[i] = value;
					hashSet.Remove(i);
					hashSet.Add(i - 1);
				}
			}
		}
		else
		{
			for (int num = _videos.Count - 2; num >= 0; num--)
			{
				if (hashSet.Contains(num) && !hashSet.Contains(num + 1))
				{
					string value2 = _videos[num + 1];
					_videos[num + 1] = _videos[num];
					_videos[num] = value2;
					hashSet.Remove(num);
					hashSet.Add(num + 1);
				}
			}
		}
		UpdateListView(hashSet.OrderBy((int x) => x));
		if (flag)
		{
			_statusLabel.Text = "已关闭自动相似排序，并按手动顺序移动所选视频。";
		}
	}

	private bool SwitchToManualSortingMode()
	{
		if (!_similaritySort.Checked)
		{
			return false;
		}
		_similaritySort.Checked = false;
		return true;
	}

	private void UpdateListView(IEnumerable<int> selectedIndices)
	{
		UpdateVideoList(_videoList, _videos, _mergeVideoAdjustments, _countLabel, selectedIndices);
	}

	private void UpdateSplitListView(IEnumerable<int> selectedIndices)
	{
		UpdateVideoList(_splitVideoList, _splitVideos, _splitVideoAdjustments, _splitCountLabel, selectedIndices);
	}

	private void UpdateWatermarkVideoList(IEnumerable<int> selectedIndices)
	{
		UpdateVideoList(_watermarkVideoList, _watermarkVideos, _watermarkVideoAdjustments, _watermarkVideoCountLabel, selectedIndices);
	}

	private void UpdateVideoList(ListView list, List<string> videos, Dictionary<string, VideoAdjustmentSettings> settings, Label countLabel, IEnumerable<int> selectedIndices)
	{
		list.BeginUpdate();
		list.Items.Clear();
		for (int i = 0; i < videos.Count; i++)
		{
			ListViewItem listViewItem = new ListViewItem((i + 1).ToString(CultureInfo.InvariantCulture));
			listViewItem.SubItems.Add(Path.GetFileName(videos[i]));
			listViewItem.SubItems.Add(Path.GetDirectoryName(videos[i]));
			VideoAdjustmentSettings videoAdjustment = GetVideoAdjustment(settings, videos[i]);
			listViewItem.SubItems.Add(videoAdjustment.HorizontalFlip ? "是" : "否");
			listViewItem.SubItems.Add(FfmpegNumber(videoAdjustment.ScaleRatio) + "×");
			listViewItem.SubItems.Add(FfmpegNumber(videoAdjustment.SpeedRatio) + "×");
			listViewItem.SubItems.Add(videoAdjustment.VolumePercent.ToString(CultureInfo.InvariantCulture) + "%");
			listViewItem.SubItems.Add(videoAdjustment.ReversePlayback ? "是" : "否");
			listViewItem.SubItems.Add(videoAdjustment.CenterCropPortrait ? "是" : "否");
			listViewItem.SubItems.Add("重置");
			listViewItem.ToolTipText = videos[i];
			list.Items.Add(listViewItem);
		}
		if (selectedIndices != null)
		{
			foreach (int selectedIndex in selectedIndices)
			{
				if (selectedIndex >= 0 && selectedIndex < list.Items.Count)
				{
					list.Items[selectedIndex].Selected = true;
				}
			}
		}
		list.EndUpdate();
		PositionVideoListEmptyHint(list);
		countLabel.Text = "共 " + videos.Count + " 个视频";
		ResizeColumns();
	}

	private void HookVideoAdjustmentEditor(ListView list, List<string> videos, Dictionary<string, VideoAdjustmentSettings> settings, VideoAdjustmentEditor editor, Action<IEnumerable<int>> refresh, Action<string> showStatus)
	{
		Action copyAdjustment = delegate
		{
			if (_isRunning || list.SelectedIndices.Count == 0)
			{
				showStatus("请先选择一条要复制参数的视频。");
			}
			else
			{
				int num = list.SelectedIndices[0];
				if (num >= 0 && num < videos.Count)
				{
					_copiedVideoAdjustment = GetVideoAdjustment(settings, videos[num]).Clone();
					LoadVideoAdjustmentEditor(editor, _copiedVideoAdjustment);
					showStatus("已复制该视频的翻转、倒放、横屏裁中、缩放、速率和音量参数，可粘贴到其他视频。");
				}
			}
		};
		Action pasteAdjustment = delegate
		{
			if (!_isRunning)
			{
				if (_copiedVideoAdjustment == null)
				{
					showStatus("还没有复制参数，请先选择一条视频并点击“复制参数”。");
				}
				else
				{
					List<int> list2 = (from int x in list.SelectedIndices
						orderby x
						select x).ToList();
					if (list2.Count == 0)
					{
						showStatus("请先选择要粘贴参数的视频；可以按住 Ctrl 或 Shift 多选。");
					}
					else
					{
						foreach (int item in list2)
						{
							if (item >= 0 && item < videos.Count)
							{
								settings[videos[item]] = _copiedVideoAdjustment.Clone();
							}
						}
						LoadVideoAdjustmentEditor(editor, _copiedVideoAdjustment);
						refresh(list2);
						showStatus("已把复制的参数粘贴到所选 " + list2.Count + " 个视频。");
					}
				}
			}
		};
		list.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs e)
		{
			DrawVideoListSubItem(e, videos, settings);
		};
		list.MouseDown += delegate(object sender, MouseEventArgs e)
		{
			if (!_isRunning && TryGetVideoListCell(list, e.Location, out var row, out var column) && row < videos.Count)
			{
				if (e.Button == MouseButtons.Right)
				{
					if (!list.Items[row].Selected)
					{
						foreach (ListViewItem item2 in list.Items)
						{
							item2.Selected = false;
						}
						list.Items[row].Selected = true;
					}
				}
				else if (e.Button == MouseButtons.Left && column >= 3)
				{
					list.Items[row].Selected = true;
					switch (column)
					{
					case 3:
					{
						VideoAdjustmentSettings videoAdjustmentSettings4 = GetVideoAdjustment(settings, videos[row]).Clone();
						videoAdjustmentSettings4.HorizontalFlip = !videoAdjustmentSettings4.HorizontalFlip;
						settings[videos[row]] = videoAdjustmentSettings4;
						UpdateVideoAdjustmentListRow(list, videos, settings, row);
						LoadVideoAdjustmentEditor(editor, videoAdjustmentSettings4);
						showStatus("已" + (videoAdjustmentSettings4.HorizontalFlip ? "启用" : "取消") + "该视频的水平翻转。");
						break;
					}
					case 7:
					{
						VideoAdjustmentSettings videoAdjustmentSettings3 = GetVideoAdjustment(settings, videos[row]).Clone();
						videoAdjustmentSettings3.ReversePlayback = !videoAdjustmentSettings3.ReversePlayback;
						settings[videos[row]] = videoAdjustmentSettings3;
						UpdateVideoAdjustmentListRow(list, videos, settings, row);
						LoadVideoAdjustmentEditor(editor, videoAdjustmentSettings3);
						showStatus("已" + (videoAdjustmentSettings3.ReversePlayback ? "启用" : "取消") + "该视频的倒放。音频也会同步倒放。");
						break;
					}
					case 8:
					{
						VideoAdjustmentSettings videoAdjustmentSettings2 = GetVideoAdjustment(settings, videos[row]).Clone();
						videoAdjustmentSettings2.CenterCropPortrait = !videoAdjustmentSettings2.CenterCropPortrait;
						settings[videos[row]] = videoAdjustmentSettings2;
						UpdateVideoAdjustmentListRow(list, videos, settings, row);
						LoadVideoAdjustmentEditor(editor, videoAdjustmentSettings2);
						showStatus("已" + (videoAdjustmentSettings2.CenterCropPortrait ? "启用" : "取消") + "该视频的横屏裁中间；横屏会转为 9:16 竖屏并保留中央画面。");
						break;
					}
					case 9:
					{
						VideoAdjustmentSettings videoAdjustmentSettings = new VideoAdjustmentSettings();
						settings[videos[row]] = videoAdjustmentSettings;
						UpdateVideoAdjustmentListRow(list, videos, settings, row);
						LoadVideoAdjustmentEditor(editor, videoAdjustmentSettings);
						showStatus("已重置该视频：横屏裁中开启，翻转和倒放关闭，缩放 1×、速率 1×、音量 100%。");
						break;
					}
					default:
						editor.DragRow = row;
						editor.DragColumn = column;
						list.Capture = true;
						ApplyVideoSliderPoint(list, videos, settings, editor, row, column, e.X);
						break;
					}
				}
			}
		};
		list.MouseMove += delegate(object sender, MouseEventArgs e)
		{
			if (editor.DragRow >= 0 && editor.DragColumn >= 4 && e.Button == MouseButtons.Left)
			{
				ApplyVideoSliderPoint(list, videos, settings, editor, editor.DragRow, editor.DragColumn, e.X);
			}
			else
			{
				list.Cursor = ((TryGetVideoListCell(list, e.Location, out var _, out var column) && column >= 3) ? Cursors.Hand : Cursors.Default);
			}
		};
		list.MouseUp += delegate
		{
			if (editor.DragRow >= 0)
			{
				VideoAdjustmentSettings videoAdjustment = GetVideoAdjustment(settings, videos[editor.DragRow]);
				showStatus("已直接调整该视频：" + DescribeVideoAdjustment(videoAdjustment));
			}
			editor.DragRow = -1;
			editor.DragColumn = -1;
			list.Capture = false;
		};
		list.SelectedIndexChanged += delegate
		{
			if (list.SelectedIndices.Count != 0)
			{
				int num = list.SelectedIndices[0];
				if (num >= 0 && num < videos.Count)
				{
					LoadVideoAdjustmentEditor(editor, GetVideoAdjustment(settings, videos[num]));
				}
			}
		};
		editor.ApplySelected.Click += delegate
		{
			if (!_isRunning)
			{
				List<int> list2 = (from int x in list.SelectedIndices
					orderby x
					select x).ToList();
				if (list2.Count == 0)
				{
					showStatus("请先在列表中选择要调整的视频。");
				}
				else
				{
					VideoAdjustmentSettings videoAdjustmentSettings = CaptureVideoAdjustmentEditor(editor);
					foreach (int item3 in list2)
					{
						if (item3 >= 0 && item3 < videos.Count)
						{
							settings[videos[item3]] = videoAdjustmentSettings.Clone();
						}
					}
					refresh(list2);
					showStatus("已把画面、速率和音量设置应用到所选 " + list2.Count + " 个视频。");
				}
			}
		};
		editor.ApplyAll.Click += delegate
		{
			if (!_isRunning && videos.Count != 0)
			{
				VideoAdjustmentSettings videoAdjustmentSettings = CaptureVideoAdjustmentEditor(editor);
				for (int i = 0; i < videos.Count; i++)
				{
					settings[videos[i]] = videoAdjustmentSettings.Clone();
				}
				refresh(null);
				showStatus("已把当前设置应用到列表中的全部 " + videos.Count + " 个视频。");
			}
		};
		editor.CopyAdjustment.Click += delegate
		{
			copyAdjustment();
		};
		editor.PasteAdjustment.Click += delegate
		{
			pasteAdjustment();
		};
		list.KeyDown += delegate(object sender, KeyEventArgs e)
		{
			if (e.Control)
			{
				if (e.KeyCode == Keys.C)
				{
					copyAdjustment();
					e.Handled = true;
					e.SuppressKeyPress = true;
				}
				else if (e.KeyCode == Keys.V)
				{
					pasteAdjustment();
					e.Handled = true;
					e.SuppressKeyPress = true;
				}
			}
		};
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		ToolStripMenuItem copyItem = new ToolStripMenuItem("复制本条参数    Ctrl+C");
		ToolStripMenuItem pasteItem = new ToolStripMenuItem("粘贴到所选    Ctrl+V");
		copyItem.Click += delegate
		{
			copyAdjustment();
		};
		pasteItem.Click += delegate
		{
			pasteAdjustment();
		};
		contextMenuStrip.Items.Add(copyItem);
		contextMenuStrip.Items.Add(pasteItem);
		contextMenuStrip.Opening += delegate
		{
			copyItem.Enabled = !_isRunning && list.SelectedIndices.Count > 0;
			pasteItem.Enabled = !_isRunning && _copiedVideoAdjustment != null && list.SelectedIndices.Count > 0;
		};
		list.ContextMenuStrip = contextMenuStrip;
	}

	private static void DrawVideoListColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
	{
		using (Brush brush = new SolidBrush(Color.FromArgb(239, 243, 248)))
		{
			e.Graphics.FillRectangle(brush, e.Bounds);
		}
		using (Pen pen = new Pen(BorderColor))
		{
			e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
			e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 5, e.Bounds.Right - 1, e.Bounds.Bottom - 5);
		}
		using Font font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		TextRenderer.DrawText(e.Graphics, e.Header.Text, font, e.Bounds, InkColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}

	private static void DrawVideoListSubItem(DrawListViewSubItemEventArgs e, List<string> videos, Dictionary<string, VideoAdjustmentSettings> settings)
	{
		Color color = (e.Item.Selected ? Color.FromArgb(232, 240, 254) : ((e.ItemIndex % 2 == 0) ? SurfaceColor : Color.FromArgb(249, 251, 253)));
		using (Brush brush = new SolidBrush(color))
		{
			e.Graphics.FillRectangle(brush, e.Bounds);
		}
		using (Pen pen = new Pen(Color.FromArgb(232, 237, 243)))
		{
			e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
			e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
		}
		if (e.ItemIndex < 0 || e.ItemIndex >= videos.Count)
		{
			return;
		}
		VideoAdjustmentSettings videoAdjustment = GetVideoAdjustment(settings, videos[e.ItemIndex]);
		if (e.ColumnIndex <= 2)
		{
			TextFormatFlags textFormatFlags = ((e.ColumnIndex == 0) ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Default);
			Rectangle bounds = Rectangle.Inflate(e.Bounds, -7, 0);
			using Font font = new Font("Microsoft YaHei UI", 9f);
			TextRenderer.DrawText(e.Graphics, e.SubItem.Text, font, bounds, InkColor, textFormatFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
			return;
		}
		if (e.ColumnIndex == 3)
		{
			Rectangle rectangle = new Rectangle(e.Bounds.Left + (e.Bounds.Width - 16) / 2, e.Bounds.Top + (e.Bounds.Height - 16) / 2, 16, 16);
			ControlPaint.DrawCheckBox(e.Graphics, rectangle, videoAdjustment.HorizontalFlip ? ButtonState.Checked : ButtonState.Normal);
			return;
		}
		if (e.ColumnIndex == 7)
		{
			Rectangle rectangle2 = new Rectangle(e.Bounds.Left + (e.Bounds.Width - 16) / 2, e.Bounds.Top + (e.Bounds.Height - 16) / 2, 16, 16);
			ControlPaint.DrawCheckBox(e.Graphics, rectangle2, videoAdjustment.ReversePlayback ? ButtonState.Checked : ButtonState.Normal);
			return;
		}
		if (e.ColumnIndex == 8)
		{
			Rectangle rectangle3 = new Rectangle(e.Bounds.Left + (e.Bounds.Width - 16) / 2, e.Bounds.Top + (e.Bounds.Height - 16) / 2, 16, 16);
			ControlPaint.DrawCheckBox(e.Graphics, rectangle3, videoAdjustment.CenterCropPortrait ? ButtonState.Checked : ButtonState.Normal);
			return;
		}
		if (e.ColumnIndex == 9)
		{
			Rectangle rectangle4 = Rectangle.Inflate(e.Bounds, -6, -6);
			using (Brush brush2 = new SolidBrush(Color.FromArgb(248, 250, 252)))
			{
				e.Graphics.FillRectangle(brush2, rectangle4);
			}
			using (Pen pen2 = new Pen(Color.FromArgb(203, 213, 225)))
			{
				e.Graphics.DrawRectangle(pen2, rectangle4.X, rectangle4.Y, rectangle4.Width - 1, rectangle4.Height - 1);
			}
			using Font font2 = new Font("Microsoft YaHei UI", 8f);
			TextRenderer.DrawText(e.Graphics, "重置", font2, rectangle4, MutedColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			return;
		}
		double num;
		double num2;
		double num3;
		string text;
		if (e.ColumnIndex == 4)
		{
			num = videoAdjustment.ScaleRatio;
			num2 = 0.1;
			num3 = 3.0;
			text = FfmpegNumber(num) + "×";
		}
		else if (e.ColumnIndex == 5)
		{
			num = videoAdjustment.SpeedRatio;
			num2 = 0.1;
			num3 = 3.0;
			text = FfmpegNumber(num) + "×";
		}
		else
		{
			num = videoAdjustment.VolumePercent;
			num2 = 0.0;
			num3 = 300.0;
			text = videoAdjustment.VolumePercent + "%";
		}
		Rectangle bounds2 = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + 1, e.Bounds.Width - 4, 14);
		using (Font font3 = new Font("Microsoft YaHei UI", 8f))
		{
			TextRenderer.DrawText(e.Graphics, text, font3, bounds2, InkColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}
		int num4 = e.Bounds.Left + 8;
		int num5 = e.Bounds.Right - 8;
		int num6 = e.Bounds.Bottom - 8;
		using (Pen pen3 = new Pen(Color.FromArgb(203, 213, 225), 3f))
		{
			e.Graphics.DrawLine(pen3, num4, num6, num5, num6);
		}
		double num7 = Math.Max(0.0, Math.Min(1.0, (num - num2) / Math.Max(0.0001, num3 - num2)));
		int num8 = num4 + (int)Math.Round((double)(num5 - num4) * num7);
		using (Pen pen4 = new Pen(AccentColor, 3f))
		{
			e.Graphics.DrawLine(pen4, num4, num6, num8, num6);
		}
		using Brush brush3 = new SolidBrush(AccentColor);
		e.Graphics.FillEllipse(brush3, num8 - 4, num6 - 4, 8, 8);
	}

	private static bool TryGetVideoListCell(ListView list, Point location, out int row, out int column)
	{
		row = -1;
		column = -1;
		ListViewItem itemAt = list.GetItemAt(location.X, location.Y);
		if (itemAt == null)
		{
			return false;
		}
		row = itemAt.Index;
		for (int i = 1; i < itemAt.SubItems.Count; i++)
		{
			if (itemAt.SubItems[i].Bounds.Contains(location))
			{
				column = i;
				return true;
			}
		}
		Rectangle rectangle = new Rectangle(itemAt.Bounds.Left, itemAt.Bounds.Top, (list.Columns.Count > 0) ? list.Columns[0].Width : itemAt.Bounds.Width, itemAt.Bounds.Height);
		if (rectangle.Contains(location))
		{
			column = 0;
			return true;
		}
		return false;
	}

	private static void ApplyVideoSliderPoint(ListView list, List<string> videos, Dictionary<string, VideoAdjustmentSettings> settings, VideoAdjustmentEditor editor, int row, int column, int mouseX)
	{
		if (row >= 0 && row < videos.Count && column >= 4 && column <= 6)
		{
			Rectangle bounds = list.Items[row].SubItems[column].Bounds;
			double num = Math.Max(0.0, Math.Min(1.0, ((double)mouseX - ((double)bounds.Left + 8.0)) / Math.Max(1.0, (double)bounds.Width - 16.0)));
			VideoAdjustmentSettings videoAdjustmentSettings = GetVideoAdjustment(settings, videos[row]).Clone();
			switch (column)
			{
			case 4:
				videoAdjustmentSettings.ScaleRatio = Math.Round(0.1 + num * 2.9, 1);
				break;
			case 5:
				videoAdjustmentSettings.SpeedRatio = Math.Round(0.1 + num * 2.9, 1);
				break;
			default:
				videoAdjustmentSettings.VolumePercent = Math.Max(0, Math.Min(300, (int)Math.Round(num * 60.0) * 5));
				break;
			}
			settings[videos[row]] = videoAdjustmentSettings;
			UpdateVideoAdjustmentListRow(list, videos, settings, row);
			LoadVideoAdjustmentEditor(editor, videoAdjustmentSettings);
		}
	}

	private static void UpdateVideoAdjustmentListRow(ListView list, List<string> videos, Dictionary<string, VideoAdjustmentSettings> settings, int row)
	{
		if (row >= 0 && row < videos.Count && row < list.Items.Count)
		{
			VideoAdjustmentSettings videoAdjustment = GetVideoAdjustment(settings, videos[row]);
			ListViewItem listViewItem = list.Items[row];
			listViewItem.SubItems[3].Text = (videoAdjustment.HorizontalFlip ? "是" : "否");
			listViewItem.SubItems[4].Text = FfmpegNumber(videoAdjustment.ScaleRatio) + "×";
			listViewItem.SubItems[5].Text = FfmpegNumber(videoAdjustment.SpeedRatio) + "×";
			listViewItem.SubItems[6].Text = videoAdjustment.VolumePercent + "%";
			listViewItem.SubItems[7].Text = (videoAdjustment.ReversePlayback ? "是" : "否");
			listViewItem.SubItems[8].Text = (videoAdjustment.CenterCropPortrait ? "是" : "否");
			list.Invalidate(listViewItem.Bounds);
		}
	}

	private static void LoadVideoAdjustmentEditor(VideoAdjustmentEditor editor, VideoAdjustmentSettings adjustment)
	{
		editor.HorizontalFlip.Checked = adjustment.HorizontalFlip;
		editor.ReversePlayback.Checked = adjustment.ReversePlayback;
		editor.CenterCropPortrait.Checked = adjustment.CenterCropPortrait;
		editor.ScaleRatio.Value = ClampDecimal((decimal)adjustment.ScaleRatio, editor.ScaleRatio.Minimum, editor.ScaleRatio.Maximum);
		editor.SpeedRatio.Value = ClampDecimal((decimal)adjustment.SpeedRatio, editor.SpeedRatio.Minimum, editor.SpeedRatio.Maximum);
		editor.VolumePercent.Value = ClampDecimal(adjustment.VolumePercent, editor.VolumePercent.Minimum, editor.VolumePercent.Maximum);
	}

	private static VideoAdjustmentSettings CaptureVideoAdjustmentEditor(VideoAdjustmentEditor editor)
	{
		VideoAdjustmentSettings videoAdjustmentSettings = new VideoAdjustmentSettings();
		videoAdjustmentSettings.HorizontalFlip = editor.HorizontalFlip.Checked;
		videoAdjustmentSettings.ReversePlayback = editor.ReversePlayback.Checked;
		videoAdjustmentSettings.CenterCropPortrait = editor.CenterCropPortrait.Checked;
		videoAdjustmentSettings.ScaleRatio = decimal.ToDouble(editor.ScaleRatio.Value);
		videoAdjustmentSettings.SpeedRatio = decimal.ToDouble(editor.SpeedRatio.Value);
		videoAdjustmentSettings.VolumePercent = decimal.ToInt32(editor.VolumePercent.Value);
		return videoAdjustmentSettings;
	}

	private static decimal ClampDecimal(decimal value, decimal minimum, decimal maximum)
	{
		return Math.Min(maximum, Math.Max(minimum, value));
	}

	private static VideoAdjustmentSettings GetVideoAdjustment(Dictionary<string, VideoAdjustmentSettings> settings, string path)
	{
		if (settings == null || !settings.TryGetValue(path, out var value) || value == null)
		{
			return new VideoAdjustmentSettings();
		}
		return value;
	}

	private static Dictionary<string, VideoAdjustmentSettings> CaptureVideoAdjustmentSnapshot(IEnumerable<string> files, Dictionary<string, VideoAdjustmentSettings> settings)
	{
		Dictionary<string, VideoAdjustmentSettings> dictionary = new Dictionary<string, VideoAdjustmentSettings>(StringComparer.OrdinalIgnoreCase);
		foreach (string file in files)
		{
			dictionary[file] = GetVideoAdjustment(settings, file).Clone();
		}
		return dictionary;
	}

	private void ChooseOutputFolder()
	{
		if (_isRunning)
		{
			return;
		}
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "选择合并视频的保存位置";
		folderBrowserDialog.SelectedPath = _outputFolder.Text;
		folderBrowserDialog.ShowNewFolderButton = true;
		if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
		{
			_outputFolder.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private void OpenOutputFolder()
	{
		OpenFolder(_outputFolder.Text, _latestMergeOutputFolder);
	}

	private void OpenFolder(string folderText, string latestOutputFolder)
	{
		try
		{
			string text = folderText.Trim();
			if (text.Length != 0)
			{
				Directory.CreateDirectory(text);
				ProcessStartInfo processStartInfo = new ProcessStartInfo("explorer.exe", BuildExplorerOpenArguments(text, latestOutputFolder));
				processStartInfo.UseShellExecute = true;
				Process.Start(processStartInfo);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "无法打开输出目录：\n" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private void ShowCompletionAndOpenFolder(string message, string caption, MessageBoxIcon icon, string parentFolder, string batchFolder)
	{
		MessageBox.Show(this, message, caption, MessageBoxButtons.OK, icon);
		OpenFolder(parentFolder, batchFolder);
	}

	private static string BuildExplorerOpenArguments(string parentFolder, string latestOutputFolder)
	{
		string text = Path.GetFullPath(parentFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (!string.IsNullOrWhiteSpace(latestOutputFolder) && Directory.Exists(latestOutputFolder))
		{
			string text2 = Path.GetFullPath(latestOutputFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string value = text + Path.DirectorySeparatorChar;
			if (text2.StartsWith(value, StringComparison.OrdinalIgnoreCase))
			{
				return "/select," + QuoteArg(text2);
			}
		}
		return QuoteArg(text);
	}

	private void ChooseSplitOutputFolder()
	{
		if (_isRunning)
		{
			return;
		}
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "选择拆分视频的保存位置";
		folderBrowserDialog.SelectedPath = _splitOutputFolder.Text;
		folderBrowserDialog.ShowNewFolderButton = true;
		if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
		{
			_splitOutputFolder.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private void ChooseWatermarkImages(int index)
	{
		if (_isRunning)
		{
			return;
		}
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Title = "批量选择水印图片";
		openFileDialog.Filter = "图片文件（支持透明 PNG）|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*";
		openFileDialog.Multiselect = true;
		if (openFileDialog.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		SaveImageWatermarkSelection(index);
		int count = _imageWatermarkItems[index].Count;
		string[] fileNames = openFileDialog.FileNames;
		foreach (string file in fileNames)
		{
			List<WatermarkSettings> source = _imageWatermarkItems[index];
			Func<WatermarkSettings, bool> predicate = (WatermarkSettings x) => string.Equals(x.ImagePath, file, StringComparison.OrdinalIgnoreCase);
			if (!source.Any(predicate))
			{
				_imageWatermarkItems[index].Add(NewImageWatermarkSettings(file));
			}
		}
		_imageWatermarkEnabled[index].Checked = true;
		RefreshImageWatermarkList(index, Math.Min(count, _imageWatermarkItems[index].Count - 1));
	}

	private static WatermarkSettings NewImageWatermarkSettings(string path)
	{
		WatermarkSettings watermarkSettings = new WatermarkSettings();
		watermarkSettings.Enabled = true;
		watermarkSettings.IsText = false;
		watermarkSettings.ImagePath = path;
		watermarkSettings.FontSize = 36f;
		watermarkSettings.TextColor = Color.White;
		watermarkSettings.Opacity = 0.7;
		watermarkSettings.Position = "右下角";
		watermarkSettings.ImageWidthPercent = 20;
		watermarkSettings.OffsetX = 0;
		watermarkSettings.OffsetY = 0;
		watermarkSettings.AllowOverflow = false;
		watermarkSettings.StartSeconds = 0.0;
		watermarkSettings.ShowUntilEnd = true;
		watermarkSettings.EndSeconds = 5.0;
		watermarkSettings.EntryEffect = "淡入";
		watermarkSettings.EntryDurationSeconds = 1.0;
		watermarkSettings.ExitEffect = "淡出";
		watermarkSettings.ExitDurationSeconds = 1.0;
		watermarkSettings.StayEffect = "无";
		watermarkSettings.StayIntensity = 0.3;
		watermarkSettings.StayPeriodSeconds = 2.0;
		watermarkSettings.StayPauseSeconds = 0.0;
		watermarkSettings.SlideDurationSeconds = 3.0;
		return watermarkSettings;
	}

	private void RefreshImageWatermarkList(int libraryIndex, int selectedIndex)
	{
		SaveImageWatermarkSelection(libraryIndex);
		_imageWatermarkEditingIndex[libraryIndex] = -1;
		ListBox listBox = _imageWatermarkList[libraryIndex];
		listBox.BeginUpdate();
		listBox.Items.Clear();
		foreach (WatermarkSettings item in _imageWatermarkItems[libraryIndex])
		{
			listBox.Items.Add(Path.GetFileName(item.ImagePath));
		}
		listBox.EndUpdate();
		int count = _imageWatermarkItems[libraryIndex].Count;
		_imageWatermarkRandomCount[libraryIndex].Maximum = Math.Max(1, count);
		if (_imageWatermarkRandomCount[libraryIndex].Value > (decimal)Math.Max(1, count))
		{
			_imageWatermarkRandomCount[libraryIndex].Value = Math.Max(1, count);
		}
		if (count > 0)
		{
			listBox.SelectedIndex = Math.Max(0, Math.Min(count - 1, selectedIndex));
		}
		else
		{
			LoadImageWatermarkSelection(libraryIndex, null);
		}
		UpdateWatermarkUi();
	}

	private void ChangeSelectedWatermarkImage(int libraryIndex)
	{
		SaveImageWatermarkSelection(libraryIndex);
		int selectedIndex = _imageWatermarkList[libraryIndex].SelectedIndex;
		_imageWatermarkEditingIndex[libraryIndex] = selectedIndex;
		LoadImageWatermarkSelection(libraryIndex, (selectedIndex >= 0 && selectedIndex < _imageWatermarkItems[libraryIndex].Count) ? _imageWatermarkItems[libraryIndex][selectedIndex] : null);
		UpdateWatermarkUi();
	}

	private void SaveImageWatermarkSelection(int libraryIndex)
	{
		int num = _imageWatermarkEditingIndex[libraryIndex];
		if (num >= 0 && num < _imageWatermarkItems[libraryIndex].Count)
		{
			WatermarkSettings watermarkSettings = _imageWatermarkItems[libraryIndex][num];
			watermarkSettings.ImageWidthPercent = decimal.ToInt32(_imageWatermarkScale[libraryIndex].Value);
			watermarkSettings.Opacity = decimal.ToDouble(_imageWatermarkOpacity[libraryIndex].Value) / 100.0;
			watermarkSettings.Position = SelectedPosition(_imageWatermarkPosition[libraryIndex]);
			watermarkSettings.OffsetX = decimal.ToInt32(_imageWatermarkOffsetX[libraryIndex].Value);
			watermarkSettings.OffsetY = decimal.ToInt32(_imageWatermarkOffsetY[libraryIndex].Value);
			watermarkSettings.AllowOverflow = _imageWatermarkAllowOverflow[libraryIndex].Checked;
			watermarkSettings.StartSeconds = decimal.ToDouble(_imageWatermarkStart[libraryIndex].Value);
			watermarkSettings.ShowUntilEnd = _imageWatermarkShowUntilEnd[libraryIndex].Checked;
			watermarkSettings.EndSeconds = decimal.ToDouble(_imageWatermarkEnd[libraryIndex].Value);
			watermarkSettings.EntryEffect = SelectedEntryEffect(_imageWatermarkEntryEffect[libraryIndex]);
			watermarkSettings.EntryDurationSeconds = decimal.ToDouble(_imageWatermarkEntryDuration[libraryIndex].Value);
			watermarkSettings.ExitEffect = SelectedExitEffect(_imageWatermarkExitEffect[libraryIndex]);
			watermarkSettings.ExitDurationSeconds = decimal.ToDouble(_imageWatermarkExitDuration[libraryIndex].Value);
			watermarkSettings.StayEffect = SelectedStayEffect(_imageWatermarkStayEffect[libraryIndex]);
			watermarkSettings.StayIntensity = decimal.ToDouble(_imageWatermarkStayIntensity[libraryIndex].Value) / 100.0;
			watermarkSettings.StayPeriodSeconds = decimal.ToDouble(_imageWatermarkStayPeriod[libraryIndex].Value);
			watermarkSettings.StayPauseSeconds = decimal.ToDouble(_imageWatermarkStayPause[libraryIndex].Value);
			watermarkSettings.SlideDurationSeconds = decimal.ToDouble(_imageWatermarkSlideDuration[libraryIndex].Value);
		}
	}

	private void LoadImageWatermarkSelection(int libraryIndex, WatermarkSettings item)
	{
		bool flag = item != null;
		_imageWatermarkPath[libraryIndex].Text = (flag ? item.ImagePath : "请先批量导入图片");
		if (flag)
		{
			_imageWatermarkScale[libraryIndex].Value = ClampDecimal(item.ImageWidthPercent, _imageWatermarkScale[libraryIndex]);
			_imageWatermarkOpacity[libraryIndex].Value = ClampDecimal((decimal)(item.Opacity * 100.0), _imageWatermarkOpacity[libraryIndex]);
			SelectComboText(_imageWatermarkPosition[libraryIndex], item.Position, 0);
			_imageWatermarkOffsetX[libraryIndex].Value = ClampDecimal(item.OffsetX, _imageWatermarkOffsetX[libraryIndex]);
			_imageWatermarkOffsetY[libraryIndex].Value = ClampDecimal(item.OffsetY, _imageWatermarkOffsetY[libraryIndex]);
			_imageWatermarkAllowOverflow[libraryIndex].Checked = item.AllowOverflow;
			_imageWatermarkStart[libraryIndex].Value = ClampDecimal((decimal)item.StartSeconds, _imageWatermarkStart[libraryIndex]);
			_imageWatermarkShowUntilEnd[libraryIndex].Checked = item.ShowUntilEnd;
			_imageWatermarkEnd[libraryIndex].Value = ClampDecimal((decimal)item.EndSeconds, _imageWatermarkEnd[libraryIndex]);
			SelectComboText(_imageWatermarkEntryEffect[libraryIndex], NormalizeEntryEffect(item.EntryEffect), 1);
			_imageWatermarkEntryDuration[libraryIndex].Value = ClampDecimal((decimal)Math.Max(0.1, item.EntryDurationSeconds), _imageWatermarkEntryDuration[libraryIndex]);
			SelectComboText(_imageWatermarkExitEffect[libraryIndex], NormalizeExitEffect(item.ExitEffect), 1);
			_imageWatermarkExitDuration[libraryIndex].Value = ClampDecimal((decimal)Math.Max(0.1, item.ExitDurationSeconds), _imageWatermarkExitDuration[libraryIndex]);
			SelectComboText(_imageWatermarkStayEffect[libraryIndex], NormalizeStayEffect(item.StayEffect), 0);
			_imageWatermarkStayIntensity[libraryIndex].Value = ClampDecimal((decimal)(Math.Max(0.0, item.StayIntensity) * 100.0), _imageWatermarkStayIntensity[libraryIndex]);
			_imageWatermarkStayPeriod[libraryIndex].Value = ClampDecimal((decimal)Math.Max(0.2, item.StayPeriodSeconds), _imageWatermarkStayPeriod[libraryIndex]);
			_imageWatermarkStayPause[libraryIndex].Value = ClampDecimal((decimal)Math.Max(0.0, item.StayPauseSeconds), _imageWatermarkStayPause[libraryIndex]);
			_imageWatermarkSlideDuration[libraryIndex].Value = ClampDecimal((decimal)Math.Max(0.5, item.SlideDurationSeconds), _imageWatermarkSlideDuration[libraryIndex]);
		}
		else
		{
			_imageWatermarkAllowOverflow[libraryIndex].Checked = false;
		}
	}

	private static decimal ClampDecimal(decimal value, NumericUpDown control)
	{
		return Math.Max(control.Minimum, Math.Min(control.Maximum, value));
	}

	private static void SelectComboText(ComboBox combo, string text, int fallbackIndex)
	{
		int num = combo.Items.IndexOf(text);
		combo.SelectedIndex = ((num >= 0) ? num : Math.Max(0, Math.Min(combo.Items.Count - 1, fallbackIndex)));
	}

	private void RemoveSelectedWatermarkImage(int libraryIndex)
	{
		if (!_isRunning)
		{
			int selectedIndex = _imageWatermarkList[libraryIndex].SelectedIndex;
			if (selectedIndex >= 0)
			{
				_imageWatermarkEditingIndex[libraryIndex] = -1;
				_imageWatermarkItems[libraryIndex].RemoveAt(selectedIndex);
				RefreshImageWatermarkList(libraryIndex, Math.Min(selectedIndex, _imageWatermarkItems[libraryIndex].Count - 1));
			}
		}
	}

	private void ClearWatermarkImages(int libraryIndex)
	{
		if (!_isRunning)
		{
			_imageWatermarkEditingIndex[libraryIndex] = -1;
			_imageWatermarkItems[libraryIndex].Clear();
			RefreshImageWatermarkList(libraryIndex, -1);
		}
	}

	private void ApplyCurrentImageSettingsToAll(int libraryIndex)
	{
		if (_isRunning)
		{
			return;
		}
		SaveImageWatermarkSelection(libraryIndex);
		int selectedIndex = _imageWatermarkList[libraryIndex].SelectedIndex;
		if (selectedIndex < 0 || selectedIndex >= _imageWatermarkItems[libraryIndex].Count)
		{
			return;
		}
		WatermarkSettings watermarkSettings = _imageWatermarkItems[libraryIndex][selectedIndex];
		foreach (WatermarkSettings item in _imageWatermarkItems[libraryIndex])
		{
			item.ImageWidthPercent = watermarkSettings.ImageWidthPercent;
			item.Opacity = watermarkSettings.Opacity;
			item.Position = watermarkSettings.Position;
			item.OffsetX = watermarkSettings.OffsetX;
			item.OffsetY = watermarkSettings.OffsetY;
			item.AllowOverflow = watermarkSettings.AllowOverflow;
			item.StartSeconds = watermarkSettings.StartSeconds;
			item.ShowUntilEnd = watermarkSettings.ShowUntilEnd;
			item.EndSeconds = watermarkSettings.EndSeconds;
			item.EntryEffect = watermarkSettings.EntryEffect;
			item.EntryDurationSeconds = watermarkSettings.EntryDurationSeconds;
			item.ExitEffect = watermarkSettings.ExitEffect;
			item.ExitDurationSeconds = watermarkSettings.ExitDurationSeconds;
			item.StayEffect = watermarkSettings.StayEffect;
			item.StayIntensity = watermarkSettings.StayIntensity;
			item.StayPeriodSeconds = watermarkSettings.StayPeriodSeconds;
			item.StayPauseSeconds = watermarkSettings.StayPauseSeconds;
			item.SlideDurationSeconds = watermarkSettings.SlideDurationSeconds;
		}
		_watermarkHint.Text = "已把当前图片的大小、位置、透明度、单张时长、显示时间和动画参数应用到本图片库全部 " + _imageWatermarkItems[libraryIndex].Count + " 张图片。";
	}

	private void CenterWatermarkLayer(ComboBox position, NumericUpDown offsetX, NumericUpDown offsetY)
	{
		SetWatermarkPosition(position, offsetX, offsetY, "画面中央");
	}

	private void SetWatermarkPosition(ComboBox position, NumericUpDown offsetX, NumericUpDown offsetY, string targetPosition)
	{
		if (!_isRunning)
		{
			SelectComboText(position, targetPosition, 4);
			offsetX.Value = 0m;
			offsetY.Value = 0m;
		}
	}

	private void ChooseWatermarkOutputFolder()
	{
		if (_isRunning)
		{
			return;
		}
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "选择视频或图片加水印的总输出位置";
		folderBrowserDialog.SelectedPath = _watermarkOutputFolder.Text;
		folderBrowserDialog.ShowNewFolderButton = true;
		if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
		{
			_watermarkOutputFolder.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private void ChooseWatermarkColor(int index)
	{
		if (_isRunning)
		{
			return;
		}
		using ColorDialog colorDialog = new ColorDialog();
		colorDialog.Color = _watermarkColor[index];
		colorDialog.FullOpen = true;
		if (colorDialog.ShowDialog(this) == DialogResult.OK)
		{
			ref Color reference = ref _watermarkColor[index];
			reference = colorDialog.Color;
			_watermarkColorButton[index].BackColor = colorDialog.Color;
			int num = (colorDialog.Color.R * 299 + colorDialog.Color.G * 587 + colorDialog.Color.B * 114) / 1000;
			_watermarkColorButton[index].ForeColor = ((num < 130) ? Color.White : Color.Black);
		}
	}

	private void ChooseWatermarkBackgroundColor(int index)
	{
		if (_isRunning)
		{
			return;
		}
		using ColorDialog colorDialog = new ColorDialog();
		colorDialog.Color = _watermarkBackgroundColor[index];
		colorDialog.FullOpen = true;
		if (colorDialog.ShowDialog(this) == DialogResult.OK)
		{
			ref Color reference = ref _watermarkBackgroundColor[index];
			reference = colorDialog.Color;
			_textWatermarkBackgroundColorButton[index].BackColor = colorDialog.Color;
		}
	}

	private void ChooseWatermarkOutlineColor(int index)
	{
		if (_isRunning)
		{
			return;
		}
		using ColorDialog colorDialog = new ColorDialog();
		colorDialog.Color = _watermarkOutlineColor[index];
		colorDialog.FullOpen = true;
		if (colorDialog.ShowDialog(this) == DialogResult.OK)
		{
			ref Color reference = ref _watermarkOutlineColor[index];
			reference = colorDialog.Color;
			ApplyColorButton(_textWatermarkOutlineColorButton[index], colorDialog.Color, chooseReadableText: false);
		}
	}

	private void UpdateWatermarkUi()
	{
		if (_textWatermarkEnabled[0] == null)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < 3; i++)
		{
			bool flag = _textWatermarkEnabled[i].Checked && !_isRunning;
			_watermarkText[i].Enabled = flag;
			_watermarkFontSize[i].Enabled = flag;
			_textWatermarkMaxWidth[i].Enabled = flag;
			_textWatermarkSafeMargin[i].Enabled = flag && !_textWatermarkAllowOverflow[i].Checked;
			_textWatermarkAllowOverflow[i].Enabled = flag;
			_textWatermarkAboveImages[i].Enabled = flag;
			_watermarkFontFamily[i].Enabled = flag;
			_watermarkFontBold[i].Enabled = flag;
			_watermarkFontItalic[i].Enabled = flag;
			_watermarkColorButton[i].Enabled = flag;
			_textWatermarkAlignment[i].Enabled = flag;
			_textWatermarkOutlineEnabled[i].Enabled = flag;
			bool enabled = flag && _textWatermarkOutlineEnabled[i].Checked;
			_textWatermarkOutlineColorButton[i].Enabled = enabled;
			_textWatermarkOutlineWidth[i].Enabled = enabled;
			_textWatermarkOpacity[i].Enabled = flag;
			_textWatermarkPosition[i].Enabled = flag;
			_textWatermarkOffsetX[i].Enabled = flag;
			_textWatermarkOffsetY[i].Enabled = flag;
			_textWatermarkTopCenter[i].Enabled = flag;
			_textWatermarkCenter[i].Enabled = flag;
			_textWatermarkBottomCenter[i].Enabled = flag;
			_textWatermarkStart[i].Enabled = flag;
			_textWatermarkShowUntilEnd[i].Enabled = flag;
			_textWatermarkEnd[i].Enabled = flag && !_textWatermarkShowUntilEnd[i].Checked;
			_textWatermarkEntryEffect[i].Enabled = flag;
			_textWatermarkEntryDuration[i].Enabled = flag;
			_textWatermarkExitEffect[i].Enabled = flag && !_textWatermarkShowUntilEnd[i].Checked;
			_textWatermarkExitDuration[i].Enabled = flag && !_textWatermarkShowUntilEnd[i].Checked;
			_textWatermarkStayEffect[i].Enabled = flag;
			bool enabled2 = flag && _textWatermarkStayEffect[i].SelectedIndex > 0;
			_textWatermarkStayIntensity[i].Enabled = enabled2;
			_textWatermarkStayPeriod[i].Enabled = enabled2;
			_textWatermarkStayPause[i].Enabled = enabled2;
			_textWatermarkBackgroundEnabled[i].Enabled = flag;
			bool enabled3 = flag && _textWatermarkBackgroundEnabled[i].Checked;
			_textWatermarkBackgroundStyle[i].Enabled = enabled3;
			_textWatermarkBackgroundColorButton[i].Enabled = enabled3;
			_textWatermarkBackgroundOpacity[i].Enabled = enabled3;
			_textWatermarkBackgroundPaddingX[i].Enabled = enabled3;
			_textWatermarkBackgroundPaddingY[i].Enabled = enabled3;
			_textWatermarkBackgroundRadius[i].Enabled = enabled3;
			if (_textWatermarkEnabled[i].Checked)
			{
				num++;
			}
		}
		for (int j = 0; j < 3; j++)
		{
			bool flag2 = _imageWatermarkList[j].SelectedIndex >= 0;
			bool flag3 = _imageWatermarkEnabled[j].Checked && flag2 && !_isRunning;
			_imageWatermarkList[j].Enabled = !_isRunning;
			_imageWatermarkBrowse[j].Enabled = !_isRunning;
			_imageWatermarkRemove[j].Enabled = flag2 && !_isRunning;
			_imageWatermarkClear[j].Enabled = _imageWatermarkItems[j].Count > 0 && !_isRunning;
			_imageWatermarkRandomCount[j].Enabled = _imageWatermarkEnabled[j].Checked && _imageWatermarkItems[j].Count > 0 && !_isRunning;
			_imageWatermarkAssignmentMode[j].Enabled = _imageWatermarkEnabled[j].Checked && _imageWatermarkItems[j].Count > 0 && !_isRunning;
			bool flag4 = _imageWatermarkEnabled[j].Checked && _imageWatermarkItems[j].Count > 1 && _imageWatermarkPlaybackMode[j].SelectedIndex == 1 && !_isRunning;
			_imageWatermarkPlaybackMode[j].Enabled = _imageWatermarkEnabled[j].Checked && _imageWatermarkItems[j].Count > 0 && !_isRunning;
			_imageWatermarkSwitchEffect[j].Enabled = flag4;
			_imageWatermarkSwitchDuration[j].Enabled = flag4 && ComboText(_imageWatermarkSwitchEffect[j]) != "直接切换";
			_imageWatermarkSwitchInterval[j].Enabled = flag4;
			_imageWatermarkScale[j].Enabled = flag3;
			_imageWatermarkOpacity[j].Enabled = flag3;
			_imageWatermarkPosition[j].Enabled = flag3;
			_imageWatermarkOffsetX[j].Enabled = flag3;
			_imageWatermarkOffsetY[j].Enabled = flag3;
			_imageWatermarkAllowOverflow[j].Enabled = flag3;
			_imageWatermarkCenter[j].Enabled = flag3;
			_imageWatermarkApplyAll[j].Enabled = flag3 && _imageWatermarkItems[j].Count > 1;
			_imageWatermarkStart[j].Enabled = flag3;
			_imageWatermarkShowUntilEnd[j].Enabled = flag3;
			_imageWatermarkEnd[j].Enabled = flag3 && !_imageWatermarkShowUntilEnd[j].Checked;
			_imageWatermarkSlideDuration[j].Enabled = flag3 && flag4;
			_imageWatermarkEntryEffect[j].Enabled = flag3;
			_imageWatermarkEntryDuration[j].Enabled = flag3;
			_imageWatermarkExitEffect[j].Enabled = flag3 && (flag4 || !_imageWatermarkShowUntilEnd[j].Checked);
			_imageWatermarkExitDuration[j].Enabled = flag3 && (flag4 || !_imageWatermarkShowUntilEnd[j].Checked);
			_imageWatermarkStayEffect[j].Enabled = flag3;
			bool enabled4 = flag3 && _imageWatermarkStayEffect[j].SelectedIndex > 0;
			_imageWatermarkStayIntensity[j].Enabled = enabled4;
			_imageWatermarkStayPeriod[j].Enabled = enabled4;
			_imageWatermarkStayPause[j].Enabled = enabled4;
			if (_imageWatermarkEnabled[j].Checked && _imageWatermarkItems[j].Count > 0)
			{
				num++;
			}
		}
		if (_watermarkHint != null)
		{
			_watermarkHint.Text = "已启用 " + num + " 个水印图层" + ((_watermarkOnMerge.Checked || _watermarkOnSplit.Checked || _watermarkOnSplitScreen.Checked) ? ("；将用于" + (_watermarkOnMerge.Checked ? "合并 " : "") + (_watermarkOnSplit.Checked ? "拆分 " : "") + (_watermarkOnSplitScreen.Checked ? "视频拼屏" : "")) : ("；也可直接对下方" + ((_watermarkSourceTabs != null && _watermarkSourceTabs.SelectedIndex == 1) ? "原图片" : "原视频") + "导出"));
		}
		RefreshSplitScreenWatermarkLayerChoices();
	}

	private void SetAllTransitionChecks(bool value)
	{
		if (!_isRunning)
		{
			for (int i = 0; i < _transitionEffects.Items.Count; i++)
			{
				_transitionEffects.SetItemChecked(i, value);
			}
		}
	}

	private void SetAllWatermarkStayEffectChecks(bool value)
	{
		if (!_isRunning)
		{
			for (int i = 0; i < _watermarkStayEffectPool.Items.Count; i++)
			{
				_watermarkStayEffectPool.SetItemChecked(i, value);
			}
			_watermarkHint.Text = (value ? "已全选全部停留效果；点击“随机分配”即可批量分配给文字和图片水印。" : "已清除停留效果池的选择。");
		}
	}

	private void RandomlyAssignWatermarkStayEffects()
	{
		if (_isRunning)
		{
			return;
		}
		List<string> list = (from object x in _watermarkStayEffectPool.Items
			select x.ToString()).ToList();
		Random random = new Random((Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode());
		for (int num = 0; num < 3; num++)
		{
			SelectComboText(_textWatermarkStayEffect[num], list[random.Next(list.Count)], 0);
			SaveImageWatermarkSelection(num);
			int selectedIndex = _imageWatermarkList[num].SelectedIndex;
			_imageWatermarkEditingIndex[num] = -1;
			foreach (WatermarkSettings item in _imageWatermarkItems[num])
			{
				item.StayEffect = list[random.Next(list.Count)];
			}
			if (_imageWatermarkItems[num].Count > 0)
			{
				RefreshImageWatermarkList(num, Math.Max(0, selectedIndex));
			}
		}
		UpdateWatermarkUi();
		SaveUserSettings();
		_watermarkHint.Text = "已从 " + list.Count + " 种停留动画中随机分配给全部文字和图片水印；可在各图层下方继续单独调整。";
	}

	private void ClearWatermarkStayEffects()
	{
		if (_isRunning)
		{
			return;
		}
		SetAllWatermarkStayEffectChecks(value: false);
		for (int i = 0; i < 3; i++)
		{
			_textWatermarkStayEffect[i].SelectedIndex = 0;
			_textWatermarkStayIntensity[i].Value = 30m;
			_textWatermarkStayPeriod[i].Value = 2m;
			_textWatermarkStayPause[i].Value = 0m;
			SaveImageWatermarkSelection(i);
			int selectedIndex = _imageWatermarkList[i].SelectedIndex;
			_imageWatermarkEditingIndex[i] = -1;
			foreach (WatermarkSettings item in _imageWatermarkItems[i])
			{
				item.StayEffect = "无";
				item.StayIntensity = 0.3;
				item.StayPeriodSeconds = 2.0;
				item.StayPauseSeconds = 0.0;
			}
			if (_imageWatermarkItems[i].Count > 0)
			{
				RefreshImageWatermarkList(i, Math.Max(0, selectedIndex));
			}
		}
		UpdateWatermarkUi();
		SaveUserSettings();
		_watermarkHint.Text = "已清除并重置全部水印停留效果；入场、退场、位置和透明度保持不变。";
	}

	private void UpdateSplitModeUi()
	{
		if (_splitMode != null)
		{
			bool flag = _splitMode.SelectedIndex == 0;
			_splitSeconds.Visible = flag;
			_splitSecondsLabel.Visible = flag;
			_splitTailMode.Visible = flag;
			_splitTailLabel.Visible = flag;
			_splitEqualParts.Visible = !flag;
			_splitPartsLabel.Visible = !flag;
			_splitSeconds.Enabled = flag && !_isRunning;
			_splitTailMode.Enabled = flag && !_isRunning;
			_splitEqualParts.Enabled = !flag && !_isRunning;
			if (_splitModeHint != null)
			{
				_splitModeHint.Text = (flag ? "尾部不足设定秒数时可并入上一段或直接舍弃，不会生成短尾片段。" : "每个源视频都根据自己的时长，平均切成指定份数。");
			}
		}
	}

	private void UpdateMergePlanningUi()
	{
		if (_limitGroupDuration != null)
		{
			bool enabled = _limitGroupDuration.Checked && !_isRunning;
			_maxGroupDuration.Enabled = enabled;
			_overlongVideoMode.Enabled = enabled;
			bool enabled2 = _similaritySort.Checked && !_isRunning;
			_similarityMode.Enabled = enabled2;
			_similarityThreshold.Enabled = enabled2;
			bool enabled3 = !_isRunning;
			_upButton.Enabled = enabled3;
			_downButton.Enabled = enabled3;
			_shuffleButton.Enabled = enabled3;
			_crossFolderButton.Enabled = enabled3;
			_combinationStartMode.Enabled = !_isRunning && _maxOutputCount.Value > 0m;
			if (_maxOutputCount.Value == 0m)
			{
				_combinationStartMode.SelectedIndex = 0;
			}
			if (_similaritySort.Checked && !_isRunning)
			{
				_statusLabel.Text = "已启用自动相似排序：处理时拥有最高优先级；点击手动排序按钮会自动退出该模式。";
			}
		}
	}

	private int RenderWatermarkVideo(string ffmpeg, string source, string outputPath, WatermarkProfile profile, string tempDirectory, Action<double> progress, out string error)
	{
		VideoInfo videoInfo = Probe(ffmpeg, source);
		if (!videoInfo.HasVideo || videoInfo.DurationSeconds <= 0.0)
		{
			error = "无法识别要添加水印的视频。";
			return -1;
		}
		VideoAdjustmentSettings videoAdjustment = GetVideoAdjustment(_activeWatermarkVideoAdjustments, source);
		double num = videoInfo.DurationSeconds / Math.Max(0.1, videoAdjustment.SpeedRatio);
		Size outputCanvasSize = GetOutputCanvasSize(videoInfo, videoAdjustment, _activeWatermarkOutputFrame);
		int num2 = outputCanvasSize.Width;
		int num3 = outputCanvasSize.Height;
		List<PreparedWatermarkLayer> layers;
		try
		{
			layers = PrepareWatermarkLayers(profile, num2, num3, tempDirectory, num);
		}
		catch (Exception ex)
		{
			error = "准备水印失败：" + ex.Message;
			return -1;
		}
		StringBuilder stringBuilder = new StringBuilder("-hide_banner -y -i ").Append(QuoteArg(source));
		AppendWatermarkInputs(stringBuilder, layers);
		string text = BuildAdjustedVideoFilterWithCanvasCrop(num2, num3, videoAdjustment, normalizeFrameRate: false, ShouldFillOutputCanvas(videoInfo, videoAdjustment, _activeWatermarkOutputFrame), ResolveCropPositionPercent(_activeWatermarkOutputFrame));
		List<string> list = new List<string>();
		list.Add("[0:v]" + text + "[base]");
		List<string> list2 = list;
		string currentVideo = "base";
		AppendAnimatedWatermarkFilters(list2, ref currentVideo, 1, layers);
		list2.Add("[" + currentVideo + "]format=yuv420p[vout]");
		stringBuilder.Append(" -filter_complex ").Append(QuoteArg(string.Join(";", list2.ToArray())));
		stringBuilder.Append(" -map [vout] -map 0:a:0? ");
		if (videoInfo.HasAudio)
		{
			stringBuilder.Append("-af ").Append(QuoteArg(BuildAdjustedAudioFilter(videoAdjustment))).Append(" ");
		}
		stringBuilder.Append("-c:v libx264 -preset fast -crf 17 -pix_fmt yuv420p -metadata:s:v:0 rotate=0 -c:a aac -b:a 192k");
		stringBuilder.Append(" -t ").Append(FfmpegNumber(num));
		stringBuilder.Append(" -movflags +faststart -progress pipe:1 -nostats ").Append(QuoteArg(outputPath));
		return RunFfmpeg(ffmpeg, stringBuilder.ToString(), num, progress, out error);
	}

	private int RenderWatermarkVideoReliable(string ffmpeg, string source, string outputPath, WatermarkProfile profile, string tempDirectory, Action<double> progress, StreamWriter log, string context, out string error, out bool usedSafeFallback)
	{
		usedSafeFallback = false;
		error = null;
		VideoInfo videoInfo = Probe(ffmpeg, source);
		VideoAdjustmentSettings videoAdjustment = GetVideoAdjustment(_activeWatermarkVideoAdjustments, source);
		double expectedDuration = videoInfo.DurationSeconds / Math.Max(0.1, videoAdjustment.SpeedRatio);
		for (int i = 1; i <= 3; i++)
		{
			if (_cancelRequested)
			{
				return -1;
			}
			TryDelete(outputPath);
			bool flag = i == 3;
			WatermarkProfile profile2 = (flag ? MakeSafeWatermarkProfile(profile) : profile);
			int num = RenderWatermarkVideo(ffmpeg, source, outputPath, profile2, tempDirectory, progress, out var error2);
			string reason = null;
			if (num == 0 && ValidateVideoOutput(ffmpeg, outputPath, expectedDuration, out reason))
			{
				usedSafeFallback = flag;
				if (flag)
				{
					log?.WriteLine("  提示：" + context + "使用安全入场方式补做成功，输出数量已补齐。");
				}
				return 0;
			}
			error = ((num == 0) ? reason : error2);
			TryDelete(outputPath);
			log?.WriteLine("  " + context + "第 " + i + " 次未通过校验，将自动补做：" + LastUsefulLines(error, 5));
		}
		return -1;
	}

	private static WatermarkProfile MakeSafeWatermarkProfile(WatermarkProfile profile)
	{
		WatermarkProfile watermarkProfile = new WatermarkProfile();
		watermarkProfile.Enabled = profile.Enabled;
		WatermarkProfile watermarkProfile2 = watermarkProfile;
		foreach (WatermarkSettings layer in profile.Layers)
		{
			WatermarkSettings watermarkSettings = CloneWatermarkSettings(layer);
			watermarkSettings.EntryEffect = "直接出现";
			watermarkSettings.EntryDurationSeconds = 0.1;
			watermarkSettings.ExitEffect = "直接消失";
			watermarkSettings.ExitDurationSeconds = 0.1;
			watermarkSettings.StayEffect = "无";
			watermarkProfile2.Layers.Add(watermarkSettings);
		}
		foreach (WatermarkImageLibrary imageLibrary in profile.ImageLibraries)
		{
			WatermarkImageLibrary watermarkImageLibrary = new WatermarkImageLibrary();
			watermarkImageLibrary.CandidateCount = imageLibrary.CandidateCount;
			watermarkImageLibrary.RandomAssignment = imageLibrary.RandomAssignment;
			watermarkImageLibrary.Slideshow = imageLibrary.Slideshow;
			watermarkImageLibrary.SwitchEffect = "直接切换";
			watermarkImageLibrary.SwitchDurationSeconds = 0.1;
			watermarkImageLibrary.SwitchIntervalSeconds = Math.Max(0.0, imageLibrary.SwitchIntervalSeconds);
			WatermarkImageLibrary watermarkImageLibrary2 = watermarkImageLibrary;
			foreach (WatermarkSettings image in imageLibrary.Images)
			{
				WatermarkSettings watermarkSettings2 = CloneWatermarkSettings(image);
				watermarkSettings2.EntryEffect = "直接出现";
				watermarkSettings2.EntryDurationSeconds = 0.1;
				watermarkSettings2.ExitEffect = "直接消失";
				watermarkSettings2.ExitDurationSeconds = 0.1;
				watermarkSettings2.StayEffect = "无";
				watermarkImageLibrary2.Images.Add(watermarkSettings2);
			}
			watermarkProfile2.ImageLibraries.Add(watermarkImageLibrary2);
		}
		return watermarkProfile2;
	}

	private bool ValidateVideoOutput(string ffmpeg, string outputPath, double expectedDuration, out string reason)
	{
		reason = null;
		if (!File.Exists(outputPath))
		{
			reason = "输出文件不存在";
			return false;
		}
		FileInfo fileInfo = new FileInfo(outputPath);
		if (fileInfo.Length < 1024)
		{
			reason = "输出文件过小或未写完整";
			return false;
		}
		VideoInfo videoInfo = Probe(ffmpeg, outputPath);
		if (!videoInfo.HasVideo || videoInfo.DurationSeconds <= 0.0)
		{
			reason = "输出文件无法重新读取";
			return false;
		}
		if (expectedDuration > 0.3)
		{
			double num = Math.Max(0.1, expectedDuration - Math.Max(0.5, expectedDuration * 0.08));
			if (videoInfo.DurationSeconds < num)
			{
				reason = "输出时长不足，预计 " + FfmpegNumber(expectedDuration) + " 秒，实际 " + FfmpegNumber(videoInfo.DurationSeconds) + " 秒";
				return false;
			}
		}
		return true;
	}

	private bool ValidateExactDurationOutput(string ffmpeg, string outputPath, double expectedDuration, out string reason)
	{
		if (!ValidateVideoOutput(ffmpeg, outputPath, expectedDuration, out reason))
		{
			return false;
		}
		VideoInfo videoInfo = Probe(ffmpeg, outputPath);
		double num = Math.Max(0.12, Math.Min(0.25, expectedDuration * 0.005));
		if (Math.Abs(videoInfo.DurationSeconds - expectedDuration) > num)
		{
			reason = "成品时长未统一，要求 " + FfmpegNumber(expectedDuration) + " 秒，实际 " + FfmpegNumber(videoInfo.DurationSeconds) + " 秒";
			return false;
		}
		return true;
	}

	private bool ValidateSplitOutputSet(string ffmpeg, string[] outputPaths, int expectedSegments, List<double> boundaries, double totalDuration, out string reason)
	{
		reason = null;
		if (outputPaths.Length != expectedSegments)
		{
			reason = "预计 " + expectedSegments + " 段，实际生成 " + outputPaths.Length + " 段";
			return false;
		}
		Array.Sort(outputPaths, StringComparer.OrdinalIgnoreCase);
		double num = 0.0;
		for (int i = 0; i < outputPaths.Length; i++)
		{
			double num2 = ((i < boundaries.Count) ? boundaries[i] : totalDuration);
			double expectedDuration = Math.Max(0.05, num2 - num);
			if (!ValidateVideoOutput(ffmpeg, outputPaths[i], expectedDuration, out var reason2))
			{
				reason = "第 " + (i + 1) + " 段未通过完整性校验：" + reason2;
				return false;
			}
			num = num2;
		}
		return true;
	}

	private bool RenderSplitSegmentsIndependently(string ffmpeg, string source, string outputFolder, string prefix, List<double> boundaries, double totalDuration, bool hasAudio, VideoAdjustmentSettings adjustment, string baseVideoFilter, Action<double> progress, out string error)
	{
		error = null;
		int segmentCount = boundaries.Count + 1;
		double num = 0.0;
		for (int i = 0; i < segmentCount; i++)
		{
			if (_cancelRequested)
			{
				error = "用户已取消";
				return false;
			}
			double num2 = ((i < boundaries.Count) ? boundaries[i] : totalDuration);
			double num3 = Math.Max(0.05, num2 - num);
			string text = Path.Combine(outputFolder, prefix + "_片段_" + (i + 1).ToString("000", CultureInfo.InvariantCulture) + ".mp4");
			TryDelete(text);
			string value = baseVideoFilter + ",trim=start=" + FfmpegNumber(num) + ":duration=" + FfmpegNumber(num3) + ",setpts=PTS-STARTPTS";
			StringBuilder stringBuilder = new StringBuilder("-hide_banner -y -i ").Append(QuoteArg(source));
			stringBuilder.Append(" -map 0:v:0 -map 0:a:0? -vf ").Append(QuoteArg(value));
			if (hasAudio)
			{
				string value2 = BuildAdjustedAudioFilter(adjustment) + ",atrim=start=" + FfmpegNumber(num) + ":duration=" + FfmpegNumber(num3) + ",asetpts=PTS-STARTPTS";
				stringBuilder.Append(" -af ").Append(QuoteArg(value2));
			}
			stringBuilder.Append(" -c:v libx264 -preset fast -crf 17 -pix_fmt yuv420p -metadata:s:v:0 rotate=0").Append(" -c:a aac -b:a 192k -t ").Append(FfmpegNumber(num3))
				.Append(" -movflags +faststart -progress pipe:1 -nostats ")
				.Append(QuoteArg(text));
			int completedSegments = i;
			if (RunFfmpeg(ffmpeg, stringBuilder.ToString(), num3, delegate(double p)
			{
				if (progress != null)
				{
					progress(((double)completedSegments + p) / (double)Math.Max(1, segmentCount));
				}
			}, out var errorText) != 0 || !File.Exists(text))
			{
				error = "第 " + (i + 1) + " 段补做失败：" + LastUsefulLines(errorText, 8);
				return false;
			}
			num = num2;
		}
		if (progress != null)
		{
			progress(1.0);
		}
		return true;
	}

	private void StartDirectWatermark()
	{
		if (_isRunning)
		{
			return;
		}
		if (_watermarkSourceTabs != null && _watermarkSourceTabs.SelectedIndex == 1)
		{
			StartDirectImageWatermark();
			return;
		}
		if (_watermarkVideos.Count == 0)
		{
			MessageBox.Show(this, "请先在水印页面添加一个或多个原视频。", "还没有视频", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		WatermarkProfile profile = CaptureWatermarkProfile(enabled: true);
		string text = ValidateWatermark(profile);
		if (text != null)
		{
			MessageBox.Show(this, text, "水印设置不完整", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		string ffmpeg = FindFfmpeg();
		if (ffmpeg == null)
		{
			MessageBox.Show(this, "没有找到 FFmpeg。请保持便携版文件完整。", "缺少 FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		string output = _watermarkOutputFolder.Text.Trim();
		if (output.Length == 0)
		{
			MessageBox.Show(this, "请选择水印视频总输出目录。", "缺少输出目录", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		try
		{
			Directory.CreateDirectory(output);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "无法创建输出目录：\n" + ex.Message, "输出目录不可用", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		List<string> files = new List<string>(_watermarkVideos);
		_activeWatermarkVideoAdjustments = CaptureVideoAdjustmentSnapshot(files, _watermarkVideoAdjustments);
		_activeWatermarkOutputFrame = CaptureOutputFrameSettings(_watermarkOutputFrameControls);
		_activeWatermarkBgmPlan = CaptureBgmPlan(_watermarkBgm);
		SetRunningState(running: true);
		_cancelRequested = false;
		_watermarkProgressBar.Value = 0;
		Task.Factory.StartNew(delegate
		{
			RunDirectWatermarkBatch(ffmpeg, files, output, profile);
		}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
	}

	private void StartDirectImageWatermark()
	{
		if (_isRunning)
		{
			return;
		}
		if (_watermarkSourceImages.Count == 0)
		{
			MessageBox.Show(this, "请先在“原图片加水印”中添加一张或多张图片。", "还没有图片", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		WatermarkProfile profile = CaptureWatermarkProfile(enabled: true);
		string text = ValidateWatermark(profile);
		if (text != null)
		{
			MessageBox.Show(this, text, "水印设置不完整", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		string output = _watermarkOutputFolder.Text.Trim();
		if (output.Length == 0)
		{
			MessageBox.Show(this, "请选择图片水印总输出目录。", "缺少输出目录", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		try
		{
			Directory.CreateDirectory(output);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "无法创建输出目录：\n" + ex.Message, "输出目录不可用", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		List<string> files = new List<string>(_watermarkSourceImages.Where(File.Exists));
		SetRunningState(running: true);
		_cancelRequested = false;
		_watermarkProgressBar.Value = 0;
		Task.Factory.StartNew(delegate
		{
			RunDirectImageWatermarkBatch(files, output, profile);
		}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
	}

	private void RunDirectImageWatermarkBatch(List<string> files, string outputParent, WatermarkProfile profile)
	{
		int succeeded = 0;
		int failed = 0;
		string outputFolder = null;
		try
		{
			outputFolder = CreateBatchOutputFolder(outputParent, "图片加水印");
			_latestWatermarkOutputFolder = outputFolder;
			List<WatermarkProfile> list = CreateWatermarkAssignments(profile, files.Count, expandSlideshows: false);
			for (int i = 0; i < files.Count; i++)
			{
				if (_cancelRequested)
				{
					throw new OperationCanceledException();
				}
				string text = files[i];
				string text2 = FindAvailableWatermarkImageOutput(outputFolder, text);
				string text3 = Path.Combine(Path.GetTempPath(), "ImageWatermark_" + Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(text3);
				bool flag = false;
				try
				{
					for (int j = 1; j <= 3; j++)
					{
						if (flag)
						{
							break;
						}
						if (_cancelRequested)
						{
							throw new OperationCanceledException();
						}
						TryDelete(text2);
						RenderWatermarkedImage(text, text2, list[i], text3);
						flag = ValidateImageOutput(text, text2);
					}
				}
				catch
				{
					flag = false;
				}
				finally
				{
					TryDeleteDirectory(text3);
				}
				if (flag)
				{
					succeeded++;
				}
				else
				{
					failed++;
					TryDelete(text2);
				}
				int num = i;
				UiWatermarkProgress(((double)i + 1.0) / (double)Math.Max(1, files.Count), "正在导出第 " + (num + 1) + "/" + files.Count + " 张水印图片");
			}
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_watermarkProgressBar.Value = 100;
				_watermarkStatusLabel.Text = "图片加水印完成：成功 " + succeeded + "，失败 " + failed + "。";
				ShowCompletionAndOpenFolder("图片加水印完成。\n\n成功：" + succeeded + "\n失败：" + failed + "\n\n本批次输出目录：\n" + outputFolder, (failed == 0) ? "导出完成" : "导出完成（有失败）", (failed == 0) ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation, outputParent, outputFolder);
			});
		}
		catch (OperationCanceledException)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_watermarkStatusLabel.Text = "图片水印导出已取消；已经完成的图片会保留。";
			});
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_watermarkStatusLabel.Text = "图片水印导出失败，请检查图片和水印设置。";
				MessageBox.Show(this, ex3.Message, "图片水印导出失败", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			});
		}
	}

	private static void RenderWatermarkedImage(string sourcePath, string outputPath, WatermarkProfile profile, string tempDirectory)
	{
		using Image image = Image.FromFile(sourcePath);
		ApplyExifOrientation(image);
		using Bitmap bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.CompositingMode = CompositingMode.SourceCopy;
		graphics.CompositingQuality = CompositingQuality.HighQuality;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.SmoothingMode = SmoothingMode.HighQuality;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.DrawImageUnscaled(image, 0, 0);
		graphics.CompositingMode = CompositingMode.SourceOver;
		string filename = PrepareCompositeWatermarkPng(profile, bitmap.Width, bitmap.Height, tempDirectory);
		using (Image image2 = Image.FromFile(filename))
		{
			graphics.DrawImageUnscaled(image2, 0, 0);
		}
		bitmap.Save(outputPath, ImageFormat.Png);
	}

	private static void ApplyExifOrientation(Image image)
	{
		try
		{
			if (!image.PropertyIdList.Contains(274))
			{
				return;
			}
			switch (BitConverter.ToUInt16(image.GetPropertyItem(274).Value, 0))
			{
			case 2:
				image.RotateFlip(RotateFlipType.RotateNoneFlipX);
				break;
			case 3:
				image.RotateFlip(RotateFlipType.Rotate180FlipNone);
				break;
			case 4:
				image.RotateFlip(RotateFlipType.Rotate180FlipX);
				break;
			case 5:
				image.RotateFlip(RotateFlipType.Rotate90FlipX);
				break;
			case 6:
				image.RotateFlip(RotateFlipType.Rotate90FlipNone);
				break;
			case 7:
				image.RotateFlip(RotateFlipType.Rotate270FlipX);
				break;
			case 8:
				image.RotateFlip(RotateFlipType.Rotate270FlipNone);
				break;
			}
			try
			{
				image.RemovePropertyItem(274);
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private static bool ValidateImageOutput(string sourcePath, string outputPath)
	{
		if (!File.Exists(outputPath) || new FileInfo(outputPath).Length < 128)
		{
			return false;
		}
		try
		{
			using Image image = Image.FromFile(sourcePath);
			using Image image2 = Image.FromFile(outputPath);
			ApplyExifOrientation(image);
			return image.Width == image2.Width && image.Height == image2.Height;
		}
		catch
		{
			return false;
		}
	}

	private void RunDirectWatermarkBatch(string ffmpeg, List<string> files, string outputParent, WatermarkProfile profile)
	{
		int succeeded = 0;
		int failed = 0;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string outputFolder = null;
		string path = null;
		try
		{
			outputFolder = CreateBatchOutputFolder(outputParent, "直接加水印");
			_latestWatermarkOutputFolder = outputFolder;
			path = Path.Combine(Path.GetTempPath(), "VideoBatch_Watermark_" + Guid.NewGuid().ToString("N") + ".log");
			List<WatermarkProfile> list3 = CreateWatermarkAssignments(profile, files.Count);
			Random random = new Random(Guid.NewGuid().GetHashCode());
			using (StreamWriter streamWriter = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
			{
				streamWriter.WriteLine("原视频直接加水印导出记录");
				streamWriter.WriteLine("开始时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				streamWriter.WriteLine("输出画幅：" + DescribeOutputFrame(_activeWatermarkOutputFrame));
				streamWriter.WriteLine("水印图层：" + WatermarkLayerCount(profile));
				streamWriter.WriteLine();
				for (int i = 0; i < files.Count; i++)
				{
					if (_cancelRequested)
					{
						throw new OperationCanceledException();
					}
					string text = files[i];
					WatermarkProfile profile2 = list3[i];
					int captured = i;
					UiWatermarkBatchProgress(i, 0.0, files.Count, "正在分析第 " + (i + 1) + "/" + files.Count + " 个原视频");
					VideoInfo videoInfo = Probe(ffmpeg, text);
					if (_cancelRequested)
					{
						throw new OperationCanceledException();
					}
					streamWriter.WriteLine("视频 " + (i + 1) + "：" + text);
					streamWriter.WriteLine("  素材调整：" + DescribeVideoAdjustment(GetVideoAdjustment(_activeWatermarkVideoAdjustments, text)));
					if (!videoInfo.HasVideo || videoInfo.DurationSeconds <= 0.0)
					{
						failed++;
						list2.Add(text + "（无法识别视频或时长）");
						streamWriter.WriteLine("  结果：失败，无法识别视频或时长");
						UiWatermarkBatchProgress(i + 1, 0.0, files.Count, "已处理 " + (i + 1) + "/" + files.Count + " 个水印视频");
						continue;
					}
					string text2 = FindAvailableWatermarkOutput(outputFolder, text);
					string text3 = Path.Combine(Path.GetTempPath(), "VideoWatermark_" + Guid.NewGuid().ToString("N"));
					Directory.CreateDirectory(text3);
					try
					{
						int num = RenderWatermarkVideoReliable(ffmpeg, text, text2, profile2, text3, delegate(double p)
						{
							UiWatermarkBatchProgress(captured, p, files.Count, "正在处理第 " + (captured + 1) + "/" + files.Count + " 个水印视频");
						}, streamWriter, "水印导出", out var error, out var _);
						if (_cancelRequested)
						{
							TryDelete(text2);
							throw new OperationCanceledException();
						}
						if (num == 0 && _activeWatermarkBgmPlan.Enabled)
						{
							string text4 = SelectBgm(_activeWatermarkBgmPlan, i, random);
							if (!ApplyBackgroundMusic(ffmpeg, text2, text4, _activeWatermarkBgmPlan.VolumePercent, delegate(double p)
							{
								UiWatermarkBatchProgress(captured, 0.85 + p * 0.15, files.Count, "正在为第 " + (captured + 1) + "/" + files.Count + " 个视频加入 BGM");
							}, out var error2))
							{
								num = -2;
								error = "BGM 配乐失败：" + error2;
							}
							else
							{
								streamWriter.WriteLine("  BGM：" + text4);
							}
						}
						if (num == 0 && File.Exists(text2) && new FileInfo(text2).Length > 0)
						{
							succeeded++;
							list.Add(text2);
							streamWriter.WriteLine("  输出：" + text2);
							streamWriter.WriteLine("  结果：成功");
						}
						else
						{
							failed++;
							list2.Add(text + "（补做 3 次后仍失败）");
							TryDelete(text2);
							streamWriter.WriteLine("  结果：失败，" + LastUsefulLines(error, 10));
						}
					}
					finally
					{
						TryDeleteDirectory(text3);
					}
					UiWatermarkBatchProgress(i + 1, 0.0, files.Count, "已处理 " + (i + 1) + "/" + files.Count + " 个水印视频");
					streamWriter.WriteLine();
				}
				streamWriter.WriteLine("完成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				streamWriter.WriteLine("成功：" + succeeded + "，失败：" + failed);
				streamWriter.WriteLine("数量对账：输入 " + files.Count + "，有效输出 " + list.Count + ((list.Count == files.Count) ? "，一致" : ("，缺少 " + (files.Count - list.Count))));
			}
			WriteOutputReconciliation(outputFolder, "直接加水印", files.Count, list, list2);
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_watermarkProgressBar.Value = 100;
				_watermarkStatusLabel.Text = "直接水印导出完成：成功 " + succeeded + "，失败 " + failed + "。";
				ShowCompletionAndOpenFolder("水印视频导出完成。\n\n成功：" + succeeded + "\n失败：" + failed + "\n\n本批次输出目录：\n" + outputFolder, (failed == 0) ? "导出完成" : "导出完成（有失败）", (failed == 0) ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation, outputParent, outputFolder);
			});
		}
		catch (OperationCanceledException)
		{
			list2.Add("任务由用户取消，尚未处理的输入未导出");
			WriteOutputReconciliation(outputFolder, "直接加水印", files.Count, list, list2);
			TryAppendLog(path, "任务已由用户取消：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_watermarkStatusLabel.Text = "水印导出已取消；已经完成的输出会保留。";
			});
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			list2.Add("任务异常中断：" + ex3.Message);
			WriteOutputReconciliation(outputFolder, "直接加水印", files.Count, list, list2);
			TryAppendLog(path, "未处理异常：" + ex3);
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_watermarkStatusLabel.Text = "水印导出遇到错误，请检查素材与水印设置后重试。";
				MessageBox.Show(this, "水印导出遇到错误：\n" + ex3.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			});
		}
		finally
		{
			TryDelete(path);
		}
	}

	private void StartMerge()
	{
		if (_isRunning)
		{
			return;
		}
		if (_videos.Count == 0)
		{
			MessageBox.Show(this, "请先拖入或添加视频。", "还没有视频", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		string ffmpeg = FindFfmpeg();
		if (ffmpeg == null)
		{
			MessageBox.Show(this, "没有找到 FFmpeg。\n\n请把 ffmpeg.exe 和它需要的 DLL 放在本程序同一文件夹，或把 FFmpeg 加入系统 PATH。", "缺少 FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		string output = _outputFolder.Text.Trim();
		if (output.Length == 0)
		{
			MessageBox.Show(this, "请选择合并视频总输出目录。", "缺少输出目录", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		try
		{
			Directory.CreateDirectory(output);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "无法创建输出目录：\n" + ex.Message, "输出目录不可用", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		WatermarkProfile watermark = CaptureWatermarkProfile(_watermarkOnMerge.Checked);
		string text = ValidateWatermark(watermark);
		if (text != null)
		{
			_tabs.SelectedIndex = 2;
			MessageBox.Show(this, text, "水印设置不完整", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		MergePlanningOptions planning = CaptureMergePlanningOptions();
		TransitionPlan transition = CaptureTransitionSettings();
		List<string> files = new List<string>(_videos);
		_activeMergeVideoAdjustments = CaptureVideoAdjustmentSnapshot(files, _mergeVideoAdjustments);
		_activeMergeBgmPlan = CaptureBgmPlan(_mergeBgm);
		_activeMergeOutputFrame = CaptureOutputFrameSettings(_mergeOutputFrameControls);
		_activeMergeFillCanvas = _mergeCanvasFitMode.SelectedIndex != 1;
		bool fallback = _autoFallback.Checked;
		SetRunningState(running: true);
		_cancelRequested = false;
		_progressBar.Value = 0;
		Task.Factory.StartNew(delegate
		{
			RunBatchPlanned(ffmpeg, files, output, planning, fallback, transition, watermark);
		}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
	}

	private async void StartMergePreview()
	{
		if (_isRunning)
		{
			MessageBox.Show(this, "当前有正在执行的渲染或导出任务，请稍候再试。", "任务进行中", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		if (_videos.Count == 0)
		{
			MessageBox.Show(this, "请先拖入或添加要合并的视频。", "还没有视频", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		string ffmpeg = FindFfmpeg();
		if (string.IsNullOrWhiteSpace(ffmpeg))
		{
			MessageBox.Show(this, "没有找到 FFmpeg。请保持便携版文件完整，或把 FFmpeg 加入系统 PATH。", "缺少 FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}

		List<string> validFiles = _videos.Where(File.Exists).ToList();
		if (validFiles.Count == 0)
		{
			MessageBox.Show(this, "列表中的视频文件均不存在，请重新添加素材。", "素材未找到", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}

		WatermarkProfile watermark = CaptureWatermarkProfile(_watermarkOnMerge.Checked);
		MergePlanningOptions planning = CaptureMergePlanningOptions();
		TransitionPlan transition = CaptureTransitionSettings();
		List<string> files = new List<string>(validFiles);
		_activeMergeVideoAdjustments = CaptureVideoAdjustmentSnapshot(files, _mergeVideoAdjustments);
		_activeMergeBgmPlan = CaptureBgmPlan(_mergeBgm);
		_activeMergeOutputFrame = CaptureOutputFrameSettings(_mergeOutputFrameControls);
		_activeMergeFillCanvas = _mergeCanvasFitMode.SelectedIndex != 1;
		bool fallback = _autoFallback.Checked;

		int groupCount = Math.Max(1, Math.Min(planning.MaxItemsPerGroup, validFiles.Count));
		List<string> groupFiles = validFiles.Take(groupCount).ToList();

		double previewLimit = 8.0;
		if (planning.LimitDuration && planning.MaxDurationSeconds > 0)
		{
			previewLimit = Math.Min(planning.MaxDurationSeconds, 10.0);
		}

		TransitionPlan groupTransition = SelectTransitionPlanForOutput(transition, 0);
		string tempOutputFile = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_MergePreview_" + Guid.NewGuid().ToString("N") + ".mp4");

		_mergePreviewButton.Enabled = false;
		_mergePreviewButton.Text = "⏳ 正在合成预览...";
		_statusLabel.Text = "正在生成第 1 组效果动态试看 (包含转场、调色与画幅)...";

		MergeResult result = null;
		try
		{
			await Task.Run(() =>
			{
				using (StreamWriter dummyLog = new StreamWriter(Stream.Null))
				{
					result = MergeGroupWithProfileCapped(
						ffmpeg,
						groupFiles,
						tempOutputFile,
						fallback,
						groupTransition,
						watermark,
						previewLimit,
						planning.RandomClipRanges,
						delegate { },
						dummyLog
					);
				}

				if (result.Success && File.Exists(tempOutputFile) && _activeMergeBgmPlan.Enabled && _activeMergeBgmPlan.Files != null && _activeMergeBgmPlan.Files.Count > 0)
				{
					string bgmPath = SelectBgm(_activeMergeBgmPlan, 0, new Random());
					if (File.Exists(bgmPath))
					{
						ApplyBackgroundMusic(ffmpeg, tempOutputFile, bgmPath, _activeMergeBgmPlan.VolumePercent, delegate { }, out _);
					}
				}
			});
		}
		catch (Exception ex)
		{
			result = new MergeResult { Error = ex.Message };
		}
		finally
		{
			_mergePreviewButton.Enabled = true;
			_mergePreviewButton.Text = "▶ 播放合并预览 (第1组)";
		}

		if (result != null && result.Success && File.Exists(tempOutputFile))
		{
			_statusLabel.Text = "第 1 组效果预览已生成，正在播放...";
			using (VideoPreviewForm previewForm = new VideoPreviewForm(tempOutputFile, "批量合并 - 第1组效果试看 (约8秒)"))
			{
				previewForm.ShowDialog(this);
			}
			_statusLabel.Text = "批量合并待命中。可点击播放预览或开始合并。";
		}
		else
		{
			string errMsg = result != null ? result.Error : "未知错误";
			_statusLabel.Text = "生成合并预览失败：" + errMsg;
			MessageBox.Show(this, "生成合并预览失败：\n" + errMsg, "预览失败", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private void StartSplit()
	{
		if (_isRunning)
		{
			return;
		}
		if (_splitVideos.Count == 0)
		{
			MessageBox.Show(this, "请先拖入或添加要拆分的视频。", "还没有视频", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		string ffmpeg = FindFfmpeg();
		if (ffmpeg == null)
		{
			MessageBox.Show(this, "没有找到 FFmpeg。请保持便携版文件完整，或把 FFmpeg 加入系统 PATH。", "缺少 FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		string output = _splitOutputFolder.Text.Trim();
		if (output.Length == 0)
		{
			MessageBox.Show(this, "请选择拆分视频总输出目录。", "缺少输出目录", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		try
		{
			Directory.CreateDirectory(output);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "无法创建输出目录：\n" + ex.Message, "输出目录不可用", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		WatermarkProfile watermark = CaptureWatermarkProfile(_watermarkOnSplit.Checked);
		string text = ValidateWatermark(watermark);
		if (text != null)
		{
			_tabs.SelectedIndex = 2;
			MessageBox.Show(this, text, "水印设置不完整", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		List<string> files = new List<string>(_splitVideos);
		_activeSplitVideoAdjustments = CaptureVideoAdjustmentSnapshot(files, _splitVideoAdjustments);
		_activeSplitOutputFrame = CaptureOutputFrameSettings(_splitOutputFrameControls);
		_activeSplitBgmPlan = CaptureBgmPlan(_splitBgm);
		SplitPlan splitPlan = CaptureSplitPlan();
		SetRunningState(running: true);
		_cancelRequested = false;
		_splitProgressBar.Value = 0;
		Task.Factory.StartNew(delegate
		{
			RunSplitBatchProfile(ffmpeg, files, output, splitPlan, watermark);
		}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
	}

	private TransitionPlan CaptureImageTransitionSettings()
	{
		TransitionPlan transitionPlan = new TransitionPlan();
		transitionPlan.DurationSeconds = decimal.ToDouble(_imageTransitionDuration.Value);
		transitionPlan.RandomOrder = _imageTransitionOrder.SelectedIndex == 1;
		TransitionPlan transitionPlan2 = transitionPlan;
		List<TransitionSpec> list = CreateTransitionCatalog(transitionPlan2.DurationSeconds);
		foreach (int checkedIndex in _imageTransitions.CheckedIndices)
		{
			if (checkedIndex >= 0 && checkedIndex < list.Count)
			{
				transitionPlan2.Effects.Add(list[checkedIndex]);
			}
		}
		return transitionPlan2;
	}

	private void StartImageVideo()
	{
		if (_isRunning)
		{
			return;
		}
		if (_slideshowImages.Count == 0)
		{
			MessageBox.Show(this, "请先添加一张或多张图片。", "还没有图片", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		string ffmpeg = FindFfmpeg();
		if (ffmpeg == null)
		{
			MessageBox.Show(this, "没有找到 FFmpeg。请保持便携版文件完整。", "缺少 FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		if (_imageLayoutPool.CheckedIndices.Count == 0)
		{
			MessageBox.Show(this, "请至少勾选一种图片排版。", "还没有选择排版", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		if (_imageMotionPool.CheckedIndices.Count == 0)
		{
			MessageBox.Show(this, "请至少勾选一种展示律动。", "还没有选择展示效果", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		double imageDuration = decimal.ToDouble(_imageDuration.Value);
		ImageVideoLayoutPlan layoutPlan = CaptureImageVideoLayoutPlan();
		TransitionPlan transition = CaptureImageTransitionSettings();
		string output = _imageOutputFolder.Text.Trim();
		if (output.Length == 0)
		{
			MessageBox.Show(this, "请选择图片成片总输出目录。", "缺少输出目录", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		try
		{
			Directory.CreateDirectory(output);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "无法创建输出目录：\n" + ex.Message, "输出目录不可用", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		GetImageVideoResolution(out var width, out var height);
		List<string> images = new List<string>(_slideshowImages.Where(File.Exists));
		if (images.Count == 0)
		{
			MessageBox.Show(this, "图片文件已经移动或不存在，请重新导入。", "图片不可用", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		BgmPlan bgm = CaptureBgmPlan(_imageBgm);
		SetRunningState(running: true);
		_cancelRequested = false;
		_imageProgressBar.Value = 0;
		Task.Factory.StartNew(delegate
		{
			RunImageVideoBatch(ffmpeg, images, output, imageDuration, width, height, transition, bgm, layoutPlan);
		}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
	}

	private void GetImageVideoResolution(out int width, out int height)
	{
		if (_imageResolution.SelectedIndex == 1)
		{
			width = 1080;
			height = 1920;
		}
		else if (_imageResolution.SelectedIndex == 2)
		{
			width = 1080;
			height = 1080;
		}
		else if (_imageResolution.SelectedIndex == 3)
		{
			width = 1280;
			height = 720;
		}
		else
		{
			width = 1920;
			height = 1080;
		}
	}

	private void RunImageVideoBatch(string ffmpeg, List<string> images, string outputParent, double imageDuration, int width, int height, TransitionPlan transition, BgmPlan bgm, ImageVideoLayoutPlan layoutPlan)
	{
		string outputFolder = null;
		string path = null;
		List<string> createdOutputs = new List<string>();
		try
		{
			outputFolder = CreateBatchOutputFolder(outputParent, "图片成片");
			_latestImageVideoOutputFolder = outputFolder;
			int outputCount = Math.Max(1, (layoutPlan == null) ? 1 : layoutPlan.OutputCount);
			double targetDuration = Math.Max(1.0, (layoutPlan == null) ? 30.0 : layoutPlan.TargetDurationSeconds);
			List<List<string>> outputGroups = BuildImageOutputGroups(images, outputCount);
			Random random = new Random(Guid.NewGuid().GetHashCode());
			int totalPages = 0;
			int outputIndex;
			for (outputIndex = 0; outputIndex < outputGroups.Count; outputIndex++)
			{
				if (_cancelRequested)
				{
					throw new OperationCanceledException();
				}
				ImageVideoLayoutPlan imageVideoLayoutPlan = CloneImageVideoLayoutPlan(layoutPlan);
				List<string> images2 = BuildImagePageSequence(outputGroups[outputIndex], imageDuration, targetDuration, transition, imageVideoLayoutPlan, outputIndex, out var pages, out var actualPageDuration, out var actualTransitionDuration);
				imageVideoLayoutPlan.Pages = pages;
				imageVideoLayoutPlan.TargetDurationSeconds = targetDuration;
				totalPages += pages.Count;
				TransitionPlan transitionPlan2;
				if (!transition.Enabled || pages.Count <= 1)
				{
					TransitionPlan transitionPlan = new TransitionPlan();
					transitionPlan.DurationSeconds = 0.0;
					transitionPlan2 = transitionPlan;
				}
				else
				{
					transitionPlan2 = CopyTransitionPlanWithDuration(transition, actualTransitionDuration);
				}
				TransitionPlan transition2 = transitionPlan2;
				string text = Path.Combine(outputFolder, "图片成片_" + (outputIndex + 1).ToString("000") + ".mp4");
				path = text;
				int num = RenderImageSlideshow(ffmpeg, images2, text, actualPageDuration, width, height, transition2, imageVideoLayoutPlan, delegate(double p)
				{
					double num2 = (bgm.Enabled ? (p * 0.82) : p);
					UiImageProgress(((double)outputIndex + num2) / (double)outputGroups.Count, "正在生成第 " + (outputIndex + 1) + "/" + outputGroups.Count + " 个图片视频");
				}, out var error);
				if (_cancelRequested)
				{
					throw new OperationCanceledException();
				}
				if (num != 0 || !File.Exists(text))
				{
					throw new InvalidOperationException("第 " + (outputIndex + 1) + " 个图片视频生成失败：" + LastUsefulLines(error, 12));
				}
				if (bgm.Enabled)
				{
					string bgmPath = SelectBgm(bgm, outputIndex, random);
					if (!ApplyBackgroundMusic(ffmpeg, text, bgmPath, bgm.VolumePercent, delegate(double p)
					{
						UiImageProgress(((double)outputIndex + 0.82 + p * 0.18) / (double)outputGroups.Count, "正在为第 " + (outputIndex + 1) + " 个成品加入 BGM");
					}, out var error2))
					{
						throw new InvalidOperationException("第 " + (outputIndex + 1) + " 个成品 BGM 配乐失败：" + LastUsefulLines(error2, 10));
					}
				}
				if (!ValidateExactDurationOutput(ffmpeg, text, targetDuration, out var reason))
				{
					throw new InvalidOperationException("第 " + (outputIndex + 1) + " 个成品校验失败：" + reason);
				}
				createdOutputs.Add(text);
				path = null;
			}
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_imageProgressBar.Value = 100;
				_imageStatusLabel.Text = "图片成片完成：生成 " + createdOutputs.Count + " 个成品，共 " + totalPages + " 组画面。";
				ShowCompletionAndOpenFolder("图片成片完成。\n\n使用图片：" + images.Count + " 张\n成品数量：" + createdOutputs.Count + " 个\n每个成品：" + FfmpegNumber(targetDuration) + " 秒\n画面组数：" + totalPages + "\n排版：" + layoutPlan.DisplayName + "\n\n本批次输出目录：\n" + outputFolder, "图片成片完成", MessageBoxIcon.Asterisk, outputParent, outputFolder);
			});
		}
		catch (OperationCanceledException)
		{
			TryDelete(path);
			foreach (string item in createdOutputs)
			{
				TryDelete(item);
			}
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_imageStatusLabel.Text = "图片成片任务已取消。";
			});
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			TryDelete(path);
			foreach (string item2 in createdOutputs)
			{
				TryDelete(item2);
			}
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_imageStatusLabel.Text = "图片成片失败，请检查图片和转场设置。";
				MessageBox.Show(this, ex3.Message, "图片成片失败", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			});
		}
	}

	private static List<List<string>> BuildImageOutputGroups(List<string> images, int outputCount)
	{
		List<List<string>> list = new List<List<string>>();
		outputCount = Math.Max(1, outputCount);
		if (images == null || images.Count == 0)
		{
			return list;
		}
		if (outputCount > images.Count)
		{
			for (int i = 0; i < outputCount; i++)
			{
				list.Add(new List<string> { images[i % images.Count] });
			}
			return list;
		}
		int num = 0;
		int num2 = images.Count / outputCount;
		int num3 = images.Count % outputCount;
		for (int j = 0; j < outputCount; j++)
		{
			int num4 = num2 + ((j < num3) ? 1 : 0);
			list.Add(images.Skip(num).Take(num4).ToList());
			num += num4;
		}
		return list;
	}

	private static ImageVideoLayoutPlan CloneImageVideoLayoutPlan(ImageVideoLayoutPlan source)
	{
		source = source ?? new ImageVideoLayoutPlan();
		ImageVideoLayoutPlan imageVideoLayoutPlan = new ImageVideoLayoutPlan();
		imageVideoLayoutPlan.ImagesPerPage = source.ImagesPerPage;
		imageVideoLayoutPlan.DisplayName = source.DisplayName;
		imageVideoLayoutPlan.TileAnimation = source.TileAnimation;
		imageVideoLayoutPlan.TileAnimationDuration = source.TileAnimationDuration;
		imageVideoLayoutPlan.SmartEnhance = source.SmartEnhance;
		imageVideoLayoutPlan.RandomLayouts = source.RandomLayouts;
		imageVideoLayoutPlan.RandomMotions = source.RandomMotions;
		imageVideoLayoutPlan.OutputCount = source.OutputCount;
		imageVideoLayoutPlan.TargetDurationSeconds = source.TargetDurationSeconds;
		imageVideoLayoutPlan.LayoutChoices = source.LayoutChoices.Select((ImageLayoutChoice x) => new ImageLayoutChoice
		{
			DisplayName = x.DisplayName,
			ImagesPerPage = x.ImagesPerPage,
			LayoutKind = x.LayoutKind
		}).ToList();
		imageVideoLayoutPlan.MotionEffects = new List<string>(source.MotionEffects);
		return imageVideoLayoutPlan;
	}

	private static List<string> BuildImagePageSequence(List<string> sourceImages, double preferredPageDuration, double targetDuration, TransitionPlan transition, ImageVideoLayoutPlan plan, int outputIndex, out List<ImageVideoPagePlan> pages, out double actualPageDuration, out double actualTransitionDuration)
	{
		pages = new List<ImageVideoPagePlan>();
		List<string> list = new List<string>();
		if (sourceImages == null || sourceImages.Count == 0)
		{
			actualPageDuration = targetDuration;
			actualTransitionDuration = 0.0;
			return list;
		}
		List<ImageLayoutChoice> list3;
		if (plan.LayoutChoices.Count <= 0)
		{
			List<ImageLayoutChoice> list2 = new List<ImageLayoutChoice>();
			list2.Add(new ImageLayoutChoice
			{
				DisplayName = plan.DisplayName,
				ImagesPerPage = Math.Max(1, plan.ImagesPerPage),
				LayoutKind = 0
			});
			list3 = list2;
		}
		else
		{
			list3 = plan.LayoutChoices;
		}
		List<ImageLayoutChoice> list4 = list3;
		List<string> list6;
		if (plan.MotionEffects.Count <= 0)
		{
			List<string> list5 = new List<string>();
			list5.Add(plan.TileAnimation ?? "直接显示");
			list6 = list5;
		}
		else
		{
			list6 = plan.MotionEffects;
		}
		List<string> list7 = list6;
		double num = ((transition != null && transition.Enabled) ? Math.Min(transition.DurationSeconds, Math.Max(0.05, preferredPageDuration * 0.4)) : 0.0);
		double num2 = Math.Max(0.1, preferredPageDuration - num);
		int num3 = Math.Max(1, (int)Math.Ceiling(Math.Max(0.1, targetDuration - num) / num2));
		Random random = new Random(7919 + outputIndex * 104729 + Guid.NewGuid().GetHashCode());
		int num4 = 0;
		while (num4 < sourceImages.Count || pages.Count < num3)
		{
			int count = pages.Count;
			ImageLayoutChoice imageLayoutChoice = (plan.RandomLayouts ? list4[random.Next(list4.Count)] : list4[count % list4.Count]);
			string motionEffect = (plan.RandomMotions ? list7[random.Next(list7.Count)] : list7[count % list7.Count]);
			int num5 = Math.Max(1, Math.Min(imageLayoutChoice.ImagesPerPage, sourceImages.Count));
			if (num4 < sourceImages.Count)
			{
				num5 = Math.Min(num5, sourceImages.Count - num4);
			}
			for (int i = 0; i < num5; i++)
			{
				list.Add(sourceImages[(num4 + i) % sourceImages.Count]);
			}
			num4 += num5;
			pages.Add(new ImageVideoPagePlan
			{
				ImageCount = num5,
				LayoutKind = imageLayoutChoice.LayoutKind,
				LayoutName = imageLayoutChoice.DisplayName,
				MotionEffect = motionEffect
			});
			if (pages.Count >= 2000)
			{
				throw new InvalidOperationException("成品时长与每页时长组合需要超过 2000 组画面，请增大每页时长或缩短成品时长。");
			}
		}
		actualTransitionDuration = ((transition != null && transition.Enabled && pages.Count > 1) ? Math.Min(transition.DurationSeconds, Math.Max(0.05, targetDuration / ((double)pages.Count * 3.0))) : 0.0);
		actualPageDuration = Math.Max(0.1, (targetDuration + (double)Math.Max(0, pages.Count - 1) * actualTransitionDuration) / (double)pages.Count);
		if (actualTransitionDuration >= actualPageDuration)
		{
			actualTransitionDuration = Math.Max(0.02, actualPageDuration * 0.35);
		}
		actualPageDuration = (targetDuration + (double)Math.Max(0, pages.Count - 1) * actualTransitionDuration) / (double)pages.Count;
		return list;
	}

	private int RenderImageSlideshow(string ffmpeg, List<string> images, string outputPath, double imageDuration, int width, int height, TransitionPlan transition, ImageVideoLayoutPlan layoutPlan, Action<double> progress, out string error)
	{
		layoutPlan = layoutPlan ?? new ImageVideoLayoutPlan();
		int num = Math.Max(1, Math.Min(9, layoutPlan.ImagesPerPage));
		bool flag = layoutPlan.Pages != null && layoutPlan.Pages.Count > 0;
		int num2 = (flag ? layoutPlan.Pages.Count : ((int)Math.Ceiling((double)images.Count / (double)num)));
		StringBuilder stringBuilder = new StringBuilder("-hide_banner -y");
		foreach (string image in images)
		{
			stringBuilder.Append(" -loop 1 -framerate 30 -t ").Append(FfmpegNumber(imageDuration)).Append(" -i ")
				.Append(QuoteArg(image));
		}
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		int num3 = 0;
		for (int i = 0; i < num2; i++)
		{
			ImageVideoPagePlan imageVideoPagePlan = (flag ? layoutPlan.Pages[i] : null);
			int val = ((imageVideoPagePlan == null) ? num : Math.Max(1, imageVideoPagePlan.ImageCount));
			int num4 = Math.Min(val, images.Count - num3);
			int layoutKind = imageVideoPagePlan?.LayoutKind ?? 0;
			string text = ((imageVideoPagePlan == null) ? (layoutPlan.TileAnimation ?? "直接显示") : (imageVideoPagePlan.MotionEffect ?? "直接显示"));
			bool flag2 = IsImagePageMotion(text);
			List<Rectangle> list3 = CreateCollageLayoutRectanglesForKind(num4, width, height, layoutKind);
			string text2 = "pagebase" + i;
			list.Add("color=c=0x111827:s=" + width + "x" + height + ":d=" + FfmpegNumber(imageDuration) + ":r=30,format=rgba[" + text2 + "]");
			string text3 = text2;
			int num5 = 0;
			while (num5 < num4)
			{
				Rectangle rectangle = list3[num5];
				string text4 = "tile" + i + "_" + num5;
				StringBuilder stringBuilder2 = new StringBuilder("[").Append(num3).Append(":v]scale=").Append(rectangle.Width)
					.Append(":")
					.Append(rectangle.Height)
					.Append(":force_original_aspect_ratio=increase:flags=lanczos+accurate_rnd,crop=")
					.Append(rectangle.Width)
					.Append(":")
					.Append(rectangle.Height)
					.Append(",setsar=1,fps=30");
				if (layoutPlan.SmartEnhance)
				{
					stringBuilder2.Append(",eq=contrast=1.04:brightness=0.012:saturation=1.08:gamma=1.02").Append(",hqdn3d=1.0:1.0:4.0:4.0,unsharp=5:5:0.22:3:3:0.0");
				}
				double num6 = Math.Min(Math.Max(0.1, layoutPlan.TileAnimationDuration), Math.Max(0.1, imageDuration * 0.35));
				double num7 = Math.Min(num6 * 0.75, (double)num5 * 0.1);
				double value = Math.Max(num7 + num6, imageDuration - num6 - Math.Min(0.35, (double)(num4 - 1 - num5) * 0.04));
				bool flag3 = !flag2 && text != "直接显示";
				bool flag4 = !flag2 && text != "直接显示";
				stringBuilder2.Append(",format=rgba");
				if (flag3)
				{
					stringBuilder2.Append(",fade=t=in:st=").Append(FfmpegNumber(num7)).Append(":d=")
						.Append(FfmpegNumber(num6))
						.Append(":alpha=1");
				}
				if (flag4)
				{
					stringBuilder2.Append(",fade=t=out:st=").Append(FfmpegNumber(value)).Append(":d=")
						.Append(FfmpegNumber(num6))
						.Append(":alpha=1");
				}
				stringBuilder2.Append("[").Append(text4).Append("]");
				list.Add(stringBuilder2.ToString());
				string text5 = rectangle.X.ToString(CultureInfo.InvariantCulture);
				string text6 = rectangle.Y.ToString(CultureInfo.InvariantCulture);
				switch (text)
				{
				case "从左滑入后淡出":
					text5 = "if(lt(t," + FfmpegNumber(num7 + num6) + "),-overlay_w+(" + rectangle.X + "+overlay_w)*max(0,min(1,(t-" + FfmpegNumber(num7) + ")/" + FfmpegNumber(num6) + "))," + rectangle.X + ")";
					break;
				case "上浮进入后淡出":
					text6 = "if(lt(t," + FfmpegNumber(num7 + num6) + "),main_h+(" + rectangle.Y + "-main_h)*max(0,min(1,(t-" + FfmpegNumber(num7) + ")/" + FfmpegNumber(num6) + "))," + rectangle.Y + ")";
					break;
				case "向右滑出":
					text5 = "if(gte(t," + FfmpegNumber(value) + ")," + rectangle.X + "+(main_w-" + rectangle.X + ")*min(1,(t-" + FfmpegNumber(value) + ")/" + FfmpegNumber(num6) + ")," + rectangle.X + ")";
					break;
				case "向下沉出":
					text6 = "if(gte(t," + FfmpegNumber(value) + ")," + rectangle.Y + "+(main_h-" + rectangle.Y + ")*min(1,(t-" + FfmpegNumber(value) + ")/" + FfmpegNumber(num6) + ")," + rectangle.Y + ")";
					break;
				}
				string text7 = "pageov" + i + "_" + num5;
				list.Add("[" + text3 + "][" + text4 + "]overlay=x='" + text5 + "':y='" + text6 + "':eof_action=repeat:shortest=1[" + text7 + "]");
				text3 = text7;
				num5++;
				num3++;
			}
			string text8 = "page" + i;
			string text9 = (flag2 ? BuildImagePageMotionFilter(text, width, height, imageDuration) : "");
			list.Add("[" + text3 + "]" + text9 + "trim=duration=" + FfmpegNumber(imageDuration) + ",setpts=PTS-STARTPTS,fps=30,format=yuv420p[" + text8 + "]");
			list2.Add(text8);
		}
		string value2;
		if (list2.Count == 1)
		{
			value2 = list2[0];
		}
		else if (!transition.Enabled)
		{
			StringBuilder stringBuilder3 = new StringBuilder();
			for (int j = 0; j < list2.Count; j++)
			{
				stringBuilder3.Append("[").Append(list2[j]).Append("]");
			}
			list.Add(string.Concat(stringBuilder3, "concat=n=", list2.Count, ":v=1:a=0[vout]"));
			value2 = "vout";
		}
		else
		{
			Random random = new Random(Guid.NewGuid().GetHashCode());
			string text10 = list2[0];
			for (int k = 1; k < list2.Count; k++)
			{
				TransitionSpec transitionSpec = (transition.RandomOrder ? transition.Effects[random.Next(transition.Effects.Count)] : transition.Effects[(k - 1) % transition.Effects.Count]);
				string text11 = "vx" + k;
				double value3 = (double)k * (imageDuration - transition.DurationSeconds);
				string text12 = "[" + text10 + "][" + list2[k] + "]xfade=transition=" + transitionSpec.FfmpegName + ":duration=" + FfmpegNumber(transition.DurationSeconds) + ":offset=" + FfmpegNumber(value3);
				if (transitionSpec.FfmpegName == "custom" && !string.IsNullOrWhiteSpace(transitionSpec.CustomExpression))
				{
					text12 = text12 + ":expr='" + transitionSpec.CustomExpression + "'";
				}
				list.Add(text12 + "[" + text11 + "]");
				text10 = text11;
			}
			value2 = text10;
		}
		double num8 = ((flag && layoutPlan.TargetDurationSeconds > 0.0) ? layoutPlan.TargetDurationSeconds : Math.Max(0.1, (double)list2.Count * imageDuration - (transition.Enabled ? ((double)Math.Max(0, list2.Count - 1) * transition.DurationSeconds) : 0.0)));
		stringBuilder.Append(" -filter_complex ").Append(QuoteArg(string.Join(";", list.ToArray()))).Append(" -map [")
			.Append(value2)
			.Append("] -an -c:v libx264 -preset fast -crf 17 -metadata:s:v:0 rotate=0")
			.Append(" -pix_fmt yuv420p -r 30 -t ")
			.Append(FfmpegNumber(num8))
			.Append(" -movflags +faststart -progress pipe:1 -nostats ")
			.Append(QuoteArg(outputPath));
		return RunFfmpeg(ffmpeg, stringBuilder.ToString(), num8, progress, out error);
	}

	private static bool IsImagePageMotion(string effect)
	{
		switch (effect)
		{
		default:
			return effect == "微微律动";
		case "轻微放大":
		case "呼吸缩放":
		case "轻柔漂移":
			return true;
		}
	}

	private static string BuildImagePageMotionFilter(string effect, int width, int height, double duration)
	{
		string text = FfmpegNumber(Math.Max(0.2, duration));
		string text2;
		switch (effect)
		{
		case "轻柔漂移":
		{
			int num = (int)Math.Ceiling((double)width * 1.06 / 2.0) * 2;
			int num2 = (int)Math.Ceiling((double)height * 1.06 / 2.0) * 2;
			return "scale=" + num + ":" + num2 + ":flags=lanczos+accurate_rnd,crop=" + width + ":" + height + ":x='(iw-ow)*(0.5+0.32*sin(2*PI*t/" + text + "))':y='(ih-oh)*(0.5+0.22*cos(2*PI*t/" + text + "))',setsar=1,";
		}
		case "呼吸缩放":
			text2 = "1.025+0.014*sin(2*PI*t/" + text + ")";
			break;
		case "微微律动":
			text2 = "1.028+0.010*sin(6*PI*t/" + text + ")";
			break;
		default:
			text2 = "1+0.04*min(t/" + text + "\\,1)";
			break;
		}
		return "scale=w='trunc(" + width + "*(" + text2 + ")/2)*2':h='trunc(" + height + "*(" + text2 + ")/2)*2':eval=frame:flags=lanczos+accurate_rnd,crop=" + width + ":" + height + ":x='(iw-ow)/2':y='(ih-oh)/2',setsar=1,";
	}

	private static List<Rectangle> CreateCollageLayoutRectanglesForKind(int count, int width, int height, int layoutKind)
	{
		if (count == 4 && layoutKind == 2)
		{
			List<RectangleF> list = new List<RectangleF>();
			AddGridRectangles(list, 4, 2, 2, 0f, 0f, 1f, 1f);
			return ConvertNormalizedRectangles(list, count, width, height);
		}
		return CreateCollageLayoutRectangles(count, width, height);
	}

	private static List<Rectangle> CreateCollageLayoutRectangles(int count, int width, int height)
	{
		count = Math.Max(1, Math.Min(9, count));
		List<RectangleF> list = new List<RectangleF>();
		switch (count)
		{
		case 1:
			list.Add(new RectangleF(0f, 0f, 1f, 1f));
			break;
		case 2:
			list.Add(new RectangleF(0f, 0f, 0.5f, 1f));
			list.Add(new RectangleF(0.5f, 0f, 0.5f, 1f));
			break;
		case 3:
			list.Add(new RectangleF(0f, 0f, 0.64f, 1f));
			list.Add(new RectangleF(0.64f, 0f, 0.36f, 0.5f));
			list.Add(new RectangleF(0.64f, 0.5f, 0.36f, 0.5f));
			break;
		case 4:
			list.Add(new RectangleF(0f, 0f, 0.64f, 0.66f));
			list.Add(new RectangleF(0.64f, 0f, 0.36f, 0.5f));
			list.Add(new RectangleF(0.64f, 0.5f, 0.36f, 0.5f));
			list.Add(new RectangleF(0f, 0.66f, 0.64f, 0.34f));
			break;
		case 5:
			list.Add(new RectangleF(0f, 0f, 0.58f, 1f));
			AddGridRectangles(list, 4, 2, 2, 0.58f, 0f, 0.42f, 1f);
			break;
		case 6:
			AddGridRectangles(list, 6, 3, 2, 0f, 0f, 1f, 1f);
			break;
		case 7:
			list.Add(new RectangleF(0f, 0f, 0.52f, 1f));
			AddGridRectangles(list, 6, 2, 3, 0.52f, 0f, 0.48f, 1f);
			break;
		case 8:
			list.Add(new RectangleF(0f, 0f, 0.6f, 0.65f));
			list.Add(new RectangleF(0.6f, 0f, 0.4f, 0.325f));
			list.Add(new RectangleF(0.6f, 0.325f, 0.4f, 0.325f));
			AddGridRectangles(list, 5, 5, 1, 0f, 0.65f, 1f, 0.35f);
			break;
		default:
			AddGridRectangles(list, 9, 3, 3, 0f, 0f, 1f, 1f);
			break;
		}
		return ConvertNormalizedRectangles(list, count, width, height);
	}

	private static List<Rectangle> ConvertNormalizedRectangles(List<RectangleF> normalized, int count, int width, int height)
	{
		int num = Math.Max(4, Math.Min(width, height) / 120);
		List<Rectangle> list = new List<Rectangle>();
		foreach (RectangleF item in normalized.Take(count))
		{
			int num2 = (int)Math.Round(item.X * (float)width) + num;
			int num3 = (int)Math.Round(item.Y * (float)height) + num;
			int num4 = (int)Math.Round((item.X + item.Width) * (float)width) - num;
			int num5 = (int)Math.Round((item.Y + item.Height) * (float)height) - num;
			list.Add(new Rectangle(num2, num3, Math.Max(2, num4 - num2), Math.Max(2, num5 - num3)));
		}
		return list;
	}

	private static void AddGridRectangles(List<RectangleF> target, int count, int columns, int rows, float x, float y, float width, float height)
	{
		for (int i = 0; i < count; i++)
		{
			int num = i % columns;
			int num2 = i / columns;
			target.Add(new RectangleF(x + width * (float)num / (float)columns, y + height * (float)num2 / (float)rows, width / (float)columns, height / (float)rows));
		}
	}

	private void RunSplitBatch(string ffmpeg, List<string> files, string outputFolder, double segmentSeconds, WatermarkSettings watermark)
	{
		RunSplitBatchPlan(ffmpeg, files, outputFolder, new SplitPlan
		{
			EqualPartsMode = false,
			SegmentSeconds = segmentSeconds,
			DiscardShortTail = false,
			EqualParts = 2
		}, watermark);
	}

	private void RunSplitBatchPlan(string ffmpeg, List<string> files, string outputFolder, SplitPlan splitPlan, WatermarkSettings watermark)
	{
		RunSplitBatchProfile(ffmpeg, files, outputFolder, splitPlan, WatermarkProfile.FromSingle(watermark));
	}

	private void RunSplitBatchProfile(string ffmpeg, List<string> files, string outputParent, SplitPlan splitPlan, WatermarkProfile watermark)
	{
		int succeeded = 0;
		int failed = 0;
		int totalSegments = 0;
		int num = 0;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string outputFolder = null;
		string path = null;
		try
		{
			outputFolder = CreateBatchOutputFolder(outputParent, "视频拆分");
			_latestSplitOutputFolder = outputFolder;
			path = Path.Combine(Path.GetTempPath(), "VideoBatch_Split_" + Guid.NewGuid().ToString("N") + ".log");
			List<WatermarkProfile> list3 = CreateWatermarkAssignments(watermark, files.Count);
			Random random = new Random(Guid.NewGuid().GetHashCode());
			int num2 = 0;
			using (StreamWriter streamWriter = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
			{
				streamWriter.WriteLine("视频批量拆分记录");
				streamWriter.WriteLine("开始时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				streamWriter.WriteLine("拆分方式：" + (splitPlan.EqualPartsMode ? ("按视频时长平均切成 " + splitPlan.EqualParts + " 份") : ("每段 " + FfmpegNumber(splitPlan.SegmentSeconds) + " 秒，尾段" + (splitPlan.DiscardShortTail ? "舍弃" : "并入上一段"))));
				streamWriter.WriteLine("输出画幅：" + DescribeOutputFrame(_activeSplitOutputFrame));
				streamWriter.WriteLine("水印：" + (watermark.Enabled ? (WatermarkLayerCount(watermark) + " 个图层") : "无"));
				streamWriter.WriteLine();
				for (int i = 0; i < files.Count; i++)
				{
					if (_cancelRequested)
					{
						throw new OperationCanceledException();
					}
					string text = files[i];
					WatermarkProfile currentWatermark = list3[i];
					int captured = i;
					UiSplitStatus("正在分析第 " + (i + 1) + "/" + files.Count + " 个视频…");
					VideoInfo videoInfo = Probe(ffmpeg, text);
					if (_cancelRequested)
					{
						throw new OperationCanceledException();
					}
					streamWriter.WriteLine("视频 " + (i + 1) + "：" + text);
					if (!videoInfo.HasVideo || videoInfo.DurationSeconds <= 0.0)
					{
						failed++;
						list2.Add(text + "（无法识别视频或时长）");
						streamWriter.WriteLine("  结果：失败，无法识别视频或时长");
						continue;
					}
					VideoAdjustmentSettings videoAdjustment = GetVideoAdjustment(_activeSplitVideoAdjustments, text);
					videoInfo.DurationSeconds /= Math.Max(0.1, videoAdjustment.SpeedRatio);
					streamWriter.WriteLine("  素材调整：" + DescribeVideoAdjustment(videoAdjustment));
					streamWriter.WriteLine("  调整后时长：约 " + FfmpegNumber(videoInfo.DurationSeconds) + " 秒");
					List<double> list4 = new List<double>();
					double num3 = videoInfo.DurationSeconds;
					int num4;
					if (splitPlan.EqualPartsMode)
					{
						num4 = splitPlan.EqualParts;
						double num5 = videoInfo.DurationSeconds / (double)num4;
						for (int j = 1; j < num4; j++)
						{
							list4.Add(num5 * (double)j);
						}
						streamWriter.WriteLine("  计划：平均 " + num4 + " 份，每份约 " + FfmpegNumber(num5) + " 秒");
					}
					else
					{
						double segmentSeconds = splitPlan.SegmentSeconds;
						int num6 = (int)Math.Floor(videoInfo.DurationSeconds / segmentSeconds + 0.001);
						if (num6 < 1)
						{
							failed++;
							list2.Add(text + "（短于设定时长，按当前规则跳过）");
							streamWriter.WriteLine("  结果：跳过，视频短于设定的 " + FfmpegNumber(segmentSeconds) + " 秒");
							continue;
						}
						double num7 = Math.Max(0.0, videoInfo.DurationSeconds - (double)num6 * segmentSeconds);
						num4 = num6;
						for (int k = 1; k < num6; k++)
						{
							list4.Add(segmentSeconds * (double)k);
						}
						if (splitPlan.DiscardShortTail && num7 > 0.02)
						{
							num3 = (double)num6 * segmentSeconds;
							streamWriter.WriteLine("  计划：生成 " + num4 + " 段，舍弃尾部约 " + FfmpegNumber(num7) + " 秒");
						}
						else if (num7 > 0.02)
						{
							streamWriter.WriteLine("  计划：生成 " + num4 + " 段，尾部约 " + FfmpegNumber(num7) + " 秒并入最后一段");
						}
						else
						{
							streamWriter.WriteLine("  计划：生成 " + num4 + " 个等长片段");
						}
					}
					num += num4;
					string value = FindAvailableSegmentPattern(outputFolder, text, out var prefix);
					string text2 = Path.Combine(Path.GetTempPath(), "VideoBatchSplit_" + Guid.NewGuid().ToString("N"));
					Directory.CreateDirectory(text2);
					try
					{
						Size outputCanvasSize = GetOutputCanvasSize(videoInfo, videoAdjustment, _activeSplitOutputFrame);
						int targetWidth = outputCanvasSize.Width;
						int targetHeight = outputCanvasSize.Height;
						string text3 = BuildAdjustedVideoFilterWithCanvasCrop(targetWidth, targetHeight, videoAdjustment, normalizeFrameRate: false, ShouldFillOutputCanvas(videoInfo, videoAdjustment, _activeSplitOutputFrame), ResolveCropPositionPercent(_activeSplitOutputFrame));
						StringBuilder stringBuilder = new StringBuilder("-hide_banner -y -i ").Append(QuoteArg(text));
						stringBuilder.Append(" -map 0:v:0 -map 0:a:0? -vf ").Append(QuoteArg(text3));
						if (videoInfo.HasAudio)
						{
							stringBuilder.Append(" -af ").Append(QuoteArg(BuildAdjustedAudioFilter(videoAdjustment)));
						}
						if (!splitPlan.EqualPartsMode && splitPlan.DiscardShortTail && num3 < videoInfo.DurationSeconds - 0.02)
						{
							stringBuilder.Append(" -t ").Append(FfmpegNumber(num3));
						}
						stringBuilder.Append(" -c:v libx264 -preset fast -crf 17 -metadata:s:v:0 rotate=0 -c:a aac -b:a 192k");
						if (list4.Count > 0)
						{
							string value2 = string.Join(",", list4.Select(FfmpegNumber).ToArray());
							stringBuilder.Append(" -force_key_frames ").Append(QuoteArg(value2));
							stringBuilder.Append(" -f segment -segment_times ").Append(QuoteArg(value2));
							stringBuilder.Append(" -segment_time_delta 0.05");
							stringBuilder.Append(" -reset_timestamps 1 -segment_start_number 1 -progress pipe:1 -nostats ").Append(QuoteArg(value));
						}
						else
						{
							string value3 = Path.Combine(outputFolder, prefix + "_片段_001.mp4");
							stringBuilder.Append(" -movflags +faststart -progress pipe:1 -nostats ").Append(QuoteArg(value3));
						}
						string errorText = null;
						int num8 = -1;
						string[] created = new string[0];
						bool flag = false;
						string reason = null;
						string[] files2 = Directory.GetFiles(outputFolder, prefix + "_片段_*.mp4", SearchOption.TopDirectoryOnly);
						foreach (string path2 in files2)
						{
							TryDelete(path2);
						}
						num8 = RunFfmpeg(ffmpeg, stringBuilder.ToString(), num3, delegate(double p)
						{
							double num11 = (currentWatermark.Enabled ? (p * 0.5) : p);
							UiSplitProgress(((double)captured + num11) / (double)files.Count, "正在拆分第 " + (captured + 1) + "/" + files.Count + " 个视频");
						}, out errorText);
						created = Directory.GetFiles(outputFolder, prefix + "_片段_*.mp4", SearchOption.TopDirectoryOnly);
						flag = num8 == 0 && ValidateSplitOutputSet(ffmpeg, created, num4, list4, num3, out reason);
						if (!flag && !_cancelRequested)
						{
							files2 = Directory.GetFiles(outputFolder, prefix + "_片段_*.mp4", SearchOption.TopDirectoryOnly);
							foreach (string path3 in files2)
							{
								TryDelete(path3);
							}
							streamWriter.WriteLine("  快速分段未达到预计数量（预计 " + num4 + "，实际 " + created.Length + "），改为逐段精确补做。" + (string.IsNullOrWhiteSpace(reason) ? "" : (" 原因：" + reason)));
							bool flag2 = RenderSplitSegmentsIndependently(ffmpeg, text, outputFolder, prefix, list4, num3, videoInfo.HasAudio, videoAdjustment, text3, delegate(double p)
							{
								double num11 = (currentWatermark.Enabled ? (p * 0.5) : p);
								UiSplitProgress(((double)captured + num11) / (double)files.Count, "正在精确补做第 " + (captured + 1) + "/" + files.Count + " 个视频");
							}, out var error);
							created = Directory.GetFiles(outputFolder, prefix + "_片段_*.mp4", SearchOption.TopDirectoryOnly);
							flag = flag2 && ValidateSplitOutputSet(ffmpeg, created, num4, list4, num3, out reason);
							if (!flag2)
							{
								errorText = error;
							}
							else if (flag)
							{
								streamWriter.WriteLine("  逐段精确补做成功，已生成完整的 " + created.Length + " 段。");
							}
						}
						if (_cancelRequested)
						{
							files2 = created;
							foreach (string path4 in files2)
							{
								TryDelete(path4);
							}
							throw new OperationCanceledException();
						}
						bool flag3 = true;
						string text4 = null;
						if (flag && currentWatermark.Enabled)
						{
							Array.Sort(created, StringComparer.OrdinalIgnoreCase);
							int segmentIndex;
							for (segmentIndex = 0; segmentIndex < created.Length; segmentIndex++)
							{
								if (_cancelRequested)
								{
									throw new OperationCanceledException();
								}
								string text5 = Path.Combine(text2, "watermarked_" + segmentIndex.ToString("000") + ".mp4");
								int num9 = RenderWatermarkVideoReliable(ffmpeg, created[segmentIndex], text5, currentWatermark, text2, delegate(double p)
								{
									double num11 = ((double)segmentIndex + p) / (double)Math.Max(1, created.Length);
									UiSplitProgress(((double)captured + 0.5 + num11 * 0.5) / (double)files.Count, "正在为第 " + (captured + 1) + "/" + files.Count + " 个视频片段添加水印");
								}, streamWriter, "片段 " + (segmentIndex + 1) + " 水印", out var error2, out var _);
								if (_cancelRequested)
								{
									TryDelete(text5);
									files2 = created;
									foreach (string path5 in files2)
									{
										TryDelete(path5);
									}
									throw new OperationCanceledException();
								}
								if (num9 != 0 || !File.Exists(text5))
								{
									flag3 = false;
									text4 = error2;
									break;
								}
								TryDelete(created[segmentIndex]);
								File.Move(text5, created[segmentIndex]);
							}
						}
						bool flag4 = true;
						string error3 = null;
						if (flag && flag3 && _activeSplitBgmPlan.Enabled)
						{
							Array.Sort(created, StringComparer.OrdinalIgnoreCase);
							for (int num10 = 0; num10 < created.Length; num10++)
							{
								string text6 = SelectBgm(_activeSplitBgmPlan, num2++, random);
								int capturedSegment = num10;
								if (!ApplyBackgroundMusic(ffmpeg, created[num10], text6, _activeSplitBgmPlan.VolumePercent, delegate(double p)
								{
									double num11 = ((double)capturedSegment + p) / (double)Math.Max(1, created.Length);
									UiSplitProgress(((double)captured + 0.8 + num11 * 0.2) / (double)files.Count, "正在为拆分片段加入 BGM 配乐");
								}, out error3))
								{
									flag4 = false;
									break;
								}
								streamWriter.WriteLine("  片段 " + (num10 + 1) + " BGM：" + text6);
							}
						}
						if (flag && flag3 && flag4)
						{
							succeeded++;
							totalSegments += created.Length;
							list.AddRange(created);
							streamWriter.WriteLine("  结果：成功，生成 " + created.Length + " 个片段");
						}
						else
						{
							failed++;
							files2 = created;
							foreach (string path6 in files2)
							{
								TryDelete(path6);
							}
							string text7 = ((!flag3) ? text4 : ((!flag4) ? ("BGM 配乐失败：" + error3) : (string.IsNullOrWhiteSpace(reason) ? errorText : reason)));
							string text8 = LastUsefulLines(text7, 4);
							list2.Add(Path.GetFileName(text) + "（" + text8 + "）");
							streamWriter.WriteLine("  结果：失败，预期 " + num4 + " 段，实际 " + created.Length + " 段。" + LastUsefulLines(text7, 10));
						}
					}
					finally
					{
						TryDeleteDirectory(text2);
					}
					streamWriter.WriteLine();
					UiSplitProgress(((double)i + 1.0) / (double)files.Count, "已完成 " + (i + 1) + "/" + files.Count + " 个视频");
				}
				streamWriter.WriteLine("完成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				streamWriter.WriteLine("成功视频：" + succeeded + "，失败视频：" + failed + "，生成片段：" + totalSegments);
				streamWriter.WriteLine("数量对账：预计片段 " + num + "，有效输出 " + list.Count + ((list.Count == num) ? "，一致" : ("，缺少 " + Math.Max(0, num - list.Count))));
			}
			WriteOutputReconciliation(outputFolder, "视频拆分", num, list, list2);
			string failureSummary = ((list2.Count == 0) ? "" : ("\n\n失败原因：\n" + string.Join("\n", list2.Take(3).ToArray())));
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_splitProgressBar.Value = 100;
				_splitStatusLabel.Text = "拆分完成：成功 " + succeeded + " 个视频，共生成 " + totalSegments + " 个片段。";
				ShowCompletionAndOpenFolder("批量拆分完成。\n\n成功视频：" + succeeded + "\n失败视频：" + failed + "\n生成片段：" + totalSegments + failureSummary + "\n\n本批次输出目录：\n" + outputFolder, (failed == 0) ? "拆分完成" : "拆分完成（有失败）", (failed == 0) ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation, outputParent, outputFolder);
			});
		}
		catch (OperationCanceledException)
		{
			list2.Add("任务由用户取消，尚未处理的输入未导出");
			WriteOutputReconciliation(outputFolder, "视频拆分", num, list, list2);
			TryAppendLog(path, "任务已由用户取消：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_splitStatusLabel.Text = "拆分任务已取消。已经完成的输出会保留。";
			});
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			list2.Add("任务异常中断：" + ex3.Message);
			WriteOutputReconciliation(outputFolder, "视频拆分", num, list, list2);
			TryAppendLog(path, "未处理异常：" + ex3);
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_splitStatusLabel.Text = "拆分遇到错误，请检查素材和拆分设置后重试。";
				MessageBox.Show(this, "拆分遇到错误：\n" + ex3.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			});
		}
		finally
		{
			TryDelete(path);
		}
	}

	private SplitPlan CaptureSplitPlan()
	{
		SplitPlan splitPlan = new SplitPlan();
		splitPlan.EqualPartsMode = _splitMode.SelectedIndex == 1;
		splitPlan.SegmentSeconds = decimal.ToDouble(_splitSeconds.Value);
		splitPlan.EqualParts = decimal.ToInt32(_splitEqualParts.Value);
		splitPlan.DiscardShortTail = _splitTailMode.SelectedIndex == 1;
		return splitPlan;
	}

	private MergePlanningOptions CaptureMergePlanningOptions()
	{
		MergePlanningOptions mergePlanningOptions = new MergePlanningOptions();
		mergePlanningOptions.MaxItemsPerGroup = decimal.ToInt32(_groupSize.Value);
		mergePlanningOptions.MaxRepeatsPerSource = decimal.ToInt32(_maxRepeatsPerSource.Value);
		mergePlanningOptions.MaxOutputCount = decimal.ToInt32(_maxOutputCount.Value);
		mergePlanningOptions.RandomCombinationStart = _maxOutputCount.Value > 0m && _combinationStartMode.SelectedIndex == 1;
		mergePlanningOptions.LimitDuration = _limitGroupDuration.Checked;
		mergePlanningOptions.MaxDurationSeconds = decimal.ToDouble(_maxGroupDuration.Value);
		mergePlanningOptions.RandomClipRanges = _limitGroupDuration.Checked && _overlongVideoMode.SelectedIndex == 1;
		mergePlanningOptions.OverlongMode = _overlongVideoMode.SelectedIndex;
		mergePlanningOptions.SortBySimilarity = _similaritySort.Checked;
		mergePlanningOptions.DetailedSimilarity = _similarityMode.SelectedIndex == 1;
		mergePlanningOptions.SimilarityThreshold = decimal.ToDouble(_similarityThreshold.Value) / 100.0;
		return mergePlanningOptions;
	}

	private TransitionPlan CaptureTransitionSettings()
	{
		TransitionPlan transitionPlan = new TransitionPlan();
		transitionPlan.DurationSeconds = decimal.ToDouble(_transitionDuration.Value);
		transitionPlan.RandomOrder = _transitionOrder.SelectedIndex == 1;
		transitionPlan.LockSingleEffectPerOutput = _transitionLockSingleEffect.Checked;
		List<TransitionSpec> list = CreateTransitionCatalog(transitionPlan.DurationSeconds);
		foreach (int checkedIndex in _transitionEffects.CheckedIndices)
		{
			if (checkedIndex >= 0 && checkedIndex < list.Count)
			{
				transitionPlan.Effects.Add(list[checkedIndex]);
			}
		}
		return transitionPlan;
	}

	private static List<TransitionSpec> CreateTransitionCatalog(double durationSeconds)
	{
		string customExpression = "A*(1-max(0,min(1,P+0.08*sin(4*PI*Y/H-3*PI*P)*sin(PI*P))))+B*max(0,min(1,P+0.08*sin(4*PI*Y/H-3*PI*P)*sin(PI*P)))";
		List<TransitionSpec> list = new List<TransitionSpec>();
		list.Add(NewTransition("交叉淡化", "fade", durationSeconds));
		list.Add(NewTransition("穿越融合", "zoomin", durationSeconds));
		list.Add(NewTransition("水面微波", "custom", durationSeconds, customExpression));
		list.Add(NewTransition("柔和溶解", "dissolve", durationSeconds));
		list.Add(NewTransition("模糊融合", "hblur", durationSeconds));
		list.Add(NewTransition("慢速柔化", "fadeslow", durationSeconds));
		list.Add(NewTransition("灰度柔化", "fadegrays", durationSeconds));
		list.Add(NewTransition("柔光扩散", "distance", durationSeconds));
		list.Add(NewTransition("柔滑向左", "smoothleft", durationSeconds));
		list.Add(NewTransition("柔滑向右", "smoothright", durationSeconds));
		list.Add(NewTransition("柔滑向上", "smoothup", durationSeconds));
		list.Add(NewTransition("柔滑向下", "smoothdown", durationSeconds));
		return list;
	}

	private static TransitionSpec NewTransition(string displayName, string ffmpegName, double durationSeconds, string customExpression = null)
	{
		TransitionSpec transitionSpec = new TransitionSpec();
		transitionSpec.Enabled = true;
		transitionSpec.DisplayName = displayName;
		transitionSpec.FfmpegName = ffmpegName;
		transitionSpec.CustomExpression = customExpression;
		transitionSpec.DurationSeconds = durationSeconds;
		return transitionSpec;
	}

	private static TransitionPlan CopyTransitionPlanWithDuration(TransitionPlan source, double duration)
	{
		TransitionPlan transitionPlan = new TransitionPlan();
		transitionPlan.RandomOrder = source.RandomOrder;
		transitionPlan.LockSingleEffectPerOutput = source.LockSingleEffectPerOutput;
		transitionPlan.DurationSeconds = duration;
		TransitionPlan transitionPlan2 = transitionPlan;
		foreach (TransitionSpec effect in source.Effects)
		{
			transitionPlan2.Effects.Add(new TransitionSpec
			{
				Enabled = effect.Enabled,
				DisplayName = effect.DisplayName,
				FfmpegName = effect.FfmpegName,
				CustomExpression = effect.CustomExpression,
				DurationSeconds = duration
			});
		}
		return transitionPlan2;
	}

	private static TransitionPlan SelectTransitionPlanForOutput(TransitionPlan source, int outputIndex)
	{
		if (source == null || !source.LockSingleEffectPerOutput || source.Effects.Count <= 1)
		{
			return source;
		}
		int index = ((!source.RandomOrder) ? (Math.Abs(outputIndex) % source.Effects.Count) : new Random(Guid.NewGuid().GetHashCode()).Next(source.Effects.Count));
		TransitionPlan transitionPlan = new TransitionPlan();
		transitionPlan.DurationSeconds = source.DurationSeconds;
		transitionPlan.RandomOrder = false;
		transitionPlan.LockSingleEffectPerOutput = true;
		TransitionPlan transitionPlan2 = transitionPlan;
		transitionPlan2.Effects.Add(source.Effects[index]);
		return transitionPlan2;
	}

	private WatermarkProfile CaptureWatermarkProfile(bool enabled)
	{
		WatermarkProfile watermarkProfile = new WatermarkProfile();
		watermarkProfile.Enabled = enabled;
		WatermarkProfile watermarkProfile2 = watermarkProfile;
		if (!enabled)
		{
			return watermarkProfile2;
		}
		for (int i = 0; i < 3; i++)
		{
			if (_textWatermarkEnabled[i].Checked)
			{
				watermarkProfile2.Layers.Add(new WatermarkSettings
				{
					Enabled = true,
					IsText = true,
					Text = _watermarkText[i].Text,
					FontSize = decimal.ToSingle(_watermarkFontSize[i].Value),
					TextMaxWidthPercent = decimal.ToInt32(_textWatermarkMaxWidth[i].Value),
					SafeMarginPercent = decimal.ToInt32(_textWatermarkSafeMargin[i].Value),
					AllowOverflow = _textWatermarkAllowOverflow[i].Checked,
					PlaceAboveImages = _textWatermarkAboveImages[i].Checked,
					FontFamilyName = SelectedWatermarkFont(_watermarkFontFamily[i]),
					FontBold = _watermarkFontBold[i].Checked,
					FontItalic = _watermarkFontItalic[i].Checked,
					TextColor = _watermarkColor[i],
					TextAlignment = SelectedTextAlignment(_textWatermarkAlignment[i]),
					TextOutlineEnabled = _textWatermarkOutlineEnabled[i].Checked,
					TextOutlineColor = _watermarkOutlineColor[i],
					TextOutlineWidth = decimal.ToInt32(_textWatermarkOutlineWidth[i].Value),
					Opacity = decimal.ToDouble(_textWatermarkOpacity[i].Value) / 100.0,
					Position = SelectedPosition(_textWatermarkPosition[i]),
					ImageWidthPercent = 20,
					OffsetX = decimal.ToInt32(_textWatermarkOffsetX[i].Value),
					OffsetY = decimal.ToInt32(_textWatermarkOffsetY[i].Value),
					StartSeconds = decimal.ToDouble(_textWatermarkStart[i].Value),
					ShowUntilEnd = _textWatermarkShowUntilEnd[i].Checked,
					EndSeconds = decimal.ToDouble(_textWatermarkEnd[i].Value),
					EntryEffect = SelectedEntryEffect(_textWatermarkEntryEffect[i]),
					EntryDurationSeconds = decimal.ToDouble(_textWatermarkEntryDuration[i].Value),
					ExitEffect = SelectedExitEffect(_textWatermarkExitEffect[i]),
					ExitDurationSeconds = decimal.ToDouble(_textWatermarkExitDuration[i].Value),
					StayEffect = SelectedStayEffect(_textWatermarkStayEffect[i]),
					StayIntensity = decimal.ToDouble(_textWatermarkStayIntensity[i].Value) / 100.0,
					StayPeriodSeconds = decimal.ToDouble(_textWatermarkStayPeriod[i].Value),
					StayPauseSeconds = decimal.ToDouble(_textWatermarkStayPause[i].Value),
					TextBackgroundEnabled = _textWatermarkBackgroundEnabled[i].Checked,
					TextBackgroundColor = _watermarkBackgroundColor[i],
					TextBackgroundOpacity = decimal.ToDouble(_textWatermarkBackgroundOpacity[i].Value) / 100.0,
					TextBackgroundPaddingX = decimal.ToInt32(_textWatermarkBackgroundPaddingX[i].Value),
					TextBackgroundPaddingY = decimal.ToInt32(_textWatermarkBackgroundPaddingY[i].Value),
					TextBackgroundCornerRadius = decimal.ToInt32(_textWatermarkBackgroundRadius[i].Value),
					TextBackgroundStyle = SelectedTextBackgroundStyle(_textWatermarkBackgroundStyle[i])
				});
			}
		}
		for (int j = 0; j < 3; j++)
		{
			if (!_imageWatermarkEnabled[j].Checked)
			{
				continue;
			}
			SaveImageWatermarkSelection(j);
			WatermarkImageLibrary watermarkImageLibrary = new WatermarkImageLibrary();
			watermarkImageLibrary.CandidateCount = Math.Min(_imageWatermarkItems[j].Count, decimal.ToInt32(_imageWatermarkRandomCount[j].Value));
			watermarkImageLibrary.RandomAssignment = _imageWatermarkAssignmentMode[j].SelectedIndex == 0;
			watermarkImageLibrary.Slideshow = _imageWatermarkPlaybackMode[j].SelectedIndex == 1 && _imageWatermarkItems[j].Count > 1;
			watermarkImageLibrary.SwitchEffect = ComboText(_imageWatermarkSwitchEffect[j]);
			watermarkImageLibrary.SwitchDurationSeconds = decimal.ToDouble(_imageWatermarkSwitchDuration[j].Value);
			watermarkImageLibrary.SwitchIntervalSeconds = decimal.ToDouble(_imageWatermarkSwitchInterval[j].Value);
			foreach (WatermarkSettings item in _imageWatermarkItems[j])
			{
				watermarkImageLibrary.Images.Add(CloneWatermarkSettings(item));
			}
			watermarkProfile2.ImageLibraries.Add(watermarkImageLibrary);
		}
		return watermarkProfile2;
	}

	private static string SelectedPosition(ComboBox combo)
	{
		if (combo.SelectedItem != null)
		{
			return combo.SelectedItem.ToString();
		}
		return "右下角";
	}

	private static string SelectedTextAlignment(ComboBox combo)
	{
		string text = ComboText(combo);
		if (!(text == "居中对齐") && !(text == "右对齐"))
		{
			return "左对齐";
		}
		return text;
	}

	private static string SelectedTextBackgroundStyle(ComboBox combo)
	{
		if (!(ComboText(combo) == "逐行包裹文字"))
		{
			return "整块背景";
		}
		return "逐行包裹文字";
	}

	private static string SelectedWatermarkFont(ComboBox combo)
	{
		string text = ((combo.SelectedItem == null) ? "" : combo.SelectedItem.ToString());
		if (text.StartsWith("Noto Serif SC", StringComparison.Ordinal))
		{
			return "Noto Serif SC";
		}
		if (text.StartsWith("微软雅黑", StringComparison.Ordinal))
		{
			return "Microsoft YaHei UI";
		}
		if (text.StartsWith("黑体", StringComparison.Ordinal))
		{
			return "SimHei";
		}
		if (text.StartsWith("宋体", StringComparison.Ordinal))
		{
			return "SimSun";
		}
		return "Noto Sans SC";
	}

	private static string SelectedEntryEffect(ComboBox combo)
	{
		if (combo.SelectedItem != null)
		{
			return combo.SelectedItem.ToString();
		}
		return "直接出现";
	}

	private static string NormalizeEntryEffect(string effect)
	{
		switch (effect)
		{
		case "淡入":
		case "放大进入":
		case "从左滑入":
		case "从上滑入":
		case "从右滑入":
		case "从下浮入":
		case "底部弹出":
		case "弹跳进入":
		case "旋转进入":
			return effect;
		default:
			return "直接出现";
		}
	}

	private static string SelectedExitEffect(ComboBox combo)
	{
		if (combo.SelectedItem != null)
		{
			return combo.SelectedItem.ToString();
		}
		return "直接消失";
	}

	private static string NormalizeExitEffect(string effect)
	{
		switch (effect)
		{
		case "淡出":
		case "缩小退出":
		case "向右滑出":
		case "向下滑出":
			return effect;
		default:
			return "直接消失";
		}
	}

	private static string SelectedStayEffect(ComboBox combo)
	{
		if (combo.SelectedItem != null)
		{
			return combo.SelectedItem.ToString();
		}
		return "无";
	}

	private static string NormalizeStayEffect(string effect)
	{
		if (effect == "柔光扫过")
		{
			effect = "金色斜向扫光";
		}
		switch (effect)
		{
		case "轻微漂浮":
		case "呼吸缩放":
		case "轻柔摇摆":
		case "持续旋转":
		case "律动弹跳":
		case "轻微抖动":
		case "闪烁":
		case "金色斜向扫光":
		case "星光粒子":
			return effect;
		default:
			return "无";
		}
	}

	private static string NormalizeWatermarkSwitchEffect(string effect)
	{
		switch (effect)
		{
		case "柔和缩放":
		case "滑动切换":
		case "直接切换":
			return effect;
		default:
			return "交叉淡化";
		}
	}

	private static WatermarkSettings CloneWatermarkSettings(WatermarkSettings source)
	{
		WatermarkSettings watermarkSettings = new WatermarkSettings();
		watermarkSettings.Enabled = source.Enabled;
		watermarkSettings.IsText = source.IsText;
		watermarkSettings.Text = source.Text;
		watermarkSettings.ImagePath = source.ImagePath;
		watermarkSettings.FontSize = source.FontSize;
		watermarkSettings.TextMaxWidthPercent = ((source.TextMaxWidthPercent <= 0) ? 85 : source.TextMaxWidthPercent);
		watermarkSettings.SafeMarginPercent = Math.Max(0, source.SafeMarginPercent);
		watermarkSettings.AllowOverflow = source.AllowOverflow;
		watermarkSettings.PlaceAboveImages = source.PlaceAboveImages;
		watermarkSettings.FontFamilyName = source.FontFamilyName;
		watermarkSettings.FontBold = source.FontBold;
		watermarkSettings.FontItalic = source.FontItalic;
		watermarkSettings.TextColor = source.TextColor;
		watermarkSettings.TextAlignment = ((source.TextAlignment == "居中对齐" || source.TextAlignment == "右对齐") ? source.TextAlignment : "左对齐");
		watermarkSettings.TextOutlineEnabled = source.TextOutlineEnabled;
		watermarkSettings.TextOutlineColor = (source.TextOutlineColor.IsEmpty ? Color.Black : source.TextOutlineColor);
		watermarkSettings.TextOutlineWidth = ((source.TextOutlineWidth <= 0) ? 2 : source.TextOutlineWidth);
		watermarkSettings.Opacity = source.Opacity;
		watermarkSettings.Position = source.Position;
		watermarkSettings.ImageWidthPercent = source.ImageWidthPercent;
		watermarkSettings.OffsetX = source.OffsetX;
		watermarkSettings.OffsetY = source.OffsetY;
		watermarkSettings.StartSeconds = source.StartSeconds;
		watermarkSettings.ShowUntilEnd = source.ShowUntilEnd;
		watermarkSettings.EndSeconds = source.EndSeconds;
		watermarkSettings.EntryEffect = NormalizeEntryEffect(source.EntryEffect);
		watermarkSettings.EntryDurationSeconds = ((source.EntryDurationSeconds <= 0.0) ? 1.0 : source.EntryDurationSeconds);
		watermarkSettings.ExitEffect = NormalizeExitEffect(source.ExitEffect);
		watermarkSettings.ExitDurationSeconds = ((source.ExitDurationSeconds <= 0.0) ? 1.0 : source.ExitDurationSeconds);
		watermarkSettings.StayEffect = NormalizeStayEffect(source.StayEffect);
		watermarkSettings.StayIntensity = Math.Max(0.0, Math.Min(1.0, source.StayIntensity));
		watermarkSettings.StayPeriodSeconds = ((source.StayPeriodSeconds <= 0.0) ? 2.0 : source.StayPeriodSeconds);
		watermarkSettings.StayPauseSeconds = Math.Max(0.0, source.StayPauseSeconds);
		watermarkSettings.SlideDurationSeconds = ((source.SlideDurationSeconds <= 0.0) ? 3.0 : source.SlideDurationSeconds);
		watermarkSettings.TextBackgroundEnabled = source.TextBackgroundEnabled;
		watermarkSettings.TextBackgroundColor = source.TextBackgroundColor;
		watermarkSettings.TextBackgroundOpacity = source.TextBackgroundOpacity;
		watermarkSettings.TextBackgroundPaddingX = source.TextBackgroundPaddingX;
		watermarkSettings.TextBackgroundPaddingY = source.TextBackgroundPaddingY;
		watermarkSettings.TextBackgroundCornerRadius = source.TextBackgroundCornerRadius;
		watermarkSettings.TextBackgroundStyle = ((source.TextBackgroundStyle == "逐行包裹文字") ? "逐行包裹文字" : "整块背景");
		return watermarkSettings;
	}

	private static string ValidateWatermark(WatermarkProfile profile)
	{
		if (!profile.Enabled)
		{
			return null;
		}
		if (profile.Layers.Count == 0 && profile.ImageLibraries.Count == 0)
		{
			return "请至少启用一个文字或图片水印图层。";
		}
		for (int i = 0; i < profile.Layers.Count; i++)
		{
			WatermarkSettings watermarkSettings = profile.Layers[i];
			if (watermarkSettings.IsText && string.IsNullOrWhiteSpace(watermarkSettings.Text))
			{
				return "已启用文字水印，请输入水印文字。";
			}
			if (!watermarkSettings.IsText && !File.Exists(watermarkSettings.ImagePath))
			{
				return "已启用图片水印，但图片文件不存在：\n" + watermarkSettings.ImagePath;
			}
			if (!watermarkSettings.ShowUntilEnd && watermarkSettings.EndSeconds > 0.0 && watermarkSettings.EndSeconds <= watermarkSettings.StartSeconds)
			{
				return "水印结束秒数必须大于开始秒数。";
			}
		}
		foreach (WatermarkImageLibrary imageLibrary in profile.ImageLibraries)
		{
			if (imageLibrary.Images.Count == 0)
			{
				return "已启用图片水印库，但还没有导入图片。";
			}
			foreach (WatermarkSettings image in imageLibrary.Images)
			{
				if (!File.Exists(image.ImagePath))
				{
					return "图片水印文件不存在：\n" + image.ImagePath;
				}
				if (!image.ShowUntilEnd && image.EndSeconds > 0.0 && image.EndSeconds <= image.StartSeconds)
				{
					return "图片水印结束秒数必须大于开始秒数。";
				}
			}
		}
		return null;
	}

	private static int WatermarkLayerCount(WatermarkProfile profile)
	{
		return profile.Layers.Count + profile.ImageLibraries.Count;
	}

	private static List<WatermarkProfile> CreateWatermarkAssignments(WatermarkProfile profile, int outputCount, bool expandSlideshows = true)
	{
		List<WatermarkProfile> list = new List<WatermarkProfile>();
		for (int i = 0; i < outputCount; i++)
		{
			WatermarkProfile watermarkProfile = new WatermarkProfile();
			watermarkProfile.Enabled = profile.Enabled;
			WatermarkProfile watermarkProfile2 = watermarkProfile;
			watermarkProfile2.Layers.AddRange(profile.Layers.Select(CloneWatermarkSettings));
			list.Add(watermarkProfile2);
		}
		if (!profile.Enabled || outputCount == 0)
		{
			return list;
		}
		Random random = new Random(Guid.NewGuid().GetHashCode());
		foreach (WatermarkImageLibrary imageLibrary in profile.ImageLibraries)
		{
			int num = Math.Max(1, Math.Min(imageLibrary.CandidateCount, imageLibrary.Images.Count));
			if (imageLibrary.Slideshow && expandSlideshows && num > 1)
			{
				for (int j = 0; j < outputCount; j++)
				{
					List<WatermarkSettings> list2 = (imageLibrary.RandomAssignment ? imageLibrary.Images.OrderBy((WatermarkSettings x) => random.Next()).Take(num).ToList() : imageLibrary.Images.Take(num).ToList());
					WatermarkImageLibrary watermarkImageLibrary = new WatermarkImageLibrary();
					watermarkImageLibrary.CandidateCount = list2.Count;
					watermarkImageLibrary.RandomAssignment = imageLibrary.RandomAssignment;
					watermarkImageLibrary.Slideshow = true;
					watermarkImageLibrary.SwitchEffect = NormalizeWatermarkSwitchEffect(imageLibrary.SwitchEffect);
					watermarkImageLibrary.SwitchDurationSeconds = Math.Max(0.1, imageLibrary.SwitchDurationSeconds);
					watermarkImageLibrary.SwitchIntervalSeconds = Math.Max(0.0, imageLibrary.SwitchIntervalSeconds);
					WatermarkImageLibrary watermarkImageLibrary2 = watermarkImageLibrary;
					foreach (WatermarkSettings item in list2)
					{
						watermarkImageLibrary2.Images.Add(CloneWatermarkSettings(item));
					}
					list[j].ImageLibraries.Add(watermarkImageLibrary2);
				}
				continue;
			}
			if (!imageLibrary.RandomAssignment)
			{
				List<WatermarkSettings> list3 = imageLibrary.Images.Take(num).ToList();
				for (int num2 = 0; num2 < outputCount; num2++)
				{
					list[num2].Layers.Add(CloneWatermarkSettings(list3[num2 % list3.Count]));
				}
				continue;
			}
			List<WatermarkSettings> source = imageLibrary.Images.OrderBy((WatermarkSettings x) => random.Next()).ToList();
			List<WatermarkSettings> source2 = source.Take(num).ToList();
			List<WatermarkSettings> list4 = new List<WatermarkSettings>();
			for (int num3 = 0; num3 < outputCount; num3++)
			{
				if (list4.Count == 0)
				{
					list4 = source2.OrderBy((WatermarkSettings x) => random.Next()).ToList();
				}
				WatermarkSettings source3 = list4[0];
				list4.RemoveAt(0);
				list[num3].Layers.Add(CloneWatermarkSettings(source3));
			}
		}
		return list;
	}

	private void RunBatch(string ffmpeg, List<string> files, string outputFolder, int groupSize, bool allowFallback, TransitionPlan transition, WatermarkProfile watermark)
	{
		RunBatchPlanned(ffmpeg, files, outputFolder, new MergePlanningOptions
		{
			MaxItemsPerGroup = groupSize,
			MaxRepeatsPerSource = 0,
			MaxOutputCount = 0,
			RandomCombinationStart = false,
			LimitDuration = false,
			MaxDurationSeconds = 120.0,
			RandomClipRanges = false,
			OverlongMode = 0,
			SortBySimilarity = false,
			DetailedSimilarity = false,
			SimilarityThreshold = 0.65
		}, allowFallback, transition, watermark);
	}

	private void RunBatchPlanned(string ffmpeg, List<string> files, string outputParent, MergePlanningOptions options, bool allowFallback, TransitionPlan transition, WatermarkProfile watermark)
	{
		int succeeded = 0;
		int failed = 0;
		int expectedCount = 0;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string outputFolder = null;
		string path = null;
		string text = Path.Combine(Path.GetTempPath(), "VideoBatchPlanning_" + Guid.NewGuid().ToString("N"));
		try
		{
			outputFolder = CreateBatchOutputFolder(outputParent, "批量合并");
			_latestMergeOutputFolder = outputFolder;
			path = Path.Combine(Path.GetTempPath(), "VideoBatch_Merge_" + Guid.NewGuid().ToString("N") + ".log");
			Directory.CreateDirectory(text);
			using (StreamWriter streamWriter = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
			{
				streamWriter.WriteLine("视频批量合并记录");
				streamWriter.WriteLine("开始时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				streamWriter.WriteLine("FFmpeg：" + ffmpeg);
				streamWriter.WriteLine("每组最多数量：" + options.MaxItemsPerGroup);
				streamWriter.WriteLine("每个视频最多额外重复：" + options.MaxRepeatsPerSource + " 次");
				streamWriter.WriteLine("最多输出总数：" + ((options.MaxOutputCount > 0) ? (options.MaxOutputCount + " 个") : "不限"));
				streamWriter.WriteLine("组合起点：" + ((options.MaxOutputCount > 0 && options.RandomCombinationStart) ? "随机起点" : "按列表顺序"));
				streamWriter.WriteLine("统一成品时长：" + (options.LimitDuration ? (FfmpegNumber(options.MaxDurationSeconds) + " 秒 / " + (options.RandomClipRanges ? "组内每个视频随机截取" : "组内平均截取各视频开头")) : "未启用（按每组内完整素材总时长输出）"));
				streamWriter.WriteLine("画面适配：" + (_activeMergeFillCanvas ? "自动放大铺满（保持比例并居中裁掉少量边缘）" : "完整显示（保持比例，空白区域补黑边）"));
				streamWriter.WriteLine("输出画幅：" + DescribeOutputFrame(_activeMergeOutputFrame));
				streamWriter.WriteLine("相似画面排序：" + (options.SortBySimilarity ? ((options.DetailedSimilarity ? "精细分析" : "快速分析") + " / 相似要求 " + Math.Round(options.SimilarityThreshold * 100.0) + "%") : "未启用"));
				streamWriter.WriteLine("转场：" + (transition.Enabled ? (string.Join("、", transition.Effects.Select((TransitionSpec x) => x.DisplayName).ToArray()) + " / " + transition.DurationSeconds + " 秒 / " + (transition.RandomOrder ? "随机使用" : "顺序循环") + (transition.LockSingleEffectPerOutput ? " / 每条成品锁定一种" : "")) : "无"));
				streamWriter.WriteLine("水印：" + (watermark.Enabled ? (WatermarkLayerCount(watermark) + " 个图层") : "无"));
				streamWriter.WriteLine();
				List<PlannedMergeItem> list3 = PrepareMergeItems(ffmpeg, files, options, text, transition, streamWriter, out var skipped);
				failed += skipped;
				if (skipped > 0)
				{
					list2.Add("分析阶段有 " + skipped + " 个素材被跳过");
				}
				MergePlanningOptions mergePlanningOptions = CopyMergePlanningOptions(options);
				mergePlanningOptions.MaxRepeatsPerSource = 0;
				mergePlanningOptions.MaxOutputCount = 0;
				int count = BuildMergeGroups(list3, mergePlanningOptions, transition).Count;
				if (options.MaxOutputCount > 0 && count > options.MaxOutputCount)
				{
					streamWriter.WriteLine("数量限制：完整覆盖全部素材原本需要 " + count + " 个输出；按用户设置只生成 " + options.MaxOutputCount + " 个，其余未入组素材主动舍弃。");
				}
				List<List<PlannedMergeItem>> list4 = BuildMergeGroups(list3, options, transition);
				int totalGroups = list4.Count;
				expectedCount = totalGroups;
				HashSet<string> usedSourcePaths = new HashSet<string>(from x in list4.SelectMany((List<PlannedMergeItem> x) => x)
					select x.Path, StringComparer.OrdinalIgnoreCase);
				List<PlannedMergeItem> list5 = (from x in list3.GroupBy((PlannedMergeItem x) => x.Path, StringComparer.OrdinalIgnoreCase)
					select x.First() into x
					where !usedSourcePaths.Contains(x.Path)
					select x).ToList();
				if (list5.Count > 0 && options.MaxOutputCount > 0)
				{
					streamWriter.WriteLine("依照最多输出总数主动舍弃素材：" + list5.Count + " 个");
					foreach (PlannedMergeItem item in list5)
					{
						streamWriter.WriteLine("  舍弃：" + item.OriginalPath);
					}
					streamWriter.WriteLine();
				}
				List<WatermarkProfile> list6 = CreateWatermarkAssignments(watermark, totalGroups);
				Random random = new Random(Guid.NewGuid().GetHashCode());
				streamWriter.WriteLine("最终分组：" + totalGroups + " 组");
				Dictionary<string, int> dictionary = list4.SelectMany((List<PlannedMergeItem> x) => x).GroupBy((PlannedMergeItem x) => x.Path, StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, PlannedMergeItem> x) => x.Key, (IGrouping<string, PlannedMergeItem> x) => x.Count(), StringComparer.OrdinalIgnoreCase);
				int num = dictionary.Values.Sum((int x) => Math.Max(0, x - 1));
				int num2 = list4.Count((List<PlannedMergeItem> x) => x.Count == 1 && !IsAllowedOverlongMergeItem(x[0], options));
				streamWriter.WriteLine("实际额外重复使用：" + num + " 次；单视频尾组：" + num2 + " 组");
				if (num2 > 0)
				{
					streamWriter.WriteLine("提示：仍有单视频组，原因是当前每组数量或重复次数限制下无法安全配对。");
				}
				streamWriter.WriteLine();
				if (totalGroups == 0)
				{
					streamWriter.WriteLine("没有可处理的视频。请检查素材或分组设置。");
					WriteOutputReconciliation(outputFolder, "批量合并", expectedCount, list, list2);
					BeginInvoke((MethodInvoker)delegate
					{
						SetRunningState(running: false);
						_progressBar.Value = 0;
						_statusLabel.Text = "没有可处理的视频，请检查素材和当前分组设置。";
						MessageBox.Show(this, "没有生成输出视频。请检查素材是否可读取，以及当前分组、时长和重复限制。", "没有可处理的视频", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					});
					return;
				}
				for (int num3 = 0; num3 < totalGroups; num3++)
				{
					if (_cancelRequested)
					{
						throw new OperationCanceledException();
					}
					List<PlannedMergeItem> list7 = list4[num3];
					WatermarkProfile watermarkProfile = list6[num3];
					TransitionPlan transitionPlan = SelectTransitionPlanForOutput(transition, num3);
					List<string> files2 = list7.Select((PlannedMergeItem x) => x.Path).ToList();
					string text2 = FindAvailableOutput(outputFolder, num3 + 1);
					int capturedIndex = num3;
					double num4 = (options.LimitDuration ? options.MaxDurationSeconds : EstimateMergedDuration(list7, transitionPlan));
					double num5 = (options.LimitDuration ? options.MaxDurationSeconds : 0.0);
					UiStatus("正在处理第 " + (num3 + 1) + "/" + totalGroups + " 组：分析视频…");
					streamWriter.WriteLine("第 " + (num3 + 1) + " 组（预计 " + FfmpegNumber(num4) + " 秒）：");
					foreach (PlannedMergeItem item2 in list7)
					{
						streamWriter.WriteLine("  " + item2.Path + ((item2.Path == item2.OriginalPath) ? "" : ("  ← " + item2.OriginalPath)));
					}
					if (num5 > 0.0)
					{
						double num6 = ((transitionPlan.Enabled && list7.Count > 1) ? (transitionPlan.DurationSeconds * (double)(list7.Count - 1)) : 0.0);
						streamWriter.WriteLine("  " + (options.RandomClipRanges ? "随机截取" : "平均截取开头") + "：每个视频约 " + FfmpegNumber((num5 + num6) / (double)list7.Count) + " 秒（转场重叠后成品严格为 " + FfmpegNumber(num5) + " 秒）");
					}
					if (transition.LockSingleEffectPerOutput && transitionPlan.Enabled)
					{
						streamWriter.WriteLine("  本条成品锁定转场：" + transitionPlan.Effects[0].DisplayName);
					}
					streamWriter.WriteLine("  输出：" + text2);
					MergeResult mergeResult = null;
					for (int num7 = 1; num7 <= 4; num7++)
					{
						bool flag = num7 >= 3;
						bool flag2 = num7 == 4;
						WatermarkProfile watermark2 = (flag ? MakeSafeWatermarkProfile(watermarkProfile) : watermarkProfile);
						TransitionPlan transitionPlan2;
						if (!flag2)
						{
							transitionPlan2 = transitionPlan;
						}
						else
						{
							TransitionPlan transitionPlan3 = new TransitionPlan();
							transitionPlan3.DurationSeconds = transitionPlan.DurationSeconds;
							transitionPlan3.RandomOrder = false;
							transitionPlan2 = transitionPlan3;
						}
						TransitionPlan transition2 = transitionPlan2;
						TryDelete(text2);
						mergeResult = MergeGroupWithProfileCapped(ffmpeg, files2, text2, allowFallback, transition2, watermark2, num5, options.RandomClipRanges, delegate(double groupProgress, string stage)
						{
							double ratio = 0.2 + 0.8 * ((double)capturedIndex + Math.Max(0.0, Math.Min(1.0, groupProgress))) / (double)totalGroups;
							UiProgress(ratio, "第 " + (capturedIndex + 1) + "/" + totalGroups + " 组 · " + stage);
						}, streamWriter);
						string reason = null;
						double expectedDuration = ((num5 > 0.0) ? num5 : ((mergeResult.RenderedDurationSeconds > 0.0) ? mergeResult.RenderedDurationSeconds : num4));
						if (mergeResult.Success && ((num5 > 0.0) ? ValidateExactDurationOutput(ffmpeg, text2, expectedDuration, out reason) : ValidateVideoOutput(ffmpeg, text2, expectedDuration, out reason)))
						{
							if (flag2)
							{
								streamWriter.WriteLine("  提示：为保证输出数量，本组使用无转场安全方式补做成功。");
							}
							else if (flag)
							{
								streamWriter.WriteLine("  提示：本组使用安全水印入场方式补做成功。");
							}
							break;
						}
						if (mergeResult.Success)
						{
							MergeResult mergeResult2 = new MergeResult();
							mergeResult2.Error = reason;
							mergeResult = mergeResult2;
						}
						TryDelete(text2);
						streamWriter.WriteLine("  第 " + num7 + " 次输出未通过完整性校验，将自动补做。");
					}
					if (mergeResult.Cancelled)
					{
						throw new OperationCanceledException();
					}
					if (mergeResult.Success && _activeMergeBgmPlan.Enabled)
					{
						string text3 = SelectBgm(_activeMergeBgmPlan, num3, random);
						if (!ApplyBackgroundMusic(ffmpeg, text2, text3, _activeMergeBgmPlan.VolumePercent, delegate(double p)
						{
							UiProgress(0.2 + 0.8 * ((double)capturedIndex + 0.85 + p * 0.15) / (double)totalGroups, "第 " + (capturedIndex + 1) + "/" + totalGroups + " 组 · 正在加入 BGM");
						}, out var error))
						{
							MergeResult mergeResult3 = new MergeResult();
							mergeResult3.Error = "BGM 配乐失败：" + error;
							mergeResult = mergeResult3;
							TryDelete(text2);
						}
						else
						{
							streamWriter.WriteLine("  BGM：" + text3);
						}
					}
					if (mergeResult.Success)
					{
						succeeded++;
						list.Add(text2);
						streamWriter.WriteLine("  结果：成功");
					}
					else
					{
						failed++;
						list2.Add("第 " + (num3 + 1) + " 组（" + string.Join("、", list7.Select((PlannedMergeItem x) => Path.GetFileName(x.OriginalPath)).Distinct().ToArray()) + "）");
						streamWriter.WriteLine("  结果：失败");
						streamWriter.WriteLine("  原因：" + mergeResult.Error);
					}
					streamWriter.WriteLine();
					UiProgress(0.2 + 0.8 * ((double)num3 + 1.0) / (double)totalGroups, "已完成 " + (num3 + 1) + "/" + totalGroups + " 组");
				}
				streamWriter.WriteLine("完成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				streamWriter.WriteLine("成功：" + succeeded + "，失败：" + failed);
				streamWriter.WriteLine("数量对账：预计合并输出 " + totalGroups + "，有效输出 " + list.Count + ((list.Count == totalGroups) ? "，一致" : ("，缺少 " + Math.Max(0, totalGroups - list.Count))));
			}
			WriteOutputReconciliation(outputFolder, "批量合并", expectedCount, list, list2);
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_progressBar.Value = 100;
				_statusLabel.Text = "处理完成：成功 " + succeeded + " 组，失败 " + failed + " 组。";
				ShowCompletionAndOpenFolder("批量处理完成。\n\n成功：" + succeeded + " 组\n失败或跳过：" + failed + "\n\n本批次输出目录：\n" + outputFolder, (failed == 0) ? "合并完成" : "处理完成（有失败）", (failed == 0) ? MessageBoxIcon.Asterisk : MessageBoxIcon.Exclamation, outputParent, outputFolder);
			});
		}
		catch (OperationCanceledException)
		{
			list2.Add("任务由用户取消，尚未处理的分组未导出");
			WriteOutputReconciliation(outputFolder, "批量合并", expectedCount, list, list2);
			TryAppendLog(path, "任务已由用户取消：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_statusLabel.Text = "任务已取消。已经完成的输出文件会保留。";
			});
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			list2.Add("任务异常中断：" + ex3.Message);
			WriteOutputReconciliation(outputFolder, "批量合并", expectedCount, list, list2);
			TryAppendLog(path, "未处理异常：" + ex3);
			BeginInvoke((MethodInvoker)delegate
			{
				SetRunningState(running: false);
				_statusLabel.Text = "处理遇到错误，请检查素材和合并设置后重试。";
				MessageBox.Show(this, "处理遇到错误：\n" + ex3.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			});
		}
		finally
		{
			TryDeleteDirectory(text);
			TryDelete(path);
		}
	}

	private List<PlannedMergeItem> PrepareMergeItems(string ffmpeg, List<string> files, MergePlanningOptions options, string tempRoot, TransitionPlan transition, StreamWriter log, out int skipped)
	{
		skipped = 0;
		List<PlannedMergeItem> list = new List<PlannedMergeItem>();
		for (int i = 0; i < files.Count; i++)
		{
			if (_cancelRequested)
			{
				throw new OperationCanceledException();
			}
			UiProgress(0.16 * (double)i / (double)Math.Max(1, files.Count), "正在分析素材 " + (i + 1) + "/" + files.Count);
			VideoInfo videoInfo = Probe(ffmpeg, files[i]);
			if (!videoInfo.HasVideo || videoInfo.DurationSeconds <= 0.0)
			{
				skipped++;
				log.WriteLine("跳过无法识别的素材：" + files[i]);
				continue;
			}
			VideoAdjustmentSettings videoAdjustment = GetVideoAdjustment(_activeMergeVideoAdjustments, files[i]);
			PlannedMergeItem plannedMergeItem = new PlannedMergeItem();
			plannedMergeItem.Path = files[i];
			plannedMergeItem.OriginalPath = files[i];
			plannedMergeItem.DurationSeconds = videoInfo.DurationSeconds / Math.Max(0.1, videoAdjustment.SpeedRatio);
			PlannedMergeItem plannedMergeItem2 = plannedMergeItem;
			log.WriteLine("素材调整：" + files[i] + " → " + DescribeVideoAdjustment(videoAdjustment) + "，调整后约 " + FfmpegNumber(plannedMergeItem2.DurationSeconds) + " 秒");
			if (options.SortBySimilarity)
			{
				UiProgress(0.16 * ((double)i + 0.35) / (double)Math.Max(1, files.Count), "正在提取代表画面 " + (i + 1) + "/" + files.Count);
				plannedMergeItem2.VisualFeature = ExtractVideoVisualFeature(ffmpeg, videoInfo, options.DetailedSimilarity, tempRoot, i);
				if (plannedMergeItem2.VisualFeature == null)
				{
					log.WriteLine("画面分析失败，将保留原顺序：" + files[i]);
				}
			}
			list.Add(plannedMergeItem2);
		}
		if (options.SortBySimilarity)
		{
			list = OrderItemsBySimilarity(list, options.SimilarityThreshold);
			UiApplyMergeOrder(list.Select((PlannedMergeItem x) => x.OriginalPath).ToList());
			log.WriteLine("已完成相似画面排序：");
			for (int num = 0; num < list.Count; num++)
			{
				log.WriteLine("  " + (num + 1) + ". " + list[num].OriginalPath);
			}
			log.WriteLine();
		}
		if (options.LimitDuration)
		{
			log.WriteLine("已启用统一成品时长：分组只按每组数量进行；渲染时" + (options.RandomClipRanges ? "从每个视频内随机截取所需片段，" : "平均截取每个视频开头，") + "并统一输出为 " + FfmpegNumber(options.MaxDurationSeconds) + " 秒。");
			UiProgress(0.2, "分析与自动分组已完成");
			return list;
		}
		List<PlannedMergeItem> list2 = new List<PlannedMergeItem>();
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			PlannedMergeItem plannedMergeItem3 = list[num2];
			if (!options.LimitDuration || plannedMergeItem3.DurationSeconds <= options.MaxDurationSeconds + 0.001)
			{
				list2.Add(plannedMergeItem3);
				continue;
			}
			if (options.OverlongMode == 1)
			{
				skipped++;
				log.WriteLine("跳过超长素材（" + FfmpegNumber(plannedMergeItem3.DurationSeconds) + " 秒）：" + plannedMergeItem3.OriginalPath);
				continue;
			}
			if (options.OverlongMode == 2)
			{
				log.WriteLine("允许超长素材单独输出（" + FfmpegNumber(plannedMergeItem3.DurationSeconds) + " 秒）：" + plannedMergeItem3.OriginalPath);
				list2.Add(plannedMergeItem3);
				continue;
			}
			UiProgress(0.16 + 0.04 * ((double)num2 + 1.0) / (double)Math.Max(1, list.Count), "正在自动切开超长素材 " + (num2 + 1) + "/" + list.Count);
			List<string> list3 = CreateTemporaryMergeSegments(ffmpeg, plannedMergeItem3, options.MaxDurationSeconds, Path.Combine(tempRoot, "overlong_" + num2.ToString("000")), log);
			foreach (string item in list3)
			{
				VideoInfo videoInfo2 = Probe(ffmpeg, item);
				list2.Add(new PlannedMergeItem
				{
					Path = item,
					OriginalPath = plannedMergeItem3.OriginalPath,
					DurationSeconds = videoInfo2.DurationSeconds,
					VisualFeature = plannedMergeItem3.VisualFeature
				});
			}
		}
		UiProgress(0.2, "分析与自动分组已完成");
		return list2;
	}

	private static MergePlanningOptions CopyMergePlanningOptions(MergePlanningOptions source)
	{
		MergePlanningOptions mergePlanningOptions = new MergePlanningOptions();
		mergePlanningOptions.MaxItemsPerGroup = source.MaxItemsPerGroup;
		mergePlanningOptions.MaxRepeatsPerSource = source.MaxRepeatsPerSource;
		mergePlanningOptions.MaxOutputCount = source.MaxOutputCount;
		mergePlanningOptions.RandomCombinationStart = source.RandomCombinationStart;
		mergePlanningOptions.LimitDuration = source.LimitDuration;
		mergePlanningOptions.MaxDurationSeconds = source.MaxDurationSeconds;
		mergePlanningOptions.RandomClipRanges = source.RandomClipRanges;
		mergePlanningOptions.OverlongMode = source.OverlongMode;
		mergePlanningOptions.SortBySimilarity = source.SortBySimilarity;
		mergePlanningOptions.DetailedSimilarity = source.DetailedSimilarity;
		mergePlanningOptions.SimilarityThreshold = source.SimilarityThreshold;
		return mergePlanningOptions;
	}

	private static List<List<PlannedMergeItem>> BuildMergeGroups(List<PlannedMergeItem> items, MergePlanningOptions options, TransitionPlan transition)
	{
		List<List<PlannedMergeItem>> list = new List<List<PlannedMergeItem>>();
		List<PlannedMergeItem> list2 = (from x in items.GroupBy((PlannedMergeItem x) => x.Path, StringComparer.OrdinalIgnoreCase)
			select x.First()).ToList();
		if (list2.Count == 0)
		{
			return list;
		}
		if (options.MaxOutputCount > 0 && options.RandomCombinationStart && list2.Count > 1)
		{
			int count = new Random(Guid.NewGuid().GetHashCode()).Next(list2.Count);
			list2 = list2.Skip(count).Concat(list2.Take(count)).ToList();
		}
		int maximumUses = Math.Max(1, options.MaxRepeatsPerSource + 1);
		int num = ((options.MaxOutputCount > 0) ? options.MaxOutputCount : int.MaxValue);
		Dictionary<string, int> usage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		for (int num2 = 0; num2 < maximumUses; num2++)
		{
			if (list.Count >= num)
			{
				break;
			}
			List<PlannedMergeItem> source = OrderItemsForCombinationPass(list2, num2);
			List<PlannedMergeItem> list3 = source.Where((PlannedMergeItem x) => GetMergeItemUsage(usage, x) < maximumUses).ToList();
			if (list3.Count == 0)
			{
				break;
			}
			List<List<PlannedMergeItem>> list4 = BuildMergeGroupsSinglePass(list3, options, transition);
			int num3 = num - list.Count;
			bool flag = list4.Count > num3;
			if (flag)
			{
				list4 = list4.Take(num3).ToList();
			}
			else
			{
				RebalanceSingleItemTail(list4, options, transition);
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>(usage, StringComparer.OrdinalIgnoreCase);
			foreach (PlannedMergeItem item in list4.SelectMany((List<PlannedMergeItem> x) => x))
			{
				IncreaseMergeItemUsage(dictionary, item);
			}
			if (!flag)
			{
				TryFillSingleItemTailWithAllowedRepeat(list4, list2, dictionary, maximumUses, options, transition);
			}
			foreach (List<PlannedMergeItem> item2 in list4)
			{
				if (list.Count >= num)
				{
					break;
				}
				list.Add(item2);
				foreach (PlannedMergeItem item3 in item2)
				{
					IncreaseMergeItemUsage(usage, item3);
				}
			}
		}
		return list;
	}

	private static List<List<PlannedMergeItem>> BuildMergeGroupsSinglePass(List<PlannedMergeItem> items, MergePlanningOptions options, TransitionPlan transition)
	{
		List<List<PlannedMergeItem>> list = new List<List<PlannedMergeItem>>();
		List<PlannedMergeItem> list2 = new List<PlannedMergeItem>();
		foreach (PlannedMergeItem item in items)
		{
			List<PlannedMergeItem> list3 = new List<PlannedMergeItem>(list2);
			list3.Add(item);
			List<PlannedMergeItem> list4 = list3;
			if (list2.Count > 0 && list4.Count > Math.Max(1, options.MaxItemsPerGroup))
			{
				list.Add(list2);
				list2 = new List<PlannedMergeItem>();
			}
			list2.Add(item);
		}
		if (list2.Count > 0)
		{
			list.Add(list2);
		}
		return list;
	}

	private static List<PlannedMergeItem> OrderItemsForCombinationPass(List<PlannedMergeItem> items, int pass)
	{
		List<PlannedMergeItem> list = new List<PlannedMergeItem>(items);
		if (pass == 0 || list.Count < 2)
		{
			return list;
		}
		Random random = new Random(7919 * pass + 104729 * list.Count);
		for (int num = list.Count - 1; num > 0; num--)
		{
			int index = random.Next(num + 1);
			PlannedMergeItem value = list[num];
			list[num] = list[index];
			list[index] = value;
		}
		return list;
	}

	private static void RebalanceSingleItemTail(List<List<PlannedMergeItem>> groups, MergePlanningOptions options, TransitionPlan transition)
	{
		if (groups.Count < 2 || groups[groups.Count - 1].Count != 1)
		{
			return;
		}
		List<PlannedMergeItem> list = groups[groups.Count - 1];
		if (IsAllowedOverlongMergeItem(list[0], options))
		{
			return;
		}
		List<PlannedMergeItem> collection = groups[groups.Count - 2];
		List<PlannedMergeItem> list2 = new List<PlannedMergeItem>(collection);
		list2.Add(list[0]);
		if (MergeGroupFits(list2, options, transition))
		{
			groups[groups.Count - 2] = list2;
			groups.RemoveAt(groups.Count - 1);
			return;
		}
		for (int num = groups.Count - 2; num >= 0; num--)
		{
			List<PlannedMergeItem> list3 = groups[num];
			if (list3.Count >= 3)
			{
				for (int num2 = list3.Count - 1; num2 >= 0; num2--)
				{
					PlannedMergeItem item = list3[num2];
					List<PlannedMergeItem> list4 = new List<PlannedMergeItem>();
					list4.Add(item);
					list4.Add(list[0]);
					List<PlannedMergeItem> list5 = list4;
					if (MergeGroupFits(list5, options, transition))
					{
						list3.RemoveAt(num2);
						groups[groups.Count - 1] = list5;
						return;
					}
				}
			}
		}
	}

	private static void TryFillSingleItemTailWithAllowedRepeat(List<List<PlannedMergeItem>> groups, List<PlannedMergeItem> allItems, Dictionary<string, int> plannedUsage, int maximumUses, MergePlanningOptions options, TransitionPlan transition)
	{
		if (options.MaxRepeatsPerSource <= 0 || groups.Count == 0)
		{
			return;
		}
		List<PlannedMergeItem> tail = groups[groups.Count - 1];
		if (tail.Count == 1 && !IsAllowedOverlongMergeItem(tail[0], options))
		{
			PlannedMergeItem plannedMergeItem = (from x in allItems
				where !string.Equals(x.OriginalPath, tail[0].OriginalPath, StringComparison.OrdinalIgnoreCase)
				where GetMergeItemUsage(plannedUsage, x) < maximumUses
				where MergeGroupFits(new List<PlannedMergeItem>
				{
					tail[0],
					x
				}, options, transition)
				orderby OrphanCompanionScore(tail[0], x) descending
				select x).FirstOrDefault();
			if (plannedMergeItem != null)
			{
				tail.Add(plannedMergeItem);
				IncreaseMergeItemUsage(plannedUsage, plannedMergeItem);
			}
		}
	}

	private static double OrphanCompanionScore(PlannedMergeItem first, PlannedMergeItem second)
	{
		if (first.VisualFeature != null && second.VisualFeature != null)
		{
			return VisualSimilarity(first.VisualFeature, second.VisualFeature);
		}
		return 1.0 / (1.0 + Math.Abs(first.DurationSeconds - second.DurationSeconds));
	}

	private static bool MergeGroupFits(List<PlannedMergeItem> group, MergePlanningOptions options, TransitionPlan transition)
	{
		if (group.Count == 0 || group.Count > Math.Max(1, options.MaxItemsPerGroup))
		{
			return false;
		}
		if (options.LimitDuration)
		{
			return true;
		}
		return true;
	}

	private static bool IsAllowedOverlongMergeItem(PlannedMergeItem item, MergePlanningOptions options)
	{
		return false;
	}

	private static int GetMergeItemUsage(Dictionary<string, int> usage, PlannedMergeItem item)
	{
		if (!usage.TryGetValue(item.Path, out var value))
		{
			return 0;
		}
		return value;
	}

	private static void IncreaseMergeItemUsage(Dictionary<string, int> usage, PlannedMergeItem item)
	{
		usage[item.Path] = GetMergeItemUsage(usage, item) + 1;
	}

	private static double EstimateMergedDuration(List<PlannedMergeItem> items, TransitionPlan transition)
	{
		if (items.Count == 0)
		{
			return 0.0;
		}
		double num = items.Sum((PlannedMergeItem x) => Math.Max(0.1, x.DurationSeconds));
		if (!transition.Enabled || items.Count < 2)
		{
			return num;
		}
		double num2 = transition.DurationSeconds;
		double num3 = items.Min((PlannedMergeItem x) => Math.Max(0.1, x.DurationSeconds));
		if (num2 >= num3)
		{
			num2 = Math.Max(0.08, num3 * 0.4);
		}
		return Math.Max(0.1, num - num2 * (double)(items.Count - 1));
	}

	private static string OverlongModeName(int mode)
	{
		return mode switch
		{
			1 => "跳过", 
			2 => "允许单独超时", 
			_ => "自动平均切开", 
		};
	}

	private double[] ExtractVideoVisualFeature(string ffmpeg, VideoInfo info, bool detailed, string tempRoot, int sourceIndex)
	{
		double[] array = ((!detailed) ? new double[1] { 0.5 } : new double[3] { 0.18, 0.5, 0.82 });
		List<double[]> list = new List<double[]>();
		string text = Path.Combine(tempRoot, "frames_" + sourceIndex.ToString("000"));
		Directory.CreateDirectory(text);
		for (int i = 0; i < array.Length; i++)
		{
			if (_cancelRequested)
			{
				throw new OperationCanceledException();
			}
			double value = Math.Max(0.0, Math.Min(Math.Max(0.0, info.DurationSeconds - 0.05), info.DurationSeconds * array[i]));
			string text2 = Path.Combine(text, "sample_" + i + ".png");
			string value2 = "scale=64:64:force_original_aspect_ratio=decrease,pad=64:64:(ow-iw)/2:(oh-ih)/2:black";
			string arguments = "-hide_banner -loglevel error -y -ss " + FfmpegNumber(value) + " -i " + QuoteArg(info.Path) + " -frames:v 1 -an -vf " + QuoteArg(value2) + " -progress pipe:1 -nostats " + QuoteArg(text2);
			if (RunFfmpeg(ffmpeg, arguments, 1.0, delegate
			{
			}, out var _) == 0 && File.Exists(text2))
			{
				try
				{
					list.Add(DescribeFrame(text2));
				}
				catch
				{
					list.Add(null);
				}
			}
			else
			{
				list.Add(null);
			}
		}
		double[] array2 = list.FirstOrDefault((double[] x) => x != null);
		if (array2 == null)
		{
			return null;
		}
		int num = array2.Length;
		double[] array3 = new double[num * list.Count];
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			double[] sourceArray = list[num2] ?? array2;
			Array.Copy(sourceArray, 0, array3, num2 * num, num);
		}
		return array3;
	}

	private static double[] DescribeFrame(string imagePath)
	{
		double[,,] array = new double[4, 4, 3];
		int[,] array2 = new int[4, 4];
		double[,] array3 = new double[4, 4];
		int[,] array4 = new int[4, 4];
		double[,] array5 = new double[3, 8];
		using Bitmap bitmap = new Bitmap(imagePath);
		for (int i = 0; i < bitmap.Height; i++)
		{
			for (int j = 0; j < bitmap.Width; j++)
			{
				Color pixel = bitmap.GetPixel(j, i);
				int num = Math.Min(3, j * 4 / bitmap.Width);
				int num2 = Math.Min(3, i * 4 / bitmap.Height);
				array[num2, num, 0] += (double)(int)pixel.R / 255.0;
				array[num2, num, 1] += (double)(int)pixel.G / 255.0;
				array[num2, num, 2] += (double)(int)pixel.B / 255.0;
				array2[num2, num]++;
				array5[0, Math.Min(7, pixel.R * 8 / 256)]++;
				array5[1, Math.Min(7, pixel.G * 8 / 256)]++;
				array5[2, Math.Min(7, pixel.B * 8 / 256)]++;
				if (j + 1 < bitmap.Width && i + 1 < bitmap.Height)
				{
					Color pixel2 = bitmap.GetPixel(j + 1, i);
					Color pixel3 = bitmap.GetPixel(j, i + 1);
					double num3 = (0.299 * (double)(int)pixel.R + 0.587 * (double)(int)pixel.G + 0.114 * (double)(int)pixel.B) / 255.0;
					double num4 = (0.299 * (double)(int)pixel2.R + 0.587 * (double)(int)pixel2.G + 0.114 * (double)(int)pixel2.B) / 255.0;
					double num5 = (0.299 * (double)(int)pixel3.R + 0.587 * (double)(int)pixel3.G + 0.114 * (double)(int)pixel3.B) / 255.0;
					if (Math.Abs(num3 - num4) + Math.Abs(num3 - num5) > 0.18)
					{
						array3[num2, num]++;
					}
					array4[num2, num]++;
				}
			}
		}
		List<double> list = new List<double>();
		for (int k = 0; k < 4; k++)
		{
			for (int l = 0; l < 4; l++)
			{
				for (int m = 0; m < 3; m++)
				{
					list.Add(array[k, l, m] / (double)Math.Max(1, array2[k, l]));
				}
			}
		}
		double num6 = Math.Max(1, bitmap.Width * bitmap.Height);
		for (int n = 0; n < 3; n++)
		{
			for (int num7 = 0; num7 < 8; num7++)
			{
				list.Add(array5[n, num7] / num6);
			}
		}
		for (int num8 = 0; num8 < 4; num8++)
		{
			for (int num9 = 0; num9 < 4; num9++)
			{
				list.Add(array3[num8, num9] / (double)Math.Max(1, array4[num8, num9]));
			}
		}
		return list.ToArray();
	}

	private static List<PlannedMergeItem> OrderItemsBySimilarity(List<PlannedMergeItem> items, double threshold)
	{
		List<PlannedMergeItem> list = new List<PlannedMergeItem>(items.Where((PlannedMergeItem x) => x.VisualFeature != null));
		List<PlannedMergeItem> list2 = new List<PlannedMergeItem>();
		while (list.Count > 0)
		{
			PlannedMergeItem plannedMergeItem = list[0];
			list.RemoveAt(0);
			list2.Add(plannedMergeItem);
			while (list.Count > 0)
			{
				PlannedMergeItem plannedMergeItem2 = null;
				double num = double.MinValue;
				foreach (PlannedMergeItem item in list)
				{
					double num2 = VisualSimilarity(plannedMergeItem.VisualFeature, item.VisualFeature);
					if (num2 > num)
					{
						num = num2;
						plannedMergeItem2 = item;
					}
				}
				if (plannedMergeItem2 == null || num < threshold)
				{
					break;
				}
				list.Remove(plannedMergeItem2);
				list2.Add(plannedMergeItem2);
				plannedMergeItem = plannedMergeItem2;
			}
		}
		list2.AddRange(items.Where((PlannedMergeItem x) => x.VisualFeature == null));
		return list2;
	}

	private static double VisualSimilarity(double[] first, double[] second)
	{
		if (first == null || second == null || first.Length == 0 || first.Length != second.Length)
		{
			return 0.0;
		}
		double num = 0.0;
		for (int i = 0; i < first.Length; i++)
		{
			double num2 = first[i] - second[i];
			num += num2 * num2;
		}
		return Math.Max(0.0, Math.Min(1.0, 1.0 - Math.Sqrt(num / (double)first.Length)));
	}

	private List<string> CreateTemporaryMergeSegments(string ffmpeg, PlannedMergeItem item, double maxDuration, string segmentDirectory, StreamWriter log)
	{
		Directory.CreateDirectory(segmentDirectory);
		double num = Math.Max(0.5, maxDuration - 0.2);
		int num2 = Math.Max(2, (int)Math.Ceiling(item.DurationSeconds / num));
		double num3 = item.DurationSeconds / (double)num2;
		List<double> list = new List<double>();
		for (int i = 1; i < num2; i++)
		{
			list.Add(num3 * (double)i);
		}
		string value = Path.Combine(segmentDirectory, "part_%03d.mp4");
		string value2 = string.Join(",", list.Select(FfmpegNumber).ToArray());
		string value3 = "scale=trunc(iw/2)*2:trunc(ih/2)*2,setsar=1,format=yuv420p";
		string arguments = "-hide_banner -y -i " + QuoteArg(item.Path) + " -map 0:v:0 -map 0:a:0? -vf " + QuoteArg(value3) + " -c:v libx264 -preset fast -crf 17 -metadata:s:v:0 rotate=0 -c:a aac -b:a 192k -force_key_frames " + QuoteArg(value2) + " -f segment -segment_times " + QuoteArg(value2) + " -reset_timestamps 1 -segment_start_number 1 -progress pipe:1 -nostats " + QuoteArg(value);
		int num4 = RunFfmpeg(ffmpeg, arguments, item.DurationSeconds, delegate
		{
		}, out var errorText);
		if (_cancelRequested)
		{
			throw new OperationCanceledException();
		}
		List<string> list2 = Directory.GetFiles(segmentDirectory, "part_*.mp4", SearchOption.TopDirectoryOnly).OrderBy((string x) => x, StringComparer.OrdinalIgnoreCase).ToList();
		if (num4 != 0 || list2.Count != num2)
		{
			throw new InvalidOperationException("自动切开超长素材失败：" + Path.GetFileName(item.OriginalPath) + "。" + LastUsefulLines(errorText, 8));
		}
		log.WriteLine("超长素材已平均切成 " + num2 + " 段，每段约 " + FfmpegNumber(num3) + " 秒：" + item.OriginalPath);
		return list2;
	}

	private void UiApplyMergeOrder(List<string> orderedFiles)
	{
		if (!base.IsDisposed && base.IsHandleCreated)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				_videos.Clear();
				_videos.AddRange(orderedFiles);
				UpdateListView(null);
			});
		}
	}

	private MergeResult MergeGroup(string ffmpeg, List<string> files, string outputPath, bool allowFallback, Action<double, string> progress, StreamWriter log)
	{
		return MergeGroupAdvanced(ffmpeg, files, outputPath, allowFallback, TransitionSpec.None(), new WatermarkSettings
		{
			Enabled = false
		}, progress, log);
	}

	private MergeResult MergeGroupAdvanced(string ffmpeg, List<string> files, string outputPath, bool allowFallback, TransitionSpec transition, WatermarkSettings watermark, Action<double, string> progress, StreamWriter log)
	{
		return MergeGroupWithPlan(ffmpeg, files, outputPath, allowFallback, TransitionPlan.FromSingle(transition), watermark, progress, log);
	}

	private MergeResult MergeGroupWithPlan(string ffmpeg, List<string> files, string outputPath, bool allowFallback, TransitionPlan transition, WatermarkSettings watermark, Action<double, string> progress, StreamWriter log)
	{
		return MergeGroupWithProfile(ffmpeg, files, outputPath, allowFallback, transition, WatermarkProfile.FromSingle(watermark), progress, log);
	}

	private MergeResult MergeGroupWithProfile(string ffmpeg, List<string> files, string outputPath, bool allowFallback, TransitionPlan transition, WatermarkProfile watermark, Action<double, string> progress, StreamWriter log)
	{
		return MergeGroupWithProfileCapped(ffmpeg, files, outputPath, allowFallback, transition, watermark, 0.0, randomClipRanges: false, progress, log);
	}

	private MergeResult MergeGroupWithProfileCapped(string ffmpeg, List<string> files, string outputPath, bool allowFallback, TransitionPlan transition, WatermarkProfile watermark, double outputLimitSeconds, bool randomClipRanges, Action<double, string> progress, StreamWriter log)
	{
		List<VideoInfo> list = new List<VideoInfo>();
		foreach (string file in files)
		{
			if (_cancelRequested)
			{
				MergeResult mergeResult = new MergeResult();
				mergeResult.Cancelled = true;
				return mergeResult;
			}
			VideoInfo videoInfo = Probe(ffmpeg, file);
			if (_cancelRequested)
			{
				MergeResult mergeResult2 = new MergeResult();
				mergeResult2.Cancelled = true;
				return mergeResult2;
			}
			if (!videoInfo.HasVideo)
			{
				MergeResult mergeResult3 = new MergeResult();
				mergeResult3.Error = "无法识别视频画面：" + Path.GetFileName(file);
				return mergeResult3;
			}
			VideoAdjustmentSettings videoAdjustment = GetVideoAdjustment(_activeMergeVideoAdjustments, file);
			videoInfo.DurationSeconds /= Math.Max(0.1, videoAdjustment.SpeedRatio);
			list.Add(videoInfo);
		}
		bool exactDurationOutput = outputLimitSeconds > 0.0;
		TransitionPlan renderTransition = transition;
		List<double> exactPartDurations = null;
		if (exactDurationOutput)
		{
			Dictionary<string, double> dictionary = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
			foreach (VideoInfo item in list)
			{
				if (!dictionary.TryGetValue(item.Path, out var value))
				{
					VideoAdjustmentSettings videoAdjustment2 = GetVideoAdjustment(_activeMergeVideoAdjustments, item.Path);
					value = MeasureVideoStreamDuration(ffmpeg, item.Path, item.DurationSeconds * Math.Max(0.1, videoAdjustment2.SpeedRatio)) / Math.Max(0.1, videoAdjustment2.SpeedRatio);
					dictionary[item.Path] = value;
				}
				if (value > 0.05)
				{
					if (item.DurationSeconds - value > 0.15)
					{
						log.WriteLine("  检测到音轨长于画面：" + Path.GetFileName(item.Path) + "，按实际画面 " + FfmpegNumber(value) + " 秒参与合并。");
					}
					item.DurationSeconds = Math.Min(item.DurationSeconds, value);
				}
			}
			double num = 0.0;
			if (transition.Enabled && list.Count > 1)
			{
				num = Math.Max(0.08, transition.DurationSeconds);
				double num2 = outputLimitSeconds / (double)list.Count;
				if (num >= num2)
				{
					num = Math.Max(0.08, num2 * 0.35);
				}
				double num3 = list.Min((VideoInfo x) => ExactUsableVideoDuration(x));
				if (num >= num3)
				{
					num = Math.Max(0.04, num3 * 0.35);
				}
				renderTransition = CopyTransitionPlanWithDuration(transition, num);
			}
			int count = list.Count;
			List<string> list2 = new List<string>(files);
			List<VideoInfo> list3 = new List<VideoInfo>(list);
			List<string> list4 = new List<string>();
			int num4 = 0;
			double num5 = outputLimitSeconds + 0.02;
			while (ExactAvailableOutputDuration(list3, num) < num5)
			{
				if (list3.Count >= 2000)
				{
					MergeResult mergeResult4 = new MergeResult();
					mergeResult4.Error = "目标时长远大于本组素材时长，自动循环已达到 2000 段安全上限。请增加素材或缩短目标时长。";
					return mergeResult4;
				}
				int index = SelectExactDurationFillerIndex(files, list2, num4);
				list2.Add(files[index]);
				list3.Add(list[index]);
				list4.Add(files[index]);
				num4++;
			}
			files = list2;
			list = list3;
			double requiredContentDuration = outputLimitSeconds + num * (double)Math.Max(0, list.Count - 1) + 0.02;
			exactPartDurations = AllocateExactPartDurations(list, requiredContentDuration);
			if (exactPartDurations == null)
			{
				MergeResult mergeResult5 = new MergeResult();
				mergeResult5.Error = "本组素材无法在不定格尾帧的情况下补足目标时长。请增加素材或缩短目标时长。";
				return mergeResult5;
			}
			if (num4 > 0)
			{
				log.WriteLine("  原素材画面不足 " + FfmpegNumber(outputLimitSeconds) + " 秒，已" + ((count > 1) ? "从本组第 2 个素材开始" : "循环本组唯一素材") + "自动补入 " + num4 + " 段；" + ((count > 1) ? "未使用本组第一段作为自动补段，" : "") + "不会定格尾帧。");
				log.WriteLine("  自动补段顺序：" + string.Join("、", list4.Select(Path.GetFileName).ToArray()));
			}
			log.WriteLine("  统一时长：原组 " + count + " 段，实际使用 " + list.Count + " 段；每段" + (randomClipRanges ? "随机截取" : "取开头") + "约 " + FfmpegNumber(exactPartDurations.Min()) + "–" + FfmpegNumber(exactPartDurations.Max()) + " 秒，最终输出 " + FfmpegNumber(outputLimitSeconds) + " 秒。" + ((num > 0.0) ? (" 已计入每次 " + FfmpegNumber(num) + " 秒转场重叠。") : ""));
		}
		double totalDuration = list.Sum((VideoInfo x) => Math.Max(0.1, x.DurationSeconds));
		string text = Path.Combine(Path.GetTempPath(), "VideoBatchMerger_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		try
		{
			string text2 = Path.Combine(text, "concat.txt");
			VideoInfo firstInfo = list[0];
			bool flag = list.All((VideoInfo x) => x.Width == firstInfo.Width && x.Height == firstInfo.Height && x.HasAudio == firstInfo.HasAudio);
			bool flag2 = list.Any(delegate(VideoInfo x)
			{
				VideoAdjustmentSettings videoAdjustment4 = GetVideoAdjustment(_activeMergeVideoAdjustments, x.Path);
				return !videoAdjustment4.IsIdentity || ShouldCenterCropPortrait(x, videoAdjustment4);
			}) || (_activeMergeOutputFrame != null && _activeMergeOutputFrame.ForceAspect);
			bool flag3 = renderTransition.Enabled || watermark.Enabled || exactDurationOutput || flag2;
			if (flag && !flag3)
			{
				WriteConcatList(text2, files);
				progress(0.0, "尝试无损快速合并");
				string arguments = "-hide_banner -y -f concat -safe 0 -i " + QuoteArg(text2) + " -map 0:v:0? -map 0:a:0? -c copy" + OutputDurationArgument(outputLimitSeconds) + " -movflags +faststart -progress pipe:1 -nostats " + QuoteArg(outputPath);
				int num6 = RunFfmpeg(ffmpeg, arguments, totalDuration, delegate(double p)
				{
					progress(p, "无损快速合并");
				}, out var errorText);
				if (_cancelRequested)
				{
					TryDelete(outputPath);
					MergeResult mergeResult6 = new MergeResult();
					mergeResult6.Cancelled = true;
					return mergeResult6;
				}
				if (num6 == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
				{
					progress(1.0, "无损合并完成");
					MergeResult mergeResult7 = new MergeResult();
					mergeResult7.Success = true;
					mergeResult7.RenderedDurationSeconds = totalDuration;
					return mergeResult7;
				}
				TryDelete(outputPath);
				log.WriteLine("  快速合并未成功：" + LastUsefulLines(errorText, 6));
			}
			else
			{
				if (!flag)
				{
					log.WriteLine("  检测到分辨率或音轨结构不一致，将使用兼容转换。");
				}
				if (flag3)
				{
					log.WriteLine("  已启用素材调整、统一时长、转场或水印，将使用兼容转换和重新编码。");
				}
			}
			if (!allowFallback && !flag3)
			{
				MergeResult mergeResult8 = new MergeResult();
				mergeResult8.Error = "视频参数不一致，快速合并失败。可勾选“自动兼容转换”后重试。";
				return mergeResult8;
			}
			int num7 = MakeEven((list[0].Width > 0) ? list[0].Width : 1920);
			int num8 = MakeEven((list[0].Height > 0) ? list[0].Height : 1080);
			VideoInfo videoInfo2 = list.FirstOrDefault((VideoInfo x) => ShouldCenterCropPortrait(x, GetVideoAdjustment(_activeMergeVideoAdjustments, x.Path)));
			bool flag4 = _activeMergeOutputFrame != null && _activeMergeOutputFrame.ForceAspect;
			bool flag5 = !flag4 && videoInfo2 != null;
			if (flag4)
			{
				Size outputCanvasSize = GetOutputCanvasSize(list[0], GetVideoAdjustment(_activeMergeVideoAdjustments, list[0].Path), _activeMergeOutputFrame);
				num7 = outputCanvasSize.Width;
				num8 = outputCanvasSize.Height;
				log.WriteLine("  已统一输出画幅：" + DescribeOutputFrame(_activeMergeOutputFrame) + "；所有素材等比例铺满，不拉伸、不留黑边。");
			}
			else if (flag5)
			{
				Size adjustedCanvasSize = GetAdjustedCanvasSize(videoInfo2, GetVideoAdjustment(_activeMergeVideoAdjustments, videoInfo2.Path));
				num7 = adjustedCanvasSize.Width;
				num8 = adjustedCanvasSize.Height;
				log.WriteLine("  已启用横屏裁中间：本组统一输出为 9:16 竖屏，保留中央区域。");
			}
			List<string> list5 = new List<string>();
			List<VideoInfo> list6 = new List<VideoInfo>();
			double num9 = 0.0;
			double normalizationWeight = (flag3 ? 0.6 : 0.9);
			Random random = new Random(Guid.NewGuid().GetHashCode());
			for (int num10 = 0; num10 < list.Count; num10++)
			{
				if (_cancelRequested)
				{
					MergeResult mergeResult9 = new MergeResult();
					mergeResult9.Cancelled = true;
					return mergeResult9;
				}
				VideoInfo videoInfo3 = list[num10];
				VideoAdjustmentSettings videoAdjustment3 = GetVideoAdjustment(_activeMergeVideoAdjustments, videoInfo3.Path);
				double num11 = Math.Max(0.1, videoAdjustment3.SpeedRatio);
				string text3 = Path.Combine(text, "part_" + num10.ToString("000") + ".mp4");
				string text4 = BuildAdjustedVideoFilterWithCanvasCrop(num7, num8, videoAdjustment3, normalizeFrameRate: true, flag4 || flag5 || _activeMergeFillCanvas, flag4 ? ResolveCropPositionPercent(_activeMergeOutputFrame) : 50);
				double targetPartDuration = (exactDurationOutput ? exactPartDurations[num10] : Math.Max(0.1, videoInfo3.DurationSeconds));
				double num12 = (exactDurationOutput ? SelectClipStartSeconds(videoInfo3.DurationSeconds, targetPartDuration, randomClipRanges, random) : 0.0);
				if (exactDurationOutput)
				{
					text4 = text4 + ",trim=duration=" + FfmpegNumber(targetPartDuration) + ",setpts=PTS-STARTPTS";
					if (randomClipRanges)
					{
						log.WriteLine("    素材 " + (num10 + 1) + " 随机起点：" + FfmpegNumber(num12) + " 秒（" + Path.GetFileName(videoInfo3.Path) + "）");
					}
				}
				string text5 = ((exactDurationOutput && num12 > 0.001) ? (" -ss " + FfmpegNumber(num12 * num11)) : "");
				string arguments2;
				if (videoInfo3.HasAudio)
				{
					string value2 = (exactDurationOutput ? (BuildAdjustedAudioFilter(videoAdjustment3) + ",atrim=duration=" + FfmpegNumber(targetPartDuration) + ",asetpts=PTS-STARTPTS,apad=whole_dur=" + FfmpegNumber(targetPartDuration) + ",atrim=duration=" + FfmpegNumber(targetPartDuration)) : (BuildAdjustedAudioFilter(videoAdjustment3) + ",apad"));
					arguments2 = "-hide_banner -y" + text5 + " -i " + QuoteArg(videoInfo3.Path) + " -map 0:v:0 -map 0:a:0 -vf " + QuoteArg(text4) + " -af " + QuoteArg(value2) + " -c:v libx264 -preset fast -crf 17 -metadata:s:v:0 rotate=0 -c:a aac -b:a 192k -ar 48000 -ac 2 -shortest" + (exactDurationOutput ? (" -t " + FfmpegNumber(targetPartDuration)) : "") + " -movflags +faststart -progress pipe:1 -nostats " + QuoteArg(text3);
				}
				else
				{
					arguments2 = "-hide_banner -y" + text5 + " -i " + QuoteArg(videoInfo3.Path) + " -f lavfi -i " + QuoteArg("anullsrc=channel_layout=stereo:sample_rate=48000") + " -map 0:v:0 -map 1:a:0 -vf " + QuoteArg(text4) + " -c:v libx264 -preset fast -crf 17 -metadata:s:v:0 rotate=0 -c:a aac -b:a 192k -ar 48000 -ac 2 -shortest" + (exactDurationOutput ? (" -t " + FfmpegNumber(targetPartDuration)) : "") + " -movflags +faststart -progress pipe:1 -nostats " + QuoteArg(text3);
				}
				double before = num9;
				string normalizationStage = ((num11 < 0.45) ? ("兼容转换（极慢速快速补帧） " + (num10 + 1) + "/" + list.Count) : ("兼容转换 " + (num10 + 1) + "/" + list.Count));
				progress(normalizationWeight * before / Math.Max(0.1, exactDurationOutput ? exactPartDurations.Sum() : totalDuration), normalizationStage);
				int num13 = RunFfmpeg(ffmpeg, arguments2, targetPartDuration, delegate(double p)
				{
					double num16 = before + p * targetPartDuration;
					double val = (exactDurationOutput ? exactPartDurations.Sum() : totalDuration);
					progress(normalizationWeight * num16 / Math.Max(0.1, val), normalizationStage);
				}, out var errorText2);
				if (_cancelRequested)
				{
					MergeResult mergeResult10 = new MergeResult();
					mergeResult10.Cancelled = true;
					return mergeResult10;
				}
				if (num13 != 0 || !File.Exists(text3))
				{
					log.WriteLine("  兼容转换失败：" + LastUsefulLines(errorText2, 10));
					MergeResult mergeResult11 = new MergeResult();
					mergeResult11.Error = "兼容转换失败：" + Path.GetFileName(videoInfo3.Path);
					return mergeResult11;
				}
				VideoInfo videoInfo4 = Probe(ffmpeg, text3);
				if (!videoInfo4.HasVideo || videoInfo4.DurationSeconds <= 0.0)
				{
					MergeResult mergeResult12 = new MergeResult();
					mergeResult12.Error = "兼容转换后的片段无法重新读取：" + Path.GetFileName(videoInfo3.Path);
					return mergeResult12;
				}
				double num14 = (exactDurationOutput ? targetPartDuration : Math.Max(0.1, videoInfo4.DurationSeconds));
				list5.Add(text3);
				list6.Add(new VideoInfo
				{
					Path = text3,
					DurationSeconds = num14,
					Width = num7,
					Height = num8,
					CodedWidth = num7,
					CodedHeight = num8,
					RotationDegrees = 0,
					HasVideo = true,
					HasAudio = true
				});
				num9 += num14;
			}
			double renderedDuration = totalDuration;
			string arguments3;
			if (flag3)
			{
				List<PreparedWatermarkLayer> watermarkLayers = new List<PreparedWatermarkLayer>();
				if (watermark.Enabled)
				{
					try
					{
						watermarkLayers = PrepareWatermarkLayers(watermark, num7, num8, text, exactDurationOutput ? outputLimitSeconds : totalDuration);
					}
					catch (Exception ex)
					{
						log.WriteLine("  准备水印失败：" + ex.Message);
						MergeResult mergeResult13 = new MergeResult();
						mergeResult13.Error = "无法生成水印，请检查水印内容或图片。";
						return mergeResult13;
					}
				}
				arguments3 = BuildAdvancedMergeArguments(list5, list6, outputPath, renderTransition, watermark, watermarkLayers, outputLimitSeconds, out renderedDuration, log);
			}
			else
			{
				WriteConcatList(text2, list5);
				arguments3 = "-hide_banner -y -f concat -safe 0 -i " + QuoteArg(text2) + " -map 0:v:0 -map 0:a:0 -c copy" + OutputDurationArgument(outputLimitSeconds) + " -movflags +faststart -progress pipe:1 -nostats " + QuoteArg(outputPath);
			}
			int num15 = RunFfmpeg(ffmpeg, arguments3, renderedDuration, delegate(double p)
			{
				progress(normalizationWeight + p * (1.0 - normalizationWeight), renderTransition.Enabled ? "正在渲染转场" : (watermark.Enabled ? "正在添加水印" : "正在生成输出文件"));
			}, out var errorText3);
			if (_cancelRequested)
			{
				TryDelete(outputPath);
				MergeResult mergeResult14 = new MergeResult();
				mergeResult14.Cancelled = true;
				return mergeResult14;
			}
			if (num15 != 0 || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
			{
				TryDelete(outputPath);
				log.WriteLine("  生成输出失败：" + LastUsefulLines(errorText3, 10));
				MergeResult mergeResult15 = new MergeResult();
				mergeResult15.Error = "生成最终输出文件失败。";
				return mergeResult15;
			}
			progress(1.0, flag3 ? "效果合并完成" : "兼容合并完成");
			MergeResult mergeResult16 = new MergeResult();
			mergeResult16.Success = true;
			mergeResult16.RenderedDurationSeconds = renderedDuration;
			return mergeResult16;
		}
		finally
		{
			TryDeleteDirectory(text);
		}
	}

	private static double SelectClipStartSeconds(double sourceDuration, double requestedDuration, bool randomClipRange, Random random)
	{
		double num = Math.Max(0.0, sourceDuration - requestedDuration);
		if (!randomClipRange || num <= 0.01 || random == null)
		{
			return 0.0;
		}
		return Math.Max(0.0, Math.Min(num, random.NextDouble() * num));
	}

	private static double ExactUsableVideoDuration(VideoInfo info)
	{
		return Math.Max(0.05, info.DurationSeconds - 0.06);
	}

	private static double ExactAvailableOutputDuration(List<VideoInfo> infos, double transitionDuration)
	{
		if (infos == null || infos.Count == 0)
		{
			return 0.0;
		}
		return Math.Max(0.0, infos.Sum((VideoInfo x) => ExactUsableVideoDuration(x)) - transitionDuration * (double)Math.Max(0, infos.Count - 1));
	}

	private static int SelectExactDurationFillerIndex(List<string> originalFiles, List<string> expandedFiles, int addedFillers)
	{
		if (originalFiles == null || originalFiles.Count <= 1)
		{
			return 0;
		}
		List<int> list = (from index in Enumerable.Range(1, originalFiles.Count - 1)
			where !string.Equals(originalFiles[index], originalFiles[0], StringComparison.OrdinalIgnoreCase)
			select index).ToList();
		if (list.Count == 0)
		{
			list = Enumerable.Range(1, originalFiles.Count - 1).ToList();
		}
		int num = Math.Abs(addedFillers) % list.Count;
		string b = ((expandedFiles.Count > 0) ? expandedFiles[expandedFiles.Count - 1] : null);
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			int num3 = list[(num + num2) % list.Count];
			if (!string.Equals(originalFiles[num3], b, StringComparison.OrdinalIgnoreCase))
			{
				return num3;
			}
		}
		return list[num];
	}

	private static List<double> AllocateExactPartDurations(List<VideoInfo> infos, double requiredContentDuration)
	{
		List<double> capacities = infos.Select(ExactUsableVideoDuration).ToList();
		if (capacities.Sum() + 0.001 < requiredContentDuration)
		{
			return null;
		}
		List<double> list = Enumerable.Repeat(0.0, capacities.Count).ToList();
		List<int> list2 = Enumerable.Range(0, capacities.Count).ToList();
		double num = requiredContentDuration;
		while (list2.Count > 0)
		{
			double share = num / (double)list2.Count;
			List<int> list3 = list2.Where((int index) => capacities[index] < share - 0.0001).ToList();
			if (list3.Count == 0)
			{
				foreach (int item in list2)
				{
					list[item] = share;
				}
				num = 0.0;
				break;
			}
			foreach (int item2 in list3)
			{
				list[item2] = capacities[item2];
				num -= capacities[item2];
				list2.Remove(item2);
			}
		}
		if (!(num <= 0.01))
		{
			return null;
		}
		return list;
	}

	private string BuildAdvancedMergeArguments(List<string> normalized, List<VideoInfo> infos, string outputPath, TransitionPlan transition, WatermarkProfile watermark, List<PreparedWatermarkLayer> watermarkLayers, double outputLimitSeconds, out double renderedDuration, StreamWriter log)
	{
		StringBuilder stringBuilder = new StringBuilder("-hide_banner -y");
		foreach (string item in normalized)
		{
			stringBuilder.Append(" -i ").Append(QuoteArg(item));
		}
		if (watermark.Enabled)
		{
			AppendWatermarkInputs(stringBuilder, watermarkLayers);
		}
		List<string> list = new List<string>();
		int count = normalized.Count;
		double num = infos.Sum((VideoInfo x) => Math.Max(0.1, x.DurationSeconds));
		double num2 = transition.DurationSeconds;
		if (transition.Enabled && count > 1)
		{
			double num3 = infos.Min((VideoInfo x) => Math.Max(0.1, x.DurationSeconds));
			if (num2 >= num3)
			{
				num2 = Math.Max(0.08, num3 * 0.4);
				log.WriteLine("  部分视频短于所选转场时间，实际转场自动缩短为 " + num2.ToString("0.###", CultureInfo.InvariantCulture) + " 秒。");
			}
		}
		string currentVideo;
		string text7;
		if (transition.Enabled && count > 1)
		{
			string[] array = new string[count];
			string[] array2 = new string[count];
			for (int num4 = 0; num4 < count; num4++)
			{
				string text = FfmpegNumber(Math.Max(0.1, infos[num4].DurationSeconds));
				array[num4] = "tv" + num4;
				array2[num4] = "ta" + num4;
				list.Add("[" + num4 + ":v]trim=duration=" + text + ",setpts=PTS-STARTPTS,fps=30,settb=AVTB,format=yuv420p[" + array[num4] + "]");
				list.Add("[" + num4 + ":a]atrim=duration=" + text + ",asetpts=PTS-STARTPTS,aresample=48000:async=1:first_pts=0,apad=whole_dur=" + text + ",atrim=duration=" + text + "[" + array2[num4] + "]");
			}
			string text2 = array[0];
			string text3 = array2[0];
			double num5 = Math.Max(0.1, infos[0].DurationSeconds);
			Random random = new Random(Guid.NewGuid().GetHashCode());
			TransitionSpec transitionSpec = null;
			if (transition.LockSingleEffectPerOutput && transition.Effects.Count > 0)
			{
				transitionSpec = (transition.RandomOrder ? transition.Effects[random.Next(transition.Effects.Count)] : transition.Effects[0]);
			}
			for (int num6 = 1; num6 < count; num6++)
			{
				double value = Math.Max(0.0, num5 - num2 * (double)num6);
				string text4 = "vx" + num6;
				string text5 = "ax" + num6;
				TransitionSpec transitionSpec2 = transitionSpec ?? (transition.RandomOrder ? transition.Effects[random.Next(transition.Effects.Count)] : transition.Effects[(num6 - 1) % transition.Effects.Count]);
				log.WriteLine("  衔接 " + num6 + " 使用转场：" + transitionSpec2.DisplayName);
				string text6 = "[" + text2 + "][" + array[num6] + "]xfade=transition=" + transitionSpec2.FfmpegName + ":duration=" + FfmpegNumber(num2) + ":offset=" + FfmpegNumber(value);
				if (transitionSpec2.FfmpegName == "custom" && !string.IsNullOrWhiteSpace(transitionSpec2.CustomExpression))
				{
					text6 = text6 + ":expr='" + transitionSpec2.CustomExpression + "'";
				}
				list.Add(text6 + "[" + text4 + "]");
				list.Add("[" + text3 + "][" + array2[num6] + "]acrossfade=d=" + FfmpegNumber(num2) + ":c1=qsin:c2=qsin[" + text5 + "]");
				text2 = text4;
				text3 = text5;
				num5 += Math.Max(0.1, infos[num6].DurationSeconds);
			}
			currentVideo = text2;
			text7 = text3;
			renderedDuration = Math.Max(0.1, num - num2 * (double)(count - 1));
		}
		else if (count > 1)
		{
			StringBuilder stringBuilder2 = new StringBuilder();
			for (int num7 = 0; num7 < count; num7++)
			{
				stringBuilder2.Append("[").Append(num7).Append(":v][")
					.Append(num7)
					.Append(":a]");
			}
			list.Add(string.Concat(stringBuilder2, "concat=n=", count, ":v=1:a=1[vcat][acat]"));
			currentVideo = "vcat";
			text7 = "acat";
			renderedDuration = num;
		}
		else
		{
			currentVideo = "0:v";
			text7 = "0:a";
			renderedDuration = num;
		}
		if (watermark.Enabled)
		{
			AppendAnimatedWatermarkFilters(list, ref currentVideo, count, watermarkLayers);
		}
		if (list.Count == 0)
		{
			stringBuilder.Append(" -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart -progress pipe:1 -nostats ").Append(QuoteArg(outputPath));
			return stringBuilder.ToString();
		}
		stringBuilder.Append(" -filter_complex ").Append(QuoteArg(string.Join(";", list.ToArray())));
		stringBuilder.Append(" -map ").Append(currentVideo.Contains(":") ? currentVideo : ("[" + currentVideo + "]"));
		stringBuilder.Append(" -map ").Append(text7.Contains(":") ? text7 : ("[" + text7 + "]"));
		stringBuilder.Append(" -c:v libx264 -preset fast -crf 17 -pix_fmt yuv420p -metadata:s:v:0 rotate=0 -c:a aac -b:a 192k -ar 48000 -ac 2");
		stringBuilder.Append(OutputDurationArgument(outputLimitSeconds));
		stringBuilder.Append(" -movflags +faststart -progress pipe:1 -nostats ").Append(QuoteArg(outputPath));
		if (outputLimitSeconds > 0.0)
		{
			renderedDuration = Math.Min(renderedDuration, outputLimitSeconds);
		}
		return stringBuilder.ToString();
	}

	private static string OutputDurationArgument(double outputLimitSeconds)
	{
		if (outputLimitSeconds <= 0.0)
		{
			return "";
		}
		return " -t " + FfmpegNumber(Math.Max(0.1, outputLimitSeconds));
	}

	private static List<PreparedWatermarkLayer> PrepareWatermarkLayers(WatermarkProfile profile, int videoWidth, int videoHeight, string tempDirectory, double videoDurationSeconds = 0.0)
	{
		List<PreparedWatermarkLayer> list = new List<PreparedWatermarkLayer>();
		List<WatermarkSettings> list2 = new List<WatermarkSettings>(profile.Layers);
		list2.AddRange(ExpandWatermarkSlideshows(profile.ImageLibraries, videoDurationSeconds));
		List<WatermarkSettings> list3 = OrderWatermarkLayers(list2);
		for (int i = 0; i < list3.Count; i++)
		{
			WatermarkSettings watermarkSettings = CloneWatermarkSettings(list3[i]);
			AdjustWatermarkTiming(watermarkSettings, videoDurationSeconds);
			string fileName = "watermark-layer-" + i.ToString("000") + ".png";
			string text = PrepareWatermarkPng(watermarkSettings, videoWidth, videoHeight, tempDirectory, fileName);
			using Image image = Image.FromFile(text);
			Point point = WatermarkPoint(watermarkSettings.Position, videoWidth, videoHeight, image.Width, image.Height, watermarkSettings.OffsetX, watermarkSettings.OffsetY, watermarkSettings.IsText ? watermarkSettings.SafeMarginPercent : 0, watermarkSettings.AllowOverflow);
			list.Add(new PreparedWatermarkLayer
			{
				Settings = watermarkSettings,
				ImagePath = text,
				Width = image.Width,
				Height = image.Height,
				X = point.X,
				Y = point.Y
			});
		}
		return list;
	}

	private static List<WatermarkSettings> ExpandWatermarkSlideshows(IEnumerable<WatermarkImageLibrary> libraries, double videoDurationSeconds)
	{
		List<WatermarkSettings> list = new List<WatermarkSettings>();
		foreach (WatermarkImageLibrary item in libraries ?? Enumerable.Empty<WatermarkImageLibrary>())
		{
			if (!item.Slideshow || item.Images.Count == 0)
			{
				continue;
			}
			WatermarkSettings watermarkSettings = item.Images[0];
			double num = Math.Max(0.0, watermarkSettings.StartSeconds);
			double num2 = ((!watermarkSettings.ShowUntilEnd && watermarkSettings.EndSeconds > num + 0.01) ? ((videoDurationSeconds > 0.0) ? Math.Min(videoDurationSeconds, watermarkSettings.EndSeconds) : watermarkSettings.EndSeconds) : ((!(videoDurationSeconds > 0.0)) ? (num + item.Images.Sum((WatermarkSettings x) => Math.Max(0.5, x.SlideDurationSeconds))) : videoDurationSeconds));
			if (!(num2 <= num + 0.01))
			{
				string text = NormalizeWatermarkSwitchEffect(item.SwitchEffect);
				double val = ((text == "直接切换") ? 0.04 : Math.Max(0.1, item.SwitchDurationSeconds));
				double num3 = Math.Max(0.0, item.SwitchIntervalSeconds);
				double num4 = num;
				int num5 = 0;
				while (num4 < num2 - 0.01 && num5 < 360)
				{
					WatermarkSettings source = item.Images[num5 % item.Images.Count];
					WatermarkSettings watermarkSettings2 = CloneWatermarkSettings(source);
					double num6 = Math.Max(0.5, watermarkSettings2.SlideDurationSeconds);
					double num7 = Math.Min(num2, num4 + num6 + num3);
					bool flag = num7 < num2 - 0.01;
					double num8 = Math.Min(val, Math.Max(0.04, Math.Min(num6 * 0.45, Math.Max(0.04, num2 - num7))));
					watermarkSettings2.StartSeconds = num4;
					watermarkSettings2.ShowUntilEnd = false;
					watermarkSettings2.EndSeconds = ((flag && text != "直接切换") ? Math.Min(num2, num7 + num8) : num7);
					ApplyWatermarkSwitchAnimation(watermarkSettings2, text, num8, num5 > 0, flag);
					list.Add(watermarkSettings2);
					num4 = num7;
					num5++;
				}
				if (num5 >= 360 && num4 < num2 - 0.01 && list.Count > 0)
				{
					WatermarkSettings watermarkSettings3 = list[list.Count - 1];
					watermarkSettings3.EndSeconds = num2;
					watermarkSettings3.ExitEffect = "直接消失";
					watermarkSettings3.ExitDurationSeconds = 0.1;
				}
			}
		}
		return list;
	}

	private static void ApplyWatermarkSwitchAnimation(WatermarkSettings layer, string switchEffect, double transitionSeconds, bool hasPrevious, bool hasNext)
	{
		switch (switchEffect)
		{
		case "交叉淡化":
			layer.EntryEffect = (hasPrevious ? "淡入" : layer.EntryEffect);
			layer.ExitEffect = (hasNext ? "淡出" : layer.ExitEffect);
			break;
		case "柔和缩放":
			layer.EntryEffect = (hasPrevious ? "放大进入" : layer.EntryEffect);
			layer.ExitEffect = (hasNext ? "缩小退出" : layer.ExitEffect);
			break;
		case "滑动切换":
			layer.EntryEffect = (hasPrevious ? "从左滑入" : layer.EntryEffect);
			layer.ExitEffect = (hasNext ? "向右滑出" : layer.ExitEffect);
			break;
		default:
			layer.EntryEffect = (hasPrevious ? "直接出现" : layer.EntryEffect);
			layer.ExitEffect = (hasNext ? "直接消失" : layer.ExitEffect);
			break;
		}
		if (hasPrevious)
		{
			layer.EntryDurationSeconds = transitionSeconds;
		}
		if (hasNext)
		{
			layer.ExitDurationSeconds = transitionSeconds;
		}
	}

	private static List<WatermarkSettings> OrderWatermarkLayers(IEnumerable<WatermarkSettings> layers)
	{
		return (layers ?? Enumerable.Empty<WatermarkSettings>()).OrderBy(delegate(WatermarkSettings x)
		{
			if (!x.IsText)
			{
				return 1;
			}
			return x.PlaceAboveImages ? 2 : 0;
		}).ToList();
	}

	private static void AdjustWatermarkTiming(WatermarkSettings settings, double videoDurationSeconds)
	{
		settings.EntryEffect = NormalizeEntryEffect(settings.EntryEffect);
		settings.ExitEffect = NormalizeExitEffect(settings.ExitEffect);
		settings.StayEffect = NormalizeStayEffect(settings.StayEffect);
		settings.StayIntensity = Math.Max(0.0, Math.Min(1.0, settings.StayIntensity));
		settings.StayPeriodSeconds = Math.Max(0.2, settings.StayPeriodSeconds);
		settings.StayPauseSeconds = Math.Max(0.0, settings.StayPauseSeconds);
		settings.EntryDurationSeconds = Math.Max(0.04, settings.EntryDurationSeconds);
		settings.ExitDurationSeconds = Math.Max(0.04, settings.ExitDurationSeconds);
		settings.StartSeconds = Math.Max(0.0, settings.StartSeconds);
		bool flag = !settings.ShowUntilEnd && settings.EndSeconds > settings.StartSeconds + 0.01;
		if (videoDurationSeconds > 0.0)
		{
			settings.StartSeconds = Math.Min(settings.StartSeconds, Math.Max(0.0, videoDurationSeconds - 0.04));
			if (flag)
			{
				settings.EndSeconds = Math.Min(settings.EndSeconds, videoDurationSeconds);
			}
		}
		double num = (flag ? settings.EndSeconds : videoDurationSeconds);
		double num2 = ((num > settings.StartSeconds) ? (num - settings.StartSeconds) : Math.Max(settings.EntryDurationSeconds, 0.1));
		double val = Math.Max(0.04, num2 * 0.45);
		settings.EntryDurationSeconds = Math.Min(settings.EntryDurationSeconds, val);
		settings.ExitDurationSeconds = Math.Min(settings.ExitDurationSeconds, val);
	}

	private static void AppendWatermarkInputs(StringBuilder args, List<PreparedWatermarkLayer> layers)
	{
		foreach (PreparedWatermarkLayer layer in layers)
		{
			args.Append(" -loop 1 -framerate 30 -i ").Append(QuoteArg(layer.ImagePath));
		}
	}

	private static void AppendAnimatedWatermarkFilters(List<string> filters, ref string currentVideo, int firstInputIndex, List<PreparedWatermarkLayer> layers)
	{
		for (int i = 0; i < layers.Count; i++)
		{
			PreparedWatermarkLayer preparedWatermarkLayer = layers[i];
			WatermarkSettings settings = preparedWatermarkLayer.Settings;
			double num = Math.Max(0.0, settings.StartSeconds);
			double num2 = Math.Max(0.04, settings.EntryDurationSeconds);
			double value = num + num2;
			string text = NormalizeEntryEffect(settings.EntryEffect);
			string text2 = NormalizeExitEffect(settings.ExitEffect);
			string text3 = NormalizeStayEffect(settings.StayEffect);
			double num3 = Math.Max(0.0, Math.Min(1.0, settings.StayIntensity));
			double num4 = Math.Max(0.2, settings.StayPeriodSeconds);
			double num5 = Math.Max(0.0, settings.StayPauseSeconds);
			bool flag = !settings.ShowUntilEnd && settings.EndSeconds > num + 0.01;
			double num6 = (flag ? settings.EndSeconds : 0.0);
			double num7 = (flag ? Math.Max(0.04, Math.Min(settings.ExitDurationSeconds, num6 - num)) : 0.0);
			double value2 = (flag ? Math.Max(num, num6 - num7) : 0.0);
			string value3 = firstInputIndex + i + ":v";
			string text4 = "wmp" + i;
			string text5 = FfmpegNumber(num);
			string text6 = FfmpegNumber(value);
			string text7 = FfmpegNumber(num2);
			string text8 = FfmpegNumber(num4);
			string text9 = FfmpegNumber(num4 + num5);
			string text10 = "mod(max(0\\,t-" + text6 + ")\\," + text9 + ")";
			string text11 = "lt(" + text10 + "\\," + text8 + ")";
			string text12 = "2*PI*min(" + text10 + "\\," + text8 + ")/" + text8;
			string text13 = "1";
			bool flag2 = text == "放大进入" || text == "弹跳进入" || text3 == "呼吸缩放" || (flag && text2 == "缩小退出");
			if (text == "放大进入")
			{
				text13 = "if(lt(t\\," + text5 + ")\\,0.10\\,if(lt(t\\," + text6 + ")\\,0.10+0.90*(t-" + text5 + ")/" + text7 + "\\,1))";
			}
			else if (text == "弹跳进入")
			{
				text13 = "if(lt(t\\," + text5 + ")\\,0.10\\,if(lt(t\\," + text6 + ")\\,0.10+0.90*(t-" + text5 + ")/" + text7 + "+0.35*sin(PI*(t-" + text5 + ")/" + text7 + ")\\,1))";
			}
			if (text3 == "呼吸缩放")
			{
				double value4 = 0.015 + num3 * 0.085;
				string text14 = "1+" + FfmpegNumber(value4) + "*sin(" + text12 + ")";
				text13 = "if(gte(t\\," + text6 + ")\\," + text14 + "\\," + text13 + ")";
			}
			if (flag && text2 == "缩小退出")
			{
				string text15 = "max(0.10\\,1-0.90*(t-" + FfmpegNumber(value2) + ")/" + FfmpegNumber(num7) + ")";
				text13 = "if(gte(t\\," + FfmpegNumber(value2) + ")\\," + text15 + "\\," + text13 + ")";
			}
			bool flag3 = text == "旋转进入" || text3 == "轻柔摇摆" || text3 == "持续旋转";
			string text16 = "0";
			if (text == "旋转进入")
			{
				text16 = "if(lt(t\\," + text5 + ")\\,0.55\\,if(lt(t\\," + text6 + ")\\,0.55*(1-(t-" + text5 + ")/" + text7 + ")\\,0))";
			}
			if (text3 == "轻柔摇摆")
			{
				double value5 = 0.025 + num3 * 0.15;
				text16 = "if(gte(t\\," + text6 + ")\\," + FfmpegNumber(value5) + "*sin(" + text12 + ")\\," + text16 + ")";
			}
			else if (text3 == "持续旋转")
			{
				text16 = "if(gte(t\\," + text6 + ")\\," + text12 + "\\," + text16 + ")";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[").Append(value3).Append("]format=rgba");
			if (flag2)
			{
				stringBuilder.Append(",scale=w='max(2\\,trunc(iw*(").Append(text13).Append(")/2)*2)':h='max(2\\,trunc(ih*(")
					.Append(text13)
					.Append(")/2)*2)':eval=frame:flags=lanczos+accurate_rnd");
			}
			if (flag3)
			{
				stringBuilder.Append(",rotate=angle='").Append(text16).Append("':ow='hypot(iw,ih)':oh='hypot(iw,ih)':c=none");
			}
			if (text3 == "金色斜向扫光")
			{
				double value6 = 45.0 + num3 * 170.0;
				double value7 = 0.06 + num3 * 0.1;
				string text17 = "mod(max(0\\,T-" + text6 + ")\\," + text9 + ")";
				string text18 = "min(" + text17 + "\\," + text8 + ")/" + text8 + "*(W+H)*1.3-(W+H)*0.15";
				string text19 = FfmpegNumber(value6) + "*exp(-pow((X+Y-(" + text18 + "))/(min(W\\,H)*" + FfmpegNumber(value7) + ")\\,2))";
				string text20 = "if(gte(T\\," + text6 + ")*lt(" + text17 + "\\," + text8 + ")\\," + text19 + "\\,0)";
				AppendWatermarkGeq(stringBuilder, text20, "(" + text20 + ")*0.72", "(" + text20 + ")*0.18");
			}
			else if (text3 == "星光粒子")
			{
				double value8 = 60.0 + num3 * 190.0;
				string text21 = "mod(max(0\\,T-" + text6 + ")\\," + text9 + ")";
				string text22 = FfmpegNumber(value8) + "*if(gt(mod(floor(X/3)*17+floor(Y/3)*29+floor(max(0\\,T-" + text6 + ")*5)*13\\,127)\\,125)\\,0.5+0.5*sin(2*PI*T/" + text8 + ")\\,0)";
				AppendWatermarkGeq(stringBuilder, "if(gte(T\\," + text6 + ")*lt(" + text21 + "\\," + text8 + ")\\," + text22 + "\\,0)");
			}
			switch (text)
			{
			case "淡入":
			case "从下浮入":
			case "旋转进入":
				stringBuilder.Append(",fade=t=in:st=").Append(text5).Append(":d=")
					.Append(text7)
					.Append(":alpha=1");
				break;
			}
			if (flag && text2 == "淡出")
			{
				stringBuilder.Append(",fade=t=out:st=").Append(FfmpegNumber(value2)).Append(":d=")
					.Append(FfmpegNumber(num7))
					.Append(":alpha=1");
			}
			stringBuilder.Append("[").Append(text4).Append("]");
			filters.Add(stringBuilder.ToString());
			string text23 = preparedWatermarkLayer.X.ToString(CultureInfo.InvariantCulture);
			string text24 = preparedWatermarkLayer.Y.ToString(CultureInfo.InvariantCulture);
			bool flag4 = flag2 || flag3;
			string text25 = (flag4 ? (((double)preparedWatermarkLayer.X + (double)preparedWatermarkLayer.Width / 2.0).ToString("0.###", CultureInfo.InvariantCulture) + "-overlay_w/2") : text23);
			string text26 = (flag4 ? (((double)preparedWatermarkLayer.Y + (double)preparedWatermarkLayer.Height / 2.0).ToString("0.###", CultureInfo.InvariantCulture) + "-overlay_h/2") : text24);
			string text27 = text25;
			string text28 = text26;
			switch (text)
			{
			case "从左滑入":
				text27 = "if(lt(t\\," + text6 + ")\\,-overlay_w+(" + text25 + "+overlay_w)*(t-" + text5 + ")/" + text7 + "\\," + text25 + ")";
				break;
			case "从上滑入":
				text28 = "if(lt(t\\," + text6 + ")\\,-overlay_h+(" + text26 + "+overlay_h)*(t-" + text5 + ")/" + text7 + "\\," + text26 + ")";
				break;
			case "从右滑入":
				text27 = "if(lt(t\\," + text6 + ")\\,main_w+(" + text25 + "-main_w)*(t-" + text5 + ")/" + text7 + "\\," + text25 + ")";
				break;
			case "从下浮入":
				text28 = "if(lt(t\\," + text6 + ")\\,(" + text26 + ")+60*(1-(t-" + text5 + ")/" + text7 + ")\\," + text26 + ")";
				break;
			case "底部弹出":
				text28 = "if(lt(t\\," + text6 + ")\\,(" + text26 + ")+(main_h-(" + text26 + "))*pow(1-(t-" + text5 + ")/" + text7 + "\\,2)\\," + text26 + ")";
				break;
			}
			switch (text3)
			{
			case "轻微漂浮":
			{
				double value11 = 2.0 + num3 * 18.0;
				string text32 = "max(0\\,min(max(0\\,main_h-overlay_h)\\,(" + text26 + ")+" + FfmpegNumber(value11) + "*sin(" + text12 + ")))";
				text28 = "if(gte(t\\," + text6 + ")\\," + text32 + "\\," + text28 + ")";
				break;
			}
			case "律动弹跳":
			{
				double value10 = 3.0 + num3 * 26.0;
				string text31 = "max(0\\,min(max(0\\,main_h-overlay_h)\\,(" + text26 + ")-" + FfmpegNumber(value10) + "*abs(sin(" + text12 + "))))";
				text28 = "if(gte(t\\," + text6 + ")\\," + text31 + "\\," + text28 + ")";
				break;
			}
			case "轻微抖动":
			{
				double value9 = 1.0 + num3 * 11.0;
				string text29 = "max(0\\,min(max(0\\,main_w-overlay_w)\\,(" + text25 + ")+" + FfmpegNumber(value9) + "*sin(3*(" + text12 + "))))";
				string text30 = "max(0\\,min(max(0\\,main_h-overlay_h)\\,(" + text26 + ")+" + FfmpegNumber(value9) + "*sin(4*(" + text12 + ")+1.2)))";
				text27 = "if(gte(t\\," + text6 + ")\\," + text29 + "\\," + text27 + ")";
				text28 = "if(gte(t\\," + text6 + ")\\," + text30 + "\\," + text28 + ")";
				break;
			}
			}
			if (flag && text2 == "缩小退出")
			{
				text27 = text25;
				text28 = text26;
			}
			else if (flag && text2 == "向右滑出")
			{
				text27 = "if(gte(t\\," + FfmpegNumber(value2) + ")\\,min(main_w\\," + text23 + "+(main_w-" + text23 + ")*(t-" + FfmpegNumber(value2) + ")/" + FfmpegNumber(num7) + ")\\," + text27 + ")";
			}
			else if (flag && text2 == "向下滑出")
			{
				text28 = "if(gte(t\\," + FfmpegNumber(value2) + ")\\,min(main_h\\," + text24 + "+(main_h-" + text24 + ")*(t-" + FfmpegNumber(value2) + ")/" + FfmpegNumber(num7) + ")\\," + text28 + ")";
			}
			string text33 = "wmv" + i;
			string text34 = (flag ? ("between(t," + FfmpegNumber(num) + "," + FfmpegNumber(num6) + ")") : ("gte(t," + FfmpegNumber(num) + ")"));
			if (text3 == "闪烁")
			{
				double value12 = -0.85 + num3 * 1.2;
				text34 = "(" + text34 + ")*if(lt(t," + text6 + "),1,if(" + text11 + ",gt(sin(" + text12 + ")," + FfmpegNumber(value12) + "),1))";
			}
			filters.Add("[" + currentVideo + "][" + text4 + "]overlay=x='" + text27 + "':y='" + text28 + "':enable='" + text34 + "':eof_action=repeat:shortest=1[" + text33 + "]");
			currentVideo = text33;
		}
	}

	private static void AppendWatermarkGeq(StringBuilder filter, string brightnessExpression)
	{
		AppendWatermarkGeq(filter, brightnessExpression, brightnessExpression, brightnessExpression);
	}

	private static void AppendWatermarkGeq(StringBuilder filter, string redBrightness, string greenBrightness, string blueBrightness)
	{
		filter.Append(",geq=r='min(255\\,r(X\\,Y)+").Append(redBrightness).Append(")':g='min(255\\,g(X\\,Y)+")
			.Append(greenBrightness)
			.Append(")':b='min(255\\,b(X\\,Y)+")
			.Append(blueBrightness)
			.Append(")':a='alpha(X\\,Y)'");
	}

	private static string PrepareCompositeWatermarkPng(WatermarkProfile profile, int videoWidth, int videoHeight, string tempDirectory)
	{
		string text = Path.Combine(tempDirectory, "watermark-composite.png");
		using Bitmap bitmap = new Bitmap(videoWidth, videoHeight, PixelFormat.Format32bppArgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.Clear(Color.Transparent);
		graphics.CompositingMode = CompositingMode.SourceOver;
		graphics.CompositingQuality = CompositingQuality.HighQuality;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.SmoothingMode = SmoothingMode.HighQuality;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		foreach (WatermarkSettings item in OrderWatermarkLayers(profile.Layers))
		{
			string filename = PrepareWatermarkPng(item, videoWidth, videoHeight, tempDirectory, "watermark-composite-layer-" + Guid.NewGuid().ToString("N") + ".png");
			using Image image = Image.FromFile(filename);
			Point point = WatermarkPoint(item.Position, videoWidth, videoHeight, image.Width, image.Height, item.OffsetX, item.OffsetY, item.IsText ? item.SafeMarginPercent : 0, item.AllowOverflow);
			graphics.DrawImageUnscaled(image, point.X, point.Y);
		}
		bitmap.Save(text, ImageFormat.Png);
		return text;
	}

	private static Point WatermarkPoint(string position, int canvasWidth, int canvasHeight, int layerWidth, int layerHeight, int offsetX, int offsetY)
	{
		return WatermarkPoint(position, canvasWidth, canvasHeight, layerWidth, layerHeight, offsetX, offsetY, 0, allowOverflow: false);
	}

	private static Point WatermarkPoint(string position, int canvasWidth, int canvasHeight, int layerWidth, int layerHeight, int offsetX, int offsetY, int safeMarginPercent)
	{
		return WatermarkPoint(position, canvasWidth, canvasHeight, layerWidth, layerHeight, offsetX, offsetY, safeMarginPercent, allowOverflow: false);
	}

	private static Point WatermarkPoint(string position, int canvasWidth, int canvasHeight, int layerWidth, int layerHeight, int offsetX, int offsetY, int safeMarginPercent, bool allowOverflow)
	{
		int num = ((!allowOverflow) ? ((safeMarginPercent > 0) ? ((int)Math.Round((double)(Math.Min(canvasWidth, canvasHeight) * Math.Min(20, safeMarginPercent)) / 100.0)) : 20) : 0);
		int num2;
		int num3;
		switch (position)
		{
		case "左上角":
			num2 = num;
			num3 = num;
			break;
		case "右上角":
			num2 = canvasWidth - layerWidth - num;
			num3 = num;
			break;
		case "左下角":
			num2 = num;
			num3 = canvasHeight - layerHeight - num;
			break;
		case "顶部居中":
			num2 = (canvasWidth - layerWidth) / 2;
			num3 = num;
			break;
		case "底部居中":
			num2 = (canvasWidth - layerWidth) / 2;
			num3 = canvasHeight - layerHeight - num;
			break;
		case "左侧居中":
			num2 = num;
			num3 = (canvasHeight - layerHeight) / 2;
			break;
		case "右侧居中":
			num2 = canvasWidth - layerWidth - num;
			num3 = (canvasHeight - layerHeight) / 2;
			break;
		case "画面中央":
			num2 = (canvasWidth - layerWidth) / 2;
			num3 = (canvasHeight - layerHeight) / 2;
			break;
		default:
			num2 = canvasWidth - layerWidth - num;
			num3 = canvasHeight - layerHeight - num;
			break;
		}
		num2 += offsetX;
		num3 += offsetY;
		if (allowOverflow)
		{
			return new Point(num2, num3);
		}
		int num4 = ((layerWidth + num * 2 <= canvasWidth) ? num : 0);
		int num5 = ((layerHeight + num * 2 <= canvasHeight) ? num : 0);
		int val = Math.Max(num4, canvasWidth - layerWidth - num4);
		int val2 = Math.Max(num5, canvasHeight - layerHeight - num5);
		num2 = Math.Max(num4, Math.Min(val, num2));
		num3 = Math.Max(num5, Math.Min(val2, num3));
		return new Point(num2, num3);
	}

	private static string PrepareWatermarkPng(WatermarkSettings settings, int videoWidth, string tempDirectory)
	{
		return PrepareWatermarkPng(settings, videoWidth, 0, tempDirectory, "watermark.png");
	}

	private static string PrepareWatermarkPng(WatermarkSettings settings, int videoWidth, string tempDirectory, string fileName)
	{
		return PrepareWatermarkPng(settings, videoWidth, 0, tempDirectory, fileName);
	}

	private static string PrepareWatermarkPng(WatermarkSettings settings, int videoWidth, int videoHeight, string tempDirectory, string fileName)
	{
		string text = Path.Combine(tempDirectory, fileName);
		int alpha = Math.Max(0, Math.Min(255, (int)Math.Round(settings.Opacity * 255.0)));
		if (settings.IsText)
		{
			Font font = CreateWatermarkFont(settings);
			try
			{
				using Bitmap image = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
				Graphics measureGraphics = Graphics.FromImage(image);
				try
				{
					StringFormat textFormat = new StringFormat(StringFormat.GenericTypographic);
					try
					{
						measureGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
						int num = (settings.TextBackgroundEnabled ? Math.Max(0, settings.TextBackgroundPaddingX) : 8);
						int num2 = (settings.TextBackgroundEnabled ? Math.Max(0, settings.TextBackgroundPaddingY) : 6);
						int num3 = (settings.TextOutlineEnabled ? Math.Max(1, Math.Min(20, (settings.TextOutlineWidth <= 0) ? 2 : settings.TextOutlineWidth)) : 0);
						int num4 = ((num3 > 0) ? (num3 + 2) : 0);
						int num5 = Math.Max(20, Math.Min(100, (settings.TextMaxWidthPercent <= 0) ? 85 : settings.TextMaxWidthPercent));
						int num6 = ((!settings.AllowOverflow) ? ((int)Math.Round((double)(videoWidth * Math.Max(0, Math.Min(20, settings.SafeMarginPercent))) / 100.0)) : 0);
						int val = Math.Max(24, (int)Math.Round((double)(videoWidth * num5) / 100.0));
						int val2 = Math.Max(24, videoWidth - num6 * 2);
						int num7 = Math.Min(val, val2);
						textFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
						int contentWidth = Math.Max(12, num7 - (num + num4) * 2);
						List<string> list = WrapWatermarkText(measureGraphics, settings.Text ?? "", font, contentWidth, textFormat);
						int num8 = Math.Max(2, (int)Math.Ceiling(measureGraphics.MeasureString("Ag国", font, int.MaxValue, textFormat).Height));
						float[] array = list.Select((string line) => Math.Min(contentWidth, Math.Max(0f, measureGraphics.MeasureString(line, font, int.MaxValue, textFormat).Width))).ToArray();
						bool flag = settings.TextBackgroundEnabled && settings.TextBackgroundStyle == "逐行包裹文字";
						int num9 = (flag ? 2 : 0);
						int num10 = (flag ? (num8 + (num2 + num4) * 2) : num8);
						int num11 = Math.Max(2, num7);
						int num12 = (flag ? Math.Max(2, num10 * list.Count + num9 * Math.Max(0, list.Count - 1)) : Math.Max(2, num8 * list.Count + (num2 + num4) * 2));
						using Bitmap bitmap = new Bitmap(num11, num12, PixelFormat.Format32bppArgb);
						using Graphics graphics = Graphics.FromImage(bitmap);
						using Brush fillBrush = new SolidBrush(Color.FromArgb(alpha, settings.TextColor));
						graphics.Clear(Color.Transparent);
						graphics.SmoothingMode = SmoothingMode.HighQuality;
						graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
						int alpha2 = Math.Max(0, Math.Min(255, (int)Math.Round(settings.TextBackgroundOpacity * 255.0)));
						Color baseColor = (settings.TextBackgroundColor.IsEmpty ? Color.Black : settings.TextBackgroundColor);
						using (Brush brush = new SolidBrush(Color.FromArgb(alpha2, baseColor)))
						{
							if (settings.TextBackgroundEnabled && !flag)
							{
								using GraphicsPath path = CreateRoundedRectanglePath(new RectangleF(0.5f, 0.5f, (float)num11 - 1f, (float)num12 - 1f), settings.TextBackgroundCornerRadius);
								graphics.FillPath(brush, path);
							}
							for (int num13 = 0; num13 < list.Count; num13++)
							{
								float num14 = array[num13];
								float num18;
								float num19;
								if (flag)
								{
									float num15 = Math.Min(num11, Math.Max(2f, num14 + (float)((num + num4) * 2)));
									float num16 = AlignedWatermarkX(settings.TextAlignment, num11, num15, 0f);
									float num17 = num13 * (num10 + num9);
									using (GraphicsPath path2 = CreateRoundedRectanglePath(new RectangleF(num16 + 0.5f, num17 + 0.5f, Math.Max(1f, num15 - 1f), Math.Max(1f, (float)num10 - 1f)), settings.TextBackgroundCornerRadius))
									{
										graphics.FillPath(brush, path2);
									}
									num18 = num16 + (float)num + (float)num4;
									num19 = num17 + (float)num2 + (float)num4;
								}
								else
								{
									float num20 = num + num4;
									float availableWidth = Math.Max(2f, (float)num11 - num20 * 2f);
									num18 = AlignedWatermarkX(settings.TextAlignment, availableWidth, num14, num20);
									num19 = num2 + num4 + num13 * num8;
								}
								DrawWatermarkTextLine(graphics, list[num13], font, fillBrush, num18, num19, Math.Max(2f, num14 + 2f), num8, settings, alpha, textFormat);
							}
						}
						int num21 = ((videoHeight > 0 && !settings.AllowOverflow) ? Math.Max(2, videoHeight - (int)Math.Round((double)(Math.Min(videoWidth, videoHeight) * Math.Max(0, Math.Min(20, settings.SafeMarginPercent)) * 2) / 100.0)) : int.MaxValue);
						if (bitmap.Height > num21)
						{
							int num22 = Math.Max(2, (int)Math.Round((double)(bitmap.Width * num21) / (double)bitmap.Height));
							using Bitmap bitmap2 = new Bitmap(num22, num21, PixelFormat.Format32bppArgb);
							using Graphics graphics2 = Graphics.FromImage(bitmap2);
							graphics2.Clear(Color.Transparent);
							graphics2.CompositingQuality = CompositingQuality.HighQuality;
							graphics2.InterpolationMode = InterpolationMode.HighQualityBicubic;
							graphics2.DrawImage(bitmap, new Rectangle(0, 0, num22, num21));
							bitmap2.Save(text, ImageFormat.Png);
						}
						else
						{
							bitmap.Save(text, ImageFormat.Png);
						}
					}
					finally
					{
						if (textFormat != null)
						{
							((IDisposable)textFormat).Dispose();
						}
					}
				}
				finally
				{
					if (measureGraphics != null)
					{
						((IDisposable)measureGraphics).Dispose();
					}
				}
			}
			finally
			{
				if (font != null)
				{
					((IDisposable)font).Dispose();
				}
			}
		}
		else
		{
			using Image image2 = Image.FromFile(settings.ImagePath);
			int num23 = Math.Max(2, MakeEven((int)Math.Round((double)(videoWidth * settings.ImageWidthPercent) / 100.0)));
			int num24 = Math.Max(2, (int)Math.Round((double)image2.Height * ((double)num23 / (double)image2.Width)));
			using Bitmap bitmap3 = new Bitmap(num23, num24, PixelFormat.Format32bppArgb);
			using (Graphics graphics3 = Graphics.FromImage(bitmap3))
			{
				graphics3.Clear(Color.Transparent);
				graphics3.CompositingMode = CompositingMode.SourceCopy;
				graphics3.CompositingQuality = CompositingQuality.HighQuality;
				graphics3.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics3.SmoothingMode = SmoothingMode.HighQuality;
				graphics3.PixelOffsetMode = PixelOffsetMode.HighQuality;
				using ImageAttributes imageAttributes = new ImageAttributes();
				imageAttributes.SetWrapMode(WrapMode.TileFlipXY);
				graphics3.DrawImage(image2, new Rectangle(0, 0, num23, num24), 0, 0, image2.Width, image2.Height, GraphicsUnit.Pixel, imageAttributes);
			}
			NormalizeImageAlpha(bitmap3, settings.Opacity);
			bitmap3.Save(text, ImageFormat.Png);
		}
		return text;
	}

	private static float AlignedWatermarkX(string alignment, float availableWidth, float itemWidth, float availableLeft)
	{
		if (alignment == "居中对齐")
		{
			return availableLeft + Math.Max(0f, (availableWidth - itemWidth) / 2f);
		}
		if (alignment == "右对齐")
		{
			return availableLeft + Math.Max(0f, availableWidth - itemWidth);
		}
		return availableLeft;
	}

	private static List<string> WrapWatermarkText(Graphics graphics, string text, Font font, int maximumWidth, StringFormat format)
	{
		List<string> list = new List<string>();
		string text2 = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
		string[] array = text2.Split(new char[1] { '\n' }, StringSplitOptions.None);
		string[] array2 = array;
		foreach (string text3 in array2)
		{
			if (text3.Length == 0)
			{
				list.Add("");
				continue;
			}
			string[] array3 = text3.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (array3.Length == 0)
			{
				list.Add("");
				continue;
			}
			string text4 = "";
			string[] array4 = array3;
			foreach (string text5 in array4)
			{
				string text6 = ((text4.Length == 0) ? text5 : (text4 + " " + text5));
				if (MeasureWatermarkLine(graphics, text6, font, format) <= (float)maximumWidth)
				{
					text4 = text6;
					continue;
				}
				if (text4.Length > 0)
				{
					list.Add(text4);
					text4 = "";
				}
				string text7 = "";
				string text8 = text5;
				for (int k = 0; k < text8.Length; k++)
				{
					char c = text8[k];
					string text9 = text7 + c;
					if (text7.Length > 0 && MeasureWatermarkLine(graphics, text9, font, format) > (float)maximumWidth)
					{
						list.Add(text7);
						text7 = c.ToString();
					}
					else
					{
						text7 = text9;
					}
				}
				text4 = text7;
			}
			if (text4.Length > 0)
			{
				list.Add(text4);
			}
		}
		if (list.Count == 0)
		{
			list.Add("");
		}
		return list;
	}

	private static float MeasureWatermarkLine(Graphics graphics, string text, Font font, StringFormat format)
	{
		return graphics.MeasureString(text ?? "", font, int.MaxValue, format).Width;
	}

	private static void DrawWatermarkTextLine(Graphics graphics, string text, Font font, Brush fillBrush, float x, float y, float width, float height, WatermarkSettings settings, int alpha, StringFormat sourceFormat)
	{
		using StringFormat stringFormat = new StringFormat(sourceFormat);
		stringFormat.FormatFlags |= StringFormatFlags.NoWrap;
		RectangleF rectangleF = new RectangleF(x, y, Math.Max(2f, width), Math.Max(2f, height));
		if (!settings.TextOutlineEnabled)
		{
			graphics.DrawString(text ?? "", font, fillBrush, rectangleF, stringFormat);
			return;
		}
		Color baseColor = (settings.TextOutlineColor.IsEmpty ? Color.Black : settings.TextOutlineColor);
		float num = Math.Max(1, Math.Min(20, (settings.TextOutlineWidth <= 0) ? 2 : settings.TextOutlineWidth));
		using GraphicsPath graphicsPath = new GraphicsPath();
		using Pen pen = new Pen(Color.FromArgb(alpha, baseColor), num);
		pen.LineJoin = LineJoin.Round;
		graphicsPath.AddString(text ?? "", font.FontFamily, (int)font.Style, font.Size, rectangleF, stringFormat);
		graphics.DrawPath(pen, graphicsPath);
		graphics.FillPath(fillBrush, graphicsPath);
	}

	private static GraphicsPath CreateRoundedRectanglePath(RectangleF rectangle, float radius)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		float num = Math.Max(0f, Math.Min(radius, Math.Min(rectangle.Width, rectangle.Height) / 2f));
		if (num < 0.5f)
		{
			graphicsPath.AddRectangle(rectangle);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}
		float num2 = num * 2f;
		RectangleF rect = new RectangleF(rectangle.X, rectangle.Y, num2, num2);
		graphicsPath.AddArc(rect, 180f, 90f);
		rect.X = rectangle.Right - num2;
		graphicsPath.AddArc(rect, 270f, 90f);
		rect.Y = rectangle.Bottom - num2;
		graphicsPath.AddArc(rect, 0f, 90f);
		rect.X = rectangle.X;
		graphicsPath.AddArc(rect, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	private static void NormalizeImageAlpha(Bitmap bitmap, double opacity)
	{
		Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
		BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
		try
		{
			int num = Math.Abs(bitmapData.Stride) * bitmap.Height;
			byte[] array = new byte[num];
			Marshal.Copy(bitmapData.Scan0, array, 0, num);
			int[] array2 = new int[256];
			int num2 = 0;
			for (int i = 0; i < bitmap.Height; i++)
			{
				int num3 = i * Math.Abs(bitmapData.Stride);
				for (int j = 0; j < bitmap.Width; j++)
				{
					int num4 = array[num3 + j * 4 + 3];
					if (num4 > 0)
					{
						array2[num4]++;
						num2++;
					}
				}
			}
			if (num2 == 0)
			{
				return;
			}
			int num5 = (int)Math.Ceiling((double)num2 * 0.9);
			int num6 = 0;
			int val = 255;
			for (int k = 1; k <= 255; k++)
			{
				num6 += array2[k];
				if (num6 >= num5)
				{
					val = k;
					break;
				}
			}
			int num7 = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, opacity)) * 255.0);
			double num8 = (double)num7 / (double)Math.Max(1, val);
			for (int l = 0; l < bitmap.Height; l++)
			{
				int num9 = l * Math.Abs(bitmapData.Stride);
				for (int m = 0; m < bitmap.Width; m++)
				{
					int num10 = num9 + m * 4 + 3;
					array[num10] = (byte)Math.Max(0, Math.Min(num7, (int)Math.Round((double)(int)array[num10] * num8)));
				}
			}
			Marshal.Copy(array, 0, bitmapData.Scan0, num);
		}
		finally
		{
			bitmap.UnlockBits(bitmapData);
		}
	}

	private static Font CreateWatermarkFont(WatermarkSettings settings)
	{
		string requestedFamily = (string.IsNullOrWhiteSpace(settings.FontFamilyName) ? "Noto Sans SC" : settings.FontFamilyName);
		FontStyle fontStyle = FontStyle.Regular;
		if (settings.FontBold)
		{
			fontStyle |= FontStyle.Bold;
		}
		if (settings.FontItalic)
		{
			fontStyle |= FontStyle.Italic;
		}
		FontFamily fontFamily = null;
		if (requestedFamily.StartsWith("Noto ", StringComparison.OrdinalIgnoreCase))
		{
			EnsureWatermarkFontsLoaded();
			lock (WatermarkFontLock)
			{
				if (_watermarkPrivateFonts != null)
				{
					fontFamily = _watermarkPrivateFonts.Families.FirstOrDefault((FontFamily x) => string.Equals(x.Name, requestedFamily, StringComparison.OrdinalIgnoreCase));
				}
			}
		}
		if (fontFamily != null)
		{
			try
			{
				return new Font(fontFamily, settings.FontSize, fontStyle, GraphicsUnit.Pixel);
			}
			catch
			{
			}
		}
		try
		{
			return new Font(requestedFamily, settings.FontSize, fontStyle, GraphicsUnit.Pixel);
		}
		catch
		{
			try
			{
				return new Font("Microsoft YaHei UI", settings.FontSize, fontStyle, GraphicsUnit.Pixel);
			}
			catch
			{
				return new Font(FontFamily.GenericSansSerif, settings.FontSize, FontStyle.Regular, GraphicsUnit.Pixel);
			}
		}
	}

	private static void EnsureWatermarkFontsLoaded()
	{
		lock (WatermarkFontLock)
		{
			if (_watermarkFontsLoaded)
			{
				return;
			}
			_watermarkFontsLoaded = true;
			_watermarkPrivateFonts = new PrivateFontCollection();
			string directoryName = Path.GetDirectoryName(typeof(MainForm).Assembly.Location);
			string path = Path.Combine(directoryName ?? AppDomain.CurrentDomain.BaseDirectory, "fonts");
			string[] array = new string[2] { "NotoSansSC-VF.ttf", "NotoSerifSC-VF.ttf" };
			foreach (string path2 in array)
			{
				string text = Path.Combine(path, path2);
				try
				{
					if (File.Exists(text))
					{
						_watermarkPrivateFonts.AddFontFile(text);
					}
				}
				catch
				{
				}
			}
		}
	}

	private static string WatermarkOverlayExpression(string position)
	{
		return position switch
		{
			"左上角" => "x=20:y=20", 
			"右上角" => "x=main_w-overlay_w-20:y=20", 
			"左下角" => "x=20:y=main_h-overlay_h-20", 
			"画面中央" => "x=(main_w-overlay_w)/2:y=(main_h-overlay_h)/2", 
			_ => "x=main_w-overlay_w-20:y=main_h-overlay_h-20", 
		};
	}

	private static string FfmpegNumber(double value)
	{
		return value.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static string BuildAdjustedVideoFilter(int targetWidth, int targetHeight, VideoAdjustmentSettings adjustment, bool normalizeFrameRate)
	{
		return BuildAdjustedVideoFilterWithCanvasMode(targetWidth, targetHeight, adjustment, normalizeFrameRate, fillCanvas: false);
	}

	private static bool ShouldCenterCropPortrait(VideoInfo info, VideoAdjustmentSettings adjustment)
	{
		if (info != null && adjustment != null && adjustment.CenterCropPortrait && info.Width > 0 && info.Height > 0)
		{
			return info.Width > info.Height;
		}
		return false;
	}

	private static Size GetAdjustedCanvasSize(VideoInfo info, VideoAdjustmentSettings adjustment)
	{
		int num = MakeEven((info != null && info.Width > 0) ? info.Width : 1920);
		int val = MakeEven((info != null && info.Height > 0) ? info.Height : 1080);
		if (!ShouldCenterCropPortrait(info, adjustment))
		{
			return new Size(num, val);
		}
		int num2 = MakeEven(Math.Min(1080, val));
		int num3 = MakeEven((int)Math.Round((double)num2 * 16.0 / 9.0));
		return new Size(num2, num3);
	}

	private static Size GetOutputCanvasSize(VideoInfo info, VideoAdjustmentSettings adjustment, OutputFrameSettings outputFrame)
	{
		outputFrame = outputFrame ?? new OutputFrameSettings();
		if (!outputFrame.ForceAspect)
		{
			return GetAdjustedCanvasSize(info, adjustment);
		}
		int val = MakeEven((info != null && info.Width > 0) ? info.Width : 1920);
		int val2 = MakeEven((info != null && info.Height > 0) ? info.Height : 1080);
		int val3 = Math.Max(2, Math.Min(val, val2));
		if (outputFrame.AspectMode == 2)
		{
			int num = MakeEven(Math.Min(1080, val3));
			int num2 = MakeEven((int)Math.Round((double)num * 16.0 / 9.0));
			return new Size(num2, num);
		}
		int num3 = MakeEven(Math.Min(1080, val3));
		int num4 = MakeEven((int)Math.Round((double)num3 * 16.0 / 9.0));
		return new Size(num3, num4);
	}

	private static bool ShouldFillOutputCanvas(VideoInfo info, VideoAdjustmentSettings adjustment, OutputFrameSettings outputFrame)
	{
		if (outputFrame == null || !outputFrame.ForceAspect)
		{
			return ShouldCenterCropPortrait(info, adjustment);
		}
		return true;
	}

	private static string DescribeOutputFrame(OutputFrameSettings outputFrame)
	{
		outputFrame = outputFrame ?? new OutputFrameSettings();
		if (!outputFrame.ForceAspect)
		{
			return "自动（沿用每行设置）";
		}
		string text = ((outputFrame.AspectMode == 2) ? "16:9 横屏" : "9:16 竖屏");
		string text2 = ((outputFrame.CropAnchor == 1) ? ((outputFrame.AspectMode == 2) ? "上三分" : "左三分") : ((outputFrame.CropAnchor == 2) ? ((outputFrame.AspectMode == 2) ? "下三分" : "右三分") : ((outputFrame.CropAnchor != 3) ? "中间部分" : ("自定义 " + ResolveCropPositionPercent(outputFrame) + "%"))));
		return text + "，截取" + text2;
	}

	private static string BuildAdjustedVideoFilterWithCanvasMode(int targetWidth, int targetHeight, VideoAdjustmentSettings adjustment, bool normalizeFrameRate, bool fillCanvas)
	{
		return BuildAdjustedVideoFilterWithCanvasCrop(targetWidth, targetHeight, adjustment, normalizeFrameRate, fillCanvas, 50);
	}

	private static string BuildAdjustedVideoFilterWithCanvasCrop(int targetWidth, int targetHeight, VideoAdjustmentSettings adjustment, bool normalizeFrameRate, bool fillCanvas, int cropPositionPercent)
	{
		adjustment = adjustment ?? new VideoAdjustmentSettings();
		targetWidth = MakeEven(targetWidth);
		targetHeight = MakeEven(targetHeight);
		StringBuilder stringBuilder = new StringBuilder();
		if (fillCanvas)
		{
			double value = (double)Math.Max(0, Math.Min(100, cropPositionPercent)) / 100.0;
			string value2 = FfmpegNumber(value);
			stringBuilder.Append("scale=").Append(targetWidth).Append(":")
				.Append(targetHeight)
				.Append(":force_original_aspect_ratio=increase:flags=lanczos+accurate_rnd,crop=")
				.Append(targetWidth)
				.Append(":")
				.Append(targetHeight)
				.Append(":x='(iw-ow)*")
				.Append(value2)
				.Append("':y='(ih-oh)*")
				.Append(value2)
				.Append("',setsar=1");
		}
		else
		{
			stringBuilder.Append("scale=").Append(targetWidth).Append(":")
				.Append(targetHeight)
				.Append(":force_original_aspect_ratio=decrease:flags=lanczos+accurate_rnd,pad=")
				.Append(targetWidth)
				.Append(":")
				.Append(targetHeight)
				.Append(":(ow-iw)/2:(oh-ih)/2:black,setsar=1");
		}
		if (adjustment.HorizontalFlip)
		{
			stringBuilder.Append(",hflip");
		}
		if (adjustment.ReversePlayback)
		{
			stringBuilder.Append(",reverse");
		}
		double num = Math.Max(0.1, Math.Min(3.0, adjustment.ScaleRatio));
		if (num < 0.9995)
		{
			int value3 = MakeEven((int)Math.Round((double)targetWidth * num));
			int value4 = MakeEven((int)Math.Round((double)targetHeight * num));
			stringBuilder.Append(",scale=").Append(value3).Append(":")
				.Append(value4)
				.Append(":flags=lanczos+accurate_rnd")
				.Append(",pad=")
				.Append(targetWidth)
				.Append(":")
				.Append(targetHeight)
				.Append(":(ow-iw)/2:(oh-ih)/2:black");
		}
		else if (num > 1.0005)
		{
			int value5 = MakeEven((int)Math.Round((double)targetWidth * num));
			int value6 = MakeEven((int)Math.Round((double)targetHeight * num));
			stringBuilder.Append(",scale=").Append(value5).Append(":")
				.Append(value6)
				.Append(":flags=lanczos+accurate_rnd")
				.Append(",crop=")
				.Append(targetWidth)
				.Append(":")
				.Append(targetHeight)
				.Append(":(iw-ow)/2:(ih-oh)/2");
		}
		double num2 = Math.Max(0.1, Math.Min(3.0, adjustment.SpeedRatio));
		stringBuilder.Append(",setpts=(PTS-STARTPTS)/").Append(FfmpegNumber(num2));
		if (num2 < 0.9995)
		{
			if (num2 < 0.45)
			{
				stringBuilder.Append(",minterpolate=fps=30:mi_mode=blend");
			}
			else
			{
				stringBuilder.Append(",minterpolate=fps=30:mi_mode=mci:mc_mode=aobmc:me_mode=bidir:vsbmc=1");
			}
		}
		else if (normalizeFrameRate)
		{
			stringBuilder.Append(",fps=30");
		}
		stringBuilder.Append(",format=yuv420p");
		return stringBuilder.ToString();
	}

	private static string BuildAdjustedAudioFilter(VideoAdjustmentSettings adjustment)
	{
		adjustment = adjustment ?? new VideoAdjustmentSettings();
		double num = Math.Max(0.1, Math.Min(3.0, adjustment.SpeedRatio));
		List<string> list = new List<string>();
		list.Add("asetpts=PTS-STARTPTS");
		List<string> list2 = list;
		if (adjustment.ReversePlayback)
		{
			list2.Add("areverse");
		}
		while (num < 0.4999)
		{
			list2.Add("atempo=0.5");
			num /= 0.5;
		}
		while (num > 2.0001)
		{
			list2.Add("atempo=2.0");
			num /= 2.0;
		}
		if (Math.Abs(num - 1.0) > 0.0001)
		{
			list2.Add("atempo=" + FfmpegNumber(num));
		}
		list2.Add("volume=" + FfmpegNumber((double)Math.Max(0, Math.Min(300, adjustment.VolumePercent)) / 100.0));
		list2.Add("aresample=async=1:first_pts=0");
		return string.Join(",", list2.ToArray());
	}

	private static string DescribeVideoAdjustment(VideoAdjustmentSettings adjustment)
	{
		adjustment = adjustment ?? new VideoAdjustmentSettings();
		return "翻转=" + (adjustment.HorizontalFlip ? "是" : "否") + "，倒放=" + (adjustment.ReversePlayback ? "是" : "否") + "，横屏裁中=" + (adjustment.CenterCropPortrait ? "是" : "否") + "，缩放=" + FfmpegNumber(adjustment.ScaleRatio) + " 倍，速率=" + FfmpegNumber(adjustment.SpeedRatio) + " 倍" + ((adjustment.SpeedRatio < 0.45) ? "（快速融合补帧）" : ((adjustment.SpeedRatio < 0.9995) ? "（自动运动补帧）" : "")) + "，音量=" + adjustment.VolumePercent + "%";
	}

	private VideoInfo Probe(string ffmpeg, string path)
	{
		string arguments = "-hide_banner -i " + QuoteArg(path);
		ProcessStartInfo startInfo = NewProcessInfo(ffmpeg, arguments);
		new StringBuilder();
		using Process process = new Process();
		process.StartInfo = startInfo;
		process.Start();
		lock (_processLock)
		{
			_currentProcess = process;
		}
		string input = process.StandardError.ReadToEnd();
		process.WaitForExit();
		lock (_processLock)
		{
			if (_currentProcess == process)
			{
				_currentProcess = null;
			}
		}
		VideoInfo videoInfo = new VideoInfo();
		videoInfo.Path = path;
		Match match = Regex.Match(input, "Duration:\\s*(\\d+):(\\d+):(\\d+(?:\\.\\d+)?)", RegexOptions.IgnoreCase);
		if (match.Success)
		{
			videoInfo.DurationSeconds = (double)(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600 + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60) + double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
		}
		Match match2 = Regex.Match(input, "Video:\\s*[^\\r\\n]*?\\b(\\d{2,5})x(\\d{2,5})\\b", RegexOptions.IgnoreCase);
		videoInfo.HasVideo = match2.Success;
		if (match2.Success)
		{
			videoInfo.CodedWidth = int.Parse(match2.Groups[1].Value, CultureInfo.InvariantCulture);
			videoInfo.CodedHeight = int.Parse(match2.Groups[2].Value, CultureInfo.InvariantCulture);
			videoInfo.Width = videoInfo.CodedWidth;
			videoInfo.Height = videoInfo.CodedHeight;
			Match match3 = Regex.Match(input, "rotation of\\s+(-?\\d+(?:\\.\\d+)?)\\s+degrees", RegexOptions.IgnoreCase);
			if (!match3.Success)
			{
				match3 = Regex.Match(input, "(?:rotate|rotation)\\s*:\\s*(-?\\d+(?:\\.\\d+)?)", RegexOptions.IgnoreCase);
			}
			if (match3.Success && double.TryParse(match3.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				int num = (videoInfo.RotationDegrees = ((int)Math.Round(result) % 360 + 360) % 360);
				if (num == 90 || num == 270)
				{
					videoInfo.Width = videoInfo.CodedHeight;
					videoInfo.Height = videoInfo.CodedWidth;
				}
			}
		}
		videoInfo.HasAudio = Regex.IsMatch(input, "Audio:\\s*", RegexOptions.IgnoreCase);
		return videoInfo;
	}

	private double MeasureVideoStreamDuration(string ffmpeg, string path, double fallbackDuration)
	{
		string arguments = "-hide_banner -loglevel error -i " + QuoteArg(path) + " -map 0:v:0 -an -c:v copy -progress pipe:1 -nostats -f null -";
		ProcessStartInfo startInfo = NewProcessInfo(ffmpeg, arguments);
		long num = 0L;
		using (Process process = new Process())
		{
			process.StartInfo = startInfo;
			process.Start();
			lock (_processLock)
			{
				_currentProcess = process;
			}
			process.BeginErrorReadLine();
			while (!process.StandardOutput.EndOfStream)
			{
				if (_cancelRequested)
				{
					TryKill(process);
					break;
				}
				string text = process.StandardOutput.ReadLine();
				if (text != null && (text.StartsWith("out_time_us=", StringComparison.Ordinal) || text.StartsWith("out_time_ms=", StringComparison.Ordinal)))
				{
					int num2 = text.IndexOf('=');
					if (num2 >= 0 && long.TryParse(text.Substring(num2 + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
					{
						num = Math.Max(num, result);
					}
				}
			}
			process.WaitForExit();
			lock (_processLock)
			{
				if (_currentProcess == process)
				{
					_currentProcess = null;
				}
			}
			if (!_cancelRequested && process.ExitCode == 0 && num > 50000)
			{
				return (double)num / 1000000.0;
			}
		}
		return Math.Max(0.1, fallbackDuration);
	}

	private int RunFfmpeg(string ffmpeg, string arguments, double durationSeconds, Action<double> progress, out string errorText)
	{
		ProcessStartInfo startInfo = NewProcessInfo(ffmpeg, arguments);
		StringBuilder errors = new StringBuilder();
		using Process process = new Process();
		process.StartInfo = startInfo;
		process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				lock (errors)
				{
					errors.AppendLine(e.Data);
				}
			}
		};
		process.Start();
		lock (_processLock)
		{
			_currentProcess = process;
		}
		process.BeginErrorReadLine();
		while (!process.StandardOutput.EndOfStream)
		{
			if (_cancelRequested)
			{
				TryKill(process);
				break;
			}
			string text = process.StandardOutput.ReadLine();
			if (text == null)
			{
				continue;
			}
			if (text.StartsWith("out_time_ms=", StringComparison.Ordinal))
			{
				if (long.TryParse(text.Substring("out_time_ms=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
				{
					double val = (double)result / 1000000.0 / Math.Max(0.1, durationSeconds);
					progress(Math.Max(0.0, Math.Min(1.0, val)));
				}
			}
			else if (text.Equals("progress=end", StringComparison.Ordinal))
			{
				progress(1.0);
			}
		}
		process.WaitForExit();
		lock (_processLock)
		{
			if (_currentProcess == process)
			{
				_currentProcess = null;
			}
		}
		lock (errors)
		{
			errorText = errors.ToString();
		}
		return _cancelRequested ? (-999) : process.ExitCode;
	}

	private bool ApplyBackgroundMusic(string ffmpeg, string videoPath, string bgmPath, int volumePercent, Action<double> progress, out string error)
	{
		error = null;
		if (string.IsNullOrWhiteSpace(bgmPath) || !File.Exists(bgmPath) || volumePercent <= 0)
		{
			return true;
		}
		VideoInfo videoInfo = Probe(ffmpeg, videoPath);
		if (!videoInfo.HasVideo || videoInfo.DurationSeconds <= 0.0)
		{
			error = "无法读取要添加配乐的视频。";
			return false;
		}
		string text = Path.Combine(Path.GetDirectoryName(videoPath), Path.GetFileNameWithoutExtension(videoPath) + "_bgm_" + Guid.NewGuid().ToString("N") + ".mp4");
		double value = (double)Math.Max(0, Math.Min(100, volumePercent)) / 100.0;
		StringBuilder stringBuilder = new StringBuilder("-hide_banner -y -i ").Append(QuoteArg(videoPath)).Append(" -stream_loop -1 -i ").Append(QuoteArg(bgmPath));
		if (videoInfo.HasAudio)
		{
			string value2 = "[0:a:0]aresample=async=1:first_pts=0,apad=whole_dur=" + FfmpegNumber(videoInfo.DurationSeconds) + ",atrim=duration=" + FfmpegNumber(videoInfo.DurationSeconds) + "[a0];[1:a:0]volume=" + FfmpegNumber(value) + ",atrim=duration=" + FfmpegNumber(videoInfo.DurationSeconds) + ",asetpts=PTS-STARTPTS[bgm];[a0][bgm]amix=inputs=2:duration=first:dropout_transition=2,aresample=async=1:first_pts=0[aout]";
			stringBuilder.Append(" -filter_complex ").Append(QuoteArg(value2)).Append(" -map 0:v:0 -map [aout]");
		}
		else
		{
			string value3 = "[1:a:0]volume=" + FfmpegNumber(value) + ",atrim=duration=" + FfmpegNumber(videoInfo.DurationSeconds) + ",asetpts=PTS-STARTPTS,aresample=async=1:first_pts=0[aout]";
			stringBuilder.Append(" -filter_complex ").Append(QuoteArg(value3)).Append(" -map 0:v:0 -map [aout]");
		}
		stringBuilder.Append(" -c:v copy -c:a aac -b:a 192k -t ").Append(FfmpegNumber(videoInfo.DurationSeconds)).Append(" -movflags +faststart -progress pipe:1 -nostats ")
			.Append(QuoteArg(text));
		if (RunFfmpeg(ffmpeg, stringBuilder.ToString(), videoInfo.DurationSeconds, progress, out error) != 0 || !File.Exists(text))
		{
			TryDelete(text);
			return false;
		}
		if (!ValidateVideoOutput(ffmpeg, text, videoInfo.DurationSeconds, out var reason))
		{
			error = reason;
			TryDelete(text);
			return false;
		}
		try
		{
			File.Copy(text, videoPath, overwrite: true);
			TryDelete(text);
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			TryDelete(text);
			return false;
		}
	}

	private static ProcessStartInfo NewProcessInfo(string ffmpeg, string arguments)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = ffmpeg;
		processStartInfo.Arguments = arguments;
		processStartInfo.UseShellExecute = false;
		processStartInfo.CreateNoWindow = true;
		processStartInfo.RedirectStandardOutput = true;
		processStartInfo.RedirectStandardError = true;
		processStartInfo.StandardOutputEncoding = Encoding.UTF8;
		processStartInfo.StandardErrorEncoding = Encoding.UTF8;
		return processStartInfo;
	}

	private static void WriteConcatList(string listPath, IEnumerable<string> files)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string file in files)
		{
			string value = Path.GetFullPath(file).Replace('\\', '/').Replace("'", "'\\''");
			stringBuilder.Append("file '").Append(value).AppendLine("'");
		}
		File.WriteAllText(listPath, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	private static string FindFfmpeg()
	{
		string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
		if (File.Exists(text))
		{
			return text;
		}
		string text2 = Environment.GetEnvironmentVariable("PATH") ?? "";
		string[] array = text2.Split(Path.PathSeparator);
		foreach (string text3 in array)
		{
			try
			{
				string text4 = text3.Trim().Trim('"');
				if (text4.Length != 0)
				{
					string text5 = Path.Combine(text4, "ffmpeg.exe");
					if (File.Exists(text5))
					{
						return text5;
					}
				}
			}
			catch
			{
			}
		}
		return null;
	}

	private static string FindAvailableOutput(string folder, int index)
	{
		string text = "合并视频_" + index.ToString("000");
		string text2 = Path.Combine(folder, text + ".mp4");
		int num = 2;
		while (File.Exists(text2))
		{
			text2 = Path.Combine(folder, text + "_" + num + ".mp4");
			num++;
		}
		return text2;
	}

	private static string CreateBatchOutputFolder(string parentFolder, string moduleName)
	{
		Directory.CreateDirectory(parentFolder);
		string text = DateTime.Now.ToString("yyyyMMdd") + "_" + moduleName + "_";
		for (int i = 1; i <= 9999; i++)
		{
			string text2 = Path.Combine(parentFolder, text + i.ToString("000"));
			if (!Directory.Exists(text2))
			{
				Directory.CreateDirectory(text2);
				return text2;
			}
		}
		throw new IOException("今天的“" + moduleName + "”输出批次编号已用完，请更换总输出目录。");
	}

	private static string FindAvailableWatermarkOutput(string folder, string source)
	{
		string text = Path.GetFileNameWithoutExtension(source) + "_水印";
		string text2 = Path.Combine(folder, text + ".mp4");
		int num = 2;
		while (File.Exists(text2))
		{
			text2 = Path.Combine(folder, text + "_" + num + ".mp4");
			num++;
		}
		return text2;
	}

	private static string FindAvailableWatermarkImageOutput(string folder, string source)
	{
		string text = Path.GetFileNameWithoutExtension(source) + "_水印";
		string text2 = Path.Combine(folder, text + ".png");
		int num = 2;
		while (File.Exists(text2))
		{
			text2 = Path.Combine(folder, text + "_" + num + ".png");
			num++;
		}
		return text2;
	}

	private static string FindAvailableSegmentPattern(string folder, string source, out string prefix)
	{
		string text = (prefix = Path.GetFileNameWithoutExtension(source).Replace('%', '_'));
		int num = 2;
		while (Directory.GetFiles(folder, prefix + "_片段_*.mp4", SearchOption.TopDirectoryOnly).Length > 0)
		{
			prefix = text + "_" + num;
			num++;
		}
		return Path.Combine(folder, prefix + "_片段_%03d.mp4");
	}

	private void CancelMerge()
	{
		if (!_isRunning)
		{
			return;
		}
		_cancelRequested = true;
		_cancelButton.Enabled = false;
		_splitCancelButton.Enabled = false;
		_watermarkCancelButton.Enabled = false;
		_splitScreenCancelButton.Enabled = false;
		_statusLabel.Text = "正在取消，请稍候…";
		_splitStatusLabel.Text = "正在取消，请稍候…";
		_watermarkStatusLabel.Text = "正在取消，请稍候…";
		_imageStatusLabel.Text = "正在取消，请稍候…";
		_splitScreenStatusLabel.Text = "正在取消，请稍候…";
		lock (_processLock)
		{
			if (_currentProcess != null)
			{
				TryKill(_currentProcess);
			}
		}
	}

	private static string DefaultOutputFolder(string name)
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
		if (string.IsNullOrWhiteSpace(folderPath))
		{
			folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		}
		return Path.Combine(folderPath, name);
	}

	private bool ConfirmParameterReset(string title, string scope)
	{
		if (_isRunning)
		{
			return false;
		}
		return MessageBox.Show(this, "确定把" + scope + "的所有参数恢复为初始值吗？\n\n已导入的视频、拼屏区域素材、成片图片、待加水印原图片、水印图片和 BGM 列表会保留；不会删除任何原始文件。", title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
	}

	private void ResetMergePage()
	{
		if (ConfirmParameterReset("重置批量合并参数", "批量合并页面"))
		{
			ResetMergeParametersCore();
			SaveUserSettings();
			_statusLabel.Text = "批量合并页面的全部参数已恢复；视频列表和当前排列已保留。";
		}
	}

	private void ResetSplitPage()
	{
		if (ConfirmParameterReset("重置视频拆分参数", "视频拆分页面"))
		{
			ResetSplitParametersCore();
			SaveUserSettings();
			_splitStatusLabel.Text = "视频拆分页面的全部参数已恢复；视频列表已保留。";
		}
	}

	private void ResetWatermarkPage()
	{
		if (ConfirmParameterReset("重置水印处理参数", "水印处理页面"))
		{
			ResetWatermarkParametersCore();
			SaveUserSettings();
			_watermarkStatusLabel.Text = "水印参数已恢复；原视频、原图片和已导入的水印图片均已保留。";
		}
	}

	private void ResetImageVideoPage()
	{
		if (ConfirmParameterReset("重置图片成片参数", "图片成片页面"))
		{
			ResetImageVideoParametersCore();
			SaveUserSettings();
			_imageStatusLabel.Text = "图片成片参数已恢复；当前图片列表和排列已保留。";
		}
	}

	private void ResetAllSettings()
	{
		if (ConfirmParameterReset("重置全部设置", "整个软件"))
		{
			ResetMergeParametersCore();
			ResetSplitParametersCore();
			ResetWatermarkParametersCore();
			ResetSplitScreenParametersCore();
			ResetImageVideoParametersCore();
			_copiedVideoAdjustment = null;
			_tabs.SelectedIndex = 0;
			SaveUserSettings();
			_statusLabel.Text = "整个软件的全部参数已恢复；所有已导入素材均已保留。";
			MessageBox.Show(this, "全部参数已恢复为初始值，并已自动保存。\n已导入的视频、拼屏区域素材、成片图片、待加水印原图片、水印图片和 BGM 均未删除。", "重置完成", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private void ResetMergeParametersCore()
	{
		_groupSize.Value = 3m;
		_maxRepeatsPerSource.Value = 0m;
		_maxOutputCount.Value = 0m;
		_combinationStartMode.SelectedIndex = 0;
		_limitGroupDuration.Checked = false;
		_maxGroupDuration.Value = 120m;
		_overlongVideoMode.SelectedIndex = 0;
		_mergeCanvasFitMode.SelectedIndex = 0;
		ResetOutputFrameControls(_mergeOutputFrameControls);
		_similaritySort.Checked = false;
		_similarityMode.SelectedIndex = 1;
		_similarityThreshold.Value = 65m;
		for (int i = 0; i < _transitionEffects.Items.Count; i++)
		{
			_transitionEffects.SetItemChecked(i, value: false);
		}
		_transitionOrder.SelectedIndex = 0;
		_transitionDuration.Value = 1m;
		_transitionLockSingleEffect.Checked = false;
		_autoFallback.Checked = true;
		ResetBgmParameters(_mergeBgm);
		_outputFolder.Text = DefaultOutputFolder("批量合并输出");
		_latestMergeOutputFolder = null;
		ResetVideoAdjustmentParameters(_videos, _mergeVideoAdjustments, _mergeAdjustmentEditor);
		_progressBar.Value = 0;
		UpdateListView(null);
		UpdateMergePlanningUi();
	}

	private void ResetSplitParametersCore()
	{
		_splitMode.SelectedIndex = 0;
		_splitSeconds.Value = 60m;
		_splitTailMode.SelectedIndex = 0;
		_splitEqualParts.Value = 3m;
		ResetOutputFrameControls(_splitOutputFrameControls);
		_splitOutputFolder.Text = DefaultOutputFolder("批量拆分输出");
		ResetBgmParameters(_splitBgm);
		_latestSplitOutputFolder = null;
		ResetVideoAdjustmentParameters(_splitVideos, _splitVideoAdjustments, _splitAdjustmentEditor);
		_splitProgressBar.Value = 0;
		UpdateSplitListView(null);
		UpdateSplitModeUi();
	}

	private void ResetWatermarkParametersCore()
	{
		ClearSplitScreenWatermarkPreview();
		_watermarkOnMerge.Checked = false;
		_watermarkOnSplit.Checked = false;
		_watermarkOnSplitScreen.Checked = false;
		_watermarkSourceTabs.SelectedIndex = 0;
		ResetOutputFrameControls(_watermarkOutputFrameControls);
		for (int i = 0; i < _watermarkStayEffectPool.Items.Count; i++)
		{
			_watermarkStayEffectPool.SetItemChecked(i, value: false);
		}
		for (int j = 0; j < 3; j++)
		{
			_textWatermarkEnabled[j].Checked = false;
			_watermarkText[j].Text = ((j == 0) ? "我的视频" : "");
			_watermarkFontSize[j].Value = 36m;
			_textWatermarkMaxWidth[j].Value = 85m;
			_textWatermarkSafeMargin[j].Value = 3m;
			_textWatermarkAllowOverflow[j].Checked = false;
			_textWatermarkAboveImages[j].Checked = false;
			_watermarkFontFamily[j].SelectedIndex = 0;
			_watermarkFontBold[j].Checked = true;
			_watermarkFontItalic[j].Checked = false;
			ref Color reference = ref _watermarkColor[j];
			reference = Color.White;
			ApplyColorButton(_watermarkColorButton[j], Color.White, chooseReadableText: true);
			_textWatermarkAlignment[j].SelectedIndex = 0;
			_textWatermarkOutlineEnabled[j].Checked = false;
			ref Color reference2 = ref _watermarkOutlineColor[j];
			reference2 = Color.Black;
			ApplyColorButton(_textWatermarkOutlineColorButton[j], Color.Black, chooseReadableText: false);
			_textWatermarkOutlineWidth[j].Value = 2m;
			_textWatermarkOpacity[j].Value = 70m;
			_textWatermarkPosition[j].SelectedIndex = 0;
			_textWatermarkOffsetX[j].Value = 0m;
			_textWatermarkOffsetY[j].Value = 0m;
			_textWatermarkStart[j].Value = 0m;
			_textWatermarkShowUntilEnd[j].Checked = true;
			_textWatermarkEnd[j].Value = 5m;
			_textWatermarkEntryEffect[j].SelectedIndex = 1;
			_textWatermarkEntryDuration[j].Value = 1m;
			_textWatermarkExitEffect[j].SelectedIndex = 1;
			_textWatermarkExitDuration[j].Value = 1m;
			_textWatermarkStayEffect[j].SelectedIndex = 0;
			_textWatermarkStayIntensity[j].Value = 30m;
			_textWatermarkStayPeriod[j].Value = 2m;
			_textWatermarkStayPause[j].Value = 0m;
			_textWatermarkBackgroundEnabled[j].Checked = false;
			_textWatermarkBackgroundStyle[j].SelectedIndex = 0;
			ref Color reference3 = ref _watermarkBackgroundColor[j];
			reference3 = Color.Black;
			ApplyColorButton(_textWatermarkBackgroundColorButton[j], Color.Black, chooseReadableText: false);
			_textWatermarkBackgroundOpacity[j].Value = 55m;
			_textWatermarkBackgroundPaddingX[j].Value = 16m;
			_textWatermarkBackgroundPaddingY[j].Value = 8m;
			_textWatermarkBackgroundRadius[j].Value = 12m;
			_imageWatermarkEditingIndex[j] = -1;
			for (int k = 0; k < _imageWatermarkItems[j].Count; k++)
			{
				string imagePath = _imageWatermarkItems[j][k].ImagePath;
				_imageWatermarkItems[j][k] = NewImageWatermarkSettings(imagePath);
			}
			RefreshImageWatermarkList(j, (_imageWatermarkItems[j].Count <= 0) ? (-1) : 0);
			_imageWatermarkRandomCount[j].Value = 1m;
			_imageWatermarkAssignmentMode[j].SelectedIndex = 0;
			_imageWatermarkPlaybackMode[j].SelectedIndex = 0;
			_imageWatermarkSwitchEffect[j].SelectedIndex = 0;
			_imageWatermarkSwitchDuration[j].Value = 0.6m;
			_imageWatermarkSwitchInterval[j].Value = 0m;
			_imageWatermarkEnabled[j].Checked = false;
		}
		_watermarkOutputFolder.Text = DefaultOutputFolder("水印输出");
		ResetBgmParameters(_watermarkBgm);
		_latestWatermarkOutputFolder = null;
		ResetVideoAdjustmentParameters(_watermarkVideos, _watermarkVideoAdjustments, _watermarkAdjustmentEditor);
		_watermarkProgressBar.Value = 0;
		UpdateWatermarkVideoList(null);
		UpdateWatermarkUi();
	}

	private void ResetImageVideoParametersCore()
	{
		_imageDuration.Value = 3m;
		_imageOutputCount.Value = 1m;
		_imageTargetDuration.Value = 30m;
		_imageResolution.SelectedIndex = 0;
		_imageLayoutPreset.SelectedIndex = 0;
		_imageTileAnimation.SelectedIndex = 1;
		_imageTileAnimationDuration.Value = 0.6m;
		_imageSmartEnhance.Checked = false;
		for (int i = 0; i < _imageLayoutPool.Items.Count; i++)
		{
			_imageLayoutPool.SetItemChecked(i, i == 0);
		}
		for (int j = 0; j < _imageMotionPool.Items.Count; j++)
		{
			_imageMotionPool.SetItemChecked(j, j == 0);
		}
		_imageLayoutOrder.SelectedIndex = 0;
		_imageMotionOrder.SelectedIndex = 0;
		for (int k = 0; k < _imageTransitions.Items.Count; k++)
		{
			_imageTransitions.SetItemChecked(k, value: false);
		}
		_imageTransitionOrder.SelectedIndex = 0;
		_imageTransitionDuration.Value = 1m;
		_imageOutputFolder.Text = DefaultOutputFolder("图片成片输出");
		_latestImageVideoOutputFolder = null;
		ResetBgmParameters(_imageBgm);
		_imageProgressBar.Value = 0;
		UpdateImageLayoutUi();
	}

	private static void ResetBgmParameters(BgmControls controls)
	{
		controls.AssignmentMode.SelectedIndex = 0;
		controls.VolumePercent.Value = 30m;
	}

	private static void ResetOutputFrameControls(OutputFrameControls controls)
	{
		if (controls != null)
		{
			controls.AspectMode.SelectedIndex = 0;
			controls.CropAnchor.SelectedIndex = 0;
			controls.CustomPositionPercent.Value = 50m;
		}
	}

	private static void ResetVideoAdjustmentParameters(List<string> videos, Dictionary<string, VideoAdjustmentSettings> settings, VideoAdjustmentEditor editor)
	{
		LoadVideoAdjustmentEditor(editor, new VideoAdjustmentSettings());
		foreach (string video in videos)
		{
			settings[video] = new VideoAdjustmentSettings();
		}
	}

	private static string GetUserSettingsPath()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("VIDEO_BATCH_STUDIO_SETTINGS_PATH");
		if (!string.IsNullOrWhiteSpace(environmentVariable))
		{
			return Path.GetFullPath(environmentVariable);
		}
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		return Path.Combine(folderPath, "VideoBatchStudio", "settings.dat");
	}

	private void SaveUserSettings()
	{
		try
		{
			Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			PutSetting(values, "format.version", "3");
			PutSetting(values, "ui.tab", _tabs.SelectedIndex);
			SaveNumericSetting(values, "merge.groupSize", _groupSize);
			SaveNumericSetting(values, "merge.maxRepeats", _maxRepeatsPerSource);
			SaveNumericSetting(values, "merge.maxOutputs", _maxOutputCount);
			PutSetting(values, "merge.startMode", _combinationStartMode.SelectedIndex);
			PutSetting(values, "merge.limitDuration", _limitGroupDuration.Checked);
			SaveNumericSetting(values, "merge.duration", _maxGroupDuration);
			PutSetting(values, "merge.overlongMode", _overlongVideoMode.SelectedIndex);
			PutSetting(values, "merge.canvasFitMode", _mergeCanvasFitMode.SelectedIndex);
			SaveOutputFrameSettings(values, "merge.outputFrame.", _mergeOutputFrameControls);
			PutSetting(values, "merge.similarity", _similaritySort.Checked);
			PutSetting(values, "merge.similarityMode", _similarityMode.SelectedIndex);
			SaveNumericSetting(values, "merge.similarityThreshold", _similarityThreshold);
			PutSetting(values, "merge.transitionOrder", _transitionOrder.SelectedIndex);
			SaveNumericSetting(values, "merge.transitionDuration", _transitionDuration);
			PutSetting(values, "merge.transitionLock", _transitionLockSingleEffect.Checked);
			PutSetting(values, "merge.autoFallback", _autoFallback.Checked);
			PutSetting(values, "merge.outputFolder", _outputFolder.Text);
			SaveBgmSettings(values, "merge.bgm.", _mergeBgm);
			PutSetting(values, "merge.transitions", string.Join(",", from int x in _transitionEffects.CheckedIndices
				select x.ToString(CultureInfo.InvariantCulture)));
			PutSetting(values, "split.mode", _splitMode.SelectedIndex);
			SaveNumericSetting(values, "split.seconds", _splitSeconds);
			PutSetting(values, "split.tailMode", _splitTailMode.SelectedIndex);
			SaveNumericSetting(values, "split.equalParts", _splitEqualParts);
			PutSetting(values, "split.outputFolder", _splitOutputFolder.Text);
			SaveOutputFrameSettings(values, "split.outputFrame.", _splitOutputFrameControls);
			SaveBgmSettings(values, "split.bgm.", _splitBgm);
			PutSetting(values, "watermark.onMerge", _watermarkOnMerge.Checked);
			PutSetting(values, "watermark.onSplit", _watermarkOnSplit.Checked);
			PutSetting(values, "watermark.onSplitScreen", _watermarkOnSplitScreen.Checked);
			PutSetting(values, "watermark.outputFolder", _watermarkOutputFolder.Text);
			SaveOutputFrameSettings(values, "watermark.outputFrame.", _watermarkOutputFrameControls);
			PutSetting(values, "watermark.sourceMode", _watermarkSourceTabs.SelectedIndex);
			SaveBgmSettings(values, "watermark.bgm.", _watermarkBgm);
			PutSetting(values, "watermark.stayPool", string.Join(",", from int x in _watermarkStayEffectPool.CheckedIndices
				select x.ToString(CultureInfo.InvariantCulture)));
			for (int num = 0; num < 3; num++)
			{
				string text = "watermark.text." + num + ".";
				PutSetting(values, text + "enabled", _textWatermarkEnabled[num].Checked);
				PutSetting(values, text + "text", _watermarkText[num].Text);
				SaveNumericSetting(values, text + "fontSize", _watermarkFontSize[num]);
				SaveNumericSetting(values, text + "maxWidth", _textWatermarkMaxWidth[num]);
				SaveNumericSetting(values, text + "safeMargin", _textWatermarkSafeMargin[num]);
				PutSetting(values, text + "allowOverflow", _textWatermarkAllowOverflow[num].Checked);
				PutSetting(values, text + "aboveImages", _textWatermarkAboveImages[num].Checked);
				PutSetting(values, text + "fontFamily", ComboText(_watermarkFontFamily[num]));
				PutSetting(values, text + "bold", _watermarkFontBold[num].Checked);
				PutSetting(values, text + "italic", _watermarkFontItalic[num].Checked);
				PutSetting(values, text + "color", _watermarkColor[num].ToArgb());
				PutSetting(values, text + "alignment", ComboText(_textWatermarkAlignment[num]));
				PutSetting(values, text + "outlineEnabled", _textWatermarkOutlineEnabled[num].Checked);
				PutSetting(values, text + "outlineColor", _watermarkOutlineColor[num].ToArgb());
				SaveNumericSetting(values, text + "outlineWidth", _textWatermarkOutlineWidth[num]);
				SaveNumericSetting(values, text + "opacity", _textWatermarkOpacity[num]);
				PutSetting(values, text + "position", ComboText(_textWatermarkPosition[num]));
				SaveNumericSetting(values, text + "offsetX", _textWatermarkOffsetX[num]);
				SaveNumericSetting(values, text + "offsetY", _textWatermarkOffsetY[num]);
				SaveNumericSetting(values, text + "start", _textWatermarkStart[num]);
				PutSetting(values, text + "showUntilEnd", _textWatermarkShowUntilEnd[num].Checked);
				SaveNumericSetting(values, text + "end", _textWatermarkEnd[num]);
				PutSetting(values, text + "entry", ComboText(_textWatermarkEntryEffect[num]));
				SaveNumericSetting(values, text + "entryDuration", _textWatermarkEntryDuration[num]);
				PutSetting(values, text + "exit", ComboText(_textWatermarkExitEffect[num]));
				SaveNumericSetting(values, text + "exitDuration", _textWatermarkExitDuration[num]);
				PutSetting(values, text + "stay", ComboText(_textWatermarkStayEffect[num]));
				SaveNumericSetting(values, text + "stayIntensity", _textWatermarkStayIntensity[num]);
				SaveNumericSetting(values, text + "stayPeriod", _textWatermarkStayPeriod[num]);
				SaveNumericSetting(values, text + "stayPause", _textWatermarkStayPause[num]);
				PutSetting(values, text + "backgroundEnabled", _textWatermarkBackgroundEnabled[num].Checked);
				PutSetting(values, text + "backgroundStyle", ComboText(_textWatermarkBackgroundStyle[num]));
				PutSetting(values, text + "backgroundColor", _watermarkBackgroundColor[num].ToArgb());
				SaveNumericSetting(values, text + "backgroundOpacity", _textWatermarkBackgroundOpacity[num]);
				SaveNumericSetting(values, text + "backgroundPaddingX", _textWatermarkBackgroundPaddingX[num]);
				SaveNumericSetting(values, text + "backgroundPaddingY", _textWatermarkBackgroundPaddingY[num]);
				SaveNumericSetting(values, text + "backgroundRadius", _textWatermarkBackgroundRadius[num]);
			}
			for (int num2 = 0; num2 < 3; num2++)
			{
				SaveImageWatermarkSelection(num2);
				string text2 = "watermark.imageLibrary." + num2 + ".";
				PutSetting(values, text2 + "enabled", _imageWatermarkEnabled[num2].Checked);
				SaveNumericSetting(values, text2 + "candidateCount", _imageWatermarkRandomCount[num2]);
				PutSetting(values, text2 + "assignmentMode", _imageWatermarkAssignmentMode[num2].SelectedIndex);
				PutSetting(values, text2 + "playbackMode", _imageWatermarkPlaybackMode[num2].SelectedIndex);
				PutSetting(values, text2 + "switchEffect", ComboText(_imageWatermarkSwitchEffect[num2]));
				SaveNumericSetting(values, text2 + "switchDuration", _imageWatermarkSwitchDuration[num2]);
				SaveNumericSetting(values, text2 + "switchInterval", _imageWatermarkSwitchInterval[num2]);
				PutSetting(values, text2 + "count", _imageWatermarkItems[num2].Count);
				for (int num3 = 0; num3 < _imageWatermarkItems[num2].Count; num3++)
				{
					SaveImageWatermarkSetting(values, text2 + "item." + num3 + ".", _imageWatermarkItems[num2][num3]);
				}
			}
			SaveVideoAdjustmentSetting(values, "adjustment.merge.", CaptureVideoAdjustmentEditor(_mergeAdjustmentEditor));
			SaveVideoAdjustmentSetting(values, "adjustment.split.", CaptureVideoAdjustmentEditor(_splitAdjustmentEditor));
			SaveVideoAdjustmentSetting(values, "adjustment.watermark.", CaptureVideoAdjustmentEditor(_watermarkAdjustmentEditor));
			SaveNumericSetting(values, "imageVideo.duration", _imageDuration);
			SaveNumericSetting(values, "imageVideo.outputCount", _imageOutputCount);
			SaveNumericSetting(values, "imageVideo.targetDuration", _imageTargetDuration);
			PutSetting(values, "imageVideo.resolution", _imageResolution.SelectedIndex);
			PutSetting(values, "imageVideo.layout", _imageLayoutPreset.SelectedIndex);
			PutSetting(values, "imageVideo.tileAnimation", _imageTileAnimation.SelectedIndex);
			SaveNumericSetting(values, "imageVideo.tileAnimationDuration", _imageTileAnimationDuration);
			PutSetting(values, "imageVideo.smartEnhance", _imageSmartEnhance.Checked);
			PutSetting(values, "imageVideo.layoutOrder", _imageLayoutOrder.SelectedIndex);
			PutSetting(values, "imageVideo.motionOrder", _imageMotionOrder.SelectedIndex);
			PutSetting(values, "imageVideo.layoutPool", string.Join(",", from int x in _imageLayoutPool.CheckedIndices
				select x.ToString(CultureInfo.InvariantCulture)));
			PutSetting(values, "imageVideo.motionPool", string.Join(",", from int x in _imageMotionPool.CheckedIndices
				select x.ToString(CultureInfo.InvariantCulture)));
			PutSetting(values, "imageVideo.transitionOrder", _imageTransitionOrder.SelectedIndex);
			SaveNumericSetting(values, "imageVideo.transitionDuration", _imageTransitionDuration);
			PutSetting(values, "imageVideo.outputFolder", _imageOutputFolder.Text);
			PutSetting(values, "imageVideo.transitions", string.Join(",", from int x in _imageTransitions.CheckedIndices
				select x.ToString(CultureInfo.InvariantCulture)));
			SaveBgmSettings(values, "imageVideo.bgm.", _imageBgm);
			PutSetting(values, "splitScreen.layout", (_activeSplitScreenLayout == null) ? "top_half" : _activeSplitScreenLayout.Id);
			PutSetting(values, "splitScreen.canvas", _splitScreenCanvas.SelectedIndex);
			PutSetting(values, "splitScreen.durationMode", _splitScreenDurationMode.SelectedIndex);
			SaveNumericSetting(values, "splitScreen.duration", _splitScreenDuration);
			SaveNumericSetting(values, "splitScreen.outputCount", _splitScreenOutputCount);
			PutSetting(values, "splitScreen.assignment", _splitScreenAssignmentMode.SelectedIndex);
			PutSetting(values, "splitScreen.audio", _splitScreenAudioRegion.SelectedIndex);
			PutSetting(values, "splitScreen.borderPreset", _splitScreenBorderPreset.SelectedIndex);
			PutSetting(values, "splitScreen.borderScope", _splitScreenBorderScope.SelectedIndex);
			SaveNumericSetting(values, "splitScreen.borderWidth", _splitScreenBorderWidth);
			PutSetting(values, "splitScreen.borderColor", _splitScreenBorderColor.ToArgb());
			PutSetting(values, "splitScreen.pipShape", _splitScreenPipShape.SelectedIndex);
			PutSetting(values, "splitScreen.pipAspect", _splitScreenPipAspect.SelectedIndex);
			PutSetting(values, "splitScreen.pipPosition", _splitScreenPipPosition.SelectedIndex);
			SaveNumericSetting(values, "splitScreen.pipSize", _splitScreenPipSize);
			SaveNumericSetting(values, "splitScreen.pipOffsetX", _splitScreenPipOffsetX);
			SaveNumericSetting(values, "splitScreen.pipOffsetY", _splitScreenPipOffsetY);
			PutSetting(values, "splitScreen.outputFolder", _splitScreenOutputFolder.Text);
			SaveBgmSettings(values, "splitScreen.bgm.", _splitScreenBgm);
			foreach (SplitScreenLayoutDefinition splitScreenLayout in _splitScreenLayouts)
			{
				EnsureSplitScreenLayoutSettings(splitScreenLayout);
				string text3 = "splitScreen.layoutState." + splitScreenLayout.Id + ".";
				bool[] enabled = _splitScreenEnabledRegionsByLayout[splitScreenLayout.Id];
				bool[] border = _splitScreenBorderRegionsByLayout[splitScreenLayout.Id];
				PutSetting(values, text3 + "main", _splitScreenMainRegionByLayout[splitScreenLayout.Id]);
				PutSetting(values, text3 + "enabled", string.Join(",", from i in Enumerable.Range(0, splitScreenLayout.RegionCount)
					where enabled[i]
					select i.ToString(CultureInfo.InvariantCulture)));
				PutSetting(values, text3 + "border", string.Join(",", from i in Enumerable.Range(0, splitScreenLayout.RegionCount)
					where border[i]
					select i.ToString(CultureInfo.InvariantCulture)));
				SplitScreenRegionSettings[] array = _splitScreenRegionSettingsByLayout[splitScreenLayout.Id];
				for (int num4 = 0; num4 < splitScreenLayout.RegionCount; num4++)
				{
					string text4 = text3 + "region." + num4 + ".";
					PutSetting(values, text4 + "pipSize", array[num4].PipSizePercent);
					PutSetting(values, text4 + "pipX", array[num4].PipOffsetXPercent);
					PutSetting(values, text4 + "pipY", array[num4].PipOffsetYPercent);
					PutSetting(values, text4 + "pipShape", array[num4].PipShape);
					PutSetting(values, text4 + "pipAspect", array[num4].PipAspect);
					PutSetting(values, text4 + "pipPosition", array[num4].PipPosition);
					PutSetting(values, text4 + "volume", array[num4].VolumePercent);
					PutSetting(values, text4 + "borderPreset", array[num4].BorderPresetIndex);
					PutSetting(values, text4 + "borderWidth", array[num4].BorderWidth);
					PutSetting(values, text4 + "borderColor", array[num4].BorderColor.ToArgb());
				}
			}
			WriteSettingsFile(GetUserSettingsPath(), values);
		}
		catch
		{
		}
	}

	private bool LoadUserSettings()
	{
		string userSettingsPath = GetUserSettingsPath();
		if (!File.Exists(userSettingsPath))
		{
			return false;
		}
		try
		{
			Dictionary<string, string> values = ReadSettingsFile(userSettingsPath);
			int num = GetIntSetting(values, "ui.tab", 0);
			if (GetIntSetting(values, "format.version", 1) < 2 && num >= 3)
			{
				num++;
			}
			if (num >= 0 && num < _tabs.TabPages.Count)
			{
				_tabs.SelectedIndex = num;
			}
			LoadNumericSetting(values, "merge.groupSize", _groupSize);
			LoadNumericSetting(values, "merge.maxRepeats", _maxRepeatsPerSource);
			LoadNumericSetting(values, "merge.maxOutputs", _maxOutputCount);
			LoadComboIndexSetting(values, "merge.startMode", _combinationStartMode);
			LoadCheckSetting(values, "merge.limitDuration", _limitGroupDuration);
			LoadNumericSetting(values, "merge.duration", _maxGroupDuration);
			LoadComboIndexSetting(values, "merge.overlongMode", _overlongVideoMode);
			LoadComboIndexSetting(values, "merge.canvasFitMode", _mergeCanvasFitMode);
			LoadOutputFrameSettings(values, "merge.outputFrame.", _mergeOutputFrameControls);
			LoadCheckSetting(values, "merge.similarity", _similaritySort);
			LoadComboIndexSetting(values, "merge.similarityMode", _similarityMode);
			LoadNumericSetting(values, "merge.similarityThreshold", _similarityThreshold);
			LoadComboIndexSetting(values, "merge.transitionOrder", _transitionOrder);
			LoadNumericSetting(values, "merge.transitionDuration", _transitionDuration);
			LoadCheckSetting(values, "merge.transitionLock", _transitionLockSingleEffect);
			LoadCheckSetting(values, "merge.autoFallback", _autoFallback);
			LoadTextSetting(values, "merge.outputFolder", _outputFolder);
			LoadBgmSettings(values, "merge.bgm.", _mergeBgm);
			string setting = GetSetting(values, "merge.transitions", null);
			if (setting != null)
			{
				HashSet<int> hashSet = new HashSet<int>();
				string[] array = setting.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string s in array)
				{
					if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
					{
						hashSet.Add(result);
					}
				}
				for (int j = 0; j < _transitionEffects.Items.Count; j++)
				{
					_transitionEffects.SetItemChecked(j, hashSet.Contains(j));
				}
			}
			LoadComboIndexSetting(values, "split.mode", _splitMode);
			LoadNumericSetting(values, "split.seconds", _splitSeconds);
			LoadComboIndexSetting(values, "split.tailMode", _splitTailMode);
			LoadNumericSetting(values, "split.equalParts", _splitEqualParts);
			LoadTextSetting(values, "split.outputFolder", _splitOutputFolder);
			LoadOutputFrameSettings(values, "split.outputFrame.", _splitOutputFrameControls);
			LoadBgmSettings(values, "split.bgm.", _splitBgm);
			LoadCheckSetting(values, "watermark.onMerge", _watermarkOnMerge);
			LoadCheckSetting(values, "watermark.onSplit", _watermarkOnSplit);
			LoadCheckSetting(values, "watermark.onSplitScreen", _watermarkOnSplitScreen);
			LoadTextSetting(values, "watermark.outputFolder", _watermarkOutputFolder);
			LoadOutputFrameSettings(values, "watermark.outputFrame.", _watermarkOutputFrameControls);
			LoadComboIndexSetting(values, "watermark.sourceMode", _watermarkSourceTabs);
			LoadBgmSettings(values, "watermark.bgm.", _watermarkBgm);
			string setting2 = GetSetting(values, "watermark.stayPool", null);
			if (setting2 != null)
			{
				HashSet<int> hashSet2 = new HashSet<int>();
				string[] array2 = setting2.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string s2 in array2)
				{
					if (int.TryParse(s2, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
					{
						hashSet2.Add(result2);
					}
				}
				for (int k = 0; k < _watermarkStayEffectPool.Items.Count; k++)
				{
					_watermarkStayEffectPool.SetItemChecked(k, hashSet2.Contains(k));
				}
			}
			for (int l = 0; l < 3; l++)
			{
				string text = "watermark.text." + l + ".";
				LoadCheckSetting(values, text + "enabled", _textWatermarkEnabled[l]);
				LoadTextSetting(values, text + "text", _watermarkText[l]);
				LoadNumericSetting(values, text + "fontSize", _watermarkFontSize[l]);
				LoadNumericSetting(values, text + "maxWidth", _textWatermarkMaxWidth[l]);
				LoadNumericSetting(values, text + "safeMargin", _textWatermarkSafeMargin[l]);
				LoadCheckSetting(values, text + "allowOverflow", _textWatermarkAllowOverflow[l]);
				LoadCheckSetting(values, text + "aboveImages", _textWatermarkAboveImages[l]);
				LoadComboTextSetting(values, text + "fontFamily", _watermarkFontFamily[l]);
				LoadCheckSetting(values, text + "bold", _watermarkFontBold[l]);
				LoadCheckSetting(values, text + "italic", _watermarkFontItalic[l]);
				ref Color reference = ref _watermarkColor[l];
				reference = Color.FromArgb(GetIntSetting(values, text + "color", _watermarkColor[l].ToArgb()));
				ApplyColorButton(_watermarkColorButton[l], _watermarkColor[l], chooseReadableText: true);
				LoadComboTextSetting(values, text + "alignment", _textWatermarkAlignment[l]);
				LoadCheckSetting(values, text + "outlineEnabled", _textWatermarkOutlineEnabled[l]);
				ref Color reference2 = ref _watermarkOutlineColor[l];
				reference2 = Color.FromArgb(GetIntSetting(values, text + "outlineColor", _watermarkOutlineColor[l].ToArgb()));
				ApplyColorButton(_textWatermarkOutlineColorButton[l], _watermarkOutlineColor[l], chooseReadableText: false);
				LoadNumericSetting(values, text + "outlineWidth", _textWatermarkOutlineWidth[l]);
				LoadNumericSetting(values, text + "opacity", _textWatermarkOpacity[l]);
				LoadComboTextSetting(values, text + "position", _textWatermarkPosition[l]);
				LoadNumericSetting(values, text + "offsetX", _textWatermarkOffsetX[l]);
				LoadNumericSetting(values, text + "offsetY", _textWatermarkOffsetY[l]);
				LoadNumericSetting(values, text + "start", _textWatermarkStart[l]);
				LoadCheckSetting(values, text + "showUntilEnd", _textWatermarkShowUntilEnd[l]);
				LoadNumericSetting(values, text + "end", _textWatermarkEnd[l]);
				LoadComboTextSetting(values, text + "entry", _textWatermarkEntryEffect[l]);
				LoadNumericSetting(values, text + "entryDuration", _textWatermarkEntryDuration[l]);
				LoadComboTextSetting(values, text + "exit", _textWatermarkExitEffect[l]);
				LoadNumericSetting(values, text + "exitDuration", _textWatermarkExitDuration[l]);
				LoadComboTextSetting(values, text + "stay", _textWatermarkStayEffect[l]);
				LoadNumericSetting(values, text + "stayIntensity", _textWatermarkStayIntensity[l]);
				LoadNumericSetting(values, text + "stayPeriod", _textWatermarkStayPeriod[l]);
				LoadNumericSetting(values, text + "stayPause", _textWatermarkStayPause[l]);
				LoadCheckSetting(values, text + "backgroundEnabled", _textWatermarkBackgroundEnabled[l]);
				LoadComboTextSetting(values, text + "backgroundStyle", _textWatermarkBackgroundStyle[l]);
				ref Color reference3 = ref _watermarkBackgroundColor[l];
				reference3 = Color.FromArgb(GetIntSetting(values, text + "backgroundColor", _watermarkBackgroundColor[l].ToArgb()));
				ApplyColorButton(_textWatermarkBackgroundColorButton[l], _watermarkBackgroundColor[l], chooseReadableText: false);
				LoadNumericSetting(values, text + "backgroundOpacity", _textWatermarkBackgroundOpacity[l]);
				LoadNumericSetting(values, text + "backgroundPaddingX", _textWatermarkBackgroundPaddingX[l]);
				LoadNumericSetting(values, text + "backgroundPaddingY", _textWatermarkBackgroundPaddingY[l]);
				LoadNumericSetting(values, text + "backgroundRadius", _textWatermarkBackgroundRadius[l]);
			}
			for (int m = 0; m < 3; m++)
			{
				string text2 = "watermark.imageLibrary." + m + ".";
				bool boolSetting = GetBoolSetting(values, text2 + "enabled", fallback: false);
				int num2 = Math.Max(0, Math.Min(2000, GetIntSetting(values, text2 + "count", 0)));
				_imageWatermarkEditingIndex[m] = -1;
				_imageWatermarkItems[m].Clear();
				for (int n = 0; n < num2; n++)
				{
					WatermarkSettings watermarkSettings = LoadImageWatermarkSetting(values, text2 + "item." + n + ".");
					if (watermarkSettings != null && File.Exists(watermarkSettings.ImagePath))
					{
						_imageWatermarkItems[m].Add(watermarkSettings);
					}
				}
				RefreshImageWatermarkList(m, (_imageWatermarkItems[m].Count <= 0) ? (-1) : 0);
				LoadNumericSetting(values, text2 + "candidateCount", _imageWatermarkRandomCount[m]);
				LoadComboIndexSetting(values, text2 + "assignmentMode", _imageWatermarkAssignmentMode[m]);
				LoadComboIndexSetting(values, text2 + "playbackMode", _imageWatermarkPlaybackMode[m]);
				LoadComboTextSetting(values, text2 + "switchEffect", _imageWatermarkSwitchEffect[m]);
				LoadNumericSetting(values, text2 + "switchDuration", _imageWatermarkSwitchDuration[m]);
				LoadNumericSetting(values, text2 + "switchInterval", _imageWatermarkSwitchInterval[m]);
				_imageWatermarkEnabled[m].Checked = boolSetting && _imageWatermarkItems[m].Count > 0;
			}
			LoadVideoAdjustmentSetting(values, "adjustment.merge.", _mergeAdjustmentEditor);
			LoadVideoAdjustmentSetting(values, "adjustment.split.", _splitAdjustmentEditor);
			LoadVideoAdjustmentSetting(values, "adjustment.watermark.", _watermarkAdjustmentEditor);
			LoadNumericSetting(values, "imageVideo.duration", _imageDuration);
			LoadNumericSetting(values, "imageVideo.outputCount", _imageOutputCount);
			LoadNumericSetting(values, "imageVideo.targetDuration", _imageTargetDuration);
			LoadComboIndexSetting(values, "imageVideo.resolution", _imageResolution);
			LoadComboIndexSetting(values, "imageVideo.layout", _imageLayoutPreset);
			LoadComboIndexSetting(values, "imageVideo.tileAnimation", _imageTileAnimation);
			LoadNumericSetting(values, "imageVideo.tileAnimationDuration", _imageTileAnimationDuration);
			LoadCheckSetting(values, "imageVideo.smartEnhance", _imageSmartEnhance);
			LoadComboIndexSetting(values, "imageVideo.layoutOrder", _imageLayoutOrder);
			LoadComboIndexSetting(values, "imageVideo.motionOrder", _imageMotionOrder);
			string setting3 = GetSetting(values, "imageVideo.layoutPool", null);
			if (setting3 != null)
			{
				LoadCheckedIndices(_imageLayoutPool, setting3);
			}
			else
			{
				int[] array3 = new int[8] { 0, 1, 3, 4, 5, 6, 7, 8 };
				int num3 = array3[Math.Min(Math.Max(0, _imageLayoutPreset.SelectedIndex), array3.Length - 1)];
				for (int num4 = 0; num4 < _imageLayoutPool.Items.Count; num4++)
				{
					_imageLayoutPool.SetItemChecked(num4, num4 == num3);
				}
			}
			string setting4 = GetSetting(values, "imageVideo.motionPool", null);
			if (setting4 != null)
			{
				LoadCheckedIndices(_imageMotionPool, setting4);
			}
			else
			{
				string value = ((_imageTileAnimation.SelectedItem == null) ? "轻微放大" : _imageTileAnimation.SelectedItem.ToString());
				int num5 = Math.Max(0, _imageMotionPool.Items.IndexOf(value));
				for (int num6 = 0; num6 < _imageMotionPool.Items.Count; num6++)
				{
					_imageMotionPool.SetItemChecked(num6, num6 == num5);
				}
			}
			LoadComboIndexSetting(values, "imageVideo.transitionOrder", _imageTransitionOrder);
			LoadNumericSetting(values, "imageVideo.transitionDuration", _imageTransitionDuration);
			LoadTextSetting(values, "imageVideo.outputFolder", _imageOutputFolder);
			string setting5 = GetSetting(values, "imageVideo.transitions", null);
			if (setting5 != null)
			{
				HashSet<int> hashSet3 = new HashSet<int>();
				string[] array = setting5.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string s3 in array)
				{
					if (int.TryParse(s3, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result3))
					{
						hashSet3.Add(result3);
					}
				}
				for (int num7 = 0; num7 < _imageTransitions.Items.Count; num7++)
				{
					_imageTransitions.SetItemChecked(num7, hashSet3.Contains(num7));
				}
			}
			LoadBgmSettings(values, "imageVideo.bgm.", _imageBgm);
			string splitScreenLayoutId = GetSetting(values, "splitScreen.layout", "top_half");
			SplitScreenLayoutDefinition splitScreenLayoutDefinition = _splitScreenLayouts.FirstOrDefault((SplitScreenLayoutDefinition x) => string.Equals(x.Id, splitScreenLayoutId, StringComparison.OrdinalIgnoreCase)) ?? _splitScreenLayouts.FirstOrDefault();
			LoadComboIndexSetting(values, "splitScreen.canvas", _splitScreenCanvas);
			LoadComboIndexSetting(values, "splitScreen.durationMode", _splitScreenDurationMode);
			LoadNumericSetting(values, "splitScreen.duration", _splitScreenDuration);
			LoadNumericSetting(values, "splitScreen.outputCount", _splitScreenOutputCount);
			LoadComboIndexSetting(values, "splitScreen.assignment", _splitScreenAssignmentMode);
			LoadComboIndexSetting(values, "splitScreen.borderPreset", _splitScreenBorderPreset);
			LoadComboIndexSetting(values, "splitScreen.borderScope", _splitScreenBorderScope);
			LoadNumericSetting(values, "splitScreen.borderWidth", _splitScreenBorderWidth);
			_splitScreenBorderColor = Color.FromArgb(GetIntSetting(values, "splitScreen.borderColor", Color.White.ToArgb()));
			LoadComboIndexSetting(values, "splitScreen.pipShape", _splitScreenPipShape);
			LoadComboIndexSetting(values, "splitScreen.pipAspect", _splitScreenPipAspect);
			LoadComboIndexSetting(values, "splitScreen.pipPosition", _splitScreenPipPosition);
			LoadNumericSetting(values, "splitScreen.pipSize", _splitScreenPipSize);
			LoadNumericSetting(values, "splitScreen.pipOffsetX", _splitScreenPipOffsetX);
			LoadNumericSetting(values, "splitScreen.pipOffsetY", _splitScreenPipOffsetY);
			LoadTextSetting(values, "splitScreen.outputFolder", _splitScreenOutputFolder);
			LoadBgmSettings(values, "splitScreen.bgm.", _splitScreenBgm);
			foreach (SplitScreenLayoutDefinition splitScreenLayout in _splitScreenLayouts)
			{
				EnsureSplitScreenLayoutSettings(splitScreenLayout);
				string text3 = "splitScreen.layoutState." + splitScreenLayout.Id + ".";
				bool[] array4 = _splitScreenEnabledRegionsByLayout[splitScreenLayout.Id];
				bool[] array5 = _splitScreenBorderRegionsByLayout[splitScreenLayout.Id];
				string setting6 = GetSetting(values, text3 + "enabled", null);
				if (setting6 != null)
				{
					HashSet<int> hashSet4 = ParseSplitScreenIndices(setting6);
					for (int num8 = 0; num8 < splitScreenLayout.RegionCount; num8++)
					{
						array4[num8] = hashSet4.Contains(num8);
					}
					if (!array4.Take(splitScreenLayout.RegionCount).Any((bool x) => x))
					{
						array4[0] = true;
					}
				}
				string setting7 = GetSetting(values, text3 + "border", null);
				if (setting7 != null)
				{
					HashSet<int> hashSet5 = ParseSplitScreenIndices(setting7);
					for (int num9 = 0; num9 < splitScreenLayout.RegionCount; num9++)
					{
						array5[num9] = hashSet5.Contains(num9);
					}
				}
				int num10 = GetIntSetting(values, text3 + "main", _splitScreenMainRegionByLayout[splitScreenLayout.Id]);
				if (num10 < 0 || num10 >= splitScreenLayout.RegionCount || !array4[num10])
				{
					num10 = Array.FindIndex(array4, 0, splitScreenLayout.RegionCount, (bool x) => x);
				}
				_splitScreenMainRegionByLayout[splitScreenLayout.Id] = Math.Max(0, num10);
				SplitScreenRegionSettings[] array6 = _splitScreenRegionSettingsByLayout[splitScreenLayout.Id];
				for (int num11 = 0; num11 < splitScreenLayout.RegionCount; num11++)
				{
					string text4 = text3 + "region." + num11 + ".";
					array6[num11].PipSizePercent = GetIntSetting(values, text4 + "pipSize", array6[num11].PipSizePercent);
					array6[num11].PipOffsetXPercent = GetIntSetting(values, text4 + "pipX", array6[num11].PipOffsetXPercent);
					array6[num11].PipOffsetYPercent = GetIntSetting(values, text4 + "pipY", array6[num11].PipOffsetYPercent);
					array6[num11].PipShape = GetSetting(values, text4 + "pipShape", array6[num11].PipShape);
					array6[num11].PipAspect = GetSetting(values, text4 + "pipAspect", array6[num11].PipAspect);
					array6[num11].PipPosition = GetSetting(values, text4 + "pipPosition", array6[num11].PipPosition);
					array6[num11].VolumePercent = Math.Max(0, Math.Min(300, GetIntSetting(values, text4 + "volume", array6[num11].VolumePercent)));
					string setting8 = GetSetting(values, text4 + "borderWidth", null);
					if (setting8 != null)
					{
						array6[num11].BorderPresetIndex = Math.Max(0, Math.Min(4, GetIntSetting(values, text4 + "borderPreset", 1)));
						array6[num11].BorderWidth = Math.Max(0, Math.Min(80, GetIntSetting(values, text4 + "borderWidth", 0)));
						array6[num11].BorderColor = Color.FromArgb(GetIntSetting(values, text4 + "borderColor", Color.Transparent.ToArgb()));
					}
					else if (array5[num11])
					{
						array6[num11].BorderPresetIndex = _splitScreenBorderPreset.SelectedIndex;
						array6[num11].BorderWidth = decimal.ToInt32(_splitScreenBorderWidth.Value);
						array6[num11].BorderColor = _splitScreenBorderColor;
					}
				}
			}
			if (splitScreenLayoutDefinition != null)
			{
				ListView listView = ((splitScreenLayoutDefinition.Category == "多格拼屏") ? _splitScreenGridPresets : ((splitScreenLayoutDefinition.Category == "画中画") ? _splitScreenPipPresets : _splitScreenQuickPresets));
				SelectSplitScreenLayout(splitScreenLayoutDefinition, listView);
				foreach (ListViewItem item in listView.Items)
				{
					if (item.Tag is SplitScreenLayoutDefinition splitScreenLayoutDefinition2 && splitScreenLayoutDefinition2.Id == splitScreenLayoutDefinition.Id)
					{
						item.Selected = true;
						item.EnsureVisible();
						break;
					}
				}
			}
			int intSetting = GetIntSetting(values, "splitScreen.audio", 0);
			if (intSetting >= 0 && intSetting < _splitScreenAudioRegion.Items.Count)
			{
				_splitScreenAudioRegion.SelectedIndex = intSetting;
			}
			UpdateSplitScreenBorderColorButton();
			RefreshSplitScreenBgmList();
			UpdateSplitScreenPlanningUi();
			UpdateSplitScreenPipUi();
			RefreshSplitScreenPreview();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void SaveImageWatermarkSetting(Dictionary<string, string> values, string prefix, WatermarkSettings item)
	{
		PutSetting(values, prefix + "path", item.ImagePath);
		PutSetting(values, prefix + "width", item.ImageWidthPercent);
		PutSetting(values, prefix + "opacity", item.Opacity);
		PutSetting(values, prefix + "position", item.Position);
		PutSetting(values, prefix + "offsetX", item.OffsetX);
		PutSetting(values, prefix + "offsetY", item.OffsetY);
		PutSetting(values, prefix + "allowOverflow", item.AllowOverflow);
		PutSetting(values, prefix + "start", item.StartSeconds);
		PutSetting(values, prefix + "showUntilEnd", item.ShowUntilEnd);
		PutSetting(values, prefix + "end", item.EndSeconds);
		PutSetting(values, prefix + "entry", item.EntryEffect);
		PutSetting(values, prefix + "entryDuration", item.EntryDurationSeconds);
		PutSetting(values, prefix + "exit", item.ExitEffect);
		PutSetting(values, prefix + "exitDuration", item.ExitDurationSeconds);
		PutSetting(values, prefix + "stay", item.StayEffect);
		PutSetting(values, prefix + "stayIntensity", item.StayIntensity);
		PutSetting(values, prefix + "stayPeriod", item.StayPeriodSeconds);
		PutSetting(values, prefix + "stayPause", item.StayPauseSeconds);
		PutSetting(values, prefix + "slideDuration", item.SlideDurationSeconds);
	}

	private static WatermarkSettings LoadImageWatermarkSetting(Dictionary<string, string> values, string prefix)
	{
		string setting = GetSetting(values, prefix + "path", "");
		if (string.IsNullOrWhiteSpace(setting))
		{
			return null;
		}
		WatermarkSettings watermarkSettings = new WatermarkSettings();
		watermarkSettings.Enabled = true;
		watermarkSettings.IsText = false;
		watermarkSettings.ImagePath = setting;
		watermarkSettings.ImageWidthPercent = GetIntSetting(values, prefix + "width", 20);
		watermarkSettings.Opacity = GetDoubleSetting(values, prefix + "opacity", 0.7);
		watermarkSettings.Position = GetSetting(values, prefix + "position", "右下角");
		watermarkSettings.OffsetX = GetIntSetting(values, prefix + "offsetX", 0);
		watermarkSettings.OffsetY = GetIntSetting(values, prefix + "offsetY", 0);
		watermarkSettings.AllowOverflow = GetBoolSetting(values, prefix + "allowOverflow", fallback: false);
		watermarkSettings.StartSeconds = GetDoubleSetting(values, prefix + "start", 0.0);
		watermarkSettings.ShowUntilEnd = GetBoolSetting(values, prefix + "showUntilEnd", fallback: true);
		watermarkSettings.EndSeconds = GetDoubleSetting(values, prefix + "end", 5.0);
		watermarkSettings.EntryEffect = GetSetting(values, prefix + "entry", "淡入");
		watermarkSettings.EntryDurationSeconds = GetDoubleSetting(values, prefix + "entryDuration", 1.0);
		watermarkSettings.ExitEffect = GetSetting(values, prefix + "exit", "淡出");
		watermarkSettings.ExitDurationSeconds = GetDoubleSetting(values, prefix + "exitDuration", 1.0);
		watermarkSettings.StayEffect = GetSetting(values, prefix + "stay", "无");
		watermarkSettings.StayIntensity = GetDoubleSetting(values, prefix + "stayIntensity", 0.3);
		watermarkSettings.StayPeriodSeconds = GetDoubleSetting(values, prefix + "stayPeriod", 2.0);
		watermarkSettings.StayPauseSeconds = GetDoubleSetting(values, prefix + "stayPause", 0.0);
		watermarkSettings.SlideDurationSeconds = GetDoubleSetting(values, prefix + "slideDuration", 3.0);
		return watermarkSettings;
	}

	private static void SaveVideoAdjustmentSetting(Dictionary<string, string> values, string prefix, VideoAdjustmentSettings adjustment)
	{
		PutSetting(values, prefix + "flip", adjustment.HorizontalFlip);
		PutSetting(values, prefix + "reverse", adjustment.ReversePlayback);
		PutSetting(values, prefix + "centerCropPortrait", adjustment.CenterCropPortrait);
		PutSetting(values, prefix + "scale", adjustment.ScaleRatio);
		PutSetting(values, prefix + "speed", adjustment.SpeedRatio);
		PutSetting(values, prefix + "volume", adjustment.VolumePercent);
	}

	private static void SaveOutputFrameSettings(Dictionary<string, string> values, string prefix, OutputFrameControls controls)
	{
		OutputFrameSettings outputFrameSettings = CaptureOutputFrameSettings(controls);
		PutSetting(values, prefix + "aspect", outputFrameSettings.AspectMode);
		PutSetting(values, prefix + "anchor", outputFrameSettings.CropAnchor);
		PutSetting(values, prefix + "customPosition", outputFrameSettings.CustomPositionPercent);
	}

	private static void LoadOutputFrameSettings(Dictionary<string, string> values, string prefix, OutputFrameControls controls)
	{
		if (controls != null)
		{
			LoadComboIndexSetting(values, prefix + "aspect", controls.AspectMode);
			LoadComboIndexSetting(values, prefix + "anchor", controls.CropAnchor);
			LoadNumericSetting(values, prefix + "customPosition", controls.CustomPositionPercent);
		}
	}

	private static void SaveBgmSettings(Dictionary<string, string> values, string prefix, BgmControls controls)
	{
		PutSetting(values, prefix + "assignment", controls.AssignmentMode.SelectedIndex);
		PutSetting(values, prefix + "volume", controls.VolumePercent.Value);
		PutSetting(values, prefix + "count", controls.Files.Count);
		for (int i = 0; i < controls.Files.Count; i++)
		{
			PutSetting(values, prefix + "file." + i, controls.Files[i]);
		}
	}

	private static void LoadBgmSettings(Dictionary<string, string> values, string prefix, BgmControls controls)
	{
		controls.Files.Clear();
		int num = Math.Max(0, Math.Min(500, GetIntSetting(values, prefix + "count", 0)));
		for (int i = 0; i < num; i++)
		{
			string setting = GetSetting(values, prefix + "file." + i, "");
			if (!string.IsNullOrWhiteSpace(setting) && File.Exists(setting))
			{
				controls.Files.Add(setting);
			}
		}
		LoadComboIndexSetting(values, prefix + "assignment", controls.AssignmentMode);
		LoadNumericSetting(values, prefix + "volume", controls.VolumePercent);
		UpdateBgmCount(controls);
	}

	private static void LoadVideoAdjustmentSetting(Dictionary<string, string> values, string prefix, VideoAdjustmentEditor editor)
	{
		LoadVideoAdjustmentEditor(editor, new VideoAdjustmentSettings
		{
			HorizontalFlip = GetBoolSetting(values, prefix + "flip", editor.HorizontalFlip.Checked),
			ReversePlayback = GetBoolSetting(values, prefix + "reverse", editor.ReversePlayback.Checked),
			CenterCropPortrait = GetBoolSetting(values, prefix + "centerCropPortrait", editor.CenterCropPortrait.Checked),
			ScaleRatio = GetDoubleSetting(values, prefix + "scale", decimal.ToDouble(editor.ScaleRatio.Value)),
			SpeedRatio = GetDoubleSetting(values, prefix + "speed", decimal.ToDouble(editor.SpeedRatio.Value)),
			VolumePercent = GetIntSetting(values, prefix + "volume", decimal.ToInt32(editor.VolumePercent.Value))
		});
	}

	private static void PutSetting(Dictionary<string, string> values, string key, object value)
	{
		if (value is IFormattable)
		{
			values[key] = ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
		}
		else
		{
			values[key] = ((value == null) ? "" : value.ToString());
		}
	}

	private static string GetSetting(Dictionary<string, string> values, string key, string fallback)
	{
		if (!values.TryGetValue(key, out var value))
		{
			return fallback;
		}
		return value;
	}

	private static bool GetBoolSetting(Dictionary<string, string> values, string key, bool fallback)
	{
		string setting = GetSetting(values, key, null);
		if (setting == null || !bool.TryParse(setting, out var result))
		{
			return fallback;
		}
		return result;
	}

	private static int GetIntSetting(Dictionary<string, string> values, string key, int fallback)
	{
		string setting = GetSetting(values, key, null);
		if (setting == null || !int.TryParse(setting, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return result;
	}

	private static HashSet<int> ParseSplitScreenIndices(string serialized)
	{
		HashSet<int> hashSet = new HashSet<int>();
		string[] array = (serialized ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string s in array)
		{
			if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				hashSet.Add(result);
			}
		}
		return hashSet;
	}

	private static double GetDoubleSetting(Dictionary<string, string> values, string key, double fallback)
	{
		string setting = GetSetting(values, key, null);
		if (setting == null || !double.TryParse(setting, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || double.IsNaN(result) || double.IsInfinity(result))
		{
			return fallback;
		}
		return result;
	}

	private static void SaveNumericSetting(Dictionary<string, string> values, string key, NumericUpDown control)
	{
		PutSetting(values, key, control.Value);
	}

	private static void LoadNumericSetting(Dictionary<string, string> values, string key, NumericUpDown control)
	{
		string setting = GetSetting(values, key, null);
		if (setting != null && decimal.TryParse(setting, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			control.Value = ClampDecimal(result, control);
		}
	}

	private static void LoadCheckSetting(Dictionary<string, string> values, string key, CheckBox control)
	{
		control.Checked = GetBoolSetting(values, key, control.Checked);
	}

	private static void LoadTextSetting(Dictionary<string, string> values, string key, TextBox control)
	{
		string setting = GetSetting(values, key, null);
		if (setting != null)
		{
			control.Text = setting;
		}
	}

	private static void LoadComboIndexSetting(Dictionary<string, string> values, string key, ComboBox control)
	{
		int intSetting = GetIntSetting(values, key, control.SelectedIndex);
		if (intSetting >= 0 && intSetting < control.Items.Count)
		{
			control.SelectedIndex = intSetting;
		}
	}

	private static void LoadCheckedIndices(CheckedListBox control, string serialized)
	{
		HashSet<int> hashSet = new HashSet<int>();
		string[] array = (serialized ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string s in array)
		{
			if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				hashSet.Add(result);
			}
		}
		for (int j = 0; j < control.Items.Count; j++)
		{
			control.SetItemChecked(j, hashSet.Contains(j));
		}
	}

	private static void LoadComboIndexSetting(Dictionary<string, string> values, string key, TabControl control)
	{
		int intSetting = GetIntSetting(values, key, control.SelectedIndex);
		if (intSetting >= 0 && intSetting < control.TabPages.Count)
		{
			control.SelectedIndex = intSetting;
		}
	}

	private static string ComboText(ComboBox control)
	{
		if (control.SelectedItem != null)
		{
			return control.SelectedItem.ToString();
		}
		return "";
	}

	private static void LoadComboTextSetting(Dictionary<string, string> values, string key, ComboBox control)
	{
		string setting = GetSetting(values, key, null);
		if (setting != null)
		{
			int num = control.Items.IndexOf(setting);
			if (num >= 0)
			{
				control.SelectedIndex = num;
			}
		}
	}

	private static void ApplyColorButton(Button button, Color color, bool chooseReadableText)
	{
		button.BackColor = color;
		if (chooseReadableText)
		{
			int num = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
			button.ForeColor = ((num < 130) ? Color.White : Color.Black);
		}
	}

	private static void WriteSettingsFile(string path, Dictionary<string, string> values)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		string text = path + ".tmp";
		string[] contents = (from x in values.OrderBy((KeyValuePair<string, string> x) => x.Key, StringComparer.Ordinal)
			select x.Key + "\t" + Convert.ToBase64String(Encoding.UTF8.GetBytes(x.Value ?? ""))).ToArray();
		File.WriteAllLines(text, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		File.Copy(text, path, overwrite: true);
		TryDelete(text);
	}

	private static Dictionary<string, string> ReadSettingsFile(string path)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string[] array = File.ReadAllLines(path, Encoding.UTF8);
		foreach (string text in array)
		{
			int num = text.IndexOf('\t');
			if (num > 0)
			{
				try
				{
					string key = text.Substring(0, num);
					string value = Encoding.UTF8.GetString(Convert.FromBase64String(text.Substring(num + 1)));
					dictionary[key] = value;
				}
				catch
				{
				}
			}
		}
		return dictionary;
	}

	private void OnFormClosing(object sender, FormClosingEventArgs e)
	{
		if (_isRunning)
		{
			DialogResult dialogResult = MessageBox.Show(this, "视频处理尚未完成，确定要退出吗？", "确认退出", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
			if (dialogResult == DialogResult.No)
			{
				e.Cancel = true;
				return;
			}
			_cancelRequested = true;
			lock (_processLock)
			{
				if (_currentProcess != null)
				{
					TryKill(_currentProcess);
				}
			}
		}
		SaveUserSettings();
		lock (_splitScreenPreviewLock)
		{
			foreach (Image value in _splitScreenPreviewImages.Values)
			{
				value.Dispose();
			}
			_splitScreenPreviewImages.Clear();
			_splitScreenPendingPreviews.Clear();
		}
	}

	private void SetRunningState(bool running)
	{
		_isRunning = running;
		_addButton.Enabled = !running;
		_removeButton.Enabled = !running;
		_upButton.Enabled = !running;
		_downButton.Enabled = !running;
		_shuffleButton.Enabled = !running;
		_crossFolderButton.Enabled = !running;
		_clearButton.Enabled = !running;
		_mergeResetButton.Enabled = !running;
		_browseOutputButton.Enabled = !running;
		_groupSize.Enabled = !running;
		_maxRepeatsPerSource.Enabled = !running;
		_maxOutputCount.Enabled = !running;
		_combinationStartMode.Enabled = !running;
		_limitGroupDuration.Enabled = !running;
		_maxGroupDuration.Enabled = !running;
		_overlongVideoMode.Enabled = !running;
		_mergeCanvasFitMode.Enabled = !running;
		SetOutputFrameControlsEnabled(_mergeOutputFrameControls, !running);
		_similaritySort.Enabled = !running;
		_similarityMode.Enabled = !running;
		_similarityThreshold.Enabled = !running;
		_outputFolder.Enabled = !running;
		_autoFallback.Enabled = !running;
		_transitionEffects.Enabled = !running;
		_transitionSelectAllButton.Enabled = !running;
		_transitionClearButton.Enabled = !running;
		_transitionOrder.Enabled = !running;
		_transitionDuration.Enabled = !running;
		_transitionLockSingleEffect.Enabled = !running;
		_startButton.Enabled = !running;
		if (_mergePreviewButton != null)
		{
			_mergePreviewButton.Enabled = !running;
		}
		_cancelButton.Enabled = running;
		SetBgmControlsEnabled(_mergeBgm, !running);
		_splitAddButton.Enabled = !running;
		_splitRemoveButton.Enabled = !running;
		_splitClearButton.Enabled = !running;
		_splitResetButton.Enabled = !running;
		_splitMode.Enabled = !running;
		SetOutputFrameControlsEnabled(_splitOutputFrameControls, !running);
		_splitOutputFolder.Enabled = !running;
		_splitBrowseOutputButton.Enabled = !running;
		_splitStartButton.Enabled = !running;
		_splitCancelButton.Enabled = running;
		SetBgmControlsEnabled(_splitBgm, !running);
		_watermarkOnMerge.Enabled = !running;
		_watermarkOnSplit.Enabled = !running;
		_watermarkOnSplitScreen.Enabled = !running;
		_watermarkStayEffectPool.Enabled = !running;
		_watermarkStaySelectAllButton.Enabled = !running;
		_watermarkStayRandomButton.Enabled = !running;
		_watermarkStayClearButton.Enabled = !running;
		for (int i = 0; i < 3; i++)
		{
			_textWatermarkEnabled[i].Enabled = !running;
			_imageWatermarkEnabled[i].Enabled = !running;
		}
		_watermarkVideoAddButton.Enabled = !running;
		_watermarkVideoRemoveButton.Enabled = !running;
		_watermarkVideoClearButton.Enabled = !running;
		_watermarkSourceTabs.Enabled = !running;
		_watermarkSourceImageAddButton.Enabled = !running;
		_watermarkSourceImageRemoveButton.Enabled = !running;
		_watermarkSourceImageClearButton.Enabled = !running;
		_watermarkResetButton.Enabled = !running;
		_globalResetButton.Enabled = !running;
		_watermarkOutputFolder.Enabled = !running;
		SetOutputFrameControlsEnabled(_watermarkOutputFrameControls, !running && _watermarkSourceTabs.SelectedIndex == 0);
		_watermarkOutputBrowseButton.Enabled = !running;
		_watermarkStartButton.Enabled = !running;
		_watermarkCancelButton.Enabled = running;
		SetBgmControlsEnabled(_watermarkBgm, !running && _watermarkSourceTabs.SelectedIndex == 0);
		_imageAddButton.Enabled = !running;
		_imageRemoveButton.Enabled = !running;
		_imageUpButton.Enabled = !running;
		_imageDownButton.Enabled = !running;
		_imageShuffleButton.Enabled = !running;
		_imageClearButton.Enabled = !running;
		_imageResetButton.Enabled = !running;
		_imageDuration.Enabled = !running;
		_imageOutputCount.Enabled = !running;
		_imageTargetDuration.Enabled = !running;
		_imageResolution.Enabled = !running;
		_imageLayoutPreset.Enabled = !running;
		_imageSmartEnhance.Enabled = !running;
		_imageLayoutPool.Enabled = !running;
		_imageLayoutSelectAllButton.Enabled = !running;
		_imageLayoutClearButton.Enabled = !running;
		_imageLayoutOrder.Enabled = !running;
		_imageMotionPool.Enabled = !running;
		_imageMotionSelectAllButton.Enabled = !running;
		_imageMotionClearButton.Enabled = !running;
		_imageMotionOrder.Enabled = !running;
		_imageTransitions.Enabled = !running;
		_imageTransitionSelectAllButton.Enabled = !running;
		_imageTransitionClearButton.Enabled = !running;
		_imageTransitionOrder.Enabled = !running;
		_imageTransitionDuration.Enabled = !running;
		_imageTileAnimation.Enabled = false;
		_imageTileAnimationDuration.Enabled = !running && _imageMotionPool.CheckedIndices.Count > 0;
		_imageOutputFolder.Enabled = !running;
		_imageOutputBrowseButton.Enabled = !running;
		_imageStartButton.Enabled = !running;
		_imageCancelButton.Enabled = running;
		SetBgmControlsEnabled(_imageBgm, !running);
		_splitScreenPresetTabs.Enabled = !running;
		_splitScreenRegionSelector.Enabled = !running;
		_splitScreenRegionList.Enabled = !running;
		_splitScreenAddButton.Enabled = !running;
		_splitScreenRemoveButton.Enabled = !running;
		_splitScreenClearButton.Enabled = !running;
		_splitScreenMoveUpButton.Enabled = !running;
		_splitScreenMoveDownButton.Enabled = !running;
		_splitScreenAssignmentMode.Enabled = !running;
		_splitScreenCanvas.Enabled = !running;
		_splitScreenDurationMode.Enabled = !running;
		_splitScreenMainRegion.Enabled = !running;
		_splitScreenAudioRegion.Enabled = !running;
		_splitScreenBorderPreset.Enabled = !running;
		_splitScreenBorderScope.Enabled = !running;
		_splitScreenBorderRegionsButton.Enabled = !running;
		_splitScreenBorderWidth.Enabled = !running;
		_splitScreenBorderColorButton.Enabled = !running;
		_splitScreenWatermarkLayer.Enabled = !running;
		_splitScreenWatermarkRefreshButton.Enabled = !running;
		_splitScreenRestoreRegionsButton.Enabled = !running;
		_splitScreenRegionEnabled.Enabled = !running;
		_splitScreenOutputFolder.Enabled = !running;
		_splitScreenOutputBrowseButton.Enabled = !running;
		_splitScreenResetButton.Enabled = !running;
		_splitScreenStartButton.Enabled = !running;
		if (_splitScreenPlayPreviewButton != null)
		{
			_splitScreenPlayPreviewButton.Enabled = !running;
		}
		if (_splitScreenPreviewDuration != null)
		{
			_splitScreenPreviewDuration.Enabled = !running;
		}
		_splitScreenCancelButton.Enabled = running;
		SetBgmControlsEnabled(_splitScreenBgm, !running);
		SetVideoAdjustmentEditorEnabled(_mergeAdjustmentEditor, !running);
		SetVideoAdjustmentEditorEnabled(_splitAdjustmentEditor, !running);
		SetVideoAdjustmentEditorEnabled(_watermarkAdjustmentEditor, !running);
		UpdateWatermarkUi();
		UpdateSplitModeUi();
		UpdateMergePlanningUi();
		UpdateImageLayoutUi();
		UpdateSplitScreenPlanningUi();
		LoadSplitScreenRegionControls();
		UpdateSplitScreenPipUi();
	}

	private static void SetVideoAdjustmentEditorEnabled(VideoAdjustmentEditor editor, bool enabled)
	{
		if (editor != null)
		{
			editor.HorizontalFlip.Enabled = enabled;
			editor.ReversePlayback.Enabled = enabled;
			editor.CenterCropPortrait.Enabled = enabled;
			editor.ScaleRatio.Enabled = enabled;
			editor.SpeedRatio.Enabled = enabled;
			editor.VolumePercent.Enabled = enabled;
			editor.ApplySelected.Enabled = enabled;
			editor.ApplyAll.Enabled = enabled;
			editor.CopyAdjustment.Enabled = enabled;
			editor.PasteAdjustment.Enabled = enabled;
		}
	}

	private void SetOutputFrameControlsEnabled(OutputFrameControls controls, bool enabled)
	{
		if (controls != null)
		{
			controls.AspectMode.Enabled = enabled;
			bool flag = controls.AspectMode.SelectedIndex == 1 || controls.AspectMode.SelectedIndex == 2;
			controls.CropAnchor.Enabled = enabled && flag;
			controls.CustomPositionPercent.Enabled = enabled && flag && controls.CropAnchor.SelectedIndex == 3;
		}
	}

	private static void SetBgmControlsEnabled(BgmControls controls, bool enabled)
	{
		if (controls != null)
		{
			controls.Add.Enabled = enabled;
			controls.Clear.Enabled = enabled && controls.Files.Count > 0;
			controls.AssignmentMode.Enabled = enabled;
			controls.VolumePercent.Enabled = enabled;
		}
	}

	private void UiProgress(double ratio, string status)
	{
		if (!base.IsDisposed && base.IsHandleCreated)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				int num = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, ratio)) * 100.0);
				_progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, num));
				_statusLabel.Text = status + "（" + num + "%）";
			});
		}
	}

	private void UiStatus(string status)
	{
		if (!base.IsDisposed && base.IsHandleCreated)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				_statusLabel.Text = status;
			});
		}
	}

	private void UiSplitProgress(double ratio, string status)
	{
		if (!base.IsDisposed && base.IsHandleCreated)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				int num = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, ratio)) * 100.0);
				_splitProgressBar.Value = Math.Max(_splitProgressBar.Minimum, Math.Min(_splitProgressBar.Maximum, num));
				_splitStatusLabel.Text = status + "（" + num + "%）";
			});
		}
	}

	private void UiSplitStatus(string status)
	{
		if (!base.IsDisposed && base.IsHandleCreated)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				_splitStatusLabel.Text = status;
			});
		}
	}

	private void UiWatermarkProgress(double ratio, string status)
	{
		if (!base.IsDisposed && base.IsHandleCreated)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				int num = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, ratio)) * 100.0);
				_watermarkProgressBar.Value = Math.Max(_watermarkProgressBar.Minimum, Math.Min(_watermarkProgressBar.Maximum, num));
				_watermarkStatusLabel.Text = status + "（" + num + "%）";
			});
		}
	}

	private void UiWatermarkBatchProgress(int completedItems, double activeItemProgress, int totalItems, string status)
	{
		int num = Math.Max(1, totalItems);
		double num2 = Math.Max(0.0, Math.Min(1.0, activeItemProgress));
		double ratio = ((double)Math.Max(0, completedItems) + num2) / (double)num;
		UiWatermarkProgress(ratio, status);
	}

	private void UiWatermarkStatus(string status)
	{
		if (!base.IsDisposed && base.IsHandleCreated)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				_watermarkStatusLabel.Text = status;
			});
		}
	}

	private void UiImageProgress(double ratio, string status)
	{
		if (!base.IsDisposed && base.IsHandleCreated)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				int num = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, ratio)) * 100.0);
				_imageProgressBar.Value = Math.Max(_imageProgressBar.Minimum, Math.Min(_imageProgressBar.Maximum, num));
				_imageStatusLabel.Text = status + "（" + num + "%）";
			});
		}
	}

	private static void TryKill(Process process)
	{
		try
		{
			if (process != null && !process.HasExited)
			{
				process.Kill();
			}
		}
		catch
		{
		}
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch
		{
		}
	}

	private static void TryAppendLog(string path, string text)
	{
		try
		{
			File.AppendAllText(path, Environment.NewLine + text + Environment.NewLine, Encoding.UTF8);
		}
		catch
		{
		}
	}

	private static void WriteOutputReconciliation(string outputFolder, string moduleName, int expectedCount, List<string> successfulOutputs, List<string> failedItems)
	{
	}

	private static string LastUsefulLines(string text, int count)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return "FFmpeg 未返回详细信息";
		}
		string[] array = text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		return string.Join(" | ", array.Skip(Math.Max(0, array.Length - count)).ToArray());
	}

	private static int MakeEven(int value)
	{
		value = Math.Max(2, value);
		if (value % 2 != 0)
		{
			return value - 1;
		}
		return value;
	}

	private static string QuoteArg(string value)
	{
		return "\"" + value.Replace("\"", "\\\"") + "\"";
	}
}
