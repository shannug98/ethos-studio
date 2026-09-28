using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Analytics;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Controllers.Admin;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Ethos.Api.Tests.Analytics;

public class FakeAdminInsightsService : IAdminInsightsService
{
    public Task<BusinessOverviewResponse> GetOverviewAsync(string range = "last30days", Guid? workshopId = null, int windowMinutes = 15, CancellationToken cancellationToken = default)
    {
        if (range == "invalid") throw new ArgumentException("Invalid range");
        return Task.FromResult(new BusinessOverviewResponse { Range = range, Visitors = 100, CompletedBookings = 25 });
    }

    public Task<BusinessTrendsResponse> GetTrendsAsync(string range = "last30days", Guid? workshopId = null, CancellationToken cancellationToken = default)
    {
        if (range == "invalid") throw new ArgumentException("Invalid range");
        return Task.FromResult(new BusinessTrendsResponse());
    }

    public Task<BusinessWorkshopMatrixResponse> GetWorkshopMatrixAsync(string range = "last30days", int limit = 10, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new BusinessWorkshopMatrixResponse());
    }

    public Task<PaymentOutcomesDto> GetPaymentOutcomesAsync(string range = "last30days", Guid? workshopId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentOutcomesDto { TotalAttempts = 10, Successful = 8 });
    }

    public Task<BusinessLiveUsersResponse> GetLiveUsersAsync(int windowMinutes = 15, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new BusinessLiveUsersResponse { WindowMinutes = windowMinutes, LiveUserCount = 5 });
    }

    public Task<BusinessActivityFeedResponse> GetActivityFeedAsync(int limit = 20, Guid? workshopId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new BusinessActivityFeedResponse());
    }

    public Task<TrafficSourcesResponse> GetTrafficSourcesAsync(string range = "last30days", Guid? workshopId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TrafficSourcesResponse { TotalVisitors = 10 });
    }

    public Task<DeviceBreakdownResponse> GetDeviceBreakdownAsync(string range = "last30days", Guid? workshopId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DeviceBreakdownResponse { TotalVisitors = 10 });
    }

    public Task<TopLocationsResponse> GetTopLocationsAsync(string range = "last30days", Guid? workshopId = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TopLocationsResponse { TotalVisitors = 10 });
    }
}

public class AdminInsightsControllerTests
{
    private (AdminInsightsController Controller, FakeAdminInsightsService Service, FakeAdminAuthorizationService AuthService) CreateController()
    {
        var service = new FakeAdminInsightsService();
        var authService = new FakeAdminAuthorizationService();
        var controller = new AdminInsightsController(service, authService);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "ADMIN")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return (controller, service, authService);
    }

    [Fact]
    public async Task GetOverview_WhenAuthorized_ReturnsOkResult()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetOverview("last30days");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<BusinessOverviewResponse>(okResult.Value);
        Assert.Equal(100, response.Visitors);
        Assert.Equal(25, response.CompletedBookings);
    }

    [Fact]
    public async Task GetOverview_WhenForbidden_Returns403()
    {
        var (controller, _, authService) = CreateController();
        authService.ShouldAuthorize = false;

        var result = await controller.GetOverview("last30days");

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetOverview_InvalidRange_ReturnsBadRequest()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetOverview("invalid");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetLiveUsers_WhenAuthorized_ReturnsLiveUsers()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetLiveUsers(15);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<BusinessLiveUsersResponse>(okResult.Value);
        Assert.Equal(5, response.LiveUserCount);
    }
}
