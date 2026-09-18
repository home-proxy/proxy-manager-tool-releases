using System.Text.Json;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;

namespace NetAgent.ProxyManager.Infrastructure.Storage;

public sealed class JsonAppSettingsRepository(
    AppDataPaths paths,
    IOptions<ProxifierOptions> proxifierOptions) : IAppSettingsRepository
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(80);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            paths.EnsureDirectories();
            if (File.Exists(paths.SettingsFilePath))
            {
                await using var stream = new FileStream(
                    paths.SettingsFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
                if (settings is not null)
                {
                    settings.DataGridColumnVisibility ??= [];
                    settings.ProfileOutputDirectory = ExpandPath(settings.ProfileOutputDirectory);
                    if (!Enum.IsDefined(settings.ProxyRestartConfirmationPreference))
                    {
                        settings.ProxyRestartConfirmationPreference = ProxyRestartConfirmationPreference.Ask;
                    }

                    return settings;
                }
            }

            var defaults = proxifierOptions.Value;
            return new AppSettings
            {
                ProxifierExecutablePath = defaults.ExecutablePath,
                ProfileOutputDirectory = ExpandPath(defaults.ProfileOutputDirectory),
                DefaultRouteDirect = true,
                ProxyRestartConfirmationPreference = ProxyRestartConfirmationPreference.Ask
            };
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            await SaveWithRetryAsync(settings, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task SaveWithRetryAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var tempPath = $"{paths.SettingsFilePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                paths.EnsureDirectories();
                await using (var stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
                }

                File.Move(tempPath, paths.SettingsFilePath, overwrite: true);
                return;
            }
            catch (Exception ex) when (
                attempt < maxAttempts &&
                ex is IOException or UnauthorizedAccessException)
            {
                TryDeleteTempFile(tempPath);
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch
            {
                TryDeleteTempFile(tempPath);
                throw;
            }
        }
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup only; a later save can overwrite the real settings file.
        }
    }

    private static string ExpandPath(string path) =>
        Environment.ExpandEnvironmentVariables(path.Replace("%AppData%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StringComparison.OrdinalIgnoreCase));
}
