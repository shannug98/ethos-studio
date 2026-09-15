using Ethos.Api.Contracts.Auth;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse?> VerifyOtpAndLoginAsync(
        string phone,
        string otp,
        OtpPurpose purpose = OtpPurpose.Login,
        CancellationToken cancellationToken = default);

    Task<LoginResult?> LoginWithPasswordAsync(
        string phone,
        string password,
        CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}
