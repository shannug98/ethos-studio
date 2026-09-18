using Ethos.Api.Domain.Entities;

namespace Ethos.Api.Application.Workshops;

public record TicketPdfResult(
    bool Success,
    string? StorageKey,
    string? SignedHttpsUrl,
    string? FileHash,
    string? ErrorMessage = null
);

public interface ITicketPdfService
{
    Task<TicketPdfResult> GetOrCreateTicketPdfAsync(
        WorkshopTicket ticket,
        Workshop workshop,
        WorkshopBooking booking,
        string? rawQrToken = null,
        CancellationToken cancellationToken = default);
}
