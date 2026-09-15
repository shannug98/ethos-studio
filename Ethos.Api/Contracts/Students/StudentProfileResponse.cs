namespace Ethos.Api.Contracts.Students;

public class StudentProfileResponse
{
    public Guid UserId { get; set; }

    public string CustomerCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool ProfileCompleted { get; set; }

    public int ProfileCompletionPercentage { get; set; }

    public string? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? City { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? Bio { get; set; }

    public DateTime? MemberSince { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
