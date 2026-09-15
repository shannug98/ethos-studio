using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> builder)
    {
        builder.ToTable("student_profiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.City)
            .HasMaxLength(100);

        builder.Property(x => x.Gender)
            .HasMaxLength(50);

        builder.Property(x => x.ProfilePhotoUrl)
            .HasMaxLength(500);

        builder.Property(x => x.EmergencyContactName)
            .HasMaxLength(150);

        builder.Property(x => x.EmergencyContactPhone)
            .HasMaxLength(30);

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.HasOne(x => x.User)
            .WithOne(x => x.StudentProfile)
            .HasForeignKey<StudentProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
