using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxyOrderApiClient
{
    Task<ProxyOrderPage> GetProxyOrdersAsync(ProxyOrderPageRequest request, CancellationToken cancellationToken);

    Task<HistoryPage<PurchaseHistoryItem>> GetPurchaseHistoryAsync(
        HistoryPageRequest request,
        CancellationToken cancellationToken);

    Task<HistoryPage<TransactionHistoryItem>> GetTransactionHistoryAsync(
        HistoryPageRequest request,
        CancellationToken cancellationToken);

    Task<ProxyProductPage> GetProductsAsync(int categoryTypeId, CancellationToken cancellationToken);

    Task<ProxyDiscountRules> GetDiscountRulesAsync(CancellationToken cancellationToken);

    Task PurchaseProxyAsync(ProxyPurchaseOrder order, CancellationToken cancellationToken);

    Task ChangeProxyInfoAsync(
        IReadOnlyCollection<int> userProxyIds,
        string username,
        string password,
        ProxyProtocol protocol,
        CancellationToken cancellationToken);

    Task RenewProxiesAsync(
        IReadOnlyCollection<int> userProxyIds,
        int dayOfRenewal,
        int categoryTypeId,
        CancellationToken cancellationToken,
        int? rotateInterval = null,
        bool? isAutoRotate = null);

    Task ChangeRotateProxyInfoAsync(
        IReadOnlyCollection<int> userProxyIds,
        string password,
        int rotateInterval,
        bool isAutoRotate,
        CancellationToken cancellationToken);

    Task RotateProxiesByIdsAsync(
        IReadOnlyCollection<int> userProxyIds,
        CancellationToken cancellationToken);

    Task<ProxyRotateResult> RotateProxyAsync(
        int userProxyId,
        bool checkOnly,
        CancellationToken cancellationToken);

    Task<string?> GenerateProxyTokenAsync(
        int userProxyId,
        bool isCdk,
        CancellationToken cancellationToken);

    string BuildPublicRotateUrl(string token, bool checkOnly);

    Task<ProxyRotateResult> FetchProxyByRotateTokenAsync(
        string token,
        bool checkOnly,
        CancellationToken cancellationToken);
}
