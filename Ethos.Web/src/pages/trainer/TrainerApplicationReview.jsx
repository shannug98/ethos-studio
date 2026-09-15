import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import { paymentApi } from "../../services/paymentApi";
import "./TrainerApplicationReview.css";

import tierArtwork from "../../assets/trainer/ethos-tier-emblems.png";

const tierPositions = {
  SILVER: "silver",
  GOLD: "gold",
  DIAMOND: "diamond",
  PLATINUM: "platinum",
};

const PAYMENT_STATUS_MAP = {
  1: "Created",
  2: "Order Created",
  3: "Payment Pending",
  4: "Paid",
  5: "Failed",
  6: "Cancelled",
  7: "Refunded",
};

export default function TrainerApplicationReview() {
  const navigate = useNavigate();

  const [application, setApplication] = useState(null);
  const [documents, setDocuments] = useState([]);
  const [tiers, setTiers] = useState([]);
  const [payment, setPayment] = useState(null);
  const [video, setVideo] = useState(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [docError, setDocError] = useState("");
  const [paymentError, setPaymentError] = useState("");

  const [confirmed, setConfirmed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState("");

  useEffect(() => {
    let mounted = true;

    async function loadData() {
      setLoading(true);
      setError("");
      setDocError("");
      setPaymentError("");

      try {
        const [appData, tierData] = await Promise.all([
          trainerApi.getApplication(),
          trainerApi.getTiers(),
        ]);

        if (!mounted) return;

        setApplication(appData);
        setTiers(tierData || []);

        if (appData?.paymentTransactionId) {
          try {
            const payData = await paymentApi.getPayment(
              appData.paymentTransactionId
            );

            if (mounted) setPayment(payData);
          } catch (payErr) {
            if (mounted) {
              setPaymentError(
                payErr?.message || "Payment details could not be loaded."
              );
            }
          }
        }
      } catch (err) {
        if (!mounted) return;

        setError(
          err?.message || "Unable to load your application for review."
        );
      }



      try {
        const videoData = await trainerApi.getApplicationVideo();
        if (mounted) setVideo(videoData);
      } catch {
        // Video endpoint returns null/empty if youtube/no video
      } finally {
        if (mounted) setLoading(false);
      }
    }

    loadData();

    return () => {
      mounted = false;
    };
  }, []);

  function formatDocumentType(type) {
    if (!type) return "Document";

    return type
      .replace(/_/g, " ")
      .toLowerCase()
      .replace(/\b\w/g, (char) => char.toUpperCase());
  }

  function formatFileSize(bytes) {
    if (!bytes) return "Size N/A";

    if (bytes < 1024) return `${bytes} B`;

    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;

    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  function formatDate(dateStr) {
    if (!dateStr) return "N/A";

    try {
      return new Date(dateStr).toLocaleDateString("en-GB", {
        day: "2-digit",
        month: "short",
        year: "numeric",
      });
    } catch {
      return dateStr;
    }
  }

  function getPaymentStatusLabel(status) {
    if (status === undefined || status === null) return "Unknown";

    if (typeof status === "number") {
      return PAYMENT_STATUS_MAP[status] || `Status ${status}`;
    }

    return String(status);
  }

  function getYouTubeUrl(application) {
    return (
      application?.youTubeUrl ||
      application?.youtubeUrl ||
      application?.YouTubeUrl ||
      ""
    ).trim();
  }

  function getTierCode(tier, application) {
    const rawCode =
      tier?.code ||
      application?.tierCode ||
      application?.tier?.code ||
      "";

    if (rawCode) {
      return String(rawCode).trim().toUpperCase();
    }

    const tierName =
      tier?.name ||
      application?.tier?.name ||
      application?.tier ||
      "";

    const normalizedName = String(tierName).trim().toUpperCase();

    if (normalizedName.includes("PLATINUM")) return "PLATINUM";
    if (normalizedName.includes("DIAMOND")) return "DIAMOND";
    if (normalizedName.includes("GOLD")) return "GOLD";
    if (normalizedName.includes("SILVER")) return "SILVER";

    return "";
  }

  const handleSubmit = async () => {
    if (!confirmed || submitting) {
      return;
    }

    setSubmitting(true);
    setSubmitError("");

    try {
      await trainerApi.submitApplication();

      const updatedApplication = await trainerApi.getApplication();

      if (updatedApplication?.status === "UnderReview") {
        navigate("/trainer/application/status", {
          replace: true,
        });
        return;
      }

      setSubmitError(
        "Your application was submitted, but we could not confirm its current status."
      );
    } catch (err) {
      const message =
        err?.response?.data?.message ||
        err?.response?.data?.title ||
        err?.message ||
        "Unable to submit your application. Please try again.";

      setSubmitError(message);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <main className="trainer-review-page">
        <div className="trainer-review-loading">
          <div className="trainer-review-loader" />
          <p>Preparing your final application review...</p>
        </div>
      </main>
    );
  }

  if (error || !application) {
    return (
      <main className="trainer-review-page">
        <section className="trainer-review-shell">
          <div className="trainer-review-error-card">
            <span>REVIEW ERROR</span>

            <h1>We couldn't load your application.</h1>

            <p>{error || "Application details unavailable."}</p>

            <button
              type="button"
              onClick={() => window.location.reload()}
            >
              TRY AGAIN
            </button>
          </div>
        </section>
      </main>
    );
  }

  const selectedTier =
    tiers.find(
      (t) =>
        String(t.id) === String(application.currentTierId)
    ) ||
    tiers.find(
      (t) =>
        String(t.name || "").trim().toLowerCase() ===
        String(application.tier || "").trim().toLowerCase()
    ) ||
    null;

  const tierCode = getTierCode(selectedTier, application);

  const youtubeUrl = getYouTubeUrl(application);

  return (
    <main className="trainer-review-page">
      <section className="trainer-review-shell">

        {/* BACK LINK */}
        <button
          type="button"
          className="trainer-application-back"
          onClick={() => navigate("/trainer/application/tier")}
          style={{ marginBottom: "24px", cursor: "pointer", background: "none", border: "none" }}
        >
          ← BACK TO TIER
        </button>

        {/* 5-STEP PROGRESS BAR */}
        <div className="trainer-details-progress">
          <div className="trainer-progress-item trainer-progress-item--done">
            <span>01</span>
            <strong>PROFILE</strong>
          </div>

          <div className="trainer-progress-line trainer-progress-line--done" />

          <div className="trainer-progress-item trainer-progress-item--done">
            <span>02</span>
            <strong>INTRODUCTION</strong>
          </div>

          <div className="trainer-progress-line trainer-progress-line--done" />

          <div className="trainer-progress-item trainer-progress-item--done">
            <span>03</span>
            <strong>TIER</strong>
          </div>

          <div className="trainer-progress-line trainer-progress-line--done" />

          <div className="trainer-progress-item trainer-progress-item--active">
            <span>04</span>
            <strong>REVIEW</strong>
          </div>
        </div>

        {/* Header */}
        <header className="trainer-review-header">
          <div>
            <div className="trainer-review-top-meta">
              <span className="trainer-review-eyebrow">
                ETHOS TRAINER APPLICATION · STEP 04 OF 04
              </span>

              {application.status && (
                <span className="trainer-status-badge">
                  STATUS: {application.status}
                </span>
              )}
            </div>

            <h1>
              Final
              <br />
              <em>review.</em>
            </h1>

            <p>
              Review everything carefully before submitting your trainer
              application to the ETHOS team.
            </p>
          </div>
        </header>

        <div className="trainer-review-content-stack">

          {/* Section 1: Personal Details */}
          <section className="trainer-review-card">
            <div className="trainer-review-card-header">
              <div>
                <span className="trainer-review-card-index">01</span>
                <h2>PERSONAL DETAILS</h2>
              </div>

              <button
                type="button"
                className="trainer-edit-button"
                onClick={() => navigate("/trainer/application/details")}
              >
                EDIT →
              </button>
            </div>

            <div className="trainer-review-grid-two">
              <div className="trainer-review-field">
                <label>FULL NAME</label>
                <strong>{application.fullName || "Not provided"}</strong>
              </div>

              <div className="trainer-review-field">
                <label>CITY</label>
                <strong>{application.city || "Not provided"}</strong>
              </div>

              <div className="trainer-review-field">
                <label>PRIMARY DANCE STYLE</label>
                <strong>{application.primaryDanceStyle || "Not provided"}</strong>
              </div>

              <div className="trainer-review-field">
                <label>SECONDARY DANCE STYLES</label>
                <strong>{application.secondaryDanceStyles || "None listed"}</strong>
              </div>

              <div className="trainer-review-field">
                <label>EXPERIENCE</label>
                <strong>
                  {application.experienceYears != null
                    ? `${application.experienceYears} Years`
                    : "Not specified"}
                </strong>
              </div>

              <div className="trainer-review-field">
                <label>CURRENT STUDIO</label>
                <strong>{application.currentStudio || "Independent"}</strong>
              </div>
            </div>
          </section>

          {/* Section 2: Introduction & Social */}
          <section className="trainer-review-card">
            <div className="trainer-review-card-header">
              <div>
                <span className="trainer-review-card-index">02</span>
                <h2>INTRODUCTION & SOCIAL</h2>
              </div>

              <button
                type="button"
                className="trainer-edit-button"
                onClick={() => navigate("/trainer/application/introduction")}
              >
                EDIT →
              </button>
            </div>

            <div className="trainer-review-field full-width">
              <label>BIO / TEACHING PHILOSOPHY</label>
              <p className="trainer-review-bio-text">
                {application.bio || "No introduction provided yet."}
              </p>
            </div>

            <div className="trainer-review-grid-two" style={{ marginTop: "20px" }}>
              <div className="trainer-review-field">
                <label>VIDEO INTRODUCTION</label>
                {youtubeUrl ? (
                  <div className="trainer-review-video-confirmed">
                    <span className="trainer-review-video-source">
                      YOUTUBE INTRODUCTION
                    </span>

                    <a
                      href={youtubeUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="trainer-review-link"
                    >
                      Watch Video Introduction ↗
                    </a>
                  </div>
                ) : video ? (
                  <div className="trainer-review-video-confirmed">
                    <span className="trainer-review-video-source">
                      UPLOADED VIDEO
                    </span>

                    <strong className="trainer-review-video-name">
                      {video.fileName || "Trainer video"}
                    </strong>

                    <span className="trainer-review-video-meta">
                      {video.durationSeconds != null
                        ? `${video.durationSeconds} sec`
                        : "Duration unavailable"}

                      {video.fileSizeBytes
                        ? ` · ${formatFileSize(video.fileSizeBytes)}`
                        : ""}

                      {video.contentType
                        ? ` · ${video.contentType.replace("video/", "").toUpperCase()}`
                        : ""}
                    </span>
                  </div>
                ) : (
                  <span className="trainer-review-empty">
                    No video introduction available.
                  </span>
                )}
              </div>

              <div className="trainer-review-field">
                <label>INSTAGRAM</label>
                {application.instagramUrl ? (
                  <a
                    href={application.instagramUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="trainer-review-link"
                  >
                    {application.instagramUrl} ↗
                  </a>
                ) : (
                  <span>No Instagram provided</span>
                )}
              </div>
            </div>

            {application.applicationNotes && (
              <div className="trainer-review-field full-width" style={{ marginTop: "20px" }}>
                <label>APPLICATION NOTES</label>
                <p className="trainer-review-notes">
                  {application.applicationNotes}
                </p>
              </div>
            )}
          </section>

          {/* Section 3: Selected Tier */}
          <section className="trainer-review-card">
            <div className="trainer-review-card-header">
              <div>
                <span className="trainer-review-card-index">03</span>
                <h2>SELECTED TRAINER PATHWAY</h2>
              </div>

              <button
                type="button"
                className="trainer-edit-button"
                onClick={() => navigate("/trainer/application/tier")}
              >
                EDIT →
              </button>
            </div>

            <div className="trainer-review-tier-display">
              <div className="trainer-review-tier-art">
                <img
                  src={tierArtwork}
                  alt={`${selectedTier?.name || application.tier || "Trainer"} emblem`}
                  className={`trainer-tier-art-${
                    tierPositions[tierCode] || "silver"
                  }`}
                />
              </div>

              <div className="trainer-review-tier-info">
                <span className="trainer-tier-badge-code">
                  {tierCode}
                </span>

                <h3>{selectedTier?.name || application.tier || "Silver Tier"}</h3>

                <p>
                  {selectedTier?.description ||
                    "ETHOS Trainer Pathway program."}
                </p>

                <div className="trainer-review-tier-fee-box">
                  <span>APPLICATION FEE:</span>

                  <strong>
                    {selectedTier?.applicationFee != null
                      ? `₹${Number(selectedTier.applicationFee).toLocaleString(
                          "en-IN"
                        )}`
                      : "CONFIRMED"}
                  </strong>
                </div>
              </div>
            </div>
          </section>

          {/* Section 5: Payment Details */}
          <section className="trainer-review-card">
            <div className="trainer-review-card-header">
              <div>
                <span className="trainer-review-card-index trainer-review-card-index--text">
                  PAY
                </span>

                <h2>PAYMENT VERIFICATION</h2>
              </div>

              <div className="trainer-verified-pill">
                ✓ VERIFIED
              </div>
            </div>

            {paymentError ? (
              <div className="trainer-review-sub-error">{paymentError}</div>
            ) : payment ? (
              <div className="trainer-review-grid-two">
                <div className="trainer-review-field">
                  <label>PAYMENT STATUS</label>
                  <strong style={{ color: "#e98a68" }}>
                    {getPaymentStatusLabel(payment.status)}
                  </strong>
                </div>

                <div className="trainer-review-field">
                  <label>AMOUNT PAID</label>
                  <strong>
                    ₹
                    {Number(payment.amount || 0).toLocaleString("en-IN")} {payment.currency || "INR"}
                  </strong>
                </div>

                <div className="trainer-review-field">
                  <label>RAZORPAY ORDER ID</label>
                  <code className="trainer-review-code">
                    {payment.razorpayOrderId || "N/A"}
                  </code>
                </div>

                <div className="trainer-review-field">
                  <label>RAZORPAY PAYMENT ID</label>
                  <code className="trainer-review-code">
                    {payment.razorpayPaymentId || "N/A"}
                  </code>
                </div>

                <div className="trainer-review-field">
                  <label>PAID AT</label>
                  <strong>{formatDate(payment.paidAt || payment.createdAt)}</strong>
                </div>

                <div className="trainer-review-field">
                  <label>TRANSACTION REFERENCE</label>
                  <code className="trainer-review-code">{payment.id}</code>
                </div>
              </div>
            ) : (
              <div className="trainer-review-empty">
                Payment verified against application.
              </div>
            )}
          </section>

        </div>

        {/* Confirmation & Submission Footer */}
        <section className="trainer-review-footer-box">

          {submitError && (
            <div className="review-submit-error" role="alert">
              <div className="review-submit-error-icon">!</div>

              <div>
                <strong>Submission unsuccessful</strong>
                <p>{submitError}</p>
              </div>
            </div>
          )}

          <label className="trainer-review-checkbox-label">
            <input
              type="checkbox"
              checked={confirmed}
              disabled={submitting}
              onChange={(e) => setConfirmed(e.target.checked)}
            />

            <span>
              I confirm that the information provided above is
              accurate and complete.
            </span>
          </label>

          <footer className="trainer-review-actions">
            <button
              type="button"
              className="trainer-back-btn"
              disabled={submitting}
              onClick={() => navigate("/trainer/application/tier")}
            >
              ← BACK
            </button>

            <button
              type="button"
              className="trainer-submit-btn"
              disabled={!confirmed || submitting}
              onClick={handleSubmit}
            >
              {submitting ? "SUBMITTING..." : "SUBMIT APPLICATION →"}
            </button>
          </footer>

        </section>

      </section>
    </main>
  );
}
