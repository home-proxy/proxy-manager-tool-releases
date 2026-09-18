using System.Text.Json;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Storage;

public sealed class JsonApplicationRuleRepository : IApplicationRuleRepository
{
    private readonly AppDataPaths _paths;
    private readonly AuthenticatedUserStorageScope? _legacyUserStorageScope;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonApplicationRuleRepository(AppDataPaths paths)
        : this(paths, authService: null)
    {
    }

    public JsonApplicationRuleRepository(AppDataPaths paths, IAuthService? authService)
    {
        _paths = paths;
        _legacyUserStorageScope = authService is null ? null : new AuthenticatedUserStorageScope(authService);
    }

    public async Task<IReadOnlyList<ApplicationRule>> GetAllAsync(CancellationToken cancellationToken)
    {
        var filePath = await GetFilePathAsync(cancellationToken);
        await TryMigrateLegacyUserScopedRulesAsync(filePath, cancellationToken);
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<ApplicationRule>>(stream, SerializerOptions, cancellationToken) ?? [];
    }

    public async Task SaveAllAsync(IReadOnlyCollection<ApplicationRule> rules, CancellationToken cancellationToken)
    {
        var filePath = await GetFilePathAsync(cancellationToken);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, rules, SerializerOptions, cancellationToken);
    }

    private Task<string> GetFilePathAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _paths.EnsureDirectories();
        return Task.FromResult(_paths.ApplicationRulesFilePath);
    }

    private async Task TryMigrateLegacyUserScopedRulesAsync(string machineScopedFilePath, CancellationToken cancellationToken)
    {
        if (File.Exists(machineScopedFilePath))
        {
            return;
        }

        var legacyFilePath = await FindLegacyUserScopedFileAsync(cancellationToken);
        if (legacyFilePath is null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(machineScopedFilePath)!);
        File.Copy(legacyFilePath, machineScopedFilePath, overwrite: false);
    }

    private async Task<string?> FindLegacyUserScopedFileAsync(CancellationToken cancellationToken)
    {
        if (_legacyUserStorageScope is not null)
        {
            var legacyStorageKey = await _legacyUserStorageScope.GetStorageKeyAsync(cancellationToken);
            var currentUserFilePath = _paths.GetUserApplicationRulesFilePath(legacyStorageKey);
            if (File.Exists(currentUserFilePath))
            {
                return currentUserFilePath;
            }
        }

        if (!Directory.Exists(_paths.UsersDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(_paths.UsersDirectory, "application-rules.json", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
