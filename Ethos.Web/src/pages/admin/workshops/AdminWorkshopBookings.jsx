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

  const [actionSuccess, setActionSuccess] = useState(null);

  const handleEditContact = async (row) => {
    const bookingId = row.id || row.bookingId;
    const newPhone = window.prompt(
      `Update WhatsApp Contact Phone for ${row.studentName || "Attendee"}:\n(e.g., +919876543210 or 9876543210)`,
      row.studentPhone || ""
    );
    if (newPhone === null) return;
    if (!newPhone.trim()) {
      alert("Phone number cannot be empty.");
      return;
    }

    try {
      await adminApi.updateBookingContact(bookingId, { phone: newPhone.trim() });
      setActionSuccess(`Updated phone number for ${row.studentName} to ${newPhone.trim()}.`);
      await loadBookings();
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      alert(err?.message || "Failed to update contact number.");
    }
  };

  const handleResendWhatsApp = async (row) => {
    const bookingId = row.id || row.bookingId;
    if (!window.confirm(`Resend official WhatsApp ticket PDF pass for booking ${row.bookingReference}?`)) return;

    try {
      const res = await adminApi.resendWhatsAppTicket(bookingId);
      setActionSuccess(res?.message || `WhatsApp Ticket PDF queued and resent to ${row.studentName || "contact"}.`);
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      alert(err?.message || "Failed to resend WhatsApp ticket.");
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
      header: "Tickets",
      key: "quantity",
      render: (row) => <span className="font-semibold">{row.quantity || 1} ticket(s)</span>,
    },
    {
      header: "Payment Status",
      key: "paymentStatus",
      render: (row) => (
        <AdminBadge tone={row.paymentStatus === "Paid" ? "success" : "warning"}>
          {row.paymentStatus}
        </AdminBadge>
      ),
    },
    {
      header: "Booking Status",
      key: "status",
      render: (row) => <AdminBadge tone="neutral">{row.status}</AdminBadge>,
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
      render: (row) => (
        <div className="table-actions-cell flex gap-1.5">
          <button
            type="button"
            className="admin-btn-secondary btn-xs"
            title="Edit WhatsApp phone number"
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
            💬 Resend
          </button>
        </div>
      ),
    },
  ];

  return (
    <div className="workshop-subpage-container">
      <div className="subpage-header">
        <div>
          <h1 className="subpage-title">Workshop Bookings Ledger</h1>
          <p className="subpage-subtitle">
            Order transactions and financial receipts recorded for this workshop.
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
    </div>
  );
}
