using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Payments;

public class VerifyPaymentRequest
{
    [Required]
    public Guid TransactionId { get; set; }

    [Required]
    public string RazorpayOrderId { get; set; } = string.Empty;

    [Required]
    public string RazorpayPaymentId { get; set; } = string.Empty;

    [Required]
    public string RazorpaySignature { get; set; } = string.Empty;
}
