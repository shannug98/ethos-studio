namespace Ethos.Api.Contracts.Trainers;

public class TrainerUpgradeRequestResponse
{
    public Guid Id { get; set; }

    public string? CurrentTier { get; set; }

    public string? RequestedTier { get; set; }

    public Guid RequestedTierId { get; set; }

    public string RequestedTierName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Reason { get; set; }

    public string? AdminNotes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime RequestedAt
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    public DateTime? ProcessedAt
    {
        get => ReviewedAt;
        set => ReviewedAt = value;
    }
}
