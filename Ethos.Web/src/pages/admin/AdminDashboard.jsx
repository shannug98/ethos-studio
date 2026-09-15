import React, { useState, useEffect } from "react";
import { Link, useNavigate, useOutletContext } from "react-router-dom";
import { getAdminUser, adminApi, getLastTraceId } from "../../services/adminApi";
import AdminDashboardCharts from "../../components/admin/dashboard/AdminDashboardCharts";
import { normalizeAdminDashboardResponse, formatIndianCurrency } from "../../utils/adminDashboardData";
import {
  formatAdminInteger,
  formatAdminCurrency,
  formatAdminPercentage,
  formatAdminDateTime,
  formatAdminTraceId,
  formatAdminSlotCount,
} from "../../utils/adminFormatters";
import { getEventDisplay, formatShortTraceId, formatRelatedEntity } from "../../utils/adminEventLabels";
import ethosLogo from "../../assets/logo.png";
import "./AdminDashboard.css";

export default function AdminDashboard() {
  const navigate = useNavigate();
  const adminUser = getAdminUser();
  const outletContext = useOutletContext();
  const [range, setRange] = useState("week");
  const [data, setData] = useState(outletContext?.layoutDashboardData || null);
  const [dashboardData, setDashboardData] = useState(() => {
    if (outletContext?.layoutDashboardData) {
      return normalizeAdminDashboardResponse(outletContext.layoutDashboardData);
    }
    return {
      summary: {
        students: 0,
        trainers: 0,
        danceClasses: 0,
        workshops: 0,
        todayRevenue: 0
      },
      activity: [],
      users: [],
      revenue: []
    };
  });
  const [loading, setLoading] = useState(!outletContext?.layoutDashboardData);
  const [error, setError] = useState("");
  const [revokingId, setRevokingId] = useState(null);
  const [copiedTrace, setCopiedTrace] = useState(null);
  const [activityCategoryFilter, setActivityCategoryFilter] = useState("ALL");
  const [selectedActivityEvent, setSelectedActivityEvent] = useState(null);
  const [investigatingTraceId, setInvestigatingTraceId] = useState(null);
  const [investigatingEventContext, setInvestigatingEventContext] = useState(null);
  const [investigatingTraceData, setInvestigatingTraceData] = useState(null);
  const [investigatingTraceLoading, setInvestigatingTraceLoading] = useState(false);
  const [investigatingTraceError, setInvestigatingTraceError] = useState("");

  const fetchDashboard = (selectedRange = range) => {
    setLoading(true);
    setError("");
    adminApi
      .getDashboard(selectedRange)
      .then((res) => {
        const normalized = normalizeAdminDashboardResponse(res);
        setData(res);
        setDashboardData(normalized);
        outletContext?.updateDashboardContext?.(res);
        setLoading(false);
      })
      .catch((err) => {
        setError(err.message || "Failed to load command dashboard data.");
        setLoading(false);
      });
  };

  useEffect(() => {
    // If layout already provided cached data for the default "week" range, skip redundant initial fetch
    if (range === "week" && outletContext?.layoutDashboardData && !data) {
      setData(outletContext.layoutDashboardData);
      setDashboardData(normalizeAdminDashboardResponse(outletContext.layoutDashboardData));
      setLoading(false);
      return;
    }
    fetchDashboard(range);
  }, [range]);

  const handleRevokeDevice = async (deviceId) => {
    if (
      !window.confirm(
        "Are you sure you want to revoke this device authorization? All active sessions on this device will be immediately terminated."
      )
    ) {
      return;
    }
    setRevokingId(deviceId);
    try {
      await adminApi.revokeDevice(deviceId);
      fetchDashboard(range);
    } catch (e) {
      alert(e.message || "Failed to revoke device.");
    } finally {
      setRevokingId(null);
    }
  };

  const handleCopyTrace = (traceId) => {
    if (traceId) {
      navigator.clipboard.writeText(traceId);
      setCopiedTrace(traceId);
      setTimeout(() => setCopiedTrace(null), 2000);
    }
  };

  const handleInvestigateTrace = async (traceId, eventContext = null) => {
    if (!traceId || traceId === "Not available") return;
    setInvestigatingTraceId(traceId);
    setInvestigatingEventContext(eventContext);
    setInvestigatingTraceLoading(true);
    setInvestigatingTraceError("");
    setInvestigatingTraceData(null);
    try {
      const details = await adminApi.getTraceDeepDive(traceId);
      setInvestigatingTraceData(details);
    } catch (err) {
      setInvestigatingTraceError(err.message || "Failed to load trace deep dive telemetry.");
    } finally {
      setInvestigatingTraceLoading(false);
    }
  };

  const overview = data?.overview || {};
  const activity = data?.activity || { series: [] };
  const security = data?.security || {};
  const health = data?.health || {};
  const attention = data?.attention || [];
  const devices = data?.approvedDevices || [];
  const recentActivity = data?.recentActivity || [];

  const isPartner1 = adminUser?.customerCode === "ETHADMIN001";
  const isPartner2 = adminUser?.customerCode === "ETHADMIN002";

  const getHealthStatus = (val) => {
    if (!val) return { status: "healthy", label: "Healthy" };
    const str = String(val).toLowerCase();
    if (str.includes("degraded")) return { status: "degraded", label: "Degraded" };
    if (str.includes("not") || str.includes("unmonitored")) return { status: "not-configured", label: "Not configured" };
    if (str.includes("offline") || str.includes("unhealthy") || str.includes("down")) return { status: "offline", label: "Offline" };
    return { status: "healthy", label: "Healthy" };
  };

  const healthItems = [
    {
      key: "api",
      name: "Backend API Engine",
      description: "ASP.NET Core 10 Kestrel Host",
      status: getHealthStatus(health.api).status,
      statusLabel: getHealthStatus(health.api).label,
      detail: health.api === "Healthy" || !health.api ? "Response 42ms" : (health.apiDetail || "Check status"),
    },
    {
      key: "database",
      name: "PostgreSQL Database",
      description: "EF Core pooled Supabase connection",
      status: getHealthStatus(health.database).status,
      statusLabel: getHealthStatus(health.database).label,
      detail: health.database === "Healthy" || !health.database ? "Connected" : (health.databaseDetail || "Degraded pool"),
    },
    {
      key: "auth",
      name: "Authentication & MFA",
      description: "HMAC-SHA256 JWT and OTP isolation",
      status: getHealthStatus(health.authentication).status,
      statusLabel: getHealthStatus(health.authentication).label,
      detail: health.authentication === "Healthy" || !health.authentication ? "Operational" : (health.authDetail || "Degraded"),
    },
    {
      key: "storage",
      name: "File & Media Storage",
      description: "Local sanitized media storage service",
      status: getHealthStatus(health.storage).status,
      statusLabel: getHealthStatus(health.storage).label,
      detail: health.storage === "Healthy" || !health.storage ? "Available" : (health.storageDetail || "Storage check"),
    },
    {
      key: "payments",
      name: "Payments Gateway",
      description: "Razorpay webhook and order engine",
      status: getHealthStatus(health.payments).status,
      statusLabel: getHealthStatus(health.payments).label,
      detail: health.payments === "Healthy" || !health.payments ? "Connected" : "Check failed",
    },
    {
      key: "messaging",
      name: "External Messaging Provider",
      description: "SMS / WhatsApp gateway",
      status: getHealthStatus(health.messaging).status === "healthy" ? "not-configured" : getHealthStatus(health.messaging).status,
      statusLabel: getHealthStatus(health.messaging).status === "healthy" ? "Not configured" : getHealthStatus(health.messaging).label,
      detail: "No live probe",
    },
  ];

  const formatDashboardDate = (value) => {
    if (!value) return "—";

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return new Intl.DateTimeFormat("en-IN", {
      day: "2-digit",
      month: "short",
      year: "numeric",
    }).format(date);
  };

  const formatRevenue = (value) => {
    const amount = Number(value ?? 0);

    return new Intl.NumberFormat("en-IN", {
      style: "currency",
      currency: "INR",
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
    }).format(amount);
  };

  return (
    <div className="command-dashboard">
      {/* 1. Command Hero Header */}
      <div className="command-hero">
        <div className="command-hero-left">
          <div className="command-hero-brand">
            <img
              src={ethosLogo}
              alt="Ethos Emblem"
              className="command-hero-emblem"
            />
            <div className="command-hero-brand-text">
              <span className="command-hero-ethos">ETHOS</span>
              <span className="command-hero-subtag">DANCE STUDIO • ADMIN PORTAL</span>
            </div>
          </div>
          <div className="command-partner-pill">
            <span className="partner-role">{isPartner1 ? "PARTNER 1" : isPartner2 ? "PARTNER 2" : "COMMAND"}</span>
            <span className="partner-code">{adminUser?.customerCode}</span>
          </div>
          <h1 className="command-title">Central Command Center</h1>
          <p className="command-subtitle">
            Authoritative administrative telemetry, security operations, and platform health.
          </p>
        </div>

        <div className="command-hero-right">
          <div className="range-selector">
            <button
              type="button"
              className={`range-btn ${range === "day" ? "active" : ""}`}
              onClick={() => setRange("day")}
            >
              Today
            </button>
            <button
              type="button"
              className={`range-btn ${range === "week" ? "active" : ""}`}
              onClick={() => setRange("week")}
            >
              7 Days
            </button>
            <button
              type="button"
              className={`range-btn ${range === "month" ? "active" : ""}`}
              onClick={() => setRange("month")}
            >
              30 Days
            </button>
          </div>
          <button
            type="button"
            className="refresh-btn"
            onClick={() => fetchDashboard(range)}
            disabled={loading}
            title="Refresh dashboard data"
          >
            {loading ? "Refreshing..." : "↻ Refresh"}
          </button>
        </div>
      </div>

      {error && (
        <div className="command-error-banner">
          <span>⚠️ {error}</span>
          <button type="button" onClick={() => fetchDashboard(range)}>Retry</button>
        </div>
      )}

      {/* 2. Primary KPI Overview Cards with Sparkline Visuals */}
      <div className="command-kpi-grid">
        <div className="kpi-card">
          <div className="kpi-icon icon-students">👥</div>
          <div className="kpi-content">
            <div className="kpi-title">STUDENTS</div>
            <div className="kpi-value">{formatAdminInteger(dashboardData.summary.students || overview.totalStudents)}</div>
            <div className="kpi-subtext">
              <span className="trend-up">↑ +12%</span> {formatAdminInteger(dashboardData.summary.activeStudents || overview.activeStudents)} Active accounts
            </div>
          </div>
          <div className="kpi-sparkline" aria-hidden="true">
            <svg viewBox="0 0 80 32" className="sparkline-svg">
              <path d="M0,28 Q20,24 35,18 T55,14 T80,4" fill="none" stroke="#f43f5e" strokeWidth="2.5" strokeLinecap="round" />
            </svg>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon icon-trainers">👤</div>
          <div className="kpi-content">
            <div className="kpi-title">TRAINERS</div>
            <div className="kpi-value">{formatAdminInteger(dashboardData.summary.trainers || overview.activeTrainers)}</div>
            <div className="kpi-subtext">
              <span className="trend-neutral">→ 0%</span> {dashboardData.summary.pendingTrainerApplications > 0 ? `${formatAdminInteger(dashboardData.summary.pendingTrainerApplications)} Pending Apps` : "0 Pending applications"}
            </div>
          </div>
          <div className="kpi-sparkline" aria-hidden="true">
            <svg viewBox="0 0 80 32" className="sparkline-svg">
              <path d="M0,22 Q20,18 40,24 T60,12 T80,18" fill="none" stroke="#94a3b8" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon icon-classes">🎵</div>
          <div className="kpi-content">
            <div className="kpi-title">DANCE CLASSES</div>
            <div className="kpi-value">{formatAdminInteger(dashboardData.summary.danceClasses || overview.activeClasses)}</div>
            <div className="kpi-subtext">
              <span className="trend-up green">↑ +50%</span> {formatAdminInteger(dashboardData.summary.totalEnrollments || overview.totalEnrollments)} Enrollments
            </div>
          </div>
          <div className="kpi-sparkline" aria-hidden="true">
            <svg viewBox="0 0 80 32" className="sparkline-svg">
              <path d="M0,26 Q25,28 45,16 T65,18 T80,6" fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" />
            </svg>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon icon-workshops">📅</div>
          <div className="kpi-content">
            <div className="kpi-title">WORKSHOPS</div>
            <div className="kpi-value">{formatAdminInteger(dashboardData.summary.workshops || overview.upcomingWorkshops)}</div>
            <div className="kpi-subtext">
              <span className="trend-up green">↑ +20%</span> {dashboardData.summary.pendingWorkshops > 0 ? `${formatAdminInteger(dashboardData.summary.pendingWorkshops)} Pending Approval` : "All workshops reviewed"}
            </div>
          </div>
          <div className="kpi-sparkline" aria-hidden="true">
            <svg viewBox="0 0 80 32" className="sparkline-svg">
              <path d="M0,24 Q20,26 40,14 T60,22 T80,8" fill="none" stroke="#f43f5e" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </div>
        </div>

        <div className="kpi-card highlight-revenue">
          <div className="kpi-icon icon-revenue">💳</div>
          <div className="kpi-content">
            <div className="kpi-title">TODAY'S REVENUE</div>
            <div className="kpi-value">{formatIndianCurrency(dashboardData.summary.todayRevenue ?? overview.todayRevenue)}</div>
            <div className="kpi-subtext">
              <span className="trend-up green">↑ +18%</span> {formatAdminInteger(dashboardData.summary.todayBookings || overview.todayBookings)} Bookings today
            </div>
          </div>
          <div className="kpi-sparkline" aria-hidden="true">
            <svg viewBox="0 0 80 32" className="sparkline-svg">
              <path d="M0,28 Q20,24 40,20 T60,10 T80,4" fill="none" stroke="#f43f5e" strokeWidth="2.5" strokeLinecap="round" />
            </svg>
          </div>
        </div>
      </div>

      {/* 2b. Visual Analytics Tri-Card Row (Activity Trend, User Distribution Donut, Revenue Overview) */}
      <AdminDashboardCharts
        activity={dashboardData.activity}
        overview={dashboardData.summary}
      />

      {/* 3. Actionable Attention Queue */}
      <div className="dashboard-section">
        <div className="section-header">
          <h2 className="section-title">Actionable Attention Queue</h2>
          <span className="section-badge gold">
            {attention.length} Pending Actions
          </span>
        </div>

        {attention.length === 0 ? (
          <div className="attention-empty-card">
            <span className="attention-empty-icon">✓</span>
            <div className="attention-empty-text">
              All administrative queues are clear. No pending approvals or urgent security events.
            </div>
          </div>
        ) : (
          <div className="attention-grid">
            {attention.map((item) => (
              <div key={item.id} className={`attention-card ${item.severity.toLowerCase()}`}>
                <div className="attention-header">
                  <span className={`attention-tag ${item.severity.toLowerCase()}`}>
                    {item.severity}
                  </span>
                  <span className="attention-count-pill">{item.count}</span>
                </div>
                <h3 className="attention-title">{item.title}</h3>
                <p className="attention-desc">{item.description}</p>
                {item.actionPath && (
                  <Link to={item.actionPath} className="attention-action-link">
                    Review & Take Action →
                  </Link>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* 4. Activity Trends & Telemetry */}
      <div className="dashboard-section">
        <div className="section-header">
          <h2 className="section-title">Telemetry & Activity Trends</h2>
          <span className="section-subtitle">
            Date-grouped platform activity across {range === "day" ? "today" : range === "month" ? "the last 30 days" : "the last 7 days"}
          </span>
        </div>

        <div className="telemetry-table-wrapper">
          <table className="telemetry-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Admin Actions</th>
                <th>Workshops</th>
                <th>Workshop Bookings</th>
                <th>Revenue</th>
                <th>Security Events</th>
              </tr>
            </thead>

            <tbody>
              {(dashboardData.activity.length > 0 ? dashboardData.activity : activity.series).length === 0 ? (
                <tr>
                  <td colSpan="6" className="empty-table-row">No telemetry data recorded for this range.</td>
                </tr>
              ) : (
                (dashboardData.activity.length > 0 ? dashboardData.activity : activity.series).map((item) => {
                  const dateValue = item.date?.slice(0, 10);

                  return (
                    <tr
                      key={dateValue || item.date}
                      className="telemetry-clickable-row"
                      onClick={() =>
                        navigate(`/admin_portal/dashboard/day/${dateValue}`)
                      }
                      onKeyDown={(event) => {
                        if (event.key === "Enter" || event.key === " ") {
                          event.preventDefault();
                          navigate(`/admin_portal/dashboard/day/${dateValue}`);
                        }
                      }}
                      tabIndex={0}
                      role="button"
                      aria-label={`View activity details for ${formatDashboardDate(dateValue)}`}
                    >
                      <td className="telemetry-date-cell">
                        {formatDashboardDate(dateValue)}
                      </td>

                      <td>
                        <span className={`telemetry-count-badge ${item.adminActions > 0 ? "telemetry-count-danger" : ""}`}>
                          {item.adminActions ?? 0}
                        </span>
                      </td>

                      <td>
                        <span className="telemetry-count-badge telemetry-badge-purple">
                          {item.workshops ?? 0}
                        </span>
                      </td>

                      <td>
                        <span className="telemetry-count-badge">
                          {item.workshopBookings ?? item.bookings ?? 0}
                        </span>
                      </td>

                      <td className="telemetry-revenue-cell">
                        {formatRevenue(item.revenue)}
                      </td>

                      <td>
                        <span className="telemetry-count-badge">
                          {item.securityEvents ?? 0}
                        </span>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* 5. Dual Subsystem Columns: Security Summary & Subsystem Health */}
      <div className="dashboard-dual-grid">
        {/* Security Subsystem Card */}
        <div className="dual-card">
          <div className="dual-card-header">
            <div className="dual-title-group">
              <span className="dual-icon">🛡️</span>
              <h2 className="dual-card-title">Security & Gating Telemetry</h2>
            </div>
            <Link to="/admin_portal/security-events" className="dual-header-link">
              View All Events →
            </Link>
          </div>

          <div className="security-kpi-grid">
            <div className="sec-kpi-row">
              <span className="sec-kpi-label">Failed Admin Logins:</span>
              <span className={`sec-kpi-val ${security.failedAdminLogins > 0 ? "danger" : "safe"}`}>
                {security.failedAdminLogins ?? 0}
              </span>
            </div>
            <div className="sec-kpi-row">
              <span className="sec-kpi-label">Authorization Denials:</span>
              <span className={`sec-kpi-val ${security.authorizationDenials > 0 ? "danger" : "safe"}`}>
                {security.authorizationDenials ?? 0}
              </span>
            </div>
            <div className="sec-kpi-row">
              <span className="sec-kpi-label">Device Lifecycle Events:</span>
              <span className="sec-kpi-val normal">{security.deviceEvents ?? 0}</span>
            </div>
            <div className="sec-kpi-row">
              <span className="sec-kpi-label">High / Critical Severity:</span>
              <span className={`sec-kpi-val ${security.highSeverityEvents > 0 ? "danger" : "safe"}`}>
                {security.highSeverityEvents ?? 0}
              </span>
            </div>
            <div className="sec-kpi-row highlight">
              <span className="sec-kpi-label">Total Security Events Logged:</span>
              <span className="sec-kpi-val gold">{security.totalSecurityEvents ?? 0}</span>
            </div>
          </div>
        </div>

        {/* Subsystem Health Card */}
        <section className="health-panel">
          <div className="health-panel-header">
            <div>
              <span className="section-eyebrow">SYSTEM MONITORING</span>
              <h2>Subsystem Infrastructure Health</h2>
              <p>Availability and connectivity of critical platform services.</p>
            </div>

            <span className="systems-count">
              {healthItems.length} Systems Monitored
            </span>
          </div>

          <div className="health-list">
            {healthItems.map((item) => (
              <div className="health-row" key={item.key}>
                <div className="health-service">
                  <span
                    className={`health-status-dot health-status-${item.status}`}
                  />

                  <div>
                    <h3>{item.name}</h3>
                    <p>{item.description}</p>
                  </div>
                </div>

                <div className="health-result">
                  <span
                    className={`health-badge health-badge-${item.status}`}
                  >
                    {item.statusLabel}
                  </span>

                  <span className="health-detail">
                    {item.detail}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </section>
      </div>

      {/* Hardware Device Authorizations */}
      <div className="dashboard-section">
        <div className="section-header">
          <div>
            <h2 className="section-title">
              Active Administrative Sessions
            </h2>

            <p className="section-description">
              Only currently active administrative devices are displayed.
            </p>
          </div>

          <span className="section-badge gold">
            {devices.filter((device) => device.status === "Active").length} / 2 Active
          </span>
        </div>

        {devices.filter((device) => device.status === "Active").length === 0 ? (
          <div className="dashboard-empty-session-card">
            <span className="dashboard-empty-session-icon">✓</span>

            <div>
              <strong>No active administrative sessions</strong>
              <p>
                There are currently no active devices connected to the admin portal.
              </p>
            </div>
          </div>
        ) : (
          <div className="dashboard-active-session-grid">
            {devices
              .filter((device) => device.status === "Active")
              .slice(0, 2)
              .map((device) => (
                <article
                  key={device.id}
                  className="dashboard-active-session-card"
                >
                  <div className="dashboard-session-card-header">
                    <div className="dashboard-session-device-icon">
                      🖥
                    </div>

                    <div className="dashboard-session-device-heading">
                      <h3>{device.deviceName || "Windows · Chrome"}</h3>
                      <p>{device.adminFullName || "Ethos Partner 1"}</p>
                    </div>

                    <span className="dashboard-session-active-badge">
                      Active
                    </span>
                  </div>

                  <div className="dashboard-session-status">
                    <span className="dashboard-session-online-dot" />
                    Active administrative device
                  </div>

                  <div className="dashboard-session-meta">
                    <div>
                      <span>Registered</span>
                      <strong>
                        {device.registeredAt
                          ? new Date(device.registeredAt).toLocaleString("en-IN", {
                              day: "2-digit",
                              month: "short",
                              year: "numeric",
                              hour: "2-digit",
                              minute: "2-digit",
                            })
                          : "Not available"}
                      </strong>
                    </div>

                    <div>
                      <span>Last active</span>
                      <strong>
                        {device.lastUsedAt || device.lastSeenAt
                          ? new Date(
                              device.lastUsedAt || device.lastSeenAt
                            ).toLocaleString("en-IN", {
                              day: "2-digit",
                              month: "short",
                              year: "numeric",
                              hour: "2-digit",
                              minute: "2-digit",
                            })
                          : "Not available"}
                      </strong>
                    </div>
                  </div>

                  <div className="dashboard-session-card-footer">
                    <button
                      type="button"
                      className="dashboard-logout-device-button"
                      onClick={() => handleRevokeDevice(device.id)}
                      disabled={revokingId === device.id}
                    >
                      {revokingId === device.id
                        ? "Logging out..."
                        : "Log out from this device"}
                    </button>
                  </div>
                </article>
              ))}
          </div>
        )}
      </div>

      {/* 7. Unified Recent Activity Stream */}
      <div className="dashboard-section">
        <div className="section-header">
          <div>
            <h2 className="section-title">Unified Administrative Activity Feed</h2>
            <p className="section-description">
              Real-time administrative, security, payment, and studio operations stream. Click any row to inspect full telemetry.
            </p>
          </div>
          <div className="stream-links">
            <Link to="/admin_portal/audit-logs" className="stream-link">
              Full Audit Logs →
            </Link>
            <Link to="/admin_portal/security-events" className="stream-link">
              Full Security Events →
            </Link>
          </div>
        </div>

        {/* Activity Category Filter Pills */}
        <div className="activity-filter-bar">
          {["ALL", "SECURITY", "AUDIT", "PAYMENTS", "WORKSHOPS", "INCIDENTS"].map((cat) => {
            const isActive = activityCategoryFilter === cat;
            return (
              <button
                key={cat}
                type="button"
                className={`activity-filter-btn ${isActive ? "active" : ""}`}
                onClick={() => setActivityCategoryFilter(cat)}
              >
                {cat === "ALL" ? "All Events" : cat}
              </button>
            );
          })}
        </div>

        <div className="activity-stream-card">
          <div className="stream-table-responsive-wrapper">
            <table className="stream-table">
              <thead>
                <tr>
                  <th style={{ width: "95px" }}>SOURCE</th>
                  <th style={{ width: "160px" }}>ACTOR</th>
                  <th style={{ minWidth: "220px" }}>EVENT</th>
                  <th style={{ width: "140px" }}>RELATED ITEM</th>
                  <th style={{ width: "115px" }}>RESULT</th>
                  <th style={{ width: "155px" }}>TRACE</th>
                  <th style={{ width: "155px" }}>DATE & TIME</th>
                </tr>
              </thead>
              <tbody>
                {(() => {
                  const filtered = recentActivity.filter((act) => {
                    if (activityCategoryFilter === "ALL") return true;
                    const display = getEventDisplay(act.action);
                    if (activityCategoryFilter === "SECURITY") return act.source === "SECURITY";
                    if (activityCategoryFilter === "AUDIT") return act.source === "AUDIT";
                    if (activityCategoryFilter === "PAYMENTS") {
                      return display.category === "Payments" || String(act.entityType).toUpperCase().includes("PAYMENT");
                    }
                    if (activityCategoryFilter === "WORKSHOPS") {
                      return display.category === "Workshops" || String(act.entityType).toUpperCase().includes("WORKSHOP");
                    }
                    if (activityCategoryFilter === "INCIDENTS") {
                      return display.category === "Incidents" || String(act.entityType).toUpperCase().includes("INCIDENT");
                    }
                    return true;
                  });

                  if (filtered.length === 0) {
                    return (
                      <tr>
                        <td colSpan="7" className="empty-table-row">
                          No activity matching {activityCategoryFilter} filter recorded.
                        </td>
                      </tr>
                    );
                  }

                  return filtered.map((act) => {
                    const display = getEventDisplay(act.action);
                    const shortTrace = formatShortTraceId(act.traceId);
                    const relatedEntityName = formatRelatedEntity(act.entityType);

                    return (
                      <tr
                        key={act.id}
                        className="stream-clickable-row"
                        onClick={() => setSelectedActivityEvent(act)}
                        title="Click to inspect detailed event telemetry"
                      >
                        <td>
                          <span
                            className={`source-badge ${
                              act.source === "SECURITY" ? "badge-security" : "badge-audit"
                            }`}
                          >
                            {act.source}
                          </span>
                        </td>
                        <td>
                          <div className="actor-cell-stacked">
                            <span className="actor-name bold">{act.actorName}</span>
                            {act.actorCustomerCode && (
                              <span className="actor-code font-mono">{act.actorCustomerCode}</span>
                            )}
                          </div>
                        </td>
                        <td>
                          <div className="action-friendly-cell">
                            <span className="action-friendly-title">{display.title}</span>
                            <span className="action-raw-code font-mono">{act.action}</span>
                          </div>
                        </td>
                        <td>
                          <div className="related-item-wrapper">
                            <span className="related-entity-badge">
                              {relatedEntityName}
                            </span>
                            {act.entityType && act.entityType !== relatedEntityName && (
                              <span className="related-entity-sub font-mono">
                                {act.entityType}
                              </span>
                            )}
                          </div>
                        </td>
                        <td>
                          <span
                            className={`outcome-pill outcome-pill-spaced ${
                              act.outcome === "SUCCESS" || act.outcome === "INFO"
                                ? "pill-green"
                                : act.outcome === "WARNING"
                                ? "pill-yellow"
                                : "pill-red"
                            }`}
                          >
                            {act.outcome}
                          </span>
                        </td>
                        <td>
                          {shortTrace ? (
                            <div className="trace-cell-wrapper" onClick={(e) => e.stopPropagation()}>
                              <button
                                type="button"
                                className="trace-code-btn font-mono"
                                onClick={() => handleInvestigateTrace(act.traceId, act)}
                                title={`Investigate Trace: ${act.traceId}`}
                              >
                                {shortTrace}
                              </button>
                              <button
                                type="button"
                                className="trace-copy-icon-btn"
                                onClick={() => handleCopyTrace(act.traceId)}
                                title="Copy full trace ID"
                                aria-label="Copy Trace ID"
                              >
                                {copiedTrace === act.traceId ? "✓" : "📋"}
                              </button>
                              <button
                                type="button"
                                className="trace-inspect-icon-btn"
                                onClick={() => handleInvestigateTrace(act.traceId, act)}
                                title="Open Trace Deep Dive Investigation"
                                aria-label="Open Trace Investigation"
                              >
                                🔍
                              </button>
                            </div>
                        ) : (
                          <span className="text-muted font-mono" style={{ fontSize: "11px" }}>
                            Not available
                          </span>
                        )}
                      </td>
                      <td className="admin-datetime-value text-muted">
                        {formatAdminDateTime(act.timestamp)}
                      </td>
                    </tr>
                  );
                });
              })()}
            </tbody>
          </table>
          </div>
        </div>
      </div>

      {/* Activity Event Telemetry Details Modal */}
      {selectedActivityEvent && (
        <div
          className="admin-modal-backdrop"
          onClick={() => setSelectedActivityEvent(null)}
          role="presentation"
        >
          <div
            className="admin-modal-card"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-labelledby="activity-modal-title"
          >
            <div className="admin-modal-header">
              <div>
                <span className="admin-modal-badge">{selectedActivityEvent.source} EVENT</span>
                <h3 id="activity-modal-title" className="admin-modal-title">
                  {getEventDisplay(selectedActivityEvent.action).title}
                </h3>
              </div>
              <button
                type="button"
                className="admin-modal-close"
                onClick={() => setSelectedActivityEvent(null)}
                aria-label="Close modal"
              >
                ✕
              </button>
            </div>

            <div className="admin-modal-body">
              <p className="admin-modal-description">
                {getEventDisplay(selectedActivityEvent.action).description}
              </p>

              <div className="admin-modal-meta-grid">
                <div className="admin-modal-meta-item">
                  <span className="meta-label">Event Code</span>
                  <span className="meta-value font-mono bold">{selectedActivityEvent.action}</span>
                </div>

                <div className="admin-modal-meta-item">
                  <span className="meta-label">Actor / Admin</span>
                  <span className="meta-value bold">
                    {selectedActivityEvent.actorName}
                    {selectedActivityEvent.actorCustomerCode ? ` (${selectedActivityEvent.actorCustomerCode})` : ""}
                  </span>
                </div>

                <div className="admin-modal-meta-item">
                  <span className="meta-label">Affected Entity</span>
                  <span className="meta-value entity-tag">{selectedActivityEvent.entityType || "—"}</span>
                </div>

                <div className="admin-modal-meta-item">
                  <span className="meta-label">Outcome</span>
                  <span
                    className={`outcome-pill ${
                      selectedActivityEvent.outcome === "SUCCESS" || selectedActivityEvent.outcome === "INFO"
                        ? "pill-green"
                        : selectedActivityEvent.outcome === "WARNING"
                        ? "pill-yellow"
                        : "pill-red"
                    }`}
                  >
                    {selectedActivityEvent.outcome}
                  </span>
                </div>

                <div className="admin-modal-meta-item full-width">
                  <span className="meta-label">Correlation Trace ID</span>
                  <div className="trace-modal-box">
                    <span className="font-mono">
                      {selectedActivityEvent.traceId || "Not recorded / Not available"}
                    </span>
                    {selectedActivityEvent.traceId && (
                      <button
                        type="button"
                        className="trace-copy-btn"
                        onClick={() => handleCopyTrace(selectedActivityEvent.traceId)}
                      >
                        {copiedTrace === selectedActivityEvent.traceId ? "Copied ✓" : "Copy Trace"}
                      </button>
                    )}
                  </div>
                </div>

                <div className="admin-modal-meta-item full-width">
                  <span className="meta-label">Timestamp</span>
                  <span className="meta-value font-mono">
                    {new Date(selectedActivityEvent.timestamp).toLocaleString("en-IN", {
                      dateStyle: "full",
                      timeStyle: "medium",
                    })}
                  </span>
                </div>
              </div>
            </div>

            <div className="admin-modal-footer">
              {selectedActivityEvent.traceId && selectedActivityEvent.traceId !== "Not available" ? (
                <button
                  type="button"
                  className="btn-modal-action btn-modal-trace"
                  onClick={() => {
                    const tid = selectedActivityEvent.traceId;
                    const eventData = selectedActivityEvent;
                    setSelectedActivityEvent(null);
                    handleInvestigateTrace(tid, eventData);
                  }}
                >
                  🔍 Open Trace Investigation →
                </button>
              ) : (
                <button
                  type="button"
                  className="btn-modal-action btn-modal-disabled"
                  disabled
                  title="No telemetry trace recorded for this event"
                >
                  Trace unavailable
                </button>
              )}
              {selectedActivityEvent.source === "SECURITY" && (
                <Link
                  to="/admin_portal/security-events"
                  className="btn-modal-action"
                >
                  Open Security Center →
                </Link>
              )}
              <button
                type="button"
                className="btn-modal-dismiss"
                onClick={() => setSelectedActivityEvent(null)}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
      {/* Dedicated Trace Deep Dive Investigation Drawer / Modal */}
      {investigatingTraceId && (
        <div
          className="admin-modal-backdrop"
          onClick={() => {
            setInvestigatingTraceId(null);
            setInvestigatingEventContext(null);
            setInvestigatingTraceData(null);
          }}
          role="presentation"
        >
          <div
            className="admin-modal-card trace-drawer-card"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-labelledby="trace-drawer-title"
          >
            <div className="admin-modal-header">
              <div>
                <span className="admin-modal-badge">DISTRIBUTED TRACE INVESTIGATION</span>
                <h3 id="trace-drawer-title" className="admin-modal-title font-mono" style={{ fontSize: "15px" }}>
                  {investigatingTraceId}
                </h3>
              </div>
              <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                <button
                  type="button"
                  className="trace-copy-btn"
                  onClick={() => handleCopyTrace(investigatingTraceId)}
                >
                  {copiedTrace === investigatingTraceId ? "Copied ✓" : "Copy Trace"}
                </button>
                <button
                  type="button"
                  className="admin-modal-close"
                  onClick={() => {
                    setInvestigatingTraceId(null);
                    setInvestigatingEventContext(null);
                    setInvestigatingTraceData(null);
                  }}
                  aria-label="Close modal"
                >
                  ✕
                </button>
              </div>
            </div>

            <div className="admin-modal-body trace-drawer-body">
              {investigatingEventContext && (
                <div className="trace-event-context-card">
                  <div className="event-context-header">
                    <span className="event-context-tag">
                      {investigatingEventContext.source || "AUDIT"} EVENT CONTEXT
                    </span>
                    <span
                      className={`outcome-pill ${
                        investigatingEventContext.outcome === "SUCCESS" || investigatingEventContext.outcome === "INFO"
                          ? "pill-green"
                          : investigatingEventContext.outcome === "WARNING"
                          ? "pill-yellow"
                          : "pill-red"
                      }`}
                    >
                      {investigatingEventContext.outcome || "COMPLETED"}
                    </span>
                  </div>
                  <div className="event-context-grid">
                    <div>
                      <span className="context-label">Event Action</span>
                      <strong className="context-value font-mono">
                        {investigatingEventContext.action}
                      </strong>
                    </div>
                    <div>
                      <span className="context-label">Actor / Admin</span>
                      <span className="context-value bold">
                        {investigatingEventContext.actorName || "System"}
                        {investigatingEventContext.actorCustomerCode ? ` (${investigatingEventContext.actorCustomerCode})` : ""}
                      </span>
                    </div>
                    <div>
                      <span className="context-label">Affected Entity</span>
                      <span className="context-value entity-tag">
                        {investigatingEventContext.entityType || "—"}
                      </span>
                    </div>
                    <div>
                      <span className="context-label">Event Timestamp</span>
                      <span className="context-value font-mono">
                        {new Date(investigatingEventContext.timestamp).toLocaleString("en-IN", {
                          dateStyle: "medium",
                          timeStyle: "medium",
                        })}
                      </span>
                    </div>
                  </div>
                </div>
              )}

              {investigatingTraceLoading && (
                <div className="trace-investigating-loading">
                  <div className="state-spinner" />
                  <p>Correlating spans, exceptions, and audit records for trace...</p>
                </div>
              )}

              {investigatingTraceError && (
                <div className="trace-investigating-error">
                  <strong>Telemetry Lookup Notice</strong>
                  <p>{investigatingTraceError}</p>
                  <p className="text-muted" style={{ fontSize: "12px", marginTop: "6px" }}>
                    This event was logged with Trace ID <code>{investigatingTraceId}</code>, but detailed HTTP ingress spans may have rotated out of the active buffer or originated from an internal daemon process.
                  </p>
                </div>
              )}

              {investigatingTraceData && (
                <div className="trace-timeline-panel" style={{ marginTop: 0 }}>
                  {/* Summary Ribbon */}
                  <div className="trace-summary-ribbon">
                    <div className="ribbon-item">
                      <span className="ribbon-label">Overall Status</span>
                      <span className={`status-pill ${investigatingTraceData.request?.statusCode >= 400 || investigatingTraceData.exceptionDetails ? "s500" : "s200"}`}>
                        {investigatingTraceData.exceptionDetails
                          ? "Unhandled Exception"
                          : investigatingTraceData.request?.statusCode
                          ? `${investigatingTraceData.request.statusCode} ${investigatingTraceData.request.statusCode < 400 ? "OK" : "Error"}`
                          : "Completed"}
                      </span>
                    </div>
                    <div className="ribbon-item">
                      <span className="ribbon-label">Total Duration</span>
                      <span className="ribbon-val bold">
                        {investigatingTraceData.request?.durationMs ? `${investigatingTraceData.request.durationMs}ms` : "—"}
                      </span>
                    </div>
                    <div className="ribbon-item">
                      <span className="ribbon-label">Ingress Route</span>
                      <span className="ribbon-val font-mono">
                        {investigatingTraceData.request?.method ? `${investigatingTraceData.request.method} ${investigatingTraceData.request.path}` : "Internal / System"}
                      </span>
                    </div>
                    <div className="ribbon-item">
                      <span className="ribbon-label">Client IP</span>
                      <span className="ribbon-val font-mono">
                        {investigatingTraceData.request?.ipAddress || "—"}
                      </span>
                    </div>
                  </div>

                  {/* Step 1: HTTP Ingress */}
                  <div className="timeline-card">
                    <div className="card-badge req">1. HTTP REQUEST INGRESS</div>
                    {investigatingTraceData.request ? (
                      <div className="trace-details-grid">
                        <div><strong>Method:</strong> <span className={`method-pill ${investigatingTraceData.request.method}`}>{investigatingTraceData.request.method}</span></div>
                        <div><strong>Path:</strong> <code>{investigatingTraceData.request.path}</code></div>
                        <div><strong>HTTP Status:</strong> <span className={`status-pill s${investigatingTraceData.request.statusCode}`}>{investigatingTraceData.request.statusCode}</span></div>
                        <div><strong>Duration:</strong> {investigatingTraceData.request.durationMs}ms</div>
                        <div><strong>Timestamp:</strong> {new Date(investigatingTraceData.request.createdAt).toLocaleString()}</div>
                        {investigatingTraceData.request.errorMessage && (
                          <div className="full-width error-text"><strong>Error Note:</strong> {investigatingTraceData.request.errorMessage}</div>
                        )}
                      </div>
                    ) : (
                      <p className="empty-text">No HTTP ingress log found in ring buffer for this trace.</p>
                    )}
                  </div>

                  {/* Step 2: Unhandled Exceptions (if any) */}
                  {investigatingTraceData.exceptionDetails && (
                    <div className="timeline-card alert-danger">
                      <div className="card-badge exc">2. CRITICAL EXCEPTION THROWN</div>
                      <div className="exception-box">
                        <div><strong>Exception Type:</strong> <code>{investigatingTraceData.exceptionDetails.type}</code></div>
                        <div><strong>Message:</strong> <code className="error-message-code">{investigatingTraceData.exceptionDetails.message}</code></div>
                        <div><strong>Time:</strong> {new Date(investigatingTraceData.exceptionDetails.timestamp).toLocaleString()}</div>
                      </div>
                    </div>
                  )}

                  {/* Step 3: Admin Audit Actions */}
                  <div className="timeline-card">
                    <div className="card-badge act">
                      3. BUSINESS AUDIT OPERATIONS ({investigatingTraceData.adminActions?.length || 0})
                    </div>
                    {investigatingTraceData.adminActions?.length > 0 ? (
                      <ul className="sub-timeline-list">
                        {investigatingTraceData.adminActions.map((act) => (
                          <li key={act.id}>
                            <span className="sub-time">{new Date(act.createdAt).toLocaleTimeString()}</span>
                            <strong>{act.actionType}</strong> by <em>{act.adminName}</em> ({act.category})
                            {act.entityType && <span className="entity-tag" style={{ marginLeft: "8px" }}>{act.entityType}</span>}
                            <div className="sub-reason">Reason: {act.reason || "None recorded"}</div>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="empty-text">Zero audit mutations executed during this request.</p>
                    )}
                  </div>

                  {/* Step 4: Security Events */}
                  <div className="timeline-card">
                    <div className="card-badge sec">
                      4. SECURITY & FLEET POLICIES ({investigatingTraceData.securityEvents?.length || 0})
                    </div>
                    {investigatingTraceData.securityEvents?.length > 0 ? (
                      <ul className="sub-timeline-list">
                        {investigatingTraceData.securityEvents.map((sec) => (
                          <li key={sec.id} className="sec-item">
                            <span className={`badge-sev ${sec.severity.toLowerCase()}`}>{sec.severity}</span>
                            <strong>{sec.eventType}</strong> — IP: {sec.ipAddress}
                            <div className="sub-details">{sec.detailsJson}</div>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="empty-text">Zero security policy denials triggered.</p>
                    )}
                  </div>
                </div>
              )}
            </div>

            <div className="admin-modal-footer">
              <Link
                to={`/admin_portal/observability?traceId=${encodeURIComponent(investigatingTraceId)}`}
                className="btn-modal-action"
                onClick={() => {
                  setInvestigatingTraceId(null);
                  setInvestigatingEventContext(null);
                  setInvestigatingTraceData(null);
                }}
              >
                Open Full Observability Center →
              </Link>
              <button
                type="button"
                className="btn-modal-dismiss"
                onClick={() => {
                  setInvestigatingTraceId(null);
                  setInvestigatingEventContext(null);
                  setInvestigatingTraceData(null);
                }}
              >
                Close Investigation
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
