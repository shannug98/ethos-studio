using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Infrastructure.Authentication;

public interface ICurrentUserService
{
    Guid UserId { get; }

    string? Phone { get; }

    string? Name { get; }

    IReadOnlyList<string> Roles { get; }

    bool IsAuthenticated { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?
            .User?
            .Identity?
            .IsAuthenticated == true;

    public Guid UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?
                .User?
                .FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(value, out var userId))
            {
                throw new UnauthorizedAccessException(
                    "Authenticated user ID is missing.");
            }

            return userId;
        }
    }

    public string? Phone =>
        _httpContextAccessor.HttpContext?
            .User?
            .FindFirstValue(ClaimTypes.MobilePhone);

    public string? Name =>
        _httpContextAccessor.HttpContext?
            .User?
            .FindFirstValue(ClaimTypes.Name);

    public IReadOnlyList<string> Roles =>
        _httpContextAccessor.HttpContext?
            .User?
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList()
        ?? [];
}
