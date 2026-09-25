using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopSessionConfiguration : IEntityTypeConfiguration<WorkshopSession>
{
    public void Configure(EntityTypeBuilder<WorkshopSession> builder)
    {
        builder.ToTable("workshop_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.SessionDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.StartTime)
            .IsRequired();

        builder.Property(x => x.EndTime)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Capacity)
            .HasDefaultValue(30)
            .IsRequired();

        builder.Property(x => x.PosterImageUrl)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(x => x.BookingCutoffTime);

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        // Non-unique indexes for fast schedule querying
        builder.HasIndex(x => new { x.WorkshopId, x.SessionDate });
        builder.HasIndex(x => x.TrainerProfileId);

        builder.HasOne(x => x.Workshop)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TrainerProfile)
            .WithMany()
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
