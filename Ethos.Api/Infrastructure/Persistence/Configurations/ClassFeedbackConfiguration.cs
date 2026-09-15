using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class ClassFeedbackConfiguration : IEntityTypeConfiguration<ClassFeedback>
{
    public void Configure(EntityTypeBuilder<ClassFeedback> builder)
    {
        builder.ToTable("class_feedback");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.OverallRating).IsRequired();
        builder.Property(x => x.TeachingQuality).IsRequired();
        builder.Property(x => x.ExplanationClarity).IsRequired();
        builder.Property(x => x.TrainerEngagement).IsRequired();
        builder.Property(x => x.ClassPace).IsRequired();
        builder.Property(x => x.ChoreographyContent).IsRequired();
        builder.Property(x => x.DifficultyLevel).IsRequired();
        builder.Property(x => x.ClassExperience).IsRequired();

        builder.Property(x => x.LikedAspects).HasMaxLength(2000);
        builder.Property(x => x.Improvements).HasMaxLength(2000);
        builder.Property(x => x.WouldAttendAgain).HasMaxLength(50).IsRequired();

        builder.Property(x => x.SubmittedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => x.ClassEnrollmentId).IsUnique();

        builder.HasOne(x => x.ClassEnrollment)
            .WithMany()
            .HasForeignKey(x => x.ClassEnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DanceClass)
            .WithMany()
            .HasForeignKey(x => x.DanceClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StudentProfile)
            .WithMany(x => x.ClassFeedbacks)
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
