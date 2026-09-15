using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Auth;

public class RequestOtpRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    public string? Purpose { get; set; }
}
