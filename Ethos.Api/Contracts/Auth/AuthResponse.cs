namespace Ethos.Api.Contracts.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public UserInfoResponse User { get; set; } = new();
}

public class UserInfoResponse
{
    public Guid Id { get; set; }

    public string CustomerCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public List<string> Roles { get; set; } = [];

    public bool MustChangePassword { get; set; }
}
