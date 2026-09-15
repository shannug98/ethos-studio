using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerAvailabilityConfiguration : IEntityTypeConfiguration<TrainerAvailability>
{
    public void Configure(EntityTypeBuilder<TrainerAvailability> builder)
    {
        builder.ToTable("trainer_availability");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.TrainerProfileId,
            x.DayOfWeek,
            x.StartTime,
            x.EndTime
        }).IsUnique();

        builder.HasOne(x => x.TrainerProfile)
            .WithMany(x => x.Availability)
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
