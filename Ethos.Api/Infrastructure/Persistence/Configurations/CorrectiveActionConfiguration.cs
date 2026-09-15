using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class CorrectiveActionConfiguration : IEntityTypeConfiguration<CorrectiveAction>
{
    public void Configure(EntityTypeBuilder<CorrectiveAction> builder)
    {
        builder.ToTable("corrective_actions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.ActionNumber)
            .IsUnique();

        builder.Property(x => x.ActionType)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.TargetEntityType)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.TargetEntityId)
            .IsRequired();

        builder.Property(x => x.TraceId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(128);

        builder.Property(x => x.PreconditionHash)
            .HasMaxLength(128);

        builder.Property(x => x.ParametersJson)
            .HasMaxLength(8000);

        builder.Property(x => x.BeforeStateJson)
            .HasMaxLength(8000);

        builder.Property(x => x.AfterStateJson)
            .HasMaxLength(8000);

        builder.Property(x => x.Justification)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.ExecutionLog)
            .HasMaxLength(8000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Incident)
            .WithMany()
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AdminUser)
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ActionType);
        builder.HasIndex(x => x.TargetEntityId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.IdempotencyKey);
    }
}
