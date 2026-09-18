using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProxyOrderCacheServiceTests
{
    [Fact]
    public async Task GetPageAsync_ShouldFetchFreshPageEveryTime()
    {
        var api = new CountingProxyOrderApiClient();
        var service = new ProxyOrderCacheService(api);
        var request = new ProxyOrderPageRequest { Page = 1, Limit = 20, CategoryTypeId = 2, IsCdk = false };

        var first = await service.GetPageAsync(request, forceRefresh: false, CancellationToken.None);
        var second = await service.GetPageAsync(request, forceRefresh: false, CancellationToken.None);
        var third = await service.GetPageAsync(request, forceRefresh: true, CancellationToken.None);

        api.GetPageCallCount.Should().Be(3);
        first.Orders[0].Code.Should().Be("CALL1");
        second.Orders[0].Code.Should().Be("CALL2");
        third.Orders[0].Code.Should().Be("CALL3");
    }

    [Fact]
    public async Task GetPageAsync_ShouldForwardEveryRequestToApi()
    {
        var api = new CountingProxyOrderApiClient();
        var service = new ProxyOrderCacheService(api);
        var baseRequest = new ProxyOrderPageRequest { Page = 1, Limit = 20, CategoryTypeId = 1 };
        var datacenterRequest = new ProxyOrderPageRequest
        {
            Page = 1,
            Limit = 20,
            CategoryTypeId = 1,
            ProviderIn = ["US", "CMC"]
        };

        var first = await service.GetPageAsync(baseRequest, forceRefresh: false, CancellationToken.None);
        var second = await service.GetPageAsync(datacenterRequest, forceRefresh: false, CancellationToken.None);
        var third = await service.GetPageAsync(datacenterRequest, forceRefresh: false, CancellationToken.None);

        api.GetPageCallCount.Should().Be(3);
        first.Orders[0].Code.Should().Be("CALL1");
        second.Orders[0].Code.Should().Be("CALL2");
        third.Orders[0].Code.Should().Be("CALL3");
        api.PageRequests.Should().HaveCount(3);
        api.PageRequests[0].ProviderIn.Should().BeNull();
        api.PageRequests[1].ProviderIn.Should().Equal("US", "CMC");
        api.PageRequests[2].ProviderIn.Should().Equal("US", "CMC");
    }

    [Fact]
    public async Task UpdateOrderInfo_ShouldNotMaskFreshBackendPages()
    {
        var api = new CountingProxyOrderApiClient();
        var service = new ProxyOrderCacheService(api);
        var request = new ProxyOrderPageRequest { Page = 1, Limit = 20, CategoryTypeId = 1 };

        await service.GetPageAsync(request, forceRefresh: false, CancellationToken.None);
        service.UpdateOrderInfo(123, "new-user", "new-pass", ProxyProtocol.Socks5);
        var fresh = await service.GetPageAsync(request, forceRefresh: false, CancellationToken.None);

        fresh.Orders[0].Code.Should().Be("CALL2");
        fresh.Orders[0].Username.Should().Be("user");
        fresh.Orders[0].Password.Should().Be("pass");
        fresh.Orders[0].Protocol.Should().Be(ProxyProtocol.Https);
        api.GetPageCallCount.Should().Be(2);
    }

    [Fact]
    public void CheckState_ShouldBeSharedByOrderIdAndEndpoint()
    {
        var service = new ProxyOrderCacheService(new CountingProxyOrderApiClient());
        var order = CreateOrder();
        var sameEndpointDifferentId = CreateOrder(userProxyId: 456);

        service.SetCheckState(order, ProxyStatus.Live, 42);

        service.GetCheckState(order).Should().Be(new ProxyOrderCheckState(ProxyStatus.Live, 42));
        service.GetCheckState(sameEndpointDifferentId).Should().Be(new ProxyOrderCheckState(ProxyStatus.Live, 42));
    }

    [Fact]
    public async Task Clear_ShouldRemoveCachedPagesAndCheckStates()
    {
        var api = new CountingProxyOrderApiClient();
        var service = new ProxyOrderCacheService(api);
        var request = new ProxyOrderPageRequest { Page = 1, Limit = 20, CategoryTypeId = 1 };
        var order = CreateOrder();

        await service.GetPageAsync(request, forceRefresh: false, CancellationToken.None);
        service.SetCheckState(order, ProxyStatus.Live, 42);

        service.Clear();
        var fresh = await service.GetPageAsync(request, forceRefresh: false, CancellationToken.None);

        api.GetPageCallCount.Should().Be(2);
        fresh.Orders[0].Code.Should().Be("CALL2");
        service.GetCheckState(order).Should().BeNull();
    }

    [Fact]
    public async Task GetProductsAsync_ShouldReuseCachedProductsUntilForceRefresh()
    {
        var api = new CountingProxyOrderApiClient();
        var service = new ProxyOrderCacheService(api);

        var first = await service.GetProductsAsync(1, forceRefresh: false, CancellationToken.None);
        var second = await service.GetProductsAsync(1, forceRefresh: false, CancellationToken.None);
        var third = await service.GetProductsAsync(1, forceRefresh: true, CancellationToken.None);

        api.GetProductsCallCount.Should().Be(2);
        first.Products[0].Name.Should().Be("Product 1");
        second.Products[0].Name.Should().Be("Product 1");
        third.Products[0].Name.Should().Be("Product 2");
    }

    [Fact]
    public async Task GetDiscountRulesAsync_ShouldReuseCachedRulesUntilInvalidated()
    {
        var api = new CountingProxyOrderApiClient();
        var service = new ProxyOrderCacheService(api);

        var first = await service.GetDiscountRulesAsync(forceRefresh: false, CancellationToken.None);
        var second = await service.GetDiscountRulesAsync(forceRefresh: false, CancellationToken.None);
        service.InvalidateProducts();
        var third = await service.GetDiscountRulesAsync(forceRefresh: false, CancellationToken.None);

        api.GetDiscountRulesCallCount.Should().Be(2);
        first.Scope.Should().Be("discount-1");
        second.Scope.Should().Be("discount-1");
        third.Scope.Should().Be("discount-2");
    }

    private static ProxyOrder CreateOrder(int userProxyId = 123, string? code = null) =>
        new()
        {
            UserProxyId = userProxyId,
            Code = code ?? $"CALL{userProxyId}",
            Ip = "127.0.0.1",
            Port = 8080,
            Username = "user",
            Password = "pass",
            Protocol = ProxyProtocol.Https,
            CategoryTypeId = 1
        };

    private sealed class CountingProxyOrderApiClient : IProxyOrderApiClient
    {
        public int GetPageCallCount { get; private set; }
        public int GetProductsCallCount { get; private set; }
        public int GetDiscountRulesCallCount { get; private set; }
        public List<ProxyOrderPageRequest> PageRequests { get; } = [];

        public Task<ProxyOrderPage> GetProxyOrdersAsync(ProxyOrderPageRequest request, CancellationToken cancellationToken)
        {
            GetPageCallCount++;
            PageRequests.Add(request);
            return Task.FromResult(new ProxyOrderPage
            {
                Orders = [CreateOrder(code: $"CALL{GetPageCallCount}")],
                Total = 1,
                HasNextPage = false
            });
        }

        public Task<HistoryPage<PurchaseHistoryItem>> GetPurchaseHistoryAsync(HistoryPageRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new HistoryPage<PurchaseHistoryItem>());

        public Task<HistoryPage<TransactionHistoryItem>> GetTransactionHistoryAsync(HistoryPageRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new HistoryPage<TransactionHistoryItem>());

        public Task<ProxyProductPage> GetProductsAsync(int categoryTypeId, CancellationToken cancellationToken)
        {
            GetProductsCallCount++;
            return Task.FromResult(new ProxyProductPage
            {
                Products =
                [
                    new ProxyProduct
                    {
                        Name = $"Product {GetProductsCallCount}",
                        Id = $"product-{GetProductsCallCount}",
                        Provider = "VNPT",
                        Category = new ProxyProductCategory
                        {
                            Id = "category-id",
                            CategoryType = new ProxyProductCategoryType { Id = categoryTypeId }
                        }
                    }
                ],
                Total = 1,
                HasNextPage = false
            });
        }

        public Task<ProxyDiscountRules> GetDiscountRulesAsync(CancellationToken cancellationToken)
        {
            GetDiscountRulesCallCount++;
            return Task.FromResult(new ProxyDiscountRules
            {
                Scope = $"discount-{GetDiscountRulesCallCount}"
            });
        }

        public Task PurchaseProxyAsync(ProxyPurchaseOrder order, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ChangeProxyInfoAsync(IReadOnlyCollection<int> userProxyIds, string username, string password, ProxyProtocol protocol, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RenewProxiesAsync(
            IReadOnlyCollection<int> userProxyIds,
            int dayOfRenewal,
            int categoryTypeId,
            CancellationToken cancellationToken,
            int? rotateInterval = null,
            bool? isAutoRotate = null) =>
            Task.CompletedTask;

        public Task ChangeRotateProxyInfoAsync(IReadOnlyCollection<int> userProxyIds, string password, int rotateInterval, bool isAutoRotate, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RotateProxiesByIdsAsync(IReadOnlyCollection<int> userProxyIds, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ProxyRotateResult> RotateProxyAsync(int userProxyId, bool checkOnly, CancellationToken cancellationToken) =>
            Task.FromResult(new ProxyRotateResult());

        public Task<string?> GenerateProxyTokenAsync(int userProxyId, bool isCdk, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public string BuildPublicRotateUrl(string token, bool checkOnly) => token;

        public Task<ProxyRotateResult> FetchProxyByRotateTokenAsync(string token, bool checkOnly, CancellationToken cancellationToken) =>
            Task.FromResult(new ProxyRotateResult());
    }
}
