using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Classes;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Classes")]
public class AdminClassesController : ControllerBase
{
    private readonly IAdminClassService _classService;
    private readonly IAdminAuthorizationService _authService;

    public AdminClassesController(
        IAdminClassService classService,
        IAdminAuthorizationService authService)
    {
        _classService = classService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("classes")]
    public async Task<ActionResult<PagedResult<DanceClassResponse>>> GetClasses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassView, "DanceClass", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _classService.GetClassesAsync(page, pageSize, search, status, isActive, cancellationToken);
        return Ok(result);
    }

    [HttpGet("classes/{classId:guid}")]
    public async Task<ActionResult<DanceClassResponse>> GetClassById(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassView, "DanceClass", classId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _classService.GetClassByIdAsync(classId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("classes")]
    public async Task<ActionResult<DanceClassResponse>> CreateClass(
        [FromBody] CreateDanceClassRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassCreate, "DanceClass", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _classService.CreateClassAsync(AdminUserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetClassById), new { classId = result.Id }, result);
    }

    [HttpPut("classes/{classId:guid}")]
    public async Task<ActionResult<DanceClassResponse>> UpdateClass(
        Guid classId,
        [FromBody] UpdateDanceClassRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassUpdate, "DanceClass", classId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _classService.UpdateClassAsync(classId, AdminUserId, request, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPatch("classes/{classId:guid}/status")]
    public async Task<IActionResult> UpdateClassStatus(
        Guid classId,
        [FromBody] AdminUpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassCancel, "DanceClass", classId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(new { message = "Reason is required for class status update." });

        await _classService.UpdateClassStatusAsync(classId, AdminUserId, request.IsActive, request.Reason, cancellationToken);
        return NoContent();
    }

    [HttpGet("classes/{classId:guid}/dependencies")]
    public async Task<ActionResult<ClassDependencyCheckResponse>> CheckClassDependencies(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassView, "DanceClass", classId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _classService.CheckClassDependenciesAsync(classId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("classes/{classId:guid}")]
    public async Task<IActionResult> DeleteClass(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassCancel, "DanceClass", classId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _classService.DeleteClassAsync(classId, AdminUserId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            var check = await _classService.CheckClassDependenciesAsync(classId, cancellationToken);
            return Conflict(new
            {
                code = "CLASS_HAS_DEPENDENCIES",
                message = ex.Message,
                canArchive = check.CanArchive,
                dependencies = check.Dependencies
            });
        }
    }

    [HttpPost("classes/{classId:guid}/archive")]
    public async Task<IActionResult> ArchiveClass(
        Guid classId,
        [FromBody] ArchiveDanceClassRequest? request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassCancel, "DanceClass", classId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _classService.ArchiveClassAsync(classId, AdminUserId, request?.Reason, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("classes/{classId:guid}/restore")]
    public async Task<IActionResult> RestoreClass(
        Guid classId,
        [FromBody] RestoreDanceClassRequest? request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassCancel, "DanceClass", classId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _classService.RestoreClassAsync(classId, AdminUserId, request?.Reason, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("classes/{classId:guid}/schedules")]
    public async Task<ActionResult<IReadOnlyList<AdminClassScheduleResponse>>> GetClassSchedules(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassView, "ClassSchedule", classId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _classService.GetClassSchedulesAsync(classId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("schedules")]
    public async Task<ActionResult<IReadOnlyList<AdminClassScheduleResponse>>> GetAllSchedules(
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassView, "ClassSchedule", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        var result = await _classService.GetAllSchedulesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("classes/{classId:guid}/schedules")]
    public async Task<ActionResult<AdminClassScheduleResponse>> CreateSchedule(
        Guid classId,
        [FromBody] AdminClassScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassCreate, "ClassSchedule", classId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _classService.CreateScheduleAsync(classId, AdminUserId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("classes/{classId:guid}/schedules/{scheduleId:guid}")]
    public async Task<ActionResult<AdminClassScheduleResponse>> UpdateSchedule(
        Guid classId,
        Guid scheduleId,
        [FromBody] AdminClassScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassUpdate, "ClassSchedule", scheduleId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _classService.UpdateScheduleAsync(classId, scheduleId, AdminUserId, request, cancellationToken);
            if (result == null) return NotFound(new { message = "Schedule not found." });
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("classes/{classId:guid}/schedules/{scheduleId:guid}/activate")]
    public async Task<IActionResult> ActivateSchedule(
        Guid classId,
        Guid scheduleId,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassUpdate, "ClassSchedule", scheduleId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _classService.ActivateScheduleAsync(classId, scheduleId, AdminUserId, reason, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("classes/{classId:guid}/schedules/{scheduleId:guid}")]
    public async Task<IActionResult> DeactivateSchedule(
        Guid classId,
        Guid scheduleId,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassCancel, "ClassSchedule", scheduleId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _classService.DeactivateScheduleAsync(classId, scheduleId, AdminUserId, reason, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("classes/{classId:guid}/schedules/{scheduleId:guid}/dependencies")]
    public async Task<ActionResult<ScheduleDependencyCheckResponse>> CheckScheduleDependencies(
        Guid classId,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassView, "ClassSchedule", scheduleId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            var result = await _classService.CheckScheduleDependenciesAsync(classId, scheduleId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("classes/{classId:guid}/schedules/{scheduleId:guid}/permanent")]
    public async Task<IActionResult> DeleteSchedule(
        Guid classId,
        Guid scheduleId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.ClassCancel, "ClassSchedule", scheduleId, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        try
        {
            await _classService.DeleteScheduleAsync(classId, scheduleId, AdminUserId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            var check = await _classService.CheckScheduleDependenciesAsync(classId, scheduleId, cancellationToken);
            return Conflict(new
            {
                code = "SCHEDULE_HAS_DEPENDENCIES",
                message = ex.Message,
                canDeactivate = check.CanDeactivate,
                sessionsCount = check.SessionsCount,
                attendanceRecordsCount = check.AttendanceRecordsCount
            });
        }
    }
}
