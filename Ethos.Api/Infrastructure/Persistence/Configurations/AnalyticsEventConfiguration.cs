using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class AnalyticsEventConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.ToTable("analytics_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EventType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.EventName)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.OccurredAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.VisitorId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.SessionId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.WorkshopId)
            .HasColumnType("uuid");

        builder.Property(x => x.Path)
            .HasMaxLength(500);

        builder.Property(x => x.Referrer)
            .HasMaxLength(500);

        builder.Property(x => x.MetadataJson)
            .HasColumnType("text");

        builder.Property(x => x.IpAddress)
            .HasMaxLength(100);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.UserId)
            .HasColumnType("uuid");

        // Relationships (Optional soft references with SetNull on delete)
        builder.HasOne(x => x.Workshop)
            .WithMany()
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Targeted compound & single-column indexes for fast query performance
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.EventType, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.VisitorId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.WorkshopId, x.OccurredAtUtc });
        builder.HasIndex(x => x.SessionId);
    }
}
