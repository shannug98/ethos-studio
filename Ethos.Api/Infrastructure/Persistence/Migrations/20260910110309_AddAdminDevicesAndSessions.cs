using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminDevicesAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MaskedPhone",
                table: "security_events",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "admin_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceCredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenIp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FingerprintTelemetry = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_devices_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admin_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_sessions_admin_devices_AdminDeviceId",
                        column: x => x.AdminDeviceId,
                        principalTable: "admin_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_admin_sessions_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_devices_AdminUserId_Status",
                table: "admin_devices",
                columns: new[] { "AdminUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_devices_DeviceCredentialHash",
                table: "admin_devices",
                column: "DeviceCredentialHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_devices_Status_RevokedAt",
                table: "admin_devices",
                columns: new[] { "Status", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_sessions_AdminDeviceId_IsActive",
                table: "admin_sessions",
                columns: new[] { "AdminDeviceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_sessions_AdminUserId_IsActive",
                table: "admin_sessions",
                columns: new[] { "AdminUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_sessions_SessionTokenHash",
                table: "admin_sessions",
                column: "SessionTokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_security_events_users_UserId",
                table: "security_events",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_security_events_users_UserId",
                table: "security_events");

            migrationBuilder.DropTable(
                name: "admin_sessions");

            migrationBuilder.DropTable(
                name: "admin_devices");

            migrationBuilder.AlterColumn<string>(
                name: "MaskedPhone",
                table: "security_events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
