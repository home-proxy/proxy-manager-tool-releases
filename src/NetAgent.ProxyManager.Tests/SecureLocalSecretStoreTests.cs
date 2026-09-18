using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Infrastructure.Storage;

namespace NetAgent.ProxyManager.Tests;

public sealed class SecureLocalSecretStoreTests
{
    [Fact]
    public async Task GetSessionAsync_ShouldClearLegacyApiKeySecretPayload()
    {
        var root = Path.Combine(Path.GetTempPath(), $"netagent-secret-{Guid.NewGuid():N}");
        try
        {
            var paths = new AppDataPaths(root);
            paths.EnsureDirectories();
            await File.WriteAllTextAsync(paths.SecretsFilePath, "legacy-api-key", CancellationToken.None);

            var store = new SecureLocalSecretStore(paths, new IdentityCredentialProtector());

            var session = await store.GetSessionAsync(CancellationToken.None);

            session.Should().BeNull();
            File.Exists(paths.SecretsFilePath).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class IdentityCredentialProtector : ICredentialProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string Unprotect(string protectedValue) => protectedValue;
    }
}
