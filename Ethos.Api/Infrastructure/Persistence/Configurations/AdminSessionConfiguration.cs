using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
{
    public void Configure(EntityTypeBuilder<AdminSession> builder)
    {
        builder.ToTable("admin_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionTokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.LastSeenAt)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.RevokedAt);

        builder.Property(x => x.LoggedOutAt);

        builder.HasOne(x => x.AdminDevice)
            .WithMany(d => d.Sessions)
            .HasForeignKey(x => x.AdminDeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AdminUser)
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SessionTokenHash)
            .IsUnique();

        builder.HasIndex(x => new { x.AdminDeviceId, x.IsActive });
        builder.HasIndex(x => new { x.AdminUserId, x.IsActive });
    }
}
