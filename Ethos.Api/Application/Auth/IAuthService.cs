using Ethos.Api.Contracts.Auth;

namespace Ethos.Api.Application.Auth;

public interface IAuthService
{
    Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

