namespace Ethos.Api.Domain.Entities;

public class TrainerTierPermission
{
    public Guid TrainerTierId { get; set; }

    public Guid PermissionId { get; set; }

    public bool IsAllowed { get; set; }

    public TrainerTier TrainerTier { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}
