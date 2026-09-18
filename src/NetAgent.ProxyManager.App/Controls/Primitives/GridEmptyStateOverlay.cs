using System.Drawing.Drawing2D;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal sealed class GridEmptyStateOverlay
{
    private const int IconSize = 72;
    private const int ButtonTopGap = 14;
    private const int IconTextGap = 12;

    private readonly DataGridView _grid;
    private readonly Func<GridEmptyStateContent?> _getContent;
    private readonly Image? _icon;
    private Rectangle _buttonBounds = Rectangle.Empty;
    private bool _isButtonHovering;
    private bool _isButtonPressed;

    public GridEmptyStateOverlay(DataGridView grid, Func<GridEmptyStateContent?> getContent)
    {
        _grid = grid;
        _getContent = getContent;
        _icon = SidebarIconRenderer.LoadOriginal("Empty archive.svg", IconSize);

        _grid.Paint += Paint;
        _grid.MouseMove += HandleMouseMove;
        _grid.MouseLeave += (_, _) =>
        {
            _grid.Cursor = Cursors.Default;
            SetButtonHoverState(false);
        };
        _grid.MouseDown += HandleMouseDown;
        _grid.MouseUp += HandleMouseUp;
        _grid.MouseClick += HandleMouseClick;
        _grid.DataBindingComplete += (_, _) => _grid.Invalidate();
        _grid.RowsAdded += (_, _) => _grid.Invalidate();
        _grid.RowsRemoved += (_, _) => _grid.Invalidate();
    }

    private void Paint(object? sender, PaintEventArgs e)
    {
        var content = _getContent();
        if (content is null || string.IsNullOrWhiteSpace(content.Text))
        {
            _buttonBounds = Rectangle.Empty;
            return;
        }

        var headerHeight = _grid.ColumnHeadersVisible ? _grid.ColumnHeadersHeight : 0;
        var bodyBounds = new Rectangle(
            0,
            headerHeight,
            _grid.ClientSize.Width,
            Math.Max(0, _grid.ClientSize.Height - headerHeight));
        if (bodyBounds.Width <= 0 || bodyBounds.Height <= 0)
        {
            _buttonBounds = Rectangle.Empty;
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using (var background = new SolidBrush(_grid.BackgroundColor))
        {
            e.Graphics.FillRectangle(background, bodyBounds);
        }

        using var font = new Font(_grid.Font.FontFamily, 10.5f, content.FontStyle);
        var maxTextSize = new Size(Math.Max(0, bodyBounds.Width - 48), bodyBounds.Height);
        var textSize = TextRenderer.MeasureText(
            e.Graphics,
            content.Text,
            font,
            maxTextSize,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        var isClearFilterButton = IsClearFilterButton(content.ButtonText);
        var buttonSize = string.IsNullOrWhiteSpace(content.ButtonText)
            ? Size.Empty
            : isClearFilterButton
                ? MeasureClearFilterButton(content.ButtonText)
                : GridCellButtonRenderer.MeasureOutlineButton(content.ButtonText);
        var showIcon = content.ShowIcon && _icon is not null;
        var iconHeight = showIcon ? IconSize : 0;
        var iconTextGap = showIcon ? IconTextGap : 0;
        var totalHeight = iconHeight + iconTextGap + textSize.Height +
            (buttonSize == Size.Empty ? 0 : ButtonTopGap + Math.Max(32, buttonSize.Height));
        var top = bodyBounds.Top + Math.Max(0, (bodyBounds.Height - totalHeight) / 2);

        if (showIcon)
        {
            var iconBounds = new Rectangle(
                bodyBounds.Left + Math.Max(0, (bodyBounds.Width - IconSize) / 2),
                top,
                IconSize,
                IconSize);
            e.Graphics.DrawImage(_icon!, iconBounds);
        }

        top += iconHeight + iconTextGap;
        var textBounds = new Rectangle(
            bodyBounds.Left + 24,
            top,
            Math.Max(0, bodyBounds.Width - 48),
            textSize.Height + 4);
        TextRenderer.DrawText(
            e.Graphics,
            content.Text,
            font,
            textBounds,
            LightTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);

        if (buttonSize == Size.Empty || string.IsNullOrWhiteSpace(content.ButtonText))
        {
            _buttonBounds = Rectangle.Empty;
            DrawGridBorder(e.Graphics);
            return;
        }

        var buttonHeight = Math.Max(32, buttonSize.Height);
        var buttonWidth = isClearFilterButton ? buttonSize.Width : Math.Max(120, buttonSize.Width + 10);
        _buttonBounds = new Rectangle(
            bodyBounds.Left + Math.Max(0, (bodyBounds.Width - buttonWidth) / 2),
            textBounds.Bottom + ButtonTopGap,
            buttonWidth,
            buttonHeight);
        if (isClearFilterButton)
        {
            DrawClearFilterButton(e.Graphics, _buttonBounds, content.ButtonText);
            DrawGridBorder(e.Graphics);
            return;
        }

        GridCellButtonRenderer.DrawOutlineButton(e.Graphics, _buttonBounds, content.ButtonText, content.AccentColor);
        DrawGridBorder(e.Graphics);
    }

    private void DrawGridBorder(Graphics graphics)
    {
        if (_grid.ClientSize.Width <= 1 || _grid.ClientSize.Height <= 1)
        {
            return;
        }

        using var pen = new Pen(SystemColors.ControlDark);
        var right = _grid.ClientSize.Width - 1;
        var bottom = _grid.ClientSize.Height - 1;
        graphics.DrawLine(pen, right, 0, right, bottom);
        graphics.DrawLine(pen, 0, bottom, right, bottom);
    }

    private void HandleMouseMove(object? sender, MouseEventArgs e)
    {
        var hovering = _buttonBounds.Contains(e.Location);
        _grid.Cursor = hovering ? Cursors.Hand : Cursors.Default;
        SetButtonHoverState(hovering);
    }

    private void HandleMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_buttonBounds.Contains(e.Location))
        {
            return;
        }

        _isButtonPressed = true;
        _grid.Invalidate();
    }

    private void HandleMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isButtonPressed)
        {
            return;
        }

        _isButtonPressed = false;
        _grid.Invalidate();
    }

    private void HandleMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_buttonBounds.Contains(e.Location))
        {
            return;
        }

        _getContent()?.Click?.Invoke();
    }

    private void SetButtonHoverState(bool hovering)
    {
        if (_isButtonHovering == hovering)
        {
            return;
        }

        _isButtonHovering = hovering;
        if (!hovering)
        {
            _isButtonPressed = false;
        }

        _grid.Invalidate();
    }

    private static bool IsClearFilterButton(string? text) =>
        string.Equals(text, "Xóa lọc", StringComparison.OrdinalIgnoreCase);

    private static Size MeasureClearFilterButton(string text)
    {
        using var font = new Font("Segoe UI", 10f, FontStyle.Regular);
        var textSize = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding);
        var icon = UiIcons.NewFunnelClear;
        return new Size(
            Math.Max(78, 12 + icon.Width + 4 + textSize.Width),
            Math.Max(32, 12 + Math.Max(icon.Height, textSize.Height)));
    }

    private void DrawClearFilterButton(Graphics graphics, Rectangle bounds, string text)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var backColor = _isButtonPressed
            ? AppClearFilterButton.PressedBackColor
            : _isButtonHovering
                ? AppClearFilterButton.HoverBackColor
                : Color.Transparent;
        using (var path = AppButton.CreateRoundedRectangle(bounds, 2))
        using (var background = new SolidBrush(backColor))
        {
            graphics.FillPath(background, path);
        }

        using var font = new Font("Segoe UI", 10f, FontStyle.Regular);
        var foreColor = _isButtonHovering ? AppClearFilterButton.HoverColor : AppClearFilterButton.RestColor;
        var icon = _isButtonHovering ? UiIcons.NewFunnelClearHover : UiIcons.NewFunnelClear;
        var textSize = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding);
        var totalWidth = icon.Width + 4 + textSize.Width;
        var left = bounds.Left + Math.Max(0, (bounds.Width - totalWidth) / 2);
        graphics.DrawImage(icon, left, bounds.Top + (bounds.Height - icon.Height) / 2, icon.Width, icon.Height);
        left += icon.Width + 4;

        TextRenderer.DrawText(
            graphics,
            text,
            font,
            new Rectangle(left, bounds.Top, Math.Max(0, bounds.Right - left), bounds.Height),
            foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

internal sealed record GridEmptyStateContent(
    string Text,
    string ButtonText,
    Action Click,
    Color AccentColor,
    bool ShowIcon = true,
    FontStyle FontStyle = FontStyle.Bold)
{
    public static GridEmptyStateContent TextOnly(string text) =>
        new(text, string.Empty, static () => { }, LightTheme.Muted, ShowIcon: false, FontStyle: FontStyle.Regular);
}
