# Neon PostgreSQL — PRODUCTION Environment Guide

> **ENVIRONMENT**: **PRODUCTION (PROD)**  
> **Target Database**: `ethos-prod-db`  
> **Status**: ⏳ Provision AFTER full TEST/UAT sign-off

---

## 1. Resource Naming Convention

| Resource Type | Resource Name | Purpose |
|---|---|---|
| Neon Project | `ethos-prod` | Dedicated production database compute & storage |
| Neon Database | `ethos-prod-db` | Production transactional database |
| Database Role / User | `ethos_prod_admin` | Dedicated production application user |
| Region | Closest to Azure `ethos-prod-api-app` region | Minimizes query latency |

---

## 2. Step-by-Step Creation Guide (Manual Neon Console)

> [!CAUTION]
> Ensure you are creating an entirely new Neon Project (`ethos-prod`), completely distinct from `ethos-test`.

### Step 1: Create Production Project
1. Log in to [Neon Console](https://console.neon.tech).
2. Click **New Project**.
3. **Project Name**: `ethos-prod`.
4. **Postgres Version**: Postgres 16.
5. **Region**: Match or align with Azure production region.
6. Click **Create Project**.

### Step 2: Create Production Database
1. Navigate to **Databases** -> **New Database**.
2. **Database Name**: `ethos-prod-db`.
3. **Owner**: Select `ethos_prod_admin`.
4. Click **Create**.

### Step 3: Copy Authoritative Connection String
1. Select **Pooled connection** string (uses Neon PgBouncer for optimal high-concurrency connection pooling).
2. Ensure `sslmode=require` is present.
3. Save connection string directly into Azure App Service `ConnectionStrings__DefaultConnection` (or Azure Key Vault).

---

## 3. Production Migration & Seed Execution

In production, automatic EF Core migrations on application startup are **strictly disabled** (`app.Environment.IsDevelopment()` guard). Migrations must be executed via controlled deployment pipeline or CLI step:

```powershell
# Set temporary connection string to ethos-prod-db
$env:ConnectionStrings__DefaultConnection="postgresql://<prod-user>:<prod-pass>@<neon-prod-pooler-host>/ethos-prod-db?sslmode=require"

# Apply EF Core migrations
dotnet ef database update --project Ethos.Api --startup-project Ethos.Api

# Clear temporary environment variable
$env:ConnectionStrings__DefaultConnection=""
```

Verify that the production database contains all required tables and indexes.

---

## 4. Production Safety Rules

> [!CAUTION]
> 1. Never execute seed scripts containing test passwords or dummy users against `ethos-prod-db`.
> 2. Daily automated backups / Point-In-Time Restore (PITR) must be enabled in Neon.
> 3. Never run `DropDatabase`, reset scripts, or unverified manual SQL queries against `ethos-prod-db`.
