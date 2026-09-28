using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopFeedbackSettingConfiguration : IEntityTypeConfiguration<WorkshopFeedbackSetting>
{
    public void Configure(EntityTypeBuilder<WorkshopFeedbackSetting> builder)
    {
        builder.ToTable("workshop_feedback_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IsFeedbackEnabled)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.ConfigCutoffUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.IsLocked)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.LockedAtUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamptz");

        builder.HasIndex(x => x.WorkshopId)
            .IsUnique();

        builder.HasOne(x => x.Workshop)
            .WithOne(x => x.FeedbackSetting)
            .HasForeignKey<WorkshopFeedbackSetting>(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ActiveVersion)
            .WithMany()
            .HasForeignKey(x => x.ActiveVersionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.FormVersions)
            .WithOne(x => x.FeedbackSetting)
            .HasForeignKey(x => x.WorkshopFeedbackSettingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
