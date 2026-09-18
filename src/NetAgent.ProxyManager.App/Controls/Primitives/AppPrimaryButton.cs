using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal sealed class AppPrimaryButton : AppButton
{
    public AppPrimaryButton()
    {
        Height = LightTheme.ButtonHeight;
        MinimumSize = new Size(0, LightTheme.ButtonHeight);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        LightTheme.SetPrimaryButton(this, LightTheme.Accent);
    }
}
