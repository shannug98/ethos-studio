# Environment Variables — TEST Environment

> **ENVIRONMENT**: **TEST ONLY**  
> **Target**: Azure App Service (`ethos-test-api-app`) & Frontend Test Host  
> **Format**: Azure App Service Application Settings syntax (`Category__Key`)

---

## 1. Backend Application Settings (`ethos-test-api-app`)

Configure these in Azure Portal under **Settings -> Configuration -> Application settings** (or Environment variables) for `ethos-test-api-app`:

```ini
# Core Environment
ASPNETCORE_ENVIRONMENT=Production

# Database (Neon TEST)
ConnectionStrings__DefaultConnection=postgresql://<test-user>:<test-password>@<neon-test-pooler-host>/ethos-test-db?sslmode=require

# Authentication & Security
Jwt__SecretKey=<generate-random-256-bit-secret-min-32-chars>
Jwt__Issuer=EthosDanceStudioTest
Jwt__Audience=EthosDanceStudioTestAudience
Jwt__ExpirationMinutes=60
TicketSecurity__SecretKey=<generate-random-ticket-secret-min-32-chars>

# Payment Gateway (Razorpay TEST Mode)
Razorpay__KeyId=rzp_test_<your-test-key-id>
Razorpay__KeySecret=<your-razorpay-test-key-secret>
Razorpay__WebhookSecret=<your-test-webhook-secret>

# Storage (Cloudflare R2 TEST Bucket)
CloudflareR2__AccountId=<your-cloudflare-account-id>
CloudflareR2__AccessKeyId=<r2-test-token-access-key-id>
CloudflareR2__SecretAccessKey=<r2-test-token-secret-access-key>
CloudflareR2__BucketName=ethos-test-media
CloudflareR2__PublicDomain=https://pub-test-xxxx.r2.dev

# CORS Settings (Allow Test Frontend)
Cors__AllowedOrigins__0=https://ethos-test.netlify.app
Cors__AllowedOrigins__1=https://ethos-test-api-app.azurewebsites.net
Cors__AllowedOrigins__2=http://localhost:5173

# WhatsApp Notifications (MSG91 Controlled Test Setup)
# Initially kept false; set to true ONLY when running controlled WhatsApp test with approved whitelist
Msg91__Enabled=false
Msg91__AuthKey=<msg91-auth-key>
Msg91__IntegratedNumber=<registered-msg91-sender-phone-number>
Msg91__AllowTestEndpoints=true
Msg91__ApprovedTestNumbers__0=<tester-phone-number-e164-without-plus>

# Seed Data (Optional Test Accounts)
Seed__TestTrainerPassword=<secure-test-trainer-password>
Seed__SecondTestTrainerPassword=<secure-second-test-trainer-password>
```

---

## 2. Frontend Environment Variables (`Ethos.Web` TEST Build)

Configure in Netlify (Site settings -> Environment variables) or local `.env.test`:

```ini
# TEST API Endpoint
VITE_API_BASE_URL=https://ethos-test-api-app.azurewebsites.net

# Razorpay Client-Side Key (MUST be rzp_test_*)
VITE_RAZORPAY_KEY_ID=rzp_test_<your-test-key-id>
```

---

## 3. Strict Pre-Deployment Checks for TEST

Before saving these variables in Azure:
1. `ConnectionStrings__DefaultConnection` must connect to `ethos-test-db` (Never `ethos-prod-db`).
2. `Razorpay__KeyId` must start with `rzp_test_` (Never `rzp_live_`).
3. `CloudflareR2__BucketName` must equal `ethos-test-media` (Never `ethos-production-media`).
4. `Msg91__ApprovedTestNumbers__0` must be restricted to internal developers/testers.
