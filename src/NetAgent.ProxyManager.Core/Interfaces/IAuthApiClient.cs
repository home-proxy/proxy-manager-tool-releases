using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IAuthApiClient
{
    Task<AuthSession> LoginAsync(string phone, string password, CancellationToken cancellationToken);
    Task RegisterAsync(AuthRegisterRequest request, CancellationToken cancellationToken);
    Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task<AuthUser> GetMeAsync(string accessToken, CancellationToken cancellationToken);
    Task PatchMeAsync(AuthUpdateRequest request, string accessToken, CancellationToken cancellationToken);
    Task LogoutAsync(string accessToken, CancellationToken cancellationToken);
}
