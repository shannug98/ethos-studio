import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { workshopsApi } from "../../services/workshopsApi";
import { studentApi } from "../../services/studentApi";
import { useAuth } from "../../context/AuthContext";
import "./StudentWorkshops.css";

function loadRazorpayScript() {
  return new Promise((resolve) => {
    if (window.Razorpay) {
      resolve(true);
      return;
    }
    const script = document.createElement("script");
    script.src = "https://checkout.razorpay.com/v1/checkout.js";
    script.onload = () => resolve(true);
    script.onerror = () => resolve(false);
    document.body.appendChild(script);
  });
}

export default function StudentWorkshops() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const [loading, setLoading] = useState(true);
  const [bookingWorkshopId, setBookingWorkshopId] = useState(null);
  const [error, setError] = useState("");
  const [successMsg, setSuccessMsg] = useState("");

  const [workshops, setWorkshops] = useState([]);
  const [myBookings, setMyBookings] = useState([]);
  const [activePackage, setActivePackage] = useState(null);

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    setError("");
    try {
      const [wsList, bookingsList, pkgRes] = await Promise.all([
        workshopsApi.getApprovedWorkshops().catch(() => []),
        workshopsApi.getMyWorkshops().catch(() => []),
        studentApi.getActivePackage().catch(() => null),
      ]);

      setWorkshops(Array.isArray(wsList) ? wsList : []);
      setMyBookings(Array.isArray(bookingsList) ? bookingsList : []);
      setActivePackage(pkgRes || null);
    } catch (err) {
      console.error("Failed to load workshops:", err);
      setError("Unable to load workshops. Please refresh the page.");
    } finally {
      setLoading(false);
    }
  }

  const confirmedBookingWorkshopIds = new Set(
    myBookings
      .filter((b) => b.status === 2 || b.status === "Confirmed")
      .map((b) => b.workshopId)
  );

  async function handleBookWorkshop(workshop) {
    setError("");
    setSuccessMsg("");

    if (confirmedBookingWorkshopIds.has(workshop.id)) {
      setError("You have already booked this workshop.");
      return;
    }

    if (workshop.isFull || workshop.remainingSeats <= 0) {
      setError("This workshop is already fully booked.");
      return;
    }

    const scriptLoaded = await loadRazorpayScript();
    if (!scriptLoaded) {
      setError("Failed to load Razorpay payment gateway. Please check your connection.");
      return;
    }

    setBookingWorkshopId(workshop.id);

    try {
      // 1. Create order on backend (authoritative pricing computed server-side)
      const order = await workshopsApi.createWorkshopOrder(workshop.id);

      // 2. Open Razorpay Checkout modal
      const options = {
        key: order.razorpayKeyId,
        amount: Math.round(order.amount * 100),
        currency: order.currency || "INR",
        name: "ETHOS DANCE STUDIO",
        description: `Workshop Booking: ${order.workshopTitle}`,
        order_id: order.razorpayOrderId,
        prefill: {
          name: user?.fullName || "Ethos Student",
          contact: user?.phone || "",
        },
        theme: {
          color: "#e50914",
        },
        handler: async function (response) {
          try {
            // 3. Verify payment on backend
            await workshopsApi.verifyWorkshopPayment(workshop.id, {
              transactionId: order.transactionId,
              razorpayOrderId: response.razorpay_order_id,
              razorpayPaymentId: response.razorpay_payment_id,
              razorpaySignature: response.razorpay_signature,
            });

            setSuccessMsg(`Successfully booked "${workshop.title}"!`);
            await loadData();
          } catch (verErr) {
            console.error("Payment verification failed:", verErr);
            setError(verErr?.response?.data?.message || "Payment verification failed. Please contact support.");
          } finally {
            setBookingWorkshopId(null);
          }
        },
        modal: {
          ondismiss: function () {
            setBookingWorkshopId(null);
          },
        },
      };

      const rzp = new window.Razorpay(options);
      rzp.on("payment.failed", function (resp) {
        console.error("Razorpay payment failed:", resp.error);
        setError(`Payment failed: ${resp.error?.description || "Transaction declined"}`);
        setBookingWorkshopId(null);
      });
      rzp.open();
    } catch (err) {
      console.error("Order creation error:", err);
      const msg = err?.response?.data?.message || err?.message || "Failed to start booking.";
      setError(msg);
      setBookingWorkshopId(null);
    }
  }

  function formatDate(isoStr) {
    if (!isoStr) return "TBA";
    return new Date(isoStr).toLocaleDateString("en-IN", {
      weekday: "short",
      day: "numeric",
      month: "short",
      year: "numeric",
    });
  }

  function formatTime(timeStr) {
    if (!timeStr) return "";
    const [h, m] = timeStr.split(":");
    const hours = parseInt(h, 10);
    const ampm = hours >= 12 ? "PM" : "AM";
    const formattedHours = hours % 12 || 12;
    return `${formattedHours}:${m} ${ampm}`;
  }

  return (
    <div className="student-classes-page student-workshops-page">
      <div className="student-classes-container">

        {/* WORKSHOPS SUBNAV BAR */}
        <div className="student-workshops-subnav">
          <div className="student-workshops-subnav-left">
            <span className="student-card-eyebrow">MASTERCLASSES & INTENSIVES</span>
            <h2 className="student-workshops-subnav-title">Studio Workshops</h2>
          </div>
          <div className="student-workshops-subnav-right">
            <Link to="/student/classes" className="student-subnav-pill">
              Regular Classes
            </Link>
            <Link to="/student/my-workshops" className="student-subnav-pill">
              My Bookings ({myBookings.filter(b => b.status === 2 || b.status === "Confirmed").length})
            </Link>
          </div>
        </div>

        {/* STUDENT DISCOUNT STATUS BANNER */}
        <section className="student-pass-banner">
          <div className="student-pass-status-badge">
            <span className="student-status-dot" />
            <span>EXCLUSIVE STUDENT BENEFIT</span>
          </div>

          <div className="student-pass-details">
            <div className="student-pass-title-group">
              <h3>Special Student Rate: ₹100 Off</h3>
              <p>
                {activePackage
                  ? `Active Membership: ${activePackage.packageName} • You qualify for the fixed ₹100 student discount on all starting prices!`
                  : "Active monthly pass holders receive an exclusive ₹100 discount on original starting prices."}
              </p>
            </div>

            <div className="student-pass-stat">
              <span className="student-pass-stat-num">
                {activePackage ? "ACTIVE" : "STANDARD"}
              </span>
              <span className="student-pass-stat-label">DISCOUNT TIER</span>
            </div>
          </div>
        </section>

        {/* ALERTS */}
        {error && (
          <div className="student-alert student-alert--error">
            <span>⚠️ {error}</span>
            <button type="button" onClick={() => setError("")}>✕</button>
          </div>
        )}
        {successMsg && (
          <div className="student-alert student-alert--success">
            <span>✓ {successMsg}</span>
            <button type="button" onClick={() => setSuccessMsg("")}>✕</button>
          </div>
        )}

        {/* LOADING */}
        {loading ? (
          <div className="student-loading-wrap">
            <div className="student-spinner" />
            <span>Loading upcoming workshops...</span>
          </div>
        ) : (
          <>
            {/* AVAILABLE WORKSHOPS CATALOG */}
            <section className="student-section-block">
              <div className="student-section-header">
                <div>
                  <span className="student-card-eyebrow">EXPLORE SESSIONS</span>
                  <h3>Upcoming Masterclasses & Workshops</h3>
                </div>
                <span className="student-counter-pill">
                  {workshops.length} {workshops.length === 1 ? "Workshop" : "Workshops"} Available
                </span>
              </div>

              {workshops.length === 0 ? (
                <div className="student-empty-state">
                  <div className="student-empty-icon">🎭</div>
                  <h4>No upcoming workshops scheduled</h4>
                  <p>Check back soon for new weekend masterclasses and choreography intensives.</p>
                </div>
              ) : (
                <div className="student-catalog-grid">
                  {workshops.map((w) => {
                    const isBooked = confirmedBookingWorkshopIds.has(w.id);
                    const isProcessing = bookingWorkshopId === w.id;
                    const remaining = w.remainingSeats ?? (w.capacity - (w.bookedSeats || 0));
                    const isFull = w.isFull || remaining <= 0;

                    return (
                      <div
                        key={w.id}
                        className={`student-class-card ${isBooked ? "student-class-card--enrolled" : ""}`}
                      >
                        <div className="student-class-card-header">
                          <span className="student-style-badge">{w.danceStyle}</span>
                          <span className="student-level-badge">{w.level}</span>
                        </div>

                        <h4 className="student-class-title">{w.title}</h4>
                        {w.description && (
                          <p className="student-class-desc">{w.description}</p>
                        )}

                        <div className="student-workshop-meta-rows">
                          <div className="student-schedule-meta-item">
                            <span className="meta-icon">👤</span>
                            <span>Instructor: <strong>{w.trainerName}</strong></span>
                          </div>
                          <div className="student-schedule-meta-item">
                            <span className="meta-icon">📅</span>
                            <span>Date: <strong>{formatDate(w.workshopDate)}</strong></span>
                          </div>
                          <div className="student-schedule-meta-item">
                            <span className="meta-icon">⏱</span>
                            <span>Time: <strong>{formatTime(w.startTime)} - {formatTime(w.endTime)}</strong></span>
                          </div>
                          <div className="student-schedule-meta-item">
                            <span className="meta-icon">📍</span>
                            <span>Venue: {w.venue}</span>
                          </div>
                        </div>

                        {/* LIVE SEATS INDICATOR */}
                        <div className="student-seats-row">
                          <span className="seats-indicator">
                            {isFull ? "🔴 WORKSHOP FULL" : `🟢 ${w.bookedSeats || 0} / ${w.capacity} seats filled (${remaining} remaining)`}
                          </span>
                        </div>

                        {/* PRICING BREAKDOWN */}
                        <div className="student-pricing-box">
                          <div className="pricing-line">
                            <span>Public Price (Current Tier):</span>
                            <strong className="public-price">₹{Number(w.currentPrice || w.price).toLocaleString("en-IN")}</strong>
                          </div>
                          <div className="pricing-line student-benefit-line">
                            <span>Student Price (Fixed):</span>
                            <strong className="student-price">₹{Number(w.studentPrice || (w.startingPrice - 100)).toLocaleString("en-IN")}</strong>
                          </div>
                        </div>

                        <div className="student-class-card-footer">
                          {isBooked ? (
                            <div className="student-booked-banner">
                              ✓ YOU ARE BOOKED
                            </div>
                          ) : (
                            <button
                              type="button"
                              className="student-enroll-btn"
                              disabled={isFull || isProcessing}
                              onClick={() => handleBookWorkshop(w)}
                            >
                              {isProcessing
                                ? "INITIALIZING PAYMENT..."
                                : isFull
                                ? "WORKSHOP FULL"
                                : `BOOK NOW FOR ₹${Number(w.studentPrice || (w.startingPrice - 100)).toLocaleString("en-IN")}`}
                            </button>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </section>
          </>
        )}

      </div>
    </div>
  );
}
