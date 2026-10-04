using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoBatchMerger;

/// <summary>
/// 独立弹出式工作预览区窗口（专业视频剪辑工作区弹出监视器）
/// 支持硬件加速实时音画同步播放、字幕上下高度调节、全屏大屏监看、毫秒级精准寻道与进退微调
/// </summary>
internal sealed class CutEditPopoutPreviewForm : Form
{
	private readonly MainForm _mainForm;
	private readonly Panel _canvasPanel;
	private readonly PictureBox _previewBox;
	private readonly Label _timeLabel;
	private readonly TrackBar _scrubber;
	private readonly Button _btnPlayPause;
	private readonly Label _titleInfoLabel;
	private readonly TrackBar _subtitlePosSlider;
	private readonly Label _subtitlePosValLabel;
	private bool _isDraggingScrubber;
	private bool _isFullscreen;
	private FormWindowState _previousWindowState = FormWindowState.Normal;
	private FormBorderStyle _previousBorderStyle = FormBorderStyle.Sizable;

	public Panel CanvasPanel => _canvasPanel;
	public PictureBox PreviewBox => _previewBox;

	public CutEditPopoutPreviewForm(MainForm mainForm)
	{
		_mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));

		Text = "🖥️ 工作预览区 · 独立监视器 (Pop-out Workspace Studio)";
		Width = 1000;
		Height = 740;
		MinimumSize = new Size(720, 500);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(12, 16, 24);
		ForeColor = Color.White;
		Icon = _mainForm.Icon;
		KeyPreview = true;

		// 1. Top Bar: Header & Info (Dock Top, Height 40)
		Panel topBar = new Panel
		{
			Dock = DockStyle.Top,
			Height = 40,
			BackColor = Color.FromArgb(20, 26, 38),
			Padding = new Padding(12, 6, 12, 6)
		};

		Label titleLbl = new Label
		{
			Text = "▶ 独立监视器 · 实时音画与字幕对齐预审",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(56, 189, 248),
			AutoSize = true,
			Location = new Point(12, 10)
		};
		topBar.Controls.Add(titleLbl);

		_titleInfoLabel = new Label
		{
			Text = "空格键 播放/暂停 | 左右方向键 逐秒微调 | 双击画面 全屏切换",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(148, 163, 184),
			AutoSize = true,
			Location = new Point(310, 11)
		};
		topBar.Controls.Add(_titleInfoLabel);

		Button btnFullscreen = new Button
		{
			Text = "⛶ 全屏",
			Size = new Size(68, 28),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			Location = new Point(topBar.ClientSize.Width - 170, 6),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 45, 60),
			ForeColor = Color.FromArgb(226, 232, 240),
			Font = new Font("Microsoft YaHei UI", 8.5f),
			Cursor = Cursors.Hand
		};
		btnFullscreen.FlatAppearance.BorderSize = 0;
		btnFullscreen.Click += delegate { ToggleFullscreen(); };
		topBar.Controls.Add(btnFullscreen);

		Button btnClose = new Button
		{
			Text = "✕ 关闭窗口",
			Size = new Size(84, 28),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			Location = new Point(topBar.ClientSize.Width - 96, 6),
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
			btnClose.Location = new Point(topBar.ClientSize.Width - btnClose.Width - 12, 6);
			btnFullscreen.Location = new Point(btnClose.Left - btnFullscreen.Width - 8, 6);
			_titleInfoLabel.Location = new Point(Math.Min(320, Math.Max(12, topBar.ClientSize.Width - 520)), 11);
		};

		// 2. Bottom Control Panel (Dock Bottom, Height 86)
		Panel bottomPanel = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 86,
			BackColor = Color.FromArgb(18, 24, 34),
			Padding = new Padding(12, 4, 12, 4)
		};

		// Row 1: Scrubber TrackBar
		_scrubber = new TrackBar
		{
			Dock = DockStyle.Top,
			Height = 28,
			Minimum = 0,
			Maximum = 1000,
			Value = 0,
			TickStyle = TickStyle.None,
			Cursor = Cursors.Hand,
			BackColor = Color.FromArgb(18, 24, 34)
		};
		_scrubber.MouseDown += delegate { _isDraggingScrubber = true; };
		_scrubber.MouseUp += delegate
		{
			_isDraggingScrubber = false;
			double dur = _mainForm.CutEditDuration;
			if (dur > 0.0)
			{
				double sec = (_scrubber.Value / 1000.0) * dur;
				_mainForm.SeekCutEditVideo(sec);
			}
		};
		_scrubber.Scroll += delegate
		{
			double dur = _mainForm.CutEditDuration;
			if (dur > 0.0)
			{
				double sec = (_scrubber.Value / 1000.0) * dur;
				_mainForm.SeekCutEditVideo(sec);
			}
		};
		bottomPanel.Controls.Add(_scrubber);

		// Row 2: Transport & Subtitle Position controls
		Panel ctrlRow = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.Transparent,
			Padding = new Padding(0, 4, 0, 0)
		};

		_timeLabel = new Label
		{
			Text = "00:00.0 / 00:00.0",
			ForeColor = Color.FromArgb(56, 189, 248),
			Font = new Font("Consolas", 10.5f, FontStyle.Bold),
			AutoSize = true,
			Location = new Point(8, 12)
		};
		ctrlRow.Controls.Add(_timeLabel);

		FlowLayoutPanel btnFlow = new FlowLayoutPanel
		{
			Size = new Size(350, 36),
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			BackColor = Color.Transparent
		};

		Button btnStepBack5 = MakeBtn("⏪ -5s", 58, delegate { _mainForm.StepCutEditTime(-5.0); });
		btnFlow.Controls.Add(btnStepBack5);

		Button btnStepBack1 = MakeBtn("◀ -1s", 52, delegate { _mainForm.StepCutEditTime(-1.0); });
		btnStepBack1.Margin = new Padding(4, 0, 0, 0);
		btnFlow.Controls.Add(btnStepBack1);

		_btnPlayPause = new Button
		{
			Text = "▶ 播放",
			Size = new Size(84, 32),
			Margin = new Padding(6, 0, 0, 0),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 99, 235),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
			Cursor = Cursors.Hand
		};
		_btnPlayPause.FlatAppearance.BorderSize = 0;
		_btnPlayPause.Click += delegate { _mainForm.ToggleCutEditPlayPause(); };
		btnFlow.Controls.Add(_btnPlayPause);

		Button btnStepFwd1 = MakeBtn("+1s ▶", 52, delegate { _mainForm.StepCutEditTime(1.0); });
		btnStepFwd1.Margin = new Padding(6, 0, 0, 0);
		btnFlow.Controls.Add(btnStepFwd1);

		Button btnStepFwd5 = MakeBtn("+5s ⏩", 58, delegate { _mainForm.StepCutEditTime(5.0); });
		btnStepFwd5.Margin = new Padding(4, 0, 0, 0);
		btnFlow.Controls.Add(btnStepFwd5);

		ctrlRow.Controls.Add(btnFlow);

		// Subtitle position slider on right side
		Panel subPosPanel = new Panel
		{
			Dock = DockStyle.Right,
			Width = 330,
			Height = 36,
			BackColor = Color.Transparent
		};

		Label subPosLbl = new Label
		{
			Text = "字幕高度:",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(203, 213, 225),
			AutoSize = true,
			Location = new Point(4, 12)
		};
		subPosPanel.Controls.Add(subPosLbl);

		_subtitlePosSlider = new TrackBar
		{
			Location = new Point(74, 6),
			Width = 135,
			Height = 26,
			Minimum = 20,
			Maximum = 800,
			Value = Math.Max(20, Math.Min(800, _mainForm.CutEditSubtitleBottomOffset)),
			TickStyle = TickStyle.None,
			BackColor = Color.FromArgb(18, 24, 34),
			Cursor = Cursors.Hand
		};
		_subtitlePosValLabel = new Label
		{
			Text = $"底:{_subtitlePosSlider.Value}px",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(56, 189, 248),
			AutoSize = true,
			Location = new Point(216, 12)
		};
		_subtitlePosSlider.ValueChanged += delegate
		{
			_subtitlePosValLabel.Text = $"底:{_subtitlePosSlider.Value}px";
			_mainForm.SetCutEditSubtitleBottomOffset(_subtitlePosSlider.Value);
		};
		subPosPanel.Controls.Add(_subtitlePosSlider);
		subPosPanel.Controls.Add(_subtitlePosValLabel);
		ctrlRow.Controls.Add(subPosPanel);

		ctrlRow.Resize += delegate
		{
			int leftReserved = _timeLabel.Right + 12;
			int rightReserved = ctrlRow.ClientSize.Width - subPosPanel.Width;
			int availableWidth = Math.Max(0, rightReserved - leftReserved);
			int targetX = leftReserved + (availableWidth - btnFlow.Width) / 2;
			btnFlow.Location = new Point(Math.Max(leftReserved, targetX), 6);
		};

		bottomPanel.Controls.Add(ctrlRow);

		// 3. Central Canvas Panel (Dock Fill) & PictureBox
		_canvasPanel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(10, 14, 20),
			Padding = new Padding(0)
		};

		_previewBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.FromArgb(10, 14, 20),
			Cursor = Cursors.Hand
		};
		typeof(PictureBox).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(_previewBox, true);
		_previewBox.DoubleClick += delegate { ToggleFullscreen(); };
		_canvasPanel.Controls.Add(_previewBox);

		// WinForms docking layout: Center Fill FIRST, then Top and Bottom!
		Controls.Add(_canvasPanel);
		Controls.Add(topBar);
		Controls.Add(bottomPanel);

		KeyDown += (s, e) =>
		{
			if (e.KeyCode == Keys.Space)
			{
				_mainForm.ToggleCutEditPlayPause();
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Left)
			{
				_mainForm.StepCutEditTime(e.Shift ? -5.0 : -1.0);
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Right)
			{
				_mainForm.StepCutEditTime(e.Shift ? 5.0 : 1.0);
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Escape && _isFullscreen)
			{
				ToggleFullscreen();
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.F11)
			{
				ToggleFullscreen();
				e.Handled = true;
			}
		};

		FormClosing += delegate
		{
			_mainForm.OnCutEditPopoutClosing();
		};

		FormClosed += delegate
		{
			_mainForm.OnCutEditPopoutClosed();
		};
	}

	private Button MakeBtn(string text, int width, EventHandler onClick)
	{
		Button btn = new Button
		{
			Text = text,
			Size = new Size(width, 32),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 45, 60),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 8.5f),
			Cursor = Cursors.Hand
		};
		btn.FlatAppearance.BorderSize = 0;
		btn.Click += onClick;
		return btn;
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
		}
		else
		{
			FormBorderStyle = _previousBorderStyle;
			WindowState = _previousWindowState;
			_isFullscreen = false;
		}
	}

	public void UpdateTimeAndScrubber(double currentPos, double duration, bool isPlaying)
	{
		if (IsDisposed || Disposing) return;

		if (InvokeRequired)
		{
			try
			{
				BeginInvoke((MethodInvoker)delegate { UpdateTimeAndScrubber(currentPos, duration, isPlaying); });
			}
			catch { }
			return;
		}

		_timeLabel.Text = $"{FormatDuration(currentPos)} / {FormatDuration(duration)}";
		_btnPlayPause.Text = isPlaying ? "⏸ 暂停" : "▶ 播放";

		if (!_isDraggingScrubber && duration > 0.0)
		{
			int val = (int)Math.Max(0, Math.Min(1000, (currentPos / duration) * 1000.0));
			if (_scrubber.Value != val)
			{
				_scrubber.Value = val;
			}
		}
	}

	public void UpdateFrame(Image frame, double currentPos, double duration, bool isPlaying)
	{
		if (IsDisposed || Disposing) return;

		if (InvokeRequired)
		{
			try
			{
				BeginInvoke((MethodInvoker)delegate { UpdateFrame(frame, currentPos, duration, isPlaying); });
			}
			catch { }
			return;
		}

		_previewBox.Image = frame;
		_previewBox.Invalidate();

		UpdateTimeAndScrubber(currentPos, duration, isPlaying);
	}

	public void SyncSubtitleOffset(int offset)
	{
		if (IsDisposed || Disposing) return;
		if (_subtitlePosSlider != null && _subtitlePosSlider.Value != offset)
		{
			int clamped = Math.Max(20, Math.Min(800, offset));
			_subtitlePosSlider.Value = clamped;
			_subtitlePosValLabel.Text = $"底:{clamped}px";
		}
	}

	private static string FormatDuration(double sec)
	{
		if (double.IsNaN(sec) || sec < 0.0) sec = 0.0;
		TimeSpan ts = TimeSpan.FromSeconds(sec);
		if (ts.TotalHours >= 1.0)
		{
			return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 100:0}";
		}
		return $"{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 100:0}";
	}
}
