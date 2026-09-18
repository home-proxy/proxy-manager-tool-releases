using System.Drawing.Drawing2D;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal static class GridCellButtonRenderer
{
    private const int HorizontalPadding = 18;
    private const int VerticalPadding = 6;
    private const int DefaultMaxHeight = 24;
    private const int DefaultMinHeight = 20;

    public static void DrawOutlineButton(Graphics graphics, Rectangle bounds, string text)
    {
        DrawOutlineButton(graphics, bounds, text, LightTheme.Accent);
    }

    public static void DrawOutlineButton(Graphics graphics, Rectangle bounds, string text, Color accent)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = AppButton.CreateRoundedRectangle(
            new Rectangle(bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1),
            LightTheme.ButtonRadius);
        using var background = new SolidBrush(LightTheme.Surface);
        using var border = new Pen(accent, 1.2f);
        graphics.FillPath(background, path);
        graphics.DrawPath(border, path);
        TextRenderer.DrawText(
            graphics,
            text,
            SystemFonts.MessageBoxFont,
            bounds,
            accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    public static void DrawOutlineIconButton(Graphics graphics, Rectangle bounds, Image icon)
    {
        DrawOutlineIconButton(graphics, bounds, icon, LightTheme.Accent);
    }

    public static void DrawOutlineIconButton(Graphics graphics, Rectangle bounds, Image icon, Color accent)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = AppButton.CreateRoundedRectangle(
            new Rectangle(bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1),
            LightTheme.ButtonRadius);
        using var background = new SolidBrush(LightTheme.Surface);
        using var border = new Pen(accent, 1.2f);
        graphics.FillPath(background, path);
        graphics.DrawPath(border, path);

        var iconSize = Math.Min(16, Math.Min(bounds.Width - 8, bounds.Height - 8));
        if (iconSize <= 0)
        {
            return;
        }

        var iconBounds = new Rectangle(
            bounds.Left + (bounds.Width - iconSize) / 2,
            bounds.Top + (bounds.Height - iconSize) / 2,
            iconSize,
            iconSize);
        graphics.DrawImage(icon, iconBounds);
    }

    public static void DrawImageButton(Graphics graphics, Rectangle bounds, Image icon)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var iconSize = Math.Min(bounds.Width, bounds.Height);
        if (iconSize <= 0)
        {
            return;
        }

        var iconBounds = new Rectangle(
            bounds.Left + (bounds.Width - iconSize) / 2,
            bounds.Top + (bounds.Height - iconSize) / 2,
            iconSize,
            iconSize);
        graphics.DrawImage(icon, iconBounds);
    }

    public static Size MeasureOutlineButton(string text)
    {
        var textSize = TextRenderer.MeasureText(
            text,
            SystemFonts.MessageBoxFont,
            Size.Empty,
            TextFormatFlags.NoPadding);
        return new Size(
            textSize.Width + HorizontalPadding,
            textSize.Height + VerticalPadding);
    }

    public static Rectangle GetRightAlignedButtonBounds(Rectangle cellBounds, string text, int rightPadding = 6)
    {
        var size = MeasureOutlineButton(text);
        var height = Math.Min(DefaultMaxHeight, Math.Max(DefaultMinHeight, Math.Min(size.Height, cellBounds.Height - 6)));
        var width = Math.Min(size.Width, Math.Max(0, cellBounds.Width - rightPadding - 4));
        return new Rectangle(
            cellBounds.Right - width - rightPadding,
            cellBounds.Top + Math.Max(3, (cellBounds.Height - height) / 2),
            width,
            height);
    }

    public static Rectangle GetRightAlignedIconButtonBounds(Rectangle cellBounds, int rightPadding = 6)
    {
        var height = Math.Min(DefaultMaxHeight, Math.Max(DefaultMinHeight, cellBounds.Height - 6));
        var width = Math.Min(30, Math.Max(0, cellBounds.Width - rightPadding - 4));
        return new Rectangle(
            cellBounds.Right - width - rightPadding,
            cellBounds.Top + Math.Max(3, (cellBounds.Height - height) / 2),
            width,
            height);
    }

    public static IReadOnlyList<Rectangle> GetCenteredIconButtonBounds(
        Rectangle cellBounds,
        int count,
        int buttonSize = 28,
        int gap = 8)
    {
        var totalWidth = (buttonSize * count) + (Math.Max(0, count - 1) * gap);
        var left = cellBounds.Left + Math.Max(4, (cellBounds.Width - totalWidth) / 2);
        var top = cellBounds.Top + Math.Max(3, (cellBounds.Height - buttonSize) / 2);
        var bounds = new List<Rectangle>(count);
        for (var index = 0; index < count; index++)
        {
            bounds.Add(new Rectangle(left, top, buttonSize, buttonSize));
            left += buttonSize + gap;
        }

        return bounds;
    }

    public static Rectangle GetLeftAlignedButtonBounds(Rectangle cellBounds, string text, int leftPadding = 8)
    {
        var size = MeasureOutlineButton(text);
        var height = Math.Min(DefaultMaxHeight, Math.Max(DefaultMinHeight, Math.Min(size.Height, cellBounds.Height - 6)));
        var width = Math.Min(size.Width, Math.Max(0, cellBounds.Width - leftPadding - 8));
        return new Rectangle(
            cellBounds.Left + leftPadding,
            cellBounds.Top + Math.Max(3, (cellBounds.Height - height) / 2),
            width,
            height);
    }

    public static IReadOnlyList<(Rectangle Bounds, string Text)> GetCenteredButtonLayouts(
        Rectangle cellBounds,
        IReadOnlyList<string> texts,
        int gap = 6)
    {
        var buttonSizes = texts.Select(MeasureOutlineButton).ToList();
        var totalWidth = buttonSizes.Sum(size => size.Width) + (Math.Max(0, buttonSizes.Count - 1) * gap);
        var left = cellBounds.Left + Math.Max(4, (cellBounds.Width - totalWidth) / 2);
        var layouts = new List<(Rectangle Bounds, string Text)>(texts.Count);

        for (var index = 0; index < texts.Count; index++)
        {
            var size = buttonSizes[index];
            var height = Math.Min(DefaultMaxHeight, Math.Max(DefaultMinHeight, Math.Min(size.Height, cellBounds.Height - 6)));
            var bounds = new Rectangle(
                left,
                cellBounds.Top + Math.Max(3, (cellBounds.Height - height) / 2),
                size.Width,
                height);
            layouts.Add((bounds, texts[index]));
            left += size.Width + gap;
        }

        return layouts;
    }
}
