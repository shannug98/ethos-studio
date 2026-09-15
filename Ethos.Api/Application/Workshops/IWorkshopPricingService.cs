using Ethos.Api.Contracts.Workshops;
using Ethos.Api.Domain.Entities;

namespace Ethos.Api.Application.Workshops;

public interface IWorkshopPricingService
{
    Task<WorkshopPricingResponse> CalculatePricingAsync(
        Workshop workshop,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<WorkshopPriceQuoteResponse> CalculateQuoteAsync(
        Guid workshopId,
        int quantity,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task EnsureDefaultTiersAsync(
        Workshop workshop,
        CancellationToken cancellationToken = default);

    decimal CalculatePublicPrice(decimal startingPrice, int bookedSeats);

    decimal CalculateStudentPrice(decimal startingPrice);
}
