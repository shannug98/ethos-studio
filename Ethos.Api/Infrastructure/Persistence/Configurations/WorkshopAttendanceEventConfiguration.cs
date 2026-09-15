using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopAttendanceEventConfiguration : IEntityTypeConfiguration<WorkshopAttendanceEvent>
{
    public void Configure(EntityTypeBuilder<WorkshopAttendanceEvent> builder)
    {
        builder.ToTable("workshop_attendance_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EventType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.Method)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasIndex(x => x.WorkshopTicketId);
        builder.HasIndex(x => new { x.WorkshopId, x.OccurredAt });

        builder.HasOne(x => x.WorkshopTicket)
            .WithMany()
            .HasForeignKey(x => x.WorkshopTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Workshop)
            .WithMany()
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PerformedByUser)
            .WithMany()
            .HasForeignKey(x => x.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
