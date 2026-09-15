namespace Ethos.Api.Domain.Entities;

public class IncidentUpdate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid IncidentId { get; set; }

    public Guid AdminUserId { get; set; }

    public string? PreviousStatus { get; set; }

    public string? NewStatus { get; set; }

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Incident Incident { get; set; } = null!;

    public User AdminUser { get; set; } = null!;
}