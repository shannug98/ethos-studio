import React from "react";
import { useOutletContext, useNavigate, Link } from "react-router-dom";
import AdminKpiCard from "../../../components/admin/common/AdminKpiCard";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopOverview() {
  const { workshop } = useOutletContext();
  const navigate = useNavigate();
  const workshopId = workshop.Id || workshop.id;

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
          >
            ✏️ Edit Workshop
          </button>
        </div>
      </div>

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
    </div>
  );
}
