import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Calendar, Clock, MapPin, ArrowRight, Ticket, CheckCircle2 } from "lucide-react";
import { useAuth } from "../../context/AuthContext";
import { studentDashboardCache } from "../../services/studentDashboardCache";
import { studentStateSync } from "../../services/studentStateSync";
import WorkshopPassModal from "../../components/student/WorkshopPassModal";
import membershipCardBg from "../../assets/hero/hero-02.jpg";
import "./StudentDashboard.css";

export default function StudentDashboard() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const cached = studentDashboardCache.getCachedData();
  const [dashboardData, setDashboardData] = useState(cached);
  const [loading, setLoading] = useState(!cached);
  const [revalidating, setRevalidating] = useState(false);
  const [error, setError] = useState("");
  const [activePassBooking, setActivePassBooking] = useState(null);

  useEffect(() => {
    let isMounted = true;

    // Subscribe to background cache updates
    const unsubscribeCache = studentDashboardCache.subscribe((freshData) => {
      if (isMounted && freshData) {
        setDashboardData(freshData);
        setLoading(false);
        setRevalidating(false);
      }
    });

    // Subscribe to unread notification count changes
    const unsubUnread = studentStateSync.onUnreadCount((count) => {
      if (isMounted) {
        setDashboardData((prev) => (prev ? { ...prev, unreadNotifications: count } : prev));
      }
    });

    // Subscribe to profile changes
    const unsubProfile = studentStateSync.onProfileUpdated((profile) => {
      if (isMounted && profile) {
        setDashboardData((prev) => (prev ? { ...prev, profile: { ...prev.profile, ...profile } } : prev));
      }
    });

    async function loadDashboard() {
      const currentCache = studentDashboardCache.getCachedData();
      if (currentCache) {
        setDashboardData(currentCache);
        setLoading(false);
        // If cached data is stale (>30s), trigger background revalidation
        if (!studentDashboardCache.isFresh()) {
          setRevalidating(true);
          try {
            const fresh = await studentDashboardCache.getDashboard({ forceRefresh: true });
            if (isMounted) {
              setDashboardData(fresh);
            }
          } catch {
            // Retain cached data on network error
          } finally {
            if (isMounted) setRevalidating(false);
          }
        }
        return;
      }

      // Cold load (no cache available)
      try {
        setLoading(true);
        setError("");
        const data = await studentDashboardCache.getDashboard();
        if (isMounted) {
          setDashboardData(data);
        }
      } catch (err) {
        if (isMounted) {
          setError(
            err?.data?.message ||
            err?.message ||
            "Unable to load student dashboard. Please refresh."
          );
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    loadDashboard();

    return () => {
      isMounted = false;
      unsubscribeCache();
      unsubUnread();
      unsubProfile();
    };
  }, []);

  const handleLogout = () => {
    logout();
    navigate("/student/login", { replace: true });
  };

  const profile = dashboardData?.profile || {};
  const activePackage = dashboardData?.activePackage || null;
  const upcomingClasses = dashboardData?.upcomingClasses || [];
  const upcomingWorkshops = dashboardData?.upcomingWorkshops || [];
  const recentPayments = dashboardData?.recentPayments || [];

  const allowedClasses = activePackage?.classesAllowed ?? null;
  const usedClasses = activePackage?.classesUsed ?? 0;
  const classesRemaining =
    allowedClasses !== null ? Math.max(0, allowedClasses - usedClasses) : "Unlimited";

  const packageStatusMap = {
    1: "Active",
    2: "Expired",
    3: "Exhausted",
    4: "Cancelled",
  };

  function resolvePackageStatus(status) {
    if (typeof status === "string") return status;
    if (typeof status === "number") return packageStatusMap[status] || "Active";
    if (typeof status === "object") {
      if (typeof status.name === "string") return status.name;
      if (typeof status.value === "string") return status.value;
      if (typeof status.code === "string") return status.code;
      if (typeof status.value === "number") return packageStatusMap[status.value] || "Active";
    }
    return "Active";
  }

  const packageStatusString = resolvePackageStatus(activePackage?.status);

  function formatDate(dateStr) {
    if (!dateStr) return "—";
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString("en-US", {
        month: "short",
        day: "numeric",
        year: "numeric",
      });
    } catch {
      return dateStr;
    }
  }

  function formatTime(timeStr) {
    if (!timeStr) return "";
    return timeStr.slice(0, 5);
  }

  return (
    <div className="student-dashboard-page">
      <div className="student-dashboard-container">

        {/* WELCOME SECTION */}
        <section className="student-welcome-section">
          <div className="student-welcome-badge">
            <span className="student-status-indicator" />
            <span>ACTIVE MEMBERSHIP VERIFIED</span>
          </div>

          <h1>
            Welcome back,<br />
            <em>{profile.fullName || user?.fullName || "Ethos Student"}.</em>
          </h1>

          <div className="student-profile-strip">
            <span
              className="student-strip-item"
              style={{ cursor: "pointer" }}
              onClick={() => navigate("/student/profile")}
              title="Click to view full student profile"
            >
              <strong>MEMBER CODE:</strong> {profile.customerCode || user?.customerCode || "ETH-STUDENT"} ↗
            </span>
            <span className="student-strip-item">
              <strong>PHONE:</strong> +91 {profile.phone || user?.phone || "—"}
            </span>
            {profile.city && (
              <span className="student-strip-item">
                <strong>STUDIO CITY:</strong> {profile.city}
              </span>
            )}
          </div>
        </section>

        {error && (
          <div className="student-dashboard-error">
            <p>{error}</p>
          </div>
        )}

        {/* SKELETON PLACEHOLDERS */}
        {loading && !dashboardData && (
          <div className="student-dashboard-grid student-skeleton-grid">
            <div className="student-card student-skeleton-card" style={{ height: "260px" }}>
              <div className="skeleton-line" style={{ width: "30%" }} />
              <div className="skeleton-line" style={{ width: "60%", height: "24px", margin: "14px 0" }} />
              <div className="skeleton-line" style={{ width: "100%", height: "60px" }} />
            </div>
            <div className="student-card student-skeleton-card" style={{ height: "260px" }}>
              <div className="skeleton-line" style={{ width: "40%" }} />
              <div className="skeleton-line" style={{ width: "80%", height: "24px", margin: "14px 0" }} />
              <div className="skeleton-line" style={{ width: "100%", height: "60px" }} />
            </div>
          </div>
        )}

        {/* LOADED DASHBOARD CONTENT */}
        {dashboardData && (
          <>
            <div className="student-dashboard-grid">

              {/* MEMBERSHIP STATUS CARD */}
              <div
                className="student-card student-membership-card"
                style={{
                  backgroundImage: `linear-gradient(135deg, rgba(14, 13, 12, 0.88) 0%, rgba(20, 18, 16, 0.82) 100%), url(${membershipCardBg})`,
                  backgroundSize: "cover",
                  backgroundPosition: "center right",
                }}
              >
                <div className="student-card-header">
                  <span className="student-card-eyebrow">STUDIO PASS</span>
                  <span className={`student-status-badge status-${packageStatusString.toLowerCase()}`}>
                    {packageStatusString.toUpperCase()}
                  </span>
                </div>

                {activePackage ? (
                  <>
                    <h3 className="student-package-name">{activePackage.packageName}</h3>
                    <p className="student-meta-sub">
                      Valid through: {formatDate(activePackage.expiryDate)}
                    </p>

                    <div className="student-credits-display">
                      <div className="student-credit-block">
                        <span className="student-credit-label">CLASSES REMAINING</span>
                        <strong className="student-credit-number">{classesRemaining}</strong>
                      </div>
                      <div className="student-credit-block student-credit-block--used">
                        <span className="student-credit-label">USED THIS PASS</span>
                        <strong className="student-credit-number">{usedClasses}</strong>
                      </div>
                    </div>

                    <div className="student-card-actions">
                      <button
                        type="button"
                        className="student-primary-action"
                        onClick={() => navigate("/student/classes")}
                      >
                        BOOK A CLASS →
                      </button>
                      <button
                        type="button"
                        className="student-secondary-action"
                        onClick={() => navigate("/student/packages")}
                      >
                        RENEW / UPGRADE PASS
                      </button>
                    </div>
                  </>
                ) : (
                  <div className="student-empty-state-box">
                    <p>You do not have an active dance pass.</p>
                    <button
                      type="button"
                      className="student-primary-action"
                      onClick={() => navigate("/student/packages")}
                    >
                      EXPLORE PASSES →
                    </button>
                  </div>
                )}
              </div>

              {/* QUICK ACTIONS & STUDIO STATS */}
              <div className="student-card student-actions-card">
                <span className="student-card-eyebrow">STUDIO NAVIGATION</span>
                <h3>Quick Actions</h3>
                <p className="student-actions-intro">
                  Access studio programs, check your dance attendance, or explore guest workshops.
                </p>

                <div className="student-actions-menu">
                  <button
                    type="button"
                    className="student-menu-btn"
                    onClick={() => navigate("/student/classes")}
                  >
                    <span>STUDIO CLASSES & ENROLLMENTS</span>
                    <span>→</span>
                  </button>
                  <button
                    type="button"
                    className="student-menu-btn"
                    onClick={() => navigate("/student/workshops")}
                  >
                    <span>STUDIO WORKSHOPS</span>
                    <span>→</span>
                  </button>
                  <button
                    type="button"
                    className="student-menu-btn"
                    onClick={() => navigate("/student/my-workshops")}
                  >
                    <span>MY WORKSHOP BOOKINGS</span>
                    <span>→</span>
                  </button>
                  <button
                    type="button"
                    className="student-menu-btn"
                    onClick={() => navigate("/student/packages")}
                  >
                    <span>MY MEMBERSHIP PACKAGES</span>
                    <span>→</span>
                  </button>
                  <button
                    type="button"
                    className="student-menu-btn"
                    onClick={() => navigate("/student/feedback")}
                  >
                    <span>LEARNING HISTORY & REVIEWS</span>
                    <span>→</span>
                  </button>
                </div>

                {dashboardData?.learningActivity && (
                  <div className="student-attendance-summary-box">
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <span className="student-meta-sub">LEARNING ACTIVITY OVERVIEW</span>
                      <button
                        type="button"
                        style={{ background: "transparent", border: "none", color: "#e97963", fontSize: "10px", fontWeight: 700, cursor: "pointer", letterSpacing: "0.1em" }}
                        onClick={() => navigate("/student/feedback")}
                      >
                        VIEW ALL REVIEWS →
                      </button>
                    </div>
                    <div className="student-att-stat-row">
                      <div className="student-att-stat">
                        <strong>{dashboardData.learningActivity.classesEnrolled ?? 0}</strong>
                        <small>CLASSES ENROLLED</small>
                      </div>
                      <div className="student-att-stat">
                        <strong>{dashboardData.learningActivity.classesCompleted ?? 0}</strong>
                        <small>COMPLETED</small>
                      </div>
                      <div className="student-att-stat">
                        <strong>{dashboardData.learningActivity.workshopsBooked ?? 0}</strong>
                        <small>WORKSHOPS</small>
                      </div>
                      <div className="student-att-stat">
                        <strong style={{ color: "#e97963" }}>{dashboardData.learningActivity.feedbackPending ?? 0}</strong>
                        <small>REVIEWS DUE</small>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </div>

            {/* UPCOMING CLASSES SECTION */}
            <section className="student-section-wrap">
              <div className="student-section-header">
                <div>
                  <span className="student-card-eyebrow">YOUR SCHEDULE</span>
                  <h3>Upcoming Classes</h3>
                </div>
              </div>

              {upcomingClasses.length > 0 ? (
                <div className="student-sessions-list">
                  {upcomingClasses.map((session) => (
                    <div key={session.sessionId} className="student-session-card">
                      <div className="student-session-date-badge">
                        <span className="session-date-day">
                          {new Date(session.sessionDate).toLocaleDateString("en-US", { weekday: "short" }).toUpperCase()}
                        </span>
                        <span className="session-date-num">
                          {new Date(session.sessionDate).getDate()}
                        </span>
                        <span className="session-date-month">
                          {new Date(session.sessionDate).toLocaleDateString("en-US", { month: "short" }).toUpperCase()}
                        </span>
                      </div>

                      <div className="student-session-info">
                        <h4>{session.danceClassName}</h4>
                        <div className="student-session-meta">
                          <span>⏱ {formatTime(session.startTime)} - {formatTime(session.endTime)}</span>
                          <span>• {session.danceStyle}</span>
                          {session.studioRoom && <span>• {session.studioRoom}</span>}
                        </div>
                      </div>

                      <div className="student-session-status">
                        <span className="student-session-pill">CONFIRMED</span>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="student-empty-state-box">
                  <div className="student-empty-icon">📅</div>
                  <h4>No upcoming classes scheduled</h4>
                  <p>Explore available studio classes and reserve your spot for the upcoming week.</p>
                  <button
                    type="button"
                    className="student-secondary-action"
                    onClick={() => navigate("/student/classes")}
                  >
                    EXPLORE & ENROLL IN CLASSES →
                  </button>
                </div>
              )}
            </section>

            {/* UPCOMING WORKSHOPS SECTION (INTERACTIVE BOOKED WORKSHOP CARDS) */}
            {upcomingWorkshops.length > 0 && (
              <section className="student-section-wrap">
                <div className="student-section-header">
                  <div>
                    <span className="student-card-eyebrow">CONFIRMED PASSES</span>
                    <h3>Booked Workshops</h3>
                  </div>
                  <button
                    type="button"
                    className="student-view-all-link"
                    onClick={() => navigate("/student/my-workshops")}
                  >
                    View All Bookings →
                  </button>
                </div>

                <div className="dashboard-booked-workshops-grid">
                  {upcomingWorkshops.map((workshop) => {
                    const d = new Date(workshop.workshopDate);
                    const day = d.getDate();
                    const month = d.toLocaleDateString("en-US", { month: "short" }).toUpperCase();
                    const year = d.getFullYear();

                    return (
                      <div key={workshop.bookingId} className="dashboard-workshop-pass-card">
                        <div className="dw-card-top">
                          <span className="dw-pass-badge">YOUR WORKSHOP</span>
                          <span className="dw-status-badge">
                            <span className="status-dot-green" />
                            BOOKING CONFIRMED
                          </span>
                        </div>

                        <h4 className="dw-title">{workshop.workshopTitle}</h4>

                        <div className="dw-schedule-row">
                          <div className="dw-date-pill">
                            <Calendar size={13} />
                            <span>{day} {month} {year}</span>
                          </div>
                          {workshop.startTime && workshop.endTime && (
                            <div className="dw-time-pill">
                              <Clock size={13} />
                              <span>{formatTime(workshop.startTime)} – {formatTime(workshop.endTime)}</span>
                            </div>
                          )}
                        </div>

                        <div className="dw-venue-row">
                          <MapPin size={13} />
                          <span>{workshop.venue || workshop.city || "Ethos Dance Studio Main Arena"}</span>
                        </div>

                        <div className="dw-action-row">
                          <button
                            type="button"
                            className="dw-view-pass-btn"
                            onClick={() => setActivePassBooking({
                              id: workshop.bookingId,
                              workshopId: workshop.workshopId,
                              workshopTitle: workshop.workshopTitle,
                              workshopDate: workshop.workshopDate,
                              startTime: workshop.startTime,
                              endTime: workshop.endTime,
                              trainerName: workshop.trainerName,
                              venue: workshop.venue || workshop.city || "Ethos Dance Studio",
                              price: workshop.price,
                              bookingReference: workshop.bookingReference,
                            })}
                          >
                            <span>VIEW WORKSHOP PASS</span>
                            <ArrowRight size={14} />
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </section>
            )}

            {/* RECENT PAYMENTS / ACTIVITY */}
            <section className="student-section-wrap">
              <div className="student-section-header">
                <div>
                  <span className="student-card-eyebrow">ORDER HISTORY</span>
                  <h3>Recent Payment Activity</h3>
                </div>
              </div>

              {recentPayments.length > 0 ? (
                <div className="student-payments-table-wrap">
                  <table className="student-payments-table">
                    <thead>
                      <tr>
                        <th>DATE</th>
                        <th>TRANSACTION ID</th>
                        <th>AMOUNT</th>
                        <th>STATUS</th>
                      </tr>
                    </thead>
                    <tbody>
                      {recentPayments.map((payment) => (
                        <tr key={payment.id}>
                          <td>{formatDate(payment.paidAt || payment.createdAt)}</td>
                          <td className="student-tx-id">{payment.id.slice(0, 16)}...</td>
                          <td className="student-tx-amount">₹{Number(payment.amount).toLocaleString("en-IN")}</td>
                          <td>
                            <span className="student-paid-tag">PAID</span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <div className="student-empty-state-box">
                  <p>No recent payment history found.</p>
                </div>
              )}
            </section>

          </>
        )}

        {/* WORKSHOP PASS MODAL */}
        <WorkshopPassModal
          isOpen={Boolean(activePassBooking)}
          booking={activePassBooking}
          student={user}
          onClose={() => setActivePassBooking(null)}
        />

      </div>
    </div>
  );
}
