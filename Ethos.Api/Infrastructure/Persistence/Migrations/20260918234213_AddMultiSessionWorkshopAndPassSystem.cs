using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiSessionWorkshopAndPassSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkshopSessionId",
                table: "workshop_tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassName",
                table: "workshop_bookings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PassPrice",
                table: "workshop_bookings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedSessionIdsJson",
                table: "workshop_bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionsIncludedCount",
                table: "workshop_bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkshopPassTypeId",
                table: "workshop_bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workshop_pass_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SessionsIncluded = table.Column<int>(type: "integer", nullable: true),
                    TotalQuantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    SalesEndUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workshop_pass_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workshop_pass_types_workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workshop_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionDate = table.Column<DateTime>(type: "date", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TrainerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    BookingCutoffTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workshop_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workshop_sessions_trainer_profiles_TrainerProfileId",
                        column: x => x.TrainerProfileId,
                        principalTable: "trainer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workshop_sessions_workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workshop_trainers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AssignedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workshop_trainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workshop_trainers_trainer_profiles_TrainerProfileId",
                        column: x => x.TrainerProfileId,
                        principalTable: "trainer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workshop_trainers_workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workshop_booking_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopBookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkshopTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OriginalSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ReplacedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    CutoffOverrideUsed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    OverrideReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workshop_booking_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workshop_booking_sessions_workshop_bookings_WorkshopBooking~",
                        column: x => x.WorkshopBookingId,
                        principalTable: "workshop_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workshop_booking_sessions_workshop_sessions_WorkshopSession~",
                        column: x => x.WorkshopSessionId,
                        principalTable: "workshop_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workshop_booking_sessions_workshop_tickets_WorkshopTicketId",
                        column: x => x.WorkshopTicketId,
                        principalTable: "workshop_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workshop_tickets_WorkshopSessionId",
                table: "workshop_tickets",
                column: "WorkshopSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_bookings_WorkshopPassTypeId",
                table: "workshop_bookings",
                column: "WorkshopPassTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_booking_sessions_WorkshopBookingId",
                table: "workshop_booking_sessions",
                column: "WorkshopBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_booking_sessions_WorkshopSessionId_Status",
                table: "workshop_booking_sessions",
                columns: new[] { "WorkshopSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_workshop_booking_sessions_WorkshopTicketId",
                table: "workshop_booking_sessions",
                column: "WorkshopTicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workshop_pass_types_WorkshopId",
                table: "workshop_pass_types",
                column: "WorkshopId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_sessions_TrainerProfileId",
                table: "workshop_sessions",
                column: "TrainerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_sessions_WorkshopId_SessionDate",
                table: "workshop_sessions",
                columns: new[] { "WorkshopId", "SessionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_workshop_trainers_TrainerProfileId",
                table: "workshop_trainers",
                column: "TrainerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_trainers_WorkshopId_TrainerProfileId",
                table: "workshop_trainers",
                columns: new[] { "WorkshopId", "TrainerProfileId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_workshop_bookings_workshop_pass_types_WorkshopPassTypeId",
                table: "workshop_bookings",
                column: "WorkshopPassTypeId",
                principalTable: "workshop_pass_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_workshop_tickets_workshop_sessions_WorkshopSessionId",
                table: "workshop_tickets",
                column: "WorkshopSessionId",
                principalTable: "workshop_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workshop_bookings_workshop_pass_types_WorkshopPassTypeId",
                table: "workshop_bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_workshop_tickets_workshop_sessions_WorkshopSessionId",
                table: "workshop_tickets");

            migrationBuilder.DropTable(
                name: "workshop_booking_sessions");

            migrationBuilder.DropTable(
                name: "workshop_pass_types");

            migrationBuilder.DropTable(
                name: "workshop_trainers");

            migrationBuilder.DropTable(
                name: "workshop_sessions");

            migrationBuilder.DropIndex(
                name: "IX_workshop_tickets_WorkshopSessionId",
                table: "workshop_tickets");

            migrationBuilder.DropIndex(
                name: "IX_workshop_bookings_WorkshopPassTypeId",
                table: "workshop_bookings");

            migrationBuilder.DropColumn(
                name: "WorkshopSessionId",
                table: "workshop_tickets");

            migrationBuilder.DropColumn(
                name: "PassName",
                table: "workshop_bookings");

            migrationBuilder.DropColumn(
                name: "PassPrice",
                table: "workshop_bookings");

            migrationBuilder.DropColumn(
                name: "SelectedSessionIdsJson",
                table: "workshop_bookings");

            migrationBuilder.DropColumn(
                name: "SessionsIncludedCount",
                table: "workshop_bookings");

            migrationBuilder.DropColumn(
                name: "WorkshopPassTypeId",
                table: "workshop_bookings");
        }
    }
}
