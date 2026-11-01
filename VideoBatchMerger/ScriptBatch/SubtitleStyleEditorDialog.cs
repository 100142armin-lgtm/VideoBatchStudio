using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VideoBatchMerger.ScriptBatch
{
	internal sealed class SubtitleStyleEditorDialog : Form
	{
		private sealed class LiveScrollPanel : FlowLayoutPanel
		{
			public LiveScrollPanel()
			{
				SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
			}

			protected override void OnHandleCreated(EventArgs e)
			{
				base.OnHandleCreated(e);
				SetScrollState(ScrollStateFullDrag, true);
			}

			protected override void WndProc(ref Message message)
			{
				bool scrollMessage = message.Msg == 0x0115 || message.Msg == 0x0114; // WM_VSCROLL / WM_HSCROLL
				bool thumbTracking = scrollMessage && (message.WParam.ToInt64() & 0xffff) == 5; // SB_THUMBTRACK
				// WinForms otherwise inherits Windows' "show window contents while dragging" preference.
				// Keep this editor live even when that system-wide preference is disabled or changes.
				if (scrollMessage) SetScrollState(ScrollStateFullDrag, true);
				base.WndProc(ref message);
				if (thumbTracking) Update();
			}
		}

		private sealed class EditorRow
		{
			public Panel Panel;
			public Label Caption;
			public Control Input;
		}

		private sealed class EditorSection
		{
			public Panel Panel;
			public Label Heading;
			public readonly List<EditorRow> Rows = new List<EditorRow>();
		}

		private sealed class ColorPickerButton : Button
		{
			public Color SelectedColor { get; set; }
			public string Hex => ScriptBatchSettings.ToHtmlHex(SelectedColor);

			public ColorPickerButton()
			{
				FlatStyle = FlatStyle.Flat;
				FlatAppearance.BorderColor = Color.FromArgb(110, 136, 169);
				BackColor = Color.FromArgb(29, 42, 64);
				ForeColor = Color.White;
				UseVisualStyleBackColor = false;
				AccessibleRole = AccessibleRole.PushButton;
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				base.OnPaint(e);
				int inset = Math.Max(6, Height / 6);
				int swatch = Math.Max(16, Height - inset * 2);
				var box = new Rectangle(inset, inset, swatch, swatch);
				using (var fill = new SolidBrush(SelectedColor)) e.Graphics.FillRectangle(fill, box);
				using (var border = new Pen(Color.FromArgb(190, 204, 223))) e.Graphics.DrawRectangle(border, box);
				var textBounds = new Rectangle(box.Right + inset, 0, Math.Max(0, Width - box.Right - inset * 2), Height);
				TextRenderer.DrawText(e.Graphics, Hex, Font, textBounds, ForeColor,
					TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
				if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3), ForeColor, BackColor);
			}
		}

		private readonly ScriptBatchSettings _settings;
		private readonly Action _onChange;
		private readonly Action _persist;
		private readonly FlowLayoutPanel _rows;
		private readonly Panel _header, _footer;
		private readonly Label _note;
		private readonly FlowLayoutPanel _toolbar;
		private readonly ComboBox _uiScale;
		private readonly CheckBox _autoScale;
		private readonly Button _done;
		private readonly List<EditorSection> _sections = new List<EditorSection>();
		private readonly System.Windows.Forms.Timer _resizeTimer;
		private Font _displayFont, _headingFont;
		private bool _layingOut;

		public SubtitleStyleEditorDialog(ScriptBatchSettings settings, Action onChange, Action persistSettings = null)
		{
			_settings = settings;
			_onChange = onChange;
			_persist = persistSettings ?? (() => _settings.Save());
			Text = "字幕自由调节 · 大字易读";
			StartPosition = FormStartPosition.CenterParent;
			BackColor = Color.FromArgb(17, 24, 39);
			ForeColor = Color.FromArgb(245, 248, 255);
			AutoScaleMode = AutoScaleMode.Dpi;
			Font = new Font("Microsoft YaHei UI", 11f);
			var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
			Size = new Size(Math.Min(800, work.Width - 32), Math.Min(850, work.Height - 48));
			MinimumSize = new Size(640, Math.Min(560, work.Height - 48));

			_rows = new LiveScrollPanel { Dock = DockStyle.Fill, AutoScroll = true,
				FlowDirection = FlowDirection.TopDown, WrapContents = false, TabStop = false };
			_footer = new Panel { Dock = DockStyle.Bottom, BackColor = Color.FromArgb(23, 35, 56) };
			_header = new Panel { Dock = DockStyle.Top, BackColor = Color.FromArgb(23, 35, 56) };
			Controls.Add(_rows); Controls.Add(_footer); Controls.Add(_header);
			_toolbar = new FlowLayoutPanel { WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
				AutoSize = false, TabStop = false };
			_header.Controls.Add(_toolbar);
			_toolbar.Controls.Add(new Label { Text = "界面字号", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
			_uiScale = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(29, 42, 64),
				ForeColor = Color.White, AccessibleName = "设置窗口显示比例，不影响视频字幕字号", DropDownHeight = 320 };
			_uiScale.Items.AddRange(new object[] { "100% · 标准", "125% · 大字", "150% · 更大", "175% · 特大", "200% · 超大" });
			int[] scales = { 100, 125, 150, 175, 200 };
			int restoredScale = Math.Max(100, Math.Min(200, settings.SubtitleEditorUiScalePercent));
			_uiScale.SelectedIndex = Array.IndexOf(scales, restoredScale);
			if (_uiScale.SelectedIndex < 0) _uiScale.SelectedIndex = 1;
			_toolbar.Controls.Add(_uiScale);
			_autoScale = new CheckBox { Text = "随窗口放大", AutoSize = true, Checked = settings.SubtitleEditorAutoScale,
				AccessibleName = "最大化或放大窗口时，自动增大设置文字" };
			_toolbar.Controls.Add(_autoScale);
			_note = new Label { Text = "界面放大不改变成片字幕；下方参数修改会同步预览。",
				ForeColor = Color.FromArgb(209, 226, 246), AutoSize = false };
			_header.Controls.Add(_note);
			_done = new Button { Text = "完成并返回", BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
			_done.Click += (_, _) => Close();
			_footer.Controls.Add(_done);
			AcceptButton = _done; CancelButton = _done;

			var text = AddSection("字体与文字");
			var font = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList,
				BackColor = Color.FromArgb(29, 42, 64), ForeColor = Color.White, DropDownHeight = 420 };
			foreach (string name in SubtitlePresetCatalog.GetInstalledFontNames().OrderBy(s => s)) font.Items.Add(name);
			if (!font.Items.Contains(settings.SubtitleFontFamily)) font.Items.Add(settings.SubtitleFontFamily);
			font.SelectedItem = settings.SubtitleFontFamily;
			font.SelectedIndexChanged += (_, _) => { settings.SubtitleFontFamily = font.SelectedItem?.ToString() ?? "Arial"; Changed(); };
			AddRow(text, "字体", font);
			AddNumber(text, "字幕字号 px", 24, 160, settings.SubtitleFontSize, 0, value => settings.SubtitleFontSize = (int)value);
			AddCheck(text, "文字加粗", settings.SubtitleBold, value => settings.SubtitleBold = value);
			AddCheck(text, "字幕全部大写（仅改变显示，不改变配音）", settings.SubtitleUppercase, value => settings.SubtitleUppercase = value);
			AddColor(text, "文字颜色", settings.SubtitleColorHex, color => settings.SubtitleColorHex = color);
			AddNumber(text, "文字不透明度 %", 20, 100, settings.SubtitleTextOpacity, 0, value => settings.SubtitleTextOpacity = (int)value);

			var outline = AddSection("描边与阴影");
			AddColor(outline, "描边颜色", settings.SubtitleStrokeColorHex, color => settings.SubtitleStrokeColorHex = color);
			AddNumber(outline, "描边粗细 px", 0, 40, (decimal)settings.SubtitleStrokeWidth, 1, value => settings.SubtitleStrokeWidth = (float)value);
			AddColor(outline, "阴影颜色", settings.SubtitleShadowColorHex, color => settings.SubtitleShadowColorHex = color);
			AddNumber(outline, "阴影距离 px", 0, 10, (decimal)settings.SubtitleShadowDepth, 1, value => settings.SubtitleShadowDepth = (float)value);
			AddNumber(outline, "柔光强度", 0, 15, settings.SubtitleGlowBlur, 0, value => settings.SubtitleGlowBlur = (int)value);

			var highlight = AddSection("重点词与背景底板");
			AddColor(highlight, "重点词颜色", settings.SubtitleHighlightTextColorHex, color => settings.SubtitleHighlightTextColorHex = color);
			AddCheck(highlight, "逐词高亮底框", settings.SubtitleHighlightBoxEnabled, value => settings.SubtitleHighlightBoxEnabled = value);
			AddColor(highlight, "高亮底框颜色", settings.SubtitleHighlightBoxColorHex, color => settings.SubtitleHighlightBoxColorHex = color);
			AddNumber(highlight, "高亮底框不透明度 %", 10, 100, settings.SubtitleHighlightBoxOpacity, 0, value => settings.SubtitleHighlightBoxOpacity = (int)value);
			AddCheck(highlight, "整句底板", settings.SubtitleBorderStyle == 3, value => settings.SubtitleBorderStyle = value ? 3 : 1);
			AddCheck(highlight, "上滚字幕底板", settings.SubtitleScrollPanelEnabled, value => settings.SubtitleScrollPanelEnabled = value);
			AddColor(highlight, "整句 / 上滚底板颜色", settings.SubtitleScrollPanelColorHex, color => settings.SubtitleScrollPanelColorHex = color);
			AddNumber(highlight, "底板不透明度 %", 10, 95, settings.SubtitleScrollPanelOpacity, 0, value => settings.SubtitleScrollPanelOpacity = (int)value);

			_uiScale.SelectedIndexChanged += (_, _) => { _settings.SubtitleEditorUiScalePercent = scales[_uiScale.SelectedIndex]; ApplyReadableLayout(); _persist(); };
			_autoScale.CheckedChanged += (_, _) => { _settings.SubtitleEditorAutoScale = _autoScale.Checked; ApplyReadableLayout(); _persist(); };
			_resizeTimer = new System.Windows.Forms.Timer { Interval = 80 };
			_resizeTimer.Tick += (_, _) => { _resizeTimer.Stop(); ApplyReadableLayout(); };
			Resize += (_, _) => { if (!_layingOut && _resizeTimer != null) { _resizeTimer.Stop(); _resizeTimer.Start(); } };
			Shown += (_, _) => ApplyReadableLayout();
			ApplyReadableLayout();
		}

		private EditorSection AddSection(string title)
		{
			var section = new EditorSection {
				Panel = new Panel { BackColor = Color.FromArgb(23, 35, 56), Margin = new Padding(0, 0, 0, 14), TabStop = false },
				Heading = new Label { Text = title, ForeColor = Color.FromArgb(125, 211, 252), AutoSize = false }
			};
			section.Panel.Controls.Add(section.Heading); _sections.Add(section); _rows.Controls.Add(section.Panel);
			return section;
		}

		private void AddRow(EditorSection section, string label, Control control)
		{
			var row = new EditorRow { Panel = new Panel { Margin = Padding.Empty, TabStop = false }, Input = control };
			if (!string.IsNullOrEmpty(label))
			{
				row.Caption = new Label { Text = label, ForeColor = ForeColor, TextAlign = ContentAlignment.MiddleLeft,
					AutoSize = false, UseMnemonic = false };
				row.Panel.Controls.Add(row.Caption);
			}
			control.AccessibleName = control.AccessibleName ?? label;
			row.Panel.Controls.Add(control); section.Panel.Controls.Add(row.Panel); section.Rows.Add(row);
		}

		private void Changed() { _persist(); _onChange?.Invoke(); }

		private void AddNumber(EditorSection section, string label, decimal min, decimal max, decimal current, int places, Action<decimal> update)
		{
			var value = new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = places,
				Increment = places == 0 ? 1 : 0.5m, Value = Math.Max(min, Math.Min(max, current)),
				BackColor = Color.FromArgb(29, 42, 64), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
			value.ValueChanged += (_, _) => { update(value.Value); Changed(); };
			AddRow(section, label, value);
		}

		private void AddCheck(EditorSection section, string label, bool current, Action<bool> update)
		{
			var check = new CheckBox { Text = label, Checked = current, ForeColor = ForeColor, AutoSize = false };
			check.CheckedChanged += (_, _) => { update(check.Checked); Changed(); };
			AddRow(section, null, check);
		}

		private void AddColor(EditorSection section, string label, string current, Action<string> update)
		{
			var button = new ColorPickerButton { SelectedColor = ScriptBatchSettings.ParseToColor(current, Color.White),
				AccessibleName = label + "，点击选择颜色" };
			button.Click += (_, _) =>
			{
				using (var picker = new ColorDialog { Color = button.SelectedColor, FullOpen = true })
				{
					if (picker.ShowDialog(this) != DialogResult.OK) return;
					button.SelectedColor = picker.Color; button.Invalidate(); update(button.Hex); Changed();
				}
			};
			AddRow(section, label, button);
		}

		private void ApplyReadableLayout()
		{
			if (_layingOut || ClientSize.Width <= 0 || _uiScale == null || _rows == null) return;
			_layingOut = true;
			Point previousScroll = _rows.AutoScrollPosition;
			SuspendLayout(); _rows.SuspendLayout();
			try
			{
				float dpi = Math.Max(1f, DeviceDpi / 96f);
				float[] scales = { 1f, 1.25f, 1.5f, 1.75f, 2f };
				float scale = scales[Math.Max(0, _uiScale.SelectedIndex)];
				float windowScale = _autoScale.Checked
					? (float)Math.Max(1, Math.Min(1.4, Math.Sqrt((ClientSize.Width / (760f * dpi)) * (ClientSize.Height / (760f * dpi)))))
					: 1f;
				float pointSize = 11f * scale * windowScale;
				if (_displayFont == null || Math.Abs(_displayFont.SizeInPoints - pointSize) > 0.15f)
				{
					var previousFont = _displayFont; var previousHeading = _headingFont;
					_displayFont = new Font("Microsoft YaHei UI", pointSize, FontStyle.Regular, GraphicsUnit.Point);
					_headingFont = new Font("Microsoft YaHei UI", pointSize + 0.75f, FontStyle.Bold, GraphicsUnit.Point);
					Font = _displayFont;
					foreach (var section in _sections) section.Heading.Font = _headingFont;
					previousFont?.Dispose(); previousHeading?.Dispose();
				}
				int pad = (int)Math.Round(14 * dpi);
				int inputHeight = Math.Max((int)(34 * dpi), Font.Height + (int)(12 * dpi));
				int gap = (int)(8 * dpi);
				_toolbar.SetBounds(pad, pad, Math.Max(1, ClientSize.Width - pad * 2), inputHeight * 2);
				foreach (Control control in _toolbar.Controls) control.Margin = new Padding(0, gap / 2, gap, 0);
				_uiScale.Width = Math.Max((int)(155 * dpi), TextRenderer.MeasureText("200% · 超大", Font).Width + (int)(36 * dpi));
				_uiScale.DropDownWidth = Math.Max(_uiScale.Width, (int)(220 * dpi));
				_toolbar.Height = _toolbar.GetPreferredSize(new Size(_toolbar.Width, 0)).Height + gap;
				_note.SetBounds(pad, _toolbar.Bottom + gap, _toolbar.Width,
					TextRenderer.MeasureText(_note.Text, Font, new Size(_toolbar.Width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + gap);
				_header.Height = _note.Bottom + pad;
				_done.SetBounds(Math.Max(pad, ClientSize.Width - (int)(210 * dpi) - pad), pad / 2, (int)(210 * dpi), inputHeight + gap);
				_footer.Height = _done.Bottom + pad / 2;
				_rows.Padding = new Padding(pad);
				int sectionWidth = Math.Max(200, _rows.ClientSize.Width - pad * 2 - SystemInformation.VerticalScrollBarWidth);
				foreach (var section in _sections)
				{
					int innerWidth = Math.Max(160, sectionWidth - pad * 2);
					int headingHeight = TextRenderer.MeasureText(section.Heading.Text, _headingFont).Height + gap;
					section.Heading.SetBounds(pad, pad, innerWidth, headingHeight);
					int y = section.Heading.Bottom + gap;
					foreach (var row in section.Rows)
					{
						int captionWidth = Math.Min((int)(innerWidth * 0.44), (int)(300 * dpi));
						int fieldWidth = row.Caption == null ? innerWidth : innerWidth - captionWidth - gap;
						int labelHeight = row.Caption == null ? 0 : TextRenderer.MeasureText(row.Caption.Text, Font,
							new Size(captionWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + gap;
						int height = row.Input is CheckBox
							? Math.Max(inputHeight, TextRenderer.MeasureText(row.Input.Text, Font, new Size(fieldWidth - 28, 0),
								TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + gap * 2)
							: Math.Max(inputHeight, labelHeight);
						row.Panel.SetBounds(pad, y, innerWidth, height + gap);
						row.Caption?.SetBounds(0, 0, captionWidth, height);
						int inputWidth = row.Input is NumericUpDown ? Math.Min(fieldWidth, (int)(180 * dpi * scale)) : fieldWidth;
						row.Input.SetBounds(row.Caption == null ? 0 : captionWidth + gap, 0, inputWidth, height);
						if (row.Input is ComboBox combo) combo.DropDownWidth = Math.Max(combo.Width, (int)(420 * dpi));
						y += height + gap * 2;
					}
					section.Panel.Size = new Size(sectionWidth, y + pad);
				}
			}
			finally
			{
				_rows.ResumeLayout(true); ResumeLayout(true); _layingOut = false;
				_rows.AutoScrollPosition = new Point(0, Math.Max(0, -previousScroll.Y));
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing) { _resizeTimer?.Dispose(); }
			base.Dispose(disposing);
			if (disposing) { _displayFont?.Dispose(); _headingFont?.Dispose(); }
		}
	}
}
