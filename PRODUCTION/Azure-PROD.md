# Azure App Service — PRODUCTION Environment Guide

> **ENVIRONMENT**: **PRODUCTION (PROD)**  
> **Target Resource**: `ethos-prod-api-app`  
> **Status**: ⏳ Provision AFTER full TEST/UAT sign-off

---

## 1. Resource Naming Convention

| Resource Type | Resource Name | Purpose |
|---|---|---|
| Resource Group | `rg-ethos-prod` | Dedicated production resource boundary |
| App Service Plan | `asp-ethos-prod` | Linux compute plan (Recommended tier: `P1v2` or `P2v2` with autoscaling) |
| Web App (App Service) | `ethos-prod-api-app` | Production backend .NET 10 API |
| Custom Domain Binding | `https://api.ethosdancestudio.com` | Authoritative public API domain |
| Default URL | `https://ethos-prod-api-app.azurewebsites.net` | Fallback Azure URL |

---

## 2. Step-by-Step Creation Guide (Manual Azure Portal)

> [!WARNING]
> Do NOT create or configure these production resources until all TEST environment stages, database verification, R2 verification, payment processing, and UAT have passed.

### Step 1: Create Production Resource Group
1. In [Azure Portal](https://portal.azure.com), navigate to **Resource groups** -> **Create**.
2. **Resource group**: Enter `rg-ethos-prod`.
3. **Region**: Select primary production region (e.g. `Central India`).
4. Click **Review + create** -> **Create**.

### Step 2: Create Production App Service Plan
1. Navigate to **App Service Plans** -> **Create**.
2. **Resource Group**: `rg-ethos-prod`.
3. **Name**: `asp-ethos-prod`.
4. **Operating System**: **Linux**.
5. **Pricing Plan**: **P1v2** (1 vCPU, 3.5 GB RAM, Auto-scale, SLA, Custom Domains, SSL).
6. Click **Review + create** -> **Create**.

### Step 3: Create Production Web App
1. Navigate to **App Services** -> **Create** -> **Web App**.
2. **Resource Group**: `rg-ethos-prod`.
3. **Name**: `ethos-prod-api-app`.
4. **Publish**: **Code**.
5. **Runtime stack**: **.NET 10** (or LTS Linux).
6. **Operating System**: **Linux**.
7. **App Service Plan**: `asp-ethos-prod`.
8. Click **Review + create** -> **Create**.

---

## 3. Production Hardening & Custom Domain Setup

1. **General settings**:
   - **Always On**: **On** (mandatory for outbox workers, ticket PDF generators, and refund processors).
   - **HTTPS Only**: **On**.
   - **Minimum TLS Version**: **1.2**.
   - **HTTP Version**: **2.0**.
2. **Custom Domains**:
   - Add Custom Domain: `api.ethosdancestudio.com`.
   - Add CNAME record in DNS provider pointing `api` to `ethos-prod-api-app.azurewebsites.net`.
   - Add TXT verification record `asuid.api` if required.
   - Bind free App Service Managed Certificate or upload SSL/TLS certificate.

---

## 4. Configuration & Secrets

Configure production application settings as specified in [`PRODUCTION/Environment-Variables-PROD.md`](file:///d:/ETHOS%20DANCE%20studio/PRODUCTION/Environment-Variables-PROD.md).
All secrets must come from production Key Vault or secure Azure App Service configuration.
