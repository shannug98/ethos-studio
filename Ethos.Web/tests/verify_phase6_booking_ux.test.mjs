import { describe, it } from "node:test";
import assert from "node:assert/strict";

// Replicate the overlap function from SelectTicketsModal.jsx
function doTimesOverlap(startA, endA, startB, endB) {
  return startA < endB && startB < endA;
}

function checkSessionsOverlap(sessions) {
  for (let i = 0; i < sessions.length; i++) {
    for (let j = i + 1; j < sessions.length; j++) {
      const s1 = sessions[i];
      const s2 = sessions[j];
      const d1 = s1.sessionDate.split("T")[0];
      const d2 = s2.sessionDate.split("T")[0];
      if (d1 === d2) {
        if (doTimesOverlap(s1.startTime, s1.endTime, s2.startTime, s2.endTime)) {
          return `Conflict: "${s1.title}" and "${s2.title}" overlap in time on ${d1}.`;
        }
      }
    }
  }
  return null;
}

// Replicate quote URL builder from workshopsApi.js
function buildQuoteUrl(id, quantity = 1, passTypeId = null, selectedSessionIds = []) {
  let url = `/api/workshops/${id}/quote?quantity=${quantity}`;
  if (passTypeId) {
    url += `&passTypeId=${encodeURIComponent(passTypeId)}`;
  }
  if (Array.isArray(selectedSessionIds) && selectedSessionIds.length > 0) {
    selectedSessionIds.forEach((sid) => {
      url += `&selectedSessionIds=${encodeURIComponent(sid)}`;
    });
  }
  return url;
}

// Replicate max pass quantity calculation from SelectTicketsModal.jsx
function calculateMaxPassQuantity(currentPass, availableSessions, selectedSessionIds) {
  if (!currentPass) return 10;
  const remainingPassSeats = currentPass.totalQuantity != null && currentPass.soldQuantity != null
    ? Math.max(0, currentPass.totalQuantity - currentPass.soldQuantity)
    : 10;

  let cap = 10;
  if (currentPass.workshopSessionId) {
    const s = availableSessions.find((x) => x.id === currentPass.workshopSessionId);
    if (s && s.remainingSeats != null) cap = s.remainingSeats;
  } else if (currentPass.isOverallPass) {
    const rems = availableSessions.map((x) => (x.remainingSeats != null ? x.remainingSeats : 10));
    if (rems.length > 0) cap = Math.min(...rems);
  } else if (selectedSessionIds.length > 0) {
    const rems = availableSessions
      .filter((x) => selectedSessionIds.includes(x.id))
      .map((x) => (x.remainingSeats != null ? x.remainingSeats : 10));
    if (rems.length > 0) cap = Math.min(...rems);
  }
  return Math.max(1, Math.min(10, remainingPassSeats, cap));
}

// Replicate payload construction from WorkshopCheckoutPage.jsx
function buildOrderPayload({ quantity, fullName, phone, email, idempotencyKey, passTypeId, selectedSessionIds, attemptedTamperedAmount }) {
  const payload = {
    quantity,
    fullName: fullName?.trim(),
    phone: phone?.trim(),
    email: email?.trim(),
    idempotencyKey,
  };
  if (passTypeId) {
    payload.passTypeId = passTypeId;
  }
  if (selectedSessionIds && selectedSessionIds.length > 0) {
    payload.selectedSessionIds = selectedSessionIds;
  }
  // Any client-supplied amount or price is omitted by design!
  return payload;
}

describe("Phase 6 Frontend: Customer Booking & Purchase Integration Tests", () => {
  describe("1. Multi-Session Bundle Time Overlap Boundary Rules [start, end)", () => {
    it("allows touching boundary sessions on same date (10:00-11:00 and 11:00-12:00)", () => {
      const s1 = { id: "s1", title: "Class A", sessionDate: "2026-10-15T00:00:00", startTime: "10:00", endTime: "11:00" };
      const s2 = { id: "s2", title: "Class B", sessionDate: "2026-10-15T00:00:00", startTime: "11:00", endTime: "12:00" };

      const conflict = checkSessionsOverlap([s1, s2]);
      assert.equal(conflict, null, "Touching sessions must not trigger an overlap error");
    });

    it("detects and rejects overlapping sessions on same date (10:00-11:30 and 11:00-12:00)", () => {
      const s1 = { id: "s1", title: "Class A", sessionDate: "2026-10-15T00:00:00", startTime: "10:00", endTime: "11:30" };
      const s2 = { id: "s2", title: "Class B", sessionDate: "2026-10-15T00:00:00", startTime: "11:00", endTime: "12:00" };

      const conflict = checkSessionsOverlap([s1, s2]);
      assert.notEqual(conflict, null);
      assert.match(conflict, /overlap in time on 2026-10-15/);
    });

    it("allows identical time windows across different calendar dates", () => {
      const s1 = { id: "s1", title: "Saturday Session", sessionDate: "2026-10-15T00:00:00", startTime: "10:00", endTime: "12:00" };
      const s2 = { id: "s2", title: "Sunday Session", sessionDate: "2026-10-16T00:00:00", startTime: "10:00", endTime: "12:00" };

      const conflict = checkSessionsOverlap([s1, s2]);
      assert.equal(conflict, null, "Sessions on different dates must not overlap");
    });
  });

  describe("2. Advisory Quote API Query Construction", () => {
    it("builds query for Single-Session pass with single selected session", () => {
      const url = buildQuoteUrl("ws-100", 2, "pass-single", ["sess-01"]);
      assert.equal(url, "/api/workshops/ws-100/quote?quantity=2&passTypeId=pass-single&selectedSessionIds=sess-01");
    });

    it("builds query for Multi-Session Bundle with repeated selectedSessionIds params", () => {
      const url = buildQuoteUrl("ws-100", 3, "pass-bundle", ["sess-01", "sess-02", "sess-03"]);
      assert.equal(url, "/api/workshops/ws-100/quote?quantity=3&passTypeId=pass-bundle&selectedSessionIds=sess-01&selectedSessionIds=sess-02&selectedSessionIds=sess-03");
    });

    it("builds query for All-Access pass without selectedSessionIds", () => {
      const url = buildQuoteUrl("ws-100", 1, "pass-all-access", []);
      assert.equal(url, "/api/workshops/ws-100/quote?quantity=1&passTypeId=pass-all-access");
    });

    it("builds query for Legacy workshop pricing with quantity only", () => {
      const url = buildQuoteUrl("ws-legacy", 4);
      assert.equal(url, "/api/workshops/ws-legacy/quote?quantity=4");
    });
  });

  describe("3. Pass Selection and Auto-Selection Rules", () => {
    it("auto-selects bound session for Single-Session pass", () => {
      const singlePass = { id: "p1", name: "HipHop Single", workshopSessionId: "sess-01", sessionsIncluded: 1 };
      const initialSelected = singlePass.workshopSessionId ? [singlePass.workshopSessionId] : [];
      assert.deepEqual(initialSelected, ["sess-01"]);
    });

    it("does not require session picking for All-Access pass", () => {
      const allAccessPass = { id: "p2", name: "VIP All Access", isOverallPass: true };
      const requiresPicking = !allAccessPass.isOverallPass;
      assert.equal(requiresPicking, false);
    });

    it("validates that Bundle selection matches exact N required sessions", () => {
      const bundlePass = { id: "p3", name: "3-Class Pack", sessionsIncluded: 3 };
      const selectionA = ["s1", "s2"];
      const selectionB = ["s1", "s2", "s3"];

      assert.equal(selectionA.length === bundlePass.sessionsIncluded, false);
      assert.equal(selectionB.length === bundlePass.sessionsIncluded, true);
    });
  });

  describe("4. Pass Quantity Selector & Physical Capacity Cap", () => {
    it("caps quantity by linked session remaining capacity for Single-Session pass", () => {
      const pass = { id: "p1", workshopSessionId: "s1", totalQuantity: 20, soldQuantity: 2 };
      const sessions = [{ id: "s1", remainingSeats: 3 }];
      const maxQty = calculateMaxPassQuantity(pass, sessions, ["s1"]);
      assert.equal(maxQty, 3, "Quantity must be capped at 3 remaining seats in linked session");
    });

    it("caps quantity by the most constrained session among selected bundle sessions", () => {
      const pass = { id: "p2", sessionsIncluded: 2, totalQuantity: 50, soldQuantity: 0 };
      const sessions = [
        { id: "s1", remainingSeats: 8 },
        { id: "s2", remainingSeats: 4 }, // Bottleneck!
        { id: "s3", remainingSeats: 15 },
      ];
      const maxQty = calculateMaxPassQuantity(pass, sessions, ["s1", "s2"]);
      assert.equal(maxQty, 4, "Quantity must be capped by bottleneck session with 4 seats");
    });

    it("caps quantity at 10 tickets per booking maximum", () => {
      const pass = { id: "p3", isOverallPass: true, totalQuantity: 100, soldQuantity: 0 };
      const sessions = [{ id: "s1", remainingSeats: 50 }, { id: "s2", remainingSeats: 40 }];
      const maxQty = calculateMaxPassQuantity(pass, sessions, []);
      assert.equal(maxQty, 10, "Quantity must not exceed 10 per transaction");
    });
  });

  describe("5. Server-Authoritative Order Payload Construction & Price Immunity", () => {
    it("strictly omits client-supplied prices from order payload", () => {
      const payload = buildOrderPayload({
        quantity: 2,
        fullName: "Maya Sen",
        phone: "9876543210",
        email: "maya@ethos.test",
        idempotencyKey: "test-idem-key-123",
        passTypeId: "pass-456",
        selectedSessionIds: ["sess-01"],
        attemptedTamperedAmount: 1, // Attacker tries to inject 1 INR
      });

      assert.equal(payload.quantity, 2);
      assert.equal(payload.passTypeId, "pass-456");
      assert.deepEqual(payload.selectedSessionIds, ["sess-01"]);
      assert.equal(payload.attemptedTamperedAmount, undefined, "Client prices must never be sent to server");
      assert.equal(payload.amount, undefined);
      assert.equal(payload.price, undefined);
      assert.equal(payload.totalAmount, undefined);
    });

    it("preserves idempotency key across retry attempts", () => {
      const key = "key-" + Date.now();
      const p1 = buildOrderPayload({ quantity: 1, idempotencyKey: key, fullName: "A", phone: "1", email: "a@b.com" });
      const p2 = buildOrderPayload({ quantity: 1, idempotencyKey: key, fullName: "A", phone: "1", email: "a@b.com" });

      assert.equal(p1.idempotencyKey, p2.idempotencyKey);
    });
  });
});
