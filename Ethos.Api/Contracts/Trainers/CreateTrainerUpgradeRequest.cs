namespace Ethos.Api.Contracts.Trainers;

public class CreateTrainerUpgradeRequest
{
    public Guid RequestedTierId { get; set; }

    public string? Reason { get; set; }
}
