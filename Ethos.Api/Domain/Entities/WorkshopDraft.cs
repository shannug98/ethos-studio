namespace Ethos.Api.Domain.Entities;

public class WorkshopDraft
{
    public Guid Id { get; set; }

    public Guid AdminUserId { get; set; }

    public Guid? WorkshopId { get; set; }

    public string DraftJson { get; set; } = "{}";

    public long Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User AdminUser { get; set; } = null!;

    public Workshop? Workshop { get; set; }
}
