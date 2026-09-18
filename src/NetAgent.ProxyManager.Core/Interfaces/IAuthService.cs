using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IAuthService
{
    Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken);
    Task<AuthSession> LoginAsync(string phone, string password, CancellationToken cancellationToken);
    Task<AuthSession> LoginAsync(string phone, string password, bool rememberSession, CancellationToken cancellationToken);
    Task<AuthSession> RegisterAsync(AuthRegisterRequest request, CancellationToken cancellationToken);
    Task<AuthSession> RegisterAsync(AuthRegisterRequest request, bool rememberSession, CancellationToken cancellationToken);
    Task<AuthSession?> RefreshAsync(CancellationToken cancellationToken);
    Task<AuthUser?> GetCurrentUserAsync(CancellationToken cancellationToken);
    Task<AuthUser> UpdateCurrentUserAsync(AuthUpdateRequest request, CancellationToken cancellationToken);
    Task<AuthSession> ChangePasswordAsync(AuthUpdateRequest request, CancellationToken cancellationToken);
    Task<bool> EnsureValidSessionAsync(CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
    string MaskToken(string token);
}
