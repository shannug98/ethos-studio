using Ethos.Api.Domain.Enums;

namespace Ethos.Api.Domain.Payment;

public static class PaymentStateMachine
{
    private static readonly Dictionary<PaymentStatus, HashSet<PaymentStatus>> LegalTransitions = new()
    {
        [PaymentStatus.Created] = new() { PaymentStatus.OrderCreated, PaymentStatus.Failed, PaymentStatus.Cancelled },
        [PaymentStatus.OrderCreated] = new() { PaymentStatus.PaymentPending, PaymentStatus.Paid, PaymentStatus.Failed, PaymentStatus.Cancelled },
        [PaymentStatus.PaymentPending] = new() { PaymentStatus.Paid, PaymentStatus.Failed, PaymentStatus.Cancelled },
        [PaymentStatus.Paid] = new() { PaymentStatus.Refunded, PaymentStatus.PartiallyRefunded },
        [PaymentStatus.PartiallyRefunded] = new() { PaymentStatus.Refunded, PaymentStatus.PartiallyRefunded },
        [PaymentStatus.Failed] = new(), // Terminal
        [PaymentStatus.Cancelled] = new(), // Terminal
        [PaymentStatus.Refunded] = new() // Terminal
    };

    public static bool CanTransition(PaymentStatus current, PaymentStatus target)
    {
        if (current == target) return true; // Idempotent
        return LegalTransitions.TryGetValue(current, out var targets) && targets.Contains(target);
    }

    public static void AssertTransition(PaymentStatus current, PaymentStatus target)
    {
        if (!CanTransition(current, target))
        {
            throw new InvalidOperationException($"Illegal payment status transition from '{current}' to '{target}'.");
        }
    }

    public static bool CanAdminResolve(PaymentStatus current, PaymentStatus target)
    {
        if (current == target) return true; // Idempotent

        // Governed Administrative Resolution:
        // An authorized admin with gateway payment ID or bank deduction proof can resolve
        // Failed, PaymentPending, OrderCreated, or Created states to Paid.
        if (target == PaymentStatus.Paid)
        {
            return current == PaymentStatus.Failed ||
                   current == PaymentStatus.PaymentPending ||
                   current == PaymentStatus.OrderCreated ||
                   current == PaymentStatus.Created;
        }

        return CanTransition(current, target);
    }

    public static void AssertAdminResolution(PaymentStatus current, PaymentStatus target)
    {
        if (!CanAdminResolve(current, target))
        {
            throw new InvalidOperationException($"Administrative resolution cannot transition payment from '{current}' to '{target}'.");
        }
    }
}