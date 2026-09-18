using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Auth;
using Ethos.Api.Domain.Authentication;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ethos.Api.Tests.Admin;

public class AdminAuthPasswordLockoutTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Test_AdminPasswordLogin_Success_ResetsLockoutCounter()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var passwordService = new PasswordService();
        var jwtService = new JwtService(new Microsoft.Extensions.Options.OptionsWrapper<JwtSettings>(new JwtSettings
        {
            Issuer = "Ethos.Api",
            Audience = "Ethos.Client",
            SecretKey = "SuperSecretKeyForEthosAdminUnitTests12345!"
        }));
        var adminDeviceService = new AdminDeviceService(db, NullLogger<AdminDeviceService>.Instance);
        var env = new TestWebHostEnvironment();
        var config = new ConfigurationBuilder().Build();

        var adminService = new AdminAuthService(
            db,
            jwtService,
            passwordService,
            adminDeviceService,
            env,
            config,
            NullLogger<AdminAuthService>.Instance);

        // Seed admin user
        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN001",
            FullName = "Ethos Partner 1",
            Phone = "8019013757",
            PasswordHash = passwordService.HashPassword("AdminPass#2026!"),
            FailedLoginCount = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        adminUser.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = adminUser.Id, RoleId = adminRole.Id });

        db.Roles.Add(adminRole);
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        // Login with correct password
        var result = await adminService.LoginAsync("8019013757", "AdminPass#2026!", null, "TestDevice", "127.0.0.1", "TestAgent");

        Assert.True(result.Success);
        Assert.NotNull(result.AuthResponse);
        Assert.Equal("Ethos Partner 1", result.AuthResponse.User.FullName);

        var updatedUser = await db.Users.FindAsync(adminUser.Id);
        Assert.Equal(0, updatedUser!.FailedLoginCount);
        Assert.Null(updatedUser.LockoutEnd);
    }

    [Fact]
    public async Task Test_AdminPasswordLogin_InvalidPassword_IncrementsFailedCount()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var passwordService = new PasswordService();
        var jwtService = new JwtService(new Microsoft.Extensions.Options.OptionsWrapper<JwtSettings>(new JwtSettings
        {
            Issuer = "Ethos.Api",
            Audience = "Ethos.Client",
            SecretKey = "SuperSecretKeyForEthosAdminUnitTests12345!"
        }));
        var adminDeviceService = new AdminDeviceService(db, NullLogger<AdminDeviceService>.Instance);
        var env = new TestWebHostEnvironment();
        var config = new ConfigurationBuilder().Build();

        var adminService = new AdminAuthService(
            db,
            jwtService,
            passwordService,
            adminDeviceService,
            env,
            config,
            NullLogger<AdminAuthService>.Instance);

        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN001",
            FullName = "Ethos Partner 1",
            Phone = "8019013757",
            PasswordHash = passwordService.HashPassword("AdminPass#2026!"),
            FailedLoginCount = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        adminUser.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = adminUser.Id, RoleId = adminRole.Id });

        db.Roles.Add(adminRole);
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        var result = await adminService.LoginAsync("8019013757", "WrongPass#999", null, "TestDevice", "127.0.0.1", "TestAgent");

        Assert.False(result.Success);
        Assert.Equal("Invalid administrative credentials.", result.Message);

        var updatedUser = await db.Users.FindAsync(adminUser.Id);
        Assert.Equal(1, updatedUser!.FailedLoginCount);
    }

    [Fact]
    public async Task Test_AdminPasswordLogin_5FailedAttempts_TriggersAccountLockout()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var passwordService = new PasswordService();
        var jwtService = new JwtService(new Microsoft.Extensions.Options.OptionsWrapper<JwtSettings>(new JwtSettings
        {
            Issuer = "Ethos.Api",
            Audience = "Ethos.Client",
            SecretKey = "SuperSecretKeyForEthosAdminUnitTests12345!"
        }));
        var adminDeviceService = new AdminDeviceService(db, NullLogger<AdminDeviceService>.Instance);
        var env = new TestWebHostEnvironment();
        var config = new ConfigurationBuilder().Build();

        var adminService = new AdminAuthService(
            db,
            jwtService,
            passwordService,
            adminDeviceService,
            env,
            config,
            NullLogger<AdminAuthService>.Instance);

        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN001",
            FullName = "Ethos Partner 1",
            Phone = "8019013757",
            PasswordHash = passwordService.HashPassword("AdminPass#2026!"),
            FailedLoginCount = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        adminUser.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = adminUser.Id, RoleId = adminRole.Id });

        db.Roles.Add(adminRole);
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        // 5 consecutive failed login attempts
        for (int i = 1; i <= 5; i++)
        {
            var res = await adminService.LoginAsync("8019013757", $"WrongPass#{i}", null, "TestDevice", "127.0.0.1", "TestAgent");
            Assert.False(res.Success);
            Assert.Equal("Invalid administrative credentials.", res.Message);
        }

        var updatedUser = await db.Users.FindAsync(adminUser.Id);
        Assert.Equal(5, updatedUser!.FailedLoginCount);
        Assert.NotNull(updatedUser.LockoutEnd);
        Assert.True(updatedUser.LockoutEnd > DateTime.UtcNow);

        // Even with correct password, lockout prevents login
        var lockedResult = await adminService.LoginAsync("8019013757", "AdminPass#2026!", null, "TestDevice", "127.0.0.1", "TestAgent");
        Assert.False(lockedResult.Success);
        Assert.Equal("Invalid administrative credentials.", lockedResult.Message);
    }

    [Fact]
    public async Task Test_PasswordResetToken_SingleUseAndExpiration()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var passwordService = new PasswordService();
        var jwtService = new JwtService(new Microsoft.Extensions.Options.OptionsWrapper<JwtSettings>(new JwtSettings
        {
            Issuer = "Ethos.Api",
            Audience = "Ethos.Client",
            SecretKey = "SuperSecretKeyForEthosAdminUnitTests12345!"
        }));
        var adminDeviceService = new AdminDeviceService(db, NullLogger<AdminDeviceService>.Instance);
        var env = new TestWebHostEnvironment();
        var config = new ConfigurationBuilder().Build();

        var adminService = new AdminAuthService(
            db,
            jwtService,
            passwordService,
            adminDeviceService,
            env,
            config,
            NullLogger<AdminAuthService>.Instance);

        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN001",
            FullName = "Ethos Partner 1",
            Phone = "8019013757",
            PasswordHash = passwordService.HashPassword("OldPass#123456"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        adminUser.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = adminUser.Id, RoleId = adminRole.Id });

        db.Roles.Add(adminRole);
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        // 1. Request Password Reset
        var requestRes = await adminService.RequestPasswordResetAsync("8019013757", "127.0.0.1", "TestAgent");
        Assert.True(requestRes.Success);
        Assert.Contains("dispatched", requestRes.Message);

        var tokenRecord = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == adminUser.Id);
        Assert.NotNull(tokenRecord);
        Assert.NotNull(tokenRecord.TokenHash);

        // 2. Perform Password Reset using token
        // Create raw token to reset
        var rawTokenBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToHexString(rawTokenBytes).ToLowerInvariant();
        var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        tokenRecord.TokenHash = tokenHash;
        await db.SaveChangesAsync();

        var resetRes = await adminService.ResetPasswordWithTokenAsync(rawToken, "NewSecurePass#2026!", "127.0.0.1", "TestAgent");
        Assert.True(resetRes.Success);

        var updatedUser = await db.Users.FindAsync(adminUser.Id);
        Assert.True(passwordService.VerifyPassword("NewSecurePass#2026!", updatedUser!.PasswordHash!));
        Assert.NotNull(tokenRecord.UsedAt);

        // 3. Second attempt with same token fails (Single-Use enforcement)
        var reuseRes = await adminService.ResetPasswordWithTokenAsync(rawToken, "AnotherPass#9999", "127.0.0.1", "TestAgent");
        Assert.False(reuseRes.Success);
        Assert.Equal("Invalid or expired password reset token.", reuseRes.Message);
    }
}

internal class TestWebHostEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "Ethos.Api";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
}
