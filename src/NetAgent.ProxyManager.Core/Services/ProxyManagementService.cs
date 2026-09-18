using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ProxyManagementService(
    IProxyRepository repository,
    IProxyApiClient proxyApiClient,
    IProxyChecker proxyChecker)
{
    public Task<IReadOnlyList<ProxyServer>> GetAllAsync(CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);

    public async Task SaveAllAsync(IReadOnlyCollection<ProxyServer> proxies, CancellationToken cancellationToken) =>
        await repository.SaveAllAsync(proxies, cancellationToken);

    public async Task<IReadOnlyList<ProxyServer>> FetchFromBackendAsync(CancellationToken cancellationToken)
    {
        var existing = (await repository.GetAllAsync(cancellationToken)).ToList();
        var fetched = await proxyApiClient.GetMyProxiesAsync(cancellationToken);

        foreach (var proxy in fetched)
        {
            var existingIndex = existing.FindIndex(p =>
                string.Equals(p.Proxy, proxy.Proxy, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Username, proxy.Username, StringComparison.Ordinal));

            if (existingIndex >= 0)
            {
                existing[existingIndex] = proxy;
            }
            else
            {
                existing.Add(proxy);
            }
        }

        await repository.SaveAllAsync(existing, cancellationToken);
        return existing;
    }

    public async Task CheckAsync(IEnumerable<ProxyServer> proxies, CancellationToken cancellationToken)
    {
        var targets = proxies.ToList();

        await Parallel.ForEachAsync(targets, cancellationToken, async (proxy, ct) =>
        {
            proxy.Status = ProxyStatus.Checking;
            proxy.LatencyMs = null;
            var result = await proxyChecker.CheckAsync(proxy, ct);
            proxy.Status = result.IsReachable ? ProxyStatus.Live : ProxyStatus.Dead;
            proxy.LatencyMs = result.IsReachable ? result.LatencyMs : null;
        });

        var all = (await repository.GetAllAsync(cancellationToken)).ToDictionary(p => p.Id);
        foreach (var proxy in targets)
        {
            all[proxy.Id] = proxy;
        }

        await repository.SaveAllAsync(all.Values.ToList(), cancellationToken);
    }

    public async Task<ProxyServer> UpsertBackendOrderAsync(
        ProxyOrder order,
        ProxyOrderKind kind,
        string? proxyAddress,
        CancellationToken cancellationToken)
    {
        if (!TryCreateProxyServer(order, kind, proxyAddress, out var backendProxy))
        {
            throw new InvalidOperationException("Backend order does not contain a valid proxy endpoint.");
        }

        var proxies = (await repository.GetAllAsync(cancellationToken)).ToList();
        var existingIndex = proxies.FindIndex(proxy =>
            proxy.BackendUserProxyId == order.UserProxyId ||
            (proxy.BackendUserProxyId is null &&
             string.Equals(proxy.Proxy, backendProxy.Proxy, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(proxy.Username ?? string.Empty, backendProxy.Username ?? string.Empty, StringComparison.Ordinal)));

        if (existingIndex >= 0)
        {
            var existing = proxies[existingIndex];
            existing.Proxy = backendProxy.Proxy;
            existing.Protocol = backendProxy.Protocol;
            existing.Username = backendProxy.Username;
            existing.Password = backendProxy.Password;
            existing.IsFromBackend = true;
            existing.BackendUserProxyId = order.UserProxyId;
            existing.BackendOrderKind = kind;
            existing.BackendProvider = backendProxy.BackendProvider;
            existing.ExpiredAt = backendProxy.ExpiredAt;
            proxies[existingIndex] = existing;
            await repository.SaveAllAsync(proxies, cancellationToken);
            return existing;
        }

        proxies.Add(backendProxy);
        await repository.SaveAllAsync(proxies, cancellationToken);
        return backendProxy;
    }

    private static bool TryCreateProxyServer(
        ProxyOrder order,
        ProxyOrderKind kind,
        string? proxyAddress,
        out ProxyServer proxy)
    {
        proxy = new ProxyServer
        {
            Protocol = order.Protocol,
            IsFromBackend = true,
            BackendUserProxyId = order.UserProxyId,
            BackendOrderKind = kind,
            BackendProvider = order.Provider,
            ExpiredAt = order.ExpiredAt
        };

        var value = string.IsNullOrWhiteSpace(proxyAddress) ? order.ProxyAddress : proxyAddress;
        var segments = value.Split(':', StringSplitOptions.TrimEntries);
        if (segments.Length < 2 || !int.TryParse(segments[1], out var port))
        {
            return false;
        }

        proxy.Proxy = $"{segments[0]}:{port}";
        if (segments.Length >= 4)
        {
            proxy.Username = segments[2];
            proxy.Password = segments[3];
        }
        else
        {
            proxy.Username = order.Username;
            proxy.Password = order.Password;
        }

        return proxy.HasValidEndpoint;
    }
}
