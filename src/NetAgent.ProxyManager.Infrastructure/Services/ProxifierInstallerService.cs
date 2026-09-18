using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;
using NetAgent.ProxyManager.Infrastructure.Storage;

namespace NetAgent.ProxyManager.Infrastructure.Services;

public sealed class ProxifierInstallerService : IProxifierInstallerService
{
    private readonly ProxifierOptions _options;
    private readonly AppDataPaths _paths;
    private readonly IProxifierService _proxifierService;
    private readonly IInstallerProcessRunner _processRunner;
    private readonly IProxifierOwnershipStore _ownershipStore;

    public ProxifierInstallerService(
        IOptions<ProxifierOptions> options,
        AppDataPaths paths,
        IProxifierService proxifierService,
        IInstallerProcessRunner processRunner,
        IProxifierOwnershipStore ownershipStore)
    {
        _options = options.Value;
        _paths = paths;
        _proxifierService = proxifierService;
        _processRunner = processRunner;
        _ownershipStore = ownershipStore;
    }

    public bool TryResolveBundledInstallerPath(out string installerPath)
    {
        installerPath = ResolveInstallerPath(AppContext.BaseDirectory, _options.BundledInstallerPath);
        return File.Exists(installerPath);
    }

    public async Task<ProxifierInstallResult> InstallOrRepairAsync(CancellationToken cancellationToken)
    {
        if (_ownershipStore.TryDetectInstalled(out var existingInstall) &&
            TryResolveDetectedExecutablePath(existingInstall, out var existingProxifierExePath))
        {
            return new ProxifierInstallResult(
                true,
                "Đã phát hiện Proxifier đã được cài đặt.",
                existingProxifierExePath);
        }

        if (!TryResolveBundledInstallerPath(out var installerPath))
        {
            return new ProxifierInstallResult(
                false,
                $"Không tìm thấy ProxifierSetup.exe tại: {installerPath}",
                InstallerPath: installerPath);
        }

        var expectedHash = NormalizeHash(_options.InstallerSha256);
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return new ProxifierInstallResult(
                false,
                "Chưa cấu hình Proxifier:InstallerSha256 cho ProxifierSetup.exe.",
                InstallerPath: installerPath);
        }

        var actualHash = await ComputeSha256Async(installerPath, cancellationToken);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            return new ProxifierInstallResult(
                false,
                $"SHA-256 của ProxifierSetup.exe không khớp. Actual: {actualHash}",
                InstallerPath: installerPath);
        }

        _paths.EnsureDirectories();
        var logPath = Path.Combine(_paths.LogsDirectory, $"proxifier-setup-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
        var arguments = BuildInstallerArguments(_options.SilentInstallArguments, logPath);
        var runResult = await _processRunner.RunElevatedAndWaitAsync(
            installerPath,
            arguments,
            TimeSpan.FromSeconds(Math.Max(30, _options.InstallerTimeoutSeconds)),
            cancellationToken);

        if (!runResult.Started)
        {
            return new ProxifierInstallResult(
                false,
                runResult.ErrorMessage ?? "Không thể khởi động installer Proxifier.",
                InstallerPath: installerPath,
                LogPath: logPath);
        }

        if (runResult.ExitCode is not 0)
        {
            return new ProxifierInstallResult(
                false,
                BuildFailedInstallerMessage(runResult.ExitCode, logPath),
                ExitCode: runResult.ExitCode,
                InstallerPath: installerPath,
                LogPath: logPath);
        }

        if (_proxifierService.TryDetectProxifierPath(out var proxifierExePath))
        {
            TryRecordOwnership(proxifierExePath);
            return new ProxifierInstallResult(
                true,
                "Đã cài đặt Proxifier.",
                proxifierExePath,
                runResult.ExitCode,
                installerPath,
                logPath);
        }

        return new ProxifierInstallResult(
            false,
            "Installer đã chạy thành công nhưng app không tìm thấy Proxifier.exe.",
            ExitCode: runResult.ExitCode,
            InstallerPath: installerPath,
            LogPath: logPath);
    }

    private bool TryResolveDetectedExecutablePath(
        ProxifierInstallMetadata metadata,
        out string proxifierExePath)
    {
        if (!string.IsNullOrWhiteSpace(metadata.ProxifierExecutablePath) &&
            File.Exists(metadata.ProxifierExecutablePath))
        {
            proxifierExePath = metadata.ProxifierExecutablePath;
            return true;
        }

        if (_proxifierService.TryDetectProxifierPath(out var detectedPath) &&
            !string.IsNullOrWhiteSpace(detectedPath))
        {
            proxifierExePath = detectedPath;
            return true;
        }

        proxifierExePath = string.Empty;
        return false;
    }

    private void TryRecordOwnership(string? proxifierExePath)
    {
        try
        {
            if (_ownershipStore.TryDetectInstalled(out var installed) &&
                !string.IsNullOrWhiteSpace(installed.UninstallString))
            {
                _ownershipStore.RecordInstalledByProxyManager(installed with
                {
                    ProxifierExecutablePath = FirstNonEmpty(installed.ProxifierExecutablePath, proxifierExePath)
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
        }
    }

    public static string ResolveInstallerPath(string baseDirectory, string configuredPath)
    {
        var path = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    public static string BuildInstallerArguments(string silentArguments, string logPath)
    {
        var args = string.IsNullOrWhiteSpace(silentArguments)
            ? "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
            : silentArguments.Trim();

        return $"{args} /LOG=\"{logPath}\"";
    }

    public static string BuildFailedInstallerMessage(int? exitCode, string logPath)
    {
        var logText = TryReadLog(logPath);
        if (RequiresWindowsRestart(logText))
        {
            return $"Windows cần restart để hoàn tất cài/gỡ Proxifier trước đó. Hãy restart máy rồi mở lại app. Log: {logPath}";
        }

        return $"Installer Proxifier kết thúc với exit code {exitCode}. Log: {logPath}";
    }

    private static string TryReadLog(string logPath)
    {
        try
        {
            return File.Exists(logPath) ? File.ReadAllText(logPath) : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static bool RequiresWindowsRestart(string logText) =>
        logText.Contains("Found pending rename or delete", StringComparison.OrdinalIgnoreCase) ||
        logText.Contains("previous program was not completed", StringComparison.OrdinalIgnoreCase) ||
        logText.Contains("Need to restart Windows? Yes", StringComparison.OrdinalIgnoreCase);

    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLower(CultureInfo.InvariantCulture);
    }

    public static string NormalizeHash(string hash) =>
        hash
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
