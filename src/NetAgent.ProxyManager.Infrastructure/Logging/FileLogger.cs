using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Infrastructure.Storage;

namespace NetAgent.ProxyManager.Infrastructure.Logging;

public sealed class FileLogger(AppDataPaths paths, ILoggerSanitizer sanitizer)
{
    public async Task InfoAsync(string message, CancellationToken cancellationToken = default) =>
        await WriteAsync("INFO", message, cancellationToken);

    public async Task ErrorAsync(string message, CancellationToken cancellationToken = default) =>
        await WriteAsync("ERROR", message, cancellationToken);

    private async Task WriteAsync(string level, string message, CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        var sanitized = sanitizer.Sanitize(message);
        var line = $"{DateTimeOffset.Now:O} [{level}] {sanitized}{Environment.NewLine}";
        await File.AppendAllTextAsync(paths.LogFilePath, line, cancellationToken);
    }
}
