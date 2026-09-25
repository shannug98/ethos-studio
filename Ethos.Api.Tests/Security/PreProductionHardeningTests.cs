using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Auth;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Authentication;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Security;

public class PreProductionHardeningTests
{
    private class DummyWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ethos.Api";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class MockWhatsAppService : IMsg91WhatsAppService
    {
        public string? LastDispatchedResetUrl { get; private set; }
        public string? LastRecipientPhone { get; private set; }
        public int ResetDispatchCount { get; private set; }

        public Task<Msg91DispatchResult> SendAdminPasswordResetAsync(string resetUrl, string recipientPhone, CancellationToken cancellationToken = default)
        {
            LastDispatchedResetUrl = resetUrl;
            LastRecipientPhone = recipientPhone;
            ResetDispatchCount++;
            return Task.FromResult(Msg91DispatchResult.Accepted("msg_reset_test", "req_reset_test"));
        }

        public Task<Msg91DispatchResult> SendBookingConfirmedAsync(BookingConfirmedData data, string recipientPhone, CancellationToken cancellationToken = default) =>
            Task.FromResult(Msg91DispatchResult.Accepted("msg_b", "req_b"));

        public Task<Msg91DispatchResult> SendTicketPdfAsync(TicketPdfData data, string recipientPhone, CancellationToken cancellationToken = default) =>
            Task.FromResult(Msg91DispatchResult.Accepted("msg_t", "req_t"));

        public bool TryNormalizePhoneNumber(string? rawPhone, out string normalizedPhone, out string? failureReason, string defaultCountryCode = "91")
        {
            normalizedPhone = "91" + (rawPhone ?? "").TrimStart('+').Trim();
            failureReason = null;
            return true;
        }

        public string BuildBookingConfirmedJson(BookingConfirmedData data, string recipientPhone) => "{}";
        public string BuildTicketPdfJson(TicketPdfData data, string recipientPhone) => "{}";
    }

    private class MockCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; }
        public string? Phone { get; set; }
        public string? Name { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
        public bool IsAuthenticated { get; set; }
    }

    private AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AdminPermission_SuperAdmin_HasAccessToSensitiveOperations()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var authService = new AdminAuthorizationService(db, NullLogger<AdminAuthorizationService>.Instance);

        var superAdminRole = new Role { Id = Guid.NewGuid(), Code = "SUPER_ADMIN", Name = "Super Admin" };
        db.Roles.Add(superAdminRole);

        var superUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN001",
            FullName = "Founding SuperAdmin",
            Phone = "8019013757",
            IsActive = true
        };
        superUser.UserRoles.Add(new UserRole { UserId = superUser.Id, RoleId = superAdminRole.Id });
        db.Users.Add(superUser);
        await db.SaveChangesAsync();

        // SuperAdmin can access sensitive operations like PAYMENT_RECONCILE and MEDIA_DELETE
        var canReconcile = await authService.HasPermissionAsync(superUser.Id, AdminPermissions.PaymentReconcile);
        var canDeleteMedia = await authService.HasPermissionAsync(superUser.Id, AdminPermissions.MediaDelete);

        Assert.True(canReconcile);
        Assert.True(canDeleteMedia);
    }

    [Fact]
    public async Task AdminPermission_StandardAdmin_DeniedSensitiveOperations()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var authService = new AdminAuthorizationService(db, NullLogger<AdminAuthorizationService>.Instance);

        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
        db.Roles.Add(adminRole);

        var regularAdmin = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ADMIN_REGULAR",
            FullName = "Regular Operations Admin",
            Phone = "9900112233", // NOT a super-admin phone
            IsActive = true
        };
        regularAdmin.UserRoles.Add(new UserRole { UserId = regularAdmin.Id, RoleId = adminRole.Id });
        db.Users.Add(regularAdmin);
        await db.SaveChangesAsync();

        // Standard ADMIN is granted baseline operational permissions
        var canViewWorkshops = await authService.HasPermissionAsync(regularAdmin.Id, AdminPermissions.WorkshopView);
        var canUpdateClasses = await authService.HasPermissionAsync(regularAdmin.Id, AdminPermissions.ClassUpdate);
        Assert.True(canViewWorkshops);
        Assert.True(canUpdateClasses);

        // Standard ADMIN is STRICTLY DENIED sensitive operations without explicit SUPER_ADMIN elevation
        var canReconcile = await authService.HasPermissionAsync(regularAdmin.Id, AdminPermissions.PaymentReconcile);
        var canDeleteMedia = await authService.HasPermissionAsync(regularAdmin.Id, AdminPermissions.MediaDelete);
        var canRevokeDevice = await authService.HasPermissionAsync(regularAdmin.Id, AdminPermissions.DeviceRevoke);

        Assert.False(canReconcile);
        Assert.False(canDeleteMedia);
        Assert.False(canRevokeDevice);

        // AuthorizeActionAsync returns 403 Forbidden with single-event security logging
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, regularAdmin.Id.ToString()),
            new Claim(ClaimTypes.Role, "ADMIN")
        }, "TestAuth"));

        var result = await authService.AuthorizeActionAsync(claims, AdminPermissions.PaymentReconcile);
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("FORBIDDEN", result.ErrorCode);

        var secEvent = await db.SecurityEvents.FirstOrDefaultAsync(e => e.UserId == regularAdmin.Id);
        Assert.NotNull(secEvent);
        Assert.Equal("ADMIN_AUTHORIZATION_DENIED", secEvent.EventType);
    }

    [Fact]
    public async Task AdminPermission_InvalidPermission_AlwaysDenied()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var authService = new AdminAuthorizationService(db, NullLogger<AdminAuthorizationService>.Instance);

        var superAdminRole = new Role { Id = Guid.NewGuid(), Code = "SUPER_ADMIN", Name = "Super Admin" };
        db.Roles.Add(superAdminRole);

        var superUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN001",
            FullName = "SuperAdmin",
            Phone = "8019013757",
            IsActive = true
        };
        superUser.UserRoles.Add(new UserRole { UserId = superUser.Id, RoleId = superAdminRole.Id });
        db.Users.Add(superUser);
        await db.SaveChangesAsync();

        var canAccessBogus = await authService.HasPermissionAsync(superUser.Id, "BOGUS_NONEXISTENT_PERMISSION");
        Assert.False(canAccessBogus);
    }

    [Fact]
    public async Task PasswordReset_DispatchesResetUrl_AndInvalidatesOnUse()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var passwordService = new PasswordService();
        var jwtService = new JwtService(Options.Create(new JwtSettings
        {
            Issuer = "Ethos.Api",
            Audience = "Ethos.Client",
            SecretKey = "SuperSecretKeyForEthosAdminUnitTests12345!"
        }));
        var adminDeviceService = new AdminDeviceService(db, NullLogger<AdminDeviceService>.Instance);
        var env = new DummyWebHostEnvironment();
        var config = new ConfigurationBuilder().Build();
        var whatsAppMock = new MockWhatsAppService();

        var adminService = new AdminAuthService(
            db,
            jwtService,
            passwordService,
            adminDeviceService,
            env,
            config,
            NullLogger<AdminAuthService>.Instance,
            whatsAppMock);

        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
        db.Roles.Add(adminRole);

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN001",
            FullName = "Authorized Partner",
            Phone = "8019013757",
            PasswordHash = passwordService.HashPassword("OldPass#2026!"),
            IsActive = true
        };
        adminUser.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        // 1. Request Password Reset
        var requestResult = await adminService.RequestPasswordResetAsync("8019013757", "127.0.0.1", "TestAgent");
        Assert.True(requestResult.Success);
        Assert.Equal(1, whatsAppMock.ResetDispatchCount);
        Assert.NotNull(whatsAppMock.LastDispatchedResetUrl);
        Assert.Contains("https://ethosdancestudio.com/admin/reset-password?token=", whatsAppMock.LastDispatchedResetUrl);

        // Verify token is hashed in database, never stored in plaintext
        var tokenRecord = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == adminUser.Id);
        Assert.NotNull(tokenRecord);
        Assert.False(string.IsNullOrWhiteSpace(tokenRecord.TokenHash));
        Assert.Null(tokenRecord.UsedAt);
        Assert.True(tokenRecord.ExpiresAt > DateTime.UtcNow);

        // Verify communication log was created without exposing raw token
        var commLog = await db.CommunicationLogs.FirstOrDefaultAsync(c => c.RecipientUserId == adminUser.Id);
        Assert.NotNull(commLog);
        Assert.Equal("DISPATCHED", commLog.Status);
        Assert.DoesNotContain("token=", commLog.BodyPreview);

        // Extract raw token from dispatched URL
        var rawToken = whatsAppMock.LastDispatchedResetUrl.Split("token=")[1];

        // 2. Perform Reset with Token
        var resetResult = await adminService.ResetPasswordWithTokenAsync(
            rawToken,
            "NewAdminSecure#2026!",
            "127.0.0.1",
            "TestAgent");

        Assert.True(resetResult.Success);
        Assert.Equal(200, resetResult.StatusCode);

        // Verify token is marked as UsedAt
        await db.Entry(tokenRecord).ReloadAsync();
        Assert.NotNull(tokenRecord.UsedAt);

        // 3. Replay attack: attempt to use the same token a second time
        var replayResult = await adminService.ResetPasswordWithTokenAsync(
            rawToken,
            "AnotherNewPassword#2026!",
            "127.0.0.1",
            "TestAgent");

        Assert.False(replayResult.Success);
        Assert.Equal(400, replayResult.StatusCode);
    }

    [Fact]
    public async Task WorkshopIdempotency_CrossCustomerConflict_ThrowsControlledConflict()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var pricingService = new WorkshopPricingService(db, null!);
        var currentUser = new MockCurrentUserService();

        var workshopService = new WorkshopService(
            db,
            currentUser,
            pricingService,
            null!, // notificationService
            null!, // ticketService
            null!, // fulfillmentService
            Options.Create(new RazorpaySettings { KeyId = "rzp_test_placeholder" }),
            new DummyWebHostEnvironment());

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Isolation Workshop",
            Capacity = 20,
            WorkshopDate = DateTime.UtcNow.AddDays(7),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Status = WorkshopStatus.Published
        };
        db.Workshops.Add(workshop);

        var sharedIdempotencyKey = "client-unique-key-xyz-123";

        var originalBooking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            GuestEmail = "original_customer@example.com",
            GuestPhone = "919988776655",
            IdempotencyKey = sharedIdempotencyKey,
            Quantity = 1,
            TotalPrice = 1000m,
            Status = WorkshopBookingStatus.PendingPayment,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(originalBooking);
        await db.SaveChangesAsync();

        // A different customer attempts to checkout with the same idempotency key
        var conflictingRequest = new CreateWorkshopOrderRequest
        {
            Quantity = 1,
            Email = "malicious_attacker@example.com", // Different customer!
            Phone = "919123456789",
            IdempotencyKey = sharedIdempotencyKey
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workshopService.CreateWorkshopOrderAsync(workshop.Id, conflictingRequest));

        Assert.Equal("Idempotency key already belongs to another request.", ex.Message);
    }

    [Fact]
    public async Task SecurityHeaders_SetsStrictCspAndSecurityFlags()
    {
        var middleware = new SecurityHeadersMiddleware(next: (ctx) => Task.CompletedTask);
        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        var headers = context.Response.Headers;

        Assert.Equal("nosniff", headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", headers["X-Frame-Options"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"].ToString());

        var csp = headers["Content-Security-Policy"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(csp));
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self' https://checkout.razorpay.com", csp);
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", csp); // No unsafe-inline in scripts!
        Assert.Contains("frame-src 'self' https://api.razorpay.com https://checkout.razorpay.com", csp);
        Assert.Contains("https://media.ethosdancestudio.com", csp);
    }

    [Fact]
    public void ValidateProductionSecrets_ValidConfiguration_Passes()
    {
        var validConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=prod.db;Database=neondb;SSL Mode=Require;Trust Server Certificate=false;",
                ["Razorpay:KeyId"] = "rzp_live_realproductionkey12345",
                ["Razorpay:KeySecret"] = "realproductionsecretkeyabcdefgh",
                ["Razorpay:WebhookSecret"] = "realproductionwebhooksecret64characterlongstringhere1234567890!!",
                ["Jwt:SecretKey"] = "realproductionjwtsecret64characterlongstringhere12345678901234!!",
                ["Jwt:ExpirationMinutes"] = "60",
                ["TicketSecurity:SecretKey"] = "realproductionticketsecret64characterlongstringhere123456789012!!",
                ["CloudflareR2:AccountId"] = "real_production_cloudflare_r2_account_id_12345",
                ["CloudflareR2:AccessKeyId"] = "real_production_r2_access_key_id_12345",
                ["CloudflareR2:SecretAccessKey"] = "real_production_r2_secret_access_key_12345",
                ["CloudflareR2:BucketName"] = "ethos-production-media",
                ["CloudflareR2:PublicDomain"] = "https://media.ethosdancestudio.com",
                ["Msg91:Enabled"] = "true",
                ["Msg91:AuthKey"] = "real_production_msg91_auth_key_12345",
                ["Msg91:IntegratedNumber"] = "919988776655"
            })
            .Build();

        var ex = Record.Exception(() => ProductionSecurityValidator.ValidateProductionSecrets(validConfig));
        Assert.Null(ex);
    }

    [Fact]
    public void ValidateProductionSecrets_Msg91Disabled_AllowsEmptyMsg91Credentials()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=prod.db;Database=neondb;SSL Mode=Require;Trust Server Certificate=false;",
                ["Razorpay:KeyId"] = "rzp_live_realproductionkey12345",
                ["Razorpay:KeySecret"] = "realproductionsecretkeyabcdefgh",
                ["Razorpay:WebhookSecret"] = "realproductionwebhooksecret64characterlongstringhere1234567890!!",
                ["Jwt:SecretKey"] = "realproductionjwtsecret64characterlongstringhere12345678901234!!",
                ["Jwt:ExpirationMinutes"] = "60",
                ["TicketSecurity:SecretKey"] = "realproductionticketsecret64characterlongstringhere123456789012!!",
                ["CloudflareR2:AccountId"] = "real_production_cloudflare_r2_account_id_12345",
                ["CloudflareR2:AccessKeyId"] = "real_production_r2_access_key_id_12345",
                ["CloudflareR2:SecretAccessKey"] = "real_production_r2_secret_access_key_12345",
                ["CloudflareR2:BucketName"] = "ethos-production-media",
                ["CloudflareR2:PublicDomain"] = "https://media.ethosdancestudio.com",
                ["Msg91:Enabled"] = "false",
                ["Msg91:AuthKey"] = "",
                ["Msg91:IntegratedNumber"] = ""
            })
            .Build();

        var ex = Record.Exception(() => ProductionSecurityValidator.ValidateProductionSecrets(config));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("ConnectionStrings:DefaultConnection", "")]
    [InlineData("ConnectionStrings:DefaultConnection", "Host=prod.db;Trust Server Certificate=true;")]
    [InlineData("ConnectionStrings:DefaultConnection", "Host=prod.db;Password=[YOUR-PASSWORD];Trust Server Certificate=false;")]
    [InlineData("Razorpay:KeyId", "")]
    [InlineData("Razorpay:KeyId", "rzp_test_placeholder")]
    [InlineData("Razorpay:KeySecret", "")]
    [InlineData("Razorpay:KeySecret", "placeholder_secret")]
    [InlineData("Razorpay:WebhookSecret", "")]
    [InlineData("Razorpay:WebhookSecret", "[GENERATE-SECRET]")]
    [InlineData("Jwt:SecretKey", "")]
    [InlineData("Jwt:SecretKey", "short_secret")]
    [InlineData("Jwt:ExpirationMinutes", "")]
    [InlineData("Jwt:ExpirationMinutes", "0")]
    [InlineData("Jwt:ExpirationMinutes", "-10")]
    [InlineData("Jwt:ExpirationMinutes", "invalid_number")]
    [InlineData("TicketSecurity:SecretKey", "")]
    [InlineData("CloudflareR2:AccountId", "")]
    [InlineData("CloudflareR2:AccountId", "[YOUR-ACCOUNT-ID]")]
    [InlineData("CloudflareR2:AccessKeyId", "")]
    [InlineData("CloudflareR2:SecretAccessKey", "")]
    [InlineData("CloudflareR2:BucketName", "")]
    [InlineData("CloudflareR2:PublicDomain", "")]
    [InlineData("Msg91:AuthKey", "")]
    [InlineData("Msg91:AuthKey", "[YOUR-MSG91-KEY]")]
    [InlineData("Msg91:IntegratedNumber", "")]
    public void ValidateProductionSecrets_InvalidOrMissingSetting_ThrowsInvalidOperationException(string key, string badValue)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=prod.db;Database=neondb;SSL Mode=Require;Trust Server Certificate=false;",
            ["Razorpay:KeyId"] = "rzp_live_realproductionkey12345",
            ["Razorpay:KeySecret"] = "realproductionsecretkeyabcdefgh",
            ["Razorpay:WebhookSecret"] = "realproductionwebhooksecret64characterlongstringhere1234567890!!",
            ["Jwt:SecretKey"] = "realproductionjwtsecret64characterlongstringhere12345678901234!!",
            ["Jwt:ExpirationMinutes"] = "60",
            ["TicketSecurity:SecretKey"] = "realproductionticketsecret64characterlongstringhere123456789012!!",
            ["CloudflareR2:AccountId"] = "real_production_cloudflare_r2_account_id_12345",
            ["CloudflareR2:AccessKeyId"] = "real_production_r2_access_key_id_12345",
            ["CloudflareR2:SecretAccessKey"] = "real_production_r2_secret_access_key_12345",
            ["CloudflareR2:BucketName"] = "ethos-production-media",
            ["CloudflareR2:PublicDomain"] = "https://media.ethosdancestudio.com",
            ["Msg91:Enabled"] = "true",
            ["Msg91:AuthKey"] = "real_production_msg91_auth_key_12345",
            ["Msg91:IntegratedNumber"] = "919988776655"
        };

        settings[key] = badValue;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        Assert.Throws<InvalidOperationException>(() => ProductionSecurityValidator.ValidateProductionSecrets(config));
    }

    [Fact]
    public void ValidateProductionSecrets_AcceptanceTestModeTrue_WithValidTestKey_Passes()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=prod.db;Database=neondb;SSL Mode=Require;Trust Server Certificate=false;",
                ["Razorpay:AcceptanceTestMode"] = "true",
                ["Razorpay:KeyId"] = "rzp_test_realacceptancekey12345",
                ["Razorpay:KeySecret"] = "realproductionsecretkeyabcdefgh",
                ["Razorpay:WebhookSecret"] = "realproductionwebhooksecret64characterlongstringhere1234567890!!",
                ["Jwt:SecretKey"] = "realproductionjwtsecret64characterlongstringhere12345678901234!!",
                ["Jwt:ExpirationMinutes"] = "60",
                ["TicketSecurity:SecretKey"] = "realproductionticketsecret64characterlongstringhere123456789012!!",
                ["CloudflareR2:AccountId"] = "real_production_cloudflare_r2_account_id_12345",
                ["CloudflareR2:AccessKeyId"] = "real_production_r2_access_key_id_12345",
                ["CloudflareR2:SecretAccessKey"] = "real_production_r2_secret_access_key_12345",
                ["CloudflareR2:BucketName"] = "ethos-production-media",
                ["CloudflareR2:PublicDomain"] = "https://media.ethosdancestudio.com",
                ["Msg91:Enabled"] = "true",
                ["Msg91:AuthKey"] = "real_production_msg91_auth_key_12345",
                ["Msg91:IntegratedNumber"] = "919988776655"
            })
            .Build();

        var ex = Record.Exception(() => ProductionSecurityValidator.ValidateProductionSecrets(config));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("rzp_test_placeholder")]
    [InlineData("rzp_live_somelivekey")]
    [InlineData("[YOUR-RAZORPAY-KEY]")]
    public void ValidateProductionSecrets_AcceptanceTestModeTrue_WithPlaceholderOrNonTestKey_Throws(string invalidKeyId)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=prod.db;Database=neondb;SSL Mode=Require;Trust Server Certificate=false;",
                ["Razorpay:AcceptanceTestMode"] = "true",
                ["Razorpay:KeyId"] = invalidKeyId,
                ["Razorpay:KeySecret"] = "realproductionsecretkeyabcdefgh",
                ["Razorpay:WebhookSecret"] = "realproductionwebhooksecret64characterlongstringhere1234567890!!",
                ["Jwt:SecretKey"] = "realproductionjwtsecret64characterlongstringhere12345678901234!!",
                ["Jwt:ExpirationMinutes"] = "60",
                ["TicketSecurity:SecretKey"] = "realproductionticketsecret64characterlongstringhere123456789012!!",
                ["CloudflareR2:AccountId"] = "real_production_cloudflare_r2_account_id_12345",
                ["CloudflareR2:AccessKeyId"] = "real_production_r2_access_key_id_12345",
                ["CloudflareR2:SecretAccessKey"] = "real_production_r2_secret_access_key_12345",
                ["CloudflareR2:BucketName"] = "ethos-production-media",
                ["CloudflareR2:PublicDomain"] = "https://media.ethosdancestudio.com",
                ["Msg91:Enabled"] = "false"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => ProductionSecurityValidator.ValidateProductionSecrets(config));
    }

    [Fact]
    public void ValidateProductionSecrets_AcceptanceTestModeFalse_WithTestKey_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=prod.db;Database=neondb;SSL Mode=Require;Trust Server Certificate=false;",
                ["Razorpay:AcceptanceTestMode"] = "false",
                ["Razorpay:KeyId"] = "rzp_test_realacceptancekey12345",
                ["Razorpay:KeySecret"] = "realproductionsecretkeyabcdefgh",
                ["Razorpay:WebhookSecret"] = "realproductionwebhooksecret64characterlongstringhere1234567890!!",
                ["Jwt:SecretKey"] = "realproductionjwtsecret64characterlongstringhere12345678901234!!",
                ["Jwt:ExpirationMinutes"] = "60",
                ["TicketSecurity:SecretKey"] = "realproductionticketsecret64characterlongstringhere123456789012!!",
                ["CloudflareR2:AccountId"] = "real_production_cloudflare_r2_account_id_12345",
                ["CloudflareR2:AccessKeyId"] = "real_production_r2_access_key_id_12345",
                ["CloudflareR2:SecretAccessKey"] = "real_production_r2_secret_access_key_12345",
                ["CloudflareR2:BucketName"] = "ethos-production-media",
                ["CloudflareR2:PublicDomain"] = "https://media.ethosdancestudio.com",
                ["Msg91:Enabled"] = "false"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => ProductionSecurityValidator.ValidateProductionSecrets(config));
    }
}
