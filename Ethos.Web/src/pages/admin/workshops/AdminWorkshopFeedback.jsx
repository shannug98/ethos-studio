import React, { useState, useEffect, useCallback } from "react";
import { useOutletContext } from "react-router-dom";
import { adminApi } from "../../../services/adminApi";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopFeedback() {
  const { workshop } = useOutletContext();
  const workshopId = workshop.Id || workshop.id;

  const [activeTab, setActiveTab] = useState("reviews"); // "reviews" | "automation"
  const [feedbacks, setFeedbacks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const loadFeedback = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await adminApi.getWorkshopFeedback(workshopId);
      setFeedbacks(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err?.message || "Failed to load workshop reviews.");
    } finally {
      setLoading(false);
    }
  }, [workshopId]);

  useEffect(() => {
    loadFeedback();
  }, [loadFeedback]);

  const avgRating =
    feedbacks.length > 0
      ? (feedbacks.reduce((acc, f) => acc + (f.rating || f.Rating || 0), 0) / feedbacks.length).toFixed(1)
      : null;

  return (
    <div className="workshop-subpage-container">
      {/* Header */}
      <div className="subpage-header" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "16px" }}>
        <div>
          <h1 className="subpage-title">Feedback & Reviews</h1>
          <p className="subpage-subtitle">
            Participant feedback ratings, star reviews, and automated post-workshop WhatsApp campaigns.
          </p>
        </div>

        {avgRating && (
          <div className="feedback-avg-badge" style={{ background: "#fef3c7", border: "1px solid #fde68a", padding: "8px 16px", borderRadius: "8px", display: "flex", alignItems: "center", gap: "8px" }}>
            <span className="star-icon" style={{ color: "#d97706", fontSize: "18px" }}>★</span>
            <span className="avg-num" style={{ fontWeight: "800", color: "#92400e", fontSize: "16px" }}>{avgRating}</span>
            <span className="avg-sub" style={{ color: "#b45309", fontSize: "12px", fontWeight: "600" }}>/ 5.0 ({feedbacks.length} reviews)</span>
          </div>
        )}
      </div>

      {error && <div className="subpage-error-banner">{error}</div>}

      {/* Subpage Tabs Navigation */}
      <div style={{ display: "flex", gap: "8px", borderBottom: "1px solid #e2e8f0", marginBottom: "20px" }}>
        <button
          type="button"
          onClick={() => setActiveTab("reviews")}
          style={{
            padding: "10px 18px",
            background: "none",
            border: "none",
            borderBottom: activeTab === "reviews" ? "3px solid #ff5500" : "3px solid transparent",
            color: activeTab === "reviews" ? "#ff5500" : "#475569",
            fontWeight: "750",
            fontSize: "14px",
            cursor: "pointer",
            transition: "all 0.15s ease"
          }}
        >
          🌟 Attendee Reviews ({feedbacks.length})
        </button>

        <button
          type="button"
          onClick={() => setActiveTab("automation")}
          style={{
            padding: "10px 18px",
            background: "none",
            border: "none",
            borderBottom: activeTab === "automation" ? "3px solid #ff5500" : "3px solid transparent",
            color: activeTab === "automation" ? "#ff5500" : "#475569",
            fontWeight: "750",
            fontSize: "14px",
            cursor: "pointer",
            transition: "all 0.15s ease"
          }}
        >
          ⚙️ WhatsApp Feedback Automation
        </button>
      </div>

      {/* TAB 1: ATTENDEE REVIEWS */}
      {activeTab === "reviews" && (
        <>
          {feedbacks.length === 0 && !loading ? (
            <div className="subpage-empty-box" style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: "12px", padding: "40px 20px", textAlign: "center" }}>
              <span className="empty-box-icon" style={{ fontSize: "36px", display: "block", marginBottom: "12px" }}>💬</span>
              <h3 style={{ color: "#0f172a", margin: "0 0 8px 0" }}>No Reviews Submitted Yet</h3>
              <p style={{ color: "#64748b", margin: 0, fontSize: "14px" }}>
                Feedback collection messages are queued automatically after the workshop session concludes.
              </p>
            </div>
          ) : (
            <div className="feedback-cards-list" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))", gap: "16px" }}>
              {feedbacks.map((fb) => {
                const rating = fb.rating || fb.Rating || 5;
                const name = fb.studentNameMasked || fb.StudentNameMasked || "Verified Attendee";
                const comment = fb.comment || fb.Comment || "No written review provided.";
                const dateStr = fb.formattedDate || fb.FormattedDate || "Recently";

                return (
                  <div key={fb.id || fb.Id} className="feedback-review-card" style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "16px", boxShadow: "0 1px 3px rgba(0,0,0,0.05)" }}>
                    <div className="fb-card-top" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "10px" }}>
                      <div className="fb-author-info">
                        <span className="fb-author-name" style={{ fontWeight: "700", color: "#0f172a", fontSize: "14px" }}>{name}</span>
                        <span className="fb-verified-pill" style={{ marginLeft: "8px", fontSize: "11px", padding: "1px 6px", background: "#f0fdf4", color: "#16a34a", border: "1px solid #bbf7d0", borderRadius: "4px", fontWeight: "600" }}>✓ Verified Attendee</span>
                      </div>
                      <div className="fb-rating-stars" style={{ color: "#eab308", fontSize: "16px" }}>
                        {"★".repeat(rating)}
                        <span className="empty-stars" style={{ color: "#cbd5e1" }}>{"★".repeat(Math.max(0, 5 - rating))}</span>
                      </div>
                    </div>

                    <p className="fb-comment-text" style={{ color: "#334155", fontSize: "13px", lineHeight: "1.5", margin: "0 0 12px 0" }}>"{comment}"</p>
                    <div className="fb-date-footer" style={{ fontSize: "11px", color: "#94a3b8" }}>{dateStr}</div>
                  </div>
                );
              })}
            </div>
          )}
        </>
      )}

      {/* TAB 2: WHATSAPP AUTOMATION & CONFIG */}
      {activeTab === "automation" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
          {/* Automation Status Card */}
          <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: "12px", padding: "24px", boxShadow: "0 1px 3px rgba(0,0,0,0.05)" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "12px", marginBottom: "16px" }}>
              <div>
                <h3 style={{ margin: "0 0 4px 0", fontSize: "16px", fontWeight: "800", color: "#0f172a" }}>
                  Automated MSG91 WhatsApp Feedback Campaign
                </h3>
                <p style={{ margin: 0, fontSize: "13px", color: "#64748b" }}>
                  Dispatches an official feedback request to all verified attendees via WhatsApp outbox.
                </p>
              </div>
              <span style={{ display: "inline-flex", alignItems: "center", gap: "6px", background: "#ecfdf5", color: "#15803d", border: "1px solid #86efac", padding: "4px 10px", borderRadius: "20px", fontSize: "12px", fontWeight: "750" }}>
                <span style={{ width: "8px", height: "8px", borderRadius: "50%", background: "#22c55e" }}></span>
                Automation Active
              </span>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "16px", marginTop: "16px" }}>
              <div style={{ background: "#f8fafc", padding: "14px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                <div style={{ fontSize: "11px", fontWeight: "700", color: "#64748b", textTransform: "uppercase" }}>Template Name</div>
                <div style={{ fontSize: "14px", fontWeight: "750", color: "#0f172a", marginTop: "4px" }}>ethos_workshop_feedback</div>
              </div>

              <div style={{ background: "#f8fafc", padding: "14px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                <div style={{ fontSize: "11px", fontWeight: "700", color: "#64748b", textTransform: "uppercase" }}>Trigger Condition</div>
                <div style={{ fontSize: "14px", fontWeight: "750", color: "#0f172a", marginTop: "4px" }}>Workshop Concludes (Attended)</div>
              </div>

              <div style={{ background: "#f8fafc", padding: "14px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                <div style={{ fontSize: "11px", fontWeight: "700", color: "#64748b", textTransform: "uppercase" }}>Dispatch Window</div>
                <div style={{ fontSize: "14px", fontWeight: "750", color: "#0f172a", marginTop: "4px" }}>2 Hours Post-Event</div>
              </div>
            </div>
          </div>

          {/* Template Message Preview */}
          <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: "12px", padding: "24px", boxShadow: "0 1px 3px rgba(0,0,0,0.05)" }}>
            <h3 style={{ margin: "0 0 12px 0", fontSize: "16px", fontWeight: "800", color: "#0f172a" }}>
              WhatsApp Template Content Preview
            </h3>
            <div style={{ maxWidth: "480px", background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: "10px", padding: "18px", color: "#166534", fontSize: "13px", lineHeight: "1.6" }}>
              <p style={{ margin: "0 0 10px 0" }}>
                <strong>Hey {`{{name}}`}! 👋</strong>
              </p>
              <p style={{ margin: "0 0 10px 0" }}>
                Thank you for dancing with us at <strong>{workshop?.Title || workshop?.title || "ETHOS Workshop"}</strong>!
              </p>
              <p style={{ margin: "0 0 10px 0" }}>
                We would love to hear your thoughts and experience to help us keep improving. It only takes 30 seconds:
              </p>
              <p style={{ margin: "0 0 10px 0" }}>
                👉 <span style={{ textDecoration: "underline", fontWeight: "700" }}>https://ethosdance.in/feedback?token=...</span>
              </p>
              <p style={{ margin: 0, fontSize: "12px", color: "#15803d" }}>
                See you on the dance floor soon! ✨<br />
                <strong>Team ETHOS Dance Studio</strong>
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
