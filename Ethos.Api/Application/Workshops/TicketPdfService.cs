using System.Security.Cryptography;
using Ethos.Api.Application.Common;
using Ethos.Api.Application.Storage;
using Ethos.Api.Domain.Entities;
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

                return new TicketPdfResult(
                    Success: false,
                    StorageKey: existing.StorageKey,
                    SignedHttpsUrl: null,
                    FileHash: existing.FileHash,
                    ErrorMessage: "Cloudflare R2 is unconfigured or returned a non-HTTPS signed URL. Cannot dispatch to WhatsApp.");
            }

            _logger.LogInformation(
                "Reusing existing TicketPdf record for Ticket {TicketId} (StorageKey: {StorageKey})",
                ticket.Id,
                existing.StorageKey);

            return new TicketPdfResult(
                Success: true,
                StorageKey: existing.StorageKey,
                SignedHttpsUrl: signedUrl,
                FileHash: existing.FileHash);
        }

        // 2. Generate the PDF bytes
        var qrToken = rawQrToken ?? _ticketService.DeriveQrToken(ticket);
        var bookingRef = "BK-" + booking.Id.ToString()[..8].ToUpperInvariant();

        var startTimeStr = DateTime.Today.Add(workshop.StartTime).ToString("h:mm tt");
        var endTimeStr = DateTime.Today.Add(workshop.EndTime).ToString("h:mm tt");
        var timeDisplay = $"{startTimeStr} – {endTimeStr}";
        var dateDisplay = workshop.WorkshopDate.ToString("dd MMMM yyyy");

        var model = new TicketPdfModel(
            WorkshopTitle: workshop.Title,
            WorkshopDate: dateDisplay,
            WorkshopTime: timeDisplay,
            Venue: workshop.Venue ?? "Ethos Dance Studio",
            AttendeeName: ticket.AttendeeName,
            BookingReference: bookingRef,
            TicketNumber: ticket.TicketNumber,
            QrToken: qrToken);

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

            return new TicketPdfResult(
                Success: false,
                StorageKey: storageKey,
                SignedHttpsUrl: null,
                FileHash: fileHash,
                ErrorMessage: "Cloudflare R2 is unconfigured or returned a non-HTTPS URL. Local paths cannot be dispatched to WhatsApp.");
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
                    FileHash: reloaded.FileHash);
            }
        }

        return new TicketPdfResult(
            Success: true,
            StorageKey: storageKey,
            SignedHttpsUrl: freshSignedUrl,
            FileHash: fileHash);
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
