using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class HomeProxyAuthApiClient(
    HttpClient httpClient,
    IOptions<BackendApiOptions> options) : IAuthApiClient
{
    private readonly BackendApiOptions _options = options.Value;

    public async Task<AuthSession> LoginAsync(string phone, string password, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            Endpoint("auth/login"),
            new { phone, password },
            cancellationToken);
        await EnsureAuthSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        return payload?.ToSession() ?? throw new InvalidOperationException("Login returned an empty payload.");
    }

    public async Task RegisterAsync(AuthRegisterRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            Endpoint("auth/register"),
            new
            {
                email = request.Email.Trim(),
                password = request.Password,
                firstName = request.FirstName.Trim(),
                lastName = request.LastName.Trim(),
                phone = request.Phone.Trim(),
                refClickId = _options.RegisterRefClickId,
                domain = _options.RegisterDomain
            },
            cancellationToken);
        await EnsureAuthSuccessAsync(response, cancellationToken);
    }

    public async Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("auth/refresh"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureAuthSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        return payload?.ToSession() ?? throw new InvalidOperationException("Refresh returned an empty payload.");
    }

    public async Task<AuthUser> GetMeAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint("auth/me"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureAuthSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken);
        return payload?.ToUser() ?? throw new InvalidOperationException("Current-user endpoint returned an empty payload.");
    }

    public async Task PatchMeAsync(
        AuthUpdateRequest request,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, Endpoint("auth/me"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Content = JsonContent.Create(BuildUpdatePayload(request));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureAuthSuccessAsync(response, cancellationToken);
    }

    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("auth/logout"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureAuthSuccessAsync(response, cancellationToken);
    }

    private string Endpoint(string path)
    {
        var version = string.IsNullOrWhiteSpace(_options.ApiVersion)
            ? "v1"
            : _options.ApiVersion.Trim().Trim('/');
        return $"{version}/{path.TrimStart('/')}";
    }

    private static Dictionary<string, object?> BuildUpdatePayload(AuthUpdateRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["firstName"] = request.FirstName.Trim(),
            ["lastName"] = request.LastName.Trim(),
            ["email"] = request.Email.Trim(),
            ["phone"] = request.Phone.Trim(),
            ["userName"] = request.UserName,
            ["gender"] = string.IsNullOrWhiteSpace(request.Gender) ? "male" : request.Gender
        };

        if (!string.IsNullOrEmpty(request.OldPassword))
        {
            payload["oldPassword"] = request.OldPassword;
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            payload["password"] = request.Password;
        }

        return payload;
    }

    private static async Task EnsureAuthSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var fieldErrors = await ReadFieldErrorsAsync(response, cancellationToken);
        if (fieldErrors.Count > 0)
        {
            throw new AuthApiException(response.StatusCode, fieldErrors);
        }

        throw new HttpRequestException(
            $"ProxyManager auth request failed with status code {(int)response.StatusCode}.",
            null,
            response.StatusCode);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadFieldErrorsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.UnprocessableEntity ||
            response.Content.Headers.ContentLength == 0)
        {
            return new Dictionary<string, string>();
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("errors", out var errors) ||
                errors.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>();
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in errors.EnumerateObject())
            {
                var code = ReadErrorCode(property.Value);
                if (!string.IsNullOrWhiteSpace(code))
                {
                    result[property.Name] = code;
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static string? ReadErrorCode(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("key", out var key) &&
            key.ValueKind == JsonValueKind.String)
        {
            return key.GetString();
        }

        return null;
    }

    private sealed class AuthResponse
    {
        public string RefreshToken { get; init; } = string.Empty;
        public string Token { get; init; } = string.Empty;
        public long TokenExpires { get; init; }
        public UserDto? User { get; init; }

        public AuthSession ToSession() =>
            new()
            {
                AccessToken = Token,
                RefreshToken = RefreshToken,
                AccessTokenExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(TokenExpires),
                CreatedAt = DateTimeOffset.UtcNow,
                User = User?.ToUser()
            };
    }

    private sealed class UserDto
    {
        public string Id { get; init; } = string.Empty;
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? UserName { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Gender { get; init; }
        public DateTimeOffset? CreatedAt { get; init; }
        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAtSnakeCase { get; init; }
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? Coin { get; init; }
        [JsonPropertyName("balance")]
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? Balance { get; init; }
        [JsonPropertyName("wallet")]
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? Wallet { get; init; }
        [JsonPropertyName("walletBalance")]
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? WalletBalance { get; init; }
        [JsonPropertyName("merchant")]
        public MerchantDto? Merchant { get; init; }
        [JsonPropertyName("roleId")]
        public string? RoleId { get; init; }
        [JsonPropertyName("role")]
        public RoleDto? Role { get; init; }

        public AuthUser ToUser() =>
            new()
            {
                Id = Id,
                Phone = Phone,
                Email = Email,
                UserName = UserName,
                FirstName = FirstName,
                LastName = LastName,
                Gender = Gender,
                CreatedAt = CreatedAt ?? CreatedAtSnakeCase,
                Coin = FirstCoin(
                    Coin,
                    Balance,
                    Wallet,
                    WalletBalance,
                    Merchant?.Coin,
                    Merchant?.Balance,
                    Merchant?.Wallet,
                    Merchant?.WalletBalance,
                    Merchant?.Amount),
                // Prefer the nested `role.id` (matches website's `user?.role?.id` check) and fall
                // back to a flat `roleId` field in case a given endpoint only returns that shape.
                RoleId = Role?.Id?.ToString() ?? RoleId
            };
    }

    private sealed class MerchantDto
    {
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? Coin { get; init; }
        [JsonPropertyName("balance")]
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? Balance { get; init; }
        [JsonPropertyName("wallet")]
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? Wallet { get; init; }
        [JsonPropertyName("walletBalance")]
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? WalletBalance { get; init; }
        [JsonPropertyName("amount")]
        [JsonConverter(typeof(FlexibleInt64Converter))]
        public long? Amount { get; init; }
    }

    private static long? FirstCoin(params long?[] values)
    {
        foreach (var value in values)
        {
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private sealed class FlexibleInt64Converter : JsonConverter<long?>
    {
        public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out var integer))
                    {
                        return integer;
                    }

                    return reader.TryGetDecimal(out var number)
                        ? decimal.ToInt64(decimal.Truncate(number))
                        : null;
                case JsonTokenType.String:
                    return ParseDecimalText(reader.GetString());
                case JsonTokenType.StartObject:
                    using (var document = JsonDocument.ParseValue(ref reader))
                    {
                        return ReadCoinObject(document.RootElement);
                    }
                case JsonTokenType.Null:
                    return null;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteNumberValue(value.Value);
        }

        private static long? ReadCoinObject(JsonElement element)
        {
            foreach (var name in new[] { "coin", "balance", "walletBalance", "wallet", "amount" })
            {
                if (!element.TryGetProperty(name, out var property))
                {
                    continue;
                }

                if (property.ValueKind == JsonValueKind.Number)
                {
                    if (property.TryGetInt64(out var integer))
                    {
                        return integer;
                    }

                    if (property.TryGetDecimal(out var number))
                    {
                        return decimal.ToInt64(decimal.Truncate(number));
                    }
                }

                if (property.ValueKind == JsonValueKind.String &&
                    ParseDecimalText(property.GetString()) is { } parsed)
                {
                    return parsed;
                }
            }

            return null;
        }

        private static long? ParseDecimalText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return integer;
            }

            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? decimal.ToInt64(decimal.Truncate(number))
                : null;
        }
    }

    private sealed class RoleDto
    {
        // Backend sends role.id as a JSON number (e.g. 2), not a string — do not type this as
        // string, System.Text.Json throws on a number-to-string mismatch and silently breaks
        // every login/auth-me call.
        public int? Id { get; init; }
    }
}
