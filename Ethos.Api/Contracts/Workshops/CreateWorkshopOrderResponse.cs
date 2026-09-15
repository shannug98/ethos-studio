namespace Ethos.Api.Contracts.Workshops;

public class CreateWorkshopOrderResponse
{
    public Guid BookingId { get; set; }

    public Guid TransactionId { get; set; }

    public Guid WorkshopId { get; set; }

    public string WorkshopTitle { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public string RazorpayOrderId { get; set; } = string.Empty;

    public string RazorpayKeyId { get; set; } = string.Empty;

    public bool IsStudentDiscountApplied { get; set; }

    public bool IsSplitTier { get; set; }

    public string SplitTierMessage { get; set; } = string.Empty;

    public List<WorkshopPriceQuoteItem> Breakdown { get; set; } = new();
}
