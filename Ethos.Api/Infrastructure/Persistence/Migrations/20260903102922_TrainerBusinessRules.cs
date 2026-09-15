using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrainerBusinessRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trainer_profiles_users_UserId1",
                table: "trainer_profiles");

            migrationBuilder.DropIndex(
                name: "IX_trainer_profiles_UserId1",
                table: "trainer_profiles");

            migrationBuilder.DropColumn(
                name: "TrainerType",
                table: "trainer_profiles");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "trainer_profiles");

            migrationBuilder.RenameColumn(
                name: "PassPrice",
                table: "trainer_tiers",
                newName: "UpgradeFee");

            migrationBuilder.AddColumn<decimal>(
                name: "AdminApprovedPrice",
                table: "workshops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceApprovedAt",
                table: "workshops",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceApprovedByUserId",
                table: "workshops",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TrainerProposedPrice",
                table: "workshops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApplicationFee",
                table: "trainer_tiers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrainerProfileId1",
                table: "trainer_permission_overrides",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_permission_overrides_TrainerProfileId1",
                table: "trainer_permission_overrides",
                column: "TrainerProfileId1");

            migrationBuilder.AddForeignKey(
                name: "FK_trainer_permission_overrides_trainer_profiles_TrainerProfi~1",
                table: "trainer_permission_overrides",
                column: "TrainerProfileId1",
                principalTable: "trainer_profiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trainer_permission_overrides_trainer_profiles_TrainerProfi~1",
                table: "trainer_permission_overrides");

            migrationBuilder.DropIndex(
                name: "IX_trainer_permission_overrides_TrainerProfileId1",
                table: "trainer_permission_overrides");

            migrationBuilder.DropColumn(
                name: "AdminApprovedPrice",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "PriceApprovedAt",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "PriceApprovedByUserId",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "TrainerProposedPrice",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "ApplicationFee",
                table: "trainer_tiers");

            migrationBuilder.DropColumn(
                name: "TrainerProfileId1",
                table: "trainer_permission_overrides");

            migrationBuilder.RenameColumn(
                name: "UpgradeFee",
                table: "trainer_tiers",
                newName: "PassPrice");

            migrationBuilder.AddColumn<int>(
                name: "TrainerType",
                table: "trainer_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "trainer_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_profiles_UserId1",
                table: "trainer_profiles",
                column: "UserId1",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_trainer_profiles_users_UserId1",
                table: "trainer_profiles",
                column: "UserId1",
                principalTable: "users",
                principalColumn: "Id");
        }
    }
}
