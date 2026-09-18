using System.Drawing.Drawing2D;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal static class AppDataGridFooter
{
    public const int ControlHeight = 34;
    private const int PagerButtonSize = 34;
    private const int PageLabelMinimumWidth = 48;
    private const int PageLabelHorizontalPadding = 14;

    public static Label CreateStatusLabel() =>
        new AppDataGridStatusLabel
        {
            Dock = DockStyle.Fill,
            Height = ControlHeight,
            MinimumSize = new Size(0, ControlHeight),
            Margin = new Padding(8, 0, 8, 0)
        };

    public static Control CreatePageSizePanel(ComboBox pageSizeCombo)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, ControlHeight));

        panel.Controls.Add(new Label
        {
            Text = "Kích cỡ trang:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Height = ControlHeight,
            Padding = new Padding(0, 0, 8, 0),
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        pageSizeCombo.Height = ControlHeight;
        pageSizeCombo.Anchor = AnchorStyles.Left;
        pageSizeCombo.Margin = Padding.Empty;
        panel.Controls.Add(pageSizeCombo, 1, 0);
        return panel;
    }

    public static Button CreatePagerButton(bool previous, EventHandler onClick)
    {
        var iconName = previous
            ? "new-design/chevron-left.svg"
            : "new-design/chevron-right.svg";
        var button = new AppDataGridPagerButton(SidebarIconRenderer.LoadOriginal(iconName, 16))
        {
            Size = new Size(PagerButtonSize, PagerButtonSize),
            MinimumSize = new Size(PagerButtonSize, PagerButtonSize),
            MaximumSize = new Size(PagerButtonSize, PagerButtonSize)
        };
        button.Click += onClick;
        return button;
    }

    public static Control CreatePager(Label pageLabel, Button previousButton, Button nextButton)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        ConfigurePageLabel(pageLabel);
        panel.Controls.Add(previousButton);
        panel.Controls.Add(pageLabel);
        panel.Controls.Add(nextButton);
        return panel;
    }

    public static TableLayoutPanel Create(Control pageSizePanel, Label statusLabel, Control pager)
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        footer.Controls.Add(pageSizePanel, 0, 0);
        footer.Controls.Add(new FooterSeparator(), 1, 0);
        footer.Controls.Add(statusLabel, 2, 0);
        footer.Controls.Add(pager, 3, 0);
        return footer;
    }

    public static TableLayoutPanel Create(Label statusLabel, Control pager)
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(pager, 1, 0);
        return footer;
    }

    private static void ConfigurePageLabel(Label pageLabel)
    {
        pageLabel.AutoSize = false;
        pageLabel.Size = new Size(PageLabelMinimumWidth, ControlHeight);
        pageLabel.MinimumSize = new Size(PageLabelMinimumWidth, ControlHeight);
        pageLabel.MaximumSize = Size.Empty;
        pageLabel.TextAlign = ContentAlignment.MiddleCenter;
        pageLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        pageLabel.ForeColor = Color.FromArgb(32, 32, 32);
        pageLabel.BackColor = LightTheme.Background;
        pageLabel.Margin = Padding.Empty;
        pageLabel.Padding = Padding.Empty;
        pageLabel.TextChanged -= ResizePageLabelToContent;
        pageLabel.TextChanged += ResizePageLabelToContent;
        ResizePageLabelToContent(pageLabel, EventArgs.Empty);
    }

    private static void ResizePageLabelToContent(object? sender, EventArgs e)
    {
        if (sender is not Label pageLabel)
        {
            return;
        }

        var textSize = TextRenderer.MeasureText(
            string.IsNullOrWhiteSpace(pageLabel.Text) ? "1/1" : pageLabel.Text,
            pageLabel.Font,
            Size.Empty,
            TextFormatFlags.NoPadding);
        var width = Math.Max(PageLabelMinimumWidth, textSize.Width + PageLabelHorizontalPadding);
        pageLabel.Width = width;
        pageLabel.MinimumSize = new Size(width, ControlHeight);
    }

    private sealed class FooterSeparator : Control
    {
        public FooterSeparator()
        {
            Width = 1;
            Height = ControlHeight;
            Margin = new Padding(12, 0, 4, 0);
            BackColor = Color.FromArgb(224, 224, 224);
        }
    }

    private sealed class AppDataGridStatusLabel : Label
    {
        private static readonly Color StatusBackColor = Color.FromArgb(248, 248, 248);
        private static readonly Color StatusBorderColor = Color.FromArgb(224, 224, 224);
        private static readonly Color StatusTextColor = Color.FromArgb(32, 32, 32);

        public AppDataGridStatusLabel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            AutoSize = false;
            TextAlign = ContentAlignment.MiddleCenter;
            Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            ForeColor = StatusTextColor;
            BackColor = LightTheme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? LightTheme.Background);

            var bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;
            using var path = AppButton.CreateRoundedRectangle(bounds, 4);
            using var fill = new SolidBrush(StatusBackColor);
            using var border = new Pen(StatusBorderColor, 1);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                StatusTextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class AppDataGridPagerButton : Button
    {
        private static readonly Color BorderColor = Color.FromArgb(224, 224, 224);
        private static readonly Color DisabledBackColor = Color.FromArgb(248, 248, 248);
        private static readonly Color HoverBackColor = Color.FromArgb(245, 245, 245);
        private static readonly Color PressedBackColor = Color.FromArgb(238, 238, 238);

        private bool _isHovering;
        private bool _isPressed;

        public AppDataGridPagerButton(Image? image)
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            Tag = LightTheme.SkipButtonThemeTag;
            Image = image;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            TabStop = false;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovering = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovering = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? LightTheme.Background);

            var backColor = !Enabled
                ? DisabledBackColor
                : _isPressed
                    ? PressedBackColor
                    : _isHovering
                        ? HoverBackColor
                        : Color.White;

            var bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;
            using var path = AppButton.CreateRoundedRectangle(bounds, 4);
            using var fill = new SolidBrush(backColor);
            using var border = new Pen(BorderColor, 1);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);

            if (Image is null)
            {
                return;
            }

            var imageX = (Width - Image.Width) / 2;
            var imageY = (Height - Image.Height) / 2;
            if (Enabled)
            {
                e.Graphics.DrawImage(Image, imageX, imageY, Image.Width, Image.Height);
                return;
            }

            ControlPaint.DrawImageDisabled(e.Graphics, Image, imageX, imageY, DisabledBackColor);
        }
    }
}
