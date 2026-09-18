using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopBookingConfiguration : IEntityTypeConfiguration<WorkshopBooking>
{
    public void Configure(EntityTypeBuilder<WorkshopBooking> builder)
    {
        builder.ToTable("workshop_bookings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Quantity)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(x => x.TotalPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.PriceBreakdownJson)
            .HasColumnType("text");

        builder.Property(x => x.GuestName)
            .HasMaxLength(200);

        builder.Property(x => x.GuestPhone)
            .HasMaxLength(30);

        builder.Property(x => x.GuestEmail)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.BookedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ReservationExpiresAt)
            .HasColumnType("timestamptz");

        builder.HasIndex(x => new { x.WorkshopId, x.StudentProfileId });
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        builder.HasOne(x => x.Workshop)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StudentProfile)
            .WithMany(x => x.WorkshopBookings)
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
