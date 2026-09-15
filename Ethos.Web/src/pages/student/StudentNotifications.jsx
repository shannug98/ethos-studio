import { useEffect, useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { studentApi } from "../../services/studentApi";
import { studentStateSync } from "../../services/studentStateSync";
import "./StudentNotifications.css";

export default function StudentNotifications() {
  const navigate = useNavigate();

  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState("");
  const [activeTab, setActiveTab] = useState("ALL");

  const loadNotifications = async () => {
    try {
      setLoading(true);
      setError("");

      const [listRes, countRes] = await Promise.all([
        studentApi.getNotifications(),
        studentApi.getUnreadNotificationCount(),
      ]);

      const items = Array.isArray(listRes) ? listRes : listRes?.data ?? [];
      const count = typeof countRes === "number" ? countRes : countRes?.unreadCount ?? 0;

      setNotifications(items);
      setUnreadCount(count);
      studentStateSync.emitUnreadCount(count);
    } catch (err) {
      setError(
        err?.data?.message ||
        err?.message ||
        "Unable to load notifications. Please try again."
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadNotifications();
  }, []);

  const handleMarkRead = async (id, isAlreadyRead) => {
    if (isAlreadyRead) return;
    try {
      await studentApi.markNotificationRead(id);
      setNotifications((prev) =>
        prev.map((n) =>
          n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n
        )
      );
      setUnreadCount((prev) => {
        const next = Math.max(0, prev - 1);
        studentStateSync.emitUnreadCount(next);
        return next;
      });
    } catch (err) {
      console.error("Failed to mark as read:", err);
    }
  };

  const handleMarkAllRead = async () => {
    if (unreadCount === 0 || actionLoading) return;
    try {
      setActionLoading(true);
      await studentApi.markAllNotificationsRead();
      const nowIso = new Date().toISOString();
      setNotifications((prev) =>
        prev.map((n) => ({ ...n, isRead: true, readAt: nowIso }))
      );
      setUnreadCount(0);
      studentStateSync.emitUnreadCount(0);
    } catch (err) {
      console.error("Failed to mark all read:", err);
    } finally {
      setActionLoading(false);
    }
  };

  const handleDelete = async (id) => {
    try {
      await studentApi.deleteNotification(id);
      const target = notifications.find((n) => n.id === id);
      setNotifications((prev) => prev.filter((n) => n.id !== id));
      if (target && !target.isRead) {
        setUnreadCount((prev) => {
          const next = Math.max(0, prev - 1);
          studentStateSync.emitUnreadCount(next);
          return next;
        });
      }
    } catch (err) {
      console.error("Failed to delete notification:", err);
    }
  };

  const filteredNotifications = useMemo(() => {
    if (activeTab === "ALL") return notifications;

    if (activeTab === "PACKAGES") {
      return notifications.filter((n) => {
        const t = String(n.type).toLowerCase();
        return t === "package" || t === "payment" || t === "6" || t === "5";
      });
    }

    if (activeTab === "CLASSES") {
      return notifications.filter((n) => {
        const t = String(n.type).toLowerCase();
        return t === "class" || t === "3";
      });
    }

    if (activeTab === "WORKSHOPS") {
      return notifications.filter((n) => {
        const t = String(n.type).toLowerCase();
        return t === "workshop" || t === "4";
      });
    }

    if (activeTab === "UPDATES") {
      return notifications.filter((n) => {
        const t = String(n.type).toLowerCase();
        return t === "system" || t === "account" || t === "feedback" || t === "auth" || t === "1" || t === "7" || t === "8";
      });
    }

    return notifications;
  }, [notifications, activeTab]);

  const formatDate = (dateStr) => {
    if (!dateStr) return "—";
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString("en-US", {
        month: "short",
        day: "numeric",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      });
    } catch {
      return dateStr;
    }
  };

  const getTypeMeta = (type) => {
    const t = String(type).toLowerCase();
    switch (t) {
      case "package":
      case "6":
        return { label: "PACKAGE", class: "badge-package" };
      case "payment":
      case "5":
        return { label: "PAYMENT", class: "badge-payment" };
      case "class":
      case "3":
        return { label: "CLASS", class: "badge-class" };
      case "workshop":
      case "4":
        return { label: "WORKSHOP", class: "badge-workshop" };
      case "feedback":
      case "7":
        return { label: "FEEDBACK", class: "badge-feedback" };
      case "account":
      case "8":
        return { label: "ACCOUNT", class: "badge-account" };
      default:
        return { label: "STUDIO", class: "badge-system" };
    }
  };

  return (
    <div className="student-notifs-page">
      <div className="student-notifs-container">

        {/* HEADER */}
        <header className="student-notifs-header">
          <div className="student-notifs-header-text">
            <h1>
              Studio Alerts &<br />
              <em>Activity Feed.</em>
            </h1>
            <p>
              Stay on top of package renewals, class reservations, workshop bookings,
              and important studio announcements.
            </p>
          </div>

          <div className="student-notifs-header-actions">
            {unreadCount > 0 && (
              <button
                type="button"
                className="student-mark-all-btn"
                onClick={handleMarkAllRead}
                disabled={actionLoading}
              >
                {actionLoading ? "MARKING..." : "MARK ALL AS READ"}
              </button>
            )}
          </div>
        </header>

        {error && (
          <div className="student-dashboard-error" style={{ marginBottom: "24px" }}>
            <p>{error}</p>
          </div>
        )}

        {/* CATEGORY TABS */}
        <div className="student-notifs-tabs">
          <button
            type="button"
            className={`student-notif-tab ${activeTab === "ALL" ? "student-notif-tab--active" : ""}`}
            onClick={() => setActiveTab("ALL")}
          >
            ALL ({notifications.length})
          </button>
          <button
            type="button"
            className={`student-notif-tab ${activeTab === "PACKAGES" ? "student-notif-tab--active" : ""}`}
            onClick={() => setActiveTab("PACKAGES")}
          >
            PACKAGES & PAYMENTS
          </button>
          <button
            type="button"
            className={`student-notif-tab ${activeTab === "CLASSES" ? "student-notif-tab--active" : ""}`}
            onClick={() => setActiveTab("CLASSES")}
          >
            CLASSES
          </button>
          <button
            type="button"
            className={`student-notif-tab ${activeTab === "WORKSHOPS" ? "student-notif-tab--active" : ""}`}
            onClick={() => setActiveTab("WORKSHOPS")}
          >
            WORKSHOPS
          </button>
          <button
            type="button"
            className={`student-notif-tab ${activeTab === "UPDATES" ? "student-notif-tab--active" : ""}`}
            onClick={() => setActiveTab("UPDATES")}
          >
            STUDIO UPDATES
          </button>
        </div>

        {/* CONTENT */}
        {loading ? (
          <div className="student-dashboard-loading">
            <div className="student-spinner" />
            <span>Loading notifications...</span>
          </div>
        ) : filteredNotifications.length === 0 ? (
          <div className="student-notifs-empty">
            <div className="student-notifs-empty-icon">🔔</div>
            <h3>No notifications here</h3>
            <p>
              {activeTab === "ALL"
                ? "You are all caught up! New updates regarding your passes, classes, and bookings will appear here."
                : `No ${activeTab.toLowerCase()} notifications found.`}
            </p>
          </div>
        ) : (
          <div className="student-notifs-list">
            {filteredNotifications.map((n) => {
              const meta = getTypeMeta(n.type);
              return (
                <article
                  key={n.id}
                  className={`student-notif-card ${n.isRead ? "student-notif-card--read" : "student-notif-card--unread"}`}
                  onClick={() => handleMarkRead(n.id, n.isRead)}
                >
                  <div className="student-notif-card-header">
                    <div className="student-notif-badges">
                      <span className={`student-notif-badge ${meta.class}`}>
                        {meta.label}
                      </span>
                      {!n.isRead && (
                        <span className="student-notif-unread-dot" title="Unread" />
                      )}
                    </div>
                    <time className="student-notif-time">
                      {formatDate(n.createdAt)}
                    </time>
                  </div>

                  <h3 className="student-notif-title">{n.title}</h3>
                  <p className="student-notif-message">{n.message}</p>

                  <div className="student-notif-card-footer" onClick={(e) => e.stopPropagation()}>
                    <div className="student-notif-cta-wrap">
                      {n.actionUrl && (
                        <button
                          type="button"
                          className="student-notif-cta-btn"
                          onClick={() => navigate(n.actionUrl)}
                        >
                          VIEW DETAILS →
                        </button>
                      )}
                    </div>

                    <div className="student-notif-actions">
                      {!n.isRead && (
                        <button
                          type="button"
                          className="student-notif-action-btn"
                          onClick={() => handleMarkRead(n.id, false)}
                        >
                          Mark as read
                        </button>
                      )}
                      <button
                        type="button"
                        className="student-notif-action-btn student-notif-action-btn--delete"
                        onClick={() => handleDelete(n.id)}
                        title="Dismiss notification"
                      >
                        Dismiss
                      </button>
                    </div>
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
