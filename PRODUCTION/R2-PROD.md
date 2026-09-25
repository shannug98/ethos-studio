# Cloudflare R2 — PRODUCTION Environment Guide

> **ENVIRONMENT**: **PRODUCTION (PROD)**  
> **Target Bucket**: `ethos-production-media`  
> **Status**: ⏳ Provision AFTER full TEST/UAT sign-off

---

## 1. Resource Naming Convention

| Resource Type | Resource Name | Purpose |
|---|---|---|
| R2 Bucket | `ethos-production-media` | Authoritative permanent storage for real trainer audition videos, tickets, and gallery media |
| R2 Custom Domain | `https://media.ethosdancestudio.com` | Authoritative public CDN domain for production assets |
| R2 API Token | `ethos-prod-r2-token` | Scoped API token with Object Read & Write on `ethos-production-media` only |

---

## 2. Step-by-Step Creation Guide (Manual Cloudflare Dashboard)

### Step 1: Create Production Bucket
1. Log in to [Cloudflare Dashboard](https://dash.cloudflare.com) -> **R2 Storage**.
2. Click **Create bucket**.
3. **Bucket name**: `ethos-production-media`.
4. Click **Create bucket**.

### Step 2: Configure Production CORS Policy
In `ethos-production-media` -> **Settings** -> **CORS Policy**:

```json
[
  {
    "AllowedOrigins": [
      "https://ethosdancestudio.com",
      "https://www.ethosdancestudio.com",
      "https://api.ethosdancestudio.com",
      "https://media.ethosdancestudio.com"
    ],
    "AllowedMethods": [
      "GET",
      "HEAD",
      "PUT",
      "POST",
      "DELETE"
    ],
    "AllowedHeaders": [
      "*"
    ],
    "ExposeHeaders": [
      "ETag"
    ],
    "MaxAgeSeconds": 86400
  }
]
```

### Step 3: Connect Custom Production Domain
1. In `ethos-production-media` -> **Settings** -> **Public Access** -> **Connect Domain**.
2. Enter: `media.ethosdancestudio.com`.
3. Cloudflare will automatically provision the DNS CNAME and SSL certificate.

### Step 4: Create Scoped Production API Token
1. In R2 -> **Manage R2 API Tokens** -> **Create API Token**.
2. **Token name**: `ethos-prod-r2-token`.
3. **Permissions**: **Object Read & Write**.
4. **Apply to specific bucket only**: **`ethos-production-media`**.
5. Click **Create API Token**.
6. Copy Account ID, Access Key ID, and Secret Access Key.

---

## 3. Production Environment Variables (Template)

Configure in Azure App Service (`ethos-prod-api-app`):

```text
CloudflareR2__AccountId=<production-cloudflare-account-id>
CloudflareR2__AccessKeyId=<r2-prod-token-access-key-id>
CloudflareR2__SecretAccessKey=<r2-prod-token-secret-access-key>
CloudflareR2__BucketName=ethos-production-media
CloudflareR2__PublicDomain=https://media.ethosdancestudio.com
```

---

## 4. Production Safety Rules

> [!CAUTION]
> 1. Never configure the TEST application or local dev scripts with `ethos-production-media` credentials.
> 2. Ensure R2 Object Versioning or automated bucket backups are enabled if available.
> 3. Verify public directory browsing is disabled on `media.ethosdancestudio.com`.
