using Ethos.Api.Application.Trainers.DTOs;
using Ethos.Api.Contracts.Trainers;
using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Application.Trainers;

public interface ITrainerApplicationService
{
    Task<TrainerApplicationResponse> CreateAsync(
        Guid userId,
        CreateTrainerApplicationRequest request,
        CancellationToken cancellationToken);

    Task<TrainerApplicationResponse?> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<TrainerApplicationResponse?> UpdateAsync(
        Guid userId,
        UpdateTrainerApplicationRequest request,
        CancellationToken cancellationToken);

    Task SubmitAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<TrainerApplicationVideoDto> UploadVideoAsync(
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken);

    Task DeleteVideoAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<TrainerApplicationVideoDto?> GetVideoAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<(string PhysicalPath, string ContentType)?> GetVideoStreamAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
