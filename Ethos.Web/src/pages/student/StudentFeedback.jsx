import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { feedbackApi } from "../../services/feedbackApi";
import { studentStateSync } from "../../services/studentStateSync";
import "./StudentFeedback.css";

export default function StudentFeedback() {
  const navigate = useNavigate();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [actionMessage, setActionMessage] = useState("");

  const [overview, setOverview] = useState(null);
  const [feedbackList, setFeedbackList] = useState([]);
  const [pendingList, setPendingList] = useState([]);

  const [activeTab, setActiveTab] = useState("all"); // "all", "classes", "workshops", "pending"

  // Modal State for feedback submission
  const [modalOpen, setModalOpen] = useState(false);
  const [selectedItem, setSelectedItem] = useState(null); // Pending item to review
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState("");

  // Form fields
  const [overallRating, setOverallRating] = useState(5);
  const [teachingQuality, setTeachingQuality] = useState(5);
  const [explanationClarity, setExplanationClarity] = useState(5);
  const [trainerEngagement, setTrainerEngagement] = useState(5);
  const [likedAspects, setLikedAspects] = useState("");
  const [improvements, setImprovements] = useState("");
  const [wouldAttendAgain, setWouldAttendAgain] = useState("Yes"); // "Yes", "Maybe", "No"

  const loadData = async () => {
    try {
      setLoading(true);
      setError("");

      const [overviewData, myFeedbackData, pendingData] = await Promise.all([
        feedbackApi.getOverview(),
        feedbackApi.getMyFeedback(),
        feedbackApi.getPendingFeedback(),
      ]);

      setOverview(overviewData);
      setFeedbackList(myFeedbackData || []);
      setPendingList(pendingData || []);
    } catch (err) {
      setError(
        err?.data?.message ||
        err?.message ||
        "Unable to load student history and feedback."
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const openFeedbackModal = (item) => {
    setSelectedItem(item);
    setOverallRating(5);
    setTeachingQuality(5);
    setExplanationClarity(5);
    setTrainerEngagement(5);
    setLikedAspects("");
    setImprovements("");
    setWouldAttendAgain("Yes");
    setFormError("");
    setModalOpen(true);
  };

  const closeFeedbackModal = () => {
    setModalOpen(false);
    setSelectedItem(null);
    setFormError("");
  };

  const handleSubmitFeedback = async (e) => {
    e.preventDefault();
    if (!selectedItem) return;

    try {
      setSubmitting(true);
      setFormError("");

      if (selectedItem.type === "CLASS") {
        const payload = {
          overallRating: Number(overallRating),
          teachingQuality: Number(teachingQuality),
          explanationClarity: Number(explanationClarity),
          trainerEngagement: Number(trainerEngagement),
          classPace: 5,
          choreographyContent: 5,
          difficultyLevel: 3,
          classExperience: 5,
          likedAspects: likedAspects.trim() || "Clear instructions and encouraging trainer.",
          improvements: improvements.trim() || null,
          wouldAttendAgain: wouldAttendAgain,
        };

        await feedbackApi.submitClassFeedback(selectedItem.itemId, payload);
      } else {
        const payload = {
          overallRating: Number(overallRating),
          teachingQuality: Number(teachingQuality),
          explanationClarity: Number(explanationClarity),
          demonstrationRating: 5,
          interactionRating: Number(trainerEngagement),
          choreographyContent: 5,
          durationRating: 4,
          organizationRating: 5,
          venueRating: 5,
          valueForMoney: 5,
          wouldRecommend: true,
          wouldAttendTrainerAgain: wouldAttendAgain === "Yes",
          whatEnjoyed: likedAspects.trim() || "Intense session and high energy masterclass.",
          improvements: improvements.trim() || null,
        };

        await feedbackApi.submitWorkshopFeedback(selectedItem.itemId, payload);
      }

      setActionMessage(`Feedback successfully recorded for ${selectedItem.title}!`);
      closeFeedbackModal();
      await loadData();
      studentStateSync.emitDashboardUpdated();
    } catch (err) {
      setFormError(
        err?.data?.message ||
        err?.message ||
        "Failed to submit feedback. Please check required fields."
      );
    } finally {
      setSubmitting(false);
    }
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return "—";
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString("en-US", {
        month: "short",
        day: "numeric",
        year: "numeric",
      });
    } catch {
      return dateStr;
    }
  };

  const filteredFeedbacks = feedbackList.filter((fb) => {
    if (activeTab === "classes") return fb.type === "CLASS";
    if (activeTab === "workshops") return fb.type === "WORKSHOP";
    return true;
  });

  return (
    <div className="student-feedback-page">
      <div className="student-feedback-container">

        {/* HEADER */}
        <header className="student-feedback-header">
          <h1>
            Your Studio Journey &<br />
            <em>Reflections.</em>
          </h1>
          <p>
            Review your verified class and workshop learning history, inspect your submitted
            evaluations, or provide valuable feedback on past completed sessions.
          </p>
        </header>

        {actionMessage && (
          <div className="student-feedback-metric-card" style={{ borderLeft: "3px solid #73c880", marginBottom: "24px" }}>
            <p style={{ margin: 0, color: "#73c880", fontSize: "13px" }}>✓ {actionMessage}</p>
          </div>
        )}

        {error && (
          <div className="student-dashboard-error">
            <p>{error}</p>
          </div>
        )}

        {/* METRICS STRIP */}
        {overview && (
          <div className="student-feedback-metrics-grid">
            <div className="student-feedback-metric-card">
              <strong>{overview.classesEnrolled}</strong>
              <span>CLASSES ENROLLED</span>
            </div>
            <div className="student-feedback-metric-card">
              <strong>{overview.classesCompleted}</strong>
              <span>CLASSES COMPLETED</span>
            </div>
            <div className="student-feedback-metric-card">
              <strong>{overview.workshopsBooked}</strong>
              <span>WORKSHOPS BOOKED</span>
            </div>
            <div className="student-feedback-metric-card">
              <strong>{overview.feedbackSubmitted}</strong>
              <span>FEEDBACK SUBMITTED</span>
            </div>
            <div className="student-feedback-metric-card student-feedback-metric-card--highlight">
              <strong>{overview.feedbackPending}</strong>
              <span>FEEDBACK PENDING</span>
            </div>
          </div>
        )}

        {/* TABS */}
        <div className="student-feedback-tabs">
          <button
            type="button"
            className={`student-feedback-tab-btn ${activeTab === "all" ? "active" : ""}`}
            onClick={() => setActiveTab("all")}
          >
            ALL REVIEWS ({feedbackList.length})
          </button>
          <button
            type="button"
            className={`student-feedback-tab-btn ${activeTab === "classes" ? "active" : ""}`}
            onClick={() => setActiveTab("classes")}
          >
            CLASS REVIEWS ({feedbackList.filter(f => f.type === "CLASS").length})
          </button>
          <button
            type="button"
            className={`student-feedback-tab-btn ${activeTab === "workshops" ? "active" : ""}`}
            onClick={() => setActiveTab("workshops")}
          >
            WORKSHOP REVIEWS ({feedbackList.filter(f => f.type === "WORKSHOP").length})
          </button>
          <button
            type="button"
            className={`student-feedback-tab-btn ${activeTab === "pending" ? "active" : ""}`}
            onClick={() => setActiveTab("pending")}
          >
            PENDING FEEDBACK ({overview ? overview.feedbackPending : pendingList.filter(p => p.canSubmit).length})
          </button>
        </div>

        {/* CONTENT */}
        {loading ? (
          <div className="student-dashboard-loading">
            <div className="student-spinner" />
            <span>Loading studio learning history...</span>
          </div>
        ) : activeTab === "pending" ? (
          <div className="student-pending-list">
            {pendingList.length > 0 ? (
              pendingList.map((item) => {
                const key = item.referenceId || item.itemId;
                const canSubmit = item.canSubmit;
                const isLocked = item.status === "LOCKED" || !canSubmit;

                return (
                  <div
                    key={key}
                    className={`student-pending-card ${isLocked ? "student-pending-card--locked" : ""}`}
                  >
                    <div className="student-pending-info">
                      <div className="student-pending-badge-row">
                        <span className={`student-status-badge ${isLocked ? "student-status-badge--locked" : "student-status-badge--pending"}`}>
                          {isLocked ? "🔒 FEEDBACK LOCKED" : "⭐ FEEDBACK AVAILABLE"}
                        </span>
                        <span className="student-feedback-type-badge">{item.type}</span>
                      </div>
                      <h4>{item.title}</h4>
                      <div className="student-pending-meta">
                        <span>• Instructor: {item.trainerName || "Ethos Faculty"}</span>
                        <span>• Date: {formatDate(item.eventDate)}</span>
                        {item.venue && <span>• Venue: {item.venue}</span>}
                      </div>
                      {isLocked && (
                        <p className="student-locked-note">
                          <span>🔒</span>
                          {item.lockReason || "Feedback locked. Available after your workshop session ends."}
                        </p>
                      )}
                    </div>
                    <div>
                      {canSubmit ? (
                        <button
                          type="button"
                          className="student-review-now-btn"
                          onClick={() => openFeedbackModal(item)}
                        >
                          GIVE FEEDBACK →
                        </button>
                      ) : (
                        <button
                          type="button"
                          className="student-locked-btn"
                          disabled
                          title={item.lockReason || "Feedback locked. Available after your workshop session ends."}
                        >
                          🔒 SESSION IN PROGRESS
                        </button>
                      )}
                    </div>
                  </div>
                );
              })
            ) : (
              <div className="student-empty-state-box">
                <div className="student-empty-icon">✓</div>
                <h4>All feedback up to date</h4>
                <p>You have submitted feedback for all your completed sessions.</p>
              </div>
            )}
          </div>
        ) : (
          <div className="student-feedback-list">
            {filteredFeedbacks.length > 0 ? (
              filteredFeedbacks.map((fb) => (
                <div key={fb.feedbackId || fb.id} className="student-feedback-card">
                  <div className="student-feedback-card-header">
                    <div className="student-feedback-title-group">
                      <h3>{fb.title}</h3>
                      <div className="student-feedback-meta">
                        <span>Trainer: {fb.trainerName}</span>
                        <span>• Submitted: {formatDate(fb.submittedAt)}</span>
                      </div>
                    </div>
                    <span className="student-feedback-type-badge">{fb.type}</span>
                  </div>

                  <div className="student-feedback-score-bar">
                    <span className="student-feedback-stars">
                      {"★".repeat(fb.overallRating)}
                      {"☆".repeat(5 - fb.overallRating)}
                    </span>
                    <span className="student-feedback-score-text">
                      {fb.overallRating} / 5 Rating
                    </span>
                  </div>

                  <div className="student-criteria-breakdown">
                    {fb.criteria && Object.entries(fb.criteria).map(([name, score]) => (
                      <div key={name} className="student-criterion-item">
                        <span>{name}:</span>
                        <strong>{score} / 5</strong>
                      </div>
                    ))}
                    {fb.recommendation && (
                      <div className="student-criterion-item">
                        <span>Recommendation:</span>
                        <strong>{fb.recommendation}</strong>
                      </div>
                    )}
                  </div>

                  {fb.likedAspects && (
                    <div className="student-feedback-text-block">
                      <strong>HIGHLIGHTS & PRAISE</strong>
                      <p>{fb.likedAspects}</p>
                    </div>
                  )}

                  {fb.improvements && (
                    <div className="student-feedback-text-block">
                      <strong>SUGGESTIONS FOR STUDIO</strong>
                      <p>{fb.improvements}</p>
                    </div>
                  )}
                </div>
              ))
            ) : (
              <div className="student-empty-state-box">
                <div className="student-empty-icon">📝</div>
                <h4>No feedback records found</h4>
                <p>You haven't submitted reviews yet. Complete your sessions to share feedback.</p>
              </div>
            )}
          </div>
        )}

      </div>

      {/* FEEDBACK MODAL */}
      {modalOpen && selectedItem && (
        <div className="student-modal-backdrop" onClick={closeFeedbackModal}>
          <div className="student-modal-container" onClick={(e) => e.stopPropagation()}>
            <div className="student-modal-header">
              <div>
                <span className="student-card-eyebrow">SUBMIT STRUCTURED REVIEW</span>
                <h3>{selectedItem.title}</h3>
                <small style={{ color: "rgba(255,255,255,0.5)" }}>
                  Instructor: {selectedItem.trainerName} • Completed Session
                </small>
              </div>
              <button
                type="button"
                className="student-modal-close"
                onClick={closeFeedbackModal}
              >
                ✕
              </button>
            </div>

            {formError && (
              <div className="student-dashboard-error">
                <p>{formError}</p>
              </div>
            )}

            <form onSubmit={handleSubmitFeedback}>
              <div className="student-form-section">
                <div className="student-form-group">
                  <label className="student-form-label">OVERALL EXPERIENCE RATING (1-5)</label>
                  <div className="student-rating-row">
                    {[1, 2, 3, 4, 5].map((star) => (
                      <button
                        key={star}
                        type="button"
                        className={`student-rating-btn ${overallRating === star ? "selected" : ""}`}
                        onClick={() => setOverallRating(star)}
                      >
                        {star}★
                      </button>
                    ))}
                  </div>
                </div>

                <div className="student-form-group">
                  <label className="student-form-label">TEACHING QUALITY (1-5)</label>
                  <div className="student-rating-row">
                    {[1, 2, 3, 4, 5].map((star) => (
                      <button
                        key={star}
                        type="button"
                        className={`student-rating-btn ${teachingQuality === star ? "selected" : ""}`}
                        onClick={() => setTeachingQuality(star)}
                      >
                        {star}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="student-form-group">
                  <label className="student-form-label">EXPLANATION CLARITY (1-5)</label>
                  <div className="student-rating-row">
                    {[1, 2, 3, 4, 5].map((star) => (
                      <button
                        key={star}
                        type="button"
                        className={`student-rating-btn ${explanationClarity === star ? "selected" : ""}`}
                        onClick={() => setExplanationClarity(star)}
                      >
                        {star}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="student-form-group">
                  <label className="student-form-label">TRAINER ENGAGEMENT (1-5)</label>
                  <div className="student-rating-row">
                    {[1, 2, 3, 4, 5].map((star) => (
                      <button
                        key={star}
                        type="button"
                        className={`student-rating-btn ${trainerEngagement === star ? "selected" : ""}`}
                        onClick={() => setTrainerEngagement(star)}
                      >
                        {star}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="student-form-group">
                  <label className="student-form-label">WHAT DID YOU ENJOY MOST?</label>
                  <textarea
                    className="student-textarea"
                    placeholder="E.g. Great choreography, breakdown of musicality, supportive trainer..."
                    value={likedAspects}
                    onChange={(e) => setLikedAspects(e.target.value)}
                  />
                </div>

                <div className="student-form-group">
                  <label className="student-form-label">AREAS FOR IMPROVEMENT (OPTIONAL)</label>
                  <textarea
                    className="student-textarea"
                    placeholder="E.g. Class pace was slightly fast, studio air conditioning..."
                    value={improvements}
                    onChange={(e) => setImprovements(e.target.value)}
                  />
                </div>

                <div className="student-form-group">
                  <label className="student-form-label">WOULD YOU ATTEND AGAIN?</label>
                  <div className="student-rating-row">
                    {["Yes", "Maybe", "No"].map((opt) => (
                      <button
                        key={opt}
                        type="button"
                        className={`student-feedback-tab-btn ${wouldAttendAgain === opt ? "active" : ""}`}
                        onClick={() => setWouldAttendAgain(opt)}
                      >
                        {opt}
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              <div className="student-modal-actions">
                <button
                  type="button"
                  className="student-cancel-btn"
                  onClick={closeFeedbackModal}
                  disabled={submitting}
                >
                  CANCEL
                </button>
                <button
                  type="submit"
                  className="student-review-now-btn"
                  disabled={submitting}
                >
                  {submitting ? "RECORDING..." : "CONFIRM & SUBMIT REVIEW"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

    </div>
  );
}
