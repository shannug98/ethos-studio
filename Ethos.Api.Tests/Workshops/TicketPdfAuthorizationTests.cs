using System.Security.Claims;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Controllers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class TicketPdfAuthorizationTests
{
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly IConfiguration _configuration;

    public TicketPdfAuthorizationTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["TicketSecurity:SecretKey"] = "super_secure_secret_key_for_testing_purposes_123456789"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();
    }

    private IWorkshopTicketService CreateTicketService(AppDbContext db)
    {
        return new WorkshopTicketService(db, _configuration);
    }

    private async Task<(Workshop Workshop, User OwnerUser, StudentProfile OwnerProfile, WorkshopBooking Booking, WorkshopTicket Ticket)> SeedDataAsync(AppDbContext db)
    {
        var role = new Role { Id = Guid.NewGuid(), Code = "STUDENT", Name = "Student" };
        db.Roles.Add(role);

        var owner = new User
        {
            Id = Guid.NewGuid(),
            CustomerCode = "CUST-OWNER",
            FullName = "Jane Doe",
            Phone = "9876543210",
            IsActive = true
        };
        owner.UserRoles.Add(new UserRole { UserId = owner.Id, RoleId = role.Id });
        db.Users.Add(owner);

        var profile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.StudentProfiles.Add(profile);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Contemporary Masterclass",
            DanceStyle = "Contemporary",
            Level = "Intermediate",
            Capacity = 30,
            Price = 1200,
            WorkshopDate = DateTime.UtcNow.AddDays(10),
            StartTime = TimeSpan.FromHours(14),
            EndTime = TimeSpan.FromHours(16),
            Venue = "Main Studio"
        };
        db.Workshops.Add(workshop);

        var booking = new WorkshopBooking
        {
            Id = Guid.NewGuid(),
            WorkshopId = workshop.Id,
            StudentProfileId = profile.Id,
            Quantity = 1,
            TotalPrice = 1200m,
            GuestName = "Jane Doe",
            GuestPhone = "9876543210",
            Status = WorkshopBookingStatus.Confirmed,
            BookedAt = DateTime.UtcNow
        };
        db.WorkshopBookings.Add(booking);

        var ticket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            TicketNumber = "ETH-WS-TEST-01",
            AttendeeName = "Jane Doe",
            AttendeePhone = "9876543210",
            QrTokenHash = "QR-HASH-123",
            Status = TicketStatus.Issued,
            IssuedAt = DateTime.UtcNow
        };
        db.WorkshopTickets.Add(ticket);

        await db.SaveChangesAsync();
        return (workshop, owner, profile, booking, ticket);
    }

    private WorkshopsController CreateControllerWithUser(ClaimsPrincipal? user)
    {
        var controller = new WorkshopsController(null!);
        var httpContext = new DefaultHttpContext();
        if (user != null)
        {
            httpContext.User = user;
        }
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    [Fact]
    public async Task GetTicketPdf_AsTicketOwner_ReturnsOkWithPdf()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var (_, owner, _, _, ticket) = await SeedDataAsync(db);

        var ownerClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()),
            new Claim(ClaimTypes.Role, "Student")
        }, "TestAuth"));

        var controller = CreateControllerWithUser(ownerClaims);

        var result = await controller.GetTicketPdf(
            ticket.Id,
            token: null,
            db,
            ticketService,
            CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.NotNull(fileResult.FileContents);
        Assert.True(fileResult.FileContents.Length > 0);
    }

    [Fact]
    public async Task GetTicketPdf_AsAdmin_ReturnsOkWithPdf()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var (_, _, _, _, ticket) = await SeedDataAsync(db);

        var adminClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));

        var controller = CreateControllerWithUser(adminClaims);

        var result = await controller.GetTicketPdf(
            ticket.Id,
            token: null,
            db,
            ticketService,
            CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
    }

    [Fact]
    public async Task GetTicketPdf_AsAnotherStudent_WithoutToken_Returns403Forbidden()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var (_, _, _, _, ticket) = await SeedDataAsync(db);

        var otherStudentClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Student")
        }, "TestAuth"));

        var controller = CreateControllerWithUser(otherStudentClaims);

        var result = await controller.GetTicketPdf(
            ticket.Id,
            token: null,
            db,
            ticketService,
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetTicketPdf_AsAnonymous_WithoutToken_Returns403Forbidden()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var (_, _, _, _, ticket) = await SeedDataAsync(db);

        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated
        var controller = CreateControllerWithUser(anonymousUser);

        var result = await controller.GetTicketPdf(
            ticket.Id,
            token: null,
            db,
            ticketService,
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetTicketPdf_AsAnonymous_WithInvalidToken_Returns403Forbidden()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var (_, _, _, _, ticket) = await SeedDataAsync(db);

        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var controller = CreateControllerWithUser(anonymousUser);

        var result = await controller.GetTicketPdf(
            ticket.Id,
            token: "invalid_tampered_hmac_token",
            db,
            ticketService,
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetTicketPdf_AsAnonymous_WithValidScopedToken_ReturnsOkWithPdf()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var (_, _, _, _, ticket) = await SeedDataAsync(db);

        // Generate authentic HMAC token scoped to this ticket
        var validToken = ticketService.GeneratePdfDownloadToken(ticket.Id);

        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var controller = CreateControllerWithUser(anonymousUser);

        var result = await controller.GetTicketPdf(
            ticket.Id,
            token: validToken,
            db,
            ticketService,
            CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.NotNull(fileResult.FileContents);
    }
}
