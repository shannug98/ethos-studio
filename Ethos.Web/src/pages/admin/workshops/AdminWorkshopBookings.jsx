import React, { useState, useEffect, useCallback } from "react";
import { useOutletContext } from "react-router-dom";
import { adminApi } from "../../../services/adminApi";
import AdminDataTable from "../../../components/admin/common/AdminDataTable";
import AdminBadge from "../../../components/admin/common/AdminBadge";
import AdminExportButton from "../../../components/admin/common/AdminExportButton";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopBookings() {
  const { workshop } = useOutletContext();
  const workshopId = workshop.Id || workshop.id;

  const [bookings, setBookings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [page, setPage] = useState(1);
  const [actionSuccess, setActionSuccess] = useState(null);

  // Customer Edit Modal State
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editBooking, setEditBooking] = useState(null);
  const [editForm, setEditForm] = useState({ fullName: "", phone: "", email: "" });
  const [savingEdit, setSavingEdit] = useState(false);
  const [sendingWa, setSendingWa] = useState(false);
  const [editError, setEditError] = useState(null);
  const [editSuccess, setEditSuccess] = useState(null);

  // Single Refund Modal State
  const [refundModalOpen, setRefundModalOpen] = useState(false);
  const [refundBooking, setRefundBooking] = useState(null);
  const [refundReason, setRefundReason] = useState("");
  const [processingRefund, setProcessingRefund] = useState(false);
  const [refundError, setRefundError] = useState(null);

  // Modify Session Modal State
  const [modifyModalOpen, setModifyModalOpen] = useState(false);
  const [selectedBooking, setSelectedBooking] = useState(null);
  const [workshopSessions, setWorkshopSessions] = useState([]);
  const [currentSessionId, setCurrentSessionId] = useState("");
  const [replacementSessionId, setReplacementSessionId] = useState("");
  const [overrideCutoff, setOverrideCutoff] = useState(false);
  const [overrideReason, setOverrideReason] = useState("");
  const [modifying, setModifying] = useState(false);
  const [modifyError, setModifyError] = useState(null);

  const loadBookings = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getWorkshopRegistrations(workshopId, page, 50);
      setBookings(res?.items || []);
    } catch (err) {
      setError(err?.message || "Failed to load bookings ledger.");
    } finally {
      setLoading(false);
    }
  }, [workshopId, page]);

  useEffect(() => {
    loadBookings();
  }, [loadBookings]);

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

  // Handle opening Customer Edit Modal
  const handleOpenEditModal = (row) => {
    setEditBooking(row);
    setEditForm({
      fullName: row.studentName || row.guestName || "",
      phone: row.studentPhone || row.guestPhone || "",
      email: row.studentEmail || row.guestEmail || "",
    });
    setEditError(null);
    setEditSuccess(null);
    setEditModalOpen(true);
  };

  // Save Customer Changes
  const handleSaveCustomer = async () => {
    if (!editForm.phone.trim()) {
      setEditError("Phone number is required.");
      return;
    }

    setSavingEdit(true);
    setEditError(null);
    setEditSuccess(null);

    const bookingId = editBooking.id || editBooking.bookingId;
    try {
      await adminApi.updateCustomerDetails(bookingId, {
        fullName: editForm.fullName.trim(),
        phone: editForm.phone.trim(),
        email: editForm.email.trim() || null,
      });

      setEditSuccess("Customer contact details updated successfully.");
      await loadBookings();
      setTimeout(() => {
        setEditModalOpen(false);
        setActionSuccess(`Updated contact for ${editForm.fullName || "Customer"}.`);
        setTimeout(() => setActionSuccess(null), 5000);
      }, 1200);
    } catch (err) {
      setEditError(err?.message || "Failed to update contact details.");
    } finally {
      setSavingEdit(false);
    }
  };

  // Send WhatsApp Ticket
  const handleSendWhatsApp = async () => {
    const bookingId = editBooking.id || editBooking.bookingId;
    setSendingWa(true);
    setEditError(null);
    setEditSuccess(null);

    try {
      const res = await adminApi.sendWhatsAppNotification(bookingId, editForm.phone.trim() || null);
      setEditSuccess(res?.message || "Ticket pass queued for WhatsApp dispatch via outbox.");
      setActionSuccess(res?.message || "Ticket pass queued for WhatsApp dispatch.");
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      setEditError(err?.message || "Failed to queue WhatsApp notification.");
    } finally {
      setSendingWa(false);
    }
  };

  // Open Refund Modal
  const handleOpenRefundModal = (row) => {
    setRefundBooking(row);
    setRefundReason("Customer requested cancellation and refund.");
    setRefundError(null);
    setRefundModalOpen(true);
  };

  // Process Refund
  const handleConfirmRefund = async () => {
    if (!refundReason.trim()) {
      setRefundError("Refund reason is required.");
      return;
    }

    const bookingId = refundBooking.id || refundBooking.bookingId;
    setProcessingRefund(true);
    setRefundError(null);

    try {
      const res = await adminApi.refundWorkshopBooking(bookingId, refundReason.trim());
      setRefundModalOpen(false);
      setActionSuccess(
        `Refund of ₹${refundBooking.totalPrice || refundBooking.amount} processed successfully! (Ref: ${res?.razorpayRefundId || "Refunded"})`
      );
      await loadBookings();
      setTimeout(() => setActionSuccess(null), 7000);
    } catch (err) {
      setRefundError(err?.message || "Failed to process refund. Check gateway status.");
    } finally {
      setProcessingRefund(false);
    }
  };

  // Quick Check In Action
  const handleQuickCheckIn = async (row) => {
    const bookingId = row.id || row.bookingId;
    const ticketId = row.tickets?.[0]?.id || row.ticketId;

    if (!ticketId) {
      alert("No active ticket ID found for this booking.");
      return;
    }

    if (!window.confirm(`Check in attendee ${row.studentName} (${row.bookingReference})?`)) return;

    try {
      await adminApi.checkInWorkshopTicket(workshopId, {
        ticketId,
        method: "AdminOverride",
        notes: "Manual check-in from Bookings Ledger",
      });
      setActionSuccess(`Checked in ${row.studentName} successfully!`);
      await loadBookings();
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      alert(err?.message || "Check-in failed.");
    }
  };

  // Modify Session handlers
  const handleOpenModifyModal = (row) => {
    setSelectedBooking(row);
    const activeSessions = (row.bookingSessions || []).filter(
      (bs) => bs.status === 1 || bs.status === "Booked"
    );
    setCurrentSessionId(activeSessions[0]?.workshopSessionId || "");
    setReplacementSessionId("");
    setOverrideCutoff(false);
    setOverrideReason("");
    setModifyError(null);
    setModifyModalOpen(true);
  };

  const handleConfirmModifySession = async () => {
    if (!currentSessionId || !replacementSessionId) {
      setModifyError("Please select both a current session to replace and a replacement session.");
      return;
    }

    if (currentSessionId === replacementSessionId) {
      setModifyError("Replacement session cannot be the same as current session.");
      return;
    }

    const repSession = workshopSessions.find((s) => s.id === replacementSessionId);
    if (repSession?.isBookingClosed && !overrideCutoff) {
      setModifyError("Booking cutoff has passed for this session. You must enable cutoff override.");
      return;
    }

    if (overrideCutoff && !overrideReason.trim()) {
      setModifyError("A clear override reason is required when overriding booking cutoff.");
      return;
    }

    setModifying(true);
    setModifyError(null);

    try {
      const selectedBs = (selectedBooking.bookingSessions || []).find(
        (bs) => bs.workshopSessionId === currentSessionId
      );
      const ticketId = selectedBs?.workshopTicketId || selectedBooking.tickets?.find((t) => t.workshopSessionId === currentSessionId)?.id;

      const res = await adminApi.modifyWorkshopBookingSession(selectedBooking.id || selectedBooking.bookingId, {
        currentTicketId: ticketId || undefined,
        currentSessionId,
        replacementSessionId,
        overrideCutoff,
        overrideReason: overrideCutoff ? overrideReason.trim() : null,
      });

      setModifyModalOpen(false);
      setActionSuccess(
        `Session swapped successfully! Replaced ticket ${res.replacedTicketNumber} with new ticket ${res.newTicketNumber}. Fresh pass sent via WhatsApp.`
      );
      await loadBookings();
      setTimeout(() => setActionSuccess(null), 7000);
    } catch (err) {
      setModifyError(err?.message || "Failed to swap session.");
    } finally {
      setModifying(false);
    }
  };

  const columns = [
    {
      header: "Booking Ref",
      key: "bookingReference",
      render: (row) => <span className="font-mono text-xs font-bold">{row.bookingReference}</span>,
    },
    {
      header: "Student / Guest",
      key: "studentName",
      render: (row) => (
        <div>
          <div className="font-semibold text-slate-800">{row.studentName}</div>
          <div className="text-xs text-slate-500">{row.studentPhone || "No Phone"}</div>
        </div>
      ),
    },
    {
      header: "Pass & Sessions",
      key: "passName",
      render: (row) => {
        const bSessions = row.bookingSessions || [];
        const isOverall = !row.sessionsIncludedCount && row.passName;

        return (
          <div style={{ maxWidth: "260px" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "4px" }}>
              <span style={{ fontSize: "12px", fontWeight: 700, color: "#1e293b" }}>
                {row.passName || "Standard Entry"}
              </span>
              {isOverall && (
                <span style={{ fontSize: "10px", background: "#fef3c7", color: "#b45309", padding: "1px 5px", borderRadius: "3px", fontWeight: 700 }}>
                  Overall Pass
                </span>
              )}
            </div>

            {bSessions.length > 0 ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "3px" }}>
                {bSessions.map((bs, idx) => {
                  const isReplaced = bs.status === 2 || bs.status === "Replaced";
                  return (
                    <div
                      key={idx}
                      style={{
                        fontSize: "11px",
                        padding: "2px 6px",
                        borderRadius: "4px",
                        background: isReplaced ? "rgba(239, 68, 68, 0.08)" : "rgba(241, 245, 249, 0.9)",
                        color: isReplaced ? "#991b1b" : "#334155",
                        textDecoration: isReplaced ? "line-through" : "none",
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                      }}
                    >
                      <span>{bs.sessionTitle || `Session ${idx + 1}`}</span>
                      {isReplaced && <span style={{ fontSize: "9px", fontWeight: 700, textDecoration: "none" }}>REPLACED</span>}
                    </div>
                  );
                })}
              </div>
            ) : (
              <span className="text-xs text-slate-500">{row.quantity || 1} seat(s)</span>
            )}
          </div>
        );
      },
    },
    {
      header: "Payment",
      key: "paymentStatus",
      render: (row) => {
        const isPaid = row.paymentStatus === "Paid";
        const isRefunded = row.paymentStatus === "Refunded";
        return (
          <div>
            <AdminBadge tone={isPaid ? "success" : isRefunded ? "neutral" : "warning"}>
              {row.paymentStatus}
            </AdminBadge>
            <div className="text-xs font-bold text-slate-700 mt-1">
              ₹{Number(row.totalPrice || row.amount || 0).toLocaleString("en-IN")}
            </div>
          </div>
        );
      },
    },
    {
      header: "Status",
      key: "status",
      render: (row) => {
        const isCancelled = row.status === "Cancelled" || row.status === 3;
        const isAttended = row.status === "Attended" || row.status === 4;
        return (
          <AdminBadge tone={isCancelled ? "danger" : isAttended ? "success" : "neutral"}>
            {row.status}
          </AdminBadge>
        );
      },
    },
    {
      header: "Booked Date",
      key: "bookedAt",
      render: (row) => (
        <span className="text-xs text-slate-500 font-mono">
          {new Date(row.bookedAt).toLocaleDateString()}
        </span>
      ),
    },
    {
      header: "Actions",
      key: "actions",
      render: (row) => {
        const isCancelled = row.status === "Cancelled" || row.status === 3 || row.paymentStatus === "Refunded";
        const isAttended = row.status === "Attended" || row.status === 4 || row.tickets?.some((t) => t.status === "CheckedIn" || t.checkedInAt);
        const canModifySessions =
          workshopSessions.length > 1 &&
          row.paymentStatus === "Paid" &&
          !isCancelled &&
          row.bookingSessions &&
          row.bookingSessions.length > 0 &&
          row.sessionsIncludedCount != null;

        return (
          <div className="table-actions-cell flex gap-1.5" style={{ flexWrap: "wrap", alignItems: "center" }}>
            {/* 1. Check In */}
            {isAttended ? (
              <span
                style={{
                  fontSize: "11px",
                  fontWeight: 700,
                  color: "#16a34a",
                  background: "#dcfce7",
                  padding: "2px 6px",
                  borderRadius: "4px",
                }}
              >
                ✓ Checked In
              </span>
            ) : !isCancelled ? (
              <button
                type="button"
                className="admin-btn-secondary btn-xs text-emerald-700"
                style={{ fontWeight: 600, borderColor: "#bbf7d0", background: "#f0fdf4" }}
                title="Check in attendee"
                onClick={() => handleQuickCheckIn(row)}
              >
                ✓ Check In
              </button>
            ) : null}

            {/* 2. Edit Customer Details */}
            <button
              type="button"
              className="admin-btn-secondary btn-xs"
              style={{ fontWeight: 600 }}
              title="View details and edit customer contact info"
              onClick={() => handleOpenEditModal(row)}
            >
              ✏️ Edit
            </button>

            {/* 3. Refund */}
            {!isCancelled && row.paymentStatus === "Paid" && (
              <button
                type="button"
                className="admin-btn-secondary btn-xs text-red-600"
                style={{ fontWeight: 600, borderColor: "#fecaca", background: "#fff5f5" }}
                title="Refund booking and revoke tickets"
                onClick={() => handleOpenRefundModal(row)}
              >
                💳 Refund
              </button>
            )}

            {/* Optional session swap */}
            {canModifySessions && (
              <button
                type="button"
                className="admin-btn-secondary btn-xs text-indigo-600"
                title="Swap session under this pass"
                onClick={() => handleOpenModifyModal(row)}
                style={{ fontWeight: 600 }}
              >
                🔄 Swap
              </button>
            )}
          </div>
        );
      },
    },
  ];

  return (
    <div className="workshop-subpage-container">
      <div className="subpage-header">
        <div>
          <h1 className="subpage-title">Workshop Bookings Ledger</h1>
          <p className="subpage-subtitle">
            Order transactions, customer contacts, check-in operations, and refunds.
          </p>
        </div>
        <AdminExportButton data={bookings} filename="workshop_bookings" />
      </div>

      {actionSuccess && <div className="subpage-success-banner">{actionSuccess}</div>}
      {error && <div className="subpage-error-banner">{error}</div>}

      <AdminDataTable
        columns={columns}
        data={bookings}
        loading={loading}
        page={page}
        pageSize={20}
        totalItems={bookings.length}
        onPageChange={setPage}
        emptyMessage="No booking transactions recorded for this workshop."
        emptyIcon="📅"
      />

      {/* ------------------------------------------------------------- */}
      {/* 1. Customer Edit & Details Modal */}
      {/* ------------------------------------------------------------- */}
      {editModalOpen && editBooking && (
        <div
          className="admin-modal-backdrop"
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(0, 0, 0, 0.65)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 9999,
            padding: "20px",
          }}
          onClick={() => setEditModalOpen(false)}
        >
          <div
            className="admin-modal-card"
            style={{
              background: "#ffffff",
              color: "#1e293b",
              borderRadius: "16px",
              padding: "24px",
              width: "100%",
              maxWidth: "560px",
              boxShadow: "0 20px 40px rgba(0, 0, 0, 0.2)",
              border: "1px solid #e2e8f0",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px", borderBottom: "1px solid #f1f5f9", paddingBottom: "12px" }}>
              <div>
                <h3 style={{ margin: 0, fontSize: "18px", fontWeight: 800, color: "#0f172a" }}>
                  Booking Details & Customer Contact
                </h3>
                <span style={{ fontSize: "12px", color: "#64748b" }}>
                  Reference: <strong className="font-mono text-slate-900">{editBooking.bookingReference}</strong>
                </span>
              </div>
              <button
                type="button"
                onClick={() => setEditModalOpen(false)}
                style={{ background: "none", border: "none", color: "#64748b", cursor: "pointer", fontSize: "20px" }}
              >
                ✕
              </button>
            </div>

            {editError && (
              <div style={{ padding: "10px 14px", borderRadius: "8px", background: "#fef2f2", border: "1px solid #fecaca", color: "#991b1b", fontSize: "13px", marginBottom: "16px" }}>
                ⚠️ {editError}
              </div>
            )}
            {editSuccess && (
              <div style={{ padding: "10px 14px", borderRadius: "8px", background: "#f0fdf4", border: "1px solid #bbf7d0", color: "#15803d", fontSize: "13px", marginBottom: "16px" }}>
                ✓ {editSuccess}
              </div>
            )}

            {/* Read-only Booking Summary */}
            <div style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "14px", marginBottom: "18px", fontSize: "13px" }}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "10px" }}>
                <div>
                  <span style={{ color: "#64748b", display: "block", fontSize: "11px", textTransform: "uppercase", fontWeight: 700 }}>Pass Type</span>
                  <span style={{ fontWeight: 700, color: "#0f172a" }}>{editBooking.passName || "Standard Entry"} ({editBooking.quantity || 1} seat)</span>
                </div>
                <div>
                  <span style={{ color: "#64748b", display: "block", fontSize: "11px", textTransform: "uppercase", fontWeight: 700 }}>Total Paid</span>
                  <span style={{ fontWeight: 700, color: "#16a34a" }}>₹{Number(editBooking.totalPrice || editBooking.amount || 0).toLocaleString("en-IN")} ({editBooking.paymentStatus})</span>
                </div>
                <div>
                  <span style={{ color: "#64748b", display: "block", fontSize: "11px", textTransform: "uppercase", fontWeight: 700 }}>Booking Status</span>
                  <span style={{ fontWeight: 700, color: "#0f172a" }}>{editBooking.status}</span>
                </div>
                <div>
                  <span style={{ color: "#64748b", display: "block", fontSize: "11px", textTransform: "uppercase", fontWeight: 700 }}>Booked On</span>
                  <span style={{ color: "#0f172a" }}>{new Date(editBooking.bookedAt).toLocaleString()}</span>
                </div>
              </div>
            </div>

            {/* Editable Fields */}
            <div style={{ display: "flex", flexDirection: "column", gap: "14px", marginBottom: "20px" }}>
              <div>
                <label style={{ display: "block", fontSize: "12px", fontWeight: 700, color: "#334155", marginBottom: "4px" }}>
                  Customer / Primary Attendee Name:
                </label>
                <input
                  type="text"
                  value={editForm.fullName}
                  onChange={(e) => setEditForm({ ...editForm, fullName: e.target.value })}
                  style={{
                    width: "100%",
                    padding: "9px 12px",
                    borderRadius: "8px",
                    border: "1px solid #cbd5e1",
                    fontSize: "14px",
                    background: "#ffffff",
                    color: "#0f172a",
                  }}
                  placeholder="e.g. Jane Doe"
                />
              </div>

              <div>
                <label style={{ display: "block", fontSize: "12px", fontWeight: 700, color: "#334155", marginBottom: "4px" }}>
                  WhatsApp Phone Number (Required for ticket delivery): *
                </label>
                <input
                  type="tel"
                  value={editForm.phone}
                  onChange={(e) => setEditForm({ ...editForm, phone: e.target.value })}
                  style={{
                    width: "100%",
                    padding: "9px 12px",
                    borderRadius: "8px",
                    border: "1px solid #cbd5e1",
                    fontSize: "14px",
                    background: "#ffffff",
                    color: "#0f172a",
                  }}
                  placeholder="e.g. +91 98765 43210"
                />
              </div>

              <div>
                <label style={{ display: "block", fontSize: "12px", fontWeight: 700, color: "#334155", marginBottom: "4px" }}>
                  Email Address (Optional):
                </label>
                <input
                  type="email"
                  value={editForm.email}
                  onChange={(e) => setEditForm({ ...editForm, email: e.target.value })}
                  style={{
                    width: "100%",
                    padding: "9px 12px",
                    borderRadius: "8px",
                    border: "1px solid #cbd5e1",
                    fontSize: "14px",
                    background: "#ffffff",
                    color: "#0f172a",
                  }}
                  placeholder="e.g. dancer@example.com"
                />
              </div>
            </div>

            {/* Modal Actions */}
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderTop: "1px solid #f1f5f9", paddingTop: "16px" }}>
              <button
                type="button"
                className="admin-btn-secondary"
                disabled={sendingWa || savingEdit}
                onClick={handleSendWhatsApp}
                style={{ color: "#16a34a", borderColor: "#bbf7d0", background: "#f0fdf4" }}
                title="Send official ticket pass to customer's WhatsApp phone"
              >
                {sendingWa ? "Queuing..." : "💬 Send WhatsApp Ticket"}
              </button>

              <div style={{ display: "flex", gap: "10px" }}>
                <button
                  type="button"
                  className="admin-btn-secondary"
                  disabled={savingEdit || sendingWa}
                  onClick={() => setEditModalOpen(false)}
                >
                  Close
                </button>
                <button
                  type="button"
                  className="admin-btn-primary"
                  disabled={savingEdit || sendingWa}
                  onClick={handleSaveCustomer}
                >
                  {savingEdit ? "Saving..." : "Save Changes"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ------------------------------------------------------------- */}
      {/* 2. Refund Confirmation Modal */}
      {/* ------------------------------------------------------------- */}
      {refundModalOpen && refundBooking && (
        <div
          className="admin-modal-backdrop"
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(0, 0, 0, 0.65)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 9999,
            padding: "20px",
          }}
          onClick={() => setRefundModalOpen(false)}
        >
          <div
            className="admin-modal-card"
            style={{
              background: "#ffffff",
              color: "#1e293b",
              borderRadius: "16px",
              padding: "24px",
              width: "100%",
              maxWidth: "520px",
              boxShadow: "0 20px 40px rgba(0, 0, 0, 0.2)",
              border: "1px solid #fecaca",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "10px", marginBottom: "14px" }}>
              <div style={{ fontSize: "24px" }}>💳</div>
              <div>
                <h3 style={{ margin: 0, fontSize: "18px", fontWeight: 800, color: "#991b1b" }}>
                  Confirm Booking Refund
                </h3>
                <span style={{ fontSize: "12px", color: "#64748b" }}>
                  Booking Ref: <strong>{refundBooking.bookingReference}</strong>
                </span>
              </div>
            </div>

            <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: "10px", padding: "14px", marginBottom: "16px", fontSize: "13px", color: "#991b1b" }}>
              <strong>⚠️ Warning:</strong> This action will revoke all issued tickets for{" "}
              <strong>{refundBooking.studentName}</strong>, cancel the booking, and initiate a server-authoritative refund of{" "}
              <strong>₹{Number(refundBooking.totalPrice || refundBooking.amount || 0).toLocaleString("en-IN")}</strong> to the customer's original payment method via Razorpay.
            </div>

            {refundError && (
              <div style={{ padding: "10px 14px", borderRadius: "8px", background: "#fef2f2", border: "1px solid #fecaca", color: "#991b1b", fontSize: "13px", marginBottom: "16px" }}>
                ⚠️ {refundError}
              </div>
            )}

            <div style={{ marginBottom: "20px" }}>
              <label style={{ display: "block", fontSize: "12px", fontWeight: 700, color: "#334155", marginBottom: "6px" }}>
                Refund Reason (Audit Record) *
              </label>
              <textarea
                rows="3"
                value={refundReason}
                onChange={(e) => setRefundReason(e.target.value)}
                placeholder="e.g. Customer requested cancellation, duplicate charge, scheduling conflict..."
                style={{
                  width: "100%",
                  padding: "9px 12px",
                  borderRadius: "8px",
                  border: "1px solid #cbd5e1",
                  fontSize: "13px",
                  background: "#ffffff",
                  color: "#0f172a",
                }}
              />
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px", borderTop: "1px solid #f1f5f9", paddingTop: "14px" }}>
              <button
                type="button"
                className="admin-btn-secondary"
                disabled={processingRefund}
                onClick={() => setRefundModalOpen(false)}
              >
                Keep Booking
              </button>
              <button
                type="button"
                className="admin-btn-danger"
                style={{ background: "#dc2626", color: "#ffffff", borderColor: "#dc2626" }}
                disabled={processingRefund}
                onClick={handleConfirmRefund}
              >
                {processingRefund ? "Processing Refund..." : `Confirm & Refund ₹${Number(refundBooking.totalPrice || refundBooking.amount || 0).toLocaleString("en-IN")}`}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ------------------------------------------------------------- */}
      {/* 3. Modify Session Modal (Swap session for pass) */}
      {/* ------------------------------------------------------------- */}
      {modifyModalOpen && selectedBooking && (
        <div
          className="admin-modal-backdrop"
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(0, 0, 0, 0.65)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 9999,
            padding: "20px",
          }}
          onClick={() => setModifyModalOpen(false)}
        >
          <div
            className="admin-modal-card"
            style={{
              background: "#ffffff",
              color: "#1e293b",
              borderRadius: "16px",
              padding: "24px",
              width: "100%",
              maxWidth: "540px",
              boxShadow: "0 20px 40px rgba(0, 0, 0, 0.2)",
              border: "1px solid #e2e8f0",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px", borderBottom: "1px solid #f1f5f9", paddingBottom: "10px" }}>
              <h3 style={{ margin: 0, fontSize: "18px", fontWeight: 800, color: "#0f172a" }}>
                Swap Workshop Session
              </h3>
              <button
                type="button"
                onClick={() => setModifyModalOpen(false)}
                style={{ background: "none", border: "none", color: "#64748b", cursor: "pointer", fontSize: "18px" }}
              >
                ✕
              </button>
            </div>

            <p style={{ fontSize: "13px", color: "#64748b", marginBottom: "16px" }}>
              Booking <strong>{selectedBooking.bookingReference}</strong> for{" "}
              <strong>{selectedBooking.studentName}</strong> ({selectedBooking.passName || "Pass"}).
            </p>

            {modifyError && (
              <div
                style={{
                  padding: "10px 14px",
                  borderRadius: "8px",
                  background: "#fef2f2",
                  border: "1px solid #fecaca",
                  color: "#991b1b",
                  fontSize: "12px",
                  marginBottom: "16px",
                }}
              >
                ⚠️ {modifyError}
              </div>
            )}

            {/* Step 1: Select which session to replace */}
            <div style={{ marginBottom: "16px" }}>
              <label style={{ display: "block", fontSize: "12px", fontWeight: 700, color: "#334155", marginBottom: "6px" }}>
                Current Session to Replace:
              </label>
              <select
                value={currentSessionId}
                onChange={(e) => setCurrentSessionId(e.target.value)}
                style={{
                  width: "100%",
                  padding: "8px 12px",
                  borderRadius: "8px",
                  background: "#f8fafc",
                  color: "#0f172a",
                  border: "1px solid #cbd5e1",
                  fontSize: "13px",
                }}
              >
                {(selectedBooking.bookingSessions || [])
                  .filter((bs) => bs.status === 1 || bs.status === "Booked")
                  .map((bs) => (
                    <option key={bs.workshopSessionId} value={bs.workshopSessionId}>
                      {bs.sessionTitle} ({new Date(bs.sessionDate).toLocaleDateString()} {bs.startTime?.slice(0, 5)} - {bs.endTime?.slice(0, 5)})
                    </option>
                  ))}
              </select>
            </div>

            {/* Step 2: Select replacement session */}
            <div style={{ marginBottom: "16px" }}>
              <label style={{ display: "block", fontSize: "12px", fontWeight: 700, color: "#334155", marginBottom: "6px" }}>
                Select Replacement Session:
              </label>
              <select
                value={replacementSessionId}
                onChange={(e) => setReplacementSessionId(e.target.value)}
                style={{
                  width: "100%",
                  padding: "8px 12px",
                  borderRadius: "8px",
                  background: "#f8fafc",
                  color: "#0f172a",
                  border: "1px solid #cbd5e1",
                  fontSize: "13px",
                }}
              >
                <option value="">-- Choose Replacement Session --</option>
                {workshopSessions
                  .filter((s) => s.id !== currentSessionId)
                  .map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.title} ({new Date(s.sessionDate).toLocaleDateString()} {s.startTime?.slice(0, 5)} - {s.endTime?.slice(0, 5)}) - {s.remainingCapacity ?? s.capacity} seats left {s.isBookingClosed ? "[CUTOFF CLOSED]" : ""}
                    </option>
                  ))}
              </select>
            </div>

            {/* Cutoff Override Section if replacement session cutoff closed */}
            {workshopSessions.find((s) => s.id === replacementSessionId)?.isBookingClosed && (
              <div
                style={{
                  marginBottom: "16px",
                  padding: "12px",
                  background: "#fffbeb",
                  border: "1px solid #fde68a",
                  borderRadius: "8px",
                }}
              >
                <label style={{ display: "flex", alignItems: "center", gap: "8px", fontSize: "13px", color: "#b45309", cursor: "pointer", fontWeight: 600 }}>
                  <input
                    type="checkbox"
                    checked={overrideCutoff}
                    onChange={(e) => setOverrideCutoff(e.target.checked)}
                  />
                  <span>Override Booking Cutoff Restriction</span>
                </label>

                {overrideCutoff && (
                  <div style={{ marginTop: "10px" }}>
                    <label style={{ display: "block", fontSize: "11px", color: "#92400e", marginBottom: "4px" }}>
                      Administrative Override Reason: *
                    </label>
                    <input
                      type="text"
                      value={overrideReason}
                      onChange={(e) => setOverrideReason(e.target.value)}
                      placeholder="e.g. Trainer approved late addition via phone"
                      style={{
                        width: "100%",
                        padding: "6px 10px",
                        borderRadius: "6px",
                        background: "#fff",
                        color: "#0f172a",
                        border: "1px solid #fde68a",
                        fontSize: "12px",
                      }}
                    />
                  </div>
                )}
              </div>
            )}

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px", marginTop: "24px" }}>
              <button
                type="button"
                className="admin-btn-secondary"
                disabled={modifying}
                onClick={() => setModifyModalOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="admin-btn-primary"
                disabled={modifying}
                onClick={handleConfirmModifySession}
              >
                {modifying ? "Swapping Session..." : "Confirm & Swap Session"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
