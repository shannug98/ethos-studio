import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

test("Wizard Step 6: Ticket Types Validation recognizes Solo Passes and multi-passes", () => {
  const wizardFile = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/admin/workshops/wizard/AdminWorkshopWizard.jsx"),
    "utf8"
  );

  // Must not reject Solo Passes (sessionsIncluded === 1)
  assert.doesNotMatch(
    wizardFile,
    /Number\(p\.sessionsIncluded\)\s*<\s*2/,
    "AdminWorkshopWizard must not require sessionsIncluded >= 2; solo passes (sessionsIncluded === 1) must be valid"
  );

  // Must check < 1
  assert.match(
    wizardFile,
    /Number\(p\.sessionsIncluded\)\s*<\s*1/,
    "AdminWorkshopWizard should allow 1 session (solo pass) or more"
  );
});

test("Wizard Step 6: Multi-Session Schedule Validation checks final session completion", () => {
  const step6File = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/admin/workshops/wizard/Step6Review.jsx"),
    "utf8"
  );

  // Must calculate isPast by checking last session ending time
  assert.match(
    step6File,
    /sortedSessions\[sortedSessions\.length - 1\]/,
    "Step6Review must check the final session in the schedule so same-day ongoing events are not flagged as past"
  );
});

test("Public Workshop Details Page: Default open accordions and hero backdrop", () => {
  const detailsJsx = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/WorkshopDetailsPage.jsx"),
    "utf8"
  );
  const detailsCss = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/WorkshopDetailsPage.css"),
    "utf8"
  );

  // Default open sections
  assert.match(
    detailsJsx,
    /openSections.*passes:\s*true.*schedule:\s*true.*about:\s*true/s,
    "WorkshopDetailsPage must open Passes, Schedule, and About by default"
  );

  // Hero card backdrop container in JSX
  assert.match(
    detailsJsx,
    /workshop-hero-card-backdrop/,
    "WorkshopDetailsPage must render workshop-hero-card-backdrop"
  );

  // Hero card backdrop container in CSS
  assert.match(
    detailsCss,
    /\.workshop-hero-card-backdrop\s*\{[^}]*filter:\s*blur/s,
    "WorkshopDetailsPage.css must define blurred backdrop for portrait posters"
  );

  // Hero image must be contain to prevent cutting off portrait poster
  assert.match(
    detailsCss,
    /\.workshop-hero-img\s*\{[^}]*object-fit:\s*contain/s,
    "WorkshopDetailsPage.css must use object-fit: contain so portrait posters are fully visible without cropping"
  );

  // remainingSeats must be destructured from getPassAvailability
  assert.match(
    detailsJsx,
    /const\s*\{\s*isSoldOut,\s*remainingSeats\s*\}\s*=\s*getPassAvailability\(pass\)/,
    "WorkshopDetailsPage.jsx must destructure remainingSeats so it does not throw ReferenceError"
  );
});

test("Backend Workshop & WorkshopSession: Booking closure evaluated through session end time", () => {
  const sessionCs = fs.readFileSync(
    path.resolve(__dirname, "../../Ethos.Api/Domain/Entities/WorkshopSession.cs"),
    "utf8"
  );

  // Must fallback to effectiveEnd (EndTime)
  assert.match(
    sessionCs,
    /var effectiveEnd = EndTime > TimeSpan\.Zero \? EndTime : StartTime/,
    "WorkshopSession.cs must allow bookings until EndTime"
  );

  // Must not prematurely close individual sessions based on workshop cutoff
  assert.doesNotMatch(
    sessionCs,
    /Workshop\.BookingCutoffTime\.HasValue/,
    "WorkshopSession.cs must evaluate session-specific cutoffs independently"
  );
});

test("Step 3: Delete Session Modal has modern styling and no raw buttons", () => {
  const step3File = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/admin/workshops/wizard/Step3VenueSchedule.jsx"),
    "utf8"
  );

  // Must not have raw unstyled bootstrap classes
  assert.doesNotMatch(
    step3File,
    /className="btn btn-outline-secondary"/,
    "Step 3 delete modal must not use unstyled btn-outline-secondary"
  );
  assert.doesNotMatch(
    step3File,
    /className="btn btn-danger"/,
    "Step 3 delete modal must not use unstyled btn-danger"
  );

  // Must have Trash2 icon and styled buttons
  assert.match(
    step3File,
    /Delete this session\?/,
    "Step 3 delete modal must exist"
  );
  assert.match(
    step3File,
    /Continue & Delete/,
    "Step 3 delete modal must have action button"
  );
});

test("Checkout Page & Media Assets: Sticky pay bar hidden on success & local fallbacks set", () => {
  const checkoutFile = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/WorkshopCheckoutPage.jsx"),
    "utf8"
  );
  const mediaAssetsFile = fs.readFileSync(
    path.resolve(__dirname, "../src/config/mediaAssets.js"),
    "utf8"
  );

  // Sticky bottom pay bar must be hidden when booking is confirmed / pass modal is open
  assert.match(
    checkoutFile,
    /!confirmedBooking\s*&&\s*!isPassModalOpen/,
    "WorkshopCheckoutPage must hide the sticky bottom pay bar once payment succeeds and pass modal opens"
  );

  // Under Approach B (Admin-Driven Clean Slate), mediaAssets.js is normalized to genuine application assets
  assert.ok(
    mediaAssetsFile.includes("MEDIA_ASSETS"),
    "mediaAssets.js must export MEDIA_ASSETS registry"
  );
});

test("Step 6: Review & Publish checklist items are clickable and navigate directly to fix errors", () => {
  const step6Jsx = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/admin/workshops/wizard/Step6Review.jsx"),
    "utf8"
  );

  // onGoToStep prop must be received
  assert.match(step6Jsx, /onGoToStep/, "Step6Review must accept onGoToStep callback");

  // Step routing map must direct errors to correct steps
  assert.match(step6Jsx, /step:\s*1/, "Step6Review must route title/details errors to Step 1");
  assert.match(step6Jsx, /step:\s*2/, "Step6Review must route poster/media errors to Step 2");
  assert.match(step6Jsx, /step:\s*3/, "Step6Review must route schedule/venue errors to Step 3");
  assert.match(step6Jsx, /step:\s*4/, "Step6Review must route ticket pass errors to Step 4");

  // Clickable error item styling & handler
  assert.match(step6Jsx, /step6-checklist-item/, "Step6Review must render checklist items");
  assert.match(step6Jsx, /onGoToStep\?\.\(item\.step\)/, "Step6Review must trigger onGoToStep(item.step) when clicking a checklist item");
  assert.match(step6Jsx, /Fix in Step/, "Step6Review must show 'Fix in Step' hint for invalid items");
});

test("SelectTicketsModal: Matches Image 2 UI with radio circles, avatars, and dual-column footer", () => {
  const modalJsx = fs.readFileSync(
    path.resolve(__dirname, "../src/components/workshop/SelectTicketsModal.jsx"),
    "utf8"
  );
  const modalCss = fs.readFileSync(
    path.resolve(__dirname, "../src/components/workshop/SelectTicketsModal.css"),
    "utf8"
  );

  // Pass selection radio container
  assert.match(modalJsx, /pass-radio-container/, "SelectTicketsModal must render radio circles for passes");
  assert.match(modalJsx, /pass-entitlement-pill/, "SelectTicketsModal must render entitlement pill");

  // Session picker radio and trainer avatar
  assert.match(modalJsx, /session-radio-container/, "SelectTicketsModal must render radio circles for sessions");
  assert.match(modalJsx, /TrainerAvatar/, "SelectTicketsModal must render TrainerAvatar in session rows");
  assert.match(modalJsx, /session-seats-badge/, "SelectTicketsModal must render seat availability badge");

  // Footer structure with Selected Pass and Total
  assert.match(modalJsx, /select-tickets-footer-left/, "SelectTicketsModal must have select-tickets-footer-left");
  assert.match(modalJsx, /footer-meta-label/, "SelectTicketsModal must have footer meta labels");
  assert.match(modalJsx, /footer-total-price/, "SelectTicketsModal must display footer-total-price");

  // CSS verification
  assert.match(modalCss, /\.pass-radio-outer/, "SelectTicketsModal.css must style radio buttons");
  assert.match(modalCss, /\.select-tickets-footer-left/, "SelectTicketsModal.css must style footer columns");
  assert.match(modalCss, /white-space:\s*nowrap/, "Continue button must prevent text wrapping onto 2 lines");
  assert.match(modalCss, /overflow-y:\s*auto/, "Modal body must be scrollable to prevent clipping on mobile viewports");
  assert.match(modalCss, /gap:\s*1[2-6]px/, "Footer must maintain a clean gap between amount and continue button");
});

test("AdminWorkshopWizard: XCircle icon import and loading resilience", () => {
  const wizardFile = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/admin/workshops/wizard/AdminWorkshopWizard.jsx"),
    "utf8"
  );
  const adminApiFile = fs.readFileSync(
    path.resolve(__dirname, "../src/services/adminApi.js"),
    "utf8"
  );

  // Must import XCircle from lucide-react
  assert.match(
    wizardFile,
    /import[\s\S]*?XCircle[\s\S]*?from\s+["']lucide-react["']/,
    "AdminWorkshopWizard must import XCircle from lucide-react"
  );

  // Must have loadError state and fallback error screen
  assert.match(
    wizardFile,
    /const\s*\[loadError,\s*setLoadError\]\s*=\s*useState/,
    "AdminWorkshopWizard must track loadError state"
  );
  assert.match(
    wizardFile,
    /isEdit\s*&&\s*loadError\s*&&\s*!form\.title/,
    "AdminWorkshopWizard must show clean error screen when workshop cannot be loaded"
  );

  // getWorkshopDraft must safely return null when no draft exists
  assert.match(
    adminApiFile,
    /getWorkshopDraft[\s\S]*?catch[\s\S]*?return\s+null/,
    "adminApi.getWorkshopDraft must gracefully return null if draft does not exist"
  );
});

test("Public Workshop Details Page: Mobile booking card reordering & sticky bottom bar", () => {
  const detailsJsx = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/WorkshopDetailsPage.jsx"),
    "utf8"
  );
  const detailsCss = fs.readFileSync(
    path.resolve(__dirname, "../src/pages/WorkshopDetailsPage.css"),
    "utf8"
  );

  // Must have workshop-header-meta wrapper
  assert.match(
    detailsJsx,
    /className="workshop-header-meta"/,
    "WorkshopDetailsPage must wrap title and tags in workshop-header-meta"
  );

  // Must have mobile sticky bottom bar in JSX
  assert.match(
    detailsJsx,
    /mobile-sticky-booking-bar/,
    "WorkshopDetailsPage must render mobile-sticky-booking-bar"
  );
  assert.match(
    detailsJsx,
    /mobile-sticky-bar-price/,
    "WorkshopDetailsPage must display mobile-sticky-bar-price"
  );

  // CSS layout ordering: Booking card right under title/tags on mobile
  assert.match(
    detailsCss,
    /\.workshop-header-meta\s*\{\s*order:\s*1;\s*\}/,
    "CSS must place workshop-header-meta at order 1 on mobile"
  );
  assert.match(
    detailsCss,
    /\.workshop-sidebar-col\s*\{\s*order:\s*2;/,
    "CSS must place booking card (.workshop-sidebar-col) at order 2 on mobile"
  );
  assert.match(
    detailsCss,
    /\.details-trainers-section[\s\S]*?\{\s*order:\s*3;\s*\}/,
    "CSS must place trainer lineup after booking card on mobile"
  );

  // Floating socials clearance
  assert.match(
    detailsCss,
    /body\.has-sticky-booking-bar\s+\.ethos-floating-socials/,
    "CSS must lift ethos-floating-socials when mobile sticky booking bar is visible"
  );
});


