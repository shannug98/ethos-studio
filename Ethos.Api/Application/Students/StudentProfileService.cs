using System.Globalization;
using Ethos.Api.Application.Notifications;
using Ethos.Api.Application.Storage;
using Ethos.Api.Contracts.Notifications;
using Ethos.Api.Contracts.Students;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Ethos.Api.Infrastructure.Authentication;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Students;

public class StudentProfileService : IStudentProfileService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudentProfilePhotoStorageService _photoStorage;
    private readonly INotificationService _notificationService;

    public StudentProfileService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IStudentProfilePhotoStorageService photoStorage,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _photoStorage = photoStorage;
        _notificationService = notificationService;
    }

    public async Task<StudentProfileResponse> GetMyProfileAsync()
    {
        var userId = _currentUser.UserId;

        var user = await _dbContext.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User account was not found.");
        }

        EnsureStudentRole(user);

        return MapToResponse(user);
    }

    public async Task<StudentProfileResponse> UpdateMyProfileAsync(
        UpdateStudentProfileRequest request)
    {
        var userId = _currentUser.UserId;

        var user = await _dbContext.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User account was not found.");
        }

        EnsureStudentRole(user);

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ArgumentException("Full name is required.");
        }

        // Validate DateOfBirth if provided
        if (!string.IsNullOrWhiteSpace(request.DateOfBirth))
        {
            if (DateTime.TryParseExact(request.DateOfBirth.Trim(),
                    new[] { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd-MM-yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDob))
            {
                if (parsedDob > DateTime.UtcNow.Date)
                {
                    throw new ArgumentException("Date of birth cannot be in the future.");
                }

                if (parsedDob < DateTime.UtcNow.Date.AddYears(-120))
                {
                    throw new ArgumentException("Please enter a valid date of birth.");
                }
            }
            else
            {
                throw new ArgumentException("Date of birth format is invalid. Please use YYYY-MM-DD.");
            }
        }

        // Validate and normalize Emergency Contact Phone if provided
        string? normalizedEmergencyPhone = null;
        if (!string.IsNullOrWhiteSpace(request.EmergencyContactPhone))
        {
            var digits = new string(request.EmergencyContactPhone.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91"))
            {
                digits = digits[2..];
            }
            else if (digits.Length == 11 && digits.StartsWith("0"))
            {
                digits = digits[1..];
            }

            if (digits.Length != 10)
            {
                throw new ArgumentException("Emergency contact phone must be a valid 10-digit mobile number.");
            }

            normalizedEmergencyPhone = digits;
        }

        // User-level information update
        user.FullName = request.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        // Student profile update
        var profile = user.StudentProfile;
        if (profile == null)
        {
            profile = new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.StudentProfiles.Add(profile);
            user.StudentProfile = profile;
        }

        profile.DateOfBirth = string.IsNullOrWhiteSpace(request.DateOfBirth) ? null : request.DateOfBirth.Trim();
        profile.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        profile.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        profile.EmergencyContactName = string.IsNullOrWhiteSpace(request.EmergencyContactName) ? null : request.EmergencyContactName.Trim();
        profile.EmergencyContactPhone = normalizedEmergencyPhone;
        profile.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(new CreateNotificationRequest
        {
            UserId = userId,
            Type = NotificationType.Account,
            Title = "Profile Details Updated 👤",
            Message = "Your student profile details were updated successfully.",
            Channel = NotificationChannel.InApp,
            ActionUrl = "/student/profile",
            EventKey = $"ProfileUpdated:{user.Id}:{DateTime.UtcNow.Ticks}"
        });

        return MapToResponse(user);
    }

    public async Task<StudentProfileResponse> UploadProfilePhotoAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var user = await _dbContext.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User account was not found.");
        }

        EnsureStudentRole(user);

        var profile = user.StudentProfile;
        if (profile == null)
        {
            profile = new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.StudentProfiles.Add(profile);
            user.StudentProfile = profile;
        }

        var oldPhotoPath = profile.ProfilePhotoUrl;

        var photoUrl = await _photoStorage.SaveAsync(userId, file, cancellationToken);

        profile.ProfilePhotoUrl = photoUrl;
        profile.UpdatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _notificationService.SendNotificationAsync(new CreateNotificationRequest
        {
            UserId = userId,
            Type = NotificationType.Account,
            Title = "Profile Avatar Updated 📸",
            Message = "Your profile photo has been successfully updated.",
            Channel = NotificationChannel.InApp,
            ActionUrl = "/student/profile",
            EventKey = $"ProfilePhotoUpdated:{user.Id}:{DateTime.UtcNow.Ticks}"
        }, cancellationToken);

        // Best-effort cleanup of old photo
        if (!string.IsNullOrWhiteSpace(oldPhotoPath))
        {
            try
            {
                await _photoStorage.DeleteAsync(oldPhotoPath, cancellationToken);
            }
            catch
            {
                // Silently ignore cleanup errors
            }
        }

        return MapToResponse(user);
    }

    public async Task<(Stream Stream, string ContentType)?> GetMyProfilePhotoStreamAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var user = await _dbContext.Users
            .Include(u => u.StudentProfile)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User account was not found.");
        }

        EnsureStudentRole(user);

        var photoUrl = user.StudentProfile?.ProfilePhotoUrl;
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return null;
        }

        var relativeStoragePath = photoUrl.StartsWith("/uploads/student-profile-photos/")
            ? photoUrl.Replace("/uploads/student-profile-photos/", "App_Data/uploads/student-profile-photos/")
            : photoUrl;

        return await _photoStorage.GetPhotoStreamAsync(relativeStoragePath, cancellationToken);
    }

    private static void EnsureStudentRole(User user)
    {
        var isStudent = user.UserRoles
            .Any(ur => ur.Role.Code == "STUDENT");

        if (!isStudent)
        {
            throw new UnauthorizedAccessException("The authenticated user does not have the Student role.");
        }
    }

    private static StudentProfileResponse MapToResponse(User user)
    {
        var profile = user.StudentProfile;

        // Calculate profile completeness across all 9 student profile fields
        var totalFields = 9;
        var completedFields = 0;
        if (!string.IsNullOrWhiteSpace(user.FullName)) completedFields++;
        if (!string.IsNullOrWhiteSpace(user.Email)) completedFields++;
        if (!string.IsNullOrWhiteSpace(profile?.City)) completedFields++;
        if (!string.IsNullOrWhiteSpace(profile?.DateOfBirth)) completedFields++;
        if (!string.IsNullOrWhiteSpace(profile?.Gender)) completedFields++;
        if (!string.IsNullOrWhiteSpace(profile?.Bio)) completedFields++;
        if (!string.IsNullOrWhiteSpace(profile?.EmergencyContactName)) completedFields++;
        if (!string.IsNullOrWhiteSpace(profile?.EmergencyContactPhone)) completedFields++;
        if (!string.IsNullOrWhiteSpace(profile?.ProfilePhotoUrl)) completedFields++;

        var completionPct = completedFields == totalFields
            ? 100
            : Math.Min(95, (int)Math.Round((double)completedFields / totalFields * 100));

        return new StudentProfileResponse
        {
            UserId = user.Id,
            CustomerCode = user.CustomerCode,
            FullName = user.FullName,
            Phone = user.Phone,
            Email = user.Email,
            ProfileCompleted = profile != null,
            ProfileCompletionPercentage = completionPct,
            DateOfBirth = profile?.DateOfBirth,
            Gender = profile?.Gender,
            City = profile?.City,
            ProfilePhotoUrl = profile?.ProfilePhotoUrl,
            EmergencyContactName = profile?.EmergencyContactName,
            EmergencyContactPhone = profile?.EmergencyContactPhone,
            Bio = profile?.Bio,
            MemberSince = profile?.CreatedAt ?? user.CreatedAt,
            CreatedAt = profile?.CreatedAt,
            UpdatedAt = profile?.UpdatedAt
        };
    }
}
