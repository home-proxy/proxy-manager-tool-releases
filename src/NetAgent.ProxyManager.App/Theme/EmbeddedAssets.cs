using System.Reflection;

namespace NetAgent.ProxyManager.App.Theme;

/// <summary>
/// Resolves files that are embedded into the assembly (config + UI assets) so the
/// single-file executable can run without any loose files sitting next to it.
/// Logical resource names are registered with forward/back slashes preserved; this
/// helper normalises separators and casing so lookups are stable across platforms.
/// </summary>
internal static class EmbeddedAssets
{
    private static readonly Assembly Assembly = typeof(EmbeddedAssets).Assembly;
    private static readonly Dictionary<string, string> ResourceNames = BuildResourceMap();

    private static Dictionary<string, string> BuildResourceMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Assembly.GetManifestResourceNames())
        {
            map[Normalize(name)] = name;
        }

        return map;
    }

    private static string Normalize(string value) =>
        value.Replace('\\', '/').Trim('/');

    /// <summary>Opens an embedded asset stream, or returns null when it does not exist.</summary>
    public static Stream? Open(string logicalPath)
    {
        var key = Normalize(logicalPath);

        if (ResourceNames.TryGetValue(key, out var exactName))
        {
            return Assembly.GetManifestResourceStream(exactName);
        }

        foreach (var pair in ResourceNames)
        {
            if (pair.Key.EndsWith("/" + key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return Assembly.GetManifestResourceStream(pair.Value);
            }
        }

        return null;
    }

    public static string? ReadAllText(string logicalPath)
    {
        using var stream = Open(logicalPath);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
