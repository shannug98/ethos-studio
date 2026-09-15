import React, { useState, useEffect } from "react";
import { adminApi, getAdminUser } from "../../services/adminApi";
import AdminKpiCard from "../../components/admin/common/AdminKpiCard";
import { formatAdminLastActive, formatAdminDateTime } from "../../utils/adminFormatters";
import "./AdminDevices.css";

export default function AdminDevices() {
  const currentAdmin = getAdminUser();
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [actionSuccess, setActionSuccess] = useState(null);
  const [loggingOutSessionId, setLoggingOutSessionId] = useState(null);

  const loadSessions = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getSessions();
      setSessions(Array.isArray(res) ? res : []);
    } catch (err) {
      setError(err.message || "Failed to load active sessions.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSessions();
  }, []);

  const handleLogoutDevice = async (sessionId) => {
    try {
      setLoggingOutSessionId(sessionId);
      setError(null);
      await adminApi.logoutSession(sessionId);
      setActionSuccess("Successfully logged out session. Slot is now available.");
      await loadSessions();
      setTimeout(() => setActionSuccess(null), 5000);
    } catch (err) {
      setError(err.message || "Unable to log out this device.");
    } finally {
      setLoggingOutSessionId(null);
    }
  };

  // Strictly filter only active sessions
  const activeSessions = (sessions || []).filter(
    (session) =>
      session.isActive === true &&
      session.loggedOutAt == null &&
      session.revokedAt == null &&
      (!session.expiresAt || new Date(session.expiresAt) > new Date())
  );

  const currentSession = activeSessions.find((s) => s.isCurrent || s.isCurrentDevice);
  const maxSlots = 2;
  const slotsUsed = activeSessions.length;

  return (
    <div className="admin-devices-page">
      {/* Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Active Sessions & Device Management</h1>
          <p className="admin-page-subtitle">
            Hotstar-style concurrent session control. Maximum {maxSlots} active administrative sessions are permitted across approved devices.
          </p>
        </div>
        <div className="admin-header-actions">
          <button type="button" className="btn btn-secondary" onClick={loadSessions} disabled={loading}>
            {loading ? "Refreshing..." : "Refresh Sessions"}
          </button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="admin-kpi-grid">
        <AdminKpiCard
          label="Active Sessions"
          value={`${slotsUsed} of ${maxSlots}`}
          tone={slotsUsed >= maxSlots ? "warning" : "success"}
          sublabel="Slots currently occupied"
          icon="🛡️"
        />
        <AdminKpiCard
          label="Slot Status"
          value={slotsUsed >= maxSlots ? "FULL (2 of 2)" : `${maxSlots - slotsUsed} AVAILABLE`}
          tone={slotsUsed >= maxSlots ? "danger" : "success"}
          sublabel={slotsUsed >= maxSlots ? "New logins require remote logout" : "Ready for next sign-in"}
          icon={slotsUsed >= maxSlots ? "🔒" : "🔓"}
        />
        <AdminKpiCard
          label="Current Session"
          value={currentSession ? `${currentSession.browser || "Browser"}` : "Active"}
          tone="brand"
          sublabel={currentSession ? `${currentSession.operatingSystem || "Windows"} (This device)` : "Administrative portal"}
          icon="💻"
        />
        <AdminKpiCard
          label="Administrator"
          value={currentAdmin?.fullName || "Ethos Partner"}
          tone="info"
          sublabel={currentAdmin?.phone ? `+91 ${currentAdmin.phone}` : "Partner Account"}
          icon="👤"
        />
      </div>

      {error && <div className="alert alert-danger">{error}</div>}
      {actionSuccess && <div className="alert alert-success">{actionSuccess}</div>}

      {/* Active Administrative Sessions Section */}
      <div className="admin-active-sessions-section">
        <div className="admin-section-heading">
          <div>
            <span className="admin-section-eyebrow">SECURITY & FLEET</span>
            <h2>Active administrative sessions</h2>
            <p>Only currently active devices are displayed.</p>
          </div>

          <span className="admin-session-count">
            {activeSessions.length} of 2 active
          </span>
        </div>

        {activeSessions.length === 0 ? (
          <div className="admin-empty-session-card">
            <div className="admin-empty-session-icon">✓</div>
            <div>
              <strong>No active sessions</strong>
              <p>There are currently no active administrative devices.</p>
            </div>
          </div>
        ) : (
          <div className="admin-session-grid">
            {activeSessions.map((session) => {
              const isCurrentDevice = Boolean(session.isCurrent || session.isCurrentDevice);
              const lastActiveText = formatAdminLastActive(session.lastActivityAt || session.lastSeenAt);
              const createdAtText = formatAdminDateTime(session.createdAt);
              const isLoggingOut = loggingOutSessionId === session.id;

              return (
                <article
                  className={`admin-session-card ${
                    isCurrentDevice ? "is-current" : ""
                  }`}
                  key={session.id}
                >
                  <div className="admin-session-card-top">
                    <div className="admin-device-icon">
                      {session.deviceType === "mobile" ? "📱" : "🖥"}
                    </div>

                    <div className="admin-session-title">
                      <h3>
                        {session.operatingSystem || "Windows"} · {session.browser || "Browser"}
                      </h3>

                      <p>{session.partnerName || currentAdmin?.fullName || "Ethos Partner"}</p>
                    </div>

                    {isCurrentDevice && (
                      <span className="admin-current-badge">This device</span>
                    )}
                  </div>

                  <div className="admin-session-status-row">
                    <span className="admin-online-dot" />
                    <span>Active now</span>
                  </div>

                  <div className="admin-session-details">
                    <div>
                      <span>Last active</span>
                      <strong>{lastActiveText || "Just now"}</strong>
                    </div>

                    <div>
                      <span>Signed in</span>
                      <strong>{createdAtText}</strong>
                    </div>
                  </div>

                  <div className="admin-session-card-footer">
                    {isCurrentDevice ? (
                      <span className="admin-current-session-label">
                        Current session
                      </span>
                    ) : (
                      <button
                        type="button"
                        className="admin-logout-device-button"
                        onClick={() => handleLogoutDevice(session.id)}
                        disabled={isLoggingOut}
                      >
                        {isLoggingOut ? "Logging out..." : "Log out this device"}
                      </button>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
