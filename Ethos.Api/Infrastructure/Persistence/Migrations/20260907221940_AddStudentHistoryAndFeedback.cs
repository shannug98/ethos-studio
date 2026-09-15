using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentHistoryAndFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "workshop_feedback",
                type: "timestamptz",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz");

            migrationBuilder.AddColumn<int>(
                name: "DemonstrationRating",
                table: "workshop_feedback",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationRating",
                table: "workshop_feedback",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExplanationClarity",
                table: "workshop_feedback",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Improvements",
                table: "workshop_feedback",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InteractionRating",
                table: "workshop_feedback",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationRating",
                table: "workshop_feedback",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValueForMoney",
                table: "workshop_feedback",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VenueRating",
                table: "workshop_feedback",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkshopBookingId",
                table: "workshop_feedback",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WouldAttendTrainerAgain",
                table: "workshop_feedback",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "class_feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DanceClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    TeachingQuality = table.Column<int>(type: "integer", nullable: false),
                    ExplanationClarity = table.Column<int>(type: "integer", nullable: false),
                    TrainerEngagement = table.Column<int>(type: "integer", nullable: false),
                    ClassPace = table.Column<int>(type: "integer", nullable: false),
                    ChoreographyContent = table.Column<int>(type: "integer", nullable: false),
                    DifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                    ClassExperience = table.Column<int>(type: "integer", nullable: false),
                    LikedAspects = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Improvements = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WouldAttendAgain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_class_feedback_class_enrollments_ClassEnrollmentId",
                        column: x => x.ClassEnrollmentId,
                        principalTable: "class_enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_class_feedback_dance_classes_DanceClassId",
                        column: x => x.DanceClassId,
                        principalTable: "dance_classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_class_feedback_student_profiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "student_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workshop_feedback_WorkshopBookingId",
                table: "workshop_feedback",
                column: "WorkshopBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_class_feedback_ClassEnrollmentId",
                table: "class_feedback",
                column: "ClassEnrollmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_feedback_DanceClassId",
                table: "class_feedback",
                column: "DanceClassId");

            migrationBuilder.CreateIndex(
                name: "IX_class_feedback_StudentProfileId",
                table: "class_feedback",
                column: "StudentProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_workshop_feedback_workshop_bookings_WorkshopBookingId",
                table: "workshop_feedback",
                column: "WorkshopBookingId",
                principalTable: "workshop_bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workshop_feedback_workshop_bookings_WorkshopBookingId",
                table: "workshop_feedback");

            migrationBuilder.DropTable(
                name: "class_feedback");

            migrationBuilder.DropIndex(
                name: "IX_workshop_feedback_WorkshopBookingId",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "DemonstrationRating",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "DurationRating",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "ExplanationClarity",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "Improvements",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "InteractionRating",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "OrganizationRating",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "ValueForMoney",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "VenueRating",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "WorkshopBookingId",
                table: "workshop_feedback");

            migrationBuilder.DropColumn(
                name: "WouldAttendTrainerAgain",
                table: "workshop_feedback");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "workshop_feedback",
                type: "timestamptz",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldNullable: true);
        }
    }
}
