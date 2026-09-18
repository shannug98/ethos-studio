using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WhatsAppNotificationConfiguration : IEntityTypeConfiguration<WhatsAppNotification>
{
    public void Configure(EntityTypeBuilder<WhatsAppNotification> builder)
    {
        builder.ToTable("whatsapp_notifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.BookingId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.WorkshopTicketId)
            .HasColumnType("uuid");

        builder.Property(x => x.NotificationType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RecipientPhone)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Attempts)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.ProviderMessageId)
            .HasMaxLength(128);

        builder.Property(x => x.ProviderRequestId)
            .HasMaxLength(128);

        builder.Property(x => x.LastError)
            .HasMaxLength(2000);

        builder.Property(x => x.LeaseExpiresAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.LockedByWorkerId)
            .HasMaxLength(64);

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.SentAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.NextAttemptAt)
            .HasColumnType("timestamptz");

        // Unique index on IdempotencyKey
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique();

        // Index for polling pending and lease recovery
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.LeaseExpiresAt });
        builder.HasIndex(x => x.BookingId);
        builder.HasIndex(x => x.WorkshopTicketId);

        builder.HasOne(x => x.WorkshopBooking)
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WorkshopTicket)
            .WithMany()
            .HasForeignKey(x => x.WorkshopTicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
