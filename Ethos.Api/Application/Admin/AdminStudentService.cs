using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Classes;
using Ethos.Api.Contracts.Packages;
using Ethos.Api.Contracts.Payments;
using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminStudentService : IAdminStudentService
{
    private readonly AppDbContext _db;

    public AdminStudentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AdminStudentListResponse>> GetStudentsAsync(
        int page,
        int pageSize,
        string? search,
        string? profileStatus,
        string? accountStatus,
        string? packageStatus,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = DateTime.UtcNow;

        var query = _db.StudentProfiles
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.StudentPackages)
                .ThenInclude(spk => spk.Package)
            .Include(s => s.AttendanceRecords)
            .Where(sp => sp.User.UserRoles.Any(ur => ur.Role.Code == "STUDENT"))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(sp =>
                sp.User.FullName.ToLower().Contains(s) ||
                sp.User.Phone.Contains(s) ||
                (sp.User.Email != null && sp.User.Email.ToLower().Contains(s)) ||
                sp.User.CustomerCode.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(accountStatus) && accountStatus != "ALL")
        {
            if (accountStatus.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
                query = query.Where(sp => sp.User.IsActive);
            else if (accountStatus.Equals("SUSPENDED", StringComparison.OrdinalIgnoreCase) || accountStatus.Equals("INACTIVE", StringComparison.OrdinalIgnoreCase))
                query = query.Where(sp => !sp.User.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(profileStatus) && profileStatus != "ALL")
        {
            if (profileStatus.Equals("COMPLETE", StringComparison.OrdinalIgnoreCase))
                query = query.Where(sp => !string.IsNullOrWhiteSpace(sp.DateOfBirth) && !string.IsNullOrWhiteSpace(sp.City) && !string.IsNullOrWhiteSpace(sp.EmergencyContactPhone));
            else if (profileStatus.Equals("INCOMPLETE", StringComparison.OrdinalIgnoreCase))
                query = query.Where(sp => string.IsNullOrWhiteSpace(sp.DateOfBirth) || string.IsNullOrWhiteSpace(sp.City) || string.IsNullOrWhiteSpace(sp.EmergencyContactPhone));
        }

        if (!string.IsNullOrWhiteSpace(packageStatus) && packageStatus != "ALL")
        {
            if (packageStatus.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(sp => sp.StudentPackages.Any(p => p.Status == StudentPackageStatus.Active && p.ExpiryDate >= now));
            }
            else if (packageStatus.Equals("NONE", StringComparison.OrdinalIgnoreCase) || packageStatus.Equals("EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(sp => !sp.StudentPackages.Any(p => p.Status == StudentPackageStatus.Active && p.ExpiryDate >= now));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var students = await query
            .OrderByDescending(sp => sp.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = students.Select(sp =>
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(sp.DateOfBirth)) missing.Add("Date of Birth");
            if (string.IsNullOrWhiteSpace(sp.City)) missing.Add("City");
            if (string.IsNullOrWhiteSpace(sp.EmergencyContactName)) missing.Add("Emergency Contact");
            if (string.IsNullOrWhiteSpace(sp.EmergencyContactPhone)) missing.Add("Emergency Phone");

            var activePkg = sp.StudentPackages
                .Where(p => p.Status == StudentPackageStatus.Active && p.ExpiryDate >= now)
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefault();

            return new AdminStudentListResponse
            {
                StudentId = sp.Id,
                UserId = sp.UserId,
                CustomerCode = sp.User.CustomerCode,
                FullName = sp.User.FullName,
                Phone = sp.User.Phone,
                Email = sp.User.Email,
                IsActive = sp.User.IsActive,
                ProfileCompleted = missing.Count == 0,
                City = sp.City,
                CreatedAt = sp.CreatedAt,
                ActivePackageName = activePkg?.Package?.Name,
                ClassesAllowed = activePkg?.ClassesAllowed,
                ClassesUsed = activePkg?.ClassesUsed ?? 0,
                TotalAttendanceCount = sp.AttendanceRecords?.Count ?? 0,
                MissingFieldsCount = missing.Count,
                MissingFields = missing
            };
        }).ToList();

        return new PagedResult<AdminStudentListResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminStudentSummaryStatsResponse> GetStudentStatsAsync(
        CancellationToken cancellationToken)
    {
        var profiles = await _db.StudentProfiles
            .AsNoTracking()
            .Include(s => s.User)
            .Select(s => new
            {
                s.DateOfBirth,
                s.City,
                s.EmergencyContactPhone,
                s.User.IsActive
            })
            .ToListAsync(cancellationToken);

        var total = profiles.Count;
        var active = profiles.Count(p => p.IsActive);
        var complete = profiles.Count(p => !string.IsNullOrWhiteSpace(p.DateOfBirth) && !string.IsNullOrWhiteSpace(p.City) && !string.IsNullOrWhiteSpace(p.EmergencyContactPhone));
        var incomplete = total - complete;

        return new AdminStudentSummaryStatsResponse
        {
            TotalStudents = total,
            ActiveStudents = active,
            CompleteProfiles = complete,
            IncompleteProfiles = incomplete
        };
    }

    public async Task<AdminStudentDetailsResponse?> GetStudentByIdAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var sp = await _db.StudentProfiles
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == studentId || s.UserId == studentId, cancellationToken);

        if (sp == null) return null;

        var now = DateTime.UtcNow;

        // Missing fields calculation
        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(sp.DateOfBirth)) missingFields.Add("Date of Birth");
        if (string.IsNullOrWhiteSpace(sp.City)) missingFields.Add("City");
        if (string.IsNullOrWhiteSpace(sp.EmergencyContactName)) missingFields.Add("Emergency Contact");
        if (string.IsNullOrWhiteSpace(sp.EmergencyContactPhone)) missingFields.Add("Emergency Phone");

        // Packages
        var studentPackages = await _db.StudentPackages
            .AsNoTracking()
            .Include(spk => spk.Package)
            .Where(spk => spk.StudentProfileId == sp.Id)
            .OrderByDescending(spk => spk.StartDate)
            .ToListAsync(cancellationToken);

        var packageDtos = studentPackages.Select(spk => new StudentPackageResponse
        {
            Id = spk.Id,
            PackageId = spk.PackageId,
            PackageName = spk.Package?.Name ?? "Dance Package",
            StartDate = spk.StartDate,
            ExpiryDate = spk.ExpiryDate,
            Status = spk.Status,
            ClassesAllowed = spk.ClassesAllowed,
            ClassesUsed = spk.ClassesUsed,
            IsActive = spk.Status == StudentPackageStatus.Active && spk.ExpiryDate >= now
        }).ToList();

        var activePackage = packageDtos.FirstOrDefault(p => p.IsActive);

        // Class Enrollments
        var enrollments = await _db.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.DanceClass)
            .Where(e => e.StudentProfileId == sp.Id)
            .OrderByDescending(e => e.EnrollmentDate)
            .Select(e => new ClassEnrollmentResponse
            {
                Id = e.Id,
                DanceClassId = e.DanceClassId,
                DanceClassName = e.DanceClass.Name,
                DanceStyle = e.DanceClass.DanceStyle,
                StudentPackageId = e.StudentPackageId,
                EnrollmentDate = e.EnrollmentDate,
                Status = e.Status
            })
            .ToListAsync(cancellationToken);

        // Attendance Records
        var attendanceRecords = await _db.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.ClassSession)
                .ThenInclude(cs => cs.ClassSchedule)
                    .ThenInclude(sch => sch.DanceClass)
            .Where(a => a.StudentProfileId == sp.Id)
            .OrderByDescending(a => a.MarkedAt)
            .Select(a => new AdminStudentAttendanceItemResponse
            {
                Id = a.Id,
                ClassSessionId = a.ClassSessionId,
                ClassName = a.ClassSession.ClassSchedule.DanceClass.Name,
                DanceStyle = a.ClassSession.ClassSchedule.DanceClass.DanceStyle,
                SessionDate = a.ClassSession.SessionDate,
                Status = a.Status.ToString(),
                MarkedAt = a.MarkedAt,
                Notes = a.Notes
            })
            .ToListAsync(cancellationToken);

        // Workshop Bookings
        var rawBookings = await _db.WorkshopBookings
            .AsNoTracking()
            .Include(wb => wb.Workshop)
                .ThenInclude(w => w.TrainerProfile)
            .Where(wb => wb.StudentProfileId == sp.Id)
            .OrderByDescending(wb => wb.BookedAt)
            .ToListAsync(cancellationToken);

        var studentPaymentTxList = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.UserId == sp.UserId && p.Purpose == PaymentPurpose.WorkshopBooking)
            .ToListAsync(cancellationToken);

        var bookingIds = rawBookings.Select(b => b.Id).ToList();
        var feedbacks = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Where(f => f.WorkshopBookingId.HasValue && bookingIds.Contains(f.WorkshopBookingId.Value))
            .ToListAsync(cancellationToken);

        var tokens = await _db.WorkshopFeedbackTokens
            .AsNoTracking()
            .Where(t => bookingIds.Contains(t.WorkshopBookingId))
            .ToListAsync(cancellationToken);

        var workshopBookings = rawBookings.Select(wb =>
        {
            var tx = wb.PaymentTransactionId.HasValue 
                ? studentPaymentTxList.FirstOrDefault(p => p.Id == wb.PaymentTransactionId.Value)
                : studentPaymentTxList.FirstOrDefault(p => p.ReferenceId == wb.Id || p.ReferenceId == wb.WorkshopId);

            var bookingStatusString = wb.Status switch
            {
                WorkshopBookingStatus.PendingPayment => "Payment Pending",
                WorkshopBookingStatus.Confirmed => "Booked — Attendance Pending",
                WorkshopBookingStatus.Cancelled => "Cancelled",
                WorkshopBookingStatus.Attended => "Attended",
                WorkshopBookingStatus.NoShow => "No-show",
                _ => wb.Status.ToString()
            };

            var nowUtc = DateTime.UtcNow;
            var workshopEndUtc = DateTime.SpecifyKind(wb.Workshop.WorkshopDate.Date.Add(wb.Workshop.EndTime), DateTimeKind.Utc);

            var attendanceStatusString = wb.Status switch
            {
                WorkshopBookingStatus.Attended => "Attended",
                WorkshopBookingStatus.NoShow => "No-show",
                WorkshopBookingStatus.Cancelled => "Cancelled",
                WorkshopBookingStatus.PendingPayment => "Payment Pending",
                _ => workshopEndUtc > nowUtc ? "Scheduled" : "Not Attended"
            };

            var tok = tokens.FirstOrDefault(t => t.WorkshopBookingId == wb.Id);

            string feedbackStatus;
            int? feedbackRating = null;

            if (wb.Status != WorkshopBookingStatus.Attended)
            {
                // CRITICAL RULE: Under no circumstances allow a rating if not Attended!
                feedbackRating = null;

                if (wb.Status is WorkshopBookingStatus.Cancelled or WorkshopBookingStatus.PendingPayment)
                {
                    feedbackStatus = "Ineligible";
                }
                else if (workshopEndUtc > nowUtc)
                {
                    feedbackStatus = "Available after workshop";
                }
                else
                {
                    feedbackStatus = "Not Available";
                }
            }
            else
            {
                // Attendee actually attended! Check for valid feedback
                var validFb = feedbacks.FirstOrDefault(f => f.WorkshopBookingId == wb.Id && f.IsValid);
                if (validFb != null)
                {
                    feedbackStatus = "Submitted";
                    feedbackRating = validFb.Rating;
                }
                else
                {
                    feedbackStatus = "Pending";
                    feedbackRating = null;
                }
            }

            return new AdminStudentWorkshopBookingItemResponse
            {
                Id = wb.Id,
                WorkshopId = wb.WorkshopId,
                WorkshopTitle = wb.Workshop.Title,
                TrainerName = wb.Workshop.TrainerProfile?.FullName ?? "Staff Trainer",
                WorkshopDate = wb.Workshop.WorkshopDate,
                StartTime = wb.Workshop.StartTime,
                EndTime = wb.Workshop.EndTime,
                DanceStyle = wb.Workshop.DanceStyle,
                Venue = wb.Workshop.Venue,
                Amount = wb.Workshop.AdminApprovedPrice ?? wb.Workshop.TrainerProposedPrice ?? wb.Workshop.Price,
                Status = bookingStatusString,
                StatusCode = (int)wb.Status,
                BookedAt = wb.BookedAt,
                PaymentTransactionId = wb.PaymentTransactionId ?? tx?.Id,
                PaymentStatus = tx != null ? (tx.Status == PaymentStatus.Paid ? "Paid" : tx.Status.ToString()) : (wb.Status == WorkshopBookingStatus.Confirmed || wb.Status == WorkshopBookingStatus.Attended ? "Paid" : "Pending"),
                RazorpayOrderId = tx?.RazorpayOrderId,
                RazorpayPaymentId = tx?.RazorpayPaymentId,
                AttendanceStatus = attendanceStatusString,
                FeedbackStatus = feedbackStatus,
                FeedbackRating = feedbackRating,
                FeedbackToken = tok != null ? tok.Id.ToString() : null
            };
        }).ToList();

        // Submitted Feedback: Only valid feedbacks from attended workshops
        var feedbackList = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Include(f => f.Workshop)
            .Include(f => f.WorkshopBooking)
            .Where(f => f.StudentProfileId == sp.Id &&
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
                Comment = f.Comment,
                SubmittedAt = f.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        // Payment Transactions
        var payments = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.UserId == sp.UserId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentTransactionResponse
            {
                Id = p.Id,
                Purpose = p.Purpose,
                ReferenceId = p.ReferenceId,
                Amount = p.Amount,
                Currency = p.Currency,
                Status = p.Status,
                RazorpayOrderId = p.RazorpayOrderId,
                RazorpayPaymentId = p.RazorpayPaymentId,
                CreatedAt = p.CreatedAt,
                PaidAt = p.PaidAt
            })
            .ToListAsync(cancellationToken);

        DateTime? dob = null;
        if (DateTime.TryParse(sp.DateOfBirth, out var parsedDob)) dob = parsedDob;

        return new AdminStudentDetailsResponse
        {
            StudentId = sp.Id,
            UserId = sp.UserId,
            CustomerCode = sp.User.CustomerCode,
            FullName = sp.User.FullName,
            Phone = sp.User.Phone,
            Email = sp.User.Email,
            IsActive = sp.User.IsActive,
            ProfileCompleted = missingFields.Count == 0,
            MissingFields = missingFields,
            DateOfBirth = dob,
            Gender = sp.Gender,
            City = sp.City,
            ProfilePhotoUrl = sp.ProfilePhotoUrl,
            EmergencyContactName = sp.EmergencyContactName,
            EmergencyContactPhone = sp.EmergencyContactPhone,
            Bio = sp.Bio,
            CreatedAt = sp.CreatedAt,
            ActivePackage = activePackage,
            PackageHistory = packageDtos,
            ClassEnrollments = enrollments,
            AttendanceRecords = attendanceRecords,
            WorkshopBookings = workshopBookings,
            SubmittedFeedback = feedbackList,
            PaymentTransactions = payments
        };
    }

    public async Task<StudentDiagnosticReport?> GetStudentDiagnosticsAsync(
        Guid studentId,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var sp = await _db.StudentProfiles
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == studentId || s.UserId == studentId, cancellationToken);

        if (sp == null) return null;

        var now = DateTime.UtcNow;
        var twentyFourHoursAgo = now.AddHours(-24);
        var thirtyDaysAgo = now.AddDays(-30);

        var businessIssues = new List<DiagnosticIssue>();
        var technicalFailures = new List<DiagnosticIssue>();

        // 1. Packages Evaluation (Business State)
        var packages = await _db.StudentPackages
            .AsNoTracking()
            .Include(p => p.Package)
            .Where(p => p.StudentProfileId == sp.Id)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var activePackages = packages.Where(p => p.Status == StudentPackageStatus.Active && p.ExpiryDate >= now).ToList();

        if (activePackages.Count == 0)
        {
            var recentExpired = packages.FirstOrDefault(p => p.ExpiryDate < now && p.ExpiryDate >= thirtyDaysAgo);
            if (recentExpired != null)
            {
                businessIssues.Add(new DiagnosticIssue
                {
                    Code = "PACKAGE_EXPIRED",
                    Category = "BUSINESS_STATE",
                    Severity = "WARNING",
                    Title = "Active package expired",
                    Description = $"Student's package '{recentExpired.Package?.Name ?? "Dance Package"}' expired on {recentExpired.ExpiryDate:yyyy-MM-dd}.",
                    Evidence = new
                    {
                        packageId = recentExpired.Id,
                        packageName = recentExpired.Package?.Name,
                        expiryDate = recentExpired.ExpiryDate,
                        classesUsed = recentExpired.ClassesUsed,
                        classesAllowed = recentExpired.ClassesAllowed,
                        currentDate = now
                    },
                    DetectedAt = now,
                    TraceId = traceId,
                    RecommendedAction = "Student's package has expired. Advise student to renew or purchase a new package."
                });
            }
            else
            {
                businessIssues.Add(new DiagnosticIssue
                {
                    Code = "NO_ACTIVE_PACKAGE",
                    Category = "BUSINESS_STATE",
                    Severity = "WARNING",
                    Title = "No active package",
                    Description = "Student currently does not possess any active package.",
                    Evidence = new { totalHistoricalPackages = packages.Count },
                    DetectedAt = now,
                    TraceId = traceId,
                    RecommendedAction = "Student does not have an active package. Advise student to enroll in an eligible package."
                });
            }
        }
        else
        {
            var depleted = activePackages.FirstOrDefault(p => p.ClassesAllowed.HasValue && p.ClassesUsed >= p.ClassesAllowed.Value);
            if (depleted != null)
            {
                businessIssues.Add(new DiagnosticIssue
                {
                    Code = "ZERO_CLASSES_REMAINING",
                    Category = "BUSINESS_STATE",
                    Severity = "WARNING",
                    Title = "Zero classes remaining on active package",
                    Description = $"Student has used all {depleted.ClassesAllowed} allocated classes on '{depleted.Package?.Name ?? "Active Package"}'.",
                    Evidence = new
                    {
                        packageId = depleted.Id,
                        packageName = depleted.Package?.Name,
                        classesAllowed = depleted.ClassesAllowed,
                        classesUsed = depleted.ClassesUsed
                    },
                    DetectedAt = now,
                    TraceId = traceId,
                    RecommendedAction = "Student has depleted all allocated classes. Suggest purchasing an add-on or renewing package."
                });
            }
        }

        // 2. Profile Completeness (Business State)
        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(sp.DateOfBirth)) missingFields.Add("DateOfBirth");
        if (string.IsNullOrWhiteSpace(sp.City)) missingFields.Add("City");
        if (string.IsNullOrWhiteSpace(sp.EmergencyContactPhone)) missingFields.Add("EmergencyContactPhone");

        if (missingFields.Count > 0)
        {
            businessIssues.Add(new DiagnosticIssue
            {
                Code = "PROFILE_INCOMPLETE",
                Category = "BUSINESS_STATE",
                Severity = "INFO",
                Title = "Student profile incomplete",
                Description = $"Missing required student profile fields: {string.Join(", ", missingFields)}.",
                Evidence = new { missingFields, profileCompleted = false },
                DetectedAt = now,
                TraceId = traceId,
                RecommendedAction = "Prompt student to complete profile details in the student portal."
            });
        }

        // 3. Unpaid / Pending Bookings (Business State)
        var pendingBookings = await _db.WorkshopBookings
            .AsNoTracking()
            .Include(wb => wb.Workshop)
            .Where(wb => wb.StudentProfileId == sp.Id && wb.Status == WorkshopBookingStatus.PendingPayment)
            .Where(wb => wb.BookedAt >= thirtyDaysAgo)
            .ToListAsync(cancellationToken);

        if (pendingBookings.Count > 0)
        {
            businessIssues.Add(new DiagnosticIssue
            {
                Code = "UNPAID_BOOKING",
                Category = "BUSINESS_STATE",
                Severity = "WARNING",
                Title = "Unpaid workshop booking pending",
                Description = $"Student has {pendingBookings.Count} unconfirmed workshop booking(s) pending payment.",
                Evidence = new
                {
                    pendingCount = pendingBookings.Count,
                    workshops = pendingBookings.Select(b => new { b.Id, b.Workshop.Title, b.BookedAt }).ToList()
                },
                DetectedAt = now,
                TraceId = traceId,
                RecommendedAction = "Review pending booking or notify student to complete checkout."
            });
        }

        // 4. Payment Failures (Technical Failure - 30 days)
        var failedPayments = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.UserId == sp.UserId && p.Status == PaymentStatus.Failed && p.CreatedAt >= thirtyDaysAgo)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var p in failedPayments)
        {
            technicalFailures.Add(new DiagnosticIssue
            {
                Code = "PAYMENT_GATEWAY_FAILURE",
                Category = "TECHNICAL_FAILURE",
                Severity = "CRITICAL",
                Title = "Payment transaction failed",
                Description = $"Payment transaction {p.Id} of {p.Currency} {p.Amount:F2} failed at payment gateway.",
                Evidence = new
                {
                    paymentId = p.Id,
                    purpose = p.Purpose.ToString(),
                    amount = p.Amount,
                    currency = p.Currency,
                    razorpayOrderId = p.RazorpayOrderId,
                    razorpayPaymentId = p.RazorpayPaymentId,
                    createdAt = p.CreatedAt,
                    status = "FAILED"
                },
                DetectedAt = p.CreatedAt,
                TraceId = traceId,
                RecommendedAction = $"Investigate payment gateway failure for Order ID {p.RazorpayOrderId ?? p.Id.ToString()}."
            });
        }

        // 5. OTP Failures (Technical Failure - 24 hours)
        var failedOtps = await _db.OtpVerifications
            .AsNoTracking()
            .Where(o => o.Phone == sp.User.Phone && o.CreatedAt >= twentyFourHoursAgo && (o.AttemptCount >= 3 || (!o.IsUsed && o.ExpiresAt < now)))
            .ToListAsync(cancellationToken);

        var otpSecEvents = await _db.SecurityEvents
            .AsNoTracking()
            .Where(s => s.UserId == sp.UserId && s.CreatedAt >= twentyFourHoursAgo && s.EventType.Contains("OTP"))
            .ToListAsync(cancellationToken);

        if (failedOtps.Count > 0 || otpSecEvents.Count > 0)
        {
            technicalFailures.Add(new DiagnosticIssue
            {
                Code = "OTP_FAILURE",
                Category = "TECHNICAL_FAILURE",
                Severity = "WARNING",
                Title = "Recent OTP verification issue",
                Description = "Failed OTP authentication or verification attempts detected in the last 24 hours.",
                Evidence = new
                {
                    maskedPhone = MaskPhone(sp.User.Phone),
                    failedAttempts = failedOtps.Sum(o => o.AttemptCount),
                    securityEvents = otpSecEvents.Count,
                    window = "24h"
                },
                DetectedAt = now,
                TraceId = traceId,
                RecommendedAction = "Verify mobile connectivity or advise student on OTP entry."
            });
        }

        // 6. Security / Authorization Anomaly (Technical Failure - 30 days)
        var secDenials = await _db.SecurityEvents
            .AsNoTracking()
            .Where(s => s.UserId == sp.UserId && s.CreatedAt >= thirtyDaysAgo && (s.EventType.Contains("DENIED") || s.Severity == "CRITICAL" || s.Severity == "WARNING"))
            .Take(10)
            .ToListAsync(cancellationToken);

        if (secDenials.Count > 0)
        {
            technicalFailures.Add(new DiagnosticIssue
            {
                Code = "AUTHORIZATION_DENIED",
                Category = "TECHNICAL_FAILURE",
                Severity = "WARNING",
                Title = "Security denial events detected",
                Description = $"Encountered {secDenials.Count} warning/denial security events for this account in the last 30 days.",
                Evidence = new
                {
                    eventCount = secDenials.Count,
                    eventTypes = secDenials.Select(s => s.EventType).Distinct().ToList(),
                    window = "30d"
                },
                DetectedAt = now,
                TraceId = traceId,
                RecommendedAction = "Review audit & security logs to verify potential unauthorized activity."
            });
        }

        // 7. Attendance Aggregation
        var attendanceStats = await _db.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.StudentProfileId == sp.Id)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var attended = attendanceStats.Where(x => x.Status == AttendanceStatus.Present).Sum(x => x.Count);
        var missed = attendanceStats.Where(x => x.Status == AttendanceStatus.Absent).Sum(x => x.Count);
        var total = attended + missed;
        var rate = total > 0 ? Math.Round((double)attended / total * 100.0, 1) : 100.0;

        // Overall Status Synthesis
        var overallStatus = "HEALTHY";
        if (technicalFailures.Any(t => t.Severity == "CRITICAL") || businessIssues.Any(b => b.Severity == "CRITICAL"))
        {
            overallStatus = "CRITICAL_FAILURE";
        }
        else if (technicalFailures.Any(t => t.Severity == "WARNING") || businessIssues.Any(b => b.Severity == "WARNING"))
        {
            overallStatus = "ATTENTION_REQUIRED";
        }

        var allActions = businessIssues.Select(b => b.RecommendedAction)
            .Concat(technicalFailures.Select(t => t.RecommendedAction))
            .Distinct()
            .ToList();

        var totalBookingsCount = await _db.WorkshopBookings
            .AsNoTracking()
            .CountAsync(b => b.StudentProfileId == sp.Id, cancellationToken);

        return new StudentDiagnosticReport
        {
            StudentId = sp.Id,
            UserId = sp.UserId,
            CustomerCode = sp.User.CustomerCode,
            FullName = sp.User.FullName,
            OverallStatus = overallStatus,
            BusinessIssues = businessIssues,
            TechnicalFailures = technicalFailures,
            RecommendedActions = allActions,
            TotalClassesAttended = attended,
            TotalClassesMissed = missed,
            AttendanceRate = rate,
            ActivePackagesCount = activePackages.Count,
            TotalBookingsCount = totalBookingsCount,
            GeneratedAt = now,
            TraceId = traceId
        };
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 4) return phone ?? "";
        return string.Concat("******", phone.AsSpan(phone.Length - 4));
    }
}
