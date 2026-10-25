using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace VideoBatchMerger.ScriptBatch
{
	internal sealed class SubtitleStyleEditorDialog : Form
	{
		private readonly ScriptBatchSettings _settings;
		private readonly Action _onChange;
		private readonly FlowLayoutPanel _rows;

		public SubtitleStyleEditorDialog(ScriptBatchSettings settings, Action onChange)
		{
			_settings = settings;
			_onChange = onChange;
			Text = "字幕自由调节";
			Width = 520;
			Height = 710;
			MinimumSize = new Size(460, 540);
			StartPosition = FormStartPosition.CenterParent;
			BackColor = Color.FromArgb(17, 24, 39);
			ForeColor = Color.White;
			Font = new Font("Microsoft YaHei UI", 9f);
			AutoScaleMode = AutoScaleMode.Dpi;

			_rows = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				Padding = new Padding(14)
			};
			Controls.Add(_rows);
			AddNote("修改后立即更新手机预览；播放预览片段可核对实际视频和配音节奏。");

			var font = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 278, BackColor = Color.FromArgb(29, 42, 64), ForeColor = Color.White };
			foreach (string name in SubtitlePresetCatalog.GetInstalledFontNames().OrderBy(s => s))
				font.Items.Add(name);
			if (!font.Items.Contains(settings.SubtitleFontFamily)) font.Items.Add(settings.SubtitleFontFamily);
			font.SelectedItem = settings.SubtitleFontFamily;
			font.SelectedIndexChanged += (_, _) => { settings.SubtitleFontFamily = font.SelectedItem?.ToString() ?? "Arial"; Changed(); };
			AddRow("字体", font);
			AddNumber("字号 px", 24, 160, settings.SubtitleFontSize, 0, value => settings.SubtitleFontSize = (int)value);
			AddColor("文字颜色", settings.SubtitleColorHex, color => settings.SubtitleColorHex = color);
			AddNumber("文字不透明度 %", 20, 100, settings.SubtitleTextOpacity, 0, value => settings.SubtitleTextOpacity = (int)value);
			AddColor("描边颜色", settings.SubtitleStrokeColorHex, color => settings.SubtitleStrokeColorHex = color);
			AddNumber("描边粗细 px", 0, 12, (decimal)settings.SubtitleStrokeWidth, 1, value => settings.SubtitleStrokeWidth = (float)value);
			AddColor("阴影颜色", settings.SubtitleShadowColorHex, color => settings.SubtitleShadowColorHex = color);
			AddNumber("阴影距离 px", 0, 10, (decimal)settings.SubtitleShadowDepth, 1, value => settings.SubtitleShadowDepth = (float)value);
			AddNumber("柔光强度", 0, 15, settings.SubtitleGlowBlur, 0, value => settings.SubtitleGlowBlur = (int)value);
			AddColor("重点词颜色", settings.SubtitleHighlightTextColorHex, color => settings.SubtitleHighlightTextColorHex = color);
			AddCheck("逐词高亮底框", settings.SubtitleHighlightBoxEnabled, value => settings.SubtitleHighlightBoxEnabled = value);
			AddColor("高亮底框颜色", settings.SubtitleHighlightBoxColorHex, color => settings.SubtitleHighlightBoxColorHex = color);
			AddNumber("高亮底框不透明度 %", 10, 100, settings.SubtitleHighlightBoxOpacity, 0, value => settings.SubtitleHighlightBoxOpacity = (int)value);
			AddCheck("整句底板", settings.SubtitleBorderStyle == 3, value => settings.SubtitleBorderStyle = value ? 3 : 1);
			AddCheck("上滚字幕底板", settings.SubtitleScrollPanelEnabled, value => settings.SubtitleScrollPanelEnabled = value);
			AddColor("整句／上滚底板颜色", settings.SubtitleScrollPanelColorHex, color => settings.SubtitleScrollPanelColorHex = color);
			AddNumber("底板不透明度 %", 10, 95, settings.SubtitleScrollPanelOpacity, 0, value => settings.SubtitleScrollPanelOpacity = (int)value);
			var done = new Button { Text = "完成", Width = 445, Height = 38, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
			done.Click += (_, _) => Close();
			_rows.Controls.Add(done);
		}

		private void Changed()
		{
			_settings.Save();
			_onChange?.Invoke();
		}

		private void AddNote(string text)
		{
			_rows.Controls.Add(new Label { Text = text, Width = 445, Height = 45, ForeColor = Color.FromArgb(179, 207, 237) });
		}

		private void AddRow(string label, Control control)
		{
			var row = new Panel { Width = 450, Height = 39, Margin = new Padding(0, 0, 0, 3) };
			row.Controls.Add(new Label { Text = label, Width = 155, Height = 31, Top = 5, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft });
			control.Left = 162;
			control.Top = 4;
			row.Controls.Add(control);
			_rows.Controls.Add(row);
		}

		private void AddNumber(string label, decimal min, decimal max, decimal current, int places, Action<decimal> update)
		{
			var value = new NumericUpDown
			{
				Width = 115,
				Minimum = min,
				Maximum = max,
				DecimalPlaces = places,
				Increment = places == 0 ? 1 : 0.5m,
				Value = Math.Max(min, Math.Min(max, current)),
				BackColor = Color.FromArgb(29, 42, 64),
				ForeColor = Color.White
			};
			value.ValueChanged += (_, _) => { update(value.Value); Changed(); };
			AddRow(label, value);
		}

		private void AddCheck(string label, bool current, Action<bool> update)
		{
			var check = new CheckBox { Text = label, Checked = current, Width = 276, Height = 30, ForeColor = Color.White };
			check.CheckedChanged += (_, _) => { update(check.Checked); Changed(); };
			AddRow(string.Empty, check);
		}

		private void AddColor(string label, string current, Action<string> update)
		{
			var button = new Button
			{
				Text = current,
				Width = 124,
				Height = 28,
				BackColor = ScriptBatchSettings.ParseToColor(current, Color.White),
				ForeColor = Color.FromArgb(30, 40, 54),
				FlatStyle = FlatStyle.Flat
			};
			button.Click += (_, _) =>
			{
				using var picker = new ColorDialog { Color = button.BackColor, FullOpen = true };
				if (picker.ShowDialog(this) != DialogResult.OK) return;
				string hex = ScriptBatchSettings.ToHtmlHex(picker.Color);
				button.Text = hex;
				button.BackColor = picker.Color;
				update(hex);
				Changed();
			};
			AddRow(label, button);
		}
	}
}
