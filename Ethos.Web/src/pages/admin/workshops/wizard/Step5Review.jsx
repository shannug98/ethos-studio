import React, { useState } from "react";
import { Check, AlertCircle, Calendar, Clock, MapPin, Users, IndianRupee, Eye, ShieldCheck, Sparkles, User, AlertTriangle, ExternalLink } from "lucide-react";
import { getTrainerPhotoUrl, DEFAULT_AVATAR_PLACEHOLDER } from "../../../../utils/mediaUrl";
import { getChronologicalGroupedSessions, getWorkshopTimingDisplay } from "../../../../utils/workshopPresentation";

export default function Step5Review({
  form,
  trainers,
  isEdit,
  saving,
  onSaveDraft,
  onSubmitApproval,
  onPublishNow,
  validationChecklist,
}) {
  const [modalPreviewOpen, setModalPreviewOpen] = useState(false);

  const assignedTrainerIds = Array.isArray(form.trainerProfileIds) && form.trainerProfileIds.length > 0
    ? form.trainerProfileIds
    : (form.trainerProfileId ? [form.trainerProfileId] : []);

  const selectedTrainers = assignedTrainerIds
    .map((id) => trainers.find((t) => (t.id || t.trainerProfileId || t.trainerId) === id))
    .filter(Boolean);

  const chronologicalGroupedSessions = getChronologicalGroupedSessions(form.sessions, form.workshopDate);
  const distinctDates = chronologicalGroupedSessions.map(([dateKey]) => dateKey);

  const selectedTrainer = selectedTrainers[0] || trainers.find(
    (t) => (t.id || t.trainerProfileId) === form.trainerProfileId
  );
  const trainerName = selectedTrainer?.name || selectedTrainer?.fullName || "Assigned Choreographer";
  const trainerStyles = selectedTrainer?.danceStyles || "";
  const trainerPhoto = selectedTrainer ? getTrainerPhotoUrl(selectedTrainer) : "";

  const allValid = Object.values(validationChecklist).every(Boolean);

  // Eligibility checks
  const isPast = form.workshopDate && new Date(`${form.workshopDate}T${form.startTime || "00:00"}:00`) < new Date();
  const isPrivate = !form.publicVisibility;
  const isEligible = allValid && !isPast && !isPrivate;

  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Review & Publish Workshop</h2>
        <p className="wizard-section-desc">
          Review the public card presentation and pre-publish eligibility checklist before publishing live on the Ethos website.
        </p>
      </div>

      <div className="wizard-review-grid">
        {/* Left Column: Live Public Poster / Card Preview */}
        <div className="review-preview-column">
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
            <h3 className="review-col-title" style={{ margin: 0 }}>
              <Eye size={16} />
              <span>Card Visual Preview</span>
            </h3>
            <button
              type="button"
              className="btn btn-sm btn-outline-secondary"
              style={{ fontSize: "11px", padding: "4px 10px", borderRadius: "9999px", background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.15)", color: "#fff", cursor: "pointer" }}
              onClick={() => setModalPreviewOpen(true)}
            >
              Full Screen Preview ↗
            </button>
          </div>

          <div className="public-card-preview">
            <div className="preview-image-container">
              {form.imageUrl ? (
                <img src={form.imageUrl} alt={form.title} className="preview-cover-img" />
              ) : (
                <div className="preview-placeholder-cover">
                  <Sparkles size={36} />
                  <span>3:4 Cover Poster</span>
                </div>
              )}
              <div className="preview-badge-overlay">
                <span className="preview-tag style-tag">
                  {form.danceStyle === "Other" ? form.customStyle || "Custom" : form.danceStyle}
                </span>
                <span className="preview-tag level-tag">{form.level}</span>
              </div>
              <div className="preview-price-tag">
                <span className="price-label">Starts at</span>
                <span className="price-val">₹500</span>
              </div>
            </div>

            <div className="preview-content-body">
              <h4 className="preview-title">{form.title || "Untitled Workshop"}</h4>

              {/* Trainer Chip */}
              <div style={{ display: "flex", alignItems: "center", gap: "8px", margin: "6px 0 10px" }}>
                <img
                  src={trainerPhoto || DEFAULT_AVATAR_PLACEHOLDER}
                  alt={trainerName}
                  style={{ width: "22px", height: "22px", borderRadius: "50%", objectFit: "cover", border: "1.5px solid #df806c" }}
                  onError={(e) => {
                    e.currentTarget.onerror = null;
                    e.currentTarget.src = DEFAULT_AVATAR_PLACEHOLDER;
                  }}
                />
                <span style={{ fontSize: "12px", color: "#fff", fontWeight: 600 }}>{trainerName}</span>
                {trainerStyles && (
                  <span style={{ fontSize: "11px", color: "#a1a1aa" }}>• {trainerStyles}</span>
                )}
              </div>

              <div className="preview-meta-row">
                <div className="preview-meta-item">
                  <Calendar size={13} />
                  <span>
                    {distinctDates.length > 1
                      ? `${distinctDates[0]} – ${distinctDates[distinctDates.length - 1]}`
                      : (distinctDates[0] || form.workshopDate || "TBD")}
                  </span>
                </div>
                <div className="preview-meta-item">
                  <Clock size={13} />
                  <span>
                    {(() => {
                      const timingText = getWorkshopTimingDisplay(form.sessions, form.startTime, form.endTime);
                      if (!timingText) return "TBD";
                      return timingText.includes("Multiple") ? timingText : `${timingText} IST`;
                    })()}
                  </span>
                </div>
              </div>

              <div className="preview-venue-row">
                <MapPin size={13} />
                <span>
                  {form.venue || "Studio Venue"}
                  {form.city ? `, ${form.city}` : ""}
                </span>
              </div>
              {form.locationUrl && (
                <div style={{ marginTop: "4px", paddingLeft: "18px" }}>
                  <a
                    href={form.locationUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    style={{ display: "inline-flex", alignItems: "center", gap: "4px", color: "#FF5500", fontSize: "11px", fontWeight: 600, textDecoration: "none" }}
                  >
                    <ExternalLink size={11} />
                    <span>Open Location Link ↗</span>
                  </a>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Right Column: Public Eligibility Status & Detailed Specs */}
        <div className="review-details-column">
          {/* Public Eligibility Banner */}
          <div
            style={{
              padding: "16px 20px",
              borderRadius: "12px",
              marginBottom: "16px",
              background: isEligible
                ? "rgba(16, 185, 129, 0.12)"
                : isPrivate
                ? "rgba(245, 158, 11, 0.12)"
                : "rgba(239, 68, 68, 0.12)",
              border: `1px solid ${
                isEligible
                  ? "rgba(16, 185, 129, 0.4)"
                  : isPrivate
                  ? "rgba(245, 158, 11, 0.4)"
                  : "rgba(239, 68, 68, 0.4)"
              }`,
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "10px", marginBottom: "4px" }}>
              {isEligible ? (
                <ShieldCheck size={20} color="#10b981" />
              ) : isPrivate ? (
                <AlertTriangle size={20} color="#f59e0b" />
              ) : (
                <AlertCircle size={20} color="#ef4444" />
              )}
              <strong style={{ fontSize: "14px", color: isEligible ? "#10b981" : isPrivate ? "#f59e0b" : "#ef4444" }}>
                {isEligible
                  ? "Eligible for Public Listing"
                  : isPrivate
                  ? "Private Direct-Link Event (Unlisted)"
                  : "Ineligible for Public Listing"}
              </strong>
            </div>
            <p style={{ margin: 0, fontSize: "12px", color: "#a1a1aa", lineHeight: 1.5 }}>
              {isEligible
                ? "Upon publishing, this workshop will immediately appear on the homepage and the public workshop calendar."
                : isPrivate
                ? "Public visibility is set to OFF. Attendees will only be able to book through the direct link."
                : "Please resolve the incomplete checklist requirements before publishing live."}
            </p>
          </div>

          {/* Validation Checklist Card */}
          <div className="review-checklist-card">
            <h4 className="checklist-title">Eligibility Criteria</h4>
            <div className="checklist-items">
              <div className={`checklist-item ${validationChecklist.details ? "valid" : "invalid"}`}>
                {validationChecklist.details ? <Check size={16} /> : <AlertCircle size={16} />}
                <span>Workshop details &amp; faculty complete</span>
              </div>
              <div className={`checklist-item ${validationChecklist.portrait ? "valid" : "invalid"}`}>
                {validationChecklist.portrait ? <Check size={16} /> : <AlertCircle size={16} />}
                <span>3:4 Portrait cover image uploaded</span>
              </div>
              <div className={`checklist-item ${validationChecklist.venue ? "valid" : "invalid"}`}>
                {validationChecklist.venue ? <Check size={16} /> : <AlertCircle size={16} />}
                <span>Venue & session schedule configured</span>
              </div>
              <div className={`checklist-item ${validationChecklist.capacity ? "valid" : "invalid"}`}>
                {validationChecklist.capacity ? <Check size={16} /> : <AlertCircle size={16} />}
                <span>Capacity defined (min 5 seats)</span>
              </div>
              <div className={`checklist-item ${!isPast ? "valid" : "invalid"}`}>
                {!isPast ? <Check size={16} /> : <AlertCircle size={16} />}
                <span>Schedule is set in the future</span>
              </div>
              <div className={`checklist-item ${!isPrivate ? "valid" : "invalid"}`}>
                {!isPrivate ? <Check size={16} /> : <AlertTriangle size={16} />}
                <span>Public website visibility enabled</span>
              </div>
            </div>
          </div>

          {/* Configuration Summary per Requirement H */}
          <div className="review-summary-card">
            <h4 className="summary-title" style={{ fontSize: "14px", fontWeight: 800, color: "#f8fafc", marginBottom: "12px" }}>
              Workshop Architectural Summary
            </h4>

            {/* DATES */}
            <div style={{ marginBottom: "14px" }}>
              <span style={{ fontSize: "11px", fontWeight: 700, textTransform: "uppercase", color: "#94a3b8", display: "block", marginBottom: "4px" }}>
                Workshop Dates ({distinctDates.length})
              </span>
              <div style={{ display: "flex", gap: "6px", flexWrap: "wrap" }}>
                {distinctDates.map((d, i) => (
                  <span key={i} style={{ padding: "3px 8px", borderRadius: "6px", background: "rgba(255, 85, 0, 0.12)", color: "#FF5500", border: "1px solid rgba(255, 85, 0, 0.3)", fontSize: "12px", fontWeight: 600 }}>
                    📅 {d}
                  </span>
                ))}
              </div>
            </div>

            {/* TRAINERS */}
            <div style={{ marginBottom: "14px" }}>
              <span style={{ fontSize: "11px", fontWeight: 700, textTransform: "uppercase", color: "#94a3b8", display: "block", marginBottom: "4px" }}>
                Instructors / Faculty ({selectedTrainers.length})
              </span>
              <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
                {selectedTrainers.map((t, idx) => (
                  <div key={t.id || idx} style={{ display: "flex", alignItems: "center", gap: "6px", padding: "4px 8px", borderRadius: "6px", background: "#1e293b", border: "1px solid #334155", fontSize: "12px" }}>
                    <span style={{ fontWeight: 600, color: "#f8fafc" }}>{t.fullName || t.name}</span>
                  </div>
                ))}
              </div>
            </div>

            {/* SESSIONS */}
            <div style={{ marginBottom: "14px" }}>
              <span style={{ fontSize: "11px", fontWeight: 700, textTransform: "uppercase", color: "#94a3b8", display: "block", marginBottom: "4px" }}>
                Scheduled Sessions ({Array.isArray(form.sessions) ? form.sessions.length : 0})
              </span>
              <div style={{ display: "flex", flexDirection: "column", gap: "8px", maxHeight: "180px", overflowY: "auto" }}>
                {chronologicalGroupedSessions.map(([dateKey, dateSessions]) => (
                  <div key={dateKey} style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
                    <span style={{ fontSize: "11px", fontWeight: 700, color: "#FF5500", textTransform: "uppercase" }}>
                      📅 {dateKey}
                    </span>
                    {dateSessions.map((s, idx) => {
                      const tr = trainers.find((t) => (t.id || t.trainerProfileId || t.trainerId) === s.trainerProfileId);
                      return (
                        <div key={s.id || idx} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "6px 10px", borderRadius: "6px", background: "#0f172a", border: "1px solid rgba(255,255,255,0.06)", fontSize: "11px" }}>
                          <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                            <span style={{ color: "#94a3b8" }}>{s.startTime ? s.startTime.slice(0, 5) : ""} - {s.endTime ? s.endTime.slice(0, 5) : ""}</span>
                            <strong style={{ color: "#f8fafc" }}>{s.title || `Session ${idx + 1}`}</strong>
                          </div>
                          <span style={{ color: "#38bdf8" }}>{tr?.fullName || tr?.name || "Trainer"}</span>
                        </div>
                      );
                    })}
                  </div>
                ))}
              </div>
            </div>

            {/* PASS TYPES */}
            <div>
              <span style={{ fontSize: "11px", fontWeight: 700, textTransform: "uppercase", color: "#94a3b8", display: "block", marginBottom: "4px" }}>
                Configured Pass Types ({Array.isArray(form.passTypes) ? form.passTypes.length : 0})
              </span>
              <div style={{ display: "flex", gap: "6px", flexWrap: "wrap" }}>
                {(form.passTypes || []).map((p, idx) => (
                  <div key={idx} style={{ padding: "4px 8px", borderRadius: "6px", background: "rgba(56, 189, 248, 0.1)", border: "1px solid rgba(56, 189, 248, 0.3)", fontSize: "11px", color: "#38bdf8" }}>
                    <strong>{p.name}</strong>: ₹{p.price} ({p.sessionsIncluded == null ? "All Sessions" : `${p.sessionsIncluded} S`})
                  </div>
                ))}
              </div>
            </div>

            {/* VENUE & LOCATION LINK */}
            <div style={{ marginTop: "14px" }}>
              <span style={{ fontSize: "11px", fontWeight: 700, textTransform: "uppercase", color: "#94a3b8", display: "block", marginBottom: "4px" }}>
                Venue & Location Link
              </span>
              <div style={{ fontSize: "12px", color: "#f8fafc" }}>
                <div><strong>{form.venue || "Venue not set"}</strong>{form.venueAddress ? ` — ${form.venueAddress}` : ""}</div>
                {form.locationUrl && (
                  <div style={{ marginTop: "4px" }}>
                    <a
                      href={form.locationUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      style={{ display: "inline-flex", alignItems: "center", gap: "4px", color: "#38bdf8", textDecoration: "none" }}
                    >
                      <ExternalLink size={12} />
                      <span>Open Location Link ↗</span>
                    </a>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Publishing Actions */}
          <div className="review-actions-wrap">
            <button
              type="button"
              className="btn-wizard-draft"
              disabled={saving}
              onClick={onSaveDraft}
            >
              {saving ? "Saving..." : isEdit ? "Save Changes" : "Save as Draft"}
            </button>

            <button
              type="button"
              className="btn-wizard-approval"
              disabled={saving || !allValid}
              onClick={onSubmitApproval}
              title={!allValid ? "Complete all required fields first" : ""}
            >
              {saving ? "Submitting..." : "Submit for Approval"}
            </button>

            <button
              type="button"
              className="btn-wizard-publish"
              disabled={saving || !allValid}
              onClick={onPublishNow}
              title={!allValid ? "Complete all required fields first" : ""}
            >
              <Sparkles size={16} />
              <span>{saving ? "Publishing..." : isEdit ? "Update & Publish" : "Publish Workshop"}</span>
            </button>
          </div>
        </div>
      </div>

      {/* Full Modal Preview */}
      {modalPreviewOpen && (
        <div
          className="admin-modal-backdrop"
          onClick={() => setModalPreviewOpen(false)}
          style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.85)", backdropFilter: "blur(8px)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 9999, padding: "20px" }}
        >
          <div
            className="admin-modal-card"
            onClick={(e) => e.stopPropagation()}
            style={{ maxWidth: "560px", width: "100%", background: "#0f172a", border: "1px solid rgba(255,255,255,0.15)", borderRadius: "16px", padding: "24px", color: "#fff" }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px" }}>
              <h3 style={{ margin: 0, fontSize: "18px" }}>Public Website Workshop Preview</h3>
              <button
                type="button"
                onClick={() => setModalPreviewOpen(false)}
                style={{ background: "transparent", border: "none", color: "#a1a1aa", fontSize: "18px", cursor: "pointer" }}
              >
                ✕
              </button>
            </div>

            {form.imageUrl && (
              <img
                src={form.imageUrl}
                alt={form.title}
                style={{ width: "100%", maxHeight: "300px", objectFit: "cover", borderRadius: "12px", marginBottom: "16px" }}
              />
            )}

            <h2 style={{ fontSize: "22px", margin: "0 0 10px" }}>{form.title || "Untitled Workshop"}</h2>
            <div style={{ display: "flex", alignItems: "center", gap: "10px", marginBottom: "16px" }}>
              <img
                src={trainerPhoto || DEFAULT_AVATAR_PLACEHOLDER}
                alt={trainerName}
                style={{ width: "32px", height: "32px", borderRadius: "50%", objectFit: "cover" }}
                onError={(e) => {
                  e.currentTarget.onerror = null;
                  e.currentTarget.src = DEFAULT_AVATAR_PLACEHOLDER;
                }}
              />
              <div>
                <div style={{ fontWeight: "bold" }}>{trainerName}</div>
                <div style={{ fontSize: "12px", color: "#a1a1aa" }}>{trainerStyles || "Ethos Choreographer"}</div>
              </div>
            </div>

            <p style={{ fontSize: "14px", color: "#cbd5e1", lineHeight: 1.6, marginBottom: "20px" }}>
              {form.shortDescription || form.fullDescription || "Experience transformative dance movements with Ethos."}
            </p>

            <div style={{ display: "flex", justifyContent: "flex-end" }}>
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => setModalPreviewOpen(false)}
              >
                Close Preview
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
