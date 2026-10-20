namespace NetAgent.ProxyManager.Core.Models;

public sealed record ChromeBrowserDownloadProgress(string Stage, long BytesReceived, long? TotalBytes)
{
    public int? Percent => TotalBytes is > 0
        ? (int)Math.Clamp(BytesReceived * 100 / TotalBytes.Value, 0, 100)
        : null;
}
