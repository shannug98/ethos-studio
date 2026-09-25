using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundSystemAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_paise = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "INR"),
                    status = table.Column<int>(type: "integer", nullable: false),
                    razorpay_refund_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    razorpay_payment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    initiated_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    processing_started_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    processed_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    last_attempt_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_refunds", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_refunds_payment_transactions_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payment_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_refunds_workshop_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "workshop_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refund_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workshop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_refund_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_paise = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    initiated_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_retry_utc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_refund_jobs_payment_refunds_payment_refund_id",
                        column: x => x.payment_refund_id,
                        principalTable: "payment_refunds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_refund_jobs_payment_transactions_payment_transaction_id",
                        column: x => x.payment_transaction_id,
                        principalTable: "payment_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refund_jobs_workshop_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "workshop_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refund_jobs_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_refunds_booking_id",
                table: "payment_refunds",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_refunds_payment_id_active_unique",
                table: "payment_refunds",
                column: "payment_id",
                unique: true,
                filter: "\"status\" != 4");

            migrationBuilder.CreateIndex(
                name: "ix_payment_refunds_status",
                table: "payment_refunds",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_refund_jobs_booking_id",
                table: "refund_jobs",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_refund_jobs_payment_refund_id",
                table: "refund_jobs",
                column: "payment_refund_id");

            migrationBuilder.CreateIndex(
                name: "IX_refund_jobs_payment_transaction_id",
                table: "refund_jobs",
                column: "payment_transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_refund_jobs_status",
                table: "refund_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_refund_jobs_workshop_id_status",
                table: "refund_jobs",
                columns: new[] { "workshop_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refund_jobs");

            migrationBuilder.DropTable(
                name: "payment_refunds");
        }
    }
}
