namespace Ethos.Api.Domain.Entities;

public class StudentProfile
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? City { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? Bio { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<StudentPackage> StudentPackages { get; set; }
        = new List<StudentPackage>();

    public ICollection<ClassEnrollment> Enrollments { get; set; }
        = new List<ClassEnrollment>();

    public ICollection<AttendanceRecord> AttendanceRecords { get; set; }
        = new List<AttendanceRecord>();

    public ICollection<WorkshopBooking> WorkshopBookings { get; set; }
        = new List<WorkshopBooking>();

    public ICollection<WorkshopFeedback> WorkshopFeedbacks { get; set; }
        = new List<WorkshopFeedback>();

    public ICollection<ClassFeedback> ClassFeedbacks { get; set; }
        = new List<ClassFeedback>();
}
