using System.Security.Claims;
using Ethos.Api.Application.Videos;
using Ethos.Api.Contracts.Videos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/videos")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Video Management")]
public class AdminVideosController : ControllerBase
{
    private readonly IVideoService _videoService;
    private readonly ILogger<AdminVideosController> _logger;

    public AdminVideosController(
        IVideoService videoService,
        ILogger<AdminVideosController> logger)
    {
        _videoService = videoService;
        _logger = logger;
    }

    private Guid? CurrentAdminUserId
    {
        get
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out var guid) ? guid : null;
        }
    }

    /// <summary>
    /// Retrieve list of all videos with admin metadata (including hidden videos)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StudioVideoResponse>>> GetAllVideos(
        [FromQuery] string? section,
        [FromQuery] bool? includeInactive,
        CancellationToken cancellationToken)
    {
        var videos = await _videoService.GetAdminVideosAsync(section, includeInactive ?? true, cancellationToken);
        return Ok(videos);
    }

    /// <summary>
    /// Upload a new video to Cloudflare R2 and save metadata
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(105 * 1024 * 1024)] // 105 MB
    public async Task<ActionResult<StudioVideoResponse>> UploadVideo(
        [FromForm] VideoUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _videoService.UploadVideoAsync(
                request.File,
                request.Section,
                request.Title,
                request.Description,
                request.DisplayOrder,
                CurrentAdminUserId,
                cancellationToken);

            return CreatedAtAction(nameof(GetAllVideos), new { id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading video: {Message}", ex.Message);
            return StatusCode(500, new { message = "An error occurred during video upload. Please check Cloudflare R2 credentials." });
        }
    }

    /// <summary>
    /// Update video title, description, or display order
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StudioVideoResponse>> UpdateMetadata(
        Guid id,
        [FromBody] VideoUpdateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _videoService.UpdateVideoMetadataAsync(id, request, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Replace the underlying video file in R2 while preserving metadata
    /// </summary>
    [HttpPost("{id:guid}/replace")]
    [RequestSizeLimit(105 * 1024 * 1024)]
    public async Task<ActionResult<StudioVideoResponse>> ReplaceVideo(
        Guid id,
        [FromForm] VideoReplaceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _videoService.ReplaceVideoFileAsync(id, request.File, CurrentAdminUserId, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replacing video file {VideoId}: {Message}", id, ex.Message);
            return StatusCode(500, new { message = "Failed to replace video file." });
        }
    }

    /// <summary>
    /// Toggle visibility (Active vs Hidden) without deleting the file
    /// </summary>
    [HttpPatch("{id:guid}/toggle-active")]
    public async Task<ActionResult<StudioVideoResponse>> ToggleActive(
        Guid id,
        [FromBody] ToggleActiveDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _videoService.ToggleActiveAsync(id, dto.IsActive, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reorder display sequence of multiple videos
    /// </summary>
    [HttpPatch("reorder")]
    public async Task<IActionResult> ReorderVideos(
        [FromBody] VideoReorderRequest request,
        CancellationToken cancellationToken)
    {
        await _videoService.ReorderVideosAsync(request.Items, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Permanently delete video file from Cloudflare R2 and remove database record
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePermanently(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await _videoService.DeleteVideoPermanentlyAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

public class ToggleActiveDto
{
    public bool IsActive { get; set; }
}
