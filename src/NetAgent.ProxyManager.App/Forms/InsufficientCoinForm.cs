using NetAgent.ProxyManager.App.Controls.Primitives;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Forms;

public sealed class InsufficientCoinForm : Form
{
    private static readonly Color FooterBackground = Color.FromArgb(247, 247, 249);
    private static readonly Color FooterBorder = Color.FromArgb(224, 224, 224);
    private static readonly Color WarningBackground = Color.FromArgb(255, 247, 230);
    private static readonly Color WarningBorder = Color.FromArgb(255, 170, 82);
    private static readonly Color TextColor = Color.FromArgb(36, 36, 36);

    private readonly AppPrimaryButton _depositButton = new();
    private readonly AppButton _closeButton = new();

    public InsufficientCoinForm()
    {
        Text = "Không đủ coin";
        Width = 520;
        Height = 300;
        MinimumSize = new Size(520, 300);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        LightTheme.Apply(this);
        RestoreBackColors(this);
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
            RowCount = 3,
            BackColor = Color.White,
            Padding = new Padding(28, 26, 28, 18),
            Margin = Padding.Empty
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        content.Controls.Add(new Label
        {
            Text = "Không đủ coin để mua Proxy",
            AutoSize = true,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = TextColor,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 10)
        }, 0, 0);

        content.Controls.Add(new Label
        {
            Text = "Số coin hiện tại không đủ để tạo đơn hàng. Vui lòng nạp thêm coin rồi thử mua lại.",
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 46,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ForeColor = LightTheme.Muted,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 16)
        }, 0, 1);

        // content.Controls.Add(CreateNoticePanel(), 0, 2);

        root.Controls.Add(content, 0, 0);
        root.Controls.Add(CreateFooter(), 0, 1);
        Controls.Add(root);
    }

    private static Control CreateNoticePanel()
    {
        var panel = new NoticePanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = WarningBackground,
            Tag = WarningBackground,
            Padding = new Padding(14, 10, 14, 10),
            Margin = Padding.Empty
        };

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Bạn có thể nạp tiền thanh toán ngay tại đây để tiếp tục mua Proxy.",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = WarningBackground,
            Tag = WarningBackground
        };
        panel.Controls.Add(label);
        return panel;
    }

    private Control CreateFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = FooterBackground,
            Tag = FooterBackground,
            Padding = new Padding(20, 14, 20, 14)
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(FooterBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, footer.ClientSize.Width, 0);
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

        _depositButton.Text = "Nạp tiền tại đây";
        _depositButton.Height = 42;
        _depositButton.MinimumSize = new Size(150, 42);
        _depositButton.Margin = new Padding(0, 0, 10, 0);
        _depositButton.Click += (_, _) => DialogResult = DialogResult.OK;

        _closeButton.Text = "Đóng";
        _closeButton.Height = 42;
        _closeButton.MinimumSize = new Size(78, 42);
        _closeButton.Variant = AppButtonVariant.Muted;
        _closeButton.AccentColor = LightTheme.Muted;
        _closeButton.Margin = Padding.Empty;
        _closeButton.Click += (_, _) => DialogResult = DialogResult.Cancel;

        buttons.Controls.Add(_depositButton);
        buttons.Controls.Add(_closeButton);
        footer.Controls.Add(buttons);
        AcceptButton = _depositButton;
        CancelButton = _closeButton;
        return footer;
    }

    private static void RestoreBackColors(Control control)
    {
        if (control.Tag is Color color)
        {
            control.BackColor = color;
        }

        foreach (Control child in control.Controls)
        {
            RestoreBackColors(child);
        }
    }

    private sealed class NoticePanel : Panel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = AppButton.CreateRoundedRectangle(bounds, 4);
            using var border = new Pen(WarningBorder, 1);
            e.Graphics.DrawPath(border, path);
        }
    }
}
