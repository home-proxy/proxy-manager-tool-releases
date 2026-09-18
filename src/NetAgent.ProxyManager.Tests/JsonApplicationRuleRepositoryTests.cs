using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Storage;
using System.Security.Cryptography;
using System.Text;

namespace NetAgent.ProxyManager.Tests;

public sealed class JsonApplicationRuleRepositoryTests
{
    [Fact]
    public async Task Repository_ShouldShareApplicationRulesAcrossAuthenticatedUsersOnSameMachine()
    {
        var root = Path.Combine(Path.GetTempPath(), "netagent-app-rule-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppDataPaths(root);
            var userARepository = new JsonApplicationRuleRepository(paths, new FakeAuthService("user-a"));
            var userBRepository = new JsonApplicationRuleRepository(paths, new FakeAuthService("user-b"));

            await userARepository.SaveAllAsync(
                [new ApplicationRule { ExecutableName = "chrome.exe" }],
                CancellationToken.None);

            (await userARepository.GetAllAsync(CancellationToken.None)).Should().ContainSingle();
            (await userBRepository.GetAllAsync(CancellationToken.None))
                .Should()
                .ContainSingle(rule => rule.ExecutableName == "chrome.exe");
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
    public async Task Repository_ShouldMigrateLegacyUserScopedApplicationRulesToMachineScopedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "netagent-app-rule-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppDataPaths(root);
            var legacyUserKey = BuildStorageKey("user-a");
            paths.EnsureUserDirectory(legacyUserKey);
            await File.WriteAllTextAsync(
                paths.GetUserApplicationRulesFilePath(legacyUserKey),
                """
                [
                  {
                    "executableName": "chrome.exe",
                    "isEnabled": false,
                    "autoAssignProxy": true
                  }
                ]
                """);

            var repository = new JsonApplicationRuleRepository(paths, new FakeAuthService("user-a"));

            var rules = await repository.GetAllAsync(CancellationToken.None);

            rules.Should().ContainSingle(rule => rule.ExecutableName == "chrome.exe");
            File.Exists(paths.ApplicationRulesFilePath).Should().BeTrue();
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
    public async Task Repository_ShouldMigrateMostRecentLegacyApplicationRulesWhenNoUserScopeIsAvailable()
    {
        var root = Path.Combine(Path.GetTempPath(), "netagent-app-rule-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppDataPaths(root);
            var olderFilePath = await WriteLegacyRulesAsync(paths, "user-a", "old.exe");
            var newerFilePath = await WriteLegacyRulesAsync(paths, "user-b", "new.exe");
            File.SetLastWriteTimeUtc(olderFilePath, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(newerFilePath, DateTime.UtcNow);

            var repository = new JsonApplicationRuleRepository(paths);

            var rules = await repository.GetAllAsync(CancellationToken.None);

            rules.Should().ContainSingle(rule => rule.ExecutableName == "new.exe");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
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

    private static string BuildStorageKey(string identifier)
    {
        var normalized = identifier.Trim().ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return $"user-{hash[..16]}";
    }

    private static async Task<string> WriteLegacyRulesAsync(AppDataPaths paths, string userId, string executableName)
    {
        var legacyUserKey = BuildStorageKey(userId);
        paths.EnsureUserDirectory(legacyUserKey);
        var filePath = paths.GetUserApplicationRulesFilePath(legacyUserKey);
        await File.WriteAllTextAsync(
            filePath,
            $$"""
            [
              {
                "executableName": "{{executableName}}",
                "isEnabled": false,
                "autoAssignProxy": true
              }
            ]
            """);
        return filePath;
    }
}
