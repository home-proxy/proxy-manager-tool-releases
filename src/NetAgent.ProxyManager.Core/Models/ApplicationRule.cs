namespace NetAgent.ProxyManager.Core.Models;

public sealed class ApplicationRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ApplicationTargetType TargetType { get; set; } = ApplicationTargetType.Executable;
    public string ExecutableName { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
    public EmulatorKind? EmulatorKind { get; set; }
    public string? EmulatorInstanceKey { get; set; }
    public string? EmulatorInstanceName { get; set; }
    public string? RuntimeProcessName { get; set; }
    public string? RuntimeExecutablePath { get; set; }
    public Guid? AssignedProxyId { get; set; }
    public string? Warning { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool AutoAssignProxy { get; set; } = true;

    public string GetApplicationName()
    {
        if (TargetType == ApplicationTargetType.Emulator)
        {
            if (!string.IsNullOrWhiteSpace(EmulatorInstanceName))
            {
                return EmulatorInstanceName.Trim();
            }

            return EmulatorKind is null ? "Emulator" : EmulatorKind.ToString()!;
        }

        return GetExecutableDisplayName(ExecutableName);
    }

    public static string GetExecutableDisplayName(string executableName)
    {
        var value = executableName.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var fileName = Path.GetFileName(value);
        return string.IsNullOrWhiteSpace(fileName) ? value : fileName;
    }
}
