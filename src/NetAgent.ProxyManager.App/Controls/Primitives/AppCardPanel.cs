using System.Drawing.Drawing2D;
using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal sealed class AppCardPanel : Panel
{
    public AppCardPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = LightTheme.Surface;
        Padding = new Padding(12);
    }

    public int Radius { get; set; } = 4;

    public Color BorderColor { get; set; } = Color.FromArgb(224, 224, 224);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        using var path = AppButton.CreateRoundedRectangle(bounds, Radius);
        using var pen = new Pen(BorderColor, 1);
        e.Graphics.DrawPath(pen, path);
    }
}
