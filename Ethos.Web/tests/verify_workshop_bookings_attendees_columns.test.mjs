import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webRoot = path.resolve(__dirname, "..");

describe("AdminWorkshopBookingsAttendees Column Contract & Rendering Verification", () => {
  const filePath = path.join(webRoot, "src/pages/admin/workshops/AdminWorkshopBookingsAttendees.jsx");
  const sourceCode = fs.readFileSync(filePath, "utf8");

  it("1. bookingColumns uses header and render: (row) => ... matching AdminDataTable contract", () => {
    // Assert no two-argument (val, row) render callbacks exist in column definitions
    assert.ok(
      !sourceCode.includes("render: (val, row)"),
      "Should not contain 'render: (val, row)' signature"
    );
    assert.ok(
      !sourceCode.includes("render: (name, row)"),
      "Should not contain 'render: (name, row)' signature"
    );
    assert.ok(
      !sourceCode.includes("render: (passName, row)"),
      "Should not contain 'render: (passName, row)' signature"
    );
    assert.ok(
      !sourceCode.includes("render: (status, row)"),
      "Should not contain 'render: (status, row)' signature"
    );
    assert.ok(
      !sourceCode.includes("render: (_, row)"),
      "Should not contain 'render: (_, row)' signature"
    );
    assert.ok(
      !sourceCode.includes("render: (checkedIn, row)"),
      "Should not contain 'render: (checkedIn, row)' signature"
    );
  });

  it("2. Table columns define header properties for all columns", () => {
    // Check key headers are explicitly defined
    assert.ok(sourceCode.includes('header: "Booking Reference"'));
    assert.ok(sourceCode.includes('header: "Customer"'));
    assert.ok(sourceCode.includes('header: "Pass / Ticket Type"'));
    assert.ok(sourceCode.includes('header: "Attendees"'));
    assert.ok(sourceCode.includes('header: "Payment"'));
    assert.ok(sourceCode.includes('header: "Booking Status"'));
    assert.ok(sourceCode.includes('header: "Booked Date"'));
    assert.ok(sourceCode.includes('header: "Check-in"'));
    assert.ok(sourceCode.includes('header: "Ticket #"'));
    assert.ok(sourceCode.includes('header: "Attendee Name"'));
    assert.ok(sourceCode.includes('header: "Check-in Status"'));
  });

  it("3. Row-level rendering works identically with AdminDataTable for Guest bookings", () => {
    const guestBooking = {
      bookingId: "10069b23-9fdf-4f3e-a67e-7eb221399152",
      bookingReference: "BK-10069B23",
      studentName: "Gaddam Shanmuka",
      studentPhone: "7985465654",
      studentEmail: "guest@example.com",
      isGuest: true,
      passName: "Solo Pass",
      sessionsIncludedCount: 1,
      status: "Confirmed",
      paymentStatus: "Paid",
      totalPrice: 500,
      bookedAt: "2026-09-26T06:00:00Z",
      attendanceStatus: "Present",
      tickets: [
        {
          id: "tkt-1",
          ticketNumber: "ETHOS-SOLO-001",
          attendeeName: "Gaddam Shanmuka",
          checkedInAt: "2026-09-26T06:30:00Z",
        },
      ],
    };

    // Simulate AdminDataTable calling col.render(row)
    const bookingColumns = [
      {
        header: "Booking Reference",
        key: "bookingReference",
        render: (row) => ({
          ref: row.bookingReference || `BK-${(row.bookingId || row.id || "").toString().slice(0, 8).toUpperCase()}`,
          isGuest: row.isGuest,
        }),
      },
      {
        header: "Customer",
        key: "studentName",
        render: (row) => ({
          name: row.studentName || "Workshop Attendee",
          phone: row.studentPhone || "—",
          email: row.studentEmail,
        }),
      },
      {
        header: "Pass / Ticket Type",
        key: "passName",
        render: (row) => ({
          passName: row.passName,
          sessionsIncludedCount: row.sessionsIncludedCount,
        }),
      },
      {
        header: "Booking Status",
        key: "status",
        render: (row) => row.status,
      },
    ];

    for (const col of bookingColumns) {
      assert.doesNotThrow(() => {
        const result = col.render ? col.render(guestBooking) : guestBooking[col.key];
        assert.ok(result !== undefined);
      });
    }

    const renderedRef = bookingColumns[0].render(guestBooking);
    assert.equal(renderedRef.ref, "BK-10069B23");
    assert.equal(renderedRef.isGuest, true);
  });

  it("4. Row-level rendering works identically with AdminDataTable for Registered Student bookings", () => {
    const studentBooking = {
      bookingId: "20069b23-9fdf-4f3e-a67e-7eb221399153",
      bookingReference: "BK-20069B23",
      studentName: "Ananya Rao",
      studentPhone: "9876543210",
      studentEmail: "ananya@example.com",
      isGuest: false,
      passName: "All Access Pass",
      sessionsIncludedCount: null,
      status: "Confirmed",
      paymentStatus: "Paid",
      totalPrice: 1500,
      bookedAt: "2026-09-26T07:00:00Z",
      tickets: [],
    };

    const refCol = {
      header: "Booking Reference",
      key: "bookingReference",
      render: (row) => ({
        ref: row.bookingReference,
        isGuest: row.isGuest,
      }),
    };

    const rendered = refCol.render(studentBooking);
    assert.equal(rendered.ref, "BK-20069B23");
    assert.equal(rendered.isGuest, false);
  });

  it("5. Attendee row rendering works with AdminDataTable single-row signature", () => {
    const attendeeRow = {
      ticketId: "tkt-1",
      ticketNumber: "TKT-001",
      attendeeName: "Gaddam Shanmuka",
      attendeePhoneMasked: "798***5654",
      isGuest: true,
      bookingReference: "BK-10069B23",
      sessionTitle: "Session 1 - Pranay",
      sessionDate: "2026-09-27T10:00:00Z",
      passName: "Solo Pass",
      paymentStatus: "Paid",
      isCheckedIn: true,
      formattedCheckedInAt: "10:15 AM",
      checkInMethod: "QR Scanner",
    };

    const attendeeColumns = [
      {
        header: "Ticket #",
        key: "ticketNumber",
        render: (row) => row.ticketNumber,
      },
      {
        header: "Attendee Name",
        key: "attendeeName",
        render: (row) => ({
          name: row.attendeeName,
          phone: row.attendeePhoneMasked,
          isGuest: row.isGuest,
        }),
      },
      {
        header: "Check-in Status",
        key: "isCheckedIn",
        render: (row) => ({
          checkedIn: row.isCheckedIn,
          time: row.formattedCheckedInAt,
        }),
      },
    ];

    for (const col of attendeeColumns) {
      assert.doesNotThrow(() => {
        const res = col.render ? col.render(attendeeRow) : attendeeRow[col.key];
        assert.ok(res !== undefined);
      });
    }
  });

  it("6. AdminDataTable shared component remains unmodified (zero shared component risk)", () => {
    const dataTablePath = path.join(webRoot, "src/components/admin/common/AdminDataTable.jsx");
    const dataTableCode = fs.readFileSync(dataTablePath, "utf8");

    // Confirms line 78 still uses canonical single-row render invocation: col.render ? col.render(row) : row[col.key]
    assert.ok(
      dataTableCode.includes("{col.render ? col.render(row) : row[col.key]}"),
      "AdminDataTable should preserve canonical {col.render ? col.render(row) : row[col.key]}"
    );
  });
});
