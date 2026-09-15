using System.Security.Claims;
using Ethos.Api.Application.Admin;
using Ethos.Api.Contracts.Admin;
using Ethos.Api.Contracts.Packages;
using Ethos.Api.Domain.Constants;
using Ethos.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ethos.Api.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Tags("Admin - Packages")]
public class AdminPackagesController : ControllerBase
{
    private readonly IAdminPackageService _packageService;
    private readonly IAdminAuthorizationService _authService;

    public AdminPackagesController(IAdminPackageService packageService, IAdminAuthorizationService authService)
    {
        _packageService = packageService;
        _authService = authService;
    }

    private Guid AdminUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("packages")]
    public async Task<ActionResult<PagedResult<PackageResponse>>> GetPackages(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageView, "Package", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _packageService.GetPackagesAsync(page, pageSize, search, isActive, cancellationToken);
        return Ok(result);
    }

    [HttpGet("packages/{packageId:guid}")]
    public async Task<ActionResult<PackageResponse>> GetPackageById(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageView, "Package", packageId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var result = await _packageService.GetPackageByIdAsync(packageId, cancellationToken);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("packages")]
    public async Task<ActionResult<PackageResponse>> CreatePackage(
        [FromBody] CreatePackageRequest request,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageCreate, "Package", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var result = await _packageService.CreatePackageAsync(AdminUserId, request, cancellationToken);
            return CreatedAtAction(nameof(GetPackageById), new { packageId = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("packages/{packageId:guid}")]
    public async Task<ActionResult<PackageResponse>> UpdatePackage(
        Guid packageId,
        [FromBody] UpdatePackageRequest request,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageUpdate, "Package", packageId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var result = await _packageService.UpdatePackageAsync(packageId, AdminUserId, request, cancellationToken);
            if (result == null) return NotFound(new { message = "Package not found." });
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("packages/{packageId:guid}/status")]
    public async Task<IActionResult> UpdatePackageStatus(
        Guid packageId,
        [FromBody] AdminUpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageUpdate, "Package", packageId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            await _packageService.UpdatePackageStatusAsync(packageId, AdminUserId, request.IsActive, request.Reason, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("packages/stats")]
    public async Task<ActionResult<AdminPackageStatsResponse>> GetPackageStats(
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageView, "Package", null, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var stats = await _packageService.GetPackageStatsAsync(cancellationToken);
        return Ok(stats);
    }

    [HttpGet("packages/{packageId:guid}/dependencies")]
    public async Task<ActionResult<PackageDependencyCheckResponse>> CheckPackageDependencies(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageView, "Package", packageId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            var result = await _packageService.CheckPackageDependenciesAsync(packageId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("packages/{packageId:guid}")]
    public async Task<IActionResult> DeletePackage(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageDelete, "Package", packageId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        try
        {
            await _packageService.DeletePackageAsync(packageId, AdminUserId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            var check = await _packageService.CheckPackageDependenciesAsync(packageId, cancellationToken);
            return Conflict(new
            {
                code = "PACKAGE_HAS_DEPENDENCIES",
                message = ex.Message,
                canDeactivate = check.CanDeactivate,
                recommendedAction = check.RecommendedAction,
                recordsUsingThisPackage = check.RecordsUsingThisPackage
            });
        }
    }

    [HttpGet("packages/{packageId:guid}/details")]
    public async Task<ActionResult<AdminPackageDetailResponse>> GetPackageDetails(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageView, "Package", packageId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var detail = await _packageService.GetPackageDetailAsync(packageId, cancellationToken);
        if (detail == null)
        {
            return NotFound(new { message = "Package not found." });
        }
        return Ok(detail);
    }

    [HttpGet("packages/{packageId:guid}/activity")]
    public async Task<ActionResult<PagedResult<AdminPackageActivityItem>>> GetPackageActivity(
        Guid packageId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var auth = await _authService.AuthorizeActionAsync(User, AdminPermissions.PackageView, "Package", packageId, HttpContext, cancellationToken);
        if (!auth.Success) return StatusCode(auth.StatusCode, new { message = auth.ErrorMessage, errorCode = auth.ErrorCode });

        var activity = await _packageService.GetPackageActivityHistoryAsync(packageId, page, pageSize, cancellationToken);
        return Ok(activity);
    }
}
