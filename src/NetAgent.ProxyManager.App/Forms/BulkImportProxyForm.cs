using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class BulkImportProxyForm : Form
{
    private const string CheckColumnName = "CheckProxy";

    private readonly IBulkProxyParser _parser;
    private readonly IProxyChecker _proxyChecker;
    private readonly LineNumberRichTextBox _input = new();
    private readonly Panel _lineNumberGutter = new() { Width = 44 };
    private readonly DataGridView _previewGrid = new AppDataGridView();
    private readonly Label _summaryLabel = new() { AutoSize = true };
    private List<ImportedProxyRow> _previewRows = [];
    private (int RowIndex, int ColumnIndex)? _pendingComboOpenCell;
    private bool _lastAnalysisHasErrors;

    public BulkImportProxyForm(IBulkProxyParser parser, IProxyChecker proxyChecker)
    {
        _parser = parser;
        _proxyChecker = proxyChecker;

        Text = "Nhập Proxy hàng loạt";
        Width = 1180;
        Height = 780;
        MinimumSize = new Size(1080, 700);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(BuildLayout());
        LightTheme.Apply(this);
        ConfigureLineNumberGutter();
    }

    public IReadOnlyList<ProxyServer> ImportedProxies { get; private set; } = [];

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 7
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(BuildSupportedFormatsPanel(), 0, 0);

        ConfigureInput();
        layout.Controls.Add(BuildInputEditor(), 0, 1);
        layout.Controls.Add(BuildProtocolToolbar(), 0, 2);

        layout.Controls.Add(new Label
        {
            Text = "Proxy hợp lệ sau khi phân tích",
            AutoSize = true
        }, 0, 3);

        ConfigurePreviewGrid();
        layout.Controls.Add(_previewGrid, 0, 4);

        layout.Controls.Add(_summaryLabel, 0, 5);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        var cancel = LightTheme.CreateToolbarButton("Hủy", (_, _) => DialogResult = DialogResult.Cancel, LightTheme.Muted);
        var import = LightTheme.CreateToolbarButton("Nhập proxy hợp lệ", (_, _) => Import(), LightTheme.Success);
        var analyze = LightTheme.CreateToolbarButton("Kiểm tra dữ liệu", (_, _) => AnalyzeInput(), LightTheme.Accent);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(import);
        buttons.Controls.Add(analyze);
        layout.Controls.Add(buttons, 0, 6);

        return layout;
    }

    private Control BuildSupportedFormatsPanel()
    {
        var group = new GroupBox
        {
            Text = "Định dạng được hỗ trợ",
            Dock = DockStyle.Fill,
            AutoSize = true
        };

        var formats = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(12),
            WrapContents = true
        };
        formats.Controls.Add(CreateFormatLabel("ip:port:HTTP"));
        formats.Controls.Add(CreateFormatLabel("ip:port:SOCKS5"));
        formats.Controls.Add(CreateFormatLabel("ip:port:user:pass:HTTP"));
        formats.Controls.Add(CreateFormatLabel("ip:port:user:pass:SOCKS5"));
        group.Controls.Add(formats);
        return group;
    }

    private static Label CreateFormatLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Consolas", 10, FontStyle.Bold),
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 0, 10, 6),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private void ConfigureInput()
    {
        _input.Dock = DockStyle.Fill;
        _input.Font = new Font("Consolas", 10);
        _input.WordWrap = false;
        _input.AcceptsTab = true;
        _input.TextChanged += (_, _) =>
        {
            ClearLineHighlights();
            UpdateLineNumberGutterWidth();
            _lineNumberGutter.Invalidate();
        };
        _input.ViewChanged += (_, _) => _lineNumberGutter.Invalidate();
    }

    private Control BuildInputEditor()
    {
        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.Controls.Add(_lineNumberGutter, 0, 0);
        editor.Controls.Add(_input, 1, 0);
        return editor;
    }

    private void ConfigureLineNumberGutter()
    {
        _lineNumberGutter.Dock = DockStyle.Fill;
        _lineNumberGutter.BackColor = LightTheme.SurfaceAlt;
        _lineNumberGutter.Paint += PaintLineNumbers;
        UpdateLineNumberGutterWidth();
    }

    private void ConfigurePreviewGrid()
    {
        _previewGrid.Dock = DockStyle.Fill;
        _previewGrid.ReadOnly = false;
        _previewGrid.AllowUserToAddRows = false;
        _previewGrid.AllowUserToDeleteRows = false;
        _previewGrid.MultiSelect = true;
        _previewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _previewGrid.AutoGenerateColumns = false;
        _previewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _previewGrid.EditMode = DataGridViewEditMode.EditProgrammatically;
        _previewGrid.CellPainting += PaintPreviewCheckCell;
        _previewGrid.CellClick += HandlePreviewCellClick;
        _previewGrid.EditingControlShowing += HandlePreviewEditingControlShowing;
        _previewGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_previewGrid.IsCurrentCellDirty)
            {
                _previewGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _previewGrid.CellValueChanged += (_, _) => _previewGrid.Refresh();
        _previewGrid.DataError += (_, _) => { };

        _previewGrid.Columns.Add(CreatePreviewColumn("Proxy", nameof(ImportedProxyRow.Proxy), 180, 22));
        _previewGrid.Columns.Add(CreatePreviewColumn("Tài khoản", nameof(ImportedProxyRow.Username), 120, 14));
        _previewGrid.Columns.Add(CreatePreviewColumn("Mật khẩu", nameof(ImportedProxyRow.Password), 120, 14));
        _previewGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "Giao thức",
            DataPropertyName = nameof(ImportedProxyRow.ProtocolValue),
            MinimumWidth = 110,
            FillWeight = 11,
            DataSource = ProxyProtocolDisplay.Options.ToList(),
            DisplayMember = nameof(ProxyProtocolOption.DisplayName),
            ValueMember = nameof(ProxyProtocolOption.Value)
        });
        _previewGrid.Columns.Add(CreatePreviewColumn("Trạng thái", nameof(ImportedProxyRow.Status), 110, 12));
        _previewGrid.Columns.Add(CreatePreviewColumn("Độ trễ (ms)", nameof(ImportedProxyRow.LatencyMs), 90, 10));
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = CheckColumnName,
            HeaderText = "Check",
            MinimumWidth = 90,
            FillWeight = 9,
            ReadOnly = true
        });
    }

    private static DataGridViewTextBoxColumn CreatePreviewColumn(string header, string propertyName, int minimumWidth, float fillWeight)
    {
        return new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = propertyName,
            MinimumWidth = minimumWidth,
            FillWeight = fillWeight,
            ReadOnly = true
        };
    }

    private Control BuildProtocolToolbar()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 8)
        };
        panel.Controls.Add(LightTheme.CreateToolbarButton("Gắn HTTP cho Proxy đã nhập", (_, _) => AppendProtocolToInputLines(ProxyProtocol.Https), LightTheme.Accent));
        panel.Controls.Add(LightTheme.CreateToolbarButton("Gắn SOCKS5 cho Proxy đã nhập", (_, _) => AppendProtocolToInputLines(ProxyProtocol.Socks5), LightTheme.Warning));
        return panel;
    }

    private void AnalyzeInput()
    {
        ClearLineHighlights();
        var result = _parser.Parse(_input.Text);
        _previewRows = result.Proxies.Select(proxy => new ImportedProxyRow(proxy)).ToList();
        _previewGrid.DataSource = _previewRows;
        _lastAnalysisHasErrors = result.HasErrors;

        if (result.HasErrors)
        {
            HighlightInvalidLines(result.Errors);
        }

        _summaryLabel.Text = $"Hợp lệ: {result.Proxies.Count} | Không hợp lệ: {result.Errors.Count}";
    }

    private void Import()
    {
        AnalyzeInput();
        if (!_previewRows.Any())
        {
            return;
        }

        if (_lastAnalysisHasErrors)
        {
            MessageBox.Show(this, "Hãy sửa các dòng không hợp lệ trước khi nhập.", "Dữ liệu chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ImportedProxies = _previewRows.Select(row => row.ProxyServer).ToList();
        DialogResult = DialogResult.OK;
    }

    private void AppendProtocolToInputLines(ProxyProtocol protocol)
    {
        var token = protocol.ToDisplayName();
        var lines = _input.Text.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();
            var segments = trimmed.Split(':', StringSplitOptions.TrimEntries);
            if (segments.Length > 0 && ProxyProtocolDisplay.TryParse(segments[^1], out _))
            {
                segments[^1] = token;
                lines[index] = string.Join(':', segments);
                continue;
            }

            lines[index] = $"{trimmed}:{token}";
        }

        _input.Text = string.Join(Environment.NewLine, lines);
        AnalyzeInput();
    }

    private void HandlePreviewCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 &&
            e.ColumnIndex >= 0 &&
            _previewGrid.Columns[e.ColumnIndex].Name == CheckColumnName &&
            GetPreviewCheckButtonBounds(_previewGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, cutOverflow: true))
                .Contains(_previewGrid.PointToClient(Cursor.Position)))
        {
            _ = HandlePreviewCellContentClickAsync(e);
            return;
        }

        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _previewGrid.Columns[e.ColumnIndex] is not DataGridViewComboBoxColumn)
        {
            return;
        }

        if (_previewGrid.IsCurrentCellInEditMode && !_previewGrid.EndEdit())
        {
            return;
        }

        _pendingComboOpenCell = (e.RowIndex, e.ColumnIndex);
        _previewGrid.CurrentCell = _previewGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        _previewGrid.BeginEdit(selectAll: true);
    }

    private void PaintPreviewCheckCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _previewGrid.Columns[e.ColumnIndex].Name != CheckColumnName ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false);
        GridCellButtonRenderer.DrawOutlineButton(e.Graphics, GetPreviewCheckButtonBounds(e.CellBounds), "Check");
        e.Handled = true;
    }

    private static Rectangle GetPreviewCheckButtonBounds(Rectangle cellBounds) =>
        GridCellButtonRenderer.GetCenteredButtonLayouts(cellBounds, ["Check"]).Single().Bounds;

    private async Task HandlePreviewCellContentClickAsync(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _previewGrid.Columns[e.ColumnIndex].Name != CheckColumnName ||
            _previewGrid.Rows[e.RowIndex].DataBoundItem is not ImportedProxyRow row)
        {
            return;
        }

        row.Status = ProxyStatus.Checking.ToString();
        row.LatencyMs = string.Empty;
        _previewGrid.Refresh();

        var result = await _proxyChecker.CheckAsync(row.ProxyServer, CancellationToken.None);
        row.Status = result.IsReachable ? ProxyStatus.Live.ToString() : ProxyStatus.Dead.ToString();
        row.LatencyMs = result.IsReachable ? result.LatencyMs?.ToString() ?? string.Empty : string.Empty;
        _previewGrid.Refresh();
    }

    private void HandlePreviewEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (_pendingComboOpenCell is not { } pendingCell ||
            _previewGrid.CurrentCell is null ||
            _previewGrid.CurrentCell.RowIndex != pendingCell.RowIndex ||
            _previewGrid.CurrentCell.ColumnIndex != pendingCell.ColumnIndex ||
            _previewGrid.CurrentCell.OwningColumn is not DataGridViewComboBoxColumn ||
            e.Control is not ComboBox comboBox)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (_previewGrid.CurrentCell?.RowIndex == pendingCell.RowIndex &&
                _previewGrid.CurrentCell.ColumnIndex == pendingCell.ColumnIndex &&
                _previewGrid.EditingControl == comboBox)
            {
                comboBox.DroppedDown = true;
            }
        }));
        _pendingComboOpenCell = null;
    }

    private void ClearLineHighlights()
    {
        var currentSelectionStart = _input.SelectionStart;
        var currentSelectionLength = _input.SelectionLength;
        _input.SelectAll();
        _input.SelectionBackColor = LightTheme.Surface;
        _input.SelectionColor = LightTheme.Foreground;
        _input.Select(currentSelectionStart, currentSelectionLength);
    }

    private void HighlightInvalidLines(IEnumerable<ProxyImportError> errors)
    {
        foreach (var error in errors)
        {
            var lineIndex = error.LineNumber - 1;
            var start = _input.GetFirstCharIndexFromLine(lineIndex);
            if (start < 0 || lineIndex >= _input.Lines.Length)
            {
                continue;
            }

            var lineLength = _input.Lines[lineIndex].Length;
            _input.Select(start, lineLength);
            _input.SelectionBackColor = Color.FromArgb(255, 205, 205);
            _input.SelectionColor = LightTheme.Danger;
        }

        _input.Select(0, 0);
    }

    private void PaintLineNumbers(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(LightTheme.SurfaceAlt);
        using var brush = new SolidBrush(LightTheme.Muted);
        var firstCharIndex = _input.GetCharIndexFromPosition(Point.Empty);
        var firstLine = _input.GetLineFromCharIndex(firstCharIndex);
        var lineCount = Math.Max(_input.Lines.Length, 1);

        for (var line = firstLine; line < lineCount; line++)
        {
            var charIndex = _input.GetFirstCharIndexFromLine(line);
            if (charIndex < 0)
            {
                continue;
            }

            var position = _input.GetPositionFromCharIndex(charIndex);
            if (position.Y > _input.Height)
            {
                break;
            }

            var bounds = new Rectangle(0, position.Y + 2, _lineNumberGutter.Width - 6, _input.Font.Height);
            TextRenderer.DrawText(
                e.Graphics,
                (line + 1).ToString(),
                _input.Font,
                bounds,
                LightTheme.Muted,
                TextFormatFlags.Right | TextFormatFlags.NoPadding);
        }
    }

    private void UpdateLineNumberGutterWidth()
    {
        var digits = Math.Max(2, Math.Max(_input.Lines.Length, 1).ToString().Length);
        _lineNumberGutter.Width = 18 + digits * 10;
    }

    private sealed class ImportedProxyRow
    {
        public ImportedProxyRow(ProxyServer proxyServer)
        {
            ProxyServer = proxyServer;
            Status = proxyServer.Status.ToString();
            LatencyMs = proxyServer.LatencyMs?.ToString() ?? string.Empty;
        }

        public ProxyServer ProxyServer { get; }
        public string Proxy => ProxyServer.Proxy;
        public string Username => ProxyServer.Username ?? "-";
        public string Password => ProxyServer.Password ?? "-";
        public ProxyProtocol ProtocolValue
        {
            get => ProxyServer.Protocol;
            set => ProxyServer.Protocol = value;
        }

        public string Status { get; set; }
        public string LatencyMs { get; set; }
    }

    private sealed class LineNumberRichTextBox : RichTextBox
    {
        private const int WmVScroll = 0x0115;
        private const int WmMouseWheel = 0x020A;
        private const int WmKeyUp = 0x0101;
        private const int WmSize = 0x0005;

        public event EventHandler? ViewChanged;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg is WmVScroll or WmMouseWheel or WmKeyUp or WmSize)
            {
                ViewChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
