using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Students;

public class StudentEligibilityService : IStudentEligibilityService
{
    private readonly AppDbContext _db;

    public StudentEligibilityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<StudentEligibilityResult> CheckEligibilityByPhoneAsync(
        string phone,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = phone.Trim();
        if (normalizedPhone.StartsWith("+91"))
            normalizedPhone = normalizedPhone[3..];
        normalizedPhone = new string(normalizedPhone.Where(char.IsDigit).ToArray());
        if (normalizedPhone.Length > 10)
            normalizedPhone = normalizedPhone[^10..];

        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.StudentProfile!)
                .ThenInclude(sp => sp.StudentPackages)
            .FirstOrDefaultAsync(u => u.Phone == normalizedPhone, cancellationToken);

        if (user is null)
        {
            return new StudentEligibilityResult(
                false,
                false,
                false,
                "No Ethos membership found for this mobile number. User not found.");
        }

        var hasStudentRole = user.UserRoles.Any(r => r.Role != null && r.Role.Code == "STUDENT");
        if (!hasStudentRole)
        {
            return new StudentEligibilityResult(
                true,
                false,
                false,
                "Please purchase a monthly class package to activate your student account.");
        }

        var now = DateTime.UtcNow;
        var hasActivePackage = user.StudentProfile != null &&
            user.StudentProfile.StudentPackages.Any(p =>
                p.Status == StudentPackageStatus.Active && p.ExpiryDate >= now);

        if (!hasActivePackage)
        {
            return new StudentEligibilityResult(
                true,
                true,
                false,
                "Please purchase a monthly class package to access the Student Portal.");
        }

        return new StudentEligibilityResult(true, true, true);
    }

    public async Task<bool> IsStudentPortalEligibleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.StudentProfile!)
                .ThenInclude(sp => sp.StudentPackages)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user is null)
            return false;

        var hasStudentRole = user.UserRoles.Any(r => r.Role != null && r.Role.Code == "STUDENT");
        if (!hasStudentRole)
            return false;

        var now = DateTime.UtcNow;
        return user.StudentProfile != null &&
            user.StudentProfile.StudentPackages.Any(p =>
                p.Status == StudentPackageStatus.Active && p.ExpiryDate >= now);
    }
}
