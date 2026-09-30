import React, { useState, useEffect, useCallback, useMemo } from "react";
import { useOutletContext, useNavigate, useSearchParams } from "react-router-dom";
import { adminApi } from "../../../services/adminApi";
import AdminDataTable from "../../../components/admin/common/AdminDataTable";
import AdminBadge from "../../../components/admin/common/AdminBadge";
import AdminExportButton from "../../../components/admin/common/AdminExportButton";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopBookingsAttendees() {
  const { workshop, reloadWorkshop } = useOutletContext();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const workshopId = workshop?.Id || workshop?.id;

  // View state: 'bookings' (default) | 'attendees'
  const currentView = searchParams.get("view") === "attendees" ? "attendees" : "bookings";
  const setView = (view) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      if (view === "attendees") {
        next.set("view", "attendees");
      } else {
        next.delete("view");
      }
      return next;
    });
  };

  // Data states
  const [bookings, setBookings] = useState([]);
  const [attendees, setAttendees] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [actionSuccess, setActionSuccess] = useState(null);

  // Search and filter states
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [sessionFilter, setSessionFilter] = useState("all");

  // Expanded booking IDs for ticket details drawer
  const [expandedBookingIds, setExpandedBookingIds] = useState(new Set());

  // Customer Edit Drawer State
  const [editDrawerOpen, setEditDrawerOpen] = useState(false);
  const [editBooking, setEditBooking] = useState(null);
  const [editForm, setEditForm] = useState({ fullName: "", phone: "", email: "" });
  const [savingEdit, setSavingEdit] = useState(false);
  const [sendingWa, setSendingWa] = useState(false);
  const [editError, setEditError] = useState(null);
  const [editSuccess, setEditSuccess] = useState(null);

  // Session Swap State (inside Edit Drawer)
  const [workshopSessions, setWorkshopSessions] = useState([]);
  const [swapTicketId, setSwapTicketId] = useState("");
  const [replacementSessionId, setReplacementSessionId] = useState("");
  const [overrideCutoff, setOverrideCutoff] = useState(false);
  const [overrideReason, setOverrideReason] = useState("");
  const [swappingSession, setSwappingSession] = useState(false);
  const [swapError, setSwapError] = useState(null);

  // Single Refund Modal State
  const [refundModalOpen, setRefundModalOpen] = useState(false);
  const [refundBooking, setRefundBooking] = useState(null);
  const [refundReason, setRefundReason] = useState("");
  const [processingRefund, setProcessingRefund] = useState(false);
  const [refundError, setRefundError] = useState(null);

  // Check In All Confirmation Modal State (Safeguard 1)
  const [checkInAllModal, setCheckInAllModal] = useState({
    open: false,
    booking: null,
    processing: false,
  });

  // Load bookings and attendees together
  const loadData = useCallback(async () => {
    if (!workshopId) return;
    setLoading(true);
    setError(null);
    try {
      const [bookingsRes, attendeesRes] = await Promise.allSettled([
        adminApi.getWorkshopRegistrations(workshopId, 1, 100),
        adminApi.getWorkshopAttendees(workshopId),
      ]);

      if (bookingsRes.status === "fulfilled") {
        setBookings(bookingsRes.value?.items || []);
      } else {
        console.warn("Could not load workshop bookings:", bookingsRes.reason);
      }

      if (attendeesRes.status === "fulfilled") {
        setAttendees(Array.isArray(attendeesRes.value) ? attendeesRes.value : []);
      } else {
        console.warn("Could not load workshop attendees:", attendeesRes.reason);
      }
    } catch (err) {
      setError(err?.message || "Failed to load bookings and attendees.");
    } finally {
      setLoading(false);
    }
  }, [workshopId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Load workshop sessions for swap modal
  useEffect(() => {
    async function loadSessions() {
      try {
        const ws = await adminApi.getWorkshopById(workshopId);
        if (ws?.sessions) {
          setWorkshopSessions(ws.sessions);
        }
      } catch (err) {
        console.warn("Could not load workshop sessions:", err);
      }
    }
    if (workshopId) {
      loadSessions();
    }
  }, [workshopId]);

  // Toggle booking row expansion
  const toggleBookingExpand = (bookingId) => {
    setExpandedBookingIds((prev) => {
      const next = new Set(prev);
      if (next.has(bookingId)) {
        next.delete(bookingId);
      } else {
        next.add(bookingId);
      }
      return next;
    });
  };

  // Handle single ticket check-in (Safeguard 1: check in selected/primary ticket only)
  const handleCheckInSingleTicket = async (ticketId, attendeeName) => {
    if (!ticketId) {
      alert("No valid ticket ID found for check-in.");
      return;
    }

    try {
      await adminApi.checkInWorkshopTicket(workshopId, {
        ticketId,
        method: "AdminOverride",
        notes: `Checked in at desk for ${attendeeName || "attendee"}`,
      });
      setActionSuccess(`Checked in ${attendeeName || "ticket"} successfully!`);
      await loadData();
      if (reloadWorkshop) reloadWorkshop();
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      alert(err?.message || "Check-in failed. Ticket may already be checked in or cancelled.");
    }
  };

  // Primary Check In action on booking row (Safeguard 1: checks in selected/primary ticket only)
  const handleBookingPrimaryCheckIn = async (row) => {
    const primaryTicket =
      row.tickets?.find((t) => t.isPrimaryAttendee) ||
      row.tickets?.[0] ||
      (row.ticketId ? { id: row.ticketId, attendeeName: row.studentName } : null);

    if (!primaryTicket?.id) {
      alert("No active ticket found for this booking.");
      return;
    }

    if (primaryTicket.checkedInAt) {
      alert(`Primary ticket for ${row.studentName} is already checked in.`);
      return;
    }

    await handleCheckInSingleTicket(primaryTicket.id, primaryTicket.attendeeName || row.studentName);
  };

  // Check In All explicit action (Safeguard 1: separate explicit action with confirmation)
  const handleCheckInAllTickets = async () => {
    const b = checkInAllModal.booking;
    if (!b || !b.tickets || b.tickets.length === 0) return;

    setCheckInAllModal((prev) => ({ ...prev, processing: true }));
    let succeeded = 0;
    let failed = 0;

    for (const t of b.tickets) {
      if (!t.checkedInAt && t.status !== "Cancelled") {
        try {
          await adminApi.checkInWorkshopTicket(workshopId, {
            ticketId: t.id,
            method: "AdminOverride",
            notes: `Bulk check-in for booking ${b.bookingReference}`,
          });
          succeeded++;
        } catch {
          failed++;
        }
      }
    }

    setCheckInAllModal({ open: false, booking: null, processing: false });
    setActionSuccess(`Check In All complete: ${succeeded} tickets checked in.${failed > 0 ? ` (${failed} failed)` : ""}`);
    await loadData();
    if (reloadWorkshop) reloadWorkshop();
    setTimeout(() => setActionSuccess(null), 5000);
  };

  // Undo ticket check-in
  const handleUndoTicketCheckIn = async (ticketId, attendeeName) => {
    const reason = window.prompt(`Enter mandatory reason to reverse check-in for ${attendeeName || "attendee"}:`, "Scanned by mistake / Left venue");
    if (!reason || !reason.trim()) return;

    try {
      await adminApi.undoWorkshopTicketCheckIn(workshopId, ticketId, reason.trim());
      setActionSuccess(`Check-in reversed for ${attendeeName || "attendee"}.`);
      await loadData();
      if (reloadWorkshop) reloadWorkshop();
      setTimeout(() => setActionSuccess(null), 4000);
    } catch (err) {
      alert(err?.message || "Failed to reverse check-in.");
    }
  };

  // Open Edit Drawer
  const handleOpenEditDrawer = (row) => {
    setEditBooking(row);
    setEditForm({
      fullName: row.studentName || row.guestName || "",
      phone: row.studentPhone || row.guestPhone || "",
      email: row.studentEmail || row.guestEmail || "",
    });
    setSwapTicketId(row.tickets?.[0]?.id || "");
    setReplacementSessionId("");
    setOverrideCutoff(false);
    setOverrideReason("");
    setEditError(null);
    setEditSuccess(null);
    setSwapError(null);
    setEditDrawerOpen(true);
  };

  // Save Contact Updates
  const handleSaveContact = async () => {
    if (!editForm.phone.trim()) {
      setEditError("Phone number is required.");
      return;
    }

    setSavingEdit(true);
    setEditError(null);
    setEditSuccess(null);

    const bookingId = editBooking.bookingId || editBooking.id;
    try {
      await adminApi.updateCustomerDetails(bookingId, {
        fullName: editForm.fullName.trim(),
        phone: editForm.phone.trim(),
        email: editForm.email.trim() || null,
      });

      setEditSuccess("Customer contact details updated successfully.");
      await loadData();
      setTimeout(() => {
        setActionSuccess(`Updated contact for ${editForm.fullName || "Customer"}.`);
        setTimeout(() => setActionSuccess(null), 5000);
      }, 800);
    } catch (err) {
      setEditError(err?.message || "Failed to update contact details.");
    } finally {
      setSavingEdit(false);
    }
  };

  // Send WhatsApp Notification via Outbox
  const handleSendWhatsApp = async (templateType = "pdf") => {
    const bookingId = editBooking.bookingId || editBooking.id;
    setSendingWa(templateType);
    setEditError(null);
    setEditSuccess(null);

    try {
      const res = await adminApi.sendWhatsAppNotification(bookingId, editForm.phone.trim() || null, templateType);
      const msg = res?.message || "WhatsApp message queued for dispatch via outbox.";
      setEditSuccess(msg);
      setActionSuccess(msg);
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      setEditError(err?.message || "Failed to queue WhatsApp notification.");
    } finally {
      setSendingWa(null);
    }
  };

  // Session Swap Execution (Safeguard 2: backend authoritative capacity, cutoff, and audit)
  const handleSwapSession = async () => {
    if (!replacementSessionId) {
      setSwapError("Please select a target replacement session.");
      return;
    }
    if (overrideCutoff && (!overrideReason.trim() || overrideReason.trim().length < 5)) {
      setSwapError("A mandatory administrative reason of at least 5 characters is required when overriding the session cutoff.");
      return;
    }

    const bookingId = editBooking.bookingId || editBooking.id;
    setSwappingSession(true);
    setSwapError(null);

    try {
      await adminApi.modifyWorkshopBookingSession(bookingId, {
        currentTicketId: swapTicketId || null,
        replacementSessionId,
        overrideCutoff,
        overrideReason: overrideCutoff ? overrideReason.trim() : null,
      });

      setEditSuccess("Session successfully swapped and ticket reissued.");
      setActionSuccess("Session assignment modified and audited successfully.");
      await loadData();
      if (reloadWorkshop) reloadWorkshop();
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      setSwapError(err?.message || "Failed to swap session. Check capacity or cutoff rules.");
    } finally {
      setSwappingSession(false);
    }
  };

  // Open Refund Modal
  const handleOpenRefundModal = (row) => {
    setRefundBooking(row);
    setRefundReason("Customer requested cancellation and refund.");
    setRefundError(null);
    setRefundModalOpen(true);
  };

  // Confirm Refund (Safeguard 3: uses existing PaymentRefund API)
  const handleConfirmRefund = async () => {
    if (!refundReason.trim()) {
      setRefundError("Refund reason is required.");
      return;
    }

    const bookingId = refundBooking.bookingId || refundBooking.id;
    setProcessingRefund(true);
    setRefundError(null);

    try {
      const res = await adminApi.refundWorkshopBooking(bookingId, refundReason.trim());
      setRefundModalOpen(false);
      setActionSuccess(
        `Refund of ₹${refundBooking.totalPrice || refundBooking.amount || "amount"} processed successfully! (Ref: ${res?.razorpayRefundId || "Refunded"})`
      );
      await loadData();
      if (reloadWorkshop) reloadWorkshop();
      setTimeout(() => setActionSuccess(null), 7000);
    } catch (err) {
      setRefundError(err?.message || "Failed to process refund. Check gateway status.");
    } finally {
      setProcessingRefund(false);
    }
  };

  // Filtered Bookings
  const filteredBookings = useMemo(() => {
    return bookings.filter((b) => {
      const q = searchTerm.trim().toLowerCase();
      const matchSearch =
        !q ||
        (b.bookingReference && b.bookingReference.toLowerCase().includes(q)) ||
        (b.studentName && b.studentName.toLowerCase().includes(q)) ||
        (b.studentPhone && b.studentPhone.toLowerCase().includes(q)) ||
        (b.studentEmail && b.studentEmail.toLowerCase().includes(q)) ||
        (b.tickets && b.tickets.some((t) => t.attendeeName?.toLowerCase().includes(q) || t.ticketNumber?.toLowerCase().includes(q)));

      const matchStatus =
        statusFilter === "all" ||
        (statusFilter === "confirmed" && b.status === "Confirmed") ||
        (statusFilter === "attended" && b.status === "Attended") ||
        (statusFilter === "cancelled" && b.status === "Cancelled") ||
        (statusFilter === "pending" && b.paymentStatus === "Pending");

      return matchSearch && matchStatus;
    });
  }, [bookings, searchTerm, statusFilter]);

  // Filtered Attendees
  const filteredAttendees = useMemo(() => {
    return attendees.filter((a) => {
      const q = searchTerm.trim().toLowerCase();
      const matchSearch =
        !q ||
        (a.attendeeName && a.attendeeName.toLowerCase().includes(q)) ||
        (a.ticketNumber && a.ticketNumber.toLowerCase().includes(q)) ||
        (a.bookingReference && a.bookingReference.toLowerCase().includes(q)) ||
        (a.sessionTitle && a.sessionTitle.toLowerCase().includes(q)) ||
        (a.passName && a.passName.toLowerCase().includes(q));

      const matchStatus =
        statusFilter === "all" ||
        (statusFilter === "checked_in" && a.isCheckedIn) ||
        (statusFilter === "not_checked_in" && !a.isCheckedIn) ||
        (statusFilter === "guests" && a.isGuest) ||
        (statusFilter === "students" && !a.isGuest);

      const matchSession =
        sessionFilter === "all" ||
        a.workshopSessionId === sessionFilter;

      return matchSearch && matchStatus && matchSession;
    });
  }, [attendees, searchTerm, statusFilter, sessionFilter]);

  // Status badge variant helper
  const getStatusVariant = (status) => {
    switch ((status || "").toLowerCase()) {
      case "confirmed":
      case "attended":
      case "paid":
        return "success";
      case "pending":
      case "pending payment":
        return "warning";
      case "cancelled":
      case "failed":
        return "danger";
      default:
        return "neutral";
    }
  };

  // Booking table column definitions (9 required columns)
  const bookingColumns = [
    {
      header: "Booking Reference",
      key: "bookingReference",
      render: (row) => (
        <div style={{ display: "flex", alignItems: "center", gap: "6px", whiteSpace: "nowrap" }}>
          <span className="font-mono font-bold" style={{ color: "#ea580c" }}>
            {row.bookingReference || `BK-${(row.bookingId || row.id || "").toString().slice(0, 8).toUpperCase()}`}
          </span>
          {row.isGuest ? (
            <span style={{ fontSize: "11px", padding: "1px 6px", borderRadius: "4px", background: "#f1f5f9", color: "#475569", border: "1px solid #cbd5e1", fontWeight: "600" }}>
              Guest
            </span>
          ) : null}
        </div>
      ),
    },
    {
      header: "Customer",
      key: "studentName",
      render: (row) => (
        <div>
          <div className="font-bold" style={{ color: "#0f172a", fontSize: "13px" }}>{row.studentName || "Workshop Attendee"}</div>
          <div style={{ fontSize: "12px", color: "#334155", fontWeight: "500" }}>{row.studentPhone || "—"}</div>
          {row.studentEmail ? <div style={{ fontSize: "11px", color: "#475569" }}>{row.studentEmail}</div> : null}
        </div>
      ),
    },
    {
      header: "Pass / Ticket Type",
      key: "passName",
      render: (row) => (
        <div>
          <span className="font-semibold" style={{ color: "#0f172a", fontSize: "13px" }}>{row.passName || "Standard Admission"}</span>
          {row.sessionsIncludedCount ? (
            <div style={{ fontSize: "11px", color: "#475569", fontWeight: "500" }}>{row.sessionsIncludedCount} Sessions Pass</div>
          ) : null}
        </div>
      ),
    },
    {
      header: "Attendees",
      key: "attendees",
      render: (row) => {
        const count = row.tickets?.length || 1;
        const isExpanded = expandedBookingIds.has(row.bookingId || row.id);
        return (
          <button
            type="button"
            onClick={() => toggleBookingExpand(row.bookingId || row.id)}
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: "6px",
              padding: "4px 8px",
              borderRadius: "6px",
              background: isExpanded ? "rgba(234, 88, 12, 0.12)" : "#f8fafc",
              border: `1px solid ${isExpanded ? "rgba(234, 88, 12, 0.4)" : "#cbd5e1"}`,
              color: isExpanded ? "#ea580c" : "#0f172a",
              fontSize: "12px",
              fontWeight: "600",
              cursor: "pointer",
              whiteSpace: "nowrap"
            }}
          >
            <span>👥 {count} {count === 1 ? "attendee" : "attendees"}</span>
            <span style={{ fontSize: "10px" }}>{isExpanded ? "▲" : "▼"}</span>
          </button>
        );
      },
    },
    {
      header: "Payment",
      key: "paymentStatus",
      render: (row) => (
        <div>
          <AdminBadge variant={getStatusVariant(row.paymentStatus)}>{row.paymentStatus || "Paid"}</AdminBadge>
          <div style={{ fontSize: "12px", fontWeight: "700", marginTop: "2px", color: "#0f172a" }}>
            ₹{Number(row.totalPrice || row.amount || workshop?.Price || workshop?.price || 0).toLocaleString("en-IN")}
          </div>
        </div>
      ),
    },
    {
      header: "Booking Status",
      key: "status",
      render: (row) => <AdminBadge variant={getStatusVariant(row.status)}>{row.status}</AdminBadge>,
    },
    {
      header: "Booked Date",
      key: "bookedAt",
      render: (row) => (
        <span style={{ fontSize: "12px", color: "#0f172a", fontWeight: "500", whiteSpace: "nowrap" }}>
          {row.bookedAt ? new Date(row.bookedAt).toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }) : "—"}
        </span>
      ),
    },
    {
      header: "Check-in",
      key: "checkInSummary",
      render: (row) => {
        const tickets = row.tickets || [];
        if (tickets.length === 0) {
          return <AdminBadge variant={row.attendanceStatus === "Present" ? "success" : "neutral"}>{row.attendanceStatus || "Pending"}</AdminBadge>;
        }
        const checkedInCount = tickets.filter((t) => t.checkedInAt).length;
        const allCheckedIn = checkedInCount === tickets.length && tickets.length > 0;
        return (
          <div style={{ whiteSpace: "nowrap" }}>
            <AdminBadge variant={allCheckedIn ? "success" : checkedInCount > 0 ? "warning" : "neutral"}>
              {checkedInCount} / {tickets.length} Checked In
            </AdminBadge>
          </div>
        );
      },
    },
    {
      header: "Actions",
      key: "actions",
      render: (row) => {
        const isCancelled = row.status === "Cancelled";
        const tickets = row.tickets || [];
        const hasUncheckedIn = tickets.length > 0 ? tickets.some((t) => !t.checkedInAt) : row.attendanceStatus !== "Present";

        return (
          <div style={{ display: "inline-flex", gap: "6px", alignItems: "center", flexWrap: "nowrap", whiteSpace: "nowrap" }}>
            {/* Primary Check In action (Safeguard 1: selected/primary ticket only) */}
            <button
              type="button"
              className="admin-btn-action"
              title="Check in primary attendee"
              disabled={isCancelled || !hasUncheckedIn}
              onClick={() => handleBookingPrimaryCheckIn(row)}
              style={{ padding: "5px 9px", fontSize: "12px", background: "rgba(16, 185, 129, 0.15)", color: "#10b981", border: "1px solid rgba(16, 185, 129, 0.3)", borderRadius: "4px", fontWeight: "600", whiteSpace: "nowrap", cursor: isCancelled || !hasUncheckedIn ? "not-allowed" : "pointer" }}
            >
              ✓ Check In
            </button>

            {/* Check In All explicit action for multi-attendee bookings (Safeguard 1) */}
            {tickets.length > 1 && hasUncheckedIn && !isCancelled ? (
              <button
                type="button"
                className="admin-btn-action"
                title="Explicitly check in all attendees with confirmation"
                onClick={() => setCheckInAllModal({ open: true, booking: row, processing: false })}
                style={{ padding: "5px 7px", fontSize: "11px", background: "rgba(59, 130, 246, 0.15)", color: "#2563eb", border: "1px solid rgba(59, 130, 246, 0.3)", borderRadius: "4px", fontWeight: "700", whiteSpace: "nowrap", cursor: "pointer" }}
              >
                All
              </button>
            ) : null}

            {/* Edit Drawer action */}
            <button
              type="button"
              className="admin-btn-action"
              title="Edit customer details & session"
              onClick={() => handleOpenEditDrawer(row)}
              style={{ padding: "5px 9px", fontSize: "12px", background: "#f1f5f9", color: "#0f172a", border: "1px solid #cbd5e1", borderRadius: "4px", fontWeight: "600", whiteSpace: "nowrap", cursor: "pointer" }}
            >
              ✏️ Edit
            </button>

            {/* Refund action */}
            <button
              type="button"
              className="admin-btn-action"
              title="Process booking refund via payment gateway"
              disabled={isCancelled || row.paymentStatus !== "Paid"}
              onClick={() => handleOpenRefundModal(row)}
              style={{ padding: "5px 9px", fontSize: "12px", background: "rgba(239, 68, 68, 0.12)", color: "#dc2626", border: "1px solid rgba(239, 68, 68, 0.3)", borderRadius: "4px", fontWeight: "600", whiteSpace: "nowrap", cursor: isCancelled || row.paymentStatus !== "Paid" ? "not-allowed" : "pointer" }}
            >
              ↩️ Refund
            </button>
          </div>
        );
      },
    },
  ];

  // Attendee table column definitions (for [Attendees] view)
  const attendeeColumns = [
    {
      header: "Ticket #",
      key: "ticketNumber",
      render: (row) => <span className="font-mono font-bold" style={{ color: "#ea580c" }}>{row.ticketNumber}</span>,
    },
    {
      header: "Attendee Name",
      key: "attendeeName",
      render: (row) => (
        <div>
          <div className="font-bold" style={{ color: "#0f172a", fontSize: "13px" }}>{row.attendeeName}</div>
          <div style={{ fontSize: "11px", color: "#334155", fontWeight: "500" }}>
            {row.attendeePhoneMasked || "—"} {row.isGuest ? "(Guest)" : ""}
          </div>
        </div>
      ),
    },
    {
      header: "Booking Ref",
      key: "bookingReference",
      render: (row) => <span className="font-mono font-semibold" style={{ color: "#0f172a" }}>{row.bookingReference}</span>,
    },
    {
      header: "Session / Pass",
      key: "sessionTitle",
      render: (row) => (
        <div>
          <div style={{ fontWeight: 600, color: "#0f172a", fontSize: "13px" }}>
            {row.sessionTitle || "Workshop Session"}
          </div>
          <div style={{ fontSize: "11px", color: "#475569" }}>
            {row.passName ? (
              <span style={{ color: "#ea580c", fontWeight: "600", marginRight: "6px" }}>{row.passName}</span>
            ) : null}
            {row.sessionDate ? `• ${new Date(row.sessionDate).toLocaleDateString("en-IN", { day: "numeric", month: "short" })}` : ""}
          </div>
        </div>
      ),
    },
    {
      header: "Payment",
      key: "paymentStatus",
      render: (row) => <AdminBadge variant={getStatusVariant(row.paymentStatus)}>{row.paymentStatus}</AdminBadge>,
    },
    {
      header: "Check-in Status",
      key: "isCheckedIn",
      render: (row) => (
        <div>
          <AdminBadge variant={row.isCheckedIn ? "success" : "neutral"}>
            {row.isCheckedIn ? "Checked In" : "Pending"}
          </AdminBadge>
          {row.isCheckedIn && row.formattedCheckedInAt ? (
            <div style={{ fontSize: "11px", color: "#475569", marginTop: "2px" }}>
              {row.formattedCheckedInAt} ({row.checkInMethod || "Scan"})
            </div>
          ) : null}
        </div>
      ),
    },
    {
      header: "Actions",
      key: "actions",
      render: (row) => (
        <div style={{ display: "inline-flex", gap: "6px", alignItems: "center", flexWrap: "nowrap", whiteSpace: "nowrap" }}>
          {!row.isCheckedIn ? (
            <button
              type="button"
              onClick={() => handleCheckInSingleTicket(row.ticketId, row.attendeeName)}
              style={{ padding: "5px 9px", fontSize: "12px", background: "rgba(16, 185, 129, 0.15)", color: "#10b981", border: "1px solid rgba(16, 185, 129, 0.3)", borderRadius: "4px", fontWeight: "600", whiteSpace: "nowrap", cursor: "pointer" }}
            >
              ✓ Check In
            </button>
          ) : (
            <button
              type="button"
              onClick={() => handleUndoTicketCheckIn(row.ticketId, row.attendeeName)}
              style={{ padding: "5px 9px", fontSize: "12px", background: "rgba(239, 68, 68, 0.12)", color: "#dc2626", border: "1px solid rgba(239, 68, 68, 0.3)", borderRadius: "4px", fontWeight: "600", whiteSpace: "nowrap", cursor: "pointer" }}
            >
              Undo Check In
            </button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="workshop-subpage-container">
      {/* Toast Alert */}
      {actionSuccess ? (
        <div style={{ padding: "12px 16px", borderRadius: "8px", background: "rgba(16, 185, 129, 0.2)", border: "1px solid #10b981", color: "#10b981", marginBottom: "16px", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <span>✓ {actionSuccess}</span>
          <button type="button" onClick={() => setActionSuccess(null)} style={{ background: "none", border: "none", color: "#10b981", cursor: "pointer" }}>✕</button>
        </div>
      ) : null}

      {/* Subpage Header */}
      <div className="subpage-header" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "16px" }}>
        <div>
          <h1 className="subpage-title">Bookings & Attendees</h1>
          <p className="subpage-subtitle">Unified bookings ledger, ticket allocations, and real-time attendance management.</p>
        </div>

        {/* Header Actions */}
        <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
          <button
            type="button"
            className="admin-btn-primary"
            onClick={() => navigate(`/admin_portal/workshops/${workshopId}/scanner`)}
            style={{ display: "inline-flex", alignItems: "center", gap: "6px", background: "linear-gradient(135deg, #f97316 0%, #ea580c 100%)", color: "#fff", border: "none", padding: "8px 14px", borderRadius: "6px", fontWeight: "600", cursor: "pointer" }}
          >
            📷 Launch QR Scanner
          </button>

          <AdminExportButton
            data={currentView === "bookings" ? filteredBookings : filteredAttendees}
            filename={`workshop_${currentView}_${workshopId}`}
          />
        </div>
      </div>

      {/* View Selector & Search Filter Bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "12px", margin: "16px 0" }}>
        {/* Toggle Pills: [Bookings] [Attendees] */}
        <div style={{ display: "inline-flex", background: "#f1f5f9", padding: "4px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
          <button
            type="button"
            onClick={() => setView("bookings")}
            style={{
              padding: "6px 14px",
              borderRadius: "6px",
              border: "none",
              cursor: "pointer",
              fontWeight: "600",
              fontSize: "13px",
              background: currentView === "bookings" ? "#ff5500" : "transparent",
              color: currentView === "bookings" ? "#ffffff" : "#64748b",
              transition: "all 0.15s ease",
            }}
          >
            Bookings ({bookings.length})
          </button>
          <button
            type="button"
            onClick={() => setView("attendees")}
            style={{
              padding: "6px 14px",
              borderRadius: "6px",
              border: "none",
              cursor: "pointer",
              fontWeight: "600",
              fontSize: "13px",
              background: currentView === "attendees" ? "#ff5500" : "transparent",
              color: currentView === "attendees" ? "#ffffff" : "#64748b",
              transition: "all 0.15s ease",
            }}
          >
            Attendees ({attendees.length})
          </button>
        </div>

        {/* Search Input & Status Filter */}
        <div style={{ display: "flex", gap: "10px", alignItems: "center", flexWrap: "wrap" }}>
          <input
            type="text"
            placeholder={currentView === "bookings" ? "Search reference, name, phone..." : "Search ticket #, attendee..."}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            style={{
              padding: "8px 12px",
              background: "#ffffff",
              border: "1px solid #cbd5e1",
              borderRadius: "8px",
              color: "#0f172a",
              fontSize: "13px",
              minWidth: "240px",
              boxShadow: "0 1px 2px rgba(0,0,0,0.04)",
              outline: "none",
            }}
          />

          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            style={{
              padding: "8px 12px",
              background: "#ffffff",
              border: "1px solid #cbd5e1",
              borderRadius: "8px",
              color: "#0f172a",
              fontSize: "13px",
              boxShadow: "0 1px 2px rgba(0,0,0,0.04)",
              cursor: "pointer",
              outline: "none",
            }}
          >
            <option value="all">All Statuses</option>
            {currentView === "bookings" ? (
              <>
                <option value="confirmed">Confirmed</option>
                <option value="attended">Attended</option>
                <option value="pending">Pending Payment</option>
                <option value="cancelled">Cancelled</option>
              </>
            ) : (
              <>
                <option value="checked_in">Checked In</option>
                <option value="not_checked_in">Not Checked In</option>
                <option value="guests">Guests</option>
                <option value="students">Students</option>
              </>
            )}
          </select>

          {currentView === "attendees" && workshopSessions.length > 0 ? (
            <select
              value={sessionFilter}
              onChange={(e) => setSessionFilter(e.target.value)}
              style={{
                padding: "8px 12px",
                background: "#ffffff",
                border: "1px solid #cbd5e1",
                borderRadius: "8px",
                color: "#0f172a",
                fontSize: "13px",
                boxShadow: "0 1px 2px rgba(0,0,0,0.04)",
                cursor: "pointer",
                outline: "none",
              }}
            >
              <option value="all">All Sessions ({workshopSessions.length})</option>
              {workshopSessions.map((ws) => (
                <option key={ws.id} value={ws.id}>
                  {ws.title} ({ws.sessionDate ? new Date(ws.sessionDate).toLocaleDateString("en-IN", { day: "numeric", month: "short" }) : ""})
                </option>
              ))}
            </select>
          ) : null}
        </div>
      </div>

      {/* Main Ledger Table */}
      {currentView === "bookings" ? (
        <div className="table-responsive-container">
          <AdminDataTable
            columns={bookingColumns}
            data={filteredBookings}
            loading={loading}
            emptyMessage={error || "No workshop bookings found matching your search."}
          />

          {/* Render Expanded Tickets Sub-Tables */}
          {filteredBookings.map((b) => {
            const bookingId = b.bookingId || b.id;
            if (!expandedBookingIds.has(bookingId)) return null;

            const tickets = b.tickets || [];
            return (
              <div
                key={`sub-${bookingId}`}
                style={{
                  margin: "8px 0 16px 0",
                  padding: "16px",
                  background: "#f8fafc",
                  borderRadius: "10px",
                  border: "1px solid #e2e8f0",
                  boxShadow: "0 1px 3px rgba(0,0,0,0.03)",
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                  <h4 style={{ margin: 0, color: "#ea580c", fontSize: "14px", fontWeight: "700" }}>
                    🎟️ Ticket Roster for {b.bookingReference} ({tickets.length} {tickets.length === 1 ? "Ticket" : "Tickets"})
                  </h4>
                  <span style={{ fontSize: "12px", color: "#64748b" }}>Customer: {b.studentName} ({b.studentPhone})</span>
                </div>

                {tickets.length === 0 ? (
                  <p style={{ color: "#64748b", fontSize: "13px", margin: 0 }}>No individual ticket records found for this booking.</p>
                ) : (
                  <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px" }}>
                    <thead>
                      <tr style={{ borderBottom: "1px solid #e2e8f0", textAlign: "left", color: "#64748b" }}>
                        <th style={{ padding: "8px" }}>Ticket #</th>
                        <th style={{ padding: "8px" }}>Attendee</th>
                        <th style={{ padding: "8px" }}>Assigned Session</th>
                        <th style={{ padding: "8px" }}>Check-in Status</th>
                        <th style={{ padding: "8px" }}>Check-in Time</th>
                        <th style={{ padding: "8px" }}>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {tickets.map((t) => (
                        <tr key={t.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                          <td style={{ padding: "8px", fontFamily: "monospace", color: "#ea580c", fontWeight: "600" }}>{t.ticketNumber}</td>
                          <td style={{ padding: "8px" }}>
                            <span className="font-bold" style={{ color: "#0f172a" }}>{t.attendeeName}</span>
                            {t.isPrimaryAttendee ? (
                              <span style={{ marginLeft: "6px", fontSize: "10px", padding: "1px 5px", background: "#ffedd5", color: "#ea580c", borderRadius: "3px", fontWeight: "600" }}>
                                Primary
                              </span>
                            ) : null}
                          </td>
                          <td style={{ padding: "8px", color: "#475569" }}>
                            {t.sessionTitle || "Full Workshop Session"}
                            {t.sessionDate ? ` (${new Date(t.sessionDate).toLocaleDateString("en-IN")})` : ""}
                          </td>
                          <td style={{ padding: "8px" }}>
                            <AdminBadge variant={t.checkedInAt ? "success" : "neutral"}>
                              {t.checkedInAt ? "Checked In" : "Pending"}
                            </AdminBadge>
                          </td>
                          <td style={{ padding: "8px", color: "#64748b", fontSize: "12px" }}>
                            {t.checkedInAt ? new Date(t.checkedInAt).toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit" }) : "—"}
                          </td>
                          <td style={{ padding: "8px" }}>
                            {!t.checkedInAt ? (
                              <button
                                type="button"
                                onClick={() => handleCheckInSingleTicket(t.id, t.attendeeName)}
                                style={{ padding: "3px 10px", fontSize: "12px", background: "#ecfdf5", color: "#059669", border: "1px solid #a7f3d0", borderRadius: "4px", cursor: "pointer", fontWeight: "600" }}
                              >
                                ✓ Check In
                              </button>
                            ) : (
                              <button
                                type="button"
                                onClick={() => handleUndoTicketCheckIn(t.id, t.attendeeName)}
                                style={{ padding: "3px 10px", fontSize: "12px", background: "#fef2f2", color: "#dc2626", border: "1px solid #fecaca", borderRadius: "4px", cursor: "pointer", fontWeight: "600" }}
                              >
                                Undo
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            );
          })}
        </div>
      ) : (
        <div className="table-responsive-container">
          <AdminDataTable
            columns={attendeeColumns}
            data={filteredAttendees}
            loading={loading}
            emptyMessage={error || "No attendees found matching your search."}
          />
        </div>
      )}

      {/* Customer Details & Edit Drawer */}
      {editDrawerOpen && editBooking ? (
        <div style={{ position: "fixed", top: 0, right: 0, bottom: 0, width: "100%", maxWidth: "520px", background: "#ffffff", borderLeft: "1px solid #e2e8f0", boxShadow: "-10px 0 30px rgba(0,0,0,0.15)", zIndex: 1000, display: "flex", flexDirection: "column" }}>
          {/* Drawer Header */}
          <div style={{ padding: "20px", borderBottom: "1px solid #e2e8f0", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <div>
              <h3 style={{ margin: 0, fontSize: "18px", color: "#0f172a", fontWeight: "800" }}>Edit Booking & Customer</h3>
              <span className="font-mono" style={{ fontSize: "12px", color: "#ea580c", fontWeight: "600" }}>{editBooking.bookingReference}</span>
            </div>
            <button type="button" onClick={() => setEditDrawerOpen(false)} style={{ background: "none", border: "none", color: "#64748b", fontSize: "20px", cursor: "pointer" }}>✕</button>
          </div>

          {/* Drawer Body */}
          <div style={{ padding: "20px", overflowY: "auto", flex: 1 }}>
            {editError ? <div style={{ padding: "10px", borderRadius: "6px", background: "#fef2f2", border: "1px solid #fecaca", color: "#991b1b", marginBottom: "12px", fontSize: "13px" }}>{editError}</div> : null}
            {editSuccess ? <div style={{ padding: "10px", borderRadius: "6px", background: "#f0fdf4", border: "1px solid #bbf7d0", color: "#15803d", marginBottom: "12px", fontSize: "13px" }}>{editSuccess}</div> : null}

            {/* Contact Section */}
            <h4 style={{ margin: "0 0 12px 0", color: "#0f172a", fontSize: "14px", fontWeight: "700" }}>Customer Contact Details</h4>
            <div style={{ marginBottom: "12px" }}>
              <label style={{ display: "block", fontSize: "12px", color: "#475569", fontWeight: "600", marginBottom: "4px" }}>Full Name</label>
              <input
                type="text"
                value={editForm.fullName}
                onChange={(e) => setEditForm({ ...editForm, fullName: e.target.value })}
                style={{ width: "100%", padding: "8px 12px", background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", color: "#0f172a", outline: "none" }}
              />
            </div>

            <div style={{ marginBottom: "12px" }}>
              <label style={{ display: "block", fontSize: "12px", color: "#475569", fontWeight: "600", marginBottom: "4px" }}>WhatsApp / Phone Number</label>
              <input
                type="text"
                value={editForm.phone}
                onChange={(e) => setEditForm({ ...editForm, phone: e.target.value })}
                style={{ width: "100%", padding: "8px 12px", background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", color: "#0f172a", outline: "none" }}
              />
            </div>

            <div style={{ marginBottom: "16px" }}>
              <label style={{ display: "block", fontSize: "12px", color: "#475569", fontWeight: "600", marginBottom: "4px" }}>Email Address</label>
              <input
                type="email"
                value={editForm.email}
                onChange={(e) => setEditForm({ ...editForm, email: e.target.value })}
                style={{ width: "100%", padding: "8px 12px", background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", color: "#0f172a", outline: "none" }}
              />
            </div>

            <div style={{ marginBottom: "20px" }}>
              <button
                type="button"
                className="admin-btn-primary"
                onClick={handleSaveContact}
                disabled={savingEdit}
                style={{ width: "100%", padding: "10px", background: "#ea580c", color: "#fff", border: "none", borderRadius: "8px", fontWeight: "750", fontSize: "13px", cursor: "pointer" }}
              >
                {savingEdit ? "Saving Contact Changes..." : "💾 Save Contact Info"}
              </button>
            </div>

            {/* WhatsApp Notifications Section */}
            <div style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "14px", marginBottom: "20px" }}>
              <div style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "6px" }}>
                <span style={{ fontSize: "15px" }}>💬</span>
                <h5 style={{ margin: 0, fontSize: "13px", fontWeight: "700", color: "#0f172a" }}>WhatsApp Outbox Messages</h5>
              </div>
              <p style={{ margin: "0 0 10px 0", fontSize: "12px", color: "#475569" }}>
                Send or re-send official WhatsApp messages to <strong>{editForm.phone || "the customer"}</strong>:
              </p>

              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "8px", marginBottom: "8px" }}>
                <button
                  type="button"
                  onClick={() => handleSendWhatsApp("pdf")}
                  disabled={!!sendingWa}
                  style={{
                    padding: "8px 10px",
                    background: "#ffffff",
                    border: "1px solid #cbd5e1",
                    color: "#0f172a",
                    borderRadius: "6px",
                    fontWeight: "600",
                    fontSize: "12px",
                    cursor: sendingWa ? "not-allowed" : "pointer",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    gap: "6px",
                    boxShadow: "0 1px 2px rgba(0,0,0,0.04)"
                  }}
                >
                  <span>📄</span>
                  <span>{sendingWa === "pdf" ? "Queuing..." : "Send Ticket PDF"}</span>
                </button>

                <button
                  type="button"
                  onClick={() => handleSendWhatsApp("confirmed")}
                  disabled={!!sendingWa}
                  style={{
                    padding: "8px 10px",
                    background: "#ffffff",
                    border: "1px solid #cbd5e1",
                    color: "#0f172a",
                    borderRadius: "6px",
                    fontWeight: "600",
                    fontSize: "12px",
                    cursor: sendingWa ? "not-allowed" : "pointer",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    gap: "6px",
                    boxShadow: "0 1px 2px rgba(0,0,0,0.04)"
                  }}
                >
                  <span>✅</span>
                  <span>{sendingWa === "confirmed" ? "Queuing..." : "Send Confirmed"}</span>
                </button>
              </div>

              <button
                type="button"
                onClick={() => handleSendWhatsApp("both")}
                disabled={!!sendingWa}
                style={{
                  width: "100%",
                  padding: "8px 12px",
                  background: "#ecfdf5",
                  border: "1px solid #86efac",
                  color: "#15803d",
                  borderRadius: "6px",
                  fontWeight: "700",
                  fontSize: "12px",
                  cursor: sendingWa ? "not-allowed" : "pointer",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  gap: "6px"
                }}
              >
                <span>📨</span>
                <span>{sendingWa === "both" ? "Queuing Both Messages..." : "Send Both (Confirmed + PDF)"}</span>
              </button>
            </div>

            <hr style={{ borderColor: "#e2e8f0", margin: "20px 0" }} />

            {/* Session Modification Section (Safeguard 2) */}
            <h4 style={{ margin: "0 0 12px 0", color: "#0f172a", fontSize: "14px", fontWeight: "700" }}>Modify Session Assignment</h4>
            <p style={{ fontSize: "12px", color: "#64748b", marginTop: 0 }}>
              Swap an attendee into another available session. Enforces server-side capacity and cutoff policies.
            </p>

            {swapError ? <div style={{ padding: "8px", borderRadius: "6px", background: "#fef2f2", border: "1px solid #fecaca", color: "#991b1b", marginBottom: "12px", fontSize: "12px" }}>{swapError}</div> : null}

            {editBooking.tickets && editBooking.tickets.length > 1 ? (
              <div style={{ marginBottom: "12px" }}>
                <label style={{ display: "block", fontSize: "12px", color: "#475569", fontWeight: "600", marginBottom: "4px" }}>Select Ticket / Attendee</label>
                <select
                  value={swapTicketId}
                  onChange={(e) => setSwapTicketId(e.target.value)}
                  style={{ width: "100%", padding: "8px 12px", background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", color: "#0f172a", outline: "none" }}
                >
                  {editBooking.tickets.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.attendeeName} ({t.ticketNumber})
                    </option>
                  ))}
                </select>
              </div>
            ) : null}

            <div style={{ marginBottom: "12px" }}>
              <label style={{ display: "block", fontSize: "12px", color: "#475569", fontWeight: "600", marginBottom: "4px" }}>Target Replacement Session</label>
              <select
                value={replacementSessionId}
                onChange={(e) => setReplacementSessionId(e.target.value)}
                style={{ width: "100%", padding: "8px 12px", background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", color: "#0f172a", outline: "none" }}
              >
                <option value="">-- Choose replacement session --</option>
                {workshopSessions.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.title} ({s.date || (s.sessionDate ? new Date(s.sessionDate).toLocaleDateString() : "")} · {s.startTime || ""})
                  </option>
                ))}
              </select>
            </div>

            <div style={{ marginBottom: "12px" }}>
              <label style={{ display: "flex", alignItems: "center", gap: "8px", fontSize: "12px", color: "#334155", cursor: "pointer", fontWeight: "500" }}>
                <input
                  type="checkbox"
                  checked={overrideCutoff}
                  onChange={(e) => setOverrideCutoff(e.target.checked)}
                />
                Admin Cutoff Override (Requires reason for audit log)
              </label>
            </div>

            {overrideCutoff ? (
              <div style={{ marginBottom: "12px" }}>
                <label style={{ display: "block", fontSize: "12px", color: "#475569", fontWeight: "600", marginBottom: "4px" }}>Mandatory Override Reason</label>
                <textarea
                  rows={2}
                  value={overrideReason}
                  onChange={(e) => setOverrideReason(e.target.value)}
                  placeholder="Reason for administrative cutoff override..."
                  style={{ width: "100%", padding: "8px 12px", background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", color: "#0f172a", fontSize: "12px", outline: "none" }}
                />
              </div>
            ) : null}

            <button
              type="button"
              onClick={handleSwapSession}
              disabled={swappingSession}
              style={{ width: "100%", padding: "9px", background: "#eff6ff", border: "1px solid #93c5fd", color: "#1d4ed8", borderRadius: "8px", fontWeight: "700", cursor: "pointer" }}
            >
              {swappingSession ? "Executing Session Swap..." : "Execute Session Swap"}
            </button>
          </div>
        </div>
      ) : null}

      {/* Booking-Level Refund Modal (Safeguard 3) */}
      {refundModalOpen && refundBooking ? (
        <div style={{ position: "fixed", top: 0, left: 0, right: 0, bottom: 0, background: "rgba(0,0,0,0.5)", backdropFilter: "blur(2px)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1100, padding: "20px" }}>
          <div style={{ width: "100%", maxWidth: "460px", background: "#ffffff", borderRadius: "14px", border: "1px solid #fecaca", padding: "24px", boxShadow: "0 20px 40px rgba(0,0,0,0.15)" }}>
            <h3 style={{ margin: "0 0 8px 0", color: "#dc2626", fontSize: "18px", fontWeight: "800" }}>↩️ Process Booking Refund</h3>
            <p style={{ color: "#475569", fontSize: "13px", lineHeight: "1.5", margin: "0 0 16px 0" }}>
              You are about to issue a full gateway refund for booking <strong style={{ color: "#0f172a" }}>{refundBooking.bookingReference}</strong> ({refundBooking.studentName}). All associated tickets will be marked Cancelled and access revoked.
            </p>

            {refundError ? <div style={{ padding: "10px", borderRadius: "6px", background: "#fef2f2", border: "1px solid #fecaca", color: "#991b1b", marginBottom: "16px", fontSize: "13px" }}>{refundError}</div> : null}

            <div style={{ marginBottom: "16px" }}>
              <label style={{ display: "block", fontSize: "12px", color: "#475569", fontWeight: "600", marginBottom: "4px" }}>Refund Reason (Audited)</label>
              <textarea
                rows={3}
                value={refundReason}
                onChange={(e) => setRefundReason(e.target.value)}
                placeholder="Reason for cancellation and refund..."
                style={{ width: "100%", padding: "8px 12px", background: "#ffffff", border: "1px solid #cbd5e1", borderRadius: "8px", color: "#0f172a", fontSize: "13px", outline: "none" }}
              />
            </div>

            <div style={{ display: "flex", gap: "10px", justifyContent: "flex-end" }}>
              <button
                type="button"
                onClick={() => setRefundModalOpen(false)}
                disabled={processingRefund}
                style={{ padding: "8px 16px", background: "transparent", border: "1px solid rgba(255, 255, 255, 0.2)", color: "#cbd5e1", borderRadius: "6px", cursor: "pointer" }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleConfirmRefund}
                disabled={processingRefund}
                style={{ padding: "8px 16px", background: "#ef4444", border: "none", color: "#fff", borderRadius: "6px", fontWeight: "600", cursor: "pointer" }}
              >
                {processingRefund ? "Processing Refund..." : "Confirm & Refund Gateway"}
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {/* Explicit Check In All Confirmation Modal (Safeguard 1) */}
      {checkInAllModal.open && checkInAllModal.booking ? (
        <div style={{ position: "fixed", top: 0, left: 0, right: 0, bottom: 0, background: "rgba(0,0,0,0.75)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1100, padding: "20px" }}>
          <div style={{ width: "100%", maxWidth: "440px", background: "#0f172a", borderRadius: "12px", border: "1px solid rgba(59, 130, 246, 0.4)", padding: "24px" }}>
            <h3 style={{ margin: "0 0 8px 0", color: "#60a5fa", fontSize: "18px" }}>👥 Confirm Check In All Attendees</h3>
            <p style={{ color: "#94a3b8", fontSize: "13px", lineHeight: "1.5", margin: "0 0 16px 0" }}>
              Are you sure you want to mark all <strong style={{ color: "#f8fafc" }}>{checkInAllModal.booking.tickets?.length || 0} attendees</strong> in booking <strong style={{ color: "#f97316" }}>{checkInAllModal.booking.bookingReference}</strong> as checked in?
            </p>

            <div style={{ display: "flex", gap: "10px", justifyContent: "flex-end" }}>
              <button
                type="button"
                onClick={() => setCheckInAllModal({ open: false, booking: null, processing: false })}
                disabled={checkInAllModal.processing}
                style={{ padding: "8px 16px", background: "transparent", border: "1px solid rgba(255, 255, 255, 0.2)", color: "#cbd5e1", borderRadius: "6px", cursor: "pointer" }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleCheckInAllTickets}
                disabled={checkInAllModal.processing}
                style={{ padding: "8px 16px", background: "#3b82f6", border: "none", color: "#fff", borderRadius: "6px", fontWeight: "600", cursor: "pointer" }}
              >
                {checkInAllModal.processing ? "Checking In All..." : "Yes, Check In All"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
