using System.Globalization;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class DepositQrPaymentForm : Form
{
    private const int PaymentTimeoutSeconds = 10 * 60;
    private const int PollIntervalSeconds = 5;

    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);
    private static readonly Color FieldBorder = Color.FromArgb(212, 212, 212);
    private static readonly Color PanelBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color TransferInfoBackground = ColorTranslator.FromHtml("#F7F7F9");
    private static readonly Color QrHeaderBackground = ColorTranslator.FromHtml("#CFE4FA");
    private static readonly Color PurchaseAccent = Color.FromArgb(15, 108, 189);
    private static readonly Color Orange = Color.FromArgb(232, 119, 34);
    private static readonly Color CountdownBack = ColorTranslator.FromHtml("#FFF7E6");

    private readonly IDepositApiClient _depositApiClient;
    private readonly DepositTransaction _transaction;
    private readonly System.Windows.Forms.Timer _countdownTimer = new();
    private readonly System.Windows.Forms.Timer _pollTimer = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Label _countdownLabel = new();
    private readonly Label _statusLabel = new();
    private readonly PictureBox _qrPictureBox = new();
    private int _remainingSeconds = PaymentTimeoutSeconds;
    private bool _polling;
    private bool _completed;
    private bool _expired;

    public DepositQrPaymentForm(IDepositApiClient depositApiClient, DepositTransaction transaction)
    {
        _depositApiClient = depositApiClient;
        _transaction = transaction;

        Text = "Nạp tiền thanh toán";
        Width = 940;
        Height = 725;
        MinimumSize = new Size(840, 625);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        ConfigureTimers();
        LightTheme.Apply(this);
        RestoreDesignedColors(this);

        Shown += async (_, _) => await LoadQrImageAsync();
        FormClosed += (_, _) => DisposeRuntimeResources();
    }

    public event EventHandler? PaymentCompleted;

    public bool IsPaymentCompleted => _completed;

    public bool IsPaymentExpired => _expired;

    private void BuildUi()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 22, 24, 20),
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 374));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(CreatePaymentInfoPanel(), 0, 0);
        layout.Controls.Add(CreateQrPanel(), 1, 0);

        var notice = CreateBottomNotice();
        layout.Controls.Add(notice, 0, 1);
        layout.SetColumnSpan(notice, 2);

        Controls.Add(layout);
    }

    private Control CreatePaymentInfoPanel()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = TransferInfoBackground,
            Tag = TransferInfoBackground,
            BorderColor = PanelBorder,
            BorderRadius = 4,
            Padding = new Padding(18, 18, 18, 16),
            Margin = new Padding(0, 0, 22, 0)
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = TransferInfoBackground,
            Tag = TransferInfoBackground
        };
        for (var index = 0; index < 5; index++)
        {
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        stack.Controls.Add(new PaymentField("Ngân hàng", FirstNonEmpty(_transaction.BankName, _transaction.BankCode, "-")), 0, 0);
        stack.Controls.Add(new PaymentField("Số tài khoản", FirstNonEmpty(_transaction.AccountNumber, "-"), copyable: true), 0, 1);
        stack.Controls.Add(new PaymentField("Chủ tài khoản", FirstNonEmpty(_transaction.AccountName, "-")), 0, 2);
        stack.Controls.Add(new PaymentField("Số tiền", FormatVnd(_transaction.Amount)), 0, 3);
        stack.Controls.Add(new PaymentField("Nội dung chuyển khoản", FirstNonEmpty(_transaction.Description, _transaction.CodeOrId()), copyable: true, valueColor: LightTheme.Accent), 0, 4);

        panel.Controls.Add(stack);
        return panel;
    }

    private Control CreateQrPanel()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Tag = Color.White,
            BorderColor = PurchaseAccent,
            BorderRadius = 5,
            BorderWidth = 2,
            Padding = new Padding(2),
            Margin = Padding.Empty
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.White,
            Tag = Color.White,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

        layout.Controls.Add(CreateQrHeader(), 0, 0);

        var qrHost = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Tag = Color.White,
            BorderColor = FieldBorder,
            BorderRadius = 4,
            Padding = new Padding(14),
            Margin = new Padding(24, 18, 24, 14)
        };
        _qrPictureBox.Dock = DockStyle.Fill;
        _qrPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _qrPictureBox.BackColor = Color.White;
        qrHost.Controls.Add(_qrPictureBox);
        layout.Controls.Add(qrHost, 0, 1);

        var countdownBadge = new CountdownBadge(_countdownLabel)
        {
            Anchor = AnchorStyles.None,
            Width = 188,
            Height = 48,
            Margin = new Padding(0, 0, 0, 16)
        };
        layout.Controls.Add(countdownBadge, 0, 2);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.ForeColor = LightTheme.Muted;
        _statusLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _statusLabel.Text = string.IsNullOrWhiteSpace(_transaction.QrCode) ? "Chưa có mã QR" : string.Empty;
        layout.Controls.Add(_statusLabel, 0, 3);

        panel.Controls.Add(layout);
        UpdateCountdownLabel();
        return panel;
    }

    private static Control CreateQrHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = QrHeaderBackground,
            Tag = QrHeaderBackground,
            Margin = Padding.Empty,
            ColumnCount = 1,
            RowCount = 1
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var content = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            BackColor = QrHeaderBackground,
            Tag = QrHeaderBackground,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 2,
            RowCount = 1
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var icon = new PictureBox
        {
            Image = SidebarIconRenderer.Load("new-design/qr-icon.svg", TextColor, 22),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Width = 28,
            Height = 28,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 10, 0),
            BackColor = QrHeaderBackground,
            Tag = QrHeaderBackground
        };
        var label = new Label
        {
            Text = "Quét mã QR chuyển khoản",
            AutoSize = true,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.None,
            Margin = Padding.Empty,
            BackColor = QrHeaderBackground,
            Tag = QrHeaderBackground
        };
        content.Controls.Add(icon, 0, 0);
        content.Controls.Add(label, 1, 0);
        header.Controls.Add(content, 0, 0);
        return header;
    }

    private static Control CreateBottomNotice()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Margin = new Padding(0, 12, 0, 0),
            BackColor = Color.White
        };
        var icon = new PictureBox
        {
            Image = SidebarIconRenderer.LoadOriginal("new-design/luu-y-icon-payment-form.svg", 24),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Width = 28,
            Height = 28,
            Location = new Point(0, 7)
        };
        var text = new Label
        {
            Text = "Ví tiền sẽ được nạp ngay sau khi thanh toán thành công",
            AutoSize = true,
            Font = new Font("Segoe UI", 11.5f, FontStyle.Regular),
            ForeColor = TextColor,
            Location = new Point(38, 8)
        };
        panel.Controls.Add(icon);
        panel.Controls.Add(text);
        return panel;
    }

    private void ConfigureTimers()
    {
        _countdownTimer.Interval = 1000;
        _countdownTimer.Tick += (_, _) =>
        {
            if (_completed || _expired)
            {
                return;
            }

            _remainingSeconds = Math.Max(0, _remainingSeconds - 1);
            UpdateCountdownLabel();
            if (_remainingSeconds == 0)
            {
                StopTimers();
                _statusLabel.Text = "Mã QR đã hết hạn";
                _statusLabel.ForeColor = LightTheme.Danger;
                _expired = true;
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        _pollTimer.Interval = PollIntervalSeconds * 1000;
        _pollTimer.Tick += async (_, _) => await PollTransactionStatusAsync();

        _countdownTimer.Start();
        _pollTimer.Start();
    }

    private async Task LoadQrImageAsync()
    {
        if (string.IsNullOrWhiteSpace(_transaction.QrCode))
        {
            return;
        }

        try
        {
            var image = await LoadImageAsync(_transaction.QrCode, _disposeCts.Token);
            if (_disposeCts.IsCancellationRequested)
            {
                image.Dispose();
                return;
            }

            var oldImage = _qrPictureBox.Image;
            _qrPictureBox.Image = image;
            oldImage?.Dispose();
            _statusLabel.Text = string.Empty;
        }
        catch
        {
            if (!_disposeCts.IsCancellationRequested)
            {
                _statusLabel.Text = "Không tải được mã QR";
                _statusLabel.ForeColor = LightTheme.Danger;
            }
        }
    }

    private async Task PollTransactionStatusAsync()
    {
        if (_polling || _completed || _remainingSeconds <= 0 || string.IsNullOrWhiteSpace(_transaction.Id))
        {
            return;
        }

        _polling = true;
        try
        {
            var latest = await _depositApiClient.GetDepositTransactionAsync(_transaction.Id, _disposeCts.Token);
            if (latest.IsPaid)
            {
                MarkPaid();
            }
            else if (latest.IsCancelled)
            {
                StopTimers();
                _statusLabel.Text = "Giao dịch đã hủy";
                _statusLabel.ForeColor = LightTheme.Danger;
                _countdownLabel.Text = "Đã hủy";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Keep polling until the countdown expires; transient network errors should not close the payment flow.
        }
        finally
        {
            _polling = false;
        }
    }

    private void MarkPaid()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        StopTimers();
        _statusLabel.Text = "Thanh toán thành công";
        _statusLabel.ForeColor = LightTheme.Success;
        _countdownLabel.Text = "Hoàn tất";
        _countdownLabel.ForeColor = LightTheme.Success;
        PaymentCompleted?.Invoke(this, EventArgs.Empty);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateCountdownLabel()
    {
        var minutes = _remainingSeconds / 60;
        var seconds = _remainingSeconds % 60;
        _countdownLabel.Text = $"{minutes:00}  :  {seconds:00}";
    }

    private void StopTimers()
    {
        _countdownTimer.Stop();
        _pollTimer.Stop();
    }

    private void DisposeRuntimeResources()
    {
        StopTimers();
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _countdownTimer.Dispose();
        _pollTimer.Dispose();
        _qrPictureBox.Image?.Dispose();
        _qrPictureBox.Image = null;
    }

    private static async Task<Image> LoadImageAsync(string source, CancellationToken cancellationToken)
    {
        var bytes = source.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? DecodeDataUri(source)
            : IsLikelyBase64(source)
                ? Convert.FromBase64String(source)
                : await SharedImageHttpClient.Instance.GetByteArrayAsync(source, cancellationToken);

        using var stream = new MemoryStream(bytes);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private static byte[] DecodeDataUri(string source)
    {
        var commaIndex = source.IndexOf(',');
        if (commaIndex < 0 || commaIndex >= source.Length - 1)
        {
            return [];
        }

        return Convert.FromBase64String(source[(commaIndex + 1)..]);
    }

    private static bool IsLikelyBase64(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 64 &&
            trimmed.Length % 4 == 0 &&
            trimmed.All(character => char.IsLetterOrDigit(character) || character is '+' or '/' or '=');
    }

    private static string FormatVnd(long amount) =>
        $"{amount.ToString("N0", ViCulture)}đ";

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static void RestoreDesignedColors(Control control)
    {
        if (control.Tag is Color backColor)
        {
            control.BackColor = backColor;
        }

        if (control is CountdownBadge badge)
        {
            badge.RestoreDesignColors();
        }
        else if (control is RoundedValueBox valueBox)
        {
            valueBox.RestoreDesignColors();
        }
        else if (control is PaymentField paymentField)
        {
            paymentField.RestoreDesignColors();
        }

        foreach (Control child in control.Controls)
        {
            RestoreDesignedColors(child);
        }
    }

    private sealed class PaymentField : UserControl
    {
        private readonly Label _label = new();
        private readonly RoundedValueBox? _valueBox;

        public PaymentField(string label, string value, bool copyable = false, Color? valueColor = null)
        {
            Height = 78;
            Dock = DockStyle.Top;
            Margin = new Padding(0, 0, 0, 12);
            BackColor = Color.Transparent;

            _label.Text = label;
            _label.AutoSize = true;
            _label.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            _label.ForeColor = TextColor;
            _label.Location = new Point(0, 0);

            _valueBox = new RoundedValueBox(value, copyable, valueColor ?? TextColor)
            {
                Location = new Point(0, 26),
                Width = 338
            };

            Controls.Add(_label);
            Controls.Add(_valueBox);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_valueBox is not null)
            {
                _valueBox.Width = Math.Max(120, Width);
            }
        }

        public void RestoreDesignColors()
        {
            BackColor = TransferInfoBackground;
            _label.BackColor = TransferInfoBackground;
            _label.ForeColor = TextColor;
        }
    }

    private sealed class RoundedValueBox : UserControl
    {
        private const int Radius = 4;
        private readonly TextBox _textBox = new();
        private readonly Button? _copyButton;
        private readonly System.Windows.Forms.Timer? _copyFeedbackTimer;
        private readonly Image? _copyIcon;
        private readonly Image? _checkIcon;
        private readonly string _value;
        private readonly Color _valueColor;

        public RoundedValueBox(string value, bool copyable, Color valueColor)
        {
            _value = value;
            _valueColor = valueColor;
            Height = 48;
            MinimumSize = new Size(120, 48);
            BackColor = Color.Transparent;

            _textBox.Text = value;
            _textBox.ReadOnly = true;
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Font = new Font("Segoe UI", 11.5f, FontStyle.Regular);
            _textBox.ForeColor = valueColor;
            _textBox.BackColor = Color.White;
            _textBox.TabStop = false;
            Controls.Add(_textBox);

            if (copyable)
            {
                _copyIcon = SidebarIconRenderer.Load("new-design/Copy.svg", Color.Black, 24);
                _checkIcon = SidebarIconRenderer.Load("new-design/check.svg", LightTheme.Success, 24);
                _copyButton = new Button
                {
                    Image = _copyIcon,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Tag = LightTheme.SkipButtonThemeTag,
                    BackColor = Color.White,
                    TabStop = false
                };
                _copyButton.FlatAppearance.BorderSize = 0;
                _copyButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 245, 245);
                _copyButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(238, 238, 238);
                _copyButton.Click += (_, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(_value) && _value != "-")
                    {
                        Clipboard.SetText(_value);
                        ShowCopiedState();
                    }
                };
                Controls.Add(_copyButton);

                _copyFeedbackTimer = new System.Windows.Forms.Timer
                {
                    Interval = 1200
                };
                _copyFeedbackTimer.Tick += (_, _) =>
                {
                    _copyFeedbackTimer.Stop();
                    if (_copyButton is not null)
                    {
                        _copyButton.Image = _copyIcon;
                    }
                };
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            var copyWidth = _copyButton is null ? 0 : 42;
            var textHeight = _textBox.PreferredHeight;
            _textBox.SetBounds(16, Math.Max(0, (Height - textHeight) / 2), Math.Max(10, Width - 32 - copyWidth), textHeight);
            _copyButton?.SetBounds(Math.Max(0, Width - 44), 4, 38, 40);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? Color.White);
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = AppButton.CreateRoundedRectangle(bounds, Radius);
            using var background = new SolidBrush(Color.White);
            using var border = new Pen(FieldBorder, 1);
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(border, path);
        }

        public void RestoreDesignColors()
        {
            _textBox.BackColor = Color.White;
            _textBox.ForeColor = _valueColor;
            if (_copyButton is not null)
            {
                _copyButton.BackColor = Color.White;
                _copyButton.Image = _copyIcon;
            }
        }

        private void ShowCopiedState()
        {
            if (_copyButton is null || _checkIcon is null || _copyFeedbackTimer is null)
            {
                return;
            }

            _copyButton.Image = _checkIcon;
            _copyFeedbackTimer.Stop();
            _copyFeedbackTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _copyFeedbackTimer?.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class CountdownBadge : UserControl
    {
        private readonly Label _label;

        public CountdownBadge(Label label)
        {
            _label = label;
            BackColor = CountdownBack;
            Controls.Add(_label);

            var icon = new PictureBox
            {
                Image = SidebarIconRenderer.Load("new-design/count-down-icon.svg", Orange, 30),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Width = 34,
                Height = 34,
                Location = new Point(24, 7),
                BackColor = CountdownBack
            };
            Controls.Add(icon);

            _label.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            _label.ForeColor = Orange;
            _label.BackColor = CountdownBack;
            _label.TextAlign = ContentAlignment.MiddleLeft;
            _label.Location = new Point(66, 0);
            _label.Size = new Size(110, 48);
        }

        public void RestoreDesignColors()
        {
            BackColor = CountdownBack;
            _label.BackColor = CountdownBack;
            _label.ForeColor = Orange;
            foreach (Control child in Controls)
            {
                if (child is PictureBox)
                {
                    child.BackColor = CountdownBack;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using var path = AppButton.CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), 4);
            Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(CountdownBack);
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = AppButton.CreateRoundedRectangle(bounds, 4);
            using var background = new SolidBrush(CountdownBack);
            e.Graphics.FillPath(background, path);
        }
    }

    private sealed class RoundedPanel : Panel
    {
        public Color BorderColor { get; init; } = LightTheme.Border;
        public int BorderRadius { get; init; } = 4;
        public int BorderWidth { get; init; } = 1;

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using var path = AppButton.CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), BorderRadius);
            Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = new Rectangle(
                BorderWidth / 2,
                BorderWidth / 2,
                Math.Max(1, Width - BorderWidth),
                Math.Max(1, Height - BorderWidth));
            using var path = AppButton.CreateRoundedRectangle(bounds, BorderRadius);
            using var background = new SolidBrush(BackColor);
            using var border = new Pen(BorderColor, BorderWidth);
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(border, path);
        }
    }

    private sealed class SharedImageHttpClient
    {
        public static readonly HttpClient Instance = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }
}

file static class DepositTransactionExtensions
{
    public static string CodeOrId(this DepositTransaction transaction) =>
        string.IsNullOrWhiteSpace(transaction.Id) ? "-" : transaction.Id;
}
