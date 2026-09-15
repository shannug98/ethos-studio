using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Classes;

public class EnrollInClassRequest
{
    [Required]
    public Guid DanceClassId { get; set; }
}
