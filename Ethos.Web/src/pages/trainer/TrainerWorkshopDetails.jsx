import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { QrCode, CheckCircle2, UserCheck, Users, Search, DoorOpen, LogOut } from "lucide-react";
import TrainerQrScannerModal from "../../components/trainer/TrainerQrScannerModal";
import { trainerApi } from "../../services/trainerApi";
import { useTrainerPermissions } from "../../hooks/useTrainerPermissions";
import { TRAINER_PERMISSIONS } from "../../constants/trainerPermissions";
import LoadingState from "../../components/trainer/LoadingState";
import ErrorState from "../../components/trainer/ErrorState";
import EmptyState from "../../components/trainer/EmptyState";
import StatusBadge from "../../components/trainer/StatusBadge";
import ConfirmDialog from "../../components/trainer/ConfirmDialog";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import "./TrainerWorkshopDetails.css";

function formatDate(value) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleDateString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
}

function formatTime(value) {
  if (!value) return "—";
  const parts = value.split(":");
  const hours = Number(parts[0]);
  const minutes = Number(parts[1] || 0);

  if (Number.isNaN(hours)) return value;

  const date = new Date();
  date.setHours(hours, minutes, 0, 0);

  return date.toLocaleTimeString("en-IN", {
    hour: "numeric",
    minute: "2-digit",
  });
}

function formatPrice(value) {
  if (value === null || value === undefined) return "—";
  return `₹${Number(value).toLocaleString("en-IN")}`;
}

export default function TrainerWorkshopDetails() {
  const { id } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const mountedRef = useRef(true);

  const { hasPermission } = useTrainerPermissions();

  // Determine active tab from URL path
  let activeTab = "overview";
  if (location.pathname.endsWith("/students")) {
    activeTab = "students";
  } else if (location.pathname.endsWith("/feedback")) {
    activeTab = "feedback";
  } else if (location.pathname.endsWith("/attendance")) {
    activeTab = "attendance";
  }

  const [workshop, setWorkshop] = useState(null);
  const [students, setStudents] = useState([]);
  const [feedback, setFeedback] = useState([]);

  const [loading, setLoading] = useState(true);
  const [loadingStudents, setLoadingStudents] = useState(false);
  const [loadingFeedback, setLoadingFeedback] = useState(false);

  const [actionLoading, setActionLoading] = useState("");
  const [error, setError] = useState("");
  const [actionMessage, setActionMessage] = useState("");
  const [confirmModal, setConfirmModal] = useState(null); // null | 'submit' | 'cancel'
  const [attendance, setAttendance] = useState(null);
  const [loadingAttendance, setLoadingAttendance] = useState(false);
  const [showScanner, setShowScanner] = useState(false);
  const [attendanceSearch, setAttendanceSearch] = useState("");
  const [attendanceActionLoading, setAttendanceActionLoading] = useState("");

  const loadAttendance = useCallback(async () => {
    if (!id) return;
    try {
      setLoadingAttendance(true);
      const res = await trainerApi.getWorkshopAttendance(id);
      if (!mountedRef.current) return;
      setAttendance(res?.data || res);
    } catch {
      if (mountedRef.current) setAttendance(null);
    } finally {
      if (mountedRef.current) setLoadingAttendance(false);
    }
  }, [id]);

  // Search & Filter States
  const [studentSearch, setStudentSearch] = useState("");
  const [ratingFilter, setRatingFilter] = useState("ALL");

  const loadWorkshop = useCallback(async () => {
    try {
      setLoading(true);
      setError("");
      const response = await trainerApi.getWorkshop(id);
      if (!mountedRef.current) return;
      setWorkshop(response?.data || response);
    } catch (err) {
      if (!mountedRef.current) return;
      setError(
        getApiErrorMessage(err, "We couldn't load this workshop.")
      );
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, [id]);

  const loadStudents = useCallback(async () => {
    try {
      setLoadingStudents(true);
      const response = await trainerApi.getWorkshopStudents(id);
      if (!mountedRef.current) return;
      setStudents(Array.isArray(response?.data) ? response.data : (Array.isArray(response) ? response : []));
    } catch {
      if (mountedRef.current) {
        setStudents([]);
      }
    } finally {
      if (mountedRef.current) {
        setLoadingStudents(false);
      }
    }
  }, [id]);

  const loadFeedback = useCallback(async () => {
    try {
      setLoadingFeedback(true);
      const response = await trainerApi.getWorkshopFeedback(id);
      if (!mountedRef.current) return;
      setFeedback(Array.isArray(response?.data) ? response.data : (Array.isArray(response) ? response : []));
    } catch {
      if (mountedRef.current) {
        setFeedback([]);
      }
    } finally {
      if (mountedRef.current) {
        setLoadingFeedback(false);
      }
    }
  }, [id]);

  useEffect(() => {
    mountedRef.current = true;
    loadWorkshop();
    return () => {
      mountedRef.current = false;
    };
  }, [loadWorkshop]);

  useEffect(() => {
    if (!workshop) return;
    loadStudents();
    loadFeedback();
    loadAttendance();
  }, [workshop?.id, loadStudents, loadFeedback, loadAttendance]);

  async function executeSubmit() {
    try {
      setActionLoading("submit");
      setError("");
      setActionMessage("");

      await trainerApi.submitWorkshop(id);
      if (!mountedRef.current) return;

      setActionMessage("WORKSHOP SUBMITTED FOR APPROVAL");
      setConfirmModal(null);
      await loadWorkshop();
    } catch (err) {
      if (!mountedRef.current) return;
      setError(
        getApiErrorMessage(err, "The workshop could not be submitted.")
      );
    } finally {
      if (mountedRef.current) {
        setActionLoading("");
      }
    }
  }


  async function handleQuickCheckIn(ticketId) {
    try {
      setAttendanceActionLoading(ticketId);
      await trainerApi.checkInWorkshopTicket(id, ticketId, { method: 2 }); // Manual search
      await loadAttendance();
      setActionMessage("Attendee checked in successfully.");
    } catch (err) {
      setError(getApiErrorMessage(err, "Check-in failed."));
    } finally {
      setAttendanceActionLoading("");
    }
  }

  async function handleReEntry(ticketId, isExiting) {
    try {
      setAttendanceActionLoading(ticketId);
      await trainerApi.recordWorkshopReEntry(id, ticketId, isExiting ? 1 : 2); // 1 = CheckOut, 2 = ReEntry
      await loadAttendance();
      setActionMessage(isExiting ? "Pass-out (exit) recorded." : "Re-entry confirmed.");
    } catch (err) {
      setError(getApiErrorMessage(err, "Failed to record entry/exit."));
    } finally {
      setAttendanceActionLoading("");
    }
  }

  async function executeCancel() {
    try {
      setActionLoading("cancel");
      setError("");
      setActionMessage("");

      await trainerApi.cancelWorkshop(id);
      if (!mountedRef.current) return;

      setActionMessage("WORKSHOP CANCELLED");
      setConfirmModal(null);
      await loadWorkshop();
    } catch (err) {
      if (!mountedRef.current) return;
      setError(
        getApiErrorMessage(err, "The workshop could not be cancelled.")
      );
    } finally {
      if (mountedRef.current) {
        setActionLoading("");
      }
    }
  }

  function handleTabChange(tab) {
    if (tab === "overview") {
      navigate(`/trainer/workshops/${id}`);
    } else {
      navigate(`/trainer/workshops/${id}/${tab}`);
    }
  }

  if (loading) {
    return <LoadingState label="Loading workshop details..." />;
  }

  if (error && !workshop) {
    return (
      <ErrorState
        title="Workshop unavailable"
        description={error}
        action={
          <div style={{ display: "flex", gap: "12px" }}>
            <button
              type="button"
              className="trainer-primary-button"
              onClick={loadWorkshop}
            >
              TRY AGAIN
            </button>
            <Link
              to="/trainer/workshops"
              className="trainer-secondary-button"
              style={{ display: "inline-block", textDecoration: "none" }}
            >
              ALL WORKSHOPS
            </Link>
          </div>
        }
      />
    );
  }

  const canEdit =
    (workshop.status === "Draft" || workshop.status === "Rejected") &&
    hasPermission(TRAINER_PERMISSIONS.UPDATE_WORKSHOP);
  const canSubmit =
    (workshop.status === "Draft" || workshop.status === "Rejected") &&
    hasPermission(TRAINER_PERMISSIONS.UPDATE_WORKSHOP);
  const canCancel =
    workshop.status !== "Completed" &&
    workshop.status !== "Cancelled" &&
    hasPermission(TRAINER_PERMISSIONS.CANCEL_WORKSHOP);

  const canViewStudents = hasPermission(
    TRAINER_PERMISSIONS.VIEW_WORKSHOP_STUDENTS
  );
  const canViewFeedback = hasPermission(
    TRAINER_PERMISSIONS.VIEW_WORKSHOP_FEEDBACK
  );

  // Filtered Students
  const filteredStudents = students.filter((s) => {
    if (!studentSearch.trim()) return true;
    const query = studentSearch.toLowerCase();
    return (
      (s.studentName || "").toLowerCase().includes(query) ||
      (s.studentPhone || "").toLowerCase().includes(query)
    );
  });

  // Feedback Metrics Calculation
  const totalReviews = feedback.length;
  const avgRating =
    totalReviews > 0
      ? (
          feedback.reduce((sum, item) => sum + item.rating, 0) / totalReviews
        ).toFixed(1)
      : "0.0";

  const ratingCounts = { 5: 0, 4: 0, 3: 0, 2: 0, 1: 0 };
  feedback.forEach((item) => {
    if (ratingCounts[item.rating] !== undefined) {
      ratingCounts[item.rating]++;
    }
  });

  const filteredFeedback = feedback.filter((item) => {
    if (ratingFilter === "ALL") return true;
    return item.rating === Number(ratingFilter);
  });

  return (
    <main className="trainer-workshop-details-page">
      <div className="workshop-details-shell">
        <Link to="/trainer/workshops" className="workshop-back-link">
          ← ALL WORKSHOPS
        </Link>

        {actionMessage && (
          <div className="workshop-success-message" role="status" aria-live="polite">
            {actionMessage}
          </div>
        )}

        {error && (
          <div className="workshop-detail-error-inline" role="alert">
            {error}
          </div>
        )}

        {/* HERO SECTION */}
        <section className="workshop-detail-hero">
          <div className="workshop-detail-image">
            {workshop.imageUrl ? (
              <img src={workshop.imageUrl} alt={workshop.title} />
            ) : (
              <div className="hero-fallback-mark">ETHOS</div>
            )}
          </div>

          <div className="workshop-detail-intro">
            <div className="workshop-detail-topline">
              <span>{workshop.danceStyle || "DANCE"}</span>
              <StatusBadge status={workshop.status} />
            </div>

            <h1>{workshop.title}</h1>
            <p>{workshop.description}</p>

            <div className="workshop-detail-actions">
              {canEdit && (
                <Link
                  to={`/trainer/workshops/${workshop.id}/edit`}
                  className="trainer-secondary-action"
                  style={{ textDecoration: "none", display: "inline-block" }}
                >
                  EDIT WORKSHOP
                </Link>
              )}

              {canSubmit && (
                <button
                  type="button"
                  onClick={() => setConfirmModal("submit")}
                  disabled={actionLoading === "submit"}
                  className="trainer-primary-action"
                >
                  {actionLoading === "submit" ? "SUBMITTING..." : "SUBMIT FOR APPROVAL"}
                </button>
              )}

              {canCancel && (
                <button
                  type="button"
                  onClick={() => setConfirmModal("cancel")}
                  disabled={actionLoading === "cancel"}
                  className="trainer-danger-action"
                >
                  {actionLoading === "cancel" ? "CANCELLING..." : "CANCEL WORKSHOP"}
                </button>
              )}
            </div>
          </div>
        </section>

        {/* TAB NAVIGATION */}
        <nav className="workshop-detail-tabs">
          <button
            type="button"
            className={`tab-btn ${activeTab === "overview" ? "active" : ""}`}
            onClick={() => handleTabChange("overview")}
          >
            OVERVIEW
          </button>
          {canViewStudents && (
            <button
              type="button"
              className={`tab-btn ${activeTab === "students" ? "active" : ""}`}
              onClick={() => handleTabChange("students")}
            >
              STUDENTS <span className="tab-badge">{students.length}</span>
            </button>
          )}
          <button
            type="button"
            className={`tab-btn ${activeTab === "attendance" ? "active" : ""}`}
            onClick={() => handleTabChange("attendance")}
          >
            ATTENDANCE & PASSES{' '}
            <span className="tab-badge">
              {attendance?.totalCheckedIn ?? 0} / {attendance?.totalTicketsIssued ?? 0}
            </span>
          </button>
          {canViewFeedback && (
            <button
              type="button"
              className={`tab-btn ${activeTab === "feedback" ? "active" : ""}`}
              onClick={() => handleTabChange("feedback")}
            >
              REVIEWS & FEEDBACK <span className="tab-badge">{feedback.length}</span>
            </button>
          )}
        </nav>

        {/* TAB 1: OVERVIEW */}
        {activeTab === "overview" && (
          <section className="workshop-tab-content">
            <div className="detail-section-heading">
              <span>01</span>
              <h2>Workshop Overview</h2>
            </div>

            <div className="workshop-detail-grid">
              <div>
                <small>DATE</small>
                <strong>{formatDate(workshop.workshopDate)}</strong>
              </div>

              <div>
                <small>SCHEDULE</small>
                <strong>
                  {formatTime(workshop.startTime)} — {formatTime(workshop.endTime)}
                </strong>
              </div>

              <div>
                <small>LEVEL</small>
                <strong>{workshop.level || "—"}</strong>
              </div>

              <div>
                <small>VENUE</small>
                <strong>{workshop.venue || "—"}</strong>
              </div>

              <div>
                <small>CAPACITY</small>
                <strong>{workshop.capacity} Seats</strong>
              </div>

              <div>
                <small>BOOKED SEATS</small>
                <strong>{workshop.bookedCount} Confirmed</strong>
              </div>

              <div>
                <small>WORKSHOP PRICE</small>
                <strong>{formatPrice(workshop.price)}</strong>
              </div>

              <div>
                <small>CREATED DATE</small>
                <strong>{formatDate(workshop.createdAt)}</strong>
              </div>
            </div>

            {/* Capacity Visual Bar */}
            <div className="overview-capacity-widget">
              <div className="widget-header">
                <span>ENROLLMENT PROGRESS</span>
                <strong>
                  {workshop.bookedCount} / {workshop.capacity} SEATS FILLED (
                  {workshop.capacity
                    ? Math.round((workshop.bookedCount / workshop.capacity) * 100)
                    : 0}
                  %)
                </strong>
              </div>
              <div className="widget-track">
                <div
                  className="widget-fill"
                  style={{
                    width: `${
                      workshop.capacity
                        ? Math.min(100, (workshop.bookedCount / workshop.capacity) * 100)
                        : 0
                    }%`,
                  }}
                />
              </div>
            </div>
          </section>
        )}

        {/* TAB 2: STUDENTS */}
        {activeTab === "students" && (
          <section className="workshop-tab-content">
            <div className="detail-section-heading">
              <span>02</span>
              <h2>Enrolled Students</h2>
            </div>

            <div className="students-filter-bar">
              <input
                type="text"
                placeholder="Search by student name or phone..."
                value={studentSearch}
                onChange={(e) => setStudentSearch(e.target.value)}
                className="student-search-input"
              />
              <div className="students-count-tag">
                Showing {filteredStudents.length} of {students.length} students
              </div>
            </div>

            {loadingStudents ? (
              <LoadingState label="Loading student directory..." />
            ) : filteredStudents.length === 0 ? (
              <EmptyState
                title={studentSearch ? "No matching students" : "No enrollments yet"}
                description={
                  studentSearch
                    ? "No student names or phone numbers match your search filter."
                    : "No confirmed student enrollments found for this workshop."
                }
              />
            ) : (
              <div className="student-cards-grid">
                {filteredStudents.map((student, idx) => (
                  <article className="student-detail-card" key={student.studentId || idx}>
                    <div className="student-avatar-badge">
                      {(student.studentName || "ST")
                        .split(" ")
                        .map((n) => n[0])
                        .join("")
                        .substring(0, 2)
                        .toUpperCase()}
                    </div>

                    <div className="student-card-info">
                      <h3>{student.studentName}</h3>
                      <span className="student-phone">{student.studentPhone}</span>
                    </div>

                    <div className="student-card-meta">
                      <span className="student-status-badge">{student.status}</span>
                      <small>Booked on {formatDate(student.bookedAt)}</small>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>
        )}

        
        {/* TAB 4: ATTENDANCE & PASSES */}
        {activeTab === "attendance" && (
          <section className="workshop-tab-content">
            <div className="detail-section-heading">
              <span>04</span>
              <h2>Workshop Attendance & Passes</h2>
            </div>

            {/* METRICS ROW */}
            <div className="attendance-metrics-grid">
              <div className="attendance-metric-card">
                <Users size={20} className="metric-icon" />
                <div>
                  <small>TOTAL PASSES SOLD</small>
                  <strong>{attendance?.totalTicketsIssued ?? 0}</strong>
                </div>
              </div>

              <div className="attendance-metric-card">
                <CheckCircle2 size={20} className="metric-icon success-icon" />
                <div>
                  <small>CHECKED IN</small>
                  <strong>{attendance?.totalCheckedIn ?? 0}</strong>
                </div>
              </div>

              <div className="attendance-metric-card">
                <DoorOpen size={20} className="metric-icon active-icon" />
                <div>
                  <small>CURRENTLY INSIDE</small>
                  <strong>{attendance?.currentlyInside ?? 0}</strong>
                </div>
              </div>

              <div className="attendance-metric-card">
                <UserCheck size={20} className="metric-icon" />
                <div>
                  <small>ATTENDANCE RATE</small>
                  <strong>{attendance?.attendancePercentage ?? 0}%</strong>
                </div>
              </div>
            </div>

            {/* ACTION & SEARCH BAR */}
            <div className="attendance-controls-bar">
              <div className="attendance-search-box">
                <Search size={16} className="search-icon" />
                <input
                  type="text"
                  placeholder="Filter attendees by name or ticket number..."
                  value={attendanceSearch}
                  onChange={e => setAttendanceSearch(e.target.value)}
                />
              </div>

              <button
                type="button"
                className="launch-scanner-cta-btn"
                onClick={() => setShowScanner(true)}
              >
                <QrCode size={16} />
                <span>Launch QR Scanner</span>
              </button>
            </div>

            {/* ATTENDEE LIST TABLE */}
            {attendance?.attendees && attendance.attendees.length > 0 ? (
              <div className="attendance-table-container">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>ATTENDEE</th>
                      <th>TICKET NO.</th>
                      <th>STATUS</th>
                      <th>CHECK-IN TIME</th>
                      <th>LOCATION</th>
                      <th>ACTIONS</th>
                    </tr>
                  </thead>
                  <tbody>
                    {attendance.attendees
                      .filter(a => {
                        if (!attendanceSearch.trim()) return true;
                        const q = attendanceSearch.toLowerCase();
                        return (
                          (a.attendeeName || "").toLowerCase().includes(q) ||
                          (a.ticketNumber || "").toLowerCase().includes(q)
                        );
                      })
                      .map(a => (
                        <tr key={a.ticketId}>
                          <td>
                            <strong className="attendee-tbl-name">{a.attendeeName}</strong>
                            <small className="attendee-tbl-phone">{a.maskedPhone || "—"}</small>
                          </td>
                          <td>
                            <span className="attendee-tbl-code">{a.ticketNumber}</span>
                          </td>
                          <td>
                            <span className={`tbl-status-pill ${a.isCheckedIn ? "status-in" : "status-pending"}`}>
                              {a.isCheckedIn ? "Admitted" : "Pending Check-In"}
                            </span>
                          </td>
                          <td>
                            {a.checkedInAt ? (
                              <span className="tbl-time">{new Date(a.checkedInAt).toLocaleTimeString("en-IN", { hour: "numeric", minute: "2-digit" })}</span>
                            ) : "—"}
                          </td>
                          <td>
                            <span className={`tbl-inside-pill ${a.isCurrentlyInside ? "inside" : "outside"}`}>
                              {a.isCurrentlyInside ? "Inside Venue" : "Outside"}
                            </span>
                          </td>
                          <td>
                            {!a.isCheckedIn ? (
                              <button
                                type="button"
                                className="tbl-action-checkin-btn"
                                onClick={() => handleQuickCheckIn(a.ticketId)}
                                disabled={attendanceActionLoading === a.ticketId}
                              >
                                {attendanceActionLoading === a.ticketId ? "..." : "Check In"}
                              </button>
                            ) : attendance?.allowReEntry ? (
                              a.isCurrentlyInside ? (
                                <button
                                  type="button"
                                  className="tbl-action-exit-btn"
                                  onClick={() => handleReEntry(a.ticketId, true)}
                                  disabled={attendanceActionLoading === a.ticketId}
                                  title="Record Pass-Out / Exit"
                                >
                                  <LogOut size={13} />
                                  <span>Exit</span>
                                </button>
                              ) : (
                                <button
                                  type="button"
                                  className="tbl-action-reentry-btn"
                                  onClick={() => handleReEntry(a.ticketId, false)}
                                  disabled={attendanceActionLoading === a.ticketId}
                                  title="Confirm Re-Entry"
                                >
                                  <DoorOpen size={13} />
                                  <span>Re-Enter</span>
                                </button>
                              )
                            ) : (
                              <span className="tbl-admitted-text">Admitted</span>
                            )}
                          </td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="attendance-empty-state">
                <Users size={36} />
                <p>No ticket passes issued yet for this workshop.</p>
              </div>
            )}
          </section>
        )}


        {/* TAB 3: FEEDBACK */}
        {activeTab === "feedback" && (
          <section className="workshop-tab-content">
            <div className="detail-section-heading">
              <span>03</span>
              <h2>Student Reviews & Feedback</h2>
            </div>

            {loadingFeedback ? (
              <LoadingState label="Loading feedback..." />
            ) : feedback.length === 0 ? (
              <EmptyState
                title="No reviews submitted yet"
                description="No student reviews or ratings have been submitted for this workshop."
              />
            ) : (
              <>
                {/* RATING BREAKDOWN WIDGET */}
                <div className="rating-summary-widget">
                  <div className="rating-score-block">
                    <span className="avg-score-number">{avgRating}</span>
                    <div className="score-stars">
                      {"★".repeat(Math.round(Number(avgRating)))}
                      {"☆".repeat(5 - Math.round(Number(avgRating)))}
                    </div>
                    <small>Based on {totalReviews} student reviews</small>
                  </div>

                  <div className="rating-bars-block">
                    {[5, 4, 3, 2, 1].map((star) => {
                      const count = ratingCounts[star] || 0;
                      const percent = totalReviews > 0 ? (count / totalReviews) * 100 : 0;
                      return (
                        <div className="rating-bar-row" key={star}>
                          <span className="star-label">{star} ★</span>
                          <div className="star-bar-track">
                            <div className="star-bar-fill" style={{ width: `${percent}%` }} />
                          </div>
                          <span className="star-count-label">{count}</span>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* RATING FILTER BUTTONS */}
                <div className="feedback-filter-buttons">
                  <button
                    type="button"
                    className={`filter-chip ${ratingFilter === "ALL" ? "active" : ""}`}
                    onClick={() => setRatingFilter("ALL")}
                  >
                    ALL ({totalReviews})
                  </button>
                  {[5, 4, 3, 2, 1].map((star) => (
                    <button
                      key={star}
                      type="button"
                      className={`filter-chip ${ratingFilter === String(star) ? "active" : ""}`}
                      onClick={() => setRatingFilter(String(star))}
                    >
                      {star} ★ ({ratingCounts[star] || 0})
                    </button>
                  ))}
                </div>

                {/* FEEDBACK LIST */}
                <div className="feedback-list">
                  {filteredFeedback.length === 0 ? (
                    <EmptyState
                      title="No matching reviews"
                      description={`No reviews found with ${ratingFilter} star rating.`}
                    />
                  ) : (
                    filteredFeedback.map((item) => (
                      <article className="feedback-card" key={item.id}>
                        <div className="feedback-card-header">
                          <div className="feedback-rating-stars">
                            {"★".repeat(Math.max(0, Math.min(5, item.rating)))}
                            {"☆".repeat(5 - Math.max(0, Math.min(5, item.rating)))}
                          </div>
                          <span className="feedback-date">{formatDate(item.createdAt)}</span>
                        </div>

                        <p className="feedback-comment">“{item.comment}”</p>

                        <div className="feedback-author">
                          <strong>{item.studentName}</strong>
                          <span className="feedback-verified">VERIFIED STUDENT</span>
                        </div>
                      </article>
                    ))
                  )}
                </div>
              </>
            )}
          </section>
        )}
      </div>

      {/* CONFIRMATION DIALOGS */}
      <ConfirmDialog
        open={confirmModal === "submit"}
        title="Submit Workshop for Approval?"
        description="Submitting will send your workshop to Ethos administration for review and scheduling."
        confirmLabel="SUBMIT WORKSHOP"
        cancelLabel="CANCEL"
        onConfirm={executeSubmit}
        onCancel={() => setConfirmModal(null)}
        loading={actionLoading === "submit"}
      />

      <ConfirmDialog
        open={confirmModal === "cancel"}
        title="Cancel Workshop?"
        description="Are you sure you want to cancel this workshop? This action cannot be undone."
        confirmLabel="CANCEL WORKSHOP"
        cancelLabel="KEEP WORKSHOP"
        onConfirm={executeCancel}
        onCancel={() => setConfirmModal(null)}
        loading={actionLoading === "cancel"}
        destructive
      />
    </main>
  );
}
