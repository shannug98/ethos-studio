using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class RefundJobConfiguration : IEntityTypeConfiguration<RefundJob>
{
    public void Configure(EntityTypeBuilder<RefundJob> builder)
    {
        builder.ToTable("refund_jobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.WorkshopId)
            .HasColumnName("workshop_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.BookingId)
            .HasColumnName("booking_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.PaymentTransactionId)
            .HasColumnName("payment_transaction_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.PaymentRefundId)
            .HasColumnName("payment_refund_id")
            .HasColumnType("uuid");

        builder.Property(x => x.AmountPaise)
            .HasColumnName("amount_paise")
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.InitiatedByAdminId)
            .HasColumnName("initiated_by_admin_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.NextRetryUtc)
            .HasColumnName("next_retry_utc")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamptz");

        builder.Property(x => x.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.WorkshopId, x.Status })
            .HasDatabaseName("ix_refund_jobs_workshop_id_status");

        builder.HasIndex(x => x.BookingId)
            .HasDatabaseName("ix_refund_jobs_booking_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_refund_jobs_status");

        // Relationships
        builder.HasOne(x => x.Workshop)
            .WithMany()
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Booking)
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaymentTransaction)
            .WithMany()
            .HasForeignKey(x => x.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaymentRefund)
            .WithMany()
            .HasForeignKey(x => x.PaymentRefundId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
