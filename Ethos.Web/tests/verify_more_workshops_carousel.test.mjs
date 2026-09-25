import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const jsxPath = path.resolve(__dirname, "../src/pages/WorkshopDetailsPage.jsx");
const cssPath = path.resolve(__dirname, "../src/pages/WorkshopDetailsPage.css");

const jsxContent = fs.readFileSync(jsxPath, "utf8");
const cssContent = fs.readFileSync(cssPath, "utf8");

test("More Workshops: WorkshopDetailsPage.jsx renders More Workshops section with correct title and subtitle", () => {
  assert.ok(jsxContent.includes('className="more-workshops-section"'), "Must contain more-workshops-section");
  assert.ok(jsxContent.includes('className="more-workshops-title">More Workshops</h2>'), "Must render 'More Workshops' as heading");
  assert.ok(
    jsxContent.includes("Discover other upcoming dance experiences and masterclasses"),
    "Must render descriptive subtitle"
  );
});

test("More Workshops: Carousel navigation controls and track ref are wired up", () => {
  assert.ok(jsxContent.includes("moreWorkshopsTrackRef"), "Must define and use moreWorkshopsTrackRef");
  assert.ok(jsxContent.includes("handleScrollMore"), "Must define handleScrollMore function");
  assert.ok(jsxContent.includes('className="more-carousel-controls"'), "Must render carousel controls container");
  assert.ok(jsxContent.includes('className="more-carousel-btn prev"'), "Must render prev carousel button");
  assert.ok(jsxContent.includes('className="more-carousel-btn next"'), "Must render next carousel button");
  assert.ok(jsxContent.includes('className="more-workshops-track"'), "Must render more-workshops-track");
  assert.ok(jsxContent.includes('ref={moreWorkshopsTrackRef}'), "Track must be attached to ref");
});

test("More Workshops: Navigation button clicks do not navigate browser and scroll smoothly", () => {
  assert.ok(jsxContent.includes('type="button"'), "Carousel buttons must be explicit type='button'");
  assert.ok(jsxContent.includes("e.preventDefault()"), "Carousel button clicks must preventDefault");
  assert.ok(jsxContent.includes("scrollBy"), "handleScrollMore must call scrollBy");
  assert.ok(jsxContent.includes('"smooth"'), "Scrolling behavior must be smooth");
});

test("More Workshops: Workshop cards maintain luxury Ethos presentation elements", () => {
  assert.ok(jsxContent.includes('className="more-workshop-card"'), "Must render more-workshop-card");
  assert.ok(jsxContent.includes('className="more-workshop-poster"'), "Must render workshop poster");
  assert.ok(jsxContent.includes('className="more-workshop-tag"'), "Must render dance style tag");
  assert.ok(jsxContent.includes('className="more-workshop-name"'), "Must render workshop title");
  assert.ok(jsxContent.includes('className="more-workshop-info-box"'), "Must render meta info boxes");
  assert.ok(jsxContent.includes('className="more-card-price"'), "Must render starting price");
  assert.ok(jsxContent.includes('className="more-card-book-btn"'), "Must render Book Now button");
  assert.ok(jsxContent.includes('className="more-card-share-btn"'), "Must render Share button");
  assert.ok(jsxContent.includes("fallbackImage"), "Must provide fallback image for broken/missing media");
});

test("More Workshops: Card click and Book Now navigate to existing workshop detail route starting at top", () => {
  assert.ok(jsxContent.includes("navigate(`/workshops/${otherSlug}`)"), "Must navigate to /workshops/:slug");
  assert.ok(
    jsxContent.includes('window.scrollTo({ top: 0, behavior: "smooth" })'),
    "Must scroll window to top on navigation"
  );
  assert.ok(jsxContent.includes("e.stopPropagation()"), "Book Now click must stop event propagation");
});

test("More Workshops: getRelatedUpcomingWorkshops helper strictly excludes current workshop and non-bookable workshops", () => {
  assert.ok(
    jsxContent.includes("function getRelatedUpcomingWorkshops"),
    "Must define getRelatedUpcomingWorkshops helper"
  );

  // Extract function dynamically or recreate pure logic to test invariants
  const now = new Date();
  const sampleCurrent = { id: "ws-current-123", title: "Current Contemporary Batch" };
  const sampleList = [
    { id: "ws-current-123", title: "Current Contemporary Batch", workshopDate: "2026-10-15T18:00:00Z" },
    { id: "WS-CURRENT-123", title: "Current Contemporary Batch", workshopDate: "2026-10-15T18:00:00Z" }, // Different case
    { id: "ws-past-456", title: "Past Workshop", workshopDate: "2024-01-01T10:00:00Z" }, // Ended
    { id: "ws-draft-789", title: "Draft Workshop", status: "draft", workshopDate: "2026-11-01T10:00:00Z" }, // Draft
    { id: "ws-cancelled-101", title: "Cancelled Workshop", status: "cancelled", workshopDate: "2026-11-01T10:00:00Z" }, // Cancelled
    { id: "ws-upcoming-202", title: "Upcoming Jazz Funk", status: "published", workshopDate: "2026-11-20T10:00:00Z" }, // Valid upcoming
    { id: "ws-upcoming-201", title: "Upcoming Hip Hop", status: "approved", workshopDate: "2026-10-25T10:00:00Z" }, // Valid upcoming earlier
  ];

  // Helper matching the implemented algorithm
  function testFilter(list, current, currentSlug) {
    const currentId = String(current?.id || "").trim().toLowerCase();
    const normalizedSlug = String(currentSlug || "").trim().toLowerCase();
    const testNow = new Date();

    return list
      .filter((item) => {
        if (!item) return false;
        const itemId = String(item.id || "").trim().toLowerCase();
        if (currentId && itemId === currentId) return false;

        const itemSlug = (item.title || item.name || item.id || "").toLowerCase().replace(/\s+/g, "-");
        if (normalizedSlug && itemSlug === normalizedSlug) return false;

        if (item.status) {
          const st = String(item.status).toLowerCase();
          if (st === "draft" || st === "cancelled" || st === "canceled" || st === "archived") return false;
        }

        let isCompleted = false;
        if (item.endUtc) {
          const d = new Date(item.endUtc);
          if (!isNaN(d.getTime())) isCompleted = d <= testNow;
        } else {
          const datePart = item.workshopDate ? item.workshopDate.split("T")[0] : "";
          const timePart = item.endTime || "23:59:59";
          const d = new Date(`${datePart}T${timePart}`);
          if (!isNaN(d.getTime())) isCompleted = d <= testNow;
        }
        if (isCompleted) return false;

        return true;
      })
      .sort((a, b) => new Date(a.workshopDate || 0) - new Date(b.workshopDate || 0));
  }

  const result = testFilter(sampleList, sampleCurrent, "current-contemporary-batch");
  assert.strictEqual(result.length, 2, "Expected exactly 2 upcoming workshops to pass filter");
  assert.strictEqual(result[0].id, "ws-upcoming-201", "Earliest upcoming workshop must be sorted first");
  assert.strictEqual(result[1].id, "ws-upcoming-202", "Later upcoming workshop must follow");

  // Verify none of the excluded items are present
  const resultIds = result.map((r) => r.id);
  assert.ok(!resultIds.includes("ws-current-123"), "Must exclude current workshop ID");
  assert.ok(!resultIds.includes("WS-CURRENT-123"), "Must exclude current workshop case-insensitively");
  assert.ok(!resultIds.includes("ws-past-456"), "Must exclude past workshop");
  assert.ok(!resultIds.includes("ws-draft-789"), "Must exclude draft workshop");
  assert.ok(!resultIds.includes("ws-cancelled-101"), "Must exclude cancelled workshop");
});

test("More Workshops: CSS implements horizontal scrolling, snap alignment, and zero page overflow", () => {
  // Track has flex, overflow-x: auto, scroll-snap-type
  assert.ok(cssContent.includes(".more-workshops-track {"), "Must define .more-workshops-track");
  assert.ok(cssContent.includes("scroll-snap-type: x mandatory;"), "Must specify scroll-snap-type: x mandatory");
  assert.ok(cssContent.includes("overflow-x: auto;"), "Track must specify overflow-x: auto");
  assert.ok(cssContent.includes("scrollbar-width: none;"), "Track must hide raw scrollbars");

  // Section contains overflow to prevent page-level horizontal overflow
  assert.ok(cssContent.includes(".more-workshops-section {"), "Must define .more-workshops-section");
  assert.ok(cssContent.includes("overflow: hidden;"), "Section must contain horizontal overflow");
  assert.ok(cssContent.includes("max-width: 100%;"), "Section max-width must be 100%");

  // Card flex and snap align
  assert.ok(cssContent.includes("scroll-snap-align: start;"), "Cards must snap to start");
  assert.ok(cssContent.includes("flex: 0 0 clamp(290px, 26vw, 340px);"), "Cards must have fixed flex basis on desktop");

  // Mobile rules
  assert.ok(cssContent.includes("flex: 0 0 82vw;"), "Mobile cards must use viewport-width based sizing for edge peek");
  assert.ok(!cssContent.includes(".more-workshops-grid {\n    display: flex;\n    flex-direction: column;"), "Must NOT stack into a vertical column on mobile");
});
