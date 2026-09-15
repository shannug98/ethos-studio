namespace Ethos.Api.Domain.Entities;

public class TrainerTierHistory
{
    public Guid Id { get; set; }

    public Guid TrainerProfileId { get; set; }

    public Guid? PreviousTierId { get; set; }

    public Guid NewTierId { get; set; }

    public string? Reason { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; }

    public TrainerProfile TrainerProfile { get; set; } = null!;

    public TrainerTier NewTier { get; set; } = null!;

    public TrainerTier? PreviousTier { get; set; }
}
