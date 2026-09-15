namespace Ethos.Api.Application.Trainers.DTOs;

public sealed class TrainerApplicationVideoDto
{
    public Guid Id { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }

    public int DurationSeconds { get; init; }

    public DateTime UploadedAt { get; init; }
}
