import React from "react";
import { NavLink, useNavigate } from "react-router-dom";
import {
  LayoutDashboard,
  Sparkles,
  CalendarDays,
  Users,
  FileText,
  CreditCard,
  Image,
  LogOut,
  GraduationCap,
  UserPlus,
  ClipboardCheck,
  Package,
  Star,
  MessageSquare,
  Activity,
  Shield,
  AlertTriangle,
  Wrench,
  History,
  UserCheck,
  Smartphone,
  Layers,
} from "lucide-react";
import { adminApi, clearAdminAuth, getAdminUser } from "../../services/adminApi";
import ethosLogo from "../../assets/brand/ethos-emblem.png";
import "./AdminSidebar.css";

// 7 Primary Core operational items matching the studio business operations (Visible to both Admin and Developer)
export const PRIMARY_NAV_ITEMS = [
  {
    to: "/admin_portal/dashboard",
    label: "Dashboard",
    Icon: LayoutDashboard,
    accentClass: "accent-dashboard",
  },
  {
    to: "/admin_portal/analytics",
    label: "Live Insights",
    Icon: Sparkles,
    accentClass: "accent-insights",
  },
  {
    to: "/admin_portal/workshops",
    label: "Workshops",
    Icon: CalendarDays,
    accentClass: "accent-workshops",
    badgeKey: "pendingWorkshops",
  },
  {
    to: "/admin_portal/trainers",
    label: "Manage Trainers",
    Icon: Users,
    accentClass: "accent-trainers",
    badgeKey: "pendingTrainers",
  },
  {
    to: "/admin_portal/bookings",
    label: "Bookings",
    Icon: FileText,
    accentClass: "accent-bookings",
  },
  {
    to: "/admin_portal/payments",
    label: "Payments",
    Icon: CreditCard,
    accentClass: "accent-payments",
  },
  {
    to: "/admin_portal/videos",
    label: "Media Gallery",
    Icon: Image,
    accentClass: "accent-media",
  },
];

// Additional studio community & operational items (Visible to Developer account)
export const STUDIO_NAV_ITEMS = [
  {
    to: "/admin_portal/classes",
    label: "Dance Classes",
    Icon: GraduationCap,
    accentClass: "accent-classes",
  },
  {
    to: "/admin_portal/students",
    label: "Students",
    Icon: UserPlus,
    accentClass: "accent-students",
  },
  {
    to: "/admin_portal/attendance",
    label: "Attendance",
    Icon: ClipboardCheck,
    accentClass: "accent-attendance",
  },
  {
    to: "/admin_portal/packages",
    label: "Dance Packages",
    Icon: Package,
    accentClass: "accent-packages",
  },
  {
    to: "/admin_portal/feedback",
    label: "Student Feedback",
    Icon: Star,
    accentClass: "accent-feedback",
  },
];

// System, Engineering & Governance items (Visible to Developer account)
export const DEVELOPER_SYSTEM_NAV_ITEMS = [
  {
    to: "/admin_portal/communications",
    label: "Communications",
    Icon: MessageSquare,
    accentClass: "accent-comms",
  },
  {
    to: "/admin_portal/observability",
    label: "System Monitoring",
    Icon: Activity,
    accentClass: "accent-observability",
  },
  {
    to: "/admin_portal/security",
    label: "Security & Access",
    Icon: Shield,
    accentClass: "accent-security",
    badgeKey: "security",
  },
  {
    to: "/admin_portal/incidents",
    label: "Problems & Incidents",
    Icon: AlertTriangle,
    accentClass: "accent-incidents",
    badgeKey: "incidents",
  },
  {
    to: "/admin_portal/corrective-actions",
    label: "Corrective Actions",
    Icon: Wrench,
    accentClass: "accent-corrective",
    badgeKey: "correctiveActions",
  },
  {
    to: "/admin_portal/audit-logs",
    label: "Activity History",
    Icon: History,
    accentClass: "accent-audit",
  },
  {
    to: "/admin_portal/users",
    label: "Users & Accounts",
    Icon: UserCheck,
    accentClass: "accent-users",
  },
  {
    to: "/admin_portal/devices",
    label: "Login Devices",
    Icon: Smartphone,
    accentClass: "accent-devices",
  },
  {
    to: "/admin_portal/platforms",
    label: "Connected Platforms",
    Icon: Layers,
    accentClass: "accent-platforms",
  },
];

export default function AdminSidebar({ collapsed, onToggleCollapse, attentionCounts = {} }) {
  const navigate = useNavigate();
  const adminUser = getAdminUser();
  const isDeveloper =
    adminUser?.displayRole === "Developer" ||
    adminUser?.phone === "8019013757" ||
    (Array.isArray(adminUser?.roles) && adminUser.roles.includes("DEVELOPER"));

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

  const renderNavGroup = (items, sectionTitle = null) => (
    <React.Fragment key={sectionTitle || "core"}>
      {isDeveloper && sectionTitle && (
        <li className="ethos-sidebar-section-header">
          <span className="ethos-sidebar-section-title">{sectionTitle}</span>
          <div className="ethos-sidebar-section-divider" />
        </li>
      )}
      {items.map((item) => {
        const { to, label, Icon, accentClass } = item;
        const badgeValue = item.badgeKey
          ? (Number(attentionCounts[item.badgeKey]) || 0)
          : 0;

        return (
          <li key={to} className="ethos-nav-item">
            <NavLink
              to={to}
              className={({ isActive }) =>
                `ethos-nav-link ${isActive ? "active" : ""}`
              }
              title={collapsed ? label : undefined}
            >
              <span className={`ethos-nav-icon-wrap ${accentClass}`}>
                <Icon size={18} strokeWidth={2.2} />
              </span>
              {!collapsed && <span className="ethos-nav-label">{label}</span>}
              {!collapsed && badgeValue > 0 && (
                <span className="ethos-nav-badge-pill">{badgeValue}</span>
              )}
            </NavLink>
          </li>
        );
      })}
    </React.Fragment>
  );

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

      {/* Navigation List */}
      <nav className="ethos-sidebar-nav">
        <ul className="ethos-nav-list">
          {renderNavGroup(PRIMARY_NAV_ITEMS, isDeveloper ? "Core Operations" : null)}
          {isDeveloper && renderNavGroup(STUDIO_NAV_ITEMS, "Studio & Community")}
          {isDeveloper && renderNavGroup(DEVELOPER_SYSTEM_NAV_ITEMS, "System & Engineering")}
        </ul>
      </nav>

      {/* Logout Action Button */}
      <div className="ethos-sidebar-logout-container">
        <button
          type="button"
          className="ethos-sidebar-logout-btn"
          onClick={handleSignOut}
          title={collapsed ? "Sign Out" : undefined}
        >
          <span className="ethos-logout-icon-wrap">
            <LogOut size={17} strokeWidth={2.2} />
          </span>
          {!collapsed && <span className="ethos-logout-label">Sign Out</span>}
        </button>
      </div>

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
