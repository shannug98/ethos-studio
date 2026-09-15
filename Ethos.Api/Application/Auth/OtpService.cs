using System.Security.Cryptography;
using System.Text;
using Ethos.Api.Application.Students;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Auth;

public class OtpService : IOtpService
{
    private const int OtpLength = 6;
    private const int OtpExpiryMinutes = 5;
    private const int MaxAttempts = 5;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OtpService> _logger;
    private readonly IStudentEligibilityService _studentEligibilityService;

    public OtpService(
        AppDbContext db,
        IWebHostEnvironment environment,
        ILogger<OtpService> logger,
        IStudentEligibilityService studentEligibilityService)
    {
        _db = db;
        _environment = environment;
        _logger = logger;
        _studentEligibilityService = studentEligibilityService;
    }

    public async Task<OtpRequestResult> RequestOtpAsync(
        string phone,
        OtpPurpose purpose = OtpPurpose.Login,
        CancellationToken cancellationToken = default)
    {
        phone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(phone))
        {
            return new OtpRequestResult(
                false,
                "A valid phone number is required.");
        }

        if (purpose == OtpPurpose.StudentLogin)
        {
            var eligibility = await _studentEligibilityService.CheckEligibilityByPhoneAsync(phone, cancellationToken);
            if (!eligibility.HasActivePackage)
            {
                return new OtpRequestResult(
                    false,
                    eligibility.Message ?? "Please purchase a monthly class package to access the Student Portal.");
            }
        }
        else if (purpose == OtpPurpose.Login)
        {
            var userExists = await _db.Users
                .AnyAsync(x => x.Phone == phone, cancellationToken);

            if (!userExists)
            {
                return new OtpRequestResult(
                    false,
                    "Mobile number not found. Please register first to continue.");
            }
        }
        else if (purpose == OtpPurpose.AdminLogin || purpose == OtpPurpose.AdminPasswordChange)
        {
            if (phone != "8019013757" && phone != "8341701113")
            {
                return new OtpRequestResult(
                    false,
                    "Invalid administrative credentials.");
            }

            var adminUser = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(x => x.Phone == phone && x.IsActive, cancellationToken);

            var hasAdminRole = adminUser != null && adminUser.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "ADMIN");

            if (!hasAdminRole)
            {
                return new OtpRequestResult(
                    false,
                    "Invalid administrative credentials.");
            }
        }
        else if (purpose == OtpPurpose.AdminForgotPassword)
        {
            var isWhitelisted = phone == "8019013757" || phone == "8341701113";
            var adminUser = isWhitelisted
                ? await _db.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(x => x.Phone == phone && x.IsActive, cancellationToken)
                : null;

            var hasAdminRole = adminUser != null && adminUser.UserRoles.Any(ur => ur.Role != null && ur.Role.Code == "ADMIN");

            if (!hasAdminRole)
            {
                return new OtpRequestResult(
                    true,
                    "If the number belongs to an authorized administrator, an OTP has been generated.");
            }
        }

        // Prevent excessive OTP requests.
        var recentRequestCount = await _db.OtpVerifications
            .CountAsync(
                x =>
                    x.Phone == phone &&
                    x.CreatedAt >= DateTime.UtcNow.AddMinutes(-1),
                cancellationToken);

        if (recentRequestCount >= 3)
        {
            return new OtpRequestResult(
                false,
                "Too many OTP requests. Please wait a minute before requesting another code.");
        }

        var otp = GenerateOtp();

        var otpVerification = new OtpVerification
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            OtpHash = HashOtp(otp),
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
            IsUsed = false,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _db.OtpVerifications.Add(otpVerification);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "OTP generated for phone {Phone} with purpose {Purpose}. Expires at {ExpiresAt}",
            phone,
            purpose,
            otpVerification.ExpiresAt);

        var defaultMsg = purpose == OtpPurpose.AdminForgotPassword
            ? "If the number belongs to an authorized administrator, an OTP has been generated."
            : "OTP generated successfully.";

        // Development only.
        if (_environment.IsDevelopment())
        {
            return new OtpRequestResult(
                true,
                defaultMsg,
                otp);
        }

        var prodMsg = purpose == OtpPurpose.AdminForgotPassword
            ? "If the number belongs to an authorized administrator, an OTP has been generated."
            : "OTP sent successfully.";

        // Production will later send OTP through WhatsApp/SMS.
        return new OtpRequestResult(
            true,
            prodMsg);
    }

    public async Task<OtpVerificationResult> VerifyOtpAsync(
        string phone,
        string otp,
        OtpPurpose purpose = OtpPurpose.Login,
        CancellationToken cancellationToken = default)
    {
        phone = NormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(phone))
        {
            return new OtpVerificationResult(
                false,
                "A valid phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(otp) ||
            otp.Length != OtpLength ||
            !otp.All(char.IsDigit))
        {
            return new OtpVerificationResult(
                false,
                "Invalid OTP format.");
        }

        var verification = await _db.OtpVerifications
            .Where(x =>
                x.Phone == phone &&
                x.Purpose == purpose &&
                !x.IsUsed)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (verification is null)
        {
            return new OtpVerificationResult(
                false,
                "OTP not found or already used.");
        }

        if (verification.ExpiresAt < DateTime.UtcNow)
        {
            return new OtpVerificationResult(
                false,
                "OTP has expired.");
        }

        if (verification.AttemptCount >= MaxAttempts)
        {
            return new OtpVerificationResult(
                false,
                "Too many incorrect attempts. Please request a new OTP.");
        }

        verification.AttemptCount++;

        var suppliedHash = HashOtp(otp);

        if (!CryptographicEquals(
                suppliedHash,
                verification.OtpHash))
        {
            await _db.SaveChangesAsync(cancellationToken);

            return new OtpVerificationResult(
                false,
                "Invalid OTP.");
        }

        verification.IsUsed = true;

        await _db.SaveChangesAsync(cancellationToken);

        return new OtpVerificationResult(
            true,
            "OTP verified successfully.");
    }

    private static string GenerateOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);

        return value.ToString("D6");
    }

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(otp));

        return Convert.ToHexString(bytes);
    }

    private static bool CryptographicEquals(
        string first,
        string second)
    {
        var firstBytes =
            Encoding.UTF8.GetBytes(first);

        var secondBytes =
            Encoding.UTF8.GetBytes(second);

        return firstBytes.Length == secondBytes.Length &&
               CryptographicOperations.FixedTimeEquals(
                   firstBytes,
                   secondBytes);
    }

    private static string NormalizePhone(string phone)
    {
        return new string(
            phone
                .Trim()
                .Where(char.IsDigit)
                .ToArray());
    }
}
