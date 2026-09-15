using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Payments;

public class CreatePublicPackageOrderRequest
{
    [Required]
    public Guid PackageId { get; set; }

    [Required]
    [RegularExpression(
        @"^\d{10}$",
        ErrorMessage = "Phone number must contain exactly 10 digits.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    public string? FullName { get; set; }

    [EmailAddress]
    [StringLength(200)]
    public string? Email { get; set; }
}

public class VerifyPublicPackagePaymentRequest
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
