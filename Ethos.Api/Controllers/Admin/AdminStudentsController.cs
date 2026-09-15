using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Students")]
public class AdminStudentsController : ControllerBase
{
    private readonly IAdminStudentService _studentService;
    private readonly IAdminUserService _userService;
    private readonly IAdminAuthorizationService _authService;

    public AdminStudentsController(
        IAdminStudentService studentService,
        IAdminUserService userService,
        IAdminAuthorizationService authService)
    {
        _studentService = studentService;
        _userService = userService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("students")]
    public async Task<ActionResult<PagedResult<AdminStudentListResponse>>> GetStudents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? profileStatus = null,
        [FromQuery] string? accountStatus = null,
        [FromQuery] string? packageStatus = null,
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.StudentView, "Student", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _studentService.GetStudentsAsync(page, pageSize, search, profileStatus, accountStatus, packageStatus, cancellationToken);
        return Ok(result);
    }

    [HttpGet("students/stats")]
    public async Task<ActionResult<AdminStudentSummaryStatsResponse>> GetStudentStats(
        CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.StudentView, "Student", null, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _studentService.GetStudentStatsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("students/{studentId:guid}")]
    public async Task<ActionResult<AdminStudentDetailsResponse>> GetStudentById(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.StudentView, "Student", studentId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var result = await _studentService.GetStudentByIdAsync(studentId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("students/{studentId:guid}/diagnostics")]
    public async Task<ActionResult<StudentDiagnosticReport>> GetStudentDiagnostics(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.StudentView, "Student", studentId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var traceId = HttpContext.GetTraceId();
        var report = await _studentService.GetStudentDiagnosticsAsync(studentId, traceId, cancellationToken);
        if (report == null) return NotFound();
        return Ok(report);
    }

    [HttpPatch("students/{studentId:guid}/status")]
    public async Task<IActionResult> UpdateStudentStatus(
        Guid studentId,
        [FromBody] AdminUpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.StudentUpdate, "Student", studentId, HttpContext, cancellationToken);
        if (!authCheck.Success)
            return StatusCode(authCheck.StatusCode, new { error = authCheck.ErrorCode, message = authCheck.ErrorMessage });

        var student = await _studentService.GetStudentByIdAsync(studentId, cancellationToken);
        if (student == null) return NotFound(new { message = "Student not found." });

        try
        {
            // Per Adjustment 12: Route through authoritative account status service
            await _userService.UpdateUserStatusAsync(student.UserId, AdminUserId, request.IsActive, request.Reason, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
