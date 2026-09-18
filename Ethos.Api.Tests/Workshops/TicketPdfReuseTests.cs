using Ethos.Api.Application.Common;
using Ethos.Api.Application.Storage;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ethos.Api.Tests.Workshops;

public class TicketPdfReuseTests
{
    private class MockStorageService : ICloudflareR2StorageService
    {
        public int UploadCount { get; private set; }
        public int GenerateUrlCount { get; private set; }

        public Task<R2UploadResult> UploadAsync(Stream stream, string originalFileName, string contentType, string section, CancellationToken cancellationToken = default)
        {
            UploadCount++;
            return Task.FromResult(new R2UploadResult
            {
                ObjectKey = $"tickets/2026/10/{originalFileName}",
                PublicUrl = $"https://media.ethosdancestudio.com/tickets/2026/10/{originalFileName}",
                ContentType = contentType,
                FileSizeBytes = stream.Length
            });
        }

        public string GeneratePreSignedGetUrl(string objectKey, TimeSpan duration)
        {
            GenerateUrlCount++;
            return $"https://media.ethosdancestudio.com/{objectKey}?token=fresh_secure_jwt";
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public string GetPublicUrl(string objectKey) => $"https://media.ethosdancestudio.com/{objectKey}";
        public Task<(Stream Stream, string ContentType)?> GetObjectStreamAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult<(Stream Stream, string ContentType)?>(null);
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

    [Fact]
    public async Task GetOrCreateTicketPdfAsync_WhenCalledMultipleTimes_ReusesExistingStorageKeyWithoutReuploading()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(dbOptions);

        var mockStorage = new MockStorageService();
        var mockSecurity = new MockTicketSecurityService();
        var options = Options.Create(new Msg91Options { PdfUrlExpiryHours = 24 });

        var service = new TicketPdfService(db, mockStorage, mockSecurity, options, NullLogger<TicketPdfService>.Instance);

        var ticketId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var workshop = new Workshop { Id = Guid.NewGuid(), Title = "Bhangra Fusion", WorkshopDate = DateTime.UtcNow.AddDays(5), StartTime = TimeSpan.FromHours(17), EndTime = TimeSpan.FromHours(19) };
        var booking = new WorkshopBooking { Id = bookingId, Workshop = workshop, GuestName = "Ananya" };
        var ticket = new WorkshopTicket { Id = ticketId, TicketNumber = "ETHOS-WKS-001-01", AttendeeName = "Ananya", QrTokenHash = "hash123", WorkshopBookingId = bookingId, Workshop = workshop };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);
        db.WorkshopTickets.Add(ticket);
        await db.SaveChangesAsync();

        // 1. First call: Generates and uploads
        var result1 = await service.GetOrCreateTicketPdfAsync(ticket, workshop, booking);
        Assert.True(result1.Success);
        Assert.Equal(1, mockStorage.UploadCount);
        Assert.Equal(1, mockStorage.GenerateUrlCount);
        Assert.StartsWith("https://media.ethosdancestudio.com", result1.SignedHttpsUrl);

        // Verify record in database
        var storedPdf = await db.TicketPdfs.FirstOrDefaultAsync(p => p.TicketId == ticketId);
        Assert.NotNull(storedPdf);
        Assert.Equal(result1.StorageKey, storedPdf.StorageKey);

        // 2. Second call: Reuses existing without upload
        var result2 = await service.GetOrCreateTicketPdfAsync(ticket, workshop, booking);
        Assert.True(result2.Success);
        Assert.Equal(1, mockStorage.UploadCount); // DID NOT INCREMENT (NO RE-UPLOAD)
        Assert.Equal(2, mockStorage.GenerateUrlCount); // Generated fresh signed URL
        Assert.Equal(result1.StorageKey, result2.StorageKey);
    }

    [Fact]
    public void GeneratePdf_ProducesNonEmptyValidPdfBytes()
    {
        var model = new TicketPdfModel(
            WorkshopTitle: "Urban Grooves & Footwork Masterclass",
            WorkshopDate: "30 September 2026",
            WorkshopTime: "5:00 PM - 6:30 PM",
            Venue: "Ethos Main Studio A",
            AttendeeName: "Gaddam Shanmuka",
            BookingReference: "BK-60F43550",
            TicketNumber: "ETHOS-WKS-60F43550-01",
            QrToken: "SAMPLE-TOKEN-60F43550",
            Status: "CONFIRMED / ACTIVE"
        );

        var bytes = TicketPdfGenerator.Generate(model);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000);
    }
}
