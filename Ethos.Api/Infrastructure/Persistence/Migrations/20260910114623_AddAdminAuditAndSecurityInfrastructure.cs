using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAuditAndSecurityInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdminDeviceId",
                table: "security_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdminSessionId",
                table: "security_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "security_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdminDeviceId",
                table: "admin_actions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdminSessionId",
                table: "admin_actions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "admin_actions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "admin_actions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "admin_actions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeCode",
                table: "admin_actions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "admin_actions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Success",
                table: "admin_actions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "admin_actions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "admin_actions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_security_events_AdminDeviceId",
                table: "security_events",
                column: "AdminDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_AdminSessionId",
                table: "security_events",
                column: "AdminSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_security_events_TraceId",
                table: "security_events",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_AdminDeviceId",
                table: "admin_actions",
                column: "AdminDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_AdminSessionId",
                table: "admin_actions",
                column: "AdminSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_Category",
                table: "admin_actions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_CreatedAt",
                table: "admin_actions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_TraceId",
                table: "admin_actions",
                column: "TraceId");

            migrationBuilder.AddForeignKey(
                name: "FK_admin_actions_admin_devices_AdminDeviceId",
                table: "admin_actions",
                column: "AdminDeviceId",
                principalTable: "admin_devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_admin_actions_admin_sessions_AdminSessionId",
                table: "admin_actions",
                column: "AdminSessionId",
                principalTable: "admin_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_admin_actions_users_AdminUserId",
                table: "admin_actions",
                column: "AdminUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_security_events_admin_devices_AdminDeviceId",
                table: "security_events",
                column: "AdminDeviceId",
                principalTable: "admin_devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_security_events_admin_sessions_AdminSessionId",
                table: "security_events",
                column: "AdminSessionId",
                principalTable: "admin_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_admin_actions_admin_devices_AdminDeviceId",
                table: "admin_actions");

            migrationBuilder.DropForeignKey(
                name: "FK_admin_actions_admin_sessions_AdminSessionId",
                table: "admin_actions");

            migrationBuilder.DropForeignKey(
                name: "FK_admin_actions_users_AdminUserId",
                table: "admin_actions");

            migrationBuilder.DropForeignKey(
                name: "FK_security_events_admin_devices_AdminDeviceId",
                table: "security_events");

            migrationBuilder.DropForeignKey(
                name: "FK_security_events_admin_sessions_AdminSessionId",
                table: "security_events");

            migrationBuilder.DropIndex(
                name: "IX_security_events_AdminDeviceId",
                table: "security_events");

            migrationBuilder.DropIndex(
                name: "IX_security_events_AdminSessionId",
                table: "security_events");

            migrationBuilder.DropIndex(
                name: "IX_security_events_TraceId",
                table: "security_events");

            migrationBuilder.DropIndex(
                name: "IX_admin_actions_AdminDeviceId",
                table: "admin_actions");

            migrationBuilder.DropIndex(
                name: "IX_admin_actions_AdminSessionId",
                table: "admin_actions");

            migrationBuilder.DropIndex(
                name: "IX_admin_actions_Category",
                table: "admin_actions");

            migrationBuilder.DropIndex(
                name: "IX_admin_actions_CreatedAt",
                table: "admin_actions");

            migrationBuilder.DropIndex(
                name: "IX_admin_actions_TraceId",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "AdminDeviceId",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "AdminSessionId",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "AdminDeviceId",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "AdminSessionId",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "OutcomeCode",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "Success",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "admin_actions");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "admin_actions");
        }
    }
}
