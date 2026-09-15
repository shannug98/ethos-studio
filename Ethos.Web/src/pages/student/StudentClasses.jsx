import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { studentApi } from "../../services/studentApi";
import { classesApi } from "../../services/classesApi";
import { useAuth } from "../../context/AuthContext";
import "./StudentClasses.css";

const DAY_NAMES = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
];

export default function StudentClasses() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState("");
  const [successMsg, setSuccessMsg] = useState("");

  const [activePackage, setActivePackage] = useState(null);
  const [availableClasses, setAvailableClasses] = useState([]);
  const [enrollments, setEnrollments] = useState([]);
  const [cancellingId, setCancellingId] = useState(null);

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    setError("");
    try {
      const [pkgRes, classesRes, enrollRes] = await Promise.all([
        studentApi.getActivePackage().catch(() => null),
        classesApi.getActiveClasses().catch(() => []),
        studentApi.getEnrollments().catch(() => []),
      ]);

      setActivePackage(pkgRes || null);
      setAvailableClasses(Array.isArray(classesRes) ? classesRes : []);
      setEnrollments(Array.isArray(enrollRes) ? enrollRes : []);
    } catch (err) {
      console.error("Failed to load classes data:", err);
      setError("Unable to load classes. Please refresh or try again.");
    } finally {
      setLoading(false);
    }
  }

  const activeEnrollments = enrollments.filter((e) => e.status === 1 || e.status === "Active");
  const enrolledClassIds = new Set(activeEnrollments.map((e) => e.danceClassId));

  const allowed = activePackage?.classesAllowed ?? null;
  const used = activePackage?.classesUsed ?? 0;
  const remaining = allowed !== null ? Math.max(0, allowed - used) : "Unlimited";
  const hasCredits = allowed === null || remaining > 0;

  async function handleEnroll(danceClassId) {
    if (!activePackage) {
      setError("An active membership pass is required to enroll in classes.");
      return;
    }
    if (allowed !== null && remaining <= 0) {
      setError("You have reached the class limit for your current pass cycle.");
      return;
    }

    setActionLoading(true);
    setError("");
    setSuccessMsg("");

    try {
      await studentApi.enrollInClass(danceClassId);
      setSuccessMsg("Successfully enrolled in class!");
      await loadData();
    } catch (err) {
      console.error("Enroll error:", err);
      const msg =
        err?.data?.message ||
        err?.message ||
        "Enrollment failed. Please verify your pass status.";
      setError(msg);
    } finally {
      setActionLoading(false);
    }
  }

  async function handleCancel(enrollmentId, className) {
    const confirmCancel = window.confirm(
      `Are you sure you want to cancel your enrollment in ${className}? Your pass credit will be refunded.`
    );
    if (!confirmCancel) return;

    setCancellingId(enrollmentId);
    setError("");
    setSuccessMsg("");

    try {
      await studentApi.cancelEnrollment(enrollmentId);
      setSuccessMsg(`Enrollment in ${className} cancelled. 1 pass credit refunded.`);
      await loadData();
    } catch (err) {
      console.error("Cancel error:", err);
      const msg =
        err?.data?.message ||
        err?.message ||
        "Cancellation failed. Please try again.";
      setError(msg);
    } finally {
      setCancellingId(null);
    }
  }

  function formatTime(timeStr) {
    if (!timeStr) return "";
    return timeStr.slice(0, 5);
  }

  return (
    <div className="student-classes-page">
      <div className="student-classes-container">
        {/* HERO TITLE & PASS STATUS */}
        <div className="student-classes-title-area">
          <span className="student-classes-badge">STUDIO ENROLLMENTS</span>
          <h1>
            Your Studio Classes<br />
            <em>& Enrollment Hub.</em>
          </h1>
          <p className="student-classes-intro">
            Reserve your weekly class positions, explore upcoming dance styles, and manage your active studio schedule.
          </p>

          {/* ACTIVE PASS BANNER */}
          <div className="student-classes-pass-banner">
            <div className="pass-banner-left">
              <span className="pass-banner-eyebrow">ACTIVE MEMBERSHIP PASS</span>
              <h3>{activePackage ? activePackage.packageName : "No Active Pass Found"}</h3>
              {activePackage && (
                <p className="pass-banner-dates">
                  Valid until: {new Date(activePackage.expiryDate).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}
                </p>
              )}
            </div>

            <div className="pass-banner-right">
              {activePackage ? (
                <div className="pass-credits-box">
                  <span className="credits-number">{remaining}</span>
                  <span className="credits-label">
                    {allowed !== null ? "CLASSES REMAINING" : "UNLIMITED SESSIONS"}
                  </span>
                </div>
              ) : (
                <Link to="/classes" className="pass-buy-link">
                  EXPLORE PASSES →
                </Link>
              )}
            </div>
          </div>
        </div>

        {/* NOTIFICATIONS / FEEDBACK */}
        {error && (
          <div className="student-classes-alert student-classes-alert--error">
            <span>⚠</span>
            <p>{error}</p>
          </div>
        )}
        {successMsg && (
          <div className="student-classes-alert student-classes-alert--success">
            <span>✓</span>
            <p>{successMsg}</p>
          </div>
        )}

        {loading ? (
          <div className="student-classes-loading">
            <div className="student-spinner" />
            <span>Loading studio classes and your enrollments...</span>
          </div>
        ) : (
          <>
            {/* SECTION: MY ACTIVE ENROLLMENTS */}
            <section className="student-classes-section">
              <div className="section-head">
                <div>
                  <span className="section-eyebrow">CURRENTLY JOINED</span>
                  <h2>My Enrolled Classes ({activeEnrollments.length})</h2>
                </div>
              </div>

              {activeEnrollments.length > 0 ? (
                <div className="enrollments-grid">
                  {activeEnrollments.map((item) => (
                    <div key={item.id} className="enrollment-card">
                      <div className="enrollment-card-top">
                        <span className="enrollment-tag">ENROLLED</span>
                        <span className="enrollment-style">{item.danceStyle}</span>
                      </div>
                      <h3>{item.danceClassName}</h3>
                      <p className="enrollment-date">
                        Enrolled on: {new Date(item.enrollmentDate).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}
                      </p>

                      <div className="enrollment-actions">
                        <button
                          type="button"
                          className="cancel-enrollment-btn"
                          disabled={cancellingId === item.id || actionLoading}
                          onClick={() => handleCancel(item.id, item.danceClassName)}
                        >
                          {cancellingId === item.id ? "CANCELLING..." : "CANCEL ENROLLMENT"}
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="classes-empty-box">
                  <span className="empty-icon">💃</span>
                  <h4>No active class enrollments yet</h4>
                  <p>Browse the available studio dance classes below and select your first class.</p>
                </div>
              )}
            </section>

            {/* SECTION: AVAILABLE CLASSES */}
            <section className="student-classes-section">
              <div className="section-head">
                <div>
                  <span className="section-eyebrow">STUDIO REPERTOIRE</span>
                  <h2>Available Studio Classes</h2>
                </div>
              </div>

              <div className="available-classes-grid">
                {availableClasses.map((cls) => {
                  const isEnrolled = enrolledClassIds.has(cls.id);

                  return (
                    <div key={cls.id} className={`class-catalog-card ${isEnrolled ? "class-catalog-card--enrolled" : ""}`}>
                      <div className="catalog-card-header">
                        <div className="catalog-pills">
                          <span className="style-pill">{cls.danceStyle}</span>
                          <span className="level-pill">{cls.level}</span>
                        </div>
                        <span className="duration-tag">{cls.durationMinutes} MIN</span>
                      </div>

                      <h3>{cls.name}</h3>
                      {cls.description && <p className="catalog-desc">{cls.description}</p>}

                      {/* SCHEDULES LIST */}
                      <div className="catalog-schedules">
                        <span className="sched-label">WEEKLY SESSIONS:</span>
                        {cls.schedules && cls.schedules.length > 0 ? (
                          <div className="sched-tags">
                            {cls.schedules.map((s) => (
                              <div key={s.id} className="sched-chip">
                                <strong>{DAY_NAMES[s.dayOfWeek]}</strong>: {formatTime(s.startTime)} - {formatTime(s.endTime)}
                                {s.studioRoom && <span className="room-label"> ({s.studioRoom})</span>}
                              </div>
                            ))}
                          </div>
                        ) : (
                          <p className="no-sched">Schedule announced weekly</p>
                        )}
                      </div>

                      {/* ACTION FOOTER */}
                      <div className="catalog-card-footer">
                        {isEnrolled ? (
                          <span className="enrolled-indicator">
                            ✓ Currently Enrolled
                          </span>
                        ) : (
                          <button
                            type="button"
                            className="enroll-btn"
                            disabled={actionLoading || !hasCredits || !activePackage}
                            onClick={() => handleEnroll(cls.id)}
                          >
                            {actionLoading ? "ENROLLING..." : "ENROLL IN CLASS →"}
                          </button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </section>
          </>
        )}

      </div>
    </div>
  );
}
