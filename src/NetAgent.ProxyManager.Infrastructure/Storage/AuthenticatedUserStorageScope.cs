using System.Security.Cryptography;
using System.Text;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Storage;

internal sealed class AuthenticatedUserStorageScope(IAuthService? authService)
{
    private const string AnonymousStorageKey = "user-anonymous";

    public async Task<string> GetStorageKeyAsync(CancellationToken cancellationToken)
    {
        if (authService is null)
        {
            return AnonymousStorageKey;
        }

        var session = await authService.GetSessionAsync(cancellationToken);
        var user = session?.User;
        if (!HasStableIdentifier(user))
        {
            try
            {
                user = await authService.GetCurrentUserAsync(cancellationToken);
            }
            catch
            {
                user = session?.User;
            }
        }

        return BuildStorageKey(user);
    }

    private static bool HasStableIdentifier(AuthUser? user) =>
        user is not null &&
        (!string.IsNullOrWhiteSpace(user.Id) ||
            !string.IsNullOrWhiteSpace(user.Phone) ||
            !string.IsNullOrWhiteSpace(user.Email));

    private static string BuildStorageKey(AuthUser? user)
    {
        var identifier = user is null
            ? AnonymousStorageKey
            : FirstNonEmpty(user.Id, user.Phone, user.Email) ?? AnonymousStorageKey;
        var normalized = identifier.Trim().ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return $"user-{hash[..16]}";
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
