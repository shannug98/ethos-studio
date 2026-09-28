using Ethos.Api.Application.Analytics;
using Ethos.Api.Contracts.Analytics;
using Ethos.Api.Controllers;
using Ethos.Api.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Ethos.Api.Tests.Analytics;

public class FakeAnalyticsEventService : IAnalyticsEventService
{
    public RecordAnalyticsEventRequest? LastRequest { get; private set; }
    public bool ShouldSucceed { get; set; } = true;
    public Guid GeneratedEventId { get; set; } = Guid.NewGuid();

    public Task<RecordAnalyticsEventResponse> RecordEventAsync(
        RecordAnalyticsEventRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(new RecordAnalyticsEventResponse
        {
            Success = ShouldSucceed,
            EventId = GeneratedEventId,
            OccurredAtUtc = DateTime.UtcNow
        });
    }
}

public class AnalyticsControllerTests
{
    [Fact]
    public async Task RecordEvent_ValidRequest_ReturnsOkWithResponse()
    {
        // Arrange
        var fakeService = new FakeAnalyticsEventService();
        var controller = new AnalyticsController(fakeService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.WorkshopCheckoutStarted,
            VisitorId = "vis_test_12345",
            SessionId = "sess_test_12345",
            WorkshopId = Guid.NewGuid()
        };

        // Act
        var result = await controller.RecordEvent(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<RecordAnalyticsEventResponse>(okResult.Value);
        Assert.True(value.Success);
        Assert.Equal(fakeService.GeneratedEventId, value.EventId);
        Assert.Equal(request, fakeService.LastRequest);
    }

    [Fact]
    public async Task RecordEvent_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var fakeService = new FakeAnalyticsEventService();
        var controller = new AnalyticsController(fakeService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var request = new RecordAnalyticsEventRequest
        {
            EventType = AnalyticsEventType.PageView,
            VisitorId = "short", // Invalid
            SessionId = "sess_123"
        };

        controller.ModelState.AddModelError("VisitorId", "VisitorId must be between 8 and 64 characters.");

        // Act
        var result = await controller.RecordEvent(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(fakeService.LastRequest);
    }
}
