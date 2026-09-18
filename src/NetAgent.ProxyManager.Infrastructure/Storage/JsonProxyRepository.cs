using System.Text.Json;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Storage;

public sealed class JsonProxyRepository : IProxyRepository
{
    private readonly AppDataPaths _paths;
    private readonly ICredentialProtector _protector;
    private readonly AuthenticatedUserStorageScope _userStorageScope;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonProxyRepository(AppDataPaths paths, ICredentialProtector protector)
        : this(paths, protector, authService: null)
    {
    }

    public JsonProxyRepository(AppDataPaths paths, ICredentialProtector protector, IAuthService? authService)
    {
        _paths = paths;
        _protector = protector;
        _userStorageScope = new AuthenticatedUserStorageScope(authService);
    }

    public async Task<IReadOnlyList<ProxyServer>> GetAllAsync(CancellationToken cancellationToken)
    {
        var sharedManualRecords = await ReadRecordsAsync(_paths.ProxiesFilePath, cancellationToken);
        var userRecords = await ReadRecordsAsync(await GetUserFilePathAsync(cancellationToken), cancellationToken);
        var manualRecords = sharedManualRecords.Count > 0
            ? sharedManualRecords.Where(record => !record.IsFromBackend)
            : userRecords.Where(record => !record.IsFromBackend);
        var backendRecords = userRecords.Where(record => record.IsFromBackend);

        return manualRecords
            .Concat(backendRecords)
            .Select(ToProxyServer)
            .ToList();
    }

    public async Task SaveAllAsync(IReadOnlyCollection<ProxyServer> proxies, CancellationToken cancellationToken)
    {
        var manualRecords = proxies
            .Where(proxy => !proxy.IsFromBackend)
            .Select(ToStorageRecord)
            .ToList();
        var backendRecords = proxies
            .Where(proxy => proxy.IsFromBackend)
            .Select(ToStorageRecord)
            .ToList();

        _paths.EnsureDirectories();
        await WriteRecordsAsync(_paths.ProxiesFilePath, manualRecords, cancellationToken);
        await WriteRecordsAsync(await GetUserFilePathAsync(cancellationToken), backendRecords, cancellationToken);
    }

    private async Task<string> GetUserFilePathAsync(CancellationToken cancellationToken)
    {
        var storageKey = await _userStorageScope.GetStorageKeyAsync(cancellationToken);
        _paths.EnsureUserDirectory(storageKey);
        return _paths.GetUserProxiesFilePath(storageKey);
    }

    private async Task<List<ProxyStorageRecord>> ReadRecordsAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<ProxyStorageRecord>>(stream, SerializerOptions, cancellationToken) ?? [];
    }

    private static async Task WriteRecordsAsync(
        string filePath,
        IReadOnlyCollection<ProxyStorageRecord> records,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, records, SerializerOptions, cancellationToken);
    }

    private ProxyServer ToProxyServer(ProxyStorageRecord record) =>
        new()
        {
            Id = record.Id,
            Proxy = ResolveProxy(record),
            Protocol = NormalizeProtocol(record.Protocol),
            Username = record.Username,
            Password = string.IsNullOrWhiteSpace(record.ProtectedPassword) ? null : _protector.Unprotect(record.ProtectedPassword),
            Status = record.Status,
            LatencyMs = record.LatencyMs,
            IsFromBackend = record.IsFromBackend,
            BackendUserProxyId = record.BackendUserProxyId,
            BackendOrderKind = record.BackendOrderKind,
            BackendProvider = record.BackendProvider,
            ExpiredAt = record.ExpiredAt ?? record.ExpiresAt
        };

    private ProxyStorageRecord ToStorageRecord(ProxyServer proxy) =>
        new()
        {
            Id = proxy.Id,
            Proxy = proxy.Proxy,
            Protocol = JsonSerializer.SerializeToElement(proxy.Protocol.ToProxifierProfileType()),
            Username = proxy.Username,
            ProtectedPassword = string.IsNullOrWhiteSpace(proxy.Password) ? null : _protector.Protect(proxy.Password),
            Status = proxy.Status,
            LatencyMs = proxy.LatencyMs,
            IsFromBackend = proxy.IsFromBackend,
            BackendUserProxyId = proxy.BackendUserProxyId,
            BackendOrderKind = proxy.BackendOrderKind,
            BackendProvider = proxy.BackendProvider,
            ExpiredAt = proxy.ExpiredAt
        };

    private static string ResolveProxy(ProxyStorageRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.Proxy))
        {
            return record.Proxy;
        }

        if (!string.IsNullOrWhiteSpace(record.Host) && record.Port > 0)
        {
            return $"{record.Host}:{record.Port}";
        }

        return string.Empty;
    }

    private static ProxyProtocol NormalizeProtocol(JsonElement protocol)
    {
        if (protocol.ValueKind == JsonValueKind.Number &&
            protocol.TryGetInt32(out var numericProtocol) &&
            Enum.IsDefined(typeof(ProxyProtocol), numericProtocol))
        {
            return (ProxyProtocol)numericProtocol;
        }

        if (protocol.ValueKind == JsonValueKind.String &&
            ProxyProtocolDisplay.TryParse(protocol.GetString() ?? string.Empty, out var parsedProtocol))
        {
            return parsedProtocol;
        }

        return ProxyProtocol.Socks5;
    }

    private sealed class ProxyStorageRecord
    {
        public Guid Id { get; init; }
        public string Proxy { get; init; } = string.Empty;
        public JsonElement Protocol { get; init; }
        public string? Username { get; init; }
        public string? ProtectedPassword { get; init; }
        public ProxyStatus Status { get; init; }
        public int? LatencyMs { get; init; }
        public bool IsFromBackend { get; init; }
        public int? BackendUserProxyId { get; init; }
        public ProxyOrderKind? BackendOrderKind { get; init; }
        public string? BackendProvider { get; init; }
        public DateTimeOffset? ExpiredAt { get; init; }

        // Legacy fields kept only so older local JSON can be read and migrated on next save.
        public string? Label { get; init; }
        public string? Host { get; init; }
        public int Port { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }
    }
}
