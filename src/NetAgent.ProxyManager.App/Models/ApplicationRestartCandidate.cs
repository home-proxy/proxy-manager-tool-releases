using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Models;

public sealed record ApplicationRestartCandidate(
    ApplicationRule Rule,
    string DisplayName,
    Image Icon);
