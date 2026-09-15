using System.Security.Cryptography;

namespace Ethos.Api.Application.Trainers;

public class TrainerCodeService : ITrainerCodeService
{
    public Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        var number = BitConverter.ToUInt32(bytes);
        var code = $"TRN{number % 1_000_000:000000}";
        return Task.FromResult(code);
    }
}
