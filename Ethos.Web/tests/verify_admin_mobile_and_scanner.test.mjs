import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webRoot = path.resolve(__dirname, "..");

test("PHASE 1 — QR Scanner: Safe Camera Constraints & Lifecycle Guard Verification", () => {
  const scannerJsxPath = path.join(webRoot, "src", "pages", "admin", "workshops", "AdminWorkshopScanner.jsx");
  const scannerContent = fs.readFileSync(scannerJsxPath, "utf-8");

  // 1. Verifies facingMode: "environment" is default
  assert.ok(
    scannerContent.includes('facingMode: "environment"') || scannerContent.includes("{ facingMode: \"environment\" }"),
    "Scanner must use facingMode: 'environment' when no specific camera is chosen"
  );

  // 2. Verifies rigid 1280x720 videoConstraints were removed
  assert.ok(
    !scannerContent.includes("width: { ideal: 1280 }"),
    "Scanner must not force rigid 1280x720 videoConstraints which fail on portrait mobile cameras"
  );

  // 3. Verifies dynamic qrbox calculation based on viewfinder dimensions
  assert.ok(
    scannerContent.includes("qrbox: (viewfinderWidth, viewfinderHeight)"),
    "Scanner must use dynamic function-based qrbox calculation"
  );

  // 4. Verifies lifecycle guards (isTransitioningRef / isMountedRef) to prevent race conditions
  assert.ok(
    scannerContent.includes("isTransitioningRef") && scannerContent.includes("isMountedRef"),
    "Scanner must maintain isTransitioningRef and isMountedRef lifecycle guards"
  );

  // 5. Verifies playsinline / muted handling for iOS/mobile WebKit
  assert.ok(
    scannerContent.includes('setAttribute("playsinline", "true")') && scannerContent.includes("muted = true"),
    "Scanner must ensure playsinline and muted attributes on video element for mobile Safari compatibility"
  );
});

test("PHASE 1 — QR Scanner: DOM Isolation & Artifact Suppression Verification", () => {
  const scannerCssPath = path.join(webRoot, "src", "pages", "admin", "workshops", "AdminWorkshopScanner.css");
  const scannerCss = fs.readFileSync(scannerCssPath, "utf-8");

  // 1. Verifies internal html5-qrcode dashboards and buttons are suppressed
  assert.ok(
    scannerCss.includes("#qr-camera-viewport__dashboard"),
    "Scanner CSS must isolate and hide #qr-camera-viewport__dashboard"
  );
  assert.ok(
    scannerCss.includes("#qr-camera-viewport video") && scannerCss.includes("object-fit: cover !important"),
    "Scanner CSS must style video element with object-fit: cover"
  );

  // 2. Verifies mobile responsive breakpoints exist for scanner
  assert.ok(
    scannerCss.includes("@media (max-width: 768px)") && scannerCss.includes(".viewfinder-container"),
    "Scanner CSS must include responsive rules for mobile viewports"
  );
});

test("PHASE 2 — Admin Mobile Navigation & Layout Shell Verification", () => {
  const layoutCssPath = path.join(webRoot, "src", "components", "admin", "AdminLayout.css");
  const sidebarCssPath = path.join(webRoot, "src", "components", "admin", "AdminSidebar.css");
  const sidebarJsxPath = path.join(webRoot, "src", "components", "admin", "AdminSidebar.jsx");

  const layoutCss = fs.readFileSync(layoutCssPath, "utf-8");
  const sidebarCss = fs.readFileSync(sidebarCssPath, "utf-8");
  const sidebarJsx = fs.readFileSync(sidebarJsxPath, "utf-8");

  // 1. Verifies margin-left: 0 and full width on mobile
  assert.ok(
    layoutCss.includes("margin-left: 0 !important") && layoutCss.includes("max-width: 100vw !important"),
    "AdminLayout must remove desktop margin-left and prevent horizontal expansion at <= 768px"
  );

  // 2. Verifies off-canvas drawer transform & backdrop
  assert.ok(
    sidebarCss.includes("transform: translateX(-100%) !important"),
    "AdminSidebar must translate off-canvas when collapsed on mobile"
  );
  assert.ok(
    layoutCss.includes(".admin-sidebar-backdrop"),
    "AdminLayout must provide an active sidebar backdrop on mobile"
  );

  // 3. Verifies auto-close on link click for mobile viewports
  assert.ok(
    sidebarJsx.includes("window.innerWidth <= 768") && sidebarJsx.includes("onToggleCollapse"),
    "AdminSidebar must auto-close upon navigation link click on mobile"
  );
});

test("PHASE 3 & 4 — Tables, Grids, and Modal Dialog Responsive Containment", () => {
  const bookingsCssPath = path.join(webRoot, "src", "pages", "admin", "AdminBookings.css");
  const paymentsCssPath = path.join(webRoot, "src", "pages", "admin", "AdminPayments.css");
  const studentsCssPath = path.join(webRoot, "src", "pages", "admin", "AdminStudents.css");
  const auditCssPath = path.join(webRoot, "src", "pages", "admin", "AdminAudit.css");
  const cropperCssPath = path.join(webRoot, "src", "components", "admin", "common", "ImageCropperModal.css");

  const bookingsCss = fs.readFileSync(bookingsCssPath, "utf-8");
  const paymentsCss = fs.readFileSync(paymentsCssPath, "utf-8");
  const studentsCss = fs.readFileSync(studentsCssPath, "utf-8");
  const auditCss = fs.readFileSync(auditCssPath, "utf-8");
  const cropperCss = fs.readFileSync(cropperCssPath, "utf-8");

  // 1. Verifies horizontal overflow scrolling on table wrappers
  assert.ok(bookingsCss.includes("overflow-x: auto"), "Bookings table must have horizontal scroll containment");
  assert.ok(paymentsCss.includes("overflow-x: auto") || paymentsCss.includes("table-wrapper"), "Payments table must have horizontal scroll containment");
  assert.ok(studentsCss.includes("overflow-x: auto"), "Students table must have horizontal scroll containment");
  assert.ok(auditCss.includes("overflow-x: auto"), "Audit table must have horizontal scroll containment");

  // 2. Verifies modal dialog max-height & scroll containment
  assert.ok(
    cropperCss.includes("max-height: 92vh") || cropperCss.includes("max-height: 90vh"),
    "ImageCropperModal must constrain max-height on mobile viewports"
  );
});

test("WORKSHOP DRAFT — Optimistic Concurrency & In-Flight Save Mutex Verification", () => {
  const wizardJsxPath = path.join(webRoot, "src", "pages", "admin", "workshops", "wizard", "AdminWorkshopWizard.jsx");
  const wizardJsx = fs.readFileSync(wizardJsxPath, "utf-8");

  // 1. Verifies isSavingDraftRef mutex exists
  assert.ok(
    wizardJsx.includes("isSavingDraftRef"),
    "AdminWorkshopWizard must declare isSavingDraftRef mutex to prevent overlapping in-flight draft saves"
  );

  // 2. Verifies synchronous ref update immediately after saveWorkshopDraft resolves
  assert.ok(
    wizardJsx.includes("draftVersionRef.current = res.version;"),
    "AdminWorkshopWizard must synchronously update draftVersionRef immediately upon save resolution"
  );

  // 3. Verifies autosave effect is paused during active conflictModal or in-flight saves
  assert.ok(
    wizardJsx.includes("conflictModal || isSavingDraftRef.current"),
    "AdminWorkshopWizard autosave must be guarded by conflictModal and isSavingDraftRef"
  );
});

