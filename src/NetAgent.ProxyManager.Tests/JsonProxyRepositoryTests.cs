using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Storage;

namespace NetAgent.ProxyManager.Tests;

public sealed class JsonProxyRepositoryTests
{
    [Fact]
    public async Task SaveAllAsync_ShouldPersistBackendMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "netagent-proxy-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new JsonProxyRepository(new AppDataPaths(root), new IdentityCredentialProtector());
            var proxy = new ProxyServer
            {
                Proxy = "127.0.0.1:8080",
                Protocol = ProxyProtocol.Https,
                Username = "user",
                Password = "pass",
                IsFromBackend = true,
                BackendUserProxyId = 123,
                BackendOrderKind = ProxyOrderKind.RotateProxy,
                BackendProvider = "Viettel",
                ExpiredAt = DateTimeOffset.UtcNow.AddDays(1)
            };

            await repository.SaveAllAsync([proxy], CancellationToken.None);
            var saved = (await repository.GetAllAsync(CancellationToken.None)).Single();

            saved.BackendUserProxyId.Should().Be(123);
            saved.BackendOrderKind.Should().Be(ProxyOrderKind.RotateProxy);
            saved.BackendProvider.Should().Be("Viettel");
            saved.ExpiredAt.Should().Be(proxy.ExpiredAt);
            saved.Password.Should().Be("pass");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Repository_ShouldScopeBackendProxyDataByAuthenticatedUser()
    {
        var root = Path.Combine(Path.GetTempPath(), "netagent-proxy-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppDataPaths(root);
            var userARepository = new JsonProxyRepository(paths, new IdentityCredentialProtector(), new FakeAuthService("user-a"));
            var userBRepository = new JsonProxyRepository(paths, new IdentityCredentialProtector(), new FakeAuthService("user-b"));

            await userARepository.SaveAllAsync(
                [
                    new ProxyServer
                    {
                        Proxy = "127.0.0.1:8080",
                        Protocol = ProxyProtocol.Https,
                        Username = "user-a",
                        Password = "pass-a",
                        IsFromBackend = true,
                        BackendUserProxyId = 100
                    }
                ],
                CancellationToken.None);

            (await userARepository.GetAllAsync(CancellationToken.None)).Should().ContainSingle();
            (await userBRepository.GetAllAsync(CancellationToken.None)).Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Repository_ShouldShareManualProxyDataAcrossAuthenticatedUsers()
    {
        var root = Path.Combine(Path.GetTempPath(), "netagent-proxy-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppDataPaths(root);
            var userARepository = new JsonProxyRepository(paths, new IdentityCredentialProtector(), new FakeAuthService("user-a"));
            var userBRepository = new JsonProxyRepository(paths, new IdentityCredentialProtector(), new FakeAuthService("user-b"));

            await userARepository.SaveAllAsync(
                [
                    new ProxyServer
                    {
                        Proxy = "127.0.0.1:8080",
                        Protocol = ProxyProtocol.Https,
                        Username = "manual",
                        Password = "pass"
                    }
                ],
                CancellationToken.None);

            (await userARepository.GetAllAsync(CancellationToken.None)).Should().ContainSingle();
            var shared = (await userBRepository.GetAllAsync(CancellationToken.None)).Should().ContainSingle().Subject;
            shared.IsFromBackend.Should().BeFalse();
            shared.Username.Should().Be("manual");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetAllAsync_ShouldReadLegacyRecordsWithoutBackendProvider()
    {
        var root = Path.Combine(Path.GetTempPath(), "netagent-proxy-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppDataPaths(root);
            var userDirectory = paths.GetUserDirectory("user-anonymous");
            Directory.CreateDirectory(userDirectory);
            await File.WriteAllTextAsync(
                paths.GetUserProxiesFilePath("user-anonymous"),
                """
                [
                  {
                    "id": "11111111-1111-1111-1111-111111111111",
                    "proxy": "127.0.0.1:8080",
                    "protocol": "HTTPS",
                    "username": "user",
                    "protectedPassword": "pass",
                    "isFromBackend": true,
                    "backendUserProxyId": 123,
                    "backendOrderKind": 0
                  }
                ]
                """);
            var repository = new JsonProxyRepository(paths, new IdentityCredentialProtector());

            var saved = (await repository.GetAllAsync(CancellationToken.None)).Single();

            saved.BackendProvider.Should().BeNull();
            saved.BackendUserProxyId.Should().Be(123);
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
        public string Protect(string value) => value;

        public string Unprotect(string protectedValue) => protectedValue;
    }

    private sealed class FakeAuthService(string userId) : IAuthService
    {
        private readonly AuthSession _session = new()
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            User = new AuthUser { Id = userId }
        };

        public Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AuthSession?>(_session);

        public Task<AuthSession> LoginAsync(string phone, string password, CancellationToken cancellationToken) =>
            Task.FromResult(_session);

        public Task<AuthSession> LoginAsync(string phone, string password, bool rememberSession, CancellationToken cancellationToken) =>
            Task.FromResult(_session);

        public Task<AuthSession> RegisterAsync(AuthRegisterRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_session);

        public Task<AuthSession> RegisterAsync(AuthRegisterRequest request, bool rememberSession, CancellationToken cancellationToken) =>
            Task.FromResult(_session);

        public Task<AuthSession?> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AuthSession?>(_session);

        public Task<AuthUser?> GetCurrentUserAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_session.User);

        public Task<AuthUser> UpdateCurrentUserAsync(AuthUpdateRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_session.User!);

        public Task<AuthSession> ChangePasswordAsync(AuthUpdateRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_session);

        public Task<bool> EnsureValidSessionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task LogoutAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public string MaskToken(string token) => token;
    }
}
