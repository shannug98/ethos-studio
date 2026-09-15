import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Ticket, Calendar, Clock, MapPin, User, CheckCircle, ArrowRight, ShieldCheck } from "lucide-react";
import { workshopsApi } from "../../services/workshopsApi";
import { useAuth } from "../../context/AuthContext";
import WorkshopPassModal from "../../components/student/WorkshopPassModal";
import "./StudentWorkshops.css";

export default function StudentMyWorkshops() {
  const navigate = useNavigate();
  const { user } = useAuth();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [bookings, setBookings] = useState([]);
  const [activePassBooking, setActivePassBooking] = useState(null);

  useEffect(() => {
    loadBookings();
  }, []);

  async function loadBookings() {
    setLoading(true);
    setError("");
    try {
      const res = await workshopsApi.getMyWorkshops();
      setBookings(Array.isArray(res) ? res : []);
    } catch (err) {
      console.error("Failed to load workshop bookings:", err);
      setError("Unable to load bookings. Please refresh the page.");
    } finally {
      setLoading(false);
    }
  }

  function formatDate(isoStr) {
    if (!isoStr) return "TBA";
    return new Date(isoStr).toLocaleDateString("en-IN", {
      month: "long",
      day: "numeric",
      year: "numeric",
    });
  }

  function formatDayAndMonth(isoStr) {
    if (!isoStr) return { day: "--", month: "---" };
    try {
      const d = new Date(isoStr);
      return {
        day: d.getDate(),
        month: d.toLocaleDateString("en-US", { month: "short" }).toUpperCase(),
      };
    } catch {
      return { day: "--", month: "---" };
    }
  }

  function formatTime(timeStr) {
    if (!timeStr) return "";
    const [h, m] = timeStr.split(":");
    const hours = parseInt(h, 10);
    const ampm = hours >= 12 ? "PM" : "AM";
    const formattedHours = hours % 12 || 12;
    return `${formattedHours}:${m} ${ampm}`;
  }

  const confirmedBookings = bookings.filter((b) => b.status === 2 || b.status === "Confirmed");

  return (
    <div className="student-classes-page student-workshops-page">
      <div className="student-classes-container">

        {/* WORKSHOPS SUBNAV BAR */}
        <div className="student-workshops-subnav">
          <div className="student-workshops-subnav-left">
            <span className="student-card-eyebrow">WORKSHOP ADMISSIONS & PASSES</span>
            <h2 className="student-workshops-subnav-title">My Booked Workshops</h2>
          </div>
          <div className="student-workshops-subnav-right">
            <Link to="/student/workshops" className="student-subnav-pill">
              ← Explore Workshops
            </Link>
            <Link to="/student/classes" className="student-subnav-pill">
              Regular Classes
            </Link>
          </div>
        </div>

        {error && (
          <div className="student-alert student-alert--error">
            <span>⚠️ {error}</span>
            <button type="button" onClick={() => setError("")}>✕</button>
          </div>
        )}

        {loading ? (
          <div className="student-loading-wrap">
            <div className="student-spinner" />
            <span>Loading your workshop passes...</span>
          </div>
        ) : (
          <>
            <section className="student-section-block">
              <div className="student-section-header">
                <div>
                  <span className="student-card-eyebrow">CONFIRMED PASSES</span>
                  <h3>Active Workshop Admissions ({confirmedBookings.length})</h3>
                </div>
              </div>

              {confirmedBookings.length === 0 ? (
                <div className="student-empty-state">
                  <div className="student-empty-icon">🎟️</div>
                  <h4>No confirmed workshop bookings yet</h4>
                  <p>Discover masterclasses with expert choreographers and reserve your spot.</p>
                  <button
                    type="button"
                    className="student-enroll-btn"
                    style={{ maxWidth: "240px", margin: "1.5rem auto 0" }}
                    onClick={() => navigate("/student/workshops")}
                  >
                    BROWSE WORKSHOPS →
                  </button>
                </div>
              ) : (
                <div className="student-catalog-grid">
                  {confirmedBookings.map((b) => {
                    const { day, month } = formatDayAndMonth(b.workshopDate);
                    const bookingRef = b.bookingReference || `ETH-WS-${(b.id || "").replace(/-/g, "").slice(0, 8).toUpperCase()}`;

                    return (
                      <div key={b.id} className="student-class-card student-class-card--enrolled booking-pass-card">
                        {/* TOP PASS BADGES */}
                        <div className="booking-pass-top">
                          <span className="pass-type-tag">
                            <Ticket size={12} />
                            WORKSHOP PASS
                          </span>
                          <span className="booking-status-tag">
                            <span className="status-dot-green" />
                            CONFIRMED
                          </span>
                        </div>

                        {/* TITLE */}
                        <h4 className="booking-pass-title">{b.workshopTitle}</h4>

                        {/* DATE BLOCK */}
                        <div className="booking-pass-date-block">
                          <div className="pass-calendar-box">
                            <span className="cal-day">{day}</span>
                            <span className="cal-month">{month}</span>
                          </div>
                          <div className="pass-date-texts">
                            <strong className="pass-full-date">{formatDate(b.workshopDate)}</strong>
                            <span className="pass-time-range">
                              {b.startTime && b.endTime
                                ? `${formatTime(b.startTime)} – ${formatTime(b.endTime)}`
                                : "Time scheduled upon check-in"}
                            </span>
                          </div>
                        </div>

                        {/* META ROWS */}
                        <div className="booking-pass-meta">
                          <div className="booking-meta-row">
                            <span className="booking-meta-label">Instructor</span>
                            <strong className="booking-meta-value">{b.trainerName || "Elite Faculty"}</strong>
                          </div>
                          <div className="booking-meta-row">
                            <span className="booking-meta-label">Venue</span>
                            <strong className="booking-meta-value">{b.venue || "Ethos Main Arena"}</strong>
                          </div>
                          <div className="booking-meta-row">
                            <span className="booking-meta-label">Payment</span>
                            <strong className="booking-meta-value price-tag">
                              ₹{Number(b.price || 0).toLocaleString("en-IN")}
                            </strong>
                          </div>
                        </div>

                        {/* SECONDARY REF INFO */}
                        <div className="booking-pass-ref">
                          <span>Ref: <strong>{bookingRef}</strong></span>
                        </div>

                        {/* CTA ACTION */}
                        <div className="booking-pass-action">
                          <button
                            type="button"
                            className="view-pass-btn"
                            onClick={() => setActivePassBooking(b)}
                          >
                            <Ticket size={14} />
                            <span>VIEW WORKSHOP PASS</span>
                          </button>
                        </div>
                      </div>
                    );
                  })}
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
