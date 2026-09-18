using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class BearerTokenHandler(IAuthService authService) : DelegatingHandler
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsAuthEndpoint(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var session = await authService.GetSessionAsync(cancellationToken);
        if (session is not null && !session.HasUsableAccessToken(RefreshSkew))
        {
            session = await RefreshSingleFlightAsync(session.AccessToken, cancellationToken);
        }

        if (session is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        var retryRequest = await CloneAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            retryRequest.Dispose();
            return response;
        }

        var isExpiredAccessToken = await IsExpiredAccessTokenResponseAsync(response, cancellationToken);
        if (!isExpiredAccessToken)
        {
            retryRequest.Dispose();
            await authService.ClearAsync(cancellationToken);
            return response;
        }

        response.Dispose();
        var refreshed = await RefreshSingleFlightAsync(session?.AccessToken, cancellationToken);
        if (refreshed is null)
        {
            retryRequest.Dispose();
            await authService.ClearAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                RequestMessage = request
            };
        }

        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private async Task<AuthSession?> RefreshSingleFlightAsync(
        string? tokenObservedByRequest,
        CancellationToken cancellationToken)
    {
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var current = await authService.GetSessionAsync(cancellationToken);
            if (current is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(tokenObservedByRequest) &&
                !string.Equals(current.AccessToken, tokenObservedByRequest, StringComparison.Ordinal) &&
                current.HasUsableAccessToken(RefreshSkew))
            {
                return current;
            }

            return await authService.RefreshAsync(cancellationToken);
        }
        catch
        {
            await authService.ClearAsync(cancellationToken);
            return null;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static bool IsAuthEndpoint(HttpRequestMessage request)
    {
        var path = request.RequestUri?.OriginalString ?? string.Empty;
        return path.Contains("/auth/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("auth/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> IsExpiredAccessTokenResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return false;
        }

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(cancellationToken);
            return payload?.StatusCode == 401 &&
                string.Equals(payload.Message, "No valid authentication provided", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private sealed class AuthErrorResponse
    {
        public string? Message { get; init; }
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; init; }
    }
}
