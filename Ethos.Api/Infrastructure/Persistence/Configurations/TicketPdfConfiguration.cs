using Ethos.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ethos.Api.Infrastructure.Persistence.Configurations;

public class TicketPdfConfiguration : IEntityTypeConfiguration<TicketPdf>
{
    public void Configure(EntityTypeBuilder<TicketPdf> builder)
    {
        builder.ToTable("ticket_pdfs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TicketId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(x => x.StorageKey)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.FileHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.FileSizeBytes)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(x => x.TicketId)
            .IsUnique();

        builder.HasOne(x => x.WorkshopTicket)
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
