namespace Ethos.Api.Contracts.Admin;

public class AdminUpdateTrainerTierRequest
{
    public Guid TierId { get; set; }

    public string? Reason { get; set; }
}
