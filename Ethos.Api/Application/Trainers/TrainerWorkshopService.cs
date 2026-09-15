using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Trainers;

public class TrainerWorkshopService : ITrainerWorkshopService
{
    private readonly AppDbContext _db;
    private readonly ITrainerPermissionService _permissionService;
    private readonly Application.Workshops.IWorkshopTicketService _ticketService;

    public TrainerWorkshopService(
        AppDbContext db,
        ITrainerPermissionService permissionService,
        Application.Workshops.IWorkshopTicketService ticketService)
    {
        _db = db;
        _permissionService = permissionService;
        _ticketService = ticketService;
    }

    public async Task<IReadOnlyList<TrainerWorkshopResponse>> GetMyWorkshopsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            return Array.Empty<TrainerWorkshopResponse>();

        var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_VIEW_WORKSHOPS", cancellationToken);
        if (!hasPerm)
            return Array.Empty<TrainerWorkshopResponse>();

        return await _db.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
            .Include(w => w.Bookings)
            .Where(w => w.TrainerProfileId == trainer.Id)
            .OrderByDescending(w => w.WorkshopDate)
            .Select(w => Map(w))
            .ToListAsync(cancellationToken);
    }

    public async Task<TrainerWorkshopResponse?> GetMyWorkshopByIdAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            return null;

        var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_VIEW_WORKSHOPS", cancellationToken);
        if (!hasPerm)
            return null;

        var workshop = await _db.Workshops
            .AsNoTracking()
            .Include(w => w.TrainerProfile)
            .Include(w => w.Bookings)
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        return workshop == null ? null : Map(workshop);
    }

    public async Task<TrainerWorkshopResponse> CreateWorkshopAsync(
        Guid userId,
        TrainerWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (trainer == null || trainer.Status != TrainerStatus.Active)
        {
            throw new InvalidOperationException("Only active trainers can create workshops.");
        }

        var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_CREATE_WORKSHOP", cancellationToken);
        if (!hasPerm)
        {
            throw new UnauthorizedAccessException("You do not have permission to create workshops.");
        }

        if (request == null)
        {
            throw new ArgumentException("Workshop request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Workshop title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DanceStyle))
        {
            throw new ArgumentException("Dance style is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Level))
        {
            throw new ArgumentException("Level is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Venue))
        {
            throw new ArgumentException("Venue is required.");
        }

        if (request.Capacity <= 0)
        {
            throw new ArgumentException("Capacity must be greater than zero.");
        }

        if (request.ProposedPrice < 0)
        {
            throw new ArgumentException("Proposed price cannot be negative.");
        }

        if (request.EndTime <= request.StartTime)
        {
            throw new ArgumentException("End time must be after start time.");
        }

        var workshop = new Workshop
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainer.Id,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            DanceStyle = request.DanceStyle.Trim(),
            Level = request.Level.Trim(),
            WorkshopDate = request.WorkshopDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Venue = request.Venue.Trim(),
            TrainerProposedPrice = request.ProposedPrice,
            AdminApprovedPrice = null,
            PriceApprovedAt = null,
            PriceApprovedByUserId = null,
            Price = request.ProposedPrice,
            Capacity = request.Capacity,
            ImageUrl = request.ImageUrl?.Trim(),
            Status = WorkshopStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Workshops.Add(workshop);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetMyWorkshopByIdAsync(userId, workshop.Id, cancellationToken)
            ?? throw new InvalidOperationException("Error retrieving created workshop.");
    }

    public async Task<TrainerWorkshopResponse?> UpdateWorkshopAsync(
        Guid userId,
        Guid workshopId,
        TrainerWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            return null;

        var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_UPDATE_WORKSHOP", cancellationToken);
        if (!hasPerm)
        {
            throw new UnauthorizedAccessException("You do not have permission to update workshops.");
        }

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            return null;

        if (workshop.Status != WorkshopStatus.Draft && workshop.Status != WorkshopStatus.Rejected)
        {
            throw new InvalidOperationException("Workshops can only be updated when in Draft or Rejected status.");
        }

        if (request == null)
        {
            throw new ArgumentException("Workshop request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Workshop title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DanceStyle))
        {
            throw new ArgumentException("Dance style is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Level))
        {
            throw new ArgumentException("Level is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Venue))
        {
            throw new ArgumentException("Venue is required.");
        }

        if (request.Capacity <= 0)
        {
            throw new ArgumentException("Capacity must be greater than zero.");
        }

        if (request.ProposedPrice < 0)
        {
            throw new ArgumentException("Proposed price cannot be negative.");
        }

        if (request.EndTime <= request.StartTime)
        {
            throw new ArgumentException("End time must be after start time.");
        }

        // Reset approval state on material edit
        workshop.Title = request.Title.Trim();
        workshop.Description = request.Description?.Trim();
        workshop.DanceStyle = request.DanceStyle.Trim();
        workshop.Level = request.Level.Trim();
        workshop.WorkshopDate = request.WorkshopDate;
        workshop.StartTime = request.StartTime;
        workshop.EndTime = request.EndTime;
        workshop.Venue = request.Venue.Trim();
        workshop.TrainerProposedPrice = request.ProposedPrice;
        workshop.AdminApprovedPrice = null;
        workshop.PriceApprovedAt = null;
        workshop.PriceApprovedByUserId = null;
        workshop.Price = request.ProposedPrice;
        workshop.Capacity = request.Capacity;
        workshop.ImageUrl = request.ImageUrl?.Trim();
        workshop.Status = WorkshopStatus.Draft;
        workshop.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetMyWorkshopByIdAsync(userId, workshop.Id, cancellationToken);
    }

    public async Task SubmitWorkshopAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new InvalidOperationException("Trainer profile not active.");

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

        if (workshop.Status != WorkshopStatus.Draft && workshop.Status != WorkshopStatus.Rejected)
        {
            throw new InvalidOperationException("Only draft or rejected workshops can be submitted for approval.");
        }

        workshop.Status = WorkshopStatus.PendingApproval;
        workshop.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelWorkshopAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new InvalidOperationException("Trainer profile not active.");

        var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_CANCEL_WORKSHOP", cancellationToken);
        if (!hasPerm)
        {
            throw new UnauthorizedAccessException("You do not have permission to cancel workshops.");
        }

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

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

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrainerWorkshopStudentResponse>> GetWorkshopStudentsAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new InvalidOperationException("Trainer profile not active.");

        var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_VIEW_WORKSHOP_STUDENTS", cancellationToken);
        if (!hasPerm)
        {
            throw new UnauthorizedAccessException("You do not have permission to view workshop students.");
        }

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

        return await _db.WorkshopBookings
            .Include(b => b.StudentProfile)
            .ThenInclude(s => s.User)
            .Where(b => b.WorkshopId == workshopId &&
                        (b.Status == WorkshopBookingStatus.Confirmed ||
                         b.Status == WorkshopBookingStatus.Attended ||
                         b.Status == WorkshopBookingStatus.NoShow))
            .Select(b => new TrainerWorkshopStudentResponse
            {
                StudentId = b.StudentProfileId,
                StudentName = b.StudentProfile.User.FullName,
                StudentPhone = b.StudentProfile.User.Phone,
                Status = b.Status.ToString(),
                BookedAt = b.BookedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrainerWorkshopFeedbackResponse>> GetWorkshopFeedbackAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new InvalidOperationException("Trainer profile not active.");

        var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_VIEW_WORKSHOP_FEEDBACK", cancellationToken);
        if (!hasPerm)
        {
            throw new UnauthorizedAccessException("You do not have permission to view workshop feedback.");
        }

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

        return await _db.WorkshopFeedbacks
            .Include(f => f.StudentProfile)
            .ThenInclude(s => s.User)
            .Where(f => f.WorkshopId == workshopId &&
                        f.IsValid &&
                        f.WorkshopBooking != null &&
                        f.WorkshopBooking.Status == WorkshopBookingStatus.Attended)
            .OrderByDescending(f => f.SubmittedAt)
            .Select(f => new TrainerWorkshopFeedbackResponse
            {
                Id = f.Id,
                StudentName = "Student",
                Rating = f.Rating,
                Comment = null,
                CreatedAt = f.SubmittedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateWorkshopStudentStatusAsync(
        Guid userId,
        Guid workshopId,
        Guid studentId,
        WorkshopBookingStatus newStatus,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new InvalidOperationException("Trainer profile not active.");

        var hasPerm = await _permissionService.HasPermissionAsync(trainer.Id, "TRAINER_VIEW_WORKSHOP_STUDENTS", cancellationToken);
        if (!hasPerm)
        {
            throw new UnauthorizedAccessException("You do not have permission to manage workshop students.");
        }

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

        var booking = await _db.WorkshopBookings
            .FirstOrDefaultAsync(b => b.WorkshopId == workshopId && b.StudentProfileId == studentId, cancellationToken);

        if (booking == null)
            throw new ArgumentException("Booking not found for this student and workshop.");

        if (newStatus != WorkshopBookingStatus.Attended && newStatus != WorkshopBookingStatus.NoShow && newStatus != WorkshopBookingStatus.Confirmed)
        {
            throw new ArgumentException("Invalid booking status for attendance.");
        }

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = booking.Status;
            booking.Status = newStatus;

            if (before == WorkshopBookingStatus.Attended && newStatus != WorkshopBookingStatus.Attended)
            {
                var activeFeedbacks = await _db.WorkshopFeedbacks
                    .Where(f => f.WorkshopBookingId == booking.Id && f.IsValid)
                    .ToListAsync(cancellationToken);

                foreach (var fb in activeFeedbacks)
                {
                    fb.IsValid = false;
                    fb.InvalidationReason = $"Attendance reverted from {before} to {newStatus} by trainer.";
                    fb.InvalidatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static TrainerWorkshopResponse Map(Workshop w)
    {
        var effectivePrice = w.AdminApprovedPrice ?? w.TrainerProposedPrice ?? w.Price;
        return new TrainerWorkshopResponse
        {
            Id = w.Id,
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

    public async Task<TicketValidationResponse> ValidateTicketAsync(
        Guid userId,
        Guid workshopId,
        TicketValidationRequest request,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new UnauthorizedAccessException("Trainer profile not active.");

        var workshop = await _db.Workshops
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

        if (string.IsNullOrWhiteSpace(request?.TokenOrNumber))
        {
            return new TicketValidationResponse
            {
                IsValid = false,
                ValidationStatus = "Invalid",
                Message = "Please scan or enter a ticket QR code or ticket number."
            };
        }

        var input = request.TokenOrNumber.Trim();
        var tokenHash = _ticketService.ComputeTokenHash(input);

        var ticket = await _db.WorkshopTickets
            .Include(t => t.WorkshopBooking)
            .Include(t => t.Attendance)
            .FirstOrDefaultAsync(t => t.QrTokenHash == tokenHash || t.TicketNumber == input, cancellationToken);

        if (ticket == null)
        {
            return new TicketValidationResponse
            {
                IsValid = false,
                ValidationStatus = "NotFound",
                Message = "Ticket not recognized. Please scan a valid Ethos ticket QR."
            };
        }

        if (ticket.WorkshopId != workshopId)
        {
            return new TicketValidationResponse
            {
                IsValid = false,
                ValidationStatus = "InvalidWorkshop",
                Message = "This ticket belongs to a different workshop session.",
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName
            };
        }

        if (ticket.Status == TicketStatus.Cancelled || ticket.Status == TicketStatus.Refunded)
        {
            return new TicketValidationResponse
            {
                IsValid = false,
                ValidationStatus = "CancelledOrRefunded",
                Message = $"This ticket is {ticket.Status} and is no longer valid for entry.",
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName,
                Status = ticket.Status
            };
        }

        if (ticket.Status == TicketStatus.Expired)
        {
            return new TicketValidationResponse
            {
                IsValid = false,
                ValidationStatus = "Expired",
                Message = "This workshop ticket has expired.",
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName,
                Status = ticket.Status
            };
        }

        if (ticket.WorkshopBooking != null &&
            ticket.WorkshopBooking.Status != WorkshopBookingStatus.Confirmed &&
            ticket.WorkshopBooking.Status != WorkshopBookingStatus.Attended)
        {
            return new TicketValidationResponse
            {
                IsValid = false,
                ValidationStatus = "Unpaid",
                Message = "Booking payment is not confirmed.",
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName
            };
        }

        // Load all tickets in this booking for group preview
        var groupTickets = await _db.WorkshopTickets
            .Where(t => t.WorkshopBookingId == ticket.WorkshopBookingId)
            .OrderBy(t => t.TicketNumber)
            .Select(gt => new GroupTicketSummaryDto
            {
                TicketId = gt.Id,
                TicketNumber = gt.TicketNumber,
                AttendeeName = gt.AttendeeName,
                IsPrimaryAttendee = gt.IsPrimaryAttendee,
                IsCheckedIn = gt.CheckedInAt.HasValue,
                CheckedInAt = gt.CheckedInAt,
                Status = gt.Status,
                IsEligibleForCheckIn = gt.Status == TicketStatus.Issued && !gt.CheckedInAt.HasValue
            })
            .ToListAsync(cancellationToken);

        var checkedInCount = groupTickets.Count(gt => gt.IsCheckedIn);

        string MaskPhone(string? p)
        {
            if (string.IsNullOrWhiteSpace(p) || p.Length < 4) return "******";
            return new string('*', Math.Max(0, p.Length - 4)) + p[^4..];
        }

        if (ticket.CheckedInAt.HasValue)
        {
            return new TicketValidationResponse
            {
                IsValid = false,
                ValidationStatus = "AlreadyCheckedIn",
                Message = $"Ticket already checked in at {ticket.CheckedInAt.Value:hh:mm tt, dd MMM}.",
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName,
                MaskedPhone = MaskPhone(ticket.AttendeePhone),
                IsPrimaryAttendee = ticket.IsPrimaryAttendee,
                Status = ticket.Status,
                CheckedInAt = ticket.CheckedInAt,
                IsCurrentlyInside = ticket.Attendance?.IsCurrentlyInside ?? false,
                WorkshopBookingId = ticket.WorkshopBookingId,
                GroupTotalTickets = groupTickets.Count,
                GroupCheckedInCount = checkedInCount,
                GroupTickets = groupTickets
            };
        }

        return new TicketValidationResponse
        {
            IsValid = true,
            ValidationStatus = "Valid",
            Message = "Valid ticket pass ready for check-in.",
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            AttendeeName = ticket.AttendeeName,
            MaskedPhone = MaskPhone(ticket.AttendeePhone),
            IsPrimaryAttendee = ticket.IsPrimaryAttendee,
            Status = ticket.Status,
            CheckedInAt = null,
            IsCurrentlyInside = false,
            WorkshopBookingId = ticket.WorkshopBookingId,
            GroupTotalTickets = groupTickets.Count,
            GroupCheckedInCount = checkedInCount,
            GroupTickets = groupTickets
        };
    }

    public async Task<TicketValidationResponse> CheckInTicketAsync(
        Guid userId,
        Guid workshopId,
        Guid ticketId,
        CheckInTicketRequest request,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new UnauthorizedAccessException("Trainer profile not active.");

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var ticket = await _db.WorkshopTickets
                .Include(t => t.WorkshopBooking)
                .FirstOrDefaultAsync(t => t.Id == ticketId && t.WorkshopId == workshopId, cancellationToken);

            if (ticket == null)
            {
                throw new ArgumentException("Ticket not found for this workshop.");
            }

            if (ticket.CheckedInAt.HasValue)
            {
                throw new InvalidOperationException($"Ticket is already checked in (at {ticket.CheckedInAt.Value:hh:mm tt}).");
            }

            if (ticket.Status != TicketStatus.Issued)
            {
                throw new InvalidOperationException($"Ticket cannot be checked in because its status is {ticket.Status}.");
            }

            var checkInTime = DateTime.UtcNow;
            var checkInMethod = request?.Method ?? CheckInMethod.QrScan;

            ticket.CheckedInAt = checkInTime;
            ticket.CheckedInByUserId = userId;
            ticket.CheckInMethod = checkInMethod;
            ticket.AttendeeDetailsLockedAt = checkInTime;

            var attendance = new WorkshopAttendance
            {
                Id = Guid.NewGuid(),
                WorkshopTicketId = ticket.Id,
                WorkshopId = workshopId,
                CheckedInByUserId = userId,
                FirstCheckedInAt = checkInTime,
                LastCheckedInAt = checkInTime,
                IsCurrentlyInside = true,
                Method = checkInMethod,
                Notes = request?.Notes
            };
            _db.WorkshopAttendances.Add(attendance);

            var attEvent = new WorkshopAttendanceEvent
            {
                Id = Guid.NewGuid(),
                WorkshopTicketId = ticket.Id,
                WorkshopId = workshopId,
                PerformedByUserId = userId,
                EventType = AttendanceEventType.CheckIn,
                OccurredAt = checkInTime,
                Method = checkInMethod,
                Notes = request?.Notes
            };
            _db.WorkshopAttendanceEvents.Add(attEvent);

            if (ticket.WorkshopBooking != null && ticket.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed)
            {
                ticket.WorkshopBooking.Status = WorkshopBookingStatus.Attended;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            string MaskPhone(string? p)
            {
                if (string.IsNullOrWhiteSpace(p) || p.Length < 4) return "******";
                return new string('*', Math.Max(0, p.Length - 4)) + p[^4..];
            }

            return new TicketValidationResponse
            {
                IsValid = true,
                ValidationStatus = "CheckedIn",
                Message = $"Check-in confirmed for {ticket.AttendeeName}!",
                TicketId = ticket.Id,
                TicketNumber = ticket.TicketNumber,
                AttendeeName = ticket.AttendeeName,
                MaskedPhone = MaskPhone(ticket.AttendeePhone),
                IsPrimaryAttendee = ticket.IsPrimaryAttendee,
                Status = ticket.Status,
                CheckedInAt = checkInTime,
                IsCurrentlyInside = true,
                WorkshopBookingId = ticket.WorkshopBookingId
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<GroupCheckInResponse> GroupCheckInAsync(
        Guid userId,
        Guid workshopId,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new UnauthorizedAccessException("Trainer profile not active.");

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var tickets = await _db.WorkshopTickets
                .Include(t => t.WorkshopBooking)
                .Where(t => t.WorkshopBookingId == bookingId && t.WorkshopId == workshopId)
                .OrderBy(t => t.TicketNumber)
                .ToListAsync(cancellationToken);

            if (tickets.Count == 0)
            {
                throw new ArgumentException("No tickets found for this booking.");
            }

            var response = new GroupCheckInResponse
            {
                BookingId = bookingId,
                TotalTickets = tickets.Count
            };

            var checkInTime = DateTime.UtcNow;
            bool anyCheckedIn = false;

            foreach (var ticket in tickets)
            {
                if (ticket.CheckedInAt.HasValue)
                {
                    response.AlreadyCheckedIn++;
                    response.Results.Add(new GroupCheckInItemResult
                    {
                        TicketId = ticket.Id,
                        TicketNumber = ticket.TicketNumber,
                        AttendeeName = ticket.AttendeeName,
                        Success = false,
                        Status = "AlreadyCheckedIn",
                        Message = "Already checked in earlier."
                    });
                    continue;
                }

                if (ticket.Status != TicketStatus.Issued)
                {
                    response.Failed++;
                    response.Results.Add(new GroupCheckInItemResult
                    {
                        TicketId = ticket.Id,
                        TicketNumber = ticket.TicketNumber,
                        AttendeeName = ticket.AttendeeName,
                        Success = false,
                        Status = ticket.Status.ToString(),
                        Message = $"Cannot check in {ticket.Status} ticket."
                    });
                    continue;
                }

                ticket.CheckedInAt = checkInTime;
                ticket.CheckedInByUserId = userId;
                ticket.CheckInMethod = CheckInMethod.QrScan;
                ticket.AttendeeDetailsLockedAt = checkInTime;

                var attendance = new WorkshopAttendance
                {
                    Id = Guid.NewGuid(),
                    WorkshopTicketId = ticket.Id,
                    WorkshopId = workshopId,
                    CheckedInByUserId = userId,
                    FirstCheckedInAt = checkInTime,
                    LastCheckedInAt = checkInTime,
                    IsCurrentlyInside = true,
                    Method = CheckInMethod.QrScan,
                    Notes = "Group check-in batch"
                };
                _db.WorkshopAttendances.Add(attendance);

                var attEvent = new WorkshopAttendanceEvent
                {
                    Id = Guid.NewGuid(),
                    WorkshopTicketId = ticket.Id,
                    WorkshopId = workshopId,
                    PerformedByUserId = userId,
                    EventType = AttendanceEventType.CheckIn,
                    OccurredAt = checkInTime,
                    Method = CheckInMethod.QrScan,
                    Notes = "Group check-in batch"
                };
                _db.WorkshopAttendanceEvents.Add(attEvent);

                response.SuccessfullyCheckedIn++;
                anyCheckedIn = true;
                response.Results.Add(new GroupCheckInItemResult
                {
                    TicketId = ticket.Id,
                    TicketNumber = ticket.TicketNumber,
                    AttendeeName = ticket.AttendeeName,
                    Success = true,
                    Status = "CheckedIn",
                    Message = "Checked in successfully."
                });
            }

            if (anyCheckedIn)
            {
                var booking = tickets.First().WorkshopBooking;
                if (booking != null && booking.Status == WorkshopBookingStatus.Confirmed)
                {
                    booking.Status = WorkshopBookingStatus.Attended;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return response;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<WorkshopAttendanceSummaryResponse> GetWorkshopAttendanceSummaryAsync(
        Guid userId,
        Guid workshopId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new UnauthorizedAccessException("Trainer profile not active.");

        var workshop = await _db.Workshops
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

        var tickets = await _db.WorkshopTickets
            .AsNoTracking()
            .Include(t => t.Attendance)
            .Where(t => t.WorkshopId == workshopId)
            .OrderBy(t => t.TicketNumber)
            .ToListAsync(cancellationToken);

        var totalIssued = tickets.Count;
        var checkedInCount = tickets.Count(t => t.CheckedInAt.HasValue);
        var currentlyInsideCount = tickets.Count(t => t.Attendance != null && t.Attendance.IsCurrentlyInside);

        string MaskPhone(string? p)
        {
            if (string.IsNullOrWhiteSpace(p) || p.Length < 4) return "******";
            return new string('*', Math.Max(0, p.Length - 4)) + p[^4..];
        }

        var attendees = tickets.Select(t => new WorkshopAttendeeListItemDto
        {
            TicketId = t.Id,
            TicketNumber = t.TicketNumber,
            WorkshopBookingId = t.WorkshopBookingId,
            AttendeeName = t.AttendeeName,
            MaskedPhone = MaskPhone(t.AttendeePhone),
            IsPrimaryAttendee = t.IsPrimaryAttendee,
            IsCheckedIn = t.CheckedInAt.HasValue,
            IsCurrentlyInside = t.Attendance?.IsCurrentlyInside ?? false,
            CheckedInAt = t.CheckedInAt,
            CheckInMethod = t.CheckInMethod?.ToString(),
            Status = t.Status.ToString()
        }).ToList();

        return new WorkshopAttendanceSummaryResponse
        {
            WorkshopId = workshop.Id,
            WorkshopTitle = workshop.Title,
            Capacity = workshop.Capacity,
            TotalTicketsIssued = totalIssued,
            TotalCheckedIn = checkedInCount,
            CurrentlyInside = currentlyInsideCount,
            AttendancePercentage = totalIssued > 0 ? Math.Round((decimal)checkedInCount / totalIssued * 100, 1) : 0m,
            AllowReEntry = workshop.AllowReEntry,
            Attendees = attendees
        };
    }

    public async Task<bool> RecordReEntryAsync(
        Guid userId,
        Guid workshopId,
        Guid ticketId,
        AttendanceEventType eventType,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (trainer == null || trainer.Status != TrainerStatus.Active)
            throw new UnauthorizedAccessException("Trainer profile not active.");

        var workshop = await _db.Workshops
            .FirstOrDefaultAsync(w => w.Id == workshopId && w.TrainerProfileId == trainer.Id, cancellationToken);

        if (workshop == null)
            throw new ArgumentException("Workshop not found or access denied.");

        if (!workshop.AllowReEntry && (eventType == AttendanceEventType.CheckOut || eventType == AttendanceEventType.ReEntry))
        {
            throw new InvalidOperationException("Re-entry is not enabled for this workshop.");
        }

        var attendance = await _db.WorkshopAttendances
            .FirstOrDefaultAsync(a => a.WorkshopTicketId == ticketId && a.WorkshopId == workshopId, cancellationToken);

        if (attendance == null)
        {
            throw new InvalidOperationException("Cannot record re-entry or exit for an attendee who has not checked in.");
        }

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            if (eventType == AttendanceEventType.CheckOut)
            {
                attendance.IsCurrentlyInside = false;
            }
            else if (eventType == AttendanceEventType.ReEntry)
            {
                attendance.IsCurrentlyInside = true;
                attendance.LastCheckedInAt = now;
            }

            var attEvent = new WorkshopAttendanceEvent
            {
                Id = Guid.NewGuid(),
                WorkshopTicketId = ticketId,
                WorkshopId = workshopId,
                PerformedByUserId = userId,
                EventType = eventType,
                OccurredAt = now,
                Method = CheckInMethod.QrScan,
                Notes = $"Recorded by trainer"
            };
            _db.WorkshopAttendanceEvents.Add(attEvent);

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return true;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

}
