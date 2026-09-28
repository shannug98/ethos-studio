using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Auth;
using Ethos.Api.Domain.Authentication;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace Ethos.Api.Tests.Admin;

public class AdminIdentityDisplayTests
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
    public async Task DeveloperLogin_Returns_DeveloperDisplayRole_And_AdminRole()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var passwordService = new PasswordService();
        var jwtSettings = new JwtSettings
        {
            Issuer = "Ethos.Api",
            Audience = "Ethos.Client",
            SecretKey = "SuperSecretKeyForEthosAdminUnitTests12345!"
        };
        var jwtService = new JwtService(new Microsoft.Extensions.Options.OptionsWrapper<JwtSettings>(jwtSettings));
        var adminDeviceService = new AdminDeviceService(db, NullLogger<AdminDeviceService>.Instance);
        var env = new TestWebHostEnvironment();
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "AdminIdentity:DeveloperPhone", "8019013757" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var adminService = new AdminAuthService(
            db,
            jwtService,
            passwordService,
            adminDeviceService,
            env,
            config,
            NullLogger<AdminAuthService>.Instance);

        // Seed Developer Admin account
        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
        var devUser = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN001",
            FullName = "Ethos Partner 1",
            Phone = "8019013757",
            PasswordHash = passwordService.HashPassword("DevPass#2026!"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        devUser.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = devUser.Id, RoleId = adminRole.Id });

        db.Roles.Add(adminRole);
        db.Users.Add(devUser);
        await db.SaveChangesAsync();

        // Login as Developer
        var result = await adminService.LoginAsync("8019013757", "DevPass#2026!", null, "DevLaptop", "127.0.0.1", "DevBrowser");

        Assert.True(result.Success);
        Assert.NotNull(result.AuthResponse);
        Assert.Equal("Ethos Partner 1", result.AuthResponse.User.FullName);
        Assert.Equal("ETHADMIN001", result.AuthResponse.User.CustomerCode);
        Assert.Equal("Developer", result.AuthResponse.User.DisplayRole);
        Assert.Contains("ADMIN", result.AuthResponse.User.Roles);

        // Verify JWT token has display_role claim
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AuthResponse.AccessToken);
        var displayRoleClaim = token.Claims.FirstOrDefault(c => c.Type == "display_role");
        Assert.NotNull(displayRoleClaim);
        Assert.Equal("Developer", displayRoleClaim.Value);
    }

    [Fact]
    public async Task NormalAdminLogin_Returns_NullDisplayRole_And_AdminRole()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var passwordService = new PasswordService();
        var jwtSettings = new JwtSettings
        {
            Issuer = "Ethos.Api",
            Audience = "Ethos.Client",
            SecretKey = "SuperSecretKeyForEthosAdminUnitTests12345!"
        };
        var jwtService = new JwtService(new Microsoft.Extensions.Options.OptionsWrapper<JwtSettings>(jwtSettings));
        var adminDeviceService = new AdminDeviceService(db, NullLogger<AdminDeviceService>.Instance);
        var env = new TestWebHostEnvironment();
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "AdminIdentity:DeveloperPhone", "8019013757" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var adminService = new AdminAuthService(
            db,
            jwtService,
            passwordService,
            adminDeviceService,
            env,
            config,
            NullLogger<AdminAuthService>.Instance);

        // Seed Normal Admin account
        var adminRole = new Role { Id = Guid.NewGuid(), Code = "ADMIN", Name = "Admin" };
        var normalAdmin = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "ETHADMIN002",
            FullName = "Ethos Partner 2",
            Phone = "8341701113",
            PasswordHash = passwordService.HashPassword("AdminPass#2026!"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        normalAdmin.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = normalAdmin.Id, RoleId = adminRole.Id });

        db.Roles.Add(adminRole);
        db.Users.Add(normalAdmin);
        await db.SaveChangesAsync();

        // Login as Normal Admin
        var result = await adminService.LoginAsync("8341701113", "AdminPass#2026!", null, "AdminTablet", "127.0.0.1", "AdminBrowser");

        Assert.True(result.Success);
        Assert.NotNull(result.AuthResponse);
        Assert.Equal("Ethos Partner 2", result.AuthResponse.User.FullName);
        Assert.Equal("ETHADMIN002", result.AuthResponse.User.CustomerCode);
        Assert.Null(result.AuthResponse.User.DisplayRole);
        Assert.Contains("ADMIN", result.AuthResponse.User.Roles);

        // Verify JWT token has no display_role claim
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AuthResponse.AccessToken);
        var displayRoleClaim = token.Claims.FirstOrDefault(c => c.Type == "display_role");
        Assert.Null(displayRoleClaim);
    }
}
