namespace NetAgent.ProxyManager.Core.Models;

// One running tool-owned Chrome instance, i.e. one --user-data-dir. All of its traffic leaves
// through the NetworkService utility process, which is what the Proxifier pid rule must target;
// it can be null for a moment while Chrome respawns it.
public sealed record ChromeProfileRuntimeInfo(
    string UserDataDirectory,
    int BrowserProcessId,
    int? NetworkServiceProcessId);
