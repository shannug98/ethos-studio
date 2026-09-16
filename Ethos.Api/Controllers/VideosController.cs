using Ethos.Api.Application.Videos;
using Ethos.Api.Contracts.Videos;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/videos")]
public class VideosController : ControllerBase
{
    private readonly IVideoService _videoService;

    public VideosController(IVideoService videoService)
    {
        _videoService = videoService;
    }

    /// <summary>
    /// Fetch active videos for public display (section="ShortVideos" or "Gallery")
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StudioVideoResponse>>> GetVideos(
        [FromQuery] string? section,
        CancellationToken cancellationToken)
    {
        var targetSection = section ?? "ShortVideos";
        var videos = await _videoService.GetPublicVideosAsync(targetSection, cancellationToken);
        return Ok(videos);
    }

    /// <summary>
    /// Fetch a single public video by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudioVideoResponse>> GetVideoById(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var video = await _videoService.GetVideoByIdAsync(id, cancellationToken);
            if (!video.IsActive)
            {
                return NotFound(new { message = "Video is currently unavailable." });
            }
            return Ok(video);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Video not found." });
        }
    }
}
