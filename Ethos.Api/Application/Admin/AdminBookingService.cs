using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminBookingService : IAdminBookingService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;
    private readonly IWorkshopTicketService _ticketService;
    private readonly ITicketPdfService _ticketPdfService;

    public AdminBookingService(
        AppDbContext db,
        IAdminAuditService auditService,
        IWorkshopTicketService ticketService,
        ITicketPdfService ticketPdfService)
    {
        _db = db;
        _auditService = auditService;
        _ticketService = ticketService;
        _ticketPdfService = ticketPdfService;
    }

    public async Task<PagedResult<AdminClassEnrollmentResponse>> GetClassEnrollmentsAsync(
        int page,
        int pageSize,
        Guid? classId,
        Guid? studentId,
        int? status,
        string? search,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.StudentProfile)
            .ThenInclude(s => s.User)
            .Include(e => e.DanceClass)
            .Include(e => e.StudentPackage)
            .ThenInclude(p => p!.Package)
            .AsQueryable();

        if (classId.HasValue)
            query = query.Where(e => e.DanceClassId == classId.Value);

        if (studentId.HasValue)
            query = query.Where(e => e.StudentProfileId == studentId.Value);

        if (status.HasValue)
            query = query.Where(e => (int)e.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(e =>
                e.StudentProfile.User.FullName.ToLower().Contains(s) ||
                e.StudentProfile.User.Phone.Contains(s) ||
                e.StudentProfile.User.CustomerCode.ToLower().Contains(s) ||
                e.DanceClass.Name.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AdminClassEnrollmentResponse
            {
                Id = e.Id,
                StudentProfileId = e.StudentProfileId,
                StudentName = e.StudentProfile.User.FullName,
                StudentPhone = e.StudentProfile.User.Phone,
                StudentCustomerCode = e.StudentProfile.User.CustomerCode,
                DanceClassId = e.DanceClassId,
                DanceClassName = e.DanceClass.Name,
                StudentPackageId = e.StudentPackageId,
                PackageName = e.StudentPackage != null ? e.StudentPackage.Package.Name : null,
                EnrollmentDate = e.EnrollmentDate,
                Status = (int)e.Status,
                StatusName = e.Status.ToString(),
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminClassEnrollmentResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<AdminWorkshopRegistrationResponse>> GetWorkshopBookingsAsync(
        int page,
        int pageSize,
        Guid? workshopId,
        Guid? studentId,
        WorkshopBookingStatus? status,
        string? search,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.WorkshopBookings
            .AsNoTracking()
            .Include(b => b.Workshop)
            .Include(b => b.StudentProfile)
            .ThenInclude(s => s.User)
            .Include(b => b.BookingSessions)
                .ThenInclude(bs => bs.WorkshopSession)
                    .ThenInclude(s => s!.TrainerProfile)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.WorkshopSession)
                    .ThenInclude(s => s!.TrainerProfile)
            .AsQueryable();

        if (workshopId.HasValue)
            query = query.Where(b => b.WorkshopId == workshopId.Value);

        if (studentId.HasValue)
            query = query.Where(b => b.StudentProfileId == studentId.Value);

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(b =>
                b.StudentProfile.User.FullName.ToLower().Contains(s) ||
                b.StudentProfile.User.Phone.Contains(s) ||
                b.Workshop.Title.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.BookedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new AdminWorkshopRegistrationResponse
            {
                BookingId = b.Id,
                WorkshopId = b.WorkshopId,
                WorkshopTitle = b.Workshop.Title,
                StudentId = b.StudentProfileId,
                StudentName = (b.StudentProfile != null && b.StudentProfile.User != null && !string.IsNullOrEmpty(b.StudentProfile.User.FullName)) ? b.StudentProfile.User.FullName : (b.GuestName ?? "Workshop Attendee"),
                StudentPhone = (b.StudentProfile != null && b.StudentProfile.User != null && !string.IsNullOrEmpty(b.StudentProfile.User.Phone)) ? b.StudentProfile.User.Phone : (b.GuestPhone ?? ""),
                StudentEmail = (b.GuestEmail != null && b.GuestEmail != "") ? b.GuestEmail : (b.StudentProfile != null && b.StudentProfile.User != null ? b.StudentProfile.User.Email : ""),
                Status = b.Status.ToString(),
                BookedAt = b.BookedAt,
                PaymentTransactionId = b.PaymentTransactionId,
                PaymentStatus = b.PaymentTransactionId.HasValue ? "Paid" : "Free/Pending",
                CustomerCode = (b.StudentProfile != null && b.StudentProfile.User != null && b.StudentProfile.User.CustomerCode != null) ? b.StudentProfile.User.CustomerCode : "GUEST",
                BookingReference = "BK-" + b.Id.ToString().Substring(0, 8).ToUpper(),
                AttendeeType = (b.StudentProfile != null && b.StudentProfile.User != null && b.StudentProfile.User.CustomerCode != null && !b.StudentProfile.User.CustomerCode.StartsWith("GUEST")) ? "ETHOS Student" : "Workshop Attendee",
                IsGuest = b.StudentProfile == null || b.StudentProfile.User == null || b.StudentProfile.User.CustomerCode == null || b.StudentProfile.User.CustomerCode.StartsWith("GUEST"),
                AttendanceStatus = b.Status == WorkshopBookingStatus.Attended ? "Present" : (b.Status == WorkshopBookingStatus.NoShow ? "Absent" : (b.Status == WorkshopBookingStatus.Confirmed ? "Not marked" : b.Status.ToString())),
                FeedbackStatus = "Pending",
                PassName = b.PassName,
                SessionsIncludedCount = b.SessionsIncludedCount,
                Tickets = b.Tickets.OrderBy(t => t.TicketNumber).Select(t => new WorkshopTicketResponse
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
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminWorkshopRegistrationResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task CancelClassEnrollmentAsync(
        Guid enrollmentId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.");

        var enrollment = await _db.ClassEnrollments
            .Include(e => e.DanceClass)
            .Include(e => e.StudentProfile)
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);

        if (enrollment == null)
            throw new ArgumentException("Class enrollment not found.");

        if (enrollment.Status == EnrollmentStatus.Cancelled)
            return; // Idempotent

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        enrollment.Status = EnrollmentStatus.Cancelled;
        enrollment.UpdatedAt = DateTime.UtcNow;

        if (enrollment.StudentPackageId.HasValue)
        {
            var pkg = await _db.StudentPackages
                .FirstOrDefaultAsync(p => p.Id == enrollment.StudentPackageId.Value, cancellationToken);

            if (pkg != null)
            {
                pkg.ClassesUsed = Math.Max(0, pkg.ClassesUsed - 1);
                pkg.UpdatedAt = DateTime.UtcNow;
            }
        }

        _auditService.AddAuditLog(
            adminUserId,
            "CLASS_ENROLLMENT_CANCELLED",
            "ClassEnrollment",
            enrollment.Id,
            $"Cancelled enrollment for {enrollment.StudentProfile?.User?.FullName ?? "Student"} in {enrollment.DanceClass?.Name}: {reason}");

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task CancelWorkshopBookingAsync(
        Guid bookingId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.");

        var booking = await _db.WorkshopBookings
            .Include(b => b.Workshop)
            .Include(b => b.StudentProfile)
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
            throw new ArgumentException("Workshop booking not found.");

        if (booking.Status == WorkshopBookingStatus.Cancelled)
            return; // Idempotent

        booking.Status = WorkshopBookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_BOOKING_CANCELLED",
            "WorkshopBooking",
            booking.Id,
            $"Cancelled workshop booking for {booking.StudentProfile?.User?.FullName ?? "Student"} in {booking.Workshop?.Title}: {reason}");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminClassEnrollmentResponse> ManualEnrollmentAsync(
        Guid adminUserId,
        AdminManualEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var student = await _db.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == request.StudentProfileId, cancellationToken);

        if (student == null)
            throw new ArgumentException("Student not found.");

        var danceClass = await _db.DanceClasses
            .Include(c => c.Schedules)
            .FirstOrDefaultAsync(c => c.Id == request.DanceClassId, cancellationToken);

        if (danceClass == null || !danceClass.IsActive)
            throw new ArgumentException("Dance class not found or inactive.");

        // 1. Duplicate enrollment check
        var isAlreadyEnrolled = await _db.ClassEnrollments
            .AnyAsync(e => e.StudentProfileId == request.StudentProfileId &&
                           e.DanceClassId == request.DanceClassId &&
                           e.Status == EnrollmentStatus.Active, cancellationToken);

        if (isAlreadyEnrolled)
            throw new InvalidOperationException("Student is already actively enrolled in this class.");

        // 2. Atomic capacity check
        var totalCapacity = danceClass.Schedules.Where(s => s.IsActive).Sum(s => s.Capacity);
        if (totalCapacity <= 0) totalCapacity = 30; // Standard room floor

        var currentActiveEnrollments = await _db.ClassEnrollments
            .CountAsync(e => e.DanceClassId == request.DanceClassId && e.Status == EnrollmentStatus.Active, cancellationToken);

        if (currentActiveEnrollments >= totalCapacity)
            throw new InvalidOperationException($"Class has reached maximum capacity ({totalCapacity}).");

        // 3. Package quota check
        Guid? packageId = request.StudentPackageId;
        StudentPackage? targetPkg = null;

        if (packageId.HasValue)
        {
            targetPkg = await _db.StudentPackages
                .Include(p => p.Package)
                .FirstOrDefaultAsync(p => p.Id == packageId.Value &&
                                          p.StudentProfileId == request.StudentProfileId &&
                                          p.Status == StudentPackageStatus.Active, cancellationToken);

            if (targetPkg == null)
                throw new ArgumentException("Specified student package was not found or is inactive.");
        }
        else
        {
            targetPkg = await _db.StudentPackages
                .Include(p => p.Package)
                .Where(p => p.StudentProfileId == request.StudentProfileId &&
                            p.Status == StudentPackageStatus.Active &&
                            (!p.ClassesAllowed.HasValue || p.ClassesUsed < p.ClassesAllowed.Value))
                .OrderBy(p => p.ExpiryDate)
                .FirstOrDefaultAsync(cancellationToken);
            packageId = targetPkg?.Id;
        }

        bool hasRemainingClasses = targetPkg != null && (!targetPkg.ClassesAllowed.HasValue || targetPkg.ClassesUsed < targetPkg.ClassesAllowed.Value);

        if (!hasRemainingClasses)
        {
            if (!request.AllowPackageBypass)
            {
                throw new InvalidOperationException("Student does not have an active package with remaining classes. To override, set AllowPackageBypass with a mandatory reason.");
            }

            if (string.IsNullOrWhiteSpace(request.BypassReason))
            {
                throw new ArgumentException("BypassReason is required when AllowPackageBypass is enabled.");
            }
        }

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (targetPkg != null)
        {
            targetPkg.ClassesUsed++;
            targetPkg.UpdatedAt = DateTime.UtcNow;
        }

        var enrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            StudentProfileId = request.StudentProfileId,
            DanceClassId = request.DanceClassId,
            StudentPackageId = packageId,
            EnrollmentDate = DateTime.UtcNow,
            Status = EnrollmentStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ClassEnrollments.Add(enrollment);

        var actionType = request.AllowPackageBypass ? "CLASS_MANUAL_ENROLLMENT_OVERRIDE" : "CLASS_MANUAL_ENROLLMENT";
        var auditNote = request.AllowPackageBypass
            ? $"Admin manually enrolled {student.User.FullName} into {danceClass.Name} with package bypass: {request.BypassReason}"
            : $"Admin manually enrolled {student.User.FullName} into {danceClass.Name}";

        _auditService.AddAuditLog(
            adminUserId,
            actionType,
            "ClassEnrollment",
            enrollment.Id,
            auditNote);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new AdminClassEnrollmentResponse
        {
            Id = enrollment.Id,
            StudentProfileId = student.Id,
            StudentName = student.User.FullName,
            StudentPhone = student.User.Phone,
            StudentCustomerCode = student.User.CustomerCode,
            DanceClassId = danceClass.Id,
            DanceClassName = danceClass.Name,
            StudentPackageId = packageId,
            PackageName = targetPkg?.Package?.Name,
            EnrollmentDate = enrollment.EnrollmentDate,
            Status = (int)enrollment.Status,
            StatusName = enrollment.Status.ToString(),
            CreatedAt = enrollment.CreatedAt
        };
    }

    public async Task UpdateWorkshopBookingContactAsync(
        Guid bookingId,
        Guid adminUserId,
        string phone,
        string? email,
        string? name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("WhatsApp Phone number is required.");

        var booking = await _db.WorkshopBookings
            .Include(b => b.StudentProfile)
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
            throw new ArgumentException("Workshop booking not found.");

        var oldPhone = booking.GuestPhone;
        booking.GuestPhone = phone.Trim();
        if (!string.IsNullOrWhiteSpace(email)) booking.GuestEmail = email.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(name)) booking.GuestName = name.Trim();

        if (booking.StudentProfile?.User != null)
        {
            booking.StudentProfile.User.Phone = phone.Trim();
            if (!string.IsNullOrWhiteSpace(email)) booking.StudentProfile.User.Email = email.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(name)) booking.StudentProfile.User.FullName = name.Trim();
        }

        var tickets = await _db.WorkshopTickets
            .Where(t => t.WorkshopBookingId == bookingId)
            .ToListAsync(cancellationToken);

        foreach (var ticket in tickets)
        {
            ticket.AttendeePhone = phone.Trim();
            if (!string.IsNullOrWhiteSpace(name)) ticket.AttendeeName = name.Trim();
        }

        _auditService.AddAuditLog(
            adminUserId,
            "ADMIN_BOOKING_CUSTOMER_UPDATED",
            "WorkshopBooking",
            booking.Id,
            $"Updated contact details for booking {booking.Id}: Name '{booking.GuestName}', Phone '{booking.GuestPhone}', Email '{booking.GuestEmail}'. Previous Phone: '{oldPhone}'.");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> ResendWhatsAppTicketAsync(
        Guid bookingId,
        Guid adminUserId,
        string? overridePhone,
        string? templateType,
        CancellationToken cancellationToken)
    {
        var booking = await _db.WorkshopBookings
            .Include(b => b.Workshop)
            .Include(b => b.StudentProfile)
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
            throw new ArgumentException("Workshop booking not found.");

        var ticket = await _db.WorkshopTickets
            .FirstOrDefaultAsync(t => t.WorkshopBookingId == bookingId, cancellationToken);

        var recipientPhone = !string.IsNullOrWhiteSpace(overridePhone)
            ? overridePhone.Trim()
            : (!string.IsNullOrWhiteSpace(ticket?.AttendeePhone)
                ? ticket.AttendeePhone
                : booking.GuestPhone ?? booking.StudentProfile?.User?.Phone);

        if (string.IsNullOrWhiteSpace(recipientPhone))
            throw new InvalidOperationException("No valid recipient WhatsApp phone number available for resending.");

        var normalizedType = (templateType ?? "pdf").Trim().ToLowerInvariant();

        if (normalizedType == "confirmed" || normalizedType == "booking_confirmed" || normalizedType == "both")
        {
            var confKey = $"wapp_conf_{booking.Id}_{Guid.NewGuid():N}";
            var confNotification = new WhatsAppNotification
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                WorkshopTicketId = ticket?.Id,
                NotificationType = WhatsAppNotificationType.BookingConfirmed,
                RecipientPhone = recipientPhone,
                IdempotencyKey = confKey,
                Status = WhatsAppNotificationStatus.Pending,
                Attempts = 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.WhatsAppNotifications.Add(confNotification);
        }

        if (normalizedType == "pdf" || normalizedType == "ticket_pdf" || normalizedType == "both")
        {
            var resendKey = $"wapp_resend_{booking.Id}_{Guid.NewGuid():N}";
            var pdfNotification = new WhatsAppNotification
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                WorkshopTicketId = ticket?.Id,
                NotificationType = WhatsAppNotificationType.TicketPdf,
                RecipientPhone = recipientPhone,
                IdempotencyKey = resendKey,
                Status = WhatsAppNotificationStatus.Pending,
                Attempts = 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.WhatsAppNotifications.Add(pdfNotification);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_TICKET_RESENT_WHATSAPP",
            "WorkshopBooking",
            booking.Id,
            $"Enqueued {normalizedType} WhatsApp resend to {recipientPhone} for booking {booking.Id}");

        return recipientPhone;
    }

    public async Task<AdminModifyBookingSessionResponse> ModifyWorkshopBookingSessionAsync(
        Guid bookingId,
        Guid adminUserId,
        AdminModifyBookingSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var booking = await _db.WorkshopBookings
            .Include(b => b.Workshop)
            .Include(b => b.WorkshopPassType)
            .Include(b => b.StudentProfile)
                .ThenInclude(s => s.User)
            .Include(b => b.BookingSessions)
                .ThenInclude(bs => bs.WorkshopSession)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
            throw new ArgumentException("Workshop booking was not found.");

        if (booking.Status != WorkshopBookingStatus.Confirmed && booking.Status != WorkshopBookingStatus.Attended)
            throw new InvalidOperationException("Only confirmed or attended workshop bookings can have sessions modified.");

        // Boundary: Single-Session and All-Access passes do not support individual session replacement
        if (booking.WorkshopPassTypeId.HasValue && booking.WorkshopPassType != null)
        {
            if (!booking.WorkshopPassType.SessionsIncluded.HasValue)
            {
                throw new InvalidOperationException("Overall Pass / All-Access Pass covers the entire workshop curriculum and does not support individual session replacement.");
            }
            if (booking.WorkshopPassType.SessionsIncluded == 1 || booking.WorkshopPassType.WorkshopSessionId.HasValue)
            {
                throw new InvalidOperationException("Single-Session passes are bound to a specific session and do not support session replacement. Only Multi-Session Bundles support session transfer.");
            }
        }

        var currentTicket = await _db.WorkshopTickets
            .Include(t => t.WorkshopSession)
            .FirstOrDefaultAsync(t => t.WorkshopBookingId == bookingId &&
                ((request.CurrentTicketId.HasValue && request.CurrentTicketId.Value != Guid.Empty && t.Id == request.CurrentTicketId.Value) ||
                 (request.CurrentSessionId.HasValue && request.CurrentSessionId.Value != Guid.Empty && t.WorkshopSessionId == request.CurrentSessionId.Value)), cancellationToken);

        if (currentTicket == null)
            throw new ArgumentException("The specified ticket was not found for this booking.");

        if (currentTicket.Status != TicketStatus.Issued)
            throw new InvalidOperationException($"Ticket {currentTicket.TicketNumber} cannot be replaced because its status is {currentTicket.Status}.");

        if (currentTicket.CheckedInAt.HasValue)
            throw new InvalidOperationException($"Ticket {currentTicket.TicketNumber} has already been checked in.");

        var replacementSession = await _db.WorkshopSessions
            .Include(s => s.TrainerProfile)
            .FirstOrDefaultAsync(s => s.Id == request.ReplacementSessionId, cancellationToken);

        if (replacementSession == null)
            throw new ArgumentException("Replacement session was not found.");

        if (replacementSession.WorkshopId != booking.WorkshopId)
            throw new InvalidOperationException("Replacement session must belong to the same workshop.");

        if (!replacementSession.IsActive)
            throw new InvalidOperationException("Replacement session is not active.");

        var currentSessionId = currentTicket.WorkshopSessionId;
        if (currentSessionId.HasValue && currentSessionId.Value == replacementSession.Id)
            throw new InvalidOperationException("Replacement session cannot be the same as the current session.");

        // Active booking sessions for this booking
        var activeBookingSessions = booking.BookingSessions
            .Where(bs => bs.Status == WorkshopBookingSessionStatus.Booked)
            .ToList();

        var currentBookingSession = activeBookingSessions.FirstOrDefault(bs =>
            (bs.WorkshopTicketId == currentTicket.Id || (currentSessionId.HasValue && bs.WorkshopSessionId == currentSessionId.Value)));

        if (currentBookingSession == null)
        {
            throw new InvalidOperationException("Active booking session for this ticket was not found.");
        }

        // Full post-replacement bundle validation
        var remainingActiveSessions = activeBookingSessions
            .Where(bs => bs.Id != currentBookingSession.Id)
            .Select(bs => bs.WorkshopSession ?? _db.WorkshopSessions.FirstOrDefault(ws => ws.Id == bs.WorkshopSessionId)!)
            .ToList();

        var resultingSessions = new List<WorkshopSession>(remainingActiveSessions) { replacementSession };

        // 1. Exact N check
        var expectedN = booking.SessionsIncludedCount ?? booking.WorkshopPassType?.SessionsIncluded ?? resultingSessions.Count;
        if (resultingSessions.Count != expectedN)
        {
            throw new InvalidOperationException($"Resulting bundle must contain exactly {expectedN} sessions, but contains {resultingSessions.Count}.");
        }

        // 2. Distinctness check (no duplicates)
        if (resultingSessions.Select(s => s.Id).Distinct().Count() != resultingSessions.Count)
        {
            throw new InvalidOperationException("The attendee is already booked into this replacement session.");
        }

        // 3. Status and workshop scope
        if (resultingSessions.Any(s => s.WorkshopId != booking.WorkshopId || !s.IsActive))
        {
            throw new InvalidOperationException("All sessions in the resulting bundle must belong to the workshop and be active.");
        }

        // 4. Overlap validation among resulting bundle sessions
        for (int i = 0; i < resultingSessions.Count; i++)
        {
            var s1 = resultingSessions[i];
            for (int j = i + 1; j < resultingSessions.Count; j++)
            {
                var s2 = resultingSessions[j];
                if (s1.SessionDate.Date == s2.SessionDate.Date)
                {
                    if (s1.StartTime < s2.EndTime && s2.StartTime < s1.EndTime)
                    {
                        throw new InvalidOperationException($"Selected sessions '{s1.Title}' and '{s2.Title}' overlap in time on {s1.SessionDate:yyyy-MM-dd}.");
                    }
                }
            }
        }

        var nowUtc = DateTime.UtcNow;

        var cutoffUtc = replacementSession.GetBookingCutoffUtc(booking.Workshop?.Timezone ?? "Asia/Kolkata");
        var isCutoffPassed = nowUtc >= cutoffUtc;
        if (isCutoffPassed)
        {
            if (!request.OverrideCutoff)
            {
                throw new InvalidOperationException($"The booking cutoff for session '{replacementSession.Title}' has passed. Administrator override is required.");
            }
            if (string.IsNullOrWhiteSpace(request.OverrideReason) || request.OverrideReason.Trim().Length < 5)
            {
                throw new InvalidOperationException("A valid reason of at least 5 characters is required when overriding session cutoff.");
            }
        }

        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (_db.Database.IsNpgsql())
        {
            var lockSessionIds = currentSessionId.HasValue
                ? new[] { currentSessionId.Value, replacementSession.Id }.Distinct().OrderBy(id => id).ToList()
                : new[] { replacementSession.Id }.ToList();

            var idListStr = string.Join(",", lockSessionIds.Select(id => $"'{id}'::uuid"));
            await _db.Database.ExecuteSqlRawAsync(
                $"SELECT \"Id\" FROM workshop_sessions WHERE \"Id\" = ANY(ARRAY[{idListStr}]) ORDER BY \"Id\" FOR UPDATE",
                cancellationToken);
        }

        var bookedSeats = await _db.WorkshopBookingSessions
            .Where(bs => bs.WorkshopSessionId == replacementSession.Id &&
                         bs.Status == WorkshopBookingSessionStatus.Booked &&
                         (bs.WorkshopBooking.Status == WorkshopBookingStatus.Confirmed ||
                          bs.WorkshopBooking.Status == WorkshopBookingStatus.Attended ||
                          (bs.WorkshopBooking.Status == WorkshopBookingStatus.PendingPayment &&
                           bs.WorkshopBooking.ReservationExpiresAt > nowUtc)))
            .CountAsync(cancellationToken);

        var remainingCapacity = replacementSession.Capacity - bookedSeats;
        if (remainingCapacity < 1)
        {
            throw new InvalidOperationException($"Replacement session '{replacementSession.Title}' has reached maximum capacity ({replacementSession.Capacity} seats).");
        }

        currentBookingSession.Status = WorkshopBookingSessionStatus.Replaced;
        currentBookingSession.ReplacedAt = nowUtc;
        currentBookingSession.ReplacedByAdminId = adminUserId;
        currentBookingSession.OverrideReason = request.OverrideReason?.Trim();
        currentBookingSession.UpdatedAt = nowUtc;

        currentTicket.Status = TicketStatus.Replaced;

        var newBookingSession = new WorkshopBookingSession
        {
            Id = Guid.NewGuid(),
            WorkshopBookingId = booking.Id,
            WorkshopSessionId = replacementSession.Id,
            Status = WorkshopBookingSessionStatus.Booked,
            OriginalSessionId = currentSessionId,
            ReplacedAt = nowUtc,
            ReplacedByAdminId = adminUserId,
            CutoffOverrideUsed = isCutoffPassed,
            OverrideReason = request.OverrideReason?.Trim(),
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
        _db.WorkshopBookingSessions.Add(newBookingSession);

        var originalTicketNumber = currentTicket.TicketNumber;
        var baseTicketNumber = originalTicketNumber.Contains("-R")
            ? originalTicketNumber[..originalTicketNumber.IndexOf("-R")]
            : originalTicketNumber;

        var existingRevisions = await _db.WorkshopTickets
            .Where(t => t.WorkshopBookingId == booking.Id && t.TicketNumber.StartsWith(baseTicketNumber + "-R"))
            .CountAsync(cancellationToken);

        var revisionNumber = existingRevisions + 1;
        var newTicketNumber = $"{baseTicketNumber}-R{revisionNumber:D2}";

        var newTicket = new WorkshopTicket
        {
            Id = Guid.NewGuid(),
            TicketNumber = newTicketNumber,
            WorkshopBookingId = booking.Id,
            WorkshopId = booking.WorkshopId,
            WorkshopSessionId = replacementSession.Id,
            UserId = currentTicket.UserId,
            PaymentTransactionId = currentTicket.PaymentTransactionId,
            AttendeeName = currentTicket.AttendeeName,
            AttendeePhone = currentTicket.AttendeePhone,
            AttendeeEmail = currentTicket.AttendeeEmail,
            IsPrimaryAttendee = currentTicket.IsPrimaryAttendee,
            Status = TicketStatus.Issued,
            IssuedAt = nowUtc,
            Workshop = booking.Workshop,
            WorkshopSession = replacementSession
        };

        var rawQrToken = _ticketService.DeriveQrToken(newTicket);
        newTicket.QrTokenHash = _ticketService.ComputeTokenHash(rawQrToken);

        _db.WorkshopTickets.Add(newTicket);
        newBookingSession.WorkshopTicketId = newTicket.Id;

        // Invalidate and purge old PDF cache for replaced ticket
        var oldPdfs = await _db.TicketPdfs.Where(p => p.TicketId == currentTicket.Id).ToListAsync(cancellationToken);
        if (oldPdfs.Count > 0)
        {
            _db.TicketPdfs.RemoveRange(oldPdfs);
        }

        var auditMeta = System.Text.Json.JsonSerializer.Serialize(new
        {
            AdminUserId = adminUserId,
            BookingId = booking.Id,
            OldSessionId = currentSessionId,
            ReplacementSessionId = replacementSession.Id,
            OldTicketId = currentTicket.Id,
            NewTicketId = newTicket.Id,
            OverrideCutoff = isCutoffPassed,
            Reason = request.OverrideReason,
            TimestampUtc = nowUtc
        });

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_SESSION_MODIFIED",
            "WorkshopBooking",
            booking.Id,
            $"Admin modified session for booking {booking.Id}. Old ticket {currentTicket.TicketNumber} (Status: Replaced) swapped for session '{replacementSession.Title}' with new ticket {newTicket.TicketNumber}. Override used: {isCutoffPassed}. Reason: {request.OverrideReason}",
            metadataJson: auditMeta);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        try
        {
            await _ticketPdfService.GetOrCreateTicketPdfAsync(newTicket, booking.Workshop, booking, rawQrToken, cancellationToken);
        }
        catch
        {
            // outbox / retry will handle
        }

        var recipientPhone = !string.IsNullOrWhiteSpace(newTicket.AttendeePhone)
            ? newTicket.AttendeePhone
            : (!string.IsNullOrWhiteSpace(booking.GuestPhone) ? booking.GuestPhone : booking.StudentProfile?.User?.Phone);

        if (!string.IsNullOrWhiteSpace(recipientPhone))
        {
            var ticketKey = $"wapp_ticket_mod_{newTicket.Id}";
            _db.WhatsAppNotifications.Add(new WhatsAppNotification
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                WorkshopTicketId = newTicket.Id,
                NotificationType = WhatsAppNotificationType.TicketPdf,
                RecipientPhone = recipientPhone,
                IdempotencyKey = ticketKey,
                Status = WhatsAppNotificationStatus.Pending,
                Attempts = 0,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new AdminModifyBookingSessionResponse
        {
            BookingId = booking.Id,
            OldTicketId = currentTicket.Id,
            NewTicketId = newTicket.Id,
            NewTicketNumber = newTicket.TicketNumber,
            ReplacementSessionId = replacementSession.Id,
            Message = $"Successfully transferred seat to session '{replacementSession.Title}'. New ticket {newTicket.TicketNumber} issued."
        };
    }
}