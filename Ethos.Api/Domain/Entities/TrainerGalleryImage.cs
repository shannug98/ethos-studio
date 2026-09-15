namespace Ethos.Api.Domain.Entities;

public class TrainerGalleryImage
{
    public Guid Id { get; set; }

    public Guid TrainerProfileId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime UploadedAt { get; set; }

    public TrainerProfile TrainerProfile { get; set; } = null!;
}
