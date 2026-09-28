import { test, describe } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const srcDir = path.resolve(__dirname, "../src");

describe("Stage 5.4: Public Client Instrumentation & Telemetry Dispatcher Verification", () => {
  test("1. Verifies analytics.js defines complete AnalyticsEventType allowlist and storage helpers", () => {
    const analyticsPath = path.join(srcDir, "services/analytics.js");
    const content = fs.readFileSync(analyticsPath, "utf-8");

    assert.ok(content.includes("PageView: 1"), "AnalyticsEventType must include PageView = 1");
    assert.ok(content.includes("WorkshopView: 2"), "AnalyticsEventType must include WorkshopView = 2");
    assert.ok(content.includes("WorkshopCheckoutStarted: 3"), "AnalyticsEventType must include WorkshopCheckoutStarted = 3");
    assert.ok(content.includes("WorkshopCheckoutCompleted: 4"), "AnalyticsEventType must include WorkshopCheckoutCompleted = 4");
    assert.ok(content.includes("FeedbackOpened: 5"), "AnalyticsEventType must include FeedbackOpened = 5");
    assert.ok(content.includes("FeedbackSubmitted: 6"), "AnalyticsEventType must include FeedbackSubmitted = 6");
    assert.ok(content.includes("LoginStarted: 7"), "AnalyticsEventType must include LoginStarted = 7");
    assert.ok(content.includes("LoginCompleted: 8"), "AnalyticsEventType must include LoginCompleted = 8");

    assert.ok(content.includes("ethos_visitor_id"), "Must manage ethos_visitor_id in storage");
    assert.ok(content.includes("ethos_session_id"), "Must manage ethos_session_id in storage");
    assert.ok(content.includes("sendBeacon"), "Must prioritize navigator.sendBeacon");
    assert.ok(content.includes("keepalive: true"), "Must fallback to fetch with keepalive");
  });

  test("2. Verifies App.jsx instruments AnalyticsRouteTracker for PageView", () => {
    const appPath = path.join(srcDir, "App.jsx");
    const content = fs.readFileSync(appPath, "utf-8");

    assert.ok(content.includes("trackPageView"), "App.jsx must import trackPageView");
    assert.ok(content.includes("AnalyticsRouteTracker"), "App.jsx must define AnalyticsRouteTracker component");
    assert.ok(content.includes("<AnalyticsRouteTracker />"), "App.jsx must render AnalyticsRouteTracker inside BrowserRouter");
  });

  test("3. Verifies WorkshopDetailsPage.jsx instruments WorkshopView", () => {
    const pagePath = path.join(srcDir, "pages/WorkshopDetailsPage.jsx");
    const content = fs.readFileSync(pagePath, "utf-8");

    assert.ok(content.includes("trackWorkshopView"), "WorkshopDetailsPage must import trackWorkshopView");
    assert.ok(content.includes("trackWorkshopView(matched.id"), "WorkshopDetailsPage must track workshop view with matched.id");
  });

  test("4. Verifies WorkshopCheckoutPage.jsx instruments WorkshopCheckoutStarted & WorkshopCheckoutCompleted", () => {
    const pagePath = path.join(srcDir, "pages/WorkshopCheckoutPage.jsx");
    const content = fs.readFileSync(pagePath, "utf-8");

    assert.ok(content.includes("trackWorkshopCheckoutStarted"), "Checkout page must import trackWorkshopCheckoutStarted");
    assert.ok(content.includes("trackWorkshopCheckoutCompleted"), "Checkout page must import trackWorkshopCheckoutCompleted");
    assert.ok(content.includes("trackWorkshopCheckoutStarted(ws.id"), "Checkout page must track started event");
    assert.ok(content.includes("trackWorkshopCheckoutCompleted(workshop.id"), "Checkout page must track completed event on verified payment");
  });

  test("5. Verifies GuestWorkshopFeedback.jsx instruments FeedbackOpened & FeedbackSubmitted", () => {
    const pagePath = path.join(srcDir, "pages/GuestWorkshopFeedback.jsx");
    const content = fs.readFileSync(pagePath, "utf-8");

    assert.ok(content.includes("trackFeedbackOpened"), "GuestWorkshopFeedback must import trackFeedbackOpened");
    assert.ok(content.includes("trackFeedbackSubmitted"), "GuestWorkshopFeedback must import trackFeedbackSubmitted");
    assert.ok(content.includes("trackFeedbackOpened(data.workshopId"), "Must track feedback opened on token load");
    assert.ok(content.includes("trackFeedbackSubmitted(details.workshopId"), "Must track feedback submitted on success");
  });

  test("6. Verifies AdminLogin.jsx and StudentLogin.jsx instrument LoginStarted & LoginCompleted", () => {
    const adminLoginPath = path.join(srcDir, "pages/admin/AdminLogin.jsx");
    const adminContent = fs.readFileSync(adminLoginPath, "utf-8");

    assert.ok(adminContent.includes("trackLoginStarted"), "AdminLogin must import trackLoginStarted");
    assert.ok(adminContent.includes("trackLoginCompleted"), "AdminLogin must import trackLoginCompleted");

    const studentLoginPath = path.join(srcDir, "pages/student/StudentLogin.jsx");
    const studentContent = fs.readFileSync(studentLoginPath, "utf-8");

    assert.ok(studentContent.includes("trackLoginStarted"), "StudentLogin must import trackLoginStarted");
    assert.ok(studentContent.includes("trackLoginCompleted"), "StudentLogin must import trackLoginCompleted");
  });
});
