namespace NetAgent.ProxyManager.Core.Models;

public sealed class AuthUpdateRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Gender { get; set; }
    public string? OldPassword { get; set; }
    public string? Password { get; set; }
}
