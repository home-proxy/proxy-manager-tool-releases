using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class DepositAmountForm : Form
{
    private const long MinDepositAmount = 1000;

    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);
    private static readonly Color NoticeBorder = Color.FromArgb(255, 170, 82);
    private static readonly Color NoticeBackground = ColorTranslator.FromHtml("#FFF7E6");
    private static readonly Color FooterBackground = ColorTranslator.FromHtml("#F7F7F9");
    private static readonly Color FooterBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color ErrorTextColor = Color.FromArgb(190, 50, 50);

    private readonly IDepositApiClient _depositApiClient;
    private readonly RoundedTextInput _amountInput = new() { PlaceholderText = "0đ" };
    private readonly AppPrimaryButton _submitButton = new();
    private readonly Label _messageLabel = new();
    private bool _normalizingText;

    public DepositAmountForm(IDepositApiClient depositApiClient)
    {
        _depositApiClient = depositApiClient;

        Text = "Nạp tiền thanh toán";
        Width = 650;
        Height = 470;
        MinimumSize = new Size(650, 470);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LightTheme.Apply(this);
        RestoreTaggedBackColors(this);
    }

    public DepositTransaction? Transaction { get; private set; }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 24, 24, 18),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = "Số tiền muốn nạp",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            ForeColor = TextColor
        }, 0, 0);

        _amountInput.Dock = DockStyle.Top;
        _amountInput.Margin = new Padding(0, 0, 0, 10);
        _amountInput.InnerTextBox.KeyPress += HandleAmountKeyPress;
        _amountInput.InnerTextBox.TextChanged += HandleAmountTextChanged;
        layout.Controls.Add(_amountInput, 0, 1);

        _messageLabel.AutoSize = true;
        _messageLabel.ForeColor = ErrorTextColor;
        _messageLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        _messageLabel.Margin = new Padding(0, 0, 0, 8);
        _messageLabel.Visible = false;
        layout.Controls.Add(_messageLabel, 0, 2);

        layout.Controls.Add(CreateNoticePanel(), 0, 3);

        _submitButton.Text = "Tạo QR chuyển khoản";
        _submitButton.Dock = DockStyle.Fill;
        _submitButton.Height = 42;
        _submitButton.MinimumSize = new Size(0, 42);
        _submitButton.Margin = Padding.Empty;
        _submitButton.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _submitButton.Click += async (_, _) => await CreateTransactionAsync();

        root.Controls.Add(layout, 0, 0);
        root.Controls.Add(CreateFooter(), 0, 1);

        Controls.Add(root);
        AcceptButton = _submitButton;
    }

    private Control CreateFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FooterBackground,
            Tag = FooterBackground,
            Padding = new Padding(24, 17, 24, 17)
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(FooterBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, footer.ClientSize.Width, 0);
        };
        footer.Controls.Add(_submitButton);
        return footer;
    }

    private static Control CreateNoticePanel()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Top,
            Height = 150,
            BackColor = NoticeBackground,
            Tag = NoticeBackground,
            BorderColor = NoticeBorder,
            BorderRadius = 4,
            Padding = new Padding(18, 14, 18, 14),
            Margin = new Padding(0, 4, 0, 0)
        };

        var icon = new PictureBox
        {
            Image = SidebarIconRenderer.LoadOriginal("new-design/luu-y-icon-payment-form.svg", 24),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Width = 26,
            Height = 26,
            Location = new Point(18, 16),
            BackColor = NoticeBackground,
            Tag = NoticeBackground
        };
        var title = new Label
        {
            Text = "Lưu ý:",
            AutoSize = true,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = TextColor,
            Location = new Point(50, 16),
            BackColor = NoticeBackground,
            Tag = NoticeBackground
        };
        var notes = new WrappingNoticeLabel
        {
            Text = $"•  Sai nội dung hoặc 10 phút không lên tiền cần liên hệ kiểm tra\r\n•  Số tiền nạp tối thiểu là {FormatVnd(MinDepositAmount)}",
            AutoSize = false,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = TextColor,
            Location = new Point(50, 43),
            BackColor = NoticeBackground,
            Tag = NoticeBackground
        };
        void LayoutNotes()
        {
            var width = Math.Max(120, panel.ClientSize.Width - notes.Left - 18);
            notes.SetBounds(notes.Left, notes.Top, width, Math.Max(82, panel.ClientSize.Height - notes.Top - 14));
        }
        panel.Resize += (_, _) => LayoutNotes();
        LayoutNotes();

        panel.Controls.Add(icon);
        panel.Controls.Add(title);
        panel.Controls.Add(notes);
        return panel;
    }

    private void HandleAmountKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private void HandleAmountTextChanged(object? sender, EventArgs e)
    {
        if (_normalizingText)
        {
            return;
        }

        var digits = GetDigits(_amountInput.Text);
        var formatted = digits.Length == 0
            ? string.Empty
            : $"{long.Parse(digits, CultureInfo.InvariantCulture).ToString("N0", ViCulture)}đ";
        if (string.Equals(formatted, _amountInput.Text, StringComparison.Ordinal))
        {
            ClearMessage();
            return;
        }

        _normalizingText = true;
        _amountInput.Text = formatted;
        _amountInput.SelectionStart = formatted.EndsWith('đ') ? Math.Max(0, formatted.Length - 1) : formatted.Length;
        _normalizingText = false;
        ClearMessage();
    }

    private async Task CreateTransactionAsync()
    {
        if (!TryReadAmount(out var amount))
        {
            return;
        }

        SetBusy(true);
        try
        {
            Transaction = await _depositApiClient.CreateDepositTransactionAsync(amount, CancellationToken.None);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            ShowMessage(UiFeedback.GetFriendlyErrorMessage(ex));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool TryReadAmount(out long amount)
    {
        amount = 0;
        var digits = GetDigits(_amountInput.Text);
        if (digits.Length == 0 ||
            !long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount) ||
            amount <= 0)
        {
            ShowMessage("Vui lòng nhập số tiền muốn nạp.");
            return false;
        }

        if (amount < MinDepositAmount)
        {
            ShowMessage($"Vui lòng nạp tối thiểu {FormatVnd(MinDepositAmount)}.");
            return false;
        }

        ClearMessage();
        return true;
    }

    private void SetBusy(bool busy)
    {
        _submitButton.Enabled = !busy;
        _amountInput.Enabled = !busy;
        _submitButton.Text = busy ? "Đang tạo QR..." : "Tạo QR chuyển khoản";
    }

    private void ShowMessage(string message)
    {
        _messageLabel.Text = message;
        _messageLabel.Visible = true;
    }

    private void ClearMessage()
    {
        _messageLabel.Text = string.Empty;
        _messageLabel.Visible = false;
    }

    private static string GetDigits(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    private static string FormatVnd(long amount) =>
        $"{amount.ToString("N0", ViCulture)}đ";

    private void RestoreTaggedBackColors(Control control)
    {
        if (control.Tag is Color backColor)
        {
            control.BackColor = backColor;
        }

        if (control == _messageLabel)
        {
            control.ForeColor = ErrorTextColor;
        }

        foreach (Control child in control.Controls)
        {
            RestoreTaggedBackColors(child);
        }
    }

    private sealed class WrappingNoticeLabel : Label
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                ForeColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPrefix);
        }
    }

    private sealed class RoundedTextInput : UserControl
    {
        private const int Radius = 4;
        private static readonly Color BorderColor = Color.FromArgb(212, 212, 212);
        private static readonly Color PlaceholderColor = Color.FromArgb(115, 115, 115);
        private readonly TextBox _textBox = new();

        public RoundedTextInput()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            Height = 48;
            MinimumSize = new Size(120, 48);
            BackColor = Color.White;

            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Font = new Font("Segoe UI", 11.5f, FontStyle.Regular);
            _textBox.ForeColor = TextColor;
            _textBox.BackColor = Color.White;
            _textBox.Margin = Padding.Empty;
            _textBox.Enter += (_, _) => Invalidate();
            _textBox.Leave += (_, _) => Invalidate();
            _textBox.TextChanged += (_, _) => Invalidate();
            Controls.Add(_textBox);
        }

        public TextBox InnerTextBox => _textBox;

        [AllowNull]
        public override string Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value ?? string.Empty;
        }

        public string PlaceholderText
        {
            get => _textBox.PlaceholderText;
            set => _textBox.PlaceholderText = value;
        }

        public int SelectionStart
        {
            get => _textBox.SelectionStart;
            set => _textBox.SelectionStart = Math.Min(Math.Max(0, value), _textBox.TextLength);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutTextBox();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _textBox.Enabled = Enabled;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? Color.White);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = AppButton.CreateRoundedRectangle(bounds, Radius);
            using var background = new SolidBrush(Enabled ? Color.White : Color.FromArgb(250, 250, 250));
            using var border = new Pen(_textBox.Focused ? LightTheme.Accent : BorderColor, _textBox.Focused ? 1.7f : 1f);
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(border, path);
        }

        private void LayoutTextBox()
        {
            var height = _textBox.PreferredHeight;
            _textBox.SetBounds(14, Math.Max(0, (Height - height) / 2), Math.Max(10, Width - 28), height);
        }
    }

    private sealed class RoundedPanel : Panel
    {
        public Color BorderColor { get; init; } = LightTheme.Border;
        public int BorderRadius { get; init; } = 4;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = AppButton.CreateRoundedRectangle(bounds, BorderRadius);
            using var border = new Pen(BorderColor, 1);
            e.Graphics.DrawPath(border, path);
        }
    }
}
