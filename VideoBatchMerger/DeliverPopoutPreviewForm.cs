using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoBatchMerger;

/// <summary>
/// 交付渲染工作台 · 独立全屏/大屏终审监视器 (Master Review Pop-out Window)
/// 支持超清大屏硬件加速实时播放、画面细节放大、音画与贴片层叠复核、播放/暂停、快进微调与进度拖拽
/// </summary>
internal sealed class DeliverPopoutPreviewForm : Form
{
	private readonly MainForm _mainForm;
	private readonly PictureBox _previewBox;
	private readonly Panel _canvasPanel;
	private readonly Label _timeLabel;
	private readonly TrackBar _scrubber;
	private readonly Button _btnPlayPause;
	private readonly Label _titleInfoLabel;
	private bool _isDraggingScrubber;
	private bool _isFullscreen;
	private FormWindowState _previousWindowState = FormWindowState.Normal;
	private FormBorderStyle _previousBorderStyle = FormBorderStyle.Sizable;

	public Panel CanvasPanel => _canvasPanel;
	public PictureBox PreviewBox => _previewBox;

	public DeliverPopoutPreviewForm(MainForm mainForm)
	{
		_mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));

		Text = "🎬 最终成片终审大屏监视器 · Master Review Studio";
		Width = 1060;
		Height = 780;
		MinimumSize = new Size(760, 520);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(12, 16, 24);
		ForeColor = Color.White;
		Icon = _mainForm.Icon;
		KeyPreview = true;

		// 1. Top Bar: Header & Info (Dock Top, Height 42)
		Panel topBar = new Panel
		{
			Dock = DockStyle.Top,
			Height = 42,
			BackColor = Color.FromArgb(20, 26, 38),
			Padding = new Padding(12, 6, 12, 6)
		};

		Label titleLbl = new Label
		{
			Text = "🎬 最终成片终审大屏监视器 · Master Review Player",
			Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
			ForeColor = Color.FromArgb(56, 189, 248),
			AutoSize = true,
			Location = new Point(12, 10)
		};
		topBar.Controls.Add(titleLbl);

		_titleInfoLabel = new Label
		{
			Text = "双击画面全屏/退出全屏 | 空格键播放/暂停 | 左右方向键单帧微调",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(148, 163, 184),
			AutoSize = true,
			Location = new Point(410, 12)
		};
		topBar.Controls.Add(_titleInfoLabel);

		Button btnClose = new Button
		{
			Text = "✕ 关闭大屏",
			Size = new Size(88, 28),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			Location = new Point(topBar.ClientSize.Width - 100, 7),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 45, 60),
			ForeColor = Color.FromArgb(226, 232, 240),
			Font = new Font("Microsoft YaHei UI", 8.5f),
			Cursor = Cursors.Hand
		};
		btnClose.FlatAppearance.BorderSize = 0;
		btnClose.Click += delegate { Close(); };
		topBar.Controls.Add(btnClose);

		topBar.Resize += delegate
		{
			btnClose.Location = new Point(topBar.ClientSize.Width - btnClose.Width - 12, 7);
			_titleInfoLabel.Location = new Point(Math.Min(410, Math.Max(12, topBar.ClientSize.Width - 520)), 12);
		};

		// 2. Bottom Control Panel (Dock Bottom, Height 86)
		Panel bottomPanel = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 86,
			BackColor = Color.FromArgb(18, 24, 34)
		};

		// Row 1: Scrubber and Time Label
		_scrubber = new TrackBar
		{
			Location = new Point(14, 2),
			Height = 28,
			Width = Math.Max(200, bottomPanel.ClientSize.Width - 190),
			Minimum = 0,
			Maximum = 1000,
			TickStyle = TickStyle.None,
			BackColor = Color.FromArgb(18, 24, 34),
			Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
		};
		_scrubber.MouseDown += delegate { _isDraggingScrubber = true; };
		_scrubber.MouseUp += delegate
		{
			_isDraggingScrubber = false;
			SeekFromScrubber();
		};
		_scrubber.Scroll += delegate
		{
			if (_isDraggingScrubber) SeekFromScrubber();
		};
		bottomPanel.Controls.Add(_scrubber);

		_timeLabel = new Label
		{
			Text = "00:00.0 / 00:00.0",
			Font = new Font("Consolas", 11f, FontStyle.Bold),
			ForeColor = Color.FromArgb(56, 189, 248),
			Size = new Size(160, 24),
			TextAlign = ContentAlignment.MiddleRight,
			Location = new Point(bottomPanel.ClientSize.Width - 174, 8),
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		bottomPanel.Controls.Add(_timeLabel);

		// Row 2: Playback Controls
		_btnPlayPause = new Button
		{
			Text = "▶ 播放 (空格)",
			Size = new Size(116, 32),
			Location = new Point(14, 44),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 99, 235),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
			Cursor = Cursors.Hand
		};
		_btnPlayPause.FlatAppearance.BorderSize = 0;
		_btnPlayPause.Click += delegate { _mainForm.ToggleDeliverPlayPause(); };
		bottomPanel.Controls.Add(_btnPlayPause);

		Button btnBack5 = MakeSmallBtn("⏮ -5s", 68, 44, delegate { _mainForm.StepDeliverPreview(-5.0); });
		Button btnBack1 = MakeSmallBtn("◀ -1s", 62, 44, delegate { _mainForm.StepDeliverPreview(-1.0); });
		Button btnFwd1 = MakeSmallBtn("+1s ▶", 62, 44, delegate { _mainForm.StepDeliverPreview(1.0); });
		Button btnFwd5 = MakeSmallBtn("+5s ⏭", 68, 44, delegate { _mainForm.StepDeliverPreview(5.0); });
		Button btnRefresh = MakeSmallBtn("🔄 刷新监视器", 110, 44, delegate { _mainForm.InitOrRefreshDeliverPreview(forceReload: true); });
		Button btnFullscreen = MakeSmallBtn("⛶ 全屏监看", 96, 44, delegate { ToggleFullscreen(); });

		btnBack5.Location = new Point(136, 44);
		btnBack1.Location = new Point(208, 44);
		btnFwd1.Location = new Point(274, 44);
		btnFwd5.Location = new Point(340, 44);
		btnRefresh.Location = new Point(414, 44);
		btnFullscreen.Location = new Point(530, 44);

		bottomPanel.Controls.Add(btnBack5);
		bottomPanel.Controls.Add(btnBack1);
		bottomPanel.Controls.Add(btnFwd1);
		bottomPanel.Controls.Add(btnFwd5);
		bottomPanel.Controls.Add(btnRefresh);
		bottomPanel.Controls.Add(btnFullscreen);

		Label bottomTip = new Label
		{
			Text = "💡 空格键 播放/暂停 | 左右键 单帧微调 | 双击画面 全屏切换",
			ForeColor = Color.FromArgb(148, 163, 184),
			Font = new Font("Microsoft YaHei UI", 8.5f),
			AutoSize = true,
			Location = new Point(638, 51)
		};
		bottomPanel.Controls.Add(bottomTip);

		bottomPanel.Resize += delegate
		{
			_scrubber.Width = Math.Max(100, bottomPanel.ClientSize.Width - 190);
			_timeLabel.Location = new Point(bottomPanel.ClientSize.Width - 174, 8);
			bottomTip.Visible = (bottomPanel.ClientSize.Width >= 880);
		};

		// 3. Central PictureBox Container (Dock Fill)
		_canvasPanel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.Black,
			Padding = new Padding(2)
		};

		_previewBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.Black,
			Cursor = Cursors.Hand
		};
		typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(_previewBox, true);
		_previewBox.DoubleClick += delegate { ToggleFullscreen(); };

		_canvasPanel.Controls.Add(_previewBox);

		// Docking order: Fill FIRST, then Top and Bottom
		Controls.Add(_canvasPanel);
		Controls.Add(topBar);
		Controls.Add(bottomPanel);

		KeyDown += DeliverPopoutPreviewForm_KeyDown;
		FormClosing += delegate { _mainForm.OnDeliverPopoutClosing(); };
		FormClosed += delegate { _mainForm.OnDeliverPopoutClosed(); };
	}

	private Button MakeSmallBtn(string text, int width, int top, EventHandler onClick)
	{
		Button btn = new Button
		{
			Text = text,
			Size = new Size(width, 32),
			Location = new Point(0, top),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(30, 41, 59),
			ForeColor = Color.FromArgb(226, 232, 240),
			Font = new Font("Microsoft YaHei UI", 8.5f),
			Cursor = Cursors.Hand
		};
		btn.FlatAppearance.BorderSize = 0;
		btn.Click += onClick;
		return btn;
	}

	private void SeekFromScrubber()
	{
		double dur = _mainForm.GetDeliverTotalDuration();
		if (dur <= 0.0) return;
		double pos = (_scrubber.Value / 1000.0) * dur;
		_mainForm.SeekDeliverPreview(pos);
	}

	private void ToggleFullscreen()
	{
		if (!_isFullscreen)
		{
			_previousWindowState = WindowState;
			_previousBorderStyle = FormBorderStyle;
			FormBorderStyle = FormBorderStyle.None;
			WindowState = FormWindowState.Maximized;
			_isFullscreen = true;
			_titleInfoLabel.Text = "已处于大屏全屏模式 | 按 ESC 或再次双击退出全屏";
		}
		else
		{
			FormBorderStyle = _previousBorderStyle;
			WindowState = _previousWindowState;
			_isFullscreen = false;
			_titleInfoLabel.Text = "双击画面全屏/退出全屏 | 空格键播放/暂停 | 左右方向键单帧微调";
		}
	}

	private void DeliverPopoutPreviewForm_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Space)
		{
			_mainForm.ToggleDeliverPlayPause();
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.Escape && _isFullscreen)
		{
			ToggleFullscreen();
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.Left)
		{
			_mainForm.StepDeliverPreview(-0.1);
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.Right)
		{
			_mainForm.StepDeliverPreview(0.1);
			e.Handled = true;
		}
	}

	public void UpdateFrame(Image frameImage, double currentSec, double totalSec, bool isPlaying)
	{
		if (IsDisposed || !IsHandleCreated) return;
		if (InvokeRequired)
		{
			BeginInvoke(new Action<Image, double, double, bool>(UpdateFrame), frameImage, currentSec, totalSec, isPlaying);
			return;
		}

		if (frameImage != null)
		{
			var old = _previewBox.Image;
			_previewBox.Image = new Bitmap(frameImage);
			old?.Dispose();
		}

		UpdateTimeAndScrubber(currentSec, totalSec, isPlaying);
	}

	public void UpdateTimeAndScrubber(double currentSec, double totalSec, bool isPlaying)
	{
		if (IsDisposed || !IsHandleCreated) return;
		if (InvokeRequired)
		{
			BeginInvoke(new Action<double, double, bool>(UpdateTimeAndScrubber), currentSec, totalSec, isPlaying);
			return;
		}

		_btnPlayPause.Text = isPlaying ? "⏸ 暂停 (空格)" : "▶ 播放 (空格)";
		_btnPlayPause.BackColor = isPlaying ? Color.FromArgb(234, 88, 12) : Color.FromArgb(37, 99, 235);

		if (!_isDraggingScrubber && totalSec > 0.0)
		{
			_scrubber.Value = (int)Math.Max(0, Math.Min(1000, (currentSec / totalSec) * 1000.0));
		}

		_timeLabel.Text = $"{FormatTime(currentSec)} / {FormatTime(totalSec)}";
	}

	private static string FormatTime(double sec)
	{
		if (sec < 0) sec = 0;
		TimeSpan ts = TimeSpan.FromSeconds(sec);
		return $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100:D1}";
	}
}
