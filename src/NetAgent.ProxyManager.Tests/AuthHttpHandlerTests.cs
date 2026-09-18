using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;

namespace NetAgent.ProxyManager.Tests;

public sealed class AuthHttpHandlerTests
{
    [Fact]
    public async Task MerchantHeaderHandler_ShouldAttachMerchantId()
    {
        HttpRequestMessage? captured = null;
        var handler = new MerchantHeaderHandler(Options.Create(new BackendApiOptions
        {
            MerchantId = "merchant_1"
        }))
        {
            InnerHandler = new StubHttpMessageHandler(request =>
            {
                captured = request;
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        using var response = await client.GetAsync("v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        captured!.Headers.GetValues("x-merchant-id").Single().Should().Be("merchant_1");
    }

    [Fact]
    public async Task BearerTokenHandler_ShouldAttachAccessToken()
    {
        HttpRequestMessage? captured = null;
        var auth = new FakeAuthService(new AuthSession
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        var handler = new BearerTokenHandler(auth)
        {
            InnerHandler = new StubHttpMessageHandler(request =>
            {
                captured = request;
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        _ = await client.GetAsync("v1/orders");

        captured!.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "access"));
    }

    [Fact]
    public async Task BearerTokenHandler_ShouldRefreshExpiredTokenAndRetryOnce()
    {
        var auth = new FakeAuthService(new AuthSession
        {
            AccessToken = "old_access",
            RefreshToken = "old_refresh",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        var tokens = new List<string?>();
        var handler = new BearerTokenHandler(auth)
        {
            InnerHandler = new StubHttpMessageHandler(request =>
            {
                tokens.Add(request.Headers.Authorization?.Parameter);
                return tokens.Count == 1
                    ? ExpiredAccessTokenResponse()
                    : new HttpResponseMessage(HttpStatusCode.OK);
            })
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        using var response = await client.GetAsync("v1/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tokens.Should().Equal("old_access", "new_access");
        auth.RefreshCalls.Should().Be(1);
    }

    [Fact]
    public async Task BearerTokenHandler_ShouldSingleFlightConcurrentRefreshes()
    {
        var auth = new FakeAuthService(new AuthSession
        {
            AccessToken = "old_access",
            RefreshToken = "old_refresh",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        var handler = new BearerTokenHandler(auth)
        {
            InnerHandler = new StubHttpMessageHandler(request =>
            {
                var token = request.Headers.Authorization?.Parameter;
                return token == "old_access"
                    ? ExpiredAccessTokenResponse()
                    : new HttpResponseMessage(HttpStatusCode.OK);
            })
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        var responses = await Task.WhenAll(
            client.GetAsync("v1/orders/1"),
            client.GetAsync("v1/orders/2"));

        responses.Select(response => response.StatusCode).Should().OnlyContain(code => code == HttpStatusCode.OK);
        auth.RefreshCalls.Should().Be(1);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task BearerTokenHandler_ShouldClearSessionWhenRefreshFails()
    {
        var auth = new FakeAuthService(new AuthSession
        {
            AccessToken = "old_access",
            RefreshToken = "old_refresh",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        })
        {
            RefreshShouldFail = true
        };
        var handler = new BearerTokenHandler(auth)
        {
            InnerHandler = new StubHttpMessageHandler(_ => ExpiredAccessTokenResponse())
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        using var response = await client.GetAsync("v1/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        auth.ClearCalls.Should().BeGreaterThan(0);
        auth.Session.Should().BeNull();
    }

    private static HttpResponseMessage ExpiredAccessTokenResponse() =>
        new(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                "{\"message\":\"No valid authentication provided\",\"error\":\"Unauthorized\",\"statusCode\":401}")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class FakeAuthService(AuthSession? session) : IAuthService
    {
        public AuthSession? Session { get; private set; } = session;
        public int RefreshCalls { get; private set; }
        public int ClearCalls { get; private set; }
        public bool RefreshShouldFail { get; init; }

        public Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Session);

        public Task<AuthSession> LoginAsync(string phone, string password, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AuthSession> LoginAsync(string phone, string password, bool rememberSession, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AuthSession> RegisterAsync(AuthRegisterRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AuthSession> RegisterAsync(AuthRegisterRequest request, bool rememberSession, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AuthSession?> RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCalls++;
            if (RefreshShouldFail)
            {
                throw new InvalidOperationException("refresh failed");
            }

            Session = new AuthSession
            {
                AccessToken = "new_access",
                RefreshToken = "new_refresh",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
            };
            return Task.FromResult<AuthSession?>(Session);
        }

        public Task<AuthUser?> GetCurrentUserAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Session?.User);

        public Task<AuthUser> UpdateCurrentUserAsync(AuthUpdateRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AuthSession> ChangePasswordAsync(AuthUpdateRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<bool> EnsureValidSessionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Session is not null);

        public Task LogoutAsync(CancellationToken cancellationToken)
        {
            Session = null;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            ClearCalls++;
            Session = null;
            return Task.CompletedTask;
        }

        public string MaskToken(string token) => token;
    }
}
