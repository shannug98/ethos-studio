using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class ClassEnrollmentConfiguration : IEntityTypeConfiguration<ClassEnrollment>
{
    public void Configure(EntityTypeBuilder<ClassEnrollment> builder)
    {
        builder.ToTable("class_enrollments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EnrollmentDate)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => new { x.StudentProfileId, x.DanceClassId }).IsUnique();

        builder.HasOne(x => x.StudentProfile)
            .WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DanceClass)
            .WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.DanceClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StudentPackage)
            .WithMany()
            .HasForeignKey(x => x.StudentPackageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
