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

    private class FakeR2Storage : Ethos.Api.Application.Storage.ICloudflareR2StorageService
    {
        public Task<Ethos.Api.Application.Storage.R2UploadResult> UploadAsync(Stream stream, string originalFileName, string contentType, string section, CancellationToken cancellationToken = default)
        {
            var key = $"{section}/{originalFileName}";
            return Task.FromResult(new Ethos.Api.Application.Storage.R2UploadResult
            {
                ObjectKey = key,
                PublicUrl = $"https://media.ethosdancestudio.com/{key}",
                ContentType = contentType,
                FileSizeBytes = stream.Length
            });
        }
        public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration) => $"https://media.ethosdancestudio.com/{objectKey}?token=fake";
        public string GeneratePreSignedPutUrl(string objectKey, string contentType, TimeSpan duration) => $"https://media.ethosdancestudio.com/{objectKey}?token=fake_put";
        public string GetPublicUrl(string objectKey) => $"https://media.ethosdancestudio.com/{objectKey}";
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Ethos.Api.Application.Storage.R2ObjectMetadata?> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<Ethos.Api.Application.Storage.R2ObjectMetadata?>(null);
        public Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<(Stream Stream, string ContentType)?>(null);
        public Task<Ethos.Api.Application.Storage.R2RangeResult?> GetObjectRangeStreamAsync(string objectKey, long? fromByte, long? toByte, CancellationToken cancellationToken = default) => Task.FromResult<Ethos.Api.Application.Storage.R2RangeResult?>(null);
    }

    private ITicketPdfService CreateTicketPdfService(AppDbContext db, IWorkshopTicketService ticketService)
    {
        return new TicketPdfService(
            db,
            new FakeR2Storage(),
            ticketService,
            Microsoft.Extensions.Options.Options.Create(new Msg91Options()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TicketPdfService>.Instance);
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
        var pdfService = CreateTicketPdfService(db, ticketService);
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
            pdfService,
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
        var pdfService = CreateTicketPdfService(db, ticketService);
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
            pdfService,
            CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
    }

    [Fact]
    public async Task GetTicketPdf_AsAnotherStudent_WithoutToken_Returns403Forbidden()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var pdfService = CreateTicketPdfService(db, ticketService);
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
            pdfService,
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetTicketPdf_AsAnonymous_WithoutToken_Returns403Forbidden()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var pdfService = CreateTicketPdfService(db, ticketService);
        var (_, _, _, _, ticket) = await SeedDataAsync(db);

        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated
        var controller = CreateControllerWithUser(anonymousUser);

        var result = await controller.GetTicketPdf(
            ticket.Id,
            token: null,
            db,
            ticketService,
            pdfService,
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetTicketPdf_AsAnonymous_WithInvalidToken_Returns403Forbidden()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var pdfService = CreateTicketPdfService(db, ticketService);
        var (_, _, _, _, ticket) = await SeedDataAsync(db);

        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var controller = CreateControllerWithUser(anonymousUser);

        var result = await controller.GetTicketPdf(
            ticket.Id,
            token: "invalid_tampered_hmac_token",
            db,
            ticketService,
            pdfService,
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetTicketPdf_AsAnonymous_WithValidScopedToken_ReturnsOkWithPdf()
    {
        using var db = new AppDbContext(_dbOptions);
        var ticketService = CreateTicketService(db);
        var pdfService = CreateTicketPdfService(db, ticketService);
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
            pdfService,
            CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.NotNull(fileResult.FileContents);
    }
}
