using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("security_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Severity)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.IpAddress)
            .HasMaxLength(100);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.MaskedPhone)
            .HasMaxLength(50);

        builder.Property(x => x.TraceId)
            .HasMaxLength(100);

        builder.Property(x => x.DetailsJson)
            .HasMaxLength(4000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AdminDevice)
            .WithMany()
            .HasForeignKey(x => x.AdminDeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AdminSession)
            .WithMany()
            .HasForeignKey(x => x.AdminSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.EventType);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TraceId);
    }
}
