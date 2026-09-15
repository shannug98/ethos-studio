using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class TrainerUpgradeRequest
{
    public Guid Id { get; set; }

    public Guid TrainerProfileId { get; set; }

    public Guid CurrentTierId { get; set; }

    public Guid RequestedTierId { get; set; }

    public TrainerUpgradeRequestStatus Status { get; set; }

    public string? Reason { get; set; }

    public string? AdminNotes { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public TrainerProfile TrainerProfile { get; set; } = null!;

    public TrainerTier CurrentTier { get; set; } = null!;

    public TrainerTier RequestedTier { get; set; } = null!;
}
