using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaPlacementsAndRemoveGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─────────────────────────────────────────────────────────────────────
            // STEP 1: Create the new media_placements table first
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "media_placements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Section = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    VisibleFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VisibleUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_placements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_media_placements_media_items_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "media_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_media_placements_is_published",
                table: "media_placements",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "ix_media_placements_item_section",
                table: "media_placements",
                columns: new[] { "MediaItemId", "Section" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_placements_section",
                table: "media_placements",
                column: "Section");

            // ─────────────────────────────────────────────────────────────────────
            // STEP 2: Seed existing MediaItems into media_placements.
            //   - HomepageGallery items → "Draft" (section removed, safe fallback)
            //   - All other sections → copy as-is
            //   - HomepageReels is new; no existing items map to it
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT INTO media_placements (""Id"", ""MediaItemId"", ""Section"", ""DisplayOrder"", ""IsPublished"", ""IsFeatured"", ""VisibleFromUtc"", ""VisibleUntilUtc"", ""CreatedAt"")
                SELECT
                    gen_random_uuid(),
                    ""Id"",
                    CASE WHEN ""Section"" = 'HomepageGallery' THEN 'Draft' ELSE ""Section"" END,
                    ""DisplayOrder"",
                    ""IsPublished"",
                    ""IsFeatured"",
                    ""VisibleFromUtc"",
                    ""VisibleUntilUtc"",
                    NOW() AT TIME ZONE 'UTC'
                FROM media_items
                WHERE ""IsDeleted"" = false;
            ");

            // ─────────────────────────────────────────────────────────────────────
            // STEP 3: Remove placement-specific columns from media_items
            // ─────────────────────────────────────────────────────────────────────
            migrationBuilder.DropIndex(
                name: "IX_media_items_DisplayOrder",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "IX_media_items_IsPublished",
                table: "media_items");

            migrationBuilder.DropIndex(
                name: "IX_media_items_Section",
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
                name: "Section",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "VisibleFromUtc",
                table: "media_items");

            migrationBuilder.DropColumn(
                name: "VisibleUntilUtc",
                table: "media_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_placements");

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
                name: "Section",
                table: "media_items",
                type: "character varying(100)",
                maxLength: 100,
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

            migrationBuilder.CreateIndex(
                name: "IX_media_items_DisplayOrder",
                table: "media_items",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_media_items_IsPublished",
                table: "media_items",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_media_items_Section",
                table: "media_items",
                column: "Section");
        }
    }
}
