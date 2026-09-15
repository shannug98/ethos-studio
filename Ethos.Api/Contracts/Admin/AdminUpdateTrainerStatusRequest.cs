using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Admin;

public class AdminUpdateTrainerStatusRequest
{
    public TrainerStatus Status { get; set; }
    public string? Reason { get; set; }
}
