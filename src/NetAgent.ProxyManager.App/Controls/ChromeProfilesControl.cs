using Microsoft.Extensions.DependencyInjection;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Forms;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.App.Controls;

// Tab "Chrome Profile": each row is one tool-owned Chrome instance (its own --user-data-dir) with
// exactly one proxy. Routing itself happens in Proxifier via the NetworkService PID — this control
// only manages the profiles and asks MainForm (OpenProfileRequestedAsync) to open one, because
// opening must first make sure the proxy session and its leak-guard rule are active.
public sealed class ChromeProfilesControl : UserControl
{
    private const string ActionsColumnName = "Actions";
    private const int DesignedToolbarButtonHeight = 40;
    private const string OpenActionText = "Mở";
    private const string CloseActionText = "Đóng";
    private const string AssignProxyActionText = "Gắn proxy";
    private const string ChangeProxyActionText = "Đổi proxy";
    private const string RenameActionText = "Đổi tên";
    private const string DeleteActionText = "Xoá";

    private readonly ApplicationRuleService _applicationRuleService;
    private readonly ProxyManagementService _proxyManagementService;
    private readonly ChromeProfileService _chromeProfileService;
    private readonly IChromeBrowserProvisioner _browserProvisioner;
    private readonly IServiceProvider _serviceProvider;
    private readonly DataGridView _grid = new AppDataGridView();
    private readonly Label _statusLabel = AppDataGridFooter.CreateStatusLabel();
    private readonly Label _hintLabel = new();

    private List<ApplicationRule> _profiles = [];
    private List<ProxyServer> _proxies = [];
    private IReadOnlyList<ChromeProfileRuntimeInfo> _runtimes = [];
    private bool _isBusy;

    public ChromeProfilesControl(
        ApplicationRuleService applicationRuleService,
        ProxyManagementService proxyManagementService,
        ChromeProfileService chromeProfileService,
        IChromeBrowserProvisioner browserProvisioner,
        IServiceProvider serviceProvider)
    {
        _applicationRuleService = applicationRuleService;
        _proxyManagementService = proxyManagementService;
        _chromeProfileService = chromeProfileService;
        _browserProvisioner = browserProvisioner;
        _serviceProvider = serviceProvider;

        Dock = DockStyle.Fill;
        BuildUi();
        LightTheme.Apply(this);
        DesignedGridTheme.Apply(_grid);
    }

    public event EventHandler<ProfileAffectingChange>? ProfileAffectingChanged;

    public Func<Task<bool>>? LoginRequestedAsync { get; set; }

    // Implemented by MainForm: ensure the proxy session (with leak guard) is running, then launch.
    public Func<ApplicationRule, IProgress<ChromeBrowserDownloadProgress>, Task<bool>>? OpenProfileRequestedAsync { get; set; }

    public async Task LoadAsync()
    {
        _profiles = (await _applicationRuleService.GetChromeProfilesAsync(CancellationToken.None))
            .OrderBy(rule => rule.GetApplicationName(), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
        try
        {
            _runtimes = await _chromeProfileService.ScanAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is System.Management.ManagementException or UnauthorizedAccessException or InvalidOperationException)
        {
            _runtimes = [];
        }

        BindGrid();
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
        ConfigureGrid();
        root.Controls.Add(_grid, 0, 1);
        _statusLabel.Dock = DockStyle.Fill;
        root.Controls.Add(_statusLabel, 0, 2);
        Controls.Add(root);
    }

    private Control BuildToolbar()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _hintLabel.AutoSize = true;
        _hintLabel.Anchor = AnchorStyles.Left;
        _hintLabel.ForeColor = DesignedGridTheme.MutedColor;
        _hintLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        _hintLabel.Text = "Mỗi Chrome profile có cookie/đăng nhập riêng và luôn đi qua đúng 1 proxy. " +
            "Nếu proxy gặp sự cố, Chrome của profile sẽ bị chặn mạng thay vì lộ IP thật.";

        var toolbar = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        toolbar.Controls.Add(CreateToolbarButton("Tạo profile", UiIcons.NewAddApplication, UiIcons.NewAddApplicationHover, async (_, _) => await CreateProfileAsync(), 140));
        toolbar.Controls.Add(CreateToolbarButton("Làm mới", UiIcons.NewReset, UiIcons.NewResetHover, async (_, _) => await RunBusyAsync(LoadAsync), 106));

        row.Controls.Add(_hintLabel, 0, 0);
        row.Controls.Add(toolbar, 1, 0);
        return row;
    }

    private static Button CreateToolbarButton(string text, Image icon, Image hoverIcon, EventHandler onClick, int minimumWidth)
    {
        var button = new DesignedToolbarButton
        {
            Text = text,
            Image = icon,
            HoverImage = hoverIcon,
            RestTextColor = DesignedGridTheme.TextColor,
            HoverTextColor = DesignedGridTheme.AccentColor,
            MinimumSize = new Size(minimumWidth, DesignedToolbarButtonHeight),
            Height = DesignedToolbarButtonHeight,
            Padding = new Padding(6),
            Margin = new Padding(0, 0, 8, 0),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular)
        };
        button.Click += onClick;
        return button;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.Margin = Padding.Empty;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(CreateTextColumn("Tên profile", nameof(ChromeProfileRow.Name), 180, 22));
        _grid.Columns.Add(CreateTextColumn("Proxy", nameof(ChromeProfileRow.ProxyText), 200, 26));
        _grid.Columns.Add(CreateTextColumn("Trạng thái", nameof(ChromeProfileRow.StatusText), 200, 24));
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ActionsColumnName,
            HeaderText = "Thao tác",
            MinimumWidth = 320,
            FillWeight = 28,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.CellPainting += PaintActionCell;
        _grid.CellFormatting += FormatStatusCell;
        _grid.CellClick += async (_, e) => await HandleGridCellClickAsync(e);
        _grid.MouseMove += (_, e) => _grid.Cursor = GetActionAt(e.Location) is null ? Cursors.Default : Cursors.Hand;
        _ = new GridEmptyStateOverlay(_grid, () => _profiles.Count == 0
            ? GridEmptyStateContent.TextOnly("Chưa có Chrome profile nào. Bấm \"Tạo profile\" để bắt đầu.")
            : null);
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string header, string propertyName, int minimumWidth, float fillWeight) =>
        new()
        {
            HeaderText = header,
            DataPropertyName = propertyName,
            Name = propertyName,
            MinimumWidth = minimumWidth,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

    private void BindGrid()
    {
        var proxiesById = _proxies.ToDictionary(proxy => proxy.Id);
        _grid.DataSource = _profiles.Select(rule => BuildRow(rule, proxiesById)).ToList();
        DesignedGridTheme.ApplyMetrics(_grid);
        var openCount = _profiles.Count(IsRunning);
        _statusLabel.Text = $"{_profiles.Count} Chrome profile · {openCount} đang mở";
        _grid.Invalidate();
    }

    private ChromeProfileRow BuildRow(ApplicationRule rule, IReadOnlyDictionary<Guid, ProxyServer> proxiesById)
    {
        var eligibility = ApplicationRuleEligibility.Evaluate(rule, proxiesById);
        var proxyText = rule.AssignedProxyId is { } proxyId && proxiesById.TryGetValue(proxyId, out var proxy)
            ? proxy.DisplayValue
            : "Chưa gắn proxy";
        var isRunning = IsRunning(rule);
        var (statusText, statusKind) = (isRunning, eligibility.CanUseProxy) switch
        {
            (true, true) => ("Đang mở", ChromeProfileStatusKind.Running),
            (true, false) => ($"Đang mở · bị chặn mạng ({eligibility.Message})", ChromeProfileStatusKind.Warning),
            (false, true) => ("Đã đóng", ChromeProfileStatusKind.Idle),
            _ => (eligibility.Message, ChromeProfileStatusKind.Warning)
        };

        return new ChromeProfileRow(rule.Id, rule.GetApplicationName(), proxyText, statusText, statusKind, isRunning, rule.AssignedProxyId is not null);
    }

    private bool IsRunning(ApplicationRule rule) => ChromeProfileService.FindRuntime(rule, _runtimes) is not null;

    private void FormatStatusCell(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            _grid.Columns[e.ColumnIndex].Name != nameof(ChromeProfileRow.StatusText) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ChromeProfileRow row)
        {
            return;
        }

        e.CellStyle!.ForeColor = row.StatusKind switch
        {
            ChromeProfileStatusKind.Running => Color.FromArgb(16, 124, 16),
            ChromeProfileStatusKind.Warning => DesignedGridTheme.DangerColor,
            _ => DesignedGridTheme.MutedColor
        };
    }

    private static IReadOnlyList<string> GetActionTexts(ChromeProfileRow row) =>
    [
        row.IsRunning ? CloseActionText : OpenActionText,
        row.HasProxy ? ChangeProxyActionText : AssignProxyActionText,
        RenameActionText,
        DeleteActionText
    ];

    private void PaintActionCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.ColumnIndex < 0 ||
            e.Graphics is null ||
            _grid.Columns[e.ColumnIndex].Name != ActionsColumnName ||
            _grid.Rows[e.RowIndex].DataBoundItem is not ChromeProfileRow row)
        {
            return;
        }

        DesignedGridTheme.PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: false, isSelected: DesignedGridTheme.IsSelected(e));
        foreach (var (bounds, text) in GridCellButtonRenderer.GetCenteredButtonLayouts(e.CellBounds, GetActionTexts(row)))
        {
            var accent = text == DeleteActionText ? DesignedGridTheme.DangerColor : DesignedGridTheme.AccentColor;
            GridCellButtonRenderer.DrawOutlineButton(e.Graphics, bounds, text, accent);
        }

        e.Handled = true;
    }

    private (ChromeProfileRow Row, string Action)? GetActionAt(Point location)
    {
        var hit = _grid.HitTest(location.X, location.Y);
        if (hit.RowIndex < 0 ||
            hit.ColumnIndex < 0 ||
            _grid.Columns[hit.ColumnIndex].Name != ActionsColumnName ||
            _grid.Rows[hit.RowIndex].DataBoundItem is not ChromeProfileRow row)
        {
            return null;
        }

        var cellBounds = _grid.GetCellDisplayRectangle(hit.ColumnIndex, hit.RowIndex, cutOverflow: false);
        foreach (var (bounds, text) in GridCellButtonRenderer.GetCenteredButtonLayouts(cellBounds, GetActionTexts(row)))
        {
            if (bounds.Contains(location))
            {
                return (row, text);
            }
        }

        return null;
    }

    private async Task HandleGridCellClickAsync(DataGridViewCellEventArgs e)
    {
        if (_isBusy || e.RowIndex < 0 || GetActionAt(_grid.PointToClient(Cursor.Position)) is not { } hit)
        {
            return;
        }

        var rule = _profiles.FirstOrDefault(item => item.Id == hit.Row.Id);
        if (rule is null)
        {
            await RunBusyAsync(LoadAsync);
            return;
        }

        switch (hit.Action)
        {
            case OpenActionText:
                await RunBusyAsync(() => OpenProfileAsync(rule));
                break;
            case CloseActionText:
                await RunBusyAsync(() => CloseProfileAsync(rule));
                break;
            case AssignProxyActionText:
            case ChangeProxyActionText:
                await RunBusyAsync(() => ChangeProxyAsync(rule));
                break;
            case RenameActionText:
                await RunBusyAsync(() => RenameProfileAsync(rule));
                break;
            case DeleteActionText:
                await RunBusyAsync(() => DeleteProfileAsync(rule));
                break;
        }
    }

    private async Task CreateProfileAsync()
    {
        using var form = _serviceProvider.GetRequiredService<ChromeProfileNameForm>();
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var rule = _chromeProfileService.CreateProfile(form.ProfileName);
        _profiles.Add(rule);
        await SaveAsync();
        await LoadAsync();

        var answer = MessageBox.Show(
            this,
            $"Đã tạo Chrome profile \"{rule.GetApplicationName()}\". Gắn proxy cho profile này ngay bây giờ?",
            "Chrome Profile",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer == DialogResult.Yes)
        {
            await RunBusyAsync(() => ChangeProxyAsync(rule));
        }
    }

    private async Task OpenProfileAsync(ApplicationRule rule)
    {
        var eligibility = ApplicationRuleEligibility.Evaluate(rule, _proxies);
        if (!eligibility.CanUseProxy)
        {
            // Opening without a usable proxy would only show a Chrome that the leak guard blocks.
            UiFeedback.ShowWarning(this, $"Hãy gắn proxy hợp lệ trước khi mở Chrome profile. ({eligibility.Message})", "Chưa thể mở");
            return;
        }

        if (!_browserProvisioner.IsInstalled)
        {
            var answer = MessageBox.Show(
                this,
                "Lần đầu dùng Chrome Profile, tool cần tải trình duyệt riêng (khoảng 200MB). Tiếp tục?",
                "Tải trình duyệt",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }
        }

        if (OpenProfileRequestedAsync is null)
        {
            return;
        }

        var progress = new Progress<ChromeBrowserDownloadProgress>(report =>
            _statusLabel.Text = report.Percent is { } percent ? $"{report.Stage} {percent}%" : report.Stage);
        _statusLabel.Text = $"Đang mở \"{rule.GetApplicationName()}\"...";
        await OpenProfileRequestedAsync(rule, progress);
        await LoadAsync();
    }

    private async Task CloseProfileAsync(ApplicationRule rule)
    {
        _statusLabel.Text = $"Đang đóng \"{rule.GetApplicationName()}\"...";
        await _chromeProfileService.CloseAsync(rule, CancellationToken.None);
        if (FindProfile(rule.Id) is { } current)
        {
            current.ProcessId = null;
        }

        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Đóng Chrome profile", [rule.Id]));
        await LoadAsync();
    }

    private async Task ChangeProxyAsync(ApplicationRule rule)
    {
        _proxies = (await _proxyManagementService.GetAllAsync(CancellationToken.None)).ToList();
        var allRules = await _applicationRuleService.GetAllAsync(CancellationToken.None);
        using var form = _serviceProvider.GetRequiredService<ChangeApplicationProxyForm>();
        form.LoginRequestedAsync = LoginRequestedAsync;
        var isAssignMode = rule.AssignedProxyId is null;
        form.Text = isAssignMode ? "Gắn Proxy" : "Đổi Proxy";
        form.ProfileAffectingChanged += (_, change) => OnProfileAffectingChanged(change);
        form.LoadContext(allRules, _proxies);
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

        var probe = new ApplicationRule
        {
            TargetType = ApplicationTargetType.ChromeProfile,
            ChromeUserDataDirectory = rule.ChromeUserDataDirectory,
            AssignedProxyId = selectedProxy.Id
        };
        var eligibility = ApplicationRuleEligibility.Evaluate(probe, [selectedProxy]);
        if (!eligibility.CanUseProxy)
        {
            UiFeedback.ShowWarning(this, eligibility.Message, "Không thể gắn Proxy");
            return;
        }

        // LoadAsync rebuilds _profiles from disk (after "Tạo profile", and whenever MainForm refreshes
        // this tab while the dialog is open), so the instance passed in may be a stale copy that
        // SaveAsync would never write. Always edit the instance that is actually in _profiles.
        if (FindProfile(rule.Id) is not { } target)
        {
            await LoadAsync();
            target = FindProfile(rule.Id);
        }

        if (target is null)
        {
            UiFeedback.ShowWarning(this, "Chrome profile này không còn tồn tại.", "Không thể gắn Proxy");
            return;
        }

        rule = target;
        rule.AssignedProxyId = selectedProxy.Id;
        rule.AutoAssignProxy = false;
        rule.IsEnabled = true;
        rule.Warning = null;
        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules(
            isAssignMode ? "Gắn proxy Chrome profile" : "Đổi proxy Chrome profile",
            [rule.Id]));
        await LoadAsync();
        rule = FindProfile(rule.Id) ?? rule;

        if (!IsRunning(rule))
        {
            UiFeedback.ShowInfo(this, $"Đã gắn proxy {selectedProxy.DisplayValue} cho \"{rule.GetApplicationName()}\".");
            return;
        }

        // Connections Chrome already opened keep using the previous proxy until they are recycled.
        var restart = MessageBox.Show(
            this,
            $"\"{rule.GetApplicationName()}\" đang mở. Khởi động lại Chrome để chắc chắn mọi kết nối đi qua proxy mới?",
            "Đổi proxy",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (restart == DialogResult.Yes)
        {
            await _chromeProfileService.CloseAsync(rule, CancellationToken.None);
            await SaveAsync();
            await OpenProfileAsync(rule);
        }
    }

    private async Task RenameProfileAsync(ApplicationRule rule)
    {
        using var form = _serviceProvider.GetRequiredService<ChromeProfileNameForm>();
        form.ConfigureForRename(rule.GetApplicationName());
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (FindProfile(rule.Id) is not { } current)
        {
            UiFeedback.ShowWarning(this, "Chrome profile này không còn tồn tại.", "Không thể đổi tên");
            return;
        }

        current.ChromeProfileName = form.ProfileName;
        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Đổi tên Chrome profile", [rule.Id]));
        await LoadAsync();
    }

    private async Task DeleteProfileAsync(ApplicationRule rule)
    {
        var answer = MessageBox.Show(
            this,
            $"Xoá Chrome profile \"{rule.GetApplicationName()}\"?\n\nChrome của profile sẽ bị đóng và toàn bộ dữ liệu " +
            "(cookie, tài khoản đăng nhập, lịch sử) sẽ bị xoá vĩnh viễn.",
            "Xoá Chrome profile",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            return;
        }

        _statusLabel.Text = $"Đang xoá \"{rule.GetApplicationName()}\"...";
        await _chromeProfileService.DeleteProfileDataAsync(rule, CancellationToken.None);
        _profiles.RemoveAll(item => item.Id == rule.Id);
        await SaveAsync();
        OnProfileAffectingChanged(ProfileAffectingChange.ForRules("Xoá Chrome profile", [rule.Id]));
        await LoadAsync();
    }

    private ApplicationRule? FindProfile(Guid id) => _profiles.Find(item => item.Id == id);

    private Task SaveAsync() => _applicationRuleService.SaveChromeProfilesAsync(_profiles, CancellationToken.None);

    private void OnProfileAffectingChanged(ProfileAffectingChange change) =>
        ProfileAffectingChanged?.Invoke(this, change);

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        UseWaitCursor = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(this, ex, "Chrome Profile");
            try
            {
                await LoadAsync();
            }
            catch (Exception reloadException)
            {
                _statusLabel.Text = reloadException.Message;
            }
        }
        finally
        {
            UseWaitCursor = false;
            _isBusy = false;
        }
    }

    private enum ChromeProfileStatusKind
    {
        Idle,
        Running,
        Warning
    }

    private sealed record ChromeProfileRow(
        Guid Id,
        string Name,
        string ProxyText,
        string StatusText,
        ChromeProfileStatusKind StatusKind,
        bool IsRunning,
        bool HasProxy);
}
