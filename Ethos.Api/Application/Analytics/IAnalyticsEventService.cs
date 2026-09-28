using Ethos.Api.Contracts.Analytics;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Analytics;

public interface IAnalyticsEventService
{
    Task<RecordAnalyticsEventResponse> RecordEventAsync(
        RecordAnalyticsEventRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default);
}
