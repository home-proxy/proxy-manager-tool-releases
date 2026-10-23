using System.Net;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class AuthService(
    ILocalSecretStore secretStore,
    IAuthApiClient authApiClient) : IAuthService
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private AuthSession? _currentSession;
    private bool _rememberCurrentSession;

    public event EventHandler? SessionCleared;

    public Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken) =>
        WithSessionGate(() => GetSessionCoreAsync(cancellationToken), cancellationToken);

    public Task<AuthSession> LoginAsync(string phone, string password, CancellationToken cancellationToken) =>
        LoginAsync(phone, password, rememberSession: true, cancellationToken);

    public Task<AuthSession> LoginAsync(
        string phone,
        string password,
        bool rememberSession,
        CancellationToken cancellationToken) =>
        WithSessionGate(() => LoginCoreAsync(phone, password, rememberSession, cancellationToken), cancellationToken);

    public Task<AuthSession> RegisterAsync(AuthRegisterRequest request, CancellationToken cancellationToken) =>
        RegisterAsync(request, rememberSession: true, cancellationToken);

    public Task<AuthSession> RegisterAsync(
        AuthRegisterRequest request,
        bool rememberSession,
        CancellationToken cancellationToken) =>
        WithSessionGate(() => RegisterCoreAsync(request, rememberSession, cancellationToken), cancellationToken);

    public Task<AuthSession?> RefreshAsync(CancellationToken cancellationToken) =>
        WithSessionGate(() => RefreshCoreAsync(cancellationToken), cancellationToken);

    public Task<AuthUser?> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        WithSessionGate(() => GetCurrentUserCoreAsync(cancellationToken), cancellationToken);

    public Task<AuthUser> UpdateCurrentUserAsync(AuthUpdateRequest request, CancellationToken cancellationToken) =>
        WithSessionGate(() => UpdateCurrentUserCoreAsync(request, cancellationToken), cancellationToken);

    public Task<AuthSession> ChangePasswordAsync(AuthUpdateRequest request, CancellationToken cancellationToken) =>
        WithSessionGate(() => ChangePasswordCoreAsync(request, cancellationToken), cancellationToken);

    public Task<bool> EnsureValidSessionAsync(CancellationToken cancellationToken) =>
        WithSessionGate(() => EnsureValidSessionCoreAsync(cancellationToken), cancellationToken);

    public Task LogoutAsync(CancellationToken cancellationToken) =>
        WithSessionGate(() => LogoutCoreAsync(cancellationToken), cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken) =>
        WithSessionGate(() => ClearCoreAsync(cancellationToken), cancellationToken);

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

    private async Task<T> WithSessionGate<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await _sessionGate.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    private async Task WithSessionGate(Func<Task> action, CancellationToken cancellationToken)
    {
        await _sessionGate.WaitAsync(cancellationToken);
        try
        {
            await action();
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    private async Task<AuthSession?> GetSessionCoreAsync(CancellationToken cancellationToken)
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

    private async Task<AuthSession> LoginCoreAsync(
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
        await SetSessionCoreAsync(session, rememberSession, cancellationToken);
        return session;
    }

    private async Task<AuthSession> RegisterCoreAsync(
        AuthRegisterRequest request,
        bool rememberSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRegisterRequest(request);

        await authApiClient.RegisterAsync(request, cancellationToken);
        return await LoginCoreAsync(request.Phone, request.Password, rememberSession, cancellationToken);
    }

    private async Task<AuthSession?> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var current = await GetSessionCoreAsync(cancellationToken);
        if (current is null || string.IsNullOrWhiteSpace(current.RefreshToken))
        {
            await ClearCoreAsync(cancellationToken);
            return null;
        }

        try
        {
            var refreshed = await authApiClient.RefreshAsync(current.RefreshToken, cancellationToken);
            refreshed.User = current.User;
            refreshed.CreatedAt = current.CreatedAt == default ? DateTimeOffset.UtcNow : current.CreatedAt;
            await SetSessionCoreAsync(refreshed, _rememberCurrentSession, cancellationToken);
            return refreshed;
        }
        catch (Exception ex) when (!IsAuthenticationRejected(ex))
        {
            throw;
        }
        catch
        {
            await ClearCoreAsync(cancellationToken);
            throw;
        }
    }

    private async Task<AuthUser?> GetCurrentUserCoreAsync(CancellationToken cancellationToken)
    {
        var session = await EnsureAndGetSessionCoreAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        try
        {
            var user = await authApiClient.GetMeAsync(session.AccessToken, cancellationToken);
            KeepKnownCoin(user, session.User);
            session.User = user;
            await SetSessionCoreAsync(session, _rememberCurrentSession, cancellationToken);
            return user;
        }
        catch (Exception ex) when (!IsAuthenticationRejected(ex))
        {
            return session.User;
        }
        catch
        {
            await ClearCoreAsync(cancellationToken);
            throw;
        }
    }

    private async Task<AuthUser> UpdateCurrentUserCoreAsync(AuthUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUpdateRequest(request);

        var session = await EnsureAndGetSessionCoreAsync(cancellationToken)
            ?? throw new InvalidOperationException("A valid login session is required.");

        await authApiClient.PatchMeAsync(request, session.AccessToken, cancellationToken);
        var user = await authApiClient.GetMeAsync(session.AccessToken, cancellationToken);
        session.User = user;
        await SetSessionCoreAsync(session, _rememberCurrentSession, cancellationToken);
        return user;
    }

    private async Task<AuthSession> ChangePasswordCoreAsync(AuthUpdateRequest request, CancellationToken cancellationToken)
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

        var session = await EnsureAndGetSessionCoreAsync(cancellationToken)
            ?? throw new InvalidOperationException("A valid login session is required.");

        await authApiClient.PatchMeAsync(request, session.AccessToken, cancellationToken);

        try
        {
            return await LoginCoreAsync(
                NormalizePhoneForLogin(request.Phone),
                request.Password,
                _rememberCurrentSession,
                cancellationToken);
        }
        catch
        {
            await ClearCoreAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> EnsureValidSessionCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var session = await EnsureAndGetSessionCoreAsync(cancellationToken);
            if (session is null)
            {
                return false;
            }

            var user = await authApiClient.GetMeAsync(session.AccessToken, cancellationToken);
            KeepKnownCoin(user, session.User);
            session.User = user;
            await SetSessionCoreAsync(session, _rememberCurrentSession, cancellationToken);
            return true;
        }
        catch (Exception ex) when (!IsAuthenticationRejected(ex))
        {
            return IsComplete(_currentSession);
        }
        catch
        {
            await ClearCoreAsync(cancellationToken);
            return false;
        }
    }

    private async Task LogoutCoreAsync(CancellationToken cancellationToken)
    {
        var session = await GetSessionCoreAsync(cancellationToken);
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

        await ClearCoreAsync(cancellationToken);
    }

    private async Task ClearCoreAsync(CancellationToken cancellationToken)
    {
        var hadSession = _currentSession is not null || _rememberCurrentSession;
        _currentSession = null;
        _rememberCurrentSession = false;
        await secretStore.ClearSessionAsync(cancellationToken);
        if (hadSession)
        {
            SessionCleared?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<AuthSession?> EnsureAndGetSessionCoreAsync(CancellationToken cancellationToken)
    {
        var session = await GetSessionCoreAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (session.HasUsableAccessToken(RefreshSkew))
        {
            return session;
        }

        return await RefreshCoreAsync(cancellationToken);
    }

    private async Task SetSessionCoreAsync(
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

    private static void KeepKnownCoin(AuthUser incoming, AuthUser? previous)
    {
        if (incoming.Coin is null && previous?.Coin is not null)
        {
            incoming.Coin = previous.Coin;
        }
    }

    private static bool IsAuthenticationRejected(Exception exception)
    {
        if (exception is AuthApiException auth &&
            auth.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return true;
        }

        return exception is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden };
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
