using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class BulkProxyParserTests
{
    [Fact]
    public void Parse_ShouldSupportAllDocumentedFormats()
    {
        var parser = new BulkProxyParser();
        var input = """
                    192.0.2.10:443:HTTP
                    192.0.2.11:1080:SOCKS5
                    192.0.2.12:443:user01:pass01:HTTP
                    192.0.2.13:1080:user02:pass02:SOCKS5
                    """;

        var result = parser.Parse(input);

        result.Errors.Should().BeEmpty();
        result.Proxies.Should().HaveCount(4);
        result.Proxies[0].Protocol.Should().Be(ProxyProtocol.Https);
        result.Proxies[1].Protocol.Should().Be(ProxyProtocol.Socks5);
        result.Proxies[2].Username.Should().Be("user01");
        result.Proxies[3].Password.Should().Be("pass02");
        result.Proxies[3].Proxy.Should().Be("192.0.2.13:1080");
    }

    [Fact]
    public void Parse_ShouldReturnLineLevelErrors()
    {
        var parser = new BulkProxyParser();

        var result = parser.Parse("badline\nhost:99999:SOCKS5\nhost:1080:HTTPX\n");

        result.Errors.Should().HaveCount(3);
        result.Errors[0].LineNumber.Should().Be(1);
        result.Errors[1].LineNumber.Should().Be(2);
        result.Errors[2].LineNumber.Should().Be(3);
    }

    [Fact]
    public void Parse_ShouldAcceptLegacyHttpsTokenAsHttpProxyProtocol()
    {
        var parser = new BulkProxyParser();

        var result = parser.Parse("192.0.2.20:443:HTTPS");

        result.Errors.Should().BeEmpty();
        result.Proxies.Should().ContainSingle();
        result.Proxies[0].Protocol.Should().Be(ProxyProtocol.Https);
        result.Proxies[0].DisplayValue.Should().Be("192.0.2.20:443:http");
    }

    [Fact]
    public void Parse_ShouldDefaultMissingProtocolToSocks5()
    {
        var parser = new BulkProxyParser();
        var input = """
                    192.0.2.30:1080
                    192.0.2.31:1080:user03:pass03
                    """;

        var result = parser.Parse(input);

        result.Errors.Should().BeEmpty();
        result.Proxies.Should().HaveCount(2);
        result.Proxies.Should().OnlyContain(proxy => proxy.Protocol == ProxyProtocol.Socks5);
        result.Proxies[1].Username.Should().Be("user03");
        result.Proxies[1].Password.Should().Be("pass03");
    }

    [Fact]
    public void Parse_ShouldSupportUriFormats()
    {
        var parser = new BulkProxyParser();
        var input = """
                    http://192.0.2.40:8080
                    socks5://user04:pass04@192.0.2.41:1080
                    """;

        var result = parser.Parse(input);

        result.Errors.Should().BeEmpty();
        result.Proxies.Should().HaveCount(2);
        result.Proxies[0].Protocol.Should().Be(ProxyProtocol.Https);
        result.Proxies[0].Proxy.Should().Be("192.0.2.40:8080");
        result.Proxies[1].Protocol.Should().Be(ProxyProtocol.Socks5);
        result.Proxies[1].Username.Should().Be("user04");
        result.Proxies[1].Password.Should().Be("pass04");
    }

    [Fact]
    public void Parse_ShouldReturnErrorsForMalformedUriAndUnsupportedProtocol()
    {
        var parser = new BulkProxyParser();

        var result = parser.Parse("ftp://192.0.2.50:21\nhttp://192.0.2.51\n");

        result.Errors.Should().HaveCount(2);
        result.Errors[0].LineNumber.Should().Be(1);
        result.Errors[1].LineNumber.Should().Be(2);
    }
}
