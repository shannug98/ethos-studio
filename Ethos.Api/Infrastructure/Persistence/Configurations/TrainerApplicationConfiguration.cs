using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TrainerApplicationConfiguration : IEntityTypeConfiguration<TrainerApplication>
{
    public void Configure(EntityTypeBuilder<TrainerApplication> builder)
    {
        builder.ToTable("trainer_applications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ApplicationNotes)
            .HasMaxLength(3000);

        builder.Property(x => x.AdminNotes)
            .HasMaxLength(3000);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.TrainerProfileId);

        builder.HasIndex(x => x.PaymentTransactionId)
            .IsUnique()
            .HasFilter("\"PaymentTransactionId\" IS NOT NULL");

        builder.HasOne(x => x.TrainerProfile)
            .WithMany(x => x.Applications)
            .HasForeignKey(x => x.TrainerProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PaymentTransaction)
            .WithMany()
            .HasForeignKey(x => x.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
