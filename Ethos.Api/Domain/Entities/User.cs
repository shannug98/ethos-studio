namespace Ethos.Api.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string CustomerCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // Authentication
    // Stores only the securely generated password hash.
    // Never store a plain-text password.
    public string? PasswordHash { get; set; }

    // True only when the trainer is using an administrator-generated
    // temporary password and must create their own password.
    public bool MustChangePassword { get; set; } = false;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public StudentProfile? StudentProfile { get; set; }

    public TrainerProfile? TrainerProfile { get; set; }
}
