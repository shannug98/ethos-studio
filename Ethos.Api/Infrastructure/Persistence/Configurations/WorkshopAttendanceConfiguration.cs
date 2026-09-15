using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopAttendanceConfiguration : IEntityTypeConfiguration<WorkshopAttendance>
{
    public void Configure(EntityTypeBuilder<WorkshopAttendance> builder)
    {
        builder.ToTable("workshop_attendances");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FirstCheckedInAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.LastCheckedInAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.IsCurrentlyInside)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.Method)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.DeviceIp)
            .HasMaxLength(50);

        builder.HasIndex(x => x.WorkshopTicketId).IsUnique();
        builder.HasIndex(x => new { x.WorkshopId, x.IsCurrentlyInside });

        builder.HasOne(x => x.WorkshopTicket)
            .WithOne(x => x.Attendance)
            .HasForeignKey<WorkshopAttendance>(x => x.WorkshopTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Workshop)
            .WithMany()
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CheckedInByUser)
            .WithMany()
            .HasForeignKey(x => x.CheckedInByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
