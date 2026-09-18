using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class ProxyEditorForm : Form
{
    private const int FormWidth = 850;
    private const int SingleFormHeight = 450;
    private const int BulkFormHeight = 400;
    private const int FooterHeight = 64;
    private const int InputWidth = 276;
    private const int CheckButtonHeight = 36;

    private static readonly Color FooterBackground = Color.FromArgb(247, 247, 249);
    private static readonly Color HeaderBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);

    private readonly IProxyChecker _proxyChecker;
    private readonly AppTextInput _ipTextBox = new();
    private readonly AppTextInput _portTextBox = new() { Width = 120 };
    private readonly ComboBox _protocolCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly AppTextInput _usernameTextBox = new();
    private readonly AppTextInput _passwordTextBox = new() { UseSystemPasswordChar = true, ShowPasswordToggle = true };
    private readonly Label _checkStatusLabel = new()
    {
        AutoSize = true,
        ForeColor = LightTheme.Muted,
        Margin = new Padding(12, 0, 12, 8),
        Visible = false
    };
    private readonly Label _bulkHintLabel = new()
    {
        Text = "Chỉ các trường được đánh dấu mới áp dụng cho toàn bộ proxy đã chọn.",
        AutoSize = true,
        Visible = false,
        ForeColor = LightTheme.Muted,
        Margin = new Padding(0, 6, 0, 0)
    };
    private readonly CheckBox _applyEndpointCheckBox = new() { Text = "Áp dụng IP/Port", Visible = false };
    private readonly CheckBox _applyProtocolCheckBox = new() { Text = "Áp dụng giao thức", Visible = false };
    private readonly CheckBox _applyUsernameCheckBox = new() { Text = "Áp dụng username", Visible = false };
    private readonly CheckBox _applyPasswordCheckBox = new() { Text = "Áp dụng password", Visible = false };
    private readonly Button _checkButton;
    private readonly List<ProxyServer> _targets = [];
    private bool _isAutofilling;

    public ProxyEditorForm(IProxyChecker proxyChecker)
    {
        _proxyChecker = proxyChecker;

        Text = "Sửa thông tin Proxy";
        Width = FormWidth;
        Height = SingleFormHeight;
        MinimumSize = new Size(FormWidth, SingleFormHeight);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        _protocolCombo.DataSource = ProxyProtocolDisplay.Options.ToList();
        _protocolCombo.DisplayMember = nameof(ProxyProtocolOption.DisplayName);
        _protocolCombo.ValueMember = nameof(ProxyProtocolOption.Value);
        _protocolCombo.SelectedValue = ProxyProtocol.Socks5;
        _ipTextBox.TextChanged += (_, _) => TryAutofillFromIpInput();

        _checkButton = new AppButton
        {
            Text = "Check proxy",
            Image = UiIcons.NewCheckProxyHover,
            Height = CheckButtonHeight,
            MinimumSize = new Size(122, CheckButtonHeight),
            Variant = AppButtonVariant.Muted,
            AccentColor = TextColor,
            Margin = Padding.Empty,
            Padding = new Padding(10, 6, 12, 6)
        };
        _checkButton.Click += async (_, _) => await CheckProxyAsync();

        Controls.Add(BuildLayout());
        LightTheme.Apply(this);
        RestoreCustomColors(this);
        LightTheme.SetSecondaryButton(_checkButton, TextColor);
    }

    public ProxyServer? Proxy { get; private set; }

    public void LoadProxy(ProxyServer proxy)
    {
        Proxy = proxy;
        _targets.Clear();
        _targets.Add(proxy);
        _ipTextBox.Text = proxy.Host;
        _portTextBox.Text = proxy.Port > 0 ? proxy.Port.ToString() : string.Empty;
        _protocolCombo.SelectedValue = proxy.Protocol;
        _usernameTextBox.Text = proxy.Username;
        _passwordTextBox.Text = proxy.Password;
        SetBulkMode(false);
    }

    public void LoadProxies(IReadOnlyList<ProxyServer> proxies)
    {
        if (proxies.Count == 0)
        {
            return;
        }

        _targets.Clear();
        _targets.AddRange(proxies);
        Proxy = proxies[0];
        Text = $"Sửa {proxies.Count} proxy";
        _ipTextBox.Text = GetCommonValue(proxies.Select(proxy => proxy.Host));
        _portTextBox.Text = GetCommonValue(proxies.Select(proxy => proxy.Port > 0 ? proxy.Port.ToString() : string.Empty));
        _protocolCombo.SelectedValue = proxies.Select(proxy => proxy.Protocol).Distinct().Count() == 1
            ? proxies[0].Protocol
            : ProxyProtocol.Socks5;
        _usernameTextBox.Text = GetCommonValue(proxies.Select(proxy => proxy.Username ?? string.Empty));
        _passwordTextBox.Text = GetCommonValue(proxies.Select(proxy => proxy.Password ?? string.Empty));
        SetBulkMode(true);
    }

    private Control BuildLayout()
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
        return root;
    }

    private Control CreateContent()
    {
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 12),
            BackColor = Color.White
        };

        var card = new AppCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 12),
            BackColor = Color.White,
            BorderColor = HeaderBorder,
            Radius = 4
        };

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fields.Controls.Add(CreateFieldPanel("Giao thức", _protocolCombo, _applyProtocolCheckBox, new Padding(0, 0, 6, 10)), 0, 0);
        fields.Controls.Add(CreateCheckButtonHost(), 1, 0);
        fields.Controls.Add(CreateFieldPanel("IP", _ipTextBox, _applyEndpointCheckBox, new Padding(0, 0, 6, 10)), 0, 1);
        fields.Controls.Add(CreateFieldPanel("Port", _portTextBox, null, new Padding(6, 0, 0, 10)), 1, 1);
        fields.Controls.Add(CreateFieldPanel("Username", _usernameTextBox, _applyUsernameCheckBox, new Padding(0, 0, 6, 0)), 0, 2);
        fields.Controls.Add(CreateFieldPanel("Password", BuildPasswordInput(), _applyPasswordCheckBox, new Padding(6, 0, 0, 0)), 1, 2);
        fields.Controls.Add(_checkStatusLabel, 0, 3);
        fields.SetColumnSpan(_checkStatusLabel, 2);
        fields.Controls.Add(_bulkHintLabel, 0, 4);
        fields.SetColumnSpan(_bulkHintLabel, 2);

        card.Controls.Add(fields);
        content.Controls.Add(card);
        return content;
    }

    private Control CreateCheckButtonHost()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(6, 4, 0, 10)
        };
        _checkButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        host.Resize += (_, _) =>
        {
            _checkButton.Location = new Point(Math.Max(0, host.ClientSize.Width - _checkButton.Width), 0);
        };
        host.Controls.Add(_checkButton);
        return host;
    }

    private Control CreateFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FooterBackground,
            Padding = new Padding(12, 8, 12, 8),
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
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        var save = new AppPrimaryButton
        {
            Text = "Lưu",
            Width = 46,
            Height = 34,
            MinimumSize = new Size(46, 34),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0)
        };
        save.Click += (_, _) => SaveProxy();
        var cancel = new AppButton
        {
            Text = "Hủy",
            Width = 56,
            Height = 34,
            MinimumSize = new Size(56, 34),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Variant = AppButtonVariant.Muted,
            AccentColor = LightTheme.Muted,
            Tag = LightTheme.Muted,
            Margin = Padding.Empty
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        footer.Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
        return footer;
    }

    private static Control CreateTwoColumnRow(string leftLabel, Control leftInput, Control? leftApply, string rightLabel, Control rightInput, Control? rightApply)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.Controls.Add(CreateFieldPanel(leftLabel, leftInput, leftApply), 0, 0);
        row.Controls.Add(CreateFieldPanel(rightLabel, rightInput, rightApply), 1, 0);
        return row;
    }

    private static Control CreateFieldPanel(string label, Control input, Control? applyControl, Padding? margin = null)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = applyControl is null ? 2 : 3,
            BackColor = Color.White,
            Margin = margin ?? new Padding(0, 0, 8, 8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (applyControl is not null)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        panel.Controls.Add(CreateFieldLabel(label), 0, 0);
        input.Dock = DockStyle.Top;
        input.Margin = Padding.Empty;
        input.MinimumSize = new Size(Math.Max(InputWidth, input.MinimumSize.Width), input.MinimumSize.Height);
        panel.Controls.Add(input, 0, 1);
        if (applyControl is not null)
        {
            applyControl.Margin = new Padding(0, 6, 0, 0);
            panel.Controls.Add(applyControl, 0, 2);
        }

        return panel;
    }

    private static Label CreateFieldLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = TextColor,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 4)
        };

    private Control BuildPasswordInput()
    {
        _passwordTextBox.Dock = DockStyle.Top;
        return _passwordTextBox;
    }

    private void SetBulkMode(bool enabled)
    {
        _bulkHintLabel.Visible = enabled;
        _applyEndpointCheckBox.Visible = enabled;
        _applyProtocolCheckBox.Visible = enabled;
        _applyUsernameCheckBox.Visible = enabled;
        _applyPasswordCheckBox.Visible = enabled;
        _checkButton.Enabled = !enabled;
        _checkStatusLabel.Visible = false;
        Height = enabled ? BulkFormHeight : SingleFormHeight;
        MinimumSize = new Size(FormWidth, Height);
        if (!enabled)
        {
            Text = "Sửa thông tin Proxy";
        }
    }

    private void SaveProxy()
    {
        if (_targets.Count <= 1)
        {
            SaveSingleProxy();
            return;
        }

        SaveManyProxies();
    }

    private void SaveSingleProxy()
    {
        if (!TryBuildProxyFromInputs(out var proxy, out var message))
        {
            MessageBox.Show(this, message, "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Proxy ??= new ProxyServer();
        Proxy.Proxy = proxy.Proxy;
        Proxy.Protocol = proxy.Protocol;
        Proxy.Username = proxy.Username;
        Proxy.Password = proxy.Password;
        DialogResult = DialogResult.OK;
    }

    private void SaveManyProxies()
    {
        if (!_applyEndpointCheckBox.Checked &&
            !_applyProtocolCheckBox.Checked &&
            !_applyUsernameCheckBox.Checked &&
            !_applyPasswordCheckBox.Checked)
        {
            MessageBox.Show(this, "Hãy chọn ít nhất một trường cần áp dụng.", "Chưa có thay đổi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var endpoint = string.Empty;
        if (_applyEndpointCheckBox.Checked && !IsValidEndpointInputs(out endpoint))
        {
            MessageBox.Show(this, "IP và Port phải tạo thành endpoint hợp lệ.", "Dữ liệu không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var parsedProtocol = (ProxyProtocol)_protocolCombo.SelectedValue!;
        foreach (var proxy in _targets)
        {
            if (_applyEndpointCheckBox.Checked)
            {
                proxy.Proxy = endpoint;
            }

            if (_applyProtocolCheckBox.Checked)
            {
                proxy.Protocol = parsedProtocol;
            }

            if (_applyUsernameCheckBox.Checked)
            {
                proxy.Username = NormalizeOptional(_usernameTextBox.Text);
            }

            if (_applyPasswordCheckBox.Checked)
            {
                proxy.Password = NormalizeOptional(_passwordTextBox.Text);
            }
        }

        DialogResult = DialogResult.OK;
    }

    private async Task CheckProxyAsync()
    {
        if (!TryBuildProxyFromInputs(out var proxy, out var message))
        {
            MessageBox.Show(this, message, "Không thể check proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _checkButton.Enabled = false;
        _checkStatusLabel.Visible = true;
        _checkButton.Text = "Đang check...";
        _checkStatusLabel.ForeColor = LightTheme.Muted;
        _checkStatusLabel.Text = "Đang check proxy...";
        try
        {
            var result = await _proxyChecker.CheckAsync(proxy, CancellationToken.None);
            _checkStatusLabel.ForeColor = result.IsReachable ? LightTheme.Success : LightTheme.Danger;
            _checkStatusLabel.Text = result.IsReachable
                ? $"Proxy đang hoạt động: Độ trễ {result.LatencyMs?.ToString() ?? "-"}ms"
                : $"Proxy không hoạt động. {result.ErrorMessage}";
        }
        finally
        {
            _checkButton.Text = "Check proxy";
            _checkButton.Enabled = _targets.Count <= 1;
        }
    }

    private void TryAutofillFromIpInput()
    {
        if (_isAutofilling || !_ipTextBox.Text.Contains(':', StringComparison.Ordinal))
        {
            return;
        }

        if (!TryParseFullProxy(_ipTextBox.Text, out var endpoint, out var username, out var password, out var protocol))
        {
            return;
        }

        var segments = endpoint.Split(':', StringSplitOptions.TrimEntries);
        _isAutofilling = true;
        _ipTextBox.Text = segments[0];
        _portTextBox.Text = segments[1];
        _protocolCombo.SelectedValue = protocol;
        _usernameTextBox.Text = username ?? string.Empty;
        _passwordTextBox.Text = password ?? string.Empty;
        _ipTextBox.SelectionStart = _ipTextBox.Text.Length;
        _isAutofilling = false;
    }

    private bool TryBuildProxyFromInputs(out ProxyServer proxy, out string message)
    {
        proxy = new ProxyServer();
        if (!IsValidEndpointInputs(out var endpoint))
        {
            message = "IP và Port phải tạo thành endpoint hợp lệ.";
            return false;
        }

        proxy.Proxy = endpoint;
        proxy.Protocol = (ProxyProtocol)_protocolCombo.SelectedValue!;
        proxy.Username = NormalizeOptional(_usernameTextBox.Text);
        proxy.Password = NormalizeOptional(_passwordTextBox.Text);
        message = string.Empty;
        return true;
    }

    private bool IsValidEndpointInputs(out string endpoint)
    {
        endpoint = $"{_ipTextBox.Text.Trim()}:{_portTextBox.Text.Trim()}";
        return new ProxyServer { Proxy = endpoint }.HasValidEndpoint;
    }

    private static bool TryParseFullProxy(
        string value,
        out string endpoint,
        out string? username,
        out string? password,
        out ProxyProtocol protocol)
    {
        endpoint = string.Empty;
        username = null;
        password = null;
        protocol = default;

        var segments = value.Split(':', StringSplitOptions.TrimEntries);
        if (segments.Length is not (3 or 5))
        {
            return false;
        }

        endpoint = $"{segments[0]}:{segments[1]}";
        if (!new ProxyServer { Proxy = endpoint }.HasValidEndpoint ||
            !ProxyProtocolDisplay.TryParse(segments[^1], out protocol))
        {
            return false;
        }

        if (segments.Length == 5)
        {
            username = NormalizeOptional(segments[2]);
            password = NormalizeOptional(segments[3]);
        }

        return true;
    }

    private static string? NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetCommonValue(IEnumerable<string> values)
    {
        var distinct = values.Distinct(StringComparer.Ordinal).ToList();
        return distinct.Count == 1 ? distinct[0] : string.Empty;
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
