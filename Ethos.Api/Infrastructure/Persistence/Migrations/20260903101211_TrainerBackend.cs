using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrainerBackend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trainer_profiles_users_UserId",
                table: "trainer_profiles");

            migrationBuilder.DropIndex(
                name: "IX_trainer_applications_TrainerProfileId_Status",
                table: "trainer_applications");

            migrationBuilder.AlterColumn<string>(
                name: "ProfilePhotoUrl",
                table: "trainer_profiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrentStudio",
                table: "trainer_profiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Bio",
                table: "trainer_profiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentTierId",
                table: "trainer_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "trainer_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "trainer_documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "trainer_documents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ApplicationNotes",
                table: "trainer_applications",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "trainer_applications",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTransactionId",
                table: "trainer_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentVerifiedAt",
                table: "trainer_applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "admin_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trainer_availability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_availability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trainer_availability_trainer_profiles_TrainerProfileId",
                        column: x => x.TrainerProfileId,
                        principalTable: "trainer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trainer_performance_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionsConducted = table.Column<int>(type: "integer", nullable: false),
                    UniqueStudents = table.Column<int>(type: "integer", nullable: false),
                    AttendanceCount = table.Column<int>(type: "integer", nullable: false),
                    AttendancePercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FeedbackCount = table.Column<int>(type: "integer", nullable: false),
                    AverageFeedbackRating = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    WorkshopsConducted = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_performance_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trainer_performance_snapshots_trainer_profiles_TrainerProfi~",
                        column: x => x.TrainerProfileId,
                        principalTable: "trainer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trainer_tiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    PassPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_tiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trainer_permission_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_permission_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trainer_permission_overrides_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trainer_permission_overrides_trainer_profiles_TrainerProfil~",
                        column: x => x.TrainerProfileId,
                        principalTable: "trainer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trainer_tier_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousTierId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewTierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_tier_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trainer_tier_history_trainer_profiles_TrainerProfileId",
                        column: x => x.TrainerProfileId,
                        principalTable: "trainer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trainer_tier_history_trainer_tiers_NewTierId",
                        column: x => x.NewTierId,
                        principalTable: "trainer_tiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trainer_tier_history_trainer_tiers_PreviousTierId",
                        column: x => x.PreviousTierId,
                        principalTable: "trainer_tiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trainer_tier_permissions",
                columns: table => new
                {
                    TrainerTierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_tier_permissions", x => new { x.TrainerTierId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_trainer_tier_permissions_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trainer_tier_permissions_trainer_tiers_TrainerTierId",
                        column: x => x.TrainerTierId,
                        principalTable: "trainer_tiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trainer_upgrade_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentTierId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedTierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AdminNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainer_upgrade_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trainer_upgrade_requests_trainer_profiles_TrainerProfileId",
                        column: x => x.TrainerProfileId,
                        principalTable: "trainer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trainer_upgrade_requests_trainer_tiers_CurrentTierId",
                        column: x => x.CurrentTierId,
                        principalTable: "trainer_tiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trainer_upgrade_requests_trainer_tiers_RequestedTierId",
                        column: x => x.RequestedTierId,
                        principalTable: "trainer_tiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trainer_profiles_CurrentTierId",
                table: "trainer_profiles",
                column: "CurrentTierId");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_profiles_UserId1",
                table: "trainer_profiles",
                column: "UserId1",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_applications_PaymentTransactionId",
                table: "trainer_applications",
                column: "PaymentTransactionId",
                unique: true,
                filter: "\"PaymentTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_applications_TrainerProfileId",
                table: "trainer_applications",
                column: "TrainerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_AdminUserId",
                table: "admin_actions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_EntityType_EntityId",
                table: "admin_actions",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Code",
                table: "permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_availability_TrainerProfileId_DayOfWeek_StartTime_E~",
                table: "trainer_availability",
                columns: new[] { "TrainerProfileId", "DayOfWeek", "StartTime", "EndTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_performance_snapshots_TrainerProfileId_SnapshotDate",
                table: "trainer_performance_snapshots",
                columns: new[] { "TrainerProfileId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_permission_overrides_PermissionId",
                table: "trainer_permission_overrides",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_permission_overrides_TrainerProfileId_PermissionId",
                table: "trainer_permission_overrides",
                columns: new[] { "TrainerProfileId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_tier_history_NewTierId",
                table: "trainer_tier_history",
                column: "NewTierId");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_tier_history_PreviousTierId",
                table: "trainer_tier_history",
                column: "PreviousTierId");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_tier_history_TrainerProfileId",
                table: "trainer_tier_history",
                column: "TrainerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_tier_permissions_PermissionId",
                table: "trainer_tier_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_tiers_Code",
                table: "trainer_tiers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_tiers_DisplayOrder",
                table: "trainer_tiers",
                column: "DisplayOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_upgrade_requests_CurrentTierId",
                table: "trainer_upgrade_requests",
                column: "CurrentTierId");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_upgrade_requests_RequestedTierId",
                table: "trainer_upgrade_requests",
                column: "RequestedTierId");

            migrationBuilder.CreateIndex(
                name: "IX_trainer_upgrade_requests_TrainerProfileId_Status",
                table: "trainer_upgrade_requests",
                columns: new[] { "TrainerProfileId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_trainer_applications_payment_transactions_PaymentTransactio~",
                table: "trainer_applications",
                column: "PaymentTransactionId",
                principalTable: "payment_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trainer_profiles_trainer_tiers_CurrentTierId",
                table: "trainer_profiles",
                column: "CurrentTierId",
                principalTable: "trainer_tiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trainer_profiles_users_UserId",
                table: "trainer_profiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_trainer_profiles_users_UserId1",
                table: "trainer_profiles",
                column: "UserId1",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trainer_applications_payment_transactions_PaymentTransactio~",
                table: "trainer_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_trainer_profiles_trainer_tiers_CurrentTierId",
                table: "trainer_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_trainer_profiles_users_UserId",
                table: "trainer_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_trainer_profiles_users_UserId1",
                table: "trainer_profiles");

            migrationBuilder.DropTable(
                name: "admin_actions");

            migrationBuilder.DropTable(
                name: "trainer_availability");

            migrationBuilder.DropTable(
                name: "trainer_performance_snapshots");

            migrationBuilder.DropTable(
                name: "trainer_permission_overrides");

            migrationBuilder.DropTable(
                name: "trainer_tier_history");

            migrationBuilder.DropTable(
                name: "trainer_tier_permissions");

            migrationBuilder.DropTable(
                name: "trainer_upgrade_requests");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "trainer_tiers");

            migrationBuilder.DropIndex(
                name: "IX_trainer_profiles_CurrentTierId",
                table: "trainer_profiles");

            migrationBuilder.DropIndex(
                name: "IX_trainer_profiles_UserId1",
                table: "trainer_profiles");

            migrationBuilder.DropIndex(
                name: "IX_trainer_applications_PaymentTransactionId",
                table: "trainer_applications");

            migrationBuilder.DropIndex(
                name: "IX_trainer_applications_TrainerProfileId",
                table: "trainer_applications");

            migrationBuilder.DropColumn(
                name: "CurrentTierId",
                table: "trainer_profiles");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "trainer_profiles");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "trainer_documents");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "trainer_documents");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "trainer_applications");

            migrationBuilder.DropColumn(
                name: "PaymentTransactionId",
                table: "trainer_applications");

            migrationBuilder.DropColumn(
                name: "PaymentVerifiedAt",
                table: "trainer_applications");

            migrationBuilder.AlterColumn<string>(
                name: "ProfilePhotoUrl",
                table: "trainer_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrentStudio",
                table: "trainer_profiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Bio",
                table: "trainer_profiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ApplicationNotes",
                table: "trainer_applications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3000)",
                oldMaxLength: 3000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainer_applications_TrainerProfileId_Status",
                table: "trainer_applications",
                columns: new[] { "TrainerProfileId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_trainer_profiles_users_UserId",
                table: "trainer_profiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
