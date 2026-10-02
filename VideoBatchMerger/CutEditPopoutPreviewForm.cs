using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoBatchMerger;

/// <summary>
/// 独立弹出式工作预览区窗口（参考专业视频剪辑软件工作区弹出设计）
/// 支持实时音画同步对齐、字幕定位预审、全屏/大屏多监视器审查
/// </summary>
internal sealed class CutEditPopoutPreviewForm : Form
{
	private readonly MainForm _mainForm;
	private readonly PictureBox _previewBox;
	private readonly Label _timeLabel;
	private readonly TrackBar _scrubber;
	private readonly Button _btnPlayPause;
	private readonly Label _titleInfoLabel;
	private readonly TrackBar _subtitlePosSlider;
	private readonly Label _subtitlePosValLabel;
	private bool _isDraggingScrubber;

	public CutEditPopoutPreviewForm(MainForm mainForm)
	{
		_mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));

		Text = "🖥️ 工作预览区 · 独立监视器 (Pop-out Workspace Studio)";
		Width = 920;
		Height = 720;
		MinimumSize = new Size(640, 480);
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = Color.FromArgb(15, 20, 28);
		ForeColor = Color.White;
		Icon = _mainForm.Icon;
		KeyPreview = true;

		// 1. Top Bar: Header & Info
		Panel topBar = new Panel
		{
			Dock = DockStyle.Top,
			Height = 38,
			BackColor = Color.FromArgb(20, 26, 38),
			Padding = new Padding(12, 6, 12, 6)
		};

		Label titleLbl = new Label
		{
			Text = "🖥️ 独立全屏/大屏监视器 · 实时音画与字幕对齐预审",
			Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(56, 189, 248),
			AutoSize = true,
			Location = new Point(12, 9)
		};
		topBar.Controls.Add(titleLbl);

		_titleInfoLabel = new Label
		{
			Text = "双击画面可全屏切换 | 支持空格键播放/暂停",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(148, 163, 184),
			AutoSize = true,
			Location = new Point(360, 11)
		};
		topBar.Controls.Add(_titleInfoLabel);

		Button btnClose = new Button
		{
			Text = "✕ 关闭窗口",
			Size = new Size(88, 26),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			Location = new Point(Width - 110, 6),
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
			_titleInfoLabel.Location = new Point(Math.Min(360, Math.Max(12, topBar.ClientSize.Width - 450)), 11);
		};
		Controls.Add(topBar);

		// 2. Bottom Control Panel
		Panel bottomPanel = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 88,
			BackColor = Color.FromArgb(20, 26, 38),
			Padding = new Padding(12, 4, 12, 6)
		};

		// Row 1: Scrubber
		_scrubber = new TrackBar
		{
			Dock = DockStyle.Top,
			Height = 26,
			Minimum = 0,
			Maximum = 1000,
			Value = 0,
			TickStyle = TickStyle.None,
			Cursor = Cursors.Hand
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
			Location = new Point(220, 6),
			Size = new Size(260, 36),
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};

		Button btnStepBack = new Button
		{
			Text = "◀-1s",
			Size = new Size(54, 30),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 45, 60),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 8.5f),
			Cursor = Cursors.Hand
		};
		btnStepBack.FlatAppearance.BorderSize = 0;
		btnStepBack.Click += delegate { _mainForm.StepCutEditTime(-1.0); };
		btnFlow.Controls.Add(btnStepBack);

		_btnPlayPause = new Button
		{
			Text = "▶ 播放",
			Size = new Size(80, 30),
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

		Button btnStepFwd = new Button
		{
			Text = "+1s ▶",
			Size = new Size(54, 30),
			Margin = new Padding(6, 0, 0, 0),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(37, 45, 60),
			ForeColor = Color.White,
			Font = new Font("Microsoft YaHei UI", 8.5f),
			Cursor = Cursors.Hand
		};
		btnStepFwd.FlatAppearance.BorderSize = 0;
		btnStepFwd.Click += delegate { _mainForm.StepCutEditTime(1.0); };
		btnFlow.Controls.Add(btnStepFwd);
		ctrlRow.Controls.Add(btnFlow);

		// Subtitle position slider on right side
		Panel subPosPanel = new Panel
		{
			Dock = DockStyle.Right,
			Width = 320,
			Height = 36,
			BackColor = Color.Transparent
		};

		Label subPosLbl = new Label
		{
			Text = "字幕上下位置: 低",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(203, 213, 225),
			AutoSize = true,
			Location = new Point(4, 12)
		};
		subPosPanel.Controls.Add(subPosLbl);

		_subtitlePosSlider = new TrackBar
		{
			Location = new Point(106, 6),
			Width = 120,
			Height = 26,
			Minimum = 20,
			Maximum = 800,
			Value = Math.Max(20, Math.Min(800, _mainForm.CutEditSubtitleBottomOffset)),
			TickStyle = TickStyle.None,
			BackColor = Color.FromArgb(20, 26, 38)
		};
		_subtitlePosValLabel = new Label
		{
			Text = $"距离底:{_subtitlePosSlider.Value}",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(56, 189, 248),
			AutoSize = true,
			Location = new Point(232, 12)
		};
		_subtitlePosSlider.ValueChanged += delegate
		{
			_subtitlePosValLabel.Text = $"距离底:{_subtitlePosSlider.Value}";
			_mainForm.SetCutEditSubtitleBottomOffset(_subtitlePosSlider.Value);
		};
		subPosPanel.Controls.Add(_subtitlePosSlider);
		subPosPanel.Controls.Add(_subtitlePosValLabel);
		ctrlRow.Controls.Add(subPosPanel);

		bottomPanel.Controls.Add(ctrlRow);
		Controls.Add(bottomPanel);

		// 3. Center PictureBox (Zoom mode for crisp video rendering)
		_previewBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.FromArgb(10, 14, 20),
			Cursor = Cursors.Hand
		};
		_previewBox.DoubleClick += delegate
		{
			if (WindowState == FormWindowState.Maximized)
			{
				WindowState = FormWindowState.Normal;
				FormBorderStyle = FormBorderStyle.Sizable;
			}
			else
			{
				FormBorderStyle = FormBorderStyle.Sizable;
				WindowState = FormWindowState.Maximized;
			}
		};
		Controls.Add(_previewBox);

		// WinForms docking order: Center Fill first, then Top and Bottom
		topBar.SendToBack();
		bottomPanel.SendToBack();

		KeyDown += (s, e) =>
		{
			if (e.KeyCode == Keys.Space)
			{
				_mainForm.ToggleCutEditPlayPause();
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Escape && WindowState == FormWindowState.Maximized)
			{
				WindowState = FormWindowState.Normal;
				e.Handled = true;
			}
		};

		FormClosed += delegate
		{
			_mainForm.OnCutEditPopoutClosed();
		};
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

		_timeLabel.Text = $"{FormatDuration(currentPos)} / {FormatDuration(duration)}";
		_btnPlayPause.Text = isPlaying ? "⏸ 暂停" : "▶ 播放";

		if (!_isDraggingScrubber && duration > 0.0)
		{
			int val = (int)Math.Max(0, Math.Min(1000, (currentPos / duration) * 1000.0));
			_scrubber.Value = val;
		}
	}

	public void SyncSubtitleOffset(int offset)
	{
		if (IsDisposed || Disposing) return;
		if (_subtitlePosSlider != null && _subtitlePosSlider.Value != offset)
		{
			int clamped = Math.Max(20, Math.Min(800, offset));
			_subtitlePosSlider.Value = clamped;
			_subtitlePosValLabel.Text = $"距离底:{clamped}";
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
