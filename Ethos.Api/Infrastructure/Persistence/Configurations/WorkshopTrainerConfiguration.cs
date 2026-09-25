using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopTrainerConfiguration : IEntityTypeConfiguration<WorkshopTrainer>
{
    public void Configure(EntityTypeBuilder<WorkshopTrainer> builder)
    {
        builder.ToTable("workshop_trainers");

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

        builder.HasIndex(x => new { x.WorkshopId, x.TrainerProfileId })
            .IsUnique();

        builder.HasOne(x => x.Workshop)
            .WithMany(x => x.WorkshopTrainers)
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TrainerProfile)
            .WithMany()
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
