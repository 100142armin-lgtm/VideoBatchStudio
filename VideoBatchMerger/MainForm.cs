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
	private static bool _isDarkMode = true;
	public static bool IsDarkMode
	{
		get => _isDarkMode;
		set => _isDarkMode = value;
	}

	private static Color CanvasColor => _isDarkMode ? Color.FromArgb(11, 19, 32) : Color.FromArgb(241, 245, 249);
	private static Color SurfaceColor => _isDarkMode ? Color.FromArgb(19, 32, 55) : Color.FromArgb(255, 255, 255);
	private static Color SurfaceAltColor => _isDarkMode ? Color.FromArgb(15, 26, 45) : Color.FromArgb(248, 250, 252);
	private static Color InkColor => _isDarkMode ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
	private static Color MutedColor => _isDarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
	private static Color BorderColor => _isDarkMode ? Color.FromArgb(34, 53, 84) : Color.FromArgb(203, 213, 225);
	private static Color AccentColor => Color.FromArgb(37, 99, 235);
	private static Color AccentHoverColor => Color.FromArgb(59, 130, 246);
	private static Color HeaderColor => _isDarkMode ? Color.FromArgb(15, 26, 45) : Color.FromArgb(255, 255, 255);
	private static Color SplitterColor => _isDarkMode ? Color.FromArgb(11, 19, 32) : Color.FromArgb(226, 232, 240);
	private static Color InputBgColor => _isDarkMode ? Color.FromArgb(13, 24, 41) : Color.FromArgb(248, 250, 252);

	private Panel _bottomNavBar;
	private Panel _bottomNavLine;
	public const string CurrentAppVersion = "8.7.1";
	private Button _checkUpdateButton;
	private Button _themeToggleButton;
	private readonly List<Button> _navButtons = new List<Button>();
	private Panel _cutEditVideoHostPanel;
	private double _cutEditPlaybackStartPos = 0.0;

	private string _cutEditSourcePath;
	private double _cutEditDuration;
	private int _cutEditWidth;
	private int _cutEditHeight;
	private double _cutEditCurrentPos;
	private readonly List<CutSegment> _cutEditSegments = new List<CutSegment>();
	private Label _cutEditVideoInfoLabel;
	private PictureBox _cutEditPreviewBox;
	private Label _cutEditEmptyPlaceholder;
	private Button _splitScreenSendToCutBtn;
	private System.Windows.Forms.Integration.ElementHost _cutEditElementHost;
	private System.Windows.Controls.MediaElement _cutEditMediaElement;
	private System.Windows.Controls.Image _cutEditWpfOverlayImage;
	private System.Windows.Controls.Border _cutEditWpfTransitionBorder;
	private Button _cutEditClearAllBtn;
	private System.Windows.Forms.Timer _cutEditPlayTimer;
	private bool _cutEditIsPlaying;
	private bool _cutEditIsDraggingScrubber;
	private readonly System.Diagnostics.Stopwatch _cutEditPlaybackSw = new System.Diagnostics.Stopwatch();
	private TrackBar _cutEditTimeScrubber;
	private Label _cutEditTimeLabel;
	private Button _cutEditPlayPauseButton;
	private Button _cutEditStepBack5Btn;
	private Button _cutEditStepBack1Btn;
	private Button _cutEditStepForward1Btn;
	private Button _cutEditStepForward5Btn;
	private TrackBar _cutEditVolumeTrackBar;
	private Button _cutEditMuteBtn;
	private CheckBox _cutEditLoopCheckBox;
	private Button _cutEditSetInButton;
	private Button _cutEditSetOutButton;
	private Button _cutEditSplitButton;
	private ListView _cutEditSegmentList = new ListView();
	private Button _cutEditDeleteBtn;
	private Button _cutEditToggleSegmentButton;
	private Button _cutEditResetSegmentsButton;
	private Label _cutEditEstimatedDurationLabel;

	// Enhanced Multi-track & Media Pool fields
	private readonly List<TimelineTrack> _cutEditTracks = new List<TimelineTrack>();
	private int _cutEditSelectedSegmentIndex = -1;
	private string _cutEditSelectedTrackId = "V1";
	private double _cutEditTimelineZoom = 1.0;
	private int _cutEditBaseTrackHeight = 36;
	private TrackBar _cutEditZoomSlider;
	private Label _cutEditZoomLabel;
	private Button _cutEditZoomOutBtn;
	private Button _cutEditZoomInBtn;
	private TrackBar _cutEditTrackHeightSlider;
	private Label _cutEditTrackHeightLabel;
	private Button _cutEditHeightDownBtn;
	private Button _cutEditHeightUpBtn;
	private Button _cutEditAddTrackBtn;

	// Professional timeline tools and ergonomic navigation
	private enum TimelineToolMode { Select, Razor }
	private TimelineToolMode _cutEditCurrentTool = TimelineToolMode.Select;
	private bool _cutEditSnappingEnabled = true;
	private bool _cutEditLinkedSelectionEnabled = true;
	private int _cutEditScrollX = 0;
	private int _snapGuideScreenX = -1;
	private int _razorHoverX = -1;
	private Point _panStartMousePoint;
	private int _panStartScrollX;
	private CutSegment _dragPartnerSegment = null;
	private double _dragPartnerOriginalTimelineStart = 0.0;

	private Button _cutEditToolSelectBtn;
	private Button _cutEditToolRazorBtn;
	private Button _cutEditRippleDeleteBtn;
	private Button _cutEditSnapBtn;
	private Button _cutEditLinkBtn;
	private Button _cutEditCloseGapsBtn;
	private ToolTip _cutEditToolTip;

	// Dragging & Repositioning states
	private enum TimelineDragMode { None, ScrubPlayhead, MoveClip, TrimIn, TrimOut, PanHand }
	private TimelineDragMode _timelineDragMode = TimelineDragMode.None;
	private int _dragSegmentIndex = -1;
	private Point _dragStartMousePoint;
	private double _dragOriginalTimelineStart;
	private double _dragOriginalDuration;
	private double _dragOriginalStartSec;
	private double _dragOriginalEndSec;
	private string _dragOriginalTrackId;
	private CutSegment _lastActiveSegment = null;

	// Media pool thumbnail & view modes
	private ImageList _cutEditMediaPoolImageList;
	private bool _cutEditMediaPoolIsThumbView = false;
	private readonly Dictionary<string, Image> _cutEditMediaPoolThumbCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _cutEditMediaPoolThumbsGenerating = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	private Button _cutEditMediaPoolViewListBtn;
	private Button _cutEditMediaPoolViewThumbBtn;
	private Button _cutEditMediaPoolAppendEndBtn;
	private Button _cutEditMediaPoolInsertPlayheadBtn;
	private Button _cutEditMediaPoolInsertStartBtn;

	// Real audio waveform cache
	private readonly Dictionary<string, Image> _cutEditWaveformCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _cutEditWaveformsGenerating = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private CheckBox _cutEditTitleEnabled;
	private RadioButton _cutEditTitleOverlayRadio;
	private RadioButton _cutEditTitleCardRadio;
	private ComboBox _cutEditTitleStyle;
	private TextBox _cutEditMainTitle;
	private TextBox _cutEditSubtitle;
	private NumericUpDown _cutEditTitleStartTime;
	private NumericUpDown _cutEditTitleDuration;
	private Rectangle _cachedT1Rect;
	private TrackBar _cutEditTitleFontSizeSlider;
	private Label _cutEditTitleFontSizeLabel;
	private ComboBox _cutEditTitleTextColorCombo;
	private ComboBox _cutEditTitleStrokeCombo;
	private ComboBox _cutEditTitleBannerBgCombo;
	private ComboBox _cutEditTitlePositionCombo;
	private bool _updatingTitleTemplate;
	private Button _cutEditTitlePreviewButton;
	private TabControl _cutEditInspectorTabs;

	// Multi-segment Text & Image Overlays (Phase 2)
	private readonly List<CutOverlayItem> _cutEditOverlays = new List<CutOverlayItem>();
	private CutOverlayItem _selectedOverlay = null;
	private readonly Dictionary<string, Rectangle> _cachedOverlayRects = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Image> _overlayImageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
	private bool _isDraggingOverlay = false;
	private int _overlayDragEdge = 0;
	private int _overlayDragStartMouse;
	private double _overlayDragOrigStart;
	private double _overlayDragOrigDur;
	private CutOverlayItem _draggedOverlayItem = null;
	private bool _updatingOverlayInspector = false;

	private ComboBox _overlayItemCombo;
	private CheckBox _overlayItemEnabledCheckBox;
	private Button _overlayAddTextBtn;
	private Button _overlayAddImageBtn;
	private Button _overlayDuplicateBtn;
	private Button _overlayDeleteBtn;
	private NumericUpDown _overlayStartTimeNum;
	private NumericUpDown _overlayDurationNum;
	private ComboBox _overlayPositionCombo;
	private NumericUpDown _overlayOffsetXNum;
	private NumericUpDown _overlayOffsetYNum;
	private Panel _overlayTextPropsPanel;
	private Panel _overlayImagePropsPanel;
	private TextBox _overlayImagePathText;
	private PictureBox _overlayImageThumbBox;
	private TrackBar _overlayImageScaleSlider;
	private Label _overlayImageScaleLabel;
	private TrackBar _overlayImageOpacitySlider;
	private Label _overlayImageOpacityLabel;

	private int _cutEditPreviewSeq;

	private sealed class CutEditProjectState
	{
		public List<CutSegment> Segments { get; set; }
		public List<CutOverlayItem> Overlays { get; set; }
		public double CurrentPos { get; set; }
		public string SelectedOverlayId { get; set; }
		public string Description { get; set; }
	}

	private Bitmap _cutEditCachedBaseFrame;
	private string _cutEditCachedBaseFramePath;
	private double _cutEditCachedBaseFrameTime = -999.0;
	private readonly object _cutEditBaseFrameLock = new object();

	private Bitmap _deliverCachedBaseFrame;
	private string _deliverCachedBaseFramePath;
	private double _deliverCachedBaseFrameTime = -999.0;
	private readonly object _deliverBaseFrameLock = new object();

	private SplitContainer _deliverRightSplit;
	private Button _deliverPopoutBtn;
	private Button _deliverExpandToggleBtn;
	private DeliverPopoutPreviewForm _deliverPopoutForm;
	private Panel _deliverMonitorBox;

	private readonly Stack<CutEditProjectState> _cutEditUndoStack = new Stack<CutEditProjectState>();
	private readonly Stack<CutEditProjectState> _cutEditRedoStack = new Stack<CutEditProjectState>();
	private bool _isPerformingUndoRedo = false;
	private Button _cutEditUndoButton;
	private Button _cutEditRedoButton;
	private Button _cutEditSaveProjectButton;
	private string _cutEditCurrentProjectPath;

	private readonly List<string> _cutEditMediaPool = new List<string>();
	private ListView _cutEditMediaPoolList;
	private Button _cutEditMediaPoolAddButton;
	private Button _cutEditMediaPoolRemoveButton;
	private Button _cutEditMediaPoolClearButton;
	private PictureBox _cutEditTimelineCanvas;
	private bool _cutEditAudioMuted;
	private int _cutEditAudioVolume = 100;

	internal double CutEditDuration => _cutEditDuration;
	internal int CutEditSubtitleBottomOffset => _cutEditSubtitleBottomOffset;
	private int _cutEditSubtitleBottomOffset = 407;
	private CutEditPopoutPreviewForm _cutEditPopoutForm;
	private TrackBar _cutEditSubtitlePosTrackBar;
	private Label _cutEditSubtitlePosLabel;
	private CheckBox _cutEditRealtimePreviewCheckBox;
	private Button _cutEditPopoutPreviewBtn;
	private Button _cutEditPopoutPreviewHeaderBtn;

	// Audio quick mix fields for timeline toolbar
	private CheckBox _cutEditKeepOriginalAudioCheckBox;
	private TrackBar _cutEditBgmVolumeTrackBar;
	private Label _cutEditBgmVolumeLabel;
	private int _cutEditBgmVolume = 40;
	private Button _cutEditFitWindowBtn;
	private ComboBox _cutEditSpeedCombo;

	private TextBox _deliverOutputFolder;
	private TextBox _deliverOutputFileName;
	private ComboBox _deliverFormatCombo;
	private ComboBox _deliverResolutionCombo;
	private ComboBox _deliverQualityCombo;
	private ComboBox _deliverFpsCombo;
	private ComboBox _deliverAudioBitrateCombo;
	private Label _deliverProjectSummary;
	private Button _deliverStartButton;
	private ProgressBar _deliverProgressBar;
	private Label _deliverStatusLabel;
	private ListView _deliverHistoryList;
	private Button _deliverOpenFolderButton;
	private Button _deliverPlayOutputButton;
	private Button _deliverSendToSplitScreenButton;
	private Button _deliverSendToMergeButton;
	private Button _deliverSendBackToCutButton;
	private string _deliverLastExportPath;
	private PictureBox _deliverPreviewBox;
	private Label _deliverEmptyPlaceholder;
	private System.Windows.Forms.Integration.ElementHost _deliverElementHost;
	private System.Windows.Controls.MediaElement _deliverMediaElement;
	private System.Windows.Controls.Image _deliverWpfOverlayImage;
	private System.Windows.Controls.Border _deliverWpfTransitionBorder;
	private Button _deliverPlayPauseButton;
	private TrackBar _deliverTimeScrubber;
	private Label _deliverTimeLabel;
	private System.Windows.Forms.Timer _deliverPlayTimer;
	private bool _deliverIsPlaying;
	private double _deliverCurrentPos;
	private double _deliverDuration;
	private System.Diagnostics.Stopwatch _deliverPlaybackSw = new System.Diagnostics.Stopwatch();
	private double _deliverPlaybackStartPos;
	private int _deliverPreviewSeq;
	private bool _deliverScrubberWasPlaying;

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

	internal static readonly (string id, string name, string shortName)[] TransitionDefinitions = new[]
	{
		("none", "✖️ 无转场 (默认直接硬切)", "无"),
		("dissolve", "🌫️ 交叉叠化溶解 (Dissolve)", "溶解"),
		("fadeblack", "🖤 黑色淡入淡出 (Dip to Black)", "黑场"),
		("fadewhite", "⚪ 亮白闪烁淡入 (Flash White)", "白闪"),
		("wipeleft", "⬅️ 向左擦除 (Wipe Left)", "左擦"),
		("wiperight", "➡️ 向右擦除 (Wipe Right)", "右擦"),
		("wipeup", "⬆️ 向上推移 (Slide Up)", "上移"),
		("wipedown", "⬇️ 向下推移 (Slide Down)", "下移"),
		("slideleft", "◀️ 向左平移滑入 (Slide Left)", "左滑"),
		("slideright", "▶️ 向右平移滑入 (Slide Right)", "右滑"),
		("zoom", "🔍 镜头推拉缩放 (Zoom In)", "缩放"),
		("radial", "🔄 径向旋转 (Radial Spin)", "旋转"),
		("pixelize", "🔲 像素马赛克 (Pixelize)", "马赛克"),
		("hblur", "💨 动态水平模糊 (Motion Blur)", "动模"),
		("circleopen", "⭕ 圆形展开 (Circle Open)", "圆展")
	};

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

	private CheckBox _mergeTitleEnabled;

	private ComboBox _mergeTitleStyleCombo;

	private TextBox _mergeTitleMainTextBox;

	private NumericUpDown _mergeTitleDurationNum;

	private Button _mergeTitleTweakBtn;

	private MergeTitlePlan _mergeTitlePlan = new MergeTitlePlan();

	private MergeTitlePlan _activeMergeTitlePlan;

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

	private CheckBox _splitScreenTitleEnabled;
	private ComboBox _splitScreenTitleStyleCombo;
	private ComboBox _splitScreenTitlePositionCombo;
	private TextBox _splitScreenTitleTextBox;
	private NumericUpDown _splitScreenTitleDurationNum;
	private Button _splitScreenTitleStyleBtn;
	private MergeTitlePlan _splitScreenTitlePlan = new MergeTitlePlan
	{
		Position = "中间分割线",
		DurationSeconds = 0.0
	};

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
		InitCutEditTracks();
		Text = $"视频批处理工具 V{CurrentAppVersion}";
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

		ThreadPool.QueueUserWorkItem(delegate
		{
			Thread.Sleep(3000);
			if (!IsDisposed)
			{
				UpdateChecker.CheckForUpdatesAsync(this, CurrentAppVersion, isManual: false);
			}
		});
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
		UpdateThemeToggleButton();
		ApplyThemeToWholeApp();
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
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
		base.BackColor = CanvasColor;
		base.Controls.Add(tableLayoutPanel);

		_tabs = new TabControl();
		_tabs.Dock = DockStyle.Fill;
		_tabs.Margin = new Padding(0);
		_tabs.SizeMode = TabSizeMode.Fixed;
		_tabs.ItemSize = new Size(0, 1);
		_tabs.Appearance = TabAppearance.FlatButtons;

		_tabs.TabPages.Add(BuildMergeTab());        // 0
		_tabs.TabPages.Add(BuildSplitTab());        // 1
		_tabs.TabPages.Add(BuildWatermarkTab());    // 2
		_tabs.TabPages.Add(BuildImageVideoTab());   // 3
		_tabs.TabPages.Add(BuildSplitScreenTab());  // 4
		_tabs.TabPages.Add(BuildCutEditTab());      // 5
		_tabs.TabPages.Add(BuildDeliverTab());      // 6
		tableLayoutPanel.Controls.Add(_tabs, 0, 0);

		_bottomNavBar = new Panel();
		_bottomNavBar.Dock = DockStyle.Fill;
		_bottomNavBar.Margin = new Padding(0);
		_bottomNavBar.BackColor = HeaderColor;

		_bottomNavLine = new Panel();
		_bottomNavLine.Dock = DockStyle.Top;
		_bottomNavLine.Height = 1;
		_bottomNavLine.BackColor = BorderColor;
		_bottomNavBar.Controls.Add(_bottomNavLine);

		FlowLayoutPanel navRight = new FlowLayoutPanel();
		navRight.Dock = DockStyle.Right;
		navRight.AutoSize = true;
		navRight.FlowDirection = FlowDirection.LeftToRight;
		navRight.WrapContents = false;
		navRight.Padding = new Padding(0, 7, 16, 7);
		navRight.Margin = new Padding(0);

		_checkUpdateButton = MakeButton("🔔 检查更新", 108);
		_checkUpdateButton.Height = 36;
		_checkUpdateButton.Margin = new Padding(0, 0, 8, 0);
		_checkUpdateButton.Click += delegate
		{
			UpdateChecker.CheckForUpdatesAsync(this, CurrentAppVersion, isManual: true);
		};
		ToolTip updateTip = new ToolTip();
		updateTip.SetToolTip(_checkUpdateButton, "检查最新版本与团队功能更新");
		navRight.Controls.Add(_checkUpdateButton);

		_themeToggleButton = MakeButton(_isDarkMode ? "🌙 暗黑模式" : "☀️ 日间模式", 112);
		_themeToggleButton.Tag = "accent";
		_themeToggleButton.Height = 36;
		_themeToggleButton.Margin = new Padding(0, 0, 8, 0);
		_themeToggleButton.Click += delegate
		{
			_isDarkMode = !_isDarkMode;
			UpdateThemeToggleButton();
			ApplyThemeToWholeApp();
			SaveUserSettings();
		};
		ToolTip headerTip = new ToolTip();
		headerTip.SetToolTip(_themeToggleButton, "切换白天 / 暗黑界面模式（当前模式会自动保存）");
		navRight.Controls.Add(_themeToggleButton);

		_globalResetButton = MakeButton("重置全部设置", 116);
		_globalResetButton.Tag = "danger";
		_globalResetButton.Height = 36;
		_globalResetButton.Margin = new Padding(0);
		navRight.Controls.Add(_globalResetButton);
		_bottomNavBar.Controls.Add(navRight);

		FlowLayoutPanel navCenter = new FlowLayoutPanel();
		navCenter.AutoSize = true;
		navCenter.FlowDirection = FlowDirection.LeftToRight;
		navCenter.WrapContents = false;
		navCenter.Padding = new Padding(0, 7, 0, 7);
		navCenter.Margin = new Padding(0);

		_navButtons.Clear();
		navCenter.Controls.Add(MakeNavButton("📦 批量合并", 0));
		navCenter.Controls.Add(MakeNavButton("✂️ 视频拆分", 1));
		navCenter.Controls.Add(MakeNavButton("💧 水印处理", 2));
		navCenter.Controls.Add(MakeNavButton("🖼️ 图片成片", 3));

		Panel navDivider = new Panel();
		navDivider.Width = 1;
		navDivider.Height = 24;
		navDivider.Margin = new Padding(8, 6, 8, 6);
		navDivider.BackColor = BorderColor;
		navCenter.Controls.Add(navDivider);

		navCenter.Controls.Add(MakeNavButton("▦ 视频拼屏", 4));
		navCenter.Controls.Add(MakeNavButton("🎬 视频剪辑", 5));
		navCenter.Controls.Add(MakeNavButton("🚀 导出交付", 6));
		_bottomNavBar.Controls.Add(navCenter);

		void CenterNavButtons()
		{
			if (_bottomNavBar.ClientSize.Width > 0 && navCenter.PreferredSize.Width > 0)
			{
				int x = (_bottomNavBar.ClientSize.Width - navCenter.PreferredSize.Width) / 2;
				navCenter.Location = new Point(Math.Max(10, x), 0);
			}
		}

		_bottomNavBar.Resize += delegate
		{
			CenterNavButtons();
		};
		base.Shown += delegate
		{
			CenterNavButtons();
		};

		tableLayoutPanel.Controls.Add(_bottomNavBar, 0, 1);

		UpdateNavButtonsActiveState();
		ApplyThemeToWholeApp();
	}

	private Button MakeNavButton(string text, int tabIndex)
	{
		Button btn = MakeButton(text, 116);
		btn.Height = 36;
		btn.Margin = new Padding(0, 0, 6, 0);
		btn.Tag = (tabIndex == (_tabs?.SelectedIndex ?? 0)) ? "nav-active" : "nav";
		btn.Click += delegate
		{
			SwitchToWorkspace(tabIndex);
		};
		_navButtons.Add(btn);
		return btn;
	}

	private void SwitchToWorkspace(int index)
	{
		if (_tabs == null || index < 0 || index >= _tabs.TabPages.Count) return;
		_tabs.SelectedIndex = index;
		UpdateNavButtonsActiveState();
		if (index == 6)
		{
			UpdateDeliverSummary();
			InitOrRefreshDeliverPreview();
		}
		SaveUserSettings();
	}

	private void UpdateNavButtonsActiveState()
	{
		if (_tabs == null) return;
		for (int i = 0; i < _navButtons.Count; i++)
		{
			bool isActive = (i == _tabs.SelectedIndex);
			_navButtons[i].Tag = isActive ? "nav-active" : "nav";
			_navButtons[i].Invalidate();
		}
	}

	private TabPage BuildMergeTab()
	{
		TabPage tabPage = new TabPage("批量合并");
		tabPage.BackColor = CanvasColor;
		Panel panel = new Panel();
		panel.Dock = DockStyle.Top;
		panel.Height = 52;
		panel.Margin = new Padding(0);
		panel.Padding = new Padding(12, 9, 12, 6);
		panel.BackColor = SurfaceColor;
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
		tabPage.Controls.Add(panel2);
		Panel panel3 = MakeListHost();
		_videoList = MakeVideoList();
		_mergeAdjustmentEditor = MakeVideoAdjustmentEditor();
		InstallVideoListArea(panel3, _videoList, _mergeAdjustmentEditor);
		Panel panel4 = new Panel();
		panel4.Dock = DockStyle.Fill;
		panel4.Margin = new Padding(0);
		panel4.BackColor = SurfaceColor;
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

		// Video Title Packaging Row
		_mergeTitleEnabled = new CheckBox
		{
			Text = "🏷️ 视频标题包装",
			AutoSize = true,
			Location = new Point(20, 305),
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
		};
		panel5.Controls.Add(_mergeTitleEnabled);

		_mergeTitleStyleCombo = new ComboBox
		{
			Location = new Point(160, 302),
			Width = 310,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_mergeTitleStyleCombo.Items.AddRange(MergeTitleStyleCatalog.Styles.Select(s => (object)s.Name).ToArray());
		_mergeTitleStyleCombo.SelectedIndex = 0;
		panel5.Controls.Add(_mergeTitleStyleCombo);

		panel5.Controls.Add(MakeLabel("标题内容", 482, 306));
		_mergeTitleMainTextBox = new TextBox
		{
			Location = new Point(542, 302),
			Width = 150,
			Text = "{文件名}"
		};
		panel5.Controls.Add(_mergeTitleMainTextBox);

		Button btnInsertFileName = MakeButton("+首素材名", 76);
		btnInsertFileName.Location = new Point(698, 300);
		btnInsertFileName.Height = 26;
		btnInsertFileName.Click += delegate
		{
			_mergeTitleMainTextBox.Paste("{文件名}");
		};
		panel5.Controls.Add(btnInsertFileName);

		Button btnInsertIndex = MakeButton("+序号", 56);
		btnInsertIndex.Location = new Point(780, 300);
		btnInsertIndex.Height = 26;
		btnInsertIndex.Click += delegate
		{
			_mergeTitleMainTextBox.Paste("{序号}");
		};
		panel5.Controls.Add(btnInsertIndex);

		panel5.Controls.Add(MakeLabel("时长", 846, 306));
		_mergeTitleDurationNum = new NumericUpDown
		{
			Location = new Point(880, 302),
			Width = 56,
			Minimum = 0m,
			Maximum = 30m,
			Value = 4.0m,
			Increment = 0.5m,
			DecimalPlaces = 1,
			TextAlign = HorizontalAlignment.Center
		};
		panel5.Controls.Add(_mergeTitleDurationNum);
		panel5.Controls.Add(MakeLabel("秒(0=全程)", 940, 306));

		_mergeTitleTweakBtn = MakeButton("🎨 样式微调...", 96);
		_mergeTitleTweakBtn.Location = new Point(1022, 300);
		_mergeTitleTweakBtn.Height = 26;
		_mergeTitleTweakBtn.Click += delegate
		{
			ShowMergeTitleTweakDialog();
		};
		panel5.Controls.Add(_mergeTitleTweakBtn);

		_mergeTitleEnabled.CheckedChanged += delegate
		{
			UpdateMergeTitleControlsEnabled();
		};
		UpdateMergeTitleControlsEnabled();

		panel5.Controls.Add(MakeLabel("总输出目录", 20, 340));
		_outputFolder = new TextBox
		{
			Location = new Point(110, 336),
			Width = 708,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		panel5.Controls.Add(_outputFolder);
		_browseOutputButton = MakeButton("选择…", 72);
		_browseOutputButton.Location = new Point(830, 333);
		_browseOutputButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		panel5.Controls.Add(_browseOutputButton);
		_openOutputButton = MakeButton("打开", 64);
		_openOutputButton.Location = new Point(910, 333);
		_openOutputButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		panel5.Controls.Add(_openOutputButton);
		_progressBar = new ProgressBar
		{
			Location = new Point(20, 374),
			Height = 18,
			Width = 954,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		panel5.Controls.Add(_progressBar);
		_statusLabel = new Label
		{
			Location = new Point(20, 398),
			Size = new Size(954, 24),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			ForeColor = Color.FromArgb(77, 87, 101),
			AutoEllipsis = true,
			Text = "就绪：可拖入视频文件或包含视频的文件夹"
		};
		panel5.Controls.Add(_statusLabel);
		_startButton = MakePrimaryButton("开始批量合并", 20, 430, 150);
		panel5.Controls.Add(_startButton);
		_cancelButton = MakeButton("取消", 90);
		_cancelButton.Location = new Point(180, 430);
		_cancelButton.Height = 42;
		_cancelButton.Enabled = false;
		panel5.Controls.Add(_cancelButton);
		_mergePreviewButton = MakeButton("▶ 播放合并预览 (第1组)", 175);
		_mergePreviewButton.Location = new Point(280, 430);
		_mergePreviewButton.Height = 42;
		panel5.Controls.Add(_mergePreviewButton);
		Button mergeSendToCutBtn = MakeButton("🎬 发送最新成品至剪辑", 175);
		mergeSendToCutBtn.Location = new Point(465, 430);
		mergeSendToCutBtn.Height = 42;
		mergeSendToCutBtn.Tag = "accent";
		mergeSendToCutBtn.Click += delegate
		{
			SendLatestMergeToCutEditor();
		};
		panel5.Controls.Add(mergeSendToCutBtn);

		void CenterMergeActionButtons()
		{
			if (panel5.ClientSize.Width > 0 && _startButton != null && _cancelButton != null && _mergePreviewButton != null && mergeSendToCutBtn != null)
			{
				int totalW = _startButton.Width + 12 + _cancelButton.Width + 12 + _mergePreviewButton.Width + 12 + mergeSendToCutBtn.Width;
				int startX = Math.Max(20, (panel5.ClientSize.Width - totalW) / 2);
				_startButton.Left = startX;
				_cancelButton.Left = _startButton.Right + 12;
				_mergePreviewButton.Left = _cancelButton.Right + 12;
				mergeSendToCutBtn.Left = _mergePreviewButton.Right + 12;
			}
		}
		panel5.Resize += delegate { CenterMergeActionButtons(); };
		base.Shown += delegate { CenterMergeActionButtons(); };
		CenterMergeActionButtons();

		Label label = MakeLabel("每批会自动创建“日期_批量合并_编号”文件夹；原视频不会被修改。", 650, 444);
		label.ForeColor = Color.FromArgb(110, 119, 132);
		SplitContainer mergeMainSplitter = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			SplitterWidth = 7,
			BackColor = Color.FromArgb(16, 20, 28)
		};
		StyleTechSplitter(mergeMainSplitter);
		mergeMainSplitter.Panel1.Controls.Add(panel3);

		Panel mergeBottomScrollHost = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = SurfaceColor,
			Padding = new Padding(0)
		};
		panel5.Dock = DockStyle.Top;
		panel5.Height = 495;
		mergeBottomScrollHost.Controls.Add(panel5);
		mergeMainSplitter.Panel2.Controls.Add(mergeBottomScrollHost);

		tabPage.Controls.Add(mergeMainSplitter);
		mergeMainSplitter.BringToFront();

		tabPage.HandleCreated += delegate
		{
			BeginInvoke((Action)delegate
			{
				try
				{
					if (mergeMainSplitter.ClientSize.Height > 450)
					{
						mergeMainSplitter.Panel1MinSize = 100;
						mergeMainSplitter.Panel2MinSize = 120;
						int targetDist = Math.Max(120, mergeMainSplitter.ClientSize.Height - 485);
						if (targetDist >= mergeMainSplitter.Panel1MinSize &&
						    targetDist <= mergeMainSplitter.ClientSize.Height - mergeMainSplitter.Panel2MinSize)
						{
							mergeMainSplitter.SplitterDistance = targetDist;
						}
					}
				}
				catch { }
			});
		};
		UpdateMergePlanningUi();
		return tabPage;
	}

	private TabPage BuildSplitTab()
	{
		TabPage tabPage = new TabPage("视频拆分");
		tabPage.BackColor = CanvasColor;
		Panel panel = new Panel();
		panel.Dock = DockStyle.Top;
		panel.Height = 52;
		panel.Margin = new Padding(0);
		panel.Padding = new Padding(12, 9, 12, 6);
		panel.BackColor = SurfaceColor;
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
		tabPage.Controls.Add(panel2);
		Panel panel3 = MakeListHost();
		_splitVideoList = MakeVideoList();
		_splitAdjustmentEditor = MakeVideoAdjustmentEditor();
		InstallVideoListArea(panel3, _splitVideoList, _splitAdjustmentEditor);
		Panel panel4 = new Panel();
		panel4.Dock = DockStyle.Fill;
		panel4.Margin = new Padding(0);
		panel4.BackColor = SurfaceColor;
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
		SplitContainer splitTabSplitter = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			SplitterWidth = 7,
			BackColor = Color.FromArgb(16, 20, 28)
		};
		StyleTechSplitter(splitTabSplitter);
		splitTabSplitter.Panel1.Controls.Add(panel3);

		Panel splitBottomScrollHost = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = SurfaceColor,
			Padding = new Padding(0)
		};
		panel5.Dock = DockStyle.Top;
		panel5.Height = 335;
		splitBottomScrollHost.Controls.Add(panel5);
		splitTabSplitter.Panel2.Controls.Add(splitBottomScrollHost);

		tabPage.Controls.Add(splitTabSplitter);
		splitTabSplitter.BringToFront();

		tabPage.HandleCreated += delegate
		{
			BeginInvoke((Action)delegate
			{
				try
				{
					if (splitTabSplitter.ClientSize.Height > 380)
					{
						splitTabSplitter.Panel1MinSize = 100;
						splitTabSplitter.Panel2MinSize = 120;
						int targetDist = Math.Max(120, splitTabSplitter.ClientSize.Height - 345);
						if (targetDist >= splitTabSplitter.Panel1MinSize &&
						    targetDist <= splitTabSplitter.ClientSize.Height - splitTabSplitter.Panel2MinSize)
						{
							splitTabSplitter.SplitterDistance = targetDist;
						}
					}
				}
				catch { }
			});
		};
		UpdateSplitModeUi();
		return tabPage;
	}

	private TabPage BuildWatermarkTab()
	{
		TabPage tabPage = new TabPage("水印处理");
		tabPage.BackColor = CanvasColor;
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
		flowLayoutPanel.BackColor = SurfaceColor;
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
		tabPage2.BackColor = CanvasColor;
		TabPage tabPage3 = tabPage2;
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
		tableLayoutPanel3.Dock = DockStyle.Fill;
		tableLayoutPanel3.ColumnCount = 1;
		tableLayoutPanel3.RowCount = 2;
		tableLayoutPanel3.Margin = new Padding(0);
		tableLayoutPanel3.BackColor = CanvasColor;
		TableLayoutPanel tableLayoutPanel4 = tableLayoutPanel3;
		tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Absolute, 43f));
		tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		FlowLayoutPanel flowLayoutPanel3 = new FlowLayoutPanel();
		flowLayoutPanel3.Dock = DockStyle.Fill;
		flowLayoutPanel3.Padding = new Padding(12, 7, 12, 3);
		flowLayoutPanel3.WrapContents = false;
		flowLayoutPanel3.BackColor = SurfaceColor;
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
		tabPage4.BackColor = CanvasColor;
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
		flowLayoutPanel5.BackColor = SurfaceColor;
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
		tabPage.BackColor = CanvasColor;
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
		panel.BackColor = SurfaceColor;
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
		panel3.BackColor = CanvasColor;
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
		panel5.BackColor = SurfaceColor;
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

	private static GroupBox MakeGroupBox(string text, int x, int y, int width, int height)
	{
		GroupBox gb = new GroupBox();
		gb.Text = text;
		gb.Location = new Point(x, y);
		gb.Size = new Size(width, height);
		gb.BackColor = SurfaceColor;
		gb.ForeColor = _isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42);
		StyleModernGroupBox(gb);
		return gb;
	}

	private static string FormatDuration(double seconds)
	{
		if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) seconds = 0;
		TimeSpan ts = TimeSpan.FromSeconds(seconds);
		if (ts.TotalHours >= 1.0)
		{
			return string.Format("{0:00}:{1:00}:{2:00}.{3:0}", (int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds / 100);
		}
		return string.Format("{0:00}:{1:00}.{2:0}", ts.Minutes, ts.Seconds, ts.Milliseconds / 100);
	}

	private TabPage BuildCutEditTab()
	{
		TabPage tabPage = new TabPage("视频剪辑");
		tabPage.BackColor = CanvasColor;

		// Top Bar: Open video, info, and Deliver Studio shortcut
		Panel topBar = new Panel
		{
			Dock = DockStyle.Top,
			Height = 48,
			BackColor = SurfaceColor,
			Padding = new Padding(12, 6, 12, 6)
		};

		Button openVideoBtn = MakeButton("📂 导入视频素材…", 140);
		openVideoBtn.Height = 36;
		openVideoBtn.Location = new Point(12, 6);
		openVideoBtn.Click += delegate
		{
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Title = "选择需要剪辑的视频素材（支持多选）";
				ofd.Filter = "视频文件 (*.mp4;*.mov;*.mkv;*.flv;*.avi;*.wmv)|*.mp4;*.mov;*.mkv;*.flv;*.avi;*.wmv|所有文件 (*.*)|*.*";
				ofd.Multiselect = true;
				if (ofd.ShowDialog(this) == DialogResult.OK)
				{
					AddCutEditMediaPoolPaths(ofd.FileNames);
				}
			}
		};
		topBar.Controls.Add(openVideoBtn);

		Button goToDeliverBtn = MakeButton("🚀 前往【导出交付】工作台 (Deliver) →", 270);
		goToDeliverBtn.Tag = "accent";
		goToDeliverBtn.Height = 36;
		goToDeliverBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		goToDeliverBtn.Location = new Point(tabPage.ClientSize.Width - 282, 6);
		goToDeliverBtn.Click += delegate
		{
			SwitchToWorkspace(6);
		};
		topBar.Controls.Add(goToDeliverBtn);
		topBar.Resize += delegate
		{
			goToDeliverBtn.Location = new Point(topBar.ClientSize.Width - goToDeliverBtn.Width - 12, 6);
		};

		_cutEditVideoInfoLabel = new Label
		{
			Location = new Point(162, 14),
			AutoSize = true,
			Text = "尚未载入视频 | 支持在左侧【素材媒体池】中拖拽添加多个视频，或双击素材直接载入时间轴",
			ForeColor = MutedColor,
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular)
		};
		topBar.Controls.Add(_cutEditVideoInfoLabel);
		topBar.Paint += delegate(object s, PaintEventArgs e)
		{
			using (Pen p = new Pen(Color.FromArgb(40, 50, 68), 1f))
			{
				e.Graphics.DrawLine(p, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
			}
		};

		// SplitContainer: Top (Media Pool + Viewer + Title Inspector) & Bottom (Multi-track Timeline + Segments)
		SplitContainer splitMain = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			SplitterWidth = 4,
			BackColor = Color.FromArgb(20, 24, 32)
		};
		splitMain.Panel1.BackColor = CanvasColor;
		splitMain.Panel2.BackColor = SurfaceColor;

		// --- Panel1: Top section (Media Pool, Viewer, Title Inspector) ---
		// Left: Media Pool (Width: 280px, supports Images, Thumbnails & Details view, Add to Timeline options)
		Panel mediaPoolPanel = new Panel
		{
			Dock = DockStyle.Left,
			Width = 340,
			BackColor = SurfaceColor,
			Padding = new Padding(8, 6, 8, 6)
		};

		Label mpTitle = new Label
		{
			Text = "🎬 素材媒体池 (Media Pool)",
			Font = new Font("Microsoft YaHei UI", 9.75f, FontStyle.Bold),
			ForeColor = _isDarkMode ? Color.White : Color.FromArgb(15, 23, 42),
			AutoSize = true,
			Location = new Point(8, 8)
		};
		mediaPoolPanel.Controls.Add(mpTitle);

		Label mpSub = new Label
		{
			Text = "拖入视频/音频/图片素材，自由加入轨道",
			Font = new Font("Microsoft YaHei UI", 8.25f),
			ForeColor = MutedColor,
			AutoSize = true,
			Location = new Point(8, 28)
		};
		mediaPoolPanel.Controls.Add(mpSub);

		// Row 1: Import, Remove, Clear, and View mode toggle
		FlowLayoutPanel mpBar1 = new FlowLayoutPanel
		{
			Location = new Point(6, 46),
			Width = 328,
			Height = 32,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		_cutEditMediaPoolAddButton = MakeButton("＋ 导入", 58);
		_cutEditMediaPoolAddButton.Height = 28;
		_cutEditMediaPoolAddButton.Click += delegate
		{
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Title = "添加素材到媒体池（视频、音频/BGM、图片）";
				ofd.Filter = "全部媒体文件 (*.mp4;*.mov;*.mkv;*.mp3;*.wav;*.m4a;*.aac;*.jpg;*.png)|*.mp4;*.mov;*.mkv;*.flv;*.avi;*.wmv;*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.wma;*.jpg;*.jpeg;*.png;*.webp;*.bmp|视频文件 (*.mp4;*.mov;*.mkv;*.flv;*.avi;*.wmv)|*.mp4;*.mov;*.mkv;*.flv;*.avi;*.wmv|音频/BGM文件 (*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg)|*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.wma|图片文件 (*.jpg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|所有文件 (*.*)|*.*";
				ofd.Multiselect = true;
				if (ofd.ShowDialog(this) == DialogResult.OK)
				{
					AddCutEditMediaPoolPaths(ofd.FileNames);
				}
			}
		};
		mpBar1.Controls.Add(_cutEditMediaPoolAddButton);

		_cutEditMediaPoolRemoveButton = MakeButton("移除", 46);
		_cutEditMediaPoolRemoveButton.Height = 28;
		_cutEditMediaPoolRemoveButton.Margin = new Padding(3, 0, 0, 0);
		_cutEditMediaPoolRemoveButton.Click += delegate
		{
			if (_cutEditMediaPoolList.SelectedItems.Count > 0)
			{
				string path = _cutEditMediaPoolList.SelectedItems[0].Tag as string;
				if (path != null)
				{
					_cutEditMediaPool.Remove(path);
					RefreshMediaPoolList();
				}
			}
		};
		mpBar1.Controls.Add(_cutEditMediaPoolRemoveButton);

		_cutEditMediaPoolClearButton = MakeButton("清空", 46);
		_cutEditMediaPoolClearButton.Height = 28;
		_cutEditMediaPoolClearButton.Margin = new Padding(3, 0, 0, 0);
		_cutEditMediaPoolClearButton.Click += delegate
		{
			_cutEditMediaPool.Clear();
			RefreshMediaPoolList();
		};
		mpBar1.Controls.Add(_cutEditMediaPoolClearButton);

		_cutEditMediaPoolViewListBtn = MakeButton("📋 列表", 70);
		_cutEditMediaPoolViewListBtn.Height = 28;
		_cutEditMediaPoolViewListBtn.Margin = new Padding(4, 0, 0, 0);
		_cutEditMediaPoolViewListBtn.Tag = "accent";
		_cutEditMediaPoolViewListBtn.Click += delegate { SwitchMediaPoolView(false); };
		mpBar1.Controls.Add(_cutEditMediaPoolViewListBtn);

		_cutEditMediaPoolViewThumbBtn = MakeButton("🖼 缩略图", 80);
		_cutEditMediaPoolViewThumbBtn.Height = 28;
		_cutEditMediaPoolViewThumbBtn.Margin = new Padding(3, 0, 0, 0);
		_cutEditMediaPoolViewThumbBtn.Click += delegate { SwitchMediaPoolView(true); };
		mpBar1.Controls.Add(_cutEditMediaPoolViewThumbBtn);
		mediaPoolPanel.Controls.Add(mpBar1);

		// Row 2: Add to Timeline actions (Append, Insert Playhead, Insert Start)
		FlowLayoutPanel mpBar2 = new FlowLayoutPanel
		{
			Location = new Point(6, 78),
			Width = 328,
			Height = 32,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		_cutEditMediaPoolAppendEndBtn = MakeButton("➕ 末尾追加", 102);
		_cutEditMediaPoolAppendEndBtn.Tag = "accent";
		_cutEditMediaPoolAppendEndBtn.Height = 28;
		_cutEditMediaPoolAppendEndBtn.Click += delegate
		{
			InsertSelectedMediaPoolToTimeline(2); // 2: Append
		};
		mpBar2.Controls.Add(_cutEditMediaPoolAppendEndBtn);

		_cutEditMediaPoolInsertPlayheadBtn = MakeButton("⬇ 插入指针", 102);
		_cutEditMediaPoolInsertPlayheadBtn.Height = 28;
		_cutEditMediaPoolInsertPlayheadBtn.Margin = new Padding(4, 0, 0, 0);
		_cutEditMediaPoolInsertPlayheadBtn.Click += delegate
		{
			InsertSelectedMediaPoolToTimeline(1); // 1: At Playhead
		};
		mpBar2.Controls.Add(_cutEditMediaPoolInsertPlayheadBtn);

		_cutEditMediaPoolInsertStartBtn = MakeButton("⬆ 插入最前", 102);
		_cutEditMediaPoolInsertStartBtn.Height = 28;
		_cutEditMediaPoolInsertStartBtn.Margin = new Padding(4, 0, 0, 0);
		_cutEditMediaPoolInsertStartBtn.Click += delegate
		{
			InsertSelectedMediaPoolToTimeline(0); // 0: Start
		};
		mpBar2.Controls.Add(_cutEditMediaPoolInsertStartBtn);
		mediaPoolPanel.Controls.Add(mpBar2);

		_cutEditMediaPoolImageList = new ImageList
		{
			ImageSize = new Size(116, 88),
			ColorDepth = ColorDepth.Depth32Bit
		};

		_cutEditMediaPoolList = new ListView
		{
			Location = new Point(8, 114),
			Width = 324,
			Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
			View = View.Details,
			FullRowSelect = true,
			GridLines = false,
			BorderStyle = BorderStyle.None,
			BackColor = Color.FromArgb(16, 20, 28),
			ForeColor = Color.FromArgb(226, 232, 240),
			MultiSelect = false,
			AllowDrop = true,
			OwnerDraw = true,
			LargeImageList = _cutEditMediaPoolImageList,
			Font = new Font("Microsoft YaHei UI", 9f)
		};
		_cutEditMediaPoolList.Columns.Add("素材文件", 146);
		_cutEditMediaPoolList.Columns.Add("时长", 74);
		_cutEditMediaPoolList.Columns.Add("分辨率", 84);
		_cutEditMediaPoolList.DrawColumnHeader += DrawVideoListColumnHeader;
		_cutEditMediaPoolList.DrawItem += delegate(object s, DrawListViewItemEventArgs e)
		{
			if (_cutEditMediaPoolList.View == View.Details)
			{
				e.DrawDefault = true;
				return;
			}

			// Custom draw for LargeIcon Thumbnail view
			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;

			Rectangle r = e.Bounds;
			r.Inflate(-2, -2);
			bool isSelected = e.Item.Selected;

			using (var bgBrush = new SolidBrush(isSelected ? Color.FromArgb(36, 52, 78) : Color.FromArgb(24, 28, 38)))
			using (var borderPen = new Pen(isSelected ? Color.FromArgb(14, 165, 233) : Color.FromArgb(45, 52, 68), isSelected ? 2f : 1f))
			{
				g.FillRectangle(bgBrush, r);
				g.DrawRectangle(borderPen, r);
			}

			string path = e.Item.Tag as string;
			Rectangle thumbRect = new Rectangle(r.X + (r.Width - 96) / 2, r.Y + 6, 96, 64);
			Image thumbImg = null;
			lock (_cutEditMediaPoolThumbCache)
			{
				if (path != null && _cutEditMediaPoolThumbCache.TryGetValue(path, out var img))
				{
					thumbImg = img;
				}
			}

			if (thumbImg != null)
			{
				g.DrawImage(thumbImg, thumbRect);
			}
			else
			{
				using (var ph = new SolidBrush(Color.FromArgb(14, 18, 25)))
				{
					g.FillRectangle(ph, thumbRect);
				}
				using (Pen phBorder = new Pen(Color.FromArgb(40, 48, 64), 1f))
				{
					g.DrawRectangle(phBorder, thumbRect);
				}
				TextRenderer.DrawText(g, IsImage(path) ? "🖼 图片" : "🎬 视频", this.Font, thumbRect, Color.Gray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}

			// Pill duration tag on bottom-right of thumbRect
			if (e.Item.SubItems.Count > 1)
			{
				string dur = e.Item.SubItems[1].Text;
				if (!string.IsNullOrEmpty(dur) && dur != "--:--")
				{
					Size sBadge = TextRenderer.MeasureText(dur, SystemFonts.SmallCaptionFont);
					Rectangle bRect = new Rectangle(thumbRect.Right - sBadge.Width - 4, thumbRect.Bottom - sBadge.Height - 3, sBadge.Width + 4, sBadge.Height + 2);
					using (var bBg = new SolidBrush(Color.FromArgb(210, 0, 0, 0)))
					{
						g.FillRectangle(bBg, bRect);
					}
					TextRenderer.DrawText(g, dur, SystemFonts.SmallCaptionFont, bRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
				}
			}

			// Text below thumbnail
			Rectangle tRect = new Rectangle(r.X + 4, thumbRect.Bottom + 4, r.Width - 8, r.Bottom - thumbRect.Bottom - 6);
			string name = Path.GetFileName(path ?? e.Item.Text);
			TextRenderer.DrawText(g, name, this.Font, tRect, isSelected ? Color.White : Color.FromArgb(215, 222, 235), TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
		};
		_cutEditMediaPoolList.DrawSubItem += delegate(object s, DrawListViewSubItemEventArgs e)
		{
			if (_cutEditMediaPoolList.View == View.Details) e.DrawDefault = true;
		};
		_cutEditMediaPoolList.DoubleClick += delegate
		{
			InsertSelectedMediaPoolToTimeline(2); // Double click appends to timeline
		};
		_cutEditMediaPoolList.ItemDrag += delegate(object s, ItemDragEventArgs e)
		{
			if (_cutEditMediaPoolList.SelectedItems.Count > 0)
			{
				string path = _cutEditMediaPoolList.SelectedItems[0].Tag as string;
				if (!string.IsNullOrEmpty(path))
				{
					_cutEditMediaPoolList.DoDragDrop(path, DragDropEffects.Copy);
				}
			}
		};
		_cutEditMediaPoolList.DragEnter += delegate(object s, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
		};
		_cutEditMediaPoolList.DragDrop += delegate(object s, DragEventArgs e)
		{
			if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
			{
				AddCutEditMediaPoolPaths(files);
			}
		};
		mediaPoolPanel.Controls.Add(_cutEditMediaPoolList);

		// Right: Inspector / Title Designer
		// Right: Inspector / Title Designer with Modern Tabs (Directly matching Fig 3 multi-tab toolbox)
		Panel titlePanel = new Panel
		{
			Dock = DockStyle.Right,
			Width = 340,
			BackColor = SurfaceColor,
			Padding = new Padding(4, 4, 4, 4)
		};

		TabControl inspectorTabs = new TabControl
		{
			Dock = DockStyle.Fill,
			DrawMode = TabDrawMode.OwnerDrawFixed,
			ItemSize = new Size(76, 28),
			SizeMode = TabSizeMode.Fixed,
			Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold)
		};
		_cutEditInspectorTabs = inspectorTabs;
		inspectorTabs.DrawItem += delegate(object s, DrawItemEventArgs e)
		{
			bool isSel = (e.Index == inspectorTabs.SelectedIndex);
			Color bg = isSel ? Color.FromArgb(37, 99, 235) : Color.FromArgb(24, 30, 42);
			Color fg = isSel ? Color.White : Color.FromArgb(148, 163, 184);
			using (Brush b = new SolidBrush(bg))
			{
				e.Graphics.FillRectangle(b, e.Bounds);
			}
			using (Brush bText = new SolidBrush(fg))
			using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
			{
				e.Graphics.DrawString(inspectorTabs.TabPages[e.Index].Text, e.Font, bText, e.Bounds, sf);
			}
		};

		// Tab 1: 图文与贴片包装 (Text & Overlay Studio)
		TabPage tabTitle = new TabPage("🏷️ 图文贴片");
		tabTitle.BackColor = SurfaceColor;
		tabTitle.AutoScroll = true;

		// 1. Group: Overlay Item Management (List & Actions)
		GroupBox overlayListGroup = MakeGroupBox("🏷️ 图文与贴片管理 (Overlay Studio)", 4, 4, 314, 86);
		overlayListGroup.Dock = DockStyle.Top;

		_overlayItemCombo = new ComboBox
		{
			Location = new Point(12, 22),
			Width = 288,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Font = new Font("Microsoft YaHei UI", 8.5f)
		};
		_overlayItemCombo.SelectedIndexChanged += delegate
		{
			if (_updatingOverlayInspector || _overlayItemCombo.SelectedIndex < 0) return;
			if (_overlayItemCombo.SelectedIndex < _cutEditOverlays.Count)
			{
				_selectedOverlay = _cutEditOverlays[_overlayItemCombo.SelectedIndex];
				SyncSelectedOverlayToControls();
				_cutEditTimelineCanvas?.Invalidate();
				TriggerTitleLivePreview();
			}
		};
		overlayListGroup.Controls.Add(_overlayItemCombo);

		FlowLayoutPanel overlayBtnFlow = new FlowLayoutPanel
		{
			Location = new Point(12, 52),
			Width = 288,
			Height = 28,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};

		_overlayAddTextBtn = MakeButton("➕ 文案", 68);
		_overlayAddTextBtn.Height = 25;
		_overlayAddTextBtn.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
		_overlayAddTextBtn.Click += delegate
		{
			PushCutEditUndoState("添加文案包装");
			var newItem = new CutOverlayItem
			{
				Type = OverlayItemType.Text,
				Name = $"文案 #{_cutEditOverlays.Count + 1}",
				StartSeconds = Math.Max(0.0, _cutEditCurrentPos),
				Duration = 5.0,
				TextContent = "在此输入重点文字/联系方式",
				SubtitleContent = "",
				PositionPreset = "居中偏下",
				FontSize = 42,
				TextColorIndex = 0,
				StrokeIndex = 0,
				BannerBgIndex = 0
			};
			_cutEditOverlays.Add(newItem);
			RefreshOverlayCombo(newItem);
		};
		overlayBtnFlow.Controls.Add(_overlayAddTextBtn);

		_overlayAddImageBtn = MakeButton("🖼️ 贴图", 68);
		_overlayAddImageBtn.Height = 25;
		_overlayAddImageBtn.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
		_overlayAddImageBtn.Margin = new Padding(4, 0, 0, 0);
		_overlayAddImageBtn.Click += delegate
		{
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Title = "选择要叠加的图片/贴图/二维码/Logo (PNG/JPG/BMP)";
				ofd.Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件 (*.*)|*.*";
				if (ofd.ShowDialog(this) == DialogResult.OK)
				{
					PushCutEditUndoState("添加贴图包装");
					var newItem = new CutOverlayItem
					{
						Type = OverlayItemType.Image,
						Name = Path.GetFileNameWithoutExtension(ofd.FileName),
						ImagePath = ofd.FileName,
						StartSeconds = Math.Max(0.0, _cutEditCurrentPos),
						Duration = 6.0,
						PositionPreset = "右下角",
						ScalePercent = 80,
						OpacityPercent = 100
					};
					_cutEditOverlays.Add(newItem);
					RefreshOverlayCombo(newItem);
				}
			}
		};
		overlayBtnFlow.Controls.Add(_overlayAddImageBtn);

		_overlayDuplicateBtn = MakeButton("📋 复制", 68);
		_overlayDuplicateBtn.Height = 25;
		_overlayDuplicateBtn.Font = new Font("Microsoft YaHei UI", 8f);
		_overlayDuplicateBtn.Margin = new Padding(4, 0, 0, 0);
		_overlayDuplicateBtn.Click += delegate
		{
			if (_selectedOverlay != null)
			{
				PushCutEditUndoState("复制包装对象");
				var cloned = _selectedOverlay.Clone();
				_cutEditOverlays.Add(cloned);
				RefreshOverlayCombo(cloned);
			}
		};
		overlayBtnFlow.Controls.Add(_overlayDuplicateBtn);

		_overlayDeleteBtn = MakeButton("🗑️ 删除", 68);
		_overlayDeleteBtn.Height = 25;
		_overlayDeleteBtn.Font = new Font("Microsoft YaHei UI", 8f);
		_overlayDeleteBtn.Margin = new Padding(4, 0, 0, 0);
		_overlayDeleteBtn.Click += delegate
		{
			if (_cutEditOverlays.Count > 1 && _selectedOverlay != null)
			{
				PushCutEditUndoState("删除包装对象");
				int idx = _cutEditOverlays.IndexOf(_selectedOverlay);
				_cutEditOverlays.Remove(_selectedOverlay);
				int nextIdx = Math.Min(idx, _cutEditOverlays.Count - 1);
				RefreshOverlayCombo(_cutEditOverlays[nextIdx]);
			}
			else if (_cutEditOverlays.Count == 1 && _selectedOverlay != null)
			{
				PushCutEditUndoState("关闭包装对象");
				_selectedOverlay.Enabled = false;
				SyncSelectedOverlayToControls();
				_cutEditTimelineCanvas?.Invalidate();
				TriggerTitleLivePreview();
				UpdateDeliverSummary();
			}
		};
		overlayBtnFlow.Controls.Add(_overlayDeleteBtn);
		overlayListGroup.Controls.Add(overlayBtnFlow);

		// 2. Group: Current Overlay Properties
		GroupBox overlayPropsGroup = MakeGroupBox("⚙️ 当前选中对象属性配置", 4, 94, 314, 690);
		overlayPropsGroup.Dock = DockStyle.Top;

		_overlayItemEnabledCheckBox = new CheckBox
		{
			Location = new Point(14, 20),
			Text = "启用此包装对象 (仅在设定时段内呈现)",
			AutoSize = true,
			Checked = true,
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
		};
		_overlayItemEnabledCheckBox.CheckedChanged += delegate
		{
			SyncControlsToSelectedOverlay();
		};
		_cutEditTitleEnabled = _overlayItemEnabledCheckBox;
		overlayPropsGroup.Controls.Add(_overlayItemEnabledCheckBox);

		overlayPropsGroup.Controls.Add(MakeLabel("时段: 起始:", 14, 46));
		_overlayStartTimeNum = new NumericUpDown
		{
			Location = new Point(78, 44),
			Width = 56,
			Minimum = 0m,
			Maximum = 3600m,
			Value = 0m,
			DecimalPlaces = 1
		};
		_overlayStartTimeNum.ValueChanged += delegate { SyncControlsToSelectedOverlay(); };
		_cutEditTitleStartTime = _overlayStartTimeNum;
		overlayPropsGroup.Controls.Add(_overlayStartTimeNum);

		overlayPropsGroup.Controls.Add(MakeLabel("时长:", 140, 46));
		_overlayDurationNum = new NumericUpDown
		{
			Location = new Point(174, 44),
			Width = 56,
			Minimum = 0.5m,
			Maximum = 3600m,
			Value = 5m,
			DecimalPlaces = 1
		};
		_overlayDurationNum.ValueChanged += delegate { SyncControlsToSelectedOverlay(); };
		_cutEditTitleDuration = _overlayDurationNum;
		overlayPropsGroup.Controls.Add(_overlayDurationNum);

		// Timing Presets
		FlowLayoutPanel timingPresetsFlow = new FlowLayoutPanel
		{
			Location = new Point(14, 72),
			Width = 286,
			Height = 26,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		Button btnHead = MakeButton("🎬片头0s", 66);
		btnHead.Height = 22;
		btnHead.Font = new Font("Microsoft YaHei UI", 8f);
		btnHead.Click += delegate
		{
			_overlayStartTimeNum.Value = 0m;
			SyncControlsToSelectedOverlay();
		};
		timingPresetsFlow.Controls.Add(btnHead);

		Button btnMid = MakeButton("⚡片中居中", 72);
		btnMid.Height = 22;
		btnMid.Font = new Font("Microsoft YaHei UI", 8f);
		btnMid.Margin = new Padding(3, 0, 0, 0);
		btnMid.Click += delegate
		{
			double dur = Math.Max(1.0, _cutEditDuration);
			double tDur = (double)_overlayDurationNum.Value;
			double mid = Math.Max(0.0, (dur - tDur) / 2.0);
			_overlayStartTimeNum.Value = (decimal)Math.Round(mid, 1);
			SyncControlsToSelectedOverlay();
		};
		timingPresetsFlow.Controls.Add(btnMid);

		Button btnTail = MakeButton("🏁片尾", 66);
		btnTail.Height = 22;
		btnTail.Font = new Font("Microsoft YaHei UI", 8f);
		btnTail.Margin = new Padding(3, 0, 0, 0);
		btnTail.Click += delegate
		{
			double dur = Math.Max(1.0, _cutEditDuration);
			double tDur = (double)_overlayDurationNum.Value;
			double tail = Math.Max(0.0, dur - tDur);
			_overlayStartTimeNum.Value = (decimal)Math.Round(tail, 1);
			SyncControlsToSelectedOverlay();
		};
		timingPresetsFlow.Controls.Add(btnTail);

		Button btnAll = MakeButton("🌐全片", 66);
		btnAll.Height = 22;
		btnAll.Font = new Font("Microsoft YaHei UI", 8f);
		btnAll.Margin = new Padding(3, 0, 0, 0);
		btnAll.Click += delegate
		{
			_overlayStartTimeNum.Value = 0m;
			_overlayDurationNum.Value = (decimal)Math.Max(1.0, _cutEditDuration);
			SyncControlsToSelectedOverlay();
		};
		timingPresetsFlow.Controls.Add(btnAll);
		overlayPropsGroup.Controls.Add(timingPresetsFlow);

		// Layout & Position
		overlayPropsGroup.Controls.Add(MakeLabel("位置:", 14, 104));
		_overlayPositionCombo = new ComboBox
		{
			Location = new Point(48, 102),
			Width = 100,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_overlayPositionCombo.Items.AddRange(new object[]
		{
			"居中偏下",
			"居中偏上",
			"画面居中",
			"左上角",
			"右上角",
			"左下角",
			"右下角",
			"自定义坐标"
		});
		_overlayPositionCombo.SelectedIndex = 0;
		_overlayPositionCombo.SelectedIndexChanged += delegate { SyncControlsToSelectedOverlay(); };
		_cutEditTitlePositionCombo = _overlayPositionCombo;
		overlayPropsGroup.Controls.Add(_overlayPositionCombo);

		overlayPropsGroup.Controls.Add(MakeLabel("X:", 152, 104));
		_overlayOffsetXNum = new NumericUpDown
		{
			Location = new Point(168, 102),
			Width = 48,
			Minimum = -1920m,
			Maximum = 1920m,
			Value = 0m
		};
		_overlayOffsetXNum.ValueChanged += delegate { SyncControlsToSelectedOverlay(); };
		overlayPropsGroup.Controls.Add(_overlayOffsetXNum);

		overlayPropsGroup.Controls.Add(MakeLabel("Y:", 220, 104));
		_overlayOffsetYNum = new NumericUpDown
		{
			Location = new Point(236, 102),
			Width = 48,
			Minimum = -1080m,
			Maximum = 1080m,
			Value = 0m
		};
		_overlayOffsetYNum.ValueChanged += delegate { SyncControlsToSelectedOverlay(); };
		overlayPropsGroup.Controls.Add(_overlayOffsetYNum);

		// 3. Dynamic Panel: Text Overlay Properties
		_overlayTextPropsPanel = new Panel
		{
			Location = new Point(8, 130),
			Width = 298,
			Height = 460,
			BackColor = Color.Transparent
		};

		_overlayTextPropsPanel.Controls.Add(MakeLabel("设计模板样式 (一键套用):", 6, 4));
		_cutEditTitleStyle = new ComboBox
		{
			Location = new Point(6, 24),
			Width = 286,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_cutEditTitleStyle.Items.AddRange(new object[]
		{
			"🎬 经典电影宽银幕 (Cinematic Letterbox)",
			"🔥 抖音爆款醒目大字 (Punchy Social)",
			"📕 小红书流行双色风 (Redbook Dual-Color)",
			"🎤 综艺搞笑爆款大描边 (Variety Pop)",
			"📰 深度科普访谈胶囊 (Knowledge Pill)",
			"📱 手机短视频竖屏金句 (Social Quote)",
			"🎞️ 港风复古胶片感 (Hong Kong Vintage)",
			"⚡ 赛博电竞荧光霓虹 (Cyber Neon)",
			"🏷️ 纪实资讯下三分之一 (Lower Third)",
			"☕ 日系治愈极简杂志 (Japanese Minimal)",
			"🚨 紧急高能预警红牌 (Alert High-Energy)",
			"👑 商务演讲科技发布会 (Keynote Elite)"
		});
		_cutEditTitleStyle.SelectedIndex = 0;
		_cutEditTitleStyle.SelectedIndexChanged += delegate
		{
			if (_cutEditTitleStyle.SelectedIndex < 0) return;
			_updatingTitleTemplate = true;
			try
			{
				int idx = _cutEditTitleStyle.SelectedIndex;
				switch (idx)
				{
					case 0: // 🎬 经典电影宽银幕
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 0;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 2;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 0;
						SetTitleFontSize(52);
						break;
					case 1: // 🔥 抖音爆款醒目大字
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 1;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 0;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 2;
						SetTitleFontSize(68);
						break;
					case 2: // 📕 小红书流行双色风
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 0;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 0;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 5;
						SetTitleFontSize(60);
						break;
					case 3: // 🎤 综艺搞笑爆款大描边
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 1;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 1;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 1;
						SetTitleFontSize(66);
						break;
					case 4: // 📰 深度科普访谈胶囊
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 0;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 4;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 2;
						SetTitleFontSize(44);
						break;
					case 5: // 📱 手机短视频竖屏金句
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 0;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 2;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 0;
						SetTitleFontSize(54);
						break;
					case 6: // 🎞️ 港风复古胶片感
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 4;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 0;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 0;
						SetTitleFontSize(50);
						break;
					case 7: // ⚡ 赛博电竞荧光霓虹
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 2;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 0;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 3;
						SetTitleFontSize(60);
						break;
					case 8: // 🏷️ 纪实资讯下三分之一
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 0;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 3;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 2;
						SetTitleFontSize(40);
						break;
					case 9: // ☕ 日系治愈极简杂志
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 0;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 4;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 8;
						SetTitleFontSize(44);
						break;
					case 10: // 🚨 紧急高能预警红牌
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 0;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 0;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 6;
						SetTitleFontSize(64);
						break;
					case 11: // 👑 商务演讲科技发布会
						if (_cutEditTitleTextColorCombo != null) _cutEditTitleTextColorCombo.SelectedIndex = 0;
						if (_cutEditTitleStrokeCombo != null) _cutEditTitleStrokeCombo.SelectedIndex = 3;
						if (_cutEditTitleBannerBgCombo != null) _cutEditTitleBannerBgCombo.SelectedIndex = 7;
						SetTitleFontSize(46);
						break;
				}
			}
			finally
			{
				_updatingTitleTemplate = false;
			}
			SyncControlsToSelectedOverlay();
		};
		_overlayTextPropsPanel.Controls.Add(_cutEditTitleStyle);

		_overlayTextPropsPanel.Controls.Add(MakeLabel("主标题文字 (支持多行/Enter换行):", 6, 52));
		_cutEditMainTitle = new TextBox
		{
			Location = new Point(6, 70),
			Width = 286,
			Height = 44,
			Multiline = true,
			ScrollBars = ScrollBars.Vertical,
			Text = "CINEMATIC MOMENTS"
		};
		_cutEditMainTitle.TextChanged += delegate { SyncControlsToSelectedOverlay(); };
		_overlayTextPropsPanel.Controls.Add(_cutEditMainTitle);

		_overlayTextPropsPanel.Controls.Add(MakeLabel("副标题 / 说明文字:", 6, 118));
		_cutEditSubtitle = new TextBox
		{
			Location = new Point(6, 136),
			Width = 286,
			Text = "A Story of Light and Motion"
		};
		_cutEditSubtitle.TextChanged += delegate { SyncControlsToSelectedOverlay(); };
		_overlayTextPropsPanel.Controls.Add(_cutEditSubtitle);

		_cutEditTitleFontSizeLabel = MakeLabel("字号大小: 52 px (⚡ 醒目大字)", 6, 166);
		_cutEditTitleFontSizeLabel.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
		_overlayTextPropsPanel.Controls.Add(_cutEditTitleFontSizeLabel);

		_cutEditTitleFontSizeSlider = new TrackBar
		{
			Location = new Point(2, 184),
			Width = 294,
			Height = 28,
			Minimum = 20,
			Maximum = 110,
			Value = 52,
			TickFrequency = 10,
			SmallChange = 2,
			LargeChange = 10
		};
		_cutEditTitleFontSizeSlider.ValueChanged += delegate
		{
			int sz = _cutEditTitleFontSizeSlider.Value;
			_cutEditTitleFontSizeLabel.Text = $"字号大小: {sz} px ({GetTitleSizeDescription(sz)})";
			if (!_updatingTitleTemplate) SyncControlsToSelectedOverlay();
		};
		_overlayTextPropsPanel.Controls.Add(_cutEditTitleFontSizeSlider);

		FlowLayoutPanel textQuickSizeFlow = new FlowLayoutPanel
		{
			Location = new Point(6, 214),
			Width = 286,
			Height = 26,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		Button btnHuge = MakeButton("🔥巨大72", 68);
		btnHuge.Height = 22;
		btnHuge.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
		btnHuge.Click += delegate { SetTitleFontSize(72); };
		textQuickSizeFlow.Controls.Add(btnHuge);

		Button btnLarge = MakeButton("⚡大号56", 68);
		btnLarge.Height = 22;
		btnLarge.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
		btnLarge.Margin = new Padding(4, 0, 0, 0);
		btnLarge.Click += delegate { SetTitleFontSize(56); };
		textQuickSizeFlow.Controls.Add(btnLarge);

		Button btnStandard = MakeButton("✨标准42", 68);
		btnStandard.Height = 22;
		btnStandard.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
		btnStandard.Margin = new Padding(4, 0, 0, 0);
		btnStandard.Click += delegate { SetTitleFontSize(42); };
		textQuickSizeFlow.Controls.Add(btnStandard);

		Button btnModest = MakeButton("📝适中30", 68);
		btnModest.Height = 22;
		btnModest.Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold);
		btnModest.Margin = new Padding(4, 0, 0, 0);
		btnModest.Click += delegate { SetTitleFontSize(30); };
		textQuickSizeFlow.Controls.Add(btnModest);
		_overlayTextPropsPanel.Controls.Add(textQuickSizeFlow);

		_overlayTextPropsPanel.Controls.Add(MakeLabel("字体主色:", 6, 246));
		_cutEditTitleTextColorCombo = new ComboBox
		{
			Location = new Point(66, 244),
			Width = 84,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_cutEditTitleTextColorCombo.Items.AddRange(new object[]
		{
			"纯白高光",
			"醒目亮黄",
			"赛博电光青",
			"炽热醒目红",
			"尊贵暖金",
			"荧光草绿",
			"活力炫橙",
			"珊瑚粉红"
		});
		_cutEditTitleTextColorCombo.SelectedIndex = 0;
		_cutEditTitleTextColorCombo.SelectedIndexChanged += delegate { if (!_updatingTitleTemplate) SyncControlsToSelectedOverlay(); };
		_overlayTextPropsPanel.Controls.Add(_cutEditTitleTextColorCombo);

		_overlayTextPropsPanel.Controls.Add(MakeLabel("描边:", 156, 246));
		_cutEditTitleStrokeCombo = new ComboBox
		{
			Location = new Point(194, 244),
			Width = 98,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_cutEditTitleStrokeCombo.Items.AddRange(new object[]
		{
			"粗黑描边 8px",
			"重度超粗 12px",
			"清晰描边 5px",
			"精致描边 2px",
			"无描边立体阴影"
		});
		_cutEditTitleStrokeCombo.SelectedIndex = 0;
		_cutEditTitleStrokeCombo.SelectedIndexChanged += delegate { if (!_updatingTitleTemplate) SyncControlsToSelectedOverlay(); };
		_overlayTextPropsPanel.Controls.Add(_cutEditTitleStrokeCombo);

		_overlayTextPropsPanel.Controls.Add(MakeLabel("底板风格:", 6, 276));
		_cutEditTitleBannerBgCombo = new ComboBox
		{
			Location = new Point(66, 274),
			Width = 226,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_cutEditTitleBannerBgCombo.Items.AddRange(new object[]
		{
			"🎬 电影宽银幕暗影 (渐变暗角底栏)",
			"🔥 亮黄高对比底牌 (短视频爆款)",
			"💎 磨砂半透黑金卡片 (现代轻奢)",
			"⚡ 赛博电光描边外框 (炫酷科技)",
			"⬛ 纯黑全屏开场底板 (片头独立呈现)",
			"📕 小红书磨砂胶囊 (双色流行风)",
			"🚨 紧急高能红牌底板 (预警爆款)",
			"🌊 科技深蓝商务渐变条 (发布会演讲)",
			"🚫 无底板 (纯大字+立体描边)"
		});
		_cutEditTitleBannerBgCombo.SelectedIndex = 0;
		_cutEditTitleBannerBgCombo.SelectedIndexChanged += delegate { if (!_updatingTitleTemplate) SyncControlsToSelectedOverlay(); };
		_overlayTextPropsPanel.Controls.Add(_cutEditTitleBannerBgCombo);

		_cutEditTitleOverlayRadio = new RadioButton
		{
			Location = new Point(6, 306),
			Text = "动态叠印在原视频画面上",
			AutoSize = true,
			Checked = true
		};
		_cutEditTitleOverlayRadio.CheckedChanged += delegate { if (_cutEditTitleOverlayRadio.Checked) SyncControlsToSelectedOverlay(); };
		_overlayTextPropsPanel.Controls.Add(_cutEditTitleOverlayRadio);

		_cutEditTitleCardRadio = new RadioButton
		{
			Location = new Point(6, 328),
			Text = "纯黑质感片头独立展示",
			AutoSize = true,
			Checked = false
		};
		_cutEditTitleCardRadio.CheckedChanged += delegate { if (_cutEditTitleCardRadio.Checked) SyncControlsToSelectedOverlay(); };
		_overlayTextPropsPanel.Controls.Add(_cutEditTitleCardRadio);
		overlayPropsGroup.Controls.Add(_overlayTextPropsPanel);

		// 4. Dynamic Panel: Image Overlay Properties
		_overlayImagePropsPanel = new Panel
		{
			Location = new Point(8, 130),
			Width = 298,
			Height = 460,
			BackColor = Color.Transparent,
			Visible = false
		};

		_overlayImagePropsPanel.Controls.Add(MakeLabel("贴图/二维码图片文件:", 6, 4));
		_overlayImagePathText = new TextBox
		{
			Location = new Point(6, 24),
			Width = 208,
			ReadOnly = true
		};
		_overlayImagePropsPanel.Controls.Add(_overlayImagePathText);

		Button btnBrowseImg = MakeButton("浏览…", 70);
		btnBrowseImg.Location = new Point(220, 22);
		btnBrowseImg.Height = 26;
		btnBrowseImg.Click += delegate
		{
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Title = "选择贴图/二维码/Logo图片";
				ofd.Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件 (*.*)|*.*";
				if (ofd.ShowDialog(this) == DialogResult.OK)
				{
					_overlayImagePathText.Text = ofd.FileName;
					if (_selectedOverlay != null)
					{
						_selectedOverlay.ImagePath = ofd.FileName;
						_selectedOverlay.Name = Path.GetFileNameWithoutExtension(ofd.FileName);
					}
					UpdateOverlayImageThumb(ofd.FileName);
					SyncControlsToSelectedOverlay();
				}
			}
		};
		_overlayImagePropsPanel.Controls.Add(btnBrowseImg);

		_overlayImageThumbBox = new PictureBox
		{
			Location = new Point(6, 56),
			Size = new Size(64, 64),
			SizeMode = PictureBoxSizeMode.Zoom,
			BorderStyle = BorderStyle.FixedSingle,
			BackColor = Color.FromArgb(16, 20, 28)
		};
		_overlayImagePropsPanel.Controls.Add(_overlayImageThumbBox);

		_overlayImageScaleLabel = MakeLabel("缩放尺寸: 80%", 78, 56);
		_overlayImageScaleLabel.ForeColor = AccentColor;
		_overlayImageScaleLabel.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		_overlayImagePropsPanel.Controls.Add(_overlayImageScaleLabel);

		_overlayImageScaleSlider = new TrackBar
		{
			Location = new Point(74, 80),
			Width = 218,
			Height = 30,
			Minimum = 10,
			Maximum = 300,
			Value = 80,
			TickStyle = TickStyle.None
		};
		_overlayImageScaleSlider.ValueChanged += delegate
		{
			_overlayImageScaleLabel.Text = $"缩放尺寸: {_overlayImageScaleSlider.Value}%";
			SyncControlsToSelectedOverlay();
		};
		_overlayImagePropsPanel.Controls.Add(_overlayImageScaleSlider);

		FlowLayoutPanel imgScaleFlow = new FlowLayoutPanel
		{
			Location = new Point(6, 126),
			Width = 286,
			Height = 28,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		int[] scalePresets = new int[] { 40, 70, 100, 150 };
		string[] scaleLabels = new string[] { "角标40%", "卡片70%", "原图100%", "放大150%" };
		for (int i = 0; i < scalePresets.Length; i++)
		{
			int sp = scalePresets[i];
			Button btnSp = MakeButton(scaleLabels[i], 68);
			btnSp.Height = 24;
			btnSp.Font = new Font("Microsoft YaHei UI", 8f);
			btnSp.Margin = new Padding(0, 0, 4, 0);
			btnSp.Click += delegate { _overlayImageScaleSlider.Value = sp; };
			imgScaleFlow.Controls.Add(btnSp);
		}
		_overlayImagePropsPanel.Controls.Add(imgScaleFlow);

		_overlayImageOpacityLabel = MakeLabel("不透明度: 100%", 6, 160);
		_overlayImagePropsPanel.Controls.Add(_overlayImageOpacityLabel);

		_overlayImageOpacitySlider = new TrackBar
		{
			Location = new Point(4, 180),
			Width = 288,
			Height = 30,
			Minimum = 10,
			Maximum = 100,
			Value = 100,
			TickStyle = TickStyle.None
		};
		_overlayImageOpacitySlider.ValueChanged += delegate
		{
			_overlayImageOpacityLabel.Text = $"不透明度: {_overlayImageOpacitySlider.Value}%";
			SyncControlsToSelectedOverlay();
		};
		_overlayImagePropsPanel.Controls.Add(_overlayImageOpacitySlider);

		Label imgHint = MakeLabel("💡 支持 PNG 透明通道、JPG、WebP 等。\n可将二维码、联系方式名片或Logo置于画面任意角落，并自由调节大小与呈现时段。", 6, 222);
		imgHint.Size = new Size(286, 60);
		imgHint.ForeColor = MutedColor;
		imgHint.Font = new Font("Microsoft YaHei UI", 8f);
		_overlayImagePropsPanel.Controls.Add(imgHint);
		overlayPropsGroup.Controls.Add(_overlayImagePropsPanel);

		// Bottom Buttons in overlayPropsGroup
		_cutEditTitlePreviewButton = MakeButton("👁️ 预览当前设计效果", 286);
		_cutEditTitlePreviewButton.Location = new Point(14, 604);
		_cutEditTitlePreviewButton.Height = 32;
		_cutEditTitlePreviewButton.Tag = "accent";
		_cutEditTitlePreviewButton.Click += delegate
		{
			if (_cutEditSegments.Count == 0 && (string.IsNullOrEmpty(_cutEditSourcePath) || !File.Exists(_cutEditSourcePath)))
			{
				MessageBox.Show(this, "请先在左侧媒体池或轨道中添加素材！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			if (_selectedOverlay != null)
			{
				_cutEditCurrentPos = _selectedOverlay.StartSeconds;
				if (_cutEditTimeScrubber != null && _cutEditDuration > 0.0)
				{
					_cutEditTimeScrubber.Value = (int)((_cutEditCurrentPos / _cutEditDuration) * 1000);
				}
				UpdateCutEditTimeLabel();
				_cutEditTimelineCanvas?.Invalidate();
				TriggerTitleLivePreview();
			}
		};
		overlayPropsGroup.Controls.Add(_cutEditTitlePreviewButton);

		Button btnPlaySeg = MakeButton("▶ 跳至此时段并播放", 286);
		btnPlaySeg.Location = new Point(14, 642);
		btnPlaySeg.Height = 28;
		btnPlaySeg.Click += delegate
		{
			if (_selectedOverlay != null)
			{
				_cutEditCurrentPos = _selectedOverlay.StartSeconds;
				SyncPlayerAtCurrentPos(forceReload: false);
				if (!_cutEditIsPlaying) ToggleCutEditPlayPause();
			}
		};
		overlayPropsGroup.Controls.Add(btnPlaySeg);

		tabTitle.Controls.Add(overlayPropsGroup);
		tabTitle.Controls.Add(overlayListGroup);
		overlayListGroup.SendToBack();
		inspectorTabs.TabPages.Add(tabTitle);

		InitDefaultOverlays();
		RefreshOverlayCombo();

		// Tab 2: 镜头转场 (精简移除样式特效与视听混流，升级镜头转场面板)
		TabPage tabCut = new TabPage("⚡ 镜头转场");
		tabCut.BackColor = SurfaceColor;
		tabCut.AutoScroll = true;
		GroupBox cutGroup = MakeGroupBox("片段过渡转场与镜头特效", 4, 4, 314, 380);
		cutGroup.Dock = DockStyle.Fill;
		cutGroup.Controls.Add(MakeLabel("转场过渡特效类型 (默认无转场):", 14, 28));

		ComboBox transCombo = new ComboBox { Location = new Point(14, 48), Width = 286, DropDownStyle = ComboBoxStyle.DropDownList };
		foreach (var td in TransitionDefinitions)
		{
			transCombo.Items.Add(td.name);
		}
		transCombo.SelectedIndex = 0; // Default: none
		cutGroup.Controls.Add(transCombo);

		cutGroup.Controls.Add(MakeLabel("转场持续时间 (秒):", 14, 84));
		ComboBox transDurCombo = new ComboBox { Location = new Point(14, 104), Width = 286, DropDownStyle = ComboBoxStyle.DropDownList };
		transDurCombo.Items.AddRange(new object[] { "0.3 秒 (快捷轻快)", "0.5 秒 (自然标准推荐)", "0.8 秒 (舒缓)", "1.0 秒 (平滑)", "1.5 秒 (抒情漫长)", "2.0 秒 (超长淡化)" });
		transDurCombo.SelectedIndex = 1;
		cutGroup.Controls.Add(transDurCombo);

		Button btnApplyIn = MakeButton("⚡ 应用到当前选中片段【片头】", 286);
		btnApplyIn.Location = new Point(14, 142);
		btnApplyIn.Height = 32;
		btnApplyIn.Tag = "accent";
		btnApplyIn.Click += delegate
		{
			if (_cutEditSelectedSegmentIndex >= 0 && _cutEditSelectedSegmentIndex < _cutEditSegments.Count)
			{
				var seg = _cutEditSegments[_cutEditSelectedSegmentIndex];
				int selIdx = transCombo.SelectedIndex;
				if (selIdx >= 0 && selIdx < TransitionDefinitions.Length)
				{
					seg.TransitionInType = TransitionDefinitions[selIdx].id;
					seg.TransitionInDuration = transDurCombo.SelectedIndex switch { 0 => 0.3, 1 => 0.5, 2 => 0.8, 3 => 1.0, 4 => 1.5, _ => 2.0 };
					_cutEditTimelineCanvas?.Invalidate();
					UpdateDeliverSummary();
					MessageBox.Show(this, $"已将【{TransitionDefinitions[selIdx].name}】应用于当前片段片头！", "转场已应用", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
			else
			{
				MessageBox.Show(this, "请先在时间线轨道上点击选中一个视频片段！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		};
		cutGroup.Controls.Add(btnApplyIn);

		Button btnApplyOut = MakeButton("⚡ 应用到当前选中片段【片尾】", 286);
		btnApplyOut.Location = new Point(14, 180);
		btnApplyOut.Height = 32;
		btnApplyOut.Click += delegate
		{
			if (_cutEditSelectedSegmentIndex >= 0 && _cutEditSelectedSegmentIndex < _cutEditSegments.Count)
			{
				var seg = _cutEditSegments[_cutEditSelectedSegmentIndex];
				int selIdx = transCombo.SelectedIndex;
				if (selIdx >= 0 && selIdx < TransitionDefinitions.Length)
				{
					seg.TransitionOutType = TransitionDefinitions[selIdx].id;
					seg.TransitionOutDuration = transDurCombo.SelectedIndex switch { 0 => 0.3, 1 => 0.5, 2 => 0.8, 3 => 1.0, 4 => 1.5, _ => 2.0 };
					_cutEditTimelineCanvas?.Invalidate();
					UpdateDeliverSummary();
					MessageBox.Show(this, $"已将【{TransitionDefinitions[selIdx].name}】应用于当前片段片尾！", "转场已应用", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
			else
			{
				MessageBox.Show(this, "请先在时间线轨道上点击选中一个视频片段！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		};
		cutGroup.Controls.Add(btnApplyOut);

		Label lblTip = MakeLabel("💡 快捷技巧：\n也可直接在时间线片段上【鼠标右键】，\n在右键菜单中一键添加多种转场与音视频分离！", 14, 222);
		lblTip.Size = new Size(286, 50);
		lblTip.ForeColor = Color.FromArgb(56, 189, 248);
		cutGroup.Controls.Add(lblTip);

		Button btnAddWatermark = MakeButton("🖼️ 载入台标/水印PNG徽标…", 286);
		btnAddWatermark.Location = new Point(14, 280);
		btnAddWatermark.Height = 32;
		btnAddWatermark.Click += delegate
		{
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Title = "选择透明PNG水印徽标";
				ofd.Filter = "PNG图片 (*.png)|*.png|所有图片 (*.*)|*.*";
				if (ofd.ShowDialog(this) == DialogResult.OK)
				{
					MessageBox.Show(this, $"已载入水印徽标: {Path.GetFileName(ofd.FileName)}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
		};
		cutGroup.Controls.Add(btnAddWatermark);
		tabCut.Controls.Add(cutGroup);
		inspectorTabs.TabPages.Add(tabCut);

		titlePanel.Controls.Add(inspectorTabs);

		// Center: Main Viewer (Hardware-accelerated WPF Player + Dual Preview + Fig 3 Transport & Subtitle Sliders)
		Panel centerViewer = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(12, 16, 24),
			Padding = new Padding(4, 4, 4, 4)
		};

		// 0. Top: Monitor Header Bar (工作预览区标题栏与弹出独立窗按钮)
		Panel previewHeaderBar = new Panel
		{
			Dock = DockStyle.Top,
			Height = 36,
			BackColor = Color.FromArgb(18, 24, 34),
			Padding = new Padding(10, 4, 10, 4)
		};

		Label previewHeaderTitle = new Label
		{
			Text = "🖥️ 工作预览区 · 实时预审与字幕设计",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(56, 189, 248),
			AutoSize = true,
			Location = new Point(10, 8)
		};
		previewHeaderBar.Controls.Add(previewHeaderTitle);

		Label previewHeaderTip = new Label
		{
			Text = "双击画面：弹出独立预览窗口 | 实时音画与字幕对齐预审",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(148, 163, 184),
			AutoSize = true,
			Location = new Point(270, 9)
		};
		previewHeaderBar.Controls.Add(previewHeaderTip);

		_cutEditPopoutPreviewHeaderBtn = MakeButton("🗗 弹出独立预览窗", 145);
		_cutEditPopoutPreviewHeaderBtn.Tag = "accent";
		_cutEditPopoutPreviewHeaderBtn.Height = 28;
		_cutEditPopoutPreviewHeaderBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_cutEditPopoutPreviewHeaderBtn.Location = new Point(centerViewer.ClientSize.Width - 155, 4);
		_cutEditPopoutPreviewHeaderBtn.Click += delegate { OpenCutEditPopoutPreview(); };
		previewHeaderBar.Controls.Add(_cutEditPopoutPreviewHeaderBtn);
		previewHeaderBar.Resize += delegate
		{
			_cutEditPopoutPreviewHeaderBtn.Location = new Point(previewHeaderBar.ClientSize.Width - _cutEditPopoutPreviewHeaderBtn.Width - 10, 4);
		};

		// 1. Bottom: Subtitle & Overlay Position Bar (Directly matching Fig 3)
		Panel subtitlePosBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 36,
			BackColor = Color.FromArgb(16, 20, 28),
			Padding = new Padding(10, 4, 10, 4)
		};

		Label subPosLbl = new Label
		{
			Text = "字幕上下位置: 低",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(203, 213, 225),
			AutoSize = true,
			Location = new Point(8, 9)
		};
		subtitlePosBar.Controls.Add(subPosLbl);

		_cutEditSubtitlePosTrackBar = new TrackBar
		{
			Location = new Point(122, 4),
			Width = 160,
			Height = 26,
			Minimum = 20,
			Maximum = 800,
			Value = _cutEditSubtitleBottomOffset,
			TickStyle = TickStyle.None,
			Cursor = Cursors.Hand
		};
		_cutEditSubtitlePosTrackBar.ValueChanged += delegate
		{
			SetCutEditSubtitleBottomOffset(_cutEditSubtitlePosTrackBar.Value);
		};
		subtitlePosBar.Controls.Add(_cutEditSubtitlePosTrackBar);

		_cutEditSubtitlePosLabel = new Label
		{
			Text = $"高  距离底: {_cutEditSubtitleBottomOffset} px",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(56, 189, 248),
			AutoSize = true,
			Location = new Point(288, 9)
		};
		subtitlePosBar.Controls.Add(_cutEditSubtitlePosLabel);

		_cutEditRealtimePreviewCheckBox = new CheckBox
		{
			Text = "☑ 实时显示效果 (音画同步/视效位置)",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(226, 232, 240),
			AutoSize = true,
			Checked = true,
			Location = new Point(415, 8)
		};
		_cutEditRealtimePreviewCheckBox.CheckedChanged += delegate
		{
			UpdateCutEditPreviewFrame(_cutEditCurrentPos, _cutEditRealtimePreviewCheckBox.Checked);
		};
		subtitlePosBar.Controls.Add(_cutEditRealtimePreviewCheckBox);

		Button resetSubPosBtn = MakeButton("↺ 还原位置", 82);
		resetSubPosBtn.Height = 26;
		resetSubPosBtn.Location = new Point(660, 4);
		resetSubPosBtn.Click += delegate
		{
			SetCutEditSubtitleBottomOffset(407);
		};
		subtitlePosBar.Controls.Add(resetSubPosBtn);

		Button refreshPreviewBtn = MakeButton("👁 刷新预审", 82);
		refreshPreviewBtn.Height = 26;
		refreshPreviewBtn.Location = new Point(748, 4);
		refreshPreviewBtn.Click += delegate
		{
			UpdateCutEditPreviewFrame(_cutEditCurrentPos, true);
		};
		subtitlePosBar.Controls.Add(refreshPreviewBtn);

		// 2. Transport Controls Bar
		Panel transportBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 44,
			BackColor = Color.FromArgb(18, 22, 32),
			Padding = new Padding(8, 4, 8, 4)
		};

		// Left of Transport: Glowing LCD Timecode badge
		_cutEditTimeLabel = new Label
		{
			Text = "00:00.0 / 00:00.0",
			ForeColor = Color.FromArgb(56, 189, 248),
			Font = new Font("Consolas", 10.5f, FontStyle.Bold),
			AutoSize = true,
			Location = new Point(10, 11)
		};
		transportBar.Controls.Add(_cutEditTimeLabel);

		FlowLayoutPanel transportBtnFlow = new FlowLayoutPanel
		{
			Height = 36,
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			Location = new Point(190, 4)
		};

		_cutEditStepBack5Btn = MakeButton("⏪ -5s", 58);
		_cutEditStepBack5Btn.Height = 32;
		_cutEditStepBack5Btn.Click += delegate { StepCutEditTime(-5.0); };
		transportBtnFlow.Controls.Add(_cutEditStepBack5Btn);

		_cutEditStepBack1Btn = MakeButton("◀ -1s", 52);
		_cutEditStepBack1Btn.Height = 32;
		_cutEditStepBack1Btn.Margin = new Padding(3, 0, 0, 0);
		_cutEditStepBack1Btn.Click += delegate { StepCutEditTime(-1.0); };
		transportBtnFlow.Controls.Add(_cutEditStepBack1Btn);

		_cutEditPlayPauseButton = MakeButton("▶ 播放 (空格)", 115);
		_cutEditPlayPauseButton.Tag = "accent";
		_cutEditPlayPauseButton.Height = 32;
		_cutEditPlayPauseButton.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
		_cutEditPlayPauseButton.Margin = new Padding(4, 0, 0, 0);
		_cutEditPlayPauseButton.Click += delegate { ToggleCutEditPlayPause(); };
		transportBtnFlow.Controls.Add(_cutEditPlayPauseButton);

		_cutEditStepForward1Btn = MakeButton("+1s ▶", 52);
		_cutEditStepForward1Btn.Height = 32;
		_cutEditStepForward1Btn.Margin = new Padding(4, 0, 0, 0);
		_cutEditStepForward1Btn.Click += delegate { StepCutEditTime(1.0); };
		transportBtnFlow.Controls.Add(_cutEditStepForward1Btn);

		_cutEditStepForward5Btn = MakeButton("+5s ⏩", 58);
		_cutEditStepForward5Btn.Height = 32;
		_cutEditStepForward5Btn.Margin = new Padding(3, 0, 0, 0);
		_cutEditStepForward5Btn.Click += delegate { StepCutEditTime(5.0); };
		transportBtnFlow.Controls.Add(_cutEditStepForward5Btn);

		_cutEditPopoutPreviewBtn = MakeButton("🗗 弹出工作区", 108);
		_cutEditPopoutPreviewBtn.Height = 32;
		_cutEditPopoutPreviewBtn.Margin = new Padding(4, 0, 0, 0);
		_cutEditPopoutPreviewBtn.Click += delegate { OpenCutEditPopoutPreview(); };
		transportBtnFlow.Controls.Add(_cutEditPopoutPreviewBtn);

		transportBar.Controls.Add(transportBtnFlow);

		// Right of Transport: Volume & Loop
		Panel transportRightPanel = new Panel
		{
			Dock = DockStyle.Right,
			Width = 190,
			Height = 36,
			BackColor = Color.Transparent
		};

		_cutEditLoopCheckBox = new CheckBox
		{
			Text = "🔁 循环",
			AutoSize = true,
			Checked = true,
			Location = new Point(4, 10),
			ForeColor = Color.FromArgb(200, 210, 225),
			Font = new Font("Microsoft YaHei UI", 8.5f)
		};
		transportRightPanel.Controls.Add(_cutEditLoopCheckBox);

		_cutEditMuteBtn = new Button
		{
			Text = "🔊",
			Size = new Size(32, 28),
			Location = new Point(78, 6),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 45, 60),
			ForeColor = Color.White,
			Font = new Font("Segoe UI Emoji", 9.5f)
		};
		_cutEditMuteBtn.FlatAppearance.BorderSize = 0;
		_cutEditMuteBtn.Click += delegate
		{
			if (_cutEditMediaElement != null)
			{
				_cutEditMediaElement.IsMuted = !_cutEditMediaElement.IsMuted;
				_cutEditMuteBtn.Text = _cutEditMediaElement.IsMuted ? "🔇" : "🔊";
				_cutEditAudioMuted = _cutEditMediaElement.IsMuted;
				if (_cutEditKeepOriginalAudioCheckBox != null) _cutEditKeepOriginalAudioCheckBox.Checked = !_cutEditAudioMuted;
			}
		};
		transportRightPanel.Controls.Add(_cutEditMuteBtn);

		_cutEditVolumeTrackBar = new TrackBar
		{
			Location = new Point(114, 4),
			Width = 72,
			Height = 30,
			Minimum = 0,
			Maximum = 100,
			Value = 100,
			TickStyle = TickStyle.None
		};
		_cutEditVolumeTrackBar.ValueChanged += delegate
		{
			if (_cutEditMediaElement != null)
			{
				double vol = _cutEditVolumeTrackBar.Value / 100.0;
				_cutEditMediaElement.Volume = vol;
				_cutEditMuteBtn.Text = (vol <= 0) ? "🔇" : "🔊";
			}
		};
		transportRightPanel.Controls.Add(_cutEditVolumeTrackBar);
		transportBar.Controls.Add(transportRightPanel);

		transportBar.Resize += delegate
		{
			int mid = (transportBar.ClientSize.Width - transportBtnFlow.Width) / 2;
			transportBtnFlow.Location = new Point(Math.Max(_cutEditTimeLabel.Right + 10, mid), 4);
		};

		// 3. Scrubber Trackbar Bar
		Panel scrubberPanel = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 28,
			BackColor = Color.FromArgb(14, 18, 26),
			Padding = new Padding(6, 2, 6, 2)
		};

		_cutEditTimeScrubber = new TrackBar
		{
			Dock = DockStyle.Fill,
			Height = 26,
			Minimum = 0,
			Maximum = 1000,
			Value = 0,
			TickStyle = TickStyle.None,
			Cursor = Cursors.Hand
		};
		_cutEditTimeScrubber.MouseDown += (s, e) => { _cutEditIsDraggingScrubber = true; };
		_cutEditTimeScrubber.MouseUp += (s, e) =>
		{
			_cutEditIsDraggingScrubber = false;
			if (_cutEditDuration > 0.0)
			{
				double targetSec = (_cutEditTimeScrubber.Value / 1000.0) * _cutEditDuration;
				SeekCutEditVideo(targetSec);
			}
		};
		_cutEditTimeScrubber.Scroll += delegate
		{
			if (_cutEditDuration > 0.0)
			{
				SeekCutEditVideo((_cutEditTimeScrubber.Value / 1000.0) * _cutEditDuration);
			}
		};
		scrubberPanel.Controls.Add(_cutEditTimeScrubber);

		// 4. Center Video Host Panel
		Panel videoHostPanel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(10, 14, 20)
		};
		_cutEditVideoHostPanel = videoHostPanel;

		_cutEditPreviewBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.FromArgb(10, 14, 20),
			Visible = false,
			Cursor = Cursors.Hand
		};
		_cutEditPreviewBox.DoubleClick += delegate
		{
			OpenCutEditPopoutPreview();
		};
		typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(_cutEditPreviewBox, true);

		_cutEditElementHost = new System.Windows.Forms.Integration.ElementHost
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(10, 14, 20)
		};

		_cutEditMediaElement = new System.Windows.Controls.MediaElement
		{
			LoadedBehavior = System.Windows.Controls.MediaState.Manual,
			UnloadedBehavior = System.Windows.Controls.MediaState.Manual,
			Stretch = System.Windows.Media.Stretch.Uniform,
			ScrubbingEnabled = true
		};
		_cutEditMediaElement.MediaOpened += CutEditMediaElement_MediaOpened;
		_cutEditMediaElement.MediaEnded += CutEditMediaElement_MediaEnded;
		_cutEditMediaElement.MediaFailed += CutEditMediaElement_MediaFailed;
		_cutEditMediaElement.MouseDown += (s, e) =>
		{
			if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
			{
				ToggleCutEditPlayPause();
			}
		};

		_cutEditWpfOverlayImage = new System.Windows.Controls.Image
		{
			Stretch = System.Windows.Media.Stretch.Uniform,
			IsHitTestVisible = false
		};

		_cutEditWpfTransitionBorder = new System.Windows.Controls.Border
		{
			IsHitTestVisible = false,
			Visibility = System.Windows.Visibility.Collapsed
		};

		System.Windows.Controls.Grid cutEditGrid = new System.Windows.Controls.Grid();
		cutEditGrid.Children.Add(_cutEditMediaElement);
		cutEditGrid.Children.Add(_cutEditWpfTransitionBorder);
		cutEditGrid.Children.Add(_cutEditWpfOverlayImage);
		_cutEditElementHost.Child = cutEditGrid;

		_cutEditEmptyPlaceholder = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			Text = "🎬 专业多轨剪辑工作台\n\n请在左侧【素材媒体池】中拖拽或双击载入视频\n支持空格键播放/暂停、I / O 设置出入点、B 剃刀切割片段",
			ForeColor = Color.FromArgb(120, 136, 160),
			Font = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Regular),
			BackColor = Color.FromArgb(12, 16, 24)
		};

		videoHostPanel.Controls.Add(_cutEditPreviewBox);
		videoHostPanel.Controls.Add(_cutEditElementHost);
		videoHostPanel.Controls.Add(_cutEditEmptyPlaceholder);
		_cutEditElementHost.Visible = false;

		centerViewer.Controls.Add(videoHostPanel);
		centerViewer.Controls.Add(scrubberPanel);
		centerViewer.Controls.Add(transportBar);
		centerViewer.Controls.Add(subtitlePosBar);
		centerViewer.Controls.Add(previewHeaderBar);
		previewHeaderBar.SendToBack();
		subtitlePosBar.SendToBack();
		transportBar.SendToBack();
		scrubberPanel.SendToBack();

		splitMain.Panel1.Controls.Add(centerViewer);
		splitMain.Panel1.Controls.Add(mediaPoolPanel);
		splitMain.Panel1.Controls.Add(titlePanel);

		// Play Timer setup - High Precision Master Clock Architecture
		_cutEditPlayTimer = new System.Windows.Forms.Timer { Interval = 33 };
		_cutEditPlayTimer.Tick += delegate
		{
			if (!_cutEditIsPlaying || _cutEditIsDraggingScrubber || _cutEditDuration <= 0.0)
				return;

			double elapsed = _cutEditPlaybackSw.Elapsed.TotalSeconds;
			_cutEditCurrentPos = _cutEditPlaybackStartPos + elapsed;

			if (_cutEditCurrentPos >= _cutEditDuration)
			{
				if (_cutEditLoopCheckBox?.Checked == true)
				{
					_cutEditCurrentPos = 0.0;
					_cutEditPlaybackStartPos = 0.0;
					_cutEditPlaybackSw.Restart();
					_lastActiveSegment = null;
					SyncPlayerAtCurrentPos(forceReload: false);
				}
				else
				{
					_cutEditCurrentPos = _cutEditDuration;
					ToggleCutEditPlayPause();
					return;
				}
			}

			var activeSeg = GetActiveVideoSegmentAtTime(_cutEditCurrentPos);
			if (activeSeg != _lastActiveSegment)
			{
				_lastActiveSegment = activeSeg;
				SyncPlayerAtCurrentPos(forceReload: false);
			}
			else if (activeSeg != null && activeSeg.MediaType != "image" && _cutEditMediaElement != null)
			{
				double inSourceSec = activeSeg.StartSeconds + Math.Max(0.0, _cutEditCurrentPos - activeSeg.TimelineStartSeconds);
				TimeSpan targetPos = TimeSpan.FromSeconds(Math.Max(0.0, inSourceSec));
				if (Math.Abs((_cutEditMediaElement.Position - targetPos).TotalSeconds) > 0.35)
				{
					_cutEditMediaElement.Position = targetPos;
				}
			}

			// Update Scrubber TrackBar only when position changed
			if (_cutEditTimeScrubber != null && _cutEditDuration > 0.0)
			{
				int scrubVal = (int)Math.Max(0, Math.Min(1000, (_cutEditCurrentPos / _cutEditDuration) * 1000.0));
				if (_cutEditTimeScrubber.Value != scrubVal)
				{
					_cutEditTimeScrubber.Value = scrubVal;
				}
			}

			UpdateCutEditTimeLabel();
			UpdateCutEditWpfOverlay(_cutEditCurrentPos, forcePreviewSelected: false);
			_cutEditTimelineCanvas?.Invalidate();

			if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed)
			{
				_cutEditPopoutForm.UpdateTimeAndScrubber(_cutEditCurrentPos, _cutEditDuration, _cutEditIsPlaying);
			}
		};

		// --- Panel2: Bottom section (Multi-track Timeline Canvas - Full Workspace) ---
		Panel bottomTimelineHost = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = SurfaceColor,
			Padding = new Padding(8, 4, 8, 4)
		};

		// Timeline Tools Bar
		Panel timelineTools = new Panel
		{
			Dock = DockStyle.Top,
			Height = 40,
			BackColor = SurfaceColor
		};

		FlowLayoutPanel tlToolsLeft = new FlowLayoutPanel
		{
			Dock = DockStyle.Left,
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			Padding = new Padding(0, 4, 0, 4)
		};

		_cutEditToolTip = new ToolTip
		{
			InitialDelay = 150,
			ReshowDelay = 100,
			AutoPopDelay = 10000,
			ShowAlways = true
		};

		Control MakeDivider() => new Panel { Width = 1, Height = 18, BackColor = Color.FromArgb(71, 85, 105), Margin = new Padding(3, 6, 3, 0) };

		// Group 1: Editing & trimming tools
		_cutEditToolSelectBtn = MakeTimelineIconButton("↖", "选择工具 (快捷键: V)\n用于在轨道上选择和拖动素材片段", 32);
		_cutEditToolSelectBtn.BackColor = Color.FromArgb(14, 165, 233);
		_cutEditToolSelectBtn.ForeColor = Color.White;
		_cutEditToolSelectBtn.Click += delegate { SetTimelineToolMode(TimelineToolMode.Select); };
		tlToolsLeft.Controls.Add(_cutEditToolSelectBtn);

		_cutEditToolRazorBtn = MakeTimelineIconButton("✂", "剃刀切片工具 (快捷键: C)\n在鼠标点击位置快速分割切断素材", 32);
		_cutEditToolRazorBtn.Click += delegate { SetTimelineToolMode(TimelineToolMode.Razor); };
		tlToolsLeft.Controls.Add(_cutEditToolRazorBtn);

		_cutEditSplitButton = MakeTimelineIconButton("✁", "当前位置切片 (快捷键: B)\n在当前播放头指针处截断选中片段", 32);
		_cutEditSplitButton.Click += delegate { SplitCutEditCurrentPosition(); };
		tlToolsLeft.Controls.Add(_cutEditSplitButton);

		_cutEditRippleDeleteBtn = MakeTimelineIconButton("🌊", "波纹删除 (快捷键: Shift+Del)\n删除所选片段并自动将后方所有素材向前吸附闭合间隙", 32);
		_cutEditRippleDeleteBtn.ForeColor = Color.FromArgb(251, 146, 60);
		_cutEditRippleDeleteBtn.Click += delegate { DeleteSelectedCutSegment(isRipple: true); };
		tlToolsLeft.Controls.Add(_cutEditRippleDeleteBtn);

		_cutEditDeleteBtn = MakeTimelineIconButton("🗑️", "常规删除 (快捷键: Delete)\n删除选中的素材片段（保留原位空白间隙）", 32);
		_cutEditDeleteBtn.ForeColor = Color.FromArgb(248, 113, 113);
		_cutEditDeleteBtn.Click += delegate { DeleteSelectedCutSegment(isRipple: false); };
		tlToolsLeft.Controls.Add(_cutEditDeleteBtn);

		tlToolsLeft.Controls.Add(MakeDivider());

		// Group 2: Snapping, Linking & Gap closing
		_cutEditSnapBtn = MakeTimelineIconButton("🧲", "自动磁吸对齐 (快捷键: S)\n当前状态: 已开启\n开启后移动片段或播放头时会自动吸附到素材边缘", 32);
		_cutEditSnapBtn.BackColor = Color.FromArgb(6, 182, 212);
		_cutEditSnapBtn.ForeColor = Color.Black;
		_cutEditSnapBtn.Click += delegate { ToggleSnapping(); };
		tlToolsLeft.Controls.Add(_cutEditSnapBtn);

		_cutEditLinkBtn = MakeTimelineIconButton("🔗", "视音频联动选择 (快捷键: L)\n当前状态: 已开启\n开启后选中视频片段会自动联动选中对应音频", 32);
		_cutEditLinkBtn.BackColor = Color.FromArgb(59, 130, 246);
		_cutEditLinkBtn.ForeColor = Color.White;
		_cutEditLinkBtn.Click += delegate { ToggleLinkedSelection(); };
		tlToolsLeft.Controls.Add(_cutEditLinkBtn);

		_cutEditCloseGapsBtn = MakeTimelineIconButton("⏩", "闭合所有间隙\n自动扫描轨道并将所有素材向前拼接消除空隙", 32);
		_cutEditCloseGapsBtn.Click += delegate { CloseAllTimelineGaps(); };
		tlToolsLeft.Controls.Add(_cutEditCloseGapsBtn);

		tlToolsLeft.Controls.Add(MakeDivider());

		// Group 3: Tracks & Segment State
		_cutEditAddTrackBtn = MakeTimelineIconButton("➕", "添加新轨道\n点击弹出菜单新建视频轨道、音频轨道或字幕轨道", 32);
		_cutEditAddTrackBtn.Click += delegate
		{
			ContextMenuStrip cms = new ContextMenuStrip();
			cms.Items.Add("📹 添加视频轨道 (Video Track)", null, delegate { AddCutEditTrack(TrackType.Video); });
			cms.Items.Add("🎵 添加音频轨道 (Audio Track)", null, delegate { AddCutEditTrack(TrackType.Audio); });
			cms.Items.Add("📝 添加字幕轨道 (Subtitle Track)", null, delegate { AddCutEditTrack(TrackType.Subtitle); });
			cms.Show(_cutEditAddTrackBtn, new Point(0, _cutEditAddTrackBtn.Height));
		};
		tlToolsLeft.Controls.Add(_cutEditAddTrackBtn);

		_cutEditToggleSegmentButton = MakeTimelineIconButton("🚫", "剔除 / 保留切换\n将选中片段切换为禁用（不参与导出）或启用", 32);
		_cutEditToggleSegmentButton.Click += delegate { ToggleSelectedSegmentKept(); };
		tlToolsLeft.Controls.Add(_cutEditToggleSegmentButton);

		_cutEditResetSegmentsButton = MakeTimelineIconButton("↺", "恢复全部片段\n重置所有片段的剔除与裁剪为初始完整状态", 32);
		_cutEditResetSegmentsButton.Click += delegate { ResetCutEditSegments(); };
		tlToolsLeft.Controls.Add(_cutEditResetSegmentsButton);

		_cutEditClearAllBtn = MakeTimelineIconButton("🧨", "清空所有轨道\n清空时间轴上的所有剪辑素材与轨道工程", 32);
		_cutEditClearAllBtn.ForeColor = Color.FromArgb(248, 113, 113);
		_cutEditClearAllBtn.Click += delegate { ClearCutEditorProject(suppressPrompt: false); };
		tlToolsLeft.Controls.Add(_cutEditClearAllBtn);

		tlToolsLeft.Controls.Add(MakeDivider());

		// Group 4: Undo, Redo, Save
		_cutEditUndoButton = MakeTimelineIconButton("↶", "撤销操作 (快捷键: Ctrl+Z)", 32);
		_cutEditUndoButton.Enabled = false;
		_cutEditUndoButton.Click += delegate { UndoCutEditAction(); };
		tlToolsLeft.Controls.Add(_cutEditUndoButton);

		_cutEditRedoButton = MakeTimelineIconButton("↷", "重做操作 (快捷键: Ctrl+Y)", 32);
		_cutEditRedoButton.Enabled = false;
		_cutEditRedoButton.Click += delegate { RedoCutEditAction(); };
		tlToolsLeft.Controls.Add(_cutEditRedoButton);

		_cutEditSaveProjectButton = MakeTimelineIconButton("💾", "保存工程 (快捷键: Ctrl+S)", 32);
		_cutEditSaveProjectButton.Click += delegate { SaveCutEditProject(); };
		tlToolsLeft.Controls.Add(_cutEditSaveProjectButton);

		tlToolsLeft.Controls.Add(MakeDivider());

		// Group 5: Mark In / Mark Out
		_cutEditSetInButton = MakeTimelineIconButton("[", "设置入点 (快捷键: I)\n以当前播放头位置标记工程导出或剪辑入点", 30);
		_cutEditSetInButton.Click += delegate { SetCutEditInPoint(); };
		tlToolsLeft.Controls.Add(_cutEditSetInButton);

		_cutEditSetOutButton = MakeTimelineIconButton("]", "设置出点 (快捷键: O)\n以当前播放头位置标记工程导出或剪辑出点", 30);
		_cutEditSetOutButton.Click += delegate { SetCutEditOutPoint(); };
		tlToolsLeft.Controls.Add(_cutEditSetOutButton);

		_cutEditEstimatedDurationLabel = new Label
		{
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleLeft,
			Text = "预计时长: 00:00.0",
			ForeColor = MutedColor,
			Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular),
			Margin = new Padding(6, 7, 4, 0)
		};
		tlToolsLeft.Controls.Add(_cutEditEstimatedDurationLabel);

		// Right Audio mixing & Timeline Zoom & Track Height controls
		FlowLayoutPanel tlToolsRight = new FlowLayoutPanel
		{
			Dock = DockStyle.Right,
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			Padding = new Padding(0, 4, 6, 4)
		};

		Label lblSpd = new Label { Text = "速度:", AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Microsoft YaHei UI", 8.5f), Margin = new Padding(2, 6, 0, 0) };
		tlToolsRight.Controls.Add(lblSpd);

		_cutEditSpeedCombo = new ComboBox { Width = 64, Height = 26, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Microsoft YaHei UI", 8.5f), Margin = new Padding(2, 2, 2, 0) };
		_cutEditSpeedCombo.Items.AddRange(new object[] { "1.00x", "0.50x", "0.75x", "1.25x", "1.50x", "2.00x" });
		_cutEditSpeedCombo.SelectedIndex = 0;
		_cutEditToolTip.SetToolTip(_cutEditSpeedCombo, "选择时间轴整体播放速度");
		tlToolsRight.Controls.Add(_cutEditSpeedCombo);

		_cutEditKeepOriginalAudioCheckBox = new CheckBox { Text = "原声", AutoSize = true, Checked = true, ForeColor = Color.FromArgb(203, 213, 225), Font = new Font("Microsoft YaHei UI", 8.5f), Margin = new Padding(4, 5, 2, 0) };
		_cutEditKeepOriginalAudioCheckBox.CheckedChanged += delegate
		{
			_cutEditAudioMuted = !_cutEditKeepOriginalAudioCheckBox.Checked;
			if (_cutEditMediaElement != null) _cutEditMediaElement.IsMuted = _cutEditAudioMuted;
			_cutEditTimelineCanvas?.Invalidate();
		};
		_cutEditToolTip.SetToolTip(_cutEditKeepOriginalAudioCheckBox, "勾选保留原视频音频，取消则全局静音原声");
		tlToolsRight.Controls.Add(_cutEditKeepOriginalAudioCheckBox);

		_cutEditBgmVolumeLabel = new Label { Text = $"🎵 配乐: {_cutEditBgmVolume}%", AutoSize = true, ForeColor = Color.FromArgb(192, 132, 252), Font = new Font("Microsoft YaHei UI", 8.5f), Margin = new Padding(4, 6, 0, 0) };
		tlToolsRight.Controls.Add(_cutEditBgmVolumeLabel);

		_cutEditBgmVolumeTrackBar = new TrackBar { Width = 64, Height = 24, Minimum = 0, Maximum = 100, Value = _cutEditBgmVolume, TickStyle = TickStyle.None, Margin = new Padding(1, 4, 4, 0) };
		_cutEditBgmVolumeTrackBar.ValueChanged += delegate
		{
			_cutEditBgmVolume = _cutEditBgmVolumeTrackBar.Value;
			_cutEditBgmVolumeLabel.Text = $"🎵 配乐: {_cutEditBgmVolume}%";
			_cutEditTimelineCanvas?.Invalidate();
		};
		_cutEditToolTip.SetToolTip(_cutEditBgmVolumeTrackBar, "调节背景配乐音量大小 (0% - 100%)");
		tlToolsRight.Controls.Add(_cutEditBgmVolumeTrackBar);

		tlToolsRight.Controls.Add(new Panel { Width = 1, Height = 18, BackColor = Color.FromArgb(71, 85, 105), Margin = new Padding(4, 6, 4, 0) });

		// Zoom controls (轨道长短 / 缩放拉杆)
		_cutEditZoomLabel = new Label { Text = "🔍 100%", AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Microsoft YaHei UI", 8.5f), Margin = new Padding(4, 6, 1, 0), Cursor = Cursors.Hand };
		_cutEditToolTip.SetToolTip(_cutEditZoomLabel, "时间轴缩放比例 (双击或右键重置为100%)\n支持快捷键: Ctrl + 滚轮 或 Alt + 滚轮");
		_cutEditZoomLabel.MouseDoubleClick += delegate { SetCutEditZoom(100); };
		_cutEditZoomLabel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) SetCutEditZoom(100); };
		tlToolsRight.Controls.Add(_cutEditZoomLabel);

		_cutEditZoomOutBtn = MakeMiniStepButton("➖");
		_cutEditZoomOutBtn.Click += delegate { SetCutEditZoom((int)Math.Round(_cutEditTimelineZoom * 100) - 15); };
		_cutEditToolTip.SetToolTip(_cutEditZoomOutBtn, "缩小时间轴 (长短缩短)");
		tlToolsRight.Controls.Add(_cutEditZoomOutBtn);

		_cutEditZoomSlider = new TrackBar { Width = 72, Height = 24, Minimum = 20, Maximum = 600, Value = 100, TickStyle = TickStyle.None, Margin = new Padding(0, 4, 0, 0) };
		_cutEditZoomSlider.ValueChanged += delegate
		{
			SetCutEditZoom(_cutEditZoomSlider.Value);
		};
		_cutEditToolTip.SetToolTip(_cutEditZoomSlider, "轨道时间长短拉杆 (20% - 600%)\n快捷键: Ctrl + 滚轮 或 Alt + 滚轮");
		tlToolsRight.Controls.Add(_cutEditZoomSlider);

		_cutEditZoomInBtn = MakeMiniStepButton("➕");
		_cutEditZoomInBtn.Click += delegate { SetCutEditZoom((int)Math.Round(_cutEditTimelineZoom * 100) + 15); };
		_cutEditToolTip.SetToolTip(_cutEditZoomInBtn, "放大时间轴 (长短拉长)");
		tlToolsRight.Controls.Add(_cutEditZoomInBtn);

		tlToolsRight.Controls.Add(new Panel { Width = 1, Height = 18, BackColor = Color.FromArgb(71, 85, 105), Margin = new Padding(4, 6, 4, 0) });

		// Track Height controls (轨道宽窄 / 高度调节拉杆)
		_cutEditTrackHeightLabel = new Label { Text = $"↕️ {_cutEditBaseTrackHeight}px", AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Microsoft YaHei UI", 8.5f), Margin = new Padding(4, 6, 1, 0), Cursor = Cursors.Hand };
		_cutEditToolTip.SetToolTip(_cutEditTrackHeightLabel, "轨道高度 (双击或右键重置为标准36px)\n支持快捷键: Shift + 滚轮");
		_cutEditTrackHeightLabel.MouseDoubleClick += delegate { SetCutEditTrackHeight(36); };
		_cutEditTrackHeightLabel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) SetCutEditTrackHeight(36); };
		tlToolsRight.Controls.Add(_cutEditTrackHeightLabel);

		_cutEditHeightDownBtn = MakeMiniStepButton("➖");
		_cutEditHeightDownBtn.Click += delegate { SetCutEditTrackHeight(_cutEditBaseTrackHeight - 4); };
		_cutEditToolTip.SetToolTip(_cutEditHeightDownBtn, "轨道变窄/变矮");
		tlToolsRight.Controls.Add(_cutEditHeightDownBtn);

		_cutEditTrackHeightSlider = new TrackBar { Width = 68, Height = 24, Minimum = 24, Maximum = 110, Value = _cutEditBaseTrackHeight, TickStyle = TickStyle.None, Margin = new Padding(0, 4, 0, 0) };
		_cutEditTrackHeightSlider.ValueChanged += delegate
		{
			SetCutEditTrackHeight(_cutEditTrackHeightSlider.Value);
		};
		_cutEditToolTip.SetToolTip(_cutEditTrackHeightSlider, "轨道宽窄拉杆 (24px - 110px)\n快捷键: Shift + 滚轮");
		tlToolsRight.Controls.Add(_cutEditTrackHeightSlider);

		_cutEditHeightUpBtn = MakeMiniStepButton("➕");
		_cutEditHeightUpBtn.Click += delegate { SetCutEditTrackHeight(_cutEditBaseTrackHeight + 4); };
		_cutEditToolTip.SetToolTip(_cutEditHeightUpBtn, "轨道加宽/加高");
		tlToolsRight.Controls.Add(_cutEditHeightUpBtn);

		_cutEditFitWindowBtn = MakeButton("⛶ 适合", 64);
		_cutEditFitWindowBtn.Height = 28;
		_cutEditFitWindowBtn.Margin = new Padding(4, 1, 0, 0);
		_cutEditFitWindowBtn.Click += delegate
		{
			_cutEditScrollX = 0;
			SetCutEditZoom(100);
		};
		_cutEditToolTip.SetToolTip(_cutEditFitWindowBtn, "适合窗口: 一键将时间轴重置为100%并对齐开头");
		tlToolsRight.Controls.Add(_cutEditFitWindowBtn);

		// CRITICAL: DockStyle.Right MUST be added FIRST so it always stays docked on the right side!
		timelineTools.Controls.Add(tlToolsRight);
		timelineTools.Controls.Add(tlToolsLeft);
		bottomTimelineHost.Controls.Add(timelineTools);

		// Multi-Track Timeline Canvas (DockStyle.Fill - fully occupying bottom panel)
		_cutEditTimelineCanvas = new PictureBox
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(16, 20, 28),
			Cursor = Cursors.Default,
			AllowDrop = true
		};
		typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(_cutEditTimelineCanvas, true);
		_cutEditTimelineCanvas.Paint += delegate(object s, PaintEventArgs e)
		{
			PaintTimelineCanvas(e.Graphics, _cutEditTimelineCanvas.ClientRectangle);
		};
		_cutEditTimelineCanvas.MouseDown += delegate(object s, MouseEventArgs e)
		{
			HandleTimelineMouseDown(e);
		};
		_cutEditTimelineCanvas.MouseMove += delegate(object s, MouseEventArgs e)
		{
			HandleTimelineMouseMove(e);
		};
		_cutEditTimelineCanvas.MouseUp += delegate(object s, MouseEventArgs e)
		{
			HandleTimelineMouseUp(e);
		};
		_cutEditTimelineCanvas.MouseEnter += delegate
		{
			_cutEditTimelineCanvas.Focus();
		};
		_cutEditTimelineCanvas.MouseLeave += delegate
		{
			_razorHoverX = -1;
			_snapGuideScreenX = -1;
			_cutEditTimelineCanvas.Invalidate();
		};
		_cutEditTimelineCanvas.MouseWheel += HandleTimelineMouseWheel;
		_cutEditTimelineCanvas.DragEnter += delegate(object s, DragEventArgs e)
		{
			HandleTimelineDragEnter(e);
		};
		_cutEditTimelineCanvas.DragOver += delegate(object s, DragEventArgs e)
		{
			HandleTimelineDragOver(e);
		};
		_cutEditTimelineCanvas.DragDrop += delegate(object s, DragEventArgs e)
		{
			HandleTimelineDragDrop(e);
		};
		_cutEditTimelineCanvas.Resize += delegate
		{
			ClampTimelineScroll(_cutEditTimelineCanvas.ClientSize.Width);
			_cutEditTimelineCanvas.Invalidate();
		};
		bottomTimelineHost.Controls.Add(_cutEditTimelineCanvas);
		_cutEditTimelineCanvas.BringToFront();
		
splitMain.Panel2.Controls.Add(bottomTimelineHost);
		tabPage.Controls.Add(splitMain);
		tabPage.Controls.Add(topBar);
		topBar.SendToBack();

		tabPage.Resize += delegate
		{
			if (splitMain.Height > 400)
			{
				int target = (int)(splitMain.Height * 0.54);
				if (target > 200 && splitMain.Height - target > 160)
				{
					splitMain.SplitterDistance = target;
				}
			}
		};

		return tabPage;
	}

	private TabPage BuildDeliverTab()
	{
		TabPage tabPage = new TabPage("导出交付");
		tabPage.BackColor = CanvasColor;

		// Top Bar
		Panel topBar = new Panel
		{
			Dock = DockStyle.Top,
			Height = 52,
			BackColor = SurfaceColor,
			Padding = new Padding(16, 8, 16, 8)
		};

		Label deliverTitle = new Label
		{
			Text = "🚀 交付渲染工作台 (Deliver Studio) — 终审、高规格硬件编码与跨模块无缝交付",
			Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
			ForeColor = _isDarkMode ? Color.White : Color.FromArgb(15, 23, 42),
			AutoSize = true,
			Location = new Point(14, 8)
		};
		topBar.Controls.Add(deliverTitle);

		Label deliverSub = new Label
		{
			Text = "无缝承接来自【视频剪辑】、【视频拼屏】或【批量合并】的工程结果，进行标准化高规格输出与回流",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = MutedColor,
			AutoSize = true,
			Location = new Point(14, 29)
		};
		topBar.Controls.Add(deliverSub);
		topBar.Paint += delegate(object s, PaintEventArgs e)
		{
			using (Pen p = new Pen(Color.FromArgb(40, 50, 68), 1f))
			{
				e.Graphics.DrawLine(p, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
			}
		};

		SplitContainer split = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Vertical,
			SplitterWidth = 4,
			BackColor = Color.FromArgb(20, 24, 32)
		};
		split.Panel1.BackColor = SurfaceColor;
		split.Panel2.BackColor = SurfaceColor;

		// --- Left Panel: Render Settings ---
		Panel leftScroll = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			Padding = new Padding(16, 14, 16, 14)
		};

		// Group 1: Output Destination
		GroupBox destGroup = MakeGroupBox("📁 成片保存目录与文件命名", 0, 0, 480, 110);
		destGroup.Dock = DockStyle.Top;
		destGroup.Height = 110;

		destGroup.Controls.Add(MakeLabel("保存目录:", 16, 28));
		_deliverOutputFolder = new TextBox
		{
			Location = new Point(90, 24),
			Width = 285
		};
		destGroup.Controls.Add(_deliverOutputFolder);

		Button browseFolderBtn = MakeButton("选择…", 70);
		browseFolderBtn.Location = new Point(385, 22);
		browseFolderBtn.Click += delegate
		{
			using (FolderBrowserDialog fbd = new FolderBrowserDialog())
			{
				fbd.Description = "选择交付成片输出保存目录";
				if (Directory.Exists(_deliverOutputFolder.Text))
				{
					fbd.SelectedPath = _deliverOutputFolder.Text;
				}
				if (fbd.ShowDialog(this) == DialogResult.OK)
				{
					_deliverOutputFolder.Text = fbd.SelectedPath;
				}
			}
		};
		destGroup.Controls.Add(browseFolderBtn);

		destGroup.Controls.Add(MakeLabel("成片文件名:", 16, 68));
		_deliverOutputFileName = new TextBox
		{
			Location = new Point(105, 64),
			Width = 350,
			Text = "交付成片_{timestamp}.mp4"
		};
		destGroup.Controls.Add(_deliverOutputFileName);
		leftScroll.Controls.Add(destGroup);

		// Group 2: Encoding Profile
		GroupBox profileGroup = MakeGroupBox("⚙️ 编码规格与画质基准 (Encoding Profile)", 0, 118, 480, 240);
		profileGroup.Dock = DockStyle.Top;
		profileGroup.Height = 240;

		profileGroup.Controls.Add(MakeLabel("封装格式:", 16, 28));
		_deliverFormatCombo = new ComboBox
		{
			Location = new Point(90, 24),
			Width = 365,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_deliverFormatCombo.Items.AddRange(new object[]
		{
			"MP4 (.mp4 - H.264 / AAC 全平台广泛兼容)",
			"MOV (.mov - Apple QuickTime 高保真格式)",
			"MKV (.mkv - Matroska 全功能无损容器)"
		});
		_deliverFormatCombo.SelectedIndex = 0;
		profileGroup.Controls.Add(_deliverFormatCombo);

		profileGroup.Controls.Add(MakeLabel("分辨率规格:", 16, 68));
		_deliverResolutionCombo = new ComboBox
		{
			Location = new Point(90, 64),
			Width = 365,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_deliverResolutionCombo.Items.AddRange(new object[]
		{
			"保持素材原画分辨率 (推荐，极速高保真)",
			"4K 超高清 (3840x2160 16:9)",
			"1080P 高清横屏 (1920x1080 16:9)",
			"720P 标清横屏 (1280x720 16:9)",
			"抖音/小红书 1080P 竖屏 (1080x1920 9:16)",
			"微信朋友圈正方形 (1080x1080 1:1)"
		});
		_deliverResolutionCombo.SelectedIndex = 0;
		profileGroup.Controls.Add(_deliverResolutionCombo);

		profileGroup.Controls.Add(MakeLabel("渲染画质:", 16, 108));
		_deliverQualityCombo = new ComboBox
		{
			Location = new Point(90, 104),
			Width = 365,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_deliverQualityCombo.Items.AddRange(new object[]
		{
			"CRF 16 - 视觉无损超高质量 (大体积)",
			"CRF 19 - 电影级高质量 (推荐平衡)",
			"CRF 23 - 网页网络标准质量 (体积适中)",
			"CRF 28 - 极速小体积压缩"
		});
		_deliverQualityCombo.SelectedIndex = 1;
		profileGroup.Controls.Add(_deliverQualityCombo);

		profileGroup.Controls.Add(MakeLabel("成片帧率:", 16, 148));
		_deliverFpsCombo = new ComboBox
		{
			Location = new Point(90, 144),
			Width = 365,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_deliverFpsCombo.Items.AddRange(new object[]
		{
			"保持素材原帧率 (推荐)",
			"60 FPS (丝滑流畅)",
			"30 FPS (标准视频)",
			"24 FPS (电影原生帧率)"
		});
		_deliverFpsCombo.SelectedIndex = 0;
		profileGroup.Controls.Add(_deliverFpsCombo);

		profileGroup.Controls.Add(MakeLabel("音频码率:", 16, 188));
		_deliverAudioBitrateCombo = new ComboBox
		{
			Location = new Point(90, 184),
			Width = 365,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_deliverAudioBitrateCombo.Items.AddRange(new object[]
		{
			"320 kbps (录音室无损高音质)",
			"192 kbps (高品质立体声，推荐)",
			"128 kbps (标准网络音频)",
			"静音 (无音频流输出)"
		});
		_deliverAudioBitrateCombo.SelectedIndex = 1;
		profileGroup.Controls.Add(_deliverAudioBitrateCombo);
		leftScroll.Controls.Add(profileGroup);

		// Group 3: Project Summary Card
		GroupBox summaryGroup = MakeGroupBox("📋 待交付工程状态概览", 0, 366, 480, 170);
		summaryGroup.Dock = DockStyle.Top;
		summaryGroup.Height = 170;

		_deliverProjectSummary = new Label
		{
			Location = new Point(16, 24),
			Width = 440,
			Height = 135,
			Font = new Font("Microsoft YaHei UI", 9f),
			ForeColor = MutedColor,
			Text = "当前待交付工程状态:\n• 素材源: (尚未载入)\n• 请先在【🎬 视频剪辑】工作台载入素材并完成分段修剪"
		};
		summaryGroup.Controls.Add(_deliverProjectSummary);
		leftScroll.Controls.Add(summaryGroup);

		Panel startPanel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 60,
			Padding = new Padding(0, 10, 0, 0)
		};
		_deliverStartButton = MakePrimaryButton("🚀 开始渲染并交付成片", 16, 8, 440);
		_deliverStartButton.Height = 44;
		_deliverStartButton.Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
		_deliverStartButton.Click += delegate
		{
			StartDeliverExport();
		};
		void CenterDeliverButton()
		{
			if (startPanel.ClientSize.Width > 0 && _deliverStartButton != null)
			{
				_deliverStartButton.Left = Math.Max(16, (startPanel.ClientSize.Width - _deliverStartButton.Width) / 2);
			}
		}
		startPanel.Resize += delegate { CenterDeliverButton(); };
		startPanel.Controls.Add(_deliverStartButton);
		CenterDeliverButton();
		leftScroll.Controls.Add(startPanel);

		destGroup.BringToFront();
		profileGroup.BringToFront();
		summaryGroup.BringToFront();
		startPanel.BringToFront();

		split.Panel1.Controls.Add(leftScroll);

		// --- Right Panel: SplitContainer between Master Review Player and Lower Monitors ---
		_deliverRightSplit = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			SplitterDistance = 460, // Default generous height for clear HD details
			SplitterWidth = 8,
			BackColor = Color.FromArgb(30, 41, 59)
		};

		// Group 0: Master Review Player (Expands dynamically to fill Panel1)
		GroupBox masterPlayerGroup = MakeGroupBox("🎬 最终成片终审播放监视器 (Master Review Player)", 0, 0, 520, 360);
		masterPlayerGroup.Dock = DockStyle.Fill;
		masterPlayerGroup.Padding = new Padding(12, 22, 12, 8);

		Panel monitorBox = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(10, 14, 20)
		};
		_deliverMonitorBox = monitorBox;

		_deliverPreviewBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.Black,
			Cursor = Cursors.Hand
		};
		typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(_deliverPreviewBox, true);
		_deliverPreviewBox.DoubleClick += delegate { OpenDeliverPopoutPreview(); };

		_deliverEmptyPlaceholder = new Label
		{
			Dock = DockStyle.Fill,
			Text = "🎬 暂无待交付工程画面\n请先在【视频剪辑】中载入素材或通过【视频拼屏】一键直通剪辑",
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = MutedColor,
			Font = new Font("Microsoft YaHei UI", 9.5f)
		};

		_deliverElementHost = new System.Windows.Forms.Integration.ElementHost
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(10, 14, 20),
			Visible = false
		};

		_deliverMediaElement = new System.Windows.Controls.MediaElement
		{
			LoadedBehavior = System.Windows.Controls.MediaState.Manual,
			UnloadedBehavior = System.Windows.Controls.MediaState.Manual,
			Stretch = System.Windows.Media.Stretch.Uniform,
			ScrubbingEnabled = true
		};
		_deliverMediaElement.MediaOpened += DeliverMediaElement_MediaOpened;
		_deliverMediaElement.MediaEnded += DeliverMediaElement_MediaEnded;
		_deliverWpfOverlayImage = new System.Windows.Controls.Image
		{
			Stretch = System.Windows.Media.Stretch.Uniform,
			IsHitTestVisible = false
		};

		_deliverWpfTransitionBorder = new System.Windows.Controls.Border
		{
			IsHitTestVisible = false,
			Visibility = System.Windows.Visibility.Collapsed
		};

		System.Windows.Controls.Grid deliverGrid = new System.Windows.Controls.Grid();
		deliverGrid.Children.Add(_deliverMediaElement);
		deliverGrid.Children.Add(_deliverWpfTransitionBorder);
		deliverGrid.Children.Add(_deliverWpfOverlayImage);
		_deliverElementHost.Child = deliverGrid;

		monitorBox.Controls.Add(_deliverPreviewBox);
		monitorBox.Controls.Add(_deliverEmptyPlaceholder);
		monitorBox.Controls.Add(_deliverElementHost);
		Panel ctrlBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 84,
			BackColor = Color.FromArgb(16, 22, 32),
			Padding = new Padding(6, 4, 6, 6)
		};

		_deliverPlayPauseButton = MakeButton("▶ 播放 (空格)", 100);
		_deliverPlayPauseButton.Location = new Point(6, 6);
		_deliverPlayPauseButton.Height = 30;
		_deliverPlayPauseButton.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		_deliverPlayPauseButton.Click += delegate { ToggleDeliverPlayPause(); };
		ctrlBar.Controls.Add(_deliverPlayPauseButton);

		_deliverTimeScrubber = new TrackBar
		{
			Location = new Point(112, 6),
			Width = 260,
			Height = 28,
			Minimum = 0,
			Maximum = 1000,
			TickStyle = TickStyle.None,
			BackColor = Color.FromArgb(16, 22, 32),
			Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
		};
		_deliverTimeScrubber.Scroll += DeliverTimeScrubber_Scroll;
		_deliverTimeScrubber.MouseDown += DeliverTimeScrubber_MouseDown;
		_deliverTimeScrubber.MouseUp += DeliverTimeScrubber_MouseUp;
		ctrlBar.Controls.Add(_deliverTimeScrubber);

		_deliverTimeLabel = MakeLabel("00:00.0 / 00:00.0", 374, 11);
		_deliverTimeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		_deliverTimeLabel.Width = 135;
		_deliverTimeLabel.ForeColor = AccentColor;
		_deliverTimeLabel.Font = new Font("Consolas", 9f, FontStyle.Bold);
		_deliverTimeLabel.TextAlign = ContentAlignment.MiddleRight;
		ctrlBar.Controls.Add(_deliverTimeLabel);

		Button btnDeliverRefresh = MakeButton("🔄 刷新监视器", 100);
		btnDeliverRefresh.Location = new Point(6, 44);
		btnDeliverRefresh.Height = 30;
		btnDeliverRefresh.Font = new Font("Microsoft YaHei UI", 8.5f);
		btnDeliverRefresh.Click += delegate { InitOrRefreshDeliverPreview(forceReload: true); };
		ctrlBar.Controls.Add(btnDeliverRefresh);

		_deliverPopoutBtn = MakeButton("🗖 弹出独立大窗", 120);
		_deliverPopoutBtn.Location = new Point(112, 44);
		_deliverPopoutBtn.Height = 30;
		_deliverPopoutBtn.BackColor = Color.FromArgb(14, 116, 144);
		_deliverPopoutBtn.ForeColor = Color.White;
		_deliverPopoutBtn.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
		_deliverPopoutBtn.Click += delegate { OpenDeliverPopoutPreview(); };
		ctrlBar.Controls.Add(_deliverPopoutBtn);

		_deliverExpandToggleBtn = MakeButton("↕ 放大/还原视窗", 120);
		_deliverExpandToggleBtn.Location = new Point(238, 44);
		_deliverExpandToggleBtn.Height = 30;
		_deliverExpandToggleBtn.Font = new Font("Microsoft YaHei UI", 8.5f);
		_deliverExpandToggleBtn.Click += delegate
		{
			if (_deliverRightSplit != null)
			{
				if (_deliverRightSplit.SplitterDistance < 560)
				{
					_deliverRightSplit.SplitterDistance = Math.Min(680, _deliverRightSplit.Height - 140);
				}
				else
				{
					_deliverRightSplit.SplitterDistance = 420;
				}
			}
		};
		ctrlBar.Controls.Add(_deliverExpandToggleBtn);

		Label deliverHint = MakeLabel("💡 终审播放器可直接拽着下边缘拖拽缩放，双击或点击上方按钮可弹出独立大窗！", 366, 51);
		deliverHint.Font = new Font("Microsoft YaHei UI", 8.5f);
		deliverHint.ForeColor = MutedColor;
		ctrlBar.Controls.Add(deliverHint);

		masterPlayerGroup.Controls.Add(monitorBox);
		masterPlayerGroup.Controls.Add(ctrlBar);
		_deliverRightSplit.Panel1.Controls.Add(masterPlayerGroup);

		GroupBox monGroup = MakeGroupBox("🖥️ 渲染监视器 (Render Monitor)", 0, 370, 520, 95);
		monGroup.Dock = DockStyle.Top;
		monGroup.Height = 95;

		_deliverProgressBar = new ProgressBar
		{
			Location = new Point(16, 26),
			Width = 490,
			Height = 20,
			Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
		};
		monGroup.Controls.Add(_deliverProgressBar);

		_deliverStatusLabel = new Label
		{
			Location = new Point(16, 52),
			Width = 490,
			Height = 35,
			Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
			ForeColor = MutedColor,
			Font = new Font("Microsoft YaHei UI", 9.25f),
			Text = "就绪。点击左侧【开始渲染并交付成片】。"
		};
		monGroup.Controls.Add(_deliverStatusLabel);

		GroupBox histGroup = MakeGroupBox("📦 交付成片库与跨模块联动 (Delivered Outputs & Linkages)", 0, 118, 520, 480);
		histGroup.Dock = DockStyle.Fill;

		_deliverHistoryList = new ListView
		{
			Location = new Point(16, 26),
			Width = 490,
			Height = 380,
			Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
			View = View.Details,
			FullRowSelect = true,
			GridLines = false,
			BorderStyle = BorderStyle.None,
			BackColor = Color.FromArgb(16, 20, 28),
			ForeColor = Color.FromArgb(226, 232, 240),
			MultiSelect = false,
			OwnerDraw = true,
			Font = new Font("Microsoft YaHei UI", 9f)
		};
		_deliverHistoryList.Columns.Add("成片文件名", 160);
		_deliverHistoryList.Columns.Add("规格", 80);
		_deliverHistoryList.Columns.Add("时长", 70);
		_deliverHistoryList.Columns.Add("大小", 75);
		_deliverHistoryList.Columns.Add("交付时间", 80);
		_deliverHistoryList.Columns.Add("完整路径", 240);
		_deliverHistoryList.DrawColumnHeader += DrawVideoListColumnHeader;
		_deliverHistoryList.DrawItem += delegate(object s, DrawListViewItemEventArgs e) { e.DrawDefault = true; };
		_deliverHistoryList.DrawSubItem += delegate(object s, DrawListViewSubItemEventArgs e) { e.DrawDefault = true; };
		_deliverHistoryList.SelectedIndexChanged += delegate
		{
			bool hasSel = _deliverHistoryList.SelectedIndices.Count > 0;
			_deliverOpenFolderButton.Enabled = hasSel || !string.IsNullOrEmpty(_deliverLastExportPath);
			_deliverPlayOutputButton.Enabled = hasSel || !string.IsNullOrEmpty(_deliverLastExportPath);
			_deliverSendToSplitScreenButton.Enabled = hasSel || !string.IsNullOrEmpty(_deliverLastExportPath);
			_deliverSendToMergeButton.Enabled = hasSel || !string.IsNullOrEmpty(_deliverLastExportPath);
			_deliverSendBackToCutButton.Enabled = hasSel || !string.IsNullOrEmpty(_deliverLastExportPath);
		};
		_deliverHistoryList.DoubleClick += delegate
		{
			if (_deliverHistoryList.SelectedItems.Count > 0)
			{
				string path = _deliverHistoryList.SelectedItems[0].SubItems[5].Text;
				if (File.Exists(path))
				{
					PlayVideoPathWithPreviewForm(path, "播放交付成片");
				}
			}
		};
		histGroup.Controls.Add(_deliverHistoryList);

		void AutoFitDeliverColumns()
		{
			if (_deliverHistoryList.Columns.Count >= 6 && _deliverHistoryList.ClientSize.Width > 500)
			{
				int fixedW = 160 + 80 + 70 + 75 + 80;
				_deliverHistoryList.Columns[5].Width = Math.Max(240, _deliverHistoryList.ClientSize.Width - fixedW - 4);
			}
		}
		_deliverHistoryList.Resize += delegate { AutoFitDeliverColumns(); };
		base.Shown += delegate { AutoFitDeliverColumns(); };
		AutoFitDeliverColumns();

		FlowLayoutPanel histBtnRow = new FlowLayoutPanel
		{
			Location = new Point(16, 416),
			Width = 490,
			Height = 44,
			Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};

		_deliverOpenFolderButton = MakeButton("📂 定位文件", 110);
		_deliverOpenFolderButton.Height = 36;
		_deliverOpenFolderButton.Enabled = false;
		_deliverOpenFolderButton.Click += delegate
		{
			string target = GetSelectedDeliverPath();
			if (!string.IsNullOrEmpty(target) && File.Exists(target))
			{
				HighlightFileInExplorer(target);
			}
			else if (Directory.Exists(_deliverOutputFolder.Text))
			{
				Process.Start("explorer.exe", QuoteArg(_deliverOutputFolder.Text));
			}
		};
		histBtnRow.Controls.Add(_deliverOpenFolderButton);

		_deliverPlayOutputButton = MakeButton("▶ 播放成片", 100);
		_deliverPlayOutputButton.Tag = "accent";
		_deliverPlayOutputButton.Height = 36;
		_deliverPlayOutputButton.Margin = new Padding(6, 0, 0, 0);
		_deliverPlayOutputButton.Enabled = false;
		_deliverPlayOutputButton.Click += delegate
		{
			string target = GetSelectedDeliverPath();
			if (!string.IsNullOrEmpty(target) && File.Exists(target))
			{
				PlayVideoPathWithPreviewForm(target, "播放交付成片");
			}
		};
		histBtnRow.Controls.Add(_deliverPlayOutputButton);

		_deliverSendToSplitScreenButton = MakeButton("▦ 发送至拼屏", 116);
		_deliverSendToSplitScreenButton.Height = 36;
		_deliverSendToSplitScreenButton.Margin = new Padding(6, 0, 0, 0);
		_deliverSendToSplitScreenButton.Enabled = false;
		_deliverSendToSplitScreenButton.Click += delegate
		{
			string target = GetSelectedDeliverPath();
			if (!string.IsNullOrEmpty(target) && File.Exists(target))
			{
				SendCutVideoToSplitScreen(target);
			}
		};
		histBtnRow.Controls.Add(_deliverSendToSplitScreenButton);

		_deliverSendToMergeButton = MakeButton("➕ 加入合并库", 120);
		_deliverSendToMergeButton.Height = 36;
		_deliverSendToMergeButton.Margin = new Padding(6, 0, 0, 0);
		_deliverSendToMergeButton.Enabled = false;
		_deliverSendToMergeButton.Click += delegate
		{
			string target = GetSelectedDeliverPath();
			if (!string.IsNullOrEmpty(target) && File.Exists(target))
			{
				SendCutVideoToMergeList(target);
			}
		};
		histBtnRow.Controls.Add(_deliverSendToMergeButton);

		_deliverSendBackToCutButton = MakeButton("✂️ 载入剪辑", 100);
		_deliverSendBackToCutButton.Height = 36;
		_deliverSendBackToCutButton.Margin = new Padding(6, 0, 0, 0);
		_deliverSendBackToCutButton.Enabled = false;
		_deliverSendBackToCutButton.Click += delegate
		{
			string target = GetSelectedDeliverPath();
			if (!string.IsNullOrEmpty(target) && File.Exists(target))
			{
				LoadVideoIntoCutEditor(target, clearExisting: true);
				SwitchToWorkspace(5);
			}
		};
		histBtnRow.Controls.Add(_deliverSendBackToCutButton);

		histGroup.Controls.Add(histBtnRow);
		Panel rightLowerPanel = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			Padding = new Padding(12, 6, 12, 10)
		};
		rightLowerPanel.Controls.Add(histGroup);
		rightLowerPanel.Controls.Add(monGroup);
		_deliverRightSplit.Panel2.Controls.Add(rightLowerPanel);

		split.Panel2.Controls.Add(_deliverRightSplit);
		tabPage.Controls.Add(split);
		tabPage.Controls.Add(topBar);
		topBar.SendToBack();

		tabPage.Resize += delegate
		{
			if (split.Width > 700)
			{
				split.SplitterDistance = Math.Max(420, (int)(split.Width * 0.46));
			}
		};

		return tabPage;
	}

	private string GetSelectedDeliverPath()
	{
		if (_deliverHistoryList != null && _deliverHistoryList.SelectedItems.Count > 0)
		{
			string p = _deliverHistoryList.SelectedItems[0].SubItems[5].Text;
			if (File.Exists(p)) return p;
		}
		if (!string.IsNullOrEmpty(_deliverLastExportPath) && File.Exists(_deliverLastExportPath))
		{
			return _deliverLastExportPath;
		}
		return null;
	}

	private void PlayVideoPathWithPreviewForm(string path, string title)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
		try
		{
			using (VideoPreviewForm previewForm = new VideoPreviewForm(path, $"{title} - {Path.GetFileName(path)}", deleteOnClose: false))
			{
				previewForm.ShowDialog(this);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "调用播放器失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private static void DrawTrackBadge(Graphics g, Rectangle rect, string text, Color color)
	{
		using (GraphicsPath path = CreateRoundedRectanglePath(rect, 4))
		using (Brush b = new SolidBrush(color))
		{
			g.FillPath(b, path);
		}
		using (Font f = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold))
		using (Brush b = new SolidBrush(Color.White))
		using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
		{
			g.DrawString(text, f, b, rect, sf);
		}
	}

	private void InitCutEditTracks()
	{
		_cutEditTracks.Clear();
		_cutEditTracks.Add(new TimelineTrack { Id = "V2", Name = "V2 画面", Type = TrackType.Video, Height = _cutEditBaseTrackHeight });
		_cutEditTracks.Add(new TimelineTrack { Id = "V1", Name = "V1 画面", Type = TrackType.Video, Height = _cutEditBaseTrackHeight });
		_cutEditTracks.Add(new TimelineTrack { Id = "T1", Name = "T1 字幕", Type = TrackType.Subtitle, Height = Math.Max(22, _cutEditBaseTrackHeight - 8) });
		_cutEditTracks.Add(new TimelineTrack { Id = "A1", Name = "A1 原声", Type = TrackType.Audio, Height = _cutEditBaseTrackHeight });
		_cutEditTracks.Add(new TimelineTrack { Id = "A2", Name = "A2 配乐", Type = TrackType.Audio, Height = Math.Max(24, _cutEditBaseTrackHeight - 4) });
	}

	internal void AddCutEditTrack(TrackType type)
	{
		string prefix = type switch { TrackType.Video => "V", TrackType.Audio => "A", _ => "T" };
		string typeName = type switch { TrackType.Video => "画面", TrackType.Audio => "音频", _ => "字幕" };
		int num = 1;
		while (_cutEditTracks.Any(t => t.Id == $"{prefix}{num}")) num++;
		string id = $"{prefix}{num}";
		int h = (type == TrackType.Subtitle) ? Math.Max(22, _cutEditBaseTrackHeight - 8) : _cutEditBaseTrackHeight;
		
		int insertIdx = _cutEditTracks.Count;
		if (type == TrackType.Video)
		{
			insertIdx = 0;
		}
		_cutEditTracks.Insert(insertIdx, new TimelineTrack
		{
			Id = id,
			Name = $"{id} {typeName}",
			Type = type,
			Height = h
		});
		_cutEditTimelineCanvas?.Invalidate();
	}

	private TimelineTrack GetTrackAtY(int y)
	{
		int curY = 26;
		foreach (var trk in _cutEditTracks)
		{
			if (y >= curY && y < curY + trk.Height)
			{
				return trk;
			}
			curY += trk.Height + 2;
		}
		return _cutEditTracks.FirstOrDefault(t => t.Id == "V1") ?? _cutEditTracks.FirstOrDefault();
	}

	private int TimeToScreenX(double t, int canvasWidth)
	{
		int headerW = 92;
		int padR = 14;
		int trackW = Math.Max(10, (int)((canvasWidth - headerW - padR) * _cutEditTimelineZoom));
		double dur = Math.Max(0.1, _cutEditDuration);
		return headerW + 2 - _cutEditScrollX + (int)((t / dur) * trackW);
	}

	private double ScreenXToTime(int sx, int canvasWidth)
	{
		int headerW = 92;
		int padR = 14;
		int trackW = Math.Max(10, (int)((canvasWidth - headerW - padR) * _cutEditTimelineZoom));
		double dur = Math.Max(0.1, _cutEditDuration);
		double ratio = (double)(sx - (headerW + 2) + _cutEditScrollX) / trackW;
		return Math.Max(0.0, Math.Min(dur, ratio * dur));
	}

	private void ClampTimelineScroll(int canvasWidth)
	{
		int headerW = 92;
		int padR = 14;
		int trackW = Math.Max(10, (int)((canvasWidth - headerW - padR) * _cutEditTimelineZoom));
		int visibleW = Math.Max(10, canvasWidth - headerW - padR);
		int maxScroll = Math.Max(0, trackW - visibleW);
		_cutEditScrollX = Math.Max(0, Math.Min(maxScroll, _cutEditScrollX));
	}

	private double SnapTimeToNearestBoundary(double targetTime, int canvasWidth, out bool didSnap, string excludeSegmentId = null)
	{
		didSnap = false;
		if (!_cutEditSnappingEnabled || _cutEditDuration <= 0.0)
		{
			_snapGuideScreenX = -1;
			return targetTime;
		}

		int headerW = 92;
		int padR = 14;
		int trackW = Math.Max(10, (int)((canvasWidth - headerW - padR) * _cutEditTimelineZoom));
		double dur = Math.Max(0.1, _cutEditDuration);
		double thresholdSec = Math.Max(0.04, (10.0 / trackW) * dur);

		List<double> candidates = new List<double> { 0.0, _cutEditCurrentPos };

		foreach (var s in _cutEditSegments)
		{
			if (!string.IsNullOrEmpty(excludeSegmentId) && (s.Id == excludeSegmentId || s.LinkedPartnerId == excludeSegmentId)) continue;
			candidates.Add(s.TimelineStartSeconds);
			candidates.Add(s.TimelineStartSeconds + s.Duration);
		}

		foreach (var ol in _cutEditOverlays)
		{
			if (!ol.Enabled) continue;
			candidates.Add(ol.StartSeconds);
			candidates.Add(ol.StartSeconds + ol.Duration);
		}

		double bestDiff = double.MaxValue;
		double bestCandidate = targetTime;

		foreach (var c in candidates)
		{
			double diff = Math.Abs(targetTime - c);
			if (diff <= thresholdSec && diff < bestDiff)
			{
				bestDiff = diff;
				bestCandidate = c;
				didSnap = true;
			}
		}

		if (didSnap)
		{
			_snapGuideScreenX = TimeToScreenX(bestCandidate, canvasWidth);
			return bestCandidate;
		}
		else
		{
			_snapGuideScreenX = -1;
			return targetTime;
		}
	}

	private void SetTimelineToolMode(TimelineToolMode mode)
	{
		_cutEditCurrentTool = mode;
		if (_cutEditToolSelectBtn != null)
		{
			bool isSel = (mode == TimelineToolMode.Select);
			_cutEditToolSelectBtn.BackColor = isSel ? Color.FromArgb(14, 165, 233) : Color.FromArgb(30, 41, 59);
			_cutEditToolSelectBtn.ForeColor = isSel ? Color.White : Color.FromArgb(203, 213, 225);
		}
		if (_cutEditToolRazorBtn != null)
		{
			bool isRazor = (mode == TimelineToolMode.Razor);
			_cutEditToolRazorBtn.BackColor = isRazor ? Color.FromArgb(239, 68, 68) : Color.FromArgb(30, 41, 59);
			_cutEditToolRazorBtn.ForeColor = isRazor ? Color.White : Color.FromArgb(203, 213, 225);
		}
		if (_cutEditTimelineCanvas != null)
		{
			_cutEditTimelineCanvas.Cursor = (mode == TimelineToolMode.Razor) ? Cursors.Cross : Cursors.Default;
			_razorHoverX = -1;
			_cutEditTimelineCanvas.Invalidate();
		}
	}

	private void ToggleSnapping()
	{
		_cutEditSnappingEnabled = !_cutEditSnappingEnabled;
		if (_cutEditSnapBtn != null)
		{
			_cutEditSnapBtn.Text = "🧲";
			_cutEditSnapBtn.BackColor = _cutEditSnappingEnabled ? Color.FromArgb(6, 182, 212) : Color.FromArgb(30, 41, 59);
			_cutEditSnapBtn.ForeColor = _cutEditSnappingEnabled ? Color.Black : Color.FromArgb(148, 163, 184);
			_cutEditToolTip?.SetToolTip(_cutEditSnapBtn, _cutEditSnappingEnabled ? "自动磁吸: 已开启 (快捷键: S)\n移动片段或指针时自动吸附对齐\n点击切换关闭" : "自动磁吸: 已关闭 (快捷键: S)\n移动片段或指针时自由移动\n点击切换开启");
		}
		_snapGuideScreenX = -1;
		_cutEditTimelineCanvas?.Invalidate();
	}

	private void ToggleLinkedSelection()
	{
		_cutEditLinkedSelectionEnabled = !_cutEditLinkedSelectionEnabled;
		if (_cutEditLinkBtn != null)
		{
			_cutEditLinkBtn.Text = "🔗";
			_cutEditLinkBtn.BackColor = _cutEditLinkedSelectionEnabled ? Color.FromArgb(59, 130, 246) : Color.FromArgb(30, 41, 59);
			_cutEditLinkBtn.ForeColor = _cutEditLinkedSelectionEnabled ? Color.White : Color.FromArgb(148, 163, 184);
			_cutEditToolTip?.SetToolTip(_cutEditLinkBtn, _cutEditLinkedSelectionEnabled ? "视音频联动: 已开启 (快捷键: L)\n选中视频时同步联动选中对应音频\n点击切换关闭" : "视音频联动: 已关闭 (快捷键: L)\n视频与音频独立选中\n点击切换开启");
		}
		_cutEditTimelineCanvas?.Invalidate();
	}

	private Button MakeTimelineIconButton(string icon, string tooltipText, int width = 32)
	{
		Button btn = MakeButton(icon, width);
		btn.Height = 28;
		btn.Margin = new Padding(2, 2, 0, 0);
		btn.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
		btn.TextAlign = ContentAlignment.MiddleCenter;
		if (_cutEditToolTip != null && !string.IsNullOrEmpty(tooltipText))
		{
			_cutEditToolTip.SetToolTip(btn, tooltipText);
		}
		return btn;
	}

	private Button MakeMiniStepButton(string text)
	{
		Button btn = new Button
		{
			Text = text,
			Width = 20,
			Height = 22,
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand,
			Font = new Font("Microsoft YaHei UI", 7f, FontStyle.Bold),
			ForeColor = Color.FromArgb(203, 213, 225),
			BackColor = Color.FromArgb(45, 55, 72),
			Margin = new Padding(1, 4, 1, 0),
			Padding = Padding.Empty
		};
		btn.FlatAppearance.BorderSize = 0;
		btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(71, 85, 105);
		btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 41, 59);
		return btn;
	}

	private void SetCutEditTrackHeight(int h)
	{
		h = Math.Max(24, Math.Min(110, h));
		_cutEditBaseTrackHeight = h;
		if (_cutEditTrackHeightSlider != null && _cutEditTrackHeightSlider.Value != h)
		{
			_cutEditTrackHeightSlider.Value = h;
		}
		if (_cutEditTrackHeightLabel != null)
		{
			_cutEditTrackHeightLabel.Text = $"↕️ {h}px";
		}
		foreach (var trk in _cutEditTracks)
		{
			trk.Height = (trk.Type == TrackType.Subtitle) ? Math.Max(20, h - 8) : h;
		}
		_cutEditTimelineCanvas?.Invalidate();
	}

	private void SetCutEditZoom(int zoomPercent)
	{
		zoomPercent = Math.Max(20, Math.Min(600, zoomPercent));
		_cutEditTimelineZoom = zoomPercent / 100.0;
		if (_cutEditZoomSlider != null && _cutEditZoomSlider.Value != zoomPercent)
		{
			_cutEditZoomSlider.Value = zoomPercent;
		}
		if (_cutEditZoomLabel != null)
		{
			_cutEditZoomLabel.Text = $"🔍 {zoomPercent}%";
		}
		ClampTimelineScroll(_cutEditTimelineCanvas?.ClientSize.Width ?? 800);
		_cutEditTimelineCanvas?.Invalidate();
	}

	private void HandleTimelineMouseWheel(object sender, MouseEventArgs e)
	{
		if (_cutEditTimelineCanvas == null || _cutEditDuration <= 0.0) return;
		int w = _cutEditTimelineCanvas.ClientSize.Width;

		if (Control.ModifierKeys.HasFlag(Keys.Shift))
		{
			// Shift + 滚轮: 垂直调节轨道宽窄/高度
			int step = e.Delta > 0 ? 4 : -4;
			SetCutEditTrackHeight(_cutEditBaseTrackHeight + step);
		}
		else if (Control.ModifierKeys.HasFlag(Keys.Alt) || Control.ModifierKeys.HasFlag(Keys.Control))
		{
			// Ctrl/Alt + 滚轮: 以鼠标所指时间点为中心缩放时间轴 (20% - 600%)
			int headerW = 92;
			int padR = 14;
			double mouseTime = ScreenXToTime(e.X, w);
			double oldZoom = _cutEditTimelineZoom;
			double factor = e.Delta > 0 ? 1.2 : 0.8333;
			double newZoom = Math.Max(0.2, Math.Min(6.0, oldZoom * factor));
			_cutEditTimelineZoom = newZoom;
			int val = (int)Math.Round(newZoom * 100);
			if (_cutEditZoomSlider != null && _cutEditZoomSlider.Value != val)
			{
				_cutEditZoomSlider.Value = Math.Max(_cutEditZoomSlider.Minimum, Math.Min(_cutEditZoomSlider.Maximum, val));
			}
			if (_cutEditZoomLabel != null)
			{
				_cutEditZoomLabel.Text = $"🔍 {val}%";
			}

			// Keep mouseTime at the same screen X
			int newTrackW = Math.Max(10, (int)((w - headerW - padR) * newZoom));
			double dur = Math.Max(0.1, _cutEditDuration);
			int newScrollX = headerW + 2 + (int)((mouseTime / dur) * newTrackW) - e.X;
			int visibleW = Math.Max(10, w - headerW - padR);
			int maxScroll = Math.Max(0, newTrackW - visibleW);
			_cutEditScrollX = Math.Max(0, Math.Min(maxScroll, newScrollX));
			_cutEditTimelineCanvas.Invalidate();
		}
		else
		{
			// 水平平移
			int headerW = 92;
			int padR = 14;
			int trackW = Math.Max(10, (int)((w - headerW - padR) * _cutEditTimelineZoom));
			int visibleW = Math.Max(10, w - headerW - padR);
			int maxScroll = Math.Max(0, trackW - visibleW);
			int delta = (int)(e.Delta * 0.8);
			_cutEditScrollX = Math.Max(0, Math.Min(maxScroll, _cutEditScrollX - delta));
			_cutEditTimelineCanvas.Invalidate();
		}
	}

	private void SplitSegmentAtTime(CutSegment targetSeg, double pos)
	{
		if (targetSeg == null) return;
		int segIdx = _cutEditSegments.IndexOf(targetSeg);
		if (segIdx < 0) return;

		var trk = _cutEditTracks.FirstOrDefault(t => t.Id == targetSeg.TrackId);
		if (trk?.IsLocked == true)
		{
			MessageBox.Show(this, $"轨道【{trk.Name}】已锁定，无法切割！", "轨道已锁定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		if (pos <= targetSeg.TimelineStartSeconds + 0.05 || pos >= targetSeg.TimelineStartSeconds + targetSeg.Duration - 0.05)
		{
			return;
		}

		PushCutEditUndoState("剃刀切割");

		double offsetInSeg = pos - targetSeg.TimelineStartSeconds;
		CutSegment seg1 = new CutSegment
		{
			Id = Guid.NewGuid().ToString("N"),
			SourcePath = targetSeg.SourcePath,
			StartSeconds = targetSeg.StartSeconds,
			EndSeconds = targetSeg.StartSeconds + offsetInSeg,
			TimelineStartSeconds = targetSeg.TimelineStartSeconds,
			IsKept = targetSeg.IsKept,
			TrackId = targetSeg.TrackId,
			MediaType = targetSeg.MediaType,
			Title = targetSeg.Title,
			VolumePercent = targetSeg.VolumePercent,
			TransitionInType = targetSeg.TransitionInType,
			TransitionInDuration = targetSeg.TransitionInDuration,
			TransitionOutType = "none",
			TransitionOutDuration = targetSeg.TransitionOutDuration
		};
		CutSegment seg2 = new CutSegment
		{
			Id = Guid.NewGuid().ToString("N"),
			SourcePath = targetSeg.SourcePath,
			StartSeconds = targetSeg.StartSeconds + offsetInSeg,
			EndSeconds = targetSeg.EndSeconds,
			TimelineStartSeconds = targetSeg.TimelineStartSeconds + offsetInSeg,
			IsKept = targetSeg.IsKept,
			TrackId = targetSeg.TrackId,
			MediaType = targetSeg.MediaType,
			Title = targetSeg.Title,
			VolumePercent = targetSeg.VolumePercent,
			TransitionInType = "none",
			TransitionInDuration = targetSeg.TransitionInDuration,
			TransitionOutType = targetSeg.TransitionOutType,
			TransitionOutDuration = targetSeg.TransitionOutDuration
		};

		// Check if linked partner exists
		CutSegment partner = null;
		if (_cutEditLinkedSelectionEnabled && !Control.ModifierKeys.HasFlag(Keys.Alt) && !string.IsNullOrEmpty(targetSeg.LinkedPartnerId))
		{
			partner = _cutEditSegments.FirstOrDefault(s => s.Id == targetSeg.LinkedPartnerId);
		}

		_cutEditSegments.RemoveAt(segIdx);
		_cutEditSegments.Insert(segIdx, seg2);
		_cutEditSegments.Insert(segIdx, seg1);

		if (partner != null)
		{
			int partnerIdx = _cutEditSegments.IndexOf(partner);
			if (partnerIdx >= 0 && pos > partner.TimelineStartSeconds + 0.05 && pos < partner.TimelineStartSeconds + partner.Duration - 0.05)
			{
				double pOffset = pos - partner.TimelineStartSeconds;
				CutSegment p1 = new CutSegment
				{
					Id = Guid.NewGuid().ToString("N"),
					SourcePath = partner.SourcePath,
					StartSeconds = partner.StartSeconds,
					EndSeconds = partner.StartSeconds + pOffset,
					TimelineStartSeconds = partner.TimelineStartSeconds,
					IsKept = partner.IsKept,
					TrackId = partner.TrackId,
					MediaType = partner.MediaType,
					Title = partner.Title,
					VolumePercent = partner.VolumePercent,
					TransitionInType = partner.TransitionInType,
					TransitionInDuration = partner.TransitionInDuration,
					TransitionOutType = "none",
					TransitionOutDuration = partner.TransitionOutDuration
				};
				CutSegment p2 = new CutSegment
				{
					Id = Guid.NewGuid().ToString("N"),
					SourcePath = partner.SourcePath,
					StartSeconds = partner.StartSeconds + pOffset,
					EndSeconds = partner.EndSeconds,
					TimelineStartSeconds = partner.TimelineStartSeconds + pOffset,
					IsKept = partner.IsKept,
					TrackId = partner.TrackId,
					MediaType = partner.MediaType,
					Title = partner.Title,
					VolumePercent = partner.VolumePercent,
					TransitionInType = "none",
					TransitionInDuration = partner.TransitionInDuration,
					TransitionOutType = partner.TransitionOutType,
					TransitionOutDuration = partner.TransitionOutDuration
				};

				seg1.LinkedPartnerId = p1.Id;
				p1.LinkedPartnerId = seg1.Id;
				seg2.LinkedPartnerId = p2.Id;
				p2.LinkedPartnerId = seg2.Id;

				_cutEditSegments.RemoveAt(partnerIdx);
				_cutEditSegments.Insert(partnerIdx, p2);
				_cutEditSegments.Insert(partnerIdx, p1);
			}
		}

		_cutEditSelectedSegmentIndex = _cutEditSegments.IndexOf(seg2);
		RefreshCutEditSegmentList();
	}

	private int GetSegmentAtPoint(Point pt, out int hitEdge)
	{
		hitEdge = 0;
		int headerW = 92;
		if (pt.X < headerW || _cutEditDuration <= 0.0 || _cutEditTimelineCanvas == null) return -1;
		int canvasW = _cutEditTimelineCanvas.ClientSize.Width;

		int curY = 26;
		foreach (var trk in _cutEditTracks)
		{
			if (pt.Y >= curY && pt.Y < curY + trk.Height)
			{
				for (int i = 0; i < _cutEditSegments.Count; i++)
				{
					var seg = _cutEditSegments[i];
					bool matchTrack = (seg.TrackId == trk.Id);
					if (!matchTrack) continue;

					int sx = TimeToScreenX(seg.TimelineStartSeconds, canvasW);
					int ex = TimeToScreenX(seg.TimelineStartSeconds + seg.Duration, canvasW);
					int sw = Math.Max(6, ex - sx);

					if (pt.X >= sx - 4 && pt.X <= sx + sw + 4)
					{
						if (Math.Abs(pt.X - sx) <= 6) hitEdge = -1; // Trim in
						else if (Math.Abs(pt.X - (sx + sw)) <= 6) hitEdge = 1; // Trim out
						else hitEdge = 0; // Move body
						return i;
					}
				}
				break;
			}
			curY += trk.Height + 2;
		}

		return -1;
	}

	private void HandleTimelineMouseDown(MouseEventArgs e)
	{
		int headerW = 92;
		int canvasW = _cutEditTimelineCanvas.ClientSize.Width;

		if (e.X < headerW)
		{
			int curY = 26;
			foreach (var trk in _cutEditTracks)
			{
				if (e.Y >= curY && e.Y < curY + trk.Height)
				{
					_cutEditSelectedTrackId = trk.Id;
					if (e.X >= 42 && e.X <= 64)
					{
						trk.IsLocked = !trk.IsLocked;
						_cutEditTimelineCanvas?.Invalidate();
						return;
					}
					else if (e.X >= 66 && e.X <= 88)
					{
						if (trk.Type == TrackType.Audio)
						{
							trk.IsMuted = !trk.IsMuted;
							if (trk.Id == "A1")
							{
								_cutEditAudioMuted = trk.IsMuted;
								if (_cutEditKeepOriginalAudioCheckBox != null) _cutEditKeepOriginalAudioCheckBox.Checked = !_cutEditAudioMuted;
								if (_cutEditMediaElement != null) _cutEditMediaElement.IsMuted = _cutEditAudioMuted;
							}
						}
						else
						{
							trk.IsVisible = !trk.IsVisible;
						}
						_cutEditTimelineCanvas?.Invalidate();
						return;
					}
					_cutEditTimelineCanvas?.Invalidate();
					break;
				}
				curY += trk.Height + 2;
			}
			return;
		}

		// Middle click or Space+Left click: Pan Hand
		if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && Control.ModifierKeys.HasFlag(Keys.Space)))
		{
			_timelineDragMode = TimelineDragMode.PanHand;
			_panStartMousePoint = e.Location;
			_panStartScrollX = _cutEditScrollX;
			_cutEditTimelineCanvas.Cursor = Cursors.SizeAll;
			return;
		}

		if (e.Button == MouseButtons.Right)
		{
			if (FindSeamAtPoint(e.Location, out CutSegment seamA, out CutSegment seamB, out Rectangle seamR))
			{
				ShowSeamTransitionContextMenu(e.Location, seamA, seamB);
				return;
			}

			int hitEdge;
			int segIdx = GetSegmentAtPoint(e.Location, out hitEdge);
			if (segIdx >= 0)
			{
				_cutEditSelectedSegmentIndex = segIdx;
				_cutEditSelectedTrackId = _cutEditSegments[segIdx].TrackId;
				_cutEditTimelineCanvas?.Invalidate();
				ShowSegmentContextMenu(e.Location);
			}
			else
			{
				ShowTimelineEmptyContextMenu(e.Location);
			}
			return;
		}

		if (e.Button == MouseButtons.Left)
		{
			// Check if left-clicked directly on active seam transition bridge badge
			if (FindSeamAtPoint(e.Location, out CutSegment seamA, out CutSegment seamB, out Rectangle seamR))
			{
				bool hasTrans = (!string.IsNullOrEmpty(seamA.TransitionOutType) && seamA.TransitionOutType != "none") ||
				                (!string.IsNullOrEmpty(seamB.TransitionInType) && seamB.TransitionInType != "none");
				if (hasTrans && seamR.Contains(e.Location))
				{
					ShowSeamTransitionContextMenu(e.Location, seamA, seamB);
					return;
				}
			}
			// Razor Tool: Click-to-cut directly at cursor
			if (_cutEditCurrentTool == TimelineToolMode.Razor)
			{
				if (_cutEditDuration > 0.0)
				{
					double clickTime = ScreenXToTime(e.X, canvasW);
					if (_cutEditSnappingEnabled)
					{
						bool didSnap;
						clickTime = SnapTimeToNearestBoundary(clickTime, canvasW, out didSnap);
					}

					var trk = GetTrackAtY(e.Y);
					CutSegment hitSeg = null;
					if (trk != null)
					{
						hitSeg = _cutEditSegments.FirstOrDefault(s => s.TrackId == trk.Id && clickTime > s.TimelineStartSeconds + 0.05 && clickTime < s.TimelineStartSeconds + s.Duration - 0.05);
					}
					if (hitSeg == null)
					{
						int hitEdge;
						int sIdx = GetSegmentAtPoint(e.Location, out hitEdge);
						if (sIdx >= 0) hitSeg = _cutEditSegments[sIdx];
					}

					if (hitSeg != null)
					{
						SplitSegmentAtTime(hitSeg, clickTime);
						_cutEditCurrentPos = clickTime;
						UpdateCutEditTimeLabel();
						SeekCutEditVideo(clickTime);
						_cutEditTimelineCanvas?.Invalidate();
						return;
					}
				}
				return;
			}

			// Check if clicked any overlay item on T1 track
			CutOverlayItem hitItem = null;
			Rectangle hitRect = Rectangle.Empty;
			int hitEdgeOl = 0;
			foreach (var kvp in _cachedOverlayRects)
			{
				if (kvp.Value.Contains(e.Location))
				{
					hitItem = _cutEditOverlays.FirstOrDefault(o => o.Id == kvp.Key);
					hitRect = kvp.Value;
					break;
				}
			}

			if (hitItem != null)
			{
				_cutEditSelectedSegmentIndex = -1;
				_cutEditSelectedTrackId = "T1";
				SelectOverlayItem(hitItem);
				_isDraggingOverlay = true;
				_draggedOverlayItem = hitItem;
				_overlayDragStartMouse = e.X;
				_overlayDragOrigStart = hitItem.StartSeconds;
				_overlayDragOrigDur = hitItem.Duration;

				if (e.X <= hitRect.Left + 8) hitEdgeOl = -1;
				else if (e.X >= hitRect.Right - 8) hitEdgeOl = 1;
				else hitEdgeOl = 0;
				_overlayDragEdge = hitEdgeOl;

				if (_cutEditInspectorTabs != null && _cutEditInspectorTabs.TabPages.Count > 0)
				{
					_cutEditInspectorTabs.SelectedIndex = 0;
				}
				SeekCutEditVideo(hitItem.StartSeconds);
				UpdateCutEditWpfOverlay(hitItem.StartSeconds, forcePreviewSelected: true);
				_cutEditTimelineCanvas?.Invalidate();
				return;
			}

			int hitEdgeSeg;
			int clickedSegIdx = GetSegmentAtPoint(e.Location, out hitEdgeSeg);
			if (clickedSegIdx >= 0)
			{
				var seg = _cutEditSegments[clickedSegIdx];
				var trk = _cutEditTracks.FirstOrDefault(t => t.Id == seg.TrackId);
				_cutEditSelectedSegmentIndex = clickedSegIdx;
				_cutEditSelectedTrackId = seg.TrackId;

				if (trk?.IsLocked == true)
				{
					_timelineDragMode = TimelineDragMode.None;
					_cutEditTimelineCanvas?.Invalidate();
					return;
				}

				_dragSegmentIndex = clickedSegIdx;
				_dragStartMousePoint = e.Location;
				_dragOriginalTimelineStart = seg.TimelineStartSeconds;
				_dragOriginalDuration = seg.Duration;
				_dragOriginalStartSec = seg.StartSeconds;
				_dragOriginalEndSec = seg.EndSeconds;
				_dragOriginalTrackId = seg.TrackId;

				// Setup linked partner drag if linked selection is active and Alt is NOT pressed
				_dragPartnerSegment = null;
				_dragPartnerOriginalTimelineStart = 0.0;
				if (_cutEditLinkedSelectionEnabled && !Control.ModifierKeys.HasFlag(Keys.Alt) && !string.IsNullOrEmpty(seg.LinkedPartnerId))
				{
					_dragPartnerSegment = _cutEditSegments.FirstOrDefault(s => s.Id == seg.LinkedPartnerId);
					if (_dragPartnerSegment != null)
					{
						_dragPartnerOriginalTimelineStart = _dragPartnerSegment.TimelineStartSeconds;
					}
				}

				if (hitEdgeSeg == -1) _timelineDragMode = TimelineDragMode.TrimIn;
				else if (hitEdgeSeg == 1) _timelineDragMode = TimelineDragMode.TrimOut;
				else _timelineDragMode = TimelineDragMode.MoveClip;

				_cutEditTimelineCanvas?.Invalidate();
			}
			else
			{
				_cutEditSelectedSegmentIndex = -1;
				var trk = GetTrackAtY(e.Y);
				if (trk != null)
				{
					_cutEditSelectedTrackId = trk.Id;
					if (trk.Id == "T1")
					{
						if (_cutEditInspectorTabs != null && _cutEditInspectorTabs.TabPages.Count > 0)
						{
							_cutEditInspectorTabs.SelectedIndex = 0;
						}
						_cutEditMainTitle?.Focus();
						_cutEditMainTitle?.SelectAll();
					}
				}
				_timelineDragMode = TimelineDragMode.ScrubPlayhead;
				HandleTimelineSeek(e);
			}
		}
	}

	private void HandleTimelineMouseMove(MouseEventArgs e)
	{
		int headerW = 92;
		int padR = 14;
		int canvasW = _cutEditTimelineCanvas.ClientSize.Width;
		int trackW = Math.Max(10, (int)((canvasW - headerW - padR) * _cutEditTimelineZoom));
		double dur = Math.Max(0.1, _cutEditDuration);

		if (e.Button == MouseButtons.None)
		{
			if (e.X >= headerW)
			{
				if (_cutEditCurrentTool == TimelineToolMode.Razor)
				{
					_cutEditTimelineCanvas.Cursor = Cursors.Cross;
					_razorHoverX = e.X;
					_cutEditTimelineCanvas.Invalidate();
					return;
				}

				_razorHoverX = -1;

				bool hoveredOl = false;
				foreach (var kvp in _cachedOverlayRects)
				{
					if (kvp.Value.Contains(e.Location))
					{
						hoveredOl = true;
						if (e.X <= kvp.Value.Left + 6 || e.X >= kvp.Value.Right - 6)
						{
							_cutEditTimelineCanvas.Cursor = Cursors.SizeWE;
						}
						else
						{
							_cutEditTimelineCanvas.Cursor = Cursors.SizeAll;
						}
						break;
					}
				}
				if (hoveredOl) return;

				int hitEdge;
				int segIdx = GetSegmentAtPoint(e.Location, out hitEdge);
				if (segIdx >= 0)
				{
					var seg = _cutEditSegments[segIdx];
					var trk = _cutEditTracks.FirstOrDefault(t => t.Id == seg.TrackId);
					if (trk?.IsLocked == true)
					{
						_cutEditTimelineCanvas.Cursor = Cursors.No;
					}
					else if (hitEdge != 0)
					{
						_cutEditTimelineCanvas.Cursor = Cursors.SizeWE;
					}
					else
					{
						_cutEditTimelineCanvas.Cursor = Cursors.SizeAll;
					}
				}
				else
				{
					_cutEditTimelineCanvas.Cursor = Cursors.Default;
				}
			}
			else
			{
				_cutEditTimelineCanvas.Cursor = Cursors.Hand;
			}
			return;
		}

		// Middle click or Space+Left: Pan Hand
		if (_timelineDragMode == TimelineDragMode.PanHand)
		{
			int dx = e.X - _panStartMousePoint.X;
			int visibleW = Math.Max(10, canvasW - headerW - padR);
			int maxScroll = Math.Max(0, trackW - visibleW);
			_cutEditScrollX = Math.Max(0, Math.Min(maxScroll, _panStartScrollX - dx));
			_cutEditTimelineCanvas?.Invalidate();
			return;
		}

		if (e.Button == MouseButtons.Left)
		{
			if (_isDraggingOverlay && _draggedOverlayItem != null)
			{
				double dt = (double)(e.X - _overlayDragStartMouse) / trackW * dur;
				if (_overlayDragEdge == 0)
				{
					double maxStart = Math.Max(0.0, dur - _overlayDragOrigDur);
					double newStart = Math.Max(0.0, Math.Min(dur - 0.2, _overlayDragOrigStart + dt));
					_draggedOverlayItem.StartSeconds = Math.Round(newStart, 1);
				}
				else if (_overlayDragEdge == -1)
				{
					double newStart = Math.Max(0.0, Math.Min(_overlayDragOrigStart + _overlayDragOrigDur - 0.5, _overlayDragOrigStart + dt));
					double newDur = Math.Max(0.5, (_overlayDragOrigStart + _overlayDragOrigDur) - newStart);
					_draggedOverlayItem.StartSeconds = Math.Round(newStart, 1);
					_draggedOverlayItem.Duration = Math.Round(newDur, 1);
				}
				else if (_overlayDragEdge == 1)
				{
					double newDur = Math.Max(0.5, Math.Min(dur - _draggedOverlayItem.StartSeconds, _overlayDragOrigDur + dt));
					_draggedOverlayItem.Duration = Math.Round(newDur, 1);
				}

				SyncSelectedOverlayToControls();
				_cutEditTimelineCanvas?.Invalidate();
				SeekCutEditVideo(_draggedOverlayItem.StartSeconds);
				UpdateCutEditWpfOverlay(_draggedOverlayItem.StartSeconds, forcePreviewSelected: true);
				UpdateDeliverSummary();
				return;
			}

			if (_timelineDragMode == TimelineDragMode.ScrubPlayhead)
			{
				HandleTimelineSeek(e);
			}
			else if (_timelineDragMode == TimelineDragMode.MoveClip && _dragSegmentIndex >= 0 && _dragSegmentIndex < _cutEditSegments.Count)
			{
				var seg = _cutEditSegments[_dragSegmentIndex];
				int dx = e.X - _dragStartMousePoint.X;
				double timeDelta = (double)dx / trackW * dur;
				double newStart = Math.Max(0.0, _dragOriginalTimelineStart + timeDelta);

				// Magnetic snapping
				if (_cutEditSnappingEnabled)
				{
					bool didSnapStart;
					double snapCandidate = SnapTimeToNearestBoundary(newStart, canvasW, out didSnapStart, seg.Id);
					if (didSnapStart)
					{
						newStart = snapCandidate;
					}
					else
					{
						bool didSnapEnd;
						double snapEndCandidate = SnapTimeToNearestBoundary(newStart + seg.Duration, canvasW, out didSnapEnd, seg.Id);
						if (didSnapEnd)
						{
							newStart = Math.Max(0.0, snapEndCandidate - seg.Duration);
						}
						else
						{
							_snapGuideScreenX = -1;
						}
					}
				}
				else
				{
					_snapGuideScreenX = -1;
				}

				double deltaApplied = newStart - _dragOriginalTimelineStart;
				seg.TimelineStartSeconds = newStart;

				// Synchronously move linked partner clip
				if (_dragPartnerSegment != null && _cutEditLinkedSelectionEnabled && !Control.ModifierKeys.HasFlag(Keys.Alt))
				{
					_dragPartnerSegment.TimelineStartSeconds = Math.Max(0.0, _dragPartnerOriginalTimelineStart + deltaApplied);
				}

				// Track changes vertically
				var trk = GetTrackAtY(e.Y);
				if (trk != null && !trk.IsLocked)
				{
					if (trk.Type == TrackType.Video && (seg.MediaType == "video" || seg.MediaType == "image"))
					{
						seg.TrackId = trk.Id;
						_cutEditSelectedTrackId = trk.Id;
					}
					else if (trk.Type == TrackType.Audio && seg.MediaType == "audio")
					{
						seg.TrackId = trk.Id;
						_cutEditSelectedTrackId = trk.Id;
					}
				}

				RecalculateTimelineTotalDuration();
				_cutEditTimelineCanvas?.Invalidate();
			}
			else if (_timelineDragMode == TimelineDragMode.TrimIn && _dragSegmentIndex >= 0 && _dragSegmentIndex < _cutEditSegments.Count)
			{
				var seg = _cutEditSegments[_dragSegmentIndex];
				int dx = e.X - _dragStartMousePoint.X;
				double timeDelta = (double)dx / trackW * dur;
				double maxDelta = _dragOriginalDuration - 0.2;
				double actualDelta = Math.Max(-_dragOriginalStartSec, Math.Min(maxDelta, timeDelta));
				double targetTimelineStart = Math.Max(0.0, _dragOriginalTimelineStart + actualDelta);

				if (_cutEditSnappingEnabled)
				{
					bool didSnap;
					double snapped = SnapTimeToNearestBoundary(targetTimelineStart, canvasW, out didSnap, seg.Id);
					if (didSnap)
					{
						actualDelta = snapped - _dragOriginalTimelineStart;
						actualDelta = Math.Max(-_dragOriginalStartSec, Math.Min(maxDelta, actualDelta));
					}
				}

				seg.StartSeconds = _dragOriginalStartSec + actualDelta;
				seg.TimelineStartSeconds = Math.Max(0.0, _dragOriginalTimelineStart + actualDelta);

				if (_dragPartnerSegment != null && _cutEditLinkedSelectionEnabled && !Control.ModifierKeys.HasFlag(Keys.Alt))
				{
					_dragPartnerSegment.StartSeconds = seg.StartSeconds;
					_dragPartnerSegment.TimelineStartSeconds = seg.TimelineStartSeconds;
				}

				RecalculateTimelineTotalDuration();
				_cutEditTimelineCanvas?.Invalidate();
			}
			else if (_timelineDragMode == TimelineDragMode.TrimOut && _dragSegmentIndex >= 0 && _dragSegmentIndex < _cutEditSegments.Count)
			{
				var seg = _cutEditSegments[_dragSegmentIndex];
				int dx = e.X - _dragStartMousePoint.X;
				double timeDelta = (double)dx / trackW * dur;
				double newDur = Math.Max(0.2, _dragOriginalDuration + timeDelta);
				double targetTimelineEnd = seg.TimelineStartSeconds + newDur;

				if (_cutEditSnappingEnabled)
				{
					bool didSnap;
					double snapped = SnapTimeToNearestBoundary(targetTimelineEnd, canvasW, out didSnap, seg.Id);
					if (didSnap)
					{
						newDur = Math.Max(0.2, snapped - seg.TimelineStartSeconds);
					}
				}

				seg.EndSeconds = seg.StartSeconds + newDur;

				if (_dragPartnerSegment != null && _cutEditLinkedSelectionEnabled && !Control.ModifierKeys.HasFlag(Keys.Alt))
				{
					_dragPartnerSegment.EndSeconds = _dragPartnerSegment.StartSeconds + newDur;
				}

				RecalculateTimelineTotalDuration();
				_cutEditTimelineCanvas?.Invalidate();
			}
		}
	}

	private void HandleTimelineMouseUp(MouseEventArgs e)
	{
		_snapGuideScreenX = -1;
		_dragPartnerSegment = null;

		if (_isDraggingOverlay)
		{
			_isDraggingOverlay = false;
			_draggedOverlayItem = null;
			_cutEditTimelineCanvas.Cursor = (_cutEditCurrentTool == TimelineToolMode.Razor) ? Cursors.Cross : Cursors.Default;
			UpdateCutEditWpfOverlay(_cutEditCurrentPos, forcePreviewSelected: true);
			UpdateDeliverSummary();
			_cutEditTimelineCanvas?.Invalidate();
			return;
		}

		if (_timelineDragMode != TimelineDragMode.None)
		{
			_timelineDragMode = TimelineDragMode.None;
			_dragSegmentIndex = -1;
			_cutEditTimelineCanvas.Cursor = (_cutEditCurrentTool == TimelineToolMode.Razor) ? Cursors.Cross : Cursors.Default;
			RecalculateTimelineTotalDuration();
			UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
			_cutEditTimelineCanvas?.Invalidate();
		}
	}

	private void HandleTimelineDragEnter(DragEventArgs e)
	{
		if (e.Data.GetDataPresent(DataFormats.StringFormat) || e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			e.Effect = DragDropEffects.Copy;
		}
	}

	private void HandleTimelineDragOver(DragEventArgs e)
	{
		if (e.Data.GetDataPresent(DataFormats.StringFormat) || e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			Point cp = _cutEditTimelineCanvas.PointToClient(new Point(e.X, e.Y));
			var trk = GetTrackAtY(cp.Y);
			if (trk != null && trk.IsLocked)
			{
				e.Effect = DragDropEffects.None;
			}
			else
			{
				e.Effect = DragDropEffects.Copy;
			}
		}
	}

	private void HandleTimelineDragDrop(DragEventArgs e)
	{
		string dropped = null;
		if (e.Data.GetDataPresent(DataFormats.StringFormat))
		{
			dropped = e.Data.GetData(DataFormats.StringFormat) as string;
		}
		else if (e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			string[] fls = e.Data.GetData(DataFormats.FileDrop) as string[];
			if (fls != null && fls.Length > 0) dropped = fls[0];
		}
		if (!string.IsNullOrEmpty(dropped))
		{
			Point cp = _cutEditTimelineCanvas.PointToClient(new Point(e.X, e.Y));
			InsertMediaIntoTimelineAtPoint(dropped, cp);
		}
	}

	private void ShowSegmentContextMenu(Point canvasPt)
	{
		if (_cutEditSelectedSegmentIndex < 0 || _cutEditSelectedSegmentIndex >= _cutEditSegments.Count) return;
		var seg = _cutEditSegments[_cutEditSelectedSegmentIndex];
		ContextMenuStrip cms = new ContextMenuStrip();

		var itemRippleDel = cms.Items.Add("🌊 波纹删除此片段 (Shift+Del)", null, delegate { DeleteSelectedCutSegment(isRipple: true); });
		itemRippleDel.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		itemRippleDel.ForeColor = Color.FromArgb(234, 88, 12);

		var itemDel = cms.Items.Add("🗑️ 普通删除此片段 (Delete)", null, delegate { DeleteSelectedCutSegment(isRipple: false); });
		itemDel.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular);

		cms.Items.Add(new ToolStripSeparator());

		cms.Items.Add("✂️ 在当前播放指针处切割 (B)", null, delegate { SplitCutEditCurrentPosition(); });
		cms.Items.Add(seg.IsKept ? "🚫 标记为剔除" : "✅ 恢复为保留", null, delegate { ToggleSelectedSegmentKept(); });

		cms.Items.Add(new ToolStripSeparator());

		// 1. Audio/Video Separation for Video Segments
		if (seg.MediaType == "video" || seg.TrackId.StartsWith("V"))
		{
			cms.Items.Add("✂️ 音视频分离 (提取音频到A1轨道)", null, delegate
			{
				bool hasA1Already = _cutEditSegments.Any(s => s.TrackId == "A1" && s.SourcePath == seg.SourcePath && Math.Abs(s.TimelineStartSeconds - seg.TimelineStartSeconds) < 0.1);
				if (hasA1Already)
				{
					MessageBox.Show(this, "该片段在 A1 原声音轨上已有对应的独立音频片段，无需重复分离。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				CutSegment aSeg = new CutSegment
				{
					Id = Guid.NewGuid().ToString("N"),
					SourcePath = seg.SourcePath,
					StartSeconds = seg.StartSeconds,
					EndSeconds = seg.EndSeconds,
					TimelineStartSeconds = seg.TimelineStartSeconds,
					IsKept = seg.IsKept,
					TrackId = "A1",
					MediaType = "audio",
					Title = seg.Title + " (音频)",
					VolumePercent = 100,
					LinkedPartnerId = seg.Id
				};
				seg.LinkedPartnerId = aSeg.Id;
				_cutEditSegments.Add(aSeg);
				RecalculateTimelineTotalDuration();
				_cutEditTimelineCanvas?.Invalidate();
				RequestRealAudioWaveform(seg.SourcePath, Color.FromArgb(34, 197, 94));
				MessageBox.Show(this, "已成功将音频分离至 A1 原声音轨！\n画面与音频现已完全解绑，您可以自由独立剪切或替换画面与音频。", "音视频分离成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
			});

			// Seam transitions between adjacent clips on the same track
			CutSegment segPrev = _cutEditSegments
				.Where(s => s.TrackId == seg.TrackId && s != seg && s.TimelineStartSeconds < seg.TimelineStartSeconds)
				.OrderByDescending(s => s.TimelineStartSeconds)
				.FirstOrDefault();
			bool hasPrevAdjacent = (segPrev != null && Math.Abs((segPrev.TimelineStartSeconds + segPrev.Duration) - seg.TimelineStartSeconds) <= 0.4);

			CutSegment segNext = _cutEditSegments
				.Where(s => s.TrackId == seg.TrackId && s != seg && s.TimelineStartSeconds > seg.TimelineStartSeconds)
				.OrderBy(s => s.TimelineStartSeconds)
				.FirstOrDefault();
			bool hasNextAdjacent = (segNext != null && Math.Abs((seg.TimelineStartSeconds + seg.Duration) - segNext.TimelineStartSeconds) <= 0.4);

			if (hasNextAdjacent)
			{
				string currentTrans = (!string.IsNullOrEmpty(seg.TransitionOutType) && seg.TransitionOutType != "none") ? seg.TransitionOutType : segNext.TransitionInType;
				if (string.IsNullOrEmpty(currentTrans)) currentTrans = "none";
				double curDur = (seg.TransitionOutDuration > 0.05) ? seg.TransitionOutDuration : (segNext.TransitionInDuration > 0.05 ? segNext.TransitionInDuration : 0.5);

				ToolStripMenuItem seamNextMenu = new ToolStripMenuItem($"⚡ 在与【后一素材】接缝处加转场 ({GetTransitionName(currentTrans)})");
				seamNextMenu.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
				seamNextMenu.ForeColor = Color.FromArgb(147, 51, 234);

				foreach (var td in TransitionDefinitions)
				{
					string tid = td.id;
					var mi = seamNextMenu.DropDownItems.Add(td.name, null, delegate
					{
						ApplySeamTransition(seg, segNext, tid, curDur);
					});
					if (currentTrans == tid) mi.Font = new Font(mi.Font, FontStyle.Bold);
				}

				seamNextMenu.DropDownItems.Add(new ToolStripSeparator());
				ToolStripMenuItem durSub = new ToolStripMenuItem($"⏱️ 接缝转场时长 ({curDur:0.0}s)");
				foreach (var d in new double[] { 0.2, 0.3, 0.5, 0.8, 1.0, 1.5, 2.0 })
				{
					double curd = d;
					var mi = durSub.DropDownItems.Add($"{curd:0.0} 秒", null, delegate
					{
						string eff = (currentTrans == "none") ? "fade" : currentTrans;
						ApplySeamTransition(seg, segNext, eff, curd);
					});
					if (Math.Abs(curDur - curd) < 0.05 && currentTrans != "none") mi.Font = new Font(mi.Font, FontStyle.Bold);
				}
				seamNextMenu.DropDownItems.Add(durSub);

				if (currentTrans != "none")
				{
					seamNextMenu.DropDownItems.Add(new ToolStripSeparator());
					var delTrans = seamNextMenu.DropDownItems.Add("❌ 移除此接缝转场", null, delegate
					{
						RemoveSeamTransition(seg, segNext);
					});
					delTrans.ForeColor = Color.FromArgb(239, 68, 68);
				}

				cms.Items.Add(seamNextMenu);
			}

			if (hasPrevAdjacent)
			{
				string currentTrans = (!string.IsNullOrEmpty(segPrev.TransitionOutType) && segPrev.TransitionOutType != "none") ? segPrev.TransitionOutType : seg.TransitionInType;
				if (string.IsNullOrEmpty(currentTrans)) currentTrans = "none";
				double curDur = (segPrev.TransitionOutDuration > 0.05) ? segPrev.TransitionOutDuration : (seg.TransitionInDuration > 0.05 ? seg.TransitionInDuration : 0.5);

				ToolStripMenuItem seamPrevMenu = new ToolStripMenuItem($"⚡ 在与【前一素材】接缝处加转场 ({GetTransitionName(currentTrans)})");
				seamPrevMenu.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
				seamPrevMenu.ForeColor = Color.FromArgb(147, 51, 234);

				foreach (var td in TransitionDefinitions)
				{
					string tid = td.id;
					var mi = seamPrevMenu.DropDownItems.Add(td.name, null, delegate
					{
						ApplySeamTransition(segPrev, seg, tid, curDur);
					});
					if (currentTrans == tid) mi.Font = new Font(mi.Font, FontStyle.Bold);
				}

				seamPrevMenu.DropDownItems.Add(new ToolStripSeparator());
				ToolStripMenuItem durSub = new ToolStripMenuItem($"⏱️ 接缝转场时长 ({curDur:0.0}s)");
				foreach (var d in new double[] { 0.2, 0.3, 0.5, 0.8, 1.0, 1.5, 2.0 })
				{
					double curd = d;
					var mi = durSub.DropDownItems.Add($"{curd:0.0} 秒", null, delegate
					{
						string eff = (currentTrans == "none") ? "fade" : currentTrans;
						ApplySeamTransition(segPrev, seg, eff, curd);
					});
					if (Math.Abs(curDur - curd) < 0.05 && currentTrans != "none") mi.Font = new Font(mi.Font, FontStyle.Bold);
				}
				seamPrevMenu.DropDownItems.Add(durSub);

				if (currentTrans != "none")
				{
					seamPrevMenu.DropDownItems.Add(new ToolStripSeparator());
					var delTrans = seamPrevMenu.DropDownItems.Add("❌ 移除此接缝转场", null, delegate
					{
						RemoveSeamTransition(segPrev, seg);
					});
					delTrans.ForeColor = Color.FromArgb(239, 68, 68);
				}

				cms.Items.Add(seamPrevMenu);
			}

			if (hasPrevAdjacent || hasNextAdjacent)
			{
				cms.Items.Add(new ToolStripSeparator());
			}

			// 2. Single Clip Transitions Menu
			ToolStripMenuItem inTransMenu = new ToolStripMenuItem($"⚡ 片头转场 ({GetTransitionName(seg.TransitionInType)})");
			foreach (var td in TransitionDefinitions)
			{
				string tid = td.id;
				var mi = inTransMenu.DropDownItems.Add(td.name, null, delegate
				{
					PushCutEditUndoState("设置片头转场");
					seg.TransitionInType = tid;
					_cutEditTimelineCanvas?.Invalidate();
					UpdateDeliverSummary();
					UpdateCutEditWpfOverlay(_cutEditCurrentPos, forcePreviewSelected: false);
					UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
				});
				if (seg.TransitionInType == tid) mi.Font = new Font(mi.Font, FontStyle.Bold);
			}
			cms.Items.Add(inTransMenu);

			ToolStripMenuItem outTransMenu = new ToolStripMenuItem($"⚡ 片尾转场 ({GetTransitionName(seg.TransitionOutType)})");
			foreach (var td in TransitionDefinitions)
			{
				string tid = td.id;
				var mi = outTransMenu.DropDownItems.Add(td.name, null, delegate
				{
					PushCutEditUndoState("设置片尾转场");
					seg.TransitionOutType = tid;
					_cutEditTimelineCanvas?.Invalidate();
					UpdateDeliverSummary();
					UpdateCutEditWpfOverlay(_cutEditCurrentPos, forcePreviewSelected: false);
					UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
				});
				if (seg.TransitionOutType == tid) mi.Font = new Font(mi.Font, FontStyle.Bold);
			}
			cms.Items.Add(outTransMenu);

			ToolStripMenuItem durMenu = new ToolStripMenuItem($"⏱️ 单素材转场时长 ({seg.TransitionInDuration:0.0}s)");
			double[] durs = new double[] { 0.2, 0.3, 0.5, 0.8, 1.0, 1.5, 2.0 };
			foreach (var d in durs)
			{
				double curd = d;
				var mi = durMenu.DropDownItems.Add($"{curd:0.0} 秒", null, delegate
				{
					PushCutEditUndoState("修改转场时长");
					seg.TransitionInDuration = curd;
					seg.TransitionOutDuration = curd;
					_cutEditTimelineCanvas?.Invalidate();
					UpdateDeliverSummary();
					UpdateCutEditWpfOverlay(_cutEditCurrentPos, forcePreviewSelected: false);
					UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
				});
				if (Math.Abs(seg.TransitionInDuration - curd) < 0.05) mi.Font = new Font(mi.Font, FontStyle.Bold);
			}
			cms.Items.Add(durMenu);

			cms.Items.Add(new ToolStripSeparator());
		}
		else if (seg.MediaType == "audio")
		{
			// Volume Adjustment for Audio Segments
			ToolStripMenuItem volMenu = new ToolStripMenuItem($"🔊 调节片段音量 ({seg.VolumePercent}%)");
			int[] vols = new int[] { 200, 150, 120, 100, 80, 50, 20, 0 };
			foreach (var v in vols)
			{
				int curv = v;
				string label = curv switch
				{
					200 => "200% (🔥 双倍超强增益)",
					150 => "150% (增益放大 / 人声强化)",
					120 => "120% (微调提升)",
					100 => "100% (标准原声)",
					80 => "80% (轻度压暗)",
					50 => "50% (背景弱化)",
					20 => "20% (极微背景音)",
					0 => "0% (静音)",
					_ => $"{curv}%"
				};
				var mi = volMenu.DropDownItems.Add(label, null, delegate
				{
					PushCutEditUndoState("调节音频音量");
					seg.VolumePercent = curv;
					_cutEditTimelineCanvas?.Invalidate();
					UpdateDeliverSummary();
				});
				if (seg.VolumePercent == curv) mi.Font = new Font(mi.Font, FontStyle.Bold);
			}
			cms.Items.Add(volMenu);
			cms.Items.Add(new ToolStripSeparator());
		}

		var videoTracks = _cutEditTracks.Where(t => t.Type == TrackType.Video).ToList();
		if (videoTracks.Count > 1 && (seg.MediaType == "video" || seg.MediaType == "image"))
		{
			ToolStripMenuItem moveTrackMenu = new ToolStripMenuItem("↕️ 转移到指定轨道");
			foreach (var vt in videoTracks)
			{
				string tid = vt.Id;
				var mi = moveTrackMenu.DropDownItems.Add($"{vt.Name} ({tid})", null, delegate
				{
					if (vt.IsLocked)
					{
						MessageBox.Show(this, $"轨道【{vt.Name}】已锁定！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						return;
					}
					seg.TrackId = tid;
					_cutEditSelectedTrackId = tid;
					_cutEditTimelineCanvas?.Invalidate();
				});
				if (seg.TrackId == tid) mi.Font = new Font(mi.Font, FontStyle.Bold);
			}
			cms.Items.Add(moveTrackMenu);
		}

		cms.Show(_cutEditTimelineCanvas, canvasPt);
	}

	private static string GetTransitionName(string id)
	{
		if (string.IsNullOrEmpty(id) || id == "none") return "无";
		var td = TransitionDefinitions.FirstOrDefault(t => t.id == id);
		return td.shortName ?? id;
	}

	private int GetTrackY(string trackId)
	{
		int curY = 26;
		foreach (var trk in _cutEditTracks)
		{
			if (trk.Id == trackId) return curY;
			curY += trk.Height + 2;
		}
		return curY;
	}

	private bool FindSeamAtPoint(Point pt, out CutSegment segLeft, out CutSegment segRight, out Rectangle seamBadgeRect)
	{
		segLeft = null;
		segRight = null;
		seamBadgeRect = Rectangle.Empty;
		if (pt.X < 92 || _cutEditDuration <= 0.0 || _cutEditTimelineCanvas == null) return false;
		int canvasW = _cutEditTimelineCanvas.ClientSize.Width;

		var trk = GetTrackAtY(pt.Y);
		if (trk == null || trk.Type != TrackType.Video) return false;

		var trackSegs = _cutEditSegments.Where(s => s.TrackId == trk.Id).OrderBy(s => s.TimelineStartSeconds).ToList();
		for (int i = 0; i < trackSegs.Count - 1; i++)
		{
			var s1 = trackSegs[i];
			var s2 = trackSegs[i + 1];
			double s1End = s1.TimelineStartSeconds + s1.Duration;
			if (Math.Abs(s2.TimelineStartSeconds - s1End) <= 0.4)
			{
				double seamTime = (s1End + s2.TimelineStartSeconds) / 2.0;
				int seamX = TimeToScreenX(seamTime, canvasW);
				int curY = GetTrackY(trk.Id);
				Rectangle badge = new Rectangle(seamX - 34, curY + 2, 68, trk.Height - 4);
				if (badge.Contains(pt) || (Math.Abs(pt.X - seamX) <= 18 && pt.Y >= curY && pt.Y < curY + trk.Height))
				{
					segLeft = s1;
					segRight = s2;
					seamBadgeRect = badge;
					return true;
				}
			}
		}
		return false;
	}

	private void ApplySeamTransition(CutSegment segLeft, CutSegment segRight, string transType, double duration)
	{
		if (segLeft == null || segRight == null) return;
		PushCutEditUndoState("应用接缝转场");
		segLeft.TransitionOutType = transType;
		segLeft.TransitionOutDuration = duration;
		segRight.TransitionInType = transType;
		segRight.TransitionInDuration = duration;
		_cutEditTimelineCanvas?.Invalidate();
		UpdateDeliverSummary();
		UpdateCutEditWpfOverlay(_cutEditCurrentPos, forcePreviewSelected: false);
		UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
	}

	private void RemoveSeamTransition(CutSegment segLeft, CutSegment segRight)
	{
		if (segLeft == null && segRight == null) return;
		PushCutEditUndoState("移除接缝转场");
		if (segLeft != null)
		{
			segLeft.TransitionOutType = "none";
		}
		if (segRight != null)
		{
			segRight.TransitionInType = "none";
		}
		_cutEditTimelineCanvas?.Invalidate();
		UpdateDeliverSummary();
		UpdateCutEditWpfOverlay(_cutEditCurrentPos, forcePreviewSelected: false);
		UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
	}

	private void CloseGapBetween(CutSegment segLeft, CutSegment segRight)
	{
		if (segLeft == null || segRight == null) return;
		double s1End = segLeft.TimelineStartSeconds + segLeft.Duration;
		double gap = segRight.TimelineStartSeconds - s1End;
		if (gap <= 0.001) return;

		PushCutEditUndoState("闭合素材间隙");
		double threshold = segRight.TimelineStartSeconds - 0.001;
		foreach (var s in _cutEditSegments)
		{
			if (s.TrackId == segRight.TrackId && s.TimelineStartSeconds >= threshold)
			{
				s.TimelineStartSeconds = Math.Max(0.0, s.TimelineStartSeconds - gap);
				if (_cutEditLinkedSelectionEnabled && !string.IsNullOrEmpty(s.LinkedPartnerId))
				{
					var p = _cutEditSegments.FirstOrDefault(x => x.Id == s.LinkedPartnerId);
					if (p != null) p.TimelineStartSeconds = s.TimelineStartSeconds;
				}
			}
		}
		RecalculateTimelineTotalDuration();
		_cutEditTimelineCanvas?.Invalidate();
		UpdateDeliverSummary();
	}

	private void CloseAllTimelineGaps()
	{
		if (_cutEditSegments.Count == 0) return;
		PushCutEditUndoState("闭合全轨所有间隙");

		int closedCount = 0;
		foreach (var trk in _cutEditTracks)
		{
			if (trk.IsLocked) continue;
			var segs = _cutEditSegments.Where(s => s.TrackId == trk.Id).OrderBy(s => s.TimelineStartSeconds).ToList();
			if (segs.Count == 0) continue;

			if (segs[0].TimelineStartSeconds > 0.02)
			{
				double leadGap = segs[0].TimelineStartSeconds;
				for (int i = 0; i < segs.Count; i++)
				{
					segs[i].TimelineStartSeconds = Math.Max(0.0, segs[i].TimelineStartSeconds - leadGap);
				}
				closedCount++;
			}

			for (int i = 0; i < segs.Count - 1; i++)
			{
				double s1End = segs[i].TimelineStartSeconds + segs[i].Duration;
				double gap = segs[i + 1].TimelineStartSeconds - s1End;
				if (gap > 0.02)
				{
					for (int j = i + 1; j < segs.Count; j++)
					{
						segs[j].TimelineStartSeconds = Math.Max(0.0, segs[j].TimelineStartSeconds - gap);
					}
					closedCount++;
				}
			}
		}

		if (_cutEditLinkedSelectionEnabled)
		{
			foreach (var s in _cutEditSegments)
			{
				if (!string.IsNullOrEmpty(s.LinkedPartnerId))
				{
					var p = _cutEditSegments.FirstOrDefault(x => x.Id == s.LinkedPartnerId);
					if (p != null && Math.Abs(p.TimelineStartSeconds - s.TimelineStartSeconds) > 0.02)
					{
						p.TimelineStartSeconds = s.TimelineStartSeconds;
					}
				}
			}
		}

		RecalculateTimelineTotalDuration();
		_cutEditTimelineCanvas?.Invalidate();
		UpdateDeliverSummary();
		MessageBox.Show(this, "已成功闭合全轨道所有素材间隙！所有黑屏与静音空隙已被消除。", "闭合间隙完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void ShowSeamTransitionContextMenu(Point canvasPt, CutSegment segLeft, CutSegment segRight)
	{
		if (segLeft == null || segRight == null) return;
		ContextMenuStrip cms = new ContextMenuStrip();
		cms.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);

		string currentTrans = (!string.IsNullOrEmpty(segLeft.TransitionOutType) && segLeft.TransitionOutType != "none") 
			? segLeft.TransitionOutType 
			: segRight.TransitionInType;
		if (string.IsNullOrEmpty(currentTrans)) currentTrans = "none";
		double currentDur = (segLeft.TransitionOutDuration > 0.05) ? segLeft.TransitionOutDuration : (segRight.TransitionInDuration > 0.05 ? segRight.TransitionInDuration : 0.5);

		var headerItem = cms.Items.Add($"⚡ 素材接缝转场设置 ({GetTransitionName(currentTrans)})");
		headerItem.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		headerItem.ForeColor = Color.FromArgb(147, 51, 234);
		headerItem.Enabled = false;

		cms.Items.Add(new ToolStripSeparator());

		foreach (var td in TransitionDefinitions)
		{
			string tid = td.id;
			var mi = cms.Items.Add(td.name, null, delegate
			{
				ApplySeamTransition(segLeft, segRight, tid, currentDur);
			});
			if (currentTrans == tid)
			{
				mi.Font = new Font(mi.Font, FontStyle.Bold);
				mi.Text = "✓ " + mi.Text;
			}
		}

		cms.Items.Add(new ToolStripSeparator());

		var durSub = new ToolStripMenuItem($"⏱️ 快速调整转场时长 ({currentDur:0.0}秒)");
		durSub.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		durSub.ForeColor = Color.FromArgb(59, 130, 246);
		(double durVal, string durDesc)[] durPresets = new (double, string)[]
		{
			(0.2, "0.2 秒 (极速快切)"),
			(0.3, "0.3 秒 (轻快转场)"),
			(0.5, "0.5 秒 (⭐ 经典推荐)"),
			(0.8, "0.8 秒 (舒缓转场)"),
			(1.0, "1.0 秒 (标准电影感)"),
			(1.5, "1.5 秒 (悠长慢淡)"),
			(2.0, "2.0 秒 (超长抒情)")
		};
		foreach (var preset in durPresets)
		{
			double d = preset.durVal;
			var mi = durSub.DropDownItems.Add(preset.durDesc, null, delegate
			{
				string effectiveType = (currentTrans == "none") ? "fade" : currentTrans;
				ApplySeamTransition(segLeft, segRight, effectiveType, d);
			});
			if (Math.Abs(currentDur - d) < 0.05 && currentTrans != "none")
			{
				mi.Font = new Font(mi.Font, FontStyle.Bold);
				mi.Text = "✓ " + mi.Text;
			}
		}
		cms.Items.Add(durSub);

		if (currentTrans != "none")
		{
			cms.Items.Add(new ToolStripSeparator());
			var delItem = cms.Items.Add("❌ 移除此接缝转场", null, delegate
			{
				RemoveSeamTransition(segLeft, segRight);
			});
			delItem.ForeColor = Color.FromArgb(239, 68, 68);
		}

		double gapSec = segRight.TimelineStartSeconds - (segLeft.TimelineStartSeconds + segLeft.Duration);
		if (gapSec > 0.02)
		{
			cms.Items.Add(new ToolStripSeparator());
			var closeGapItem = cms.Items.Add($"🧲 闭合两素材间隙 (消除 {gapSec:0.2}秒 黑屏)", null, delegate
			{
				CloseGapBetween(segLeft, segRight);
			});
			closeGapItem.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
			closeGapItem.ForeColor = Color.FromArgb(16, 185, 129);
		}

		cms.Show(_cutEditTimelineCanvas, canvasPt);
	}

	private void ShowTimelineEmptyContextMenu(Point canvasPt)
	{
		int canvasW = _cutEditTimelineCanvas.ClientSize.Width;
		double clickTime = ScreenXToTime(canvasPt.X, canvasW);
		var trk = GetTrackAtY(canvasPt.Y);

		ContextMenuStrip cms = new ContextMenuStrip();
		cms.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);

		CutSegment segBefore = null;
		CutSegment segAfter = null;
		if (trk != null)
		{
			var segs = _cutEditSegments.Where(s => s.TrackId == trk.Id).OrderBy(s => s.TimelineStartSeconds).ToList();
			for (int i = 0; i < segs.Count; i++)
			{
				var s = segs[i];
				if (s.TimelineStartSeconds + s.Duration <= clickTime + 0.01)
				{
					segBefore = s;
				}
				else if (s.TimelineStartSeconds >= clickTime - 0.01)
				{
					segAfter = s;
					break;
				}
			}
		}

		if (segBefore != null && segAfter != null)
		{
			double s1End = segBefore.TimelineStartSeconds + segBefore.Duration;
			double gapDur = segAfter.TimelineStartSeconds - s1End;
			if (gapDur > 0.02)
			{
				var miCloseGap = cms.Items.Add($"🧲 闭合此处间隙 (消除 {gapDur:0.2}秒 空隙)", null, delegate
				{
					CloseGapBetween(segBefore, segAfter);
				});
				miCloseGap.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
				miCloseGap.ForeColor = Color.FromArgb(16, 185, 129);
				cms.Items.Add(new ToolStripSeparator());
			}
		}

		cms.Items.Add("🧲 紧凑排列 / 闭合全轨道所有间隙", null, delegate
		{
			CloseAllTimelineGaps();
		});

		cms.Items.Add(new ToolStripSeparator());

		cms.Items.Add($"⏱️ 移动播放指针至此处 ({FormatDuration(clickTime)})", null, delegate
		{
			_cutEditCurrentPos = Math.Min(clickTime, _cutEditDuration);
			UpdateCutEditTimeLabel();
			SeekCutEditVideo(_cutEditCurrentPos);
			_cutEditTimelineCanvas?.Invalidate();
		});

		cms.Show(_cutEditTimelineCanvas, canvasPt);
	}

	private void RippleTrimLeft()
	{
		if (_cutEditDuration <= 0.0 || _cutEditSegments.Count == 0) return;
		double pos = _cutEditCurrentPos;

		CutSegment target = null;
		if (!string.IsNullOrEmpty(_cutEditSelectedTrackId))
		{
			target = _cutEditSegments.FirstOrDefault(s => s.TrackId == _cutEditSelectedTrackId && pos > s.TimelineStartSeconds + 0.02 && pos < s.TimelineStartSeconds + s.Duration - 0.02);
		}
		if (target == null)
		{
			target = _cutEditSegments.FirstOrDefault(s => pos > s.TimelineStartSeconds + 0.02 && pos < s.TimelineStartSeconds + s.Duration - 0.02);
		}
		if (target == null) return;

		var trk = _cutEditTracks.FirstOrDefault(t => t.Id == target.TrackId);
		if (trk?.IsLocked == true)
		{
			MessageBox.Show(this, $"轨道【{trk.Name}】已锁定，无法剪辑！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		PushCutEditUndoState("Q 快速波纹剪首");

		double trimSec = pos - target.TimelineStartSeconds;
		if (trimSec <= 0.02 || target.Duration - trimSec < 0.1) return;

		target.StartSeconds += trimSec;

		CutSegment partner = null;
		if (_cutEditLinkedSelectionEnabled && !Control.ModifierKeys.HasFlag(Keys.Alt) && !string.IsNullOrEmpty(target.LinkedPartnerId))
		{
			partner = _cutEditSegments.FirstOrDefault(s => s.Id == target.LinkedPartnerId);
			if (partner != null && partner.Duration - trimSec >= 0.1)
			{
				partner.StartSeconds += trimSec;
			}
		}

		foreach (var s in _cutEditSegments)
		{
			if (s != target && s != partner && s.TimelineStartSeconds >= target.TimelineStartSeconds + target.Duration - 0.01)
			{
				s.TimelineStartSeconds = Math.Max(0.0, s.TimelineStartSeconds - trimSec);
			}
		}
		foreach (var ol in _cutEditOverlays)
		{
			if (ol.StartSeconds >= target.TimelineStartSeconds + target.Duration - 0.01)
			{
				ol.StartSeconds = Math.Max(0.0, ol.StartSeconds - trimSec);
			}
		}

		_cutEditCurrentPos = target.TimelineStartSeconds;
		RecalculateTimelineTotalDuration();
		UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
		UpdateCutEditTimeLabel();
		_cutEditTimelineCanvas?.Invalidate();
		UpdateDeliverSummary();
	}

	private void RippleTrimRight()
	{
		if (_cutEditDuration <= 0.0 || _cutEditSegments.Count == 0) return;
		double pos = _cutEditCurrentPos;

		CutSegment target = null;
		if (!string.IsNullOrEmpty(_cutEditSelectedTrackId))
		{
			target = _cutEditSegments.FirstOrDefault(s => s.TrackId == _cutEditSelectedTrackId && pos > s.TimelineStartSeconds + 0.02 && pos < s.TimelineStartSeconds + s.Duration - 0.02);
		}
		if (target == null)
		{
			target = _cutEditSegments.FirstOrDefault(s => pos > s.TimelineStartSeconds + 0.02 && pos < s.TimelineStartSeconds + s.Duration - 0.02);
		}
		if (target == null) return;

		var trk = _cutEditTracks.FirstOrDefault(t => t.Id == target.TrackId);
		if (trk?.IsLocked == true)
		{
			MessageBox.Show(this, $"轨道【{trk.Name}】已锁定，无法剪辑！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		PushCutEditUndoState("W 快速波纹剪尾");

		double trimSec = (target.TimelineStartSeconds + target.Duration) - pos;
		if (trimSec <= 0.02 || target.Duration - trimSec < 0.1) return;

		target.EndSeconds = target.StartSeconds + (pos - target.TimelineStartSeconds);

		CutSegment partner = null;
		if (_cutEditLinkedSelectionEnabled && !Control.ModifierKeys.HasFlag(Keys.Alt) && !string.IsNullOrEmpty(target.LinkedPartnerId))
		{
			partner = _cutEditSegments.FirstOrDefault(s => s.Id == target.LinkedPartnerId);
			if (partner != null && partner.Duration - trimSec >= 0.1)
			{
				partner.EndSeconds = partner.StartSeconds + (pos - partner.TimelineStartSeconds);
			}
		}

		foreach (var s in _cutEditSegments)
		{
			if (s != target && s != partner && s.TimelineStartSeconds >= pos - 0.01)
			{
				s.TimelineStartSeconds = Math.Max(0.0, s.TimelineStartSeconds - trimSec);
			}
		}
		foreach (var ol in _cutEditOverlays)
		{
			if (ol.StartSeconds >= pos - 0.01)
			{
				ol.StartSeconds = Math.Max(0.0, ol.StartSeconds - trimSec);
			}
		}

		_cutEditCurrentPos = pos;
		RecalculateTimelineTotalDuration();
		UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
		UpdateCutEditTimeLabel();
		_cutEditTimelineCanvas?.Invalidate();
		UpdateDeliverSummary();
	}

	private void DeleteSelectedCutSegment(bool isRipple = false)
	{
		if (_cutEditSelectedSegmentIndex < 0 || _cutEditSelectedSegmentIndex >= _cutEditSegments.Count)
		{
			// Check if playhead is over a segment on selected track
			for (int i = 0; i < _cutEditSegments.Count; i++)
			{
				var s = _cutEditSegments[i];
				if (s.TrackId == _cutEditSelectedTrackId && _cutEditCurrentPos >= s.TimelineStartSeconds && _cutEditCurrentPos <= s.TimelineStartSeconds + s.Duration)
				{
					_cutEditSelectedSegmentIndex = i;
					break;
				}
			}
		}

		if (_cutEditSelectedSegmentIndex < 0 || _cutEditSelectedSegmentIndex >= _cutEditSegments.Count)
		{
			// Check if playhead is over ANY segment
			for (int i = 0; i < _cutEditSegments.Count; i++)
			{
				var s = _cutEditSegments[i];
				if (_cutEditCurrentPos >= s.TimelineStartSeconds && _cutEditCurrentPos <= s.TimelineStartSeconds + s.Duration)
				{
					_cutEditSelectedSegmentIndex = i;
					break;
				}
			}
		}

		if (_cutEditSelectedSegmentIndex >= 0 && _cutEditSelectedSegmentIndex < _cutEditSegments.Count)
		{
			var seg = _cutEditSegments[_cutEditSelectedSegmentIndex];
			var trk = _cutEditTracks.FirstOrDefault(t => t.Id == seg.TrackId);
			if (trk?.IsLocked == true)
			{
				MessageBox.Show(this, $"轨道【{trk.Name}】已锁定，无法删除其中的片段！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			PushCutEditUndoState(isRipple ? "波纹删除" : "删除片段");

			double delStart = seg.TimelineStartSeconds;
			double delDur = seg.Duration;
			string partnerId = seg.LinkedPartnerId;

			_cutEditSegments.RemoveAt(_cutEditSelectedSegmentIndex);
			_cutEditSelectedSegmentIndex = -1;

			// Also remove linked partner if linked selection is active and not holding Alt
			if (_cutEditLinkedSelectionEnabled && !Control.ModifierKeys.HasFlag(Keys.Alt) && !string.IsNullOrEmpty(partnerId))
			{
				var partner = _cutEditSegments.FirstOrDefault(s => s.Id == partnerId);
				if (partner != null)
				{
					_cutEditSegments.Remove(partner);
				}
			}

			if (isRipple)
			{
				// Ripple shift all subsequent segments forward
				foreach (var s in _cutEditSegments)
				{
					if (s.TimelineStartSeconds >= delStart - 0.001)
					{
						s.TimelineStartSeconds = Math.Max(0.0, s.TimelineStartSeconds - delDur);
					}
				}
				// Also ripple subtitle / overlays
				foreach (var ol in _cutEditOverlays)
				{
					if (ol.StartSeconds >= delStart - 0.001)
					{
						ol.StartSeconds = Math.Max(0.0, ol.StartSeconds - delDur);
					}
				}
			}

			RecalculateTimelineTotalDuration();

			if (_cutEditSegments.Count == 0)
			{
				_cutEditDuration = 0.0;
				_cutEditCurrentPos = 0.0;
				if (_cutEditEmptyPlaceholder != null) _cutEditEmptyPlaceholder.Visible = true;
				if (_cutEditPreviewBox != null) _cutEditPreviewBox.Image = null;
				if (_cutEditMediaElement != null)
				{
					try { _cutEditMediaElement.Source = null; } catch { }
				}
			}
			else
			{
				_cutEditCurrentPos = Math.Min(_cutEditCurrentPos, _cutEditDuration);
				UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
			}

			_cutEditTimelineCanvas?.Invalidate();
			UpdateDeliverSummary();
		}
	}

	private void HandleTimelineSeek(MouseEventArgs e)
	{
		if (_cutEditDuration <= 0.0 || _cutEditTimelineCanvas == null) return;
		int canvasW = _cutEditTimelineCanvas.ClientSize.Width;

		double seekTime = ScreenXToTime(e.X, canvasW);
		if (_cutEditSnappingEnabled)
		{
			bool didSnap;
			seekTime = SnapTimeToNearestBoundary(seekTime, canvasW, out didSnap);
		}

		_cutEditCurrentPos = seekTime;
		SeekCutEditVideo(seekTime);
		if (_cutEditTimeScrubber != null)
		{
			_cutEditTimeScrubber.Value = (int)Math.Max(0, Math.Min(1000, (seekTime / _cutEditDuration) * 1000.0));
		}
		UpdateCutEditTimeLabel();
		_cutEditTimelineCanvas?.Invalidate();
	}

	private void PaintTimelineCanvas(Graphics g, Rectangle bounds)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		g.Clear(Color.FromArgb(16, 20, 28));

		int headerW = 92;
		int padR = 14;
		int trackW = Math.Max(10, (int)((bounds.Width - headerW - padR) * _cutEditTimelineZoom));
		double dur = Math.Max(0.1, _cutEditDuration);

		// 1. Top Time Ruler (Y: 0..24)
		Rectangle rulerRect = new Rectangle(0, 0, bounds.Width, 24);
		using (Brush rulerBg = new SolidBrush(Color.FromArgb(12, 15, 22)))
		{
			g.FillRectangle(rulerBg, rulerRect);
		}
		using (Pen rulerBorder = new Pen(Color.FromArgb(40, 50, 68), 1f))
		{
			g.DrawLine(rulerBorder, 0, 24, bounds.Width, 24);
		}

		// Clip ruler ticks to visible lanes area
		Rectangle rulerClip = new Rectangle(headerW + 1, 0, bounds.Width - headerW - 1, 24);
		g.SetClip(rulerClip);

		using (Font fRuler = new Font("Consolas", 8f, FontStyle.Regular))
		using (Brush bRuler = new SolidBrush(Color.FromArgb(148, 163, 184)))
		using (Pen pTickMajor = new Pen(Color.FromArgb(94, 110, 134), 1f))
		using (Pen pTickMinor = new Pen(Color.FromArgb(51, 65, 85), 1f))
		{
			double stepSec = (dur <= 30.0) ? 5.0 : ((dur <= 90.0) ? 10.0 : ((dur <= 300.0) ? 30.0 : 60.0));
			double minorStep = stepSec / 5.0;

			for (double mt = 0; mt <= dur; mt += minorStep)
			{
				int tx = TimeToScreenX(mt, bounds.Width);
				if (tx < headerW) continue;
				if (tx > bounds.Width) break;
				g.DrawLine(pTickMinor, tx, 18, tx, 24);
			}

			for (double t = 0; t <= dur; t += stepSec)
			{
				int tx = TimeToScreenX(t, bounds.Width);
				if (tx < headerW - 20) continue;
				if (tx > bounds.Width) break;
				g.DrawLine(pTickMajor, tx, 12, tx, 24);
				string timeStr = FormatDuration(t).Split('.')[0];
				g.DrawString(timeStr, fRuler, bRuler, tx - 14, 2);
			}
		}
		g.ResetClip();

		// 2. Track Lanes (Clip to headerW + 1 .. bounds.Width)
		int curY = 26;
		using (Font badgeFont = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold))
		using (Font iconFont = new Font("Segoe UI Emoji", 8.5f, FontStyle.Regular))
		using (Font segFont = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular))
		using (StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
		{
			foreach (var trk in _cutEditTracks)
			{
				int th = trk.Height;
				Rectangle headerRect = new Rectangle(0, curY, headerW, th);
				Rectangle laneRect = new Rectangle(headerW + 2, curY, bounds.Width - headerW - 2, th);

				// Draw Lane background
				using (Brush laneBrush = new SolidBrush(Color.FromArgb(12, 16, 24)))
				{
					g.FillRectangle(laneBrush, laneRect);
				}
				using (Pen laneBorder = new Pen(Color.FromArgb(28, 36, 50), 1f))
				{
					g.DrawLine(laneBorder, headerW, curY + th, bounds.Width, curY + th);
				}

				// Clip lanes for drawing segments, waveforms, and overlays
				Rectangle laneClip = new Rectangle(headerW + 1, curY, bounds.Width - headerW - 1, th);
				g.SetClip(laneClip);

				// --- Paint Segments on this Track ---
				if (_cutEditSegments.Count == 0)
				{
					if (trk.Id == "V1")
					{
						using (Brush emptyTxt = new SolidBrush(Color.FromArgb(100, 116, 139)))
						{
							g.DrawString("📽️ 轨道当前为空 | 请在左侧媒体池选择素材点击【➕ 添加到轨道】或直接拖拽素材到此处", segFont, emptyTxt, laneRect, sfCenter);
						}
					}
				}
				else
				{
					for (int i = 0; i < _cutEditSegments.Count; i++)
					{
						var seg = _cutEditSegments[i];
						bool matchTrack = (seg.TrackId == trk.Id);
						if (!matchTrack) continue;

						int sx = TimeToScreenX(seg.TimelineStartSeconds, bounds.Width);
						int ex = TimeToScreenX(seg.TimelineStartSeconds + seg.Duration, bounds.Width);
						int sw = Math.Max(6, ex - sx);
						if (sx + sw < headerW || sx > bounds.Width) continue;

						Rectangle segRect = new Rectangle(sx, curY + 2, sw, th - 4);
						bool isSelected = (_cutEditSelectedSegmentIndex == i);

						bool isLinkedPartnerSelected = false;
						if (!isSelected && _cutEditLinkedSelectionEnabled && _cutEditSelectedSegmentIndex >= 0 && _cutEditSelectedSegmentIndex < _cutEditSegments.Count)
						{
							var curSel = _cutEditSegments[_cutEditSelectedSegmentIndex];
							if (!string.IsNullOrEmpty(curSel.LinkedPartnerId) && curSel.LinkedPartnerId == seg.Id)
							{
								isLinkedPartnerSelected = true;
							}
						}

						if (trk.Type == TrackType.Video)
						{
							bool isImg = (seg.MediaType == "image") || IsImage(seg.SourcePath);
							Color c1 = isImg ? Color.FromArgb(99, 102, 241) : Color.FromArgb(37, 99, 235);
							Color c2 = isImg ? Color.FromArgb(67, 56, 202) : Color.FromArgb(30, 64, 175);

							if (seg.IsKept)
							{
								using (LinearGradientBrush lgb = new LinearGradientBrush(segRect, c1, c2, LinearGradientMode.Vertical))
								{
									g.FillRectangle(lgb, segRect);
								}

								Color borderColor = isSelected
									? Color.FromArgb(234, 179, 8)
									: (isLinkedPartnerSelected ? Color.FromArgb(56, 189, 248) : Color.FromArgb(96, 165, 250));
								float borderWidth = isSelected ? 2.5f : (isLinkedPartnerSelected ? 2f : 1f);

								using (Pen p = new Pen(borderColor, borderWidth))
								{
									if (isLinkedPartnerSelected) p.DashStyle = DashStyle.Dash;
									g.DrawRectangle(p, segRect);
								}

								// Golden trim handles on selected segment
								if (isSelected && sw >= 16)
								{
									using (Pen pHandle = new Pen(Color.FromArgb(253, 224, 71), 2.5f))
									{
										g.DrawLine(pHandle, sx + 4, curY + 4, sx + 1, curY + 4);
										g.DrawLine(pHandle, sx + 1, curY + 4, sx + 1, curY + th - 6);
										g.DrawLine(pHandle, sx + 1, curY + th - 6, sx + 4, curY + th - 6);

										g.DrawLine(pHandle, sx + sw - 5, curY + 4, sx + sw - 2, curY + 4);
										g.DrawLine(pHandle, sx + sw - 2, curY + 4, sx + sw - 2, curY + th - 6);
										g.DrawLine(pHandle, sx + sw - 2, curY + th - 6, sx + sw - 5, curY + th - 6);
									}
								}

								if (sw >= 28)
								{
									string typePrefix = isImg ? "🖼️ " : "🎬 ";
									string linkPrefix = !string.IsNullOrEmpty(seg.LinkedPartnerId) ? "🔗 " : "";
									string label = string.IsNullOrEmpty(seg.Title)
										? $"{linkPrefix}{typePrefix}#{i + 1} {FormatDuration(seg.Duration)}"
										: $"{linkPrefix}{typePrefix}#{i + 1} {seg.Title}";
									using (Brush txtBrush = new SolidBrush(Color.White))
									{
										g.DrawString(label, segFont, txtBrush, segRect, sfCenter);
									}
								}

								// Transition badges
								if (!string.IsNullOrEmpty(seg.TransitionInType) && seg.TransitionInType != "none" && sw >= 18)
								{
									string inShort = GetTransitionName(seg.TransitionInType);
									int bW = Math.Min(50, sw / 2);
									Rectangle inBadge = new Rectangle(sx + 2, curY + 3, bW, 16);
									using (GraphicsPath bp = CreateRoundedRectanglePath(inBadge, 3))
									using (Brush bBg = new SolidBrush(Color.FromArgb(168, 85, 247)))
									using (Brush bText = new SolidBrush(Color.White))
									{
										g.FillPath(bBg, bp);
										g.DrawString($"⚡{inShort}", badgeFont, bText, inBadge, sfCenter);
									}
								}
								if (!string.IsNullOrEmpty(seg.TransitionOutType) && seg.TransitionOutType != "none" && sw >= 18)
								{
									string outShort = GetTransitionName(seg.TransitionOutType);
									int bW = Math.Min(50, sw / 2);
									Rectangle outBadge = new Rectangle(sx + sw - bW - 2, curY + 3, bW, 16);
									using (GraphicsPath bp = CreateRoundedRectanglePath(outBadge, 3))
									using (Brush bBg = new SolidBrush(Color.FromArgb(234, 88, 12)))
									using (Brush bText = new SolidBrush(Color.White))
									{
										g.FillPath(bBg, bp);
										g.DrawString($"⚡{outShort}", badgeFont, bText, outBadge, sfCenter);
									}
								}
							}
							else
							{
								using (HatchBrush hb = new HatchBrush(HatchStyle.LightUpwardDiagonal, Color.FromArgb(220, 38, 38), Color.FromArgb(69, 10, 10)))
								{
									g.FillRectangle(hb, segRect);
								}
								using (Pen p = new Pen(isSelected ? Color.FromArgb(234, 179, 8) : Color.FromArgb(239, 68, 68), isSelected ? 2f : 1f))
								{
									g.DrawRectangle(p, segRect);
								}
								if (sw >= 28)
								{
									using (Brush txtBrush = new SolidBrush(Color.FromArgb(254, 202, 202)))
									{
										g.DrawString("[已剔除]", segFont, txtBrush, segRect, sfCenter);
									}
								}
							}
						}
						else if (trk.Type == TrackType.Audio)
						{
							// Audio Track
							if (seg.IsKept)
							{
								if (trk.IsMuted || _cutEditAudioMuted)
								{
									using (HatchBrush hb = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(71, 85, 105), Color.FromArgb(20, 26, 38)))
									{
										g.FillRectangle(hb, segRect);
									}
								}
								else
								{
									using (LinearGradientBrush lgb = new LinearGradientBrush(segRect, Color.FromArgb(16, 120, 80), Color.FromArgb(6, 78, 54), LinearGradientMode.Vertical))
									{
										g.FillRectangle(lgb, segRect);
									}

									// Real Waveform
									if (!string.IsNullOrEmpty(seg.SourcePath) && _cutEditWaveformCache.TryGetValue(seg.SourcePath, out Image waveImg))
									{
										double mediaDur = GetMediaDuration(seg.SourcePath);
										if (mediaDur <= 0.0) mediaDur = seg.Duration;
										float srcX1 = (float)(seg.StartSeconds / Math.Max(0.1, mediaDur)) * waveImg.Width;
										float srcX2 = (float)(seg.EndSeconds / Math.Max(0.1, mediaDur)) * waveImg.Width;
										float srcW = Math.Max(1f, srcX2 - srcX1);
										RectangleF srcRect = new RectangleF(srcX1, 0, srcW, waveImg.Height);
										g.DrawImage(waveImg, segRect, srcRect, GraphicsUnit.Pixel);
									}
									else if (!string.IsNullOrEmpty(seg.SourcePath))
									{
										RequestRealAudioWaveform(seg.SourcePath, Color.FromArgb(34, 197, 94));
										int midY = segRect.Y + segRect.Height / 2;
										using (Pen pWave = new Pen(Color.FromArgb(52, 211, 153), 1f))
										{
											for (int wx = sx + 2; wx < sx + sw - 2; wx += 4)
											{
												int wh = ((wx * 5 + i * 11) % (segRect.Height / 2 - 2)) + 2;
												g.DrawLine(pWave, wx, midY - wh, wx, midY + wh);
											}
										}
									}
								}

								if (sw >= 28)
								{
									string linkPrefix = !string.IsNullOrEmpty(seg.LinkedPartnerId) ? "🔗 " : "";
									string audTitle = string.IsNullOrEmpty(seg.Title)
										? $"{linkPrefix}🎵 #{i + 1} {FormatDuration(seg.Duration)}"
										: $"{linkPrefix}🎵 #{i + 1} {seg.Title}";
									if (seg.VolumePercent != 100) audTitle += $" [{seg.VolumePercent}%]";
									using (Brush txtBrush = new SolidBrush(Color.FromArgb(220, 252, 231)))
									{
										g.DrawString(audTitle, segFont, txtBrush, segRect, sfCenter);
									}
								}

								Color borderColor = isSelected
									? Color.FromArgb(234, 179, 8)
									: (isLinkedPartnerSelected ? Color.FromArgb(56, 189, 248) : Color.FromArgb(52, 211, 153));
								float borderWidth = isSelected ? 2.5f : (isLinkedPartnerSelected ? 2f : 1f);

								using (Pen p = new Pen(borderColor, borderWidth))
								{
									if (isLinkedPartnerSelected) p.DashStyle = DashStyle.Dash;
									g.DrawRectangle(p, segRect);
								}
							}
							else
							{
								using (HatchBrush hb = new HatchBrush(HatchStyle.LightUpwardDiagonal, Color.FromArgb(220, 38, 38), Color.FromArgb(69, 10, 10)))
								{
									g.FillRectangle(hb, segRect);
								}
							}
						}
					}
				}

				// Draw Seam Transition Bridge Badges across adjacent cuts on this track
				if (trk.Type == TrackType.Video && _cutEditDuration > 0.0)
				{
					var trackSegs = _cutEditSegments.Where(s => s.TrackId == trk.Id).OrderBy(s => s.TimelineStartSeconds).ToList();
					for (int si = 0; si < trackSegs.Count - 1; si++)
					{
						var s1 = trackSegs[si];
						var s2 = trackSegs[si + 1];
						double s1End = s1.TimelineStartSeconds + s1.Duration;
						if (Math.Abs(s2.TimelineStartSeconds - s1End) <= 0.4)
						{
							int seamX = TimeToScreenX(s1End, bounds.Width);
							bool hasBridgeTrans = (!string.IsNullOrEmpty(s1.TransitionOutType) && s1.TransitionOutType != "none") ||
							                      (!string.IsNullOrEmpty(s2.TransitionInType) && s2.TransitionInType != "none");
							if (hasBridgeTrans)
							{
								string tType = (s1.TransitionOutType != "none" && !string.IsNullOrEmpty(s1.TransitionOutType)) ? s1.TransitionOutType : s2.TransitionInType;
								double tDur = Math.Max(s1.TransitionOutDuration, s2.TransitionInDuration);
								string tShort = GetTransitionName(tType);

								int bW = 68;
								int bH = 18;
								Rectangle bridgeRect = new Rectangle(seamX - bW / 2, curY + (th - bH) / 2, bW, bH);
								using (GraphicsPath bp = CreateRoundedRectanglePath(bridgeRect, 4))
								using (LinearGradientBrush bgBrush = new LinearGradientBrush(bridgeRect, Color.FromArgb(147, 51, 234), Color.FromArgb(109, 40, 217), LinearGradientMode.Vertical))
								using (Pen bPen = new Pen(Color.FromArgb(216, 180, 254), 1.5f))
								using (Brush bText = new SolidBrush(Color.White))
								{
									g.FillPath(bgBrush, bp);
									g.DrawPath(bPen, bp);
									g.DrawString($"⚡{tShort} {tDur:0.0}s", badgeFont, bText, bridgeRect, sfCenter);
								}
							}
							else
							{
								// Subtle cut seam notches
								using (Pen pSeam = new Pen(Color.FromArgb(250, 204, 21), 1.5f))
								{
									g.DrawLine(pSeam, seamX - 3, curY + 2, seamX + 3, curY + 2);
									g.DrawLine(pSeam, seamX, curY + 2, seamX, curY + 5);
									g.DrawLine(pSeam, seamX - 3, curY + th - 3, seamX + 3, curY + th - 3);
									g.DrawLine(pSeam, seamX, curY + th - 6, seamX, curY + th - 3);
								}
							}
						}
					}
				}

				// Subtitle & Overlay Track T1
				if (trk.Id == "T1")
				{
					_cachedOverlayRects.Clear();
					_cachedT1Rect = Rectangle.Empty;

					if (_cutEditDuration > 0.0 && _cutEditOverlays.Count > 0)
					{
						for (int oi = 0; oi < _cutEditOverlays.Count; oi++)
						{
							var olItem = _cutEditOverlays[oi];
							if (!olItem.Enabled) continue;

							double tStart = Math.Max(0.0, Math.Min(dur - 0.2, olItem.StartSeconds));
							double tDur = Math.Max(0.5, olItem.Duration);
							int tx1 = TimeToScreenX(tStart, bounds.Width);
							int tx2 = TimeToScreenX(tStart + tDur, bounds.Width);
							int tw = Math.Max(16, tx2 - tx1);
							Rectangle olRect = new Rectangle(tx1, curY + 2, tw, th - 4);
							_cachedOverlayRects[olItem.Id] = olRect;
							if (_selectedOverlay == olItem) _cachedT1Rect = olRect;

							bool isThisSel = (_selectedOverlay == olItem);
							Color cGrad1 = (olItem.Type == OverlayItemType.Image) ? Color.FromArgb(168, 85, 247) : Color.FromArgb(14, 165, 233);
							Color cGrad2 = (olItem.Type == OverlayItemType.Image) ? Color.FromArgb(126, 34, 206) : Color.FromArgb(2, 132, 199);
							Color cBorder = isThisSel ? Color.FromArgb(234, 179, 8) : ((olItem.Type == OverlayItemType.Image) ? Color.FromArgb(216, 180, 254) : Color.FromArgb(125, 211, 252));

							using (LinearGradientBrush lgb = new LinearGradientBrush(olRect, cGrad1, cGrad2, LinearGradientMode.Vertical))
							{
								g.FillRectangle(lgb, olRect);
							}
							using (Pen p = new Pen(cBorder, isThisSel ? 2.5f : 1f))
							{
								g.DrawRectangle(p, olRect);
							}

							using (Brush handleB = new SolidBrush(isThisSel ? Color.FromArgb(253, 224, 71) : Color.FromArgb(224, 242, 254)))
							{
								g.FillRectangle(handleB, tx1 + 1, curY + 4, 3, th - 8);
								g.FillRectangle(handleB, tx1 + tw - 4, curY + 4, 3, th - 8);
							}

							string label = olItem.GetDisplayName();
							using (Brush bTxt = new SolidBrush(Color.White))
							{
								g.DrawString(label, segFont, bTxt, olRect, sfCenter);
							}
						}
					}
				}

				// BGM Track A2 prompt when no segments exist on A2
				if (trk.Id == "A2" && !_cutEditSegments.Any(s => s.TrackId == "A2") && _cutEditDuration > 0.0)
				{
					int a2X = TimeToScreenX(0.0, bounds.Width);
					int a2W = TimeToScreenX(_cutEditDuration, bounds.Width) - a2X;
					Rectangle a2Rect = new Rectangle(a2X, curY + 2, a2W, th - 4);
					using (Pen p = new Pen(Color.FromArgb(60, 139, 92, 246), 1f) { DashStyle = DashStyle.Dash })
					{
						g.DrawRectangle(p, a2Rect);
					}
					string a2Text = $"♫ A2 配乐轨 (音量 {_cutEditBgmVolume}%) | 可从左侧媒体池直接拖入音频/BGM素材";
					using (Brush bA2 = new SolidBrush(Color.FromArgb(148, 163, 184)))
					{
						g.DrawString(a2Text, segFont, bA2, a2Rect, sfCenter);
					}
				}

				// If track is locked, paint hatched warning overlay across the lane
				if (trk.IsLocked)
				{
					using (HatchBrush lkHatch = new HatchBrush(HatchStyle.LightDownwardDiagonal, Color.FromArgb(90, 245, 158, 11), Color.FromArgb(25, 245, 158, 11)))
					{
						g.FillRectangle(lkHatch, laneClip);
					}
					using (Pen lkBorder = new Pen(Color.FromArgb(234, 179, 8), 1.5f))
					{
						g.DrawRectangle(lkBorder, laneClip);
					}
					using (Font fLk = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold))
					using (Brush bBg = new SolidBrush(Color.FromArgb(220, 30, 22, 0)))
					using (Brush bLk = new SolidBrush(Color.FromArgb(253, 224, 71)))
					{
						Rectangle lkBadge = new Rectangle(bounds.Width - 116, curY + (th - 22) / 2, 102, 22);
						g.FillRectangle(bBg, lkBadge);
						g.DrawRectangle(Pens.Goldenrod, lkBadge);
						g.DrawString("🔒 轨道已锁定", fLk, bLk, lkBadge, sfCenter);
					}
				}

				g.ResetClip();

				// Track Header background (painted on top of lanes so left side is always fixed!)
				using (Brush hBg = new SolidBrush(Color.FromArgb(18, 24, 34)))
				{
					g.FillRectangle(hBg, headerRect);
				}

				// If this track is selected, draw active indicator bar
				if (trk.Id == _cutEditSelectedTrackId)
				{
					using (Brush bSel = new SolidBrush(Color.FromArgb(14, 165, 233)))
					{
						g.FillRectangle(bSel, 0, curY, 3, th);
					}
				}

				// Draw Track Badge (Pill)
				Rectangle badgeRect = new Rectangle(4, curY + (th - 22) / 2, 38, 22);
				using (GraphicsPath bp = CreateRoundedRectanglePath(badgeRect, 4))
				using (Brush bBrush = new SolidBrush(trk.GetBadgeColor()))
				using (Brush wBrush = new SolidBrush(Color.White))
				{
					g.FillPath(bBrush, bp);
					g.DrawString(trk.Id, badgeFont, wBrush, badgeRect, sfCenter);
				}

				// Draw Lock Button icon
				Rectangle lockRect = new Rectangle(44, curY + (th - 20) / 2, 20, 20);
				string lockIcon = trk.IsLocked ? "🔒" : "🔓";
				Color lockColor = trk.IsLocked ? Color.FromArgb(245, 158, 11) : Color.FromArgb(100, 116, 139);
				if (trk.IsLocked)
				{
					using (Brush lkBg = new SolidBrush(Color.FromArgb(50, 245, 158, 11)))
					{
						g.FillRectangle(lkBg, lockRect);
					}
				}
				using (Brush lkBrush = new SolidBrush(lockColor))
				{
					g.DrawString(lockIcon, iconFont, lkBrush, lockRect, sfCenter);
				}

				// Draw Mute / Eye icon
				Rectangle muteRect = new Rectangle(68, curY + (th - 20) / 2, 20, 20);
				string actIcon = (trk.Type == TrackType.Audio)
					? (trk.IsMuted ? "🔇" : "🔊")
					: (trk.IsVisible ? "👁" : "🚫");
				Color actColor = (trk.Type == TrackType.Audio && trk.IsMuted) || (!trk.IsVisible)
					? Color.FromArgb(239, 68, 68)
					: Color.FromArgb(148, 163, 184);
				using (Brush actBrush = new SolidBrush(actColor))
				{
					g.DrawString(actIcon, iconFont, actBrush, muteRect, sfCenter);
				}

				curY += th + 2;
			}
		}

		// Vertical divider line between header and lanes
		using (Pen p = new Pen(Color.FromArgb(40, 50, 68), 1.5f))
		{
			g.DrawLine(p, headerW, 0, headerW, bounds.Height);
		}

		// Floating drag badge
		if (_timelineDragMode == TimelineDragMode.MoveClip && _dragSegmentIndex >= 0 && _dragSegmentIndex < _cutEditSegments.Count)
		{
			var dragSeg = _cutEditSegments[_dragSegmentIndex];
			string badgeText = $"⏱️ {FormatDuration(dragSeg.TimelineStartSeconds)} - {FormatDuration(dragSeg.TimelineStartSeconds + dragSeg.Duration)}  |  轨道: {dragSeg.TrackId}";
			using (Font fBadge = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold))
			{
				Size bSize = TextRenderer.MeasureText(badgeText, fBadge);
				int bx = Math.Max(headerW + 10, Math.Min(bounds.Width - bSize.Width - 20, _dragStartMousePoint.X - bSize.Width / 2));
				int by = Math.Max(28, _dragStartMousePoint.Y - 36);
				Rectangle bRect = new Rectangle(bx, by, bSize.Width + 16, bSize.Height + 6);
				using (Brush bBg = new SolidBrush(Color.FromArgb(235, 15, 23, 42)))
				using (Pen pBorder = new Pen(Color.FromArgb(234, 179, 8), 1.5f))
				using (Brush bTxt = new SolidBrush(Color.FromArgb(254, 240, 138)))
				using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
				{
					g.FillRectangle(bBg, bRect);
					g.DrawRectangle(pBorder, bRect);
					g.DrawString(badgeText, fBadge, bTxt, bRect, sf);
				}
			}
		}

		// 3. Cyan Magnetic Snapping guide line
		if (_snapGuideScreenX >= headerW + 1 && _snapGuideScreenX <= bounds.Width)
		{
			using (Pen pSnap = new Pen(Color.FromArgb(6, 182, 212), 2f) { DashStyle = DashStyle.Dash })
			{
				g.DrawLine(pSnap, _snapGuideScreenX, 0, _snapGuideScreenX, bounds.Height);
			}
			using (Font fSnap = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Bold))
			using (Brush bSnapBg = new SolidBrush(Color.FromArgb(220, 6, 182, 212)))
			using (Brush bSnapTxt = new SolidBrush(Color.Black))
			using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
			{
				Rectangle snapTag = new Rectangle(_snapGuideScreenX - 24, 4, 48, 16);
				g.FillRectangle(bSnapBg, snapTag);
				g.DrawString("🧲磁吸", fSnap, bSnapTxt, snapTag, sf);
			}
		}

		// 4. Red Razor Tool hover line
		if (_cutEditCurrentTool == TimelineToolMode.Razor && _razorHoverX >= headerW + 1 && _razorHoverX <= bounds.Width)
		{
			using (Pen pRazor = new Pen(Color.FromArgb(239, 68, 68), 1.5f) { DashStyle = DashStyle.Dash })
			{
				g.DrawLine(pRazor, _razorHoverX, 0, _razorHoverX, bounds.Height);
			}
			using (Font fCut = new Font("Segoe UI Emoji", 10f, FontStyle.Regular))
			using (Brush bCut = new SolidBrush(Color.FromArgb(239, 68, 68)))
			{
				g.DrawString("✂", fCut, bCut, _razorHoverX - 7, 2);
			}
		}

		// 5. Playhead (Red needle with top triangle head)
		if (_cutEditDuration > 0.0)
		{
			int px = TimeToScreenX(_cutEditCurrentPos, bounds.Width);
			if (px >= headerW + 1 && px <= bounds.Width)
			{
				using (Pen pHead = new Pen(Color.FromArgb(239, 68, 68), 2f))
				{
					g.DrawLine(pHead, px, 0, px, bounds.Height);
				}
				Point[] cursorPoints = new Point[]
				{
					new Point(px - 6, 0),
					new Point(px + 6, 0),
					new Point(px, 14)
				};
				using (Brush b = new SolidBrush(Color.FromArgb(239, 68, 68)))
				{
					g.FillPolygon(b, cursorPoints);
				}
			}
		}
	}

	private double GetMediaDuration(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0.0;
		try
		{
			if (IsImage(path)) return 5.0;
			string ffmpeg = FindFfmpeg();
			if (!string.IsNullOrEmpty(ffmpeg))
			{
				var info = Probe(ffmpeg, path);
				return info.DurationSeconds;
			}
		}
		catch { }
		return 0.0;
	}

	private void RequestRealAudioWaveform(string mediaPath, Color waveColor)
	{
		if (string.IsNullOrEmpty(mediaPath) || !File.Exists(mediaPath) || IsImage(mediaPath)) return;
		lock (_cutEditWaveformCache)
		{
			if (_cutEditWaveformCache.ContainsKey(mediaPath)) return;
		}
		lock (_cutEditWaveformsGenerating)
		{
			if (_cutEditWaveformsGenerating.Contains(mediaPath)) return;
			_cutEditWaveformsGenerating.Add(mediaPath);
		}

		Task.Run(() =>
		{
			try
			{
				string ffmpeg = FindFfmpeg();
				if (string.IsNullOrEmpty(ffmpeg) || !File.Exists(ffmpeg)) return;

				string tempWavePng = Path.Combine(Path.GetTempPath(), $"wave_{Guid.NewGuid():N}.png");
				string hexColor = $"0x{waveColor.R:X2}{waveColor.G:X2}{waveColor.B:X2}";
				string args = "-y -i " + QuoteArg(mediaPath) + " -filter_complex " + QuoteArg("aformat=channel_layouts=mono,showwavespic=s=1600x64:colors=" + hexColor) + " -frames:v 1 -update 1 " + QuoteArg(tempWavePng);

				ProcessStartInfo psi = new ProcessStartInfo(ffmpeg, args)
				{
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardError = true
				};
				using (var proc = Process.Start(psi))
				{
					proc.WaitForExit(6000);
				}

				if (File.Exists(tempWavePng))
				{
					using (var fs = new FileStream(tempWavePng, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					{
						using (var srcBmp = new Bitmap(fs))
						{
							var cachedBmp = new Bitmap(srcBmp);
							lock (_cutEditWaveformCache)
							{
								_cutEditWaveformCache[mediaPath] = cachedBmp;
							}
						}
					}
					TryDelete(tempWavePng);

					BeginInvoke((MethodInvoker)delegate
					{
						_cutEditTimelineCanvas?.Invalidate();
					});
				}
			}
			catch { }
			finally
			{
				lock (_cutEditWaveformsGenerating)
				{
					_cutEditWaveformsGenerating.Remove(mediaPath);
				}
			}
		});
	}

	internal void SwitchMediaPoolView(bool isThumbView)
	{
		_cutEditMediaPoolIsThumbView = isThumbView;
		if (_cutEditMediaPoolViewListBtn != null)
		{
			_cutEditMediaPoolViewListBtn.Tag = isThumbView ? "btn" : "accent";
			_cutEditMediaPoolViewListBtn.BackColor = isThumbView ? SurfaceColor : AccentColor;
			_cutEditMediaPoolViewListBtn.ForeColor = isThumbView ? InkColor : Color.White;
		}
		if (_cutEditMediaPoolViewThumbBtn != null)
		{
			_cutEditMediaPoolViewThumbBtn.Tag = isThumbView ? "accent" : "btn";
			_cutEditMediaPoolViewThumbBtn.BackColor = isThumbView ? AccentColor : SurfaceColor;
			_cutEditMediaPoolViewThumbBtn.ForeColor = isThumbView ? Color.White : InkColor;
		}
		if (_cutEditMediaPoolList != null)
		{
			_cutEditMediaPoolList.View = isThumbView ? View.LargeIcon : View.Details;
			RefreshMediaPoolList();
			if (isThumbView)
			{
				foreach (var p in _cutEditMediaPool)
				{
					GenerateMediaPoolThumbnailAsync(p);
				}
			}
			_cutEditMediaPoolList.Invalidate();
		}
	}

	internal void AddCutEditMediaPoolPaths(string[] paths)
	{
		if (paths == null || paths.Length == 0) return;
		List<string> toAdd = new List<string>();
		foreach (string p in paths)
		{
			if (Directory.Exists(p))
			{
				foreach (string ext in VideoExtensions)
				{
					toAdd.AddRange(Directory.GetFiles(p, "*" + ext, SearchOption.AllDirectories));
				}
				foreach (string ext in ImageExtensions)
				{
					toAdd.AddRange(Directory.GetFiles(p, "*" + ext, SearchOption.AllDirectories));
				}
				foreach (string ext in AudioExtensions)
				{
					toAdd.AddRange(Directory.GetFiles(p, "*" + ext, SearchOption.AllDirectories));
				}
			}
			else if (File.Exists(p))
			{
				string ext = Path.GetExtension(p);
				if (VideoExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)) ||
				    ImageExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)) ||
				    AudioExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
				{
					toAdd.Add(p);
				}
			}
		}

		bool added = false;
		foreach (string f in toAdd)
		{
			if (!_cutEditMediaPool.Contains(f, StringComparer.OrdinalIgnoreCase))
			{
				_cutEditMediaPool.Add(f);
				added = true;
				GenerateMediaPoolThumbnailAsync(f);
			}
		}
		if (added)
		{
			RefreshMediaPoolList();
		}
	}

	private void GenerateMediaPoolThumbnailAsync(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
		lock (_cutEditMediaPoolThumbCache)
		{
			if (_cutEditMediaPoolThumbCache.ContainsKey(path)) return;
		}
		lock (_cutEditMediaPoolThumbsGenerating)
		{
			if (_cutEditMediaPoolThumbsGenerating.Contains(path)) return;
			_cutEditMediaPoolThumbsGenerating.Add(path);
		}

		Task.Run(() =>
		{
			try
			{
				Bitmap thumbBmp = new Bitmap(96, 64);
				if (IsImage(path))
				{
					using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					using (var src = Image.FromStream(fs))
					using (Graphics g = Graphics.FromImage(thumbBmp))
					{
						g.Clear(Color.FromArgb(12, 16, 24));
						g.InterpolationMode = InterpolationMode.HighQualityBicubic;
						float scale = Math.Min(96f / src.Width, 64f / src.Height);
						int dw = (int)(src.Width * scale);
						int dh = (int)(src.Height * scale);
						g.DrawImage(src, (96 - dw) / 2, (64 - dh) / 2, dw, dh);
					}
				}
				else
				{
					string ffmpeg = FindFfmpeg();
					if (!string.IsNullOrEmpty(ffmpeg))
					{
						string tempJpg = Path.Combine(Path.GetTempPath(), $"thumb_{Guid.NewGuid():N}.jpg");
						string args = "-y -ss 00:00:01 -i " + QuoteArg(path) + " -vframes 1 -vf " + QuoteArg("scale=96:64:force_original_aspect_ratio=decrease,pad=96:64:(ow-iw)/2:(oh-ih)/2:black") + " -update 1 " + QuoteArg(tempJpg);
						ProcessStartInfo psi = new ProcessStartInfo(ffmpeg, args) { CreateNoWindow = true, UseShellExecute = false };
						using (var proc = Process.Start(psi)) proc.WaitForExit(4000);
						if (File.Exists(tempJpg))
						{
							using (var fs = new FileStream(tempJpg, FileMode.Open, FileAccess.Read))
							using (var loaded = new Bitmap(fs))
							using (Graphics g = Graphics.FromImage(thumbBmp))
							{
								g.DrawImage(loaded, 0, 0, 96, 64);
							}
							TryDelete(tempJpg);
						}
					}
				}

				lock (_cutEditMediaPoolThumbCache)
				{
					_cutEditMediaPoolThumbCache[path] = thumbBmp;
				}

				BeginInvoke((MethodInvoker)delegate
				{
					if (_cutEditMediaPoolImageList != null)
					{
						if (!_cutEditMediaPoolImageList.Images.ContainsKey(path))
						{
							_cutEditMediaPoolImageList.Images.Add(path, thumbBmp);
						}
					}
					if (_cutEditMediaPoolIsThumbView)
					{
						RefreshMediaPoolList();
					}
				});
			}
			catch { }
			finally
			{
				lock (_cutEditMediaPoolThumbsGenerating)
				{
					_cutEditMediaPoolThumbsGenerating.Remove(path);
				}
			}
		});
	}

	private void RefreshMediaPoolList()
	{
		if (_cutEditMediaPoolList == null) return;
		_cutEditMediaPoolList.BeginUpdate();
		_cutEditMediaPoolList.Items.Clear();
		string ffmpeg = FindFfmpeg();

		foreach (var path in _cutEditMediaPool)
		{
			string name = Path.GetFileName(path);
			string durStr = "--:--";
			string resStr = "---";
			try
			{
				if (IsImage(path))
				{
					durStr = "5.0s";
					using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					using (var img = Image.FromStream(fs))
					{
						resStr = $"{img.Width}x{img.Height}";
					}
				}
				else if (!string.IsNullOrEmpty(ffmpeg) && File.Exists(ffmpeg))
				{
					var info = Probe(ffmpeg, path);
					if (info.HasVideo)
					{
						durStr = FormatDuration(info.DurationSeconds);
						resStr = $"{info.Width}x{info.Height}";
					}
					else if (info.HasAudio)
					{
						durStr = FormatDuration(info.DurationSeconds);
						resStr = "音频(BGM)";
					}
				}
			}
			catch { }

			string extP = Path.GetExtension(path);
			bool isAud = AudioExtensions.Any(e => string.Equals(e, extP, StringComparison.OrdinalIgnoreCase));
			string iconPrefix = isAud ? "🎵 " : (IsImage(path) ? "🖼️ " : "🎬 ");

			ListViewItem lvi = new ListViewItem(iconPrefix + name);
			lvi.SubItems.Add(durStr);
			lvi.SubItems.Add(resStr);
			lvi.Tag = path;
			lvi.ImageKey = path;

			if (_cutEditMediaPoolIsThumbView)
			{
				lvi.Text = $"{iconPrefix}{name}\n[{durStr}]";
			}

			_cutEditMediaPoolList.Items.Add(lvi);
		}
		_cutEditMediaPoolList.EndUpdate();
	}

	private void InsertSelectedMediaPoolToTimeline(int mode)
	{
		string path = null;
		if (_cutEditMediaPoolList != null && _cutEditMediaPoolList.SelectedItems.Count > 0)
		{
			path = _cutEditMediaPoolList.SelectedItems[0].Tag as string;
		}
		else if (_cutEditMediaPool.Count > 0)
		{
			path = _cutEditMediaPool[0];
		}

		if (!string.IsNullOrEmpty(path) && File.Exists(path))
		{
			InsertMediaIntoTimeline(path, mode);
		}
	}

	private void InsertMediaIntoTimelineAtPoint(string path, Point canvasPt)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
		var targetTrack = GetTrackAtY(canvasPt.Y);
		if (targetTrack == null)
		{
			targetTrack = _cutEditTracks.FirstOrDefault(t => t.Id == "V1") ?? _cutEditTracks.FirstOrDefault();
		}
		if (targetTrack != null && targetTrack.IsLocked)
		{
			MessageBox.Show(this, $"轨道【{targetTrack.Name}】已被锁定，无法添加素材！请先解锁该轨道。", "轨道已锁定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		int headerW = 92;
		int padR = 14;
		int trackW = Math.Max(10, (int)((_cutEditTimelineCanvas.ClientSize.Width - headerW - padR) * _cutEditTimelineZoom));
		double dur = Math.Max(1.0, _cutEditDuration);

		double dropTime = 0.0;
		if (canvasPt.X > headerW && _cutEditDuration > 0.0)
		{
			dropTime = Math.Max(0.0, (double)(canvasPt.X - headerW) / trackW * _cutEditDuration);
			if (dropTime < 0.25) dropTime = 0.0;
		}
		else if (_cutEditDuration == 0.0)
		{
			dropTime = 0.0;
		}

		InsertMediaIntoTrackAtTime(path, targetTrack?.Id ?? "V1", dropTime);
	}

	internal void InsertMediaIntoTrackAtTime(string path, string targetTrackId, double targetTime)
	{
		try
		{
			var trk = _cutEditTracks.FirstOrDefault(t => t.Id == targetTrackId);
			if (trk != null && trk.IsLocked)
			{
				MessageBox.Show(this, $"轨道【{trk.Name}】已被锁定，无法添加素材！请先解锁该轨道。", "轨道已锁定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			double itemDur = 5.0;
			int w = 1920, h = 1080;
			string mediaType = "video";
			bool hasAudio = false;
			bool hasVideo = false;

			if (IsImage(path))
			{
				mediaType = "image";
				using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var img = Image.FromStream(fs))
				{
					w = img.Width;
					h = img.Height;
				}
			}
			else
			{
				string ffmpeg = FindFfmpeg();
				if (!string.IsNullOrEmpty(ffmpeg))
				{
					var info = Probe(ffmpeg, path);
					if (info.DurationSeconds > 0) itemDur = info.DurationSeconds;
					if (info.Width > 0) w = info.Width;
					if (info.Height > 0) h = info.Height;
					hasAudio = info.HasAudio;
					hasVideo = info.HasVideo;
					if (info.HasAudio && !info.HasVideo) mediaType = "audio";
					else if (info.HasVideo) mediaType = "video";
				}
			}

			string extP = Path.GetExtension(path);
			if (AudioExtensions.Any(e => string.Equals(e, extP, StringComparison.OrdinalIgnoreCase)))
			{
				mediaType = "audio";
			}

			if (mediaType == "audio")
			{
				if (string.IsNullOrEmpty(targetTrackId) || targetTrackId.StartsWith("V") || targetTrackId.StartsWith("T"))
				{
					targetTrackId = "A2";
				}
			}
			else if (string.IsNullOrEmpty(targetTrackId))
			{
				targetTrackId = "V1";
			}

			// Ripple shift: shift clips at or after targetTime forward by itemDur to prevent any truncation or overwrite
			if (_cutEditSegments.Count > 0)
			{
				foreach (var s in _cutEditSegments)
				{
					bool shouldShift = false;
					if (mediaType == "video" || mediaType == "image")
					{
						if (s.TrackId == targetTrackId || (targetTrackId == "V1" && s.TrackId == "A1"))
						{
							shouldShift = true;
						}
					}
					else if (mediaType == "audio")
					{
						if (s.TrackId == targetTrackId)
						{
							shouldShift = true;
						}
					}

					if (shouldShift && s.TimelineStartSeconds >= targetTime - 0.05)
					{
						s.TimelineStartSeconds += itemDur;
					}
				}
			}

			CutSegment newSeg = new CutSegment
			{
				Id = Guid.NewGuid().ToString("N"),
				SourcePath = path,
				StartSeconds = 0.0,
				EndSeconds = itemDur,
				TimelineStartSeconds = Math.Max(0.0, targetTime),
				IsKept = true,
				TrackId = targetTrackId,
				MediaType = mediaType,
				Title = Path.GetFileName(path)
			};
			_cutEditSegments.Add(newSeg);

			// Auto-separate audio into A1 track if video has audio and target is V1/V2
			if (mediaType == "video" && hasAudio && targetTrackId.StartsWith("V"))
			{
				CutSegment audioSeg = new CutSegment
				{
					Id = Guid.NewGuid().ToString("N"),
					SourcePath = path,
					StartSeconds = 0.0,
					EndSeconds = itemDur,
					TimelineStartSeconds = Math.Max(0.0, targetTime),
					IsKept = true,
					TrackId = "A1",
					MediaType = "audio",
					Title = Path.GetFileName(path) + " (音频)",
					VolumePercent = 100,
					LinkedPartnerId = newSeg.Id
				};
				newSeg.LinkedPartnerId = audioSeg.Id;
				_cutEditSegments.Add(audioSeg);
			}

			_cutEditSelectedSegmentIndex = _cutEditSegments.IndexOf(newSeg);
			_cutEditSelectedTrackId = newSeg.TrackId;

			if (string.IsNullOrEmpty(_cutEditSourcePath) || _cutEditSegments.Count <= 2)
			{
				_cutEditSourcePath = path;
				_cutEditWidth = w;
				_cutEditHeight = h;
				if (mediaType == "video" && _cutEditMediaElement != null)
				{
					try { _cutEditMediaElement.Source = new Uri(path); } catch { }
				}
			}

			if (_cutEditEmptyPlaceholder != null) _cutEditEmptyPlaceholder.Visible = false;
			if (_cutEditPreviewBox != null)
			{
				_cutEditPreviewBox.Visible = true;
				_cutEditPreviewBox.BringToFront();
			}

			RecalculateTimelineTotalDuration();

			if (mediaType == "video" || mediaType == "audio" || hasAudio)
			{
				RequestRealAudioWaveform(path, Color.FromArgb(34, 197, 94));
			}

			UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
			_cutEditTimelineCanvas?.Invalidate();
			UpdateDeliverSummary();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "加入轨道失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	internal void InsertMediaIntoTimeline(string path, int mode)
	{
		double targetTime = 0.0;
		if (mode == 0) // Start
		{
			targetTime = 0.0;
		}
		else if (mode == 1) // Playhead
		{
			targetTime = _cutEditCurrentPos;
		}
		else // 2: End
		{
			double maxEndOnTrack = _cutEditSegments
				.Where(s => s.TrackId == _cutEditSelectedTrackId && s.IsKept)
				.Select(s => s.TimelineStartSeconds + s.Duration)
				.DefaultIfEmpty(0.0)
				.Max();
			targetTime = (maxEndOnTrack > 0.0) ? maxEndOnTrack : _cutEditDuration;
		}

		InsertMediaIntoTrackAtTime(path, _cutEditSelectedTrackId ?? "V1", targetTime);
	}

	private void RecalculateTimelineTotalDuration()
	{
		double maxEnd = 0.0;
		int keptCount = 0;
		int excludedCount = 0;
		foreach (var s in _cutEditSegments)
		{
			if (s.IsKept)
			{
				keptCount++;
				double end = s.TimelineStartSeconds + s.Duration;
				if (end > maxEnd) maxEnd = end;
			}
			else
			{
				excludedCount++;
			}
		}
		_cutEditDuration = maxEnd;
		if (_cutEditEstimatedDurationLabel != null)
		{
			_cutEditEstimatedDurationLabel.Text = $"剪辑后总时长: {FormatDuration(_cutEditDuration)} (共 {_cutEditSegments.Count} 段，保留 {keptCount} 段，剔除 {excludedCount} 段)";
		}
		UpdateCutEditTimeLabel();
	}

	private void LoadMediaPoolSelectedToTimeline()
	{
		InsertSelectedMediaPoolToTimeline(2);
	}

	[System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
	[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
	private static extern bool GdiDeleteObject(IntPtr hObject);

	private static System.Windows.Media.Imaging.BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
	{
		if (bitmap == null) return null;
		IntPtr hBitmap = bitmap.GetHbitmap();
		try
		{
			return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
				hBitmap,
				IntPtr.Zero,
				System.Windows.Int32Rect.Empty,
				System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
		}
		finally
		{
			GdiDeleteObject(hBitmap);
		}
	}

	private bool GetActiveTransitionAtTime(double timeSec, out string transType, out double progress, out bool isTransIn)
	{
		transType = null;
		progress = 0.0;
		isTransIn = false;

		if (_cutEditSegments == null || _cutEditSegments.Count == 0) return false;

		var kept = _cutEditSegments.Where(s => s.IsKept && (s.MediaType == "video" || s.MediaType == "image" || s.TrackId.StartsWith("V"))).OrderBy(s => s.TimelineStartSeconds).ToList();
		if (kept.Count == 0) return false;

		// Check if timeSec is inside any segment's Transition In
		foreach (var s in kept)
		{
			if (!string.IsNullOrEmpty(s.TransitionInType) && s.TransitionInType != "none" && s.TransitionInDuration > 0.04)
			{
				double inStart = s.TimelineStartSeconds;
				double inEnd = s.TimelineStartSeconds + s.TransitionInDuration;
				if (timeSec >= inStart - 0.001 && timeSec <= inEnd)
				{
					transType = s.TransitionInType;
					progress = Math.Max(0.0, Math.Min(1.0, (timeSec - inStart) / s.TransitionInDuration));
					isTransIn = true;
					return true;
				}
			}
		}

		// Check if timeSec is inside any segment's Transition Out
		foreach (var s in kept)
		{
			if (!string.IsNullOrEmpty(s.TransitionOutType) && s.TransitionOutType != "none" && s.TransitionOutDuration > 0.04)
			{
				double outEnd = s.TimelineStartSeconds + s.Duration;
				double outStart = outEnd - s.TransitionOutDuration;
				if (timeSec >= outStart && timeSec <= outEnd + 0.001)
				{
					transType = s.TransitionOutType;
					progress = Math.Max(0.0, Math.Min(1.0, (timeSec - outStart) / s.TransitionOutDuration));
					isTransIn = false;
					return true;
				}
			}
		}

		return false;
	}

	private void UpdateWpfTransitionOverlay(System.Windows.Controls.Border border, double timeSec)
	{
		if (border == null) return;
		try
		{
			if (border.Dispatcher.CheckAccess())
			{
				ApplyWpfTransitionToBorder(border, timeSec);
			}
			else
			{
				border.Dispatcher.BeginInvoke(new Action(() =>
				{
					ApplyWpfTransitionToBorder(border, timeSec);
				}));
			}
		}
		catch { }
	}

	private void ApplyWpfTransitionToBorder(System.Windows.Controls.Border border, double timeSec)
	{
		if (!GetActiveTransitionAtTime(timeSec, out string transType, out double progress, out bool isTransIn))
		{
			border.Visibility = System.Windows.Visibility.Collapsed;
			border.Background = null;
			return;
		}

		border.Visibility = System.Windows.Visibility.Visible;

		if (transType == "fadewhite")
		{
			double alpha = isTransIn ? (1.0 - progress) : progress;
			byte a = (byte)Math.Max(0, Math.Min(255, (int)(alpha * 255)));
			border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, 255, 255, 255));
		}
		else if (transType == "fadeblack" || transType == "dissolve" || transType == "none" || transType == "fade")
		{
			double alpha = isTransIn ? (1.0 - progress) : progress;
			byte a = (byte)Math.Max(0, Math.Min(255, (int)(alpha * 255)));
			border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, 0, 0, 0));
		}
		else if (transType == "wipeleft" || transType == "slideleft")
		{
			double coverRatio = isTransIn ? (1.0 - progress) : progress;
			coverRatio = Math.Max(0.001, Math.Min(0.999, coverRatio));
			var lgb = new System.Windows.Media.LinearGradientBrush
			{
				StartPoint = new System.Windows.Point(0, 0.5),
				EndPoint = new System.Windows.Point(1, 0.5)
			};
			if (isTransIn)
			{
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, 0.0));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, coverRatio));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, coverRatio));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 1.0));
			}
			else
			{
				double split = 1.0 - coverRatio;
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 0.0));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, split));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, split));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, 1.0));
			}
			border.Background = lgb;
		}
		else if (transType == "wiperight" || transType == "slideright")
		{
			double coverRatio = isTransIn ? (1.0 - progress) : progress;
			coverRatio = Math.Max(0.001, Math.Min(0.999, coverRatio));
			var lgb = new System.Windows.Media.LinearGradientBrush
			{
				StartPoint = new System.Windows.Point(0, 0.5),
				EndPoint = new System.Windows.Point(1, 0.5)
			};
			if (isTransIn)
			{
				double split = 1.0 - coverRatio;
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 0.0));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, split));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, split));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, 1.0));
			}
			else
			{
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, 0.0));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, coverRatio));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, coverRatio));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 1.0));
			}
			border.Background = lgb;
		}
		else if (transType == "wipeup")
		{
			double coverRatio = isTransIn ? (1.0 - progress) : progress;
			coverRatio = Math.Max(0.001, Math.Min(0.999, coverRatio));
			var lgb = new System.Windows.Media.LinearGradientBrush
			{
				StartPoint = new System.Windows.Point(0.5, 0),
				EndPoint = new System.Windows.Point(0.5, 1)
			};
			if (isTransIn)
			{
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, 0.0));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, coverRatio));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, coverRatio));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 1.0));
			}
			else
			{
				double split = 1.0 - coverRatio;
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 0.0));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, split));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, split));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, 1.0));
			}
			border.Background = lgb;
		}
		else if (transType == "wipedown")
		{
			double coverRatio = isTransIn ? (1.0 - progress) : progress;
			coverRatio = Math.Max(0.001, Math.Min(0.999, coverRatio));
			var lgb = new System.Windows.Media.LinearGradientBrush
			{
				StartPoint = new System.Windows.Point(0.5, 0),
				EndPoint = new System.Windows.Point(0.5, 1)
			};
			if (isTransIn)
			{
				double split = 1.0 - coverRatio;
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 0.0));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, split));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, split));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, 1.0));
			}
			else
			{
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, 0.0));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, coverRatio));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, coverRatio));
				lgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 1.0));
			}
			border.Background = lgb;
		}
		else if (transType == "circleopen")
		{
			double openRatio = isTransIn ? progress : (1.0 - progress);
			openRatio = Math.Max(0.001, Math.Min(1.0, openRatio));
			var rgb = new System.Windows.Media.RadialGradientBrush
			{
				Center = new System.Windows.Point(0.5, 0.5),
				GradientOrigin = new System.Windows.Point(0.5, 0.5),
				RadiusX = openRatio,
				RadiusY = openRatio
			};
			rgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 0.0));
			rgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Transparent, 0.98));
			rgb.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Colors.Black, 1.0));
			border.Background = rgb;
		}
		else
		{
			double alpha = isTransIn ? (1.0 - progress) : progress;
			byte a = (byte)Math.Max(0, Math.Min(255, (int)(alpha * 255)));
			border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, 0, 0, 0));
		}
	}

	private void RenderTransitionOnBitmap(Bitmap bmp, double timeSec)
	{
		if (bmp == null || _cutEditSegments == null || _cutEditSegments.Count == 0) return;
		if (!GetActiveTransitionAtTime(timeSec, out string transType, out double progress, out bool isTransIn))
			return;

		int w = bmp.Width;
		int h = bmp.Height;

		using (Graphics g = Graphics.FromImage(bmp))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;

			if (transType == "fadewhite")
			{
				double alpha = isTransIn ? (1.0 - progress) : progress;
				int a = Math.Max(0, Math.Min(255, (int)(alpha * 255)));
				if (a > 0)
				{
					using (Brush b = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
					{
						g.FillRectangle(b, 0, 0, w, h);
					}
				}
			}
			else if (transType == "fadeblack" || transType == "dissolve" || transType == "none" || transType == "fade")
			{
				double alpha = isTransIn ? (1.0 - progress) : progress;
				int a = Math.Max(0, Math.Min(255, (int)(alpha * 255)));
				if (a > 0)
				{
					using (Brush b = new SolidBrush(Color.FromArgb(a, 0, 0, 0)))
					{
						g.FillRectangle(b, 0, 0, w, h);
					}
				}
			}
			else if (transType == "wipeleft" || transType == "slideleft")
			{
				double coverRatio = isTransIn ? (1.0 - progress) : progress;
				int wipeW = (int)(w * coverRatio);
				if (wipeW > 0)
				{
					Rectangle r = isTransIn ? new Rectangle(0, 0, wipeW, h) : new Rectangle(w - wipeW, 0, wipeW, h);
					using (Brush b = new SolidBrush(Color.Black))
					{
						g.FillRectangle(b, r);
					}
				}
			}
			else if (transType == "wiperight" || transType == "slideright")
			{
				double coverRatio = isTransIn ? (1.0 - progress) : progress;
				int wipeW = (int)(w * coverRatio);
				if (wipeW > 0)
				{
					Rectangle r = isTransIn ? new Rectangle(w - wipeW, 0, wipeW, h) : new Rectangle(0, 0, wipeW, h);
					using (Brush b = new SolidBrush(Color.Black))
					{
						g.FillRectangle(b, r);
					}
				}
			}
			else if (transType == "wipeup")
			{
				double coverRatio = isTransIn ? (1.0 - progress) : progress;
				int wipeH = (int)(h * coverRatio);
				if (wipeH > 0)
				{
					Rectangle r = isTransIn ? new Rectangle(0, 0, w, wipeH) : new Rectangle(0, h - wipeH, w, wipeH);
					using (Brush b = new SolidBrush(Color.Black))
					{
						g.FillRectangle(b, r);
					}
				}
			}
			else if (transType == "wipedown")
			{
				double coverRatio = isTransIn ? (1.0 - progress) : progress;
				int wipeH = (int)(h * coverRatio);
				if (wipeH > 0)
				{
					Rectangle r = isTransIn ? new Rectangle(0, h - wipeH, w, wipeH) : new Rectangle(0, 0, w, wipeH);
					using (Brush b = new SolidBrush(Color.Black))
					{
						g.FillRectangle(b, r);
					}
				}
			}
			else if (transType == "circleopen")
			{
				double openRatio = isTransIn ? progress : (1.0 - progress);
				double maxRadius = Math.Sqrt(w * w + h * h) / 2.0;
				float radius = (float)(maxRadius * openRatio);
				using (GraphicsPath path = new GraphicsPath())
				{
					path.AddRectangle(new Rectangle(0, 0, w, h));
					if (radius > 1)
					{
						path.AddEllipse(w / 2f - radius, h / 2f - radius, radius * 2f, radius * 2f);
					}
					using (Brush b = new SolidBrush(Color.Black))
					{
						g.FillPath(b, path);
					}
				}
			}
			else
			{
				double alpha = isTransIn ? (1.0 - progress) : progress;
				int a = Math.Max(0, Math.Min(255, (int)(alpha * 255)));
				if (a > 0)
				{
					using (Brush b = new SolidBrush(Color.FromArgb(a, 0, 0, 0)))
					{
						g.FillRectangle(b, 0, 0, w, h);
					}
				}
			}
		}
	}

	private void UpdateCutEditWpfOverlay(double timeSec, bool forcePreviewSelected = false)
	{
		UpdateWpfTransitionOverlay(_cutEditWpfTransitionBorder, timeSec);
		if (_cutEditWpfOverlayImage == null) return;
		try
		{
			bool hasActive = false;
			if (forcePreviewSelected && _selectedOverlay != null && _selectedOverlay.Enabled)
			{
				hasActive = true;
			}
			else
			{
				foreach (var o in _cutEditOverlays)
				{
					if (o.Enabled && timeSec >= o.StartSeconds && timeSec <= o.StartSeconds + o.Duration)
					{
						hasActive = true;
						break;
					}
				}
			}

			if (!hasActive)
			{
				if (_cutEditWpfOverlayImage.Source != null)
				{
					_cutEditWpfOverlayImage.Dispatcher.BeginInvoke(new Action(() =>
					{
						_cutEditWpfOverlayImage.Source = null;
					}));
				}
				return;
			}

			int w = _cutEditWidth > 0 ? _cutEditWidth : 1920;
			int h = _cutEditHeight > 0 ? _cutEditHeight : 1080;
			using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
			{
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.Clear(Color.Transparent);
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;

					if (forcePreviewSelected && _selectedOverlay != null)
					{
						if (_selectedOverlay.Enabled)
						{
							if (_selectedOverlay.Type == OverlayItemType.Text)
								RenderSingleTextOverlay(g, _selectedOverlay, w, h);
							else if (_selectedOverlay.Type == OverlayItemType.Image)
								RenderSingleImageOverlay(g, _selectedOverlay, w, h);
						}
					}
					else
					{
						foreach (var item in _cutEditOverlays)
						{
							if (!item.Enabled) continue;
							if (timeSec < item.StartSeconds || timeSec > (item.StartSeconds + item.Duration)) continue;

							if (item.Type == OverlayItemType.Text)
								RenderSingleTextOverlay(g, item, w, h);
							else if (item.Type == OverlayItemType.Image)
								RenderSingleImageOverlay(g, item, w, h);
						}
					}
				}

				var bmpSrc = ConvertBitmapToBitmapSource(bmp);
				bmpSrc?.Freeze();
				_cutEditWpfOverlayImage.Dispatcher.BeginInvoke(new Action(() =>
				{
					_cutEditWpfOverlayImage.Source = bmpSrc;
				}));
			}
		}
		catch { }
	}

	private void UpdateDeliverWpfOverlay(double timeSec)
	{
		UpdateWpfTransitionOverlay(_deliverWpfTransitionBorder, timeSec);
		if (_deliverWpfOverlayImage == null) return;
		try
		{
			bool hasActive = false;
			foreach (var o in _cutEditOverlays)
			{
				if (o.Enabled && timeSec >= o.StartSeconds && timeSec <= o.StartSeconds + o.Duration)
				{
					hasActive = true;
					break;
				}
			}

			if (!hasActive)
			{
				if (_deliverWpfOverlayImage.Source != null)
				{
					_deliverWpfOverlayImage.Dispatcher.BeginInvoke(new Action(() =>
					{
						_deliverWpfOverlayImage.Source = null;
					}));
				}
				return;
			}

			int w = _cutEditWidth > 0 ? _cutEditWidth : 1920;
			int h = _cutEditHeight > 0 ? _cutEditHeight : 1080;
			using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
			{
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.Clear(Color.Transparent);
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;

					foreach (var item in _cutEditOverlays)
					{
						if (!item.Enabled) continue;
						if (timeSec < item.StartSeconds || timeSec > (item.StartSeconds + item.Duration)) continue;

						if (item.Type == OverlayItemType.Text)
							RenderSingleTextOverlay(g, item, w, h);
						else if (item.Type == OverlayItemType.Image)
							RenderSingleImageOverlay(g, item, w, h);
					}
				}

				var bmpSrc = ConvertBitmapToBitmapSource(bmp);
				bmpSrc?.Freeze();
				_deliverWpfOverlayImage.Dispatcher.BeginInvoke(new Action(() =>
				{
					_deliverWpfOverlayImage.Source = bmpSrc;
				}));
			}
		}
		catch { }
	}

	internal void LoadVideoIntoCutEditor(string path, bool clearExisting = false)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

		if (_cutEditSegments.Count > 0)
		{
			if (clearExisting)
			{
				ClearCutEditorProject(suppressPrompt: true);
			}
			else
			{
				var dr = MessageBox.Show(this,
					"剪辑工作台当前已有正在编辑的视频内容。\n\n是否清空当前轨道以载入全新的视频？\n\n• 点击【是 (Y)】：清空当前轨道并从头载入新视频（推荐）\n• 点击【否 (N)】：在当前轨道末尾追加此视频\n• 点击【取消】：放弃载入",
					"载入视频到剪辑工作台",
					MessageBoxButtons.YesNoCancel,
					MessageBoxIcon.Question);

				if (dr == DialogResult.Cancel) return;
				if (dr == DialogResult.Yes)
				{
					ClearCutEditorProject(suppressPrompt: true);
				}
			}
		}

		InsertMediaIntoTrackAtTime(path, "V1", 0.0);
		_cutEditCurrentPos = 0.0;
		SeekCutEditVideo(0.0);
	}

	internal void ClearCutEditorProject(bool suppressPrompt = false)
	{
		if (!suppressPrompt && _cutEditSegments.Count > 0)
		{
			if (MessageBox.Show(this, "确定要清空剪辑工作台的所有轨道和内容吗？\n当前未保存的剪辑进度将被重置。", "清空剪辑轨道", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
			{
				return;
			}
		}

		try
		{
			if (_cutEditIsPlaying)
			{
				ToggleCutEditPlayPause();
			}
			if (_cutEditMediaElement != null)
			{
				_cutEditMediaElement.Pause();
				_cutEditMediaElement.Source = null;
			}

			_cutEditSegments.Clear();
			_cutEditOverlays.Clear();
			_cutEditUndoStack.Clear();
			_cutEditRedoStack.Clear();
			_cutEditWaveformCache.Clear();
			_cutEditSourcePath = null;
			_cutEditCurrentProjectPath = null;
			_cutEditDuration = 0.0;
			_cutEditCurrentPos = 0.0;
			_lastActiveSegment = null;

			InitDefaultOverlays();

			if (_cutEditWpfOverlayImage != null)
			{
				_cutEditWpfOverlayImage.Dispatcher.BeginInvoke(new Action(() =>
				{
					_cutEditWpfOverlayImage.Source = null;
				}));
			}

			if (_cutEditEmptyPlaceholder != null) _cutEditEmptyPlaceholder.Visible = true;
			if (_cutEditPreviewBox != null) _cutEditPreviewBox.Image = null;

			RecalculateTimelineTotalDuration();
			RefreshCutEditSegmentList();
			RefreshOverlayCombo();
			UpdateUndoRedoButtons();
			UpdateDeliverSummary();
			_cutEditTimelineCanvas?.Invalidate();
		}
		catch { }
	}

	private void UpdateCutEditTimeLabel()
	{
		if (_cutEditTimeLabel != null)
		{
			_cutEditTimeLabel.Text = $"{FormatDuration(_cutEditCurrentPos)} / {FormatDuration(_cutEditDuration)}";
		}
	}

	private void RefreshCutEditSegmentList()
	{
		RecalculateTimelineTotalDuration();
		_cutEditTimelineCanvas?.Invalidate();
		UpdateDeliverSummary();
	}

	private void SplitCutEditCurrentPosition()
	{
		if (_cutEditDuration <= 0.0 || _cutEditSegments.Count == 0) return;
		double pos = _cutEditCurrentPos;

		CutSegment targetSeg = null;

		if (_cutEditSelectedSegmentIndex >= 0 && _cutEditSelectedSegmentIndex < _cutEditSegments.Count)
		{
			var s = _cutEditSegments[_cutEditSelectedSegmentIndex];
			if (pos > s.TimelineStartSeconds + 0.05 && pos < s.TimelineStartSeconds + s.Duration - 0.05)
			{
				targetSeg = s;
			}
		}

		if (targetSeg == null)
		{
			for (int i = 0; i < _cutEditSegments.Count; i++)
			{
				var s = _cutEditSegments[i];
				if (s.TrackId == _cutEditSelectedTrackId && pos > s.TimelineStartSeconds + 0.05 && pos < s.TimelineStartSeconds + s.Duration - 0.05)
				{
					targetSeg = s;
					break;
				}
			}
		}

		if (targetSeg == null)
		{
			for (int i = 0; i < _cutEditSegments.Count; i++)
			{
				var s = _cutEditSegments[i];
				if (pos > s.TimelineStartSeconds + 0.05 && pos < s.TimelineStartSeconds + s.Duration - 0.05)
				{
					targetSeg = s;
					break;
				}
			}
		}

		if (targetSeg != null)
		{
			SplitSegmentAtTime(targetSeg, pos);
		}
	}

	private void SetCutEditInPoint()
	{
		if (_cutEditDuration <= 0.0) return;
		double pos = _cutEditCurrentPos;
		SplitCutEditCurrentPosition();
		foreach (var seg in _cutEditSegments)
		{
			double segEnd = seg.TimelineStartSeconds + seg.Duration;
			if (segEnd <= pos + 0.05)
			{
				var trk = _cutEditTracks.FirstOrDefault(t => t.Id == seg.TrackId);
				if (trk?.IsLocked != true)
				{
					seg.IsKept = false;
				}
			}
		}
		RefreshCutEditSegmentList();
	}

	private void SetCutEditOutPoint()
	{
		if (_cutEditDuration <= 0.0) return;
		double pos = _cutEditCurrentPos;
		SplitCutEditCurrentPosition();
		foreach (var seg in _cutEditSegments)
		{
			if (seg.TimelineStartSeconds >= pos - 0.05)
			{
				var trk = _cutEditTracks.FirstOrDefault(t => t.Id == seg.TrackId);
				if (trk?.IsLocked != true)
				{
					seg.IsKept = false;
				}
			}
		}
		RefreshCutEditSegmentList();
	}

	private void ToggleSelectedSegmentKept()
	{
		if (_cutEditSelectedSegmentIndex >= 0 && _cutEditSelectedSegmentIndex < _cutEditSegments.Count)
		{
			var seg = _cutEditSegments[_cutEditSelectedSegmentIndex];
			var trk = _cutEditTracks.FirstOrDefault(t => t.Id == seg.TrackId);
			if (trk?.IsLocked == true)
			{
				MessageBox.Show(this, $"轨道 {trk.Name} 已锁定，无法修改或剔除此分段！", "轨道已锁定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			seg.IsKept = !seg.IsKept;
			RefreshCutEditSegmentList();
		}
		else if (_cutEditSegments.Count > 0)
		{
			double pos = _cutEditCurrentPos;
			foreach (var seg in _cutEditSegments)
			{
				double sEnd = seg.TimelineStartSeconds + seg.Duration;
				if (pos >= seg.TimelineStartSeconds && pos <= sEnd)
				{
					var trk = _cutEditTracks.FirstOrDefault(t => t.Id == seg.TrackId);
					if (trk?.IsLocked == true)
					{
						MessageBox.Show(this, $"轨道 {trk.Name} 已锁定，无法修改此分段！", "轨道已锁定", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						return;
					}
					seg.IsKept = !seg.IsKept;
					RefreshCutEditSegmentList();
					break;
				}
			}
		}
	}

	private void ResetCutEditSegments()
	{
		foreach (var seg in _cutEditSegments)
		{
			var trk = _cutEditTracks.FirstOrDefault(t => t.Id == seg.TrackId);
			if (trk?.IsLocked != true)
			{
				seg.IsKept = true;
			}
		}
		RefreshCutEditSegmentList();
	}

	private void PlayCutEditVideoWithSound()
	{
		ToggleCutEditPlayPause();
	}

	internal void ToggleCutEditPlayPause()
	{
		if (_cutEditSegments.Count == 0 || _cutEditDuration <= 0.0)
		{
			MessageBox.Show(this, "请先在左侧媒体池添加或拖入素材到轨道！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		if (_cutEditIsPlaying)
		{
			if (_cutEditMediaElement != null)
			{
				try { _cutEditMediaElement.Pause(); } catch { }
			}
			_cutEditIsPlaying = false;
			_cutEditPlaybackSw.Stop();
			if (_cutEditPlayPauseButton != null) _cutEditPlayPauseButton.Text = "▶ 播放 (空格)";
			_cutEditPlayTimer?.Stop();

			if (_cutEditElementHost != null)
			{
				_cutEditElementHost.Visible = false;
			}
			if (_cutEditPreviewBox != null)
			{
				_cutEditPreviewBox.Visible = true;
				_cutEditPreviewBox.BringToFront();
			}
			if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed && _cutEditPopoutForm.PreviewBox != null)
			{
				_cutEditPopoutForm.PreviewBox.Visible = true;
				_cutEditPopoutForm.PreviewBox.BringToFront();
			}
			UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: _cutEditRealtimePreviewCheckBox?.Checked == true);
		}
		else
		{
			if (_cutEditEmptyPlaceholder != null) _cutEditEmptyPlaceholder.Visible = false;
			if (_cutEditCurrentPos >= _cutEditDuration - 0.05)
			{
				_cutEditCurrentPos = 0.0;
			}
			_cutEditIsPlaying = true;
			_cutEditPlaybackStartPos = _cutEditCurrentPos;
			_cutEditPlaybackSw.Restart();
			if (_cutEditPlayPauseButton != null) _cutEditPlayPauseButton.Text = "⏸ 暂停 (空格)";
			SyncPlayerAtCurrentPos(forceReload: false);
			_cutEditPlayTimer?.Start();
		}

		if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed)
		{
			_cutEditPopoutForm.UpdateTimeAndScrubber(_cutEditCurrentPos, _cutEditDuration, _cutEditIsPlaying);
		}
	}

	private CutSegment GetActiveVideoSegmentAtTime(double timeSec)
	{
		var videoTracks = _cutEditTracks
			.Where(t => t.Type == TrackType.Video && t.IsVisible)
			.OrderByDescending(t => t.Id)
			.ToList();

		foreach (var trk in videoTracks)
		{
			var seg = _cutEditSegments.FirstOrDefault(s => s.TrackId == trk.Id && s.IsKept &&
				timeSec >= s.TimelineStartSeconds && timeSec < s.TimelineStartSeconds + s.Duration);
			if (seg != null) return seg;
		}

		return _cutEditSegments.FirstOrDefault(s => s.IsKept &&
			timeSec >= s.TimelineStartSeconds && timeSec < s.TimelineStartSeconds + s.Duration);
	}

	private void SyncPlayerAtCurrentPos(bool forceReload)
	{
		if (_cutEditEmptyPlaceholder != null && _cutEditEmptyPlaceholder.Visible)
		{
			_cutEditEmptyPlaceholder.Visible = false;
		}

		var activeSeg = GetActiveVideoSegmentAtTime(_cutEditCurrentPos);
		_lastActiveSegment = activeSeg;

		if (activeSeg == null)
		{
			if (_cutEditMediaElement != null)
			{
				try { _cutEditMediaElement.Pause(); } catch { }
			}
			_cutEditElementHost.Visible = false;
			_cutEditPreviewBox.Visible = true;
			_cutEditPreviewBox.BringToFront();
			RenderTimelineGapPreview();
			return;
		}

		if (activeSeg.MediaType == "image" || IsImage(activeSeg.SourcePath))
		{
			if (_cutEditMediaElement != null)
			{
				try { _cutEditMediaElement.Pause(); } catch { }
			}
			_cutEditElementHost.Visible = false;
			_cutEditPreviewBox.Visible = true;
			_cutEditPreviewBox.BringToFront();
			RenderImageClipPreview(activeSeg);
			return;
		}

		// Video clip
		if (_cutEditMediaElement != null)
		{
			try
			{
				string fullPath = Path.GetFullPath(activeSeg.SourcePath);
				Uri targetUri = new Uri(fullPath);
				bool needNewSource = (_cutEditMediaElement.Source == null || !_cutEditMediaElement.Source.Equals(targetUri));
				if (needNewSource || forceReload)
				{
					_cutEditMediaElement.Source = targetUri;
				}
				double inSourceSec = activeSeg.StartSeconds + Math.Max(0.0, _cutEditCurrentPos - activeSeg.TimelineStartSeconds);
				TimeSpan targetPos = TimeSpan.FromSeconds(Math.Max(0, inSourceSec));
				if (Math.Abs((_cutEditMediaElement.Position - targetPos).TotalSeconds) > 0.35 || forceReload || needNewSource)
				{
					_cutEditMediaElement.Position = targetPos;
				}
				_cutEditMediaElement.Volume = _cutEditAudioMuted ? 0.0 : ((_cutEditVolumeTrackBar?.Value ?? 100) / 100.0);
				_cutEditMediaElement.IsMuted = _cutEditAudioMuted;

				if (_cutEditIsPlaying)
				{
					if (_cutEditPreviewBox != null) _cutEditPreviewBox.Visible = false;
					if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed && _cutEditPopoutForm.PreviewBox != null)
					{
						_cutEditPopoutForm.PreviewBox.Visible = false;
					}
					_cutEditElementHost.Visible = true;
					_cutEditElementHost.BringToFront();
					_cutEditMediaElement.Play();
				}
				else
				{
					_cutEditElementHost.Visible = false;
					if (_cutEditPreviewBox != null)
					{
						_cutEditPreviewBox.Visible = true;
						_cutEditPreviewBox.BringToFront();
					}
					if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed && _cutEditPopoutForm.PreviewBox != null)
					{
						_cutEditPopoutForm.PreviewBox.Visible = true;
						_cutEditPopoutForm.PreviewBox.BringToFront();
					}
					_cutEditMediaElement.Pause();
					UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: _cutEditRealtimePreviewCheckBox?.Checked == true);
				}
				UpdateCutEditWpfOverlay(_cutEditCurrentPos, forcePreviewSelected: false);
			}
			catch
			{
				_cutEditElementHost.Visible = false;
				_cutEditPreviewBox.Visible = true;
				_cutEditPreviewBox.BringToFront();
				UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: _cutEditRealtimePreviewCheckBox?.Checked == true);
			}
		}
	}

	private void RenderTimelineGapPreview()
	{
		int w = _cutEditPreviewBox.Width > 0 ? _cutEditPreviewBox.Width : 640;
		int h = _cutEditPreviewBox.Height > 0 ? _cutEditPreviewBox.Height : 360;
		Bitmap bmp = new Bitmap(w, h);
		using (Graphics g = Graphics.FromImage(bmp))
		{
			g.Clear(Color.FromArgb(12, 16, 24));
			using (Font f = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular))
			using (Brush b = new SolidBrush(Color.FromArgb(100, 116, 139)))
			using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
			{
				g.DrawString("〔 时间轴空隙 / 黑场 〕", f, b, new Rectangle(0, 0, w, h), sf);
			}
		}
		var old = _cutEditPreviewBox.Image;
		_cutEditPreviewBox.Image = bmp;
		old?.Dispose();
		if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed)
		{
			_cutEditPopoutForm.UpdateFrame(bmp, _cutEditCurrentPos, _cutEditDuration, _cutEditIsPlaying);
		}
	}

	private void RenderImageClipPreview(CutSegment seg)
	{
		try
		{
			if (File.Exists(seg.SourcePath))
			{
				using (var fs = new FileStream(seg.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var src = Image.FromStream(fs))
				{
					Bitmap bmp = new Bitmap(src);
					if (_cutEditRealtimePreviewCheckBox?.Checked == true || _cutEditTitleEnabled?.Checked == true)
					{
						RenderTitleOverlayOnBitmap(bmp);
					}
					RenderTransitionOnBitmap(bmp, _cutEditCurrentPos);
					var old = _cutEditPreviewBox.Image;
					_cutEditPreviewBox.Image = bmp;
					old?.Dispose();
					if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed)
					{
						_cutEditPopoutForm.UpdateFrame(bmp, _cutEditCurrentPos, _cutEditDuration, _cutEditIsPlaying);
					}
					return;
				}
			}
		}
		catch { }
		RenderTimelineGapPreview();
	}

	internal void SeekCutEditVideo(double targetSec)
	{
		if (_cutEditDuration <= 0.0) return;
		_cutEditCurrentPos = Math.Max(0.0, Math.Min(_cutEditDuration, targetSec));
		if (_cutEditIsPlaying)
		{
			_cutEditPlaybackStartPos = _cutEditCurrentPos;
			_cutEditPlaybackSw.Restart();
		}
		UpdateCutEditTimeLabel();
		SyncPlayerAtCurrentPos(forceReload: false);
		_cutEditTimelineCanvas?.Invalidate();
		if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed)
		{
			_cutEditPopoutForm.UpdateTimeAndScrubber(_cutEditCurrentPos, _cutEditDuration, _cutEditIsPlaying);
		}
	}

	internal void StepCutEditTime(double deltaSec)
	{
		if (_cutEditDuration <= 0.0) return;
		double newPos = Math.Max(0.0, Math.Min(_cutEditDuration, _cutEditCurrentPos + deltaSec));
		SeekCutEditVideo(newPos);
		if (_cutEditTimeScrubber != null)
		{
			_cutEditTimeScrubber.Value = (int)Math.Max(0, Math.Min(1000, (_cutEditCurrentPos / _cutEditDuration) * 1000.0));
		}
	}

	internal void SetCutEditSubtitleBottomOffset(int val)
	{
		_cutEditSubtitleBottomOffset = Math.Max(20, Math.Min(800, val));
		if (_cutEditSubtitlePosTrackBar != null && _cutEditSubtitlePosTrackBar.Value != _cutEditSubtitleBottomOffset)
		{
			_cutEditSubtitlePosTrackBar.Value = _cutEditSubtitleBottomOffset;
		}
		if (_cutEditSubtitlePosLabel != null)
		{
			_cutEditSubtitlePosLabel.Text = $"高度 距离底部: {_cutEditSubtitleBottomOffset} px";
		}
		if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed)
		{
			_cutEditPopoutForm.SyncSubtitleOffset(_cutEditSubtitleBottomOffset);
		}
		if (_cutEditRealtimePreviewCheckBox?.Checked == true)
		{
			UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
		}
	}

	private void AttachCutEditPlayerToPopout()
	{
		if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed && _cutEditElementHost != null)
		{
			_cutEditElementHost.Parent = _cutEditPopoutForm.CanvasPanel;
			_cutEditElementHost.Dock = DockStyle.Fill;
			if (_cutEditIsPlaying)
			{
				_cutEditElementHost.Visible = true;
				_cutEditElementHost.BringToFront();
			}
			else
			{
				_cutEditElementHost.Visible = false;
				if (_cutEditPopoutForm.PreviewBox != null)
				{
					_cutEditPopoutForm.PreviewBox.Visible = true;
					_cutEditPopoutForm.PreviewBox.BringToFront();
				}
			}
		}
	}

	internal void OnCutEditPopoutClosing()
	{
		DetachCutEditPlayerFromPopout();
	}

	internal void OnCutEditPopoutClosed()
	{
		DetachCutEditPlayerFromPopout();
		_cutEditPopoutForm = null;
	}

	private void DetachCutEditPlayerFromPopout()
	{
		if (_cutEditElementHost != null && _cutEditVideoHostPanel != null)
		{
			_cutEditElementHost.Parent = _cutEditVideoHostPanel;
			_cutEditElementHost.Dock = DockStyle.Fill;
			if (_cutEditIsPlaying)
			{
				_cutEditElementHost.Visible = true;
				_cutEditElementHost.BringToFront();
			}
			else
			{
				_cutEditElementHost.Visible = false;
				if (_cutEditPreviewBox != null)
				{
					_cutEditPreviewBox.Visible = true;
					_cutEditPreviewBox.BringToFront();
				}
			}
		}
	}

	private void OpenCutEditPopoutPreview()
	{
		if (_cutEditSegments.Count == 0 || _cutEditDuration <= 0.0)
		{
			MessageBox.Show(this, "请先在左侧媒体池或点击上方导入需要剪辑的视频素材！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		if (_cutEditPopoutForm == null || _cutEditPopoutForm.IsDisposed)
		{
			_cutEditPopoutForm = new CutEditPopoutPreviewForm(this);
			_cutEditPopoutForm.Show(this);
		}
		else
		{
			_cutEditPopoutForm.BringToFront();
		}

		AttachCutEditPlayerToPopout();

		if (_cutEditPreviewBox?.Image != null)
		{
			_cutEditPopoutForm.UpdateFrame(_cutEditPreviewBox.Image, _cutEditCurrentPos, _cutEditDuration, _cutEditIsPlaying);
		}
		else
		{
			_cutEditPopoutForm.UpdateTimeAndScrubber(_cutEditCurrentPos, _cutEditDuration, _cutEditIsPlaying);
		}
	}

	internal double GetDeliverTotalDuration() => _deliverDuration;

	internal void SeekDeliverPreview(double targetSec)
	{
		if (_deliverDuration <= 0.05) return;
		_deliverCurrentPos = Math.Max(0.0, Math.Min(_deliverDuration, targetSec));
		UpdateDeliverTimeLabel();
		if (!_deliverIsPlaying)
		{
			UpdateDeliverPreviewFrame(_deliverCurrentPos);
		}
		else
		{
			_deliverPlaybackStartPos = _deliverCurrentPos;
			_deliverPlaybackSw.Restart();
			SyncDeliverPlaybackSegment();
		}
	}

	internal void StepDeliverPreview(double deltaSec)
	{
		SeekDeliverPreview(_deliverCurrentPos + deltaSec);
	}

	private void AttachDeliverPlayerToPopout()
	{
		if (_deliverPopoutForm != null && !_deliverPopoutForm.IsDisposed && _deliverElementHost != null)
		{
			_deliverElementHost.Parent = _deliverPopoutForm.CanvasPanel;
			_deliverElementHost.Dock = DockStyle.Fill;
			if (_deliverIsPlaying)
			{
				_deliverElementHost.Visible = true;
				_deliverElementHost.BringToFront();
			}
			else
			{
				_deliverElementHost.Visible = false;
				if (_deliverPopoutForm.PreviewBox != null)
				{
					_deliverPopoutForm.PreviewBox.Visible = true;
					_deliverPopoutForm.PreviewBox.BringToFront();
				}
			}
		}
	}

	internal void OnDeliverPopoutClosing()
	{
		DetachDeliverPlayerFromPopout();
	}

	internal void OnDeliverPopoutClosed()
	{
		DetachDeliverPlayerFromPopout();
		_deliverPopoutForm = null;
	}

	private void DetachDeliverPlayerFromPopout()
	{
		if (_deliverElementHost != null && _deliverMonitorBox != null)
		{
			_deliverElementHost.Parent = _deliverMonitorBox;
			_deliverElementHost.Dock = DockStyle.Fill;
			if (_deliverIsPlaying)
			{
				_deliverElementHost.Visible = true;
				_deliverElementHost.BringToFront();
			}
			else
			{
				_deliverElementHost.Visible = false;
				if (_deliverPreviewBox != null)
				{
					_deliverPreviewBox.Visible = true;
					_deliverPreviewBox.BringToFront();
				}
			}
		}
	}

	internal void OpenDeliverPopoutPreview()
	{
		if (_cutEditSegments.Count == 0 || _deliverDuration <= 0.0)
		{
			MessageBox.Show(this, "当前无待交付成片工程，请先在【视频剪辑】中载入素材！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		if (_deliverPopoutForm == null || _deliverPopoutForm.IsDisposed)
		{
			_deliverPopoutForm = new DeliverPopoutPreviewForm(this);
			_deliverPopoutForm.Show(this);
		}
		else
		{
			_deliverPopoutForm.BringToFront();
		}

		AttachDeliverPlayerToPopout();

		if (_deliverPopoutForm != null)
		{
			_deliverPopoutForm.UpdateFrame(_deliverPreviewBox?.Image, _deliverCurrentPos, _deliverDuration, _deliverIsPlaying);
		}
	}

	private void CutEditMediaElement_MediaOpened(object sender, System.Windows.RoutedEventArgs e)
	{
		BeginInvoke((MethodInvoker)delegate
		{
			if (_cutEditIsPlaying && _cutEditMediaElement != null)
			{
				try { _cutEditMediaElement.Play(); } catch { }
			}
			UpdateCutEditTimeLabel();
			_cutEditTimelineCanvas?.Invalidate();
		});
	}

	private void CutEditMediaElement_MediaEnded(object sender, System.Windows.RoutedEventArgs e)
	{
		BeginInvoke((MethodInvoker)delegate
		{
			if (!_cutEditIsPlaying) return;

			var activeSeg = GetActiveVideoSegmentAtTime(_cutEditCurrentPos);
			double segEndTimeline = (activeSeg != null) ? (activeSeg.TimelineStartSeconds + activeSeg.Duration) : _cutEditDuration;

			if (segEndTimeline < _cutEditDuration - 0.05)
			{
				// Advance smoothly to next segment on timeline
				_cutEditCurrentPos = segEndTimeline;
				var nextSeg = GetActiveVideoSegmentAtTime(_cutEditCurrentPos);
				_lastActiveSegment = nextSeg;
				SyncPlayerAtCurrentPos(forceReload: false);
			}
			else
			{
				if (_cutEditLoopCheckBox?.Checked == true)
				{
					_cutEditCurrentPos = 0.0;
					_lastActiveSegment = null;
					SyncPlayerAtCurrentPos(forceReload: false);
				}
				else
				{
					_cutEditIsPlaying = false;
					if (_cutEditPlayPauseButton != null) _cutEditPlayPauseButton.Text = "▶ 播放 (空格)";
					_cutEditPlayTimer?.Stop();
				}
			}
		});
	}

	private void CutEditMediaElement_MediaFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
	{
		BeginInvoke((MethodInvoker)delegate
		{
			// Never black screen: fallback to frame extraction
			_cutEditElementHost.Visible = false;
			_cutEditPreviewBox.Visible = true;
			_cutEditPreviewBox.BringToFront();
			UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
		});
	}

	private void UpdateCutEditPreviewFrame(double timeSec, bool withTitlePreview = false)
	{
		if (_cutEditSegments.Count == 0) return;
		var activeSeg = GetActiveVideoSegmentAtTime(timeSec);
		if (activeSeg == null)
		{
			activeSeg = _cutEditSegments.FirstOrDefault(s => s.IsKept);
		}
		if (activeSeg == null)
		{
			RenderTimelineGapPreview();
			return;
		}

		if (activeSeg.MediaType == "image" || IsImage(activeSeg.SourcePath))
		{
			RenderImageClipPreview(activeSeg);
			return;
		}

		string ffmpeg = FindFfmpeg();
		if (string.IsNullOrEmpty(ffmpeg) || !File.Exists(activeSeg.SourcePath)) return;

		double inSourceSec = activeSeg.StartSeconds + Math.Max(0.0, timeSec - activeSeg.TimelineStartSeconds);

		// ULTRA-FAST IN-MEMORY COMPOSITING:
		// If the video frame at this exact timestamp is already cached, reuse it immediately in <1ms!
		// This eliminates all FFmpeg spawning and disk I/O when adjusting sliders, fonts, colors, scale, opacity, etc.!
		bool hasCachedBase = false;
		Bitmap cachedBaseClone = null;
		lock (_cutEditBaseFrameLock)
		{
			if (_cutEditCachedBaseFrame != null &&
				string.Equals(_cutEditCachedBaseFramePath, activeSeg.SourcePath, StringComparison.OrdinalIgnoreCase) &&
				Math.Abs(_cutEditCachedBaseFrameTime - inSourceSec) < 0.08)
			{
				hasCachedBase = true;
				cachedBaseClone = new Bitmap(_cutEditCachedBaseFrame);
			}
		}

		if (hasCachedBase && cachedBaseClone != null)
		{
			using (cachedBaseClone)
			{
				Bitmap fastBmp = new Bitmap(cachedBaseClone);
				RenderAllOverlaysOnBitmap(fastBmp, timeSec, forcePreviewSelected: withTitlePreview);
				RenderTransitionOnBitmap(fastBmp, timeSec);
				var old = _cutEditPreviewBox.Image;
				_cutEditPreviewBox.Image = fastBmp;
				_cutEditPreviewBox.Visible = true;
				_cutEditPreviewBox.BringToFront();
				_cutEditPreviewBox.Invalidate();
				old?.Dispose();

				if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed)
				{
					_cutEditPopoutForm.UpdateFrame(fastBmp, _cutEditCurrentPos, _cutEditDuration, _cutEditIsPlaying);
				}
			}
			return;
		}

		int seq = Interlocked.Increment(ref _cutEditPreviewSeq);
		ThreadPool.QueueUserWorkItem(delegate
		{
			string tempJpg = null;
			try
			{
				if (seq != _cutEditPreviewSeq) return;
				tempJpg = Path.Combine(Path.GetTempPath(), "CutEdit_Preview_" + Guid.NewGuid().ToString("N") + ".jpg");
				string tStr = Math.Max(0.0, inSourceSec).ToString("0.00", CultureInfo.InvariantCulture);
				string args = $"-hide_banner -y -ss {tStr} -noaccurate_seek -i {QuoteArg(activeSeg.SourcePath)} -frames:v 1 -q:v 2 {QuoteArg(tempJpg)}";

				ProcessStartInfo psi = NewProcessInfo(ffmpeg, args);
				using (Process proc = new Process())
				{
					proc.StartInfo = psi;
					proc.Start();
					proc.StandardOutput.ReadToEnd();
					proc.StandardError.ReadToEnd();
					proc.WaitForExit(3500);
				}

				if (seq != _cutEditPreviewSeq) return;

				if (File.Exists(tempJpg))
				{
					byte[] bytes = File.ReadAllBytes(tempJpg);
					using (MemoryStream ms = new MemoryStream(bytes))
					using (Bitmap origBmp = new Bitmap(ms))
					{
						lock (_cutEditBaseFrameLock)
						{
							_cutEditCachedBaseFrame?.Dispose();
							_cutEditCachedBaseFrame = new Bitmap(origBmp);
							_cutEditCachedBaseFramePath = activeSeg.SourcePath;
							_cutEditCachedBaseFrameTime = inSourceSec;
						}

						Bitmap frameBmp = new Bitmap(origBmp);
						RenderAllOverlaysOnBitmap(frameBmp, timeSec, forcePreviewSelected: withTitlePreview);
						RenderTransitionOnBitmap(frameBmp, timeSec);

						if (seq == _cutEditPreviewSeq)
						{
							BeginInvoke((MethodInvoker)delegate
							{
								if (seq == _cutEditPreviewSeq)
								{
									var old = _cutEditPreviewBox.Image;
									_cutEditPreviewBox.Image = frameBmp;
									_cutEditPreviewBox.Visible = true;
									_cutEditPreviewBox.BringToFront();
									_cutEditPreviewBox.Invalidate();
									old?.Dispose();

									if (_cutEditPopoutForm != null && !_cutEditPopoutForm.IsDisposed)
									{
										_cutEditPopoutForm.UpdateFrame(frameBmp, _cutEditCurrentPos, _cutEditDuration, _cutEditIsPlaying);
									}
								}
								else
								{
									frameBmp.Dispose();
								}
							});
						}
						else
						{
							frameBmp.Dispose();
						}
					}
				}
			}
			catch { }
			finally
			{
				TryDelete(tempJpg);
			}
		});
	}

	private static float AutoFitFontSize(Graphics g, string text, string fontFamily, FontStyle style, float preferredSize, float maxWidth, float minSize = 12f)
	{
		if (string.IsNullOrEmpty(text) || maxWidth <= 20) return preferredSize;
		float size = preferredSize;
		try
		{
			using (Font f = new Font(fontFamily, size, style))
			{
				SizeF sz = g.MeasureString(text, f);
				if (sz.Width > maxWidth && sz.Width > 0)
				{
					size = Math.Max(minSize, size * (maxWidth / sz.Width));
				}
			}
		}
		catch { }
		return size;
	}

	private static List<string> WrapTextToBalancedLines(Graphics g, string text, Font font, float maxWidth, int maxLines = 4)
	{
		var result = new List<string>();
		if (string.IsNullOrWhiteSpace(text)) return result;

		string[] paragraphs = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
		foreach (var p in paragraphs)
		{
			string trimmed = p.Trim();
			if (string.IsNullOrEmpty(trimmed)) continue;

			SizeF fullSz = g.MeasureString(trimmed, font);
			if (fullSz.Width <= maxWidth)
			{
				result.Add(trimmed);
				continue;
			}

			bool hasSpaces = trimmed.Contains(' ');
			if (hasSpaces)
			{
				string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				StringBuilder curLine = new StringBuilder();
				for (int i = 0; i < words.Length; i++)
				{
					string testLine = curLine.Length == 0 ? words[i] : (curLine.ToString() + " " + words[i]);
					if (g.MeasureString(testLine, font).Width <= maxWidth)
					{
						if (curLine.Length > 0) curLine.Append(" ");
						curLine.Append(words[i]);
					}
					else
					{
						if (curLine.Length > 0)
						{
							result.Add(curLine.ToString());
							curLine.Clear();
						}
						if (g.MeasureString(words[i], font).Width > maxWidth)
						{
							for (int c = 0; c < words[i].Length; c++)
							{
								string testChar = curLine.ToString() + words[i][c];
								if (g.MeasureString(testChar, font).Width <= maxWidth)
								{
									curLine.Append(words[i][c]);
								}
								else
								{
									result.Add(curLine.ToString());
									curLine.Clear();
									curLine.Append(words[i][c]);
								}
							}
						}
						else
						{
							curLine.Append(words[i]);
						}
					}
				}
				if (curLine.Length > 0)
				{
					result.Add(curLine.ToString());
				}
			}
			else
			{
				StringBuilder curLine = new StringBuilder();
				for (int i = 0; i < trimmed.Length; i++)
				{
					string testLine = curLine.ToString() + trimmed[i];
					if (g.MeasureString(testLine, font).Width <= maxWidth)
					{
						curLine.Append(trimmed[i]);
					}
					else
					{
						result.Add(curLine.ToString());
						curLine.Clear();
						curLine.Append(trimmed[i]);
					}
				}
				if (curLine.Length > 0)
				{
					result.Add(curLine.ToString());
				}
			}
		}

		if (result.Count == 2 && result[0].Contains(' ') && result[1].Contains(' '))
		{
			string combined = result[0] + " " + result[1];
			string[] words = combined.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (words.Length >= 4)
			{
				int half = words.Length / 2;
				string l1 = string.Join(" ", words.Take(half));
				string l2 = string.Join(" ", words.Skip(half));
				if (g.MeasureString(l1, font).Width <= maxWidth && g.MeasureString(l2, font).Width <= maxWidth)
				{
					result[0] = l1;
					result[1] = l2;
				}
			}
		}

		return result;
	}

	private void TriggerTitleLivePreview()
	{
		if (_cutEditElementHost != null) _cutEditElementHost.Visible = false;
		if (_cutEditPreviewBox != null)
		{
			_cutEditPreviewBox.Visible = true;
			_cutEditPreviewBox.BringToFront();
		}
		UpdateCutEditPreviewFrame(_cutEditCurrentPos, withTitlePreview: true);
		_cutEditTimelineCanvas?.Invalidate();
	}

	private void SetTitleFontSize(int sizePx)
	{
		if (_cutEditTitleFontSizeSlider != null)
		{
			_cutEditTitleFontSizeSlider.Value = Math.Max(_cutEditTitleFontSizeSlider.Minimum, Math.Min(_cutEditTitleFontSizeSlider.Maximum, sizePx));
		}
	}

	private static string GetTitleSizeDescription(int size)
	{
		if (size >= 70) return "🔥 巨大封面";
		if (size >= 54) return "⚡ 醒目大字";
		if (size >= 38) return "✨ 标准字号";
		return "📝 适中内敛";
	}
	private void InitDefaultOverlays()
	{
		if (_cutEditOverlays.Count == 0)
		{
			var defItem = new CutOverlayItem
			{
				Name = "片头标题包装",
				Type = OverlayItemType.Text,
				StartSeconds = 0.0,
				Duration = 5.0,
				TextContent = "CINEMATIC MOMENTS",
				SubtitleContent = "A Story of Light and Motion",
				PositionPreset = "居中偏下",
				FontSize = 52,
				TextColorIndex = 0,
				StrokeIndex = 0,
				BannerBgIndex = 0,
				IsTitleCard = false
			};
			_cutEditOverlays.Add(defItem);
			_selectedOverlay = defItem;
		}
	}

	private void RefreshOverlayCombo(CutOverlayItem selectItem = null)
	{
		if (_overlayItemCombo == null) return;
		_updatingOverlayInspector = true;
		try
		{
			_overlayItemCombo.Items.Clear();
			for (int i = 0; i < _cutEditOverlays.Count; i++)
			{
				var item = _cutEditOverlays[i];
				_overlayItemCombo.Items.Add($"#{i + 1} {item.GetDisplayName()}");
			}
			if (selectItem != null && _cutEditOverlays.Contains(selectItem))
			{
				_selectedOverlay = selectItem;
			}
			else if (_cutEditOverlays.Count > 0 && (_selectedOverlay == null || !_cutEditOverlays.Contains(_selectedOverlay)))
			{
				_selectedOverlay = _cutEditOverlays[0];
			}

			if (_selectedOverlay != null)
			{
				int idx = _cutEditOverlays.IndexOf(_selectedOverlay);
				if (idx >= 0 && idx < _overlayItemCombo.Items.Count)
				{
					_overlayItemCombo.SelectedIndex = idx;
				}
			}
		}
		finally
		{
			_updatingOverlayInspector = false;
		}
		SyncSelectedOverlayToControls();
		_cutEditTimelineCanvas?.Invalidate();
		TriggerTitleLivePreview();
		UpdateDeliverSummary();
	}

	private void SyncSelectedOverlayToControls()
	{
		if (_selectedOverlay == null || _overlayItemEnabledCheckBox == null) return;
		_updatingOverlayInspector = true;
		try
		{
			_overlayItemEnabledCheckBox.Checked = _selectedOverlay.Enabled;
			if (_cutEditTitleEnabled != null) _cutEditTitleEnabled.Checked = _selectedOverlay.Enabled;

			if (_overlayStartTimeNum != null)
			{
				_overlayStartTimeNum.Value = (decimal)Math.Max(0.0, Math.Min(3600.0, _selectedOverlay.StartSeconds));
				if (_cutEditTitleStartTime != null) _cutEditTitleStartTime.Value = _overlayStartTimeNum.Value;
			}
			if (_overlayDurationNum != null)
			{
				_overlayDurationNum.Value = (decimal)Math.Max(0.5, Math.Min(3600.0, _selectedOverlay.Duration));
				if (_cutEditTitleDuration != null) _cutEditTitleDuration.Value = _overlayDurationNum.Value;
			}

			if (_overlayPositionCombo != null)
			{
				int pIdx = _overlayPositionCombo.FindString(_selectedOverlay.PositionPreset);
				_overlayPositionCombo.SelectedIndex = (pIdx >= 0) ? pIdx : 0;
			}
			if (_overlayOffsetXNum != null) _overlayOffsetXNum.Value = Math.Max(-1920, Math.Min(1920, _selectedOverlay.OffsetX));
			if (_overlayOffsetYNum != null) _overlayOffsetYNum.Value = Math.Max(-1080, Math.Min(1080, _selectedOverlay.OffsetY));

			if (_selectedOverlay.Type == OverlayItemType.Text)
			{
				if (_overlayTextPropsPanel != null) _overlayTextPropsPanel.Visible = true;
				if (_overlayImagePropsPanel != null) _overlayImagePropsPanel.Visible = false;

				if (_cutEditMainTitle != null) _cutEditMainTitle.Text = _selectedOverlay.TextContent;
				if (_cutEditSubtitle != null) _cutEditSubtitle.Text = _selectedOverlay.SubtitleContent;
				if (_cutEditTitleFontSizeSlider != null) _cutEditTitleFontSizeSlider.Value = Math.Max(20, Math.Min(110, _selectedOverlay.FontSize));
				if (_cutEditTitleTextColorCombo != null && _selectedOverlay.TextColorIndex < _cutEditTitleTextColorCombo.Items.Count)
					_cutEditTitleTextColorCombo.SelectedIndex = _selectedOverlay.TextColorIndex;
				if (_cutEditTitleStrokeCombo != null && _selectedOverlay.StrokeIndex < _cutEditTitleStrokeCombo.Items.Count)
					_cutEditTitleStrokeCombo.SelectedIndex = _selectedOverlay.StrokeIndex;
				if (_cutEditTitleBannerBgCombo != null && _selectedOverlay.BannerBgIndex < _cutEditTitleBannerBgCombo.Items.Count)
					_cutEditTitleBannerBgCombo.SelectedIndex = _selectedOverlay.BannerBgIndex;
				if (_cutEditTitleCardRadio != null) _cutEditTitleCardRadio.Checked = _selectedOverlay.IsTitleCard;
				if (_cutEditTitleOverlayRadio != null) _cutEditTitleOverlayRadio.Checked = !_selectedOverlay.IsTitleCard;
			}
			else
			{
				if (_overlayTextPropsPanel != null) _overlayTextPropsPanel.Visible = false;
				if (_overlayImagePropsPanel != null) _overlayImagePropsPanel.Visible = true;

				if (_overlayImagePathText != null) _overlayImagePathText.Text = _selectedOverlay.ImagePath;
				if (_overlayImageScaleSlider != null) _overlayImageScaleSlider.Value = Math.Max(10, Math.Min(300, _selectedOverlay.ScalePercent));
				if (_overlayImageScaleLabel != null) _overlayImageScaleLabel.Text = $"缩放尺寸: {_selectedOverlay.ScalePercent}%";
				if (_overlayImageOpacitySlider != null) _overlayImageOpacitySlider.Value = Math.Max(10, Math.Min(100, _selectedOverlay.OpacityPercent));
				if (_overlayImageOpacityLabel != null) _overlayImageOpacityLabel.Text = $"不透明度: {_selectedOverlay.OpacityPercent}%";

				UpdateOverlayImageThumb(_selectedOverlay.ImagePath);
			}
		}
		finally
		{
			_updatingOverlayInspector = false;
		}
	}

	private void SyncControlsToSelectedOverlay()
	{
		if (_selectedOverlay == null || _updatingOverlayInspector) return;

		_selectedOverlay.Enabled = _overlayItemEnabledCheckBox?.Checked ?? true;
		_selectedOverlay.StartSeconds = (double)(_overlayStartTimeNum?.Value ?? 0m);
		_selectedOverlay.Duration = (double)(_overlayDurationNum?.Value ?? 5m);
		if (_overlayPositionCombo?.SelectedItem != null)
		{
			string posStr = _overlayPositionCombo.SelectedItem.ToString();
			if (posStr.Contains("(")) posStr = posStr.Split('(')[0].Trim();
			_selectedOverlay.PositionPreset = posStr;
		}
		_selectedOverlay.OffsetX = (int)(_overlayOffsetXNum?.Value ?? 0m);
		_selectedOverlay.OffsetY = (int)(_overlayOffsetYNum?.Value ?? 0m);

		if (_selectedOverlay.Type == OverlayItemType.Text)
		{
			_selectedOverlay.TextContent = _cutEditMainTitle?.Text ?? "";
			_selectedOverlay.SubtitleContent = _cutEditSubtitle?.Text ?? "";
			_selectedOverlay.FontSize = _cutEditTitleFontSizeSlider?.Value ?? 52;
			_selectedOverlay.TextColorIndex = _cutEditTitleTextColorCombo?.SelectedIndex ?? 0;
			_selectedOverlay.StrokeIndex = _cutEditTitleStrokeCombo?.SelectedIndex ?? 0;
			_selectedOverlay.BannerBgIndex = _cutEditTitleBannerBgCombo?.SelectedIndex ?? 0;
			_selectedOverlay.IsTitleCard = _cutEditTitleCardRadio?.Checked == true;
		}
		else
		{
			_selectedOverlay.ImagePath = _overlayImagePathText?.Text ?? "";
			_selectedOverlay.ScalePercent = _overlayImageScaleSlider?.Value ?? 80;
			_selectedOverlay.OpacityPercent = _overlayImageOpacitySlider?.Value ?? 100;
		}

		if (_overlayItemCombo != null && _selectedOverlay != null)
		{
			int idx = _cutEditOverlays.IndexOf(_selectedOverlay);
			if (idx >= 0 && idx < _overlayItemCombo.Items.Count)
			{
				_updatingOverlayInspector = true;
				_overlayItemCombo.Items[idx] = $"#{idx + 1} {_selectedOverlay.GetDisplayName()}";
				_updatingOverlayInspector = false;
			}
		}

		_cutEditTimelineCanvas?.Invalidate();
		UpdateCutEditWpfOverlay(_selectedOverlay?.StartSeconds ?? _cutEditCurrentPos, forcePreviewSelected: true);
		TriggerTitleLivePreview();
		UpdateDeliverSummary();
	}

	private void SelectOverlayItem(CutOverlayItem item)
	{
		if (item == null || !_cutEditOverlays.Contains(item)) return;
		_selectedOverlay = item;
		int idx = _cutEditOverlays.IndexOf(item);
		if (_overlayItemCombo != null && idx >= 0 && idx < _overlayItemCombo.Items.Count)
		{
			_overlayItemCombo.SelectedIndex = idx;
		}
		else
		{
			SyncSelectedOverlayToControls();
		}
	}

	private void UpdateOverlayImageThumb(string path)
	{
		if (_overlayImageThumbBox == null) return;
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			_overlayImageThumbBox.Image = null;
			return;
		}
		try
		{
			var img = GetOrCreateOverlayImage(path);
			_overlayImageThumbBox.Image = img;
		}
		catch
		{
			_overlayImageThumbBox.Image = null;
		}
	}

	private Image GetOrCreateOverlayImage(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
		lock (_overlayImageCache)
		{
			if (_overlayImageCache.TryGetValue(path, out var cached))
			{
				return cached;
			}
			try
			{
				byte[] bytes = File.ReadAllBytes(path);
				using (MemoryStream ms = new MemoryStream(bytes))
				{
					Image loaded = Image.FromStream(ms);
					Bitmap bmp = new Bitmap(loaded);
					_overlayImageCache[path] = bmp;
					return bmp;
				}
			}
			catch
			{
				return null;
			}
		}
	}

	private void RenderSingleTextOverlay(Graphics g, CutOverlayItem item, int w, int h)
	{
		if (item == null) return;
		string mainText = item.TextContent ?? "CINEMATIC MOMENTS";
		string subText = item.SubtitleContent ?? "";
		int fontSizeVal = item.FontSize;
		int colorIdx = item.TextColorIndex;
		int strokeIdx = item.StrokeIndex;
		int bannerIdx = item.BannerBgIndex;

		if (item.IsTitleCard || bannerIdx == 4)
		{
			g.Clear(Color.FromArgb(10, 10, 14));
		}

		float resScale = Math.Max(0.25f, (float)w / 1080f);
		float chosenFontSize = fontSizeVal * resScale;

		Color mainTextColor;
		switch (colorIdx)
		{
			case 1: mainTextColor = Color.FromArgb(253, 224, 71); break;
			case 2: mainTextColor = Color.FromArgb(34, 211, 238); break;
			case 3: mainTextColor = Color.FromArgb(239, 68, 68); break;
			case 4: mainTextColor = Color.FromArgb(245, 158, 11); break;
			case 5: mainTextColor = Color.FromArgb(132, 204, 22); break;
			case 6: mainTextColor = Color.FromArgb(249, 115, 22); break;
			case 7: mainTextColor = Color.FromArgb(244, 114, 182); break;
			default: mainTextColor = Color.White; break;
		}

		float strokeWidth = 0f;
		bool hasShadow = true;
		Color strokeColor = Color.FromArgb(235, 10, 10, 14);
		switch (strokeIdx)
		{
			case 0: strokeWidth = 8f * resScale; break;
			case 1: strokeWidth = 12f * resScale; break;
			case 2: strokeWidth = 5f * resScale; break;
			case 3: strokeWidth = 2.5f * resScale; break;
			case 4: strokeWidth = 0f; break;
		}

		float maxTitleW = w * 0.88f;
		float effectiveFontSize = chosenFontSize;
		List<string> mainLines = null;
		Font fontMain = null;

		while (effectiveFontSize >= 20f * resScale)
		{
			fontMain?.Dispose();
			fontMain = new Font("Microsoft YaHei UI", effectiveFontSize, FontStyle.Bold);
			mainLines = WrapTextToBalancedLines(g, mainText, fontMain, maxTitleW);
			if (mainLines.Count <= 3) break;
			effectiveFontSize -= 2f * resScale;
		}

		if (fontMain == null)
		{
			fontMain = new Font("Microsoft YaHei UI", effectiveFontSize, FontStyle.Bold);
			mainLines = WrapTextToBalancedLines(g, mainText, fontMain, maxTitleW);
		}

		using (fontMain)
		{
			if (mainLines.Count == 0 && !string.IsNullOrWhiteSpace(mainText))
			{
				mainLines.Add(mainText);
			}

			float mainLineHeight = fontMain.GetHeight(g) * 1.15f;
			float mainBlockHeight = Math.Max(mainLineHeight, mainLines.Count * mainLineHeight);

			bool hasSub = !string.IsNullOrWhiteSpace(subText);
			float subFontSize = Math.Max(11f * resScale, effectiveFontSize * 0.44f);
			float subBlockHeight = 0f;
			using (Font fontSub = new Font("Microsoft YaHei UI", subFontSize, FontStyle.Bold))
			{
				if (hasSub)
				{
					subBlockHeight = fontSub.GetHeight(g) * 1.25f + 16f * resScale;
				}

				float totalBlockH = mainBlockHeight + subBlockHeight;

				float maxLineWidth = 0f;
				foreach (var line in mainLines)
				{
					float lw = g.MeasureString(line, fontMain).Width;
					if (lw > maxLineWidth) maxLineWidth = lw;
				}
				if (hasSub)
				{
					float subW = g.MeasureString(subText, fontSub).Width + 40f * resScale;
					if (subW > maxLineWidth) maxLineWidth = subW;
				}
				maxLineWidth = Math.Min(w * 0.94f, maxLineWidth);

				string pos = item.PositionPreset ?? "居中偏下";
				float targetCenterX;
				float targetCenterY;

				if (pos.Contains("偏上"))
				{
					targetCenterX = w / 2f + item.OffsetX;
					targetCenterY = h * 0.22f + item.OffsetY;
				}
				else if (pos.Contains("居中") || pos.Contains("中央"))
				{
					targetCenterX = w / 2f + item.OffsetX;
					targetCenterY = h * 0.50f + item.OffsetY;
				}
				else if (pos.Contains("左上"))
				{
					targetCenterX = maxLineWidth / 2f + 40f * resScale + item.OffsetX;
					targetCenterY = totalBlockH / 2f + 40f * resScale + item.OffsetY;
				}
				else if (pos.Contains("右上"))
				{
					targetCenterX = w - maxLineWidth / 2f - 40f * resScale + item.OffsetX;
					targetCenterY = totalBlockH / 2f + 40f * resScale + item.OffsetY;
				}
				else if (pos.Contains("左下"))
				{
					targetCenterX = maxLineWidth / 2f + 40f * resScale + item.OffsetX;
					targetCenterY = h - totalBlockH / 2f - 40f * resScale + item.OffsetY;
				}
				else if (pos.Contains("右下"))
				{
					targetCenterX = w - maxLineWidth / 2f - 40f * resScale + item.OffsetX;
					targetCenterY = h - totalBlockH / 2f - 40f * resScale + item.OffsetY;
				}
				else if (pos.Contains("自定义"))
				{
					targetCenterX = item.OffsetX;
					targetCenterY = item.OffsetY;
				}
				else
				{
					targetCenterX = w / 2f + item.OffsetX;
					targetCenterY = h * 0.76f + item.OffsetY;
				}

				float blockTopY = targetCenterY - totalBlockH / 2f;
				blockTopY = Math.Max(10f * resScale, Math.Min(h - totalBlockH - 10f * resScale, blockTopY));

				switch (bannerIdx)
				{
					case 0:
					{
						int barH = (int)(totalBlockH + 110f * resScale);
						int barY = (int)Math.Max(0, blockTopY - 55f * resScale);
						using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, barY, w, barH), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(195, 0, 0, 0), LinearGradientMode.Vertical))
						{
							lgb.SetBlendTriangularShape(0.5f);
							g.FillRectangle(lgb, 0, barY, w, barH);
						}
						break;
					}
					case 1:
					{
						float cardW = Math.Min(w * 0.94f, maxLineWidth + 70f * resScale);
						float cardH = totalBlockH + 36f * resScale;
						RectangleF cardRect = new RectangleF(targetCenterX - cardW / 2f, blockTopY - 18f * resScale, cardW, cardH);
						using (GraphicsPath cardPath = CreateRoundedRectanglePath(Rectangle.Round(cardRect), (int)(18f * resScale)))
						using (Brush bgBrush = new SolidBrush(Color.FromArgb(253, 224, 71)))
						using (Pen borderPen = new Pen(Color.FromArgb(245, 158, 11), 3f * resScale))
						{
							g.FillPath(bgBrush, cardPath);
							g.DrawPath(borderPen, cardPath);
						}
						if (colorIdx == 0) mainTextColor = Color.FromArgb(15, 23, 42);
						break;
					}
					case 2:
					{
						float cardW = Math.Min(w * 0.94f, maxLineWidth + 64f * resScale);
						float cardH = totalBlockH + 34f * resScale;
						RectangleF cardRect = new RectangleF(targetCenterX - cardW / 2f, blockTopY - 17f * resScale, cardW, cardH);
						using (GraphicsPath cardPath = CreateRoundedRectanglePath(Rectangle.Round(cardRect), (int)(16f * resScale)))
						using (Brush bgBrush = new SolidBrush(Color.FromArgb(215, 15, 23, 42)))
						using (Pen borderPen = new Pen(Color.FromArgb(245, 158, 11), 2.5f * resScale))
						{
							g.FillPath(bgBrush, cardPath);
							g.DrawPath(borderPen, cardPath);
						}
						break;
					}
					case 3:
					{
						float cardW = Math.Min(w * 0.94f, maxLineWidth + 64f * resScale);
						float cardH = totalBlockH + 34f * resScale;
						RectangleF cardRect = new RectangleF(targetCenterX - cardW / 2f, blockTopY - 17f * resScale, cardW, cardH);
						using (GraphicsPath cardPath = CreateRoundedRectanglePath(Rectangle.Round(cardRect), (int)(16f * resScale)))
						using (Brush bgBrush = new SolidBrush(Color.FromArgb(225, 10, 15, 26)))
						using (Pen borderPen = new Pen(Color.FromArgb(34, 211, 238), 3f * resScale))
						{
							g.FillPath(bgBrush, cardPath);
							g.DrawPath(borderPen, cardPath);
						}
						break;
					}
					case 5:
					{
						float cardW = Math.Min(w * 0.94f, maxLineWidth + 64f * resScale);
						float cardH = totalBlockH + 34f * resScale;
						RectangleF cardRect = new RectangleF(targetCenterX - cardW / 2f, blockTopY - 17f * resScale, cardW, cardH);
						using (GraphicsPath cardPath = CreateRoundedRectanglePath(Rectangle.Round(cardRect), (int)(18f * resScale)))
						using (Brush bgBrush = new SolidBrush(Color.FromArgb(215, 20, 20, 30)))
						using (Pen borderPen = new Pen(Color.FromArgb(244, 114, 182), 2.5f * resScale))
						{
							g.FillPath(bgBrush, cardPath);
							g.DrawPath(borderPen, cardPath);
						}
						break;
					}
					case 6:
					{
						float cardW = Math.Min(w * 0.94f, maxLineWidth + 70f * resScale);
						float cardH = totalBlockH + 36f * resScale;
						RectangleF cardRect = new RectangleF(targetCenterX - cardW / 2f, blockTopY - 18f * resScale, cardW, cardH);
						using (GraphicsPath cardPath = CreateRoundedRectanglePath(Rectangle.Round(cardRect), (int)(16f * resScale)))
						using (Brush bgBrush = new SolidBrush(Color.FromArgb(235, 225, 29, 72)))
						using (Pen borderPen = new Pen(Color.FromArgb(254, 202, 202), 3f * resScale))
						{
							g.FillPath(bgBrush, cardPath);
							g.DrawPath(borderPen, cardPath);
						}
						break;
					}
					case 7:
					{
						int barH = (int)(totalBlockH + 70f * resScale);
						int barY = (int)Math.Max(0, blockTopY - 35f * resScale);
						using (LinearGradientBrush lgb = new LinearGradientBrush(new Rectangle(0, barY, w, barH), Color.FromArgb(230, 15, 23, 42), Color.FromArgb(230, 30, 58, 138), LinearGradientMode.Horizontal))
						{
							g.FillRectangle(lgb, 0, barY, w, barH);
						}
						using (Pen cyanLine = new Pen(Color.FromArgb(56, 189, 248), 2f * resScale))
						{
							g.DrawLine(cyanLine, 0, barY, w, barY);
							g.DrawLine(cyanLine, 0, barY + barH, w, barY + barH);
						}
						break;
					}
				}

				for (int i = 0; i < mainLines.Count; i++)
				{
					string line = mainLines[i];
					SizeF lineSz = g.MeasureString(line, fontMain);
					float lineX = targetCenterX - lineSz.Width / 2f;
					float lineY = blockTopY + i * mainLineHeight;

					using (GraphicsPath path = new GraphicsPath())
					{
						float emSize = g.DpiY * fontMain.Size / 72f;
						path.AddString(line, fontMain.FontFamily, (int)fontMain.Style, emSize, new PointF(lineX, lineY), StringFormat.GenericDefault);

						if (hasShadow)
						{
							using (GraphicsPath shadowPath = (GraphicsPath)path.Clone())
							using (Matrix mx = new Matrix())
							{
								mx.Translate(3.5f * resScale, 4.5f * resScale);
								shadowPath.Transform(mx);
								using (Brush shadowBrush = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
								{
									g.FillPath(shadowBrush, shadowPath);
								}
								if (strokeWidth > 0)
								{
									using (Pen shadowPen = new Pen(Color.FromArgb(130, 0, 0, 0), strokeWidth))
									{
										shadowPen.LineJoin = LineJoin.Round;
										g.DrawPath(shadowPen, shadowPath);
									}
								}
							}
						}

						if (strokeWidth > 0)
						{
							using (Pen pen = new Pen(strokeColor, strokeWidth))
							{
								pen.LineJoin = LineJoin.Round;
								g.DrawPath(pen, path);
							}
						}

						using (Brush fillBrush = new SolidBrush(mainTextColor))
						{
							g.FillPath(fillBrush, path);
						}
					}
				}

				if (hasSub)
				{
					float subY = blockTopY + mainBlockHeight + 10f * resScale;
					SizeF subSz = g.MeasureString(subText, fontSub);

					if (bannerIdx == 1 || colorIdx == 1)
					{
						float pillW = Math.Min(w * 0.90f, subSz.Width + 36f * resScale);
						float pillH = subSz.Height + 10f * resScale;
						RectangleF pillRect = new RectangleF(targetCenterX - pillW / 2f, subY, pillW, pillH);
						using (GraphicsPath pillPath = CreateRoundedRectanglePath(Rectangle.Round(pillRect), (int)(pillH / 2f)))
						using (Brush pillBrush = new SolidBrush(Color.FromArgb(220, 38, 38)))
						{
							g.FillPath(pillBrush, pillPath);
						}
						using (Brush whiteBrush = new SolidBrush(Color.White))
						using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
						{
							g.DrawString(subText, fontSub, whiteBrush, pillRect, sf);
						}
					}
					else
					{
						float subX = targetCenterX - subSz.Width / 2f;
						using (Brush subShadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
						using (Brush subBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
						{
							g.DrawString(subText, fontSub, subShadow, subX + 1.5f * resScale, subY + 2f * resScale);
							g.DrawString(subText, fontSub, subBrush, subX, subY);
						}
					}
				}
			}
		}
	}

	private void RenderSingleImageOverlay(Graphics g, CutOverlayItem item, int w, int h)
	{
		if (item == null || string.IsNullOrEmpty(item.ImagePath) || !File.Exists(item.ImagePath)) return;
		Image img = GetOrCreateOverlayImage(item.ImagePath);
		if (img == null) return;

		float scale = Math.Max(0.1f, Math.Min(4.0f, (float)item.ScalePercent / 100.0f));
		float baseScale = Math.Max(0.35f, (float)w / 1920f);
		float destW = img.Width * scale * baseScale;
		float destH = img.Height * scale * baseScale;

		float destX = 30 * baseScale;
		float destY = 30 * baseScale;
		int padX = (int)(30 * baseScale);
		int padY = (int)(30 * baseScale);

		string pos = item.PositionPreset ?? "右下角";
		if (pos.Contains("左上"))
		{
			destX = padX + item.OffsetX;
			destY = padY + item.OffsetY;
		}
		else if (pos.Contains("右上"))
		{
			destX = w - destW - padX + item.OffsetX;
			destY = padY + item.OffsetY;
		}
		else if (pos.Contains("偏上"))
		{
			destX = (w - destW) / 2f + item.OffsetX;
			destY = padY + (h * 0.08f) + item.OffsetY;
		}
		else if (pos.Contains("居中") || pos.Contains("中央"))
		{
			destX = (w - destW) / 2f + item.OffsetX;
			destY = (h - destH) / 2f + item.OffsetY;
		}
		else if (pos.Contains("左下"))
		{
			destX = padX + item.OffsetX;
			destY = h - destH - padY + item.OffsetY;
		}
		else if (pos.Contains("偏下"))
		{
			destX = (w - destW) / 2f + item.OffsetX;
			destY = h - destH - padY - (h * 0.08f) + item.OffsetY;
		}
		else if (pos.Contains("右下"))
		{
			destX = w - destW - padX + item.OffsetX;
			destY = h - destH - padY + item.OffsetY;
		}
		else
		{
			destX = item.OffsetX;
			destY = item.OffsetY;
		}

		float alpha = Math.Max(0.05f, Math.Min(1.0f, item.OpacityPercent / 100.0f));
		using (ImageAttributes attr = new ImageAttributes())
		{
			ColorMatrix matrix = new ColorMatrix { Matrix33 = alpha };
			attr.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
			Rectangle destRect = new Rectangle((int)destX, (int)destY, Math.Max(1, (int)destW), Math.Max(1, (int)destH));
			g.DrawImage(img, destRect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
		}
	}

	private void RenderAllOverlaysOnBitmap(Bitmap bmp, double timeSec, bool forcePreviewSelected = false)
	{
		if (bmp == null || _cutEditOverlays.Count == 0) return;
		int w = bmp.Width;
		int h = bmp.Height;

		using (Graphics g = Graphics.FromImage(bmp))
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;

			if (forcePreviewSelected && _selectedOverlay != null)
			{
				if (_selectedOverlay.Enabled)
				{
					if (_selectedOverlay.Type == OverlayItemType.Text)
						RenderSingleTextOverlay(g, _selectedOverlay, w, h);
					else if (_selectedOverlay.Type == OverlayItemType.Image)
						RenderSingleImageOverlay(g, _selectedOverlay, w, h);
				}
				return;
			}

			foreach (var item in _cutEditOverlays)
			{
				if (!item.Enabled) continue;
				if (timeSec < item.StartSeconds || timeSec > (item.StartSeconds + item.Duration)) continue;

				if (item.Type == OverlayItemType.Text)
				{
					RenderSingleTextOverlay(g, item, w, h);
				}
				else if (item.Type == OverlayItemType.Image)
				{
					RenderSingleImageOverlay(g, item, w, h);
				}
			}
		}
	}

	private void RenderTitleOverlayOnBitmap(Bitmap bmp)
	{
		RenderAllOverlaysOnBitmap(bmp, _cutEditCurrentPos, forcePreviewSelected: true);
	}

	private void StartExportEditedVideo()
	{
		SwitchToWorkspace(6);
		StartDeliverExport();
	}

	private void UpdateDeliverSummary()
	{
		if (_deliverProjectSummary == null) return;
		if (string.IsNullOrEmpty(_cutEditSourcePath) || !File.Exists(_cutEditSourcePath))
		{
			_deliverProjectSummary.Text = "当前待交付工程状态:\n• 素材源: (尚未载入视频素材)\n• 请先在【🎬 视频剪辑】工作台载入素材并完成分段修剪";
			_deliverProjectSummary.ForeColor = MutedColor;
			if (_deliverStartButton != null) _deliverStartButton.Enabled = false;
			return;
		}

		var kept = _cutEditSegments.Where(s => s.IsKept).ToList();
		double totalKept = kept.Sum(s => s.Duration);
		var activeOverlays = _cutEditOverlays.Where(o => o.Enabled).ToList();
		int txtCount = activeOverlays.Count(o => o.Type == OverlayItemType.Text);
		int imgCount = activeOverlays.Count(o => o.Type == OverlayItemType.Image);
		string overlayInfo = (activeOverlays.Count > 0)
			? $"已启用 {activeOverlays.Count} 项图文包装 (文案: {txtCount} 条, 贴图/二维码: {imgCount} 张)"
			: "未启用图文包装";
		string audioInfo = _cutEditAudioMuted
			? "A1 音频已静音 (不输出声音)"
			: $"A1 声音正常 (音量 {_cutEditAudioVolume}%)";

		_deliverProjectSummary.Text = $"当前待交付工程状态:\n" +
			$"• 素材源: {Path.GetFileName(_cutEditSourcePath)} ({_cutEditWidth}x{_cutEditHeight})\n" +
			$"• 剪辑分段: 共 {_cutEditSegments.Count} 段 (保留 {kept.Count} 段，已剔除 {_cutEditSegments.Count - kept.Count} 段)\n" +
			$"• 预计成片时长: {FormatDuration(totalKept)}\n" +
			$"• 图文与贴片: {overlayInfo}\n" +
			$"• 音频轨道: {audioInfo}";
		_deliverProjectSummary.ForeColor = _isDarkMode ? Color.White : Color.Black;
		if (_deliverStartButton != null) _deliverStartButton.Enabled = kept.Count > 0;
	}

	private void StartDeliverExport()
	{
		if (string.IsNullOrEmpty(_cutEditSourcePath) || !File.Exists(_cutEditSourcePath))
		{
			MessageBox.Show(this, "尚未载入视频素材，请先在【视频剪辑】工作台中载入并修剪视频！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			SwitchToWorkspace(5);
			return;
		}

		var kept = _cutEditSegments.Where(s => s.IsKept).OrderBy(s => s.TimelineStartSeconds).ToList();
		if (kept.Count == 0)
		{
			MessageBox.Show(this, "所有切片均已被剔除，请至少保留一个片段后再交付！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		string outFolder = _deliverOutputFolder.Text.Trim();
		if (string.IsNullOrEmpty(outFolder))
		{
			outFolder = Path.GetDirectoryName(_cutEditSourcePath);
			_deliverOutputFolder.Text = outFolder;
		}
		if (!Directory.Exists(outFolder))
		{
			try { Directory.CreateDirectory(outFolder); }
			catch (Exception ex)
			{
				MessageBox.Show(this, "创建输出目录失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
		}

		string rawFileName = _deliverOutputFileName.Text.Trim();
		if (string.IsNullOrEmpty(rawFileName)) rawFileName = "交付成片_{timestamp}.mp4";
		string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		string finalFileName = rawFileName.Replace("{timestamp}", timeStamp).Replace("{time}", timeStamp);

		int formatIdx = _deliverFormatCombo.SelectedIndex;
		string ext = (formatIdx == 1) ? ".mov" : ((formatIdx == 2) ? ".mkv" : ".mp4");
		string currentExt = Path.GetExtension(finalFileName);
		if (string.IsNullOrEmpty(currentExt) || !string.Equals(currentExt, ext, StringComparison.OrdinalIgnoreCase))
		{
			finalFileName = Path.ChangeExtension(finalFileName, ext);
		}
		string targetOutPath = Path.Combine(outFolder, finalFileName);

		string ffmpeg = FindFfmpeg();
		if (string.IsNullOrEmpty(ffmpeg))
		{
			MessageBox.Show(this, "未找到 FFmpeg 核心组件，无法执行交付渲染！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}

		_deliverStartButton.Enabled = false;
		_deliverProgressBar.Value = 8;
		_deliverStatusLabel.Text = "正在准备硬件编码、滤镜与分段拼接...";

		ThreadPool.QueueUserWorkItem(delegate
		{
			string tempTitlePng = null;
			List<string> tempOverlayPngs = new List<string>();
			try
			{
				List<CutOverlayItem> exportOverlays = new List<CutOverlayItem>();
				int outResIndex = 0;
				int qualityIndex = 1;
				int fpsIndex = 0;
				int audioBitrateIndex = 1;
				bool audioMuted = _cutEditAudioMuted;
				int audioVolume = _cutEditAudioVolume;

				Invoke((MethodInvoker)delegate
				{
					exportOverlays = _cutEditOverlays.Where(o => o.Enabled && o.Duration > 0.0).Select(o => o.Clone()).ToList();
					outResIndex = _deliverResolutionCombo?.SelectedIndex ?? 0;
					qualityIndex = _deliverQualityCombo?.SelectedIndex ?? 1;
					fpsIndex = _deliverFpsCombo?.SelectedIndex ?? 0;
					audioBitrateIndex = _deliverAudioBitrateCombo?.SelectedIndex ?? 1;
				});

				int targetW = _cutEditWidth > 0 ? _cutEditWidth : 1920;
				int targetH = _cutEditHeight > 0 ? _cutEditHeight : 1080;
				if (outResIndex == 1) { targetW = 3840; targetH = 2160; }
				else if (outResIndex == 2) { targetW = 1920; targetH = 1080; }
				else if (outResIndex == 3) { targetW = 1280; targetH = 720; }
				else if (outResIndex == 4) { targetW = 1080; targetH = 1920; }
				else if (outResIndex == 5) { targetW = 1080; targetH = 1080; }

				for (int oi = 0; oi < exportOverlays.Count; oi++)
				{
					var olItem = exportOverlays[oi];
					string tempOlPng = Path.Combine(Path.GetTempPath(), $"CutEdit_ExportOl_{oi}_" + Guid.NewGuid().ToString("N") + ".png");
					tempOverlayPngs.Add(tempOlPng);
					using (Bitmap olBmp = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb))
					{
						using (Graphics g = Graphics.FromImage(olBmp))
						{
							g.Clear(Color.Transparent);
							g.SmoothingMode = SmoothingMode.AntiAlias;
							g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
							g.InterpolationMode = InterpolationMode.HighQualityBicubic;

							if (olItem.Type == OverlayItemType.Text)
							{
								RenderSingleTextOverlay(g, olItem, targetW, targetH);
							}
							else if (olItem.Type == OverlayItemType.Image)
							{
								RenderSingleImageOverlay(g, olItem, targetW, targetH);
							}
						}
						olBmp.Save(tempOlPng, ImageFormat.Png);
					}
				}

				List<string> inputs = new List<string>();
				Dictionary<string, int> srcMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

				foreach (var s in kept)
				{
					string sp = !string.IsNullOrEmpty(s.SourcePath) ? s.SourcePath : _cutEditSourcePath;
					if (!string.IsNullOrEmpty(sp) && !srcMap.ContainsKey(sp))
					{
						int idx = inputs.Count;
						srcMap[sp] = idx;
						if (IsImage(sp) || s.MediaType == "image")
						{
							inputs.Add($"-loop 1 -t 600 -i {QuoteArg(sp)}");
						}
						else
						{
							inputs.Add($"-i {QuoteArg(sp)}");
						}
					}
				}
				if (inputs.Count == 0 && !string.IsNullOrEmpty(_cutEditSourcePath))
				{
					srcMap[_cutEditSourcePath] = 0;
					inputs.Add($"-i {QuoteArg(_cutEditSourcePath)}");
				}

				var keptVideo = kept.Where(s => s.TrackId.StartsWith("V") || s.MediaType == "video" || s.MediaType == "image").OrderBy(s => s.TimelineStartSeconds).ToList();
				var keptAudio = kept.Where(s => s.TrackId.StartsWith("A") || s.MediaType == "audio").OrderBy(s => s.TimelineStartSeconds).ToList();
				if (keptVideo.Count == 0) keptVideo = kept;

				StringBuilder fg = new StringBuilder();
				bool outputAudio = !audioMuted && audioBitrateIndex != 3;
				float volFactor = (float)audioVolume / 100.0f;

				// 1. Process Video Clips with Transitions
				for (int i = 0; i < keptVideo.Count; i++)
				{
					var s = keptVideo[i];
					string sp = !string.IsNullOrEmpty(s.SourcePath) ? s.SourcePath : _cutEditSourcePath;
					int inIdx = (sp != null && srcMap.ContainsKey(sp)) ? srcMap[sp] : 0;
					bool isImg = (sp != null && IsImage(sp)) || s.MediaType == "image";
					string st = s.StartSeconds.ToString("0.000", CultureInfo.InvariantCulture);
					string et = s.EndSeconds.ToString("0.000", CultureInfo.InvariantCulture);
					string segDurStr = s.Duration.ToString("0.000", CultureInfo.InvariantCulture);

					string vFilter = isImg
						? $"trim=duration={segDurStr},setpts=PTS-STARTPTS,scale={targetW}:{targetH}:force_original_aspect_ratio=decrease,pad={targetW}:{targetH}:(ow-iw)/2:(oh-ih)/2:black,setsar=1"
						: $"trim=start={st}:end={et},setpts=PTS-STARTPTS,scale={targetW}:{targetH}:force_original_aspect_ratio=decrease,pad={targetW}:{targetH}:(ow-iw)/2:(oh-ih)/2:black,setsar=1";

					// Transition In (Fade in)
					if (!string.IsNullOrEmpty(s.TransitionInType) && s.TransitionInType != "none")
					{
						double inD = Math.Max(0.1, Math.Min(s.Duration / 2, s.TransitionInDuration));
						string inDStr = inD.ToString("0.00", CultureInfo.InvariantCulture);
						string c = (s.TransitionInType == "fadewhite") ? "white" : "black";
						vFilter += $",fade=t=in:st=0:d={inDStr}:c={c}";
					}
					// Transition Out (Fade out)
					if (!string.IsNullOrEmpty(s.TransitionOutType) && s.TransitionOutType != "none")
					{
						double outD = Math.Max(0.1, Math.Min(s.Duration / 2, s.TransitionOutDuration));
						double outSt = Math.Max(0.0, s.Duration - outD);
						string outDStr = outD.ToString("0.00", CultureInfo.InvariantCulture);
						string outStStr = outSt.ToString("0.00", CultureInfo.InvariantCulture);
						string c = (s.TransitionOutType == "fadewhite") ? "white" : "black";
						vFilter += $",fade=t=out:st={outStStr}:d={outDStr}:c={c}";
					}

					fg.Append($"[{inIdx}:v]{vFilter}[v{i}];");
				}

				string curV = "[v_concat]";
				if (keptVideo.Count > 1)
				{
					for (int i = 0; i < keptVideo.Count; i++) fg.Append($"[v{i}]");
					fg.Append($"concat=n={keptVideo.Count}:v=1:a=0[v_concat];");
				}
				else
				{
					curV = "[v0]";
				}

				// 2. Process Audio Streams (A1 Voice + A2 BGM multi-track mixing)
				string curA = "[a_concat]";
				if (outputAudio)
				{
					if (keptAudio.Count > 0)
					{
						var a1Clips = keptAudio.Where(s => s.TrackId == "A1" || !s.TrackId.StartsWith("A2")).ToList();
						var a2Clips = keptAudio.Where(s => s.TrackId == "A2").ToList();

						string a1Out = null;
						if (a1Clips.Count > 0)
						{
							for (int k = 0; k < a1Clips.Count; k++)
							{
								var s = a1Clips[k];
								string sp = !string.IsNullOrEmpty(s.SourcePath) ? s.SourcePath : _cutEditSourcePath;
								int inIdx = (sp != null && srcMap.ContainsKey(sp)) ? srcMap[sp] : 0;
								string st = s.StartSeconds.ToString("0.000", CultureInfo.InvariantCulture);
								string et = s.EndSeconds.ToString("0.000", CultureInfo.InvariantCulture);
								float clipVol = ((float)s.VolumePercent / 100.0f) * volFactor;
								string volFilter = (Math.Abs(clipVol - 1.0f) > 0.01f) ? $",volume={clipVol.ToString("0.00", CultureInfo.InvariantCulture)}" : "";
								fg.Append($"[{inIdx}:a]atrim=start={st}:end={et},asetpts=PTS-STARTPTS{volFilter}[a1_{k}];");
							}
							if (a1Clips.Count > 1)
							{
								for (int k = 0; k < a1Clips.Count; k++) fg.Append($"[a1_{k}]");
								fg.Append($"concat=n={a1Clips.Count}:v=0:a=1[a1_concat];");
								a1Out = "[a1_concat]";
							}
							else
							{
								a1Out = "[a1_0]";
							}
						}

						string a2Out = null;
						if (a2Clips.Count > 0)
						{
							for (int m = 0; m < a2Clips.Count; m++)
							{
								var s = a2Clips[m];
								string sp = !string.IsNullOrEmpty(s.SourcePath) ? s.SourcePath : _cutEditSourcePath;
								int inIdx = (sp != null && srcMap.ContainsKey(sp)) ? srcMap[sp] : 0;
								string st = s.StartSeconds.ToString("0.000", CultureInfo.InvariantCulture);
								string et = s.EndSeconds.ToString("0.000", CultureInfo.InvariantCulture);
								float bgmVol = ((float)_cutEditBgmVolume / 100.0f) * ((float)s.VolumePercent / 100.0f);
								string volFilter = $",volume={bgmVol.ToString("0.00", CultureInfo.InvariantCulture)}";
								fg.Append($"[{inIdx}:a]atrim=start={st}:end={et},asetpts=PTS-STARTPTS{volFilter}[a2_{m}];");
							}
							if (a2Clips.Count > 1)
							{
								for (int m = 0; m < a2Clips.Count; m++) fg.Append($"[a2_{m}]");
								fg.Append($"concat=n={a2Clips.Count}:v=0:a=1[a2_concat];");
								a2Out = "[a2_concat]";
							}
							else
							{
								a2Out = "[a2_0]";
							}
						}

						if (a1Out != null && a2Out != null)
						{
							fg.Append($"{a1Out}{a2Out}amix=inputs=2:duration=first:dropout_transition=2[a_mix];");
							curA = "[a_mix]";
						}
						else if (a1Out != null)
						{
							curA = a1Out;
						}
						else if (a2Out != null)
						{
							curA = a2Out;
						}
					}
					else
					{
						// Fallback: extract audio from keptVideo clips
						for (int i = 0; i < keptVideo.Count; i++)
						{
							var s = keptVideo[i];
							string sp = !string.IsNullOrEmpty(s.SourcePath) ? s.SourcePath : _cutEditSourcePath;
							int inIdx = (sp != null && srcMap.ContainsKey(sp)) ? srcMap[sp] : 0;
							bool isImg = (sp != null && IsImage(sp)) || s.MediaType == "image";
							string st = s.StartSeconds.ToString("0.000", CultureInfo.InvariantCulture);
							string et = s.EndSeconds.ToString("0.000", CultureInfo.InvariantCulture);
							string segDurStr = s.Duration.ToString("0.000", CultureInfo.InvariantCulture);

							if (isImg)
							{
								fg.Append($"aevalsrc=0:d={segDurStr}:s=44100[a{i}];");
							}
							else
							{
								string volFilter = (Math.Abs(volFactor - 1.0f) > 0.01f) ? $",volume={volFactor.ToString("0.00", CultureInfo.InvariantCulture)}" : "";
								fg.Append($"[{inIdx}:a]atrim=start={st}:end={et},asetpts=PTS-STARTPTS{volFilter}[a{i}];");
							}
						}
						if (keptVideo.Count > 1)
						{
							for (int i = 0; i < keptVideo.Count; i++) fg.Append($"[a{i}]");
							fg.Append($"concat=n={keptVideo.Count}:v=0:a=1[a_concat];");
							curA = "[a_concat]";
						}
						else
						{
							curA = "[a0]";
						}
					}
				}

				string finalV = curV;
				if (exportOverlays.Count > 0)
				{
					double timelineOffset = kept.Count > 0 ? kept.Min(s => s.TimelineStartSeconds) : 0.0;

					for (int oi = 0; oi < exportOverlays.Count; oi++)
					{
						var olItem = exportOverlays[oi];
						string tempOlPng = (oi < tempOverlayPngs.Count) ? tempOverlayPngs[oi] : null;
						if (string.IsNullOrEmpty(tempOlPng) || !File.Exists(tempOlPng)) continue;

						int olInIdx = inputs.Count;
						inputs.Add($"-loop 1 -i {QuoteArg(tempOlPng)}");
						double shiftedStart = Math.Max(0.0, olItem.StartSeconds - timelineOffset);
						double shiftedEnd = Math.Max(shiftedStart + 0.1, (olItem.StartSeconds + olItem.Duration) - timelineOffset);
						string startStr = shiftedStart.ToString("0.00", CultureInfo.InvariantCulture);
						string endStr = shiftedEnd.ToString("0.00", CultureInfo.InvariantCulture);

						fg.Append($"[{olInIdx}:v]format=rgba[ol_in_{oi}];");
						fg.Append($"{curV}[ol_in_{oi}]overlay=0:0:shortest=1:enable='between(t,{startStr},{endStr})'[v_ol_{oi}];");
						curV = $"[v_ol_{oi}]";
					}
					finalV = curV;
				}
				if (fg.Length > 0 && fg[fg.Length - 1] == ';')
				{
					fg.Remove(fg.Length - 1, 1);
				}

				int crf = 19;
				if (qualityIndex == 0) crf = 16;
				else if (qualityIndex == 1) crf = 19;
				else if (qualityIndex == 2) crf = 23;
				else if (qualityIndex == 3) crf = 28;

				string audioBitrate = "192k";
				if (audioBitrateIndex == 0) audioBitrate = "320k";
				else if (audioBitrateIndex == 1) audioBitrate = "192k";
				else if (audioBitrateIndex == 2) audioBitrate = "128k";

				StringBuilder args = new StringBuilder();
				args.Append("-hide_banner -y ");
				foreach (var inp in inputs) args.Append(inp).Append(" ");

				if (fg.Length > 0)
				{
					args.Append($"-filter_complex {QuoteArg(fg.ToString())} ");
					args.Append($"-map {QuoteArg(finalV)} ");
					if (outputAudio)
					{
						args.Append($"-map {QuoteArg(curA)} ");
					}
				}
				else
				{
					args.Append("-map 0:v ");
					if (outputAudio) args.Append("-map 0:a ");
				}

				args.Append($"-c:v libx264 -preset fast -crf {crf} -pix_fmt yuv420p ");

				if (fpsIndex == 1) args.Append("-r 60 ");
				else if (fpsIndex == 2) args.Append("-r 30 ");
				else if (fpsIndex == 3) args.Append("-r 24 ");

				if (outputAudio)
				{
					args.Append($"-c:a aac -b:a {audioBitrate} ");
				}
				else
				{
					args.Append("-an ");
				}

				if (string.Equals(ext, ".mp4", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".mov", StringComparison.OrdinalIgnoreCase))
				{
					args.Append("-movflags +faststart ");
				}

				args.Append("-progress pipe:1 -nostats ");
				args.Append(QuoteArg(targetOutPath));

				Invoke((MethodInvoker)delegate
				{
					_deliverProgressBar.Value = 30;
					_deliverStatusLabel.Text = "正在进行专业级多轨道编码与封装交付...";
				});

				double totalDur = kept.Sum(s => s.Duration);
				string lastError = "";
				int exitCode = RunFfmpeg(ffmpeg, args.ToString(), totalDur, p =>
				{
					BeginInvoke((MethodInvoker)delegate
					{
						_deliverProgressBar.Value = (int)Math.Max(0, Math.Min(100, p * 100));
					});
				}, out lastError);

				if (File.Exists(targetOutPath) && new FileInfo(targetOutPath).Length > 0)
				{
					_deliverLastExportPath = targetOutPath;
					FileInfo fi = new FileInfo(targetOutPath);
					double mb = fi.Length / 1024.0 / 1024.0;
					string timeStr = DateTime.Now.ToString("HH:mm:ss");

					BeginInvoke((MethodInvoker)delegate
					{
						_deliverProgressBar.Value = 100;
						_deliverStatusLabel.Text = $"🎉 交付成功！成片文件大小: {mb:0.0} MB，时长: {FormatDuration(totalDur)}";
						_deliverStartButton.Enabled = true;
						_deliverOpenFolderButton.Enabled = true;
						_deliverPlayOutputButton.Enabled = true;
						_deliverSendToSplitScreenButton.Enabled = true;
						_deliverSendToMergeButton.Enabled = true;
						_deliverSendBackToCutButton.Enabled = true;

						ListViewItem lvi = new ListViewItem(Path.GetFileName(targetOutPath));
						lvi.SubItems.Add($"{targetW}x{targetH}");
						lvi.SubItems.Add(FormatDuration(totalDur));
						lvi.SubItems.Add($"{mb:0.0} MB");
						lvi.SubItems.Add(timeStr);
						lvi.SubItems.Add(targetOutPath);
						_deliverHistoryList.Items.Insert(0, lvi);
						lvi.Selected = true;
						InitOrRefreshDeliverPreview();

						DialogResult dr = MessageBox.Show(this, $"交付成片已成功渲染！\n\n成片保存路径:\n{targetOutPath}\n\n文件大小: {mb:0.0} MB | 时长: {FormatDuration(totalDur)}\n\n是否立即打开所在文件夹查看？", "交付成片完成", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
						if (dr == DialogResult.Yes)
						{
							HighlightFileInExplorer(targetOutPath);
						}
					});
				}
				else
				{
					throw new Exception("FFmpeg 交付渲染未生成有效文件: " + lastError);
				}
			}
			catch (Exception ex)
			{
				BeginInvoke((MethodInvoker)delegate
				{
					_deliverProgressBar.Value = 0;
					_deliverStatusLabel.Text = "交付渲染失败: " + ex.Message;
					_deliverStartButton.Enabled = true;
					MessageBox.Show(this, "交付视频失败: " + ex.Message, "渲染错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				});
			}
			finally
			{
				TryDelete(tempTitlePng);
				if (tempOverlayPngs != null)
				{
					foreach (var f in tempOverlayPngs)
					{
						TryDelete(f);
					}
				}
			}
		});
	}

	private void SendCutVideoToSplitScreen(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
		AddSplitScreenVideoPaths(new string[] { path }, _splitScreenSelectedRegion);
		SwitchToWorkspace(4);
		MessageBox.Show(this, $"已将成片成功加载至【视频拼屏】工作台（区域 {_splitScreenSelectedRegion + 1}）！", "联动成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void SendCutVideoToMergeList(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
		if (!_videos.Contains(path))
		{
			_videos.Add(path);
			UpdateListView(null);
		}
		SwitchToWorkspace(0);
		MessageBox.Show(this, "已将成片成功添加至【批量合并】素材库！", "联动成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void SendLatestMergeToCutEditor()
	{
		string target = null;
		if (!string.IsNullOrEmpty(_latestMergeOutputFolder) && Directory.Exists(_latestMergeOutputFolder))
		{
			var files = Directory.GetFiles(_latestMergeOutputFolder, "*.mp4")
				.OrderByDescending(f => File.GetLastWriteTime(f)).ToList();
			if (files.Count > 0) target = files[0];
		}
		if (string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(_outputFolder.Text) && Directory.Exists(_outputFolder.Text))
		{
			var files = Directory.GetFiles(_outputFolder.Text, "*.mp4", SearchOption.AllDirectories)
				.OrderByDescending(f => File.GetLastWriteTime(f)).ToList();
			if (files.Count > 0) target = files[0];
		}
		if (string.IsNullOrEmpty(target))
		{
			MessageBox.Show(this, "尚未检测到已导出的合并成片，请先完成一次批量合并或直接在剪辑工作台中打开视频。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		LoadVideoIntoCutEditor(target, clearExisting: true);
		SwitchToWorkspace(5);
	}

	private void DeliverMediaElement_MediaOpened(object sender, System.Windows.RoutedEventArgs e)
	{
		BeginInvoke((MethodInvoker)delegate
		{
			if (_deliverIsPlaying && _deliverMediaElement != null)
			{
				try { _deliverMediaElement.Play(); } catch { }
			}
			UpdateDeliverTimeLabel();
		});
	}

	private void DeliverMediaElement_MediaEnded(object sender, System.Windows.RoutedEventArgs e)
	{
		BeginInvoke((MethodInvoker)delegate
		{
			PauseDeliverPlayback();
			_deliverCurrentPos = 0.0;
			UpdateDeliverTimeLabel();
			UpdateDeliverPreviewFrame(0.0);
		});
	}

	private void DeliverMediaElement_MediaFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
	{
		BeginInvoke((MethodInvoker)delegate
		{
			PauseDeliverPlayback();
			UpdateDeliverPreviewFrame(_deliverCurrentPos);
		});
	}

	internal void InitOrRefreshDeliverPreview(bool forceReload = false)
	{
		if (_deliverPreviewBox == null) return;

		var kept = _cutEditSegments?.Where(s => s.IsKept).OrderBy(s => s.TimelineStartSeconds).ToList() ?? new List<CutSegment>();
		bool hasContent = (kept.Count > 0) || (!string.IsNullOrEmpty(_cutEditSourcePath) && File.Exists(_cutEditSourcePath));

		if (!hasContent)
		{
			if (_deliverIsPlaying) PauseDeliverPlayback();
			if (_deliverEmptyPlaceholder != null) _deliverEmptyPlaceholder.Visible = true;
			if (_deliverPreviewBox != null) _deliverPreviewBox.Visible = false;
			if (_deliverElementHost != null) _deliverElementHost.Visible = false;
			if (_deliverPlayPauseButton != null) _deliverPlayPauseButton.Enabled = false;
			if (_deliverTimeScrubber != null) _deliverTimeScrubber.Enabled = false;
			if (_deliverTimeLabel != null) _deliverTimeLabel.Text = "00:00.0 / 00:00.0";
			return;
		}

		if (_deliverEmptyPlaceholder != null) _deliverEmptyPlaceholder.Visible = false;
		if (_deliverPlayPauseButton != null) _deliverPlayPauseButton.Enabled = true;
		if (_deliverTimeScrubber != null) _deliverTimeScrubber.Enabled = true;

		_deliverDuration = kept.Count > 0 ? kept.Sum(s => s.Duration) : _cutEditDuration;
		if (_deliverDuration <= 0.05 && !string.IsNullOrEmpty(_cutEditSourcePath) && File.Exists(_cutEditSourcePath))
		{
			_deliverDuration = GetMediaDuration(_cutEditSourcePath);
		}
		if (forceReload || _deliverCurrentPos > _deliverDuration)
		{
			_deliverCurrentPos = 0.0;
		}

		UpdateDeliverTimeLabel();
		if (!_deliverIsPlaying)
		{
			UpdateDeliverPreviewFrame(_deliverCurrentPos);
		}
	}

	internal void ToggleDeliverPlayPause()
	{
		if (_deliverIsPlaying)
		{
			PauseDeliverPlayback();
		}
		else
		{
			StartDeliverPlayback();
		}
	}

	private void StartDeliverPlayback()
	{
		if (_deliverDuration <= 0.05) return;
		if (_deliverCurrentPos >= _deliverDuration - 0.05)
		{
			_deliverCurrentPos = 0.0;
		}
		_deliverIsPlaying = true;
		if (_deliverPlayPauseButton != null) _deliverPlayPauseButton.Text = "⏸ 暂停 (空格)";
		if (_deliverPopoutForm != null && !_deliverPopoutForm.IsDisposed)
		{
			_deliverPopoutForm.UpdateTimeAndScrubber(_deliverCurrentPos, _deliverDuration, true);
		}
		_deliverPlaybackStartPos = _deliverCurrentPos;
		_deliverPlaybackSw.Restart();

		if (_deliverPlayTimer == null)
		{
			_deliverPlayTimer = new System.Windows.Forms.Timer { Interval = 33 };
			_deliverPlayTimer.Tick += DeliverPlayTimer_Tick;
		}
		_deliverPlayTimer.Start();
		SyncDeliverPlaybackSegment();
	}

	private void PauseDeliverPlayback()
	{
		_deliverIsPlaying = false;
		_deliverPlaybackSw.Stop();
		_deliverPlayTimer?.Stop();
		if (_deliverPlayPauseButton != null) _deliverPlayPauseButton.Text = "▶ 播放 (空格)";
		if (_deliverPopoutForm != null && !_deliverPopoutForm.IsDisposed)
		{
			_deliverPopoutForm.UpdateTimeAndScrubber(_deliverCurrentPos, _deliverDuration, false);
		}
		try { _deliverMediaElement?.Pause(); } catch { }
		if (_deliverElementHost != null)
		{
			_deliverElementHost.Visible = false;
		}
		if (_deliverPreviewBox != null)
		{
			_deliverPreviewBox.Visible = true;
			_deliverPreviewBox.BringToFront();
		}
		if (_deliverPopoutForm != null && !_deliverPopoutForm.IsDisposed && _deliverPopoutForm.PreviewBox != null)
		{
			_deliverPopoutForm.PreviewBox.Visible = true;
			_deliverPopoutForm.PreviewBox.BringToFront();
		}
		UpdateDeliverPreviewFrame(_deliverCurrentPos);
	}

	private void DeliverPlayTimer_Tick(object sender, EventArgs e)
	{
		if (!_deliverIsPlaying) return;
		double elapsed = _deliverPlaybackSw.Elapsed.TotalSeconds;
		_deliverCurrentPos = _deliverPlaybackStartPos + elapsed;
		if (_deliverCurrentPos >= _deliverDuration)
		{
			_deliverCurrentPos = _deliverDuration;
			PauseDeliverPlayback();
			UpdateDeliverTimeLabel();
			return;
		}
		UpdateDeliverTimeLabel();
		UpdateDeliverWpfOverlay(_deliverCurrentPos);
		SyncDeliverPlaybackSegment();
	}

	private void UpdateDeliverTimeLabel()
	{
		if (_deliverTimeLabel != null)
		{
			_deliverTimeLabel.Text = $"{FormatDuration(_deliverCurrentPos)} / {FormatDuration(_deliverDuration)}";
		}
		if (_deliverTimeScrubber != null && _deliverDuration > 0)
		{
			int val = (int)Math.Max(0, Math.Min(1000, (_deliverCurrentPos / _deliverDuration * 1000.0)));
			_deliverTimeScrubber.Value = val;
		}
		if (_deliverPopoutForm != null && !_deliverPopoutForm.IsDisposed)
		{
			_deliverPopoutForm.UpdateTimeAndScrubber(_deliverCurrentPos, _deliverDuration, _deliverIsPlaying);
		}
	}

	private void DeliverTimeScrubber_Scroll(object sender, EventArgs e)
	{
		if (_deliverDuration <= 0.05) return;
		_deliverCurrentPos = (_deliverTimeScrubber.Value / 1000.0) * _deliverDuration;
		_deliverPlaybackStartPos = _deliverCurrentPos;
		_deliverPlaybackSw.Restart();
		UpdateDeliverTimeLabel();
		UpdateDeliverWpfOverlay(_deliverCurrentPos);
		if (_deliverIsPlaying)
		{
			SyncDeliverPlaybackSegment();
		}
		else
		{
			UpdateDeliverPreviewFrame(_deliverCurrentPos);
		}
	}

	private void DeliverTimeScrubber_MouseDown(object sender, MouseEventArgs e)
	{
		_deliverScrubberWasPlaying = _deliverIsPlaying;
		if (_deliverIsPlaying)
		{
			try { _deliverMediaElement?.Pause(); } catch { }
			_deliverPlaybackSw.Stop();
			_deliverPlayTimer?.Stop();
		}
	}

	private void DeliverTimeScrubber_MouseUp(object sender, MouseEventArgs e)
	{
		if (_deliverScrubberWasPlaying)
		{
			StartDeliverPlayback();
		}
	}

	private void SyncDeliverPlaybackSegment()
	{
		var kept = _cutEditSegments?.Where(s => s.IsKept).OrderBy(s => s.TimelineStartSeconds).ToList() ?? new List<CutSegment>();
		CutSegment activeSeg = null;
		double tAccum = 0.0;
		double segOffset = 0.0;

		foreach (var s in kept)
		{
			if (_deliverCurrentPos >= tAccum && _deliverCurrentPos < tAccum + s.Duration)
			{
				activeSeg = s;
				segOffset = _deliverCurrentPos - tAccum;
				break;
			}
			tAccum += s.Duration;
		}
		if (activeSeg == null && kept.Count > 0)
		{
			activeSeg = kept.Last();
			segOffset = activeSeg.Duration;
		}

		string sp = (activeSeg != null && !string.IsNullOrEmpty(activeSeg.SourcePath)) ? activeSeg.SourcePath : _cutEditSourcePath;

		if (activeSeg != null && (activeSeg.MediaType == "image" || (sp != null && IsImage(sp))))
		{
			try { _deliverMediaElement?.Pause(); } catch { }
			UpdateDeliverPreviewFrame(_deliverCurrentPos);
			return;
		}

		if (!string.IsNullOrEmpty(sp) && File.Exists(sp) && _deliverMediaElement != null)
		{
			try
			{
				Uri uri = new Uri(sp);
				if (_deliverMediaElement.Source == null || !_deliverMediaElement.Source.Equals(uri))
				{
					_deliverMediaElement.Source = uri;
				}
				double inSrcSec = (activeSeg != null ? activeSeg.StartSeconds : 0.0) + segOffset;
				TimeSpan targetPos = TimeSpan.FromSeconds(Math.Max(0.0, inSrcSec));
				if (Math.Abs((_deliverMediaElement.Position - targetPos).TotalSeconds) > 0.35)
				{
					_deliverMediaElement.Position = targetPos;
				}
				_deliverMediaElement.Volume = _cutEditAudioMuted ? 0.0 : (_cutEditAudioVolume / 100.0);
				_deliverMediaElement.IsMuted = _cutEditAudioMuted;

				if (_deliverElementHost != null && !_deliverElementHost.Visible)
				{
					if (_deliverPreviewBox != null) _deliverPreviewBox.Visible = false;
					if (_deliverPopoutForm != null && !_deliverPopoutForm.IsDisposed && _deliverPopoutForm.PreviewBox != null)
					{
						_deliverPopoutForm.PreviewBox.Visible = false;
					}
					_deliverElementHost.Visible = true;
					_deliverElementHost.BringToFront();
				}
				_deliverMediaElement.Play();
			}
			catch { }
		}
		else
		{
			UpdateDeliverPreviewFrame(_deliverCurrentPos);
		}
	}

	private void UpdateDeliverPreviewFrame(double timeSec)
	{
		if (_deliverPreviewBox == null) return;

		var kept = _cutEditSegments?.Where(s => s.IsKept).OrderBy(s => s.TimelineStartSeconds).ToList() ?? new List<CutSegment>();
		CutSegment activeSeg = null;
		double tAccum = 0.0;
		double segOffset = 0.0;

		foreach (var s in kept)
		{
			if (timeSec >= tAccum && timeSec < tAccum + s.Duration)
			{
				activeSeg = s;
				segOffset = timeSec - tAccum;
				break;
			}
			tAccum += s.Duration;
		}
		if (activeSeg == null && kept.Count > 0)
		{
			activeSeg = kept.Last();
			segOffset = activeSeg.Duration;
		}

		string sp = (activeSeg != null && !string.IsNullOrEmpty(activeSeg.SourcePath)) ? activeSeg.SourcePath : _cutEditSourcePath;
		if (string.IsNullOrEmpty(sp) || !File.Exists(sp)) return;

		if (_deliverElementHost != null && _deliverElementHost.Visible)
		{
			_deliverElementHost.Visible = false;
		}
		if (_deliverPreviewBox != null)
		{
			_deliverPreviewBox.Visible = true;
			_deliverPreviewBox.BringToFront();
		}

		bool isImg = (activeSeg != null && activeSeg.MediaType == "image") || IsImage(sp);
		if (isImg)
		{
			try
			{
				byte[] bytes = File.ReadAllBytes(sp);
				using (MemoryStream ms = new MemoryStream(bytes))
				using (Bitmap origBmp = new Bitmap(ms))
				{
					Bitmap bmp = new Bitmap(origBmp);
					RenderAllOverlaysOnBitmap(bmp, timeSec, forcePreviewSelected: false);
					RenderTransitionOnBitmap(bmp, timeSec);
					var old = _deliverPreviewBox.Image;
					_deliverPreviewBox.Image = bmp;
					old?.Dispose();
				}
			}
			catch { }
			return;
		}

		string ffmpeg = FindFfmpeg();
		if (string.IsNullOrEmpty(ffmpeg)) return;

		double inSourceSec = (activeSeg != null ? activeSeg.StartSeconds : 0.0) + segOffset;

		// FAST IN-MEMORY COMPOSITING:
		bool hasCachedBase = false;
		Bitmap cachedBaseClone = null;
		lock (_deliverBaseFrameLock)
		{
			if (_deliverCachedBaseFrame != null &&
				string.Equals(_deliverCachedBaseFramePath, sp, StringComparison.OrdinalIgnoreCase) &&
				Math.Abs(_deliverCachedBaseFrameTime - inSourceSec) < 0.08)
			{
				hasCachedBase = true;
				cachedBaseClone = new Bitmap(_deliverCachedBaseFrame);
			}
		}

		if (hasCachedBase && cachedBaseClone != null)
		{
			using (cachedBaseClone)
			{
				Bitmap fastBmp = new Bitmap(cachedBaseClone);
				RenderAllOverlaysOnBitmap(fastBmp, timeSec, forcePreviewSelected: false);
				RenderTransitionOnBitmap(fastBmp, timeSec);
				var old = _deliverPreviewBox.Image;
				_deliverPreviewBox.Image = fastBmp;
				_deliverPreviewBox.Visible = true;
				_deliverPreviewBox.BringToFront();
				old?.Dispose();

				if (_deliverPopoutForm != null && !_deliverPopoutForm.IsDisposed)
				{
					_deliverPopoutForm.UpdateFrame(fastBmp, _deliverCurrentPos, _deliverDuration, _deliverIsPlaying);
				}
			}
			return;
		}

		int seq = Interlocked.Increment(ref _deliverPreviewSeq);

		ThreadPool.QueueUserWorkItem(delegate
		{
			string tempJpg = null;
			try
			{
				if (seq != _deliverPreviewSeq) return;
				tempJpg = Path.Combine(Path.GetTempPath(), "Deliver_Preview_" + Guid.NewGuid().ToString("N") + ".jpg");
				string tStr = Math.Max(0.0, inSourceSec).ToString("0.00", CultureInfo.InvariantCulture);
				string args = $"-hide_banner -y -ss {tStr} -noaccurate_seek -i {QuoteArg(sp)} -frames:v 1 -q:v 2 {QuoteArg(tempJpg)}";

				ProcessStartInfo psi = NewProcessInfo(ffmpeg, args);
				using (Process proc = new Process())
				{
					proc.StartInfo = psi;
					proc.Start();
					proc.StandardOutput.ReadToEnd();
					proc.StandardError.ReadToEnd();
					proc.WaitForExit(3500);
				}

				if (seq != _deliverPreviewSeq) return;

				if (File.Exists(tempJpg))
				{
					byte[] bytes = File.ReadAllBytes(tempJpg);
					using (MemoryStream ms = new MemoryStream(bytes))
					using (Bitmap origBmp = new Bitmap(ms))
					{
						lock (_deliverBaseFrameLock)
						{
							_deliverCachedBaseFrame?.Dispose();
							_deliverCachedBaseFrame = new Bitmap(origBmp);
							_deliverCachedBaseFramePath = sp;
							_deliverCachedBaseFrameTime = inSourceSec;
						}

						Bitmap frameBmp = new Bitmap(origBmp);
						RenderAllOverlaysOnBitmap(frameBmp, timeSec, forcePreviewSelected: false);
						RenderTransitionOnBitmap(frameBmp, timeSec);

						if (seq == _deliverPreviewSeq)
						{
							BeginInvoke((MethodInvoker)delegate
							{
								if (seq == _deliverPreviewSeq && _deliverPreviewBox != null)
								{
									var old = _deliverPreviewBox.Image;
									_deliverPreviewBox.Image = frameBmp;
									old?.Dispose();

									if (_deliverPopoutForm != null && !_deliverPopoutForm.IsDisposed)
									{
										_deliverPopoutForm.UpdateFrame(frameBmp, _deliverCurrentPos, _deliverDuration, _deliverIsPlaying);
									}
								}
								else
								{
									frameBmp.Dispose();
								}
							});
						}
						else
						{
							frameBmp.Dispose();
						}
					}
				}
			}
			catch { }
			finally
			{
				TryDelete(tempJpg);
			}
		});
	}

	private async void SendLatestSplitScreenToCutEditor()
	{
		// Direct background synthesis of current preview configuration
		string ffmpeg = FindFfmpeg();
		if (string.IsNullOrWhiteSpace(ffmpeg))
		{
			MessageBox.Show(this, "未找到 FFmpeg，无法合成拼屏成片直通剪辑。", "缺少 FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Hand);
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
					MessageBox.Show(this, "区域 " + (j + 1) + " 选中的视频素材不存在，请先添加有效素材后再发送至剪辑。", "素材缺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}
				sources[j] = p;
			}
		}

		double renderSec = plan.DurationSeconds;
		if (plan.FollowMainDuration && plan.MainRegion >= 0 && plan.MainRegion < sources.Length && !string.IsNullOrWhiteSpace(sources[plan.MainRegion]))
		{
			VideoInfo vi = Probe(ffmpeg, sources[plan.MainRegion]);
			if (vi.DurationSeconds > 0.05) renderSec = vi.DurationSeconds;
		}

		SplitScreenRenderPlan directPlan = CloneSplitScreenRenderPlan(plan);
		directPlan.DurationSeconds = Math.Max(1.0, renderSec);
		for (int m = 0; m < sources.Length; m++)
		{
			directPlan.VideoViewSettings[m] = (string.IsNullOrWhiteSpace(sources[m]) ? new SplitScreenVideoViewSettings() : GetSplitScreenVideoViewSettings(m, sources[m]).Clone());
		}

		WatermarkProfile splitScreenWatermark = CaptureWatermarkProfile(_watermarkOnSplitScreen.Checked);
		BgmPlan bgmPlan = CaptureBgmPlan(_splitScreenBgm);
		SyncSplitScreenTitlePlanFromUi();
		MergeTitlePlan splitScreenTitle = _splitScreenTitlePlan?.Clone();

		string tempDir = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_DirectCut");
		try { Directory.CreateDirectory(tempDir); } catch { }
		string tempComposite = Path.Combine(tempDir, "SplitProject_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4");

		_splitScreenSendToCutBtn.Enabled = false;
		string origBtnText = _splitScreenSendToCutBtn.Text;
		_splitScreenSendToCutBtn.Text = "⏳ 正在直接合成拼屏画面并发送至剪辑...";
		_splitScreenStatusLabel.Text = "正在后台合成当前拼屏组合并载入剪辑工作台...";

		bool ok = false;
		string error = null;

		try
		{
			await Task.Run(() =>
			{
				ok = RenderSplitScreenOutput(ffmpeg, directPlan, normalizedRects, sources, tempComposite, delegate { }, out error, isFastPreview: false);
				if (ok && File.Exists(tempComposite))
				{
					if (splitScreenTitle != null && splitScreenTitle.Enabled)
					{
						ApplyTitleToSplitScreenOutput(ffmpeg, tempComposite, splitScreenTitle, sources, 0, directPlan.DurationSeconds, delegate { }, out _);
					}
					if (splitScreenWatermark.Enabled)
					{
						ApplyWatermarkToSplitScreenOutput(ffmpeg, tempComposite, splitScreenWatermark, directPlan.DurationSeconds, delegate { }, out _);
					}
					if (bgmPlan.Enabled && bgmPlan.Files != null && bgmPlan.Files.Count > 0)
					{
						string bgmPath = SelectBgm(bgmPlan, 0, new Random());
						if (File.Exists(bgmPath))
						{
							ApplyBackgroundMusic(ffmpeg, tempComposite, bgmPath, bgmPlan.VolumePercent, delegate { }, out _);
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
			_splitScreenSendToCutBtn.Enabled = true;
			_splitScreenSendToCutBtn.Text = origBtnText;
		}

		if (ok && File.Exists(tempComposite))
		{
			_splitScreenStatusLabel.Text = "✅ 拼屏画面合成完毕，已直接载入视频剪辑工作台！";
			LoadVideoIntoCutEditor(tempComposite, clearExisting: true);
			SwitchToWorkspace(5);
		}
		else
		{
			_splitScreenStatusLabel.Text = "❌ 拼屏直通剪辑合成失败：" + error;
			MessageBox.Show(this, "拼屏合成失败：\n" + error, "直通剪辑异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private static void HighlightFileInExplorer(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				ProcessStartInfo psi = new ProcessStartInfo("explorer.exe", "/select," + QuoteArg(path))
				{
					UseShellExecute = true
				};
				Process.Start(psi);
			}
		}
		catch { }
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
		SplitContainer splitScreenMainSplitter = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			SplitterWidth = 7,
			BackColor = Color.FromArgb(16, 20, 28)
		};
		StyleTechSplitter(splitScreenMainSplitter);

		SplitContainer splitScreenLeftSplitter = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Vertical,
			SplitterWidth = 7,
			BackColor = Color.FromArgb(16, 20, 28)
		};
		StyleTechSplitter(splitScreenLeftSplitter);

		SplitContainer splitScreenCenterRightSplitter = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Vertical,
			SplitterWidth = 7,
			BackColor = Color.FromArgb(16, 20, 28)
		};
		StyleTechSplitter(splitScreenCenterRightSplitter);

		splitScreenLeftSplitter.Panel2.Controls.Add(splitScreenCenterRightSplitter);
		splitScreenMainSplitter.Panel1.Controls.Add(splitScreenLeftSplitter);

		Panel splitScreenBottomScrollHost = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = SurfaceColor,
			Padding = new Padding(0)
		};
		splitScreenMainSplitter.Panel2.Controls.Add(splitScreenBottomScrollHost);
		tabPage2.Controls.Add(splitScreenMainSplitter);
		GroupBox groupBox = new GroupBox();
		groupBox.Text = "① 选择拼屏模板";
		groupBox.Dock = DockStyle.Fill;
		groupBox.Margin = new Padding(0, 0, 7, 0);
		groupBox.Padding = new Padding(8);
		groupBox.BackColor = SurfaceColor;
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
		splitScreenLeftSplitter.Panel1.Controls.Add(groupBox2);
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.Margin = new Padding(0, 0, 7, 0);
		panel.Padding = new Padding(10);
		panel.BackColor = SurfaceColor;
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
			BackColor = Color.FromArgb(20, 24, 34)
		};
		_splitScreenPreviewHost = previewSurface;
		_splitScreenPreview = new SplitScreenPreviewBox
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(20, 24, 34),
			SizeMode = PictureBoxSizeMode.Normal,
			AllowDrop = true,
			Cursor = Cursors.Hand
		};
		_splitScreenPreview.Resize += delegate
		{
			RefreshSplitScreenPreview();
		};
		previewSurface.Controls.Add(_splitScreenPreview);
		Panel splitScreenPreviewBar = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 38,
			BackColor = SurfaceColor,
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
		splitScreenMainSplitter.SplitterMoved += delegate
		{
			LayoutSplitScreenPreview(previewSurface);
		};
		splitScreenLeftSplitter.SplitterMoved += delegate
		{
			LayoutSplitScreenPreview(previewSurface);
		};
		splitScreenCenterRightSplitter.SplitterMoved += delegate
		{
			LayoutSplitScreenPreview(previewSurface);
		};
		tabPage2.Resize += delegate
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
		splitScreenCenterRightSplitter.Panel1.Controls.Add(panel2);
		GroupBox groupBox3 = new GroupBox();
		groupBox3.Text = "③ 给编号区域添加视频";
		groupBox3.Dock = DockStyle.Fill;
		groupBox3.Margin = new Padding(0);
		groupBox3.Padding = new Padding(10);
		groupBox3.BackColor = SurfaceColor;
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
		panel3.BackColor = SurfaceColor;
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
		splitScreenCenterRightSplitter.Panel2.Controls.Add(groupBox4);
		Panel settings = new Panel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(8, 3, 8, 7),
			BackColor = SurfaceColor
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
		_splitScreenTitleEnabled = new CheckBox
		{
			Text = "🎬 拼屏标题与隔断花字",
			Location = new Point(18, 166),
			AutoSize = true,
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
			ForeColor = AccentColor
		};
		settings.Controls.Add(_splitScreenTitleEnabled);

		settings.Controls.Add(MakeLabel("风格", 212, 169));
		_splitScreenTitleStyleCombo = new ComboBox
		{
			Location = new Point(248, 165),
			Width = 175,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		foreach (var s in MergeTitleStyleCatalog.Styles)
		{
			_splitScreenTitleStyleCombo.Items.Add(s.Name);
		}
		_splitScreenTitleStyleCombo.SelectedIndex = 0;
		settings.Controls.Add(_splitScreenTitleStyleCombo);

		settings.Controls.Add(MakeLabel("位置", 432, 169));
		_splitScreenTitlePositionCombo = new ComboBox
		{
			Location = new Point(468, 165),
			Width = 150,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_splitScreenTitlePositionCombo.Items.AddRange(new object[]
		{
			"中间分割线 (隔断花字)",
			"顶部居中 (大标题)",
			"居中偏上",
			"居中 (画面正中央)",
			"居中偏下",
			"底部居中"
		});
		_splitScreenTitlePositionCombo.SelectedIndex = 0;
		settings.Controls.Add(_splitScreenTitlePositionCombo);

		settings.Controls.Add(MakeLabel("主标题", 628, 169));
		_splitScreenTitleTextBox = new TextBox
		{
			Location = new Point(676, 165),
			Width = 145,
			Text = "{文件名}"
		};
		settings.Controls.Add(_splitScreenTitleTextBox);

		_splitScreenTitleStyleBtn = MakeButton("🎨 样式与微调…", 112);
		_splitScreenTitleStyleBtn.Location = new Point(830, 162);
		settings.Controls.Add(_splitScreenTitleStyleBtn);

		settings.Controls.Add(MakeLabel("呈现", 950, 169));
		_splitScreenTitleDurationNum = new NumericUpDown
		{
			Location = new Point(984, 165),
			Width = 56,
			Minimum = 0m,
			Maximum = 3600m,
			Value = 0m,
			DecimalPlaces = 1
		};
		settings.Controls.Add(_splitScreenTitleDurationNum);
		settings.Controls.Add(MakeLabel("秒(0为全程)", 1044, 169));

		_splitScreenTitleEnabled.CheckedChanged += delegate
		{
			SyncSplitScreenTitlePlanFromUi();
			SaveUserSettings();
			RefreshSplitScreenPreview();
		};
		_splitScreenTitleStyleCombo.SelectedIndexChanged += delegate
		{
			SyncSplitScreenTitlePlanFromUi();
			SaveUserSettings();
			RefreshSplitScreenPreview();
		};
		_splitScreenTitlePositionCombo.SelectedIndexChanged += delegate
		{
			SyncSplitScreenTitlePlanFromUi();
			SaveUserSettings();
			RefreshSplitScreenPreview();
		};
		_splitScreenTitleTextBox.TextChanged += delegate
		{
			SyncSplitScreenTitlePlanFromUi();
			SaveUserSettings();
			RefreshSplitScreenPreview();
		};
		_splitScreenTitleDurationNum.ValueChanged += delegate
		{
			SyncSplitScreenTitlePlanFromUi();
			SaveUserSettings();
		};
		_splitScreenTitleStyleBtn.Click += delegate
		{
			string sampleText = _splitScreenTitleTextBox?.Text?.Trim();
			if (string.IsNullOrEmpty(sampleText) || sampleText == "{文件名}")
			{
				string firstFile = _splitScreenRegionVideos.Where(x => x != null && x.Count > 0).Select(x => x[0]).FirstOrDefault();
				sampleText = !string.IsNullOrEmpty(firstFile) ? MergeTitleStyleCatalog.CleanFilenameForTitle(firstFile) : "🔥 热门拼屏对比标题示例";
			}
			using (var dlg = new MergeTitleStyleDialog(_splitScreenTitlePlan, sampleText, _isDarkMode))
			{
				if (dlg.ShowDialog(this) == DialogResult.OK)
				{
					SyncSplitScreenTitleUiFromPlan();
					SaveUserSettings();
					RefreshSplitScreenPreview();
				}
			}
		};

		_splitScreenBgm = InstallBgmControls(settings, 18, 204);
		_splitScreenBgmList = new ListBox
		{
			Location = new Point(18, 240),
			Size = new Size(740, 40),
			IntegralHeight = false,
			HorizontalScrollbar = true,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		settings.Controls.Add(_splitScreenBgmList);
		settings.Controls.Add(MakeLabel("音乐池（可多首）", 770, 250));
		settings.Controls.Add(MakeLabel("总输出目录", 18, 292));
		_splitScreenOutputFolder = new TextBox
		{
			Location = new Point(106, 288),
			Width = 710,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		settings.Controls.Add(_splitScreenOutputFolder);
		_splitScreenOutputBrowseButton = MakeButton("选择…", 72);
		_splitScreenOutputBrowseButton.Location = new Point(828, 285);
		_splitScreenOutputBrowseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		settings.Controls.Add(_splitScreenOutputBrowseButton);
		_splitScreenOutputOpenButton = MakeButton("打开", 64);
		_splitScreenOutputOpenButton.Location = new Point(908, 285);
		_splitScreenOutputOpenButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		settings.Controls.Add(_splitScreenOutputOpenButton);
		_splitScreenProgressBar = new ProgressBar
		{
			Location = new Point(18, 325),
			Height = 15,
			Width = 954,
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
		};
		settings.Controls.Add(_splitScreenProgressBar);
		_splitScreenStatusLabel = new Label
		{
			Location = new Point(18, 345),
			Size = new Size(954, 22),
			Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
			ForeColor = MutedColor,
			AutoEllipsis = true,
			Text = "先选模板，再给每个编号区域添加至少一个视频。"
		};
		settings.Controls.Add(_splitScreenStatusLabel);
		_splitScreenStartButton = MakePrimaryButton("开始视频拼屏", 18, 370, 150);
		settings.Controls.Add(_splitScreenStartButton);
		_splitScreenCancelButton = MakeButton("取消", 88);
		_splitScreenCancelButton.Location = new Point(180, 370);
		_splitScreenCancelButton.Height = 42;
		_splitScreenCancelButton.Enabled = false;
		settings.Controls.Add(_splitScreenCancelButton);
		_splitScreenSendToCutBtn = MakeButton("🎬 发送最新成品至剪辑", 175);
		_splitScreenSendToCutBtn.Location = new Point(280, 370);
		_splitScreenSendToCutBtn.Height = 42;
		_splitScreenSendToCutBtn.Tag = "accent";
		_splitScreenSendToCutBtn.Click += delegate
		{
			SendLatestSplitScreenToCutEditor();
		};
		settings.Controls.Add(_splitScreenSendToCutBtn);
		Label label4 = MakeLabel("主区域模式会逐条完整导出主视频；其他区域自动顺序或随机循环配合。", 468, 384);
		label4.ForeColor = MutedColor;
		settings.Controls.Add(label4);
		settings.Resize += delegate
		{
			LayoutSplitScreenOutputPanel(settings);
		};
		settings.Dock = DockStyle.Top;
		settings.Height = 430;
		splitScreenBottomScrollHost.Controls.Add(settings);
		tabPage2.HandleCreated += delegate
		{
			BeginInvoke((Action)delegate
			{
				try
				{
					if (splitScreenLeftSplitter.ClientSize.Width > 580)
					{
						splitScreenLeftSplitter.Panel1MinSize = 120;
						splitScreenLeftSplitter.Panel2MinSize = 200;
						int targetDist = 310;
						if (targetDist >= splitScreenLeftSplitter.Panel1MinSize &&
						    targetDist <= splitScreenLeftSplitter.ClientSize.Width - splitScreenLeftSplitter.Panel2MinSize)
						{
							splitScreenLeftSplitter.SplitterDistance = targetDist;
						}
					}
					if (splitScreenCenterRightSplitter.ClientSize.Width > 500)
					{
						splitScreenCenterRightSplitter.Panel1MinSize = 180;
						splitScreenCenterRightSplitter.Panel2MinSize = 200;
						int targetDist = Math.Max(200, splitScreenCenterRightSplitter.ClientSize.Width - 380);
						if (targetDist >= splitScreenCenterRightSplitter.Panel1MinSize &&
						    targetDist <= splitScreenCenterRightSplitter.ClientSize.Width - splitScreenCenterRightSplitter.Panel2MinSize)
						{
							splitScreenCenterRightSplitter.SplitterDistance = targetDist;
						}
					}
					if (splitScreenMainSplitter.ClientSize.Height > 420)
					{
						splitScreenMainSplitter.Panel1MinSize = 120;
						splitScreenMainSplitter.Panel2MinSize = 140;
						int targetDist = Math.Max(180, splitScreenMainSplitter.ClientSize.Height - 435);
						if (targetDist >= splitScreenMainSplitter.Panel1MinSize &&
						    targetDist <= splitScreenMainSplitter.ClientSize.Height - splitScreenMainSplitter.Panel2MinSize)
						{
							splitScreenMainSplitter.SplitterDistance = targetDist;
						}
					}
					LayoutSplitScreenPreview(previewSurface);
				}
				catch { }
			});
		};
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
		tabPage.BackColor = SurfaceColor;
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
		listView.BackColor = SurfaceColor;
		listView.ForeColor = InkColor;
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
		Color thumbBg = _isDarkMode ? Color.FromArgb(19, 32, 55) : Color.FromArgb(246, 248, 252);
		Color boxBg = _isDarkMode ? Color.FromArgb(13, 24, 41) : Color.FromArgb(30, 41, 59);
		graphics.Clear(thumbBg);
		RectangleF rectangleF = FitRectangle(new Size(9, 16), new RectangleF(8f, 5f, size.Width - 16, size.Height - 10));
		using (Brush brush = new SolidBrush(boxBg))
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

	private void SyncSplitScreenTitlePlanFromUi()
	{
		if (_splitScreenTitlePlan == null) _splitScreenTitlePlan = new MergeTitlePlan { Position = "中间分割线", DurationSeconds = 0.0 };
		_splitScreenTitlePlan.Enabled = _splitScreenTitleEnabled?.Checked ?? false;
		if (_splitScreenTitleStyleCombo != null && _splitScreenTitleStyleCombo.SelectedIndex >= 0 && _splitScreenTitleStyleCombo.SelectedIndex < MergeTitleStyleCatalog.Styles.Count)
		{
			_splitScreenTitlePlan.StyleId = MergeTitleStyleCatalog.Styles[_splitScreenTitleStyleCombo.SelectedIndex].Id;
		}
		if (_splitScreenTitlePositionCombo != null && _splitScreenTitlePositionCombo.SelectedIndex >= 0)
		{
			_splitScreenTitlePlan.Position = _splitScreenTitlePositionCombo.SelectedIndex switch
			{
				0 => "中间分割线",
				1 => "顶部居中",
				2 => "居中偏上",
				3 => "居中",
				4 => "居中偏下",
				5 => "底部居中",
				_ => "中间分割线"
			};
		}
		_splitScreenTitlePlan.MainTitleTemplate = _splitScreenTitleTextBox?.Text ?? "{文件名}";
		_splitScreenTitlePlan.DurationSeconds = (double)(_splitScreenTitleDurationNum?.Value ?? 0m);

		bool en = _splitScreenTitlePlan.Enabled;
		if (_splitScreenTitleStyleCombo != null) _splitScreenTitleStyleCombo.Enabled = en;
		if (_splitScreenTitlePositionCombo != null) _splitScreenTitlePositionCombo.Enabled = en;
		if (_splitScreenTitleTextBox != null) _splitScreenTitleTextBox.Enabled = en;
		if (_splitScreenTitleDurationNum != null) _splitScreenTitleDurationNum.Enabled = en;
		if (_splitScreenTitleStyleBtn != null) _splitScreenTitleStyleBtn.Enabled = en;
	}

	private void SyncSplitScreenTitleUiFromPlan()
	{
		if (_splitScreenTitlePlan == null) return;
		if (_splitScreenTitleEnabled != null) _splitScreenTitleEnabled.Checked = _splitScreenTitlePlan.Enabled;
		if (_splitScreenTitleStyleCombo != null)
		{
			int idx = MergeTitleStyleCatalog.Styles.ToList().FindIndex(x => x.Id == _splitScreenTitlePlan.StyleId);
			_splitScreenTitleStyleCombo.SelectedIndex = Math.Max(0, idx);
		}
		if (_splitScreenTitlePositionCombo != null)
		{
			string targetPos = _splitScreenTitlePlan.Position ?? "中间分割线";
			int pidx = 0;
			if (targetPos.Contains("分割线")) pidx = 0;
			else if (targetPos.Contains("顶部") || targetPos.Contains("顶端")) pidx = 1;
			else if (targetPos.Contains("偏上")) pidx = 2;
			else if (targetPos.Contains("中央") || (targetPos.Contains("居中") && !targetPos.Contains("偏") && !targetPos.Contains("顶") && !targetPos.Contains("底"))) pidx = 3;
			else if (targetPos.Contains("偏下")) pidx = 4;
			else if (targetPos.Contains("底部")) pidx = 5;
			_splitScreenTitlePositionCombo.SelectedIndex = pidx;
		}
		if (_splitScreenTitleTextBox != null) _splitScreenTitleTextBox.Text = _splitScreenTitlePlan.MainTitleTemplate;
		if (_splitScreenTitleDurationNum != null) _splitScreenTitleDurationNum.Value = Math.Min(3600m, Math.Max(0m, (decimal)_splitScreenTitlePlan.DurationSeconds));

		bool en = _splitScreenTitlePlan.Enabled;
		if (_splitScreenTitleStyleCombo != null) _splitScreenTitleStyleCombo.Enabled = en;
		if (_splitScreenTitlePositionCombo != null) _splitScreenTitlePositionCombo.Enabled = en;
		if (_splitScreenTitleTextBox != null) _splitScreenTitleTextBox.Enabled = en;
		if (_splitScreenTitleDurationNum != null) _splitScreenTitleDurationNum.Enabled = en;
		if (_splitScreenTitleStyleBtn != null) _splitScreenTitleStyleBtn.Enabled = en;
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
			if (_splitScreenStartButton != null && _splitScreenCancelButton != null && _splitScreenSendToCutBtn != null)
			{
				int totalBtnW = _splitScreenStartButton.Width + 14 + _splitScreenCancelButton.Width + 14 + _splitScreenSendToCutBtn.Width;
				int startX = Math.Max(18, (panel.ClientSize.Width - totalBtnW) / 2);
				_splitScreenStartButton.Left = startX;
				_splitScreenCancelButton.Left = _splitScreenStartButton.Right + 14;
				_splitScreenSendToCutBtn.Left = _splitScreenCancelButton.Right + 14;
			}
		}
	}

	private void LayoutSplitScreenPreview(Control host)
	{
		if (host == null || _splitScreenPreview == null)
		{
			return;
		}
		if (host.ClientSize.Width > 20 && host.ClientSize.Height > 20)
		{
			_splitScreenPreview.Bounds = new Rectangle(0, 0, host.ClientSize.Width, host.ClientSize.Height);
		}
		RefreshSplitScreenPreview();
	}

	private void WireSplitScreenEvents()
	{
		base.Shown += delegate
		{
			LayoutSplitScreenPreview(_splitScreenPreviewHost);
		};
		_tabs.SelectedIndexChanged += delegate
		{
			if (_tabs.SelectedIndex != 5 && _cutEditIsPlaying && _cutEditMediaElement != null)
			{
				try
				{
					_cutEditMediaElement.Pause();
					_cutEditIsPlaying = false;
					if (_cutEditPlayPauseButton != null) _cutEditPlayPauseButton.Text = "▶ 播放 (空格)";
					_cutEditPlayTimer?.Stop();
				}
				catch { }
			}
			if (_tabs.SelectedIndex != 6 && _deliverIsPlaying && _deliverMediaElement != null)
			{
				try
				{
					_deliverMediaElement.Pause();
					_deliverIsPlaying = false;
					if (_deliverPlayPauseButton != null) _deliverPlayPauseButton.Text = "▶ 播放 (空格)";
					_deliverPlayTimer?.Stop();
				}
				catch { }
			}
			if (_tabs.SelectedIndex == 6)
			{
				UpdateDeliverSummary();
				InitOrRefreshDeliverPreview();
			}
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
		if (_splitScreenTitleEnabled != null) _splitScreenTitleEnabled.Checked = false;
		if (_splitScreenTitleStyleCombo != null) _splitScreenTitleStyleCombo.SelectedIndex = 0;
		if (_splitScreenTitlePositionCombo != null) _splitScreenTitlePositionCombo.SelectedIndex = 0;
		if (_splitScreenTitleTextBox != null) _splitScreenTitleTextBox.Text = "{文件名}";
		if (_splitScreenTitleDurationNum != null) _splitScreenTitleDurationNum.Value = 0m;
		_splitScreenTitlePlan = new MergeTitlePlan { Position = "中间分割线", DurationSeconds = 0.0 };
		SyncSplitScreenTitlePlanFromUi();
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
		if (_splitScreenPreview == null || _splitScreenPreview.ClientSize.Width <= 10 || _splitScreenPreview.ClientSize.Height <= 10)
		{
			return RectangleF.Empty;
		}
		Size splitScreenCanvasSize = GetSplitScreenCanvasSize();
		int pad = 16;
		int w = Math.Max(20, _splitScreenPreview.ClientSize.Width - pad * 2);
		int h = Math.Max(20, _splitScreenPreview.ClientSize.Height - pad * 2);
		RectangleF bounds = new RectangleF(pad, pad, w, h);
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
		int num = Math.Max(20, _splitScreenPreview.ClientSize.Width);
		int num2 = Math.Max(20, _splitScreenPreview.ClientSize.Height);
		Bitmap image = new Bitmap(num, num2, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(image))
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.Clear(Color.FromArgb(20, 24, 34));
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
			if (_splitScreenTitlePlan != null && _splitScreenTitlePlan.Enabled && !string.IsNullOrWhiteSpace(_splitScreenTitleTextBox?.Text))
			{
				try
				{
					List<string> sampleFiles = _splitScreenRegionVideos.Where(x => x != null && x.Count > 0).Select(x => x[0]).ToList();
					string previewMain = MergeTitleStyleCatalog.ResolveTitleText(_splitScreenTitleTextBox.Text, sampleFiles, 0);
					string previewSub = MergeTitleStyleCatalog.ResolveTitleText(_splitScreenTitlePlan.SubTitleTemplate, sampleFiles, 0);
					if (!string.IsNullOrWhiteSpace(previewMain))
					{
						Size canvasSize = GetSplitScreenCanvasSize();
						using (Bitmap titleBmp = MergeTitleStyleCatalog.RenderTitleBitmap(_splitScreenTitlePlan, canvasSize.Width, canvasSize.Height, previewMain, previewSub))
						{
							graphics.DrawImage(titleBmp, splitScreenPreviewCanvas, new RectangleF(0f, 0f, titleBmp.Width, titleBmp.Height), GraphicsUnit.Pixel);
						}
					}
				}
				catch { }
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
		SyncSplitScreenTitlePlanFromUi();
		MergeTitlePlan splitScreenTitle = _splitScreenTitlePlan?.Clone();

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
					if (splitScreenTitle != null && splitScreenTitle.Enabled)
					{
						ApplyTitleToSplitScreenOutput(ffmpeg, tempPreviewFile, splitScreenTitle, sources, 0, previewPlan.DurationSeconds, delegate { }, out _);
					}
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
		SyncSplitScreenTitlePlanFromUi();
		MergeTitlePlan splitScreenTitle = _splitScreenTitlePlan?.Clone();
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
					bool titleEnabled = splitScreenTitle != null && splitScreenTitle.Enabled;
					bool wmEnabled = splitScreenWatermark.Enabled;
					bool bgmEnabled = _activeSplitScreenBgmPlan.Enabled;
					double titlePortion = titleEnabled ? 0.12 : 0.0;
					double wmPortion = wmEnabled ? 0.12 : 0.0;
					double bgmPortion = bgmEnabled ? 0.12 : 0.0;
					double renderPortion = 1.0 - titlePortion - wmPortion - bgmPortion;
					double titleStart = renderPortion;
					double wmStart = titleStart + titlePortion;
					double bgmStart = wmStart + wmPortion;
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
					else if (titleEnabled && !_cancelRequested && !ApplyTitleToSplitScreenOutput(ffmpeg, text, splitScreenTitle, array, k, splitScreenRenderPlan.DurationSeconds, delegate(double p)
					{
						ReportSplitScreenProgress(completedBefore, plan.OutputCount, titleStart + p * titlePortion, "正在为第 " + (completedBefore + 1) + "/" + plan.OutputCount + " 个拼屏成品合成标题");
					}, out var errorTitle))
					{
						failed++;
						lastError = errorTitle;
						TryDelete(text);
					}
					else if (wmEnabled && !_cancelRequested && !ApplyWatermarkToSplitScreenOutput(ffmpeg, text, splitScreenWatermarkAssignments[Math.Min(k, splitScreenWatermarkAssignments.Count - 1)], splitScreenRenderPlan.DurationSeconds, delegate(double p)
					{
						ReportSplitScreenProgress(completedBefore, plan.OutputCount, wmStart + p * wmPortion, "正在为第 " + (completedBefore + 1) + "/" + plan.OutputCount + " 个拼屏成品添加水印");
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

	private bool ApplyTitleToSplitScreenOutput(string ffmpeg, string outputPath, MergeTitlePlan titlePlan, IReadOnlyList<string> regionFiles, int outputIndex, double expectedDuration, Action<double> progress, out string error)
	{
		error = null;
		if (titlePlan == null || !titlePlan.Enabled) return true;
		string text = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_SplitTitle_" + Guid.NewGuid().ToString("N"));
		string tempOutput = Path.Combine(text, "titled.mp4");
		string titlePng = Path.Combine(text, "title_overlay.png");
		try
		{
			Directory.CreateDirectory(text);
			VideoInfo videoInfo = Probe(ffmpeg, outputPath);
			if (!videoInfo.HasVideo || videoInfo.Width <= 0 || videoInfo.Height <= 0)
			{
				error = "无法读取刚生成的拼屏成品。";
				return false;
			}
			string resolvedMain = MergeTitleStyleCatalog.ResolveTitleText(titlePlan.MainTitleTemplate, regionFiles, outputIndex);
			string resolvedSub = MergeTitleStyleCatalog.ResolveTitleText(titlePlan.SubTitleTemplate, regionFiles, outputIndex);
			if (string.IsNullOrWhiteSpace(resolvedMain)) return true;

			using (Bitmap bmp = MergeTitleStyleCatalog.RenderTitleBitmap(titlePlan, videoInfo.Width, videoInfo.Height, resolvedMain, resolvedSub))
			{
				bmp.Save(titlePng, System.Drawing.Imaging.ImageFormat.Png);
			}

			StringBuilder stringBuilder = new StringBuilder("-hide_banner -y -i ").Append(QuoteArg(outputPath));
			stringBuilder.Append(" -loop 1 -framerate 30 -i ").Append(QuoteArg(titlePng));

			List<string> filters = new List<string>();
			string currentVideo = "0:v";
			AppendTitleFilter(filters, ref currentVideo, 1, titlePlan);
			filters.Add("[" + currentVideo + "]trim=duration=" + FfmpegNumber(expectedDuration) + ",setpts=PTS-STARTPTS,format=yuv420p[vout]");

			stringBuilder.Append(" -filter_complex ").Append(QuoteArg(string.Join(";", filters.ToArray())))
				.Append(" -map [vout] -map 0:a:0? -c:v libx264 -preset fast -crf 17 -pix_fmt yuv420p")
				.Append(" -metadata:s:v:0 rotate=0 -c:a copy -t ")
				.Append(FfmpegNumber(expectedDuration))
				.Append(" -movflags +faststart -progress pipe:1 -nostats ")
				.Append(QuoteArg(tempOutput));

			int num = RunFfmpeg(ffmpeg, stringBuilder.ToString(), expectedDuration, progress, out var errorText);
			string reason = null;
			if (num == 0 && ValidateExactDurationOutput(ffmpeg, tempOutput, expectedDuration, out reason))
			{
				File.Copy(tempOutput, outputPath, overwrite: true);
				TryDelete(tempOutput);
				return true;
			}
			error = "拼屏标题添加失败：" + LastUsefulLines(errorText, 6);
			return false;
		}
		catch (Exception ex)
		{
			error = "拼屏标题添加异常：" + ex.Message;
			return false;
		}
		finally
		{
			TryDelete(tempOutput);
			TryDelete(titlePng);
			TryDelete(text);
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

	private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
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

	private static readonly System.Collections.Generic.HashSet<Button> _glassStyledButtons = new System.Collections.Generic.HashSet<Button>();
	private static readonly System.Collections.Generic.HashSet<GroupBox> _styledGroupBoxes = new System.Collections.Generic.HashSet<GroupBox>();

	private static void StyleModernGroupBox(GroupBox gb)
	{
		if (gb == null) return;
		if (_styledGroupBoxes.Contains(gb)) return;
		_styledGroupBoxes.Add(gb);
		gb.Disposed += delegate { _styledGroupBoxes.Remove(gb); };

		gb.Paint += delegate(object sender, PaintEventArgs e)
		{
			try
			{
				Graphics g = e.Graphics;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				Rectangle bounds = gb.ClientRectangle;
				bounds.Width -= 1;
				bounds.Height -= 1;
				if (bounds.Width <= 10 || bounds.Height <= 10) return;

				Color titleColor = _isDarkMode ? Color.FromArgb(240, 246, 255) : Color.FromArgb(15, 23, 42);
				Color borderColor = _isDarkMode ? Color.FromArgb(42, 64, 100) : Color.FromArgb(203, 213, 225);
				Color cardBg = SurfaceColor;

				using (Brush bgBrush = new SolidBrush(cardBg))
				{
					e.Graphics.FillRectangle(bgBrush, gb.ClientRectangle);
				}

				using Font font = new Font(gb.Font.FontFamily, 9.5f, FontStyle.Bold);
				Size textSize = TextRenderer.MeasureText(gb.Text, font);
				int topOffset = Math.Max(8, textSize.Height / 2);

				Rectangle boxRect = new Rectangle(0, topOffset, bounds.Width, bounds.Height - topOffset);
				using (GraphicsPath path = CreateRoundedRectanglePath(boxRect, 6))
				{
					using (Pen pen = new Pen(borderColor, 1.2f))
					{
						g.DrawPath(pen, path);
					}
				}

				Rectangle textRect = new Rectangle(12, 0, textSize.Width + 8, textSize.Height);
				using (Brush bgBrush = new SolidBrush(cardBg))
				{
					e.Graphics.FillRectangle(bgBrush, textRect);
				}
				TextRenderer.DrawText(g, gb.Text, font, new Point(16, 0), titleColor);
			}
			catch { }
		};
	}

	private static void StyleTechSplitter(SplitContainer sc)
	{
		sc.SplitterWidth = 7;
		sc.BackColor = SplitterColor;
		sc.Paint += delegate(object sender, PaintEventArgs e)
		{
			try
			{
				Rectangle rect = sc.SplitterRectangle;
				if (rect.Width <= 0 || rect.Height <= 0) return;
				Color gripBg = _isDarkMode ? Color.FromArgb(19, 32, 55) : Color.FromArgb(226, 232, 240);
				Color borderLine = _isDarkMode ? Color.FromArgb(34, 53, 84) : Color.FromArgb(203, 213, 225);
				Color dotColor = _isDarkMode ? Color.FromArgb(59, 130, 246) : Color.FromArgb(37, 99, 235);

				using (Brush bgBrush = new SolidBrush(gripBg))
				{
					e.Graphics.FillRectangle(bgBrush, rect);
				}
				using (Pen pen = new Pen(borderLine))
				{
					if (sc.Orientation == Orientation.Horizontal)
					{
						e.Graphics.DrawLine(pen, rect.Left, rect.Top, rect.Right, rect.Top);
						e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
						int midX = rect.Left + rect.Width / 2;
						int midY = rect.Top + rect.Height / 2;
						using (Brush dotBrush = new SolidBrush(dotColor))
						{
							e.Graphics.FillRectangle(dotBrush, midX - 16, midY - 1, 6, 2);
							e.Graphics.FillRectangle(dotBrush, midX - 3, midY - 1, 6, 2);
							e.Graphics.FillRectangle(dotBrush, midX + 10, midY - 1, 6, 2);
						}
					}
					else
					{
						e.Graphics.DrawLine(pen, rect.Left, rect.Top, rect.Left, rect.Bottom);
						e.Graphics.DrawLine(pen, rect.Right - 1, rect.Top, rect.Right - 1, rect.Bottom);
						int midX = rect.Left + rect.Width / 2;
						int midY = rect.Top + rect.Height / 2;
						using (Brush dotBrush = new SolidBrush(dotColor))
						{
							e.Graphics.FillRectangle(dotBrush, midX - 1, midY - 16, 2, 6);
							e.Graphics.FillRectangle(dotBrush, midX - 1, midY - 3, 2, 6);
							e.Graphics.FillRectangle(dotBrush, midX - 1, midY + 10, 2, 6);
						}
					}
				}
			}
			catch { }
		};
	}

	private static void ApplyGlassButtonEffects(Button button)
	{
		if (button == null) return;
		if (_glassStyledButtons.Contains(button)) return;
		_glassStyledButtons.Add(button);
		button.Disposed += delegate { _glassStyledButtons.Remove(button); };

		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderSize = 0;
		button.Cursor = Cursors.Hand;

		bool isHover = false;
		bool isPressed = false;

		button.MouseEnter += delegate { isHover = true; button.Invalidate(); };
		button.MouseLeave += delegate { isHover = false; isPressed = false; button.Invalidate(); };
		button.MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { isPressed = true; button.Refresh(); } };
		button.MouseUp += delegate { isPressed = false; button.Refresh(); };

		button.Paint += delegate(object sender, PaintEventArgs e)
		{
			try
			{
				Graphics g = e.Graphics;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.PixelOffsetMode = PixelOffsetMode.HighQuality;
				g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

				Rectangle bounds = button.ClientRectangle;
				bounds.Width -= 1;
				bounds.Height -= 1;
				if (bounds.Width < 2 || bounds.Height < 2) return;

				Color parentBg = button.Parent?.BackColor ?? (_isDarkMode ? Color.FromArgb(19, 32, 55) : Color.White);
				using (Brush pbg = new SolidBrush(parentBg))
				{
					g.FillRectangle(pbg, button.ClientRectangle);
				}

				if (isPressed)
				{
					bounds.Offset(1, 1);
				}

				bool isPrimary = (button.Tag as string) == "primary" || 
				                 button.Text.StartsWith("开始") || 
				                 button.Text.StartsWith("直接加水印") || 
				                 button.Text.Contains("播放");

				bool isDanger = (button.Tag as string) == "danger" || 
				                button.Text == "重置全部设置" || 
				                button.Text == "清空" || 
				                button.Text == "取消";

				bool isAccent = (button.Tag as string) == "accent" || 
				                button.Text.Contains("模式");

				bool isNavActive = (button.Tag as string) == "nav-active";
				bool isNav = (button.Tag as string) == "nav";

				bool isEnabled = button.Enabled;

				Color topGrad, botGrad, borderColor, specularColor, textColor;

				if (!isEnabled)
				{
					if (_isDarkMode)
					{
						topGrad = Color.FromArgb(22, 32, 50);
						botGrad = Color.FromArgb(16, 24, 38);
						borderColor = Color.FromArgb(32, 45, 68);
						specularColor = Color.Transparent;
						textColor = Color.FromArgb(80, 95, 120);
					}
					else
					{
						topGrad = Color.FromArgb(243, 244, 246);
						botGrad = Color.FromArgb(229, 231, 235);
						borderColor = Color.FromArgb(209, 213, 219);
						specularColor = Color.Transparent;
						textColor = Color.FromArgb(156, 163, 175);
					}
				}
				else if (isPrimary)
				{
					if (isPressed)
					{
						topGrad = Color.FromArgb(29, 78, 216);
						botGrad = Color.FromArgb(17, 24, 39);
						borderColor = Color.FromArgb(147, 197, 253);
						specularColor = Color.Transparent;
						textColor = Color.FromArgb(191, 219, 254);
					}
					else if (isHover)
					{
						topGrad = Color.FromArgb(59, 130, 246);
						botGrad = Color.FromArgb(29, 78, 216);
						borderColor = Color.FromArgb(147, 197, 253);
						specularColor = Color.FromArgb(120, 255, 255, 255);
						textColor = Color.White;
					}
					else
					{
						topGrad = Color.FromArgb(37, 99, 235);
						botGrad = Color.FromArgb(29, 78, 216);
						borderColor = Color.FromArgb(96, 165, 250);
						specularColor = Color.FromArgb(80, 255, 255, 255);
						textColor = Color.White;
					}
				}
				else if (isDanger)
				{
					if (isPressed)
					{
						topGrad = Color.FromArgb(127, 29, 29);
						botGrad = Color.FromArgb(69, 10, 10);
						borderColor = Color.FromArgb(248, 113, 113);
						specularColor = Color.Transparent;
						textColor = Color.FromArgb(254, 205, 211);
					}
					else if (isHover)
					{
						topGrad = Color.FromArgb(220, 38, 38);
						botGrad = Color.FromArgb(185, 28, 28);
						borderColor = Color.FromArgb(252, 165, 165);
						specularColor = Color.FromArgb(90, 255, 220, 220);
						textColor = Color.White;
					}
					else
					{
						topGrad = Color.FromArgb(185, 28, 28);
						botGrad = Color.FromArgb(153, 27, 27);
						borderColor = Color.FromArgb(239, 68, 68);
						specularColor = Color.FromArgb(60, 255, 200, 200);
						textColor = Color.White;
					}
				}
				else if (isAccent)
				{
					if (_isDarkMode)
					{
						if (isPressed)
						{
							topGrad = Color.FromArgb(12, 28, 52);
							botGrad = Color.FromArgb(8, 20, 38);
							borderColor = Color.FromArgb(56, 189, 248);
							specularColor = Color.Transparent;
							textColor = Color.FromArgb(186, 230, 253);
						}
						else if (isHover)
						{
							topGrad = Color.FromArgb(32, 60, 105);
							botGrad = Color.FromArgb(20, 42, 78);
							borderColor = Color.FromArgb(125, 211, 252);
							specularColor = Color.FromArgb(100, 56, 189, 248);
							textColor = Color.White;
						}
						else
						{
							topGrad = Color.FromArgb(22, 45, 80);
							botGrad = Color.FromArgb(15, 32, 60);
							borderColor = Color.FromArgb(56, 189, 248);
							specularColor = Color.FromArgb(60, 56, 189, 248);
							textColor = Color.FromArgb(224, 242, 254);
						}
					}
					else
					{
						if (isPressed)
						{
							topGrad = Color.FromArgb(186, 230, 253);
							botGrad = Color.FromArgb(125, 211, 252);
							borderColor = Color.FromArgb(2, 132, 199);
							specularColor = Color.Transparent;
							textColor = Color.FromArgb(12, 74, 110);
						}
						else if (isHover)
						{
							topGrad = Color.FromArgb(224, 242, 254);
							botGrad = Color.FromArgb(186, 230, 253);
							borderColor = Color.FromArgb(2, 132, 199);
							specularColor = Color.FromArgb(90, 255, 255, 255);
							textColor = Color.FromArgb(2, 132, 199);
						}
						else
						{
							topGrad = Color.FromArgb(240, 249, 255);
							botGrad = Color.FromArgb(224, 242, 254);
							borderColor = Color.FromArgb(56, 189, 248);
							specularColor = Color.FromArgb(90, 255, 255, 255);
							textColor = Color.FromArgb(3, 105, 161);
						}
					}
				}
				else if (isNavActive)
				{
					if (isPressed)
					{
						topGrad = Color.FromArgb(29, 78, 216);
						botGrad = Color.FromArgb(17, 24, 39);
						borderColor = Color.FromArgb(147, 197, 253);
						specularColor = Color.Transparent;
						textColor = Color.FromArgb(191, 219, 254);
					}
					else if (isHover)
					{
						topGrad = Color.FromArgb(59, 130, 246);
						botGrad = Color.FromArgb(29, 78, 216);
						borderColor = Color.FromArgb(147, 197, 253);
						specularColor = Color.FromArgb(120, 255, 255, 255);
						textColor = Color.White;
					}
					else
					{
						topGrad = Color.FromArgb(37, 99, 235);
						botGrad = Color.FromArgb(29, 78, 216);
						borderColor = Color.FromArgb(96, 165, 250);
						specularColor = Color.FromArgb(80, 255, 255, 255);
						textColor = Color.White;
					}
				}
				else if (isNav)
				{
					if (_isDarkMode)
					{
						if (isPressed)
						{
							topGrad = Color.FromArgb(10, 18, 32);
							botGrad = Color.FromArgb(8, 14, 25);
							borderColor = Color.FromArgb(59, 130, 246);
							specularColor = Color.Transparent;
							textColor = Color.FromArgb(147, 197, 253);
						}
						else if (isHover)
						{
							topGrad = Color.FromArgb(30, 48, 80);
							botGrad = Color.FromArgb(20, 34, 60);
							borderColor = Color.FromArgb(96, 165, 250);
							specularColor = Color.FromArgb(60, 255, 255, 255);
							textColor = Color.White;
						}
						else
						{
							topGrad = Color.FromArgb(18, 28, 48);
							botGrad = Color.FromArgb(13, 22, 38);
							borderColor = Color.FromArgb(35, 50, 75);
							specularColor = Color.FromArgb(25, 255, 255, 255);
							textColor = Color.FromArgb(148, 163, 184);
						}
					}
					else
					{
						if (isPressed)
						{
							topGrad = Color.FromArgb(226, 232, 240);
							botGrad = Color.FromArgb(203, 213, 225);
							borderColor = Color.FromArgb(37, 99, 235);
							specularColor = Color.Transparent;
							textColor = Color.FromArgb(29, 78, 216);
						}
						else if (isHover)
						{
							topGrad = Color.FromArgb(255, 255, 255);
							botGrad = Color.FromArgb(241, 245, 249);
							borderColor = Color.FromArgb(59, 130, 246);
							specularColor = Color.FromArgb(80, 255, 255, 255);
							textColor = Color.FromArgb(15, 23, 42);
						}
						else
						{
							topGrad = Color.FromArgb(248, 250, 252);
							botGrad = Color.FromArgb(238, 242, 246);
							borderColor = Color.FromArgb(218, 224, 233);
							specularColor = Color.FromArgb(80, 255, 255, 255);
							textColor = Color.FromArgb(71, 85, 105);
						}
					}
				}
				else
				{
					if (_isDarkMode)
					{
						if (isPressed)
						{
							topGrad = Color.FromArgb(14, 24, 42);
							botGrad = Color.FromArgb(10, 18, 32);
							borderColor = Color.FromArgb(59, 130, 246);
							specularColor = Color.Transparent;
							textColor = Color.FromArgb(147, 197, 253);
						}
						else if (isHover)
						{
							topGrad = Color.FromArgb(38, 60, 95);
							botGrad = Color.FromArgb(28, 45, 75);
							borderColor = Color.FromArgb(96, 165, 250);
							specularColor = Color.FromArgb(70, 255, 255, 255);
							textColor = Color.White;
						}
						else
						{
							topGrad = Color.FromArgb(26, 42, 68);
							botGrad = Color.FromArgb(19, 32, 55);
							borderColor = Color.FromArgb(46, 70, 110);
							specularColor = Color.FromArgb(40, 255, 255, 255);
							textColor = Color.FromArgb(241, 245, 249);
						}
					}
					else
					{
						if (isPressed)
						{
							topGrad = Color.FromArgb(239, 246, 255);
							botGrad = Color.FromArgb(219, 234, 254);
							borderColor = Color.FromArgb(37, 99, 235);
							specularColor = Color.Transparent;
							textColor = Color.FromArgb(29, 78, 216);
						}
						else if (isHover)
						{
							topGrad = Color.FromArgb(255, 255, 255);
							botGrad = Color.FromArgb(243, 244, 246);
							borderColor = Color.FromArgb(59, 130, 246);
							specularColor = Color.FromArgb(80, 255, 255, 255);
							textColor = Color.FromArgb(15, 23, 42);
						}
						else
						{
							topGrad = Color.FromArgb(255, 255, 255);
							botGrad = Color.FromArgb(248, 250, 252);
							borderColor = Color.FromArgb(203, 213, 225);
							specularColor = Color.FromArgb(90, 255, 255, 255);
							textColor = Color.FromArgb(30, 41, 59);
						}
					}
				}

				using (GraphicsPath path = CreateRoundedRectanglePath(bounds, 5))
				{
					using (LinearGradientBrush lgb = new LinearGradientBrush(bounds, topGrad, botGrad, LinearGradientMode.Vertical))
					{
						g.FillPath(lgb, path);
					}
					if (specularColor.A > 0)
					{
						using (Pen specPen = new Pen(specularColor, 1f))
						{
							g.DrawLine(specPen, bounds.Left + 4, bounds.Top + 1, bounds.Right - 4, bounds.Top + 1);
						}
					}
					using (Pen borderPen = new Pen(borderColor, 1f))
					{
						g.DrawPath(borderPen, path);
					}
				}

				Font btnFont = (isPrimary || isDanger || isNavActive) ? new Font(button.Font, FontStyle.Bold) : button.Font;
				try
				{
					TextRenderer.DrawText(g, button.Text, btnFont, bounds, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
				}
				finally
				{
					if (btnFont != button.Font) btnFont.Dispose();
				}
			}
			catch { }
		};
	}

	private Button MakePrimaryButton(string text, int x, int y, int width)
	{
		Button button = MakeButton(text, width);
		button.Location = new Point(x, y);
		button.Height = 42;
		button.Tag = "primary";
		return button;
	}

	private Button MakeButton(string text, int width)
	{
		Button button = new Button();
		button.Text = text;
		button.Width = width;
		button.Height = 34;
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderSize = 0;
		button.Margin = new Padding(0, 0, 8, 0);
		button.Cursor = Cursors.Hand;
		ApplyGlassButtonEffects(button);
		return button;
	}

	private void UpdateThemeToggleButton()
	{
		if (_themeToggleButton != null)
		{
			_themeToggleButton.Text = _isDarkMode ? "🌙 暗黑模式" : "☀️ 日间模式";
			_themeToggleButton.Invalidate();
		}
	}

	private void RefreshSplitScreenPresetThumbnails()
	{
		try
		{
			ReloadSplitScreenPresetCategory(_splitScreenQuickPresets, "常用分屏");
			ReloadSplitScreenPresetCategory(_splitScreenGridPresets, "多格拼屏");
			ReloadSplitScreenPresetCategory(_splitScreenPipPresets, "画中画");
		}
		catch { }
	}

	private void ReloadSplitScreenPresetCategory(ListView listView, string category)
	{
		if (listView == null || listView.LargeImageList == null) return;
		listView.BackColor = SurfaceColor;
		listView.ForeColor = InkColor;
		ImageList imageList = listView.LargeImageList;
		imageList.Images.Clear();
		foreach (SplitScreenLayoutDefinition item in _splitScreenLayouts.Where(x => x.Category == category))
		{
			imageList.Images.Add(item.Id, DrawSplitScreenTemplateThumbnail(item, imageList.ImageSize));
		}
		listView.Invalidate();
	}

	private void ApplyThemeToWholeApp()
	{
		SuspendLayout();
		try
		{
			base.BackColor = CanvasColor;
			if (_bottomNavBar != null)
			{
				_bottomNavBar.BackColor = HeaderColor;
			}
			if (_bottomNavLine != null)
			{
				_bottomNavLine.BackColor = BorderColor;
			}
			if (_tabs != null)
			{
				_tabs.BackColor = CanvasColor;
				_tabs.Invalidate();
			}
			UpdateThemeToggleButton();
			ApplyThemeRecursive(this);
			RefreshSplitScreenPresetThumbnails();
		}
		finally
		{
			ResumeLayout(performLayout: true);
			Invalidate(invalidateChildren: true);
		}
	}

	private static readonly System.Collections.Generic.HashSet<Control> _focusHookedControls = new System.Collections.Generic.HashSet<Control>();
	private static void HookFocusVisual(Control c)
	{
		if (c == null || _focusHookedControls.Contains(c)) return;
		_focusHookedControls.Add(c);
		c.Disposed += delegate { _focusHookedControls.Remove(c); };

		c.Enter += delegate
		{
			c.BackColor = _isDarkMode ? Color.FromArgb(28, 48, 80) : Color.FromArgb(255, 255, 255);
		};
		c.Leave += delegate
		{
			c.BackColor = InputBgColor;
		};
	}

	private void ApplyThemeRecursive(Control root)
	{
		if (root == null) return;

		foreach (Control control in root.Controls)
		{
			if (control is TabPage tabPage)
			{
				tabPage.BackColor = (tabPage.Parent == _tabs) ? CanvasColor : SurfaceColor;
			}
			else if (control is SplitContainer sc)
			{
				sc.BackColor = SplitterColor;
				sc.Invalidate();
			}
			else if (control is SplitScreenPreviewBox)
			{
				control.BackColor = Color.FromArgb(20, 24, 34);
			}
			else if (control == _splitScreenPreviewHost)
			{
				control.BackColor = Color.FromArgb(20, 24, 34);
			}
			else if (control is GroupBox groupBox)
			{
				groupBox.BackColor = SurfaceColor;
				groupBox.ForeColor = _isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42);
				StyleModernGroupBox(groupBox);
			}
			else if (control is Panel panel)
			{
				if (panel != _bottomNavBar && panel != _bottomNavLine)
				{
					panel.BackColor = SurfaceColor;
				}
			}
			else if (control is TextBox textBox)
			{
				textBox.BorderStyle = BorderStyle.FixedSingle;
				textBox.BackColor = InputBgColor;
				textBox.ForeColor = InkColor;
				HookFocusVisual(textBox);
			}
			else if (control is NumericUpDown numericUpDown)
			{
				numericUpDown.BorderStyle = BorderStyle.FixedSingle;
				numericUpDown.BackColor = InputBgColor;
				numericUpDown.ForeColor = InkColor;
				HookFocusVisual(numericUpDown);
			}
			else if (control is ComboBox comboBox)
			{
				comboBox.FlatStyle = FlatStyle.Flat;
				comboBox.BackColor = InputBgColor;
				comboBox.ForeColor = InkColor;
				HookFocusVisual(comboBox);
			}
			else if (control is CheckedListBox || control is ListBox)
			{
				control.BackColor = InputBgColor;
				control.ForeColor = InkColor;
			}
			else if (control is ListView listView)
			{
				listView.BackColor = SurfaceColor;
				listView.ForeColor = InkColor;
				listView.Invalidate();
			}
			else if (control is CheckBox cb)
			{
				cb.ForeColor = InkColor;
				cb.BackColor = Color.Transparent;
				cb.Cursor = Cursors.Hand;
			}
			else if (control is RadioButton rb)
			{
				rb.ForeColor = InkColor;
				rb.BackColor = Color.Transparent;
				rb.Cursor = Cursors.Hand;
			}
			else if (control is TabControl tabControl)
			{
				tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
				tabControl.SizeMode = TabSizeMode.Fixed;
				tabControl.ItemSize = ((tabControl == _tabs) ? new Size(0, 1) : ((tabControl == _splitScreenPresetTabs) ? new Size(92, 32) : new Size(150, 32)));
				tabControl.DrawItem -= DrawModernTab;
				tabControl.DrawItem += DrawModernTab;
				tabControl.Invalidate();
			}
			else if (control is Label label)
			{
				if (label.Name == "VideoListEmptyHint" || 
				    label.ForeColor == Color.FromArgb(148, 163, 184) || 
				    label.ForeColor == Color.FromArgb(100, 110, 125) || 
				    label.ForeColor == Color.FromArgb(110, 119, 132))
				{
					label.ForeColor = MutedColor;
				}
				else if (label.ForeColor != AccentColor && label.ForeColor != Color.FromArgb(56, 189, 248))
				{
					label.ForeColor = InkColor;
				}
			}
			else if (control is Button button)
			{
				ApplyGlassButtonEffects(button);
				button.Invalidate();
			}

			ApplyThemeRecursive(control);
		}
	}

	private void DrawModernTab(object sender, DrawItemEventArgs e)
	{
		if (!(sender is TabControl tabControl) || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
		{
			return;
		}
		bool isSelected = e.Index == tabControl.SelectedIndex;
		Rectangle bounds = e.Bounds;

		Color tabBg;
		Color tabTextColor;
		Color tabIndicator = AccentColor;

		if (_isDarkMode)
		{
			tabBg = isSelected ? SurfaceColor : CanvasColor;
			tabTextColor = isSelected ? Color.White : MutedColor;
		}
		else
		{
			tabBg = isSelected ? Color.White : Color.FromArgb(235, 238, 243);
			tabTextColor = isSelected ? Color.FromArgb(15, 23, 42) : Color.FromArgb(100, 116, 139);
		}

		using (Brush brush = new SolidBrush(tabBg))
		{
			e.Graphics.FillRectangle(brush, bounds);
		}

		if (isSelected)
		{
			using Brush brush2 = new SolidBrush(tabIndicator);
			int pad = (tabControl == _tabs) ? 8 : 6;
			e.Graphics.FillRectangle(brush2, bounds.Left + pad, bounds.Bottom - 3, Math.Max(4, bounds.Width - pad * 2), 3);
		}

		using Font font = new Font("Microsoft YaHei UI", (tabControl == _tabs) ? 10f : 9f, isSelected ? FontStyle.Bold : FontStyle.Regular);
		TextRenderer.DrawText(e.Graphics, tabControl.TabPages[e.Index].Text, font, bounds, tabTextColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

		if (e.Index == tabControl.TabPages.Count - 1 && bounds.Right < tabControl.ClientSize.Width)
		{
			try
			{
				using (Graphics g = tabControl.CreateGraphics())
				{
					Rectangle restRect = new Rectangle(bounds.Right, 0, tabControl.ClientSize.Width - bounds.Right, bounds.Height + 2);
					using (Brush restBrush = new SolidBrush(_isDarkMode ? CanvasColor : Color.FromArgb(235, 238, 243)))
					{
						g.FillRectangle(restBrush, restRect);
					}
				}
			}
			catch { }
		}
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
			if (sender == _splitScreenRegionList || sender == _splitScreenPreview || (_tabs != null && _tabs.SelectedIndex == 4))
			{
				AddSplitScreenVideoPaths(paths, _splitScreenSelectedRegion);
			}
			else if (sender == _cutEditMediaPoolList || sender == _cutEditPreviewBox || (_tabs != null && _tabs.SelectedIndex == 5))
			{
				AddCutEditMediaPoolPaths(paths);
			}
			else if (sender == _slideshowImageList || (_tabs != null && _tabs.SelectedIndex == 3))
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
		Color headerBg = _isDarkMode ? Color.FromArgb(15, 26, 45) : Color.FromArgb(241, 245, 249);
		Color borderColor = _isDarkMode ? Color.FromArgb(34, 53, 84) : Color.FromArgb(203, 213, 225);
		Color textColor = _isDarkMode ? Color.FromArgb(210, 225, 245) : Color.FromArgb(30, 41, 59);

		using (Brush brush = new SolidBrush(headerBg))
		{
			e.Graphics.FillRectangle(brush, e.Bounds);
		}
		using (Pen pen = new Pen(borderColor))
		{
			e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
			e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 5, e.Bounds.Right - 1, e.Bounds.Bottom - 5);
		}
		using Font font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		TextRenderer.DrawText(e.Graphics, e.Header.Text, font, e.Bounds, textColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}

	private static void DrawVideoListSubItem(DrawListViewSubItemEventArgs e, List<string> videos, Dictionary<string, VideoAdjustmentSettings> settings)
	{
		Color selColor = _isDarkMode ? Color.FromArgb(30, 64, 120) : Color.FromArgb(219, 234, 254);
		Color alt1 = SurfaceColor;
		Color alt2 = SurfaceAltColor;
		Color gridLine = _isDarkMode ? Color.FromArgb(30, 48, 76) : Color.FromArgb(226, 232, 240);

		Color color = e.Item.Selected ? selColor : ((e.ItemIndex % 2 == 0) ? alt1 : alt2);
		using (Brush brush = new SolidBrush(color))
		{
			e.Graphics.FillRectangle(brush, e.Bounds);
		}
		using (Pen pen = new Pen(gridLine))
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
			Color resetBg = _isDarkMode ? Color.FromArgb(26, 42, 68) : Color.FromArgb(241, 245, 249);
			Color resetBorder = _isDarkMode ? Color.FromArgb(46, 70, 110) : Color.FromArgb(203, 213, 225);
			using (Brush brush2 = new SolidBrush(resetBg))
			{
				e.Graphics.FillRectangle(brush2, rectangle4);
			}
			using (Pen pen2 = new Pen(resetBorder))
			{
				e.Graphics.DrawRectangle(pen2, rectangle4.X, rectangle4.Y, rectangle4.Width - 1, rectangle4.Height - 1);
			}
			using Font font2 = new Font("Microsoft YaHei UI", 8f);
			Color resetTextColor = _isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59);
			TextRenderer.DrawText(e.Graphics, "重置", font2, rectangle4, resetTextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
		Color trackBg = _isDarkMode ? Color.FromArgb(34, 53, 84) : Color.FromArgb(203, 213, 225);
		using (Pen pen3 = new Pen(trackBg, 3f))
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
		_activeMergeTitlePlan = CaptureMergeTitlePlan();
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
		_activeMergeTitlePlan = CaptureMergeTitlePlan();
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
						dummyLog,
						0
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

	private void UpdateMergeTitleControlsEnabled()
	{
		bool en = _mergeTitleEnabled?.Checked == true;
		if (_mergeTitleStyleCombo != null) _mergeTitleStyleCombo.Enabled = en;
		if (_mergeTitleMainTextBox != null) _mergeTitleMainTextBox.Enabled = en;
		if (_mergeTitleDurationNum != null) _mergeTitleDurationNum.Enabled = en;
		if (_mergeTitleTweakBtn != null) _mergeTitleTweakBtn.Enabled = en;
	}

	private void ShowMergeTitleTweakDialog()
	{
		var plan = CaptureMergeTitlePlan();
		string sampleText = _mergeTitleMainTextBox.Text.Trim();
		if (sampleText.Contains("{文件名}") || sampleText.Contains("{filename}") || sampleText.Contains("{name}"))
		{
			string firstFile = _videos.FirstOrDefault(File.Exists);
			sampleText = !string.IsNullOrEmpty(firstFile) ? MergeTitleStyleCatalog.CleanFilenameForTitle(firstFile) : "🔥 热门短视频标题示例";
		}
		using (var dlg = new MergeTitleStyleDialog(plan, sampleText, _isDarkMode))
		{
			if (dlg.ShowDialog(this) == DialogResult.OK)
			{
				_mergeTitlePlan = plan;
			}
		}
	}

	private MergeTitlePlan CaptureMergeTitlePlan()
	{
		var plan = _mergeTitlePlan?.Clone() ?? new MergeTitlePlan();
		plan.Enabled = _mergeTitleEnabled?.Checked ?? false;
		if (_mergeTitleStyleCombo != null && _mergeTitleStyleCombo.SelectedIndex >= 0 && _mergeTitleStyleCombo.SelectedIndex < MergeTitleStyleCatalog.Styles.Count)
		{
			plan.StyleId = MergeTitleStyleCatalog.Styles[_mergeTitleStyleCombo.SelectedIndex].Id;
		}
		if (_mergeTitleMainTextBox != null)
		{
			plan.MainTitleTemplate = _mergeTitleMainTextBox.Text.Trim();
		}
		if (_mergeTitleDurationNum != null)
		{
			plan.DurationSeconds = (double)_mergeTitleDurationNum.Value;
		}
		return plan;
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
						}, streamWriter, capturedIndex);
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

	private MergeResult MergeGroupWithProfileCapped(string ffmpeg, List<string> files, string outputPath, bool allowFallback, TransitionPlan transition, WatermarkProfile watermark, double outputLimitSeconds, bool randomClipRanges, Action<double, string> progress, StreamWriter log, int groupIndex = 0)
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
			bool flag3 = renderTransition.Enabled || watermark.Enabled || exactDurationOutput || flag2 || (_activeMergeTitlePlan != null && _activeMergeTitlePlan.Enabled);
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
					log.WriteLine("  已启用素材调整、统一时长、转场、水印或视频标题，将使用兼容转换和重新编码。");
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
				string tempTitlePng = null;
				if (_activeMergeTitlePlan != null && _activeMergeTitlePlan.Enabled)
				{
					try
					{
						string resolvedMain = MergeTitleStyleCatalog.ResolveTitleText(_activeMergeTitlePlan.MainTitleTemplate, files, groupIndex);
						string resolvedSub = MergeTitleStyleCatalog.ResolveTitleText(_activeMergeTitlePlan.SubTitleTemplate, files, groupIndex);
						if (!string.IsNullOrWhiteSpace(resolvedMain))
						{
							tempTitlePng = Path.Combine(text, $"MergeTitle_Group{groupIndex}_" + Guid.NewGuid().ToString("N") + ".png");
							using (Bitmap bmp = MergeTitleStyleCatalog.RenderTitleBitmap(_activeMergeTitlePlan, num7, num8, resolvedMain, resolvedSub))
							{
								bmp.Save(tempTitlePng, System.Drawing.Imaging.ImageFormat.Png);
							}
							log.WriteLine($"  已生成视频标题：【{resolvedMain}】({_activeMergeTitlePlan.Position}，呈现 {_activeMergeTitlePlan.DurationSeconds:0.#} 秒)");
						}
					}
					catch (Exception ex)
					{
						log.WriteLine("  生成视频标题失败: " + ex.Message);
					}
				}
				arguments3 = BuildAdvancedMergeArguments(list5, list6, outputPath, renderTransition, watermark, watermarkLayers, _activeMergeTitlePlan, tempTitlePng, outputLimitSeconds, out renderedDuration, log);
			}
			else
			{
				WriteConcatList(text2, list5);
				arguments3 = "-hide_banner -y -f concat -safe 0 -i " + QuoteArg(text2) + " -map 0:v:0 -map 0:a:0 -c copy" + OutputDurationArgument(outputLimitSeconds) + " -movflags +faststart -progress pipe:1 -nostats " + QuoteArg(outputPath);
			}
			int num15 = RunFfmpeg(ffmpeg, arguments3, renderedDuration, delegate(double p)
			{
				progress(normalizationWeight + p * (1.0 - normalizationWeight), renderTransition.Enabled ? "正在渲染转场" : ((_activeMergeTitlePlan != null && _activeMergeTitlePlan.Enabled) ? "正在合成标题" : (watermark.Enabled ? "正在添加水印" : "正在生成输出文件")));
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

	private string BuildAdvancedMergeArguments(List<string> normalized, List<VideoInfo> infos, string outputPath, TransitionPlan transition, WatermarkProfile watermark, List<PreparedWatermarkLayer> watermarkLayers, MergeTitlePlan titlePlan, string titlePngPath, double outputLimitSeconds, out double renderedDuration, StreamWriter log)
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
		int watermarkInputCount = watermark.Enabled ? watermarkLayers.Count : 0;
		int titleInputIndex = normalized.Count + watermarkInputCount;
		bool hasTitle = titlePlan != null && titlePlan.Enabled && !string.IsNullOrEmpty(titlePngPath) && File.Exists(titlePngPath);
		if (hasTitle)
		{
			stringBuilder.Append(" -loop 1 -framerate 30 -i ").Append(QuoteArg(titlePngPath));
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
		if (hasTitle)
		{
			AppendTitleFilter(list, ref currentVideo, titleInputIndex, titlePlan);
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

	private static void AppendTitleFilter(List<string> filters, ref string currentVideo, int titleInputIndex, MergeTitlePlan titlePlan)
	{
		double dur = titlePlan.DurationSeconds;
		string titleIn = titleInputIndex + ":v";
		string nextVideo = "vtitle";

		if (dur <= 0.05)
		{
			filters.Add("[" + currentVideo + "][" + titleIn + "]overlay=0:0:format=auto[" + nextVideo + "]");
		}
		else
		{
			double fadeOutDur = Math.Min(0.4, dur * 0.25);
			double fadeOutStart = Math.Max(0.0, dur - fadeOutDur);
			string durStr = FfmpegNumber(dur);
			string foStartStr = FfmpegNumber(fadeOutStart);
			string foDurStr = FfmpegNumber(fadeOutDur);

			string titleFaded = "tfaded";
			filters.Add("[" + titleIn + "]format=rgba,fade=t=out:st=" + foStartStr + ":d=" + foDurStr + ":alpha=1[" + titleFaded + "]");
			filters.Add("[" + currentVideo + "][" + titleFaded + "]overlay=0:0:enable='lte(t\\," + durStr + ")':format=auto[" + nextVideo + "]");
		}
		currentVideo = nextVideo;
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
			string value = Path.GetFullPath(file).Replace("\r", "").Replace("\n", "").Replace('\\', '/').Replace("'", "'\\''");
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
		string asmDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
		if (!string.IsNullOrEmpty(asmDir))
		{
			string asmFfmpeg = Path.Combine(asmDir, "ffmpeg.exe");
			if (File.Exists(asmFfmpeg)) return asmFfmpeg;
		}
		string dPath = @"D:\工具\视频拼接\VideoBatchStudio\ffmpeg.exe";
		if (File.Exists(dPath)) return dPath;
		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string wingetPath = Path.Combine(localAppData, @"Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0.1-full_build\bin\ffmpeg.exe");
		if (File.Exists(wingetPath)) return wingetPath;
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
		if (_mergeTitleEnabled != null) _mergeTitleEnabled.Checked = false;
		if (_mergeTitleStyleCombo != null) _mergeTitleStyleCombo.SelectedIndex = 0;
		if (_mergeTitleMainTextBox != null) _mergeTitleMainTextBox.Text = "{文件名}";
		if (_mergeTitleDurationNum != null) _mergeTitleDurationNum.Value = 4.0m;
		_mergeTitlePlan = new MergeTitlePlan();
		UpdateMergeTitleControlsEnabled();
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
			PutSetting(values, "ui.dark_mode", _isDarkMode);
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
			PutSetting(values, "merge.title.enabled", _mergeTitleEnabled?.Checked ?? false);
			PutSetting(values, "merge.title.style", _mergeTitleStyleCombo?.SelectedIndex ?? 0);
			PutSetting(values, "merge.title.template", _mergeTitleMainTextBox?.Text ?? "{文件名}");
			SaveNumericSetting(values, "merge.title.duration", _mergeTitleDurationNum);
			if (_mergeTitlePlan != null)
			{
				PutSetting(values, "merge.title.subTemplate", _mergeTitlePlan.SubTitleTemplate);
				PutSetting(values, "merge.title.position", _mergeTitlePlan.Position);
				PutSetting(values, "merge.title.fontScale", _mergeTitlePlan.FontSizeScale.ToString(CultureInfo.InvariantCulture));
				PutSetting(values, "merge.title.offsetY", _mergeTitlePlan.CustomOffsetY);
			}
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
			PutSetting(values, "splitScreen.title.enabled", _splitScreenTitleEnabled?.Checked ?? false);
			PutSetting(values, "splitScreen.title.style", _splitScreenTitleStyleCombo?.SelectedIndex ?? 0);
			PutSetting(values, "splitScreen.title.position", _splitScreenTitlePositionCombo?.SelectedIndex ?? 0);
			PutSetting(values, "splitScreen.title.template", _splitScreenTitleTextBox?.Text ?? "{文件名}");
			SaveNumericSetting(values, "splitScreen.title.duration", _splitScreenTitleDurationNum);
			if (_splitScreenTitlePlan != null)
			{
				PutSetting(values, "splitScreen.title.subTemplate", _splitScreenTitlePlan.SubTitleTemplate);
				PutSetting(values, "splitScreen.title.fontScale", _splitScreenTitlePlan.FontSizeScale.ToString(CultureInfo.InvariantCulture));
				PutSetting(values, "splitScreen.title.offsetY", _splitScreenTitlePlan.CustomOffsetY);
			}
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
			_isDarkMode = GetBoolSetting(values, "ui.dark_mode", fallback: true);
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
			LoadCheckSetting(values, "merge.title.enabled", _mergeTitleEnabled);
			LoadComboIndexSetting(values, "merge.title.style", _mergeTitleStyleCombo);
			LoadTextSetting(values, "merge.title.template", _mergeTitleMainTextBox);
			LoadNumericSetting(values, "merge.title.duration", _mergeTitleDurationNum);
			string subT = GetSetting(values, "merge.title.subTemplate", null);
			if (subT != null) _mergeTitlePlan.SubTitleTemplate = subT;
			string pos = GetSetting(values, "merge.title.position", null);
			if (pos != null) _mergeTitlePlan.Position = pos;
			string fontScale = GetSetting(values, "merge.title.fontScale", null);
			if (fontScale != null && float.TryParse(fontScale, NumberStyles.Float, CultureInfo.InvariantCulture, out var fs)) _mergeTitlePlan.FontSizeScale = fs;
			string offY = GetSetting(values, "merge.title.offsetY", null);
			if (offY != null && int.TryParse(offY, NumberStyles.Integer, CultureInfo.InvariantCulture, out var oy)) _mergeTitlePlan.CustomOffsetY = oy;
			UpdateMergeTitleControlsEnabled();
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
			LoadCheckSetting(values, "splitScreen.title.enabled", _splitScreenTitleEnabled);
			LoadComboIndexSetting(values, "splitScreen.title.style", _splitScreenTitleStyleCombo);
			LoadComboIndexSetting(values, "splitScreen.title.position", _splitScreenTitlePositionCombo);
			LoadTextSetting(values, "splitScreen.title.template", _splitScreenTitleTextBox);
			LoadNumericSetting(values, "splitScreen.title.duration", _splitScreenTitleDurationNum);
			string spSubT = GetSetting(values, "splitScreen.title.subTemplate", null);
			if (spSubT != null && _splitScreenTitlePlan != null) _splitScreenTitlePlan.SubTitleTemplate = spSubT;
			string spFontScale = GetSetting(values, "splitScreen.title.fontScale", null);
			if (spFontScale != null && _splitScreenTitlePlan != null && float.TryParse(spFontScale, NumberStyles.Float, CultureInfo.InvariantCulture, out var spFs)) _splitScreenTitlePlan.FontSizeScale = spFs;
			string spOffY = GetSetting(values, "splitScreen.title.offsetY", null);
			if (spOffY != null && _splitScreenTitlePlan != null && int.TryParse(spOffY, NumberStyles.Integer, CultureInfo.InvariantCulture, out var spOy)) _splitScreenTitlePlan.CustomOffsetY = spOy;
			SyncSplitScreenTitlePlanFromUi();
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
		if (_splitScreenTitleEnabled != null) _splitScreenTitleEnabled.Enabled = !running;
		if (_splitScreenTitleStyleCombo != null) _splitScreenTitleStyleCombo.Enabled = !running && (_splitScreenTitleEnabled?.Checked ?? false);
		if (_splitScreenTitlePositionCombo != null) _splitScreenTitlePositionCombo.Enabled = !running && (_splitScreenTitleEnabled?.Checked ?? false);
		if (_splitScreenTitleTextBox != null) _splitScreenTitleTextBox.Enabled = !running && (_splitScreenTitleEnabled?.Checked ?? false);
		if (_splitScreenTitleDurationNum != null) _splitScreenTitleDurationNum.Enabled = !running && (_splitScreenTitleEnabled?.Checked ?? false);
		if (_splitScreenTitleStyleBtn != null) _splitScreenTitleStyleBtn.Enabled = !running && (_splitScreenTitleEnabled?.Checked ?? false);
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
		if (string.IsNullOrEmpty(value)) return "\"\"";
		StringBuilder sb = new StringBuilder("\"", value.Length + 16);
		for (int i = 0; i < value.Length; i++)
		{
			int backslashCount = 0;
			while (i < value.Length && value[i] == '\\')
			{
				backslashCount++;
				i++;
			}
			if (i == value.Length)
			{
				sb.Append('\\', backslashCount * 2);
				break;
			}
			if (value[i] == '"')
			{
				sb.Append('\\', backslashCount * 2 + 1);
				sb.Append('"');
			}
			else
			{
				sb.Append('\\', backslashCount);
				sb.Append(value[i]);
			}
		}
		sb.Append('"');
		return sb.ToString();
	}

	[System.Runtime.InteropServices.DllImport("user32.dll")]
	private static extern IntPtr GetFocus();

	private bool IsTextInputControlFocused()
	{
		try
		{
			IntPtr h = GetFocus();
			if (h != IntPtr.Zero)
			{
				Control c = Control.FromHandle(h) ?? Control.FromChildHandle(h);
				if (c != null)
				{
					if (c is TextBoxBase || c is ComboBox || c is UpDownBase || c is DateTimePicker)
						return true;
				}
			}
		}
		catch { }

		if (_cutEditMainTitle?.Focused == true || _cutEditSubtitle?.Focused == true ||
		    _cutEditTitleDuration?.Focused == true || _deliverOutputFileName?.Focused == true)
		{
			return true;
		}

		Control act = ActiveControl;
		while (act is ContainerControl cc && cc.ActiveControl != null)
		{
			act = cc.ActiveControl;
		}
		return act is TextBoxBase || act is ComboBox || act is UpDownBase;
	}

	private void PushCutEditUndoState(string desc = null)
	{
		if (_isPerformingUndoRedo) return;
		try
		{
			var state = new CutEditProjectState
			{
				Segments = _cutEditSegments?.Select(s => s.Clone()).ToList() ?? new List<CutSegment>(),
				Overlays = _cutEditOverlays?.Select(o => o.Clone()).ToList() ?? new List<CutOverlayItem>(),
				CurrentPos = _cutEditCurrentPos,
				SelectedOverlayId = _selectedOverlay?.Id,
				Description = desc ?? "剪辑操作"
			};
			_cutEditUndoStack.Push(state);
			if (_cutEditUndoStack.Count > 50)
			{
				var list = _cutEditUndoStack.ToList();
				list.RemoveAt(list.Count - 1);
				_cutEditUndoStack.Clear();
				for (int i = list.Count - 1; i >= 0; i--) _cutEditUndoStack.Push(list[i]);
			}
			_cutEditRedoStack.Clear();
			UpdateUndoRedoButtons();
		}
		catch { }
	}

	private void UpdateUndoRedoButtons()
	{
		if (_cutEditUndoButton != null)
		{
			_cutEditUndoButton.Enabled = _cutEditUndoStack.Count > 0;
			_cutEditUndoButton.Text = _cutEditUndoStack.Count > 0 ? $"↶ 撤销 ({_cutEditUndoStack.Count})" : "↶ 撤销 (Ctrl+Z)";
		}
		if (_cutEditRedoButton != null)
		{
			_cutEditRedoButton.Enabled = _cutEditRedoStack.Count > 0;
			_cutEditRedoButton.Text = _cutEditRedoStack.Count > 0 ? $"↷ 重做 ({_cutEditRedoStack.Count})" : "↷ 重做 (Ctrl+Y)";
		}
	}

	private void UndoCutEditAction()
	{
		if (_cutEditUndoStack.Count == 0) return;
		_isPerformingUndoRedo = true;
		try
		{
			var curState = new CutEditProjectState
			{
				Segments = _cutEditSegments?.Select(s => s.Clone()).ToList() ?? new List<CutSegment>(),
				Overlays = _cutEditOverlays?.Select(o => o.Clone()).ToList() ?? new List<CutOverlayItem>(),
				CurrentPos = _cutEditCurrentPos,
				SelectedOverlayId = _selectedOverlay?.Id,
				Description = "撤销前状态"
			};
			_cutEditRedoStack.Push(curState);

			var prev = _cutEditUndoStack.Pop();
			ApplyProjectState(prev);
			ShowCutEditToast($"↶ 已撤销: {prev.Description}");
		}
		catch { }
		finally
		{
			_isPerformingUndoRedo = false;
			UpdateUndoRedoButtons();
		}
	}

	private void RedoCutEditAction()
	{
		if (_cutEditRedoStack.Count == 0) return;
		_isPerformingUndoRedo = true;
		try
		{
			var curState = new CutEditProjectState
			{
				Segments = _cutEditSegments?.Select(s => s.Clone()).ToList() ?? new List<CutSegment>(),
				Overlays = _cutEditOverlays?.Select(o => o.Clone()).ToList() ?? new List<CutOverlayItem>(),
				CurrentPos = _cutEditCurrentPos,
				SelectedOverlayId = _selectedOverlay?.Id,
				Description = "重做前状态"
			};
			_cutEditUndoStack.Push(curState);

			var next = _cutEditRedoStack.Pop();
			ApplyProjectState(next);
			ShowCutEditToast($"↷ 已重做: {next.Description}");
		}
		catch { }
		finally
		{
			_isPerformingUndoRedo = false;
			UpdateUndoRedoButtons();
		}
	}

	private void ApplyProjectState(CutEditProjectState state)
	{
		if (state == null) return;
		_cutEditSegments.Clear();
		if (state.Segments != null)
		{
			_cutEditSegments.AddRange(state.Segments.Select(s => s.Clone()));
		}

		_cutEditOverlays.Clear();
		if (state.Overlays != null)
		{
			_cutEditOverlays.AddRange(state.Overlays.Select(o => o.Clone()));
		}

		RecalculateTimelineTotalDuration();
		RefreshCutEditSegmentList();
		RefreshOverlayCombo();

		if (!string.IsNullOrEmpty(state.SelectedOverlayId))
		{
			var match = _cutEditOverlays.FirstOrDefault(o => o.Id == state.SelectedOverlayId);
			if (match != null) SelectOverlayItem(match);
		}
		else if (_cutEditOverlays.Count > 0)
		{
			SelectOverlayItem(_cutEditOverlays[0]);
		}

		SeekCutEditVideo(state.CurrentPos);
		_cutEditTimelineCanvas?.Invalidate();
		TriggerTitleLivePreview();
	}

	private void ShowCutEditToast(string msg)
	{
		if (string.IsNullOrEmpty(msg)) return;
		if (_cutEditEstimatedDurationLabel != null)
		{
			_cutEditEstimatedDurationLabel.Text = msg;
		}
	}

	private void SaveCutEditProject()
	{
		if (_cutEditSegments.Count == 0 && _cutEditOverlays.Count == 0)
		{
			MessageBox.Show(this, "当前剪辑时间轴尚无内容，请先载入视频素材！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		if (string.IsNullOrEmpty(_cutEditCurrentProjectPath))
		{
			string projDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VideoBatchStudio", "Projects");
			try { Directory.CreateDirectory(projDir); } catch { }

			using (SaveFileDialog sfd = new SaveFileDialog())
			{
				sfd.Title = "保存剪辑工程文件";
				sfd.Filter = "剪辑工程文件 (*.vbp)|*.vbp|JSON 工程 (*.json)|*.json";
				sfd.InitialDirectory = projDir;
				sfd.FileName = $"剪辑工程_{DateTime.Now:yyyyMMdd_HHmmss}.vbp";
				if (sfd.ShowDialog(this) != DialogResult.OK) return;
				_cutEditCurrentProjectPath = sfd.FileName;
			}
		}

		try
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine($"  \"Version\": \"8.0\",");
			sb.AppendLine($"  \"SavedAt\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
			sb.AppendLine($"  \"SourcePath\": \"{EscapeJsonString(_cutEditSourcePath)}\",");
			sb.AppendLine($"  \"Duration\": {_cutEditDuration.ToString("0.00", CultureInfo.InvariantCulture)},");
			sb.AppendLine($"  \"SegmentCount\": {_cutEditSegments.Count},");
			sb.AppendLine($"  \"OverlayCount\": {_cutEditOverlays.Count}");
			sb.AppendLine("}");

			File.WriteAllText(_cutEditCurrentProjectPath, sb.ToString(), Encoding.UTF8);
			ShowCutEditToast($"💾 工程已成功保存 (Ctrl+S)！文件: {Path.GetFileName(_cutEditCurrentProjectPath)}");
			MessageBox.Show(this, $"剪辑工程已安全保存至：\n{_cutEditCurrentProjectPath}\n\n包含 {_cutEditSegments.Count} 个分段与 {_cutEditOverlays.Count} 项图文贴片包装。", "工程保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "保存工程失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private static string EscapeJsonString(string s)
	{
		if (string.IsNullOrEmpty(s)) return "";
		return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (_tabs != null && _tabs.SelectedIndex == 5)
		{
			if (keyData == (Keys.Control | Keys.S))
			{
				SaveCutEditProject();
				return true;
			}
			if (keyData == (Keys.Control | Keys.Z))
			{
				UndoCutEditAction();
				return true;
			}
			if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z))
			{
				RedoCutEditAction();
				return true;
			}

			// If user is currently focused on any text box, combo box, or number input, never swallow keys!
			if (IsTextInputControlFocused())
			{
				return base.ProcessCmdKey(ref msg, keyData);
			}

			if (keyData == Keys.Space)
			{
				ToggleCutEditPlayPause();
				return true;
			}
			if (keyData == Keys.I)
			{
				SetCutEditInPoint();
				return true;
			}
			if (keyData == Keys.O)
			{
				SetCutEditOutPoint();
				return true;
			}
			if (keyData == Keys.B)
			{
				SplitCutEditCurrentPosition();
				return true;
			}
			if (keyData == Keys.C)
			{
				SetTimelineToolMode(TimelineToolMode.Razor);
				return true;
			}
			if (keyData == Keys.V)
			{
				SetTimelineToolMode(TimelineToolMode.Select);
				return true;
			}
			if (keyData == Keys.S)
			{
				ToggleSnapping();
				return true;
			}
			if (keyData == Keys.L)
			{
				ToggleLinkedSelection();
				return true;
			}
			if (keyData == Keys.Q)
			{
				RippleTrimLeft();
				return true;
			}
			if (keyData == Keys.W)
			{
				RippleTrimRight();
				return true;
			}
			if (keyData == (Keys.Shift | Keys.Delete) || keyData == (Keys.Shift | Keys.Back))
			{
				DeleteSelectedCutSegment(isRipple: true);
				return true;
			}
			if (keyData == Keys.Delete || keyData == Keys.Back)
			{
				DeleteSelectedCutSegment(isRipple: false);
				return true;
			}
			if (keyData == Keys.J)
			{
				StepCutEditTime(-2.0);
				return true;
			}
			if (keyData == Keys.K)
			{
				if (_cutEditIsPlaying) ToggleCutEditPlayPause();
				return true;
			}
			if (keyData == Keys.Left)
			{
				StepCutEditTime(-1.0);
				return true;
			}
			if (keyData == Keys.Right)
			{
				StepCutEditTime(1.0);
				return true;
			}
			if (keyData == (Keys.Left | Keys.Shift))
			{
				StepCutEditTime(-5.0);
				return true;
			}
			if (keyData == (Keys.Right | Keys.Shift))
			{
				StepCutEditTime(5.0);
				return true;
			}
		}
		if (_tabs != null && _tabs.SelectedIndex == 6)
		{
			if (keyData == (Keys.Control | Keys.P))
			{
				OpenDeliverPopoutPreview();
				return true;
			}
			if (IsTextInputControlFocused())
			{
				return base.ProcessCmdKey(ref msg, keyData);
			}
			if (keyData == Keys.Space)
			{
				ToggleDeliverPlayPause();
				return true;
			}
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}
}
