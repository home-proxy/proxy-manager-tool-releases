using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.App.Models;
using NetAgent.ProxyManager.App.Controls;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Services;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;
using NetAgent.ProxyManager.Infrastructure.Api;
using NetAgent.ProxyManager.Infrastructure.Storage;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class MainForm : Form
{
    private const int SidebarExpandedWidth = 348;
    private const int SidebarCollapsedWidth = 64;
    private const int SidebarPanelHorizontalPadding = 12;
    private const int SidebarPanelTopPadding = 36;
    private const int SidebarPanelBottomPadding = 24;
    private const int SidebarExpandedButtonWidth = SidebarExpandedWidth - (SidebarPanelHorizontalPadding * 2);
    private const int SidebarCollapsedButtonWidth = SidebarCollapsedWidth - (SidebarPanelHorizontalPadding * 2);
    private const int SidebarButtonHeight = 54;
    private const int SidebarToggleExpandedWidth = 52;
    private const int SidebarToggleHeight = 40;
    private const int SidebarSeparatorHorizontalMargin = 12;
    private const int SidebarSeparatorWidth = SidebarExpandedButtonWidth - (SidebarSeparatorHorizontalMargin * 2);
    private const int SidebarButtonVerticalGap = 8;
    private const float SidebarButtonFontSize = 11f;
    private const int SidebarButtonIconSize = 20;
    private static readonly Color SidebarBackground = Color.FromArgb(243, 243, 243);
    private static readonly Color SidebarBorderColor = Color.FromArgb(224, 224, 224);
    private static readonly Color SidebarMenuHoverColor = Color.FromArgb(234, 234, 234);
    private static readonly Color SidebarMenuPressedColor = Color.FromArgb(224, 224, 224);
    private static readonly Color SidebarSelectedTextColor = Color.FromArgb(23, 23, 23);
    private static readonly Color SidebarDefaultTextColor = Color.FromArgb(82, 82, 82);
    private static readonly Color SidebarDefaultIconColor = Color.FromArgb(66, 66, 66);
    private static readonly Color HeaderAccent = Color.FromArgb(15, 108, 189);
    private static readonly Color HeaderAccentSoft = Color.FromArgb(235, 243, 252);
    private static readonly Color HeaderBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color HeaderText = Color.FromArgb(23, 23, 23);
    private static readonly Color HeaderButtonBorder = Color.FromArgb(209, 209, 209);
    private static readonly Color SettingsCardBackground = Color.FromArgb(250, 250, 250);
    private static readonly Color SettingsDivider = Color.FromArgb(224, 224, 224);
    private const int SettingsCardsHeight = 470;
    private static readonly Regex SettingsEmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SettingsWhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ApplicationRulesControl _applicationRulesControl;
    private readonly StatusBarControl _statusBarControl;
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly IProxyRepository _proxyRepository;
    private readonly IApplicationRuleRepository _applicationRuleRepository;
    private readonly ApplicationRuleService _applicationRuleService;
    private readonly IProxifierProfileBuilder _profileBuilder;
    private readonly IProxifierService _proxifierService;
    private readonly IProxifierInstallerService _proxifierInstallerService;
    private readonly IProxifierRegistrationService _proxifierRegistrationService;
    private readonly IProxifierPreferenceService _proxifierPreferenceService;
    private readonly IProxifierSessionService _proxifierSessionService;
    private readonly IApplicationRuntimeController _applicationRuntimeController;
    private readonly IAuthService _authService;
    private readonly IDepositApiClient _depositApiClient;
    private readonly IProxyOrderCacheService _proxyOrderCacheService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppDataPaths _paths;
    private readonly string _portalBaseUrl;

    private readonly Panel _contentPanel = new();
    private readonly List<Button> _sidebarButtons = [];
    private readonly Dictionary<Button, string> _sidebarButtonIconNames = [];
    private readonly Dictionary<Button, string> _sidebarButtonTexts = [];
    private readonly List<Control> _expandedOnlySidebarControls = [];
    private readonly ToolTip _sidebarToolTip = new() { ShowAlways = true };
    private TableLayoutPanel _shell = null!;
    private ColumnStyle _sidebarColumnStyle = null!;
    private TableLayoutPanel _sidebarRootPanel = null!;
    private FlowLayoutPanel _sidebarPanel = null!;
    private Panel _sidebarLogoHost = null!;
    private PictureBox _sidebarLogoPictureBox = null!;
    private Label _sidebarLogoTextLabel = null!;
    private Button _sidebarToggleButton = null!;
    private Image? _largeSidebarLogoImage;
    private Image? _smallSidebarLogoImage;
    private bool _largeSidebarLogoResolved;
    private bool _smallSidebarLogoResolved;
    private ProxyOrdersControl _staticProxyOrdersControl = null!;
    private ProxyOrdersControl _datacenterProxyOrdersControl = null!;
    private ProxyOrdersControl _rotatingProxyOrdersControl = null!;
    private ProductPurchaseControl _purchaseControl = null!;
    private ProxyListControl _myProxyListControl = null!;
    private Button _purchaseSidebarButton = null!;
    private Button _staticProxySidebarButton = null!;
    private Button _datacenterProxySidebarButton = null!;
    private Button _rotatingProxySidebarButton = null!;
    private Button _myProxySidebarButton = null!;
    private Button _applicationsSidebarButton = null!;
    private Button? _selectedSidebarButton;
    private readonly AppTextInput _proxifierPathTextBox = new();
    private readonly AppTextInput _lastProfileTextBox = new();
    private readonly AppTextInput _registrationKeyTextBox = new();
    private readonly AppTextInput _accountDisplayNameInput = new();
    private readonly AppTextInput _accountEmailInput = new();
    private readonly AppTextInput _oldPasswordInput = new();
    private readonly AppTextInput _newPasswordInput = new();
    private readonly AppTextInput _confirmPasswordInput = new();
    private readonly CheckBox _autoRestartApplicationsCheckBox = new()
    {
        Text = "Tự động restart các ứng dụng đang chạy khi dùng Proxy",
        AutoSize = true
    };
    private readonly RichTextBox _profilePreviewTextBox = new();
    private readonly Label _commandResultLabel = new();
    private readonly Label _runtimeStateLabel = new();
    private readonly Label _accountDisplayNameValueLabel = new();
    private readonly Label _accountEmailValueLabel = new();
    private readonly Label _accountPhoneValueLabel = new();
    private readonly Label _accountJoinedValueLabel = new();
    private readonly Label _accountMessageLabel = new();
    private readonly Label _passwordMessageLabel = new();
    private readonly Label _headerTitleLabel = new();
    private readonly Label _accountLabel = new();
    private readonly ToolTip _settingsToolTip = new() { ShowAlways = true };
    private Button _balanceButton = null!;
    private Button _loginButton = null!;
    private Button _sessionActionButton = null!;
    private SessionProxyActionControl _sessionActionControl = null!;
    private Button _logoutButton = null!;
    private AuthUser? _currentAuthUser;
    private ProxifierRuntimeState _runtimeState = ProxifierRuntimeState.Stopped;
    private ProxyRestartConfirmationPreference _proxyRestartConfirmationPreference =
        ProxyRestartConfirmationPreference.Ask;
    private bool _isUpdatingProxyRestartPreferenceUi;
    private bool _isSidebarCollapsed;
    private bool _hasStartableProxyRule;
    private bool _exitCleanupCompleted;
    private bool _exitCleanupRunning;
    private readonly ProfileAutoReloadCoordinator _profileAutoReloadCoordinator;

    public MainForm(
        ApplicationRulesControl applicationRulesControl,
        StatusBarControl statusBarControl,
        IAppSettingsRepository settingsRepository,
        IProxyRepository proxyRepository,
        IApplicationRuleRepository applicationRuleRepository,
        ApplicationRuleService applicationRuleService,
        IProxifierProfileBuilder profileBuilder,
        IProxifierService proxifierService,
        IProxifierInstallerService proxifierInstallerService,
        IProxifierRegistrationService proxifierRegistrationService,
        IProxifierPreferenceService proxifierPreferenceService,
        IProxifierSessionService proxifierSessionService,
        IApplicationRuntimeController applicationRuntimeController,
        IAuthService authService,
        IDepositApiClient depositApiClient,
        IProxyOrderCacheService proxyOrderCacheService,
        IHostEnvironment hostEnvironment,
        IServiceProvider serviceProvider,
        AppDataPaths paths,
        IOptions<BackendApiOptions> backendApiOptions)
    {
        _applicationRulesControl = applicationRulesControl;
        _statusBarControl = statusBarControl;
        _settingsRepository = settingsRepository;
        _proxyRepository = proxyRepository;
        _applicationRuleRepository = applicationRuleRepository;
        _applicationRuleService = applicationRuleService;
        _profileBuilder = profileBuilder;
        _proxifierService = proxifierService;
        _proxifierInstallerService = proxifierInstallerService;
        _proxifierRegistrationService = proxifierRegistrationService;
        _proxifierPreferenceService = proxifierPreferenceService;
        _proxifierSessionService = proxifierSessionService;
        _applicationRuntimeController = applicationRuntimeController;
        _authService = authService;
        _depositApiClient = depositApiClient;
        _proxyOrderCacheService = proxyOrderCacheService;
        _hostEnvironment = hostEnvironment;
        _serviceProvider = serviceProvider;
        _paths = paths;
        _portalBaseUrl = NormalizePortalBaseUrl(backendApiOptions.Value.PortalBaseUrl);
        _profileAutoReloadCoordinator = new ProfileAutoReloadCoordinator(
            () => _runtimeState == ProxifierRuntimeState.Running,
            async (change, _) => await ReloadActiveProfileOnceAsync(change));

        Text = AppBranding.GetMainWindowTitle();
        Width = 1480;
        Height = 900;
        MinimumSize = new Size(1700, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = AppBranding.LoadApplicationIcon() ?? Icon;

        BuildUi();
        Shown += async (_, _) => await InitializeAsync();
        FormClosing += HandleFormClosing;
        LightTheme.Apply(this);
        ApplySidebarLayoutState();
        RefreshSidebarButtons();
    }

    private void BuildUi()
    {
        BuildSidebarUi();
    }

    private void BuildSidebarUi()
    {
        _staticProxyOrdersControl = _serviceProvider.GetRequiredService<ProxyOrdersControl>();
        _staticProxyOrdersControl.Configure(ProxyOrderKind.Static);
        _datacenterProxyOrdersControl = _serviceProvider.GetRequiredService<ProxyOrdersControl>();
        _datacenterProxyOrdersControl.Configure(ProxyOrderKind.Datacenter);
        _rotatingProxyOrdersControl = _serviceProvider.GetRequiredService<ProxyOrdersControl>();
        _rotatingProxyOrdersControl.Configure(ProxyOrderKind.RotateProxy);
        _purchaseControl = _serviceProvider.GetRequiredService<ProductPurchaseControl>();
        _purchaseControl.Configure(ProxyProductKind.Static);
        _myProxyListControl = _serviceProvider.GetRequiredService<ProxyListControl>();
        _staticProxyOrdersControl.LoginRequestedAsync = ShowLoginModalAsync;
        _datacenterProxyOrdersControl.LoginRequestedAsync = ShowLoginModalAsync;
        _rotatingProxyOrdersControl.LoginRequestedAsync = ShowLoginModalAsync;
        _purchaseControl.LoginRequestedAsync = ShowLoginModalAsync;
        _purchaseControl.DepositRequestedAsync = OpenDepositModalAsync;
        _applicationRulesControl.LoginRequestedAsync = ShowLoginModalAsync;
        _applicationRulesControl.ProfileAffectingChanged += HandleProfileAffectingChanged;
        _myProxyListControl.ProfileAffectingChanged += HandleProfileAffectingChanged;
        _staticProxyOrdersControl.ProfileAffectingChanged += HandleProfileAffectingChanged;
        _datacenterProxyOrdersControl.ProfileAffectingChanged += HandleProfileAffectingChanged;
        _rotatingProxyOrdersControl.ProfileAffectingChanged += HandleProfileAffectingChanged;
        _staticProxyOrdersControl.ViewApplicationsRequested += HandleViewApplicationsRequested;
        _datacenterProxyOrdersControl.ViewApplicationsRequested += HandleViewApplicationsRequested;
        _rotatingProxyOrdersControl.ViewApplicationsRequested += HandleViewApplicationsRequested;
        _staticProxyOrdersControl.PurchaseRequested += HandlePurchaseRequested;
        _datacenterProxyOrdersControl.PurchaseRequested += HandlePurchaseRequested;
        _rotatingProxyOrdersControl.PurchaseRequested += HandlePurchaseRequested;
        _staticProxyOrdersControl.RenewCompleted += HandleProxyRenewCompleted;
        _datacenterProxyOrdersControl.RenewCompleted += HandleProxyRenewCompleted;
        _rotatingProxyOrdersControl.RenewCompleted += HandleProxyRenewCompleted;
        _myProxyListControl.ViewApplicationsRequested += HandleViewApplicationsRequested;
        _applicationRulesControl.ViewProxyRequested += HandleViewProxyRequested;
        _purchaseControl.PurchaseCompleted += HandlePurchaseCompleted;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = LightTheme.Background
        };
        _sidebarColumnStyle = new ColumnStyle(SizeType.Absolute, SidebarExpandedWidth);
        _shell.ColumnStyles.Add(_sidebarColumnStyle);
        _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var mainArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = LightTheme.Background
        };
        mainArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var topActions = BuildTopHeaderPanel();
        topActions.Dock = DockStyle.Fill;
        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.BackColor = LightTheme.Background;
        mainArea.Controls.Add(topActions, 0, 0);
        mainArea.Controls.Add(_contentPanel, 0, 1);

        _shell.Controls.Add(BuildSidebar(), 0, 0);
        _shell.Controls.Add(mainArea, 1, 0);
        root.Controls.Add(_shell, 0, 0);
        Controls.Add(root);

        ShowView(_applicationRulesControl, _applicationsSidebarButton, "Ứng dụng");
    }

    private Control BuildSidebar()
    {
        _sidebarRootPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = SidebarBackground,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        _sidebarRootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sidebarRootPanel.Paint += PaintSidebarRootPanel;

        _sidebarPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(SidebarPanelHorizontalPadding, SidebarPanelTopPadding, SidebarPanelHorizontalPadding, SidebarPanelBottomPadding),
            Margin = Padding.Empty,
            BackColor = SidebarBackground
        };

        _sidebarToggleButton = CreateSidebarToggleButton();
        _sidebarPanel.Controls.Add(_sidebarToggleButton);

        _applicationsSidebarButton = CreateSidebarButton("Ứng dụng", "ung-dung.svg", async button =>
        {
            await _applicationRulesControl.LoadAsync();
            ShowView(_applicationRulesControl, button, "Ứng dụng");
        });
        _sidebarPanel.Controls.Add(_applicationsSidebarButton);

        var applicationSeparator = CreateSidebarSeparator();
        RegisterExpandedOnlySidebarControl(applicationSeparator);
        _sidebarPanel.Controls.Add(applicationSeparator);

        _staticProxySidebarButton = CreateSidebarButton("Proxy Tĩnh", "proxy-tinh.svg", async button => await ShowOrderViewAsync(_staticProxyOrdersControl, button));
        _datacenterProxySidebarButton = CreateSidebarButton("Proxy Datacenter", "proxy-data-center.svg", async button => await ShowOrderViewAsync(_datacenterProxyOrdersControl, button));
        _rotatingProxySidebarButton = CreateSidebarButton("Proxy Xoay", "proxy-xoay.svg", async button => await ShowOrderViewAsync(_rotatingProxyOrdersControl, button));
        _myProxySidebarButton = CreateSidebarButton("Proxy của tôi", "proxy-cua-toi.svg", async button => await ShowMyProxyViewAsync(button));
        _sidebarPanel.Controls.Add(_staticProxySidebarButton);
        _sidebarPanel.Controls.Add(_datacenterProxySidebarButton);
        _sidebarPanel.Controls.Add(_rotatingProxySidebarButton);
        _sidebarPanel.Controls.Add(_myProxySidebarButton);

        var settingsSeparator = CreateSidebarSeparator();
        RegisterExpandedOnlySidebarControl(settingsSeparator);
        _sidebarPanel.Controls.Add(settingsSeparator);

        _purchaseSidebarButton = CreateSidebarButton("Mua hàng", "shopping-cart.svg", async button =>
        {
            _purchaseControl.Configure(ProxyProductKind.Static);
            await ShowPurchaseViewAsync(_purchaseControl, button);
        });
        _sidebarPanel.Controls.Add(_purchaseSidebarButton);

        _sidebarPanel.Controls.Add(CreateSidebarButton("Thiết lập", "cai-dat.svg", button =>
        {
            ShowView(BuildSettingsTab(), button, "Thiết lập");
            return Task.CompletedTask;
        }));

        _sidebarRootPanel.Controls.Add(_sidebarPanel, 0, 0);
        ApplySidebarLayoutState();
        return _sidebarRootPanel;
    }

    private static void PaintSidebarRootPanel(object? sender, PaintEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        using var pen = new Pen(SidebarBorderColor, 1);
        e.Graphics.DrawLine(pen, control.Width - 1, 0, control.Width - 1, control.Height);
    }

    private void RegisterExpandedOnlySidebarControl(Control control) =>
        _expandedOnlySidebarControls.Add(control);

    private Panel CreateSidebarLogoHost()
    {
        var panel = new Panel
        {
            AutoSize = false,
            Width = SidebarExpandedButtonWidth,
            Height = 54,
            BackColor = SidebarBackground,
            Margin = new Padding(0, 0, 0, 14)
        };

        _sidebarLogoPictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = SidebarBackground
        };

        _sidebarLogoTextLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = AppBranding.AppDisplayName,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = LightTheme.Primary,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = SidebarBackground,
            Visible = false
        };

        panel.Controls.Add(_sidebarLogoPictureBox);
        panel.Controls.Add(_sidebarLogoTextLabel);
        return panel;
    }

    private static Control CreateSidebarSeparator() =>
        new Panel
        {
            Width = SidebarSeparatorWidth,
            Height = 1,
            BackColor = SidebarBorderColor,
            Margin = new Padding(SidebarSeparatorHorizontalMargin, 5, SidebarSeparatorHorizontalMargin, 5)
        };

    private Button CreateSidebarToggleButton()
    {
        var button = new Button
        {
            AutoSize = false,
            Width = SidebarToggleExpandedWidth,
            Height = SidebarToggleHeight,
            Text = string.Empty,
            Padding = Padding.Empty,
            Margin = new Padding(0, 0, 0, SidebarButtonVerticalGap),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            Tag = LightTheme.SkipButtonThemeTag
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseDownBackColor = SidebarBackground;
        button.FlatAppearance.MouseOverBackColor = SidebarBackground;
        button.BackColor = SidebarBackground;
        button.Click += (_, _) => ToggleSidebarCollapsed();
        button.MouseEnter += (_, _) => button.Invalidate();
        button.MouseLeave += (_, _) => button.Invalidate();
        button.MouseDown += (_, _) => button.Invalidate();
        button.MouseUp += (_, _) => button.Invalidate();
        button.Paint += PaintSidebarToggleButton;
        _sidebarToolTip.SetToolTip(button, "Thu gọn");
        return button;
    }

    private void PaintSidebarToggleButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.Clear(SidebarBackground);

        var iconButtonBounds = new Rectangle(
            _isSidebarCollapsed ? 0 : Math.Max(0, (button.Width - SidebarCollapsedButtonWidth) / 2),
            0,
            SidebarCollapsedButtonWidth,
            SidebarToggleHeight);
        var clientPoint = button.PointToClient(Cursor.Position);
        var isHovering = button.ClientRectangle.Contains(clientPoint);
        var isPressed = isHovering && Control.MouseButtons.HasFlag(MouseButtons.Left);
        var backColor = isPressed
            ? SidebarMenuPressedColor
            : isHovering
                ? SidebarMenuHoverColor
                : Color.Transparent;

        using (var path = AppButton.CreateRoundedRectangle(iconButtonBounds, 4))
        using (var fill = new SolidBrush(backColor))
        {
            e.Graphics.FillPath(fill, path);
        }

        var icon = SidebarIconRenderer.Load("toggle-sidebar.svg", SidebarDefaultIconColor, SidebarButtonIconSize);
        if (icon is null)
        {
            return;
        }

        var iconX = iconButtonBounds.Left + (iconButtonBounds.Width - icon.Width) / 2;
        var iconY = iconButtonBounds.Top + (iconButtonBounds.Height - icon.Height) / 2;
        e.Graphics.DrawImage(icon, iconX, iconY, icon.Width, icon.Height);
    }

    private Button CreateSidebarButton(string text, string iconFileName, Func<Button, Task> onClick)
    {
        var button = new SidebarNavButton
        {
            Text = text,
            AutoSize = false,
            Width = SidebarExpandedButtonWidth,
            Height = SidebarButtonHeight,
            TextAlign = ContentAlignment.MiddleLeft,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            Padding = new Padding(12, 0, 8, 0),
            Margin = new Padding(0, 0, 0, 2),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            Tag = LightTheme.SkipButtonThemeTag,
            IconFileName = iconFileName
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseDownBackColor = SidebarMenuPressedColor;
        button.FlatAppearance.MouseOverBackColor = SidebarMenuHoverColor;
        button.Click += async (_, _) => await onClick(button);
        _sidebarButtons.Add(button);
        _sidebarButtonIconNames[button] = iconFileName;
        _sidebarButtonTexts[button] = text;
        _sidebarToolTip.SetToolTip(button, text);
        return button;
    }

    private void ToggleSidebarCollapsed()
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;
        ApplySidebarLayoutState();
        RefreshSidebarButtons();
    }

    private void ApplySidebarLayoutState()
    {
        if (_sidebarRootPanel is null || _sidebarPanel is null || _sidebarColumnStyle is null || _sidebarToggleButton is null)
        {
            return;
        }

        _sidebarColumnStyle.Width = _isSidebarCollapsed ? SidebarCollapsedWidth : SidebarExpandedWidth;
        _sidebarRootPanel.BackColor = SidebarBackground;
        _sidebarPanel.BackColor = SidebarBackground;
        _sidebarPanel.Padding = _isSidebarCollapsed
            ? new Padding(SidebarPanelHorizontalPadding, SidebarPanelTopPadding, SidebarPanelHorizontalPadding, SidebarPanelBottomPadding)
            : new Padding(SidebarPanelHorizontalPadding, SidebarPanelTopPadding, SidebarPanelHorizontalPadding, SidebarPanelBottomPadding);

        foreach (var control in _expandedOnlySidebarControls)
        {
            control.Visible = !_isSidebarCollapsed;
            control.Width = control is Panel && control.Height == 1 ? SidebarSeparatorWidth : SidebarExpandedButtonWidth;
            control.BackColor = control is Panel && control.Height == 1
                ? SidebarBorderColor
                : SidebarBackground;
        }

        _sidebarToggleButton.Width = _isSidebarCollapsed ? SidebarCollapsedButtonWidth : SidebarToggleExpandedWidth;
        _sidebarToggleButton.Height = SidebarToggleHeight;
        _sidebarToggleButton.Margin = _isSidebarCollapsed
            ? new Padding(0, 0, 0, 2)
            : new Padding(0, 0, 0, 2);
        _sidebarToggleButton.BackColor = SidebarBackground;
        _sidebarToggleButton.FlatAppearance.BorderColor = SidebarBackground;
        _sidebarToggleButton.FlatAppearance.MouseOverBackColor = SidebarBackground;
        _sidebarToggleButton.FlatAppearance.MouseDownBackColor = SidebarBackground;
        _sidebarToolTip.SetToolTip(_sidebarToggleButton, _isSidebarCollapsed ? "Mở rộng sidebar" : "Thu gọn thanh điều hướng");
        _sidebarToggleButton.Invalidate();

        foreach (var button in _sidebarButtons)
        {
            if (_sidebarButtonTexts.TryGetValue(button, out var text))
            {
                button.Text = _isSidebarCollapsed ? string.Empty : text;
                _sidebarToolTip.SetToolTip(button, text);
            }

            button.Width = _isSidebarCollapsed ? SidebarCollapsedButtonWidth : SidebarExpandedButtonWidth;
            button.Height = SidebarButtonHeight;
            button.Padding = _isSidebarCollapsed
                ? Padding.Empty
                : new Padding(12, 0, 8, 0);
            button.ImageAlign = _isSidebarCollapsed ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            button.TextAlign = _isSidebarCollapsed ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            button.TextImageRelation = _isSidebarCollapsed ? TextImageRelation.Overlay : TextImageRelation.ImageBeforeText;
            if (button is SidebarNavButton sidebarButton)
            {
                sidebarButton.IsCollapsed = _isSidebarCollapsed;
            }
        }

        _shell.PerformLayout();
    }

    private void ApplySidebarLogoState()
    {
        var logo = _isSidebarCollapsed ? GetSmallSidebarLogoImage() : GetLargeSidebarLogoImage();
        _sidebarLogoHost.Width = _isSidebarCollapsed ? SidebarCollapsedButtonWidth : SidebarExpandedButtonWidth;
        _sidebarLogoHost.Height = _isSidebarCollapsed ? 48 : 54;
        _sidebarLogoHost.Margin = _isSidebarCollapsed ? new Padding(0, 0, 0, 14) : new Padding(0, 0, 0, 14);
        _sidebarLogoHost.BackColor = SidebarBackground;
        _sidebarLogoPictureBox.BackColor = SidebarBackground;
        _sidebarLogoTextLabel.BackColor = SidebarBackground;

        _sidebarLogoPictureBox.Image = logo;
        _sidebarLogoPictureBox.Visible = logo is not null;
        _sidebarLogoTextLabel.Visible = !_isSidebarCollapsed && logo is null;
        _sidebarLogoTextLabel.TextAlign = ContentAlignment.MiddleLeft;
    }

    private Image? GetLargeSidebarLogoImage()
    {
        if (!_largeSidebarLogoResolved)
        {
            _largeSidebarLogoImage = AppBranding.LoadLogoImage();
            _largeSidebarLogoResolved = true;
        }

        return _largeSidebarLogoImage;
    }

    private Image? GetSmallSidebarLogoImage()
    {
        if (!_smallSidebarLogoResolved)
        {
            _smallSidebarLogoImage = AppBranding.LoadSmallLogoImage();
            if (_smallSidebarLogoImage is null)
            {
                using var icon = AppBranding.LoadApplicationIcon();
                _smallSidebarLogoImage = icon?.ToBitmap();
            }

            _smallSidebarLogoResolved = true;
        }

        return _smallSidebarLogoImage;
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private async Task ShowOrderViewAsync(ProxyOrdersControl control, Button button)
    {
        ShowView(control, button, GetProxyOrderHeaderTitle(control));
        await control.LoadAsync();
    }

    private async Task ShowPurchaseViewAsync(ProductPurchaseControl control, Button button)
    {
        ShowView(control, button, "Mua hàng");
        await control.LoadAsync();
    }

    private async void HandlePurchaseRequested(object? sender, ProxyOrderKind kind)
    {
        var productKind = kind switch
        {
            ProxyOrderKind.Datacenter => ProxyProductKind.Datacenter,
            ProxyOrderKind.RotateProxy or ProxyOrderKind.RotateKey => ProxyProductKind.Rotate,
            _ => ProxyProductKind.Static
        };

        _purchaseControl.Configure(productKind);
        await ShowPurchaseViewAsync(_purchaseControl, _purchaseSidebarButton);
    }

    private async Task ShowMyProxyViewAsync(Button button)
    {
        ShowView(_myProxyListControl, button, "Proxy của tôi");
        await _myProxyListControl.LoadAsync();
    }

    private async void HandleViewApplicationsRequested(object? sender, Guid proxyId)
    {
        ShowView(_applicationRulesControl, _applicationsSidebarButton, "Ứng dụng");
        await _applicationRulesControl.LoadAndSelectApplicationsUsingProxyAsync(proxyId);
    }

    private async void HandleViewProxyRequested(object? sender, Guid proxyId)
    {
        var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
        var proxy = proxies.FirstOrDefault(item => item.Id == proxyId);
        if (proxy is null)
        {
            return;
        }

        if (!proxy.IsFromBackend)
        {
            ShowView(_myProxyListControl, _myProxySidebarButton, "Proxy của tôi");
            await _myProxyListControl.LoadAndFocusProxyAsync(proxy);
            return;
        }

        var (control, button) = proxy.BackendOrderKind switch
        {
            ProxyOrderKind.Datacenter => (_datacenterProxyOrdersControl, _datacenterProxySidebarButton),
            ProxyOrderKind.RotateProxy => (_rotatingProxyOrdersControl, _rotatingProxySidebarButton),
            ProxyOrderKind.RotateKey => (_rotatingProxyOrdersControl, _rotatingProxySidebarButton),
            _ => (_staticProxyOrdersControl, _staticProxySidebarButton)
        };

        ShowView(control, button, GetProxyOrderHeaderTitle(control));
        await control.LoadAndFocusProxyAsync(proxy);
    }

    private void ShowView(Control view, Button button, string title)
    {
        _headerTitleLabel.Text = title;
        _contentPanel.Controls.Clear();
        view.Dock = DockStyle.Fill;
        _contentPanel.Controls.Add(view);
        _selectedSidebarButton = button;
        RefreshSidebarButtons();
    }

    private string GetProxyOrderHeaderTitle(ProxyOrdersControl control)
    {
        if (ReferenceEquals(control, _datacenterProxyOrdersControl))
        {
            return "Đơn hàng Proxy Datacenter";
        }

        if (ReferenceEquals(control, _rotatingProxyOrdersControl))
        {
            return "Đơn hàng Proxy Xoay";
        }

        return "Đơn hàng Proxy Tĩnh";
    }

    private void RefreshSidebarButtons()
    {
        foreach (var button in _sidebarButtons)
        {
            var selected = ReferenceEquals(button, _selectedSidebarButton);
            button.BackColor = SidebarBackground;
            button.ForeColor = selected ? SidebarSelectedTextColor : SidebarDefaultTextColor;
            button.Font = new Font(selected ? "Segoe UI Semibold" : "Segoe UI", SidebarButtonFontSize, FontStyle.Regular);
            button.FlatAppearance.BorderColor = SidebarBackground;
            button.FlatAppearance.MouseOverBackColor = SidebarMenuHoverColor;
            button.FlatAppearance.MouseDownBackColor = SidebarMenuPressedColor;
            if (button is SidebarNavButton sidebarButton)
            {
                sidebarButton.IsSelected = selected;
                sidebarButton.IsCollapsed = _isSidebarCollapsed;
            }
        }
    }

    private Control BuildPlaceholderView(string title, string message)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            RowCount = 2,
            ColumnCount = 1
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);
        panel.Controls.Add(new Label
        {
            Text = message,
            AutoSize = true,
            ForeColor = LightTheme.Muted
        }, 0, 1);
        return panel;
    }

    private Control BuildTopHeaderPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Color.White,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 0, 16, 0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));

        var headerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, 8)
        };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var rightActions = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            BackColor = Color.White,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        rightActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rightActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rightActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rightActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rightActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        _headerTitleLabel.AutoSize = true;
        _headerTitleLabel.Anchor = AnchorStyles.Left;
        _headerTitleLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _headerTitleLabel.ForeColor = HeaderText;
        _headerTitleLabel.Margin = Padding.Empty;
        _headerTitleLabel.Text = "Ứng dụng";

        _sessionActionControl = new SessionProxyActionControl();
        _sessionActionControl.StartRequested += async (_, _) => await StartProxySessionAsync();
        _sessionActionControl.StopRequested += async (_, _) => await StopProxySessionAsync();
        _applicationRulesControl.SetLeadingToolbarControl(_sessionActionControl);

        _balanceButton = CreateHeaderButton("Coin", async (_, _) => await OpenDepositModalAsync(), filled: false, image: UiIcons.NewWallet);
        _loginButton = CreateHeaderButton("Đăng nhập", async (_, _) => await ShowLoginModalAsync(), filled: true);
        _logoutButton = CreateHeaderButton("Đăng xuất", async (_, _) => await LogoutAsync(), filled: false);

        _accountLabel.AutoSize = true;
        _accountLabel.Anchor = AnchorStyles.None;
        _accountLabel.Margin = new Padding(4, 0, 8, 0);
        _accountLabel.ForeColor = Color.Black;
        _accountLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _accountLabel.TextAlign = ContentAlignment.MiddleCenter;

        _loginButton.Anchor = AnchorStyles.None;
        _balanceButton.Anchor = AnchorStyles.None;
        _logoutButton.Anchor = AnchorStyles.None;

        rightActions.Controls.Add(_loginButton, 0, 0);
        rightActions.Controls.Add(_balanceButton, 1, 0);
        rightActions.Controls.Add(_accountLabel, 2, 0);
        rightActions.Controls.Add(_logoutButton, 3, 0);
        headerRow.Controls.Add(_headerTitleLabel, 0, 0);
        headerRow.Controls.Add(rightActions, 1, 0);
        panel.Controls.Add(headerRow, 0, 0);
        panel.Controls.Add(new HeaderDivider
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        }, 0, 1);

        ApplyLoggedOutAccountState();
        RefreshSessionActionButton();
        return panel;
    }

    private static Button CreateHeaderButton(string text, EventHandler onClick, bool filled, Image? image = null)
    {
        var isCoinButton = image is not null;
        var button = new HeaderActionButton
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = 32,
            MinimumSize = new Size(isCoinButton ? 132 : filled ? 112 : 96, 32),
            Padding = isCoinButton || filled ? new Padding(10, 5, 10, 5) : new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 0, 12, 0),
            Font = new Font("Segoe UI", filled ? 11f : 10f, filled || isCoinButton ? FontStyle.Bold : FontStyle.Regular),
            Image = image,
            TextAlign = ContentAlignment.MiddleCenter,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            RestBackColor = isCoinButton ? HeaderAccentSoft : filled ? HeaderAccent : Color.White,
            HoverBackColor = isCoinButton
                ? ControlPaint.Light(HeaderAccentSoft, 0.04f)
                : filled
                    ? ControlPaint.Light(HeaderAccent, 0.10f)
                    : LightTheme.ButtonHover,
            PressedBackColor = isCoinButton
                ? ControlPaint.Dark(HeaderAccentSoft, 0.03f)
                : filled
                    ? ControlPaint.Dark(HeaderAccent, 0.08f)
                    : HeaderAccentSoft,
            BorderColor = isCoinButton || filled ? HeaderAccent : HeaderButtonBorder,
            TextColor = isCoinButton ? HeaderAccent : filled ? Color.White : Color.FromArgb(36, 36, 36)
        };
        button.Click += onClick;
        return button;
    }

    private Control BuildTopActionPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = LightTheme.Background,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8, 4, 8, 4),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var leftActions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            WrapContents = false
        };

        var rightActions = new FlowLayoutPanel
        {
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            WrapContents = false
        };

        _sessionActionButton = LightTheme.CreateToolbarButton(
            string.Empty,
            async (_, _) => await HandleSessionActionAsync());
        _balanceButton = LightTheme.CreateToolbarButton(
            "Coin",
            async (_, _) => await OpenDepositModalAsync(),
            LightTheme.Warning,
            UiIcons.Coin);
        _loginButton = LightTheme.CreateToolbarButton(
            "Đăng nhập",
            async (_, _) => await ShowLoginModalAsync(),
            LightTheme.Accent);
        _logoutButton = LightTheme.CreateToolbarButton(
            "Đăng xuất",
            async (_, _) => await LogoutAsync(),
            LightTheme.Danger);

        _accountLabel.AutoSize = true;
        _accountLabel.Anchor = AnchorStyles.Left;
        _accountLabel.Padding = new Padding(12, 9, 12, 0);
        _accountLabel.ForeColor = LightTheme.Foreground;
        _accountLabel.Font = new Font(Font, FontStyle.Regular);

        leftActions.Controls.Add(_sessionActionButton);
        rightActions.Controls.Add(_loginButton);
        rightActions.Controls.Add(_balanceButton);
        rightActions.Controls.Add(_accountLabel);
        rightActions.Controls.Add(_logoutButton);
        panel.Controls.Add(leftActions, 0, 0);
        panel.Controls.Add(rightActions, 1, 0);
        ApplyLoggedOutAccountState();
        RefreshSessionActionButton();
        return panel;
    }

    private static void PositionTopActionPanel(Control host, Control panel)
    {
        panel.Location = new Point(
            Math.Max(0, host.ClientSize.Width - panel.Width - 10),
            2);
    }

    private Control BuildSettingsTab()
    {
        return BuildEndUserSettingsTab();
    }

    private Control BuildEndUserSettingsTab()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 14, 12, 0),
            BackColor = LightTheme.Background
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = SettingsCardsHeight,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = LightTheme.Background
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32.7f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32.7f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34.6f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsCardsHeight));

        layout.Controls.Add(BuildAccountSettingsCard(), 0, 0);
        layout.Controls.Add(BuildPasswordSettingsCard(), 1, 0);
        layout.Controls.Add(BuildGeneralSettingsCard(), 2, 0);

        host.Controls.Add(layout);
        return host;
    }

    private Control BuildProxyRestartPreferenceRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 12, 0, 0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // row.Controls.Add(new Label
        // {
        //     Text = "Sau khi load profile",
        //     AutoSize = true,
        //     Anchor = AnchorStyles.Left,
        //     Margin = new Padding(0, 7, 12, 0)
        // }, 0, 0);

        _autoRestartApplicationsCheckBox.Checked = _proxyRestartConfirmationPreference == ProxyRestartConfirmationPreference.AutoRestart;
        _autoRestartApplicationsCheckBox.BackColor = SettingsCardBackground;
        _autoRestartApplicationsCheckBox.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _autoRestartApplicationsCheckBox.CheckedChanged += AutoRestartApplicationsCheckBoxCheckedChanged;
        row.Controls.Add(_autoRestartApplicationsCheckBox, 1, 0);
        return row;
    }

    private Control BuildAccountSettingsCard()
    {
        var card = CreateSettingsCard("Thông tin tài khoản");
        var content = (TableLayoutPanel)card.Controls[0];
        content.RowCount = 6;
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        ConfigureAccountInput(_accountDisplayNameInput);
        ConfigureAccountInput(_accountEmailInput);

        content.Controls.Add(BuildEditableAccountRow(
            "Tên hiển thị",
            _accountDisplayNameValueLabel,
            _accountDisplayNameInput,
            AccountEditField.DisplayName,
            canEdit: true), 0, 1);
        content.Controls.Add(BuildEditableAccountRow(
            "Email",
            _accountEmailValueLabel,
            _accountEmailInput,
            AccountEditField.Email,
            canEdit: true), 0, 2);
        content.Controls.Add(BuildReadonlyAccountRow("Số điện thoại", _accountPhoneValueLabel, showDisabledEdit: false), 0, 3);
        content.Controls.Add(BuildReadonlyAccountRow("Ngày tham gia", _accountJoinedValueLabel, showDisabledEdit: false), 0, 4);

        _accountMessageLabel.AutoSize = true;
        _accountMessageLabel.ForeColor = LightTheme.Muted;
        _accountMessageLabel.BackColor = SettingsCardBackground;
        _accountMessageLabel.Margin = new Padding(0, 6, 0, 0);
        content.Controls.Add(_accountMessageLabel, 0, 5);

        UpdateSettingsAccountView(_currentAuthUser);
        return card;
    }

    private Control BuildPasswordSettingsCard()
    {
        var card = CreateSettingsCard("Đổi mật khẩu");
        var content = (TableLayoutPanel)card.Controls[0];
        content.RowCount = 7;
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        ConfigurePasswordInput(_oldPasswordInput, "Nhập mật khẩu cũ");
        ConfigurePasswordInput(_newPasswordInput, "Mật khẩu mới");
        ConfigurePasswordInput(_confirmPasswordInput, "Xác nhận mật khẩu mới");

        content.Controls.Add(_oldPasswordInput, 0, 1);
        content.Controls.Add(_newPasswordInput, 0, 2);
        content.Controls.Add(_confirmPasswordInput, 0, 3);

        var button = new AppPrimaryButton
        {
            Text = "Đổi mật khẩu",
            Dock = DockStyle.Top,
            Height = 32,
            MinimumSize = new Size(0, 32),
            Margin = new Padding(0, 10, 0, 0),
            Padding = new Padding(10, 4, 10, 4)
        };
        button.Click += async (_, _) => await ChangePasswordAsync(button);
        WireEnterSubmit(_oldPasswordInput, async () => await ChangePasswordAsync(button));
        WireEnterSubmit(_newPasswordInput, async () => await ChangePasswordAsync(button));
        WireEnterSubmit(_confirmPasswordInput, async () => await ChangePasswordAsync(button));
        content.Controls.Add(button, 0, 4);

        _passwordMessageLabel.AutoSize = true;
        _passwordMessageLabel.ForeColor = LightTheme.Muted;
        _passwordMessageLabel.BackColor = SettingsCardBackground;
        _passwordMessageLabel.Margin = new Padding(0, 6, 0, 0);
        content.Controls.Add(_passwordMessageLabel, 0, 5);
        return card;
    }

    private Control BuildGeneralSettingsCard()
    {
        var card = CreateSettingsCard("Cài đặt chung");
        var content = (TableLayoutPanel)card.Controls[0];
        content.RowCount = 6;
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var runtimeRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = SettingsCardBackground,
            Margin = new Padding(0, 8, 0, 8)
        };
        runtimeRow.Controls.Add(CreateSettingsLabel("Trạng thái"));
        runtimeRow.Controls.Add(new Label
        {
            Text = "•",
            AutoSize = true,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = HeaderText,
            Margin = new Padding(12, 1, 0, 0)
        });
        _runtimeStateLabel.AutoSize = true;
        _runtimeStateLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _runtimeStateLabel.ForeColor = HeaderText;
        _runtimeStateLabel.Margin = new Padding(6, 2, 0, 0);
        runtimeRow.Controls.Add(_runtimeStateLabel);
        UpdateRuntimeState(_runtimeState);
        content.Controls.Add(runtimeRow, 0, 1);

        var keyLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = SettingsCardBackground,
            Margin = new Padding(0, 0, 0, 8)
        };
        keyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        keyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        keyLayout.Controls.Add(CreateSettingsLabel("Key"), 0, 0);
        _registrationKeyTextBox.Dock = DockStyle.Top;
        _registrationKeyTextBox.Margin = new Padding(12, 0, 8, 0);
        _registrationKeyTextBox.UseSystemPasswordChar = false;
        WireEnterSubmit(_registrationKeyTextBox, () =>
        {
            SaveRegistrationKey();
            return Task.CompletedTask;
        });
        keyLayout.Controls.Add(_registrationKeyTextBox, 1, 0);

        var saveKeyButton = new AppPrimaryButton
        {
            Text = "Lưu",
            Height = 28,
            MinimumSize = new Size(48, 28),
            Margin = Padding.Empty,
            Padding = new Padding(8, 3, 8, 3)
        };
        saveKeyButton.Click += (_, _) => SaveRegistrationKey();
        keyLayout.Controls.Add(saveKeyButton, 2, 0);
        content.Controls.Add(keyLayout, 0, 2);

        content.Controls.Add(BuildProxyRestartPreferenceRow(), 0, 3);

        _commandResultLabel.AutoSize = true;
        _commandResultLabel.ForeColor = LightTheme.Muted;
        _commandResultLabel.BackColor = SettingsCardBackground;
        _commandResultLabel.Margin = new Padding(0, 8, 0, 0);
        content.Controls.Add(_commandResultLabel, 0, 4);
        return card;
    }

    private static AppCardPanel CreateSettingsCard(string title)
    {
        var card = new AppCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 12, 0),
            BackColor = SettingsCardBackground,
            Padding = new Padding(12)
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SettingsCardBackground,
            ColumnCount = 1,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(new Label
        {
            Text = title,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = HeaderText,
            BackColor = SettingsCardBackground,
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);

        card.Controls.Add(content);
        return card;
    }

    private Control BuildEditableAccountRow(
        string title,
        Label valueLabel,
        AppTextInput input,
        AccountEditField field,
        bool canEdit)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 10),
            BackColor = SettingsCardBackground
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Paint += PaintSettingsRowDivider;

        row.Controls.Add(CreateSettingsLabel(title), 0, 0);

        var valueHost = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = SettingsCardBackground,
            Margin = Padding.Empty
        };
        ConfigureAccountValueLabel(valueLabel);
        valueHost.Controls.Add(valueLabel);
        valueHost.Controls.Add(input);
        input.Visible = false;
        row.Controls.Add(valueHost, 1, 0);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = SettingsCardBackground,
            Margin = Padding.Empty
        };
        var editButton = CreateIconButton(enabled: canEdit);
        var saveButton = CreateMiniPrimaryButton("Lưu");
        var cancelButton = CreateMiniSecondaryButton("Hủy");
        saveButton.Visible = false;
        cancelButton.Visible = false;

        editButton.Click += (_, _) => BeginAccountEdit(valueLabel, input, editButton, saveButton, cancelButton);
        cancelButton.Click += (_, _) => CancelAccountEdit(valueLabel, input, editButton, saveButton, cancelButton);
        saveButton.Click += async (_, _) => await SaveAccountFieldAsync(field, valueLabel, input, editButton, saveButton, cancelButton);
        WireEnterSubmit(input, async () => await SaveAccountFieldAsync(field, valueLabel, input, editButton, saveButton, cancelButton));

        actions.Controls.Add(editButton);
        actions.Controls.Add(saveButton);
        actions.Controls.Add(cancelButton);
        row.Controls.Add(actions, 2, 0);
        return row;
    }

    private Control BuildReadonlyAccountRow(string title, Label valueLabel, bool showDisabledEdit)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 10),
            BackColor = SettingsCardBackground
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Paint += PaintSettingsRowDivider;

        row.Controls.Add(CreateSettingsLabel(title), 0, 0);
        ConfigureAccountValueLabel(valueLabel);
        row.Controls.Add(valueLabel, 1, 0);

        if (showDisabledEdit)
        {
            var editButton = CreateIconButton(enabled: false);
            _settingsToolTip.SetToolTip(editButton, "Số điện thoại không thể chỉnh sửa trong ứng dụng.");
            row.Controls.Add(editButton, 2, 0);
        }

        return row;
    }

    private static Label CreateSettingsLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = LightTheme.Muted,
            BackColor = SettingsCardBackground,
            Margin = new Padding(0, 6, 0, 0)
        };

    private static void ConfigureAccountValueLabel(Label label)
    {
        label.Dock = DockStyle.Top;
        label.AutoSize = false;
        label.Height = 36;
        label.TextAlign = ContentAlignment.MiddleRight;
        label.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        label.ForeColor = HeaderText;
        label.BackColor = SettingsCardBackground;
        label.Margin = Padding.Empty;
    }

    private static void ConfigureAccountInput(AppTextInput input)
    {
        input.Dock = DockStyle.Top;
        input.Height = 42;
        input.Margin = Padding.Empty;
        input.BackColor = SettingsCardBackground;
    }

    private static void ConfigurePasswordInput(AppTextInput input, string label)
    {
        input.Dock = DockStyle.Top;
        input.LabelText = label;
        input.LabelContentHeight = 28;
        input.LabelInputGap = 6;
        input.ErrorMessageHeight = 34;
        input.PlaceholderText = string.Empty;
        input.UseSystemPasswordChar = true;
        input.ShowPasswordToggle = true;
        input.LeadingIcon = UiIcons.NewLock;
        input.BackColor = SettingsCardBackground;
        input.Margin = new Padding(0, 0, 0, 10);
    }

    private static Button CreateIconButton(bool enabled)
    {
        var button = new Button
        {
            Size = new Size(32, 32),
            MinimumSize = new Size(32, 32),
            MaximumSize = new Size(32, 32),
            Image = UiIcons.EditLoggedUserInfo,
            Enabled = enabled,
            Tag = LightTheme.SkipButtonThemeTag,
            Cursor = enabled ? Cursors.Hand : Cursors.Default,
            FlatStyle = FlatStyle.Flat,
            BackColor = SettingsCardBackground,
            Margin = new Padding(8, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = LightTheme.ButtonHover;
        button.FlatAppearance.MouseDownBackColor = LightTheme.AccentSoft;
        return button;
    }

    private static AppPrimaryButton CreateMiniPrimaryButton(string text) =>
        new()
        {
            Text = text,
            Height = 28,
            MinimumSize = new Size(48, 28),
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(8, 3, 8, 3)
        };

    private static AppButton CreateMiniSecondaryButton(string text)
    {
        var button = new AppButton
        {
            Text = text,
            Height = 28,
            MinimumSize = new Size(48, 28),
            Margin = new Padding(6, 0, 0, 0),
            Padding = new Padding(8, 3, 8, 3),
            Variant = AppButtonVariant.Muted,
            AccentColor = LightTheme.Muted,
            Filled = false
        };
        LightTheme.SetSecondaryButton(button, LightTheme.Muted);
        return button;
    }

    private static void PaintSettingsRowDivider(object? sender, PaintEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        using var pen = new Pen(SettingsDivider, 1);
        var y = control.ClientSize.Height - 1;
        e.Graphics.DrawLine(pen, 0, y, control.ClientSize.Width, y);
    }

    private static void WireEnterSubmit(AppTextInput input, Func<Task> submitAsync)
    {
        input.InnerTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            await submitAsync();
        };
    }

    private void BeginAccountEdit(
        Label valueLabel,
        AppTextInput input,
        Button editButton,
        Button saveButton,
        Button cancelButton)
    {
        if (_currentAuthUser is null)
        {
            _accountMessageLabel.ForeColor = LightTheme.Warning;
            _accountMessageLabel.Text = "Vui lòng đăng nhập để cập nhật thông tin.";
            return;
        }

        _accountMessageLabel.Text = string.Empty;
        input.Text = valueLabel.Text == "-" ? string.Empty : valueLabel.Text;
        input.ClearError();
        valueLabel.Visible = false;
        input.Visible = true;
        editButton.Visible = false;
        saveButton.Visible = true;
        cancelButton.Visible = true;
        input.FocusInput();
    }

    private static void CancelAccountEdit(
        Label valueLabel,
        AppTextInput input,
        Button editButton,
        Button saveButton,
        Button cancelButton)
    {
        input.ClearError();
        input.Visible = false;
        valueLabel.Visible = true;
        editButton.Visible = true;
        saveButton.Visible = false;
        cancelButton.Visible = false;
    }

    private async Task SaveAccountFieldAsync(
        AccountEditField field,
        Label valueLabel,
        AppTextInput input,
        Button editButton,
        Button saveButton,
        Button cancelButton)
    {
        if (_currentAuthUser is null)
        {
            input.SetError("Vui lòng đăng nhập.");
            return;
        }

        var request = CreateUpdateRequestFromCurrentUser();
        if (field == AccountEditField.DisplayName)
        {
            var displayName = NormalizeSettingsText(input.TextValue);
            if (!TrySplitDisplayName(displayName, out var firstName, out var lastName))
            {
                input.SetError("Vui lòng nhập đầy đủ họ và tên.");
                return;
            }

            request.FirstName = firstName;
            request.LastName = lastName;
        }
        else
        {
            var email = input.TextValue.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                input.SetError("Email là bắt buộc.");
                return;
            }

            if (!SettingsEmailRegex.IsMatch(email))
            {
                input.SetError("Email không hợp lệ.");
                return;
            }

            request.Email = email;
        }

        await RunAccountUpdateAsync(
            async () =>
            {
                var user = await _authService.UpdateCurrentUserAsync(request, CancellationToken.None);
                _currentAuthUser = user;
                UpdateSettingsAccountView(user);
                CancelAccountEdit(valueLabel, input, editButton, saveButton, cancelButton);
                _accountMessageLabel.ForeColor = LightTheme.Success;
                _accountMessageLabel.Text = "Đã cập nhật thông tin.";
                await RefreshAccountStateAsync();
            },
            input,
            saveButton,
            cancelButton);
    }

    private async Task ChangePasswordAsync(Button button)
    {
        ClearPasswordErrors();
        if (_currentAuthUser is null)
        {
            _passwordMessageLabel.ForeColor = LightTheme.Warning;
            _passwordMessageLabel.Text = "Vui lòng đăng nhập để đổi mật khẩu.";
            return;
        }

        if (!ValidatePasswordInputs())
        {
            return;
        }

        var request = CreateUpdateRequestFromCurrentUser();
        request.OldPassword = _oldPasswordInput.TextValue;
        request.Password = _newPasswordInput.TextValue;

        await RunPasswordUpdateAsync(async () =>
        {
            await _authService.ChangePasswordAsync(request, CancellationToken.None);
            _oldPasswordInput.Clear();
            _newPasswordInput.Clear();
            _confirmPasswordInput.Clear();
            _passwordMessageLabel.ForeColor = LightTheme.Success;
            _passwordMessageLabel.Text = "Đã đổi mật khẩu.";
            await RefreshAfterAuthScopeChangedAsync();
        }, button);
    }

    private async Task RunAccountUpdateAsync(
        Func<Task> action,
        AppTextInput input,
        params Button[] buttons)
    {
        SetButtonsEnabled(false, buttons);
        try
        {
            await action();
        }
        catch (AuthApiException ex)
        {
            input.SetError(GetFriendlyAuthUpdateError(ex));
            _accountMessageLabel.ForeColor = LightTheme.Danger;
            _accountMessageLabel.Text = "Không thể cập nhật thông tin.";
        }
        catch (Exception ex)
        {
            input.SetError(ex.Message);
            _accountMessageLabel.ForeColor = LightTheme.Danger;
            _accountMessageLabel.Text = "Không thể cập nhật thông tin.";
        }
        finally
        {
            SetButtonsEnabled(true, buttons);
        }
    }

    private async Task RunPasswordUpdateAsync(Func<Task> action, Button button)
    {
        button.Enabled = false;
        try
        {
            await action();
        }
        catch (AuthApiException ex)
        {
            if (await ApplyLoggedOutPasswordChangeStateIfNeededAsync())
            {
                return;
            }

            ApplyPasswordAuthError(ex);
        }
        catch (Exception ex)
        {
            if (await ApplyLoggedOutPasswordChangeStateIfNeededAsync())
            {
                return;
            }

            _passwordMessageLabel.ForeColor = LightTheme.Danger;
            _passwordMessageLabel.Text = ex.Message;
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private async Task<bool> ApplyLoggedOutPasswordChangeStateIfNeededAsync()
    {
        if (await _authService.GetSessionAsync(CancellationToken.None) is not null)
        {
            return false;
        }

        _proxyOrderCacheService.Clear();
        ApplyLoggedOutAccountState();
        _passwordMessageLabel.ForeColor = LightTheme.Success;
        _passwordMessageLabel.Text = "Đã đổi mật khẩu. Vui lòng đăng nhập lại.";
        return true;
    }

    private bool ValidatePasswordInputs()
    {
        var isValid = true;
        if (string.IsNullOrWhiteSpace(_oldPasswordInput.TextValue))
        {
            _oldPasswordInput.SetError("Vui lòng nhập mật khẩu cũ.");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(_newPasswordInput.TextValue))
        {
            _newPasswordInput.SetError("Vui lòng nhập mật khẩu mới.");
            isValid = false;
        }
        else if (_newPasswordInput.TextValue.Length < 6)
        {
            _newPasswordInput.SetError("Mật khẩu mới tối thiểu 6 ký tự.");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(_confirmPasswordInput.TextValue))
        {
            _confirmPasswordInput.SetError("Vui lòng xác nhận mật khẩu mới.");
            isValid = false;
        }
        else if (!string.Equals(_confirmPasswordInput.TextValue, _newPasswordInput.TextValue, StringComparison.Ordinal))
        {
            _confirmPasswordInput.SetError("Mật khẩu xác nhận không khớp.");
            isValid = false;
        }

        return isValid;
    }

    private void ApplyPasswordAuthError(AuthApiException exception)
    {
        var message = GetFriendlyAuthUpdateError(exception);
        if (exception.HasError("oldPassword", "incorrectOldPassword") ||
            exception.HasError("oldPassword", "incorrectPassword") ||
            exception.HasError("password", "incorrectOldPassword") ||
            exception.HasError("password", "incorrectPassword"))
        {
            _oldPasswordInput.SetError(message);
        }
        else
        {
            _passwordMessageLabel.ForeColor = LightTheme.Danger;
            _passwordMessageLabel.Text = message;
        }
    }

    private void ClearPasswordErrors()
    {
        _oldPasswordInput.ClearError();
        _newPasswordInput.ClearError();
        _confirmPasswordInput.ClearError();
        _passwordMessageLabel.Text = string.Empty;
    }

    private AuthUpdateRequest CreateUpdateRequestFromCurrentUser()
    {
        var user = _currentAuthUser ?? new AuthUser();
        return new AuthUpdateRequest
        {
            FirstName = user.FirstName ?? string.Empty,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Phone = user.Phone ?? string.Empty,
            UserName = user.UserName,
            Gender = string.IsNullOrWhiteSpace(user.Gender) ? "male" : user.Gender
        };
    }

    private void UpdateSettingsAccountView(AuthUser? user)
    {
        _currentAuthUser = user;
        _accountDisplayNameValueLabel.Text = user?.DisplayName ?? "-";
        _accountEmailValueLabel.Text = string.IsNullOrWhiteSpace(user?.Email) ? "-" : user.Email;
        _accountPhoneValueLabel.Text = string.IsNullOrWhiteSpace(user?.Phone) ? "-" : user.Phone;
        _accountJoinedValueLabel.Text = user?.CreatedAt is { } createdAt
            ? createdAt.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : "-";
    }

    private static void SetButtonsEnabled(bool enabled, IEnumerable<Button> buttons)
    {
        foreach (var button in buttons)
        {
            button.Enabled = enabled;
        }
    }

    private static string GetFriendlyAuthUpdateError(AuthApiException exception)
    {
        if (exception.HasError("oldPassword", "incorrectOldPassword") ||
            exception.HasError("oldPassword", "incorrectPassword") ||
            exception.HasError("password", "incorrectOldPassword") ||
            exception.HasError("password", "incorrectPassword"))
        {
            return "Mật khẩu hiện tại không chính xác.";
        }

        if (exception.HasError("email", "duplicateEmail"))
        {
            return "Email đã được sử dụng.";
        }

        if (exception.HasError("phone", "duplicatePhone"))
        {
            return "Số điện thoại đã được sử dụng.";
        }

        if (exception.HasError("userName", "duplicateUserName"))
        {
            return "Tên đăng nhập đã tồn tại.";
        }

        if (exception.HasError("email", "invalidEmail"))
        {
            return "Email không hợp lệ.";
        }

        if (exception.HasError("phone", "invalidPhone"))
        {
            return "Số điện thoại không hợp lệ.";
        }

        if (exception.HasError("password", "invalidPassword"))
        {
            return "Mật khẩu không hợp lệ.";
        }

        return "Không thể cập nhật thông tin. Vui lòng thử lại.";
    }

    private static bool TrySplitDisplayName(string displayName, out string firstName, out string lastName)
    {
        firstName = string.Empty;
        lastName = string.Empty;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        var lastSpaceIndex = displayName.LastIndexOf(' ');
        if (lastSpaceIndex <= 0 || lastSpaceIndex >= displayName.Length - 1)
        {
            return false;
        }

        firstName = displayName[..lastSpaceIndex];
        lastName = displayName[(lastSpaceIndex + 1)..];
        return true;
    }

    private static string NormalizeSettingsText(string value) =>
        SettingsWhitespaceRegex.Replace(value.Trim(), " ");

    private enum AccountEditField
    {
        DisplayName,
        Email
    }

    private async Task InitializeAsync()
    {
        var settings = await _settingsRepository.GetAsync(CancellationToken.None);
        _proxifierPathTextBox.Text = settings.ProxifierExecutablePath;
        _lastProfileTextBox.Text = Path.Combine(settings.ProfileOutputDirectory, "active.ppx");
        _proxyRestartConfirmationPreference = settings.ProxyRestartConfirmationPreference;
        SetAutoRestartApplicationsCheckBox(_proxyRestartConfirmationPreference == ProxyRestartConfirmationPreference.AutoRestart);

        await RefreshAccountStateAsync();
        await _applicationRulesControl.LoadAsync();
        await RefreshStartEligibilityAsync();
        RefreshProxifierVisibilityButton(settings.ProxifierExecutablePath);
        await TryAutoInstallProxifierSilentlyAsync();
    }

    private async Task RefreshAccountStateAsync()
    {
        if (!await _authService.EnsureValidSessionAsync(CancellationToken.None))
        {
            ApplyLoggedOutAccountState();
            return;
        }

        try
        {
            var user = await _authService.GetCurrentUserAsync(CancellationToken.None);
            _currentAuthUser = user;
            UpdateSettingsAccountView(user);
            ApplyLoggedInAccountState(user?.DisplayName ?? AppBranding.AppDisplayName);
            _balanceButton.Text = FormatCoin(user?.Coin);
        }
        catch
        {
            var session = await _authService.GetSessionAsync(CancellationToken.None);
            if (session is null)
            {
                ApplyLoggedOutAccountState();
                return;
            }

            _currentAuthUser = session.User;
            UpdateSettingsAccountView(session.User);
            ApplyLoggedInAccountState(session.User?.DisplayName ?? AppBranding.AppDisplayName);
            _balanceButton.Text = FormatCoin(session?.User?.Coin);
        }
    }

    private void ApplyLoggedOutAccountState()
    {
        _currentAuthUser = null;
        UpdateSettingsAccountView(null);
        _accountLabel.Text = string.Empty;
        _balanceButton.Text = "Coin";
        _loginButton.Visible = true;
        _balanceButton.Visible = false;
        _accountLabel.Visible = false;
        _logoutButton.Visible = false;
    }

    private void ApplyLoggedInAccountState(string displayName)
    {
        _accountLabel.Text = displayName;
        _loginButton.Visible = false;
        _balanceButton.Visible = true;
        _accountLabel.Visible = true;
        _logoutButton.Visible = true;
        _logoutButton.Enabled = true;
    }

    private async Task<bool> ShowLoginModalAsync()
    {
        using var loginForm = _serviceProvider.GetRequiredService<LoginForm>();
        if (loginForm.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        _proxyOrderCacheService.Clear();
        await RefreshAfterAuthScopeChangedAsync();
        return true;
    }

    private async Task RefreshAfterAuthScopeChangedAsync()
    {
        await RefreshAccountStateAsync();
        await _applicationRulesControl.LoadAsync();
        await RefreshStartEligibilityAsync();

        if (GetVisibleProxyOrdersControl() is { } currentProxyOrdersControl)
        {
            await currentProxyOrdersControl.LoadAsync();
        }
        else if (GetVisibleProxyListControl() is { } currentProxyListControl)
        {
            await currentProxyListControl.LoadAsync();
        }
        else if (GetVisibleProductPurchaseControl() is { } currentProductPurchaseControl)
        {
            await currentProductPurchaseControl.LoadAsync();
        }
    }

    private ProxyOrdersControl? GetVisibleProxyOrdersControl() =>
        _contentPanel.Controls.Count > 0
            ? _contentPanel.Controls[0] as ProxyOrdersControl
            : null;

    private ProxyListControl? GetVisibleProxyListControl() =>
        _contentPanel.Controls.Count > 0
            ? _contentPanel.Controls[0] as ProxyListControl
            : null;

    private ProductPurchaseControl? GetVisibleProductPurchaseControl() =>
        _contentPanel.Controls.Count > 0
            ? _contentPanel.Controls[0] as ProductPurchaseControl
            : null;

    private async void HandlePurchaseCompleted(object? sender, EventArgs e)
    {
        _proxyOrderCacheService.Clear();
        await RefreshAccountStateAsync();

        if (sender is not ProductPurchaseControl purchaseControl)
        {
            return;
        }

        var (ordersControl, sidebarButton) = purchaseControl.SelectedKind switch
        {
            ProxyProductKind.Datacenter => (_datacenterProxyOrdersControl, _datacenterProxySidebarButton),
            ProxyProductKind.Rotate => (_rotatingProxyOrdersControl, _rotatingProxySidebarButton),
            _ => (_staticProxyOrdersControl, _staticProxySidebarButton)
        };

        await ShowOrderViewAsync(ordersControl, sidebarButton);
    }

    private async void HandleProxyRenewCompleted(object? sender, EventArgs e) =>
        await RefreshAccountStateAsync();

    private static string FormatCoin(long? coin) =>
        coin is null
            ? "Coin"
            : $"{coin.Value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} coin";

    private async Task OpenDepositModalAsync()
    {
        if (!await _authService.EnsureValidSessionAsync(CancellationToken.None) &&
            !await ShowLoginModalAsync())
        {
            return;
        }

        using var amountForm = _serviceProvider.GetRequiredService<DepositAmountForm>();
        if (amountForm.ShowDialog(this) != DialogResult.OK || amountForm.Transaction is null)
        {
            return;
        }

        using var qrForm = new DepositQrPaymentForm(_depositApiClient, amountForm.Transaction);
        qrForm.ShowDialog(this);
        if (qrForm.IsPaymentExpired)
        {
            UiFeedback.ShowInfo(this, "Mã QR đã hết hạn. Vui lòng tạo mã QR mới.");
            return;
        }

        if (!qrForm.IsPaymentCompleted)
        {
            return;
        }

        await RefreshAccountStateAsync();
        UiFeedback.ShowInfo(this, "Nạp tiền thành công.");
    }

    private void OpenPortal()
    {
        try
        {
            ExternalLinkLauncher.OpenInChromeOrDefault(_portalBaseUrl);
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể mở ProxyManager");
        }
    }

    private static string NormalizePortalBaseUrl(string value)
    {
        var baseUrl = string.IsNullOrWhiteSpace(value)
            ? "https://app.homeproxy.vn"
            : value.Trim();
        return baseUrl.TrimEnd('/') + "/";
    }

    private async Task RefreshStartEligibilityAsync()
    {
        var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
        var rules = (await _applicationRuleRepository.GetAllAsync(CancellationToken.None)).ToList();
        await ClearMissingProxyAssignmentsAsync(rules, proxies, reloadApplicationGrid: true);
        _hasStartableProxyRule = ApplicationRuleEligibility.HasAnyUsableEnabledRule(rules, proxies);
        RefreshSessionActionButton();
    }

    private async Task<int> ClearMissingProxyAssignmentsAsync(
        List<ApplicationRule> applications,
        IReadOnlyCollection<ProxyServer> proxies,
        bool reloadApplicationGrid)
    {
        var clearedCount =
            _applicationRuleService.ClearMissingProxyAssignments(applications, proxies) +
            _applicationRuleService.ClearExpiredProxyAssignments(applications, proxies, DateTimeOffset.Now);
        if (clearedCount == 0)
        {
            return 0;
        }

        await _applicationRuleRepository.SaveAllAsync(applications, CancellationToken.None);
        if (reloadApplicationGrid)
        {
            await _applicationRulesControl.LoadAsync();
        }

        return clearedCount;
    }

    private async Task LogoutAsync()
    {
        var answer = MessageBox.Show(
            this,
            "Bạn có chắc muốn đăng xuất không?",
            "Đăng xuất",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        _logoutButton.Enabled = false;
        if (_runtimeState == ProxifierRuntimeState.Running)
        {
            await ReloadProfileForLogoutAsync();
        }

        await _authService.LogoutAsync(CancellationToken.None);
        _proxyOrderCacheService.Clear();
        await RefreshAfterAuthScopeChangedAsync();
    }

    private async Task ReloadProfileForLogoutAsync()
    {
        try
        {
            _statusBarControl.SetStatus("Dang cap nhat profile Proxifier truoc khi dang xuat...");
            UpdateRuntimeState(ProxifierRuntimeState.Starting);

            var settings = await _settingsRepository.GetAsync(CancellationToken.None);
            if (!IsValidProxifierPath(settings.ProxifierExecutablePath))
            {
                if (!_proxifierService.TryDetectProxifierPath(out var detectedPath))
                {
                    UpdateRuntimeState(ProxifierRuntimeState.Error);
                    return;
                }

                settings.ProxifierExecutablePath = detectedPath!;
                await _settingsRepository.SaveAsync(settings, CancellationToken.None);
            }

            var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
            var applications = (await _applicationRuleRepository.GetAllAsync(CancellationToken.None))
                .Select(CloneApplicationRule)
                .ToList();
            var manualProxies = proxies.Where(proxy => !proxy.IsFromBackend).ToList();
            RemoveLogoutBackendAssignments(applications, manualProxies);

            var result = await _proxifierSessionService.StartAsync(settings, manualProxies, applications, CancellationToken.None);
            _lastProfileTextBox.Text = result.ProfilePath;
            RenderXmlPreview(result.Profile.MaskedPreviewXml);
            _commandResultLabel.Text = result.Message;
            UpdateRuntimeState(result.Success ? ProxifierRuntimeState.Running : ProxifierRuntimeState.Error);
            RefreshProxifierVisibilityButton(settings.ProxifierExecutablePath);
            _statusBarControl.SetStatus(result.Success
                ? "Da cap nhat profile Proxifier truoc khi dang xuat."
                : "Khong the cap nhat profile Proxifier truoc khi dang xuat.");
            await RunPostProfileLoadRestartFlowAsync(settings, applications, manualProxies, result.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _commandResultLabel.Text = ex.Message;
            UpdateRuntimeState(ProxifierRuntimeState.Error);
            _statusBarControl.SetStatus("Khong the cap nhat profile Proxifier truoc khi dang xuat.");
        }
    }

    private static void RemoveLogoutBackendAssignments(
        IReadOnlyList<ApplicationRule> applications,
        IReadOnlyCollection<ProxyServer> manualProxies)
    {
        var manualProxyIds = manualProxies.Select(proxy => proxy.Id).ToHashSet();
        foreach (var rule in applications.Where(rule => rule.AssignedProxyId is { } proxyId && !manualProxyIds.Contains(proxyId)))
        {
            rule.AssignedProxyId = null;
            rule.IsEnabled = false;
            rule.Warning = ApplicationRuleEligibility.MissingAssignedProxyMessage;
        }
    }

    private static ApplicationRule CloneApplicationRule(ApplicationRule rule) =>
        new()
        {
            TargetType = rule.TargetType,
            ExecutableName = rule.ExecutableName,
            ProcessId = rule.ProcessId,
            EmulatorKind = rule.EmulatorKind,
            EmulatorInstanceKey = rule.EmulatorInstanceKey,
            EmulatorInstanceName = rule.EmulatorInstanceName,
            RuntimeProcessName = rule.RuntimeProcessName,
            RuntimeExecutablePath = rule.RuntimeExecutablePath,
            AssignedProxyId = rule.AssignedProxyId,
            Warning = rule.Warning,
            IsEnabled = rule.IsEnabled,
            AutoAssignProxy = rule.AutoAssignProxy
        };

    private async void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exitCleanupCompleted)
        {
            return;
        }

        e.Cancel = true;
        if (_exitCleanupRunning)
        {
            return;
        }

        _exitCleanupRunning = true;
        try
        {
            await StopProxyBeforeExitAsync();
        }
        finally
        {
            _exitCleanupCompleted = true;
            _exitCleanupRunning = false;
            if (!IsDisposed)
            {
                BeginInvoke(new Action(Close));
            }
        }
    }

    private async Task DetectProxifierAsync()
    {
        if (_proxifierService.TryDetectProxifierPath(out var path))
        {
            _proxifierPathTextBox.Text = path;
            var settings = await _settingsRepository.GetAsync(CancellationToken.None);
            settings.ProxifierExecutablePath = path!;
            await _settingsRepository.SaveAsync(settings, CancellationToken.None);
            RefreshProxifierVisibilityButton(path);
            _statusBarControl.SetStatus("Đã phát hiện Proxifier.exe.");
            return;
        }

        MessageBox.Show(this, "Không tự dò thấy Proxifier.exe. Hãy cấu hình thủ công trong Cài đặt.", "Không tìm thấy", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BrowseProxifierExecutable()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Proxifier.exe|Proxifier.exe|Ứng dụng Windows (*.exe)|*.exe",
            Title = "Chọn Proxifier.exe"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _proxifierPathTextBox.Text = dialog.FileName;
        }
    }

    private async Task SaveProxifierPathAsync()
    {
        var settings = await _settingsRepository.GetAsync(CancellationToken.None);
        settings.ProxifierExecutablePath = _proxifierPathTextBox.Text.Trim();
        await _settingsRepository.SaveAsync(settings, CancellationToken.None);
        RefreshProxifierVisibilityButton(settings.ProxifierExecutablePath);
        _statusBarControl.SetStatus("Đã lưu đường dẫn Proxifier.exe.");
        UiFeedback.ShowInfo(this, "Đã lưu đường dẫn Proxifier.exe.");
    }

    private async Task<AppSettings?> InstallOrRepairProxifierAsync()
    {
        try
        {
            if (_sessionActionControl is not null)
            {
                _sessionActionControl.Enabled = false;
            }
            _statusBarControl.SetStatus("Đang cài đặt Proxifier...");
            var result = await _proxifierInstallerService.InstallOrRepairAsync(CancellationToken.None);
            _commandResultLabel.Text = result.Message;

            if (!result.Success || string.IsNullOrWhiteSpace(result.ProxifierExecutablePath))
            {
                _statusBarControl.SetStatus("Cài đặt Proxifier thất bại.");
                MessageBox.Show(this, result.Message, "Không thể cài đặt Proxifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            var settings = await _settingsRepository.GetAsync(CancellationToken.None);
            settings.ProxifierExecutablePath = result.ProxifierExecutablePath;
            await _settingsRepository.SaveAsync(settings, CancellationToken.None);
            _proxifierPathTextBox.Text = settings.ProxifierExecutablePath;
            ApplyProxifierEndUserDefaults();
            ApplyConfiguredProxifierRegistration();
            _statusBarControl.SetStatus("Đã cài đặt Proxifier.");
            return settings;
        }
        finally
        {
            RefreshSessionActionButton();
        }
    }

    private async Task<AppSettings?> EnsureProxifierReadyAsync(bool allowInstall)
    {
        var settings = await _settingsRepository.GetAsync(CancellationToken.None);
        if (IsValidProxifierPath(settings.ProxifierExecutablePath))
        {
            ApplyProxifierEndUserDefaults();
            ApplyConfiguredProxifierRegistration();
            return settings;
        }

        if (_proxifierService.TryDetectProxifierPath(out var detectedPath))
        {
            settings.ProxifierExecutablePath = detectedPath!;
            await _settingsRepository.SaveAsync(settings, CancellationToken.None);
            _proxifierPathTextBox.Text = settings.ProxifierExecutablePath;
            ApplyProxifierEndUserDefaults();
            ApplyConfiguredProxifierRegistration();
            _statusBarControl.SetStatus("Đã phát hiện Proxifier.exe.");
            return settings;
        }

        if (!allowInstall)
        {
            MessageBox.Show(this, "Hãy cài đặt hoặc cấu hình đúng đường dẫn Proxifier.exe trước.", "Thiếu Proxifier", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        return await InstallOrRepairProxifierAsync();
    }

    // Runs once on startup: if Proxifier is not present yet and a bundled installer is
    // available (embedded in the exe and materialized to %AppData%), install it silently
    // so the end user does not have to do anything. Stays quiet on any failure.
    private async Task TryAutoInstallProxifierSilentlyAsync()
    {
        try
        {
            var settings = await _settingsRepository.GetAsync(CancellationToken.None);
            if (IsValidProxifierPath(settings.ProxifierExecutablePath))
            {
                ApplyProxifierEndUserDefaults();
                ApplyConfiguredProxifierRegistration();
                return;
            }

            if (_proxifierService.TryDetectProxifierPath(out var detectedPath) &&
                !string.IsNullOrWhiteSpace(detectedPath))
            {
                settings.ProxifierExecutablePath = detectedPath!;
                await _settingsRepository.SaveAsync(settings, CancellationToken.None);
                _proxifierPathTextBox.Text = settings.ProxifierExecutablePath;
                RefreshProxifierVisibilityButton(settings.ProxifierExecutablePath);
                ApplyProxifierEndUserDefaults();
                ApplyConfiguredProxifierRegistration();
                return;
            }

            if (!_proxifierInstallerService.TryResolveBundledInstallerPath(out _))
            {
                return;
            }

            var result = await _proxifierInstallerService.InstallOrRepairAsync(CancellationToken.None);
            if (result.Success && !string.IsNullOrWhiteSpace(result.ProxifierExecutablePath))
            {
                settings.ProxifierExecutablePath = result.ProxifierExecutablePath;
                await _settingsRepository.SaveAsync(settings, CancellationToken.None);
                _proxifierPathTextBox.Text = settings.ProxifierExecutablePath;
                RefreshProxifierVisibilityButton(settings.ProxifierExecutablePath);
                ApplyProxifierEndUserDefaults();
                ApplyConfiguredProxifierRegistration();
                _statusBarControl.SetStatus("Đã cài đặt Proxifier.");
            }
        }
        catch
        {
            // Silent on first run; the user can still install from Settings later.
        }
    }

    private void ApplyConfiguredProxifierRegistration()
    {
        var state = _proxifierRegistrationService.GetRegistrationState();
        if (state.IsRegistered)
        {
            return;
        }

        try
        {
            if (!_proxifierRegistrationService.TrySaveConfiguredRegistration())
            {
                _commandResultLabel.Text = "Chưa cấu hình registration key trong appsettings.json.";
            }
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _commandResultLabel.Text = ex.Message;
        }
    }

    private void ApplyProxifierEndUserDefaults()
    {
        var result = _proxifierPreferenceService.ApplyEndUserDefaults();
        if (result.Warnings.Count > 0)
        {
            _commandResultLabel.Text = string.Join(Environment.NewLine, result.Warnings);
        }
    }

    private async Task StopProxyBeforeExitAsync()
    {
        if (_exitCleanupCompleted)
        {
            return;
        }

        if (_runtimeState != ProxifierRuntimeState.Running)
        {
            return;
        }

        try
        {
            _statusBarControl.SetStatus("Đang dừng proxy...");
            UpdateRuntimeState(ProxifierRuntimeState.Stopping);

            var settings = await _settingsRepository.GetAsync(CancellationToken.None);
            if (!IsValidProxifierPath(settings.ProxifierExecutablePath))
            {
                if (!_proxifierService.TryDetectProxifierPath(out var detectedPath))
                {
                    UpdateRuntimeState(ProxifierRuntimeState.Stopped);
                    return;
                }

                settings.ProxifierExecutablePath = detectedPath!;
                await _settingsRepository.SaveAsync(settings, CancellationToken.None);
            }

            var result = await _proxifierSessionService.StopAsync(settings, CancellationToken.None);
            _lastProfileTextBox.Text = result.ProfilePath;
            _commandResultLabel.Text = result.Message;
            UpdateRuntimeState(result.Success ? ProxifierRuntimeState.Stopped : ProxifierRuntimeState.Error);
            _statusBarControl.SetStatus(result.Success ? "Đã dừng proxy." : "Không thể dừng proxy.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _commandResultLabel.Text = ex.Message;
            UpdateRuntimeState(ProxifierRuntimeState.Error);
            _statusBarControl.SetStatus("Không thể dừng proxy.");
        }
    }

    private void SaveRegistrationKey()
    {
        try
        {
            _proxifierRegistrationService.SaveRegistrationKey(_registrationKeyTextBox.Text);
            _registrationKeyTextBox.Clear();
            _commandResultLabel.Text = "Đã lưu key.";
            _statusBarControl.SetStatus("Đã lưu key.");
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this, ex.Message, "Không thể lưu key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task GenerateProfileAsync()
    {
        try
        {
            var settings = await _settingsRepository.GetAsync(CancellationToken.None);
            var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
            var rules = (await _applicationRuleRepository.GetAllAsync(CancellationToken.None)).ToList();
            await RefreshEmulatorRuntimeTargetsAsync(rules);

            var result = _profileBuilder.Build(new ProxifierProfileModel
            {
                Proxies = proxies,
                Rules = rules,
                DefaultRouteDirect = settings.DefaultRouteDirect
            });

            Directory.CreateDirectory(settings.ProfileOutputDirectory);
            var profilePath = Path.Combine(settings.ProfileOutputDirectory, "active.ppx");
            await File.WriteAllTextAsync(profilePath, result.Xml, CancellationToken.None);
            _lastProfileTextBox.Text = profilePath;
            RenderXmlPreview(result.MaskedPreviewXml);
            _statusBarControl.SetStatus("Đã tạo profile Proxifier.");
            UiFeedback.ShowInfo(this, "Đã tạo profile Proxifier.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Không thể tạo profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task LoadProfileAsync()
    {
        var settings = await EnsureProxifierReadyAsync(allowInstall: true);
        if (settings is null)
        {
            return;
        }

        var profilePath = _lastProfileTextBox.Text;
        var result = await _proxifierService.LoadProfileAsync(settings.ProxifierExecutablePath, profilePath, CancellationToken.None);
        _commandResultLabel.Text = result.Message;
        _statusBarControl.SetStatus(result.Success ? "Đã gửi lệnh load profile." : "Load profile thất bại.");
        if (result.Success)
        {
            var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
            var applications = (await _applicationRuleRepository.GetAllAsync(CancellationToken.None)).ToList();
            await RunPostProfileLoadRestartFlowAsync(settings, applications, proxies, profileLoadSucceeded: true);
            UiFeedback.ShowInfo(this, "Đã gửi lệnh load profile vào Proxifier.");
        }
    }

    private async Task StartProxySessionAsync()
    {
        try
        {
            await RefreshStartEligibilityAsync();
            if (!_hasStartableProxyRule)
            {
                UiFeedback.ShowInfo(this, "Chưa có ứng dụng nào đủ điều kiện để bắt đầu dùng proxy.");
                return;
            }

            var settings = await EnsureProxifierReadyAsync(allowInstall: true);
            if (settings is null)
            {
                return;
            }

            UpdateRuntimeState(ProxifierRuntimeState.Starting);
            var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
            var applications = (await _applicationRuleRepository.GetAllAsync(CancellationToken.None)).ToList();
            await RefreshEmulatorRuntimeTargetsAsync(applications);
            proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
            applications = (await _applicationRuleRepository.GetAllAsync(CancellationToken.None)).ToList();
            await ClearMissingProxyAssignmentsAsync(applications, proxies, reloadApplicationGrid: true);
            _hasStartableProxyRule = ApplicationRuleEligibility.HasAnyUsableEnabledRule(applications, proxies);
            RefreshSessionActionButton();
            if (!_hasStartableProxyRule)
            {
                UpdateRuntimeState(ProxifierRuntimeState.Stopped);
                UiFeedback.ShowInfo(this, "Chưa có ứng dụng nào đủ điều kiện để bắt đầu dùng proxy.");
                return;
            }

            var result = await _proxifierSessionService.StartAsync(settings, proxies, applications, CancellationToken.None);

            _lastProfileTextBox.Text = result.ProfilePath;
            RenderXmlPreview(result.Profile.MaskedPreviewXml);
            _commandResultLabel.Text = result.Message;
            UpdateRuntimeState(result.Success ? ProxifierRuntimeState.Running : ProxifierRuntimeState.Error);
            RefreshProxifierVisibilityButton(settings.ProxifierExecutablePath);
            _statusBarControl.SetStatus(result.Success ? "Đang dùng proxy." : "Không thể bắt đầu dùng proxy.");
            await RunPostProfileLoadRestartFlowAsync(settings, applications, proxies, result.Success);

            UiFeedback.ShowInfo(this, result.Success ? "Đã bắt đầu dùng proxy." : result.Message);
        }
        catch (Exception ex)
        {
            UpdateRuntimeState(ProxifierRuntimeState.Error);
            MessageBox.Show(this, ex.Message, "Không thể bắt đầu dùng proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RunPostProfileLoadRestartFlowAsync(
        AppSettings settings,
        IReadOnlyList<ApplicationRule> applications,
        IReadOnlyList<ProxyServer> proxies,
        bool profileLoadSucceeded,
        ProfileAffectingChange? change = null)
    {
        if (!profileLoadSucceeded)
        {
            return;
        }

        var targetRuleIds = ProfileRestartScopeResolver.ResolveTargetRuleIds(change, applications);
        var restartCandidates = await GetRunningRestartableApplicationCandidatesAsync(applications, proxies, targetRuleIds);
        if (restartCandidates.Count == 0)
        {
            return;
        }

        var shouldRestartApplications = await ShouldRestartApplicationsAfterProfileLoadAsync(settings, restartCandidates);
        if (shouldRestartApplications)
        {
            await RestartApplicationsForProxyAsync(restartCandidates);
        }
    }

    private async Task<bool> ShouldRestartApplicationsAfterProfileLoadAsync(
        AppSettings settings,
        IReadOnlyList<ApplicationRestartCandidate> restartableApplications)
    {
        return settings.ProxyRestartConfirmationPreference == ProxyRestartConfirmationPreference.AutoRestart ||
            await ShowProxyRestartConfirmationAsync(settings, restartableApplications);
    }

    private async Task<IReadOnlyList<ApplicationRestartCandidate>> GetRunningRestartableApplicationCandidatesAsync(
        IReadOnlyList<ApplicationRule> applications,
        IReadOnlyList<ProxyServer> proxies,
        IReadOnlySet<Guid>? targetRuleIds = null)
    {
        var proxiesById = proxies.ToDictionary(proxy => proxy.Id);
        var candidates = applications
            .Where(rule => rule.TargetType == ApplicationTargetType.Executable && rule.IsEnabled)
            .Where(rule => targetRuleIds is null || targetRuleIds.Contains(rule.Id))
            .Where(rule => ApplicationRuleEligibility.Evaluate(rule, proxiesById).CanUseProxy)
            .ToList();
        var running = new List<ApplicationRestartCandidate>();
        foreach (var application in candidates)
        {
            if (await _applicationRuntimeController.IsRunningAsync(application, CancellationToken.None))
            {
                running.Add(new ApplicationRestartCandidate(
                    application,
                    application.GetApplicationName(),
                    GetApplicationRestartIcon(application)));
            }
        }

        return running;
    }

    private async Task<bool> ShowProxyRestartConfirmationAsync(
        AppSettings settings,
        IReadOnlyList<ApplicationRestartCandidate> applications)
    {
        using var form = new ProxyRestartConfirmationForm(applications);
        var shouldRestart = form.ShowDialog(this) == DialogResult.OK;
        if (form.DoNotAskAgain && shouldRestart)
        {
            settings.ProxyRestartConfirmationPreference = ProxyRestartConfirmationPreference.AutoRestart;
            _proxyRestartConfirmationPreference = settings.ProxyRestartConfirmationPreference;
            await _settingsRepository.SaveAsync(settings, CancellationToken.None);
            SetAutoRestartApplicationsCheckBox(settings.ProxyRestartConfirmationPreference == ProxyRestartConfirmationPreference.AutoRestart);
        }

        return shouldRestart;
    }

    private async Task RestartApplicationsForProxyAsync(IReadOnlyList<ApplicationRestartCandidate> applications)
    {
        var failed = 0;
        foreach (var application in applications)
        {
            var restartResult = await _applicationRuntimeController.RestartIfRunningAsync(application.Rule, CancellationToken.None);
            if (!restartResult.Success)
            {
                failed++;
            }
        }

        _statusBarControl.SetStatus(failed == 0
            ? "Đã load profile và restart các ứng dụng bị ảnh hưởng."
            : $"Đã load profile. {failed} ứng dụng không tự đóng được để restart.");
    }

    private static Image GetApplicationRestartIcon(ApplicationRule rule)
    {
        var path = rule.TargetType == ApplicationTargetType.Emulator
            ? rule.RuntimeExecutablePath
            : rule.ExecutableName;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(path);
                return icon?.ToBitmap() ?? SystemIcons.Application.ToBitmap();
            }
            catch
            {
                // Use fallback below.
            }
        }

        return SystemIcons.Application.ToBitmap();
    }

    private async Task SaveProxyRestartPreferenceAsync()
    {
        if (_isUpdatingProxyRestartPreferenceUi)
        {
            return;
        }

        var settings = await _settingsRepository.GetAsync(CancellationToken.None);
        settings.ProxyRestartConfirmationPreference = _autoRestartApplicationsCheckBox.Checked
            ? ProxyRestartConfirmationPreference.AutoRestart
            : ProxyRestartConfirmationPreference.Ask;
        _proxyRestartConfirmationPreference = settings.ProxyRestartConfirmationPreference;
        await _settingsRepository.SaveAsync(settings, CancellationToken.None);
        _statusBarControl.SetStatus("Đã lưu cài đặt restart ứng dụng.");
    }

    private async void AutoRestartApplicationsCheckBoxCheckedChanged(object? sender, EventArgs e) =>
        await SaveProxyRestartPreferenceAsync();

    private void SetAutoRestartApplicationsCheckBox(bool isChecked)
    {
        _isUpdatingProxyRestartPreferenceUi = true;
        try
        {
            _autoRestartApplicationsCheckBox.Checked = isChecked;
        }
        finally
        {
            _isUpdatingProxyRestartPreferenceUi = false;
        }
    }

    private async Task StopProxySessionAsync()
    {
        try
        {
            var settings = await EnsureProxifierReadyAsync(allowInstall: true);
            if (settings is null)
            {
                return;
            }

            UpdateRuntimeState(ProxifierRuntimeState.Stopping);
            var result = await _proxifierSessionService.StopAsync(settings, CancellationToken.None);

            _lastProfileTextBox.Text = result.ProfilePath;
            RenderXmlPreview(result.Profile.MaskedPreviewXml);
            _commandResultLabel.Text = result.Message;
            UpdateRuntimeState(result.Success ? ProxifierRuntimeState.Stopped : ProxifierRuntimeState.Error);
            RefreshProxifierVisibilityButton(settings.ProxifierExecutablePath);
            _statusBarControl.SetStatus(result.Success ? "Đã dừng dùng proxy." : "Không thể dừng dùng proxy.");
            UiFeedback.ShowInfo(this, result.Success ? "Đã dừng dùng proxy." : result.Message);
        }
        catch (Exception ex)
        {
            UpdateRuntimeState(ProxifierRuntimeState.Error);
            MessageBox.Show(this, ex.Message, "Không thể dừng dùng proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void HandleProfileAffectingChanged(object? sender, ProfileAffectingChange change)
    {
        await RefreshStartEligibilityAsync();
        await _profileAutoReloadCoordinator.RequestReloadAsync(change, CancellationToken.None);
    }

    private async Task ReloadActiveProfileOnceAsync(ProfileAffectingChange change)
    {
        try
        {
            var settings = await _settingsRepository.GetAsync(CancellationToken.None);
            if (!ValidateProxifierPath(settings))
            {
                UpdateRuntimeState(ProxifierRuntimeState.Error);
                return;
            }

            _statusBarControl.SetStatus($"Đang tự cập nhật profile Proxifier: {change.Reason}...");
            var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
            var applications = (await _applicationRuleRepository.GetAllAsync(CancellationToken.None)).ToList();
            await RefreshEmulatorRuntimeTargetsAsync(applications);
            applications = (await _applicationRuleRepository.GetAllAsync(CancellationToken.None)).ToList();
            await ClearMissingProxyAssignmentsAsync(applications, proxies, reloadApplicationGrid: true);
            var result = await _proxifierSessionService.StartAsync(settings, proxies, applications, CancellationToken.None);

            _lastProfileTextBox.Text = result.ProfilePath;
            RenderXmlPreview(result.Profile.MaskedPreviewXml);
            _commandResultLabel.Text = result.Message;
            UpdateRuntimeState(result.Success ? ProxifierRuntimeState.Running : ProxifierRuntimeState.Error);
            RefreshProxifierVisibilityButton(settings.ProxifierExecutablePath);
            _statusBarControl.SetStatus(result.Success
                ? $"Đã tự cập nhật profile Proxifier: {change.Reason}."
                : "Tự cập nhật profile Proxifier thất bại.");
            await RunPostProfileLoadRestartFlowAsync(settings, applications, proxies, result.Success, change);

            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "Không thể tự cập nhật profile Proxifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            UpdateRuntimeState(ProxifierRuntimeState.Error);
            MessageBox.Show(this, ex.Message, "Không thể tự cập nhật profile Proxifier", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RefreshEmulatorRuntimeTargetsAsync(IReadOnlyList<ApplicationRule> applications)
    {
        await _applicationRuleService.RefreshEmulatorRuntimeTargetsAsync(applications, CancellationToken.None);
        await _applicationRuleRepository.SaveAllAsync(applications, CancellationToken.None);
        await _applicationRulesControl.LoadAsync();
    }

    private bool ValidateProxifierPath(AppSettings settings)
    {
        if (IsValidProxifierPath(settings.ProxifierExecutablePath))
        {
            return true;
        }

        MessageBox.Show(this, "Hãy cài đặt hoặc cấu hình đúng đường dẫn Proxifier.exe trước.", "Thiếu Proxifier", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    private static bool IsValidProxifierPath(string? proxifierExePath) =>
        !string.IsNullOrWhiteSpace(proxifierExePath) && File.Exists(proxifierExePath);

    private void UpdateRuntimeState(ProxifierRuntimeState state)
    {
        _runtimeState = state;
        _runtimeStateLabel.Text = state switch
        {
            ProxifierRuntimeState.Starting => "Đang khởi động...",
            ProxifierRuntimeState.Running => "Đang dùng proxy",
            ProxifierRuntimeState.Stopping => "Đang dừng...",
            ProxifierRuntimeState.Error => "Có lỗi",
            _ => "Đã dừng"
        };
        _runtimeStateLabel.ForeColor = HeaderText;
        RefreshSessionActionButton();
    }

    private async Task HandleSessionActionAsync()
    {
        if (_runtimeState is ProxifierRuntimeState.Starting or ProxifierRuntimeState.Stopping)
        {
            return;
        }

        if (_runtimeState == ProxifierRuntimeState.Running)
        {
            await StopProxySessionAsync();
            return;
        }

        await StartProxySessionAsync();
    }

    private void RefreshSessionActionButton()
    {
        if (_sessionActionControl is null)
        {
            return;
        }

        _sessionActionControl.SetState(_runtimeState, _hasStartableProxyRule);
    }

    private void RefreshProxifierVisibilityButton(string? proxifierExePath = null)
    {
    }

    private void OpenProfileFolder()
    {
        var profilePath = _lastProfileTextBox.Text;
        var directory = string.IsNullOrWhiteSpace(profilePath) ? _paths.ProfilesDirectory : Path.GetDirectoryName(profilePath);
        if (directory is null)
        {
            return;
        }

        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }

    private void RenderXmlPreview(string xml)
    {
        _profilePreviewTextBox.Text = xml;
        _profilePreviewTextBox.SelectAll();
        _profilePreviewTextBox.SelectionColor = LightTheme.Foreground;

        ColorizeXmlPattern(@"<\?xml.*?\?>", LightTheme.Muted);
        ColorizeXmlPattern(@"</?[A-Za-z_][^>\s/]*", LightTheme.Accent);
        ColorizeXmlPattern(@"\b[A-Za-z_][\w:-]*(?=\=)", LightTheme.Success);
        ColorizeXmlPattern("\"[^\"]*\"", LightTheme.Warning);

        _profilePreviewTextBox.Select(0, 0);
    }

    private void InitializeComponent()
    {

    }

    private void ColorizeXmlPattern(string pattern, Color color)
    {
        foreach (Match match in Regex.Matches(_profilePreviewTextBox.Text, pattern))
        {
            _profilePreviewTextBox.Select(match.Index, match.Length);
            _profilePreviewTextBox.SelectionColor = color;
        }
    }

    private sealed class SidebarNavButton : Button
    {
        private const int Radius = 4;
        private const int IconTextGap = 12;
        private const int ExpandedLeftPadding = 12;
        private const int ExpandedRightPadding = 8;
        private const int IndicatorWidth = 4;
        private const int IndicatorHeight = 22;

        private bool _isHovering;
        private bool _isPressed;
        private bool _isSelected;
        private bool _isCollapsed;

        public SidebarNavButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            Font = new Font("Segoe UI", SidebarButtonFontSize, FontStyle.Regular);
        }

        public string IconFileName { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                Invalidate();
            }
        }

        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed == value)
                {
                    return;
                }

                _isCollapsed = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovering = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovering = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? SidebarBackground);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = AppButton.CreateRoundedRectangle(bounds, Radius))
            using (var fill = new SolidBrush(ResolveBackColor()))
            {
                e.Graphics.FillPath(fill, path);
            }

            if (IsSelected)
            {
                var indicatorBounds = new Rectangle(
                    0,
                    Math.Max(0, (Height - IndicatorHeight) / 2),
                    IndicatorWidth,
                    IndicatorHeight);
                using var indicatorPath = AppButton.CreateRoundedRectangle(indicatorBounds, 2);
                using var indicatorBrush = new SolidBrush(HeaderAccent);
                e.Graphics.FillPath(indicatorBrush, indicatorPath);
            }

            DrawContent(e.Graphics, bounds);
        }

        private Color ResolveBackColor()
        {
            if (!Enabled)
            {
                return Color.Transparent;
            }

            if (_isPressed)
            {
                return SidebarMenuPressedColor;
            }

            return IsSelected || _isHovering ? SidebarMenuHoverColor : Color.Transparent;
        }

        private void DrawContent(Graphics graphics, Rectangle bounds)
        {
            var active = Enabled && (IsSelected || _isHovering);
            var textColor = active ? SidebarSelectedTextColor : SidebarDefaultTextColor;
            var iconColor = active ? SidebarSelectedTextColor : SidebarDefaultIconColor;
            var icon = !string.IsNullOrWhiteSpace(IconFileName)
                ? SidebarIconRenderer.Load(IconFileName, iconColor, SidebarButtonIconSize)
                : null;

            if (IsCollapsed)
            {
                DrawCenteredIcon(graphics, bounds, icon);
                return;
            }

            var iconX = bounds.Left + ExpandedLeftPadding;
            if (icon is not null)
            {
                var iconY = bounds.Top + (bounds.Height - icon.Height) / 2;
                graphics.DrawImage(icon, iconX, iconY, icon.Width, icon.Height);
            }

            var textLeft = iconX + SidebarButtonIconSize + IconTextGap;
            var textBounds = new Rectangle(
                textLeft,
                bounds.Top,
                Math.Max(0, bounds.Right - textLeft - ExpandedRightPadding),
                bounds.Height);
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                textBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        private static void DrawCenteredIcon(Graphics graphics, Rectangle bounds, Image? icon)
        {
            if (icon is null)
            {
                return;
            }

            var iconX = bounds.Left + (bounds.Width - icon.Width) / 2;
            var iconY = bounds.Top + (bounds.Height - icon.Height) / 2;
            graphics.DrawImage(icon, iconX, iconY, icon.Width, icon.Height);
        }
    }

    private sealed class SessionProxyActionControl : Control
    {
        private const int Radius = 4;
        private const int IconSize = 16;
        private const int IconTextGap = 4;
        // Manual sizing: increase/decrease ButtonHeight for the "Bắt đầu dùng proxy" vertical padding.
        private const int ButtonHeight = 44;
        private const int HelperTextGap = 4;
        private const int HelperTextHeight = 30;
        private const int HorizontalPadding = 8;
        private const int PrimaryButtonWidth = 226;
        private const int StartHintMinWidth = 400;
        private const int RunningStopWidth = 38;
        private const int RunningGap = 6;
        private static readonly Color ReadyBackColor = Color.FromArgb(235, 235, 235);
        private static readonly Color ReadyTextColor = Color.FromArgb(204, 0, 0, 0);
        private static readonly Color DisabledColor = Color.FromArgb(189, 189, 189);
        private static readonly Color RunningColor = Color.FromArgb(14, 164, 0);
        private static readonly Color LoadingBackColor = Color.FromArgb(102, 14, 164, 0);
        private static readonly Color StopBackColor = Color.FromArgb(197, 15, 31);
        private static readonly Font HelperTextFont = new("Segoe UI", 10f, FontStyle.Regular);
        private static readonly Color HelperTextColor = Color.FromArgb(189, 189, 189);

        private ProxifierRuntimeState _runtimeState = ProxifierRuntimeState.Stopped;
        private bool _canStart;
        private HitArea _hoverArea;
        private HitArea _pressedArea;

        public SessionProxyActionControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
            true);

            AutoSize = true;
            MinimumSize = new Size(PrimaryButtonWidth, ButtonHeight);
            Size = MinimumSize;
            Margin = Padding.Empty;
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        }

        public event EventHandler? StartRequested;

        public event EventHandler? StopRequested;

        public void SetState(ProxifierRuntimeState runtimeState, bool canStart)
        {
            _runtimeState = runtimeState;
            _canStart = canStart;
            Enabled = true;
            MinimumSize = new Size(IsRunning ? GetRunningControlWidth() : PrimaryButtonWidth, ButtonHeight);
            Size = GetPreferredSize(Size.Empty);
            Cursor = ResolveCursor(PointToClient(Cursor.Position));
            Invalidate();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var preferredHeight = GetControlHeight();
            if (IsRunning)
            {
                return new Size(GetRunningControlWidth(), preferredHeight);
            }

            if (!ShowStartHint)
            {
                return new Size(PrimaryButtonWidth, preferredHeight);
            }

            var hintWidth = TextRenderer.MeasureText(GetStartHintText(), HelperTextFont, Size.Empty, TextFormatFlags.NoPadding).Width;
            return new Size(Math.Max(StartHintMinWidth, Math.Max(PrimaryButtonWidth, hintWidth)), preferredHeight);
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            var preferred = GetPreferredSize(Size.Empty);
            base.SetBoundsCore(x, y, Math.Max(width, preferred.Width), Math.Max(height, preferred.Height), specified);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = ResolveCursor(PointToClient(Cursor.Position));
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var nextArea = HitTest(e.Location);
            if (nextArea != _hoverArea)
            {
                _hoverArea = nextArea;
                Cursor = ResolveCursor(e.Location);
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverArea = HitArea.None;
            _pressedArea = HitArea.None;
            Cursor = ResolveCursor(Point.Empty);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressedArea = HitTest(e.Location);
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var releasedArea = HitTest(e.Location);
            var pressedArea = _pressedArea;
            _pressedArea = HitArea.None;
            Invalidate();

            if (pressedArea != releasedArea)
            {
                return;
            }

            if (releasedArea == HitArea.Primary && IsReady)
            {
                StartRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (releasedArea == HitArea.Stop && IsRunning)
            {
                StopRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? LightTheme.Background);

            if (IsRunning)
            {
                PaintRunning(e.Graphics);
                return;
            }

            PaintSingle(e.Graphics);
            PaintStartHint(e.Graphics);
        }

        private bool IsBusy => _runtimeState is ProxifierRuntimeState.Starting or ProxifierRuntimeState.Stopping;

        private bool IsRunning => _runtimeState == ProxifierRuntimeState.Running;

        private bool IsReady => Enabled && !IsBusy && !IsRunning && _canStart;

        private bool IsDisabled => !Enabled || (!IsReady && !IsBusy && !IsRunning);

        private bool ShowStartHint => !IsBusy && !IsRunning && !_canStart;

        private void PaintSingle(Graphics graphics)
        {
            var bounds = GetPrimaryButtonBounds();
            var text = GetPrimaryText();
            var icon = IsBusy
                ? LoadIcon("new-design/Loading.svg", RunningColor)
                : LoadIcon("new-design/Play.svg", IsDisabled ? DisabledColor : ReadyTextColor);
            var backColor = IsBusy
                ? LoadingBackColor
                : IsReady
                    ? ReadyBackColor
                    : Color.Transparent;
            var foreColor = IsBusy
                ? RunningColor
                : IsReady
                    ? ReadyTextColor
                    : DisabledColor;
            var borderColor = IsDisabled ? DisabledColor : Color.Transparent;

            using var path = AppButton.CreateRoundedRectangle(bounds, Radius);
            using var background = new SolidBrush(backColor);
            using var border = new Pen(borderColor, IsDisabled ? 1 : 0);
            graphics.FillPath(background, path);
            if (IsDisabled)
            {
                graphics.DrawPath(border, path);
            }

            if (IsReady)
            {
                using var shadow = new Pen(Color.FromArgb(45, 0, 0, 0), 1);
                using var inset = new Pen(Color.FromArgb(190, 255, 255, 255), 1);
                graphics.DrawLine(shadow, bounds.Left + 1, bounds.Bottom, bounds.Right - 1, bounds.Bottom);
                graphics.DrawLine(inset, bounds.Left + 1, bounds.Top + 1, bounds.Right - 1, bounds.Top + 1);
            }

            DrawButtonContent(graphics, bounds, text, icon, foreColor);
        }

        private void PaintStartHint(Graphics graphics)
        {
            if (!ShowStartHint)
            {
                return;
            }

            var textBounds = new Rectangle(
                0,
                ButtonHeight + HelperTextGap,
                Math.Max(1, Width),
                HelperTextHeight);
            TextRenderer.DrawText(
                graphics,
                GetStartHintText(),
                HelperTextFont,
                textBounds,
                HelperTextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void PaintRunning(Graphics graphics)
        {
            var primaryBounds = GetPrimaryButtonBounds();
            var stopBounds = new Rectangle(PrimaryButtonWidth + RunningGap, 0, RunningStopWidth - 1, ButtonHeight - 1);

            using (var primaryPath = AppButton.CreateRoundedRectangle(primaryBounds, Radius))
            using (var primaryBackground = new SolidBrush(RunningColor))
            {
                graphics.FillPath(primaryBackground, primaryPath);
            }

            using (var stopPath = AppButton.CreateRoundedRectangle(stopBounds, Radius))
            using (var stopBackground = new SolidBrush(StopBackColor))
            {
                graphics.FillPath(stopBackground, stopPath);
            }

            DrawButtonContent(
                graphics,
                primaryBounds,
                GetPrimaryText(),
                LoadIcon("new-design/Play.svg", Color.White),
                Color.White);
            DrawCenteredImage(graphics, stopBounds, LoadIcon("new-design/pause.svg", Color.White));
        }

        private void DrawButtonContent(Graphics graphics, Rectangle bounds, string text, Image? icon, Color foreColor)
        {
            var textSize = TextRenderer.MeasureText(text, Font, Size.Empty, TextFormatFlags.NoPadding);
            var iconWidth = icon?.Width ?? 0;
            var totalWidth = iconWidth + (iconWidth > 0 ? IconTextGap : 0) + textSize.Width;
            var left = bounds.Left + HorizontalPadding + Math.Max(0, (bounds.Width - (HorizontalPadding * 2) - totalWidth) / 2);

            if (icon is not null)
            {
                var iconY = bounds.Top + (bounds.Height - icon.Height) / 2;
                graphics.DrawImage(icon, left, iconY, icon.Width, icon.Height);
                left += icon.Width + IconTextGap;
            }

            var textBounds = new Rectangle(
                left,
                bounds.Top,
                Math.Max(0, bounds.Right - left - HorizontalPadding),
                bounds.Height);
            TextRenderer.DrawText(
                graphics,
                text,
                Font,
                textBounds,
                foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawCenteredImage(Graphics graphics, Rectangle bounds, Image? icon)
        {
            if (icon is null)
            {
                return;
            }

            var x = bounds.Left + (bounds.Width - icon.Width) / 2;
            var y = bounds.Top + (bounds.Height - icon.Height) / 2;
            graphics.DrawImage(icon, x, y, icon.Width, icon.Height);
        }

        private HitArea HitTest(Point point)
        {
            if (!ClientRectangle.Contains(point) || point.Y >= ButtonHeight || IsBusy || IsDisabled)
            {
                return HitArea.None;
            }

            if (!IsRunning)
            {
                return GetPrimaryButtonBounds().Contains(point) ? HitArea.Primary : HitArea.None;
            }

            var stopBounds = new Rectangle(PrimaryButtonWidth + RunningGap, 0, RunningStopWidth, ButtonHeight);
            if (stopBounds.Contains(point))
            {
                return HitArea.Stop;
            }

            return GetPrimaryButtonBounds().Contains(point) ? HitArea.Primary : HitArea.None;
        }

        private Cursor ResolveCursor(Point point)
        {
            var area = HitTest(point);
            return area is HitArea.Primary && IsReady || area is HitArea.Stop && IsRunning
                ? Cursors.Hand
                : Cursors.Default;
        }

        private static int GetRunningControlWidth() => PrimaryButtonWidth + RunningGap + RunningStopWidth;

        private static Rectangle GetPrimaryButtonBounds() => new(0, 0, PrimaryButtonWidth - 1, ButtonHeight - 1);

        private static int GetControlHeight() => ButtonHeight + HelperTextGap + HelperTextHeight;

        private string GetPrimaryText() =>
            _runtimeState switch
            {
                ProxifierRuntimeState.Starting => "Đang dùng proxy...",
                ProxifierRuntimeState.Stopping => "Đang dừng proxy...",
                ProxifierRuntimeState.Running => "Đang dùng proxy",
                _ => "Bắt đầu dùng proxy"
            };

        private static string GetStartHintText() => "Chọn ít nhất một ứng dụng để bắt đầu";

        private static Bitmap? LoadIcon(string fileName, Color color) =>
            SidebarIconRenderer.Load(fileName, color, IconSize);

        private enum HitArea
        {
            None,
            Primary,
            Stop
        }
    }

    private sealed class HeaderDivider : Control
    {
        public HeaderDivider()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            Height = 1;
            MinimumSize = new Size(0, 1);
            Margin = Padding.Empty;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var pen = new Pen(HeaderBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }
    }

}
