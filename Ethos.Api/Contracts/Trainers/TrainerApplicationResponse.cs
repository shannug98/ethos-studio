namespace Ethos.Api.Contracts.Trainers;

public class TrainerApplicationResponse
{
    public Guid Id { get; set; }

    public Guid TrainerProfileId { get; set; }

    public string? TrainerCode { get; set; }

    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? City { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? PrimaryDanceStyle { get; set; }

    public string? SecondaryDanceStyles { get; set; }

    public int? ExperienceYears { get; set; }

    public string? CurrentStudio { get; set; }

    public string? Bio { get; set; }

    public string? InstagramUrl { get; set; }

    public string? YouTubeUrl { get; set; }

    public string? Tier { get; set; }

    public Guid? CurrentTierId { get; set; }

    public decimal? TierFee { get; set; }

    public string Status { get; set; } = null!;

    public string? ApplicationNotes { get; set; }

    public string? AdminNotes { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? PaymentTransactionId { get; set; }

    public decimal? PaymentAmount { get; set; }

    public string? PaymentStatus { get; set; }

    public string? PaymentReference { get; set; }

    public DateTime? PaymentDate { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? PaymentVerifiedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    // Audition / Introduction Video Information
    public bool HasVideo { get; set; }

    public string? VideoFileName { get; set; }

    public int? VideoDurationSeconds { get; set; }

    public long? VideoSizeBytes { get; set; }

    public string? VideoContentType { get; set; }

    public DateTime? VideoUploadedAt { get; set; }
}
