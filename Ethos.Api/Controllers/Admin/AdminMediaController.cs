using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Application.Media;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Media;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/media")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Media Management")]
public class AdminMediaController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly IAdminAuthorizationService _authService;

    public AdminMediaController(IMediaService mediaService, IAdminAuthorizationService authService)
    {
        _mediaService = mediaService; _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Upload ────────────────────────────────────────────────────────────────

    [HttpPost("upload")]
    [RequestSizeLimit(105 * 1024 * 1024)]
    public async Task<ActionResult<MediaUploadResponse>> UploadMedia(
        [FromForm] IFormFile file,
        [FromForm] List<string>? sections,
        [FromForm] string? section,
        [FromForm] string? mediaType,
        [FromForm] string? title,
        [FromForm] string? caption,
        [FromForm] string? altText,
        [FromForm] string? focalPoint,
        [FromForm] string? category,
        [FromForm] string? layoutType,
        [FromForm] int? displayOrder,
        [FromForm] bool? isPublished,
        [FromForm] bool? isFeatured,
        [FromForm] double? durationSeconds,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaUpload, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });

        // Accept both sections[] array and legacy single section field
        var effectiveSections = (sections?.Count > 0 ? sections : null)
            ?? (section != null ? new List<string> { section } : null)
            ?? new List<string> { MediaConstants.Sections.Draft };

        try
        {
            var result = await _mediaService.UploadMediaAsync(
                file, effectiveSections, mediaType, title, caption, altText,
                focalPoint, category, layoutType, displayOrder, isPublished, isFeatured,
                durationSeconds, AdminUserId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<PagedResult<MediaItemResponse>>> GetMedia(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? section = null, [FromQuery] string? mediaType = null,
        [FromQuery] string? category = null, [FromQuery] bool? isPublished = null,
        [FromQuery] bool? isArchived = null, CancellationToken cancellationToken = default)
    {
        var result = await _mediaService.GetAdminMediaPagedAsync(page, pageSize, section, mediaType, category, isPublished, isArchived, cancellationToken);
        return Ok(result);
    }

    // ── Update metadata ───────────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MediaItemResponse>> UpdateMedia(Guid id, [FromBody] AdminUpdateMediaRequest request, CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaUpload, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        try { return Ok(await _mediaService.UpdateMediaAsync(id, request, AdminUserId, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Placement endpoints ───────────────────────────────────────────────────

    [HttpPost("{id:guid}/placements")]
    public async Task<ActionResult<AdminPlacementResponse>> AddPlacement(Guid id, [FromBody] AdminPlacementRequest request, CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaUpload, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        try { return Ok(await _mediaService.AddPlacementAsync(id, request, AdminUserId, cancellationToken)); }
        catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}/placements/{placementId:guid}")]
    public async Task<ActionResult<AdminPlacementResponse>> UpdatePlacement(Guid id, Guid placementId, [FromBody] AdminPlacementRequest request, CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaUpload, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        try { return Ok(await _mediaService.UpdatePlacementAsync(placementId, request, AdminUserId, cancellationToken)); }
        catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpDelete("{id:guid}/placements/{placementId:guid}")]
    public async Task<IActionResult> RemovePlacement(Guid id, Guid placementId, CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaUpload, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        try { await _mediaService.RemovePlacementAsync(placementId, AdminUserId, cancellationToken); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/placements/{placementId:guid}/publish")]
    public async Task<ActionResult<AdminPlacementResponse>> TogglePlacementPublish(Guid id, Guid placementId, [FromBody] TogglePublishRequest body, CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaUpload, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        try { return Ok(await _mediaService.TogglePlacementPublishAsync(placementId, body.IsPublished, AdminUserId, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ── Archive / Restore ─────────────────────────────────────────────────────

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<MediaItemResponse>> ArchiveMedia(Guid id, CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaDelete, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        try { return Ok(await _mediaService.ArchiveMediaAsync(id, AdminUserId, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<MediaItemResponse>> RestoreMedia(Guid id, CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaUpload, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        try { return Ok(await _mediaService.RestoreMediaAsync(id, AdminUserId, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ── Reorder ───────────────────────────────────────────────────────────────

    [HttpPatch("reorder")]
    public async Task<IActionResult> ReorderMedia([FromBody] AdminReorderMediaRequest request, CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaUpload, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        await _mediaService.ReorderMediaAsync(request, AdminUserId, cancellationToken);
        return Ok(new { message = "Media reordered successfully" });
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMedia(Guid id, [FromQuery] bool permanent = false, CancellationToken cancellationToken = default)
    {
        var authCheck = await _authService.AuthorizeActionAsync(User, AdminPermissions.MediaDelete, "Media", null, HttpContext, cancellationToken);
        if (!authCheck.Success) return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        try { await _mediaService.DeleteMediaAsync(id, permanent, AdminUserId, cancellationToken); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex)         { return NotFound(new { message = ex.Message }); }
    }
}
