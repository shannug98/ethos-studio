import React, { useState, useEffect } from "react";
import "./AdminTrainerApplicationDetailsModal.css";
import { adminApi, getAdminToken } from "../../services/adminApi";

export default function AdminTrainerApplicationDetailsModal({
  isOpen,
  application,
  onClose,
  onApplicationUpdated,
}) {
  const [activeDecision, setActiveDecision] = useState(null); // 'approve' | 'changes' | 'reject'
  const [decisionReason, setDecisionReason] = useState("");
  const [adminNotes, setAdminNotes] = useState("");
  const [actionLoading, setActionLoading] = useState(false);
  const [actionError, setActionError] = useState("");

  // Video streaming state
  const [videoBlobUrl, setVideoBlobUrl] = useState("");
  const [videoLoading, setVideoLoading] = useState(false);
  const [videoError, setVideoError] = useState("");

  // Clean state when modal opens/closes or application changes
  useEffect(() => {
    setActiveDecision(null);
    setDecisionReason("");
    setAdminNotes("");
    setActionError("");
    setVideoError("");

    if (!isOpen || !application) {
      if (videoBlobUrl) {
        URL.revokeObjectURL(videoBlobUrl);
        setVideoBlobUrl("");
      }
      return;
    }

    // Load audition video if available
    if (application.hasVideo) {
      setVideoLoading(true);
      const token = getAdminToken();
      fetch(`/api/admin/trainer-applications/${application.id}/video/stream`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      })
        .then((res) => {
          if (!res.ok) throw new Error("Could not load candidate video stream.");
          return res.blob();
        })
        .then((blob) => {
          const url = URL.createObjectURL(blob);
          setVideoBlobUrl(url);
        })
        .catch((err) => {
          console.warn("Audition video stream error:", err);
          setVideoError(err.message || "Failed to load audition video.");
        })
        .finally(() => {
          setVideoLoading(false);
        });
    }

    return () => {
      if (videoBlobUrl) {
        URL.revokeObjectURL(videoBlobUrl);
      }
    };
  }, [isOpen, application?.id, application?.hasVideo]);

  // Keyboard Esc listener
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen || !application) return null;

  const handleDecisionSubmit = async () => {
    setActionError("");

    if (activeDecision === "reject" && !decisionReason.trim()) {
      setActionError("A rejection reason is strictly mandatory.");
      return;
    }
    if (activeDecision === "changes" && !decisionReason.trim()) {
      setActionError("Specific requested changes notes are mandatory.");
      return;
    }

    setActionLoading(true);
    try {
      if (activeDecision === "approve") {
        await adminApi.approveTrainerApplication(application.id, adminNotes.trim());
      } else if (activeDecision === "reject") {
        await adminApi.rejectTrainerApplication(application.id, decisionReason.trim(), adminNotes.trim());
      } else if (activeDecision === "changes") {
        await adminApi.requestChangesTrainerApplication(application.id, decisionReason.trim());
      }

      setActiveDecision(null);
      if (onApplicationUpdated) {
        onApplicationUpdated();
      }
      onClose();
    } catch (err) {
      setActionError(err.message || "Failed to submit application review decision.");
    } finally {
      setActionLoading(false);
    }
  };

  const isPending =
    application.status === "Pending" ||
    application.status === "Submitted" ||
    application.status === "ChangesRequested";

  const initials = (application.fullName || "Trainer")
    .split(" ")
    .map((n) => n[0])
    .join("")
    .toUpperCase()
    .substring(0, 2);

  const formattedDob = application.dateOfBirth
    ? new Date(application.dateOfBirth).toLocaleDateString("en-IN", {
        year: "numeric",
        month: "short",
        day: "numeric",
      })
    : null;

  const formattedSubmitted = application.submittedAt || application.createdAt
    ? new Date(application.submittedAt || application.createdAt).toLocaleDateString("en-IN", {
        year: "numeric",
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      })
    : "—";

  return (
    <div className="app-details-backdrop" onClick={onClose}>
      <div className="app-details-modal" onClick={(e) => e.stopPropagation()}>
        {/* MODAL HEADER */}
        <div className="app-details-header">
          <div className="app-details-header-title">
            <h3>Trainer Candidate Application Dossier</h3>
            <p>
              ID: <strong>{application.id}</strong> · Submitted: {formattedSubmitted}
            </p>
          </div>
          <button className="app-details-close-btn" onClick={onClose}>
            ✕
          </button>
        </div>

        {/* MODAL BODY */}
        <div className="app-details-body">
          {/* Candidate Hero Card */}
          <div className="candidate-hero-card">
            <div className="candidate-avatar">
              {application.profilePhotoUrl ? (
                <img src={application.profilePhotoUrl} alt={application.fullName} />
              ) : (
                initials
              )}
            </div>
            <div className="candidate-meta" style={{ flex: 1 }}>
              <h4>{application.fullName || "Candidate Applicant"}</h4>
              <div className="candidate-meta-badges">
                <span className="meta-badge badge-code">
                  Code: {application.trainerCode || "Unassigned"}
                </span>
                <span className="meta-badge badge-tier">
                  Pathway: {application.tier || "Silver Tier"}
                </span>
                <span className={`status-pill status-${application.status?.toLowerCase()}`}>
                  {application.status}
                </span>
              </div>
              <div className="candidate-contact-row">
                <span>📱 {application.phone || "—"}</span>
                {application.email && <span>✉️ {application.email}</span>}
                {application.city && <span>📍 {application.city}</span>}
                {formattedDob && <span>🎂 DOB: {formattedDob}</span>}
              </div>
            </div>
          </div>

          {/* 2-Column Info Grid */}
          <div className="details-grid">
            {/* Column 1: Dance Experience & Background */}
            <div className="details-section-card">
              <h5>🩰 Dance Styles & Background</h5>
              <div className="info-item">
                <span className="info-label">Primary Dance Style</span>
                <div className="style-tags">
                  <span className="style-tag">
                    {application.primaryDanceStyle || "Not Specified"}
                  </span>
                </div>
              </div>

              {application.secondaryDanceStyles && (
                <div className="info-item">
                  <span className="info-label">Secondary Dance Styles</span>
                  <div className="style-tags">
                    {application.secondaryDanceStyles.split(",").map((s, idx) => (
                      <span key={idx} className="style-tag style-tag-secondary">
                        {s.trim()}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              <div className="info-item">
                <span className="info-label">Teaching & Performance Experience</span>
                <span className="info-value">
                  {application.experienceYears ? `${application.experienceYears} Years` : "—"}
                </span>
              </div>

              <div className="info-item">
                <span className="info-label">Current Studio / Affiliation</span>
                <span className="info-value">{application.currentStudio || "Independent"}</span>
              </div>

              <div className="info-item">
                <span className="info-label">Candidate Biography</span>
                <div className="bio-box">{application.bio || "No biography provided."}</div>
              </div>

              {(application.instagramUrl || application.youTubeUrl) && (
                <div className="info-item">
                  <span className="info-label">Portfolio & Social Handles</span>
                  <div style={{ display: "flex", gap: "12px", marginTop: "4px", fontSize: "0.85rem" }}>
                    {application.instagramUrl && (
                      <a
                        href={application.instagramUrl.startsWith("http") ? application.instagramUrl : `https://instagram.com/${application.instagramUrl.replace("@", "")}`}
                        target="_blank"
                        rel="noreferrer"
                        style={{ color: "#0284c7", textDecoration: "underline", fontWeight: 600 }}
                      >
                        📸 Instagram Profile
                      </a>
                    )}
                    {application.youTubeUrl && (
                      <a
                        href={application.youTubeUrl}
                        target="_blank"
                        rel="noreferrer"
                        style={{ color: "#dc2626", textDecoration: "underline", fontWeight: 600 }}
                      >
                        ▶️ YouTube Channel
                      </a>
                    )}
                  </div>
                </div>
              )}

              {application.applicationNotes && (
                <div className="info-item">
                  <span className="info-label">Applicant Notes</span>
                  <div className="bio-box">{application.applicationNotes}</div>
                </div>
              )}
            </div>

            {/* Column 2: Selected Tier & Payment Information */}
            <div className="details-section-card">
              <h5>💳 Pathway Tier & Payment Verification</h5>

              <div className="info-item">
                <span className="info-label">Selected Tier</span>
                <span className="info-value" style={{ fontWeight: 700, color: "#0f172a" }}>
                  {application.tier || "Standard Tier"}
                </span>
              </div>

              <div className="info-item">
                <span className="info-label">Application Fee</span>
                <span className="info-value" style={{ color: "#16a34a", fontWeight: 700 }}>
                  ₹{application.tierFee ? Number(application.tierFee).toLocaleString("en-IN") : "1,500"}
                </span>
              </div>

              <div className="info-item">
                <span className="info-label">Payment Status</span>
                <span className="info-value">
                  {application.paymentVerifiedAt ? (
                    <span className="badge badge-success" style={{ padding: "4px 8px" }}>
                      ✓ Payment Verified
                    </span>
                  ) : application.paymentStatus ? (
                    <span className="badge badge-warning" style={{ padding: "4px 8px" }}>
                      {application.paymentStatus}
                    </span>
                  ) : (
                    <span className="badge badge-secondary" style={{ padding: "4px 8px" }}>
                      Unverified
                    </span>
                  )}
                </span>
              </div>

              {application.paymentAmount && (
                <div className="info-item">
                  <span className="info-label">Amount Paid</span>
                  <span className="info-value">
                    ₹{Number(application.paymentAmount).toLocaleString("en-IN")}
                  </span>
                </div>
              )}

              {application.paymentReference && (
                <div className="info-item">
                  <span className="info-label">Payment Reference / Txn ID</span>
                  <span className="info-value" style={{ fontFamily: "monospace" }}>
                    {application.paymentReference}
                  </span>
                </div>
              )}

              {application.paymentDate && (
                <div className="info-item">
                  <span className="info-label">Payment Date</span>
                  <span className="info-value">
                    {new Date(application.paymentDate).toLocaleDateString("en-IN", {
                      year: "numeric",
                      month: "short",
                      day: "numeric",
                    })}
                  </span>
                </div>
              )}

              {/* Admin Decision History */}
              <div style={{ marginTop: "16px", paddingTop: "12px", borderTop: "1px solid #f1f5f9" }}>
                <span className="info-label">Review Status & History</span>
                <div style={{ fontSize: "0.85rem", color: "#475569", marginTop: "4px" }}>
                  {application.reviewedAt ? (
                    <>Reviewed on {new Date(application.reviewedAt).toLocaleDateString("en-IN")}</>
                  ) : (
                    <>Awaiting administrator decision</>
                  )}
                </div>

                {application.adminNotes && (
                  <div style={{ marginTop: "8px" }}>
                    <span className="info-label">Internal Admin Notes:</span>
                    <div className="bio-box" style={{ background: "#fffbeb", borderColor: "#fde68a" }}>
                      {application.adminNotes}
                    </div>
                  </div>
                )}

                {application.rejectionReason && (
                  <div style={{ marginTop: "8px" }}>
                    <span className="info-label">Recorded Rejection Reason:</span>
                    <div className="bio-box" style={{ background: "#fef2f2", borderColor: "#fecaca", color: "#991b1b" }}>
                      {application.rejectionReason}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Introduction & Audition Video Section */}
          <div className="video-section-card">
            <h5>
              <span>🎬 Introduction & Audition Video</span>
              {application.hasVideo && application.videoDurationSeconds && (
                <span className="video-meta-tag">
                  Duration: {application.videoDurationSeconds}s · Size:{" "}
                  {application.videoSizeBytes
                    ? `${(application.videoSizeBytes / (1024 * 1024)).toFixed(1)} MB`
                    : "—"}
                </span>
              )}
            </h5>

            {application.hasVideo ? (
              <div className="video-player-wrapper">
                {videoLoading && (
                  <div className="video-loading-placeholder">
                    <span>Loading authenticated audition video stream...</span>
                  </div>
                )}

                {videoError && (
                  <div className="video-loading-placeholder" style={{ color: "#ef4444" }}>
                    ⚠️ {videoError}
                  </div>
                )}

                {videoBlobUrl && (
                  <video
                    src={videoBlobUrl}
                    controls
                    preload="metadata"
                    playsInline
                  >
                    Your browser does not support HTML5 video streaming.
                  </video>
                )}
              </div>
            ) : application.youTubeUrl ? (
              <div style={{ padding: "16px", textAlign: "center", background: "#ffffff", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                <p style={{ margin: "0 0 10px 0", fontSize: "0.9rem", color: "#334155" }}>
                  Candidate submitted an external video link:
                </p>
                <a
                  href={application.youTubeUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="btn btn-sm btn-primary"
                  style={{ display: "inline-flex", alignItems: "center", gap: "6px" }}
                >
                  ▶️ Open Candidate Audition Link
                </a>
              </div>
            ) : (
              <div className="video-empty-placeholder">
                No audition video or link submitted for this candidate application.
              </div>
            )}
          </div>

          {/* Decision Panel */}
          {isPending && (
            <div className="decision-panel">
              <h5 style={{ margin: "0 0 12px 0", fontSize: "0.95rem", fontWeight: 700, color: "#0f172a" }}>
                Take Administrative Action
              </h5>

              <div className="decision-buttons-row">
                <button
                  type="button"
                  className="btn-action-approve"
                  onClick={() => {
                    setActiveDecision("approve");
                    setDecisionReason("");
                    setActionError("");
                  }}
                  disabled={actionLoading}
                >
                  ✓ Approve Candidate
                </button>

                <button
                  type="button"
                  className="btn-action-changes"
                  onClick={() => {
                    setActiveDecision("changes");
                    setDecisionReason("");
                    setActionError("");
                  }}
                  disabled={actionLoading}
                >
                  ✎ Request Changes
                </button>

                <button
                  type="button"
                  className="btn-action-reject"
                  onClick={() => {
                    setActiveDecision("reject");
                    setDecisionReason("");
                    setActionError("");
                  }}
                  disabled={actionLoading}
                >
                  ✕ Reject Application
                </button>
              </div>

              {/* Expandable Decision Input Form */}
              {activeDecision && (
                <div className="decision-form-panel">
                  {actionError && (
                    <div style={{ padding: "8px 12px", background: "#fef2f2", color: "#dc2626", borderRadius: "6px", marginBottom: "12px", fontSize: "0.85rem", fontWeight: 600 }}>
                      ⚠️ {actionError}
                    </div>
                  )}

                  {activeDecision !== "approve" && (
                    <div style={{ marginBottom: "12px" }}>
                      <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 700, color: "#334155", marginBottom: "4px" }}>
                        {activeDecision === "reject"
                          ? "Rejection Reason (Strictly Mandatory):"
                          : "Specific Changes Requested (Strictly Mandatory):"}
                      </label>
                      <textarea
                        rows={3}
                        placeholder={
                          activeDecision === "reject"
                            ? "Explain why the application does not meet studio requirements..."
                            : "Specify exact updates or adjustments required from the candidate..."
                        }
                        value={decisionReason}
                        onChange={(e) => setDecisionReason(e.target.value)}
                        disabled={actionLoading}
                      />
                    </div>
                  )}

                  <div style={{ marginBottom: "12px" }}>
                    <label style={{ display: "block", fontSize: "0.8rem", fontWeight: 700, color: "#334155", marginBottom: "4px" }}>
                      Internal Administrative Notes (Optional):
                    </label>
                    <textarea
                      rows={2}
                      placeholder="Notes for studio management logs..."
                      value={adminNotes}
                      onChange={(e) => setAdminNotes(e.target.value)}
                      disabled={actionLoading}
                    />
                  </div>

                  <div className="decision-form-panel-actions">
                    <button
                      type="button"
                      className="btn btn-secondary"
                      onClick={() => setActiveDecision(null)}
                      disabled={actionLoading}
                    >
                      Cancel
                    </button>
                    <button
                      type="button"
                      className={`btn ${
                        activeDecision === "approve"
                          ? "btn-success"
                          : activeDecision === "reject"
                          ? "btn-danger"
                          : "btn-warning"
                      }`}
                      onClick={handleDecisionSubmit}
                      disabled={actionLoading}
                    >
                      {actionLoading
                        ? "Submitting Decision..."
                        : activeDecision === "approve"
                        ? "Confirm Approval"
                        : activeDecision === "reject"
                        ? "Confirm Rejection"
                        : "Submit Changes Request"}
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
