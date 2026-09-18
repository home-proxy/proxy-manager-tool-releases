using System.Drawing.Drawing2D;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal sealed class AppClearFilterButton : Button
{
    private const int Radius = 2;
    private const int IconTextGap = 4;

    public static readonly Color RestColor = DesignedGridTheme.AccentColor;
    public static readonly Color RestBackColor = Color.White;
    public static readonly Color RestBorderColor = Color.FromArgb(174, 208, 238);
    public static readonly Color HoverColor = DesignedGridTheme.AccentColor;
    public static readonly Color HoverBackColor = Color.FromArgb(243, 248, 253);
    public static readonly Color HoverBorderColor = DesignedGridTheme.AccentColor;
    public static readonly Color PressedBackColor = Color.FromArgb(229, 241, 251);

    private bool _isHovering;
    private bool _isPressed;

    public AppClearFilterButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        Text = "Xóa lọc";
        Image = UiIcons.NewFunnelClear;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Tag = LightTheme.SkipButtonThemeTag;
        AutoSize = true;
        MinimumSize = new Size(78, 32);
        Size = new Size(78, 32);
        Padding = new Padding(6);
        Margin = Padding.Empty;
        Font = new Font("Segoe UI", 10f, FontStyle.Regular);
    }

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

        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        var backColor = _isPressed
            ? PressedBackColor
            : _isHovering
                ? HoverBackColor
                : RestBackColor;
        var borderColor = _isHovering || _isPressed ? HoverBorderColor : RestBorderColor;

        using (var path = AppButton.CreateRoundedRectangle(bounds, Radius))
        using (var background = new SolidBrush(backColor))
        using (var border = new Pen(borderColor))
        {
            e.Graphics.FillPath(background, path);
            e.Graphics.DrawPath(border, path);
        }

        DrawContent(e.Graphics, bounds, _isHovering || _isPressed ? HoverColor : RestColor, UiIcons.NewFunnelClearHover);
    }

    private void DrawContent(Graphics graphics, Rectangle bounds, Color foreColor, Image? icon)
    {
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
            foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private Size MeasureContentSize()
    {
        var textSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        var iconWidth = Image?.Width ?? 0;
        var iconHeight = Image?.Height ?? 0;
        return new Size(
            Padding.Horizontal + iconWidth + (iconWidth > 0 ? IconTextGap : 0) + textSize.Width,
            Padding.Vertical + Math.Max(iconHeight, textSize.Height));
    }
}
