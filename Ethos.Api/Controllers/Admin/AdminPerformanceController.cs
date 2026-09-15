using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Trainers;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Infrastructure.Persistence;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Performance")]
public class AdminPerformanceController : ControllerBase
{
    private readonly ITrainerPerformanceService _performanceService;
    private readonly AppDbContext _db;
    private readonly IAdminAuthorizationService _authService;

    public AdminPerformanceController(
        ITrainerPerformanceService performanceService,
        AppDbContext db,
        IAdminAuthorizationService authService)
    {
        _performanceService = performanceService;
        _db = db;
        _authService = authService;
    }

    [HttpGet("trainers/{trainerId:guid}/performance")]
    public async Task<ActionResult<TrainerPerformanceResponse>> GetTrainerPerformance(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.TrainerView, "TrainerProfile", trainerId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var trainer = await _db.TrainerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trainerId || t.UserId == trainerId, cancellationToken);

        if (trainer == null) return NotFound();

        var result = await _performanceService.GetMyPerformanceAsync(trainer.UserId, cancellationToken, checkPermission: false);
        return Ok(result);
    }
}
