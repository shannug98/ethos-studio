using Ethos.Api.Application.Students;
using Ethos.Api.Contracts.Auth;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;
    private readonly IStudentEligibilityService _studentEligibilityService;

    public AuthService(
        AppDbContext db,
        IJwtService jwtService,
        IPasswordService passwordService,
        IStudentEligibilityService studentEligibilityService)
    {
        _db = db;
        _jwtService = jwtService;
        _passwordService = passwordService;
        _studentEligibilityService = studentEligibilityService;
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) ||
            string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        if (currentPassword == newPassword)
        {
            return false;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return false;
        }

        var currentPasswordValid = _passwordService.VerifyPassword(
            currentPassword,
            user.PasswordHash);

        if (!currentPasswordValid)
        {
            return false;
        }

        user.PasswordHash = _passwordService.HashPassword(newPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}

