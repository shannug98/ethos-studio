namespace Ethos.Api.Domain.Entities;

public class TrainerPermissionOverride
{
    public Guid Id { get; set; }

    public Guid TrainerProfileId { get; set; }

    public Guid PermissionId { get; set; }

    public bool IsAllowed { get; set; }

    public string? Reason { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public TrainerProfile TrainerProfile { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}
