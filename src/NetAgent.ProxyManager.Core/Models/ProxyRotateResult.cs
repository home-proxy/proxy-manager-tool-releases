namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxyRotateResult
{
    public string Status { get; init; } = string.Empty;
    public string Proxy { get; init; } = string.Empty;
    public string Ip { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int? TimeRemaining { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string LastRotate { get; init; } = string.Empty;

    public bool IsSuccess =>
        string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(Proxy);
}
