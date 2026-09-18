namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxyDiscountRules
{
    public string Scope { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, IReadOnlyList<ProxyDiscountRule>> RulesByCategory { get; init; } =
        new Dictionary<string, IReadOnlyList<ProxyDiscountRule>>(StringComparer.OrdinalIgnoreCase);

    public decimal GetDiscountMultiplier(string categoryId, int unitCount)
    {
        if (string.IsNullOrWhiteSpace(categoryId) ||
            unitCount < 1 ||
            !RulesByCategory.TryGetValue(categoryId, out var rules))
        {
            return 1m;
        }

        var matchingRule = rules.FirstOrDefault(rule => rule.AppliesTo(unitCount));
        return matchingRule is null ? 1m : matchingRule.Discount;
    }
}

public sealed class ProxyDiscountRule
{
    public int MinUnit { get; init; }
    public int? MaxUnit { get; init; }
    public decimal Discount { get; init; }

    public bool AppliesTo(int unitCount) =>
        unitCount >= MinUnit && (MaxUnit is null || unitCount <= MaxUnit.Value);
}
