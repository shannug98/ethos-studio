using Ethos.Api.Application.Storage;
using Ethos.Api.Application.Trainers.DTOs;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Trainers;

public class TrainerGalleryService : ITrainerGalleryService
{
    private readonly AppDbContext _dbContext;
    private readonly ITrainerGalleryStorageService _storageService;
    private readonly IWebHostEnvironment _environment;

    public TrainerGalleryService(
        AppDbContext dbContext,
        ITrainerGalleryStorageService storageService,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _environment = environment;
    }

    public async Task<IReadOnlyList<TrainerGalleryImageDto>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await GetProfileByUserIdAsync(userId, cancellationToken);

        var images = await _dbContext.TrainerGalleryImages
            .AsNoTracking()
            .Where(x => x.TrainerProfileId == profile.Id)
            .OrderBy(x => x.DisplayOrder)
            .ThenByDescending(x => x.UploadedAt)
            .Select(x => new TrainerGalleryImageDto
            {
                Id = x.Id,
                FileName = x.FileName,
                ContentType = x.ContentType,
                FileSizeBytes = x.FileSizeBytes,
                DisplayOrder = x.DisplayOrder,
                UploadedAt = x.UploadedAt
            })
            .ToListAsync(cancellationToken);

        return images;
    }

    public async Task<TrainerGalleryImageDto> UploadAsync(
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var profile = await GetProfileByUserIdAsync(userId, cancellationToken);

        var currentCount = await _dbContext.TrainerGalleryImages
            .CountAsync(x => x.TrainerProfileId == profile.Id, cancellationToken);

        if (currentCount >= 12)
        {
            throw new InvalidOperationException("Maximum 12 images allowed in gallery.");
        }

        var storagePath = await _storageService.SaveAsync(profile.Id, file, cancellationToken);

        var entity = new TrainerGalleryImage
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = profile.Id,
            FileName = Path.GetFileName(file.FileName),
            StoragePath = storagePath,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType,
            FileSizeBytes = file.Length,
            DisplayOrder = currentCount + 1,
            UploadedAt = DateTime.UtcNow
        };

        _dbContext.TrainerGalleryImages.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TrainerGalleryImageDto
        {
            Id = entity.Id,
            FileName = entity.FileName,
            ContentType = entity.ContentType,
            FileSizeBytes = entity.FileSizeBytes,
            DisplayOrder = entity.DisplayOrder,
            UploadedAt = entity.UploadedAt
        };
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var profile = await GetProfileByUserIdAsync(userId, cancellationToken);

        var image = await _dbContext.TrainerGalleryImages
            .FirstOrDefaultAsync(x => x.Id == imageId && x.TrainerProfileId == profile.Id, cancellationToken);

        if (image is null)
        {
            throw new KeyNotFoundException("Gallery image not found.");
        }

        await _storageService.DeleteAsync(image.StoragePath, cancellationToken);

        _dbContext.TrainerGalleryImages.Remove(image);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(string PhysicalPath, string ContentType)?> GetImageFileAsync(
        Guid userId,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var profile = await GetProfileByUserIdAsync(userId, cancellationToken);

        var image = await _dbContext.TrainerGalleryImages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == imageId && x.TrainerProfileId == profile.Id, cancellationToken);

        if (image is null)
        {
            return null;
        }

        var fullPath = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            image.StoragePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            return null;
        }

        return (fullPath, image.ContentType);
    }

    private async Task<TrainerProfile> GetProfileByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.TrainerProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (profile is null)
        {
            throw new KeyNotFoundException("Trainer profile not found.");
        }

        return profile;
    }
}
