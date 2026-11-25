using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Forms;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.App.Controls;

// "Gắn proxy cho toàn bộ máy" on the settings tab: every application without its own rule goes
// through the chosen proxy. This control owns the choice (AppSettings.SystemProxyId) and its UI;
// what the running session does about a change stays with MainForm via SystemProxyChangedAsync.
// See docs/10-system-wide-proxy.md.
public sealed class SystemProxySettingsControl : UserControl
{
    private static readonly Color CardBackground = Color.FromArgb(250, 250, 250);
    private static readonly Color ValueTextColor = Color.FromArgb(23, 23, 23);

    private readonly IAppSettingsRepository _settingsRepository;
    private readonly IProxyRepository _proxyRepository;
    private readonly ProxyManagementService _proxyManagementService;
    private readonly IServiceProvider _serviceProvider;
    private readonly CheckBox _enabledCheckBox = new()
    {
        Text = "Gắn proxy cho toàn bộ máy",
        AutoSize = true
    };
    private readonly Label _proxyValueLabel = new();
    private readonly AppButton _pickButton = new()
    {
        Text = "Đổi proxy",
        Height = 28,
        MinimumSize = new Size(48, 28),
        Margin = new Padding(6, 0, 0, 0),
        Padding = new Padding(8, 3, 8, 3),
        Variant = AppButtonVariant.Muted,
        AccentColor = LightTheme.Muted,
        Filled = false
    };
    private readonly Label _hintLabel = new();
    private bool _isUpdatingUi;

    public SystemProxySettingsControl(
        IAppSettingsRepository settingsRepository,
        IProxyRepository proxyRepository,
        ProxyManagementService proxyManagementService,
        IServiceProvider serviceProvider)
    {
        _settingsRepository = settingsRepository;
        _proxyRepository = proxyRepository;
        _proxyManagementService = proxyManagementService;
        _serviceProvider = serviceProvider;

        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = CardBackground;
        Margin = new Padding(0, 12, 0, 0);

        BuildUi();
        _enabledCheckBox.CheckedChanged += EnabledCheckBoxCheckedChanged;
        _pickButton.Click += async (_, _) => await PickProxyAsync(confirm: false);
    }

    public Func<Task<bool>>? LoginRequestedAsync { get; set; }

    // Invoked after the choice was saved, with the new SystemProxyId (null = switched off).
    public Func<Guid?, Task>? SystemProxyChangedAsync { get; set; }

    public event EventHandler<ProfileAffectingChange>? ProfileAffectingChanged;

    public async Task LoadAsync()
    {
        var settings = await _settingsRepository.GetAsync(CancellationToken.None);
        var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
        var state = SystemProxyEligibility.Evaluate(settings.SystemProxyId, proxies, DateTimeOffset.Now);

        _isUpdatingUi = true;
        try
        {
            _enabledCheckBox.Checked = settings.SystemProxyId is not null;
        }
        finally
        {
            _isUpdatingUi = false;
        }

        _proxyValueLabel.Text = state.Proxy is null
            ? (settings.SystemProxyId is null ? "Chưa chọn proxy" : "Proxy đã chọn không còn tồn tại")
            : FormatProxy(state.Proxy);
        _pickButton.Enabled = settings.SystemProxyId is not null;

        if (state.State == SystemProxyState.Unusable)
        {
            _hintLabel.ForeColor = LightTheme.Danger;
            _hintLabel.Text =
                $"{state.Message} Khi đang dùng proxy, các ứng dụng chưa gắn proxy riêng sẽ bị chặn mạng. Hãy đổi proxy khác hoặc tắt chế độ này.";
        }
        else
        {
            _hintLabel.ForeColor = LightTheme.Muted;
            _hintLabel.Text =
                "Mọi ứng dụng chưa gắn proxy riêng sẽ đi qua proxy này. Ứng dụng đã gắn proxy riêng vẫn giữ proxy của nó.";
        }
    }

    private void BuildUi()
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = CardBackground,
            Margin = Padding.Empty
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _enabledCheckBox.BackColor = CardBackground;
        _enabledCheckBox.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _enabledCheckBox.Margin = new Padding(3, 0, 0, 6);
        section.Controls.Add(_enabledCheckBox, 0, 0);

        var proxyRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = CardBackground,
            Margin = new Padding(0, 0, 0, 6)
        };
        proxyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        proxyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _proxyValueLabel.AutoSize = false;
        _proxyValueLabel.AutoEllipsis = true;
        _proxyValueLabel.Dock = DockStyle.Fill;
        _proxyValueLabel.Height = 28;
        _proxyValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        _proxyValueLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _proxyValueLabel.ForeColor = ValueTextColor;
        _proxyValueLabel.BackColor = CardBackground;
        _proxyValueLabel.Margin = new Padding(3, 0, 8, 0);
        proxyRow.Controls.Add(_proxyValueLabel, 0, 0);

        LightTheme.SetSecondaryButton(_pickButton, LightTheme.Muted);
        proxyRow.Controls.Add(_pickButton, 1, 0);
        section.Controls.Add(proxyRow, 0, 1);

        _hintLabel.AutoSize = true;
        _hintLabel.MaximumSize = new Size(420, 0);
        _hintLabel.BackColor = CardBackground;
        _hintLabel.Margin = Padding.Empty;
        section.Controls.Add(_hintLabel, 0, 2);

        Controls.Add(section);
    }

    // Credentials are deliberately left out: this text sits in plain sight on the settings tab.
    private static string FormatProxy(ProxyServer proxy) =>
        $"{proxy.Proxy} ({proxy.Protocol.ToDisplayToken()})";

    private async void EnabledCheckBoxCheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingUi)
        {
            return;
        }

        if (!_enabledCheckBox.Checked)
        {
            await ApplyAsync(null);
            return;
        }

        await PickProxyAsync(confirm: true);
    }

    // Uses the same picker as "Gắn Proxy" on the Ứng dụng tab, so every purchased order (tĩnh, xoay,
    // datacenter) and every manual proxy is offered, not only the ones already stored locally.
    private async Task PickProxyAsync(bool confirm)
    {
        try
        {
            var selectedProxy = await ChangeApplicationProxyForm.PickProxyAsync(
                this,
                _serviceProvider,
                _proxyManagementService,
                "Gắn proxy cho toàn bộ máy",
                LoginRequestedAsync,
                change => ProfileAffectingChanged?.Invoke(this, change),
                [],
                []);
            if (selectedProxy is null)
            {
                return;
            }

            var proxies = await _proxyRepository.GetAllAsync(CancellationToken.None);
            var eligibility = SystemProxyEligibility.Evaluate(selectedProxy.Id, proxies, DateTimeOffset.Now);
            if (!eligibility.IsUsable)
            {
                UiFeedback.ShowWarning(this, eligibility.Message, "Không thể gắn proxy cho toàn bộ máy");
                return;
            }

            if (confirm)
            {
                var answer = MessageBox.Show(
                    this,
                    $"Mọi ứng dụng trên máy chưa gắn proxy riêng sẽ đi qua proxy {FormatProxy(selectedProxy)}.{Environment.NewLine}{Environment.NewLine}" +
                    "Lưu ý: kết nối UDP (game, gọi thoại/video) có thể không hoạt động, và nếu proxy ngừng hoạt động thì các ứng dụng này sẽ mất mạng cho tới khi bạn đổi proxy hoặc tắt chế độ này.",
                    "Gắn proxy cho toàn bộ máy",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.OK)
                {
                    return;
                }
            }

            await ApplyAsync(selectedProxy.Id);
        }
        catch (InvalidOperationException ex)
        {
            UiFeedback.ShowWarning(this, ex.Message, "Không thể gắn proxy cho toàn bộ máy");
        }
        finally
        {
            // Covers every way out without a saved choice (cancel, invalid proxy): the checkbox
            // goes back to what the settings actually say.
            await LoadAsync();
        }
    }

    private async Task ApplyAsync(Guid? proxyId)
    {
        try
        {
            var settings = await _settingsRepository.GetAsync(CancellationToken.None);
            if (settings.SystemProxyId == proxyId)
            {
                return;
            }

            settings.SystemProxyId = proxyId;
            await _settingsRepository.SaveAsync(settings, CancellationToken.None);
            await LoadAsync();
            if (SystemProxyChangedAsync is { } changed)
            {
                await changed(proxyId);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            UiFeedback.ShowWarning(this, ex.Message, "Không thể lưu cài đặt proxy toàn bộ máy");
            await LoadAsync();
        }
    }
}
