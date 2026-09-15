namespace Ethos.Api.Contracts.Admin;

public sealed class AdminPermissionOverrideRequest
{
    public string PermissionCode { get; set; } = null!;

    public bool? IsAllowed { get; set; }

    public bool ClearOverride { get; set; }

    public string Reason { get; set; } = string.Empty;
}
