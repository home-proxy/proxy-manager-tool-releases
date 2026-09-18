namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class BackendApiOptions
{
    public string Mode { get; set; } = "Http";
    public string BaseUrl { get; set; } = "https://api.homeproxy.vn/api";
    public string ApiVersion { get; set; } = "v1";
    public string MerchantId { get; set; } = string.Empty;
    public string RegisterDomain { get; set; } = "app.homeproxy.vn";
    public string PortalBaseUrl { get; set; } = "https://app.homeproxy.vn";
    public string RegisterRefClickId { get; set; } = "00000000-0000-4000-8000-000000000001";
    public int TimeoutSeconds { get; set; } = 20;
}
