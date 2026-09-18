using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Forms;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.App.Controls;

public sealed class ProductPurchaseControl : UserControl
{
    private const int MinimumProxyPasswordLength = 9;
    private const string ProductsTabKey = "products";
    private const string PurchaseHistoryTabKey = "purchase-history";
    private const string TransactionHistoryTabKey = "transaction-history";
    private const int ProductCardContentHeight = 150;
    private const int ProductCardsFramePadding = 20;
    private const int DescriptionRowHeight = 42;
    private const int DescriptionIconSize = 22;
    private const int DescriptionCardPadding = 12;
    private const int DescriptionLabelColumnWidth = 150;
    private const int DescriptionTotalRowHeight = 30;
    private const int DescriptionProviderLogoWidth = 56;
    private const int DescriptionProviderLogoHeight = 26;
    private const int DescriptionFlagWidth = 38;
    private const int DescriptionFlagHeight = 28;
    private const int PurchaseButtonHeight = 42;
    private const int ComboItemImageHeight = 22;
    private const int ComboMaxItemImageWidth = 46;
    private const int ComboItemHorizontalPadding = 8;
    private const int ComboItemImageGap = 4;
    private const float DescriptionFontSize = 10.5f;

    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Color PurchaseAccent = Color.FromArgb(15, 108, 189);
    private static readonly Color PurchasePanelNeutral = Color.FromArgb(250, 250, 250);
    private static readonly Color DescriptionCardBackColor = Color.White;
    private static readonly Color DescriptionPanelBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color DescriptionRowBorder = Color.FromArgb(196, 196, 196);

    private readonly IProxyOrderApiClient _proxyOrderApiClient;
    private readonly IProxyOrderCacheService _proxyOrderCacheService;
    private readonly IAuthService _authService;

    private readonly AppUnderlineTabs _tabs = new();
    private readonly Panel _tabContentHost = new();
    private readonly TableLayoutPanel _productsPage = new();
    private readonly TableLayoutPanel _loadingPanel = new();
    private readonly Label _statusLabel = new();
    private readonly AppButton _refreshButton = new();
    private readonly HistoryListControl _purchaseHistoryControl;
    private readonly HistoryListControl _transactionHistoryControl;
    private readonly ProductKindCard _staticCard = new(ProxyProductKind.Static, "Proxy Tĩnh", "new-design/proxy-tinh-product-card.svg");
    private readonly ProductKindCard _datacenterCard = new(ProxyProductKind.Datacenter, "Proxy Datacenter", "new-design/proxy-datacenter-product-card.svg");
    private readonly ProductKindCard _rotateCard = new(ProxyProductKind.Rotate, "Proxy Xoay", "new-design/proxy-xoay-product-card.svg");
    private readonly Panel _formHost = new();
    private readonly Panel _descriptionHost = new();
    private readonly Label _configTitleLabel = new();
    private readonly Label _totalLabel = new();
    private readonly PurchaseButton _buyButton = new();

    private readonly ComboBox _productCombo = new();
    private readonly ComboBox _protocolCombo = new();
    private readonly ComboBox _locationCombo = CreateReadonlyCombo("random");
    private readonly ComboBox _ipStartCombo = CreateReadonlyCombo("random");
    private readonly Image? _cmcFlagIcon = SidebarIconRenderer.LoadOriginal("new-design/cmc-datacemter-flag.svg", 22);
    private readonly Image? _usFlagIcon = SidebarIconRenderer.LoadOriginal("new-design/us-datacenter-flag.svg", 22);
    private readonly Image? _fptProviderIcon = LoadProductImage("fpt.png");
    private readonly Image? _viettelProviderIcon = LoadProductImage("viettel.png");
    private readonly Image? _vnptProviderIcon = LoadProductImage("vnpt.png");
    private readonly NumericUpDown _durationInput = CreateNumericInput(1, 10000);
    private readonly NumericUpDown _quantityInput = CreateNumericInput(1, 10000);
    private readonly NumericUpDown _rotateIntervalInput = CreateNumericInput(0, 10000);
    private readonly AppTextInput _usernameInput = new();
    private readonly AppTextInput _passwordInput = new();
    private readonly CheckBox _autoRotateCheckBox = new();

    private readonly Dictionary<ProxyProductKind, List<ProxyProduct>> _productsByKind = new()
    {
        [ProxyProductKind.Static] = [],
        [ProxyProductKind.Datacenter] = [],
        [ProxyProductKind.Rotate] = []
    };

    private ProxyProductKind _selectedKind = ProxyProductKind.Static;
    private ProxyProductKind _pendingKind = ProxyProductKind.Static;
    private ProxyDiscountRules _discountRules = new();
    private bool _isBusy;
    private bool _isLoadingProducts;
    private bool _suppressInputEvents;
    private Func<Task<bool>>? _loginRequestedAsync;

    public ProductPurchaseControl(
        IProxyOrderApiClient proxyOrderApiClient,
        IProxyOrderCacheService proxyOrderCacheService,
        IAuthService authService)
    {
        _proxyOrderApiClient = proxyOrderApiClient;
        _proxyOrderCacheService = proxyOrderCacheService;
        _authService = authService;
        _purchaseHistoryControl = new HistoryListControl(_proxyOrderApiClient, _authService, HistoryListKind.Purchase);
        _transactionHistoryControl = new HistoryListControl(_proxyOrderApiClient, _authService, HistoryListKind.Transaction);

        Dock = DockStyle.Fill;
        BuildUi();
        ConfigureInputEvents();
        LightTheme.Apply(this);
        RestoreCustomColors(this);
    }

    public Func<Task<bool>>? LoginRequestedAsync
    {
        get => _loginRequestedAsync;
        set
        {
            _loginRequestedAsync = value;
            _purchaseHistoryControl.LoginRequestedAsync = value;
            _transactionHistoryControl.LoginRequestedAsync = value;
        }
    }

    public Func<Task>? DepositRequestedAsync { get; set; }

    public event EventHandler? PurchaseCompleted;

    public ProxyProductKind SelectedKind => _selectedKind;

    public void Configure(ProxyProductKind kind)
    {
        _pendingKind = kind;
        _tabs.SelectedKey = ProductsTabKey;
        ShowProductsTab();
        SelectKind(kind);
    }

    public Task LoadAsync() => LoadActiveTabAsync(forceRefresh: false);

    private Task LoadActiveTabAsync(bool forceRefresh) =>
        _tabs.SelectedKey switch
        {
            PurchaseHistoryTabKey => _purchaseHistoryControl.LoadAsync(forceRefresh),
            TransactionHistoryTabKey => _transactionHistoryControl.LoadAsync(forceRefresh),
            _ => LoadProductsAsync(forceRefresh)
        };

    private async Task LoadProductsAsync(bool forceRefresh)
    {
        if (_isLoadingProducts)
        {
            return;
        }

        SetProductLoading(true);
        try
        {
            if (forceRefresh)
            {
                _proxyOrderCacheService.InvalidateProducts();
            }

            var categoryOneTask = _proxyOrderCacheService.GetProductsAsync(1, forceRefresh, CancellationToken.None);
            var categoryTwoTask = _proxyOrderCacheService.GetProductsAsync(2, forceRefresh, CancellationToken.None);
            var discountsTask = _proxyOrderCacheService.GetDiscountRulesAsync(forceRefresh, CancellationToken.None);
            await Task.WhenAll(categoryOneTask, categoryTwoTask, discountsTask);

            _discountRules = discountsTask.Result;
            foreach (var key in _productsByKind.Keys.ToList())
            {
                _productsByKind[key].Clear();
            }

            foreach (var product in categoryOneTask.Result.Products.Concat(categoryTwoTask.Result.Products))
            {
                if (product.Kind == ProxyProductKind.Static && !IsAllowedStaticProvider(product.Provider))
                {
                    continue;
                }

                if (_productsByKind.TryGetValue(product.Kind, out var group))
                {
                    group.Add(product);
                }
            }

            foreach (var products in _productsByKind.Values)
            {
                products.Sort(CompareProducts);
            }

            UpdateKindCards();
            SelectKind(_pendingKind);
            _statusLabel.Text = _productsByKind.Values.Sum(products => products.Count) == 0
                ? "Chưa có sản phẩm phù hợp."
                : string.Empty;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Không thể tải danh sách sản phẩm.";
            UiFeedback.ShowWarning(this, ex, "Không thể tải sản phẩm proxy");
            UpdateKindCards();
            RebuildSelectedKindUi();
        }
        finally
        {
            SetProductLoading(false);
        }
    }

    private void BuildUi()
    {
        BackColor = LightTheme.Background;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = LightTheme.Background,
            Padding = new Padding(18, 12, 18, 18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _tabs.SetTabs(
        [
            new AppUnderlineTab(ProductsTabKey, "Sản phẩm"),
            new AppUnderlineTab(PurchaseHistoryTabKey, "Lịch sử mua hàng"),
            new AppUnderlineTab(TransactionHistoryTabKey, "Lịch sử giao dịch")
        ]);
        _tabs.Dock = DockStyle.Fill;
        _tabs.SelectedTabChanged += async (_, key) =>
        {
            if (key == ProductsTabKey)
            {
                ShowProductsTab();
                return;
            }

            await ShowHistoryTabAsync(key);
        };

        _refreshButton.Text = "Làm mới";
        _refreshButton.Image = UiIcons.Refresh;
        _refreshButton.Variant = AppButtonVariant.Secondary;
        _refreshButton.AccentColor = PurchaseAccent;
        _refreshButton.Filled = false;
        _refreshButton.AutoSize = true;
        _refreshButton.Anchor = AnchorStyles.Right;
        _refreshButton.Margin = new Padding(12, 3, 0, 0);
        _refreshButton.Click += async (_, _) => await LoadActiveTabAsync(forceRefresh: true);

        var tabRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = LightTheme.Background,
            Margin = Padding.Empty
        };
        tabRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tabRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tabRow.Controls.Add(_tabs, 0, 0);
        tabRow.Controls.Add(_refreshButton, 1, 0);

        _tabContentHost.Dock = DockStyle.Fill;
        _tabContentHost.BackColor = LightTheme.Background;
        BuildProductsPage();

        root.Controls.Add(tabRow, 0, 0);
        root.Controls.Add(_tabContentHost, 0, 1);
        Controls.Add(root);

        ShowProductsTab();
    }

    private void BuildProductsPage()
    {
        _productsPage.Dock = DockStyle.Fill;
        _productsPage.ColumnCount = 1;
        _productsPage.RowCount = 3;
        _productsPage.BackColor = LightTheme.Background;
        _productsPage.Padding = new Padding(0, 8, 0, 0);
        _productsPage.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _productsPage.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _productsPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = LightTheme.Muted;
        _statusLabel.Anchor = AnchorStyles.Left;
        _statusLabel.Margin = Padding.Empty;
        _productsPage.Controls.Add(_statusLabel, 0, 0);

        var mainContent = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = LightTheme.Background,
            Margin = Padding.Empty
        };
        mainContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        mainContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = LightTheme.Background,
            Margin = new Padding(0, 0, 14, 0)
        };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var cardsFrame = new Panel
        {
            Dock = DockStyle.Top,
            Height = ProductCardContentHeight + (ProductCardsFramePadding * 2),
            BackColor = PurchasePanelNeutral,
            Padding = new Padding(ProductCardsFramePadding),
            Margin = new Padding(0, 0, 0, 14),
            Tag = new PurchaseBackColor(PurchasePanelNeutral)
        };
        cardsFrame.Paint += (_, e) => DrawBorder(e.Graphics, cardsFrame.ClientRectangle, DescriptionPanelBorder, 4);

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = PurchasePanelNeutral
        };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cards.Controls.Add(_staticCard, 0, 0);
        cards.Controls.Add(_datacenterCard, 1, 0);
        cards.Controls.Add(_rotateCard, 2, 0);
        cardsFrame.Controls.Add(cards);
        left.Controls.Add(cardsFrame, 0, 0);

        _staticCard.Clicked += (_, _) => SelectKind(ProxyProductKind.Static);
        _datacenterCard.Clicked += (_, _) => SelectKind(ProxyProductKind.Datacenter);
        _rotateCard.Clicked += (_, _) => SelectKind(ProxyProductKind.Rotate);

        _configTitleLabel.AutoSize = true;
        _configTitleLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        _configTitleLabel.ForeColor = LightTheme.Foreground;
        _configTitleLabel.Margin = new Padding(0, 0, 0, 8);
        left.Controls.Add(_configTitleLabel, 0, 1);

        _formHost.Dock = DockStyle.Top;
        _formHost.AutoSize = true;
        _formHost.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _formHost.BackColor = PurchasePanelNeutral;
        _formHost.Padding = new Padding(12);
        _formHost.Tag = new PurchaseBackColor(PurchasePanelNeutral);
        _formHost.Paint += (_, e) => DrawBorder(e.Graphics, _formHost.ClientRectangle, DescriptionPanelBorder, 4);
        left.Controls.Add(_formHost, 0, 2);

        _descriptionHost.Dock = DockStyle.Fill;
        _descriptionHost.MinimumSize = new Size(320, 0);
        _descriptionHost.BackColor = PurchasePanelNeutral;
        _descriptionHost.Padding = new Padding(12);
        _descriptionHost.Margin = new Padding(0, 0, 0, 0);
        _descriptionHost.Tag = new PurchaseBackColor(PurchasePanelNeutral);
        _descriptionHost.Paint += (_, e) => DrawBorder(e.Graphics, _descriptionHost.ClientRectangle, DescriptionPanelBorder, 4);

        mainContent.Controls.Add(left, 0, 0);
        mainContent.Controls.Add(_descriptionHost, 1, 0);
        _productsPage.Controls.Add(mainContent, 0, 1);

        BuildLoadingPanel();
    }

    private void BuildLoadingPanel()
    {
        _loadingPanel.Dock = DockStyle.Fill;
        _loadingPanel.BackColor = LightTheme.Background;
        _loadingPanel.Visible = false;
        _loadingPanel.ColumnCount = 3;
        _loadingPanel.RowCount = 3;
        _loadingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _loadingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _loadingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _loadingPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _loadingPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _loadingPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var content = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = LightTheme.Background
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.Controls.Add(new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 24,
            Width = 180,
            Height = 12,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);
        content.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Đang tải...",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = LightTheme.Muted,
            BackColor = LightTheme.Background,
            Anchor = AnchorStyles.None
        }, 0, 1);
        _loadingPanel.Controls.Add(content, 1, 1);
    }

    private void ShowProductsTab()
    {
        _tabContentHost.Controls.Clear();
        if (_isLoadingProducts)
        {
            _tabContentHost.Controls.Add(_loadingPanel);
        }
        else
        {
            _tabContentHost.Controls.Add(_productsPage);
        }
    }

    private async Task ShowHistoryTabAsync(string key)
    {
        _tabContentHost.Controls.Clear();
        var control = key == PurchaseHistoryTabKey ? _purchaseHistoryControl : _transactionHistoryControl;
        _tabContentHost.Controls.Add(control);
        await control.LoadAsync();
    }

    private void ShowPendingTab()
    {
        _tabContentHost.Controls.Clear();
        _tabContentHost.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Pending",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12f, FontStyle.Regular),
            ForeColor = LightTheme.Muted,
            BackColor = LightTheme.Background
        });
    }

    private void ConfigureInputEvents()
    {
        _productCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _productCombo.DisplayMember = nameof(ProductOption.DisplayName);
        _productCombo.DrawMode = DrawMode.OwnerDrawFixed;
        _productCombo.ItemHeight = Math.Max(_productCombo.ItemHeight, LightTheme.InputHeight - 4);
        _productCombo.DrawItem += DrawProductComboItem;
        _productCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressInputEvents)
            {
                return;
            }

            ApplySelectedProductDefaults();
            RebuildDescription();
            UpdateTotal();
        };

        _protocolCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _protocolCombo.DisplayMember = nameof(ProxyProtocolOption.DisplayName);
        _protocolCombo.Items.AddRange(ProxyProtocolDisplay.Options.Cast<object>().ToArray());
        var httpIndex = ProxyProtocolDisplay.Options
            .ToList()
            .FindIndex(option => option.Value == ProxyProtocol.Https);
        _protocolCombo.SelectedIndex = httpIndex >= 0 ? httpIndex : 0;

        NumericInputGuard.AllowDigitsOnly(_durationInput);
        NumericInputGuard.AllowDigitsOnly(_quantityInput);
        NumericInputGuard.AllowDigitsOnly(_rotateIntervalInput);
        _durationInput.ValueChanged += (_, _) => UpdateTotal();
        _quantityInput.ValueChanged += (_, _) => UpdateTotal();

        _usernameInput.PlaceholderText = "random";
        _passwordInput.PlaceholderText = "random";
        _passwordInput.ErrorMessageHeight = 34;
        _passwordInput.UseSystemPasswordChar = true;
        _passwordInput.ShowPasswordToggle = true;

        _autoRotateCheckBox.AutoSize = true;
        _autoRotateCheckBox.Text = string.Empty;
        _autoRotateCheckBox.CheckedChanged += (_, _) =>
        {
            _rotateIntervalInput.Enabled = _autoRotateCheckBox.Checked && !_isBusy;
            if (!_autoRotateCheckBox.Checked)
            {
                _rotateIntervalInput.Value = 0;
            }
        };

        _buyButton.Text = "Mua hàng";
        _buyButton.Height = PurchaseButtonHeight;
        _buyButton.Click += async (_, _) => await PurchaseAsync();
    }

    private void SelectKind(ProxyProductKind kind)
    {
        var isKindChanged = _selectedKind != kind;
        _pendingKind = kind;
        _selectedKind = kind;
        _staticCard.IsSelected = kind == ProxyProductKind.Static;
        _datacenterCard.IsSelected = kind == ProxyProductKind.Datacenter;
        _rotateCard.IsSelected = kind == ProxyProductKind.Rotate;
        if (isKindChanged)
        {
            ResetSensitiveInputs();
        }

        RebuildSelectedKindUi();
    }

    private void ResetSensitiveInputs()
    {
        _passwordInput.Clear();
        _passwordInput.ClearError();
        _usernameInput.ClearError();
    }

    private void RebuildSelectedKindUi()
    {
        BindProductOptions();
        BuildConfigForm();
        ApplySelectedProductDefaults();
        RebuildDescription();
        UpdateTotal();
        RestoreCustomColors(this);
        SetBusy(_isBusy);
    }

    private void BindProductOptions()
    {
        _suppressInputEvents = true;
        try
        {
            var options = _productsByKind[_selectedKind]
                .Select(product => new ProductOption(BuildProductDisplayName(product), product, GetProductOptionImages(product)))
                .ToList();
            _productCombo.BeginUpdate();
            try
            {
                _productCombo.DataSource = null;
                _productCombo.Items.Clear();
                _productCombo.Items.AddRange(options.Cast<object>().ToArray());
                _productCombo.SelectedIndex = _productCombo.Items.Count > 0 ? 0 : -1;
            }
            finally
            {
                _productCombo.EndUpdate();
            }
        }
        finally
        {
            _suppressInputEvents = false;
        }
    }

    private void BuildConfigForm()
    {
        _formHost.SuspendLayout();
        try
        {
            _formHost.Controls.Clear();
            _configTitleLabel.Text = $"Chọn cấu hình {GetKindDisplayName(_selectedKind)}";

            var isRotate = _selectedKind == ProxyProductKind.Rotate;
            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = isRotate ? 3 : 3,
                BackColor = PurchasePanelNeutral,
                Margin = Padding.Empty
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            for (var row = 0; row < fields.RowCount; row++)
            {
                fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            if (isRotate)
            {
                fields.Controls.Add(CreateField("Chọn gói sử dụng", _productCombo), 0, 0);
                fields.SetColumnSpan(_productCombo.Parent!, 1);
                fields.Controls.Add(CreateCheckField("Tự động xoay", _autoRotateCheckBox), 1, 0);
                fields.Controls.Add(CreateField("Thời gian xoay", _rotateIntervalInput), 2, 0);
                fields.Controls.Add(CreateField(GetDurationLabel(), _durationInput), 0, 1);
                fields.Controls.Add(CreateField("Số lượng", _quantityInput), 1, 1);
                fields.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = PurchasePanelNeutral }, 2, 1);
                fields.Controls.Add(CreateField("Mật khẩu", _passwordInput), 0, 2);
                fields.Controls.Add(CreateField("Tài khoản", _usernameInput), 1, 2);
                fields.SetColumnSpan(_passwordInput.Parent!, 1);
            }
            else
            {
                var productLabel = _selectedKind == ProxyProductKind.Datacenter ? "Loại datacenter" : "Nhà mạng";
                fields.Controls.Add(CreateField("Giao thức", _protocolCombo), 0, 0);
                fields.Controls.Add(CreateField(productLabel, _productCombo), 1, 0);
                fields.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = PurchasePanelNeutral }, 2, 0);
                fields.Controls.Add(CreateField("Địa điểm", _locationCombo), 0, 1);
                fields.Controls.Add(CreateField(GetDurationLabel(), _durationInput), 1, 1);
                fields.Controls.Add(CreateField("Số lượng", _quantityInput), 2, 1);
                fields.Controls.Add(CreateField("Đầu IP", _ipStartCombo), 0, 2);
                fields.Controls.Add(CreateField("Tài khoản", _usernameInput), 1, 2);
                fields.Controls.Add(CreateField("Mật khẩu", _passwordInput), 2, 2);
            }

            _formHost.Controls.Add(fields);
        }
        finally
        {
            _formHost.ResumeLayout(true);
            _formHost.PerformLayout();
            SyncConfigComboLayouts();
        }
    }

    private void SyncConfigComboLayouts()
    {
        _productCombo.Invalidate();
        _protocolCombo.Invalidate();
        _locationCombo.Invalidate();
        _ipStartCombo.Invalidate();
    }

    private void ApplySelectedProductDefaults()
    {
        _suppressInputEvents = true;
        try
        {
            var selected = GetSelectedProduct();
            var period = selected is null ? 1 : Math.Max(1, ParsePositiveInt(selected.Category.Period, 1));
            _durationInput.Value = Math.Clamp(period, (int)_durationInput.Minimum, (int)_durationInput.Maximum);
            if (_quantityInput.Value < 1)
            {
                _quantityInput.Value = 1;
            }

            var isRotate = _selectedKind == ProxyProductKind.Rotate;
            _usernameInput.Text = isRotate ? "random" : string.Empty;
            _usernameInput.ReadOnly = isRotate;
            _rotateIntervalInput.Enabled = isRotate && _autoRotateCheckBox.Checked && !_isBusy;
        }
        finally
        {
            _suppressInputEvents = false;
        }

        BuildConfigForm();
    }

    private void RebuildDescription()
    {
        _descriptionHost.SuspendLayout();
        try
        {
            _descriptionHost.Controls.Clear();
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = PurchasePanelNeutral,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                Tag = new PurchaseBackColor(PurchasePanelNeutral)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = PurchasePanelNeutral,
                Margin = Padding.Empty,
                Tag = new PurchaseBackColor(PurchasePanelNeutral)
            };
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            content.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Mô tả sản phẩm",
                Font = new Font("Segoe UI", 12f, FontStyle.Regular),
                ForeColor = LightTheme.Foreground,
                BackColor = PurchasePanelNeutral,
                Margin = new Padding(0, 0, 0, 8)
            }, 0, 0);

            var detailsCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = GetDescriptionCardHeight(),
                BackColor = DescriptionCardBackColor,
                Padding = new Padding(DescriptionCardPadding),
                Margin = Padding.Empty,
                Tag = new PurchaseBackColor(DescriptionCardBackColor)
            };
            detailsCard.Paint += (_, e) => DrawBorder(e.Graphics, detailsCard.ClientRectangle, DescriptionPanelBorder, 4);

            var rows = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                BackColor = DescriptionCardBackColor,
                Margin = Padding.Empty
            };

            AddDescriptionRows(rows);
            detailsCard.Controls.Add(rows);
            content.Controls.Add(detailsCard, 0, 1);
            root.Controls.Add(content, 0, 0);

            var totalPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = PurchasePanelNeutral,
                Padding = new Padding(0, 12, 0, 0),
                Margin = Padding.Empty,
                Tag = new PurchaseBackColor(PurchasePanelNeutral)
            };
            totalPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            totalPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            totalPanel.Paint += (_, e) =>
            {
                using var pen = new Pen(DescriptionPanelBorder, 1);
                e.Graphics.DrawLine(pen, 0, 0, totalPanel.Width, 0);
            };

            var totalRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = DescriptionTotalRowHeight,
                ColumnCount = 2,
                BackColor = PurchasePanelNeutral,
                Margin = new Padding(0, 0, 0, 12),
                Tag = new PurchaseBackColor(PurchasePanelNeutral)
            };
            totalRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            totalRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            totalRow.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Tổng tiền",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Regular),
                ForeColor = LightTheme.Foreground,
                BackColor = PurchasePanelNeutral
            }, 0, 0);
            _totalLabel.Dock = DockStyle.Fill;
            _totalLabel.TextAlign = ContentAlignment.MiddleRight;
            _totalLabel.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            _totalLabel.ForeColor = LightTheme.Foreground;
            _totalLabel.BackColor = PurchasePanelNeutral;
            totalRow.Controls.Add(_totalLabel, 1, 0);

            _buyButton.Dock = DockStyle.Top;
            _buyButton.Margin = Padding.Empty;
            totalPanel.Controls.Add(totalRow, 0, 0);
            totalPanel.Controls.Add(_buyButton, 0, 1);
            root.Controls.Add(totalPanel, 0, 2);
            _descriptionHost.Controls.Add(root);
        }
        finally
        {
            _descriptionHost.ResumeLayout();
        }
    }

    private void AddDescriptionRows(TableLayoutPanel rows)
    {
        var detailRows = new List<Control>
        {
            CreateDetailRow("new-design/network-type.svg", "Kiểu mạng", "Cáp quang"),
            CreateProtocolDetailRow(),
            CreateDetailRow("new-design/ip-type.svg", "Loại IP", "Pv4 dân cư-Unlimited Bandwidth")
        };

        if (_selectedKind != ProxyProductKind.Datacenter)
        {
            detailRows.Add(CreateProviderDetailRow());
        }

        detailRows.Add(CreateDetailRow("new-design/location.svg", "Vị trí", "Ngẫu nhiên"));

        if (_selectedKind == ProxyProductKind.Rotate)
        {
            detailRows.Add(CreateDetailRow("new-design/time-and-keep-proxy-time.svg", "Thời gian", "Đổi IP tối thiểu 60s/lần", LightTheme.Warning));
            detailRows.Add(CreateDetailRow("new-design/time-and-keep-proxy-time.svg", "Giữ IP", "Tối đa 30 phút", LightTheme.Warning));
        }

        detailRows.Add(CreateNationDetailRow());

        for (var index = 0; index < detailRows.Count; index++)
        {
            var row = detailRows[index];
            row.Margin = Padding.Empty;
            rows.Controls.Add(row);
        }
    }

    private int GetDescriptionCardHeight()
    {
        var rowCount = _selectedKind == ProxyProductKind.Rotate ? 8 : 6;
        return (rowCount * DescriptionRowHeight) + (DescriptionCardPadding * 2);
    }

    private Control CreateDetailRow(string iconName, string label, string value, Color? valueColor = null, int height = DescriptionRowHeight)
    {
        var row = CreateDetailRowShell(height);
        row.Controls.Add(CreateDetailLabel(iconName, label), 0, 0);
        row.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = value,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", DescriptionFontSize, FontStyle.Regular),
            ForeColor = valueColor ?? LightTheme.Foreground,
            AutoEllipsis = false,
            UseMnemonic = false,
            BackColor = DescriptionCardBackColor,
            Padding = new Padding(0, 0, 2, 0),
            Margin = Padding.Empty
        }, 1, 0);
        return row;
    }

    private Control CreateProtocolDetailRow()
    {
        var row = CreateDetailRowShell(DescriptionRowHeight);
        row.Controls.Add(CreateDetailLabel("new-design/protocol.svg", "Giao thức"), 0, 0);

        var tags = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = DescriptionCardBackColor,
            Margin = Padding.Empty,
            Padding = new Padding(0, 6, 0, 0)
        };
        tags.Controls.Add(CreateTag("SOCK5"));
        tags.Controls.Add(CreateTag("HTTP"));
        row.Controls.Add(tags, 1, 0);
        return row;
    }

    private Control CreateProviderDetailRow()
    {
        var row = CreateDetailRowShell(DescriptionRowHeight);
        row.Controls.Add(CreateDetailLabel("new-design/network-provider.svg", "Nhà mạng"), 0, 0);

        var logos = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = DescriptionCardBackColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        logos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DescriptionProviderLogoWidth + 4));
        logos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DescriptionProviderLogoWidth + 4));
        logos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DescriptionProviderLogoWidth));
        logos.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var providerImages = new[] { "viettel.png", "fpt.png", "vnpt.png" };
        for (var index = 0; index < providerImages.Length; index++)
        {
            var image = LoadProductImage(providerImages[index]);
            if (image is null)
            {
                continue;
            }

            logos.Controls.Add(new PictureBox
            {
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = DescriptionProviderLogoWidth,
                Height = DescriptionProviderLogoHeight,
                Anchor = AnchorStyles.None,
                Margin = Padding.Empty,
                BackColor = DescriptionCardBackColor
            }, index + 1, 0);
        }

        if (logos.Controls.Count == 0)
        {
            logos.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Viettel / FPT / VNPT",
                ForeColor = LightTheme.Foreground,
                Margin = Padding.Empty,
                BackColor = DescriptionCardBackColor
            }, 1, 0);
        }

        row.Controls.Add(logos, 1, 0);
        return row;
    }

    private Control CreateNationDetailRow()
    {
        var row = CreateDetailRowShell(DescriptionRowHeight);
        row.Controls.Add(CreateDetailLabel("new-design/nation.svg", "Quốc gia"), 0, 0);

        var flags = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            BackColor = DescriptionCardBackColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        if (_selectedKind == ProxyProductKind.Datacenter)
        {
            flags.ColumnCount = 4;
            flags.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            flags.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DescriptionFlagWidth));
            flags.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
            flags.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DescriptionFlagWidth));
            flags.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            flags.Controls.Add(CreateFlagBox(_cmcFlagIcon), 1, 0);
            flags.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "/",
                Font = new Font("Segoe UI", DescriptionFontSize, FontStyle.Bold),
                ForeColor = LightTheme.Foreground,
                BackColor = DescriptionCardBackColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty
            }, 2, 0);
            flags.Controls.Add(CreateFlagBox(_usFlagIcon), 3, 0);
        }
        else
        {
            flags.ColumnCount = 2;
            flags.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            flags.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DescriptionFlagWidth));
            flags.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            flags.Controls.Add(CreateFlagBox(_cmcFlagIcon), 1, 0);
        }

        row.Controls.Add(flags, 1, 0);
        return row;
    }

    private static Control CreateFlagBox(Image? image) =>
        new PictureBox
        {
            Image = image,
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = DescriptionFlagWidth,
            Height = DescriptionFlagHeight,
            Anchor = AnchorStyles.None,
            Margin = Padding.Empty,
            BackColor = DescriptionCardBackColor
        };

    private static TableLayoutPanel CreateDetailRowShell(int height)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = height,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = DescriptionCardBackColor,
            Margin = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DescriptionLabelColumnWidth));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        row.Paint += (_, e) =>
        {
            using var pen = new Pen(DescriptionRowBorder, 1);
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };
        return row;
    }

    private static Control CreateDetailLabel(string iconName, string label)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = DescriptionCardBackColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DescriptionIconSize + 8));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new PictureBox
        {
            Image = SidebarIconRenderer.LoadOriginal(iconName, DescriptionIconSize),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = DescriptionCardBackColor
        }, 0, 0);
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = label,
            Font = new Font("Segoe UI", DescriptionFontSize, FontStyle.Regular),
            ForeColor = LightTheme.Muted,
            BackColor = DescriptionCardBackColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        }, 1, 0);
        return panel;
    }

    private static Label CreateTag(string text) =>
        new()
        {
            AutoSize = true,
            Text = text,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = LightTheme.Foreground,
            BackColor = Color.FromArgb(243, 243, 243),
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(5, 0, 0, 0),
            Tag = new PurchaseBackColor(Color.FromArgb(243, 243, 243))
        };

    private Control CreateField(string label, Control input)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = PurchasePanelNeutral,
            Margin = new Padding(0, 0, 12, 10)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = LightTheme.Foreground,
            BackColor = PurchasePanelNeutral,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        input.Dock = DockStyle.Top;
        input.Margin = Padding.Empty;
        panel.Controls.Add(input, 0, 1);
        return panel;
    }

    private Control CreateCheckField(string label, Control input)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = PurchasePanelNeutral,
            Margin = new Padding(0, 0, 12, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.Controls.Add(new Panel { Height = 22, Width = 1, BackColor = PurchasePanelNeutral }, 0, 0);
        panel.SetColumnSpan(panel.Controls[0], 2);
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            ForeColor = LightTheme.Foreground,
            BackColor = PurchasePanelNeutral,
            Margin = new Padding(0, 8, 8, 0)
        }, 0, 1);
        input.Anchor = AnchorStyles.Left;
        input.Margin = new Padding(0, 8, 0, 0);
        panel.Controls.Add(input, 1, 1);
        return panel;
    }

    private async Task PurchaseAsync()
    {
        if (_isBusy || !TryBuildPurchaseProduct(out var product))
        {
            return;
        }

        if (!await EnsureAuthenticatedAsync())
        {
            return;
        }

        await SubmitPurchaseAsync(product, retryAfterLogin: true);
    }

    private async Task SubmitPurchaseAsync(ProxyPurchaseProduct product, bool retryAfterLogin)
    {
        SetBusy(true);
        _statusLabel.Text = "Đang tạo đơn hàng...";
        try
        {
            await _proxyOrderApiClient.PurchaseProxyAsync(
                new ProxyPurchaseOrder
                {
                    Products = [product]
                },
                CancellationToken.None);
            _statusLabel.Text = "Đã tạo đơn hàng.";
            UiFeedback.ShowInfo(this, "Đã mua proxy thành công.");
            await RefreshLoadedHistoryTabsAsync();
            PurchaseCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized && retryAfterLogin)
        {
            await _authService.ClearAsync(CancellationToken.None);
            SetBusy(false);
            if (await EnsureAuthenticatedAsync())
            {
                await SubmitPurchaseAsync(product, retryAfterLogin: false);
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            _statusLabel.Text = "Không đủ coin để mua proxy.";
            using var form = new InsufficientCoinForm();
            if (form.ShowDialog(this) == DialogResult.OK && DepositRequestedAsync is not null)
            {
                SetBusy(false);
                await DepositRequestedAsync();
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Không thể tạo đơn hàng.";
            UiFeedback.ShowWarning(this, ex, "Không thể mua proxy");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshLoadedHistoryTabsAsync()
    {
        var tasks = new List<Task>();
        if (_purchaseHistoryControl.HasLoaded)
        {
            tasks.Add(_purchaseHistoryControl.RefreshAsync());
        }

        if (_transactionHistoryControl.HasLoaded)
        {
            tasks.Add(_transactionHistoryControl.RefreshAsync());
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    private bool TryBuildPurchaseProduct(out ProxyPurchaseProduct purchaseProduct)
    {
        purchaseProduct = new ProxyPurchaseProduct();
        var selectedProduct = GetSelectedProduct();
        if (selectedProduct is null)
        {
            UiFeedback.ShowInfo(this, "Chưa có sản phẩm để mua.");
            return false;
        }

        var password = ResolvePassword();
        if (!string.IsNullOrWhiteSpace(_passwordInput.Text.Trim()) && password.Length < MinimumProxyPasswordLength)
        {
            _passwordInput.ErrorText = "Mật khẩu phải có tối thiểu 9 kí tự.";
            _passwordInput.FocusInput();
            return false;
        }

        var isRotate = _selectedKind == ProxyProductKind.Rotate;
        if (isRotate && _autoRotateCheckBox.Checked && _rotateIntervalInput.Value < 1)
        {
            MessageBox.Show(
                this,
                "Thời gian xoay phải lớn hơn 0 khi bật tự động xoay.",
                "Dữ liệu không hợp lệ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _rotateIntervalInput.Focus();
            return false;
        }

        purchaseProduct = new ProxyPurchaseProduct
        {
            IsCdk = false,
            DayOfUse = (int)_durationInput.Value,
            RotateInterval = isRotate && _autoRotateCheckBox.Checked ? (int)_rotateIntervalInput.Value : 0,
            Password = password,
            User = isRotate ? "random" : ResolveUsername(),
            ProtocolType = ResolveProtocolType(),
            Provider = selectedProduct.Provider,
            Quantity = (int)_quantityInput.Value,
            IsAutoRotate = isRotate ? _autoRotateCheckBox.Checked : null,
            Product = new ProxyPurchaseProductReference
            {
                Id = selectedProduct.Id
            }
        };
        return true;
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        try
        {
            if (await _authService.EnsureValidSessionAsync(CancellationToken.None))
            {
                return true;
            }
        }
        catch
        {
            await _authService.ClearAsync(CancellationToken.None);
        }

        return LoginRequestedAsync is not null && await LoginRequestedAsync();
    }

    private void UpdateKindCards()
    {
        _staticCard.SetPrice(GetStartingPrice(ProxyProductKind.Static));
        _datacenterCard.SetPrice(GetStartingPrice(ProxyProductKind.Datacenter));
        _rotateCard.SetPrice(GetStartingPrice(ProxyProductKind.Rotate));
        _staticCard.Enabled = _productsByKind[ProxyProductKind.Static].Count > 0;
        _datacenterCard.Enabled = _productsByKind[ProxyProductKind.Datacenter].Count > 0;
        _rotateCard.Enabled = _productsByKind[ProxyProductKind.Rotate].Count > 0;
    }

    private decimal? GetStartingPrice(ProxyProductKind kind)
    {
        var products = _productsByKind[kind];
        return products.Count == 0 ? null : products.Min(product => product.Price);
    }

    private void UpdateTotal()
    {
        if (_suppressInputEvents)
        {
            return;
        }

        var selected = GetSelectedProduct();
        if (selected is null)
        {
            _totalLabel.Text = FormatCurrency(0);
            _buyButton.Enabled = false;
            return;
        }

        var duration = (int)_durationInput.Value;
        var quantity = (int)_quantityInput.Value;
        var discount = _discountRules.GetDiscountMultiplier(selected.Category.Id, duration);
        var total = Math.Round(selected.Price * duration * quantity * discount, 0, MidpointRounding.AwayFromZero);
        _totalLabel.Text = FormatCurrency(total);
        _buyButton.Enabled = !_isBusy;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _refreshButton.Enabled = !busy && !_isLoadingProducts;
        _productCombo.Enabled = !busy && GetSelectedProduct() is not null;
        _protocolCombo.Enabled = !busy;
        _durationInput.Enabled = !busy;
        _quantityInput.Enabled = !busy;
        _usernameInput.Enabled = !busy && _selectedKind != ProxyProductKind.Rotate;
        _passwordInput.Enabled = !busy;
        _autoRotateCheckBox.Enabled = !busy && _selectedKind == ProxyProductKind.Rotate;
        _rotateIntervalInput.Enabled = !busy && _selectedKind == ProxyProductKind.Rotate && _autoRotateCheckBox.Checked;
        _buyButton.Enabled = !busy && GetSelectedProduct() is not null;
    }

    private void SetProductLoading(bool loading)
    {
        _isLoadingProducts = loading;
        _refreshButton.Enabled = !loading && !_isBusy;
        if (_tabs.SelectedKey == ProductsTabKey)
        {
            ShowProductsTab();
        }
    }

    private ProxyProduct? GetSelectedProduct() =>
        _productCombo.SelectedItem is ProductOption option ? option.Product : null;

    private string ResolveUsername()
    {
        var username = _usernameInput.Text.Trim();
        return string.IsNullOrWhiteSpace(username)
            ? $"user{RandomNumberGenerator.GetInt32(100000, 999999)}"
            : username;
    }

    private string ResolvePassword()
    {
        var password = _passwordInput.Text.Trim();
        return string.IsNullOrWhiteSpace(password)
            ? $"proxy{RandomNumberGenerator.GetInt32(100000, 999999)}"
            : password;
    }

    private string ResolveProtocolType() =>
        _protocolCombo.SelectedItem is ProxyProtocolOption option
            ? option.DisplayName
            : "HTTP";

    private string GetDurationLabel()
    {
        var selected = GetSelectedProduct();
        var unit = selected is null ? "ngày" : GetUnitDisplayNameLower(selected);
        return unit switch
        {
            "tuần" => "Tuần sử dụng",
            "tháng" => "Tháng sử dụng",
            _ => "Ngày sử dụng"
        };
    }

    private string GetNationDisplay()
    {
        if (_selectedKind == ProxyProductKind.Datacenter)
        {
            return _productsByKind[ProxyProductKind.Datacenter].Any(product => IsUsProduct(product))
                ? "Việt Nam / US"
                : "Việt Nam";
        }

        return "Việt Nam";
    }

    private IReadOnlyList<Image?> GetProductOptionImages(ProxyProduct product)
    {
        if (product.Kind == ProxyProductKind.Datacenter)
        {
            return [IsUsProduct(product) ? _usFlagIcon : _cmcFlagIcon];
        }

        if (product.Kind == ProxyProductKind.Static)
        {
            return [GetProviderIcon(product.Provider)];
        }

        return [];
    }

    private Image? GetProviderIcon(string provider) =>
        provider.Trim().ToUpperInvariant() switch
        {
            "FPT" => _fptProviderIcon,
            "VIETTEL" => _viettelProviderIcon,
            "VNPT" => _vnptProviderIcon,
            _ => null
        };

    private static bool IsAllowedStaticProvider(string provider) =>
        provider.Trim().ToUpperInvariant() is "FPT" or "VIETTEL" or "VNPT";

    private static int CompareProducts(ProxyProduct left, ProxyProduct right)
    {
        var sort = left.Sort.CompareTo(right.Sort);
        if (sort != 0)
        {
            return sort;
        }

        var price = left.Price.CompareTo(right.Price);
        if (price != 0)
        {
            return price;
        }

        return string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string BuildProductDisplayName(ProxyProduct product)
    {
        if (product.Kind == ProxyProductKind.Rotate)
        {
            return $"Gói theo {GetUnitDisplayNameLower(product)}";
        }

        if (product.Kind == ProxyProductKind.Datacenter)
        {
            return IsUsProduct(product) ? "US" : "Việt Nam";
        }

        return GetProviderDisplayName(product.Provider);
    }

    private static string GetProviderDisplayName(string provider) =>
        provider.Trim().ToUpperInvariant() switch
        {
            "VIETTEL" => "Viettel",
            "FPT" => "FPT",
            "VNPT" => "VNPT",
            "CMC" => "CMC",
            "US" => "US",
            _ => string.IsNullOrWhiteSpace(provider) ? "Proxy" : provider
        };

    private static string GetKindDisplayName(ProxyProductKind kind) =>
        kind switch
        {
            ProxyProductKind.Datacenter => "Proxy Datacenter",
            ProxyProductKind.Rotate => "Proxy Xoay",
            _ => "Proxy tĩnh"
        };

    private static string GetUnitDisplayNameLower(ProxyProduct product)
    {
        var unit = product.Category.Unit.Trim().ToLowerInvariant();
        if (unit.Contains("week", StringComparison.OrdinalIgnoreCase) ||
            unit.Contains("tuan", StringComparison.OrdinalIgnoreCase) ||
            unit.Contains("tuần", StringComparison.OrdinalIgnoreCase))
        {
            return "tuần";
        }

        if (unit.Contains("month", StringComparison.OrdinalIgnoreCase) ||
            unit.Contains("thang", StringComparison.OrdinalIgnoreCase) ||
            unit.Contains("tháng", StringComparison.OrdinalIgnoreCase))
        {
            return "tháng";
        }

        return "ngày";
    }

    private static bool IsUsProduct(ProxyProduct product) =>
        string.Equals(product.Provider, "US", StringComparison.OrdinalIgnoreCase) ||
        product.Name.Contains("US", StringComparison.OrdinalIgnoreCase) ||
        product.Name.Contains("USA", StringComparison.OrdinalIgnoreCase);

    private static int ParsePositiveInt(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number
            : fallback;

    private static string FormatCurrency(decimal value) =>
        $"{value.ToString("N0", ViCulture)}đ";

    private static ComboBox CreateReadonlyCombo(string value)
    {
        var comboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        comboBox.Items.Add(value);
        comboBox.SelectedIndex = 0;
        comboBox.Enabled = false;
        return comboBox;
    }

    private static NumericUpDown CreateNumericInput(int minimum, int maximum) =>
        new()
        {
            Minimum = minimum,
            Maximum = maximum,
            DecimalPlaces = 0,
            Increment = 1,
            Height = LightTheme.InputHeight,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10f)
        };

    private static Image? LoadProductImage(string imageName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "product-card-images", imageName);
        if (File.Exists(path))
        {
            using var image = Image.FromFile(path);
            return new Bitmap(image);
        }

        using var resource = EmbeddedAssets.Open($"Assets/product-card-images/{imageName}");
        if (resource is null)
        {
            return null;
        }

        using var embedded = Image.FromStream(resource);
        return new Bitmap(embedded);
    }

    private static void DrawBorder(Graphics graphics, Rectangle bounds, Color color, int radius)
    {
        bounds.Width -= 1;
        bounds.Height -= 1;
        using var path = AppButton.CreateRoundedRectangle(bounds, radius);
        using var pen = new Pen(color, 1);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.DrawPath(pen, path);
    }

    private static void RestoreCustomColors(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (child.Tag is PurchaseBackColor purchaseBackColor)
            {
                child.BackColor = purchaseBackColor.Color;
            }
            else
            {
                switch (child)
                {
                    case ProductKindCard productKindCard:
                        productKindCard.RestoreWhiteBackground();
                        break;
                    case AppButton or AppTextInput or ComboBox or NumericUpDown or CheckBox:
                        break;
                    case Label label:
                        label.BackColor = label.Parent?.BackColor ?? LightTheme.Background;
                        break;
                    case PictureBox pictureBox:
                        pictureBox.BackColor = pictureBox.Parent?.BackColor ?? LightTheme.Surface;
                        break;
                    default:
                        child.BackColor = child.Parent?.BackColor ?? LightTheme.Background;
                        break;
                }
            }

            RestoreCustomColors(child);
        }
    }

    private void DrawProductComboItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox comboBox ||
            e.Index < 0 ||
            e.Index >= comboBox.Items.Count)
        {
            return;
        }

        e.DrawBackground();
        var item = comboBox.Items[e.Index];
        var text = comboBox.GetItemText(item);
        var images = item is ProductOption option ? option.Images : [];
        var textLeft = e.Bounds.Left + ComboItemHorizontalPadding;

        foreach (var image in images.Where(image => image is not null))
        {
            var imageHeight = Math.Min(ComboItemImageHeight, Math.Max(1, e.Bounds.Height - 6));
            var imageWidth = Math.Min(
                ComboMaxItemImageWidth,
                Math.Max(1, (int)Math.Round(image!.Width * (imageHeight / (double)Math.Max(1, image.Height)))));
            var imageTop = e.Bounds.Top + Math.Max(0, (e.Bounds.Height - imageHeight) / 2);
            e.Graphics.DrawImage(image, textLeft, imageTop, imageWidth, imageHeight);
            textLeft += imageWidth + ComboItemImageGap;
        }

        var textBounds = new Rectangle(
            textLeft,
            e.Bounds.Top,
            Math.Max(1, e.Bounds.Right - textLeft - ComboItemHorizontalPadding),
            e.Bounds.Height);
        var textColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected
            ? SystemColors.HighlightText
            : comboBox.ForeColor;

        TextRenderer.DrawText(
            e.Graphics,
            text,
            comboBox.Font,
            textBounds,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private sealed record PurchaseBackColor(Color Color);

    private sealed record ProductOption(string DisplayName, ProxyProduct Product, IReadOnlyList<Image?> Images);

    private sealed class ProductKindCard : UserControl
    {
        private const int ProductCardHorizontalPadding = 16;
        private const int ProductCardTopPadding = 14;
        private const int ProductCardBottomPadding = 12;
        private const int ProductIconSize = 30;
        private const int ProductIconBoxSize = 34;
        private const int ProductTitleRowHeight = 42;
        private const int ProductPriceRowHeight = 34;
        private const float ProductTitleFontSize = 12.5f;
        private const float ProductPriceLabelFontSize = 10.5f;
        private const float ProductPriceValueFontSize = 10.5f;

        private readonly ProxyProductKind _kind;
        private readonly PictureBox _icon = new();
        private readonly Label _titleLabel = new();
        private readonly Label _priceLabel = new();
        private readonly SelectedIndicator _selectedIndicator = new();
        private readonly TableLayoutPanel _contentRoot = new();
        private bool _isHovering;
        private bool _isSelected;

        public ProductKindCard(ProxyProductKind kind, string title, string iconName)
        {
            _kind = kind;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            Dock = DockStyle.Fill;
            Margin = new Padding(0, 0, 14, 0);
            Padding = new Padding(
                ProductCardHorizontalPadding,
                ProductCardTopPadding,
                ProductCardHorizontalPadding,
                ProductCardBottomPadding);
            BackColor = LightTheme.Surface;
            Cursor = Cursors.Hand;

            _contentRoot.Dock = DockStyle.Fill;
            _contentRoot.ColumnCount = 1;
            _contentRoot.RowCount = 3;
            _contentRoot.BackColor = LightTheme.Surface;
            _contentRoot.Margin = Padding.Empty;
            _contentRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, ProductIconBoxSize));
            _contentRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, ProductTitleRowHeight));
            _contentRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, ProductPriceRowHeight));

            _icon.Image = SidebarIconRenderer.LoadOriginal(iconName, ProductIconSize);
            _icon.SizeMode = PictureBoxSizeMode.CenterImage;
            _icon.Width = ProductIconBoxSize;
            _icon.Height = ProductIconBoxSize;
            _icon.Margin = Padding.Empty;
            _icon.BackColor = LightTheme.Surface;
            _contentRoot.Controls.Add(_icon, 0, 0);

            _titleLabel.AutoSize = false;
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.Text = title;
            _titleLabel.Font = new Font("Segoe UI", ProductTitleFontSize, FontStyle.Bold);
            _titleLabel.ForeColor = LightTheme.Foreground;
            _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            _titleLabel.AutoEllipsis = true;
            _titleLabel.Margin = Padding.Empty;
            _contentRoot.Controls.Add(_titleLabel, 0, 1);

            var priceRow = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = ProductPriceRowHeight,
                ColumnCount = 2,
                BackColor = LightTheme.Surface,
                Margin = Padding.Empty
            };
            priceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            priceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            priceRow.Paint += (_, e) =>
            {
                using var pen = new Pen(LightTheme.Border, 1);
                e.Graphics.DrawLine(pen, 0, 0, priceRow.Width, 0);
            };
            priceRow.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Giá chỉ từ",
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = LightTheme.Muted,
                Font = new Font("Segoe UI", ProductPriceLabelFontSize, FontStyle.Regular),
                BackColor = LightTheme.Surface
            }, 0, 0);
            _priceLabel.Dock = DockStyle.Fill;
            _priceLabel.TextAlign = ContentAlignment.BottomRight;
            _priceLabel.ForeColor = LightTheme.Foreground;
            _priceLabel.Font = new Font("Segoe UI", ProductPriceValueFontSize, FontStyle.Bold);
            _priceLabel.BackColor = LightTheme.Surface;
            priceRow.Controls.Add(_priceLabel, 1, 0);
            _contentRoot.Controls.Add(priceRow, 0, 2);
            Controls.Add(_contentRoot);
            Controls.Add(_selectedIndicator);
            _selectedIndicator.Visible = false;
            _selectedIndicator.BringToFront();

            WireClick(this);
        }

        public event EventHandler? Clicked;

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
                _selectedIndicator.Visible = value;
                if (value)
                {
                    _selectedIndicator.BringToFront();
                }

                Invalidate();
            }
        }

        public void SetPrice(decimal? price) =>
            _priceLabel.Text = price is null ? "--" : FormatCurrency(price.Value);

        public void RestoreWhiteBackground()
        {
            BackColor = LightTheme.Surface;
            RestoreWhiteBackground(this);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
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
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _selectedIndicator.SetBounds(
                Math.Max(0, Width - SelectedIndicator.IndicatorSize - 1),
                1,
                SelectedIndicator.IndicatorSize,
                SelectedIndicator.IndicatorSize);
            _selectedIndicator.BringToFront();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? LightTheme.Surface);

            var border = _isSelected
                ? PurchaseAccent
                : _isHovering && Enabled
                    ? Color.FromArgb(138, 180, 218)
                    : LightTheme.Border;
            var background = Enabled ? LightTheme.Surface : Color.FromArgb(250, 250, 250);
            var bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;
            using var path = AppButton.CreateRoundedRectangle(bounds, 5);
            using var fill = new SolidBrush(background);
            using var pen = new Pen(border, _isSelected ? 2 : 1);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(pen, path);
        }

        private void WireClick(Control control)
        {
            control.Click += (_, _) =>
            {
                if (Enabled)
                {
                    Clicked?.Invoke(this, EventArgs.Empty);
                }
            };

            foreach (Control child in control.Controls)
            {
                WireClick(child);
            }
        }

        private static void RestoreWhiteBackground(Control control)
        {
            foreach (Control child in control.Controls)
            {
                switch (child)
                {
                    case SelectedIndicator:
                        break;
                    default:
                        child.BackColor = LightTheme.Surface;
                        break;
                }

                RestoreWhiteBackground(child);
            }
        }

        private sealed class SelectedIndicator : Control
        {
            public const int IndicatorSize = 54;

            private readonly Image? _checkIcon = SidebarIconRenderer.Load("new-design/check.svg", Color.White, 18);

            public SelectedIndicator()
            {
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);

                BackColor = PurchaseAccent;
                Cursor = Cursors.Hand;
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                using var path = CreateShapePath(ClientRectangle);
                Region = new Region(path);
            }

            protected override void OnPaintBackground(PaintEventArgs pevent)
            {
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = CreateShapePath(ClientRectangle))
                using (var brush = new SolidBrush(PurchaseAccent))
                {
                    e.Graphics.FillPath(brush, path);
                }

                if (_checkIcon is not null)
                {
                    e.Graphics.DrawImage(_checkIcon, Width - 34, 11, 18, 18);
                    return;
                }

                using var checkPen = new Pen(Color.White, 2)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                e.Graphics.DrawLine(checkPen, Width - 36, 18, Width - 29, 26);
                e.Graphics.DrawLine(checkPen, Width - 29, 26, Width - 16, 11);
            }

            private static System.Drawing.Drawing2D.GraphicsPath CreateShapePath(Rectangle bounds)
            {
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                var width = Math.Max(1, bounds.Width - 1);
                var height = Math.Max(1, bounds.Height - 1);
                path.StartFigure();
                path.AddLine(bounds.Left, bounds.Top, bounds.Left + width, bounds.Top);
                path.AddLine(bounds.Left + width, bounds.Top, bounds.Left + width, bounds.Top + height);
                path.AddBezier(
                    bounds.Left + width,
                    bounds.Top + height,
                    bounds.Left + (width * 0.45f),
                    bounds.Top + height,
                    bounds.Left,
                    bounds.Top + (height * 0.55f),
                    bounds.Left,
                    bounds.Top);
                path.CloseFigure();
                return path;
            }
        }
    }

    private sealed class PurchaseButton : Button
    {
        private bool _isHovering;
        private bool _isPressed;

        public PurchaseButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            Tag = LightTheme.SkipButtonThemeTag;
            TabStop = false;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            ForeColor = Color.White;
            Padding = Padding.Empty;
            Margin = Padding.Empty;
            Height = PurchaseButtonHeight;
            MinimumSize = new Size(0, PurchaseButtonHeight);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
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
            e.Graphics.Clear(Parent?.BackColor ?? LightTheme.Surface);

            var accent = Enabled
                ? _isPressed
                    ? ControlPaint.Dark(PurchaseAccent, 0.08f)
                    : _isHovering
                        ? ControlPaint.Light(PurchaseAccent, 0.08f)
                        : PurchaseAccent
                : Color.FromArgb(156, 163, 175);

            var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using var path = AppButton.CreateRoundedRectangle(bounds, 4);
            using var brush = new SolidBrush(accent);
            e.Graphics.FillPath(brush, path);

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                bounds,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
