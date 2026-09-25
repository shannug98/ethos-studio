import React, { useState, useEffect, useCallback } from "react";
import { useOutletContext, useNavigate, Link } from "react-router-dom";
import { AlertTriangle, X, XCircle, RefreshCw } from "lucide-react";
import AdminKpiCard from "../../../components/admin/common/AdminKpiCard";
import AdminBadge from "../../../components/admin/common/AdminBadge";
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
    loadingStats: false,
    stats: null,
    error: null,
  });
  const [statusMsg, setStatusMsg] = useState(null);

  // Refund tracking state for cancelled workshops
  const [refundProgress, setRefundProgress] = useState(null);
  const [loadingProgress, setLoadingProgress] = useState(false);
  const [retryingRefunds, setRetryingRefunds] = useState(false);

  const phase = workshop.LifecyclePhase || workshop.lifecyclePhase || "Upcoming";
  const isCancelled = phase === "Cancelled" || workshop.status === "Cancelled" || workshop.Status === 4;
  const isCompleted = phase === "Completed" || workshop.status === "Completed" || workshop.Status === 3;

  // Load cancellation stats when opening cancel modal
  const handleOpenCancelModal = async () => {
    setCancelModal({
      open: true,
      reason: "",
      submitting: false,
      loadingStats: true,
      stats: null,
      error: null,
    });

    try {
      const stats = await adminApi.getWorkshopCancellationStats(workshopId);
      setCancelModal((prev) => ({
        ...prev,
        loadingStats: false,
        stats,
      }));
    } catch (err) {
      setCancelModal((prev) => ({
        ...prev,
        loadingStats: false,
        error: "Could not load pre-cancellation financial metrics.",
      }));
    }
  };

  const handleConfirmCancelWorkshop = async () => {
    if (!cancelModal.reason.trim()) {
      setCancelModal((prev) => ({ ...prev, error: "A cancellation reason is required." }));
      return;
    }

    setCancelModal((prev) => ({ ...prev, submitting: true, error: null }));

    try {
      await adminApi.cancelWorkshop(workshopId, cancelModal.reason.trim());
      setCancelModal({ open: false, reason: "", submitting: false, loadingStats: false, stats: null, error: null });
      setStatusMsg({
        type: "success",
        text: "Workshop cancelled successfully. Refund jobs have been enqueued for all paid attendees.",
      });
      if (reloadWorkshop) await reloadWorkshop();
      await loadRefundProgress();
    } catch (err) {
      setCancelModal((prev) => ({
        ...prev,
        submitting: false,
        error: err?.message || "Failed to cancel workshop.",
      }));
    }
  };

  // Load live refund progress
  const loadRefundProgress = useCallback(async () => {
    if (!isCancelled) return;
    setLoadingProgress(true);
    try {
      const progress = await adminApi.getWorkshopRefundProgress(workshopId);
      setRefundProgress(progress);
    } catch (err) {
      console.warn("Could not load refund progress:", err);
    } finally {
      setLoadingProgress(false);
    }
  }, [workshopId, isCancelled]);

  useEffect(() => {
    if (isCancelled) {
      loadRefundProgress();
      const interval = setInterval(loadRefundProgress, 10000);
      return () => clearInterval(interval);
    }
  }, [isCancelled, loadRefundProgress]);

  // Retry failed refunds
  const handleRetryRefunds = async () => {
    setRetryingRefunds(true);
    try {
      const res = await adminApi.retryWorkshopRefunds(workshopId);
      setStatusMsg({
        type: "success",
        text: res?.message || `Successfully requeued failed refund jobs.`,
      });
      await loadRefundProgress();
    } catch (err) {
      setStatusMsg({
        type: "error",
        text: err?.message || "Failed to retry refunds.",
      });
    } finally {
      setRetryingRefunds(false);
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
            Operational dashboard, live capacity tracking, and refund telemetry.
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
            onClick={() => navigate(`/admin_portal/workshops/${workshopId}/bookings-attendees?view=attendees`)}
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
              onClick={handleOpenCancelModal}
              title="Cancel Workshop and refund registered attendees"
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
            background: "#fef2f2",
            color: "#991b1b",
            border: "1px solid #fecaca",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <div>
            🚫 This workshop is <strong>Cancelled</strong>. Ticket sales are suspended, and all bookings have been invalidated.
          </div>
        </div>
      )}

      {statusMsg && (
        <div className={`subpage-${statusMsg.type}-banner`}>{statusMsg.text}</div>
      )}

      {/* ------------------------------------------------------------- */}
      {/* Refund Processing Tracker for Cancelled Workshops */}
      {/* ------------------------------------------------------------- */}
      {isCancelled && refundProgress && (
        <div
          style={{
            background: "#ffffff",
            border: "1px solid #e2e8f0",
            borderRadius: "14px",
            padding: "20px",
            boxShadow: "0 1px 3px rgba(0, 0, 0, 0.04)",
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px" }}>
            <div>
              <h3 style={{ margin: 0, fontSize: "16px", fontWeight: 800, color: "#0f172a" }}>
                💳 Workshop Cancellation Refund Tracker
              </h3>
              <span style={{ fontSize: "12px", color: "#64748b" }}>
                Automated Razorpay outbox status for cancelled attendee bookings
              </span>
            </div>
            <div style={{ display: "flex", gap: "10px", alignItems: "center" }}>
              {refundProgress.failedJobs > 0 && (
                <button
                  type="button"
                  className="admin-btn-danger-solid btn-xs"
                  disabled={retryingRefunds}
                  onClick={handleRetryRefunds}
                  style={{ fontWeight: 700 }}
                >
                  {retryingRefunds ? "Retrying..." : `🔄 Retry ${refundProgress.failedJobs} Failed Refund(s)`}
                </button>
              )}
              <button
                type="button"
                className="admin-btn-secondary btn-xs"
                onClick={loadRefundProgress}
                disabled={loadingProgress}
                title="Refresh refund progress"
              >
                <RefreshCw size={13} className={loadingProgress ? "animate-spin" : ""} />
                <span>Refresh</span>
              </button>
            </div>
          </div>

          {/* Refund Metric Grid */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: "12px", marginBottom: "16px" }}>
            <div style={{ background: "#f8fafc", padding: "12px", borderRadius: "10px", border: "1px solid #e2e8f0" }}>
              <span style={{ display: "block", fontSize: "11px", fontWeight: 700, color: "#64748b" }}>TOTAL REFUNDS</span>
              <span style={{ fontSize: "20px", fontWeight: 800, color: "#0f172a" }}>{refundProgress.totalJobs}</span>
            </div>
            <div style={{ background: "#f0fdf4", padding: "12px", borderRadius: "10px", border: "1px solid #bbf7d0" }}>
              <span style={{ display: "block", fontSize: "11px", fontWeight: 700, color: "#15803d" }}>PROCESSED</span>
              <span style={{ fontSize: "20px", fontWeight: 800, color: "#16a34a" }}>{refundProgress.processedJobs}</span>
            </div>
            <div style={{ background: "#fffbeb", padding: "12px", borderRadius: "10px", border: "1px solid #fde68a" }}>
              <span style={{ display: "block", fontSize: "11px", fontWeight: 700, color: "#b45309" }}>QUEUED / IN FLIGHT</span>
              <span style={{ fontSize: "20px", fontWeight: 800, color: "#d97706" }}>
                {refundProgress.requestedJobs + refundProgress.processingJobs}
              </span>
            </div>
            {refundProgress.failedJobs > 0 && (
              <div style={{ background: "#fef2f2", padding: "12px", borderRadius: "10px", border: "1px solid #fecaca" }}>
                <span style={{ display: "block", fontSize: "11px", fontWeight: 700, color: "#991b1b" }}>FAILED</span>
                <span style={{ fontSize: "20px", fontWeight: 800, color: "#dc2626" }}>{refundProgress.failedJobs}</span>
              </div>
            )}
            {refundProgress.reconciliationRequiredJobs > 0 && (
              <div style={{ background: "#fff7ed", padding: "12px", borderRadius: "10px", border: "1px solid #fed7aa" }}>
                <span style={{ display: "block", fontSize: "11px", fontWeight: 700, color: "#c2410c" }}>RECONCILIATION</span>
                <span style={{ fontSize: "20px", fontWeight: 800, color: "#ea580c" }}>{refundProgress.reconciliationRequiredJobs}</span>
              </div>
            )}
          </div>

          {/* Progress Bar */}
          {refundProgress.totalJobs > 0 && (
            <div style={{ marginBottom: "18px" }}>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: "12px", fontWeight: 600, color: "#475569", marginBottom: "4px" }}>
                <span>Completion</span>
                <span>
                  {Math.round((refundProgress.processedJobs / refundProgress.totalJobs) * 100)}% ({refundProgress.processedJobs} of {refundProgress.totalJobs})
                </span>
              </div>
              <div style={{ width: "100%", height: "8px", background: "#e2e8f0", borderRadius: "4px", overflow: "hidden" }}>
                <div
                  style={{
                    width: `${Math.round((refundProgress.processedJobs / refundProgress.totalJobs) * 100)}%`,
                    height: "100%",
                    background: refundProgress.failedJobs > 0 ? "#eab308" : "#16a34a",
                    transition: "width 0.3s ease",
                  }}
                />
              </div>
            </div>
          )}

          {/* Refund Job List */}
          {refundProgress.jobs?.length > 0 && (
            <div style={{ maxHeight: "280px", overflowY: "auto", border: "1px solid #e2e8f0", borderRadius: "8px" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "12px" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0", textAlign: "left", color: "#64748b" }}>
                    <th style={{ padding: "8px 12px" }}>Customer</th>
                    <th style={{ padding: "8px 12px" }}>Phone</th>
                    <th style={{ padding: "8px 12px" }}>Amount</th>
                    <th style={{ padding: "8px 12px" }}>Status</th>
                    <th style={{ padding: "8px 12px" }}>Gateway Reference</th>
                  </tr>
                </thead>
                <tbody>
                  {refundProgress.jobs.map((j) => (
                    <tr key={j.jobId} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "8px 12px", fontWeight: 600, color: "#0f172a" }}>{j.customerName}</td>
                      <td style={{ padding: "8px 12px", color: "#64748b" }}>{j.customerPhone || "—"}</td>
                      <td style={{ padding: "8px 12px", fontWeight: 700 }}>₹{Number(j.amount).toLocaleString("en-IN")}</td>
                      <td style={{ padding: "8px 12px" }}>
                        <AdminBadge
                          tone={
                            j.status === "Processed"
                              ? "success"
                              : j.status === "Failed"
                              ? "danger"
                              : j.status === "ReconciliationRequired"
                              ? "warning"
                              : "neutral"
                          }
                        >
                          {j.status}
                        </AdminBadge>
                        {j.lastError && (
                          <div style={{ fontSize: "10px", color: "#dc2626", marginTop: "2px", maxWidth: "200px" }}>
                            {j.lastError}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: "8px 12px", fontFamily: "monospace", color: "#475569" }}>
                        {j.razorpayRefundId || "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
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

      {/* ------------------------------------------------------------- */}
      {/* Cancel Confirmation Modal with Live Pre-Cancellation Stats */}
      {/* ------------------------------------------------------------- */}
      {cancelModal.open && (
        <div
          className="cancel-modal-overlay"
          onClick={() => setCancelModal({ open: false, reason: "", submitting: false, loadingStats: false, stats: null, error: null })}
        >
          <div className="cancel-modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="cancel-modal-header">
              <h3 className="cancel-modal-title">
                <AlertTriangle size={18} />
                <span>Confirm Workshop Cancellation & Refunds</span>
              </h3>
              <button
                type="button"
                style={{ background: "transparent", border: "none", cursor: "pointer", color: "#64748b" }}
                onClick={() => setCancelModal({ open: false, reason: "", submitting: false, loadingStats: false, stats: null, error: null })}
              >
                <X size={18} />
              </button>
            </div>

            <div className="cancel-modal-body">
              <p style={{ margin: "0 0 14px", fontSize: "14px", color: "#334155" }}>
                Are you sure you want to cancel <strong>"{workshop.Title || workshop.title}"</strong>?
              </p>

              {/* Pre-cancellation stats card */}
              {cancelModal.loadingStats ? (
                <div style={{ padding: "16px", textAlign: "center", color: "#64748b", fontSize: "13px" }}>
                  Loading financial & attendee impact...
                </div>
              ) : cancelModal.stats ? (
                <div
                  style={{
                    background: "#fef2f2",
                    border: "1px solid #fecaca",
                    borderRadius: "10px",
                    padding: "14px",
                    marginBottom: "16px",
                    fontSize: "13px",
                  }}
                >
                  <div style={{ fontWeight: 700, color: "#991b1b", marginBottom: "8px" }}>
                    Impact Summary:
                  </div>
                  <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "8px", color: "#7f1d1d" }}>
                    <div>Total Bookings: <strong>{cancelModal.stats.totalBookings}</strong></div>
                    <div>Paid Bookings to Refund: <strong>{cancelModal.stats.paidBookings}</strong></div>
                    <div>Total Attendees: <strong>{cancelModal.stats.totalAttendees}</strong></div>
                    <div>Total Refund Amount: <strong>₹{Number(cancelModal.stats.totalRefundableAmount).toLocaleString("en-IN")}</strong></div>
                  </div>
                  <div style={{ marginTop: "10px", fontSize: "12px", color: "#b91c1c" }}>
                    ⚠️ All issued ticket passes will be invalidated immediately, and automated Razorpay refunds will be enqueued for all paid bookings.
                  </div>
                </div>
              ) : null}

              <div className="form-group">
                <label className="form-label" style={{ color: "#991b1b", fontWeight: 700 }}>
                  Cancellation Reason (Required for Audit & Notifications) *
                </label>
                <textarea
                  rows="3"
                  className="form-control"
                  placeholder="e.g. Lead trainer unavailable due to emergency, studio facility maintenance..."
                  value={cancelModal.reason}
                  onChange={(e) =>
                    setCancelModal((prev) => ({ ...prev, reason: e.target.value, error: null }))
                  }
                  required
                />
              </div>

              {cancelModal.error && (
                <div style={{ color: "#dc2626", fontSize: "13px", fontWeight: "600", marginTop: "10px" }}>
                  ⚠️ {cancelModal.error}
                </div>
              )}
            </div>

            <div className="cancel-modal-footer">
              <button
                type="button"
                className="admin-btn-secondary"
                disabled={cancelModal.submitting}
                onClick={() => setCancelModal({ open: false, reason: "", submitting: false, loadingStats: false, stats: null, error: null })}
              >
                Keep Workshop
              </button>
              <button
                type="button"
                className="admin-btn-danger-solid"
                disabled={cancelModal.submitting || cancelModal.loadingStats}
                onClick={handleConfirmCancelWorkshop}
              >
                {cancelModal.submitting ? "Cancelling & Enqueuing Refunds..." : "Yes, Cancel & Refund Attendees"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
