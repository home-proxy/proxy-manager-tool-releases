namespace NetAgent.ProxyManager.Core.Models;

public sealed class AuthUser
{
    public string Id { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public long? Coin { get; set; }
    /// <summary>
    /// Backend role id (e.g. "2" = merchant/reseller account). Determines whether purchases must
    /// go through the merchant-only order endpoint (see HomeProxyOrderApiClient.PurchaseProxyAsync).
    /// </summary>
    public string? RoleId { get; set; }

    public string DisplayName
    {
        get
        {
            var fullName = $"{FirstName} {LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            return !string.IsNullOrWhiteSpace(UserName)
                ? UserName
                : Phone ?? Email ?? Id;
        }
    }
}
