# Ethos Dance Studio — Configuration & Secret Mapping Checklist

This checklist documents every configuration key and secret required by the Ethos Dance Studio platform across TEST and PRODUCTION environments.

> [!CAUTION]
> **Zero Secrets in Repository Policy**:
> - Never commit passwords, tokens, API keys, or connection strings into `.json`, `.yml`, `.cs`, `.js`, or `.md` files.
> - In Azure App Service, configure all variables in **Configuration -> Application Settings** (or Azure Key Vault).
> - In Netlify, configure variables in **Site Configuration -> Environment Variables**.
> - Locally during development, use .NET User Secrets (`dotnet user-secrets`) or a local untracked `.env` file.

---

## Complete Configuration Mapping

| Configuration Variable (JSON Path / Azure Env Var) | Secret? | Required? | TEST Purpose / Value Format | PROD Purpose / Value Format | Where to Configure | Safe Default / Fallback |
|---|---|---|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | No | **Yes** | `Production` or `Staging` | `Production` | Azure App Service App Settings | None (must set) |
| `ConnectionStrings__DefaultConnection`<br>(`ConnectionStrings:DefaultConnection`) | **YES** | **Yes** | Neon TEST PostgreSQL connection string with SSL | Neon PROD PostgreSQL connection string with SSL | Azure App Service Connection Strings / App Settings | None (Throws `InvalidOperationException` if empty in Prod) |
| `Jwt__SecretKey`<br>(`Jwt:SecretKey`) | **YES** | **Yes** | 256-bit+ random secret for TEST JWT generation | 256-bit+ cryptographically secure secret for PROD JWTs | Azure App Service App Settings | None (Throws `InvalidOperationException` if <32 chars in Prod) |
| `Jwt__Issuer`<br>(`Jwt:Issuer`) | No | Yes | `EthosDanceStudioTest` | `EthosDanceStudioProduction` | Azure App Service App Settings | `EthosDanceStudioProduction` |
| `Jwt__Audience`<br>(`Jwt:Audience`) | No | Yes | `EthosDanceStudioTestAudience` | `EthosDanceStudioProductionAudience` | Azure App Service App Settings | `EthosDanceStudioProductionAudience` |
| `Jwt__ExpirationMinutes`<br>(`Jwt:ExpirationMinutes`) | No | No | Access token lifespan in minutes (e.g. `60`) | Access token lifespan in minutes (e.g. `60`) | Azure App Service App Settings | `60` |
| `TicketSecurity__SecretKey`<br>(`TicketSecurity:SecretKey`) | **YES** | **Yes** | Min 32-character key for TEST ticket HMAC & QR tokens | Min 32-character key for PROD ticket HMAC & QR tokens | Azure App Service App Settings | None (Throws `InvalidOperationException` if empty in Prod) |
| `Razorpay__KeyId`<br>(`Razorpay:KeyId`) | No | **Yes** | Razorpay TEST Key ID (`rzp_test_...`) | Razorpay LIVE Key ID (`rzp_live_...`) | Azure App Service App Settings | None (Required for orders) |
| `Razorpay__KeySecret`<br>(`Razorpay:KeySecret`) | **YES** | **Yes** | Razorpay TEST Key Secret | Razorpay LIVE Key Secret | Azure App Service App Settings | None (Required for HMAC check) |
| `Razorpay__WebhookSecret`<br>(`Razorpay:WebhookSecret`) | **YES** | **Yes** | Webhook secret for test endpoint verification | Webhook secret for live endpoint verification | Azure App Service App Settings | None (Required for webhook signature verification) |
| `Razorpay__AcceptanceTestMode`<br>(`Razorpay:AcceptanceTestMode`) | No | No | `true` (enables acceptance testing with `rzp_test_` keys in production-like environment) | `false` (enforces strict `rzp_live_` key requirement) | Azure App Service App Settings / VPS Env | `false` |
| `CloudflareR2__AccountId`<br>(`CloudflareR2:AccountId`) | **YES** | **Yes** | Cloudflare Account ID for TEST R2 bucket | Cloudflare Account ID for PROD R2 bucket | Azure App Service App Settings | None (Throws in Prod if empty) |
| `CloudflareR2__AccessKeyId`<br>(`CloudflareR2:AccessKeyId`) | **YES** | **Yes** | R2 API Token Access Key ID for `ethos-test-media` | R2 API Token Access Key ID for `ethos-production-media` | Azure App Service App Settings | None (Throws in Prod if empty) |
| `CloudflareR2__SecretAccessKey`<br>(`CloudflareR2:SecretAccessKey`) | **YES** | **Yes** | R2 API Token Secret Access Key for `ethos-test-media` | R2 API Token Secret Access Key for `ethos-production-media` | Azure App Service App Settings | None (Throws in Prod if empty) |
| `CloudflareR2__BucketName`<br>(`CloudflareR2:BucketName`) | No | **Yes** | `ethos-test-media` | `ethos-production-media` | Azure App Service App Settings | `ethos-production-media` |
| `CloudflareR2__PublicDomain`<br>(`CloudflareR2:PublicDomain`) | No | **Yes** | `https://test-media.ethosdancestudio.com` or `https://pub-test-*.r2.dev` | `https://media.ethosdancestudio.com` | Azure App Service App Settings | `https://media.ethosdancestudio.com` |
| `Cors__AllowedOrigins__0`<br>(`Cors:AllowedOrigins:0`) | No | **Yes** | Test frontend domain (e.g. `https://ethos-test.netlify.app`) | `https://ethosdancestudio.com` | Azure App Service App Settings | Pre-configured in appsettings |
| `Cors__AllowedOrigins__1`<br>(`Cors:AllowedOrigins:1`) | No | No | Second test domain (if applicable) | `https://www.ethosdancestudio.com` | Azure App Service App Settings | Pre-configured in appsettings |
| `Msg91__Enabled`<br>(`Msg91:Enabled`) | No | **Yes** | `false` initially; `true` during controlled WhatsApp testing | `false` initially; `true` at final Go-Live | Azure App Service App Settings | `false` |
| `Msg91__AuthKey`<br>(`Msg91:AuthKey`) | **YES** | Conditional | Real/Test MSG91 Auth Key (required when Enabled=true) | Live MSG91 Auth Key (required when Enabled=true) | Azure App Service App Settings | None (Validated in Prod when Enabled=true) |
| `Msg91__IntegratedNumber`<br>(`Msg91:IntegratedNumber`) | No | Conditional | Registered MSG91 WhatsApp number (e.g. `919...`) | Registered MSG91 WhatsApp number (e.g. `919...`) | Azure App Service App Settings | None (Validated in Prod when Enabled=true) |
| `Msg91__AllowTestEndpoints`<br>(`Msg91:AllowTestEndpoints`) | No | No | `true` in TEST (enables admin test dispatch endpoints) | `false` in PROD (blocks test dispatch endpoints) | Azure App Service App Settings | `false` |
| `Msg91__ApprovedTestNumbers__0`<br>(`Msg91:ApprovedTestNumbers:0`) | No | Conditional | Tester's whitelisted phone number (E.164 without `+`) | *(Leave empty in PROD — dispatches to real customers)* | Azure App Service App Settings | Empty list |
| `Seed__TestTrainerPassword`<br>(`Seed:TestTrainerPassword`) | **YES** | Dev/Test only | Password for seeded test trainer account | *(Leave empty in PROD — test seeding disabled)* | Azure App Service App Settings / User Secrets | None |
| `Seed__SecondTestTrainerPassword`<br>(`Seed:SecondTestTrainerPassword`) | **YES** | Dev/Test only | Password for second test trainer account | *(Leave empty in PROD — test seeding disabled)* | Azure App Service App Settings / User Secrets | None |
| **Frontend Variables** | | | | | | |
| `VITE_API_BASE_URL` | No | **Yes** | `https://ethos-test-api-app.azurewebsites.net` | `https://api.ethosdancestudio.com` | Netlify Site Config / `.env.production` | `""` (relative origin fallback) |
| `VITE_RAZORPAY_KEY_ID` | No | **Yes** | Razorpay TEST Key ID (`rzp_test_...`) | Razorpay LIVE Key ID (`rzp_live_...`) | Netlify Site Config / `.env.production` | `""` |

---

## ASP.NET Core Azure App Service Naming Syntax Note
In Azure Linux App Service, hierarchical configuration keys can be supplied using **double underscores** (`__`) in place of colons (`:`).
For example:
- `ConnectionStrings:DefaultConnection` -> `ConnectionStrings__DefaultConnection`
- `Jwt:SecretKey` -> `Jwt__SecretKey`
- `Razorpay:KeyId` -> `Razorpay__KeyId`
- `Razorpay:AcceptanceTestMode` -> `Razorpay__AcceptanceTestMode`
- `CloudflareR2:BucketName` -> `CloudflareR2__BucketName`
- `Msg91:Enabled` -> `Msg91__Enabled`
