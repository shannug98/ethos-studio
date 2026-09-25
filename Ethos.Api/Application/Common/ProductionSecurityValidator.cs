using Microsoft.Extensions.Configuration;

namespace Ethos.Api.Application.Common;

public static class ProductionSecurityValidator
{
    private static readonly string[] PlaceholderTokens = new[]
    {
        "[YOUR-",
        "[GENERATE-",
        "placeholder",
        "example.com",
        "test_secret"
    };

    private static bool ContainsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        foreach (var token in PlaceholderTokens)
        {
            if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static void ValidateProductionSecrets(IConfiguration config)
    {
        // 1. ConnectionStrings:DefaultConnection
        var connStr = config.GetConnectionString("DefaultConnection") ?? config["ConnectionStrings:DefaultConnection"];
        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException("Production ConnectionStrings:DefaultConnection cannot be empty.");
        }
        if (connStr.Contains("Trust Server Certificate=true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Production database connection string must enforce 'Trust Server Certificate=false'.");
        }
        if (ContainsPlaceholder(connStr))
        {
            throw new InvalidOperationException("Production database connection string contains placeholder values.");
        }

        // 2. Razorpay credentials
        var acceptanceTestMode = config.GetValue<bool>("Razorpay:AcceptanceTestMode", false);
        var rzpKeyId = config["Razorpay:KeyId"];
        if (acceptanceTestMode)
        {
            if (string.IsNullOrWhiteSpace(rzpKeyId) ||
                !rzpKeyId.StartsWith("rzp_test_", StringComparison.OrdinalIgnoreCase) ||
                ContainsPlaceholder(rzpKeyId))
            {
                throw new InvalidOperationException("Acceptance test mode requires a valid Razorpay test key starting with 'rzp_test_' and cannot be empty or a placeholder.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(rzpKeyId) ||
                rzpKeyId.StartsWith("rzp_test_", StringComparison.OrdinalIgnoreCase) ||
                ContainsPlaceholder(rzpKeyId))
            {
                throw new InvalidOperationException("Production Razorpay:KeyId cannot be empty, a test key ('rzp_test_'), or a placeholder.");
            }
        }

        var rzpSecret = config["Razorpay:KeySecret"];
        if (string.IsNullOrWhiteSpace(rzpSecret) || ContainsPlaceholder(rzpSecret))
        {
            throw new InvalidOperationException("Production Razorpay:KeySecret cannot be empty or a placeholder.");
        }

        var rzpWebhook = config["Razorpay:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(rzpWebhook) || ContainsPlaceholder(rzpWebhook))
        {
            throw new InvalidOperationException("Production Razorpay:WebhookSecret cannot be empty or a placeholder.");
        }

        // 3. Jwt:SecretKey & Jwt:ExpirationMinutes
        var jwtSecret = config["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32 || ContainsPlaceholder(jwtSecret))
        {
            throw new InvalidOperationException("Production Jwt:SecretKey must be a secure key of at least 32 characters and cannot be empty or a placeholder.");
        }

        var jwtExpStr = config["Jwt:ExpirationMinutes"];
        if (string.IsNullOrWhiteSpace(jwtExpStr) || !int.TryParse(jwtExpStr, out var jwtExp) || jwtExp <= 0)
        {
            throw new InvalidOperationException("Production Jwt:ExpirationMinutes must be configured and greater than zero.");
        }

        // 4. TicketSecurity:SecretKey
        var ticketSecret = config["TicketSecurity:SecretKey"];
        if (string.IsNullOrWhiteSpace(ticketSecret) || ticketSecret.Length < 32 || ContainsPlaceholder(ticketSecret))
        {
            throw new InvalidOperationException("Production TicketSecurity:SecretKey must be a secure key of at least 32 characters and cannot be empty or a placeholder.");
        }

        // 5. Cloudflare R2 credentials & endpoints
        var r2Account = config["CloudflareR2:AccountId"];
        if (string.IsNullOrWhiteSpace(r2Account) || ContainsPlaceholder(r2Account))
        {
            throw new InvalidOperationException("Production CloudflareR2:AccountId cannot be empty or a placeholder.");
        }

        var r2AccessKey = config["CloudflareR2:AccessKeyId"];
        if (string.IsNullOrWhiteSpace(r2AccessKey) || ContainsPlaceholder(r2AccessKey))
        {
            throw new InvalidOperationException("Production CloudflareR2:AccessKeyId cannot be empty or a placeholder.");
        }

        var r2Secret = config["CloudflareR2:SecretAccessKey"];
        if (string.IsNullOrWhiteSpace(r2Secret) || ContainsPlaceholder(r2Secret))
        {
            throw new InvalidOperationException("Production CloudflareR2:SecretAccessKey cannot be empty or a placeholder.");
        }

        var r2Bucket = config["CloudflareR2:BucketName"];
        if (string.IsNullOrWhiteSpace(r2Bucket) || ContainsPlaceholder(r2Bucket))
        {
            throw new InvalidOperationException("Production CloudflareR2:BucketName cannot be empty or a placeholder.");
        }

        var r2Domain = config["CloudflareR2:PublicDomain"];
        if (string.IsNullOrWhiteSpace(r2Domain) || ContainsPlaceholder(r2Domain))
        {
            throw new InvalidOperationException("Production CloudflareR2:PublicDomain cannot be empty or a placeholder.");
        }

        // 6. MSG91 WhatsApp credentials (only if enabled)
        var msg91Enabled = config.GetValue<bool>("Msg91:Enabled", false);
        if (msg91Enabled)
        {
            var msg91AuthKey = config["Msg91:AuthKey"];
            if (string.IsNullOrWhiteSpace(msg91AuthKey) || ContainsPlaceholder(msg91AuthKey))
            {
                throw new InvalidOperationException("Production Msg91:AuthKey cannot be empty or a placeholder when Msg91 is enabled.");
            }

            var msg91Number = config["Msg91:IntegratedNumber"];
            if (string.IsNullOrWhiteSpace(msg91Number) || ContainsPlaceholder(msg91Number))
            {
                throw new InvalidOperationException("Production Msg91:IntegratedNumber cannot be empty or a placeholder when Msg91 is enabled.");
            }
        }
    }
}
