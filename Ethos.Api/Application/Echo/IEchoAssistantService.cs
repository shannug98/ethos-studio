using Ethos.Api.Contracts.Echo;

namespace Ethos.Api.Application.Echo;

public interface IEchoAssistantService
{
    Task<EchoChatResponse> ProcessMessageAsync(EchoChatRequest request, CancellationToken cancellationToken = default);
}
