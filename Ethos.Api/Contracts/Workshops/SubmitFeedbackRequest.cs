using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Workshops;

public class SubmitFeedbackRequest
{
    [Required]
    public Guid WorkshopId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [Range(1, 5)]
    public int? TeachingRating { get; set; }

    [Range(1, 5)]
    public int? EnergyRating { get; set; }

    [Range(1, 5)]
    public int? ContentRating { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }

    public bool WouldRecommend { get; set; }
}
