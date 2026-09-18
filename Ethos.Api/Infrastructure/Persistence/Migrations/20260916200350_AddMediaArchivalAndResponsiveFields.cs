using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaArchivalAndResponsiveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LayoutType",
                table: "media_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Square",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "square_1_1");

            migrationBuilder.AddColumn<string>(
                name: "AltText",
                table: "media_items",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FocalPoint",
                table: "media_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "center");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "media_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OptimizedUrl",
                table: "media_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "media_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_items_IsArchived",
                table: "media_items",
                column: "IsArchived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_media_items_IsArchived",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "AltText",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "FocalPoint",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "OptimizedUrl",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "media_items");

            migrationBuilder.AlterColumn<string>(
                name: "LayoutType",
                table: "media_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "square_1_1",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Square");
        }
    }
}
