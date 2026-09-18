using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProxyManagementServiceTests
{
    [Fact]
    public async Task CheckAsync_ShouldPersistLatencyForReachableProxy()
    {
        var proxy = new ProxyServer { Proxy = "127.0.0.1:1080", LatencyMs = 999 };
        var repository = new InMemoryProxyRepository([proxy]);
        var service = new ProxyManagementService(
            repository,
            new EmptyProxyApiClient(),
            new StubProxyChecker(new ProxyCheckResult(true, 42, EgressIp: "203.0.113.10")));

        await service.CheckAsync([proxy], CancellationToken.None);

        var saved = (await repository.GetAllAsync(CancellationToken.None)).Single();
        saved.Status.Should().Be(ProxyStatus.Live);
        saved.LatencyMs.Should().Be(42);
    }

    [Fact]
    public async Task CheckAsync_ShouldClearLatencyForDeadProxy()
    {
        var proxy = new ProxyServer { Proxy = "127.0.0.1:1080", LatencyMs = 999 };
        var repository = new InMemoryProxyRepository([proxy]);
        var service = new ProxyManagementService(
            repository,
            new EmptyProxyApiClient(),
            new StubProxyChecker(new ProxyCheckResult(false, null, "failed")));

        await service.CheckAsync([proxy], CancellationToken.None);

        var saved = (await repository.GetAllAsync(CancellationToken.None)).Single();
        saved.Status.Should().Be(ProxyStatus.Dead);
        saved.LatencyMs.Should().BeNull();
    }

    [Fact]
    public async Task UpsertBackendOrderAsync_ShouldCreateBackendProxy()
    {
        var repository = new InMemoryProxyRepository([]);
        var service = new ProxyManagementService(repository, new EmptyProxyApiClient(), new StubProxyChecker(new ProxyCheckResult(true, 1, "ok")));
        var order = CreateOrder();

        var proxy = await service.UpsertBackendOrderAsync(order, ProxyOrderKind.Static, null, CancellationToken.None);

        proxy.Proxy.Should().Be("127.0.0.1:8080");
        proxy.Username.Should().Be("user");
        proxy.Password.Should().Be("pass");
        proxy.Protocol.Should().Be(ProxyProtocol.Https);
        proxy.IsFromBackend.Should().BeTrue();
        proxy.BackendUserProxyId.Should().Be(123);
        proxy.BackendOrderKind.Should().Be(ProxyOrderKind.Static);
        proxy.BackendProvider.Should().Be("VNPT");
        (await repository.GetAllAsync(CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task UpsertBackendOrderAsync_ShouldUpdateExistingBackendProxyAndKeepId()
    {
        var existing = new ProxyServer
        {
            Id = Guid.NewGuid(),
            Proxy = "127.0.0.1:8080",
            Username = "old",
            Password = "old",
            BackendUserProxyId = 123
        };
        var repository = new InMemoryProxyRepository([existing]);
        var service = new ProxyManagementService(repository, new EmptyProxyApiClient(), new StubProxyChecker(new ProxyCheckResult(true, 1, "ok")));
        var order = CreateOrder(username: "new-user", password: "new-pass", protocol: ProxyProtocol.Socks5);

        var proxy = await service.UpsertBackendOrderAsync(order, ProxyOrderKind.Datacenter, null, CancellationToken.None);

        proxy.Id.Should().Be(existing.Id);
        proxy.Username.Should().Be("new-user");
        proxy.Password.Should().Be("new-pass");
        proxy.Protocol.Should().Be(ProxyProtocol.Socks5);
        proxy.BackendOrderKind.Should().Be(ProxyOrderKind.Datacenter);
        proxy.BackendProvider.Should().Be("CMC");
        (await repository.GetAllAsync(CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task UpsertBackendOrderAsync_ShouldPersistExpiredAt()
    {
        var repository = new InMemoryProxyRepository([]);
        var service = new ProxyManagementService(repository, new EmptyProxyApiClient(), new StubProxyChecker(new ProxyCheckResult(true, 1, "ok")));
        var expiredAt = DateTimeOffset.Parse("2026-07-03T00:00:00Z");
        var order = CreateOrder(expiredAt: expiredAt);

        var proxy = await service.UpsertBackendOrderAsync(order, ProxyOrderKind.Static, null, CancellationToken.None);

        proxy.ExpiredAt.Should().Be(expiredAt);
        var saved = (await repository.GetAllAsync(CancellationToken.None)).Single();
        saved.ExpiredAt.Should().Be(expiredAt);
    }

    private static ProxyOrder CreateOrder(
        string username = "user",
        string password = "pass",
        ProxyProtocol protocol = ProxyProtocol.Https,
        DateTimeOffset? expiredAt = null) =>
        new()
        {
            UserProxyId = 123,
            Provider = protocol == ProxyProtocol.Socks5 ? "CMC" : "VNPT",
            Ip = "127.0.0.1",
            Port = 8080,
            Username = username,
            Password = password,
            Protocol = protocol,
            ExpiredAt = expiredAt,
            CategoryTypeId = 1
        };

    private sealed class InMemoryProxyRepository(IReadOnlyCollection<ProxyServer> proxies) : IProxyRepository
    {
        private List<ProxyServer> _proxies = proxies.ToList();

        public Task<IReadOnlyList<ProxyServer>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProxyServer>>(_proxies);

        public Task SaveAllAsync(IReadOnlyCollection<ProxyServer> proxies, CancellationToken cancellationToken)
        {
            _proxies = proxies.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class StubProxyChecker(ProxyCheckResult result) : IProxyChecker
    {
        public Task<ProxyCheckResult> CheckAsync(ProxyServer proxy, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class EmptyProxyApiClient : IProxyApiClient
    {
        public Task<IReadOnlyList<ProxyServer>> GetMyProxiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProxyServer>>([]);

        public Task<ProxySessionDto> CreateProxySessionAsync(string countryCode, CancellationToken cancellationToken) =>
            Task.FromResult(new ProxySessionDto { CountryCode = countryCode, ExpiresAt = DateTimeOffset.UtcNow });
    }
}
