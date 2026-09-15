using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/attendance")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Attendance")]
public class AdminAttendanceController : ControllerBase
{
    private readonly IAdminAttendanceService _attendanceService;
    private readonly IAdminAuthorizationService _authService;

    public AdminAttendanceController(
        IAdminAttendanceService attendanceService,
        IAdminAuthorizationService authService)
    {
        _attendanceService = attendanceService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<AdminClassSessionResponse>>> GetSessions(
        [FromQuery] DateTime? date = null,
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid? scheduleId = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.AttendanceView, "ClassSession", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _attendanceService.GetSessionsAsync(date, classId, scheduleId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("sessions/{sessionId:guid}/roster")]
    public async Task<ActionResult<AdminSessionRosterResponse>> GetSessionRoster(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.AttendanceView, "ClassSession", sessionId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _attendanceService.GetSessionRosterAsync(sessionId, cancellationToken);
        if (result == null) return NotFound(new { message = "Session not found." });
        return Ok(result);
    }

    [HttpPost("sessions/{sessionId:guid}/mark")]
    public async Task<IActionResult> MarkSessionAttendance(
        Guid sessionId,
        [FromBody] AdminMarkAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.AttendanceCorrect, "ClassSession", sessionId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _attendanceService.MarkSessionAttendanceAsync(sessionId, AdminUserId, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("workshops/{workshopId:guid}/mark")]
    public async Task<IActionResult> MarkWorkshopAttendance(
        Guid workshopId,
        [FromBody] AdminMarkWorkshopAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.AttendanceCorrect, "Workshop", workshopId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _attendanceService.MarkWorkshopAttendanceAsync(workshopId, AdminUserId, request, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}