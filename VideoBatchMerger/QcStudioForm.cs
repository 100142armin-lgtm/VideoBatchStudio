using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoBatchMerger.Transcriber;

namespace VideoBatchMerger;

internal sealed class QcStudioForm : Form
{
	private readonly MainForm _mainForm;
	private readonly string _srcPath;
	private readonly string _whisperLang;
	private QcInput _qcInput;
	private QcReport _qcReport;

	// Top summary cards
	private Label _lblVerdictTitle;
	private Label _lblVerdictScore;
	private Label _lblVerdictDesc;

	private Label _lblRecallScore;
	private Label _lblRecallDesc;

	private Label _lblCutStats;
	private Label _lblCutDesc;

	private Label _lblRiskStats;
	private Label _lblRiskDesc;

	// Left: Script Diff
	private TextBox _txtScriptInput;
	private Button _btnReAnalyze;
	private RichTextBox _rtbDiffView;

	private enum CutFilterMode
	{
		All,
		RiskOnly,
		RetakeOnly,
		FillerOnly,
		SilenceOnly,
		SafeOnly
	}

	private CutFilterMode _currentFilterMode = CutFilterMode.All;
	private int _scriptIssueCursor = -1;
	private Label _lblRightTitle;

	// Right: Cut Audit Table
	private ListView _lvCutAudit;
	private RadioButton _rbFilterAll;
	private RadioButton _rbFilterReview;
	private RadioButton _rbFilterSafe;

	// Detail preview bar for selected cut
	private Label _lblSelectedCutTitle;
	private Label _lblSelectedCutDesc;
	private Button _btnPlayCut;
	private Button _btnRestoreCut;

	private QcCutItem _selectedCutItem;
	private readonly Color _bgColor = Color.FromArgb(16, 17, 26);
	private readonly Color _cardBg = Color.FromArgb(23, 25, 38);
	private readonly Color _borderColor = Color.FromArgb(42, 45, 64);
	private readonly Color _textColor = Color.FromArgb(241, 245, 249);
	private readonly Color _mutedColor = Color.FromArgb(148, 163, 184);

	public QcStudioForm(MainForm mainForm, QcInput input, string sourcePath, string whisperLang = "auto")
	{
		_mainForm = mainForm;
		_qcInput = input;
		_srcPath = sourcePath;
		_whisperLang = string.IsNullOrWhiteSpace(whisperLang) ? "auto" : whisperLang;

		Text = "🧐 AI 智能视频质检与脚本对齐工作台 (Script Alignment & Quality Control Studio)";
		Size = new Size(1420, 900);
		MinimumSize = new Size(1150, 720);
		StartPosition = FormStartPosition.CenterParent;
		BackColor = _bgColor;
		ForeColor = _textColor;
		Font = new Font("Microsoft YaHei UI", 9f);
		KeyPreview = true;

		InitializeUi();
		RunAnalysis();
	}

	private void InitializeUi()
	{
		// Top Header
		Panel headerPanel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 72,
			BackColor = _cardBg,
			Padding = new Padding(20, 10, 20, 10)
		};
		headerPanel.Paint += (s, e) =>
		{
			using Pen p = new Pen(_borderColor);
			e.Graphics.DrawLine(p, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
		};

		Label titleLabel = new Label
		{
			Text = "🧐 AI 智能视频质检与脚本对齐工作台 (Script QC & Alignment Studio)",
			Font = new Font("Microsoft YaHei UI", 12.5f, FontStyle.Bold),
			ForeColor = Color.White,
			AutoSize = true,
			Location = new Point(18, 12)
		};
		headerPanel.Controls.Add(titleLabel);

		string langBadge = _whisperLang.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "🇦🇴 非洲葡萄牙语 / 通用葡语" : $"🌐 语种: {_whisperLang}";
		Label subLabel = new Label
		{
			Text = $"出海多语种视频工业化品控 · 黄金脚本对照 · 误剪高危预警 · 零外语门槛一键溯源与恢复 | 素材: {Path.GetFileName(_srcPath)} | {langBadge}",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = _mutedColor,
			AutoSize = true,
			Location = new Point(20, 42)
		};
		headerPanel.Controls.Add(subLabel);
		Controls.Add(headerPanel);

		// KPI Cards Container
		Panel kpiContainer = new Panel
		{
			Dock = DockStyle.Top,
			Height = 100,
			Padding = new Padding(16, 8, 16, 8),
			BackColor = _bgColor
		};

		TableLayoutPanel kpiGrid = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 4,
			RowCount = 1
		};
		kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
		kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
		kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
		kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));

		// Card 1: 综合结论 (支持一键点击重置查看全部)
		Panel c1 = MakeInteractiveCardPanel(Color.FromArgb(16, 185, 129), "👆 重置全部", ResetAllFilters);
		_lblVerdictTitle = MakeCardTitle("综合品控结论 (QC Status)");
		_lblVerdictScore = new Label { Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold), ForeColor = Color.FromArgb(16, 185, 129), AutoSize = true, Location = new Point(14, 26), Text = "🟢 计算中..." };
		_lblVerdictDesc = new Label { Font = new Font("Microsoft YaHei UI", 8f), ForeColor = Color.FromArgb(110, 231, 183), AutoSize = true, Location = new Point(16, 58), Text = "正在对齐与评估..." };
		c1.Controls.AddRange(new Control[] { _lblVerdictTitle, _lblVerdictScore, _lblVerdictDesc });
		kpiGrid.Controls.Add(c1, 0, 0);

		// Card 2: 脚本契合度 (支持点击跳转文案缺失句)
		Panel c2 = MakeInteractiveCardPanel(Color.FromArgb(56, 189, 248), "👆 定位缺失句", JumpToNextScriptIssue);
		Label c2Title = MakeCardTitle("脚本契合度 (Script Recall)");
		_lblRecallScore = new Label { Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold), ForeColor = Color.FromArgb(56, 189, 248), AutoSize = true, Location = new Point(14, 26), Text = "--%" };
		_lblRecallDesc = new Label { Font = new Font("Microsoft YaHei UI", 8f), ForeColor = _mutedColor, AutoSize = true, Location = new Point(16, 58), Text = "需提供标准文案" };
		c2.Controls.AddRange(new Control[] { c2Title, _lblRecallScore, _lblRecallDesc });
		kpiGrid.Controls.Add(c2, 1, 0);

		// Card 3: 剪切透视统计 (支持循环切换切除分类筛选)
		Panel c3 = MakeInteractiveCardPanel(Color.FromArgb(245, 158, 11), "👆 循环分类筛选", CycleCutBreakdownFilter);
		Label c3Title = MakeCardTitle("剪切透视 (Cut Breakdown)");
		_lblCutStats = new Label { Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold), ForeColor = Color.FromArgb(245, 158, 11), AutoSize = true, Location = new Point(14, 26), Text = "-- 处" };
		_lblCutDesc = new Label { Font = new Font("Microsoft YaHei UI", 8f), ForeColor = _mutedColor, AutoSize = true, Location = new Point(16, 58), Text = "静音 / 语气词 / 重复" };
		c3.Controls.AddRange(new Control[] { c3Title, _lblCutStats, _lblCutDesc });
		kpiGrid.Controls.Add(c3, 2, 0);

		// Card 4: 风险监测 (支持一键排查高危与需复核项)
		Panel c4 = MakeInteractiveCardPanel(Color.FromArgb(244, 63, 94), "👆 一键排查高危", FilterByRisk);
		Label c4Title = MakeCardTitle("风险预警 (Risk Guard)");
		_lblRiskStats = new Label { Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold), ForeColor = Color.FromArgb(244, 63, 94), AutoSize = true, Location = new Point(14, 26), Text = "-- 处" };
		_lblRiskDesc = new Label { Font = new Font("Microsoft YaHei UI", 8f), ForeColor = _mutedColor, AutoSize = true, Location = new Point(16, 58), Text = "高危疑似误剪监控" };
		c4.Controls.AddRange(new Control[] { c4Title, _lblRiskStats, _lblRiskDesc });
		kpiGrid.Controls.Add(c4, 3, 0);

		kpiContainer.Controls.Add(kpiGrid);
		Controls.Add(kpiContainer);

		// Bottom Action Bar
		Panel bottomPanel = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 64,
			BackColor = _cardBg,
			Padding = new Padding(18, 10, 18, 10)
		};
		bottomPanel.Paint += (s, e) =>
		{
			using Pen p = new Pen(_borderColor);
			e.Graphics.DrawLine(p, 0, 0, bottomPanel.Width, 0);
		};

		Label tipLabel = new Label
		{
			Text = "💡 战略智能提示: 质检完全为可选流程，不影响直接交付。点击【▶ 试听】可试听剪除原音，若发现误剪可点击【➕ 恢复】一秒挽回。",
			Font = new Font("Microsoft YaHei UI", 8.5f),
			ForeColor = Color.FromArgb(165, 180, 252),
			AutoSize = true,
			Location = new Point(18, 22)
		};
		bottomPanel.Controls.Add(tipLabel);

		Button btnExportReport = MakeButton("📄 导出质检报告", 130, 36, Color.FromArgb(37, 42, 60), Color.White);
		btnExportReport.Anchor = AnchorStyles.Right | AnchorStyles.Top;
		btnExportReport.Location = new Point(bottomPanel.Width - 440, 14);
		btnExportReport.Click += delegate { ExportQcReportHtml(); };
		bottomPanel.Controls.Add(btnExportReport);

		Button btnGoDeliver = MakeButton("🚀 确认无误，直通交付 →", 180, 36, Color.FromArgb(16, 185, 129), Color.White);
		btnGoDeliver.Anchor = AnchorStyles.Right | AnchorStyles.Top;
		btnGoDeliver.Location = new Point(bottomPanel.Width - 300, 14);
		btnGoDeliver.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
		btnGoDeliver.Click += delegate
		{
			DialogResult = DialogResult.OK;
			Close();
			_mainForm.SwitchToWorkspace(MainForm.WorkspaceDeliver);
		};
		bottomPanel.Controls.Add(btnGoDeliver);

		Button btnClose = MakeButton("关闭", 90, 36, Color.FromArgb(45, 50, 70), Color.White);
		btnClose.Anchor = AnchorStyles.Right | AnchorStyles.Top;
		btnClose.Location = new Point(bottomPanel.Width - 110, 14);
		btnClose.Click += delegate { Close(); };
		bottomPanel.Controls.Add(btnClose);

		bottomPanel.Resize += (s, e) =>
		{
			btnExportReport.Left = bottomPanel.Width - 440;
			btnGoDeliver.Left = bottomPanel.Width - 300;
			btnClose.Left = bottomPanel.Width - 110;
		};
		Controls.Add(bottomPanel);

		// Selected Cut Action Sub-bar (between workspace and bottom panel)
		Panel selectedActionPanel = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 60,
			BackColor = Color.FromArgb(20, 22, 33),
			Padding = new Padding(16, 6, 16, 6)
		};
		selectedActionPanel.Paint += (s, e) =>
		{
			using Pen p = new Pen(_borderColor);
			e.Graphics.DrawLine(p, 0, 0, selectedActionPanel.Width, 0);
		};

		_lblSelectedCutTitle = new Label
		{
			Text = "🎯 选中切除片段: 请在右侧列表中点击任意一项进行试听与审阅",
			Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
			ForeColor = Color.White,
			AutoSize = true,
			Location = new Point(18, 10)
		};
		selectedActionPanel.Controls.Add(_lblSelectedCutTitle);

		_lblSelectedCutDesc = new Label
		{
			Text = "点击【▶ 试听切除原音】可单独聆听被切除的原片声音与画面；若属于关键内容可点击【➕ 恢复此段到时间线】一秒挽救。",
			Font = new Font("Microsoft YaHei UI", 8.25f),
			ForeColor = _mutedColor,
			AutoSize = true,
			Location = new Point(18, 32)
		};
		selectedActionPanel.Controls.Add(_lblSelectedCutDesc);

		_btnPlayCut = MakeButton("▶ 试听切除原音", 130, 36, Color.FromArgb(14, 165, 233), Color.White);
		_btnPlayCut.Anchor = AnchorStyles.Right | AnchorStyles.Top;
		_btnPlayCut.Location = new Point(selectedActionPanel.Width - 310, 12);
		_btnPlayCut.Enabled = false;
		_btnPlayCut.Click += delegate { PlaySelectedCut(); };
		selectedActionPanel.Controls.Add(_btnPlayCut);

		_btnRestoreCut = MakeButton("➕ 恢复此段到时间线", 150, 36, Color.FromArgb(79, 70, 229), Color.White);
		_btnRestoreCut.Anchor = AnchorStyles.Right | AnchorStyles.Top;
		_btnRestoreCut.Location = new Point(selectedActionPanel.Width - 170, 12);
		_btnRestoreCut.Enabled = false;
		_btnRestoreCut.Click += delegate { RestoreSelectedCut(); };
		selectedActionPanel.Controls.Add(_btnRestoreCut);

		selectedActionPanel.Resize += (s, e) =>
		{
			_btnPlayCut.Left = selectedActionPanel.Width - 310;
			_btnRestoreCut.Left = selectedActionPanel.Width - 170;
		};
		Controls.Add(selectedActionPanel);

		// Main Split Area (Left 44%: Script Diff | Right 56%: Cut Audit List)
		SplitContainer splitMain = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Vertical,
			SplitterDistance = 600,
			SplitterWidth = 8,
			BackColor = _bgColor
		};

		// --- Left: Script Diff Panel ---
		Panel leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = _cardBg, Padding = new Padding(12) };
		Label lblLeftTitle = new Label
		{
			Text = "📋 黄金标准文案 vs 成片口播 智能对照 (含中文直译)",
			Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
			ForeColor = Color.White,
			Dock = DockStyle.Top,
			Height = 28
		};
		leftPanel.Controls.Add(lblLeftTitle);

		Panel scriptInputBox = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(0, 4, 0, 4) };
		_txtScriptInput = new TextBox
		{
			Multiline = true,
			ScrollBars = ScrollBars.Vertical,
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(15, 17, 24),
			ForeColor = _textColor,
			BorderStyle = BorderStyle.FixedSingle,
			Font = new Font("Microsoft YaHei UI", 8.5f),
			Text = _qcInput.Script ?? ""
		};
		scriptInputBox.Controls.Add(_txtScriptInput);

		Panel scriptActionRow = new Panel { Dock = DockStyle.Top, Height = 32 };
		_btnReAnalyze = MakeButton("🔄 引用此文案重新质检比对", 180, 26, Color.FromArgb(37, 42, 60), Color.FromArgb(226, 232, 240));
		_btnReAnalyze.Font = new Font("Microsoft YaHei UI", 8f);
		_btnReAnalyze.Click += delegate
		{
			_qcInput.Script = _txtScriptInput.Text.Trim();
			RunAnalysis();
		};
		scriptActionRow.Controls.Add(_btnReAnalyze);

		Label lblScriptHint = new Label
		{
			Text = "可在上方修改或粘贴标准脚本",
			ForeColor = _mutedColor,
			Font = new Font("Microsoft YaHei UI", 8f),
			AutoSize = true,
			Location = new Point(190, 6)
		};
		scriptActionRow.Controls.Add(lblScriptHint);

		leftPanel.Controls.Add(scriptActionRow);
		leftPanel.Controls.Add(scriptInputBox);

		// Diff RichTextBox
		_rtbDiffView = new RichTextBox
		{
			Dock = DockStyle.Fill,
			BackColor = Color.FromArgb(12, 14, 20),
			ForeColor = _textColor,
			BorderStyle = BorderStyle.None,
			ReadOnly = true,
			Font = new Font("Segoe UI", 9.5f)
		};
		leftPanel.Controls.Add(_rtbDiffView);

		// Legend bar at bottom of left panel
		Panel legendPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(4) };
		Label lblLegend = new Label
		{
			Text = "图例: ⚪ 吻合保留  🟢 主播发挥  🔴 念了被剪  ⚪ 划线 未念/未识别",
			Font = new Font("Microsoft YaHei UI", 8f),
			ForeColor = _mutedColor,
			Dock = DockStyle.Fill
		};
		legendPanel.Controls.Add(lblLegend);
		leftPanel.Controls.Add(legendPanel);

		splitMain.Panel1.Controls.Add(leftPanel);

		// --- Right: Cut Audit Table Panel ---
		Panel rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = _cardBg, Padding = new Padding(12) };
		Panel rightHeader = new Panel { Dock = DockStyle.Top, Height = 36 };
		_lblRightTitle = new Label
		{
			Text = "✂️ 被剪切片段全量透明审计与一键纠偏",
			Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
			ForeColor = Color.White,
			AutoSize = true,
			Location = new Point(0, 6)
		};
		rightHeader.Controls.Add(_lblRightTitle);

		FlowLayoutPanel filterFlow = new FlowLayoutPanel
		{
			Dock = DockStyle.Right,
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			Padding = new Padding(0, 4, 0, 0),
			BackColor = Color.Transparent
		};
		_rbFilterAll = new RadioButton { Text = "全部", ForeColor = Color.White, Checked = true, AutoSize = true, Cursor = Cursors.Hand };
		_rbFilterReview = new RadioButton { Text = "⚠️ 需复核", ForeColor = Color.FromArgb(245, 158, 11), AutoSize = true, Cursor = Cursors.Hand };
		_rbFilterSafe = new RadioButton { Text = "🟢 安全切除", ForeColor = Color.FromArgb(16, 185, 129), AutoSize = true, Cursor = Cursors.Hand };

		_rbFilterAll.CheckedChanged += delegate { if (_rbFilterAll.Checked) { _currentFilterMode = CutFilterMode.All; _lblRightTitle.Text = "✂️ 被剪切片段全量透明审计与一键纠偏 [全部]"; RefreshCutTable(); } };
		_rbFilterReview.CheckedChanged += delegate { if (_rbFilterReview.Checked) { _currentFilterMode = CutFilterMode.RiskOnly; _lblRightTitle.Text = "✂️ 被剪切片段全量透明审计 [🔴 已筛选高危/需复核切片]"; RefreshCutTable(); } };
		_rbFilterSafe.CheckedChanged += delegate { if (_rbFilterSafe.Checked) { _currentFilterMode = CutFilterMode.SafeOnly; _lblRightTitle.Text = "✂️ 被剪切片段全量透明审计 [🟢 仅看安全切除]"; RefreshCutTable(); } };

		filterFlow.Controls.AddRange(new Control[] { _rbFilterAll, _rbFilterReview, _rbFilterSafe });
		rightHeader.Controls.Add(filterFlow);
		rightPanel.Controls.Add(rightHeader);

		// Cut ListView
		_lvCutAudit = new ListView
		{
			Dock = DockStyle.Fill,
			View = View.Details,
			FullRowSelect = true,
			MultiSelect = false,
			HideSelection = false,
			BackColor = Color.FromArgb(12, 14, 20),
			ForeColor = _textColor,
			BorderStyle = BorderStyle.None,
			Font = new Font("Microsoft YaHei UI", 8.5f)
		};
		_lvCutAudit.Columns.Add("序号", 45);
		_lvCutAudit.Columns.Add("时间轴范围", 120);
		_lvCutAudit.Columns.Add("被剪切原文", 200);
		_lvCutAudit.Columns.Add("中文直译", 180);
		_lvCutAudit.Columns.Add("切除分类与原因", 230);
		_lvCutAudit.Columns.Add("风险判定", 90);

		_lvCutAudit.SelectedIndexChanged += delegate
		{
			if (_lvCutAudit.SelectedItems.Count > 0)
			{
				_selectedCutItem = _lvCutAudit.SelectedItems[0].Tag as QcCutItem;
				UpdateSelectedCutCard();
			}
			else
			{
				_selectedCutItem = null;
				UpdateSelectedCutCard();
			}
		};

		_lvCutAudit.DoubleClick += delegate
		{
			if (_selectedCutItem != null) PlaySelectedCut();
		};

		rightPanel.Controls.Add(_lvCutAudit);
		splitMain.Panel2.Controls.Add(rightPanel);

		Controls.Add(splitMain);

		// Ensure proper z-order
		splitMain.BringToFront();
	}

	private Panel MakeInteractiveCardPanel(Color accentColor, string actionTag, Action onClick)
	{
		Panel p = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = _cardBg,
			Margin = new Padding(4),
			Cursor = Cursors.Hand
		};

		bool isHovered = false;

		p.Paint += (s, e) =>
		{
			Color bColor = isHovered ? accentColor : _borderColor;
			using Pen pen = new Pen(bColor, isHovered ? 2f : 1f);
			e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);

			if (!string.IsNullOrEmpty(actionTag))
			{
				using Font tagFont = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Bold);
				using SolidBrush tagBrush = new SolidBrush(isHovered ? accentColor : Color.FromArgb(120, 140, 175));
				SizeF sz = e.Graphics.MeasureString(actionTag, tagFont);
				e.Graphics.DrawString(actionTag, tagFont, tagBrush, p.Width - sz.Width - 8, 8);
			}
		};

		void SetHover(bool h)
		{
			if (isHovered == h) return;
			isHovered = h;
			p.BackColor = h ? Color.FromArgb(32, 38, 56) : _cardBg;
			p.Invalidate();
		}

		p.MouseEnter += (s, e) => SetHover(true);
		p.MouseLeave += (s, e) => SetHover(false);
		p.Click += (s, e) => onClick?.Invoke();

		p.ControlAdded += (s, e) =>
		{
			if (e.Control == null) return;
			e.Control.Cursor = Cursors.Hand;
			e.Control.MouseEnter += (cs, ce) => SetHover(true);
			e.Control.MouseLeave += (cs, ce) => SetHover(false);
			e.Control.Click += (cs, ce) => onClick?.Invoke();
		};

		return p;
	}

	private void ResetAllFilters()
	{
		_currentFilterMode = CutFilterMode.All;
		if (_rbFilterAll != null) _rbFilterAll.Checked = true;
		RefreshCutTable();
		if (_lvCutAudit.Items.Count > 0)
		{
			_lvCutAudit.Items[0].Selected = true;
			_lvCutAudit.Items[0].EnsureVisible();
		}
		if (_rtbDiffView.TextLength > 0)
		{
			_rtbDiffView.SelectionStart = 0;
			_rtbDiffView.ScrollToCaret();
		}
		if (_lblRightTitle != null) _lblRightTitle.Text = "✂️ 被剪切片段全量透明审计与一键纠偏 [全部]";
		if (_lblSelectedCutTitle != null) _lblSelectedCutTitle.Text = $"🎯 品控总览: {_qcReport?.Score:0.0}分 · {_qcReport?.Verdict} | 已恢复展示全部 {_qcReport?.CutItems?.Count ?? 0} 处切除明细";
		if (_lblSelectedCutDesc != null) _lblSelectedCutDesc.Text = "💡 点击上方指标卡片可快速筛选高危、分类查看或定位文案缺失句；点击【试听】与【恢复】可一秒抢救。";
	}

	private void JumpToNextScriptIssue()
	{
		if (_qcReport == null || _qcReport.ScriptSentences == null || _qcReport.ScriptSentences.Count == 0) return;

		var issues = _qcReport.ScriptSentences
			.Where(s => s.Risk != QcRisk.Safe || s.Status.Contains("剪") || s.Status.Contains("缺失") || s.Status.Contains("未念"))
			.ToList();

		if (issues.Count == 0)
		{
			MessageBox.Show(this, "🎉 太棒了！标准文案中所有句子均已完整保留在成片中，无任何缺失或误剪！", "文案 100% 完整", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		_scriptIssueCursor = (_scriptIssueCursor + 1) % issues.Count;
		var target = issues[_scriptIssueCursor];

		string headerTag = $"[第 {target.Index} 句";
		int pos = _rtbDiffView.Text.IndexOf(headerTag, StringComparison.Ordinal);
		if (pos >= 0)
		{
			int lineEnd = _rtbDiffView.Text.IndexOf('\n', pos);
			int len = (lineEnd > pos) ? lineEnd - pos : headerTag.Length;
			_rtbDiffView.SelectionStart = pos;
			_rtbDiffView.SelectionLength = len;
			_rtbDiffView.ScrollToCaret();
			_rtbDiffView.Focus();
		}

		if (_lblSelectedCutTitle != null)
			_lblSelectedCutTitle.Text = $"🎯 脚本问题定位 ({_scriptIssueCursor + 1}/{issues.Count}): [第 {target.Index} 句 · {target.Status}] (再次点击卡片可跳至下一处)";
		if (_lblSelectedCutDesc != null)
			_lblSelectedCutDesc.Text = $"原文: \"{target.Text}\" | 中文直译: \"{target.LiteralZh}\"";
	}

	private void CycleCutBreakdownFilter()
	{
		switch (_currentFilterMode)
		{
			case CutFilterMode.RetakeOnly:
				_currentFilterMode = CutFilterMode.FillerOnly;
				if (_lblRightTitle != null) _lblRightTitle.Text = "✂️ 被剪切片段全量透明审计 [🟡 仅看语气词 / 口头禅]";
				break;
			case CutFilterMode.FillerOnly:
				_currentFilterMode = CutFilterMode.SilenceOnly;
				if (_lblRightTitle != null) _lblRightTitle.Text = "✂️ 被剪切片段全量透明审计 [⚪ 仅看静音停顿 / 换气留白]";
				break;
			case CutFilterMode.SilenceOnly:
				_currentFilterMode = CutFilterMode.All;
				if (_rbFilterAll != null) _rbFilterAll.Checked = true;
				if (_lblRightTitle != null) _lblRightTitle.Text = "✂️ 被剪切片段全量透明审计与一键纠偏 [全部]";
				break;
			default:
				_currentFilterMode = CutFilterMode.RetakeOnly;
				if (_lblRightTitle != null) _lblRightTitle.Text = "✂️ 被剪切片段全量透明审计 [🟠 仅看口误重念 / 重复录制]";
				break;
		}

		RefreshCutTable();
		if (_lvCutAudit.Items.Count > 0)
		{
			_lvCutAudit.Items[0].Selected = true;
			_lvCutAudit.Items[0].EnsureVisible();
			_lvCutAudit.Focus();
		}
	}

	private void FilterByRisk()
	{
		_currentFilterMode = CutFilterMode.RiskOnly;
		if (_rbFilterReview != null) _rbFilterReview.Checked = true;
		RefreshCutTable();
		if (_lvCutAudit.Items.Count > 0)
		{
			_lvCutAudit.Items[0].Selected = true;
			_lvCutAudit.Items[0].EnsureVisible();
			_lvCutAudit.Focus();
		}
		if (_lblRightTitle != null)
			_lblRightTitle.Text = $"✂️ 被剪切片段全量透明审计 [🔴 已筛选 {_lvCutAudit.Items.Count} 处高危/需复核切片]";
		if (_lblSelectedCutTitle != null)
			_lblSelectedCutTitle.Text = $"🎯 已极速定位至高危切除片段: 列表中共有 {_lvCutAudit.Items.Count} 项，已为您自动选中第 1 项";
	}

	private Label MakeCardTitle(string title)
	{
		return new Label
		{
			Text = title,
			Font = new Font("Microsoft YaHei UI", 8.25f, FontStyle.Bold),
			ForeColor = _mutedColor,
			AutoSize = true,
			Location = new Point(14, 8)
		};
	}

	private Button MakeButton(string text, int width, int height, Color bg, Color fg)
	{
		Button b = new Button
		{
			Text = text,
			Size = new Size(width, height),
			BackColor = bg,
			ForeColor = fg,
			FlatStyle = FlatStyle.Flat,
			Cursor = Cursors.Hand
		};
		b.FlatAppearance.BorderSize = 0;
		return b;
	}

	private void RunAnalysis()
	{
		_qcReport = QcEngine.Analyze(_qcInput);
		UpdateKpiDisplay();
		RenderScriptDiff();
		RefreshCutTable();

		// Asynchronously fetch Chinese direct translation (直译) for cut items and script sentences
		ThreadPool.QueueUserWorkItem(async delegate
		{
			await PopulateLiteralTranslationsAsync();
		});
	}

	private void UpdateKpiDisplay()
	{
		if (_qcReport == null) return;

		// Card 1
		Color vColor = _qcReport.VerdictLevel switch
		{
			QcRisk.Safe => Color.FromArgb(16, 185, 129),
			QcRisk.Review => Color.FromArgb(245, 158, 11),
			_ => Color.FromArgb(244, 63, 94)
		};
		string icon = _qcReport.VerdictLevel switch
		{
			QcRisk.Safe => "🟢",
			QcRisk.Review => "🟡",
			_ => "🔴"
		};
		_lblVerdictScore.ForeColor = vColor;
		_lblVerdictScore.Text = $"{icon} {_qcReport.Score:0.0}分 · {_qcReport.Verdict}";
		_lblVerdictDesc.ForeColor = vColor;
		_lblVerdictDesc.Text = _qcReport.VerdictLevel == QcRisk.Safe
			? "✔ 核心卖点完整保留 · 无致命信息缺失"
			: (_qcReport.VerdictLevel == QcRisk.Review ? "⚠ 存在疑似吞字或重音剪除，建议复核" : "✖ 关键信息缺失较多，请重点核对");

		// Card 2
		if (_qcReport.HasScript)
		{
			_lblRecallScore.Text = $"{_qcReport.ScriptRecall * 100.0:0.0}%";
			_lblRecallDesc.Text = $"{_qcReport.KeptMatchedTokenCount} / {_qcReport.ScriptTokenCount} 词匹配 (保留率 {_qcReport.RetentionRate * 100.0:0.0}%)";
		}
		else
		{
			_lblRecallScore.Text = "未提供文案";
			_lblRecallDesc.Text = "仅按纯人声切除进行质检";
		}

		// Card 3
		_lblCutStats.Text = $"共切除 {_qcReport.CutItems.Count} 处 (-{_qcReport.CutDuration:0.0}s)";
		_lblCutDesc.Text = $"静音 {_qcReport.SilenceCount} | 语气词 {_qcReport.FillerCount} | 口误 {_qcReport.RetakeCount}";

		// Card 4
		int totalRisk = _qcReport.HighCount + _qcReport.ReviewCount;
		_lblRiskStats.Text = $"{_qcReport.HighCount} 致命 · {_qcReport.ReviewCount} 建议复核";
		_lblRiskStats.ForeColor = totalRisk > 0 ? Color.FromArgb(244, 63, 94) : Color.FromArgb(16, 185, 129);
		_lblRiskDesc.Text = _qcReport.ClippedCount > 0 ? $"发现 {_qcReport.ClippedCount} 处接缝可能吞字" : "音画接缝平滑完整";
	}

	private void RenderScriptDiff()
	{
		_rtbDiffView.Clear();
		if (_qcReport == null) return;

		if (!_qcReport.HasScript)
		{
			_rtbDiffView.SelectionColor = _mutedColor;
			_rtbDiffView.AppendText("【提示】尚未提供标准参考文案。\n\n在上方文本框中粘贴标准脚本，并点击【🔄 引用此文案重新质检比对】，系统即可自动与原声逐词比对，识别是否有关键内容被剪除！");
			return;
		}

		for (int i = 0; i < _qcReport.ScriptSentences.Count; i++)
		{
			var ss = _qcReport.ScriptSentences[i];

			// Sentence title & status badge
			Color titleColor = ss.Risk switch
			{
				QcRisk.Safe => Color.FromArgb(52, 211, 153),
				QcRisk.Review => Color.FromArgb(251, 191, 36),
				_ => Color.FromArgb(248, 113, 113)
			};
			_rtbDiffView.SelectionFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
			_rtbDiffView.SelectionColor = titleColor;
			_rtbDiffView.AppendText($"[第 {ss.Index} 句 · {ss.Status}]\n");

			// Render tokens with color highlighting
			for (int ti = 0; ti < ss.DisplayTokens.Count; ti++)
			{
				string tok = ss.DisplayTokens[ti];
				int state = ss.TokenState[ti];

				if (state == 0)
				{
					// Kept in final cut: Normal White
					_rtbDiffView.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
					_rtbDiffView.SelectionColor = Color.FromArgb(241, 245, 249);
					_rtbDiffView.SelectionBackColor = Color.Transparent;
					_rtbDiffView.AppendText(tok + " ");
				}
				else if (state == 1)
				{
					// Cut from spoken audio: Red Highlight with Strikeout
					_rtbDiffView.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Strikeout | FontStyle.Bold);
					_rtbDiffView.SelectionColor = Color.FromArgb(254, 202, 202);
					_rtbDiffView.SelectionBackColor = Color.FromArgb(153, 27, 27);
					_rtbDiffView.AppendText(tok + " ");
				}
				else
				{
					// Unspoken / Not detected: Muted Gray with dashed note
					_rtbDiffView.SelectionFont = new Font("Segoe UI", 9.5f, FontStyle.Italic);
					_rtbDiffView.SelectionColor = Color.FromArgb(100, 116, 139);
					_rtbDiffView.SelectionBackColor = Color.Transparent;
					_rtbDiffView.AppendText(tok + " ");
				}
			}
			_rtbDiffView.SelectionBackColor = Color.Transparent;
			_rtbDiffView.AppendText("\n");

			// Literal translation line (直译)
			if (!string.IsNullOrWhiteSpace(ss.LiteralZh))
			{
				_rtbDiffView.SelectionFont = new Font("Microsoft YaHei UI", 8.5f);
				_rtbDiffView.SelectionColor = Color.FromArgb(148, 163, 184);
				_rtbDiffView.AppendText($"【中文直译】{ss.LiteralZh}\n");
			}
			_rtbDiffView.AppendText("\n");
		}
	}

	private void RefreshCutTable()
	{
		_lvCutAudit.BeginUpdate();
		_lvCutAudit.Items.Clear();

		if (_qcReport != null && _qcReport.CutItems != null)
		{
			foreach (var cut in _qcReport.CutItems)
			{
				if (_currentFilterMode == CutFilterMode.RiskOnly && cut.Risk == QcRisk.Safe) continue;
				if (_currentFilterMode == CutFilterMode.RetakeOnly && !(cut.Category.Contains("口误") || cut.Category.Contains("重复"))) continue;
				if (_currentFilterMode == CutFilterMode.FillerOnly && !cut.Category.Contains("语气")) continue;
				if (_currentFilterMode == CutFilterMode.SilenceOnly && !cut.Category.Contains("静音")) continue;
				if (_currentFilterMode == CutFilterMode.SafeOnly && cut.Risk != QcRisk.Safe) continue;

				if (_currentFilterMode == CutFilterMode.All)
				{
					if (_rbFilterReview != null && _rbFilterReview.Checked && cut.Risk == QcRisk.Safe) continue;
					if (_rbFilterSafe != null && _rbFilterSafe.Checked && cut.Risk != QcRisk.Safe) continue;
				}

				ListViewItem lvi = new ListViewItem(cut.Index.ToString());
				lvi.SubItems.Add($"{QcEngine.FormatTime(cut.SrcStart)} ~ {QcEngine.FormatTime(cut.SrcEnd)} (-{cut.Duration:0.1}s)");
				lvi.SubItems.Add(string.IsNullOrWhiteSpace(cut.Text) ? "[静音空白 / 换气]" : cut.Text);

				string zhStr = !string.IsNullOrWhiteSpace(cut.LiteralZh)
					? cut.LiteralZh
					: (!string.IsNullOrWhiteSpace(cut.WordGlossZh) ? cut.WordGlossZh : (string.IsNullOrWhiteSpace(cut.Text) ? "[无发音]" : "--"));
				lvi.SubItems.Add(zhStr);

				lvi.SubItems.Add($"{cut.Category}: {cut.Reason}");

				string riskBadge = cut.Risk switch
				{
					QcRisk.Safe => "🟢 安全",
					QcRisk.Review => "🟡 需复核",
					_ => "🔴 疑似误剪"
				};
				lvi.SubItems.Add(riskBadge);

				if (cut.Risk == QcRisk.High)
				{
					lvi.BackColor = Color.FromArgb(50, 15, 20);
					lvi.ForeColor = Color.FromArgb(254, 202, 202);
				}
				else if (cut.Risk == QcRisk.Review)
				{
					lvi.BackColor = Color.FromArgb(45, 30, 10);
					lvi.ForeColor = Color.FromArgb(253, 224, 71);
				}

				lvi.Tag = cut;
				_lvCutAudit.Items.Add(lvi);
			}
		}

		_lvCutAudit.EndUpdate();
	}

	private void UpdateSelectedCutCard()
	{
		if (_selectedCutItem == null)
		{
			_lblSelectedCutTitle.Text = "🎯 选中切除片段: 请在右侧列表中点击任意一项进行试听与审阅";
			_lblSelectedCutDesc.Text = "点击【▶ 试听切除原音】可单独聆听被切除的原片声音与画面；若属于关键内容可点击【➕ 恢复此段到时间线】一秒挽救。";
			_btnPlayCut.Enabled = false;
			_btnRestoreCut.Enabled = false;
			return;
		}

		string textDisp = string.IsNullOrWhiteSpace(_selectedCutItem.Text) ? "[无声波对白]" : _selectedCutItem.Text;
		string zhDisp = !string.IsNullOrWhiteSpace(_selectedCutItem.LiteralZh) ? $"【中文直译: {_selectedCutItem.LiteralZh}】" : "";
		_lblSelectedCutTitle.Text = $"🎯 选中 #{_selectedCutItem.Index} [{QcEngine.FormatTime(_selectedCutItem.SrcStart)} ~ {QcEngine.FormatTime(_selectedCutItem.SrcEnd)}] {_selectedCutItem.Category}: \"{textDisp}\" {zhDisp}";
		_lblSelectedCutDesc.Text = $"原因分析: {_selectedCutItem.Reason} | 切除时长: {_selectedCutItem.Duration:0.2}秒 | 成片接缝处: {QcEngine.FormatTime(_selectedCutItem.TimelineSeam)}";

		_btnPlayCut.Enabled = true;
		_btnRestoreCut.Enabled = true;
	}

	private void PlaySelectedCut()
	{
		if (_selectedCutItem == null || string.IsNullOrEmpty(_srcPath) || !File.Exists(_srcPath)) return;

		try
		{
			double s = Math.Max(0.0, _selectedCutItem.SrcStart);
			double e = Math.Max(s + 0.3, _selectedCutItem.SrcEnd);
			using (var previewForm = new VideoPreviewForm(_srcPath, $"试听被剪切片段 - {_selectedCutItem.Category} ({_selectedCutItem.Duration:0.1}秒)", deleteOnClose: false, startSeconds: s, endSeconds: e))
			{
				previewForm.ShowDialog(this);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "播放试听片段失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void RestoreSelectedCut()
	{
		if (_selectedCutItem == null) return;

		DialogResult dr = MessageBox.Show(
			this,
			$"确定要将切除片段 #{_selectedCutItem.Index}\n[{QcEngine.FormatTime(_selectedCutItem.SrcStart)} ~ {QcEngine.FormatTime(_selectedCutItem.SrcEnd)}] ({_selectedCutItem.Duration:0.2}秒)\n恢复回成片时间线吗？\n\n系统将自动在接缝处拼回此段，并自动无缝向后顺延后续素材与字幕！",
			"确认一键恢复",
			MessageBoxButtons.YesNo,
			MessageBoxIcon.Question);

		if (dr != DialogResult.Yes) return;

		try
		{
			_mainForm.RestoreCutIntervalToTimeline(_selectedCutItem.SrcStart, _selectedCutItem.SrcEnd, _selectedCutItem.TimelineSeam);
			MessageBox.Show(this, "🎉 已成功将该片段恢复至时间线并完成波纹闭合！", "恢复成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

			// Re-query current timeline state and update QC
			_qcInput = _mainForm.BuildQcInputFromCurrentState(_qcInput.Script);
			RunAnalysis();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "恢复片段失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private async Task PopulateLiteralTranslationsAsync()
	{
		if (_qcReport == null) return;

		// 1. Fill glossaries for filler words
		foreach (var c in _qcReport.CutItems)
		{
			if (string.IsNullOrEmpty(c.LiteralZh))
			{
				var glosses = c.Words.Select(w => QcEngine.FillerGloss(QcEngine.Norm(w))).Where(g => g != null).ToList();
				if (glosses.Count > 0)
				{
					c.WordGlossZh = "“" + string.Join(" ", glosses) + "” (语气助词)";
				}
			}
		}

		// 2. Translate non-filler cut items with actual spoken words
		var needTransCut = _qcReport.CutItems
			.Where(c => !string.IsNullOrWhiteSpace(c.Text) && string.IsNullOrEmpty(c.LiteralZh) && c.ContentWords.Count > 0)
			.Take(25)
			.ToList();

		string srcLang = _whisperLang.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt" : _whisperLang;

		foreach (var item in needTransCut)
		{
			try
			{
				string zh = await TranscribeEngine.TranslateToChineseAsync(item.Text, srcLang);
				if (!string.IsNullOrWhiteSpace(zh) && zh != item.Text)
				{
					item.LiteralZh = zh.Trim();
				}
			}
			catch { }
		}

		// 3. Translate script sentences
		if (_qcReport.HasScript)
		{
			var needTransScript = _qcReport.ScriptSentences.Where(s => string.IsNullOrEmpty(s.LiteralZh)).Take(20).ToList();
			foreach (var ss in needTransScript)
			{
				try
				{
					string zh = await TranscribeEngine.TranslateToChineseAsync(ss.Text, srcLang);
					if (!string.IsNullOrWhiteSpace(zh) && zh != ss.Text)
					{
						ss.LiteralZh = zh.Trim();
					}
				}
				catch { }
			}
		}

		if (IsHandleCreated)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				RefreshCutTable();
				RenderScriptDiff();
				UpdateSelectedCutCard();
			});
		}
	}

	private void ExportQcReportHtml()
	{
		if (_qcReport == null) return;

		SaveFileDialog sfd = new SaveFileDialog
		{
			Title = "保存质检报告",
			Filter = "HTML 质检报告 (*.html)|*.html|纯文本报告 (*.txt)|*.txt",
			FileName = $"质检报告_{Path.GetFileNameWithoutExtension(_srcPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.html"
		};

		if (sfd.ShowDialog(this) != DialogResult.OK) return;

		try
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
			sb.AppendLine("<title>AI 智能视频质检与脚本对齐报告</title>");
			sb.AppendLine("<style>");
			sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Microsoft YaHei', sans-serif; background: #0F172A; color: #E2E8F0; padding: 24px; }");
			sb.AppendLine(".card { background: #1E293B; border-radius: 8px; padding: 16px; margin-bottom: 16px; border: 1px solid #334155; }");
			sb.AppendLine("h1, h2, h3 { color: #FFFFFF; }");
			sb.AppendLine(".badge-green { color: #10B981; font-weight: bold; }");
			sb.AppendLine(".badge-yellow { color: #F59E0B; font-weight: bold; }");
			sb.AppendLine(".badge-red { color: #F43F5E; font-weight: bold; }");
			sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 12px; }");
			sb.AppendLine("th, td { border: 1px solid #334155; padding: 8px 12px; text-align: left; }");
			sb.AppendLine("th { background: #0F172A; color: #94A3B8; }");
			sb.AppendLine(".strike { text-decoration: line-through; color: #F87171; background: #450A0A; padding: 2px 4px; border-radius: 3px; }");
			sb.AppendLine("</style></head><body>");

			sb.AppendLine("<h1>🧐 AI 智能视频质检与脚本对齐报告</h1>");
			sb.AppendLine($"<p>素材: <b>{Path.GetFileName(_srcPath)}</b> | 报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

			sb.AppendLine("<div class='card'>");
			sb.AppendLine($"<h2>综合品控结论: <span class='{(_qcReport.VerdictLevel == QcRisk.Safe ? "badge-green" : "badge-yellow")}'>{_qcReport.Score:0.0}分 · {_qcReport.Verdict}</span></h2>");
			sb.AppendLine($"<p>{_qcReport.Summary}</p>");
			sb.AppendLine($"<p>成片时长: <b>{_qcReport.KeptDuration:0.1}s</b> | 共剪除: <b>{_qcReport.CutItems.Count} 处 (-{_qcReport.CutDuration:0.1}s)</b> | 致命风险: <b>{_qcReport.HighCount}</b> | 建议复核: <b>{_qcReport.ReviewCount}</b></p>");
			sb.AppendLine("</div>");

			sb.AppendLine("<div class='card'><h2>被剪切片段审计明细</h2>");
			sb.AppendLine("<table><tr><th>序号</th><th>时间范围</th><th>时长</th><th>被切原文</th><th>中文直译</th><th>切除分类与原因</th><th>风险等级</th></tr>");
			foreach (var c in _qcReport.CutItems)
			{
				string rCls = c.Risk == QcRisk.Safe ? "badge-green" : (c.Risk == QcRisk.Review ? "badge-yellow" : "badge-red");
				sb.AppendLine($"<tr><td>{c.Index}</td><td>{QcEngine.FormatTime(c.SrcStart)} ~ {QcEngine.FormatTime(c.SrcEnd)}</td><td>-{c.Duration:0.2}s</td><td>{c.Text}</td><td>{c.LiteralZh}</td><td>{c.Category}: {c.Reason}</td><td class='{rCls}'>{c.Risk}</td></tr>");
			}
			sb.AppendLine("</table></div>");

			sb.AppendLine("</body></html>");

			File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
			MessageBox.Show(this, "质检报告已成功导出！", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "导出报告失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}
}
