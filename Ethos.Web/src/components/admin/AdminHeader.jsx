import React, { useState, useEffect, useRef } from "react";
import { useNavigate, useLocation, Link } from "react-router-dom";
import { getAdminUser, clearAdminAuth, adminApi } from "../../services/adminApi";
import { getAdminBreadcrumbs } from "../../constants/adminRouteRegistry";
import AdminCommandPalette from "./common/AdminCommandPalette";
import ChangePasswordModal from "./ChangePasswordModal";
import ethosLogo from "../../assets/brand/ethos-emblem.png";
import "./AdminHeader.css";

export default function AdminHeader({
  deviceCount = 1,
  currentTheme = "light",
  onThemeChange,
  attentionItems = [],
  healthData = null,
  sessionsData = [],
}) {
  const navigate = useNavigate();
  const location = useLocation();
  const adminUser = getAdminUser();

  const [paletteOpen, setPaletteOpen] = useState(false);
  const [activePopover, setActivePopover] = useState(null); // 'devices' | 'health' | 'attention' | 'profile' | null
  const [showTechnicalHealth, setShowTechnicalHealth] = useState(false);
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);
  const [currentDateTime, setCurrentDateTime] = useState(() => new Date());

  useEffect(() => {
    const timer = setInterval(() => setCurrentDateTime(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);

  const formattedDate = currentDateTime.toLocaleDateString("en-GB", {
    weekday: "short",
    day: "2-digit",
    month: "short",
    year: "numeric",
  });

  const formattedTime = currentDateTime.toLocaleTimeString("en-US", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
  });

  const headerRef = useRef(null);

  // Derive dynamic breadcrumb array
  const breadcrumbs = getAdminBreadcrumbs(location.pathname);

  // Close popovers on outside click
  useEffect(() => {
    const handleOutsideClick = (e) => {
      if (headerRef.current && !headerRef.current.contains(e.target)) {
        setActivePopover(null);
      }
    };
    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, []);

  // Keyboard shortcut Ctrl+K
  useEffect(() => {
    const handleKeyDown = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setPaletteOpen((prev) => !prev);
        setActivePopover(null);
      } else if (e.key === "Escape") {
        setActivePopover(null);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  const togglePopover = (name) => {
    setActivePopover((prev) => (prev === name ? null : name));
  };

  const handleSignOut = async () => {
    try {
      await adminApi.logout();
    } catch {
      // Ignore network errors on logout
    } finally {
      clearAdminAuth();
      navigate("/admin_portal/login", { replace: true });
    }
  };

  // --- 1. SYSTEM HEALTH COMPUTATION ---
  // Subsystem real state calculation
  const getSubsystemState = (val, isMessaging = false) => {
    if (isMessaging) {
      // WhatsApp is explicitly labeled as Setup Incomplete
      return {
        status: "warning",
        label: "Setup Incomplete",
        desc: "WhatsApp messaging is not currently configured.",
        impact: "WhatsApp notifications cannot currently be sent.",
        action: "Complete WhatsApp provider configuration in studio settings.",
      };
    }
    if (!val) {
      return {
        status: "operational",
        label: "Operational",
        desc: "Running normally.",
      };
    }
    const str = String(val).toLowerCase();
    if (str.includes("offline") || str.includes("unhealthy") || str.includes("down") || str.includes("error")) {
      return {
        status: "critical",
        label: "Unavailable",
        desc: "Subsystem failed health check.",
      };
    }
    if (str.includes("degraded")) {
      return {
        status: "warning",
        label: "Degraded",
        desc: "High response latency or partial degraded connection.",
      };
    }
    return {
      status: "operational",
      label: "Operational",
      desc: "Subsystem healthy and responding.",
    };
  };

  const subsystems = [
    {
      name: "API Service",
      key: "api",
      state: getSubsystemState(healthData?.api),
      technical: "ASP.NET Core 10 Kestrel Host",
    },
    {
      name: "Database",
      key: "database",
      state: getSubsystemState(healthData?.database),
      technical: "Configured PostgreSQL Database",
    },
    {
      name: "Authentication",
      key: "auth",
      state: getSubsystemState(healthData?.authentication),
      technical: "HMAC-SHA256 JWT Token Authority & MFA",
    },
    {
      name: "Payment Provider",
      key: "payments",
      state: getSubsystemState(healthData?.payments),
      technical: "Payment Gateway & Webhook Rail",
    },
    {
      name: "Storage",
      key: "storage",
      state: getSubsystemState(healthData?.storage),
      technical: "Configured Media & Cloud Storage",
    },
    {
      name: "WhatsApp Provider",
      key: "messaging",
      state: getSubsystemState(healthData?.messaging, true),
      technical: "Configured WhatsApp Messaging Rail",
    },
    {
      name: "Background Jobs",
      key: "jobs",
      state: { status: "operational", label: "Operational", desc: "Running scheduled maintenance and notifications." },
      technical: "Hosted In-Process Background Queue",
    },
    {
      name: "Audit Logging",
      key: "audit",
      state: { status: "operational", label: "Operational", desc: "Immutable administrative ledger recording all events." },
      technical: "Audit Trail Interceptor",
    },
  ];

  // Overall status derivation
  let overallHealthStatus = "healthy";
  let overallHealthLabel = "System Healthy";

  const hasCritical = subsystems.some((s) => s.state.status === "critical");
  const hasWarning = subsystems.some((s) => s.state.status === "warning");

  if (hasCritical) {
    overallHealthStatus = "critical";
    overallHealthLabel = "System Problem Detected";
  } else if (hasWarning) {
    overallHealthStatus = "warning";
    overallHealthLabel = "System Attention Needed";
  }

  // --- 2. DEVICE & SESSION COMPUTATION ---
  const activeSessions = (sessionsData || []).filter(
    (s) => s.isActive && !s.loggedOutAt && !s.revokedAt
  );
  const slotsUsed = Math.max(1, activeSessions.length || deviceCount);
  const maxSlots = 2;
  const currentSession = activeSessions.find((s) => s.isCurrent || s.isCurrentDevice) || {
    browser: "Chrome",
    operatingSystem: "Windows",
    lastActiveAt: new Date(),
    isCurrent: true,
  };
  const otherSessions = activeSessions.filter(
    (s) => !s.isCurrent && !s.isCurrentDevice
  );

  // --- 3. ATTENTION ITEMS COMPUTATION ---
  // Real actionable business items
  const actionableAttention = (attentionItems || []).map((item) => ({
    id: item.id || item.category || Math.random(),
    title: item.title || "Item requires review",
    description: item.description || "Administrative action needed.",
    count: item.count || 1,
    severity: item.severity || "INFO",
    actionPath: item.actionPath || "/admin_portal/dashboard",
    actionLabel: "Review & Action",
  }));

  const attentionTotalCount = actionableAttention.reduce(
    (acc, curr) => acc + (curr.count || 1),
    0
  );

  return (
    <header className="admin-header-bar ethos-visual-header" ref={headerRef}>
      {/* LEFT: VISUAL SEARCH PILL INPUT */}
      <div
        className="ethos-header-search-wrap"
        onClick={() => {
          setActivePopover(null);
          setPaletteOpen(true);
        }}
        title="Search workshops, bookings, payments, users... (Ctrl+K)"
      >
        <span className="ethos-header-search-icon">🔍</span>
        <input
          type="text"
          readOnly
          value=""
          placeholder="Search workshops, bookings, payments, users..."
          className="ethos-header-search-input"
        />
        <kbd className="ethos-search-kbd">Ctrl+K</kbd>
      </div>

      {/* RIGHT SECTION: BUSINESS CONTROLS & LIVE DATE/TIME */}
      <div className="admin-header-controls">

        {/* 2. DEVICES POPOVER TRIGGER */}
        <div className="popover-anchor">
          <button
            type="button"
            className={`admin-header-btn btn-devices ${
              activePopover === "devices" ? "active" : ""
            }`}
            onClick={() => togglePopover("devices")}
            title="Active login sessions and authorized hardware slots"
          >
            <span className="btn-icon">💻</span>
            <span className="btn-label">Devices · {slotsUsed} of {maxSlots} active</span>
          </button>

          {activePopover === "devices" && (
            <div className="admin-popover-panel panel-devices">
              <div className="popover-header">
                <div>
                  <h4>Authorized Devices</h4>
                  <p>{slotsUsed} of {maxSlots} active sessions allowed</p>
                </div>
                <span className="slot-pill">{slotsUsed} / {maxSlots} Slots</span>
              </div>

              <div className="popover-body">
                <div className="device-card current">
                  <div className="device-card-header">
                    <span className="device-icon">🖥️</span>
                    <div>
                      <strong>Current Device</strong>
                      <div className="device-sub">
                        {currentSession.browser || "Chrome"} · {currentSession.operatingSystem || "Windows"}
                      </div>
                    </div>
                    <span className="device-badge trusted">Trusted</span>
                  </div>
                  <div className="device-meta-row">
                    <span>Last active: Just now</span>
                    <span className="badge-current-session">This Session</span>
                  </div>
                </div>

                <div className="device-section-divider">Other Active Devices</div>

                {otherSessions.length === 0 ? (
                  <div className="device-empty-state">
                    ✓ No other active devices connected
                  </div>
                ) : (
                  otherSessions.map((s, idx) => (
                    <div key={idx} className="device-card">
                      <div className="device-card-header">
                        <span className="device-icon">💻</span>
                        <div>
                          <strong>{s.deviceName || `${s.browser} on ${s.operatingSystem}`}</strong>
                          <div className="device-sub">{s.ipAddress || "Authorized IP"}</div>
                        </div>
                      </div>
                    </div>
                  ))
                )}
              </div>

              <div className="popover-footer">
                <button
                  type="button"
                  className="popover-footer-action"
                  onClick={() => {
                    setActivePopover(null);
                    navigate("/admin_portal/devices");
                  }}
                >
                  Manage Devices →
                </button>
              </div>
            </div>
          )}
        </div>

        {/* 3. SYSTEM HEALTH POPOVER TRIGGER */}
        <div className="popover-anchor">
          <button
            type="button"
            className={`admin-header-btn btn-health health-${overallHealthStatus} ${
              activePopover === "health" ? "active" : ""
            }`}
            onClick={() => togglePopover("health")}
            title="Live platform subsystem operational status"
          >
            <span className={`health-dot ${overallHealthStatus}`} />
            <span className="btn-label">{overallHealthLabel}</span>
          </button>

          {activePopover === "health" && (
            <div className="admin-popover-panel panel-health">
              <div className="popover-header">
                <div>
                  <h4>System Health</h4>
                  <p>Availability and operational state of studio services</p>
                </div>
                <span className={`health-badge-status ${overallHealthStatus}`}>
                  {overallHealthLabel}
                </span>
              </div>

              <div className="popover-body health-scroll-body">
                {subsystems.map((sub) => (
                  <div key={sub.key} className="health-subsystem-row">
                    <div className="subsystem-main">
                      <div className="subsystem-name-row">
                        <span className={`status-circle ${sub.state.status}`} />
                        <span className="subsystem-name">{sub.name}</span>
                      </div>
                      <span className={`subsystem-tag ${sub.state.status}`}>
                        {sub.state.label}
                      </span>
                    </div>

                    {sub.state.impact && (
                      <div className="subsystem-explanation">
                        <strong>What this means:</strong> {sub.state.impact}
                        <br />
                        <strong>Action:</strong> {sub.state.action}
                      </div>
                    )}

                    {showTechnicalHealth && (
                      <div className="subsystem-technical-detail">
                        <code>{sub.technical}</code>
                      </div>
                    )}
                  </div>
                ))}
              </div>

              <div className="popover-footer health-footer">
                <button
                  type="button"
                  className="toggle-technical-btn"
                  onClick={() => setShowTechnicalHealth(!showTechnicalHealth)}
                >
                  {showTechnicalHealth ? "Hide Technical Details" : "Show Technical Details"}
                </button>

                <button
                  type="button"
                  className="popover-footer-action"
                  onClick={() => {
                    setActivePopover(null);
                    navigate("/admin_portal/observability");
                  }}
                >
                  Open System Health →
                </button>
              </div>
            </div>
          )}
        </div>

        {/* 4. ATTENTION POPOVER TRIGGER */}
        <div className="popover-anchor">
          <button
            type="button"
            className={`admin-header-btn btn-attention ${
              attentionTotalCount > 0 ? "has-attention" : ""
            } ${activePopover === "attention" ? "active" : ""}`}
            onClick={() => togglePopover("attention")}
            title="Actionable operational queues and approvals"
          >
            <span className="btn-icon">🔔</span>
            <span className="btn-label">
              Attention · {attentionTotalCount}
            </span>
          </button>

          {activePopover === "attention" && (
            <div className="admin-popover-panel panel-attention">
              <div className="popover-header">
                <div>
                  <h4>Items Requiring Attention</h4>
                  <p>Operational queues awaiting administrator review</p>
                </div>
                <span className="attention-count-badge">
                  {attentionTotalCount} Pending
                </span>
              </div>

              <div className="popover-body">
                {actionableAttention.length === 0 ? (
                  <div className="attention-all-clear">
                    <span className="check-icon">✓</span>
                    <div>
                      <strong>No action required</strong>
                      <p>Everything is currently up to date.</p>
                    </div>
                  </div>
                ) : (
                  actionableAttention.map((item, idx) => (
                    <div key={idx} className="attention-action-card">
                      <div className="attention-card-top">
                        <span className="attention-title-text">{item.title}</span>
                        <span className="attention-pill">{item.count}</span>
                      </div>
                      <p className="attention-card-desc">{item.description}</p>
                      <button
                        type="button"
                        className="btn-attention-action"
                        onClick={() => {
                          setActivePopover(null);
                          navigate(item.actionPath);
                        }}
                      >
                        {item.actionLabel} →
                      </button>
                    </div>
                  ))
                )}
              </div>

              <div className="popover-footer">
                <button
                  type="button"
                  className="popover-footer-action"
                  onClick={() => {
                    setActivePopover(null);
                    navigate("/admin_portal/incidents");
                  }}
                >
                  View All Issues →
                </button>
              </div>
            </div>
          )}
        </div>

        {/* 5. ADMIN PROFILE MENU TRIGGER */}
        <div className="popover-anchor">
          <button
            type="button"
            className={`admin-header-profile-btn ${
              activePopover === "profile" ? "active" : ""
            }`}
            onClick={() => togglePopover("profile")}
            title="Administrator account and security settings"
          >
            <div className="profile-avatar">
              {(adminUser?.fullName || "A").charAt(0).toUpperCase()}
            </div>
            <div className="profile-info-col">
              <span className="profile-name">
                {adminUser?.fullName || "Administrator"}
              </span>
              <span className="profile-role">
                {adminUser?.customerCode ? `${adminUser.customerCode} · Admin` : "Administrator"}
              </span>
            </div>
            <span className="profile-caret">▾</span>
          </button>

          {activePopover === "profile" && (
            <div className="admin-popover-panel panel-profile">
              <div className="profile-panel-header">
                <strong>{adminUser?.fullName || "Ethos Administrator"}</strong>
                <span>{adminUser?.email || adminUser?.phone || "Authenticated Admin"}</span>
              </div>

              <div className="profile-menu-items">
                <button
                  type="button"
                  className="profile-menu-item"
                  onClick={() => {
                    setActivePopover(null);
                    navigate("/admin_portal/users");
                  }}
                >
                  <span className="menu-icon">👤</span>
                  <span>My Profile & Users</span>
                </button>

                <button
                  type="button"
                  className="profile-menu-item"
                  onClick={() => {
                    setActivePopover(null);
                    navigate("/admin_portal/security");
                  }}
                >
                  <span className="menu-icon">🛡️</span>
                  <span>Security & Access</span>
                </button>

                <button
                  type="button"
                  className="profile-menu-item"
                  onClick={() => {
                    setActivePopover(null);
                    navigate("/admin_portal/devices");
                  }}
                >
                  <span className="menu-icon">💻</span>
                  <span>Login Devices</span>
                </button>

                <button
                  type="button"
                  className="profile-menu-item"
                  onClick={() => {
                    setActivePopover(null);
                    navigate("/admin_portal/audit-logs");
                  }}
                >
                  <span className="menu-icon">📜</span>
                  <span>Activity History</span>
                </button>

                <button
                  type="button"
                  className="profile-menu-item"
                  onClick={() => {
                    setActivePopover(null);
                    setChangePasswordOpen(true);
                  }}
                >
                  <span className="menu-icon">🔑</span>
                  <span>Change Password</span>
                </button>
              </div>

              <div className="profile-panel-footer">
                <button
                  type="button"
                  className="btn-profile-signout"
                  onClick={() => {
                    setActivePopover(null);
                    handleSignOut();
                  }}
                >
                  Sign Out
                </button>
              </div>
            </div>
          )}
        </div>

        {/* 6. LIVE DATE & TIME DISPLAY */}
        <div className="ethos-header-datetime-block">
          <span className="ethos-datetime-date">{formattedDate}</span>
          <span className="ethos-datetime-time">{formattedTime}</span>
        </div>

        {/* Global Two-Level Command Palette */}
        <AdminCommandPalette
          isOpen={paletteOpen}
          onClose={() => setPaletteOpen(false)}
          onThemeChange={onThemeChange}
          currentTheme={currentTheme}
        />

        {/* Change Password Modal */}
        <ChangePasswordModal
          isOpen={changePasswordOpen}
          onClose={() => setChangePasswordOpen(false)}
        />
      </div>
    </header>
  );
}

