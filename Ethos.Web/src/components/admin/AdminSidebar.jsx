import React, { useState } from "react";
import { NavLink } from "react-router-dom";
import ethosLogo from "../../assets/logo.png";
import "./AdminSidebar.css";

// 12 Primary Core items matching the Visual Design Reference exactly
const PRIMARY_NAV_ITEMS = [
  { to: "/admin_portal/dashboard", label: "Dashboard", icon: "📊" },
  { to: "/admin_portal/workshops", label: "Workshops", icon: "🎪" },
  { to: "/admin_portal/trainers", label: "Manage Trainers", icon: "🎓" },
  { to: "/admin_portal/bookings", label: "Bookings", icon: "📑" },
  { to: "/admin_portal/payments", label: "Payments", icon: "💳" },
  { to: "/admin_portal/videos", label: "Media Gallery", icon: "🎬" },
  { to: "/admin_portal/communications", label: "Messages", icon: "💬", badgeKey: "messages", defaultBadge: 3 },
  { to: "/admin_portal/users", label: "Users", icon: "👥" },
  { to: "/admin_portal/observability", label: "Reports", icon: "📈" },
  { to: "/admin_portal/audit-logs", label: "Audit Logs", icon: "📜" },
  { to: "/admin_portal/platforms", label: "System Health", icon: "🌐" },
  { to: "/admin_portal/devices", label: "Settings", icon: "⚙️" },
];

// Preserved Operational items so NO existing route is lost (Correction 2)
const SECONDARY_NAV_ITEMS = [
  { to: "/admin_portal/students", label: "Students Directory", icon: "👤" },
  { to: "/admin_portal/classes", label: "Studio Classes", icon: "🩰" },
  { to: "/admin_portal/attendance", label: "Attendance Rosters", icon: "📋" },
  { to: "/admin_portal/packages", label: "Dance Packages", icon: "📦" },
  { to: "/admin_portal/feedback", label: "Student Reviews", icon: "⭐" },
  { to: "/admin_portal/security", label: "Security & Access", icon: "🛡️" },
  { to: "/admin_portal/incidents", label: "Incidents & Problems", icon: "🚨" },
  { to: "/admin_portal/corrective-actions", label: "Corrective Actions", icon: "⚡" },
];

export default function AdminSidebar({ collapsed, onToggleCollapse, attentionCounts = {} }) {
  const [showMore, setShowMore] = useState(false);

  return (
    <aside className={`ethos-admin-sidebar ${collapsed ? "collapsed" : ""}`}>
      {/* Brand Header */}
      <div className="ethos-sidebar-header">
        <div className="ethos-sidebar-brand">
          <img
            src={ethosLogo}
            alt="Ethos Emblem"
            className="ethos-sidebar-emblem"
          />
          <div className="ethos-brand-text">
            <span className="ethos-brand-title">ETHOS</span>
            <span className="ethos-brand-subtitle">DANCE STUDIO</span>
          </div>
        </div>
        <button
          type="button"
          className="ethos-sidebar-toggle"
          onClick={onToggleCollapse}
          title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
        >
          {collapsed ? "»" : "«"}
        </button>
      </div>

      {/* Primary Navigation List */}
      <nav className="ethos-sidebar-nav">
        <ul className="ethos-nav-list">
          {PRIMARY_NAV_ITEMS.map((item) => {
            const badgeValue = item.badgeKey
              ? (attentionCounts[item.badgeKey] ?? item.defaultBadge)
              : null;

            return (
              <li key={item.to} className="ethos-nav-item">
                <NavLink
                  to={item.to}
                  className={({ isActive }) =>
                    `ethos-nav-link ${isActive ? "active" : ""}`
                  }
                  title={collapsed ? item.label : undefined}
                >
                  <span className="ethos-nav-icon">{item.icon}</span>
                  {!collapsed && <span className="ethos-nav-label">{item.label}</span>}
                  {!collapsed && badgeValue > 0 && (
                    <span className="ethos-nav-badge-pill">{badgeValue}</span>
                  )}
                </NavLink>
              </li>
            );
          })}
        </ul>

        {/* Extended Operations Drawer (Preserved routes) */}
        {!collapsed && (
          <div className="ethos-sidebar-extended-wrapper">
            <button
              type="button"
              className="ethos-sidebar-extended-toggle"
              onClick={() => setShowMore(!showMore)}
            >
              <span>{showMore ? "▾ Hide Studio Modules" : "▸ More Studio Modules"}</span>
              <span className="ethos-extended-count">{SECONDARY_NAV_ITEMS.length}</span>
            </button>

            {showMore && (
              <ul className="ethos-nav-list ethos-secondary-list">
                {SECONDARY_NAV_ITEMS.map((item) => (
                  <li key={item.to} className="ethos-nav-item">
                    <NavLink
                      to={item.to}
                      className={({ isActive }) =>
                        `ethos-nav-link secondary ${isActive ? "active" : ""}`
                      }
                    >
                      <span className="ethos-nav-icon">{item.icon}</span>
                      <span className="ethos-nav-label">{item.label}</span>
                    </NavLink>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </nav>

      {/* Signature Footer */}
      <div className="ethos-sidebar-footer">
        {!collapsed ? (
          <div className="ethos-sidebar-signature">
            <span className="sig-line-1">More Than Dance,</span>
            <span className="sig-line-2">A Community</span>
          </div>
        ) : (
          <div className="ethos-sidebar-signature-dot" title="More Than Dance, A Community">
            ✦
          </div>
        )}
      </div>
    </aside>
  );
}
