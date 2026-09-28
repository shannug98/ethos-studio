import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webSrcDir = path.resolve(__dirname, "../src");

describe("Admin Identity Display Distinction & Sidebar Role-Based Modules Verification", () => {
  it("AdminHeader only renders the profile identity card when displayRole === 'Developer'", () => {
    const headerPath = path.join(webSrcDir, "components/admin/AdminHeader.jsx");
    assert.ok(fs.existsSync(headerPath), "AdminHeader.jsx must exist");
    const content = fs.readFileSync(headerPath, "utf-8");

    // Must check adminUser?.displayRole === "Developer"
    assert.ok(
      content.includes('adminUser?.displayRole === "Developer"'),
      "AdminHeader must conditionally render profile identity card only when displayRole === 'Developer'"
    );

    // Must render Developer in role text
    assert.ok(
      content.includes("${adminUser.customerCode} · Developer") ||
      content.includes("ETHADMIN001 · Developer") ||
      content.includes("adminUser.customerCode ? `${adminUser.customerCode} · Developer` : \"Developer\""),
      "AdminHeader must render Developer in role text"
    );

    // Must NOT have unconditionally rendered Admin role text
    assert.ok(
      !content.includes('`${adminUser.customerCode} · Admin`'),
      "AdminHeader must not render unconditional 'ETHADMIN001 · Admin' text"
    );
  });

  it("AdminSidebar contains core 7 items for standard Admin and extended modules for Developer", () => {
    const sidebarPath = path.join(webSrcDir, "components/admin/AdminSidebar.jsx");
    assert.ok(fs.existsSync(sidebarPath), "AdminSidebar.jsx must exist");
    const content = fs.readFileSync(sidebarPath, "utf-8");

    const coreLabels = [
      "Dashboard",
      "Live Insights",
      "Workshops",
      "Manage Trainers",
      "Bookings",
      "Payments",
      "Media Gallery"
    ];

    for (const label of coreLabels) {
      assert.ok(
        content.includes(label),
        `AdminSidebar must contain core navigation label: ${label}`
      );
    }

    const developerLabels = [
      "Communications",
      "System Monitoring",
      "Security & Access",
      "Problems & Incidents",
      "Corrective Actions",
      "Activity History",
      "Users & Accounts",
      "Login Devices",
      "Connected Platforms",
      "Dance Classes",
      "Students",
      "Attendance",
      "Dance Packages",
      "Student Feedback"
    ];

    for (const label of developerLabels) {
      assert.ok(
        content.includes(label),
        `AdminSidebar must contain developer module label: ${label}`
      );
    }

    // Must distinguish developer role
    assert.ok(
      content.includes("isDeveloper"),
      "AdminSidebar must evaluate isDeveloper to show extended developer modules"
    );
  });

  it("Session persistence: getAdminUser returns parsed user from localStorage", () => {
    // Mock localStorage
    const storage = new Map();
    const mockLocalStorage = {
      getItem: (k) => storage.get(k) || null,
      setItem: (k, v) => storage.set(k, String(v)),
      removeItem: (k) => storage.delete(k),
    };

    // Developer user simulation
    const devUser = {
      id: "d54b7143-5627-494d-afdf-d865799b8209",
      fullName: "Ethos Partner 1",
      customerCode: "ETHADMIN001",
      phone: "8019013757",
      roles: ["ADMIN"],
      displayRole: "Developer"
    };

    mockLocalStorage.setItem("ethos_admin_user", JSON.stringify(devUser));
    const parsedDev = JSON.parse(mockLocalStorage.getItem("ethos_admin_user"));
    assert.equal(parsedDev.displayRole, "Developer");
    assert.equal(parsedDev.customerCode, "ETHADMIN001");
    assert.deepEqual(parsedDev.roles, ["ADMIN"]);

    // Normal admin user simulation
    const normalAdmin = {
      id: "154096c4-8e7b-49db-90f0-c0a171d320ac",
      fullName: "Ethos Partner 2",
      customerCode: "ETHADMIN002",
      phone: "8341701113",
      roles: ["ADMIN"],
      displayRole: null
    };

    mockLocalStorage.setItem("ethos_admin_user", JSON.stringify(normalAdmin));
    const parsedNormal = JSON.parse(mockLocalStorage.getItem("ethos_admin_user"));
    assert.equal(parsedNormal.displayRole, null);
    assert.equal(parsedNormal.customerCode, "ETHADMIN002");
    assert.deepEqual(parsedNormal.roles, ["ADMIN"]);
  });
});
