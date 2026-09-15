using Ethos.Api.Application.Attendance;
using Ethos.Api.Application.Classes;
using Ethos.Api.Application.Dashboard;
using Ethos.Api.Application.Packages;
using Ethos.Api.Application.Payments;
using Ethos.Api.Application.Students;
using Ethos.Api.Application.Workshops;
using Ethos.Api.Contracts.Attendance;
using Ethos.Api.Contracts.Classes;
using Ethos.Api.Contracts.Dashboard;
using Ethos.Api.Contracts.Packages;
using Ethos.Api.Contracts.Payments;
using Ethos.Api.Contracts.Students;
using Ethos.Api.Contracts.Workshops;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Roles = "STUDENT")]
public class StudentsController : ControllerBase
{
    private readonly IStudentProfileService _studentProfileService;
    private readonly IPackageService _packageService;
    private readonly IDanceClassService _danceClassService;
    private readonly IAttendanceService _attendanceService;
    private readonly IWorkshopService _workshopService;
    private readonly IPaymentService _paymentService;
    private readonly IStudentDashboardService _dashboardService;

    public StudentsController(
        IStudentProfileService studentProfileService,
        IPackageService packageService,
        IDanceClassService danceClassService,
        IAttendanceService attendanceService,
        IWorkshopService workshopService,
        IPaymentService paymentService,
        IStudentDashboardService dashboardService)
    {
        _studentProfileService = studentProfileService;
        _packageService = packageService;
        _danceClassService = danceClassService;
        _attendanceService = attendanceService;
        _workshopService = workshopService;
        _paymentService = paymentService;
        _dashboardService = dashboardService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<StudentProfileResponse>> GetMyProfile()
    {
        var response = await _studentProfileService.GetMyProfileAsync();
        return Ok(response);
    }

    [HttpPut("me")]
    public async Task<ActionResult<StudentProfileResponse>> UpdateMyProfile([FromBody] UpdateStudentProfileRequest request)
    {
        try
        {
            var response = await _studentProfileService.UpdateMyProfileAsync(request);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/profile-photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<StudentProfileResponse>> UploadProfilePhoto(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _studentProfileService.UploadProfilePhotoAsync(file, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/profile-photo")]
    public async Task<IActionResult> GetMyProfilePhoto(CancellationToken cancellationToken)
    {
        var result = await _studentProfileService.GetMyProfilePhotoStreamAsync(cancellationToken);
        if (result == null)
        {
            return NotFound(new { message = "Profile photo not found." });
        }

        return File(result.Value.Stream, result.Value.ContentType);
    }

    [HttpGet("me/dashboard")]
    public async Task<ActionResult<StudentDashboardResponse>> GetDashboard()
    {
        var response = await _dashboardService.GetStudentDashboardAsync();
        return Ok(response);
    }

    [HttpGet("me/packages")]
    public async Task<ActionResult<IReadOnlyList<StudentPackageResponse>>> GetMyPackages()
    {
        var response = await _packageService.GetMyPackagesAsync();
        return Ok(response);
    }

    [HttpGet("me/packages/active")]
    public async Task<ActionResult<StudentPackageResponse>> GetMyActivePackage()
    {
        var response = await _packageService.GetMyActivePackageAsync();

        if (response == null)
        {
            return NotFound(new { message = "No active package found." });
        }

        return Ok(response);
    }

    [HttpGet("me/classes")]
    public async Task<ActionResult<IReadOnlyList<DanceClassResponse>>> GetMyClasses()
    {
        var response = await _danceClassService.GetMyClassesAsync();
        return Ok(response);
    }

    [HttpGet("me/schedule")]
    public async Task<ActionResult<IReadOnlyList<ClassScheduleResponse>>> GetMySchedule()
    {
        var response = await _danceClassService.GetMyScheduleAsync();
        return Ok(response);
    }

    [HttpGet("me/enrollments")]
    public async Task<ActionResult<IReadOnlyList<ClassEnrollmentResponse>>> GetMyEnrollments()
    {
        var response = await _danceClassService.GetMyEnrollmentsAsync();
        return Ok(response);
    }

    [HttpPost("me/enrollments")]
    public async Task<ActionResult<ClassEnrollmentResponse>> EnrollInClass([FromBody] EnrollInClassRequest request)
    {
        try
        {
            var response = await _danceClassService.EnrollInClassAsync(request);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already actively enrolled", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = ex.Message });
            }

            if (ex.Message.Contains("active package is required", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }

            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("me/enrollments/{id:guid}")]
    public async Task<IActionResult> CancelEnrollment(Guid id)
    {
        var success = await _danceClassService.CancelEnrollmentAsync(id);

        if (!success)
        {
            return NotFound(new { message = "Enrollment not found or is already cancelled." });
        }

        return Ok(new { message = "Enrollment cancelled successfully." });
    }

    [HttpGet("me/attendance")]
    public async Task<ActionResult<IReadOnlyList<AttendanceRecordResponse>>> GetMyAttendance()
    {
        var response = await _attendanceService.GetMyAttendanceAsync();
        return Ok(response);
    }

    [HttpGet("me/attendance/summary")]
    public async Task<ActionResult<AttendanceSummaryResponse>> GetMyAttendanceSummary()
    {
        var response = await _attendanceService.GetMyAttendanceSummaryAsync();
        return Ok(response);
    }

    [HttpGet("me/workshops")]
    public async Task<ActionResult<IReadOnlyList<WorkshopBookingResponse>>> GetMyWorkshops()
    {
        var response = await _workshopService.GetMyBookingsAsync();
        return Ok(response);
    }

    [HttpPost("me/workshops/{id:guid}/book")]
    public async Task<ActionResult<WorkshopBookingResponse>> BookWorkshop(Guid id)
    {
        var response = await _workshopService.BookWorkshopAsync(id);
        return Ok(response);
    }

    [HttpDelete("me/workshops/{id:guid}/book")]
    public async Task<IActionResult> CancelWorkshopBooking(Guid id)
    {
        var success = await _workshopService.CancelBookingAsync(id);

        if (!success)
        {
            return NotFound(new { message = "Workshop booking not found." });
        }

        return Ok(new { message = "Workshop booking cancelled successfully." });
    }

    [HttpGet("me/workshops/feedback-legacy")]
    public async Task<ActionResult<IReadOnlyList<WorkshopFeedbackResponse>>> GetMyFeedbackLegacy()
    {
        var response = await _workshopService.GetMyFeedbackAsync();
        return Ok(response);
    }

    [HttpGet("me/payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentTransactionResponse>>> GetMyPayments()
    {
        var response = await _paymentService.GetMyPaymentsAsync();
        return Ok(response);
    }
}
