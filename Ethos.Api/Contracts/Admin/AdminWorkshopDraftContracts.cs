using System.ComponentModel.DataAnnotations;

namespace Ethos.Api.Contracts.Admin;

public class AdminSaveWorkshopDraftRequest
{
    public Guid? WorkshopId { get; set; }

    [Required]
    public string DraftJson { get; set; } = "{}";

    public long ExpectedVersion { get; set; } = 0;

    public bool IsEthosOriginal { get; set; }
}

public class AdminWorkshopDraftResponse
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public Guid? WorkshopId { get; set; }
    public string DraftJson { get; set; } = "{}";
    public long Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsEthosOriginal { get; set; }
}
