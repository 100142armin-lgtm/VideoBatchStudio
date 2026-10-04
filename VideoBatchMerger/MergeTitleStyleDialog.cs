using System;
using System.Drawing;
using System.Windows.Forms;

namespace VideoBatchMerger
{
	public class MergeTitleStyleDialog : Form
	{
		private readonly MergeTitlePlan _plan;
		private readonly bool _isDarkMode;
		private TextBox _subTitleBox;
		private ComboBox _positionCombo;
		private NumericUpDown _fontSizeNum;
		private NumericUpDown _offsetYNum;
		private PictureBox _previewBox;
		private string _sampleMainText;

		public MergeTitleStyleDialog(MergeTitlePlan plan, string sampleMainText, bool isDarkMode)
		{
			_plan = plan ?? new MergeTitlePlan();
			_sampleMainText = string.IsNullOrWhiteSpace(sampleMainText) ? "🔥 热门短视频标题示例" : sampleMainText;
			_isDarkMode = isDarkMode;

			InitializeUi();
			LoadFromPlan();
			UpdatePreview();
		}

		private void InitializeUi()
		{
			this.Text = "🎨 视频标题样式与位置高级微调";
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.StartPosition = FormStartPosition.CenterParent;
			this.ClientSize = new Size(540, 480);
			this.BackColor = _isDarkMode ? Color.FromArgb(20, 24, 33) : Color.FromArgb(248, 249, 250);
			this.ForeColor = _isDarkMode ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
			this.Font = new Font("Microsoft YaHei UI", 9f);

			Label titleLabel = new Label
			{
				Text = "视频标题微调（排版位置、副标题与字号）",
				Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold),
				Location = new Point(20, 16),
				AutoSize = true,
				ForeColor = _isDarkMode ? Color.FromArgb(125, 211, 252) : Color.FromArgb(2, 132, 199)
			};
			this.Controls.Add(titleLabel);

			// Preview Box
			_previewBox = new PictureBox
			{
				Location = new Point(20, 50),
				Size = new Size(500, 160),
				BackColor = _isDarkMode ? Color.FromArgb(10, 14, 20) : Color.FromArgb(30, 41, 59),
				SizeMode = PictureBoxSizeMode.Zoom,
				BorderStyle = BorderStyle.FixedSingle
			};
			this.Controls.Add(_previewBox);

			int y = 226;
			Label subLabel = new Label { Text = "副标题 / 作者标语 (可选):", Location = new Point(20, y), AutoSize = true };
			this.Controls.Add(subLabel);

			_subTitleBox = new TextBox
			{
				Location = new Point(190, y - 3),
				Width = 330,
				BackColor = _isDarkMode ? Color.FromArgb(30, 36, 48) : Color.White,
				ForeColor = this.ForeColor
			};
			_subTitleBox.TextChanged += (s, e) => UpdatePreview();
			this.Controls.Add(_subTitleBox);

			y += 38;
			Label posLabel = new Label { Text = "画面垂直位置:", Location = new Point(20, y), AutoSize = true };
			this.Controls.Add(posLabel);

			_positionCombo = new ComboBox
			{
				Location = new Point(190, y - 3),
				Width = 330,
				DropDownStyle = ComboBoxStyle.DropDownList,
				BackColor = _isDarkMode ? Color.FromArgb(30, 36, 48) : Color.White,
				ForeColor = this.ForeColor
			};
			_positionCombo.Items.AddRange(new object[]
			{
				"居中偏上 (短视频黄金视觉区，推荐)",
				"居中 (画面正中央大字)",
				"居中偏下 (适合无底部字幕时)",
				"顶部 (视频顶端通栏)",
				"底部 (下三分之一解说栏)"
			});
			_positionCombo.SelectedIndexChanged += (s, e) => UpdatePreview();
			this.Controls.Add(_positionCombo);

			y += 38;
			Label fontScaleLabel = new Label { Text = "字号大小缩放 (%):", Location = new Point(20, y), AutoSize = true };
			this.Controls.Add(fontScaleLabel);

			_fontSizeNum = new NumericUpDown
			{
				Location = new Point(190, y - 3),
				Width = 90,
				Minimum = 60m,
				Maximum = 180m,
				Value = 100m,
				Increment = 5m,
				BackColor = _isDarkMode ? Color.FromArgb(30, 36, 48) : Color.White,
				ForeColor = this.ForeColor,
				TextAlign = HorizontalAlignment.Center
			};
			_fontSizeNum.ValueChanged += (s, e) => UpdatePreview();
			this.Controls.Add(_fontSizeNum);

			Label tipScale = new Label { Text = "% (默认 100% 最佳自动适配)", Location = new Point(290, y), AutoSize = true, ForeColor = Color.FromArgb(120, 130, 145) };
			this.Controls.Add(tipScale);

			y += 38;
			Label offsetLabel = new Label { Text = "垂直位置微调 (Y轴偏移):", Location = new Point(20, y), AutoSize = true };
			this.Controls.Add(offsetLabel);

			_offsetYNum = new NumericUpDown
			{
				Location = new Point(190, y - 3),
				Width = 90,
				Minimum = -300m,
				Maximum = 300m,
				Value = 0m,
				Increment = 10m,
				BackColor = _isDarkMode ? Color.FromArgb(30, 36, 48) : Color.White,
				ForeColor = this.ForeColor,
				TextAlign = HorizontalAlignment.Center
			};
			_offsetYNum.ValueChanged += (s, e) => UpdatePreview();
			this.Controls.Add(_offsetYNum);

			Label tipOffset = new Label { Text = "像素 (负数向上移动，正数向下移动)", Location = new Point(290, y), AutoSize = true, ForeColor = Color.FromArgb(120, 130, 145) };
			this.Controls.Add(tipOffset);

			// OK / Cancel buttons
			Button okBtn = new Button
			{
				Text = "确定保存",
				DialogResult = DialogResult.OK,
				Location = new Point(310, 420),
				Size = new Size(100, 36),
				BackColor = Color.FromArgb(14, 165, 233),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat
			};
			okBtn.FlatAppearance.BorderSize = 0;
			okBtn.Click += OkBtn_Click;
			this.Controls.Add(okBtn);

			Button cancelBtn = new Button
			{
				Text = "取消",
				DialogResult = DialogResult.Cancel,
				Location = new Point(420, 420),
				Size = new Size(100, 36),
				BackColor = _isDarkMode ? Color.FromArgb(40, 48, 64) : Color.FromArgb(226, 232, 240),
				ForeColor = this.ForeColor,
				FlatStyle = FlatStyle.Flat
			};
			cancelBtn.FlatAppearance.BorderSize = 0;
			this.Controls.Add(cancelBtn);

			this.AcceptButton = okBtn;
			this.CancelButton = cancelBtn;
		}

		private void LoadFromPlan()
		{
			_subTitleBox.Text = _plan.SubTitleTemplate ?? "";
			string pos = _plan.Position ?? "居中偏上";
			if (pos.Contains("偏上")) _positionCombo.SelectedIndex = 0;
			else if (pos.Contains("中央") || (pos.Contains("居中") && !pos.Contains("偏"))) _positionCombo.SelectedIndex = 1;
			else if (pos.Contains("偏下")) _positionCombo.SelectedIndex = 2;
			else if (pos.Contains("顶部")) _positionCombo.SelectedIndex = 3;
			else if (pos.Contains("底部")) _positionCombo.SelectedIndex = 4;
			else _positionCombo.SelectedIndex = 0;

			int scalePercent = (int)Math.Round(_plan.FontSizeScale * 100);
			if (scalePercent < 60) scalePercent = 60;
			if (scalePercent > 180) scalePercent = 180;
			_fontSizeNum.Value = scalePercent;

			int offY = _plan.CustomOffsetY;
			if (offY < -300) offY = -300;
			if (offY > 300) offY = 300;
			_offsetYNum.Value = offY;
		}

		private void OkBtn_Click(object sender, EventArgs e)
		{
			_plan.SubTitleTemplate = _subTitleBox.Text.Trim();
			_plan.Position = _positionCombo.SelectedIndex switch
			{
				0 => "居中偏上",
				1 => "居中",
				2 => "居中偏下",
				3 => "顶部",
				4 => "底部",
				_ => "居中偏上"
			};
			_plan.FontSizeScale = (float)_fontSizeNum.Value / 100f;
			_plan.CustomOffsetY = (int)_offsetYNum.Value;
		}

		private void UpdatePreview()
		{
			try
			{
				var tempPlan = _plan.Clone();
				tempPlan.SubTitleTemplate = _subTitleBox.Text.Trim();
				tempPlan.Position = _positionCombo.SelectedIndex switch
				{
					0 => "居中偏上",
					1 => "居中",
					2 => "居中偏下",
					3 => "顶部",
					4 => "底部",
					_ => "居中偏上"
				};
				tempPlan.FontSizeScale = (float)_fontSizeNum.Value / 100f;
				tempPlan.CustomOffsetY = (int)_offsetYNum.Value;

				Bitmap bmp = MergeTitleStyleCatalog.RenderTitleBitmap(tempPlan, 1080, 1920, _sampleMainText, tempPlan.SubTitleTemplate);
				var old = _previewBox.Image;
				_previewBox.Image = bmp;
				old?.Dispose();
			}
			catch { }
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			base.OnFormClosed(e);
			_previewBox.Image?.Dispose();
		}
	}
}
