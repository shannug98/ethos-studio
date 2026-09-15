using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class AdminDeviceConfiguration : IEntityTypeConfiguration<AdminDevice>
{
    public void Configure(EntityTypeBuilder<AdminDevice> builder)
    {
        builder.ToTable("admin_devices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceCredentialHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.DeviceName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.RegisteredAt)
            .IsRequired();

        builder.Property(x => x.LastSeenAt)
            .IsRequired();

        builder.Property(x => x.LastSeenIp)
            .HasMaxLength(100);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.FingerprintTelemetry)
            .HasMaxLength(2000);

        builder.HasOne(x => x.AdminUser)
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DeviceCredentialHash)
            .IsUnique();

        builder.HasIndex(x => new { x.Status, x.RevokedAt });
        builder.HasIndex(x => new { x.AdminUserId, x.Status });
    }
}
