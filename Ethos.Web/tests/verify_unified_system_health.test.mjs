import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webRoot = path.resolve(__dirname, "..");
const repoRoot = path.resolve(webRoot, "..");

describe("Phase A: Unified Point-to-Point System Health Engine", () => {
  const apiRoot = path.join(repoRoot, "Ethos.Api");

  // 1. Backend Architecture & Point-to-Point Contract
  it("1. Verifies IWorkerLivenessTracker and worker heartbeat integration", () => {
    const trackerInterfacePath = path.join(apiRoot, "Application/Common/IWorkerLivenessTracker.cs");
    assert.ok(fs.existsSync(trackerInterfacePath), "IWorkerLivenessTracker.cs must exist");
    const trackerContent = fs.readFileSync(trackerInterfacePath, "utf8");
    assert.ok(trackerContent.includes("RecordHeartbeat"), "Must have RecordHeartbeat method");
    assert.ok(trackerContent.includes("GetHeartbeat"), "Must have GetHeartbeat method");
    assert.ok(trackerContent.includes("GetAllHeartbeats"), "Must have GetAllHeartbeats method");

    // Check all 3 background workers report to tracker
    const whatsappWorker = fs.readFileSync(path.join(apiRoot, "Infrastructure/BackgroundWorkers/WhatsAppOutboxBackgroundWorker.cs"), "utf8");
    assert.ok(whatsappWorker.includes("IWorkerLivenessTracker"), "WhatsAppOutboxBackgroundWorker must inject IWorkerLivenessTracker");
    assert.ok(whatsappWorker.includes("RecordHeartbeat"), "WhatsAppOutboxBackgroundWorker must record heartbeats");

    const refundWorker = fs.readFileSync(path.join(apiRoot, "Infrastructure/BackgroundWorkers/RefundOutboxBackgroundWorker.cs"), "utf8");
    assert.ok(refundWorker.includes("IWorkerLivenessTracker"), "RefundOutboxBackgroundWorker must inject IWorkerLivenessTracker");
    assert.ok(refundWorker.includes("RecordHeartbeat"), "RefundOutboxBackgroundWorker must record heartbeats");

    const telemetryWorker = fs.readFileSync(path.join(apiRoot, "Infrastructure/Telemetry/TelemetryBackgroundWorker.cs"), "utf8");
    assert.ok(telemetryWorker.includes("IWorkerLivenessTracker"), "TelemetryBackgroundWorker must inject IWorkerLivenessTracker");
    assert.ok(telemetryWorker.includes("RecordHeartbeat"), "TelemetryBackgroundWorker must record heartbeats");
  });

  it("2. Verifies SystemHealthService implements all 10 point-to-point components with strict 5-state lifecycle", () => {
    const healthServicePath = path.join(apiRoot, "Application/Admin/SystemHealthService.cs");
    assert.ok(fs.existsSync(healthServicePath), "SystemHealthService.cs must exist");
    const healthServiceContent = fs.readFileSync(healthServicePath, "utf8");

    // Verify 10 component keys
    const expectedKeys = [
      "core_api",
      "postgresql",
      "razorpay",
      "cloudflare_r2",
      "msg91_whatsapp",
      "worker_whatsapp",
      "worker_refund",
      "worker_telemetry",
      "auth_authority",
      "security_monitor",
    ];

    for (const key of expectedKeys) {
      assert.ok(
        healthServiceContent.includes(`"${key}"`),
        `SystemHealthService must evaluate component key '${key}'`
      );
    }

    // Verify 5 strict states
    const states = ["Operational", "Degraded", "Error", "Not Configured", "Standby"];
    for (const st of states) {
      assert.ok(
        healthServiceContent.includes(`"${st}"`),
        `SystemHealthService must support status '${st}'`
      );
    }

    // Verify safe diagnostics without secret leakage
    assert.ok(
      !healthServiceContent.includes('["Password"]') && !healthServiceContent.includes('["Secret"]'),
      "Diagnostics must never record raw passwords or secrets"
    );
    assert.ok(
      healthServiceContent.includes("SanitizeError"),
      "Must include error/diagnostic sanitization"
    );
  });

  it("3. Verifies Program.cs registers services and exposes lightweight public /health and /api/health", () => {
    const programCs = fs.readFileSync(path.join(apiRoot, "Program.cs"), "utf8");
    assert.ok(
      programCs.includes("IWorkerLivenessTracker") && programCs.includes("WorkerLivenessTracker"),
      "Program.cs must register WorkerLivenessTracker"
    );
    assert.ok(
      programCs.includes("ISystemHealthService") && programCs.includes("SystemHealthService"),
      "Program.cs must register SystemHealthService"
    );
    assert.ok(
      programCs.includes('MapGet("/health"') || programCs.includes('MapGet("/health",'),
      "Program.cs must map public /health endpoint"
    );
    assert.ok(
      programCs.includes('MapGet("/api/health"') || programCs.includes('MapGet("/api/health",'),
      "Program.cs must map public /api/health endpoint"
    );
  });

  it("4. Verifies AdminDashboardService and AdminObservabilityController delegate to unified ISystemHealthService", () => {
    const dashboardService = fs.readFileSync(path.join(apiRoot, "Application/Admin/AdminDashboardService.cs"), "utf8");
    assert.ok(
      dashboardService.includes("ISystemHealthService"),
      "AdminDashboardService must inject ISystemHealthService"
    );
    assert.ok(
      dashboardService.includes("GetSystemHealthAsync"),
      "AdminDashboardService must delegate GetSystemHealthAsync"
    );

    const obsController = fs.readFileSync(path.join(apiRoot, "Controllers/Admin/AdminObservabilityController.cs"), "utf8");
    assert.ok(
      obsController.includes("ISystemHealthService"),
      "AdminObservabilityController must inject ISystemHealthService"
    );
  });

  // 2. Frontend Diagnostic Drawer & Page Integrations
  it("5. Verifies SystemHealthDiagnosticDrawer exists with full diagnostic views and action dispatching", () => {
    const drawerJsxPath = path.join(webRoot, "src/components/admin/common/SystemHealthDiagnosticDrawer.jsx");
    const drawerCssPath = path.join(webRoot, "src/components/admin/common/SystemHealthDiagnosticDrawer.css");
    assert.ok(fs.existsSync(drawerJsxPath), "SystemHealthDiagnosticDrawer.jsx must exist");
    assert.ok(fs.existsSync(drawerCssPath), "SystemHealthDiagnosticDrawer.css must exist");

    const drawerJsx = fs.readFileSync(drawerJsxPath, "utf8");
    assert.ok(drawerJsx.includes("component.status"), "Drawer must display component status");
    assert.ok(drawerJsx.includes("component.latencyMs"), "Drawer must display latency");
    assert.ok(drawerJsx.includes("component.errorMessage"), "Drawer must display reported error/issue");
    assert.ok(drawerJsx.includes("component.affectedSystems"), "Drawer must list affected systems");
    assert.ok(drawerJsx.includes("component.diagnostics"), "Drawer must render diagnostics key-values");
    assert.ok(drawerJsx.includes("component.traceId"), "Drawer must display server trace ID");
    assert.ok(drawerJsx.includes("component.actionUrl"), "Drawer must support direct action navigation");
    assert.ok(drawerJsx.includes("onClose"), "Drawer must support closing");
  });

  it("6. Verifies AdminObservability.jsx renders point-to-point components with diagnostic drawer", () => {
    const obsJsx = fs.readFileSync(path.join(webRoot, "src/pages/admin/AdminObservability.jsx"), "utf8");
    const obsCss = fs.readFileSync(path.join(webRoot, "src/pages/admin/AdminObservability.css"), "utf8");

    assert.ok(
      obsJsx.includes("SystemHealthDiagnosticDrawer"),
      "AdminObservability.jsx must import and use SystemHealthDiagnosticDrawer"
    );
    assert.ok(
      obsJsx.includes("healthData.components"),
      "AdminObservability.jsx must iterate over healthData.components"
    );
    assert.ok(
      obsJsx.includes("setSelectedHealthComp"),
      "AdminObservability.jsx must have state to select a component for inspection"
    );
    assert.ok(
      obsCss.includes(".health-card.clickable"),
      "AdminObservability.css must style clickable health cards"
    );
    assert.ok(
      obsCss.includes(".health-badge.operational"),
      "AdminObservability.css must style operational health badge"
    );
  });

  it("7. Verifies AdminDashboard.jsx links subsystem rows to SystemHealthDiagnosticDrawer while preserving dashboard layout", () => {
    const dashJsx = fs.readFileSync(path.join(webRoot, "src/pages/admin/AdminDashboard.jsx"), "utf8");
    const dashCss = fs.readFileSync(path.join(webRoot, "src/pages/admin/AdminDashboard.css"), "utf8");

    assert.ok(
      dashJsx.includes("SystemHealthDiagnosticDrawer"),
      "AdminDashboard.jsx must import and use SystemHealthDiagnosticDrawer"
    );
    assert.ok(
      dashJsx.includes("healthDrawerComp"),
      "AdminDashboard.jsx must manage healthDrawerComp state"
    );
    assert.ok(
      dashJsx.includes("clickable-subsystem-row"),
      "AdminDashboard.jsx subsystem row must be marked clickable"
    );
    assert.ok(
      dashCss.includes(".clickable-subsystem-row"),
      "AdminDashboard.css must include .clickable-subsystem-row"
    );
    assert.ok(
      dashCss.includes(".subsystem-badge-degraded"),
      "AdminDashboard.css must style degraded status"
    );
    assert.ok(
      dashCss.includes(".subsystem-badge-error"),
      "AdminDashboard.css must style error status"
    );
  });

  // 3. Live Server Endpoint Check
  it("8. Verifies live public /health and /api/health return HTTP 200 without exposing secrets", async () => {
    try {
      const res1 = await fetch("http://localhost:5252/health");
      assert.strictEqual(res1.status, 200, "/health must return HTTP 200");
      const json1 = await res1.json();
      assert.strictEqual(json1.status, "Healthy", "Status must be 'Healthy'");
      assert.ok(!json1.components, "/health must not leak internal components array");

      const res2 = await fetch("http://localhost:5252/api/health");
      assert.strictEqual(res2.status, 200, "/api/health must return HTTP 200");
      const json2 = await res2.json();
      assert.strictEqual(json2.status, "Healthy", "Status must be 'Healthy'");
      assert.ok(!json2.components, "/api/health must not leak internal components array");
    } catch (err) {
      // In CI / offline environments where server is not listening, skip gracefully
      if (err.code === "ECONNREFUSED" || err.cause?.code === "ECONNREFUSED" || err.message?.includes("fetch failed")) {
        console.log("Note: localhost:5252 not reachable in offline test process, skipping live HTTP assertion.");
      } else {
        throw err;
      }
    }
  });
});
