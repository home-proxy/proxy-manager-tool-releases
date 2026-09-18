namespace NetAgent.ProxyManager.Core.Models;

public sealed class AppSettings
{
    public string ProxifierExecutablePath { get; set; } = string.Empty;
    public string ProfileOutputDirectory { get; set; } = string.Empty;
    public bool DefaultRouteDirect { get; set; } = true;
    public ProxyRestartConfirmationPreference ProxyRestartConfirmationPreference { get; set; } =
        ProxyRestartConfirmationPreference.Ask;
    public Dictionary<string, Dictionary<string, bool>> DataGridColumnVisibility { get; set; } = [];
}
