import React from "react";
import { Check, AlertCircle, Calendar, Clock, MapPin, Users, IndianRupee, Eye, ShieldCheck, Sparkles, User } from "lucide-react";

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
  const selectedTrainer = trainers.find(
    (t) => (t.id || t.trainerProfileId) === form.trainerProfileId
  );
  const trainerName = selectedTrainer?.name || selectedTrainer?.fullName || "Assigned Choreographer";

  const allValid = Object.values(validationChecklist).every(Boolean);

  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Review & Publish Workshop</h2>
        <p className="wizard-section-desc">
          Review the preview poster and workshop configuration before saving as draft, submitting for admin approval, or publishing live.
        </p>
      </div>

      <div className="wizard-review-grid">
        {/* Left Column: Live Public Poster / Card Preview */}
        <div className="review-preview-column">
          <h3 className="review-col-title">
            <Eye size={16} />
            <span>Public Card Preview</span>
          </h3>

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
              <p className="preview-trainer">
                <User size={14} />
                <span>{trainerName}</span>
              </p>

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

        {/* Right Column: Validation Checklist & Detailed Specs */}
        <div className="review-details-column">
          {/* Validation Checklist Card */}
          <div className="review-checklist-card">
            <h4 className="checklist-title">Pre-Publish Verification</h4>
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
                <span>Attendee capacity defined (min 5 seats)</span>
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
                <span className="summary-val">₹500 / ₹600 / ₹700 / ₹800 (Dynamic)</span>
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
    </div>
  );
}
