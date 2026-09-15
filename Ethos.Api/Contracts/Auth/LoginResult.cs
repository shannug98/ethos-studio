namespace Ethos.Api.Contracts.Auth;

public class LoginResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public bool OtpRequired { get; set; }

    public bool MustChangePassword { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string? DevelopmentOtp { get; set; }
}
