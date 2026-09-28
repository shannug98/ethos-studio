import React, { useState, useEffect } from "react";
import { useParams, Link } from "react-router-dom";
import { API_BASE_URL } from "../config/api";
import { trackFeedbackOpened, trackFeedbackSubmitted } from "../services/analytics";
import "./GuestWorkshopFeedback.css";

export default function GuestWorkshopFeedback() {
  const { token } = useParams();
  const [details, setDetails] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Form State
  const [overallRating, setOverallRating] = useState(5);
  const [hoverRating, setHoverRating] = useState(0);
  const [comment, setComment] = useState("");
  const [answers, setAnswers] = useState({}); // { [questionId]: { numericValue, textValue } }
  const [hoverQuestionRatings, setHoverQuestionRatings] = useState({}); // { [questionId]: hoverValue }

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

        // Track analytics event
        if (data.workshopId) {
          trackFeedbackOpened(data.workshopId, {
            audienceType: data.audienceType || "Attended",
          });
        }

        // Initialize default answer state for dynamic questions
        const initialAnswers = {};
        if (data.questions && Array.isArray(data.questions)) {
          data.questions.forEach((q) => {
            if (q.questionType === "Rating1To5") {
              initialAnswers[q.id] = { numericValue: 5, textValue: null };
            } else {
              initialAnswers[q.id] = { numericValue: null, textValue: "" };
            }
          });
        }
        setAnswers(initialAnswers);
      } catch (err) {
        setError(err.message || "Unable to retrieve workshop details.");
      } finally {
        setLoading(false);
      }
    }
    loadDetails();
  }, [token]);

  const handleAnswerChange = (questionId, field, value) => {
    setAnswers((prev) => ({
      ...prev,
      [questionId]: {
        ...prev[questionId],
        [field]: value,
      },
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitError(null);

    const isNoShow = details?.audienceType === "NoShow";

    // Validate overall rating for Attended
    if (!isNoShow && (overallRating < 1 || overallRating > 5)) {
      setSubmitError("Please select an overall rating between 1 and 5 stars.");
      return;
    }

    // Validate required dynamic questions
    if (details?.questions && Array.isArray(details.questions)) {
      for (const q of details.questions) {
        if (q.isRequired) {
          const ans = answers[q.id];
          if (q.questionType === "Rating1To5" && (!ans || !ans.numericValue)) {
            setSubmitError(`Please select a star rating for: "${q.promptText}"`);
            return;
          }
          if (q.questionType === "SingleChoice" && (!ans || !ans.textValue?.trim())) {
            setSubmitError(`Please select an option for: "${q.promptText}"`);
            return;
          }
          if (q.questionType === "Text" && (!ans || !ans.textValue?.trim())) {
            setSubmitError(`Please provide a response for: "${q.promptText}"`);
            return;
          }
        }
      }
    }

    // Prepare formatted answer array
    const formattedAnswers = Object.entries(answers)
      .filter(([_, ans]) => ans && (ans.numericValue != null || (ans.textValue && ans.textValue.trim() !== "")))
      .map(([qId, ans]) => ({
        questionId: qId,
        numericValue: ans.numericValue != null ? Number(ans.numericValue) : null,
        textValue: ans.textValue ? ans.textValue.trim() : null,
      }));

    setSubmitting(true);

    try {
      const payload = {
        token: token.trim(),
        rating: isNoShow ? null : overallRating,
        comment: comment.trim() || null,
        wouldRecommend: !isNoShow ? overallRating >= 4 : null,
        wouldAttendTrainerAgain: !isNoShow ? overallRating >= 4 : null,
        answers: formattedAnswers,
      };

      const res = await fetch(`${API_BASE_URL}/api/feedback/workshop/submit`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        throw new Error(data.message || "Failed to submit feedback.");
      }
      setSubmitSuccess(true);
      if (details?.workshopId) {
        trackFeedbackSubmitted(details.workshopId, {
          audienceType: details.audienceType || "Attended",
          rating: String(isNoShow ? 0 : overallRating),
        });
      }
    } catch (err) {
      setSubmitError(err.message || "Failed to submit feedback. Please try again.");
    } finally {
      setSubmitting(false);
    }
  };

  const starLabels = ["", "Needs Attention", "Developing", "Good", "Very Good", "Outstanding"];
  const isNoShow = details?.audienceType === "NoShow";

  return (
    <div className="guest-feedback-page">
      <div className="guest-feedback-container">
        {/* Brand Header */}
        <div className="guest-feedback-brand">
          <span className="brand-badge">ETHOS DANCE STUDIO</span>
          <h1>{isNoShow ? "We Missed You at the Workshop" : "Workshop Experience Review"}</h1>
          <p className="brand-subtitle">
            {isNoShow
              ? "We're sorry we missed you! Letting us know why helps us improve our scheduling and future offerings."
              : "Your feedback helps our instructors refine their curriculum and ensures the highest studio standards."}
          </p>
        </div>

        {/* Loading State */}
        {loading && (
          <div className="feedback-card feedback-loading">
            <div className="feedback-spinner" />
            <p>Verifying secure invitation & retrieving workshop session...</p>
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
            <h2>{isNoShow ? "Thank You for Letting Us Know!" : "Thank You for Your Feedback!"}</h2>
            <p>
              {isNoShow
                ? `Your response for ${details?.workshopTitle} has been recorded. We hope to see you on the dance floor next time!`
                : `Your review for ${details?.workshopTitle} with ${details?.trainerName} has been securely recorded.`}
            </p>
            {!isNoShow && overallRating && (
              <div className="feedback-submitted-summary">
                <span className="submitted-stars">{"★".repeat(overallRating)}{"☆".repeat(5 - overallRating)}</span>
                <span className="submitted-rating-label">{overallRating} / 5 Stars ({starLabels[overallRating]})</span>
                {comment && <p className="submitted-comment">"{comment}"</p>}
              </div>
            )}
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
                    {details.workshopDate
                      ? new Date(details.workshopDate).toLocaleDateString(undefined, {
                          weekday: "short",
                          month: "short",
                          day: "numeric",
                          year: "numeric",
                        })
                      : "—"}
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

            {/* Overall Rating Section (Attended Audience Only) */}
            {!isNoShow && (
              <div className="form-section rating-section">
                <label className="section-label">
                  Overall Session Rating <span className="req">*</span>
                </label>
                <div className="star-rating-selector" onMouseLeave={() => setHoverRating(0)}>
                  {[1, 2, 3, 4, 5].map((star) => (
                    <button
                      key={star}
                      type="button"
                      className={`star-btn ${star <= (hoverRating || overallRating) ? "active" : ""}`}
                      onClick={() => setOverallRating(star)}
                      onMouseEnter={() => setHoverRating(star)}
                      aria-label={`${star} Star`}
                    >
                      ★
                    </button>
                  ))}
                </div>
                <span className="star-feedback-label">
                  {starLabels[hoverRating || overallRating]}
                </span>
              </div>
            )}

            {/* Dynamic Versioned Questions */}
            {details.questions && details.questions.length > 0 && (
              <div className="dynamic-questions-section">
                {details.questions.map((q) => {
                  const currentAnswer = answers[q.id] || {};

                  return (
                    <div key={q.id} className="form-section dynamic-question-item">
                      <label className="section-label">
                        {q.promptText} {q.isRequired ? <span className="req">*</span> : <span className="opt">(Optional)</span>}
                      </label>

                      {/* Question Type: Rating1To5 */}
                      {q.questionType === "Rating1To5" && (
                        <div>
                          <div
                            className="star-rating-selector"
                            onMouseLeave={() =>
                              setHoverQuestionRatings((prev) => ({ ...prev, [q.id]: 0 }))
                            }
                          >
                            {[1, 2, 3, 4, 5].map((star) => {
                              const activeVal = hoverQuestionRatings[q.id] || currentAnswer.numericValue || 0;
                              return (
                                <button
                                  key={star}
                                  type="button"
                                  className={`star-btn ${star <= activeVal ? "active" : ""}`}
                                  onClick={() => handleAnswerChange(q.id, "numericValue", star)}
                                  onMouseEnter={() =>
                                    setHoverQuestionRatings((prev) => ({ ...prev, [q.id]: star }))
                                  }
                                  aria-label={`${star} Star`}
                                >
                                  ★
                                </button>
                              );
                            })}
                          </div>
                          <span className="star-feedback-label">
                            {starLabels[hoverQuestionRatings[q.id] || currentAnswer.numericValue || 0]}
                          </span>
                        </div>
                      )}

                      {/* Question Type: SingleChoice */}
                      {q.questionType === "SingleChoice" && (
                        <div className="choice-options-grid">
                          {q.choices && q.choices.length > 0 ? (
                            q.choices.map((choice, cIdx) => {
                              const isSelected = currentAnswer.textValue === choice;
                              return (
                                <label
                                  key={cIdx}
                                  className={`choice-card ${isSelected ? "choice-selected" : ""}`}
                                >
                                  <input
                                    type="radio"
                                    name={`question_${q.id}`}
                                    value={choice}
                                    checked={isSelected}
                                    onChange={(e) => handleAnswerChange(q.id, "textValue", e.target.value)}
                                    className="choice-radio"
                                  />
                                  <span className="choice-text">{choice}</span>
                                </label>
                              );
                            })
                          ) : (
                            <input
                              type="text"
                              className="feedback-text-input"
                              placeholder="Your response..."
                              value={currentAnswer.textValue || ""}
                              onChange={(e) => handleAnswerChange(q.id, "textValue", e.target.value)}
                            />
                          )}
                        </div>
                      )}

                      {/* Question Type: Text */}
                      {q.questionType === "Text" && (
                        <div>
                          <textarea
                            className="feedback-textarea"
                            rows={3}
                            maxLength={2000}
                            placeholder="Your response..."
                            value={currentAnswer.textValue || ""}
                            onChange={(e) => handleAnswerChange(q.id, "textValue", e.target.value)}
                          />
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}

            {/* Written Comment Section */}
            <div className="form-section comment-section">
              <div className="comment-label-row">
                <label className="section-label" htmlFor="feedbackComment">
                  {isNoShow ? "Additional Thoughts or Scheduling Preferences" : "Written Feedback"}{" "}
                  <span className="opt">(Optional)</span>
                </label>
                <span className="char-count">{comment.length} / 2000</span>
              </div>
              <textarea
                id="feedbackComment"
                className="feedback-textarea"
                rows={4}
                maxLength={2000}
                placeholder={
                  isNoShow
                    ? "Share any preferences for upcoming dates, timings, or choreographies..."
                    : "Share your experience: choreography clarity, pacing, instructor energy, atmosphere, or key takeaways..."
                }
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
                disabled={submitting || (!isNoShow && overallRating < 1)}
              >
                {submitting
                  ? "Submitting Response..."
                  : isNoShow
                  ? "Submit Response"
                  : "Submit Session Feedback"}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
