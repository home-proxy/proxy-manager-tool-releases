using System.Runtime.InteropServices;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal class AppComboBox : ComboBox
{
    private const int CB_SETITEMHEIGHT = 0x0153;

    // Closed ComboBox height is the selection-field height plus 6px of border.
    private const int SelectionChrome = 6;
    private const int SelectionHeight = LightTheme.FieldHeight - SelectionChrome;
    private const int TextLeftPadding = 8;

    private bool _applyingSelectionHeight;

    public AppComboBox()
    {
        IntegralHeight = false;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = SelectionHeight;
        Height = LightTheme.FieldHeight;
        MinimumSize = new Size(Math.Max(80, MinimumSize.Width), LightTheme.FieldHeight);
        DrawItem += DrawCenteredItem;
    }

    protected override Size DefaultSize => new(base.DefaultSize.Width, LightTheme.FieldHeight);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = base.GetPreferredSize(proposedSize);
        return new Size(Math.Max(size.Width, Width), LightTheme.FieldHeight);
    }

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if ((specified & BoundsSpecified.Height) != BoundsSpecified.None)
        {
            height = LightTheme.FieldHeight;
        }

        base.SetBoundsCore(x, y, width, height, specified);
        ApplySelectionHeight();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplySelectionHeight();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplySelectionHeight();
    }

    private void ApplySelectionHeight()
    {
        if (!IsHandleCreated || _applyingSelectionHeight)
        {
            return;
        }

        _applyingSelectionHeight = true;
        try
        {
            ItemHeight = SelectionHeight;
            SendMessage(Handle, CB_SETITEMHEIGHT, (IntPtr)(-1), (IntPtr)SelectionHeight);
        }
        finally
        {
            _applyingSelectionHeight = false;
        }
    }

    private void DrawCenteredItem(object? sender, DrawItemEventArgs e)
    {
        var isEdit = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
        var isHighlighted = !isEdit && (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var backColor = isHighlighted ? SystemColors.Highlight : BackColor;
        var textColor = !Enabled
            ? SystemColors.GrayText
            : isHighlighted
                ? SystemColors.HighlightText
                : ForeColor;
        using var background = new SolidBrush(backColor);
        e.Graphics.FillRectangle(background, e.Bounds);

        if (e.Index < 0)
        {
            return;
        }

        var textBounds = new Rectangle(
            e.Bounds.X + TextLeftPadding,
            e.Bounds.Y,
            Math.Max(1, e.Bounds.Width - TextLeftPadding - (isEdit ? 20 : 8)),
            e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            textBounds,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
