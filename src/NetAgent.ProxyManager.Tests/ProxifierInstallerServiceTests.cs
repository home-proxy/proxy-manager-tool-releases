using FluentAssertions;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;
using NetAgent.ProxyManager.Infrastructure.Services;
using NetAgent.ProxyManager.Infrastructure.Storage;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProxifierInstallerServiceTests
{
    [Fact]
    public void ResolveInstallerPath_ShouldResolveRelativePathFromBaseDirectory()
    {
        var path = ProxifierInstallerService.ResolveInstallerPath(
            @"C:\App\",
            @"Installers\ProxifierSetup.exe");

        path.Should().Be(@"C:\App\Installers\ProxifierSetup.exe");
    }

    [Fact]
    public void BuildInstallerArguments_ShouldAppendQuotedLogPath()
    {
        var arguments = ProxifierInstallerService.BuildInstallerArguments(
            "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            @"C:\Users\Test User\AppData\Roaming\logs\proxifier setup.log");

        arguments.Should().Be("/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=\"C:\\Users\\Test User\\AppData\\Roaming\\logs\\proxifier setup.log\"");
    }

    [Fact]
    public async Task InstallOrRepairAsync_ShouldRejectMissingConfiguredHash()
    {
        using var scope = new TempFileScope();
        await File.WriteAllTextAsync(scope.InstallerPath, "installer");
        var runner = new RecordingInstallerProcessRunner(new InstallerProcessResult(true, 0));
        var service = CreateService(scope, runner, installerSha256: string.Empty, detectedPath: @"C:\Program Files\Proxifier\Proxifier.exe");

        var result = await service.InstallOrRepairAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("InstallerSha256");
        runner.Calls.Should().Be(0);
    }

    [Fact]
    public async Task InstallOrRepairAsync_ShouldRejectHashMismatch()
    {
        using var scope = new TempFileScope();
        await File.WriteAllTextAsync(scope.InstallerPath, "installer");
        var runner = new RecordingInstallerProcessRunner(new InstallerProcessResult(true, 0));
        var service = CreateService(scope, runner, installerSha256: new string('0', 64), detectedPath: @"C:\Program Files\Proxifier\Proxifier.exe");

        var result = await service.InstallOrRepairAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("SHA-256");
        runner.Calls.Should().Be(0);
    }

    [Fact]
    public async Task InstallOrRepairAsync_ShouldRunInstallerAndReturnDetectedPathWhenHashMatches()
    {
        using var scope = new TempFileScope();
        await File.WriteAllTextAsync(scope.InstallerPath, "installer");
        var hash = await ProxifierInstallerService.ComputeSha256Async(scope.InstallerPath, CancellationToken.None);
        var detectedPath = @"C:\Program Files\Proxifier\Proxifier.exe";
        var runner = new RecordingInstallerProcessRunner(new InstallerProcessResult(true, 0));
        var ownershipStore = new FakeProxifierOwnershipStore
        {
            DetectedAfterInstall = new ProxifierInstallMetadata(detectedPath, @"""C:\Program Files\Proxifier\unins000.exe""")
        };
        var service = CreateService(scope, runner, hash, detectedPath, ownershipStore);

        var result = await service.InstallOrRepairAsync(CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProxifierExecutablePath.Should().Be(detectedPath);
        runner.Calls.Should().Be(1);
        runner.FileName.Should().Be(scope.InstallerPath);
        runner.Arguments.Should().Contain("/VERYSILENT");
        runner.Arguments.Should().Contain("/LOG=");
        ownershipStore.RecordedMetadata.Should().NotBeNull();
        ownershipStore.RecordedMetadata!.ProxifierExecutablePath.Should().Be(detectedPath);
        ownershipStore.RecordedMetadata.UninstallString.Should().Contain("unins000.exe");
    }

    [Fact]
    public async Task InstallOrRepairAsync_ShouldUseExistingProxifierWithoutTakingOwnership()
    {
        using var scope = new TempFileScope();
        await File.WriteAllTextAsync(scope.InstallerPath, "installer");
        var hash = await ProxifierInstallerService.ComputeSha256Async(scope.InstallerPath, CancellationToken.None);
        var existingPath = @"C:\Program Files\Proxifier\Proxifier.exe";
        var runner = new RecordingInstallerProcessRunner(new InstallerProcessResult(true, 0));
        var ownershipStore = new FakeProxifierOwnershipStore
        {
            ExistingInstall = new ProxifierInstallMetadata(existingPath, @"""C:\Program Files\Proxifier\unins000.exe""")
        };
        var service = CreateService(scope, runner, hash, existingPath, ownershipStore);

        var result = await service.InstallOrRepairAsync(CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProxifierExecutablePath.Should().Be(existingPath);
        runner.Calls.Should().Be(0);
        ownershipStore.RecordedMetadata.Should().BeNull();
    }

    [Fact]
    public async Task InstallOrRepairAsync_ShouldExplainPendingRestartWhenInstallerLogRequiresRestart()
    {
        using var scope = new TempFileScope();
        await File.WriteAllTextAsync(scope.InstallerPath, "installer");
        var hash = await ProxifierInstallerService.ComputeSha256Async(scope.InstallerPath, CancellationToken.None);
        var runner = new RecordingInstallerProcessRunner(new InstallerProcessResult(true, 8))
        {
            LogContentToWrite = "Found pending rename or delete that matches one of our files."
        };
        var service = CreateService(scope, runner, hash, detectedPath: null);

        var result = await service.InstallOrRepairAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(8);
        result.LogPath.Should().NotBeNullOrWhiteSpace();
        result.Message.Should().Contain("restart");
        result.Message.Should().Contain(result.LogPath);
    }

    private static ProxifierInstallerService CreateService(
        TempFileScope scope,
        RecordingInstallerProcessRunner runner,
        string installerSha256,
        string? detectedPath,
        FakeProxifierOwnershipStore? ownershipStore = null)
    {
        var options = Options.Create(new ProxifierOptions
        {
            BundledInstallerPath = scope.InstallerPath,
            InstallerSha256 = installerSha256,
            SilentInstallArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            InstallerTimeoutSeconds = 60
        });

        return new ProxifierInstallerService(
            options,
            new AppDataPaths(scope.AppDataRoot),
            new FakeProxifierService(detectedPath),
            runner,
            ownershipStore ?? new FakeProxifierOwnershipStore());
    }

    private sealed class RecordingInstallerProcessRunner(InstallerProcessResult result) : IInstallerProcessRunner
    {
        public int Calls { get; private set; }
        public string? FileName { get; private set; }
        public string? Arguments { get; private set; }
        public string? LogContentToWrite { get; init; }

        public Task<InstallerProcessResult> RunElevatedAndWaitAsync(
            string fileName,
            string arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Calls++;
            FileName = fileName;
            Arguments = arguments;
            if (!string.IsNullOrEmpty(LogContentToWrite))
            {
                var logPath = ExtractLogPath(arguments);
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.WriteAllText(logPath, LogContentToWrite);
            }

            return Task.FromResult(result);
        }

        private static string ExtractLogPath(string arguments)
        {
            const string marker = "/LOG=\"";
            var start = arguments.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                throw new InvalidOperationException("Missing /LOG argument.");
            }

            start += marker.Length;
            var end = arguments.IndexOf('"', start);
            if (end < 0)
            {
                throw new InvalidOperationException("Invalid /LOG argument.");
            }

            return arguments[start..end];
        }
    }

    private sealed class FakeProxifierService(string? detectedPath) : IProxifierService
    {
        public Task<ProxifierCommandResult> LoadProfileAsync(string proxifierExePath, string profilePath, CancellationToken cancellationToken) =>
            Task.FromResult(new ProxifierCommandResult(true, "ok"));

        public Task<ProxifierCommandResult> HideWindowAsync(string proxifierExePath, CancellationToken cancellationToken) =>
            Task.FromResult(new ProxifierCommandResult(true, "hidden"));

        public Task<ProxifierCommandResult> StopProcessesAsync(string proxifierExePath, CancellationToken cancellationToken) =>
            Task.FromResult(new ProxifierCommandResult(true, "stopped"));

        public ProxifierWindowState GetWindowState(string proxifierExePath) => ProxifierWindowState.NotRunning;

        public ProxifierCommandResult ToggleWindowVisibility(string proxifierExePath) => new(true, "ok");

        public bool TryDetectProxifierPath(out string? path)
        {
            path = detectedPath;
            return !string.IsNullOrWhiteSpace(path);
        }
    }

    private sealed class FakeProxifierOwnershipStore : IProxifierOwnershipStore
    {
        private bool _installerHasRun;

        public ProxifierInstallMetadata? ExistingInstall { get; init; }
        public ProxifierInstallMetadata? DetectedAfterInstall { get; init; }
        public ProxifierInstallMetadata? RecordedMetadata { get; private set; }

        public bool TryDetectInstalled(out ProxifierInstallMetadata metadata)
        {
            if (!_installerHasRun)
            {
                _installerHasRun = true;
                if (ExistingInstall is not null)
                {
                    metadata = ExistingInstall;
                    return true;
                }

                metadata = new ProxifierInstallMetadata(null, null);
                return false;
            }

            if (DetectedAfterInstall is not null)
            {
                metadata = DetectedAfterInstall;
                return true;
            }

            metadata = new ProxifierInstallMetadata(null, null);
            return false;
        }

        public void RecordInstalledByProxyManager(ProxifierInstallMetadata metadata)
        {
            RecordedMetadata = metadata;
        }
    }

    private sealed class TempFileScope : IDisposable
    {
        public TempFileScope()
        {
            Root = Path.Combine(Path.GetTempPath(), "netagent-proxifier-tests", Guid.NewGuid().ToString("N"));
            AppDataRoot = Path.Combine(Root, "appdata");
            Directory.CreateDirectory(Root);
            InstallerPath = Path.Combine(Root, "ProxifierSetup.exe");
        }

        public string Root { get; }
        public string AppDataRoot { get; }
        public string InstallerPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
