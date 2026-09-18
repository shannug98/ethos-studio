using Ethos.Api.Application.Media;
using Ethos.Api.Contracts.Media;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/media")]
public class PublicMediaController : ControllerBase
{
    private readonly IMediaService _mediaService;

    public PublicMediaController(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    /// <summary>
    /// Publicly accessible media feed for homepage banners, visual gallery, and showcases.
    /// Excludes internal storage keys, drafts, and archived items.
    /// </summary>
    [HttpGet("public")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<IReadOnlyList<PublicMediaResponse>>> GetPublicMedia(
        [FromQuery] string? section,
        [FromQuery] string? category,
        [FromQuery] string? mediaType,
        CancellationToken cancellationToken)
    {
        var media = await _mediaService.GetPublicMediaAsync(section, category, mediaType, cancellationToken);
        Response.Headers["Cache-Control"] = "public, max-age=60";
        return Ok(media);
    }

    /// <summary>
    /// Development utility endpoint to seed a default studio visual asset to Cloudflare R2 and Neon DB.
    /// </summary>
    [HttpPost("dev-seed-hero")]
    public async Task<IActionResult> DevSeedHero(CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Ethos.Web", "src", "assets", "hero", "hero-01.jpg");
        if (!System.IO.File.Exists(localPath))
        {
            localPath = Path.Combine(Directory.GetCurrentDirectory(), "Ethos.Web", "src", "assets", "hero", "hero-01.jpg");
        }
        if (!System.IO.File.Exists(localPath))
            return NotFound(new { message = $"Local asset not found: {localPath}" });

        var bytes = await System.IO.File.ReadAllBytesAsync(localPath, cancellationToken);
        var stream = new MemoryStream(bytes);
        var formFile = new FormFile(stream, 0, bytes.Length, "file", "hero-01.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var res = await _mediaService.UploadMediaAsync(
            formFile,
            new[] { "HomepageScrolling" },
            "Image",
            "Ethos Contemporary Movement",
            "Ethos Master Faculty choreography in studio showcase.",
            "Contemporary dance leap",
            "center",
            "General",
            "Square",
            1,
            true,
            false,
            null,
            null,
            cancellationToken);

        return Ok(res);
    }

    /// <summary>
    /// Serves binary media directly from Cloudflare R2 / storage cache with long-term HTTP caching.
    /// Ensures visual assets always render seamlessly even before custom CDN DNS propagation.
    /// </summary>
    [HttpGet("content/{id:guid}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetMediaContent(
        Guid id,
        [FromServices] Ethos.Api.Infrastructure.Persistence.AppDbContext db,
        [FromServices] Ethos.Api.Application.Storage.ICloudflareR2StorageService r2Service,
        CancellationToken cancellationToken)
    {
        var item = await db.MediaItems.FindAsync(new object[] { id }, cancellationToken);
        if (item == null || string.IsNullOrWhiteSpace(item.ObjectKey))
            return NotFound();

        var result = await r2Service.GetObjectStreamAsync(item.ObjectKey, cancellationToken);
        if (result == null) return NotFound();

        Response.Headers["Cache-Control"] = "public, max-age=86400";
        return File(result.Value.Stream, result.Value.ContentType);
    }
}
