# Cloudflare R2 — TEST Environment Guide

> **ENVIRONMENT**: **TEST ONLY**  
> **Target Bucket**: `ethos-test-media`  
> **Status**: Ready for provisioning

---

## 1. Resource Naming Convention

| Resource Type | Resource Name | Purpose |
|---|---|---|
| R2 Bucket | `ethos-test-media` | Authoritative storage for test trainer audition videos and gallery uploads |
| R2 API Token | `ethos-test-r2-token` | Scoped token for read/write on `ethos-test-media` only |
| Public Access / CDN | `https://test-media.ethosdancestudio.com`<br>*(or `https://pub-test-*.r2.dev`)* | Public asset serving domain for test media |

---

## 2. Step-by-Step Creation Guide (Manual Cloudflare Dashboard)

Because R2 provisioning requires Cloudflare account access, complete the following steps in the [Cloudflare Dashboard](https://dash.cloudflare.com):

### Step 1: Create the TEST Bucket
1. Log in to Cloudflare and navigate to **R2 Storage** in the sidebar.
2. Click **Create bucket**.
3. **Bucket name**: Enter `ethos-test-media` (Do NOT name it `ethos-production-media`).
4. **Location**: Select **Automatic** or your preferred region.
5. Click **Create bucket**.

### Step 2: Configure CORS Policy
In `ethos-test-media` bucket settings -> **CORS Policy** -> click **Add CORS policy** and add:

```json
[
  {
    "AllowedOrigins": [
      "https://ethos-test.netlify.app",
      "https://ethos-test-api-app.azurewebsites.net",
      "http://localhost:5173",
      "http://localhost:5252"
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
    "MaxAgeSeconds": 3600
  }
]
```

### Step 3: Configure Public Access / Domain
1. In bucket settings -> **Public Access**.
2. Either:
   - Click **Allow Access** under **R2.dev subdomain** (convenient for test environments; yields a URL like `https://pub-xxxxxx.r2.dev`), or
   - Click **Connect Domain** and add a test subdomain: `test-media.ethosdancestudio.com`.

### Step 4: Create Scoped API Token
1. In Cloudflare Dashboard -> **R2** -> click **Manage R2 API Tokens** (on the right).
2. Click **Create API Token**.
3. **Token name**: `ethos-test-r2-token`.
4. **Permissions**: Select **Object Read & Write**.
5. **Apply to specific buckets only**: Select **`ethos-test-media`** (Crucial: do not give access to all buckets).
6. Click **Create API Token**.
7. Copy the generated credentials:
   - **Account ID**
   - **Access Key ID**
   - **Secret Access Key**

---

## 3. Environment Variable Values (Template)

Configure in Azure App Service (`ethos-test-api-app`):

```text
CloudflareR2__AccountId=<your-cloudflare-account-id>
CloudflareR2__AccessKeyId=<r2-test-token-access-key-id>
CloudflareR2__SecretAccessKey=<r2-test-token-secret-access-key>
CloudflareR2__BucketName=ethos-test-media
CloudflareR2__PublicDomain=https://pub-test-xxxx.r2.dev (or https://test-media.ethosdancestudio.com)
```

---

## 4. Safety & Isolation Guardrails

> [!CAUTION]
> **Strict Verification Rules**:
> 1. Verify `CloudflareR2__BucketName` is set to `ethos-test-media`.
> 2. Ensure the R2 API token is scoped ONLY to `ethos-test-media`. If the token is scoped to all buckets, an accidental delete operation in TEST could affect production buckets.
> 3. Never point the production application to `ethos-test-media`.
