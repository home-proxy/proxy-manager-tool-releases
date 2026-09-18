using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal sealed class HeaderActionButton : Button
{
    private const int Radius = 4;
    private const int IconTextGap = 4;
    private static readonly Color DefaultPressedBackColor = Color.FromArgb(235, 243, 252);
    private static readonly Color DefaultBorderColor = Color.FromArgb(209, 209, 209);

    private bool _isHovering;
    private bool _isPressed;

    public HeaderActionButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Tag = LightTheme.SkipButtonThemeTag;
        TextAlign = ContentAlignment.MiddleCenter;
        ImageAlign = ContentAlignment.MiddleLeft;
        TextImageRelation = TextImageRelation.ImageBeforeText;
    }

    public Color RestBackColor { get; set; } = Color.White;

    public Color HoverBackColor { get; set; } = LightTheme.ButtonHover;

    public Color PressedBackColor { get; set; } = DefaultPressedBackColor;

    public Color BorderColor { get; set; } = DefaultBorderColor;

    public Color DisabledBorderColor { get; set; } = DefaultBorderColor;

    public Color TextColor { get; set; } = Color.FromArgb(36, 36, 36);

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
        base.SetBoundsCore(
            x,
            y,
            Math.Max(width, preferred.Width),
            Math.Max(height, preferred.Height),
            specified);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        if (AutoSize)
        {
            Size = GetPreferredSize(Size.Empty);
        }

        Invalidate();
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
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.White);

        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        var backColor = ResolveBackColor();
        var foreColor = Enabled ? TextColor : Color.FromArgb(156, 163, 175);
        var borderColor = Enabled ? BorderColor : DisabledBorderColor;
        using var path = AppButton.CreateRoundedRectangle(bounds, Radius);
        using var background = new SolidBrush(backColor);
        using var border = new Pen(borderColor, 1);
        e.Graphics.FillPath(background, path);
        e.Graphics.DrawPath(border, path);

        DrawContent(e.Graphics, bounds, foreColor);
    }

    private Color ResolveBackColor()
    {
        if (!Enabled)
        {
            return LightTheme.ButtonHover;
        }

        if (_isPressed)
        {
            return PressedBackColor;
        }

        return _isHovering ? HoverBackColor : RestBackColor;
    }

    private void DrawContent(Graphics graphics, Rectangle bounds, Color foreColor)
    {
        var contentBounds = new Rectangle(
            bounds.Left + Padding.Left,
            bounds.Top + Padding.Top,
            Math.Max(0, bounds.Width - Padding.Horizontal),
            Math.Max(0, bounds.Height - Padding.Vertical));
        var image = Image;
        var hasImage = image is not null;
        var hasText = !string.IsNullOrEmpty(Text);
        var textSize = hasText
            ? TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding)
            : Size.Empty;
        var imageWidth = hasImage ? image!.Width : 0;
        var totalWidth = imageWidth + (hasImage && hasText ? IconTextGap : 0) + textSize.Width;
        var left = contentBounds.Left + Math.Max(0, (contentBounds.Width - totalWidth) / 2);
        var contentHeight = Math.Max(hasImage ? image!.Height : 0, textSize.Height);
        var contentTop = contentBounds.Top + Math.Max(0, (contentBounds.Height - contentHeight) / 2);

        if (hasImage)
        {
            var imageY = contentTop + Math.Max(0, (contentHeight - image!.Height) / 2);
            graphics.DrawImage(image, left, imageY, image.Width, image.Height);
            left += image.Width + IconTextGap;
        }

        if (!hasText)
        {
            return;
        }

        var textBounds = new Rectangle(
            left,
            contentTop + Math.Max(0, (contentHeight - textSize.Height) / 2),
            Math.Max(0, contentBounds.Right - left),
            textSize.Height);
        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            textBounds,
            foreColor,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
    }

    private Size MeasureContentSize()
    {
        var textSize = !string.IsNullOrEmpty(Text)
            ? TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding)
            : Size.Empty;
        var imageWidth = Image?.Width ?? 0;
        var imageHeight = Image?.Height ?? 0;
        var gap = imageWidth > 0 && textSize.Width > 0 ? IconTextGap : 0;
        return new Size(
            Padding.Horizontal + imageWidth + gap + textSize.Width,
            Padding.Vertical + Math.Max(imageHeight, textSize.Height));
    }
}
