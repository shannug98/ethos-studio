using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Trainers;

public class CheckInTicketRequest
{
    public CheckInMethod Method { get; set; } = CheckInMethod.QrScan;
    public string? Notes { get; set; }
}
