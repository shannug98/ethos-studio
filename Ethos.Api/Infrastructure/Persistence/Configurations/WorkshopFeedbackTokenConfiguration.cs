using Ethos.Api.Domain.Entities;
using Ethos.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopFeedbackTokenConfiguration : IEntityTypeConfiguration<WorkshopFeedbackToken>
{
    public void Configure(EntityTypeBuilder<WorkshopFeedbackToken> builder)
    {
        builder.ToTable("workshop_feedback_tokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.AudienceType)
            .HasConversion<int>()
            .HasDefaultValue(FeedbackAudienceType.Attended)
            .IsRequired();

        builder.Property(x => x.FeedbackFormVersionId)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(x => x.ExpiresAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UsedAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(x => x.WorkshopBookingId);
        builder.HasIndex(x => x.FeedbackFormVersionId);

        builder.HasOne(x => x.WorkshopBooking)
            .WithMany()
            .HasForeignKey(x => x.WorkshopBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FeedbackFormVersion)
            .WithMany()
            .HasForeignKey(x => x.FeedbackFormVersionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
