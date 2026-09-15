using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class AdminActionConfiguration : IEntityTypeConfiguration<AdminAction>
{
    public void Configure(EntityTypeBuilder<AdminAction> builder)
    {
        builder.ToTable("admin_actions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.OutcomeCode)
            .HasMaxLength(50);

        builder.Property(x => x.Reason)
            .HasMaxLength(1000);

        builder.Property(x => x.TraceId)
            .HasMaxLength(100);

        builder.Property(x => x.RequestId)
            .HasMaxLength(100);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(100);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.MetadataJson)
            .HasMaxLength(4000);

        builder.HasOne(x => x.AdminUser)
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AdminDevice)
            .WithMany()
            .HasForeignKey(x => x.AdminDeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AdminSession)
            .WithMany()
            .HasForeignKey(x => x.AdminSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.AdminUserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.TraceId);

        builder.HasIndex(x => new
        {
            x.EntityType,
            x.EntityId
        });
    }
}
