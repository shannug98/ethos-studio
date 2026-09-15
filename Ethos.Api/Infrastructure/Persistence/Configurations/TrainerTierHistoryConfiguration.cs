using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerTierHistoryConfiguration : IEntityTypeConfiguration<TrainerTierHistory>
{
    public void Configure(EntityTypeBuilder<TrainerTierHistory> builder)
    {
        builder.ToTable("trainer_tier_history");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.TrainerProfileId);

        builder.HasOne(x => x.TrainerProfile)
            .WithMany(x => x.TierHistory)
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PreviousTier)
            .WithMany()
            .HasForeignKey(x => x.PreviousTierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.NewTier)
            .WithMany()
            .HasForeignKey(x => x.NewTierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
