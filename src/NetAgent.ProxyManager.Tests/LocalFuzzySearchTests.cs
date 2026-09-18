using FluentAssertions;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class LocalFuzzySearchTests
{
    [Theory]
    [InlineData("Ứng dụng đang chạy", "ung dung")]
    [InlineData("Google Chrome", "chrome")]
    [InlineData("BlueStacks Multi Instance Manager", "blue multi")]
    [InlineData(@"C:\Tools\AccountCreator.exe", "account creator")]
    public void IsMatch_ShouldMatchLocalSearchFriendlyQueries(string candidate, string query)
    {
        LocalFuzzySearch.IsMatch(candidate, query).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_ShouldRejectUnrelatedQuery()
    {
        LocalFuzzySearch.IsMatch("Google Chrome", "notepad").Should().BeFalse();
    }

    [Fact]
    public void IsMatch_ShouldRejectTypoThatIsNotContained()
    {
        LocalFuzzySearch.IsMatch("Google Chrome", "gogle").Should().BeFalse();
    }
}
