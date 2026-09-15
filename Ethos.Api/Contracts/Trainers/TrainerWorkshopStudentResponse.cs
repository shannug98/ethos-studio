namespace Ethos.Api.Contracts.Trainers;

public class TrainerWorkshopStudentResponse
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentPhone { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime BookedAt { get; set; }
}
