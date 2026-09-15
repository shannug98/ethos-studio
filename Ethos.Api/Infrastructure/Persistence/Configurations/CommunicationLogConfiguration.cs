using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class CommunicationLogConfiguration : IEntityTypeConfiguration<CommunicationLog>
{
    public void Configure(EntityTypeBuilder<CommunicationLog> builder)
    {
        builder.ToTable("communication_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MessageReference)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.MessageReference)
            .IsUnique();

        builder.Property(x => x.Channel)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Recipient)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TemplateId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasMaxLength(200);

        builder.Property(x => x.BodyPreview)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ProviderMessageId)
            .HasMaxLength(128);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(128);

        builder.Property(x => x.Justification)
            .HasMaxLength(1000);

        builder.Property(x => x.TraceId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Channel);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.TemplateId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.IdempotencyKey);
    }
}
