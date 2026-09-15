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

        builder.Property(m => m.Section)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(m => m.Section);

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
    }
}
