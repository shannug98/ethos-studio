using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Domain.Exceptions;

public class ConflictException : BusinessRuleException
{
    public long? ServerVersion { get; }

    public ConflictException(string message, long? serverVersion = null)
        : base("DRAFT_CONFLICT", message, StatusCodes.Status409Conflict)
    {
        ServerVersion = serverVersion;
    }
}
