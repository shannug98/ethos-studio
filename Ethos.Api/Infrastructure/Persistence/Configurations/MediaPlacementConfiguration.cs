using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class MediaPlacementConfiguration : IEntityTypeConfiguration<MediaPlacement>
{
    public void Configure(EntityTypeBuilder<MediaPlacement> builder)
    {
        builder.ToTable("media_placements");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Section)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(p => p.IsPublished)
            .HasDefaultValue(true);

        builder.Property(p => p.IsFeatured)
            .HasDefaultValue(false);

        // Composite unique index: one placement per (MediaItem, Section)
        builder.HasIndex(p => new { p.MediaItemId, p.Section })
            .IsUnique()
            .HasDatabaseName("ix_media_placements_item_section");

        builder.HasIndex(p => p.Section)
            .HasDatabaseName("ix_media_placements_section");

        builder.HasIndex(p => p.IsPublished)
            .HasDatabaseName("ix_media_placements_is_published");

        builder.HasOne(p => p.MediaItem)
            .WithMany(m => m.Placements)
            .HasForeignKey(p => p.MediaItemId)
            .OnDelete(DeleteBehavior.Cascade); // deleting a MediaItem cascades to its placements
    }
}
