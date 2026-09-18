namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxyProduct
{
    public int Sort { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal OriginPrice { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public ProxyProductCategory Category { get; init; } = new();

    public ProxyProductKind Kind
    {
        get
        {
            if (Category.CategoryType.Id == 2)
            {
                return ProxyProductKind.Rotate;
            }

            return IsDatacenterProvider(Provider)
                ? ProxyProductKind.Datacenter
                : ProxyProductKind.Static;
        }
    }

    public static bool IsDatacenterProvider(string provider) =>
        string.Equals(provider, "US", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, "CMC", StringComparison.OrdinalIgnoreCase);
}

public sealed class ProxyProductCategory
{
    public ProxyProductCategoryType CategoryType { get; init; } = new();
    public string Unit { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
}

public sealed class ProxyProductCategoryType
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
}

public sealed class ProxyProductPage
{
    public IReadOnlyList<ProxyProduct> Products { get; init; } = [];
    public int? Total { get; init; }
    public bool HasNextPage { get; init; }
}
