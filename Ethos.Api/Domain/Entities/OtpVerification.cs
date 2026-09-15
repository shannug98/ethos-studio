using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Entities;

public class OtpVerification
{
    public Guid Id { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string OtpHash { get; set; } = string.Empty;

    public OtpPurpose Purpose { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
