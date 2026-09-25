import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  calculateSimulatedQuote,
  getTierValidation,
  classifyPassCategory,
} from "../src/pages/admin/workshops/wizard/pricingUxHelpers.js";

describe("Phase 5 Frontend: Pass Types & Dynamic Pricing UX Verification", () => {
  describe("1. Pass Classification Matrix", () => {
    const classifyPass = classifyPassCategory;

    it("correctly identifies Single-Session pass (SessionId set, SessionsIncluded == 1)", () => {
      const pass = { workshopSessionId: "sess-01", sessionsIncluded: 1, totalQuantity: 30 };
      assert.equal(classifyPass(pass), "SINGLE");
    });

    it("rejects Single-Session pass with SessionsIncluded != 1", () => {
      const pass = { workshopSessionId: "sess-01", sessionsIncluded: 2, totalQuantity: 30 };
      assert.equal(classifyPass(pass), "INVALID_SINGLE");
    });

    it("correctly identifies All-Access pass (SessionId null, SessionsIncluded null)", () => {
      const pass = { workshopSessionId: null, sessionsIncluded: null, totalQuantity: 50 };
      assert.equal(classifyPass(pass), "ALL_ACCESS");
    });

    it("correctly identifies Multi-Session Bundle (SessionId null, SessionsIncluded >= 2)", () => {
      const pass2 = { workshopSessionId: null, sessionsIncluded: 2, totalQuantity: 25 };
      const pass3 = { workshopSessionId: null, sessionsIncluded: 3, totalQuantity: 25 };
      assert.equal(classifyPass(pass2), "BUNDLE");
      assert.equal(classifyPass(pass3), "BUNDLE");
    });

    it("rejects bundle with SessionsIncluded < 2", () => {
      const pass = { workshopSessionId: null, sessionsIncluded: 1, totalQuantity: 25 };
      assert.equal(classifyPass(pass), "INVALID_BUNDLE");
    });
  });

  describe("2. Mathematical Tier Validation (getTierValidation)", () => {
    it("approves a valid contiguous, monotonic 4-tier configuration", () => {
      const tiers = [
        { tierNumber: 1, minTickets: 1, maxTickets: 10, price: 500 },
        { tierNumber: 2, minTickets: 11, maxTickets: 20, price: 600 },
        { tierNumber: 3, minTickets: 21, maxTickets: 30, price: 700 },
        { tierNumber: 4, minTickets: 31, maxTickets: null, price: 800 },
      ];
      const res = getTierValidation(tiers, 50);
      assert.equal(res.valid, true);
    });

    it("rejects when Tier 1 does not start at minTickets = 1", () => {
      const tiers = [
        { tierNumber: 1, minTickets: 5, maxTickets: 10, price: 500 },
        { tierNumber: 2, minTickets: 11, maxTickets: null, price: 600 },
      ];
      const res = getTierValidation(tiers, 50);
      assert.equal(res.valid, false);
      assert.match(res.message, /must start at Min Tickets = 1/);
    });

    it("rejects zero or negative price", () => {
      const tiers = [
        { tierNumber: 1, minTickets: 1, maxTickets: 10, price: 0 },
        { tierNumber: 2, minTickets: 11, maxTickets: null, price: 600 },
      ];
      const res = getTierValidation(tiers, 50);
      assert.equal(res.valid, false);
      assert.match(res.message, /must be greater than zero/);
    });

    it("rejects decreasing prices across tiers", () => {
      const tiers = [
        { tierNumber: 1, minTickets: 1, maxTickets: 10, price: 600 },
        { tierNumber: 2, minTickets: 11, maxTickets: null, price: 500 },
      ];
      const res = getTierValidation(tiers, 50);
      assert.equal(res.valid, false);
      assert.match(res.message, /Non-decreasing pricing rule violated/);
    });

    it("rejects discontinuity between tiers (gap)", () => {
      const tiers = [
        { tierNumber: 1, minTickets: 1, maxTickets: 10, price: 500 },
        { tierNumber: 2, minTickets: 12, maxTickets: null, price: 600 }, // gap at 11
      ];
      const res = getTierValidation(tiers, 50);
      assert.equal(res.valid, false);
      assert.match(res.message, /Discontinuity/);
    });

    it("rejects non-final tier having null maxTickets (open-ended)", () => {
      const tiers = [
        { tierNumber: 1, minTickets: 1, maxTickets: null, price: 500 },
        { tierNumber: 2, minTickets: 11, maxTickets: 20, price: 600 },
      ];
      const res = getTierValidation(tiers, 50);
      assert.equal(res.valid, false);
      assert.match(res.message, /Only the final tier.*can be open-ended/);
    });

    it("rejects maxTickets < minTickets", () => {
      const tiers = [
        { tierNumber: 1, minTickets: 5, maxTickets: 4, price: 500 },
      ];
      const res = getTierValidation(tiers, 50);
      assert.equal(res.valid, false);
    });
  });

  describe("3. Cart Split Simulator Algorithm (calculateSimulatedQuote)", () => {
    const standardTiers = [
      { tierNumber: 1, tierName: "Early", minTickets: 1, maxTickets: 10, price: 500 },
      { tierNumber: 2, tierName: "Standard", minTickets: 11, maxTickets: 20, price: 600 },
      { tierNumber: 3, tierName: "Peak", minTickets: 21, maxTickets: null, price: 700 },
    ];

    it("Case A: Single Tier Order entirely inside Tier 1", () => {
      // 5 tickets starting at sold = 0
      const quote = calculateSimulatedQuote(standardTiers, 50, 0, 5);
      assert.equal(quote.error, null);
      assert.equal(quote.splits.length, 1);
      assert.equal(quote.splits[0].quantity, 5);
      assert.equal(quote.splits[0].unitPrice, 500);
      assert.equal(quote.splits[0].subtotal, 2500);
      assert.equal(quote.total, 2500);
      assert.equal(quote.avgPrice, 500);
      assert.match(quote.scenarioLabel, /Scenario A/);
    });

    it("Case B: Order splits across boundary between Tier 1 and Tier 2", () => {
      // 8 already sold. Order 5 tickets -> tickets #9, 10 in Tier 1; #11, 12, 13 in Tier 2
      const quote = calculateSimulatedQuote(standardTiers, 50, 8, 5);
      assert.equal(quote.error, null);
      assert.equal(quote.splits.length, 2);
      assert.equal(quote.splits[0].quantity, 2);
      assert.equal(quote.splits[0].unitPrice, 500);
      assert.equal(quote.splits[0].subtotal, 1000);
      assert.equal(quote.splits[1].quantity, 3);
      assert.equal(quote.splits[1].unitPrice, 600);
      assert.equal(quote.splits[1].subtotal, 1800);
      assert.equal(quote.total, 2800);
      assert.equal(quote.avgPrice, 560);
      assert.match(quote.scenarioLabel, /Scenario B/);
    });

    it("Case C: Order splits across 3 distinct tiers", () => {
      // 0 sold. Order 25 tickets -> 10 @ 500, 10 @ 600, 5 @ 700
      const quote = calculateSimulatedQuote(standardTiers, 50, 0, 25);
      assert.equal(quote.error, null);
      assert.equal(quote.splits.length, 3);
      assert.equal(quote.splits[0].quantity, 10);
      assert.equal(quote.splits[0].unitPrice, 500);
      assert.equal(quote.splits[1].quantity, 10);
      assert.equal(quote.splits[1].unitPrice, 600);
      assert.equal(quote.splits[2].quantity, 5);
      assert.equal(quote.splits[2].unitPrice, 700);
      // Total: 5000 + 6000 + 3500 = 14500
      assert.equal(quote.total, 14500);
      assert.equal(quote.avgPrice, 580);
      assert.match(quote.scenarioLabel, /Scenario C/);
    });

    it("Case D: Order entirely within final open-ended tier", () => {
      // 25 already sold. Order 5 tickets -> all 5 in Tier 3 (21+)
      const quote = calculateSimulatedQuote(standardTiers, 50, 25, 5);
      assert.equal(quote.error, null);
      assert.equal(quote.splits.length, 1);
      assert.equal(quote.splits[0].quantity, 5);
      assert.equal(quote.splits[0].unitPrice, 700);
      assert.equal(quote.total, 3500);
      assert.equal(quote.avgPrice, 700);
      assert.match(quote.scenarioLabel, /Scenario D/);
    });

    it("Case E: Capacity exceeded produces descriptive error", () => {
      // Quota = 30. Sold = 28. Order 5 tickets -> exceeds capacity
      const quote = calculateSimulatedQuote(standardTiers, 30, 28, 5);
      assert.notEqual(quote.error, null);
      assert.equal(quote.isCapacityExceeded, true);
      assert.match(quote.error, /Capacity exceeded.*exceeds commercial quota of 30/);
    });
  });

  describe("4. Contiguity Auto-Sync Logic", () => {
    it("updates subsequent tier minTickets when preceding tier maxTickets is modified", () => {
      let tiers = [
        { tierNumber: 1, minTickets: 1, maxTickets: 10, price: 500 },
        { tierNumber: 2, minTickets: 11, maxTickets: 20, price: 600 },
        { tierNumber: 3, minTickets: 21, maxTickets: null, price: 700 },
      ];

      // Simulate editing Tier 1 maxTickets to 15
      const newMax = 15;
      const tierIdx = 0;
      tiers = tiers.map((t, idx) => {
        if (idx === tierIdx) return { ...t, maxTickets: newMax };
        if (idx === tierIdx + 1) {
          const nextMin = newMax + 1;
          let nextMax = t.maxTickets;
          if (nextMax !== null && nextMax < nextMin) nextMax = nextMin + 9;
          return { ...t, minTickets: nextMin, maxTickets: nextMax };
        }
        return t;
      });

      assert.equal(tiers[0].maxTickets, 15);
      assert.equal(tiers[1].minTickets, 16);
      assert.equal(tiers[1].maxTickets, 20); // still >= 16

      // Validation check
      const valid = getTierValidation(tiers, 50);
      assert.equal(valid.valid, true);
    });
  });
});
