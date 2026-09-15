namespace Ethos.Api.Contracts.Admin;

public class AdminRejectApplicationRequest
{
    public string RejectionReason { get; set; } = null!;

    public string? AdminNotes { get; set; }
}
