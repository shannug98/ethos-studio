using System.Security.Cryptography;
using System.Text;

namespace Ethos.Api.Application.Feedback;

public static class FeedbackTokenHelper
{
    public static string DeriveRawToken(Guid bookingId, string? secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Feedback token generation failed: TicketSecurity:SecretKey is required in application configuration.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(secretKey.Trim());
        using var hmac = new HMACSHA256(keyBytes);
        var message = $"ETHOS:WORKSHOP:FEEDBACK:{bookingId:D}";
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw new ArgumentException("Raw feedback token cannot be empty.", nameof(rawToken));
        }

        var bytes = Encoding.UTF8.GetBytes(rawToken.Trim());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
