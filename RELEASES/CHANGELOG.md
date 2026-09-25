# Changelog — Ethos Dance Studio

All notable changes to the Ethos Dance Studio platform will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]
- Ongoing tracking towards `v1.0.0` live launch following production deployment, live payment verification, and MSG91 WhatsApp testing.

---

## [1.0.0-rc.1] - 2026-09-23

### Status
**Release Candidate 1** — Audit Passed & Frozen for Production Deployment.

### Added
- **Cloudflare R2 Authoritative Storage**:
  - Implemented `R2TrainerApplicationVideoStorageService` for authoritative permanent storage of trainer audition videos.
  - Implemented `R2TrainerGalleryStorageService` for authoritative storage of trainer gallery images.
  - Local `App_Data` filesystem strictly designated as ephemeral processing/duration cache.
- **MSG91 WhatsApp Integration (Phase D)**:
  - Integrated 6-variable WhatsApp payload architecture (`{{1}}` AttendeeName, `{{2}}` WorkshopTitle, `{{3}}` WorkshopDate, `{{4}}` WorkshopTime, `{{5}}` Location, `{{6}}` BookingId).
  - Added session-specific date/time resolution for multi-session tickets matching `TicketPdfService`.
  - Added authoritative location resolution formatted as `Ethos Dance Studio, [venue/address]`.
  - Added automatic student attendee name fallback (`StudentProfile.User.FullName`) when `GuestName` is empty.
  - Added `AdminWhatsAppTestController` with phone normalization, sanitization, and strict whitelist guards.
- **Security & Infrastructure Hardening**:
  - Added `ProductionSecurityValidator` to enforce mandatory production credentials before startup.
  - Added explicit origin `https://api.ethosdancestudio.com` to `connect-src` in `public/_headers` (strict CSP).
  - Added `.nvmrc` and `.node-version` pinning Node.js to `22.12.0`.
  - Added `tests/verify_admin_localhost_and_csp.test.mjs` verifying production origin isolation and node pinning.
  - Added regression test `GetOrCreateTicketPdfAsync_WhenRawQrTokenNull_UsesDerivedQrTokenNotTicketNumber`.

### Changed
- **Ticket PDF Architecture Centralized**:
  - Refactored `TicketPdfService` to return `PdfBytes` and centrally manage PDF generation, QuestPDF rendering, and R2 caching.
  - Streamlined `WorkshopsController.GetTicketPdf` to delegate completely to `TicketPdfService` and stream bytes directly, eliminating duplicate layout logic.
- **QR Token Derivation Fallback**:
  - Updated QR code token generation to strictly use HMAC-derived cryptographic token (`DeriveQrToken`) when `rawQrToken` is null, never falling back to `TicketNumber`.
- **Admin API Localhost Fallback**:
  - Enforced `import.meta.env?.DEV` guard on `127.0.0.1:5252` fallback in `src/services/adminApi.js`. In production (`isDev = false`), candidate list includes only canonical base URLs.
- **Seed Password Hardening**:
  - Removed all hardcoded seed passwords (`Trainer@123`, `EthosTemp#9999`) from `Phase2SeedService.cs`. Required seed passwords must be supplied via User Secrets or configuration (`Seed:TestTrainerPassword`).
- **Production Configuration Defaults**:
  - Configured `Msg91:Enabled = false` by default in `appsettings.Production.json`.
  - Cleared all dummy passwords, keys, and tokens to empty strings `""`.
- **Frontend Test Suite Runner**:
  - Updated `package.json` `"test"` script to `"node --test tests/verify_*.mjs tests/verified_fixes.test.mjs"`. Added `"test:e2e"`.

### Security Verification
- **Secrets Clean**: Zero passwords, live Razorpay secrets, MSG91 keys, or connection strings in repository.
- **EF Migrations**: Startup auto-migration restricted strictly to Development environment (`app.Environment.IsDevelopment()`).
- **Swagger UI**: Restricted strictly to Development environment.
- **Payment Mock Bypasses**: Prohibited outside Development environment (throws `InvalidOperationException`).
- **MSG91 Test Endpoints**: Disabled by server configuration (`Msg91:AllowTestEndpoints = false`).

### Verification Metrics
- Backend Test Suite: **327 passed, 0 failed, 8 skipped, total 335 (100% PASS)**
- Frontend Test Suite: **132 passed, 0 failed (100% PASS)**
- Vite Production Build: **0 errors**
- .NET Release Build: **0 errors (6 harmless/intentional CS/EF compiler warnings classified)**
