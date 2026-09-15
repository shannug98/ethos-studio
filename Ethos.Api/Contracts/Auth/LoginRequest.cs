using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Auth;

public class LoginRequest
{
    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
