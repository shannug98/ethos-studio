namespace Ethos.Api.Contracts.Trainers;

public class UpdateTrainerRequest
{
    public string FullName { get; set; } = null!;

    public string? City { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? PrimaryDanceStyle { get; set; }

    public string? SecondaryDanceStyles { get; set; }

    public int? ExperienceYears { get; set; }

    public string? CurrentStudio { get; set; }

    public string? Bio { get; set; }

    public string? InstagramUrl { get; set; }

    public string? YouTubeUrl { get; set; }
}
