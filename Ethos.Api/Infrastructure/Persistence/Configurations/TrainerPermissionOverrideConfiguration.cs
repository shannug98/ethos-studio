using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerPermissionOverrideConfiguration : IEntityTypeConfiguration<TrainerPermissionOverride>
{
    public void Configure(EntityTypeBuilder<TrainerPermissionOverride> builder)
    {
        builder.ToTable("trainer_permission_overrides");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.TrainerProfileId,
            x.PermissionId
        }).IsUnique();

        builder.Property(x => x.Reason)
            .HasMaxLength(1000);

        builder.HasOne(x => x.TrainerProfile)
            .WithMany(x => x.PermissionOverrides)
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany(x => x.Overrides)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
