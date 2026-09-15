namespace Ethos.Api.Contracts.Trainers;

public class TrainerTierResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public decimal? ApplicationFee { get; set; }

    public decimal? UpgradeFee { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }
}
