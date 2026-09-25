using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class PaymentRefundConfiguration : IEntityTypeConfiguration<PaymentRefund>
{
    public void Configure(EntityTypeBuilder<PaymentRefund> builder)
    {
        builder.ToTable("payment_refunds");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PaymentId)
            .HasColumnName("payment_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.BookingId)
            .HasColumnName("booking_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.AmountPaise)
            .HasColumnName("amount_paise")
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .HasDefaultValue("INR")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RazorpayRefundId)
            .HasColumnName("razorpay_refund_id")
            .HasMaxLength(100);

        builder.Property(x => x.RazorpayPaymentId)
            .HasColumnName("razorpay_payment_id")
            .HasMaxLength(100);

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.InitiatedByAdminId)
            .HasColumnName("initiated_by_admin_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.ProcessingStartedAtUtc)
            .HasColumnName("processing_started_at_utc")
            .HasColumnType("timestamptz");

        builder.Property(x => x.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamptz");

        builder.Property(x => x.LastAttemptAtUtc)
            .HasColumnName("last_attempt_at_utc")
            .HasColumnType("timestamptz");

        builder.Property(x => x.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(1000);

        // Filtered unique index: exactly one active/non-failed refund attempt per payment
        builder.HasIndex(x => x.PaymentId)
            .IsUnique()
            .HasFilter("\"status\" != 4") // 4 = RefundStatus.Failed
            .HasDatabaseName("ix_payment_refunds_payment_id_active_unique");

        builder.HasIndex(x => x.BookingId)
            .HasDatabaseName("ix_payment_refunds_booking_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_payment_refunds_status");

        // Relationships
        builder.HasOne(x => x.PaymentTransaction)
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Booking)
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
