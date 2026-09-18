using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("media_items");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title)
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);

        builder.Property(m => m.Caption)
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        builder.Property(m => m.AltText)
            .HasMaxLength(300)
            .HasDefaultValue(string.Empty);

        builder.Property(m => m.FocalPoint)
            .HasMaxLength(50)
            .HasDefaultValue("center");

        builder.Property(m => m.Category)
            .HasMaxLength(100)
            .HasDefaultValue("General");

        builder.Property(m => m.LayoutType)
            .HasMaxLength(50)
            .HasDefaultValue("Square");

        builder.Property(m => m.IsArchived)
            .HasDefaultValue(false);

        builder.Property(m => m.TargetUrl)
            .HasMaxLength(500);

        builder.Property(m => m.OptimizedUrl)
            .HasMaxLength(1000);

        builder.Property(m => m.ThumbnailUrl)
            .HasMaxLength(1000);

        builder.Property(m => m.ObjectKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(m => m.ObjectKey)
            .IsUnique();

        builder.Property(m => m.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(m => m.MediaType)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(m => m.Category);
        builder.HasIndex(m => m.IsArchived);

        builder.Property(m => m.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.PublicUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(m => m.Visibility)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.ApprovalStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Checksum)
            .HasMaxLength(128);

        builder.HasOne(m => m.UploadedByUser)
            .WithMany()
            .HasForeignKey(m => m.UploadedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.Workshop)
            .WithMany()
            .HasForeignKey(m => m.WorkshopId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
