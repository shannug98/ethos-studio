import React, { useState } from "react";
import { useOutletContext, useNavigate, Link } from "react-router-dom";
import { AlertTriangle, X, XCircle } from "lucide-react";
import AdminKpiCard from "../../../components/admin/common/AdminKpiCard";
import { adminApi } from "../../../services/adminApi";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopOverview() {
  const { workshop, reloadWorkshop } = useOutletContext();
  const navigate = useNavigate();
  const workshopId = workshop.Id || workshop.id;

  const [cancelModal, setCancelModal] = useState({
    open: false,
    reason: "",
    submitting: false,
    error: null,
  });
  const [statusMsg, setStatusMsg] = useState(null);

  const phase = workshop.LifecyclePhase || workshop.lifecyclePhase || "Upcoming";
  const isCancelled = phase === "Cancelled";
  const isCompleted = phase === "Completed";

  const handleConfirmCancelWorkshop = async () => {
    if (!cancelModal.reason.trim()) {
      setCancelModal((prev) => ({ ...prev, error: "A cancellation reason is required." }));
      return;
    }

    setCancelModal((prev) => ({ ...prev, submitting: true, error: null }));

    try {
      await adminApi.cancelWorkshop(workshopId, cancelModal.reason.trim());
      setCancelModal({ open: false, reason: "", submitting: false, error: null });
      setStatusMsg({ type: "success", text: "Workshop has been cancelled successfully." });
      if (reloadWorkshop) await reloadWorkshop();
    } catch (err) {
      setCancelModal((prev) => ({
        ...prev,
        submitting: false,
        error: err?.message || "Failed to cancel workshop.",
      }));
    }
  };

  const bookedCount = workshop.BookedCount ?? workshop.bookedCount ?? 0;
  const attendedCount = workshop.AttendedCount ?? workshop.attendedCount ?? 0;
  const capacity = workshop.Capacity ?? workshop.capacity ?? 0;
  const capPct = workshop.CapacityPercentage ?? workshop.capacityPercentage ?? 0;
  const checkInPct = workshop.CheckInPercentage ?? workshop.checkInPercentage ?? 0;
  const totalRevenue = workshop.TotalRevenue ?? workshop.totalRevenue ?? 0;
  const price = workshop.Price ?? workshop.price ?? 0;
  const recentCheckIns = workshop.RecentCheckIns || workshop.recentCheckIns || [];

  return (
    <div className="workshop-subpage-container">
      {/* Page Title & Quick Action Bar */}
      <div className="subpage-header">
        <div>
          <h1 className="subpage-title">{workshop.Title || workshop.title} · Overview</h1>
          <p className="subpage-subtitle">
            Operational dashboard, live capacity tracking, and check-in telemetry.
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
          <button
            type="button"
            className="admin-btn-secondary"
            onClick={() => navigate(`/admin_portal/workshops/${workshopId}/attendees`)}
          >
            👥 View Attendees ({bookedCount})
          </button>
          <button
            type="button"
            className="admin-btn-secondary"
            onClick={() => navigate(`/admin_portal/workshops/${workshopId}/edit`)}
            title={attendedCount > 0 ? "Editing locked: Attendee check-ins have already occurred." : "Edit Workshop"}
          >
            {attendedCount > 0 ? "🔒 Edit Workshop (Locked)" : "✏️ Edit Workshop"}
          </button>

          {!isCompleted && !isCancelled && (
            <button
              type="button"
              className="admin-btn-danger"
              onClick={() => setCancelModal({ open: true, reason: "", submitting: false, error: null })}
              title="Cancel Workshop"
            >
              <XCircle size={15} />
              <span>Cancel Workshop</span>
            </button>
          )}
        </div>
      </div>

      {isCancelled && (
        <div
          className="subpage-error-banner"
          style={{
            background: "rgba(239, 68, 68, 0.12)",
            color: "#b91c1c",
            border: "1px solid rgba(239, 68, 68, 0.3)",
          }}
        >
          🚫 This workshop is <strong>Cancelled</strong>. Ticket sales are suspended.
        </div>
      )}

      {statusMsg && (
        <div className={`subpage-${statusMsg.type}-banner`}>{statusMsg.text}</div>
      )}

      {/* KPI Cards */}
      <div className="admin-kpi-grid">
        <AdminKpiCard
          title="Total Bookings"
          value={bookedCount}
          subtitle={`${capPct}% of ${capacity} capacity`}
          icon="👥"
          tone="neutral"
        />
        <AdminKpiCard
          title="Checked In"
          value={attendedCount}
          subtitle={`${checkInPct}% check-in attendance`}
          icon="✅"
          tone="success"
        />
        <AdminKpiCard
          title="Total Revenue"
          value={`₹ ${Number(totalRevenue).toLocaleString("en-IN")}`}
          subtitle={`Ticket price: ₹ ${price}`}
          icon="₹"
          tone="brand"
        />
        <AdminKpiCard
          title="Lead Trainer"
          value={workshop.TrainerName || workshop.trainerName || "Ethos Master"}
          subtitle={`Style: ${workshop.DanceStyle || workshop.danceStyle} (${workshop.Level || workshop.level})`}
          icon="🩰"
          tone="neutral"
        />
      </div>

      {/* 2-Column Grid: Schedule / Venue + Recent Check-ins */}
      <div className="overview-sections-grid">
        {/* Left Column: Workshop Details Summary */}
        <div className="overview-card">
          <div className="overview-card-header">
            <h3 className="card-section-title">Schedule & Venue Details</h3>
            <Link to={`/admin_portal/workshops/${workshopId}/details`} className="card-header-link">
              Full Details →
            </Link>
          </div>

          <div className="overview-details-list">
            <div className="detail-item-row">
              <span className="detail-label">Date & Time:</span>
              <span className="detail-value">{workshop.FormattedSchedule || workshop.formattedSchedule}</span>
            </div>
            <div className="detail-item-row">
              <span className="detail-label">Location / Studio:</span>
              <span className="detail-value">📍 {workshop.Venue || workshop.venue || "Ethos Main Studio"}</span>
            </div>
            <div className="detail-item-row">
              <span className="detail-label">Dance Style:</span>
              <span className="detail-value">{workshop.DanceStyle || workshop.danceStyle}</span>
            </div>
            <div className="detail-item-row">
              <span className="detail-label">Experience Level:</span>
              <span className="detail-value">{workshop.Level || workshop.level}</span>
            </div>
            <div className="detail-item-row">
              <span className="detail-label">Reference ID:</span>
              <span className="detail-value font-mono">{workshop.WorkshopReference || workshop.workshopReference || "WKS-2026"}</span>
            </div>
          </div>

          {workshop.Description || workshop.description ? (
            <div className="overview-desc-box">
              <span className="desc-box-label">Description:</span>
              <p className="desc-box-text">{workshop.Description || workshop.description}</p>
            </div>
          ) : null}
        </div>

        {/* Right Column: Live Check-in Activity */}
        <div className="overview-card">
          <div className="overview-card-header">
            <h3 className="card-section-title">Recent Check-ins ({recentCheckIns.length})</h3>
            <Link to={`/admin_portal/workshops/${workshopId}/scanner`} className="card-header-link">
              Launch Scanner →
            </Link>
          </div>

          {recentCheckIns.length === 0 ? (
            <div className="overview-empty-state">
              <span className="empty-icon">🎟️</span>
              <p>No check-in activity recorded yet.</p>
              <small>Use the QR Scanner to check in attendees as they arrive.</small>
              <button
                type="button"
                className="admin-btn-primary btn-sm"
                style={{ marginTop: "12px" }}
                onClick={() => navigate(`/admin_portal/workshops/${workshopId}/scanner`)}
              >
                Open QR Scanner
              </button>
            </div>
          ) : (
            <div className="overview-checkin-list">
              {recentCheckIns.slice(0, 6).map((c, i) => (
                <div key={c.ticketId || i} className="overview-checkin-row">
                  <div className="checkin-mini-avatar">{c.attendeeName ? c.attendeeName.charAt(0) : "A"}</div>
                  <div className="checkin-mini-info">
                    <span className="checkin-mini-name">{c.attendeeName}</span>
                    <span className="checkin-mini-ticket font-mono">{c.ticketNumber}</span>
                  </div>
                  <span className="checkin-mini-time">{c.formattedTime}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Cancel Confirmation Modal */}
      {cancelModal.open && (
        <div
          className="cancel-modal-overlay"
          onClick={() => setCancelModal({ open: false, reason: "", submitting: false, error: null })}
        >
          <div className="cancel-modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="cancel-modal-header">
              <h3 className="cancel-modal-title">
                <AlertTriangle size={18} />
                <span>Confirm Workshop Cancellation</span>
              </h3>
              <button
                type="button"
                style={{ background: "transparent", border: "none", cursor: "pointer", color: "#64748b" }}
                onClick={() => setCancelModal({ open: false, reason: "", submitting: false, error: null })}
              >
                <X size={18} />
              </button>
            </div>

            <div className="cancel-modal-body">
              <p>
                Are you sure you want to cancel{" "}
                <strong>"{workshop.Title || workshop.title}"</strong>? This will notify all registered attendees,
                stop all ticket bookings, and archive this session as Cancelled.
              </p>

              <div className="form-group">
                <label className="form-label" style={{ color: "#991b1b" }}>
                  Cancellation Reason (Required) *
                </label>
                <textarea
                  rows="3"
                  className="form-control"
                  placeholder="e.g. Lead trainer unavailable, studio renovation, weather warning..."
                  value={cancelModal.reason}
                  onChange={(e) =>
                    setCancelModal((prev) => ({ ...prev, reason: e.target.value, error: null }))
                  }
                  required
                />
              </div>

              {cancelModal.error && (
                <div style={{ color: "#dc2626", fontSize: "13px", fontWeight: "600" }}>
                  ⚠️ {cancelModal.error}
                </div>
              )}
            </div>

            <div className="cancel-modal-footer">
              <button
                type="button"
                className="admin-btn-secondary"
                disabled={cancelModal.submitting}
                onClick={() => setCancelModal({ open: false, reason: "", submitting: false, error: null })}
              >
                Keep Workshop
              </button>
              <button
                type="button"
                className="admin-btn-danger-solid"
                disabled={cancelModal.submitting}
                onClick={handleConfirmCancelWorkshop}
              >
                {cancelModal.submitting ? "Cancelling Workshop..." : "Yes, Cancel Workshop"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
