using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;

namespace NetAgent.ProxyManager.Tests;

public sealed class HomeProxyAuthApiClientTests
{
    [Fact]
    public async Task LoginAsync_ShouldParseTokenExpiresAsUnixMilliseconds()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            request.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/auth/login");
            return JsonResponse("""
                {
                  "refreshToken": "refresh",
                  "token": "access",
                  "tokenExpires": 1779419495742,
                  "user": {
                    "id": "user_1",
                    "phone": "+84862285763",
                    "coin": 15592,
                    "firstName": "Phu",
                    "lastName": "Loc"
                  }
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions()));

        var session = await api.LoginAsync("0862285763", "password", CancellationToken.None);

        session.AccessToken.Should().Be("access");
        session.RefreshToken.Should().Be("refresh");
        session.AccessTokenExpiresAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1779419495742));
        session.User!.DisplayName.Should().Be("Phu Loc");
        session.User.Coin.Should().Be(15592);
    }

    [Fact]
    public async Task LoginAsync_ShouldParseNumericRoleIdWithoutThrowing()
    {
        // Regression test: backend sends role.id as a JSON *number* (e.g. 2), not a string.
        // A RoleDto.Id typed as string throws a JsonException here and silently breaks login.
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
            JsonResponse("""
                {
                  "refreshToken": "refresh",
                  "token": "access",
                  "tokenExpires": 1779419495742,
                  "user": {
                    "id": "user_1",
                    "phone": "+84862285763",
                    "role": { "id": 2, "name": "Merchant", "__entity": "RoleEntity" }
                  }
                }
                """)))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions()));

        var session = await api.LoginAsync("0862285763", "password", CancellationToken.None);

        session.User!.RoleId.Should().Be("2");
    }

    [Fact]
    public async Task GetMeAsync_ShouldUseMerchantCoinWhenRootCoinIsMissing()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            request.RequestUri!.OriginalString.Should().Be("https://api.homeproxy.vn/api/v1/auth/me");
            return JsonResponse("""
                {
                  "id": "user_1",
                  "phone": "+84862285763",
                  "createdAt": "2026-06-22T00:00:00Z",
                  "merchant": {
                    "coin": 9012
                  }
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions()));

        var user = await api.GetMeAsync("access", CancellationToken.None);

        user.Coin.Should().Be(9012);
        user.CreatedAt.Should().Be(DateTimeOffset.Parse("2026-06-22T00:00:00Z"));
    }

    [Fact]
    public async Task PatchMeAsync_ShouldSendBearerTokenAndUpdatePayload()
    {
        string? method = null;
        string? url = null;
        string? bearer = null;
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async request =>
        {
            method = request.Method.Method;
            url = request.RequestUri!.OriginalString;
            bearer = request.Headers.Authorization?.Parameter;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions()));

        await api.PatchMeAsync(new AuthUpdateRequest
        {
            FirstName = "Phu",
            LastName = "Loc",
            Email = "phu@example.com",
            Phone = "0988182912",
            UserName = "phuloc",
            Gender = "male",
            OldPassword = "old-password",
            Password = "new-password"
        }, "access", CancellationToken.None);

        method.Should().Be("PATCH");
        url.Should().Be("https://api.homeproxy.vn/api/v1/auth/me");
        bearer.Should().Be("access");
        body.Should().Contain("\"firstName\":\"Phu\"");
        body.Should().Contain("\"lastName\":\"Loc\"");
        body.Should().Contain("\"email\":\"phu@example.com\"");
        body.Should().Contain("\"phone\":\"0988182912\"");
        body.Should().Contain("\"userName\":\"phuloc\"");
        body.Should().Contain("\"gender\":\"male\"");
        body.Should().Contain("\"oldPassword\":\"old-password\"");
        body.Should().Contain("\"password\":\"new-password\"");
    }

    [Fact]
    public async Task PatchMeAsync_ShouldThrowStructuredAuthErrorForValidationPayloads()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "status": 422,
              "errors": {
                "email": "duplicateEmail"
              }
            }
            """, HttpStatusCode.UnprocessableEntity)))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions()));

        var act = async () => await api.PatchMeAsync(new AuthUpdateRequest
        {
            FirstName = "Phu",
            LastName = "Loc",
            Email = "used@example.com",
            Phone = "0988182912"
        }, "access", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AuthApiException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        exception.Which.FieldErrors.Should().Contain("email", "duplicateEmail");
    }

    public static TheoryData<string, string, string> AuthValidationErrors => new()
    {
        {
            """
            {
              "status": 422,
              "errors": {
                "phone": "invalidPhone"
              }
            }
            """,
            "phone",
            "invalidPhone"
        },
        {
            """
            {
              "status": 422,
              "errors": {
                "password": "incorrectPassword"
              }
            }
            """,
            "password",
            "incorrectPassword"
        },
        {
            """
            {
              "status": 422,
              "errors": {
                "phone": {
                  "key": "notFound",
                  "msg": "Phone not found"
                }
              }
            }
            """,
            "phone",
            "notFound"
        }
    };

    [Theory]
    [MemberData(nameof(AuthValidationErrors))]
    public async Task LoginAsync_ShouldThrowStructuredAuthErrorForValidationPayloads(
        string responseJson,
        string expectedField,
        string expectedCode)
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(responseJson, HttpStatusCode.UnprocessableEntity)))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions()));

        var act = async () => await api.LoginAsync("0862285763", "wrong-password", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AuthApiException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        exception.Which.FieldErrors.Should().Contain(expectedField, expectedCode);
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowStructuredAuthErrorForExistingPhone()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "status": 422,
              "errors": {
                "phone": "phoneExists"
              }
            }
            """, HttpStatusCode.UnprocessableEntity)))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions()));

        var act = async () => await api.RegisterAsync(new AuthRegisterRequest
        {
            Email = "test@example.com",
            Password = "123123123",
            FirstName = "John",
            LastName = "Doe",
            Phone = "0988182912"
        }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AuthApiException>();
        exception.Which.FieldErrors.Should().Contain("phone", "phoneExists");
    }

    [Fact]
    public async Task LoginAsync_ShouldNotWriteCredentialsToConsoleWhenValidationFails()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "status": 422,
              "errors": {
                "password": "incorrectPassword"
              }
            }
            """, HttpStatusCode.UnprocessableEntity)))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions()));
        using var output = new StringWriter();
        var originalOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            var act = async () => await api.LoginAsync("0862285763", "super-secret-password", CancellationToken.None);

            await act.Should().ThrowAsync<AuthApiException>();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        output.ToString().Should().NotContain("0862285763");
        output.ToString().Should().NotContain("super-secret-password");
    }

    [Fact]
    public async Task RegisterAsync_ShouldSendConfiguredDomainAndRefClickId()
    {
        string? body = null;
        using var client = new HttpClient(new AsyncStubHttpMessageHandler(async request =>
        {
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Created);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions
        {
            RegisterDomain = "app.homeproxy.vn",
            RegisterRefClickId = "00000000-0000-4000-8000-000000000001"
        }));

        await api.RegisterAsync(new AuthRegisterRequest
        {
            Email = "test@example.com",
            Password = "123123123",
            FirstName = "John",
            LastName = "Doe",
            Phone = "0988182912"
        }, CancellationToken.None);

        body.Should().Contain("\"domain\":\"app.homeproxy.vn\"");
        body.Should().Contain("\"refClickId\":\"00000000-0000-4000-8000-000000000001\"");
    }

    [Fact]
    public async Task RefreshAsync_ShouldSendRefreshTokenAsBearerAuthorization()
    {
        string? bearerToken = null;
        using var client = new HttpClient(new StubHttpMessageHandler(request =>
        {
            bearerToken = request.Headers.Authorization?.Parameter;
            return JsonResponse("""
                {
                  "refreshToken": "new_refresh",
                  "token": "new_access",
                  "tokenExpires": 1779419495742
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.homeproxy.vn/api/")
        };
        var api = new HomeProxyAuthApiClient(client, Options.Create(new BackendApiOptions()));

        var session = await api.RefreshAsync("old_refresh", CancellationToken.None);

        bearerToken.Should().Be("old_refresh");
        session.AccessToken.Should().Be("new_access");
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json)
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class AsyncStubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request);
    }
}
