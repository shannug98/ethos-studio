import React, { useState, useEffect, useCallback, useMemo } from "react";
import { useOutletContext, useNavigate } from "react-router-dom";
import { adminApi } from "../../../services/adminApi";
import AdminDataTable from "../../../components/admin/common/AdminDataTable";
import AdminBadge from "../../../components/admin/common/AdminBadge";
import AdminExportButton from "../../../components/admin/common/AdminExportButton";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopAttendees() {
  const { workshop, reloadWorkshop } = useOutletContext();
  const navigate = useNavigate();
  const workshopId = workshop.Id || workshop.id;

  const [attendees, setAttendees] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("all");
  const [page, setPage] = useState(1);
  const [actionSuccess, setActionSuccess] = useState(null);

  const loadAttendees = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await adminApi.getWorkshopAttendees(workshopId);
      setAttendees(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err?.message || "Failed to load attendees.");
    } finally {
      setLoading(false);
    }
  }, [workshopId]);

  useEffect(() => {
    loadAttendees();
  }, [loadAttendees]);

  const handleManualCheckIn = async (attendee) => {
    const reason = window.prompt(`Enter mandatory reason for manual check-in of ${attendee.attendeeName}:`, "Manual desk verification");
    if (!reason || !reason.trim()) return;

    try {
      await adminApi.overrideWorkshopTicket(workshopId, attendee.ticketId, reason.trim());
      setActionSuccess(`${attendee.attendeeName} successfully checked in.`);
      await loadAttendees();
      if (reloadWorkshop) reloadWorkshop();
      setTimeout(() => setActionSuccess(null), 4000);
    } catch (err) {
      alert(err?.message || "Failed to check in attendee.");
    }
  };

  const handleUndoCheckIn = async (attendee) => {
    const reason = window.prompt(`Enter reason to reverse check-in for ${attendee.attendeeName}:`, "Scanned by mistake / Left venue");
    if (!reason || !reason.trim()) return;

    try {
      await adminApi.undoWorkshopTicketCheckIn(workshopId, attendee.ticketId, reason.trim());
      setActionSuccess(`Check-in reversed for ${attendee.attendeeName}.`);
      await loadAttendees();
      if (reloadWorkshop) reloadWorkshop();
      setTimeout(() => setActionSuccess(null), 4000);
    } catch (err) {
      alert(err?.message || "Failed to reverse check-in.");
    }
  };

  const handleExportCsv = async () => {
    try {
      const blob = await adminApi.exportWorkshopAttendance(workshopId);
      // handled by browser download
    } catch {
      // ignore
    }
  };

  // Client-side filtering & search
  const filteredAttendees = useMemo(() => {
    return attendees.filter((a) => {
      const matchFilter =
        filter === "all" ||
        (filter === "checked_in" && a.isCheckedIn) ||
        (filter === "not_checked_in" && !a.isCheckedIn) ||
        (filter === "guests" && a.isGuest) ||
        (filter === "students" && !a.isGuest);

      const q = search.trim().toLowerCase();
      const matchSearch =
        !q ||
        a.attendeeName.toLowerCase().includes(q) ||
        a.ticketNumber.toLowerCase().includes(q) ||
        a.bookingReference.toLowerCase().includes(q);

      return matchFilter && matchSearch;
    });
  }, [attendees, filter, search]);

  const checkedInCount = attendees.filter((a) => a.isCheckedIn).length;
  const notCheckedInCount = attendees.length - checkedInCount;

  const handleEditContact = async (row) => {
    const currentPhone = row.attendeePhoneMasked || "";
    const newPhone = window.prompt(
      `Correct WhatsApp Phone Number for ${row.attendeeName}:\n(e.g., +919876543210 or 9876543210)`,
      ""
    );
    if (newPhone === null) return;
    if (!newPhone.trim()) {
      alert("Phone number cannot be empty.");
      return;
    }

    try {
      await adminApi.updateBookingContact(row.bookingId, { phone: newPhone.trim() });
      setActionSuccess(`Contact phone number updated for ${row.attendeeName}.`);
      await loadAttendees();
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      alert(err?.message || "Failed to update contact number.");
    }
  };

  const handleResendWhatsApp = async (row) => {
    if (!window.confirm(`Resend official WhatsApp ticket PDF pass to ${row.attendeeName}?`)) return;

    try {
      const res = await adminApi.resendWhatsAppTicket(row.bookingId);
      setActionSuccess(res?.message || `WhatsApp Ticket PDF queued and resent to ${row.attendeeName}.`);
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      alert(err?.message || "Failed to resend WhatsApp ticket.");
    }
  };

  const tableColumns = [
    {
      header: "Attendee",
      key: "attendeeName",
      render: (row) => (
        <div className="attendee-name-cell">
          <div className="attendee-avatar">{row.attendeeName.charAt(0).toUpperCase()}</div>
          <div>
            <div className="attendee-primary-name">{row.attendeeName}</div>
            <div className="text-xs text-slate-500">{row.attendeePhoneMasked || row.attendeePhone || "No Phone"}</div>
            <div className="attendee-type-badge">{row.attendeeType}</div>
          </div>
        </div>
      ),
    },
    {
      header: "Ticket Number",
      key: "ticketNumber",
      render: (row) => <span className="font-mono text-xs">{row.ticketNumber}</span>,
    },
    {
      header: "Booking Ref",
      key: "bookingReference",
      render: (row) => <span className="font-mono text-xs text-secondary">{row.bookingReference}</span>,
    },
    {
      header: "Payment",
      key: "paymentStatus",
      render: (row) => (
        <AdminBadge tone={row.paymentStatus === "Paid" ? "success" : "warning"}>
          {row.paymentStatus}
        </AdminBadge>
      ),
    },
    {
      header: "Check-in Status",
      key: "isCheckedIn",
      render: (row) => (
        <AdminBadge tone={row.isCheckedIn ? "success" : "neutral"} dot>
          {row.isCheckedIn ? "Checked In" : "Not Checked In"}
        </AdminBadge>
      ),
    },
    {
      header: "Check-in Time",
      key: "formattedCheckedInAt",
      render: (row) => (
        <span className="text-xs text-secondary">
          {row.formattedCheckedInAt ? `${row.formattedCheckedInAt} (${row.checkInMethod || "QR"})` : "—"}
        </span>
      ),
    },
    {
      header: "Actions",
      key: "actions",
      render: (row) => (
        <div className="table-actions-cell flex gap-1.5 flex-wrap">
          {!row.isCheckedIn ? (
            <button
              type="button"
              className="admin-btn-secondary btn-xs"
              onClick={() => handleManualCheckIn(row)}
            >
              ✓ Check In
            </button>
          ) : (
            <button
              type="button"
              className="admin-btn-secondary btn-xs text-danger"
              onClick={() => handleUndoCheckIn(row)}
            >
              Undo
            </button>
          )}

          <button
            type="button"
            className="admin-btn-secondary btn-xs"
            title="Edit contact phone number"
            onClick={() => handleEditContact(row)}
          >
            ✏️ Contact
          </button>

          <button
            type="button"
            className="admin-btn-secondary btn-xs text-emerald-600"
            title="Resend WhatsApp Ticket PDF"
            onClick={() => handleResendWhatsApp(row)}
          >
            💬 WhatsApp
          </button>
        </div>
      ),
    },
  ];

  return (
    <div className="workshop-subpage-container">
      {/* Header */}
      <div className="subpage-header">
        <div>
          <h1 className="subpage-title">Attendees Roster ({attendees.length})</h1>
          <p className="subpage-subtitle">
            Full participant roster, contact information, and manual desk check-in controls.
          </p>
        </div>

        <div className="subpage-header-actions">
          <button
            type="button"
            className="admin-btn-primary"
            onClick={() => navigate(`/admin_portal/workshops/${workshopId}/scanner`)}
          >
            📷 Open QR Scanner
          </button>
          <AdminExportButton data={attendees} filename={`attendees_${workshop.title || "workshop"}`} />
        </div>
      </div>

      {actionSuccess && <div className="subpage-success-banner">{actionSuccess}</div>}
      {error && <div className="subpage-error-banner">{error}</div>}

      {/* Filter Toolbar */}
      <div className="admin-filter-bar">
        <div className="filter-tabs-group">
          <button
            type="button"
            className={`filter-tab-pill ${filter === "all" ? "active" : ""}`}
            onClick={() => setFilter("all")}
          >
            All ({attendees.length})
          </button>
          <button
            type="button"
            className={`filter-tab-pill ${filter === "checked_in" ? "active" : ""}`}
            onClick={() => setFilter("checked_in")}
          >
            Checked In ({checkedInCount})
          </button>
          <button
            type="button"
            className={`filter-tab-pill ${filter === "not_checked_in" ? "active" : ""}`}
            onClick={() => setFilter("not_checked_in")}
          >
            Not Checked In ({notCheckedInCount})
          </button>
        </div>

        <div className="filter-search-group">
          <input
            type="text"
            placeholder="Search attendee, ticket, or booking ref..."
            className="subpage-search-input"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      {/* Data Table */}
      <AdminDataTable
        columns={tableColumns}
        data={filteredAttendees}
        loading={loading}
        page={page}
        pageSize={15}
        totalItems={filteredAttendees.length}
        onPageChange={setPage}
        emptyMessage="No attendees found matching your filter."
        emptyIcon="👥"
      />
    </div>
  );
}
