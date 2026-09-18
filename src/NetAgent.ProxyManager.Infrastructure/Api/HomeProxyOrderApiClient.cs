using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class HomeProxyOrderApiClient(
    HttpClient httpClient,
    IOptions<BackendApiOptions> options,
    IAuthService authService) : IProxyOrderApiClient
{
    private const int StaticProxyCategoryTypeId = 1;
    // Backend rejects orders/{...} with 403 for merchant-role accounts — they must purchase
    // through orders/merchant instead. Mirrors the website's `if (user?.role?.id == '2') url += '/merchant'`.
    private const string MerchantRoleId = "2";
    private const string SortJson = """[{"orderBy":"createdAt","order":"desc"}]""";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions PurchaseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly BackendApiOptions _options = options.Value;

    public async Task<ProxyOrderPage> GetProxyOrdersAsync(
        ProxyOrderPageRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildProxyOrdersEndpoint(request));
        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ProxyOrdersResponse>(JsonOptions, cancellationToken);
        return new ProxyOrderPage
        {
            Orders = payload?.Data.Select(MapOrder).ToList() ?? [],
            Total = payload?.Total,
            HasNextPage = payload?.HasNextPage ?? false
        };
    }

    public async Task<HistoryPage<PurchaseHistoryItem>> GetPurchaseHistoryAsync(
        HistoryPageRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildHistoryEndpoint("orders", request));
        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HistoryListResponse<PurchaseHistoryDto>>(JsonOptions, cancellationToken);
        return new HistoryPage<PurchaseHistoryItem>
        {
            Items = payload?.Data.Select(MapPurchaseHistoryItem).ToList() ?? [],
            Total = payload?.Total,
            HasNextPage = payload?.HasNextPage ?? false
        };
    }

    public async Task<HistoryPage<TransactionHistoryItem>> GetTransactionHistoryAsync(
        HistoryPageRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildHistoryEndpoint("transactions", request));
        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HistoryListResponse<TransactionHistoryDto>>(JsonOptions, cancellationToken);
        return new HistoryPage<TransactionHistoryItem>
        {
            Items = payload?.Data.Select(MapTransactionHistoryItem).ToList() ?? [],
            Total = payload?.Total,
            HasNextPage = payload?.HasNextPage ?? false
        };
    }

    public async Task<ProxyProductPage> GetProductsAsync(int categoryTypeId, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildProductsEndpoint(categoryTypeId));
        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(JsonOptions, cancellationToken);
        return new ProxyProductPage
        {
            Products = payload?.Data.Select(MapProduct).ToList() ?? [],
            Total = payload?.Total,
            HasNextPage = payload?.HasNextPage ?? false
        };
    }

    public async Task<ProxyDiscountRules> GetDiscountRulesAsync(CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, Endpoint("discount-rules"));
        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DiscountRulesResponse>(JsonOptions, cancellationToken);
        return new ProxyDiscountRules
        {
            Scope = payload?.Scope ?? string.Empty,
            RulesByCategory = payload?.RulesByCategory.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<ProxyDiscountRule>)pair.Value.Select(MapDiscountRule).ToList(),
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, IReadOnlyList<ProxyDiscountRule>>(StringComparer.OrdinalIgnoreCase)
        };
    }

    public async Task PurchaseProxyAsync(ProxyPurchaseOrder order, CancellationToken cancellationToken)
    {
        var session = await authService.GetSessionAsync(cancellationToken);
        var path = session?.User?.RoleId == MerchantRoleId ? "orders/merchant" : "orders";

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(path))
        {
            Content = JsonContent.Create(order, options: PurchaseJsonOptions)
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ChangeProxyInfoAsync(
        IReadOnlyCollection<int> userProxyIds,
        string username,
        string password,
        ProxyProtocol protocol,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("orders/change-info-proxies"))
        {
            Content = JsonContent.Create(new
            {
                userProxyIds = userProxyIds.ToArray(),
                username,
                password,
                protocol = ToBackendProtocol(protocol),
                //rotateInterval = 0
            }, options: JsonOptions)
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RenewProxiesAsync(
        IReadOnlyCollection<int> userProxyIds,
        int dayOfRenewal,
        int categoryTypeId,
        CancellationToken cancellationToken,
        int? rotateInterval = null,
        bool? isAutoRotate = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["userProxyIds"] = userProxyIds.ToArray(),
            ["dayOfRenewal"] = dayOfRenewal,
            ["isRenewal"] = false,
            ["categoryTypeId"] = categoryTypeId
        };
        if (rotateInterval is not null)
        {
            payload["rotateInterval"] = rotateInterval.Value;
        }

        if (isAutoRotate is not null)
        {
            payload["isAutoRotate"] = isAutoRotate.Value;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("orders/renewal-proxies"))
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ChangeRotateProxyInfoAsync(
        IReadOnlyCollection<int> userProxyIds,
        string password,
        int rotateInterval,
        bool isAutoRotate,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("orders/change-info-proxies"))
        {
            Content = JsonContent.Create(new
            {
                userProxyIds = userProxyIds.ToArray(),
                password,
                rotateInterval,
                isAutoRotate
            }, options: JsonOptions)
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RotateProxiesByIdsAsync(
        IReadOnlyCollection<int> userProxyIds,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("proxies/rotate-by-ids"))
        {
            Content = JsonContent.Create(new
            {
                userproxiesIds = userProxyIds.ToArray()
            }, options: JsonOptions)
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ProxyRotateResult> RotateProxyAsync(
        int userProxyId,
        bool checkOnly,
        CancellationToken cancellationToken)
    {
        var endpoint = Endpoint($"proxies/{userProxyId}/rotate") + (checkOnly ? "?checkOnly=true" : string.Empty);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRotateResultAsync(response, cancellationToken);
    }

    public async Task<string?> GenerateProxyTokenAsync(
        int userProxyId,
        bool isCdk,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            Endpoint($"proxies/{userProxyId}/generate-token", isCdk ? "v2" : "v1"));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GenerateTokenResponse>(JsonOptions, cancellationToken);
        return payload?.Token;
    }

    public string BuildPublicRotateUrl(string token, bool checkOnly)
    {
        var baseUrl = GetPublicRotateBaseUrl();
        var checkOnlyPart = checkOnly ? "&checkOnly=true" : string.Empty;
        return $"{baseUrl}/v3/users/rotatev2?token={Uri.EscapeDataString(token)}{checkOnlyPart}";
    }

    public async Task<ProxyRotateResult> FetchProxyByRotateTokenAsync(
        string token,
        bool checkOnly,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildPublicRotateUrl(token, checkOnly));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRotateResultAsync(response, cancellationToken);
    }

    private string BuildProxyOrdersEndpoint(ProxyOrderPageRequest request)
    {
        var page = Math.Max(1, request.Page);
        var limit = Math.Max(1, request.Limit);
        var filter = BuildFilter(request);

        return $"{Endpoint("users/proxies")}" +
            $"?page={page}" +
            $"&limit={limit}" +
            $"&sort={Uri.EscapeDataString(SortJson)}" +
            $"&filter={Uri.EscapeDataString(filter)}";
    }

    private string BuildHistoryEndpoint(string path, HistoryPageRequest request)
    {
        var page = Math.Max(1, request.Page);
        var limit = Math.Max(1, request.Limit);
        var filter = BuildHistoryFilter(request);
        var query = new List<string>
        {
            $"page={page}",
            $"limit={limit}",
            $"sort={Uri.EscapeDataString(SortJson)}"
        };

        var searchText = request.SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(searchText) &&
            (string.IsNullOrWhiteSpace(request.SearchField) ||
             string.Equals(request.SearchField, HistorySearchFields.All, StringComparison.OrdinalIgnoreCase)))
        {
            query.Add($"s={Uri.EscapeDataString(searchText)}");
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query.Add($"filter={Uri.EscapeDataString(filter)}");
        }

        return $"{Endpoint(path)}?{string.Join('&', query)}";
    }

    private string BuildProductsEndpoint(int categoryTypeId)
    {
        var filters = JsonSerializer.Serialize(new
        {
            category = new
            {
                categorytype = new
                {
                    id = Math.Max(1, categoryTypeId)
                }
            }
        }, JsonOptions);
        return $"{Endpoint("products")}?filters={Uri.EscapeDataString(filters)}";
    }

    private static string BuildFilter(ProxyOrderPageRequest request)
    {
        var filters = new List<string>();
        var searchField = request.SearchField;
        var searchText = request.SearchText;
        if (searchField is not null && !string.IsNullOrWhiteSpace(searchText))
        {
            var field = ResolveSearchField(request, searchField.Value);

            filters.Add($"{field}:$eq:string:{searchText.Trim()}");
        }

        if (request.IsCdk is not null)
        {
            filters.Add($"proxy.isCdk:$eq:boolean:{request.IsCdk.Value.ToString().ToLowerInvariant()}");
        }

        if (request.ProviderIn is { Count: > 0 })
        {
            var providers = request.ProviderIn
                .Where(provider => !string.IsNullOrWhiteSpace(provider))
                .Select(provider => provider.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (providers.Length > 0)
            {
                filters.Add($"ipaddress.provider:$in:string:{string.Join(',', providers)}");
            }
        }

        filters.Add($"categorytype.id:$eq:number:{Math.Max(1, request.CategoryTypeId)}");
        return string.Join(';', filters);
    }

    private static string BuildHistoryFilter(HistoryPageRequest request)
    {
        var filters = new List<string>();
        var statusFilter = request.Status switch
        {
            HistoryStatusFilter.Completed => "status.id:$eq:number:6",
            HistoryStatusFilter.Processing => "status.id:$in:number:3,4",
            HistoryStatusFilter.Cancelled => "status.id:$eq:number:7",
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            filters.Add(statusFilter);
        }

        var searchText = SanitizeFilterValue(request.SearchText).Trim();
        var searchField = request.SearchField?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(searchText) &&
            !string.IsNullOrWhiteSpace(searchField) &&
            !string.Equals(searchField, HistorySearchFields.All, StringComparison.OrdinalIgnoreCase))
        {
            filters.Add($"{searchField}:$eq:string:{searchText}");
        }

        return string.Join(';', filters);
    }

    private static string SanitizeFilterValue(string? value) =>
        (value ?? string.Empty).Replace(';', ' ');

    private static string ResolveSearchField(ProxyOrderPageRequest request, ProxyOrderSearchField searchField) =>
        searchField switch
        {
            ProxyOrderSearchField.Id => "code",
            ProxyOrderSearchField.OrderCode => "ord.code",
            ProxyOrderSearchField.Proxy when request.OrderKind == ProxyOrderKind.Datacenter => "proxy.ipIn",
            ProxyOrderSearchField.Proxy => "ipaddress.ip",
            ProxyOrderSearchField.ProxyDomain => "ipaddress.domain",
            _ => "code"
        };

    private static PurchaseHistoryItem MapPurchaseHistoryItem(PurchaseHistoryDto dto)
    {
        var products = dto.Products ?? [];
        var dayValues = products
            .Select(item => ParseElementString(item.DayOfUse))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PurchaseHistoryItem
        {
            Id = ParseElementString(dto.Id),
            Code = dto.Code ?? ParseElementString(dto.Id),
            Account = FirstNonEmpty(dto.User?.Phone, dto.User?.Email, dto.User?.Name, "-"),
            Quantity = products.Count == 0
                ? 0
                : products.Sum(item =>
                {
                    var quantity = ParseElementInt(item.Quantity);
                    return quantity is > 0 ? quantity.Value : 1;
                }),
            Amount = ParseElementDecimal(dto.Amount),
            DayOfUse = dayValues.Length == 0 ? "-" : string.Join(", ", dayValues),
            TaxCode = dto.TaxCode?.Mst ?? "-",
            StatusId = ParseStatusId(dto.Status),
            StatusName = dto.Status?.Name ?? string.Empty,
            CreatedAt = ParseElementDateTime(dto.CreatedAt)
        };
    }

    private static TransactionHistoryItem MapTransactionHistoryItem(TransactionHistoryDto dto) =>
        new()
        {
            Id = ParseElementString(dto.Id),
            Code = dto.Code ?? ParseElementString(dto.Id),
            Type = ParseElementInt(dto.Type),
            Amount = ParseElementDecimal(dto.Amount),
            Content = dto.Content ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            OrderCode = dto.OrderCode ?? string.Empty,
            StatusId = ParseStatusId(dto.Status),
            StatusName = dto.Status?.Name ?? string.Empty,
            CreatedAt = ParseElementDateTime(dto.TransactionDate) ?? ParseElementDateTime(dto.CreatedAt)
        };

    private ProxyOrder MapOrder(ProxyOrderDto dto)
    {
        var proxy = dto.Proxy;
        var ipAddress = proxy?.IpAddress;

        if (!ProxyProtocolDisplay.TryParse(dto.Protocol ?? proxy?.Protocol ?? string.Empty, out var protocol))
        {
            protocol = ProxyProtocol.Https;
        }

        return new ProxyOrder
        {
            UserProxyId = dto.Id,
            Code = dto.Code ?? string.Empty,
            OrderCode = dto.Order?.Code ?? string.Empty,
            Provider = ipAddress?.Provider ?? string.Empty,
            Ip = SelectProxyIp(proxy, ipAddress),
            Port = proxy?.Port ?? 0,
            Domain = ipAddress?.Domain ?? string.Empty,
            Username = proxy?.Username ?? string.Empty,
            Password = proxy?.Password ?? string.Empty,
            Protocol = protocol,
            PreviousIp = ipAddress?.PrevIp,
            Description = dto.Description,
            ExpiredAt = dto.ExpiredAt is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(dto.ExpiredAt.Value),
            CategoryTypeId = ipAddress?.CategoryType?.Id ?? StaticProxyCategoryTypeId,
            StatusId = ParseStatusId(dto.Status),
            StatusName = dto.Status?.Name ?? string.Empty,
            IsCdk = dto.IsCdk ?? proxy?.IsCdk ?? false,
            RotateInterval = proxy?.RotateInterval ?? 0
        };
    }

    private static ProxyProduct MapProduct(ProductDto dto) =>
        new()
        {
            Sort = dto.Sort,
            Provider = dto.Provider ?? string.Empty,
            ImageUrl = dto.ImageUrl ?? string.Empty,
            Price = dto.Price,
            OriginPrice = dto.OriginPrice,
            Description = dto.Description ?? string.Empty,
            Slug = dto.Slug ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Id = dto.Id ?? string.Empty,
            Category = new ProxyProductCategory
            {
                Unit = dto.Category?.Unit ?? string.Empty,
                Period = dto.Category?.Period ?? string.Empty,
                Description = dto.Category?.Description ?? string.Empty,
                Slug = dto.Category?.Slug ?? string.Empty,
                Name = dto.Category?.Name ?? string.Empty,
                Id = dto.Category?.Id ?? string.Empty,
                CategoryType = new ProxyProductCategoryType
                {
                    Id = dto.Category?.CategoryType?.Id ?? 0,
                    Name = dto.Category?.CategoryType?.Name ?? string.Empty,
                    Slug = dto.Category?.CategoryType?.Slug ?? string.Empty
                }
            }
        };

    private static ProxyDiscountRule MapDiscountRule(DiscountRuleDto dto) =>
        new()
        {
            MinUnit = dto.MinDay,
            MaxUnit = dto.MaxDay,
            Discount = dto.Discount
        };

    private static string SelectProxyIp(OrderProxyDto? proxy, IpAddressDto? ipAddress)
    {
        if (proxy is null)
        {
            return string.Empty;
        }

        var categoryTypeId = ipAddress?.CategoryType?.Id ?? StaticProxyCategoryTypeId;
        var isUsDatacenter =
            categoryTypeId == StaticProxyCategoryTypeId &&
            (string.Equals(ipAddress?.Provider, "US", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(ipAddress?.Location, "US", StringComparison.OrdinalIgnoreCase));

        return isUsDatacenter && !string.IsNullOrWhiteSpace(proxy.IpIn)
            ? proxy.IpIn
            : ipAddress?.Ip ?? string.Empty;
    }

    private async Task<ProxyRotateResult> ReadRotateResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<RotateProxyResponse>(JsonOptions, cancellationToken);
        return new ProxyRotateResult
        {
            Status = payload?.Status ?? string.Empty,
            Proxy = payload?.Proxy ?? string.Empty,
            Ip = payload?.Ip ?? string.Empty,
            Message = payload?.Message ?? string.Empty,
            TimeRemaining = payload?.TimeRemaining,
            Location = payload?.Location ?? string.Empty,
            Provider = payload?.Provider ?? string.Empty,
            LastRotate = payload?.LastRotate ?? string.Empty
        };
    }

    private static int? ParseStatusId(StatusDto? status)
    {
        if (status is null || status.Id.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (status.Id.ValueKind == JsonValueKind.Number && status.Id.TryGetInt32(out var number))
        {
            return number;
        }

        if (status.Id.ValueKind == JsonValueKind.String &&
            int.TryParse(status.Id.GetString(), CultureInfo.InvariantCulture, out var textNumber))
        {
            return textNumber;
        }

        return null;
    }

    private static string ParseElementString(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var integer)
                ? integer.ToString(CultureInfo.InvariantCulture)
                : element.TryGetDecimal(out var number)
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty
        };

    private static int? ParseElementInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return number;
        }

        return element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var textNumber)
                ? textNumber
                : null;
    }

    private static decimal ParseElementDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number))
        {
            return number;
        }

        return element.ValueKind == JsonValueKind.String &&
            decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var textNumber)
                ? textNumber
                : 0m;
    }

    private static DateTimeOffset? ParseElementDateTime(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : null;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var timestamp))
        {
            return null;
        }

        try
        {
            return timestamp > 9_999_999_999
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                : DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private string Endpoint(string path, string? versionOverride = null)
    {
        var version = string.IsNullOrWhiteSpace(versionOverride)
            ? string.IsNullOrWhiteSpace(_options.ApiVersion)
            ? "v1"
            : _options.ApiVersion.Trim().Trim('/')
            : versionOverride.Trim().Trim('/');
        return $"{version}/{path.TrimStart('/')}";
    }

    private string GetPublicRotateBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_options.RegisterDomain))
        {
            var host = _options.RegisterDomain.Trim().TrimEnd('/');
            host = host.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase);
            return $"https://{host}/api";
        }

        return _options.BaseUrl.Trim().TrimEnd('/');
    }

    private static string ToBackendProtocol(ProxyProtocol protocol) =>
        protocol == ProxyProtocol.Socks5 ? "SOCKS" : "HTTP";

    private sealed class ProxyOrdersResponse
    {
        public List<ProxyOrderDto> Data { get; init; } = [];
        public int? Total { get; init; }
        public bool HasNextPage { get; init; }
    }

    private sealed class ProductsResponse
    {
        public List<ProductDto> Data { get; init; } = [];
        public int? Total { get; init; }
        public bool HasNextPage { get; init; }
    }

    private sealed class HistoryListResponse<T>
    {
        public List<T> Data { get; init; } = [];
        public int? Total { get; init; }
        public bool HasNextPage { get; init; }
    }

    private sealed class PurchaseHistoryDto
    {
        public JsonElement Id { get; init; }
        public string? Code { get; init; }
        public JsonElement Amount { get; init; }
        public List<PurchaseHistoryProductDto>? Products { get; init; }
        public HistoryUserDto? User { get; init; }
        public TaxCodeDto? TaxCode { get; init; }
        public StatusDto? Status { get; init; }
        public JsonElement CreatedAt { get; init; }
    }

    private sealed class PurchaseHistoryProductDto
    {
        public JsonElement Quantity { get; init; }
        public JsonElement DayOfUse { get; init; }
    }

    private sealed class TransactionHistoryDto
    {
        public JsonElement Id { get; init; }
        public string? Code { get; init; }
        public JsonElement Type { get; init; }
        public JsonElement Amount { get; init; }
        public string? Content { get; init; }
        public string? Description { get; init; }
        public string? OrderCode { get; init; }
        public StatusDto? Status { get; init; }
        public JsonElement CreatedAt { get; init; }
        public JsonElement TransactionDate { get; init; }
    }

    private sealed class HistoryUserDto
    {
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? Name { get; init; }
    }

    private sealed class TaxCodeDto
    {
        public string? Mst { get; init; }
    }

    private sealed class ProductDto
    {
        public int Sort { get; init; }
        public string? Provider { get; init; }
        public string? ImageUrl { get; init; }
        public decimal Price { get; init; }
        public decimal OriginPrice { get; init; }
        public ProductCategoryDto? Category { get; init; }
        public string? Description { get; init; }
        public string? Slug { get; init; }
        public string? Name { get; init; }
        public string? Id { get; init; }
    }

    private sealed class ProductCategoryDto
    {
        [JsonPropertyName("categorytype")]
        public ProductCategoryTypeDto? CategoryType { get; init; }

        public string? Unit { get; init; }
        public string? Period { get; init; }
        public string? Description { get; init; }
        public string? Slug { get; init; }
        public string? Name { get; init; }
        public string? Id { get; init; }
    }

    private sealed class ProductCategoryTypeDto
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public string? Slug { get; init; }
    }

    private sealed class DiscountRulesResponse
    {
        public string? Scope { get; init; }
        public Dictionary<string, List<DiscountRuleDto>> RulesByCategory { get; init; } = [];
    }

    private sealed class DiscountRuleDto
    {
        public int MinDay { get; init; }
        public int? MaxDay { get; init; }
        public decimal Discount { get; init; }
    }

    private sealed class ProxyOrderDto
    {
        public string? Code { get; init; }
        public string? Description { get; init; }
        public OrderDto? Order { get; init; }
        public string? Protocol { get; init; }
        public OrderProxyDto? Proxy { get; init; }
        public long? ExpiredAt { get; init; }
        public StatusDto? Status { get; init; }
        public bool? IsCdk { get; init; }
        public int Id { get; init; }
    }

    private sealed class OrderDto
    {
        public string Code { get; init; } = string.Empty;
    }

    private sealed class OrderProxyDto
    {
        [JsonPropertyName("ipaddress")]
        public IpAddressDto? IpAddress { get; init; }

        public string? Password { get; init; }
        public string? Username { get; init; }
        public string? Protocol { get; init; }
        public bool? IsCdk { get; init; }
        public string? IpIn { get; init; }
        public int? RotateInterval { get; init; }
        public int Port { get; init; }
    }

    private sealed class IpAddressDto
    {
        public string? Domain { get; init; }
        public string? PrevIp { get; init; }
        public string? Location { get; init; }
        public string? Provider { get; init; }
        public string? Ip { get; init; }

        [JsonPropertyName("categorytype")]
        public CategoryTypeDto? CategoryType { get; init; }
    }

    private sealed class CategoryTypeDto
    {
        public int Id { get; init; }
    }

    private sealed class StatusDto
    {
        public JsonElement Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class GenerateTokenResponse
    {
        public string? Token { get; init; }
    }

    private sealed class RotateProxyResponse
    {
        public string? Status { get; init; }
        public string? Proxy { get; init; }
        public string? Ip { get; init; }
        public string? Message { get; init; }
        public int? TimeRemaining { get; init; }
        public string? Location { get; init; }
        public string? Provider { get; init; }
        public string? LastRotate { get; init; }
    }
}
