using System.ComponentModel;
using System.Globalization;
using System.Net;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Forms;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Controls;

internal sealed class HistoryListControl : UserControl
{
    private const int DefaultPage = 1;
    private const int DefaultLimit = 10;
    private const string StatusColumnName = nameof(HistoryGridRow.Status);

    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly int[] PageSizeOptions = [10, 20, 50];

    private readonly IProxyOrderApiClient _proxyOrderApiClient;
    private readonly IAuthService _authService;
    private readonly HistoryListKind _kind;
    private readonly DataGridView _grid = new AppDataGridView();
    private readonly ComboBox _searchFieldCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly AppTextInput _searchTextBox = new() { Width = 260 };
    private readonly ComboBox _statusCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _columnVisibilityCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _pageSizeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
    private readonly AppClearFilterButton _clearFilterButton = new();
    private readonly Label _pageLabel = new();
    private readonly Label _statusLabel = AppDataGridFooter.CreateStatusLabel();
    private readonly Button _previousPageButton;
    private readonly Button _nextPageButton;
    private readonly System.Windows.Forms.Timer _searchDebounceTimer = new() { Interval = 900 };
    private readonly BindingList<HistoryGridRow> _rows = [];
    private readonly List<SearchFieldOption> _searchFields;
    private readonly List<HistoryColumnSpec> _columns;

    private TableLayoutPanel _bottomBar = null!;
    private int _page = DefaultPage;
    private int _limit = DefaultLimit;
    private int? _total;
    private bool _hasNextPage;
    private bool _isBusy;
    private bool _requiresLogin;
    private bool _loaded;
    private bool _isSettingFilters;
    private string _emptyStateText = "Không tìm thấy dữ liệu";
    private string _emptyStateButtonText = string.Empty;
    private Action _emptyStateButtonClick = static () => { };

    public HistoryListControl(
        IProxyOrderApiClient proxyOrderApiClient,
        IAuthService authService,
        HistoryListKind kind)
    {
        _proxyOrderApiClient = proxyOrderApiClient;
        _authService = authService;
        _kind = kind;
        _searchFields = CreateSearchFields(kind);
        _columns = CreateColumns(kind);
        _previousPageButton = AppDataGridFooter.CreatePagerButton(previous: true, async (_, _) => await MovePageAsync(-1));
        _nextPageButton = AppDataGridFooter.CreatePagerButton(previous: false, async (_, _) => await MovePageAsync(1));

        Dock = DockStyle.Fill;
        BackColor = LightTheme.Background;
        BuildUi();
        LightTheme.Apply(this);
        _grid.CellPainting += PaintStatusCell;
    }

    public Func<Task<bool>>? LoginRequestedAsync { get; set; }

    public bool HasLoaded => _loaded;

    public async Task LoadAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _loaded)
        {
            return;
        }

        await FetchPageAsync();
    }

    public async Task RefreshAsync()
    {
        _page = DefaultPage;
        await FetchPageAsync();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = LightTheme.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildToolbar(), 0, 0);

        ConfigureGrid();
        RefreshColumnVisibilityCombo();
        _grid.DataSource = _rows;
        _ = new GridEmptyStateOverlay(_grid, GetEmptyStateContent);
        root.Controls.Add(_grid, 0, 1);

        _bottomBar = AppDataGridFooter.Create(BuildPageSizePanel(), _statusLabel, BuildPager());
        root.Controls.Add(_bottomBar, 0, 2);
        Controls.Add(root);
        UpdatePager();
    }

    private Control BuildToolbar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 10,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, 12),
            BackColor = LightTheme.Background
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _searchFieldCombo.DataSource = _searchFields;
        _searchFieldCombo.DisplayMember = nameof(SearchFieldOption.DisplayName);
        _searchFieldCombo.ValueMember = nameof(SearchFieldOption.Value);
        _searchFieldCombo.SelectedItem = GetDefaultSearchField();
        _searchFieldCombo.SelectionChangeCommitted += async (_, _) =>
        {
            UpdateSearchPlaceholder();
            if (!_isSettingFilters)
            {
                await ResetAndFetchAsync();
            }
        };

        _searchTextBox.ShowSearchIcon = true;
        _searchTextBox.TextChanged += (_, _) =>
        {
            if (_isSettingFilters)
            {
                return;
            }

            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        };
        _searchDebounceTimer.Tick += async (_, _) =>
        {
            _searchDebounceTimer.Stop();
            await ResetAndFetchAsync();
        };
        UpdateSearchPlaceholder();

        _statusCombo.DataSource = new List<StatusFilterOption>
        {
            new("Tất cả", HistoryStatusFilter.All),
            new("Hoàn thành", HistoryStatusFilter.Completed),
            new("Chờ xử lý", HistoryStatusFilter.Processing),
            new("Đã hủy", HistoryStatusFilter.Cancelled)
        };
        _statusCombo.DisplayMember = nameof(StatusFilterOption.DisplayName);
        _statusCombo.ValueMember = nameof(StatusFilterOption.Value);
        _statusCombo.SelectionChangeCommitted += async (_, _) =>
        {
            if (!_isSettingFilters)
            {
                await ResetAndFetchAsync();
            }
        };

        _columnVisibilityCombo.SelectionChangeCommitted += (_, _) => ToggleSelectedColumnVisibility();

        _clearFilterButton.Click += async (_, _) => await ClearFiltersAsync();

        _searchFieldCombo.Anchor = AnchorStyles.Left;
        _searchTextBox.Anchor = AnchorStyles.Left;
        _statusCombo.Anchor = AnchorStyles.Left;
        _columnVisibilityCombo.Anchor = AnchorStyles.Left;
        _clearFilterButton.Anchor = AnchorStyles.Left;
        _searchFieldCombo.Margin = new Padding(0, 0, 10, 0);
        _searchTextBox.Margin = new Padding(0, 0, 10, 0);
        _statusCombo.Margin = new Padding(0, 0, 12, 0);
        _columnVisibilityCombo.Margin = new Padding(0, 0, 10, 0);

        panel.Controls.Add(CreateToolbarLabel("Tìm kiếm:", new Padding(0, 0, 8, 0)), 0, 0);
        panel.Controls.Add(_searchFieldCombo, 1, 0);
        panel.Controls.Add(_searchTextBox, 2, 0);
        panel.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = LightTheme.Background }, 3, 0);
        panel.Controls.Add(CreateToolbarLabel("Trạng thái:", new Padding(8, 0, 8, 0)), 4, 0);
        panel.Controls.Add(_statusCombo, 5, 0);
        panel.Controls.Add(CreateToolbarLabel("Hiển thị:", new Padding(0, 0, 8, 0)), 6, 0);
        panel.Controls.Add(_columnVisibilityCombo, 7, 0);
        panel.Controls.Add(_clearFilterButton, 8, 0);

        return panel;
    }

    private Control BuildPageSizePanel()
    {
        _pageSizeCombo.DataSource = PageSizeOptions.ToList();
        _pageSizeCombo.SelectedItem = DefaultLimit;
        _pageSizeCombo.SelectionChangeCommitted += async (_, _) =>
        {
            _limit = (int)_pageSizeCombo.SelectedItem!;
            await ResetAndFetchAsync();
        };
        return AppDataGridFooter.CreatePageSizePanel(_pageSizeCombo);
    }

    private Control BuildPager() =>
        AppDataGridFooter.CreatePager(_pageLabel, _previousPageButton, _nextPageButton);

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.EditMode = DataGridViewEditMode.EditProgrammatically;
        _grid.ReadOnly = true;
        _grid.Tag = LightTheme.SkipActiveGridHeaderThemeTag;

        foreach (var column in _columns)
        {
            _grid.Columns.Add(CreateTextColumn(column.HeaderText, column.PropertyName, column.MinimumWidth, column.FillWeight, column.Visible));
        }

        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(
        string header,
        string propertyName,
        int minimumWidth,
        float fillWeight,
        bool visible = true) =>
        new()
        {
            Name = propertyName,
            HeaderText = header,
            DataPropertyName = propertyName,
            MinimumWidth = minimumWidth,
            FillWeight = fillWeight,
            ReadOnly = true,
            Visible = visible
        };

    private async Task ResetAndFetchAsync()
    {
        _page = DefaultPage;
        await FetchPageAsync();
    }

    private async Task ClearFiltersAsync()
    {
        _isSettingFilters = true;
        try
        {
            _searchDebounceTimer.Stop();
            _searchTextBox.Clear();
            _searchFieldCombo.SelectedItem = GetDefaultSearchField();
            _statusCombo.SelectedIndex = 0;
            UpdateSearchPlaceholder();
        }
        finally
        {
            _isSettingFilters = false;
        }

        await ResetAndFetchAsync();
    }

    private async Task MovePageAsync(int delta)
    {
        var totalPages = GetTotalPages();
        var target = Math.Min(Math.Max(1, _page + delta), totalPages);
        if (target == _page)
        {
            return;
        }

        _page = target;
        await FetchPageAsync();
    }

    private async Task FetchPageAsync()
    {
        try
        {
            if (!await EnsureAuthenticatedAsync())
            {
                ShowLoginRequiredState();
                return;
            }

            _requiresLogin = false;
            _emptyStateText = "Đang tải dữ liệu...";
            _rows.Clear();
            _grid.Invalidate();
            _statusLabel.Text = _kind == HistoryListKind.Purchase
                ? "Đang tải lịch sử mua hàng..."
                : "Đang tải lịch sử giao dịch...";
            SetToolbarEnabled(false);

            var request = BuildRequest();
            if (_kind == HistoryListKind.Purchase)
            {
                var page = await _proxyOrderApiClient.GetPurchaseHistoryAsync(request, CancellationToken.None);
                BindRows(page.Items.Select(CreatePurchaseRow));
                _total = page.Total;
                _hasNextPage = page.HasNextPage;
            }
            else
            {
                var page = await _proxyOrderApiClient.GetTransactionHistoryAsync(request, CancellationToken.None);
                BindRows(page.Items.Select(CreateTransactionRow));
                _total = page.Total;
                _hasNextPage = page.HasNextPage;
            }

            _loaded = true;
            _emptyStateText = "Không tìm thấy dữ liệu";
            UpdateStatusText();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.ClearAsync(CancellationToken.None);
            ShowLoginRequiredState();
        }
        catch (Exception ex)
        {
            _rows.Clear();
            _emptyStateText = "Chưa tải được danh sách";
            _emptyStateButtonText = "Tải lại";
            _emptyStateButtonClick = async () => await FetchPageAsync();
            _statusLabel.Text = _kind == HistoryListKind.Purchase
                ? "Không thể tải lịch sử mua hàng."
                : "Không thể tải lịch sử giao dịch.";
            UiFeedback.ShowWarning(this, ex, _statusLabel.Text);
        }
        finally
        {
            SetToolbarEnabled(!_requiresLogin);
            UpdatePager();
            _grid.Invalidate();
        }
    }

    private HistoryPageRequest BuildRequest() =>
        new()
        {
            Page = _page,
            Limit = _limit,
            SearchField = (_searchFieldCombo.SelectedItem as SearchFieldOption)?.Value ?? HistorySearchFields.All,
            SearchText = _searchTextBox.TextValue,
            Status = (_statusCombo.SelectedItem as StatusFilterOption)?.Value ?? HistoryStatusFilter.All
        };

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        try
        {
            return await _authService.EnsureValidSessionAsync(CancellationToken.None);
        }
        catch
        {
            await _authService.ClearAsync(CancellationToken.None);
            return false;
        }
    }

    private void ShowLoginRequiredState()
    {
        _requiresLogin = true;
        _loaded = true;
        _rows.Clear();
        _total = null;
        _hasNextPage = false;
        _emptyStateText = _kind == HistoryListKind.Purchase
            ? "Vui lòng đăng nhập để xem lịch sử mua hàng."
            : "Vui lòng đăng nhập để xem lịch sử giao dịch.";
        _emptyStateButtonText = LoginRequestedAsync is null ? string.Empty : "Đăng nhập";
        _emptyStateButtonClick = async () =>
        {
            if (LoginRequestedAsync is not null && await LoginRequestedAsync())
            {
                await FetchPageAsync();
            }
        };
        _statusLabel.Text = _emptyStateText;
    }

    private void BindRows(IEnumerable<HistoryGridRow> rows)
    {
        _rows.RaiseListChangedEvents = false;
        try
        {
            _rows.Clear();
            foreach (var row in rows)
            {
                _rows.Add(row);
            }
        }
        finally
        {
            _rows.RaiseListChangedEvents = true;
            _rows.ResetBindings();
        }
    }

    private void UpdateStatusText()
    {
        var total = _total ?? _rows.Count;
        if (total <= 0 || _rows.Count == 0)
        {
            _statusLabel.Text = "0 mục";
            return;
        }

        var first = ((_page - 1) * _limit) + 1;
        var last = Math.Min((_page - 1) * _limit + _rows.Count, total);
        _statusLabel.Text = $"{first} – {last} trong số {total} mục";
    }

    private void UpdatePager()
    {
        var totalPages = GetTotalPages();
        _page = Math.Min(Math.Max(1, _page), totalPages);
        _pageLabel.Text = $"{_page}/{totalPages}";
        var canPage = !_requiresLogin && !_isBusy;
        _previousPageButton.Enabled = canPage && _page > 1;
        _nextPageButton.Enabled = canPage && (_total is null ? _hasNextPage : _page < totalPages);
        _bottomBar.Visible = !_requiresLogin;
    }

    private int GetTotalPages() =>
        _total is > 0 ? Math.Max(1, (int)Math.Ceiling(_total.Value / (double)_limit)) : 1;

    private void SetToolbarEnabled(bool enabled)
    {
        _isBusy = !enabled;
        foreach (Control control in Controls.Cast<Control>().SelectMany(Flatten))
        {
            if (control is Button or ComboBox or AppTextInput)
            {
                control.Enabled = enabled;
            }
        }
    }

    private GridEmptyStateContent? GetEmptyStateContent()
    {
        if (_rows.Count > 0 || string.IsNullOrWhiteSpace(_emptyStateText))
        {
            return null;
        }

        return GridEmptyStateContent.TextOnly(_emptyStateText);
    }

    private void ToggleSelectedColumnVisibility()
    {
        if (_columnVisibilityCombo.SelectedItem is not ColumnVisibilityOption option)
        {
            return;
        }

        if (option.IsAll)
        {
            foreach (DataGridViewColumn column in _grid.Columns)
            {
                column.Visible = true;
            }

            RefreshColumnVisibilityCombo();
            return;
        }

        if (_grid.Columns[option.ColumnName] is not { } target)
        {
            return;
        }

        var visibleCount = _grid.Columns.Cast<DataGridViewColumn>().Count(column => column.Visible);
        if (target.Visible && visibleCount <= 1)
        {
            RefreshColumnVisibilityCombo();
            return;
        }

        target.Visible = !target.Visible;
        RefreshColumnVisibilityCombo();
    }

    private void RefreshColumnVisibilityCombo()
    {
        var options = new List<ColumnVisibilityOption>
        {
            new(string.Empty, "Tất cả", IsAll: true, IsVisible: _grid.Columns.Cast<DataGridViewColumn>().All(column => column.Visible))
        };
        options.AddRange(_grid.Columns
            .Cast<DataGridViewColumn>()
            .OrderBy(column => column.DisplayIndex)
            .Select(column => new ColumnVisibilityOption(column.Name, column.HeaderText, IsAll: false, column.Visible)));

        _columnVisibilityCombo.BeginUpdate();
        try
        {
            _columnVisibilityCombo.DataSource = null;
            _columnVisibilityCombo.Items.Clear();
            _columnVisibilityCombo.Items.AddRange(options.Cast<object>().ToArray());
            _columnVisibilityCombo.SelectedIndex = _columnVisibilityCombo.Items.Count > 0 ? 0 : -1;
        }
        finally
        {
            _columnVisibilityCombo.EndUpdate();
        }
    }

    private SearchFieldOption GetDefaultSearchField() =>
        _kind == HistoryListKind.Purchase
            ? _searchFields.First(field => field.Value == "code")
            : _searchFields.First(field => field.Value == "orderCode");

    private void UpdateSearchPlaceholder()
    {
        var selected = _searchFieldCombo.SelectedItem as SearchFieldOption ?? GetDefaultSearchField();
        _searchTextBox.PlaceholderText = selected.Placeholder;
    }

    private void PaintStatusCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != StatusColumnName ||
            _grid.Rows[e.RowIndex].DataBoundItem is not HistoryGridRow row ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        using var dotBrush = new SolidBrush(row.StatusColor);
        var textSize = TextRenderer.MeasureText(row.Status, _grid.Font, Size.Empty, TextFormatFlags.NoPadding);
        var contentWidth = 8 + 6 + textSize.Width;
        var left = e.CellBounds.Left + Math.Max(8, (e.CellBounds.Width - contentWidth) / 2);
        var dotTop = e.CellBounds.Top + ((e.CellBounds.Height - 6) / 2);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(dotBrush, left, dotTop, 6, 6);
        TextRenderer.DrawText(
            e.Graphics,
            row.Status,
            _grid.Font,
            new Rectangle(left + 12, e.CellBounds.Top, Math.Max(0, e.CellBounds.Right - left - 12), e.CellBounds.Height),
            row.StatusColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.Handled = true;
    }

    private static HistoryGridRow CreatePurchaseRow(PurchaseHistoryItem item)
    {
        var status = ResolveStatus(item.StatusId, item.StatusName);
        return new HistoryGridRow
        {
            Code = FirstNonEmpty(item.Code, item.Id, "-"),
            Account = FirstNonEmpty(item.Account, "-"),
            Quantity = item.Quantity.ToString(CultureInfo.InvariantCulture),
            Amount = FormatCurrency(item.Amount),
            DayOfUse = FirstNonEmpty(item.DayOfUse, "-"),
            TaxCode = FirstNonEmpty(item.TaxCode, "-"),
            Status = status.Label,
            StatusColor = status.Color,
            CreatedAt = FormatDateTime(item.CreatedAt)
        };
    }

    private static HistoryGridRow CreateTransactionRow(TransactionHistoryItem item)
    {
        var status = ResolveStatus(item.StatusId, item.StatusName);
        return new HistoryGridRow
        {
            Type = ResolveTransactionType(item.Type),
            Code = FirstNonEmpty(item.Code, item.Id, "-"),
            Amount = FormatCurrency(item.Amount),
            Note = FirstNonEmpty(item.Content, item.Description, "-"),
            Status = status.Label,
            StatusColor = status.Color,
            CreatedAt = FormatDateTime(item.CreatedAt)
        };
    }

    private static (string Label, Color Color) ResolveStatus(int? statusId, string statusName) =>
        statusId switch
        {
            3 or 4 => ("Chờ xử lý", Color.FromArgb(230, 126, 0)),
            6 => ("Hoàn thành", Color.FromArgb(20, 128, 84)),
            7 => ("Đã hủy", Color.FromArgb(197, 15, 31)),
            _ => (FirstNonEmpty(statusName, "-"), LightTheme.Muted)
        };

    private static string ResolveTransactionType(int? type) =>
        type switch
        {
            1 => "Nạp tiền",
            2 => "Mua hàng",
            4 => "Bonus",
            5 => "Hoàn tiền",
            _ => "Khác"
        };

    private static string FormatCurrency(decimal value) =>
        $"{value.ToString("N0", ViCulture)} đ";

    private static string FormatDateTime(DateTimeOffset? value) =>
        value is null ? "-" : value.Value.ToLocalTime().ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static Label CreateToolbarLabel(string text, Padding margin) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = margin,
            BackColor = LightTheme.Background
        };

    private static IEnumerable<Control> Flatten(Control control)
    {
        yield return control;
        foreach (Control child in control.Controls)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private static List<SearchFieldOption> CreateSearchFields(HistoryListKind kind) =>
        kind == HistoryListKind.Purchase
            ?
            [
                new("Tất cả", HistorySearchFields.All, "Tìm kiếm theo đơn hàng, tài khoản..."),
                new("Mã đơn hàng", "code", "Tìm theo mã đơn hàng"),
                new("Tài khoản", "user.phone", "Tìm theo số điện thoại"),
                new("Mã số thuế", "taxCode.mst", "Tìm theo mã số thuế")
            ]
            :
            [
                new("Tất cả", HistorySearchFields.All, "Tìm kiếm theo giao dịch, ghi chú..."),
                new("Mã giao dịch", "code", "Tìm theo mã giao dịch"),
                new("Mã đơn hàng", "orderCode", "Tìm theo mã đơn hàng"),
                new("Ghi chú", "description", "Tìm theo ghi chú")
            ];

    private static List<HistoryColumnSpec> CreateColumns(HistoryListKind kind) =>
        kind == HistoryListKind.Purchase
            ?
            [
                new("Mã đơn hàng", nameof(HistoryGridRow.Code), 130, 12),
                new("Tài khoản", nameof(HistoryGridRow.Account), 150, 12),
                new("Số lượng", nameof(HistoryGridRow.Quantity), 90, 8),
                new("Số tiền thanh toán", nameof(HistoryGridRow.Amount), 150, 13),
                new("Ngày sử dụng", nameof(HistoryGridRow.DayOfUse), 120, 10),
                new("Mã số thuế", nameof(HistoryGridRow.TaxCode), 120, 10, Visible: false),
                new("Trạng thái", nameof(HistoryGridRow.Status), 140, 12),
                new("Ngày thực hiện", nameof(HistoryGridRow.CreatedAt), 160, 13)
            ]
            :
            [
                new("Loại giao dịch", nameof(HistoryGridRow.Type), 130, 11),
                new("Mã giao dịch", nameof(HistoryGridRow.Code), 150, 12),
                new("Số tiền", nameof(HistoryGridRow.Amount), 140, 12),
                new("Note", nameof(HistoryGridRow.Note), 260, 22),
                new("Trạng thái", nameof(HistoryGridRow.Status), 140, 12),
                new("Ngày thực hiện", nameof(HistoryGridRow.CreatedAt), 160, 13)
            ];

    private sealed record SearchFieldOption(string DisplayName, string Value, string Placeholder);

    private sealed record StatusFilterOption(string DisplayName, HistoryStatusFilter Value);

    private sealed record ColumnVisibilityOption(string ColumnName, string DisplayName, bool IsAll, bool IsVisible)
    {
        public override string ToString() => IsAll ? DisplayName : $"{(IsVisible ? "✓" : " ")} {DisplayName}";
    }

    private sealed record HistoryColumnSpec(
        string HeaderText,
        string PropertyName,
        int MinimumWidth,
        float FillWeight,
        bool Visible = true);

    private sealed class HistoryGridRow
    {
        public string Type { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Account { get; init; } = string.Empty;
        public string Quantity { get; init; } = string.Empty;
        public string Amount { get; init; } = string.Empty;
        public string DayOfUse { get; init; } = string.Empty;
        public string TaxCode { get; init; } = string.Empty;
        public string Note { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public Color StatusColor { get; init; } = LightTheme.Muted;
        public string CreatedAt { get; init; } = string.Empty;
    }
}

internal enum HistoryListKind
{
    Purchase,
    Transaction
}
