using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaGalleryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Caption",
                table: "media_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "media_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "media_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "media_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "media_items",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "LayoutType",
                table: "media_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "square_1_1");

            migrationBuilder.AddColumn<string>(
                name: "TargetUrl",
                table: "media_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "media_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "VisibleFromUtc",
                table: "media_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VisibleUntilUtc",
                table: "media_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkshopId",
                table: "media_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_items_Category",
                table: "media_items",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_media_items_DisplayOrder",
                table: "media_items",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_media_items_IsPublished",
                table: "media_items",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_media_items_WorkshopId",
                table: "media_items",
                column: "WorkshopId");

            migrationBuilder.AddForeignKey(
                name: "FK_media_items_workshops_WorkshopId",
                table: "media_items",
                column: "WorkshopId",
                principalTable: "workshops",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_media_items_workshops_WorkshopId",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "IX_media_items_Category",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "IX_media_items_DisplayOrder",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "IX_media_items_IsPublished",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "IX_media_items_WorkshopId",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "Caption",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "LayoutType",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "TargetUrl",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "VisibleFromUtc",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "VisibleUntilUtc",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "WorkshopId",
                table: "media_items");
        }
    }
}
