using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkshopLocationUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workshop_drafts_AdminUserId",
                table: "workshop_drafts");

            migrationBuilder.DropIndex(
                name: "IX_workshop_drafts_AdminUserId_WorkshopId",
                table: "workshop_drafts");

            migrationBuilder.AddColumn<string>(
                name: "LocationUrl",
                table: "workshops",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_workshop_drafts_AdminUserId",
                table: "workshop_drafts",
                column: "AdminUserId",
                unique: true,
                filter: "\"WorkshopId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_drafts_AdminUserId_WorkshopId",
                table: "workshop_drafts",
                columns: new[] { "AdminUserId", "WorkshopId" },
                unique: true,
                filter: "\"WorkshopId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workshop_drafts_AdminUserId",
                table: "workshop_drafts");

            migrationBuilder.DropIndex(
                name: "IX_workshop_drafts_AdminUserId_WorkshopId",
                table: "workshop_drafts");

            migrationBuilder.DropColumn(
                name: "LocationUrl",
                table: "workshops");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_drafts_AdminUserId",
                table: "workshop_drafts",
                column: "AdminUserId",
                unique: true,
                filter: "workshop_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_drafts_AdminUserId_WorkshopId",
                table: "workshop_drafts",
                columns: new[] { "AdminUserId", "WorkshopId" },
                unique: true,
                filter: "workshop_id IS NOT NULL");
        }
    }
}
