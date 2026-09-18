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

    public bool MustChangePassword { get; set; } = false;

    // Password Security & Lockout Metadata
    public int FailedLoginCount { get; set; } = 0;

    public DateTime? LockoutEnd { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public StudentProfile? StudentProfile { get; set; }

    public TrainerProfile? TrainerProfile { get; set; }
}
