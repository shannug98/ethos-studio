using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class WorkshopDraftConfiguration : IEntityTypeConfiguration<WorkshopDraft>
{
    public void Configure(EntityTypeBuilder<WorkshopDraft> builder)
    {
        builder.ToTable("workshop_drafts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AdminUserId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.WorkshopId)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(x => x.DraftJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        // PostgreSQL NULL-Safe Partial Unique Indexes:
        // 1. Exactly 1 draft per (AdminUserId, WorkshopId) for existing workshops
        builder.HasIndex(x => new { x.AdminUserId, x.WorkshopId })
            .IsUnique()
            .HasFilter(@"""WorkshopId"" IS NOT NULL");

        // 2. Exactly 1 draft per AdminUserId for new workshops (WorkshopId IS NULL)
        builder.HasIndex(x => x.AdminUserId)
            .IsUnique()
            .HasFilter(@"""WorkshopId"" IS NULL");

        builder.HasOne(x => x.AdminUser)
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Workshop)
            .WithMany()
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
