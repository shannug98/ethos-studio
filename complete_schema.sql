CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902151225_InitialIdentity') THEN
    CREATE TABLE roles (
        "Id" uuid NOT NULL,
        "Code" character varying(50) NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Description" character varying(500),
        CONSTRAINT "PK_roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902151225_InitialIdentity') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "CustomerCode" character varying(30) NOT NULL,
        "FullName" character varying(150) NOT NULL,
        "Phone" character varying(20) NOT NULL,
        "Email" character varying(255),
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "LastLoginAt" timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902151225_InitialIdentity') THEN
    CREATE TABLE user_roles (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        "AssignedAt" timestamp with time zone NOT NULL,
        "AssignedBy" uuid,
        CONSTRAINT "PK_user_roles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_user_roles_roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES roles ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_user_roles_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902151225_InitialIdentity') THEN
    CREATE UNIQUE INDEX "IX_roles_Code" ON roles ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902151225_InitialIdentity') THEN
    CREATE INDEX "IX_user_roles_RoleId" ON user_roles ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902151225_InitialIdentity') THEN
    CREATE UNIQUE INDEX "IX_user_roles_UserId_RoleId" ON user_roles ("UserId", "RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902151225_InitialIdentity') THEN
    CREATE UNIQUE INDEX "IX_users_CustomerCode" ON users ("CustomerCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902151225_InitialIdentity') THEN
    CREATE UNIQUE INDEX "IX_users_Phone" ON users ("Phone");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902151225_InitialIdentity') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260902151225_InitialIdentity', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902153433_AddOtpVerification') THEN
    CREATE TABLE otp_verifications (
        "Id" uuid NOT NULL,
        "Phone" character varying(20) NOT NULL,
        "OtpHash" character varying(255) NOT NULL,
        "ExpiresAt" timestamptz NOT NULL,
        "IsUsed" boolean NOT NULL,
        "AttemptCount" integer NOT NULL,
        "CreatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_otp_verifications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902153433_AddOtpVerification') THEN
    CREATE INDEX "IX_otp_verifications_Phone_CreatedAt" ON otp_verifications ("Phone", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902153433_AddOtpVerification') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260902153433_AddOtpVerification', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902153805_AddOtpPurpose') THEN
    ALTER TABLE otp_verifications ADD "Purpose" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902153805_AddOtpPurpose') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260902153805_AddOtpPurpose', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902155121_SeedSystemRoles') THEN
    ALTER TABLE roles ALTER COLUMN "Description" TYPE character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902155121_SeedSystemRoles') THEN
    INSERT INTO roles ("Id", "Code", "Description", "Name")
    VALUES ('11111111-1111-1111-1111-111111111111', 'STUDENT', 'Ethos student', 'Student');
    INSERT INTO roles ("Id", "Code", "Description", "Name")
    VALUES ('22222222-2222-2222-2222-222222222222', 'TRAINER', 'Ethos trainer', 'Trainer');
    INSERT INTO roles ("Id", "Code", "Description", "Name")
    VALUES ('33333333-3333-3333-3333-333333333333', 'ADMIN', 'Ethos administrator', 'Administrator');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260902155121_SeedSystemRoles') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260902155121_SeedSystemRoles', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    CREATE TABLE student_profiles (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "DateOfBirth" text,
        "Gender" character varying(50),
        "City" character varying(100),
        "ProfilePhotoUrl" character varying(500),
        "EmergencyContactName" character varying(150),
        "EmergencyContactPhone" character varying(30),
        "Bio" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_student_profiles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_student_profiles_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    CREATE TABLE trainer_profiles (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TrainerCode" character varying(30) NOT NULL,
        "FullName" character varying(150) NOT NULL,
        "City" character varying(100),
        "ProfilePhotoUrl" character varying(500),
        "PrimaryDanceStyle" character varying(100),
        "SecondaryDanceStyles" character varying(500),
        "ExperienceYears" integer,
        "CurrentStudio" character varying(200),
        "Bio" text,
        "InstagramUrl" character varying(500),
        "YouTubeUrl" character varying(500),
        "TrainerType" integer NOT NULL,
        "Status" integer NOT NULL,
        "ApprovedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_trainer_profiles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_trainer_profiles_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    CREATE TABLE trainer_applications (
        "Id" uuid NOT NULL,
        "TrainerProfileId" uuid NOT NULL,
        "Status" integer NOT NULL,
        "ApplicationNotes" character varying(2000),
        "RejectionReason" character varying(2000),
        "SubmittedAt" timestamp with time zone,
        "ReviewedAt" timestamp with time zone,
        "ReviewedByUserId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_trainer_applications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_trainer_applications_trainer_profiles_TrainerProfileId" FOREIGN KEY ("TrainerProfileId") REFERENCES trainer_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    CREATE TABLE trainer_documents (
        "Id" uuid NOT NULL,
        "TrainerApplicationId" uuid NOT NULL,
        "DocumentType" character varying(100) NOT NULL,
        "FileUrl" character varying(1000) NOT NULL,
        "FileName" character varying(255),
        "UploadedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_trainer_documents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_trainer_documents_trainer_applications_TrainerApplicationId" FOREIGN KEY ("TrainerApplicationId") REFERENCES trainer_applications ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    CREATE UNIQUE INDEX "IX_student_profiles_UserId" ON student_profiles ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    CREATE INDEX "IX_trainer_applications_TrainerProfileId_Status" ON trainer_applications ("TrainerProfileId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    CREATE INDEX "IX_trainer_documents_TrainerApplicationId" ON trainer_documents ("TrainerApplicationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    CREATE UNIQUE INDEX "IX_trainer_profiles_TrainerCode" ON trainer_profiles ("TrainerCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    CREATE UNIQUE INDEX "IX_trainer_profiles_UserId" ON trainer_profiles ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903074345_AddUserProfilesAndTrainerApplications') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260903074345_AddUserProfilesAndTrainerApplications', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903081551_AddPackagesAndStudentPackages') THEN
    CREATE TABLE packages (
        "Id" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "Description" character varying(1000),
        "Price" numeric(18,2) NOT NULL,
        "DurationDays" integer NOT NULL,
        "ClassLimit" integer,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "IsFeatured" boolean NOT NULL DEFAULT FALSE,
        "FeaturesJson" text,
        "CreatedAt" timestamptz NOT NULL,
        "UpdatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_packages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903081551_AddPackagesAndStudentPackages') THEN
    CREATE TABLE student_packages (
        "Id" uuid NOT NULL,
        "StudentProfileId" uuid NOT NULL,
        "PackageId" uuid NOT NULL,
        "StartDate" timestamptz NOT NULL,
        "ExpiryDate" timestamptz NOT NULL,
        "Status" integer NOT NULL,
        "ClassesAllowed" integer,
        "ClassesUsed" integer NOT NULL DEFAULT 0,
        "CreatedAt" timestamptz NOT NULL,
        "UpdatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_student_packages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_student_packages_packages_PackageId" FOREIGN KEY ("PackageId") REFERENCES packages ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_student_packages_student_profiles_StudentProfileId" FOREIGN KEY ("StudentProfileId") REFERENCES student_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903081551_AddPackagesAndStudentPackages') THEN
    CREATE INDEX "IX_student_packages_PackageId" ON student_packages ("PackageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903081551_AddPackagesAndStudentPackages') THEN
    CREATE INDEX "IX_student_packages_Status" ON student_packages ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903081551_AddPackagesAndStudentPackages') THEN
    CREATE INDEX "IX_student_packages_StudentProfileId" ON student_packages ("StudentProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903081551_AddPackagesAndStudentPackages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260903081551_AddPackagesAndStudentPackages', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE TABLE dance_classes (
        "Id" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "Description" character varying(1000),
        "DanceStyle" character varying(100) NOT NULL,
        "Level" character varying(50) NOT NULL,
        "DurationMinutes" integer NOT NULL,
        "ImageUrl" character varying(500),
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamptz NOT NULL,
        "UpdatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_dance_classes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE TABLE class_enrollments (
        "Id" uuid NOT NULL,
        "StudentProfileId" uuid NOT NULL,
        "DanceClassId" uuid NOT NULL,
        "StudentPackageId" uuid,
        "EnrollmentDate" timestamptz NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAt" timestamptz NOT NULL,
        "UpdatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_class_enrollments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_class_enrollments_dance_classes_DanceClassId" FOREIGN KEY ("DanceClassId") REFERENCES dance_classes ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_class_enrollments_student_packages_StudentPackageId" FOREIGN KEY ("StudentPackageId") REFERENCES student_packages ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_class_enrollments_student_profiles_StudentProfileId" FOREIGN KEY ("StudentProfileId") REFERENCES student_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE TABLE class_schedules (
        "Id" uuid NOT NULL,
        "DanceClassId" uuid NOT NULL,
        "DayOfWeek" integer NOT NULL,
        "StartTime" interval NOT NULL,
        "EndTime" interval NOT NULL,
        "StudioRoom" character varying(100),
        "Capacity" integer NOT NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamptz NOT NULL,
        "UpdatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_class_schedules" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_class_schedules_dance_classes_DanceClassId" FOREIGN KEY ("DanceClassId") REFERENCES dance_classes ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE TABLE class_sessions (
        "Id" uuid NOT NULL,
        "ClassScheduleId" uuid NOT NULL,
        "SessionDate" timestamptz NOT NULL,
        "StartTime" interval NOT NULL,
        "EndTime" interval NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAt" timestamptz NOT NULL,
        "UpdatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_class_sessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_class_sessions_class_schedules_ClassScheduleId" FOREIGN KEY ("ClassScheduleId") REFERENCES class_schedules ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE INDEX "IX_class_enrollments_DanceClassId" ON class_enrollments ("DanceClassId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE INDEX "IX_class_enrollments_StudentPackageId" ON class_enrollments ("StudentPackageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE UNIQUE INDEX "IX_class_enrollments_StudentProfileId_DanceClassId" ON class_enrollments ("StudentProfileId", "DanceClassId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE INDEX "IX_class_schedules_DanceClassId" ON class_schedules ("DanceClassId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE INDEX "IX_class_sessions_ClassScheduleId" ON class_sessions ("ClassScheduleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    CREATE UNIQUE INDEX "IX_class_sessions_ClassScheduleId_SessionDate" ON class_sessions ("ClassScheduleId", "SessionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903083926_AddClassesAndEnrollments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260903083926_AddClassesAndEnrollments', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE TABLE attendance_records (
        "Id" uuid NOT NULL,
        "ClassSessionId" uuid NOT NULL,
        "StudentProfileId" uuid NOT NULL,
        "Status" integer NOT NULL,
        "MarkedAt" timestamptz NOT NULL,
        "MarkedByUserId" uuid,
        "Notes" character varying(500),
        CONSTRAINT "PK_attendance_records" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_attendance_records_class_sessions_ClassSessionId" FOREIGN KEY ("ClassSessionId") REFERENCES class_sessions ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_attendance_records_student_profiles_StudentProfileId" FOREIGN KEY ("StudentProfileId") REFERENCES student_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE TABLE notifications (
        "Id" uuid NOT NULL,
        "Type" integer NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Message" character varying(1000) NOT NULL,
        "Channel" integer NOT NULL,
        "CreatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_notifications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE TABLE payment_transactions (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Purpose" integer NOT NULL,
        "ReferenceId" uuid NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "Currency" character varying(10) NOT NULL DEFAULT 'INR',
        "Status" integer NOT NULL,
        "RazorpayOrderId" character varying(100),
        "RazorpayPaymentId" character varying(100),
        "RazorpaySignature" character varying(500),
        "CreatedAt" timestamptz NOT NULL,
        "PaidAt" timestamptz,
        "UpdatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_payment_transactions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_payment_transactions_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE TABLE workshops (
        "Id" uuid NOT NULL,
        "TrainerProfileId" uuid,
        "Title" character varying(200) NOT NULL,
        "Description" character varying(2000),
        "DanceStyle" character varying(100) NOT NULL,
        "Level" character varying(50) NOT NULL,
        "WorkshopDate" timestamptz NOT NULL,
        "StartTime" interval NOT NULL,
        "EndTime" interval NOT NULL,
        "Venue" character varying(300) NOT NULL,
        "Price" numeric(18,2) NOT NULL,
        "Capacity" integer NOT NULL,
        "Status" integer NOT NULL,
        "ImageUrl" character varying(500),
        "CreatedAt" timestamptz NOT NULL,
        "UpdatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_workshops" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_workshops_trainer_profiles_TrainerProfileId" FOREIGN KEY ("TrainerProfileId") REFERENCES trainer_profiles ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE TABLE notification_recipients (
        "Id" uuid NOT NULL,
        "NotificationId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "IsRead" boolean NOT NULL DEFAULT FALSE,
        "ReadAt" timestamptz,
        "CreatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_notification_recipients" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_notification_recipients_notifications_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES notifications ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_notification_recipients_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE TABLE payment_events (
        "Id" uuid NOT NULL,
        "PaymentTransactionId" uuid NOT NULL,
        "EventType" character varying(100) NOT NULL,
        "Payload" text,
        "CreatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_payment_events" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_payment_events_payment_transactions_PaymentTransactionId" FOREIGN KEY ("PaymentTransactionId") REFERENCES payment_transactions ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE TABLE workshop_bookings (
        "Id" uuid NOT NULL,
        "WorkshopId" uuid NOT NULL,
        "StudentProfileId" uuid NOT NULL,
        "PaymentTransactionId" uuid,
        "Status" integer NOT NULL,
        "BookedAt" timestamptz NOT NULL,
        "CancelledAt" timestamptz,
        CONSTRAINT "PK_workshop_bookings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_workshop_bookings_student_profiles_StudentProfileId" FOREIGN KEY ("StudentProfileId") REFERENCES student_profiles ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_workshop_bookings_workshops_WorkshopId" FOREIGN KEY ("WorkshopId") REFERENCES workshops ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE TABLE workshop_feedback (
        "Id" uuid NOT NULL,
        "WorkshopId" uuid NOT NULL,
        "StudentProfileId" uuid NOT NULL,
        "Rating" integer NOT NULL,
        "TeachingRating" integer,
        "EnergyRating" integer,
        "ContentRating" integer,
        "Comment" character varying(2000),
        "WouldRecommend" boolean NOT NULL,
        "SubmittedAt" timestamptz NOT NULL,
        "UpdatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_workshop_feedback" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_workshop_feedback_student_profiles_StudentProfileId" FOREIGN KEY ("StudentProfileId") REFERENCES student_profiles ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_workshop_feedback_workshops_WorkshopId" FOREIGN KEY ("WorkshopId") REFERENCES workshops ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE UNIQUE INDEX "IX_attendance_records_ClassSessionId_StudentProfileId" ON attendance_records ("ClassSessionId", "StudentProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_attendance_records_StudentProfileId" ON attendance_records ("StudentProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_notification_recipients_NotificationId" ON notification_recipients ("NotificationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_notification_recipients_UserId" ON notification_recipients ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_notification_recipients_UserId_IsRead" ON notification_recipients ("UserId", "IsRead");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_payment_events_PaymentTransactionId" ON payment_events ("PaymentTransactionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_payment_transactions_RazorpayOrderId" ON payment_transactions ("RazorpayOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_payment_transactions_UserId" ON payment_transactions ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_workshop_bookings_StudentProfileId" ON workshop_bookings ("StudentProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE UNIQUE INDEX "IX_workshop_bookings_WorkshopId_StudentProfileId" ON workshop_bookings ("WorkshopId", "StudentProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_workshop_feedback_StudentProfileId" ON workshop_feedback ("StudentProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE UNIQUE INDEX "IX_workshop_feedback_WorkshopId_StudentProfileId" ON workshop_feedback ("WorkshopId", "StudentProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    CREATE INDEX "IX_workshops_TrainerProfileId" ON workshops ("TrainerProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903084745_AddStudentFullDomainModel') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260903084745_AddStudentFullDomainModel', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903092644_AddPaymentTransactionToStudentPackage') THEN
    ALTER TABLE student_packages ADD "PaymentTransactionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903092644_AddPaymentTransactionToStudentPackage') THEN
    CREATE UNIQUE INDEX "IX_student_packages_PaymentTransactionId" ON student_packages ("PaymentTransactionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903092644_AddPaymentTransactionToStudentPackage') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260903092644_AddPaymentTransactionToStudentPackage', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_profiles DROP CONSTRAINT "FK_trainer_profiles_users_UserId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    DROP INDEX "IX_trainer_applications_TrainerProfileId_Status";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_profiles ALTER COLUMN "ProfilePhotoUrl" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_profiles ALTER COLUMN "CurrentStudio" TYPE character varying(150);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_profiles ALTER COLUMN "Bio" TYPE character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_profiles ADD "CurrentTierId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_profiles ADD "UserId1" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_documents ADD "ContentType" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_documents ADD "FileSizeBytes" bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_applications ALTER COLUMN "ApplicationNotes" TYPE character varying(3000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_applications ADD "AdminNotes" character varying(3000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_applications ADD "PaymentTransactionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_applications ADD "PaymentVerifiedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE TABLE admin_actions (
        "Id" uuid NOT NULL,
        "AdminUserId" uuid NOT NULL,
        "ActionType" character varying(100) NOT NULL,
        "EntityType" character varying(100) NOT NULL,
        "EntityId" uuid NOT NULL,
        "Reason" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_admin_actions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE TABLE permissions (
        "Id" uuid NOT NULL,
        "Code" character varying(100) NOT NULL,
        "Name" character varying(150) NOT NULL,
        "Description" character varying(500),
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_permissions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE TABLE trainer_availability (
        "Id" uuid NOT NULL,
        "TrainerProfileId" uuid NOT NULL,
        "DayOfWeek" integer NOT NULL,
        "StartTime" interval NOT NULL,
        "EndTime" interval NOT NULL,
        "IsAvailable" boolean NOT NULL,
        CONSTRAINT "PK_trainer_availability" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_trainer_availability_trainer_profiles_TrainerProfileId" FOREIGN KEY ("TrainerProfileId") REFERENCES trainer_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE TABLE trainer_performance_snapshots (
        "Id" uuid NOT NULL,
        "TrainerProfileId" uuid NOT NULL,
        "SnapshotDate" timestamp with time zone NOT NULL,
        "SessionsConducted" integer NOT NULL,
        "UniqueStudents" integer NOT NULL,
        "AttendanceCount" integer NOT NULL,
        "AttendancePercentage" numeric(5,2) NOT NULL,
        "FeedbackCount" integer NOT NULL,
        "AverageFeedbackRating" numeric(4,2),
        "WorkshopsConducted" integer NOT NULL,
        "Notes" character varying(2000),
        CONSTRAINT "PK_trainer_performance_snapshots" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_trainer_performance_snapshots_trainer_profiles_TrainerProfi~" FOREIGN KEY ("TrainerProfileId") REFERENCES trainer_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE TABLE trainer_tiers (
        "Id" uuid NOT NULL,
        "Code" character varying(30) NOT NULL,
        "Name" character varying(100) NOT NULL,
        "DisplayOrder" integer NOT NULL,
        "PassPrice" numeric(18,2),
        "Description" character varying(1000),
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_trainer_tiers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE TABLE trainer_permission_overrides (
        "Id" uuid NOT NULL,
        "TrainerProfileId" uuid NOT NULL,
        "PermissionId" uuid NOT NULL,
        "IsAllowed" boolean NOT NULL,
        "Reason" character varying(1000),
        "CreatedByUserId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_trainer_permission_overrides" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_trainer_permission_overrides_permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES permissions ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_trainer_permission_overrides_trainer_profiles_TrainerProfil~" FOREIGN KEY ("TrainerProfileId") REFERENCES trainer_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE TABLE trainer_tier_history (
        "Id" uuid NOT NULL,
        "TrainerProfileId" uuid NOT NULL,
        "PreviousTierId" uuid,
        "NewTierId" uuid NOT NULL,
        "Reason" character varying(1000),
        "ChangedByUserId" uuid,
        "ChangedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_trainer_tier_history" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_trainer_tier_history_trainer_profiles_TrainerProfileId" FOREIGN KEY ("TrainerProfileId") REFERENCES trainer_profiles ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_trainer_tier_history_trainer_tiers_NewTierId" FOREIGN KEY ("NewTierId") REFERENCES trainer_tiers ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_trainer_tier_history_trainer_tiers_PreviousTierId" FOREIGN KEY ("PreviousTierId") REFERENCES trainer_tiers ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE TABLE trainer_tier_permissions (
        "TrainerTierId" uuid NOT NULL,
        "PermissionId" uuid NOT NULL,
        "IsAllowed" boolean NOT NULL,
        CONSTRAINT "PK_trainer_tier_permissions" PRIMARY KEY ("TrainerTierId", "PermissionId"),
        CONSTRAINT "FK_trainer_tier_permissions_permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES permissions ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_trainer_tier_permissions_trainer_tiers_TrainerTierId" FOREIGN KEY ("TrainerTierId") REFERENCES trainer_tiers ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE TABLE trainer_upgrade_requests (
        "Id" uuid NOT NULL,
        "TrainerProfileId" uuid NOT NULL,
        "CurrentTierId" uuid NOT NULL,
        "RequestedTierId" uuid NOT NULL,
        "Status" integer NOT NULL,
        "Reason" character varying(2000),
        "AdminNotes" character varying(2000),
        "ReviewedByUserId" uuid,
        "ReviewedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_trainer_upgrade_requests" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_trainer_upgrade_requests_trainer_profiles_TrainerProfileId" FOREIGN KEY ("TrainerProfileId") REFERENCES trainer_profiles ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_trainer_upgrade_requests_trainer_tiers_CurrentTierId" FOREIGN KEY ("CurrentTierId") REFERENCES trainer_tiers ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_trainer_upgrade_requests_trainer_tiers_RequestedTierId" FOREIGN KEY ("RequestedTierId") REFERENCES trainer_tiers ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_profiles_CurrentTierId" ON trainer_profiles ("CurrentTierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE UNIQUE INDEX "IX_trainer_profiles_UserId1" ON trainer_profiles ("UserId1");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE UNIQUE INDEX "IX_trainer_applications_PaymentTransactionId" ON trainer_applications ("PaymentTransactionId") WHERE "PaymentTransactionId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_applications_TrainerProfileId" ON trainer_applications ("TrainerProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_admin_actions_AdminUserId" ON admin_actions ("AdminUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_admin_actions_EntityType_EntityId" ON admin_actions ("EntityType", "EntityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE UNIQUE INDEX "IX_permissions_Code" ON permissions ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE UNIQUE INDEX "IX_trainer_availability_TrainerProfileId_DayOfWeek_StartTime_E~" ON trainer_availability ("TrainerProfileId", "DayOfWeek", "StartTime", "EndTime");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE UNIQUE INDEX "IX_trainer_performance_snapshots_TrainerProfileId_SnapshotDate" ON trainer_performance_snapshots ("TrainerProfileId", "SnapshotDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_permission_overrides_PermissionId" ON trainer_permission_overrides ("PermissionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE UNIQUE INDEX "IX_trainer_permission_overrides_TrainerProfileId_PermissionId" ON trainer_permission_overrides ("TrainerProfileId", "PermissionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_tier_history_NewTierId" ON trainer_tier_history ("NewTierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_tier_history_PreviousTierId" ON trainer_tier_history ("PreviousTierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_tier_history_TrainerProfileId" ON trainer_tier_history ("TrainerProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_tier_permissions_PermissionId" ON trainer_tier_permissions ("PermissionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE UNIQUE INDEX "IX_trainer_tiers_Code" ON trainer_tiers ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE UNIQUE INDEX "IX_trainer_tiers_DisplayOrder" ON trainer_tiers ("DisplayOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_upgrade_requests_CurrentTierId" ON trainer_upgrade_requests ("CurrentTierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_upgrade_requests_RequestedTierId" ON trainer_upgrade_requests ("RequestedTierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    CREATE INDEX "IX_trainer_upgrade_requests_TrainerProfileId_Status" ON trainer_upgrade_requests ("TrainerProfileId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_applications ADD CONSTRAINT "FK_trainer_applications_payment_transactions_PaymentTransactio~" FOREIGN KEY ("PaymentTransactionId") REFERENCES payment_transactions ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_profiles ADD CONSTRAINT "FK_trainer_profiles_trainer_tiers_CurrentTierId" FOREIGN KEY ("CurrentTierId") REFERENCES trainer_tiers ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_profiles ADD CONSTRAINT "FK_trainer_profiles_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    ALTER TABLE trainer_profiles ADD CONSTRAINT "FK_trainer_profiles_users_UserId1" FOREIGN KEY ("UserId1") REFERENCES users ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903101211_TrainerBackend') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260903101211_TrainerBackend', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE trainer_profiles DROP CONSTRAINT "FK_trainer_profiles_users_UserId1";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    DROP INDEX "IX_trainer_profiles_UserId1";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE trainer_profiles DROP COLUMN "TrainerType";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE trainer_profiles DROP COLUMN "UserId1";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE trainer_tiers RENAME COLUMN "PassPrice" TO "UpgradeFee";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE workshops ADD "AdminApprovedPrice" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE workshops ADD "PriceApprovedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE workshops ADD "PriceApprovedByUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE workshops ADD "TrainerProposedPrice" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE trainer_tiers ADD "ApplicationFee" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE trainer_permission_overrides ADD "TrainerProfileId1" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    CREATE INDEX "IX_trainer_permission_overrides_TrainerProfileId1" ON trainer_permission_overrides ("TrainerProfileId1");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    ALTER TABLE trainer_permission_overrides ADD CONSTRAINT "FK_trainer_permission_overrides_trainer_profiles_TrainerProfi~1" FOREIGN KEY ("TrainerProfileId1") REFERENCES trainer_profiles ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260903102922_TrainerBusinessRules') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260903102922_TrainerBusinessRules', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905112536_AddTrainerPasswordAuthentication') THEN
    ALTER TABLE users ADD "MustChangePassword" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905112536_AddTrainerPasswordAuthentication') THEN
    ALTER TABLE users ADD "PasswordHash" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260905112536_AddTrainerPasswordAuthentication') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260905112536_AddTrainerPasswordAuthentication', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906210728_AddTrainerApplicationVideo') THEN
    CREATE TABLE trainer_application_videos (
        "Id" uuid NOT NULL,
        "TrainerApplicationId" uuid NOT NULL,
        "FileName" character varying(255) NOT NULL,
        "StoragePath" character varying(500) NOT NULL,
        "ContentType" character varying(100) NOT NULL,
        "FileSizeBytes" bigint NOT NULL,
        "DurationSeconds" integer NOT NULL,
        "UploadedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_trainer_application_videos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_trainer_application_videos_trainer_applications_TrainerAppl~" FOREIGN KEY ("TrainerApplicationId") REFERENCES trainer_applications ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906210728_AddTrainerApplicationVideo') THEN
    CREATE UNIQUE INDEX "IX_trainer_application_videos_TrainerApplicationId" ON trainer_application_videos ("TrainerApplicationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906210728_AddTrainerApplicationVideo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906210728_AddTrainerApplicationVideo', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906234053_AddTrainerGallery') THEN
    CREATE TABLE "TrainerGalleryImages" (
        "Id" uuid NOT NULL,
        "TrainerProfileId" uuid NOT NULL,
        "FileName" character varying(255) NOT NULL,
        "StoragePath" character varying(500) NOT NULL,
        "ContentType" character varying(100) NOT NULL,
        "FileSizeBytes" bigint NOT NULL,
        "DisplayOrder" integer NOT NULL,
        "UploadedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TrainerGalleryImages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TrainerGalleryImages_trainer_profiles_TrainerProfileId" FOREIGN KEY ("TrainerProfileId") REFERENCES trainer_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906234053_AddTrainerGallery') THEN
    CREATE INDEX "IX_TrainerGalleryImages_TrainerProfileId_DisplayOrder" ON "TrainerGalleryImages" ("TrainerProfileId", "DisplayOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906234053_AddTrainerGallery') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906234053_AddTrainerGallery', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ALTER COLUMN "UpdatedAt" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "DemonstrationRating" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "DurationRating" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "ExplanationClarity" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "Improvements" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "InteractionRating" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "OrganizationRating" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "ValueForMoney" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "VenueRating" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "WorkshopBookingId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD "WouldAttendTrainerAgain" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    CREATE TABLE class_feedback (
        "Id" uuid NOT NULL,
        "ClassEnrollmentId" uuid NOT NULL,
        "DanceClassId" uuid NOT NULL,
        "StudentProfileId" uuid NOT NULL,
        "OverallRating" integer NOT NULL,
        "TeachingQuality" integer NOT NULL,
        "ExplanationClarity" integer NOT NULL,
        "TrainerEngagement" integer NOT NULL,
        "ClassPace" integer NOT NULL,
        "ChoreographyContent" integer NOT NULL,
        "DifficultyLevel" integer NOT NULL,
        "ClassExperience" integer NOT NULL,
        "LikedAspects" character varying(2000),
        "Improvements" character varying(2000),
        "WouldAttendAgain" character varying(50) NOT NULL,
        "SubmittedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_class_feedback" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_class_feedback_class_enrollments_ClassEnrollmentId" FOREIGN KEY ("ClassEnrollmentId") REFERENCES class_enrollments ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_class_feedback_dance_classes_DanceClassId" FOREIGN KEY ("DanceClassId") REFERENCES dance_classes ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_class_feedback_student_profiles_StudentProfileId" FOREIGN KEY ("StudentProfileId") REFERENCES student_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    CREATE INDEX "IX_workshop_feedback_WorkshopBookingId" ON workshop_feedback ("WorkshopBookingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    CREATE UNIQUE INDEX "IX_class_feedback_ClassEnrollmentId" ON class_feedback ("ClassEnrollmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    CREATE INDEX "IX_class_feedback_DanceClassId" ON class_feedback ("DanceClassId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    CREATE INDEX "IX_class_feedback_StudentProfileId" ON class_feedback ("StudentProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    ALTER TABLE workshop_feedback ADD CONSTRAINT "FK_workshop_feedback_workshop_bookings_WorkshopBookingId" FOREIGN KEY ("WorkshopBookingId") REFERENCES workshop_bookings ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907221940_AddStudentHistoryAndFeedback') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260907221940_AddStudentHistoryAndFeedback', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907224214_AddNotificationEventKeyAndSoftDelete') THEN
    ALTER TABLE notifications ADD "ActionUrl" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907224214_AddNotificationEventKeyAndSoftDelete') THEN
    ALTER TABLE notifications ADD "EventKey" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907224214_AddNotificationEventKeyAndSoftDelete') THEN
    ALTER TABLE notification_recipients ADD "DeletedAt" timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907224214_AddNotificationEventKeyAndSoftDelete') THEN
    CREATE UNIQUE INDEX "IX_notifications_EventKey" ON notifications ("EventKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907224214_AddNotificationEventKeyAndSoftDelete') THEN
    CREATE INDEX "IX_notification_recipients_UserId_DeletedAt" ON notification_recipients ("UserId", "DeletedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260907224214_AddNotificationEventKeyAndSoftDelete') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260907224214_AddNotificationEventKeyAndSoftDelete', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260909220926_AddAdminSecurityEvents') THEN
    CREATE TABLE security_events (
        "Id" uuid NOT NULL,
        "EventType" character varying(100) NOT NULL,
        "Severity" character varying(20) NOT NULL,
        "IpAddress" character varying(100),
        "UserAgent" character varying(500),
        "UserId" uuid,
        "MaskedPhone" character varying(20),
        "DetailsJson" character varying(4000),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_security_events" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260909220926_AddAdminSecurityEvents') THEN
    CREATE INDEX "IX_security_events_CreatedAt" ON security_events ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260909220926_AddAdminSecurityEvents') THEN
    CREATE INDEX "IX_security_events_EventType" ON security_events ("EventType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260909220926_AddAdminSecurityEvents') THEN
    CREATE INDEX "IX_security_events_UserId" ON security_events ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260909220926_AddAdminSecurityEvents') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260909220926_AddAdminSecurityEvents', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    ALTER TABLE security_events ALTER COLUMN "MaskedPhone" TYPE character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    CREATE TABLE admin_devices (
        "Id" uuid NOT NULL,
        "AdminUserId" uuid NOT NULL,
        "DeviceCredentialHash" character varying(64) NOT NULL,
        "DeviceName" character varying(100) NOT NULL,
        "Status" integer NOT NULL,
        "RegisteredAt" timestamp with time zone NOT NULL,
        "LastSeenAt" timestamp with time zone NOT NULL,
        "LastSeenIp" character varying(100),
        "UserAgent" character varying(500),
        "FingerprintTelemetry" character varying(2000),
        "RevokedAt" timestamp with time zone,
        CONSTRAINT "PK_admin_devices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_admin_devices_users_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    CREATE TABLE admin_sessions (
        "Id" uuid NOT NULL,
        "AdminDeviceId" uuid NOT NULL,
        "AdminUserId" uuid NOT NULL,
        "SessionTokenHash" character varying(64) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "LastSeenAt" timestamp with time zone NOT NULL,
        "RevokedAt" timestamp with time zone,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_admin_sessions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_admin_sessions_admin_devices_AdminDeviceId" FOREIGN KEY ("AdminDeviceId") REFERENCES admin_devices ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_admin_sessions_users_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    CREATE INDEX "IX_admin_devices_AdminUserId_Status" ON admin_devices ("AdminUserId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    CREATE UNIQUE INDEX "IX_admin_devices_DeviceCredentialHash" ON admin_devices ("DeviceCredentialHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    CREATE INDEX "IX_admin_devices_Status_RevokedAt" ON admin_devices ("Status", "RevokedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    CREATE INDEX "IX_admin_sessions_AdminDeviceId_IsActive" ON admin_sessions ("AdminDeviceId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    CREATE INDEX "IX_admin_sessions_AdminUserId_IsActive" ON admin_sessions ("AdminUserId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    CREATE UNIQUE INDEX "IX_admin_sessions_SessionTokenHash" ON admin_sessions ("SessionTokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    ALTER TABLE security_events ADD CONSTRAINT "FK_security_events_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910110309_AddAdminDevicesAndSessions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260910110309_AddAdminDevicesAndSessions', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE security_events ADD "AdminDeviceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE security_events ADD "AdminSessionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE security_events ADD "TraceId" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "AdminDeviceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "AdminSessionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "Category" character varying(50) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "IpAddress" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "MetadataJson" character varying(4000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "OutcomeCode" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "RequestId" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "Success" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "TraceId" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD "UserAgent" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    CREATE INDEX "IX_security_events_AdminDeviceId" ON security_events ("AdminDeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    CREATE INDEX "IX_security_events_AdminSessionId" ON security_events ("AdminSessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    CREATE INDEX "IX_security_events_TraceId" ON security_events ("TraceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    CREATE INDEX "IX_admin_actions_AdminDeviceId" ON admin_actions ("AdminDeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    CREATE INDEX "IX_admin_actions_AdminSessionId" ON admin_actions ("AdminSessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    CREATE INDEX "IX_admin_actions_Category" ON admin_actions ("Category");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    CREATE INDEX "IX_admin_actions_CreatedAt" ON admin_actions ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    CREATE INDEX "IX_admin_actions_TraceId" ON admin_actions ("TraceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD CONSTRAINT "FK_admin_actions_admin_devices_AdminDeviceId" FOREIGN KEY ("AdminDeviceId") REFERENCES admin_devices ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD CONSTRAINT "FK_admin_actions_admin_sessions_AdminSessionId" FOREIGN KEY ("AdminSessionId") REFERENCES admin_sessions ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE admin_actions ADD CONSTRAINT "FK_admin_actions_users_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES users ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE security_events ADD CONSTRAINT "FK_security_events_admin_devices_AdminDeviceId" FOREIGN KEY ("AdminDeviceId") REFERENCES admin_devices ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    ALTER TABLE security_events ADD CONSTRAINT "FK_security_events_admin_sessions_AdminSessionId" FOREIGN KEY ("AdminSessionId") REFERENCES admin_sessions ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260910114623_AddAdminAuditAndSecurityInfrastructure') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260910114623_AddAdminAuditAndSecurityInfrastructure', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915115622_RemoveTrainerDocuments') THEN
    DROP TABLE IF EXISTS trainer_documents CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915115622_RemoveTrainerDocuments') THEN

                        CREATE TABLE IF NOT EXISTS api_request_logs (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "TraceId" character varying(100) NOT NULL,
                            "CorrelationId" character varying(100),
                            "Method" character varying(10) NOT NULL,
                            "Path" character varying(500) NOT NULL,
                            "StatusCode" integer NOT NULL,
                            "DurationMs" bigint NOT NULL,
                            "IpAddress" character varying(100),
                            "UserAgent" character varying(500),
                            "UserId" uuid,
                            "Role" character varying(50),
                            "ErrorMessage" character varying(2000),
                            "CreatedAt" timestamp with time zone NOT NULL,
                            CONSTRAINT "FK_api_request_logs_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE SET NULL
                        );
                        CREATE INDEX IF NOT EXISTS "IX_api_request_logs_CreatedAt" ON api_request_logs ("CreatedAt");
                        CREATE INDEX IF NOT EXISTS "IX_api_request_logs_Path" ON api_request_logs ("Path");
                        CREATE INDEX IF NOT EXISTS "IX_api_request_logs_StatusCode" ON api_request_logs ("StatusCode");
                        CREATE INDEX IF NOT EXISTS "IX_api_request_logs_TraceId" ON api_request_logs ("TraceId");
                        CREATE INDEX IF NOT EXISTS "IX_api_request_logs_UserId" ON api_request_logs ("UserId");

                        CREATE TABLE IF NOT EXISTS incidents (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "IncidentNumber" character varying(50) NOT NULL,
                            "Title" character varying(200) NOT NULL,
                            "Description" character varying(4000) NOT NULL,
                            "Severity" character varying(20) NOT NULL,
                            "Status" character varying(30) NOT NULL,
                            "AffectedService" character varying(50) NOT NULL,
                            "AssignedAdminId" uuid,
                            "TraceId" character varying(100),
                            "EvidenceJson" character varying(8000),
                            "RootCause" character varying(4000),
                            "ResolutionNotes" character varying(4000),
                            "CreatedAt" timestamp with time zone NOT NULL,
                            "UpdatedAt" timestamp with time zone NOT NULL,
                            "ResolvedAt" timestamp with time zone,
                            CONSTRAINT "FK_incidents_users_AssignedAdminId" FOREIGN KEY ("AssignedAdminId") REFERENCES users ("Id") ON DELETE SET NULL
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_incidents_IncidentNumber" ON incidents ("IncidentNumber");
                        CREATE INDEX IF NOT EXISTS "IX_incidents_AssignedAdminId" ON incidents ("AssignedAdminId");
                        CREATE INDEX IF NOT EXISTS "IX_incidents_CreatedAt" ON incidents ("CreatedAt");
                        CREATE INDEX IF NOT EXISTS "IX_incidents_Severity" ON incidents ("Severity");
                        CREATE INDEX IF NOT EXISTS "IX_incidents_Status" ON incidents ("Status");
                        CREATE INDEX IF NOT EXISTS "IX_incidents_TraceId" ON incidents ("TraceId");

                        CREATE TABLE IF NOT EXISTS incident_updates (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "IncidentId" uuid NOT NULL,
                            "AdminUserId" uuid NOT NULL,
                            "PreviousStatus" character varying(30),
                            "NewStatus" character varying(30),
                            "Message" character varying(2000) NOT NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            CONSTRAINT "FK_incident_updates_incidents_IncidentId" FOREIGN KEY ("IncidentId") REFERENCES incidents ("Id") ON DELETE CASCADE,
                            CONSTRAINT "FK_incident_updates_users_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES users ("Id") ON DELETE RESTRICT
                        );
                        CREATE INDEX IF NOT EXISTS "IX_incident_updates_AdminUserId" ON incident_updates ("AdminUserId");
                        CREATE INDEX IF NOT EXISTS "IX_incident_updates_CreatedAt" ON incident_updates ("CreatedAt");
                        CREATE INDEX IF NOT EXISTS "IX_incident_updates_IncidentId" ON incident_updates ("IncidentId");

                        CREATE TABLE IF NOT EXISTS corrective_actions (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "ActionNumber" character varying(50) NOT NULL,
                            "ActionType" character varying(60) NOT NULL,
                            "TargetEntityType" character varying(40) NOT NULL,
                            "TargetEntityId" uuid NOT NULL,
                            "IncidentId" uuid,
                            "TraceId" character varying(100) NOT NULL,
                            "AdminUserId" uuid NOT NULL,
                            "Status" character varying(30) NOT NULL,
                            "IdempotencyKey" character varying(128),
                            "PreconditionHash" character varying(128),
                            "ParametersJson" character varying(8000),
                            "BeforeStateJson" character varying(8000),
                            "AfterStateJson" character varying(8000),
                            "Justification" character varying(2000) NOT NULL,
                            "ExecutionLog" character varying(8000),
                            "ExecutedAt" timestamp with time zone,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            CONSTRAINT "FK_corrective_actions_incidents_IncidentId" FOREIGN KEY ("IncidentId") REFERENCES incidents ("Id") ON DELETE SET NULL,
                            CONSTRAINT "FK_corrective_actions_users_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES users ("Id") ON DELETE RESTRICT
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_corrective_actions_ActionNumber" ON corrective_actions ("ActionNumber");
                        CREATE INDEX IF NOT EXISTS "IX_corrective_actions_ActionType" ON corrective_actions ("ActionType");
                        CREATE INDEX IF NOT EXISTS "IX_corrective_actions_AdminUserId" ON corrective_actions ("AdminUserId");
                        CREATE INDEX IF NOT EXISTS "IX_corrective_actions_CreatedAt" ON corrective_actions ("CreatedAt");
                        CREATE INDEX IF NOT EXISTS "IX_corrective_actions_IdempotencyKey" ON corrective_actions ("IdempotencyKey");
                        CREATE INDEX IF NOT EXISTS "IX_corrective_actions_IncidentId" ON corrective_actions ("IncidentId");
                        CREATE INDEX IF NOT EXISTS "IX_corrective_actions_Status" ON corrective_actions ("Status");
                        CREATE INDEX IF NOT EXISTS "IX_corrective_actions_TargetEntityId" ON corrective_actions ("TargetEntityId");

                        CREATE TABLE IF NOT EXISTS communication_logs (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "MessageReference" character varying(50) NOT NULL,
                            "Channel" character varying(30) NOT NULL,
                            "Recipient" character varying(100) NOT NULL,
                            "RecipientUserId" uuid,
                            "TemplateId" character varying(100) NOT NULL,
                            "Subject" character varying(200),
                            "BodyPreview" character varying(2000) NOT NULL,
                            "Status" character varying(30) NOT NULL,
                            "Provider" character varying(50) NOT NULL,
                            "ProviderMessageId" character varying(128),
                            "ErrorMessage" character varying(2000),
                            "RetryCount" integer NOT NULL DEFAULT 0,
                            "IdempotencyKey" character varying(128),
                            "Justification" character varying(1000),
                            "TraceId" character varying(100) NOT NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            "DeliveredAt" timestamp with time zone,
                            CONSTRAINT "FK_communication_logs_users_RecipientUserId" FOREIGN KEY ("RecipientUserId") REFERENCES users ("Id") ON DELETE SET NULL
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_communication_logs_MessageReference" ON communication_logs ("MessageReference");
                        CREATE INDEX IF NOT EXISTS "IX_communication_logs_Channel" ON communication_logs ("Channel");
                        CREATE INDEX IF NOT EXISTS "IX_communication_logs_CreatedAt" ON communication_logs ("CreatedAt");
                        CREATE INDEX IF NOT EXISTS "IX_communication_logs_IdempotencyKey" ON communication_logs ("IdempotencyKey");
                        CREATE INDEX IF NOT EXISTS "IX_communication_logs_Status" ON communication_logs ("Status");
                        CREATE INDEX IF NOT EXISTS "IX_communication_logs_TemplateId" ON communication_logs ("TemplateId");

                        CREATE TABLE IF NOT EXISTS workshop_pricing_tiers (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "WorkshopId" uuid NOT NULL,
                            "TierNumber" integer NOT NULL,
                            "TierName" character varying(100) NOT NULL,
                            "MinTickets" integer NOT NULL,
                            "MaxTickets" integer,
                            "Price" numeric NOT NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            "UpdatedAt" timestamp with time zone NOT NULL,
                            CONSTRAINT "FK_workshop_pricing_tiers_workshops_WorkshopId" FOREIGN KEY ("WorkshopId") REFERENCES workshops ("Id") ON DELETE CASCADE
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_workshop_pricing_tiers_WorkshopId_TierNumber" ON workshop_pricing_tiers ("WorkshopId", "TierNumber");

                        CREATE TABLE IF NOT EXISTS workshop_tickets (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "WorkshopBookingId" uuid NOT NULL,
                            "WorkshopId" uuid NOT NULL,
                            "UserId" uuid NOT NULL,
                            "PaymentTransactionId" uuid NOT NULL,
                            "TicketNumber" character varying(50) NOT NULL,
                            "QrTokenHash" character varying(128) NOT NULL,
                            "AttendeeName" character varying(200) NOT NULL,
                            "AttendeePhone" character varying(30),
                            "AttendeeEmail" character varying(200),
                            "IsPrimaryAttendee" boolean NOT NULL DEFAULT FALSE,
                            "Status" integer NOT NULL,
                            "IssuedAt" timestamp with time zone NOT NULL,
                            "CheckedInAt" timestamp with time zone,
                            "CheckedInByUserId" uuid,
                            "CheckInMethod" integer,
                            "EmailSent" boolean NOT NULL DEFAULT FALSE,
                            "WhatsAppSent" boolean NOT NULL DEFAULT FALSE,
                            "ResendCount" integer NOT NULL DEFAULT 0,
                            "LastResentAt" timestamp with time zone,
                            "AttendeeDetailsLockedAt" timestamp with time zone,
                            CONSTRAINT "FK_workshop_tickets_payment_transactions_PaymentTransactionId" FOREIGN KEY ("PaymentTransactionId") REFERENCES payment_transactions ("Id") ON DELETE RESTRICT,
                            CONSTRAINT "FK_workshop_tickets_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE RESTRICT,
                            CONSTRAINT "FK_workshop_tickets_workshop_bookings_WorkshopBookingId" FOREIGN KEY ("WorkshopBookingId") REFERENCES workshop_bookings ("Id") ON DELETE CASCADE,
                            CONSTRAINT "FK_workshop_tickets_workshops_WorkshopId" FOREIGN KEY ("WorkshopId") REFERENCES workshops ("Id") ON DELETE CASCADE
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_workshop_tickets_QrTokenHash" ON workshop_tickets ("QrTokenHash");
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_workshop_tickets_TicketNumber" ON workshop_tickets ("TicketNumber");
                        CREATE INDEX IF NOT EXISTS "IX_workshop_tickets_PaymentTransactionId" ON workshop_tickets ("PaymentTransactionId");
                        CREATE INDEX IF NOT EXISTS "IX_workshop_tickets_UserId" ON workshop_tickets ("UserId");
                        CREATE INDEX IF NOT EXISTS "IX_workshop_tickets_WorkshopBookingId" ON workshop_tickets ("WorkshopBookingId");
                        CREATE INDEX IF NOT EXISTS "IX_workshop_tickets_WorkshopId_Status" ON workshop_tickets ("WorkshopId", "Status");

                        CREATE TABLE IF NOT EXISTS workshop_attendances (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "WorkshopTicketId" uuid NOT NULL,
                            "WorkshopId" uuid NOT NULL,
                            "CheckedInByUserId" uuid NOT NULL,
                            "FirstCheckedInAt" timestamp with time zone NOT NULL,
                            "LastCheckedInAt" timestamp with time zone,
                            "IsCurrentlyInside" boolean NOT NULL DEFAULT TRUE,
                            "Method" integer NOT NULL,
                            "Notes" character varying(500),
                            "DeviceIp" character varying(50),
                            CONSTRAINT "FK_workshop_attendances_users_CheckedInByUserId" FOREIGN KEY ("CheckedInByUserId") REFERENCES users ("Id") ON DELETE RESTRICT,
                            CONSTRAINT "FK_workshop_attendances_workshop_tickets_WorkshopTicketId" FOREIGN KEY ("WorkshopTicketId") REFERENCES workshop_tickets ("Id") ON DELETE CASCADE,
                            CONSTRAINT "FK_workshop_attendances_workshops_WorkshopId" FOREIGN KEY ("WorkshopId") REFERENCES workshops ("Id") ON DELETE CASCADE
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_workshop_attendances_WorkshopTicketId" ON workshop_attendances ("WorkshopTicketId");
                        CREATE INDEX IF NOT EXISTS "IX_workshop_attendances_CheckedInByUserId" ON workshop_attendances ("CheckedInByUserId");
                        CREATE INDEX IF NOT EXISTS "IX_workshop_attendances_WorkshopId_IsCurrentlyInside" ON workshop_attendances ("WorkshopId", "IsCurrentlyInside");

                        CREATE TABLE IF NOT EXISTS workshop_attendance_events (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "WorkshopTicketId" uuid NOT NULL,
                            "WorkshopId" uuid NOT NULL,
                            "PerformedByUserId" uuid NOT NULL,
                            "EventType" integer NOT NULL,
                            "OccurredAt" timestamp with time zone NOT NULL,
                            "Method" integer NOT NULL,
                            "Notes" character varying(500),
                            CONSTRAINT "FK_workshop_attendance_events_users_PerformedByUserId" FOREIGN KEY ("PerformedByUserId") REFERENCES users ("Id") ON DELETE RESTRICT,
                            CONSTRAINT "FK_workshop_attendance_events_workshop_tickets_WorkshopTicketId" FOREIGN KEY ("WorkshopTicketId") REFERENCES workshop_tickets ("Id") ON DELETE CASCADE,
                            CONSTRAINT "FK_workshop_attendance_events_workshops_WorkshopId" FOREIGN KEY ("WorkshopId") REFERENCES workshops ("Id") ON DELETE CASCADE
                        );
                        CREATE INDEX IF NOT EXISTS "IX_workshop_attendance_events_PerformedByUserId" ON workshop_attendance_events ("PerformedByUserId");
                        CREATE INDEX IF NOT EXISTS "IX_workshop_attendance_events_WorkshopId_OccurredAt" ON workshop_attendance_events ("WorkshopId", "OccurredAt");
                        CREATE INDEX IF NOT EXISTS "IX_workshop_attendance_events_WorkshopTicketId" ON workshop_attendance_events ("WorkshopTicketId");

                        CREATE TABLE IF NOT EXISTS workshop_feedback_tokens (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "WorkshopBookingId" uuid NOT NULL,
                            "TokenHash" character varying(128) NOT NULL,
                            "ExpiresAt" timestamp with time zone NOT NULL,
                            "UsedAt" timestamp with time zone,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            CONSTRAINT "FK_workshop_feedback_tokens_workshop_bookings_WorkshopBookingId" FOREIGN KEY ("WorkshopBookingId") REFERENCES workshop_bookings ("Id") ON DELETE CASCADE
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_workshop_feedback_tokens_TokenHash" ON workshop_feedback_tokens ("TokenHash");
                        CREATE INDEX IF NOT EXISTS "IX_workshop_feedback_tokens_WorkshopBookingId" ON workshop_feedback_tokens ("WorkshopBookingId");

                        -- Column additions to pre-existing tables captured in this migration's designer
                        ALTER TABLE admin_sessions ADD COLUMN IF NOT EXISTS "LoggedOutAt" timestamp with time zone;
                        ALTER TABLE dance_classes ADD COLUMN IF NOT EXISTS "ArchivedAt" timestamp with time zone;
                        ALTER TABLE dance_classes ADD COLUMN IF NOT EXISTS "IsArchived" boolean NOT NULL DEFAULT FALSE;
                        ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS "GuestEmail" character varying(200);
                        ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS "GuestName" character varying(200);
                        ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS "GuestPhone" character varying(30);
                        ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS "PriceBreakdownJson" text;
                        ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS "Quantity" integer NOT NULL DEFAULT 1;
                        ALTER TABLE workshop_bookings ADD COLUMN IF NOT EXISTS "TotalPrice" numeric NOT NULL DEFAULT 0;
                        ALTER TABLE workshop_feedback ADD COLUMN IF NOT EXISTS "InvalidatedAt" timestamp with time zone;
                        ALTER TABLE workshop_feedback ADD COLUMN IF NOT EXISTS "InvalidationReason" character varying(500);
                        ALTER TABLE workshop_feedback ADD COLUMN IF NOT EXISTS "IsValid" boolean NOT NULL DEFAULT TRUE;
                        ALTER TABLE workshops ADD COLUMN IF NOT EXISTS "AllowReEntry" boolean NOT NULL DEFAULT TRUE;
                        ALTER TABLE workshops ADD COLUMN IF NOT EXISTS "ReEntryCooldown" interval;
                        ALTER TABLE workshops ADD COLUMN IF NOT EXISTS "RequireReEntryVerification" boolean NOT NULL DEFAULT TRUE;
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915115622_RemoveTrainerDocuments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915115622_RemoveTrainerDocuments', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915200612_AddMediaItems') THEN
    CREATE TABLE media_items (
        "Id" uuid NOT NULL,
        "ObjectKey" character varying(500) NOT NULL,
        "OriginalFileName" character varying(255) NOT NULL,
        "MediaType" character varying(50) NOT NULL,
        "Section" character varying(100) NOT NULL,
        "MimeType" character varying(100) NOT NULL,
        "FileSizeBytes" bigint NOT NULL,
        "Width" integer,
        "Height" integer,
        "DurationSeconds" double precision,
        "ThumbnailObjectKey" text,
        "PosterObjectKey" text,
        "PublicUrl" character varying(1000) NOT NULL,
        "Visibility" character varying(50) NOT NULL,
        "ApprovalStatus" character varying(50) NOT NULL,
        "UploadedByUserId" uuid,
        "Checksum" character varying(128),
        "IsDeleted" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_media_items" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_media_items_users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES users ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915200612_AddMediaItems') THEN
    CREATE UNIQUE INDEX "IX_media_items_ObjectKey" ON media_items ("ObjectKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915200612_AddMediaItems') THEN
    CREATE INDEX "IX_media_items_Section" ON media_items ("Section");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915200612_AddMediaItems') THEN
    CREATE INDEX "IX_media_items_UploadedByUserId" ON media_items ("UploadedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915200612_AddMediaItems') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915200612_AddMediaItems', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "Area" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "City" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "ContactNumber" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "ContactPerson" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "EndUtc" timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "GooglePlaceId" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "LandscapeImageUrl" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "Latitude" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "Longitude" double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "PublicVisibility" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "RegistrationType" character varying(50) NOT NULL DEFAULT 'Standard';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "ShortDescription" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "StartUtc" timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "TermsAndCancellationPolicy" character varying(4000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "Timezone" character varying(100) NOT NULL DEFAULT 'Asia/Kolkata';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    ALTER TABLE workshops ADD "VenueAddress" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN

                    UPDATE workshops 
                    SET "StartUtc" = ("WorkshopDate"::date + "StartTime") AT TIME ZONE 'Asia/Kolkata' AT TIME ZONE 'UTC',
                        "EndUtc" = CASE 
                            WHEN "EndTime" > "StartTime" THEN ("WorkshopDate"::date + "EndTime") AT TIME ZONE 'Asia/Kolkata' AT TIME ZONE 'UTC'
                            ELSE (("WorkshopDate"::date + interval '1 day') + "EndTime") AT TIME ZONE 'Asia/Kolkata' AT TIME ZONE 'UTC'
                        END,
                        "Timezone" = COALESCE("Timezone", 'Asia/Kolkata'),
                        "PublicVisibility" = COALESCE("PublicVisibility", true),
                        "RegistrationType" = COALESCE("RegistrationType", 'Standard')
                    WHERE "StartUtc" IS NULL;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916153144_AddWorkshopWizardMetadata') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260916153144_AddWorkshopWizardMetadata', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "Caption" character varying(500) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "Category" character varying(100) NOT NULL DEFAULT 'General';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "DisplayOrder" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "IsFeatured" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "IsPublished" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "LayoutType" character varying(50) NOT NULL DEFAULT 'square_1_1';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "TargetUrl" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "Title" character varying(200) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "VisibleFromUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "VisibleUntilUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD "WorkshopId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    CREATE INDEX "IX_media_items_Category" ON media_items ("Category");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    CREATE INDEX "IX_media_items_DisplayOrder" ON media_items ("DisplayOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    CREATE INDEX "IX_media_items_IsPublished" ON media_items ("IsPublished");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    CREATE INDEX "IX_media_items_WorkshopId" ON media_items ("WorkshopId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    ALTER TABLE media_items ADD CONSTRAINT "FK_media_items_workshops_WorkshopId" FOREIGN KEY ("WorkshopId") REFERENCES workshops ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916193648_AddMediaGalleryMetadata') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260916193648_AddMediaGalleryMetadata', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916200350_AddMediaArchivalAndResponsiveFields') THEN
    ALTER TABLE media_items ALTER COLUMN "LayoutType" SET DEFAULT 'Square';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916200350_AddMediaArchivalAndResponsiveFields') THEN
    ALTER TABLE media_items ADD "AltText" character varying(300) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916200350_AddMediaArchivalAndResponsiveFields') THEN
    ALTER TABLE media_items ADD "FocalPoint" character varying(50) NOT NULL DEFAULT 'center';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916200350_AddMediaArchivalAndResponsiveFields') THEN
    ALTER TABLE media_items ADD "IsArchived" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916200350_AddMediaArchivalAndResponsiveFields') THEN
    ALTER TABLE media_items ADD "OptimizedUrl" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916200350_AddMediaArchivalAndResponsiveFields') THEN
    ALTER TABLE media_items ADD "ThumbnailUrl" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916200350_AddMediaArchivalAndResponsiveFields') THEN
    CREATE INDEX "IX_media_items_IsArchived" ON media_items ("IsArchived");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916200350_AddMediaArchivalAndResponsiveFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260916200350_AddMediaArchivalAndResponsiveFields', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    CREATE TABLE media_placements (
        "Id" uuid NOT NULL,
        "MediaItemId" uuid NOT NULL,
        "Section" character varying(100) NOT NULL,
        "DisplayOrder" integer NOT NULL DEFAULT 0,
        "IsPublished" boolean NOT NULL DEFAULT TRUE,
        "IsFeatured" boolean NOT NULL DEFAULT FALSE,
        "VisibleFromUtc" timestamp with time zone,
        "VisibleUntilUtc" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_media_placements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_media_placements_media_items_MediaItemId" FOREIGN KEY ("MediaItemId") REFERENCES media_items ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    CREATE INDEX ix_media_placements_is_published ON media_placements ("IsPublished");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    CREATE UNIQUE INDEX ix_media_placements_item_section ON media_placements ("MediaItemId", "Section");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    CREATE INDEX ix_media_placements_section ON media_placements ("Section");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN

                    INSERT INTO media_placements ("Id", "MediaItemId", "Section", "DisplayOrder", "IsPublished", "IsFeatured", "VisibleFromUtc", "VisibleUntilUtc", "CreatedAt")
                    SELECT
                        gen_random_uuid(),
                        "Id",
                        CASE WHEN "Section" = 'HomepageGallery' THEN 'Draft' ELSE "Section" END,
                        "DisplayOrder",
                        "IsPublished",
                        "IsFeatured",
                        "VisibleFromUtc",
                        "VisibleUntilUtc",
                        NOW() AT TIME ZONE 'UTC'
                    FROM media_items
                    WHERE "IsDeleted" = false;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    DROP INDEX "IX_media_items_DisplayOrder";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    DROP INDEX "IX_media_items_IsPublished";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    DROP INDEX "IX_media_items_Section";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    ALTER TABLE media_items DROP COLUMN "DisplayOrder";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    ALTER TABLE media_items DROP COLUMN "IsFeatured";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    ALTER TABLE media_items DROP COLUMN "IsPublished";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    ALTER TABLE media_items DROP COLUMN "Section";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    ALTER TABLE media_items DROP COLUMN "VisibleFromUtc";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    ALTER TABLE media_items DROP COLUMN "VisibleUntilUtc";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260916210910_AddMediaPlacementsAndRemoveGallery') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260916210910_AddMediaPlacementsAndRemoveGallery', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917145121_AddWhatsAppOutboxAndTicketPdf') THEN
    CREATE TABLE ticket_pdfs (
        "Id" uuid NOT NULL,
        "TicketId" uuid NOT NULL,
        "StorageKey" character varying(512) NOT NULL,
        "FileHash" character varying(64) NOT NULL,
        "FileSizeBytes" bigint NOT NULL,
        "CreatedAt" timestamptz NOT NULL,
        CONSTRAINT "PK_ticket_pdfs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ticket_pdfs_workshop_tickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES workshop_tickets ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917145121_AddWhatsAppOutboxAndTicketPdf') THEN
    CREATE TABLE whatsapp_notifications (
        "Id" uuid NOT NULL,
        "BookingId" uuid NOT NULL,
        "WorkshopTicketId" uuid,
        "NotificationType" integer NOT NULL,
        "RecipientPhone" character varying(32) NOT NULL,
        "IdempotencyKey" character varying(128) NOT NULL,
        "Status" integer NOT NULL,
        "Attempts" integer NOT NULL DEFAULT 0,
        "ProviderMessageId" character varying(128),
        "ProviderRequestId" character varying(128),
        "LastError" character varying(2000),
        "LeaseExpiresAt" timestamptz,
        "LockedByWorkerId" character varying(64),
        "CreatedAt" timestamptz NOT NULL,
        "SentAt" timestamptz,
        "NextAttemptAt" timestamptz,
        CONSTRAINT "PK_whatsapp_notifications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_whatsapp_notifications_workshop_bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES workshop_bookings ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_whatsapp_notifications_workshop_tickets_WorkshopTicketId" FOREIGN KEY ("WorkshopTicketId") REFERENCES workshop_tickets ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917145121_AddWhatsAppOutboxAndTicketPdf') THEN
    CREATE UNIQUE INDEX "IX_ticket_pdfs_TicketId" ON ticket_pdfs ("TicketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917145121_AddWhatsAppOutboxAndTicketPdf') THEN
    CREATE INDEX "IX_whatsapp_notifications_BookingId" ON whatsapp_notifications ("BookingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917145121_AddWhatsAppOutboxAndTicketPdf') THEN
    CREATE UNIQUE INDEX "IX_whatsapp_notifications_IdempotencyKey" ON whatsapp_notifications ("IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917145121_AddWhatsAppOutboxAndTicketPdf') THEN
    CREATE INDEX "IX_whatsapp_notifications_Status_NextAttemptAt_LeaseExpiresAt" ON whatsapp_notifications ("Status", "NextAttemptAt", "LeaseExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917145121_AddWhatsAppOutboxAndTicketPdf') THEN
    CREATE INDEX "IX_whatsapp_notifications_WorkshopTicketId" ON whatsapp_notifications ("WorkshopTicketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917145121_AddWhatsAppOutboxAndTicketPdf') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260917145121_AddWhatsAppOutboxAndTicketPdf', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens') THEN
    DROP TABLE otp_verifications;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens') THEN
    ALTER TABLE users ALTER COLUMN "MustChangePassword" SET DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens') THEN
    ALTER TABLE users ADD "FailedLoginCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens') THEN
    ALTER TABLE users ADD "LockoutEnd" timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens') THEN
    ALTER TABLE users ADD "PasswordChangedAt" timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens') THEN
    CREATE TABLE password_reset_tokens (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TokenHash" character varying(128) NOT NULL,
        "ExpiresAt" timestamptz NOT NULL,
        "UsedAt" timestamptz,
        "CreatedAt" timestamptz NOT NULL,
        "CreatedByIpHash" character varying(64),
        CONSTRAINT "PK_password_reset_tokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_password_reset_tokens_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens') THEN
    CREATE INDEX "IX_password_reset_tokens_TokenHash" ON password_reset_tokens ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens') THEN
    CREATE INDEX "IX_password_reset_tokens_UserId" ON password_reset_tokens ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260917201930_RemoveOtpVerificationTableAndAddPasswordResetTokens', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN
    DROP INDEX "IX_workshop_bookings_WorkshopId_StudentProfileId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN
    ALTER TABLE workshops ALTER COLUMN "LandscapeImageUrl" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN
    ALTER TABLE workshops ALTER COLUMN "ImageUrl" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN
    ALTER TABLE workshop_bookings ALTER COLUMN "CancelledAt" TYPE timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN
    ALTER TABLE workshop_bookings ADD "IdempotencyKey" character varying(128) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN
    ALTER TABLE workshop_bookings ADD "ReservationExpiresAt" timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN
    CREATE UNIQUE INDEX "IX_workshop_bookings_IdempotencyKey" ON workshop_bookings ("IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN
    CREATE INDEX "IX_workshop_bookings_WorkshopId_StudentProfileId" ON workshop_bookings ("WorkshopId", "StudentProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN

                        CREATE TABLE IF NOT EXISTS studio_videos (
                            "Id" uuid NOT NULL PRIMARY KEY,
                            "Title" character varying(150) NOT NULL,
                            "Description" character varying(500),
                            "Section" character varying(50) NOT NULL,
                            "ObjectKey" character varying(500) NOT NULL,
                            "PublicUrl" character varying(1000) NOT NULL,
                            "ThumbnailUrl" character varying(1000),
                            "DisplayOrder" integer NOT NULL DEFAULT 0,
                            "IsActive" boolean NOT NULL DEFAULT TRUE,
                            "DurationSeconds" double precision,
                            "FileSizeBytes" bigint NOT NULL DEFAULT 0,
                            "MimeType" character varying(100) NOT NULL DEFAULT 'video/mp4',
                            "UploadedByUserId" uuid,
                            "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            CONSTRAINT "FK_studio_videos_users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES users ("Id") ON DELETE SET NULL
                        );
                        CREATE INDEX IF NOT EXISTS "IX_studio_videos_Section_IsActive_DisplayOrder" ON studio_videos ("Section", "IsActive", "DisplayOrder");
                        CREATE INDEX IF NOT EXISTS "IX_studio_videos_ObjectKey" ON studio_videos ("ObjectKey");

                        UPDATE workshop_bookings SET "IdempotencyKey" = gen_random_uuid()::text WHERE "IdempotencyKey" IS NULL OR "IdempotencyKey" = '';
                    
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918110714_AddBookingConcurrencyAndStudioVideos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260918110714_AddBookingConcurrencyAndStudioVideos', '10.0.11');
    END IF;
END $EF$;
COMMIT;

