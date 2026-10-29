using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoBatchMerger.ScriptBatch
{
    internal sealed class ChatterboxSettingsDialog : Form
    {
        private readonly ScriptBatchSettings _settings;
        private readonly Label _status;
        private readonly TextBox _reference;
        private readonly NumericUpDown _emotion;
        private readonly NumericUpDown _cfg;
        private readonly ComboBox _keepAlive;
        private readonly CheckBox _authorized;
        private readonly Button _install;
        private readonly Button _save;
        private readonly TextBox _log;
        private CancellationTokenSource _installation;
        private string _referencePath;
        private readonly Color _input = Color.FromArgb(15, 23, 42);

        public ChatterboxSettingsDialog(ScriptBatchSettings settings)
        {
            _settings = settings;
            _referencePath = settings.ChatterboxReferenceAudio ?? "";
            Text = "Chatterbox 本地配音 · 声音与情绪";
            Size = new Size(690, 790);
            MinimumSize = new Size(580, 670);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(20, 31, 52);
            ForeColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9f);
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 13 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(content);
            _status = new Label { AutoSize = true, MaximumSize = new Size(610, 0), Margin = new Padding(0, 0, 0, 10) };
            content.Controls.Add(_status);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            _install = Button("安装 / 检查模型", 155);
            _install.Click += async (_, _) => await InstallAsync();
            actions.Controls.Add(_install);
            var cancel = Button("取消安装", 100);
            cancel.Click += (_, _) => _installation?.Cancel();
            actions.Controls.Add(cancel);
            content.Controls.Add(actions);
            content.Controls.Add(Label("声音参考（可选）：默认音色可直接使用。欧葡 / 巴葡口音由参考声音引导。"));
            _reference = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, BackColor = _input, ForeColor = Color.White };
            content.Controls.Add(_reference);
            var references = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var choose = Button("选择参考声音…", 145);
            choose.Click += async (_, _) =>
            {
                using (var dialog = new OpenFileDialog { Filter = "音频 / 视频|*.wav;*.mp3;*.m4a;*.flac;*.ogg;*.mp4;*.mov;*.aac;*.wma|所有文件|*.*" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    choose.Enabled = false;
                    try
                    {
                        _referencePath = await ChatterboxEngine.ImportReferenceAsync(dialog.FileName, CancellationToken.None);
                        _authorized.Checked = false;
                        UpdateStatus();
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "参考声音未导入"); }
                    finally { choose.Enabled = true; }
                }
            };
            references.Controls.Add(choose);
            var clear = Button("使用默认音色", 135);
            clear.Click += (_, _) => { _referencePath = ""; _authorized.Checked = false; UpdateStatus(); };
            references.Controls.Add(clear);
            content.Controls.Add(references);
            _authorized = new CheckBox { Text = "我拥有参考声音的使用 / 克隆授权", AutoSize = true, Checked = settings.ChatterboxReferenceAuthorized };
            content.Controls.Add(_authorized);
            content.Controls.Add(Label("建议：6–12 秒清晰单人干声，不带配乐。软件会保存前 12 秒，不上传该文件。"));
            var tuning = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            tuning.Controls.Add(Label("表现预设："));
            var preset = new ComboBox { Width = 145, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = _input, ForeColor = Color.White };
            preset.Items.AddRange(new object[] { "温暖自然", "坚定有力", "庄重沉稳", "自定义" });
            tuning.Controls.Add(preset);
            _emotion = Number(0, 1.5m, (decimal)Math.Max(0, Math.Min(1.5, settings.ChatterboxExaggeration)));
            _cfg = Number(0, 1, (decimal)Math.Max(0, Math.Min(1, settings.ChatterboxCfg)));
            tuning.Controls.Add(Label("情绪强度")); tuning.Controls.Add(_emotion);
            tuning.Controls.Add(Label("节奏引导")); tuning.Controls.Add(_cfg);
            preset.SelectedIndex = 3;
            preset.SelectedIndexChanged += (_, _) =>
            {
                switch (preset.SelectedIndex)
                {
                    case 0: _emotion.Value = 0.45m; _cfg.Value = 0.50m; break;
                    case 1: _emotion.Value = 0.75m; _cfg.Value = 0.30m; break;
                    case 2: _emotion.Value = 0.55m; _cfg.Value = 0.35m; break;
                }
            };
            content.Controls.Add(tuning);
            content.Controls.Add(Label("情绪过高可能加快语速或降低发音稳定性。语速在主面板调节；字幕会按最终音频重新对齐。"));
            var memory = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            memory.Controls.Add(Label("模型保留："));
            _keepAlive = new ComboBox { Width = 165, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = _input, ForeColor = Color.White };
            _keepAlive.Items.AddRange(new object[] { "空闲 3 分钟后释放", "空闲 15 分钟后释放", "空闲 30 分钟后释放" });
            _keepAlive.SelectedIndex = settings.ChatterboxKeepAliveMinutes == 3 ? 0 : settings.ChatterboxKeepAliveMinutes == 30 ? 2 : 1;
            memory.Controls.Add(_keepAlive);
            var release = Button("立即释放显存", 130);
            release.Click += (_, _) => _log.AppendText(ChatterboxEngine.TryReleaseModel()
                ? "模型已释放；下次生成需重新加载，相同参数的试听缓存仍可立即播放。" + Environment.NewLine
                : "正在配音或安装，请完成 / 取消当前任务后再释放。" + Environment.NewLine);
            memory.Controls.Add(release);
            content.Controls.Add(memory);
            content.RowStyles.Clear();
            for (int i = 0; i < 10; i++) content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _log = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
                BackColor = _input, ForeColor = Color.FromArgb(180, 212, 245), MinimumSize = new Size(0, 150), WordWrap = true };
            content.Controls.Add(_log);
            content.Controls.Add(Label("快速试听只生成声音；相同参数直接复用缓存。保留模型可加快连续调试；完整成片仍会对齐字幕。"));
            _save = Button("保存设置", 130);
            _save.Anchor = AnchorStyles.Right;
            _save.Click += (_, _) =>
            {
                if (!string.IsNullOrEmpty(_referencePath) && !_authorized.Checked)
                {
                    MessageBox.Show(this, "请先确认参考声音的使用授权，或选择【使用默认音色】。", "参考声音"); return;
                }
                settings.ChatterboxReferenceAudio = _referencePath;
                settings.ChatterboxReferenceAuthorized = _authorized.Checked;
                settings.ChatterboxExaggeration = (double)_emotion.Value;
                settings.ChatterboxCfg = (double)_cfg.Value;
                settings.ChatterboxKeepAliveMinutes = _keepAlive.SelectedIndex == 0 ? 3 : _keepAlive.SelectedIndex == 2 ? 30 : 15;
                ChatterboxEngine.ConfigureIdleTimeout(settings.ChatterboxKeepAliveMinutes);
                settings.Save();
                DialogResult = DialogResult.OK;
                Close();
            };
            content.Controls.Add(_save);
            FormClosing += (_, e) =>
            {
                if (_installation != null) { _installation.Cancel(); e.Cancel = true; }
            };
            UpdateStatus();
        }

        private Label Label(string text) => new Label { Text = text, AutoSize = true, MaximumSize = new Size(610, 0),
            ForeColor = Color.FromArgb(200, 214, 233), Margin = new Padding(0, 8, 0, 8) };
        private Button Button(string text, int width) => new Button { Text = text, Width = width, Height = 34,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(37, 75, 130), ForeColor = Color.White, Margin = new Padding(0, 3, 8, 3) };
        private NumericUpDown Number(decimal min, decimal max, decimal value) => new NumericUpDown
        { Minimum = min, Maximum = max, Value = value, DecimalPlaces = 2, Increment = 0.05m,
            Width = 72, BackColor = _input, ForeColor = Color.White, Margin = new Padding(0, 8, 8, 0) };

        private void UpdateStatus()
        {
            _status.Text = ChatterboxEngine.IsInstalled ? "● 本地模型已安装 · Chatterbox Multilingual V3 · PerTh 音频水印已启用"
                : "● 本地模型尚未安装 · 需要 64 位 Windows，NVIDIA GPU 推荐 8GB 显存";
            _status.ForeColor = ChatterboxEngine.IsInstalled ? Color.LightGreen : Color.Gold;
            _reference.Text = string.IsNullOrEmpty(_referencePath) ? "模型默认音色" : _referencePath;
        }

        private async Task InstallAsync()
        {
            if (_installation != null) return;
            _installation = new CancellationTokenSource();
            _install.Enabled = _save.Enabled = false;
            var progress = new Progress<string>(line => { if (!IsDisposed) _log.AppendText(line + Environment.NewLine); });
            try
            {
                await Task.Run(() => ChatterboxEngine.InstallAsync(line => ((IProgress<string>)progress).Report(line), _installation.Token));
                _log.AppendText("本地配音环境已就绪。" + Environment.NewLine);
            }
            catch (OperationCanceledException) { _log.AppendText("已取消安装；下次可重新安装。" + Environment.NewLine); }
            catch (Exception ex) { _log.AppendText(ex.Message + Environment.NewLine); }
            finally
            {
                _installation.Dispose(); _installation = null;
                _install.Enabled = _save.Enabled = true;
                UpdateStatus();
            }
        }
    }
}
