using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Admin;

public class AdminModifyBookingSessionRequest
{
    public Guid? CurrentTicketId { get; set; }

    public Guid? CurrentSessionId { get; set; }

    [Required]
    public Guid ReplacementSessionId { get; set; }

    public bool OverrideCutoff { get; set; } = false;

    public string? OverrideReason { get; set; }
}

public class AdminModifyBookingSessionResponse
{
    public Guid BookingId { get; set; }
    public Guid OldTicketId { get; set; }
    public Guid NewTicketId { get; set; }
    public string NewTicketNumber { get; set; } = string.Empty;
    public Guid ReplacementSessionId { get; set; }
    public string Message { get; set; } = string.Empty;
}
