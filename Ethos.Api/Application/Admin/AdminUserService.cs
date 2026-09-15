using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Students;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminUserService : IAdminUserService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public AdminUserService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<PagedResult<AdminUserListResponse>> GetUsersAsync(
        int page,
        int pageSize,
        string? search,
        string? role,
        bool? isActive,
        string? sortBy,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (string.Equals(role, "QUARANTINE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "NO_ROLE", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(u => !u.UserRoles.Any());
        }
        else
        {
            query = query.Where(u => u.UserRoles.Any());

            if (!string.IsNullOrWhiteSpace(role))
            {
                var roleUpper = role.Trim().ToUpper();
                query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Code == roleUpper));
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(s) ||
                u.Phone.Contains(s) ||
                (u.Email != null && u.Email.ToLower().Contains(s)) ||
                u.CustomerCode.ToLower().Contains(s));
        }

        query = sortBy?.ToLower() switch
        {
            "name" => query.OrderBy(u => u.FullName),
            "phone" => query.OrderBy(u => u.Phone),
            "createdat_asc" => query.OrderBy(u => u.CreatedAt),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = users.Select(u =>
        {
            var roleList = u.UserRoles.Select(ur => ur.Role.Code).ToList();
            string? primary = null;
            if (roleList.Contains("ADMIN")) primary = "ADMIN";
            else if (roleList.Contains("TRAINER")) primary = "TRAINER";
            else if (roleList.Contains("STUDENT")) primary = "STUDENT";
            else primary = roleList.FirstOrDefault();

            var others = primary != null ? roleList.Where(r => r != primary).ToList() : new List<string>();

            return new AdminUserListResponse
            {
                UserId = u.Id,
                Phone = u.Phone,
                FullName = u.FullName,
                Email = u.Email,
                Roles = roleList,
                PrimaryRole = primary,
                OtherRoles = others,
                IsActive = u.IsActive,
                CustomerCode = u.CustomerCode,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            };
        }).ToList();

        return new PagedResult<AdminUserListResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AdminUserDetailsResponse?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return null;

        var studentProfile = await _db.StudentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        StudentProfileResponse? studentDto = studentProfile == null ? null : new StudentProfileResponse
        {
            UserId = user.Id,
            CustomerCode = user.CustomerCode,
            FullName = user.FullName,
            Phone = user.Phone,
            Email = user.Email,
            ProfileCompleted = !string.IsNullOrWhiteSpace(studentProfile.DateOfBirth) && !string.IsNullOrWhiteSpace(studentProfile.City),
            DateOfBirth = studentProfile.DateOfBirth,
            Gender = studentProfile.Gender,
            City = studentProfile.City,
            ProfilePhotoUrl = studentProfile.ProfilePhotoUrl,
            EmergencyContactName = studentProfile.EmergencyContactName,
            EmergencyContactPhone = studentProfile.EmergencyContactPhone,
            Bio = studentProfile.Bio,
            CreatedAt = studentProfile.CreatedAt,
            UpdatedAt = studentProfile.UpdatedAt
        };

        var trainerProfile = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(t => t.CurrentTier)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

        TrainerResponse? trainerDto = trainerProfile == null ? null : new TrainerResponse
        {
            Id = trainerProfile.Id,
            TrainerCode = trainerProfile.TrainerCode,
            FullName = trainerProfile.FullName,
            City = trainerProfile.City,
            ProfilePhotoUrl = trainerProfile.ProfilePhotoUrl,
            PrimaryDanceStyle = trainerProfile.PrimaryDanceStyle,
            SecondaryDanceStyles = trainerProfile.SecondaryDanceStyles,
            ExperienceYears = trainerProfile.ExperienceYears,
            CurrentStudio = trainerProfile.CurrentStudio,
            Bio = trainerProfile.Bio,
            InstagramUrl = trainerProfile.InstagramUrl,
            YouTubeUrl = trainerProfile.YouTubeUrl,
            Status = trainerProfile.Status.ToString(),
            Tier = trainerProfile.CurrentTier?.Name,
            ApprovedAt = trainerProfile.ApprovedAt
        };

        return new AdminUserDetailsResponse
        {
            UserId = user.Id,
            Phone = user.Phone,
            FullName = user.FullName,
            Email = user.Email,
            Roles = user.UserRoles.Select(ur => ur.Role.Code).ToList(),
            IsActive = user.IsActive,
            CustomerCode = user.CustomerCode,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            StudentProfile = studentDto,
            TrainerProfile = trainerDto
        };
    }

    public async Task UpdateUserStatusAsync(
        Guid userId,
        Guid adminUserId,
        bool isActive,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (userId == adminUserId && !isActive)
        {
            throw new InvalidOperationException("Administrators cannot deactivate their own account.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new ArgumentException("User not found.");
        }

        // Canonical identity & phone protection for Partner 1 & 2
        if (!isActive && (user.CustomerCode == "ETHADMIN001" || user.CustomerCode == "ETHADMIN002" || user.Phone == "8019013757" || user.Phone == "8341701113"))
        {
            throw new InvalidOperationException("Protected administrative partner accounts cannot be deactivated.");
        }

        // Explicit idempotent no-op check (does not generate duplicate audit entries)
        if (user.IsActive == isActive)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is mandatory for status updates.");
        }

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;

        _auditService.AddAuditLog(
            adminUserId,
            "USER_STATUS_CHANGED",
            "User",
            userId,
            reason.Trim());

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminUserAuditHistoryResponse?> GetUserAuditHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return null;

        var adminActions = await _db.AdminActions
            .AsNoTracking()
            .Include(a => a.AdminUser)
            .Where(a => ((a.EntityType == "USER" || a.EntityType == "User") && a.EntityId == userId) || a.AdminUserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var securityEvents = await _db.SecurityEvents
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var historyItems = new List<AdminUserAuditHistoryItem>();

        foreach (var action in adminActions)
        {
            historyItems.Add(new AdminUserAuditHistoryItem
            {
                Timestamp = action.CreatedAt,
                Type = "ADMIN_ACTION",
                Event = action.ActionType,
                Severity = action.Success ? "INFO" : "WARNING",
                ActorAdminUserId = action.AdminUserId.ToString(),
                ActorName = action.AdminUser?.FullName,
                TraceId = action.TraceId,
                Reason = action.Reason,
                Before = null,
                After = null,
                DetailsJson = action.MetadataJson
            });
        }

        foreach (var sec in securityEvents)
        {
            historyItems.Add(new AdminUserAuditHistoryItem
            {
                Timestamp = sec.CreatedAt,
                Type = "SECURITY_EVENT",
                Event = sec.EventType,
                Severity = sec.Severity,
                ActorAdminUserId = null,
                ActorName = null,
                TraceId = sec.TraceId,
                Reason = null,
                Before = null,
                After = null,
                DetailsJson = sec.DetailsJson
            });
        }

        var sortedHistory = historyItems
            .OrderByDescending(h => h.Timestamp)
            .Take(50)
            .ToList();

        return new AdminUserAuditHistoryResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            CustomerCode = user.CustomerCode,
            History = sortedHistory
        };
    }
}
