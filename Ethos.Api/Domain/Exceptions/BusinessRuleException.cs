using Microsoft.AspNetCore.Http;

namespace Ethos.Api.Domain.Exceptions;

public class BusinessRuleException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public BusinessRuleException(
        string code,
        string message,
        int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}
