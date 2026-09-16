import React from "react";
import { Info, User, Tag, MapPin, Phone, ShieldCheck, FileText } from "lucide-react";

export const DANCE_STYLE_PRESETS = [
  "Hip Hop",
  "Urban Choreography",
  "Contemporary",
  "Commercial",
  "Jazz",
  "Ballet",
  "Salsa",
  "Bachata",
  "Afro",
  "Dancehall",
  "Bollywood",
  "Heels",
  "Waacking",
  "Krump",
  "Fusion",
  "Other",
];

export const LEVEL_PRESETS = ["Open Level", "Beginner", "Intermediate", "Advanced"];

const STANDARD_POLICY_TEMPLATE = 
`Cancellation & Refund Policy:
1. Cancellations made at least 48 hours prior to workshop start receive a full refund or studio credit.
2. Cancellations within 24-48 hours are eligible for 50% studio credit.
3. No refunds or credits for cancellations under 24 hours or no-shows.
4. Studio reserves the right to reschedule in case of emergency with full refund option.`;

export default function Step1Details({ form, onChange, trainers, loadingTrainers, errors }) {
  const isCustomStyle = form.danceStyle === "Other";

  const handleStyleSelect = (style) => {
    onChange("danceStyle", style);
    if (style !== "Other") {
      onChange("customStyle", "");
    }
  };

  const handleApplyPolicyTemplate = () => {
    onChange("termsAndCancellationPolicy", STANDARD_POLICY_TEMPLATE);
  };

  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Workshop Details</h2>
        <p className="wizard-section-desc">
          Enter core information about your workshop, lead instructor, dance style, and public visibility.
        </p>
      </div>

      {/* Workshop Title */}
      <div className="wizard-form-group">
        <label className="wizard-label">
          Workshop Title <span className="req">*</span>
        </label>
        <input
          type="text"
          className={`wizard-input ${errors.title ? "has-error" : ""}`}
          placeholder="e.g. Urban Groove Masterclass with Rahul"
          value={form.title}
          onChange={(e) => onChange("title", e.target.value)}
          maxLength={120}
        />
        {errors.title && <span className="wizard-error-text">{errors.title}</span>}
      </div>

      {/* Trainer & Level Row */}
      <div className="wizard-form-grid-2">
        <div className="wizard-form-group">
          <label className="wizard-label">
            Lead Trainer / Choreographer <span className="req">*</span>
          </label>
          <div className="wizard-select-wrap">
            <User size={16} className="wizard-input-icon" />
            <select
              className={`wizard-select ${errors.trainerProfileId ? "has-error" : ""}`}
              value={form.trainerProfileId}
              onChange={(e) => onChange("trainerProfileId", e.target.value)}
              disabled={loadingTrainers}
            >
              <option value="">
                {loadingTrainers ? "Loading trainers..." : "Select a Trainer"}
              </option>
              {trainers.map((t) => (
                <option key={t.id || t.trainerProfileId} value={t.id || t.trainerProfileId}>
                  {t.name || t.fullName} {t.specialty ? `(${t.specialty})` : ""}
                </option>
              ))}
            </select>
          </div>
          {errors.trainerProfileId && (
            <span className="wizard-error-text">{errors.trainerProfileId}</span>
          )}
        </div>

        <div className="wizard-form-group">
          <label className="wizard-label">
            Skill Level <span className="req">*</span>
          </label>
          <select
            className="wizard-select"
            value={form.level}
            onChange={(e) => onChange("level", e.target.value)}
          >
            {LEVEL_PRESETS.map((lvl) => (
              <option key={lvl} value={lvl}>
                {lvl}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Dance Style Pills */}
      <div className="wizard-form-group">
        <label className="wizard-label">
          Dance Style <span className="req">*</span>
        </label>
        <div className="wizard-style-pills">
          {DANCE_STYLE_PRESETS.map((style) => (
            <button
              type="button"
              key={style}
              className={`style-pill-btn ${form.danceStyle === style ? "selected" : ""}`}
              onClick={() => handleStyleSelect(style)}
            >
              {style}
            </button>
          ))}
        </div>

        {isCustomStyle && (
          <div className="wizard-custom-style-wrap">
            <input
              type="text"
              className={`wizard-input ${errors.customStyle ? "has-error" : ""}`}
              placeholder="Enter custom dance style (e.g. Kathak-Contemporary Fusion)"
              value={form.customStyle}
              onChange={(e) => onChange("customStyle", e.target.value)}
            />
            {errors.customStyle && (
              <span className="wizard-error-text">{errors.customStyle}</span>
            )}
          </div>
        )}
      </div>

      {/* City & Area / Zone */}
      <div className="wizard-form-grid-2">
        <div className="wizard-form-group">
          <label className="wizard-label">
            City <span className="req">*</span>
          </label>
          <div className="wizard-input-wrap">
            <MapPin size={16} className="wizard-input-icon" />
            <input
              type="text"
              className={`wizard-input ${errors.city ? "has-error" : ""}`}
              placeholder="e.g. Hyderabad"
              value={form.city}
              onChange={(e) => onChange("city", e.target.value)}
            />
          </div>
          {errors.city && <span className="wizard-error-text">{errors.city}</span>}
        </div>

        <div className="wizard-form-group">
          <label className="wizard-label">Area / Zone</label>
          <input
            type="text"
            className="wizard-input"
            placeholder="e.g. Jubilee Hills, Banjara Hills"
            value={form.area}
            onChange={(e) => onChange("area", e.target.value)}
          />
        </div>
      </div>

      {/* Short Description */}
      <div className="wizard-form-group">
        <div className="wizard-label-row">
          <label className="wizard-label">
            Short Tagline / Teaser <span className="req">*</span>
          </label>
          <span className={`wizard-char-counter ${form.shortDescription?.length > 160 ? "counter-over" : ""}`}>
            {form.shortDescription?.length || 0} / 160
          </span>
        </div>
        <input
          type="text"
          className={`wizard-input ${errors.shortDescription ? "has-error" : ""}`}
          placeholder="High-energy 90-minute choreography workshop focusing on musicality and footwork."
          value={form.shortDescription}
          onChange={(e) => onChange("shortDescription", e.target.value)}
          maxLength={160}
        />
        {errors.shortDescription && (
          <span className="wizard-error-text">{errors.shortDescription}</span>
        )}
      </div>

      {/* Full Description */}
      <div className="wizard-form-group">
        <label className="wizard-label">Full Workshop Description</label>
        <textarea
          className="wizard-textarea"
          rows={4}
          placeholder="Provide a detailed breakdown of what students will learn, who should attend, prerequisites, and what to bring..."
          value={form.description}
          onChange={(e) => onChange("description", e.target.value)}
        />
      </div>

      {/* Contact Person & Number */}
      <div className="wizard-form-grid-2">
        <div className="wizard-form-group">
          <label className="wizard-label">Contact Person / Coordinator</label>
          <input
            type="text"
            className="wizard-input"
            placeholder="e.g. Studio Desk / Admin"
            value={form.contactPerson}
            onChange={(e) => onChange("contactPerson", e.target.value)}
          />
        </div>

        <div className="wizard-form-group">
          <label className="wizard-label">Contact Number (WhatsApp/Call)</label>
          <div className="wizard-input-wrap">
            <Phone size={16} className="wizard-input-icon" />
            <input
              type="tel"
              className={`wizard-input ${errors.contactNumber ? "has-error" : ""}`}
              placeholder="e.g. 9876543210"
              value={form.contactNumber}
              onChange={(e) => onChange("contactNumber", e.target.value)}
              maxLength={15}
            />
          </div>
          {errors.contactNumber && (
            <span className="wizard-error-text">{errors.contactNumber}</span>
          )}
        </div>
      </div>

      {/* Registration Type & Visibility */}
      <div className="wizard-form-grid-2">
        <div className="wizard-form-group">
          <label className="wizard-label">Registration Type</label>
          <select
            className="wizard-select"
            value={form.registrationType}
            onChange={(e) => onChange("registrationType", e.target.value)}
          >
            <option value="Standard">Standard (Open Public Registration)</option>
            <option value="Exclusive">Exclusive (Invite / Private Member)</option>
            <option value="Audition">Audition (Selection Required)</option>
          </select>
        </div>

        <div className="wizard-form-group">
          <label className="wizard-label">Public Website Visibility</label>
          <div className="wizard-toggle-card">
            <div className="toggle-info">
              <span className="toggle-title">
                {form.publicVisibility ? "Visible on Public Website" : "Hidden from Public"}
              </span>
              <span className="toggle-subtitle">
                {form.publicVisibility
                  ? "Appears in /workshops catalogue and search engines."
                  : "Accessible only via direct admin link."}
              </span>
            </div>
            <label className="switch-toggle">
              <input
                type="checkbox"
                checked={form.publicVisibility}
                onChange={(e) => onChange("publicVisibility", e.target.checked)}
              />
              <span className="slider-toggle round"></span>
            </label>
          </div>
        </div>
      </div>

      {/* Terms & Cancellation Policy */}
      <div className="wizard-form-group">
        <div className="wizard-label-row">
          <label className="wizard-label">Terms & Cancellation Policy</label>
          <button
            type="button"
            className="wizard-link-btn"
            onClick={handleApplyPolicyTemplate}
          >
            <FileText size={14} />
            <span>Insert Standard Studio Policy</span>
          </button>
        </div>
        <textarea
          className="wizard-textarea"
          rows={4}
          placeholder="Specify workshop rules, cancellation timeframes, dress code, and studio policies..."
          value={form.termsAndCancellationPolicy}
          onChange={(e) => onChange("termsAndCancellationPolicy", e.target.value)}
        />
      </div>
    </div>
  );
}
