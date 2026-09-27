using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Controls;
using System.Windows.Media;

namespace VideoBatchMerger;

public sealed class VideoPreviewForm : Form
{
	private readonly string _videoPath;
	private readonly bool _deleteOnClose;
	private ElementHost _elementHost;
	private System.Windows.Controls.MediaElement _mediaElement;
	private readonly System.Windows.Forms.Timer _playTimer;

	private System.Windows.Forms.Button _btnPlayPause;
	private System.Windows.Forms.Button _btnReplay;
	private System.Windows.Forms.CheckBox _chkLoop;
	private System.Windows.Forms.TrackBar _trackTime;
	private System.Windows.Forms.Label _lblTime;
	private System.Windows.Forms.Button _btnMute;
	private System.Windows.Forms.TrackBar _trackVolume;
	private System.Windows.Forms.Button _btnOpenExternal;
	private System.Windows.Forms.Button _btnClose;

	private bool _isDraggingSeek;
	private bool _isPlaying;
	private bool _isMuted;
	private double _lastVolume = 1.0;
	private TimeSpan _totalDuration = TimeSpan.Zero;

	public VideoPreviewForm(string videoPath, string title = "视频播放预览", bool deleteOnClose = true)
	{
		_videoPath = videoPath;
		_deleteOnClose = deleteOnClose;

		InitializeUi(title);

		_playTimer = new System.Windows.Forms.Timer { Interval = 100 };
		_playTimer.Tick += PlayTimer_Tick;

		LoadVideo();
	}

	private void InitializeUi(string title)
	{
		Text = title;
		Width = 720;
		Height = 860;
		MinimumSize = new Size(520, 600);
		StartPosition = FormStartPosition.CenterParent;
		BackColor = System.Drawing.Color.FromArgb(20, 24, 30);
		ForeColor = System.Drawing.Color.White;
		KeyPreview = true;

		// Video surface (Center)
		_elementHost = new ElementHost
		{
			Dock = DockStyle.Fill,
			BackColor = System.Drawing.Color.FromArgb(12, 14, 18)
		};

		_mediaElement = new System.Windows.Controls.MediaElement
		{
			LoadedBehavior = MediaState.Manual,
			UnloadedBehavior = MediaState.Manual,
			Stretch = System.Windows.Media.Stretch.Uniform,
			ScrubbingEnabled = true
		};

		_mediaElement.MediaOpened += MediaElement_MediaOpened;
		_mediaElement.MediaEnded += MediaElement_MediaEnded;
		_mediaElement.MediaFailed += MediaElement_MediaFailed;

		// Click video to toggle play/pause
		_mediaElement.MouseDown += (s, e) =>
		{
			if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
			{
				TogglePlayPause();
			}
		};

		_elementHost.Child = _mediaElement;
		Controls.Add(_elementHost);

		// Bottom control bar (Dock Bottom)
		System.Windows.Forms.Panel bottomPanel = new System.Windows.Forms.Panel
		{
			Dock = DockStyle.Bottom,
			Height = 84,
			BackColor = System.Drawing.Color.FromArgb(28, 32, 40),
			Padding = new System.Windows.Forms.Padding(12, 6, 12, 8)
		};

		// Top row of bottom panel: Seek track bar
		System.Windows.Forms.Panel trackRow = new System.Windows.Forms.Panel
		{
			Dock = DockStyle.Top,
			Height = 32,
			BackColor = System.Drawing.Color.Transparent
		};

		_trackTime = new System.Windows.Forms.TrackBar
		{
			Dock = DockStyle.Fill,
			Minimum = 0,
			Maximum = 1000,
			Value = 0,
			TickStyle = TickStyle.None,
			Cursor = Cursors.Hand
		};
		_trackTime.MouseDown += (s, e) => _isDraggingSeek = true;
		_trackTime.MouseUp += (s, e) =>
		{
			_isDraggingSeek = false;
			if (_totalDuration.TotalSeconds > 0)
			{
				double targetSec = (_trackTime.Value / 1000.0) * _totalDuration.TotalSeconds;
				_mediaElement.Position = TimeSpan.FromSeconds(targetSec);
			}
		};
		_trackTime.Scroll += (s, e) =>
		{
			if (_isDraggingSeek && _totalDuration.TotalSeconds > 0)
			{
				double sec = (_trackTime.Value / 1000.0) * _totalDuration.TotalSeconds;
				_lblTime.Text = $"{FormatTime(TimeSpan.FromSeconds(sec))} / {FormatTime(_totalDuration)}";
			}
		};
		trackRow.Controls.Add(_trackTime);
		bottomPanel.Controls.Add(trackRow);

		// Bottom row: Buttons, volume, etc.
		System.Windows.Forms.Panel btnRow = new System.Windows.Forms.Panel
		{
			Dock = DockStyle.Bottom,
			Height = 38,
			BackColor = System.Drawing.Color.Transparent
		};

		_btnPlayPause = new System.Windows.Forms.Button
		{
			Text = "⏸ 暂停 (空格)",
			Size = new Size(110, 32),
			Location = new System.Drawing.Point(0, 3),
			FlatStyle = FlatStyle.Flat,
			BackColor = System.Drawing.Color.FromArgb(37, 99, 235),
			ForeColor = System.Drawing.Color.White,
			Cursor = Cursors.Hand,
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
		};
		_btnPlayPause.FlatAppearance.BorderSize = 0;
		_btnPlayPause.Click += (s, e) => TogglePlayPause();
		btnRow.Controls.Add(_btnPlayPause);

		_btnReplay = new System.Windows.Forms.Button
		{
			Text = "⏮ 重放",
			Size = new Size(72, 32),
			Location = new System.Drawing.Point(118, 3),
			FlatStyle = FlatStyle.Flat,
			BackColor = System.Drawing.Color.FromArgb(48, 54, 66),
			ForeColor = System.Drawing.Color.White,
			Cursor = Cursors.Hand
		};
		_btnReplay.FlatAppearance.BorderSize = 0;
		_btnReplay.Click += (s, e) =>
		{
			_mediaElement.Position = TimeSpan.Zero;
			_mediaElement.Play();
			_isPlaying = true;
			_btnPlayPause.Text = "⏸ 暂停 (空格)";
		};
		btnRow.Controls.Add(_btnReplay);

		_lblTime = new System.Windows.Forms.Label
		{
			Text = "00:00 / 00:00",
			Location = new System.Drawing.Point(200, 10),
			AutoSize = true,
			ForeColor = System.Drawing.Color.FromArgb(200, 210, 225),
			Font = new Font("Consolas", 10f, FontStyle.Regular)
		};
		btnRow.Controls.Add(_lblTime);

		_chkLoop = new System.Windows.Forms.CheckBox
		{
			Text = "🔁 循环播放",
			Checked = true,
			Location = new System.Drawing.Point(340, 9),
			AutoSize = true,
			ForeColor = System.Drawing.Color.FromArgb(210, 220, 235),
			Cursor = Cursors.Hand
		};
		btnRow.Controls.Add(_chkLoop);

		// Right-aligned controls
		_btnClose = new System.Windows.Forms.Button
		{
			Text = "关闭 (Esc)",
			Size = new Size(82, 32),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			Location = new System.Drawing.Point(bottomPanel.ClientSize.Width - 96, 3),
			FlatStyle = FlatStyle.Flat,
			BackColor = System.Drawing.Color.FromArgb(64, 70, 82),
			ForeColor = System.Drawing.Color.White,
			Cursor = Cursors.Hand
		};
		_btnClose.FlatAppearance.BorderSize = 0;
		_btnClose.Click += (s, e) => Close();
		btnRow.Controls.Add(_btnClose);

		_btnOpenExternal = new System.Windows.Forms.Button
		{
			Text = "系统播放器打开",
			Size = new Size(115, 32),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			Location = new System.Drawing.Point(bottomPanel.ClientSize.Width - 220, 3),
			FlatStyle = FlatStyle.Flat,
			BackColor = System.Drawing.Color.FromArgb(48, 54, 66),
			ForeColor = System.Drawing.Color.White,
			Cursor = Cursors.Hand
		};
		_btnOpenExternal.FlatAppearance.BorderSize = 0;
		_btnOpenExternal.Click += (s, e) =>
		{
			try
			{
				if (File.Exists(_videoPath))
				{
					Process.Start(new ProcessStartInfo(_videoPath) { UseShellExecute = true });
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "无法调用外部播放器: " + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		};
		btnRow.Controls.Add(_btnOpenExternal);

		_trackVolume = new System.Windows.Forms.TrackBar
		{
			Minimum = 0,
			Maximum = 100,
			Value = 100,
			TickStyle = TickStyle.None,
			Size = new Size(70, 32),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			Location = new System.Drawing.Point(bottomPanel.ClientSize.Width - 300, 4),
			Cursor = Cursors.Hand
		};
		_trackVolume.ValueChanged += (s, e) =>
		{
			double v = _trackVolume.Value / 100.0;
			_mediaElement.Volume = v;
			_lastVolume = Math.Max(0.1, v);
			_btnMute.Text = (v <= 0) ? "🔇" : "🔊";
			_isMuted = (v <= 0);
		};
		btnRow.Controls.Add(_trackVolume);

		_btnMute = new System.Windows.Forms.Button
		{
			Text = "🔊",
			Size = new Size(34, 32),
			Anchor = AnchorStyles.Top | AnchorStyles.Right,
			Location = new System.Drawing.Point(bottomPanel.ClientSize.Width - 338, 3),
			FlatStyle = FlatStyle.Flat,
			BackColor = System.Drawing.Color.FromArgb(48, 54, 66),
			ForeColor = System.Drawing.Color.White,
			Cursor = Cursors.Hand,
			Font = new Font("Segoe UI Emoji", 10f)
		};
		_btnMute.FlatAppearance.BorderSize = 0;
		_btnMute.Click += (s, e) =>
		{
			_isMuted = !_isMuted;
			if (_isMuted)
			{
				_mediaElement.Volume = 0;
				_trackVolume.Value = 0;
				_btnMute.Text = "🔇";
			}
			else
			{
				int val = (int)(_lastVolume * 100);
				_trackVolume.Value = val;
				_mediaElement.Volume = _lastVolume;
				_btnMute.Text = "🔊";
			}
		};
		btnRow.Controls.Add(_btnMute);

		bottomPanel.Controls.Add(btnRow);
		Controls.Add(bottomPanel);

		// Keydown handles
		KeyDown += (s, e) =>
		{
			if (e.KeyCode == Keys.Space)
			{
				TogglePlayPause();
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Escape)
			{
				Close();
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Left)
			{
				StepSeconds(-1);
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Right)
			{
				StepSeconds(1);
				e.Handled = true;
			}
		};

		FormClosing += (s, e) =>
		{
			_playTimer.Stop();
			try
			{
				_mediaElement.Stop();
				_mediaElement.Close();
			}
			catch
			{
			}

			if (_deleteOnClose && !string.IsNullOrEmpty(_videoPath) && File.Exists(_videoPath))
			{
				try
				{
					// Short delay to let OS release file handle if needed
					System.Threading.ThreadPool.QueueUserWorkItem(delegate
					{
						System.Threading.Thread.Sleep(500);
						try
						{
							if (File.Exists(_videoPath))
							{
								File.Delete(_videoPath);
							}
						}
						catch
						{
						}
					});
				}
				catch
				{
				}
			}
		};
	}

	private void LoadVideo()
	{
		if (string.IsNullOrEmpty(_videoPath) || !File.Exists(_videoPath))
		{
			_lblTime.Text = "文件未找到";
			return;
		}

		try
		{
			_mediaElement.Source = new Uri(Path.GetFullPath(_videoPath));
			_mediaElement.Play();
			_isPlaying = true;
			_btnPlayPause.Text = "⏸ 暂停 (空格)";
			_playTimer.Start();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "播放视频失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void MediaElement_MediaOpened(object sender, System.Windows.RoutedEventArgs e)
	{
		if (_mediaElement.NaturalDuration.HasTimeSpan)
		{
			_totalDuration = _mediaElement.NaturalDuration.TimeSpan;
			_lblTime.Text = $"00:00 / {FormatTime(_totalDuration)}";
		}
	}

	private void MediaElement_MediaEnded(object sender, System.Windows.RoutedEventArgs e)
	{
		if (_chkLoop.Checked)
		{
			_mediaElement.Position = TimeSpan.Zero;
			_mediaElement.Play();
			_isPlaying = true;
			_btnPlayPause.Text = "⏸ 暂停 (空格)";
		}
		else
		{
			_mediaElement.Pause();
			_isPlaying = false;
			_btnPlayPause.Text = "▶ 播放 (空格)";
		}
	}

	private void MediaElement_MediaFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
	{
		_playTimer.Stop();
		MessageBox.Show(this, "视频解码播放异常: " + e.ErrorException?.Message, "解码错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
	}

	private void PlayTimer_Tick(object sender, EventArgs e)
	{
		if (!_isDraggingSeek && _totalDuration.TotalSeconds > 0)
		{
			TimeSpan pos = _mediaElement.Position;
			double ratio = Math.Max(0.0, Math.Min(1.0, pos.TotalSeconds / _totalDuration.TotalSeconds));
			int targetVal = (int)(ratio * 1000);
			if (targetVal >= 0 && targetVal <= 1000)
			{
				_trackTime.Value = targetVal;
			}
			_lblTime.Text = $"{FormatTime(pos)} / {FormatTime(_totalDuration)}";
		}
	}

	private void TogglePlayPause()
	{
		if (_isPlaying)
		{
			_mediaElement.Pause();
			_isPlaying = false;
			_btnPlayPause.Text = "▶ 播放 (空格)";
		}
		else
		{
			_mediaElement.Play();
			_isPlaying = true;
			_btnPlayPause.Text = "⏸ 暂停 (空格)";
		}
	}

	private void StepSeconds(double seconds)
	{
		if (_totalDuration.TotalSeconds > 0)
		{
			double target = _mediaElement.Position.TotalSeconds + seconds;
			target = Math.Max(0.0, Math.Min(_totalDuration.TotalSeconds, target));
			_mediaElement.Position = TimeSpan.FromSeconds(target);
		}
	}

	private static string FormatTime(TimeSpan t)
	{
		if (t.TotalHours >= 1)
		{
			return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
		}
		return $"{t.Minutes:00}:{t.Seconds:00}";
	}
}
