# Ethos Dance Studio — Production Platform

Production website and administration platform for **Ethos Dance Studio**.

## Architecture Overview

This monorepo contains the decoupled production system:

* **`Ethos.Api`**: Production backend built with ASP.NET Core 9 Web API, Entity Framework Core, Supabase PostgreSQL, and Cloudflare R2 object storage.
* **`Ethos.Web`**: Production client built with React 18, Vite, and React Router v6.

---

## 1. Backend (`Ethos.Api`)

### Requirements
* .NET 9.0 SDK or .NET 10.0 SDK
* Supabase PostgreSQL instance
* Cloudflare R2 bucket (`ethos-production-media`)

### Configuration
Configuration is managed via `appsettings.json` and environment variables in Azure App Service:
* `ConnectionStrings:DefaultConnection`: PostgreSQL connection string
* `CloudflareR2:AccountId`: Cloudflare account identifier
* `CloudflareR2:AccessKeyId` & `SecretAccessKey`: R2 S3-compatible API credentials
* `CloudflareR2:BucketName`: `ethos-production-media`
* `CloudflareR2:PublicDomain`: `https://media.ethosdancestudio.com`
* `Jwt`: Production JWT authentication settings
* `Razorpay`: Payment gateway keys (Test keys in dev, Live keys for production)

### Build & Run
```bash
cd Ethos.Api
dotnet restore
dotnet build
dotnet run
```

---

## 2. Frontend (`Ethos.Web`)

### Requirements
* Node.js v18+ & npm

### Configuration
Create `.env.production` based on `.env.production.example`:
```env
VITE_API_BASE_URL=https://api.ethosdancestudio.com
VITE_RAZORPAY_KEY_ID=rzp_live_...
```

### Build & Run
```bash
cd Ethos.Web
npm install
npm run dev   # Local development server
npm run build # Production bundle in dist/
```

---

## Key Features & Production Hardening

* **Cloudflare R2 Media Integration**: Streaming-optimized media handling with magic-byte signature validation and Supabase PostgreSQL metadata tracking.
* **Workshop Publishing Workflow**: Server-authorized status progression (`Draft` → `Published` → `Unpublished` → `Archived`). Public endpoints strictly serve `Published` workshops.
* **Preserved Brand Aesthetics**: Pixel-perfect layout preservation of the existing Homepage, About, Founders, Faculty, Classes, and Footer.
* **Login Coming Soon Experience**: Navigation Login opens an on-brand modal directing visitors to contact channels, while preserving active `/admin_portal` routes for administrators.
* **Security & Observability**: Server-side permission guards, rate limiting, request trace IDs, and comprehensive admin audit logging.
