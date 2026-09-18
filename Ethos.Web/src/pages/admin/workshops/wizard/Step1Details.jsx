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
  ChevronDown,
  X,
} from "lucide-react";

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

  // Selected trainer object
  const selectedTrainer = trainers.find(
    (t) => (t.trainerId || t.id || t.trainerProfileId) === form.trainerProfileId
  );

  const handleSelectTrainer = (t) => {
    if (!isTrainerEligible(t)) return;
    const id = t.trainerId || t.id || t.trainerProfileId;
    onChange("trainerProfileId", id);
    setTrainerDropdownOpen(false);
    setTrainerSearch("");
  };

  const handleClearTrainer = (e) => {
    e.stopPropagation();
    onChange("trainerProfileId", "");
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
          Enter core information about your workshop, lead instructor, dance style, and public visibility.
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
        {/* Searchable Lead Trainer Selector */}
        <div className="wizard-form-group" ref={dropdownRef}>
          <div className="wizard-label-row">
            <label className="wizard-label">
              Lead Trainer / Choreographer <span className="req" aria-hidden="true">*</span>
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
            {/* Display box (Click to toggle dropdown) */}
            <div
              className={`trainer-select-display ${trainerDropdownOpen ? "open" : ""} ${errors.trainerProfileId ? "has-error" : ""}`}
              onClick={() => setTrainerDropdownOpen(!trainerDropdownOpen)}
            >
              {selectedTrainer ? (
                <div className="selected-trainer-info">
                  {selectedTrainer.profilePhotoUrl ? (
                    <img
                      src={selectedTrainer.profilePhotoUrl}
                      alt={selectedTrainer.fullName}
                      className="trainer-avatar-img"
                    />
                  ) : (
                    <div className="trainer-avatar-placeholder">
                      {(selectedTrainer.fullName || selectedTrainer.name || "T")[0]}
                    </div>
                  )}
                  <div className="trainer-text-group">
                    <span className="trainer-display-name">
                      {selectedTrainer.fullName || selectedTrainer.name}
                    </span>
                    <span className="trainer-display-style">
                      Lead Trainer {selectedTrainer.primaryDanceStyle ? `• ${selectedTrainer.primaryDanceStyle}` : ""}
                    </span>
                  </div>
                  <div className="trainer-actions-group">
                    <button
                      type="button"
                      className="trainer-change-link-btn"
                      onClick={(e) => {
                        e.stopPropagation();
                        setTrainerDropdownOpen(true);
                      }}
                    >
                      Change
                    </button>
                    <button
                      type="button"
                      className="trainer-clear-btn"
                      onClick={handleClearTrainer}
                      title="Remove trainer"
                    >
                      <X size={14} />
                    </button>
                  </div>
                </div>
              ) : (
                <div className="trainer-placeholder-text">
                  <User size={16} className="trainer-placeholder-icon" />
                  <span>
                    {loadingTrainers ? "Loading active trainers..." : "Search & Select an Active Trainer"}
                  </span>
                  <ChevronDown size={16} className="trainer-chevron" />
                </div>
              )}
            </div>

            {/* Dropdown Menu */}
            {trainerDropdownOpen && (
              <div className="trainer-search-dropdown">
                {/* Search Input Box */}
                <div className="trainer-search-box">
                  <Search size={14} className="search-box-icon" />
                  <input
                    type="text"
                    className="trainer-search-input"
                    placeholder="Search by name, phone, or style..."
                    value={trainerSearch}
                    onChange={(e) => setTrainerSearch(e.target.value)}
                    autoFocus
                    onClick={(e) => e.stopPropagation()}
                  />
                  {trainerSearch && (
                    <button
                      type="button"
                      className="clear-search-mini"
                      onClick={() => setTrainerSearch("")}
                    >
                      ✕
                    </button>
                  )}
                </div>

                {/* Trainer Items List */}
                <div className="trainer-items-list">
                  {filteredTrainers.length === 0 ? (
                    <div className="trainer-no-results">
                      <p>No active trainers found.</p>
                      <Link
                        to="/admin_portal/trainers"
                        target="_blank"
                        className="trainer-add-btn-link"
                      >
                        + Add or approve a trainer first
                      </Link>
                    </div>
                  ) : (
                    filteredTrainers.map((t) => {
                      const id = t.trainerId || t.id || t.trainerProfileId;
                      const eligible = isTrainerEligible(t);
                      const isSelected = form.trainerProfileId === id;

                      return (
                        <div
                          key={id}
                          className={`trainer-dropdown-item ${eligible ? "eligible" : "ineligible"} ${isSelected ? "selected" : ""}`}
                          onClick={() => eligible && handleSelectTrainer(t)}
                        >
                          <div className="trainer-item-left">
                            {t.profilePhotoUrl ? (
                              <img src={t.profilePhotoUrl} alt="" className="trainer-item-img" />
                            ) : (
                              <div className="trainer-item-placeholder">
                                {(t.fullName || t.name || "T")[0]}
                              </div>
                            )}
                            <div className="trainer-item-details">
                              <div className="trainer-item-name-row">
                                <span className="trainer-item-name">{t.fullName || t.name}</span>
                                {eligible ? (
                                  <span className="trainer-badge-active">Active</span>
                                ) : (
                                  <span className="trainer-badge-inactive">
                                    {t.status || "Unavailable"}
                                  </span>
                                )}
                              </div>
                              <span className="trainer-item-meta">
                                {t.primaryDanceStyle || t.specialty || "Instructor"}
                                {t.phone ? ` • ${t.phone}` : ""}
                              </span>
                            </div>
                          </div>

                          {isSelected && <Check size={16} className="trainer-check-icon" />}
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
