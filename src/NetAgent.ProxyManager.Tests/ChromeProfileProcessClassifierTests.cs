using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ChromeProfileProcessClassifierTests
{
    private const string ToolChrome = @"C:\Users\me\AppData\Roaming\ProxyManager\browser\155\chrome-win64\chrome.exe";
    private const string SystemChrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    private const string ProfileA = @"C:\Users\me\AppData\Roaming\ProxyManager\chrome-profiles\aaa";
    private const string ProfileB = @"C:\Users\me\AppData\Roaming\ProxyManager\chrome-profiles\b b";

    [Fact]
    public void Classify_ShouldGroupProcessesByUserDataDirAndFindNetworkService()
    {
        var processes = new[]
        {
            Process(10, ToolChrome, $"\"{ToolChrome}\" --user-data-dir={ProfileA} --no-first-run"),
            Process(11, ToolChrome, $"\"{ToolChrome}\" --type=renderer --user-data-dir=\"{ProfileA}\""),
            Process(12, ToolChrome, $"\"{ToolChrome}\" --type=utility --utility-sub-type=network.mojom.NetworkService --user-data-dir=\"{ProfileA}\""),
            Process(20, ToolChrome, $"\"{ToolChrome}\" \"--user-data-dir={ProfileB}\""),
            Process(21, ToolChrome, $"\"{ToolChrome}\" --type=gpu-process --user-data-dir=\"{ProfileB}\"")
        };

        var result = ChromeProfileProcessClassifier.Classify(processes, [ToolChrome]);

        result.Should().BeEquivalentTo(new[]
        {
            new ChromeProfileRuntimeInfo(ProfileA, 10, 12),
            new ChromeProfileRuntimeInfo(ProfileB, 20, null)
        });
    }

    [Fact]
    public void Classify_ShouldIgnoreTheUsersOwnChrome()
    {
        var processes = new[]
        {
            Process(30, SystemChrome, $"\"{SystemChrome}\""),
            Process(31, SystemChrome, $"\"{SystemChrome}\" --type=utility --utility-sub-type=network.mojom.NetworkService"),
            // Even with a --user-data-dir that looks like ours, a different executable is never ours.
            Process(32, SystemChrome, $"\"{SystemChrome}\" --user-data-dir={ProfileA}")
        };

        ChromeProfileProcessClassifier.Classify(processes, [ToolChrome]).Should().BeEmpty();
    }

    [Fact]
    public void Classify_ShouldMatchExecutablePathCaseInsensitively()
    {
        var processes = new[] { Process(40, ToolChrome.ToUpperInvariant(), $"chrome.exe --user-data-dir={ProfileA}") };

        ChromeProfileProcessClassifier.Classify(processes, [ToolChrome]).Should().ContainSingle()
            .Which.BrowserProcessId.Should().Be(40);
    }

    [Fact]
    public void Classify_ShouldDropInstancesWhoseBrowserProcessIsGone()
    {
        var processes = new[]
        {
            Process(50, ToolChrome, $"chrome.exe --type=utility --utility-sub-type=network.mojom.NetworkService --user-data-dir={ProfileA}")
        };

        ChromeProfileProcessClassifier.Classify(processes, [ToolChrome]).Should().BeEmpty();
    }

    [Theory]
    [InlineData("chrome.exe --user-data-dir=C:\\data\\p1 --x", "C:\\data\\p1")]
    [InlineData("chrome.exe --user-data-dir=\"C:\\data\\p 2\" --x", "C:\\data\\p 2")]
    [InlineData("chrome.exe \"--user-data-dir=C:\\data\\p 3\" --x", "C:\\data\\p 3")]
    [InlineData("chrome.exe --no-first-run", null)]
    public void TryGetUserDataDirectory_ShouldParseCommonQuotingForms(string commandLine, string? expected)
    {
        ChromeProfileProcessClassifier.TryGetUserDataDirectory(commandLine).Should().Be(expected);
    }

    [Fact]
    public void IsSameDirectory_ShouldIgnoreCaseAndTrailingSeparator()
    {
        ChromeProfileProcessClassifier.IsSameDirectory(ProfileA + "\\", ProfileA.ToUpperInvariant()).Should().BeTrue();
        ChromeProfileProcessClassifier.IsSameDirectory(ProfileA, ProfileB).Should().BeFalse();
    }

    private static RunningProcessInfo Process(int pid, string executablePath, string commandLine) =>
        new(pid, "chrome.exe", "chrome.exe", null, executablePath, null, commandLine);
}
