using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class TrainerProfile
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TrainerCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? City { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? PrimaryDanceStyle { get; set; }

    public string? SecondaryDanceStyles { get; set; }

    public int? ExperienceYears { get; set; }

    public string? CurrentStudio { get; set; }

    public string? Bio { get; set; }

    public string? InstagramUrl { get; set; }

    public string? YouTubeUrl { get; set; }

    public TrainerStatus Status { get; set; }

    public Guid? CurrentTierId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public TrainerTier? CurrentTier { get; set; }

    public ICollection<TrainerApplication> Applications { get; set; }
        = new List<TrainerApplication>();

    public ICollection<TrainerAvailability> Availability { get; set; }
        = new List<TrainerAvailability>();

    public ICollection<TrainerPermissionOverride> PermissionOverrides { get; set; }
        = new List<TrainerPermissionOverride>();

    public ICollection<TrainerTierHistory> TierHistory { get; set; }
        = new List<TrainerTierHistory>();

    public ICollection<TrainerUpgradeRequest> UpgradeRequests { get; set; }
        = new List<TrainerUpgradeRequest>();

    public ICollection<TrainerPerformanceSnapshot> PerformanceSnapshots { get; set; }
        = new List<TrainerPerformanceSnapshot>();

    public ICollection<Workshop> Workshops { get; set; }
        = new List<Workshop>();
}
