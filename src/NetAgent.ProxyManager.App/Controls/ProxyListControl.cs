using Microsoft.Extensions.DependencyInjection;
using NetAgent.ProxyManager.App.Forms;
using NetAgent.ProxyManager.App.Configuration;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;
using PushButtonState = System.Windows.Forms.VisualStyles.PushButtonState;
using CheckBoxState = System.Windows.Forms.VisualStyles.CheckBoxState;

namespace NetAgent.ProxyManager.App.Controls;

public sealed class ProxyListControl : UserControl
{
    private const string SelectionColumnName = nameof(ProxyRow.IsSelected);
    private const string ActionsColumnName = nameof(ProxyRow.Actions);
    private const string ViewUsageButtonText = "Xem";
    private const string CheckingStatusText = "Đang check";
    private const string UsageColumnHeaderText = "Ứng dụng";
    private const int DesignedToolbarButtonHeight = 40;
    private const int FilterToolbarRowHeight = 40;
    private const int ToolbarToFilterRowGap = 32;
    private const int FilterRowToGridGap = 32;
    private const int UsageAppIconSize = 22;
    private const int UsageViewButtonSize = 32;
    private static readonly Color DisabledToolbarColor = Color.FromArgb(189, 189, 189);

    private readonly ProxyManagementService _proxyManagementService;
    private readonly IApplicationRuleRepository _applicationRuleRepository;
    private readonly IServiceProvider _serviceProvider;
    private TableLayoutPanel _rootLayout = null!;
    private TableLayoutPanel _toolbarLayout = null!;
    private TableLayoutPanel _filterPanel = null!;
    private readonly DataGridView _grid = new AppDataGridView();
    private readonly Panel _emptyState = new();
    private readonly ContextMenuStrip _rowContextMenu = new();
    private readonly Image _copyIcon = UiIcons.CopyGlyph;
    private readonly Dictionary<string, Image> _applicationIconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBox _proxyCellTextSelector = new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        ReadOnly = true,
        Visible = false,
        TabStop = false
    };
    private readonly Label _statusLabel = AppDataGridFooter.CreateStatusLabel();
    private readonly ComboBox _textFilterColumnCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly AppTextInput _textFilterTextBox = new() { Width = 260 };
    private readonly ComboBox _protocolFilterCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly ComboBox _statusFilterCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _pageSizeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly Label _pageLabel = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleCenter };
    private readonly System.Windows.Forms.Timer _textFilterDebounceTimer = new() { Interval = 900 };
    private Button _refreshButton = null!;
    private Button _addProxyButton = null!;
    private Button _deleteSelectedButton = null!;
    private Button _checkSelectedButton = null!;
    private Button _copySelectedButton = null!;
    private Button _previousPageButton = null!;
    private Button _nextPageButton = null!;
    private List<ProxyServer> _manualProxies = [];
    private List<ProxyRow> _rows = [];
    private readonly HashSet<Guid> _selectedProxyIds = [];
    private IReadOnlyDictionary<Guid, ProxyUsageInfo> _usageByProxyId = new Dictionary<Guid, ProxyUsageInfo>();
    private (int RowIndex, int ColumnIndex)? _pendingComboOpenCell;
    private int _page = ProxyOrderListConstants.DefaultPage;
    private int _limit = ProxyOrderListConstants.DefaultLimit;
    private int _filteredCount;
    private bool _isBusy;
    private bool _selectionOnlyMode;

    public ProxyListControl(
        ProxyManagementService proxyManagementService,
        IApplicationRuleRepository applicationRuleRepository,
        IServiceProvider serviceProvider)
    {
        _proxyManagementService = proxyManagementService;
        _applicationRuleRepository = applicationRuleRepository;
        _serviceProvider = serviceProvider;

        Dock = DockStyle.Fill;
        BuildUi();
        LightTheme.Apply(this);
    }

    public event EventHandler<ProfileAffectingChange>? ProfileAffectingChanged;

    public event EventHandler<Guid>? ViewApplicationsRequested;

    public event EventHandler? SelectionAvailabilityChanged;

    public IReadOnlyList<ProxyServer> CurrentProxies => _manualProxies;

    public bool HasSelectedProxy => GetSelectedProxies().Count > 0;

    public void UseCompactLayout()
    {
        _rootLayout.Padding = new Padding(8);
        _toolbarLayout.Padding = new Padding(0, 0, 0, FilterRowToGridGap);
        _filterPanel.Margin = Padding.Empty;
        _grid.Margin = Padding.Empty;
    }

    public void UseSelectionOnlyLayout()
    {
        UseCompactLayout();
        _selectionOnlyMode = true;
        _grid.MultiSelect = false;
        _grid.ReadOnly = true;
        _grid.AllowUserToResizeRows = false;
        if (_grid.Columns[SelectionColumnName] is { } selectionColumn)
        {
            selectionColumn.Visible = true;
        }

        if (_grid.Columns[ActionsColumnName] is { } actionsColumn)
        {
            actionsColumn.Visible = false;
        }

        _rowContextMenu.Enabled = false;
        if (_toolbarLayout.GetControlFromPosition(0, 1) is { } filters)
        {
            filters.Visible = false;
        }

        _refreshButton.Visible = false;
        _deleteSelectedButton.Visible = false;
        _checkSelectedButton.Visible = false;
        _copySelectedButton.Visible = false;
        _addProxyButton.Visible = true;
        _addProxyButton.Anchor = AnchorStyles.Right;
    }

    public bool TryGetSelectedProxy(out ProxyServer proxy)
    {
        if (_selectionOnlyMode)
        {
            proxy = GetCurrentProxy()!;
            return proxy is not null;
        }

        proxy = GetSelectedProxies().FirstOrDefault()!;
        return proxy is not null;
    }

    public async Task CheckSelectedProxyAsync() => await CheckSelectedAsync();

    public async Task LoadAsync()
    {
        try
        {
            _isBusy = true;
            _statusLabel.Text = "Đang tải danh sách proxy...";
            UpdateSelectionDependentActions();
            _manualProxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None))
                .Where(proxy => !proxy.IsFromBackend)
                .DistinctBy(CreateProxyKey)
                .ToList();
            await RefreshUsageCountsAsync();
            BindGrid();
        }
        finally
        {
            _isBusy = false;
            UpdateSelectionDependentActions();
        }
    }

    public async Task RefreshUsageCountsAsync()
    {
        var rules = await _applicationRuleRepository.GetAllAsync(CancellationToken.None);
        _usageByProxyId = rules
            .Where(rule => rule.AssignedProxyId is not null)
            .GroupBy(rule => rule.AssignedProxyId!.Value)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var applications = group
                        .Select(CreateProxyApplicationUsage)
                        .Where(application => !string.IsNullOrWhiteSpace(application.DisplayName))
                        .OrderBy(application => application.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                    return new ProxyUsageInfo(applications);
                });
        BindGrid();
    }

    private ProxyApplicationUsage CreateProxyApplicationUsage(ApplicationRule rule)
    {
        var displayName = !string.IsNullOrWhiteSpace(rule.RuntimeProcessName)
            ? rule.RuntimeProcessName.Trim()
            : rule.GetApplicationName();
        return new ProxyApplicationUsage(displayName, LoadApplicationIcon(rule));
    }

    private Image LoadApplicationIcon(ApplicationRule rule)
    {
        var path = GetApplicationIconPath(rule);
        if (!string.IsNullOrWhiteSpace(path) &&
            File.Exists(path))
        {
            if (_applicationIconCache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            try
            {
                using var icon = Icon.ExtractAssociatedIcon(path);
                var bitmap = icon?.ToBitmap() ?? SystemIcons.Application.ToBitmap();
                _applicationIconCache[path] = bitmap;
                return bitmap;
            }
            catch
            {
                // Fall back to the generic application icon below.
            }
        }

        const string fallbackKey = "__application_fallback__";
        if (_applicationIconCache.TryGetValue(fallbackKey, out var fallback))
        {
            return fallback;
        }

        var fallbackBitmap = SystemIcons.Application.ToBitmap();
        _applicationIconCache[fallbackKey] = fallbackBitmap;
        return fallbackBitmap;
    }

    private static string? GetApplicationIconPath(ApplicationRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.RuntimeExecutablePath))
        {
            return rule.RuntimeExecutablePath.Trim().Trim('"');
        }

        if (!string.IsNullOrWhiteSpace(rule.ExecutableName))
        {
            return rule.ExecutableName.Trim().Trim('"');
        }

        return null;
    }

    public async Task LoadAndFocusProxyAsync(ProxyServer proxy)
    {
        await LoadAsync();
        var target = _rows.FirstOrDefault(row => row.Id == proxy.Id);
        if (target is null)
        {
            return;
        }

        target.IsSelected = true;
        _selectedProxyIds.Add(target.Id);
        FocusProxyRow(target.Id);
        _grid.Refresh();
        InvalidateSelectionHeader();
        UpdateSelectionDependentActions();
    }

    private void FocusProxyRow(Guid proxyId)
    {
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is ProxyRow row && row.Id == proxyId)
            {
                _grid.ClearSelection();
                gridRow.Selected = true;
                _grid.CurrentCell = gridRow.Cells.Cast<DataGridViewCell>().FirstOrDefault(cell => cell.Visible);
                _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, gridRow.Index);
                break;
            }
        }
    }

    private void BuildUi()
    {
        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(16)
        };
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _rootLayout.Controls.Add(BuildToolbar(), 0, 0);

        var gridHost = new Panel { Dock = DockStyle.Fill };
        ConfigureGrid();
        ConfigureEmptyState();
        gridHost.Controls.Add(_grid);
        gridHost.Controls.Add(_emptyState);
        _rootLayout.Controls.Add(gridHost, 0, 1);

        _rootLayout.Controls.Add(BuildBottomBar(), 0, 2);
        Controls.Add(_rootLayout);
    }

    private Control BuildToolbar()
    {
        _toolbarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, FilterRowToGridGap)
        };
        _toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _toolbarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _toolbarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var filterPanel = BuildFilterPanel();
        var actionToolbarRow = BuildActionToolbarRow();
        _toolbarLayout.Controls.Add(actionToolbarRow, 0, 0);
        _toolbarLayout.Controls.Add(filterPanel, 0, 1);
        return _toolbarLayout;
    }

    private Control BuildBottomBar()
    {
        return AppDataGridFooter.Create(BuildPageSizePanel(), _statusLabel, BuildPager());
    }

    private Control BuildPageSizePanel()
    {
        _pageSizeCombo.DataSource = ProxyOrderListConstants.PageSizeOptions.ToList();
        _pageSizeCombo.SelectedItem = ProxyOrderListConstants.DefaultLimit;
        _pageSizeCombo.SelectionChangeCommitted += (_, _) =>
        {
            _limit = (int)_pageSizeCombo.SelectedItem!;
            _page = ProxyOrderListConstants.DefaultPage;
            BindGrid();
        };
        return AppDataGridFooter.CreatePageSizePanel(_pageSizeCombo);
    }

    private Control BuildPager()
    {
        _previousPageButton = AppDataGridFooter.CreatePagerButton(previous: true, (_, _) => MovePage(-1));
        _nextPageButton = AppDataGridFooter.CreatePagerButton(previous: false, (_, _) => MovePage(1));
        return AppDataGridFooter.CreatePager(_pageLabel, _previousPageButton, _nextPageButton);
    }

    private void MovePage(int delta)
    {
        var totalPages = GetTotalPages();
        var nextPage = Math.Max(1, Math.Min(totalPages, _page + delta));
        if (nextPage == _page)
        {
            return;
        }

        _page = nextPage;
        BindGrid();
    }

    private int GetTotalPages() =>
        Math.Max(1, (int)Math.Ceiling(_filteredCount / (double)Math.Max(1, _limit)));

    private void UpdatePager()
    {
        if (_previousPageButton is null || _nextPageButton is null)
        {
            return;
        }

        var totalPages = GetTotalPages();
        _pageLabel.Text = $"{_page}/{totalPages}";
        var canPage = !_isBusy && _filteredCount > 0;
        _previousPageButton.Enabled = canPage && _page > 1;
        _nextPageButton.Enabled = canPage && _page < totalPages;
    }

    private Control BuildActionToolbarRow()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = DesignedToolbarButtonHeight,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, ToolbarToFilterRowGap)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignedToolbarButtonHeight));

        _addProxyButton = new AppPrimaryButton
        {
            Text = "Thêm proxy",
            Image = UiIcons.NewAddMyProxies,
            Height = DesignedToolbarButtonHeight,
            MinimumSize = new Size(120, DesignedToolbarButtonHeight),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty
        };
        _addProxyButton.Click += async (_, _) => await AddProxyAsync();

        _refreshButton = CreateToolbarRowButton("Làm mới", async (_, _) => await LoadAsync(), UiIcons.NewReset, UiIcons.NewResetHover, UiIcons.NewResetDisabled, 108);
        _checkSelectedButton = CreateToolbarRowButton("Check proxy", async (_, _) => await CheckSelectedAsync(), UiIcons.NewCheckProxy, UiIcons.NewCheckProxyHover, UiIcons.NewCheckProxyDisabled, 132);
        _copySelectedButton = CreateToolbarRowButton("Sao chép", (_, _) => CopySelectedProxies(), UiIcons.NewCopy, UiIcons.NewCopyHover, UiIcons.NewCopyDisabled, 116);
        _deleteSelectedButton = CreateToolbarRowButton("Xóa đã chọn", async (_, _) => await DeleteSelectedAsync(), UiIcons.NewRemoveAll, UiIcons.NewRemoveAllHover, UiIcons.NewRemoveAllDisabled, 162);

        var actions = CreateDesignedToolbarGroup(
            _refreshButton,
            _checkSelectedButton,
            _copySelectedButton,
            _deleteSelectedButton);
        actions.Anchor = AnchorStyles.Right;

        panel.Controls.Add(_addProxyButton, 0, 0);
        panel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 1, 0);
        panel.Controls.Add(actions, 2, 0);
        return panel;
    }

    private static Control CreateDesignedToolbarGroup(params Control[] buttons)
    {
        var group = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Height = DesignedToolbarButtonHeight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        foreach (var button in buttons)
        {
            button.Margin = new Padding(0, 0, 8, 0);
            group.Controls.Add(button);
        }

        return group;
    }

    private static Button CreateToolbarRowButton(
        string text,
        EventHandler onClick,
        Image icon,
        Image hoverIcon,
        Image disabledIcon,
        int minimumWidth)
    {
        var button = new DesignedToolbarButton
        {
            Text = text,
            Image = icon,
            HoverImage = hoverIcon,
            DisabledImage = disabledIcon,
            RestTextColor = DesignedGridTheme.TextColor,
            HoverTextColor = DesignedGridTheme.AccentColor,
            DisabledTextColor = DisabledToolbarColor,
            MinimumSize = new Size(minimumWidth, DesignedToolbarButtonHeight),
            Height = DesignedToolbarButtonHeight,
            Padding = new Padding(6),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            Anchor = AnchorStyles.Left
        };
        button.Click += onClick;
        return button;
    }

    private Control BuildFilterPanel()
    {
        _textFilterColumnCombo.DataSource = new List<FilterOption>
        {
            new("Proxy", nameof(ProxyRow.Proxy)),
            new("Username", nameof(ProxyRow.Username)),
            new("Password", nameof(ProxyRow.Password))
        };
        _textFilterColumnCombo.DisplayMember = nameof(FilterOption.DisplayName);
        _textFilterColumnCombo.ValueMember = nameof(FilterOption.Value);

        _protocolFilterCombo.Items.AddRange(["Tất cả", "HTTP", "SOCKS5"]);
        _statusFilterCombo.Items.AddRange(["Tất cả trạng thái", FormatStatus(ProxyStatus.Unknown), FormatStatus(ProxyStatus.Checking), FormatStatus(ProxyStatus.Live), FormatStatus(ProxyStatus.Dead)]);
        _protocolFilterCombo.SelectedIndex = 0;
        _statusFilterCombo.SelectedIndex = 0;

        _textFilterTextBox.PlaceholderText = "Nhập từ khóa lọc...";
        _textFilterTextBox.TextChanged += (_, _) =>
        {
            _textFilterDebounceTimer.Stop();
            _textFilterDebounceTimer.Start();
        };
        _textFilterDebounceTimer.Tick += (_, _) =>
        {
            _textFilterDebounceTimer.Stop();
            ResetPageAndBindGrid();
        };
        _textFilterColumnCombo.SelectedIndexChanged += (_, _) => ResetPageAndBindGrid();
        _protocolFilterCombo.SelectedIndexChanged += (_, _) => ResetPageAndBindGrid();
        _statusFilterCombo.SelectedIndexChanged += (_, _) => ResetPageAndBindGrid();

        _filterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = FilterToolbarRowHeight,
            ColumnCount = 10,
            RowCount = 1,
            Margin = Padding.Empty
        };
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        _filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _filterPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, FilterToolbarRowHeight));

        foreach (var input in new Control[] { _textFilterColumnCombo, _textFilterTextBox, _protocolFilterCombo, _statusFilterCombo })
        {
            input.Anchor = AnchorStyles.Left;
            input.Margin = new Padding(0, 0, 10, 0);
        }

        var clearFilterButton = new AppClearFilterButton();
        clearFilterButton.Click += (_, _) => ClearFilters();
        clearFilterButton.Anchor = AnchorStyles.Right;
        clearFilterButton.Margin = Padding.Empty;

        _filterPanel.Controls.Add(CreateFilterLabel("Tìm kiếm:", new Padding(0, 0, 8, 0)), 0, 0);
        _filterPanel.Controls.Add(CreateInlineFilterGroup(_textFilterColumnCombo, _textFilterTextBox), 1, 0);
        _filterPanel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 2, 0);
        _filterPanel.Controls.Add(CreateFilterLabel("Giao thức:", new Padding(0, 0, 8, 0)), 3, 0);
        _filterPanel.Controls.Add(CreateInlineFilterGroup(_protocolFilterCombo), 4, 0);
        _filterPanel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 5, 0);
        _filterPanel.Controls.Add(CreateFilterLabel("Trạng thái:", new Padding(0, 0, 8, 0)), 6, 0);
        _filterPanel.Controls.Add(CreateInlineFilterGroup(_statusFilterCombo), 7, 0);
        _filterPanel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 8, 0);
        _filterPanel.Controls.Add(clearFilterButton, 9, 0);
        return _filterPanel;
    }

    private static TableLayoutPanel CreateInlineFilterGroup(params Control[] controls)
    {
        var group = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = controls.Length,
            RowCount = 1,
            Height = FilterToolbarRowHeight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, FilterToolbarRowHeight));
        foreach (var control in controls)
        {
            control.Anchor = AnchorStyles.Left;
            group.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            group.Controls.Add(control, group.Controls.Count, 0);
        }

        return group;
    }

    private static Label CreateFilterLabel(string text, Padding margin) =>
        new()
        {
            Text = text,
            AutoSize = false,
            Height = LightTheme.InputHeight,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            Margin = margin
        };

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.Margin = Padding.Empty;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.EditMode = DataGridViewEditMode.EditProgrammatically;
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _grid.CellClick += HandleGridCellClick;
        _grid.CellPainting += PaintSelectionHeaderCell;
        _grid.CellPainting += PaintProxyCopyCell;
        _grid.CellPainting += PaintEditableCellIcon;
        _grid.CellPainting += PaintStatusCheckCell;
        _grid.CellPainting += PaintUsageCell;
        _grid.CellPainting += PaintActionCell;
        _grid.CellToolTipTextNeeded += HandleCellToolTipTextNeeded;
        _grid.CellDoubleClick += HandleCellDoubleClick;
        _grid.CellEndEdit += async (_, e) => await HandleCellEndEditAsync(e);
        _grid.CellValidating += ValidateEditableCell;
        _grid.EditingControlShowing += HandleEditingControlShowing;
        _grid.SelectionChanged += (_, _) => UpdateSelectionDependentActions();
        _grid.ColumnHeaderMouseClick += HandleColumnHeaderMouseClick;
        _grid.CellMouseDown += HandleGridCellMouseDown;
        _grid.MouseMove += HandleGridMouseMove;
        _grid.MouseLeave += HandleGridMouseLeave;
        _grid.KeyDown += async (_, e) => await HandleGridKeyDownAsync(e);
        _grid.Scroll += (_, _) => HideProxyCellTextSelector();
        _grid.ColumnWidthChanged += (_, _) => HideProxyCellTextSelector();
        _grid.RowsAdded += (_, _) => HideProxyCellTextSelector();
        _grid.RowsRemoved += (_, _) => HideProxyCellTextSelector();
        _proxyCellTextSelector.Leave += (_, _) => HideProxyCellTextSelector();
        _proxyCellTextSelector.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Escape or Keys.Enter)
            {
                HideProxyCellTextSelector();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        _grid.Controls.Add(_proxyCellTextSelector);
        ConfigureGridColumns();
        BuildRowContextMenu();
        _ = new GridEmptyStateOverlay(_grid, GetEmptyStateContent);
    }

    private void ConfigureEmptyState()
    {
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.Visible = false;
        _emptyState.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = LightTheme.Muted,
            Text = "Chưa có proxy thủ công nào.\nBấm Thêm Proxy để thêm proxy đơn lẻ hoặc hàng loạt."
        });
    }

    private GridEmptyStateContent? GetEmptyStateContent()
    {
        if (_rows.Count > 0)
        {
            return null;
        }

        if (_manualProxies.Count == 0)
        {
            if (_selectionOnlyMode)
            {
                return new GridEmptyStateContent(
                    "Chưa có proxy thủ công nào.\nBấm thêm proxy để thêm proxy đơn lẻ hoặc hàng loạt.",
                    "Thêm proxy",
                    () => _ = AddProxyAsync(),
                    LightTheme.Accent,
                    ShowIcon: false,
                    FontStyle: FontStyle.Regular);
            }

            return GridEmptyStateContent.TextOnly("Chưa có proxy thủ công nào.\nBấm thêm proxy để thêm proxy đơn lẻ hoặc hàng loạt.");
        }

        if (_filteredCount == 0)
        {
            return GridEmptyStateContent.TextOnly("Không tìm thấy proxy phù hợp với bộ lọc hiện tại.");
        }

        return null;
    }

    private void ConfigureGridColumns()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = SelectionColumnName,
            HeaderText = string.Empty,
            DataPropertyName = nameof(ProxyRow.IsSelected),
            MinimumWidth = 42,
            FillWeight = 4,
            ReadOnly = true
        });
        _grid.Columns.Add(CreateTextColumn("Proxy", nameof(ProxyRow.ProxyDisplay), 210, 22, readOnly: true));
        _grid.Columns.Add(CreateTextColumn("Username", nameof(ProxyRow.Username), 120, 13));
        _grid.Columns.Add(CreateTextColumn("Password", nameof(ProxyRow.Password), 120, 13));
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "Loại",
            Name = nameof(ProxyRow.Protocol),
            DataPropertyName = nameof(ProxyRow.Protocol),
            MinimumWidth = 105,
            FillWeight = 10,
            DataSource = ProxyProtocolDisplay.Options.ToList(),
            DisplayMember = nameof(ProxyProtocolOption.DisplayName),
            ValueMember = nameof(ProxyProtocolOption.Value),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            DisplayStyleForCurrentCellOnly = false
        });
        _grid.Columns.Add(CreateTextColumn("Trạng thái", nameof(ProxyRow.Status), 120, 11, readOnly: true));
        _grid.Columns.Add(CreateTextColumn("Độ trễ (ms)", nameof(ProxyRow.LatencyMs), 100, 9, readOnly: true));
        _grid.Columns.Add(CreateTextColumn(UsageColumnHeaderText, nameof(ProxyRow.UsageCount), 120, 10, readOnly: true));
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ActionsColumnName,
            HeaderText = "Actions",
            DataPropertyName = nameof(ProxyRow.Actions),
            MinimumWidth = 130,
            FillWeight = 13,
            ReadOnly = true,
            Visible = false
        });
        foreach (var columnName in new[] { nameof(ProxyRow.Username), nameof(ProxyRow.Password) })
        {
            if (_grid.Columns[columnName] is { } column)
            {
                column.DefaultCellStyle.Padding = new Padding(4, 0, 28, 0);
            }
        }
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(
        string header,
        string propertyName,
        int minimumWidth,
        float fillWeight,
        bool readOnly = false) =>
        new()
        {
            Name = propertyName,
            HeaderText = header,
            DataPropertyName = propertyName,
            MinimumWidth = minimumWidth,
            FillWeight = fillWeight,
            ReadOnly = readOnly
        };

    private void BuildRowContextMenu()
    {
        _rowContextMenu.Items.Clear();
        _rowContextMenu.Opening -= HandleRowContextMenuOpening;
        _rowContextMenu.Items.Add("Check proxy", UiIcons.NewCheckProxyHover, async (_, _) => await CheckSelectedAsync());
        _rowContextMenu.Items.Add("Đổi thông tin", UiIcons.NewChangeProxyInfoHover, async (_, _) => await EditSelectedAsync());
        _rowContextMenu.Items.Add(new ToolStripSeparator());
        _rowContextMenu.Items.Add("Xóa", UiIcons.NewRemoveAllHover, async (_, _) => await DeleteSelectedAsync());
        _rowContextMenu.Opening += HandleRowContextMenuOpening;
    }

    private void HandleRowContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e) =>
        e.Cancel = GetSelectedProxies().Count == 0;

    private void HandleGridCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0)
        {
            return;
        }

        ShowRowContextMenu(e.RowIndex, Math.Max(e.ColumnIndex, 0));
    }

    private void ShowRowContextMenu(int rowIndex, int columnIndex)
    {
        if (_selectionOnlyMode ||
            rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not ProxyRow row)
        {
            return;
        }

        if (!row.IsSelected)
        {
            _selectedProxyIds.Clear();
            foreach (var item in _rows)
            {
                item.IsSelected = false;
            }

            row.IsSelected = true;
            _selectedProxyIds.Add(row.Id);
        }

        _grid.ClearSelection();
        _grid.Rows[rowIndex].Selected = true;
        _grid.CurrentCell = _grid.Rows[rowIndex].Cells[columnIndex];
        _grid.Invalidate();
        InvalidateSelectionHeader();
        UpdateSelectionDependentActions();

        _rowContextMenu.Show(_grid, _grid.PointToClient(Cursor.Position));
    }

    private void HandleGridCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var column = _grid.Columns[e.ColumnIndex];
        if (_selectionOnlyMode)
        {
            SelectSingleProxyRow(e.RowIndex);
        }

        if (column.DataPropertyName == nameof(ProxyRow.ProxyDisplay) &&
            TryGetProxyCopyButtonBounds(e.RowIndex, e.ColumnIndex, out var proxyCopyButtonBounds) &&
            proxyCopyButtonBounds.Contains(_grid.PointToClient(Cursor.Position)) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyRow proxyRow)
        {
            CopyTextToClipboard(proxyRow.ProxyDisplay);
            return;
        }

        if (_selectionOnlyMode)
        {
            UpdateSelectionDependentActions();
            return;
        }

        if (column.Name == SelectionColumnName &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyRow selectionRow)
        {
            selectionRow.IsSelected = !selectionRow.IsSelected;
            if (selectionRow.IsSelected)
            {
                _selectedProxyIds.Add(selectionRow.Id);
            }
            else
            {
                _selectedProxyIds.Remove(selectionRow.Id);
            }

            _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = selectionRow.IsSelected;
            InvalidateSelectionHeader();
            UpdateSelectionDependentActions();
            return;
        }

        if (column.DataPropertyName == nameof(ProxyRow.Status) &&
            TryGetStatusCheckButtonBounds(e.RowIndex, e.ColumnIndex, out var statusButtonBounds) &&
            statusButtonBounds.Contains(_grid.PointToClient(Cursor.Position)))
        {
            _ = CheckRowAsync(e.RowIndex);
            return;
        }

        if (column.DataPropertyName == nameof(ProxyRow.UsageCount) &&
            TryGetUsageViewButtonBounds(e.RowIndex, e.ColumnIndex, out var usageButtonBounds) &&
            usageButtonBounds.Contains(_grid.PointToClient(Cursor.Position)) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyRow usageRow)
        {
            ViewApplicationsRequested?.Invoke(this, usageRow.Id);
            return;
        }

        if (TryGetInlineEditIconBounds(e.RowIndex, e.ColumnIndex, out var inlineEditBounds) &&
            inlineEditBounds.Contains(_grid.PointToClient(Cursor.Position)))
        {
            BeginInlineEdit(e.RowIndex, e.ColumnIndex);
            return;
        }

        if (column.Name == ActionsColumnName &&
            TryGetActionButton(_grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, cutOverflow: true), out var action))
        {
            _ = action == RowAction.Edit ? EditRowAsync(e.RowIndex) : DeleteRowAsync(e.RowIndex);
            return;
        }

        if (column is not DataGridViewComboBoxColumn)
        {
            return;
        }

        if (_grid.IsCurrentCellInEditMode && !_grid.EndEdit())
        {
            return;
        }

        _pendingComboOpenCell = (e.RowIndex, e.ColumnIndex);
        _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        _grid.BeginEdit(selectAll: true);
    }

    private void HandleCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 &&
            e.ColumnIndex >= 0 &&
            _grid.Columns[e.ColumnIndex].DataPropertyName == nameof(ProxyRow.ProxyDisplay) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyRow proxyRow)
        {
            ShowProxyCellTextSelector(e.RowIndex, e.ColumnIndex, proxyRow.ProxyDisplay);
            return;
        }

        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].ReadOnly ||
            _grid.Columns[e.ColumnIndex].Name == SelectionColumnName)
        {
            return;
        }

        _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        _grid.BeginEdit(selectAll: true);
    }

    private void HandleEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (_grid.CurrentCell?.OwningColumn is { } currentColumn &&
            IsInlineEditableColumn(currentColumn) &&
            e.Control is TextBox textBox)
        {
            BeginInvoke(new Action(textBox.SelectAll));
            return;
        }

        if (_pendingComboOpenCell is not { } pendingCell ||
            _grid.CurrentCell is null ||
            _grid.CurrentCell.RowIndex != pendingCell.RowIndex ||
            _grid.CurrentCell.ColumnIndex != pendingCell.ColumnIndex ||
            _grid.CurrentCell.OwningColumn is not DataGridViewComboBoxColumn ||
            e.Control is not ComboBox comboBox)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (_grid.CurrentCell?.RowIndex == pendingCell.RowIndex &&
                _grid.CurrentCell.ColumnIndex == pendingCell.ColumnIndex &&
                _grid.EditingControl == comboBox)
            {
                comboBox.DroppedDown = true;
            }
        }));
        _pendingComboOpenCell = null;
    }

    private void HandleGridMouseMove(object? sender, MouseEventArgs e)
    {
        _grid.Cursor = IsClickableGridPoint(e.Location) ? Cursors.Hand : Cursors.Default;
    }

    private void HandleGridMouseLeave(object? sender, EventArgs e)
    {
        _grid.Cursor = Cursors.Default;
    }

    private bool IsClickableGridPoint(Point point)
    {
        var hit = _grid.HitTest(point.X, point.Y);
        if (hit.ColumnIndex < 0)
        {
            return false;
        }

        var column = _grid.Columns[hit.ColumnIndex];
        if (hit.RowIndex == -1)
        {
            return !_selectionOnlyMode && column.Name == SelectionColumnName;
        }

        if (hit.RowIndex < 0)
        {
            return false;
        }

        if (!_selectionOnlyMode && column.Name == SelectionColumnName)
        {
            return true;
        }

        return TryGetProxyCopyButtonBounds(hit.RowIndex, hit.ColumnIndex, out var proxyCopyBounds) && proxyCopyBounds.Contains(point) ||
            column.DataPropertyName == nameof(ProxyRow.Status) &&
                TryGetStatusCheckButtonBounds(hit.RowIndex, hit.ColumnIndex, out var statusButtonBounds) && statusButtonBounds.Contains(point) ||
            TryGetUsageViewButtonBounds(hit.RowIndex, hit.ColumnIndex, out var usageButtonBounds) && usageButtonBounds.Contains(point) ||
            TryGetInlineEditIconBounds(hit.RowIndex, hit.ColumnIndex, out var inlineEditBounds) && inlineEditBounds.Contains(point) ||
            column.Name == ActionsColumnName && IsActionButtonPoint(hit.RowIndex, hit.ColumnIndex, point);
    }

    private void HandleCellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var column = _grid.Columns[e.ColumnIndex];
        if (column.DataPropertyName == nameof(ProxyRow.UsageCount) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyRow row &&
            row.Applications.Count > 0)
        {
            e.ToolTipText = string.Join(Environment.NewLine, row.Applications.Select(application => application.DisplayName));
        }
    }

    private void SelectSingleProxyRow(int rowIndex)
    {
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not ProxyRow selectedRow)
        {
            return;
        }

        _selectedProxyIds.Clear();
        _selectedProxyIds.Add(selectedRow.Id);
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is ProxyRow row)
            {
                row.IsSelected = row.Id == selectedRow.Id;
                gridRow.Selected = row.IsSelected;
            }
        }

        _grid.CurrentCell = _grid.Rows[rowIndex].Cells.Cast<DataGridViewCell>().FirstOrDefault(cell => cell.Visible);
        _grid.Refresh();
        InvalidateSelectionHeader();
        UpdateSelectionDependentActions();
    }

    private void HandleColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (_selectionOnlyMode ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].Name != SelectionColumnName)
        {
            return;
        }

        var shouldSelectAll = _rows.Any(row => !row.IsSelected);
        foreach (var row in _rows)
        {
            row.IsSelected = shouldSelectAll;
            if (shouldSelectAll)
            {
                _selectedProxyIds.Add(row.Id);
            }
            else
            {
                _selectedProxyIds.Remove(row.Id);
            }
        }

        _grid.Refresh();
        InvalidateSelectionHeader();
        UpdateSelectionDependentActions();
    }

    private async Task HandleGridKeyDownAsync(KeyEventArgs e)
    {
        if (e.Control &&
            e.KeyCode == Keys.C &&
            TryGetCurrentProxyCellText(out var proxyText))
        {
            CopyTextToClipboard(proxyText);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode != Keys.Delete || GetSelectedProxies().Count == 0)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        await DeleteSelectedAsync();
    }

    private void ValidateEditableCell(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ProxyRow.Proxy))
        {
            return;
        }

        var value = Convert.ToString(e.FormattedValue) ?? string.Empty;
        if (!new ProxyServer { Proxy = value.Trim() }.HasValidEndpoint)
        {
            e.Cancel = true;
            MessageBox.Show(this, "Proxy phải có dạng host:port.", "Dữ liệu không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task HandleCellEndEditAsync(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].DataBoundItem is not ProxyRow row)
        {
            return;
        }

        var proxy = _manualProxies.First(item => item.Id == row.Id);
        proxy.Protocol = row.Protocol;
        proxy.Username = NormalizeOptional(row.Username);
        proxy.Password = NormalizeOptional(row.Password);
        await SaveManualProxiesAsync();
        BindGrid();
        OnProfileAffectingChanged(ProfileAffectingChange.ForProxies("Cập nhật proxy thủ công", [row.Id]));
    }

    private void BindGrid()
    {
        _selectedProxyIds.RemoveWhere(id => _manualProxies.All(proxy => proxy.Id != id));
        var filtered = _manualProxies
            .Select((proxy, index) => new ProxyRow(proxy, index + 1, _usageByProxyId.GetValueOrDefault(proxy.Id), _selectedProxyIds.Contains(proxy.Id)))
            .Where(MatchesFilters)
            .ToList();
        _filteredCount = filtered.Count;
        var totalPages = GetTotalPages();
        _page = Math.Max(1, Math.Min(_page, totalPages));
        _rows = filtered
            .Skip((_page - 1) * _limit)
            .Take(_limit)
            .ToList();

        _grid.DataSource = null;
        _grid.DataSource = _rows;
        _grid.Visible = true;
        _emptyState.Visible = false;
        _grid.BringToFront();
        _grid.Invalidate();

        _statusLabel.Text = _filteredCount == _manualProxies.Count
            ? $"{_manualProxies.Count} proxy thủ công"
            : $"{_filteredCount}/{_manualProxies.Count} proxy thủ công";
        InvalidateSelectionHeader();
        UpdatePager();
        UpdateSelectionDependentActions();
    }

    private bool MatchesFilters(ProxyRow row)
    {
        var filterText = _textFilterTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(filterText))
        {
            var selectedColumn = _textFilterColumnCombo.SelectedValue as string ?? nameof(ProxyRow.Proxy);
            var candidate = selectedColumn switch
            {
                nameof(ProxyRow.Username) => row.Username,
                nameof(ProxyRow.Password) => row.Password,
                _ => row.ProxyDisplay
            };

            if (!LocalFuzzySearch.IsMatch(candidate, filterText))
            {
                return false;
            }
        }

        if (_protocolFilterCombo.SelectedIndex > 0 &&
            !string.Equals(row.Protocol.ToDisplayName(), Convert.ToString(_protocolFilterCombo.SelectedItem), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _statusFilterCombo.SelectedIndex <= 0 ||
            string.Equals(row.Status, Convert.ToString(_statusFilterCombo.SelectedItem), StringComparison.OrdinalIgnoreCase);
    }

    private void ResetPageAndBindGrid()
    {
        _page = ProxyOrderListConstants.DefaultPage;
        BindGrid();
    }

    private void ClearFilters()
    {
        _textFilterDebounceTimer.Stop();
        _textFilterTextBox.Clear();
        _textFilterColumnCombo.SelectedIndex = 0;
        _protocolFilterCombo.SelectedIndex = 0;
        _statusFilterCombo.SelectedIndex = 0;
        ResetPageAndBindGrid();
    }

    private async Task AddProxyAsync()
    {
        using var form = _serviceProvider.GetRequiredService<AddProxyForm>();
        if (form.ShowDialog(this) != DialogResult.OK || form.ImportedProxies.Count == 0)
        {
            return;
        }

        var existingKeys = _manualProxies.Select(CreateProxyKey).ToHashSet();
        var addedCount = 0;
        var addedProxies = new List<ProxyServer>();
        foreach (var proxy in form.ImportedProxies)
        {
            proxy.IsFromBackend = false;
            proxy.BackendUserProxyId = null;
            proxy.BackendOrderKind = null;
            proxy.BackendProvider = null;
            proxy.ExpiredAt = null;
            if (!existingKeys.Add(CreateProxyKey(proxy)))
            {
                continue;
            }

            _manualProxies.Add(proxy);
            addedProxies.Add(proxy);
            addedCount++;
        }

        if (addedCount == 0)
        {
            UiFeedback.ShowInfo(this, "Các proxy này đã tồn tại trong Proxy của tôi.");
            return;
        }

        await SaveManualProxiesAsync();
        _selectedProxyIds.Clear();
        foreach (var proxy in addedProxies)
        {
            _selectedProxyIds.Add(proxy.Id);
        }

        if (addedProxies.Count > 0)
        {
            _textFilterDebounceTimer.Stop();
            _textFilterTextBox.Clear();
            _textFilterColumnCombo.SelectedIndex = 0;
            _protocolFilterCombo.SelectedIndex = 0;
            _statusFilterCombo.SelectedIndex = 0;
            _page = Math.Max(1, (int)Math.Ceiling(_manualProxies.Count / (double)Math.Max(1, _limit)));
        }

        BindGrid();
        if (addedProxies.FirstOrDefault() is { } firstAddedProxy)
        {
            FocusProxyRow(firstAddedProxy.Id);
        }

        OnProfileAffectingChanged(ProfileAffectingChange.ForProxies(
            "Thêm proxy thủ công",
            form.ImportedProxies.Select(proxy => proxy.Id)));
        UiFeedback.ShowInfo(this, $"Đã thêm {addedCount} proxy.");
    }

    private async Task EditSelectedAsync()
    {
        var selected = GetSelectedProxies().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        using var form = _serviceProvider.GetRequiredService<ProxyEditorForm>();
        if (selected.Count == 1)
        {
            form.LoadProxy(selected[0]);
        }
        else
        {
            form.LoadProxies(selected);
        }

        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await SaveManualProxiesAsync();
        BindGrid();
        OnProfileAffectingChanged(ProfileAffectingChange.ForProxies(
            "Cập nhật proxy thủ công",
            selected.Select(proxy => proxy.Id)));
        UiFeedback.ShowInfo(this, selected.Count == 1 ? "Đã cập nhật proxy." : $"Đã cập nhật {selected.Count} proxy.");
    }

    private async Task EditRowAsync(int rowIndex)
    {
        if (!TryGetProxyFromRow(rowIndex, out var proxy))
        {
            return;
        }

        using var form = _serviceProvider.GetRequiredService<ProxyEditorForm>();
        form.LoadProxy(proxy);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await SaveManualProxiesAsync();
        BindGrid();
        OnProfileAffectingChanged(ProfileAffectingChange.ForProxies("Cập nhật proxy thủ công", [proxy.Id]));
    }

    private async Task DeleteSelectedAsync()
    {
        var selected = GetSelectedProxies().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        await DeleteProxiesAsync(selected);
    }

    private async Task DeleteRowAsync(int rowIndex)
    {
        if (TryGetProxyFromRow(rowIndex, out var proxy))
        {
            await DeleteProxiesAsync([proxy]);
        }
    }

    private async Task DeleteProxiesAsync(IReadOnlyCollection<ProxyServer> selected)
    {
        var answer = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa {selected.Count} proxy đã chọn?",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        foreach (var proxy in selected)
        {
            _manualProxies.Remove(proxy);
            _selectedProxyIds.Remove(proxy.Id);
        }

        await SaveManualProxiesAsync();
        BindGrid();
        OnProfileAffectingChanged(ProfileAffectingChange.ForProxies(
            "Xóa proxy thủ công",
            selected.Select(proxy => proxy.Id)));
        UiFeedback.ShowInfo(this, $"Đã xóa {selected.Count} proxy.");
    }

    private void CopySelectedProxies()
    {
        var selected = GetSelectedProxies();
        if (selected.Count == 0)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất một proxy để copy.");
            return;
        }

        var lines = selected
            .Select(CreateProxyDisplayValue)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        CopyTextToClipboard(string.Join(Environment.NewLine, lines));
    }

    private async Task CheckSelectedAsync()
    {
        var selected = GetSelectedProxies().ToList();
        if (selected.Count == 0)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất một proxy để kiểm tra.");
            return;
        }

        try
        {
            _isBusy = true;
            UpdateSelectionDependentActions();
            _statusLabel.Text = "\u0110ang ki\u1ec3m tra proxy...";
            SetRowsCheckingState(selected.Select(proxy => proxy.Id).ToHashSet());
            await _proxyManagementService.CheckAsync(selected, CancellationToken.None);
            await LoadAsync();
            UiFeedback.ShowInfo(this, "Đã kiểm tra proxy.");
        }
        finally
        {
            _isBusy = false;
            UpdateSelectionDependentActions();
        }
    }
    private async Task CheckRowAsync(int rowIndex)
    {
        if (!TryGetProxyFromRow(rowIndex, out var proxy))
        {
            return;
        }

        try
        {
            _isBusy = true;
            UpdateSelectionDependentActions();
            _statusLabel.Text = "\u0110ang ki\u1ec3m tra proxy...";
            SetRowsCheckingState(new HashSet<Guid> { proxy.Id });
            await _proxyManagementService.CheckAsync([proxy], CancellationToken.None);
            await LoadAsync();
            OnProfileAffectingChanged(ProfileAffectingChange.ForProxies("Kiểm tra proxy thủ công", [proxy.Id]));
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể kiểm tra proxy");
        }
        finally
        {
            _isBusy = false;
            UpdateSelectionDependentActions();
        }
    }
    private void SetRowsCheckingState(IReadOnlySet<Guid> proxyIds)
    {
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is not ProxyRow row || !proxyIds.Contains(row.Id))
            {
                continue;
            }

            row.Status = CheckingStatusText;
            row.LatencyMs = string.Empty;
            gridRow.Cells[nameof(ProxyRow.Status)].Value = row.Status;
            gridRow.Cells[nameof(ProxyRow.LatencyMs)].Value = row.LatencyMs;
            _grid.InvalidateCell(gridRow.Cells[nameof(ProxyRow.Status)]);
            _grid.InvalidateCell(gridRow.Cells[nameof(ProxyRow.LatencyMs)]);
        }
    }

    private void UpdateSelectionDependentActions()
    {
        if (_deleteSelectedButton is null || _checkSelectedButton is null || _copySelectedButton is null)
        {
            return;
        }

        var hasSelection = !_isBusy && GetSelectedProxies().Count > 0;
        _deleteSelectedButton.Enabled = hasSelection;
        _checkSelectedButton.Enabled = hasSelection;
        _copySelectedButton.Enabled = hasSelection;
        if (_refreshButton is not null)
        {
            _refreshButton.Enabled = !_isBusy;
        }

        if (_addProxyButton is not null)
        {
            _addProxyButton.Enabled = !_isBusy;
        }

        UpdatePager();
        SelectionAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveManualProxiesAsync()
    {
        var all = await _proxyManagementService.GetAllAsync(CancellationToken.None);
        var backendProxies = all.Where(proxy => proxy.IsFromBackend).ToList();
        _manualProxies = _manualProxies
            .Where(proxy => !proxy.IsFromBackend)
            .DistinctBy(CreateProxyKey)
            .ToList();
        await _proxyManagementService.SaveAllAsync(backendProxies.Concat(_manualProxies).ToList(), CancellationToken.None);
    }

    private List<ProxyServer> GetSelectedProxies()
    {
        if (_selectionOnlyMode && GetCurrentProxy() is { } current)
        {
            return [current];
        }

        if (_selectedProxyIds.Count == 0)
        {
            var gridSelectedIds = _grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem)
                .OfType<ProxyRow>()
                .Select(row => row.Id)
                .ToHashSet();
            return _manualProxies.Where(proxy => gridSelectedIds.Contains(proxy.Id)).ToList();
        }

        return _manualProxies.Where(proxy => _selectedProxyIds.Contains(proxy.Id)).ToList();
    }

    private ProxyServer? GetCurrentProxy()
    {
        if (_grid.CurrentRow?.DataBoundItem is ProxyRow currentRow)
        {
            return _manualProxies.FirstOrDefault(proxy => proxy.Id == currentRow.Id);
        }

        if (_grid.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault()?.DataBoundItem is ProxyRow selectedRow)
        {
            return _manualProxies.FirstOrDefault(proxy => proxy.Id == selectedRow.Id);
        }

        return _selectedProxyIds.Count == 0
            ? null
            : _manualProxies.FirstOrDefault(proxy => _selectedProxyIds.Contains(proxy.Id));
    }

    private bool TryGetProxyFromRow(int rowIndex, out ProxyServer proxy)
    {
        proxy = null!;
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not ProxyRow row)
        {
            return false;
        }

        proxy = _manualProxies.FirstOrDefault(item => item.Id == row.Id)!;
        return proxy is not null;
    }

    private void PaintSelectionHeaderCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 ||
            e.ColumnIndex < 0 ||
            _selectionOnlyMode ||
            _grid.Columns[e.ColumnIndex].Name != SelectionColumnName ||
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

    private void PaintProxyCopyCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ProxyRow.ProxyDisplay) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ProxyRow row ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var buttonBounds = GetProxyCopyButtonBounds(e.CellBounds);
        var textBounds = new Rectangle(
            e.CellBounds.Left + 8,
            e.CellBounds.Top,
            Math.Max(0, buttonBounds.Left - e.CellBounds.Left - 12),
            e.CellBounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            row.ProxyDisplay,
            e.CellStyle?.Font ?? _grid.Font,
            textBounds,
            e.CellStyle?.ForeColor ?? _grid.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        GridCellButtonRenderer.DrawImageButton(e.Graphics, buttonBounds, _copyIcon);
        e.Handled = true;
    }

    private CheckBoxState GetSelectionHeaderState()
    {
        if (_rows.Count == 0 || _rows.All(row => !row.IsSelected))
        {
            return CheckBoxState.UncheckedNormal;
        }

        return _rows.All(row => row.IsSelected)
            ? CheckBoxState.CheckedNormal
            : CheckBoxState.MixedNormal;
    }

    private void InvalidateSelectionHeader()
    {
        if (_grid.Columns[SelectionColumnName] is { } column)
        {
            _grid.InvalidateCell(column.Index, -1);
        }
    }

    private void PaintEditableCellIcon(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            !IsInlineEditableColumn(_grid.Columns[e.ColumnIndex]) ||
            e.Graphics is null)
        {
            return;
        }

        e.Paint(e.CellBounds, e.PaintParts);
        DrawInlineEditIcon(e.Graphics, e.CellBounds);
        e.Handled = true;
    }

    private void PaintStatusCheckCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ProxyRow.Status) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ProxyRow row ||
            !ShouldShowStatusCheckButton(row) ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var isChecking = IsCheckingStatus(row);
        var buttonBounds = GetStatusCheckButtonBounds(e.CellBounds, isChecking);
        var textBounds = new Rectangle(
            e.CellBounds.Left + 8,
            e.CellBounds.Top,
            Math.Max(0, buttonBounds.Left - e.CellBounds.Left - 12),
            e.CellBounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            row.Status,
            e.CellStyle?.Font ?? _grid.Font,
            textBounds,
            e.CellStyle?.ForeColor ?? _grid.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (isChecking)
        {
            GridCellButtonRenderer.DrawOutlineIconButton(e.Graphics, buttonBounds, UiIcons.NewLoadingWarning, Color.FromArgb(236, 113, 0));
        }
        else
        {
            GridCellButtonRenderer.DrawOutlineButton(e.Graphics, buttonBounds, "Check");
        }

        e.Handled = true;
    }

    private void PaintUsageCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ProxyRow.UsageCount) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ProxyRow row ||
            !ShouldShowUsageViewButton(row) ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var buttonBounds = GetUsageViewButtonBounds(e.CellBounds);
        var chipBounds = new Rectangle(
            e.CellBounds.Left + 8,
            e.CellBounds.Top + Math.Max(6, (e.CellBounds.Height - 30) / 2),
            Math.Max(0, buttonBounds.Left - e.CellBounds.Left - 12),
            30);
        DrawUsageChip(e.Graphics, chipBounds, row);
        GridCellButtonRenderer.DrawImageButton(e.Graphics, buttonBounds, UiIcons.NewViewProxy);
        e.Handled = true;
    }

    private void PaintActionCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].Name != ActionsColumnName ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var buttons = GetActionIconButtonBounds(e.CellBounds);
        DesignedGridTheme.DrawGrayIconButton(e.Graphics, buttons[0], UiIcons.NewEdit);
        DesignedGridTheme.DrawGrayIconButton(e.Graphics, buttons[1], UiIcons.NewRemove);

        e.Handled = true;
    }

    private static Rectangle GetUsageViewButtonBounds(Rectangle cellBounds)
    {
        var size = Math.Min(UsageViewButtonSize, Math.Max(24, cellBounds.Height - 12));
        return new Rectangle(
            cellBounds.Right - size - 8,
            cellBounds.Top + Math.Max(4, (cellBounds.Height - size) / 2),
            size,
            size);
    }

    private bool TryGetUsageViewButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Columns[columnIndex].DataPropertyName != nameof(ProxyRow.UsageCount) ||
            _grid.Rows[rowIndex].DataBoundItem is not ProxyRow row ||
            !ShouldShowUsageViewButton(row))
        {
            return false;
        }

        bounds = GetUsageViewButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private static bool ShouldShowUsageViewButton(ProxyRow row) =>
        row.Applications.Count > 0;

    private static void DrawUsageChip(Graphics graphics, Rectangle bounds, ProxyRow row)
    {
        if (bounds.Width <= 0 || row.Applications.Count == 0)
        {
            return;
        }

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = AppButton.CreateRoundedRectangle(bounds, 6))
        using (var background = new SolidBrush(Color.FromArgb(247, 247, 247)))
        {
            graphics.FillPath(background, path);
        }

        var application = row.Applications[0];
        var iconBounds = new Rectangle(
            bounds.Left + 4,
            bounds.Top + (bounds.Height - UsageAppIconSize) / 2,
            UsageAppIconSize,
            UsageAppIconSize);
        graphics.DrawImage(application.Icon, iconBounds);

        var extraCount = row.Applications.Count - 1;
        var extraText = extraCount > 0 ? $"+{extraCount}" : string.Empty;
        var font = DesignedGridTheme.CellFont;
        var extraSize = string.IsNullOrWhiteSpace(extraText)
            ? Size.Empty
            : TextRenderer.MeasureText(graphics, extraText, font, Size.Empty, TextFormatFlags.NoPadding);
        var extraWidth = extraSize == Size.Empty ? 0 : extraSize.Width + 6;
        var textBounds = new Rectangle(
            iconBounds.Right + 4,
            bounds.Top,
            Math.Max(0, bounds.Width - (iconBounds.Right - bounds.Left) - 8 - extraWidth),
            bounds.Height);

        TextRenderer.DrawText(
            graphics,
            application.DisplayName,
            font,
            textBounds,
            DesignedGridTheme.TextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (!string.IsNullOrWhiteSpace(extraText))
        {
            var extraBounds = new Rectangle(
                bounds.Right - extraWidth - 4,
                bounds.Top,
                extraWidth,
                bounds.Height);
            TextRenderer.DrawText(
                graphics,
                extraText,
                font,
                extraBounds,
                DesignedGridTheme.MutedColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private bool TryGetStatusCheckButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not ProxyRow row ||
            !ShouldShowStatusCheckButton(row))
        {
            return false;
        }

        bounds = GetStatusCheckButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true), IsCheckingStatus(row));
        return true;
    }

    private bool TryGetProxyCopyButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Columns[columnIndex].DataPropertyName != nameof(ProxyRow.ProxyDisplay))
        {
            return false;
        }

        bounds = GetProxyCopyButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private static Rectangle GetProxyCopyButtonBounds(Rectangle cellBounds)
    {
        return GridCellButtonRenderer.GetRightAlignedIconButtonBounds(cellBounds);
    }

    private static bool ShouldShowStatusCheckButton(ProxyRow row) =>
        true;

    private static bool IsCheckingStatus(ProxyRow row) =>
        string.Equals(row.Status, FormatStatus(ProxyStatus.Checking), StringComparison.OrdinalIgnoreCase);

    private static Rectangle GetStatusCheckButtonBounds(Rectangle cellBounds, bool iconOnly = false)
    {
        return iconOnly
            ? GridCellButtonRenderer.GetRightAlignedIconButtonBounds(cellBounds)
            : GridCellButtonRenderer.GetRightAlignedButtonBounds(cellBounds, "Check");
    }

    private static bool IsInlineEditableColumn(DataGridViewColumn column) =>
        !column.ReadOnly &&
        column.DataPropertyName is nameof(ProxyRow.Username) or nameof(ProxyRow.Password);

    private static void DrawInlineEditIcon(Graphics graphics, Rectangle cellBounds)
    {
        graphics.DrawImage(UiIcons.NewInlineEdit, GetInlineEditIconBounds(cellBounds));
    }

    private static Rectangle GetInlineEditIconBounds(Rectangle cellBounds) =>
        new(cellBounds.Right - 24, cellBounds.Top + ((cellBounds.Height - 18) / 2), 18, 18);

    private bool TryGetInlineEditIconBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            !IsInlineEditableColumn(_grid.Columns[columnIndex]))
        {
            return false;
        }

        bounds = GetInlineEditIconBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private void BeginInlineEdit(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || columnIndex < 0 || rowIndex >= _grid.Rows.Count)
        {
            return;
        }

        _grid.CurrentCell = _grid.Rows[rowIndex].Cells[columnIndex];
        _grid.BeginEdit(selectAll: true);
    }

    private static IEnumerable<(Rectangle Bounds, string Text)> GetActionButtonLayouts(Rectangle cellBounds)
    {
        return GridCellButtonRenderer.GetCenteredButtonLayouts(cellBounds, ["Sửa", "Xóa"]);
    }

    private bool TryGetActionButton(Rectangle cellBounds, out RowAction action)
    {
        var point = _grid.PointToClient(Cursor.Position);
        var buttons = GetActionIconButtonBounds(cellBounds);
        if (buttons[0].Contains(point))
        {
            action = RowAction.Edit;
            return true;
        }

        if (buttons[1].Contains(point))
        {
            action = RowAction.Delete;
            return true;
        }

        action = default;
        return false;
    }

    private bool IsActionButtonPoint(int rowIndex, int columnIndex, Point point)
    {
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count)
        {
            return false;
        }

        var cellBounds = _grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true);
        return GetActionIconButtonBounds(cellBounds).Any(bounds => bounds.Contains(point));
    }

    private static IReadOnlyList<Rectangle> GetActionIconButtonBounds(Rectangle cellBounds) =>
        GridCellButtonRenderer.GetCenteredIconButtonBounds(
            cellBounds,
            2,
            buttonSize: DesignedGridTheme.IconButtonSize,
            gap: 4);

    private bool TryGetCurrentProxyCellText(out string text)
    {
        text = string.Empty;
        if (_grid.CurrentCell is not { } cell ||
            cell.RowIndex < 0 ||
            cell.ColumnIndex < 0 ||
            _grid.Columns[cell.ColumnIndex].DataPropertyName != nameof(ProxyRow.ProxyDisplay) ||
            _grid.Rows[cell.RowIndex].DataBoundItem is not ProxyRow row ||
            string.IsNullOrWhiteSpace(row.ProxyDisplay))
        {
            return false;
        }

        text = row.ProxyDisplay;
        return true;
    }

    private void ShowProxyCellTextSelector(int rowIndex, int columnIndex, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var bounds = _grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _proxyCellTextSelector.Bounds = new Rectangle(
            bounds.Left + 1,
            bounds.Top + 1,
            Math.Max(1, bounds.Width - 2),
            Math.Max(1, bounds.Height - 2));
        _proxyCellTextSelector.Font = _grid.Font;
        _proxyCellTextSelector.Text = text;
        _proxyCellTextSelector.Visible = true;
        _proxyCellTextSelector.BringToFront();
        _proxyCellTextSelector.Focus();
        _proxyCellTextSelector.SelectAll();
    }

    private void HideProxyCellTextSelector()
    {
        if (!_proxyCellTextSelector.Visible)
        {
            return;
        }

        _proxyCellTextSelector.Visible = false;
        _proxyCellTextSelector.Clear();
    }

    private void CopyTextToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            UiFeedback.ShowInfo(this, "Không có dữ liệu để copy.");
            return;
        }

        Clipboard.SetText(text);
        UiFeedback.ShowInfo(this, "Đã copy vào clipboard.");
    }

    private static string? NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) || value == "-" ? null : value.Trim();

    private static ProxyKey CreateProxyKey(ProxyServer proxy) =>
        new(
            proxy.Proxy.Trim().ToLowerInvariant(),
            (proxy.Username ?? string.Empty).Trim(),
            (proxy.Password ?? string.Empty).Trim(),
            proxy.Protocol);

    private static string CreateProxyDisplayValue(ProxyServer proxy) =>
        string.IsNullOrWhiteSpace(proxy.Username) && string.IsNullOrWhiteSpace(proxy.Password)
            ? proxy.Proxy
            : $"{proxy.Proxy}:{proxy.Username}:{proxy.Password}";

    private static string FormatStatus(ProxyStatus status) =>
        status switch
        {
            ProxyStatus.Unknown => "Chưa check",
            ProxyStatus.Checking => CheckingStatusText,
            _ => status.ToString()
        };

    private static string FormatLatency(int? latencyMs) =>
        latencyMs is null ? string.Empty : $"{latencyMs.Value} ms";

    private void OnProfileAffectingChanged(ProfileAffectingChange change) =>
        ProfileAffectingChanged?.Invoke(this, change);

    private enum RowAction
    {
        Edit,
        Delete
    }

    private sealed record ProxyKey(string Endpoint, string Username, string Password, ProxyProtocol Protocol);

    private sealed record ProxyApplicationUsage(string DisplayName, Image Icon);

    private sealed record ProxyUsageInfo(IReadOnlyList<ProxyApplicationUsage> Applications)
    {
        public int ApplicationCount => Applications.Count;
    }

    private sealed class ProxyRow
    {
        public ProxyRow(ProxyServer proxy, int order, ProxyUsageInfo? usage, bool isSelected)
        {
            Id = proxy.Id;
            Order = order;
            IsSelected = isSelected;
            Proxy = proxy.Proxy;
            Protocol = proxy.Protocol;
            Username = proxy.Username ?? string.Empty;
            Password = proxy.Password ?? string.Empty;
            Status = FormatStatus(proxy.Status);
            LatencyMs = FormatLatency(proxy.LatencyMs);
            UsageCount = usage?.ApplicationCount ?? 0;
            Applications = usage?.Applications ?? [];
        }

        public Guid Id { get; }
        public int Order { get; }
        public bool IsSelected { get; set; }
        public string Proxy { get; set; }
        public string ProxyDisplay => string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password)
            ? Proxy
            : $"{Proxy}:{Username}:{Password}";
        public string Username { get; set; }
        public string Password { get; set; }
        public ProxyProtocol Protocol { get; set; }
        public string Status { get; set; }
        public string LatencyMs { get; set; }
        public int UsageCount { get; }
        public IReadOnlyList<ProxyApplicationUsage> Applications { get; }
        public string Actions => string.Empty;
    }

    private sealed record FilterOption(string DisplayName, string Value);
}
