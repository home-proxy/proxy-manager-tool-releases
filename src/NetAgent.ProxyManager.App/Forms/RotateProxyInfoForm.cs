using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class RotateProxyInfoForm : Form
{
    private const int FormWidth = 560;
    private const int FormHeight = 550;
    private const int FooterHeight = 88;
    private const int ProxyListHeight = 170;
    private const int NumberInputHeight = 40;

    private static readonly Color HeaderBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color FooterBackground = Color.FromArgb(247, 247, 249);
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);
    private static readonly Color ProxyTextColor = Color.FromArgb(156, 163, 175);

    private readonly TextBox _proxyTextBox = new();
    private readonly AppTextInput _passwordTextBox = new();
    private readonly NumericUpDown _rotateIntervalInput = CreateNumberInput();
    private readonly CheckBox _autoRotateCheckBox = new();

    public RotateProxyInfoForm()
    {
        Text = "Cập nhật thông tin proxy xoay";
        Width = FormWidth;
        Height = FormHeight;
        MinimumSize = new Size(FormWidth, FormHeight);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        NumericInputGuard.AllowDigitsOnly(_rotateIntervalInput);
        LightTheme.Apply(this);
        RestoreCustomColors(this);
    }

    public string Password => _passwordTextBox.Text.Trim();

    public bool IsAutoRotate => _autoRotateCheckBox.Checked;

    public int RotateInterval => IsAutoRotate ? (int)_rotateIntervalInput.Value : 0;

    public void LoadOrders(IReadOnlyCollection<ProxyOrder> orders)
    {
        _proxyTextBox.Text = string.Join(Environment.NewLine, orders.Select(FormatProxyLine));
        var first = orders.FirstOrDefault();
        if (first is not null && orders.Count == 1)
        {
            _passwordTextBox.Text = first.Password;
            _autoRotateCheckBox.Checked = first.RotateInterval > 0;
            _rotateIntervalInput.Value = Math.Clamp(first.RotateInterval, (int)_rotateIntervalInput.Minimum, (int)_rotateIntervalInput.Maximum);
            UpdateRotateIntervalState();
        }
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
            Padding = new Padding(24, 20, 24, 14),
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
        content.Controls.Add(CreateTextField("Password", _passwordTextBox, required: true), 0, 2);
        content.Controls.Add(CreateRotateOptions(), 0, 3);
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

    private static Control CreateTextField(string label, AppTextInput input, bool required = false)
    {
        var panel = CreateFieldPanel();
        panel.Controls.Add(CreateFieldLabel(label, required), 0, 0);
        input.Dock = DockStyle.Top;
        input.Margin = Padding.Empty;
        panel.Controls.Add(input, 0, 1);
        return panel;
    }

    private Control CreateRotateOptions()
    {
        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 12)
        };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        _autoRotateCheckBox.AutoSize = true;
        _autoRotateCheckBox.Text = "Tự động xoay";
        _autoRotateCheckBox.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _autoRotateCheckBox.ForeColor = TextColor;
        _autoRotateCheckBox.BackColor = Color.White;
        _autoRotateCheckBox.Margin = new Padding(0, 10, 0, 0);
        _autoRotateCheckBox.CheckedChanged += (_, _) => UpdateRotateIntervalState();

        options.Controls.Add(_autoRotateCheckBox, 0, 0);
        options.Controls.Add(CreateInlineNumberField("Thời gian xoay", _rotateIntervalInput), 1, 0);
        return options;
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
            Margin = new Padding(0, 10, 12, 0)
        }, 0, 0);
        input.Dock = DockStyle.Top;
        input.Margin = Padding.Empty;
        panel.Controls.Add(input, 1, 0);
        return panel;
    }

    private Control CreateFooter()
    {
        _passwordTextBox.PlaceholderText = "Nhập password proxy";
        _passwordTextBox.UseSystemPasswordChar = true;
        _passwordTextBox.ShowPasswordToggle = true;
        UpdateRotateIntervalState();

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
        var saveButton = new AppPrimaryButton
        {
            Text = "Cập nhật",
            Width = 116,
            Height = 40,
            MinimumSize = new Size(116, 40),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Margin = new Padding(0, 0, 12, 0)
        };
        saveButton.Click += (_, _) => Confirm();
        var cancelButton = new AppButton
        {
            Text = "Hủy",
            Width = 76,
            Height = 40,
            MinimumSize = new Size(76, 40),
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Variant = AppButtonVariant.Muted,
            AccentColor = LightTheme.Muted,
            Tag = LightTheme.Muted,
            Margin = Padding.Empty
        };
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        footer.Controls.Add(buttons);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        return footer;
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

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            MessageBox.Show(this, "Password là bắt buộc.", "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (Password.Length < 9)
        {
            MessageBox.Show(this, "Password phải có ít nhất 9 ký tự.", "Dữ liệu không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private static TableLayoutPanel CreateFieldPanel()
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
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return panel;
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

    private static NumericUpDown CreateNumberInput() =>
        new()
        {
            Minimum = 0,
            Maximum = 100000,
            DecimalPlaces = 0,
            Increment = 1,
            Value = 0,
            Height = NumberInputHeight,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
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
