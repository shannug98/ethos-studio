using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopPassTypeConfiguration : IEntityTypeConfiguration<WorkshopPassType>
{
    public void Configure(EntityTypeBuilder<WorkshopPassType> builder)
    {
        builder.ToTable("workshop_pass_types");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.SessionsIncluded);

        builder.Property(x => x.TotalQuantity)
            .HasDefaultValue(1000)
            .IsRequired();

        builder.Property(x => x.SalesStartUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.SalesEndUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.WorkshopSessionId)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.HasIndex(x => x.WorkshopId);
        builder.HasIndex(x => x.WorkshopSessionId);

        builder.HasOne(x => x.Workshop)
            .WithMany(x => x.PassTypes)
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.WorkshopSession)
            .WithMany()
            .HasForeignKey(x => x.WorkshopSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.PricingTiers)
            .WithOne(x => x.WorkshopPassType)
            .HasForeignKey(x => x.WorkshopPassTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
