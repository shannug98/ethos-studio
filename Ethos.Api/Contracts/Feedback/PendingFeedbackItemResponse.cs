namespace Ethos.Api.Contracts.Feedback;

public class PendingFeedbackItemResponse
{
    public string Type { get; set; } = string.Empty; // "CLASS" or "WORKSHOP"

    public Guid ItemId { get; set; }

    public Guid ReferenceId { get; set; } // ClassEnrollmentId or WorkshopBookingId

    public string Title { get; set; } = string.Empty;

    public string? TrainerName { get; set; }

    public DateTime EventDate { get; set; }

    public string? Venue { get; set; }

    public bool CanSubmit { get; set; }

    public string Status { get; set; } = "PENDING"; // "PENDING" or "LOCKED"

    public string? LockReason { get; set; }
}
