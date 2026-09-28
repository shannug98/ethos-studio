import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webRoot = path.resolve(__dirname, "..");
const repoRoot = path.resolve(webRoot, "..");

describe("Finance UI & Business Flow: Payout Removal and Explicit Refund Workflow", () => {
  const adminPaymentsPath = path.join(webRoot, "src/pages/admin/AdminPayments.jsx");
  const adminPaymentsCssPath = path.join(webRoot, "src/pages/admin/AdminPayments.css");
  const adminApiPath = path.join(webRoot, "src/services/adminApi.js");
  const adminRouteRegistryPath = path.join(webRoot, "src/constants/adminRouteRegistry.js");

  const paymentsJsx = fs.readFileSync(adminPaymentsPath, "utf8");
  const paymentsCss = fs.readFileSync(adminPaymentsCssPath, "utf8");
  const apiJs = fs.readFileSync(adminApiPath, "utf8");
  const routeRegistryJs = fs.readFileSync(adminRouteRegistryPath, "utf8");

  it("1. Trainer Payouts are completely removed from frontend files", () => {
    // Assert no payout methods in adminApi.js
    assert.ok(
      !apiJs.includes("getTrainerPayouts"),
      "adminApi.js must not contain getTrainerPayouts"
    );
    assert.ok(
      !apiJs.includes("processTrainerPayout"),
      "adminApi.js must not contain processTrainerPayout"
    );

    // Assert no payout references in AdminPayments.jsx
    assert.ok(
      !paymentsJsx.toLowerCase().includes("trainer payout"),
      "AdminPayments.jsx must not contain 'trainer payout'"
    );
    assert.ok(
      !paymentsJsx.includes("payoutModal"),
      "AdminPayments.jsx must not contain payoutModal state or component"
    );
    assert.ok(
      !paymentsJsx.includes("handleConfirmPayout"),
      "AdminPayments.jsx must not contain handleConfirmPayout"
    );
    assert.ok(
      !paymentsJsx.includes("handleOpenPayout"),
      "AdminPayments.jsx must not contain handleOpenPayout"
    );
    assert.ok(
      !paymentsJsx.includes("payments-tabs"),
      "AdminPayments.jsx must not contain tab navigation for payouts"
    );

    // Assert route registry description does not mention trainer payouts
    assert.ok(
      !routeRegistryJs.toLowerCase().includes("trainer payouts"),
      "adminRouteRegistry.js must not mention trainer payouts"
    );
  });

  it("2. Record Refund Modal communicates explicit external gateway recording workflow", () => {
    // Modal title & tone
    assert.ok(
      paymentsJsx.includes('title="Record Refund"'),
      "Modal title must be 'Record Refund'"
    );
    assert.ok(
      paymentsJsx.includes('Maximum Refundable:'),
      "Modal subtitle must specify 'Maximum Refundable:'"
    );
    assert.ok(
      paymentsJsx.includes('className="ethos-white-modal"'),
      "AdminActionModal must use ethos-white-modal class"
    );

    // Explicit callout banner
    assert.ok(
      paymentsJsx.includes("refund-info-callout"),
      "Must render .refund-info-callout"
    );
    assert.ok(
      paymentsJsx.includes("Important:"),
      "Must contain Important: label"
    );
    assert.ok(
      paymentsJsx.includes(
        "This records a refund that has already been issued through the payment gateway. It does not initiate a new refund."
      ),
      "Must contain the exact callout text clarifying external gateway recording"
    );

    // Form labels
    assert.ok(
      paymentsJsx.includes("Refund Amount"),
      "Must include 'Refund Amount' label"
    );
    assert.ok(
      paymentsJsx.includes("Gateway Refund ID / Reference"),
      "Must include 'Gateway Refund ID / Reference' label"
    );
    assert.ok(
      paymentsJsx.includes("Administrative Justification"),
      "Must include 'Administrative Justification' label"
    );
    assert.ok(
      paymentsJsx.includes("Internal Audit Notes"),
      "Must include 'Internal Audit Notes' label"
    );

    // Submit button
    assert.ok(
      paymentsJsx.includes('"Recording Refund..." : "Record Refund"'),
      "Submit button must alternate between 'Recording Refund...' and 'Record Refund'"
    );
    assert.ok(
      !paymentsJsx.includes("Confirm & Record Refund"),
      "Old button text 'Confirm & Record Refund' must be replaced"
    );
  });

  it("3. CSS styling defines ethos-white-modal and refund callout styles", () => {
    // White modal overrides
    assert.ok(
      paymentsCss.includes(".admin-modal-container.ethos-white-modal"),
      "AdminPayments.css must define .ethos-white-modal on .admin-modal-container"
    );
    assert.ok(
      paymentsCss.includes("background: #ffffff !important"),
      "White modal must force white background"
    );
    assert.ok(
      paymentsCss.includes(".refund-info-callout"),
      "AdminPayments.css must define .refund-info-callout"
    );
    assert.ok(
      paymentsCss.includes(".record-refund-btn"),
      "AdminPayments.css must define .record-refund-btn"
    );

    // Obsolete payout classes removed
    assert.ok(
      !paymentsCss.includes(".payout-summary-banner"),
      ".payout-summary-banner should be removed from AdminPayments.css"
    );
    assert.ok(
      !paymentsCss.includes(".status-tag.status-paid"),
      "Obsolete payout .status-tag styles should be removed from AdminPayments.css"
    );
  });

  it("4. Refresh button provides clean secondary styling, aligned icon, and loading feedback", () => {
    // Refresh button structure
    assert.ok(
      paymentsJsx.includes('className="admin-btn secondary refresh-btn"'),
      "Refresh button must use secondary styling"
    );
    assert.ok(
      paymentsJsx.includes("↻"),
      "Refresh button must contain the ↻ icon"
    );
    assert.ok(
      paymentsJsx.includes('{refreshing ? "Refreshing..." : "Refresh"}'),
      "Refresh button must show 'Refreshing...' during refresh"
    );
    assert.ok(
      paymentsJsx.includes("disabled={refreshing || loading}"),
      "Refresh button must be disabled when refreshing or loading"
    );

    // Spin animation in CSS
    assert.ok(
      paymentsCss.includes(".spin-icon"),
      "AdminPayments.css must define .spin-icon"
    );
    assert.ok(
      paymentsCss.includes("@keyframes spin"),
      "AdminPayments.css must define @keyframes spin"
    );
  });

  it("5. Backend C# architecture has payout endpoints and services removed", () => {
    const controllerPath = path.join(
      repoRoot,
      "Ethos.Api/Controllers/Admin/AdminPaymentsController.cs"
    );
    const serviceInterfacePath = path.join(
      repoRoot,
      "Ethos.Api/Application/Admin/IAdminPaymentService.cs"
    );
    const contractsPath = path.join(
      repoRoot,
      "Ethos.Api/Contracts/Admin/AdminContracts.cs"
    );
    const trainerPayoutServicePath = path.join(
      repoRoot,
      "Ethos.Api/Application/Finance/TrainerPayoutService.cs"
    );

    // TrainerPayoutService file must not exist
    assert.strictEqual(
      fs.existsSync(trainerPayoutServicePath),
      false,
      "TrainerPayoutService.cs file must be deleted"
    );

    if (fs.existsSync(controllerPath)) {
      const controllerCode = fs.readFileSync(controllerPath, "utf8");
      assert.ok(
        !controllerCode.includes("GetTrainerPayouts"),
        "AdminPaymentsController must not have GetTrainerPayouts"
      );
      assert.ok(
        !controllerCode.includes("ProcessTrainerPayout"),
        "AdminPaymentsController must not have ProcessTrainerPayout"
      );
    }

    if (fs.existsSync(serviceInterfacePath)) {
      const serviceCode = fs.readFileSync(serviceInterfacePath, "utf8");
      assert.ok(
        !serviceCode.includes("GetTrainerPayoutsAsync"),
        "IAdminPaymentService must not have GetTrainerPayoutsAsync"
      );
      assert.ok(
        !serviceCode.includes("ProcessTrainerPayoutAsync"),
        "IAdminPaymentService must not have ProcessTrainerPayoutAsync"
      );
    }

    if (fs.existsSync(contractsPath)) {
      const contractsCode = fs.readFileSync(contractsPath, "utf8");
      assert.ok(
        !contractsCode.includes("AdminTrainerPayoutResponse"),
        "AdminContracts must not have AdminTrainerPayoutResponse"
      );
      assert.ok(
        !contractsCode.includes("AdminProcessTrainerPayoutRequest"),
        "AdminContracts must not have AdminProcessTrainerPayoutRequest"
      );
    }
  });
});
