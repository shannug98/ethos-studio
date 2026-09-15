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

    public AdminMediaController(
        IMediaService mediaService,
        IAdminAuthorizationService authService)
    {
        _mediaService = mediaService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("upload")]
    [RequestSizeLimit(105 * 1024 * 1024)] // 105 MB
    public async Task<ActionResult<MediaUploadResponse>> UploadMedia(
        [FromForm] IFormFile file,
        [FromForm] string? section,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.MediaUpload,
            "Media",
            null,
            HttpContext,
            cancellationToken);

        if (!authCheck.Success)
        {
            return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        }

        try
        {
            var result = await _mediaService.UploadMediaAsync(
                file,
                section ?? "general",
                AdminUserId,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<MediaItemResponse>>> GetMedia(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? section = null,
        [FromQuery] string? mediaType = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediaService.GetAdminMediaPagedAsync(
            page,
            pageSize,
            section,
            mediaType,
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMedia(
        Guid id,
        CancellationToken cancellationToken)
    {
        var authCheck = await _authService.AuthorizeActionAsync(
            User,
            AdminPermissions.MediaDelete,
            "Media",
            null,
            HttpContext,
            cancellationToken);

        if (!authCheck.Success)
        {
            return StatusCode(authCheck.StatusCode, new { message = authCheck.ErrorMessage });
        }

        try
        {
            await _mediaService.DeleteMediaAsync(id, AdminUserId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
