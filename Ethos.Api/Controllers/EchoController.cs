using Ethos.Api.Application.Echo;
using Ethos.Api.Contracts.Echo;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/echo")]
public class EchoController : ControllerBase
{
    private readonly IEchoAssistantService _echoAssistantService;

    public EchoController(IEchoAssistantService echoAssistantService)
    {
        _echoAssistantService = echoAssistantService;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<EchoChatResponse>> Chat(
        [FromBody] EchoChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message cannot be empty." });
        }

        var response = await _echoAssistantService.ProcessMessageAsync(request, cancellationToken);
        return Ok(response);
    }
}
