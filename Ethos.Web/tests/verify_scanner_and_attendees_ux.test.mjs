import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webSrcDir = path.resolve(__dirname, "../src");

test("Image 1 & Image 2 UX Verification: Scanner Detection, Live Verification Card, & Bookings Ledger Light Theme", async (t) => {
  await t.test("1. Bookings & Attendees Page: Light Theme Styling Integrity (Image 1)", () => {
    const attendeesCode = fs.readFileSync(
      path.join(webSrcDir, "pages/admin/workshops/AdminWorkshopBookingsAttendees.jsx"),
      "utf8"
    );

    // Verify dark obsidian backgrounds are completely removed from search input & filter dropdowns
    assert.ok(
      !attendeesCode.includes('background: "rgba(15, 23, 42, 0.6)"'),
      "Should not contain dark background rgba(15, 23, 42, 0.6) for inputs"
    );
    assert.ok(
      !attendeesCode.includes('background: "rgba(15, 23, 42, 0.8)"'),
      "Should not contain dark background rgba(15, 23, 42, 0.8) for ticket roster sub-table"
    );

    // Verify clean light background and border styles
    assert.ok(
      attendeesCode.includes('background: "#ffffff"'),
      "Should use light background #ffffff on search and filter inputs"
    );
    assert.ok(
      attendeesCode.includes('border: "1px solid #cbd5e1"'),
      "Should use standard light border #cbd5e1 on search and filter inputs"
    );
    assert.ok(
      attendeesCode.includes('color: "#0f172a"'),
      "Should use dark text color #0f172a on light inputs"
    );

    // Verify sub-table light styling
    assert.ok(
      attendeesCode.includes('background: "#f8fafc"'),
      "Ticket roster sub-table should use light background #f8fafc"
    );
  });

  await t.test("2. Scanner Camera Detection Sensitivity & Hardware Barcode API (Image 2)", () => {
    const scannerCode = fs.readFileSync(
      path.join(webSrcDir, "pages/admin/workshops/AdminWorkshopScanner.jsx"),
      "utf8"
    );

    // Verify high FPS for fast scanning off screens
    assert.ok(
      scannerCode.includes("fps: 20"),
      "Should use 20 fps for snappy recognition"
    );

    // Verify hardware BarcodeDetector feature is enabled
    assert.ok(
      scannerCode.includes("useBarCodeDetectorIfSupported: true"),
      "Should enable useBarCodeDetectorIfSupported for native browser decoding"
    );

    // Verify dynamic responsive qrbox
    assert.ok(
      scannerCode.includes("qrbox: (viewfinderWidth, viewfinderHeight) =>"),
      "Should dynamically size qrbox based on container dimensions"
    );

    // Verify audio feedback is integrated
    assert.ok(
      scannerCode.includes("playScanChime"),
      "Should include synthesizer audio chime for instant feedback"
    );
  });

  await t.test("3. Live Verification Card & Wrong Workshop Detection (Image 2)", () => {
    const scannerCode = fs.readFileSync(
      path.join(webSrcDir, "pages/admin/workshops/AdminWorkshopScanner.jsx"),
      "utf8"
    );

    // Verify lastScanResult state exists
    assert.ok(
      scannerCode.includes("const [lastScanResult, setLastScanResult] = useState(null)"),
      "Should track lastScanResult state"
    );

    // Verify live alert directly under camera viewport
    assert.ok(
      scannerCode.includes("scanner-live-alert"),
      "Should render real-time alert directly under the camera viewport"
    );

    // Verify cross-workshop ticket warning
    assert.ok(
      scannerCode.includes("wrong_workshop"),
      "Should handle wrong_workshop status specifically"
    );

    // Verify one-click switch button
    assert.ok(
      scannerCode.includes("Switch to"),
      "Should provide quick switch button when a ticket from another workshop is scanned"
    );

    // Verify sidebar live verification monitor
    assert.ok(
      scannerCode.includes("live-scan-card"),
      "Should render dedicated Live Verification Monitor in the right column"
    );
  });

  await t.test("4. Scanner CSS Styles Parity", () => {
    const scannerCss = fs.readFileSync(
      path.join(webSrcDir, "pages/admin/workshops/AdminWorkshopScanner.css"),
      "utf8"
    );

    assert.ok(scannerCss.includes(".scanner-live-alert"), "CSS should define .scanner-live-alert");
    assert.ok(scannerCss.includes(".alert-wrong_workshop"), "CSS should define .alert-wrong_workshop");
    assert.ok(scannerCss.includes(".live-scan-card"), "CSS should define .live-scan-card");
    assert.ok(scannerCss.includes(".scan-switch-workshop-btn"), "CSS should define .scan-switch-workshop-btn");
  });

  await t.test("5. Workshop Pagination Bar Matching Image 1 (Max 9 Cards Default)", () => {
    const workshopsJsx = fs.readFileSync(
      path.join(webSrcDir, "pages/admin/AdminWorkshops.jsx"),
      "utf8"
    );
    const workshopsCss = fs.readFileSync(
      path.join(webSrcDir, "pages/admin/AdminWorkshops.css"),
      "utf8"
    );

    // Verify default pageSize = 9
    assert.ok(
      workshopsJsx.includes("const [pageSize, setPageSize] = useState(9)"),
      "Default page size must be 9 cards per page"
    );

    // Verify pagination controls and info text
    assert.ok(
      workshopsJsx.includes("workshops-pagination-bar"),
      "Should render workshops-pagination-bar container"
    );
    assert.ok(
      workshopsJsx.includes("Page {currentPage} of {totalPages} - {totalItems} total"),
      "Should render exact pagination text: Page X of Y - Z total"
    );
    assert.ok(
      workshopsJsx.includes("paginatedWorkshops.map"),
      "Should map over paginatedWorkshops instead of rendering all cards at once"
    );

    // Verify CSS styles
    assert.ok(
      workshopsCss.includes(".workshops-pagination-bar"),
      "CSS should define .workshops-pagination-bar"
    );
    assert.ok(
      workshopsCss.includes(".pagination-pagesize-select"),
      "CSS should define .pagination-pagesize-select"
    );
    assert.ok(
      workshopsCss.includes(".pagination-nav-btn"),
      "CSS should define .pagination-nav-btn"
    );
  });

  await t.test("6. Workshop Card Floating Inset Corners Matching Image 2", () => {
    const workshopsCss = fs.readFileSync(
      path.join(webSrcDir, "pages/admin/AdminWorkshops.css"),
      "utf8"
    );

    // Verify outer card has inset padding (so corners are not fully occupied by image)
    assert.ok(
      workshopsCss.includes("padding: 12px !important"),
      "Card must have 12px inset padding around inner elements"
    );

    // Verify outer card has smooth rounded corners (18px)
    assert.ok(
      workshopsCss.includes("border-radius: 18px !important"),
      "Outer card must have 18px rounded corners"
    );

    // Verify media wrap is an inner floating element with its own rounded corners
    assert.ok(
      workshopsCss.includes("border-radius: 12px !important"),
      "Inner image/media wrap must have 12px rounded corners"
    );

    // Verify 3-column responsive grid on desktop
    assert.ok(
      workshopsCss.includes("minmax(360px, 1fr)"),
      "Cards grid must use clean multi-column layout"
    );
  });
});

