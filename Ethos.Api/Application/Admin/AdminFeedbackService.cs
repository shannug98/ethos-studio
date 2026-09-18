using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminFeedbackService : IAdminFeedbackService
{
    private readonly AppDbContext _db;

    public AdminFeedbackService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AdminFeedbackResponse>> GetFeedbackAsync(
        int page,
        int pageSize,
        Guid? trainerId,
        Guid? studentId,
        Guid? workshopId,
        int? minRating,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.WorkshopFeedbacks
            .AsNoTracking()
            .Include(f => f.Workshop)
                .ThenInclude(w => w.TrainerProfile)
            .Include(f => f.StudentProfile)
                .ThenInclude(s => s!.User)
            .Include(f => f.WorkshopBooking)
            .Where(f => f.IsValid &&
                        f.WorkshopBooking != null &&
                        f.WorkshopBooking.Status == WorkshopBookingStatus.Attended)
            .AsQueryable();

        if (trainerId.HasValue)
            query = query.Where(f => f.Workshop.TrainerProfileId == trainerId.Value);

        if (studentId.HasValue)
            query = query.Where(f => f.StudentProfileId == studentId.Value);

        if (workshopId.HasValue)
            query = query.Where(f => f.WorkshopId == workshopId.Value);

        if (minRating.HasValue)
            query = query.Where(f => f.Rating >= minRating.Value);

        if (startDate.HasValue)
            query = query.Where(f => f.SubmittedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(f => f.SubmittedAt <= endDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var feedbacks = await query
            .OrderByDescending(f => f.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = feedbacks.Select(f => new AdminFeedbackResponse
        {
            Id = f.Id,
            WorkshopId = f.WorkshopId,
            WorkshopTitle = f.Workshop.Title,
            TrainerId = f.Workshop.TrainerProfileId ?? Guid.Empty,
            TrainerName = f.Workshop.TrainerProfile?.FullName ?? "Ethos Trainer",
            StudentId = f.StudentProfileId ?? Guid.Empty,
            StudentName = f.StudentProfile != null && f.StudentProfile.User != null ? f.StudentProfile.User.FullName : "Guest Attendee",
            StudentPhone = f.StudentProfile?.User?.Phone ?? "—",
            Rating = f.Rating,
            TeachingRating = f.TeachingRating ?? f.Rating,
            EnergyRating = f.EnergyRating ?? f.Rating,
            ContentRating = f.ContentRating ?? f.Rating,
            Comment = f.Comment,
            WouldRecommend = f.WouldRecommend,
            SubmittedAt = f.SubmittedAt
        }).ToList();

        return new PagedResult<AdminFeedbackResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminFeedbackResponse?> GetFeedbackByIdAsync(
        Guid feedbackId,
        CancellationToken cancellationToken)
    {
        var f = await _db.WorkshopFeedbacks
            .AsNoTracking()
            .Include(f => f.Workshop)
                .ThenInclude(w => w.TrainerProfile)
            .Include(f => f.StudentProfile)
                .ThenInclude(s => s!.User)
            .Include(f => f.WorkshopBooking)
            .FirstOrDefaultAsync(f => f.Id == feedbackId &&
                                      f.IsValid &&
                                      f.WorkshopBooking != null &&
                                      f.WorkshopBooking.Status == WorkshopBookingStatus.Attended, cancellationToken);

        if (f == null) return null;

        return new AdminFeedbackResponse
        {
            Id = f.Id,
            WorkshopId = f.WorkshopId,
            WorkshopTitle = f.Workshop.Title,
            TrainerId = f.Workshop.TrainerProfileId ?? Guid.Empty,
            TrainerName = f.Workshop.TrainerProfile?.FullName ?? "Ethos Trainer",
            StudentId = f.StudentProfileId ?? Guid.Empty,
            StudentName = f.StudentProfile != null && f.StudentProfile.User != null ? f.StudentProfile.User.FullName : "Guest Attendee",
            StudentPhone = f.StudentProfile?.User?.Phone ?? "—",
            Rating = f.Rating,
            TeachingRating = f.TeachingRating ?? f.Rating,
            EnergyRating = f.EnergyRating ?? f.Rating,
            ContentRating = f.ContentRating ?? f.Rating,
            Comment = f.Comment,
            WouldRecommend = f.WouldRecommend,
            SubmittedAt = f.SubmittedAt
        };
    }
}
