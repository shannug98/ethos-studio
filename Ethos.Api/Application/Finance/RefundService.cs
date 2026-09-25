using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ethos.Api.Application.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Application.Finance;

public class RefundService : IRefundService
{
    private readonly AppDbContext _dbContext;
    private readonly RazorpaySettings _razorpaySettings;
    private readonly IAdminAuditService _auditService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RefundService> _logger;
    private readonly HttpClient _httpClient;

    public RefundService(
        AppDbContext dbContext,
        IOptions<RazorpaySettings> razorpaySettings,
        IAdminAuditService auditService,
        IWebHostEnvironment environment,
        ILogger<RefundService> logger,
        HttpClient? httpClient = null)
    {
        _dbContext = dbContext;
        _razorpaySettings = razorpaySettings.Value;
        _auditService = auditService;
        _environment = environment;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri("https://api.razorpay.com/v1/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<RefundResult> RefundPaymentAsync(
        Guid paymentId,
        string reason,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch payment transaction and associated booking
        var payment = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        if (payment == null)
        {
            return new RefundResult
            {
                Success = false,
                PaymentId = paymentId,
                Message = "Payment transaction not found."
            };
        }

        var booking = await _dbContext.WorkshopBookings
            .Include(b => b.Tickets)
            .Include(b => b.StudentProfile)
                .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(b => b.PaymentTransactionId == paymentId || b.Id == payment.ReferenceId, cancellationToken);

        var bookingId = booking?.Id ?? Guid.Empty;

        // 2. Check for existing refunds for this payment
        var existingRefunds = await _dbContext.PaymentRefunds
            .Where(r => r.PaymentId == paymentId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var successfulRefund = existingRefunds.FirstOrDefault(r => r.Status == RefundStatus.Processed);
        if (successfulRefund != null)
        {
            _logger.LogInformation("Payment {PaymentId} has already been refunded via refund {RefundId}.", paymentId, successfulRefund.Id);
            return new RefundResult
            {
                Success = true,
                RefundId = successfulRefund.Id,
                PaymentId = paymentId,
                BookingId = successfulRefund.BookingId,
                AmountPaise = successfulRefund.AmountPaise,
                Status = successfulRefund.Status,
                RazorpayRefundId = successfulRefund.RazorpayRefundId,
                Message = "Payment has already been refunded."
            };
        }

        var inFlightRefund = existingRefunds.FirstOrDefault(r =>
            r.Status == RefundStatus.Processing &&
            r.LastAttemptAtUtc.HasValue &&
            r.LastAttemptAtUtc.Value > DateTime.UtcNow.AddMinutes(-10));

        if (inFlightRefund != null)
        {
            return new RefundResult
            {
                Success = false,
                RefundId = inFlightRefund.Id,
                PaymentId = paymentId,
                BookingId = inFlightRefund.BookingId,
                AmountPaise = inFlightRefund.AmountPaise,
                Status = inFlightRefund.Status,
                Message = "A refund is currently in progress for this payment."
            };
        }

        var reconciliationRefund = existingRefunds.FirstOrDefault(r => r.Status == RefundStatus.ReconciliationRequired);
        if (reconciliationRefund != null)
        {
            return new RefundResult
            {
                Success = false,
                RefundId = reconciliationRefund.Id,
                PaymentId = paymentId,
                BookingId = reconciliationRefund.BookingId,
                AmountPaise = reconciliationRefund.AmountPaise,
                Status = reconciliationRefund.Status,
                FailureReason = reconciliationRefund.FailureReason,
                Message = "Refund requires reconciliation before retry. Gateway status is unconfirmed."
            };
        }

        // Validate payment state
        if (payment.Status != PaymentStatus.Paid)
        {
            return new RefundResult
            {
                Success = false,
                PaymentId = paymentId,
                BookingId = bookingId,
                Message = $"Cannot refund payment with status '{payment.Status}'. Only Paid transactions can be refunded."
            };
        }

        long amountPaise = (long)Math.Round(payment.Amount * 100m, MidpointRounding.AwayFromZero);
        if (amountPaise <= 0)
        {
            return new RefundResult
            {
                Success = false,
                PaymentId = paymentId,
                BookingId = bookingId,
                Message = "Payment amount must be greater than zero."
            };
        }

        // Reuse an existing failed refund record or create a new one
        var activeRefund = existingRefunds.FirstOrDefault(r => r.Status == RefundStatus.Failed);
        if (activeRefund == null)
        {
            activeRefund = new PaymentRefund
            {
                Id = Guid.NewGuid(),
                PaymentId = paymentId,
                BookingId = bookingId,
                AmountPaise = amountPaise,
                Currency = "INR",
                Reason = reason,
                InitiatedByAdminId = adminUserId,
                CreatedAtUtc = DateTime.UtcNow,
                RazorpayPaymentId = payment.RazorpayPaymentId
            };
            _dbContext.PaymentRefunds.Add(activeRefund);
        }

        activeRefund.Status = RefundStatus.Processing;
        activeRefund.ProcessingStartedAtUtc = DateTime.UtcNow;
        activeRefund.LastAttemptAtUtc = DateTime.UtcNow;
        activeRefund.AttemptCount++;
        activeRefund.FailureReason = null;
        activeRefund.Reason = reason;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 3. Perform Razorpay Refund call OUTSIDE of any database transaction
        string? razorpayRefundId = null;
        string? failureError = null;
        bool isTimeoutOrNetworkFailure = false;

        bool isTestOrMockMode =
            _environment.IsDevelopment() ||
            string.IsNullOrWhiteSpace(_razorpaySettings.KeyId) ||
            _razorpaySettings.KeyId.StartsWith("rzp_test_placeholder", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(payment.RazorpayPaymentId) ||
            payment.RazorpayPaymentId.StartsWith("pay_mock", StringComparison.OrdinalIgnoreCase) ||
            payment.RazorpayPaymentId == "mock_payment";

        if (isTestOrMockMode)
        {
            _logger.LogInformation("[RefundService] Test/Mock mode detected. Simulating successful refund for payment {PaymentId}", paymentId);
            razorpayRefundId = $"rfnd_mock_{Guid.NewGuid():N}";
        }
        else
        {
            try
            {
                var payload = new
                {
                    amount = amountPaise,
                    reverse_all = 1,
                    notes = new
                    {
                        booking_id = bookingId.ToString(),
                        admin_id = adminUserId.ToString(),
                        reason = reason
                    }
                };

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"payments/{Uri.EscapeDataString(payment.RazorpayPaymentId!)}/refund");

                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var credentials = $"{_razorpaySettings.KeyId}:{_razorpaySettings.KeySecret}";
                var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        razorpayRefundId = idProp.GetString();
                    }

                    if (string.IsNullOrWhiteSpace(razorpayRefundId))
                    {
                        razorpayRefundId = $"rfnd_unknown_{Guid.NewGuid():N}";
                    }
                }
                else
                {
                    failureError = $"Razorpay HTTP {(int)response.StatusCode}: {responseBody}";
                    _logger.LogError("[RefundService] Razorpay refund failed for payment {PaymentId}. Response: {ResponseBody}", paymentId, responseBody);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                isTimeoutOrNetworkFailure = true;
                failureError = "Gateway call timed out while awaiting refund confirmation.";
                _logger.LogWarning("[RefundService] Timeout during Razorpay refund call for payment {PaymentId}. Flagging for reconciliation.", paymentId);
            }
            catch (HttpRequestException ex)
            {
                isTimeoutOrNetworkFailure = true;
                failureError = $"Network error communicating with Razorpay: {ex.Message}";
                _logger.LogWarning(ex, "[RefundService] Network error during Razorpay refund call for payment {PaymentId}. Flagging for reconciliation.", paymentId);
            }
            catch (Exception ex)
            {
                failureError = $"Unexpected error during Razorpay refund: {ex.Message}";
                _logger.LogError(ex, "[RefundService] Unexpected exception during Razorpay refund for payment {PaymentId}", paymentId);
            }
        }

        // 4. Update database state in atomic transaction
        using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Reload entities to prevent stale overwrites in relational database
            if (_dbContext.Database.IsRelational())
            {
                await _dbContext.Entry(activeRefund).ReloadAsync(cancellationToken);
                await _dbContext.Entry(payment).ReloadAsync(cancellationToken);

                if (booking != null)
                {
                    await _dbContext.Entry(booking).ReloadAsync(cancellationToken);
                }
            }

            if (!string.IsNullOrWhiteSpace(razorpayRefundId))
            {
                activeRefund.Status = RefundStatus.Processed;
                activeRefund.RazorpayRefundId = razorpayRefundId;
                activeRefund.ProcessedAtUtc = DateTime.UtcNow;
                activeRefund.FailureReason = null;

                payment.Status = PaymentStatus.Refunded;
                payment.UpdatedAt = DateTime.UtcNow;

                if (booking != null)
                {
                    booking.Status = WorkshopBookingStatus.Cancelled;
                    booking.CancelledAt = DateTime.UtcNow;

                    // Invalidate tickets
                    var tickets = await _dbContext.WorkshopTickets
                        .Where(t => t.WorkshopBookingId == booking.Id)
                        .ToListAsync(cancellationToken);

                    foreach (var ticket in tickets)
                    {
                        ticket.Status = TicketStatus.Refunded;
                    }

                    // Enqueue WhatsApp cancellation notification
                    var recipientPhone = !string.IsNullOrWhiteSpace(booking.GuestPhone)
                        ? booking.GuestPhone
                        : booking.StudentProfile?.User?.Phone;

                    if (!string.IsNullOrWhiteSpace(recipientPhone))
                    {
                        var idempotencyKey = $"refund-wa-{booking.Id}-{activeRefund.Id}";
                        var exists = await _dbContext.WhatsAppNotifications
                            .AnyAsync(w => w.IdempotencyKey == idempotencyKey, cancellationToken);

                        if (!exists)
                        {
                            var notification = new WhatsAppNotification
                            {
                                Id = Guid.NewGuid(),
                                BookingId = booking.Id,
                                NotificationType = WhatsAppNotificationType.BookingCancelled,
                                RecipientPhone = recipientPhone,
                                IdempotencyKey = idempotencyKey,
                                Status = WhatsAppNotificationStatus.Pending,
                                CreatedAt = DateTime.UtcNow
                            };
                            _dbContext.WhatsAppNotifications.Add(notification);
                        }
                    }
                }

                _auditService.AddAuditLog(
                    adminUserId,
                    "ADMIN_BOOKING_REFUNDED",
                    "PaymentRefund",
                    activeRefund.Id,
                    $"Refunded {amountPaise / 100m:F2} INR for booking {bookingId}. Razorpay Refund ID: {razorpayRefundId}. Reason: {reason}");
            }
            else if (isTimeoutOrNetworkFailure)
            {
                activeRefund.Status = RefundStatus.ReconciliationRequired;
                activeRefund.FailureReason = failureError;

                _auditService.AddAuditLog(
                    adminUserId,
                    "ADMIN_REFUND_RECONCILIATION_REQUIRED",
                    "PaymentRefund",
                    activeRefund.Id,
                    $"Refund for booking {bookingId} timed out or lost connection. Marked ReconciliationRequired. Error: {failureError}");
            }
            else
            {
                activeRefund.Status = RefundStatus.Failed;
                activeRefund.FailureReason = failureError;

                _auditService.AddAuditLog(
                    adminUserId,
                    "ADMIN_REFUND_FAILED",
                    "PaymentRefund",
                    activeRefund.Id,
                    $"Refund for booking {bookingId} failed. Error: {failureError}");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new RefundResult
            {
                Success = !string.IsNullOrWhiteSpace(razorpayRefundId),
                RefundId = activeRefund.Id,
                PaymentId = paymentId,
                BookingId = bookingId,
                AmountPaise = amountPaise,
                Status = activeRefund.Status,
                RazorpayRefundId = razorpayRefundId,
                Message = !string.IsNullOrWhiteSpace(razorpayRefundId)
                    ? "Refund processed successfully."
                    : failureError,
                FailureReason = failureError
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "[RefundService] Failed to commit database state for refund {RefundId}", activeRefund.Id);
            throw;
        }
    }

    public async Task<RefundResult> ProcessRefundJobAsync(
        Guid refundJobId,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.RefundJobs
            .FirstOrDefaultAsync(j => j.Id == refundJobId, cancellationToken);

        if (job == null)
        {
            return new RefundResult
            {
                Success = false,
                Message = "Refund job not found."
            };
        }

        if (job.Status == RefundStatus.Processed)
        {
            return new RefundResult
            {
                Success = true,
                RefundId = job.PaymentRefundId ?? Guid.Empty,
                PaymentId = job.PaymentTransactionId,
                BookingId = job.BookingId,
                AmountPaise = job.AmountPaise,
                Status = RefundStatus.Processed,
                Message = "Refund job already completed."
            };
        }

        var result = await RefundPaymentAsync(
            job.PaymentTransactionId,
            job.Reason,
            job.InitiatedByAdminId,
            cancellationToken);

        job.PaymentRefundId = result.RefundId;
        job.Status = result.Status;
        job.LastError = result.FailureReason;

        if (result.Success)
        {
            job.ProcessedAtUtc = DateTime.UtcNow;
            job.NextRetryUtc = null;
        }
        else if (result.Status == RefundStatus.Failed)
        {
            job.RetryCount++;
            job.NextRetryUtc = DateTime.UtcNow.AddMinutes(Math.Pow(2, Math.Min(job.RetryCount, 5)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }
}
