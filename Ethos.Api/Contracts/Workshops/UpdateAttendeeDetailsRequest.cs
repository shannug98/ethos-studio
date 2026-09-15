using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Workshops;

public class UpdateAttendeeDetailsRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string AttendeeName { get; set; } = string.Empty;

    [Phone]
    [StringLength(30)]
    public string? AttendeePhone { get; set; }

    [EmailAddress]
    [StringLength(200)]
    public string? AttendeeEmail { get; set; }
}
