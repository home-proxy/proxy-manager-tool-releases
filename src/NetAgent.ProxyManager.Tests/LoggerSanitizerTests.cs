using FluentAssertions;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class LoggerSanitizerTests
{
    [Theory]
    [InlineData("apiKey=pk_live_12345", "apiKey=****")]
    [InlineData("Authorization: Bearer abc.def.ghi", "Authorization: Bearer ****")]
    [InlineData("{\"token\":\"access.jwt.value\",\"refreshToken\":\"refresh.jwt.value\"}", "\"token\":\"****\"")]
    [InlineData("{\"secretKey\":\"merchant-secret\"}", "\"secretKey\":\"****\"")]
    [InlineData("socks5://user:pass@host.example.com:1080", "socks5://user:****@host.example.com:1080")]
    [InlineData("user:pass@host.example.com:1080", "user:****@host.example.com:1080")]
    public void Sanitize_ShouldMaskSecrets(string input, string expectedFragment)
    {
        var sanitizer = new LoggerSanitizer();

        var result = sanitizer.Sanitize(input);

        result.Should().Contain(expectedFragment);
        result.Should().NotContain("pass");
        result.Should().NotContain("pk_live_12345");
        result.Should().NotContain("abc.def.ghi");
    }
}
