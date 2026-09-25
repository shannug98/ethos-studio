using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketTypeVolumePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workshop_pricing_tiers_WorkshopId_TierNumber",
                table: "workshop_pricing_tiers");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkshopPassTypeId",
                table: "workshop_pricing_tiers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SalesStartUtc",
                table: "workshop_pass_types",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_workshop_pricing_tiers_pass_type",
                table: "workshop_pricing_tiers",
                columns: new[] { "WorkshopPassTypeId", "TierNumber" },
                unique: true,
                filter: "\"WorkshopPassTypeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_pricing_tiers_workshop_legacy",
                table: "workshop_pricing_tiers",
                columns: new[] { "WorkshopId", "TierNumber" },
                unique: true,
                filter: "\"WorkshopPassTypeId\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_workshop_pricing_tiers_workshop_pass_types_WorkshopPassType~",
                table: "workshop_pricing_tiers",
                column: "WorkshopPassTypeId",
                principalTable: "workshop_pass_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workshop_pricing_tiers_workshop_pass_types_WorkshopPassType~",
                table: "workshop_pricing_tiers");

            migrationBuilder.DropIndex(
                name: "IX_workshop_pricing_tiers_pass_type",
                table: "workshop_pricing_tiers");

            migrationBuilder.DropIndex(
                name: "IX_workshop_pricing_tiers_workshop_legacy",
                table: "workshop_pricing_tiers");

            migrationBuilder.DropColumn(
                name: "WorkshopPassTypeId",
                table: "workshop_pricing_tiers");

            migrationBuilder.DropColumn(
                name: "SalesStartUtc",
                table: "workshop_pass_types");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_pricing_tiers_WorkshopId_TierNumber",
                table: "workshop_pricing_tiers",
                columns: new[] { "WorkshopId", "TierNumber" },
                unique: true);
        }
    }
}
