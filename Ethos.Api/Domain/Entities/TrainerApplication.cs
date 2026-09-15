using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class TrainerApplication
{
    public Guid Id { get; set; }

    public Guid TrainerProfileId { get; set; }

    public Guid? PaymentTransactionId { get; set; }

    public TrainerApplicationStatus Status { get; set; }

    public string? ApplicationNotes { get; set; }

    public string? AdminNotes { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? PaymentVerifiedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public TrainerProfile TrainerProfile { get; set; } = null!;

    public PaymentTransaction? PaymentTransaction { get; set; }

    public TrainerApplicationVideo? VideoIntroduction { get; set; }
}

