using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ChromeProfileServiceTests
{
    private const string Root = @"C:\Users\me\AppData\Roaming\ProxyManager\chrome-profiles";
    private const string ToolChrome = @"C:\Users\me\AppData\Roaming\ProxyManager\browser\155\chrome-win64\chrome.exe";

    [Fact]
    public void CreateProfile_ShouldGiveEachProfileItsOwnDataDirectoryUnderRoot()
    {
        var service = CreateService([]);

        var first = service.CreateProfile("  Shop 1 ");
        var second = service.CreateProfile("Shop 2");

        first.TargetType.Should().Be(ApplicationTargetType.ChromeProfile);
        first.ChromeProfileName.Should().Be("Shop 1");
        first.ChromeUserDataDirectory.Should().Be(Path.Combine(Root, first.Id.ToString("N")));
        second.ChromeUserDataDirectory.Should().NotBe(first.ChromeUserDataDirectory);
        first.IsEnabled.Should().BeFalse("a new profile has no proxy yet");
        first.AutoAssignProxy.Should().BeFalse();
    }

    [Fact]
    public void CreateProfile_ShouldRejectEmptyName()
    {
        var act = () => CreateService([]).CreateProfile("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task RefreshRuntimeTargetsAsync_ShouldSetNetworkServicePidOrClearIt()
    {
        var service = CreateService([]);
        var open = service.CreateProfile("Open");
        var respawning = service.CreateProfile("Respawning");
        var closed = service.CreateProfile("Closed");
        closed.ProcessId = 999; // stale PID from a previous run
        var app = new ApplicationRule { ExecutableName = "zalo.exe", ProcessId = 5 };
        service = CreateService(
        [
            new ChromeProfileRuntimeInfo(open.ChromeUserDataDirectory!.ToUpperInvariant(), 100, 101),
            new ChromeProfileRuntimeInfo(respawning.ChromeUserDataDirectory!, 200, null)
        ]);

        await service.RefreshRuntimeTargetsAsync([open, respawning, closed, app], CancellationToken.None);

        open.ProcessId.Should().Be(101);
        respawning.ProcessId.Should().BeNull("Proxifier must not route a PID that no longer exists");
        closed.ProcessId.Should().BeNull();
        app.ProcessId.Should().Be(5, "non-Chrome rules are left alone");
    }

    [Fact]
    public void BuildLaunchArguments_ShouldIsolateProfileAndDisableNonProxiedUdp()
    {
        var rule = CreateService([]).CreateProfile("Shop 1");

        var arguments = ChromeProfileService.BuildLaunchArguments(rule);

        arguments.Should().Contain($"--user-data-dir={rule.ChromeUserDataDirectory}");
        arguments.Should().Contain("--disable-quic");
        arguments.Should().Contain("--force-webrtc-ip-handling-policy=disable_non_proxied_udp");
        arguments.Should().Contain("--no-first-run");
    }

    [Fact]
    public async Task ScanAsync_ShouldNotScanWhenToolBrowserIsNotInstalled()
    {
        var scanner = new StubScanner([new ChromeProfileRuntimeInfo(@"C:\x", 1, 2)]);
        var service = new ChromeProfileService(scanner, new ChromeProfileRoutingTests.StubBrowserProvisioner([]), Root);

        (await service.ScanAsync(CancellationToken.None)).Should().BeEmpty();
        scanner.Calls.Should().Be(0);
    }

    [Fact]
    public async Task DeleteProfileDataAsync_ShouldNeverDeleteOutsideProfilesRoot()
    {
        var outside = Path.Combine(Path.GetTempPath(), "netagent-chrome-outside", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "netagent-chrome-root", Guid.NewGuid().ToString("N"));
            var service = new ChromeProfileService(new StubScanner([]), new ChromeProfileRoutingTests.StubBrowserProvisioner([]), root);
            var rule = service.CreateProfile("Tampered");
            rule.ChromeUserDataDirectory = outside;

            await service.DeleteProfileDataAsync(rule, CancellationToken.None);

            Directory.Exists(outside).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteProfileDataAsync_ShouldRemoveTheProfileDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "netagent-chrome-root", Guid.NewGuid().ToString("N"));
        var service = new ChromeProfileService(new StubScanner([]), new ChromeProfileRoutingTests.StubBrowserProvisioner([]), root);
        var rule = service.CreateProfile("Shop");
        Directory.CreateDirectory(Path.Combine(rule.ChromeUserDataDirectory!, "Default"));
        File.WriteAllText(Path.Combine(rule.ChromeUserDataDirectory!, "Default", "Cookies"), "x");
        try
        {
            await service.DeleteProfileDataAsync(rule, CancellationToken.None);

            Directory.Exists(rule.ChromeUserDataDirectory).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ChromeProfileService CreateService(IReadOnlyList<ChromeProfileRuntimeInfo> runtimes) =>
        new(new StubScanner(runtimes), new ChromeProfileRoutingTests.StubBrowserProvisioner([ToolChrome]), Root);

    private sealed class StubScanner(IReadOnlyList<ChromeProfileRuntimeInfo> runtimes) : IChromeProfileProcessScanner
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<ChromeProfileRuntimeInfo>> ScanAsync(
            IReadOnlyCollection<string> browserExecutablePaths,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(runtimes);
        }
    }
}
