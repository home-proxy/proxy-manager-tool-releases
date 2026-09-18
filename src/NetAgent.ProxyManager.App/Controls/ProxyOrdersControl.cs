using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.App.Configuration;
using NetAgent.ProxyManager.App.Forms;
using NetAgent.ProxyManager.App.Services;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;
using NetAgent.ProxyManager.Infrastructure.Api;
using CheckBoxState = System.Windows.Forms.VisualStyles.CheckBoxState;

namespace NetAgent.ProxyManager.App.Controls;

public sealed class ProxyOrdersControl : UserControl
{
    private const string SelectionColumnName = nameof(ProxyOrderRow.IsSelected);
    private const string ActionsColumnName = "Actions";
    private const string UncheckedStatusText = "Chưa check";
    private const string CheckingStatusText = "Đang check";
    private const string LoadingKeyText = "Đang tải key...";
    private const string EmptyKeyText = "Không có key";
    private const string KeyLoadErrorText = "Lỗi lấy key";
    private const string ViewUsageButtonText = "Xem";
    private const int MinimumProxyPasswordLength = 9;
    private const int DesignedToolbarButtonHeight = 40;
    private const int SearchToolbarRowHeight = 40;
    private const int ToolbarToSearchRowGap = 32;
    private const int SearchRowToGridGap = 32;
    private const int ProxyOrderRowHeight = 60;
    private const int RotateProxyOrderRowHeight = 74;
    private const int UsageAppIconSize = 22;
    private const int UsageViewButtonSize = 32;
    private static readonly string[] DatacenterProviders = ["US", "CMC"];
    private static readonly Color DisabledToolbarColor = Color.FromArgb(189, 189, 189);

    private readonly IProxyOrderApiClient _proxyOrderApiClient;
    private readonly IProxyOrderCacheService _proxyOrderCacheService;
    private readonly IAuthService _authService;
    private readonly IProxyRepository _proxyRepository;
    private readonly IApplicationRuleRepository _applicationRuleRepository;
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IProxyChecker _proxyChecker;
    private readonly ExpiredBackendProxyAssignmentService _expiredBackendProxyAssignmentService;
    private readonly ManualProxyRotateGuard _manualProxyRotateGuard;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _portalBaseUrl;
    private TableLayoutPanel _rootLayout = null!;
    private TableLayoutPanel _toolbarLayout = null!;
    private Control? _actionToolbarRow;
    private Control? _searchToolbarRow;
    private readonly DataGridView _grid = new AppDataGridView();
    private readonly Panel _emptyState = new();
    private readonly TableLayoutPanel _emptyStateLayout = new();
    private readonly LinkLabel _emptyStateLabel = new();
    private readonly AppButton _emptyStateLoginButton = new();
    private readonly ComboBox _searchFieldCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly AppTextInput _searchTextBox = new() { Width = 260 };
    private readonly ComboBox _columnVisibilityCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly ComboBox _pageSizeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly ContextMenuStrip _rowContextMenu = new();
    private readonly ContextMenuStrip _copyMenu = new();
    private readonly ToolTip _toolTip = new() { ShowAlways = true };
    private readonly Image _copyIcon = UiIcons.CopyGlyph;
    private readonly Dictionary<string, Image> _applicationIconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Image? _emptyArchiveIcon = SidebarIconRenderer.LoadOriginal("Empty archive.svg");
    private readonly TextBox _proxyCellTextSelector = new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        ReadOnly = true,
        Visible = false,
        TabStop = false
    };
    private readonly Label _titleLabel = new();
    private readonly Label _pageLabel = new();
    private readonly Label _statusLabel = AppDataGridFooter.CreateStatusLabel();
    private readonly Button _previousPageButton;
    private readonly Button _nextPageButton;
    private readonly System.Windows.Forms.Timer _searchDebounceTimer = new() { Interval = 900 };
    private Button _refreshButton = null!;
    private Button _checkButton = null!;
    private Button _changeInfoButton = null!;
    private Button _renewButton = null!;
    private Button _rotateButton = null!;
    private Button _fetchProxyButton = null!;
    private Button _copyButton = null!;
    private Button _purchaseButton = null!;
    private Button _clearFilterButton = null!;
    private TableLayoutPanel _bottomBar = null!;

    private ProxyOrderKind _kind = ProxyOrderKind.Static;
    private int _page = ProxyOrderListConstants.DefaultPage;
    private int _limit = ProxyOrderListConstants.DefaultLimit;
    private bool _hasNextPage;
    private int? _total;
    private bool _isBinding;
    private List<ProxyOrder> _orders = [];
    private List<ProxyOrderRow> _rows = [];
    private Dictionary<string, LocalProxyUsage> _localProxyUsage = [];
    private Dictionary<int, LocalProxyUsage> _localProxyUsageByBackendId = [];
    private Dictionary<int, LocalProxyUsage> _checkedOrderUsage = [];
    private Dictionary<int, string> _rotateKeyTokenCache = [];
    private Dictionary<int, string> _rotateKeyTextCache = [];
    private Point? _selectionDragStartPoint;
    private bool _isDraggingRowSelection;
    private bool _isBusy;
    private bool _requiresLogin;
    private bool _isSettingSearch;
    private bool _pickerMode;
    private int _bindVersion;
    private string _currentGridToolTipText = string.Empty;
    private RowAction? _currentGridToolTipAction;
    private Rectangle _currentGridToolTipBounds = Rectangle.Empty;
    private (int RowIndex, bool NewValue)? _pendingCheckboxToggle;
    private (int RowIndex, int ColumnIndex)? _pendingComboOpenCell;
    private string _gridEmptyStateText = string.Empty;

    public ProxyOrdersControl(
        IProxyOrderApiClient proxyOrderApiClient,
        IProxyOrderCacheService proxyOrderCacheService,
        IAuthService authService,
        IProxyRepository proxyRepository,
        IApplicationRuleRepository applicationRuleRepository,
        IAppSettingsRepository appSettingsRepository,
        IProxyChecker proxyChecker,
        ExpiredBackendProxyAssignmentService expiredBackendProxyAssignmentService,
        ManualProxyRotateGuard manualProxyRotateGuard,
        IServiceProvider serviceProvider,
        IOptions<BackendApiOptions> backendApiOptions)
    {
        _proxyOrderApiClient = proxyOrderApiClient;
        _proxyOrderCacheService = proxyOrderCacheService;
        _authService = authService;
        _proxyRepository = proxyRepository;
        _applicationRuleRepository = applicationRuleRepository;
        _appSettingsRepository = appSettingsRepository;
        _proxyChecker = proxyChecker;
        _expiredBackendProxyAssignmentService = expiredBackendProxyAssignmentService;
        _manualProxyRotateGuard = manualProxyRotateGuard;
        _serviceProvider = serviceProvider;
        _portalBaseUrl = NormalizePortalBaseUrl(backendApiOptions.Value.PortalBaseUrl);
        _previousPageButton = AppDataGridFooter.CreatePagerButton(previous: true, async (_, _) => await MovePageAsync(-1));
        _nextPageButton = AppDataGridFooter.CreatePagerButton(previous: false, async (_, _) => await MovePageAsync(1));

        Dock = DockStyle.Fill;
        BuildUi();
        LightTheme.Apply(this);
        _grid.CellPainting -= PaintSelectionHeaderCell;
        _grid.CellPainting += PaintSelectionHeaderCell;
        _grid.CellPainting -= PaintEditableCellIcon;
        _grid.CellPainting += PaintEditableCellIcon;
        _grid.CellPainting -= PaintActionCell;
        _grid.CellPainting += PaintActionCell;
        _grid.CellPainting -= PaintProxyCopyCell;
        _grid.CellPainting += PaintProxyCopyCell;
        _grid.CellPainting -= PaintStatusCheckCell;
        _grid.CellPainting += PaintStatusCheckCell;
        _grid.CellPainting -= PaintUsageCell;
        _grid.CellPainting += PaintUsageCell;
        _grid.CellToolTipTextNeeded += HandleCellToolTipTextNeeded;
    }

    public event EventHandler<ProfileAffectingChange>? ProfileAffectingChanged;

    public event EventHandler<Guid>? ViewApplicationsRequested;

    public event EventHandler<ProxyOrderKind>? PurchaseRequested;

    public Func<Task<bool>>? LoginRequestedAsync { get; set; }

    public event EventHandler? RenewCompleted;

    public event EventHandler? SelectionAvailabilityChanged;

    public bool HasSelectedOrder => GetSelectedOrders().Any();

    public void Configure(ProxyOrderKind kind)
    {
        _kind = kind;
        ApplyModeConfiguration();
    }

    public void UsePickerLayout()
    {
        _pickerMode = true;
        _rootLayout.Padding = Padding.Empty;
        _toolbarLayout.Padding = Padding.Empty;
        if (_actionToolbarRow is not null)
        {
            _actionToolbarRow.Visible = false;
        }

        if (_searchToolbarRow is not null)
        {
            _searchToolbarRow.Visible = false;
        }

        _grid.MultiSelect = false;
        _grid.ReadOnly = true;
        _grid.AllowUserToResizeRows = false;
        _rowContextMenu.Enabled = false;
        ApplyModeConfiguration();
        UpdateSelectionDependentActions();
    }

    public async Task LoadPickerAsync(ProxyOrderKind kind, bool forceRefresh = false)
    {
        Configure(kind);
        ClearSearchFilter();
        await LoadLocalUsageAsync();
        _page = ProxyOrderListConstants.DefaultPage;
        await FetchPageAsync(forceRefresh);
    }

    public async Task RefreshPickerAsync() => await FetchPageAsync(forceRefresh: true);

    public async Task CheckSelectedPickerOrderAsync() => await CheckSelectedAsync();

    public async Task<ProxyOrderPickerSelection?> GetSelectedPickerSelectionAsync()
    {
        var order = GetSelectedOrders().FirstOrDefault();
        if (order is null)
        {
            return null;
        }

        var address = order.ProxyAddress;
        if (_kind == ProxyOrderKind.RotateProxy && string.IsNullOrWhiteSpace(address))
        {
            var current = await _proxyOrderApiClient.RotateProxyAsync(order.UserProxyId, checkOnly: true, CancellationToken.None);
            address = current.Proxy;
        }

        return string.IsNullOrWhiteSpace(address)
            ? null
            : new ProxyOrderPickerSelection(order, _kind, address);
    }

    public async Task LoadAsync()
    {
        ApplyModeConfiguration();
        await ApplySavedColumnVisibilityAsync();
        ClearSearchFilter();
        await LoadLocalUsageAsync();
        await FetchPageAsync();
    }

    public async Task LoadAndFocusProxyAsync(ProxyServer proxy)
    {
        ApplyModeConfiguration();
        await ApplySavedColumnVisibilityAsync();
        await LoadLocalUsageAsync();

        if (proxy.TryGetEndpoint(out var host, out _))
        {
            SetSearchFilter(ProxyOrderSearchField.Proxy, host);
        }

        _page = ProxyOrderListConstants.DefaultPage;
        await FetchPageAsync();
        FocusProxy(proxy);
    }

    private void BuildUi()
    {
        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 3,
            ColumnCount = 1
        };
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _rootLayout.Controls.Add(BuildToolbar(), 0, 0);

        ConfigureGrid();
        ConfigureEmptyState();
        var gridHost = new Panel { Dock = DockStyle.Fill };
        gridHost.Controls.Add(_grid);
        gridHost.Controls.Add(_emptyState);
        _rootLayout.Controls.Add(gridHost, 0, 1);

        _bottomBar = AppDataGridFooter.Create(BuildPageSizePanel(), _statusLabel, BuildPager());
        _bottomBar.Visible = false;
        _rootLayout.Controls.Add(_bottomBar, 0, 2);

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
            Padding = new Padding(0, 0, 0, SearchRowToGridGap)
        };
        _toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _toolbarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _toolbarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _searchFieldCombo.DataSource = new List<SearchFieldOption>
        {
            new("ID", ProxyOrderSearchField.Id),
            new("Mã đơn hàng", ProxyOrderSearchField.OrderCode),
            new("Proxy", ProxyOrderSearchField.Proxy),
            new("Proxy Domain", ProxyOrderSearchField.ProxyDomain)
        };
        _searchFieldCombo.DisplayMember = nameof(SearchFieldOption.DisplayName);
        _searchFieldCombo.ValueMember = nameof(SearchFieldOption.Value);
        _searchFieldCombo.SelectedIndexChanged += async (_, _) =>
        {
            UpdateSearchPlaceholder();
            if (_isSettingSearch)
            {
                return;
            }

            await ResetAndFetchAsync();
        };

        UpdateSearchPlaceholder();
        _searchTextBox.TextChanged += (_, _) =>
        {
            if (_isSettingSearch)
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

        _columnVisibilityCombo.SelectionChangeCommitted += async (_, _) => await ToggleSelectedColumnVisibilityAsync();

        _actionToolbarRow = BuildActionToolbarRow();
        _searchToolbarRow = BuildSearchToolbarRow();
        _toolbarLayout.Controls.Add(_actionToolbarRow, 0, 0);
        _toolbarLayout.Controls.Add(_searchToolbarRow, 0, 1);
        return _toolbarLayout;
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
            Margin = new Padding(0, 0, 0, ToolbarToSearchRowGap)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignedToolbarButtonHeight));

        _refreshButton = CreateToolbarRowButton("Làm mới", async (_, _) => await FetchPageAsync(forceRefresh: true), UiIcons.NewRefreshProxy, UiIcons.NewRefreshProxyHover, UiIcons.NewRefreshProxyDisabled, accentAtRest: false, minimumWidth: 108);
        _checkButton = CreateToolbarRowButton("Check proxy", async (_, _) => await CheckSelectedAsync(), UiIcons.NewCheckProxy, UiIcons.NewCheckProxyHover, UiIcons.NewCheckProxyDisabled, accentAtRest: false, minimumWidth: 132);
        _changeInfoButton = CreateToolbarRowButton("Đổi thông tin", async (_, _) => await ChangeSelectedInfoAsync(), UiIcons.NewChangeProxyInfo, UiIcons.NewChangeProxyInfoHover, UiIcons.NewChangeProxyInfoDisabled, accentAtRest: false, minimumWidth: 146);
        _renewButton = CreateToolbarRowButton("Gia hạn", async (_, _) => await RenewSelectedAsync(), UiIcons.NewRenewProxy, UiIcons.NewRenewProxyHover, UiIcons.NewRenewProxyDisabled, accentAtRest: false, minimumWidth: 108);
        _rotateButton = CreateToolbarRowButton("Xoay proxy", async (_, _) => await RotateSelectedAsync(), UiIcons.NewRotateProxy, UiIcons.NewRotateProxyHover, UiIcons.NewRotateProxyDisabled, accentAtRest: false, minimumWidth: 126);
        _fetchProxyButton = CreateToolbarRowButton("Lấy Proxy từ key", async (_, _) => await OpenFetchProxyFromKeyFormAsync(), UiIcons.NewRotateProxy, UiIcons.NewRotateProxyHover, UiIcons.NewRotateProxyDisabled, accentAtRest: false, minimumWidth: 154);
        _copyButton = CreateToolbarRowButton("Sao chép", (_, _) => ShowCopyMenu(), UiIcons.NewCopy, UiIcons.NewCopyHover, UiIcons.NewCopyDisabled, accentAtRest: false, minimumWidth: 116);
        _purchaseButton = CreateToolbarRowButton("Mua Proxy", (_, _) => OnPurchaseRequested(), UiIcons.NewAddData, UiIcons.NewAddDataHover, UiIcons.NewAddDataDisabled, accentAtRest: false, minimumWidth: 120);
        _purchaseButton.Anchor = AnchorStyles.Right;
        _toolTip.SetToolTip(_checkButton, "Check proxy cho các dòng đã chọn.");
        _toolTip.SetToolTip(_changeInfoButton, "Cập nhật thông tin proxy cho các dòng đã chọn.");
        _toolTip.SetToolTip(_renewButton, "Gia hạn các dòng đã chọn.");
        _toolTip.SetToolTip(_rotateButton, "Xoay proxy cho các dòng đã chọn.");
        _toolTip.SetToolTip(_copyButton, "Copy proxy, key, link xoay hoặc ID từ các dòng đã chọn.");
        _toolTip.SetToolTip(_purchaseButton, "Mở màn hình mua proxy trong ứng dụng.");
        var toolbar = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        toolbar.Controls.Add(CreateDesignedToolbarGroup(withRightBorder: true, _refreshButton));
        toolbar.Controls.Add(CreateDesignedToolbarGroup(
            withRightBorder: false,
            _checkButton,
            _changeInfoButton,
            _renewButton,
            _rotateButton,
            _fetchProxyButton,
            _copyButton));

        panel.Controls.Add(toolbar, 0, 0);
        panel.Controls.Add(new Panel { Dock = DockStyle.Fill, MinimumSize = Size.Empty, Margin = Padding.Empty }, 1, 0);
        panel.Controls.Add(_purchaseButton, 2, 0);
        return panel;
    }

    private Control BuildSearchToolbarRow()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = SearchToolbarRowHeight,
            ColumnCount = 7,
            RowCount = 1,
            Margin = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, SearchToolbarRowHeight));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _searchFieldCombo.Anchor = AnchorStyles.Left;
        _searchTextBox.Anchor = AnchorStyles.Left;
        _clearFilterButton = new AppClearFilterButton();
        _clearFilterButton.Click += async (_, _) => await ClearFiltersAsync();
        _clearFilterButton.Anchor = AnchorStyles.Left;
        _columnVisibilityCombo.Anchor = AnchorStyles.Left;
        _searchFieldCombo.Margin = new Padding(0, 0, 8, 0);
        _searchTextBox.Margin = new Padding(0, 0, 8, 0);
        _clearFilterButton.Margin = Padding.Empty;
        _columnVisibilityCombo.Margin = Padding.Empty;

        panel.Controls.Add(CreateToolbarLabel("Tìm kiếm:", new Padding(0, 0, 8, 0)), 0, 0);
        panel.Controls.Add(_searchFieldCombo, 1, 0);
        panel.Controls.Add(_searchTextBox, 2, 0);
        panel.Controls.Add(_clearFilterButton, 3, 0);
        panel.Controls.Add(new Panel { Dock = DockStyle.Fill, MinimumSize = Size.Empty, Margin = Padding.Empty }, 4, 0);
        panel.Controls.Add(CreateToolbarLabel("Hiển thị:", new Padding(8, 0, 8, 0)), 5, 0);
        panel.Controls.Add(_columnVisibilityCombo, 6, 0);
        return panel;
    }

    private static Control CreateDesignedToolbarGroup(bool withRightBorder, params Control[] buttons)
    {
        var group = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Height = DesignedToolbarButtonHeight,
            Margin = Padding.Empty,
            Padding = withRightBorder ? new Padding(0, 0, 4, 0) : new Padding(4, 0, 0, 0)
        };

        foreach (var button in buttons)
        {
            button.Margin = new Padding(0, 0, 8, 0);
            group.Controls.Add(button);
        }

        if (withRightBorder)
        {
            group.Paint += (_, e) =>
            {
                using var border = new Pen(DesignedGridTheme.BorderColor);
                e.Graphics.DrawLine(border, group.Width - 1, 0, group.Width - 1, group.Height);
            };
        }

        return group;
    }

    private static Button CreateToolbarRowButton(
        string text,
        EventHandler onClick,
        Image icon,
        Image hoverIcon,
        Image disabledIcon,
        bool accentAtRest,
        int minimumWidth)
    {
        var button = new DesignedToolbarButton
        {
            Text = text,
            Image = icon,
            HoverImage = hoverIcon,
            DisabledImage = disabledIcon,
            RestTextColor = accentAtRest ? DesignedGridTheme.AccentColor : DesignedGridTheme.TextColor,
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

    private static Label CreateToolbarLabel(string text, Padding margin) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = margin
        };

    private void UpdateSearchPlaceholder()
    {
        var searchField = (_searchFieldCombo.SelectedItem as SearchFieldOption)?.Value ?? ProxyOrderSearchField.Id;
        _searchTextBox.PlaceholderText = searchField switch
        {
            ProxyOrderSearchField.OrderCode => "Nhập mã đơn hàng...",
            ProxyOrderSearchField.Proxy => "Nhập IP proxy...",
            ProxyOrderSearchField.ProxyDomain => "Nhập proxy domain...",
            _ => "Nhập ID..."
        };
    }

    private Control BuildPageSizePanel()
    {
        _pageSizeCombo.DataSource = ProxyOrderListConstants.PageSizeOptions.ToList();
        _pageSizeCombo.SelectedItem = ProxyOrderListConstants.DefaultLimit;
        _pageSizeCombo.SelectionChangeCommitted += async (_, _) =>
        {
            _limit = (int)_pageSizeCombo.SelectedItem!;
            await ResetAndFetchAsync();
        };
        return AppDataGridFooter.CreatePageSizePanel(_pageSizeCombo);
    }

    private Control BuildPager()
    {
        return AppDataGridFooter.CreatePager(_pageLabel, _previousPageButton, _nextPageButton);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
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
        _grid.CellMouseDown += HandleGridCellMouseDown;
        _grid.CellMouseUp += HandleGridCellMouseUp;
        _grid.MouseMove += HandleGridMouseMove;
        _grid.MouseUp += HandleGridMouseUp;
        _grid.MouseLeave += HandleGridMouseLeave;
        _grid.KeyDown += HandleGridKeyDown;
        _grid.CellClick += HandleGridCellClick;
        _grid.CellDoubleClick += HandleCellDoubleClick;
        _grid.CellEndEdit += async (_, e) => await HandleCellEndEditAsync(e);
        _grid.EditingControlShowing += HandleEditingControlShowing;
        _grid.ColumnHeaderMouseClick += HandleColumnHeaderMouseClick;
        _grid.Scroll += (_, _) => HideProxyCellTextSelector();
        _grid.ColumnWidthChanged += (_, _) => HideProxyCellTextSelector();
        _grid.RowsAdded += (_, _) => HideProxyCellTextSelector();
        _grid.RowsRemoved += (_, _) => HideProxyCellTextSelector();
        _ = new GridEmptyStateOverlay(_grid, GetProxyOrderEmptyStateContent);
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

        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = SelectionColumnName,
            HeaderText = "",
            DataPropertyName = nameof(ProxyOrderRow.IsSelected),
            MinimumWidth = 42,
            FillWeight = 4,
            ReadOnly = true
        });
        _grid.Columns.Add(CreateTextColumn("Mã đơn hàng", nameof(ProxyOrderRow.OrderCode), 120, 10, true));
        _grid.Columns.Add(CreateTextColumn("Proxy", nameof(ProxyOrderRow.DisplayProxy), 230, 18, true));
        _grid.Columns.Add(CreateTextColumn("Username", nameof(ProxyOrderRow.Username), 120, 10));
        _grid.Columns.Add(CreateTextColumn("Password", nameof(ProxyOrderRow.Password), 120, 10));
        _grid.Columns.Add(CreateTextColumn("Nhà mạng", nameof(ProxyOrderRow.Provider), 95, 8, true));
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = nameof(ProxyOrderRow.Protocol),
            HeaderText = "Giao thức",
            DataPropertyName = nameof(ProxyOrderRow.Protocol),
            MinimumWidth = 105,
            FillWeight = 8,
            DataSource = ProxyProtocolDisplay.Options.ToList(),
            DisplayMember = nameof(ProxyProtocolOption.DisplayName),
            ValueMember = nameof(ProxyProtocolOption.Value),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            DisplayStyleForCurrentCellOnly = false
        });
        _grid.Columns.Add(CreateTextColumn("ID", nameof(ProxyOrderRow.Code), 90, 9, true, false));
        _grid.Columns.Add(CreateTextColumn("Loại", nameof(ProxyOrderRow.ProtocolDisplay), 105, 8, true, false));
        _grid.Columns.Add(CreateTextColumn("Ngày hết hạn", nameof(ProxyOrderRow.ExpiredAt), 128, 10, true));
        _grid.Columns.Add(CreateTextColumn("Trạng thái", nameof(ProxyOrderRow.Status), 160, 11, true));
        _grid.Columns.Add(CreateTextColumn("Độ trễ (ms)", nameof(ProxyOrderRow.LatencyMs), 90, 7, true));
        _grid.Columns.Add(CreateTextColumn("Ứng dụng", nameof(ProxyOrderRow.ApplicationCount), 172, 13, true));
        _grid.Columns.Add(CreateTextColumn("ID cũ", nameof(ProxyOrderRow.UserProxyId), 72, 6, true, false));
        _grid.Columns.Add(CreateTextColumn("Proxy Trước", nameof(ProxyOrderRow.PreviousIp), 120, 9, true, false));
        _grid.Columns.Add(CreateTextColumn("Proxy Domain", nameof(ProxyOrderRow.DisplayProxyDomain), 260, 20, true, false));
        _grid.Columns.Add(CreateTextColumn("Note", nameof(ProxyOrderRow.Note), 140, 10, true, false));
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ActionsColumnName,
            HeaderText = "Hành động",
            DataPropertyName = nameof(ProxyOrderRow.Actions),
            MinimumWidth = 190,
            FillWeight = 15,
            ReadOnly = true,
            Visible = false
        });

        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        if (_grid.Columns[nameof(ProxyOrderRow.ExpiredAt)] is { } expiredAtColumn)
        {
            expiredAtColumn.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            expiredAtColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        ConfigureInlineEditCue(nameof(ProxyOrderRow.Username));
        ConfigureInlineEditCue(nameof(ProxyOrderRow.Password));
        BuildRowContextMenu();
        BuildCopyMenu();
        ApplyColumnLayout();
        RefreshColumnVisibilityCombo();
    }

    private void ConfigureEmptyState()
    {
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.Visible = false;
        _emptyState.BackColor = LightTheme.Background;

        _emptyStateLayout.Dock = DockStyle.Fill;
        _emptyStateLayout.BackColor = LightTheme.Background;
        _emptyStateLayout.ColumnCount = 3;
        _emptyStateLayout.RowCount = 3;
        _emptyStateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _emptyStateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _emptyStateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _emptyStateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _emptyStateLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _emptyStateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var content = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = LightTheme.Background,
            Anchor = AnchorStyles.None
        };

        _emptyStateLabel.AutoSize = false;
        _emptyStateLabel.Width = 620;
        _emptyStateLabel.Height = 72;
        _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;
        _emptyStateLabel.Font = new Font(Font, FontStyle.Bold);
        _emptyStateLabel.ForeColor = LightTheme.Muted;
        _emptyStateLabel.LinkColor = LightTheme.Accent;
        _emptyStateLabel.ActiveLinkColor = LightTheme.Accent;
        _emptyStateLabel.VisitedLinkColor = LightTheme.Accent;
        _emptyStateLabel.Margin = new Padding(0, 0, 0, 12);
        _emptyStateLabel.LinkClicked += HandleEmptyStateLinkClicked;

        _emptyStateLoginButton.Text = "Đăng nhập tại đây";
        _emptyStateLoginButton.AutoSize = true;
        _emptyStateLoginButton.MinimumSize = new Size(Math.Max(_emptyStateLoginButton.MinimumSize.Width, 240), 48);
        _emptyStateLoginButton.Height = 48;
        _emptyStateLoginButton.Anchor = AnchorStyles.None;
        _emptyStateLoginButton.Margin = new Padding(220, 0, 220, 0);
        _emptyStateLoginButton.Variant = AppButtonVariant.Secondary;
        _emptyStateLoginButton.AccentColor = LightTheme.Accent;
        _emptyStateLoginButton.Click += async (_, _) => await HandleEmptyStateLoginClickAsync();

        content.Controls.Add(_emptyStateLabel);
        content.Controls.Add(_emptyStateLoginButton);
        _emptyStateLayout.Controls.Add(content, 1, 1);
        _emptyState.Controls.Add(_emptyStateLayout);
    }

    private void ApplyModeConfiguration()
    {
        _titleLabel.Text = _kind switch
        {
            ProxyOrderKind.Datacenter => "Đơn hàng Proxy Datacenter",
            ProxyOrderKind.RotateProxy => "Đơn hàng Proxy Xoay",
            ProxyOrderKind.RotateKey => "Đơn hàng Key Xoay",
            _ => "Đơn hàng Proxy Tĩnh"
        };

        if (_rotateButton is not null)
        {
            _rotateButton.Visible = IsRotateProxyMode;
            _fetchProxyButton.Visible = IsRotateKeyMode;
            _copyButton.Visible = true;
            _changeInfoButton.Visible = !IsRotateKeyMode;
        }

        if (_grid.Columns.Count == 0)
        {
            return;
        }

        SetColumnHeader(nameof(ProxyOrderRow.DisplayProxy), IsRotateKeyMode ? "Key" : "Proxy");
        SetColumnHeader(nameof(ProxyOrderRow.Protocol), "Loại");
        SetColumnHeader(nameof(ProxyOrderRow.ProtocolDisplay), "Loại");
        SetColumnVisible(nameof(ProxyOrderRow.UserProxyId), false);
        SetColumnVisible(nameof(ProxyOrderRow.Code), IsRotateMode);
        SetColumnVisible(nameof(ProxyOrderRow.Provider), !IsRotateMode);
        SetColumnVisible(nameof(ProxyOrderRow.Username), !IsRotateKeyMode);
        SetColumnVisible(nameof(ProxyOrderRow.Password), true);
        SetColumnVisible(nameof(ProxyOrderRow.Protocol), !IsRotateMode);
        SetColumnVisible(nameof(ProxyOrderRow.ProtocolDisplay), IsRotateMode);
        SetColumnVisible(nameof(ProxyOrderRow.Status), true);
        SetColumnVisible(nameof(ProxyOrderRow.LatencyMs), !IsRotateKeyMode);
        SetColumnVisible(nameof(ProxyOrderRow.ApplicationCount), true);
        SetColumnVisible(nameof(ProxyOrderRow.Note), false);
        SetColumnVisible(nameof(ProxyOrderRow.DisplayProxyDomain), !IsRotateMode);
        SetColumnVisible(nameof(ProxyOrderRow.PreviousIp), !IsRotateMode);
        SetColumnVisible(ActionsColumnName, false);

        SetColumnReadOnly(nameof(ProxyOrderRow.Username), IsRotateMode);
        SetColumnReadOnly(nameof(ProxyOrderRow.Password), false);
        SetColumnReadOnly(nameof(ProxyOrderRow.Protocol), IsRotateMode);
        SetInlineEditCueEnabled(nameof(ProxyOrderRow.Username), !IsRotateMode);
        SetInlineEditCueEnabled(nameof(ProxyOrderRow.Password), true);
        if (_pickerMode)
        {
            ApplyPickerModeConfiguration();
        }

        ConfigureRowHeightForMode();

        BuildRowContextMenu();
        BuildCopyMenu();
        ApplyColumnLayout();
        RefreshColumnVisibilityCombo();
        UpdateSelectionDependentActions();
    }

    private void ApplyPickerModeConfiguration()
    {
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.Visible = false;
            column.ReadOnly = true;
        }

        SetColumnHeader(nameof(ProxyOrderRow.DisplayProxy), "Proxy");
        SetColumnHeader(nameof(ProxyOrderRow.Protocol), "Giao thức");
        SetColumnHeader(nameof(ProxyOrderRow.ProtocolDisplay), "Giao thức");
        SetColumnHeader(nameof(ProxyOrderRow.ApplicationCount), "Ứng dụng");

        SetColumnVisible(SelectionColumnName, true);
        SetColumnVisible(nameof(ProxyOrderRow.DisplayProxy), true);
        SetColumnVisible(nameof(ProxyOrderRow.Protocol), !IsRotateMode);
        SetColumnVisible(nameof(ProxyOrderRow.ProtocolDisplay), IsRotateMode);
        SetColumnVisible(nameof(ProxyOrderRow.Status), true);
        SetColumnVisible(nameof(ProxyOrderRow.LatencyMs), true);
        SetColumnVisible(nameof(ProxyOrderRow.ApplicationCount), true);

        SetInlineEditCueEnabled(nameof(ProxyOrderRow.Username), false);
        SetInlineEditCueEnabled(nameof(ProxyOrderRow.Password), false);
    }

    private void ConfigureRowHeightForMode()
    {
        var height = _pickerMode ? 37 : IsRotateMode ? RotateProxyOrderRowHeight : ProxyOrderRowHeight;
        _grid.RowTemplate.Height = height;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            row.Height = height;
            row.MinimumHeight = height;
        }
    }

    private bool IsRotateMode => _kind is ProxyOrderKind.RotateProxy or ProxyOrderKind.RotateKey;

    private bool IsRotateProxyMode => _kind == ProxyOrderKind.RotateProxy;

    private bool IsRotateKeyMode => _kind == ProxyOrderKind.RotateKey;

    private void SetColumnVisible(string columnName, bool visible)
    {
        if (_grid.Columns[columnName] is { } column)
        {
            column.Visible = visible;
        }
    }

    private void SetColumnReadOnly(string columnName, bool readOnly)
    {
        if (_grid.Columns[columnName] is { } column)
        {
            column.ReadOnly = readOnly;
        }
    }

    private void SetColumnHeader(string columnName, string header)
    {
        if (_grid.Columns[columnName] is { } column)
        {
            column.HeaderText = header;
        }
    }

    private void ApplyColumnLayout()
    {
        var order = IsRotateMode
            ? new[]
            {
                SelectionColumnName,
                nameof(ProxyOrderRow.Code),
                nameof(ProxyOrderRow.UserProxyId),
                nameof(ProxyOrderRow.OrderCode),
                nameof(ProxyOrderRow.DisplayProxy),
                nameof(ProxyOrderRow.Username),
                nameof(ProxyOrderRow.Password),
                nameof(ProxyOrderRow.ProtocolDisplay),
                nameof(ProxyOrderRow.ExpiredAt),
                nameof(ProxyOrderRow.Status),
                nameof(ProxyOrderRow.LatencyMs),
                nameof(ProxyOrderRow.ApplicationCount),
                nameof(ProxyOrderRow.Provider),
                nameof(ProxyOrderRow.PreviousIp),
                nameof(ProxyOrderRow.DisplayProxyDomain),
                nameof(ProxyOrderRow.Note),
                ActionsColumnName
            }
            : new[]
            {
                SelectionColumnName,
                nameof(ProxyOrderRow.OrderCode),
                nameof(ProxyOrderRow.DisplayProxy),
                nameof(ProxyOrderRow.Username),
                nameof(ProxyOrderRow.Password),
                nameof(ProxyOrderRow.Provider),
                nameof(ProxyOrderRow.Protocol),
                nameof(ProxyOrderRow.ExpiredAt),
                nameof(ProxyOrderRow.Status),
                nameof(ProxyOrderRow.LatencyMs),
                nameof(ProxyOrderRow.ApplicationCount),
                nameof(ProxyOrderRow.Code),
                nameof(ProxyOrderRow.UserProxyId),
                nameof(ProxyOrderRow.PreviousIp),
                nameof(ProxyOrderRow.DisplayProxyDomain),
                nameof(ProxyOrderRow.Note),
                ActionsColumnName
            };

        for (var index = 0; index < order.Length; index++)
        {
            if (_grid.Columns[order[index]] is { } column)
            {
                column.DisplayIndex = index;
            }
        }
    }

    private void BuildRowContextMenu()
    {
        _rowContextMenu.Items.Clear();
        _rowContextMenu.Opening -= HandleRowContextMenuOpening;
        if (_pickerMode)
        {
            return;
        }

        if (IsRotateProxyMode)
        {
            _rowContextMenu.Items.Add("Lấy link xoay", UiIcons.NewGetRotateLinkHover, async (_, _) => await ShowSelectedRotateLinksAsync());
            _rowContextMenu.Items.Add("Xoay proxy", UiIcons.NewRotateProxyHover, async (_, _) => await RotateSelectedSingleAsync());
            _rowContextMenu.Items.Add("Gia hạn", UiIcons.NewRenewProxyHover, async (_, _) => await RenewSelectedAsync());
            _rowContextMenu.Items.Add("Cập nhật thông tin", UiIcons.NewInlineEditActionHover, async (_, _) => await ChangeSelectedRotateInfoAsync());
            _rowContextMenu.Items.Add(new ToolStripSeparator());
            _rowContextMenu.Items.Add("Check Proxy", UiIcons.NewCheckProxyHover, async (_, _) => await CheckSelectedAsync());
            _rowContextMenu.Opening += HandleRowContextMenuOpening;
            return;
        }

        if (IsRotateKeyMode)
        {
            _rowContextMenu.Items.Add("Lấy link xoay", UiIcons.NewGetRotateLinkHover, async (_, _) => await ShowSelectedRotateLinksAsync());
            _rowContextMenu.Items.Add("Xoay proxy", UiIcons.NewRotateProxyHover, async (_, _) => await RotateSelectedSingleAsync());
            _rowContextMenu.Items.Add("Gia hạn", UiIcons.NewRenewProxyHover, async (_, _) => await RenewSelectedAsync());
            _rowContextMenu.Items.Add("Xem proxy hiện tại", UiIcons.NewViewProxyHover, async (_, _) => await ViewSelectedCurrentProxyAsync());
            _rowContextMenu.Items.Add(new ToolStripSeparator());
            _rowContextMenu.Items.Add("Check Proxy", UiIcons.NewCheckProxyHover, async (_, _) => await CheckSelectedAsync());
            _rowContextMenu.Opening += HandleRowContextMenuOpening;
            return;
        }
        _rowContextMenu.Items.Add("Check proxy", UiIcons.NewCheckProxyHover, async (_, _) => await CheckSelectedAsync());
        _rowContextMenu.Items.Add("Đổi thông tin", UiIcons.NewChangeProxyInfoHover, async (_, _) => await ChangeSelectedInfoAsync());
        _rowContextMenu.Items.Add("Gia hạn", UiIcons.NewRenewProxyHover, async (_, _) => await RenewSelectedAsync());
        _rowContextMenu.Opening += HandleRowContextMenuOpening;
    }

    private void HandleRowContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e) =>
        e.Cancel = GetSelectedOrders().Any() is false;

    private void BuildCopyMenu()
    {
        _copyMenu.Items.Clear();
        if (IsRotateProxyMode)
        {
            _copyMenu.Items.Add("Copy Proxy", _copyIcon, async (_, _) => await CopySelectedAsync(CopyOrderValue.Proxy));
            _copyMenu.Items.Add("Copy Link xoay", UiIcons.Import, async (_, _) => await CopySelectedAsync(CopyOrderValue.Link));
            _copyMenu.Items.Add("Copy Proxy và link xoay", UiIcons.Swap, async (_, _) => await CopySelectedAsync(CopyOrderValue.ProxyLink));
            _copyMenu.Items.Add("Copy ID", UiIcons.Key, async (_, _) => await CopySelectedAsync(CopyOrderValue.Id));
            return;
        }

        if (IsRotateKeyMode)
        {
            _copyMenu.Items.Add("Copy Key", _copyIcon, async (_, _) => await CopySelectedAsync(CopyOrderValue.Key));
            _copyMenu.Items.Add("Copy Link xoay", UiIcons.Import, async (_, _) => await CopySelectedAsync(CopyOrderValue.Link));
            _copyMenu.Items.Add("Copy ID", UiIcons.Key, async (_, _) => await CopySelectedAsync(CopyOrderValue.Id));
            return;
        }

        _copyMenu.Items.Add("Copy Proxy", _copyIcon, async (_, _) => await CopySelectedAsync(CopyOrderValue.Proxy));
    }

    private void ShowCopyMenu()
    {
        if (_copyMenu.Items.Count == 0)
        {
            return;
        }

        _copyMenu.Show(_copyButton, new Point(0, _copyButton.Height));
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(
        string header,
        string propertyName,
        int minimumWidth,
        float fillWeight,
        bool readOnly = false,
        bool visible = true) =>
        new()
        {
            Name = propertyName,
            HeaderText = header,
            DataPropertyName = propertyName,
            MinimumWidth = minimumWidth,
            FillWeight = fillWeight,
            ReadOnly = readOnly,
            Visible = visible
        };

    private async Task ResetAndFetchAsync()
    {
        _page = ProxyOrderListConstants.DefaultPage;
        await FetchPageAsync();
    }

    private void SetSearchFilter(ProxyOrderSearchField field, string text)
    {
        _isSettingSearch = true;
        try
        {
            foreach (var item in _searchFieldCombo.Items.Cast<SearchFieldOption>())
            {
                if (item.Value == field)
                {
                    _searchFieldCombo.SelectedItem = item;
                    break;
                }
            }

            _searchTextBox.Text = text;
            UpdateSearchPlaceholder();
            _searchDebounceTimer.Stop();
        }
        finally
        {
            _isSettingSearch = false;
        }
    }

    private void ClearSearchFilter()
    {
        _isSettingSearch = true;
        try
        {
            if (_searchFieldCombo.Items.Count > 0)
            {
                _searchFieldCombo.SelectedIndex = 0;
            }

            _searchTextBox.Clear();
            UpdateSearchPlaceholder();
            _searchDebounceTimer.Stop();
        }
        finally
        {
            _isSettingSearch = false;
        }
    }

    private async Task ClearFiltersAsync()
    {
        ClearSearchFilter();
        _page = ProxyOrderListConstants.DefaultPage;
        await FetchPageAsync();
    }

    private async Task MovePageAsync(int delta)
    {
        var target = Math.Max(1, _page + delta);
        if (target == _page)
        {
            return;
        }

        _page = target;
        await FetchPageAsync();
    }

    private async Task FetchPageAsync(bool forceRefresh = false)
    {
        try
        {
            if (!await EnsureAuthenticatedForProxyOrdersAsync())
            {
                ShowLoginRequiredState();
                return;
            }

            _requiresLogin = false;
            UpdateFooterVisibility();
            _statusLabel.Text = "Đang tải đơn hàng proxy...";
            SetToolbarEnabled(false);
            var page = await _proxyOrderCacheService.GetPageAsync(BuildPageRequest(), forceRefresh, CancellationToken.None);
            _total = page.Total;
            _hasNextPage = page.HasNextPage;
            _orders = page.Orders
                .Where(order => IsRotateMode || _kind == ProxyOrderKind.Datacenter || !order.IsDatacenter)
                .ToList();
            var affectedProxyIds = await UpdateLocalBackendProxyInfoAsync(_orders);
            if (affectedProxyIds.Count > 0)
            {
                OnProfileAffectingChanged(ProfileAffectingChange.ForProxies("Cập nhật proxy backend", affectedProxyIds));
            }

            await ClearExpiredAssignmentsAsync(_orders);
            BindRows();
            _statusLabel.Text = $"{_rows.Count} đơn hàng trên trang hiện tại";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.ClearAsync(CancellationToken.None);
            ShowLoginRequiredState();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Không thể tải đơn hàng proxy.";
            UpdateEmptyState();
            UiFeedback.ShowWarning(this, ex, "Không thể tải đơn hàng proxy");
        }
        finally
        {
            SetToolbarEnabled(!_requiresLogin);
            UpdatePager();
        }
    }

    private async Task<bool> EnsureAuthenticatedForProxyOrdersAsync()
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
        _orders = [];
        _rows = [];
        _total = null;
        _hasNextPage = false;
        _grid.DataSource = _rows;
        _statusLabel.Text = "Vui lòng đăng nhập để xem đơn hàng proxy.";
        UpdateEmptyState();
        SetToolbarEnabled(false);
        UpdatePager();
    }

    private async Task HandleEmptyStateLoginClickAsync()
    {
        if (LoginRequestedAsync is null)
        {
            return;
        }

        _emptyStateLoginButton.Enabled = false;
        try
        {
            await LoginRequestedAsync();
        }
        finally
        {
            _emptyStateLoginButton.Enabled = true;
        }
    }

    private ProxyOrderPageRequest BuildPageRequest()
    {
        var searchText = _searchTextBox.Text.Trim();
        var searchField = string.IsNullOrWhiteSpace(searchText)
            ? null
            : (ProxyOrderSearchField?)((_searchFieldCombo.SelectedItem as SearchFieldOption)?.Value ?? ProxyOrderSearchField.Id);

        return new ProxyOrderPageRequest
        {
            Page = _page,
            Limit = _limit,
            CategoryTypeId = IsRotateMode ? 2 : 1,
            IsCdk = IsRotateProxyMode ? false : IsRotateKeyMode ? true : null,
            OrderKind = _kind,
            ProviderIn = _kind == ProxyOrderKind.Datacenter ? DatacenterProviders : null,
            SearchField = searchField,
            SearchText = string.IsNullOrWhiteSpace(searchText) ? null : searchText
        };
    }

    private void BindRows(IReadOnlySet<int>? selectedUserProxyIds = null)
    {
        _isBinding = true;
        var bindVersion = ++_bindVersion;
        _rows = _orders
            .Select((order, index) =>
            {
                var row = new ProxyOrderRow(
                    order,
                    ((_page - 1) * _limit) + index + 1,
                    ResolveUsage(order));
                if (IsRotateKeyMode && _rotateKeyTextCache.TryGetValue(order.UserProxyId, out var keyText))
                {
                    row.DisplayProxy = keyText;
                }

                row.IsSelected = selectedUserProxyIds?.Contains(order.UserProxyId) == true;
                return row;
            })
            .ToList();
        _grid.DataSource = _rows;
        ConfigureRowHeightForMode();
        UpdateEmptyState();
        _isBinding = false;
        SyncGridSelectionToCheckedRows();
        InvalidateSelectionHeader();
        UpdateSelectionDependentActions();
        if (IsRotateKeyMode)
        {
            _ = FetchVisibleRotateKeyTokensAsync(bindVersion);
        }
    }

    private void UpdateEmptyState()
    {
        _grid.Visible = true;
        _emptyState.Visible = false;
        SetEmptyStateContent();
        _grid.BringToFront();
        _grid.Invalidate();
    }

    private void SetEmptyStateContent()
    {
        var filtered = !string.IsNullOrWhiteSpace(_searchTextBox.Text);
        var noun = GetProxyKindDisplayName();
        _emptyStateLabel.Links.Clear();
        _emptyStateLoginButton.Visible = false;

        if (_requiresLogin)
        {
            _gridEmptyStateText = "Vui lòng đăng nhập để xem đơn hàng proxy.";
            return;
        }

        _gridEmptyStateText = filtered
            ? $"Không tìm thấy {noun} phù hợp với bộ lọc hiện tại."
            : GetGridEmptyStateText();
    }

    private GridEmptyStateContent? GetProxyOrderEmptyStateContent()
    {
        if (_rows.Count > 0 || string.IsNullOrWhiteSpace(_gridEmptyStateText))
        {
            return null;
        }

        return GridEmptyStateContent.TextOnly(_gridEmptyStateText);
    }

    private string GetProxyKindDisplayName() =>
        _kind switch
        {
            ProxyOrderKind.Datacenter => "Proxy Datacenter",
            ProxyOrderKind.RotateProxy => "Proxy Xoay",
            ProxyOrderKind.RotateKey => "Key Xoay",
            _ => "Proxy Tĩnh"
        };

    private string GetGridEmptyStateText() =>
        _kind switch
        {
            ProxyOrderKind.Datacenter => "Danh sách Proxy Datacenter trống",
            ProxyOrderKind.RotateProxy or ProxyOrderKind.RotateKey => "Danh sách Proxy Xoay trống",
            _ => "Danh sách Proxy Tĩnh trống"
        };

    private void PaintGridEmptyState(object? sender, PaintEventArgs e)
    {
        if (_requiresLogin || _rows.Count > 0 || string.IsNullOrWhiteSpace(_gridEmptyStateText))
        {
            return;
        }

        using var font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        var headerHeight = _grid.ColumnHeadersVisible ? _grid.ColumnHeadersHeight : 0;
        var bodyBounds = new Rectangle(0, headerHeight, _grid.ClientSize.Width, Math.Max(0, _grid.ClientSize.Height - headerHeight));
        if (bodyBounds.Height <= 0)
        {
            return;
        }

        var icon = _emptyArchiveIcon;
        var iconSize = icon is null ? Size.Empty : new Size(32, 32);
        var textSize = TextRenderer.MeasureText(e.Graphics, _gridEmptyStateText, font, bodyBounds.Size, TextFormatFlags.NoPadding);
        var gap = icon is null ? 0 : 12;
        var totalHeight = iconSize.Height + gap + textSize.Height;
        var top = bodyBounds.Top + Math.Max(0, (bodyBounds.Height - totalHeight) / 2);

        if (icon is not null)
        {
            var iconX = bodyBounds.Left + Math.Max(0, (bodyBounds.Width - iconSize.Width) / 2);
            e.Graphics.DrawImage(icon, iconX, top, iconSize.Width, iconSize.Height);
            top += iconSize.Height + gap;
        }

        var textBounds = new Rectangle(bodyBounds.Left + 16, top, Math.Max(0, bodyBounds.Width - 32), textSize.Height + 4);
        TextRenderer.DrawText(
            e.Graphics,
            _gridEmptyStateText,
            font,
            textBounds,
            LightTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void OnPurchaseRequested() =>
        PurchaseRequested?.Invoke(this, _kind);

    private string GetPurchaseUrl() =>
        _kind switch
        {
            ProxyOrderKind.Datacenter => BuildPortalUrl("proxy-datacenter"),
            ProxyOrderKind.RotateProxy => BuildPortalUrl("proxy-xoay"),
            ProxyOrderKind.RotateKey => BuildPortalUrl("key-xoay"),
            _ => BuildPortalUrl("proxy-tinh")
        };

    private string BuildPortalUrl(string path) =>
        _portalBaseUrl + path.TrimStart('/');

    private static string NormalizePortalBaseUrl(string value)
    {
        var baseUrl = string.IsNullOrWhiteSpace(value)
            ? "https://app.homeproxy.vn"
            : value.Trim();
        return baseUrl.TrimEnd('/') + "/";
    }

    private void OpenPurchasePage()
    {
        try
        {
            ExternalLinkLauncher.OpenInChromeOrDefault(GetPurchaseUrl());
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể mở liên kết");
        }
    }

    private void HandleEmptyStateLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (e.Link?.LinkData is not string url || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            ExternalLinkLauncher.OpenInChromeOrDefault(url);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể mở liên kết");
        }
    }

    private async Task FetchVisibleRotateKeyTokensAsync(int bindVersion)
    {
        var rowsToFetch = _rows
            .Where(row => !_rotateKeyTextCache.ContainsKey(row.UserProxyId))
            .ToList();
        if (rowsToFetch.Count == 0)
        {
            return;
        }

        var failureCount = 0;
        foreach (var row in rowsToFetch)
        {
            try
            {
                var token = await _proxyOrderApiClient.GenerateProxyTokenAsync(row.UserProxyId, isCdk: true, CancellationToken.None);
                _rotateKeyTokenCache[row.UserProxyId] = token ?? string.Empty;
                _rotateKeyTextCache[row.UserProxyId] = string.IsNullOrWhiteSpace(token) ? EmptyKeyText : token;
                UpdateRotateKeyCell(bindVersion, row.UserProxyId, _rotateKeyTextCache[row.UserProxyId]);
            }
            catch (Exception ex)
            {
                failureCount++;
                _rotateKeyTokenCache[row.UserProxyId] = string.Empty;
                _rotateKeyTextCache[row.UserProxyId] = KeyLoadErrorText;
                UpdateRotateKeyCell(bindVersion, row.UserProxyId, KeyLoadErrorText);
                if (bindVersion == _bindVersion)
                {
                    _statusLabel.Text = $"Không thể lấy key cho {failureCount} dòng: {ex.Message}";
                }
            }
        }
    }

    private void UpdateRotateKeyCell(int bindVersion, int userProxyId, string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateRotateKeyCell(bindVersion, userProxyId, text)));
            return;
        }

        if (bindVersion != _bindVersion)
        {
            return;
        }

        var row = _rows.FirstOrDefault(item => item.UserProxyId == userProxyId);
        if (row is null)
        {
            return;
        }

        row.DisplayProxy = text;
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is ProxyOrderRow gridData && gridData.UserProxyId == userProxyId)
            {
                gridRow.Cells[nameof(ProxyOrderRow.DisplayProxy)].Value = text;
                break;
            }
        }
    }

    private async Task LoadLocalUsageAsync()
    {
        var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
        var applications = await _applicationRuleRepository.GetAllAsync(CancellationToken.None);
        var usageByProxyId = applications
            .Where(rule => rule.AssignedProxyId is not null)
            .GroupBy(rule => rule.AssignedProxyId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(CreateLocalProxyApplicationUsage)
                    .Where(application => !string.IsNullOrWhiteSpace(application.DisplayName))
                    .OrderBy(application => application.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList());

        _localProxyUsage = proxies
            .Where(proxy => proxy.HasValidEndpoint)
            .GroupBy(proxy => BuildUsageKey(proxy.Host, proxy.Port, proxy.Username ?? string.Empty))
            .ToDictionary(
                group => group.Key,
                group => CreateLocalProxyUsage(group.First(), usageByProxyId));
        _localProxyUsageByBackendId = proxies
            .Where(proxy => proxy.BackendUserProxyId is not null)
            .GroupBy(proxy => proxy.BackendUserProxyId!.Value)
            .ToDictionary(
                group => group.Key,
                group => CreateLocalProxyUsage(group.First(), usageByProxyId));
    }

    private LocalProxyUsage CreateLocalProxyUsage(
        ProxyServer proxy,
        IReadOnlyDictionary<Guid, List<LocalProxyApplicationUsage>> usageByProxyId)
    {
        var applications = usageByProxyId.GetValueOrDefault(proxy.Id) ?? [];
        return new LocalProxyUsage(
            proxy.Id,
            proxy.Status == ProxyStatus.Unknown ? UncheckedStatusText : proxy.Status.ToString(),
            FormatLatency(proxy.LatencyMs),
            applications.Count,
            applications);
    }

    private LocalProxyApplicationUsage CreateLocalProxyApplicationUsage(ApplicationRule rule)
    {
        var displayName = !string.IsNullOrWhiteSpace(rule.RuntimeProcessName)
            ? rule.RuntimeProcessName.Trim()
            : rule.GetApplicationName();
        return new LocalProxyApplicationUsage(displayName, LoadApplicationIcon(rule));
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

    private async Task ClearExpiredAssignmentsAsync(IReadOnlyCollection<ProxyOrder> refreshedOrders)
    {
        var localProxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
        var applications = (await _applicationRuleRepository.GetAllAsync(CancellationToken.None)).ToList();
        var result = _expiredBackendProxyAssignmentService.FindExpiredAssignments(
            refreshedOrders,
            localProxies,
            applications,
            DateTimeOffset.Now);
        if (!result.HasChanges)
        {
            return;
        }

        var affectedRuleIds = result.AffectedApplicationRuleIds.ToHashSet();
        foreach (var rule in applications.Where(rule => affectedRuleIds.Contains(rule.Id)))
        {
            rule.AssignedProxyId = null;
            rule.IsEnabled = false;
            rule.Warning = ApplicationRuleEligibility.MissingAssignedProxyMessage;
        }

        await _applicationRuleRepository.SaveAllAsync(applications, CancellationToken.None);
        _statusLabel.Text = $"Đã bỏ gán proxy hết hạn của {result.AffectedApplicationCount} ứng dụng.";
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Proxy đã gắn hết hạn", result.AffectedApplicationRuleIds));
    }

    private async Task<IReadOnlySet<Guid>> UpdateLocalBackendProxyInfoAsync(IEnumerable<ProxyOrder> orders)
    {
        var orderById = orders.ToDictionary(order => order.UserProxyId);
        if (orderById.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var proxies = (await _proxyRepository.GetAllAsync(CancellationToken.None)).ToList();
        var affectedProxyIds = new HashSet<Guid>();
        foreach (var proxy in proxies.Where(proxy => proxy.BackendUserProxyId is not null && orderById.ContainsKey(proxy.BackendUserProxyId.Value)))
        {
            var order = orderById[proxy.BackendUserProxyId!.Value];
            var endpoint = string.IsNullOrWhiteSpace(order.Ip) ? proxy.Proxy : $"{order.Ip}:{order.Port}";
            if (!string.Equals(proxy.Proxy, endpoint, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(proxy.Username ?? string.Empty, order.Username, StringComparison.Ordinal) ||
                !string.Equals(proxy.Password ?? string.Empty, order.Password, StringComparison.Ordinal) ||
                proxy.Protocol != order.Protocol ||
                proxy.BackendOrderKind != _kind ||
                !string.Equals(proxy.BackendProvider ?? string.Empty, order.Provider, StringComparison.OrdinalIgnoreCase) ||
                proxy.ExpiredAt != order.ExpiredAt)
            {
                proxy.Proxy = endpoint;
                proxy.Username = string.IsNullOrWhiteSpace(order.Username) ? null : order.Username;
                proxy.Password = string.IsNullOrWhiteSpace(order.Password) ? null : order.Password;
                proxy.Protocol = order.Protocol;
                proxy.BackendOrderKind = _kind;
                proxy.BackendProvider = order.Provider;
                proxy.ExpiredAt = order.ExpiredAt;
                affectedProxyIds.Add(proxy.Id);
            }
        }

        if (affectedProxyIds.Count > 0)
        {
            await _proxyRepository.SaveAllAsync(proxies, CancellationToken.None);
            await LoadLocalUsageAsync();
        }

        return affectedProxyIds;
    }

    private LocalProxyUsage? ResolveUsage(ProxyOrder order)
    {
        var usageKey = BuildUsageKey(order.Ip, order.Port, order.Username);
        var localUsage = _localProxyUsageByBackendId.GetValueOrDefault(order.UserProxyId) ??
            _localProxyUsage.GetValueOrDefault(usageKey);
        var cachedCheck = _proxyOrderCacheService.GetCheckState(order);
        if (cachedCheck is not null)
        {
            return new LocalProxyUsage(
                localUsage?.LocalProxyId,
                FormatStatus(cachedCheck.Status),
                FormatLatency(cachedCheck.LatencyMs),
                localUsage?.ApplicationCount ?? 0,
                localUsage?.Applications ?? []);
        }

        return _checkedOrderUsage.GetValueOrDefault(order.UserProxyId) ?? localUsage;
    }

    private void OnProfileAffectingChanged(ProfileAffectingChange change) =>
        ProfileAffectingChanged?.Invoke(this, change);

    private async Task HandleCellEndEditAsync(DataGridViewCellEventArgs e)
    {
        if (_isBinding || e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var propertyName = _grid.Columns[e.ColumnIndex].DataPropertyName;
        if (IsRotateMode)
        {
            await HandleRotateCellEndEditAsync(e, propertyName);
            return;
        }

        if (propertyName is not (nameof(ProxyOrderRow.Username) or nameof(ProxyOrderRow.Password) or nameof(ProxyOrderRow.Protocol)) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ProxyOrderRow row ||
            !row.HasInfoChanged)
        {
            return;
        }

        var username = row.Username.Trim();
        var password = row.Password.Trim();
        if (!TryValidateProxyCredentials(username, password))
        {
            BindRows();
            return;
        }

        if (!await ChangeInfoAsync([row.UserProxyId], username, password, row.Protocol))
        {
            return;
        }

        var order = _orders.FirstOrDefault(item => item.UserProxyId == row.UserProxyId);
        if (order is not null)
        {
            order.Username = username;
            order.Password = password;
            order.Protocol = row.Protocol;
            _proxyOrderCacheService.UpdateOrderInfo(order.UserProxyId, order.Username, order.Password, order.Protocol);
            var affectedProxyIds = await UpdateLocalBackendProxyInfoAsync([order]);
            if (affectedProxyIds.Count > 0)
            {
                OnProfileAffectingChanged(ProfileAffectingChange.ForProxies("Cập nhật thông tin proxy backend", affectedProxyIds));
            }
        }

        BindRows();
    }

    private async Task HandleRotateCellEndEditAsync(DataGridViewCellEventArgs e, string propertyName)
    {
        if (propertyName != nameof(ProxyOrderRow.Password) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ProxyOrderRow row ||
            !row.HasInfoChanged)
        {
            return;
        }

        var password = row.Password.Trim();
        if (!TryValidateProxyPassword(password))
        {
            BindRows();
            return;
        }

        var order = _orders.FirstOrDefault(item => item.UserProxyId == row.UserProxyId);
        if (order is null)
        {
            BindRows();
            return;
        }

        if (!await ChangeRotatePasswordAsync(order, password))
        {
            return;
        }

        order.Password = password;
        _proxyOrderCacheService.UpdateOrderInfo(order.UserProxyId, order.Username, order.Password, order.Protocol);
        var affectedProxyIds = await UpdateLocalBackendProxyInfoAsync([order]);
        if (affectedProxyIds.Count > 0)
        {
            OnProfileAffectingChanged(ProfileAffectingChange.ForProxies(
                IsRotateProxyMode ? "Cập nhật password proxy xoay" : "Cập nhật password key xoay",
                affectedProxyIds));
        }

        BindRows();
    }

    private async Task<bool> ChangeRotatePasswordAsync(ProxyOrder order, string password)
    {
        try
        {
            _statusLabel.Text = "Đang cập nhật password proxy xoay...";
            SetToolbarEnabled(false);
            await _proxyOrderApiClient.ChangeRotateProxyInfoAsync(
                [order.UserProxyId],
                password,
                order.RotateInterval,
                order.RotateInterval > 0,
                CancellationToken.None);
            _statusLabel.Text = "Đã cập nhật password proxy xoay.";
            return true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể cập nhật password proxy xoay");
            await FetchPageAsync(forceRefresh: true);
            return false;
        }
        finally
        {
            SetToolbarEnabled(true);
        }
    }

    private bool TryValidateProxyCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show(this, "Username là bắt buộc.", "Dữ liệu không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return TryValidateProxyPassword(password);
    }

    private bool TryValidateProxyPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(this, "Password là bắt buộc.", "Dữ liệu không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (password.Length < MinimumProxyPasswordLength)
        {
            MessageBox.Show(this, "Password phải có ít nhất 9 ký tự.", "Dữ liệu không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private async Task CheckSelectedAsync()
    {
        var selected = GetSelectedOrders().ToList();
        if (selected.Count == 0)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất 1 proxy.");
            return;
        }

        await CheckOrdersAsync(selected, rebindRows: true, showCompletionMessage: true);
    }

    private async Task CheckSingleOrderAsync(ProxyOrder order)
    {
        await CheckOrdersAsync([order], rebindRows: false, showCompletionMessage: false);
    }

    private async Task CheckOrdersAsync(
        IReadOnlyList<ProxyOrder> selected,
        bool rebindRows,
        bool showCompletionMessage)
    {
        var selectedIds = selected.Select(order => order.UserProxyId).ToHashSet();

        try
        {
            _statusLabel.Text = $"Đang check {selected.Count} proxy...";
            SetToolbarEnabled(false);
            SetSelectedRowsCheckingState(selectedIds);
            var results = await Task.WhenAll(selected.Select(CheckOrderAsync));

            foreach (var result in results)
            {
                var order = _orders.FirstOrDefault(item => item.UserProxyId == result.UserProxyId);
                if (order is not null)
                {
                    _proxyOrderCacheService.SetCheckState(order, ParseProxyStatus(result.Status), ParseLatency(result.LatencyMs));
                }

                var existingOrderUsage = _orders
                    .FirstOrDefault(order => order.UserProxyId == result.UserProxyId) is { } checkedOrder
                    ? ResolveUsage(checkedOrder)
                    : null;
                _checkedOrderUsage[result.UserProxyId] = new LocalProxyUsage(
                    existingOrderUsage?.LocalProxyId,
                    result.Status,
                    result.LatencyMs,
                    existingOrderUsage?.ApplicationCount ?? 0,
                    existingOrderUsage?.Applications ?? []);
                if (!string.IsNullOrWhiteSpace(result.UsageKey))
                {
                    var existing = _localProxyUsage.GetValueOrDefault(result.UsageKey);
                    _localProxyUsage[result.UsageKey] = new LocalProxyUsage(
                        existing?.LocalProxyId,
                        result.Status,
                        result.LatencyMs,
                        existing?.ApplicationCount ?? 0,
                        existing?.Applications ?? []);
                }
            }

            if (rebindRows)
            {
                BindRows(selectedIds);
            }
            else
            {
                UpdateCheckedRowsInGrid(results);
            }

            _statusLabel.Text = $"Đã check {selected.Count} proxy.";
            if (showCompletionMessage)
            {
                UiFeedback.ShowInfo(this, $"Đã check {selected.Count} proxy.");
            }
        }
        finally
        {
            SetToolbarEnabled(true);
        }
    }

    private async Task<CheckedProxyUsage> CheckOrderAsync(ProxyOrder order)
    {
        try
        {
            var proxy = await CreateProxyServerForCheckAsync(order);
            if (proxy is null)
            {
                return new CheckedProxyUsage(order.UserProxyId, string.Empty, ProxyStatus.Dead.ToString(), string.Empty);
            }

            var result = await _proxyChecker.CheckAsync(proxy, CancellationToken.None);
            return new CheckedProxyUsage(
                order.UserProxyId,
                BuildUsageKey(order.Ip, order.Port, order.Username),
                result.IsReachable ? ProxyStatus.Live.ToString() : ProxyStatus.Dead.ToString(),
                result.IsReachable ? FormatLatency(result.LatencyMs) : string.Empty);
        }
        catch
        {
            return new CheckedProxyUsage(
                order.UserProxyId,
                BuildUsageKey(order.Ip, order.Port, order.Username),
                ProxyStatus.Dead.ToString(),
                string.Empty);
        }
    }

    private async Task<ProxyServer?> CreateProxyServerForCheckAsync(ProxyOrder order)
    {
        if (IsRotateKeyMode)
        {
            var currentProxy = await _proxyOrderApiClient.RotateProxyAsync(order.UserProxyId, checkOnly: true, CancellationToken.None);
            return TryCreateProxyServer(currentProxy.Proxy, order.Protocol, out var proxy)
                ? proxy
                : null;
        }

        return TryCreateProxyServer(order.ProxyAddress, order.Protocol, out var orderProxy)
            ? orderProxy
            : null;
    }

    private static bool TryCreateProxyServer(string value, ProxyProtocol protocol, out ProxyServer proxy)
    {
        proxy = new ProxyServer { Protocol = protocol, IsFromBackend = true };
        var segments = value.Split(':', StringSplitOptions.TrimEntries);
        if (segments.Length < 2 ||
            !int.TryParse(segments[1], CultureInfo.InvariantCulture, out var port))
        {
            return false;
        }

        proxy.Proxy = $"{segments[0]}:{port}";
        if (segments.Length >= 4)
        {
            proxy.Username = segments[2];
            proxy.Password = segments[3];
        }

        return proxy.HasValidEndpoint;
    }

    private void SetSelectedRowsCheckingState(IReadOnlySet<int> selectedIds)
    {
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is not ProxyOrderRow row ||
                !selectedIds.Contains(row.UserProxyId))
            {
                continue;
            }

            row.Status = CheckingStatusText;
            row.LatencyMs = string.Empty;
            gridRow.Cells[nameof(ProxyOrderRow.Status)].Value = row.Status;
            gridRow.Cells[nameof(ProxyOrderRow.LatencyMs)].Value = row.LatencyMs;
            _grid.InvalidateCell(gridRow.Cells[nameof(ProxyOrderRow.Status)]);
            _grid.InvalidateCell(gridRow.Cells[nameof(ProxyOrderRow.LatencyMs)]);
        }
    }

    private void UpdateCheckedRowsInGrid(IEnumerable<CheckedProxyUsage> results)
    {
        var resultsById = results.ToDictionary(result => result.UserProxyId);
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is not ProxyOrderRow row ||
                !resultsById.TryGetValue(row.UserProxyId, out var result))
            {
                continue;
            }

            row.Status = result.Status;
            row.LatencyMs = result.LatencyMs;
            gridRow.Cells[nameof(ProxyOrderRow.Status)].Value = row.Status;
            gridRow.Cells[nameof(ProxyOrderRow.LatencyMs)].Value = row.LatencyMs;
            _grid.InvalidateCell(gridRow.Cells[nameof(ProxyOrderRow.Status)]);
            _grid.InvalidateCell(gridRow.Cells[nameof(ProxyOrderRow.LatencyMs)]);
        }
    }

    private async Task ChangeSelectedInfoAsync()
    {
        if (IsRotateProxyMode)
        {
            await ChangeSelectedRotateInfoAsync();
            return;
        }

        if (IsRotateKeyMode)
        {
            UiFeedback.ShowInfo(this, "Key Xoay không hỗ trợ cập nhật thông tin proxy từ toolbar này.");
            return;
        }

        var selected = GetSelectedOrders().ToList();
        if (selected.Count == 0)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất 1 proxy.");
            return;
        }

        using var form = _serviceProvider.GetRequiredService<ChangeProxyInfoForm>();
        form.LoadOrders(selected);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (await ChangeInfoAsync(selected.Select(order => order.UserProxyId).ToArray(), form.Username, form.Password, form.Protocol))
        {
            foreach (var order in selected)
            {
                order.Username = form.Username;
                order.Password = form.Password;
                order.Protocol = form.Protocol;
                _proxyOrderCacheService.UpdateOrderInfo(order.UserProxyId, order.Username, order.Password, order.Protocol);
            }

            var affectedProxyIds = await UpdateLocalBackendProxyInfoAsync(selected);
            _proxyOrderCacheService.InvalidateCategory(1, null);
            await FetchPageAsync(forceRefresh: true);
            if (affectedProxyIds.Count > 0)
            {
                OnProfileAffectingChanged(ProfileAffectingChange.ForProxies("Cập nhật thông tin proxy backend", affectedProxyIds));
            }
            UiFeedback.ShowInfo(this, $"Đã cập nhật {selected.Count} proxy.");
        }
    }

    private async Task RenewSelectedAsync()
    {
        var selected = GetSelectedOrders().ToList();
        if (selected.Count == 0)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất 1 proxy.");
            return;
        }

        using var form = _serviceProvider.GetRequiredService<RenewProxyOrdersForm>();
        await form.LoadOrdersAsync(selected, IsRotateProxyMode, CancellationToken.None);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SetToolbarEnabled(false);
            await _proxyOrderApiClient.RenewProxiesAsync(
                selected.Select(order => order.UserProxyId).ToArray(),
                form.DayOfRenewal,
                selected[0].CategoryTypeId,
                CancellationToken.None,
                IsRotateProxyMode ? form.RotateInterval : null,
                IsRotateProxyMode ? form.IsAutoRotate : null);
            _proxyOrderCacheService.InvalidateCategory(selected[0].CategoryTypeId, IsRotateProxyMode ? false : IsRotateKeyMode ? true : null);
            await FetchPageAsync(forceRefresh: true);
            RenewCompleted?.Invoke(this, EventArgs.Empty);
            UiFeedback.ShowInfo(this, $"Đã gia hạn {selected.Count} proxy.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể gia hạn proxy");
        }
        finally
        {
            SetToolbarEnabled(true);
        }
    }

    private async Task<bool> ChangeInfoAsync(
        IReadOnlyCollection<int> userProxyIds,
        string username,
        string password,
        ProxyProtocol protocol)
    {
        try
        {
            _statusLabel.Text = "Đang cập nhật thông tin proxy...";
            SetToolbarEnabled(false);
            await _proxyOrderApiClient.ChangeProxyInfoAsync(userProxyIds, username, password, protocol, CancellationToken.None);
            _statusLabel.Text = "Đã cập nhật thông tin proxy.";
            return true;
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể cập nhật thông tin proxy");
            await FetchPageAsync(forceRefresh: true);
            return false;
        }
        finally
        {
            SetToolbarEnabled(true);
        }
    }

    private async Task ChangeSelectedRotateInfoAsync()
    {
        var selected = GetSelectedOrders().ToList();
        if (selected.Count == 0)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất 1 proxy.");
            return;
        }

        using var form = _serviceProvider.GetRequiredService<RotateProxyInfoForm>();
        form.LoadOrders(selected);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SetToolbarEnabled(false);
            await _proxyOrderApiClient.ChangeRotateProxyInfoAsync(
                selected.Select(order => order.UserProxyId).ToArray(),
                form.Password,
                form.RotateInterval,
                form.IsAutoRotate,
                CancellationToken.None);
            foreach (var order in selected)
            {
                order.Password = form.Password;
            }

            var affectedProxyIds = await UpdateLocalBackendProxyInfoAsync(selected);
            _proxyOrderCacheService.InvalidateCategory(2, false);
            await FetchPageAsync(forceRefresh: true);
            if (affectedProxyIds.Count > 0)
            {
                OnProfileAffectingChanged(ProfileAffectingChange.ForProxies("Cập nhật proxy xoay", affectedProxyIds));
            }

            UiFeedback.ShowInfo(this, $"Đã cập nhật {selected.Count} proxy xoay.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể cập nhật proxy xoay");
        }
        finally
        {
            SetToolbarEnabled(true);
        }
    }

    private async Task RotateSelectedAsync()
    {
        if (!IsRotateProxyMode)
        {
            return;
        }

        var selected = GetSelectedOrders().ToList();
        if (selected.Count == 0)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất 1 proxy.");
            return;
        }

        if (!TryBeginManualRotate())
        {
            return;
        }

        var rotateSucceeded = false;
        try
        {
            SetToolbarEnabled(false);
            await _proxyOrderApiClient.RotateProxiesByIdsAsync(
                selected.Select(order => order.UserProxyId).ToArray(),
                CancellationToken.None);
            rotateSucceeded = true;
            _proxyOrderCacheService.InvalidateCategory(2, false);
            await FetchPageAsync(forceRefresh: true);
            ShowActionResultModal(
                "Kết quả xoay proxy",
                "Thông tin xoay proxy",
                $"Đã gửi lệnh xoay {selected.Count} proxy.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể xoay proxy");
        }
        finally
        {
            _manualProxyRotateGuard.Finish(rotateSucceeded);
            SetToolbarEnabled(true);
        }
    }

    private async Task RotateSelectedSingleAsync()
    {
        var order = GetSelectedOrders().FirstOrDefault();
        if (order is null)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất 1 proxy.");
            return;
        }

        if (!TryBeginManualRotate())
        {
            return;
        }

        var rotateSucceeded = false;
        try
        {
            SetToolbarEnabled(false);
            var result = await _proxyOrderApiClient.RotateProxyAsync(order.UserProxyId, checkOnly: false, CancellationToken.None);
            rotateSucceeded = true;

            UiFeedback.ShowInfo(this, $"Đã gửi lệnh xoay proxy");

            // if (!string.IsNullOrWhiteSpace(result.Proxy))
            // {
            //     ShowActionResultModal("Proxy đã xoay", "Proxy đã xoay", result.Proxy);
            // }
            // else
            // {
            //     ShowActionResultModal(
            //         "Kết quả xoay proxy",
            //         "Thông tin xoay proxy",
            //         string.IsNullOrWhiteSpace(result.Message) ? "Đã gửi lệnh xoay proxy." : result.Message);
            // }

            _proxyOrderCacheService.InvalidateCategory(2, false);
            await FetchPageAsync(forceRefresh: true);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể xoay proxy");
        }
        finally
        {
            _manualProxyRotateGuard.Finish(rotateSucceeded);
            SetToolbarEnabled(true);
        }
    }

    private async Task ViewSelectedCurrentProxyAsync()
    {
        var order = GetSelectedOrders().FirstOrDefault();
        if (order is null)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn 1 key.");
            return;
        }

        try
        {
            SetToolbarEnabled(false);
            var result = await _proxyOrderApiClient.RotateProxyAsync(order.UserProxyId, checkOnly: true, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(result.Proxy))
            {
                ShowActionResultModal(
                    "Proxy hiện tại",
                    "Thông tin proxy hiện tại",
                    string.IsNullOrWhiteSpace(result.Message) ? "Không lấy được proxy hiện tại." : result.Message);
                return;
            }

            ShowActionResultModal("Proxy hiện tại", "Proxy hiện tại", result.Proxy);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể xem proxy hiện tại");
        }
        finally
        {
            SetToolbarEnabled(true);
        }
    }

    private async Task CopySelectedAsync(CopyOrderValue value)
    {
        var selected = GetSelectedOrders().ToList();
        if (selected.Count == 0)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất 1 proxy.");
            return;
        }

        try
        {
            SetToolbarEnabled(false);
            var lines = new List<string>();
            var selectedDisplayProxies = _rows
                .Where(row => row.IsSelected)
                .ToDictionary(row => row.UserProxyId, row => row.DisplayProxy);
            foreach (var order in selected)
            {
                switch (value)
                {
                    case CopyOrderValue.Id:
                        if (!string.IsNullOrWhiteSpace(order.Code))
                        {
                            lines.Add(order.Code);
                        }
                        break;
                    case CopyOrderValue.Proxy:
                        if (selectedDisplayProxies.TryGetValue(order.UserProxyId, out var displayProxy) &&
                            !string.IsNullOrWhiteSpace(displayProxy))
                        {
                            lines.Add(displayProxy);
                        }
                        break;
                    case CopyOrderValue.Key:
                        {
                            var token = await GetRotateTokenAsync(order);
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                lines.Add(token);
                            }
                            break;
                        }
                    case CopyOrderValue.Link:
                        {
                            var token = await GetRotateTokenAsync(order);
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                lines.Add(_proxyOrderApiClient.BuildPublicRotateUrl(token, checkOnly: false));
                            }
                            break;
                        }
                    case CopyOrderValue.ProxyLink:
                        if (CanExposeRotateProxy(order))
                        {
                            var token = await GetRotateTokenAsync(order);
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                lines.Add($"{order.ProxyAddress}|{_proxyOrderApiClient.BuildPublicRotateUrl(token, checkOnly: false)}");
                            }
                        }
                        break;
                }
            }

            var text = string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
            if (string.IsNullOrWhiteSpace(text))
            {
                UiFeedback.ShowInfo(this, "Không có dữ liệu để copy.");
                return;
            }

            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể copy dữ liệu");
        }
        finally
        {
            SetToolbarEnabled(true);
        }
    }

    private async Task<string?> GetRotateTokenAsync(ProxyOrder order)
    {
        if (order.IsCdk && _rotateKeyTokenCache.TryGetValue(order.UserProxyId, out var cached))
        {
            return cached;
        }

        var token = await _proxyOrderApiClient.GenerateProxyTokenAsync(order.UserProxyId, order.IsCdk, CancellationToken.None);
        if (order.IsCdk)
        {
            _rotateKeyTokenCache[order.UserProxyId] = token ?? string.Empty;
        }

        return token;
    }

    private bool TryGetCurrentProxyCellText(out string text)
    {
        text = string.Empty;
        if (_grid.CurrentCell is not { } cell ||
            cell.RowIndex < 0 ||
            cell.ColumnIndex < 0 ||
            _grid.Columns[cell.ColumnIndex].DataPropertyName != nameof(ProxyOrderRow.DisplayProxy) ||
            _grid.Rows[cell.RowIndex].DataBoundItem is not ProxyOrderRow row ||
            string.IsNullOrWhiteSpace(row.DisplayProxy))
        {
            return false;
        }

        text = row.DisplayProxy;
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

    private static bool CanExposeRotateProxy(ProxyOrder order) =>
        !string.IsNullOrWhiteSpace(order.ProxyAddress);

    private async Task OpenFetchProxyFromKeyFormAsync()
    {
        using var form = _serviceProvider.GetRequiredService<FetchProxyFromKeyForm>();
        await Task.CompletedTask;
        form.ShowDialog(this);
    }

    private IEnumerable<ProxyOrder> GetSelectedOrders()
    {
        var ids = _rows.Where(row => row.IsSelected).Select(row => row.UserProxyId).ToHashSet();
        return _orders.Where(order => ids.Contains(order.UserProxyId));
    }

    private void HandleCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 &&
            e.ColumnIndex >= 0 &&
            _grid.Columns[e.ColumnIndex].DataPropertyName == nameof(ProxyOrderRow.DisplayProxy) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyOrderRow proxyRow)
        {
            ShowProxyCellTextSelector(e.RowIndex, e.ColumnIndex, proxyRow.DisplayProxy);
            return;
        }

        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].ReadOnly)
        {
            return;
        }

        _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        _grid.BeginEdit(selectAll: true);
    }

    private async void HandleGridCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 &&
            e.ColumnIndex >= 0 &&
            _grid.Columns[e.ColumnIndex].DataPropertyName == nameof(ProxyOrderRow.DisplayProxy) &&
            TryGetProxyCopyButtonBounds(e.RowIndex, e.ColumnIndex, out var proxyCopyButtonBounds) &&
            proxyCopyButtonBounds.Contains(_grid.PointToClient(Cursor.Position)) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyOrderRow proxyRow)
        {
            CopyTextToClipboard(proxyRow.DisplayProxy);
            return;
        }

        if (e.RowIndex >= 0 &&
            e.ColumnIndex >= 0 &&
            _grid.Columns[e.ColumnIndex].Name == ActionsColumnName)
        {
            SelectSingleRow(e.RowIndex);
            if (TryGetActionAtCursor(e.RowIndex, e.ColumnIndex, out var action))
            {
                await ExecuteRowActionAsync(action);
            }
            else
            {
                ShowRowContextMenu(e.RowIndex, e.ColumnIndex);
            }

            return;
        }

        if (e.RowIndex >= 0 &&
            e.ColumnIndex >= 0 &&
            _grid.Columns[e.ColumnIndex].DataPropertyName == nameof(ProxyOrderRow.Status) &&
            TryGetStatusCheckButtonBounds(e.RowIndex, e.ColumnIndex, out var statusButtonBounds) &&
            statusButtonBounds.Contains(_grid.PointToClient(Cursor.Position)) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyOrderRow statusRow)
        {
            SyncGridSelectionToCheckedRows();
            var order = _orders.FirstOrDefault(item => item.UserProxyId == statusRow.UserProxyId);
            if (order is not null)
            {
                await CheckSingleOrderAsync(order);
                SyncGridSelectionToCheckedRows();
            }

            return;
        }

        if (e.RowIndex >= 0 &&
            e.ColumnIndex >= 0 &&
            _grid.Columns[e.ColumnIndex].DataPropertyName == nameof(ProxyOrderRow.ApplicationCount) &&
            TryGetUsageViewButtonBounds(e.RowIndex, e.ColumnIndex, out var usageButtonBounds) &&
            usageButtonBounds.Contains(_grid.PointToClient(Cursor.Position)) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyOrderRow usageRow &&
            usageRow.LocalProxyId is { } proxyId)
        {
            ViewApplicationsRequested?.Invoke(this, proxyId);
            return;
        }

        if (e.RowIndex >= 0 &&
            e.ColumnIndex >= 0 &&
            TryGetInlineEditIconBounds(e.RowIndex, e.ColumnIndex, out var inlineEditBounds) &&
            inlineEditBounds.Contains(_grid.PointToClient(Cursor.Position)))
        {
            BeginInlineEdit(e.RowIndex, e.ColumnIndex);
            return;
        }

        if (_pickerMode && e.RowIndex >= 0)
        {
            SelectSingleRow(e.RowIndex);
            return;
        }

        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex] is not DataGridViewComboBoxColumn)
        {
            return;
        }

        QueueComboOpen(e.RowIndex, e.ColumnIndex);
    }

    private void HandleGridCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        _selectionDragStartPoint = null;
        _isDraggingRowSelection = false;

        if (e.RowIndex < 0)
        {
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            if (_pickerMode)
            {
                return;
            }

            ShowRowContextMenu(e.RowIndex, Math.Max(e.ColumnIndex, 0));
            return;
        }

        if (e.ColumnIndex < 0)
        {
            return;
        }

        var column = _grid.Columns[e.ColumnIndex];
        if (column.DataPropertyName == nameof(ProxyOrderRow.IsSelected) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyOrderRow row)
        {
            _pendingCheckboxToggle = (e.RowIndex, !row.IsSelected);
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            return;
        }

        if (column is DataGridViewComboBoxColumn)
        {
            if (_pickerMode)
            {
                return;
            }

            QueueComboOpen(e.RowIndex, e.ColumnIndex);
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            _selectionDragStartPoint = _grid.PointToClient(Cursor.Position);
        }
    }

    private void HandleGridCellMouseUp(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (_pendingCheckboxToggle is not { } pendingToggle)
        {
            if (e.Button == MouseButtons.Left &&
                e.RowIndex >= 0 &&
                e.ColumnIndex >= 0 &&
                (ModifierKeys & Keys.Shift) == Keys.Shift &&
                _grid.Columns[e.ColumnIndex] is not DataGridViewComboBoxColumn)
            {
                BeginInvoke(new Action(CheckGridSelectedRows));
            }

            return;
        }

        _pendingCheckboxToggle = null;
        BeginInvoke(new Action(() => SetRowCheckedState(pendingToggle.RowIndex, pendingToggle.NewValue, updateGridSelection: true)));
    }

    private void HandleGridMouseMove(object? sender, MouseEventArgs e)
    {
        UpdateGridActionToolTip(e.Location);

        if ((e.Button & MouseButtons.Left) == 0 || _selectionDragStartPoint is not { } startPoint)
        {
            return;
        }

        if (!_isDraggingRowSelection)
        {
            var dragBounds = new Rectangle(
                startPoint.X - (SystemInformation.DragSize.Width / 2),
                startPoint.Y - (SystemInformation.DragSize.Height / 2),
                SystemInformation.DragSize.Width,
                SystemInformation.DragSize.Height);

            _isDraggingRowSelection = !dragBounds.Contains(e.Location);
        }

        if (_isDraggingRowSelection)
        {
            CheckGridSelectedRows();
        }
    }

    private void HandleGridMouseUp(object? sender, MouseEventArgs e)
    {
        UpdateGridActionToolTip(e.Location);

        if (_isDraggingRowSelection)
        {
            CheckGridSelectedRows();
        }

        _selectionDragStartPoint = null;
        _isDraggingRowSelection = false;
    }

    private void HandleGridMouseLeave(object? sender, EventArgs e)
    {
        _currentGridToolTipText = string.Empty;
        _currentGridToolTipAction = null;
        _currentGridToolTipBounds = Rectangle.Empty;
        _grid.Cursor = Cursors.Default;
        _toolTip.Hide(_grid);
        _toolTip.SetToolTip(_grid, string.Empty);
    }

    private void UpdateGridActionToolTip(Point location)
    {
        var hovered = TryGetActionButtonAtPoint(location, out var button) ? button : null;
        var text = hovered?.ToolTip ?? string.Empty;
        _grid.Cursor = IsClickableGridPoint(location) ? Cursors.Hand : Cursors.Default;

        if (!string.Equals(_currentGridToolTipText, text, StringComparison.Ordinal) ||
            _currentGridToolTipAction != hovered?.Action ||
            _currentGridToolTipBounds != (hovered?.Bounds ?? Rectangle.Empty))
        {
            _currentGridToolTipText = text;
            _currentGridToolTipAction = hovered?.Action;
            _currentGridToolTipBounds = hovered?.Bounds ?? Rectangle.Empty;
            _toolTip.SetToolTip(_grid, text);
            if (string.IsNullOrWhiteSpace(text))
            {
                _toolTip.Hide(_grid);
            }
            else
            {
                _toolTip.Show(text, _grid, location.X + 14, location.Y + 18, 4000);
            }
        }
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
            return !_pickerMode && column.DataPropertyName == nameof(ProxyOrderRow.IsSelected);
        }

        if (hit.RowIndex < 0)
        {
            return false;
        }

        if (column.DataPropertyName == nameof(ProxyOrderRow.IsSelected))
        {
            return !_pickerMode;
        }

        return TryGetProxyCopyButtonBounds(hit.RowIndex, hit.ColumnIndex, out var proxyCopyBounds) && proxyCopyBounds.Contains(point) ||
            column.DataPropertyName == nameof(ProxyOrderRow.Status) &&
                TryGetStatusCheckButtonBounds(hit.RowIndex, hit.ColumnIndex, out var statusButtonBounds) && statusButtonBounds.Contains(point) ||
            TryGetUsageViewButtonBounds(hit.RowIndex, hit.ColumnIndex, out var usageButtonBounds) && usageButtonBounds.Contains(point) ||
            TryGetInlineEditIconBounds(hit.RowIndex, hit.ColumnIndex, out var inlineEditBounds) && inlineEditBounds.Contains(point) ||
            TryGetActionButtonAtPoint(point, out _);
    }

    private void HandleGridKeyDown(object? sender, KeyEventArgs e)
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

        if (e.Control && e.KeyCode == Keys.A)
        {
            if (_pickerMode)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            SetAllRowsCheckedState(selected: true);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ShowRowContextMenu(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not ProxyOrderRow row)
        {
            return;
        }

        if (!row.IsSelected)
        {
            SetAllRowsCheckedState(selected: false);
            row.IsSelected = true;
        }

        _grid.CurrentCell = _grid.Rows[rowIndex].Cells[columnIndex];
        SyncGridSelectionToCheckedRows();
        _grid.InvalidateRow(rowIndex);
        InvalidateSelectionHeader();
        UpdateSelectionDependentActions();

        _rowContextMenu.Show(_grid, _grid.PointToClient(Cursor.Position));
    }

    private void SelectSingleRow(int rowIndex)
    {
        SetAllRowsCheckedState(selected: false);
        SetRowCheckedState(rowIndex, selected: true, updateGridSelection: true);
    }

    private void FocusProxy(ProxyServer proxy)
    {
        var targetRowIndex = -1;
        for (var index = 0; index < _grid.Rows.Count; index++)
        {
            if (_grid.Rows[index].DataBoundItem is ProxyOrderRow row &&
                (row.LocalProxyId == proxy.Id ||
                    (proxy.BackendUserProxyId is not null && row.UserProxyId == proxy.BackendUserProxyId.Value)))
            {
                targetRowIndex = index;
                break;
            }
        }

        if (targetRowIndex < 0)
        {
            return;
        }

        SetAllRowsCheckedState(selected: false);
        _grid.ClearSelection();
        var firstVisibleCell = _grid.Rows[targetRowIndex].Cells
            .Cast<DataGridViewCell>()
            .FirstOrDefault(cell => cell.Visible);
        if (firstVisibleCell is not null)
        {
            _grid.CurrentCell = firstVisibleCell;
        }

        _grid.Rows[targetRowIndex].Selected = true;
        _grid.FirstDisplayedScrollingRowIndex = targetRowIndex;
        _grid.InvalidateRow(targetRowIndex);
    }

    private async Task ExecuteRowActionAsync(RowAction action)
    {
        if (_isBusy)
        {
            return;
        }

        switch (action)
        {
            case RowAction.CopyLink:
                await ShowSelectedRotateLinksAsync();
                break;
            case RowAction.Rotate:
                await RotateSelectedSingleAsync();
                break;
            case RowAction.Renew:
                await RenewSelectedAsync();
                break;
            case RowAction.ChangeInfo:
                await ChangeSelectedRotateInfoAsync();
                break;
            case RowAction.ViewCurrentProxy:
                await ViewSelectedCurrentProxyAsync();
                break;
        }
    }

    private async Task ShowSelectedRotateLinksAsync()
    {
        var selected = GetSelectedOrders().ToList();
        if (selected.Count == 0)
        {
            UiFeedback.ShowInfo(this, "Hãy chọn ít nhất 1 proxy.");
            return;
        }

        try
        {
            SetToolbarEnabled(false);
            var links = new List<string>();
            foreach (var order in selected)
            {
                var token = await GetRotateTokenAsync(order);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    links.Add(_proxyOrderApiClient.BuildPublicRotateUrl(token, checkOnly: false));
                }
            }

            var text = string.Join(Environment.NewLine, links);
            if (string.IsNullOrWhiteSpace(text))
            {
                UiFeedback.ShowInfo(this, "Không có link xoay để copy.");
                return;
            }

            Clipboard.SetText(text);
            UiFeedback.ShowInfo(this, selected.Count == 1
                ? "Đã copy link xoay vào clipboard."
                : $"Đã copy {links.Count} link xoay vào clipboard.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể lấy link xoay");
        }
        finally
        {
            SetToolbarEnabled(true);
        }
    }

    private void ShowActionResultModal(string title, string label, string value)
    {
        using var form = new ProxyOrderActionResultForm(title, label, value);
        form.ShowDialog(this);
    }

    private bool TryBeginManualRotate()
    {
        if (!_manualProxyRotateGuard.TryBegin(out var remaining))
        {
            var remainingSeconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
            MessageBox.Show(
                this,
                $"Bạn cần chờ {remainingSeconds} giây nữa để tiếp tục xoay proxy.",
                "Chưa thể xoay proxy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private bool TryGetActionAtCursor(int rowIndex, int columnIndex, out RowAction action)
    {
        action = default;
        var cellBounds = _grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true);
        var cursorPoint = _grid.PointToClient(Cursor.Position);
        foreach (var button in GetActionButtons(cellBounds))
        {
            if (button.Bounds.Contains(cursorPoint))
            {
                action = button.Action;
                return true;
            }
        }

        return false;
    }

    private bool TryGetActionButtonAtPoint(Point point, out ActionButtonLayout button)
    {
        button = default!;
        if (!IsRotateMode)
        {
            return false;
        }

        var hit = _grid.HitTest(point.X, point.Y);
        if (hit.RowIndex < 0 ||
            hit.ColumnIndex < 0 ||
            _grid.Columns[hit.ColumnIndex].Name != ActionsColumnName)
        {
            return false;
        }

        var cellBounds = _grid.GetCellDisplayRectangle(hit.ColumnIndex, hit.RowIndex, cutOverflow: true);
        foreach (var candidate in GetActionButtons(cellBounds))
        {
            if (candidate.Bounds.Contains(point))
            {
                button = candidate;
                return true;
            }
        }

        return false;
    }

    private List<ActionButtonLayout> GetActionButtons(Rectangle cellBounds)
    {
        var actions = IsRotateKeyMode
            ? new[]
            {
                new ActionButtonSpec(RowAction.CopyLink, UiIcons.NewGetRotateLink, "Lấy link xoay"),
                new ActionButtonSpec(RowAction.Rotate, UiIcons.NewRotateProxy, "Xoay proxy"),
                new ActionButtonSpec(RowAction.Renew, UiIcons.NewRenewProxy, "Gia hạn"),
                new ActionButtonSpec(RowAction.ViewCurrentProxy, UiIcons.NewViewProxy, "Xem proxy hiện tại")
            }
            : new[]
            {
                new ActionButtonSpec(RowAction.CopyLink, UiIcons.NewGetRotateLink, "Lấy link xoay"),
                new ActionButtonSpec(RowAction.Rotate, UiIcons.NewRotateProxy, "Xoay proxy"),
                new ActionButtonSpec(RowAction.Renew, UiIcons.NewRenewProxy, "Gia hạn"),
                new ActionButtonSpec(RowAction.ChangeInfo, UiIcons.NewInlineEditAction, "Cập nhật thông tin")
            };

        const int buttonSize = 36;
        const int horizontalGap = 12;
        var totalWidth = (actions.Length * buttonSize) + ((actions.Length - 1) * horizontalGap);
        var startX = cellBounds.Left + Math.Max(6, (cellBounds.Width - totalWidth) / 2);
        var startY = cellBounds.Top + Math.Max(4, (cellBounds.Height - buttonSize) / 2);
        var result = new List<ActionButtonLayout>(actions.Length);
        for (var index = 0; index < actions.Length; index++)
        {
            result.Add(new ActionButtonLayout(
                actions[index].Action,
                actions[index].Icon,
                actions[index].ToolTip,
                new Rectangle(
                    startX + (index * (buttonSize + horizontalGap)),
                    startY,
                    buttonSize,
                    buttonSize)));
        }

        return result;
    }

    private bool TryGetStatusCheckButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not ProxyOrderRow row ||
            !ShouldShowStatusCheckButton(row))
        {
            return false;
        }

        var cellBounds = _grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true);
        bounds = GetStatusCheckButtonBounds(cellBounds, isChecking: false);
        return true;
    }

    private bool TryGetProxyCopyButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            columnIndex < 0 ||
            _grid.Columns[columnIndex].DataPropertyName != nameof(ProxyOrderRow.DisplayProxy))
        {
            return false;
        }

        bounds = GetProxyCopyButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private static Rectangle GetProxyCopyButtonBounds(Rectangle cellBounds)
    {
        return GridCellButtonRenderer.GetRightAlignedIconButtonBounds(cellBounds, rightPadding: 8);
    }

    private static Rectangle GetStatusCheckButtonBounds(Rectangle cellBounds, bool isChecking)
    {
        var height = Math.Min(32, Math.Max(24, cellBounds.Height - 10));
        var width = isChecking ? 58 : 64;
        return new Rectangle(
            cellBounds.Right - width - 8,
            cellBounds.Top + Math.Max(4, (cellBounds.Height - height) / 2),
            width,
            height);
    }

    private static bool ShouldShowStatusCheckButton(ProxyOrderRow row) =>
        !IsCheckingStatus(row);

    private static bool ShouldPaintStatusCheckCell(ProxyOrderRow row) =>
        ShouldShowStatusCheckButton(row) || IsCheckingStatus(row);

    private static bool IsCheckingStatus(ProxyOrderRow row) =>
        string.Equals(row.Status, CheckingStatusText, StringComparison.OrdinalIgnoreCase);

    private void CheckGridSelectedRows()
    {
        if (_pickerMode)
        {
            if (_grid.CurrentRow?.Index >= 0)
            {
                SelectSingleRow(_grid.CurrentRow.Index);
            }

            return;
        }

        var changed = false;
        foreach (DataGridViewRow selectedRow in _grid.SelectedRows)
        {
            if (selectedRow.DataBoundItem is ProxyOrderRow row && !row.IsSelected)
            {
                row.IsSelected = true;
                changed = true;
            }
        }

        if (changed)
        {
            _grid.Refresh();
            InvalidateSelectionHeader();
            UpdateSelectionDependentActions();
        }
    }

    private void SyncGridSelectionToCheckedRows()
    {
        _grid.ClearSelection();
        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            gridRow.Selected = gridRow.DataBoundItem is ProxyOrderRow row && row.IsSelected;
        }
    }

    private void QueueComboOpen(int rowIndex, int columnIndex)
    {
        if (_grid.IsCurrentCellInEditMode && !_grid.EndEdit())
        {
            return;
        }

        _pendingComboOpenCell = (rowIndex, columnIndex);
        _grid.CurrentCell = _grid.Rows[rowIndex].Cells[columnIndex];
        BeginInvoke(new Action(() => BeginComboEdit(rowIndex, columnIndex)));
    }

    private void BeginComboEdit(int rowIndex, int columnIndex)
    {
        if (_grid.CurrentCell?.RowIndex != rowIndex ||
            _grid.CurrentCell.ColumnIndex != columnIndex ||
            _grid.CurrentCell.OwningColumn is not DataGridViewComboBoxColumn)
        {
            return;
        }

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

    private void HandleColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (_pickerMode ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ProxyOrderRow.IsSelected) ||
            _rows.Count == 0)
        {
            return;
        }

        var newValue = _rows.Any(row => !row.IsSelected);
        SetAllRowsCheckedState(newValue);
    }

    private void SetAllRowsCheckedState(bool selected)
    {
        _grid.ClearSelection();
        for (var index = 0; index < _grid.Rows.Count; index++)
        {
            if (_grid.Rows[index].DataBoundItem is ProxyOrderRow row)
            {
                row.IsSelected = selected;
            }

            _grid.Rows[index].Selected = selected;
        }

        _grid.Refresh();
        InvalidateSelectionHeader();
        UpdateSelectionDependentActions();
    }

    private void SetRowCheckedState(int rowIndex, bool selected, bool updateGridSelection)
    {
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not ProxyOrderRow row)
        {
            return;
        }

        row.IsSelected = selected;
        if (_pickerMode && selected)
        {
            foreach (DataGridViewRow gridRow in _grid.Rows)
            {
                if (gridRow.Index == rowIndex)
                {
                    continue;
                }

                if (gridRow.DataBoundItem is ProxyOrderRow existingRow)
                {
                    existingRow.IsSelected = false;
                }

                gridRow.Selected = false;
            }
        }

        if (updateGridSelection)
        {
            _grid.Rows[rowIndex].Selected = selected;
        }

        _grid.InvalidateRow(rowIndex);
        InvalidateSelectionHeader();
        UpdateSelectionDependentActions();
    }

    private void PaintSelectionHeaderCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ProxyOrderRow.IsSelected) ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: true);

        var state = GetSelectionHeaderState();
        var checkBoxSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
        var checkBoxLocation = new Point(
            e.CellBounds.Left + (e.CellBounds.Width - checkBoxSize.Width) / 2,
            e.CellBounds.Top + (e.CellBounds.Height - checkBoxSize.Height) / 2);
        CheckBoxRenderer.DrawCheckBox(e.Graphics, checkBoxLocation, state);
        e.Handled = true;
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

    private void PaintActionCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (!IsRotateMode ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].Name != ActionsColumnName ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        foreach (var button in GetActionButtons(e.CellBounds))
        {
            DrawActionIconButton(e.Graphics, button.Bounds, button.Icon);
        }

        e.Handled = true;
    }

    private static void DrawActionIconButton(Graphics graphics, Rectangle bounds, Image icon)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = AppButton.CreateRoundedRectangle(bounds, 6))
        using (var background = new SolidBrush(Color.FromArgb(247, 247, 247)))
        {
            graphics.FillPath(background, path);
        }

        var iconSize = Math.Min(20, Math.Min(bounds.Width - 12, bounds.Height - 12));
        if (iconSize <= 0)
        {
            return;
        }

        var iconBounds = new Rectangle(
            bounds.Left + (bounds.Width - iconSize) / 2,
            bounds.Top + (bounds.Height - iconSize) / 2,
            iconSize,
            iconSize);
        graphics.DrawImage(icon, iconBounds);
    }

    private void PaintProxyCopyCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ProxyOrderRow.DisplayProxy) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ProxyOrderRow row ||
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
            row.DisplayProxy,
            e.CellStyle?.Font ?? _grid.Font,
            textBounds,
            e.CellStyle?.ForeColor ?? _grid.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        GridCellButtonRenderer.DrawImageButton(e.Graphics, buttonBounds, _copyIcon);
        e.Handled = true;
    }

    private void PaintStatusCheckCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ProxyOrderRow.Status) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ProxyOrderRow row ||
            !ShouldPaintStatusCheckCell(row) ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var isChecking = IsCheckingStatus(row);
        var buttonBounds = GetStatusCheckButtonBounds(e.CellBounds, isChecking);
        var textFont = e.CellStyle?.Font ?? _grid.Font;
        var textColor = e.CellStyle?.ForeColor ?? _grid.ForeColor;
        var textBounds = new Rectangle(
            e.CellBounds.Left + 8,
            e.CellBounds.Top,
            Math.Max(0, buttonBounds.Left - e.CellBounds.Left - 12),
            e.CellBounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            row.Status,
            textFont,
            textBounds,
            textColor,
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
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(ProxyOrderRow.ApplicationCount) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ProxyOrderRow row ||
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
            Math.Max(0, buttonBounds.Left - e.CellBounds.Left - 16),
            30);
        DrawUsageChip(e.Graphics, chipBounds, row);
        GridCellButtonRenderer.DrawImageButton(e.Graphics, buttonBounds, UiIcons.NewViewProxy);
        e.Handled = true;
    }

    private static bool ShouldShowUsageViewButton(ProxyOrderRow row) =>
        row.LocalProxyId is not null &&
        int.TryParse(row.ApplicationCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) &&
        count > 0;

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
            _grid.Columns[columnIndex].DataPropertyName != nameof(ProxyOrderRow.ApplicationCount) ||
            _grid.Rows[rowIndex].DataBoundItem is not ProxyOrderRow row ||
            !ShouldShowUsageViewButton(row))
        {
            return false;
        }

        bounds = GetUsageViewButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private static void DrawUsageChip(Graphics graphics, Rectangle bounds, ProxyOrderRow row)
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
        var extraText = extraCount > 0 ? $"+{extraCount.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
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


    private void HandleCellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var column = _grid.Columns[e.ColumnIndex];
        if (column.Name == ActionsColumnName)
        {
            e.ToolTipText = TryGetActionButtonAtPoint(_grid.PointToClient(Cursor.Position), out var hovered)
                ? hovered.ToolTip
                : string.Empty;
            return;
        }

        if (column.DataPropertyName == nameof(ProxyOrderRow.Status) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyOrderRow row &&
            ShouldShowStatusCheckButton(row))
        {
            e.ToolTipText = "Check proxy cho dòng này.";
            return;
        }

        if (column.DataPropertyName == nameof(ProxyOrderRow.ApplicationCount) &&
            _grid.Rows[e.RowIndex].DataBoundItem is ProxyOrderRow usageRow &&
            usageRow.Applications.Count > 0)
        {
            e.ToolTipText = string.Join(Environment.NewLine, usageRow.Applications.Select(application => application.DisplayName));
        }
    }

    private void ConfigureInlineEditCue(string columnName)
    {
        SetInlineEditCueEnabled(columnName, enabled: true);
    }

    private void SetInlineEditCueEnabled(string columnName, bool enabled)
    {
        if (_grid.Columns[columnName] is { } column)
        {
            column.DefaultCellStyle.Padding = enabled ? new Padding(4, 0, 28, 0) : new Padding(4, 0, 4, 0);
        }
    }

    private static bool IsInlineEditableColumn(DataGridViewColumn column) =>
        !column.ReadOnly &&
        column.DataPropertyName is nameof(ProxyOrderRow.Username) or nameof(ProxyOrderRow.Password);

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
        var selectionColumn = _grid.Columns
            .Cast<DataGridViewColumn>()
            .FirstOrDefault(column => column.DataPropertyName == nameof(ProxyOrderRow.IsSelected));
        if (selectionColumn is not null)
        {
            _grid.InvalidateCell(selectionColumn.Index, -1);
        }
    }

    private async Task ToggleSelectedColumnVisibilityAsync()
    {
        if (_columnVisibilityCombo.SelectedItem is not ColumnVisibilityOption option)
        {
            return;
        }

        var column = _grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(item => item.Name == option.ColumnName);
        if (column is null)
        {
            return;
        }

        column.Visible = !column.Visible;
        await SaveColumnVisibilityAsync();
        RefreshColumnVisibilityCombo();
    }

    private async Task ApplySavedColumnVisibilityAsync()
    {
        var settings = await _appSettingsRepository.GetAsync(CancellationToken.None);
        if (!settings.DataGridColumnVisibility.TryGetValue(GetColumnVisibilitySettingsKey(), out var savedColumns))
        {
            RefreshColumnVisibilityCombo();
            return;
        }

        foreach (var column in GetUserConfigurableColumns())
        {
            if (savedColumns.TryGetValue(column.Name, out var visible))
            {
                column.Visible = visible;
            }
        }

        ApplyModeLockedColumnVisibility();
        RefreshColumnVisibilityCombo();
    }

    private async Task SaveColumnVisibilityAsync()
    {
        var settings = await _appSettingsRepository.GetAsync(CancellationToken.None);
        settings.DataGridColumnVisibility[GetColumnVisibilitySettingsKey()] = GetUserConfigurableColumns()
            .ToDictionary(column => column.Name, column => column.Visible, StringComparer.Ordinal);
        await _appSettingsRepository.SaveAsync(settings, CancellationToken.None);
    }

    private string GetColumnVisibilitySettingsKey() =>
        $"ProxyOrders.{_kind}";

    private IEnumerable<DataGridViewColumn> GetUserConfigurableColumns() =>
        _grid.Columns
            .Cast<DataGridViewColumn>()
            .Where(column =>
                column.Name != ActionsColumnName &&
                column.DataPropertyName != nameof(ProxyOrderRow.IsSelected) &&
                !IsModeLockedHiddenColumn(column));

    private bool IsModeLockedHiddenColumn(DataGridViewColumn column) =>
        column.Name == ActionsColumnName ||
        (IsRotateMode &&
            column.DataPropertyName is nameof(ProxyOrderRow.Protocol) or nameof(ProxyOrderRow.Provider));

    private void ApplyModeLockedColumnVisibility()
    {
        foreach (var column in _grid.Columns.Cast<DataGridViewColumn>().Where(IsModeLockedHiddenColumn))
        {
            column.Visible = false;
        }
    }

    private void RefreshColumnVisibilityCombo()
    {
        var options = GetUserConfigurableColumns()
            .OrderBy(column => column.DisplayIndex)
            .Select(column => new ColumnVisibilityOption(column.Name, column.HeaderText, column.Visible))
            .ToList();

        _columnVisibilityCombo.DataSource = null;
        _columnVisibilityCombo.DataSource = options;
        _columnVisibilityCombo.SelectedIndex = -1;
        _columnVisibilityCombo.Text = "Ẩn / hiện cột";
    }

    private void UpdatePager()
    {
        var totalPages = _total is > 0 ? Math.Max(1, (int)Math.Ceiling(_total.Value / (double)_limit)) : (int?)null;
        _pageLabel.Text = totalPages is null ? $"1/1" : $"{_page}/{totalPages}";
        var canPage = !_requiresLogin && !_isBusy;
        _previousPageButton.Enabled = canPage && _page > 1;
        _nextPageButton.Enabled = canPage && (totalPages is null ? _hasNextPage : _page < totalPages.Value);
        UpdateFooterVisibility();
    }

    private void UpdateFooterVisibility() =>
        _bottomBar.Visible = !_requiresLogin;

    private void SetToolbarEnabled(bool enabled)
    {
        _isBusy = !enabled;
        foreach (Control control in Controls.Cast<Control>().SelectMany(Flatten))
        {
            if (control is Button or ComboBox or TextBox)
            {
                if (ReferenceEquals(control, _purchaseButton))
                {
                    control.Enabled = enabled || _requiresLogin;
                    continue;
                }

                control.Enabled = enabled;
            }
        }

        if (_requiresLogin)
        {
            _emptyStateLoginButton.Enabled = enabled || LoginRequestedAsync is not null;
        }

        UpdateSelectionDependentActions();
    }

    private void UpdateSelectionDependentActions()
    {
        if (_checkButton is null)
        {
            return;
        }

        var hasSelection = !_isBusy && _rows.Any(row => row.IsSelected);
        _checkButton.Enabled = hasSelection;
        _renewButton.Enabled = hasSelection;
        _copyButton.Enabled = hasSelection;
        _rotateButton.Enabled = hasSelection && IsRotateProxyMode;
        _changeInfoButton.Enabled = hasSelection && !IsRotateKeyMode;
        if (_fetchProxyButton is not null)
        {
            _fetchProxyButton.Enabled = !_isBusy && IsRotateKeyMode;
        }

        if (_purchaseButton is not null)
        {
            _purchaseButton.Enabled = _requiresLogin || !_isBusy;
        }

        SelectionAvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

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

    private static string BuildUsageKey(string host, int port, string username) =>
        $"{host.Trim().ToLowerInvariant()}:{port}:{username.Trim()}";

    private static string FormatStatus(ProxyStatus status) =>
        status == ProxyStatus.Unknown ? UncheckedStatusText : status.ToString();

    private static ProxyStatus ParseProxyStatus(string value) =>
        Enum.TryParse<ProxyStatus>(value, ignoreCase: true, out var status) ? status : ProxyStatus.Unknown;

    private static string FormatLatency(int? latencyMs) =>
        latencyMs is null ? string.Empty : $"{latencyMs.Value.ToString(CultureInfo.InvariantCulture)} ms";

    private static int? ParseLatency(string value)
    {
        var normalized = value.Replace("ms", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var latency) ? latency : null;
    }

    private sealed record SearchFieldOption(string DisplayName, ProxyOrderSearchField Value);

    private sealed record ColumnVisibilityOption(string ColumnName, string HeaderText, bool IsVisible)
    {
        public override string ToString() => $"{(IsVisible ? "✓" : " ")} {HeaderText}";
    }

    private enum CopyOrderValue
    {
        Proxy,
        Key,
        Link,
        ProxyLink,
        Id
    }

    private enum RowAction
    {
        CopyLink,
        Rotate,
        Renew,
        ChangeInfo,
        ViewCurrentProxy
    }

    private sealed record ActionButtonSpec(RowAction Action, Image Icon, string ToolTip);

    private sealed record ActionButtonLayout(RowAction Action, Image Icon, string ToolTip, Rectangle Bounds);

    private sealed record LocalProxyApplicationUsage(string DisplayName, Image Icon);

    private sealed record LocalProxyUsage(
        Guid? LocalProxyId,
        string Status,
        string LatencyMs,
        int ApplicationCount,
        IReadOnlyList<LocalProxyApplicationUsage> Applications);

    private sealed record CheckedProxyUsage(int UserProxyId, string UsageKey, string Status, string LatencyMs);

    private sealed class ProxyOrderRow
    {
        private readonly string _originalUsername;
        private readonly string _originalPassword;
        private readonly ProxyProtocol _originalProtocol;
        private string _username = string.Empty;
        private string _password = string.Empty;

        public ProxyOrderRow(ProxyOrder order, int displayOrder, LocalProxyUsage? usage)
        {
            UserProxyId = order.UserProxyId;
            Order = displayOrder;
            Code = order.Code;
            OrderCode = order.OrderCode;
            Provider = order.Provider;
            DisplayProxy = BuildDisplayProxy(order);
            PreviousIp = order.PreviousIp ?? string.Empty;
            DisplayProxyDomain = order.ProxyDomain;
            Username = order.Username;
            Password = order.Password;
            Protocol = order.Protocol;
            ProtocolDisplay = order.CategoryTypeId == 2 ? "HTTP/SOCKS5" : order.Protocol.ToDisplayName();
            Note = order.Description ?? string.Empty;
            ExpiredAt = order.ExpiredAt?.ToLocalTime().ToString("HH:mm\ndd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
            Status = usage?.Status ?? UncheckedStatusText;
            LatencyMs = usage?.LatencyMs ?? string.Empty;
            ApplicationCount = usage?.ApplicationCount.ToString(CultureInfo.InvariantCulture) ?? "0";
            Applications = usage?.Applications ?? [];
            LocalProxyId = usage?.LocalProxyId;
            _originalUsername = order.Username ?? string.Empty;
            _originalPassword = order.Password ?? string.Empty;
            _originalProtocol = order.Protocol;
        }

        private static string BuildDisplayProxy(ProxyOrder order)
        {
            if (order.IsCdk)
            {
                return LoadingKeyText;
            }

            if (order.CategoryTypeId == 2)
            {
                return string.IsNullOrWhiteSpace(order.ProxyAddress) ? "Chưa có proxy" : order.ProxyAddress;
            }

            return order.ProxyAddress;
        }

        public bool IsSelected { get; set; }
        public int UserProxyId { get; }
        public int Order { get; }
        public string Code { get; }
        public string OrderCode { get; }
        public string Provider { get; }
        public string DisplayProxy { get; set; }
        public string PreviousIp { get; }
        public string DisplayProxyDomain { get; }
        public string Username
        {
            get => _username;
            set => _username = value ?? string.Empty;
        }

        public string Password
        {
            get => _password;
            set => _password = value ?? string.Empty;
        }
        public ProxyProtocol Protocol { get; set; }
        public string ProtocolDisplay { get; }
        public string Note { get; }
        public string ExpiredAt { get; }
        public string Status { get; set; }
        public string LatencyMs { get; set; }
        public string ApplicationCount { get; }
        public IReadOnlyList<LocalProxyApplicationUsage> Applications { get; }
        public Guid? LocalProxyId { get; }
        public string Actions => string.Empty;

        public bool HasInfoChanged =>
            !string.Equals(Username, _originalUsername, StringComparison.Ordinal) ||
            !string.Equals(Password, _originalPassword, StringComparison.Ordinal) ||
            Protocol != _originalProtocol;
    }
}

public sealed record ProxyOrderPickerSelection(ProxyOrder Order, ProxyOrderKind Kind, string ProxyAddress);
