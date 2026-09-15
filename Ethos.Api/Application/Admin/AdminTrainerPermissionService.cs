using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Admin;

public class AdminTrainerPermissionService : IAdminTrainerPermissionService
{
    private readonly AppDbContext _db;
    private readonly IAdminAuditService _auditService;

    public AdminTrainerPermissionService(
        AppDbContext db,
        IAdminAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<TrainerPermissionResponse>> GetAllPermissionsAsync(
        CancellationToken cancellationToken)
    {
        return await _db.Permissions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new TrainerPermissionResponse
            {
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                IsAllowed = true
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminTrainerPermissionDetailResponse>> GetTrainerPermissionsAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .AsNoTracking()
            .Include(t => t.CurrentTier)
            .FirstOrDefaultAsync(t => t.Id == trainerId, cancellationToken);

        if (trainer == null)
        {
            throw new ArgumentException("Trainer profile not found.");
        }

        var allPermissions = await _db.Permissions
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        var tierAllowedPermissionIds = new HashSet<Guid>();
        if (trainer.CurrentTierId.HasValue)
        {
            tierAllowedPermissionIds = (await _db.TrainerTierPermissions
                .AsNoTracking()
                .Where(ttp => ttp.TrainerTierId == trainer.CurrentTierId.Value && ttp.IsAllowed)
                .Select(ttp => ttp.PermissionId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var overrides = await _db.TrainerPermissionOverrides
            .AsNoTracking()
            .Where(o => o.TrainerProfileId == trainer.Id)
            .ToDictionaryAsync(o => o.PermissionId, cancellationToken);

        var list = new List<AdminTrainerPermissionDetailResponse>();

        foreach (var p in allPermissions)
        {
            bool roleDefault = tierAllowedPermissionIds.Contains(p.Id);
            string overrideStatus = "None";
            bool effectiveValue = roleDefault;
            Guid? permissionId = p.Id;
            DateTime? overrideModifiedAt = null;
            string? overrideReason = null;

            if (overrides.TryGetValue(p.Id, out var o))
            {
                overrideStatus = o.IsAllowed ? "Allowed" : "Denied";
                effectiveValue = o.IsAllowed;
                overrideModifiedAt = o.CreatedAt;
                overrideReason = o.Reason;
            }

            list.Add(new AdminTrainerPermissionDetailResponse
            {
                PermissionId = permissionId,
                PermissionCode = p.Code,
                PermissionName = p.Name,
                Description = p.Description,
                RoleDefault = roleDefault,
                Override = overrideStatus,
                EffectiveValue = effectiveValue,
                OverrideModifiedAt = overrideModifiedAt,
                OverrideReason = overrideReason
            });
        }

        return list;
    }

    public async Task<AdminTrainerPermissionDetailResponse> SetTrainerPermissionOverrideAsync(
        Guid trainerId,
        Guid adminUserId,
        AdminPermissionOverrideRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Validation: Mutual exclusivity and mandatory reason
        if (request == null)
            throw new ArgumentException("Request payload cannot be empty.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A meaningful justification reason is mandatory for modifying trainer permissions.");

        if (request.ClearOverride && request.IsAllowed.HasValue)
            throw new ArgumentException("Ambiguous request: clearOverride cannot be true when isAllowed has a boolean value.");

        if (!request.ClearOverride && !request.IsAllowed.HasValue)
            throw new ArgumentException("Ambiguous request: isAllowed must be explicitly true or false when clearOverride is false.");

        if (string.IsNullOrWhiteSpace(request.PermissionCode))
            throw new ArgumentException("Permission code is mandatory.");

        // 2. Validation: Domain ownership check
        if (!request.PermissionCode.StartsWith("TRAINER_", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Permission code '{request.PermissionCode}' does not belong to the trainer permission domain.");

        // 3. Entity lookup
        var trainer = await _db.TrainerProfiles
            .FirstOrDefaultAsync(x => x.Id == trainerId, cancellationToken);

        if (trainer == null)
            throw new KeyNotFoundException($"Trainer profile with ID '{trainerId}' was not found.");

        var permission = await _db.Permissions
            .FirstOrDefaultAsync(x => x.Code == request.PermissionCode && x.IsActive, cancellationToken);

        if (permission == null)
            throw new KeyNotFoundException($"Active permission with code '{request.PermissionCode}' was not found.");

        // 4. Determine normal role/tier default
        bool roleDefault = false;
        if (trainer.CurrentTierId.HasValue)
        {
            roleDefault = await _db.TrainerTierPermissions
                .AsNoTracking()
                .AnyAsync(ttp => ttp.TrainerTierId == trainer.CurrentTierId.Value && ttp.PermissionId == permission.Id && ttp.IsAllowed, cancellationToken);
        }

        var existingOverride = await _db.TrainerPermissionOverrides
            .FirstOrDefaultAsync(x => x.TrainerProfileId == trainer.Id && x.PermissionId == permission.Id, cancellationToken);

        bool? prevOverride = existingOverride?.IsAllowed;
        bool prevEffective = prevOverride ?? roleDefault;

        bool? newOverride = request.ClearOverride ? null : request.IsAllowed;
        bool newEffective = newOverride ?? roleDefault;

        string auditEventType;
        string auditDetails;

        if (request.ClearOverride)
        {
            if (existingOverride != null)
            {
                _db.TrainerPermissionOverrides.Remove(existingOverride);
            }

            auditEventType = "TRAINER_PERMISSION_OVERRIDE_REMOVED";
            auditDetails = $"Override removed. Previous override: {(prevOverride == null ? "None" : prevOverride.Value ? "Allowed" : "Restricted")}, New override: None, Previous effective: {(prevEffective ? "Allowed" : "Restricted")}, New effective: {(newEffective ? "Allowed" : "Restricted")}. Reason: {request.Reason.Trim()}";
        }
        else
        {
            if (existingOverride != null)
            {
                existingOverride.IsAllowed = request.IsAllowed!.Value;
                existingOverride.Reason = request.Reason.Trim();
                auditEventType = "TRAINER_PERMISSION_OVERRIDE_UPDATED";
            }
            else
            {
                _db.TrainerPermissionOverrides.Add(new TrainerPermissionOverride
                {
                    Id = Guid.NewGuid(),
                    TrainerProfileId = trainer.Id,
                    PermissionId = permission.Id,
                    IsAllowed = request.IsAllowed!.Value,
                    Reason = request.Reason.Trim(),
                    CreatedByUserId = adminUserId,
                    CreatedAt = DateTime.UtcNow
                });
                auditEventType = "TRAINER_PERMISSION_OVERRIDE_CREATED";
            }

            auditDetails = $"Override saved. Previous override: {(prevOverride == null ? "None" : prevOverride.Value ? "Allowed" : "Restricted")}, New override: {(newOverride!.Value ? "Allowed" : "Restricted")}, Previous effective: {(prevEffective ? "Allowed" : "Restricted")}, New effective: {(newEffective ? "Allowed" : "Restricted")}. Reason: {request.Reason.Trim()}";
        }

        _auditService.AddAuditLog(
            adminUserId,
            auditEventType,
            "TrainerProfile",
            trainer.Id,
            auditDetails);

        await _db.SaveChangesAsync(cancellationToken);

        return new AdminTrainerPermissionDetailResponse
        {
            PermissionId = permission.Id,
            PermissionCode = permission.Code,
            PermissionName = permission.Name,
            Description = permission.Description,
            RoleDefault = roleDefault,
            Override = newOverride == null ? "None" : (newOverride.Value ? "Allowed" : "Denied"),
            EffectiveValue = newEffective,
            OverrideModifiedAt = newOverride == null ? null : DateTime.UtcNow,
            OverrideReason = newOverride == null ? null : request.Reason.Trim()
        };
    }

    public async Task<AdminTrainerPermissionDetailResponse> ClearTrainerPermissionOverrideAsync(
        Guid trainerId,
        Guid adminUserId,
        string permissionCode,
        string reason,
        CancellationToken cancellationToken)
    {
        return await SetTrainerPermissionOverrideAsync(
            trainerId,
            adminUserId,
            new AdminPermissionOverrideRequest
            {
                PermissionCode = permissionCode,
                ClearOverride = true,
                IsAllowed = null,
                Reason = reason
            },
            cancellationToken);
    }
}
