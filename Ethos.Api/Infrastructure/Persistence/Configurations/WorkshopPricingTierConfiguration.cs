using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopPricingTierConfiguration : IEntityTypeConfiguration<WorkshopPricingTier>
{
    public void Configure(EntityTypeBuilder<WorkshopPricingTier> builder)
    {
        builder.ToTable("workshop_pricing_tiers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TierNumber)
            .IsRequired();

        builder.Property(x => x.TierName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MinTickets)
            .IsRequired();

        builder.Property(x => x.MaxTickets);

        builder.Property(x => x.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.WorkshopPassTypeId)
            .HasColumnType("uuid");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => new { x.WorkshopId, x.TierNumber })
            .HasDatabaseName("IX_workshop_pricing_tiers_workshop_legacy")
            .HasFilter("\"WorkshopPassTypeId\" IS NULL")
            .IsUnique();

        builder.HasIndex(x => new { x.WorkshopPassTypeId, x.TierNumber })
            .HasDatabaseName("IX_workshop_pricing_tiers_pass_type")
            .HasFilter("\"WorkshopPassTypeId\" IS NOT NULL")
            .IsUnique();

        builder.HasOne(x => x.Workshop)
            .WithMany(x => x.PricingTiers)
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WorkshopPassType)
            .WithMany(x => x.PricingTiers)
            .HasForeignKey(x => x.WorkshopPassTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
