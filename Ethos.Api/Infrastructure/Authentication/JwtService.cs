using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ethos.Api.Application.Auth;
using Ethos.Api.Domain.Authentication;
using Ethos.Api.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ethos.Api.Infrastructure.Authentication;

public class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public JwtTokenResult GenerateToken(
        User user,
        IEnumerable<string> roles,
        IEnumerable<Claim>? extraClaims = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            throw new InvalidOperationException(
                "JWT SecretKey is not configured.");
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(
            _settings.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(
                ClaimTypes.Name,
                user.FullName),

            new(
                ClaimTypes.MobilePhone,
                user.Phone),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

        foreach (var role in roles.Distinct())
        {
            claims.Add(
                new Claim(ClaimTypes.Role, role));
        }

        if (extraClaims != null)
        {
            foreach (var extraClaim in extraClaims)
            {
                claims.Add(extraClaim);
            }
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SecretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
