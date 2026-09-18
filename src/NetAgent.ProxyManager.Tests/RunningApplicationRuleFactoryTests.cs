using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class RunningApplicationRuleFactoryTests
{
    [Fact]
    public void BuildCandidates_ShouldPreferRecognizedEmulatorOverProcessWithSamePid()
    {
        var emulator = new EmulatorProcessCandidate(
            ProcessId: 100,
            ParentProcessId: null,
            EmulatorKind: EmulatorKind.BlueStacks,
            EmulatorInstanceKey: "Pie64_1",
            EmulatorInstanceName: "BlueStacks 1",
            RuntimeType: "Runtime",
            ProcessName: "HD-Player.exe",
            ExecutablePath: @"C:\BlueStacks\HD-Player.exe",
            CommandLine: "--instance Pie64_1",
            MainWindowTitle: "BlueStacks 1");
        var processes = new[]
        {
            new RunningProcessInfo(100, "HD-Player.exe", "HD-Player", "BlueStacks 1", @"C:\BlueStacks\HD-Player.exe")
        };

        var candidates = RunningApplicationRuleFactory.BuildCandidates([emulator], processes);

        candidates.Should().ContainSingle();
        candidates[0].IsEmulator.Should().BeTrue();
        candidates[0].DisplayName.Should().Be("BlueStacks 1");
    }

    [Fact]
    public void BuildCandidates_ShouldExcludeKnownLauncherProcessWhenSameKindEmulatorDetected()
    {
        var emulator = new EmulatorProcessCandidate(
            ProcessId: 100,
            ParentProcessId: null,
            EmulatorKind: EmulatorKind.LDPlayer,
            EmulatorInstanceKey: "leidian0",
            EmulatorInstanceName: "LDPlayer-1",
            RuntimeType: "Headless",
            ProcessName: "Ld9BoxHeadless.exe",
            ExecutablePath: @"C:\Program Files\ldplayer9box\Ld9BoxHeadless.exe",
            CommandLine: "--comment leidian0",
            MainWindowTitle: null);
        var processes = new[]
        {
            new RunningProcessInfo(
                101,
                "dnplayer.exe",
                "dnplayer",
                MainWindowTitle: null,
                ExecutablePath: @"D:\setup\LDPlayer\LDPlayer9\dnplayer.exe")
        };

        var candidates = RunningApplicationRuleFactory.BuildCandidates([emulator], processes);

        candidates.Should().ContainSingle();
        candidates[0].IsEmulator.Should().BeTrue();
    }

    [Fact]
    public void BuildCandidates_ShouldKeepKnownLauncherProcessAsFallbackWhenNoSameKindEmulatorDetected()
    {
        var processes = new[]
        {
            new RunningProcessInfo(
                101,
                "dnplayer.exe",
                "dnplayer",
                MainWindowTitle: null,
                ExecutablePath: @"D:\setup\LDPlayer\LDPlayer9\dnplayer.exe")
        };

        var candidates = RunningApplicationRuleFactory.BuildCandidates([], processes);

        candidates.Should().ContainSingle();
        candidates[0].IsEmulator.Should().BeFalse();
    }

    [Fact]
    public void CreateRules_ShouldCreatePidRuleForRecognizedEmulator()
    {
        var emulator = new EmulatorProcessCandidate(
            ProcessId: 100,
            ParentProcessId: null,
            EmulatorKind: EmulatorKind.BlueStacks,
            EmulatorInstanceKey: "Pie64_1",
            EmulatorInstanceName: "BlueStacks 1",
            RuntimeType: "Runtime",
            ProcessName: "HD-Player.exe",
            ExecutablePath: @"C:\BlueStacks\HD-Player.exe",
            CommandLine: "--instance Pie64_1",
            MainWindowTitle: "BlueStacks 1");
        var candidate = RunningApplicationRuleFactory.BuildCandidates([emulator], []).Single();

        var result = RunningApplicationRuleFactory.CreateRules([candidate]);

        result.SkippedCount.Should().Be(0);
        result.Rules.Should().ContainSingle();
        result.Rules[0].TargetType.Should().Be(ApplicationTargetType.Emulator);
        result.Rules[0].ProcessId.Should().Be(100);
        result.Rules[0].EmulatorInstanceName.Should().Be("BlueStacks 1");
    }

    [Fact]
    public void CreateRules_ShouldUseExecutablePathForNetworkProcess()
    {
        var process = new RunningProcessInfo(
            200,
            "tool.exe",
            "tool",
            MainWindowTitle: null,
            ExecutablePath: @"C:\Tools\tool.exe",
            HasNetworkActivity: true);
        var candidate = RunningApplicationRuleFactory.BuildCandidates([], [process]).Single();

        var result = RunningApplicationRuleFactory.CreateRules([candidate]);

        result.Rules.Should().ContainSingle();
        result.Rules[0].TargetType.Should().Be(ApplicationTargetType.Executable);
        result.Rules[0].ExecutableName.Should().Be(@"C:\Tools\tool.exe");
    }

    [Fact]
    public void BuildCandidates_ShouldGroupProcessesByExecutablePath()
    {
        var first = new RunningProcessInfo(
            200,
            "chrome.exe",
            "chrome",
            "Google Chrome",
            @"C:\Chrome\chrome.exe",
            HasNetworkActivity: true);
        var second = new RunningProcessInfo(
            201,
            "chrome.exe",
            "chrome",
            MainWindowTitle: null,
            ExecutablePath: @"C:\Chrome\chrome.exe",
            HasNetworkActivity: true);

        var candidates = RunningApplicationRuleFactory.BuildCandidates([], [first, second]);

        candidates.Should().ContainSingle();
        candidates[0].DisplayName.Should().Be("Google Chrome (2)");
        candidates[0].ProcessCount.Should().Be(2);
        candidates[0].TargetExecutablePaths.Should().ContainSingle().Which.Should().Be(@"C:\Chrome\chrome.exe");
    }

    [Fact]
    public void BuildCandidates_ShouldKeepRecognizedEmulatorInstancesSeparateEvenWhenExecutablePathMatches()
    {
        var first = new EmulatorProcessCandidate(
            ProcessId: 100,
            ParentProcessId: null,
            EmulatorKind: EmulatorKind.BlueStacks,
            EmulatorInstanceKey: "Pie64_1",
            EmulatorInstanceName: "BlueStacks 1",
            RuntimeType: "Runtime",
            ProcessName: "HD-Player.exe",
            ExecutablePath: @"C:\BlueStacks\HD-Player.exe",
            CommandLine: "--instance Pie64_1",
            MainWindowTitle: "BlueStacks 1");
        var second = first with
        {
            ProcessId = 101,
            EmulatorInstanceKey = "Pie64_2",
            EmulatorInstanceName = "BlueStacks 2",
            CommandLine = "--instance Pie64_2",
            MainWindowTitle = "BlueStacks 2"
        };

        var candidates = RunningApplicationRuleFactory.BuildCandidates([first, second], []);

        candidates.Should().HaveCount(2);
        candidates.Should().OnlyContain(candidate => candidate.IsEmulator);
    }

    [Fact]
    public void BuildCandidates_ShouldExcludeProcessesWithoutExecutablePath()
    {
        var first = new RunningProcessInfo(
            200,
            "svchost.exe",
            "svchost",
            MainWindowTitle: null,
            ExecutablePath: null,
            HasNetworkActivity: false);
        var second = new RunningProcessInfo(
            201,
            "svchost.exe",
            "svchost",
            MainWindowTitle: null,
            ExecutablePath: null,
            HasNetworkActivity: false);

        var candidates = RunningApplicationRuleFactory.BuildCandidates([], [first, second]);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public void CreateRules_ShouldSkipCandidateWithoutExecutablePath()
    {
        var first = new RunningProcessInfo(
            200,
            "WUDFHost.exe",
            "WUDFHost",
            MainWindowTitle: null,
            ExecutablePath: null,
            HasNetworkActivity: false);
        var candidate = new RunningApplicationCandidate(
            "process:pid:200",
            "WUDFHost.exe",
            first,
            Emulator: null,
            []);

        var result = RunningApplicationRuleFactory.CreateRules([candidate]);

        result.Rules.Should().BeEmpty();
        result.SkippedCount.Should().Be(1);
    }

    [Fact]
    public void CreateRules_ShouldUseSelectedProcessAndSameDirectoryChildNetworkProcessWhenSelectedProcessHasNoNetwork()
    {
        var uiProcess = new RunningProcessInfo(
            200,
            "tool-ui.exe",
            "tool-ui",
            "Tool UI",
            @"C:\Tools\tool-ui.exe",
            ParentProcessId: null,
            HasNetworkActivity: false);
        var workerProcess = new RunningProcessInfo(
            201,
            "tool-worker.exe",
            "tool-worker",
            MainWindowTitle: null,
            ExecutablePath: @"C:\Tools\tool-worker.exe",
            ParentProcessId: 200,
            HasNetworkActivity: true);
        var candidate = RunningApplicationRuleFactory.BuildCandidates([], [uiProcess, workerProcess])
            .Single(item => item.Process?.ProcessId == 200);

        var result = RunningApplicationRuleFactory.CreateRules([candidate]);

        result.Rules.Should().HaveCount(2);
        result.Rules.Select(rule => rule.ExecutableName).Should().Equal(
            @"C:\Tools\tool-ui.exe",
            @"C:\Tools\tool-worker.exe");
    }

    [Fact]
    public void CreateRules_ShouldNotUseNetworkParentProcessAsSelectedProcessTarget()
    {
        var explorer = new RunningProcessInfo(
            100,
            "explorer.exe",
            "explorer",
            MainWindowTitle: null,
            ExecutablePath: @"C:\Windows\explorer.exe",
            HasNetworkActivity: true);
        var selectedProcess = new RunningProcessInfo(
            200,
            "notepad++.exe",
            "notepad++",
            "Notepad++",
            @"C:\Tools\Notepad++\notepad++.exe",
            ParentProcessId: 100,
            HasNetworkActivity: false);
        var candidate = RunningApplicationRuleFactory
            .BuildCandidates([], [explorer, selectedProcess])
            .Single(item => item.Process?.ProcessId == 200);

        var result = RunningApplicationRuleFactory.CreateRules([candidate]);

        result.Rules.Should().ContainSingle();
        result.Rules[0].ExecutableName.Should().Be(@"C:\Tools\Notepad++\notepad++.exe");
    }

    [Fact]
    public void CreateRules_ShouldNotUseNetworkAncestorProcessAsSelectedProcessTarget()
    {
        var launcher = new RunningProcessInfo(
            100,
            "launcher.exe",
            "launcher",
            MainWindowTitle: null,
            ExecutablePath: @"C:\Tools\Launcher\launcher.exe",
            HasNetworkActivity: true);
        var parentProcess = new RunningProcessInfo(
            150,
            "parent.exe",
            "parent",
            MainWindowTitle: null,
            ExecutablePath: @"C:\Tools\App\parent.exe",
            ParentProcessId: 100,
            HasNetworkActivity: false);
        var selectedProcess = new RunningProcessInfo(
            200,
            "app.exe",
            "app",
            "App",
            @"C:\Tools\App\app.exe",
            ParentProcessId: 150,
            HasNetworkActivity: false);
        var candidate = RunningApplicationRuleFactory
            .BuildCandidates([], [launcher, parentProcess, selectedProcess])
            .Single(item => item.Process?.ProcessId == 200);

        var result = RunningApplicationRuleFactory.CreateRules([candidate]);

        result.Rules.Should().ContainSingle();
        result.Rules[0].ExecutableName.Should().Be(@"C:\Tools\App\app.exe");
    }

    [Fact]
    public void CreateRules_ShouldNotUseSiblingNetworkProcessWithSameParent()
    {
        var launcher = new RunningProcessInfo(
            100,
            "explorer.exe",
            "explorer",
            MainWindowTitle: null,
            ExecutablePath: @"C:\Windows\explorer.exe");
        var selectedProcess = new RunningProcessInfo(
            200,
            "notepad++.exe",
            "notepad++",
            "Notepad++",
            @"C:\Tools\Notepad++\notepad++.exe",
            ParentProcessId: 100,
            HasNetworkActivity: false);
        var siblingNetworkProcess = new RunningProcessInfo(
            201,
            "chrome.exe",
            "chrome",
            "Google Chrome",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            ParentProcessId: 100,
            HasNetworkActivity: true);
        var candidate = RunningApplicationRuleFactory
            .BuildCandidates([], [launcher, selectedProcess, siblingNetworkProcess])
            .Single(item => item.Process?.ProcessId == 200);

        var result = RunningApplicationRuleFactory.CreateRules([candidate]);

        result.Rules.Should().ContainSingle();
        result.Rules[0].ExecutableName.Should().Be(@"C:\Tools\Notepad++\notepad++.exe");
    }

    [Fact]
    public void CreateRules_ShouldSkipProcessWithoutExecutableTarget()
    {
        var process = new RunningProcessInfo(
            200,
            "system",
            "system",
            MainWindowTitle: null,
            ExecutablePath: null,
            HasNetworkActivity: false);
        var candidate = new RunningApplicationCandidate(
            "process:pid:200",
            "system",
            process,
            Emulator: null,
            []);

        var result = RunningApplicationRuleFactory.CreateRules([candidate]);

        result.Rules.Should().BeEmpty();
        result.SkippedCount.Should().Be(1);
    }
}
