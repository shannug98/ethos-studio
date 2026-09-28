using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedWorkshopFeedbackSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudienceType",
                table: "workshop_feedback",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "FeedbackFormVersionId",
                table: "workshop_feedback",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkshopFeedbackTokenId",
                table: "workshop_feedback",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "feedback_form_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopFeedbackSettingId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsFrozen = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FrozenAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedback_form_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "feedback_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackFormVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    QuestionType = table.Column<int>(type: "integer", nullable: false),
                    TargetAudience = table.Column<int>(type: "integer", nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedback_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_feedback_questions_feedback_form_versions_FeedbackFormVersi~",
                        column: x => x.FeedbackFormVersionId,
                        principalTable: "feedback_form_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workshop_feedback_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsFeedbackEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ConfigCutoffUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LockedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ActiveVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workshop_feedback_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workshop_feedback_settings_feedback_form_versions_ActiveVer~",
                        column: x => x.ActiveVersionId,
                        principalTable: "feedback_form_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_workshop_feedback_settings_workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workshop_feedback_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopFeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumericValue = table.Column<int>(type: "integer", nullable: true),
                    TextValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workshop_feedback_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workshop_feedback_answers_feedback_questions_FeedbackQuesti~",
                        column: x => x.FeedbackQuestionId,
                        principalTable: "feedback_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workshop_feedback_answers_workshop_feedback_WorkshopFeedbac~",
                        column: x => x.WorkshopFeedbackId,
                        principalTable: "workshop_feedback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workshop_feedback_FeedbackFormVersionId",
                table: "workshop_feedback",
                column: "FeedbackFormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_feedback_WorkshopFeedbackTokenId",
                table: "workshop_feedback",
                column: "WorkshopFeedbackTokenId",
                unique: true,
                filter: "\"WorkshopFeedbackTokenId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_form_versions_WorkshopFeedbackSettingId_VersionNum~",
                table: "feedback_form_versions",
                columns: new[] { "WorkshopFeedbackSettingId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feedback_questions_FeedbackFormVersionId_SortOrder",
                table: "feedback_questions",
                columns: new[] { "FeedbackFormVersionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_workshop_feedback_answers_FeedbackQuestionId",
                table: "workshop_feedback_answers",
                column: "FeedbackQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_feedback_answers_WorkshopFeedbackId_FeedbackQuesti~",
                table: "workshop_feedback_answers",
                columns: new[] { "WorkshopFeedbackId", "FeedbackQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workshop_feedback_settings_ActiveVersionId",
                table: "workshop_feedback_settings",
                column: "ActiveVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_feedback_settings_WorkshopId",
                table: "workshop_feedback_settings",
                column: "WorkshopId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_workshop_feedback_feedback_form_versions_FeedbackFormVersio~",
                table: "workshop_feedback",
                column: "FeedbackFormVersionId",
                principalTable: "feedback_form_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_workshop_feedback_workshop_feedback_tokens_WorkshopFeedback~",
                table: "workshop_feedback",
                column: "WorkshopFeedbackTokenId",
                principalTable: "workshop_feedback_tokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_feedback_form_versions_workshop_feedback_settings_WorkshopF~",
                table: "feedback_form_versions",
                column: "WorkshopFeedbackSettingId",
                principalTable: "workshop_feedback_settings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workshop_feedback_feedback_form_versions_FeedbackFormVersio~",
                table: "workshop_feedback");

            migrationBuilder.DropForeignKey(
                name: "FK_workshop_feedback_workshop_feedback_tokens_WorkshopFeedback~",
                table: "workshop_feedback");

            migrationBuilder.DropForeignKey(
                name: "FK_feedback_form_versions_workshop_feedback_settings_WorkshopF~",
                table: "feedback_form_versions");

            migrationBuilder.DropTable(
                name: "workshop_feedback_answers");

            migrationBuilder.DropTable(
                name: "feedback_questions");

            migrationBuilder.DropTable(
                name: "workshop_feedback_settings");

            migrationBuilder.DropTable(
                name: "feedback_form_versions");

            migrationBuilder.DropIndex(
                name: "IX_workshop_feedback_FeedbackFormVersionId",
                table: "workshop_feedback");

            migrationBuilder.DropIndex(
                name: "IX_workshop_feedback_WorkshopFeedbackTokenId",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "AudienceType",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "FeedbackFormVersionId",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "WorkshopFeedbackTokenId",
                table: "workshop_feedback");
        }
    }
}
