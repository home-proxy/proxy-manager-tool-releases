using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class HomeProxyDepositApiClient(HttpClient httpClient, IOptions<BackendApiOptions> options) : IDepositApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly BackendApiOptions _options = options.Value;

    public async Task<DepositTransaction> CreateDepositTransactionAsync(
        long amount,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("transactions"))
        {
            Content = JsonContent.Create(new { amount }, options: JsonOptions)
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadTransactionAsync(response, cancellationToken);
    }

    public async Task<DepositTransaction> GetDepositTransactionAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Transaction id is required.", nameof(id));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            Endpoint($"transactions/{Uri.EscapeDataString(id.Trim())}"));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadTransactionAsync(response, cancellationToken);
    }

    private async Task<DepositTransaction> ReadTransactionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = UnwrapData(document.RootElement);

        return new DepositTransaction
        {
            Id = FirstNonEmpty(ReadString(root, "id"), ReadString(root, "code")),
            Amount = ReadInt64(root, "amount"),
            AccountName = ReadString(root, "accountName"),
            AccountNumber = FirstNonEmpty(
                ReadString(root, "accountNumber"),
                ReadString(root, "bankAccountNumber")),
            BankName = ReadString(root, "bankName"),
            BankCode = ReadString(root, "bankCode"),
            Description = FirstNonEmpty(
                ReadString(root, "description"),
                ReadString(root, "content"),
                ReadString(root, "code")),
            QrCode = FirstNonEmpty(ReadString(root, "qrCode"), ReadString(root, "qr")),
            StatusId = ReadStatusId(root),
            StatusName = ReadStatusName(root),
            ExpiredAt = ReadDateTime(root, "expiredAt") ?? ReadDateTime(root, "expiredDate"),
            ExpiresAt = ReadDateTime(root, "expiresAt") ?? ReadDateTime(root, "expiresDate")
        };
    }

    private string Endpoint(string path)
    {
        var version = string.IsNullOrWhiteSpace(_options.ApiVersion)
            ? "v1"
            : _options.ApiVersion.Trim().Trim('/');
        return $"{version}/{path.TrimStart('/')}";
    }

    private static JsonElement UnwrapData(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object)
        {
            return data;
        }

        return root;
    }

    private static int? ReadStatusId(JsonElement root)
    {
        if (!root.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.Object ||
            !status.TryGetProperty("id", out var id))
        {
            return ReadInt32(root, "statusId");
        }

        return ReadInt32(id);
    }

    private static string ReadStatusName(JsonElement root)
    {
        if (root.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.Object &&
            status.TryGetProperty("name", out var name))
        {
            return ReadString(name);
        }

        return ReadString(root, "statusName");
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        return ReadString(value);
    }

    private static string ReadString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.TryGetInt64(out var integer)
                ? integer.ToString(CultureInfo.InvariantCulture)
                : value.TryGetDecimal(out var number)
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty
        };

    private static int? ReadInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return ReadInt32(value);
    }

    private static int? ReadInt32(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var textNumber)
                ? textNumber
                : null;
    }

    private static long ReadInt64(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
            long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var textNumber)
                ? textNumber
                : 0;
    }

    private static DateTimeOffset? ReadDateTime(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var timestamp))
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

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
