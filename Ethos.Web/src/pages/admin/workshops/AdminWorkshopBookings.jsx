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
          <div className="text-xs text-slate-500">{row.studentPhone}</div>
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
