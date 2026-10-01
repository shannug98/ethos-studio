using System.Text.Json;
using Ethos.Api.Application.Feedback;
using Ethos.Api.Application.Storage;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Feedback;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Domain.Exceptions;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Ethos.Api.Application.Admin;

public class AdminWorkshopService : IAdminWorkshopService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;
    private readonly IWorkshopPricingService _pricingService;
    private readonly ICloudflareR2StorageService? _r2Storage;
    private readonly IWorkshopTicketService? _ticketService;
    private readonly IConfiguration? _configuration;

    public AdminWorkshopService(
        AppDbContext db,
        IAdminAuditService auditService,
        IWorkshopPricingService? pricingService = null,
        ICloudflareR2StorageService? r2Storage = null,
        IWorkshopTicketService? ticketService = null,
        IConfiguration? configuration = null)
    {
        _db = db;
        _auditService = auditService;
        _pricingService = pricingService ?? new WorkshopPricingService(db, new AdminWorkshopFallbackStudentEligibilityService());
        _r2Storage = r2Storage;
        _ticketService = ticketService;
        _configuration = configuration;
    }

    private static string? ExtractExactR2Key(string? mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl)) return null;
        if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri))
        {
            var key = uri.AbsolutePath.TrimStart('/');
            return string.IsNullOrEmpty(key) ? null : key;
        }
        return mediaUrl.TrimStart('/');
    }

    private static string? ValidateAndNormalizeLocationUrl(string? locationUrl)
    {
        if (string.IsNullOrWhiteSpace(locationUrl))
            return null;

        var trimmed = locationUrl.Trim();
        if (trimmed.Length > 1000)
            throw new ArgumentException("Location link cannot exceed 1000 characters.");

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Location link must be a valid secure URL starting with https://");
        }

        return trimmed;
    }

    private static void ValidateStatusTransition(WorkshopStatus current, WorkshopStatus target)
    {
        if (current == target) return;

        if (current == WorkshopStatus.Completed)
            throw new InvalidOperationException($"Invalid status transition from Completed to {target}. Completed is a terminal state.");

        if (current == WorkshopStatus.Cancelled)
            throw new InvalidOperationException($"Invalid status transition from Cancelled to {target}. Cancelled is a terminal state.");

        bool valid = (current, target) switch
        {
            (WorkshopStatus.Draft, WorkshopStatus.Published) => true,
            (WorkshopStatus.Draft, WorkshopStatus.PendingApproval) => true,

            (WorkshopStatus.PendingApproval, WorkshopStatus.Approved) => true,
            (WorkshopStatus.PendingApproval, WorkshopStatus.Rejected) => true,

            (WorkshopStatus.Approved, WorkshopStatus.Published) => true,
            (WorkshopStatus.Approved, WorkshopStatus.Unpublished) => true,
            (WorkshopStatus.Approved, WorkshopStatus.Rejected) => true,
            (WorkshopStatus.Approved, WorkshopStatus.Completed) => true,

            (WorkshopStatus.Rejected, WorkshopStatus.Draft) => true,
            (WorkshopStatus.Rejected, WorkshopStatus.PendingApproval) => true,

            (WorkshopStatus.Published, WorkshopStatus.Unpublished) => true,
            (WorkshopStatus.Published, WorkshopStatus.Completed) => true,
            (WorkshopStatus.Published, WorkshopStatus.Archived) => true,
            (WorkshopStatus.Published, WorkshopStatus.Cancelled) => true,

            (WorkshopStatus.Unpublished, WorkshopStatus.Published) => true,
            (WorkshopStatus.Unpublished, WorkshopStatus.Archived) => true,
            (WorkshopStatus.Unpublished, WorkshopStatus.Cancelled) => true,

            (WorkshopStatus.Archived, WorkshopStatus.Published) => true,
            (WorkshopStatus.Archived, WorkshopStatus.Unpublished) => true,

            _ => false
        };

        if (!valid)
        {
            throw new InvalidOperationException($"Invalid status transition from {current} to {target}.");
        }
    }

    private async Task DeleteR2MediaExactKeyAsync(string? mediaUrl, CancellationToken cancellationToken)
    {
        if (_r2Storage == null || string.IsNullOrWhiteSpace(mediaUrl)) return;
        try
        {
            if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri))
            {
                var key = uri.AbsolutePath.TrimStart('/');
                if (!string.IsNullOrEmpty(key))
                {
                    await _r2Storage.DeleteAsync(key, cancellationToken);
                }
            }
        }
        catch
        {
            // Exact-key ephemeral deletion failures do not abort database updates
        }
    }

    private class AdminWorkshopFallbackStudentEligibilityService : IStudentEligibilityService
    {
        public Task<StudentEligibilityResult> CheckEligibilityByPhoneAsync(string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StudentEligibilityResult(false, false, false));

        public Task<bool> IsStudentPortalEligibleAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    public async Task<IReadOnlyList<TrainerWorkshopResponse>> GetPendingWorkshopsAsync(
        CancellationToken cancellationToken)
    {
        return await _db.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
            .Include(w => w.Bookings)
            .Where(w => w.Status == WorkshopStatus.PendingApproval)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => Map(w))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<TrainerWorkshopResponse>> GetWorkshopsAsync(
        int page,
        int pageSize,
        string? phase,
        WorkshopStatus? status,
        Guid? trainerId,
        DateTime? startDate,
        DateTime? endDate,
        string? search,
        string? city,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
            .Include(w => w.Bookings)
            .AsQueryable();

        var nowUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(phase) && !string.Equals(phase, "All", StringComparison.OrdinalIgnoreCase))
        {
            var p = phase.Trim().ToLowerInvariant();
            if (p == "upcoming" || p == "ongoing" || p == "ended" || p == "endedpendingcompletion")
            {
                query = query.Where(w => w.Status == WorkshopStatus.Published);

                if (!string.IsNullOrWhiteSpace(city))
                {
                    var c = city.Trim().ToLower();
                    query = query.Where(w => w.City != null && w.City.ToLower() == c);
                }

                if (status.HasValue)
                    query = query.Where(w => w.Status == status.Value);

                if (trainerId.HasValue)
                    query = query.Where(w => w.TrainerProfileId == trainerId.Value);

                if (startDate.HasValue)
                    query = query.Where(w => w.WorkshopDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(w => w.WorkshopDate <= endDate.Value);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(w =>
                        w.Title.ToLower().Contains(s) ||
                        (w.Description != null && w.Description.ToLower().Contains(s)) ||
                        w.DanceStyle.ToLower().Contains(s) ||
                        w.Venue.ToLower().Contains(s));
                }

                var publishedWorkshops = await query
                    .OrderByDescending(w => w.WorkshopDate)
                    .ToListAsync(cancellationToken);

                var matching = publishedWorkshops.Where(w =>
                {
                    var sUtc = w.StartUtc ?? ComputeStartUtc(w);
                    var eUtc = w.EndUtc ?? ComputeEndUtc(w);

                    if (p == "upcoming") return nowUtc < sUtc;
                    if (p == "ongoing") return nowUtc >= sUtc && nowUtc < eUtc;
                    return nowUtc >= eUtc; // ended or endedpendingcompletion
                }).ToList();

                var totalCountPublished = matching.Count;
                var pageItems = matching
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(w => Map(w))
                    .ToList();

                return new PagedResult<TrainerWorkshopResponse>
                {
                    Items = pageItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCountPublished
                };
            }
            else if (p == "pendingreview" || p == "pending_review" || p == "pending")
            {
                query = query.Where(w => w.Status == WorkshopStatus.PendingApproval);
            }
            else if (p == "draft")
            {
                query = query.Where(w => w.Status == WorkshopStatus.Draft);
            }
            else if (p == "rejected")
            {
                query = query.Where(w => w.Status == WorkshopStatus.Rejected);
            }
            else if (p == "completed" || p == "ended")
            {
                if (!string.IsNullOrWhiteSpace(city))
                {
                    var c = city.Trim().ToLower();
                    query = query.Where(w => w.City != null && w.City.ToLower() == c);
                }

                if (trainerId.HasValue)
                    query = query.Where(w => w.TrainerProfileId == trainerId.Value);

                if (startDate.HasValue)
                    query = query.Where(w => w.WorkshopDate >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(w => w.WorkshopDate <= endDate.Value);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(w =>
                        w.Title.ToLower().Contains(s) ||
                        (w.Description != null && w.Description.ToLower().Contains(s)) ||
                        w.DanceStyle.ToLower().Contains(s) ||
                        w.Venue.ToLower().Contains(s));
                }

                var candidateWorkshops = await query
                    .Where(w => w.Status == WorkshopStatus.Completed || w.Status == WorkshopStatus.Published)
                    .OrderByDescending(w => w.WorkshopDate)
                    .ToListAsync(cancellationToken);

                var matching = candidateWorkshops.Where(w =>
                {
                    if (w.Status == WorkshopStatus.Completed) return true;
                    var eUtc = w.EndUtc ?? ComputeEndUtc(w);
                    return nowUtc >= eUtc;
                }).ToList();

                var totalCountMatching = matching.Count;
                var pageItems = matching
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(w => Map(w))
                    .ToList();

                return new PagedResult<TrainerWorkshopResponse>
                {
                    Items = pageItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCountMatching
                };
            }
            else if (p == "cancelled")
            {
                query = query.Where(w => w.Status == WorkshopStatus.Cancelled);
            }
            else if (p == "unpublished")
            {
                query = query.Where(w => w.Status == WorkshopStatus.Unpublished);
            }
            else if (p == "archived")
            {
                query = query.Where(w => w.Status == WorkshopStatus.Archived);
            }
            else if (p == "approved")
            {
                query = query.Where(w => w.Status == WorkshopStatus.Approved);
            }
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var c = city.Trim().ToLower();
            query = query.Where(w => w.City != null && w.City.ToLower() == c);
        }

        if (status.HasValue)
            query = query.Where(w => w.Status == status.Value);

        if (trainerId.HasValue)
            query = query.Where(w => w.TrainerProfileId == trainerId.Value);

        if (startDate.HasValue)
            query = query.Where(w => w.WorkshopDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(w => w.WorkshopDate <= endDate.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(w =>
                w.Title.ToLower().Contains(s) ||
                (w.Description != null && w.Description.ToLower().Contains(s)) ||
                w.DanceStyle.ToLower().Contains(s) ||
                w.Venue.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var workshops = await query
            .OrderByDescending(w => w.WorkshopDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = workshops.Select(w => Map(w)).ToList();

        return new PagedResult<TrainerWorkshopResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    
    public async Task<AdminWorkshopCountsDto> GetWorkshopCountsAsync(Guid? adminUserId, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var workshops = await _db.Workshops
            .AsNoTracking()
            .Select(w => new
            {
                w.Status,
                w.StartUtc,
                w.EndUtc,
                w.WorkshopDate,
                w.StartTime,
                w.EndTime,
                w.Timezone
            })
            .ToListAsync(cancellationToken);

        int standaloneDraftsCount = 0;
        if (adminUserId.HasValue)
        {
            standaloneDraftsCount = await _db.WorkshopDrafts
                .AsNoTracking()
                .CountAsync(d => d.WorkshopId == null && d.AdminUserId == adminUserId.Value, cancellationToken);
        }
        else
        {
            standaloneDraftsCount = await _db.WorkshopDrafts
                .AsNoTracking()
                .CountAsync(d => d.WorkshopId == null, cancellationToken);
        }

        int draft = standaloneDraftsCount;
        int pendingReview = 0;
        int rejected = 0;
        int upcoming = 0;
        int ongoing = 0;
        int ended = 0;
        int completed = 0;
        int cancelled = 0;
        int unpublished = 0;
        int archived = 0;

        foreach (var w in workshops)
        {
            switch (w.Status)
            {
                case WorkshopStatus.Draft:
                    draft++;
                    break;
                case WorkshopStatus.PendingApproval:
                    pendingReview++;
                    break;
                case WorkshopStatus.Rejected:
                    rejected++;
                    break;
                case WorkshopStatus.Cancelled:
                    cancelled++;
                    break;
                case WorkshopStatus.Completed:
                    completed++;
                    break;
                case WorkshopStatus.Unpublished:
                    unpublished++;
                    break;
                case WorkshopStatus.Archived:
                    archived++;
                    break;
                case WorkshopStatus.Published:
                    var startUtc = w.StartUtc ?? ComputeStartUtc(w.WorkshopDate, w.StartTime, w.Timezone);
                    var endUtc = w.EndUtc ?? ComputeEndUtc(w.WorkshopDate, w.StartTime, w.EndTime, w.Timezone);

                    if (nowUtc < startUtc) upcoming++;
                    else if (nowUtc >= startUtc && nowUtc < endUtc) ongoing++;
                    else ended++;
                    break;
                default:
                    break;
            }
        }

        return new AdminWorkshopCountsDto
        {
            Draft = draft,
            PendingReview = pendingReview,
            Rejected = rejected,
            Upcoming = upcoming,
            Ongoing = ongoing,
            Ended = ended,
            Completed = completed,
            Cancelled = cancelled,
            Unpublished = unpublished,
            Archived = archived,
            All = workshops.Count
        };
    }

    public async Task<TrainerWorkshopResponse?> GetWorkshopByIdAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var w = await _db.Workshops
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
            .Include(w => w.Sessions)
                .ThenInclude(s => s.BookingSessions)
                    .ThenInclude(bs => bs.WorkshopBooking)
            .Include(w => w.PassTypes)
                .ThenInclude(p => p.PricingTiers)
            .Include(w => w.Bookings)
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        return w == null ? null : Map(w);
    }

    public async Task<TrainerWorkshopResponse> CreateWorkshopAsync(
        Guid adminUserId,
        AdminCreateWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Workshop title is required.");

        if (string.IsNullOrWhiteSpace(request.DanceStyle))
            throw new ArgumentException("Dance style is required.");

        if (string.IsNullOrWhiteSpace(request.Level))
            throw new ArgumentException("Workshop difficulty level is required.");

        if (string.IsNullOrWhiteSpace(request.Venue))
            throw new ArgumentException("Workshop venue or studio room is required.");

        if (request.Sessions == null || request.Sessions.Count == 0)
        {
            if (request.EndTime <= request.StartTime)
                throw new ArgumentException("Workshop end time must be after start time.");
        }

        if (request.Capacity <= 0)
            throw new ArgumentException("Workshop capacity must be greater than zero.");

        if (request.Price < 0)
            throw new ArgumentException("Workshop price cannot be negative.");

        if (request.BookingCutoffTime.HasValue)
        {
            if (request.BookingCutoffTime.Value < TimeSpan.Zero || request.BookingCutoffTime.Value >= TimeSpan.FromDays(1))
                throw new ArgumentException("Booking cutoff time must be a valid time of day.");
        }

        if (request.TrainerProfileId.HasValue && request.TrainerProfileId.Value != Guid.Empty)
        {
            var trainerExists = await _db.TrainerProfiles
                .AnyAsync(t => t.Id == request.TrainerProfileId.Value, cancellationToken);
            if (!trainerExists)
                throw new ArgumentException("Assigned trainer profile was not found.");
        }

        var now = DateTime.UtcNow;
        var initialStatus = request.Status ?? WorkshopStatus.Approved;

        if (initialStatus == WorkshopStatus.Published)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Workshop description is required to publish.");
            if (string.IsNullOrWhiteSpace(request.ImageUrl))
                throw new ArgumentException("Workshop main portrait image is required to publish.");
            if (request.Price <= 0)
                throw new ArgumentException("Workshop must have a valid price before publishing.");
            if (request.Sessions == null || request.Sessions.Count == 0)
                throw new ArgumentException("Workshop must have at least one session to publish.");
            var hasTrainer = (request.TrainerProfileId.HasValue && request.TrainerProfileId.Value != Guid.Empty) ||
                             (request.TrainerProfileIds != null && request.TrainerProfileIds.Any(id => id != Guid.Empty));
            if (!hasTrainer)
                throw new ArgumentException("Workshop must have at least one trainer in its faculty pool to publish.");
        }

        var tzId = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Kolkata" : request.Timezone.Trim();
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

        var dateUnspecified = DateTime.SpecifyKind(request.WorkshopDate.Date, DateTimeKind.Unspecified);
        var startLocal = dateUnspecified + request.StartTime;
        var endLocal = request.EndTime > request.StartTime
            ? dateUnspecified + request.EndTime
            : dateUnspecified.AddDays(1) + request.EndTime;

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = request.TrainerProfileId == Guid.Empty ? null : request.TrainerProfileId,
            IsEthosOriginal = request.IsEthosOriginal,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            DanceStyle = request.DanceStyle.Trim(),
            Level = request.Level.Trim(),
            WorkshopDate = DateTime.SpecifyKind(request.WorkshopDate.Date, DateTimeKind.Utc),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            BookingCutoffTime = request.BookingCutoffTime,
            Venue = request.Venue.Trim(),
            Price = request.Price,
            AdminApprovedPrice = request.Price,
            PriceApprovedAt = now,
            PriceApprovedByUserId = adminUserId,
            Capacity = request.Sessions != null && request.Sessions.Count > 0
                ? Math.Max(request.Capacity, request.Sessions.Max(s => s.Capacity))
                : request.Capacity,
            Status = initialStatus,
            ImageUrl = request.ImageUrl?.Trim(),
            LandscapeImageUrl = request.LandscapeImageUrl?.Trim(),
            City = request.City?.Trim(),
            Area = request.Area?.Trim(),
            ShortDescription = request.ShortDescription?.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
            ContactNumber = request.ContactNumber?.Trim(),
            PublicVisibility = request.PublicVisibility ?? true,
            RegistrationType = string.IsNullOrWhiteSpace(request.RegistrationType) ? "Standard" : request.RegistrationType.Trim(),
            TermsAndCancellationPolicy = request.TermsAndCancellationPolicy?.Trim(),
            GooglePlaceId = request.GooglePlaceId?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            VenueAddress = request.VenueAddress?.Trim(),
            LocationUrl = ValidateAndNormalizeLocationUrl(request.LocationUrl),
            Timezone = tzId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            AllowReEntry = request.AllowReEntry,
            RequireReEntryVerification = request.RequireReEntryVerification,
            ReEntryCooldown = request.ReEntryCooldown,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Workshop Trainers
        if (request.TrainerProfileIds != null && request.TrainerProfileIds.Count > 0)
        {
            var distinctTrainerIds = request.TrainerProfileIds.Where(id => id != Guid.Empty).Distinct().ToList();
            var existingTrainers = await _db.TrainerProfiles
                .Where(t => distinctTrainerIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, cancellationToken);

            if (existingTrainers.Count != distinctTrainerIds.Count)
            {
                throw new ArgumentException("One or more assigned trainer profiles were not found.");
            }

            if (!workshop.TrainerProfileId.HasValue || workshop.TrainerProfileId == Guid.Empty)
            {
                workshop.TrainerProfileId = distinctTrainerIds[0];
            }

            int order = 0;
            foreach (var tid in distinctTrainerIds)
            {
                workshop.WorkshopTrainers.Add(new WorkshopTrainer
                {
                    Id = Guid.NewGuid(),
                    WorkshopId = workshop.Id,
                    TrainerProfileId = tid,
                    DisplayOrder = order++,
                    AssignedAt = now
                });
            }
        }
        else if (request.TrainerProfileId.HasValue && request.TrainerProfileId != Guid.Empty)
        {
            workshop.WorkshopTrainers.Add(new WorkshopTrainer
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshop.Id,
                TrainerProfileId = request.TrainerProfileId.Value,
                DisplayOrder = 0,
                AssignedAt = now
            });
        }

        // Workshop Sessions
        if (request.Sessions != null && request.Sessions.Count > 0)
        {
            var workshopFacultyIds = workshop.WorkshopTrainers.Select(wt => wt.TrainerProfileId).ToHashSet();
            if (workshop.TrainerProfileId.HasValue && workshop.TrainerProfileId.Value != Guid.Empty)
            {
                workshopFacultyIds.Add(workshop.TrainerProfileId.Value);
            }

            var sessionTrainerMap = new Dictionary<int, List<Guid>>();
            for (int i = 0; i < request.Sessions.Count; i++)
            {
                var s = request.Sessions[i];
                if (string.IsNullOrWhiteSpace(s.Title))
                    throw new ArgumentException("Session title is required.");
                if (s.EndTime <= s.StartTime)
                    throw new ArgumentException($"Session '{s.Title}' end time must be after start time.");
                if (s.Capacity <= 0)
                    throw new ArgumentException($"Session '{s.Title}' capacity must be greater than zero.");

                var tIds = new List<Guid>();
                if (s.TrainerProfileIds != null && s.TrainerProfileIds.Count > 0)
                {
                    tIds.AddRange(s.TrainerProfileIds.Where(id => id != Guid.Empty));
                }
                else if (s.TrainerProfileId.HasValue && s.TrainerProfileId.Value != Guid.Empty)
                {
                    tIds.Add(s.TrainerProfileId.Value);
                }
                else if (workshop.TrainerProfileId.HasValue && workshop.TrainerProfileId.Value != Guid.Empty)
                {
                    tIds.Add(workshop.TrainerProfileId.Value);
                }
                tIds = tIds.Distinct().ToList();

                // Faculty Invariant: all session trainers must belong to workshop faculty
                var invalidTrainerId = tIds.FirstOrDefault(id => !workshopFacultyIds.Contains(id));
                if (invalidTrainerId != Guid.Empty)
                {
                    throw new ArgumentException($"Session '{s.Title}' assigns trainer '{invalidTrainerId}' who is not part of the workshop faculty pool. Add them to the workshop faculty first.");
                }

                sessionTrainerMap[i] = tIds;
            }

            // Multi-trainer schedule overlap validation with boundary checks
            for (int i = 0; i < request.Sessions.Count; i++)
            {
                var s1 = request.Sessions[i];
                var t1List = sessionTrainerMap[i];

                for (int j = i + 1; j < request.Sessions.Count; j++)
                {
                    var s2 = request.Sessions[j];
                    var t2List = sessionTrainerMap[j];

                    if (s1.SessionDate.Date == s2.SessionDate.Date)
                    {
                        var sharedTrainers = t1List.Intersect(t2List).ToList();
                        if (sharedTrainers.Count > 0)
                        {
                            // Half-open interval overlap: touching boundaries (s1.EndTime == s2.StartTime) do not overlap
                            if (s1.StartTime < s2.EndTime && s2.StartTime < s1.EndTime)
                            {
                                throw new ArgumentException($"Trainer has overlapping sessions on {s1.SessionDate:yyyy-MM-dd}: '{s1.Title}' ({s1.StartTime:hh\\:mm}-{s1.EndTime:hh\\:mm}) and '{s2.Title}' ({s2.StartTime:hh\\:mm}-{s2.EndTime:hh\\:mm}).");
                            }
                        }
                    }
                }
            }

            int sOrder = 0;
            for (int i = 0; i < request.Sessions.Count; i++)
            {
                var s = request.Sessions[i];
                var tIds = sessionTrainerMap[i];
                var leadTrainerId = tIds.Count > 0 ? tIds[0] : (workshop.TrainerProfileId ?? Guid.Empty);

                var sessionEntity = new WorkshopSession
                {
                    Id = s.Id.HasValue && s.Id.Value != Guid.Empty ? s.Id.Value : Guid.NewGuid(),
                    WorkshopId = workshop.Id,
                    SessionDate = DateTime.SpecifyKind(s.SessionDate.Date, DateTimeKind.Utc),
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    TrainerProfileId = leadTrainerId,
                    Title = s.Title.Trim(),
                    Description = s.Description?.Trim(),
                    Capacity = s.Capacity,
                    BookingCutoffTime = s.BookingCutoffTime,
                    PosterImageUrl = s.PosterImageUrl?.Trim(),
                    DisplayOrder = s.DisplayOrder > 0 ? s.DisplayOrder : sOrder++,
                    IsActive = s.IsActive,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                int stOrder = 0;
                foreach (var tid in tIds)
                {
                    sessionEntity.SessionTrainers.Add(new WorkshopSessionTrainer
                    {
                        Id = Guid.NewGuid(),
                        WorkshopSessionId = sessionEntity.Id,
                        TrainerProfileId = tid,
                        DisplayOrder = stOrder++,
                        AssignedAt = now
                    });
                }

                workshop.Sessions.Add(sessionEntity);
            }

            var earliestSession = workshop.Sessions.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime).First();
            var latestSession = workshop.Sessions.OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.EndTime).First();
            workshop.WorkshopDate = earliestSession.SessionDate;
            workshop.StartTime = earliestSession.StartTime;
            workshop.EndTime = latestSession.EndTime;
            var date1Unspecified = DateTime.SpecifyKind(earliestSession.SessionDate.Date, DateTimeKind.Unspecified);
            var date2Unspecified = DateTime.SpecifyKind(latestSession.SessionDate.Date, DateTimeKind.Unspecified);
            var startLocalSession = date1Unspecified + earliestSession.StartTime;
            var endLocalSession = date2Unspecified + latestSession.EndTime;
            workshop.StartUtc = TimeZoneInfo.ConvertTimeToUtc(startLocalSession, tz);
            workshop.EndUtc = TimeZoneInfo.ConvertTimeToUtc(endLocalSession, tz);
        }

        // Workshop Ticket Types (PassTypes)
        if (request.PassTypes != null && request.PassTypes.Count > 0)
        {
            int pOrder = 0;
            foreach (var p in request.PassTypes)
            {
                if (string.IsNullOrWhiteSpace(p.Name))
                    throw new ArgumentException("Ticket type name is required.");
                if (p.Price < 0)
                    throw new ArgumentException("Ticket type price cannot be negative.");
                if (p.SalesStartUtc.HasValue && p.SalesEndUtc.HasValue && p.SalesStartUtc.Value >= p.SalesEndUtc.Value)
                    throw new ArgumentException($"Sales start time must be before sales end time for ticket '{p.Name}'.");

                // Pass Category Classification Matrix Validation
                if (p.WorkshopSessionId.HasValue)
                {
                    if (p.SessionsIncluded != 1)
                    {
                        throw new ArgumentException($"Single-session ticket '{p.Name}' must have SessionsIncluded equal to 1.");
                    }
                    bool sessionExists = workshop.Sessions.Any(s => s.Id == p.WorkshopSessionId.Value);
                    if (!sessionExists)
                    {
                        throw new ArgumentException($"Session referenced by ticket '{p.Name}' was not found in the workshop sessions.");
                    }
                }
                else
                {
                    if (p.SessionsIncluded.HasValue)
                    {
                        if (p.SessionsIncluded.Value < 1)
                        {
                            throw new ArgumentException($"Ticket '{p.Name}' must have SessionsIncluded greater than or equal to 1.");
                        }
                        int sessionCount = request.Sessions?.Count ?? workshop.Sessions.Count;
                        if (sessionCount > 0 && p.SessionsIncluded.Value > sessionCount)
                        {
                            throw new ArgumentException($"Ticket '{p.Name}' cannot include more sessions ({p.SessionsIncluded.Value}) than total workshop sessions ({sessionCount}).");
                        }
                    }
                }

                var totalQty = p.TotalQuantity > 0 ? p.TotalQuantity : 1000;
                var passId = p.Id.HasValue && p.Id.Value != Guid.Empty ? p.Id.Value : Guid.NewGuid();

                var passType = new WorkshopPassType
                {
                    Id = passId,
                    WorkshopId = workshop.Id,
                    WorkshopSessionId = p.WorkshopSessionId,
                    Name = p.Name.Trim(),
                    Description = p.Description?.Trim(),
                    Price = p.Price,
                    SessionsIncluded = p.SessionsIncluded,
                    TotalQuantity = totalQty,
                    SalesStartUtc = p.SalesStartUtc.HasValue ? DateTime.SpecifyKind(p.SalesStartUtc.Value, DateTimeKind.Utc) : null,
                    SalesEndUtc = p.SalesEndUtc.HasValue ? DateTime.SpecifyKind(p.SalesEndUtc.Value, DateTimeKind.Utc) : null,
                    DisplayOrder = p.DisplayOrder > 0 ? p.DisplayOrder : pOrder++,
                    IsActive = p.IsActive,
                    CreatedAt = now
                };

                if (p.PricingTiers != null && p.PricingTiers.Count > 0)
                {
                    _pricingService.ValidateTicketTypePricingTiers(passType.TotalQuantity, p.PricingTiers);

                    foreach (var pt in p.PricingTiers.OrderBy(t => t.TierNumber))
                    {
                        passType.PricingTiers.Add(new WorkshopPricingTier
                        {
                            Id = Guid.NewGuid(),
                            WorkshopId = workshop.Id,
                            WorkshopPassTypeId = passType.Id,
                            TierNumber = pt.TierNumber,
                            TierName = string.IsNullOrWhiteSpace(pt.TierName) ? $"Tier {pt.TierNumber}" : pt.TierName.Trim(),
                            MinTickets = pt.MinTickets,
                            MaxTickets = pt.MaxTickets,
                            Price = pt.Price,
                            CreatedAt = now,
                            UpdatedAt = now
                        });
                    }
                }

                workshop.PassTypes.Add(passType);
            }
        }

        _db.Workshops.Add(workshop);

        // Save custom pricing tiers if provided by admin (legacy workshops without PassTypes only)
        if ((request.PassTypes == null || request.PassTypes.Count == 0) &&
            request.PricingTiers != null && request.PricingTiers.Count > 0)
        {
            int tierNum = 1;
            foreach (var item in request.PricingTiers.OrderBy(t => t.TierNumber))
            {
                var tierEntity = new WorkshopPricingTier
                {
                    Id = Guid.NewGuid(),
                    WorkshopId = workshop.Id,
                    WorkshopPassTypeId = null,
                    TierNumber = tierNum++,
                    TierName = string.IsNullOrWhiteSpace(item.TierName) ? $"Tier {tierNum}" : item.TierName.Trim(),
                    MinTickets = item.MinTickets > 0 ? item.MinTickets : 1,
                    MaxTickets = item.MaxTickets,
                    Price = item.Price,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.WorkshopPricingTiers.Add(tierEntity);
            }
        }

        _auditService.AddAuditLog(
            adminUserId,
            "ADMIN_WORKSHOP_CREATED",
            "Workshop",
            workshop.Id,
            $"Administrator created workshop '{workshop.Title}' with status {workshop.Status} and price {workshop.Price} INR");

        await _db.SaveChangesAsync(cancellationToken);

        if (workshop.TrainerProfileId.HasValue)
        {
            await _db.Entry(workshop)
                .Reference(w => w.TrainerProfile)
                .LoadAsync(cancellationToken);
        }

        await _db.Entry(workshop)
            .Collection(w => w.WorkshopTrainers)
            .Query()
            .Include(wt => wt.TrainerProfile)
            .LoadAsync(cancellationToken);

        await _db.Entry(workshop)
            .Collection(w => w.Sessions)
            .Query()
            .Include(s => s.TrainerProfile)
            .LoadAsync(cancellationToken);

        await _db.Entry(workshop)
            .Collection(w => w.PassTypes)
            .Query()
            .Include(p => p.PricingTiers)
            .LoadAsync(cancellationToken);

        return Map(workshop);
    }

    public async Task<TrainerWorkshopResponse> UpdateWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminUpdateWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        using var tx = _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM workshops WHERE \"Id\" = {workshopId} FOR UPDATE",
                cancellationToken);
        }

        var workshop = await _db.Workshops
            .Include(w => w.TrainerProfile)
            .Include(w => w.WorkshopTrainers)
            .Include(w => w.Sessions)
                .ThenInclude(s => s.SessionTrainers)
            .Include(w => w.PassTypes)
                .ThenInclude(p => p.PricingTiers)
            .Include(w => w.Bookings)
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        EnsureWorkshopEditable(workshop);

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Workshop title is required.");

        if (string.IsNullOrWhiteSpace(request.DanceStyle))
            throw new ArgumentException("Dance style is required.");

        if (string.IsNullOrWhiteSpace(request.Level))
            throw new ArgumentException("Workshop difficulty level is required.");

        if (string.IsNullOrWhiteSpace(request.Venue))
            throw new ArgumentException("Workshop venue or studio room is required.");

        if (request.Sessions == null || request.Sessions.Count == 0)
        {
            if (request.EndTime <= request.StartTime)
                throw new ArgumentException("Workshop end time must be after start time.");
        }

        if (request.Capacity <= 0)
            throw new ArgumentException("Workshop capacity must be greater than zero.");

        if (request.Price < 0)
            throw new ArgumentException("Workshop price cannot be negative.");

        if (request.BookingCutoffTime.HasValue)
        {
            if (request.BookingCutoffTime.Value < TimeSpan.Zero || request.BookingCutoffTime.Value >= TimeSpan.FromDays(1))
                throw new ArgumentException("Booking cutoff time must be a valid time of day.");
        }

        if (request.TrainerProfileId.HasValue && request.TrainerProfileId.Value != Guid.Empty)
        {
            var trainerExists = await _db.TrainerProfiles
                .AnyAsync(t => t.Id == request.TrainerProfileId.Value, cancellationToken);
            if (!trainerExists)
                throw new ArgumentException("Assigned trainer profile was not found.");
        }

        var now = DateTime.UtcNow;

        var updateTzId = string.IsNullOrWhiteSpace(request.Timezone) ? (workshop.Timezone ?? "Asia/Kolkata") : request.Timezone.Trim();
        TimeZoneInfo updateTz;
        try { updateTz = TimeZoneInfo.FindSystemTimeZoneById(updateTzId); }
        catch { updateTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

        var dateUnspecified = DateTime.SpecifyKind(request.WorkshopDate.Date, DateTimeKind.Unspecified);
        var upStartLocal = dateUnspecified + request.StartTime;
        var upEndLocal = request.EndTime > request.StartTime
            ? dateUnspecified + request.EndTime
            : dateUnspecified.AddDays(1) + request.EndTime;

        workshop.Title = request.Title.Trim();
        workshop.IsEthosOriginal = request.IsEthosOriginal;
        workshop.Description = request.Description?.Trim();
        workshop.DanceStyle = request.DanceStyle.Trim();
        workshop.Level = request.Level.Trim();
        workshop.WorkshopDate = DateTime.SpecifyKind(request.WorkshopDate.Date, DateTimeKind.Utc);
        workshop.StartTime = request.StartTime;
        workshop.EndTime = request.EndTime;
        workshop.BookingCutoffTime = request.BookingCutoffTime;
        workshop.Venue = request.Venue.Trim();

        if (request.Capacity < workshop.Capacity)
        {
            var nowUtcForWorkshop = DateTime.UtcNow;
            var activeWorkshopBookings = await _db.WorkshopBookings
                .Where(b => b.WorkshopId == workshopId &&
                            (b.Status == WorkshopBookingStatus.Confirmed ||
                             b.Status == WorkshopBookingStatus.Attended ||
                             (b.Status == WorkshopBookingStatus.PendingPayment &&
                              b.ReservationExpiresAt.HasValue &&
                              b.ReservationExpiresAt.Value > nowUtcForWorkshop)))
                .SumAsync(b => b.Quantity, cancellationToken);

            if (request.Capacity < activeWorkshopBookings)
            {
                throw new BusinessRuleException(
                    "CANNOT_REDUCE_CAPACITY",
                    $"Cannot reduce workshop capacity to {request.Capacity}. There are already {activeWorkshopBookings} active reservations for this workshop.");
            }
        }

        workshop.Capacity = request.Capacity;
        workshop.ImageUrl = request.ImageUrl?.Trim();
        workshop.LandscapeImageUrl = request.LandscapeImageUrl?.Trim();
        workshop.City = request.City?.Trim();
        workshop.Area = request.Area?.Trim();
        workshop.ShortDescription = request.ShortDescription?.Trim();
        workshop.ContactPerson = request.ContactPerson?.Trim();
        workshop.ContactNumber = request.ContactNumber?.Trim();
        if (request.PublicVisibility.HasValue) workshop.PublicVisibility = request.PublicVisibility.Value;
        if (!string.IsNullOrWhiteSpace(request.RegistrationType)) workshop.RegistrationType = request.RegistrationType.Trim();
        workshop.TermsAndCancellationPolicy = request.TermsAndCancellationPolicy?.Trim();
        workshop.GooglePlaceId = request.GooglePlaceId?.Trim();
        workshop.Latitude = request.Latitude;
        workshop.Longitude = request.Longitude;
        workshop.VenueAddress = request.VenueAddress?.Trim();
        workshop.LocationUrl = ValidateAndNormalizeLocationUrl(request.LocationUrl);
        workshop.Timezone = updateTzId;
        workshop.StartUtc = TimeZoneInfo.ConvertTimeToUtc(upStartLocal, updateTz);
        workshop.EndUtc = TimeZoneInfo.ConvertTimeToUtc(upEndLocal, updateTz);
        workshop.TrainerProfileId = request.TrainerProfileId == Guid.Empty ? null : request.TrainerProfileId;
        workshop.AllowReEntry = request.AllowReEntry;
        workshop.RequireReEntryVerification = request.RequireReEntryVerification;
        workshop.ReEntryCooldown = request.ReEntryCooldown;

        if (request.Status.HasValue)
        {
            if (request.Status.Value == WorkshopStatus.Published)
            {
                var effDesc = request.Description ?? workshop.Description;
                if (string.IsNullOrWhiteSpace(effDesc))
                    throw new ArgumentException("Workshop description is required to publish.");
                var effImg = request.ImageUrl ?? workshop.ImageUrl;
                if (string.IsNullOrWhiteSpace(effImg))
                    throw new ArgumentException("Workshop main portrait image is required to publish.");
                if (request.Price <= 0 && workshop.Price <= 0)
                    throw new ArgumentException("Workshop must have a valid price before publishing.");
            }

            workshop.Status = request.Status.Value;
        }

        if (request.Price != workshop.Price)
        {
            workshop.Price = request.Price;
            workshop.AdminApprovedPrice = request.Price;
            workshop.PriceApprovedAt = now;
            workshop.PriceApprovedByUserId = adminUserId;
        }

        // Save custom pricing tiers if provided by admin (legacy workshops without PassTypes only)
        if ((request.PassTypes == null || request.PassTypes.Count == 0) &&
            request.PricingTiers != null && request.PricingTiers.Count > 0)
        {
            var existingTiers = await _db.WorkshopPricingTiers
                .Where(t => t.WorkshopId == workshopId && t.WorkshopPassTypeId == null)
                .ToListAsync(cancellationToken);

            if (existingTiers.Count > 0)
            {
                _db.WorkshopPricingTiers.RemoveRange(existingTiers);
            }

            int tierNum = 1;
            foreach (var item in request.PricingTiers.OrderBy(t => t.TierNumber))
            {
                var tierEntity = new WorkshopPricingTier
                {
                    Id = Guid.NewGuid(),
                    WorkshopId = workshopId,
                    TierNumber = tierNum++,
                    TierName = string.IsNullOrWhiteSpace(item.TierName) ? $"Tier {tierNum}" : item.TierName.Trim(),
                    MinTickets = item.MinTickets > 0 ? item.MinTickets : 1,
                    MaxTickets = item.MaxTickets,
                    Price = item.Price,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.WorkshopPricingTiers.Add(tierEntity);
            }
        }

        // Synchronize Workshop Trainers
        if (request.TrainerProfileIds != null && request.TrainerProfileIds.Count > 0)
        {
            var distinctTrainerIds = request.TrainerProfileIds.Where(id => id != Guid.Empty).Distinct().ToList();
            var existingTrainers = await _db.TrainerProfiles
                .Where(t => distinctTrainerIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, cancellationToken);

            if (existingTrainers.Count != distinctTrainerIds.Count)
            {
                throw new ArgumentException("One or more assigned trainer profiles were not found.");
            }

            // Invariant: Cannot remove a trainer from workshop faculty pool if currently assigned to any session
            var existingFacultyTrainerIds = workshop.WorkshopTrainers.Select(wt => wt.TrainerProfileId).ToList();
            var removedTrainerIds = existingFacultyTrainerIds.Except(distinctTrainerIds).ToList();
            if (removedTrainerIds.Count > 0)
            {
                var assignedInSessions = await _db.WorkshopSessionTrainers
                    .Where(wst => wst.WorkshopSession.WorkshopId == workshopId && removedTrainerIds.Contains(wst.TrainerProfileId))
                    .Include(wst => wst.TrainerProfile.User)
                    .Include(wst => wst.WorkshopSession)
                    .ToListAsync(cancellationToken);

                if (assignedInSessions.Count > 0)
                {
                    var trainerName = assignedInSessions[0].TrainerProfile?.User?.FullName ?? "Selected trainer";
                    var sessionTitle = assignedInSessions[0].WorkshopSession?.Title ?? "Session";
                    throw new InvalidOperationException(
                        $"Cannot remove trainer '{trainerName}' from the workshop faculty pool because they are currently assigned to session '{sessionTitle}'. Please reassign or remove the trainer from all sessions first.");
                }

                var assignedInLegacySessions = await _db.WorkshopSessions
                    .Where(s => s.WorkshopId == workshopId && removedTrainerIds.Contains(s.TrainerProfileId))
                    .Include(s => s.TrainerProfile.User)
                    .ToListAsync(cancellationToken);

                if (assignedInLegacySessions.Count > 0)
                {
                    var trainerName = assignedInLegacySessions[0].TrainerProfile?.User?.FullName ?? "Selected trainer";
                    var sessionTitle = assignedInLegacySessions[0].Title;
                    throw new InvalidOperationException(
                        $"Cannot remove trainer '{trainerName}' from the workshop faculty pool because they are currently assigned to session '{sessionTitle}'. Please reassign or remove the trainer from all sessions first.");
                }
            }

            var existingByTrainerId = workshop.WorkshopTrainers.ToDictionary(wt => wt.TrainerProfileId);

            var toRemove = workshop.WorkshopTrainers.Where(wt => !distinctTrainerIds.Contains(wt.TrainerProfileId)).ToList();
            foreach (var rem in toRemove)
            {
                _db.WorkshopTrainers.Remove(rem);
                workshop.WorkshopTrainers.Remove(rem);
            }

            workshop.TrainerProfileId = distinctTrainerIds[0];
            int order = 0;
            foreach (var tid in distinctTrainerIds)
            {
                if (existingByTrainerId.TryGetValue(tid, out var existingWt))
                {
                    existingWt.DisplayOrder = order++;
                }
                else
                {
                    workshop.WorkshopTrainers.Add(new WorkshopTrainer
                    {
                        Id = Guid.NewGuid(),
                        WorkshopId = workshop.Id,
                        TrainerProfileId = tid,
                        DisplayOrder = order++,
                        AssignedAt = now
                    });
                }
            }
        }
        else if (request.TrainerProfileId.HasValue && request.TrainerProfileId.Value != Guid.Empty)
        {
            var targetTrainerId = request.TrainerProfileId.Value;
            var toRemove = workshop.WorkshopTrainers.Where(wt => wt.TrainerProfileId != targetTrainerId).ToList();
            foreach (var rem in toRemove)
            {
                _db.WorkshopTrainers.Remove(rem);
                workshop.WorkshopTrainers.Remove(rem);
            }

            var existingWt = workshop.WorkshopTrainers.FirstOrDefault(wt => wt.TrainerProfileId == targetTrainerId);
            if (existingWt != null)
            {
                existingWt.DisplayOrder = 0;
            }
            else
            {
                workshop.WorkshopTrainers.Add(new WorkshopTrainer
                {
                    Id = Guid.NewGuid(),
                    WorkshopId = workshop.Id,
                    TrainerProfileId = targetTrainerId,
                    DisplayOrder = 0,
                    AssignedAt = now
                });
            }
        }

        // Synchronize Workshop Sessions
        if (request.Sessions != null && request.Sessions.Count > 0)
        {
            var workshopFacultyIds = workshop.WorkshopTrainers.Select(wt => wt.TrainerProfileId).ToHashSet();
            if (workshop.TrainerProfileId.HasValue && workshop.TrainerProfileId.Value != Guid.Empty)
            {
                workshopFacultyIds.Add(workshop.TrainerProfileId.Value);
            }

            var sessionTrainerMap = new Dictionary<int, List<Guid>>();
            for (int i = 0; i < request.Sessions.Count; i++)
            {
                var s = request.Sessions[i];
                if (string.IsNullOrWhiteSpace(s.Title))
                    throw new ArgumentException("Session title is required.");
                if (s.EndTime <= s.StartTime)
                    throw new ArgumentException($"Session '{s.Title}' end time must be after start time.");
                if (s.Capacity <= 0)
                    throw new ArgumentException($"Session '{s.Title}' capacity must be greater than zero.");

                var tIds = new List<Guid>();
                if (s.TrainerProfileIds != null && s.TrainerProfileIds.Count > 0)
                {
                    tIds.AddRange(s.TrainerProfileIds.Where(id => id != Guid.Empty));
                }
                else if (s.TrainerProfileId.HasValue && s.TrainerProfileId.Value != Guid.Empty)
                {
                    tIds.Add(s.TrainerProfileId.Value);
                }
                else if (workshop.TrainerProfileId.HasValue && workshop.TrainerProfileId.Value != Guid.Empty)
                {
                    tIds.Add(workshop.TrainerProfileId.Value);
                }
                tIds = tIds.Distinct().ToList();

                var invalidTrainerId = tIds.FirstOrDefault(id => !workshopFacultyIds.Contains(id));
                if (invalidTrainerId != Guid.Empty)
                {
                    throw new ArgumentException($"Session '{s.Title}' assigns trainer '{invalidTrainerId}' who is not part of the workshop faculty pool. Add them to the workshop faculty first.");
                }

                sessionTrainerMap[i] = tIds;
            }

            for (int i = 0; i < request.Sessions.Count; i++)
            {
                var s1 = request.Sessions[i];
                var t1List = sessionTrainerMap[i];

                for (int j = i + 1; j < request.Sessions.Count; j++)
                {
                    var s2 = request.Sessions[j];
                    var t2List = sessionTrainerMap[j];

                    if (s1.SessionDate.Date == s2.SessionDate.Date)
                    {
                        var sharedTrainers = t1List.Intersect(t2List).ToList();
                        if (sharedTrainers.Count > 0)
                        {
                            if (s1.StartTime < s2.EndTime && s2.StartTime < s1.EndTime)
                            {
                                throw new ArgumentException($"Trainer has overlapping sessions on {s1.SessionDate:yyyy-MM-dd}: '{s1.Title}' ({s1.StartTime:hh\\:mm}-{s1.EndTime:hh\\:mm}) and '{s2.Title}' ({s2.StartTime:hh\\:mm}-{s2.EndTime:hh\\:mm}).");
                            }
                        }
                    }
                }
            }

            var existingSessions = workshop.Sessions.ToList();
            var existingSessionIds = existingSessions.Select(s => s.Id).OrderBy(id => id).ToList();
            if (existingSessionIds.Count > 0 && _db.Database.IsNpgsql())
            {
                var idListStr = string.Join(",", existingSessionIds.Select(id => $"'{id}'::uuid"));
                await _db.Database.ExecuteSqlRawAsync(
                    $"SELECT \"Id\" FROM workshop_sessions WHERE \"Id\" = ANY(ARRAY[{idListStr}]) ORDER BY \"Id\" FOR UPDATE",
                    cancellationToken);
            }

            var requestSessionIds = request.Sessions.Where(s => s.Id.HasValue && s.Id.Value != Guid.Empty).Select(s => s.Id!.Value).ToHashSet();

            // Synchronize pass types removal first so FK restrict on WorkshopSessionId is satisfied
            if (request.PassTypes != null)
            {
                var existingPasses = workshop.PassTypes.ToList();
                var requestPassIds = request.PassTypes.Where(p => p.Id.HasValue && p.Id.Value != Guid.Empty).Select(p => p.Id!.Value).ToHashSet();

                var toDeletePasses = existingPasses.Where(p => !requestPassIds.Contains(p.Id)).ToList();
                if (toDeletePasses.Count > 0)
                {
                    var toDeletePassIds = toDeletePasses.Select(p => p.Id).ToList();
                    var hasActivePassBookings = await _db.WorkshopBookings
                        .AnyAsync(b => b.WorkshopPassTypeId.HasValue && toDeletePassIds.Contains(b.WorkshopPassTypeId.Value), cancellationToken);
                    if (hasActivePassBookings)
                    {
                        throw new InvalidOperationException("Cannot remove pass type that already has active customer bookings.");
                    }
                    _db.WorkshopPassTypes.RemoveRange(toDeletePasses);
                    foreach (var tp in toDeletePasses) workshop.PassTypes.Remove(tp);
                }
            }

            var toDelete = existingSessions.Where(s => !requestSessionIds.Contains(s.Id)).ToList();
            if (toDelete.Count > 0)
            {
                // Final-graph validation: cannot delete session if any remaining pass in request.PassTypes references it
                if (request.PassTypes != null)
                {
                    foreach (var td in toDelete)
                    {
                        var referencingPass = request.PassTypes.FirstOrDefault(p => p.WorkshopSessionId.HasValue && p.WorkshopSessionId.Value == td.Id);
                        if (referencingPass != null)
                        {
                            throw new ArgumentException($"Cannot delete session '{td.Title}' because it is still referenced by ticket '{referencingPass.Name}'. Please update or remove the ticket first.");
                        }
                    }
                }

                var toDeleteIds = toDelete.Select(s => s.Id).ToList();
                var hasActiveBookings = await _db.WorkshopBookingSessions
                    .Include(bs => bs.WorkshopBooking)
                    .AnyAsync(bs => toDeleteIds.Contains(bs.WorkshopSessionId) &&
                                    bs.Status == WorkshopBookingSessionStatus.Booked &&
                                    (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                                     bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                                     (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                                      bs.WorkshopBooking.ReservationExpiresAt > DateTime.UtcNow)),
                              cancellationToken);
                if (hasActiveBookings)
                {
                    throw new InvalidOperationException("Cannot remove session that already has active customer bookings or reservations.");
                }

                foreach (var td in toDelete)
                {
                    if (!string.IsNullOrWhiteSpace(td.PosterImageUrl))
                    {
                        await DeleteR2MediaExactKeyAsync(td.PosterImageUrl, cancellationToken);
                    }
                    _db.WorkshopSessions.Remove(td);
                    workshop.Sessions.Remove(td);
                }
            }

            int ord = 0;
            var nowUtcForSession = DateTime.UtcNow;
            for (int i = 0; i < request.Sessions.Count; i++)
            {
                var sReq = request.Sessions[i];
                var tIds = sessionTrainerMap[i];
                var leadTrainerId = tIds.Count > 0 ? tIds[0] : (workshop.TrainerProfileId ?? Guid.Empty);

                var existing = sReq.Id.HasValue ? workshop.Sessions.FirstOrDefault(s => s.Id == sReq.Id.Value) : null;
                if (existing != null)
                {
                    if (existing.SessionDate.Date != sReq.SessionDate.Date)
                    {
                        var activeBookingsForDate = await _db.WorkshopBookingSessions
                            .Include(bs => bs.WorkshopBooking)
                            .CountAsync(bs => bs.WorkshopSessionId == existing.Id &&
                                              bs.Status == WorkshopBookingSessionStatus.Booked &&
                                              (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                                               bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                                               (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                                                bs.WorkshopBooking.ReservationExpiresAt > nowUtcForSession)),
                                          cancellationToken);

                        if (activeBookingsForDate > 0)
                        {
                            throw new InvalidOperationException(
                                $"Cannot change the date of session '{existing.Title}' because it already has {activeBookingsForDate} active reservations.");
                        }
                    }

                    if (sReq.Capacity < existing.Capacity)
                    {
                        var activeBookings = await _db.WorkshopBookingSessions
                            .Include(bs => bs.WorkshopBooking)
                            .CountAsync(bs => bs.WorkshopSessionId == existing.Id &&
                                              bs.Status == WorkshopBookingSessionStatus.Booked &&
                                              (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                                               bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                                               (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                                                bs.WorkshopBooking.ReservationExpiresAt > nowUtcForSession)),
                                          cancellationToken);

                        if (sReq.Capacity < activeBookings)
                        {
                            throw new BusinessRuleException(
                                "CANNOT_REDUCE_CAPACITY",
                                $"Cannot reduce capacity to {sReq.Capacity}. There are already {activeBookings} active reservations for this session.");
                        }
                    }

                    // Handle R2 media replacement if poster changed
                    var newPosterUrl = sReq.PosterImageUrl?.Trim();
                    if (!string.IsNullOrWhiteSpace(existing.PosterImageUrl) && existing.PosterImageUrl != newPosterUrl)
                    {
                        await DeleteR2MediaExactKeyAsync(existing.PosterImageUrl, cancellationToken);
                    }

                    existing.SessionDate = DateTime.SpecifyKind(sReq.SessionDate.Date, DateTimeKind.Utc);
                    existing.StartTime = sReq.StartTime;
                    existing.EndTime = sReq.EndTime;
                    existing.TrainerProfileId = leadTrainerId;
                    existing.Title = sReq.Title.Trim();
                    existing.Description = sReq.Description?.Trim();
                    existing.Capacity = sReq.Capacity;
                    existing.BookingCutoffTime = sReq.BookingCutoffTime;
                    existing.PosterImageUrl = newPosterUrl;
                    existing.DisplayOrder = sReq.DisplayOrder > 0 ? sReq.DisplayOrder : ord++;
                    existing.IsActive = sReq.IsActive;
                    existing.UpdatedAt = now;

                    // Synchronize Session Trainers
                    var existingStList = existing.SessionTrainers.ToList();
                    foreach (var est in existingStList)
                    {
                        _db.WorkshopSessionTrainers.Remove(est);
                        existing.SessionTrainers.Remove(est);
                    }

                    int stOrd = 0;
                    foreach (var tid in tIds)
                    {
                        var newSt = new WorkshopSessionTrainer
                        {
                            Id = Guid.NewGuid(),
                            WorkshopSessionId = existing.Id,
                            TrainerProfileId = tid,
                            DisplayOrder = stOrd++,
                            AssignedAt = now
                        };
                        existing.SessionTrainers.Add(newSt);
                        _db.WorkshopSessionTrainers.Add(newSt);
                    }
                }
                else
                {
                    var newSession = new WorkshopSession
                    {
                        Id = sReq.Id.HasValue && sReq.Id.Value != Guid.Empty ? sReq.Id.Value : Guid.NewGuid(),
                        WorkshopId = workshopId,
                        SessionDate = DateTime.SpecifyKind(sReq.SessionDate.Date, DateTimeKind.Utc),
                        StartTime = sReq.StartTime,
                        EndTime = sReq.EndTime,
                        TrainerProfileId = leadTrainerId,
                        Title = sReq.Title.Trim(),
                        Description = sReq.Description?.Trim(),
                        Capacity = sReq.Capacity,
                        BookingCutoffTime = sReq.BookingCutoffTime,
                        PosterImageUrl = sReq.PosterImageUrl?.Trim(),
                        DisplayOrder = sReq.DisplayOrder > 0 ? sReq.DisplayOrder : ord++,
                        IsActive = sReq.IsActive,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    int stOrd = 0;
                    foreach (var tid in tIds)
                    {
                        newSession.SessionTrainers.Add(new WorkshopSessionTrainer
                        {
                            Id = Guid.NewGuid(),
                            WorkshopSessionId = newSession.Id,
                            TrainerProfileId = tid,
                            DisplayOrder = stOrd++,
                            AssignedAt = now
                        });
                    }

                    workshop.Sessions.Add(newSession);
                    _db.WorkshopSessions.Add(newSession);
                }
            }

            var earliestSession = workshop.Sessions.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime).First();
            var latestSession = workshop.Sessions.OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.EndTime).First();
            workshop.WorkshopDate = earliestSession.SessionDate;
            workshop.StartTime = earliestSession.StartTime;
            workshop.EndTime = latestSession.EndTime;
            var date1Unspecified = DateTime.SpecifyKind(earliestSession.SessionDate.Date, DateTimeKind.Unspecified);
            var date2Unspecified = DateTime.SpecifyKind(latestSession.SessionDate.Date, DateTimeKind.Unspecified);
            var startLocalSession = date1Unspecified + earliestSession.StartTime;
            var endLocalSession = date2Unspecified + latestSession.EndTime;
            workshop.StartUtc = TimeZoneInfo.ConvertTimeToUtc(startLocalSession, updateTz);
            workshop.EndUtc = TimeZoneInfo.ConvertTimeToUtc(endLocalSession, updateTz);
            workshop.Capacity = Math.Max(workshop.Capacity, workshop.Sessions.Max(s => s.Capacity));
        }

        // Synchronize Workshop Pass Types
        if (request.PassTypes != null && request.PassTypes.Count > 0)
        {
            int pOrd = 0;
            foreach (var pReq in request.PassTypes)
            {
                if (string.IsNullOrWhiteSpace(pReq.Name))
                    throw new ArgumentException("Ticket type name is required.");
                if (pReq.Price < 0)
                    throw new ArgumentException("Ticket type price cannot be negative.");
                if (pReq.SalesStartUtc.HasValue && pReq.SalesEndUtc.HasValue && pReq.SalesStartUtc.Value >= pReq.SalesEndUtc.Value)
                {
                    throw new ArgumentException($"Ticket type '{pReq.Name}' sales start date must be before sales end date.");
                }

                // Pass Category Classification Matrix Validation
                if (pReq.WorkshopSessionId.HasValue)
                {
                    if (pReq.SessionsIncluded != 1)
                    {
                        throw new ArgumentException($"Single-session ticket '{pReq.Name}' must have SessionsIncluded equal to 1.");
                    }
                    bool sessionExists = workshop.Sessions.Any(s => s.Id == pReq.WorkshopSessionId.Value);
                    if (!sessionExists)
                    {
                        throw new ArgumentException($"Session referenced by ticket '{pReq.Name}' was not found in the workshop sessions.");
                    }
                }
                else
                {
                    if (pReq.SessionsIncluded.HasValue)
                    {
                        if (pReq.SessionsIncluded.Value < 1)
                        {
                            throw new ArgumentException($"Ticket '{pReq.Name}' must have SessionsIncluded greater than or equal to 1.");
                        }
                        int totalSessions = workshop.Sessions.Count;
                        if (totalSessions > 0 && pReq.SessionsIncluded.Value > totalSessions)
                        {
                            throw new ArgumentException($"Ticket '{pReq.Name}' cannot include more sessions ({pReq.SessionsIncluded.Value}) than total workshop sessions ({totalSessions}).");
                        }
                    }
                }

                var existing = pReq.Id.HasValue ? workshop.PassTypes.FirstOrDefault(p => p.Id == pReq.Id.Value) : null;
                if (existing != null)
                {
                    existing.WorkshopSessionId = pReq.WorkshopSessionId;
                    existing.Name = pReq.Name.Trim();
                    existing.Description = pReq.Description?.Trim();
                    existing.Price = pReq.Price;
                    existing.SessionsIncluded = pReq.SessionsIncluded;
                    existing.TotalQuantity = pReq.TotalQuantity > 0 ? pReq.TotalQuantity : 1000;
                    existing.SalesStartUtc = pReq.SalesStartUtc.HasValue ? DateTime.SpecifyKind(pReq.SalesStartUtc.Value, DateTimeKind.Utc) : null;
                    existing.SalesEndUtc = pReq.SalesEndUtc.HasValue ? DateTime.SpecifyKind(pReq.SalesEndUtc.Value, DateTimeKind.Utc) : null;
                    existing.DisplayOrder = pReq.DisplayOrder > 0 ? pReq.DisplayOrder : pOrd++;
                    existing.IsActive = pReq.IsActive;

                    if (pReq.PricingTiers != null)
                    {
                        _pricingService.ValidateTicketTypePricingTiers(existing.TotalQuantity, pReq.PricingTiers);

                        var oldTiers = await _db.WorkshopPricingTiers
                            .Where(t => t.WorkshopPassTypeId == existing.Id)
                            .ToListAsync(cancellationToken);
                        _db.WorkshopPricingTiers.RemoveRange(oldTiers);
                        existing.PricingTiers.Clear();

                        foreach (var pt in pReq.PricingTiers.OrderBy(t => t.TierNumber))
                        {
                            var newTier = new WorkshopPricingTier
                            {
                                Id = Guid.NewGuid(),
                                WorkshopId = workshopId,
                                WorkshopPassTypeId = existing.Id,
                                TierNumber = pt.TierNumber,
                                TierName = string.IsNullOrWhiteSpace(pt.TierName) ? $"Tier {pt.TierNumber}" : pt.TierName.Trim(),
                                MinTickets = pt.MinTickets,
                                MaxTickets = pt.MaxTickets,
                                Price = pt.Price,
                                CreatedAt = now,
                                UpdatedAt = now
                            };
                            existing.PricingTiers.Add(newTier);
                            _db.WorkshopPricingTiers.Add(newTier);
                        }
                    }
                }
                else
                {
                    var newPassTotalQty = pReq.TotalQuantity > 0 ? pReq.TotalQuantity : 1000;
                    var newPassId = pReq.Id.HasValue && pReq.Id.Value != Guid.Empty ? pReq.Id.Value : Guid.NewGuid();
                    var newPass = new WorkshopPassType
                    {
                        Id = newPassId,
                        WorkshopId = workshopId,
                        WorkshopSessionId = pReq.WorkshopSessionId,
                        Name = pReq.Name.Trim(),
                        Description = pReq.Description?.Trim(),
                        Price = pReq.Price,
                        SessionsIncluded = pReq.SessionsIncluded,
                        TotalQuantity = newPassTotalQty,
                        SalesStartUtc = pReq.SalesStartUtc.HasValue ? DateTime.SpecifyKind(pReq.SalesStartUtc.Value, DateTimeKind.Utc) : null,
                        SalesEndUtc = pReq.SalesEndUtc.HasValue ? DateTime.SpecifyKind(pReq.SalesEndUtc.Value, DateTimeKind.Utc) : null,
                        DisplayOrder = pReq.DisplayOrder > 0 ? pReq.DisplayOrder : pOrd++,
                        IsActive = pReq.IsActive,
                        CreatedAt = now
                    };

                    if (pReq.PricingTiers != null && pReq.PricingTiers.Count > 0)
                    {
                        _pricingService.ValidateTicketTypePricingTiers(newPassTotalQty, pReq.PricingTiers);
                        foreach (var pt in pReq.PricingTiers.OrderBy(t => t.TierNumber))
                        {
                            var newTier = new WorkshopPricingTier
                            {
                                Id = Guid.NewGuid(),
                                WorkshopId = workshopId,
                                WorkshopPassTypeId = newPassId,
                                TierNumber = pt.TierNumber,
                                TierName = string.IsNullOrWhiteSpace(pt.TierName) ? $"Tier {pt.TierNumber}" : pt.TierName.Trim(),
                                MinTickets = pt.MinTickets,
                                MaxTickets = pt.MaxTickets,
                                Price = pt.Price,
                                CreatedAt = now,
                                UpdatedAt = now
                            };
                            newPass.PricingTiers.Add(newTier);
                            _db.WorkshopPricingTiers.Add(newTier);
                        }
                    }

                    workshop.PassTypes.Add(newPass);
                    _db.WorkshopPassTypes.Add(newPass);
                }
            }
        }

        workshop.UpdatedAt = now;

        _auditService.AddAuditLog(
            adminUserId,
            "ADMIN_WORKSHOP_UPDATED",
            "Workshop",
            workshop.Id,
            $"Administrator updated workshop '{workshop.Title}' (Status: {workshop.Status}, Price: {workshop.Price} INR)");

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException($"Failed to save workshop to database: {msg}");
        }

        if (workshop.TrainerProfileId.HasValue && workshop.TrainerProfile == null)
        {
            await _db.Entry(workshop)
                .Reference(w => w.TrainerProfile)
                .LoadAsync(cancellationToken);
        }

        await _db.Entry(workshop)
            .Collection(w => w.WorkshopTrainers)
            .Query()
            .Include(wt => wt.TrainerProfile)
            .LoadAsync(cancellationToken);

        await _db.Entry(workshop)
            .Collection(w => w.Sessions)
            .Query()
            .Include(s => s.TrainerProfile)
            .LoadAsync(cancellationToken);

        await _db.Entry(workshop)
            .Collection(w => w.PassTypes)
            .Query()
            .Include(p => p.PricingTiers)
            .LoadAsync(cancellationToken);

        return Map(workshop);
    }

    public async Task ApproveWorkshopPriceAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminApproveWorkshopPriceRequest request,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        ValidateStatusTransition(workshop.Status, WorkshopStatus.Approved);

        if (workshop.Status != WorkshopStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only workshops in PendingApproval status can be approved.");
        }

        decimal approvedPrice = request?.ApprovedPrice ?? 0m;
        if (approvedPrice <= 0)
        {
            if (workshop.TrainerProposedPrice.HasValue && workshop.TrainerProposedPrice.Value > 0)
            {
                approvedPrice = workshop.TrainerProposedPrice.Value;
            }
            else if (workshop.Price > 0)
            {
                approvedPrice = workshop.Price;
            }
            else
            {
                throw new ArgumentException("Approved price must be greater than zero.");
            }
        }

        var previousPrice = workshop.AdminApprovedPrice;
        workshop.AdminApprovedPrice = approvedPrice;
        workshop.Price = approvedPrice;
        workshop.PriceApprovedAt = DateTime.UtcNow;
        workshop.PriceApprovedByUserId = adminUserId;
        workshop.Status = WorkshopStatus.Approved;
        workshop.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_APPROVED",
            "Workshop",
            workshop.Id,
            $"Approved price {approvedPrice} INR (previous: {(previousPrice.HasValue ? previousPrice.Value.ToString() + " INR" : "none")})");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        ValidateStatusTransition(workshop.Status, WorkshopStatus.Rejected);

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.");

        workshop.Status = WorkshopStatus.Rejected;
        workshop.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_REJECTED",
            "Workshop",
            workshop.Id,
            reason);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminWorkshopCancellationStatsDto> GetWorkshopCancellationStatsAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        var bookings = await _db.WorkshopBookings
            .AsNoTracking()
            .Where(b => b.WorkshopId == workshopId && b.Status != WorkshopBookingStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var paymentIds = bookings
            .Where(b => b.PaymentTransactionId.HasValue)
            .Select(b => b.PaymentTransactionId!.Value)
            .Distinct()
            .ToList();

        var payments = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => paymentIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var paidBookings = bookings
            .Where(b => b.PaymentTransactionId.HasValue &&
                        payments.TryGetValue(b.PaymentTransactionId.Value, out var p) &&
                        p.Status == PaymentStatus.Paid)
            .ToList();

        var totalRefundableAmount = paidBookings.Sum(b => payments[b.PaymentTransactionId!.Value].Amount);
        var totalAttendees = bookings.Sum(b => b.Quantity);

        var existingRefundJobsCount = await _db.RefundJobs
            .AsNoTracking()
            .CountAsync(j => j.WorkshopId == workshopId, cancellationToken);

        return new AdminWorkshopCancellationStatsDto
        {
            WorkshopId = workshop.Id,
            Title = workshop.Title,
            Status = workshop.Status.ToString(),
            TotalBookings = bookings.Count,
            PaidBookings = paidBookings.Count,
            TotalRefundableAmount = totalRefundableAmount,
            TotalAttendees = totalAttendees,
            ExistingRefundCount = existingRefundJobsCount
        };
    }

    public async Task CancelWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        using var tx = _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        ValidateStatusTransition(workshop.Status, WorkshopStatus.Cancelled);

        var cancelReason = string.IsNullOrWhiteSpace(reason) ? "Workshop cancelled by admin." : reason.Trim();

        workshop.Status = WorkshopStatus.Cancelled;
        workshop.UpdatedAt = DateTime.UtcNow;

        // Fetch active bookings for workshop
        var bookings = await _db.WorkshopBookings
            .Include(b => b.Tickets)
            .Where(b => b.WorkshopId == workshopId && b.Status != WorkshopBookingStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var cancelPaymentIds = bookings
            .Where(b => b.PaymentTransactionId.HasValue)
            .Select(b => b.PaymentTransactionId!.Value)
            .Distinct()
            .ToList();

        var cancelPayments = await _db.PaymentTransactions
            .Where(p => cancelPaymentIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var booking in bookings)
        {
            booking.Status = WorkshopBookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;

            foreach (var ticket in booking.Tickets)
            {
                ticket.Status = TicketStatus.Cancelled;
            }

            // If booking has a paid payment transaction, enqueue a RefundJob
            if (booking.PaymentTransactionId.HasValue &&
                cancelPayments.TryGetValue(booking.PaymentTransactionId.Value, out var payment) &&
                payment.Status == PaymentStatus.Paid)
            {
                var existingJob = await _db.RefundJobs
                    .FirstOrDefaultAsync(j => j.BookingId == booking.Id && j.PaymentTransactionId == booking.PaymentTransactionId.Value, cancellationToken);

                if (existingJob == null)
                {
                    var refundJob = new RefundJob
                    {
                        Id = Guid.NewGuid(),
                        WorkshopId = workshopId,
                        BookingId = booking.Id,
                        PaymentTransactionId = booking.PaymentTransactionId.Value,
                        AmountPaise = (long)Math.Round(payment.Amount * 100m, MidpointRounding.AwayFromZero),
                        Reason = cancelReason,
                        InitiatedByAdminId = adminUserId,
                        Status = RefundStatus.Requested,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    _db.RefundJobs.Add(refundJob);
                }
            }
        }

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_CANCELLED",
            "Workshop",
            workshop.Id,
            $"{cancelReason} ({bookings.Count} bookings cancelled, refund jobs enqueued).");

        await _db.SaveChangesAsync(cancellationToken);

        if (tx != null)
        {
            await tx.CommitAsync(cancellationToken);
        }
    }

    public async Task<AdminWorkshopRefundProgressDto> GetWorkshopRefundProgressAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var jobs = await _db.RefundJobs
            .AsNoTracking()
            .Include(j => j.Booking)
                .ThenInclude(b => b!.StudentProfile)
                    .ThenInclude(s => s.User)
            .Include(j => j.PaymentRefund)
            .Where(j => j.WorkshopId == workshopId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var jobDtos = jobs.Select(j =>
        {
            var customerName = !string.IsNullOrWhiteSpace(j.Booking?.GuestName)
                ? j.Booking.GuestName
                : (j.Booking?.StudentProfile?.User?.FullName ?? "Unknown Customer");

            var customerPhone = !string.IsNullOrWhiteSpace(j.Booking?.GuestPhone)
                ? j.Booking.GuestPhone
                : (j.Booking?.StudentProfile?.User?.Phone ?? string.Empty);

            return new AdminWorkshopRefundJobItemDto
            {
                JobId = j.Id,
                BookingId = j.BookingId,
                CustomerName = customerName,
                CustomerPhone = customerPhone,
                Amount = j.AmountPaise / 100m,
                Status = j.Status.ToString(),
                RazorpayRefundId = j.PaymentRefund?.RazorpayRefundId,
                LastError = j.LastError ?? j.PaymentRefund?.FailureReason,
                ProcessedAtUtc = j.ProcessedAtUtc ?? j.PaymentRefund?.ProcessedAtUtc
            };
        }).ToList();

        return new AdminWorkshopRefundProgressDto
        {
            WorkshopId = workshopId,
            TotalJobs = jobs.Count,
            ProcessedJobs = jobs.Count(j => j.Status == RefundStatus.Processed),
            FailedJobs = jobs.Count(j => j.Status == RefundStatus.Failed),
            ProcessingJobs = jobs.Count(j => j.Status == RefundStatus.Processing),
            RequestedJobs = jobs.Count(j => j.Status == RefundStatus.Requested),
            ReconciliationRequiredJobs = jobs.Count(j => j.Status == RefundStatus.ReconciliationRequired),
            Jobs = jobDtos
        };
    }

    public async Task<int> RetryFailedWorkshopRefundsAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var failedJobs = await _db.RefundJobs
            .Where(j => j.WorkshopId == workshopId && j.Status == RefundStatus.Failed)
            .ToListAsync(cancellationToken);

        if (failedJobs.Count == 0) return 0;

        foreach (var job in failedJobs)
        {
            job.Status = RefundStatus.Requested;
            job.NextRetryUtc = null;
            job.LastError = null;
        }

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_REFUNDS_RETRIED",
            "Workshop",
            workshopId,
            $"Retried {failedJobs.Count} failed refund jobs for workshop {workshopId}.");

        await _db.SaveChangesAsync(cancellationToken);
        return failedJobs.Count;
    }

    public async Task CompleteWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        bool forceComplete = false,
        string? overrideReason = null,
        CancellationToken cancellationToken = default)
    {
        var workshop = await _db.Workshops
            .Include(w => w.Sessions)
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        if (workshop.Status == WorkshopStatus.Completed)
        {
            // Idempotent rerun: already completed, safe no-op
            return;
        }

        ValidateStatusTransition(workshop.Status, WorkshopStatus.Completed);

        var activeSessions = workshop.Sessions?.Where(s => s.IsActive).ToList() ?? new List<WorkshopSession>();
        var nowUtc = DateTime.UtcNow;

        bool hasFutureSessions = false;
        if (activeSessions.Count > 0)
        {
            foreach (var session in activeSessions)
            {
                var sessionEndUtc = session.SessionDate.Date + session.EndTime;
                if (sessionEndUtc > nowUtc)
                {
                    hasFutureSessions = true;
                    break;
                }
            }
        }
        else if (workshop.EndUtc.HasValue && workshop.EndUtc.Value > nowUtc)
        {
            hasFutureSessions = true;
        }

        if (hasFutureSessions)
        {
            if (!forceComplete || string.IsNullOrWhiteSpace(overrideReason) || overrideReason.Trim().Length < 5)
            {
                throw new InvalidOperationException("Cannot complete a workshop with future active sessions unless forceComplete is true with an overrideReason of at least 5 characters.");
            }
        }

        // STEP 1: DATABASE TRANSACTION
        // Capture exact R2 keys for ephemeral media (LandscapeImageUrl and session PosterImageUrl)
        // STRICT INVARIANT: ImageUrl (permanent portrait) is NEVER captured for deletion!
        var keysToDelete = new List<string>();

        if (!string.IsNullOrWhiteSpace(workshop.LandscapeImageUrl))
        {
            var key = ExtractExactR2Key(workshop.LandscapeImageUrl);
            if (!string.IsNullOrEmpty(key))
            {
                keysToDelete.Add(key);
            }
        }

        if (workshop.Sessions != null)
        {
            foreach (var session in workshop.Sessions)
            {
                if (!string.IsNullOrWhiteSpace(session.PosterImageUrl))
                {
                    var key = ExtractExactR2Key(session.PosterImageUrl);
                    if (!string.IsNullOrEmpty(key))
                    {
                        keysToDelete.Add(key);
                    }
                    session.PosterImageUrl = null;
                }
            }
        }

        workshop.LandscapeImageUrl = null;
        workshop.Status = WorkshopStatus.Completed;
        workshop.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_COMPLETED",
            "Workshop",
            workshop.Id,
            $"Workshop completed. Ephemeral media scheduled for deletion: {keysToDelete.Count} keys. {(forceComplete ? $"Early completion override: {overrideReason}" : "")}");

        await _db.SaveChangesAsync(cancellationToken);

        // STEP 2: POST-COMMIT EXACT-KEY R2 CLEANUP
        // Only run post-commit. If R2 fails, log audit warning; DB commit remains safe & intact.
        bool r2AllSucceeded = true;
        foreach (var key in keysToDelete)
        {
            try
            {
                if (_r2Storage != null)
                {
                    await _r2Storage.DeleteAsync(key, cancellationToken);
                }
            }
            catch
            {
                r2AllSucceeded = false;
                // Post-commit R2 deletion failure does not rollback DB
            }
        }

        if (keysToDelete.Count > 0)
        {
            if (r2AllSucceeded)
            {
                _auditService.AddAuditLog(
                    adminUserId,
                    "WORKSHOP_MEDIA_CLEANED",
                    "Workshop",
                    workshop.Id,
                    $"Successfully cleaned {keysToDelete.Count} ephemeral media assets from R2.");
            }
            else
            {
                _auditService.AddAuditLog(
                    adminUserId,
                    "WORKSHOP_MEDIA_CLEANUP_FAILED",
                    "Workshop",
                    workshop.Id,
                    $"One or more ephemeral media assets failed to delete from R2.");
            }
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var workshop = await _db.Workshops
            .Include(w => w.Sessions)
                .ThenInclude(s => s.SessionTrainers)
            .Include(w => w.WorkshopTrainers)
            .Include(w => w.PassTypes)
            .Include(w => w.PricingTiers)
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        if (workshop.Status == WorkshopStatus.Completed)
        {
            _auditService.AddAuditLog(
                adminUserId,
                "WORKSHOP_DELETE_REJECTED",
                "Workshop",
                workshop.Id,
                "Cannot delete a completed workshop. Historical studio record is permanent.");
            throw new InvalidOperationException("Cannot delete workshop with existing bookings or financial records. Use Cancel or Archive instead.");
        }

        bool hasBookings = await _db.WorkshopBookings.AnyAsync(b => b.WorkshopId == workshopId, cancellationToken);
        if (hasBookings)
        {
            _auditService.AddAuditLog(
                adminUserId,
                "WORKSHOP_DELETE_REJECTED",
                "Workshop",
                workshop.Id,
                "Cannot delete workshop with booking history.");
            throw new InvalidOperationException("Cannot delete workshop with existing bookings or financial records. Use Cancel or Archive instead.");
        }

        bool hasPayments = await _db.PaymentTransactions.AnyAsync(
            pt => pt.Purpose == PaymentPurpose.WorkshopBooking && pt.ReferenceId == workshopId,
            cancellationToken);
        if (hasPayments)
        {
            _auditService.AddAuditLog(
                adminUserId,
                "WORKSHOP_DELETE_REJECTED",
                "Workshop",
                workshop.Id,
                "Cannot delete workshop with financial transaction records.");
            throw new InvalidOperationException("Cannot delete workshop with existing bookings or financial records. Use Cancel or Archive instead.");
        }

        var keysToDelete = new List<string>();
        var portraitKey = ExtractExactR2Key(workshop.ImageUrl);
        if (!string.IsNullOrEmpty(portraitKey)) keysToDelete.Add(portraitKey);

        var landscapeKey = ExtractExactR2Key(workshop.LandscapeImageUrl);
        if (!string.IsNullOrEmpty(landscapeKey)) keysToDelete.Add(landscapeKey);

        if (workshop.Sessions != null)
        {
            foreach (var s in workshop.Sessions)
            {
                var posterKey = ExtractExactR2Key(s.PosterImageUrl);
                if (!string.IsNullOrEmpty(posterKey)) keysToDelete.Add(posterKey);
            }
        }

        // Cascading deletion:
        // 1. Remove associated WorkshopDraft records
        var drafts = await _db.WorkshopDrafts
            .Where(d => d.WorkshopId == workshopId)
            .ToListAsync(cancellationToken);
        if (drafts.Count > 0)
        {
            _db.WorkshopDrafts.RemoveRange(drafts);
        }

        // 2. Remove child rows in dependency order:
        if (workshop.Sessions != null)
        {
            foreach (var s in workshop.Sessions)
            {
                if (s.SessionTrainers != null && s.SessionTrainers.Count > 0)
                {
                    _db.WorkshopSessionTrainers.RemoveRange(s.SessionTrainers);
                }
            }
            _db.WorkshopSessions.RemoveRange(workshop.Sessions);
        }

        if (workshop.PricingTiers != null && workshop.PricingTiers.Count > 0)
        {
            _db.WorkshopPricingTiers.RemoveRange(workshop.PricingTiers);
        }

        if (workshop.PassTypes != null && workshop.PassTypes.Count > 0)
        {
            _db.WorkshopPassTypes.RemoveRange(workshop.PassTypes);
        }

        if (workshop.WorkshopTrainers != null && workshop.WorkshopTrainers.Count > 0)
        {
            _db.WorkshopTrainers.RemoveRange(workshop.WorkshopTrainers);
        }

        _db.Workshops.Remove(workshop);

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_DELETED",
            "Workshop",
            workshop.Id,
            $"Administrator deleted unbooked workshop '{workshop.Title}' and associated resources.");

        await _db.SaveChangesAsync(cancellationToken);

        // Post-commit exact-key R2 deletion
        foreach (var key in keysToDelete)
        {
            try
            {
                if (_r2Storage != null)
                {
                    await _r2Storage.DeleteAsync(key, cancellationToken);
                }
            }
            catch
            {
                // Ephemeral/unbooked R2 cleanup failure does not abort operation
            }
        }
    }

    
    public async Task<AdminWorkshopPricingTiersResponse> GetWorkshopPricingTiersAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        var tiers = await _db.WorkshopPricingTiers
            .AsNoTracking()
            .Where(t => t.WorkshopId == workshopId && t.WorkshopPassTypeId == null)
            .OrderBy(t => t.TierNumber)
            .ToListAsync(cancellationToken);

        var confirmedSold = await _db.WorkshopBookings
            .Where(b => b.WorkshopId == workshopId && b.Status == WorkshopBookingStatus.Confirmed)
            .SumAsync(b => b.Quantity, cancellationToken);

        var items = tiers.Select(t => new AdminWorkshopPricingTierItem
        {
            TierNumber = t.TierNumber,
            TierName = t.TierName,
            MinTickets = t.MinTickets,
            MaxTickets = t.MaxTickets,
            Price = t.Price
        }).ToList();

        return new AdminWorkshopPricingTiersResponse
        {
            WorkshopId = workshop.Id,
            WorkshopTitle = workshop.Title,
            Capacity = workshop.Capacity,
            ConfirmedTicketsSold = confirmedSold,
            Tiers = items
        };
    }

    public async Task UpdateWorkshopPricingTiersAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminUpdateWorkshopPricingTiersRequest request,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        if (request?.Tiers == null || request.Tiers.Count < 1)
        {
            throw new ArgumentException("At least one pricing tier must be provided.");
        }

        foreach (var t in request.Tiers)
        {
            if (t.Price < 0)
                throw new ArgumentException($"Price for Tier {t.TierNumber} cannot be negative.");
        }

        var existingTiers = await _db.WorkshopPricingTiers
            .Where(t => t.WorkshopId == workshopId && t.WorkshopPassTypeId == null)
            .ToListAsync(cancellationToken);

        if (existingTiers.Count > 0)
        {
            _db.WorkshopPricingTiers.RemoveRange(existingTiers);
        }

        int tierCount = 1;
        foreach (var item in request.Tiers.OrderBy(t => t.TierNumber))
        {
            var tierEntity = new WorkshopPricingTier
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshopId,
                WorkshopPassTypeId = null,
                TierNumber = tierCount++,
                TierName = string.IsNullOrWhiteSpace(item.TierName) ? $"Tier {tierCount}" : item.TierName.Trim(),
                MinTickets = item.MinTickets > 0 ? item.MinTickets : 1,
                MaxTickets = item.MaxTickets,
                Price = item.Price,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.WorkshopPricingTiers.Add(tierEntity);
        }

        // Keep base workshop.Price synchronized with Tier 1
        var tier1 = request.Tiers.FirstOrDefault(t => t.TierNumber == 1);
        if (tier1 != null)
        {
            workshop.Price = tier1.Price;
            workshop.AdminApprovedPrice = tier1.Price;
            workshop.UpdatedAt = DateTime.UtcNow;
        }

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_PRICING_TIERS_UPDATED",
            "Workshop",
            workshop.Id,
            request.Reason ?? "Updated 4-tier pricing structure for workshop.");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AdminWorkshopRegistrationResponse>> GetWorkshopRegistrationsAsync(
        Guid workshopId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.WorkshopBookings
            .AsNoTracking()
            .Include(b => b.Workshop)
            .Include(b => b.StudentProfile)
                .ThenInclude(s => s.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.WorkshopSession)
                    .ThenInclude(s => s!.TrainerProfile)
            .Include(b => b.BookingSessions)
                .ThenInclude(bs => bs.WorkshopSession)
                    .ThenInclude(s => s!.TrainerProfile)
            .Where(b => b.WorkshopId == workshopId)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var bookings = await query
            .OrderByDescending(b => b.BookedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var bookingIds = bookings.Select(b => b.Id).ToList();
        var studentProfileIds = bookings.Select(b => b.StudentProfileId).Distinct().ToList();

        // Query feedback for these bookings or students - strictly enforce feedback validity and attended booking status
        var feedbacks = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(f => f.WorkshopId == workshopId &&
                        f.IsValid &&
                        f.WorkshopBooking != null &&
                        f.WorkshopBooking.Status == WorkshopBookingStatus.Attended &&
                        ((f.WorkshopBookingId != null && bookingIds.Contains(f.WorkshopBookingId.Value)) ||
                         (f.StudentProfileId != null && studentProfileIds.Contains(f.StudentProfileId.Value))))
            .ToListAsync(cancellationToken);

        var items = bookings.Select(b =>
        {
            var user = b.StudentProfile?.User;
            var isGuest = user == null ||
                          string.IsNullOrEmpty(user.PasswordHash) ||
                          user.CustomerCode.StartsWith("GUEST", StringComparison.OrdinalIgnoreCase) ||
                          user.FullName.StartsWith("Guest", StringComparison.OrdinalIgnoreCase) ||
                          !user.UserRoles.Any(r => r.Role.Name == "STUDENT");

            var feedback = feedbacks.FirstOrDefault(f =>
                (f.WorkshopBookingId.HasValue && f.WorkshopBookingId.Value == b.Id) ||
                (f.StudentProfileId.HasValue && f.StudentProfileId.Value == b.StudentProfileId));

            string attendanceStatus = b.Status switch
            {
                WorkshopBookingStatus.Attended => "Present",
                WorkshopBookingStatus.NoShow => "Absent",
                WorkshopBookingStatus.Confirmed => "Not marked",
                WorkshopBookingStatus.Cancelled => "Cancelled",
                WorkshopBookingStatus.PendingPayment => "Pending Payment",
                _ => b.Status.ToString()
            };

            string paymentStatus = (b.PaymentTransactionId.HasValue ||
                                    b.Status == WorkshopBookingStatus.Confirmed ||
                                    b.Status == WorkshopBookingStatus.Attended ||
                                    b.Status == WorkshopBookingStatus.NoShow)
                ? "Paid"
                : (b.Status == WorkshopBookingStatus.PendingPayment ? "Pending" : "Free/Unpaid");

            string feedbackStatus = feedback != null
                ? $"Submitted ★ {feedback.Rating}"
                : "Pending";

            return new AdminWorkshopRegistrationResponse
            {
                BookingId = b.Id,
                WorkshopId = b.WorkshopId,
                WorkshopTitle = b.Workshop.Title,
                StudentId = b.StudentProfileId,
                StudentName = isGuest && (user == null || user.FullName == "Guest User" || string.IsNullOrWhiteSpace(user.FullName))
                    ? "Workshop Attendee"
                    : (user?.FullName ?? "Workshop Attendee"),
                StudentPhone = user?.Phone ?? "—",
                StudentEmail = user?.Email,
                Status = b.Status.ToString(),
                BookedAt = b.BookedAt,
                PaymentTransactionId = b.PaymentTransactionId,
                PaymentStatus = paymentStatus,
                CustomerCode = user?.CustomerCode ?? "GUEST",
                BookingReference = $"BK-{b.Id.ToString()[..8].ToUpperInvariant()}",
                IsGuest = isGuest,
                AttendeeType = isGuest ? "Workshop Attendee" : "ETHOS Student",
                AttendanceStatus = attendanceStatus,
                FeedbackStatus = feedbackStatus,
                FeedbackRating = feedback?.Rating,
                FeedbackComment = feedback?.Comment,
                PassName = b.PassName,
                SessionsIncludedCount = b.SessionsIncludedCount,
                Tickets = b.Tickets.OrderBy(t => t.TicketNumber).Select(t => new Ethos.Api.Contracts.Workshops.WorkshopTicketResponse
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber,
                    WorkshopBookingId = t.WorkshopBookingId,
                    WorkshopId = t.WorkshopId,
                    WorkshopSessionId = t.WorkshopSessionId,
                    SessionTitle = t.WorkshopSession != null ? t.WorkshopSession.Title : null,
                    SessionDate = t.WorkshopSession != null ? t.WorkshopSession.SessionDate : null,
                    SessionStartTime = t.WorkshopSession != null ? t.WorkshopSession.StartTime : null,
                    SessionEndTime = t.WorkshopSession != null ? t.WorkshopSession.EndTime : null,
                    SessionTrainerName = t.WorkshopSession != null && t.WorkshopSession.TrainerProfile != null ? t.WorkshopSession.TrainerProfile.FullName : null,
                    WorkshopTitle = b.Workshop.Title,
                    AttendeeName = t.AttendeeName,
                    AttendeePhone = t.AttendeePhone,
                    AttendeeEmail = t.AttendeeEmail,
                    IsPrimaryAttendee = t.IsPrimaryAttendee,
                    Status = t.Status,
                    IssuedAt = t.IssuedAt,
                    CheckedInAt = t.CheckedInAt
                }).ToList(),
                BookingSessions = b.BookingSessions.OrderBy(bs => bs.CreatedAt).Select(bs => new Ethos.Api.Contracts.Workshops.WorkshopBookingSessionDto
                {
                    Id = bs.Id,
                    WorkshopBookingId = bs.WorkshopBookingId,
                    WorkshopSessionId = bs.WorkshopSessionId,
                    WorkshopTicketId = bs.WorkshopTicketId,
                    SessionTitle = bs.WorkshopSession != null ? bs.WorkshopSession.Title : "Session",
                    SessionDate = bs.WorkshopSession != null ? bs.WorkshopSession.SessionDate : DateTime.UtcNow,
                    StartTime = bs.WorkshopSession != null ? bs.WorkshopSession.StartTime : TimeSpan.Zero,
                    EndTime = bs.WorkshopSession != null ? bs.WorkshopSession.EndTime : TimeSpan.Zero,
                    TrainerName = bs.WorkshopSession != null && bs.WorkshopSession.TrainerProfile != null ? bs.WorkshopSession.TrainerProfile.FullName : "Trainer",
                    Status = bs.Status,
                    OriginalSessionId = bs.OriginalSessionId,
                    ReplacedAt = bs.ReplacedAt,
                    CutoffOverrideUsed = bs.CutoffOverrideUsed,
                    OverrideReason = bs.OverrideReason
                }).ToList()
            };
        }).ToList();

        return new PagedResult<AdminWorkshopRegistrationResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    
    public static string DeriveLifecyclePhase(Workshop w, DateTime nowUtc)
    {
        if (w.Status == WorkshopStatus.Cancelled) return "Cancelled";
        if (w.Status == WorkshopStatus.Completed) return "Completed";
        if (w.Status == WorkshopStatus.PendingApproval) return "PendingReview";
        if (w.Status == WorkshopStatus.Rejected) return "Rejected";
        if (w.Status == WorkshopStatus.Draft) return "Draft";
        if (w.Status == WorkshopStatus.Unpublished) return "Unpublished";
        if (w.Status == WorkshopStatus.Archived) return "Archived";
        if (w.Status == WorkshopStatus.Approved) return "Approved";

        if (w.Status == WorkshopStatus.Published)
        {
            var startUtc = w.StartUtc ?? ComputeStartUtc(w);
            var endUtc = w.EndUtc ?? ComputeEndUtc(w);

            if (nowUtc < startUtc) return "Upcoming";
            if (nowUtc >= startUtc && nowUtc < endUtc) return "Ongoing";
            return "Ended";
        }

        return w.Status.ToString();
    }

    public static DateTime ComputeStartUtc(DateTime workshopDate, TimeSpan startTime, string? timezone)
    {
        var tzId = string.IsNullOrWhiteSpace(timezone) ? "Asia/Kolkata" : timezone;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

        var startLocal = DateTime.SpecifyKind(workshopDate.Date + startTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
    }

    public static DateTime ComputeEndUtc(DateTime workshopDate, TimeSpan startTime, TimeSpan endTime, string? timezone)
    {
        var tzId = string.IsNullOrWhiteSpace(timezone) ? "Asia/Kolkata" : timezone;
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }

        var endLocal = DateTime.SpecifyKind(
            endTime > startTime
                ? workshopDate.Date + endTime
                : workshopDate.Date.AddDays(1) + endTime,
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
    }

    public static DateTime ComputeStartUtc(Workshop w)
        => ComputeStartUtc(w.WorkshopDate, w.StartTime, w.Timezone);

    public static DateTime ComputeEndUtc(Workshop w)
        => ComputeEndUtc(w.WorkshopDate, w.StartTime, w.EndTime, w.Timezone);

    private static void EnsureWorkshopEditable(Workshop w)
    {
        var nowUtc = DateTime.UtcNow;
        var startUtc = w.StartUtc ?? ComputeStartUtc(w);
        
        bool hasCheckIns = w.Bookings != null && w.Bookings.Any(b => 
            b.Status == WorkshopBookingStatus.Attended || 
            (b.Tickets != null && b.Tickets.Any(t => t.CheckedInAt != null)));

        if (hasCheckIns || w.Status == WorkshopStatus.Completed)
        {
            throw new InvalidOperationException("WORKSHOP_EDIT_LOCKED: This workshop is active/ongoing and attendee check-ins have occurred. Core details and schedules are permanently locked, even for administrators.");
        }

        if (nowUtc >= startUtc)
        {
            throw new InvalidOperationException("WORKSHOP_ALREADY_STARTED: Workshop has already started or concluded. Modifications, unpublishing, and cancellation are permanently locked.");
        }
    }

    private static TrainerWorkshopResponse Map(Workshop w)
    {
        var effectivePrice = w.AdminApprovedPrice ?? w.TrainerProposedPrice ?? w.Price;
        var nowUtc = DateTime.UtcNow;
        var startUtc = w.StartUtc ?? ComputeStartUtc(w);
        var endUtc = w.EndUtc ?? ComputeEndUtc(w);
        var phase = DeriveLifecyclePhase(w, nowUtc);

        var mappedSessions = w.Sessions?.OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime).Select(s =>
        {
            var sBooked = s.BookingSessions?.Count(bs => bs.Status == WorkshopBookingSessionStatus.Booked &&
                (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                 bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                 (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment && bs.WorkshopBooking.ReservationExpiresAt > nowUtc))) ?? 0;
            var sRemaining = Math.Max(0, s.Capacity - sBooked);
            var isClosed = s.IsBookingClosed(nowUtc, w.Timezone ?? "Asia/Kolkata");
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
                ? s.SessionTrainers.OrderBy(st => st.DisplayOrder).Select(st => new Ethos.Api.Contracts.Workshops.WorkshopTrainerDto
                {
                    TrainerProfileId = st.TrainerProfileId,
                    Name = st.TrainerProfile?.FullName ?? "Ethos Faculty",
                    PhotoUrl = st.TrainerProfile?.ProfilePhotoUrl,
                    DanceStyles = st.TrainerProfile?.PrimaryDanceStyle,
                    DisplayOrder = st.DisplayOrder
                }).ToList()
                : (s.TrainerProfile != null
                    ? new List<Ethos.Api.Contracts.Workshops.WorkshopTrainerDto>
                    {
                        new Ethos.Api.Contracts.Workshops.WorkshopTrainerDto
                        {
                            TrainerProfileId = s.TrainerProfileId,
                            Name = s.TrainerProfile.FullName,
                            PhotoUrl = s.TrainerProfile.ProfilePhotoUrl,
                            DanceStyles = s.TrainerProfile.PrimaryDanceStyle,
                            DisplayOrder = 0
                        }
                    }
                    : new List<Ethos.Api.Contracts.Workshops.WorkshopTrainerDto>());

            return new Ethos.Api.Contracts.Workshops.WorkshopSessionDto
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
                BookingCutoffUtc = s.GetBookingCutoffUtc(w.Timezone ?? "Asia/Kolkata"),
                IsBookingClosed = isClosed,
                DisplayOrder = s.DisplayOrder,
                IsActive = s.IsActive,
                PosterImageUrl = s.PosterImageUrl ?? w.ImageUrl,
                Trainers = sessionTrainers,
                AvailabilityLabel = availLabel
            };
        }).ToList() ?? new();

        return new TrainerWorkshopResponse
        {
            Id = w.Id,
            WorkshopReference = $"WKS-{w.WorkshopDate.Year}-{w.Id.ToString()[..6].ToUpperInvariant()}",
            TrainerProfileId = w.TrainerProfileId ?? Guid.Empty,
            TrainerName = w.TrainerProfile?.FullName ?? "Ethos Trainer",
            TrainerPhotoUrl = w.TrainerProfile?.ProfilePhotoUrl,
            TrainerDanceStyles = w.TrainerProfile?.PrimaryDanceStyle,
            Title = w.Title,
            Description = w.Description,
            DanceStyle = w.DanceStyle,
            Level = w.Level,
            WorkshopDate = w.WorkshopDate,
            StartTime = w.StartTime,
            EndTime = w.EndTime,
            Venue = w.Venue,
            TrainerProposedPrice = w.TrainerProposedPrice,
            AdminApprovedPrice = w.AdminApprovedPrice,
            Price = effectivePrice,
            Capacity = w.Capacity,
            BookedCount = w.Bookings?.Where(b => b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended).Sum(b => b.Quantity) ?? 0,
            AttendedCount = w.Bookings?.Count(b => b.Status == WorkshopBookingStatus.Attended) ?? 0,
            TotalRevenue = w.Bookings?.Where(b => b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended).Sum(b => b.TotalPrice) ?? 0,
            Status = w.Status.ToString(),
            ApprovalStatus = w.Status.ToString(),
            LifecyclePhase = phase,
            ImageUrl = w.ImageUrl,
            LandscapeImageUrl = w.LandscapeImageUrl,
            City = w.City,
            Area = w.Area,
            ShortDescription = w.ShortDescription,
            ContactPerson = w.ContactPerson,
            ContactNumber = w.ContactNumber,
            PublicVisibility = w.PublicVisibility,
            RegistrationType = w.RegistrationType ?? "Standard",
            TermsAndCancellationPolicy = w.TermsAndCancellationPolicy,
            GooglePlaceId = w.GooglePlaceId,
            Latitude = w.Latitude,
            Longitude = w.Longitude,
            VenueAddress = w.VenueAddress,
            LocationUrl = w.LocationUrl,
            Timezone = string.IsNullOrWhiteSpace(w.Timezone) ? "Asia/Kolkata" : w.Timezone,
            StartUtc = startUtc,
            EndUtc = endUtc,
            BookingCutoffTime = w.BookingCutoffTime,
            BookingCutoffUtc = w.GetBookingCutoffUtc(),
            IsBookingClosed = w.IsBookingClosed(nowUtc),
            Trainers = w.WorkshopTrainers?.OrderBy(wt => wt.DisplayOrder).Select(wt => new Ethos.Api.Contracts.Workshops.WorkshopTrainerDto
            {
                TrainerProfileId = wt.TrainerProfileId,
                Name = wt.TrainerProfile?.FullName ?? "Ethos Trainer",
                PhotoUrl = wt.TrainerProfile?.ProfilePhotoUrl,
                DanceStyles = wt.TrainerProfile?.PrimaryDanceStyle,
                DisplayOrder = wt.DisplayOrder
            }).ToList() ?? new(),
            Sessions = mappedSessions,
            PassTypes = w.PassTypes?.OrderBy(p => p.DisplayOrder).Select(p =>
            {
                var pTotal = p.TotalQuantity > 0 ? p.TotalQuantity : 1000;
                var pBooked = w.Bookings?.Where(b => b.WorkshopPassTypeId == p.Id &&
                    (b.Status == WorkshopBookingStatus.Confirmed ||
                     b.Status == WorkshopBookingStatus.Attended ||
                     (b.Status == WorkshopBookingStatus.PendingPayment && b.ReservationExpiresAt > nowUtc)))
                    .Sum(b => b.Quantity) ?? 0;
                var pRemainingQuota = Math.Max(0, pTotal - pBooked);

                Ethos.Api.Contracts.Workshops.WorkshopSessionDto? linkedSessionDto = null;
                if (p.WorkshopSessionId.HasValue)
                {
                    linkedSessionDto = mappedSessions.FirstOrDefault(s => s.Id == p.WorkshopSessionId.Value);
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
                    var activeSessions = mappedSessions.Where(s => !s.IsBookingClosed).ToList();
                    int minRemaining = activeSessions.Count > 0 ? activeSessions.Min(s => s.RemainingSeats) : 0;
                    pRemaining = Math.Min(pRemainingQuota, minRemaining);
                    if (activeSessions.Count == 0 || minRemaining <= 0) isPassClosed = true;
                }
                else
                {
                    int n = p.SessionsIncluded.Value;
                    int availableActiveSessionsCount = mappedSessions.Count(s => !s.IsBookingClosed && s.RemainingSeats >= 1);
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

                var tierDtos = tiers.Select(t => new Ethos.Api.Contracts.Workshops.WorkshopPricingTierDto
                {
                    TierNumber = t.TierNumber,
                    TierName = t.TierName,
                    MinTickets = t.MinTickets,
                    MaxTickets = t.MaxTickets,
                    Price = t.Price,
                    Status = "UPCOMING"
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

                return new Ethos.Api.Contracts.Workshops.WorkshopPassTypeDto
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
            }).ToList() ?? new(),
            CreatedAt = w.CreatedAt
        };
    }

    public async Task<IReadOnlyList<AdminWorkshopTicketResponse>> GetWorkshopTicketsAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var tickets = await _db.WorkshopTickets
            .AsNoTracking()
            .Include(t => t.Attendance)
            .Where(t => t.WorkshopId == workshopId)
            .OrderBy(t => t.TicketNumber)
            .ToListAsync(cancellationToken);

        return tickets.Select(t => new AdminWorkshopTicketResponse
        {
            TicketId = t.Id,
            TicketNumber = t.TicketNumber,
            WorkshopBookingId = t.WorkshopBookingId,
            AttendeeName = t.AttendeeName,
            AttendeePhone = t.AttendeePhone,
            AttendeeEmail = t.AttendeeEmail,
            IsPrimaryAttendee = t.IsPrimaryAttendee,
            IsCheckedIn = t.CheckedInAt.HasValue,
            IsCurrentlyInside = t.Attendance?.IsCurrentlyInside ?? false,
            CheckedInAt = t.CheckedInAt,
            CheckInMethod = t.CheckInMethod?.ToString(),
            Status = t.Status.ToString(),
            IssuedAt = t.IssuedAt
        }).ToList();
    }

    public async Task<bool> AdminCheckInOverrideAsync(
        Guid workshopId,
        Guid ticketId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A mandatory administrative reason must be provided for manual check-in override.");
        }

        var ticket = await _db.WorkshopTickets
            .Include(t => t.WorkshopBooking)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.WorkshopId == workshopId, cancellationToken);

        if (ticket == null)
            throw new ArgumentException("Ticket not found.");

        if (ticket.CheckedInAt.HasValue)
            throw new InvalidOperationException("Ticket is already checked in.");

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            ticket.CheckedInAt = now;
            ticket.CheckedInByUserId = adminUserId;
            ticket.CheckInMethod = CheckInMethod.AdminOverride;
            ticket.AttendeeDetailsLockedAt = now;

            var attendance = new WorkshopAttendance
            {
                Id = Guid.NewGuid(),
                WorkshopTicketId = ticket.Id,
                WorkshopId = workshopId,
                CheckedInByUserId = adminUserId,
                FirstCheckedInAt = now,
                LastCheckedInAt = now,
                IsCurrentlyInside = true,
                Method = CheckInMethod.AdminOverride,
                Notes = $"Admin override: {reason}"
            };
            _db.WorkshopAttendances.Add(attendance);

            var attEvent = new WorkshopAttendanceEvent
            {
                Id = Guid.NewGuid(),
                WorkshopTicketId = ticket.Id,
                WorkshopId = workshopId,
                PerformedByUserId = adminUserId,
                EventType = AttendanceEventType.ManualOverride,
                OccurredAt = now,
                Method = CheckInMethod.AdminOverride,
                Notes = reason
            };
            _db.WorkshopAttendanceEvents.Add(attEvent);

            if (ticket.WorkshopBooking != null && ticket.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed)
            {
                ticket.WorkshopBooking.Status = WorkshopBookingStatus.Attended;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogActionAsync(
                adminUserId,
                "WORKSHOP_TICKET_MANUAL_CHECKIN",
                "WORKSHOPS",
                "WorkshopTicket",
                ticket.Id,
                true,
                "SUCCESS",
                reason,
                cancellationToken: cancellationToken);

            return true;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> AdminUndoCheckInAsync(
        Guid workshopId,
        Guid ticketId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A mandatory administrative reason must be provided to reverse check-in.");
        }

        var ticket = await _db.WorkshopTickets
            .Include(t => t.WorkshopBooking)
            .Include(t => t.Attendance)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.WorkshopId == workshopId, cancellationToken);

        if (ticket == null)
            throw new ArgumentException("Ticket not found.");

        if (!ticket.CheckedInAt.HasValue)
            throw new InvalidOperationException("Ticket is not checked in.");

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            ticket.CheckedInAt = null;
            ticket.CheckInMethod = null;

            if (ticket.Attendance != null)
            {
                ticket.Attendance.IsCurrentlyInside = false;
            }

            var attEvent = new WorkshopAttendanceEvent
            {
                Id = Guid.NewGuid(),
                WorkshopTicketId = ticket.Id,
                WorkshopId = workshopId,
                PerformedByUserId = adminUserId,
                EventType = AttendanceEventType.CheckInReversed,
                OccurredAt = now,
                Method = CheckInMethod.AdminOverride,
                Notes = $"Check-in reversed: {reason}"
            };
            _db.WorkshopAttendanceEvents.Add(attEvent);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            await _auditService.LogActionAsync(
                adminUserId,
                "WORKSHOP_TICKET_CHECKIN_REVERSED",
                "WORKSHOPS",
                "WorkshopTicket",
                ticket.Id,
                true,
                "SUCCESS",
                reason,
                cancellationToken: cancellationToken);

            return true;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<string> ExportWorkshopAttendanceCsvAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var tickets = await _db.WorkshopTickets
            .AsNoTracking()
            .Include(t => t.Attendance)
            .Where(t => t.WorkshopId == workshopId)
            .OrderBy(t => t.TicketNumber)
            .ToListAsync(cancellationToken);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("TicketNumber,AttendeeName,Phone,Email,Status,CheckedIn,CheckedInAt,CheckInMethod,CurrentlyInside");

        foreach (var t in tickets)
        {
            var checkedIn = t.CheckedInAt.HasValue ? "Yes" : "No";
            var checkInTime = t.CheckedInAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            var method = t.CheckInMethod?.ToString() ?? "";
            var inside = (t.Attendance?.IsCurrentlyInside ?? false) ? "Yes" : "No";

            sb.AppendLine($"\"{t.TicketNumber}\",\"{t.AttendeeName}\",\"{t.AttendeePhone ?? ""}\",\"{t.AttendeeEmail ?? ""}\",\"{t.Status}\",\"{checkedIn}\",\"{checkInTime}\",\"{method}\",\"{inside}\"");
        }

        return sb.ToString();
    }

    public async Task PublishWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .Include(w => w.WorkshopTrainers)
            .Include(w => w.Sessions)
                .ThenInclude(s => s.SessionTrainers)
            .Include(w => w.PassTypes)
            .Include(w => w.PricingTiers)
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        ValidateStatusTransition(workshop.Status, WorkshopStatus.Published);

        if (string.IsNullOrWhiteSpace(workshop.Title))
            throw new InvalidOperationException("Workshop title is required to publish.");

        if (workshop.Price <= 0)
            throw new InvalidOperationException("Workshop must have a valid price before publishing.");

        if (string.IsNullOrWhiteSpace(workshop.Description))
            throw new InvalidOperationException("Workshop description is required to publish.");

        if (string.IsNullOrWhiteSpace(workshop.Venue))
            throw new InvalidOperationException("Workshop venue is required to publish.");

        if (string.IsNullOrWhiteSpace(workshop.ImageUrl))
            throw new InvalidOperationException("Workshop main portrait image is required to publish.");

        // Faculty check: either workshop.TrainerProfileId or workshop.WorkshopTrainers
        var facultyTrainerIds = workshop.WorkshopTrainers?.Select(t => t.TrainerProfileId).ToHashSet() ?? new HashSet<Guid>();
        if (workshop.TrainerProfileId.HasValue && workshop.TrainerProfileId.Value != Guid.Empty)
        {
            facultyTrainerIds.Add(workshop.TrainerProfileId.Value);
        }
        if (facultyTrainerIds.Count == 0)
            throw new InvalidOperationException("Workshop must have at least one trainer in its faculty pool to publish.");

        // Sessions check
        var activeSessions = workshop.Sessions?.Where(s => s.IsActive).ToList() ?? new List<WorkshopSession>();
        if (activeSessions.Count == 0)
            throw new InvalidOperationException("Workshop must have at least one active session to publish.");

        // Validate sessions
        foreach (var s in activeSessions)
        {
            if (s.StartTime >= s.EndTime)
                throw new InvalidOperationException($"Session '{s.Title}' has invalid start/end times.");

            if (s.Capacity <= 0)
                throw new InvalidOperationException($"Session '{s.Title}' must have a positive capacity.");

            if (s.SessionTrainers != null && s.SessionTrainers.Any())
            {
                foreach (var st in s.SessionTrainers)
                {
                    if (!facultyTrainerIds.Contains(st.TrainerProfileId))
                    {
                        throw new InvalidOperationException($"Session '{s.Title}' has assigned trainer not present in the workshop faculty pool.");
                    }
                }
            }
        }

        // Interval check for non-overlapping sessions on the same date
        var sessionsByDate = activeSessions.GroupBy(s => s.SessionDate.Date);
        foreach (var dateGroup in sessionsByDate)
        {
            var sorted = dateGroup.OrderBy(s => s.StartTime).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i].EndTime > sorted[i + 1].StartTime)
                {
                    throw new InvalidOperationException($"Sessions '{sorted[i].Title}' and '{sorted[i + 1].Title}' have overlapping schedules on {dateGroup.Key:yyyy-MM-dd}.");
                }
            }
        }

        // Passes check
        var activePasses = workshop.PassTypes?.Where(p => p.IsActive).ToList() ?? new List<WorkshopPassType>();
        if (activePasses.Count == 0)
            throw new InvalidOperationException("Workshop must have at least one active pass type to publish.");

        var activeSessionIds = activeSessions.Select(s => s.Id).ToHashSet();
        foreach (var pass in activePasses)
        {
            if (pass.WorkshopSessionId.HasValue)
            {
                if (!activeSessionIds.Contains(pass.WorkshopSessionId.Value))
                {
                    throw new InvalidOperationException($"Ticket '{pass.Name}' references a session that is not active in the workshop.");
                }
            }
        }

        // Pricing tiers check
        var tiers = workshop.PricingTiers?.OrderBy(t => t.TierNumber).ToList() ?? new List<WorkshopPricingTier>();
        if (tiers.Count == 0)
            throw new InvalidOperationException("Workshop must have at least one pricing tier to publish.");

        for (int i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            if (tier.Price <= 0)
                throw new InvalidOperationException($"Pricing tier {tier.TierNumber} must have a price greater than zero.");

            if (i < tiers.Count - 1)
            {
                if (!tier.MaxTickets.HasValue)
                    throw new InvalidOperationException($"Intermediate pricing tier {tier.TierNumber} must specify MaxTickets.");

                if (tier.MaxTickets.Value < tier.MinTickets)
                    throw new InvalidOperationException($"Pricing tier {tier.TierNumber} MaxTickets cannot be less than MinTickets.");

                if (tiers[i + 1].MinTickets != tier.MaxTickets.Value + 1)
                    throw new InvalidOperationException($"Gap or overlap between pricing tier {tier.TierNumber} and {tiers[i + 1].TierNumber}.");
            }
            else
            {
                // Final tier must be open-ended
                if (tier.MaxTickets.HasValue)
                    throw new InvalidOperationException("Final pricing tier must be open-ended (MaxTickets must be null).");
            }
        }

        workshop.Status = WorkshopStatus.Published;
        workshop.StartUtc ??= ComputeStartUtc(workshop);
        workshop.EndUtc ??= ComputeEndUtc(workshop);
        workshop.AdminApprovedPrice = workshop.Price;
        workshop.PriceApprovedAt = DateTime.UtcNow;
        workshop.PriceApprovedByUserId = adminUserId;
        workshop.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_PUBLISHED",
            "Workshop",
            workshop.Id,
            $"Administrator published workshop '{workshop.Title}'");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnpublishWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        ValidateStatusTransition(workshop.Status, WorkshopStatus.Unpublished);
        EnsureWorkshopEditable(workshop);
        workshop.Status = WorkshopStatus.Unpublished;
        workshop.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_UNPUBLISHED",
            "Workshop",
            workshop.Id,
            $"Administrator unpublished workshop '{workshop.Title}'");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        ValidateStatusTransition(workshop.Status, WorkshopStatus.Archived);
        workshop.Status = WorkshopStatus.Archived;
        workshop.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_ARCHIVED",
            "Workshop",
            workshop.Id,
            $"Administrator archived workshop '{workshop.Title}'");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminWorkshopOverviewResponse> GetWorkshopOverviewAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var w = await _db.Workshops
            .AsNoTracking()
            .Include(ws => ws.TrainerProfile)
            .Include(ws => ws.Bookings)
            .Include(ws => ws.Tickets)
                .ThenInclude(t => t.Attendance)
            .FirstOrDefaultAsync(ws => ws.Id == workshopId, cancellationToken);

        if (w == null)
            throw new ArgumentException("Workshop not found.");

        var tickets = w.Tickets ?? new List<WorkshopTicket>();
        var bookings = w.Bookings ?? new List<WorkshopBooking>();

        var bookedCount = tickets.Count(t => t.Status == TicketStatus.Issued);
        if (bookedCount == 0)
        {
            bookedCount = bookings.Where(b => b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended).Sum(b => b.Quantity);
        }

        var attendedCount = tickets.Count(t => t.CheckedInAt.HasValue);
        var capacity = w.Capacity;
        var capacityPct = capacity > 0 ? Math.Round((double)bookedCount / capacity * 100, 1) : 0;
        var checkInPct = bookedCount > 0 ? Math.Round((double)attendedCount / bookedCount * 100, 1) : 0;

        var totalRevenue = bookings
            .Where(b => b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended)
            .Sum(b => b.TotalPrice);

        var recentCheckIns = tickets
            .Where(t => t.CheckedInAt.HasValue)
            .OrderByDescending(t => t.CheckedInAt)
            .Take(15)
            .Select(t => new AdminWorkshopRecentCheckInDto
            {
                TicketId = t.Id,
                AttendeeName = t.AttendeeName,
                TicketNumber = t.TicketNumber,
                CheckedInAt = t.CheckedInAt!.Value,
                FormattedTime = t.CheckedInAt!.Value.ToString("hh:mm tt"),
                CheckInMethod = t.CheckInMethod?.ToString() ?? "QR"
            })
            .ToList();

        var dateStr = w.WorkshopDate.ToString("ddd, dd MMM yyyy");
        var startStr = DateTime.Today.Add(w.StartTime).ToString("hh:mm tt");
        var endStr = DateTime.Today.Add(w.EndTime).ToString("hh:mm tt");

        return new AdminWorkshopOverviewResponse
        {
            Id = w.Id,
            Title = w.Title,
            WorkshopReference = $"WKS-{w.WorkshopDate.Year}-{w.Id.ToString()[..6].ToUpperInvariant()}",
            DanceStyle = w.DanceStyle,
            Level = w.Level,
            Status = w.Status.ToString(),
            WorkshopDate = w.WorkshopDate,
            StartTime = w.StartTime,
            EndTime = w.EndTime,
            FormattedSchedule = $"{dateStr} · {startStr} - {endStr}",
            Venue = w.Venue,
            LocationUrl = w.LocationUrl,
            Price = w.AdminApprovedPrice ?? w.TrainerProposedPrice ?? w.Price,
            Capacity = w.Capacity,
            BookedCount = bookedCount,
            AttendedCount = attendedCount,
            CapacityPercentage = capacityPct,
            CheckInPercentage = checkInPct,
            TotalRevenue = totalRevenue,
            TrainerName = w.TrainerProfile?.FullName ?? "Ethos Master Trainer",
            ImageUrl = w.ImageUrl,
            Description = w.Description,
            RecentCheckIns = recentCheckIns
        };
    }

    public async Task<AdminCheckInTicketResponse> CheckInWorkshopTicketAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminCheckInTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.QrToken) && string.IsNullOrWhiteSpace(request?.TicketNumber))
        {
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "INVALID_TOKEN",
                Message = "Please scan a valid QR code or provide a ticket number."
            };
        }

        string? tokenHash = null;
        if (!string.IsNullOrWhiteSpace(request.QrToken))
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(request.QrToken.Trim()));
            tokenHash = Convert.ToHexString(bytes).ToLowerInvariant();
        }

        var query = _db.WorkshopTickets
            .Include(t => t.Workshop)
            .Include(t => t.WorkshopBooking)
            .Include(t => t.Attendance)
            .AsQueryable();

        WorkshopTicket? ticket = null;
        if (!string.IsNullOrWhiteSpace(tokenHash))
        {
            ticket = await query.FirstOrDefaultAsync(t => t.QrTokenHash == tokenHash, cancellationToken);
            if (ticket == null && !string.IsNullOrWhiteSpace(request.QrToken))
            {
                var cleanRaw = request.QrToken.Trim().ToUpperInvariant();
                ticket = await query.FirstOrDefaultAsync(t => t.TicketNumber.ToUpper() == cleanRaw, cancellationToken);
                if (ticket == null && Guid.TryParse(request.QrToken.Trim(), out var bookingGuid))
                {
                    ticket = await query.FirstOrDefaultAsync(t => t.WorkshopBookingId == bookingGuid, cancellationToken);
                }

                if (ticket == null && request.QrToken.Trim().StartsWith("ETHOS-TKT-", StringComparison.OrdinalIgnoreCase))
                {
                    var candidateTickets = await query.Take(100).ToListAsync(cancellationToken);
                    if (_ticketService != null)
                    {
                        foreach (var cand in candidateTickets)
                        {
                            if (string.Equals(_ticketService.DeriveQrToken(cand), request.QrToken.Trim(), StringComparison.OrdinalIgnoreCase))
                            {
                                ticket = cand;
                                if (string.IsNullOrWhiteSpace(cand.QrTokenHash) && !string.IsNullOrWhiteSpace(tokenHash))
                                {
                                    cand.QrTokenHash = tokenHash;
                                    await _db.SaveChangesAsync(cancellationToken);
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.TicketNumber))
        {
            var cleanNum = request.TicketNumber.Trim().ToUpperInvariant();
            ticket = await query.FirstOrDefaultAsync(t => t.TicketNumber.ToUpper() == cleanNum, cancellationToken);
            if (ticket == null && Guid.TryParse(request.TicketNumber.Trim(), out var bGuid))
            {
                ticket = await query.FirstOrDefaultAsync(t => t.WorkshopBookingId == bGuid, cancellationToken);
            }
        }

        if (ticket == null)
        {
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "TICKET_NOT_FOUND",
                Message = "Ticket was not found in studio records."
            };
        }

        // CRITICAL WORKSHOP RELATIONSHIP CHECK
        var actualWorkshopId = ticket.WorkshopId != Guid.Empty ? ticket.WorkshopId : ticket.WorkshopBooking?.WorkshopId;
        if (actualWorkshopId != workshopId)
        {
            var otherWorkshop = ticket.Workshop ?? (ticket.WorkshopBooking != null ? await _db.Workshops.FindAsync(new object[] { ticket.WorkshopBooking.WorkshopId }, cancellationToken) : null);
            var otherTitle = otherWorkshop?.Title ?? "Another Workshop";
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "WRONG_WORKSHOP",
                Message = $"This ticket belongs to '{otherTitle}' and cannot be checked in here.",
                WorkshopId = actualWorkshopId,
                WorkshopTitle = otherTitle,
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName
            };
        }

        if (ticket.Status == TicketStatus.Cancelled || ticket.Status == TicketStatus.Refunded)
        {
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "TICKET_CANCELLED",
                Message = $"This ticket is {ticket.Status.ToString().ToLowerInvariant()} and cannot be checked in.",
                WorkshopId = workshopId,
                WorkshopTitle = ticket.Workshop?.Title ?? "Workshop",
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName
            };
        }

        if (ticket.Status == TicketStatus.Replaced)
        {
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "TICKET_REPLACED",
                Message = "This ticket was replaced by the studio administrator and is no longer valid.",
                WorkshopId = workshopId,
                WorkshopTitle = ticket.Workshop?.Title ?? "Workshop",
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName
            };
        }

        if (ticket.WorkshopBooking?.Status == WorkshopBookingStatus.Cancelled)
        {
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "BOOKING_CANCELLED",
                Message = "The booking for this ticket has been cancelled.",
                WorkshopId = workshopId,
                WorkshopTitle = ticket.Workshop?.Title ?? "Workshop",
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName
            };
        }

        if (ticket.WorkshopBooking != null && ticket.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment)
        {
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "PAYMENT_NOT_CONFIRMED",
                Message = "Payment has not been confirmed for this booking.",
                WorkshopId = workshopId,
                WorkshopTitle = ticket.Workshop?.Title ?? "Workshop",
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName
            };
        }

        var nowUtc = DateTime.UtcNow;
        var startUtc = ticket.Workshop?.StartUtc ?? ComputeStartUtc(ticket.Workshop ?? new Workshop { WorkshopDate = DateTime.UtcNow });
        var endUtc = ticket.Workshop?.EndUtc ?? ComputeEndUtc(ticket.Workshop ?? new Workshop { WorkshopDate = DateTime.UtcNow });
        var checkInOpen = startUtc.AddMinutes(-30);

        if (nowUtc < checkInOpen)
        {
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "CHECKIN_NOT_OPEN",
                Message = $"Check-in opens 30 minutes before the workshop starts (at {checkInOpen:hh:mm tt} UTC)."
            };
        }

        if (nowUtc > endUtc)
        {
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "WORKSHOP_ENDED",
                Message = "This workshop has concluded. New check-ins are closed."
            };
        }

        if (ticket.CheckedInAt.HasValue || ticket.Attendance != null)
        {
            var timeStr = ticket.CheckedInAt?.ToString("hh:mm tt") ?? "earlier";
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "ALREADY_CHECKED_IN",
                Message = $"This ticket was already checked in at {timeStr}.",
                WorkshopId = workshopId,
                TicketId = ticket.Id,
                AttendeeName = ticket.AttendeeName,
                TicketNumber = ticket.TicketNumber,
                CheckedInAt = ticket.CheckedInAt
            };
        }

        if (ticket.Workshop != null && (ticket.Workshop.Status == WorkshopStatus.Cancelled || ticket.Workshop.Status == WorkshopStatus.Archived))
        {
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "WORKSHOP_NOT_OPEN",
                Message = $"This workshop is {ticket.Workshop.Status.ToString().ToLowerInvariant()} and closed for check-ins.",
                WorkshopId = workshopId,
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber
            };
        }

        // Concurrency-safe check-in transaction
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var dbTicket = await _db.WorkshopTickets
                .FirstOrDefaultAsync(t => t.Id == ticket.Id, cancellationToken);

            if (dbTicket == null || dbTicket.CheckedInAt.HasValue)
            {
                await tx.RollbackAsync(cancellationToken);
                return new AdminCheckInTicketResponse
                {
                    Success = false,
                    Code = "ALREADY_CHECKED_IN",
                    Message = "Ticket was already checked in by another session.",
                    WorkshopId = workshopId,
                    TicketId = ticket.Id,
                    AttendeeName = ticket.AttendeeName,
                    TicketNumber = ticket.TicketNumber
                };
            }

            var now = DateTime.UtcNow;
            var method = !string.IsNullOrWhiteSpace(request.QrToken) ? CheckInMethod.QrScan : CheckInMethod.AdminOverride;

            dbTicket.CheckedInAt = now;
            dbTicket.CheckedInByUserId = adminUserId;
            dbTicket.CheckInMethod = method;
            dbTicket.AttendeeDetailsLockedAt = now;

            var existingAttendance = await _db.WorkshopAttendances
                .FirstOrDefaultAsync(a => a.WorkshopTicketId == ticket.Id, cancellationToken);

            if (existingAttendance == null)
            {
                var attendance = new WorkshopAttendance
                {
                    Id = Guid.NewGuid(),
                    WorkshopTicketId = ticket.Id,
                    WorkshopId = workshopId,
                    CheckedInByUserId = adminUserId,
                    FirstCheckedInAt = now,
                    LastCheckedInAt = now,
                    IsCurrentlyInside = true,
                    Method = method,
                    Notes = request.Notes ?? (method == CheckInMethod.QrScan ? "Scanned via QR Check-In" : "Manual Admin Check-In")
                };
                _db.WorkshopAttendances.Add(attendance);
            }
            else
            {
                existingAttendance.LastCheckedInAt = now;
                existingAttendance.IsCurrentlyInside = true;
            }

            if (ticket.WorkshopBooking != null && ticket.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed)
            {
                ticket.WorkshopBooking.Status = WorkshopBookingStatus.Attended;
            }

            _auditService.AddAuditLog(
                adminUserId,
                "WORKSHOP_TICKET_CHECKED_IN",
                "WorkshopTicket",
                ticket.Id,
                $"Ticket {ticket.TicketNumber} checked in for workshop {workshopId} via {method}.");

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new AdminCheckInTicketResponse
            {
                Success = true,
                Code = "SUCCESS",
                Message = "Check-in successful",
                WorkshopId = workshopId,
                WorkshopTitle = ticket.Workshop?.Title ?? "Workshop",
                TicketId = ticket.Id,
                AttendeeName = ticket.AttendeeName,
                TicketNumber = ticket.TicketNumber,
                CheckedInAt = now,
                CheckInMethod = method.ToString()
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "CHECKIN_FAILED",
                Message = $"Check-in failed: {ex.Message}"
            };
        }
    }

    public async Task<IReadOnlyList<AdminWorkshopAttendeeDto>> GetWorkshopAttendeesAsync(
        Guid workshopId,
        string? filter,
        string? search,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        var query = _db.WorkshopTickets
            .AsNoTracking()
            .Include(t => t.WorkshopBooking)
                .ThenInclude(b => b.WorkshopPassType)
            .Include(t => t.WorkshopSession)
            .Include(t => t.Attendance)
            .Where(t => t.WorkshopId == workshopId || t.WorkshopBooking.WorkshopId == workshopId)
            .AsQueryable();

        var f = filter?.Trim().ToLower();

        // Active vs Historical roster: exclude Replaced/Cancelled/Refunded by default unless historical explicitly requested
        if (f != "all_historical" && f != "historical")
        {
            query = query.Where(t => t.Status != TicketStatus.Replaced && t.Status != TicketStatus.Cancelled && t.Status != TicketStatus.Refunded);
        }

        if (sessionId.HasValue && sessionId.Value != Guid.Empty)
        {
            query = query.Where(t => t.WorkshopSessionId == sessionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(f))
        {
            if (f == "checked_in" || f == "present")
                query = query.Where(t => t.CheckedInAt.HasValue);
            else if (f == "not_checked_in" || f == "absent")
                query = query.Where(t => !t.CheckedInAt.HasValue);
            else if (f == "guests")
                query = query.Where(t => !t.IsPrimaryAttendee || t.WorkshopBooking.GuestName != null);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(t =>
                t.AttendeeName.ToLower().Contains(s) ||
                t.TicketNumber.ToLower().Contains(s));
        }

        var tickets = await query
            .OrderBy(t => t.TicketNumber)
            .ToListAsync(cancellationToken);

        return tickets.Select(t =>
        {
            var isGuest = !t.IsPrimaryAttendee || t.WorkshopBooking?.GuestName != null;
            var isCheckedIn = t.CheckedInAt.HasValue;

            string phoneMasked = "—";
            if (!string.IsNullOrWhiteSpace(t.AttendeePhone))
            {
                var clean = t.AttendeePhone.Trim();
                phoneMasked = clean.Length > 6 ? $"{clean[..3]}••••{clean[^3..]}" : clean;
            }

            string? emailMasked = null;
            if (!string.IsNullOrWhiteSpace(t.AttendeeEmail))
            {
                var parts = t.AttendeeEmail.Split('@');
                if (parts.Length == 2 && parts[0].Length > 2)
                    emailMasked = $"{parts[0][..2]}***@{parts[1]}";
                else
                    emailMasked = t.AttendeeEmail;
            }

            var payStatus = (t.WorkshopBooking?.PaymentTransactionId.HasValue == true ||
                             t.WorkshopBooking?.Status == WorkshopBookingStatus.Confirmed ||
                             t.WorkshopBooking?.Status == WorkshopBookingStatus.Attended)
                ? "Paid"
                : (t.WorkshopBooking?.Status == WorkshopBookingStatus.PendingPayment ? "Pending" : "Free/Unpaid");

            return new AdminWorkshopAttendeeDto
            {
                TicketId = t.Id,
                BookingId = t.WorkshopBookingId,
                AttendeeName = t.AttendeeName,
                AttendeePhoneMasked = phoneMasked,
                AttendeeEmailMasked = emailMasked,
                TicketNumber = t.TicketNumber,
                BookingReference = $"BK-{t.WorkshopBookingId.ToString()[..8].ToUpperInvariant()}",
                BookingStatus = t.WorkshopBooking?.Status.ToString() ?? "Confirmed",
                PaymentStatus = payStatus,
                IsCheckedIn = isCheckedIn,
                CheckedInAt = t.CheckedInAt,
                FormattedCheckedInAt = t.CheckedInAt?.ToString("hh:mm tt"),
                CheckInMethod = t.CheckInMethod?.ToString(),
                IsGuest = isGuest,
                AttendeeType = isGuest ? "Workshop Guest" : "ETHOS Student",
                WorkshopSessionId = t.WorkshopSessionId,
                SessionTitle = t.WorkshopSession?.Title,
                SessionDate = t.WorkshopSession?.SessionDate,
                SessionStartTime = t.WorkshopSession?.StartTime,
                SessionEndTime = t.WorkshopSession?.EndTime,
                PassName = t.WorkshopBooking?.PassName ?? t.WorkshopBooking?.WorkshopPassType?.Name,
                PassCategory = t.WorkshopBooking?.WorkshopPassType?.GetPassCategory()
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<AdminWorkshopFeedbackDto>> GetWorkshopFeedbackAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var feedbacks = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Include(f => f.StudentProfile)
                .ThenInclude(sp => sp!.User)
            .Include(f => f.WorkshopBooking)
            .Where(f => (f.WorkshopId == workshopId || (f.WorkshopBooking != null && f.WorkshopBooking.WorkshopId == workshopId)) && f.IsValid)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(cancellationToken);

        return feedbacks.Select(f =>
        {
            var rawName = f.StudentProfile?.User?.FullName;
            string masked = "Verified Attendee";
            if (!string.IsNullOrWhiteSpace(rawName))
            {
                var parts = rawName.Trim().Split(' ');
                masked = parts.Length > 1 ? $"{parts[0]} {parts[1][0]}." : parts[0];
            }

            return new AdminWorkshopFeedbackDto
            {
                Id = f.Id,
                Rating = f.Rating ?? 0,
                Comment = f.Comment,
                StudentNameMasked = masked,
                SubmittedAt = f.SubmittedAt,
                FormattedDate = f.SubmittedAt.ToString("dd MMM yyyy, hh:mm tt")
            };
        }).ToList();
    }

    private async Task<WorkshopFeedbackSetting> EnsureWorkshopFeedbackSettingAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var setting = await _db.WorkshopFeedbackSettings
            .Include(s => s.ActiveVersion)
                .ThenInclude(v => v!.Questions)
            .Include(s => s.FormVersions)
                .ThenInclude(v => v.Questions)
            .FirstOrDefaultAsync(s => s.WorkshopId == workshopId, cancellationToken);

        if (setting == null)
        {
            var now = DateTime.UtcNow;
            setting = new WorkshopFeedbackSetting
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshopId,
                IsFeedbackEnabled = true,
                IsLocked = false,
                CreatedAtUtc = now
            };

            var defaultVersion = new FeedbackFormVersion
            {
                Id = Guid.NewGuid(),
                WorkshopFeedbackSettingId = setting.Id,
                VersionNumber = 1,
                IsFrozen = false,
                CreatedAtUtc = now
            };

            var defaultQuestions = new List<FeedbackQuestion>
            {
                new FeedbackQuestion
                {
                    Id = Guid.NewGuid(),
                    FeedbackFormVersionId = defaultVersion.Id,
                    QuestionKey = "overall_rating",
                    PromptText = "Overall Masterclass Rating",
                    QuestionType = FeedbackQuestionType.Rating1To5,
                    TargetAudience = FeedbackAudienceType.Both,
                    IsRequired = true,
                    SortOrder = 1
                },
                new FeedbackQuestion
                {
                    Id = Guid.NewGuid(),
                    FeedbackFormVersionId = defaultVersion.Id,
                    QuestionKey = "instruction_rating",
                    PromptText = "How would you rate the choreography & instruction?",
                    QuestionType = FeedbackQuestionType.Rating1To5,
                    TargetAudience = FeedbackAudienceType.Attended,
                    IsRequired = false,
                    SortOrder = 2
                },
                new FeedbackQuestion
                {
                    Id = Guid.NewGuid(),
                    FeedbackFormVersionId = defaultVersion.Id,
                    QuestionKey = "feedback_comment",
                    PromptText = "What did you enjoy most or what could we improve?",
                    QuestionType = FeedbackQuestionType.Text,
                    TargetAudience = FeedbackAudienceType.Attended,
                    IsRequired = false,
                    SortOrder = 3
                },
                new FeedbackQuestion
                {
                    Id = Guid.NewGuid(),
                    FeedbackFormVersionId = defaultVersion.Id,
                    QuestionKey = "noshow_reason",
                    PromptText = "We missed you! What was the main reason you could not attend?",
                    QuestionType = FeedbackQuestionType.SingleChoice,
                    TargetAudience = FeedbackAudienceType.NoShow,
                    OptionsJson = JsonSerializer.Serialize(new[] { "Schedule Conflict", "Health / Feeling Unwell", "Travel / Transportation Delay", "Emergency", "Other" }),
                    IsRequired = false,
                    SortOrder = 4
                }
            };

            foreach (var q in defaultQuestions)
            {
                defaultVersion.Questions.Add(q);
            }

            setting.FormVersions.Add(defaultVersion);
            setting.ActiveVersionId = defaultVersion.Id;
            setting.ActiveVersion = defaultVersion;

            _db.WorkshopFeedbackSettings.Add(setting);
            _db.FeedbackFormVersions.Add(defaultVersion);
            _db.FeedbackQuestions.AddRange(defaultQuestions);

            await _db.SaveChangesAsync(cancellationToken);
        }

        return setting;
    }

    public async Task<AdminWorkshopFeedbackConfigResponse> GetWorkshopFeedbackConfigAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .AsNoTracking()
            .Include(w => w.Bookings)
                .ThenInclude(b => b.Tickets)
                    .ThenInclude(t => t.Attendance)
            .Include(w => w.Bookings)
                .ThenInclude(b => b.StudentProfile)
                    .ThenInclude(sp => sp!.User)
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
        {
            throw new KeyNotFoundException($"Workshop {workshopId} not found.");
        }

        var setting = await EnsureWorkshopFeedbackSettingAsync(workshopId, cancellationToken);

        var feedbacks = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(f => (f.WorkshopId == workshopId || (f.WorkshopBooking != null && f.WorkshopBooking.WorkshopId == workshopId)) && f.IsValid)
            .ToListAsync(cancellationToken);

        var bookingIds = workshop.Bookings.Select(b => b.Id).ToList();

        var refundedBookingIds = await _db.PaymentRefunds
            .AsNoTracking()
            .Where(r => bookingIds.Contains(r.BookingId) &&
                        (r.Status == RefundStatus.Processed ||
                         r.Status == RefundStatus.Requested ||
                         r.Status == RefundStatus.Processing))
            .Select(r => r.BookingId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var notifications = await _db.WhatsAppNotifications
            .AsNoTracking()
            .Where(n => bookingIds.Contains(n.BookingId))
            .ToListAsync(cancellationToken);

        var existingTokens = await _db.WorkshopFeedbackTokens
            .AsNoTracking()
            .Where(t => bookingIds.Contains(t.WorkshopBookingId))
            .ToListAsync(cancellationToken);

        var activeVersion = setting.ActiveVersion ?? setting.FormVersions.FirstOrDefault(v => v.Id == setting.ActiveVersionId) ?? setting.FormVersions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        var activeQuestionDtos = new List<FeedbackQuestionDto>();
        if (activeVersion?.Questions != null)
        {
            foreach (var q in activeVersion.Questions.OrderBy(x => x.SortOrder))
            {
                var choices = new List<string>();
                if (!string.IsNullOrWhiteSpace(q.OptionsJson))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<string>>(q.OptionsJson);
                        if (parsed != null) choices.AddRange(parsed);
                    }
                    catch
                    {
                        choices.AddRange(q.OptionsJson.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    }
                }

                activeQuestionDtos.Add(new FeedbackQuestionDto
                {
                    Id = q.Id,
                    QuestionKey = q.QuestionKey,
                    PromptText = q.PromptText,
                    QuestionType = q.QuestionType,
                    TargetAudience = q.TargetAudience,
                    OptionsJson = q.OptionsJson,
                    Choices = choices,
                    IsRequired = q.IsRequired,
                    SortOrder = q.SortOrder
                });
            }
        }

        var versionDtos = setting.FormVersions.OrderByDescending(v => v.VersionNumber).Select(v =>
        {
            var qDtos = v.Questions.OrderBy(q => q.SortOrder).Select(q =>
            {
                var choices = new List<string>();
                if (!string.IsNullOrWhiteSpace(q.OptionsJson))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<string>>(q.OptionsJson);
                        if (parsed != null) choices.AddRange(parsed);
                    }
                    catch
                    {
                        choices.AddRange(q.OptionsJson.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    }
                }

                return new FeedbackQuestionDto
                {
                    Id = q.Id,
                    QuestionKey = q.QuestionKey,
                    PromptText = q.PromptText,
                    QuestionType = q.QuestionType,
                    TargetAudience = q.TargetAudience,
                    OptionsJson = q.OptionsJson,
                    Choices = choices,
                    IsRequired = q.IsRequired,
                    SortOrder = q.SortOrder
                };
            }).ToList();

            return new AdminFeedbackFormVersionDto
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                IsFrozen = v.IsFrozen,
                FrozenAtUtc = v.FrozenAtUtc,
                CreatedAtUtc = v.CreatedAtUtc,
                QuestionCount = v.Questions.Count,
                IsActive = v.Id == setting.ActiveVersionId,
                Questions = qDtos
            };
        }).ToList();

        int eligibleAttended = 0;
        int eligibleNoShow = 0;
        var recipients = new List<AdminFeedbackRecipientDto>();

        var rawSecret = _configuration?["TicketSecurity:SecretKey"] ?? "EthosWorkshopTicket2026MasterSecretKey!";

        foreach (var b in workshop.Bookings)
        {
            if (b.Status == WorkshopBookingStatus.Cancelled || b.CancelledAt.HasValue) continue;
            if (b.Status == WorkshopBookingStatus.PendingPayment) continue;
            if (refundedBookingIds.Contains(b.Id)) continue;
            if (b.Tickets.Count > 0 && b.Tickets.All(t => t.Status == TicketStatus.Cancelled || t.Status == TicketStatus.Refunded)) continue;

            var hasCheckIn = b.Tickets.Any(t =>
                t.Attendance != null ||
                t.CheckedInAt.HasValue ||
                (t.Status == TicketStatus.Issued && t.CheckedInAt.HasValue));

            var aud = hasCheckIn ? "Attended" : "NoShow";
            if (hasCheckIn) eligibleAttended++;
            else eligibleNoShow++;

            var fb = feedbacks.FirstOrDefault(f => f.WorkshopBookingId == b.Id);
            var notif = notifications.FirstOrDefault(n => n.BookingId == b.Id);
            var token = existingTokens.FirstOrDefault(t => t.WorkshopBookingId == b.Id);

            string deliveryStatus = "NotQueued";
            if (fb != null) deliveryStatus = "Submitted";
            else if (notif != null)
            {
                deliveryStatus = notif.Status switch
                {
                    WhatsAppNotificationStatus.Pending => "Queued",
                    WhatsAppNotificationStatus.Sent => "Sent",
                    WhatsAppNotificationStatus.Failed => "Failed",
                    _ => notif.Status.ToString()
                };
            }

            var name = b.GuestName ?? b.StudentProfile?.User?.FullName ?? "Attendee";
            var phone = b.GuestPhone ?? b.StudentProfile?.User?.Phone ?? b.Tickets.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.AttendeePhone))?.AttendeePhone;

            string? feedbackUrl = null;
            try
            {
                var rawToken = FeedbackTokenHelper.DeriveRawToken(b.Id, rawSecret);
                feedbackUrl = $"https://ethosdancestudio.com/feedback/workshop/{rawToken}";
            }
            catch { }

            recipients.Add(new AdminFeedbackRecipientDto
            {
                BookingId = b.Id,
                BookingReference = b.Id.ToString()[..8].ToUpperInvariant(),
                AttendeeName = name,
                AttendeePhone = phone,
                AudienceType = aud,
                DeliveryStatus = deliveryStatus,
                SentAtUtc = notif?.SentAt,
                SubmittedAtUtc = fb?.SubmittedAt,
                SubmittedRating = fb?.Rating,
                FeedbackUrl = feedbackUrl
            });
        }

        var totalEligible = eligibleAttended + eligibleNoShow;
        var submittedCount = feedbacks.Count;
        var responseRate = totalEligible > 0 ? Math.Round((decimal)submittedCount / totalEligible * 100m, 1) : 0m;
        var avgRating = submittedCount > 0 ? Math.Round((decimal)feedbacks.Average(f => f.Rating ?? 5), 1) : 0m;

        var metrics = new AdminFeedbackMetricsDto
        {
            TotalBookings = totalEligible,
            EligibleAttendedCount = eligibleAttended,
            EligibleNoShowCount = eligibleNoShow,
            SubmittedCount = submittedCount,
            ResponseRate = responseRate,
            AverageRating = avgRating,
            FiveStars = feedbacks.Count(f => (f.Rating ?? 0) == 5),
            FourStars = feedbacks.Count(f => (f.Rating ?? 0) == 4),
            ThreeStars = feedbacks.Count(f => (f.Rating ?? 0) == 3),
            TwoStars = feedbacks.Count(f => (f.Rating ?? 0) == 2),
            OneStar = feedbacks.Count(f => (f.Rating ?? 0) == 1)
        };

        var automation = new AdminFeedbackAutomationDto
        {
            AttendedTemplateName = "ethos_feedback_attended",
            NoShowTemplateName = "ethos_feedback_noshow",
            TriggerCondition = "Workshop Concludes",
            PostEventDelayMinutes = 120,
            IsAutomationActive = setting.IsFeedbackEnabled,
            QueuedNotificationsCount = notifications.Count(n => n.Status == WhatsAppNotificationStatus.Pending),
            SentNotificationsCount = notifications.Count(n => n.Status == WhatsAppNotificationStatus.Sent),
            FailedNotificationsCount = notifications.Count(n => n.Status == WhatsAppNotificationStatus.Failed)
        };

        return new AdminWorkshopFeedbackConfigResponse
        {
            WorkshopId = workshop.Id,
            WorkshopTitle = workshop.Title,
            IsFeedbackEnabled = setting.IsFeedbackEnabled,
            ConfigCutoffUtc = setting.ConfigCutoffUtc,
            IsLocked = setting.IsLocked,
            LockedAtUtc = setting.LockedAtUtc,
            ActiveVersionId = setting.ActiveVersionId,
            ActiveVersionNumber = activeVersion?.VersionNumber ?? 1,
            ActiveVersionIsFrozen = activeVersion?.IsFrozen ?? false,
            ActiveQuestions = activeQuestionDtos,
            Versions = versionDtos,
            Metrics = metrics,
            Automation = automation,
            Recipients = recipients
        };
    }

    public async Task<AdminWorkshopFeedbackConfigResponse> SaveFeedbackVersionAsync(
        Guid workshopId,
        AdminSaveFeedbackVersionRequest request,
        CancellationToken cancellationToken)
    {
        var setting = await EnsureWorkshopFeedbackSettingAsync(workshopId, cancellationToken);
        var now = DateTime.UtcNow;

        var activeVersion = setting.ActiveVersion ?? setting.FormVersions.FirstOrDefault(v => v.Id == setting.ActiveVersionId);

        var hasSubmissionsOnActive = activeVersion != null && await _db.WorkshopFeedbacks
            .AnyAsync(f => f.FeedbackFormVersionId == activeVersion.Id && f.IsValid, cancellationToken);

        var canEditInPlace = activeVersion != null && !activeVersion.IsFrozen && !setting.IsLocked && !hasSubmissionsOnActive;

        FeedbackFormVersion targetVersion;

        if (canEditInPlace && activeVersion != null)
        {
            targetVersion = activeVersion;
            _db.FeedbackQuestions.RemoveRange(targetVersion.Questions);
            targetVersion.Questions.Clear();
        }
        else
        {
            var maxVersionNumber = setting.FormVersions.Count > 0 ? setting.FormVersions.Max(v => v.VersionNumber) : 0;
            targetVersion = new FeedbackFormVersion
            {
                Id = Guid.NewGuid(),
                WorkshopFeedbackSettingId = setting.Id,
                VersionNumber = maxVersionNumber + 1,
                IsFrozen = false,
                CreatedAtUtc = now
            };

            _db.FeedbackFormVersions.Add(targetVersion);
            setting.FormVersions.Add(targetVersion);
            setting.ActiveVersionId = targetVersion.Id;
            setting.ActiveVersion = targetVersion;
        }

        int sortOrder = 1;
        foreach (var q in request.Questions)
        {
            var optionsJson = q.OptionsJson;
            if (q.Choices != null && q.Choices.Count > 0)
            {
                optionsJson = JsonSerializer.Serialize(q.Choices);
            }

            var questionEntity = new FeedbackQuestion
            {
                Id = Guid.NewGuid(),
                FeedbackFormVersionId = targetVersion.Id,
                QuestionKey = string.IsNullOrWhiteSpace(q.QuestionKey) ? $"q_{sortOrder}_{Guid.NewGuid().ToString()[..6]}" : q.QuestionKey.Trim(),
                PromptText = q.PromptText.Trim(),
                QuestionType = q.QuestionType,
                TargetAudience = q.TargetAudience,
                OptionsJson = optionsJson,
                IsRequired = q.IsRequired,
                SortOrder = sortOrder++
            };

            targetVersion.Questions.Add(questionEntity);
            _db.FeedbackQuestions.Add(questionEntity);
        }

        setting.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetWorkshopFeedbackConfigAsync(workshopId, cancellationToken);
    }

    public async Task<bool> ActivateFeedbackVersionAsync(
        Guid workshopId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var setting = await EnsureWorkshopFeedbackSettingAsync(workshopId, cancellationToken);
        var version = setting.FormVersions.FirstOrDefault(v => v.Id == versionId);
        if (version == null)
        {
            throw new KeyNotFoundException($"Version {versionId} not found for Workshop {workshopId}.");
        }

        setting.ActiveVersionId = versionId;
        setting.ActiveVersion = version;
        setting.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateFeedbackSettingAsync(
        Guid workshopId,
        AdminUpdateFeedbackSettingRequest request,
        CancellationToken cancellationToken)
    {
        var setting = await EnsureWorkshopFeedbackSettingAsync(workshopId, cancellationToken);
        setting.IsFeedbackEnabled = request.IsFeedbackEnabled;
        if (request.ConfigCutoffUtc.HasValue)
        {
            setting.ConfigCutoffUtc = request.ConfigCutoffUtc.Value;
        }
        setting.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminResendFeedbackResponse> ResendWorkshopFeedbackAsync(
        Guid workshopId,
        AdminResendFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .Include(w => w.Bookings)
                .ThenInclude(b => b.Tickets)
                    .ThenInclude(t => t.Attendance)
            .Include(w => w.Bookings)
                .ThenInclude(b => b.StudentProfile)
                    .ThenInclude(sp => sp!.User)
            .Include(w => w.FeedbackSetting)
                .ThenInclude(fs => fs!.ActiveVersion)
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
        {
            throw new KeyNotFoundException($"Workshop {workshopId} not found.");
        }

        var setting = await EnsureWorkshopFeedbackSettingAsync(workshopId, cancellationToken);
        var now = DateTime.UtcNow;

        var bookingIds = workshop.Bookings.Select(b => b.Id).ToList();

        var refundedBookingIds = await _db.PaymentRefunds
            .Where(r => bookingIds.Contains(r.BookingId) &&
                        (r.Status == RefundStatus.Processed ||
                         r.Status == RefundStatus.Requested ||
                         r.Status == RefundStatus.Processing))
            .Select(r => r.BookingId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingSubmittedFeedbackBookingIds = await _db.WorkshopFeedbacks
            .Where(f => f.WorkshopBookingId.HasValue &&
                        bookingIds.Contains(f.WorkshopBookingId.Value) &&
                        f.IsValid)
            .Select(f => f.WorkshopBookingId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingTokens = await _db.WorkshopFeedbackTokens
            .Where(t => bookingIds.Contains(t.WorkshopBookingId))
            .ToListAsync(cancellationToken);

        var rawSecret = _configuration?["TicketSecurity:SecretKey"] ?? "EthosWorkshopTicket2026MasterSecretKey!";

        int queuedCount = 0;
        int skippedCount = 0;

        foreach (var booking in workshop.Bookings)
        {
            if (request.RecipientBookingIds != null && request.RecipientBookingIds.Count > 0)
            {
                if (!request.RecipientBookingIds.Contains(booking.Id)) continue;
            }

            if (booking.Status is WorkshopBookingStatus.Cancelled or WorkshopBookingStatus.PendingPayment ||
                booking.CancelledAt.HasValue ||
                refundedBookingIds.Contains(booking.Id) ||
                existingSubmittedFeedbackBookingIds.Contains(booking.Id))
            {
                skippedCount++;
                continue;
            }

            var hasCheckIn = booking.Tickets.Any(t =>
                t.Attendance != null ||
                t.CheckedInAt.HasValue ||
                (t.Status == TicketStatus.Issued && t.CheckedInAt.HasValue));

            var aud = hasCheckIn ? FeedbackAudienceType.Attended : FeedbackAudienceType.NoShow;

            if (string.Equals(request.Audience, "Attended", StringComparison.OrdinalIgnoreCase) && aud != FeedbackAudienceType.Attended)
            {
                skippedCount++;
                continue;
            }
            if (string.Equals(request.Audience, "NoShow", StringComparison.OrdinalIgnoreCase) && aud != FeedbackAudienceType.NoShow)
            {
                skippedCount++;
                continue;
            }

            var recipientPhone = booking.GuestPhone
                                 ?? booking.StudentProfile?.User?.Phone
                                 ?? booking.Tickets.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.AttendeePhone))?.AttendeePhone;

            if (string.IsNullOrWhiteSpace(recipientPhone))
            {
                skippedCount++;
                continue;
            }

            var tokenEntity = existingTokens.FirstOrDefault(t => t.WorkshopBookingId == booking.Id);
            if (tokenEntity == null)
            {
                var rawToken = FeedbackTokenHelper.DeriveRawToken(booking.Id, rawSecret);
                var tokenHash = FeedbackTokenHelper.HashToken(rawToken);

                tokenEntity = new WorkshopFeedbackToken
                {
                    Id = Guid.NewGuid(),
                    WorkshopBookingId = booking.Id,
                    TokenHash = tokenHash,
                    AudienceType = aud,
                    FeedbackFormVersionId = setting.ActiveVersionId,
                    ExpiresAt = now.AddDays(7),
                    UsedAt = null,
                    CreatedAt = now
                };

                _db.WorkshopFeedbackTokens.Add(tokenEntity);
                existingTokens.Add(tokenEntity);
            }
            else if (tokenEntity.ExpiresAt < now)
            {
                tokenEntity.ExpiresAt = now.AddDays(7);
            }

            var notifType = aud == FeedbackAudienceType.Attended
                ? WhatsAppNotificationType.FeedbackAttended
                : WhatsAppNotificationType.FeedbackNoShow;

            var primaryTicket = booking.Tickets.FirstOrDefault(t => t.IsPrimaryAttendee) ?? booking.Tickets.FirstOrDefault();
            var resendIdempotencyKey = $"feedback:resend:{workshop.Id}:{booking.Id}:{aud}:{DateTime.UtcNow.Ticks}";

            var notification = new WhatsAppNotification
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                WorkshopTicketId = primaryTicket?.Id,
                NotificationType = notifType,
                RecipientPhone = recipientPhone.Trim(),
                IdempotencyKey = resendIdempotencyKey,
                Status = WhatsAppNotificationStatus.Pending,
                CreatedAt = now
            };

            _db.WhatsAppNotifications.Add(notification);
            queuedCount++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new AdminResendFeedbackResponse
        {
            QueuedCount = queuedCount,
            SkippedCount = skippedCount,
            Message = $"Successfully queued {queuedCount} WhatsApp feedback {(queuedCount == 1 ? "request" : "requests")}."
        };
    }

    public async Task<AdminWorkshopFeedbackAnalyticsResponse> GetWorkshopFeedbackAnalyticsAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
        {
            throw new KeyNotFoundException($"Workshop {workshopId} not found.");
        }

        var feedbacks = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Include(f => f.StudentProfile)
                .ThenInclude(sp => sp!.User)
            .Include(f => f.WorkshopBooking)
            .Include(f => f.Answers)
                .ThenInclude(a => a.FeedbackQuestion)
            .Where(f => (f.WorkshopId == workshopId || (f.WorkshopBooking != null && f.WorkshopBooking.WorkshopId == workshopId)) && f.IsValid)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(cancellationToken);

        var setting = await EnsureWorkshopFeedbackSettingAsync(workshopId, cancellationToken);
        var allQuestions = setting.FormVersions.SelectMany(v => v.Questions).DistinctBy(q => q.Id).ToList();
        var questionAnalyticsList = new List<AdminQuestionAnalyticsDto>();

        foreach (var q in allQuestions)
        {
            var answers = feedbacks.SelectMany(f => f.Answers).Where(a => a.FeedbackQuestionId == q.Id).ToList();

            decimal? avgScore = null;
            Dictionary<string, int>? choiceCounts = null;
            List<string>? textAnswers = null;

            if (q.QuestionType == FeedbackQuestionType.Rating1To5)
            {
                var ratedAnswers = answers.Where(a => a.NumericValue.HasValue).Select(a => a.NumericValue!.Value).ToList();
                if (ratedAnswers.Count > 0)
                {
                    avgScore = Math.Round((decimal)ratedAnswers.Average(), 1);
                }
            }
            else if (q.QuestionType is FeedbackQuestionType.SingleChoice or FeedbackQuestionType.MultiChoice)
            {
                choiceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var ans in answers.Where(a => !string.IsNullOrWhiteSpace(a.TextValue)))
                {
                    var val = ans.TextValue!.Trim();
                    if (!choiceCounts.ContainsKey(val)) choiceCounts[val] = 0;
                    choiceCounts[val]++;
                }
            }
            else if (q.QuestionType == FeedbackQuestionType.Text)
            {
                textAnswers = answers.Where(a => !string.IsNullOrWhiteSpace(a.TextValue)).Select(a => a.TextValue!.Trim()).ToList();
            }

            questionAnalyticsList.Add(new AdminQuestionAnalyticsDto
            {
                QuestionId = q.Id,
                QuestionKey = q.QuestionKey,
                PromptText = q.PromptText,
                QuestionType = q.QuestionType,
                TargetAudience = q.TargetAudience,
                TotalAnswers = answers.Count,
                AverageRating = avgScore,
                ChoiceCounts = choiceCounts,
                TextAnswers = textAnswers
            });
        }

        var submissions = feedbacks.Select(f =>
        {
            var rawName = f.StudentProfile?.User?.FullName ?? f.WorkshopBooking?.GuestName;
            string masked = "Verified Attendee";
            if (!string.IsNullOrWhiteSpace(rawName))
            {
                var parts = rawName.Trim().Split(' ');
                masked = parts.Length > 1 ? $"{parts[0]} {parts[1][0]}." : parts[0];
            }

            var ansDtos = f.Answers.Select(a => new AdminFeedbackAnswerDetailDto
            {
                QuestionId = a.FeedbackQuestionId,
                PromptText = a.FeedbackQuestion?.PromptText ?? "Question",
                QuestionType = a.FeedbackQuestion?.QuestionType ?? FeedbackQuestionType.Rating1To5,
                NumericValue = a.NumericValue,
                TextValue = a.TextValue
            }).ToList();

            return new AdminWorkshopFeedbackSubmissionDetailDto
            {
                FeedbackId = f.Id,
                BookingId = f.WorkshopBookingId,
                BookingReference = f.WorkshopBookingId.HasValue ? f.WorkshopBookingId.Value.ToString()[..8].ToUpperInvariant() : string.Empty,
                StudentNameMasked = masked,
                Rating = f.Rating ?? 0,
                Comment = f.Comment,
                WouldRecommend = f.WouldRecommend,
                WouldAttendTrainerAgain = f.WouldAttendTrainerAgain,
                AudienceType = f.AudienceType.ToString(),
                SubmittedAt = f.SubmittedAt,
                FormattedDate = f.SubmittedAt.ToString("dd MMM yyyy, hh:mm tt"),
                Answers = ansDtos
            };
        }).ToList();

        var totalCount = feedbacks.Count;
        var avg = totalCount > 0 ? Math.Round((decimal)feedbacks.Average(f => f.Rating ?? 5), 1) : 0m;

        var metrics = new AdminFeedbackMetricsDto
        {
            TotalBookings = totalCount,
            EligibleAttendedCount = feedbacks.Count(f => f.AudienceType == FeedbackAudienceType.Attended),
            EligibleNoShowCount = feedbacks.Count(f => f.AudienceType == FeedbackAudienceType.NoShow),
            SubmittedCount = totalCount,
            ResponseRate = 100m,
            AverageRating = avg,
            FiveStars = feedbacks.Count(f => (f.Rating ?? 0) == 5),
            FourStars = feedbacks.Count(f => (f.Rating ?? 0) == 4),
            ThreeStars = feedbacks.Count(f => (f.Rating ?? 0) == 3),
            TwoStars = feedbacks.Count(f => (f.Rating ?? 0) == 2),
            OneStar = feedbacks.Count(f => (f.Rating ?? 0) == 1)
        };

        return new AdminWorkshopFeedbackAnalyticsResponse
        {
            WorkshopId = workshop.Id,
            WorkshopTitle = workshop.Title,
            Metrics = metrics,
            QuestionAnalytics = questionAnalyticsList,
            Submissions = submissions
        };
    }

    public async Task UpdateSessionAsync(
        Guid workshopId,
        Guid sessionId,
        int requestedCapacity,
        CancellationToken cancellationToken)
    {
        if (requestedCapacity <= 0)
        {
            throw new ArgumentException("Session capacity must be greater than zero.");
        }

        using var tx = _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM workshop_sessions WHERE \"Id\" = {sessionId} FOR UPDATE",
                cancellationToken);
        }

        var session = await _db.WorkshopSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.WorkshopId == workshopId, cancellationToken);

        if (session == null)
        {
            throw new ArgumentException("Workshop session not found.");
        }

        var nowUtc = DateTime.UtcNow;
        var activeBookings = await _db.WorkshopBookingSessions
            .Include(bs => bs.WorkshopBooking)
            .CountAsync(bs => bs.WorkshopSessionId == sessionId &&
                              bs.Status == WorkshopBookingSessionStatus.Booked &&
                              (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                               bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                               (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                                bs.WorkshopBooking.ReservationExpiresAt > nowUtc)),
                        cancellationToken);

        if (requestedCapacity < activeBookings)
        {
            throw new BusinessRuleException(
                "CANNOT_REDUCE_CAPACITY",
                $"Cannot reduce capacity to {requestedCapacity}. There are already {activeBookings} active reservations for this session.");
        }

        session.Capacity = requestedCapacity;
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (tx != null)
        {
            await tx.CommitAsync(cancellationToken);
        }
    }

    public async Task<AdminWorkshopDraftResponse?> GetDraftAsync(
        Guid adminUserId,
        Guid? workshopId,
        CancellationToken cancellationToken)
    {
        var targetWorkshopId = workshopId.HasValue && workshopId.Value != Guid.Empty ? workshopId.Value : (Guid?)null;

        var draft = await _db.WorkshopDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.AdminUserId == adminUserId && d.WorkshopId == targetWorkshopId, cancellationToken);

        if (draft == null) return null;

        return new AdminWorkshopDraftResponse
        {
            Id = draft.Id,
            AdminUserId = draft.AdminUserId,
            WorkshopId = draft.WorkshopId,
            DraftJson = draft.DraftJson,
            Version = draft.Version,
            CreatedAt = draft.CreatedAt,
            UpdatedAt = draft.UpdatedAt
        };
    }

    public async Task<AdminWorkshopDraftResponse> SaveDraftAsync(
        Guid adminUserId,
        AdminSaveWorkshopDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DraftJson))
        {
            throw new ArgumentException("Draft content cannot be empty.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(request.DraftJson) > 2 * 1024 * 1024)
        {
            throw new ArgumentException("Draft content exceeds maximum permitted size of 2 MB.");
        }

        // Validate JSON
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(request.DraftJson);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                throw new ArgumentException("Draft content must be a valid JSON object.");
            }
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ArgumentException("Invalid JSON format in draft content.");
        }

        var workshopId = request.WorkshopId.HasValue && request.WorkshopId.Value != Guid.Empty
            ? request.WorkshopId.Value
            : (Guid?)null;

        var existing = await _db.WorkshopDrafts
            .FirstOrDefaultAsync(d => d.AdminUserId == adminUserId && d.WorkshopId == workshopId, cancellationToken);

        var now = DateTime.UtcNow;

        if (existing == null)
        {
            // Initial draft save: expectedVersion must be 0
            if (request.ExpectedVersion > 0)
            {
                throw new ConflictException("The draft does not exist or has been deleted.");
            }

            var newDraft = new WorkshopDraft
            {
                Id = Guid.NewGuid(),
                AdminUserId = adminUserId,
                WorkshopId = workshopId,
                DraftJson = request.DraftJson,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            try
            {
                _db.WorkshopDrafts.Add(newDraft);
                await _db.SaveChangesAsync(cancellationToken);

                return new AdminWorkshopDraftResponse
                {
                    Id = newDraft.Id,
                    AdminUserId = newDraft.AdminUserId,
                    WorkshopId = newDraft.WorkshopId,
                    DraftJson = newDraft.DraftJson,
                    Version = newDraft.Version,
                    CreatedAt = newDraft.CreatedAt,
                    UpdatedAt = newDraft.UpdatedAt
                };
            }
            catch (DbUpdateException ex)
            {
                var isUniqueViolation = (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
                    || (ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
                    || (ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true);

                if (isUniqueViolation)
                {
                    // Race condition: another concurrent expectedVersion=0 request created it
                    var concurrentDraft = await _db.WorkshopDrafts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.AdminUserId == adminUserId && d.WorkshopId == workshopId, cancellationToken);

                    throw new ConflictException(
                        "A draft was just created concurrently in another session. Please reload to view the latest draft.",
                        concurrentDraft?.Version ?? 1);
                }

                throw;
            }
        }

        // Existing draft: verify expectedVersion matches
        if (request.ExpectedVersion != existing.Version)
        {
            throw new ConflictException(
                $"Draft version conflict. Expected version {request.ExpectedVersion}, but server has version {existing.Version}.",
                existing.Version);
        }

        existing.DraftJson = request.DraftJson;
        existing.Version += 1;
        existing.UpdatedAt = now;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var latest = await _db.WorkshopDrafts
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == existing.Id, cancellationToken);

            throw new ConflictException(
                "Draft was modified concurrently by another session.",
                latest?.Version ?? existing.Version);
        }

        return new AdminWorkshopDraftResponse
        {
            Id = existing.Id,
            AdminUserId = existing.AdminUserId,
            WorkshopId = existing.WorkshopId,
            DraftJson = existing.DraftJson,
            Version = existing.Version,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = existing.UpdatedAt
        };
    }

    public async Task<bool> DiscardDraftAsync(
        Guid adminUserId,
        Guid draftId,
        CancellationToken cancellationToken)
    {
        var draft = await _db.WorkshopDrafts
            .FirstOrDefaultAsync(d => d.Id == draftId, cancellationToken);

        if (draft == null) return false;

        if (draft.AdminUserId != adminUserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete another administrator's draft.");
        }

        _db.WorkshopDrafts.Remove(draft);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
