using System.Security.Claims;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Trainers;
using Ethos.Api.Application.Trainers.DTOs;
using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/trainers")]
[Authorize]
public class TrainersController : ControllerBase
{
    private readonly ITrainerService _trainerService;
    private readonly ITrainerApplicationService _applicationService;
    private readonly ITrainerAvailabilityService _availabilityService;
    private readonly ITrainerTierService _tierService;
    private readonly ITrainerUpgradeService _upgradeService;
    private readonly ITrainerPerformanceService _performanceService;
    private readonly ITrainerWorkshopService _workshopService;
    private readonly ITrainerGalleryService _galleryService;

    public TrainersController(
        ITrainerService trainerService,
        ITrainerApplicationService applicationService,
        ITrainerAvailabilityService availabilityService,
        ITrainerTierService tierService,
        ITrainerUpgradeService upgradeService,
        ITrainerPerformanceService performanceService,
        ITrainerWorkshopService workshopService,
        ITrainerGalleryService galleryService)
    {
        _trainerService = trainerService;
        _applicationService = applicationService;
        _availabilityService = availabilityService;
        _tierService = tierService;
        _upgradeService = upgradeService;
        _performanceService = performanceService;
        _workshopService = workshopService;
        _galleryService = galleryService;
    }

    private Guid CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(value!);
        }
    }

    // Pre-Approval Application Endpoints (Authenticated User)
    [HttpGet("me/application")]
    public async Task<ActionResult<TrainerApplicationResponse>> GetApplication(CancellationToken cancellationToken)
    {
        var result = await _applicationService.GetMineAsync(CurrentUserId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("me/application")]
    public async Task<ActionResult<TrainerApplicationResponse>> CreateApplication(
        [FromBody] CreateTrainerApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.CreateAsync(CurrentUserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("me/application")]
    public async Task<ActionResult<TrainerApplicationResponse>> UpdateApplication(
        [FromBody] UpdateTrainerApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.UpdateAsync(CurrentUserId, request, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("me/application/submit")]
    public async Task<IActionResult> SubmitApplication(CancellationToken cancellationToken)
    {
        try
        {
            await _applicationService.SubmitAsync(CurrentUserId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/application/video")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<ActionResult<TrainerApplicationVideoDto>> UploadApplicationVideo(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;

        try
        {
            var result =
                await _applicationService.UploadVideoAsync(
                    userId,
                    file,
                    cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("me/application/video")]
    public async Task<IActionResult> DeleteApplicationVideo(
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;

        await _applicationService.DeleteVideoAsync(
            userId,
            cancellationToken);

        return NoContent();
    }

    [HttpGet("me/application/video")]
    public async Task<ActionResult<TrainerApplicationVideoDto>> GetApplicationVideo(
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;

        var result =
            await _applicationService.GetVideoAsync(
                userId,
                cancellationToken);

        if (result is null)
        {
            return NoContent();
        }

        return Ok(result);
    }

    [HttpGet("me/application/video/stream")]
    public async Task<IActionResult> StreamMyApplicationVideo(CancellationToken cancellationToken)
    {
        var streamInfo = await _applicationService.GetVideoStreamAsync(CurrentUserId, cancellationToken);
        if (streamInfo == null)
        {
            return NotFound(new { message = "No application video found." });
        }

        return PhysicalFile(streamInfo.Value.PhysicalPath, streamInfo.Value.ContentType, enableRangeProcessing: true);
    }

    // Approved Trainer Endpoints (Requires TRAINER Role)
    [HttpGet("me")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<TrainerResponse>> GetMe(CancellationToken cancellationToken)
    {
        var result = await _trainerService.GetMeAsync(CurrentUserId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPut("me")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<TrainerResponse>> UpdateMe(
        [FromBody] UpdateTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _trainerService.UpdateMeAsync(CurrentUserId, request, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("me/profile-photo")]
    [Authorize(Roles = "TRAINER")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<TrainerResponse>> UploadProfilePhoto(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _trainerService.UploadProfilePhotoAsync(CurrentUserId, file, cancellationToken);
            if (result == null) return NotFound();
            return Ok(result);
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

    [HttpGet("me/dashboard")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<TrainerDashboardResponse>> Dashboard(CancellationToken cancellationToken)
    {
        var result = await _trainerService.GetDashboardAsync(CurrentUserId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("tiers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTiers(CancellationToken cancellationToken)
    {
        var result = await _trainerService.GetTiersAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("me/tier")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<TrainerTierResponse>> GetTier(CancellationToken cancellationToken)
    {
        var result = await _tierService.GetMyTierAsync(CurrentUserId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("me/tier-history")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<IReadOnlyList<TrainerTierHistoryResponse>>> GetTierHistory(CancellationToken cancellationToken)
    {
        var result = await _tierService.GetMyTierHistoryAsync(CurrentUserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("me/permissions")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<IReadOnlyList<TrainerPermissionResponse>>> GetPermissions(CancellationToken cancellationToken)
    {
        var result = await _tierService.GetMyPermissionsAsync(CurrentUserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("me/availability")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<IReadOnlyList<TrainerAvailabilityResponse>>> GetAvailability(CancellationToken cancellationToken)
    {
        var result = await _availabilityService.GetMineAsync(CurrentUserId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("me/availability")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<IReadOnlyList<TrainerAvailabilityResponse>>> ReplaceAvailability(
        [FromBody] List<TrainerAvailabilityRequest> requests,
        CancellationToken cancellationToken)
    {
        var result = await _availabilityService.ReplaceMineAsync(CurrentUserId, requests, cancellationToken);
        return Ok(result);
    }

    [HttpGet("me/upgrade-requests")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> GetUpgradeRequests(CancellationToken cancellationToken)
    {
        var result = await _trainerService.GetUpgradeRequestsAsync(CurrentUserId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("me/upgrade-requests")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> CreateUpgradeRequest(
        [FromBody] RequestTrainerUpgradeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _trainerService.RequestUpgradeAsync(CurrentUserId, request.RequestedTierId, cancellationToken);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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

    [HttpDelete("me/upgrade-requests/{id:guid}")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> CancelUpgradeRequest(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _upgradeService.CancelAsync(CurrentUserId, id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/performance")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> GetPerformance(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _performanceService.GetMyPerformanceAsync(CurrentUserId, cancellationToken);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("me/performance/history")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> GetPerformanceHistory(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _performanceService.GetMyPerformanceHistoryAsync(CurrentUserId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    // Trainer Workshop Endpoints
    [HttpGet("me/workshops")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<IReadOnlyList<TrainerWorkshopResponse>>> GetMyWorkshops(CancellationToken cancellationToken)
    {
        var result = await _workshopService.GetMyWorkshopsAsync(CurrentUserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("me/workshops/{id:guid}")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<TrainerWorkshopResponse>> GetMyWorkshopById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workshopService.GetMyWorkshopByIdAsync(CurrentUserId, id, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("me/workshops")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<TrainerWorkshopResponse>> CreateWorkshop(
        [FromBody] TrainerWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workshopService.CreateWorkshopAsync(CurrentUserId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("me/workshops/{id:guid}")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<TrainerWorkshopResponse>> UpdateWorkshop(
        Guid id,
        [FromBody] TrainerWorkshopRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workshopService.UpdateWorkshopAsync(CurrentUserId, id, request, cancellationToken);
            if (result == null) return NotFound(new { message = "Workshop not found or access denied." });
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/workshops/{id:guid}/submit")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> SubmitWorkshop(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _workshopService.SubmitWorkshopAsync(CurrentUserId, id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/workshops/{id:guid}/cancel")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> CancelWorkshop(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _workshopService.CancelWorkshopAsync(CurrentUserId, id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/workshops/{id:guid}/students")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<IReadOnlyList<TrainerWorkshopStudentResponse>>> GetWorkshopStudents(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workshopService.GetWorkshopStudentsAsync(CurrentUserId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/workshops/{id:guid}/students/{studentId:guid}/status")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> UpdateWorkshopStudentStatus(
        Guid id,
        Guid studentId,
        [FromBody] UpdateWorkshopBookingStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _workshopService.UpdateWorkshopStudentStatusAsync(CurrentUserId, id, studentId, request.Status, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/workshops/{id:guid}/tickets/validate")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<TicketValidationResponse>> ValidateWorkshopTicket(
        Guid id,
        [FromBody] TicketValidationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workshopService.ValidateTicketAsync(CurrentUserId, id, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("me/workshops/{id:guid}/tickets/{ticketId:guid}/check-in")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<TicketValidationResponse>> CheckInWorkshopTicket(
        Guid id,
        Guid ticketId,
        [FromBody] CheckInTicketRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workshopService.CheckInTicketAsync(CurrentUserId, id, ticketId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("me/workshops/{id:guid}/bookings/{bookingId:guid}/group-check-in")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<GroupCheckInResponse>> GroupCheckIn(
        Guid id,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workshopService.GroupCheckInAsync(CurrentUserId, id, bookingId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/workshops/{id:guid}/attendance")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<WorkshopAttendanceSummaryResponse>> GetWorkshopAttendance(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workshopService.GetWorkshopAttendanceSummaryAsync(CurrentUserId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("me/workshops/{id:guid}/tickets/{ticketId:guid}/re-entry")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> RecordWorkshopReEntry(
        Guid id,
        Guid ticketId,
        [FromQuery] AttendanceEventType eventType,
        CancellationToken cancellationToken)
    {
        try
        {
            await _workshopService.RecordReEntryAsync(CurrentUserId, id, ticketId, eventType, cancellationToken);
            return Ok(new { message = $"Attendance event {eventType} recorded successfully." });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/workshops/{id:guid}/feedback")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<IReadOnlyList<TrainerWorkshopFeedbackResponse>>> GetWorkshopFeedback(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _workshopService.GetWorkshopFeedbackAsync(CurrentUserId, id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Trainer Gallery Endpoints
    [HttpGet("me/gallery")]
    [Authorize(Roles = "TRAINER")]
    public async Task<ActionResult<IReadOnlyList<TrainerGalleryImageDto>>> GetGallery(CancellationToken cancellationToken)
    {
        var result = await _galleryService.GetMineAsync(CurrentUserId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("me/gallery")]
    [Authorize(Roles = "TRAINER")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<TrainerGalleryImageDto>> UploadGalleryImage(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _galleryService.UploadAsync(CurrentUserId, file, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("me/gallery/{id:guid}")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> DeleteGalleryImage(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _galleryService.DeleteAsync(CurrentUserId, id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("me/gallery/{id:guid}/image")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> GetGalleryImageFile(Guid id, CancellationToken cancellationToken)
    {
        var result = await _galleryService.GetImageFileAsync(CurrentUserId, id, cancellationToken);
        if (result is null) return NotFound();
        return PhysicalFile(result.Value.PhysicalPath, result.Value.ContentType);
    }

    [HttpDelete("me/notifications/{id:guid}")]
    [Authorize(Roles = "TRAINER")]
    public async Task<IActionResult> DeleteNotification(
        Guid id,
        [FromServices] INotificationService notificationService)
    {
        try
        {
            var success = await notificationService.DeleteNotificationAsync(id);
            if (!success)
            {
                return NotFound(new { message = "Notification not found." });
            }

            return Ok(new { message = "Notification deleted successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
