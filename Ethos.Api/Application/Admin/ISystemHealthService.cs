using System.Threading;
using System.Threading.Tasks;
using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Application.Admin;

public interface ISystemHealthService
{
    Task<SystemHealthResponse> GetUnifiedHealthAsync(CancellationToken cancellationToken = default);
}
