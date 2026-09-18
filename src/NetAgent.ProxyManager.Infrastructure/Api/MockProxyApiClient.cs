using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class MockProxyApiClient : IProxyApiClient
{
    public Task<IReadOnlyList<ProxyServer>> GetMyProxiesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ProxyServer> proxies =
        [
            new ProxyServer
            {
                Proxy = "127.0.0.1:1080",
                Protocol = ProxyProtocol.Socks5,
                Username = "mock_user",
                Password = "mock_pass",
                IsFromBackend = true
            },
            new ProxyServer
            {
                Proxy = "sg-1.proxy.example.com:1080",
                Protocol = ProxyProtocol.Socks5,
                Username = "user_001",
                Password = "pass_001",
                IsFromBackend = true
            },
            new ProxyServer
            {
                Proxy = "us-1.proxy.example.com:1080",
                Protocol = ProxyProtocol.Socks5,
                Username = "user_002",
                Password = "pass_002",
                IsFromBackend = true
            },
            new ProxyServer
            {
                Proxy = "vn-1.proxy.example.com:1080",
                Protocol = ProxyProtocol.Https,
                Username = "user_003",
                Password = "pass_003",
                IsFromBackend = true
            }
        ];

        return Task.FromResult(proxies);
    }

    public async Task<ProxySessionDto> CreateProxySessionAsync(
        string countryCode,
        CancellationToken cancellationToken)
    {
        var proxies = await GetMyProxiesAsync(cancellationToken);
        return new ProxySessionDto
        {
            SessionId = $"mock_{Guid.NewGuid():N}",
            CountryCode = countryCode,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            Proxies = proxies.Take(1).ToList()
        };
    }
}
