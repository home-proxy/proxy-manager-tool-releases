using System.ComponentModel;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using CheckBoxState = System.Windows.Forms.VisualStyles.CheckBoxState;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class AddProxyForm : Form
{
    private const string SelectionColumnName = nameof(BulkProxyRow.IsSelected);
    private const string ActionsColumnName = nameof(BulkProxyRow.Actions);
    private const string UncheckedStatusText = "Chưa check";
    private const string CheckingStatusText = "Đang kiểm tra...";
    private const int FooterHeight = 75;
    private const int InputTextAreaHeight = 250;

    private static readonly Color FooterBackground = Color.FromArgb(247, 247, 249);
    private static readonly Color HeaderBorder = Color.FromArgb(224, 224, 224);

    private readonly IProxyChecker _proxyChecker;
    private readonly IBulkProxyParser _parser;
    private readonly RichTextBox _bulkInput = new();
    private readonly DataGridView _bulkGrid = new AppDataGridView();
    private readonly AnalysisSummaryBar _analysisSummaryBar = new();
    private readonly Label _selectionSummaryLabel = new() { AutoSize = true, ForeColor = LightTheme.Muted };
    private readonly System.Windows.Forms.Timer _parseDebounceTimer = new() { Interval = 450 };
    private BindingList<BulkProxyRow> _bulkRows = [];
    private bool _updatingBulkInput;
    private bool _closing;

    public AddProxyForm(IProxyChecker proxyChecker, IBulkProxyParser parser)
    {
        _proxyChecker = proxyChecker;
        _parser = parser;

        Text = "Thêm Proxy";
        Width = 1750;
        Height = 940;
        MinimumSize = new Size(1180, 700);
        MinimizeBox = false;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(BuildLayout());
        LightTheme.Apply(this);
        RestoreCustomColors(this);
    }

    public IReadOnlyList<ProxyServer> ImportedProxies { get; private set; } = [];

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closing = true;
        _parseDebounceTimer.Stop();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _parseDebounceTimer.Stop();
        base.OnFormClosed(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Enter) && _bulkInput.ContainsFocus)
        {
            AddBulkProxies(selectedOnly: false);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.White,
            Padding = new Padding(8, 12, 8, 8)
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, InputTextAreaHeight));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        ConfigureBulkInput();
        ConfigureBulkGrid();
        content.Controls.Add(BuildInputHeader(), 0, 0);
        content.Controls.Add(_bulkInput, 0, 1);
        content.Controls.Add(BuildAnalysisHeader(), 0, 2);
        content.Controls.Add(BuildGridHeader(), 0, 3);
        content.Controls.Add(_bulkGrid, 0, 4);

        root.Controls.Add(content, 0, 0);
        root.Controls.Add(CreateFooter(), 0, 1);
        return root;
    }

    private Control BuildInputHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var labels = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        labels.Controls.Add(new Label
        {
            Text = "Dán danh sách Proxy",
            AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            Margin = Padding.Empty
        });
        labels.Controls.Add(new Label
        {
            Text = "Hỗ trợ: IP:Port hoặc IP:Port:Username:Password:[HTTP/SOCKS5]",
            AutoSize = true,
            ForeColor = LightTheme.Muted,
            Margin = new Padding(0, 4, 0, 8)
        });
        header.Controls.Add(labels, 0, 0);
        header.Controls.Add(BuildProtocolToolbar(), 1, 0);
        return header;
    }

    private Control BuildProtocolToolbar()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White,
            Margin = new Padding(0, 12, 0, 0)
        };
        row.Controls.Add(new Label
        {
            Text = "Giao thức mặc định",
            AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            Margin = new Padding(0, 9, 10, 0)
        });
        row.Controls.Add(CreateSegmentButton("Auto", (_, _) => AnalyzeBulkInput(allowWhileGridFocused: true), selected: true));
        row.Controls.Add(CreateSegmentButton("HTTP", (_, _) => ApplyProtocolToBulkLines(ProxyProtocol.Https), selected: false));
        row.Controls.Add(CreateSegmentButton("SOCKS5", (_, _) => ApplyProtocolToBulkLines(ProxyProtocol.Socks5), selected: false));
        return row;
    }

    private static Button CreateSegmentButton(string text, EventHandler onClick, bool selected)
    {
        var button = new AppButton
        {
            Text = text,
            Height = 34,
            MinimumSize = new Size(text == "SOCKS5" ? 76 : 56, 34),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            Variant = selected ? AppButtonVariant.Secondary : AppButtonVariant.Muted,
            AccentColor = selected ? LightTheme.Accent : LightTheme.Muted,
            Margin = new Padding(0, 0, 4, 0)
        };
        button.Click += onClick;
        return button;
    }

    private Control BuildAnalysisHeader()
    {
        _analysisSummaryBar.Dock = DockStyle.Top;
        _analysisSummaryBar.Height = 56;
        _analysisSummaryBar.Margin = new Padding(0, 10, 0, 6);
        return _analysisSummaryBar;
    }

    private Control BuildGridHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = new Padding(0, 8, 0, 4)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var labels = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        labels.Controls.Add(new Label
        {
            Text = "Danh sách Proxy đã nhập",
            AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            Margin = Padding.Empty
        });
        _selectionSummaryLabel.Margin = new Padding(0, 4, 0, 0);
        _selectionSummaryLabel.Text = "Đã chọn 0 Proxy";
        labels.Controls.Add(_selectionSummaryLabel);

        var checkButton = new DesignedToolbarButton
        {
            Text = "Check proxy",
            Image = UiIcons.NewCheckProxyDisabled,
            HoverImage = UiIcons.NewCheckProxyHover,
            DisabledImage = UiIcons.NewCheckProxyDisabled,
            RestTextColor = Color.FromArgb(189, 189, 189),
            HoverTextColor = DesignedGridTheme.AccentColor,
            DisabledTextColor = Color.FromArgb(189, 189, 189),
            MinimumSize = new Size(132, 40),
            Height = 40,
            Padding = new Padding(6),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty
        };
        checkButton.Click += async (_, _) => await CheckSelectedBulkRowsAsync();

        header.Controls.Add(labels, 0, 0);
        header.Controls.Add(checkButton, 1, 0);
        return header;
    }

    private Control CreateFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FooterBackground,
            Padding = new Padding(8, 12, 8, 12),
            Tag = FooterBackground
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(HeaderBorder);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = FooterBackground,
            Tag = FooterBackground,
            Margin = Padding.Empty
        };
        var addSelected = new AppButton
        {
            Text = "Thêm đã chọn",
            Height = 40,
            MinimumSize = new Size(134, 40),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Variant = AppButtonVariant.Secondary,
            AccentColor = LightTheme.Accent,
            Margin = new Padding(0, 0, 10, 0)
        };
        addSelected.Click += (_, _) => AddBulkProxies(selectedOnly: true);
        var addAll = new AppPrimaryButton
        {
            Text = "Thêm tất cả",
            Height = 40,
            MinimumSize = new Size(126, 40),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Margin = new Padding(0, 0, 10, 0)
        };
        addAll.Click += (_, _) => AddBulkProxies(selectedOnly: false);
        var cancel = new AppButton
        {
            Text = "Hủy",
            Height = 40,
            MinimumSize = new Size(64, 40),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Variant = AppButtonVariant.Muted,
            AccentColor = LightTheme.Muted,
            Margin = Padding.Empty
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(addSelected);
        buttons.Controls.Add(addAll);
        buttons.Controls.Add(cancel);
        footer.Controls.Add(buttons);
        AcceptButton = addAll;
        CancelButton = cancel;
        return footer;
    }

    private void ConfigureBulkInput()
    {
        _bulkInput.Dock = DockStyle.Fill;
        _bulkInput.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        _bulkInput.WordWrap = false;
        _bulkInput.AcceptsTab = true;
        _bulkInput.BorderStyle = BorderStyle.FixedSingle;
        _bulkInput.TextChanged += (_, _) =>
        {
            if (_updatingBulkInput || _closing)
            {
                return;
            }

            _parseDebounceTimer.Stop();
            _parseDebounceTimer.Start();
        };
        _parseDebounceTimer.Tick += (_, _) =>
        {
            _parseDebounceTimer.Stop();
            if (!_closing && !IsDisposed && !Disposing && !_bulkInput.IsDisposed)
            {
                AnalyzeBulkInput();
            }
        };
    }

    private void ConfigureBulkGrid()
    {
        _bulkGrid.Dock = DockStyle.Fill;
        _bulkGrid.AllowUserToAddRows = false;
        _bulkGrid.AllowUserToDeleteRows = false;
        _bulkGrid.AutoGenerateColumns = false;
        _bulkGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _bulkGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _bulkGrid.MultiSelect = true;
        _bulkGrid.EditMode = DataGridViewEditMode.EditProgrammatically;
        _bulkGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_bulkGrid.IsCurrentCellDirty)
            {
                _bulkGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _bulkGrid.CellClick += HandleBulkGridCellClick;
        _bulkGrid.CellDoubleClick += (_, e) => BeginBulkEdit(e.RowIndex, e.ColumnIndex);
        _bulkGrid.CellEndEdit += HandleBulkCellEndEdit;
        _bulkGrid.CellPainting += PaintSelectionHeaderCell;
        _bulkGrid.CellPainting += PaintBulkEditableCellIcon;
        _bulkGrid.CellPainting += PaintBulkStatusCell;
        _bulkGrid.CellPainting += PaintBulkActionCell;
        _bulkGrid.ColumnHeaderMouseClick += HandleBulkColumnHeaderMouseClick;
        _bulkGrid.DataError += (_, _) => { };
        _bulkGrid.RowPrePaint += (_, e) =>
        {
            if (_bulkGrid.Rows[e.RowIndex].DataBoundItem is BulkProxyRow { IsValid: false })
            {
                _bulkGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);
                _bulkGrid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = LightTheme.Danger;
            }
        };

        _bulkGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = SelectionColumnName,
            HeaderText = string.Empty,
            DataPropertyName = nameof(BulkProxyRow.IsSelected),
            MinimumWidth = 42,
            FillWeight = 4,
            ReadOnly = true
        });
        _bulkGrid.Columns.Add(CreateBulkColumn("Proxy", nameof(BulkProxyRow.ProxyDisplay), 180, 20, readOnly: true));
        _bulkGrid.Columns.Add(CreateBulkColumn("User name", nameof(BulkProxyRow.Username), 120, 13, readOnly: false));
        _bulkGrid.Columns.Add(CreateBulkColumn("Password", nameof(BulkProxyRow.Password), 120, 13, readOnly: false));
        _bulkGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = nameof(BulkProxyRow.Protocol),
            HeaderText = "Giao thức",
            DataPropertyName = nameof(BulkProxyRow.Protocol),
            MinimumWidth = 110,
            FillWeight = 10,
            DataSource = ProxyProtocolDisplay.Options.ToList(),
            DisplayMember = nameof(ProxyProtocolOption.DisplayName),
            ValueMember = nameof(ProxyProtocolOption.Value),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
        });
        _bulkGrid.Columns.Add(CreateBulkColumn("Trạng thái", nameof(BulkProxyRow.Status), 132, 12, readOnly: true));
        _bulkGrid.Columns.Add(CreateBulkColumn("Độ trễ (ms)", nameof(BulkProxyRow.LatencyMs), 90, 8, readOnly: true));
        _bulkGrid.Columns.Add(CreateBulkColumn("Lỗi", nameof(BulkProxyRow.ErrorMessage), 120, 12, readOnly: true));
        _bulkGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ActionsColumnName,
            HeaderText = "Hành động",
            DataPropertyName = nameof(BulkProxyRow.Actions),
            MinimumWidth = 74,
            FillWeight = 7,
            ReadOnly = true
        });
        foreach (var columnName in new[] { nameof(BulkProxyRow.Username), nameof(BulkProxyRow.Password) })
        {
            if (_bulkGrid.Columns[columnName] is { } column)
            {
                column.DefaultCellStyle.Padding = new Padding(4, 0, 28, 0);
            }
        }
    }

    private static DataGridViewTextBoxColumn CreateBulkColumn(string header, string propertyName, int minimumWidth, float fillWeight, bool readOnly) =>
        new()
        {
            Name = propertyName,
            HeaderText = header,
            DataPropertyName = propertyName,
            MinimumWidth = minimumWidth,
            FillWeight = fillWeight,
            ReadOnly = readOnly
        };

    private void AnalyzeBulkInput(bool allowWhileGridFocused = false)
    {
        if (_closing || IsDisposed || Disposing || _bulkInput.IsDisposed)
        {
            return;
        }

        if (!allowWhileGridFocused && _bulkGrid.ContainsFocus)
        {
            return;
        }

        var rows = new List<BulkProxyRow>();
        var errors = new List<ProxyImportError>();
        var lines = GetBulkLines();
        var previousSelectionsByLine = _bulkRows
            .Where(row => row.IsValid)
            .GroupBy(row => row.LineNumber)
            .ToDictionary(group => group.Key, group => group.Last().IsSelected);
        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var result = _parser.Parse(rawLine);
            if (result.Proxies.Count == 1 && result.Errors.Count == 0)
            {
                var row = new BulkProxyRow(index + 1, rawLine, result.Proxies[0]);
                if (previousSelectionsByLine.TryGetValue(row.LineNumber, out var wasSelected))
                {
                    row.IsSelected = wasSelected;
                }

                rows.Add(row);
                continue;
            }

            var error = result.Errors.FirstOrDefault() ?? new ProxyImportError(index + 1, rawLine, "Định dạng proxy không hợp lệ.");
            errors.Add(error with { LineNumber = index + 1, RawLine = rawLine });
            rows.Add(new BulkProxyRow(index + 1, rawLine, errors[^1]));
        }

        _bulkRows = new BindingList<BulkProxyRow>(rows);
        _bulkGrid.DataSource = _bulkRows;
        ApplyBulkHighlights(errors);
        UpdateSummaries(rows.Count(row => row.IsValid), errors.Count);
    }

    private void UpdateSummaries(int validCount, int invalidCount)
    {
        var total = validCount + invalidCount;
        _analysisSummaryBar.SetCounts(total, validCount, invalidCount);
        _selectionSummaryLabel.Text = $"Đã chọn {_bulkRows.Count(row => row.IsValid && row.IsSelected)} Proxy";
    }

    private void AddBulkProxies(bool selectedOnly)
    {
        _parseDebounceTimer.Stop();
        AnalyzeBulkInput(allowWhileGridFocused: true);
        var proxies = _bulkRows
            .Where(row => row.IsValid && (!selectedOnly || row.IsSelected))
            .Select(row => row.ToProxyServer())
            .ToList();

        if (proxies.Count == 0)
        {
            MessageBox.Show(this, selectedOnly ? "Chưa chọn proxy hợp lệ nào." : "Không có proxy hợp lệ để thêm.", "Chưa có dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ImportedProxies = proxies;
        DialogResult = DialogResult.OK;
    }

    private void ApplyProtocolToBulkLines(ProxyProtocol protocol)
    {
        if (_closing || IsDisposed || Disposing || _bulkInput.IsDisposed)
        {
            return;
        }

        var token = protocol.ToDisplayName();
        var lines = GetBulkLines();
        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (trimmed.Contains("://", StringComparison.Ordinal) &&
                Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var builder = new UriBuilder(uri) { Scheme = token.ToLowerInvariant() };
                lines[index] = builder.Uri.ToString();
                continue;
            }

            var segments = trimmed.Split(':', StringSplitOptions.TrimEntries);
            if (segments.Length > 0 && ProxyProtocolDisplay.TryParse(segments[^1], out _))
            {
                segments[^1] = token;
                lines[index] = string.Join(':', segments);
                continue;
            }

            lines[index] = $"{trimmed}:{token}";
        }

        ReplaceBulkInputLines(lines, analyze: true);
    }

    private void HandleBulkGridCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _bulkGrid.Rows[e.RowIndex].DataBoundItem is not BulkProxyRow row)
        {
            return;
        }

        var column = _bulkGrid.Columns[e.ColumnIndex];
        if (column.Name == SelectionColumnName)
        {
            row.IsSelected = row.IsValid && !row.IsSelected;
            _bulkGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = row.IsSelected;
            _bulkGrid.InvalidateCell(e.ColumnIndex, e.RowIndex);
            UpdateSummaries(_bulkRows.Count(item => item.IsValid), _bulkRows.Count(item => !item.IsValid));
            return;
        }

        if (column.DataPropertyName == nameof(BulkProxyRow.Status) &&
            row.IsValid &&
            TryGetBulkStatusButtonBounds(e.RowIndex, e.ColumnIndex, out var statusButtonBounds) &&
            statusButtonBounds.Contains(_bulkGrid.PointToClient(Cursor.Position)))
        {
            _ = CheckBulkRowAsync(row);
            return;
        }

        if (column.Name == ActionsColumnName && TryGetBulkDeleteButton(e.RowIndex, e.ColumnIndex, out _))
        {
            DeleteBulkRow(row);
            return;
        }

        BeginBulkEdit(e.RowIndex, e.ColumnIndex);
    }

    private void HandleBulkColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 || _bulkGrid.Columns[e.ColumnIndex].Name != SelectionColumnName)
        {
            return;
        }

        var shouldSelectAll = _bulkRows.Any(row => row.IsValid && !row.IsSelected);
        foreach (var row in _bulkRows.Where(row => row.IsValid))
        {
            row.IsSelected = shouldSelectAll;
        }

        _bulkGrid.Refresh();
        UpdateSummaries(_bulkRows.Count(row => row.IsValid), _bulkRows.Count(row => !row.IsValid));
    }

    private void BeginBulkEdit(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            _bulkGrid.Columns[columnIndex].ReadOnly ||
            _bulkGrid.Columns[columnIndex].Name == SelectionColumnName ||
            _bulkGrid.Rows[rowIndex].DataBoundItem is not BulkProxyRow { IsValid: true })
        {
            return;
        }

        if (_bulkGrid.IsCurrentCellInEditMode && !_bulkGrid.EndEdit())
        {
            return;
        }

        _bulkGrid.CurrentCell = _bulkGrid.Rows[rowIndex].Cells[columnIndex];
        _bulkGrid.BeginEdit(selectAll: true);
    }

    private void HandleBulkCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _bulkGrid.Rows[e.RowIndex].DataBoundItem is not BulkProxyRow row || !row.IsValid)
        {
            return;
        }

        row.SyncProxyServerFromFields();
        RewriteBulkLine(row);
    }

    private async Task CheckBulkRowAsync(BulkProxyRow row)
    {
        row.Status = CheckingStatusText;
        row.LatencyMs = string.Empty;
        RefreshBulkRow(row);
        var result = await _proxyChecker.CheckAsync(row.ToProxyServer(), CancellationToken.None);
        row.Status = result.IsReachable ? ProxyStatus.Live.ToString() : ProxyStatus.Dead.ToString();
        row.LatencyMs = result.IsReachable ? FormatLatency(result.LatencyMs) : string.Empty;
        row.SyncProxyServerFromFields();
        RefreshBulkRow(row);
    }

    private async Task CheckSelectedBulkRowsAsync()
    {
        var selectedRows = _bulkRows
            .Where(row => row.IsValid && row.IsSelected)
            .ToList();
        if (selectedRows.Count == 0)
        {
            MessageBox.Show(this, "Hãy chọn ít nhất một proxy hợp lệ để check.", "Chưa chọn proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        foreach (var row in selectedRows)
        {
            row.Status = CheckingStatusText;
            row.LatencyMs = string.Empty;
        }

        _bulkGrid.Refresh();
        foreach (var row in selectedRows)
        {
            var result = await _proxyChecker.CheckAsync(row.ToProxyServer(), CancellationToken.None);
            row.Status = result.IsReachable ? ProxyStatus.Live.ToString() : ProxyStatus.Dead.ToString();
            row.LatencyMs = result.IsReachable ? FormatLatency(result.LatencyMs) : string.Empty;
            row.SyncProxyServerFromFields();
            RefreshBulkRow(row);
        }
    }

    private void DeleteBulkRow(BulkProxyRow row)
    {
        var lines = GetBulkLines().ToList();
        var lineIndex = row.LineNumber - 1;
        if (lineIndex >= 0 && lineIndex < lines.Count)
        {
            lines.RemoveAt(lineIndex);
            ReplaceBulkInputLines(lines.ToArray(), analyze: true);
        }
    }

    private void RewriteBulkLine(BulkProxyRow row)
    {
        var lines = GetBulkLines();
        var lineIndex = row.LineNumber - 1;
        if (lineIndex < 0 || lineIndex >= lines.Length)
        {
            return;
        }

        lines[lineIndex] = row.BuildLine();
        ReplaceBulkInputLines(lines, analyze: true);
    }

    private void ReplaceBulkInputLines(string[] lines, bool analyze)
    {
        var selectionStart = !_bulkInput.IsDisposed ? _bulkInput.SelectionStart : 0;
        _updatingBulkInput = true;
        _bulkInput.Text = string.Join(Environment.NewLine, lines);
        RestoreBulkSelection(selectionStart, 0);
        _updatingBulkInput = false;
        if (analyze)
        {
            BeginInvoke(new Action(() => AnalyzeBulkInput(allowWhileGridFocused: true)));
        }
    }

    private string[] GetBulkLines() =>
        _bulkInput.Text.Replace("\r\n", "\n").Split('\n');

    private void ApplyBulkHighlights(IEnumerable<ProxyImportError> errors)
    {
        if (_closing || IsDisposed || Disposing || _bulkInput.IsDisposed)
        {
            return;
        }

        var selectionStart = _bulkInput.SelectionStart;
        var selectionLength = _bulkInput.SelectionLength;
        var hideSelection = _bulkInput.HideSelection;
        _bulkInput.SuspendLayout();
        try
        {
            _bulkInput.HideSelection = true;
            _bulkInput.SelectAll();
            _bulkInput.SelectionBackColor = Color.White;
            _bulkInput.SelectionColor = LightTheme.Foreground;
            foreach (var error in errors)
            {
                var lineIndex = error.LineNumber - 1;
                if (lineIndex < 0 || lineIndex >= _bulkInput.Lines.Length)
                {
                    continue;
                }

                var start = _bulkInput.GetFirstCharIndexFromLine(lineIndex);
                if (start < 0)
                {
                    continue;
                }

                _bulkInput.Select(start, _bulkInput.Lines[lineIndex].Length);
                _bulkInput.SelectionBackColor = Color.FromArgb(255, 205, 205);
                _bulkInput.SelectionColor = LightTheme.Danger;
            }
        }
        finally
        {
            RestoreBulkSelection(selectionStart, selectionLength);
            _bulkInput.HideSelection = hideSelection;
            _bulkInput.ResumeLayout();
        }
    }

    private void RestoreBulkSelection(int selectionStart, int selectionLength)
    {
        if (_bulkInput.IsDisposed)
        {
            return;
        }

        var safeStart = Math.Min(Math.Max(0, selectionStart), _bulkInput.TextLength);
        var safeLength = Math.Min(Math.Max(0, selectionLength), _bulkInput.TextLength - safeStart);
        _bulkInput.Select(safeStart, safeLength);
    }

    private void RefreshBulkRow(BulkProxyRow row)
    {
        var index = _bulkRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        _bulkGrid.InvalidateRow(index);
        _bulkGrid.Refresh();
    }

    private void PaintSelectionHeaderCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 ||
            e.ColumnIndex < 0 ||
            _bulkGrid.Columns[e.ColumnIndex].Name != SelectionColumnName ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: true);
        var checkBoxSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, CheckBoxState.UncheckedNormal);
        var location = new Point(
            e.CellBounds.Left + (e.CellBounds.Width - checkBoxSize.Width) / 2,
            e.CellBounds.Top + (e.CellBounds.Height - checkBoxSize.Height) / 2);
        CheckBoxRenderer.DrawCheckBox(e.Graphics, location, GetSelectionHeaderState());
        e.Handled = true;
    }

    private CheckBoxState GetSelectionHeaderState()
    {
        var validRows = _bulkRows.Where(row => row.IsValid).ToList();
        if (validRows.Count == 0 || validRows.All(row => !row.IsSelected))
        {
            return CheckBoxState.UncheckedNormal;
        }

        return validRows.All(row => row.IsSelected)
            ? CheckBoxState.CheckedNormal
            : CheckBoxState.MixedNormal;
    }

    private void PaintBulkEditableCellIcon(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _bulkGrid.Rows[e.RowIndex].DataBoundItem is not BulkProxyRow { IsValid: true } ||
            _bulkGrid.Columns[e.ColumnIndex].ReadOnly ||
            _bulkGrid.Columns[e.ColumnIndex].Name == SelectionColumnName ||
            !IsBulkInlineEditCueColumn(_bulkGrid.Columns[e.ColumnIndex]) ||
            e.Graphics is null)
        {
            return;
        }

        e.Paint(e.CellBounds, e.PaintParts);
        DrawInlineEditIcon(e.Graphics, e.CellBounds);
        e.Handled = true;
    }

    private void PaintBulkStatusCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _bulkGrid.Columns[e.ColumnIndex].DataPropertyName != nameof(BulkProxyRow.Status) ||
            _bulkGrid.Rows[e.RowIndex].DataBoundItem is not BulkProxyRow { IsValid: true } row ||
            row.Status != UncheckedStatusText ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var buttonBounds = GetStatusCheckButtonBounds(e.CellBounds);
        var textBounds = new Rectangle(e.CellBounds.Left + 8, e.CellBounds.Top, Math.Max(0, buttonBounds.Left - e.CellBounds.Left - 12), e.CellBounds.Height);
        TextRenderer.DrawText(e.Graphics, row.Status, _bulkGrid.Font, textBounds, e.CellStyle?.ForeColor ?? _bulkGrid.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        GridCellButtonRenderer.DrawOutlineButton(e.Graphics, buttonBounds, "Check");
        e.Handled = true;
    }

    private void PaintBulkActionCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _bulkGrid.Columns[e.ColumnIndex].Name != ActionsColumnName ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        DesignedGridTheme.DrawGrayIconButton(e.Graphics, GetBulkDeleteButtonBounds(e.CellBounds), UiIcons.NewRemove);
        e.Handled = true;
    }

    private bool TryGetBulkStatusButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _bulkGrid.Rows.Count ||
            _bulkGrid.Rows[rowIndex].DataBoundItem is not BulkProxyRow { IsValid: true, Status: UncheckedStatusText })
        {
            return false;
        }

        bounds = GetStatusCheckButtonBounds(_bulkGrid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private bool TryGetBulkDeleteButton(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _bulkGrid.Rows.Count ||
            _bulkGrid.Columns[columnIndex].Name != ActionsColumnName)
        {
            return false;
        }

        bounds = GetBulkDeleteButtonBounds(_bulkGrid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return bounds.Contains(_bulkGrid.PointToClient(Cursor.Position));
    }

    private static Rectangle GetStatusCheckButtonBounds(Rectangle cellBounds) =>
        GridCellButtonRenderer.GetRightAlignedButtonBounds(cellBounds, "Check");

    private static Rectangle GetBulkDeleteButtonBounds(Rectangle cellBounds) =>
        GridCellButtonRenderer.GetCenteredIconButtonBounds(cellBounds, 1, DesignedGridTheme.IconButtonSize, 4).Single();

    private static bool IsBulkInlineEditCueColumn(DataGridViewColumn column) =>
        column.DataPropertyName is nameof(BulkProxyRow.Username) or nameof(BulkProxyRow.Password);

    private static void DrawInlineEditIcon(Graphics graphics, Rectangle cellBounds)
    {
        var iconBounds = new Rectangle(cellBounds.Right - 24, cellBounds.Top + ((cellBounds.Height - 18) / 2), 18, 18);
        graphics.DrawImage(UiIcons.NewInlineEdit, iconBounds);
    }

    private static string? NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatLatency(int? latencyMs) =>
        latencyMs is null ? string.Empty : $"{latencyMs.Value} ms";

    private static void RestoreCustomColors(Control control)
    {
        if (control.Tag is Color color)
        {
            control.BackColor = color;
        }

        foreach (Control child in control.Controls)
        {
            RestoreCustomColors(child);
        }
    }

    private sealed class AnalysisSummaryBar : Control
    {
        private int _total;
        private int _valid;
        private int _invalid;

        public AnalysisSummaryBar()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 11f, FontStyle.Regular);
        }

        public void SetCounts(int total, int valid, int invalid)
        {
            _total = total;
            _valid = valid;
            _invalid = invalid;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.White);
            using var borderPen = new Pen(HeaderBorder);
            e.Graphics.DrawLine(borderPen, 0, 0, Width, 0);
            e.Graphics.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);

            TextRenderer.DrawText(
                e.Graphics,
                "Kết quả phân tích",
                Font,
                new Rectangle(0, 0, Math.Max(0, Width / 3), Height),
                Color.FromArgb(36, 36, 36),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var right = Width - 4;
            DrawStat(e.Graphics, ref right, "Không hợp lệ:", _invalid.ToString("00"), Color.FromArgb(253, 231, 233), Color.FromArgb(197, 15, 31));
            DrawStat(e.Graphics, ref right, "Hợp lệ:", _valid.ToString("00"), Color.FromArgb(223, 246, 221), Color.FromArgb(16, 124, 16));
            DrawStat(e.Graphics, ref right, "Tổng:", $"{_total} dòng", Color.White, Color.FromArgb(36, 36, 36), border: true);
        }

        private void DrawStat(Graphics graphics, ref int right, string label, string value, Color backColor, Color foreColor, bool border = false)
        {
            var labelSize = TextRenderer.MeasureText(graphics, label, Font, Size.Empty, TextFormatFlags.NoPadding);
            var valueSize = TextRenderer.MeasureText(graphics, value, Font, Size.Empty, TextFormatFlags.NoPadding);
            var badgeWidth = valueSize.Width + 14;
            var badgeBounds = new Rectangle(
                right - badgeWidth,
                (Height - 32) / 2,
                badgeWidth,
                32);
            var labelBounds = new Rectangle(
                badgeBounds.Left - labelSize.Width - 8,
                0,
                labelSize.Width + 2,
                Height);

            TextRenderer.DrawText(
                graphics,
                label,
                Font,
                labelBounds,
                LightTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = AppButton.CreateRoundedRectangle(badgeBounds, 5))
            using (var background = new SolidBrush(backColor))
            {
                graphics.FillPath(background, path);
                if (border)
                {
                    using var pen = new Pen(HeaderBorder);
                    graphics.DrawPath(pen, path);
                }
            }

            TextRenderer.DrawText(
                graphics,
                value,
                Font,
                badgeBounds,
                foreColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            right = labelBounds.Left - 52;
        }
    }

    private sealed class BulkProxyRow
    {
        public BulkProxyRow(int lineNumber, string rawLine, ProxyServer proxyServer)
        {
            LineNumber = lineNumber;
            RawLine = rawLine;
            ProxyServer = proxyServer;
            IsSelected = true;
            IsValid = true;
            Proxy = proxyServer.Proxy;
            Username = proxyServer.Username ?? string.Empty;
            Password = proxyServer.Password ?? string.Empty;
            Protocol = proxyServer.Protocol;
            Status = proxyServer.Status == ProxyStatus.Unknown ? UncheckedStatusText : proxyServer.Status.ToString();
            LatencyMs = FormatLatency(proxyServer.LatencyMs);
            ErrorMessage = string.Empty;
        }

        public BulkProxyRow(int lineNumber, string rawLine, ProxyImportError error)
        {
            LineNumber = lineNumber;
            RawLine = rawLine;
            IsSelected = false;
            IsValid = false;
            Proxy = error.RawLine;
            Status = "Lỗi";
            ErrorMessage = error.Message;
        }

        public int LineNumber { get; }
        public string RawLine { get; }
        public ProxyServer? ProxyServer { get; private set; }
        public bool IsSelected { get; set; }
        public bool IsValid { get; }
        public string Proxy { get; set; } = string.Empty;
        public string ProxyDisplay => string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password)
            ? Proxy
            : $"{Proxy}:{Username}:{Password}";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public ProxyProtocol Protocol { get; set; } = ProxyProtocol.Socks5;
        public string Status { get; set; } = string.Empty;
        public string LatencyMs { get; set; } = string.Empty;
        public string ErrorMessage { get; } = string.Empty;
        public string Actions => string.Empty;

        public ProxyServer ToProxyServer()
        {
            SyncProxyServerFromFields();
            return ProxyServer!;
        }

        public void SyncProxyServerFromFields()
        {
            if (!IsValid)
            {
                return;
            }

            ProxyServer ??= new ProxyServer();
            ProxyServer.Proxy = Proxy.Trim();
            ProxyServer.Username = NormalizeOptional(Username);
            ProxyServer.Password = NormalizeOptional(Password);
            ProxyServer.Protocol = Protocol;
            ProxyServer.Status = Enum.TryParse<ProxyStatus>(Status, ignoreCase: true, out var status) ? status : ProxyStatus.Unknown;
            ProxyServer.LatencyMs = TryParseLatency(LatencyMs);
        }

        public string BuildLine()
        {
            var token = Protocol.ToDisplayName();
            return string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password)
                ? $"{Proxy.Trim()}:{token}"
                : $"{Proxy.Trim()}:{Username.Trim()}:{Password.Trim()}:{token}";
        }

        private static int? TryParseLatency(string value)
        {
            var normalized = value.Replace("ms", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            return int.TryParse(normalized, out var latency) ? latency : null;
        }
    }
}
