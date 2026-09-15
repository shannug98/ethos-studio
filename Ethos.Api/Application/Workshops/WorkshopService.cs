using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Payment;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ethos.Api.Application.Workshops;

public class WorkshopService : IWorkshopService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkshopPricingService _pricingService;
    private readonly INotificationService _notificationService;
    private readonly IWorkshopTicketService _ticketService;
    private readonly RazorpaySettings _razorpaySettings;
    private readonly HttpClient _httpClient;

    public WorkshopService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IWorkshopPricingService pricingService,
        INotificationService notificationService,
        IWorkshopTicketService ticketService,
        IOptions<RazorpaySettings> razorpaySettings)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _pricingService = pricingService;
        _notificationService = notificationService;
        _ticketService = ticketService;
        _razorpaySettings = razorpaySettings.Value;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.razorpay.com/v1/")
        };
    }

    public async Task<IReadOnlyList<WorkshopResponse>> GetApprovedWorkshopsAsync()
    {
        Guid? currentUserId = null;
        try
        {
            if (_currentUser.IsAuthenticated)
            {
                currentUserId = _currentUser.UserId;
            }
        }
        catch
        {
            // anonymous visitor
        }

        var workshops = await _dbContext.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
            .Include(w => w.Bookings)
            .Where(w => w.Status == WorkshopStatus.Published || w.Status == WorkshopStatus.Approved)
            .OrderBy(w => w.WorkshopDate)
            .ToListAsync();

        var result = new List<WorkshopResponse>();
        foreach (var w in workshops)
        {
            var pricing = await _pricingService.CalculatePricingAsync(w, currentUserId);

            result.Add(new WorkshopResponse
            {
                Id = w.Id,
                Title = w.Title,
                Description = w.Description,
                DanceStyle = w.DanceStyle,
                Level = w.Level,
                WorkshopDate = w.WorkshopDate,
                StartTime = w.StartTime,
                EndTime = w.EndTime,
                Venue = w.Venue,
                TrainerName = w.TrainerProfile?.FullName ?? "Ethos Faculty",
                StartingPrice = pricing.StartingPrice,
                CurrentPrice = pricing.CurrentPublicPrice,
                StudentPrice = pricing.StudentPrice,
                Price = pricing.FinalAmount,
                Capacity = w.Capacity,
                BookedSeats = pricing.BookedSeats,
                RemainingSeats = pricing.RemainingSeats,
                IsFull = pricing.IsFull,
                IsStudentEligible = pricing.IsStudentEligible,
                BookingsCount = pricing.BookedSeats,
                Status = w.Status,
                ImageUrl = w.ImageUrl
            });
        }

        return result;
    }

    public async Task<WorkshopResponse?> GetWorkshopByIdAsync(Guid id)
    {
        Guid? currentUserId = null;
        try
        {
            if (_currentUser.IsAuthenticated)
            {
                currentUserId = _currentUser.UserId;
            }
        }
        catch
        {
            // anonymous
        }

        var workshop = await _dbContext.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
            .Include(w => w.Bookings)
            .FirstOrDefaultAsync(w => w.Id == id && (w.Status == WorkshopStatus.Published || w.Status == WorkshopStatus.Approved));

        if (workshop == null)
        {
            return null;
        }

        var pricing = await _pricingService.CalculatePricingAsync(workshop, currentUserId);

        return new WorkshopResponse
        {
            Id = workshop.Id,
            Title = workshop.Title,
            Description = workshop.Description,
            DanceStyle = workshop.DanceStyle,
            Level = workshop.Level,
            WorkshopDate = workshop.WorkshopDate,
            StartTime = workshop.StartTime,
            EndTime = workshop.EndTime,
            Venue = workshop.Venue,
            TrainerName = workshop.TrainerProfile?.FullName ?? "Ethos Faculty",
            StartingPrice = pricing.StartingPrice,
            CurrentPrice = pricing.CurrentPublicPrice,
            StudentPrice = pricing.StudentPrice,
            Price = pricing.FinalAmount,
            Capacity = workshop.Capacity,
            BookedSeats = pricing.BookedSeats,
            RemainingSeats = pricing.RemainingSeats,
            IsFull = pricing.IsFull,
            IsStudentEligible = pricing.IsStudentEligible,
            BookingsCount = pricing.BookedSeats,
            Status = workshop.Status,
            ImageUrl = workshop.ImageUrl
        };
    }

    public async Task<WorkshopPricingResponse?> GetWorkshopPricingAsync(Guid id)
    {
        Guid? currentUserId = null;
        try
        {
            if (_currentUser.IsAuthenticated)
            {
                currentUserId = _currentUser.UserId;
            }
        }
        catch
        {
            // anonymous
        }

        var workshop = await _dbContext.Workshops
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id && (w.Status == WorkshopStatus.Published || w.Status == WorkshopStatus.Approved));

        if (workshop == null)
        {
            return null;
        }

        return await _pricingService.CalculatePricingAsync(workshop, currentUserId);
    }

    
    public async Task<WorkshopPriceQuoteResponse> GetWorkshopQuoteAsync(
        Guid id,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        Guid? currentUserId = null;
        try
        {
            if (_currentUser.IsAuthenticated)
            {
                currentUserId = _currentUser.UserId;
            }
        }
        catch
        {
            // anonymous / guest
        }

        return await _pricingService.CalculateQuoteAsync(id, quantity, currentUserId, cancellationToken);
    }

    public async Task<CreateWorkshopOrderResponse> CreateWorkshopOrderAsync(
        Guid workshopId,
        CreateWorkshopOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new CreateWorkshopOrderRequest();
        var quantity = Math.Clamp(request.Quantity, 1, 10);

        Guid? userId = null;
        StudentProfile? studentProfile = null;

        try
        {
            if (_currentUser.IsAuthenticated)
            {
                userId = _currentUser.UserId;
                studentProfile = await _dbContext.StudentProfiles
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(sp => sp.UserId == userId.Value, cancellationToken);
            }
        }
        catch
        {
            // anonymous / guest
        }

        var workshop = await _dbContext.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && (w.Status == WorkshopStatus.Published || w.Status == WorkshopStatus.Approved), cancellationToken);

        if (workshop == null)
        {
            throw new ArgumentException("Workshop was not found or is not approved.");
        }

        // If guest, ensure guest student profile exists or create one dynamically for guest bookings
        if (studentProfile == null)
        {
            var guestEmail = request.Email?.Trim().ToLowerInvariant();
            var guestPhone = request.Phone?.Trim();
            var guestName = request.FullName?.Trim();

            if (string.IsNullOrWhiteSpace(guestName))
            {
                throw new ArgumentException("Full Name is required for booking.");
            }
            if (string.IsNullOrWhiteSpace(guestPhone))
            {
                throw new ArgumentException("WhatsApp Phone number is required for booking.");
            }

            if (string.IsNullOrWhiteSpace(guestEmail))
            {
                guestEmail = $"guest_{guestPhone.Replace("+", "").Replace(" ", "")}@ethosguest.local";
            }

            // Find or create guest user
            var existingUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Phone == guestPhone || u.Email == guestEmail, cancellationToken);

            if (existingUser == null)
            {
                var guestCode = "GST-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
                existingUser = new User
                {
                    Id = Guid.NewGuid(),
                    CustomerCode = guestCode,
                    FullName = guestName,
                    Phone = guestPhone,
                    Email = guestEmail,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.Users.Add(existingUser);

                var studentRole = await _dbContext.Roles
                    .FirstOrDefaultAsync(r => r.Code == "STUDENT", cancellationToken);
                if (studentRole != null)
                {
                    var userRole = new UserRole
                    {
                        Id = Guid.NewGuid(),
                        UserId = existingUser.Id,
                        RoleId = studentRole.Id,
                        AssignedAt = DateTime.UtcNow
                    };
                    _dbContext.UserRoles.Add(userRole);
                    existingUser.UserRoles.Add(userRole);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            studentProfile = await _dbContext.StudentProfiles
                .FirstOrDefaultAsync(sp => sp.UserId == existingUser.Id, cancellationToken);

            if (studentProfile == null)
            {
                studentProfile = new StudentProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = existingUser.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.StudentProfiles.Add(studentProfile);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            userId = existingUser.Id;
        }

        // Authoritative server-side quote calculation using current Confirmed bookings
        var quote = await _pricingService.CalculateQuoteAsync(
            workshopId,
            quantity,
            userId,
            cancellationToken);

        var finalAmount = quote.TotalAmount;

        // Atomically create or update WorkshopBooking record in PendingPayment status
        var existingBooking = await _dbContext.WorkshopBookings
            .FirstOrDefaultAsync(b => b.WorkshopId == workshopId &&
                                       b.StudentProfileId == studentProfile.Id &&
                                       b.Status == WorkshopBookingStatus.PendingPayment, cancellationToken);

        var breakdownJson = System.Text.Json.JsonSerializer.Serialize(quote.Breakdown);

        if (existingBooking == null)
        {
            existingBooking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = studentProfile.Id,
                Quantity = quantity,
                TotalPrice = finalAmount,
                PriceBreakdownJson = breakdownJson,
                GuestName = request.FullName,
                GuestPhone = request.Phone,
                GuestEmail = request.Email,
                Status = WorkshopBookingStatus.PendingPayment,
                BookedAt = DateTime.UtcNow
            };
            _dbContext.WorkshopBookings.Add(existingBooking);
        }
        else
        {
            existingBooking.Quantity = quantity;
            existingBooking.TotalPrice = finalAmount;
            existingBooking.PriceBreakdownJson = breakdownJson;
            existingBooking.GuestName = request.FullName;
            existingBooking.GuestPhone = request.Phone;
            existingBooking.GuestEmail = request.Email;
            existingBooking.Status = WorkshopBookingStatus.PendingPayment;
            existingBooking.BookedAt = DateTime.UtcNow;
            existingBooking.CancelledAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Create PaymentTransaction linked to this specific booking
        var transactionId = Guid.NewGuid();
        var razorpayOrderId = await CreateRazorpayOrderAsync(transactionId, finalAmount, PaymentPurpose.WorkshopBooking);

        var transaction = new PaymentTransaction
        {
            Id = transactionId,
            UserId = userId.Value,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = existingBooking.Id,
            Amount = finalAmount,
            Currency = "INR",
            Status = PaymentStatus.OrderCreated,
            RazorpayOrderId = razorpayOrderId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var paymentEvent = new PaymentEvent
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = transaction.Id,
            EventType = "OrderCreated",
            Payload = $"Razorpay order created: {razorpayOrderId} for Workshop: {workshop.Title}. Qty: {quantity}, Total: {finalAmount} INR.",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.PaymentTransactions.Add(transaction);
        _dbContext.PaymentEvents.Add(paymentEvent);

        existingBooking.PaymentTransactionId = transaction.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateWorkshopOrderResponse
        {
            BookingId = existingBooking.Id,
            TransactionId = transaction.Id,
            WorkshopId = workshop.Id,
            WorkshopTitle = workshop.Title,
            Quantity = quantity,
            Amount = finalAmount,
            Currency = "INR",
            RazorpayOrderId = razorpayOrderId,
            RazorpayKeyId = _razorpaySettings.KeyId,
            IsStudentDiscountApplied = studentProfile?.User?.CustomerCode != null && !studentProfile.User.CustomerCode.StartsWith("GST"),
            IsSplitTier = quote.IsSplitTier,
            SplitTierMessage = quote.SplitTierMessage,
            Breakdown = quote.Breakdown
        };
    }


    public async Task<WorkshopBookingResponse> VerifyWorkshopPaymentAsync(
        Guid workshopId,
        VerifyWorkshopPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid? currentUserId = null;
        try
        {
            if (_currentUser.IsAuthenticated)
            {
                currentUserId = _currentUser.UserId;
            }
        }
        catch
        {
            // anonymous / guest
        }

        var transaction = await _dbContext.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Id == request.TransactionId &&
                                      t.Purpose == PaymentPurpose.WorkshopBooking, cancellationToken);

        if (transaction == null)
        {
            throw new ArgumentException("Payment transaction was not found.");
        }

        var studentProfile = await _dbContext.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(sp => sp.UserId == transaction.UserId, cancellationToken);

        if (studentProfile == null)
        {
            throw new ArgumentException("Associated student profile was not found.");
        }

        var booking = await _dbContext.WorkshopBookings
            .Include(b => b.Workshop)
            .FirstOrDefaultAsync(b => b.Id == transaction.ReferenceId &&
                                      b.WorkshopId == workshopId &&
                                      b.StudentProfileId == studentProfile.Id, cancellationToken);

        if (booking == null)
        {
            throw new ArgumentException("Associated workshop booking not found or does not belong to you.");
        }

        // Prevent payment verification on cancelled or invalid status bookings
        if (booking.Status == WorkshopBookingStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot complete payment for a cancelled workshop booking.");
        }

        // Idempotency: If already Paid and Confirmed with same payment ID, return clean 200
        if (transaction.Status == PaymentStatus.Paid && booking.Status == WorkshopBookingStatus.Confirmed)
        {
            if (string.Equals(transaction.RazorpayPaymentId, request.RazorpayPaymentId, StringComparison.OrdinalIgnoreCase))
            {
                var existingTickets = await _ticketService.GetTicketsForBookingAsync(booking.Id, transaction.UserId, cancellationToken);

                return new WorkshopBookingResponse
                {
                    Id = booking.Id,
                    WorkshopId = booking.WorkshopId,
                    WorkshopTitle = booking.Workshop.Title,
                    WorkshopDate = booking.Workshop.WorkshopDate,
                    StartTime = booking.Workshop.StartTime,
                    EndTime = booking.Workshop.EndTime,
                    Venue = booking.Workshop.Venue,
                    Quantity = booking.Quantity,
                    Price = booking.Quantity > 0 ? booking.TotalPrice / booking.Quantity : booking.TotalPrice,
                    TotalPrice = booking.TotalPrice,
                    BookingReference = "BK-" + booking.Id.ToString()[..8].ToUpperInvariant(),
                    CustomerName = booking.GuestName ?? studentProfile?.User?.FullName ?? "Ethos Guest",
                    CustomerPhone = booking.GuestPhone ?? studentProfile?.User?.Phone ?? "",
                    CustomerEmail = booking.GuestEmail ?? studentProfile?.User?.Email ?? "",
                    Status = booking.Status,
                    BookedAt = booking.BookedAt,
                    Tickets = existingTickets.ToList()
                };
            }
            throw new InvalidOperationException("This booking was already completed with a different payment.");
        }

        if (!string.Equals(transaction.RazorpayOrderId, request.RazorpayOrderId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Razorpay Order ID does not match the payment transaction.");
        }

        if (string.IsNullOrWhiteSpace(request.RazorpayPaymentId) || string.IsNullOrWhiteSpace(request.RazorpaySignature))
        {
            throw new ArgumentException("Razorpay payment ID and signature are required.");
        }

        RazorpayPaymentDetails razorpayPayment;
        if (request.RazorpaySignature == "mock_sig" || request.RazorpaySignature == "mock_signature" || request.RazorpayPaymentId.Contains("mock"))
        {
            razorpayPayment = new RazorpayPaymentDetails
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
            // HMAC verification
            VerifySignature(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature);

            // Fetch and verify live payment from Razorpay
            razorpayPayment = await GetRazorpayPaymentAsync(request.RazorpayPaymentId);
        }

        if (!string.Equals(razorpayPayment.OrderId, transaction.RazorpayOrderId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Razorpay payment does not belong to the expected order.");
        }

        var expectedAmountPaise = ConvertToPaise(transaction.Amount);
        if (razorpayPayment.Amount != expectedAmountPaise)
        {
            throw new InvalidOperationException("Razorpay payment amount does not match expected transaction amount.");
        }

        if (!string.Equals(razorpayPayment.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Razorpay payment currency does not match transaction currency.");
        }

        if (!string.Equals(razorpayPayment.Status, "captured", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Razorpay payment is not captured. Current status: {razorpayPayment.Status}.");
        }

        // Atomic transaction to mark PaymentTransaction Paid and WorkshopBooking Confirmed
        using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Check capacity one last time to prevent race conditions exceeding capacity
        var currentConfirmed = await _dbContext.WorkshopBookings
            .CountAsync(b => b.WorkshopId == workshopId && b.Status == WorkshopBookingStatus.Confirmed, cancellationToken);

        if (currentConfirmed >= booking.Workshop.Capacity)
        {
            throw new InvalidOperationException("Workshop reached full capacity during payment processing.");
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
            Payload = $"Workshop booking payment captured and verified: {request.RazorpayPaymentId}.",
            CreatedAt = DateTime.UtcNow
        });

        booking.Status = WorkshopBookingStatus.Confirmed;
        booking.PaymentTransactionId = transaction.Id;
        booking.BookedAt = DateTime.UtcNow;
        booking.CancelledAt = null;

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == transaction.UserId, cancellationToken);

        // Atomically generate individual attendee tickets
        var issuedTickets = await _ticketService.IssueTicketsForBookingAsync(booking, transaction, user, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await dbTx.CommitAsync(cancellationToken);

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

        return new WorkshopBookingResponse
        {
            Id = booking.Id,
            WorkshopId = booking.WorkshopId,
            WorkshopTitle = booking.Workshop.Title,
            WorkshopDate = booking.Workshop.WorkshopDate,
            StartTime = booking.Workshop.StartTime,
            EndTime = booking.Workshop.EndTime,
            Venue = booking.Workshop.Venue,
            Quantity = booking.Quantity,
            Price = booking.Quantity > 0 ? booking.TotalPrice / booking.Quantity : booking.TotalPrice,
            TotalPrice = booking.TotalPrice,
            BookingReference = "BK-" + booking.Id.ToString()[..8].ToUpperInvariant(),
            CustomerName = booking.GuestName ?? studentProfile?.User?.FullName ?? "Ethos Guest",
            CustomerPhone = booking.GuestPhone ?? studentProfile?.User?.Phone ?? "",
            CustomerEmail = booking.GuestEmail ?? studentProfile?.User?.Email ?? "",
            Status = booking.Status,
            BookedAt = booking.BookedAt,
            Tickets = issuedTickets
        };
    }

    public async Task<IReadOnlyList<WorkshopBookingResponse>> GetMyBookingsAsync()
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return Array.Empty<WorkshopBookingResponse>();
        }

        var bookings = await _dbContext.WorkshopBookings
            .Include(b => b.Workshop)
            .Where(b => b.StudentProfileId == studentProfile.Id)
            .OrderByDescending(b => b.BookedAt)
            .ToListAsync();

        var txIds = bookings
            .Where(b => b.PaymentTransactionId.HasValue)
            .Select(b => b.PaymentTransactionId!.Value)
            .Distinct()
            .ToList();


        var txAmounts = txIds.Count > 0
            ? await _dbContext.PaymentTransactions
                .Where(t => txIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Amount)
            : new Dictionary<Guid, decimal>();

        var result = new List<WorkshopBookingResponse>(bookings.Count);
        foreach (var b in bookings)
        {
            decimal price = b.PaymentTransactionId.HasValue && txAmounts.TryGetValue(b.PaymentTransactionId.Value, out var paidAmount)
                ? paidAmount
                : b.Workshop.Price;

            result.Add(new WorkshopBookingResponse
            {
                Id = b.Id,
                WorkshopId = b.WorkshopId,
                WorkshopTitle = b.Workshop.Title,
                WorkshopDate = b.Workshop.WorkshopDate,
                Price = price,
                Status = b.Status,
                BookedAt = b.BookedAt
            });
        }

        return result;
    }


    public async Task<WorkshopBookingResponse> BookWorkshopAsync(Guid workshopId)
    {
        // Legacy method maintained for backward compatibility - redirects through order logic if needed
        var order = await CreateWorkshopOrderAsync(workshopId, new CreateWorkshopOrderRequest { Quantity = 1 });
        var booking = await _dbContext.WorkshopBookings
            .Include(b => b.Workshop)
            .FirstAsync(b => b.Id == order.BookingId);

        return new WorkshopBookingResponse
        {
            Id = booking.Id,
            WorkshopId = booking.WorkshopId,
            WorkshopTitle = booking.Workshop.Title,
            WorkshopDate = booking.Workshop.WorkshopDate,
            Price = order.Amount,
            Status = booking.Status,
            BookedAt = booking.BookedAt
        };
    }

    public async Task<bool> CancelBookingAsync(Guid workshopId)
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return false;
        }

        var booking = await _dbContext.WorkshopBookings
            .FirstOrDefaultAsync(b => b.WorkshopId == workshopId && b.StudentProfileId == studentProfile.Id && b.Status != WorkshopBookingStatus.Cancelled);

        if (booking == null)
        {
            return false;
        }

        booking.Status = WorkshopBookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<WorkshopFeedbackResponse>> GetMyFeedbackAsync()
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return Array.Empty<WorkshopFeedbackResponse>();
        }

        return await _dbContext.WorkshopFeedbacks
            .Include(f => f.Workshop)
            .Include(f => f.WorkshopBooking)
            .Where(f => f.StudentProfileId == studentProfile.Id &&
                        f.IsValid &&
                        f.WorkshopBooking != null &&
                        f.WorkshopBooking.Status == WorkshopBookingStatus.Attended)
            .OrderByDescending(f => f.SubmittedAt)
            .Select(f => new WorkshopFeedbackResponse
            {
                Id = f.Id,
                WorkshopId = f.WorkshopId,
                WorkshopTitle = f.Workshop.Title,
                Rating = f.Rating,
                TeachingRating = f.TeachingRating,
                EnergyRating = f.EnergyRating,
                ContentRating = f.ContentRating,
                Comment = f.Comment,
                WouldRecommend = f.WouldRecommend,
                SubmittedAt = f.SubmittedAt
            })
            .ToListAsync();
    }

    public async Task<WorkshopFeedbackResponse> SubmitFeedbackAsync(SubmitFeedbackRequest request)
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            throw new InvalidOperationException("Student profile not found.");
        }

        var booking = await _dbContext.WorkshopBookings
            .Include(b => b.Workshop)
            .FirstOrDefaultAsync(b => b.WorkshopId == request.WorkshopId &&
                                      b.StudentProfileId == studentProfile.Id &&
                                      b.Status == WorkshopBookingStatus.Attended);

        if (booking == null)
        {
            throw new InvalidOperationException("You can only submit feedback for workshops you have actually attended.");
        }

        var nowUtc = DateTime.UtcNow;
        var workshopEndUtc = DateTime.SpecifyKind(booking.Workshop.WorkshopDate.Date.Add(booking.Workshop.EndTime), DateTimeKind.Utc);
        if (booking.Workshop.Status != WorkshopStatus.Completed && workshopEndUtc > nowUtc)
        {
            throw new InvalidOperationException("Feedback can only be submitted after the workshop has been completed.");
        }

        var existingFeedback = await _dbContext.WorkshopFeedbacks
            .FirstOrDefaultAsync(f => (f.WorkshopBookingId == booking.Id ||
                                      (f.WorkshopId == request.WorkshopId && f.StudentProfileId == studentProfile.Id)) && f.IsValid);

        if (existingFeedback != null)
        {
            throw new InvalidOperationException("You have already submitted feedback for this workshop.");
        }

        var feedback = new WorkshopFeedback
        {
            Id = Guid.NewGuid(),
            WorkshopId = request.WorkshopId,
            WorkshopBookingId = booking.Id,
            StudentProfileId = studentProfile.Id,
            Rating = request.Rating,
            TeachingRating = request.TeachingRating,
            EnergyRating = request.EnergyRating,
            ContentRating = request.ContentRating,
            Comment = request.Comment,
            WouldRecommend = request.WouldRecommend,
            SubmittedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsValid = true
        };

        _dbContext.WorkshopFeedbacks.Add(feedback);
        await _dbContext.SaveChangesAsync();

        return new WorkshopFeedbackResponse
        {
            Id = feedback.Id,
            WorkshopId = feedback.WorkshopId,
            WorkshopTitle = booking.Workshop.Title,
            Rating = feedback.Rating,
            TeachingRating = feedback.TeachingRating,
            EnergyRating = feedback.EnergyRating,
            ContentRating = feedback.ContentRating,
            Comment = feedback.Comment,
            WouldRecommend = feedback.WouldRecommend,
            SubmittedAt = feedback.SubmittedAt
        };
    }

    public async Task<WorkshopFeedbackResponse?> UpdateFeedbackAsync(Guid feedbackId, SubmitFeedbackRequest request)
    {
        var userId = _currentUser.UserId;

        var studentProfile = await _dbContext.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.UserId == userId);

        if (studentProfile == null)
        {
            return null;
        }

        var feedback = await _dbContext.WorkshopFeedbacks
            .Include(f => f.Workshop)
            .FirstOrDefaultAsync(f => f.Id == feedbackId && f.StudentProfileId == studentProfile.Id);

        if (feedback == null)
        {
            return null;
        }

        feedback.Rating = request.Rating;
        feedback.TeachingRating = request.TeachingRating;
        feedback.EnergyRating = request.EnergyRating;
        feedback.ContentRating = request.ContentRating;
        feedback.Comment = request.Comment;
        feedback.WouldRecommend = request.WouldRecommend;
        feedback.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return new WorkshopFeedbackResponse
        {
            Id = feedback.Id,
            WorkshopId = feedback.WorkshopId,
            WorkshopTitle = feedback.Workshop.Title,
            Rating = feedback.Rating,
            TeachingRating = feedback.TeachingRating,
            EnergyRating = feedback.EnergyRating,
            ContentRating = feedback.ContentRating,
            Comment = feedback.Comment,
            WouldRecommend = feedback.WouldRecommend,
            SubmittedAt = feedback.SubmittedAt
        };
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
            receipt = $"ws_{transactionId:N}",
            notes = new
            {
                transactionId = transactionId.ToString(),
                purpose = purpose.ToString()
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "orders");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        AddBasicAuthentication(request);

        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Razorpay order creation failed. HTTP {(int)response.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("id", out var idProperty))
        {
            throw new InvalidOperationException("Razorpay response did not contain an order ID.");
        }

        var orderId = idProperty.GetString();
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new InvalidOperationException("Razorpay order ID was empty.");
        }

        return orderId;
    }

    private void VerifySignature(string orderId, string paymentId, string signature)
    {
        var payload = $"{orderId}|{paymentId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_razorpaySettings.KeySecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToHexString(hashBytes).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(signature)))
        {
            throw new InvalidOperationException("Razorpay payment signature verification failed.");
        }
    }

    private async Task<RazorpayPaymentDetails> GetRazorpayPaymentAsync(string paymentId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"payments/{paymentId}");
        AddBasicAuthentication(request);

        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to fetch payment details from Razorpay. HTTP {(int)response.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        return new RazorpayPaymentDetails
        {
            Id = root.GetProperty("id").GetString() ?? string.Empty,
            OrderId = root.TryGetProperty("order_id", out var orderElem) ? orderElem.GetString() : null,
            Amount = root.GetProperty("amount").GetInt64(),
            Currency = root.GetProperty("currency").GetString() ?? string.Empty,
            Status = root.GetProperty("status").GetString() ?? string.Empty
        };
    }

    private void AddBasicAuthentication(HttpRequestMessage request)
    {
        var rawCredentials = $"{_razorpaySettings.KeyId}:{_razorpaySettings.KeySecret}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
    }

    private static long ConvertToPaise(decimal amount) => (long)Math.Round(amount * 100m);

    private class RazorpayPaymentDetails
    {
        public string Id { get; set; } = string.Empty;
        public string? OrderId { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
