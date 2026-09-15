namespace Ethos.Api.Contracts.Trainers;

public class TrainerTierHistoryResponse
{
    public Guid Id { get; set; }

    public Guid? TierId { get; set; }

    public string? TierName { get; set; }

    public string? TierCode { get; set; }

    public string? PreviousTier { get; set; }

    public string? NewTier { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Reason { get; set; }
}
