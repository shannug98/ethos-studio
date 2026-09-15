using Ethos.Api.Application.Packages;
using Ethos.Api.Contracts.Packages;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers;

[ApiController]
[Route("api/packages")]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _packageService;

    public PackagesController(IPackageService packageService)
    {
        _packageService = packageService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PackageResponse>>> GetActivePackages()
    {
        var response = await _packageService.GetActivePackagesAsync();
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PackageResponse>> GetPackageById(Guid id)
    {
        var response = await _packageService.GetPackageByIdAsync(id);

        if (response == null)
        {
            return NotFound(new { message = "Package not found." });
        }

        return Ok(response);
    }
}
