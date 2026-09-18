using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

// Launcher/UI companion processes for each emulator kind, as opposed to the network-carrying
// "host" process recognized by EmulatorProcessScanner.TryClassifyRuntimeProcess. All instances of
// a given emulator kind share the exact same launcher .exe path, so these can never be used to
// distinguish one running instance from another and must never become an Executable-type
// ApplicationRule target. Only the LDPlayer entry (dnplayer.exe) has been verified against a real
// running LDPlayer9 install; the others are best-effort public names and should be reverified on a
// machine with that emulator installed (see docs/08-adding-new-emulator.md, Bước 0).
public static class EmulatorLauncherProcesses
{
    private static readonly IReadOnlyDictionary<EmulatorKind, string[]> KnownLauncherProcessNames =
        new Dictionary<EmulatorKind, string[]>
        {
            [EmulatorKind.BlueStacks] = ["BlueStacks.exe", "HD-Agent.exe", "HD-Adb.exe", "HD-RunApp.exe", "BstkSVC.exe"],
            [EmulatorKind.LDPlayer] = ["dnplayer.exe", "dnmultiplayer.exe"],
            [EmulatorKind.MEmu] = ["MEmu.exe", "MEmuConsole.exe"],
            [EmulatorKind.Nox] = ["Nox.exe", "NoxMultiPlayer.exe"]
        };

    public static bool IsLauncherProcessName(EmulatorKind kind, string? processName) =>
        !string.IsNullOrWhiteSpace(processName) &&
        KnownLauncherProcessNames.TryGetValue(kind, out var names) &&
        names.Any(name => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));

    public static bool IsLauncherProcessName(IEnumerable<EmulatorKind> kinds, string? processName) =>
        !string.IsNullOrWhiteSpace(processName) &&
        kinds.Any(kind => IsLauncherProcessName(kind, processName));

    public static bool IsLauncherExecutablePath(string? executablePath) =>
        !string.IsNullOrWhiteSpace(executablePath) &&
        IsLauncherProcessName(Enum.GetValues<EmulatorKind>(), Path.GetFileName(executablePath.Trim().Trim('"')));
}
