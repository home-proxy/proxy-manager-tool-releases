using System.Xml.Linq;
using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

// Proxifier profile, eligibility and storage rules for TargetType.ChromeProfile — see docs/09-chrome-profiles.md.
public sealed class ChromeProfileRoutingTests
{
    private const string ToolChromePath = @"C:\Users\me\AppData\Roaming\ProxyManager\browser\155.0.8059.84\chrome-win64\chrome.exe";

    [Fact]
    public void Build_ShouldRouteOpenChromeProfileByNetworkServicePid()
    {
        var proxy = CreateProxy();
        var rule = CreateChromeProfile(proxy, networkServicePid: 4242);

        var rules = BuildRules(new ProxifierProfileModel { Proxies = [proxy], Rules = [rule] });

        var chromeRule = rules.Single(element => element.Element("Name")!.Value == "Shop 1");
        chromeRule.Element("Applications")!.Value.Should().Be("pid=4242");
        chromeRule.Element("Action")!.Attribute("type")!.Value.Should().Be("Proxy");
    }

    [Fact]
    public void Build_ShouldSkipClosedChromeProfileInsteadOfFailing()
    {
        var proxy = CreateProxy();
        var closed = CreateChromeProfile(proxy, networkServicePid: null);

        var build = () => new ProxifierProfileBuilder().Build(new ProxifierProfileModel { Proxies = [proxy], Rules = [closed] });

        var result = build.Should().NotThrow().Subject;
        var document = XDocument.Parse(result.Xml);
        document.Descendants("Rule").Select(rule => rule.Element("Name")!.Value).Should().Equal("Localhost", "Default");
        document.Descendants("Proxy").Should().BeEmpty("a proxy used only by closed profiles is not needed in the profile");
    }

    [Fact]
    public void Build_ShouldPlaceLeakGuardAfterApplicationRulesAndBeforeDefault()
    {
        var proxy = CreateProxy();
        var appRule = new ApplicationRule { ExecutableName = "telegram.exe", AssignedProxyId = proxy.Id };
        var chromeRule = CreateChromeProfile(proxy, networkServicePid: 777);

        var rules = BuildRules(new ProxifierProfileModel
        {
            Proxies = [proxy],
            Rules = [appRule, chromeRule],
            BlockedExecutablePaths = [ToolChromePath, ToolChromePath.ToUpperInvariant()]
        });

        rules.Select(rule => rule.Element("Name")!.Value)
            .Should().Equal("Localhost", "telegram.exe", "Shop 1", "Chrome Profile leak guard", "Default");
        var guard = rules[3];
        guard.Element("Action")!.Attribute("type")!.Value.Should().Be("Block");
        guard.Element("Applications")!.Value.Should().Be(ToolChromePath);
    }

    [Fact]
    public void Build_ShouldQuoteLeakGuardPathContainingSpaces()
    {
        const string pathWithSpaces = @"C:\Users\Nguyen Van A\AppData\Roaming\ProxyManager\browser\1\chrome-win64\chrome.exe";

        var rules = BuildRules(new ProxifierProfileModel { Proxies = [], Rules = [], BlockedExecutablePaths = [pathWithSpaces] });

        rules.Single(rule => rule.Element("Name")!.Value == "Chrome Profile leak guard")
            .Element("Applications")!.Value.Should().Be($"\"{pathWithSpaces}\"");
    }

    [Fact]
    public void Build_ShouldNotAddLeakGuardWhenNoToolBrowserIsInstalled()
    {
        var rules = BuildRules(new ProxifierProfileModel { Proxies = [], Rules = [] });

        rules.Select(rule => rule.Element("Name")!.Value).Should().Equal("Localhost", "Default");
    }

    [Fact]
    public void Evaluate_ShouldAllowClosedChromeProfileWithValidProxy()
    {
        var proxy = CreateProxy();
        var closed = CreateChromeProfile(proxy, networkServicePid: null);

        ApplicationRuleEligibility.Evaluate(closed, [proxy]).CanUseProxy.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldRejectChromeProfileWithoutDataDirectory()
    {
        var proxy = CreateProxy();
        var rule = CreateChromeProfile(proxy, networkServicePid: null);
        rule.ChromeUserDataDirectory = null;

        ApplicationRuleEligibility.Evaluate(rule, [proxy]).CanUseProxy.Should().BeFalse();
    }

    [Fact]
    public void CanReceiveAutoAssignedProxy_ShouldExcludeChromeProfiles()
    {
        ApplicationRuleService.CanReceiveAutoAssignedProxy(CreateChromeProfile(CreateProxy(), 1)).Should().BeFalse();
    }

    [Fact]
    public async Task SaveApplicationRulesAsync_ShouldKeepChromeProfilesOnDisk()
    {
        var repository = new StoringApplicationRuleRepository();
        var chrome = CreateChromeProfile(CreateProxy(), null);
        var app = new ApplicationRule { ExecutableName = "zalo.exe" };
        await repository.SaveAllAsync([chrome, app], CancellationToken.None);
        var service = CreateService(repository);

        var applicationRules = await service.GetApplicationRulesAsync(CancellationToken.None);
        await service.SaveApplicationRulesAsync([], CancellationToken.None); // e.g. "Xóa tất cả" in tab Ứng dụng

        applicationRules.Should().ContainSingle().Which.Id.Should().Be(app.Id);
        repository.Rules.Should().ContainSingle().Which.Id.Should().Be(chrome.Id);
    }

    [Fact]
    public async Task SaveChromeProfilesAsync_ShouldKeepApplicationRulesOnDisk()
    {
        var repository = new StoringApplicationRuleRepository();
        var chrome = CreateChromeProfile(CreateProxy(), null);
        var app = new ApplicationRule { ExecutableName = "zalo.exe" };
        await repository.SaveAllAsync([chrome, app], CancellationToken.None);
        var service = CreateService(repository);
        var newProfile = CreateChromeProfile(CreateProxy(), null);

        await service.SaveChromeProfilesAsync([newProfile, app], CancellationToken.None);

        repository.Rules.Select(rule => rule.Id).Should().BeEquivalentTo([app.Id, newProfile.Id]);
        (await service.GetChromeProfilesAsync(CancellationToken.None)).Should().ContainSingle().Which.Id.Should().Be(newProfile.Id);
    }

    [Fact]
    public async Task SessionStart_ShouldPassInstalledToolBrowsersAsLeakGuard()
    {
        var builder = new CapturingProfileBuilder();
        var provisioner = new StubBrowserProvisioner([ToolChromePath]);
        var service = new ProxifierSessionService(builder, new NoOpProxifierService(), provisioner);
        var output = Path.Combine(Path.GetTempPath(), "netagent-chrome-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await service.StartAsync(
                new AppSettings { ProxifierExecutablePath = "Proxifier.exe", ProfileOutputDirectory = output },
                [],
                [],
                CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }

        builder.LastModel!.BlockedExecutablePaths.Should().Equal(ToolChromePath);
    }

    private static List<XElement> BuildRules(ProxifierProfileModel model) =>
        XDocument.Parse(new ProxifierProfileBuilder().Build(model).Xml).Descendants("Rule").ToList();

    private static ProxyServer CreateProxy() => new()
    {
        Proxy = "14.231.187.5:33275",
        Protocol = ProxyProtocol.Https,
        Username = "user",
        Password = "secret"
    };

    private static ApplicationRule CreateChromeProfile(ProxyServer proxy, int? networkServicePid) => new()
    {
        TargetType = ApplicationTargetType.ChromeProfile,
        ChromeProfileName = "Shop 1",
        ChromeUserDataDirectory = @"C:\Users\me\AppData\Roaming\ProxyManager\chrome-profiles\abc",
        AssignedProxyId = proxy.Id,
        ProcessId = networkServicePid,
        IsEnabled = true,
        AutoAssignProxy = false
    };

    private static ApplicationRuleService CreateService(IApplicationRuleRepository repository) =>
        new(repository, new RoundRobinProxyAssignmentService(), new NoEmulatorScanner());

    private sealed class StoringApplicationRuleRepository : IApplicationRuleRepository
    {
        public List<ApplicationRule> Rules { get; private set; } = [];

        public Task<IReadOnlyList<ApplicationRule>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApplicationRule>>(Rules.ToList());

        public Task SaveAllAsync(IReadOnlyCollection<ApplicationRule> rules, CancellationToken cancellationToken)
        {
            Rules = rules.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class NoEmulatorScanner : IEmulatorProcessScanner
    {
        public Task<IReadOnlyList<EmulatorProcessCandidate>> ScanAsync(
            IReadOnlyCollection<EmulatorKind> emulatorKinds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EmulatorProcessCandidate>>([]);
    }

    private sealed class CapturingProfileBuilder : IProxifierProfileBuilder
    {
        public ProxifierProfileModel? LastModel { get; private set; }

        public GeneratedProfileResult Build(ProxifierProfileModel model)
        {
            LastModel = model;
            return new GeneratedProfileResult("<xml />", "<xml />");
        }
    }

    private sealed class NoOpProxifierService : IProxifierService
    {
        public bool TryDetectProxifierPath(out string? path)
        {
            path = null;
            return false;
        }

        public Task<ProxifierCommandResult> LoadProfileAsync(string proxifierExePath, string profilePath, CancellationToken cancellationToken) =>
            Task.FromResult(new ProxifierCommandResult(true, "ok"));

        public Task<ProxifierCommandResult> HideWindowAsync(string proxifierExePath, CancellationToken cancellationToken) =>
            Task.FromResult(new ProxifierCommandResult(true, "ok"));

        public Task<ProxifierCommandResult> StopProcessesAsync(string proxifierExePath, CancellationToken cancellationToken) =>
            Task.FromResult(new ProxifierCommandResult(true, "ok"));

        public ProxifierWindowState GetWindowState(string proxifierExePath) => ProxifierWindowState.NotRunning;

        public ProxifierCommandResult ToggleWindowVisibility(string proxifierExePath) => new(true, "ok");
    }

    internal sealed class StubBrowserProvisioner(IReadOnlyList<string> installedPaths) : IChromeBrowserProvisioner
    {
        public bool IsInstalled => installedPaths.Count > 0;

        public string BrowserExecutablePath => installedPaths.FirstOrDefault() ?? string.Empty;

        public IReadOnlyList<string> GetInstalledExecutablePaths() => installedPaths;

        public Task<string> EnsureInstalledAsync(IProgress<ChromeBrowserDownloadProgress>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(BrowserExecutablePath);
    }
}
