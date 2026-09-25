namespace Ethos.Api.Domain.Entities;

public class WorkshopTrainer
{
    public Guid Id { get; set; }

    public Guid WorkshopId { get; set; }

    public Guid TrainerProfileId { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Workshop Workshop { get; set; } = null!;

    public TrainerProfile TrainerProfile { get; set; } = null!;
}
