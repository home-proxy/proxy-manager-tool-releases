using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProxifierSessionServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldHideProxifierWindowAfterLoadingProfile()
    {
        using var scope = new TempDirectoryScope();
        var proxifierService = new FakeProxifierService();
        var service = new ProxifierSessionService(new FakeProfileBuilder(), proxifierService);

        var result = await service.StartAsync(CreateSettings(scope.Root), [], [], CancellationToken.None);

        result.Success.Should().BeTrue();
        proxifierService.LoadProfileCalls.Should().Be(1);
        proxifierService.HideWindowCalls.Should().Be(1);
        proxifierService.StopProcessesCalls.Should().Be(0);
    }

    [Fact]
    public async Task StopAsync_ShouldTerminateProxifierProcessesAfterLoadingInactiveProfile()
    {
        using var scope = new TempDirectoryScope();
        var proxifierService = new FakeProxifierService();
        var service = new ProxifierSessionService(new FakeProfileBuilder(), proxifierService);

        var result = await service.StopAsync(CreateSettings(scope.Root), CancellationToken.None);

        result.Success.Should().BeTrue();
        proxifierService.LoadProfileCalls.Should().Be(1);
        proxifierService.HideWindowCalls.Should().Be(0);
        proxifierService.StopProcessesCalls.Should().Be(1);
    }

    private static AppSettings CreateSettings(string root) => new()
    {
        ProxifierExecutablePath = @"C:\Program Files\Proxifier\Proxifier.exe",
        ProfileOutputDirectory = Path.Combine(root, "profiles")
    };

    private sealed class FakeProfileBuilder : IProxifierProfileBuilder
    {
        public GeneratedProfileResult Build(ProxifierProfileModel model) =>
            new("<xml />", "<xml />");
    }

    private sealed class FakeProxifierService : IProxifierService
    {
        public int LoadProfileCalls { get; private set; }
        public int HideWindowCalls { get; private set; }
        public int StopProcessesCalls { get; private set; }

        public Task<ProxifierCommandResult> LoadProfileAsync(
            string proxifierExePath,
            string profilePath,
            CancellationToken cancellationToken)
        {
            LoadProfileCalls++;
            return Task.FromResult(new ProxifierCommandResult(true, "loaded"));
        }

        public Task<ProxifierCommandResult> HideWindowAsync(string proxifierExePath, CancellationToken cancellationToken)
        {
            HideWindowCalls++;
            return Task.FromResult(new ProxifierCommandResult(true, "hidden"));
        }

        public Task<ProxifierCommandResult> StopProcessesAsync(string proxifierExePath, CancellationToken cancellationToken)
        {
            StopProcessesCalls++;
            return Task.FromResult(new ProxifierCommandResult(true, "stopped"));
        }

        public ProxifierWindowState GetWindowState(string proxifierExePath) => ProxifierWindowState.Hidden;

        public ProxifierCommandResult ToggleWindowVisibility(string proxifierExePath) => new(true, "ok");

        public bool TryDetectProxifierPath(out string? path)
        {
            path = null;
            return false;
        }
    }

    private sealed class TempDirectoryScope : IDisposable
    {
        public TempDirectoryScope()
        {
            Root = Path.Combine(Path.GetTempPath(), "netagent-session-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
