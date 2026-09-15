namespace Ethos.Api.Contracts.Packages;

public class PackageResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int DurationDays { get; set; }

    public int? ClassLimit { get; set; }

    public bool IsFeatured { get; set; }

    public string? FeaturesJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
