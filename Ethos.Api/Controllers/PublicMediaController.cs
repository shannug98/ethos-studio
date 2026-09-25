using Ethos.Api.Application.Media;
using Ethos.Api.Contracts.Media;
using Ethos.Api.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/media")]
public class PublicMediaController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly IWebHostEnvironment _env;

    public PublicMediaController(IMediaService mediaService, IWebHostEnvironment env)
    {
        _mediaService = mediaService;
        _env = env;
    }

    /// <summary>
    /// Publicly accessible media feed for homepage banners, visual gallery, and showcases.
    /// Excludes internal storage keys, drafts, and archived items.
    /// </summary>
    [HttpGet("public")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<IReadOnlyList<PublicMediaResponse>>> GetPublicMedia(
        [FromQuery] string? section,
        [FromQuery] string? category,
        [FromQuery] string? mediaType,
        CancellationToken cancellationToken)
    {
        var media = await _mediaService.GetPublicMediaAsync(section, category, mediaType, cancellationToken);
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        return Ok(media);
    }

    /// <summary>
    /// Development utility endpoint to seed a default studio visual asset to Cloudflare R2 and Neon DB.
    /// Gated strictly to development environment.
    /// </summary>
    [HttpPost("dev-seed-hero")]
    public async Task<IActionResult> DevSeedHero(CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

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
    /// Supports HTTP 206 Partial Content range requests for instant video playback and seeking.
    /// Strictly enforces public media eligibility (non-deleted, non-archived, public visibility, approved).
    /// </summary>
    [HttpGet("content/{id:guid}")]
    [HttpHead("content/{id:guid}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetMediaContent(
        Guid id,
        [FromServices] Ethos.Api.Infrastructure.Persistence.AppDbContext db,
        [FromServices] Ethos.Api.Application.Storage.ICloudflareR2StorageService r2Service,
        CancellationToken cancellationToken)
    {
        var item = await db.MediaItems.FirstOrDefaultAsync(m =>
            m.Id == id &&
            !m.IsDeleted &&
            !m.IsArchived &&
            m.Visibility == MediaConstants.Visibility.Public &&
            m.ApprovalStatus == MediaConstants.ApprovalStatus.Approved,
            cancellationToken);

        if (item == null || string.IsNullOrWhiteSpace(item.ObjectKey))
            return NotFound();

        Response.Headers["Accept-Ranges"] = "bytes";
        Response.Headers["Cache-Control"] = "public, max-age=86400";

        var rangeHeader = Request.Headers["Range"].ToString();
        if (string.IsNullOrWhiteSpace(rangeHeader))
        {
            var fullResult = await r2Service.GetObjectRangeStreamAsync(item.ObjectKey, null, null, cancellationToken);
            if (fullResult == null) return NotFound();

            Response.Headers["Content-Length"] = fullResult.ContentLength.ToString();
            return File(fullResult.Stream, fullResult.ContentType);
        }

        if (!TryParseRange(rangeHeader, out var fromByte, out var toByte))
        {
            var meta = await r2Service.GetObjectMetadataAsync(item.ObjectKey, cancellationToken);
            long total = meta?.ContentLength ?? item.FileSizeBytes;
            Response.Headers["Content-Range"] = $"bytes */{total}";
            return StatusCode(416);
        }

        var metadata = await r2Service.GetObjectMetadataAsync(item.ObjectKey, cancellationToken);
        long totalLength = metadata?.ContentLength ?? item.FileSizeBytes;

        if (totalLength > 0 && ((fromByte.HasValue && fromByte.Value >= totalLength) ||
            (fromByte.HasValue && toByte.HasValue && fromByte.Value > toByte.Value)))
        {
            Response.Headers["Content-Range"] = $"bytes */{totalLength}";
            return StatusCode(416);
        }

        var rangeResult = await r2Service.GetObjectRangeStreamAsync(item.ObjectKey, fromByte, toByte, cancellationToken);
        if (rangeResult == null) return NotFound();

        Response.StatusCode = 206;
        Response.Headers["Content-Range"] = $"bytes {rangeResult.FromByte}-{rangeResult.ToByte}/{rangeResult.TotalLength}";
        Response.Headers["Content-Length"] = rangeResult.ContentLength.ToString();
        return File(rangeResult.Stream, rangeResult.ContentType);
    }

    private static bool TryParseRange(string rangeHeader, out long? fromByte, out long? toByte)
    {
        fromByte = null;
        toByte = null;

        if (string.IsNullOrWhiteSpace(rangeHeader) || !rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            return false;

        var rangeSpec = rangeHeader["bytes=".Length..].Trim();
        if (rangeSpec.Contains(','))
            return false;

        var parts = rangeSpec.Split('-');
        if (parts.Length != 2)
            return false;

        if (string.IsNullOrWhiteSpace(parts[0]))
        {
            if (long.TryParse(parts[1], out var suffix) && suffix > 0)
            {
                toByte = suffix;
                return true;
            }
            return false;
        }

        if (long.TryParse(parts[0], out var from) && from >= 0)
        {
            fromByte = from;
            if (!string.IsNullOrWhiteSpace(parts[1]))
            {
                if (long.TryParse(parts[1], out var to) && to >= from)
                {
                    toByte = to;
                    return true;
                }
                return false;
            }
            return true;
        }

        return false;
    }
}
