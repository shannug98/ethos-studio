namespace Ethos.Api.Domain.Entities;

public class TrainerTier
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public decimal? ApplicationFee { get; set; }

    public decimal? UpgradeFee { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<TrainerProfile> Trainers { get; set; }
        = new List<TrainerProfile>();

    public ICollection<TrainerTierPermission> Permissions { get; set; }
        = new List<TrainerTierPermission>();
}
