using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerUpgradeRequestConfiguration : IEntityTypeConfiguration<TrainerUpgradeRequest>
{
    public void Configure(EntityTypeBuilder<TrainerUpgradeRequest> builder)
    {
        builder.ToTable("trainer_upgrade_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason)
            .HasMaxLength(2000);

        builder.Property(x => x.AdminNotes)
            .HasMaxLength(2000);

        builder.HasIndex(x => new
        {
            x.TrainerProfileId,
            x.Status
        });

        builder.HasOne(x => x.TrainerProfile)
            .WithMany(x => x.UpgradeRequests)
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CurrentTier)
            .WithMany()
            .HasForeignKey(x => x.CurrentTierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedTier)
            .WithMany()
            .HasForeignKey(x => x.RequestedTierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
