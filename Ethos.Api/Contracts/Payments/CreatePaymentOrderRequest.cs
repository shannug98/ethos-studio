using System.ComponentModel.DataAnnotations;
using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Contracts.Payments;

public class CreatePaymentOrderRequest
{
    [Required]
    public PaymentPurpose Purpose { get; set; }

    [Required]
    public Guid ReferenceId { get; set; }
}
