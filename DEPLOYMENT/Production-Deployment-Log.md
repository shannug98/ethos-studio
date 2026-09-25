# Ethos Dance Studio — Production Deployment Log

This living log tracks the execution of each phase in the **ETHOS V1 — FINAL GO-LIVE ROADMAP** from baseline establishment to live production launch.

---

## Roadmap Execution Tracker

| Phase | Description | Status | Timestamp / Notes |
|---|---|---|---|
| **Phase 0** | **Release & Version-Control System** | ✅ **COMPLETED** | 2026-09-23 — Created `RELEASES/CHANGELOG.md` and `RELEASES/v1.0.0-rc.1.md`. Codebase frozen at `v1.0.0-rc.1`. |
| **Phase 1** | **Clean Azure TEST Resource** | ⏳ **PENDING (Next)** | User to delete empty `rg-ethos-test` in Azure Portal. |
| **Phase 2** | **Create Azure PROD Resource Group** | ⏳ Queued | Create `rg-ethos-prod` in `Central India`. |
| **Phase 3** | **Create Azure PROD App Service** | ⏳ Queued | Create `ethos-prod-api-app` (.NET 10 on Linux, Plan `asp-ethos-prod`, Tier B1). |
| **Phase 4** | **Azure Production Configuration** | ⏳ Queued | Populate application settings in Azure (secrets entered by user). |
| **Phase 5** | **Azure Runtime / Security Configuration** | ⏳ Queued | HTTPS-only, Always On, TLS 1.2, CORS, logging. |
| **Phase 6** | **Neon PROD Provisioning** | ⏳ Queued | Create `ethos-prod` project and `ethos-prod-db` in Neon. |
| **Phase 7** | **Production Database Initialization** | ⏳ Queued | Execute `dotnet ef database update` against `ethos-prod-db`. |
| **Phase 8** | **Cloudflare R2 PROD Provisioning** | ⏳ Queued | Create `ethos-production-media` bucket and scoped API token. |
| **Phase 9** | **Production Media Structure Verification** | ⏳ Queued | Verify R2 upload/read/delete paths; confirm zero local disk reliance. |
| **Phase 10** | **Razorpay LIVE Configuration** | ⏳ Queued | Configure live API keys (`rzp_live_*`) and live webhook. |
| **Phase 11** | **MSG91 PROD Preparation** | ⏳ Queued | Verify approved WhatsApp templates; configure live credentials (keep `Enabled=false`). |
| **Phase 12** | **Production Frontend Configuration** | ⏳ Queued | Netlify configuration with `VITE_API_BASE_URL=https://api.ethosdancestudio.com`. |
| **Phase 13** | **API Custom Domain Setup** | ⏳ Queued | Bind `api.ethosdancestudio.com` to Azure App Service with SSL. |
| **Phase 14** | **Media Custom Domain Setup** | ⏳ Queued | Bind `media.ethosdancestudio.com` to Cloudflare R2 bucket. |
| **Phase 15** | **Production Backend Deployment** | ⏳ Queued | Deploy `v1.0.0-rc.1` Release artifact to `ethos-prod-api-app`. |
| **Phase 16** | **Production Smoke Test** | ⏳ Queued | Verify public, student, trainer, and admin endpoints. |
| **Phase 17** | **Real Payment Controlled Test** | ⏳ Queued | Execute one controlled live transaction (₹1) to verify webhook & fulfillment. |
| **Phase 18** | **QR / Ticket / Attendance Verification** | ⏳ Queued | Verify generated ticket PDF, session date/time, QR token, and scanner. |
| **Phase 19** | **WhatsApp Controlled Test** | ⏳ Queued | Enable `Msg91:Enabled = true`, verify live template delivery to test phone. |
| **Phase 20** | **Production Security Check** | ⏳ Queued | Confirm Swagger 404, mock bypass blocked, CORS, CSP, no exposed secrets. |
| **Phase 21** | **Production Observability Verification** | ⏳ Queued | Verify Azure App Service logs and application health tracking. |
| **Phase 22** | **Backup & Rollback Baseline Locked** | ⏳ Queued | Record rollback artifact and Neon PITR baseline. |
| **Phase 23** | **Release Promotion (v1.0.0-rc.1 -> v1.0.0)** | ⏳ Queued | Create `RELEASES/v1.0.0.md` marking official production status. |
| **Phase 24** | **Final DNS & Domain Verification** | ⏳ Queued | Verify `ethosdancestudio.com` routing through Netlify. |
| **Phase 25** | **Official Go-Live** | ⏳ Queued | Declare ETHOS v1.0.0 LIVE. |
| **Phase 26** | **24–72 Hour Post-Launch Monitoring** | ⏳ Queued | Active monitoring of bookings, webhooks, errors, and performance. |
| **Phase 27** | **Post-Launch Stabilization & Next Iteration** | ⏳ Queued | Transition to standard v1.1.0 maintenance cycle. |

---

## Deployment Event Log

### [2026-09-23 14:15 IST] — Phase 0 Completed
- Created `RELEASES/CHANGELOG.md` following Keep a Changelog standard.
- Created `RELEASES/v1.0.0-rc.1.md` documenting verified release candidate baseline.
- Codebase state verified clean: zero test failures (327 backend, 132 frontend), zero build errors.
- Ready for Phase 1 (Azure TEST resource group deletion) and Phase 2 (Azure PROD resource group creation).

### [2026-09-23 20:00 IST] — MSG91 Azure Environment Configuration Confirmed
- Confirmed `Msg91__Enabled=true` and `Msg91__AuthKey` configured securely in Azure App Service environment variables.
- `Msg91__IntegratedNumber` to be provided in Azure configuration as well.
- Zero secrets committed or hardcoded in the codebase.
- ASP.NET Core environment provider handles runtime configuration mapping to `Msg91Options`.
- Full outbox dispatcher, retries, idempotency, communication logs, and PDF delivery intact in frozen RC.
