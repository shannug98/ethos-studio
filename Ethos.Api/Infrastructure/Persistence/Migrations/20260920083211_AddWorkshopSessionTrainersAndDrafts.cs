using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkshopSessionTrainersAndDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PosterImageUrl",
                table: "workshop_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkshopSessionId",
                table: "workshop_pass_types",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workshop_drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workshop_drafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workshop_drafts_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workshop_drafts_workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workshop_session_trainers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AssignedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workshop_session_trainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workshop_session_trainers_trainer_profiles_TrainerProfileId",
                        column: x => x.TrainerProfileId,
                        principalTable: "trainer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workshop_session_trainers_workshop_sessions_WorkshopSession~",
                        column: x => x.WorkshopSessionId,
                        principalTable: "workshop_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workshop_pass_types_WorkshopSessionId",
                table: "workshop_pass_types",
                column: "WorkshopSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_drafts_AdminUserId",
                table: "workshop_drafts",
                column: "AdminUserId",
                unique: true,
                filter: @"""WorkshopId"" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_drafts_AdminUserId_WorkshopId",
                table: "workshop_drafts",
                columns: new[] { "AdminUserId", "WorkshopId" },
                unique: true,
                filter: @"""WorkshopId"" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_drafts_WorkshopId",
                table: "workshop_drafts",
                column: "WorkshopId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_session_trainers_TrainerProfileId",
                table: "workshop_session_trainers",
                column: "TrainerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_session_trainers_WorkshopSessionId_TrainerProfileId",
                table: "workshop_session_trainers",
                columns: new[] { "WorkshopSessionId", "TrainerProfileId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_workshop_pass_types_workshop_sessions_WorkshopSessionId",
                table: "workshop_pass_types",
                column: "WorkshopSessionId",
                principalTable: "workshop_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Safe, deterministic backfill for existing workshop sessions:
            // 1. Backfill existing session trainers into workshop_session_trainers with display_order = 0
            migrationBuilder.Sql(@"
                INSERT INTO workshop_session_trainers (""Id"", ""WorkshopSessionId"", ""TrainerProfileId"", ""DisplayOrder"", ""AssignedAt"")
                SELECT gen_random_uuid(), s.""Id"", s.""TrainerProfileId"", 0, NOW()
                FROM workshop_sessions s
                WHERE s.""TrainerProfileId"" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM workshop_session_trainers wst 
                      WHERE wst.""WorkshopSessionId"" = s.""Id"" AND wst.""TrainerProfileId"" = s.""TrainerProfileId""
                  );
            ");

            // 2. Ensure all session trainers exist in workshop_trainers (workshop faculty pool)
            migrationBuilder.Sql(@"
                INSERT INTO workshop_trainers (""Id"", ""WorkshopId"", ""TrainerProfileId"", ""DisplayOrder"", ""AssignedAt"")
                SELECT gen_random_uuid(), s.""WorkshopId"", s.""TrainerProfileId"", 0, NOW()
                FROM workshop_sessions s
                WHERE s.""TrainerProfileId"" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM workshop_trainers wt 
                      WHERE wt.""WorkshopId"" = s.""WorkshopId"" AND wt.""TrainerProfileId"" = s.""TrainerProfileId""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workshop_pass_types_workshop_sessions_WorkshopSessionId",
                table: "workshop_pass_types");

            migrationBuilder.DropTable(
                name: "workshop_drafts");

            migrationBuilder.DropTable(
                name: "workshop_session_trainers");

            migrationBuilder.DropIndex(
                name: "IX_workshop_pass_types_WorkshopSessionId",
                table: "workshop_pass_types");

            migrationBuilder.DropColumn(
                name: "PosterImageUrl",
                table: "workshop_sessions");

            migrationBuilder.DropColumn(
                name: "WorkshopSessionId",
                table: "workshop_pass_types");
        }
    }
}
