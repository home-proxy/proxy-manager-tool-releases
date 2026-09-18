using System.Globalization;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class RenewProxyOrdersForm : Form
{
    private const int FormWidth = 540;
    private const int StaticFormHeight = 560;
    private const int RotateFormHeight = 620;
    private const int FooterHeight = 88;
    private const int ProxyListHeight = 168;
    private const int NumberInputHeight = 44;

    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Color HeaderBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color FooterBackground = Color.FromArgb(247, 247, 249);
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);
    private static readonly Color ProxyTextColor = Color.FromArgb(156, 163, 175);

    private readonly IProxyOrderCacheService _proxyOrderCacheService;
    private readonly TextBox _proxyTextBox = new();
    private readonly NumericUpDown _daysInput = CreateNumberInput(1, 3650);
    private readonly NumericUpDown _rotateIntervalInput = CreateNumberInput(0, 100000);
    private readonly CheckBox _autoRotateCheckBox = new();
    private readonly Label _totalValueLabel = new();
    private readonly TableLayoutPanel _rotateOptions = new();
    private IReadOnlyList<ProxyOrder> _orders = [];
    private ProxyProduct? _pricingProduct;
    private ProxyDiscountRules? _discountRules;
    private bool _showRotateOptions;

    public RenewProxyOrdersForm(IProxyOrderCacheService proxyOrderCacheService)
    {
        _proxyOrderCacheService = proxyOrderCacheService;

        Text = "Gia hạn proxy";
        Width = FormWidth;
        Height = StaticFormHeight;
        MinimumSize = new Size(FormWidth, StaticFormHeight);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        NumericInputGuard.AllowDigitsOnly(_daysInput);
        NumericInputGuard.AllowDigitsOnly(_rotateIntervalInput);
        LightTheme.Apply(this);
        RestoreCustomColors(this);
    }

    public int DayOfRenewal => (int)_daysInput.Value;

    public bool IsAutoRotate => _showRotateOptions && _autoRotateCheckBox.Checked;

    public int RotateInterval => IsAutoRotate ? (int)_rotateIntervalInput.Value : 0;

    public async Task LoadOrdersAsync(
        IReadOnlyCollection<ProxyOrder> orders,
        bool showRotateOptions,
        CancellationToken cancellationToken = default)
    {
        _orders = orders.ToList();
        _showRotateOptions = showRotateOptions;
        _proxyTextBox.Text = string.Join(Environment.NewLine, _orders.Select(FormatProxyLine));
        _rotateOptions.Visible = showRotateOptions;
        Height = showRotateOptions ? RotateFormHeight : StaticFormHeight;
        MinimumSize = new Size(FormWidth, Height);

        if (showRotateOptions)
        {
            var first = _orders.FirstOrDefault();
            _autoRotateCheckBox.Checked = first?.RotateInterval > 0;
            _rotateIntervalInput.Value = Math.Clamp(first?.RotateInterval ?? 0, (int)_rotateIntervalInput.Minimum, (int)_rotateIntervalInput.Maximum);
            UpdateRotateIntervalState();
        }

        _pricingProduct = null;
        _discountRules = null;
        _totalValueLabel.Text = "--";
        await LoadPricingAsync(cancellationToken);
        UpdateTotal();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight));

        root.Controls.Add(CreateContent(), 0, 0);
        root.Controls.Add(CreateFooter(), 0, 1);
        Controls.Add(root);
    }

    private Control CreateContent()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(24, 20, 24, 16),
            BackColor = Color.White
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, ProxyListHeight));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        content.Controls.Add(CreateFieldLabel("Proxy IP"), 0, 0);
        ConfigureProxyTextBox();
        content.Controls.Add(_proxyTextBox, 0, 1);
        content.Controls.Add(CreateNumberField("Thời hạn (ngày)", _daysInput, required: true), 0, 2);
        content.Controls.Add(CreateRotateOptions(), 0, 3);
        content.Controls.Add(CreateTotalRow(), 0, 4);
        return content;
    }

    private void ConfigureProxyTextBox()
    {
        _proxyTextBox.Dock = DockStyle.Fill;
        _proxyTextBox.Multiline = true;
        _proxyTextBox.ReadOnly = true;
        _proxyTextBox.ScrollBars = ScrollBars.Vertical;
        _proxyTextBox.BorderStyle = BorderStyle.FixedSingle;
        _proxyTextBox.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        _proxyTextBox.ForeColor = ProxyTextColor;
        _proxyTextBox.BackColor = Color.White;
        _proxyTextBox.Margin = new Padding(0, 0, 0, 18);
    }

    private Control CreateNumberField(string label, NumericUpDown input, bool required = false)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 16)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, NumberInputHeight));
        panel.Controls.Add(CreateFieldLabel(label, required), 0, 0);
        input.Dock = DockStyle.Fill;
        input.Margin = Padding.Empty;
        input.ValueChanged += (_, _) => UpdateTotal();
        panel.Controls.Add(input, 0, 1);
        return panel;
    }

    private Control CreateRotateOptions()
    {
        _rotateOptions.Dock = DockStyle.Top;
        _rotateOptions.AutoSize = true;
        _rotateOptions.ColumnCount = 2;
        _rotateOptions.RowCount = 1;
        _rotateOptions.BackColor = Color.White;
        _rotateOptions.Margin = new Padding(0, 0, 0, 14);
        _rotateOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        _rotateOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        _autoRotateCheckBox.AutoSize = true;
        _autoRotateCheckBox.Text = "Tự động xoay";
        _autoRotateCheckBox.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _autoRotateCheckBox.ForeColor = TextColor;
        _autoRotateCheckBox.BackColor = Color.White;
        _autoRotateCheckBox.Margin = new Padding(0, 11, 0, 0);
        _autoRotateCheckBox.CheckedChanged += (_, _) => UpdateRotateIntervalState();

        _rotateOptions.Controls.Add(_autoRotateCheckBox, 0, 0);
        _rotateOptions.Controls.Add(CreateInlineNumberField("Thời gian xoay", _rotateIntervalInput), 1, 0);
        _rotateOptions.Visible = false;
        return _rotateOptions;
    }

    private static Control CreateInlineNumberField(string label, NumericUpDown input)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = TextColor,
            BackColor = Color.White,
            Margin = new Padding(0, 11, 12, 0)
        }, 0, 0);
        input.Dock = DockStyle.Top;
        input.Margin = Padding.Empty;
        panel.Controls.Add(input, 1, 0);
        return panel;
    }

    private Control CreateTotalRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 8)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        row.Controls.Add(new Label
        {
            Text = "Tổng tiền",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.White
        }, 0, 0);
        _totalValueLabel.Dock = DockStyle.Fill;
        _totalValueLabel.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _totalValueLabel.ForeColor = LightTheme.Accent;
        _totalValueLabel.TextAlign = ContentAlignment.MiddleRight;
        _totalValueLabel.BackColor = Color.White;
        row.Controls.Add(_totalValueLabel, 1, 0);
        return row;
    }

    private Control CreateFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FooterBackground,
            Padding = new Padding(24, 20, 24, 20),
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
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        var renewButton = new AppPrimaryButton
        {
            Text = "Gia hạn",
            Width = 126,
            Height = 40,
            MinimumSize = new Size(126, 40),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Margin = new Padding(0, 0, 12, 0)
        };
        renewButton.Click += (_, _) => DialogResult = DialogResult.OK;
        var cancelButton = new AppButton
        {
            Text = "Hủy",
            Width = 88,
            Height = 40,
            MinimumSize = new Size(88, 40),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Variant = AppButtonVariant.Muted,
            AccentColor = LightTheme.Muted,
            Tag = LightTheme.Muted,
            Margin = Padding.Empty
        };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(renewButton);
        buttons.Controls.Add(cancelButton);
        footer.Controls.Add(buttons);

        AcceptButton = renewButton;
        CancelButton = cancelButton;
        return footer;
    }

    private async Task LoadPricingAsync(CancellationToken cancellationToken)
    {
        var categoryTypeId = _orders.FirstOrDefault()?.CategoryTypeId ?? 1;
        try
        {
            var products = await _proxyOrderCacheService.GetProductsAsync(categoryTypeId, forceRefresh: false, cancellationToken);
            _discountRules = await _proxyOrderCacheService.GetDiscountRulesAsync(forceRefresh: false, cancellationToken);
            _pricingProduct = SelectPricingProduct(products.Products, categoryTypeId);
        }
        catch
        {
            _pricingProduct = null;
            _discountRules = null;
        }
    }

    private ProxyProduct? SelectPricingProduct(IReadOnlyList<ProxyProduct> products, int categoryTypeId)
    {
        var categoryProducts = products
            .Where(product => product.Category.CategoryType.Id == categoryTypeId || product.Category.CategoryType.Id == 0)
            .ToList();
        if (categoryProducts.Count == 0)
        {
            categoryProducts = products.ToList();
        }

        var commonProvider = GetCommonProvider();
        if (!string.IsNullOrWhiteSpace(commonProvider))
        {
            var providerMatch = categoryProducts
                .Where(product => string.Equals(product.Provider, commonProvider, StringComparison.OrdinalIgnoreCase))
                .OrderBy(product => product.Price)
                .FirstOrDefault();
            if (providerMatch is not null)
            {
                return providerMatch;
            }
        }

        return categoryProducts.OrderBy(product => product.Price).FirstOrDefault();
    }

    private string? GetCommonProvider()
    {
        var providers = _orders
            .Select(order => order.Provider.Trim())
            .Where(provider => !string.IsNullOrWhiteSpace(provider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return providers.Count == 1 ? providers[0] : null;
    }

    private void UpdateTotal()
    {
        if (_pricingProduct is null || _orders.Count == 0)
        {
            _totalValueLabel.Text = "--";
            return;
        }

        var days = (int)_daysInput.Value;
        var discount = _discountRules?.GetDiscountMultiplier(_pricingProduct.Category.Id, days) ?? 1m;
        var total = Math.Round(_pricingProduct.Price * days * _orders.Count * discount, 0, MidpointRounding.AwayFromZero);
        _totalValueLabel.Text = FormatCurrency(total);
    }

    private void UpdateRotateIntervalState()
    {
        _rotateIntervalInput.Enabled = _autoRotateCheckBox.Checked;
        if (!_autoRotateCheckBox.Checked)
        {
            _rotateIntervalInput.Value = 0;
            return;
        }

        if (_rotateIntervalInput.Value == 0)
        {
            _rotateIntervalInput.Value = 1;
        }
    }

    private static Control CreateFieldLabel(string text, bool required = false)
    {
        var label = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty
        };
        label.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ForeColor = TextColor,
            BackColor = Color.White,
            Margin = Padding.Empty
        });
        if (required)
        {
            label.Controls.Add(new Label
            {
                Text = "*",
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
                ForeColor = LightTheme.Danger,
                BackColor = Color.White,
                Margin = new Padding(4, 0, 0, 0)
            });
        }

        return label;
    }

    private static NumericUpDown CreateNumberInput(int minimum, int maximum) =>
        new()
        {
            Minimum = minimum,
            Maximum = maximum,
            DecimalPlaces = 0,
            Increment = 1,
            Value = minimum,
            Height = NumberInputHeight,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center,
            BorderStyle = BorderStyle.FixedSingle
        };

    private static string FormatProxyLine(ProxyOrder order)
    {
        var endpoint = string.IsNullOrWhiteSpace(order.Ip)
            ? order.Domain
            : order.Port > 0 ? $"{order.Ip}:{order.Port}" : order.Ip;
        return string.IsNullOrWhiteSpace(order.Username)
            ? endpoint
            : $"{endpoint}:{order.Username}";
    }

    private static string FormatCurrency(decimal value) =>
        $"{value.ToString("N0", ViCulture)}đ";

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
}
