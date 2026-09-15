using Ethos.Api.Domain.Entities;

namespace Ethos.Api.Application.Auth;

public interface IJwtService
{
    JwtTokenResult GenerateToken(
        User user,
        IEnumerable<string> roles,
        IEnumerable<System.Security.Claims.Claim>? extraClaims = null);
}

public record JwtTokenResult(
    string AccessToken,
    DateTime ExpiresAt);
