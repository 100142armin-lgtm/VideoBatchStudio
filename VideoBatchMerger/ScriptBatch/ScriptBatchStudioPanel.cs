using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace VideoBatchMerger.ScriptBatch
{
	internal sealed class ScriptBatchStudioPanel : UserControl
	{
		private readonly MainForm _mainForm;
		private readonly ScriptBatchSettings _settings;
		private readonly List<ScriptBatchItem> _items = new List<ScriptBatchItem>();
		private readonly List<string> _footageFiles = new List<string>();

		private CancellationTokenSource _cts;
		private bool _isProcessing = false;

		// 控件引用：文案表格
		private DataGridView _grid;

		// 控件引用：语音与引擎
		private ComboBox _cboTtsEngine;
		private ComboBox _cboDefaultVoice;
		private Button _btnElevenKeys;
		private Button _btnFetchCloudVoices;
		private bool _isLoadingSettings = false;
		private Panel _pnlCustomVoiceId;
		private TextBox _txtCustomVoiceId;
		private Label _lblVoiceHint;
		private ComboBox _cboRate;
		private Button _btnAudition;
		private bool _isAuditioning = false;

		// 控件引用：团队制作人员与命名规范
		private TextBox _txtProducerName;
		private CheckBox _chkNamingRule;
		private Label _lblFileNamePreview;

		// 控件引用：画面与素材池
		private ComboBox _cboCanvasRatio;
		private ComboBox _cboFootageStrategy;
		private TextBox _txtFootageFolder;
		private Label _lblFootageCount;
		private TextBox _txtBgmPath;
		private TrackBar _trkBgmVol;
		private Label _lblBgmVol;

		// 控件引用：字幕样式与 XY 轴位置调节
		private ComboBox _cboSubtitleStyle;
		private ComboBox _cboSubtitleSize;
		private ComboBox _cboSubtitleMode;
		private TrackBar _trkSubtitlePos;
		private Label _lblSubtitlePos;
		private TrackBar _trkSubtitleX;
		private Label _lblSubtitleX;
		private Button _btnPresetLibrary;

		// 控件引用：视频水印 (文字水印 / 图片水印 与 XY 轴位置调节)
		private CheckBox _chkWatermarkEnabled;
		private ComboBox _cboWatermarkType;
		private Panel _pnlTextWatermark;
		private Panel _pnlImageWatermark;
		private TextBox _txtWatermarkText;
		private ComboBox _cboWatermarkFontSize;
		private Button _btnWatermarkColor;
		private TrackBar _trkWatermarkOpacity;
		private Label _lblWatermarkOpacity;
		private TextBox _txtWatermarkImgPath;
		private TrackBar _trkWatermarkScale;
		private Label _lblWatermarkScale;
		private TrackBar _trkWatermarkImgOpacity;
		private Label _lblWatermarkImgOpacity;
		private ComboBox _cboWatermarkPos;
		private TrackBar _trkWatermarkX;
		private Label _lblWatermarkX;
		private TrackBar _trkWatermarkY;
		private Label _lblWatermarkY;

		// 控件引用：GIF 动图贴纸
		private GifStickerItem _selectedSticker = null;
		private ListBox _lstGifStickers;
		private Panel _pnlStickerDetails;
		private TextBox _txtStickerPath;
		private TrackBar _trkStickerX;
		private Label _lblStickerX;
		private TrackBar _trkStickerY;
		private Label _lblStickerY;
		private TrackBar _trkStickerScale;
		private Label _lblStickerScale;
		private TrackBar _trkStickerRotate;
		private Label _lblStickerRotate;
		private TrackBar _trkStickerOpacity;
		private Label _lblStickerOpacity;
		private NumericUpDown _numStickerStart;
		private NumericUpDown _numStickerDuration;
		private CheckBox _chkStickerLoop;
		private readonly Dictionary<string, Image> _stickerImageCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

		// 控件引用：输出与控制台
		private TextBox _txtOutputDir;
		private Button _btnStartBatch;
		private Button _btnStopBatch;
		private ProgressBar _progressBar;
		private Label _lblStatusText;
		private Button _btnOpenOutput;
		private Button _btnPreviewSelected;

		// 实时预览视窗 (WebView2 60FPS 所见即所得引擎 + GDI+ 兜底)
		private PreviewCanvasBox _previewCanvas;
		private Image _cachedFootageThumbnail = null;
		private string _cachedFootageThumbnailBase64 = null;
		private Microsoft.Web.WebView2.WinForms.WebView2 _webViewPreview;
		private bool _isWebViewReady = false;
		private bool _isWebViewUpdating = false;

		// 调色板
		private static readonly Color BgCanvas = Color.FromArgb(10, 15, 26);
		private static readonly Color CardBg = Color.FromArgb(17, 24, 39);
		private static readonly Color CardBorder = Color.FromArgb(31, 41, 55);
		private static readonly Color InputBg = Color.FromArgb(24, 34, 53);
		private static readonly Color TextPrimary = Color.FromArgb(243, 244, 246);
		private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
		private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
		private static readonly Color AccentYellow = Color.FromArgb(245, 158, 11);
		private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
		private static readonly Color AccentOrange = Color.FromArgb(234, 88, 12);

		public ScriptBatchStudioPanel(MainForm mainForm)
		{
			_mainForm = mainForm;
			_settings = ScriptBatchSettings.Load();

			Dock = DockStyle.Fill;
			BackColor = BgCanvas;
			Font = new Font("Microsoft YaHei UI", 9f);

			InitializeUi();
			ApplyLoadedSettings();
			ScanFootageFolder();
			UpdateKeyButtonText();
			UpdatePreview();
			UpdateFileNamePreview();

			if (_mainForm != null)
			{
				_mainForm.FormClosing += (_, _) => SaveSettings();
			}
		}

		private void InitializeUi()
		{
			// 三栏可拖拽布局：左(文案表格) | 中(实时预览) | 右(参数控制台)
			// 栏与栏之间各有一条 6px 拖拽分隔条，鼠标悬停变为 ↔ 光标，拖动时三栏内容实时等比例缩放
			var host = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = BgCanvas
			};

			var leftPanel = BuildLeftScriptGridPanel();
			var centerPanel = BuildCenterPreviewPanel();
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
			const int MinLeft = 320, MinCenter = 260, MinRight = 300;

			double leftRatio = _settings.LayoutLeftRatio > 0.1 && _settings.LayoutLeftRatio < 0.8 ? _settings.LayoutLeftRatio : 0.38;
			double rightRatio = _settings.LayoutRightRatio > 0.1 && _settings.LayoutRightRatio < 0.8 ? _settings.LayoutRightRatio : 0.28;

			void Relayout()
			{
				int totalW = host.ClientSize.Width;
				int h = host.ClientSize.Height;
				if (totalW <= 0 || h <= 0) return;

				int usable = Math.Max(0, totalW - SplitW * 2);
				int lw = (int)Math.Round(usable * leftRatio);
				int rw = (int)Math.Round(usable * rightRatio);

				// 最小宽度保护 (窗口过窄时按比例让步)
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
					splitter.BackColor = Color.FromArgb(59, 130, 246);
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
					host.Update(); // 拖动过程中立即重绘，实时看到缩放效果
				};
				splitter.MouseUp += (_, _) =>
				{
					if (!dragging) return;
					dragging = false;
					splitter.Capture = false;
					splitter.BackColor = Color.FromArgb(30, 41, 59);
					_settings.LayoutLeftRatio = leftRatio;
					_settings.LayoutRightRatio = rightRatio;
					SaveSettings();
				};
				// 双击分隔条恢复默认比例
				splitter.DoubleClick += (_, _) =>
				{
					leftRatio = 0.38;
					rightRatio = 0.28;
					Relayout();
					_settings.LayoutLeftRatio = leftRatio;
					_settings.LayoutRightRatio = rightRatio;
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
			// 中间绘制三个小圆点作为抓手提示
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
				Height = 44,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = false,
				Padding = new Padding(0, 0, 0, 6)
			};

			var btnPaste = CreateButton("📋 粘贴剪贴板 (Ctrl+V)", AccentBlue, Color.White, 160);
			btnPaste.Click += (_, _) => PasteClipboardData();
			topBar.Controls.Add(btnPaste);

			var btnImport = CreateButton("📄 导入表格/TXT", CardBorder, TextPrimary, 120);
			btnImport.Click += (_, _) => ImportTextOrCsvFile();
			topBar.Controls.Add(btnImport);

			var btnAddRow = CreateButton("➕ 新建空行", CardBorder, TextPrimary, 95);
			btnAddRow.Click += (_, _) => AddEmptyRow();
			topBar.Controls.Add(btnAddRow);

			var btnDelete = CreateButton("🗑️ 删除选中", CardBorder, TextPrimary, 90);
			btnDelete.Click += (_, _) => DeleteSelectedRows();
			topBar.Controls.Add(btnDelete);

			var btnClear = CreateButton("🧹 全部清空", CardBorder, TextPrimary, 90);
			btnClear.Click += (_, _) => ClearAllRows();
			topBar.Controls.Add(btnClear);

			panel.Controls.Add(topBar);

			// 表格组件
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
				ColumnHeadersHeight = 38
			};

			_grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(24, 34, 53);
			_grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
			_grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
			_grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

			_grid.DefaultCellStyle.BackColor = CardBg;
			_grid.DefaultCellStyle.ForeColor = TextPrimary;
			_grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 99, 235);
			_grid.DefaultCellStyle.SelectionForeColor = Color.White;
			_grid.RowTemplate.Height = 34;

			var colId = new DataGridViewTextBoxColumn { HeaderText = "#", Width = 45, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
			var colCode = new DataGridViewTextBoxColumn { HeaderText = "文案代码", Width = 85, DefaultCellStyle = { ForeColor = AccentYellow } };
			var colTitle = new DataGridViewTextBoxColumn { HeaderText = "中文文案/标题", Width = 145 };
			var colScript = new DataGridViewTextBoxColumn { HeaderText = "目标配音文案 (葡语)", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
			var colVoice = new DataGridViewTextBoxColumn { HeaderText = "指定音色", Width = 150 };
			var colStatus = new DataGridViewTextBoxColumn { HeaderText = "状态", Width = 85, ReadOnly = true };
			var colQc = new DataGridViewTextBoxColumn
			{
				HeaderText = "4重质检雷达 (点击查看)",
				Width = 145,
				ReadOnly = true,
				DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
			};

			_grid.Columns.AddRange(colId, colCode, colTitle, colScript, colVoice, colStatus, colQc);

			_grid.CellClick += (s, e) =>
			{
				if (e.RowIndex >= 0 && e.ColumnIndex == 6)
				{
					if (_grid.Rows[e.RowIndex].Tag is ScriptBatchItem item)
					{
						using var dlg = new QcReportDialog(item);
						dlg.ShowDialog(this);
					}
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
				UpdatePreview();
				UpdateFileNamePreview();
			};

			_grid.CellValueChanged += (s, e) =>
			{
				SyncGridEditsToItems();
				UpdateFileNamePreview();
				UpdatePreview();
			};

			panel.Controls.Add(_grid);
			_grid.BringToFront();

			return panel;
		}

		private Panel BuildCenterPreviewPanel()
		{
			var panel = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(6, 10, 6, 10),
				BackColor = BgCanvas
			};

			var container = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = CardBg,
				Padding = new Padding(10)
			};

			// 顶部栏：预览标题 + 右侧【🎨 字幕预设库 (底框/动效)】按钮
			var headerPanel = new Panel
			{
				Dock = DockStyle.Top,
				Height = 36,
				BackColor = Color.FromArgb(20, 28, 44),
				Padding = new Padding(10, 4, 10, 4)
			};

			var lblTitle = new Label
			{
				Dock = DockStyle.Left,
				AutoSize = true,
				Text = "📱 手机成片实时预览 (9:16 竖屏 · 画面/字幕/水印)",
				Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold),
				ForeColor = AccentYellow,
				TextAlign = ContentAlignment.MiddleLeft
			};
			headerPanel.Controls.Add(lblTitle);

			_btnPresetLibrary = CreateButton("🎨 字幕预设库 (底框/动效)", AccentOrange, Color.White, 168);
			_btnPresetLibrary.Dock = DockStyle.Right;
			_btnPresetLibrary.Height = 28;
			_btnPresetLibrary.Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold);
			_btnPresetLibrary.Click += (_, _) =>
			{
				using var dlg = new SubtitlePresetDialog(_settings, () =>
				{
					ApplyLoadedSettings();
					UpdatePreview();
				});
				dlg.ShowDialog(this);
			};
			headerPanel.Controls.Add(_btnPresetLibrary);

			// 底部控件区：字幕位置微调 + 实时命名预览 (高度 74px，双行精准对齐，绝不换行遮挡)
			var bottomContainer = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 74,
				BackColor = Color.FromArgb(20, 28, 44),
				Padding = new Padding(8, 4, 8, 4)
			};

			// 第1行：Y轴垂直滑杆 + [底 82%] [中 50%] [顶 20%]
			var rowY = new Panel
			{
				Dock = DockStyle.Top,
				Height = 25,
				Margin = new Padding(0)
			};
			_lblSubtitlePos = new Label
			{
				Dock = DockStyle.Left,
				Width = 82,
				TextAlign = ContentAlignment.MiddleLeft,
				Text = $"↕️ 垂直: {_settings.SubtitleVerticalPercent}%",
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold)
			};
			var btnYGroup = new FlowLayoutPanel
			{
				Dock = DockStyle.Right,
				AutoSize = true,
				WrapContents = false,
				Margin = new Padding(0),
				Padding = new Padding(0)
			};
			var btnBottom = CreateButton("底 82%", CardBorder, TextPrimary, 48);
			btnBottom.Height = 22;
			btnBottom.Font = new Font("Microsoft YaHei UI", 7.8f);
			btnBottom.Click += (_, _) =>
			{
				_trkSubtitlePos.Value = 82;
				_settings.SubtitleVerticalPercent = 82;
				_lblSubtitlePos.Text = "↕️ 垂直: 82%";
				UpdatePreview();
				SaveSettings();
			};
			btnYGroup.Controls.Add(btnBottom);

			var btnMid = CreateButton("中 50%", CardBorder, TextPrimary, 48);
			btnMid.Height = 22;
			btnMid.Font = new Font("Microsoft YaHei UI", 7.8f);
			btnMid.Click += (_, _) =>
			{
				_trkSubtitlePos.Value = 50;
				_settings.SubtitleVerticalPercent = 50;
				_lblSubtitlePos.Text = "↕️ 垂直: 50%";
				UpdatePreview();
				SaveSettings();
			};
			btnYGroup.Controls.Add(btnMid);

			var btnTop = CreateButton("顶 20%", CardBorder, TextPrimary, 48);
			btnTop.Height = 22;
			btnTop.Font = new Font("Microsoft YaHei UI", 7.8f);
			btnTop.Click += (_, _) =>
			{
				_trkSubtitlePos.Value = 20;
				_settings.SubtitleVerticalPercent = 20;
				_lblSubtitlePos.Text = "↕️ 垂直: 20%";
				UpdatePreview();
				SaveSettings();
			};
			btnYGroup.Controls.Add(btnTop);

			_trkSubtitlePos = new TrackBar
			{
				Dock = DockStyle.Fill,
				Height = 22,
				TickStyle = TickStyle.None,
				Minimum = 15,
				Maximum = 88,
				Value = Math.Max(15, Math.Min(88, _settings.SubtitleVerticalPercent))
			};
			WireLiveTrackBar(_trkSubtitlePos, val =>
			{
				_settings.SubtitleVerticalPercent = val;
				_lblSubtitlePos.Text = $"↕️ 垂直: {val}%";
			}, () => SaveSettings());

			rowY.Controls.Add(_trkSubtitlePos);
			rowY.Controls.Add(btnYGroup);
			rowY.Controls.Add(_lblSubtitlePos);

			// 第2行：X轴水平滑杆 + [🎯 居中 0]
			var rowX = new Panel
			{
				Dock = DockStyle.Top,
				Height = 25,
				Margin = new Padding(0)
			};
			_lblSubtitleX = new Label
			{
				Dock = DockStyle.Left,
				Width = 82,
				TextAlign = ContentAlignment.MiddleLeft,
				Text = $"↔️ 水平: {_settings.SubtitleOffsetX}px",
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold)
			};
			var btnResetX = CreateButton("🎯 居中 0", CardBorder, TextPrimary, 58);
			btnResetX.Dock = DockStyle.Right;
			btnResetX.Height = 22;
			btnResetX.Font = new Font("Microsoft YaHei UI", 7.8f);
			btnResetX.Click += (_, _) =>
			{
				_trkSubtitleX.Value = 0;
				_settings.SubtitleOffsetX = 0;
				_lblSubtitleX.Text = "↔️ 水平: 0px";
				UpdatePreview();
				SaveSettings();
			};

			_trkSubtitleX = new TrackBar
			{
				Dock = DockStyle.Fill,
				Height = 22,
				TickStyle = TickStyle.None,
				Minimum = -350,
				Maximum = 350,
				Value = Math.Max(-350, Math.Min(350, _settings.SubtitleOffsetX))
			};
			WireLiveTrackBar(_trkSubtitleX, val =>
			{
				_settings.SubtitleOffsetX = val;
				_lblSubtitleX.Text = $"↔️ 水平: {val}px";
			}, () => SaveSettings());

			rowX.Controls.Add(_trkSubtitleX);
			rowX.Controls.Add(btnResetX);
			rowX.Controls.Add(_lblSubtitleX);

			// 第3行：实时命名预览行
			_lblFileNamePreview = new Label
			{
				Dock = DockStyle.Bottom,
				Height = 18,
				Text = "📌 导出成片命名预览: 加载中...",
				ForeColor = Color.FromArgb(147, 197, 253),
				Font = new Font("Microsoft YaHei UI", 8f),
				AutoEllipsis = true
			};

			bottomContainer.Controls.Add(_lblFileNamePreview);
			bottomContainer.Controls.Add(rowX);
			bottomContainer.Controls.Add(rowY);

			// 手机模拟大屏画布 (全景真机渲染)
			_previewCanvas = new PreviewCanvasBox(this)
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(7, 10, 18)
			};

			// 确保层级严格生效：Top 与 Bottom 靠前停靠，Fill 纯净填充中间区域，绝不遮挡底部微调按钮
			container.Controls.Add(_previewCanvas);
			container.Controls.Add(bottomContainer);
			container.Controls.Add(headerPanel);
			headerPanel.BringToFront();
			bottomContainer.BringToFront();
			_previewCanvas.SendToBack();

			try
			{
				_webViewPreview = new Microsoft.Web.WebView2.WinForms.WebView2
				{
					Dock = DockStyle.Fill,
					Visible = false
				};
				container.Controls.Add(_webViewPreview);
				_webViewPreview.BringToFront();

				InitializeWebViewAsync();
			}
			catch (Exception ex)
			{
				Debug.WriteLine("WebView2 control creation error: " + ex.Message);
			}

			panel.Controls.Add(container);
			return panel;
		}

		private Panel BuildRightSettingsPanel()
		{
			var outer = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(6, 6, 6, 6),
				BackColor = BgCanvas
			};

			var scrollContainer = new SmoothScrollPanel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				BackColor = CardBg,
				Padding = new Padding(8, 6, 8, 8)
			};

			var flow = new FlowLayoutPanel
			{
				Dock = DockStyle.Top,
				AutoSize = true,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				Width = 320
			};

			// ========================================================
			// 0. 置顶核心控制台: 批量交付流水线 (开始生产 / 停止 / 进度 / 输出目录)
			// 用户特别要求: 这些最核心的操作按钮置顶在最上方，触手可及
			// ========================================================
			var pnlTopExec = new Panel
			{
				Width = 310,
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
				Width = 294
			};

			topFlow.Controls.Add(CreateSectionHeader("🚀 批量交付流水线 (一键成片)"));

			topFlow.Controls.Add(CreateLabel("输出保存目录:"));
			var outRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			_txtOutputDir = new TextBox { Width = 225, BackColor = InputBg, ForeColor = TextPrimary, BorderStyle = BorderStyle.FixedSingle };
			_txtOutputDir.TextChanged += (_, _) => SaveSettings();
			outRow.Controls.Add(_txtOutputDir);

			var btnBrowseOut = CreateButton("浏览...", AccentBlue, Color.White, 70);
			btnBrowseOut.Click += (_, _) => BrowseOutputFolder();
			outRow.Controls.Add(btnBrowseOut);
			topFlow.Controls.Add(outRow);

			_progressBar = new ProgressBar { Width = 300, Height = 12, Style = ProgressBarStyle.Continuous, Margin = new Padding(0, 8, 0, 4) };
			topFlow.Controls.Add(_progressBar);

			_lblStatusText = new Label { Text = "准备就绪: 调节参数所见即所得，随时可批量出片。", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
			topFlow.Controls.Add(_lblStatusText);

			var actionRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 2, 0, 4) };
			_btnStartBatch = CreateButton("🎬 开始批量生产成品视频", Color.FromArgb(37, 99, 235), Color.White, 210);
			_btnStartBatch.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
			_btnStartBatch.Height = 36;
			_btnStartBatch.Click += async (_, _) => await StartBatchProcessingAsync();
			actionRow.Controls.Add(_btnStartBatch);

			_btnStopBatch = CreateButton("⏹️ 停止", Color.FromArgb(220, 38, 38), Color.White, 85);
			_btnStopBatch.Height = 36;
			_btnStopBatch.Enabled = false;
			_btnStopBatch.Click += (_, _) => StopBatchProcessing();
			actionRow.Controls.Add(_btnStopBatch);
			topFlow.Controls.Add(actionRow);

			var bottomTools = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 2) };
			_btnPreviewSelected = CreateButton("🔍 极速大屏预览选中成品", CardBorder, TextPrimary, 165);
			_btnPreviewSelected.Click += (_, _) => PreviewSelectedVideo();
			bottomTools.Controls.Add(_btnPreviewSelected);

			_btnOpenOutput = CreateButton("📁 打开输出目录", CardBorder, TextPrimary, 130);
			_btnOpenOutput.Click += (_, _) => OpenOutputFolder();
			bottomTools.Controls.Add(_btnOpenOutput);
			topFlow.Controls.Add(bottomTools);

			pnlTopExec.Controls.Add(topFlow);
			void UpdateTopExecHeight()
			{
				topFlow.PerformLayout();
				pnlTopExec.Height = topFlow.PreferredSize.Height + 20;
			}
			topFlow.SizeChanged += (_, _) => UpdateTopExecHeight();
			UpdateTopExecHeight();
			flow.Controls.Add(pnlTopExec);

			// 1. 语音引擎与高保真音色 (默认展开)
			var voiceFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 310 };
			voiceFlow.Controls.Add(CreateLabel("TTS 配音服务商引擎:"));
			_cboTtsEngine = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = 300,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat
			};
			_cboTtsEngine.Items.AddRange(new object[]
			{
				"🌐 微软 Edge-TTS (完全免费 · 纯正葡语神父腔 · 0额度限制)",
				"✨ ElevenLabs 官方 API (超自然情感 · 多Key轮询 · 0额度试听)"
			});
			_cboTtsEngine.SelectedIndexChanged += (_, _) =>
			{
				int idx = _cboTtsEngine.SelectedIndex;
				_settings.TtsEngine = idx == 1 ? "elevenlabs" : "edge_tts";
				_settings.Save();
				UpdateVoiceDropdownItems();
			};
			voiceFlow.Controls.Add(_cboTtsEngine);

			var keyActionRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 4) };
			_btnElevenKeys = CreateButton("🔑 ElevenLabs 密钥管理", AccentBlue, Color.White, 190);
			_btnElevenKeys.Click += (_, _) =>
			{
				using var dlg = new ElevenLabsKeyDialog();
				dlg.ShowDialog(this);
				UpdateKeyButtonText();
			};
			keyActionRow.Controls.Add(_btnElevenKeys);

			_btnFetchCloudVoices = CreateButton("☁️ 同步声音", CardBorder, TextPrimary, 105);
			_btnFetchCloudVoices.Click += async (_, _) => await FetchCloudVoicesAsync();
			keyActionRow.Controls.Add(_btnFetchCloudVoices);
			voiceFlow.Controls.Add(keyActionRow);

			voiceFlow.Controls.Add(CreateLabel("音色精选:"));
			_cboDefaultVoice = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = 300,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat
			};
			_cboDefaultVoice.SelectedIndexChanged += (_, _) =>
			{
				SaveSettings();
				UpdatePreview();

				if (_cboDefaultVoice.SelectedItem is VoiceOption vo)
				{
					foreach (var it in _items)
					{
						if (it.Status == "等待中" || it.Status == "语音失败" || it.Status.Contains("失败"))
						{
							it.VoiceKey = vo.Key;
						}
					}
					for (int r = 0; r < _grid.Rows.Count; r++)
					{
						if (_grid.Rows[r].Tag is ScriptBatchItem rowItem &&
							(rowItem.Status == "等待中" || rowItem.Status == "语音失败" || rowItem.Status.Contains("失败")))
						{
							_grid.Rows[r].Cells[4].Value = _cboDefaultVoice.Text;
						}
					}
				}
			};
			voiceFlow.Controls.Add(_cboDefaultVoice);

			_lblVoiceHint = new Label
			{
				Text = "💡 提示: ElevenLabs 采用多密钥池智能轮询；试听使用官方云端 CDN 零额度直连播放，绝不消耗任何字符配额！",
				ForeColor = AccentYellow,
				Font = new Font("Microsoft YaHei UI", 7.5f),
				Width = 300,
				Height = 36,
				Margin = new Padding(0, 2, 0, 4)
			};
			voiceFlow.Controls.Add(_lblVoiceHint);

			_pnlCustomVoiceId = new Panel { AutoSize = true, Visible = false, Margin = new Padding(0, 2, 0, 4) };
			_pnlCustomVoiceId.Controls.Add(new Label { Text = "自定义 Voice ID:", ForeColor = AccentYellow, AutoSize = true });
			_txtCustomVoiceId = new TextBox
			{
				Width = 300,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Location = new Point(0, 18)
			};
			_txtCustomVoiceId.TextChanged += (_, _) => SaveSettings();
			_pnlCustomVoiceId.Controls.Add(_txtCustomVoiceId);
			voiceFlow.Controls.Add(_pnlCustomVoiceId);

			var rateRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			rateRow.Controls.Add(new Label { Text = "语速: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
			_cboRate = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = 100,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat
			};
			_cboRate.Items.AddRange(new object[] { "正常 (1.0x)", "略快 (1.05x)", "快速 (1.10x)", "沉缓 (0.95x)" });
			_cboRate.SelectedIndexChanged += (_, _) => SaveSettings();
			rateRow.Controls.Add(_cboRate);

			_btnAudition = CreateButton("🔊 极速试听", AccentBlue, Color.White, 95);
			_btnAudition.Click += async (_, _) => await AuditionVoiceAsync();
			rateRow.Controls.Add(_btnAudition);
			voiceFlow.Controls.Add(rateRow);

			flow.Controls.Add(CreateCollapsibleCard("🎙️ 语音引擎与高保真音色", voiceFlow, true));

			// 2. 字幕视觉模板与预设库 (默认展开)
			var subFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 310 };
			var btnOpenPreset2 = CreateButton("🎨 浏览动态字幕预设库 (底框/卡拉OK/动效)", AccentOrange, Color.White, 300);
			btnOpenPreset2.Height = 34;
			btnOpenPreset2.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
			btnOpenPreset2.Click += (_, _) =>
			{
				using var dlg = new SubtitlePresetDialog(_settings, () =>
				{
					ApplyLoadedSettings();
					UpdatePreview();
				});
				dlg.ShowDialog(this);
			};
			subFlow.Controls.Add(btnOpenPreset2);

			_cboSubtitleStyle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, BackColor = InputBg, ForeColor = TextPrimary, FlatStyle = FlatStyle.Flat };
			foreach (var p in SubtitlePresetCatalog.Presets)
			{
				_cboSubtitleStyle.Items.Add($"[{p.Category}] {p.Name}");
			}
			_cboSubtitleStyle.SelectedIndexChanged += (_, _) =>
			{
				if (_cboSubtitleStyle.SelectedIndex >= 0 && _cboSubtitleStyle.SelectedIndex < SubtitlePresetCatalog.Presets.Count)
				{
					var sel = SubtitlePresetCatalog.Presets[_cboSubtitleStyle.SelectedIndex];
					_settings.SubtitleStyleId = sel.Id;
					_settings.SubtitleColorHex = sel.PrimaryColorHex;
					_settings.SubtitleStrokeColorHex = sel.OutlineColorHex;
					_settings.SubtitleStrokeWidth = sel.OutlineWidth;
					_settings.SubtitleBorderStyle = sel.BorderStyle;
					_settings.SubtitleDisplayMode = sel.DisplayMode;
					_settings.Save();
					UpdatePreview();
				}
			};
			subFlow.Controls.Add(_cboSubtitleStyle);

			var subOptRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 2) };
			subOptRow.Controls.Add(new Label { Text = "字号: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
			_cboSubtitleSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, BackColor = InputBg, ForeColor = TextPrimary, FlatStyle = FlatStyle.Flat };
			_cboSubtitleSize.Items.AddRange(new object[] { "68 px (精简小号)", "78 px (常规标配)", "86 px (推荐爆款)", "96 px (醒目加粗)", "108 px (超大视效)" });
			_cboSubtitleSize.SelectedIndexChanged += (_, _) => { SaveSettings(); UpdatePreview(); };
			subOptRow.Controls.Add(_cboSubtitleSize);

			subOptRow.Controls.Add(new Label { Text = "模式: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(6, 4, 2, 0) });
			_cboSubtitleMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, BackColor = InputBg, ForeColor = TextPrimary, FlatStyle = FlatStyle.Flat };
			_cboSubtitleMode.Items.AddRange(new object[]
			{
				"0. 句级自然居中",
				"1. 逐词高亮跳动",
				"2. 动感回弹弹出",
				"3. 逐词红底框高亮 (爆款底框·如同图1)"
			});
			_cboSubtitleMode.SelectedIndexChanged += (_, _) =>
			{
				_settings.SubtitleDisplayMode = _cboSubtitleMode.SelectedIndex;
				SaveSettings();
				UpdatePreview();
			};
			subOptRow.Controls.Add(_cboSubtitleMode);
			subFlow.Controls.Add(subOptRow);

			flow.Controls.Add(CreateCollapsibleCard("🎨 字幕视觉模板与排版", subFlow, true));

			// 3. 背景素材池 (智能增剪与对齐) (默认折叠)
			var footageFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 310 };
			var footageRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			_txtFootageFolder = new TextBox { Width = 225, BackColor = InputBg, ForeColor = TextPrimary, BorderStyle = BorderStyle.FixedSingle };
			_txtFootageFolder.TextChanged += (_, _) => { ScanFootageFolder(); SaveSettings(); UpdatePreview(); };
			footageRow.Controls.Add(_txtFootageFolder);

			var btnBrowseFootage = CreateButton("浏览...", AccentBlue, Color.White, 70);
			btnBrowseFootage.Click += (_, _) => BrowseFootageFolder();
			footageRow.Controls.Add(btnBrowseFootage);
			footageFlow.Controls.Add(footageRow);

			_lblFootageCount = new Label { Text = "素材目录未设置", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
			footageFlow.Controls.Add(_lblFootageCount);

			var stratRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			stratRow.Controls.Add(new Label { Text = "抽取策略: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
			_cboFootageStrategy = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 205, BackColor = InputBg, ForeColor = TextPrimary, FlatStyle = FlatStyle.Flat };
			_cboFootageStrategy.Items.AddRange(new object[] { "🎲 智能去重随机抽选 (推荐)", "📁 按素材文件名顺序拼接" });
			_cboFootageStrategy.SelectedIndexChanged += (_, _) => SaveSettings();
			stratRow.Controls.Add(_cboFootageStrategy);
			footageFlow.Controls.Add(stratRow);

			var ratioRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			ratioRow.Controls.Add(new Label { Text = "画布比例: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
			_cboCanvasRatio = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 205, BackColor = InputBg, ForeColor = TextPrimary, FlatStyle = FlatStyle.Flat };
			_cboCanvasRatio.Items.AddRange(new object[] { "📱 9:16 竖屏 (1080x1920) TikTok/Reels", "🖥️ 16:9 横屏 (1920x1080) YouTube" });
			_cboCanvasRatio.SelectedIndexChanged += (_, _) => { SaveSettings(); UpdatePreview(); };
			ratioRow.Controls.Add(_cboCanvasRatio);
			footageFlow.Controls.Add(ratioRow);

			flow.Controls.Add(CreateCollapsibleCard("🎬 背景素材池 (智能增剪与对齐)", footageFlow, false));

			// 4. 背景音乐 (BGM 自动闪避人声) (默认折叠)
			var bgmFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 310 };
			var bgmRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			_txtBgmPath = new TextBox { Width = 225, BackColor = InputBg, ForeColor = TextPrimary, BorderStyle = BorderStyle.FixedSingle };
			_txtBgmPath.TextChanged += (_, _) => SaveSettings();
			bgmRow.Controls.Add(_txtBgmPath);

			var btnBrowseBgm = CreateButton("浏览...", CardBorder, TextPrimary, 70);
			btnBrowseBgm.Click += (_, _) => BrowseBgmFile();
			bgmRow.Controls.Add(btnBrowseBgm);
			bgmFlow.Controls.Add(bgmRow);

			var bgmVolRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			bgmVolRow.Controls.Add(new Label { Text = "BGM音量: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
			_trkBgmVol = new TrackBar { Minimum = 5, Maximum = 40, Value = 12, TickFrequency = 5, Width = 130, Height = 28 };
			_lblBgmVol = new Label { Text = "12%", ForeColor = TextPrimary, AutoSize = true, Margin = new Padding(2, 6, 0, 0) };
			WireLiveTrackBar(_trkBgmVol, val => { _lblBgmVol.Text = $"{val}%"; }, () => SaveSettings());
			bgmVolRow.Controls.Add(_trkBgmVol);
			bgmVolRow.Controls.Add(_lblBgmVol);
			bgmFlow.Controls.Add(bgmVolRow);

			flow.Controls.Add(CreateCollapsibleCard("🎵 背景音乐 (BGM 自动闪避人声)", bgmFlow, false));

			// 5. 视频水印 (防搬运 / 署名) (默认折叠)
			var wmFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 310 };
			_chkWatermarkEnabled = new CheckBox
			{
				Text = "开启成片水印叠加 (调试时中间屏幕全时显示)",
				ForeColor = AccentGreen,
				Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
				AutoSize = true,
				Checked = _settings.WatermarkEnabled,
				Margin = new Padding(0, 2, 0, 4)
			};
			_chkWatermarkEnabled.CheckedChanged += (_, _) =>
			{
				_settings.WatermarkEnabled = _chkWatermarkEnabled.Checked;
				_settings.Save();
				UpdatePreview();
			};
			wmFlow.Controls.Add(_chkWatermarkEnabled);

			var wmTypeRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			wmTypeRow.Controls.Add(new Label { Text = "类型: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
			_cboWatermarkType = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = 230,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat
			};
			_cboWatermarkType.Items.AddRange(new object[]
			{
				"🔤 文字水印 (如 Voice: ElevenLabs)",
				"🖼️ 图片水印 (LOGO / 图章)"
			});
			_cboWatermarkType.SelectedIndexChanged += (_, _) =>
			{
				_settings.WatermarkType = _cboWatermarkType.SelectedIndex;
				_pnlTextWatermark.Visible = _settings.WatermarkType == 0;
				_pnlImageWatermark.Visible = _settings.WatermarkType == 1;
				_settings.Save();
				UpdatePreview();
			};
			wmTypeRow.Controls.Add(_cboWatermarkType);
			wmFlow.Controls.Add(wmTypeRow);

			// 文字水印控制面板
			_pnlTextWatermark = new Panel { AutoSize = true, Width = 300, Margin = new Padding(0, 4, 0, 4) };
			var textWmFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };

			var txtWmRow1 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 0, 0, 2) };
			txtWmRow1.Controls.Add(new Label { Text = "水印文字 (支持回车换行 / 超长自动折行):", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 2, 0, 2) });
			_txtWatermarkText = new TextBox
			{
				Width = 285,
				Height = 52,
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				AcceptsReturn = true,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Text = _settings.WatermarkText
			};
			_txtWatermarkText.TextChanged += (_, _) =>
			{
				_settings.WatermarkText = _txtWatermarkText.Text;
				_chkWatermarkEnabled.Checked = true;
				_settings.Save();
				UpdatePreview();
			};
			txtWmRow1.Controls.Add(_txtWatermarkText);
			textWmFlow.Controls.Add(txtWmRow1);

			var txtWmRow2 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 0) };
			txtWmRow2.Controls.Add(new Label { Text = "字号: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
			_cboWatermarkFontSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115, BackColor = InputBg, ForeColor = TextPrimary, FlatStyle = FlatStyle.Flat };
			_cboWatermarkFontSize.Items.AddRange(new object[] { "22 px (极简小标)", "28 px (标准标配)", "36 px (清晰中号)", "46 px (显眼大号)", "60 px (主视觉标)" });
			_cboWatermarkFontSize.SelectedIndexChanged += (_, _) =>
			{
				_settings.WatermarkFontSize = _cboWatermarkFontSize.SelectedIndex switch
				{
					0 => 22,
					2 => 36,
					3 => 46,
					4 => 60,
					_ => 28
				};
				_chkWatermarkEnabled.Checked = true;
				_settings.Save();
				UpdatePreview();
			};
			txtWmRow2.Controls.Add(_cboWatermarkFontSize);

			txtWmRow2.Controls.Add(new Label { Text = " 颜色: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(6, 4, 2, 0) });
			_btnWatermarkColor = CreateButton("■ 纯白", CardBorder, Color.White, 75);
			_btnWatermarkColor.Click += (_, _) =>
			{
				using var cd = new ColorDialog();
				if (cd.ShowDialog(this) == DialogResult.OK)
				{
					_settings.WatermarkTextColorHex = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
					_btnWatermarkColor.ForeColor = cd.Color;
					_chkWatermarkEnabled.Checked = true;
					_settings.Save();
					UpdatePreview();
				}
			};
			txtWmRow2.Controls.Add(_btnWatermarkColor);
			textWmFlow.Controls.Add(txtWmRow2);

			var txtWmRow3 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 0) };
			txtWmRow3.Controls.Add(new Label { Text = "透明度: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
			_trkWatermarkOpacity = new TrackBar { Minimum = 20, Maximum = 100, Value = Math.Max(20, Math.Min(100, _settings.WatermarkTextOpacity)), Width = 150, Height = 28, TickFrequency = 10 };
			_lblWatermarkOpacity = new Label { Text = $"{_trkWatermarkOpacity.Value}%", ForeColor = TextPrimary, AutoSize = true, Margin = new Padding(2, 4, 0, 0) };
			WireLiveTrackBar(_trkWatermarkOpacity, val =>
			{
				_settings.WatermarkTextOpacity = val;
				_lblWatermarkOpacity.Text = $"{val}%";
				_chkWatermarkEnabled.Checked = true;
			});
			txtWmRow3.Controls.Add(_trkWatermarkOpacity);
			txtWmRow3.Controls.Add(_lblWatermarkOpacity);
			textWmFlow.Controls.Add(txtWmRow3);

			_pnlTextWatermark.Controls.Add(textWmFlow);
			wmFlow.Controls.Add(_pnlTextWatermark);

			// 图片水印控制面板
			_pnlImageWatermark = new Panel { AutoSize = true, Width = 300, Visible = false, Margin = new Padding(0, 4, 0, 4) };
			var imgWmFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };

			var imgWmRow1 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			_txtWatermarkImgPath = new TextBox { Width = 205, BackColor = InputBg, ForeColor = TextPrimary, BorderStyle = BorderStyle.FixedSingle, Text = _settings.WatermarkImagePath };
			_txtWatermarkImgPath.TextChanged += (_, _) => { _settings.WatermarkImagePath = _txtWatermarkImgPath.Text; _chkWatermarkEnabled.Checked = true; _settings.Save(); UpdatePreview(); };
			imgWmRow1.Controls.Add(_txtWatermarkImgPath);
			var btnBrowseWmImg = CreateButton("浏览...", AccentBlue, Color.White, 70);
			btnBrowseWmImg.Click += (_, _) =>
			{
				using var ofd = new OpenFileDialog { Title = "选择水印图片", Filter = "图片文件 (*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp|所有文件 (*.*)|*.*" };
				if (ofd.ShowDialog(this) == DialogResult.OK)
				{
					_txtWatermarkImgPath.Text = ofd.FileName;
				}
			};
			imgWmRow1.Controls.Add(btnBrowseWmImg);
			imgWmFlow.Controls.Add(imgWmRow1);

			var imgWmRow2 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 0) };
			imgWmRow2.Controls.Add(new Label { Text = "尺寸比例: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
			_trkWatermarkScale = new TrackBar { Minimum = 5, Maximum = 45, Value = Math.Max(5, Math.Min(45, _settings.WatermarkImageScalePercent)), Width = 140, Height = 28, TickFrequency = 5 };
			_lblWatermarkScale = new Label { Text = $"{_trkWatermarkScale.Value}%", ForeColor = TextPrimary, AutoSize = true, Margin = new Padding(2, 4, 0, 0) };
			WireLiveTrackBar(_trkWatermarkScale, val =>
			{
				_settings.WatermarkImageScalePercent = val;
				_lblWatermarkScale.Text = $"{val}%";
				_chkWatermarkEnabled.Checked = true;
			});
			imgWmRow2.Controls.Add(_trkWatermarkScale);
			imgWmRow2.Controls.Add(_lblWatermarkScale);
			imgWmFlow.Controls.Add(imgWmRow2);

			var imgWmRow3 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 0) };
			imgWmRow3.Controls.Add(new Label { Text = "图片透明: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
			_trkWatermarkImgOpacity = new TrackBar { Minimum = 10, Maximum = 100, Value = Math.Max(10, Math.Min(100, _settings.WatermarkImageOpacity)), Width = 140, Height = 28, TickFrequency = 10 };
			_lblWatermarkImgOpacity = new Label { Text = $"{_trkWatermarkImgOpacity.Value}%", ForeColor = TextPrimary, AutoSize = true, Margin = new Padding(2, 4, 0, 0) };
			WireLiveTrackBar(_trkWatermarkImgOpacity, val =>
			{
				_settings.WatermarkImageOpacity = val;
				_lblWatermarkImgOpacity.Text = $"{val}%";
				_chkWatermarkEnabled.Checked = true;
			});
			imgWmRow3.Controls.Add(_trkWatermarkImgOpacity);
			imgWmRow3.Controls.Add(_lblWatermarkImgOpacity);
			imgWmFlow.Controls.Add(imgWmRow3);

			_pnlImageWatermark.Controls.Add(imgWmFlow);
			wmFlow.Controls.Add(_pnlImageWatermark);

			// 水印位置预设与自由绝对模式
			var wmPosRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 4) };
			wmPosRow.Controls.Add(new Label { Text = "基准方位: ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
			_cboWatermarkPos = new ComboBox
			{
				DropDownStyle = ComboBoxStyle.DropDownList,
				Width = 205,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				FlatStyle = FlatStyle.Flat
			};
			_cboWatermarkPos.Items.AddRange(new object[]
			{
				"↘️ 右下角 (默认标配)",
				"↗️ 右上角",
				"↙️ 左下角",
				"↖️ 左上角",
				"⬇️ 底部居中",
				"⬆️ 顶部居中",
				"🎯 自由绝对 XY 坐标"
			});
			_cboWatermarkPos.SelectedIndexChanged += (_, _) =>
			{
				_settings.WatermarkPosition = _cboWatermarkPos.SelectedIndex;
				_chkWatermarkEnabled.Checked = true;
				_settings.Save();
				UpdatePreview();
			};
			wmPosRow.Controls.Add(_cboWatermarkPos);
			wmFlow.Controls.Add(wmPosRow);

			// 水印 X 轴调节
			_lblWatermarkX = new Label
			{
				Text = $"↔️ 水印 X 轴偏移/坐标: {_settings.WatermarkOffsetX} px",
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold),
				AutoSize = true,
				Margin = new Padding(0, 4, 0, 0)
			};
			wmFlow.Controls.Add(_lblWatermarkX);

			_trkWatermarkX = new TrackBar
			{
				Minimum = 0,
				Maximum = 1000,
				Value = Math.Max(0, Math.Min(1000, _settings.WatermarkOffsetX)),
				Width = 300,
				Height = 28,
				TickFrequency = 50
			};
			WireLiveTrackBar(_trkWatermarkX, val =>
			{
				_settings.WatermarkOffsetX = val;
				_lblWatermarkX.Text = $"↔️ 水印 X 轴偏移/坐标: {val} px";
				_chkWatermarkEnabled.Checked = true;
			});
			wmFlow.Controls.Add(_trkWatermarkX);

			// 水印 Y 轴调节
			_lblWatermarkY = new Label
			{
				Text = $"↕️ 水印 Y 轴偏移/坐标: {_settings.WatermarkOffsetY} px",
				ForeColor = TextPrimary,
				Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold),
				AutoSize = true,
				Margin = new Padding(0, 2, 0, 0)
			};
			wmFlow.Controls.Add(_lblWatermarkY);

			_trkWatermarkY = new TrackBar
			{
				Minimum = 0,
				Maximum = 1800,
				Value = Math.Max(0, Math.Min(1800, _settings.WatermarkOffsetY)),
				Width = 300,
				Height = 28,
				TickFrequency = 100
			};
			WireLiveTrackBar(_trkWatermarkY, val =>
			{
				_settings.WatermarkOffsetY = val;
				_lblWatermarkY.Text = $"↕️ 水印 Y 轴偏移/坐标: {val} px";
				_chkWatermarkEnabled.Checked = true;
			});
			wmFlow.Controls.Add(_trkWatermarkY);

			flow.Controls.Add(CreateCollapsibleCard("🏷️ 视频水印 (防搬运 / 署名)", wmFlow, false));

			// 6. GIF 动图贴纸 (默认折叠)
			flow.Controls.Add(CreateCollapsibleCard("👾 GIF 动图贴纸 (支持多贴纸 · 旋转 · 缩放)", BuildGifStickerSection(), false));

			// 7. 团队人员与命名规范 (默认折叠)
			var teamFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 310 };
			var prodRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			prodRow.Controls.Add(new Label { Text = "制作人 / 团队标识: ", ForeColor = TextPrimary, AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
			_txtProducerName = new TextBox
			{
				Width = 160,
				BackColor = InputBg,
				ForeColor = Color.White,
				BorderStyle = BorderStyle.FixedSingle,
				Text = _settings.ProducerName
			};
			_txtProducerName.TextChanged += (_, _) =>
			{
				_settings.ProducerName = _txtProducerName.Text.Trim();
				_settings.Save();
				UpdateFileNamePreview();
			};
			prodRow.Controls.Add(_txtProducerName);
			teamFlow.Controls.Add(prodRow);

			_chkNamingRule = new CheckBox
			{
				Text = "启用标准命名: ZB-制作人-代码-中文第一句-日期-FF-PT-",
				ForeColor = TextMuted,
				AutoSize = true,
				Checked = _settings.NamingRuleEnabled,
				Margin = new Padding(0, 4, 0, 4)
			};
			_chkNamingRule.CheckedChanged += (_, _) =>
			{
				_settings.NamingRuleEnabled = _chkNamingRule.Checked;
				_settings.Save();
				UpdateFileNamePreview();
			};
			teamFlow.Controls.Add(_chkNamingRule);

			flow.Controls.Add(CreateCollapsibleCard("👤 团队人员与命名规范", teamFlow, false));

			scrollContainer.Controls.Add(flow);
			outer.Controls.Add(scrollContainer);

			// 右栏随拖拽宽度等比例缩放 (带严格重入保护与相等防护，彻底防止死循环与界面卡死)
			var designWidths = new Dictionary<Control, int>();
			void Collect(Control parent)
			{
				foreach (Control c in parent.Controls)
				{
					if (c is Panel && c.Parent == flow)
					{
						// 卡片容器本身宽度由 flow 统一管理，跳过避免重复计算
					}
					else if (c.Dock == DockStyle.None && c.Width >= 160 && !designWidths.ContainsKey(c))
					{
						designWidths[c] = Math.Min(c.Width, 330);
					}
					Collect(c);
				}
			}
			Collect(flow);

			bool isScaling = false;
			int lastScaledWidth = -1;

			void ScaleRightContent()
			{
				if (isScaling) return;
				int avail = scrollContainer.ClientSize.Width - scrollContainer.Padding.Horizontal - 6;
				if (avail < 120 || avail == lastScaledWidth) return;
				lastScaledWidth = avail;

				isScaling = true;
				try
				{
					float ratio = Math.Max(0.75f, avail / 310f);
					flow.SuspendLayout();
					flow.Width = avail;
					pnlTopExec.Width = avail;
					topFlow.Width = Math.Max(avail - 16, 280);

					foreach (Control card in flow.Controls)
					{
						if (card.Width != avail) card.Width = avail;
					}

					foreach (var kv in designWidths)
					{
						if (kv.Key.IsDisposed) continue;
						int target = (int)Math.Round(kv.Value * ratio);
						if (kv.Key.Width != target) kv.Key.Width = target;
					}
					flow.ResumeLayout(true);
				}
				finally
				{
					isScaling = false;
				}
			}
			scrollContainer.Resize += (_, _) => ScaleRightContent();
			scrollContainer.HandleCreated += (_, _) => ScaleRightContent();

			return outer;
		}

		/// <summary>
		/// 支持拖拽滚动条时内容实时跟随滚动的面板。
		/// 默认 Panel(AutoScroll) 在拖动滑块时只有松开鼠标才刷新内容，这里拦截 SB_THUMBTRACK 实时滚动。
		/// </summary>
		private sealed class SmoothScrollPanel : Panel
		{
			private const int WM_VSCROLL = 0x0115;
			private const int SB_THUMBTRACK = 5;
			private const int SB_THUMBPOSITION = 4;
			private const int SB_VERT = 1;
			private const uint SIF_TRACKPOS = 0x10;

			[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
			private struct SCROLLINFO
			{
				public uint cbSize;
				public uint fMask;
				public int nMin;
				public int nMax;
				public uint nPage;
				public int nPos;
				public int nTrackPos;
			}

			[System.Runtime.InteropServices.DllImport("user32.dll")]
			private static extern bool GetScrollInfo(IntPtr hwnd, int fnBar, ref SCROLLINFO lpsi);

			public SmoothScrollPanel()
			{
				SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
			}

			protected override void WndProc(ref Message m)
			{
				if (m.Msg == WM_VSCROLL)
				{
					int code = (int)m.WParam & 0xFFFF;
					if (code == SB_THUMBTRACK || code == SB_THUMBPOSITION)
					{
						var si = new SCROLLINFO
						{
							cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(SCROLLINFO)),
							fMask = SIF_TRACKPOS
						};
						if (GetScrollInfo(Handle, SB_VERT, ref si))
						{
							int pos = Math.Max(VerticalScroll.Minimum, Math.Min(VerticalScroll.Maximum, si.nTrackPos));
							AutoScrollPosition = new Point(-AutoScrollPosition.X, pos);
							Update();
							return;
						}
					}
				}
				base.WndProc(ref m);
			}

			// 鼠标滚轮滚动更顺滑、更快
			protected override void OnMouseWheel(MouseEventArgs e)
			{
				base.OnMouseWheel(e);
				Update();
			}
		}

		private void UpdateVoiceDropdownItems()
		{
			_cboDefaultVoice.Items.Clear();
			int engineIdx = _cboTtsEngine.SelectedIndex;
			bool isEleven = engineIdx == 1;

			_btnElevenKeys.Visible = isEleven;
			_btnFetchCloudVoices.Visible = isEleven;
			_lblVoiceHint.Visible = isEleven;
			if (isEleven)
			{
				_lblVoiceHint.Text = "💡 ElevenLabs 采用多密钥池智能轮询；试听使用官方云端 CDN 零额度直连播放，绝不消耗任何字符配额！";

				if (_settings.ClonedVoices != null && _settings.ClonedVoices.Count > 0)
				{
					foreach (var cv in _settings.ClonedVoices.Where(v => v.EngineType != "local_xtts"))
					{
						_cboDefaultVoice.Items.Add(new VoiceOption(cv.VoiceId, $"⭐ [克隆专属] {cv.Name}", "Cloned"));
					}
				}

				foreach (var v in ElevenLabsEngine.DefaultElevenVoices)
				{
					_cboDefaultVoice.Items.Add(v);
				}

				VoiceOption match = null;
				if (_settings.ClonedVoices != null)
				{
					var cvMatch = _settings.ClonedVoices.FirstOrDefault(v => v.VoiceId == _settings.ElevenLabsVoice);
					if (cvMatch != null)
					{
						match = _cboDefaultVoice.Items.OfType<VoiceOption>().FirstOrDefault(vo => vo.Key == cvMatch.VoiceId);
					}
				}
				if (match == null)
				{
					match = ElevenLabsEngine.DefaultElevenVoices.FirstOrDefault(v => v.Key == _settings.ElevenLabsVoice);
				}

				if (match != null) _cboDefaultVoice.SelectedItem = match;
				else if (_cboDefaultVoice.Items.Count > 0) _cboDefaultVoice.SelectedIndex = 0;
			}
			else
			{
				foreach (var v in ScriptBatchTtsEngine.AvailableVoices)
				{
					_cboDefaultVoice.Items.Add(v);
				}

				var match = ScriptBatchTtsEngine.AvailableVoices.FirstOrDefault(v => v.Key == _settings.DefaultVoice);
				if (match != null) _cboDefaultVoice.SelectedItem = match;
				else if (_cboDefaultVoice.Items.Count > 0) _cboDefaultVoice.SelectedIndex = 0;
			}

			CheckCustomVoicePanelVisibility();
		}

		private void CheckCustomVoicePanelVisibility()
		{
			if (_cboDefaultVoice.SelectedItem is VoiceOption vo)
			{
				_pnlCustomVoiceId.Visible = vo.Key == "CustomVoice";
			}
			else
			{
				_pnlCustomVoiceId.Visible = false;
			}
		}

		private void UpdateKeyButtonText()
		{
			var pool = ElevenLabsKeyPool.Instance;
			int total = pool.Keys.Count;
			int usable = pool.Keys.Count(k => k.IsUsable);
			long remain = pool.Keys.Where(k => k.IsUsable).Sum(k => (long)k.RemainingCharacters);

			if (usable > 0)
			{
				_btnElevenKeys.Text = $"🔑 ElevenLabs 密钥 ({usable}个有效/{remain / 1000:0.0}k字)";
			}
			else if (total > 0)
			{
				_btnElevenKeys.Text = $"🔑 ElevenLabs 密钥 ({total}个密钥待检测)";
			}
			else
			{
				_btnElevenKeys.Text = "🔑 ElevenLabs 密钥 (点击添加)";
			}
		}

		private async Task FetchCloudVoicesAsync()
		{
			var pool = ElevenLabsKeyPool.Instance;
			var key = pool.GetNextUsableKey();
			if (key == null || string.IsNullOrWhiteSpace(key.ApiKey))
			{
				MessageBox.Show("请先在【🔑 ElevenLabs 密钥管理】中添加至少一个可用的 API 密钥！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
					foreach (var cv in cloudVoices)
					{
						_cboDefaultVoice.Items.Add(cv);
					}
					foreach (var dv in ElevenLabsEngine.DefaultElevenVoices)
					{
						_cboDefaultVoice.Items.Add(dv);
					}

					_cboDefaultVoice.SelectedIndex = 0;
					MessageBox.Show($"成功从您的 ElevenLabs 账号拉取 {cloudVoices.Count} 个专属克隆/云端声音！", "同步成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				else
				{
					MessageBox.Show("未能拉取到声音列表，请检查网络或密钥权限。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"拉取失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
				_cboTtsEngine.SelectedIndex = string.Equals(_settings.TtsEngine, "elevenlabs", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
				UpdateVoiceDropdownItems();

				_txtProducerName.Text = string.IsNullOrWhiteSpace(_settings.ProducerName) ? "陆恒" : _settings.ProducerName;
				_chkNamingRule.Checked = _settings.NamingRuleEnabled;

				_cboRate.SelectedIndex = _settings.SpeechRate switch
				{
					"+5%" => 1,
					"+10%" => 2,
					"-5%" => 3,
					_ => 0
				};

				_cboCanvasRatio.SelectedIndex = (_settings.CanvasWidth == 1920 && _settings.CanvasHeight == 1080) ? 1 : 0;
				_cboFootageStrategy.SelectedIndex = Math.Min(1, Math.Max(0, _settings.FootageStrategy));

				_txtFootageFolder.Text = _settings.FootageFolderPath ?? "";
				_txtBgmPath.Text = _settings.BgmPath ?? "";
				_trkBgmVol.Value = Math.Max(5, Math.Min(40, _settings.BgmVolumePercent));
				_lblBgmVol.Text = $"{_trkBgmVol.Value}%";

				// 字幕预设索引匹配
				int matchedStyleIdx = SubtitlePresetCatalog.Presets.FindIndex(p => string.Equals(p.Id, _settings.SubtitleStyleId, StringComparison.OrdinalIgnoreCase));
				if (matchedStyleIdx >= 0 && matchedStyleIdx < _cboSubtitleStyle.Items.Count)
				{
					_cboSubtitleStyle.SelectedIndex = matchedStyleIdx;
				}
				else
				{
					_cboSubtitleStyle.SelectedIndex = 0;
				}

				_cboSubtitleSize.SelectedIndex = _settings.SubtitleFontSize switch
				{
					68 => 0,
					78 => 1,
					96 => 3,
					108 => 4,
					_ => 2
				};
				_cboSubtitleMode.SelectedIndex = Math.Min(3, Math.Max(0, _settings.SubtitleDisplayMode));
				_trkSubtitlePos.Value = Math.Max(15, Math.Min(88, _settings.SubtitleVerticalPercent));
				_trkSubtitleX.Value = Math.Max(-350, Math.Min(350, _settings.SubtitleOffsetX));

				// 水印设置回填
				_chkWatermarkEnabled.Checked = _settings.WatermarkEnabled;
				_cboWatermarkType.SelectedIndex = Math.Min(1, Math.Max(0, _settings.WatermarkType));
				_pnlTextWatermark.Visible = _settings.WatermarkType == 0;
				_pnlImageWatermark.Visible = _settings.WatermarkType == 1;

				_txtWatermarkText.Text = string.IsNullOrWhiteSpace(_settings.WatermarkText) ? "Voice: ElevenLabs" : _settings.WatermarkText;
				_cboWatermarkFontSize.SelectedIndex = _settings.WatermarkFontSize switch
				{
					22 => 0,
					36 => 2,
					46 => 3,
					60 => 4,
					_ => 1
				};
				_trkWatermarkOpacity.Value = Math.Max(20, Math.Min(100, _settings.WatermarkTextOpacity));
				_lblWatermarkOpacity.Text = $"{_trkWatermarkOpacity.Value}%";

				_txtWatermarkImgPath.Text = _settings.WatermarkImagePath ?? "";
				_trkWatermarkScale.Value = Math.Max(5, Math.Min(45, _settings.WatermarkImageScalePercent));
				_lblWatermarkScale.Text = $"{_trkWatermarkScale.Value}%";
				_trkWatermarkImgOpacity.Value = Math.Max(10, Math.Min(100, _settings.WatermarkImageOpacity));
				_lblWatermarkImgOpacity.Text = $"{_trkWatermarkImgOpacity.Value}%";

				_cboWatermarkPos.SelectedIndex = Math.Min(6, Math.Max(0, _settings.WatermarkPosition));
				_trkWatermarkX.Value = Math.Max(0, Math.Min(1000, _settings.WatermarkOffsetX));
				_lblWatermarkX.Text = $"↔️ 水印 X 轴偏移/坐标: {_trkWatermarkX.Value} px";
				_trkWatermarkY.Value = Math.Max(0, Math.Min(1800, _settings.WatermarkOffsetY));
				_lblWatermarkY.Text = $"↕️ 水印 Y 轴偏移/坐标: {_trkWatermarkY.Value} px";

				if (string.IsNullOrWhiteSpace(_settings.OutputFolderPath))
				{
					_settings.OutputFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "批量视频成品");
				}
				_txtOutputDir.Text = _settings.OutputFolderPath;
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
			_settings.TtsEngine = engineIdx == 1 ? "elevenlabs" : "edge_tts";

			if (_cboDefaultVoice.SelectedItem is VoiceOption vo)
			{
				if (engineIdx == 1) _settings.ElevenLabsVoice = vo.Key;
				else _settings.DefaultVoice = vo.Key;
			}
			_settings.ElevenLabsCustomVoiceId = _txtCustomVoiceId.Text.Trim();
			CheckCustomVoicePanelVisibility();

			_settings.SpeechRate = _cboRate.SelectedIndex switch
			{
				1 => "+5%",
				2 => "+10%",
				3 => "-5%",
				_ => "+0%"
			};

			if (_cboCanvasRatio.SelectedIndex == 1)
			{
				_settings.CanvasWidth = 1920;
				_settings.CanvasHeight = 1080;
			}
			else
			{
				_settings.CanvasWidth = 1080;
				_settings.CanvasHeight = 1920;
			}

			_settings.ProducerName = _txtProducerName.Text.Trim();
			_settings.NamingRuleEnabled = _chkNamingRule.Checked;

			_settings.FootageStrategy = _cboFootageStrategy.SelectedIndex;
			_settings.FootageFolderPath = _txtFootageFolder.Text.Trim();
			_settings.BgmPath = _txtBgmPath.Text.Trim();
			_settings.BgmVolumePercent = _trkBgmVol.Value;

			// 保存当前字幕预设
			if (_cboSubtitleStyle.SelectedIndex >= 0 && _cboSubtitleStyle.SelectedIndex < SubtitlePresetCatalog.Presets.Count)
			{
				var p = SubtitlePresetCatalog.Presets[_cboSubtitleStyle.SelectedIndex];
				_settings.SubtitleStyleId = p.Id;
				_settings.SubtitleColorHex = p.PrimaryColorHex;
				_settings.SubtitleStrokeColorHex = p.OutlineColorHex;
				_settings.SubtitleStrokeWidth = p.OutlineWidth;
				_settings.SubtitleBorderStyle = p.BorderStyle;
			}

			_settings.SubtitleFontSize = _cboSubtitleSize.SelectedIndex switch
			{
				0 => 68,
				1 => 78,
				3 => 96,
				4 => 108,
				_ => 86
			};
			_settings.SubtitleDisplayMode = _cboSubtitleMode.SelectedIndex;
			_settings.SubtitleVerticalPercent = _trkSubtitlePos.Value;
			_settings.SubtitleOffsetX = _trkSubtitleX.Value;

			// 水印保存
			_settings.WatermarkEnabled = _chkWatermarkEnabled.Checked;
			_settings.WatermarkType = _cboWatermarkType.SelectedIndex;
			_settings.WatermarkText = _txtWatermarkText.Text;
			_settings.WatermarkFontSize = _cboWatermarkFontSize.SelectedIndex switch
			{
				0 => 22,
				2 => 36,
				3 => 46,
				4 => 60,
				_ => 28
			};
			_settings.WatermarkTextOpacity = _trkWatermarkOpacity.Value;
			_settings.WatermarkImagePath = _txtWatermarkImgPath.Text.Trim();
			_settings.WatermarkImageScalePercent = _trkWatermarkScale.Value;
			_settings.WatermarkImageOpacity = _trkWatermarkImgOpacity.Value;
			_settings.WatermarkPosition = _cboWatermarkPos.SelectedIndex;
			_settings.WatermarkOffsetX = _trkWatermarkX.Value;
			_settings.WatermarkOffsetY = _trkWatermarkY.Value;

			_settings.OutputFolderPath = _txtOutputDir.Text.Trim();
			_settings.Save();
		}

		private void UpdateFileNamePreview()
		{
			if (_lblFileNamePreview == null) return;

			string producer = string.IsNullOrWhiteSpace(_txtProducerName.Text) ? "陆恒" : _txtProducerName.Text.Trim();
			ScriptBatchItem targetItem = null;

			if (_grid != null && _grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ScriptBatchItem item)
			{
				targetItem = item;
			}
			else if (_items.Count > 0)
			{
				targetItem = _items[0];
			}

			if (targetItem != null)
			{
				string fileName = targetItem.GenerateStandardFileName(producer) + ".mp4";
				_lblFileNamePreview.Text = $"📌 导出命名预览: {fileName}";
			}
			else
			{
				var dummy = new ScriptBatchItem
				{
					Id = 1,
					ScriptCode = "43u0cm",
					ChineseTitle = "主耶稣即将再来。这 3 个警示直接关系到你灵魂的归宿1。"
				};
				string demo = dummy.GenerateStandardFileName(producer) + ".mp4";
				_lblFileNamePreview.Text = $"📌 导出命名预览: {demo}";
			}
		}

		private void ScanFootageFolder()
		{
			_footageFiles.Clear();
			string path = _txtFootageFolder.Text.Trim();
			if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
			{
				try
				{
					var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".mkv", ".ts", ".webm" };
					var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
										 .Where(f => exts.Contains(Path.GetExtension(f)))
										 .ToList();
					_footageFiles.AddRange(files);
					_lblFootageCount.Text = $"已载入 {_footageFiles.Count} 个可用素材 (巡航抽选)";
					_lblFootageCount.ForeColor = _footageFiles.Count > 0 ? AccentGreen : AccentYellow;
					ExtractThumbnailFromFootage();
					return;
				}
				catch { }
			}
			_lblFootageCount.Text = "素材目录未设置 (将自动生成深邃暗色粒子背景)";
			_lblFootageCount.ForeColor = TextMuted;
		}

		private void ExtractThumbnailFromFootage()
		{
			if (_footageFiles.Count == 0) return;
			try
			{
				string firstVideo = _footageFiles[0];
				string ffmpeg = ScriptBatchComposer.FindFfmpegExe();
				string tempThumb = Path.Combine(Path.GetTempPath(), "script_preview_thumb.jpg");
				var psi = new ProcessStartInfo
				{
					FileName = ffmpeg,
					Arguments = $"-y -ss 00:00:01 -i {ScriptBatchComposer.QuoteArg(firstVideo)} -vframes 1 -q:v 2 {ScriptBatchComposer.QuoteArg(tempThumb)}",
					CreateNoWindow = true,
					UseShellExecute = false
				};
				using var p = Process.Start(psi);
				p.WaitForExit(3000);
				if (File.Exists(tempThumb))
				{
					using var img = Image.FromFile(tempThumb);
					_cachedFootageThumbnail = new Bitmap(img);
					try
					{
						byte[] thumbBytes = File.ReadAllBytes(tempThumb);
						_cachedFootageThumbnailBase64 = "data:image/jpeg;base64," + Convert.ToBase64String(thumbBytes);
					}
					catch { }
				}
			}
			catch { }
		}

		private void UpdatePreview()
		{
			_previewCanvas?.Invalidate();
			_previewCanvas?.Update();

			if (_isWebViewReady && _webViewPreview != null && !_webViewPreview.IsDisposed && !_isWebViewUpdating)
			{
				SendStateToWebView();
			}
		}

		private void WireLiveTrackBar(TrackBar trk, Action<int> onUpdate, Action onCommit = null)
		{
			void HandleLiveUpdate()
			{
				onUpdate(trk.Value);
				UpdatePreview();
			}

			trk.Scroll += (_, _) => HandleLiveUpdate();
			trk.ValueChanged += (_, _) => HandleLiveUpdate();
			trk.MouseUp += (_, _) =>
			{
				onCommit?.Invoke();
				_settings.Save();
			};
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			InitializeWebViewAsync();
		}

		protected override void OnVisibleChanged(EventArgs e)
		{
			base.OnVisibleChanged(e);
			if (Visible && !_isWebViewReady && IsHandleCreated)
			{
				InitializeWebViewAsync();
			}
		}

		private async void InitializeWebViewAsync()
		{
			if (_webViewPreview == null || _isWebViewReady) return;
			if (!IsHandleCreated) return;

			try
			{
				string userDataFolder = Path.Combine(Path.GetTempPath(), "VideoBatchStudio_WebView2");
				var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
				await _webViewPreview.EnsureCoreWebView2Async(env);

				_webViewPreview.CoreWebView2.Settings.IsStatusBarEnabled = false;
				_webViewPreview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
				_webViewPreview.CoreWebView2.Settings.AreDevToolsEnabled = false;

				_webViewPreview.WebMessageReceived -= OnWebViewMessageReceived;
				_webViewPreview.WebMessageReceived += OnWebViewMessageReceived;

				_webViewPreview.NavigationCompleted -= OnWebViewNavigationCompleted;
				_webViewPreview.NavigationCompleted += OnWebViewNavigationCompleted;

				string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "web_preview", "preview.html");
				if (!File.Exists(htmlPath))
				{
					string altPath = Path.Combine(@"C:\Users\PcVkH\.gemini\antigravity\scratch\VideoBatchStudio\web_preview", "preview.html");
					if (File.Exists(altPath)) htmlPath = altPath;
				}

				if (File.Exists(htmlPath))
				{
					_webViewPreview.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("WebView2 init failed: " + ex);
			}
		}

		private void OnWebViewNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
		{
			if (e.IsSuccess)
			{
				_isWebViewReady = true;
				if (IsHandleCreated)
				{
					BeginInvoke(new Action(() =>
					{
						_webViewPreview.Visible = true;
						if (_previewCanvas != null) _previewCanvas.Visible = false;
						UpdatePreview();
					}));
				}
				else
				{
					_webViewPreview.Visible = true;
					if (_previewCanvas != null) _previewCanvas.Visible = false;
				}
			}
		}

		private void OnWebViewMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
		{
			try
			{
				string json = e.WebMessageAsJson;
				if (string.IsNullOrEmpty(json)) return;

				var serializer = new JavaScriptSerializer();
				var msg = serializer.Deserialize<Dictionary<string, object>>(json);
				if (msg == null) return;

				string action = msg.ContainsKey("action") ? msg["action"]?.ToString() : null;
				if (action == "watermark_drag" && msg.ContainsKey("payload") && msg["payload"] is Dictionary<string, object> payload)
				{
					int x = Convert.ToInt32(payload["x"]);
					int y = Convert.ToInt32(payload["y"]);

					if (IsHandleCreated)
					{
						BeginInvoke(new Action(() =>
						{
							_isWebViewUpdating = true;
							try
							{
								if (_trkWatermarkX != null)
								{
									_trkWatermarkX.Value = Math.Max(_trkWatermarkX.Minimum, Math.Min(_trkWatermarkX.Maximum, x));
									_lblWatermarkX.Text = $"↔️ 水印水平 X 轴: {_trkWatermarkX.Value} px";
								}
								if (_trkWatermarkY != null)
								{
									_trkWatermarkY.Value = Math.Max(_trkWatermarkY.Minimum, Math.Min(_trkWatermarkY.Maximum, y));
									_lblWatermarkY.Text = $"↕️ 水印垂直 Y 轴: {_trkWatermarkY.Value} px";
								}
								_settings.WatermarkOffsetX = x;
								_settings.WatermarkOffsetY = y;
								_settings.Save();
							}
							finally
							{
								_isWebViewUpdating = false;
							}
						}));
					}
				}
				else if (action == "subtitle_drag" && msg.ContainsKey("payload") && msg["payload"] is Dictionary<string, object> subPayload)
				{
					int subX = Convert.ToInt32(subPayload["x"]);
					int subPercent = Convert.ToInt32(subPayload["percent"]);

					if (IsHandleCreated)
					{
						BeginInvoke(new Action(() =>
						{
							_isWebViewUpdating = true;
							try
							{
								if (_trkSubtitleX != null)
								{
									_trkSubtitleX.Value = Math.Max(_trkSubtitleX.Minimum, Math.Min(_trkSubtitleX.Maximum, subX));
									_lblSubtitleX.Text = $"↔️ 字幕水平 X 轴: {_trkSubtitleX.Value} px ({( _trkSubtitleX.Value == 0 ? "水平居中" : (_trkSubtitleX.Value < 0 ? "偏左" : "偏右") )})";
								}
								if (_trkSubtitlePos != null)
								{
									_trkSubtitlePos.Value = Math.Max(_trkSubtitlePos.Minimum, Math.Min(_trkSubtitlePos.Maximum, subPercent));
									string desc = _trkSubtitlePos.Value >= 78 ? "居下标配" : (_trkSubtitlePos.Value <= 35 ? "靠上顶部" : "居中视野");
									_lblSubtitlePos.Text = $"↕️ 字幕垂直 Y 轴: {_trkSubtitlePos.Value}% ({desc})";
								}
								_settings.SubtitleOffsetX = subX;
								_settings.SubtitleVerticalPercent = subPercent;
								_settings.Save();
							}
							finally
							{
								_isWebViewUpdating = false;
							}
						}));
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("OnWebViewMessageReceived error: " + ex);
			}
		}

		private void SendStateToWebView()
		{
			if (!_isWebViewReady || _webViewPreview == null || _webViewPreview.IsDisposed || _isWebViewUpdating) return;

			try
			{
				string sampleText = "Senhora que está segurando um telefone, talvez eu não saiba o seu nome, mas você é abençoada.";
				if (_grid != null && _grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is ScriptBatchItem item)
				{
					if (!string.IsNullOrWhiteSpace(item.ScriptText))
					{
						var lines = item.ScriptText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
						if (lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]))
						{
							sampleText = lines[0].Trim();
						}
					}
				}

				string wmImgDataUrl = "";
				if (!string.IsNullOrWhiteSpace(_settings.WatermarkImagePath) && File.Exists(_settings.WatermarkImagePath))
				{
					try
					{
						byte[] bytes = File.ReadAllBytes(_settings.WatermarkImagePath);
						string ext = Path.GetExtension(_settings.WatermarkImagePath).ToLowerInvariant().TrimStart('.');
						string mime = (ext == "jpg" || ext == "jpeg") ? "image/jpeg" : (ext == "webp" ? "image/webp" : "image/png");
						wmImgDataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
					}
					catch { }
				}

				var dto = new Dictionary<string, object>
				{
					["subtitleText"] = sampleText,
					["subtitleStyleId"] = _settings.SubtitleStyleId,
					["subtitleFontSize"] = _settings.SubtitleFontSize,
					["subtitleVerticalPercent"] = _settings.SubtitleVerticalPercent,
					["subtitleOffsetX"] = _settings.SubtitleOffsetX,
					["subtitleFontFamily"] = _settings.SubtitleFontFamily,

					["watermarkEnabled"] = _settings.WatermarkEnabled,
					["watermarkType"] = _settings.WatermarkType,
					["watermarkText"] = _settings.WatermarkText,
					["watermarkFontSize"] = _settings.WatermarkFontSize,
					["watermarkTextColorHex"] = _settings.WatermarkTextColorHex,
					["watermarkTextOpacity"] = _settings.WatermarkTextOpacity,
					["watermarkImagePath"] = _settings.WatermarkImagePath,
					["watermarkImageDataUrl"] = wmImgDataUrl,
					["watermarkImageScalePercent"] = _settings.WatermarkImageScalePercent,
					["watermarkImageOpacity"] = _settings.WatermarkImageOpacity,
					["watermarkPosition"] = _settings.WatermarkPosition,
					["watermarkOffsetX"] = _settings.WatermarkOffsetX,
					["watermarkOffsetY"] = _settings.WatermarkOffsetY,

					["bgThumbnailDataUrl"] = _cachedFootageThumbnailBase64 ?? ""
				};

				var serializer = new JavaScriptSerializer();
				string json = serializer.Serialize(dto);
				_webViewPreview.CoreWebView2.ExecuteScriptAsync($"window.updateFromHost({json});");
			}
			catch (Exception ex)
			{
				Debug.WriteLine("SendStateToWebView error: " + ex);
			}
		}

		private void ResetAuditionButton()
		{
			_isAuditioning = false;
			if (_btnAudition != null)
			{
				_btnAudition.Text = "🔊 极速试听";
				_btnAudition.BackColor = AccentBlue;
			}
		}

		// 剪贴板即时解析 (RFC-4180 / Google Sheets 多行兼容)
		private static List<List<string>> ParseClipboardRows(string text)
		{
			var rows = new List<List<string>>();
			if (string.IsNullOrWhiteSpace(text)) return rows;

			var currentRow = new List<string>();
			var currentField = new StringBuilder();
			bool inQuotes = false;
			int i = 0;

			while (i < text.Length)
			{
				char c = text[i];

				if (c == '"')
				{
					if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
					{
						currentField.Append('"');
						i += 2;
						continue;
					}
					inQuotes = !inQuotes;
					i++;
					continue;
				}

				if (!inQuotes)
				{
					if (c == '\t')
					{
						currentRow.Add(currentField.ToString().Trim());
						currentField.Clear();
						i++;
						continue;
					}
					if (c == '\r')
					{
						if (i + 1 < text.Length && text[i + 1] == '\n') i++;
						currentRow.Add(currentField.ToString().Trim());
						currentField.Clear();
						if (currentRow.Any(s => !string.IsNullOrWhiteSpace(s)))
						{
							rows.Add(currentRow);
						}
						currentRow = new List<string>();
						i++;
						continue;
					}
					if (c == '\n')
					{
						currentRow.Add(currentField.ToString().Trim());
						currentField.Clear();
						if (currentRow.Any(s => !string.IsNullOrWhiteSpace(s)))
						{
							rows.Add(currentRow);
						}
						currentRow = new List<string>();
						i++;
						continue;
					}
				}

				currentField.Append(c);
				i++;
			}

			if (currentField.Length > 0 || currentRow.Count > 0)
			{
				currentRow.Add(currentField.ToString().Trim());
				if (currentRow.Any(s => !string.IsNullOrWhiteSpace(s)))
				{
					rows.Add(currentRow);
				}
			}

			return rows;
		}

		private void PasteClipboardData()
		{
			try
			{
				if (!Clipboard.ContainsText()) return;
				string clipText = Clipboard.GetText();
				if (string.IsNullOrWhiteSpace(clipText)) return;

				var parsedRows = ParseClipboardRows(clipText);
				int addedCount = 0;

				for (int r = 0; r < parsedRows.Count; r++)
				{
					var cols = parsedRows[r];
					if (cols.Count == 0) continue;

					// 智能跳过表头行 (仅当首列明确包含代码/序号等表头词且非正文时跳过)
					if (r == 0 && cols.Count >= 2)
					{
						string c0 = cols[0].Trim().ToLowerInvariant();
						if (c0 == "文案代码" || c0 == "代码" || c0 == "code" || c0 == "序号" || c0 == "id")
						{
							continue;
						}
					}

					string code = "";
					string title = "";
					string script = "";
					string voice = "";

					if (cols.Count == 1)
					{
						script = cols[0];
					}
					else if (cols.Count == 2)
					{
						// 智能适配：复制两列时，从【中文文案】开始 (第1列为中文文案/标题，第2列为外文文案)
						title = cols[0];
						script = cols[1];
					}
					else if (cols.Count >= 3)
					{
						// 智能适配：复制三列时，从【文案代码】开始 (第1列为文案代码，第2列为中文文案/标题，第3列为外文文案)
						code = cols[0];
						title = cols[1];
						script = cols[2];
						if (cols.Count >= 4)
						{
							voice = cols[3];
						}
					}

					script = script.Trim();
					if (script.StartsWith("\"") && script.EndsWith("\"") && script.Length >= 2)
					{
						script = script.Substring(1, script.Length - 2).Trim();
					}
					script = script.Replace("\"\"", "\"");
					if (string.IsNullOrWhiteSpace(script)) continue;

					int nextId = _items.Count + 1;
					if (string.IsNullOrEmpty(title))
					{
						title = string.IsNullOrEmpty(code) ? $"文案_{nextId:D3}" : code;
					}

					var item = new ScriptBatchItem
					{
						Id = nextId,
						ScriptCode = code.Trim(),
						ChineseTitle = title.Trim(),
						Title = title.Trim(),
						ScriptText = script.Trim(),
						VoiceKey = string.IsNullOrEmpty(voice) ? (_cboDefaultVoice.SelectedItem is VoiceOption vo ? vo.Key : _settings.DefaultVoice) : voice.Trim()
					};

					_items.Add(item);
					int rowIndex = _grid.Rows.Add(item.Id, item.ScriptCode, item.ChineseTitle, item.ScriptText, _cboDefaultVoice.Text, "等待中", "⚪ 未检测");
					_grid.Rows[rowIndex].Tag = item;
					addedCount++;
				}

				_lblStatusText.Text = $"成功从剪贴板解析粘贴 {addedCount} 篇文案任务！";
				_lblStatusText.ForeColor = AccentGreen;
				UpdatePreview();
				UpdateFileNamePreview();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, $"粘贴失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		private static bool IsLikelyCode(string val)
		{
			if (string.IsNullOrWhiteSpace(val)) return false;
			val = val.Trim();
			return val.Length >= 3 && val.Length <= 18 && !val.Any(c => c > 127) && !val.Contains(" ");
		}

		private void AddEmptyRow()
		{
			int nextId = _items.Count + 1;
			var item = new ScriptBatchItem
			{
				Id = nextId,
				ScriptCode = "",
				ChineseTitle = $"文案_{nextId:D3}",
				Title = $"文案_{nextId:D3}",
				ScriptText = "在这里输入或粘贴文案内容...",
				VoiceKey = _cboDefaultVoice.SelectedItem is VoiceOption vo ? vo.Key : _settings.DefaultVoice
			};
			_items.Add(item);
			int rowIndex = _grid.Rows.Add(item.Id, item.ScriptCode, item.ChineseTitle, item.ScriptText, _cboDefaultVoice.Text, "等待中", "⚪ 未检测");
			_grid.Rows[rowIndex].Tag = item;
			UpdatePreview();
			UpdateFileNamePreview();
		}

		private void DeleteSelectedRows()
		{
			if (_grid.SelectedRows.Count == 0) return;
			var toRemove = new List<DataGridViewRow>();
			foreach (DataGridViewRow r in _grid.SelectedRows)
			{
				toRemove.Add(r);
			}
			foreach (var r in toRemove)
			{
				if (r.Tag is ScriptBatchItem item)
				{
					_items.Remove(item);
				}
				_grid.Rows.Remove(r);
			}
			for (int i = 0; i < _grid.Rows.Count; i++)
			{
				_grid.Rows[i].Cells[0].Value = i + 1;
				if (_grid.Rows[i].Tag is ScriptBatchItem item) item.Id = i + 1;
			}
			UpdatePreview();
			UpdateFileNamePreview();
		}

		private void ClearAllRows()
		{
			if (_items.Count == 0) return;
			if (MessageBox.Show(this, "确定要清空表格中全部文案吗？", "清空确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				_items.Clear();
				_grid.Rows.Clear();
				_lblStatusText.Text = "表格已清空";
				UpdatePreview();
				UpdateFileNamePreview();
			}
		}

		private void ImportTextOrCsvFile()
		{
			using var ofd = new OpenFileDialog
			{
				Title = "选择文案表格或文本文件",
				Filter = "文本/表格文件 (*.txt;*.csv)|*.txt;*.csv|所有文件 (*.*)|*.*",
				Multiselect = true
			};
			if (ofd.ShowDialog(this) == DialogResult.OK)
			{
				int added = 0;
				foreach (string file in ofd.FileNames)
				{
					try
					{
						string ext = Path.GetExtension(file).ToLowerInvariant();
						if (ext == ".csv")
						{
							string csvContent = File.ReadAllText(file, Encoding.UTF8);
							var csvRows = ParseClipboardRows(csvContent);
							for (int r = 1; r < csvRows.Count; r++)
							{
								var parts = csvRows[r];
								if (parts.Count == 0) continue;

								string code = "";
								string title = "";
								string script = "";

								if (parts.Count == 1) script = parts[0];
								else if (parts.Count == 2)
								{
									title = parts[0];
									script = parts[1];
								}
								else if (parts.Count >= 3)
								{
									code = parts[0];
									title = parts[1];
									script = parts[2];
								}

								script = script.Trim().Trim('"', '\'');
								if (string.IsNullOrWhiteSpace(script)) continue;

								int nextId = _items.Count + 1;
								var item = new ScriptBatchItem
								{
									Id = nextId,
									ScriptCode = code,
									ChineseTitle = string.IsNullOrEmpty(title) ? $"文案_{nextId:D3}" : title,
									Title = string.IsNullOrEmpty(title) ? $"文案_{nextId:D3}" : title,
									ScriptText = script,
									VoiceKey = _cboDefaultVoice.SelectedItem is VoiceOption vo ? vo.Key : _settings.DefaultVoice
								};
								_items.Add(item);
								int rIdx = _grid.Rows.Add(item.Id, item.ScriptCode, item.ChineseTitle, item.ScriptText, _cboDefaultVoice.Text, "等待中", "⚪ 未检测");
								_grid.Rows[rIdx].Tag = item;
								added++;
							}
						}
						else
						{
							string content = File.ReadAllText(file, Encoding.UTF8).Trim();
							if (!string.IsNullOrEmpty(content))
							{
								int nextId = _items.Count + 1;
								var item = new ScriptBatchItem
								{
									Id = nextId,
									ChineseTitle = Path.GetFileNameWithoutExtension(file),
									Title = Path.GetFileNameWithoutExtension(file),
									ScriptText = content,
									VoiceKey = _cboDefaultVoice.SelectedItem is VoiceOption vo ? vo.Key : _settings.DefaultVoice
								};
								_items.Add(item);
								int rIdx = _grid.Rows.Add(item.Id, item.ScriptCode, item.ChineseTitle, item.ScriptText, _cboDefaultVoice.Text, "等待中", "⚪ 未检测");
								_grid.Rows[rIdx].Tag = item;
								added++;
							}
						}
					}
					catch { }
				}
				_lblStatusText.Text = $"成功导入 {added} 篇文案！";
				UpdatePreview();
				UpdateFileNamePreview();
			}
		}

		private async Task AuditionVoiceAsync()
		{
			if (_isAuditioning)
			{
				ScriptBatchTtsEngine.StopAudio();
				ResetAuditionButton();
				_lblStatusText.Text = "已停止音频试听";
				return;
			}

			string voiceKey = _settings.DefaultVoice;
			int engineIdx = _cboTtsEngine.SelectedIndex;
			bool isEleven = engineIdx == 1;

			if (_cboDefaultVoice.SelectedItem is VoiceOption vo)
			{
				voiceKey = vo.Key;
			}

			_isAuditioning = true;
			if (_btnAudition != null)
			{
				_btnAudition.Text = "⏹️ 停止试听";
				_btnAudition.BackColor = Color.FromArgb(220, 38, 38);
			}

			string auditionDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "auditions");
			try { Directory.CreateDirectory(auditionDir); } catch { }

			try
			{
				if (isEleven)
				{
					// 参考 VideoKit 核心机制：试听优先直连官方 CDN 预录音频，100%零额度消耗，绝不消耗任何 API 字符配额
					string cachedAudio = Path.Combine(auditionDir, $"eleven_{voiceKey}.mp3");
					if (!File.Exists(cachedAudio))
					{
						string previewUrl = null;
						if (ElevenLabsEngine.VoicePreviewUrls.TryGetValue(voiceKey, out var url) && !string.IsNullOrEmpty(url))
						{
							previewUrl = url;
						}
						else
						{
							previewUrl = $"https://storage.googleapis.com/eleven-public-prod/previews/models/{voiceKey}.mp3";
						}

						_lblStatusText.Text = "正在直连 ElevenLabs 官方 CDN 获取零配额试听音频...";
						using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
						try
						{
							using var resp = await http.GetAsync(previewUrl);
							if (resp.IsSuccessStatusCode)
							{
								var bytes = await resp.Content.ReadAsByteArrayAsync();
								if (bytes != null && bytes.Length > 1024)
								{
									File.WriteAllBytes(cachedAudio, bytes);
								}
							}
						}
						catch { }
					}

					if (File.Exists(cachedAudio))
					{
						_lblStatusText.Text = "🔊 [0额度试听] 正在软件内播放 ElevenLabs 官方原音...";
						bool played = ScriptBatchTtsEngine.PlayAudioFile(cachedAudio, () =>
						{
							if (IsHandleCreated) BeginInvoke((MethodInvoker)ResetAuditionButton);
						});
						if (!played) ResetAuditionButton();
						return;
					}

					// 若官方 CDN 未命中该 voiceKey（例如自定义克隆 ID），则生成极短 3 词 sample，绝不浪费 100 字符
					_lblStatusText.Text = "正在生成专属音色短样音 (极简 2 词，极低额度消耗)...";
					string miniSample = "Deus te abençoe.";
					var res = await ElevenLabsEngine.SynthesizeWithFailoverAsync(miniSample, voiceKey, auditionDir, 999, CancellationToken.None);
					if (res.Success && File.Exists(res.AudioPath))
					{
						try { File.Copy(res.AudioPath, cachedAudio, true); } catch { }
						_lblStatusText.Text = "🔊 试听已就绪，正在软件内播放...";
						bool played = ScriptBatchTtsEngine.PlayAudioFile(res.AudioPath, () =>
						{
							if (IsHandleCreated) BeginInvoke((MethodInvoker)ResetAuditionButton);
						});
						if (!played) ResetAuditionButton();
					}
					else
					{
						ResetAuditionButton();
						_lblStatusText.Text = $"试听提示: {res.ErrorMessage}";
						MessageBox.Show(this, $"试听合成未成功:\n{res.ErrorMessage}\n\n建议在【密钥管理】中检查可用 Key 或切换到完全免费的微软 Edge-TTS 模型。", "试听提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
				}
				else
				{
					// 微软 Edge-TTS (全免费)
					if (ScriptBatchTtsEngine.PlayInstantAudition(voiceKey, () =>
					{
						if (IsHandleCreated) BeginInvoke((MethodInvoker)ResetAuditionButton);
					}))
					{
						_lblStatusText.Text = $"🔊 [完全免费] 正在播放微软预置音色: [{voiceKey}]";
						return;
					}

					string sampleText = "Senhora que está segurando um telefone, talvez eu não saiba o seu nome, mas você é abençoada.";
					_lblStatusText.Text = "正在在线合成微软音色试听 (完全免费)...";
					var res = await ScriptBatchTtsEngine.SynthesizeAsync(sampleText, voiceKey, _settings.SpeechRate, _settings.SpeechPitch, "SentenceBoundary", auditionDir, 999, CancellationToken.None);
					if (res.Success && File.Exists(res.AudioPath))
					{
						_lblStatusText.Text = "🔊 正在软件内播放微软试听...";
						bool played = ScriptBatchTtsEngine.PlayAudioFile(res.AudioPath, () =>
						{
							if (IsHandleCreated) BeginInvoke((MethodInvoker)ResetAuditionButton);
						});
						if (!played) ResetAuditionButton();
					}
					else
					{
						ResetAuditionButton();
						_lblStatusText.Text = $"试听失败: {res.ErrorMessage}";
					}
				}
			}
			catch (Exception ex)
			{
				ResetAuditionButton();
				_lblStatusText.Text = $"试听异常: {ex.Message}";
			}
		}

		private async Task StartBatchProcessingAsync()
		{
			if (_isProcessing) return;

			SyncGridEditsToItems();

			if (_items.Count == 0)
			{
				MessageBox.Show(this, "表格中尚无文案任务！请先通过剪贴板粘贴 (Ctrl+V) 或导入文案。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			string outDir = _txtOutputDir.Text.Trim();
			if (string.IsNullOrEmpty(outDir))
			{
				outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "批量视频成品");
				_txtOutputDir.Text = outDir;
			}
			Directory.CreateDirectory(outDir);

			_isProcessing = true;
			_cts = new CancellationTokenSource();
			_btnStartBatch.Enabled = false;
			_btnStopBatch.Enabled = true;

			int total = _items.Count;
			int successCount = 0;
			int failCount = 0;

			_progressBar.Value = 0;
			_lblStatusText.Text = $"批量流水线启动: 共 {total} 个视频任务待交付...";

			for (int i = 0; i < total; i++)
			{
				if (_cts.IsCancellationRequested) break;

				var item = _items[i];
				var row = FindRowByItem(item);

				if (row != null)
				{
					row.Cells[5].Value = "处理中 ⏳";
					_grid.ClearSelection();
					row.Selected = true;
				}

				int taskIdx = i + 1;
				void UpdateProgress(int pct, string msg)
				{
					if (IsHandleCreated)
					{
						BeginInvoke((MethodInvoker)delegate
						{
							_progressBar.Value = Math.Max(0, Math.Min(100, pct));
							_lblStatusText.Text = $"[{taskIdx}/{total}] {item.DisplayTitle}: {msg}";
							if (row != null) row.Cells[5].Value = $"{pct}% {msg}";
						});
					}
				}

				bool ok = await ScriptBatchComposer.ProcessItemAsync(
					item,
					_settings,
					_footageFiles,
					outDir,
					UpdateProgress,
					_cts.Token
				);

				if (ok)
				{
					successCount++;
					if (row != null)
					{
						row.Cells[5].Value = "✅ 完成";
						row.Cells[6].Value = item.QcBadge;
						row.Cells[6].ToolTipText = item.QcDetails;
					}
				}
				else
				{
					failCount++;
					if (row != null)
					{
						row.Cells[5].Value = "❌ 失败";
						row.Cells[6].Value = item.QcBadge;
						row.Cells[6].ToolTipText = item.QcDetails;
					}
				}

				await Task.Delay(150);
			}

			_isProcessing = false;
			_btnStartBatch.Enabled = true;
			_btnStopBatch.Enabled = false;
			_progressBar.Value = 100;

			string summary = $"批量生产完成！成功: {successCount} 条，失败: {failCount} 条。";
			_lblStatusText.Text = summary;
			_lblStatusText.ForeColor = successCount > 0 ? AccentGreen : AccentYellow;

			MessageBox.Show(this, $"{summary}\n\n成品已全部安全存入:\n{outDir}", "批量成片完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void StopBatchProcessing()
		{
			if (_cts != null && !_cts.IsCancellationRequested)
			{
				_cts.Cancel();
				_lblStatusText.Text = "正在安全中断批量流水线...";
			}
		}

		private void SyncGridEditsToItems()
		{
			for (int i = 0; i < _grid.Rows.Count; i++)
			{
				if (_grid.Rows[i].Tag is ScriptBatchItem item)
				{
					item.ScriptCode = _grid.Rows[i].Cells[1].Value?.ToString()?.Trim() ?? item.ScriptCode;
					item.ChineseTitle = _grid.Rows[i].Cells[2].Value?.ToString()?.Trim() ?? item.ChineseTitle;
					item.Title = item.ChineseTitle;
					item.ScriptText = _grid.Rows[i].Cells[3].Value?.ToString()?.Trim() ?? item.ScriptText;
				}
			}
		}

		private DataGridViewRow FindRowByItem(ScriptBatchItem item)
		{
			foreach (DataGridViewRow r in _grid.Rows)
			{
				if (r.Tag == item) return r;
			}
			return null;
		}

		private void PreviewSelectedVideo()
		{
			if (_grid.SelectedRows.Count == 0) return;
			if (_grid.SelectedRows[0].Tag is ScriptBatchItem item)
			{
				if (File.Exists(item.VideoOutputPath))
				{
					Process.Start(new ProcessStartInfo(item.VideoOutputPath) { UseShellExecute = true });
				}
				else if (File.Exists(item.AudioPath))
				{
					Process.Start(new ProcessStartInfo(item.AudioPath) { UseShellExecute = true });
				}
				else
				{
					MessageBox.Show(this, "该文案尚未生成视频或音频！请先点击【开始批量生产】。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
		}

		private void OpenOutputFolder()
		{
			string dir = _txtOutputDir.Text.Trim();
			if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
			{
				Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
			}
		}

		private void BrowseFootageFolder()
		{
			using var fbd = new FolderBrowserDialog { Description = "选择背景空镜/视频素材文件夹" };
			if (fbd.ShowDialog(this) == DialogResult.OK)
			{
				_txtFootageFolder.Text = fbd.SelectedPath;
			}
		}

		private void BrowseBgmFile()
		{
			using var ofd = new OpenFileDialog
			{
				Title = "选择背景音乐 (MP3/WAV)",
				Filter = "音频文件 (*.mp3;*.wav;*.m4a)|*.mp3;*.wav;*.m4a|所有文件 (*.*)|*.*"
			};
			if (ofd.ShowDialog(this) == DialogResult.OK)
			{
				_txtBgmPath.Text = ofd.FileName;
			}
		}

		private void BrowseOutputFolder()
		{
			using var fbd = new FolderBrowserDialog { Description = "选择成品视频输出保存目录" };
			if (fbd.ShowDialog(this) == DialogResult.OK)
			{
				_txtOutputDir.Text = fbd.SelectedPath;
			}
		}

		public Image GetOrLoadStickerImage(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
			if (_stickerImageCache.TryGetValue(path, out var img)) return img;
			try
			{
				var loaded = Image.FromFile(path);
				if (ImageAnimator.CanAnimate(loaded))
				{
					ImageAnimator.Animate(loaded, (s, e) =>
					{
						if (IsHandleCreated && !IsDisposed)
						{
							_previewCanvas?.Invalidate();
						}
					});
				}
				_stickerImageCache[path] = loaded;
				return loaded;
			}
			catch
			{
				return null;
			}
		}

		private Control BuildGifStickerSection()
		{
			var pnl = new Panel { AutoSize = true, Width = 310, Margin = new Padding(0, 4, 0, 4) };
			var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };

			flow.Controls.Add(CreateSectionHeader("🎞️ GIF 动图贴纸 (多图叠加 · 可旋转 · 时间轴)"));

			// 操作按钮栏
			var btnRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 2, 0, 4) };
			var btnAdd = CreateButton("➕ 添加GIF", AccentBlue, Color.White, 90);
			btnAdd.Click += (_, _) =>
			{
				using var ofd = new OpenFileDialog
				{
					Title = "选择 GIF 动图贴纸",
					Filter = "GIF 动图与贴纸 (*.gif;*.webp;*.png)|*.gif;*.webp;*.png|所有文件 (*.*)|*.*",
					Multiselect = true
				};
				if (ofd.ShowDialog(this) == DialogResult.OK)
				{
					if (_settings.GifStickers == null) _settings.GifStickers = new List<GifStickerItem>();
					foreach (var file in ofd.FileNames)
					{
						var item = new GifStickerItem
						{
							Name = Path.GetFileName(file),
							FilePath = file,
							Enabled = true,
							XPercent = 50f,
							YPercent = 50f,
							ScalePercent = 25f,
							RotationAngle = 0f,
							OpacityPercent = 100,
							Loop = true,
							StartTimeSec = 0.0,
							DurationSec = 0.0
						};
						_settings.GifStickers.Add(item);
						_selectedSticker = item;
					}
					SaveSettings();
					RefreshStickerList();
					UpdatePreview();
				}
			};
			btnRow.Controls.Add(btnAdd);

			var btnDel = CreateButton("❌ 删除", CardBorder, TextPrimary, 70);
			btnDel.Click += (_, _) =>
			{
				if (_selectedSticker != null && _settings.GifStickers != null)
				{
					_settings.GifStickers.Remove(_selectedSticker);
					_selectedSticker = _settings.GifStickers.FirstOrDefault();
					SaveSettings();
					RefreshStickerList();
					UpdatePreview();
				}
			};
			btnRow.Controls.Add(btnDel);

			var btnToggle = CreateButton("👁️ 启用/停用", CardBorder, TextPrimary, 90);
			btnToggle.Click += (_, _) =>
			{
				if (_selectedSticker != null)
				{
					_selectedSticker.Enabled = !_selectedSticker.Enabled;
					SaveSettings();
					RefreshStickerList();
					UpdatePreview();
				}
			};
			btnRow.Controls.Add(btnToggle);
			flow.Controls.Add(btnRow);

			// 贴纸列表 ListBox
			_lstGifStickers = new ListBox
			{
				Width = 310,
				Height = 75,
				BackColor = InputBg,
				ForeColor = TextPrimary,
				BorderStyle = BorderStyle.FixedSingle,
				Font = new Font("Microsoft YaHei UI", 8f)
			};
			_lstGifStickers.SelectedIndexChanged += (_, _) =>
			{
				if (_lstGifStickers.SelectedItem is GifStickerItem item)
				{
					_selectedSticker = item;
					SyncStickerDetailsFromModel();
					UpdatePreview();
				}
			};
			flow.Controls.Add(_lstGifStickers);

			// 贴纸细节属性面板
			_pnlStickerDetails = new Panel { AutoSize = true, Width = 310, Visible = false, Margin = new Padding(0, 4, 0, 0) };
			var dFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };

			// 1. 文件路径
			var pathRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			_txtStickerPath = new TextBox { Width = 210, BackColor = InputBg, ForeColor = TextPrimary, BorderStyle = BorderStyle.FixedSingle, ReadOnly = true };
			pathRow.Controls.Add(_txtStickerPath);
			var btnChangePath = CreateButton("更换...", AccentBlue, Color.White, 70);
			btnChangePath.Click += (_, _) =>
			{
				if (_selectedSticker == null) return;
				using var ofd = new OpenFileDialog { Title = "更换 GIF 文件", Filter = "GIF/贴纸 (*.gif;*.webp;*.png)|*.gif;*.webp;*.png|所有文件 (*.*)|*.*" };
				if (ofd.ShowDialog(this) == DialogResult.OK)
				{
					_selectedSticker.FilePath = ofd.FileName;
					_selectedSticker.Name = Path.GetFileName(ofd.FileName);
					_txtStickerPath.Text = ofd.FileName;
					SaveSettings();
					RefreshStickerList();
					UpdatePreview();
				}
			};
			pathRow.Controls.Add(btnChangePath);
			dFlow.Controls.Add(pathRow);

			// 2. 位置 X / Y
			_lblStickerX = new Label { Text = "↔️ 水平位置 (X): 50%", ForeColor = TextPrimary, Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
			dFlow.Controls.Add(_lblStickerX);
			_trkStickerX = new TrackBar { Minimum = 0, Maximum = 100, Value = 50, Width = 310, Height = 26, TickStyle = TickStyle.None };
			WireLiveTrackBar(_trkStickerX, val =>
			{
				if (_selectedSticker == null) return;
				_selectedSticker.XPercent = val;
				_lblStickerX.Text = $"↔️ 水平位置 (X): {val}%";
			}, () => { SaveSettings(); RefreshStickerList(); });
			dFlow.Controls.Add(_trkStickerX);

			_lblStickerY = new Label { Text = "↕️ 垂直位置 (Y): 50%", ForeColor = TextPrimary, Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
			dFlow.Controls.Add(_lblStickerY);
			_trkStickerY = new TrackBar { Minimum = 0, Maximum = 100, Value = 50, Width = 310, Height = 26, TickStyle = TickStyle.None };
			WireLiveTrackBar(_trkStickerY, val =>
			{
				if (_selectedSticker == null) return;
				_selectedSticker.YPercent = val;
				_lblStickerY.Text = $"↕️ 垂直位置 (Y): {val}%";
			}, () => { SaveSettings(); RefreshStickerList(); });
			dFlow.Controls.Add(_trkStickerY);

			// 快捷位置胶囊按钮
			var posShortcuts = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 4) };
			void AddPosBtn(string title, int px, int py, int w = 56)
			{
				var b = CreateButton(title, CardBorder, TextPrimary, w);
				b.Height = 22;
				b.Font = new Font("Microsoft YaHei UI", 7.8f);
				b.Click += (_, _) =>
				{
					if (_selectedSticker == null) return;
					_selectedSticker.XPercent = px;
					_selectedSticker.YPercent = py;
					_trkStickerX.Value = px;
					_trkStickerY.Value = py;
					_lblStickerX.Text = $"↔️ 水平位置 (X): {px}%";
					_lblStickerY.Text = $"↕️ 垂直位置 (Y): {py}%";
					SaveSettings();
					RefreshStickerList();
					UpdatePreview();
				};
				posShortcuts.Controls.Add(b);
			}
			AddPosBtn("🎯 居中", 50, 50, 58);
			AddPosBtn("↖ 左上", 18, 15);
			AddPosBtn("↗ 右上", 82, 15);
			AddPosBtn("↙ 左下", 18, 85);
			AddPosBtn("↘ 右下", 82, 85);
			dFlow.Controls.Add(posShortcuts);

			// 3. 缩放大小 (Scale)
			_lblStickerScale = new Label { Text = "🔍 缩放大小: 25%", ForeColor = TextPrimary, Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
			dFlow.Controls.Add(_lblStickerScale);
			_trkStickerScale = new TrackBar { Minimum = 5, Maximum = 90, Value = 25, Width = 310, Height = 26, TickStyle = TickStyle.None };
			WireLiveTrackBar(_trkStickerScale, val =>
			{
				if (_selectedSticker == null) return;
				_selectedSticker.ScalePercent = val;
				_lblStickerScale.Text = $"🔍 缩放大小: {val}%";
			}, () => { SaveSettings(); RefreshStickerList(); });
			dFlow.Controls.Add(_trkStickerScale);

			// 4. 旋转角度 (Rotation)
			var rotTitleRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
			_lblStickerRotate = new Label { Text = "🔄 旋转角度: 0°", ForeColor = TextPrimary, Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 4, 6, 0) };
			rotTitleRow.Controls.Add(_lblStickerRotate);
			var btnResetRot = CreateButton("重置 0°", CardBorder, TextPrimary, 58);
			btnResetRot.Height = 20;
			btnResetRot.Font = new Font("Microsoft YaHei UI", 7.5f);
			btnResetRot.Click += (_, _) =>
			{
				if (_selectedSticker == null) return;
				_selectedSticker.RotationAngle = 0f;
				_trkStickerRotate.Value = 0;
				_lblStickerRotate.Text = "🔄 旋转角度: 0°";
				SaveSettings();
				RefreshStickerList();
				UpdatePreview();
			};
			rotTitleRow.Controls.Add(btnResetRot);
			dFlow.Controls.Add(rotTitleRow);

			_trkStickerRotate = new TrackBar { Minimum = -180, Maximum = 180, Value = 0, Width = 310, Height = 26, TickStyle = TickStyle.None };
			WireLiveTrackBar(_trkStickerRotate, val =>
			{
				if (_selectedSticker == null) return;
				_selectedSticker.RotationAngle = val;
				_lblStickerRotate.Text = $"🔄 旋转角度: {val}°";
			}, () => { SaveSettings(); RefreshStickerList(); });
			dFlow.Controls.Add(_trkStickerRotate);

			// 5. 出现时间与持续时长 (Timing)
			var timeRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 4) };
			timeRow.Controls.Add(new Label { Text = "⏱️ 起始(秒): ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
			_numStickerStart = new NumericUpDown
			{
				DecimalPlaces = 1,
				Increment = 0.5m,
				Minimum = 0,
				Maximum = 999,
				Value = 0,
				Width = 60,
				BackColor = InputBg,
				ForeColor = TextPrimary
			};
			_numStickerStart.ValueChanged += (_, _) =>
			{
				if (_selectedSticker == null) return;
				_selectedSticker.StartTimeSec = (double)_numStickerStart.Value;
				SaveSettings();
				RefreshStickerList();
			};
			timeRow.Controls.Add(_numStickerStart);

			timeRow.Controls.Add(new Label { Text = " ⏳ 时长(秒): ", ForeColor = TextMuted, AutoSize = true, Margin = new Padding(4, 4, 2, 0) });
			_numStickerDuration = new NumericUpDown
			{
				DecimalPlaces = 1,
				Increment = 0.5m,
				Minimum = 0,
				Maximum = 999,
				Value = 0,
				Width = 60,
				BackColor = InputBg,
				ForeColor = TextPrimary
			};
			_numStickerDuration.ValueChanged += (_, _) =>
			{
				if (_selectedSticker == null) return;
				_selectedSticker.DurationSec = (double)_numStickerDuration.Value;
				SaveSettings();
				RefreshStickerList();
			};
			timeRow.Controls.Add(_numStickerDuration);
			dFlow.Controls.Add(timeRow);

			dFlow.Controls.Add(new Label { Text = "💡 持续时长设为 0 表示全程显示直到视频结束。", ForeColor = TextMuted, Font = new Font("Microsoft YaHei UI", 7.5f), AutoSize = true, Margin = new Padding(0, 0, 0, 4) });

			// 6. 不透明度与循环播放
			var opRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 2, 0, 0) };
			_lblStickerOpacity = new Label { Text = "🌓 不透明度: 100%", ForeColor = TextPrimary, Font = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 4, 6, 0) };
			opRow.Controls.Add(_lblStickerOpacity);
			_chkStickerLoop = new CheckBox { Text = "🔁 循环播放", ForeColor = TextPrimary, AutoSize = true, Checked = true, Margin = new Padding(10, 2, 0, 0) };
			_chkStickerLoop.CheckedChanged += (_, _) =>
			{
				if (_selectedSticker == null) return;
				_selectedSticker.Loop = _chkStickerLoop.Checked;
				SaveSettings();
			};
			opRow.Controls.Add(_chkStickerLoop);
			dFlow.Controls.Add(opRow);

			_trkStickerOpacity = new TrackBar { Minimum = 10, Maximum = 100, Value = 100, Width = 310, Height = 26, TickStyle = TickStyle.None };
			WireLiveTrackBar(_trkStickerOpacity, val =>
			{
				if (_selectedSticker == null) return;
				_selectedSticker.OpacityPercent = val;
				_lblStickerOpacity.Text = $"🌓 不透明度: {val}%";
			}, () => { SaveSettings(); });
			dFlow.Controls.Add(_trkStickerOpacity);

			_pnlStickerDetails.Controls.Add(dFlow);
			flow.Controls.Add(_pnlStickerDetails);

			pnl.Controls.Add(flow);
			RefreshStickerList();
			return pnl;
		}

		private void RefreshStickerList()
		{
			if (_lstGifStickers == null) return;
			_lstGifStickers.BeginUpdate();
			_lstGifStickers.Items.Clear();
			if (_settings.GifStickers != null)
			{
				foreach (var stk in _settings.GifStickers)
				{
					_lstGifStickers.Items.Add(stk);
				}
			}
			if (_selectedSticker != null && _settings.GifStickers != null && _settings.GifStickers.Contains(_selectedSticker))
			{
				_lstGifStickers.SelectedItem = _selectedSticker;
			}
			else if (_settings.GifStickers != null && _settings.GifStickers.Count > 0)
			{
				_selectedSticker = _settings.GifStickers[0];
				_lstGifStickers.SelectedItem = _selectedSticker;
			}
			else
			{
				_selectedSticker = null;
			}
			_lstGifStickers.EndUpdate();
			SyncStickerDetailsFromModel();
		}

		private void SyncStickerDetailsFromModel()
		{
			if (_pnlStickerDetails == null) return;
			if (_selectedSticker == null)
			{
				_pnlStickerDetails.Visible = false;
				return;
			}
			_pnlStickerDetails.Visible = true;
			_txtStickerPath.Text = _selectedSticker.FilePath;
			_trkStickerX.Value = Math.Max(0, Math.Min(100, (int)Math.Round(_selectedSticker.XPercent)));
			_lblStickerX.Text = $"↔️ 水平位置 (X): {_trkStickerX.Value}%";
			_trkStickerY.Value = Math.Max(0, Math.Min(100, (int)Math.Round(_selectedSticker.YPercent)));
			_lblStickerY.Text = $"↕️ 垂直位置 (Y): {_trkStickerY.Value}%";
			_trkStickerScale.Value = Math.Max(5, Math.Min(90, (int)Math.Round(_selectedSticker.ScalePercent)));
			_lblStickerScale.Text = $"🔍 缩放大小: {_trkStickerScale.Value}%";
			_trkStickerRotate.Value = Math.Max(-180, Math.Min(180, (int)Math.Round(_selectedSticker.RotationAngle)));
			_lblStickerRotate.Text = $"🔄 旋转角度: {_trkStickerRotate.Value}°";
			_numStickerStart.Value = (decimal)Math.Max(0, _selectedSticker.StartTimeSec);
			_numStickerDuration.Value = (decimal)Math.Max(0, _selectedSticker.DurationSec);
			_trkStickerOpacity.Value = Math.Max(10, Math.Min(100, _selectedSticker.OpacityPercent));
			_lblStickerOpacity.Text = $"🌓 不透明度: {_trkStickerOpacity.Value}%";
			_chkStickerLoop.Checked = _selectedSticker.Loop;
		}

		private static Label CreateSectionHeader(string text)
		{
			return new Label
			{
				Text = text,
				Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
				ForeColor = AccentYellow,
				AutoSize = true,
				Margin = new Padding(0, 8, 0, 4)
			};
		}

		private static Panel CreateCollapsibleCard(string title, Control contentControl, bool defaultExpanded = true)
		{
			var card = new Panel
			{
				Width = 310,
				Margin = new Padding(0, 0, 0, 8),
				BackColor = Color.FromArgb(20, 27, 40)
			};

			var header = new Panel
			{
				Dock = DockStyle.Top,
				Height = 36,
				BackColor = defaultExpanded ? Color.FromArgb(30, 42, 64) : Color.FromArgb(24, 34, 52),
				Cursor = Cursors.Hand,
				Padding = new Padding(8, 0, 8, 0)
			};

			var lblToggle = new Label
			{
				Text = defaultExpanded ? "▲ 收起" : "▼ 展开",
				ForeColor = defaultExpanded ? AccentBlue : TextMuted,
				Font = new Font("Microsoft YaHei UI", 8.5f),
				AutoSize = false,
				Dock = DockStyle.Right,
				Width = 65,
				Height = 36,
				TextAlign = ContentAlignment.MiddleRight,
				Cursor = Cursors.Hand
			};

			var lblTitle = new Label
			{
				Text = title,
				ForeColor = Color.White,
				Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold),
				AutoSize = false,
				Dock = DockStyle.Fill,
				Height = 36,
				TextAlign = ContentAlignment.MiddleLeft,
				Cursor = Cursors.Hand
			};

			header.Controls.Add(lblTitle);
			header.Controls.Add(lblToggle);

			contentControl.Dock = DockStyle.Top;
			contentControl.Visible = defaultExpanded;
			contentControl.Padding = new Padding(4, 4, 4, 6);

			void UpdateCardHeight()
			{
				if (contentControl.Visible)
				{
					contentControl.PerformLayout();
					int ch = contentControl.PreferredSize.Height;
					contentControl.Height = ch;
					card.Height = header.Height + ch + 8;
				}
				else
				{
					card.Height = header.Height;
				}
			}

			void Toggle()
			{
				contentControl.Visible = !contentControl.Visible;
				lblToggle.Text = contentControl.Visible ? "▲ 收起" : "▼ 展开";
				lblToggle.ForeColor = contentControl.Visible ? AccentBlue : TextMuted;
				header.BackColor = contentControl.Visible ? Color.FromArgb(30, 42, 64) : Color.FromArgb(24, 34, 52);
				UpdateCardHeight();
			}

			header.Click += (_, _) => Toggle();
			lblTitle.Click += (_, _) => Toggle();
			lblToggle.Click += (_, _) => Toggle();

			header.MouseEnter += (_, _) => header.BackColor = Color.FromArgb(42, 58, 86);
			lblTitle.MouseEnter += (_, _) => header.BackColor = Color.FromArgb(42, 58, 86);
			lblToggle.MouseEnter += (_, _) => header.BackColor = Color.FromArgb(42, 58, 86);

			header.MouseLeave += (_, _) => header.BackColor = contentControl.Visible ? Color.FromArgb(30, 42, 64) : Color.FromArgb(24, 34, 52);
			lblTitle.MouseLeave += (_, _) => header.BackColor = contentControl.Visible ? Color.FromArgb(30, 42, 64) : Color.FromArgb(24, 34, 52);
			lblToggle.MouseLeave += (_, _) => header.BackColor = contentControl.Visible ? Color.FromArgb(30, 42, 64) : Color.FromArgb(24, 34, 52);

			contentControl.SizeChanged += (_, _) => UpdateCardHeight();

			card.Controls.Add(contentControl);
			card.Controls.Add(header);

			UpdateCardHeight();
			return card;
		}

		private static Label CreateLabel(string text)
		{
			return new Label
			{
				Text = text,
				ForeColor = TextMuted,
				AutoSize = true,
				Margin = new Padding(0, 3, 0, 2)
			};
		}

		private static Panel CreateSpacer()
		{
			return new Panel
			{
				Height = 1,
				Width = 310,
				BackColor = CardBorder,
				Margin = new Padding(0, 6, 0, 6)
			};
		}

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
				Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular),
				Margin = new Padding(0, 0, 5, 0)
			};
			btn.FlatAppearance.BorderSize = 0;
			return btn;
		}

		// 实时预览视窗自定义控件
		private sealed class PreviewCanvasBox : Control
		{
			private readonly ScriptBatchStudioPanel _panel;
			private bool _isDraggingSticker = false;
			private bool _isRotatingSticker = false;
			private GifStickerItem _activeSticker = null;
			private Rectangle _lastScreenRect;

			public PreviewCanvasBox(ScriptBatchStudioPanel panel)
			{
				_panel = panel;
				SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				base.OnPaint(e);
				var g = e.Graphics;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

				int clientW = ClientSize.Width;
				int clientH = ClientSize.Height;
				if (clientW <= 30 || clientH <= 30) return;

				bool isVertical = _panel._settings.CanvasHeight >= _panel._settings.CanvasWidth;
				float targetAspect = isVertical ? (9f / 16f) : (16f / 9f);

				// 外围预留舒适呼吸留白 (上下 20px / 左右 16px，彻底消除贴边和被遮挡感)
				int padH = 16;
				int padV = 20;
				int availW = Math.Max(20, clientW - padH * 2);
				int availH = Math.Max(20, clientH - padV * 2);

				// 手机外壳边框厚度 (左右各 8px，上 12px，下 15px)
				int bezelSide = isVertical ? Math.Max(7, (int)(availW * 0.024f)) : 10;
				int bezelTop = isVertical ? Math.Max(12, (int)(availH * 0.024f)) : 8;
				int bezelBottom = isVertical ? Math.Max(15, (int)(availH * 0.026f)) : 8;

				int maxScreenW = availW - bezelSide * 2;
				int maxScreenH = availH - bezelTop - bezelBottom;

				int screenW, screenH;
				if ((float)maxScreenW / maxScreenH > targetAspect)
				{
					// 容器更宽：高度撑满，宽度严格按 9:16 计算
					screenH = maxScreenH;
					screenW = (int)Math.Round(screenH * targetAspect);
				}
				else
				{
					// 容器更高：宽度撑满，高度严格按 9:16 计算
					screenW = maxScreenW;
					screenH = (int)Math.Round(screenW / targetAspect);
				}

				// 安全兜底防护：严禁超出可用视窗
				if (screenH > maxScreenH)
				{
					screenH = maxScreenH;
					screenW = (int)Math.Round(screenH * targetAspect);
				}
				if (screenW > maxScreenW)
				{
					screenW = maxScreenW;
					screenH = (int)Math.Round(screenW / targetAspect);
				}

				int phoneW = screenW + bezelSide * 2;
				int phoneH = screenH + bezelTop + bezelBottom;
				int phoneX = (clientW - phoneW) / 2;
				int phoneY = (clientH - phoneH) / 2;
				var phoneRect = new Rectangle(phoneX, phoneY, phoneW, phoneH);

				int screenX = phoneX + bezelSide;
				int screenY = phoneY + bezelTop;
				var screenRect = new Rectangle(screenX, screenY, screenW, screenH);
				_lastScreenRect = screenRect;

				float phoneRadius = Math.Max(12f, Math.Min(24f, phoneW * 0.065f));
				float screenRadius = Math.Max(8f, phoneRadius - 4f);

				// 1. 绘制手机立体微阴影与暗金属精致机身
				using (var shadowBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
				using (var shadowPath = GetRoundedRectPath(new RectangleF(phoneX + 3, phoneY + 5, phoneW, phoneH), phoneRadius))
				{
					g.FillPath(shadowBrush, shadowPath);
				}

				using (var phonePath = GetRoundedRectPath(new RectangleF(phoneX, phoneY, phoneW, phoneH), phoneRadius))
				using (var phoneBrush = new LinearGradientBrush(phoneRect, Color.FromArgb(34, 45, 63), Color.FromArgb(15, 23, 42), 65f))
				using (var phoneBorderPen = new Pen(Color.FromArgb(71, 85, 105), 1.8f))
				{
					g.FillPath(phoneBrush, phonePath);
					g.DrawPath(phoneBorderPen, phonePath);
				}

				// 2. 屏幕区域裁切绘制 (圆角全屏裁切，原汁原味还原成片)
				using var screenPath = GetRoundedRectPath(screenRect, screenRadius);
				var prevClip = g.Clip;
				g.SetClip(screenPath);

				// 2.1 绘制模拟屏幕背景 (等比居中裁切 Cover 模式，杜绝拉伸畸变)
				if (_panel._cachedFootageThumbnail != null)
				{
					var thumb = _panel._cachedFootageThumbnail;
					float thumbAspect = thumb.Width / (float)thumb.Height;
					Rectangle srcRect;
					if (thumbAspect > targetAspect)
					{
						int srcW = (int)(thumb.Height * targetAspect);
						int srcX = (thumb.Width - srcW) / 2;
						srcRect = new Rectangle(srcX, 0, srcW, thumb.Height);
					}
					else
					{
						int srcH = (int)(thumb.Width / targetAspect);
						int srcY = (thumb.Height - srcH) / 2;
						srcRect = new Rectangle(0, srcY, thumb.Width, srcH);
					}
					g.DrawImage(thumb, screenRect, srcRect, GraphicsUnit.Pixel);
				}
				else
				{
					using var bgBrush = new LinearGradientBrush(screenRect, Color.FromArgb(16, 28, 48), Color.FromArgb(7, 12, 22), 45f);
					g.FillRectangle(bgBrush, screenRect);

					using var gridPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1f);
					for (int x = screenX; x < screenX + screenW; x += 30) g.DrawLine(gridPen, x, screenY, x, screenY + screenH);
					for (int y = screenY; y < screenY + screenH; y += 30) g.DrawLine(gridPen, screenX, y, screenX + screenW, y);
				}

				// 2.2 获取当前要预览的文案 (默认采用爆款神父文案)
				string sampleText = "filho precisa desta oração hoje.";
				if (_panel._grid != null && _panel._grid.SelectedRows.Count > 0 && _panel._grid.SelectedRows[0].Tag is ScriptBatchItem item)
				{
					if (!string.IsNullOrWhiteSpace(item.ScriptText))
					{
						sampleText = item.ScriptText.Split(new[] { '\r', '\n', '.' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
					}
				}

				// 2.3 绘制字幕文字与底框 (支持预设库样式与 XY 偏移)
				var preset = SubtitlePresetCatalog.GetById(_panel._settings.SubtitleStyleId);
				int displayMode = _panel._settings.SubtitleDisplayMode;
				if (preset.DisplayMode == 3) displayMode = 3;

				float posPercent = _panel._settings.SubtitleVerticalPercent / 100f;
				float subCenterY = screenY + (screenH * posPercent);
				float scale = screenW / 1080f;
				float subShiftX = _panel._settings.SubtitleOffsetX * scale;

				using var guidePen = new Pen(Color.FromArgb(35, 245, 158, 11), 1f) { DashStyle = DashStyle.Dash };
				g.DrawLine(guidePen, screenX + 10, subCenterY, screenX + screenW - 10, subCenterY);

				float previewFontSize = Math.Max(12f, _panel._settings.SubtitleFontSize * scale);
				string fontName = string.IsNullOrWhiteSpace(_panel._settings.SubtitleFontFamily) ? "Arial" : _panel._settings.SubtitleFontFamily;
				using var font = new Font(fontName, previewFontSize, FontStyle.Bold, GraphicsUnit.Pixel);

				if (displayMode == 3) // 逐词红底框高亮 (图1效果：短句排版，红底框+黄字，白字粗黑边)
				{
					string wrapped = ScriptBatchComposer.WrapToShortLines(sampleText, 16);
					string[] subLines = wrapped.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
					if (subLines.Length == 0) subLines = new[] { sampleText };

					float lineH = font.GetHeight(g) * 1.15f;
					float totalH = subLines.Length * lineH;
					float curY = subCenterY - (totalH / 2f);

					for (int li = 0; li < subLines.Length; li++)
					{
						string lText = subLines[li].Trim();
						var words = lText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
						if (words.Length == 0) continue;

						float[] wordWidths = new float[words.Length];
						float spaceW = g.MeasureString(" ", font).Width * 0.7f;
						float rowW = 0;
						for (int wi = 0; wi < words.Length; wi++)
						{
							wordWidths[wi] = g.MeasureString(words[wi], font).Width;
							rowW += wordWidths[wi];
							if (wi > 0) rowW += spaceW;
						}

						float wordX = screenX + (screenW - rowW) / 2f + subShiftX;
						int hlWordIdx = (li == 0 && words.Length >= 2) ? 1 : (li == 0 ? 0 : -1);

						for (int wi = 0; wi < words.Length; wi++)
						{
							string word = words[wi];
							float wW = wordWidths[wi];

							if (wi == hlWordIdx)
							{
								float padBoxX = 8f * scale + 4f;
								float padBoxY = 3f * scale + 2f;
								var boxRect = new RectangleF(wordX - padBoxX, curY - padBoxY, wW + padBoxX * 2, lineH + padBoxY * 2);
								using var redBrush = new SolidBrush(Color.FromArgb(208, 0, 0));
								using var boxPath = GetRoundedRectPath(boxRect, Math.Max(3f, 6f * scale));
								g.FillPath(redBrush, boxPath);

								using var yelBrush = new SolidBrush(Color.FromArgb(255, 255, 0));
								g.DrawString(word, font, yelBrush, wordX, curY);
							}
							else
							{
								float strokeW = Math.Max(2f, 6f * scale);
								using var strokePen = new Pen(Color.Black, strokeW) { LineJoin = LineJoin.Round };
								using var path = new GraphicsPath();
								path.AddString(word, font.FontFamily, (int)font.Style, font.Size, new PointF(wordX, curY), StringFormat.GenericDefault);
								g.DrawPath(strokePen, path);

								using var whiteBrush = new SolidBrush(Color.White);
								g.DrawString(word, font, whiteBrush, wordX, curY);
							}

							wordX += wW + spaceW;
						}

						curY += lineH;
					}
				}
				else
				{
					float maxSubW = screenW * 0.90f;
					var subFormat = new StringFormat
					{
						Alignment = StringAlignment.Center,
						LineAlignment = StringAlignment.Center,
						FormatFlags = StringFormatFlags.LineLimit
					};

					var textSize = g.MeasureString(sampleText, font, (int)maxSubW, subFormat);
					float textX = screenX + (screenW - textSize.Width) / 2 + subShiftX;
					float textY = subCenterY - (textSize.Height / 2);
					var subRect = new RectangleF(textX, textY, textSize.Width, textSize.Height);

					if (preset.HasBox)
					{
						float padBoxX = 14f * scale + 4f;
						float padBoxY = 6f * scale + 2f;
						var boxRect = new RectangleF(textX - padBoxX, textY - padBoxY, textSize.Width + padBoxX * 2, textSize.Height + padBoxY * 2);

						using var boxBrush = new SolidBrush(preset.PreviewBoxColor);
						using var boxPath = GetRoundedRectPath(boxRect, Math.Max(3f, 6f * scale));
						g.FillPath(boxBrush, boxPath);
					}
					else
					{
						Color strokeColor = preset.PreviewStrokeColor;
						float strokeWidth = Math.Max(1.5f, preset.OutlineWidth * scale);

						using var path = new GraphicsPath();
						path.AddString(
							sampleText,
							font.FontFamily,
							(int)font.Style,
							font.Size,
							subRect,
							subFormat
						);

						using var strokePen = new Pen(strokeColor, strokeWidth) { LineJoin = LineJoin.Round };
						g.DrawPath(strokePen, path);
					}

					using (var textBrush = new SolidBrush(preset.PreviewTextColor))
					{
						g.DrawString(sampleText, font, textBrush, subRect, subFormat);
					}
				}

				// 2.4 绘制水印预览 (当且仅当勾选启用时纯净渲染，还原最终成片)
				if (_panel._settings.WatermarkEnabled)
				{
					float wmCanvasW = screenW;
					float wmCanvasH = screenH;
					int wmPos = _panel._settings.WatermarkPosition;
					float offX = (_panel._settings.WatermarkOffsetX * scale);
					float offY = (_panel._settings.WatermarkOffsetY * scale);

					if (_panel._settings.WatermarkType == 0) // 文字水印
					{
						string wmText = string.IsNullOrWhiteSpace(_panel._settings.WatermarkText) ? "Voice: ElevenLabs" : _panel._settings.WatermarkText;
						float wmFontSize = Math.Max(8f, _panel._settings.WatermarkFontSize * scale);
						string wmFontName = string.IsNullOrWhiteSpace(_panel._settings.SubtitleFontFamily) ? "Arial" : _panel._settings.SubtitleFontFamily;
						using var wmFont = new Font(wmFontName, wmFontSize, FontStyle.Bold, GraphicsUnit.Pixel);

						float maxWmWidth = Math.Max(60f, wmCanvasW * 0.85f - offX);
						var stringFormat = new StringFormat
						{
							Alignment = (wmPos == 0 || wmPos == 1) ? StringAlignment.Far :
										((wmPos == 4 || wmPos == 5) ? StringAlignment.Center : StringAlignment.Near),
							LineAlignment = StringAlignment.Near,
							FormatFlags = StringFormatFlags.LineLimit
						};

						var sz = g.MeasureString(wmText, wmFont, (int)maxWmWidth, stringFormat);
						float itemW = Math.Min(maxWmWidth, sz.Width + 2);
						float itemH = sz.Height + 2;

						PointF pt = ScriptBatchComposer.CalculateWatermarkPosition(wmPos, wmCanvasW, wmCanvasH, itemW, itemH, offX, offY);
						float actualX = screenX + pt.X;
						float actualY = screenY + pt.Y;
						var textRect = new RectangleF(actualX, actualY, itemW, itemH);

						int alpha = (int)Math.Max(35, Math.Min(255, _panel._settings.WatermarkTextOpacity * 2.55));
						Color baseCol = ScriptBatchComposer.ParseColor(_panel._settings.WatermarkTextColorHex, Color.White);
						Color textCol = Color.FromArgb(alpha, baseCol.R, baseCol.G, baseCol.B);
						Color shadowCol = Color.FromArgb((int)(alpha * 0.7), 0, 0, 0);

						using (var shadowBrush = new SolidBrush(shadowCol))
						{
							g.DrawString(wmText, wmFont, shadowBrush, new RectangleF(actualX + 1, actualY + 1, itemW, itemH), stringFormat);
						}
						using (var wmBrush = new SolidBrush(textCol))
						{
							g.DrawString(wmText, wmFont, wmBrush, textRect, stringFormat);
						}
					}
					else // 图片水印
					{
						if (!string.IsNullOrWhiteSpace(_panel._settings.WatermarkImagePath) && File.Exists(_panel._settings.WatermarkImagePath))
						{
							try
							{
								using var img = Image.FromFile(_panel._settings.WatermarkImagePath);
								float imgScale = Math.Max(5, Math.Min(60, _panel._settings.WatermarkImageScalePercent)) / 100f;
								float targetW = wmCanvasW * imgScale;
								float targetH = img.Height * (targetW / img.Width);

								PointF pt = ScriptBatchComposer.CalculateWatermarkPosition(wmPos, wmCanvasW, wmCanvasH, targetW, targetH, offX, offY);
								float actualX = screenX + pt.X;
								float actualY = screenY + pt.Y;

								float opacity = Math.Max(0.2f, Math.Min(1.0f, _panel._settings.WatermarkImageOpacity / 100f));
								var colMatrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
								using var attr = new System.Drawing.Imaging.ImageAttributes();
								attr.SetColorMatrix(colMatrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
								g.DrawImage(img, new Rectangle((int)actualX, (int)actualY, (int)targetW, (int)targetH), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
							}
							catch { }
						}
					}
				}

				// 2.5 绘制 GIF 动图贴纸 (支持多贴纸、旋转、缩放、不透明度、选中高亮框与旋转控制手柄)
				if (_panel._settings.GifStickers != null && _panel._settings.GifStickers.Count > 0)
				{
					foreach (var stk in _panel._settings.GifStickers)
					{
						if (!stk.Enabled || string.IsNullOrWhiteSpace(stk.FilePath) || !File.Exists(stk.FilePath))
							continue;

						var img = _panel.GetOrLoadStickerImage(stk.FilePath);
						if (img == null) continue;

						try { ImageAnimator.UpdateFrames(img); } catch { }

						float targetW = screenW * (stk.ScalePercent / 100f);
						float targetH = targetW * (img.Height / (float)img.Width);
						float centerX = screenX + (screenW * (stk.XPercent / 100f));
						float centerY = screenY + (screenH * (stk.YPercent / 100f));

						var state = g.Save();
						g.TranslateTransform(centerX, centerY);
						if (Math.Abs(stk.RotationAngle) > 0.01f)
						{
							g.RotateTransform(stk.RotationAngle);
						}

						float opacity = Math.Max(0.1f, Math.Min(1.0f, stk.OpacityPercent / 100f));
						using (var attr = new System.Drawing.Imaging.ImageAttributes())
						{
							var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
							attr.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);

							var drawRect = new RectangleF(-targetW / 2f, -targetH / 2f, targetW, targetH);
							g.DrawImage(img, new Rectangle((int)drawRect.X, (int)drawRect.Y, (int)drawRect.Width, (int)drawRect.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attr);
						}

						// 选中指示虚线边框与交互控制手柄
						if (stk == _panel._selectedSticker)
						{
							using var selPen = new Pen(Color.FromArgb(59, 130, 246), 1.8f) { DashStyle = DashStyle.Dash };
							g.DrawRectangle(selPen, -targetW / 2f, -targetH / 2f, targetW, targetH);

							// 4 个角控制手柄
							using var handleBrush = new SolidBrush(Color.White);
							using var handlePen = new Pen(Color.FromArgb(59, 130, 246), 1.5f);
							float hs = 6f;
							g.FillRectangle(handleBrush, -targetW / 2f - hs / 2, -targetH / 2f - hs / 2, hs, hs);
							g.DrawRectangle(handlePen, -targetW / 2f - hs / 2, -targetH / 2f - hs / 2, hs, hs);
							g.FillRectangle(handleBrush, targetW / 2f - hs / 2, -targetH / 2f - hs / 2, hs, hs);
							g.DrawRectangle(handlePen, targetW / 2f - hs / 2, -targetH / 2f - hs / 2, hs, hs);
							g.FillRectangle(handleBrush, -targetW / 2f - hs / 2, targetH / 2f - hs / 2, hs, hs);
							g.DrawRectangle(handlePen, -targetW / 2f - hs / 2, targetH / 2f - hs / 2, hs, hs);
							g.FillRectangle(handleBrush, targetW / 2f - hs / 2, targetH / 2f - hs / 2, hs, hs);
							g.DrawRectangle(handlePen, targetW / 2f - hs / 2, targetH / 2f - hs / 2, hs, hs);

							// 顶部旋转控制连线与圆点
							using var rotLinePen = new Pen(Color.FromArgb(245, 158, 11), 1.5f);
							using var rotCircleBrush = new SolidBrush(Color.FromArgb(245, 158, 11));
							g.DrawLine(rotLinePen, 0, -targetH / 2f, 0, -targetH / 2f - 14);
							g.FillEllipse(rotCircleBrush, -4, -targetH / 2f - 18, 8, 8);
						}

						g.Restore(state);
					}
				}

				// 恢复屏幕全局裁切
				g.Clip = prevClip;

				// 3. 绘制真机屏幕细节 (灵动岛、手势小白条、精致屏幕内边框)
				if (isVertical)
				{
					// 顶部灵动岛 (Dynamic Island)
					int islandW = Math.Max(46, (int)(screenW * 0.28f));
					int islandH = Math.Max(12, (int)(screenH * 0.022f));
					int islandX = screenX + (screenW - islandW) / 2;
					int islandY = screenY + 4;
					var islandRect = new RectangleF(islandX, islandY, islandW, islandH);
					using (var islandPath = GetRoundedRectPath(islandRect, islandH / 2f))
					using (var islandBrush = new SolidBrush(Color.FromArgb(245, 10, 15, 20)))
					{
						g.FillPath(islandBrush, islandPath);
					}
					// 摄像头镜头反光微点
					using (var camBrush = new SolidBrush(Color.FromArgb(160, 30, 58, 95)))
					{
						g.FillEllipse(camBrush, islandX + islandW - 14, islandY + (islandH - 5) / 2f, 5, 5);
					}

					// 底部手势小白条 (Home Indicator)
					int barW = Math.Max(55, (int)(screenW * 0.35f));
					int barH = 3;
					int barX = screenX + (screenW - barW) / 2;
					int barY = screenY + screenH - 8;
					var barRect = new RectangleF(barX, barY, barW, barH);
					using (var barPath = GetRoundedRectPath(barRect, 1.5f))
					using (var barBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
					{
						g.FillPath(barBrush, barPath);
					}
				}

				// 精致屏幕微发光内边框
				using (var innerBorderPen = new Pen(Color.FromArgb(75, 255, 255, 255), 1f))
				{
					g.DrawPath(innerBorderPen, screenPath);
				}

				// 顶部悬浮角标 (半透明玻璃拟物徽标)
				using (var badgeBgBrush = new SolidBrush(Color.FromArgb(160, 15, 23, 42)))
				{
					var badgeRect = new RectangleF(screenX + 8, screenY + (isVertical ? 22 : 8), 136, 18);
					using var badgePath = GetRoundedRectPath(badgeRect, 4f);
					g.FillPath(badgeBgBrush, badgePath);
				}
				using (var textMutedBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
				using (var badgeFont2 = new Font("Microsoft YaHei UI", 7.2f, FontStyle.Bold))
				{
					g.DrawString(isVertical ? "📱 9:16 (1080×1920)" : "🖥️ 16:9 (1920×1080)", badgeFont2, textMutedBrush, screenX + 13, screenY + (isVertical ? 24 : 10));
				}
			}

			protected override void OnMouseDown(MouseEventArgs e)
			{
				base.OnMouseDown(e);
				if (e.Button != MouseButtons.Left) return;
				if (_lastScreenRect.Width <= 0 || _lastScreenRect.Height <= 0) return;

				// 优先检测是否点中了当前选中贴纸的顶部旋转手柄
				if (_panel._selectedSticker != null && _panel._selectedSticker.Enabled)
				{
					var selStk = _panel._selectedSticker;
					var selImg = _panel.GetOrLoadStickerImage(selStk.FilePath);
					if (selImg != null)
					{
						float targetW = _lastScreenRect.Width * (selStk.ScalePercent / 100f);
						float targetH = targetW * (selImg.Height / (float)selImg.Width);
						float centerX = _lastScreenRect.X + (_lastScreenRect.Width * (selStk.XPercent / 100f));
						float centerY = _lastScreenRect.Y + (_lastScreenRect.Height * (selStk.YPercent / 100f));

						float dx = e.X - centerX;
						float dy = e.Y - centerY;
						double rad = -selStk.RotationAngle * Math.PI / 180.0;
						float localX = (float)(dx * Math.Cos(rad) - dy * Math.Sin(rad));
						float localY = (float)(dx * Math.Sin(rad) + dy * Math.Cos(rad));

						if (Math.Abs(localX) <= 14 && localY >= -targetH / 2f - 24 && localY <= -targetH / 2f - 4)
						{
							_isRotatingSticker = true;
							_activeSticker = selStk;
							Capture = true;
							return;
						}
					}
				}

				// 从上至下反向检测所有贴纸本体
				if (_panel._settings.GifStickers != null)
				{
					for (int i = _panel._settings.GifStickers.Count - 1; i >= 0; i--)
					{
						var stk = _panel._settings.GifStickers[i];
						if (!stk.Enabled || string.IsNullOrWhiteSpace(stk.FilePath) || !File.Exists(stk.FilePath))
							continue;

						var img = _panel.GetOrLoadStickerImage(stk.FilePath);
						if (img == null) continue;

						float targetW = _lastScreenRect.Width * (stk.ScalePercent / 100f);
						float targetH = targetW * (img.Height / (float)img.Width);
						float centerX = _lastScreenRect.X + (_lastScreenRect.Width * (stk.XPercent / 100f));
						float centerY = _lastScreenRect.Y + (_lastScreenRect.Height * (stk.YPercent / 100f));

						float dx = e.X - centerX;
						float dy = e.Y - centerY;
						double rad = -stk.RotationAngle * Math.PI / 180.0;
						float localX = (float)(dx * Math.Cos(rad) - dy * Math.Sin(rad));
						float localY = (float)(dx * Math.Sin(rad) + dy * Math.Cos(rad));

						if (localX >= -targetW / 2f && localX <= targetW / 2f && localY >= -targetH / 2f && localY <= targetH / 2f)
						{
							_panel._selectedSticker = stk;
							_panel.SyncStickerDetailsFromModel();
							_activeSticker = stk;
							_isDraggingSticker = true;
							Capture = true;
							Invalidate();
							return;
						}
					}
				}
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				base.OnMouseMove(e);

				if (_isDraggingSticker && _activeSticker != null && _lastScreenRect.Width > 0 && _lastScreenRect.Height > 0)
				{
					float newX = (e.X - _lastScreenRect.X) / (float)_lastScreenRect.Width * 100f;
					float newY = (e.Y - _lastScreenRect.Y) / (float)_lastScreenRect.Height * 100f;
					_activeSticker.XPercent = (float)Math.Round(Math.Max(0f, Math.Min(100f, newX)));
					_activeSticker.YPercent = (float)Math.Round(Math.Max(0f, Math.Min(100f, newY)));
					_panel.SyncStickerDetailsFromModel();
					Invalidate();
					return;
				}

				if (_isRotatingSticker && _activeSticker != null && _lastScreenRect.Width > 0 && _lastScreenRect.Height > 0)
				{
					float centerX = _lastScreenRect.X + (_lastScreenRect.Width * (_activeSticker.XPercent / 100f));
					float centerY = _lastScreenRect.Y + (_lastScreenRect.Height * (_activeSticker.YPercent / 100f));
					double curAngle = Math.Atan2(e.Y - centerY, e.X - centerX) * 180.0 / Math.PI;
					float deg = (float)(curAngle + 90.0);
					while (deg > 180) deg -= 360;
					while (deg < -180) deg += 360;
					_activeSticker.RotationAngle = (float)Math.Round(deg);
					_panel.SyncStickerDetailsFromModel();
					Invalidate();
					return;
				}

				// 悬停光标指示
				if (_panel._selectedSticker != null && _lastScreenRect.Width > 0)
				{
					var selStk = _panel._selectedSticker;
					var selImg = _panel.GetOrLoadStickerImage(selStk.FilePath);
					if (selImg != null)
					{
						float targetW = _lastScreenRect.Width * (selStk.ScalePercent / 100f);
						float targetH = targetW * (selImg.Height / (float)selImg.Width);
						float centerX = _lastScreenRect.X + (_lastScreenRect.Width * (selStk.XPercent / 100f));
						float centerY = _lastScreenRect.Y + (_lastScreenRect.Height * (selStk.YPercent / 100f));

						float dx = e.X - centerX;
						float dy = e.Y - centerY;
						double rad = -selStk.RotationAngle * Math.PI / 180.0;
						float localX = (float)(dx * Math.Cos(rad) - dy * Math.Sin(rad));
						float localY = (float)(dx * Math.Sin(rad) + dy * Math.Cos(rad));

						if (Math.Abs(localX) <= 14 && localY >= -targetH / 2f - 24 && localY <= -targetH / 2f - 4)
						{
							Cursor = Cursors.Hand;
							return;
						}
						if (localX >= -targetW / 2f && localX <= targetW / 2f && localY >= -targetH / 2f && localY <= targetH / 2f)
						{
							Cursor = Cursors.SizeAll;
							return;
						}
					}
				}
				Cursor = Cursors.Default;
			}

			protected override void OnMouseUp(MouseEventArgs e)
			{
				base.OnMouseUp(e);
				if (_isDraggingSticker || _isRotatingSticker)
				{
					_isDraggingSticker = false;
					_isRotatingSticker = false;
					_activeSticker = null;
					Capture = false;
					_panel.SaveSettings();
					_panel.RefreshStickerList();
				}
			}

			protected override void OnMouseWheel(MouseEventArgs e)
			{
				base.OnMouseWheel(e);
				if (_panel._selectedSticker != null)
				{
					if ((ModifierKeys & Keys.Control) == Keys.Control)
					{
						// Ctrl + 滚轮缩放大小
						int delta = e.Delta > 0 ? 2 : -2;
						_panel._selectedSticker.ScalePercent = Math.Max(5, Math.Min(90, _panel._selectedSticker.ScalePercent + delta));
						_panel.SyncStickerDetailsFromModel();
						Invalidate();
						_panel.SaveSettings();
					}
					else if ((ModifierKeys & Keys.Shift) == Keys.Shift)
					{
						// Shift + 滚轮微调旋转角度
						int delta = e.Delta > 0 ? 5 : -5;
						float deg = _panel._selectedSticker.RotationAngle + delta;
						while (deg > 180) deg -= 360;
						while (deg < -180) deg += 360;
						_panel._selectedSticker.RotationAngle = deg;
						_panel.SyncStickerDetailsFromModel();
						Invalidate();
						_panel.SaveSettings();
					}
				}
			}

			private static GraphicsPath GetRoundedRectPath(RectangleF rect, float radius)
			{
				var path = new GraphicsPath();
				float d = radius * 2;
				if (d > rect.Width) d = rect.Width;
				if (d > rect.Height) d = rect.Height;
				path.AddArc(rect.X, rect.Y, d, d, 180, 90);
				path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
				path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
				path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
				path.CloseFigure();
				return path;
			}
		}
	}
}
