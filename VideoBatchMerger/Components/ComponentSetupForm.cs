using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoBatchMerger.Components;

internal sealed class ComponentSetupForm : Form
{
	private readonly ComponentManager _manager;
	private readonly DataGridView _grid;
	private readonly TextBox _log;
	private readonly Label _status;
	private readonly Button _install, _scan;
	private readonly ProgressBar _progress;
	private CancellationTokenSource _operation;
	private bool _busy;
	public ComponentSetupForm()
	{
		_manager = new ComponentManager();
		Text = "组件与模型 · 一键安装 / 修复"; Size = new Size(1000, 760); MinimumSize = new Size(800, 600);
		StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(19, 32, 55); ForeColor = Color.White;
		Font = new Font("Microsoft YaHei UI", 9f);
		var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 7 };
		layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
		layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
		layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
		layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); Controls.Add(layout);
		layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "新电脑：点击【安装推荐组件】即可准备基础功能。\nChatterbox 和其他识别模型可按需勾选；首次安装需联网，已下载文件会校验并复用。适用于 64 位 Windows 10 / 11。" });
		_grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
			RowHeadersVisible = false, BackgroundColor = Color.FromArgb(13, 24, 41), BorderStyle = BorderStyle.None,
			AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, EnableHeadersVisualStyles = false,
			SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
		_grid.DefaultCellStyle.BackColor = Color.FromArgb(19, 32, 55); _grid.DefaultCellStyle.ForeColor = Color.White;
		_grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 75, 123); _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
		_grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 45, 69); _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
		_grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "selected", HeaderText = "安装", Width = 48 });
		_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "component", HeaderText = "组件 / 模型", Width = 220, ReadOnly = true });
		_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "state", HeaderText = "检测状态", Width = 132, ReadOnly = true });
		_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "detail", HeaderText = "用途 / 空间", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
		foreach (var spec in _manager.Components) _grid.Rows.Add(spec.Recommended, spec.Name, "等待检测", spec.Description);
		layout.Controls.Add(_grid);
		var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
		_install = Button("安装推荐组件", 140); _install.Click += async (_, _) => await InstallAsync(true);
		var selected = Button("安装勾选项", 125); selected.Click += async (_, _) => await InstallAsync(false);
		var all = Button("全选模型", 100); all.Click += (_, _) => { if (!_busy) foreach (DataGridViewRow row in _grid.Rows) row.Cells[0].Value = true; };
		_scan = Button("重新检测", 100); _scan.Click += async (_, _) => await ScanAsync();
		var cancel = Button("取消操作", 100); cancel.Click += (_, _) => _operation?.Cancel();
		actions.Controls.AddRange(new Control[] { _install, selected, all, _scan, cancel }); layout.Controls.Add(actions);
		_status = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(125, 211, 252), AutoEllipsis = true }; layout.Controls.Add(_status);
		_progress = new ProgressBar { Dock = DockStyle.Fill }; layout.Controls.Add(_progress);
		_log = new TextBox { Multiline = true, Dock = DockStyle.Fill, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
			BackColor = Color.FromArgb(13, 24, 41), ForeColor = Color.FromArgb(180, 212, 245) }; layout.Controls.Add(_log);
		layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "ElevenLabs 是在线服务：无需本地模型，使用时填写您自己的 Key。NVIDIA 驱动由电脑厂商 / NVIDIA 安装。" });
		Shown += async (_, _) => await ScanAsync();
		FormClosing += (_, e) => { if (_busy) { _operation.Cancel(); e.Cancel = true; _status.Text = "正在取消；停止后可关闭窗口。"; } };
	}
	private static Button Button(string text, int width) => new Button { Text = text, Width = width, Height = 32,
		BackColor = Color.FromArgb(37, 75, 123), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
	private void Log(string text)
	{
		if (IsDisposed || !IsHandleCreated) return;
		if (InvokeRequired) { BeginInvoke((Action)(() => Log(text))); return; }
		if (_log.TextLength > 100000) _log.Clear(); _log.AppendText(text + Environment.NewLine);
	}
	private void BeginOperation()
	{
		_busy = true; _operation = new CancellationTokenSource(); _install.Enabled = _scan.Enabled = false; _grid.Enabled = false;
	}
	private void EndOperation()
	{
		_busy = false; _operation.Dispose(); _operation = null; _install.Enabled = _scan.Enabled = true; _grid.Enabled = true;
		_progress.Style = ProgressBarStyle.Blocks;
	}
	private async Task ScanAsync()
	{
		if (_busy) return; BeginOperation(); _progress.Style = ProgressBarStyle.Marquee;
		try
		{
			for (int i = 0; i < _manager.Components.Count; i++)
			{
				var spec = _manager.Components[i]; _status.Text = "正在检测：" + spec.Name;
				string result = await Task.Run(() => _manager.CheckAsync(spec, _operation.Token));
				_grid.Rows[i].Cells[2].Value = result;
				_grid.Rows[i].Cells[0].Value = spec.Recommended && result != "已就绪";
			}
			_status.Text = "检测完成。勾选要安装 / 修复的组件，或点击【安装推荐组件】。";
		}
		catch (OperationCanceledException) { _status.Text = "已取消检测。"; }
		catch (Exception ex) { _status.Text = "检测未完成：" + ex.Message; Log(ex.ToString()); }
		finally { EndOperation(); }
	}
	private async Task InstallAsync(bool recommended)
	{
		if (_busy) return; _grid.EndEdit();
		var selected = _manager.Components.Select((spec, index) => new { spec, index })
			.Where(x => recommended ? x.spec.Recommended : Convert.ToBoolean(_grid.Rows[x.index].Cells[0].Value)).ToList();
		if (selected.Count == 0) { _status.Text = "请先勾选至少一个组件。"; return; }
		BeginOperation(); _progress.Maximum = selected.Count; _progress.Value = 0; var failures = new List<string>();
		try
		{
			foreach (var item in selected)
			{
				_operation.Token.ThrowIfCancellationRequested(); _status.Text = "检查 / 安装：" + item.spec.Name;
				try
				{
					string state = await Task.Run(() => _manager.CheckAsync(item.spec, _operation.Token));
					if (state != "已就绪")
					{
						_grid.Rows[item.index].Cells[2].Value = "安装中…"; Log("开始：" + item.spec.Name);
						await Task.Run(() => _manager.InstallAsync(item.spec, Log, _operation.Token));
						state = await Task.Run(() => _manager.CheckAsync(item.spec, _operation.Token));
						if (state != "已就绪") throw new InvalidOperationException("安装后运行验证未通过：" + state);
					}
					_grid.Rows[item.index].Cells[2].Value = "已就绪"; _grid.Rows[item.index].Cells[0].Value = false; Log(item.spec.Name + "：运行验证通过。");
				}
				catch (OperationCanceledException) { _grid.Rows[item.index].Cells[2].Value = "已取消，可重试"; throw; }
				catch (Exception ex) { failures.Add(item.spec.Name); _grid.Rows[item.index].Cells[2].Value = "失败，可重试"; Log(item.spec.Name + "：" + ex.Message); }
				_progress.Value++;
			}
			_status.Text = failures.Count == 0 ? "安装完成，所有选中组件均已通过运行验证。可以关闭窗口开始制作。"
				: "部分组件未完成：" + string.Join("、", failures) + "。已安装项目会保留，请查看下方原因并重试。";
		}
		catch (OperationCanceledException) { _status.Text = "已取消；已完成的组件保留，下次可继续安装。"; }
		finally { EndOperation(); }
	}
}
