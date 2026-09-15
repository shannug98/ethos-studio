namespace Ethos.Api.Application.Trainers;

public interface ITrainerCodeService
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
