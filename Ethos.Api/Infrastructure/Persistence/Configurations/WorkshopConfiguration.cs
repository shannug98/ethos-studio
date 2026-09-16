using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopConfiguration : IEntityTypeConfiguration<Workshop>
{
    public void Configure(EntityTypeBuilder<Workshop> builder)
    {
        builder.ToTable("workshops");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.DanceStyle)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Level)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.WorkshopDate)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.Venue)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.TrainerProposedPrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.AdminApprovedPrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.Capacity)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.LandscapeImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.City)
            .HasMaxLength(100);

        builder.Property(x => x.Area)
            .HasMaxLength(100);

        builder.Property(x => x.ShortDescription)
            .HasMaxLength(500);

        builder.Property(x => x.ContactPerson)
            .HasMaxLength(100);

        builder.Property(x => x.ContactNumber)
            .HasMaxLength(20);

        builder.Property(x => x.PublicVisibility)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.RegistrationType)
            .HasMaxLength(50)
            .HasDefaultValue("Standard")
            .IsRequired();

        builder.Property(x => x.TermsAndCancellationPolicy)
            .HasMaxLength(4000);

        builder.Property(x => x.GooglePlaceId)
            .HasMaxLength(200);

        builder.Property(x => x.VenueAddress)
            .HasMaxLength(500);

        builder.Property(x => x.Timezone)
            .HasMaxLength(100)
            .HasDefaultValue("Asia/Kolkata")
            .IsRequired();

        builder.Property(x => x.StartUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.EndUtc)
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne(x => x.TrainerProfile)
            .WithMany(x => x.Workshops)
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
