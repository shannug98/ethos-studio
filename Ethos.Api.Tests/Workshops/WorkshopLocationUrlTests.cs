using System.Security.Claims;
using System.Text;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Storage;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class WorkshopLocationUrlTests
{
    private class DummyAuditService : IAdminAuditService
    {
        public void AddAuditLog(
            Guid adminUserId, string actionType, string entityType, Guid entityId,
            string? reason, string category = "OPERATIONS", Guid? adminDeviceId = null,
            Guid? adminSessionId = null, string? traceId = null, string? ipAddress = null,
            string? userAgent = null, string? metadataJson = null)
        {
        }

        public Task LogActionAsync(
            Guid adminUserId, string actionType, string category, string entityType,
            Guid entityId, bool success = true, string? outcomeCode = null, string? reason = null,
            Guid? adminDeviceId = null, Guid? adminSessionId = null, string? traceId = null,
            string? requestId = null, string? ipAddress = null, string? userAgent = null,
            object? metadata = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task LogSecurityEventAsync(
            string eventType, string severity, string? ipAddress, string? userAgent,
            Guid? userId = null, Guid? adminDeviceId = null, Guid? adminSessionId = null,
            string? traceId = null, string? maskedPhone = null, object? details = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PagedResult<AdminAuditLogResponse>> GetAuditLogsAsync(
            int page, int pageSize, string? category = null, string? actionType = null,
            string? entityType = null, Guid? entityId = null, Guid? adminUserId = null,
            string? traceId = null, DateTime? startDate = null, DateTime? endDate = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<AdminAuditLogResponse> { Items = new List<AdminAuditLogResponse>(), Page = 1, PageSize = 10, TotalCount = 0 });

        public Task<AdminAuditLogResponse?> GetAuditLogByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<AdminAuditLogResponse?>(null);

        public Task<PagedResult<AdminSecurityEventResponse>> GetSecurityEventsAsync(
            int page, int pageSize, string? eventType = null, string? severity = null,
            Guid? userId = null, string? traceId = null, DateTime? startDate = null,
            DateTime? endDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<AdminSecurityEventResponse> { Items = new List<AdminSecurityEventResponse>(), Page = 1, PageSize = 10, TotalCount = 0 });

        public Task<AdminSecurityEventResponse?> GetSecurityEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<AdminSecurityEventResponse?>(null);
    }

    private class MockStorageService : ICloudflareR2StorageService
    {
        public int UploadCount { get; private set; }
        public byte[]? LastUploadedBytes { get; private set; }

        public async Task<R2UploadResult> UploadAsync(Stream stream, string originalFileName, string contentType, string section, CancellationToken cancellationToken = default)
        {
            UploadCount++;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            LastUploadedBytes = ms.ToArray();

            return new R2UploadResult
            {
                ObjectKey = $"tickets/{originalFileName}",
                PublicUrl = $"https://media.ethosdancestudio.com/tickets/{originalFileName}",
                ContentType = contentType,
                FileSizeBytes = LastUploadedBytes.Length
            };
        }

        public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration) => $"https://media.ethosdancestudio.com/{objectKey}?token=fresh";
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public string GetPublicUrl(string objectKey) => $"https://media.ethosdancestudio.com/{objectKey}";
        public string GeneratePreSignedPutUrl(string objectKey, string contentType, TimeSpan duration) => $"https://media.ethosdancestudio.com/{objectKey}?token=put";
        public Task<R2ObjectMetadata?> GetObjectMetadataAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<R2ObjectMetadata?>(null);
        public Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<(Stream Stream, string ContentType)?>(null);
        public Task<R2RangeResult?> GetObjectRangeStreamAsync(string objectKey, long? fromByte, long? toByte, CancellationToken cancellationToken = default) => Task.FromResult<R2RangeResult?>(null);
    }

    private class MockTicketSecurityService : IWorkshopTicketService
    {
        public string DeriveQrToken(WorkshopTicket ticket) => "ETHOS-TKT-SECURE-TOKEN-1234";
        public string ComputeTokenHash(string rawToken) => "hash123";
        public Task<List<Ethos.Api.Contracts.Workshops.WorkshopTicketResponse>> IssueTicketsForBookingAsync(WorkshopBooking booking, PaymentTransaction transaction, User? user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Ethos.Api.Contracts.Workshops.WorkshopTicketResponse>> GetTicketsForBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Ethos.Api.Contracts.Workshops.WorkshopTicketResponse> GetTicketPassAsync(Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Ethos.Api.Contracts.Workshops.WorkshopTicketResponse> UpdateAttendeeDetailsAsync(Guid bookingId, Guid ticketId, Guid userId, Ethos.Api.Contracts.Workshops.UpdateAttendeeDetailsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ResendTicketPassAsync(Guid bookingId, Guid ticketId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public string GeneratePdfDownloadToken(Guid ticketId, TimeSpan? validity = null) => "test-token";
        public bool ValidatePdfDownloadToken(Guid ticketId, string? token) => token == "test-token";
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private AdminWorkshopService CreateAdminWorkshopService(AppDbContext db, ICloudflareR2StorageService? storage = null)
    {
        return new AdminWorkshopService(
            db,
            new DummyAuditService(),
            null,
            storage ?? new MockStorageService());
    }

    private AdminCreateWorkshopRequest CreateValidRequest(string? locationUrl = null)
    {
        return new AdminCreateWorkshopRequest
        {
            Title = "Urban Foundations",
            Description = "Learn essential urban choreography grooves.",
            DanceStyle = "Urban",
            Level = "Beginner",
            WorkshopDate = DateTime.UtcNow.AddDays(10).Date,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Venue = "Phoenix Arena",
            VenueAddress = "Hitec City, Hyderabad",
            LocationUrl = locationUrl,
            Price = 600m,
            Capacity = 30,
            Sessions = new List<AdminWorkshopSessionItem>
            {
                new AdminWorkshopSessionItem
                {
                    Title = "Session 1",
                    SessionDate = DateTime.UtcNow.AddDays(10).Date,
                    StartTime = TimeSpan.FromHours(10),
                    EndTime = TimeSpan.FromHours(12),
                    Capacity = 30
                }
            },
            PassTypes = new List<AdminWorkshopPassTypeItem>
            {
                new AdminWorkshopPassTypeItem
                {
                    Name = "General Admission",
                    Price = 600m,
                    TotalQuantity = 30
                }
            }
        };
    }

    [Fact]
    public async Task CreateWorkshop_WithValidHttpsLocationUrl_NormalizesAndStoresSuccessfully()
    {
        using var db = CreateDbContext();
        var service = CreateAdminWorkshopService(db);
        var adminId = Guid.NewGuid();

        var request = CreateValidRequest("  https://maps.app.goo.gl/validMapUrl123  ");
        var result = await service.CreateWorkshopAsync(adminId, request, CancellationToken.None);

        Assert.NotNull(result);
        var stored = await db.Workshops.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("https://maps.app.goo.gl/validMapUrl123", stored.LocationUrl);
        Assert.Equal("https://maps.app.goo.gl/validMapUrl123", result.LocationUrl);
    }

    [Fact]
    public async Task CreateWorkshop_WithNullOrWhitespaceLocationUrl_StoresAsNull()
    {
        using var db = CreateDbContext();
        var service = CreateAdminWorkshopService(db);
        var adminId = Guid.NewGuid();

        var requestNull = CreateValidRequest(null);
        var resultNull = await service.CreateWorkshopAsync(adminId, requestNull, CancellationToken.None);
        var storedNull = await db.Workshops.FindAsync(resultNull.Id);
        Assert.Null(storedNull!.LocationUrl);

        var requestEmpty = CreateValidRequest("    ");
        var resultEmpty = await service.CreateWorkshopAsync(adminId, requestEmpty, CancellationToken.None);
        var storedEmpty = await db.Workshops.FindAsync(resultEmpty.Id);
        Assert.Null(storedEmpty!.LocationUrl);
    }

    [Theory]
    [InlineData("http://maps.google.com/test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("/relative/path/to/venue")]
    [InlineData("not-a-valid-url")]
    public async Task CreateWorkshop_WithInvalidOrNonHttpsLocationUrl_ThrowsArgumentException(string invalidUrl)
    {
        using var db = CreateDbContext();
        var service = CreateAdminWorkshopService(db);
        var adminId = Guid.NewGuid();

        var request = CreateValidRequest(invalidUrl);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateWorkshopAsync(adminId, request, CancellationToken.None));

        Assert.Contains("secure URL starting with https://", ex.Message);
    }

    [Fact]
    public async Task CreateWorkshop_WithLocationUrlExceeding1000Chars_ThrowsArgumentException()
    {
        using var db = CreateDbContext();
        var service = CreateAdminWorkshopService(db);
        var adminId = Guid.NewGuid();

        var longUrl = "https://maps.google.com/maps?q=" + new string('a', 1005);
        var request = CreateValidRequest(longUrl);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateWorkshopAsync(adminId, request, CancellationToken.None));

        Assert.Contains("cannot exceed 1000 characters", ex.Message);
    }

    [Fact]
    public async Task UpdateWorkshop_WithValidHttpsLocationUrl_UpdatesSuccessfully()
    {
        using var db = CreateDbContext();
        var service = CreateAdminWorkshopService(db);
        var adminId = Guid.NewGuid();

        var createReq = CreateValidRequest("https://maps.google.com/old");
        var created = await service.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);

        var updateReq = new AdminUpdateWorkshopRequest
        {
            Title = created.Title,
            Description = created.Description,
            DanceStyle = created.DanceStyle,
            Level = created.Level,
            WorkshopDate = created.WorkshopDate,
            StartTime = created.StartTime,
            EndTime = created.EndTime,
            Venue = created.Venue,
            VenueAddress = created.VenueAddress,
            LocationUrl = "  https://maps.app.goo.gl/newLocation456  ",
            Price = created.Price,
            Capacity = created.Capacity,
            Sessions = createReq.Sessions,
            PassTypes = createReq.PassTypes
        };

        var updated = await service.UpdateWorkshopAsync(created.Id, adminId, updateReq, CancellationToken.None);

        Assert.Equal("https://maps.app.goo.gl/newLocation456", updated.LocationUrl);
        var stored = await db.Workshops.FindAsync(created.Id);
        Assert.Equal("https://maps.app.goo.gl/newLocation456", stored!.LocationUrl);
    }

    [Fact]
    public async Task UpdateWorkshop_WithInvalidScheme_ThrowsArgumentException()
    {
        using var db = CreateDbContext();
        var service = CreateAdminWorkshopService(db);
        var adminId = Guid.NewGuid();

        var createReq = CreateValidRequest("https://maps.google.com/initial");
        var created = await service.CreateWorkshopAsync(adminId, createReq, CancellationToken.None);

        var updateReq = new AdminUpdateWorkshopRequest
        {
            Title = created.Title,
            DanceStyle = created.DanceStyle,
            Level = created.Level,
            WorkshopDate = created.WorkshopDate,
            StartTime = created.StartTime,
            EndTime = created.EndTime,
            Venue = created.Venue,
            LocationUrl = "http://insecure.maps.com",
            Price = created.Price,
            Capacity = created.Capacity,
            Sessions = createReq.Sessions,
            PassTypes = createReq.PassTypes
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateWorkshopAsync(created.Id, adminId, updateReq, CancellationToken.None));
    }

    [Fact]
    public async Task TicketPdfService_WhenLocationUrlPresent_EmbedsAuthoritativeLocationUrlInPdf()
    {
        using var db = CreateDbContext();
        var mockStorage = new MockStorageService();
        var mockSecurity = new MockTicketSecurityService();
        var options = Options.Create(new Msg91Options { PdfUrlExpiryHours = 24 });
        var pdfService = new TicketPdfService(db, mockStorage, mockSecurity, options, NullLogger<TicketPdfService>.Instance);

        var workshopId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        const string customLocationUrl = "https://maps.app.goo.gl/EthosArenaCustom";

        var workshop = new Workshop
        {
            Id = workshopId,
            Title = "Contemporary Flow",
            Venue = "Ethos Main Studio",
            VenueAddress = "Jubilee Hills, Hyderabad",
            LocationUrl = customLocationUrl,
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            StartTime = TimeSpan.FromHours(11),
            EndTime = TimeSpan.FromHours(13)
        };

        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            Workshop = workshop,
            GuestName = "Ravi Kumar",
            GuestPhone = "+919876543210"
        };

        var ticket = new WorkshopTicket
        {
            Id = ticketId,
            TicketNumber = "WKS-TKT-2026-001",
            WorkshopBookingId = bookingId,
            WorkshopBooking = booking,
            WorkshopId = workshopId,
            Workshop = workshop,
            AttendeeName = "Ravi Kumar",
            QrTokenHash = "qr_hash_123"
        };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);
        db.WorkshopTickets.Add(ticket);
        await db.SaveChangesAsync();

        var result = await pdfService.GetOrCreateTicketPdfAsync(ticket, workshop, booking);

        Assert.NotNull(result);
        Assert.Equal(1, mockStorage.UploadCount);
        Assert.NotNull(mockStorage.LastUploadedBytes);

        var pdfString = Encoding.ASCII.GetString(mockStorage.LastUploadedBytes);
        Assert.Contains(customLocationUrl, pdfString);
        Assert.Contains("OPEN LOCATION", pdfString);
    }

    [Fact]
    public async Task TicketPdfService_WhenLocationUrlNull_FallsBackToCoordinatesOrQuery()
    {
        using var db = CreateDbContext();
        var mockStorage = new MockStorageService();
        var mockSecurity = new MockTicketSecurityService();
        var options = Options.Create(new Msg91Options { PdfUrlExpiryHours = 24 });
        var pdfService = new TicketPdfService(db, mockStorage, mockSecurity, options, NullLogger<TicketPdfService>.Instance);

        var workshopId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        var workshop = new Workshop
        {
            Id = workshopId,
            Title = "Hip Hop Beginner",
            Venue = "Ethos Main Studio",
            VenueAddress = "Jubilee Hills, Hyderabad",
            LocationUrl = null,
            Latitude = 17.4319,
            Longitude = 78.4073,
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            StartTime = TimeSpan.FromHours(11),
            EndTime = TimeSpan.FromHours(13)
        };

        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            Workshop = workshop,
            GuestName = "Suresh",
            GuestPhone = "+919876543210"
        };

        var ticket = new WorkshopTicket
        {
            Id = ticketId,
            TicketNumber = "WKS-TKT-2026-002",
            WorkshopBookingId = bookingId,
            WorkshopBooking = booking,
            WorkshopId = workshopId,
            Workshop = workshop,
            AttendeeName = "Suresh",
            QrTokenHash = "qr_hash_456"
        };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);
        db.WorkshopTickets.Add(ticket);
        await db.SaveChangesAsync();

        var result = await pdfService.GetOrCreateTicketPdfAsync(ticket, workshop, booking);

        Assert.NotNull(result);
        var pdfString = Encoding.ASCII.GetString(mockStorage.LastUploadedBytes!);
        Assert.Contains("query=17.4319,78.4073", pdfString);
        Assert.Contains("OPEN LOCATION", pdfString);
    }

    [Fact]
    public async Task HistoricalPdfInvariant_UpdatingWorkshopLocationLater_DoesNotMutateArchivedPdf()
    {
        using var db = CreateDbContext();
        var mockStorage = new MockStorageService();
        var mockSecurity = new MockTicketSecurityService();
        var options = Options.Create(new Msg91Options { PdfUrlExpiryHours = 24 });
        var pdfService = new TicketPdfService(db, mockStorage, mockSecurity, options, NullLogger<TicketPdfService>.Instance);

        var workshopId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        const string initialUrl = "https://maps.app.goo.gl/InitialVenue";

        var workshop = new Workshop
        {
            Id = workshopId,
            Title = "Jazz Funk",
            Venue = "Ethos Studio",
            LocationUrl = initialUrl,
            WorkshopDate = DateTime.UtcNow.AddDays(5),
            StartTime = TimeSpan.FromHours(14),
            EndTime = TimeSpan.FromHours(16)
        };

        var booking = new WorkshopBooking
        {
            Id = bookingId,
            WorkshopId = workshopId,
            Workshop = workshop,
            GuestName = "Meera",
            GuestPhone = "+919876543210"
        };

        var ticket = new WorkshopTicket
        {
            Id = ticketId,
            TicketNumber = "WKS-TKT-2026-003",
            WorkshopBookingId = bookingId,
            WorkshopBooking = booking,
            WorkshopId = workshopId,
            Workshop = workshop,
            AttendeeName = "Meera",
            QrTokenHash = "qr_hash_789"
        };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);
        db.WorkshopTickets.Add(ticket);
        await db.SaveChangesAsync();

        // 1. Initial ticket PDF creation
        var initialPdfResult = await pdfService.GetOrCreateTicketPdfAsync(ticket, workshop, booking);
        Assert.Equal(1, mockStorage.UploadCount);
        var storedPdf = await db.TicketPdfs.FirstOrDefaultAsync(p => p.TicketId == ticketId);
        Assert.NotNull(storedPdf);
        var initialStorageKey = storedPdf.StorageKey;
        Assert.NotNull(initialStorageKey);

        // 2. Workshop location is updated by admin later
        workshop.LocationUrl = "https://maps.app.goo.gl/ChangedLocationLinkLater";
        await db.SaveChangesAsync();

        // 3. Re-request ticket PDF (e.g. customer redownloads)
        var secondPdfResult = await pdfService.GetOrCreateTicketPdfAsync(ticket, workshop, booking);

        // Invariant: does not re-upload or alter the existing stored PDF
        Assert.Equal(1, mockStorage.UploadCount);
        Assert.Equal(initialStorageKey, secondPdfResult.StorageKey);
    }
}
