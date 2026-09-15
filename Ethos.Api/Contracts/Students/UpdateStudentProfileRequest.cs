using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Students;

public class UpdateStudentProfileRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? DateOfBirth { get; set; }

    [StringLength(30)]
    public string? Gender { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? EmergencyContactName { get; set; }

    [StringLength(20)]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(1000)]
    public string? Bio { get; set; }
}
