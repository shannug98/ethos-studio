using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Auth;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ethos.Api.Tests.Admin;

public class Phase2SeedServiceTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task SeedAsync_WhenTestDataEnabled_MissingFirstPassword_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:BootstrapPassword"] = "SecureAdminPass#123",
                ["Seed:EnableTestData"] = "true"
                // Missing Seed:TestTrainerPassword
            })
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Phase2SeedService.SeedAsync(db, null, config, isDevelopment: true));

        Assert.Contains("Seed:TestTrainerPassword", ex.Message);
    }

    [Fact]
    public async Task SeedAsync_WhenTestDataEnabled_MissingSecondPassword_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:BootstrapPassword"] = "SecureAdminPass#123",
                ["Seed:EnableTestData"] = "true",
                ["Seed:TestTrainerPassword"] = "FirstTrainerSecret#1"
                // Missing Seed:SecondTestTrainerPassword
            })
            .Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Phase2SeedService.SeedAsync(db, null, config, isDevelopment: true));

        Assert.Contains("Seed:SecondTestTrainerPassword", ex.Message);
    }

    [Fact]
    public async Task SeedAsync_WhenTestDataDisabled_DoesNotRequireTestPasswords()
    {
        using var db = CreateInMemoryDbContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:BootstrapPassword"] = "SecureAdminPass#123",
                ["Seed:EnableTestData"] = "false"
            })
            .Build();

        var ex = await Record.ExceptionAsync(() =>
            Phase2SeedService.SeedAsync(db, null, config, isDevelopment: true));

        Assert.Null(ex);

        // Verify admin roles were seeded
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "ADMIN");
        Assert.NotNull(adminRole);
    }
}
