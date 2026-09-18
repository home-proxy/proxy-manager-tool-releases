using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class AuthService(
    ILocalSecretStore secretStore,
    IAuthApiClient authApiClient) : IAuthService
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
    private AuthSession? _currentSession;
    private bool _rememberCurrentSession;

    public async Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken)
    {
        if (IsComplete(_currentSession))
        {
            return _currentSession;
        }

        var session = await secretStore.GetSessionAsync(cancellationToken);
        if (!IsComplete(session))
        {
            return null;
        }

        _currentSession = session;
        _rememberCurrentSession = true;
        return session;
    }

    public Task<AuthSession> LoginAsync(string phone, string password, CancellationToken cancellationToken) =>
        LoginAsync(phone, password, rememberSession: true, cancellationToken);

    public async Task<AuthSession> LoginAsync(
        string phone,
        string password,
        bool rememberSession,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Phone is required.", nameof(phone));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        var session = await authApiClient.LoginAsync(phone.Trim(), password, cancellationToken);
        await SetSessionAsync(session, rememberSession, cancellationToken);
        return session;
    }

    public Task<AuthSession> RegisterAsync(AuthRegisterRequest request, CancellationToken cancellationToken) =>
        RegisterAsync(request, rememberSession: true, cancellationToken);

    public async Task<AuthSession> RegisterAsync(
        AuthRegisterRequest request,
        bool rememberSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRegisterRequest(request);

        await authApiClient.RegisterAsync(request, cancellationToken);
        return await LoginAsync(request.Phone, request.Password, rememberSession, cancellationToken);
    }

    public async Task<AuthSession?> RefreshAsync(CancellationToken cancellationToken)
    {
        var current = await GetSessionAsync(cancellationToken);
        if (current is null || string.IsNullOrWhiteSpace(current.RefreshToken))
        {
            await ClearAsync(cancellationToken);
            return null;
        }

        try
        {
            var refreshed = await authApiClient.RefreshAsync(current.RefreshToken, cancellationToken);
            refreshed.User = current.User;
            refreshed.CreatedAt = current.CreatedAt == default ? DateTimeOffset.UtcNow : current.CreatedAt;
            await SetSessionAsync(refreshed, _rememberCurrentSession, cancellationToken);
            return refreshed;
        }
        catch
        {
            await ClearAsync(cancellationToken);
            throw;
        }
    }

    public async Task<AuthUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var session = await EnsureAndGetSessionAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        try
        {
            var user = await authApiClient.GetMeAsync(session.AccessToken, cancellationToken);
            session.User = user;
            await SetSessionAsync(session, _rememberCurrentSession, cancellationToken);
            return user;
        }
        catch
        {
            await ClearAsync(cancellationToken);
            throw;
        }
    }

    public async Task<AuthUser> UpdateCurrentUserAsync(AuthUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUpdateRequest(request);

        var session = await EnsureAndGetSessionAsync(cancellationToken)
            ?? throw new InvalidOperationException("A valid login session is required.");

        await authApiClient.PatchMeAsync(request, session.AccessToken, cancellationToken);
        var user = await authApiClient.GetMeAsync(session.AccessToken, cancellationToken);
        session.User = user;
        await SetSessionAsync(session, _rememberCurrentSession, cancellationToken);
        return user;
    }

    public async Task<AuthSession> ChangePasswordAsync(AuthUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUpdateRequest(request);
        if (string.IsNullOrWhiteSpace(request.OldPassword))
        {
            throw new ArgumentException("Old password is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required.", nameof(request));
        }

        var session = await EnsureAndGetSessionAsync(cancellationToken)
            ?? throw new InvalidOperationException("A valid login session is required.");

        await authApiClient.PatchMeAsync(request, session.AccessToken, cancellationToken);

        try
        {
            return await LoginAsync(
                NormalizePhoneForLogin(request.Phone),
                request.Password,
                _rememberCurrentSession,
                cancellationToken);
        }
        catch
        {
            await ClearAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> EnsureValidSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var session = await EnsureAndGetSessionAsync(cancellationToken);
            if (session is null)
            {
                return false;
            }

            var user = await authApiClient.GetMeAsync(session.AccessToken, cancellationToken);
            session.User = user;
            await SetSessionAsync(session, _rememberCurrentSession, cancellationToken);
            return true;
        }
        catch
        {
            await ClearAsync(cancellationToken);
            return false;
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(cancellationToken);
        if (session is not null && !string.IsNullOrWhiteSpace(session.AccessToken))
        {
            try
            {
                await authApiClient.LogoutAsync(session.AccessToken, cancellationToken);
            }
            catch
            {
                // Logout is best-effort; local token removal must still happen.
            }
        }

        await ClearAsync(cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        _currentSession = null;
        _rememberCurrentSession = false;
        await secretStore.ClearSessionAsync(cancellationToken);
    }

    public string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        if (token.Length <= 4)
        {
            return new string('*', token.Length);
        }

        var prefixLength = Math.Min(8, Math.Max(0, token.Length - 4));
        return $"{token[..prefixLength]}********{token[^4..]}";
    }

    private async Task<AuthSession?> EnsureAndGetSessionAsync(CancellationToken cancellationToken)
    {
        var session = await GetSessionAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (session.HasUsableAccessToken(RefreshSkew))
        {
            return session;
        }

        return await RefreshAsync(cancellationToken);
    }

    private async Task SetSessionAsync(
        AuthSession session,
        bool rememberSession,
        CancellationToken cancellationToken)
    {
        _currentSession = session;
        _rememberCurrentSession = rememberSession;
        if (rememberSession)
        {
            await secretStore.SaveSessionAsync(session, cancellationToken);
            return;
        }

        await secretStore.ClearSessionAsync(cancellationToken);
    }

    private static bool IsComplete(AuthSession? session) =>
        session is not null &&
        !string.IsNullOrWhiteSpace(session.AccessToken) &&
        !string.IsNullOrWhiteSpace(session.RefreshToken) &&
        session.AccessTokenExpiresAt > DateTimeOffset.UnixEpoch;

    private static void ValidateRegisterRequest(AuthRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            throw new ArgumentException("Phone is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            throw new ArgumentException("First name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new ArgumentException("Last name is required.", nameof(request));
        }
    }

    private static void ValidateUpdateRequest(AuthUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            throw new ArgumentException("First name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new ArgumentException("Last name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            throw new ArgumentException("Phone is required.", nameof(request));
        }
    }

    private static string NormalizePhoneForLogin(string phone)
    {
        var normalized = phone.Trim();
        if (normalized.StartsWith("+84", StringComparison.Ordinal) && normalized.Length > 3)
        {
            return $"0{normalized[3..]}";
        }

        if (normalized.StartsWith("84", StringComparison.Ordinal) && normalized.Length > 2)
        {
            return $"0{normalized[2..]}";
        }

        return normalized;
    }
}
