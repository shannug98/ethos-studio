# Razorpay Payment Gateway — PRODUCTION Environment Guide

> **ENVIRONMENT**: **PRODUCTION (PROD)**  
> **Key Prefix**: `rzp_live_*`  
> **Status**: ⏳ Configure AFTER full TEST/UAT sign-off

---

## 1. Environment Mode & Key Invariants

| Setting | PRODUCTION Requirement | Critical Safety Check |
|---|---|---|
| Dashboard Mode | **Live Mode** (Green badge in Razorpay Dashboard) | MUST say "Live Mode", NOT "Test" |
| Key ID Prefix | `rzp_live_...` | MUST start with `rzp_live_` |
| Key Secret | Live Key Secret | Generated in Live mode |
| Webhook URL | `https://api.ethosdancestudio.com/api/payments/webhook` | Points to production API domain |
| Webhook Secret | Configured matching `Razorpay__WebhookSecret` | Cryptographically random min 32-character secret |

> [!CAUTION]
> **Strict Financial Safeguard**:
> Never configure `rzp_test_*` credentials in the production environment. Using test keys in production will cause real customers to attempt payment against a sandbox, resulting in uncaptured payments, failed reconciliations, and ticket fulfillment halts.

---

## 2. Step-by-Step Configuration Guide

Follow these steps in the [Razorpay Dashboard](https://dashboard.razorpay.com):

### Step 1: Switch to Live Mode
1. Log in to Razorpay Dashboard.
2. Toggle the environment switch to **Live Mode**. Verify the top bar badge turns green ("Live Mode").

### Step 2: Generate Production API Keys
1. Navigate to **Settings** -> **API Keys**.
2. Click **Generate Live Key**.
3. Record:
   - **Key ID**: starts with `rzp_live_...`
   - **Key Secret**: keep strictly confidential
4. Store these securely in Azure App Service application settings (or Azure Key Vault).

### Step 3: Configure Production Webhook
1. Go to **Settings** -> **Webhooks**.
2. Click **Add New Webhook**.
3. **Webhook URL**: `https://api.ethosdancestudio.com/api/payments/webhook`.
4. **Secret**: Enter a freshly generated 32+ character random secret.
5. **Alert Email**: Dedicated production alerts inbox (e.g. `tech@ethosdancestudio.com`).
6. **Active Events**:
   - `order.paid`
   - `payment.captured`
   - `payment.failed`
   - `refund.processed`
   - `refund.failed`
7. Click **Create Webhook**.

---

## 3. Production Verification & Settlement Checks

- Ensure your KYC and bank account details are verified and active for automated settlements.
- Perform a minimal real transaction (e.g. ₹1 / minimal pass) upon initial Go-Live to verify live webhook delivery, HMAC verification, ticket issuance, and bank capture.
