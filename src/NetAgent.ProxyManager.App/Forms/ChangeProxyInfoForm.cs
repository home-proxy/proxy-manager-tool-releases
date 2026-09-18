using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class ChangeProxyInfoForm : Form
{
    private const int FormWidth = 460;
    private const int FormHeight = 670;
    private const int FooterHeight = 88;
    private const int ProxyListHeight = 170;

    private static readonly Color HeaderBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color FooterBackground = Color.FromArgb(247, 247, 249);
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);
    private static readonly Color ProxyTextColor = Color.FromArgb(156, 163, 175);

    private readonly TextBox _proxyTextBox = new();
    private readonly AppTextInput _usernameTextBox = new();
    private readonly AppTextInput _passwordTextBox = new();
    private readonly ComboBox _protocolCombo = new();

    public ChangeProxyInfoForm()
    {
        Text = "Đổi thông tin proxy";
        Width = FormWidth;
        Height = FormHeight;
        MinimumSize = new Size(FormWidth, FormHeight);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LightTheme.Apply(this);
        RestoreCustomColors(this);
    }

    public string Username => _usernameTextBox.Text.Trim();

    public string Password => _passwordTextBox.Text.Trim();

    public ProxyProtocol Protocol => _protocolCombo.SelectedItem is ProxyProtocolOption option
        ? option.Value
        : ProxyProtocol.Https;

    public void LoadOrders(IReadOnlyCollection<ProxyOrder> orders)
    {
        _proxyTextBox.Text = string.Join(Environment.NewLine, orders.Select(FormatProxyLine));
        var first = orders.FirstOrDefault();
        if (first is not null && orders.Count == 1)
        {
            _usernameTextBox.Text = first.Username;
            _passwordTextBox.Text = first.Password;
            SelectProtocol(first.Protocol);
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
            RowCount = 7,
            Padding = new Padding(24, 20, 24, 14),
            BackColor = Color.White
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, ProxyListHeight));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        content.Controls.Add(CreateFieldLabel("Proxy IP"), 0, 0);
        ConfigureProxyTextBox();
        content.Controls.Add(_proxyTextBox, 0, 1);
        content.Controls.Add(CreateComboField("Giao thức", _protocolCombo, required: true), 0, 2);
        content.Controls.Add(CreateTextField("Username", _usernameTextBox, required: true), 0, 3);
        content.Controls.Add(CreateTextField("Password", _passwordTextBox, required: true), 0, 4);
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
        input.Width = 390;
        panel.Controls.Add(input, 0, 1);
        return panel;
    }

    private static Control CreateComboField(string label, ComboBox combo, bool required = false)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DisplayMember = nameof(ProxyProtocolOption.DisplayName);
        combo.Height = 40;
        combo.MinimumSize = new Size(80, 40);

        var panel = CreateFieldPanel();
        panel.Controls.Add(CreateFieldLabel(label, required), 0, 0);
        combo.Dock = DockStyle.Top;
        combo.Margin = Padding.Empty;
        panel.Controls.Add(combo, 0, 1);
        return panel;
    }

    private Control CreateFooter()
    {
        _usernameTextBox.PlaceholderText = "Nhập username proxy";
        _passwordTextBox.PlaceholderText = "Nhập password proxy mới";
        _passwordTextBox.UseSystemPasswordChar = true;
        _passwordTextBox.ShowPasswordToggle = true;
        EnsureProtocolOptions();
        SelectProtocol(ProxyProtocol.Https);

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

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            MessageBox.Show(this, "Username là bắt buộc.", "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

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

    private void SelectProtocol(ProxyProtocol protocol)
    {
        EnsureProtocolOptions();
        for (var index = 0; index < _protocolCombo.Items.Count; index++)
        {
            if (_protocolCombo.Items[index] is ProxyProtocolOption option &&
                option.Value == protocol)
            {
                _protocolCombo.SelectedIndex = index;
                return;
            }
        }

        if (_protocolCombo.Items.Count > 0)
        {
            _protocolCombo.SelectedIndex = 0;
        }
    }

    private void EnsureProtocolOptions()
    {
        if (_protocolCombo.Items.Count > 0)
        {
            return;
        }

        _protocolCombo.Items.AddRange(ProxyProtocolDisplay.Options.Cast<object>().ToArray());
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
