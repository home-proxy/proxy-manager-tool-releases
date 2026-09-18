using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ProxyOrderCacheService(IProxyOrderApiClient proxyOrderApiClient) : IProxyOrderCacheService
{
    private readonly object _gate = new();
    private readonly Dictionary<int, ProxyProductPage> _productPages = [];
    private readonly Dictionary<int, ProxyOrderCheckState> _checksByUserProxyId = [];
    private readonly Dictionary<EndpointCacheKey, ProxyOrderCheckState> _checksByEndpoint = [];
    private ProxyDiscountRules? _discountRules;

    public async Task<ProxyOrderPage> GetPageAsync(
        ProxyOrderPageRequest request,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var fresh = await proxyOrderApiClient.GetProxyOrdersAsync(request, cancellationToken);
        return ClonePage(fresh);
    }

    public async Task<ProxyProductPage> GetProductsAsync(
        int categoryTypeId,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var key = Math.Max(1, categoryTypeId);
        if (!forceRefresh)
        {
            lock (_gate)
            {
                if (_productPages.TryGetValue(key, out var cached))
                {
                    return CloneProductPage(cached);
                }
            }
        }

        var fresh = await proxyOrderApiClient.GetProductsAsync(key, cancellationToken);
        lock (_gate)
        {
            _productPages[key] = CloneProductPage(fresh);
        }

        return CloneProductPage(fresh);
    }

    public async Task<ProxyDiscountRules> GetDiscountRulesAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!forceRefresh)
        {
            lock (_gate)
            {
                if (_discountRules is not null)
                {
                    return CloneDiscountRules(_discountRules);
                }
            }
        }

        var fresh = await proxyOrderApiClient.GetDiscountRulesAsync(cancellationToken);
        lock (_gate)
        {
            _discountRules = CloneDiscountRules(fresh);
        }

        return CloneDiscountRules(fresh);
    }

    public void Invalidate(ProxyOrderPageRequest request)
    {
    }

    public void InvalidateCategory(int categoryTypeId, bool? isCdk)
    {
    }

    public void InvalidateProducts()
    {
        lock (_gate)
        {
            _productPages.Clear();
            _discountRules = null;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _productPages.Clear();
            _discountRules = null;
            _checksByUserProxyId.Clear();
            _checksByEndpoint.Clear();
        }
    }

    public void UpdateOrderInfo(int userProxyId, string username, string password, ProxyProtocol protocol)
    {
    }

    public ProxyOrderCheckState? GetCheckState(ProxyOrder order)
    {
        lock (_gate)
        {
            if (_checksByUserProxyId.TryGetValue(order.UserProxyId, out var byId))
            {
                return byId;
            }

            var endpointKey = EndpointCacheKey.From(order);
            return endpointKey is not null && _checksByEndpoint.TryGetValue(endpointKey, out var byEndpoint)
                ? byEndpoint
                : null;
        }
    }

    public void SetCheckState(ProxyOrder order, ProxyStatus status, int? latencyMs)
    {
        var state = new ProxyOrderCheckState(status, latencyMs);
        lock (_gate)
        {
            _checksByUserProxyId[order.UserProxyId] = state;
            var endpointKey = EndpointCacheKey.From(order);
            if (endpointKey is not null)
            {
                _checksByEndpoint[endpointKey] = state;
            }
        }
    }

    private static ProxyOrderPage ClonePage(ProxyOrderPage page) =>
        new()
        {
            Total = page.Total,
            HasNextPage = page.HasNextPage,
            Orders = page.Orders.Select(CloneOrder).ToList()
        };

    private static ProxyOrder CloneOrder(ProxyOrder order) =>
        new()
        {
            UserProxyId = order.UserProxyId,
            Code = order.Code,
            OrderCode = order.OrderCode,
            Provider = order.Provider,
            Ip = order.Ip,
            Port = order.Port,
            Domain = order.Domain,
            Username = order.Username,
            Password = order.Password,
            Protocol = order.Protocol,
            PreviousIp = order.PreviousIp,
            Description = order.Description,
            ExpiredAt = order.ExpiredAt,
            CategoryTypeId = order.CategoryTypeId,
            StatusId = order.StatusId,
            StatusName = order.StatusName,
            IsCdk = order.IsCdk,
            RotateInterval = order.RotateInterval
        };

    private static ProxyProductPage CloneProductPage(ProxyProductPage page) =>
        new()
        {
            Total = page.Total,
            HasNextPage = page.HasNextPage,
            Products = page.Products.Select(CloneProduct).ToList()
        };

    private static ProxyProduct CloneProduct(ProxyProduct product) =>
        new()
        {
            Sort = product.Sort,
            Provider = product.Provider,
            ImageUrl = product.ImageUrl,
            Price = product.Price,
            OriginPrice = product.OriginPrice,
            Description = product.Description,
            Slug = product.Slug,
            Name = product.Name,
            Id = product.Id,
            Category = new ProxyProductCategory
            {
                Unit = product.Category.Unit,
                Period = product.Category.Period,
                Description = product.Category.Description,
                Slug = product.Category.Slug,
                Name = product.Category.Name,
                Id = product.Category.Id,
                CategoryType = new ProxyProductCategoryType
                {
                    Id = product.Category.CategoryType.Id,
                    Name = product.Category.CategoryType.Name,
                    Slug = product.Category.CategoryType.Slug
                }
            }
        };

    private static ProxyDiscountRules CloneDiscountRules(ProxyDiscountRules rules) =>
        new()
        {
            Scope = rules.Scope,
            RulesByCategory = rules.RulesByCategory.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<ProxyDiscountRule>)pair.Value
                    .Select(rule => new ProxyDiscountRule
                    {
                        MinUnit = rule.MinUnit,
                        MaxUnit = rule.MaxUnit,
                        Discount = rule.Discount
                    })
                    .ToList(),
                StringComparer.OrdinalIgnoreCase)
        };

    private sealed record EndpointCacheKey(string Host, int Port, string Username, ProxyProtocol Protocol)
    {
        public static EndpointCacheKey? From(ProxyOrder order)
        {
            if (string.IsNullOrWhiteSpace(order.Ip) || order.Port <= 0)
            {
                return null;
            }

            return new EndpointCacheKey(
                order.Ip.Trim().ToLowerInvariant(),
                order.Port,
                order.Username.Trim(),
                order.Protocol);
        }
    }
}
