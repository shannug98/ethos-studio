using System.Security.Cryptography;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Storage;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Application.Workshops;

public class TicketPdfService : ITicketPdfService
{
    private readonly AppDbContext _dbContext;
    private readonly ICloudflareR2StorageService _r2Storage;
    private readonly IWorkshopTicketService _ticketService;
    private readonly Msg91Options _options;
    private readonly ILogger<TicketPdfService> _logger;

    public TicketPdfService(
        AppDbContext dbContext,
        ICloudflareR2StorageService r2Storage,
        IWorkshopTicketService ticketService,
        IOptions<Msg91Options> options,
        ILogger<TicketPdfService> logger)
    {
        _dbContext = dbContext;
        _r2Storage = r2Storage;
        _ticketService = ticketService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TicketPdfResult> GetOrCreateTicketPdfAsync(
        WorkshopTicket ticket,
        Workshop workshop,
        WorkshopBooking booking,
        string? rawQrToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(workshop);
        ArgumentNullException.ThrowIfNull(booking);

        if (ticket.Status == TicketStatus.Replaced)
        {
            return new TicketPdfResult(
                Success: false,
                StorageKey: string.Empty,
                SignedHttpsUrl: null,
                FileHash: string.Empty,
                ErrorMessage: "Ticket pass has been replaced and cannot have a PDF generated.");
        }

        // 1. Check if a TicketPdf record already exists (deterministic reuse)
        var existing = await _dbContext.TicketPdfs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TicketId == ticket.Id, cancellationToken);

        if (existing != null)
        {
            var signedUrl = _r2Storage.GeneratePreSignedGetUrl(
                existing.StorageKey,
                TimeSpan.FromHours(Math.Max(1, _options.PdfUrlExpiryHours)));

            if (!IsSecureHttpsUrl(signedUrl))
            {
                _logger.LogWarning(
                    "Cached TicketPdf for Ticket {TicketId} generated a non-HTTPS signed URL: {Url}",
                    ticket.Id,
                    SanitizeUrl(signedUrl));
            }

            _logger.LogInformation(
                "Reusing existing TicketPdf record for Ticket {TicketId} (StorageKey: {StorageKey})",
                ticket.Id,
                existing.StorageKey);

            byte[]? existingBytes = null;
            try
            {
                var r2Stream = await _r2Storage.GetObjectStreamAsync(existing.StorageKey, cancellationToken);
                if (r2Stream != null)
                {
                    using var ms = new MemoryStream();
                    await r2Stream.Value.Stream.CopyToAsync(ms, cancellationToken);
                    existingBytes = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stream cached PDF from R2 for Ticket {TicketId}", ticket.Id);
            }

            if (existingBytes == null || existingBytes.Length == 0)
            {
                var fallbackModel = await BuildModelAsync(ticket, workshop, booking, rawQrToken, cancellationToken);
                existingBytes = TicketPdfGenerator.Generate(fallbackModel);
            }

            return new TicketPdfResult(
                Success: true,
                StorageKey: existing.StorageKey,
                SignedHttpsUrl: signedUrl,
                FileHash: existing.FileHash,
                PdfBytes: existingBytes);
        }

        // 2. Build authoritative PDF model
        var model = await BuildModelAsync(ticket, workshop, booking, rawQrToken, cancellationToken);

        byte[] pdfBytes;
        try
        {
            pdfBytes = TicketPdfGenerator.Generate(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF pass for Ticket {TicketId}", ticket.Id);
            return new TicketPdfResult(
                Success: false,
                StorageKey: null,
                SignedHttpsUrl: null,
                FileHash: null,
                ErrorMessage: $"PDF generation failed: {ex.Message}");
        }

        var fileHash = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();

        // 3. Upload to Cloudflare R2
        string storageKey;
        using (var stream = new MemoryStream(pdfBytes))
        {
            var uploadResult = await _r2Storage.UploadAsync(
                stream,
                $"{ticket.TicketNumber}.pdf",
                "application/pdf",
                "tickets",
                cancellationToken);

            storageKey = uploadResult.ObjectKey;
        }

        var freshSignedUrl = _r2Storage.GeneratePreSignedGetUrl(
            storageKey,
            TimeSpan.FromHours(Math.Max(1, _options.PdfUrlExpiryHours)));

        if (!IsSecureHttpsUrl(freshSignedUrl))
        {
            _logger.LogWarning(
                "Uploaded TicketPdf for Ticket {TicketId} generated a non-HTTPS signed URL: {Url}",
                ticket.Id,
                SanitizeUrl(freshSignedUrl));
        }

        // 4. Save TicketPdf record atomically
        var ticketPdf = new TicketPdf
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StorageKey = storageKey,
            FileHash = fileHash,
            FileSizeBytes = pdfBytes.Length,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TicketPdfs.Add(ticketPdf);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concurrency race detected saving TicketPdf for Ticket {TicketId}. Reloading existing.", ticket.Id);
            var reloaded = await _dbContext.TicketPdfs
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TicketId == ticket.Id, cancellationToken);

            if (reloaded != null)
            {
                return new TicketPdfResult(
                    Success: true,
                    StorageKey: reloaded.StorageKey,
                    SignedHttpsUrl: freshSignedUrl,
                    FileHash: reloaded.FileHash,
                    PdfBytes: pdfBytes);
            }
        }

        return new TicketPdfResult(
            Success: true,
            StorageKey: storageKey,
            SignedHttpsUrl: freshSignedUrl,
            FileHash: fileHash,
            PdfBytes: pdfBytes);
    }

    private async Task<TicketPdfModel> BuildModelAsync(
        WorkshopTicket ticket,
        Workshop workshop,
        WorkshopBooking booking,
        string? rawQrToken,
        CancellationToken cancellationToken)
    {
        // Ensure WorkshopSession and WorkshopPassType are loaded if available
        if (ticket.WorkshopSessionId.HasValue && ticket.WorkshopSession == null)
        {
            ticket.WorkshopSession = await _dbContext.WorkshopSessions
                .Include(s => s.TrainerProfile)
                .FirstOrDefaultAsync(s => s.Id == ticket.WorkshopSessionId.Value, cancellationToken);
        }

        if (booking.WorkshopPassTypeId.HasValue && booking.WorkshopPassType == null)
        {
            booking.WorkshopPassType = await _dbContext.WorkshopPassTypes
                .FirstOrDefaultAsync(p => p.Id == booking.WorkshopPassTypeId.Value, cancellationToken);
        }

        // QR Token resolution: strictly raw token if provided, else derived cryptographic QR token, never TicketNumber
        var qrToken = !string.IsNullOrWhiteSpace(rawQrToken)
            ? rawQrToken
            : _ticketService.DeriveQrToken(ticket);

        var bookingRef = "BK-" + booking.Id.ToString()[..8].ToUpperInvariant();

        var effDate = ticket.WorkshopSession?.SessionDate ?? workshop.WorkshopDate;
        var effStartTime = ticket.WorkshopSession?.StartTime ?? workshop.StartTime;
        var effEndTime = ticket.WorkshopSession?.EndTime ?? workshop.EndTime;

        var startTimeStr = DateTime.Today.Add(effStartTime).ToString("h:mm tt");
        var endTimeStr = DateTime.Today.Add(effEndTime).ToString("h:mm tt");
        var timeDisplay = $"{startTimeStr} – {endTimeStr}";
        var dateDisplay = effDate.ToString("dd MMMM yyyy");

        var venueName = workshop.Venue ?? "Ethos Dance Studio";
        var venueAddress = !string.IsNullOrWhiteSpace(workshop.VenueAddress)
            ? workshop.VenueAddress.Trim()
            : (!string.IsNullOrWhiteSpace(workshop.City) ? $"{workshop.City}, India" : "Hyderabad, India");

        string mapsUrl;
        if (!string.IsNullOrWhiteSpace(workshop.LocationUrl))
        {
            mapsUrl = workshop.LocationUrl.Trim();
        }
        else if (workshop.Latitude.HasValue && workshop.Longitude.HasValue)
        {
            mapsUrl = $"https://www.google.com/maps/search/?api=1&query={workshop.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{workshop.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }
        else
        {
            var query = Uri.EscapeDataString($"{venueName}, {venueAddress}".Trim(' ', ','));
            mapsUrl = $"https://www.google.com/maps/search/?api=1&query={query}";
        }

        var passName = booking.PassName ?? booking.WorkshopPassType?.Name;
        var sessionTitle = ticket.WorkshopSession?.Title;
        var trainerName = ticket.WorkshopSession?.TrainerProfile?.FullName;

        return new TicketPdfModel(
            WorkshopTitle: workshop.Title,
            WorkshopDate: dateDisplay,
            WorkshopTime: timeDisplay,
            Venue: venueName,
            AttendeeName: ticket.AttendeeName,
            BookingReference: bookingRef,
            TicketNumber: ticket.TicketNumber,
            QrToken: qrToken,
            Status: "CONFIRMED / ACTIVE",
            VenueAddress: venueAddress,
            MapsUrl: mapsUrl,
            PassName: passName,
            SessionTitle: sessionTitle,
            TrainerName: trainerName);
    }

    private static bool IsSecureHttpsUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
        }
        return "[INVALID_URL]";
    }
}
