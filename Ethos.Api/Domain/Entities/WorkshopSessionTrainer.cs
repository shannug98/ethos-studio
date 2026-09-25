namespace Ethos.Api.Domain.Entities;

public class WorkshopSessionTrainer
{
    public Guid Id { get; set; }

    public Guid WorkshopSessionId { get; set; }

    public Guid TrainerProfileId { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public WorkshopSession WorkshopSession { get; set; } = null!;

    public TrainerProfile TrainerProfile { get; set; } = null!;
}
