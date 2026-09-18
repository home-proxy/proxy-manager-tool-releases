using System.Drawing.Drawing2D;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal class AppButton : Button
{
    private const int IconTextGap = 8;
    private const int HorizontalSafetyPadding = 10;

    private bool _isHovering;
    private bool _isPressed;

    public AppButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        MinimumSize = new Size(0, LightTheme.ButtonHeight);
        Padding = new Padding(12, 6, 12, 6);
        TextImageRelation = TextImageRelation.ImageBeforeText;
        ImageAlign = ContentAlignment.MiddleLeft;
        TextAlign = ContentAlignment.MiddleCenter;
        UpdateContentMinimumSize();
    }

    public AppButtonVariant Variant { get; set; } = AppButtonVariant.Secondary;

    public Color AccentColor { get; set; } = LightTheme.Accent;

    public bool Filled { get; set; }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var contentSize = MeasureContentSize();
        return new Size(
            Math.Max(contentSize.Width, MinimumSize.Width),
            Math.Max(contentSize.Height, MinimumSize.Height));
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        UpdateContentMinimumSize();
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateContentMinimumSize();
        Invalidate();
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        UpdateContentMinimumSize();
        Invalidate();
    }

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        var preferred = GetPreferredSize(Size.Empty);
        var enforcedWidth = !string.IsNullOrWhiteSpace(Text)
            ? Math.Max(width, preferred.Width)
            : width;
        var enforcedHeight = Math.Max(height, preferred.Height);
        base.SetBoundsCore(x, y, enforcedWidth, enforcedHeight, specified);
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
        var palette = ResolvePalette();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? LightTheme.Background);

        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        using var path = CreateRoundedRectangle(bounds, LightTheme.ButtonRadius);
        using var background = new SolidBrush(palette.BackColor);
        using var border = new Pen(palette.BorderColor, palette.BorderWidth);
        e.Graphics.FillPath(background, path);
        if (palette.BorderWidth > 0)
        {
            e.Graphics.DrawPath(border, path);
        }

        DrawContent(e.Graphics, bounds, palette.ForeColor);
    }

    private ButtonPalette ResolvePalette()
    {
        if (!Enabled)
        {
            return new ButtonPalette(
                LightTheme.ButtonHover,
                LightTheme.Border,
                Color.FromArgb(156, 163, 175),
                1);
        }

        var accent = ResolveAccent();
        var filled = Filled || Variant == AppButtonVariant.Primary || Variant == AppButtonVariant.Success;
        if (Variant == AppButtonVariant.Ghost)
        {
            var ghostBack = _isPressed ? LightTheme.AccentSoft : _isHovering ? LightTheme.ButtonHover : Color.Transparent;
            return new ButtonPalette(ghostBack, Color.Transparent, accent, 0);
        }

        if (filled)
        {
            var back = _isPressed
                ? ControlPaint.Dark(accent, 0.08f)
                : _isHovering
                    ? ControlPaint.Light(accent, 0.10f)
                    : accent;
            return new ButtonPalette(back, accent, Color.White, 1);
        }

        var restingBack = Mix(Color.White, accent, 0.045);
        var hoverBack = Mix(Color.White, accent, 0.12);
        var border = Variant == AppButtonVariant.Muted
            ? Color.FromArgb(132, 145, 164)
            : Mix(Color.White, accent, 0.42);
        var backColor = _isPressed ? LightTheme.AccentSoft : _isHovering ? hoverBack : restingBack;
        var foreColor = Variant == AppButtonVariant.Muted ? LightTheme.Foreground : accent;
        return new ButtonPalette(backColor, border, foreColor, 1);
    }

    private Color ResolveAccent() =>
        Variant switch
        {
            AppButtonVariant.Success => LightTheme.Success,
            AppButtonVariant.Warning => LightTheme.Warning,
            AppButtonVariant.Danger => LightTheme.Danger,
            AppButtonVariant.Muted => LightTheme.Muted,
            AppButtonVariant.Primary => LightTheme.Accent,
            AppButtonVariant.Secondary => AccentColor,
            AppButtonVariant.Ghost => AccentColor,
            _ => AccentColor
        };

    private void DrawContent(Graphics graphics, Rectangle bounds, Color foreColor)
    {
        var image = Image;
        var hasImage = image is not null;
        var hasText = !string.IsNullOrEmpty(Text);
        var contentBounds = Rectangle.Inflate(bounds, -Padding.Horizontal / 2, -Padding.Vertical / 2);

        if (hasImage && !hasText)
        {
            var imageX = contentBounds.Left + (contentBounds.Width - image!.Width) / 2;
            var imageY = contentBounds.Top + (contentBounds.Height - image.Height) / 2;
            graphics.DrawImage(image, imageX, imageY, image.Width, image.Height);
            return;
        }

        var imageWidth = hasImage ? image!.Width + IconTextGap : 0;
        var textSize = hasText
            ? TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding)
            : Size.Empty;
        var totalWidth = imageWidth + textSize.Width;
        var left = contentBounds.Left + Math.Max(0, (contentBounds.Width - totalWidth) / 2);

        if (hasImage)
        {
            var imageY = contentBounds.Top + (contentBounds.Height - image!.Height) / 2;
            graphics.DrawImage(image, left, imageY, image.Width, image.Height);
            left += image.Width + IconTextGap;
        }

        if (hasText)
        {
            var textBounds = new Rectangle(
                left,
                contentBounds.Top,
                Math.Max(0, contentBounds.Right - left),
                contentBounds.Height);
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                textBounds,
                foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    internal void RefreshContentMinimumSize()
    {
        UpdateContentMinimumSize();
        Invalidate();
    }

    private void UpdateContentMinimumSize()
    {
        var currentMinimumSize = MinimumSize;
        var contentSize = MeasureContentSize();
        var minimumWidth = string.IsNullOrWhiteSpace(Text)
            ? 0
            : contentSize.Width;
        var minimumSize = new Size(
            Math.Max(minimumWidth, currentMinimumSize.Width),
            Math.Max(contentSize.Height, currentMinimumSize.Height));
        MinimumSize = minimumSize;

        if (!string.IsNullOrWhiteSpace(Text) && Width < minimumSize.Width)
        {
            Width = minimumSize.Width;
        }

        if (Height < minimumSize.Height)
        {
            Height = minimumSize.Height;
        }
    }

    private Size MeasureContentSize()
    {
        var textSize = string.IsNullOrWhiteSpace(Text)
            ? Size.Empty
            : TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        var image = Image;
        var imageWidth = image is null ? 0 : image.Width;
        var imageHeight = image is null ? 0 : image.Height;
        var gap = image is not null && textSize.Width > 0 ? IconTextGap : 0;

        return new Size(
            Padding.Horizontal + imageWidth + gap + textSize.Width + HorizontalSafetyPadding,
            Padding.Vertical + Math.Max(textSize.Height, imageHeight));
    }

    internal static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Mix(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }

    private readonly record struct ButtonPalette(Color BackColor, Color BorderColor, Color ForeColor, int BorderWidth);
}
