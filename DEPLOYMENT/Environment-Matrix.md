# Ethos Dance Studio — Environment Matrix

> [!IMPORTANT]
> **Strict Single-Codebase Invariant**: Both TEST and PRODUCTION environments execute the exact same Release Candidate codebase. Environments are separated strictly via infrastructure resources, domain names, and configuration/secrets. Never create divergent code branches or manual code forks between environments.

---

## 1. High-Level Architectural Matrix

| Component | LOCAL DEVELOPMENT | TEST ENVIRONMENT | PRODUCTION ENVIRONMENT |
|---|---|---|---|
| **Environment Tag** | `Development` | `Staging` / `Test` | `Production` |
| **Codebase Artifact** | Working Directory / Git HEAD | Release Candidate v1.0.0-rc | Release Tag v1.0.0 |
| **Frontend Host** | Localhost (Vite Dev Server) | Netlify / Azure Static Web App (TEST) | Netlify / Custom Domain (PROD) |
| **Frontend URL** | `http://localhost:5173` | `https://ethos-test.netlify.app` *(or test subdomain)* | `https://ethosdancestudio.com`<br>`https://www.ethosdancestudio.com` |
| **Backend Host** | Local Kestrel (`dotnet run`) | Azure App Service (`Linux B1/P1v2`) | Azure App Service (`Linux P1v2/P2v2`) |
| **Backend Resource Name** | `localhost:5252` | `ethos-test-api-app` | `ethos-prod-api-app` |
| **Backend Public URL** | `http://localhost:5252` | `https://ethos-test-api-app.azurewebsites.net`<br>*(or `https://api-test.ethosdancestudio.com`)* | `https://api.ethosdancestudio.com`<br>`https://ethos-prod-api-app.azurewebsites.net` |
| **Database (PostgreSQL)** | Local Postgres / Neon Dev Branch | **Neon TEST Project** | **Neon PROD Project** |
| **Database Instance Name** | `ethos-dev-db` | `ethos-test-db` | `ethos-prod-db` |
| **Media Storage (Cloudflare R2)** | Ephemeral disk / R2 dev bucket | **Cloudflare R2 TEST Bucket** | **Cloudflare R2 PROD Bucket** |
| **R2 Bucket Name** | `ethos-development-media` | `ethos-test-media` | `ethos-production-media` |
| **R2 CDN / Public Domain** | `https://media.ethosdancestudio.com` | `https://test-media.ethosdancestudio.com`<br>*(or `pub-test-*.r2.dev`)* | `https://media.ethosdancestudio.com` |
| **Payment Gateway (Razorpay)** | Razorpay Mock / Test Key | **Razorpay TEST Mode** (`rzp_test_*`) | **Razorpay LIVE Mode** (`rzp_live_*`) |
| **WhatsApp Notification (MSG91)** | Disabled / Mock | **MSG91 Controlled Testing**<br>(Approved phone whitelist only) | **MSG91 Production Live**<br>(Full transactional outbox) |
| **Swagger UI** | Enabled (`/swagger`) | Disabled | Disabled |
| **EF Core Auto-Migration** | Enabled on startup | **Disabled** (CLI script / controlled step) | **Disabled** (Strict deployment pipeline) |
| **Admin Localhost Fallback** | Enabled (`127.0.0.1:5252`) | **Disabled** | **Disabled** |

---

## 2. Resource Isolation & Cross-Contamination Boundaries

```
┌────────────────────────────────────────────────────────┐
│                   TEST ENVIRONMENT                     │
│                                                        │
│  [Frontend TEST]  ──────►  [Azure App Service TEST]   │
│  (VITE_API_BASE_URL=test)  (ethos-test-api-app)        │
│                                   │                    │
│            ┌──────────────────────┴─────────────┐      │
│            ▼                                    ▼      │
│   [Neon DB: ethos-test-db]         [R2: ethos-test-media]
│   [Razorpay: rzp_test_*]           [MSG91: Whitelist]   │
└────────────────────────────────────────────────────────┘
                           ║
          STRICT ISOLATION FIREWALL (NO CROSS-TALK)
                           ║
┌────────────────────────────────────────────────────────┐
│                PRODUCTION ENVIRONMENT                  │
│                                                        │
│  [Frontend PROD]  ──────►  [Azure App Service PROD]   │
│  (VITE_API_BASE_URL=prod)  (ethos-prod-api-app)        │
│                                   │                    │
│            ┌──────────────────────┴─────────────┐      │
│            ▼                                    ▼      │
│   [Neon DB: ethos-prod-db]         [R2: ethos-prod-media]
│   [Razorpay: rzp_live_*]           [MSG91: Live Outbox] │
└────────────────────────────────────────────────────────┘
```

> [!CAUTION]
> **Cardinal Safety Rules**:
> 1. **Never point TEST App Service to `ethos-prod-db`**: Doing so will corrupt production booking sequences, ticket numbers, and customer accounts during testing.
> 2. **Never point TEST App Service to `ethos-production-media`**: Deleting test trainer applications or gallery photos in TEST will permanently delete production assets from R2.
> 3. **Never configure `rzp_live_*` in TEST**: Real money will be charged for test workshop bookings.
> 4. **Never configure `rzp_test_*` in PROD**: Real users will not be charged, leading to unfulfilled payments and severe financial discrepancies.
> 5. **Never enable MSG91 without whitelist in TEST**: Automated outbox dispatchers could send spam or confusing test notifications to real student phone numbers.

---

## 3. Environment Lifecycle Progression

```
┌──────────────┐      ┌──────────────┐      ┌──────────────┐      ┌──────────────┐
│  Phase 1     │ ───► │  Phase 2     │ ───► │  Phase 3     │ ───► │  Phase 4     │
│  TEST Infra  │      │  Deploy TEST │      │  Verify TEST │      │  PROD Infra  │
│  Provisioning│      │  & Migrate   │      │  (UAT/R2/Pay)│      │  Provisioning│
└──────────────┘      └──────────────┘      └──────────────┘      └──────────────┘
                                                                         │
                                                                         ▼
                                                                  ┌──────────────┐
                                                                  │  Phase 5     │
                                                                  │  Deploy PROD │
                                                                  │  & Go-Live   │
                                                                  └──────────────┘
```
