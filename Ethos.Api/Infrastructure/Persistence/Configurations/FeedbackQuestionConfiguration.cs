using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class FeedbackQuestionConfiguration : IEntityTypeConfiguration<FeedbackQuestion>
{
    public void Configure(EntityTypeBuilder<FeedbackQuestion> builder)
    {
        builder.ToTable("feedback_questions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.QuestionKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PromptText)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.QuestionType)
            .IsRequired();

        builder.Property(x => x.TargetAudience)
            .IsRequired();

        builder.Property(x => x.OptionsJson)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(x => x.IsRequired)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.SortOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(x => new { x.FeedbackFormVersionId, x.SortOrder });

        builder.HasOne(x => x.FormVersion)
            .WithMany(x => x.Questions)
            .HasForeignKey(x => x.FeedbackFormVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Answers)
            .WithOne(x => x.FeedbackQuestion)
            .HasForeignKey(x => x.FeedbackQuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
