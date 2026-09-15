namespace Ethos.Api.Contracts.Feedback;

public class LearningActivitySummaryResponse
{
    public int ClassesEnrolled { get; set; }

    public int ClassesCompleted { get; set; }

    public int WorkshopsBooked { get; set; }

    public int FeedbackSubmitted { get; set; }

    public int FeedbackPending { get; set; }
}
