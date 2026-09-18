using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ShouldPersistTokenSession()
    {
        var api = new FakeAuthApiClient();
        var store = new InMemorySecretStore();
        var service = new AuthService(store, api);

        var session = await service.LoginAsync("0862285763", "password", CancellationToken.None);

        session.AccessToken.Should().Be("access_1");
        store.Session.Should().NotBeNull();
        store.Session!.RefreshToken.Should().Be("refresh_1");
    }

    [Fact]
    public async Task LoginAsync_ShouldKeepSessionInMemoryOnlyWhenRememberIsDisabled()
    {
        var api = new FakeAuthApiClient();
        var store = new InMemorySecretStore
        {
            Session = new AuthSession
            {
                AccessToken = "old_saved_access",
                RefreshToken = "old_saved_refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
            }
        };
        var service = new AuthService(store, api);

        var session = await service.LoginAsync("0862285763", "password", rememberSession: false, CancellationToken.None);
        var current = await service.GetSessionAsync(CancellationToken.None);

        session.AccessToken.Should().Be("access_1");
        current!.AccessToken.Should().Be("access_1");
        store.Session.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_ShouldRegisterThenLogin()
    {
        var api = new FakeAuthApiClient();
        var store = new InMemorySecretStore();
        var service = new AuthService(store, api);

        await service.RegisterAsync(new AuthRegisterRequest
        {
            Email = "test@example.com",
            Password = "password",
            FirstName = "John",
            LastName = "Doe",
            Phone = "0988182912"
        }, CancellationToken.None);

        api.RegisterCalls.Should().Be(1);
        api.LoginCalls.Should().Be(1);
        store.Session.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldRotateTokensAndKeepUser()
    {
        var api = new FakeAuthApiClient();
        var store = new InMemorySecretStore
        {
            Session = new AuthSession
            {
                AccessToken = "old_access",
                RefreshToken = "old_refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                User = new AuthUser { Id = "user_1", Phone = "+84862285763" }
            }
        };
        var service = new AuthService(store, api);

        var refreshed = await service.RefreshAsync(CancellationToken.None);

        refreshed.Should().NotBeNull();
        refreshed!.AccessToken.Should().Be("access_2");
        refreshed.RefreshToken.Should().Be("refresh_2");
        refreshed.User!.Id.Should().Be("user_1");
        api.RefreshTokenReceived.Should().Be("old_refresh");
    }

    [Fact]
    public async Task LogoutAsync_ShouldCallBackendAndClearLocalSession()
    {
        var api = new FakeAuthApiClient();
        var store = new InMemorySecretStore
        {
            Session = new AuthSession
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
            }
        };
        var service = new AuthService(store, api);

        await service.LogoutAsync(CancellationToken.None);

        api.LogoutTokenReceived.Should().Be("access");
        store.Session.Should().BeNull();
    }

    [Fact]
    public async Task EnsureValidSessionAsync_ShouldRefreshExpiredSessionAndValidateMe()
    {
        var api = new FakeAuthApiClient();
        var store = new InMemorySecretStore
        {
            Session = new AuthSession
            {
                AccessToken = "old_access",
                RefreshToken = "old_refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            }
        };
        var service = new AuthService(store, api);

        var result = await service.EnsureValidSessionAsync(CancellationToken.None);

        result.Should().BeTrue();
        api.RefreshCalls.Should().Be(1);
        api.GetMeTokenReceived.Should().Be("access_2");
        store.Session!.User!.Id.Should().Be("user_1");
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_ShouldPatchMeAndPersistRefreshedUser()
    {
        var api = new FakeAuthApiClient
        {
            CurrentUser = new AuthUser
            {
                Id = "user_1",
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane@example.com",
                Phone = "0988182912"
            }
        };
        var store = new InMemorySecretStore
        {
            Session = new AuthSession
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                User = new AuthUser { Id = "user_1", Phone = "0988182912" }
            }
        };
        var service = new AuthService(store, api);

        var user = await service.UpdateCurrentUserAsync(new AuthUpdateRequest
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Phone = "0988182912"
        }, CancellationToken.None);

        api.PatchMeTokenReceived.Should().Be("access");
        api.PatchMeRequest!.Email.Should().Be("jane@example.com");
        user.DisplayName.Should().Be("Jane Doe");
        store.Session!.User!.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldPatchThenLoginWithNewPassword()
    {
        var api = new FakeAuthApiClient();
        var store = new InMemorySecretStore
        {
            Session = new AuthSession
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                User = new AuthUser { Id = "user_1", Phone = "0988182912" }
            }
        };
        var service = new AuthService(store, api);

        var session = await service.ChangePasswordAsync(new AuthUpdateRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "0988182912",
            OldPassword = "old-password",
            Password = "new-password"
        }, CancellationToken.None);

        api.PatchMeTokenReceived.Should().Be("access");
        api.LastLoginPassword.Should().Be("new-password");
        session.AccessToken.Should().Be("access_1");
        store.Session!.AccessToken.Should().Be("access_1");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldNormalizeVietnamPhoneBeforeRelogin()
    {
        var api = new FakeAuthApiClient();
        var store = new InMemorySecretStore
        {
            Session = new AuthSession
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                User = new AuthUser { Id = "user_1", Phone = "+84988182912" }
            }
        };
        var service = new AuthService(store, api);

        await service.ChangePasswordAsync(new AuthUpdateRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "+84988182912",
            OldPassword = "old-password",
            Password = "new-password"
        }, CancellationToken.None);

        api.LastLoginPhone.Should().Be("0988182912");
        api.LastLoginPassword.Should().Be("new-password");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldNormalizeVietnamPhoneWithoutPlusBeforeRelogin()
    {
        var api = new FakeAuthApiClient();
        var store = new InMemorySecretStore
        {
            Session = new AuthSession
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                User = new AuthUser { Id = "user_1", Phone = "84988182912" }
            }
        };
        var service = new AuthService(store, api);

        await service.ChangePasswordAsync(new AuthUpdateRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "84988182912",
            OldPassword = "old-password",
            Password = "new-password"
        }, CancellationToken.None);

        api.LastLoginPhone.Should().Be("0988182912");
        api.LastLoginPassword.Should().Be("new-password");
    }


    [Fact]
    public async Task ChangePasswordAsync_ShouldClearSessionWhenReloginFails()
    {
        var api = new FakeAuthApiClient { LoginShouldFail = true };
        var store = new InMemorySecretStore
        {
            Session = new AuthSession
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                User = new AuthUser { Id = "user_1", Phone = "0988182912" }
            }
        };
        var service = new AuthService(store, api);

        var act = async () => await service.ChangePasswordAsync(new AuthUpdateRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "0988182912",
            OldPassword = "old-password",
            Password = "new-password"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        api.PatchMeTokenReceived.Should().Be("access");
        store.Session.Should().BeNull();
    }

    private sealed class InMemorySecretStore : ILocalSecretStore
    {
        public AuthSession? Session { get; set; }

        public Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Session);

        public Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken)
        {
            Session = session;
            return Task.CompletedTask;
        }

        public Task ClearSessionAsync(CancellationToken cancellationToken)
        {
            Session = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthApiClient : IAuthApiClient
    {
        public int LoginCalls { get; private set; }
        public int RegisterCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public string? RefreshTokenReceived { get; private set; }
        public string? GetMeTokenReceived { get; private set; }
        public string? LogoutTokenReceived { get; private set; }
        public string? PatchMeTokenReceived { get; private set; }
        public string? LastLoginPhone { get; private set; }
        public string? LastLoginPassword { get; private set; }
        public AuthUpdateRequest? PatchMeRequest { get; private set; }
        public AuthUser CurrentUser { get; init; } = new() { Id = "user_1", Phone = "+84862285763" };
        public bool LoginShouldFail { get; init; }

        public Task<AuthSession> LoginAsync(string phone, string password, CancellationToken cancellationToken)
        {
            LoginCalls++;
            LastLoginPhone = phone;
            LastLoginPassword = password;
            if (LoginShouldFail)
            {
                throw new InvalidOperationException("login failed");
            }

            return Task.FromResult(new AuthSession
            {
                AccessToken = "access_1",
                RefreshToken = "refresh_1",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(45),
                User = new AuthUser { Id = "user_1", Phone = phone }
            });
        }

        public Task RegisterAsync(AuthRegisterRequest request, CancellationToken cancellationToken)
        {
            RegisterCalls++;
            return Task.CompletedTask;
        }

        public Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            RefreshCalls++;
            RefreshTokenReceived = refreshToken;
            return Task.FromResult(new AuthSession
            {
                AccessToken = "access_2",
                RefreshToken = "refresh_2",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(45)
            });
        }

        public Task<AuthUser> GetMeAsync(string accessToken, CancellationToken cancellationToken)
        {
            GetMeTokenReceived = accessToken;
            return Task.FromResult(CurrentUser);
        }

        public Task PatchMeAsync(AuthUpdateRequest request, string accessToken, CancellationToken cancellationToken)
        {
            PatchMeRequest = request;
            PatchMeTokenReceived = accessToken;
            return Task.CompletedTask;
        }

        public Task LogoutAsync(string accessToken, CancellationToken cancellationToken)
        {
            LogoutTokenReceived = accessToken;
            return Task.CompletedTask;
        }
    }
}
