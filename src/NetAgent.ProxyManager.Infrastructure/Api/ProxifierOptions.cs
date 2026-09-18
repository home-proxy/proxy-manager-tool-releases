namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class ProxifierOptions
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string ProfileOutputDirectory { get; set; } = "%AppData%\\ProxyManager\\ProxifierProfiles";
    public string BundledInstallerPath { get; set; } = "Installers\\ProxifierSetup.exe";
    public string InstallerSha256 { get; set; } = string.Empty;
    public string SilentInstallArguments { get; set; } = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
    public int InstallerTimeoutSeconds { get; set; } = 600;
    public string RegistrationOwner { get; set; } = string.Empty;
    public string RegistrationKey { get; set; } = string.Empty;
}
