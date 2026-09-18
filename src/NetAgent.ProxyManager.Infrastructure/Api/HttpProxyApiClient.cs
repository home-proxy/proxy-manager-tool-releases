using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class HttpProxyApiClient(HttpClient httpClient, IOptions<BackendApiOptions> options) : IProxyApiClient
{
    private readonly BackendApiOptions _options = options.Value;

    public async Task<IReadOnlyList<ProxyServer>> GetMyProxiesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint("my-proxies"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<MyProxiesResponse>(cancellationToken);
        return payload?.Data.Select(MapProxy).ToList() ?? [];
    }

    public async Task<ProxySessionDto> CreateProxySessionAsync(
        string countryCode,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("proxy-sessions"))
        {
            Content = JsonContent.Create(new { country_code = countryCode })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ProxySessionResponse>(cancellationToken);
        return payload is null
            ? throw new InvalidOperationException("Backend trả về payload rỗng.")
            : new ProxySessionDto
            {
                SessionId = payload.Data.SessionId,
                CountryCode = payload.Data.CountryCode,
                ExpiresAt = payload.Data.ExpiresAt,
                Proxies = payload.Data.Proxies.Select(MapProxy).ToList()
            };
    }

    private static ProxyServer MapProxy(ProxyDto dto)
    {
        if (!ProxyProtocolDisplay.TryParse(dto.Type, out var protocol))
        {
            protocol = ProxyProtocol.Socks5;
        }

        return new ProxyServer
        {
            Proxy = $"{dto.Host}:{dto.Port}",
            Username = dto.Username,
            Password = dto.Password,
            Protocol = protocol,
            IsFromBackend = true
        };
    }

    private string Endpoint(string path)
    {
        var version = string.IsNullOrWhiteSpace(_options.ApiVersion)
            ? "v1"
            : _options.ApiVersion.Trim().Trim('/');
        return $"{version}/{path.TrimStart('/')}";
    }

    private sealed class MyProxiesResponse
    {
        public bool Success { get; init; }
        public List<ProxyDto> Data { get; init; } = [];
        [JsonPropertyName("request_id")]
        public string? RequestId { get; init; }
    }

    private sealed class ProxySessionResponse
    {
        public bool Success { get; init; }
        public required ProxySessionPayload Data { get; init; }
    }

    private sealed class ProxySessionPayload
    {
        [JsonPropertyName("session_id")]
        public string SessionId { get; init; } = string.Empty;
        [JsonPropertyName("country_code")]
        public string CountryCode { get; init; } = string.Empty;
        [JsonPropertyName("expires_at")]
        public DateTimeOffset ExpiresAt { get; init; }
        public List<ProxyDto> Proxies { get; init; } = [];
    }

    private sealed class ProxyDto
    {
        public string Id { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Type { get; init; } = "socks5";
        public string Host { get; init; } = string.Empty;
        public int Port { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
        [JsonPropertyName("expires_at")]
        public DateTimeOffset? ExpiresAt { get; init; }
    }
}
