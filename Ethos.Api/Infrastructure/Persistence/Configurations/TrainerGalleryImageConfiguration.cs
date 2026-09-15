using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerGalleryImageConfiguration : IEntityTypeConfiguration<TrainerGalleryImage>
{
    public void Configure(EntityTypeBuilder<TrainerGalleryImage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.StoragePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.TrainerProfileId,
            x.DisplayOrder
        });

        builder.HasOne(x => x.TrainerProfile)
            .WithMany()
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
