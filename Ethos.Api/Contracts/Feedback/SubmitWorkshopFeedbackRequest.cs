using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Feedback;

public class SubmitWorkshopFeedbackRequest
{
    [Range(1, 5)]
    public int OverallRating { get; set; }

    [Range(1, 5)]
    public int TeachingQuality { get; set; }

    [Range(1, 5)]
    public int ExplanationClarity { get; set; }

    [Range(1, 5)]
    public int DemonstrationRating { get; set; }

    [Range(1, 5)]
    public int InteractionRating { get; set; }

    [Range(1, 5)]
    public int ChoreographyContent { get; set; }

    [Range(1, 5)]
    public int DurationRating { get; set; }

    [Range(1, 5)]
    public int OrganizationRating { get; set; }

    [Range(1, 5)]
    public int VenueRating { get; set; }

    [Range(1, 5)]
    public int ValueForMoney { get; set; }

    public bool WouldRecommend { get; set; }

    public bool WouldAttendTrainerAgain { get; set; }

    [StringLength(2000)]
    public string? WhatEnjoyed { get; set; }

    [StringLength(2000)]
    public string? Improvements { get; set; }
}
