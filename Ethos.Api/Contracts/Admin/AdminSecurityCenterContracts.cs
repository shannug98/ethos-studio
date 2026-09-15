using Ethos.Api.Contracts.Admin;

namespace Ethos.Api.Contracts.Admin;

public class SecurityFleetResponse
{
    public int MaxAllowedSlots { get; set; } = 2;
    public int ActiveSlotCount { get; set; }
    public List<AdminDeviceResponse> ActiveDevices { get; set; } = new();
    public List<AdminSessionResponse> ActiveSessions { get; set; } = new();
    public List<AdminDeviceResponse> RevokedDevices { get; set; } = new();
}

public class SecurityThreatsSummaryResponse
{
    public List<FailedLoginClusterDto> FailedLoginClusters { get; set; } = new();
    public List<AuthorizationDenialClusterDto> AuthorizationDenialClusters { get; set; } = new();
    public List<DeviceViolationClusterDto> DeviceViolationClusters { get; set; } = new();
    public int TotalSecurityEventsLast24h { get; set; }
    public int CriticalThreatCount { get; set; }
}

public class FailedLoginClusterDto
{
    public string IpAddress { get; set; } = null!;
    public List<string> TargetedPhones { get; set; } = new();
    public int AttemptCount { get; set; }
    public DateTime FirstAttempt { get; set; }
    public DateTime LastAttempt { get; set; }
    public string Severity { get; set; } = "WARNING";
}

public class AuthorizationDenialClusterDto
{
    public Guid? UserId { get; set; }
    public string? AdminCustomerCode { get; set; }
    public string AttemptedPermission { get; set; } = null!;
    public int DenialCount { get; set; }
    public DateTime LastAttempt { get; set; }
}

public class DeviceViolationClusterDto
{
    public string IpAddress { get; set; } = null!;
    public Guid? DeviceId { get; set; }
    public string ViolationType { get; set; } = null!;
    public int AttemptCount { get; set; }
    public DateTime LastAttempt { get; set; }
}

public class SecurityInvestigationResponse
{
    public string TargetType { get; set; } = null!; // IP, USER, DEVICE, TRACE
    public string TargetValue { get; set; } = null!;
    public int RiskScore { get; set; } // 0 - 100
    public string RiskLevel { get; set; } = "LOW"; // LOW, MEDIUM, HIGH, CRITICAL
    public string ScoringPolicy { get; set; } = null!;
    public List<RiskFactorDto> ContributingFactors { get; set; } = new();
    public List<AdminSecurityEventResponse> EventHistory { get; set; } = new();
}

public class RiskFactorDto
{
    public string FactorName { get; set; } = null!;
    public int ScoreImpact { get; set; }
    public string Evidence { get; set; } = null!;
}
public class RevokeSecurityTargetRequest
{
    public string? Reason { get; set; }
}