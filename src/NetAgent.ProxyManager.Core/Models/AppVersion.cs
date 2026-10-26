using System.Globalization;

namespace NetAgent.ProxyManager.Core.Models;

public readonly record struct AppVersion(int Major, int Minor, int Patch, int Revision) : IComparable<AppVersion>
{
    public static bool TryParse(string? text, out AppVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value[0] is 'v' or 'V')
        {
            value = value[1..];
        }

        var suffix = value.IndexOfAny(['+', '-']);
        if (suffix >= 0)
        {
            value = value[..suffix];
        }

        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 4)
        {
            return false;
        }

        var numbers = new int[4];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 0)
            {
                return false;
            }

            numbers[index] = number;
        }

        version = new AppVersion(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }

    public int CompareTo(AppVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
        {
            return minor;
        }

        var patch = Patch.CompareTo(other.Patch);
        return patch != 0 ? patch : Revision.CompareTo(other.Revision);
    }

    public string ToDisplayString() =>
        Revision == 0
            ? $"{Major}.{Minor}.{Patch}"
            : $"{Major}.{Minor}.{Patch}.{Revision}";

    public override string ToString() => ToDisplayString();
}
