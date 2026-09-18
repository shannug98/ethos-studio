using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Application.Payments;

public class PaymentFulfillmentService : IPaymentFulfillmentService
{
    private readonly AppDbContext _dbContext;
    private readonly IWorkshopTicketService _ticketService;
    private readonly RazorpaySettings _razorpaySettings;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PaymentFulfillmentService> _logger;

    public PaymentFulfillmentService(
        AppDbContext dbContext,
        IWorkshopTicketService ticketService,
        IOptions<RazorpaySettings> razorpaySettings,
        INotificationService notificationService,
        ILogger<PaymentFulfillmentService> logger)
    {
        _dbContext = dbContext;
        _ticketService = ticketService;
        _razorpaySettings = razorpaySettings.Value;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<WorkshopBookingResponse> FulfillWorkshopPaymentAsync(
        PaymentTransaction transaction,
        string razorpayPaymentId,
        string? razorpaySignature,
        string source,
        string? eventId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Idempotency fast path: If transaction is already Paid, return existing booking and tickets
        if (transaction.Status == PaymentStatus.Paid)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} is already Paid. Returning existing fulfillment result safely.",
                transaction.Id);

            var existingBooking = await _dbContext.WorkshopBookings
                .Include(b => b.Workshop)
                .Include(b => b.StudentProfile)
                    .ThenInclude(sp => sp!.User)
                .FirstOrDefaultAsync(b => b.Id == transaction.ReferenceId, cancellationToken);

            if (existingBooking == null || existingBooking.Workshop == null)
            {
                throw new InvalidOperationException("Associated workshop booking was not found.");
            }

            var existingTickets = await _ticketService.GetTicketsForBookingAsync(
                existingBooking.Id,
                transaction.UserId,
                cancellationToken);

            return MapToBookingResponse(existingBooking, existingTickets);
        }

        // 2. Atomic Database Transaction with Row-Level Locking
        using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var booking = await _dbContext.WorkshopBookings
            .Include(b => b.Workshop)
            .Include(b => b.StudentProfile)
                .ThenInclude(sp => sp!.User)
            .FirstOrDefaultAsync(b => b.Id == transaction.ReferenceId, cancellationToken);

        if (booking == null || booking.Workshop == null)
        {
            throw new InvalidOperationException("Associated workshop booking was not found.");
        }

        // PostgreSQL row-level lock on the parent Workshop row to guarantee serialization
        if (_dbContext.Database.IsNpgsql())
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"Workshops\" WHERE \"Id\" = {booking.WorkshopId} FOR UPDATE",
                cancellationToken);
        }

        // Re-check after acquiring lock in case concurrent thread completed it
        if (booking.Status == WorkshopBookingStatus.Confirmed && transaction.Status == PaymentStatus.Paid)
        {
            await dbTx.RollbackAsync(cancellationToken);
            var tickets = await _ticketService.GetTicketsForBookingAsync(booking.Id, transaction.UserId, cancellationToken);
            return MapToBookingResponse(booking, tickets);
        }

        // 3. Invariant check: Confirmed seats must never exceed capacity
        var currentConfirmed = await _dbContext.WorkshopBookings
            .Where(b => b.WorkshopId == booking.WorkshopId &&
                       (b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended))
            .SumAsync(b => b.Quantity, cancellationToken);

        if (currentConfirmed + booking.Quantity > booking.Workshop.Capacity)
        {
            // CRITICAL SAFEGUARD: Do NOT orphan captured payment!
            // Flag transaction as OversoldRefundRequired for automated/manual refund queue
            transaction.Status = PaymentStatus.Failed;
            transaction.RazorpayPaymentId = razorpayPaymentId;
            transaction.UpdatedAt = DateTime.UtcNow;
            booking.Status = WorkshopBookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;

            _dbContext.PaymentEvents.Add(new PaymentEvent
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = transaction.Id,
                EventType = "OversoldRefundRequired",
                Payload = $"Workshop '{booking.Workshop.Title}' ({booking.WorkshopId}) is full. Capacity={booking.Workshop.Capacity}, Confirmed={currentConfirmed}, Requested={booking.Quantity}. Payment {razorpayPaymentId} must be refunded.",
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);

            _logger.LogError(
                "Workshop capacity exceeded for booking {BookingId}. Payment {PaymentId} flagged for refund.",
                booking.Id,
                razorpayPaymentId);

            throw new InvalidOperationException(
                $"Workshop '{booking.Workshop.Title}' reached full capacity before booking was finalized. Payment {razorpayPaymentId} has been flagged for refund.");
        }

        // 4. Update transaction & booking to confirmed state
        transaction.RazorpayPaymentId = razorpayPaymentId;
        if (!string.IsNullOrWhiteSpace(razorpaySignature))
        {
            transaction.RazorpaySignature = razorpaySignature;
        }
        transaction.Status = PaymentStatus.Paid;
        transaction.PaidAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        booking.Status = WorkshopBookingStatus.Confirmed;
        booking.PaymentTransactionId = transaction.Id;
        booking.BookedAt = DateTime.UtcNow;
        booking.CancelledAt = null;

        _dbContext.PaymentEvents.Add(new PaymentEvent
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = transaction.Id,
            EventType = "PaymentFulfilled",
            Payload = $"Payment verified and fulfilled via {source}. RazorpayPaymentId: {razorpayPaymentId}, EventId: {eventId ?? "direct"}",
            CreatedAt = DateTime.UtcNow
        });

        // 5. Issue attendee tickets
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == transaction.UserId, cancellationToken);
        var issuedTickets = await _ticketService.IssueTicketsForBookingAsync(booking, transaction, user, cancellationToken);

        // 6. Enqueue durable WhatsApp Outbox notifications
        var primaryPhone = !string.IsNullOrWhiteSpace(booking.GuestPhone) ? booking.GuestPhone : user?.Phone;
        if (!string.IsNullOrWhiteSpace(primaryPhone))
        {
            var bookingKey = $"wapp_booking_{booking.Id}";
            var hasBookingOutbox = await _dbContext.WhatsAppNotifications
                .AnyAsync(x => x.IdempotencyKey == bookingKey, cancellationToken);

            if (!hasBookingOutbox)
            {
                _dbContext.WhatsAppNotifications.Add(new WhatsAppNotification
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    WorkshopTicketId = null,
                    NotificationType = WhatsAppNotificationType.BookingConfirmed,
                    RecipientPhone = primaryPhone,
                    IdempotencyKey = bookingKey,
                    Status = WhatsAppNotificationStatus.Pending,
                    Attempts = 0,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        foreach (var ticket in issuedTickets)
        {
            var ticketPhone = !string.IsNullOrWhiteSpace(ticket.AttendeePhone) ? ticket.AttendeePhone : primaryPhone;
            if (!string.IsNullOrWhiteSpace(ticketPhone))
            {
                var ticketKey = $"wapp_ticket_{ticket.Id}";
                var hasTicketOutbox = await _dbContext.WhatsAppNotifications
                    .AnyAsync(x => x.IdempotencyKey == ticketKey, cancellationToken);

                if (!hasTicketOutbox)
                {
                    _dbContext.WhatsAppNotifications.Add(new WhatsAppNotification
                    {
                        Id = Guid.NewGuid(),
                        BookingId = booking.Id,
                        WorkshopTicketId = ticket.Id,
                        NotificationType = WhatsAppNotificationType.TicketPdf,
                        RecipientPhone = ticketPhone,
                        IdempotencyKey = ticketKey,
                        Status = WhatsAppNotificationStatus.Pending,
                        Attempts = 0,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await dbTx.CommitAsync(cancellationToken);

        try
        {
            await _notificationService.SendNotificationAsync(new CreateNotificationRequest
            {
                UserId = transaction.UserId,
                Type = NotificationType.Workshop,
                Title = "Workshop Booking Confirmed! 🌟",
                Message = $"Your seat for \"{booking.Workshop.Title}\" on {booking.Workshop.WorkshopDate:dd MMM yyyy} is confirmed! Venue: {booking.Workshop.Venue ?? "Main Studio"}.",
                Channel = NotificationChannel.InApp,
                ActionUrl = "/student/workshops",
                EventKey = $"WorkshopBookingConfirmed:{booking.Id}",
                SendExternal = true,
                RecipientEmail = user?.Email,
                RecipientPhone = user?.Phone
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send in-app notification for booking {BookingId}", booking.Id);
        }

        _logger.LogInformation(
            "Successfully fulfilled workshop booking {BookingId} for transaction {TransactionId} via {Source}.",
            booking.Id,
            transaction.Id,
            source);

        return MapToBookingResponse(booking, issuedTickets);
    }

    public async Task FulfillNonWorkshopPaymentAsync(
        PaymentTransaction transaction,
        string razorpayPaymentId,
        string source,
        string? eventId = null,
        CancellationToken cancellationToken = default)
    {
        if (transaction.Status == PaymentStatus.Paid)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} already paid. Skipping redundant non-workshop fulfillment.",
                transaction.Id);
            return;
        }

        using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        transaction.RazorpayPaymentId = razorpayPaymentId;
        transaction.Status = PaymentStatus.Paid;
        transaction.PaidAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        _dbContext.PaymentEvents.Add(new PaymentEvent
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = transaction.Id,
            EventType = "PaymentFulfilled",
            Payload = $"Non-workshop payment fulfilled via {source}. Purpose: {transaction.Purpose}, EventId: {eventId ?? "direct"}",
            CreatedAt = DateTime.UtcNow
        });

        switch (transaction.Purpose)
        {
            case PaymentPurpose.PackagePurchase:
                await FulfillPackageInternalAsync(transaction, cancellationToken);
                break;

            case PaymentPurpose.TrainerApplication:
                await FulfillTrainerApplicationInternalAsync(transaction, cancellationToken);
                break;

            case PaymentPurpose.TrainerTierUpgrade:
                await FulfillTrainerTierUpgradeInternalAsync(transaction, cancellationToken);
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await dbTx.CommitAsync(cancellationToken);
    }

    public async Task<WebhookFulfillmentResult> ProcessWebhookEventAsync(
        string rawBody,
        string? signatureHeader,
        CancellationToken cancellationToken = default)
    {
        // 1. Signature Verification
        if (string.IsNullOrWhiteSpace(_razorpaySettings.WebhookSecret))
        {
            _logger.LogError("Razorpay:WebhookSecret is not configured. Webhook cannot be processed.");
            throw new InvalidOperationException("Razorpay:WebhookSecret is not configured.");
        }

        if (!VerifyWebhookSignature(rawBody, signatureHeader, _razorpaySettings.WebhookSecret))
        {
            _logger.LogWarning("Invalid Razorpay webhook signature received.");
            throw new UnauthorizedAccessException("Invalid Razorpay webhook signature.");
        }

        // 2. Parse payload
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        var eventType = root.TryGetProperty("event", out var evProp) ? evProp.GetString() : null;
        var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

        _logger.LogInformation("Processing Razorpay webhook event: {EventType}, EventId: {EventId}", eventType, eventId);

        // We only process captured payments or paid orders
        if (eventType != "payment.captured" && eventType != "order.paid")
        {
            return new WebhookFulfillmentResult(
                Success: true,
                Status: "Ignored",
                Message: $"Event type '{eventType}' is ignored.");
        }

        if (!root.TryGetProperty("payload", out var payloadProp) ||
            !payloadProp.TryGetProperty("payment", out var paymentProp) ||
            !paymentProp.TryGetProperty("entity", out var paymentEntity))
        {
            return new WebhookFulfillmentResult(
                Success: false,
                Status: "Malformed",
                Message: "Webhook payload is missing payment entity.");
        }

        var paymentId = paymentEntity.GetProperty("id").GetString() ?? "";
        var orderId = paymentEntity.TryGetProperty("order_id", out var op) ? op.GetString() : null;
        var amountPaise = paymentEntity.GetProperty("amount").GetInt64();
        var currency = paymentEntity.GetProperty("currency").GetString() ?? "INR";
        var paymentStatus = paymentEntity.GetProperty("status").GetString() ?? "";

        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return new WebhookFulfillmentResult(
                Success: false,
                Status: "Malformed",
                Message: "Payment entity has no payment ID.");
        }

        // 3. Correlate with local PaymentTransaction
        var transaction = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(
                t => (!string.IsNullOrEmpty(orderId) && t.RazorpayOrderId == orderId) ||
                     t.RazorpayPaymentId == paymentId,
                cancellationToken);

        if (transaction == null)
        {
            _logger.LogWarning(
                "PaymentTransaction for order {OrderId} or payment {PaymentId} not found in database.",
                orderId,
                paymentId);

            return new WebhookFulfillmentResult(
                Success: true,
                Status: "NotFound",
                Message: "No matching payment transaction found. Possibly non-studio order.");
        }

        // 4. Idempotency Check: Already Paid
        if (transaction.Status == PaymentStatus.Paid)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} was already paid. Webhook completed idempotently.",
                transaction.Id);

            return new WebhookFulfillmentResult(
                Success: true,
                Status: "AlreadyPaid",
                Message: "Transaction already processed.",
                TransactionId: transaction.Id);
        }

        // 5. Server-side validation of amount and currency
        var expectedPaise = ConvertToPaise(transaction.Amount);
        if (amountPaise != expectedPaise)
        {
            _logger.LogError(
                "Webhook payment amount mismatch for transaction {TransactionId}. Expected: {Expected}, Received: {Received}",
                transaction.Id,
                expectedPaise,
                amountPaise);

            throw new InvalidOperationException("Payment amount mismatch between webhook and local transaction.");
        }

        if (!string.Equals(currency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "Webhook currency mismatch for transaction {TransactionId}. Expected: {Expected}, Received: {Received}",
                transaction.Id,
                transaction.Currency,
                currency);

            throw new InvalidOperationException("Payment currency mismatch between webhook and local transaction.");
        }

        // 6. Route to unified fulfillment
        if (transaction.Purpose == PaymentPurpose.WorkshopBooking)
        {
            await FulfillWorkshopPaymentAsync(
                transaction,
                paymentId,
                razorpaySignature: null,
                source: "Webhook",
                eventId: eventId,
                cancellationToken: cancellationToken);
        }
        else
        {
            await FulfillNonWorkshopPaymentAsync(
                transaction,
                paymentId,
                source: "Webhook",
                eventId: eventId,
                cancellationToken: cancellationToken);
        }

        return new WebhookFulfillmentResult(
            Success: true,
            Status: "Fulfilled",
            Message: "Payment successfully fulfilled via webhook.",
            TransactionId: transaction.Id);
    }

    public static bool VerifyWebhookSignature(string rawBody, string? signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(rawBody) || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var expectedHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedHex));
    }

    private static long ConvertToPaise(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }
        return (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
    }

    private static WorkshopBookingResponse MapToBookingResponse(
        WorkshopBooking booking,
        IReadOnlyList<WorkshopTicketResponse> tickets)
    {
        return new WorkshopBookingResponse
        {
            Id = booking.Id,
            WorkshopId = booking.WorkshopId,
            WorkshopTitle = booking.Workshop?.Title ?? "Workshop",
            WorkshopDate = booking.Workshop?.WorkshopDate ?? DateTime.UtcNow,
            StartTime = booking.Workshop?.StartTime ?? TimeSpan.Zero,
            EndTime = booking.Workshop?.EndTime ?? TimeSpan.Zero,
            Venue = booking.Workshop?.Venue ?? string.Empty,
            Quantity = booking.Quantity,
            Price = booking.Quantity > 0 ? booking.TotalPrice / booking.Quantity : booking.TotalPrice,
            TotalPrice = booking.TotalPrice,
            BookingReference = "BK-" + booking.Id.ToString()[..8].ToUpperInvariant(),
            CustomerName = booking.GuestName ?? booking.StudentProfile?.User?.FullName ?? "Ethos Student",
            CustomerPhone = booking.GuestPhone ?? booking.StudentProfile?.User?.Phone ?? "",
            CustomerEmail = booking.GuestEmail ?? booking.StudentProfile?.User?.Email ?? "",
            Status = booking.Status,
            BookedAt = booking.BookedAt,
            Tickets = tickets.ToList()
        };
    }

    private async Task FulfillPackageInternalAsync(PaymentTransaction transaction, CancellationToken cancellationToken)
    {
        var alreadyFulfilled = await _dbContext.StudentPackages
            .AnyAsync(sp => sp.PaymentTransactionId == transaction.Id, cancellationToken);

        if (alreadyFulfilled) return;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == transaction.UserId, cancellationToken);

        if (studentProfile == null) return;

        var package = await _dbContext.Packages
            .FirstOrDefaultAsync(p => p.Id == transaction.ReferenceId && p.IsActive, cancellationToken);

        if (package == null) return;

        var now = DateTime.UtcNow;
        var activePackage = await _dbContext.StudentPackages
            .Where(sp => sp.StudentProfileId == studentProfile.Id &&
                         sp.Status == StudentPackageStatus.Active &&
                         sp.ExpiryDate > now)
            .OrderByDescending(sp => sp.ExpiryDate)
            .FirstOrDefaultAsync(cancellationToken);

        var startDate = activePackage?.ExpiryDate ?? now;
        var expiryDate = startDate.AddDays(package.DurationDays);

        var studentPackage = new StudentPackage
        {
            Id = Guid.NewGuid(),
            StudentProfileId = studentProfile.Id,
            PackageId = package.Id,
            PaymentTransactionId = transaction.Id,
            StartDate = startDate,
            ExpiryDate = expiryDate,
            Status = activePackage != null ? StudentPackageStatus.Pending : StudentPackageStatus.Active,
            ClassesAllowed = package.ClassLimit,
            ClassesUsed = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.StudentPackages.Add(studentPackage);
    }

    private async Task FulfillTrainerApplicationInternalAsync(PaymentTransaction transaction, CancellationToken cancellationToken)
    {
        var application = await _dbContext.TrainerApplications
            .FirstOrDefaultAsync(a => a.Id == transaction.ReferenceId, cancellationToken);

        if (application != null)
        {
            application.PaymentTransactionId = transaction.Id;
            application.Status = TrainerApplicationStatus.PaymentVerified;
            application.PaymentVerifiedAt = DateTime.UtcNow;
            application.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task FulfillTrainerTierUpgradeInternalAsync(PaymentTransaction transaction, CancellationToken cancellationToken)
    {
        var upgradeReq = await _dbContext.TrainerUpgradeRequests
            .FirstOrDefaultAsync(u => u.Id == transaction.ReferenceId, cancellationToken);

        if (upgradeReq != null)
        {
            upgradeReq.Status = TrainerUpgradeRequestStatus.PaymentVerified;
        }
    }
}
