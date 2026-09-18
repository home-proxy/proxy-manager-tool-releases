namespace NetAgent.ProxyManager.Infrastructure.Api;

public static class BackendApiEnvironmentResolver
{
    public const string Development = "Development";
    public const string Production = "Production";

    public static string ResolveEnvironmentName(string? appSettingsEnvironment, string? netAgentEnvironment)
    {
        var value = string.IsNullOrWhiteSpace(netAgentEnvironment)
            ? appSettingsEnvironment
            : netAgentEnvironment;

        return NormalizeEnvironmentName(value);
    }

    public static string NormalizeEnvironmentName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Production;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "dev", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, Development, StringComparison.OrdinalIgnoreCase))
        {
            return Development;
        }

        if (string.Equals(normalized, "prod", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, Production, StringComparison.OrdinalIgnoreCase))
        {
            return Production;
        }

        return normalized;
    }
}
