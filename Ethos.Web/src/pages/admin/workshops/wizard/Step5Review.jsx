import React, { useState } from "react";
import { Check, AlertCircle, Calendar, Clock, MapPin, Users, IndianRupee, Eye, ShieldCheck, Sparkles, User, AlertTriangle } from "lucide-react";

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

  const selectedTrainer = trainers.find(
    (t) => (t.id || t.trainerProfileId) === form.trainerProfileId
  );
  const trainerName = selectedTrainer?.name || selectedTrainer?.fullName || "Assigned Choreographer";
  const trainerStyles = selectedTrainer?.danceStyles || "";
  const trainerPhoto = selectedTrainer?.profilePhotoUrl || selectedTrainer?.photoUrl || "";

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
                {trainerPhoto ? (
                  <img
                    src={trainerPhoto}
                    alt={trainerName}
                    style={{ width: "22px", height: "22px", borderRadius: "50%", objectFit: "cover", border: "1.5px solid #e11d48" }}
                  />
                ) : (
                  <span style={{ width: "22px", height: "22px", borderRadius: "50%", background: "#333", color: "#fff", display: "inline-flex", alignItems: "center", justifyContent: "center", fontSize: "10px", fontWeight: "bold" }}>
                    {trainerName.charAt(0)}
                  </span>
                )}
                <span style={{ fontSize: "12px", color: "#fff", fontWeight: 600 }}>{trainerName}</span>
                {trainerStyles && (
                  <span style={{ fontSize: "11px", color: "#a1a1aa" }}>• {trainerStyles}</span>
                )}
              </div>

              <div className="preview-meta-row">
                <div className="preview-meta-item">
                  <Calendar size={13} />
                  <span>{form.workshopDate || "TBD"}</span>
                </div>
                <div className="preview-meta-item">
                  <Clock size={13} />
                  <span>
                    {form.startTime || "00:00"} – {form.endTime || "00:00"} IST
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
                <span>Workshop details & lead trainer complete</span>
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

          {/* Configuration Summary */}
          <div className="review-summary-card">
            <h4 className="summary-title">Workshop Specifications</h4>
            <div className="summary-grid">
              <div className="summary-row">
                <span className="summary-lbl">Capacity:</span>
                <span className="summary-val">{form.capacity} seats</span>
              </div>
              <div className="summary-row">
                <span className="summary-lbl">Registration:</span>
                <span className="summary-val">{form.registrationType}</span>
              </div>
              <div className="summary-row">
                <span className="summary-lbl">Visibility:</span>
                <span className="summary-val">
                  {form.publicVisibility ? "Public (Live on Website)" : "Private (Direct Link Only)"}
                </span>
              </div>
              <div className="summary-row">
                <span className="summary-lbl">Pricing Tiers:</span>
                <span className="summary-val">₹500 / ₹600 / ₹700 / ₹800 (Server Authoritative)</span>
              </div>
              <div className="summary-row">
                <span className="summary-lbl">Contact:</span>
                <span className="summary-val">
                  {form.contactPerson || "Admin"} • {form.contactNumber || "—"}
                </span>
              </div>
              <div className="summary-row">
                <span className="summary-lbl">Timezone:</span>
                <span className="summary-val">Asia/Kolkata (IST)</span>
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
              {trainerPhoto ? (
                <img src={trainerPhoto} alt={trainerName} style={{ width: "32px", height: "32px", borderRadius: "50%", objectFit: "cover" }} />
              ) : (
                <span style={{ width: "32px", height: "32px", borderRadius: "50%", background: "#333", display: "inline-flex", alignItems: "center", justifyContent: "center" }}>{trainerName.charAt(0)}</span>
              )}
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
