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

    [Fact]
    public void GeneratePdf_WithVenueAndMapsUrl_EmbedsClickableMapsLinkAnnotation()
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
            Status: "CONFIRMED / ACTIVE",
            VenueAddress: "Plot 42, Road No 36, Jubilee Hills, Hyderabad",
            MapsUrl: "https://www.google.com/maps/search/?api=1&query=Ethos+Main+Studio+A"
        );

        var bytes = TicketPdfGenerator.Generate(model);
        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000);
        Assert.Contains("/Annot", pdfText);
        Assert.Contains("/Subtype /Link", pdfText);
        Assert.Contains("maps", pdfText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ethos Main Studio A", pdfText);
        Assert.Contains("Jubilee Hills", pdfText);
    }

    [Fact]
    public void GeneratePdf_WhenVenueDataExtremelyLong_ClampsGracefullyAndRemainsValidPdf()
    {
        var model = new TicketPdfModel(
            WorkshopTitle: "Masterclass with extremely long title that exceeds normal display limits for stress testing",
            WorkshopDate: "30 September 2026",
            WorkshopTime: "5:00 PM - 6:30 PM",
            Venue: "Extremely Long Venue Name In Mega Convention Center Hall B Floor 4 Jubilee Hills",
            AttendeeName: "Student With A Very Very Long Full Name That Might Overflow Layouts",
            BookingReference: "BK-60F43550",
            TicketNumber: "ETHOS-WKS-60F43550-01",
            QrToken: "SAMPLE-TOKEN-60F43550",
            VenueAddress: "Plot 12345, Beside Extremely Long Landmark Near Metro Pillar 1045, Outer Ring Road Phase 2, Gachibowli Financial District, Hyderabad, Telangana 500032",
            MapsUrl: "https://www.google.com/maps/search/?api=1&query=Mega+Convention+Center+Hall+B+Floor+4+Jubilee+Hills"
        );

        var bytes = TicketPdfGenerator.Generate(model);
        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000);
        Assert.StartsWith("%PDF-1.4", pdfText);
        Assert.EndsWith("%%EOF\r\n", pdfText);
        Assert.Contains("xref", pdfText);
        Assert.Contains("trailer", pdfText);
    }

    [Fact]
    public void GeneratePdf_WhenMapsUrlNull_GeneratesValidPdfWithoutAnnotation()
    {
        var model = new TicketPdfModel(
            WorkshopTitle: "Standard Workshop",
            WorkshopDate: "30 September 2026",
            WorkshopTime: "5:00 PM - 6:30 PM",
            Venue: "Main Studio",
            AttendeeName: "Student Doe",
            BookingReference: "BK-60F43550",
            TicketNumber: "ETHOS-WKS-60F43550-01",
            QrToken: "SAMPLE-TOKEN-60F43550",
            VenueAddress: null,
            MapsUrl: null
        );

        var bytes = TicketPdfGenerator.Generate(model);
        var pdfText = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000);
        Assert.DoesNotContain("/Annot", pdfText);
        Assert.DoesNotContain("/Subtype /Link", pdfText);
        Assert.Contains("<< /Type /Pages /Kids [3 0 R] /Count 1 >>", pdfText);
        Assert.EndsWith("%%EOF\r\n", pdfText);
    }

    [Fact]
    public void Generate_Static_Sample_Workshop_Ticket_Pdf_For_Msg91_Approval()
    {
        var model = new TicketPdfModel(
            WorkshopTitle: "Ethos Sample Workshop",
            WorkshopDate: "15 October 2026",
            WorkshopTime: "6:00 PM – 8:00 PM",
            Venue: "Ethos Dance Studio",
            AttendeeName: "Test Attendee",
            BookingReference: "ETHOS-TEST-001",
            TicketNumber: "ETHOS-WKS-TEST-01",
            QrToken: "ETHOS-WKS-TEST-01",
            Status: "CONFIRMED / ACTIVE",
            VenueAddress: "Sample Studio Address",
            MapsUrl: "https://maps.google.com/?q=Ethos+Dance+Studio",
            PassName: "OFFICIAL WORKSHOP PASS",
            TrainerName: "Ethos Faculty"
        );

        var bytes = TicketPdfGenerator.Generate(model);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        const string targetPath = @"d:\ETHOS DANCE studio\Ethos_Sample_Workshop_Ticket.pdf";
        File.WriteAllBytes(targetPath, bytes);
        Assert.True(File.Exists(targetPath));
    }

    [Fact]
    public async Task GetOrCreateTicketPdfAsync_WhenRawQrTokenNull_UsesDerivedQrTokenNotTicketNumber()
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
        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            Title = "Contemporary Flow",
            WorkshopDate = new DateTime(2026, 10, 15),
            StartTime = new TimeSpan(18, 0, 0),
            EndTime = new TimeSpan(20, 0, 0),
            Venue = "Ethos Dance Studio",
            City = "Hyderabad"
        };
        var booking = new WorkshopBooking
        {
            Id = bookingId,
            Workshop = workshop,
            GuestName = "Rhea Roy",
            PassName = "Solo Pass"
        };
        var ticket = new WorkshopTicket
        {
            Id = ticketId,
            TicketNumber = "ETHOS-WKS-TICKET-NUM-99",
            AttendeeName = "Rhea Roy",
            QrTokenHash = "hash999",
            WorkshopBookingId = bookingId,
            Workshop = workshop
        };

        db.Workshops.Add(workshop);
        db.WorkshopBookings.Add(booking);
        db.WorkshopTickets.Add(ticket);
        await db.SaveChangesAsync();

        // Act: Generate ticket PDF with rawQrToken = null
        var result = await service.GetOrCreateTicketPdfAsync(ticket, workshop, booking, rawQrToken: null);

        Assert.True(result.Success);
        Assert.NotNull(result.PdfBytes);

        var derivedToken = mockSecurity.DeriveQrToken(ticket); // "ETHOS-TKT-SECURE-TOKEN-1234"
        Assert.NotEqual(ticket.TicketNumber, derivedToken);

        // Expected model using derived token
        var expectedModelWithDerivedToken = new TicketPdfModel(
            WorkshopTitle: workshop.Title,
            WorkshopDate: "15 October 2026",
            WorkshopTime: "6:00 PM – 8:00 PM",
            Venue: "Ethos Dance Studio",
            AttendeeName: "Rhea Roy",
            BookingReference: "BK-" + bookingId.ToString()[..8].ToUpperInvariant(),
            TicketNumber: ticket.TicketNumber,
            QrToken: derivedToken,
            Status: "CONFIRMED / ACTIVE",
            VenueAddress: "Hyderabad, India",
            MapsUrl: "https://www.google.com/maps/search/?api=1&query=Ethos%20Dance%20Studio%2C%20Hyderabad%2C%20India",
            PassName: "Solo Pass"
        );
        var expectedBytes = TicketPdfGenerator.Generate(expectedModelWithDerivedToken);

        // Counter-model using TicketNumber instead of derived QR token
        var incorrectModelWithTicketNumber = expectedModelWithDerivedToken with
        {
            QrToken = ticket.TicketNumber
        };
        var incorrectBytes = TicketPdfGenerator.Generate(incorrectModelWithTicketNumber);

        // Assert: Generated PDF strictly matches derived QR token output and differs from TicketNumber output
        Assert.Equal(expectedBytes, result.PdfBytes);
        Assert.NotEqual(incorrectBytes, result.PdfBytes);
    }
}
