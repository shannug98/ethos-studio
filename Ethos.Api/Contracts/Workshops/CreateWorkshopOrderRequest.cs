namespace Ethos.Api.Contracts.Workshops;

public class CreateWorkshopOrderRequest
{
    public int Quantity { get; set; } = 1;

    // Optional for logged in student, required for guest
    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? IdempotencyKey { get; set; }
}
