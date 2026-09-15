import React from "react";
import { NavLink } from "react-router-dom";
import { getNavSections } from "../../constants/adminRouteRegistry";
import ethosLogo from "../../assets/logo.png";
import "./AdminSidebar.css";

export default function AdminSidebar({ collapsed, onToggleCollapse, attentionCounts = {} }) {
  const navSections = getNavSections(attentionCounts);

  return (
    <aside className={`admin-sidebar ${collapsed ? "collapsed" : ""}`}>
      <div className="admin-sidebar-header">
        <div className="admin-brand">
          <img
            src={ethosLogo}
            alt="Ethos Emblem"
            className="admin-sidebar-emblem"
          />
          <div className="admin-brand-text-col">
            <span className="admin-brand-ethos">ETHOS</span>
            <span className="admin-brand-subtag">DANCE STUDIO</span>
          </div>
          <span className="admin-brand-badge">ADMIN</span>
        </div>
        <button
          type="button"
          className="admin-sidebar-toggle"
          onClick={onToggleCollapse}
          title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
        >
          {collapsed ? "»" : "«"}
        </button>
      </div>

      <nav className="admin-sidebar-nav">
        {navSections.map((sec) => (
          <div key={sec.title} className="admin-nav-section">
            {!collapsed && <div className="admin-nav-section-title">{sec.title}</div>}
            <ul className="admin-nav-list">
              {sec.items.map((item) => (
                <li key={item.to} className="admin-nav-item">
                  <NavLink
                    to={item.to}
                    className={({ isActive }) =>
                      `admin-nav-link ${isActive ? "active" : ""}`
                    }
                    title={collapsed ? item.label : undefined}
                  >
                    <span className="admin-nav-icon">{item.icon}</span>
                    {!collapsed && <span className="admin-nav-label">{item.label}</span>}
                    {!collapsed && item.badge > 0 && (
                      <span
                        className={`admin-nav-badge ${
                          item.badgeVariant === "danger" ? "badge-danger" : "badge-warning"
                        }`}
                      >
                        {item.badge}
                      </span>
                    )}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      <div className="admin-sidebar-footer">
        {!collapsed && (
          <div className="admin-security-status-indicator">
            <span className="admin-status-dot pulse green"></span>
            <span className="admin-status-text">Admin Access Secure</span>
          </div>
        )}
      </div>
    </aside>
  );
}
