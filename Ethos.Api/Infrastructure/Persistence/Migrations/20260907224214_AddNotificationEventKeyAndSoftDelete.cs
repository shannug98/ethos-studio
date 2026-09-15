using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationEventKeyAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionUrl",
                table: "notifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventKey",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "notification_recipients",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_EventKey",
                table: "notifications",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_recipients_UserId_DeletedAt",
                table: "notification_recipients",
                columns: new[] { "UserId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_EventKey",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notification_recipients_UserId_DeletedAt",
                table: "notification_recipients");

            migrationBuilder.DropColumn(
                name: "ActionUrl",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "EventKey",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "notification_recipients");
        }
    }
}
