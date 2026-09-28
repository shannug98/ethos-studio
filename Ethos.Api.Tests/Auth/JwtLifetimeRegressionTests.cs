using System.IdentityModel.Tokens.Jwt;
using Ethos.Api.Application.Common;
using Ethos.Api.Domain.Authentication;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Auth;

public class JwtLifetimeRegressionTests
{
    [Fact]
    public void GenerateToken_WithExpirationMinutes60_ProducesExpirationApproximately60MinutesInFuture()
    {
        // Arrange
        const int configuredMinutes = 60;
        var jwtSettings = new JwtSettings
        {
            Issuer = "EthosDanceStudioTest",
            Audience = "EthosDanceStudioTestAudience",
            SecretKey = "SuperSecretKeyForEthosAdminUnitTests12345!",
            ExpirationMinutes = configuredMinutes
        };

        var jwtService = new JwtService(Options.Create(jwtSettings));

        var user = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN001",
            FullName = "Test Administrator",
            Phone = "8019013757",
            IsActive = true
        };

        var beforeUtc = DateTime.UtcNow;

        // Act
        var result = jwtService.GenerateToken(user, new[] { "ADMIN" });

        var afterUtc = DateTime.UtcNow;

        // Assert - Token result expiration
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));

        var expectedMin = beforeUtc.AddMinutes(configuredMinutes);
        var expectedMax = afterUtc.AddMinutes(configuredMinutes);

        Assert.InRange(result.ExpiresAt, expectedMin, expectedMax);

        // Assert - Proven to be roughly 60 minutes in the future, NEVER 0 minutes / immediately expired
        var lifetime = result.ExpiresAt - beforeUtc;
        Assert.True(lifetime.TotalMinutes >= 59.0 && lifetime.TotalMinutes <= 61.0,
            $"Expected token lifetime to be approximately 60 minutes, but was {lifetime.TotalMinutes} minutes.");

        // Assert - Security token payload has matching ValidTo
        var handler = new JwtSecurityTokenHandler();
        var parsedToken = handler.ReadJwtToken(result.AccessToken);

        var tokenLifetime = parsedToken.ValidTo - beforeUtc;
        Assert.True(tokenLifetime.TotalMinutes >= 59.0 && tokenLifetime.TotalMinutes <= 61.0,
            $"Expected parsed JWT ValidTo to be approximately 60 minutes into the future, but was {tokenLifetime.TotalMinutes} minutes.");
    }

    [Fact]
    public void ConfigurationBinding_JwtExpirationMinutes_BindsSuccessfully_WhereasAccessTokenExpirationMinutesDoesNot()
    {
        // 1. Correct standard key: "Jwt:ExpirationMinutes"
        var validConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "EthosDanceStudioProduction",
                ["Jwt:Audience"] = "EthosDanceStudioProductionAudience",
                ["Jwt:SecretKey"] = "SuperSecretKeyForEthosAdminUnitTests12345!",
                ["Jwt:ExpirationMinutes"] = "60"
            })
            .Build();

        var validSettings = validConfig.GetSection("Jwt").Get<JwtSettings>();
        Assert.NotNull(validSettings);
        Assert.Equal(60, validSettings.ExpirationMinutes);

        // 2. Mismatched key: "Jwt:AccessTokenExpirationMinutes" does NOT bind to ExpirationMinutes property
        var mismatchedConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "EthosDanceStudioProduction",
                ["Jwt:Audience"] = "EthosDanceStudioProductionAudience",
                ["Jwt:SecretKey"] = "SuperSecretKeyForEthosAdminUnitTests12345!",
                ["Jwt:AccessTokenExpirationMinutes"] = "60"
            })
            .Build();

        var mismatchedSettings = mismatchedConfig.GetSection("Jwt").Get<JwtSettings>();
        Assert.NotNull(mismatchedSettings);
        Assert.Equal(0, mismatchedSettings.ExpirationMinutes); // Proves why AccessTokenExpirationMinutes failed to bind
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-60")]
    [InlineData("invalid")]
    public void ProductionSecurityValidator_RejectsMissingOrNonPositiveJwtExpirationMinutes(string badExpiration)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=prod.db;Database=neondb;SSL Mode=Require;Trust Server Certificate=false;",
                ["Razorpay:KeyId"] = "rzp_live_TEST_FIXTURE_NOT_A_REAL_KEY",
                ["Razorpay:KeySecret"] = "test_fixture_secret_key_abcdefgh",
                ["Razorpay:WebhookSecret"] = "test_fixture_webhook_secret_64characterlongstringhere1234567890!!",
                ["Jwt:SecretKey"] = "test_fixture_jwt_secret_64characterlongstringhere12345678901234!!",
                ["Jwt:ExpirationMinutes"] = badExpiration,
                ["TicketSecurity:SecretKey"] = "test_fixture_ticket_secret_64characterlongstringhere123456789012!!",
                ["CloudflareR2:AccountId"] = "test_fixture_cloudflare_r2_account_id_12345",
                ["CloudflareR2:AccessKeyId"] = "test_fixture_r2_access_key_id_12345",
                ["CloudflareR2:SecretAccessKey"] = "test_fixture_r2_secret_access_key_12345",
                ["CloudflareR2:BucketName"] = "ethos-production-media",
                ["CloudflareR2:PublicDomain"] = "https://media.ethosdancestudio.com",
                ["Msg91:Enabled"] = "false"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProductionSecurityValidator.ValidateProductionSecrets(config));

        Assert.Contains("Jwt:ExpirationMinutes", ex.Message);
    }
}
