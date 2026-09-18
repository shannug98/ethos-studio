using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminBookingService : IAdminBookingService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public AdminBookingService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
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
                StudentName = b.StudentProfile.User.FullName,
                StudentPhone = b.StudentProfile.User.Phone,
                StudentEmail = b.StudentProfile.User.Email,
                Status = b.Status.ToString(),
                BookedAt = b.BookedAt,
                PaymentTransactionId = b.PaymentTransactionId,
                PaymentStatus = b.PaymentTransactionId.HasValue ? "Paid" : "Free/Pending",
                CustomerCode = b.StudentProfile.User.CustomerCode ?? "GUEST",
                BookingReference = "BK-" + b.Id.ToString().Substring(0, 8).ToUpper(),
                AttendeeType = (b.StudentProfile.User.CustomerCode != null && b.StudentProfile.User.CustomerCode.StartsWith("GUEST")) ? "Workshop Attendee" : "ETHOS Student",
                IsGuest = b.StudentProfile.User.CustomerCode != null && b.StudentProfile.User.CustomerCode.StartsWith("GUEST"),
                AttendanceStatus = b.Status == WorkshopBookingStatus.Attended ? "Present" : (b.Status == WorkshopBookingStatus.NoShow ? "Absent" : (b.Status == WorkshopBookingStatus.Confirmed ? "Not marked" : b.Status.ToString())),
                FeedbackStatus = "Pending"
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
            "WORKSHOP_BOOKING_CONTACT_UPDATED",
            "WorkshopBooking",
            booking.Id,
            $"Updated contact details for booking {booking.Id}: phone changed from '{oldPhone}' to '{phone}'");

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> ResendWhatsAppTicketAsync(
        Guid bookingId,
        Guid adminUserId,
        string? overridePhone,
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

        var resendKey = $"wapp_resend_{booking.Id}_{Guid.NewGuid():N}";
        var outboxItem = new WhatsAppNotification
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

        _db.WhatsAppNotifications.Add(outboxItem);
        await _db.SaveChangesAsync(cancellationToken);

        _auditService.AddAuditLog(
            adminUserId,
            "WORKSHOP_TICKET_RESENT_WHATSAPP",
            "WorkshopBooking",
            booking.Id,
            $"Enqueued ticket PDF WhatsApp resend to {recipientPhone} for booking {booking.Id}");

        return recipientPhone;
    }
}