using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Services;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class AppUpdateForm : Form
{
    private static readonly Color FooterBackground = Color.FromArgb(247, 247, 249);
    private static readonly Color FooterBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);

    private readonly GitHubAppUpdateClient _client;
    private readonly AppRelease? _knownUpdate;
    private readonly Label _titleLabel = new();
    private readonly Label _messageLabel = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _progressLabel = new();
    private readonly AppPrimaryButton _updateButton = new();
    private readonly AppButton _closeButton = new();
    private AppRelease? _release;
    private CancellationTokenSource? _downloadCancellation;
    private bool _downloading;

    public AppUpdateForm(GitHubAppUpdateClient client, AppRelease? knownUpdate = null)
    {
        _client = client;
        _knownUpdate = knownUpdate;
        Text = "Cập nhật ProxyManager";
        Width = 560;
        Height = 320;
        MinimumSize = new Size(560, 320);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LightTheme.Apply(this);
        RestoreBackColors(this);
        Shown += async (_, _) => await LoadAsync();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.White,
            Padding = new Padding(28, 26, 28, 12),
            Margin = Padding.Empty
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _titleLabel.Text = "Đang kiểm tra bản mới";
        _titleLabel.AutoSize = true;
        _titleLabel.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
        _titleLabel.ForeColor = TextColor;
        _titleLabel.BackColor = Color.White;
        _titleLabel.Margin = new Padding(0, 0, 0, 10);

        _messageLabel.Text = "Đang đối chiếu phiên bản hiện tại với bản phát hành mới nhất.";
        _messageLabel.AutoSize = false;
        _messageLabel.Dock = DockStyle.Fill;
        _messageLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        _messageLabel.ForeColor = LightTheme.Muted;
        _messageLabel.BackColor = Color.White;
        _messageLabel.Margin = Padding.Empty;

        _progressBar.Dock = DockStyle.Top;
        _progressBar.Height = 16;
        _progressBar.Margin = new Padding(0, 8, 0, 4);
        _progressBar.Visible = false;

        _progressLabel.AutoSize = true;
        _progressLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _progressLabel.ForeColor = LightTheme.Muted;
        _progressLabel.BackColor = Color.White;
        _progressLabel.Margin = new Padding(0, 0, 0, 4);
        _progressLabel.Visible = false;

        content.Controls.Add(_titleLabel, 0, 0);
        content.Controls.Add(_messageLabel, 0, 1);
        content.Controls.Add(_progressBar, 0, 2);
        content.Controls.Add(_progressLabel, 0, 3);

        root.Controls.Add(content, 0, 0);
        root.Controls.Add(CreateFooter(), 0, 1);
        Controls.Add(root);
    }

    private Control CreateFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FooterBackground,
            Tag = FooterBackground,
            Padding = new Padding(16, 16, 16, 16)
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(FooterBorder);
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

        _updateButton.Text = "Cập nhật";
        _updateButton.Height = 40;
        _updateButton.MinimumSize = new Size(120, 40);
        _updateButton.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _updateButton.Margin = new Padding(0, 0, 10, 0);
        _updateButton.Enabled = false;
        _updateButton.Click += async (_, _) => await DownloadAndApplyAsync();

        _closeButton.Text = "Để sau";
        _closeButton.Height = 40;
        _closeButton.MinimumSize = new Size(96, 40);
        _closeButton.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _closeButton.Variant = AppButtonVariant.Muted;
        _closeButton.Margin = Padding.Empty;
        _closeButton.Click += (_, _) => Close();

        buttons.Controls.Add(_updateButton);
        buttons.Controls.Add(_closeButton);
        footer.Controls.Add(buttons);
        CancelButton = _closeButton;
        return footer;
    }

    private async Task LoadAsync()
    {
        if (_knownUpdate is not null)
        {
            ShowAvailable(_knownUpdate);
            return;
        }

        try
        {
            var latest = await _client.GetLatestAsync(CancellationToken.None);
            if (IsDisposed)
            {
                return;
            }

            if (latest is null)
            {
                ShowMessage(
                    "Chưa có bản cập nhật",
                    "Không tìm thấy file cài đặt trong bản phát hành mới nhất.",
                    canUpdate: false);
                return;
            }

            if (AppUpdateInstaller.CurrentVersion is { } current && latest.Version.CompareTo(current) <= 0)
            {
                ShowMessage(
                    "Đang dùng bản mới nhất",
                    $"ProxyManager v{current.ToDisplayString()} là bản mới nhất.",
                    canUpdate: false);
                return;
            }

            ShowAvailable(latest);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            if (!IsDisposed)
            {
                ShowMessage(
                    "Không kiểm tra được bản mới",
                    "Không kết nối được máy chủ phát hành. Hãy thử lại sau.",
                    canUpdate: false);
            }
        }
    }

    private void ShowAvailable(AppRelease release)
    {
        _release = release;
        var current = AppUpdateInstaller.DisplayVersion;
        ShowMessage(
            $"Đã có bản v{release.Version.ToDisplayString()}",
            $"Bản hiện tại là v{current}. Cập nhật sẽ tải bản mới, đóng ứng dụng rồi mở lại. Tài khoản, proxy và cấu hình đại lý được giữ nguyên.",
            canUpdate: true);
    }

    private void ShowMessage(string title, string message, bool canUpdate)
    {
        _titleLabel.Text = title;
        _messageLabel.Text = message;
        _updateButton.Enabled = canUpdate;
        _updateButton.Visible = canUpdate;
        _closeButton.Text = canUpdate ? "Để sau" : "Đóng";
        _progressBar.Visible = false;
        _progressLabel.Visible = false;
    }

    private async Task DownloadAndApplyAsync()
    {
        if (_release is null || _downloading)
        {
            return;
        }

        var installedPath = AppUpdateInstaller.GetInstalledExecutablePath();
        if (installedPath is null)
        {
            ShowMessage(
                "Không cập nhật được",
                "Không xác định được file đang chạy. Hãy mở ProxyManager.App.exe rồi thử lại.",
                canUpdate: false);
            return;
        }

        try
        {
            AppUpdateInstaller.EnsureInstallDirectoryWritable(installedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowMessage(
                "Không ghi được bản mới",
                "Thư mục đang chạy không cho phép thay file. Hãy chuyển ProxyManager sang một thư mục bạn có quyền ghi, hoặc chạy ứng dụng bằng quyền quản trị.",
                canUpdate: true);
            return;
        }

        _downloading = true;
        _updateButton.Enabled = false;
        _closeButton.Enabled = false;
        _progressBar.Visible = true;
        _progressLabel.Visible = true;
        _progressBar.Value = 0;
        _titleLabel.Text = "Đang tải bản mới";
        _messageLabel.Text = "Đừng tắt máy trong lúc tải. Ứng dụng sẽ tự đóng và mở lại khi xong.";
        _downloadCancellation = new CancellationTokenSource();

        try
        {
            var downloadPath = AppUpdateInstaller.GetDownloadPath();
            var progress = new Progress<AppUpdateDownloadProgress>(ReportProgress);
            await _client.DownloadAsync(_release, downloadPath, progress, _downloadCancellation.Token);
            AppUpdateInstaller.ValidateDownloadedExe(downloadPath, _release.Size);
            AppUpdateInstaller.StartReplaceAndRestart(downloadPath, installedPath);
            Environment.Exit(0);
        }
        catch (OperationCanceledException)
        {
            ShowMessage("Đã hủy cập nhật", "Bản hiện tại vẫn được giữ nguyên.", canUpdate: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or InvalidOperationException)
        {
            ShowMessage(
                "Không tải được bản mới",
                "Kết nối bị gián đoạn hoặc tệp tải về không hợp lệ. Hãy thử lại.",
                canUpdate: true);
        }
        finally
        {
            _downloading = false;
            _closeButton.Enabled = true;
            _downloadCancellation?.Dispose();
            _downloadCancellation = null;
        }
    }

    private void ReportProgress(AppUpdateDownloadProgress progress)
    {
        if (IsDisposed)
        {
            return;
        }

        if (progress.TotalBytes is > 0)
        {
            var percent = (int)Math.Clamp(progress.BytesReceived * 100 / progress.TotalBytes.Value, 0, 100);
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = percent;
            _progressLabel.Text = $"{percent}%";
            return;
        }

        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressLabel.Text = "Đang tải...";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_downloading)
        {
            _downloadCancellation?.Cancel();
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private static void RestoreBackColors(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.Tag is Color color)
            {
                control.BackColor = color;
            }

            RestoreBackColors(control);
        }
    }
}
