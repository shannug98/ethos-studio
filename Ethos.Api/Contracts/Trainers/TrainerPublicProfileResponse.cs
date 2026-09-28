namespace Ethos.Api.Contracts.Trainers;

public class TrainerPublicProfileResponse
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? City { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? PrimaryDanceStyle { get; set; }

    public string? SecondaryDanceStyles { get; set; }

    public IReadOnlyList<string> DanceStyles { get; set; } = Array.Empty<string>();

    public int? ExperienceYears { get; set; }

    public string? CurrentStudio { get; set; }

    public string? Bio { get; set; }

    public string? InstagramUrl { get; set; }

    public string? YouTubeUrl { get; set; }

    public IReadOnlyList<TrainerPublicWorkshopCardDto> RecentWorkshops { get; set; } = Array.Empty<TrainerPublicWorkshopCardDto>();

    public IReadOnlyList<TrainerPublicWorkshopCardDto> UpcomingWorkshops { get; set; } = Array.Empty<TrainerPublicWorkshopCardDto>();
}

public class TrainerPublicWorkshopCardDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? PosterUrl { get; set; }

    public string? DanceStyle { get; set; }

    public DateTime? WorkshopDate { get; set; }

    public string? City { get; set; }

    public bool IsCompleted { get; set; }
}
