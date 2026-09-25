namespace Ethos.Api.Contracts.Admin;

public class AdminCompleteWorkshopRequest
{
    public bool ForceComplete { get; set; }
    public string? OverrideReason { get; set; }
}
