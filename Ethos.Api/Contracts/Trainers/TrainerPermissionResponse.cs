namespace Ethos.Api.Contracts.Trainers;

public class TrainerPermissionResponse
{
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsAllowed { get; set; }
}
