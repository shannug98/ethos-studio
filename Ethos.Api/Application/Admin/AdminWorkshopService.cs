using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminWorkshopService : IAdminWorkshopService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public AdminWorkshopService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
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
        WorkshopStatus? status,
        Guid? trainerId,
        DateTime? startDate,
        DateTime? endDate,
        string? search,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
            .Include(w => w.Bookings)
            .AsQueryable();

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

    public async Task<TrainerWorkshopResponse?> GetWorkshopByIdAsync(
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var w = await _db.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
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

        if (request.EndTime <= request.StartTime)
            throw new ArgumentException("Workshop end time must be after start time.");

        if (request.Capacity <= 0)
            throw new ArgumentException("Workshop capacity must be greater than zero.");

        if (request.Price < 0)
            throw new ArgumentException("Workshop price cannot be negative.");

        if (request.TrainerProfileId.HasValue && request.TrainerProfileId.Value != Guid.Empty)
        {
            var trainerExists = await _db.TrainerProfiles
                .AnyAsync(t => t.Id == request.TrainerProfileId.Value, cancellationToken);
            if (!trainerExists)
                throw new ArgumentException("Assigned trainer profile was not found.");
        }

        var now = DateTime.UtcNow;
        var initialStatus = request.Status ?? WorkshopStatus.Approved;

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = request.TrainerProfileId == Guid.Empty ? null : request.TrainerProfileId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            DanceStyle = request.DanceStyle.Trim(),
            Level = request.Level.Trim(),
            WorkshopDate = DateTime.SpecifyKind(request.WorkshopDate.Date, DateTimeKind.Utc),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Venue = request.Venue.Trim(),
            Price = request.Price,
            AdminApprovedPrice = request.Price,
            PriceApprovedAt = now,
            PriceApprovedByUserId = adminUserId,
            Capacity = request.Capacity,
            Status = initialStatus,
            ImageUrl = request.ImageUrl?.Trim(),
            AllowReEntry = request.AllowReEntry,
            RequireReEntryVerification = request.RequireReEntryVerification,
            ReEntryCooldown = request.ReEntryCooldown,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Workshops.Add(workshop);

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

        return Map(workshop);
    }

    public async Task<TrainerWorkshopResponse> UpdateWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        AdminUpdateWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .Include(w => w.TrainerProfile)
            .Include(w => w.Bookings)
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Workshop title is required.");

        if (string.IsNullOrWhiteSpace(request.DanceStyle))
            throw new ArgumentException("Dance style is required.");

        if (string.IsNullOrWhiteSpace(request.Level))
            throw new ArgumentException("Workshop difficulty level is required.");

        if (string.IsNullOrWhiteSpace(request.Venue))
            throw new ArgumentException("Workshop venue or studio room is required.");

        if (request.EndTime <= request.StartTime)
            throw new ArgumentException("Workshop end time must be after start time.");

        if (request.Capacity <= 0)
            throw new ArgumentException("Workshop capacity must be greater than zero.");

        if (request.Price < 0)
            throw new ArgumentException("Workshop price cannot be negative.");

        if (request.TrainerProfileId.HasValue && request.TrainerProfileId.Value != Guid.Empty)
        {
            var trainerExists = await _db.TrainerProfiles
                .AnyAsync(t => t.Id == request.TrainerProfileId.Value, cancellationToken);
            if (!trainerExists)
                throw new ArgumentException("Assigned trainer profile was not found.");
        }

        var now = DateTime.UtcNow;

        workshop.Title = request.Title.Trim();
        workshop.Description = request.Description?.Trim();
        workshop.DanceStyle = request.DanceStyle.Trim();
        workshop.Level = request.Level.Trim();
        workshop.WorkshopDate = DateTime.SpecifyKind(request.WorkshopDate.Date, DateTimeKind.Utc);
        workshop.StartTime = request.StartTime;
        workshop.EndTime = request.EndTime;
        workshop.Venue = request.Venue.Trim();
        workshop.Capacity = request.Capacity;
        workshop.ImageUrl = request.ImageUrl?.Trim();
        workshop.TrainerProfileId = request.TrainerProfileId == Guid.Empty ? null : request.TrainerProfileId;
        workshop.AllowReEntry = request.AllowReEntry;
        workshop.RequireReEntryVerification = request.RequireReEntryVerification;
        workshop.ReEntryCooldown = request.ReEntryCooldown;

        if (request.Status.HasValue)
        {
            workshop.Status = request.Status.Value;
        }

        if (request.Price != workshop.Price)
        {
            workshop.Price = request.Price;
            workshop.AdminApprovedPrice = request.Price;
            workshop.PriceApprovedAt = now;
            workshop.PriceApprovedByUserId = adminUserId;
        }

        workshop.UpdatedAt = now;

        _auditService.AddAuditLog(
            adminUserId,
            "ADMIN_WORKSHOP_UPDATED",
            "Workshop",
            workshop.Id,
            $"Administrator updated workshop '{workshop.Title}' (Status: {workshop.Status}, Price: {workshop.Price} INR)");

        await _db.SaveChangesAsync(cancellationToken);

        if (workshop.TrainerProfileId.HasValue && workshop.TrainerProfile == null)
        {
            await _db.Entry(workshop)
                .Reference(w => w.TrainerProfile)
                .LoadAsync(cancellationToken);
        }

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

    public async Task CancelWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        if (workshop.Status == WorkshopStatus.Completed)
        {
            throw new InvalidOperationException("Completed workshops cannot be cancelled.");
        }

        if (workshop.Status == WorkshopStatus.Cancelled)
        {
            throw new InvalidOperationException("Workshop is already cancelled.");
        }

        workshop.Status = WorkshopStatus.Cancelled;
        workshop.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_CANCELLED",
            "Workshop",
            workshop.Id,
            reason ?? "Workshop cancelled by admin.");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteWorkshopAsync(
        Guid workshopId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        if (workshop.Status != WorkshopStatus.Approved)
        {
            throw new InvalidOperationException("Only approved workshops can be marked as completed.");
        }

        workshop.Status = WorkshopStatus.Completed;
        workshop.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_COMPLETED",
            "Workshop",
            workshop.Id,
            "Workshop marked as completed by admin.");

        await _db.SaveChangesAsync(cancellationToken);
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
            .Where(t => t.WorkshopId == workshopId)
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

        if (request?.Tiers == null || request.Tiers.Count != 4)
        {
            throw new ArgumentException("Exact 4 pricing tiers (Tier 1 to 4) must be provided.");
        }

        foreach (var t in request.Tiers)
        {
            if (t.Price < 0)
                throw new ArgumentException($"Price for Tier {t.TierNumber} cannot be negative.");
        }

        var existingTiers = await _db.WorkshopPricingTiers
            .Where(t => t.WorkshopId == workshopId)
            .ToListAsync(cancellationToken);

        if (existingTiers.Count > 0)
        {
            _db.WorkshopPricingTiers.RemoveRange(existingTiers);
        }

        foreach (var item in request.Tiers.OrderBy(t => t.TierNumber))
        {
            var tierEntity = new WorkshopPricingTier
            {
                Id = Guid.NewGuid(),
                WorkshopId = workshopId,
                TierNumber = item.TierNumber,
                TierName = string.IsNullOrWhiteSpace(item.TierName) ? $"Tier {item.TierNumber}" : item.TierName.Trim(),
                MinTickets = item.TierNumber switch { 1 => 1, 2 => 11, 3 => 21, _ => 31 },
                MaxTickets = item.TierNumber switch { 1 => 10, 2 => 20, 3 => 30, _ => null },
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
                FeedbackComment = feedback?.Comment
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

    private static TrainerWorkshopResponse Map(Workshop w)
    {
        var effectivePrice = w.AdminApprovedPrice ?? w.TrainerProposedPrice ?? w.Price;
        return new TrainerWorkshopResponse
        {
            Id = w.Id,
            WorkshopReference = $"WKS-{w.WorkshopDate.Year}-{w.Id.ToString()[..6].ToUpperInvariant()}",
            TrainerProfileId = w.TrainerProfileId ?? Guid.Empty,
            TrainerName = w.TrainerProfile?.FullName ?? "Ethos Trainer",
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
            BookedCount = w.Bookings?.Count(b => b.Status == WorkshopBookingStatus.Confirmed || b.Status == WorkshopBookingStatus.Attended) ?? 0,
            Status = w.Status.ToString(),
            ImageUrl = w.ImageUrl,
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
            .FirstOrDefaultAsync(w => w.Id == workshopId, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found.");

        if (string.IsNullOrWhiteSpace(workshop.Title))
            throw new InvalidOperationException("Workshop title is required to publish.");

        if (workshop.Price <= 0)
            throw new InvalidOperationException("Workshop must have a valid price before publishing.");

        workshop.Status = WorkshopStatus.Published;
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
        }
        else if (!string.IsNullOrWhiteSpace(request.TicketNumber))
        {
            var cleanNum = request.TicketNumber.Trim().ToUpperInvariant();
            ticket = await query.FirstOrDefaultAsync(t => t.TicketNumber.ToUpper() == cleanNum, cancellationToken);
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
            return new AdminCheckInTicketResponse
            {
                Success = false,
                Code = "WRONG_WORKSHOP",
                Message = "This ticket belongs to another workshop and cannot be checked in here.",
                WorkshopId = actualWorkshopId,
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber
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
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber
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
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber
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
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber
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
        CancellationToken cancellationToken)
    {
        var query = _db.WorkshopTickets
            .AsNoTracking()
            .Include(t => t.WorkshopBooking)
            .Include(t => t.Attendance)
            .Where(t => t.WorkshopId == workshopId || t.WorkshopBooking.WorkshopId == workshopId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
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
                AttendeeType = isGuest ? "Workshop Guest" : "ETHOS Student"
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
                .ThenInclude(sp => sp.User)
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
                Rating = f.Rating,
                Comment = f.Comment,
                StudentNameMasked = masked,
                SubmittedAt = f.SubmittedAt,
                FormattedDate = f.SubmittedAt.ToString("dd MMM yyyy, hh:mm tt")
            };
        }).ToList();
    }
}
