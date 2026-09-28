import React, { useState, useEffect, useRef } from "react";
import { Link } from "react-router-dom";
import {
  User,
  MapPin,
  Phone,
  FileText,
  Search,
  ExternalLink,
  Check,
  AlertCircle,
  AlertTriangle,
  ChevronDown,
  X,
  Plus,
} from "lucide-react";
import {
  getTrainerPhotoUrl,
  handleTrainerImgError,
  getTrainerDisplayName,
  DEFAULT_AVATAR_PLACEHOLDER,
  ETHOS_DEFAULT_TRAINER_AVATAR,
} from "../../../../utils/mediaUrl";
import TrainerAvatar from "../../../../components/common/TrainerAvatar";

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

  // Searchable Trainer Dropdown State
  const [trainerDropdownOpen, setTrainerDropdownOpen] = useState(false);
  const [trainerSearch, setTrainerSearch] = useState("");
  const [removalWarning, setRemovalWarning] = useState(null);
  const dropdownRef = useRef(null);

  // Close dropdown on outside click
  useEffect(() => {
    function handleClickOutside(e) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setTrainerDropdownOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Filter trainers based on search query
  const filteredTrainers = trainers.filter((t) => {
    const name = (t.fullName || t.name || "").toLowerCase();
    const phone = (t.phone || "").toLowerCase();
    const style = (t.primaryDanceStyle || t.danceStyle || t.specialty || "").toLowerCase();
    const q = trainerSearch.toLowerCase();
    return name.includes(q) || phone.includes(q) || style.includes(q);
  });

  // Check if a trainer is eligible (Active and Approved)
  const isTrainerEligible = (t) => {
    const status = (t.status || "").toLowerCase();
    return status === "active" || status === "approved" || !t.status;
  };

  // Selected trainer IDs list (supports arbitrary number of trainers 1, 2, 3, 4, 5+)
  const selectedTrainerIds = Array.isArray(form.trainerProfileIds) && form.trainerProfileIds.length > 0
    ? form.trainerProfileIds
    : (form.trainerProfileId ? [form.trainerProfileId] : []);

  const selectedTrainers = selectedTrainerIds
    .map((id) => trainers.find((t) => (t.trainerId || t.id || t.trainerProfileId) === id))
    .filter(Boolean);

  const checkAssignedSessions = (id) => {
    return (form.sessions || []).filter(
      (s) => s.trainerProfileId === id || (Array.isArray(s.trainerProfileIds) && s.trainerProfileIds.includes(id))
    );
  };

  const handleToggleTrainer = (t) => {
    if (!isTrainerEligible(t)) return;
    const id = t.trainerId || t.id || t.trainerProfileId;
    let updated;
    if (selectedTrainerIds.includes(id)) {
      const assigned = checkAssignedSessions(id);
      if (assigned.length > 0) {
        setRemovalWarning({
          trainerName: getTrainerDisplayName(t),
          sessionNames: assigned.map((s) => `"${s.title || "Session"}"`).join(", "),
        });
        return;
      }
      updated = selectedTrainerIds.filter((x) => x !== id);
    } else {
      updated = [...selectedTrainerIds, id];
    }
    onChange("trainerProfileIds", updated);
    onChange("trainerProfileId", updated[0] || "");
  };

  const handleMakeLead = (id, e) => {
    e.stopPropagation();
    const updated = [id, ...selectedTrainerIds.filter((x) => x !== id)];
    onChange("trainerProfileIds", updated);
    onChange("trainerProfileId", id);
  };

  const handleRemoveTrainer = (id, e) => {
    e.stopPropagation();
    const assigned = checkAssignedSessions(id);
    if (assigned.length > 0) {
      const trainerObj = trainers.find((t) => (t.trainerId || t.id || t.trainerProfileId) === id);
      setRemovalWarning({
        trainerName: trainerObj ? getTrainerDisplayName(trainerObj) : "Selected instructor",
        sessionNames: assigned.map((s) => `"${s.title || "Session"}"`).join(", "),
      });
      return;
    }
    const updated = selectedTrainerIds.filter((x) => x !== id);
    onChange("trainerProfileIds", updated);
    onChange("trainerProfileId", updated[0] || "");
  };

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
          Enter core information about your workshop, instructors, dance style, and public visibility.
        </p>
      </div>

      {/* Workshop Title */}
      <div className="wizard-form-group">
        <label className="wizard-label">
          Workshop Title <span className="req" aria-hidden="true">*</span>
        </label>
        <input
          type="text"
          className={`wizard-input ${errors.title ? "has-error" : ""}`}
          placeholder="e.g. Urban Groove Masterclass with Rahul"
          value={form.title}
          onChange={(e) => onChange("title", e.target.value)}
          maxLength={120}
        />
        {errors.title && (
          <div className="wizard-error-text">
            <AlertCircle size={14} />
            <span>{errors.title}</span>
          </div>
        )}
      </div>

      {/* Trainer & Level Row */}
      <div className="wizard-form-grid-2">
        {/* Searchable Multi-Trainer Selector */}
        <div className="wizard-form-group" ref={dropdownRef}>
          <div className="wizard-label-row">
            <label className="wizard-label">
              Workshop Trainers / Faculty ({selectedTrainers.length}) <span className="req" aria-hidden="true">*</span>
            </label>
            <Link
              to="/admin_portal/trainers"
              target="_blank"
              rel="noopener noreferrer"
              className="wizard-manage-link"
              title="Open trainer management in a new tab"
            >
              <span>Manage Trainers</span>
              <ExternalLink size={12} />
            </Link>
          </div>

          <div className="trainer-selector-container">
            {/* Selected Trainers List Cards */}
            {selectedTrainers.length > 0 && (
              <div className="selected-trainers-cards-list">
                {selectedTrainers.map((t, idx) => {
                  const id = t.trainerId || t.id || t.trainerProfileId;
                  const displayName = getTrainerDisplayName(t);
                  return (
                    <div
                      key={id}
                      className="selected-trainer-card faculty-card"
                    >
                      <div className="selected-trainer-left">
                        <TrainerAvatar
                          trainer={t}
                          size="md"
                        />
                        <div className="selected-trainer-info-col">
                          <div className="selected-trainer-header-row">
                            <span className="selected-trainer-name">{displayName}</span>
                            <span className="selected-trainer-role-badge faculty-badge">
                              Faculty
                            </span>
                          </div>
                          <span className="selected-trainer-sub">
                            {t.primaryDanceStyle || t.danceStyle || t.specialty || "Instructor"}
                            {t.phone ? ` • ${t.phone}` : ""}
                          </span>
                        </div>
                      </div>

                      <div className="selected-trainer-actions">
                        <button
                          type="button"
                          onClick={(e) => handleRemoveTrainer(id, e)}
                          className="trainer-remove-card-btn"
                          title={`Remove ${displayName}`}
                          aria-label={`Remove ${displayName}`}
                        >
                          <X size={15} />
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            {/* Trigger Button to Open / Close Trainer Selection Dropdown */}
            <button
              type="button"
              className={`trainer-select-trigger-btn ${trainerDropdownOpen ? "open" : ""} ${errors.trainerProfileId && selectedTrainers.length === 0 ? "has-error" : ""}`}
              onClick={() => setTrainerDropdownOpen(!trainerDropdownOpen)}
              aria-expanded={trainerDropdownOpen}
            >
              <div className="trigger-left">
                <User size={16} className="trigger-icon" />
                <span className="trigger-text">
                  {selectedTrainers.length === 0
                    ? (loadingTrainers ? "Loading active trainers..." : "Search & Select Workshop Trainers (Supports Multiple)")
                    : `+ Add / Change Workshop Faculty (${selectedTrainers.length} Assigned)`}
                </span>
              </div>
              <ChevronDown size={16} className={`trigger-chevron ${trainerDropdownOpen ? "rotated" : ""}`} />
            </button>

            {/* Dropdown Menu */}
            {trainerDropdownOpen && (
              <div className="trainer-search-dropdown" role="listbox">
                {/* Search Input Box */}
                <div className="trainer-search-box">
                  <Search size={15} className="search-box-icon" />
                  <input
                    type="text"
                    className="trainer-search-input"
                    placeholder="Search trainers by name, phone, or dance style..."
                    value={trainerSearch}
                    onChange={(e) => setTrainerSearch(e.target.value)}
                    autoFocus
                    onClick={(e) => e.stopPropagation()}
                    aria-label="Search trainers"
                  />
                  {trainerSearch && (
                    <button
                      type="button"
                      className="clear-search-mini"
                      onClick={() => setTrainerSearch("")}
                      aria-label="Clear search"
                    >
                      ✕
                    </button>
                  )}
                </div>

                {/* Trainer Items List */}
                <div className="trainer-items-list">
                  {filteredTrainers.length === 0 ? (
                    <div className="trainer-no-results">
                      <p>No active trainers found matching "{trainerSearch}".</p>
                      <Link
                        to="/admin_portal/trainers"
                        target="_blank"
                        className="trainer-add-btn-link"
                      >
                        + Add or approve a trainer
                      </Link>
                    </div>
                  ) : (
                    filteredTrainers.map((t) => {
                      const id = t.trainerId || t.id || t.trainerProfileId;
                      const eligible = isTrainerEligible(t);
                      const isSelected = selectedTrainerIds.includes(id);
                      const displayName = getTrainerDisplayName(t);

                      return (
                        <div
                          key={id}
                          className={`trainer-dropdown-item ${eligible ? "eligible" : "ineligible"} ${isSelected ? "selected" : ""}`}
                          onClick={() => eligible && handleToggleTrainer(t)}
                          role="option"
                          aria-selected={isSelected}
                          tabIndex={eligible ? 0 : -1}
                          onKeyDown={(e) => {
                            if ((e.key === "Enter" || e.key === " ") && eligible) {
                              e.preventDefault();
                              handleToggleTrainer(t);
                            }
                          }}
                        >
                          <div className="trainer-item-left">
                            <TrainerAvatar
                              trainer={t}
                              size="sm"
                            />
                            <div className="trainer-item-details">
                              <div className="trainer-item-name-row">
                                <span className="trainer-item-name">{displayName}</span>
                                {eligible ? (
                                  <span className="trainer-badge-active">Active</span>
                                ) : (
                                  <span className="trainer-badge-inactive">
                                    {t.status || "Unavailable"}
                                  </span>
                                )}
                              </div>
                              <span className="trainer-item-meta">
                                {t.primaryDanceStyle || t.danceStyle || t.specialty || "Instructor"}
                                {t.phone ? ` • ${t.phone}` : ""}
                              </span>
                            </div>
                          </div>

                          <div className="trainer-item-right">
                            {isSelected ? (
                              <div className="trainer-selected-indicator">
                                <Check size={15} />
                                <span>Selected</span>
                              </div>
                            ) : eligible ? (
                              <span className="trainer-select-action-hint">+ Select</span>
                            ) : null}
                          </div>
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
            )}
          </div>

          {errors.trainerProfileId && (
            <div className="wizard-error-text">
              <AlertCircle size={14} />
              <span>{errors.trainerProfileId}</span>
            </div>
          )}
        </div>

        {/* Skill Level */}
        <div className="wizard-form-group">
          <label className="wizard-label">
            Skill Level <span className="req" aria-hidden="true">*</span>
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
          Dance Style <span className="req" aria-hidden="true">*</span>
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
              <div className="wizard-error-text">
                <AlertCircle size={14} />
                <span>{errors.customStyle}</span>
              </div>
            )}
          </div>
        )}
      </div>

      {/* City & Area / Zone */}
      <div className="wizard-form-grid-2">
        <div className="wizard-form-group">
          <label className="wizard-label">
            City <span className="req" aria-hidden="true">*</span>
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
          {errors.city && (
            <div className="wizard-error-text">
              <AlertCircle size={14} />
              <span>{errors.city}</span>
            </div>
          )}
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
            Short Tagline / Teaser <span className="req" aria-hidden="true">*</span>
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
          <div className="wizard-error-text">
            <AlertCircle size={14} />
            <span>{errors.shortDescription}</span>
          </div>
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
            <div className="wizard-error-text">
              <AlertCircle size={14} />
              <span>{errors.contactNumber}</span>
            </div>
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

      {/* Ethos Original Toggle */}
      <div style={{ marginBottom: "20px", padding: "16px 20px", background: "#FFFFFF", border: "1px solid #E2E8F0", borderRadius: "10px", boxShadow: "0 1px 2px rgba(0,0,0,0.04)" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "16px" }}>
          <div>
            <div style={{ fontSize: "14px", fontWeight: 750, color: "#172033", marginBottom: "3px" }}>✦ Ethos Original</div>
            <div style={{ fontSize: "12px", color: "#64748B" }}>Mark this as an official Ethos Original production. Shows an OG badge on cards and the detail page.</div>
          </div>
          <label style={{ display: "inline-flex", alignItems: "center", gap: "8px", fontSize: "13px", fontWeight: 650, color: form.isEthosOriginal ? "#FF5500" : "#94A3B8", cursor: "pointer", userSelect: "none", flexShrink: 0 }}>
            <input
              type="checkbox"
              checked={form.isEthosOriginal === true}
              onChange={(e) => onChange("isEthosOriginal", e.target.checked)}
              style={{ accentColor: "#FF5500", width: "18px", height: "18px", cursor: "pointer" }}
            />
            <span>{form.isEthosOriginal ? "ON" : "OFF"}</span>
          </label>
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

      {/* Invariant Warning Modal */}
      {removalWarning && (
        <div className="wizard-modal-overlay" role="dialog" aria-modal="true" aria-labelledby="invariant-modal-title">
          <div className="wizard-modal-card" style={{ maxWidth: "480px" }}>
            <div className="wizard-modal-header" style={{ display: "flex", alignItems: "center", gap: "10px", color: "#b45309" }}>
              <AlertTriangle size={24} color="#d97706" />
              <h3 id="invariant-modal-title" style={{ margin: 0, fontSize: "17px", fontWeight: 700, color: "#1e293b" }}>
                Cannot Remove Trainer
              </h3>
            </div>
            <div className="wizard-modal-body" style={{ margin: "14px 0", fontSize: "14px", lineHeight: "1.5", color: "#475569" }}>
              <p>
                <strong>{removalWarning.trainerName}</strong> cannot be removed from the workshop faculty pool because they are currently assigned to the following session(s):
              </p>
              <div style={{ background: "#fef3c7", border: "1px solid #fde68a", borderRadius: "6px", padding: "10px 14px", margin: "12px 0", color: "#92400e", fontWeight: 600 }}>
                {removalWarning.sessionNames}
              </div>
              <p style={{ margin: 0 }}>
                Please reassign or remove this instructor from those sessions in <em>Step 3 (Venue &amp; Schedule)</em> before removing them from the faculty pool.
              </p>
            </div>
            <div className="wizard-modal-actions" style={{ display: "flex", justifyContent: "flex-end", marginTop: "18px" }}>
              <button
                type="button"
                className="wizard-btn-primary"
                style={{ padding: "8px 20px", fontSize: "14px", fontWeight: 600, background: "#FF5500", color: "#fff", border: "none", borderRadius: "6px", cursor: "pointer" }}
                onClick={() => setRemovalWarning(null)}
              >
                Understood
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
