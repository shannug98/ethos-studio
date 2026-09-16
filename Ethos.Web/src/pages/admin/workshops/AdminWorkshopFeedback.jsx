import React, { useState, useEffect, useCallback } from "react";
import { useOutletContext } from "react-router-dom";
import { adminApi } from "../../../services/adminApi";
import "./AdminWorkshopSubPages.css";

export default function AdminWorkshopFeedback() {
  const { workshop } = useOutletContext();
  const workshopId = workshop.Id || workshop.id;

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
      <div className="subpage-header">
        <div>
          <h1 className="subpage-title">Attendee Reviews & Feedback</h1>
          <p className="subpage-subtitle">
            Participant feedback ratings and anonymous comments for instructor refinement.
          </p>
        </div>

        {avgRating && (
          <div className="feedback-avg-badge">
            <span className="star-icon">★</span>
            <span className="avg-num">{avgRating}</span>
            <span className="avg-sub">/ 5.0 ({feedbacks.length} reviews)</span>
          </div>
        )}
      </div>

      {error && <div className="subpage-error-banner">{error}</div>}

      {feedbacks.length === 0 && !loading ? (
        <div className="subpage-empty-box">
          <span className="empty-box-icon">💬</span>
          <h3>No Reviews Submitted Yet</h3>
          <p>Feedback requests are sent automatically to participants after the workshop concludes.</p>
        </div>
      ) : (
        <div className="feedback-cards-list">
          {feedbacks.map((fb) => {
            const rating = fb.rating || fb.Rating || 5;
            const name = fb.studentNameMasked || fb.StudentNameMasked || "Verified Attendee";
            const comment = fb.comment || fb.Comment || "No written review provided.";
            const dateStr = fb.formattedDate || fb.FormattedDate || "Recently";

            return (
              <div key={fb.id || fb.Id} className="feedback-review-card">
                <div className="fb-card-top">
                  <div className="fb-author-info">
                    <span className="fb-author-name">{name}</span>
                    <span className="fb-verified-pill">✓ Verified Attendee</span>
                  </div>
                  <div className="fb-rating-stars">
                    {"★".repeat(rating)}
                    <span className="empty-stars">{"★".repeat(Math.max(0, 5 - rating))}</span>
                  </div>
                </div>

                <p className="fb-comment-text">"{comment}"</p>
                <div className="fb-date-footer">{dateStr}</div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
