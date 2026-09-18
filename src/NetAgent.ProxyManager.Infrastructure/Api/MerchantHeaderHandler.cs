using Microsoft.Extensions.Options;

namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class MerchantHeaderHandler(IOptions<BackendApiOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var merchantId = options.Value.MerchantId;
        if (!string.IsNullOrWhiteSpace(merchantId) && !request.Headers.Contains("x-merchant-id"))
        {
            request.Headers.TryAddWithoutValidation("x-merchant-id", merchantId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
