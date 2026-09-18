using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Storage;

public sealed class SecureLocalSecretStore(AppDataPaths paths, ICredentialProtector protector) : ILocalSecretStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        if (!File.Exists(paths.SecretsFilePath))
        {
            return null;
        }

        try
        {
            var protectedValue = await File.ReadAllTextAsync(paths.SecretsFilePath, Encoding.UTF8, cancellationToken);
            if (string.IsNullOrWhiteSpace(protectedValue))
            {
                return null;
            }

            var plaintext = protector.Unprotect(protectedValue);
            var record = JsonSerializer.Deserialize<AuthSecretRecord>(plaintext, SerializerOptions);
            if (record?.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(record.AccessToken) ||
                string.IsNullOrWhiteSpace(record.RefreshToken))
            {
                await ClearSessionAsync(cancellationToken);
                return null;
            }

            return new AuthSession
            {
                AccessToken = record.AccessToken,
                RefreshToken = record.RefreshToken,
                AccessTokenExpiresAt = record.AccessTokenExpiresAt,
                CreatedAt = record.CreatedAt,
                User = record.User
            };
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or JsonException)
        {
            await ClearSessionAsync(cancellationToken);
            return null;
        }
    }

    public async Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        paths.EnsureDirectories();
        var record = new AuthSecretRecord
        {
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            AccessTokenExpiresAt = session.AccessTokenExpiresAt,
            CreatedAt = session.CreatedAt == default ? DateTimeOffset.UtcNow : session.CreatedAt,
            User = session.User
        };

        var plaintext = JsonSerializer.Serialize(record, SerializerOptions);
        var protectedValue = protector.Protect(plaintext);
        await File.WriteAllTextAsync(paths.SecretsFilePath, protectedValue, Encoding.UTF8, cancellationToken);
    }

    public Task ClearSessionAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        if (File.Exists(paths.SecretsFilePath))
        {
            File.Delete(paths.SecretsFilePath);
        }

        return Task.CompletedTask;
    }

    private sealed class AuthSecretRecord
    {
        public int SchemaVersion { get; init; } = 1;
        public string AccessToken { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
        public DateTimeOffset AccessTokenExpiresAt { get; init; }
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public AuthUser? User { get; init; }
    }
}
