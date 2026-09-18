import React, { useState, useEffect } from "react";
import { Outlet, Navigate, useLocation } from "react-router-dom";
import { isAdminAuthenticated, adminApi, clearAdminAuth } from "../../services/adminApi";
import AdminSidebar from "./AdminSidebar";
import AdminHeader from "./AdminHeader";
import AdminErrorBoundary from "./AdminErrorBoundary";
import "../../styles/adminThemeTokens.css";
import "./AdminLayout.css";

const VALID_THEMES = ["obsidian", "light", "contrast"];

export default function AdminLayout() {
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(
    () => typeof window !== "undefined" && window.innerWidth <= 768
  );

  useEffect(() => {
    if (typeof window !== "undefined" && window.innerWidth <= 768) {
      setCollapsed(true);
    }
  }, [location.pathname]);

  const [deviceCount, setDeviceCount] = useState(1);
  const [attentionCounts, setAttentionCounts] = useState({});
  const [attentionItems, setAttentionItems] = useState([]);
  const [healthData, setHealthData] = useState(null);
  const [sessionsData, setSessionsData] = useState([]);
  const [theme, setTheme] = useState(() => {
    try {
      const saved = localStorage.getItem("ethos_admin_theme");
      return VALID_THEMES.includes(saved) ? saved : "light";
    } catch {
      return "light";
    }
  });

  const [layoutDashboardData, setLayoutDashboardData] = useState(null);

  const handleThemeChange = (newTheme) => {
    if (VALID_THEMES.includes(newTheme)) {
      setTheme(newTheme);
      try {
        localStorage.setItem("ethos_admin_theme", newTheme);
      } catch {}
    }
  };

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
  }, [theme]);

  // Periodic session heartbeat (every 60s) to maintain active session presence
  useEffect(() => {
    if (!isAdminAuthenticated()) return;

    let isMounted = true;

    const pingHeartbeat = () => {
      adminApi
        .heartbeat()
        .catch((err) => {
          if (isMounted && (err?.status === 401 || err?.status === 403)) {
            clearAdminAuth();
            window.location.href = "/admin_portal/login";
          }
        });
    };

    pingHeartbeat();
    const heartbeatTimer = setInterval(pingHeartbeat, 60000);

    return () => {
      isMounted = false;
      clearInterval(heartbeatTimer);
    };
  }, []);

  const updateDashboardContext = (data) => {
    if (!data) return;
    setLayoutDashboardData(data);
    if (data?.attention) {
      setAttentionItems(data.attention);
      const counts = {};
      data.attention.forEach((item) => {
        const key = item.category?.toLowerCase();
        if (key) counts[key] = (counts[key] || 0) + item.count;
      });
      setAttentionCounts(counts);
    }
    if (data?.health) {
      setHealthData(data.health);
    }
  };

  useEffect(() => {
    if (!isAdminAuthenticated()) return;

    let isMounted = true;

    // Load active sessions
    adminApi
      .getSessions()
      .then((sessions) => {
        if (isMounted && Array.isArray(sessions)) {
          setSessionsData(sessions);
          const active = sessions.filter(
            (s) => s.isActive && !s.loggedOutAt && !s.revokedAt
          );
          setDeviceCount(active.length || 1);
        }
      })
      .catch(() => {});

    // Only load dashboard for header badges if not already on the dashboard page and not cached
    const isDashboardRoute = window.location.pathname.includes("/admin_portal/dashboard");
    if (!isDashboardRoute && !layoutDashboardData) {
      adminApi
        .getDashboard("week")
        .then((data) => {
          if (isMounted && data) {
            updateDashboardContext(data);
          }
        })
        .catch(() => {});
    }

    return () => {
      isMounted = false;
    };
  }, [layoutDashboardData]);

  if (!isAdminAuthenticated()) {
    return <Navigate to="/admin_portal/login" replace />;
  }

  return (
    <div className="admin-app-container" data-theme={theme}>
      <AdminSidebar
        collapsed={collapsed}
        onToggleCollapse={() => setCollapsed(!collapsed)}
        attentionCounts={attentionCounts}
      />
      <div className="admin-app-body">
        <AdminHeader
          deviceCount={deviceCount}
          currentTheme={theme}
          onThemeChange={handleThemeChange}
          attentionItems={attentionItems}
          healthData={healthData}
          sessionsData={sessionsData}
        />
        <main className="admin-app-content">
          <AdminErrorBoundary>
            <Outlet context={{ layoutDashboardData, updateDashboardContext }} />
          </AdminErrorBoundary>
        </main>
      </div>
    </div>
  );
}
