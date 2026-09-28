# Ethos Dance Studio — End-to-End Deployment Checklist

This document details the exact stage-by-stage deployment sequence from the frozen Release Candidate codebase to TEST verification, and subsequently to PRODUCTION Go-Live.

---

## Stage 1: Preparation & Local Gate Verification (Completed)
- [x] Codebase frozen at Release Candidate (no further feature or architecture changes).
- [x] Backend tests passing with zero failures (`dotnet test Ethos.Api.Tests` -> 327 passed, 0 failed).
- [x] Frontend tests passing with zero failures (`npm test` in `Ethos.Web` -> 132 passed, 0 failed).
- [x] Vite production build passing with zero errors (`npm run build` in `Ethos.Web`).
- [x] .NET Release build passing with zero errors (`dotnet build -c Release Ethos.Api`).
- [x] Compiler and analyzer warnings classified as harmless/intentional.
- [x] No secrets, passwords, or production credentials committed to repository.
- [x] Git tags (`v1.0.0`) and pushes held until TEST/UAT passes.

---

## Stage 2: TEST Infrastructure Provisioning
> *Refer to documentation in `/TEST` folder for detailed step-by-step setup guides.*

- [ ] **Neon PostgreSQL (TEST)**:
  - Create Neon Project: `ethos-test`
  - Create Database: `ethos-test-db`
  - Copy Connection String (pooled/direct) with SSL enabled.
- [ ] **Cloudflare R2 (TEST)**:
  - Create R2 Bucket: `ethos-test-media`
  - Configure CORS policy allowing test frontend origins.
  - Create R2 API Token with Object Read & Write permissions scoped to `ethos-test-media`.
  - Record: `AccountId`, `AccessKeyId`, `SecretAccessKey`, `PublicDomain` (or R2.dev subdomain).
- [ ] **Azure App Service (TEST)**:
  - Create Azure Resource Group: `rg-ethos-test`
  - Create App Service Plan: `asp-ethos-test` (Linux, B1 or P1v2)
  - Create Web App: `ethos-test-api-app` (.NET 10 LTS on Linux)
- [ ] **Razorpay (TEST)**:
  - Access Razorpay Dashboard in **TEST Mode**.
  - Generate API Keys: `rzp_test_*` and Key Secret.
  - Note Webhook Secret for `https://ethos-test-api-app.azurewebsites.net/api/payments/webhook`.
- [ ] **Frontend Hosting (TEST)**:
  - Setup Netlify / Static site: `ethos-test` (or deployment branch).
  - Configure environment variables: `VITE_API_BASE_URL` pointing to Azure TEST URL.

---

## Stage 3: TEST Configuration & Deployment
- [ ] Configure Environment Variables in Azure Portal for `ethos-test-api-app` (see `TEST/Environment-Variables-TEST.md`).
- [ ] Verify `ASPNETCORE_ENVIRONMENT` is set to `Production` or `Staging` (so Swagger and test mock bypasses are disabled, while `ProductionSecurityValidator` validates mandatory settings).
- [ ] Deploy backend Release artifact to `ethos-test-api-app` (via Zip Deploy or GitHub Actions).
- [ ] Apply database schema migrations to `ethos-test-db` using controlled CLI command (`dotnet ef database update`).
- [ ] Build and deploy frontend with `VITE_API_BASE_URL` pointing to `ethos-test-api-app.azurewebsites.net`.

---

## Stage 4: TEST Verification & User Acceptance Testing (UAT)
- [ ] **Smoke Test**:
  - `GET https://ethos-test-api-app.azurewebsites.net/health` returns healthy.
  - Public workshops endpoint returns 200 OK.
  - Swagger UI at `/swagger` returns 404 Not Found (verifying environment hardening).
- [ ] **R2 Storage Verification**:
  - Upload a trainer application audition video in TEST; verify file lands in `ethos-test-media` bucket.
  - Upload a gallery image in TEST; verify it resolves and displays via test CDN URL.
  - Delete an asset; verify removal from R2 bucket.
- [ ] **Payment Verification (Razorpay TEST Mode)**:
  - Book a workshop pass using Razorpay Test card / UPI test details.
  - Verify webhook is received and verified successfully.
  - Verify booking status updates to `Confirmed`.
  - Verify ticket pass records are generated in `ethos-test-db`.
- [ ] **Ticket PDF & QR Verification**:
  - Request ticket PDF for confirmed test booking.
  - Verify PDF contains correct workshop name, date/time, and fixed studio footer address.
  - Scan QR code using test scanner; verify derived cryptographic token matches ticket record.
- [ ] **Controlled MSG91 WhatsApp Testing**:
  - Add tester's phone number to `Msg91:ApprovedTestNumbers`.
  - Enable `Msg91:Enabled = true` and `Msg91:AllowTestEndpoints = true` in TEST Azure configuration.
  - Trigger test booking notification; verify WhatsApp template delivery to approved test number.
  - Verify attempted delivery to non-whitelisted numbers is blocked by security guard.

---

## Stage 5: PRODUCTION Infrastructure Provisioning (ONLY AFTER UAT PASSES)
> [!WARNING]
> Do not execute Stage 5 until all Stage 4 UAT tests pass completely with zero regressions.

- [ ] **Neon PostgreSQL (PRODUCTION)**:
  - Create Neon Project: `ethos-prod`
  - Create Database: `ethos-prod-db`
  - Copy authoritative production connection string.
- [ ] **Cloudflare R2 (PRODUCTION)**:
  - Create R2 Bucket: `ethos-production-media`
  - Set custom domain: `media.ethosdancestudio.com`
  - Configure production CORS.
  - Create production-only R2 API Token.
- [ ] **Linux VPS & Nginx (PRODUCTION)**:
  - Provision Ubuntu 22.04 / 24.04 LTS VPS with ASP.NET Core Runtime 8.0/10.0.
  - Setup Systemd service `/etc/systemd/system/ethos-api.service` listening on `localhost:5252`.
  - Configure Nginx reverse proxy for `api.ethosdancestudio.com` -> `http://127.0.0.1:5252`.
  - Issue Let's Encrypt SSL certificate via `certbot --nginx -d api.ethosdancestudio.com`.
- [ ] **Razorpay (PRODUCTION LIVE Mode)**:
  - Switch Razorpay Dashboard to **LIVE Mode**.
  - Generate LIVE API Keys: `rzp_live_*` and Live Secret.
  - Configure Live Webhook pointing to `https://api.ethosdancestudio.com/api/payments/webhook`.
- [ ] **MSG91 (PRODUCTION)**:
  - Obtain live production Auth Key and verified WhatsApp sender number.
  - Ensure Meta/WhatsApp templates (`ethos_booking_confirmed`, `ethos_ticket_pdf`) are APPROVED.

---

## Stage 6: PRODUCTION Configuration, Migration & Deployment
- [ ] Configure Environment Variables in systemd service `/etc/systemd/system/ethos-api.service` (see `PRODUCTION/Environment-Variables-PROD.md`).
- [ ] Run `dotnet ef database update` against `ethos-prod-db` to build production schema.
- [ ] Deploy backend Release artifact to `/var/www/ethos-api`.
- [ ] Connect Git repository to Netlify for automatic CI/CD builds with environment variables `VITE_API_BASE_URL=https://api.ethosdancestudio.com` and `VITE_RAZORPAY_KEY_ID=rzp_live_*`.
- [ ] Configure DNS records:
  - `ethosdancestudio.com` -> Netlify
  - `www.ethosdancestudio.com` -> Netlify
  - `api.ethosdancestudio.com` -> Linux VPS (A Record)
  - `media.ethosdancestudio.com` -> Cloudflare R2

---

## Stage 7: Production Smoke Test & Official Release Tagging
- [ ] Verify `https://ethosdancestudio.com` loads over HTTPS with clean SPA routing.
- [ ] Verify `https://api.ethosdancestudio.com/health` returns HTTP 200 Healthy.
- [ ] Verify Swagger at `https://api.ethosdancestudio.com/swagger` is secured.
- [ ] Perform live end-to-end admin login and workshop verification.
- [ ] Conduct one minimal live test transaction to verify end-to-end payment fulfillment and WhatsApp delivery in production.
- [ ] **Git Tagging & Release**:
  - Run `git tag -a v1.0.0 -m "Release v1.0.0 - Production Launch"`
  - Run `git push origin v1.0.0`
