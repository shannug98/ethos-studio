using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopFeedbackConfiguration : IEntityTypeConfiguration<WorkshopFeedback>
{
    public void Configure(EntityTypeBuilder<WorkshopFeedback> builder)
    {
        builder.ToTable("workshop_feedback");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Rating).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(2000);
        builder.Property(x => x.Improvements).HasMaxLength(2000);
        builder.Property(x => x.WouldRecommend).IsRequired();

        builder.Property(x => x.SubmittedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.IsValid)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.InvalidationReason)
            .HasMaxLength(500);

        builder.Property(x => x.InvalidatedAt)
            .HasColumnType("timestamptz");

        builder.HasIndex(x => x.IsValid);

        builder.Property(x => x.StudentProfileId)
            .IsRequired(false);

        builder.HasIndex(x => x.WorkshopBookingId)
            .IsUnique()
            .HasFilter("\"WorkshopBookingId\" IS NOT NULL");

        builder.HasOne(x => x.Workshop)
            .WithMany(x => x.Feedbacks)
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StudentProfile)
            .WithMany(x => x.WorkshopFeedbacks)
            .HasForeignKey(x => x.StudentProfileId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.WorkshopBooking)
            .WithMany()
            .HasForeignKey(x => x.WorkshopBookingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
