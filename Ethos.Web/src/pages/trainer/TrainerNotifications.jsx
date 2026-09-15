import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { trainerApi } from "../../services/trainerApi";
import LoadingState from "../../components/trainer/LoadingState";
import ErrorState from "../../components/trainer/ErrorState";
import EmptyState from "../../components/trainer/EmptyState";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import "./TrainerNotifications.css";

function formatDate(value) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleDateString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function getTypeBadge(type) {
  const str = String(type || "").toLowerCase();
  switch (str) {
    case "workshop":
      return { label: "WORKSHOP", class: "workshop" };
    case "payment":
      return { label: "PAYMENT", class: "payment" };
    case "auth":
      return { label: "SECURITY", class: "auth" };
    case "class":
      return { label: "CLASS", class: "class" };
    case "system":
    default:
      return { label: "SYSTEM", class: "system" };
  }
}

export default function TrainerNotifications() {
  const mountedRef = useRef(true);

  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [actionLoading, setActionLoading] = useState(false);
  const [filter, setFilter] = useState("ALL");

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      setError("");

      const [notifRes, countRes] = await Promise.all([
        trainerApi.getNotifications(),
        trainerApi.getUnreadNotificationCount(),
      ]);

      if (!mountedRef.current) return;

      const items = notifRes?.data ?? notifRes;
      const countData = countRes?.data ?? countRes;

      setNotifications(Array.isArray(items) ? items : []);
      setUnreadCount(countData?.unreadCount ?? 0);
    } catch (err) {
      if (!mountedRef.current) return;
      console.error("Failed to load notifications:", err);
      setError(
        getApiErrorMessage(err, "Unable to load your notifications right now.")
      );
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    loadData();
    return () => {
      mountedRef.current = false;
    };
  }, [loadData]);

  async function handleMarkRead(id, isAlreadyRead) {
    if (isAlreadyRead) return;

    try {
      await trainerApi.markNotificationRead(id);
      if (!mountedRef.current) return;

      setNotifications((prev) =>
        prev.map((n) =>
          n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n
        )
      );

      setUnreadCount((prev) => Math.max(0, prev - 1));
    } catch (err) {
      if (!mountedRef.current) return;
      console.error("Failed to mark notification as read:", err);
    }
  }

  async function handleDeleteNotification(event, id) {
    event.stopPropagation();

    try {
      await trainerApi.deleteNotification(id);
      if (!mountedRef.current) return;

      setNotifications((prev) => prev.filter((n) => n.id !== id));
    } catch (err) {
      if (!mountedRef.current) return;
      console.error("Failed to delete notification:", err);
      setError(getApiErrorMessage(err, "Unable to delete notification."));
    }
  }

  async function handleMarkAllRead() {
    if (unreadCount === 0) return;

    try {
      setActionLoading(true);
      await trainerApi.markAllNotificationsRead();
      if (!mountedRef.current) return;

      setNotifications((prev) =>
        prev.map((n) => ({
          ...n,
          isRead: true,
          readAt: n.readAt || new Date().toISOString(),
        }))
      );

      setUnreadCount(0);
    } catch (err) {
      if (!mountedRef.current) return;
      console.error("Failed to mark all as read:", err);
      setError(getApiErrorMessage(err, "Unable to mark notifications as read."));
    } finally {
      if (mountedRef.current) {
        setActionLoading(false);
      }
    }
  }

  // Payments are excluded completely from the trainer notification portal
  const visibleNotifications = useMemo(() => {
    return notifications.filter((n) => String(n.type).toLowerCase() !== "payment");
  }, [notifications]);

  const filteredNotifications = useMemo(() => {
    return visibleNotifications.filter((n) => {
      if (filter === "UNREAD") return !n.isRead;
      if (filter === "WORKSHOP")
        return String(n.type).toLowerCase() === "workshop";
      if (filter === "SYSTEM")
        return String(n.type).toLowerCase() === "system";
      return true;
    });
  }, [visibleNotifications, filter]);

  if (loading) {
    return <LoadingState label="Loading notifications..." />;
  }

  if (error && notifications.length === 0) {
    return (
      <ErrorState
        title="Notifications unavailable"
        description={error}
        action={
          <button
            type="button"
            className="mark-all-btn"
            onClick={loadData}
          >
            TRY AGAIN
          </button>
        }
      />
    );
  }

  return (
    <main className="trainer-notifications-page">
      <div className="notifications-shell">
        {/* HEADER */}
        <header className="notifications-header">
          <div>
            <span className="notifications-eyebrow">
              TRAINER PORTAL / NOTIFICATIONS
            </span>

            <h1>
              Activity <em>& updates.</em>
            </h1>

            <p>
              Stay informed about application progress, workshop submissions,
              approvals, and studio alerts.
            </p>
          </div>

          <div className="notifications-header-actions">
            {unreadCount > 0 && (
              <span className="unread-counter-badge" role="status" aria-live="polite">
                {unreadCount} UNREAD
              </span>
            )}

            <button
              type="button"
              className="mark-all-btn"
              disabled={unreadCount === 0 || actionLoading}
              onClick={handleMarkAllRead}
            >
              {actionLoading ? "MARKING..." : "MARK ALL AS READ"}
            </button>
          </div>
        </header>

        {error && (
          <div className="notifications-alert-error" role="alert">
            <span>{error}</span>
            <button type="button" onClick={loadData}>RETRY</button>
          </div>
        )}

        {/* FILTER BAR */}
        <nav className="notifications-filter-bar">
          <button
            type="button"
            className={`filter-btn ${filter === "ALL" ? "active" : ""}`}
            onClick={() => setFilter("ALL")}
          >
            ALL ({visibleNotifications.length})
          </button>
          <button
            type="button"
            className={`filter-btn ${filter === "UNREAD" ? "active" : ""}`}
            onClick={() => setFilter("UNREAD")}
          >
            UNREAD ({unreadCount})
          </button>
          <button
            type="button"
            className={`filter-btn ${filter === "WORKSHOP" ? "active" : ""}`}
            onClick={() => setFilter("WORKSHOP")}
          >
            WORKSHOPS
          </button>
          <button
            type="button"
            className={`filter-btn ${filter === "SYSTEM" ? "active" : ""}`}
            onClick={() => setFilter("SYSTEM")}
          >
            SYSTEM
          </button>
        </nav>

        {/* NOTIFICATIONS LIST */}
        {!error && filteredNotifications.length === 0 ? (
          <EmptyState
            title="No notifications found"
            description={
              filter === "UNREAD"
                ? "You're all caught up! There are no unread notifications."
                : "You don't have any notifications in this category yet."
            }
          />
        ) : (
          <section className="notifications-list">
            {filteredNotifications.map((notif) => {
              const badge = getTypeBadge(notif.type);

              return (
                <article
                  key={notif.id}
                  className={`notification-card ${!notif.isRead ? "unread" : "read"}`}
                  onClick={() => handleMarkRead(notif.id, notif.isRead)}
                >
                  {!notif.isRead && <span className="unread-dot" />}

                  <div className="notif-card-header">
                    <span className={`notif-type-tag ${badge.class}`}>
                      {badge.label}
                    </span>
                    <span className="notif-date">{formatDate(notif.createdAt)}</span>
                  </div>

                  <h3 className="notif-title">{notif.title}</h3>
                  <p className="notif-message">{notif.message}</p>

                  {notif.isRead && (
                    <div className="notif-card-footer">
                      <button
                        type="button"
                        className="notif-delete-btn"
                        onClick={(e) => handleDeleteNotification(e, notif.id)}
                        aria-label="Delete notification"
                      >
                        DELETE
                      </button>
                    </div>
                  )}
                </article>
              );
            })}
          </section>
        )}
      </div>
    </main>
  );
}
