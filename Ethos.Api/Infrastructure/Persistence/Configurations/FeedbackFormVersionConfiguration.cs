using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class FeedbackFormVersionConfiguration : IEntityTypeConfiguration<FeedbackFormVersion>
{
    public void Configure(EntityTypeBuilder<FeedbackFormVersion> builder)
    {
        builder.ToTable("feedback_form_versions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.VersionNumber)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(x => x.IsFrozen)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.FrozenAtUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(x => new { x.WorkshopFeedbackSettingId, x.VersionNumber })
            .IsUnique();

        builder.HasOne(x => x.FeedbackSetting)
            .WithMany(x => x.FormVersions)
            .HasForeignKey(x => x.WorkshopFeedbackSettingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Questions)
            .WithOne(x => x.FormVersion)
            .HasForeignKey(x => x.FeedbackFormVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Feedbacks)
            .WithOne(x => x.FeedbackFormVersion)
            .HasForeignKey(x => x.FeedbackFormVersionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
