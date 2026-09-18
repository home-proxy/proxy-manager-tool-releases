using NetAgent.ProxyManager.App.Controls;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class ChangeApplicationProxyForm : Form
{
    private const string ManualTabKey = "manual";
    private const string OrdersTabKey = "orders";
    private const int FooterHeight = 70;
    private const int ToolbarHeight = 42;

    private static readonly Color FooterBackground = Color.FromArgb(247, 247, 249);
    private static readonly Color HeaderBorder = Color.FromArgb(224, 224, 224);
    private readonly ProxyListControl _manualProxyListControl;
    private readonly ProxyOrdersControl _orderProxyListControl;
    private readonly AppUnderlineTabs _tabs = new();
    private readonly Panel _contentHost = new();
    private readonly Panel _manualPage = new();
    private readonly TableLayoutPanel _ordersPage = new();
    private readonly ComboBox _orderKindCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly Button _refreshOrdersButton;
    private readonly AppPrimaryButton _assignButton;
    private readonly AppButton _cancelButton;
    private ProxyOrderKind _selectedOrderKind = ProxyOrderKind.Datacenter;
    private bool _contextLoaded;
    private bool _ordersLoaded;
    private bool _isBusy;

    public ChangeApplicationProxyForm(
        ProxyListControl manualProxyListControl,
        ProxyOrdersControl orderProxyListControl)
    {
        _manualProxyListControl = manualProxyListControl;
        _orderProxyListControl = orderProxyListControl;
        _manualProxyListControl.UseSelectionOnlyLayout();
        _orderProxyListControl.UsePickerLayout();

        _refreshOrdersButton = CreateToolbarButton("Làm mới", async (_, _) => await RefreshOrdersAsync(), UiIcons.NewRefreshProxy, UiIcons.NewRefreshProxyHover, UiIcons.NewRefreshProxyDisabled);
        _assignButton = new AppPrimaryButton
        {
            Text = "Chọn proxy này",
            Height = 40,
            MinimumSize = new Size(130, 40),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Margin = new Padding(0, 0, 10, 0)
        };
        _assignButton.Click += async (_, _) => await AssignSelectedAsync();
        _cancelButton = new AppButton
        {
            Text = "Hủy",
            Height = 40,
            MinimumSize = new Size(58, 40),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Variant = AppButtonVariant.Muted,
            AccentColor = LightTheme.Muted,
            Margin = Padding.Empty
        };
        _cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Text = "Gắn Proxy";
        Width = 1600;
        Height = 1000;
        MinimumSize = new Size(1180, 650);
        MinimizeBox = false;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        WireEvents();
        LightTheme.Apply(this);
        RestoreCustomColors(this);
        UpdateActionButtons();
    }

    public Func<Task<bool>>? LoginRequestedAsync
    {
        get => _orderProxyListControl.LoginRequestedAsync;
        set => _orderProxyListControl.LoginRequestedAsync = value;
    }

    public ProxyServer? SelectedManualProxy { get; private set; }

    public ProxyOrder? SelectedOrder { get; private set; }

    public ProxyOrderKind SelectedKind { get; private set; }

    public string? SelectedProxyAddress { get; private set; }

    public event EventHandler<ProfileAffectingChange>? ProfileAffectingChanged;

    public void LoadContext(IReadOnlyList<ApplicationRule> rules, IReadOnlyList<ProxyServer> localProxies)
    {
        _contextLoaded = true;
        _ = LoadContextAsync();
    }

    private async Task LoadContextAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _manualProxyListControl.LoadAsync();
            if (IsOrdersTabSelected())
            {
                await LoadOrdersAsync(forceRefresh: false);
            }
        });
    }

    private void BuildUi()
    {
        BackColor = Color.White;
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
            RowCount = 2,
            BackColor = Color.White,
            Padding = new Padding(8, 6, 8, 0),
            Margin = Padding.Empty
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(BuildTopTabsRow(), 0, 0);

        _contentHost.Dock = DockStyle.Fill;
        _contentHost.BackColor = Color.White;
        BuildManualPage();
        BuildOrdersPage();
        _contentHost.Controls.Add(_manualPage);
        _contentHost.Controls.Add(_ordersPage);
        content.Controls.Add(_contentHost, 0, 1);

        root.Controls.Add(content, 0, 0);
        root.Controls.Add(CreateFooter(), 0, 1);
        Controls.Add(root);
        ShowManualPage();
    }

    private Control BuildTopTabsRow()
    {
        _tabs.SetTabs(
        [
            new AppUnderlineTab(ManualTabKey, "Proxy của tôi"),
            new AppUnderlineTab(OrdersTabKey, "Đơn hàng của tôi")
        ]);
        _tabs.SelectedKey = ManualTabKey;
        _tabs.Dock = DockStyle.Fill;

        return _tabs;
    }

    private void BuildManualPage()
    {
        _manualPage.Dock = DockStyle.Fill;
        _manualPage.BackColor = Color.White;
        _manualPage.Padding = Padding.Empty;
        _manualProxyListControl.Dock = DockStyle.Fill;
        _manualPage.Controls.Add(_manualProxyListControl);
    }

    private void BuildOrdersPage()
    {
        _ordersPage.Dock = DockStyle.Fill;
        _ordersPage.BackColor = Color.White;
        _ordersPage.Padding = new Padding(0, 8, 0, 0);
        _ordersPage.ColumnCount = 1;
        _ordersPage.RowCount = 2;
        _ordersPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        _ordersPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _ordersPage.Controls.Add(BuildOrdersToolbar(), 0, 0);
        _ordersPage.Controls.Add(_orderProxyListControl, 0, 1);
    }

    private Control BuildOrdersToolbar()
    {
        var options = new List<OrderKindOption>
        {
            new("Proxy tĩnh", ProxyOrderKind.Static),
            new("Proxy xoay", ProxyOrderKind.RotateProxy),
            new("Proxy Datacenter", ProxyOrderKind.Datacenter)
        };
        _orderKindCombo.DataSource = options;
        _orderKindCombo.DisplayMember = nameof(OrderKindOption.DisplayName);
        _orderKindCombo.ValueMember = nameof(OrderKindOption.Kind);
        _orderKindCombo.SelectedItem = options.First(option => option.Kind == _selectedOrderKind);
        _orderKindCombo.SelectionChangeCommitted += async (_, _) =>
        {
            if (_orderKindCombo.SelectedItem is not OrderKindOption option)
            {
                return;
            }

            _selectedOrderKind = option.Kind;
            _ordersLoaded = false;
            await LoadOrdersAsync(forceRefresh: false);
        };

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, ToolbarHeight));
        row.Controls.Add(new Label
        {
            Text = "Loại Proxy",
            AutoSize = false,
            Height = ToolbarHeight,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0)
        }, 0, 0);
        _orderKindCombo.Anchor = AnchorStyles.Left;
        _orderKindCombo.Margin = Padding.Empty;
        row.Controls.Add(_orderKindCombo, 1, 0);
        row.Controls.Add(new Panel { Dock = DockStyle.Fill }, 2, 0);
        _refreshOrdersButton.Anchor = AnchorStyles.Right;
        _refreshOrdersButton.Margin = Padding.Empty;
        row.Controls.Add(_refreshOrdersButton, 3, 0);
        return row;
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
        buttons.Controls.Add(_assignButton);
        buttons.Controls.Add(_cancelButton);
        footer.Controls.Add(buttons);
        AcceptButton = _assignButton;
        CancelButton = _cancelButton;
        return footer;
    }

    private void WireEvents()
    {
        _tabs.SelectedTabChanged += async (_, key) =>
        {
            if (key == OrdersTabKey)
            {
                await ShowOrdersPageAsync();
                return;
            }

            ShowManualPage();
        };
        _manualProxyListControl.ProfileAffectingChanged += (_, change) => ProfileAffectingChanged?.Invoke(this, change);
        _orderProxyListControl.ProfileAffectingChanged += (_, change) => ProfileAffectingChanged?.Invoke(this, change);
        _manualProxyListControl.SelectionAvailabilityChanged += (_, _) => UpdateActionButtons();
        _orderProxyListControl.SelectionAvailabilityChanged += (_, _) => UpdateActionButtons();
    }

    private void ShowManualPage()
    {
        _manualPage.Visible = true;
        _ordersPage.Visible = false;
        _manualPage.BringToFront();
        UpdateActionButtons();
    }

    private async Task ShowOrdersPageAsync()
    {
        _manualPage.Visible = false;
        _ordersPage.Visible = true;
        _ordersPage.BringToFront();
        if (_contextLoaded && !_ordersLoaded)
        {
            await LoadOrdersAsync(forceRefresh: false);
        }

        UpdateActionButtons();
    }

    private bool IsOrdersTabSelected() =>
        string.Equals(_tabs.SelectedKey, OrdersTabKey, StringComparison.Ordinal);

    private async Task LoadOrdersAsync(bool forceRefresh)
    {
        await RunBusyAsync(async () =>
        {
            await _orderProxyListControl.LoadPickerAsync(_selectedOrderKind, forceRefresh);
            _ordersLoaded = true;
        });
    }

    private async Task RefreshOrdersAsync()
    {
        if (!IsOrdersTabSelected())
        {
            return;
        }

        await LoadOrdersAsync(forceRefresh: true);
    }

    private async Task AssignSelectedAsync()
    {
        SelectedManualProxy = null;
        SelectedOrder = null;
        SelectedProxyAddress = null;

        if (!IsOrdersTabSelected())
        {
            if (!_manualProxyListControl.TryGetSelectedProxy(out var manualProxy))
            {
                MessageBox.Show(this, "Hãy chọn một proxy.", "Chưa chọn proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedManualProxy = manualProxy;
            DialogResult = DialogResult.OK;
            return;
        }

        ProxyOrderPickerSelection? selection;
        try
        {
            selection = await _orderProxyListControl.GetSelectedPickerSelectionAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Không thể lấy proxy hiện tại");
            return;
        }

        if (selection is null)
        {
            MessageBox.Show(this, "Hãy chọn một proxy hợp lệ.", "Chưa chọn proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectedOrder = selection.Order;
        SelectedKind = selection.Kind;
        SelectedProxyAddress = selection.ProxyAddress;
        DialogResult = DialogResult.OK;
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        UpdateActionButtons();
        try
        {
            await action();
        }
        finally
        {
            _isBusy = false;
            UpdateActionButtons();
        }
    }

    private void UpdateActionButtons()
    {
        var hasSelection = IsOrdersTabSelected()
            ? _orderProxyListControl.HasSelectedOrder
            : _manualProxyListControl.HasSelectedProxy;
        _assignButton.Enabled = !_isBusy && hasSelection;
        _refreshOrdersButton.Enabled = !_isBusy;
        _orderKindCombo.Enabled = !_isBusy;
    }

    private static Button CreateToolbarButton(string text, EventHandler onClick, Image icon, Image hoverIcon, Image disabledIcon)
    {
        var button = new DesignedToolbarButton
        {
            Text = text,
            Image = icon,
            HoverImage = hoverIcon,
            DisabledImage = disabledIcon,
            RestTextColor = DesignedGridTheme.TextColor,
            HoverTextColor = DesignedGridTheme.AccentColor,
            DisabledTextColor = Color.FromArgb(189, 189, 189),
            MinimumSize = new Size(116, 40),
            Height = 40,
            Padding = new Padding(6),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular)
        };
        button.Click += onClick;
        return button;
    }

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

    private sealed record OrderKindOption(string DisplayName, ProxyOrderKind Kind);
}
