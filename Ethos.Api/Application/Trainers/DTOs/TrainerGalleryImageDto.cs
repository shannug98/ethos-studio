namespace Ethos.Api.Application.Trainers.DTOs;

public class TrainerGalleryImageDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime UploadedAt { get; set; }
}
