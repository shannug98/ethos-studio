using Ethos.Api.Application.Classes;
using Ethos.Api.Contracts.Classes;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/classes")]
public class ClassesController : ControllerBase
{
    private readonly IDanceClassService _danceClassService;

    public ClassesController(IDanceClassService danceClassService)
    {
        _danceClassService = danceClassService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DanceClassResponse>>> GetActiveClasses()
    {
        var response = await _danceClassService.GetActiveClassesAsync();
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DanceClassResponse>> GetClassById(Guid id)
    {
        var response = await _danceClassService.GetClassByIdAsync(id);

        if (response == null)
        {
            return NotFound(new { message = "Class not found." });
        }

        return Ok(response);
    }

    [HttpGet("{id:guid}/schedule")]
    public async Task<ActionResult<IReadOnlyList<ClassScheduleResponse>>> GetClassSchedules(Guid id)
    {
        var response = await _danceClassService.GetClassSchedulesAsync(id);
        return Ok(response);
    }
}

[ApiController]
[Route("api/schedules")]
public class SchedulesController : ControllerBase
{
    private readonly IDanceClassService _danceClassService;

    public SchedulesController(IDanceClassService danceClassService)
    {
        _danceClassService = danceClassService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClassScheduleResponse>>> GetAllSchedules()
    {
        var response = await _danceClassService.GetAllSchedulesAsync();
        return Ok(response);
    }
}
