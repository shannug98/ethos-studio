using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Payments;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Application.Payments;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notificationService;
    private readonly RazorpaySettings _razorpaySettings;
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _environment;

    public PaymentService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        INotificationService notificationService,
        IOptions<RazorpaySettings> razorpaySettings,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _razorpaySettings = razorpaySettings.Value;
        _environment = environment;

        if (string.IsNullOrWhiteSpace(_razorpaySettings.KeyId) ||
            string.IsNullOrWhiteSpace(_razorpaySettings.KeySecret))
        {
            throw new InvalidOperationException(
                "Razorpay KeyId and KeySecret are missing from server configuration.");
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.razorpay.com/v1/")
        };
    }


    public async Task<PaymentTransactionResponse> CreateOrderAsync(
        CreatePaymentOrderRequest request)
    {
        var userId = _currentUser.UserId;

        decimal amount;

        switch (request.Purpose)
        {
            case PaymentPurpose.PackagePurchase:
            {
                var package = await _dbContext.Packages
                    .FirstOrDefaultAsync(
                        p => p.Id == request.ReferenceId &&
                             p.IsActive);

                if (package == null)
                {
                    throw new ArgumentException(
                        "Specified package was not found or is inactive.");
                }

                amount = package.Price;
                break;
            }

            case PaymentPurpose.WorkshopBooking:
            {
                var workshop = await _dbContext.Workshops
                    .FirstOrDefaultAsync(
                        w => w.Id == request.ReferenceId &&
                             w.Status == WorkshopStatus.Approved);

                if (workshop == null)
                {
                    throw new ArgumentException(
                        "Specified workshop was not found or is not approved.");
                }

                if (!workshop.AdminApprovedPrice.HasValue || workshop.AdminApprovedPrice.Value <= 0)
                {
                    throw new InvalidOperationException(
                        "Specified workshop does not have an approved price set by admin.");
                }

                amount = workshop.AdminApprovedPrice.Value;
                break;
            }

            case PaymentPurpose.TrainerApplication:
            {
                var application = await _dbContext.TrainerApplications
                    .Include(a => a.TrainerProfile)
                    .ThenInclude(p => p.CurrentTier)
                    .FirstOrDefaultAsync(
                        a => a.Id == request.ReferenceId &&
                             a.TrainerProfile.UserId == userId);

                if (application == null)
                {
                    throw new ArgumentException(
                        "Specified trainer application was not found or access denied.");
                }

                var tier = application.TrainerProfile.CurrentTier;
                if (tier == null || !tier.ApplicationFee.HasValue)
                {
                    throw new InvalidOperationException(
                        "Selected trainer tier does not have a configured application fee.");
                }

                amount = tier.ApplicationFee.Value;
                break;
            }

            case PaymentPurpose.TrainerTierUpgrade:
            {
                var upgradeReq = await _dbContext.TrainerUpgradeRequests
                    .Include(u => u.RequestedTier)
                    .Include(u => u.TrainerProfile)
                    .FirstOrDefaultAsync(
                        u => u.Id == request.ReferenceId &&
                             u.TrainerProfile.UserId == userId);

                if (upgradeReq == null)
                {
                    throw new ArgumentException(
                        "Specified trainer upgrade request was not found or access denied.");
                }

                var tier = upgradeReq.RequestedTier;
                if (!tier.UpgradeFee.HasValue)
                {
                    throw new InvalidOperationException(
                        "Requested tier does not have a configured upgrade fee.");
                }

                amount = tier.UpgradeFee.Value;
                break;
            }

            default:
                throw new ArgumentException(
                    $"Payment purpose {request.Purpose} is not supported.");
        }

        if (amount <= 0)
        {
            throw new InvalidOperationException(
                "Payment amount must be greater than zero.");
        }

        var transactionId = Guid.NewGuid();

        var razorpayOrderId = await CreateRazorpayOrderAsync(
            transactionId,
            amount,
            request.Purpose);

        var now = DateTime.UtcNow;

        var transaction = new PaymentTransaction
        {
            Id = transactionId,
            UserId = userId,
            Purpose = request.Purpose,
            ReferenceId = request.ReferenceId,
            Amount = amount,
            Currency = "INR",
            Status = PaymentStatus.OrderCreated,
            RazorpayOrderId = razorpayOrderId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var paymentEvent = new PaymentEvent
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = transaction.Id,
            EventType = "OrderCreated",
            Payload =
                $"Razorpay order created: {razorpayOrderId}. " +
                $"Purpose: {request.Purpose}. Amount: {amount} INR.",
            CreatedAt = now
        };

        _dbContext.PaymentTransactions.Add(transaction);
        _dbContext.PaymentEvents.Add(paymentEvent);

        await _dbContext.SaveChangesAsync();

        return MapToResponse(transaction);
    }

    public async Task<PaymentTransactionResponse> VerifyPaymentAsync(
        VerifyPaymentRequest request)
    {
        var userId = _currentUser.UserId;

        var transaction = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(
                t => t.Id == request.TransactionId &&
                     t.UserId == userId);

        if (transaction == null)
        {
            throw new ArgumentException(
                "Payment transaction not found.");
        }

        if (transaction.Status == PaymentStatus.Paid)
        {
            if (!string.Equals(
                    transaction.RazorpayPaymentId,
                    request.RazorpayPaymentId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "This transaction has already been completed with a different payment.");
            }

            return MapToResponse(transaction);
        }

        if (string.IsNullOrWhiteSpace(transaction.RazorpayOrderId))
        {
            throw new InvalidOperationException(
                "Payment transaction does not contain a Razorpay order ID.");
        }

        if (!string.Equals(
                transaction.RazorpayOrderId,
                request.RazorpayOrderId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Razorpay Order ID does not match the payment transaction.");
        }

        if (string.IsNullOrWhiteSpace(request.RazorpayPaymentId) ||
            string.IsNullOrWhiteSpace(request.RazorpaySignature))
        {
            throw new ArgumentException(
                "Razorpay payment ID and signature are required.");
        }

        bool signatureValid;
        if (_environment.IsDevelopment() &&
            (request.RazorpaySignature == "mock_sig" ||
             request.RazorpaySignature == "mock_signature" ||
             request.RazorpayPaymentId.Contains("mock") ||
             request.RazorpayPaymentId.StartsWith("pay_e2e_")))
        {
            // Development-only: allow mock signatures for local testing
            signatureValid = true;
        }
        else
        {
            signatureValid = VerifyRazorpaySignature(
                request.RazorpayOrderId,
                request.RazorpayPaymentId,
                request.RazorpaySignature,
                _razorpaySettings.KeySecret);
        }

        if (!signatureValid)
        {
            transaction.Status = PaymentStatus.Failed;
            transaction.UpdatedAt = DateTime.UtcNow;

            _dbContext.PaymentEvents.Add(new PaymentEvent
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = transaction.Id,
                EventType = "SignatureVerificationFailed",
                Payload =
                    $"Invalid Razorpay signature for order " +
                    $"{request.RazorpayOrderId}.",
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            throw new InvalidOperationException(
                "Razorpay payment signature verification failed.");
        }

        RazorpayPaymentResponse razorpayPayment;
        if (_environment.IsDevelopment() &&
            (request.RazorpayPaymentId.Contains("mock") ||
             request.RazorpayPaymentId.StartsWith("pay_e2e_")))
        {
            // Development-only: simulate captured payment without calling Razorpay API
            razorpayPayment = new RazorpayPaymentResponse
            {
                Id = request.RazorpayPaymentId,
                OrderId = transaction.RazorpayOrderId,
                Amount = ConvertToPaise(transaction.Amount),
                Currency = transaction.Currency,
                Status = "captured"
            };
        }
        else
        {
            razorpayPayment = await GetRazorpayPaymentAsync(request.RazorpayPaymentId);
        }


        if (!string.Equals(
                razorpayPayment.OrderId,
                transaction.RazorpayOrderId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Razorpay payment does not belong to the expected order.");
        }

        var expectedAmountPaise = ConvertToPaise(transaction.Amount);

        if (razorpayPayment.Amount != expectedAmountPaise)
        {
            throw new InvalidOperationException(
                "Razorpay payment amount does not match the expected transaction amount.");
        }

        if (!string.Equals(
                razorpayPayment.Currency,
                transaction.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Razorpay payment currency does not match the transaction currency.");
        }

        if (!string.Equals(
                razorpayPayment.Status,
                "captured",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Razorpay payment is not captured. Current status: {razorpayPayment.Status}.");
        }

        transaction.RazorpayPaymentId = request.RazorpayPaymentId;
        transaction.RazorpaySignature = request.RazorpaySignature;
        transaction.Status = PaymentStatus.Paid;
        transaction.PaidAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        _dbContext.PaymentEvents.Add(new PaymentEvent
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = transaction.Id,
            EventType = "PaymentVerified",
            Payload =
                $"Razorpay payment captured and verified: " +
                $"{request.RazorpayPaymentId}.",
            CreatedAt = DateTime.UtcNow
        });

        await FulfillPaymentAsync(transaction, userId);

        await _dbContext.SaveChangesAsync();

        if (transaction.Purpose == PaymentPurpose.PackagePurchase)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var package = await _dbContext.Packages.FirstOrDefaultAsync(p => p.Id == transaction.ReferenceId);
            if (user != null && package != null)
            {
                await _notificationService.SendNotificationAsync(new CreateNotificationRequest
                {
                    UserId = userId,
                    Type = NotificationType.Package,
                    Title = "Monthly Package Activated! 🎟️",
                    Message = $"Your {package.Name} package is now active with {package.ClassLimit} classes valid for {package.DurationDays} days. Welcome to Ethos!",
                    Channel = NotificationChannel.InApp,
                    ActionUrl = "/student/classes",
                    EventKey = $"PackagePurchase:{transaction.Id}",
                    SendExternal = true,
                    RecipientEmail = user.Email,
                    RecipientPhone = user.Phone
                });
            }
        }

        return MapToResponse(transaction);
    }

    public async Task<IReadOnlyList<PaymentTransactionResponse>>
        GetMyPaymentsAsync()
    {
        var userId = _currentUser.UserId;

        var transactions = await _dbContext.PaymentTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return transactions
            .Select(MapToResponse)
            .ToList();
    }

    public async Task<PaymentTransactionResponse?> GetPaymentByIdAsync(
        Guid id)
    {
        var userId = _currentUser.UserId;

        var transaction = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(
                t => t.Id == id &&
                     t.UserId == userId);

        return transaction == null
            ? null
            : MapToResponse(transaction);
    }

    private async Task<string> CreateRazorpayOrderAsync(
        Guid transactionId,
        decimal amount,
        PaymentPurpose purpose)
    {
        var amountInPaise = ConvertToPaise(amount);

        var payload = new
        {
            amount = amountInPaise,
            currency = "INR",
            receipt = $"rcpt_{transactionId:N}",
            notes = new
            {
                transactionId = transactionId.ToString(),
                purpose = purpose.ToString()
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "orders");

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        AddBasicAuthentication(request);

        using var response = await _httpClient.SendAsync(request);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Razorpay order creation failed. " +
                $"HTTP {(int)response.StatusCode}: {responseBody}");
        }

        using var document =
            JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty(
                "id",
                out var idProperty))
        {
            throw new InvalidOperationException(
                "Razorpay response did not contain an order ID.");
        }

        var orderId = idProperty.GetString();

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new InvalidOperationException(
                "Razorpay returned an empty order ID.");
        }

        return orderId;
    }

    private async Task<RazorpayPaymentResponse>
        GetRazorpayPaymentAsync(string paymentId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"payments/{Uri.EscapeDataString(paymentId)}");

        AddBasicAuthentication(request);

        using var response = await _httpClient.SendAsync(request);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Unable to retrieve Razorpay payment. " +
                $"HTTP {(int)response.StatusCode}: {responseBody}");
        }

        var payment =
            JsonSerializer.Deserialize<RazorpayPaymentResponse>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (payment == null)
        {
            throw new InvalidOperationException(
                "Invalid response received from Razorpay.");
        }

        return payment;
    }

    private void AddBasicAuthentication(
        HttpRequestMessage request)
    {
        var credentials =
            $"{_razorpaySettings.KeyId}:{_razorpaySettings.KeySecret}";

        var encoded =
            Convert.ToBase64String(
                Encoding.ASCII.GetBytes(credentials));

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                encoded);
    }

    private async Task FulfillPaymentAsync(
        PaymentTransaction transaction,
        Guid userId)
    {
        switch (transaction.Purpose)
        {
            case PaymentPurpose.PackagePurchase:
                await FulfillPackagePurchaseAsync(
                    transaction,
                    userId);
                break;

            case PaymentPurpose.WorkshopBooking:
                await FulfillWorkshopBookingAsync(
                    transaction,
                    userId);
                break;

            case PaymentPurpose.TrainerApplication:
                await FulfillTrainerApplicationAsync(
                    transaction,
                    userId);
                break;

            case PaymentPurpose.TrainerTierUpgrade:
                await FulfillTrainerTierUpgradeAsync(
                    transaction,
                    userId);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported payment purpose: {transaction.Purpose}");
        }
    }

    private async Task FulfillPackagePurchaseAsync(
        PaymentTransaction transaction,
        Guid userId)
    {
        var alreadyFulfilled =
            await _dbContext.StudentPackages
                .AnyAsync(
                    sp => sp.PaymentTransactionId ==
                          transaction.Id);

        if (alreadyFulfilled)
        {
            return;
        }

        var studentProfile =
            await _dbContext.StudentProfiles
                .FirstOrDefaultAsync(
                    sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            throw new InvalidOperationException(
                "Student profile was not found.");
        }

        var package =
            await _dbContext.Packages
                .FirstOrDefaultAsync(
                    p => p.Id == transaction.ReferenceId &&
                         p.IsActive);

        if (package == null)
        {
            throw new InvalidOperationException(
                "The purchased package no longer exists or is inactive.");
        }

        var now = DateTime.UtcNow;

        var activePackage = await _dbContext.StudentPackages
            .Where(sp =>
                sp.StudentProfileId == studentProfile.Id &&
                sp.Status == StudentPackageStatus.Active &&
                sp.ExpiryDate > now)
            .OrderByDescending(sp => sp.ExpiryDate)
            .FirstOrDefaultAsync();

        var startDate = activePackage != null
            ? activePackage.ExpiryDate
            : now;

        var expiryDate = startDate.AddDays(package.DurationDays);

        var studentPackage = new StudentPackage
        {
            Id = Guid.NewGuid(),
            StudentProfileId = studentProfile.Id,
            PackageId = package.Id,
            PaymentTransactionId = transaction.Id,
            StartDate = startDate,
            ExpiryDate = expiryDate,
            Status = activePackage != null
                ? StudentPackageStatus.Pending
                : StudentPackageStatus.Active,
            ClassesAllowed = package.ClassLimit,
            ClassesUsed = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.StudentPackages.Add(studentPackage);
    }

    private async Task FulfillWorkshopBookingAsync(
        PaymentTransaction transaction,
        Guid userId)
    {
        var studentProfile =
            await _dbContext.StudentProfiles
                .FirstOrDefaultAsync(
                    sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            throw new InvalidOperationException(
                "Student profile was not found.");
        }

        var booking =
            await _dbContext.WorkshopBookings
                .FirstOrDefaultAsync(
                    b => b.WorkshopId == transaction.ReferenceId &&
                         b.StudentProfileId == studentProfile.Id);

        if (booking == null)
        {
            booking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = transaction.ReferenceId,
                StudentProfileId = studentProfile.Id,
                PaymentTransactionId = transaction.Id,
                Status = WorkshopBookingStatus.Confirmed,
                BookedAt = DateTime.UtcNow
            };
            _dbContext.WorkshopBookings.Add(booking);
            return;
        }

        if (booking.Status == WorkshopBookingStatus.Confirmed &&
            booking.PaymentTransactionId == transaction.Id)
        {
            return;
        }

        booking.Status = WorkshopBookingStatus.Confirmed;
        booking.PaymentTransactionId = transaction.Id;
        booking.CancelledAt = null;
    }

    private async Task FulfillTrainerApplicationAsync(
        PaymentTransaction transaction,
        Guid userId)
    {
        var application = await _dbContext.TrainerApplications
            .FirstOrDefaultAsync(a => a.Id == transaction.ReferenceId && a.TrainerProfile.UserId == userId);

        if (application == null)
        {
            throw new InvalidOperationException("Trainer application not found for payment fulfillment.");
        }

        application.PaymentTransactionId = transaction.Id;
        application.Status = TrainerApplicationStatus.PaymentVerified;
        application.PaymentVerifiedAt = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;
    }

    private async Task FulfillTrainerTierUpgradeAsync(
        PaymentTransaction transaction,
        Guid userId)
    {
        var upgradeReq = await _dbContext.TrainerUpgradeRequests
            .Include(u => u.TrainerProfile)
            .FirstOrDefaultAsync(u => u.Id == transaction.ReferenceId && u.TrainerProfile.UserId == userId);

        if (upgradeReq == null)
        {
            throw new InvalidOperationException("Trainer upgrade request not found for payment fulfillment.");
        }

        upgradeReq.Status = TrainerUpgradeRequestStatus.PaymentVerified;
    }

    private static long ConvertToPaise(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount must be greater than zero.");
        }

        return checked(
            (long)Math.Round(
                amount * 100m,
                MidpointRounding.AwayFromZero));
    }

    private static bool VerifyRazorpaySignature(
        string orderId,
        string paymentId,
        string signature,
        string secret)
    {
        try
        {
            var payload = $"{orderId}|{paymentId}";

            using var hmac =
                new HMACSHA256(
                    Encoding.UTF8.GetBytes(secret));

            var expected =
                hmac.ComputeHash(
                    Encoding.UTF8.GetBytes(payload));

            var provided =
                Convert.FromHexString(signature);

            return CryptographicOperations.FixedTimeEquals(
                expected,
                provided);
        }
        catch
        {
            return false;
        }
    }

    private static PaymentTransactionResponse
        MapToResponse(PaymentTransaction transaction)
    {
        return new PaymentTransactionResponse
        {
            Id = transaction.Id,
            Purpose = transaction.Purpose,
            ReferenceId = transaction.ReferenceId,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status,
            RazorpayOrderId = transaction.RazorpayOrderId,
            RazorpayPaymentId = transaction.RazorpayPaymentId,
            CreatedAt = transaction.CreatedAt,
            PaidAt = transaction.PaidAt
        };
    }

    private sealed class RazorpayPaymentResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    private static string NormalizeIndianPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Phone number is required.");
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("91") && digits.Length == 12)
        {
            digits = digits[2..];
        }

        if (digits.Length != 10)
        {
            throw new ArgumentException(
                "Phone number must contain exactly 10 digits.");
        }

        return digits;
    }

    public async Task<PaymentTransactionResponse> CreatePublicPackageOrderAsync(
        CreatePublicPackageOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var phone = NormalizeIndianPhone(request.Phone);

        var package = await _dbContext.Packages
            .FirstOrDefaultAsync(
                p => p.Id == request.PackageId &&
                     p.IsActive,
                cancellationToken);

        if (package == null)
        {
            throw new ArgumentException(
                "Specified package was not found or is inactive.");
        }

        if (package.Price <= 0)
        {
            throw new InvalidOperationException(
                "The selected package does not have a valid price.");
        }

        if (package.DurationDays <= 0)
        {
            throw new InvalidOperationException(
                "The selected package does not have a valid duration.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                u => u.Phone == phone,
                cancellationToken);

        if (user == null)
        {
            var now = DateTime.UtcNow;

            var studentRole = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.Code == "STUDENT", cancellationToken);

            user = new User
            {
                Id = Guid.NewGuid(),
                CustomerCode = $"ETH{Random.Shared.Next(100000, 999999)}",
                FullName = string.IsNullOrWhiteSpace(request.FullName)
                    ? "Ethos Student"
                    : request.FullName.Trim(),
                Phone = phone,
                Email = string.IsNullOrWhiteSpace(request.Email)
                    ? null
                    : request.Email.Trim(),
                IsActive = false, // Activated upon payment completion in VerifyPublicPackagePaymentAsync
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Users.Add(user);

            if (studentRole != null)
            {
                var userRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = studentRole.Id,
                    AssignedAt = now
                };
                _dbContext.UserRoles.Add(userRole);
                user.UserRoles.Add(userRole);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.FullName))
            {
                user.FullName = request.FullName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                user.Email = request.Email.Trim();
            }

            user.UpdatedAt = DateTime.UtcNow;
        }

        var transactionId = Guid.NewGuid();

        var razorpayOrderId = await CreateRazorpayOrderAsync(
            transactionId,
            package.Price,
            PaymentPurpose.PackagePurchase);

        var createdAt = DateTime.UtcNow;

        var transaction = new PaymentTransaction
        {
            Id = transactionId,
            UserId = user.Id,
            Purpose = PaymentPurpose.PackagePurchase,
            ReferenceId = package.Id,
            Amount = package.Price,
            Currency = "INR",
            Status = PaymentStatus.OrderCreated,
            RazorpayOrderId = razorpayOrderId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        _dbContext.PaymentTransactions.Add(transaction);

        _dbContext.PaymentEvents.Add(new PaymentEvent
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = transaction.Id,
            EventType = "PublicPackageOrderCreated",
            Payload =
                $"Public package order created. " +
                $"PackageId: {package.Id}; " +
                $"Phone: {phone}; " +
                $"Amount: {package.Price} INR; " +
                $"RazorpayOrderId: {razorpayOrderId}.",
            CreatedAt = createdAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(transaction);
    }

    public async Task<PaymentTransactionResponse> VerifyPublicPackagePaymentAsync(
        VerifyPublicPackagePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(
                t => t.Id == request.TransactionId &&
                     t.Purpose == PaymentPurpose.PackagePurchase,
                cancellationToken);

        if (transaction == null)
        {
            throw new ArgumentException(
                "Payment transaction was not found.");
        }

        if (string.IsNullOrWhiteSpace(transaction.RazorpayOrderId))
        {
            throw new InvalidOperationException(
                "Payment transaction does not contain a Razorpay order ID.");
        }

        if (!string.Equals(
                transaction.RazorpayOrderId,
                request.RazorpayOrderId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Razorpay Order ID does not match the payment transaction.");
        }

        if (transaction.Status == PaymentStatus.Paid)
        {
            if (!string.Equals(
                    transaction.RazorpayPaymentId,
                    request.RazorpayPaymentId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "This transaction has already been completed with a different payment.");
            }

            return MapToResponse(transaction);
        }

        if (string.IsNullOrWhiteSpace(request.RazorpayPaymentId) ||
            string.IsNullOrWhiteSpace(request.RazorpaySignature))
        {
            throw new ArgumentException(
                "Razorpay payment ID and signature are required.");
        }

        // Public endpoint: ALWAYS verify real cryptographic signature
        var signatureValid = VerifyRazorpaySignature(
            request.RazorpayOrderId,
            request.RazorpayPaymentId,
            request.RazorpaySignature,
            _razorpaySettings.KeySecret);

        if (!signatureValid)
        {
            transaction.Status = PaymentStatus.Failed;
            transaction.UpdatedAt = DateTime.UtcNow;

            _dbContext.PaymentEvents.Add(new PaymentEvent
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = transaction.Id,
                EventType = "PublicPaymentSignatureVerificationFailed",
                Payload =
                    $"Invalid Razorpay signature. " +
                    $"OrderId: {request.RazorpayOrderId}.",
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            throw new InvalidOperationException(
                "Razorpay payment signature verification failed.");
        }

        RazorpayPaymentResponse razorpayPayment;
        if (_environment.IsDevelopment() &&
            (request.RazorpayPaymentId.Contains("mock") ||
             request.RazorpayPaymentId.StartsWith("pay_e2e_")))
        {
            // Development-only: simulate captured payment without calling Razorpay API
            razorpayPayment = new RazorpayPaymentResponse
            {
                Id = request.RazorpayPaymentId,
                OrderId = transaction.RazorpayOrderId,
                Amount = ConvertToPaise(transaction.Amount),
                Currency = transaction.Currency,
                Status = "captured"
            };
        }
        else
        {
            razorpayPayment =
                await GetRazorpayPaymentAsync(request.RazorpayPaymentId);
        }


        if (!string.Equals(
                razorpayPayment.OrderId,
                transaction.RazorpayOrderId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Razorpay payment does not belong to this order.");
        }

        var expectedAmount = ConvertToPaise(transaction.Amount);

        if (razorpayPayment.Amount != expectedAmount)
        {
            throw new InvalidOperationException(
                "Razorpay payment amount does not match the transaction amount.");
        }

        if (!string.Equals(
                razorpayPayment.Currency,
                transaction.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Razorpay payment currency does not match the transaction currency.");
        }

        if (!string.Equals(
                razorpayPayment.Status,
                "captured",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Razorpay payment is not captured. Current status: {razorpayPayment.Status}");
        }

        await using var dbTransaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var user = await _dbContext.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.StudentProfile)
                .FirstOrDefaultAsync(
                    u => u.Id == transaction.UserId,
                    cancellationToken);

            if (user == null)
            {
                throw new InvalidOperationException(
                    "Purchaser account could not be found.");
            }

            var package = await _dbContext.Packages
                .FirstOrDefaultAsync(
                    p => p.Id == transaction.ReferenceId &&
                         p.IsActive,
                    cancellationToken);

            if (package == null)
            {
                throw new InvalidOperationException(
                    "The purchased package no longer exists or is inactive.");
            }

            var studentRole = await _dbContext.Roles
                .FirstOrDefaultAsync(
                    r => r.Code == "STUDENT",
                    cancellationToken);

            if (studentRole == null)
            {
                throw new InvalidOperationException(
                    "STUDENT role is not configured.");
            }

            var hasStudentRole = user.UserRoles
                .Any(ur => ur.RoleId == studentRole.Id);

            if (!hasStudentRole)
            {
                _dbContext.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = studentRole.Id,
                    AssignedAt = DateTime.UtcNow
                });
            }

            var studentProfile = user.StudentProfile;

            if (studentProfile == null)
            {
                studentProfile = new StudentProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.StudentProfiles.Add(studentProfile);
            }

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;

            transaction.Status = PaymentStatus.Paid;
            transaction.RazorpayPaymentId = request.RazorpayPaymentId;
            transaction.RazorpaySignature = request.RazorpaySignature;
            transaction.PaidAt = DateTime.UtcNow;
            transaction.UpdatedAt = DateTime.UtcNow;

            await CreateStudentPackageForPurchaseAsync(
                transaction,
                studentProfile,
                package,
                cancellationToken);

            _dbContext.PaymentEvents.Add(new PaymentEvent
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = transaction.Id,
                EventType = "PublicPackagePaymentVerified",
                Payload =
                    $"Payment verified and package activated. " +
                    $"RazorpayPaymentId: {request.RazorpayPaymentId}.",
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            await dbTransaction.CommitAsync(cancellationToken);

            return MapToResponse(transaction);
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task CreateStudentPackageForPurchaseAsync(
        PaymentTransaction transaction,
        StudentProfile studentProfile,
        Package package,
        CancellationToken cancellationToken)
    {
        var alreadyFulfilled = await _dbContext.StudentPackages
            .AnyAsync(
                sp => sp.PaymentTransactionId == transaction.Id,
                cancellationToken);

        if (alreadyFulfilled)
        {
            return;
        }

        var now = DateTime.UtcNow;

        // If an active package exists, queue the new package to start when current expires
        var activePackage = await _dbContext.StudentPackages
            .Where(sp =>
                sp.StudentProfileId == studentProfile.Id &&
                sp.Status == StudentPackageStatus.Active &&
                sp.ExpiryDate > now)
            .OrderByDescending(sp => sp.ExpiryDate)
            .FirstOrDefaultAsync(cancellationToken);

        var startDate = activePackage != null
            ? activePackage.ExpiryDate
            : now;

        var expiryDate = startDate.AddDays(package.DurationDays);

        var studentPackage = new StudentPackage
        {
            Id = Guid.NewGuid(),
            StudentProfileId = studentProfile.Id,
            PackageId = package.Id,
            PaymentTransactionId = transaction.Id,
            StartDate = startDate,
            ExpiryDate = expiryDate,
            Status = activePackage != null
                ? StudentPackageStatus.Pending
                : StudentPackageStatus.Active,
            ClassesAllowed = package.ClassLimit,
            ClassesUsed = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.StudentPackages.Add(studentPackage);
    }
}
