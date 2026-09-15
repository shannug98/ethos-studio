using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IncidentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.IncidentNumber)
            .IsUnique();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(x => x.Severity)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.AffectedService)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.TraceId)
            .HasMaxLength(100);

        builder.Property(x => x.EvidenceJson)
            .HasMaxLength(8000);

        builder.Property(x => x.RootCause)
            .HasMaxLength(4000);

        builder.Property(x => x.ResolutionNotes)
            .HasMaxLength(4000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasOne(x => x.AssignedAdmin)
            .WithMany()
            .HasForeignKey(x => x.AssignedAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.TraceId);
    }
}