using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Analytics;
using Ethos.Api.Contracts.Analytics;
using Ethos.Api.Controllers.Admin;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Ethos.Api.Tests.Analytics;

public class FakeAnalyticsQueryService : IAnalyticsQueryService
{
    public Task<AnalyticsSummaryResponse> GetSummaryAsync(string range = "last7days", CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AnalyticsSummaryResponse { TotalEvents = 42 });
    }

    public Task<AnalyticsTrendsResponse> GetTrendsAsync(string range = "last30days", CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AnalyticsTrendsResponse { Granularity = "day" });
    }

    public Task<AnalyticsEventBreakdownResponse> GetEventBreakdownAsync(string range = "last30days", CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AnalyticsEventBreakdownResponse());
    }

    public Task<AnalyticsWorkshopResponse> GetWorkshopAnalyticsAsync(string range = "last30days", int limit = 10, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AnalyticsWorkshopResponse());
    }

    public Task<AnalyticsRecentEventsResponse> GetRecentEventsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AnalyticsRecentEventsResponse());
    }
}

public class FakeAdminAuthorizationService : IAdminAuthorizationService
{
    public bool ShouldAuthorize { get; set; } = true;
    public int FailureStatusCode { get; set; } = 403;
    public string? LastAuthorizedPermission { get; private set; }

    public Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(ShouldAuthorize);

    public Task<bool> HasAnyPermissionAsync(Guid userId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default) =>
        Task.FromResult(ShouldAuthorize);

    public Task<bool> HasAllPermissionsAsync(Guid userId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default) =>
        Task.FromResult(ShouldAuthorize);

    public Task<AdminAuthorizationResult> AuthorizeActionAsync(
        ClaimsPrincipal user,
        string permissionCode,
        string? resourceType = null,
        Guid? resourceId = null,
        HttpContext? httpContext = null,
        CancellationToken cancellationToken = default)
    {
        LastAuthorizedPermission = permissionCode;

        if (!ShouldAuthorize)
        {
            return Task.FromResult(new AdminAuthorizationResult
            {
                Success = false,
                StatusCode = FailureStatusCode,
                ErrorCode = FailureStatusCode == 401 ? "ADMIN_UNAUTHENTICATED" : "ADMIN_PERMISSION_DENIED",
                ErrorMessage = "Access denied."
            });
        }

        return Task.FromResult(new AdminAuthorizationResult
        {
            Success = true,
            StatusCode = 200,
            AdminUserId = Guid.NewGuid()
        });
    }
}

public class AdminAnalyticsControllerTests
{
    [Fact]
    public async Task GetSummary_Authorized_ReturnsOk()
    {
        // Arrange
        var queryService = new FakeAnalyticsQueryService();
        var authService = new FakeAdminAuthorizationService { ShouldAuthorize = true };
        var controller = new AdminAnalyticsController(queryService, authService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.GetSummary("last7days", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<AnalyticsSummaryResponse>(okResult.Value);
        Assert.Equal(42, summary.TotalEvents);
        Assert.Equal(AdminPermissions.AnalyticsView, authService.LastAuthorizedPermission);
    }

    [Fact]
    public async Task GetSummary_Forbidden_Returns403()
    {
        // Arrange
        var queryService = new FakeAnalyticsQueryService();
        var authService = new FakeAdminAuthorizationService { ShouldAuthorize = false, FailureStatusCode = 403 };
        var controller = new AdminAnalyticsController(queryService, authService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.GetSummary("last7days", CancellationToken.None);

        // Assert
        var objResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, objResult.StatusCode);
    }

    [Fact]
    public async Task GetTrends_Authorized_InvokesAnalyticsView()
    {
        // Arrange
        var queryService = new FakeAnalyticsQueryService();
        var authService = new FakeAdminAuthorizationService { ShouldAuthorize = true };
        var controller = new AdminAnalyticsController(queryService, authService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.GetTrends("last30days", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<AnalyticsTrendsResponse>(okResult.Value);
        Assert.Equal(AdminPermissions.AnalyticsView, authService.LastAuthorizedPermission);
    }

    [Fact]
    public async Task GetEventBreakdown_Authorized_InvokesAnalyticsView()
    {
        // Arrange
        var queryService = new FakeAnalyticsQueryService();
        var authService = new FakeAdminAuthorizationService { ShouldAuthorize = true };
        var controller = new AdminAnalyticsController(queryService, authService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.GetEventBreakdown("last30days", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<AnalyticsEventBreakdownResponse>(okResult.Value);
        Assert.Equal(AdminPermissions.AnalyticsView, authService.LastAuthorizedPermission);
    }

    [Fact]
    public async Task GetWorkshopAnalytics_Authorized_InvokesAnalyticsView()
    {
        // Arrange
        var queryService = new FakeAnalyticsQueryService();
        var authService = new FakeAdminAuthorizationService { ShouldAuthorize = true };
        var controller = new AdminAnalyticsController(queryService, authService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.GetWorkshopAnalytics("last30days", 10, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<AnalyticsWorkshopResponse>(okResult.Value);
        Assert.Equal(AdminPermissions.AnalyticsView, authService.LastAuthorizedPermission);
    }

    [Fact]
    public async Task GetRecentEvents_Authorized_InvokesAnalyticsView()
    {
        // Arrange
        var queryService = new FakeAnalyticsQueryService();
        var authService = new FakeAdminAuthorizationService { ShouldAuthorize = true };
        var controller = new AdminAnalyticsController(queryService, authService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.GetRecentEvents(20, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<AnalyticsRecentEventsResponse>(okResult.Value);
        Assert.Equal(AdminPermissions.AnalyticsView, authService.LastAuthorizedPermission);
    }
}
