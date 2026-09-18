using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingConcurrencyAndStudioVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workshop_bookings_WorkshopId_StudentProfileId",
                table: "workshop_bookings");

            migrationBuilder.AlterColumn<string>(
                name: "LandscapeImageUrl",
                table: "workshops",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "workshops",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CancelledAt",
                table: "workshop_bookings",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "workshop_bookings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReservationExpiresAt",
                table: "workshop_bookings",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_workshop_bookings_IdempotencyKey",
                table: "workshop_bookings",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workshop_bookings_WorkshopId_StudentProfileId",
                table: "workshop_bookings",
                columns: new[] { "WorkshopId", "StudentProfileId" });

            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    CREATE TABLE IF NOT EXISTS studio_videos (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""Title"" character varying(150) NOT NULL,
                        ""Description"" character varying(500),
                        ""Section"" character varying(50) NOT NULL,
                        ""ObjectKey"" character varying(500) NOT NULL,
                        ""PublicUrl"" character varying(1000) NOT NULL,
                        ""ThumbnailUrl"" character varying(1000),
                        ""DisplayOrder"" integer NOT NULL DEFAULT 0,
                        ""IsActive"" boolean NOT NULL DEFAULT TRUE,
                        ""DurationSeconds"" double precision,
                        ""FileSizeBytes"" bigint NOT NULL DEFAULT 0,
                        ""MimeType"" character varying(100) NOT NULL DEFAULT 'video/mp4',
                        ""UploadedByUserId"" uuid,
                        ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT ""FK_studio_videos_users_UploadedByUserId"" FOREIGN KEY (""UploadedByUserId"") REFERENCES users (""Id"") ON DELETE SET NULL
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_studio_videos_Section_IsActive_DisplayOrder"" ON studio_videos (""Section"", ""IsActive"", ""DisplayOrder"");
                    CREATE INDEX IF NOT EXISTS ""IX_studio_videos_ObjectKey"" ON studio_videos (""ObjectKey"");

                    UPDATE workshop_bookings SET ""IdempotencyKey"" = gen_random_uuid()::text WHERE ""IdempotencyKey"" IS NULL OR ""IdempotencyKey"" = '';
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workshop_bookings_IdempotencyKey",
                table: "workshop_bookings");

            migrationBuilder.DropIndex(
                name: "IX_workshop_bookings_WorkshopId_StudentProfileId",
                table: "workshop_bookings");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "workshop_bookings");

            migrationBuilder.DropColumn(
                name: "ReservationExpiresAt",
                table: "workshop_bookings");

            migrationBuilder.AlterColumn<string>(
                name: "LandscapeImageUrl",
                table: "workshops",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "workshops",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CancelledAt",
                table: "workshop_bookings",
                type: "timestamptz",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_workshop_bookings_WorkshopId_StudentProfileId",
                table: "workshop_bookings",
                columns: new[] { "WorkshopId", "StudentProfileId" },
                unique: true);
        }
    }
}
