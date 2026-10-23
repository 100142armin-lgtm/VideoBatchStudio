using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoBatchMerger.ScriptBatch
{
	internal sealed class ElevenLabsKeyDialog : Form
	{
		private readonly ElevenLabsKeyPool _pool;
		private TextBox _txtBatchInput;
		private DataGridView _grid;
		private Label _lblSummary;
		private Button _btnCheckAll;
		private Button _btnSave;
		private Button _btnClearAll;
		private ProgressBar _progress;

		private static readonly Color DarkBg = Color.FromArgb(15, 23, 42);
		private static readonly Color CardBg = Color.FromArgb(30, 41, 59);
		private static readonly Color GridBg = Color.FromArgb(20, 27, 45);
		private static readonly Color TextWhite = Color.FromArgb(248, 250, 252);
		private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
		private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
		private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
		private static readonly Color AccentRed = Color.FromArgb(239, 68, 68);
		private static readonly Color AccentYellow = Color.FromArgb(245, 158, 11);

		public ElevenLabsKeyDialog()
		{
			_pool = ElevenLabsKeyPool.Instance;

			Text = "🔑 ElevenLabs 多密钥管理中心 (智能轮询 · 故障转移 · 状态检测)";
			Size = new Size(880, 620);
			MinimumSize = new Size(760, 500);
			StartPosition = FormStartPosition.CenterParent;
			BackColor = DarkBg;
			ForeColor = TextWhite;
			Font = new Font("Microsoft YaHei UI", 9f);
			ShowIcon = false;

			InitializeComponents();
			RefreshGrid();
			UpdateSummary();
		}

		private void InitializeComponents()
		{
			var mainLayout = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 1,
				RowCount = 4,
				Padding = new Padding(16)
			};
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100f)); // 顶部批量添加区
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // 中间密钥列表 DataGridView
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));   // 进度条与状态统计
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));   // 底部操作按钮栏

			// 1. 顶部批量添加区域
			var topBox = new GroupBox
			{
				Text = " ➕ 批量添加 ElevenLabs 免费/付费 API 密钥 ",
				Dock = DockStyle.Fill,
				ForeColor = AccentBlue,
				Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
				Padding = new Padding(10)
			};

			var topFlow = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 2,
				RowCount = 1
			};
			topFlow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 82f));
			topFlow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));

			_txtBatchInput = new TextBox
			{
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				Dock = DockStyle.Fill,
				BackColor = CardBg,
				ForeColor = TextWhite,
				Font = new Font("Consolas", 9f),
				BorderStyle = BorderStyle.FixedSingle
			};

			var btnAdd = new Button
			{
				Text = "➕ 批量导入\n到密钥池",
				Dock = DockStyle.Fill,
				BackColor = AccentBlue,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
				Cursor = Cursors.Hand
			};
			btnAdd.FlatAppearance.BorderSize = 0;
			btnAdd.Click += (_, _) =>
			{
				string text = _txtBatchInput.Text;
				if (string.IsNullOrWhiteSpace(text))
				{
					MessageBox.Show("请先在左侧输入框粘贴一个或多个 ElevenLabs API 密钥（支持一行一个）！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				int added = _pool.AddKeys(text);
				_txtBatchInput.Clear();
				RefreshGrid();
				UpdateSummary();
				MessageBox.Show($"成功导入 {added} 个新密钥！\n建议点击下方【🔍 一键检测全部可用性】按钮测试额度与状态。", "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
			};

			topFlow.Controls.Add(_txtBatchInput, 0, 0);
			topFlow.Controls.Add(btnAdd, 1, 0);
			topBox.Controls.Add(topFlow);
			mainLayout.Controls.Add(topBox, 0, 0);

			// 2. 中间表格
			_grid = new DataGridView
			{
				Dock = DockStyle.Fill,
				BackgroundColor = GridBg,
				ForeColor = TextWhite,
				GridColor = Color.FromArgb(51, 65, 85),
				BorderStyle = BorderStyle.None,
				RowHeadersVisible = false,
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				EnableHeadersVisualStyles = false,
				ColumnHeadersHeight = 36,
				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
				RowTemplate = { Height = 32 }
			};

			_grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
			_grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
			_grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
			_grid.DefaultCellStyle.BackColor = GridBg;
			_grid.DefaultCellStyle.ForeColor = TextWhite;
			_grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(59, 130, 246);
			_grid.DefaultCellStyle.SelectionForeColor = Color.White;

			var colId = new DataGridViewTextBoxColumn { HeaderText = "#", Width = 45, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
			var colKey = new DataGridViewTextBoxColumn { HeaderText = "密钥预览", Width = 160, ReadOnly = true };
			var colStatus = new DataGridViewTextBoxColumn { HeaderText = "状态指示", Width = 110, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } };
			var colRemain = new DataGridViewTextBoxColumn { HeaderText = "剩余可用字符", Width = 130, ReadOnly = true };
			var colTier = new DataGridViewTextBoxColumn { HeaderText = "账户类型", Width = 90, ReadOnly = true };
			var colDetail = new DataGridViewTextBoxColumn { HeaderText = "检测详情与提示", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true };
			var colAction = new DataGridViewButtonColumn { HeaderText = "操作", Width = 75, Text = "删除", UseColumnTextForButtonValue = true };

			_grid.Columns.AddRange(colId, colKey, colStatus, colRemain, colTier, colDetail, colAction);

			_grid.CellContentClick += (_, e) =>
			{
				if (e.RowIndex >= 0 && e.ColumnIndex == colAction.Index)
				{
					if (_grid.Rows[e.RowIndex].Tag is ElevenLabsKeyItem item)
					{
						if (MessageBox.Show($"确定要从密钥池中移除密钥 [{item.MaskedKey}] 吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
						{
							_pool.RemoveKey(item.ApiKey);
							RefreshGrid();
							UpdateSummary();
						}
					}
				}
			};

			mainLayout.Controls.Add(_grid, 0, 1);

			// 3. 统计与进度提示
			var statPanel = new Panel { Dock = DockStyle.Fill };
			_lblSummary = new Label
			{
				Dock = DockStyle.Left,
				AutoSize = true,
				ForeColor = TextMuted,
				Font = new Font("Microsoft YaHei UI", 9f),
				Text = "加载中..."
			};
			_progress = new ProgressBar
			{
				Dock = DockStyle.Right,
				Width = 240,
				Height = 18,
				Visible = false
			};
			statPanel.Controls.Add(_lblSummary);
			statPanel.Controls.Add(_progress);
			mainLayout.Controls.Add(statPanel, 0, 2);

			// 4. 底部操作栏
			var bottomFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.LeftToRight,
				Padding = new Padding(0, 8, 0, 0)
			};

			_btnCheckAll = CreateButton("🔍 一键检测全部可用性", AccentGreen, Color.White, 175);
			_btnCheckAll.Click += async (_, _) => await CheckAllKeysAsync();

			var btnCleanBlocked = CreateButton("🧹 一键清理风控/失效Key", Color.FromArgb(220, 38, 38), Color.White, 185);
			btnCleanBlocked.Click += (_, _) =>
			{
				int removed = _pool.Keys.RemoveAll(k => k.Status == "风控拦截" || k.Status == "失效");
				if (removed > 0)
				{
					_pool.Save();
					RefreshGrid();
					UpdateSummary();
					MessageBox.Show($"已为您自动清理 {removed} 个被官方风控限制或失效的密钥！\n当前密钥池将优先使用正常可用的 Key。", "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				else
				{
					MessageBox.Show("当前密钥池中没有检测到风控或失效的密钥。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			};

			_btnSave = CreateButton("💾 一键保存密钥池", AccentBlue, Color.White, 140);
			_btnSave.Click += (_, _) =>
			{
				_pool.Save();
				MessageBox.Show("密钥池已成功保存到本地！", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
			};

			_btnClearAll = CreateButton("🗑️ 清空所有密钥", Color.FromArgb(71, 85, 105), Color.White, 130);
			_btnClearAll.Click += (_, _) =>
			{
				if (MessageBox.Show("确定要清空密钥池中的全部密钥吗？此操作无法恢复。", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
				{
					_pool.ClearAll();
					RefreshGrid();
					UpdateSummary();
				}
			};

			var btnClose = CreateButton("✖️ 关闭窗口", CardBg, TextWhite, 100);
			btnClose.Click += (_, _) => Close();

			bottomFlow.Controls.Add(_btnCheckAll);
			bottomFlow.Controls.Add(btnCleanBlocked);
			bottomFlow.Controls.Add(_btnSave);
			bottomFlow.Controls.Add(_btnClearAll);
			bottomFlow.Controls.Add(btnClose);

			mainLayout.Controls.Add(bottomFlow, 0, 3);
			Controls.Add(mainLayout);
		}

		private Button CreateButton(string text, Color bg, Color fg, int width)
		{
			var btn = new Button
			{
				Text = text,
				BackColor = bg,
				ForeColor = fg,
				Width = width,
				Height = 36,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand,
				Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
				Margin = new Padding(0, 0, 10, 0)
			};
			btn.FlatAppearance.BorderSize = 0;
			return btn;
		}

		private void RefreshGrid()
		{
			_grid.Rows.Clear();
			int idx = 1;
			var primary = _pool.GetNextUsableKey();

			foreach (var item in _pool.Keys)
			{
				string statusIcon = "⚪ 未检测";
				Color statusColor = TextMuted;
				bool isPrimary = primary != null && string.Equals(primary.ApiKey, item.ApiKey, StringComparison.OrdinalIgnoreCase);

				if (item.Status == "有效")
				{
					statusIcon = isPrimary ? "⭐ 主用·正常" : "🟢 正常可用";
					statusColor = AccentGreen;
				}
				else if (item.Status == "风控拦截")
				{
					statusIcon = "🔴 官方风控";
					statusColor = Color.FromArgb(239, 68, 68);
				}
				else if (item.Status == "失效")
				{
					statusIcon = "🔴 密钥失效";
					statusColor = AccentRed;
				}
				else if (item.Status == "配额用尽")
				{
					statusIcon = "🟡 配额耗尽";
					statusColor = AccentYellow;
				}
				else if (item.Status == "网络错误" || item.Status == "检测异常")
				{
					statusIcon = "⚠️ 异常";
					statusColor = AccentYellow;
				}

				string remainStr = item.CharacterLimit > 0 ? $"{item.RemainingCharacters:N0} / {item.CharacterLimit:N0}" : "--";
				if (item.Status == "风控拦截")
				{
					remainStr = "0 / 10,000 (已受限)";
				}

				int r = _grid.Rows.Add(
					idx++,
					item.MaskedKey,
					statusIcon,
					remainStr,
					item.Tier.ToUpperInvariant(),
					item.StatusDetail
				);
				_grid.Rows[r].Tag = item;
				_grid.Rows[r].Cells[2].Style.ForeColor = statusColor;
				if (isPrimary)
				{
					_grid.Rows[r].DefaultCellStyle.BackColor = Color.FromArgb(17, 34, 60);
				}
			}
		}

		private void UpdateSummary()
		{
			int total = _pool.Keys.Count;
			int usable = _pool.Keys.Count(k => k.IsUsable);
			long totalRemain = _pool.Keys.Where(k => k.IsUsable).Sum(k => (long)k.RemainingCharacters);
			var primary = _pool.GetNextUsableKey();
			string activeHint = primary != null ? $"当前主用: [{primary.MaskedKey}] (剩余 {primary.RemainingCharacters:N0} 字符)" : "暂无可用有效密钥";

			_lblSummary.Text = $"📊 密钥池: 共 {total} 个  |  🟢 正常可用: {usable} 个  |  ✨ 累计可用额度: {totalRemain:N0} 字符  |  👉 【{activeHint}】";
		}

		private async Task CheckAllKeysAsync()
		{
			if (_pool.Keys.Count == 0)
			{
				MessageBox.Show("密钥池为空，请先粘贴添加 API 密钥！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			_btnCheckAll.Enabled = false;
			_progress.Visible = true;
			_progress.Minimum = 0;
			_progress.Maximum = _pool.Keys.Count;
			_progress.Value = 0;

			try
			{
				await _pool.CheckAllAsync((cur, total, keyItem) =>
				{
					if (InvokeRequired)
					{
						BeginInvoke(new Action(() =>
						{
							_progress.Value = cur;
							RefreshGrid();
							UpdateSummary();
						}));
					}
					else
					{
						_progress.Value = cur;
						RefreshGrid();
						UpdateSummary();
					}
				});

				RefreshGrid();
				UpdateSummary();
				MessageBox.Show("所有密钥检测完毕！详情请查看列表中各密钥的状态指示与剩余字符数。", "检测完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"检测过程出现异常: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			finally
			{
				_progress.Visible = false;
				_btnCheckAll.Enabled = true;
			}
		}
	}
}
