using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopBookingSessionConfiguration : IEntityTypeConfiguration<WorkshopBookingSession>
{
    public void Configure(EntityTypeBuilder<WorkshopBookingSession> builder)
    {
        builder.ToTable("workshop_booking_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.OriginalSessionId);

        builder.Property(x => x.ReplacedAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.ReplacedByAdminId);

        builder.Property(x => x.CutoffOverrideUsed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.OverrideReason)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => new { x.WorkshopSessionId, x.Status });
        builder.HasIndex(x => x.WorkshopBookingId);
        builder.HasIndex(x => x.WorkshopTicketId);

        builder.HasOne(x => x.WorkshopBooking)
            .WithMany(x => x.BookingSessions)
            .HasForeignKey(x => x.WorkshopBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WorkshopSession)
            .WithMany(x => x.BookingSessions)
            .HasForeignKey(x => x.WorkshopSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WorkshopTicket)
            .WithOne(x => x.WorkshopBookingSession)
            .HasForeignKey<WorkshopBookingSession>(x => x.WorkshopTicketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
