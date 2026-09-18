using FluentAssertions;
using System.Xml.Linq;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProxifierProfileBuilderTests
{
    [Fact]
    public void Build_ShouldMaskPasswordInPreview()
    {
        var proxy = new ProxyServer
        {
            Proxy = "proxy.example.com:1080",
            Protocol = ProxyProtocol.Socks5,
            Username = "user",
            Password = "super-secret"
        };
        var rule = new ApplicationRule
        {
            ExecutableName = "chrome.exe",
            AssignedProxyId = proxy.Id
        };
        var builder = new ProxifierProfileBuilder();

        var result = builder.Build(new ProxifierProfileModel
        {
            Proxies = [proxy],
            Rules = [rule]
        });

        result.Xml.Should().Contain("super-secret");
        result.MaskedPreviewXml.Should().Contain("****");
        result.MaskedPreviewXml.Should().NotContain("super-secret");
    }

    [Fact]
    public void Build_ShouldGenerateProxifierV4CompatibleProfileShape()
    {
        var proxy = new ProxyServer
        {
            Proxy = "203.0.113.10:8080",
            Protocol = ProxyProtocol.Socks5,
            Username = "loc",
            Password = "secret"
        };
        var rule = new ApplicationRule
        {
            ExecutableName = "chrome.exe",
            AssignedProxyId = proxy.Id
        };
        var builder = new ProxifierProfileBuilder();

        var result = builder.Build(new ProxifierProfileModel
        {
            Proxies = [proxy],
            Rules = [rule]
        });

        result.Xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");

        var document = XDocument.Parse(result.Xml);
        var root = document.Root!;
        root.Name.LocalName.Should().Be("ProxifierProfile");
        root.Attribute("version")!.Value.Should().Be("102");
        root.Attribute("platform")!.Value.Should().Be("Windows");
        root.Attribute("product_id")!.Value.Should().Be("0");
        root.Attribute("product_minver")!.Value.Should().Be("400");

        root.Element("Options")!.Element("Encryption")!.Attribute("mode")!.Value.Should().Be("disabled");
        root.Element("ChainList").Should().NotBeNull();

        var generatedProxy = root.Element("ProxyList")!.Element("Proxy")!;
        generatedProxy.Attribute("id")!.Value.Should().Be("100");
        generatedProxy.Attribute("type")!.Value.Should().Be("SOCKS5");
        generatedProxy.Element("Protocol").Should().BeNull();
        generatedProxy.Element("Options")!.Value.Should().Be("48");
        generatedProxy.Element("Port")!.Value.Should().Be("8080");
        generatedProxy.Element("Address")!.Value.Should().Be("203.0.113.10");

        var rules = root.Element("RuleList")!.Elements("Rule").ToList();
        rules.Should().HaveCount(3);
        rules[0].Element("Name")!.Value.Should().Be("Localhost");
        rules[1].Element("Name")!.Value.Should().Be("chrome.exe");
        rules[1].Element("Action")!.Attribute("type")!.Value.Should().Be("Proxy");
        rules[1].Element("Action")!.Value.Should().Be("100");
        rules[2].Element("Name")!.Value.Should().Be("Default");
    }

    [Fact]
    public void Build_ShouldOnlyIncludeProxiesAssignedToEnabledRules()
    {
        var activeProxy = new ProxyServer
        {
            Proxy = "203.0.113.10:8080",
            Protocol = ProxyProtocol.Socks5
        };
        var disabledRuleProxy = new ProxyServer
        {
            Proxy = "203.0.113.11:8080",
            Protocol = ProxyProtocol.Socks5
        };
        var unusedProxy = new ProxyServer
        {
            Proxy = "203.0.113.12:8080",
            Protocol = ProxyProtocol.Socks5
        };
        var builder = new ProxifierProfileBuilder();

        var result = builder.Build(new ProxifierProfileModel
        {
            Proxies = [activeProxy, disabledRuleProxy, unusedProxy],
            Rules =
            [
                new ApplicationRule
                {
                    ExecutableName = "chrome.exe",
                    AssignedProxyId = activeProxy.Id,
                    IsEnabled = true
                },
                new ApplicationRule
                {
                    ExecutableName = "disabled.exe",
                    AssignedProxyId = disabledRuleProxy.Id,
                    IsEnabled = false
                }
            ]
        });

        var document = XDocument.Parse(result.Xml);
        var generatedProxies = document.Root!
            .Element("ProxyList")!
            .Elements("Proxy")
            .ToList();

        generatedProxies.Should().ContainSingle();
        generatedProxies[0].Element("Address")!.Value.Should().Be("203.0.113.10");
        result.Xml.Should().NotContain("203.0.113.11");
        result.Xml.Should().NotContain("203.0.113.12");
    }

    [Fact]
    public void Build_ShouldEnableHttpProxySupportWhenHttpProxyExists()
    {
        var proxy = new ProxyServer
        {
            Proxy = "203.0.113.11:3128",
            Protocol = ProxyProtocol.Https
        };
        var rule = new ApplicationRule
        {
            ExecutableName = "browser.exe",
            AssignedProxyId = proxy.Id
        };
        var builder = new ProxifierProfileBuilder();

        var result = builder.Build(new ProxifierProfileModel
        {
            Proxies = [proxy],
            Rules = [rule]
        });

        var document = XDocument.Parse(result.Xml);
        document.Root!
            .Element("ProxyList")!
            .Element("Proxy")!
            .Attribute("type")!
            .Value
            .Should()
            .Be("HTTPS");

        document.Root!
            .Element("Options")!
            .Element("HttpProxiesSupport")!
            .Attribute("enabled")!
            .Value
            .Should()
            .Be("true");
    }

    [Fact]
    public void Build_ShouldQuoteApplicationPathWhenItContainsSpaces()
    {
        var proxy = new ProxyServer
        {
            Proxy = "203.0.113.12:1080",
            Protocol = ProxyProtocol.Socks5
        };
        var rule = new ApplicationRule
        {
            ExecutableName = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            AssignedProxyId = proxy.Id
        };
        var builder = new ProxifierProfileBuilder();

        var result = builder.Build(new ProxifierProfileModel
        {
            Proxies = [proxy],
            Rules = [rule]
        });

        var document = XDocument.Parse(result.Xml);
        document.Root!
            .Element("RuleList")!
            .Elements("Rule")
            .Single(item => item.Element("Applications") is not null)
            .Element("Applications")!
            .Value
            .Should()
            .Be("\"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe\"");
    }

    [Fact]
    public void Build_ShouldGeneratePidSelectorForEmulatorRule()
    {
        var proxy = new ProxyServer
        {
            Proxy = "203.0.113.13:1080",
            Protocol = ProxyProtocol.Socks5
        };
        var rule = new ApplicationRule
        {
            TargetType = ApplicationTargetType.Emulator,
            EmulatorKind = EmulatorKind.LDPlayer,
            EmulatorInstanceKey = "leidian0",
            EmulatorInstanceName = "LDPlayer 1",
            RuntimeProcessName = "LdVBoxHeadless.exe",
            ProcessId = 1234,
            AssignedProxyId = proxy.Id
        };
        var builder = new ProxifierProfileBuilder();

        var result = builder.Build(new ProxifierProfileModel
        {
            Proxies = [proxy],
            Rules = [rule]
        });

        var document = XDocument.Parse(result.Xml);
        var generatedRule = document.Root!
            .Element("RuleList")!
            .Elements("Rule")
            .Single(item => item.Element("Applications") is not null);

        generatedRule.Element("Name")!.Value.Should().Be("LDPlayer 1");
        generatedRule.Element("Applications")!.Value.Should().Be("pid=1234");
    }

    [Fact]
    public void Build_ShouldRejectEnabledEmulatorRuleWithoutPid()
    {
        var proxy = new ProxyServer
        {
            Proxy = "203.0.113.14:1080",
            Protocol = ProxyProtocol.Socks5
        };
        var builder = new ProxifierProfileBuilder();

        var act = () => builder.Build(new ProxifierProfileModel
        {
            Proxies = [proxy],
            Rules =
            [
                new ApplicationRule
                {
                    TargetType = ApplicationTargetType.Emulator,
                    EmulatorKind = EmulatorKind.Nox,
                    EmulatorInstanceKey = "nox",
                    EmulatorInstanceName = "Nox 1",
                    AssignedProxyId = proxy.Id
                }
            ]
        });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PID runtime*");
    }

    [Fact]
    public void Build_ShouldRejectEnabledRulesWithoutAssignedProxy()
    {
        var builder = new ProxifierProfileBuilder();

        var act = () => builder.Build(new ProxifierProfileModel
        {
            Proxies = [],
            Rules = [new ApplicationRule { ExecutableName = "chrome.exe" }]
        });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Build_ShouldGenerateDirectOnlyProfileWhenNoApplicationsAreProvided()
    {
        var builder = new ProxifierProfileBuilder();

        var result = builder.Build(new ProxifierProfileModel
        {
            Proxies = [],
            Rules = [],
            DefaultRouteDirect = true
        });

        var document = XDocument.Parse(result.Xml);
        var rules = document.Root!.Element("RuleList")!.Elements("Rule").ToList();
        rules.Should().HaveCount(2);
        rules[0].Element("Name")!.Value.Should().Be("Localhost");
        rules[1].Element("Name")!.Value.Should().Be("Default");
        rules[1].Element("Action")!.Attribute("type")!.Value.Should().Be("Direct");
    }
}
