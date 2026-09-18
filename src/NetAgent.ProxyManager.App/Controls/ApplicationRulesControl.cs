using Microsoft.Extensions.DependencyInjection;
using NetAgent.ProxyManager.App.Configuration;
using NetAgent.ProxyManager.App.Forms;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;
using CheckBoxState = System.Windows.Forms.VisualStyles.CheckBoxState;

namespace NetAgent.ProxyManager.App.Controls;

public sealed class ApplicationRulesControl : UserControl
{
    private const string AssignProxyButtonText = "Gắn Proxy";
    private const string EnabledHeaderText = "Dùng Proxy";
    private const string ActionsColumnName = "Actions";
    private const string ProtocolNoneValue = "";
    private const string ProtocolBothValue = "Both";
    private const int BackendOrderRefreshLimit = 100;
    private const int DesignedHeaderHeight = DesignedGridTheme.HeaderHeight;
    private const int DesignedRowHeight = DesignedGridTheme.RowHeight;
    private const int DesignedIconButtonSize = DesignedGridTheme.IconButtonSize;
    private const int DesignedActionIconSize = DesignedGridTheme.ActionIconSize;
    private const int DesignedAppIconSize = 24;
    private const int DesignedRuntimeIconSize = 24;
    private const int DesignedProxyIconSize = 28;
    // Manual sizing: these constants control the in-grid "Dùng/Gắn Proxy" button icon and spacing.
    private const int DesignedAttachProxyIconSize = 20;
    private const int DesignedAttachProxyButtonLeftPadding = 10;
    private const int DesignedAttachProxyButtonTextGap = 6;
    private const int DesignedAttachProxyButtonRightPadding = 18;
    private const int DesignedPathIconSize = 22;
    private const int DesignedPathPillHeight = 28;
    private const int DesignedToolbarButtonHeight = 40;
    private const int FilterToolbarRowHeight = 40;
    private static readonly Color DesignedHeaderBackColor = DesignedGridTheme.HeaderBackColor;
    private static readonly Color DesignedBorderColor = DesignedGridTheme.BorderColor;
    private static readonly Color DesignedTextColor = DesignedGridTheme.TextColor;
    private static readonly Color DesignedMutedColor = DesignedGridTheme.MutedColor;
    private static readonly Color DesignedAccentColor = DesignedGridTheme.AccentColor;
    private static readonly Font DesignedCellFont = DesignedGridTheme.CellFont;
    private static readonly Font DesignedSmallFont = DesignedGridTheme.SmallFont;
    private readonly ApplicationRuleService _applicationRuleService;
    private readonly ProxyManagementService _proxyManagementService;
    private readonly IAppProcessScanner _processScanner;
    private readonly IProxyOrderApiClient _proxyOrderApiClient;
    private readonly IProxyOrderCacheService _proxyOrderCacheService;
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IApplicationRuntimeController _applicationRuntimeController;
    private readonly IAuthService _authService;
    private readonly ExpiredBackendProxyAssignmentService _expiredBackendProxyAssignmentService;
    private readonly ManualProxyRotateGuard _manualProxyRotateGuard;
    private readonly IServiceProvider _serviceProvider;
    private readonly DataGridView _grid = new AppDataGridView();
    private readonly ContextMenuStrip _rowContextMenu = new();
    private readonly Panel _emptyState = new();
    private readonly Panel _tableHost = new();
    // Manual sizing: adjust Width here for the application search and filter controls in this toolbar.
    private readonly AppTextInput _searchTextBox = new() { Width = 340 };
    private readonly ComboBox _enabledFilterCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly ComboBox _columnVisibilityCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly ComboBox _pageSizeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly Label _statusLabel = AppDataGridFooter.CreateStatusLabel();
    private readonly Label _pageLabel = new();
    private readonly FlowLayoutPanel _leadingActionHost = new();
    private readonly Dictionary<string, Image> _applicationIconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _searchDebounceTimer = new() { Interval = 700 };
    private static readonly IReadOnlyList<ProtocolCellOption> EditableProtocolOptions =
    [
        new(ProxyProtocol.Https.ToString(), "HTTP"),
        new(ProxyProtocol.Socks5.ToString(), "SOCKS5")
    ];
    private static readonly IReadOnlyList<ProtocolCellOption> ProtocolCellOptions =
    [
        new(ProtocolNoneValue, "-"),
        .. EditableProtocolOptions,
        new(ProtocolBothValue, "HTTP/SOCKS5")
    ];

    private List<ApplicationRule> _rules = [];
    private List<ProxyServer> _proxies = [];
    private readonly Dictionary<Guid, bool> _runtimeRunningStates = [];
    private bool _isBinding;
    private bool _isTogglingRuleEnabled;
    private bool _isRotatingRuleProxy;
    private bool _isTogglingAllRuleEnabled;
    private int _filteredRuleCount;
    private int _page = ProxyOrderListConstants.DefaultPage;
    private int _limit = ProxyOrderListConstants.DefaultLimit;
    private Button _previousPageButton = null!;
    private Button _nextPageButton = null!;

    public ApplicationRulesControl(
        ApplicationRuleService applicationRuleService,
        ProxyManagementService proxyManagementService,
        IAppProcessScanner processScanner,
        IProxyOrderApiClient proxyOrderApiClient,
        IProxyOrderCacheService proxyOrderCacheService,
        IAppSettingsRepository appSettingsRepository,
        IApplicationRuntimeController applicationRuntimeController,
        IAuthService authService,
        ExpiredBackendProxyAssignmentService expiredBackendProxyAssignmentService,
        ManualProxyRotateGuard manualProxyRotateGuard,
        IServiceProvider serviceProvider)
    {
        _applicationRuleService = applicationRuleService;
        _proxyManagementService = proxyManagementService;
        _processScanner = processScanner;
        _proxyOrderApiClient = proxyOrderApiClient;
        _proxyOrderCacheService = proxyOrderCacheService;
        _appSettingsRepository = appSettingsRepository;
        _applicationRuntimeController = applicationRuntimeController;
        _authService = authService;
        _expiredBackendProxyAssignmentService = expiredBackendProxyAssignmentService;
        _manualProxyRotateGuard = manualProxyRotateGuard;
        _serviceProvider = serviceProvider;

        Dock = DockStyle.Fill;
        BuildUi();
        LightTheme.Apply(this);
        ApplyApplicationGridTheme();
    }

    public event EventHandler<ProfileAffectingChange>? ProfileAffectingChanged;

    public event EventHandler<Guid>? ViewProxyRequested;

    public Func<Task<bool>>? LoginRequestedAsync { get; set; }

    public void SetLeadingToolbarControl(Control control)
    {
        _leadingActionHost.Controls.Clear();
        control.Margin = Padding.Empty;
        _leadingActionHost.Controls.Add(control);
        _leadingActionHost.Visible = true;
    }

    public async Task LoadAsync()
    {
        await ApplySavedColumnVisibilityAsync();
        await RefreshRulesAsync(showUserMessages: false);
    }

    private async Task LoadGridOnlyAsync()
    {
        _rules = (await _applicationRuleService.GetAllAsync(CancellationToken.None)).ToList();
        _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
        await RefreshRuntimeStatesAsync();
        BindGrid();
    }

    public async Task LoadAndSelectApplicationsUsingProxyAsync(Guid proxyId)
    {
        await LoadAsync();
        MoveToFirstPageContainingProxy(proxyId);
        SelectApplicationsUsingProxy(proxyId);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildToolbar(), 0, 0);
        root.Controls.Add(BuildTableHost(), 0, 1);
        root.Controls.Add(BuildBottomBar(), 0, 2);
        Controls.Add(root);
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
            ResetPageAndBindGrid();
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
        Math.Max(1, (int)Math.Ceiling(_filteredRuleCount / (double)Math.Max(1, _limit)));

    private void UpdatePager()
    {
        if (_previousPageButton is null || _nextPageButton is null)
        {
            return;
        }

        var totalPages = GetTotalPages();
        _pageLabel.Text = $"{_page}/{totalPages}";
        var canPage = _filteredRuleCount > 0;
        _previousPageButton.Enabled = canPage && _page > 1;
        _nextPageButton.Enabled = canPage && _page < totalPages;
    }

    private Control BuildTableHost()
    {
        _tableHost.Dock = DockStyle.Fill;
        _tableHost.BackColor = Color.White;
        _tableHost.Padding = Padding.Empty;

        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.White
        };
        ConfigureGrid();
        ConfigureEmptyState();
        gridHost.Controls.Add(_grid);
        gridHost.Controls.Add(_emptyState);

        _tableHost.Controls.Add(gridHost);
        return _tableHost;
    }

    private Control BuildToolbar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 6)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(BuildActionToolbarRow(), 0, 0);
        panel.Controls.Add(BuildFilterToolbarRow(), 0, 1);
        return panel;
    }

    private Control BuildActionToolbarRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 4)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _leadingActionHost.AutoSize = true;
        _leadingActionHost.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _leadingActionHost.FlowDirection = FlowDirection.LeftToRight;
        _leadingActionHost.WrapContents = false;
        _leadingActionHost.Margin = Padding.Empty;
        _leadingActionHost.Visible = _leadingActionHost.Controls.Count > 0;

        var toolbar = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        toolbar.Controls.Add(CreateDesignedToolbarGroup(
            withRightBorder: true,
            CreateDesignedToolbarButton("Thêm ứng dụng", UiIcons.NewAddApplication, UiIcons.NewAddApplicationHover, async (_, _) => await AddRuleAsync(), accentAtRest: false, minimumWidth: 158),
            CreateDesignedToolbarButton("Gắn proxy tự động", UiIcons.NewAutoAssignProxy, UiIcons.NewAutoAssignProxyHover, async (_, _) => await AutoAssignAsync(), accentAtRest: false, minimumWidth: 178)));
        toolbar.Controls.Add(CreateDesignedToolbarGroup(
            withRightBorder: false,
            CreateDesignedToolbarButton("Làm mới", UiIcons.NewReset, UiIcons.NewResetHover, async (_, _) => await RefreshRulesAsync(showUserMessages: true), accentAtRest: false, minimumWidth: 106),
            CreateDesignedToolbarButton("Xóa tất cả", UiIcons.NewRemoveAll, UiIcons.NewRemoveAllHover, async (_, _) => await DeleteAllAsync(), accentAtRest: false, minimumWidth: 116)));

        row.Controls.Add(_leadingActionHost, 0, 0);
        row.Controls.Add(new Panel { Dock = DockStyle.Fill }, 1, 0);
        row.Controls.Add(toolbar, 2, 0);
        return row;
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

    private static Button CreateDesignedToolbarButton(
        string text,
        Image icon,
        Image hoverIcon,
        EventHandler onClick,
        bool accentAtRest,
        int minimumWidth)
    {
        var button = new DesignedToolbarButton
        {
            Text = text,
            Image = icon,
            HoverImage = hoverIcon,
            RestTextColor = accentAtRest ? DesignedGridTheme.AccentColor : DesignedGridTheme.TextColor,
            HoverTextColor = DesignedGridTheme.AccentColor,
            MinimumSize = new Size(minimumWidth, DesignedToolbarButtonHeight),
            Height = DesignedToolbarButtonHeight,
           Padding = new Padding(6),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular)
        };
        button.Click += onClick;
        return button;
    }

    private Control BuildFilterToolbarRow()
    {
        _searchTextBox.PlaceholderText = "Tìm theo tên ứng dụng...";
        _searchTextBox.TextChanged += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            ResetPageAndBindGrid();
        };

        _enabledFilterCombo.Items.AddRange(["Tất cả", "Đang bật", "Đang tắt"]);
        _enabledFilterCombo.SelectedIndex = 0;
        _enabledFilterCombo.SelectedIndexChanged += (_, _) => ResetPageAndBindGrid();
        _columnVisibilityCombo.SelectionChangeCommitted += async (_, _) => await ToggleSelectedColumnVisibilityAsync();
        RefreshColumnVisibilityCombo();

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = FilterToolbarRowHeight,
            ColumnCount = 9,
            RowCount = 1,
            Margin = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, FilterToolbarRowHeight));

        foreach (var input in new Control[] { _searchTextBox, _enabledFilterCombo, _columnVisibilityCombo })
        {
            input.Anchor = AnchorStyles.Left;
            input.Margin = new Padding(0, 0, 10, 0);
        }

        var clearButton = new AppClearFilterButton();
        clearButton.Click += (_, _) => ClearFilters();
        clearButton.Anchor = AnchorStyles.Right;
        clearButton.Margin = Padding.Empty;

        panel.Controls.Add(CreateInlineFilterGroup(_searchTextBox), 0, 0);
        panel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 1, 0);
        panel.Controls.Add(CreateToolbarLabel("Dùng Proxy:", new Padding(0, 0, 8, 0)), 2, 0);
        panel.Controls.Add(CreateInlineFilterGroup(_enabledFilterCombo), 3, 0);
        panel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 4, 0);
        panel.Controls.Add(CreateToolbarLabel("Hiển thị:", new Padding(0, 0, 8, 0)), 5, 0);
        panel.Controls.Add(CreateInlineFilterGroup(_columnVisibilityCombo), 6, 0);
        panel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 7, 0);
        panel.Controls.Add(clearButton, 8, 0);
        return panel;
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

    private static Label CreateToolbarLabel(string text, Padding margin) =>
        new()
        {
            Text = text,
            AutoSize = false,
            Height = LightTheme.InputHeight,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            // Manual sizing: change this font size for toolbar labels such as "Dùng Proxy:" and "Hiển thị:".
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
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
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.EditMode = DataGridViewEditMode.EditProgrammatically;
        _grid.AllowUserToResizeRows = false;
        _grid.Tag = LightTheme.SkipActiveGridHeaderThemeTag;
        _grid.RowTemplate.Height = DesignedRowHeight;
        _grid.CellMouseDown += HandleGridCellMouseDown;
        _grid.CellClick += HandleGridCellClick;
        _grid.MouseMove += HandleGridMouseMove;
        _grid.MouseLeave += HandleGridMouseLeave;
        _grid.CellPainting += PaintDesignedColumnHeaderCell;
        _grid.CellPainting += PaintEnabledCell;
        _grid.CellPainting += PaintApplicationNameCell;
        _grid.CellPainting += PaintAssignedProxyCell;
        _grid.CellPainting += PaintApplicationPathCell;
        _grid.CellPainting += PaintDesignedTextCell;
        _grid.CellPainting += PaintEnabledHeaderCell;
        _grid.CellPainting += PaintActionCell;
        _grid.Paint += PaintEnabledHeaderCheckBoxOverlay;
        _grid.ColumnHeaderMouseClick += HandleGridColumnHeaderMouseClick;
        _grid.CellValueChanged += async (_, e) => await HandleGridCellValueChangedAsync(e);
        _grid.EditingControlShowing += HandleEditingControlShowing;
        _grid.CellToolTipTextNeeded += HandleCellToolTipTextNeeded;
        _grid.DataBindingComplete += (_, _) => ApplyDesignedGridMetrics();
        _grid.SizeChanged += (_, _) => ApplyDesignedGridMetrics();
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        ConfigureGridColumns();
        BuildRowContextMenu();
        _ = new GridEmptyStateOverlay(_grid, GetEmptyStateContent);
    }

    private void ApplyApplicationGridTheme()
    {
        _tableHost.BackColor = Color.White;

        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = DesignedHeaderHeight;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = DesignedHeaderBackColor;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = DesignedTextColor;
        _grid.ColumnHeadersDefaultCellStyle.Font = DesignedCellFont;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = DesignedHeaderBackColor;
        _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = DesignedTextColor;
        _grid.DefaultCellStyle.BackColor = Color.White;
        _grid.DefaultCellStyle.ForeColor = DesignedTextColor;
        _grid.DefaultCellStyle.Font = DesignedCellFont;
        _grid.DefaultCellStyle.SelectionBackColor = DesignedGridTheme.SelectedRowBackColor;
        _grid.DefaultCellStyle.SelectionForeColor = DesignedTextColor;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.White;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = DesignedGridTheme.SelectedRowBackColor;
        _grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = DesignedTextColor;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.RowTemplate.Height = DesignedRowHeight;
        _grid.RowHeadersVisible = false;
        _grid.GridColor = DesignedGridTheme.RowBorderColor;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
        _grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
        _grid.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
        _grid.CellPainting -= PaintDesignedColumnHeaderCell;
        _grid.CellPainting += PaintDesignedColumnHeaderCell;
        _grid.CellPainting -= PaintEnabledHeaderCell;
        _grid.CellPainting += PaintEnabledHeaderCell;
        ApplyDesignedGridMetrics();
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
            // Text = "Chưa có ứng dụng nào.\nHãy thêm file .exe hoặc quét runtime/headless process của giả lập."
            Text = "Chưa có ứng dụng nào"
        });
    }

    private GridEmptyStateContent? GetEmptyStateContent()
    {
        if (_filteredRuleCount > 0)
        {
            return null;
        }

        if (_rules.Count == 0)
        {
            return GridEmptyStateContent.TextOnly("Chưa có ứng dụng nào.");
        }

        return GridEmptyStateContent.TextOnly("Không tìm thấy ứng dụng phù hợp với bộ lọc hiện tại.");
    }

    private void BuildRowContextMenu()
    {
        _rowContextMenu.Items.Clear();
        _rowContextMenu.Items.Add("Xóa các ứng dụng đã chọn", UiIcons.TrashGlyph, async (_, _) => await DeleteSelectedAsync());
        _rowContextMenu.Opening += (_, e) => e.Cancel = GetSelectedRules().ToList().Count == 0;
        _grid.ContextMenuStrip = _rowContextMenu;
    }

    private void ConfigureGridColumns()
    {
        var enabledColumn = new DataGridViewCheckBoxColumn
        {
            Name = nameof(RuleRow.IsEnabled),
            HeaderText = string.Empty,
            DataPropertyName = nameof(RuleRow.IsEnabled),
            MinimumWidth = 48,
            FillWeight = 4,
            ReadOnly = true
        };
        _grid.Columns.Add(enabledColumn);
        _grid.Columns.Add(CreateTextColumn("Ứng dụng", nameof(RuleRow.ApplicationName), 200, 17));
        _grid.Columns.Add(CreateTextColumn("Proxy đã gắn", nameof(RuleRow.AssignedProxy), 262, 22));
        _grid.Columns.Add(CreateTextColumn("Đường dẫn", nameof(RuleRow.ApplicationPath), 170, 14));
        _grid.Columns.Add(CreateTextColumn("Loại Proxy", nameof(RuleRow.ProxyType), 104, 9));
        _grid.Columns.Add(CreateTextColumn("Nhà mạng", nameof(RuleRow.Provider), 104, 9));
        _grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = nameof(RuleRow.ProtocolValue),
            HeaderText = "Giao thức",
            DataPropertyName = nameof(RuleRow.ProtocolValue),
            MinimumWidth = 104,
            FillWeight = 9,
            DataSource = ProtocolCellOptions.ToList(),
            DisplayMember = nameof(ProtocolCellOption.DisplayName),
            ValueMember = nameof(ProtocolCellOption.Value),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                DisplayStyleForCurrentCellOnly = false
        });
        _grid.Columns.Add(CreateTextColumn("Cảnh báo", nameof(RuleRow.Warning), 104, 9));
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ActionsColumnName,
            HeaderText = "Hành động",
            DataPropertyName = nameof(RuleRow.Actions),
            MinimumWidth = 84,
            FillWeight = 7,
            ReadOnly = true
        });
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, string propertyName, int minimumWidth, float fillWeight)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = propertyName,
            HeaderText = header,
            DataPropertyName = propertyName,
            MinimumWidth = minimumWidth,
            FillWeight = fillWeight,
            ReadOnly = true
        };
    }

    private void HandleGridCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0)
        {
            return;
        }

        var selectedRowIndexes = _grid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index).ToHashSet();
        var clickedRowWasSelected = selectedRowIndexes.Contains(e.RowIndex);

        if (!clickedRowWasSelected)
        {
            _grid.ClearSelection();
            _grid.Rows[e.RowIndex].Selected = true;
        }

        _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[Math.Max(e.ColumnIndex, 0)];

        if (clickedRowWasSelected)
        {
            foreach (var rowIndex in selectedRowIndexes)
            {
                _grid.Rows[rowIndex].Selected = true;
            }
        }
    }

    private async void HandleGridCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        var column = _grid.Columns[e.ColumnIndex];
        if (column.DataPropertyName == nameof(RuleRow.ApplicationName))
        {
            var appCursorPoint = _grid.PointToClient(Cursor.Position);
            if (TryGetRuntimeApplicationButtonBounds(e.RowIndex, e.ColumnIndex, out var runtimeButtonBounds) &&
                runtimeButtonBounds.Contains(appCursorPoint) &&
                _grid.Rows[e.RowIndex].DataBoundItem is RuleRow runtimeRow)
            {
                if (runtimeRow.IsRuntimeRunning)
                {
                    await StopApplicationAsync(e.RowIndex);
                }
                else
                {
                    await StartApplicationAsync(e.RowIndex);
                }

                return;
            }

            return;
        }

        if (column.DataPropertyName == nameof(RuleRow.IsEnabled))
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is RuleRow enabledRow &&
                !enabledRow.IsEnabled &&
                !enabledRow.CanUseProxy)
            {
                await ShowEnableBlockedWarningAsync(enabledRow);
                return;
            }

            if (_isTogglingRuleEnabled)
            {
                return;
            }

            _isTogglingRuleEnabled = true;
            try
            {
                await ToggleRuleEnabledAsync(e.RowIndex);
            }
            finally
            {
                _isTogglingRuleEnabled = false;
            }

            return;
        }

        if (column.Name == ActionsColumnName)
        {
            var actionCursorPoint = _grid.PointToClient(Cursor.Position);
            if (TryGetDeleteRuleButtonBounds(e.RowIndex, e.ColumnIndex, out var deleteButtonBounds) &&
                deleteButtonBounds.Contains(actionCursorPoint))
            {
                await DeleteRuleAsync(e.RowIndex);
                return;
            }

            if (TryGetRotateProxyButtonBounds(e.RowIndex, e.ColumnIndex, out var rotateButtonBounds) &&
                rotateButtonBounds.Contains(actionCursorPoint))
            {
                await RotateRuleProxyAsync(e.RowIndex);
            }

            return;
        }

        if (column.DataPropertyName == nameof(RuleRow.ProtocolValue))
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is RuleRow protocolRow && protocolRow.CanEditProtocol)
            {
                _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                _grid.BeginEdit(selectAll: false);
            }

            return;
        }

        if (column.DataPropertyName == nameof(RuleRow.ApplicationPath))
        {
            return;
        }

        if (column.DataPropertyName != nameof(RuleRow.AssignedProxy) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not RuleRow row)
        {
            return;
        }

        var cursorPoint = _grid.PointToClient(Cursor.Position);
        if (row.AssignedProxyId is null)
        {
            if (TryGetAssignProxyButtonBounds(e.RowIndex, e.ColumnIndex, out var assignButtonBounds) &&
                assignButtonBounds.Contains(cursorPoint))
            {
                await AssignProxyForRowAsync(row.Id);
            }

            return;
        }

        if (TryGetAssignedProxyViewButtonBounds(e.RowIndex, e.ColumnIndex, out var buttonBounds) &&
            buttonBounds.Contains(cursorPoint) &&
            row.AssignedProxyId is { } proxyId)
        {
            ViewProxyRequested?.Invoke(this, proxyId);
            return;
        }

        if (TryGetAssignedProxyClearButtonBounds(e.RowIndex, e.ColumnIndex, out var clearButtonBounds) &&
            clearButtonBounds.Contains(cursorPoint))
        {
            await ClearAssignmentForRowAsync(row.Id);
        }
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
            return column.Name == nameof(RuleRow.IsEnabled);
        }

        if (hit.RowIndex < 0)
        {
            return false;
        }

        if (column.DataPropertyName == nameof(RuleRow.IsEnabled))
        {
            return true;
        }

        if (column.DataPropertyName == nameof(RuleRow.ProtocolValue) &&
            _grid.Rows[hit.RowIndex].DataBoundItem is RuleRow protocolRow)
        {
            return protocolRow.CanEditProtocol;
        }

        return TryGetRuntimeApplicationButtonBounds(hit.RowIndex, hit.ColumnIndex, out var runtimeBounds) && runtimeBounds.Contains(point) ||
            TryGetAssignProxyButtonBounds(hit.RowIndex, hit.ColumnIndex, out var assignBounds) && assignBounds.Contains(point) ||
            TryGetAssignedProxyViewButtonBounds(hit.RowIndex, hit.ColumnIndex, out var viewBounds) && viewBounds.Contains(point) ||
            TryGetAssignedProxyClearButtonBounds(hit.RowIndex, hit.ColumnIndex, out var clearBounds) && clearBounds.Contains(point) ||
            TryGetDeleteRuleButtonBounds(hit.RowIndex, hit.ColumnIndex, out var deleteBounds) && deleteBounds.Contains(point) ||
            TryGetRotateProxyButtonBounds(hit.RowIndex, hit.ColumnIndex, out var rotateBounds) && rotateBounds.Contains(point);
    }

    private void PaintDesignedColumnHeaderCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].Name == nameof(RuleRow.IsEnabled) ||
            e.Graphics is null)
        {
            return;
        }

        DesignedGridTheme.PaintHeaderCell(_grid, e, IsActiveHeaderColumn(e.ColumnIndex));
    }

    private void PaintEnabledCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].Name != nameof(RuleRow.IsEnabled) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not RuleRow row ||
            e.Graphics is null)
        {
            return;
        }

        PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var checkBoxBounds = GetCenteredCheckBoxBounds(e.CellBounds, 16);
        var state = row.IsEnabled
            ? CheckBoxState.CheckedNormal
            : row.CanUseProxy
                ? CheckBoxState.UncheckedNormal
                : CheckBoxState.UncheckedDisabled;
        CheckBoxRenderer.DrawCheckBox(e.Graphics, checkBoxBounds.Location, state);
        e.Handled = true;
    }

    private void PaintApplicationNameCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(RuleRow.ApplicationName) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not RuleRow row ||
            e.Graphics is null)
        {
            return;
        }

        PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var textRight = e.CellBounds.Right - 12;
        if (row.TargetTypeValue == ApplicationTargetType.Executable)
        {
            var buttonBounds = GetRuntimeButtonBounds(e.CellBounds);
            DrawCenteredImage(e.Graphics, buttonBounds, row.IsRuntimeRunning ? UiIcons.NewStop : UiIcons.NewStart, DesignedRuntimeIconSize);
            textRight = buttonBounds.Left - 8;
        }

        var iconBounds = new Rectangle(
            e.CellBounds.Left + 12,
            e.CellBounds.Top + (e.CellBounds.Height - DesignedAppIconSize) / 2,
            DesignedAppIconSize,
            DesignedAppIconSize);
        DrawApplicationIcon(e.Graphics, row, iconBounds);

        var textBounds = new Rectangle(
            iconBounds.Right + 8,
            e.CellBounds.Top,
            Math.Max(0, textRight - iconBounds.Right - 8),
            e.CellBounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            row.ApplicationName,
            DesignedCellFont,
            textBounds,
            DesignedTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.Handled = true;
    }

    private void PaintAssignedProxyCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(RuleRow.AssignedProxy) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not RuleRow row ||
            e.Graphics is null)
        {
            return;
        }

        PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        if (row.AssignedProxyId is null)
        {
            DrawAttachProxyButton(e.Graphics, GetAssignProxyButtonBounds(e.CellBounds));
            e.Handled = true;
            return;
        }

        var viewButtonBounds = GetAssignedProxyViewButtonBounds(e.CellBounds);
        var clearButtonBounds = GetAssignedProxyClearButtonBounds(e.CellBounds);
        var textBounds = new Rectangle(
            e.CellBounds.Left + 12,
            e.CellBounds.Top,
            Math.Max(0, viewButtonBounds.Left - e.CellBounds.Left - 24),
            e.CellBounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            row.AssignedProxy,
            DesignedCellFont,
            textBounds,
            DesignedTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        DrawCenteredImage(e.Graphics, viewButtonBounds, UiIcons.NewViewProxy, DesignedProxyIconSize);
        DrawCenteredImage(e.Graphics, clearButtonBounds, UiIcons.NewUnlinkProxy, DesignedProxyIconSize);
        e.Handled = true;
    }

    private void PaintApplicationPathCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(RuleRow.ApplicationPath) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not RuleRow row ||
            e.Graphics is null)
        {
            return;
        }

        PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var pillBounds = new Rectangle(
            e.CellBounds.Left + 12,
            e.CellBounds.Top + Math.Max(0, (e.CellBounds.Height - DesignedPathPillHeight) / 2),
            Math.Max(0, e.CellBounds.Width - 24),
            DesignedPathPillHeight);
        DrawPathPill(e.Graphics, pillBounds, row.ApplicationPath);
        e.Handled = true;
    }

    private void PaintDesignedTextCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            e.Graphics is null)
        {
            return;
        }

        var column = _grid.Columns[e.ColumnIndex];
        if (column.DataPropertyName is not (nameof(RuleRow.ProxyType) or nameof(RuleRow.Provider) or nameof(RuleRow.ProtocolValue) or nameof(RuleRow.Warning)))
        {
            return;
        }

        if (_grid.IsCurrentCellInEditMode &&
            _grid.CurrentCell?.RowIndex == e.RowIndex &&
            _grid.CurrentCell.ColumnIndex == e.ColumnIndex)
        {
            return;
        }

        PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        var isProtocolColumn = column.DataPropertyName == nameof(RuleRow.ProtocolValue);
        var canEditProtocol = isProtocolColumn &&
            _grid.Rows[e.RowIndex].DataBoundItem is RuleRow row &&
            row.CanEditProtocol;
        var rightPadding = canEditProtocol ? 30 : 12;
        var textBounds = new Rectangle(
            e.CellBounds.Left + 12,
            e.CellBounds.Top,
            Math.Max(0, e.CellBounds.Width - 12 - rightPadding),
            e.CellBounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            e.FormattedValue?.ToString() ?? string.Empty,
            DesignedCellFont,
            textBounds,
            DesignedTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (canEditProtocol)
        {
            DrawDropdownIndicator(e.Graphics, e.CellBounds);
        }

        e.Handled = true;
    }

    private void PaintActionCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].Name != ActionsColumnName ||
            _grid.Rows[e.RowIndex].DataBoundItem is not RuleRow row ||
            e.Graphics is null)
        {
            return;
        }

        PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        DrawGrayIconButton(e.Graphics, GetDeleteRuleButtonBounds(e.CellBounds, row.CanRotateProxy), UiIcons.NewRemove);
        if (row.CanRotateProxy)
        {
            DrawGrayIconButton(e.Graphics, GetRotateProxyButtonBounds(e.CellBounds), UiIcons.RotateProxyGlyph);
        }

        e.Handled = true;
    }

    private void PaintEnabledHeaderCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].Name != nameof(RuleRow.IsEnabled) ||
            e.Graphics is null)
        {
            return;
        }

        PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: true, isActiveHeader: IsActiveHeaderColumn(e.ColumnIndex));
        var checkBoxBounds = GetEnabledHeaderCheckBoxBounds(e.CellBounds);
        CheckBoxRenderer.DrawCheckBox(
            e.Graphics,
            checkBoxBounds.Location,
            GetEnabledHeaderCheckBoxState());

        e.Handled = true;
    }

    private void PaintEnabledHeaderCheckBoxOverlay(object? sender, PaintEventArgs e)
    {
        if (!_grid.Columns.Contains(nameof(RuleRow.IsEnabled)) ||
            !_grid.Columns[nameof(RuleRow.IsEnabled)].Visible)
        {
            return;
        }

        var columnIndex = _grid.Columns[nameof(RuleRow.IsEnabled)].Index;
        var bounds = _grid.GetCellDisplayRectangle(columnIndex, -1, cutOverflow: true);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var checkBoxBounds = GetEnabledHeaderCheckBoxBounds(bounds);
        CheckBoxRenderer.DrawCheckBox(
            e.Graphics,
            checkBoxBounds.Location,
            GetEnabledHeaderCheckBoxState());
    }

    private void ApplyDesignedGridMetrics()
    {
        DesignedGridTheme.ApplyMetrics(_grid);
        _grid.Invalidate();
    }

    private bool IsActiveHeaderColumn(int columnIndex) =>
        _grid.CurrentCell is not null && columnIndex == _grid.CurrentCell.ColumnIndex;

    private void PaintCellBackgroundAndBorder(
        Graphics graphics,
        Rectangle bounds,
        bool isHeader,
        bool isActiveHeader = false,
        bool isSelected = false)
    {
        DesignedGridTheme.PaintCellBackgroundAndBorder(graphics, bounds, isHeader, isActiveHeader, isSelected);
    }

    private static void DrawCenteredImage(Graphics graphics, Rectangle bounds, Image icon, int iconSize)
    {
        DesignedGridTheme.DrawCenteredImage(graphics, bounds, icon, iconSize);
    }

    private void DrawApplicationIcon(Graphics graphics, RuleRow row, Rectangle bounds)
    {
        var icon = GetApplicationIcon(row.ApplicationIconPath);
        graphics.DrawImage(icon, bounds);
    }

    private Image GetApplicationIcon(string iconPath)
    {
        var key = string.IsNullOrWhiteSpace(iconPath) ? "__default__" : iconPath;
        if (_applicationIconCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        Image image = SystemIcons.Application.ToBitmap();
        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(iconPath);
                image = icon?.ToBitmap() ?? image;
            }
            catch
            {
                // Keep the default application icon.
            }
        }

        _applicationIconCache[key] = image;
        return image;
    }

    private static void DrawAttachProxyButton(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = AppButton.CreateRoundedRectangle(
            new Rectangle(bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1),
            4);
        using var border = new Pen(DesignedAccentColor);
        using var background = new SolidBrush(Color.White);
        graphics.FillPath(background, path);
        graphics.DrawPath(border, path);

        var iconBounds = new Rectangle(
            bounds.Left + DesignedAttachProxyButtonLeftPadding,
            bounds.Top + (bounds.Height - DesignedAttachProxyIconSize) / 2,
            DesignedAttachProxyIconSize,
            DesignedAttachProxyIconSize);
        DrawCenteredImage(graphics, iconBounds, UiIcons.NewAttachProxy, DesignedAttachProxyIconSize);
        var textBounds = new Rectangle(
            iconBounds.Right + DesignedAttachProxyButtonTextGap,
            bounds.Top,
            Math.Max(0, bounds.Right - iconBounds.Right - DesignedAttachProxyButtonTextGap - DesignedAttachProxyButtonRightPadding),
            bounds.Height);
        TextRenderer.DrawText(
            graphics,
            AssignProxyButtonText,
            DesignedCellFont,
            textBounds,
            DesignedAccentColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static void DrawPathPill(Graphics graphics, Rectangle bounds, string text)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = AppButton.CreateRoundedRectangle(
            new Rectangle(bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1),
            10);
        using var background = new SolidBrush(DesignedHeaderBackColor);
        graphics.FillPath(background, path);
        TextRenderer.DrawText(
            graphics,
            text,
            DesignedSmallFont,
            new Rectangle(bounds.Left + 6, bounds.Top, Math.Max(0, bounds.Width - 12), bounds.Height),
            DesignedTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static void DrawGrayIconButton(Graphics graphics, Rectangle bounds, Image icon)
    {
        DesignedGridTheme.DrawGrayIconButton(graphics, bounds, icon);
    }

    private static void DrawDropdownIndicator(Graphics graphics, Rectangle cellBounds)
    {
        using var brush = new SolidBrush(DesignedMutedColor);
        var centerX = cellBounds.Right - 16;
        var centerY = cellBounds.Top + cellBounds.Height / 2 + 1;
        var points = new[]
        {
            new Point(centerX - 4, centerY - 2),
            new Point(centerX + 4, centerY - 2),
            new Point(centerX, centerY + 3)
        };
        graphics.FillPolygon(brush, points);
    }

    private static Rectangle GetAssignedProxyViewButtonBounds(Rectangle cellBounds)
    {
        var clearBounds = GetAssignedProxyClearButtonBounds(cellBounds);
        return new Rectangle(
            clearBounds.Left - DesignedIconButtonSize - 4,
            clearBounds.Top,
            DesignedIconButtonSize,
            DesignedIconButtonSize);
    }

    private static Rectangle GetAssignedProxyClearButtonBounds(Rectangle cellBounds)
    {
        return GetRightIconButtonBounds(cellBounds, rightPadding: 12);
    }

    private static Rectangle GetAssignProxyButtonBounds(Rectangle cellBounds)
    {
        var availableWidth = Math.Max(0, cellBounds.Width - 24);
        var textWidth = TextRenderer.MeasureText(
            AssignProxyButtonText,
            DesignedCellFont).Width;
        var minimumContentWidth =
            DesignedAttachProxyButtonLeftPadding +
            DesignedAttachProxyIconSize +
            DesignedAttachProxyButtonTextGap +
            textWidth +
            DesignedAttachProxyButtonRightPadding;
        var width = Math.Min(availableWidth, minimumContentWidth);
        var height = Math.Min(32, Math.Max(0, cellBounds.Height - 8));
        return new Rectangle(
            cellBounds.Left + 12,
            cellBounds.Top + Math.Max(0, (cellBounds.Height - height) / 2),
            width,
            height);
    }

    private static Rectangle GetRotateProxyButtonBounds(Rectangle cellBounds)
    {
        var layouts = GetActionButtonBounds(cellBounds, includeRotate: true);
        return layouts[1];
    }

    private static Rectangle GetDeleteRuleButtonBounds(Rectangle cellBounds, bool includeRotate)
    {
        var layouts = GetActionButtonBounds(cellBounds, includeRotate);
        return layouts[0];
    }

    private static IReadOnlyList<Rectangle> GetActionButtonBounds(Rectangle cellBounds, bool includeRotate) =>
        GridCellButtonRenderer.GetCenteredIconButtonBounds(cellBounds, includeRotate ? 2 : 1, buttonSize: DesignedIconButtonSize, gap: 4);

    private static Rectangle GetRuntimeButtonBounds(Rectangle cellBounds)
    {
        const int buttonSize = DesignedIconButtonSize;
        var top = cellBounds.Top + Math.Max(3, (cellBounds.Height - buttonSize) / 2);
        return new Rectangle(cellBounds.Right - buttonSize - 12, top, buttonSize, buttonSize);
    }

    private static Rectangle GetEnabledHeaderCheckBoxBounds(Rectangle cellBounds)
    {
        return GetCenteredCheckBoxBounds(cellBounds, 16);
    }

    private static Rectangle GetCenteredCheckBoxBounds(Rectangle cellBounds, int size)
    {
        return new Rectangle(
            cellBounds.Left + Math.Max(0, (cellBounds.Width - size) / 2),
            cellBounds.Top + Math.Max(0, (cellBounds.Height - size) / 2),
            size,
            size);
    }

    private static Rectangle GetRightIconButtonBounds(Rectangle cellBounds, int rightPadding)
    {
        var top = cellBounds.Top + Math.Max(0, (cellBounds.Height - DesignedIconButtonSize) / 2);
        return new Rectangle(
            cellBounds.Right - DesignedIconButtonSize - rightPadding,
            top,
            DesignedIconButtonSize,
            DesignedIconButtonSize);
    }

    private bool TryGetAssignedProxyViewButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Columns[columnIndex].DataPropertyName != nameof(RuleRow.AssignedProxy) ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row ||
            row.AssignedProxyId is null)
        {
            return false;
        }

        bounds = GetAssignedProxyViewButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private bool TryGetAssignedProxyClearButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Columns[columnIndex].DataPropertyName != nameof(RuleRow.AssignedProxy) ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row ||
            row.AssignedProxyId is null)
        {
            return false;
        }

        bounds = GetAssignedProxyClearButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private bool TryGetAssignProxyButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Columns[columnIndex].DataPropertyName != nameof(RuleRow.AssignedProxy) ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row ||
            row.AssignedProxyId is not null)
        {
            return false;
        }

        bounds = GetAssignProxyButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private bool TryGetRotateProxyButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Columns[columnIndex].Name != ActionsColumnName ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row ||
            !row.CanRotateProxy)
        {
            return false;
        }

        bounds = GetRotateProxyButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private bool TryGetDeleteRuleButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Columns[columnIndex].Name != ActionsColumnName ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row)
        {
            return false;
        }

        bounds = GetDeleteRuleButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true), row.CanRotateProxy);
        return true;
    }

    private bool TryGetRuntimeApplicationButtonBounds(int rowIndex, int columnIndex, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (rowIndex < 0 ||
            columnIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Columns[columnIndex].DataPropertyName != nameof(RuleRow.ApplicationName) ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row ||
            row.TargetTypeValue != ApplicationTargetType.Executable)
        {
            return false;
        }

        bounds = GetRuntimeButtonBounds(_grid.GetCellDisplayRectangle(columnIndex, rowIndex, cutOverflow: true));
        return true;
    }

    private async void HandleGridColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].Name != nameof(RuleRow.IsEnabled) ||
            _isTogglingAllRuleEnabled)
        {
            return;
        }

        _isTogglingAllRuleEnabled = true;
        try
        {
            await ToggleAllRuleEnabledAsync();
        }
        finally
        {
            _isTogglingAllRuleEnabled = false;
        }
    }

    private void BindGrid()
    {
        _isBinding = true;
        var filteredRows = BuildRuleRows().Where(MatchesFilters).ToList();
        _filteredRuleCount = filteredRows.Count;
        var totalPages = GetTotalPages();
        _page = Math.Max(1, Math.Min(_page, totalPages));
        var pageRows = filteredRows
            .Skip((_page - 1) * _limit)
            .Take(_limit)
            .ToList();

        _grid.DataSource = pageRows;
        ApplyDesignedRowHeights();
        ConfigureProtocolCells();
        ConfigureEnabledCells();
        ApplyDesignedRowHeights();
        _grid.Visible = true;
        _emptyState.Visible = false;
        _grid.BringToFront();
        _statusLabel.Text = _filteredRuleCount == _rules.Count
            ? $"{_rules.Count} ứng dụng"
            : $"{_filteredRuleCount}/{_rules.Count} ứng dụng";
        UpdatePager();
        _grid.Invalidate();
        _isBinding = false;
    }

    private List<RuleRow> BuildRuleRows()
    {
        var duplicateCounts = _applicationRuleService.GetDuplicateExecutableCounts(_rules);
        var proxiesById = _proxies.ToDictionary(proxy => proxy.Id);
        var rows = _rules.Select(rule =>
        {
            var proxy = rule.AssignedProxyId is { } proxyId && proxiesById.TryGetValue(proxyId, out var assignedProxy)
                ? assignedProxy
                : null;
            var applicationPath = GetApplicationPath(rule);
            var eligibility = ApplicationRuleEligibility.Evaluate(rule, proxiesById);
            duplicateCounts.TryGetValue(rule.GetApplicationName(), out var count);
            var warningDetail = !string.IsNullOrWhiteSpace(rule.Warning)
                ? rule.Warning
                : count > 1
                ? "Trùng tên file: các tiến trình cùng .exe có thể dùng chung route."
                : GetRuleWarning(rule);
            var warning = FormatWarningDisplay(warningDetail, rule);

            return new RuleRow(
                rule.Id,
                rule.GetApplicationName(),
                FormatApplicationPath(applicationPath),
                applicationPath,
                rule.TargetType,
                rule.TargetType == ApplicationTargetType.Emulator ? "Giả lập" : "Ứng dụng",
                rule.TargetType == ApplicationTargetType.Emulator && rule.ProcessId is not null ? rule.ProcessId.Value.ToString() : string.Empty,
                rule.TargetType == ApplicationTargetType.Emulator ? rule.RuntimeProcessName ?? "-" : string.Empty,
                proxy is null ? "-" : FormatProxy(proxy),
                proxy?.Id,
                GetProxyTypeDisplay(proxy),
                GetProviderDisplay(proxy),
                GetProtocolValue(proxy),
                CanEditProtocol(proxy),
                CanRotateProxy(proxy),
                proxy?.BackendUserProxyId,
                eligibility.CanUseProxy,
                eligibility.Message,
                GetRuleStatus(rule, proxy),
                rule.IsEnabled,
                _runtimeRunningStates.GetValueOrDefault(rule.Id),
                warning,
                warningDetail);
        }).ToList();
        return rows;
    }

    private void ApplyDesignedRowHeights()
    {
        DesignedGridTheme.ApplyMetrics(_grid);
    }

    private async Task RefreshRuntimeStatesAsync()
    {
        _runtimeRunningStates.Clear();
        foreach (var rule in _rules.Where(rule => rule.TargetType == ApplicationTargetType.Executable))
        {
            _runtimeRunningStates[rule.Id] = await _applicationRuntimeController.IsRunningAsync(rule, CancellationToken.None);
        }
    }

    private async Task RefreshRuntimeStateAsync(ApplicationRule rule)
    {
        if (rule.TargetType != ApplicationTargetType.Executable)
        {
            _runtimeRunningStates.Remove(rule.Id);
            return;
        }

        _runtimeRunningStates[rule.Id] = await _applicationRuntimeController.IsRunningAsync(rule, CancellationToken.None);
    }

    private bool MatchesFilters(RuleRow row)
    {
        var searchText = _searchTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(searchText) &&
            !LocalFuzzySearch.IsMatch(row.ApplicationName, searchText))
        {
            return false;
        }

        // if (_targetTypeFilterCombo.SelectedIndex == 1 && row.TargetTypeValue != ApplicationTargetType.Executable)
        // {
        //     return false;
        // }

        // if (_targetTypeFilterCombo.SelectedIndex == 2 && row.TargetTypeValue != ApplicationTargetType.Emulator)
        // {
        //     return false;
        // }

        if (_enabledFilterCombo.SelectedIndex == 1 && !row.IsEnabled)
        {
            return false;
        }

        return _enabledFilterCombo.SelectedIndex != 2 || !row.IsEnabled;
    }

    private void ResetPageAndBindGrid()
    {
        _page = ProxyOrderListConstants.DefaultPage;
        BindGrid();
    }

    private void ClearFilters()
    {
        _searchDebounceTimer.Stop();
        _searchTextBox.Clear();
        // if (_targetTypeFilterCombo.Items.Count > 0)
        // {
        //     _targetTypeFilterCombo.SelectedIndex = 0;
        // }

        if (_enabledFilterCombo.Items.Count > 0)
        {
            _enabledFilterCombo.SelectedIndex = 0;
        }

        ResetPageAndBindGrid();
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

        RefreshColumnVisibilityCombo();
    }

    private async Task SaveColumnVisibilityAsync()
    {
        var settings = await _appSettingsRepository.GetAsync(CancellationToken.None);
        settings.DataGridColumnVisibility[GetColumnVisibilitySettingsKey()] = GetUserConfigurableColumns()
            .ToDictionary(column => column.Name, column => column.Visible, StringComparer.Ordinal);
        await _appSettingsRepository.SaveAsync(settings, CancellationToken.None);
    }

    private static string GetColumnVisibilitySettingsKey() => "ApplicationRules";

    private IEnumerable<DataGridViewColumn> GetUserConfigurableColumns() =>
        _grid.Columns.Cast<DataGridViewColumn>();

    private void RefreshColumnVisibilityCombo()
    {
        if (_grid.Columns.Count == 0)
        {
            return;
        }

        var options = GetUserConfigurableColumns()
            .OrderBy(column => column.DisplayIndex)
            .Select(column => new ColumnVisibilityOption(column.Name, GetColumnVisibilityDisplayText(column), column.Visible))
            .ToList();

        _columnVisibilityCombo.DataSource = null;
        _columnVisibilityCombo.DataSource = options;
        _columnVisibilityCombo.SelectedIndex = -1;
        _columnVisibilityCombo.Text = "Ẩn / hiện cột";
    }

    private static string GetColumnVisibilityDisplayText(DataGridViewColumn column) =>
        column.Name == nameof(RuleRow.IsEnabled) && string.IsNullOrWhiteSpace(column.HeaderText)
            ? EnabledHeaderText
            : column.HeaderText;

    private static string FormatProxy(ProxyServer proxy)
    {
        return proxy.DisplayValue;
    }

    private static string GetApplicationPath(ApplicationRule rule)
    {
        return rule.TargetType == ApplicationTargetType.Emulator
            ? rule.RuntimeExecutablePath ?? string.Empty
            : rule.ExecutableName;
    }

    private static string FormatApplicationPath(string path)
    {
        var trimmedPath = path.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            return "-";
        }

        var root = Path.GetPathRoot(trimmedPath);
        var fileName = Path.GetFileName(trimmedPath);
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(fileName))
        {
            return trimmedPath;
        }

        return $"{root}...\\{fileName}";
    }

    private void ConfigureProtocolCells()
    {
        if (_grid.Columns[nameof(RuleRow.ProtocolValue)] is null)
        {
            return;
        }

        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is not RuleRow row ||
                gridRow.Cells[nameof(RuleRow.ProtocolValue)] is not DataGridViewComboBoxCell cell)
            {
                continue;
            }

            cell.DataSource = row.CanEditProtocol
                ? EditableProtocolOptions.ToList()
                : ProtocolCellOptions.ToList();
            cell.DisplayMember = nameof(ProtocolCellOption.DisplayName);
            cell.ValueMember = nameof(ProtocolCellOption.Value);
            cell.ReadOnly = !row.CanEditProtocol;
            cell.DisplayStyle = row.CanEditProtocol
                ? DataGridViewComboBoxDisplayStyle.DropDownButton
                : DataGridViewComboBoxDisplayStyle.Nothing;
        }
    }

    private static string GetProxyTypeDisplay(ProxyServer? proxy) =>
        proxy?.BackendOrderKind switch
        {
            ProxyOrderKind.Static => "Tĩnh",
            ProxyOrderKind.Datacenter => "Datacenter",
            ProxyOrderKind.RotateProxy or ProxyOrderKind.RotateKey => "Xoay",
            _ => "-"
        };

    private static string GetProviderDisplay(ProxyServer? proxy)
    {
        if (proxy?.BackendOrderKind is ProxyOrderKind.RotateProxy or ProxyOrderKind.RotateKey)
        {
            return "-";
        }

        if (proxy is null || string.IsNullOrWhiteSpace(proxy.BackendProvider))
        {
            return "-";
        }

        var provider = proxy.BackendProvider.Trim();
        if (proxy.BackendOrderKind == ProxyOrderKind.Datacenter)
        {
            if (string.Equals(provider, "CMC", StringComparison.OrdinalIgnoreCase))
            {
                return "VN";
            }

            if (string.Equals(provider, "US", StringComparison.OrdinalIgnoreCase))
            {
                return "US";
            }
        }

        return provider;
    }

    private static string GetProtocolValue(ProxyServer? proxy)
    {
        if (proxy is null)
        {
            return ProtocolNoneValue;
        }

        return proxy.BackendOrderKind is ProxyOrderKind.RotateProxy or ProxyOrderKind.RotateKey
            ? ProtocolBothValue
            : proxy.Protocol.ToString();
    }

    private static bool CanEditProtocol(ProxyServer? proxy) =>
        proxy?.BackendUserProxyId is not null &&
        proxy.BackendOrderKind is ProxyOrderKind.Static or ProxyOrderKind.Datacenter;

    private static bool CanRotateProxy(ProxyServer? proxy) =>
        proxy?.BackendUserProxyId is not null &&
        proxy.BackendOrderKind == ProxyOrderKind.RotateProxy;

    private static string GetRuleStatus(ApplicationRule rule, ProxyServer? proxy)
    {
        if (!rule.IsEnabled)
        {
            return "Đang tắt";
        }

        if (proxy is null)
        {
            return "Chưa gắn proxy";
        }

        if (rule.TargetType == ApplicationTargetType.Emulator && rule.ProcessId is null or <= 0)
        {
            return "Chưa có PID runtime";
        }

        return proxy.Status switch
        {
            ProxyStatus.Live => "Đã gắn - proxy hoạt động",
            ProxyStatus.Dead => "Proxy không khả dụng",
            ProxyStatus.Checking => "Đang kiểm tra proxy",
            _ => "Đã gắn - chưa kiểm tra"
        };
    }

    private async Task AddRuleAsync()
    {
        using var form = _serviceProvider.GetRequiredService<AddApplicationRuleForm>();
        form.LoadExistingRules(_rules);
        if (form.ShowDialog(this) != DialogResult.OK || form.Rules.Count == 0)
        {
            return;
        }

        var added = AddNewRules(form.Rules);
        await SaveAsync();
        UiFeedback.ShowInfo(this, $"Đã thêm {added} ứng dụng.");
    }

    private async Task EditSelectedAsync()
    {
        var selected = GetSelectedRules().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        if (selected.Count > 1)
        {
            UiFeedback.ShowInfo(this, "Chỉ có thể sửa tên một file .exe mỗi lần.");
            return;
        }

        if (selected[0].TargetType != ApplicationTargetType.Executable)
        {
            UiFeedback.ShowInfo(this, "Ứng dụng giả lập dùng tên instance và PID runtime; không sửa tên ở form này.");
            return;
        }

        var originalExecutableName = selected[0].ExecutableName;
        using var form = _serviceProvider.GetRequiredService<ApplicationRuleEditorForm>();
        form.LoadRule(selected[0]);

        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (string.Equals(originalExecutableName, selected[0].ExecutableName, StringComparison.Ordinal))
        {
            return;
        }

        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Cập nhật tên ứng dụng", [selected[0].Id]));
        UiFeedback.ShowInfo(this, "Đã cập nhật tên ứng dụng.");
    }

    private async Task DeleteRuleAsync(int rowIndex)
    {
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row)
        {
            return;
        }

        var rule = _rules.FirstOrDefault(item => item.Id == row.Id);
        if (rule is null)
        {
            BindGrid();
            UiFeedback.ShowInfo(this, "Ứng dụng này không còn tồn tại trong danh sách. Hãy chọn lại dòng.");
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa {rule.GetApplicationName()}?",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        _rules.Remove(rule);
        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Xóa ứng dụng", [rule.Id]));
        UiFeedback.ShowInfo(this, "Đã xóa ứng dụng.");
    }

    private async Task DeleteSelectedAsync()
    {
        var selected = GetSelectedRules().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa {selected.Count} ứng dụng đã chọn?",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        foreach (var rule in selected)
        {
            _rules.Remove(rule);
        }

        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Xóa ứng dụng", selected.Select(rule => rule.Id)));
        UiFeedback.ShowInfo(this, $"Đã xóa {selected.Count} ứng dụng.");
    }

    private async Task DeleteAllAsync()
    {
        if (_rules.Count == 0)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa tất cả {_rules.Count} ứng dụng?",
            "Xác nhận xóa tất cả",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        _rules.Clear();
        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.Global("Xóa tất cả ứng dụng"));
        UiFeedback.ShowInfo(this, "Đã xóa tất cả ứng dụng.");
    }

    // private async Task ScanProcessesAsync()
    // {
    //     var processes = await _processScanner.GetRunningProcessesAsync(CancellationToken.None);
    //     using var form = BuildProcessSelectionForm(processes);
    //     if (form.ShowDialog(this) != DialogResult.OK)
    //     {
    //         return;
    //     }

    //     var listBox = form.Controls.OfType<ListBox>().Single();
    //     var selected = listBox.SelectedItems.Cast<RunningProcessInfo>()
    //         .Select(process => process.ExecutableName)
    //         .Distinct(StringComparer.OrdinalIgnoreCase)
    //         .ToList();

    //     foreach (var executable in selected)
    //     {
    //         _rules.Add(new ApplicationRule
    //         {
    //             TargetType = ApplicationTargetType.Executable,
    //             ExecutableName = executable
    //         });
    //     }

    //     await SaveAsync();
    //     UiFeedback.ShowInfo(this, $"Đã thêm {selected.Count} ứng dụng từ danh sách đang chạy.");
    // }

    private async Task RefreshRulesAsync(bool showUserMessages)
    {
        try
        {
            _statusLabel.Text = "Đang làm mới ứng dụng...";
            _rules = (await _applicationRuleService.GetAllAsync(CancellationToken.None)).ToList();
            _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();

            var beforeRefresh = _rules.ToDictionary(rule => rule.Id, RuntimeTargetSnapshot.From);
            await _applicationRuleService.RefreshEmulatorRuntimeTargetsAsync(_rules, CancellationToken.None);
            foreach (var rule in _rules.Where(rule => rule.TargetType == ApplicationTargetType.Emulator))
            {
                rule.Warning = null;
            }

            var refreshedRuntimeCount = _rules.Count(rule =>
                rule.TargetType == ApplicationTargetType.Emulator &&
                beforeRefresh.TryGetValue(rule.Id, out var before) &&
                before != RuntimeTargetSnapshot.From(rule));
            var removedDuplicateCount = RemoveDuplicateEmulatorRules();

            var clearedMissingAssignmentCount = _applicationRuleService.ClearMissingProxyAssignments(_rules, _proxies);
            var clearedExpiredAssignmentCount = _applicationRuleService.ClearExpiredProxyAssignments(
                _rules,
                _proxies,
                DateTimeOffset.Now);
            var disabledInvalidRuleCount = DisableInvalidEnabledRules();
            var clearedInvalidAssignmentCount =
                clearedExpiredAssignmentCount +
                clearedMissingAssignmentCount;

            var hasProfileChanges =
                refreshedRuntimeCount > 0 ||
                removedDuplicateCount > 0 ||
                clearedInvalidAssignmentCount > 0 ||
                disabledInvalidRuleCount > 0;
            if (hasProfileChanges)
            {
                await SaveAsync();
                OnProfileAffectingChanged(ProfileAffectingChange.Global("Làm mới ứng dụng"));
            }
            else
            {
                await RefreshRuntimeStatesAsync();
                BindGrid();
            }

            _statusLabel.Text = BuildRefreshStatus(
                refreshedRuntimeCount,
                removedDuplicateCount,
                clearedInvalidAssignmentCount,
                disabledInvalidRuleCount);
            if (showUserMessages && clearedExpiredAssignmentCount > 0)
            {
                UiFeedback.ShowInfo(this, $"Đã bỏ gán proxy hết hạn của {clearedExpiredAssignmentCount} ứng dụng.");
            }
        }
        catch (Exception ex)
        {
            await LoadGridOnlyAsync();
            _statusLabel.Text = "Không thể làm mới ứng dụng.";
            UiFeedback.ShowWarning(this, ex, "Không thể làm mới ứng dụng");
        }
    }

    private int DisableInvalidEnabledRules()
    {
        var proxiesById = _proxies.ToDictionary(proxy => proxy.Id);
        var disabledCount = 0;
        foreach (var rule in _rules.Where(rule => rule.IsEnabled))
        {
            var eligibility = ApplicationRuleEligibility.Evaluate(rule, proxiesById);
            if (eligibility.CanUseProxy)
            {
                continue;
            }

            rule.IsEnabled = false;
            rule.Warning = eligibility.Message;
            disabledCount++;
        }

        return disabledCount;
    }

    private int RemoveDuplicateEmulatorRules()
    {
        var duplicateRuleIds = _rules
            .Where(rule => rule.TargetType == ApplicationTargetType.Emulator)
            .GroupBy(BuildEmulatorIdentityKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0 && group.Count() > 1)
            .SelectMany(group => group.Skip(1).Select(rule => rule.Id))
            .ToHashSet();
        if (duplicateRuleIds.Count == 0)
        {
            return 0;
        }

        _rules.RemoveAll(rule => duplicateRuleIds.Contains(rule.Id));
        return duplicateRuleIds.Count;
    }

    private static string BuildEmulatorIdentityKey(ApplicationRule rule)
    {
        var instanceKey = rule.EmulatorInstanceKey?.Trim();
        var instanceName = rule.EmulatorInstanceName?.Trim();
        if (string.IsNullOrWhiteSpace(instanceKey) && string.IsNullOrWhiteSpace(instanceName))
        {
            return string.Empty;
        }

        return string.Join(
            "|",
            rule.EmulatorKind?.ToString() ?? string.Empty,
            instanceKey ?? string.Empty,
            instanceName ?? string.Empty,
            rule.RuntimeProcessName?.Trim() ?? string.Empty);
    }

    private static string BuildRefreshStatus(
        int refreshedRuntimeCount,
        int removedDuplicateCount,
        int clearedInvalidAssignmentCount,
        int disabledInvalidRuleCount)
    {
        if (refreshedRuntimeCount == 0 &&
            removedDuplicateCount == 0 &&
            clearedInvalidAssignmentCount == 0 &&
            disabledInvalidRuleCount == 0)
        {
            return "Đã làm mới ứng dụng. Không có thay đổi.";
        }

        var parts = new List<string>();
        if (refreshedRuntimeCount > 0)
        {
            parts.Add($"cập nhật runtime {refreshedRuntimeCount} giả lập");
        }

        if (removedDuplicateCount > 0)
        {
            parts.Add($"xóa {removedDuplicateCount} giả lập trùng");
        }

        if (clearedInvalidAssignmentCount > 0)
        {
            parts.Add($"bỏ gán proxy không còn hợp lệ cho {clearedInvalidAssignmentCount} ứng dụng");
        }

        if (disabledInvalidRuleCount > 0)
        {
            parts.Add($"tắt {disabledInvalidRuleCount} ứng dụng chưa đủ điều kiện dùng proxy");
        }

        return $"Đã làm mới ứng dụng: {string.Join(", ", parts)}.";
    }

    private async Task ChangeProxyAsync()
    {
        var selectedRules = GetSelectedRules().ToList();
        if (selectedRules.Count == 0)
        {
            return;
        }

        await ChangeProxyAsync(selectedRules);
    }

    private async Task AssignProxyForRowAsync(Guid ruleId)
    {
        var rule = _rules.FirstOrDefault(item => item.Id == ruleId);
        if (rule is null)
        {
            BindGrid();
            UiFeedback.ShowInfo(this, "Ứng dụng này không còn tồn tại trong danh sách. Hãy chọn lại dòng.");
            return;
        }

        await ChangeProxyAsync([rule]);
    }

    private async Task ChangeProxyAsync(IReadOnlyList<ApplicationRule> selectedRules)
    {
        _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
        using var form = _serviceProvider.GetRequiredService<ChangeApplicationProxyForm>();
        form.LoginRequestedAsync = LoginRequestedAsync;
        var isAssignMode = selectedRules.All(rule => rule.AssignedProxyId is null);
        form.Text = isAssignMode ? "Gắn Proxy" : "Đổi Proxy";
        form.ProfileAffectingChanged += (_, change) => OnProfileAffectingChanged(change);
        form.LoadContext(_rules, _proxies);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var selectedProxy = form.SelectedManualProxy;
        if (selectedProxy is null)
        {
            if (form.SelectedOrder is not { } selectedOrder)
            {
                return;
            }

            selectedProxy = await _proxyManagementService.UpsertBackendOrderAsync(
                selectedOrder,
                form.SelectedKind,
                form.SelectedProxyAddress,
                CancellationToken.None);
        }

        var selectedProxyEligibility = ValidateAssignableProxy(selectedProxy);
        if (!selectedProxyEligibility.CanUseProxy)
        {
            UiFeedback.ShowWarning(this, selectedProxyEligibility.Message, "Không thể gắn Proxy");
            return;
        }

        UpsertCachedProxy(selectedProxy);

        foreach (var rule in selectedRules)
        {
            rule.AssignedProxyId = selectedProxy.Id;
            rule.Warning = null;
            rule.AutoAssignProxy = false;
        }

        AutoEnableEligibleRules(selectedRules);

        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules(
            isAssignMode ? "Gắn proxy ứng dụng" : "Đổi proxy ứng dụng",
            selectedRules.Select(rule => rule.Id)));
        UiFeedback.ShowInfo(
            this,
            isAssignMode
                ? $"Đã gán proxy cho {selectedRules.Count} ứng dụng."
                : $"Đã đổi proxy cho {selectedRules.Count} ứng dụng.");
    }

    private static ApplicationRuleEligibilityResult ValidateAssignableProxy(ProxyServer proxy)
    {
        var probeRule = new ApplicationRule
        {
            ExecutableName = "proxy-validation.exe",
            AssignedProxyId = proxy.Id
        };

        return ApplicationRuleEligibility.Evaluate(probeRule, [proxy]);
    }

    private void UpsertCachedProxy(ProxyServer proxy)
    {
        var existingIndex = _proxies.FindIndex(item => item.Id == proxy.Id);
        if (existingIndex >= 0)
        {
            _proxies[existingIndex] = proxy;
            return;
        }

        _proxies.Add(proxy);
    }

    private void AutoEnableEligibleRules(IEnumerable<ApplicationRule> rules)
    {
        var proxiesById = _proxies.ToDictionary(proxy => proxy.Id);
        foreach (var rule in rules)
        {
            var eligibility = ApplicationRuleEligibility.Evaluate(rule, proxiesById);
            if (!eligibility.CanUseProxy)
            {
                rule.Warning = eligibility.Message;
                continue;
            }

            rule.IsEnabled = true;
            rule.Warning = null;
        }
    }

    private void ConfigureEnabledCells()
    {
        if (!_grid.Columns.Contains(nameof(RuleRow.IsEnabled)))
        {
            return;
        }

        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is not RuleRow row ||
                gridRow.Cells[nameof(RuleRow.IsEnabled)] is not DataGridViewCell cell)
            {
                continue;
            }

            if (!row.IsEnabled && !row.CanUseProxy)
            {
                cell.ToolTipText = GetEnableBlockedMessage(row);
                cell.Style.ForeColor = LightTheme.Muted;
                cell.Style.SelectionForeColor = LightTheme.Muted;
            }
        }
    }

    private async Task AutoAssignAsync()
    {
        try
        {
            var proxyPool = await BuildAutoAssignableProxyPoolAsync();
            if (proxyPool.Count == 0)
            {
                UiFeedback.ShowInfo(this, "Không có proxy hợp lệ để gắn tự động. Hãy thêm proxy hoặc đăng nhập để lấy proxy backend.");
                return;
            }

            var targetRules = _rules
                .Where(ApplicationRuleService.CanReceiveAutoAssignedProxy)
                .ToList();
            if (targetRules.Count == 0)
            {
                UiFeedback.ShowInfo(this, "Không có ứng dụng nào đủ điều kiện để gắn proxy tự động.");
                return;
            }

            _applicationRuleService.AutoAssignRoundRobinToTargets(targetRules, proxyPool, enableAssignedRules: true);
            var assignedCount = targetRules.Count(rule => rule.AssignedProxyId is not null);
            if (assignedCount == 0)
            {
                UiFeedback.ShowInfo(this, "Chưa gắn được proxy tự động cho ứng dụng nào.");
                return;
            }

            foreach (var rule in targetRules.Where(rule => rule.AssignedProxyId is not null))
            {
                rule.IsEnabled = true;
                rule.AutoAssignProxy = true;
                rule.Warning = null;
            }

            await SaveAsync();
            OnProfileAffectingChanged(ProfileAffectingChange.ForRules(
                "Gắn proxy tự động",
                targetRules.Where(rule => rule.AssignedProxyId is not null).Select(rule => rule.Id)));
            UiFeedback.ShowInfo(this, $"Đã gắn proxy tự động cho {assignedCount} ứng dụng.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể gắn proxy tự động");
        }
    }

    private async Task<IReadOnlyList<ProxyServer>> BuildAutoAssignableProxyPoolAsync()
    {
        _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
        if (await IsAuthenticatedForBackendAutoAssignAsync())
        {
            try
            {
                await UpsertAutoAssignableBackendProxiesAsync();
                _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
            }
            catch
            {
                // Backend orders are an optional extension of the local pool; local/manual proxies remain usable.
            }
        }

        return _proxies
            .Where(IsAutoAssignableProxy)
            .DistinctBy(proxy => proxy.Id)
            .ToList();
    }

    private async Task<bool> IsAuthenticatedForBackendAutoAssignAsync()
    {
        try
        {
            return await _authService.EnsureValidSessionAsync(CancellationToken.None);
        }
        catch
        {
            return false;
        }
    }

    private async Task UpsertAutoAssignableBackendProxiesAsync()
    {
        var backendOrders = await FetchAutoAssignableBackendOrdersAsync();
        foreach (var (kind, order) in backendOrders)
        {
            try
            {
                await _proxyManagementService.UpsertBackendOrderAsync(
                    order,
                    kind,
                    order.ProxyAddress,
                    CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // Skip malformed backend rows; valid rows should still be usable for auto assignment.
            }
        }
    }

    private async Task<IReadOnlyList<(ProxyOrderKind Kind, ProxyOrder Order)>> FetchAutoAssignableBackendOrdersAsync()
    {
        var result = new List<(ProxyOrderKind Kind, ProxyOrder Order)>();
        var now = DateTimeOffset.Now;

        foreach (var kind in BackendProxyOrderSource.AutoAssignKinds)
        {
            for (var pageNumber = 1; ; pageNumber++)
            {
                var page = await _proxyOrderCacheService.GetPageAsync(
                    BackendProxyOrderSource.BuildRequest(kind, pageNumber, BackendOrderRefreshLimit),
                    forceRefresh: true,
                    CancellationToken.None);

                result.AddRange(page.Orders
                    .Where(order => BackendProxyOrderSource.ShouldUseForAutoAssign(kind, order, now))
                    .Select(order => (kind, order)));

                if (!page.HasNextPage)
                {
                    break;
                }
            }
        }

        return result;
    }

    private static bool IsAutoAssignableProxy(ProxyServer proxy) =>
        proxy.HasValidEndpoint &&
        (proxy.ExpiredAt is null || proxy.ExpiredAt.Value > DateTimeOffset.Now);

    private async Task ClearAssignmentAsync()
    {
        var selectedRules = GetSelectedRules().ToList();
        if (selectedRules.Count == 0)
        {
            return;
        }

        foreach (var rule in selectedRules)
        {
            rule.AssignedProxyId = null;
            rule.IsEnabled = false;
            rule.Warning = null;
        }

        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Xóa gắn proxy", selectedRules.Select(rule => rule.Id)));
        UiFeedback.ShowInfo(this, $"Đã xóa gắn proxy của {selectedRules.Count} ứng dụng.");
    }

    private async Task ClearAssignmentForRowAsync(Guid ruleId)
    {
        var rule = _rules.FirstOrDefault(item => item.Id == ruleId);
        if (rule is null)
        {
            BindGrid();
            UiFeedback.ShowInfo(this, "Ứng dụng này không còn tồn tại trong danh sách. Hãy chọn lại dòng.");
            return;
        }

        if (rule.AssignedProxyId is null)
        {
            return;
        }

        rule.AssignedProxyId = null;
        rule.IsEnabled = false;
        rule.Warning = null;
        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Xóa gắn proxy", [rule.Id]));
        UiFeedback.ShowInfo(this, "Đã xóa gắn proxy của ứng dụng.");
    }

    private async Task HandleGridCellValueChangedAsync(DataGridViewCellEventArgs e)
    {
        if (_isBinding ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(RuleRow.ProtocolValue))
        {
            return;
        }

        await ChangeRuleProtocolAsync(e.RowIndex);
    }

    private void HandleEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (e.Control is ComboBox comboBox)
        {
            BeginInvoke(new Action(() => comboBox.DroppedDown = true));
        }
    }

    private void HandleCellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            _grid.Rows[e.RowIndex].DataBoundItem is not RuleRow row)
        {
            return;
        }

        var column = _grid.Columns[e.ColumnIndex];
        if (column.DataPropertyName == nameof(RuleRow.IsEnabled) &&
            !row.IsEnabled &&
            !row.CanUseProxy)
        {
            e.ToolTipText = GetEnableBlockedMessage(row);
            return;
        }

        if (column.DataPropertyName == nameof(RuleRow.AssignedProxy))
        {
            e.ToolTipText = row.AssignedProxyId is null
                ? "Gắn proxy cho ứng dụng này."
                : "Xem hoặc gỡ proxy đã gắn.";
            return;
        }

        if (column.DataPropertyName == nameof(RuleRow.ApplicationPath))
        {
            e.ToolTipText = string.IsNullOrWhiteSpace(row.ApplicationIconPath)
                ? string.Empty
                : row.ApplicationIconPath;
            return;
        }

        if (column.DataPropertyName == nameof(RuleRow.Warning) &&
            !string.IsNullOrWhiteSpace(row.WarningDetail))
        {
            e.ToolTipText = row.WarningDetail;
            return;
        }

        if (column.Name == ActionsColumnName)
        {
            e.ToolTipText = row.CanRotateProxy
                ? "Xóa hoặc xoay proxy cho ứng dụng này."
                : "Xóa ứng dụng này.";
        }
    }

    private async Task ToggleRuleEnabledAsync(int rowIndex)
    {
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row)
        {
            return;
        }

        var ruleId = row.Id;
        var nextValue = !row.IsEnabled;
        var shouldRefreshRuntime = row.TargetTypeValue == ApplicationTargetType.Emulator;
        if (shouldRefreshRuntime)
        {
            _statusLabel.Text = "Đang quét lại ứng dụng giả lập...";
            await RefreshRulesAsync(showUserMessages: false);
        }

        var rule = _rules.FirstOrDefault(item => item.Id == ruleId);
        if (rule is null)
        {
            BindGrid();
            UiFeedback.ShowInfo(this, "Ứng dụng này không còn tồn tại trong danh sách. Hãy chọn lại dòng.");
            return;
        }

        var clearedMissingAssignmentCount = 0;
        if (nextValue)
        {
            _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
            clearedMissingAssignmentCount = _applicationRuleService.ClearMissingProxyAssignments(_rules, _proxies);
            clearedMissingAssignmentCount += _applicationRuleService.ClearExpiredProxyAssignments(
                _rules,
                _proxies,
                DateTimeOffset.Now);
            var eligibility = ApplicationRuleEligibility.Evaluate(rule, _proxies.ToDictionary(proxy => proxy.Id));
            if (!eligibility.CanUseProxy)
            {
                if (clearedMissingAssignmentCount > 0)
                {
                    await _applicationRuleService.SaveAllAsync(_rules, CancellationToken.None);
                    OnProfileAffectingChanged(ProfileAffectingChange.Global("Bỏ gán proxy không còn tồn tại"));
                }

                UiFeedback.ShowInfo(this, eligibility.Message, "Không thể bật Dùng Proxy");
                BindGrid();
                return;
            }
        }

        var stateChanged = rule.IsEnabled != nextValue;
        rule.IsEnabled = nextValue;
        rule.Warning = null;
        await _applicationRuleService.SaveAllAsync(_rules, CancellationToken.None);
        BindGrid();
        if (stateChanged || clearedMissingAssignmentCount > 0)
        {
            OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Cập nhật trạng thái dùng proxy", [rule.Id]));
        }
    }

    private async Task ShowEnableBlockedWarningAsync(RuleRow row)
    {
        var message = GetEnableBlockedMessage(row);
        var rule = _rules.FirstOrDefault(item => item.Id == row.Id);
        if (rule is not null)
        {
            rule.Warning = message;
            await _applicationRuleService.SaveAllAsync(_rules, CancellationToken.None);
        }

        _statusLabel.Text = message;
        BindGrid();
        UiFeedback.ShowWarning(this, message, "Không thể bật Dùng Proxy");
    }

    private async Task ToggleAllRuleEnabledAsync()
    {
        if (_rules.Count == 0)
        {
            return;
        }

        var changed = false;
        var skipped = 0;
        var clearedInvalidAssignmentCount = 0;
        _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
        clearedInvalidAssignmentCount += _applicationRuleService.ClearMissingProxyAssignments(_rules, _proxies);
        clearedInvalidAssignmentCount += _applicationRuleService.ClearExpiredProxyAssignments(
            _rules,
            _proxies,
            DateTimeOffset.Now);
        var proxiesById = _proxies.ToDictionary(proxy => proxy.Id);
        var toggleableRules = _rules
            .Where(rule => ApplicationRuleEligibility.Evaluate(rule, proxiesById).CanUseProxy)
            .ToList();
        if (toggleableRules.Count == 0)
        {
            if (clearedInvalidAssignmentCount > 0)
            {
                await _applicationRuleService.SaveAllAsync(_rules, CancellationToken.None);
                OnProfileAffectingChanged(ProfileAffectingChange.Global("Bỏ gán proxy không còn tồn tại"));
            }

            BindGrid();
            _statusLabel.Text = "Hãy gắn Proxy trước khi bật Dùng Proxy.";
            return;
        }

        var nextValue = !toggleableRules.Any(rule => rule.IsEnabled);

        if (nextValue)
        {
            skipped = _rules.Count - toggleableRules.Count;
            foreach (var rule in toggleableRules)
            {
                if (!rule.IsEnabled)
                {
                    changed = true;
                }

                rule.IsEnabled = true;
                rule.Warning = null;
            }
        }
        else
        {
            foreach (var rule in toggleableRules)
            {
                if (rule.IsEnabled)
                {
                    changed = true;
                }

                rule.IsEnabled = false;
                rule.Warning = null;
            }
        }

        if (!changed && clearedInvalidAssignmentCount == 0)
        {
            BindGrid();
            return;
        }

        await _applicationRuleService.SaveAllAsync(_rules, CancellationToken.None);
        BindGrid();
        OnProfileAffectingChanged(ProfileAffectingChange.Global(nextValue ? "Bật Dùng Proxy tất cả ứng dụng" : "Tắt Dùng Proxy tất cả ứng dụng"));
        if (skipped > 0)
        {
            UiFeedback.ShowInfo(this, $"Đã bỏ qua {skipped} ứng dụng chưa đủ điều kiện dùng proxy.");
        }
    }

    private CheckBoxState GetEnabledHeaderCheckBoxState()
    {
        var toggleableRules = GetToggleableProxyRules();
        if (toggleableRules.Count > 0 && toggleableRules.All(rule => rule.IsEnabled))
        {
            return CheckBoxState.CheckedNormal;
        }

        return toggleableRules.Any(rule => rule.IsEnabled)
            ? CheckBoxState.MixedNormal
            : CheckBoxState.UncheckedNormal;
    }

    private List<ApplicationRule> GetToggleableProxyRules()
    {
        var proxiesById = _proxies.ToDictionary(proxy => proxy.Id);
        return _rules
            .Where(rule => ApplicationRuleEligibility.Evaluate(rule, proxiesById).CanUseProxy)
            .ToList();
    }

    private async Task StartApplicationAsync(int rowIndex)
    {
        if (!TryGetRuleForRow(rowIndex, out var rule))
        {
            return;
        }

        if (rule.TargetType != ApplicationTargetType.Executable)
        {
            UiFeedback.ShowInfo(this, "Chưa hỗ trợ tắt/mở emulator trong phiên bản này.");
            return;
        }

        var result = await _applicationRuntimeController.StartAsync(rule, CancellationToken.None);
        _statusLabel.Text = result.Message;
        await RefreshRuntimeStateAsync(rule);
        BindGrid();
        if (!result.Success)
        {
            UiFeedback.ShowInfo(this, result.Message, "Không thể mở ứng dụng");
        }
    }

    private async Task StopApplicationAsync(int rowIndex)
    {
        if (!TryGetRuleForRow(rowIndex, out var rule))
        {
            return;
        }

        if (rule.TargetType != ApplicationTargetType.Executable)
        {
            UiFeedback.ShowInfo(this, "Chưa hỗ trợ tắt/mở emulator trong phiên bản này.");
            return;
        }

        var result = await _applicationRuntimeController.StopAsync(rule, CancellationToken.None);
        _statusLabel.Text = result.Message;
        await RefreshRuntimeStateAsync(rule);
        BindGrid();
    }

    private bool TryGetRuleForRow(int rowIndex, out ApplicationRule rule)
    {
        rule = null!;
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row)
        {
            return false;
        }

        rule = _rules.FirstOrDefault(item => item.Id == row.Id)!;
        if (rule is not null)
        {
            return true;
        }

        BindGrid();
        UiFeedback.ShowInfo(this, "Ứng dụng này không còn tồn tại trong danh sách. Hãy chọn lại dòng.");
        return false;
    }

    private async Task ChangeRuleProtocolAsync(int rowIndex)
    {
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row ||
            !row.CanEditProtocol ||
            row.BackendUserProxyId is not { } backendUserProxyId ||
            row.AssignedProxyId is not { } proxyId)
        {
            return;
        }

        var proxy = _proxies.FirstOrDefault(item => item.Id == proxyId);
        if (proxy is null ||
            !Enum.TryParse<ProxyProtocol>(row.ProtocolValue, out var protocol) ||
            proxy.Protocol == protocol)
        {
            BindGrid();
            return;
        }

        try
        {
            _statusLabel.Text = "Đang cập nhật giao thức proxy...";
            await _proxyOrderApiClient.ChangeProxyInfoAsync(
                [backendUserProxyId],
                proxy.Username ?? string.Empty,
                proxy.Password ?? string.Empty,
                protocol,
                CancellationToken.None);

            proxy.Protocol = protocol;
            await _proxyManagementService.SaveAllAsync(_proxies, CancellationToken.None);
            _proxyOrderCacheService.UpdateOrderInfo(
                backendUserProxyId,
                proxy.Username ?? string.Empty,
                proxy.Password ?? string.Empty,
                protocol);
            _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
            BindGrid();
            OnProfileAffectingChanged(ProfileAffectingChange.ForProxies("Cập nhật giao thức proxy ứng dụng", [proxyId]));
        }
        catch (Exception ex)
        {
            BindGrid();
            UiFeedback.ShowWarning(this, ex, "Không thể cập nhật giao thức proxy");
        }
    }

    private async Task RotateRuleProxyAsync(int rowIndex)
    {
        if (rowIndex < 0 ||
            rowIndex >= _grid.Rows.Count ||
            _grid.Rows[rowIndex].DataBoundItem is not RuleRow row ||
            !row.CanRotateProxy ||
            row.BackendUserProxyId is not { } backendUserProxyId)
        {
            return;
        }

        if (_isRotatingRuleProxy || !TryBeginManualRotate())
        {
            return;
        }

        var rotateSucceeded = false;
        try
        {
            _isRotatingRuleProxy = true;
            _statusLabel.Text = "Đang xoay proxy...";
            await _proxyOrderApiClient.RotateProxyAsync(backendUserProxyId, checkOnly: false, CancellationToken.None);
            rotateSucceeded = true;
            _proxyOrderCacheService.InvalidateCategory(2, false);
            await RefreshRulesAsync(showUserMessages: false);
            OnProfileAffectingChanged(row.AssignedProxyId is { } assignedProxyId
                ? ProfileAffectingChange.ForProxies("Xoay proxy ứng dụng", [assignedProxyId])
                : ProfileAffectingChange.ForRules("Xoay proxy ứng dụng", [row.Id]));
            UiFeedback.ShowInfo(this, "Đã gửi lệnh xoay proxy.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể xoay proxy");
        }
        finally
        {
            _manualProxyRotateGuard.Finish(rotateSucceeded);
            _isRotatingRuleProxy = false;
        }
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

    private async Task SaveAsync()
    {
        await _applicationRuleService.SaveAllAsync(_rules, CancellationToken.None);
        _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
        await RefreshRuntimeStatesAsync();
        BindGrid();
    }

    private void OnProfileAffectingChanged(ProfileAffectingChange change) =>
        ProfileAffectingChanged?.Invoke(this, change);

    private int AddNewRules(IEnumerable<ApplicationRule> rules)
    {
        var added = 0;
        foreach (var rule in rules)
        {
            if (IsDuplicateRule(rule))
            {
                continue;
            }

            rule.IsEnabled = false;
            _rules.Add(rule);
            added++;
        }

        return added;
    }

    private bool IsDuplicateRule(ApplicationRule candidate)
    {
        return IsDuplicateRuleExcluding(candidate, excludedRuleId: null);
    }

    private bool IsDuplicateRuleExcluding(ApplicationRule candidate, Guid? excludedRuleId)
    {
        if (candidate.TargetType == ApplicationTargetType.Emulator)
        {
            return _rules.Any(rule =>
                rule.Id != excludedRuleId &&
                rule.TargetType == ApplicationTargetType.Emulator &&
                rule.EmulatorKind == candidate.EmulatorKind &&
                string.Equals(rule.EmulatorInstanceKey, candidate.EmulatorInstanceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rule.RuntimeProcessName, candidate.RuntimeProcessName, StringComparison.OrdinalIgnoreCase));
        }

        return _rules.Any(rule =>
            rule.Id != excludedRuleId &&
            rule.TargetType == ApplicationTargetType.Executable &&
            string.Equals(rule.ExecutableName, candidate.ExecutableName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRuleWarning(ApplicationRule rule)
    {
        if (rule.TargetType == ApplicationTargetType.Emulator && rule.ProcessId is null or <= 0)
        {
            return "Hãy mở giả lập";
        }

        return string.Empty;
    }

    private static string GetEnableBlockedMessage(RuleRow row) =>
        row.AssignedProxyId is null
            ? "Hãy gắn Proxy"
            : string.IsNullOrWhiteSpace(row.IneligibleReason)
                ? "Hãy gắn Proxy"
                : row.IneligibleReason;

    private static string FormatWarningDisplay(string warning, ApplicationRule rule)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return string.Empty;
        }

        if (warning.Contains("Trùng", StringComparison.OrdinalIgnoreCase))
        {
            return "Trùng tên .exe";
        }

        if (warning.Contains("proxy", StringComparison.OrdinalIgnoreCase) &&
            warning.Contains("gán", StringComparison.OrdinalIgnoreCase))
        {
            return "Chưa gán proxy";
        }

        if (warning.Contains("hết hạn", StringComparison.OrdinalIgnoreCase))
        {
            return "Proxy hết hạn";
        }

        if (rule.TargetType == ApplicationTargetType.Emulator &&
            (warning.Contains("giả lập", StringComparison.OrdinalIgnoreCase) ||
             warning.Contains("PID", StringComparison.OrdinalIgnoreCase)))
        {
            return "Chưa mở giả lập";
        }

        return warning.Length <= 34
            ? warning
            : $"{warning[..31]}...";
    }

    private IEnumerable<ApplicationRule> GetSelectedRules()
    {
        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            if (row.DataBoundItem is RuleRow ruleRow)
            {
                var rule = _rules.FirstOrDefault(item => item.Id == ruleRow.Id);
                if (rule is not null)
                {
                    yield return rule;
                }
            }
        }
    }

    private void MoveToFirstPageContainingProxy(Guid proxyId)
    {
        var filteredRows = BuildRuleRows().Where(MatchesFilters).ToList();
        var matchIndex = filteredRows.FindIndex(row => row.AssignedProxyId == proxyId);
        if (matchIndex < 0)
        {
            var allRows = BuildRuleRows();
            matchIndex = allRows.FindIndex(row => row.AssignedProxyId == proxyId);
            if (matchIndex < 0)
            {
                return;
            }

            ClearFilters();
        }

        _page = (matchIndex / Math.Max(1, _limit)) + 1;
        BindGrid();
    }

    private void SelectApplicationsUsingProxy(Guid proxyId)
    {
        _grid.ClearSelection();
        DataGridViewCell? firstCell = null;
        var selectedCount = 0;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.DataBoundItem is not RuleRow ruleRow || ruleRow.AssignedProxyId != proxyId)
            {
                continue;
            }

            row.Selected = true;
            selectedCount++;
            firstCell ??= row.Cells
                .Cast<DataGridViewCell>()
                .FirstOrDefault(cell => cell.Visible);
        }

        if (firstCell is not null)
        {
            _grid.CurrentCell = firstCell;
            ScrollToCellWhenReady(firstCell, remainingAttempts: 5);
        }

        _statusLabel.Text = selectedCount == 0
            ? "Không có ứng dụng nào đang dùng proxy này."
            : $"Đã chọn {selectedCount} ứng dụng đang dùng proxy này.";
    }

    private void ScrollToCellWhenReady(DataGridViewCell cell, int remainingAttempts)
    {
        if (cell.RowIndex < 0 || cell.RowIndex >= _grid.Rows.Count)
        {
            return;
        }

        if (!_grid.IsHandleCreated || !_grid.Visible)
        {
            RetryScrollToCell(cell, remainingAttempts);
            return;
        }

        try
        {
            _grid.FirstDisplayedScrollingRowIndex = cell.RowIndex;
        }
        catch (InvalidOperationException)
        {
            RetryScrollToCell(cell, remainingAttempts);
        }
    }

    private void RetryScrollToCell(DataGridViewCell cell, int remainingAttempts)
    {
        if (remainingAttempts <= 0 || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() => ScrollToCellWhenReady(cell, remainingAttempts - 1)));
    }

    // private static Form BuildProcessSelectionForm(IReadOnlyList<RunningProcessInfo> processes)
    // {
    //     var form = new Form
    //     {
    //         Text = "Quét ứng dụng đang chạy",
    //         Width = 620,
    //         Height = 500,
    //         MinimumSize = new Size(520, 420),
    //         StartPosition = FormStartPosition.CenterParent
    //     };

    //     var list = new ListBox
    //     {
    //         Dock = DockStyle.Fill,
    //         SelectionMode = SelectionMode.MultiExtended,
    //         DisplayMember = nameof(RunningProcessInfo.ExecutableName),
    //         DataSource = processes.ToList()
    //     };

    //     var buttons = new FlowLayoutPanel
    //     {
    //         Dock = DockStyle.Bottom,
    //         Height = 56,
    //         FlowDirection = FlowDirection.RightToLeft,
    //         Padding = new Padding(8)
    //     };
    //     var cancel = LightTheme.CreateToolbarButton("Hủy", (_, _) => form.DialogResult = DialogResult.Cancel, LightTheme.Muted);
    //     var add = LightTheme.CreateToolbarButton("Thêm đã chọn", (_, _) => form.DialogResult = DialogResult.OK, LightTheme.Success);
    //     buttons.Controls.Add(cancel);
    //     buttons.Controls.Add(add);

    //     form.Controls.Add(list);
    //     form.Controls.Add(buttons);
    //     LightTheme.Apply(form);
    //     return form;
    // }

    private static Form BuildProxySelectionForm(
        IReadOnlyList<ProxyServer> proxies,
        IReadOnlyList<ApplicationRule> rules)
    {
        var form = new Form
        {
            Text = "Đổi proxy",
            Width = 760,
            Height = 460,
            MinimumSize = new Size(680, 400),
            StartPosition = FormStartPosition.CenterParent
        };
        var usageCounts = rules
            .Where(rule => rule.AssignedProxyId is not null)
            .GroupBy(rule => rule.AssignedProxyId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var grid = new AppDataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        grid.Columns.Add(CreateSelectionColumn("Proxy", nameof(ProxyUsageRow.Proxy), 180, 22));
        grid.Columns.Add(CreateSelectionColumn("Giao thức", nameof(ProxyUsageRow.Protocol), 110, 12));
        grid.Columns.Add(CreateSelectionColumn("Trạng thái", nameof(ProxyUsageRow.Status), 120, 14));
        grid.Columns.Add(CreateSelectionColumn("Đang dùng bởi", nameof(ProxyUsageRow.UsageCount), 110, 12));
        grid.DataSource = proxies.Select(proxy => new ProxyUsageRow(
            proxy.Id,
            proxy.DisplayValue,
            proxy.Protocol.ToDisplayName(),
            proxy.Status.ToString(),
            usageCounts.GetValueOrDefault(proxy.Id))).ToList();
        grid.CellDoubleClick += (_, _) => form.DialogResult = DialogResult.OK;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var cancel = LightTheme.CreateToolbarButton("Hủy", (_, _) => form.DialogResult = DialogResult.Cancel, LightTheme.Muted);
        var assign = LightTheme.CreateToolbarButton("Chọn proxy này", (_, _) => form.DialogResult = DialogResult.OK, LightTheme.Accent);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(assign);

        form.Controls.Add(grid);
        form.Controls.Add(buttons);
        LightTheme.Apply(form);
        return form;
    }

    private static DataGridViewTextBoxColumn CreateSelectionColumn(string header, string propertyName, int minimumWidth, float fillWeight)
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

    private sealed record RuntimeTargetSnapshot(
        int? ProcessId,
        string? EmulatorInstanceKey,
        string? EmulatorInstanceName,
        string? RuntimeProcessName,
        string? RuntimeExecutablePath)
    {
        public static RuntimeTargetSnapshot From(ApplicationRule rule) =>
            new(
                rule.ProcessId,
                rule.EmulatorInstanceKey,
                rule.EmulatorInstanceName,
                rule.RuntimeProcessName,
                rule.RuntimeExecutablePath);
    }

    private sealed class RuleRow
    {
        public RuleRow(
            Guid id,
            string applicationName,
            string applicationPath,
            string applicationIconPath,
            ApplicationTargetType targetTypeValue,
            string targetType,
            string processId,
            string processName,
            string assignedProxy,
            Guid? assignedProxyId,
            string proxyType,
            string provider,
            string protocolValue,
            bool canEditProtocol,
            bool canRotateProxy,
            int? backendUserProxyId,
            bool canUseProxy,
            string ineligibleReason,
            string status,
            bool isEnabled,
            bool isRuntimeRunning,
            string warning,
            string warningDetail)
        {
            Id = id;
            ApplicationName = applicationName;
            ApplicationPath = applicationPath;
            ApplicationIconPath = applicationIconPath;
            TargetTypeValue = targetTypeValue;
            TargetType = targetType;
            ProcessId = processId;
            ProcessName = processName;
            AssignedProxy = assignedProxy;
            AssignedProxyId = assignedProxyId;
            ProxyType = proxyType;
            Provider = provider;
            ProtocolValue = protocolValue;
            CanEditProtocol = canEditProtocol;
            CanRotateProxy = canRotateProxy;
            BackendUserProxyId = backendUserProxyId;
            CanUseProxy = canUseProxy;
            IneligibleReason = ineligibleReason;
            Status = status;
            IsEnabled = isEnabled;
            IsRuntimeRunning = isRuntimeRunning;
            Warning = warning;
            WarningDetail = warningDetail;
        }

        public Guid Id { get; }
        public string ApplicationName { get; }
        public string ApplicationPath { get; }
        public string ApplicationIconPath { get; }
        public ApplicationTargetType TargetTypeValue { get; }
        public string TargetType { get; }
        public string ProcessId { get; }
        public string ProcessName { get; }
        public string AssignedProxy { get; }
        public Guid? AssignedProxyId { get; }
        public string ProxyType { get; }
        public string Provider { get; }
        public string ProtocolValue { get; set; }
        public bool CanEditProtocol { get; }
        public bool CanRotateProxy { get; }
        public int? BackendUserProxyId { get; }
        public bool CanUseProxy { get; }
        public string IneligibleReason { get; }
        public string Status { get; }
        public bool IsEnabled { get; set; }
        public bool IsRuntimeRunning { get; }
        public string Warning { get; }
        public string WarningDetail { get; }
        public string Actions => string.Empty;
    }

    private sealed record ProtocolCellOption(string Value, string DisplayName);

    private sealed record ColumnVisibilityOption(string ColumnName, string HeaderText, bool IsVisible)
    {
        public override string ToString() => $"{(IsVisible ? "✓" : " ")} {HeaderText}";
    }

    private sealed record ProxyUsageRow(
        Guid Id,
        string Proxy,
        string Protocol,
        string Status,
        int UsageCount);
}
