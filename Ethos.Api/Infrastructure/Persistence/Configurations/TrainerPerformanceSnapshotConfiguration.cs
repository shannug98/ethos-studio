using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerPerformanceSnapshotConfiguration : IEntityTypeConfiguration<TrainerPerformanceSnapshot>
{
    public void Configure(EntityTypeBuilder<TrainerPerformanceSnapshot> builder)
    {
        builder.ToTable("trainer_performance_snapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AttendancePercentage)
            .HasPrecision(5, 2);

        builder.Property(x => x.AverageFeedbackRating)
            .HasPrecision(4, 2);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.HasIndex(x => new
        {
            x.TrainerProfileId,
            x.SnapshotDate
        }).IsUnique();

        builder.HasOne(x => x.TrainerProfile)
            .WithMany(x => x.PerformanceSnapshots)
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
