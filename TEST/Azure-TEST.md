# Azure App Service — TEST Environment Guide

> **ENVIRONMENT**: **TEST ONLY**  
> **Target Resource**: `ethos-test-api-app`  
> **Status**: Ready for provisioning

---

## 1. Resource Naming Convention

| Resource Type | Resource Name | Purpose |
|---|---|---|
| Resource Group | `rg-ethos-test` | Isolates all TEST resources in Azure |
| App Service Plan | `asp-ethos-test` | Linux compute plan (Pricing tier: `B1` or `P1v2`) |
| Web App (App Service) | `ethos-test-api-app` | Hosts the backend .NET 10 Web API for TEST |
| App Service Default URL | `https://ethos-test-api-app.azurewebsites.net` | Public TEST backend endpoint |

---

## 2. Step-by-Step Creation Guide (Manual Azure Portal)

Since creating an Azure App Service requires your personal Azure subscription and account credentials, perform the following steps in the [Azure Portal](https://portal.azure.com):

### Step 1: Create Resource Group
1. In the search bar, search for **Resource groups** and click **Create**.
2. **Subscription**: Select your active Azure subscription.
3. **Resource group**: Enter `rg-ethos-test`.
4. **Region**: Select your preferred region (e.g. `Central India` or `Southeast Asia`).
5. Click **Review + create** -> **Create**.

### Step 2: Create Linux App Service Plan
1. Search for **App Service Plans** -> click **Create**.
2. **Resource Group**: Select `rg-ethos-test`.
3. **Name**: Enter `asp-ethos-test`.
4. **Operating System**: Select **Linux**.
5. **Region**: Same region as `rg-ethos-test`.
6. **Pricing Plan**: Select **Basic B1** (or **Premium V2 P1v2** for always-on/enhanced performance).
7. Click **Review + create** -> **Create**.

### Step 3: Create Web App
1. Search for **App Services** -> click **Create** -> **Web App**.
2. **Resource Group**: Select `rg-ethos-test`.
3. **Name**: Enter `ethos-test-api-app` (Azure will verify availability: `ethos-test-api-app.azurewebsites.net`).
4. **Publish**: Select **Code**.
5. **Runtime stack**: Select **.NET 10** (or .NET 9/10 LTS Linux).
6. **Operating System**: **Linux**.
7. **Region**: Same region.
8. **App Service Plan**: Select `asp-ethos-test`.
9. Click **Review + create** -> **Create**.

---

## 3. General Configuration & Hardening

Once the Web App is provisioned:

1. Navigate to `ethos-test-api-app` -> **Configuration** (under Settings) -> **General settings**:
   - **Always On**: Turn **On** (keeps background worker processes active).
   - **HTTPS Only**: Turn **On** (redirects all HTTP to HTTPS).
   - **Minimum TLS Version**: Set to **1.2**.
2. Click **Save**.

---

## 4. Environment Variables Configuration

Navigate to **Configuration** -> **Application settings** and add the variables detailed in [`TEST/Environment-Variables-TEST.md`](file:///d:/ETHOS%20DANCE%20studio/TEST/Environment-Variables-TEST.md).

> [!WARNING]
> Ensure that `ConnectionStrings__DefaultConnection` points strictly to `ethos-test-db` (Neon TEST) and never to production.
