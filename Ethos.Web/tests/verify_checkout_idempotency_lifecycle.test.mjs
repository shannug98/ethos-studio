import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT_DIR = path.resolve(__dirname, "..");

test("Checkout Idempotency-Key Lifecycle & Protection Verification", async (t) => {
  const checkoutPagePath = path.join(ROOT_DIR, "src", "pages", "WorkshopCheckoutPage.jsx");
  const detailsPagePath = path.join(ROOT_DIR, "src", "pages", "WorkshopDetailsPage.jsx");
  const workshopsApiPath = path.join(ROOT_DIR, "src", "services", "workshopsApi.js");
  const workshopServicePath = path.join(ROOT_DIR, "..", "Ethos.Api", "Application", "Workshops", "WorkshopService.cs");

  const checkoutJsx = fs.readFileSync(checkoutPagePath, "utf-8");
  const detailsJsx = fs.readFileSync(detailsPagePath, "utf-8");
  const workshopsApiJs = fs.readFileSync(workshopsApiPath, "utf-8");
  const workshopServiceCs = fs.readFileSync(workshopServicePath, "utf-8");

  await t.test("1. Backend Idempotency Protection Invariant (Preserved & Unweakened)", () => {
    // Must check existing booking by IdempotencyKey
    assert.match(
      workshopServiceCs,
      /b\.IdempotencyKey\s*==\s*idempotencyKey/,
      "Backend must query existing booking by IdempotencyKey"
    );

    // Must validate request fingerprint parameters (Pass, Quantity, Sessions)
    assert.match(
      workshopServiceCs,
      /!matchesPass\s*\|\|\s*!matchesQuantity\s*\|\|\s*!matchesSessions/,
      "Backend must enforce strict fingerprinting matching pass, quantity, and selected sessions"
    );

    // Must reject mismatched parameters with 409 conflict
    assert.match(
      workshopServiceCs,
      /InvalidOperationException\("Idempotency key was previously used with different order parameters\."\)/,
      "Backend must throw when idempotency key is reused with different parameters"
    );
  });

  await t.test("2. Dynamic Idempotency Key Regeneration on Changed Order Parameters", () => {
    // Checkout page must regenerate key when pass, quantity, sessions, or workshop change
    assert.match(
      checkoutJsx,
      /useEffect\(\(\)\s*=>\s*\{[\s\S]*?setIdempotencyKey\([\s\S]*?\);\s*\}\s*,\s*\[[\s\S]*?passTypeId[\s\S]*?quantity[\s\S]*?sortedSessionIdsKey[\s\S]*?\]\)/,
      "WorkshopCheckoutPage must regenerate idempotency key when material order parameters change"
    );
  });

  await t.test("3. Navigation Fresh Attempt Lifecycle (Back -> Forward / Return to Checkout)", () => {
    // WorkshopDetailsPage must generate a fresh attempt ID when navigating to checkout
    assert.match(
      detailsJsx,
      /checkoutAttemptId:\s*crypto\.randomUUID\(\)/,
      "WorkshopDetailsPage must generate a fresh checkoutAttemptId on navigation"
    );

    // WorkshopCheckoutPage must consume checkoutAttemptId on mount
    assert.match(
      checkoutJsx,
      /stateData\.checkoutAttemptId\s*\|\|\s*crypto\.randomUUID\(\)/,
      "WorkshopCheckoutPage must initialize idempotency key with fresh checkoutAttemptId"
    );
  });

  await t.test("4. Auto-Healing Idempotency 409 Conflict Recovery", () => {
    // If a 409 idempotency conflict occurs, client must auto-recover with fresh attempt key
    assert.match(
      checkoutJsx,
      /orderErr\?\.status\s*===\s*409[\s\S]*?errMsg\.includes\("different order parameters"\)[\s\S]*?const freshKey\s*=\s*crypto\.randomUUID\(\)[\s\S]*?orderPayload\.idempotencyKey\s*=\s*freshKey/,
      "WorkshopCheckoutPage must auto-heal 409 idempotency conflicts by retrying with a fresh key"
    );
  });

  await t.test("5. Accidental Double-Click & Idempotent Retry Protection", () => {
    // Submission flag locks duplicate submissions
    assert.match(
      checkoutJsx,
      /setIsSubmitting\(true\)/,
      "WorkshopCheckoutPage must set submitting lock during payment processing"
    );

    // workshopsApi creates a key if omitted to ensure backend always gets idempotency protection
    assert.match(
      workshopsApiJs,
      /if\s*\(!data\.idempotencyKey\)\s*\{\s*data\.idempotencyKey\s*=\s*crypto\.randomUUID\(\);\s*\}/,
      "workshopsApi must ensure idempotencyKey is always present on order creation"
    );
  });
});
