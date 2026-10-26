using System.Diagnostics;
using System.Reflection;
using System.Text;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.App.Services;

internal static class AppUpdateInstaller
{
    public const long MinimumExeBytes = 1_000_000;

    private const string Script = """
        @echo off
        setlocal EnableExtensions
        set "APP_PID=%~1"
        set "SOURCE=%~2"
        set "TARGET=%~3"
        set /a TRIES=0
        :wait
        set /a TRIES+=1
        if %TRIES% GTR 60 exit /b 1
        tasklist /FI "PID eq %APP_PID%" 2>nul | find " %APP_PID% " >nul
        if not errorlevel 1 (
          ping -n 2 127.0.0.1 >nul
          goto wait
        )
        copy /Y "%SOURCE%" "%TARGET%" >nul
        if errorlevel 1 (
          ping -n 2 127.0.0.1 >nul
          goto wait
        )
        del /F /Q "%SOURCE%" >nul 2>&1
        start "" "%TARGET%"
        del /F /Q "%~f0" >nul 2>&1
        """;

    public static AppVersion? CurrentVersion { get; } = ReadCurrentVersion();

    public static string DisplayVersion => CurrentVersion?.ToDisplayString() ?? "không xác định";

    public static bool IsDebugBuild
    {
        get
        {
            var configuration = (Assembly.GetEntryAssembly() ?? typeof(AppUpdateInstaller).Assembly)
                .GetCustomAttribute<AssemblyConfigurationAttribute>()
                ?.Configuration;
            return string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static string? GetInstalledExecutablePath()
    {
        var path = Environment.ProcessPath;
        return !string.IsNullOrWhiteSpace(path) &&
            path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(path)
            ? path
            : null;
    }

    public static string GetDownloadPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProxyManager",
            "updates");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "ProxyManager.App.exe");
    }

    public static void EnsureInstallDirectoryWritable(string executablePath)
    {
        var directory = Path.GetDirectoryName(executablePath)
            ?? throw new InvalidOperationException("Không xác định được thư mục đang chạy.");
        var probe = Path.Combine(directory, $".update-probe-{Guid.NewGuid():N}");
        try
        {
            using (File.Create(probe))
            {
            }
        }
        finally
        {
            if (File.Exists(probe))
            {
                File.Delete(probe);
            }
        }
    }

    public static void ValidateDownloadedExe(string path, long? expectedSize)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MinimumExeBytes || (expectedSize is > 0 && info.Length != expectedSize))
        {
            throw new InvalidDataException("Tệp tải về không phải bản cài ProxyManager.");
        }

        Span<byte> magic = stackalloc byte[2];
        using var stream = info.OpenRead();
        if (stream.Read(magic) != 2 || magic[0] != (byte)'M' || magic[1] != (byte)'Z')
        {
            throw new InvalidDataException("Tệp tải về không phải bản cài ProxyManager.");
        }
    }

    public static void StartReplaceAndRestart(string downloadedPath, string installedPath)
    {
        var scriptPath = Path.Combine(
            Path.GetDirectoryName(downloadedPath) ?? Path.GetTempPath(),
            "apply-update.cmd");
        File.WriteAllText(scriptPath, Script.Replace("\n", "\r\n", StringComparison.Ordinal), Encoding.ASCII);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c \"\"{scriptPath}\" {Environment.ProcessId} \"{downloadedPath}\" \"{installedPath}\"\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static AppVersion? ReadCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppUpdateInstaller).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (AppVersion.TryParse(informational, out var version))
        {
            return version;
        }

        var assemblyVersion = assembly.GetName().Version;
        return assemblyVersion is null
            ? null
            : new AppVersion(assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build, assemblyVersion.Revision);
    }
}
