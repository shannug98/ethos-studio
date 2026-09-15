using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerTierPermissionConfiguration : IEntityTypeConfiguration<TrainerTierPermission>
{
    public void Configure(EntityTypeBuilder<TrainerTierPermission> builder)
    {
        builder.ToTable("trainer_tier_permissions");

        builder.HasKey(x => new
        {
            x.TrainerTierId,
            x.PermissionId
        });

        builder.HasOne(x => x.TrainerTier)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.TrainerTierId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany(x => x.TierPermissions)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
