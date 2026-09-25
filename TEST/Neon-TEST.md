# Neon PostgreSQL — TEST Environment Guide

> **ENVIRONMENT**: **TEST ONLY**  
> **Target Database**: `ethos-test-db`  
> **Status**: Ready for provisioning

---

## 1. Resource Naming Convention

| Resource Type | Resource Name | Purpose |
|---|---|---|
| Neon Project | `ethos-test` | Isolates development/test compute and storage |
| Neon Database | `ethos-test-db` | Database schema and tables for TEST |
| Database Role / User | `ethos_test_user` | Dedicated role with full permissions on `ethos-test-db` |
| Region | `ap-southeast-1` (Singapore) or closest to Azure | Minimizes database latency to Azure App Service |

---

## 2. Step-by-Step Creation Guide (Manual Neon Console)

Because creating a database project requires your Neon account credentials, follow these steps in the [Neon Console](https://console.neon.tech):

### Step 1: Create a Dedicated TEST Project
1. Log in to your Neon Console.
2. Click **New Project**.
3. **Project Name**: Enter `ethos-test` (Do NOT name it `ethos` or `ethos-dance` without `-test`).
4. **Postgres Version**: Choose Postgres 16 (or latest stable supported).
5. **Region**: Choose the region closest to your Azure App Service region (e.g. Asia/Singapore or Europe/US).
6. Click **Create Project**.

### Step 2: Create the TEST Database
1. Inside the `ethos-test` project dashboard, navigate to **Databases**.
2. Click **New Database**.
3. **Database Name**: Enter `ethos-test-db`.
4. **Owner**: Select `ethos_test_user` (or the default project user).
5. Click **Create**.

### Step 3: Copy Connection String
1. On the project dashboard, select the **Connection Details** widget.
2. Select database: `ethos-test-db`.
3. Choose **Pooled connection** (recommended for web applications) or **Direct connection**.
4. Ensure `sslmode=require` is present in the query parameters.
5. Example format:
   ```text
   postgresql://<username>:<password>@ep-test-sample-pooler.ap-southeast-1.aws.neon.tech/ethos-test-db?sslmode=require
   ```
6. **Save this securely** to configure in Azure App Service `ConnectionStrings__DefaultConnection`. Do NOT paste it into any Git-tracked file.

---

## 3. Initial Schema Setup & Migration

Once the connection string is obtained, initialize the database schema from your local terminal using the EF Core CLI:

```powershell
# Set connection string in temporary shell environment variable (never hardcoded in JSON)
$env:ConnectionStrings__DefaultConnection="postgresql://<username>:<password>@<neon-host>/ethos-test-db?sslmode=require"

# Apply all EF Core migrations to ethos-test-db
dotnet ef database update --project Ethos.Api --startup-project Ethos.Api

# Clear the temporary shell variable
$env:ConnectionStrings__DefaultConnection=""
```

Verify that all tables (such as `workshops`, `workshop_sessions`, `workshop_passes`, `workshop_bookings`, `workshop_tickets`, `ticket_pdfs`, `payment_transactions`, `media_items`) are created successfully in `ethos-test-db`.

---

## 4. Safety & Isolation Guardrails

> [!CAUTION]
> **Strict Verification Rules**:
> 1. Check that the database name in the connection string contains `-test-db`.
> 2. Never connect the local production pipeline or Azure PROD App Service to `ethos-test-db`.
> 3. Never run destructive reset scripts against anything other than `ethos-test-db`.
