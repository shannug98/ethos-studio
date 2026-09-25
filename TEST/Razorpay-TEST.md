# Razorpay Payment Gateway — TEST Environment Guide

> **ENVIRONMENT**: **TEST ONLY**  
> **Key Prefix**: `rzp_test_*`  
> **Status**: Ready for configuration

---

## 1. Environment Mode & Key Invariants

| Setting | TEST Mode Requirement | Critical Safety Check |
|---|---|---|
| Dashboard Mode | **Test Mode** (orange badge in Razorpay Dashboard) | Ensure badge says "Test Mode", NOT "Live" |
| Key ID Prefix | `rzp_test_...` | MUST start with `rzp_test_` |
| Key Secret | Test Key Secret | Secret generated in Test mode |
| Webhook URL | `https://ethos-test-api-app.azurewebsites.net/api/payments/webhook` | Points to TEST Azure Web App |
| Webhook Secret | Configured matching `Razorpay__WebhookSecret` | Distinct from production webhook secret |

> [!CAUTION]
> **Financial Safety Guard**:
> If a key starting with `rzp_live_` is configured in the TEST environment, real credit cards or bank accounts will be charged during testing. Razorpay keys must be strictly validated to begin with `rzp_test_`.

---

## 2. Step-by-Step Configuration Guide

Follow these steps in the [Razorpay Dashboard](https://dashboard.razorpay.com):

### Step 1: Switch to Test Mode
1. Log in to the Razorpay Dashboard.
2. In the top navbar or sidebar, toggle the environment switch to **Test Mode**. The dashboard header will display an amber/orange "Test Mode" badge.

### Step 2: Generate Test API Keys
1. Go to **Settings** (or **Account & Settings**) -> **API Keys**.
2. Click **Generate Test Key** (or **Regenerate Test Key** if one exists).
3. Record the generated:
   - **Key ID**: starts with `rzp_test_...`
   - **Key Secret**: keep confidential
4. Store these securely for Azure App Service configuration.

### Step 3: Configure Test Webhook
1. In **Settings** -> **Webhooks**.
2. Click **Add New Webhook**.
3. **Webhook URL**: Enter `https://ethos-test-api-app.azurewebsites.net/api/payments/webhook`.
4. **Secret**: Enter a strong random secret (min 32 characters, e.g. generated via password manager).
5. **Alert Email**: Enter your developer/test email.
6. **Active Events**: Check the following events:
   - `order.paid`
   - `payment.captured`
   - `payment.failed`
   - `refund.processed`
   - `refund.failed`
7. Click **Create Webhook**.

---

## 3. Test Payment Instrumentation & Verification

When running User Acceptance Testing (UAT) in the TEST frontend:
- Use Razorpay's official test payment cards (e.g. Card Number: `4111 1111 1111 1111`, any future expiry date, CVV: `123`, OTP: `123456`).
- For UPI test flows, enter `success@razorpay` to simulate approved payments, or `failure@razorpay` to simulate declined payments.
- Verify in `ethos-test-db` that `payment_transactions` status transitions from `Created` -> `Completed`, and `workshop_bookings` transitions to `Confirmed`.
