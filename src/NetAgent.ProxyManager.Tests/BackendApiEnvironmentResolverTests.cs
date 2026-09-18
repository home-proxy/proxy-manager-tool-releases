using FluentAssertions;
using NetAgent.ProxyManager.Infrastructure.Api;

namespace NetAgent.ProxyManager.Tests;

public sealed class BackendApiEnvironmentResolverTests
{
    [Theory]
    [InlineData("dev", BackendApiEnvironmentResolver.Development)]
    [InlineData("Development", BackendApiEnvironmentResolver.Development)]
    [InlineData("prod", BackendApiEnvironmentResolver.Production)]
    [InlineData("Production", BackendApiEnvironmentResolver.Production)]
    [InlineData(null, BackendApiEnvironmentResolver.Production)]
    public void NormalizeEnvironmentName_ShouldResolveKnownEnvironmentNames(string? value, string expected)
    {
        BackendApiEnvironmentResolver.NormalizeEnvironmentName(value).Should().Be(expected);
    }

    [Fact]
    public void ResolveEnvironmentName_ShouldPreferNetAgentEnvironment()
    {
        var result = BackendApiEnvironmentResolver.ResolveEnvironmentName(
            appSettingsEnvironment: BackendApiEnvironmentResolver.Production,
            netAgentEnvironment: BackendApiEnvironmentResolver.Development);

        result.Should().Be(BackendApiEnvironmentResolver.Development);
    }
}
