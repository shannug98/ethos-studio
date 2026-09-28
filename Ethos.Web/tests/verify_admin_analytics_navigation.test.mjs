import { test, describe } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const srcDir = path.resolve(__dirname, "../src");

describe("Workstream 5.6: Admin Live Insights UI & Navigation Verification", () => {
  test("1. Verifies adminRouteRegistry.js contains analytics entry and breadcrumb", () => {
    const routeRegistryPath = path.join(srcDir, "constants/adminRouteRegistry.js");
    const content = fs.readFileSync(routeRegistryPath, "utf-8");

    assert.ok(content.includes('id: "analytics"'), "Registry must contain analytics module id");
    assert.ok(content.includes('path: "/admin_portal/analytics"'), "Registry must specify analytics path");
    assert.ok(content.includes('requiredPermission: "ANALYTICS_VIEW"'), "Registry must require ANALYTICS_VIEW permission");
    assert.ok(content.includes('"/admin_portal/analytics": ["Admin Portal", "Live Insights"]'), "Registry must map breadcrumb");
  });

  test("2. Verifies AdminSidebar.jsx contains Live Insights navigation link", () => {
    const sidebarPath = path.join(srcDir, "components/admin/AdminSidebar.jsx");
    const content = fs.readFileSync(sidebarPath, "utf-8");

    assert.ok(content.includes('to: "/admin_portal/analytics"'), "Sidebar must link to /admin_portal/analytics");
    assert.ok(content.includes('label: "Live Insights"'), "Sidebar must label as Live Insights");
  });

  test("3. Verifies App.jsx registers AdminAnalytics lazy import and route", () => {
    const appPath = path.join(srcDir, "App.jsx");
    const content = fs.readFileSync(appPath, "utf-8");

    assert.ok(content.includes('import("./pages/admin/AdminAnalytics")'), "App.jsx must lazy import AdminAnalytics");
    assert.ok(content.includes('<Route path="analytics" element={<AdminAnalytics />} />'), "App.jsx must define analytics route");
  });

  test("4. Verifies adminApi.js contains both Workstream 5 telemetry methods and Workstream 5.6 business insight methods", () => {
    const apiPath = path.join(srcDir, "services/adminApi.js");
    const content = fs.readFileSync(apiPath, "utf-8");

    // Preserved Workstream 5 methods
    assert.ok(content.includes("getAnalyticsSummary:"), "adminApi must have getAnalyticsSummary");
    assert.ok(content.includes("getAnalyticsTrends:"), "adminApi must have getAnalyticsTrends");
    assert.ok(content.includes("getAnalyticsEvents:"), "adminApi must have getAnalyticsEvents");
    assert.ok(content.includes("getAnalyticsWorkshops:"), "adminApi must have getAnalyticsWorkshops");
    assert.ok(content.includes("getAnalyticsRecent:"), "adminApi must have getAnalyticsRecent");

    // Workstream 5.6 Business Insights methods
    assert.ok(content.includes("getAdminInsightsOverview:"), "adminApi must have getAdminInsightsOverview");
    assert.ok(content.includes("getAdminInsightsTrends:"), "adminApi must have getAdminInsightsTrends");
    assert.ok(content.includes("getAdminInsightsWorkshops:"), "adminApi must have getAdminInsightsWorkshops");
    assert.ok(content.includes("getAdminInsightsPayments:"), "adminApi must have getAdminInsightsPayments");
    assert.ok(content.includes("getAdminInsightsLive:"), "adminApi must have getAdminInsightsLive");
    assert.ok(content.includes("getAdminInsightsActivity:"), "adminApi must have getAdminInsightsActivity");
  });

  test("5. Verifies AdminAnalytics.jsx renders Img1 Live Insights UI with conversion funnel and business metrics", () => {
    const pagePath = path.join(srcDir, "pages/admin/AdminAnalytics.jsx");
    const content = fs.readFileSync(pagePath, "utf-8");

    assert.ok(content.includes("Live Insights"), "Page must have Live Insights title");
    assert.ok(content.includes("RANGE_OPTIONS"), "Page must define time range options");
    assert.ok(content.includes("adminApi.getAdminInsightsOverview"), "Page must call getAdminInsightsOverview");
    assert.ok(content.includes("adminApi.getAdminInsightsTrends"), "Page must call getAdminInsightsTrends");
    assert.ok(content.includes("adminApi.getAdminInsightsWorkshops"), "Page must call getAdminInsightsWorkshops");
    assert.ok(content.includes("adminApi.getAdminInsightsPayments"), "Page must call getAdminInsightsPayments");
    assert.ok(content.includes("adminApi.getAdminInsightsLive"), "Page must call getAdminInsightsLive");
    assert.ok(content.includes("adminApi.getAdminInsightsActivity"), "Page must call getAdminInsightsActivity");
    assert.ok(content.includes("Conversion Funnel"), "Page must render Conversion Funnel");
    assert.ok(content.includes("Payment Outcomes"), "Page must render Payment Outcomes");
    assert.ok(content.includes("Top Workshops by Interest"), "Page must render Top Workshops");
    assert.ok(content.includes("Recent Activity Feed"), "Page must render Recent Activity Feed");
    assert.ok(content.includes("ResponsiveContainer"), "Page must use Recharts ResponsiveContainer");
  });
});
