namespace NetAgent.ProxyManager.Core.Models;

public sealed record AppRelease(AppVersion Version, string TagName, Uri DownloadUri, string AssetName, long? Size);

public readonly record struct AppUpdateDownloadProgress(long BytesReceived, long? TotalBytes);
