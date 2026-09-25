using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopSessionTrainerConfiguration : IEntityTypeConfiguration<WorkshopSessionTrainer>
{
    public void Configure(EntityTypeBuilder<WorkshopSessionTrainer> builder)
    {
        builder.ToTable("workshop_session_trainers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.AssignedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        // Unique constraint preventing duplicate trainer assignment to the same session
        builder.HasIndex(x => new { x.WorkshopSessionId, x.TrainerProfileId })
            .IsUnique();

        builder.HasIndex(x => x.TrainerProfileId);

        builder.HasOne(x => x.WorkshopSession)
            .WithMany(s => s.SessionTrainers)
            .HasForeignKey(x => x.WorkshopSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TrainerProfile)
            .WithMany()
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
