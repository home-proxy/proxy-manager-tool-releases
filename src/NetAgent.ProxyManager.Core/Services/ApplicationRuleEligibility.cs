using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public static class ApplicationRuleEligibility
{
    public const string ExpiredAssignedProxyMessage = "Proxy đã gắn đã hết hạn.";
    public const string MissingAssignedProxyMessage = "Chưa được gắn proxy";
    public const string EmulatorLauncherExecutableMessage =
        "File khởi chạy giả lập – nhiều instance dùng chung, hãy xoá và thêm lại từ danh sách giả lập.";

    public static ApplicationRuleEligibilityResult Evaluate(
        ApplicationRule rule,
        IReadOnlyDictionary<Guid, ProxyServer> proxiesById) =>
        Evaluate(rule, proxiesById, DateTimeOffset.Now);

    public static ApplicationRuleEligibilityResult Evaluate(
        ApplicationRule rule,
        IReadOnlyDictionary<Guid, ProxyServer> proxiesById,
        DateTimeOffset now)
    {
        if (rule.TargetType == ApplicationTargetType.Executable &&
            string.IsNullOrWhiteSpace(rule.ExecutableName))
        {
            return ApplicationRuleEligibilityResult.Invalid("Chưa có file .exe");
        }

        if (rule.TargetType == ApplicationTargetType.Executable &&
            EmulatorLauncherProcesses.IsLauncherExecutablePath(rule.ExecutableName))
        {
            return ApplicationRuleEligibilityResult.Invalid(EmulatorLauncherExecutableMessage);
        }

        if (rule.TargetType == ApplicationTargetType.Emulator &&
            rule.ProcessId is null or <= 0)
        {
            return ApplicationRuleEligibilityResult.Invalid("Hãy mở giả lập");
        }

        if (rule.AssignedProxyId is null ||
            !proxiesById.TryGetValue(rule.AssignedProxyId.Value, out var proxy))
        {
            return ApplicationRuleEligibilityResult.Invalid(MissingAssignedProxyMessage);
        }

        if (!proxy.HasValidEndpoint)
        {
            return ApplicationRuleEligibilityResult.Invalid("Proxy đã gắn chưa có endpoint hợp lệ.");
        }

        if (proxy.ExpiredAt is not null && proxy.ExpiredAt.Value <= now)
        {
            return ApplicationRuleEligibilityResult.Invalid(ExpiredAssignedProxyMessage);
        }

        return ApplicationRuleEligibilityResult.Valid;
    }

    public static ApplicationRuleEligibilityResult Evaluate(
        ApplicationRule rule,
        IEnumerable<ProxyServer> proxies) =>
        Evaluate(rule, proxies.ToDictionary(proxy => proxy.Id));

    public static ApplicationRuleEligibilityResult Evaluate(
        ApplicationRule rule,
        IEnumerable<ProxyServer> proxies,
        DateTimeOffset now) =>
        Evaluate(rule, proxies.ToDictionary(proxy => proxy.Id), now);

    public static bool HasAnyUsableEnabledRule(
        IEnumerable<ApplicationRule> rules,
        IEnumerable<ProxyServer> proxies)
    {
        return HasAnyUsableEnabledRule(rules, proxies, DateTimeOffset.Now);
    }

    public static bool HasAnyUsableEnabledRule(
        IEnumerable<ApplicationRule> rules,
        IEnumerable<ProxyServer> proxies,
        DateTimeOffset now)
    {
        var proxiesById = proxies.ToDictionary(proxy => proxy.Id);
        return rules.Any(rule => rule.IsEnabled && Evaluate(rule, proxiesById, now).CanUseProxy);
    }
}

public sealed record ApplicationRuleEligibilityResult(bool CanUseProxy, string Message)
{
    public static ApplicationRuleEligibilityResult Valid { get; } = new(true, string.Empty);

    public static ApplicationRuleEligibilityResult Invalid(string message) => new(false, message);
}
