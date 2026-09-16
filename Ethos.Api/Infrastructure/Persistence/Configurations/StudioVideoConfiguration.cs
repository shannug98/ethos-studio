using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class StudioVideoConfiguration : IEntityTypeConfiguration<StudioVideo>
{
    public void Configure(EntityTypeBuilder<StudioVideo> builder)
    {
        builder.ToTable("studio_videos");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(v => v.Description)
            .HasMaxLength(500);

        builder.Property(v => v.Section)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.ObjectKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.PublicUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(v => v.ThumbnailUrl)
            .HasMaxLength(1000);

        builder.Property(v => v.MimeType)
            .HasMaxLength(100)
            .HasDefaultValue("video/mp4");

        builder.Property(v => v.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(v => v.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(v => new { v.Section, v.IsActive, v.DisplayOrder });
        builder.HasIndex(v => v.ObjectKey);

        builder.HasOne(v => v.UploadedByUser)
            .WithMany()
            .HasForeignKey(v => v.UploadedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
