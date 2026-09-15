import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { getDailyActivity } from "../../services/adminApi";
import "./AdminDailyActivity.css";

const formatDate = (value) => {
  if (!value) return "—";

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  }).format(date);
};

const formatDateTime = (value) => {
  if (!value) return "—";

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
};

const formatCurrency = (value) => {
  return new Intl.NumberFormat("en-IN", {
    style: "currency",
    currency: "INR",
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(Number(value ?? 0));
};

export default function AdminDailyActivity() {
  const navigate = useNavigate();
  const { date: paramDate } = useParams();
  const [searchParams] = useSearchParams();
  const date = paramDate || searchParams.get("date");

  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    const loadDailyActivity = async () => {
      try {
        setLoading(true);
        setError("");

        // Ensure date is clean YYYY-MM-DD string
        const cleanDate = date ? String(date).trim().slice(0, 10) : "";
        if (!cleanDate) {
          throw new Error("Invalid date parameter.");
        }

        const response = await getDailyActivity(cleanDate);

        if (!cancelled) {
          setData(response);
        }
      } catch (requestError) {
        if (!cancelled) {
          setError(
            requestError?.response?.data?.message ||
              requestError?.message ||
              "Unable to load daily activity."
          );
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    loadDailyActivity();

    return () => {
      cancelled = true;
    };
  }, [date]);

  const summary = useMemo(() => {
    return data?.summary ?? {};
  }, [data]);

  if (loading) {
    return (
      <section className="daily-activity-page">
        <div className="daily-activity-loading">
          Loading activity for {formatDate(date)}...
        </div>
      </section>
    );
  }

  if (error) {
    return (
      <section className="daily-activity-page">
        <div className="daily-activity-error">
          <strong>Unable to load daily activity</strong>
          <span>{error}</span>

          <button
            type="button"
            onClick={() => navigate("/admin_portal")}
          >
            Return to Command Center
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="daily-activity-page">
      <div className="daily-activity-header">
        <div>
          <button
            type="button"
            className="daily-back-button"
            onClick={() => navigate("/admin_portal")}
          >
            ← Back to Command Center
          </button>

          <p className="daily-eyebrow">DAILY TELEMETRY</p>

          <h1>Activity Details</h1>

          <p>
            Complete administrative activity for{" "}
            <strong>{formatDate(data?.date ?? date)}</strong>
          </p>
        </div>
      </div>

      <div className="daily-summary-grid">
        <article className="daily-summary-card">
          <span>Students Registered</span>
          <strong>{summary.studentsRegistered ?? 0}</strong>
        </article>

        <article className="daily-summary-card">
          <span>Workshop Bookings</span>
          <strong>{summary.workshopBookings ?? 0}</strong>
        </article>

        <article className="daily-summary-card">
          <span>Class Bookings</span>
          <strong>{summary.classBookings ?? 0}</strong>
        </article>

        <article className="daily-summary-card">
          <span>Revenue</span>
          <strong>{formatCurrency(summary.revenue)}</strong>
        </article>

        <article className="daily-summary-card">
          <span>Security Events</span>
          <strong>{summary.securityEvents ?? 0}</strong>
        </article>

        <article className="daily-summary-card">
          <span>Failed Payments</span>
          <strong>{summary.failedPayments ?? 0}</strong>
        </article>
      </div>

      <section className="daily-detail-card">
        <div className="daily-section-heading">
          <div>
            <h2>Workshop Bookings</h2>
            <p>Students registered for workshops on this date.</p>
          </div>

          <span>{data?.workshopBookings?.length ?? 0} records</span>
        </div>

        <div className="daily-table-wrapper">
          <table className="daily-table">
            <thead>
              <tr>
                <th>Student</th>
                <th>Workshop</th>
                <th>Trainer</th>
                <th>Amount</th>
                <th>Status</th>
                <th>Booked At</th>
              </tr>
            </thead>

            <tbody>
              {data?.workshopBookings?.length ? (
                data.workshopBookings.map((booking) => (
                  <tr key={booking.bookingId}>
                    <td>{booking.studentName}</td>
                    <td>{booking.workshopName}</td>
                    <td>{booking.trainerName}</td>
                    <td>{formatCurrency(booking.amount)}</td>
                    <td>
                      <span className="daily-status-badge">
                        {booking.status}
                      </span>
                    </td>
                    <td>{formatDateTime(booking.bookedAt)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan="6" className="daily-empty-cell">
                    No workshop bookings on this date.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="daily-detail-card">
        <div className="daily-section-heading">
          <div>
            <h2>Students Registered</h2>
            <p>New student accounts created on this date.</p>
          </div>

          <span>{data?.students?.length ?? 0} records</span>
        </div>

        <div className="daily-table-wrapper">
          <table className="daily-table">
            <thead>
              <tr>
                <th>Student</th>
                <th>Mobile</th>
                <th>Registered At</th>
              </tr>
            </thead>

            <tbody>
              {data?.students?.length ? (
                data.students.map((student) => (
                  <tr key={student.studentId}>
                    <td>{student.studentName}</td>
                    <td>{student.mobileNumber || "—"}</td>
                    <td>{formatDateTime(student.registeredAt)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan="3" className="daily-empty-cell">
                    No students registered on this date.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="daily-detail-card">
        <div className="daily-section-heading">
          <div>
            <h2>Payments</h2>
            <p>Payment transactions created on this date.</p>
          </div>

          <span>{data?.payments?.length ?? 0} records</span>
        </div>

        <div className="daily-table-wrapper">
          <table className="daily-table">
            <thead>
              <tr>
                <th>Purpose</th>
                <th>Amount</th>
                <th>Status</th>
                <th>Order ID</th>
                <th>Created At</th>
              </tr>
            </thead>

            <tbody>
              {data?.payments?.length ? (
                data.payments.map((payment) => (
                  <tr key={payment.paymentId}>
                    <td>{payment.purpose}</td>
                    <td>{formatCurrency(payment.amount)}</td>
                    <td>
                      <span className="daily-status-badge">
                        {payment.status}
                      </span>
                    </td>
                    <td>{payment.razorpayOrderId || "—"}</td>
                    <td>{formatDateTime(payment.createdAt)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan="5" className="daily-empty-cell">
                    No payments recorded on this date.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="daily-detail-card">
        <div className="daily-section-heading">
          <div>
            <h2>Security Events</h2>
            <p>Security and authorization events for this date.</p>
          </div>

          <span>{data?.securityEvents?.length ?? 0} records</span>
        </div>

        <div className="daily-table-wrapper">
          <table className="daily-table">
            <thead>
              <tr>
                <th>Event</th>
                <th>Severity</th>
                <th>Actor</th>
                <th>Trace ID</th>
                <th>Created At</th>
              </tr>
            </thead>

            <tbody>
              {data?.securityEvents?.length ? (
                data.securityEvents.map((event) => (
                  <tr key={event.id}>
                    <td>{event.eventType}</td>
                    <td>
                      <span className="daily-status-badge">
                        {event.severity}
                      </span>
                    </td>
                    <td>{event.actor || "System"}</td>
                    <td>{event.traceId || "—"}</td>
                    <td>{formatDateTime(event.createdAt)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan="5" className="daily-empty-cell">
                    No security events on this date.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="daily-detail-card">
        <div className="daily-section-heading">
          <div>
            <h2>Administrative & Audit Actions</h2>
            <p>Admin actions and operational modifications executed on this date.</p>
          </div>

          <span>{data?.activity?.length ?? 0} records</span>
        </div>

        <div className="daily-table-wrapper">
          <table className="daily-table">
            <thead>
              <tr>
                <th>Source</th>
                <th>Action</th>
                <th>Entity</th>
                <th>Actor</th>
                <th>Outcome</th>
                <th>Trace ID</th>
                <th>Timestamp</th>
              </tr>
            </thead>

            <tbody>
              {data?.activity?.length ? (
                data.activity.map((item, idx) => (
                  <tr key={item.id || idx}>
                    <td>
                      <span className="daily-status-badge">
                        {item.source}
                      </span>
                    </td>
                    <td className="bold">{item.action}</td>
                    <td>{item.entity || "—"}</td>
                    <td>{item.actor || "Admin"}</td>
                    <td>
                      <span className={`daily-status-badge ${item.outcome === "SUCCESS" ? "pill-green" : ""}`}>
                        {item.outcome || "INFO"}
                      </span>
                    </td>
                    <td className="font-mono">{item.traceId || "—"}</td>
                    <td>{formatDateTime(item.timestamp)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan="7" className="daily-empty-cell">
                    No administrative actions on this date.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </section>
  );
}
