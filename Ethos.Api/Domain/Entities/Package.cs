namespace Ethos.Api.Domain.Entities;

public class Package
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int DurationDays { get; set; }

    public int? ClassLimit { get; set; }

    public bool IsActive { get; set; }

    public bool IsFeatured { get; set; }

    public string? FeaturesJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<StudentPackage> StudentPackages { get; set; }
        = new List<StudentPackage>();
}
