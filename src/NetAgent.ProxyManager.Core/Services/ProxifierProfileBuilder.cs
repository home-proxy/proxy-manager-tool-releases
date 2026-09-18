using System.Text;
using System.Xml;
using System.Xml.Linq;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ProxifierProfileBuilder : IProxifierProfileBuilder
{
    public GeneratedProfileResult Build(ProxifierProfileModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var enabledRules = model.Rules.Where(rule => rule.IsEnabled).ToList();
        var missingExecutableTargets = enabledRules
            .Where(rule => rule.TargetType == ApplicationTargetType.Executable && string.IsNullOrWhiteSpace(rule.ExecutableName))
            .ToList();
        if (missingExecutableTargets.Count > 0)
        {
            throw new InvalidOperationException("Các ứng dụng thông thường đang bật chưa có executable hợp lệ.");
        }

        var proxiesById = model.Proxies.ToDictionary(proxy => proxy.Id);
        var missingAssignments = enabledRules
            .Where(rule => rule.AssignedProxyId is null || !proxiesById.ContainsKey(rule.AssignedProxyId.Value))
            .ToList();

        if (missingAssignments.Count > 0)
        {
            var names = string.Join(", ", missingAssignments.Select(GetRuleName));
            throw new InvalidOperationException($"Các ứng dụng đang bật chưa có proxy hợp lệ: {names}");
        }

        var missingRuntimeTargets = enabledRules
            .Where(rule => rule.TargetType == ApplicationTargetType.Emulator && rule.ProcessId is null or <= 0)
            .ToList();
        if (missingRuntimeTargets.Count > 0)
        {
            var names = string.Join(", ", missingRuntimeTargets.Select(GetRuleName));
            throw new InvalidOperationException($"Các giả lập đang bật chưa có PID runtime hợp lệ. Hãy mở giả lập rồi quét lại: {names}");
        }

        var enabledAssignedProxyIds = enabledRules
            .Select(rule => rule.AssignedProxyId!.Value)
            .ToHashSet();
        var profileProxies = model.Proxies
            .Where(proxy => enabledAssignedProxyIds.Contains(proxy.Id))
            .DistinctBy(proxy => proxy.Id)
            .ToList();

        // The schema is intentionally isolated in this builder because Proxifier profile XML differs
        // across versions. This shape mirrors profiles exported by Proxifier v4 for Windows.
        var proxyIdMap = profileProxies
            .Select((proxy, index) => new { proxy.Id, ProfileId = index + 100 })
            .ToDictionary(item => item.Id, item => item.ProfileId);

        var profile = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("ProxifierProfile",
                new XAttribute("version", "102"),
                new XAttribute("platform", "Windows"),
                new XAttribute("product_id", "0"),
                new XAttribute("product_minver", "400"),
                CreateOptionsElement(profileProxies),
                new XElement("ProxyList",
                    profileProxies.Select(proxy => CreateProxyElement(proxy, proxyIdMap[proxy.Id]))),
                new XElement("ChainList"),
                new XElement("RuleList",
                    CreateLocalhostRule(),
                    enabledRules.Select(rule =>
                    {
                        var assignedProxyId = proxyIdMap[rule.AssignedProxyId!.Value];
                        return new XElement("Rule",
                            new XAttribute("enabled", "true"),
                            new XElement("Action",
                                new XAttribute("type", "Proxy"),
                                assignedProxyId),
                            new XElement("Applications", FormatApplicationValue(GetApplicationSelector(rule))),
                            new XElement("Name", GetRuleName(rule)));
                    }),
                    new XElement("Rule",
                        new XAttribute("enabled", "true"),
                        new XElement("Action", new XAttribute("type", model.DefaultRouteDirect ? "Direct" : "Block")),
                        new XElement("Name", "Default")))));

        var xml = Serialize(profile);
        var maskedPreview = Serialize(MaskPasswords(profile));
        return new GeneratedProfileResult(xml, maskedPreview);
    }

    private static XElement CreateOptionsElement(IReadOnlyList<ProxyServer> proxies)
    {
        var hasHttpProxy = proxies.Any(proxy => proxy.Protocol == ProxyProtocol.Https);

        return new XElement("Options",
            new XElement("Resolve",
                new XElement("AutoModeDetection", new XAttribute("enabled", "true")),
                new XElement("ViaProxy", new XAttribute("enabled", "false")),
                new XElement("BlockNonATypes", new XAttribute("enabled", "false")),
                new XElement(
                    "ExclusionList",
                    new XAttribute("OnlyFromListMode", "false"),
                    "%ComputerName%; localhost; *.local"),
                new XElement("DnsUdpMode", "0")),
            // The app currently stores raw credentials. "disabled" keeps the generated XML honest
            // and lets Proxifier read the text as-is instead of expecting Proxifier-encrypted blobs.
            new XElement("Encryption", new XAttribute("mode", "disabled")),
            // The whole point of this app is that end users never see Proxifier's own UI
            // ("ẩn cửa sổ Proxifier đi" — see docs/05-proxifier-integration.md). Loop detection
            // pops a native Proxifier dialog (and re-shows its main window to display it) whenever
            // an app retries a blocked connection quickly — which LeakPreventionMode now makes
            // routine for chatty apps like Chrome/Edge hammering blocked QUIC endpoints. A customer
            // seeing an unexplained "Infinite Connection Loop" dialog from software they don't know
            // exists — and a wrong click there ("Apply suggested changes automatically") silently
            // routes that app Direct, undoing LeakPreventionMode without ProxyManager ever knowing —
            // is worse than losing this diagnostic, so it stays off.
            new XElement("ConnectionLoopDetection",
                new XAttribute("enabled", "false"),
                new XAttribute("resolve", "true")),
            // HTTP/HTTPS proxies cannot tunnel UDP, so UDP traffic (e.g. Chrome's QUIC/HTTP3,
            // which it prefers by default) must bypass the proxy at the transport level.
            // Without LeakPreventionMode, that bypassed traffic goes out over the real
            // connection instead of being blocked, silently exposing the user's real IP
            // behind what looks like an active, correctly-assigned proxy rule. Proxifier 4.11+
            // ships LeakPreventionMode as the documented fix for exactly this failure mode
            // (Profile -> Advanced -> "DNS and IP Leak Prevention Mode" in the Proxifier UI) —
            // enabling it makes Proxifier block traffic it cannot proxy instead of leaking it.
            new XElement("Udp", new XAttribute("mode", "mode_bypass")),
            new XElement("LeakPreventionMode", new XAttribute("enabled", "true")),
            new XElement("ProcessOtherUsers", new XAttribute("enabled", "false")),
            new XElement("ProcessServices", new XAttribute("enabled", "false")),
            new XElement("HandleDirectConnections", new XAttribute("enabled", "false")),
            new XElement("HttpProxiesSupport", new XAttribute("enabled", hasHttpProxy ? "true" : "false")));
    }

    private static XElement CreateLocalhostRule()
    {
        return new XElement("Rule",
            new XAttribute("enabled", "true"),
            new XElement("Action", new XAttribute("type", "Direct")),
            new XElement("Targets", "localhost; 127.0.0.1; %ComputerName%; ::1"),
            new XElement("Name", "Localhost"));
    }

    private static XElement CreateProxyElement(ProxyServer proxy, int profileId)
    {
        var proxyElement = new XElement("Proxy",
            new XAttribute("id", profileId),
            new XAttribute("type", GetProfileProxyType(proxy.Protocol)));

        if (!string.IsNullOrWhiteSpace(proxy.Username))
        {
            proxyElement.Add(
                new XElement("Authentication",
                    new XAttribute("enabled", "true"),
                    new XElement("Password", proxy.Password ?? string.Empty),
                    new XElement("Username", proxy.Username)));
        }

        proxyElement.Add(
            new XElement("Options", "48"),
            new XElement("Port", proxy.Port),
            new XElement("Address", proxy.Host));

        return proxyElement;
    }

    private static string GetProfileProxyType(ProxyProtocol protocol) => protocol.ToProxifierProfileType();

    private static string FormatApplicationValue(string executableName)
    {
        var value = executableName.Trim();
        if (value.Length == 0 ||
            value.StartsWith('"') && value.EndsWith('"'))
        {
            return value;
        }

        return value.Any(char.IsWhiteSpace)
            ? $"\"{value}\""
            : value;
    }

    private static string GetApplicationSelector(ApplicationRule rule)
    {
        return rule.TargetType == ApplicationTargetType.Emulator
            ? $"pid={rule.ProcessId!.Value}"
            : rule.ExecutableName;
    }

    private static string GetRuleName(ApplicationRule rule) => rule.GetApplicationName();

    private static string Serialize(XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            OmitXmlDeclaration = false
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        return Encoding.UTF8
            .GetString(stream.ToArray())
            .Replace("encoding=\"utf-8\"", "encoding=\"UTF-8\"", StringComparison.Ordinal);
    }

    private static XDocument MaskPasswords(XDocument source)
    {
        var clone = new XDocument(source);
        foreach (var password in clone.Descendants("Password"))
        {
            password.Value = "****";
        }

        return clone;
    }
}
