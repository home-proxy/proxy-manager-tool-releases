namespace NetAgent.ProxyManager.Core.Models;

public sealed class AuthSession
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public AuthUser? User { get; set; }

    public bool HasUsableAccessToken(TimeSpan refreshSkew) =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        AccessTokenExpiresAt > DateTimeOffset.UtcNow.Add(refreshSkew);
}
