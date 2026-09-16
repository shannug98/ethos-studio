using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkshopWizardMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "workshops",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "workshops",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "workshops",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson",
                table: "workshops",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndUtc",
                table: "workshops",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GooglePlaceId",
                table: "workshops",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandscapeImageUrl",
                table: "workshops",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "workshops",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "workshops",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicVisibility",
                table: "workshops",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationType",
                table: "workshops",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "workshops",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartUtc",
                table: "workshops",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsAndCancellationPolicy",
                table: "workshops",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Timezone",
                table: "workshops",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Asia/Kolkata");

            migrationBuilder.AddColumn<string>(
                name: "VenueAddress",
                table: "workshops",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE workshops 
                SET ""StartUtc"" = (""WorkshopDate""::date + ""StartTime"") AT TIME ZONE 'Asia/Kolkata' AT TIME ZONE 'UTC',
                    ""EndUtc"" = CASE 
                        WHEN ""EndTime"" > ""StartTime"" THEN (""WorkshopDate""::date + ""EndTime"") AT TIME ZONE 'Asia/Kolkata' AT TIME ZONE 'UTC'
                        ELSE ((""WorkshopDate""::date + interval '1 day') + ""EndTime"") AT TIME ZONE 'Asia/Kolkata' AT TIME ZONE 'UTC'
                    END,
                    ""Timezone"" = COALESCE(""Timezone"", 'Asia/Kolkata'),
                    ""PublicVisibility"" = COALESCE(""PublicVisibility"", true),
                    ""RegistrationType"" = COALESCE(""RegistrationType"", 'Standard')
                WHERE ""StartUtc"" IS NULL;
            ");

            // studio_videos table already exists in database
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Area",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "City",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "ContactPerson",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "EndUtc",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "GooglePlaceId",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "LandscapeImageUrl",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "PublicVisibility",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "RegistrationType",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "StartUtc",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "TermsAndCancellationPolicy",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "Timezone",
                table: "workshops");

            migrationBuilder.DropColumn(
                name: "VenueAddress",
                table: "workshops");
        }
    }
}
