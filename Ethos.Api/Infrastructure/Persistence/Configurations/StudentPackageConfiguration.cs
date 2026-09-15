using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class StudentPackageConfiguration : IEntityTypeConfiguration<StudentPackage>
{
    public void Configure(EntityTypeBuilder<StudentPackage> builder)
    {
        builder.ToTable("student_packages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.StartDate)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.ExpiryDate)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.ClassesUsed)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => x.StudentProfileId);

        builder.HasIndex(x => x.PackageId);

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.PaymentTransactionId)
            .IsUnique();

        builder.HasOne(x => x.StudentProfile)
            .WithMany(x => x.StudentPackages)
            .HasForeignKey(x => x.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Package)
            .WithMany(x => x.StudentPackages)
            .HasForeignKey(x => x.PackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
