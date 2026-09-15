namespace Ethos.Api.Contracts.Trainers;

public class CreateTrainerApplicationRequest
{
    public Guid TierId { get; set; }

    public string? FullName { get; set; }

    public string? City { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public string? PrimaryDanceStyle { get; set; }

    public string? SecondaryDanceStyles { get; set; }

    public int? ExperienceYears { get; set; }

    public string? CurrentStudio { get; set; }

    public string? Bio { get; set; }

    public string? InstagramUrl { get; set; }

    public string? YouTubeUrl { get; set; }

    public string? ApplicationNotes { get; set; }
}
