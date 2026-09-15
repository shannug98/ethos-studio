import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import "./TrainerApplicationStatus.css";

const STATUS_META = {
  Draft: {
    label: "Draft",
    title: "Your application is still in progress.",
  },
  Submitted: {
    label: "Submitted",
    title: "Your application has been submitted.",
  },
  PaymentPending: {
    label: "Payment Pending",
    title: "Payment is required to continue.",
  },
  PaymentVerified: {
    label: "Payment Verified",
    title: "Your payment has been verified.",
  },
  UnderReview: {
    label: "Under Review",
    title: "Your application is now under review.",
  },
  ChangesRequested: {
    label: "Changes Requested",
    title: "Some changes are required before approval.",
  },
  Approved: {
    label: "Approved",
    title: "Welcome to the Ethos Trainer Network.",
  },
  Rejected: {
    label: "Rejected",
    title: "Your trainer application was not approved.",
  },
  Cancelled: {
    label: "Cancelled",
    title: "This trainer application has been cancelled.",
  },
};

export default function TrainerApplicationStatus() {
  const navigate = useNavigate();

  const [application, setApplication] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;

    async function loadApplication() {
      try {
        setLoading(true);
        setError("");

        const result = await trainerApi.getApplication();

        if (active) {
          setApplication(result);
        }
      } catch (err) {
        if (active) {
          setError(
            err?.response?.data?.message ||
              err?.response?.data?.title ||
              err?.message ||
              "Unable to load your application."
          );
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    loadApplication();

    return () => {
      active = false;
    };
  }, []);

  function formatDate(value) {
    if (!value) {
      return "—";
    }

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
      return "—";
    }

    return date.toLocaleDateString("en-IN", {
      day: "2-digit",
      month: "long",
      year: "numeric",
    });
  }

  if (loading) {
    return (
      <main className="trainer-status-page">
        <div className="trainer-status-loading">
          <div className="trainer-status-loader" />
          <p>LOADING APPLICATION STATUS...</p>
        </div>
      </main>
    );
  }

  if (error || !application) {
    return (
      <main className="trainer-status-page">
        <section className="trainer-status-shell">
          <div className="trainer-status-card error-card">
            <span className="trainer-status-kicker">ETHOS TRAINER APPLICATION</span>

            <h1>UNABLE TO LOAD APPLICATION</h1>

            <p>{error || "We couldn't retrieve your application status."}</p>

            <button
              type="button"
              className="trainer-status-btn"
              onClick={() => window.location.reload()}
            >
              TRY AGAIN
            </button>
          </div>
        </section>
      </main>
    );
  }

  const statusKey = application.status || "Draft";
  const meta = STATUS_META[statusKey] || {
    label: statusKey,
    title: `Status: ${statusKey}`,
  };

  return (
    <main className="trainer-status-page">
      <section className="trainer-status-shell">

        {/* Top Eyebrow & Status Pill */}
        <header className="trainer-status-header">
          <div>
            <span className="trainer-status-kicker">
              ETHOS TRAINER PORTAL
            </span>

            <h1>
              {statusKey === "UnderReview" || statusKey === "Submitted" ? (
                <>
                  Application
                  <br />
                  <em>received.</em>
                </>
              ) : statusKey === "Approved" ? (
                <>
                  Welcome to
                  <br />
                  <em>ETHOS.</em>
                </>
              ) : (
                <>
                  Application
                  <br />
                  <em>status.</em>
                </>
              )}
            </h1>
          </div>

          <div className="trainer-status-pill-box">
            <span className="trainer-status-pill-label">STATUS</span>
            <span className={`trainer-status-pill status-${statusKey.toLowerCase()}`}>
              {meta.label.toUpperCase()}
            </span>
          </div>
        </header>

        {/* Status Specific Content */}
        {statusKey === "UnderReview" || statusKey === "Submitted" ? (
          /* UnderReview Experience */
          <div className="trainer-status-content-grid">
            <section className="trainer-status-card main-card">
              <span className="trainer-status-card-eyebrow">UNDER REVIEW</span>

              <h2>Your application is being reviewed by the ETHOS team.</h2>

              <p className="trainer-status-desc">
                Your trainer application has been successfully submitted and is now undergoing evaluation. We will notify you once our review is complete.
              </p>

              <div className="trainer-status-meta-grid">
                <div className="trainer-meta-item">
                  <label>APPLICATION ID</label>
                  <code>{application.id}</code>
                </div>

                <div className="trainer-meta-item">
                  <label>SELECTED PATHWAY</label>
                  <strong>{application.tier || "Standard Tier"}</strong>
                </div>

                <div className="trainer-meta-item">
                  <label>PAYMENT</label>
                  <strong className="status-verified-text">✓ VERIFIED</strong>
                </div>

                <div className="trainer-meta-item">
                  <label>SUBMITTED</label>
                  <strong>{formatDate(application.submittedAt || application.paymentVerifiedAt)}</strong>
                </div>
              </div>

              <div className="trainer-status-actions-footer">
                <button
                  type="button"
                  className="trainer-status-login-btn"
                  onClick={() => navigate("/trainer/login")}
                >
                  TRAINER LOGIN →
                </button>

                <button
                  type="button"
                  className="trainer-status-back-home"
                  onClick={() => navigate("/")}
                >
                  ← BACK TO ETHOS
                </button>
              </div>
            </section>

            {/* Timeline */}
            <section className="trainer-status-card timeline-card">
              <h3>APPLICATION TIMELINE</h3>

              <ul className="trainer-timeline-list">
                <li className="timeline-item done">
                  <span className="timeline-icon">✓</span>
                  <span>APPLICATION STARTED</span>
                </li>
                <li className="timeline-item done">
                  <span className="timeline-icon">✓</span>
                  <span>DETAILS COMPLETED</span>
                </li>
                <li className="timeline-item done">
                  <span className="timeline-icon">✓</span>
                  <span>DOCUMENTS UPLOADED</span>
                </li>
                <li className="timeline-item done">
                  <span className="timeline-icon">✓</span>
                  <span>INTRODUCTION COMPLETED</span>
                </li>
                <li className="timeline-item done">
                  <span className="timeline-icon">✓</span>
                  <span>TIER SELECTED</span>
                </li>
                <li className="timeline-item done">
                  <span className="timeline-icon">✓</span>
                  <span>PAYMENT VERIFIED</span>
                </li>
                <li className="timeline-item active">
                  <span className="timeline-icon">●</span>
                  <span>APPLICATION UNDER REVIEW</span>
                </li>
                <li className="timeline-item pending">
                  <span className="timeline-icon">○</span>
                  <span>ADMIN DECISION</span>
                </li>
              </ul>
            </section>
          </div>
        ) : statusKey === "Draft" ? (
          <section className="trainer-status-card action-card">
            <h2>APPLICATION IN PROGRESS</h2>
            <p>Your trainer application has not been submitted yet.</p>
            <button
              type="button"
              className="trainer-status-btn"
              onClick={() => navigate("/trainer/application/details")}
            >
              CONTINUE APPLICATION →
            </button>
          </section>
        ) : statusKey === "PaymentPending" ? (
          <section className="trainer-status-card action-card">
            <h2>PAYMENT REQUIRED</h2>
            <p>Your application is ready for payment.</p>
            <button
              type="button"
              className="trainer-status-btn"
              onClick={() => navigate("/trainer/application/payment")}
            >
              CONTINUE TO PAYMENT →
            </button>
          </section>
        ) : statusKey === "PaymentVerified" ? (
          <section className="trainer-status-card action-card">
            <h2>PAYMENT VERIFIED</h2>
            <p>Your payment has been successfully verified. Final submission is required.</p>
            <button
              type="button"
              className="trainer-status-btn"
              onClick={() => navigate("/trainer/application/review")}
            >
              CONTINUE TO REVIEW →
            </button>
          </section>
        ) : statusKey === "ChangesRequested" ? (
          <section className="trainer-status-card action-card">
            <h2>CHANGES REQUESTED</h2>
            <p>The Ethos team has reviewed your application and requested some updates.</p>

            {application.adminNotes && (
              <div className="trainer-notes-box">
                <label>REVIEW NOTES</label>
                <p>{application.adminNotes}</p>
              </div>
            )}

            <button
              type="button"
              className="trainer-status-btn"
              onClick={() => navigate("/trainer/application/details")}
            >
              UPDATE APPLICATION →
            </button>
          </section>
        ) : statusKey === "Approved" ? (
          <section className="trainer-status-card action-card success-card">
            <h2>WELCOME TO ETHOS</h2>
            <p>YOUR TRAINER APPLICATION HAS BEEN APPROVED.</p>
            <button
              type="button"
              className="trainer-status-btn"
              onClick={() => navigate("/trainer/login")}
            >
              TRAINER LOGIN →
            </button>
          </section>
        ) : statusKey === "Rejected" ? (
          <section className="trainer-status-card action-card error-status-card">
            <h2>APPLICATION NOT APPROVED</h2>
            <p>Thank you for your interest in joining Ethos.</p>
            {application.rejectionReason ? (
              <div className="trainer-notes-box">
                <label>REASON</label>
                <p>{application.rejectionReason}</p>
              </div>
            ) : (
              <p className="trainer-subtext">No additional reason was provided.</p>
            )}
          </section>
        ) : (
          <section className="trainer-status-card action-card">
            <h2>APPLICATION CANCELLED</h2>
            <p>This trainer application is no longer active.</p>
          </section>
        )}

      </section>
    </main>
  );
}
