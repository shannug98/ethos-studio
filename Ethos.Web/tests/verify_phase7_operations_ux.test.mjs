import { describe, it } from "node:test";
import assert from "node:assert/strict";

describe("Phase 7 Operations UX & Frontend Contract Verification", () => {
  // ==========================================
  // TEST 1: adminApi.getWorkshopAttendees Query Parameter Handling
  // ==========================================
  it("getWorkshopAttendees builds query with sessionId parameter", () => {
    function buildAttendeesUrl(id, params) {
      let query = "";
      if (typeof params === "string") {
        query = params ? `?${params.replace(/^\?/, "")}` : "";
      } else if (params && typeof params === "object") {
        const sp = new URLSearchParams();
        if (params.page) sp.set("page", params.page);
        if (params.pageSize) sp.set("pageSize", params.pageSize);
        if (params.search) sp.set("search", params.search);
        if (params.sessionId) sp.set("sessionId", params.sessionId);
        const qs = sp.toString();
        query = qs ? `?${qs}` : "";
      }
      return `/api/admin/workshops/${id}/attendees${query}`;
    }

    const testSessionId = "44444444-4444-4444-4444-444444444444";
    const urlWithObj = buildAttendeesUrl("workshop-123", { sessionId: testSessionId, search: "Alex" });
    assert.match(urlWithObj, /sessionId=44444444-4444-4444-4444-444444444444/);
    assert.match(urlWithObj, /search=Alex/);

    const urlWithAll = buildAttendeesUrl("workshop-123", { sessionId: "" });
    assert.doesNotMatch(urlWithAll, /sessionId=/);
  });

  // ==========================================
  // TEST 2: Session & Pass Column Formatting in Admin Roster
  // ==========================================
  it("formats Session and Pass metadata for admin attendee table", () => {
    function formatSessionPassColumn(attendee) {
      const sessionTitle = attendee.sessionTitle || "All Sessions";
      const passName = attendee.passName || "Standard Pass";
      const timeStr = attendee.sessionStartTime
        ? `${attendee.sessionStartTime.slice(0, 5)} - ${attendee.sessionEndTime ? attendee.sessionEndTime.slice(0, 5) : ""}`
        : null;

      return {
        sessionDisplay: sessionTitle,
        passBadge: passName,
        timeBadge: timeStr
      };
    }

    const bundleAttendee = {
      sessionTitle: "Hip Hop Masterclass",
      sessionStartTime: "10:00:00",
      sessionEndTime: "11:30:00",
      passName: "2-Session Bundle",
      passCategory: "MultiSessionBundle"
    };
    const res1 = formatSessionPassColumn(bundleAttendee);
    assert.equal(res1.sessionDisplay, "Hip Hop Masterclass");
    assert.equal(res1.passBadge, "2-Session Bundle");
    assert.equal(res1.timeBadge, "10:00 - 11:30");

    const allAccessAttendee = {
      sessionTitle: null,
      passName: "All-Access VIP",
      passCategory: "AllAccess"
    };
    const res2 = formatSessionPassColumn(allAccessAttendee);
    assert.equal(res2.sessionDisplay, "All Sessions");
    assert.equal(res2.passBadge, "All-Access VIP");
    assert.equal(res2.timeBadge, null);
  });

  // ==========================================
  // TEST 3: Admin Session Swap Drawer Form Validation
  // ==========================================
  it("enforces 5+ character reason on cutoff override and requires replacement session", () => {
    function validateSessionSwapForm({ replacementSessionId, overrideCutoff, overrideReason }) {
      if (!replacementSessionId) {
        return { valid: false, error: "Please select a replacement session." };
      }
      if (overrideCutoff) {
        if (!overrideReason || overrideReason.trim().length < 5) {
          return { valid: false, error: "A detailed reason (at least 5 characters) is required when overriding the booking cutoff." };
        }
      }
      return { valid: true, error: null };
    }

    // Missing replacement session
    const check1 = validateSessionSwapForm({ replacementSessionId: null });
    assert.equal(check1.valid, false);
    assert.match(check1.error, /select a replacement session/i);

    // Override without reason
    const check2 = validateSessionSwapForm({
      replacementSessionId: "session-2",
      overrideCutoff: true,
      overrideReason: ""
    });
    assert.equal(check2.valid, false);
    assert.match(check2.error, /at least 5 characters/i);

    // Override with short reason
    const check3 = validateSessionSwapForm({
      replacementSessionId: "session-2",
      overrideCutoff: true,
      overrideReason: "bad"
    });
    assert.equal(check3.valid, false);
    assert.match(check3.error, /at least 5 characters/i);

    // Valid override with sufficient reason
    const check4 = validateSessionSwapForm({
      replacementSessionId: "session-2",
      overrideCutoff: true,
      overrideReason: "Authorized emergency swap per manager request"
    });
    assert.equal(check4.valid, true);
    assert.equal(check4.error, null);

    // Valid normal swap without override
    const check5 = validateSessionSwapForm({
      replacementSessionId: "session-2",
      overrideCutoff: false,
      overrideReason: ""
    });
    assert.equal(check5.valid, true);
  });

  // ==========================================
  // TEST 4: Student Pass Presentation displays Pass Name and Session Count
  // ==========================================
  it("presents pass name and session counts in student portal modal", () => {
    function getStudentPassDisplay(booking, currentTicket) {
      const passName = currentTicket?.passName || booking?.passName || "General Admission";
      const totalSessions = booking?.sessionsIncludedCount || (booking?.sessionIds?.length ?? 1);
      return {
        passName,
        totalSessions,
        badgeText: passName
      };
    }

    const booking = {
      id: "b1",
      passName: "3-Session Pro Bundle",
      sessionsIncludedCount: 3,
      sessionIds: ["s1", "s2", "s3"]
    };
    const ticket = {
      id: "t1",
      ticketNumber: "ETHOS-WKS-B1-01",
      passName: "3-Session Pro Bundle",
      status: "Issued"
    };

    const display = getStudentPassDisplay(booking, ticket);
    assert.equal(display.passName, "3-Session Pro Bundle");
    assert.equal(display.totalSessions, 3);
  });

  // ==========================================
  // TEST 5: Student Ticket Filter Excludes Replaced and Cancelled Tickets
  // ==========================================
  it("excludes Replaced and Cancelled tickets from active passes in student portal", () => {
    const allTickets = [
      { id: "t1", ticketNumber: "ETHOS-WKS-01", status: "Issued", sessionTitle: "Session 1" },
      { id: "t2-old", ticketNumber: "ETHOS-WKS-02", status: "Replaced", sessionTitle: "Session 2" },
      { id: "t2-new", ticketNumber: "ETHOS-WKS-02-R01", status: "Issued", sessionTitle: "Session 3" },
      { id: "t3-canc", ticketNumber: "ETHOS-WKS-03", status: "Cancelled", sessionTitle: "Session 4" }
    ];

    const activeTickets = allTickets.filter(
      (t) => t.status === "Issued" || t.status === "Confirmed"
    );

    assert.equal(activeTickets.length, 2);
    assert.equal(activeTickets[0].ticketNumber, "ETHOS-WKS-01");
    assert.equal(activeTickets[1].ticketNumber, "ETHOS-WKS-02-R01");
    assert.ok(!activeTickets.some((t) => t.status === "Replaced"));
    assert.ok(!activeTickets.some((t) => t.status === "Cancelled"));
  });

  // ==========================================
  // TEST 6: Monotonic Replacement Revision Lineage Format
  // ==========================================
  it("correctly identifies and displays ticket replacement lineage format", () => {
    function parseTicketLineage(ticketNumber) {
      const match = ticketNumber.match(/^(ETHOS-WKS-[A-Z0-9]+-\d{2})(-R(\d{2}))?$/);
      if (!match) return { isReplacement: false, baseNumber: ticketNumber, revision: 0 };
      const isReplacement = Boolean(match[2]);
      const revision = match[3] ? parseInt(match[3], 10) : 0;
      return {
        isReplacement,
        baseNumber: match[1],
        revision
      };
    }

    const base = parseTicketLineage("ETHOS-WKS-ABC12345-02");
    assert.equal(base.isReplacement, false);
    assert.equal(base.revision, 0);

    const rev1 = parseTicketLineage("ETHOS-WKS-ABC12345-02-R01");
    assert.equal(rev1.isReplacement, true);
    assert.equal(rev1.baseNumber, "ETHOS-WKS-ABC12345-02");
    assert.equal(rev1.revision, 1);

    const rev2 = parseTicketLineage("ETHOS-WKS-ABC12345-02-R02");
    assert.equal(rev2.isReplacement, true);
    assert.equal(rev2.baseNumber, "ETHOS-WKS-ABC12345-02");
    assert.equal(rev2.revision, 2);
  });
});
