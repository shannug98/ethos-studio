using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ethos.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTrainerDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS trainer_documents CASCADE;");

            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql(@"
                    CREATE TABLE IF NOT EXISTS api_request_logs (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""TraceId"" character varying(100) NOT NULL,
                        ""CorrelationId"" character varying(100),
                        ""Method"" character varying(10) NOT NULL,
                        ""Path"" character varying(500) NOT NULL,
                        ""StatusCode"" integer NOT NULL,
                        ""DurationMs"" bigint NOT NULL,
                        ""IpAddress"" character varying(100),
                        ""UserAgent"" character varying(500),
                        ""UserId"" uuid,
                        ""Role"" character varying(50),
                        ""ErrorMessage"" character varying(2000),
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        CONSTRAINT ""FK_api_request_logs_users_UserId"" FOREIGN KEY (""UserId"") REFERENCES users (""Id"") ON DELETE SET NULL
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_api_request_logs_CreatedAt"" ON api_request_logs (""CreatedAt"");
                    CREATE INDEX IF NOT EXISTS ""IX_api_request_logs_Path"" ON api_request_logs (""Path"");
                    CREATE INDEX IF NOT EXISTS ""IX_api_request_logs_StatusCode"" ON api_request_logs (""StatusCode"");
                    CREATE INDEX IF NOT EXISTS ""IX_api_request_logs_TraceId"" ON api_request_logs (""TraceId"");
                    CREATE INDEX IF NOT EXISTS ""IX_api_request_logs_UserId"" ON api_request_logs (""UserId"");

                    CREATE TABLE IF NOT EXISTS incidents (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""IncidentNumber"" character varying(50) NOT NULL,
                        ""Title"" character varying(200) NOT NULL,
                        ""Description"" character varying(4000) NOT NULL,
                        ""Severity"" character varying(20) NOT NULL,
                        ""Status"" character varying(30) NOT NULL,
                        ""AffectedService"" character varying(50) NOT NULL,
                        ""AssignedAdminId"" uuid,
                        ""TraceId"" character varying(100),
                        ""EvidenceJson"" character varying(8000),
                        ""RootCause"" character varying(4000),
                        ""ResolutionNotes"" character varying(4000),
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""UpdatedAt"" timestamp with time zone NOT NULL,
                        ""ResolvedAt"" timestamp with time zone,
                        CONSTRAINT ""FK_incidents_users_AssignedAdminId"" FOREIGN KEY (""AssignedAdminId"") REFERENCES users (""Id"") ON DELETE SET NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_incidents_IncidentNumber"" ON incidents (""IncidentNumber"");
                    CREATE INDEX IF NOT EXISTS ""IX_incidents_AssignedAdminId"" ON incidents (""AssignedAdminId"");
                    CREATE INDEX IF NOT EXISTS ""IX_incidents_CreatedAt"" ON incidents (""CreatedAt"");
                    CREATE INDEX IF NOT EXISTS ""IX_incidents_Severity"" ON incidents (""Severity"");
                    CREATE INDEX IF NOT EXISTS ""IX_incidents_Status"" ON incidents (""Status"");
                    CREATE INDEX IF NOT EXISTS ""IX_incidents_TraceId"" ON incidents (""TraceId"");

                    CREATE TABLE IF NOT EXISTS incident_updates (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""IncidentId"" uuid NOT NULL,
                        ""AdminUserId"" uuid NOT NULL,
                        ""PreviousStatus"" character varying(30),
                        ""NewStatus"" character varying(30),
                        ""Message"" character varying(2000) NOT NULL,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        CONSTRAINT ""FK_incident_updates_incidents_IncidentId"" FOREIGN KEY (""IncidentId"") REFERENCES incidents (""Id"") ON DELETE CASCADE,
                        CONSTRAINT ""FK_incident_updates_users_AdminUserId"" FOREIGN KEY (""AdminUserId"") REFERENCES users (""Id"") ON DELETE RESTRICT
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_incident_updates_AdminUserId"" ON incident_updates (""AdminUserId"");
                    CREATE INDEX IF NOT EXISTS ""IX_incident_updates_CreatedAt"" ON incident_updates (""CreatedAt"");
                    CREATE INDEX IF NOT EXISTS ""IX_incident_updates_IncidentId"" ON incident_updates (""IncidentId"");

                    CREATE TABLE IF NOT EXISTS corrective_actions (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""ActionNumber"" character varying(50) NOT NULL,
                        ""ActionType"" character varying(60) NOT NULL,
                        ""TargetEntityType"" character varying(40) NOT NULL,
                        ""TargetEntityId"" uuid NOT NULL,
                        ""IncidentId"" uuid,
                        ""TraceId"" character varying(100) NOT NULL,
                        ""AdminUserId"" uuid NOT NULL,
                        ""Status"" character varying(30) NOT NULL,
                        ""IdempotencyKey"" character varying(128),
                        ""PreconditionHash"" character varying(128),
                        ""ParametersJson"" character varying(8000),
                        ""BeforeStateJson"" character varying(8000),
                        ""AfterStateJson"" character varying(8000),
                        ""Justification"" character varying(2000) NOT NULL,
                        ""ExecutionLog"" character varying(8000),
                        ""ExecutedAt"" timestamp with time zone,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        CONSTRAINT ""FK_corrective_actions_incidents_IncidentId"" FOREIGN KEY (""IncidentId"") REFERENCES incidents (""Id"") ON DELETE SET NULL,
                        CONSTRAINT ""FK_corrective_actions_users_AdminUserId"" FOREIGN KEY (""AdminUserId"") REFERENCES users (""Id"") ON DELETE RESTRICT
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_corrective_actions_ActionNumber"" ON corrective_actions (""ActionNumber"");
                    CREATE INDEX IF NOT EXISTS ""IX_corrective_actions_ActionType"" ON corrective_actions (""ActionType"");
                    CREATE INDEX IF NOT EXISTS ""IX_corrective_actions_AdminUserId"" ON corrective_actions (""AdminUserId"");
                    CREATE INDEX IF NOT EXISTS ""IX_corrective_actions_CreatedAt"" ON corrective_actions (""CreatedAt"");
                    CREATE INDEX IF NOT EXISTS ""IX_corrective_actions_IdempotencyKey"" ON corrective_actions (""IdempotencyKey"");
                    CREATE INDEX IF NOT EXISTS ""IX_corrective_actions_IncidentId"" ON corrective_actions (""IncidentId"");
                    CREATE INDEX IF NOT EXISTS ""IX_corrective_actions_Status"" ON corrective_actions (""Status"");
                    CREATE INDEX IF NOT EXISTS ""IX_corrective_actions_TargetEntityId"" ON corrective_actions (""TargetEntityId"");

                    CREATE TABLE IF NOT EXISTS communication_logs (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""MessageReference"" character varying(50) NOT NULL,
                        ""Channel"" character varying(30) NOT NULL,
                        ""Recipient"" character varying(100) NOT NULL,
                        ""RecipientUserId"" uuid,
                        ""TemplateId"" character varying(100) NOT NULL,
                        ""Subject"" character varying(200),
                        ""BodyPreview"" character varying(2000) NOT NULL,
                        ""Status"" character varying(30) NOT NULL,
                        ""Provider"" character varying(50) NOT NULL,
                        ""ProviderMessageId"" character varying(128),
                        ""ErrorMessage"" character varying(2000),
                        ""RetryCount"" integer NOT NULL DEFAULT 0,
                        ""IdempotencyKey"" character varying(128),
                        ""Justification"" character varying(1000),
                        ""TraceId"" character varying(100) NOT NULL,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""DeliveredAt"" timestamp with time zone,
                        CONSTRAINT ""FK_communication_logs_users_RecipientUserId"" FOREIGN KEY (""RecipientUserId"") REFERENCES users (""Id"") ON DELETE SET NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_communication_logs_MessageReference"" ON communication_logs (""MessageReference"");
                    CREATE INDEX IF NOT EXISTS ""IX_communication_logs_Channel"" ON communication_logs (""Channel"");
                    CREATE INDEX IF NOT EXISTS ""IX_communication_logs_CreatedAt"" ON communication_logs (""CreatedAt"");
                    CREATE INDEX IF NOT EXISTS ""IX_communication_logs_IdempotencyKey"" ON communication_logs (""IdempotencyKey"");
                    CREATE INDEX IF NOT EXISTS ""IX_communication_logs_Status"" ON communication_logs (""Status"");
                    CREATE INDEX IF NOT EXISTS ""IX_communication_logs_TemplateId"" ON communication_logs (""TemplateId"");

                    CREATE TABLE IF NOT EXISTS workshop_pricing_tiers (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""WorkshopId"" uuid NOT NULL,
                        ""TierNumber"" integer NOT NULL,
                        ""TierName"" character varying(100) NOT NULL,
                        ""MinTickets"" integer NOT NULL,
                        ""MaxTickets"" integer,
                        ""Price"" numeric NOT NULL,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""UpdatedAt"" timestamp with time zone NOT NULL,
                        CONSTRAINT ""FK_workshop_pricing_tiers_workshops_WorkshopId"" FOREIGN KEY (""WorkshopId"") REFERENCES workshops (""Id"") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_workshop_pricing_tiers_WorkshopId_TierNumber"" ON workshop_pricing_tiers (""WorkshopId"", ""TierNumber"");

                    CREATE TABLE IF NOT EXISTS workshop_tickets (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""WorkshopBookingId"" uuid NOT NULL,
                        ""WorkshopId"" uuid NOT NULL,
                        ""UserId"" uuid NOT NULL,
                        ""PaymentTransactionId"" uuid NOT NULL,
                        ""TicketNumber"" character varying(50) NOT NULL,
                        ""QrTokenHash"" character varying(128) NOT NULL,
                        ""AttendeeName"" character varying(200) NOT NULL,
                        ""AttendeePhone"" character varying(30),
                        ""AttendeeEmail"" character varying(200),
                        ""IsPrimaryAttendee"" boolean NOT NULL DEFAULT FALSE,
                        ""Status"" integer NOT NULL,
                        ""IssuedAt"" timestamp with time zone NOT NULL,
                        ""CheckedInAt"" timestamp with time zone,
                        ""CheckedInByUserId"" uuid,
                        ""CheckInMethod"" integer,
                        ""EmailSent"" boolean NOT NULL DEFAULT FALSE,
                        ""WhatsAppSent"" boolean NOT NULL DEFAULT FALSE,
                        ""ResendCount"" integer NOT NULL DEFAULT 0,
                        ""LastResentAt"" timestamp with time zone,
                        ""AttendeeDetailsLockedAt"" timestamp with time zone,
                        CONSTRAINT ""FK_workshop_tickets_payment_transactions_PaymentTransactionId"" FOREIGN KEY (""PaymentTransactionId"") REFERENCES payment_transactions (""Id"") ON DELETE RESTRICT,
                        CONSTRAINT ""FK_workshop_tickets_users_UserId"" FOREIGN KEY (""UserId"") REFERENCES users (""Id"") ON DELETE RESTRICT,
                        CONSTRAINT ""FK_workshop_tickets_workshop_bookings_WorkshopBookingId"" FOREIGN KEY (""WorkshopBookingId"") REFERENCES workshop_bookings (""Id"") ON DELETE CASCADE,
                        CONSTRAINT ""FK_workshop_tickets_workshops_WorkshopId"" FOREIGN KEY (""WorkshopId"") REFERENCES workshops (""Id"") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_workshop_tickets_QrTokenHash"" ON workshop_tickets (""QrTokenHash"");
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_workshop_tickets_TicketNumber"" ON workshop_tickets (""TicketNumber"");
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_tickets_PaymentTransactionId"" ON workshop_tickets (""PaymentTransactionId"");
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_tickets_UserId"" ON workshop_tickets (""UserId"");
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_tickets_WorkshopBookingId"" ON workshop_tickets (""WorkshopBookingId"");
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_tickets_WorkshopId_Status"" ON workshop_tickets (""WorkshopId"", ""Status"");

                    CREATE TABLE IF NOT EXISTS workshop_attendances (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""WorkshopTicketId"" uuid NOT NULL,
                        ""WorkshopId"" uuid NOT NULL,
                        ""CheckedInByUserId"" uuid NOT NULL,
                        ""FirstCheckedInAt"" timestamp with time zone NOT NULL,
                        ""LastCheckedInAt"" timestamp with time zone,
                        ""IsCurrentlyInside"" boolean NOT NULL DEFAULT TRUE,
                        ""Method"" integer NOT NULL,
                        ""Notes"" character varying(500),
                        ""DeviceIp"" character varying(50),
                        CONSTRAINT ""FK_workshop_attendances_users_CheckedInByUserId"" FOREIGN KEY (""CheckedInByUserId"") REFERENCES users (""Id"") ON DELETE RESTRICT,
                        CONSTRAINT ""FK_workshop_attendances_workshop_tickets_WorkshopTicketId"" FOREIGN KEY (""WorkshopTicketId"") REFERENCES workshop_tickets (""Id"") ON DELETE CASCADE,
                        CONSTRAINT ""FK_workshop_attendances_workshops_WorkshopId"" FOREIGN KEY (""WorkshopId"") REFERENCES workshops (""Id"") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_workshop_attendances_WorkshopTicketId"" ON workshop_attendances (""WorkshopTicketId"");
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_attendances_CheckedInByUserId"" ON workshop_attendances (""CheckedInByUserId"");
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_attendances_WorkshopId_IsCurrentlyInside"" ON workshop_attendances (""WorkshopId"", ""IsCurrentlyInside"");

                    CREATE TABLE IF NOT EXISTS workshop_attendance_events (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""WorkshopTicketId"" uuid NOT NULL,
                        ""WorkshopId"" uuid NOT NULL,
                        ""PerformedByUserId"" uuid NOT NULL,
                        ""EventType"" integer NOT NULL,
                        ""OccurredAt"" timestamp with time zone NOT NULL,
                        ""Method"" integer NOT NULL,
                        ""Notes"" character varying(500),
                        CONSTRAINT ""FK_workshop_attendance_events_users_PerformedByUserId"" FOREIGN KEY (""PerformedByUserId"") REFERENCES users (""Id"") ON DELETE RESTRICT,
                        CONSTRAINT ""FK_workshop_attendance_events_workshop_tickets_WorkshopTicketId"" FOREIGN KEY (""WorkshopTicketId"") REFERENCES workshop_tickets (""Id"") ON DELETE CASCADE,
                        CONSTRAINT ""FK_workshop_attendance_events_workshops_WorkshopId"" FOREIGN KEY (""WorkshopId"") REFERENCES workshops (""Id"") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_attendance_events_PerformedByUserId"" ON workshop_attendance_events (""PerformedByUserId"");
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_attendance_events_WorkshopId_OccurredAt"" ON workshop_attendance_events (""WorkshopId"", ""OccurredAt"");
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_attendance_events_WorkshopTicketId"" ON workshop_attendance_events (""WorkshopTicketId"");

                    CREATE TABLE IF NOT EXISTS workshop_feedback_tokens (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""WorkshopBookingId"" uuid NOT NULL,
                        ""TokenHash"" character varying(128) NOT NULL,
                        ""ExpiresAt"" timestamp with time zone NOT NULL,
                        ""UsedAt"" timestamp with time zone,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        CONSTRAINT ""FK_workshop_feedback_tokens_workshop_bookings_WorkshopBookingId"" FOREIGN KEY (""WorkshopBookingId"") REFERENCES workshop_bookings (""Id"") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_workshop_feedback_tokens_TokenHash"" ON workshop_feedback_tokens (""TokenHash"");
                    CREATE INDEX IF NOT EXISTS ""IX_workshop_feedback_tokens_WorkshopBookingId"" ON workshop_feedback_tokens (""WorkshopBookingId"");

                    -- Column additions to pre-existing tables captured in this migration's designer
                    ALTER TABLE admin_sessions ADD COLUMN IF NOT EXISTS ""LoggedOutAt"" timestamp with time zone;
                    ALTER TABLE dance_classes ADD COLUMN IF NOT EXISTS ""ArchivedAt"" timestamp with time zone;
                    ALTER TABLE dance_classes ADD COLUMN IF NOT EXISTS ""IsArchived"" boolean NOT NULL DEFAULT FALSE;
                    ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS ""GuestEmail"" character varying(200);
                    ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS ""GuestName"" character varying(200);
                    ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS ""GuestPhone"" character varying(30);
                    ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS ""PriceBreakdownJson"" text;
                    ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS ""Quantity"" integer NOT NULL DEFAULT 1;
                    ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS ""TotalPrice"" numeric NOT NULL DEFAULT 0;
                    ALTER TABLE workshop_feedback ADD COLUMN IF NOT EXISTS ""InvalidatedAt"" timestamp with time zone;
                    ALTER TABLE workshop_feedback ADD COLUMN IF NOT EXISTS ""InvalidationReason"" character varying(500);
                    ALTER TABLE workshop_feedback ADD COLUMN IF NOT EXISTS ""IsValid"" boolean NOT NULL DEFAULT TRUE;
                    ALTER TABLE workshops ADD COLUMN IF NOT EXISTS ""AllowReEntry"" boolean NOT NULL DEFAULT TRUE;
                    ALTER TABLE workshops ADD COLUMN IF NOT EXISTS ""ReEntryCooldown"" interval;
                    ALTER TABLE workshops ADD COLUMN IF NOT EXISTS ""RequireReEntryVerification"" boolean NOT NULL DEFAULT TRUE;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
