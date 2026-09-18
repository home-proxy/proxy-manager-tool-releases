using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxyOrderCacheService
{
    Task<ProxyOrderPage> GetPageAsync(
        ProxyOrderPageRequest request,
        bool forceRefresh,
        CancellationToken cancellationToken);

    Task<ProxyProductPage> GetProductsAsync(
        int categoryTypeId,
        bool forceRefresh,
        CancellationToken cancellationToken);

    Task<ProxyDiscountRules> GetDiscountRulesAsync(
        bool forceRefresh,
        CancellationToken cancellationToken);

    void Invalidate(ProxyOrderPageRequest request);

    void InvalidateCategory(int categoryTypeId, bool? isCdk);

    void InvalidateProducts();

    void Clear();

    void UpdateOrderInfo(int userProxyId, string username, string password, ProxyProtocol protocol);

    ProxyOrderCheckState? GetCheckState(ProxyOrder order);

    void SetCheckState(ProxyOrder order, ProxyStatus status, int? latencyMs);
}
