using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal sealed class DesignedToolbarButton : Button
{
    private const int Radius = 2;
    private const int DefaultIconTextGap = 6;

    private bool _isHovering;
    private bool _isPressed;

    public DesignedToolbarButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        AutoSize = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Tag = LightTheme.SkipButtonThemeTag;
    }

    public Image? HoverImage { get; set; }

    public Image? DisabledImage { get; set; }

    public Color RestTextColor { get; set; } = DesignedGridTheme.TextColor;

    public Color HoverTextColor { get; set; } = DesignedGridTheme.AccentColor;

    public Color DisabledTextColor { get; set; } = Color.FromArgb(189, 189, 189);

    public int IconTextGap { get; set; } = DefaultIconTextGap;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var contentSize = MeasureContentSize();
        return new Size(
            Math.Max(MinimumSize.Width, contentSize.Width),
            Math.Max(MinimumSize.Height, contentSize.Height));
    }

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        var preferred = GetPreferredSize(Size.Empty);
        base.SetBoundsCore(x, y, Math.Max(width, preferred.Width), Math.Max(height, preferred.Height), specified);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        _isHovering = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (!Enabled)
        {
            return;
        }

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
        if (Enabled && e.Button == MouseButtons.Left)
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

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        if (AutoSize)
        {
            Size = GetPreferredSize(Size.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? LightTheme.Background);

        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        var backColor = Enabled && _isPressed
            ? Color.FromArgb(229, 241, 251)
            : Enabled && _isHovering
                ? Color.FromArgb(243, 248, 253)
                : Color.Transparent;

        using (var path = AppButton.CreateRoundedRectangle(bounds, Radius))
        using (var background = new SolidBrush(backColor))
        {
            e.Graphics.FillPath(background, path);
        }

        DrawContent(e.Graphics, bounds);
    }

    private void DrawContent(Graphics graphics, Rectangle bounds)
    {
        var icon = !Enabled && DisabledImage is not null
            ? DisabledImage
            : Enabled && _isHovering && HoverImage is not null
                ? HoverImage
                : Image;
        var textSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        var iconWidth = icon?.Width ?? 0;
        var totalWidth = iconWidth + (iconWidth > 0 ? IconTextGap : 0) + textSize.Width;
        var contentBounds = new Rectangle(
            bounds.Left + Padding.Left,
            bounds.Top,
            Math.Max(0, bounds.Width - Padding.Horizontal),
            bounds.Height);
        var left = contentBounds.Left + Math.Max(0, (contentBounds.Width - totalWidth) / 2);

        if (icon is not null)
        {
            var iconY = bounds.Top + (bounds.Height - icon.Height) / 2;
            graphics.DrawImage(icon, left, iconY, icon.Width, icon.Height);
            left += icon.Width + IconTextGap;
        }

        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            new Rectangle(left, contentBounds.Top, Math.Max(0, contentBounds.Right - left), contentBounds.Height),
            Enabled ? (_isHovering ? HoverTextColor : RestTextColor) : DisabledTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private Size MeasureContentSize()
    {
        var textSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        var iconWidth = Image?.Width ?? HoverImage?.Width ?? DisabledImage?.Width ?? 0;
        var iconHeight = Image?.Height ?? HoverImage?.Height ?? DisabledImage?.Height ?? 0;
        return new Size(
            Padding.Horizontal + iconWidth + (iconWidth > 0 ? IconTextGap : 0) + textSize.Width,
            Padding.Vertical + Math.Max(iconHeight, textSize.Height));
    }
}
