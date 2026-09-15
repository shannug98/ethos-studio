import React, { useState, useEffect } from "react";
import { useParams, Link } from "react-router-dom";
import { API_BASE_URL } from "../config/api";
import "./GuestWorkshopFeedback.css";

export default function GuestWorkshopFeedback() {
  const { token } = useParams();
  const [details, setDetails] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [rating, setRating] = useState(5);
  const [hoverRating, setHoverRating] = useState(0);
  const [comment, setComment] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState(null);
  const [submitSuccess, setSubmitSuccess] = useState(false);

  useEffect(() => {
    async function loadDetails() {
      if (!token) {
        setError("Feedback link is missing or invalid.");
        setLoading(false);
        return;
      }
      try {
        setLoading(true);
        setError(null);
        const res = await fetch(`${API_BASE_URL}/api/feedback/workshop/token/${encodeURIComponent(token)}`);
        if (!res.ok) {
          const errData = await res.json().catch(() => ({}));
          throw new Error(errData.message || "Failed to load workshop details.");
        }
        const data = await res.json();
        setDetails(data);
      } catch (err) {
        setError(err.message || "Unable to retrieve workshop details.");
      } finally {
        setLoading(false);
      }
    }
    loadDetails();
  }, [token]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (rating < 1 || rating > 5) {
      setSubmitError("Please select a rating between 1 and 5 stars.");
      return;
    }
    setSubmitting(true);
    setSubmitError(null);
    try {
      const res = await fetch(`${API_BASE_URL}/api/feedback/workshop/submit`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          token: token.trim(),
          rating,
          comment: comment.trim() || null,
        }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        throw new Error(data.message || "Failed to submit feedback.");
      }
      setSubmitSuccess(true);
    } catch (err) {
      setSubmitError(err.message || "Failed to submit feedback. Please try again.");
    } finally {
      setSubmitting(false);
    }
  };

  const starLabels = ["", "Needs Attention", "Developing", "Good", "Very Good", "Outstanding"];

  return (
    <div className="guest-feedback-page">
      <div className="guest-feedback-container">
        {/* Brand Header */}
        <div className="guest-feedback-brand">
          <span className="brand-badge">ETHOS DANCE STUDIO</span>
          <h1>Workshop Experience Review</h1>
          <p className="brand-subtitle">
            Your feedback helps our instructors refine their curriculum and ensures the highest studio standards.
          </p>
        </div>

        {/* Loading State */}
        {loading && (
          <div className="feedback-card feedback-loading">
            <div className="feedback-spinner" />
            <p>Verifying secure link & retrieving workshop session...</p>
          </div>
        )}

        {/* Top-Level Error */}
        {!loading && error && (
          <div className="feedback-card feedback-error-card">
            <div className="error-icon">⚠️</div>
            <h2>Unable to Open Feedback</h2>
            <p>{error}</p>
            <Link to="/" className="btn-home">Return to Home</Link>
          </div>
        )}

        {/* Submission Success */}
        {!loading && !error && submitSuccess && (
          <div className="feedback-card feedback-success-card">
            <div className="success-icon">✓</div>
            <h2>Thank You for Your Feedback!</h2>
            <p>
              Your review for <strong>{details?.workshopTitle}</strong> with <strong>{details?.trainerName}</strong> has been securely recorded.
            </p>
            <div className="feedback-submitted-summary">
              <span className="submitted-stars">{"★".repeat(rating)}{"☆".repeat(5 - rating)}</span>
              <span className="submitted-rating-label">{rating} / 5 Stars ({starLabels[rating]})</span>
              {comment && <p className="submitted-comment">"{comment}"</p>}
            </div>
            <p className="privacy-note">
              🔒 <em>To preserve honest, unbiased feedback, individual attendee identities are kept strictly confidential from instructors.</em>
            </p>
            <Link to="/" className="btn-home">Done</Link>
          </div>
        )}

        {/* Already Submitted or Ineligible Notice */}
        {!loading && !error && !submitSuccess && details && !details.isEligible && (
          <div className="feedback-card feedback-ineligible-card">
            <div className="info-icon">ℹ️</div>
            <h2>{details.alreadySubmitted ? "Feedback Already Submitted" : "Session Feedback Unavailable"}</h2>
            <p>{details.ineligibilityReason || "This session is not currently accepting feedback."}</p>
            <div className="workshop-snippet">
              <strong>{details.workshopTitle}</strong>
              <span>Trainer: {details.trainerName}</span>
              {details.workshopDate && <span>Date: {new Date(details.workshopDate).toLocaleDateString()}</span>}
            </div>
            <Link to="/" className="btn-home">Return to Home</Link>
          </div>
        )}

        {/* Eligible Form */}
        {!loading && !error && !submitSuccess && details && details.isEligible && (
          <form className="feedback-card feedback-form" onSubmit={handleSubmit}>
            {/* Workshop Session Banner */}
            <div className="workshop-info-banner">
              <div className="banner-primary">
                <span className="style-tag">{details.danceStyle || "Masterclass"}</span>
                <h2>{details.workshopTitle}</h2>
                <div className="trainer-row">
                  <span>Instructor:</span>
                  <strong>{details.trainerName}</strong>
                </div>
              </div>
              <div className="banner-meta">
                <div className="meta-item">
                  <span className="meta-label">Date</span>
                  <span className="meta-val">
                    {details.workshopDate ? new Date(details.workshopDate).toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric", year: "numeric" }) : "—"}
                  </span>
                </div>
                {details.startTime && (
                  <div className="meta-item">
                    <span className="meta-label">Timing</span>
                    <span className="meta-val">
                      {String(details.startTime).substring(0, 5)} - {details.endTime ? String(details.endTime).substring(0, 5) : ""}
                    </span>
                  </div>
                )}
                {details.venue && (
                  <div className="meta-item">
                    <span className="meta-label">Venue</span>
                    <span className="meta-val">{details.venue}</span>
                  </div>
                )}
              </div>
            </div>

            {/* Rating Section */}
            <div className="form-section rating-section">
              <label className="section-label">Overall Session Rating <span className="req">*</span></label>
              <div className="star-rating-selector" onMouseLeave={() => setHoverRating(0)}>
                {[1, 2, 3, 4, 5].map((star) => (
                  <button
                    key={star}
                    type="button"
                    className={`star-btn ${star <= (hoverRating || rating) ? "active" : ""}`}
                    onClick={() => setRating(star)}
                    onMouseEnter={() => setHoverRating(star)}
                    aria-label={`${star} Star`}
                  >
                    ★
                  </button>
                ))}
              </div>
              <span className="star-feedback-label">
                {starLabels[hoverRating || rating]}
              </span>
            </div>

            {/* Written Comment Section */}
            <div className="form-section comment-section">
              <div className="comment-label-row">
                <label className="section-label" htmlFor="feedbackComment">
                  Written Feedback <span className="opt">(Optional)</span>
                </label>
                <span className="char-count">{comment.length} / 2000</span>
              </div>
              <textarea
                id="feedbackComment"
                className="feedback-textarea"
                rows={4}
                maxLength={2000}
                placeholder="Share your experience: choreography clarity, pacing, instructor energy, atmosphere, or key takeaways..."
                value={comment}
                onChange={(e) => setComment(e.target.value)}
              />
            </div>

            {submitError && <div className="alert alert-danger mb-3">{submitError}</div>}

            {/* Privacy Guarantee & Submit */}
            <div className="form-actions">
              <div className="privacy-pill">
                🔒 Anonymous feedback protected by Ethos Privacy Invariant
              </div>
              <button
                type="submit"
                className="btn-submit-feedback"
                disabled={submitting || rating < 1}
              >
                {submitting ? "Submitting Review..." : "Submit Session Feedback"}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
