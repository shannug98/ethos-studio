using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopTicketConfiguration : IEntityTypeConfiguration<WorkshopTicket>
{
    public void Configure(EntityTypeBuilder<WorkshopTicket> builder)
    {
        builder.ToTable("workshop_tickets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TicketNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.QrTokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.AttendeeName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AttendeePhone)
            .HasMaxLength(30);

        builder.Property(x => x.AttendeeEmail)
            .HasMaxLength(200);

        builder.Property(x => x.IsPrimaryAttendee)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IssuedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.CheckedInAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.CheckInMethod)
            .HasConversion<int>();

        builder.Property(x => x.AttendeeDetailsLockedAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.LastResentAt)
            .HasColumnType("timestamptz");

        builder.HasIndex(x => x.TicketNumber).IsUnique();
        builder.HasIndex(x => x.QrTokenHash).IsUnique();

        builder.HasIndex(x => x.WorkshopBookingId);
        builder.HasIndex(x => new { x.WorkshopId, x.Status });
        builder.HasIndex(x => x.PaymentTransactionId);

        builder.HasOne(x => x.WorkshopBooking)
            .WithMany(x => x.Tickets)
            .HasForeignKey(x => x.WorkshopBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Workshop)
            .WithMany(x => x.Tickets)
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaymentTransaction)
            .WithMany()
            .HasForeignKey(x => x.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
