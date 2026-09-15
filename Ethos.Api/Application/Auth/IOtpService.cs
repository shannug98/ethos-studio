using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Application.Auth;

public interface IOtpService
{
    Task<OtpRequestResult> RequestOtpAsync(
        string phone,
        OtpPurpose purpose = OtpPurpose.Login,
        CancellationToken cancellationToken = default);

    Task<OtpVerificationResult> VerifyOtpAsync(
        string phone,
        string otp,
        OtpPurpose purpose = OtpPurpose.Login,
        CancellationToken cancellationToken = default);
}

public record OtpRequestResult(
    bool Success,
    string Message,
    string? DevelopmentOtp = null);

public record OtpVerificationResult(
    bool Success,
    string Message);
