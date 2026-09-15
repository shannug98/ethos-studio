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

    [HttpGet("{section}")]
    public async Task<ActionResult<IReadOnlyList<MediaItemResponse>>> GetPublicMedia(
        string section,
        CancellationToken cancellationToken)
    {
        var media = await _mediaService.GetPublicMediaBySectionAsync(section, cancellationToken);
        return Ok(media);
    }
}
