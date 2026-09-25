# Environment Variables — PRODUCTION Environment

> **ENVIRONMENT**: **PRODUCTION (PROD)**  
> **Target**: Azure App Service (`ethos-prod-api-app`) & Frontend Host (Netlify)  
> **Format**: Azure App Service Application Settings syntax (`Category__Key`)

---

## 1. Backend Application Settings (`ethos-prod-api-app`)

Configure these in Azure Portal under **Settings -> Configuration -> Application settings** for `ethos-prod-api-app`:

```ini
# Core Environment
ASPNETCORE_ENVIRONMENT=Production

# Authoritative Database (Neon PROD)
ConnectionStrings__DefaultConnection=postgresql://<prod-user>:<prod-password>@<neon-prod-pooler-host>/ethos-prod-db?sslmode=require

# Authentication & Cryptographic Security
Jwt__SecretKey=<cryptographically-random-256-bit-key-min-32-chars>
Jwt__Issuer=EthosDanceStudioProduction
Jwt__Audience=EthosDanceStudioProductionAudience
Jwt__ExpirationMinutes=60
TicketSecurity__SecretKey=<cryptographically-random-ticket-hmac-key-min-32-chars>

# Payment Gateway (Razorpay LIVE Mode)
Razorpay__KeyId=rzp_live_<your-live-key-id>
Razorpay__KeySecret=<your-razorpay-live-key-secret>
Razorpay__WebhookSecret=<your-production-webhook-secret>

# Permanent Authoritative Media Storage (Cloudflare R2 PROD Bucket)
CloudflareR2__AccountId=<production-cloudflare-account-id>
CloudflareR2__AccessKeyId=<r2-prod-token-access-key-id>
CloudflareR2__SecretAccessKey=<r2-prod-token-secret-access-key>
CloudflareR2__BucketName=ethos-production-media
CloudflareR2__PublicDomain=https://media.ethosdancestudio.com

# CORS Settings (Strict Production Origins)
Cors__AllowedOrigins__0=https://ethosdancestudio.com
Cors__AllowedOrigins__1=https://www.ethosdancestudio.com
Cors__AllowedOrigins__2=https://media.ethosdancestudio.com

# WhatsApp Outbox & Notifications (MSG91 PROD)
# Set Enabled=false initially, switch to true at final go-live
Msg91__Enabled=false
Msg91__AuthKey=<production-msg91-auth-key>
Msg91__IntegratedNumber=<production-registered-sender-number>
Msg91__AllowTestEndpoints=false
```

---

## 2. Frontend Production Environment Variables (`Ethos.Web` PROD Build)

Configure in Netlify (Site settings -> Environment variables) or `.env.production`:

```ini
# Authoritative Production API Endpoint
VITE_API_BASE_URL=https://api.ethosdancestudio.com

# Razorpay LIVE Client-Side Key (MUST be rzp_live_*)
VITE_RAZORPAY_KEY_ID=rzp_live_<your-live-key-id>
```

---

## 3. Strict Pre-Deployment Checks for PRODUCTION

Before saving these variables in production Azure / Netlify:
1. `ConnectionStrings__DefaultConnection` connects strictly to `ethos-prod-db` (Never test DB).
2. `Razorpay__KeyId` begins with `rzp_live_` (Never `rzp_test_`).
3. `CloudflareR2__BucketName` equals `ethos-production-media` (Never test bucket).
4. `Msg91__AllowTestEndpoints` is set to `false`.
5. `Jwt__SecretKey` and `TicketSecurity__SecretKey` are newly generated random high-entropy strings, distinct from any test or development keys.
