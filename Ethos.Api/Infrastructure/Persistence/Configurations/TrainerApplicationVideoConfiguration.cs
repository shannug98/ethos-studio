using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public sealed class TrainerApplicationVideoConfiguration
    : IEntityTypeConfiguration<TrainerApplicationVideo>
{
    public void Configure(
        EntityTypeBuilder<TrainerApplicationVideo> builder)
    {
        builder.ToTable("trainer_application_videos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.StoragePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FileSizeBytes)
            .IsRequired();

        builder.Property(x => x.DurationSeconds)
            .IsRequired();

        builder.Property(x => x.UploadedAt)
            .IsRequired();

        builder.HasIndex(x => x.TrainerApplicationId)
            .IsUnique();

        builder.HasOne(x => x.TrainerApplication)
            .WithOne(x => x.VideoIntroduction)
            .HasForeignKey<TrainerApplicationVideo>(
                x => x.TrainerApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
