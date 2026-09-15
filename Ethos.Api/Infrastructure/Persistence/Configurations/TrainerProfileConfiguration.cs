using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerProfileConfiguration : IEntityTypeConfiguration<TrainerProfile>
{
    public void Configure(EntityTypeBuilder<TrainerProfile> builder)
    {
        builder.ToTable("trainer_profiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TrainerCode)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => x.TrainerCode)
            .IsUnique();

        builder.Property(x => x.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.City)
            .HasMaxLength(100);

        builder.Property(x => x.PrimaryDanceStyle)
            .HasMaxLength(100);

        builder.Property(x => x.SecondaryDanceStyles)
            .HasMaxLength(500);

        builder.Property(x => x.CurrentStudio)
            .HasMaxLength(150);

        builder.Property(x => x.Bio)
            .HasMaxLength(2000);

        builder.Property(x => x.InstagramUrl)
            .HasMaxLength(500);

        builder.Property(x => x.YouTubeUrl)
            .HasMaxLength(500);

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.HasOne(x => x.User)
            .WithOne(u => u.TrainerProfile)
            .HasForeignKey<TrainerProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentTier)
            .WithMany(x => x.Trainers)
            .HasForeignKey(x => x.CurrentTierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
