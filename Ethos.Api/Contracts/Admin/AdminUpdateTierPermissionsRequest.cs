namespace Ethos.Api.Contracts.Admin;

public class AdminUpdateTierPermissionsRequest
{
    public List<PermissionAssignment> Permissions { get; set; } = [];
}

public class PermissionAssignment
{
    public string PermissionCode { get; set; } = null!;

    public bool IsAllowed { get; set; }
}
