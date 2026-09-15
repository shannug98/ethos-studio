import React, { useState, useEffect, useMemo, useCallback } from "react";
import { adminApi } from "../../services/adminApi";
import AdminKpiCard from "../../components/admin/common/AdminKpiCard";
import AdminBadge from "../../components/admin/common/AdminBadge";
import AdminDataTable from "../../components/admin/common/AdminDataTable";
import AdminExportButton from "../../components/admin/common/AdminExportButton";
import "./AdminFeedback.css";

export default function AdminFeedback() {
  const [feedbacks, setFeedbacks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Pagination & counts
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [totalCount, setTotalCount] = useState(0);

  // Filters
  const [search, setSearch] = useState("");
  const [minRating, setMinRating] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [selectedTrainer, setSelectedTrainer] = useState("");

  // Trainers list for filter dropdown
  const [trainers, setTrainers] = useState([]);

  // Detail Modal / Drawer
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedFeedback, setSelectedFeedback] = useState(null);
  const [copiedPhone, setCopiedPhone] = useState(false);

  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === "Escape" && detailModalOpen) {
        setDetailModalOpen(false);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [detailModalOpen]);

  const handleCopyPhone = (phone) => {
    if (!phone) return;
    navigator.clipboard.writeText(phone);
    setCopiedPhone(true);
    setTimeout(() => setCopiedPhone(false), 2000);
  };

  const fetchFeedback = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      params.append("page", page.toString());
      params.append("pageSize", pageSize.toString());
      if (minRating) params.append("minRating", minRating);
      if (startDate) params.append("startDate", startDate);
      if (endDate) params.append("endDate", endDate);
      if (selectedTrainer) params.append("trainerId", selectedTrainer);

      const res = await adminApi.getFeedback(params.toString());
      const items = res?.items || res?.data || [];
      setFeedbacks(items);
      setTotalCount(res?.totalCount ?? items.length);
    } catch (err) {
      setError(err.message || "Failed to load feedback records.");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, minRating, startDate, endDate, selectedTrainer]);

  useEffect(() => {
    fetchFeedback();
  }, [fetchFeedback]);

  // Load trainers list once for filter
  useEffect(() => {
    adminApi
      .getTrainers("page=1&pageSize=50")
      .then((res) => {
        const items = res?.items || res?.trainers || [];
        setTrainers(items);
      })
      .catch((err) => console.warn("Failed to load trainers for filter:", err));
  }, []);

  // Filter feedbacks client-side by search query (name, workshop, comment)
  const filteredFeedbacks = useMemo(() => {
    if (!search.trim()) return feedbacks;
    const q = search.trim().toLowerCase();
    return feedbacks.filter(
      (f) =>
        f.workshopTitle?.toLowerCase().includes(q) ||
        f.trainerName?.toLowerCase().includes(q) ||
        f.studentName?.toLowerCase().includes(q) ||
        f.comment?.toLowerCase().includes(q)
    );
  }, [feedbacks, search]);

  // Aggregate Metrics
  const metrics = useMemo(() => {
    if (feedbacks.length === 0) {
      return {
        total: totalCount,
        average: "0.0",
        recommendRate: "0%",
        fiveStars: 0,
        distribution: { 5: 0, 4: 0, 3: 0, 2: 0, 1: 0 },
        avgTeaching: "0.0",
        avgEnergy: "0.0",
        avgContent: "0.0",
      };
    }

    const ratings = feedbacks.map((f) => f.rating || 0);
    const sum = ratings.reduce((a, b) => a + b, 0);
    const avg = (sum / feedbacks.length).toFixed(1);

    const wouldRecCount = feedbacks.filter((f) => f.wouldRecommend).length;
    const recRate = `${Math.round((wouldRecCount / feedbacks.length) * 100)}%`;

    const dist = { 5: 0, 4: 0, 3: 0, 2: 0, 1: 0 };
    feedbacks.forEach((f) => {
      const r = Math.min(5, Math.max(1, f.rating || 1));
      dist[r] = (dist[r] || 0) + 1;
    });

    const teachSum = feedbacks.reduce((acc, f) => acc + (f.teachingRating || f.rating || 0), 0);
    const energySum = feedbacks.reduce((acc, f) => acc + (f.energyRating || f.rating || 0), 0);
    const contentSum = feedbacks.reduce((acc, f) => acc + (f.contentRating || f.rating || 0), 0);

    return {
      total: totalCount,
      average: avg,
      recommendRate: recRate,
      fiveStars: dist[5] || 0,
      distribution: dist,
      avgTeaching: (teachSum / feedbacks.length).toFixed(1),
      avgEnergy: (energySum / feedbacks.length).toFixed(1),
      avgContent: (contentSum / feedbacks.length).toFixed(1),
    };
  }, [feedbacks, totalCount]);

  const handleResetFilters = () => {
    setSearch("");
    setMinRating("");
    setStartDate("");
    setEndDate("");
    setSelectedTrainer("");
    setPage(1);
  };

  const handleRowClick = (fb) => {
    setSelectedFeedback(fb);
    setDetailModalOpen(true);
  };

  const renderStars = (count) => {
    const num = Math.min(5, Math.max(0, Math.round(Number(count) || 0)));
    return (
      <span className="feedback-stars" aria-label={`${count} out of 5 stars`}>
        {"★".repeat(num)}
        {"☆".repeat(5 - num)}
      </span>
    );
  };

  const columns = [
    {
      header: "Rating",
      key: "rating",
      width: "120px",
      render: (row) => (
        <div className="rating-cell">
          <span className="rating-score">{row.rating}</span>
          {renderStars(row.rating)}
        </div>
      ),
    },
    {
      header: "Workshop & Style",
      key: "workshopTitle",
      render: (row) => (
        <div className="workshop-cell">
          <span className="cell-primary-text">{row.workshopTitle}</span>
          <span className="cell-sub-text">Instructor: {row.trainerName}</span>
        </div>
      ),
    },
    {
      header: "Attendee",
      key: "studentName",
      width: "160px",
      render: (row) => (
        <div className="attendee-cell">
          <span className="cell-primary-text">{row.studentName || "Guest Attendee"}</span>
          <span className="cell-sub-text">{row.studentPhone ? `Ph: ${row.studentPhone}` : "Verified Student"}</span>
        </div>
      ),
    },
    {
      header: "Review Summary",
      key: "comment",
      render: (row) => (
        <div className="review-snippet-cell">
          {row.comment ? (
            <p className="review-snippet-text" title={row.comment}>
              “{row.comment.length > 85 ? `${row.comment.substring(0, 85)}...` : row.comment}”
            </p>
          ) : (
            <span className="no-comment-text">Rating only (no written review)</span>
          )}
        </div>
      ),
    },
    {
      header: "Recommends",
      key: "wouldRecommend",
      width: "120px",
      render: (row) =>
        row.wouldRecommend ? (
          <AdminBadge tone="success" dot size="sm">
            Yes
          </AdminBadge>
        ) : (
          <AdminBadge tone="neutral" size="sm">
            No
          </AdminBadge>
        ),
    },
    {
      header: "Date",
      key: "submittedAt",
      width: "120px",
      render: (row) => (
        <span className="date-cell">
          {row.submittedAt ? new Date(row.submittedAt).toLocaleDateString() : "—"}
        </span>
      ),
    },
    {
      header: "Actions",
      key: "actions",
      width: "100px",
      render: (row) => (
        <button
          type="button"
          className="admin-btn-secondary btn-sm"
          onClick={(e) => {
            e.stopPropagation();
            handleRowClick(row);
          }}
          aria-label={`View feedback for ${row.workshopTitle}`}
        >
          View Details
        </button>
      ),
    },
  ];

  return (
    <div className="admin-page-container admin-feedback-page">
      {/* Top Header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Student Feedback & Reviews</h1>
          <p className="admin-page-subtitle">
            Authoritative workshop reviews, teaching evaluations, attendee ratings, and satisfaction metrics.
          </p>
        </div>
        <div className="header-actions">
          <AdminExportButton
            data={filteredFeedbacks}
            filename={`ethos_feedback_${new Date().toISOString().slice(0, 10)}.csv`}
            columns={[
              { key: "workshopTitle", header: "Workshop" },
              { key: "trainerName", header: "Trainer" },
              { key: "studentName", header: "Student" },
              { key: "rating", header: "Overall Rating" },
              { key: "teachingRating", header: "Teaching" },
              { key: "energyRating", header: "Energy" },
              { key: "contentRating", header: "Content" },
              { key: "wouldRecommend", header: "Would Recommend" },
              { key: "comment", header: "Comment" },
              { key: "submittedAt", header: "Submitted At" },
            ]}
          />
          <button
            type="button"
            className="admin-btn-secondary"
            onClick={fetchFeedback}
            disabled={loading}
            title="Refresh feedback data"
          >
            {loading ? "Refreshing..." : "↻ Refresh"}
          </button>
        </div>
      </div>

      {/* KPI Cards Strip */}
      <div className="admin-kpi-grid">
        <AdminKpiCard
          title="Total Submissions"
          value={metrics.total}
          subtitle="Verified attendee reviews"
          icon="📝"
          tone="info"
        />
        <AdminKpiCard
          title="Average Rating"
          value={`⭐ ${metrics.average} / 5.0`}
          subtitle={`Based on ${feedbacks.length} visible ratings`}
          icon="🌟"
          tone="warning"
        />
        <AdminKpiCard
          title="Recommendation Rate"
          value={metrics.recommendRate}
          subtitle="Students would recommend"
          icon="👍"
          tone="success"
        />
        <AdminKpiCard
          title="5-Star Ratings"
          value={metrics.fiveStars}
          subtitle={
            feedbacks.length > 0
              ? `${Math.round((metrics.fiveStars / feedbacks.length) * 100)}% of submitted reviews`
              : "No reviews yet"
          }
          icon="🏆"
          tone="success"
        />
      </div>

      {/* Analytical Breakdown Panel: Rating Distribution & Dimension Scores */}
      <div className="feedback-analytics-card">
        <div className="analytics-section distribution-col">
          <h3 className="analytics-heading">Rating Distribution</h3>
          <div className="distribution-bars">
            {[5, 4, 3, 2, 1].map((stars) => {
              const count = metrics.distribution[stars] || 0;
              const total = feedbacks.length || 1;
              const pct = Math.round((count / total) * 100);
              return (
                <div key={stars} className="dist-row">
                  <span className="dist-star-label">{stars} ★</span>
                  <div className="dist-bar-track">
                    <div className="dist-bar-fill" style={{ width: `${pct}%` }} />
                  </div>
                  <span className="dist-count-label">
                    {count} ({pct}%)
                  </span>
                </div>
              );
            })}
          </div>
        </div>

        <div className="analytics-section dimensions-col">
          <h3 className="analytics-heading">Category Averages</h3>
          <div className="dimension-meters-grid">
            <div className="dimension-meter">
              <span className="dimension-label">Teaching Quality</span>
              <span className="dimension-score">⭐ {metrics.avgTeaching}</span>
              <div className="dimension-bar-track">
                <div
                  className="dimension-bar-fill"
                  style={{ width: `${(Number(metrics.avgTeaching) / 5) * 100}%` }}
                />
              </div>
            </div>

            <div className="dimension-meter">
              <span className="dimension-label">Class Energy & Vibe</span>
              <span className="dimension-score">⚡ {metrics.avgEnergy}</span>
              <div className="dimension-bar-track">
                <div
                  className="dimension-bar-fill"
                  style={{ width: `${(Number(metrics.avgEnergy) / 5) * 100}%` }}
                />
              </div>
            </div>

            <div className="dimension-meter">
              <span className="dimension-label">Choreography & Content</span>
              <span className="dimension-score">💃 {metrics.avgContent}</span>
              <div className="dimension-bar-track">
                <div
                  className="dimension-bar-fill"
                  style={{ width: `${(Number(metrics.avgContent) / 5) * 100}%` }}
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Filter & Search Bar */}
      <div className="feedback-filter-panel">
        <div className="filter-input-group search-group">
          <span className="filter-icon">🔍</span>
          <input
            type="text"
            className="filter-text-input"
            placeholder="Search by workshop, instructor, student, or keywords..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        <div className="filter-controls-row">
          <div className="filter-control">
            <label className="filter-label">Min Rating</label>
            <select
              className="filter-select"
              value={minRating}
              onChange={(e) => {
                setMinRating(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All Ratings</option>
              <option value="5">5 Stars only</option>
              <option value="4">4+ Stars</option>
              <option value="3">3+ Stars</option>
              <option value="2">2+ Stars</option>
            </select>
          </div>

          <div className="filter-control">
            <label className="filter-label">Instructor</label>
            <select
              className="filter-select"
              value={selectedTrainer}
              onChange={(e) => {
                setSelectedTrainer(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All Instructors</option>
              {trainers.map((t, idx) => {
                const val = t.userId || t.trainerId || t.id || t.trainerProfileId || `trainer-${idx}`;
                return (
                  <option key={val} value={val}>
                    {t.fullName || t.name || "Instructor"}
                  </option>
                );
              })}
            </select>
          </div>

          <div className="filter-control">
            <label className="filter-label">From Date</label>
            <input
              type="date"
              className="filter-date-input"
              value={startDate}
              onChange={(e) => {
                setStartDate(e.target.value);
                setPage(1);
              }}
            />
          </div>

          <div className="filter-control">
            <label className="filter-label">To Date</label>
            <input
              type="date"
              className="filter-date-input"
              value={endDate}
              onChange={(e) => {
                setEndDate(e.target.value);
                setPage(1);
              }}
            />
          </div>

          {(search || minRating || selectedTrainer || startDate || endDate) && (
            <button
              type="button"
              className="admin-btn-ghost clear-filters-btn"
              onClick={handleResetFilters}
            >
              Reset Filters
            </button>
          )}
        </div>
      </div>

      {/* Error Notice */}
      {error && (
        <div className="admin-error-banner" role="alert">
          <span className="error-icon">⚠️</span>
          <span>{error}</span>
          <button type="button" className="retry-btn" onClick={fetchFeedback}>
            Retry
          </button>
        </div>
      )}

      {/* Data Table */}
      <AdminDataTable
        columns={columns}
        data={filteredFeedbacks}
        loading={loading}
        emptyMessage="No feedback records found matching your selected filters."
        emptyIcon="📝"
        page={page}
        pageSize={pageSize}
        totalItems={totalCount}
        onPageChange={(newPage) => setPage(newPage)}
        onRowClick={handleRowClick}
      />

      {/* Feedback Detail Drawer / Modal */}
      {detailModalOpen && selectedFeedback && (
        <div
          className="admin-modal-backdrop"
          onClick={() => setDetailModalOpen(false)}
          role="dialog"
          aria-modal="true"
        >
          <div
            className="admin-feedback-detail-modal"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-header">
              <div className="modal-header-info">
                <div className="modal-header-meta">
                  <span className="modal-badge">FEEDBACK DETAILS</span>
                  <span className="feedback-status-badge submitted">✓ Submitted</span>
                </div>
                <h2 className="modal-title">{selectedFeedback.workshopTitle}</h2>
              </div>
              <button
                type="button"
                className="modal-close-btn"
                onClick={() => setDetailModalOpen(false)}
                aria-label="Close feedback modal"
              >
                ✕
              </button>
            </div>

            <div className="modal-body">
              {/* Context Header Strip */}
              <div className="feedback-context-strip">
                <div className="context-item">
                  <span className="context-label">Instructor</span>
                  <span className="context-val font-semibold">{selectedFeedback.trainerName}</span>
                </div>
                <div className="context-item">
                  <span className="context-label">Attendee</span>
                  <span className="context-val">{selectedFeedback.studentName}</span>
                </div>
                <div className="context-item">
                  <span className="context-label">Phone</span>
                  <div className="context-phone-row">
                    <span className="context-val font-mono">{selectedFeedback.studentPhone || "—"}</span>
                    {selectedFeedback.studentPhone && (
                      <button
                        type="button"
                        className="copy-mini-btn"
                        title="Copy phone number"
                        onClick={() => handleCopyPhone(selectedFeedback.studentPhone)}
                      >
                        {copiedPhone ? "✓ Copied" : "Copy"}
                      </button>
                    )}
                  </div>
                </div>
                <div className="context-item">
                  <span className="context-label">Submitted On</span>
                  <span className="context-val">
                    {selectedFeedback.submittedAt
                      ? new Date(selectedFeedback.submittedAt).toLocaleString()
                      : "—"}
                  </span>
                </div>
              </div>

              {/* Score Badges Grid */}
              <div className="modal-ratings-grid">
                <div className="score-card overall">
                  <span className="score-card-label">Overall Experience</span>
                  <div className="score-card-value">
                    <span className="score-num">{selectedFeedback.rating}</span>
                    <span className="score-max">/ 5</span>
                  </div>
                  {renderStars(selectedFeedback.rating)}
                </div>

                <div className="score-card">
                  <span className="score-card-label">Teaching Quality</span>
                  <div className="score-card-value">
                    <span className="score-num">{selectedFeedback.teachingRating || selectedFeedback.rating}</span>
                    <span className="score-max">/ 5</span>
                  </div>
                  {renderStars(selectedFeedback.teachingRating || selectedFeedback.rating)}
                </div>

                <div className="score-card">
                  <span className="score-card-label">Energy & Vibe</span>
                  <div className="score-card-value">
                    <span className="score-num">{selectedFeedback.energyRating || selectedFeedback.rating}</span>
                    <span className="score-max">/ 5</span>
                  </div>
                  {renderStars(selectedFeedback.energyRating || selectedFeedback.rating)}
                </div>

                <div className="score-card">
                  <span className="score-card-label">Content Quality</span>
                  <div className="score-card-value">
                    <span className="score-num">{selectedFeedback.contentRating || selectedFeedback.rating}</span>
                    <span className="score-max">/ 5</span>
                  </div>
                  {renderStars(selectedFeedback.contentRating || selectedFeedback.rating)}
                </div>
              </div>

              {/* Recommendation Strip */}
              <div className="modal-recommendation-box">
                <span className="recommendation-label">Student Recommendation:</span>
                {selectedFeedback.wouldRecommend ? (
                  <span className="rec-badge positive">
                    ✓ Would recommend this workshop to peers
                  </span>
                ) : (
                  <span className="rec-badge neutral">
                    Neutral / Would not explicitly recommend
                  </span>
                )}
              </div>

              {/* Written Review Comment */}
              <div className="modal-comment-section">
                <h4 className="comment-heading">Written Feedback</h4>
                {selectedFeedback.comment ? (
                  <blockquote className="feedback-blockquote">
                    “{selectedFeedback.comment}”
                  </blockquote>
                ) : (
                  <p className="feedback-no-comment">
                    The student submitted numeric ratings without an optional written review.
                  </p>
                )}
              </div>
            </div>

            <div className="modal-footer">
              <button
                type="button"
                className="admin-btn-secondary"
                onClick={() => setDetailModalOpen(false)}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
