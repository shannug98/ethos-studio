import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import LoadingState from "../../components/trainer/LoadingState";
import ErrorState from "../../components/trainer/ErrorState";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import "./TrainerPerformance.css";

const formatDate = (value) => {
  if (!value) return "—";

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleDateString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
};

const formatNumber = (value) => {
  if (value === null || value === undefined) return "0";
  return Number(value).toLocaleString("en-IN");
};

const formatRating = (value) => {
  if (value === null || value === undefined) return "0.00";
  return Number(value).toFixed(2);
};

const clampPercentage = (value) => {
  const number = Number(value);

  if (Number.isNaN(number)) return 0;

  return Math.min(100, Math.max(0, number));
};

const getRatingLabel = (rating) => {
  const value = Number(rating);

  if (value >= 4.75) return "Exceptional";
  if (value >= 4.5) return "Excellent";
  if (value >= 4) return "Strong";
  if (value >= 3) return "Good";
  if (value >= 2) return "Developing";

  return "Needs Attention";
};

const getRatingStars = (rating) => {
  const value = Number(rating) || 0;
  const rounded = Math.round(value);

  return Array.from({ length: 5 }, (_, index) =>
    index < rounded ? "★" : "☆"
  );
};

const formatMonthYear = (value) => {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleDateString("en-IN", { month: "short", year: "numeric" }).toUpperCase();
};

const formatMonthPeriod = (start, end) => {
  const now = new Date();
  const sDate = start ? new Date(start) : new Date(now.getFullYear(), now.getMonth(), 1);
  const eDate = end ? new Date(end) : new Date(sDate.getFullYear(), sDate.getMonth() + 1, 0);

  const startStr = sDate.toLocaleDateString("en-IN", { day: "2-digit", month: "short" }).toUpperCase();
  const endStr = eDate.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" }).toUpperCase();
  return `${startStr} — ${endStr}`;
};

export default function TrainerPerformance() {
  const mountedRef = useRef(true);

  const [performance, setPerformance] = useState(null);
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [historyLoading, setHistoryLoading] = useState(true);
  const [error, setError] = useState("");
  const [historyError, setHistoryError] = useState("");

  const loadPerformance = useCallback(async () => {
    try {
      setLoading(true);
      setError("");

      const response = await trainerApi.getPerformance();
      if (!mountedRef.current) return;
      setPerformance(response?.data ?? response);
    } catch (err) {
      if (!mountedRef.current) return;
      console.error("Failed to load trainer performance:", err);
      setError(
        getApiErrorMessage(err, "Unable to load your performance data right now.")
      );
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  const loadHistory = useCallback(async () => {
    try {
      setHistoryLoading(true);
      setHistoryError("");

      const response = await trainerApi.getPerformanceHistory();
      if (!mountedRef.current) return;
      const data = response?.data ?? response;

      setHistory(Array.isArray(data) ? data : []);
    } catch (err) {
      if (!mountedRef.current) return;
      console.error("Failed to load trainer performance history:", err);
      setHistoryError(
        getApiErrorMessage(err, "Performance history is currently unavailable.")
      );
    } finally {
      if (mountedRef.current) {
        setHistoryLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    loadPerformance();
    loadHistory();
    return () => {
      mountedRef.current = false;
    };
  }, [loadPerformance, loadHistory]);

  // Overall Performance: Lifetime / Accumulated, never resets
  const overall = useMemo(() => {
    if (!performance) {
      return {
        rating: null,
        students: 0,
        workshops: 0,
        attendance: 0,
        feedbacks: 0,
        sessions: 0,
      };
    }

    const o = performance.overall || {};
    return {
      rating:
        o.rating ??
        performance.overallRating ??
        performance.averageFeedbackRating ??
        null,

      students:
        o.students ??
        performance.totalStudentsTaught ??
        performance.uniqueStudents ??
        0,

      workshops:
        o.workshops ??
        performance.totalWorkshopsCompleted ??
        performance.workshopsConducted ??
        0,

      attendance:
        o.attendancePercentage ??
        performance.attendanceRate ??
        performance.attendancePercentage ??
        0,

      feedbacks:
        o.feedback ??
        performance.totalFeedbacksReceived ??
        performance.feedbackCount ??
        0,

      sessions: o.sessions ?? performance.sessionsConducted ?? 0,
    };
  }, [performance]);

  // Monthly Performance: Current calendar month cycle, auto-resets on the 1st
  const monthly = useMemo(() => {
    if (!performance) {
      return {
        periodStart: null,
        periodEnd: null,
        rating: null,
        students: 0,
        workshops: 0,
        attendance: 0,
        feedbacks: 0,
        sessions: 0,
      };
    }

    const m = performance.monthly || {};
    return {
      periodStart: m.periodStart || null,
      periodEnd: m.periodEnd || null,
      rating: m.rating ?? null,
      students: m.students ?? 0,
      workshops: m.workshops ?? 0,
      attendance: m.attendancePercentage ?? 0,
      feedbacks: m.feedback ?? 0,
      sessions: m.sessions ?? 0,
    };
  }, [performance]);

  const ratingBreakdown = useMemo(() => {
    const rb = performance?.ratingBreakdown || {};
    const five = rb.fiveStars ?? rb.FiveStars ?? 0;
    const four = rb.fourStars ?? rb.FourStars ?? 0;
    const three = rb.threeStars ?? rb.ThreeStars ?? 0;
    const two = rb.twoStars ?? rb.TwoStars ?? 0;
    const one = rb.oneStar ?? rb.OneStar ?? 0;
    const total = five + four + three + two + one;

    return [
      { stars: 5, count: five, pct: total > 0 ? Math.round((five / total) * 100) : 0 },
      { stars: 4, count: four, pct: total > 0 ? Math.round((four / total) * 100) : 0 },
      { stars: 3, count: three, pct: total > 0 ? Math.round((three / total) * 100) : 0 },
      { stars: 2, count: two, pct: total > 0 ? Math.round((two / total) * 100) : 0 },
      { stars: 1, count: one, pct: total > 0 ? Math.round((one / total) * 100) : 0 },
    ];
  }, [performance]);

  const overallRatingLabel = getRatingLabel(overall.rating);

  if (loading) {
    return <LoadingState label="Loading performance data..." />;
  }

  if (error && !performance) {
    return (
      <ErrorState
        title="Performance unavailable"
        description={error}
        action={
          <button
            type="button"
            className="performance-primary-button"
            onClick={loadPerformance}
          >
            TRY AGAIN
          </button>
        }
      />
    );
  }

  return (
    <div className="trainer-performance-page">
      <div className="performance-shell">

        {/* HEADER */}
        <header className="performance-header">
          <div>
            <span className="performance-eyebrow">
              TRAINER PORTAL / PERFORMANCE
            </span>

            <h1>
              Your work,
              <br />
              <em>measured.</em>
            </h1>

            <p>
              A real-time view of your teaching activity, student reach,
              attendance, and feedback.
            </p>
          </div>

          <div className="performance-header-meta">
            <span>MONTHLY PERFORMANCE</span>
            <strong>{formatMonthPeriod(monthly.periodStart, monthly.periodEnd)}</strong>
          </div>
        </header>

        {/* 01 OVERALL PERFORMANCE */}
        <section className="performance-rating-hero">
          <div className="performance-rating-copy">
            <span className="performance-section-label">
              OVERALL PERFORMANCE
            </span>

            <div className="performance-rating-number">
              {overall.rating !== null ? formatRating(overall.rating) : "—"}
              <span>/5</span>
            </div>

            <div className="performance-stars">
              {getRatingStars(overall.rating ?? 0).map((star, index) => (
                <span key={index}>{star}</span>
              ))}
            </div>

            <strong className="performance-rating-label">
              {overallRatingLabel}
            </strong>

            <p>
              Based on {formatNumber(overall.feedbacks)} student{" "}
              {overall.feedbacks === 1 ? "review" : "reviews"} received
              across your workshops.
            </p>
          </div>

          <div className="performance-rating-mark">
            <span>ETHOS</span>
            <strong>{overall.rating !== null ? formatRating(overall.rating) : "—"}</strong>
            <small>OVERALL RATING</small>
          </div>
        </section>

        {/* 02 MONTHLY PERFORMANCE */}
        <section className="performance-section">
          <div className="performance-section-heading">
            <div>
              <small>MONTHLY PERFORMANCE</small>
              <h2>Current month snapshot.</h2>
            </div>
            <div className="performance-period-pill">
              {formatMonthPeriod(monthly.periodStart, monthly.periodEnd)}
            </div>
          </div>

          <div className="performance-metric-grid">
            <article className="performance-metric-card">
              <small>STUDENTS TAUGHT</small>
              <strong>{formatNumber(monthly.students)}</strong>
              <p>Students reached in the current calendar month.</p>
            </article>

            <article className="performance-metric-card">
              <small>WORKSHOPS COMPLETED</small>
              <strong>{formatNumber(monthly.workshops)}</strong>
              <p>Workshops concluded in this monthly cycle.</p>
            </article>

            <article className="performance-metric-card">
              <small>SESSIONS CONDUCTED</small>
              <strong>{formatNumber(monthly.sessions)}</strong>
              <p>Sessions held during this monthly cycle.</p>
            </article>

            <article className="performance-metric-card">
              <small>FEEDBACK RECEIVED</small>
              <strong>{formatNumber(monthly.feedbacks)}</strong>
              <p>Student reviews recorded this month.</p>
            </article>
          </div>

          {/* Monthly rating card */}
          <div className="monthly-rating-card">
            <div className="monthly-rating-header">
              <small>MONTHLY RATING</small>
              {monthly.feedbacks > 0 && monthly.rating !== null ? (
                <div className="monthly-rating-val">
                  <strong>{formatRating(monthly.rating)}</strong>
                  <span>/5</span>
                  <div className="performance-stars">
                    {getRatingStars(monthly.rating).map((star, idx) => (
                      <span key={idx}>{star}</span>
                    ))}
                  </div>
                </div>
              ) : (
                <div className="monthly-empty-tag">— No feedback yet</div>
              )}
            </div>
            <p className="monthly-rating-meta">
              {monthly.feedbacks > 0 && monthly.rating !== null
                ? `Based on ${formatNumber(monthly.feedbacks)} student ${monthly.feedbacks === 1 ? "review" : "reviews"} received this month.`
                : "No student feedback has been submitted for this month's sessions yet. Rating will appear as students submit reviews."}
            </p>
          </div>
        </section>

        {/* 03 STUDENT PARTICIPATION */}
        <section className="performance-section performance-attendance-section">
          <div className="performance-section-heading">
            <div>
              <small>STUDENT PARTICIPATION</small>
              <h2>Attendance & reach.</h2>
            </div>
          </div>

          <div className="performance-attendance-card">
            <div className="attendance-main">
              <div className="attendance-value">
                {Number(overall.attendance).toFixed(1)}
                <span>%</span>
              </div>

              <div>
                <strong>Attendance rate</strong>
                <p>
                  Confirmed bookings measured against approved workshop
                  capacity across all your workshops.
                </p>
              </div>
            </div>

            <div className="attendance-bar-wrapper">
              <div className="attendance-bar">
                <span
                  style={{
                    width: `${clampPercentage(overall.attendance)}%`,
                  }}
                />
              </div>

              <div className="attendance-bar-labels">
                <span>0%</span>
                <strong>
                  {Number(overall.attendance).toFixed(1)}%
                </strong>
                <span>100%</span>
              </div>
            </div>

            <div className="attendance-stats">
              <div>
                <span>LIFETIME ATTENDANCE</span>
                <strong>
                  {formatNumber(performance?.attendanceCount ?? overall.students)}
                </strong>
              </div>

              <div>
                <span>UNIQUE STUDENTS</span>
                <strong>
                  {formatNumber(overall.students)}
                </strong>
              </div>

              <div>
                <span>SESSIONS CONDUCTED</span>
                <strong>
                  {formatNumber(overall.sessions)}
                </strong>
              </div>
            </div>
          </div>
        </section>

        {/* 04 STUDENT FEEDBACK */}
        <section className="performance-section">
          <div className="performance-section-heading">
            <div>
              <small>STUDENT FEEDBACK</small>
              <h2>What your students are saying.</h2>
            </div>
          </div>

          <div className="feedback-performance-grid">
            <div className="feedback-score-card">
              <span>LIFETIME AVERAGE RATING</span>

              <strong>
                {overall.rating !== null ? formatRating(overall.rating) : "—"}
              </strong>

              <div className="performance-stars">
                {getRatingStars(overall.rating ?? 0).map((star, index) => (
                  <span key={index}>{star}</span>
                ))}
              </div>

              <p>
                From {formatNumber(overall.feedbacks)} total{" "}
                {overall.feedbacks === 1 ? "response" : "responses"}.
              </p>
            </div>

            <div className="feedback-breakdown-card">
              <span>RATING DISTRIBUTION</span>

              <div className="trainer-stars-distribution">
                {ratingBreakdown.map((item) => (
                  <div key={item.stars} className="trainer-dist-row">
                    <span className="trainer-dist-label">{item.stars} ★</span>
                    <div className="trainer-dist-track">
                      <div
                        className="trainer-dist-fill"
                        style={{ width: `${item.pct}%` }}
                      />
                    </div>
                    <span className="trainer-dist-count">
                      {item.count} <small className="trainer-dist-pct">({item.pct}%)</small>
                    </span>
                  </div>
                ))}
              </div>

              <p className="privacy-reassurance-note">
                🔒 Anonymous aggregation. Attendee identities, contact details, and individual written reviews are strictly protected and withheld.
              </p>
            </div>

            <div className="feedback-summary-card">
              <span>FEEDBACK SIGNAL</span>

              <h3>{overallRatingLabel}</h3>

              <p>
                Your overall rating is calculated from verified student and guest feedback
                collected across all completed workshops.
              </p>

              <Link
                to="/trainer/workshops"
                className="performance-text-link"
              >
                VIEW WORKSHOPS
                <span>↗</span>
              </Link>
            </div>
          </div>
        </section>

        {/* 05 HISTORY */}
        <section className="performance-section">
          <div className="performance-section-heading">
            <div>
              <small>PERFORMANCE HISTORY</small>
              <h2>Your performance over time.</h2>
            </div>
          </div>

          {historyLoading ? (
            <LoadingState label="Loading performance history..." />
          ) : historyError ? (
            <div className="performance-history-empty" role="alert">
              <strong>History unavailable</strong>
              <p>{historyError}</p>
            </div>
          ) : history.length === 0 ? (
            <div className="performance-history-empty">
              <span>NO HISTORICAL SNAPSHOTS</span>
              <h3>Your performance timeline will appear here.</h3>
              <p>
                Historical monthly cycles will be displayed as sessions conclude and months complete.
              </p>
            </div>
          ) : (
            <div className="performance-history-table">
              <div className="history-row history-heading">
                <span>CYCLE</span>
                <span>RATING</span>
                <span>STUDENTS</span>
                <span>WORKSHOPS</span>
                <span>ATTENDANCE</span>
                <span>RESPONSES</span>
              </div>

              {history.map((item, idx) => {
                const itemRating = item.overallRating ?? item.averageFeedbackRating;
                const feedbackCount = item.totalFeedbacksReceived ?? item.feedbackCount ?? 0;
                return (
                  <div
                    className="history-row"
                    key={item.id && item.id !== "00000000-0000-0000-0000-000000000000" ? item.id : `${item.snapshotDate}-${idx}`}
                  >
                    <span>{formatMonthYear(item.snapshotDate)}</span>

                    <strong>
                      {feedbackCount > 0 && itemRating !== null
                        ? `${formatRating(itemRating)} ★`
                        : "—"}
                    </strong>

                    <span>
                      {formatNumber(
                        item.totalStudentsTaught ??
                          item.uniqueStudents
                      )}
                    </span>

                    <span>
                      {formatNumber(
                        item.totalWorkshopsCompleted ??
                          item.workshopsConducted
                      )}
                    </span>

                    <span>
                      {Number(
                        item.attendanceRate ??
                          item.attendancePercentage ??
                          0
                      ).toFixed(1)}
                      %
                    </span>

                    <span>
                      {formatNumber(feedbackCount)}
                    </span>
                  </div>
                );
              })}
            </div>
          )}
        </section>

        {/* SYSTEM NOTE */}
        {performance?.notes && (
          <section className="performance-system-note">
            <span>SYSTEM NOTE</span>

            <p>{performance.notes}</p>
          </section>
        )}

        {/* FOOTER NAV */}
        <div className="performance-footer-nav">
          <Link to="/trainer/workshops">
            ← WORKSHOPS
          </Link>

          <Link to="/trainer/tier">
            VIEW TRAINER TIER →
          </Link>
        </div>

      </div>
    </div>
  );
}
