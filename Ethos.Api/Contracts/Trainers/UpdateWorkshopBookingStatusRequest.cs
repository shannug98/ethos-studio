using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Trainers;

public class UpdateWorkshopBookingStatusRequest
{
    public WorkshopBookingStatus Status { get; set; }
}
