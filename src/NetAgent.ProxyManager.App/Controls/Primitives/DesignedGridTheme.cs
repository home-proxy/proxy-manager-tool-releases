using System.Drawing.Drawing2D;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal static class DesignedGridTheme
{
    public const int HeaderHeight = 52;
    public const int RowHeight = 48;
    public const int FooterHeight = 32;
    public const int IconButtonSize = 32;
    public const int ActionIconSize = 20;

    public static readonly Color HeaderBackColor = Color.FromArgb(245, 245, 245);
    public static readonly Color ActiveHeaderBackColor = Color.FromArgb(224, 224, 224);
    public static readonly Color BorderColor = Color.FromArgb(224, 224, 224);
    public static readonly Color RowBorderColor = Color.FromArgb(224, 224, 224);
    public static readonly Color SelectedRowBackColor = Color.FromArgb(235, 243, 252);
    public static readonly Color SelectedRowAccentColor = Color.FromArgb(15, 108, 189);
    public static readonly Color TextColor = Color.FromArgb(36, 36, 36);
    public static readonly Color MutedColor = Color.FromArgb(115, 115, 115);
    public static readonly Color IconButtonBackColor = Color.FromArgb(243, 243, 243);
    public static readonly Color AccentColor = Color.FromArgb(15, 108, 189);
    public static readonly Color DangerColor = Color.FromArgb(197, 15, 31);
    public static readonly Font CellFont = CreateFont(1.25f, FontStyle.Regular);
    public static readonly Font ActiveHeaderFont = new(CellFont, FontStyle.Bold);
    public static readonly Font SmallFont = CreateFont(0.75f, FontStyle.Regular);

    public static void Apply(DataGridView grid)
    {
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBackColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
        grid.ColumnHeadersDefaultCellStyle.Font = CellFont;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderBackColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextColor;
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = TextColor;
        grid.DefaultCellStyle.Font = CellFont;
        grid.DefaultCellStyle.SelectionBackColor = SelectedRowBackColor;
        grid.DefaultCellStyle.SelectionForeColor = TextColor;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = SelectedRowBackColor;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextColor;
        grid.GridColor = RowBorderColor;
        grid.RowHeadersVisible = false;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
        grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
        grid.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
        grid.AllowUserToResizeRows = false;

        ApplyMetrics(grid);
        grid.SelectionChanged -= RefreshHeader;
        grid.SelectionChanged += RefreshHeader;
        grid.CellPainting -= PaintDefaultHeaderCell;
        grid.CellPainting += PaintDefaultHeaderCell;
        grid.Paint -= PaintGridLines;
        grid.Paint += PaintGridLines;
        grid.DataBindingComplete -= ApplyMetricsAfterBinding;
        grid.DataBindingComplete += ApplyMetricsAfterBinding;
    }

    public static void ApplyMetrics(DataGridView grid)
    {
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = HeaderHeight;
        grid.RowTemplate.Height = RowHeight;
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.Height = RowHeight;
            row.MinimumHeight = RowHeight;
        }
    }

    public static void PaintHeaderCell(DataGridView grid, DataGridViewCellPaintingEventArgs e, bool active)
    {
        if (e.Graphics is null || e.RowIndex != -1 || e.ColumnIndex < 0)
        {
            return;
        }

        PaintCellBackgroundAndBorder(e.Graphics, e.CellBounds, isHeader: true, isActiveHeader: active);
        TextRenderer.DrawText(
            e.Graphics,
            grid.Columns[e.ColumnIndex].HeaderText,
            active ? ActiveHeaderFont : CellFont,
            e.CellBounds,
            TextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.Handled = true;
    }

    public static void PaintCellBackgroundAndBorder(
        Graphics graphics,
        Rectangle bounds,
        bool isHeader,
        bool isActiveHeader = false,
        bool isSelected = false)
    {
        using var background = new SolidBrush(isActiveHeader
            ? ActiveHeaderBackColor
            : isHeader
                ? HeaderBackColor
                : isSelected
                    ? SelectedRowBackColor
                    : Color.White);
        graphics.FillRectangle(background, bounds);
    }

    public static bool IsSelected(DataGridViewCellPaintingEventArgs e) =>
        (e.State & DataGridViewElementStates.Selected) != 0;

    public static void DrawCenteredImage(Graphics graphics, Rectangle bounds, Image icon, int iconSize)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var size = Math.Min(iconSize, Math.Min(bounds.Width, bounds.Height));
        if (size <= 0)
        {
            return;
        }

        var iconBounds = new Rectangle(
            bounds.Left + (bounds.Width - size) / 2,
            bounds.Top + (bounds.Height - size) / 2,
            size,
            size);
        graphics.DrawImage(icon, iconBounds);
    }

    public static void DrawGrayIconButton(Graphics graphics, Rectangle bounds, Image icon)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = AppButton.CreateRoundedRectangle(
            new Rectangle(bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1),
            4);
        using var background = new SolidBrush(IconButtonBackColor);
        graphics.FillPath(background, path);
        DrawCenteredImage(graphics, bounds, icon, ActionIconSize);
    }

    private static Font CreateFont(float sizeDelta, FontStyle style)
    {
        var baseFont = SystemFonts.MessageBoxFont ?? Control.DefaultFont;
        return new Font(baseFont.FontFamily, baseFont.Size + sizeDelta, style, baseFont.Unit);
    }

    private static void RefreshHeader(object? sender, EventArgs e)
    {
        if (sender is DataGridView grid)
        {
            grid.Invalidate();
        }
    }

    private static void ApplyMetricsAfterBinding(object? sender, DataGridViewBindingCompleteEventArgs e)
    {
        if (sender is DataGridView grid)
        {
            ApplyMetrics(grid);
        }
    }

    private static void PaintDefaultHeaderCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid ||
            e.RowIndex != -1 ||
            e.ColumnIndex < 0 ||
            e.Graphics is null ||
            string.IsNullOrEmpty(grid.Columns[e.ColumnIndex].HeaderText))
        {
            return;
        }

        PaintHeaderCell(
            grid,
            e,
            grid.CurrentCell is not null &&
            e.ColumnIndex == grid.CurrentCell.ColumnIndex &&
            !Equals(grid.Tag, LightTheme.SkipActiveGridHeaderThemeTag));
    }

    private static void PaintGridLines(object? sender, PaintEventArgs e)
    {
        if (sender is not DataGridView grid || grid.ClientSize.Width <= 0 || grid.ClientSize.Height <= 0)
        {
            return;
        }

        using var pen = new Pen(RowBorderColor, 1);
        var bottom = grid.ClientSize.Height - 1;
        var right = grid.ClientSize.Width - 1;
        var headerBottom = grid.ColumnHeadersVisible ? Math.Min(bottom, grid.ColumnHeadersHeight - 1) : 0;
        var contentBottom = headerBottom;

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!row.Visible || !row.Displayed)
            {
                continue;
            }

            var bounds = grid.GetRowDisplayRectangle(row.Index, cutOverflow: true);
            if (bounds.Height <= 0)
            {
                continue;
            }

            contentBottom = Math.Max(contentBottom, Math.Min(bottom, bounds.Bottom - 1));
        }

        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (!column.Visible)
            {
                continue;
            }

            var bounds = grid.GetColumnDisplayRectangle(column.Index, cutOverflow: true);
            if (bounds.Width <= 0)
            {
                continue;
            }

            var x = Math.Min(right, bounds.Right - 1);
            e.Graphics.DrawLine(pen, x, 0, x, contentBottom);
        }

        if (grid.ColumnHeadersVisible)
        {
            e.Graphics.DrawLine(pen, 0, headerBottom, right, headerBottom);
        }

        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!row.Visible || !row.Displayed)
            {
                continue;
            }

            var bounds = grid.GetRowDisplayRectangle(row.Index, cutOverflow: true);
            if (bounds.Height <= 0)
            {
                continue;
            }

            var y = Math.Min(bottom, bounds.Bottom - 1);
            e.Graphics.DrawLine(pen, 0, y, right, y);
        }
    }
}
