using NetAgent.ProxyManager.App.Controls.Primitives;

namespace NetAgent.ProxyManager.App.Theme;

internal static class LightTheme
{
    public const string SkipButtonThemeTag = "__skip_button_theme__";
    public const string SkipActiveGridHeaderThemeTag = "__skip_active_grid_header_theme__";
    public static readonly Color Primary = Color.FromArgb(253, 67, 54);
    public static readonly Color Background = Color.White;
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceAlt = Color.FromArgb(236, 241, 250);
    public static readonly Color Accent = Color.FromArgb(35, 102, 209);
    public static readonly Color AccentSoft = Color.FromArgb(223, 235, 255);
    public static readonly Color Success = Color.FromArgb(20, 128, 84);
    public static readonly Color Warning = Color.FromArgb(196, 112, 16);
    public static readonly Color Danger = Color.FromArgb(190, 50, 50);
    public static readonly Color Foreground = Color.FromArgb(31, 41, 55);
    public static readonly Color Muted = Color.FromArgb(92, 104, 122);
    public static readonly Color Border = Color.FromArgb(214, 221, 232);
    public static readonly Color ButtonHover = Color.FromArgb(243, 246, 250);
    public const int InputHeight = 34;
    public const int InputUnderlineNormal = 1;
    public const int InputUnderlineActive = 2;
    public const int ButtonRadius = 6;
    public const int ButtonHeight = 38;
    public const int ToolbarButtonHeight = 40;

    public static void Apply(Control control)
    {
        control.BackColor = Background;
        control.ForeColor = Foreground;
        switch (control)
        {
            case AppTextInput:
                control.BackColor = Surface;
                control.ForeColor = Foreground;
                return;
            case AppDataGridView grid:
                StyleGrid(grid);
                return;
            case AppButton button:
                StyleButton(button);
                return;
        }

        foreach (Control child in control.Controls)
        {
            Apply(child);
        }

        switch (control)
        {
            case Button button:
                if (!Equals(button.Tag, SkipButtonThemeTag))
                {
                    StyleButton(button);
                }
                break;
            case TextBox textBox:
                textBox.BackColor = Surface;
                textBox.ForeColor = Foreground;
                textBox.BorderStyle = textBox.Multiline ? BorderStyle.FixedSingle : BorderStyle.None;
                break;
            case RichTextBox richTextBox:
                richTextBox.BackColor = Surface;
                richTextBox.ForeColor = Foreground;
                richTextBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox comboBox:
                StyleComboBox(comboBox);
                break;
            case NumericUpDown numericUpDown:
                numericUpDown.BackColor = Surface;
                numericUpDown.ForeColor = Foreground;
                break;
            case DataGridView grid:
                StyleGrid(grid);
                break;
            case StatusStrip statusStrip:
                statusStrip.BackColor = Surface;
                statusStrip.ForeColor = Muted;
                break;
            case TabControl tabControl:
                StyleTabControl(tabControl);
                break;
        }
    }

    public static AppButton CreateToolbarButton(
        string text,
        EventHandler onClick,
        Color? accentColor = null,
        Image? image = null)
    {
        var accent = accentColor ?? Accent;
        var button = new AppButton
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(12, 6, 12, 6),
            MinimumSize = new Size(0, ToolbarButtonHeight),
            Height = ToolbarButtonHeight,
            Tag = CreateDefaultToolbarStyle(accent),
            Variant = ResolveVariant(accent),
            AccentColor = accent,
            Filled = accent == Success,
            Image = image,
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = image is null ? TextImageRelation.Overlay : TextImageRelation.ImageBeforeText
        };
        button.Click += onClick;
        button.EnabledChanged += (_, _) => StyleButton(button);
        button.RefreshContentMinimumSize();
        if (!string.IsNullOrWhiteSpace(button.Text))
        {
            var preferredSize = button.GetPreferredSize(Size.Empty);
            button.MinimumSize = new Size(
                preferredSize.Width,
                Math.Max(preferredSize.Height, ToolbarButtonHeight));
        }

        StyleButton(button);
        return button;
    }

    public static void RefreshButtonStyle(Button button) => StyleButton(button);

    public static void SetPrimaryButton(Button button, Color accent)
    {
        button.Tag = new ButtonStyle(accent, Filled: true);
        StyleButton(button);
    }

    public static void SetSecondaryButton(Button button, Color accent)
    {
        button.Tag = new ButtonStyle(accent, Filled: false);
        StyleButton(button);
    }

    public static void StyleNormalButton(Button button)
    {
        button.BackColor = Surface;
        button.ForeColor = button.Enabled ? Foreground : Muted;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = SurfaceAlt;
        button.FlatAppearance.MouseDownBackColor = AccentSoft;
        button.Cursor = button.Enabled ? Cursors.Hand : Cursors.Default;
    }

    public static void StyleGrid(DataGridView grid)
    {
        DesignedGridTheme.Apply(grid);
    }

    public static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.BackColor = Surface;
        comboBox.ForeColor = Foreground;
        comboBox.Font = new Font("Segoe UI", 10f);
        comboBox.MinimumSize = new Size(Math.Max(80, comboBox.MinimumSize.Width), InputHeight);
        comboBox.Height = Math.Max(comboBox.Height, InputHeight);
    }

    public static void StyleTabControl(TabControl tabControl)
    {
        tabControl.BackColor = Background;
        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.ItemSize = new Size(210, 42);
        tabControl.SizeMode = TabSizeMode.Fixed;
        tabControl.Padding = new Point(16, 6);
        tabControl.DrawItem -= DrawTab;
        tabControl.DrawItem += DrawTab;
    }

    private static void StyleButton(Button button)
    {
        if (button is AppButton appButton)
        {
            var appStyle = ResolveButtonStyle(button);
            appButton.AccentColor = appStyle.Accent;
            appButton.Filled = appStyle.Filled;
            appButton.Variant = ResolveVariant(appStyle.Accent);
            appButton.RefreshContentMinimumSize();
            appButton.Invalidate();
            return;
        }

        var style = ResolveButtonStyle(button);
        var accent = style.Accent;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font(button.Font, FontStyle.Regular);
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 1;
        button.Cursor = button.Enabled ? Cursors.Hand : Cursors.Default;
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Padding = button.Image is null ? new Padding(12, 6, 12, 6) : new Padding(10, 6, 14, 6);
        button.TextImageRelation = button.Image is null
            ? TextImageRelation.Overlay
            : TextImageRelation.ImageBeforeText;

        if (!button.Enabled)
        {
            button.BackColor = ButtonHover;
            button.ForeColor = Color.FromArgb(156, 163, 175);
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = ButtonHover;
            button.FlatAppearance.MouseDownBackColor = ButtonHover;
            return;
        }

        if (style.Filled)
        {
            button.BackColor = accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = accent;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent, 0.10f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent, 0.08f);
            return;
        }

        button.BackColor = style.RestingBackColor;
        button.ForeColor = accent == Muted ? Foreground : accent;
        button.FlatAppearance.BorderColor = style.BorderColor;
        button.FlatAppearance.MouseOverBackColor = style.HoverBackColor;
        button.FlatAppearance.MouseDownBackColor = AccentSoft;
    }

    private static ButtonStyle CreateDefaultToolbarStyle(Color accent) =>
        new(accent, Filled: accent == Success);

    private static AppButtonVariant ResolveVariant(Color accent)
    {
        if (accent == Success)
        {
            return AppButtonVariant.Success;
        }

        if (accent == Warning)
        {
            return AppButtonVariant.Warning;
        }

        if (accent == Danger)
        {
            return AppButtonVariant.Danger;
        }

        if (accent == Muted)
        {
            return AppButtonVariant.Muted;
        }

        return AppButtonVariant.Secondary;
    }

    private static ButtonStyle ResolveButtonStyle(Button button) =>
        button.Tag switch
        {
            ButtonStyle style => style,
            Color color => CreateDefaultToolbarStyle(color),
            _ => CreateDefaultToolbarStyle(Accent)
        };

    private static Color Mix(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }

    private sealed record ButtonStyle(Color Accent, bool Filled)
    {
        public Color RestingBackColor => Filled ? Accent : Mix(Color.White, Accent, 0.045);
        public Color BorderColor => Filled
            ? Accent
            : Accent == Muted
                ? Color.FromArgb(132, 145, 164)
                : Mix(Color.White, Accent, 0.42);
        public Color HoverBackColor => Filled ? Accent : Mix(Color.White, Accent, 0.12);
    }

    private static void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs)
        {
            return;
        }

        var selected = e.Index == tabs.SelectedIndex;
        var bounds = e.Bounds;
        bounds.Inflate(-2, -2);

        using var background = new SolidBrush(selected ? Surface : SurfaceAlt);
        using var border = new Pen(selected ? Accent : Border, selected ? 2 : 1);
        using var textBrush = new SolidBrush(selected ? Accent : Foreground);
        using var font = new Font(
            tabs.Font,
            selected ? FontStyle.Bold : FontStyle.Regular);

        e.Graphics.FillRectangle(background, bounds);
        e.Graphics.DrawRectangle(border, bounds);

        var textBounds = bounds;
        if (tabs.ImageList is not null)
        {
            var imageKey = tabs.TabPages[e.Index].ImageKey;
            if (!string.IsNullOrWhiteSpace(imageKey) && tabs.ImageList.Images.ContainsKey(imageKey))
            {
                var image = tabs.ImageList.Images[imageKey];
                if (image is not null)
                {
                    var imagePoint = new Point(bounds.Left + 14, bounds.Top + (bounds.Height - image.Height) / 2);
                    e.Graphics.DrawImage(image, imagePoint);
                    textBounds = new Rectangle(bounds.Left + 38, bounds.Top, bounds.Width - 44, bounds.Height);
                }
            }
        }

        var textFormat = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center
        };
        e.Graphics.DrawString(tabs.TabPages[e.Index].Text, font, textBrush, textBounds, textFormat);
    }

    private static void RefreshSelectedHeader(object? sender, EventArgs e)
    {
        if (sender is DataGridView grid)
        {
            grid.Invalidate();
        }
    }

    private static void PaintActiveHeader(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid ||
            e.RowIndex != -1 ||
            e.ColumnIndex < 0 ||
            grid.CurrentCell is null ||
            e.ColumnIndex != grid.CurrentCell.ColumnIndex ||
            Equals(grid.Tag, SkipActiveGridHeaderThemeTag))
        {
            return;
        }

        if (e.Graphics is null)
        {
            return;
        }

        if (grid.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn &&
            string.IsNullOrEmpty(grid.Columns[e.ColumnIndex].HeaderText))
        {
            return;
        }

        e.PaintBackground(e.CellBounds, false);
        using var background = new SolidBrush(Accent);
        using var textBrush = new SolidBrush(Color.White);
        e.Graphics.FillRectangle(background, e.CellBounds);
        TextRenderer.DrawText(
            e.Graphics,
            Convert.ToString(e.FormattedValue) ?? string.Empty,
            grid.ColumnHeadersDefaultCellStyle.Font,
            new Rectangle(e.CellBounds.Left + 8, e.CellBounds.Top, e.CellBounds.Width - 8, e.CellBounds.Height),
            Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.Handled = true;
    }
}
