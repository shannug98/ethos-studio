# MSG91 WhatsApp Integration — PRODUCTION Environment Guide

> **ENVIRONMENT**: **PRODUCTION (PROD)**  
> **Status**: ⏳ Configure during final Go-Live phase

---

## 1. Environment & Operational Invariants

| Setting | PRODUCTION Requirement | Purpose |
|---|---|---|
| `Msg91:Enabled` | `false` until verified, then `true` | Prevents outbound calls before live readiness |
| `Msg91:AllowTestEndpoints` | `false` | Blocks test dispatch controller in production |
| `Msg91:ApprovedTestNumbers` | Empty list | In production, sends to actual booking attendees |
| Outbox Worker Polling | 5 seconds (active in background) | Authoritative background worker processes pending notifications |
| Retry & Leases | 3 attempts, 60s lease | Idempotent dispatch with lease acquisition |

---

## 2. Prerequisites for Production MSG91 Go-Live

1. **Meta / WhatsApp Business Verification**:
   - Business verification verified and active in Meta Business Manager.
   - MSG91 Integrated WhatsApp sender number verified and active.
2. **Template Approvals in MSG91**:
   - Template 1: `ethos_booking_confirmed` (6 variables: `{{1}}` AttendeeName, `{{2}}` WorkshopTitle, `{{3}}` WorkshopDate, `{{4}}` WorkshopTime, `{{5}}` Location, `{{6}}` BookingId).
   - Template 2: `ethos_ticket_pdf` (Document header + 6 body variables matching booking confirmation).
   - Status in MSG91 / Meta: **Approved**.
3. **Production R2 Public Domain**:
   - `https://media.ethosdancestudio.com` must be live and accessible over HTTPS so MSG91 WhatsApp servers can fetch ticket PDFs via signed URLs.

---

## 3. Production Environment Variables (Template)

Configure in Azure App Service (`ethos-prod-api-app`):

```ini
# Initially configure as false until final readiness check
Msg91__Enabled=false
Msg91__AuthKey=<production-msg91-auth-key>
Msg91__IntegratedNumber=<production-registered-sender-number>
Msg91__AllowTestEndpoints=false
```

When ready to enable live WhatsApp messaging to real customers:
1. Update `Msg91__Enabled=true` in Azure App Service settings.
2. Restart `ethos-prod-api-app`.
3. Background dispatcher will process pending outbox notifications idempotently.
