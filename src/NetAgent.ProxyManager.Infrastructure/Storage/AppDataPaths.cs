namespace NetAgent.ProxyManager.Infrastructure.Storage;

public sealed class AppDataPaths
{
    public AppDataPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProxyManager");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        ProfilesDirectory = Path.Combine(RootDirectory, "ProxifierProfiles");
    }

    public string RootDirectory { get; }
    public string LogsDirectory { get; }
    public string ProfilesDirectory { get; }
    public string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");
    public string ProxiesFilePath => Path.Combine(RootDirectory, "proxies.json");
    public string ApplicationRulesFilePath => Path.Combine(RootDirectory, "application-rules.json");
    public string UsersDirectory => Path.Combine(RootDirectory, "users");
    public string SecretsFilePath => Path.Combine(RootDirectory, "secrets.bin");
    public string LogFilePath => Path.Combine(LogsDirectory, "app.log");

    public string GetUserDirectory(string userStorageKey) =>
        Path.Combine(UsersDirectory, userStorageKey);

    public string GetUserProxiesFilePath(string userStorageKey) =>
        Path.Combine(GetUserDirectory(userStorageKey), "proxies.json");

    public string GetUserApplicationRulesFilePath(string userStorageKey) =>
        Path.Combine(GetUserDirectory(userStorageKey), "application-rules.json");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(UsersDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ProfilesDirectory);
    }

    public void EnsureUserDirectory(string userStorageKey)
    {
        EnsureDirectories();
        Directory.CreateDirectory(GetUserDirectory(userStorageKey));
    }
}
