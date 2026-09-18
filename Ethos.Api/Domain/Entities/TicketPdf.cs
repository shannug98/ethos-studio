namespace Ethos.Api.Domain.Entities;

public class TicketPdf
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public string StorageKey { get; set; } = null!;

    public string FileHash { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public WorkshopTicket WorkshopTicket { get; set; } = null!;
}
