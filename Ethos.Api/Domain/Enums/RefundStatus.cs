namespace Ethos.Api.Domain.Enums;

public enum RefundStatus
{
    Requested = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4,
    ReconciliationRequired = 5
}
