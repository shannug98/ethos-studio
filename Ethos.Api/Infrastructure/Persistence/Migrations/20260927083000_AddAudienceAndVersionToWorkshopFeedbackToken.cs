using System;
using Ethos.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260927083000_AddAudienceAndVersionToWorkshopFeedbackToken")]
    public partial class AddAudienceAndVersionToWorkshopFeedbackToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Rating",
                table: "workshop_feedback",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "AudienceType",
                table: "workshop_feedback_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "FeedbackFormVersionId",
                table: "workshop_feedback_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_workshop_feedback_tokens_FeedbackFormVersionId",
                table: "workshop_feedback_tokens",
                column: "FeedbackFormVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_workshop_feedback_tokens_feedback_form_versions_FeedbackFo~",
                table: "workshop_feedback_tokens",
                column: "FeedbackFormVersionId",
                principalTable: "feedback_form_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workshop_feedback_tokens_feedback_form_versions_FeedbackFo~",
                table: "workshop_feedback_tokens");

            migrationBuilder.DropIndex(
                name: "IX_workshop_feedback_tokens_FeedbackFormVersionId",
                table: "workshop_feedback_tokens");

            migrationBuilder.DropColumn(
                name: "AudienceType",
                table: "workshop_feedback_tokens");

            migrationBuilder.DropColumn(
                name: "FeedbackFormVersionId",
                table: "workshop_feedback_tokens");

            migrationBuilder.AlterColumn<int>(
                name: "Rating",
                table: "workshop_feedback",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
