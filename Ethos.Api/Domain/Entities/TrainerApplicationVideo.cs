namespace Ethos.Api.Domain.Entities;

public class TrainerApplicationVideo
{
    public Guid Id { get; set; }

    public Guid TrainerApplicationId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int DurationSeconds { get; set; }

    public DateTime UploadedAt { get; set; }

    public TrainerApplication TrainerApplication { get; set; } = null!;
}
