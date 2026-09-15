using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerTierConfiguration : IEntityTypeConfiguration<TrainerTier>
{
    public void Configure(EntityTypeBuilder<TrainerTier> builder)
    {
        builder.ToTable("trainer_tiers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ApplicationFee)
            .HasPrecision(18, 2);

        builder.Property(x => x.UpgradeFee)
            .HasPrecision(18, 2);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasIndex(x => x.DisplayOrder)
            .IsUnique();
    }
}
