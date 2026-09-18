import React, { useState, useEffect, useCallback } from "react";
import { useParams, NavLink, Link, Outlet } from "react-router-dom";
import { adminApi } from "../../../services/adminApi";
import AdminLoadingState from "../common/AdminLoadingState";
import "./AdminWorkshopLayout.css";

export default function AdminWorkshopLayout() {
  const { workshopId } = useParams();
  const [workshop, setWorkshop] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadWorkshopData = useCallback(async () => {
    if (!workshopId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await adminApi.getWorkshopOverview(workshopId);
      setWorkshop(data);
    } catch (err) {
      setError(err?.message || "Failed to load workshop details.");
    } finally {
      setLoading(false);
    }
  }, [workshopId]);

  useEffect(() => {
    loadWorkshopData();
  }, [loadWorkshopData]);

  if (loading) {
    return (
      <div className="workshop-layout-loading">
        <AdminLoadingState message="Loading workshop command workspace..." type="card" rows={3} />
      </div>
    );
  }

  if (error || !workshop) {
    return (
      <div className="workshop-layout-error">
        <div className="error-card">
          <span className="error-icon">⚠️</span>
          <h2>Workshop Not Found</h2>
          <p>{error || "The requested workshop does not exist or has been removed."}</p>
          <Link to="/admin_portal/workshops" className="admin-btn primary">
            ← Back to Workshops Listing
          </Link>
        </div>
      </div>
    );
  }

  const attendeeCount = workshop.BookedCount ?? workshop.bookedCount ?? 0;
  const attendedCount = workshop.AttendedCount ?? workshop.attendedCount ?? (workshop.RecentCheckIns?.length || workshop.recentCheckIns?.length || 0);
  const status = workshop.Status ?? workshop.status ?? "Published";
  const phase = workshop.LifecyclePhase || workshop.lifecyclePhase || "Upcoming";

  const isEditLocked = attendedCount > 0 || (phase === "Ongoing" && attendedCount > 0) || phase === "Completed";

  return (
    <div className="ethos-workshop-portal">
      {/* Top Breadcrumbs */}
      <div className="workshop-breadcrumbs">
        <Link to="/admin_portal/workshops" className="breadcrumb-link">
          Workshops
        </Link>
        <span className="breadcrumb-sep">›</span>
        <span className="breadcrumb-current">{workshop.Title || workshop.title}</span>
      </div>

      <div className="workshop-portal-body">
        {/* Left Workshop Sub-Sidebar */}
        <aside className="workshop-sub-sidebar">
          {/* Workshop Card */}
          <div className="workshop-summary-card">
            <div className="workshop-banner-wrap">
              {workshop.ImageUrl || workshop.imageUrl ? (
                <img
                  src={workshop.ImageUrl || workshop.imageUrl}
                  alt={workshop.Title || workshop.title}
                  className="workshop-banner-img"
                  onError={(e) => {
                    e.target.style.display = "none";
                  }}
                />
              ) : null}
              <div className="workshop-banner-fallback">🎭</div>
            </div>

            <div className="workshop-card-body">
              <h3 className="ws-title">{workshop.Title || workshop.title}</h3>
              <p className="ws-schedule">{workshop.FormattedSchedule || workshop.formattedSchedule}</p>
              <div className="ws-status-row">
                <span className={`ws-status-badge status-${status.toLowerCase()}`}>
                  {status}
                </span>
                <span className="ws-seats-pill">
                  {attendeeCount} / {workshop.Capacity || workshop.capacity || "—"} seats
                </span>
              </div>
            </div>
          </div>

          {/* Navigation Links */}
          <nav className="workshop-sub-nav">
            <NavLink
              to={`/admin_portal/workshops/${workshopId}/overview`}
              className={({ isActive }) => `sub-nav-item ${isActive ? "active" : ""}`}
            >
              <span className="nav-icon">🏠</span>
              <span className="nav-label">Overview</span>
            </NavLink>

            <NavLink
              to={`/admin_portal/workshops/${workshopId}/details`}
              className={({ isActive }) => `sub-nav-item ${isActive ? "active" : ""}`}
            >
              <span className="nav-icon">📋</span>
              <span className="nav-label">Workshop Details</span>
            </NavLink>

            <NavLink
              to={`/admin_portal/workshops/${workshopId}/scanner`}
              className={({ isActive }) => `sub-nav-item scanner-nav-item ${isActive ? "active" : ""}`}
            >
              <span className="nav-icon">📷</span>
              <span className="nav-label">QR Scanner</span>
            </NavLink>

            <NavLink
              to={`/admin_portal/workshops/${workshopId}/attendees`}
              className={({ isActive }) => `sub-nav-item ${isActive ? "active" : ""}`}
            >
              <span className="nav-icon">👥</span>
              <span className="nav-label">Attendees ({attendeeCount})</span>
            </NavLink>

            <NavLink
              to={`/admin_portal/workshops/${workshopId}/bookings`}
              className={({ isActive }) => `sub-nav-item ${isActive ? "active" : ""}`}
            >
              <span className="nav-icon">📅</span>
              <span className="nav-label">Bookings</span>
            </NavLink>

            <NavLink
              to={`/admin_portal/workshops/${workshopId}/feedback`}
              className={({ isActive }) => `sub-nav-item ${isActive ? "active" : ""}`}
            >
              <span className="nav-icon">💬</span>
              <span className="nav-label">Feedback</span>
            </NavLink>

            <NavLink
              to={`/admin_portal/workshops/${workshopId}/edit`}
              className={({ isActive }) => `sub-nav-item ${isActive ? "active" : ""} ${isEditLocked ? "locked-edit-nav" : ""}`}
              title={isEditLocked ? "Editing locked: Check-in activity has occurred for this ongoing session." : "Edit Workshop"}
            >
              <span className="nav-icon">{isEditLocked ? "🔒" : "✏️"}</span>
              <span className="nav-label">{isEditLocked ? "Edit Workshop (Locked)" : "Edit Workshop"}</span>
            </NavLink>
          </nav>

          {/* Return link */}
          <div className="workshop-back-wrap">
            <Link to="/admin_portal/workshops" className="workshop-back-btn">
              ← Back to Workshops
            </Link>
          </div>
        </aside>

        {/* Right Main Content Outlet */}
        <main className="workshop-portal-content">
          <Outlet context={{ workshop, reloadWorkshop: loadWorkshopData, attendeeCount }} />
        </main>
      </div>
    </div>
  );
}
