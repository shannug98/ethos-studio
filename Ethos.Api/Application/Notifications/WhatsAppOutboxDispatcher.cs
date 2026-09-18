using Ethos.Api.Application.Common;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Application.Notifications;

public class WhatsAppOutboxDispatcher : IWhatsAppOutboxDispatcher
{
    private readonly AppDbContext _dbContext;
    private readonly IMsg91WhatsAppService _msg91Service;
    private readonly ITicketPdfService _ticketPdfService;
    private readonly Msg91Options _options;
    private readonly ILogger<WhatsAppOutboxDispatcher> _logger;

    public WhatsAppOutboxDispatcher(
        AppDbContext dbContext,
        IMsg91WhatsAppService msg91Service,
        ITicketPdfService ticketPdfService,
        IOptions<Msg91Options> options,
        ILogger<WhatsAppOutboxDispatcher> logger)
    {
        _dbContext = dbContext;
        _msg91Service = msg91Service;
        _ticketPdfService = ticketPdfService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> RecoverAbandonedLeasesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var abandoned = await _dbContext.WhatsAppNotifications
            .Where(x => x.Status == WhatsAppNotificationStatus.Sending && x.LeaseExpiresAt != null && x.LeaseExpiresAt < now)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (abandoned.Count == 0) return 0;

        foreach (var item in abandoned)
        {
            if (item.Attempts >= _options.MaxRetryAttempts)
            {
                item.Status = WhatsAppNotificationStatus.Failed;
                item.LastError = "Abandoned lease exceeded maximum attempt limit.";
                item.NextAttemptAt = null;
            }
            else
            {
                item.Status = WhatsAppNotificationStatus.Pending;
                item.NextAttemptAt = now.AddMinutes(Math.Pow(2, item.Attempts));
            }

            item.LeaseExpiresAt = null;
            item.LockedByWorkerId = null;

            _logger.LogWarning(
                "[WhatsApp Outbox] Recovered abandoned lease for notification {NotificationId} (Attempts: {Attempts})",
                item.Id,
                item.Attempts);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return abandoned.Count;
    }

    public async Task<int> ProcessPendingBatchAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var batchSize = Math.Clamp(_options.BatchSize, 1, 50);

        // 1. Claim records atomically
        var candidateIds = await _dbContext.WhatsAppNotifications
            .Where(x =>
                (x.Status == WhatsAppNotificationStatus.Pending ||
                 (x.Status == WhatsAppNotificationStatus.Failed && x.Attempts < _options.MaxRetryAttempts && (x.NextAttemptAt == null || x.NextAttemptAt <= now))) &&
                (x.LeaseExpiresAt == null || x.LeaseExpiresAt < now))
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0) return 0;

        var leaseDuration = TimeSpan.FromSeconds(Math.Clamp(_options.LeaseDurationSeconds, 15, 300));
        var claimedRecords = await _dbContext.WhatsAppNotifications
            .Include(x => x.WorkshopBooking)
                .ThenInclude(b => b.Workshop)
            .Include(x => x.WorkshopTicket)
            .Where(x => candidateIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var record in claimedRecords)
        {
            record.Status = WhatsAppNotificationStatus.Sending;
            record.LeaseExpiresAt = now.Add(leaseDuration);
            record.LockedByWorkerId = workerId;
            record.Attempts += 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        int processedCount = 0;

        // 2. Dispatch each claimed record
        foreach (var record in claimedRecords)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                if (record.NotificationType == WhatsAppNotificationType.BookingConfirmed)
                {
                    await DispatchBookingConfirmedAsync(record, cancellationToken);
                }
                else if (record.NotificationType == WhatsAppNotificationType.TicketPdf)
                {
                    await DispatchTicketPdfAsync(record, cancellationToken);
                }

                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[WhatsApp Outbox] Unhandled exception processing notification {NotificationId} ({Type})",
                    record.Id,
                    record.NotificationType);

                record.Status = WhatsAppNotificationStatus.Failed;
                record.LastError = $"Worker exception: {ex.Message}";
                record.LeaseExpiresAt = null;
                record.LockedByWorkerId = null;
                record.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, Math.Min(record.Attempts, 5)));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return processedCount;
    }

    private async Task DispatchBookingConfirmedAsync(
        WhatsAppNotification record,
        CancellationToken cancellationToken)
    {
        var booking = record.WorkshopBooking;
        if (booking == null || booking.Workshop == null)
        {
            record.Status = WhatsAppNotificationStatus.Failed;
            record.LastError = "Associated booking or workshop record was not found.";
            record.LeaseExpiresAt = null;
            record.LockedByWorkerId = null;
            return;
        }

        var workshop = booking.Workshop;
        var attendeeName = !string.IsNullOrWhiteSpace(booking.GuestName)
            ? booking.GuestName
            : (booking.StudentProfile?.User?.FullName ?? "Ethos Student");

        var startTimeStr = DateTime.Today.Add(workshop.StartTime).ToString("h:mm tt");
        var endTimeStr = DateTime.Today.Add(workshop.EndTime).ToString("h:mm tt");
        var timeDisplay = $"{startTimeStr} – {endTimeStr}";
        var dateDisplay = workshop.WorkshopDate.ToString("dd MMMM yyyy");
        var bookingRef = "BK-" + booking.Id.ToString()[..8].ToUpperInvariant();

        var data = new BookingConfirmedData(
            AttendeeName: attendeeName,
            WorkshopTitle: workshop.Title,
            WorkshopDate: dateDisplay,
            WorkshopTime: timeDisplay,
            BookingId: bookingRef);

        var result = await _msg91Service.SendBookingConfirmedAsync(
            data,
            record.RecipientPhone,
            cancellationToken);

        ApplyDispatchResult(record, result, attendeeName);
    }

    private async Task DispatchTicketPdfAsync(
        WhatsAppNotification record,
        CancellationToken cancellationToken)
    {
        var booking = record.WorkshopBooking;
        var ticket = record.WorkshopTicket;

        if (booking == null || ticket == null || ticket.Workshop == null)
        {
            record.Status = WhatsAppNotificationStatus.Failed;
            record.LastError = "Associated workshop ticket or booking was not found.";
            record.LeaseExpiresAt = null;
            record.LockedByWorkerId = null;
            return;
        }

        var workshop = ticket.Workshop;

        // Obtain or create durable TicketPdf with valid HTTPS signed URL
        var pdfResult = await _ticketPdfService.GetOrCreateTicketPdfAsync(
            ticket,
            workshop,
            booking,
            rawQrToken: null,
            cancellationToken: cancellationToken);

        if (!pdfResult.Success || string.IsNullOrWhiteSpace(pdfResult.SignedHttpsUrl))
        {
            _logger.LogWarning(
                "[WhatsApp Outbox] TicketPdf acquisition failed for Ticket {TicketId}: {Error}",
                ticket.Id,
                pdfResult.ErrorMessage);

            // Re-attempt later when R2 or document rendering is restored
            record.Status = WhatsAppNotificationStatus.Failed;
            record.LastError = pdfResult.ErrorMessage ?? "PDF URL could not be generated or is non-HTTPS.";
            record.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(30, Math.Pow(2, record.Attempts)));
            record.LeaseExpiresAt = null;
            record.LockedByWorkerId = null;
            return;
        }

        var startTimeStr = DateTime.Today.Add(workshop.StartTime).ToString("h:mm tt");
        var endTimeStr = DateTime.Today.Add(workshop.EndTime).ToString("h:mm tt");
        var timeDisplay = $"{startTimeStr} – {endTimeStr}";
        var dateDisplay = workshop.WorkshopDate.ToString("dd MMMM yyyy");
        var bookingRef = "BK-" + booking.Id.ToString()[..8].ToUpperInvariant();

        var ticketData = new TicketPdfData(
            AttendeeName: ticket.AttendeeName,
            WorkshopTitle: workshop.Title,
            WorkshopDate: dateDisplay,
            WorkshopTime: timeDisplay,
            BookingId: bookingRef,
            PdfHttpsUrl: pdfResult.SignedHttpsUrl,
            FileName: $"{ticket.TicketNumber}.pdf");

        var result = await _msg91Service.SendTicketPdfAsync(
            ticketData,
            record.RecipientPhone,
            cancellationToken);

        ApplyDispatchResult(record, result, ticket.AttendeeName);
    }

    private void ApplyDispatchResult(
        WhatsAppNotification record,
        Msg91DispatchResult result,
        string attendeeName)
    {
        record.LeaseExpiresAt = null;
        record.LockedByWorkerId = null;

        if (result.Success)
        {
            record.Status = WhatsAppNotificationStatus.Sent;
            record.SentAt = DateTime.UtcNow;
            record.ProviderMessageId = result.ProviderMessageId;
            record.ProviderRequestId = result.ProviderRequestId;
            record.LastError = null;

            // Update ticket if present
            if (record.WorkshopTicket != null)
            {
                record.WorkshopTicket.WhatsAppSent = true;
            }

            // Log communication record for studio audit trail
            _dbContext.CommunicationLogs.Add(new CommunicationLog
            {
                Id = Guid.NewGuid(),
                MessageReference = $"WAPP-{record.Id.ToString()[..8].ToUpperInvariant()}",
                Channel = "WHATSAPP",
                Recipient = MaskPhone(record.RecipientPhone),
                RecipientUserId = record.WorkshopBooking?.StudentProfile?.UserId,
                TemplateId = record.NotificationType == WhatsAppNotificationType.BookingConfirmed
                    ? _options.BookingConfirmedTemplateName
                    : _options.TicketPdfTemplateName,
                Subject = null,
                BodyPreview = $"WhatsApp {record.NotificationType} sent to {attendeeName} ({MaskPhone(record.RecipientPhone)})",
                Status = "SENT",
                Provider = "MSG91",
                ProviderMessageId = result.ProviderMessageId ?? result.ProviderRequestId,
                ErrorMessage = null,
                RetryCount = record.Attempts - 1,
                IdempotencyKey = record.IdempotencyKey,
                TraceId = record.Id.ToString(),
                CreatedAt = DateTime.UtcNow,
                DeliveredAt = DateTime.UtcNow
            });
        }
        else if (result.IsTimeout)
        {
            // Requirement 3: Ambiguous timeout must be held in review and NOT automatically retried as normal failure
            record.Status = WhatsAppNotificationStatus.AmbiguousTimeout;
            record.LastError = result.ErrorMessage ?? "Provider request timed out. Message state is ambiguous; held in review.";
            record.NextAttemptAt = null; // Do not automatically retry
        }
        else if (result.IsTransientError)
        {
            record.Status = WhatsAppNotificationStatus.Failed;
            record.LastError = result.ErrorMessage;

            if (record.Attempts < _options.MaxRetryAttempts)
            {
                record.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(30, Math.Pow(2, record.Attempts)));
            }
            else
            {
                record.NextAttemptAt = null;
            }
        }
        else if (result.IsPermanentError)
        {
            record.Status = WhatsAppNotificationStatus.Failed;
            record.LastError = result.ErrorMessage;
            record.NextAttemptAt = null; // Do not retry permanent failures
        }
        else if (result.Skipped)
        {
            record.Status = WhatsAppNotificationStatus.Skipped;
            record.LastError = result.SkipReason;
            record.NextAttemptAt = null;
        }
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "[EMPTY]";
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "******";
        return $"{new string('*', digits.Length - 4)}{digits[^4..]}";
    }
}
