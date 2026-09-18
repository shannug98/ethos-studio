using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppOutboxAndTicketPdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_pdfs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_pdfs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_pdfs_workshop_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "workshop_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationType = table.Column<int>(type: "integer", nullable: false),
                    RecipientPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderRequestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    LockedByWorkerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_notifications_workshop_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "workshop_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_whatsapp_notifications_workshop_tickets_WorkshopTicketId",
                        column: x => x.WorkshopTicketId,
                        principalTable: "workshop_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_pdfs_TicketId",
                table: "ticket_pdfs",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_notifications_BookingId",
                table: "whatsapp_notifications",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_notifications_IdempotencyKey",
                table: "whatsapp_notifications",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_notifications_Status_NextAttemptAt_LeaseExpiresAt",
                table: "whatsapp_notifications",
                columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_notifications_WorkshopTicketId",
                table: "whatsapp_notifications",
                column: "WorkshopTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_pdfs");

            migrationBuilder.DropTable(
                name: "whatsapp_notifications");
        }
    }
}
