using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopFeedbackAnswerConfiguration : IEntityTypeConfiguration<WorkshopFeedbackAnswer>
{
    public void Configure(EntityTypeBuilder<WorkshopFeedbackAnswer> builder)
    {
        builder.ToTable("workshop_feedback_answers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.NumericValue)
            .IsRequired(false);

        builder.Property(x => x.TextValue)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(x => new { x.WorkshopFeedbackId, x.FeedbackQuestionId })
            .IsUnique();

        builder.HasOne(x => x.WorkshopFeedback)
            .WithMany(x => x.Answers)
            .HasForeignKey(x => x.WorkshopFeedbackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FeedbackQuestion)
            .WithMany(x => x.Answers)
            .HasForeignKey(x => x.FeedbackQuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
