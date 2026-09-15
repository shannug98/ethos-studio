using Ethos.Api.Contracts.Trainers;
using Ethos.Api.Domain.Entities;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ethos.Api.Application.Trainers;

public class TrainerAvailabilityService : ITrainerAvailabilityService
{
    private readonly AppDbContext _db;

    public TrainerAvailabilityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TrainerAvailabilityResponse>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _db.TrainerAvailabilities
            .Where(x => x.TrainerProfile.UserId == userId)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .Select(x => new TrainerAvailabilityResponse
            {
                Id = x.Id,
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                IsAvailable = x.IsAvailable
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrainerAvailabilityResponse>> ReplaceMineAsync(
        Guid userId,
        List<TrainerAvailabilityRequest> requests,
        CancellationToken cancellationToken)
    {
        var trainer = await _db.TrainerProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (trainer == null)
        {
            throw new InvalidOperationException("Trainer profile not found.");
        }

        foreach (var req in requests)
        {
            if (req.StartTime >= req.EndTime)
            {
                throw new ArgumentException($"Start time {req.StartTime} must be earlier than end time {req.EndTime}.");
            }
        }

        var existing = await _db.TrainerAvailabilities
            .Where(x => x.TrainerProfileId == trainer.Id)
            .ToListAsync(cancellationToken);

        _db.TrainerAvailabilities.RemoveRange(existing);

        var newAvailabilities = requests.Select(req => new TrainerAvailability
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainer.Id,
            DayOfWeek = req.DayOfWeek,
            StartTime = req.StartTime,
            EndTime = req.EndTime,
            IsAvailable = req.IsAvailable
        }).ToList();

        _db.TrainerAvailabilities.AddRange(newAvailabilities);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetMineAsync(userId, cancellationToken);
    }
}
