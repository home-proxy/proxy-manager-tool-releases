using NetAgent.ProxyManager.App.Theme;

namespace NetAgent.ProxyManager.App.Controls.Primitives;

internal sealed class AppDataGridView : DataGridView
{
    public AppDataGridView()
    {
        LightTheme.StyleGrid(this);
    }
}
