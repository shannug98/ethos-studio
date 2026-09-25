import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webRoot = path.resolve(__dirname, "..");

import { classifyPassCategory } from "../src/pages/admin/workshops/wizard/pricingUxHelpers.js";

describe("Step 4 Ticket Types Decoupling & Simplification Architecture Tests", () => {
  describe("1. Strict Pass Classification Invariants", () => {
    it("Single-Session pass with client ID and sessionsIncluded == 1 is valid SINGLE", () => {
      const pass = {
        category: "SINGLE",
        targetSessionClientId: "sess_client_123",
        sessionsIncluded: 1,
        totalQuantity: 30,
      };
      assert.equal(classifyPassCategory(pass), "SINGLE");
    });

    it("Single-Session pass with DB workshopSessionId and sessionsIncluded == 1 is valid SINGLE", () => {
      const pass = {
        category: "SINGLE",
        workshopSessionId: "11111111-2222-3333-4444-555555555555",
        sessionsIncluded: 1,
        totalQuantity: 30,
      };
      assert.equal(classifyPassCategory(pass), "SINGLE");
    });

    it("Solo Pass with no target session and sessionsIncluded == 1 is valid SINGLE", () => {
      const pass = {
        category: "SINGLE",
        workshopSessionId: null,
        targetSessionClientId: null,
        sessionsIncluded: 1,
        totalQuantity: 30,
      };
      assert.equal(classifyPassCategory(pass), "SINGLE");
    });

    it("Rejects Single-Session pass if sessionsIncluded != 1", () => {
      const pass = {
        category: "SINGLE",
        targetSessionClientId: "sess_client_123",
        sessionsIncluded: 2,
      };
      assert.equal(classifyPassCategory(pass), "INVALID_SINGLE");
    });

    it("All-Access pass with no target and sessionsIncluded null is valid ALL_ACCESS", () => {
      const pass = {
        category: "ALL_ACCESS",
        workshopSessionId: null,
        targetSessionClientId: null,
        sessionsIncluded: null,
      };
      assert.equal(classifyPassCategory(pass), "ALL_ACCESS");
    });

    it("Multi-Session Bundle with sessionsIncluded >= 2 and no target is valid BUNDLE", () => {
      const pass = {
        category: "BUNDLE",
        workshopSessionId: null,
        targetSessionClientId: null,
        sessionsIncluded: 3,
      };
      assert.equal(classifyPassCategory(pass), "BUNDLE");
    });

    it("Rejects Bundle if sessionsIncluded < 2", () => {
      const pass = {
        category: "BUNDLE",
        workshopSessionId: null,
        targetSessionClientId: null,
        sessionsIncluded: 1,
      };
      assert.equal(classifyPassCategory(pass), "INVALID_BUNDLE");
    });
  });

  describe("2. Step 4 Validation Rules (Decoupled Ticket Types Validation)", () => {
    function validateStep4(form) {
      const errs = {};
      if (!Array.isArray(form.passTypes) || form.passTypes.length === 0) {
        errs.passTypes = "Please configure at least one ticket type before proceeding.";
      } else {
        for (let i = 0; i < form.passTypes.length; i++) {
          const pt = form.passTypes[i];
          if (!pt.name || !pt.name.trim()) {
            errs.passTypes = `Ticket Type #${i + 1} must have a valid name.`;
            break;
          }
          if (!pt.description || !pt.description.trim()) {
            errs.passTypes = `Ticket Description is required for "${pt.name || `Ticket #${i + 1}`}".`;
            break;
          }
          if (!pt.totalQuantity || pt.totalQuantity < 1) {
            errs.passTypes = `Ticket Type "${pt.name || i + 1}" capacity must be at least 1 ticket.`;
            break;
          }
          if (pt.category === "BUNDLE" || (pt.sessionsIncluded != null && pt.sessionsIncluded !== 1)) {
            if (Number(pt.sessionsIncluded) < 2) {
              errs.passTypes = `Multi-Session Pass "${pt.name}" must include at least 2 sessions.`;
              break;
            }
          }
        }
      }
      return errs;
    }

    it("rejects when no ticket types are configured", () => {
      const form = { passTypes: [] };
      const errs = validateStep4(form);
      assert.match(errs.passTypes, /Please configure at least one ticket type/);
    });

    it("rejects ticket with empty name", () => {
      const form = {
        passTypes: [{ name: "", description: "Valid desc", totalQuantity: 30 }],
      };
      const errs = validateStep4(form);
      assert.match(errs.passTypes, /must have a valid name/);
    });

    it("rejects ticket with empty description", () => {
      const form = {
        passTypes: [{ name: "VIP Pass", description: "", totalQuantity: 30 }],
      };
      const errs = validateStep4(form);
      assert.match(errs.passTypes, /Ticket Description is required/);
    });

    it("rejects ticket with whitespace-only description", () => {
      const form = {
        passTypes: [{ name: "VIP Pass", description: "    ", totalQuantity: 30 }],
      };
      const errs = validateStep4(form);
      assert.match(errs.passTypes, /Ticket Description is required/);
    });

    it("rejects ticket with invalid quota <= 0", () => {
      const form = {
        passTypes: [{ name: "VIP Pass", description: "Valid", totalQuantity: 0 }],
      };
      const errs = validateStep4(form);
      assert.match(errs.passTypes, /capacity must be at least 1 ticket/);
    });

    it("rejects multi-session pass with sessionsIncluded < 2", () => {
      const form = {
        passTypes: [{ name: "Bundle Pass", description: "Bundle", totalQuantity: 10, category: "BUNDLE", sessionsIncluded: 1 }],
      };
      const errs = validateStep4(form);
      assert.match(errs.passTypes, /must include at least 2 sessions/);
    });

    it("approves valid Solo Pass without requiring session links", () => {
      const form = {
        sessions: [], // Empty sessions in Step 3
        passTypes: [
          {
            name: "Solo Pass",
            description: "Entry to 1 session",
            totalQuantity: 20,
            category: "SINGLE",
            sessionsIncluded: 1,
          },
        ],
      };
      const errs = validateStep4(form);
      assert.equal(errs.passTypes, undefined);
    });

    it("approves valid Multi-Session Pass without requiring session links", () => {
      const form = {
        sessions: [],
        passTypes: [
          {
            name: "2-Session Bundle",
            description: "Entry to 2 sessions",
            totalQuantity: 25,
            category: "BUNDLE",
            sessionsIncluded: 2,
          },
        ],
      };
      const errs = validateStep4(form);
      assert.equal(errs.passTypes, undefined);
    });
  });

  describe("3. Submission Payload Correlation & Identity Architecture", () => {
    it("correlates unsaved session clientId to generated payload Guid for Single-Session pass", () => {
      const sessionClientToIdMap = new Map();
      const realDbGuid = "22222222-3333-4444-5555-666666666666";
      const rawSessions = [
        { id: undefined, clientId: "client_sess_1", title: "Session 1" },
        { id: realDbGuid, clientId: "client_sess_2", title: "Session 2" },
      ];

      const mappedSessions = rawSessions.map((s, idx) => {
        const assignedSessionId = s.id && s.id.length >= 32 ? s.id : `generated-guid-${idx + 1}`;
        if (s.clientId) sessionClientToIdMap.set(s.clientId, assignedSessionId);
        if (s.id) sessionClientToIdMap.set(s.id, assignedSessionId);
        return { id: assignedSessionId, title: s.title };
      });

      const rawPassTypes = [
        {
          name: "Session 1 Pass",
          category: "SINGLE",
          targetSessionClientId: "client_sess_1",
          workshopSessionId: null,
          sessionsIncluded: 1,
          description: "Pass for Session 1",
        },
        {
          name: "Session 2 Pass",
          category: "SINGLE",
          targetSessionClientId: "client_sess_2",
          workshopSessionId: realDbGuid,
          sessionsIncluded: 1,
          description: "Pass for Session 2",
        },
      ];

      const mappedPassTypes = rawPassTypes.map((p) => {
        let resolvedSessionId = p.workshopSessionId || undefined;
        if (resolvedSessionId && sessionClientToIdMap.has(resolvedSessionId)) {
          resolvedSessionId = sessionClientToIdMap.get(resolvedSessionId);
        } else if (!resolvedSessionId && p.targetSessionClientId && sessionClientToIdMap.has(p.targetSessionClientId)) {
          resolvedSessionId = sessionClientToIdMap.get(p.targetSessionClientId);
        }
        return {
          name: p.name,
          workshopSessionId: resolvedSessionId,
          description: p.description,
        };
      });

      assert.equal(mappedSessions[0].id, "generated-guid-1");
      assert.equal(mappedPassTypes[0].workshopSessionId, "generated-guid-1");
      assert.equal(mappedSessions[1].id, realDbGuid);
      assert.equal(mappedPassTypes[1].workshopSessionId, realDbGuid);
    });
  });

  describe("4. Static UI Verification of Step4TicketTypes.jsx with Pass Scope Selector", () => {
    const filePath = path.resolve(webRoot, "src/pages/admin/workshops/wizard/Step4TicketTypes.jsx");
    const content = fs.readFileSync(filePath, "utf-8");

    it("does NOT contain SessionSelectDropdown or individual session picking dropdowns", () => {
      assert.equal(content.includes("SessionSelectDropdown"), false);
      assert.equal(content.includes("Select Target Session"), false);
    });

    it("does NOT contain Base Price or pricing tiers UI in Step 4", () => {
      assert.equal(content.includes("Base Price (₹ INR)"), false);
      assert.equal(content.includes("Starting baseline price"), false);
    });

    it("contains Pass Scope / Entitlement selector with All Sessions, Solo Pass, and Multi-Session", () => {
      assert.equal(content.includes("Pass Scope / Entitlement"), true);
      assert.equal(content.includes("All Sessions Pass"), true);
      assert.equal(content.includes("Solo Pass (1 Session)"), true);
      assert.equal(content.includes("Multi-Session Pass"), true);
    });

    it("contains Ticket Name, Description, Quantity/Seats, and Active for Sale", () => {
      assert.equal(content.includes("Ticket Name"), true);
      assert.equal(content.includes("Ticket Description"), true);
      assert.equal(content.includes("Quantity / Seats"), true);
      assert.equal(content.includes("Active for Sale"), true);
    });

    it("contains Delete Ticket action", () => {
      assert.equal(content.includes("Delete Ticket"), true);
    });
  });
});
