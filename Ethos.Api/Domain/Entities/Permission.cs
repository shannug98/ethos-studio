namespace Ethos.Api.Domain.Entities;

public class Permission
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public ICollection<TrainerTierPermission> TierPermissions { get; set; }
        = new List<TrainerTierPermission>();

    public ICollection<TrainerPermissionOverride> Overrides { get; set; }
        = new List<TrainerPermissionOverride>();
}
