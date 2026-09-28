using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Payments;
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
    private readonly IPaymentFulfillmentService _fulfillmentService;
    private readonly RazorpaySettings _razorpaySettings;
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _environment;

    public WorkshopService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IWorkshopPricingService pricingService,
        INotificationService notificationService,
        IWorkshopTicketService ticketService,
        IPaymentFulfillmentService fulfillmentService,
        IOptions<RazorpaySettings> razorpaySettings,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _pricingService = pricingService;
        _notificationService = notificationService;
        _ticketService = ticketService;
        _fulfillmentService = fulfillmentService;
        _razorpaySettings = razorpaySettings.Value;
        _environment = environment;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.razorpay.com/v1/")
        };
    }

    private WorkshopResponse MapWorkshopResponse(
        Workshop w,
        int bookedSeats,
        IReadOnlyList<WorkshopPricingTier>? workshopTiers,
        IReadOnlyDictionary<Guid, int> sessionBookedCounts,
        IReadOnlyDictionary<Guid, int> passBookedCounts,
        bool isStudentEligible,
        DateTime nowUtc)
    {
        var pricing = _pricingService.CalculatePricing(w, workshopTiers, bookedSeats, isStudentEligible);

        var trainers = w.WorkshopTrainers?.OrderBy(wt => wt.DisplayOrder).Select(wt => new WorkshopTrainerDto
        {
            TrainerProfileId = wt.TrainerProfileId,
            Name = wt.TrainerProfile?.FullName ?? "Ethos Trainer",
            PhotoUrl = wt.TrainerProfile?.ProfilePhotoUrl,
            DanceStyles = wt.TrainerProfile?.PrimaryDanceStyle,
            DisplayOrder = wt.DisplayOrder
        }).ToList() ?? new();

        var sessions = new List<WorkshopSessionDto>();
        if (w.Sessions != null && w.Sessions.Count > 0)
        {
            sessions = w.Sessions.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime).Select(s =>
            {
                var sBooked = sessionBookedCounts.GetValueOrDefault(s.Id, 0);
                var sRemaining = Math.Max(0, s.Capacity - sBooked);
                var cutoffUtc = s.GetBookingCutoffUtc(w.Timezone ?? "Asia/Kolkata");
                var isClosed = nowUtc >= cutoffUtc;
                var isPast = s.IsCompleted(nowUtc, w.Timezone ?? "Asia/Kolkata");

                string availLabel = isPast
                    ? "Past"
                    : (isClosed
                        ? "Closed"
                        : (sRemaining <= 0
                            ? "Sold Out"
                            : (sRemaining <= 5
                                ? "Selling Fast"
                                : "Available")));

                var sessionTrainers = s.SessionTrainers != null && s.SessionTrainers.Count > 0
                    ? s.SessionTrainers.OrderBy(st => st.DisplayOrder).Select(st => new WorkshopTrainerDto
                    {
                        TrainerProfileId = st.TrainerProfileId,
                        Name = st.TrainerProfile?.FullName ?? "Ethos Faculty",
                        PhotoUrl = st.TrainerProfile?.ProfilePhotoUrl,
                        DanceStyles = st.TrainerProfile?.PrimaryDanceStyle,
                        DisplayOrder = st.DisplayOrder
                    }).ToList()
                    : (s.TrainerProfile != null
                        ? new List<WorkshopTrainerDto>
                        {
                            new WorkshopTrainerDto
                            {
                                TrainerProfileId = s.TrainerProfileId,
                                Name = s.TrainerProfile.FullName,
                                PhotoUrl = s.TrainerProfile.ProfilePhotoUrl,
                                DanceStyles = s.TrainerProfile.PrimaryDanceStyle,
                                DisplayOrder = 0
                            }
                        }
                        : new List<WorkshopTrainerDto>());

                return new WorkshopSessionDto
                {
                    Id = s.Id,
                    WorkshopId = s.WorkshopId,
                    SessionDate = s.SessionDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    TrainerProfileId = s.TrainerProfileId,
                    TrainerName = s.TrainerProfile?.FullName ?? (sessionTrainers.Count > 0 ? sessionTrainers[0].Name : "Ethos Faculty"),
                    TrainerPhotoUrl = s.TrainerProfile?.ProfilePhotoUrl ?? (sessionTrainers.Count > 0 ? sessionTrainers[0].PhotoUrl : null),
                    Title = s.Title,
                    Description = s.Description,
                    Capacity = s.Capacity,
                    BookedSeats = sBooked,
                    RemainingSeats = sRemaining,
                    IsFull = sRemaining <= 0,
                    BookingCutoffTime = s.BookingCutoffTime,
                    BookingCutoffUtc = cutoffUtc,
                    IsBookingClosed = isClosed,
                    DisplayOrder = s.DisplayOrder,
                    IsActive = s.IsActive,
                    PosterImageUrl = s.PosterImageUrl ?? w.ImageUrl,
                    Trainers = sessionTrainers,
                    AvailabilityLabel = availLabel
                };
            }).ToList();
        }
        else
        {
            var isSinglePast = w.EndUtc.HasValue
                ? nowUtc >= w.EndUtc.Value
                : (w.WorkshopDate.Date + w.EndTime <= nowUtc);
            var isSingleClosed = w.IsBookingClosed(nowUtc);
            string singleAvailLabel = isSinglePast
                ? "Past"
                : (isSingleClosed
                    ? "Closed"
                    : (pricing.RemainingSeats <= 0
                        ? "Sold Out"
                        : (pricing.RemainingSeats <= 5
                            ? "Selling Fast"
                            : "Available")));

            sessions.Add(new WorkshopSessionDto
            {
                Id = w.Id,
                WorkshopId = w.Id,
                SessionDate = w.WorkshopDate,
                StartTime = w.StartTime,
                EndTime = w.EndTime,
                TrainerProfileId = w.TrainerProfileId ?? Guid.Empty,
                TrainerName = w.TrainerProfile?.FullName ?? (trainers.Count > 0 ? trainers[0].Name : "Ethos Faculty"),
                TrainerPhotoUrl = w.TrainerProfile?.ProfilePhotoUrl ?? (trainers.Count > 0 ? trainers[0].PhotoUrl : null),
                Title = w.Title,
                Description = w.Description,
                Capacity = w.Capacity,
                BookedSeats = pricing.BookedSeats,
                RemainingSeats = pricing.RemainingSeats,
                IsFull = pricing.IsFull,
                BookingCutoffTime = w.BookingCutoffTime,
                BookingCutoffUtc = w.GetBookingCutoffUtc(),
                IsBookingClosed = isSingleClosed,
                DisplayOrder = 0,
                IsActive = true,
                PosterImageUrl = w.ImageUrl,
                Trainers = trainers,
                AvailabilityLabel = singleAvailLabel
            });
        }

        var passTypes = w.PassTypes?.OrderBy(p => p.DisplayOrder).Select(p =>
        {
            var pTotal = p.TotalQuantity > 0 ? p.TotalQuantity : 1000;
            var pBooked = passBookedCounts.GetValueOrDefault(p.Id, 0);
            var pRemainingQuota = Math.Max(0, pTotal - pBooked);

            WorkshopSessionDto? linkedSessionDto = null;
            if (p.WorkshopSessionId.HasValue)
            {
                linkedSessionDto = sessions.FirstOrDefault(s => s.Id == p.WorkshopSessionId.Value);
            }

            int pRemaining;
            bool isPassClosed = (p.SalesEndUtc.HasValue && nowUtc >= p.SalesEndUtc.Value) ||
                           (p.SalesStartUtc.HasValue && nowUtc < p.SalesStartUtc.Value);

            if (p.WorkshopSessionId.HasValue)
            {
                int sRemaining = linkedSessionDto?.RemainingSeats ?? 0;
                bool isLinkedClosed = linkedSessionDto == null || linkedSessionDto.IsBookingClosed;
                pRemaining = isLinkedClosed ? 0 : Math.Min(pRemainingQuota, sRemaining);
                if (isLinkedClosed) isPassClosed = true;
            }
            else if (!p.SessionsIncluded.HasValue)
            {
                var activeSessions = sessions.Where(s => !s.IsBookingClosed).ToList();
                int minRemaining = activeSessions.Count > 0 ? activeSessions.Min(s => s.RemainingSeats) : 0;
                pRemaining = Math.Min(pRemainingQuota, minRemaining);
                if (activeSessions.Count == 0 || minRemaining <= 0) isPassClosed = true;
            }
            else
            {
                int n = p.SessionsIncluded.Value;
                int availableActiveSessionsCount = sessions.Count(s => !s.IsBookingClosed && s.RemainingSeats >= 1);
                if (availableActiveSessionsCount < n)
                {
                    pRemaining = 0;
                    isPassClosed = true;
                }
                else
                {
                    pRemaining = pRemainingQuota;
                }
            }

            if (pRemaining <= 0) isPassClosed = true;

            string passAvailLabel = (linkedSessionDto != null && linkedSessionDto.AvailabilityLabel == "Past")
                ? "Past"
                : (isPassClosed && pRemaining > 0
                    ? "Closed"
                    : (pRemaining <= 0
                        ? "Sold Out"
                        : (pRemaining <= 5
                            ? "Selling Fast"
                            : "Available")));

            var tiers = p.PricingTiers?.OrderBy(t => t.TierNumber).ToList() ?? new List<WorkshopPricingTier>();
            decimal currentPrice = p.Price;
            decimal? nextTierPrice = null;
            int currentTierNum = 1;
            string? currentTierName = null;
            int remainingInTier = pRemaining;

            var tierDtos = tiers.Select(t =>
            {
                return new WorkshopPricingTierDto
                {
                    TierNumber = t.TierNumber,
                    TierName = t.TierName,
                    MinTickets = t.MinTickets,
                    MaxTickets = t.MaxTickets,
                    Price = t.Price,
                    Status = "UPCOMING"
                };
            }).ToList();

            if (tiers.Count > 0)
            {
                int nextSlot = pBooked + 1;
                var activeTier = tiers.FirstOrDefault(t => nextSlot >= t.MinTickets && (t.MaxTickets == null || nextSlot <= t.MaxTickets))
                    ?? tiers.Last();

                currentTierNum = activeTier.TierNumber;
                currentTierName = activeTier.TierName;
                currentPrice = activeTier.Price;

                var nextTier = tiers.FirstOrDefault(t => t.TierNumber > currentTierNum);
                if (nextTier != null) nextTierPrice = nextTier.Price;

                int minSeats = activeTier.MinTickets > 0 ? activeTier.MinTickets : 1;
                int? maxSeats = activeTier.MaxTickets;
                int tierCap = maxSeats.HasValue ? Math.Max(1, maxSeats.Value - minSeats + 1) : Math.Max(1, pTotal - minSeats + 1);
                int filledInTier = Math.Clamp(pBooked - (minSeats - 1), 0, tierCap);
                remainingInTier = Math.Max(0, tierCap - filledInTier);

                foreach (var td in tierDtos)
                {
                    if (td.TierNumber < currentTierNum) td.Status = "COMPLETED";
                    else if (td.TierNumber == currentTierNum) td.Status = "ACTIVE";
                    else td.Status = "UPCOMING";
                }
            }

            return new WorkshopPassTypeDto
            {
                Id = p.Id,
                WorkshopId = p.WorkshopId,
                WorkshopSessionId = p.WorkshopSessionId,
                SessionTitle = linkedSessionDto?.Title,
                SessionDate = linkedSessionDto?.SessionDate,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                CurrentPrice = currentPrice,
                NextTierPrice = nextTierPrice,
                CurrentTierNumber = currentTierNum,
                CurrentTierName = currentTierName,
                TicketsRemainingInCurrentTier = remainingInTier,
                PricingTiers = tierDtos,
                SessionsIncluded = p.SessionsIncluded,
                TotalQuantity = pTotal,
                RemainingQuantity = pRemaining,
                SalesStartUtc = p.SalesStartUtc,
                SalesEndUtc = p.SalesEndUtc,
                IsSalesClosed = isPassClosed,
                DisplayOrder = p.DisplayOrder,
                IsActive = p.IsActive,
                AvailabilityLabel = passAvailLabel
            };
        }).ToList() ?? new();

        return new WorkshopResponse
        {
            Id = w.Id,
            IsEthosOriginal = w.IsEthosOriginal,
            Title = w.Title,
            Description = w.Description,
            DanceStyle = w.DanceStyle,
            Level = w.Level,
            WorkshopDate = w.WorkshopDate,
            StartTime = w.StartTime,
            EndTime = w.EndTime,
            Venue = w.Venue,
            TrainerName = w.TrainerProfile?.FullName ?? (trainers.Count > 0 ? trainers[0].Name : "Ethos Faculty"),
            TrainerPhotoUrl = w.TrainerProfile?.ProfilePhotoUrl ?? (trainers.Count > 0 ? trainers[0].PhotoUrl : null),
            TrainerDanceStyles = w.TrainerProfile?.PrimaryDanceStyle ?? (trainers.Count > 0 ? trainers[0].DanceStyles : null),
            StartingPrice = passTypes.Count > 0 ? passTypes.Min(p => p.Price) : pricing.StartingPrice,
            CurrentPrice = passTypes.Count > 0 ? passTypes.Min(p => p.Price) : pricing.CurrentPublicPrice,
            StudentPrice = pricing.StudentPrice,
            Price = pricing.FinalAmount,
            Capacity = w.Capacity,
            BookedSeats = pricing.BookedSeats,
            RemainingSeats = pricing.RemainingSeats,
            IsFull = pricing.IsFull,
            IsStudentEligible = pricing.IsStudentEligible,
            BookingsCount = pricing.BookedSeats,
            Status = w.Status,
            ImageUrl = w.ImageUrl,
            LandscapeImageUrl = w.LandscapeImageUrl,
            City = w.City,
            Area = w.Area,
            VenueAddress = w.VenueAddress,
            LocationUrl = w.LocationUrl,
            ShortDescription = w.ShortDescription,
            PublicVisibility = w.PublicVisibility,
            StartUtc = w.StartUtc,
            EndUtc = w.EndUtc,
            BookingCutoffTime = w.BookingCutoffTime,
            BookingCutoffUtc = w.GetBookingCutoffUtc(),
            IsBookingClosed = sessions.Count > 0 ? sessions.All(s => s.IsBookingClosed) : w.IsBookingClosed(nowUtc),
            Trainers = trainers,
            Sessions = sessions,
            PassTypes = passTypes
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

        // Query 1: Fetch approved / published workshops with their child collections (excluding unused Bookings)
        var workshops = await _dbContext.Workshops
            .AsNoTracking()
            .AsSplitQuery()
            .Include(w => w.TrainerProfile)
            .Include(w => w.WorkshopTrainers)
                .ThenInclude(wt => wt.TrainerProfile)
            .Include(w => w.Sessions)
                .ThenInclude(s => s.TrainerProfile)
            .Include(w => w.Sessions)
                .ThenInclude(s => s.SessionTrainers)
                    .ThenInclude(st => st.TrainerProfile)
            .Include(w => w.PassTypes)
                .ThenInclude(p => p.PricingTiers)
            .Where(w => w.PublicVisibility && (w.Status == WorkshopStatus.Published || w.Status == WorkshopStatus.Approved))
            .OrderBy(w => w.WorkshopDate)
            .ToListAsync();

        if (workshops.Count == 0)
        {
            return Array.Empty<WorkshopResponse>();
        }

        var workshopIds = workshops.Select(w => w.Id).ToList();
        var allSessionIds = workshops.SelectMany(w => w.Sessions).Select(s => s.Id).ToList();
        var allPassTypeIds = workshops.SelectMany(w => w.PassTypes).Select(p => p.Id).ToList();
        var nowUtc = DateTime.UtcNow;

        // Query 2: Batch sum booked seats per workshop
        var workshopBookedCounts = await _dbContext.WorkshopBookings
            .AsNoTracking()
            .Where(b => workshopIds.Contains(b.WorkshopId) &&
                       (b.Status == WorkshopBookingStatus.Confirmed ||
                        b.Status == WorkshopBookingStatus.Attended ||
                        (b.Status == WorkshopBookingStatus.PendingPayment &&
                         b.ReservationExpiresAt.HasValue &&
                         b.ReservationExpiresAt.Value > nowUtc)))
            .GroupBy(b => b.WorkshopId)
            .Select(g => new { WorkshopId = g.Key, Count = g.Sum(b => (int?)b.Quantity) ?? 0 })
            .ToDictionaryAsync(g => g.WorkshopId, g => g.Count);

        // Query 3: Batch count booked seats per session
        var sessionBookedCounts = allSessionIds.Count > 0
            ? await _dbContext.WorkshopBookingSessions
                .AsNoTracking()
                .Where(bs => allSessionIds.Contains(bs.WorkshopSessionId) &&
                             bs.Status == WorkshopBookingSessionStatus.Booked &&
                             (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                              bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                              (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                               bs.WorkshopBooking.ReservationExpiresAt > nowUtc)))
                .GroupBy(bs => bs.WorkshopSessionId)
                .Select(g => new { SessionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.SessionId, g => g.Count)
            : new Dictionary<Guid, int>();

        // Query 4: Batch sum booked quantity per pass type
        var passBookedCounts = allPassTypeIds.Count > 0
            ? await _dbContext.WorkshopBookings
                .AsNoTracking()
                .Where(b => b.WorkshopPassTypeId.HasValue &&
                            allPassTypeIds.Contains(b.WorkshopPassTypeId.Value) &&
                            (b.Status == WorkshopBookingStatus.Confirmed ||
                             b.Status == WorkshopBookingStatus.Attended ||
                             (b.Status == WorkshopBookingStatus.PendingPayment &&
                              b.ReservationExpiresAt > nowUtc)))
                .GroupBy(b => b.WorkshopPassTypeId!.Value)
                .Select(g => new { PassId = g.Key, Count = g.Sum(b => (int?)b.Quantity) ?? 0 })
                .ToDictionaryAsync(g => g.PassId, g => g.Count)
            : new Dictionary<Guid, int>();

        // Query 5: Batch query standard pricing tiers for all workshops
        var standardTiers = await _dbContext.WorkshopPricingTiers
            .AsNoTracking()
            .Where(t => workshopIds.Contains(t.WorkshopId) && t.WorkshopPassTypeId == null)
            .OrderBy(t => t.TierNumber)
            .ToListAsync();
        var tiersByWorkshop = standardTiers
            .GroupBy(t => t.WorkshopId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<WorkshopPricingTier>)g.ToList());

        // Check student eligibility once
        var isStudentEligible = await _pricingService.IsStudentEligibleAsync(currentUserId);

        // Map synchronously in-memory (0 database queries during loop)
        var result = new List<WorkshopResponse>(workshops.Count);
        foreach (var w in workshops)
        {
            var bookedSeats = workshopBookedCounts.GetValueOrDefault(w.Id, 0);
            tiersByWorkshop.TryGetValue(w.Id, out var tiers);

            result.Add(MapWorkshopResponse(
                w,
                bookedSeats,
                tiers,
                sessionBookedCounts,
                passBookedCounts,
                isStudentEligible,
                nowUtc));
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
            .AsSplitQuery()
            .Include(w => w.TrainerProfile)
            .Include(w => w.WorkshopTrainers)
                .ThenInclude(wt => wt.TrainerProfile)
            .Include(w => w.Sessions)
                .ThenInclude(s => s.TrainerProfile)
            .Include(w => w.Sessions)
                .ThenInclude(s => s.SessionTrainers)
                    .ThenInclude(st => st.TrainerProfile)
            .Include(w => w.PassTypes)
                .ThenInclude(p => p.PricingTiers)
            .FirstOrDefaultAsync(w => w.Id == id && w.PublicVisibility && (w.Status == WorkshopStatus.Published || w.Status == WorkshopStatus.Approved));

        if (workshop == null)
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;
        var sessionIds = workshop.Sessions.Select(s => s.Id).ToList();
        var passIds = workshop.PassTypes.Select(p => p.Id).ToList();

        var workshopBookedCount = await _dbContext.WorkshopBookings
            .AsNoTracking()
            .Where(b => b.WorkshopId == id &&
                       (b.Status == WorkshopBookingStatus.Confirmed ||
                        b.Status == WorkshopBookingStatus.Attended ||
                        (b.Status == WorkshopBookingStatus.PendingPayment &&
                         b.ReservationExpiresAt.HasValue &&
                         b.ReservationExpiresAt.Value > nowUtc)))
            .SumAsync(b => (int?)b.Quantity) ?? 0;

        var sessionBookedCounts = sessionIds.Count > 0
            ? await _dbContext.WorkshopBookingSessions
                .AsNoTracking()
                .Where(bs => sessionIds.Contains(bs.WorkshopSessionId) &&
                             bs.Status == WorkshopBookingSessionStatus.Booked &&
                             (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                              bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                              (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                               bs.WorkshopBooking.ReservationExpiresAt > nowUtc)))
                .GroupBy(bs => bs.WorkshopSessionId)
                .Select(g => new { SessionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.SessionId, g => g.Count)
            : new Dictionary<Guid, int>();

        var passBookedCounts = passIds.Count > 0
            ? await _dbContext.WorkshopBookings
                .AsNoTracking()
                .Where(b => b.WorkshopPassTypeId.HasValue &&
                            passIds.Contains(b.WorkshopPassTypeId.Value) &&
                            (b.Status == WorkshopBookingStatus.Confirmed ||
                             b.Status == WorkshopBookingStatus.Attended ||
                             (b.Status == WorkshopBookingStatus.PendingPayment &&
                              b.ReservationExpiresAt > nowUtc)))
                .GroupBy(b => b.WorkshopPassTypeId!.Value)
                .Select(g => new { PassId = g.Key, Count = g.Sum(b => (int?)b.Quantity) ?? 0 })
                .ToDictionaryAsync(g => g.PassId, g => g.Count)
            : new Dictionary<Guid, int>();

        var tiers = await _dbContext.WorkshopPricingTiers
            .AsNoTracking()
            .Where(t => t.WorkshopId == id && t.WorkshopPassTypeId == null)
            .OrderBy(t => t.TierNumber)
            .ToListAsync();

        var isStudentEligible = await _pricingService.IsStudentEligibleAsync(currentUserId);

        return MapWorkshopResponse(
            workshop,
            workshopBookedCount,
            tiers,
            sessionBookedCounts,
            passBookedCounts,
            isStudentEligible,
            nowUtc);
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
        Guid? passTypeId = null,
        List<Guid>? selectedSessionIds = null,
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

        if (quantity < 1 || quantity > 10)
        {
            throw new ArgumentException("Ticket quantity must be between 1 and 10.");
        }
        var nowUtc = DateTime.UtcNow;

        if (passTypeId.HasValue)
        {
            var pass = await _dbContext.WorkshopPassTypes
                .Include(p => p.PricingTiers)
                .Include(p => p.Workshop)
                    .ThenInclude(w => w.Sessions)
                .FirstOrDefaultAsync(p => p.Id == passTypeId.Value && p.WorkshopId == id && p.IsActive, cancellationToken);

            if (pass == null)
            {
                throw new ArgumentException("Selected ticket type was not found or is inactive.");
            }

            var workshop = pass.Workshop;
            var timezone = workshop?.Timezone ?? "Asia/Kolkata";
            var activeSessions = workshop?.Sessions.Where(s => s.IsActive).ToList() ?? new List<WorkshopSession>();

            // Category-specific session validation
            if (pass.WorkshopSessionId.HasValue && !pass.SessionsIncluded.HasValue)
            {
                // Single-Session pass (legacy fixed admin-bound)
                var linkedSession = activeSessions.FirstOrDefault(s => s.Id == pass.WorkshopSessionId.Value);
                if (linkedSession == null)
                {
                    throw new InvalidOperationException($"The session assigned to ticket '{pass.Name}' is not active or not found.");
                }
                if (linkedSession.IsBookingClosed(nowUtc, timezone))
                {
                    throw new InvalidOperationException($"Booking cutoff for session '{linkedSession.Title}' has passed.");
                }

                var sBookedSeats = await _dbContext.WorkshopBookingSessions
                    .Where(bs => bs.WorkshopSessionId == linkedSession.Id &&
                                 bs.Status == WorkshopBookingSessionStatus.Booked &&
                                 (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                                  bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                                  (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                                   bs.WorkshopBooking.ReservationExpiresAt > nowUtc)))
                    .CountAsync(cancellationToken);

                if (sBookedSeats + quantity > linkedSession.Capacity)
                {
                    var rem = Math.Max(0, linkedSession.Capacity - sBookedSeats);
                    throw new InvalidOperationException($"Session '{linkedSession.Title}' has only {rem} seats remaining.");
                }
            }
            else if (pass.SessionsIncluded.HasValue)
            {
                // Multi-Session Bundle
                if (selectedSessionIds != null && selectedSessionIds.Count > 0)
                {
                    if (selectedSessionIds.Count != pass.SessionsIncluded.Value)
                    {
                        throw new ArgumentException($"Please select exactly {pass.SessionsIncluded.Value} sessions for {pass.Name}.");
                    }
                    if (selectedSessionIds.Distinct().Count() != pass.SessionsIncluded.Value)
                    {
                        throw new ArgumentException("Duplicate sessions selected. Each session must be unique.");
                    }

                    var targetSessions = activeSessions.Where(s => selectedSessionIds.Contains(s.Id)).ToList();
                    if (targetSessions.Count != pass.SessionsIncluded.Value)
                    {
                        throw new ArgumentException("One or more selected sessions are invalid or inactive.");
                    }

                    // Overlap validation among selected sessions ([start, end) half-open interval)
                    for (int i = 0; i < targetSessions.Count; i++)
                    {
                        var s1 = targetSessions[i];
                        for (int j = i + 1; j < targetSessions.Count; j++)
                        {
                            var s2 = targetSessions[j];
                            if (s1.SessionDate.Date == s2.SessionDate.Date)
                            {
                                if (s1.StartTime < s2.EndTime && s2.StartTime < s1.EndTime)
                                {
                                    throw new InvalidOperationException($"Selected sessions '{s1.Title}' and '{s2.Title}' overlap in time on {s1.SessionDate:yyyy-MM-dd}.");
                                }
                            }
                        }
                    }

                    foreach (var s in targetSessions)
                    {
                        if (s.IsBookingClosed(nowUtc, timezone))
                        {
                            throw new InvalidOperationException($"Booking cutoff for session '{s.Title}' has passed.");
                        }

                        var sBookedSeats = await _dbContext.WorkshopBookingSessions
                            .Where(bs => bs.WorkshopSessionId == s.Id &&
                                         bs.Status == WorkshopBookingSessionStatus.Booked &&
                                         (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                                          bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                                          (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                                           bs.WorkshopBooking.ReservationExpiresAt > nowUtc)))
                            .CountAsync(cancellationToken);

                        if (sBookedSeats + quantity > s.Capacity)
                        {
                            var rem = Math.Max(0, s.Capacity - sBookedSeats);
                            throw new InvalidOperationException($"Session '{s.Title}' has only {rem} seats remaining.");
                        }
                    }
                }
            }
            else
            {
                // All-Access pass: check that all active sessions are open and have capacity >= quantity
                if (activeSessions.Count == 0)
                {
                    throw new InvalidOperationException("Workshop has no active sessions configured.");
                }

                foreach (var s in activeSessions)
                {
                    if (s.IsBookingClosed(nowUtc, timezone))
                    {
                        throw new InvalidOperationException($"All-Access is unavailable because session '{s.Title}' has passed its booking cutoff.");
                    }

                    var sBookedSeats = await _dbContext.WorkshopBookingSessions
                        .Where(bs => bs.WorkshopSessionId == s.Id &&
                                     bs.Status == WorkshopBookingSessionStatus.Booked &&
                                     (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                                      bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                                      (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                                       bs.WorkshopBooking.ReservationExpiresAt > nowUtc)))
                        .CountAsync(cancellationToken);

                    if (sBookedSeats + quantity > s.Capacity)
                    {
                        var rem = Math.Max(0, s.Capacity - sBookedSeats);
                        throw new InvalidOperationException($"All-Access is unavailable because session '{s.Title}' has only {rem} seats remaining.");
                    }
                }
            }

            // Check pass quota
            var passSold = await _dbContext.WorkshopBookings
                .Where(b => b.WorkshopPassTypeId == pass.Id &&
                            (b.Status == WorkshopBookingStatus.Confirmed ||
                             b.Status == WorkshopBookingStatus.Attended ||
                             (b.Status == WorkshopBookingStatus.PendingPayment &&
                              b.ReservationExpiresAt > nowUtc)))
                .SumAsync(b => (int?)b.Quantity, cancellationToken) ?? 0;

            if (passSold + quantity > pass.TotalQuantity)
            {
                var remaining = Math.Max(0, pass.TotalQuantity - passSold);
                throw new InvalidOperationException($"Ticket '{pass.Name}' has only {remaining} tickets remaining.");
            }

            return await _pricingService.CalculateTicketTypeQuoteAsync(
                pass,
                quantity,
                passSold,
                currentUserId,
                cancellationToken);
        }

        return await _pricingService.CalculateQuoteAsync(id, quantity, currentUserId, cancellationToken);
    }

    public async Task<CreateWorkshopOrderResponse> CreateWorkshopOrderAsync(
        Guid workshopId,
        CreateWorkshopOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        request ??= new CreateWorkshopOrderRequest();
        if (request.Quantity < 1 || request.Quantity > 10)
        {
            throw new ArgumentException("Ticket quantity must be between 1 and 10.");
        }
        var quantity = request.Quantity;

        var idempotencyKey = request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            idempotencyKey = Guid.NewGuid().ToString("N");
        }
        else if (idempotencyKey.Length > 128)
        {
            idempotencyKey = idempotencyKey[..128];
        }

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

        // 1. Authoritative idempotency check with customer & workshop tenant isolation + request fingerprinting
        var existingKeyBooking = await _dbContext.WorkshopBookings
            .FirstOrDefaultAsync(b => b.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingKeyBooking != null)
        {
            bool matchesWorkshop = existingKeyBooking.WorkshopId == workshopId;
            bool matchesCustomer = false;

            if (studentProfile != null)
            {
                matchesCustomer = existingKeyBooking.StudentProfileId == studentProfile.Id;
            }
            else if (!string.IsNullOrWhiteSpace(request.Email))
            {
                matchesCustomer = string.Equals(existingKeyBooking.GuestEmail, request.Email.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            else if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                matchesCustomer = string.Equals(existingKeyBooking.GuestPhone, request.Phone.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                matchesCustomer = true;
            }

            // Fingerprint check: passTypeId, quantity, and selected sessions must match exactly
            bool matchesPass = existingKeyBooking.WorkshopPassTypeId == request.PassTypeId;
            bool matchesQuantity = existingKeyBooking.Quantity == quantity;

            var existingSessionIds = string.IsNullOrWhiteSpace(existingKeyBooking.SelectedSessionIdsJson)
                ? new List<Guid>()
                : System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(existingKeyBooking.SelectedSessionIdsJson) ?? new List<Guid>();

            var reqSessionIds = (request.SelectedSessionIds ?? new List<Guid>()).OrderBy(x => x).ToList();
            var storedSortedIds = existingSessionIds.OrderBy(x => x).ToList();
            bool matchesSessions = reqSessionIds.SequenceEqual(storedSortedIds);

            if (!matchesWorkshop || !matchesCustomer)
            {
                throw new InvalidOperationException("Idempotency key already belongs to another request.");
            }

            if (!matchesPass || !matchesQuantity || !matchesSessions)
            {
                throw new InvalidOperationException("Idempotency key was previously used with different order parameters.");
            }

            var existingTx = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(t => t.Id == existingKeyBooking.PaymentTransactionId, cancellationToken);
            var ws = await _dbContext.Workshops.FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

            bool existingIsSplitTier = false;
            string? existingSplitTierMessage = null;
            List<WorkshopPriceQuoteItem> existingBreakdown = new();

            if (!string.IsNullOrWhiteSpace(existingKeyBooking.PriceBreakdownJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(existingKeyBooking.PriceBreakdownJson);
                    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("IsSplitTier", out var stProp))
                            existingIsSplitTier = stProp.GetBoolean();
                        if (doc.RootElement.TryGetProperty("SplitTierMessage", out var msgProp))
                            existingSplitTierMessage = msgProp.GetString();
                        if (doc.RootElement.TryGetProperty("Breakdown", out var bdProp) && bdProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            existingBreakdown = System.Text.Json.JsonSerializer.Deserialize<List<WorkshopPriceQuoteItem>>(bdProp.GetRawText()) ?? new();
                        }
                    }
                    else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        existingBreakdown = System.Text.Json.JsonSerializer.Deserialize<List<WorkshopPriceQuoteItem>>(existingKeyBooking.PriceBreakdownJson) ?? new();
                    }
                }
                catch
                {
                    // ignore fallback
                }
            }

            if (existingBreakdown.Count == 0 && !existingKeyBooking.WorkshopPassTypeId.HasValue)
            {
                var existingQuote = await _pricingService.CalculateQuoteAsync(workshopId, existingKeyBooking.Quantity, null, cancellationToken);
                existingIsSplitTier = existingQuote.IsSplitTier;
                existingSplitTierMessage = existingQuote.SplitTierMessage;
                existingBreakdown = existingQuote.Breakdown;
            }

            return new CreateWorkshopOrderResponse
            {
                BookingId = existingKeyBooking.Id,
                TransactionId = existingTx?.Id ?? Guid.Empty,
                WorkshopId = workshopId,
                WorkshopTitle = ws?.Title ?? "Workshop",
                Quantity = existingKeyBooking.Quantity,
                Amount = existingKeyBooking.TotalPrice,
                Currency = "INR",
                RazorpayOrderId = existingTx?.RazorpayOrderId ?? "",
                RazorpayKeyId = string.IsNullOrWhiteSpace(_razorpaySettings.KeyId) ? "rzp_test_placeholder" : _razorpaySettings.KeyId,
                IsStudentDiscountApplied = false,
                IsSplitTier = existingIsSplitTier,
                SplitTierMessage = existingSplitTierMessage,
                Breakdown = existingBreakdown
            };
        }

        var workshop = await _dbContext.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && (w.Status == WorkshopStatus.Published || w.Status == WorkshopStatus.Approved), cancellationToken);

        if (workshop == null)
        {
            throw new ArgumentException("Workshop was not found or is not approved.");
        }

        if (workshop.IsBookingClosed())
        {
            throw new InvalidOperationException("Bookings for this workshop are closed as the booking cutoff time has passed.");
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
                .FirstOrDefaultAsync(u => u.Email == guestEmail || u.Phone == guestPhone, cancellationToken);

            if (existingUser == null)
            {
                existingUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Email == guestEmail, cancellationToken)
                    ?? await _dbContext.Users.FirstOrDefaultAsync(u => u.Phone == guestPhone, cancellationToken);
            }

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
            else
            {
                if (!string.IsNullOrWhiteSpace(guestName)) existingUser.FullName = guestName;
                existingUser.UpdatedAt = DateTime.UtcNow;
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

        // PHASE 1: DB Transaction — Lock & verify effective capacity
        using var orderTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (_dbContext.Database.IsNpgsql())
        {
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM workshops WHERE \"Id\" = {workshopId} FOR UPDATE",
                cancellationToken);
        }

        var nowUtc = DateTime.UtcNow;

        WorkshopBooking newBooking;
        decimal finalAmount;
        var transactionId = Guid.NewGuid();
        bool isSplitTier = false;
        string? splitTierMessage = null;
        List<WorkshopPriceQuoteItem> breakdown = new();

        if (request.PassTypeId.HasValue)
        {
            var pass = await _dbContext.WorkshopPassTypes
                .Include(p => p.PricingTiers)
                .FirstOrDefaultAsync(p => p.Id == request.PassTypeId.Value && p.WorkshopId == workshopId && p.IsActive, cancellationToken);

            if (pass == null)
            {
                throw new ArgumentException("Selected ticket type was not found or is inactive.");
            }

            if (_dbContext.Database.IsNpgsql())
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"Id\" FROM workshop_pass_types WHERE \"Id\" = {pass.Id} FOR UPDATE",
                    cancellationToken);
            }

            if (!pass.IsSalesOpen(nowUtc))
            {
                throw new InvalidOperationException($"Sales for ticket '{pass.Name}' are currently closed.");
            }

            var allActiveSessions = await _dbContext.WorkshopSessions
                .Include(s => s.TrainerProfile)
                .Where(s => s.WorkshopId == workshopId && s.IsActive)
                .ToListAsync(cancellationToken);

            List<WorkshopSession> targetSessions;
            if (pass.WorkshopSessionId.HasValue && !pass.SessionsIncluded.HasValue)
            {
                var linkedSession = allActiveSessions.FirstOrDefault(s => s.Id == pass.WorkshopSessionId.Value);
                if (linkedSession == null)
                {
                    throw new InvalidOperationException($"The session assigned to ticket '{pass.Name}' is not active or not found.");
                }
                if (request.SelectedSessionIds != null && request.SelectedSessionIds.Count > 0)
                {
                    if (request.SelectedSessionIds.Count != 1 || request.SelectedSessionIds[0] != pass.WorkshopSessionId.Value)
                    {
                        throw new ArgumentException($"Invalid session selection for single-session ticket '{pass.Name}'.");
                    }
                }
                targetSessions = new List<WorkshopSession> { linkedSession };
            }
            else if (pass.SessionsIncluded.HasValue)
            {
                if (request.SelectedSessionIds == null || request.SelectedSessionIds.Count != pass.SessionsIncluded.Value)
                {
                    throw new ArgumentException($"Please select exactly {pass.SessionsIncluded.Value} sessions for {pass.Name}.");
                }
                if (request.SelectedSessionIds.Distinct().Count() != pass.SessionsIncluded.Value)
                {
                    throw new ArgumentException("Duplicate sessions selected. Each session must be unique.");
                }

                targetSessions = allActiveSessions.Where(s => request.SelectedSessionIds.Contains(s.Id)).ToList();
                if (targetSessions.Count != pass.SessionsIncluded.Value)
                {
                    throw new ArgumentException("One or more selected sessions are invalid or inactive.");
                }
            }
            else
            {
                if (allActiveSessions.Count == 0)
                {
                    throw new InvalidOperationException("Workshop has no active sessions configured.");
                }
                targetSessions = allActiveSessions;
            }

            // Overlap validation among selected sessions (for multi-session bundles)
            if (!pass.WorkshopSessionId.HasValue && pass.SessionsIncluded.HasValue && pass.SessionsIncluded.Value > 1)
            {
                for (int i = 0; i < targetSessions.Count; i++)
                {
                    var s1 = targetSessions[i];
                    for (int j = i + 1; j < targetSessions.Count; j++)
                    {
                        var s2 = targetSessions[j];
                        if (s1.SessionDate.Date == s2.SessionDate.Date)
                        {
                            if (s1.StartTime < s2.EndTime && s2.StartTime < s1.EndTime)
                            {
                                throw new InvalidOperationException($"Selected sessions '{s1.Title}' and '{s2.Title}' overlap in time on {s1.SessionDate:yyyy-MM-dd}.");
                            }
                        }
                    }
                }
            }

            var sortedSessionIds = targetSessions.Select(s => s.Id).OrderBy(id => id).ToList();

            if (_dbContext.Database.IsNpgsql())
            {
                var idListStr = string.Join(",", sortedSessionIds.Select(id => $"'{id}'::uuid"));
                await _dbContext.Database.ExecuteSqlRawAsync(
                    $"SELECT \"Id\" FROM workshop_sessions WHERE \"Id\" = ANY(ARRAY[{idListStr}]) ORDER BY \"Id\" FOR UPDATE",
                    cancellationToken);
            }

            bool isAllAccess = !pass.WorkshopSessionId.HasValue && !pass.SessionsIncluded.HasValue;
            foreach (var s in targetSessions)
            {
                if (s.IsBookingClosed(nowUtc, workshop.Timezone ?? "Asia/Kolkata"))
                {
                    if (isAllAccess)
                    {
                        throw new InvalidOperationException($"All-Access is unavailable because session '{s.Title}' has passed its booking cutoff.");
                    }
                    throw new InvalidOperationException($"Booking cutoff for session '{s.Title}' has passed.");
                }

                var sBookedSeats = await _dbContext.WorkshopBookingSessions
                    .Where(bs => bs.WorkshopSessionId == s.Id &&
                                 bs.Status == WorkshopBookingSessionStatus.Booked &&
                                 (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                                  bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                                  (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                                   bs.WorkshopBooking.ReservationExpiresAt > nowUtc)))
                    .CountAsync(cancellationToken);

                if (sBookedSeats + quantity > s.Capacity)
                {
                    var rem = Math.Max(0, s.Capacity - sBookedSeats);
                    if (isAllAccess)
                    {
                        throw new InvalidOperationException($"All-Access is unavailable because session '{s.Title}' has only {rem} seats remaining.");
                    }
                    throw new InvalidOperationException($"Session '{s.Title}' has only {rem} seats remaining.");
                }
            }

            var passSold = await _dbContext.WorkshopBookings
                .Where(b => b.WorkshopPassTypeId == pass.Id &&
                            (b.Status == WorkshopBookingStatus.Confirmed ||
                             b.Status == WorkshopBookingStatus.Attended ||
                             (b.Status == WorkshopBookingStatus.PendingPayment &&
                              b.ReservationExpiresAt > nowUtc)))
                .SumAsync(b => (int?)b.Quantity, cancellationToken) ?? 0;

            if (passSold + quantity > pass.TotalQuantity)
            {
                var remaining = Math.Max(0, pass.TotalQuantity - passSold);
                throw new InvalidOperationException($"Ticket '{pass.Name}' has only {remaining} tickets remaining.");
            }

            var quote = await _pricingService.CalculateTicketTypeQuoteAsync(
                pass,
                quantity,
                passSold,
                userId,
                cancellationToken);

            finalAmount = quote.TotalAmount;
            isSplitTier = quote.IsSplitTier;
            splitTierMessage = quote.SplitTierMessage;
            breakdown = quote.Breakdown;

            var passBreakdownJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                TicketTypeName = pass.Name,
                Quantity = quantity,
                TotalAmount = finalAmount,
                IsSplitTier = isSplitTier,
                SplitTierMessage = splitTierMessage,
                Breakdown = breakdown,
                Sessions = targetSessions.Select(t => new { t.Id, t.Title, t.SessionDate, t.StartTime, t.EndTime }).ToList()
            });

            newBooking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = studentProfile.Id,
                WorkshopPassTypeId = pass.Id,
                PassName = pass.Name,
                PassPrice = quantity > 0 ? Math.Round(finalAmount / quantity, 2) : pass.Price,
                SessionsIncludedCount = pass.SessionsIncluded ?? targetSessions.Count,
                SelectedSessionIdsJson = System.Text.Json.JsonSerializer.Serialize(sortedSessionIds),
                IdempotencyKey = idempotencyKey,
                Quantity = quantity,
                TotalPrice = finalAmount,
                PriceBreakdownJson = passBreakdownJson,
                GuestName = request.FullName,
                GuestPhone = request.Phone,
                GuestEmail = request.Email,
                Status = WorkshopBookingStatus.PendingPayment,
                ReservationExpiresAt = nowUtc.AddMinutes(15),
                BookedAt = nowUtc
            };

            for (int q = 0; q < quantity; q++)
            {
                foreach (var s in targetSessions)
                {
                    newBooking.BookingSessions.Add(new WorkshopBookingSession
                    {
                        Id = Guid.NewGuid(),
                        WorkshopBookingId = newBooking.Id,
                        WorkshopSessionId = s.Id,
                        Status = WorkshopBookingSessionStatus.Booked,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    });
                }
            }
        }
        else
        {
            var activePendingSeats = await _dbContext.WorkshopBookings
                .Where(b => b.WorkshopId == workshopId &&
                            b.Status == WorkshopBookingStatus.PendingPayment &&
                            b.ReservationExpiresAt.HasValue &&
                            b.ReservationExpiresAt.Value > nowUtc)
                .SumAsync(b => b.Quantity, cancellationToken);

            var confirmedSeats = await _dbContext.WorkshopBookings
                .Where(b => b.WorkshopId == workshopId &&
                           (b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended))
                .SumAsync(b => b.Quantity, cancellationToken);

            var effectiveBookedSeats = confirmedSeats + activePendingSeats;

            if (effectiveBookedSeats + quantity > workshop.Capacity)
            {
                throw new InvalidOperationException("Workshop is sold out or does not have enough remaining seats available.");
            }

            // Server-Authoritative quote calculation
            var quote = await _pricingService.CalculateQuoteAsync(
                workshopId,
                quantity,
                userId,
                cancellationToken);

            finalAmount = quote.TotalAmount;
            isSplitTier = quote.IsSplitTier;
            splitTierMessage = quote.SplitTierMessage;
            breakdown = quote.Breakdown;
            var breakdownJson = System.Text.Json.JsonSerializer.Serialize(quote.Breakdown);

            newBooking = new WorkshopBooking
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                StudentProfileId = studentProfile.Id,
                IdempotencyKey = idempotencyKey,
                Quantity = quantity,
                TotalPrice = finalAmount,
                PriceBreakdownJson = breakdownJson,
                GuestName = request.FullName,
                GuestPhone = request.Phone,
                GuestEmail = request.Email,
                Status = WorkshopBookingStatus.PendingPayment,
                ReservationExpiresAt = nowUtc.AddMinutes(15),
                BookedAt = nowUtc
            };
        }

        var transaction = new PaymentTransaction
        {
            Id = transactionId,
            UserId = userId ?? Guid.Empty,
            Purpose = PaymentPurpose.WorkshopBooking,
            ReferenceId = newBooking.Id,
            Amount = finalAmount,
            Currency = "INR",
            Status = PaymentStatus.OrderCreated,
            RazorpayOrderId = $"pending_{transactionId:N}",
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        newBooking.PaymentTransactionId = transaction.Id;

        _dbContext.WorkshopBookings.Add(newBooking);
        _dbContext.PaymentTransactions.Add(transaction);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await orderTx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("IX_workshop_bookings_IdempotencyKey", StringComparison.OrdinalIgnoreCase) == true ||
                                          ex.Message.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase) ||
                                          ex.Message.Contains("23505", StringComparison.OrdinalIgnoreCase))
        {
            await orderTx.RollbackAsync(cancellationToken);
            // Idempotency duplicate catch: return existing created order cleanly
            var existingByKey = await _dbContext.WorkshopBookings
                .FirstOrDefaultAsync(b => b.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existingByKey != null)
            {
                var existingTx = await _dbContext.PaymentTransactions
                    .FirstOrDefaultAsync(t => t.Id == existingByKey.PaymentTransactionId, cancellationToken);

                return new CreateWorkshopOrderResponse
                {
                    BookingId = existingByKey.Id,
                    TransactionId = existingTx?.Id ?? Guid.Empty,
                    WorkshopId = workshop.Id,
                    WorkshopTitle = workshop.Title,
                    Quantity = existingByKey.Quantity,
                    Amount = existingByKey.TotalPrice,
                    Currency = "INR",
                    RazorpayOrderId = existingTx?.RazorpayOrderId ?? "",
                    RazorpayKeyId = string.IsNullOrWhiteSpace(_razorpaySettings.KeyId) ? "rzp_test_placeholder" : _razorpaySettings.KeyId,
                    IsStudentDiscountApplied = studentProfile?.User?.CustomerCode != null && !studentProfile.User.CustomerCode.StartsWith("GST"),
                    IsSplitTier = isSplitTier,
                    SplitTierMessage = splitTierMessage,
                    Breakdown = breakdown
                };
            }
            throw;
        }

        // PHASE 2: External Razorpay API Order Creation (DECOUPLED OUTSIDE DB LOCK)
        string razorpayOrderId;
        try
        {
            razorpayOrderId = await CreateRazorpayOrderAsync(transactionId, finalAmount, PaymentPurpose.WorkshopBooking);
            transaction.RazorpayOrderId = razorpayOrderId;
            transaction.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WorkshopService] Failed to create Razorpay Order for transaction {transactionId}: {ex.Message}. Releasing pending reservation.");
            newBooking.Status = WorkshopBookingStatus.Cancelled;
            newBooking.CancelledAt = DateTime.UtcNow;
            newBooking.ReservationExpiresAt = DateTime.UtcNow;
            transaction.Status = PaymentStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"Payment gateway order creation failed: {ex.Message}");
        }

        return new CreateWorkshopOrderResponse
        {
            BookingId = newBooking.Id,
            TransactionId = transaction.Id,
            WorkshopId = workshop.Id,
            WorkshopTitle = workshop.Title,
            Quantity = quantity,
            Amount = finalAmount,
            Currency = "INR",
            RazorpayOrderId = razorpayOrderId,
            RazorpayKeyId = string.IsNullOrWhiteSpace(_razorpaySettings.KeyId) ? "rzp_test_placeholder" : _razorpaySettings.KeyId,
            IsStudentDiscountApplied = studentProfile?.User?.CustomerCode != null && !studentProfile.User.CustomerCode.StartsWith("GST"),
            IsSplitTier = isSplitTier,
            SplitTierMessage = splitTierMessage,
            Breakdown = breakdown
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
            .Include(b => b.WorkshopPassType)
            .Include(b => b.BookingSessions)
                .ThenInclude(bs => bs.WorkshopSession)
                    .ThenInclude(ws => ws.TrainerProfile)
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

        // Idempotency: If already Confirmed, return existing tickets and clean 200 without duplicating tickets
        if (booking.Status == WorkshopBookingStatus.Confirmed)
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
                Venue = booking.Workshop?.Venue ?? string.Empty,
                Quantity = booking.Quantity,
                Price = booking.Quantity > 0 ? booking.TotalPrice / booking.Quantity : booking.TotalPrice,
                TotalPrice = booking.TotalPrice,
                BookingReference = "BK-" + booking.Id.ToString()[..8].ToUpperInvariant(),
                CustomerName = booking.GuestName ?? studentProfile?.User?.FullName ?? "Ethos Guest",
                CustomerPhone = booking.GuestPhone ?? studentProfile?.User?.Phone ?? "",
                CustomerEmail = booking.GuestEmail ?? studentProfile?.User?.Email ?? "",
                Status = booking.Status,
                BookedAt = booking.BookedAt,
                WorkshopPassTypeId = booking.WorkshopPassTypeId,
                PassName = booking.WorkshopPassType?.Name,
                BookingSessions = booking.BookingSessions?.Select(bs => new WorkshopBookingSessionDto
                {
                    Id = bs.Id,
                    WorkshopSessionId = bs.WorkshopSessionId,
                    SessionTitle = bs.WorkshopSession?.Title ?? string.Empty,
                    SessionDate = bs.WorkshopSession?.SessionDate ?? DateTime.UtcNow,
                    StartTime = bs.WorkshopSession?.StartTime ?? TimeSpan.Zero,
                    EndTime = bs.WorkshopSession?.EndTime ?? TimeSpan.Zero,
                    TrainerName = bs.WorkshopSession?.TrainerProfile?.FullName ?? string.Empty,
                    Status = bs.Status,
                    OriginalSessionId = bs.OriginalSessionId,
                    ReplacedAt = bs.ReplacedAt
                }).ToList() ?? new List<WorkshopBookingSessionDto>(),
                Tickets = existingTickets.ToList()
            };
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
        bool isMockPayload = request.RazorpaySignature == "mock_sig" ||
                             request.RazorpaySignature == "mock_signature" ||
                             request.RazorpayPaymentId.Contains("mock", StringComparison.OrdinalIgnoreCase) ||
                             request.RazorpayOrderId.StartsWith("order_test_", StringComparison.OrdinalIgnoreCase);

        if (isMockPayload)
        {
            if (!_environment.IsDevelopment())
            {
                throw new InvalidOperationException("Mock payment verification is prohibited outside development environment.");
            }

            // In development only, allow test payment simulation
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

        return await _fulfillmentService.FulfillWorkshopPaymentAsync(
            transaction,
            request.RazorpayPaymentId,
            request.RazorpaySignature,
            source: "FrontendVerify",
            eventId: null,
            cancellationToken);
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
            .Include(b => b.WorkshopPassType)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.WorkshopSession)
                    .ThenInclude(ws => ws!.TrainerProfile)
            .Include(b => b.BookingSessions)
                .ThenInclude(bs => bs.WorkshopSession)
                    .ThenInclude(ws => ws.TrainerProfile)
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

            var passName = b.PassName ?? b.WorkshopPassType?.Name;
            var bookingRef = $"BK-{b.Id.ToString()[..8].ToUpperInvariant()}";

            var activeTickets = b.Tickets?
                .Where(t => t.Status != TicketStatus.Replaced &&
                            t.Status != TicketStatus.Cancelled &&
                            t.Status != TicketStatus.Refunded &&
                            t.Status != TicketStatus.Expired)
                .OrderBy(t => t.TicketNumber)
                .Select(t => new WorkshopTicketResponse
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber,
                    WorkshopBookingId = t.WorkshopBookingId,
                    WorkshopId = t.WorkshopId,
                    WorkshopTitle = b.Workshop?.Title ?? "Workshop Pass",
                    WorkshopDate = b.Workshop?.WorkshopDate ?? DateTime.UtcNow,
                    StartTime = b.Workshop?.StartTime ?? TimeSpan.Zero,
                    EndTime = b.Workshop?.EndTime ?? TimeSpan.Zero,
                    WorkshopSessionId = t.WorkshopSessionId,
                    SessionTitle = t.WorkshopSession?.Title,
                    SessionDate = t.WorkshopSession?.SessionDate,
                    SessionStartTime = t.WorkshopSession?.StartTime,
                    SessionEndTime = t.WorkshopSession?.EndTime,
                    SessionTrainerName = t.WorkshopSession?.TrainerProfile?.FullName,
                    PassName = passName,
                    PassCategory = b.WorkshopPassType?.GetPassCategory(),
                    Venue = b.Workshop?.Venue ?? "Ethos Dance Studio",
                    AttendeeName = t.AttendeeName,
                    AttendeePhone = t.AttendeePhone,
                    AttendeeEmail = t.AttendeeEmail,
                    IsPrimaryAttendee = t.IsPrimaryAttendee,
                    Status = t.Status,
                    IssuedAt = t.IssuedAt,
                    CheckedInAt = t.CheckedInAt,
                    AttendeeDetailsLockedAt = t.AttendeeDetailsLockedAt
                }).ToList() ?? new List<WorkshopTicketResponse>();

            result.Add(new WorkshopBookingResponse
            {
                Id = b.Id,
                WorkshopId = b.WorkshopId,
                WorkshopTitle = b.Workshop.Title,
                WorkshopDate = b.Workshop.WorkshopDate,
                StartTime = b.Workshop.StartTime,
                EndTime = b.Workshop.EndTime,
                Venue = b.Workshop.Venue,
                Quantity = b.Quantity,
                Price = price,
                TotalPrice = b.TotalPrice,
                BookingReference = bookingRef,
                CustomerName = b.GuestName,
                CustomerPhone = b.GuestPhone,
                CustomerEmail = b.GuestEmail,
                Status = b.Status,
                BookedAt = b.BookedAt,
                WorkshopPassTypeId = b.WorkshopPassTypeId,
                PassName = passName,
                Tickets = activeTickets,
                BookingSessions = b.BookingSessions?.Select(bs => new WorkshopBookingSessionDto
                {
                    Id = bs.Id,
                    WorkshopSessionId = bs.WorkshopSessionId,
                    SessionTitle = bs.WorkshopSession?.Title ?? string.Empty,
                    SessionDate = bs.WorkshopSession?.SessionDate ?? DateTime.UtcNow,
                    StartTime = bs.WorkshopSession?.StartTime ?? TimeSpan.Zero,
                    EndTime = bs.WorkshopSession?.EndTime ?? TimeSpan.Zero,
                    TrainerName = bs.WorkshopSession?.TrainerProfile?.FullName ?? string.Empty,
                    Status = bs.Status,
                    OriginalSessionId = bs.OriginalSessionId,
                    ReplacedAt = bs.ReplacedAt
                }).ToList() ?? new List<WorkshopBookingSessionDto>()
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
                Rating = f.Rating ?? 0,
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
            Rating = feedback.Rating ?? 0,
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
            Rating = feedback.Rating ?? 0,
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
        // GATED: Only allow mock test orders in development environment when placeholder credentials are used
        if (_environment.IsDevelopment() && (
            string.IsNullOrWhiteSpace(_razorpaySettings.KeyId) ||
            _razorpaySettings.KeyId.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
            _razorpaySettings.KeyId.Contains("dummy", StringComparison.OrdinalIgnoreCase)))
        {
            return $"order_test_{Guid.NewGuid():N}"[..20];
        }

        if (string.IsNullOrWhiteSpace(_razorpaySettings.KeyId) || string.IsNullOrWhiteSpace(_razorpaySettings.KeySecret))
        {
            throw new InvalidOperationException("Razorpay payment gateway credentials are not configured.");
        }

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

        try
        {
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
                // Fallback to test order ID ONLY in development mode if test credentials are not yet authorized
                if (_environment.IsDevelopment() && (
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                    responseBody.Contains("BAD_REQUEST_ERROR", StringComparison.OrdinalIgnoreCase)))
                {
                    return $"order_test_{Guid.NewGuid():N}"[..20];
                }

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
        catch (Exception ex)
        {
            if (ex is InvalidOperationException) throw;

            // In development only, if network error occurs, fallback
            if (_environment.IsDevelopment())
            {
                return $"order_test_{Guid.NewGuid():N}"[..20];
            }

            throw new InvalidOperationException($"Razorpay gateway communication error: {ex.Message}", ex);
        }
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
