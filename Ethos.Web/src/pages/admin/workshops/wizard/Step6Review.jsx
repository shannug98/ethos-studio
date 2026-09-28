import React, { useState } from "react";
import {
  Check,
  AlertCircle,
  Calendar,
  Clock,
  MapPin,
  Megaphone,
  CheckCircle2,
  FileText,
  Edit3,
  ExternalLink,
  Rocket,
  ArrowLeft,
  Layers,
  ChevronRight,
  Ticket,
} from "lucide-react";
import { getTrainerPhotoUrl, handleTrainerImgError, getTrainerDisplayName, DEFAULT_AVATAR_PLACEHOLDER } from "../../../../utils/mediaUrl";
import TrainerAvatar from "../../../../components/common/TrainerAvatar";
import { getChronologicalGroupedSessions, getWorkshopTimingDisplay } from "../../../../utils/workshopPresentation";
import { getPassScopeLabel } from "./pricingUxHelpers";
import "./Step6Review.css";

export default function Step6Review({
  form,
  trainers = [],
  isEdit,
  saving,
  onSaveDraft,
  onSubmitApproval,
  onPublishNow,
  validationChecklist = {},
  onGoToStep,
}) {
  const [modalPreviewOpen, setModalPreviewOpen] = useState(false);

  const assignedTrainerIds = Array.isArray(form.trainerProfileIds) && form.trainerProfileIds.length > 0
    ? form.trainerProfileIds
    : (form.trainerProfileId ? [form.trainerProfileId] : []);

  const selectedTrainers = assignedTrainerIds
    .map((id) => trainers.find((t) => (t.id || t.trainerProfileId || t.trainerId) === id))
    .filter(Boolean);

  const selectedTrainer = selectedTrainers[0] || trainers.find(
    (t) => (t.id || t.trainerProfileId) === form.trainerProfileId
  );
  const trainerName = getTrainerDisplayName(selectedTrainer);
  const trainerStyles = selectedTrainer?.primaryDanceStyle || selectedTrainer?.danceStyles || "";
  const trainerPhoto = selectedTrainer ? getTrainerPhotoUrl(selectedTrainer) : "";

  // Multi-session authoritative schedule derivation for eligibility checks
  const sortedSessions = (Array.isArray(form.sessions) && form.sessions.length > 0)
    ? [...form.sessions].filter((s) => s.sessionDate).sort((a, b) => {
        const dDiff = (a.sessionDate || "").localeCompare(b.sessionDate || "");
        if (dDiff !== 0) return dDiff;
        return (a.endTime || a.startTime || "").localeCompare(b.endTime || b.startTime || "");
      })
    : [];

  const lastSession = sortedSessions[sortedSessions.length - 1];
  const finalDate = lastSession?.sessionDate || form.workshopDate;
  const finalEndTime = lastSession?.endTime || form.endTime || "23:59";

  const isPast = finalDate && new Date(`${finalDate}T${finalEndTime.slice(0, 5)}:00`) < new Date();
  const isPrivate = !form.publicVisibility;

  const checklistItems = [
    {
      id: "details",
      step: 1,
      label: "Workshop details & faculty complete",
      isValid: Boolean(validationChecklist.details),
    },
    {
      id: "portrait",
      step: 2,
      label: "3:4 Portrait cover image uploaded",
      isValid: Boolean(validationChecklist.portrait),
    },
    {
      id: "venue",
      step: 3,
      label: "Venue & session schedule configured",
      isValid: Boolean(validationChecklist.venue),
    },
    {
      id: "ticketTypes",
      step: 4,
      label: "Ticket types created (min 1 ticket type)",
      isValid: Boolean(validationChecklist.ticketTypes),
    },
    {
      id: "pricing",
      step: 5,
      label: "Volume pricing configured",
      isValid: Boolean(validationChecklist.pricing),
    },
    {
      id: "schedule",
      step: 3,
      label: "Schedule is set in the future",
      isValid: !isPast,
    },
    {
      id: "visibility",
      step: 1,
      label: "Public website visibility enabled",
      isValid: !isPrivate,
    },
  ];

  const totalReqs = checklistItems.length;
  const metReqs = checklistItems.filter((item) => item.isValid).length;
  const allEligible = metReqs === totalReqs;

  const passTypes = Array.isArray(form.passTypes) ? form.passTypes : [];
  const minStartingPrice = passTypes.length > 0
    ? Math.min(...passTypes.map((p) => {
        if (Array.isArray(p.pricingTiers) && p.pricingTiers.length > 0 && p.pricingTiers[0].price != null) {
          return Number(p.pricingTiers[0].price);
        }
        return Number(p.price) || 0;
      }).filter((pr) => pr > 0))
    : form.price || 500;

  // Formatting date for summary
  const formatDisplayDate = (dStr) => {
    if (!dStr) return "Not set";
    try {
      const d = new Date(dStr);
      return d.toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric" });
    } catch {
      return dStr;
    }
  };

  const formatShortDate = (dStr) => {
    if (!dStr) return "Not set";
    try {
      const d = new Date(dStr);
      return d.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
    } catch {
      return dStr;
    }
  };

  const formatCardDate = (dStr) => {
    if (!dStr) return "TBD";
    try {
      const d = new Date(dStr);
      return d.toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short", year: "numeric" });
    } catch {
      return dStr;
    }
  };

  return (
    <div className="step6-review-container">
      {/* TOP HEADER & NOTIFICATION */}
      <div className="step6-header-row">
        <div className="step6-header-left">
          <div className="step6-header-icon">
            <Megaphone size={24} />
          </div>
          <div>
            <h2 className="step6-header-title">Step 6 — Review & Publish Workshop</h2>
            <p className="step6-header-desc">
              Review the public card presentation, ticket configurations, pricing tiers, and pre-publish eligibility checklist before publishing live on Ethos.
            </p>
          </div>
        </div>

        {/* Dynamic Status Callout Badge */}
        <div className={`step6-status-card ${allEligible ? "is-ready" : "is-attention"}`}>
          {allEligible ? (
            <CheckCircle2 size={24} color="#2563eb" style={{ flexShrink: 0 }} />
          ) : (
            <AlertCircle size={24} color="#dc2626" style={{ flexShrink: 0 }} />
          )}
          <div>
            <div className="step6-status-title">
              {allEligible ? "Everything looks good!" : "Action required before publishing"}
            </div>
            <div className="step6-status-desc">
              {allEligible
                ? "You're almost ready to publish."
                : "Click on any red item below to jump directly and resolve it."}
            </div>
          </div>
        </div>
      </div>

      {/* 2-COLUMN MAIN LAYOUT */}
      <div className="step6-main-grid">
        {/* LEFT COLUMN: PUBLIC CARD PREVIEW */}
        <div className="step6-preview-col">
          <div className="step6-section-label">
            <FileText size={17} color="#475569" />
            <span>Public Card Preview</span>
          </div>

          <div className="step6-public-card">
            {/* 3:4 Portrait Cover Image with Badges */}
            <div className="step6-card-media">
              {form.imageUrl ? (
                <img src={form.imageUrl} alt={form.title} className="step6-card-cover-img" />
              ) : (
                <div style={{ width: "100%", height: "100%", display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", background: "#f8fafc", color: "#94a3b8" }}>
                  <Calendar size={36} />
                  <span style={{ fontSize: "12px", marginTop: "6px" }}>3:4 Cover Image</span>
                </div>
              )}

              {/* Category & Level Badges */}
              <div className="step6-card-badges-top">
                <span className="step6-pill-tag style">
                  {form.danceStyle === "Other" ? form.customStyle || "Custom" : form.danceStyle || "Urban Choreography"}
                </span>
                <span className="step6-pill-tag level">
                  {form.level || "Open Level"}
                </span>
              </div>

              {/* Price Tag Overlay */}
              <div className="step6-card-price-overlay">
                <span className="step6-price-sub">Starts at</span>
                <span className="step6-price-main">₹{minStartingPrice}</span>
              </div>
            </div>

            {/* Public Card Body */}
            <div className="step6-card-body">
              <h3 className="step6-card-title">{form.title || "Untitled Workshop"}</h3>

              {/* Trainer Chip */}
              <div className="step6-trainer-line">
                <TrainerAvatar
                  trainer={selectedTrainer || trainerPhoto}
                  name={trainerName}
                  size={22}
                  bordered
                  borderColor="#FF5500"
                />
                <span className="step6-trainer-name">{trainerName}</span>
                <span style={{ color: "#94a3b8" }}>•</span>
                <span>{trainerStyles || "Urban Choreography"}</span>
              </div>

              {/* Meta items */}
              <div className="step6-meta-list">
                <div className="step6-meta-item">
                  <Calendar size={14} />
                  <span>{formatCardDate(form.workshopDate)}</span>
                </div>
                <div className="step6-meta-item">
                  <MapPin size={14} />
                  <span>{form.venue ? `${form.venue}${form.city ? `, ${form.city}` : ""}` : "Ethos Studio, Hyderabad"}</span>
                </div>
                <div className="step6-meta-item">
                  <Layers size={14} />
                  <span>
                    {(() => {
                      const timingText = getWorkshopTimingDisplay(form.sessions, form.startTime, form.endTime);
                      if (!timingText) return "Multiple Sessions — See Schedule";
                      return timingText;
                    })()}
                  </span>
                </div>

                {form.locationUrl && (
                  <a
                    href={form.locationUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="step6-map-link"
                  >
                    <span>View Location on Map</span>
                    <ExternalLink size={12} />
                  </a>
                )}
              </div>

              {/* Description Snippet */}
              {form.description && (
                <p className="step6-card-desc-snippet">
                  {form.description}
                </p>
              )}
            </div>
          </div>
        </div>

        {/* RIGHT COLUMN: SPECS, CRITERIA & CONFIGURATIONS */}
        <div className="step6-details-col">
          {/* ROW 1: ELIGIBILITY CRITERIA & WORKSHOP SUMMARY */}
          <div className="step6-row-2col">
            {/* 1. Eligibility Criteria Card */}
            <div className="step6-white-card">
              <div className="step6-card-header">
                <div className="step6-card-header-left">
                  <CheckCircle2 size={18} color="#10b981" />
                  <span>Eligibility Criteria</span>
                </div>
                <span className={`step6-badge-met ${!allEligible ? "has-errors" : ""}`}>
                  {metReqs} of {totalReqs} requirements met
                </span>
              </div>

              <div className="step6-checklist">
                {checklistItems.map((item) => (
                  <div
                    key={item.id}
                    className={`step6-checklist-item ${item.isValid ? "is-valid" : "is-invalid"}`}
                    onClick={() => onGoToStep?.(item.step)}
                    title={`Click to go to Step ${item.step}`}
                  >
                    <div className="step6-checklist-item-left">
                      <span className={`step6-check-icon-circle ${item.isValid ? "valid" : "invalid"}`}>
                        {item.isValid ? <Check size={11} strokeWidth={3} /> : <AlertCircle size={11} strokeWidth={3} />}
                      </span>
                      <span>{item.label}</span>
                    </div>

                    {!item.isValid && (
                      <span className="step6-fix-hint">
                        Fix in Step {item.step} <ChevronRight size={12} />
                      </span>
                    )}
                  </div>
                ))}
              </div>
            </div>

            {/* 2. Workshop Summary Card */}
            <div className="step6-white-card">
              <div className="step6-card-header">
                <div className="step6-card-header-left">
                  <FileText size={18} color="#2563eb" />
                  <span>Workshop Summary</span>
                </div>
                <button
                  type="button"
                  className="step6-edit-btn"
                  onClick={() => onGoToStep?.(1)}
                >
                  <Edit3 size={13} /> Edit Details
                </button>
              </div>

              <div className="step6-summary-table">
                <div className="step6-summary-row">
                  <span className="step6-summary-key">Workshop Name</span>
                  <span className="step6-summary-val">{form.title || "MAD Songs Workshop"}</span>
                </div>
                <div className="step6-summary-row">
                  <span className="step6-summary-key">Style</span>
                  <span className="step6-summary-val">{form.danceStyle === "Other" ? form.customStyle || "Custom" : form.danceStyle || "Urban Choreography"}</span>
                </div>
                <div className="step6-summary-row">
                  <span className="step6-summary-key">Level</span>
                  <span className="step6-summary-val">
                    <span className="step6-level-pill">{form.level || "Advanced"}</span>
                  </span>
                </div>
                <div className="step6-summary-row">
                  <span className="step6-summary-key">Faculty</span>
                  <span className="step6-summary-val" style={{ display: "inline-flex", alignItems: "center", gap: "6px" }}>
                    <TrainerAvatar
                      trainer={selectedTrainer || trainerPhoto}
                      name={trainerName}
                      size={18}
                    />
                    {trainerName}
                  </span>
                </div>
                <div className="step6-summary-row">
                  <span className="step6-summary-key">Workshop Date</span>
                  <span className="step6-summary-val">{formatDisplayDate(form.workshopDate)}</span>
                </div>
                <div className="step6-summary-row">
                  <span className="step6-summary-key">Sessions</span>
                  <span className="step6-summary-val">{form.sessions?.length || 1} Sessions</span>
                </div>
                <div className="step6-summary-row">
                  <span className="step6-summary-key">Venue</span>
                  <span className="step6-summary-val" style={{ maxWidth: "200px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {form.venue || "Sri Bhramaramba Cinema Hall, Hyderabad"}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* ROW 2: SCHEDULE & INSTRUCTORS + VENUE INFORMATION */}
          <div className="step6-row-2col">
            {/* 3. Schedule & Instructors Card */}
            <div className="step6-white-card">
              <div className="step6-card-header">
                <div className="step6-card-header-left">
                  <Calendar size={18} color="#2563eb" />
                  <span>Schedule & Instructors</span>
                </div>
                <button
                  type="button"
                  className="step6-edit-btn"
                  onClick={() => onGoToStep?.(3)}
                >
                  <Edit3 size={13} /> Edit Schedule
                </button>
              </div>

              <div className="step6-sessions-list">
                {(form.sessions && form.sessions.length > 0 ? form.sessions : [
                  { id: "s1", title: "Session 1", sessionDate: form.workshopDate, startTime: "17:30", endTime: "19:00", trainerName: trainerName }
                ]).map((sess, idx) => {
                  const sTrainer = trainers.find((t) => (t.id || t.trainerProfileId) === sess.trainerProfileId);
                  const sTrainerName = sess.trainerName || (sTrainer ? getTrainerDisplayName(sTrainer) : trainerName);
                  const sPhoto = sTrainer ? getTrainerPhotoUrl(sTrainer) : trainerPhoto;

                  return (
                    <div key={sess.id || idx} className="step6-session-row">
                      <span className="step6-session-name">{sess.title || `Session ${idx + 1}`}</span>
                      <span className="step6-session-date">{formatShortDate(sess.sessionDate || form.workshopDate)}</span>
                      <span className="step6-session-time">
                        {sess.startTime ? sess.startTime.slice(0, 5) : "17:30"} – {sess.endTime ? sess.endTime.slice(0, 5) : "19:00"}
                      </span>
                      <div className="step6-session-trainer">
                        <TrainerAvatar
                          trainer={sTrainer || sPhoto}
                          name={sTrainerName}
                          size={18}
                        />
                        <span>{sTrainerName}</span>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* 4. Venue Information Card */}
            <div className="step6-white-card">
              <div className="step6-card-header">
                <div className="step6-card-header-left">
                  <MapPin size={18} color="#2563eb" />
                  <span>Venue Information</span>
                </div>
                <button
                  type="button"
                  className="step6-edit-btn"
                  onClick={() => onGoToStep?.(3)}
                >
                  <Edit3 size={13} /> Edit Venue
                </button>
              </div>

              <div className="step6-venue-details">
                <div className="step6-venue-name">
                  {form.venue || "Sri Bhramaramba Cinema Hall"}
                </div>
                <div className="step6-venue-address">
                  {form.venueAddress || form.venue || "Sri Bhramaramba Cinema Hall, Plot No. 11 2, SECTOR 1, behind Pride Honda, HUDA Techno Enclave, Madhapur, Hyderabad, Telangana 500081"}
                </div>

                {/* Stylized Map Box */}
                <div className="step6-map-preview-box">
                  <div className="step6-map-pin-label">
                    <MapPin size={16} color="#ef4444" fill="#ef4444" />
                    <span>{form.venue || "Sri Bhramaramba Cinema Hall"}</span>
                  </div>

                  {form.locationUrl && (
                    <a
                      href={form.locationUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="step6-map-btn"
                    >
                      <span>Open in Maps</span>
                      <ExternalLink size={12} />
                    </a>
                  )}
                </div>
              </div>
            </div>
          </div>

          {/* ROW 3: FULL WIDTH TICKET TYPES & PRICING TABLE */}
          <div className="step6-white-card">
            <div className="step6-card-header">
              <div className="step6-card-header-left">
                <Ticket size={18} color="#ff5500" />
                <span>Ticket Types & Pricing</span>
              </div>
              <button
                type="button"
                className="step6-edit-btn"
                onClick={() => onGoToStep?.(4)}
              >
                <Edit3 size={13} /> Edit Tickets
              </button>
            </div>

            <div className="step6-tickets-table-wrap">
              <table className="step6-tickets-table">
                <thead>
                  <tr>
                    <th style={{ width: "40px" }}>#</th>
                    <th>Ticket Name</th>
                    <th>Type / Entitlement</th>
                    <th>Price Tiers</th>
                    <th style={{ textAlign: "right" }}>Available Quantity</th>
                  </tr>
                </thead>
                <tbody>
                  {(passTypes.length > 0 ? passTypes : [
                    { id: "p1", name: "General Admission", isOverallPass: true, totalQuantity: 50, price: 500 },
                    { id: "p2", name: "Ticket Type 2", sessionsIncluded: 1, totalQuantity: 35, price: 500 },
                    { id: "p3", name: "Ticket Type 3", sessionsIncluded: 2, totalQuantity: 35, price: 500 },
                  ]).map((pass, pIdx) => {
                    const tiers = Array.isArray(pass.pricingTiers) && pass.pricingTiers.length > 0
                      ? pass.pricingTiers
                      : [{ minTickets: 1, maxTickets: null, price: pass.price || 500 }];

                    return (
                      <tr key={pass.id || pIdx}>
                        <td style={{ color: "#64748b", fontWeight: 600 }}>{pIdx + 1}</td>
                        <td style={{ fontWeight: 700, color: "#0f172a" }}>{pass.name}</td>
                        <td>
                          <span className={`step6-ticket-badge-entitlement ${pass.isOverallPass || pass.sessionsIncluded == null ? 'all-access' : 'bundle'}`}>
                            {getPassScopeLabel(pass.sessionsIncluded)}
                          </span>
                        </td>
                        <td>
                          <div className="step6-price-tiers-chips">
                            {tiers.map((t, tIdx) => (
                              <span key={tIdx} className="step6-tier-chip">
                                <strong>{t.minTickets}{t.maxTickets ? `–${t.maxTickets}` : "+"}:</strong> ₹{t.price}
                              </span>
                            ))}
                          </div>
                        </td>
                        <td style={{ textAlign: "right", fontWeight: 600, color: "#475569" }}>
                          {pass.totalQuantity ? `${pass.totalQuantity} tickets` : "Unlimited"}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      {/* BOTTOM ACTION BUTTONS ROW */}
      <div className="step6-actions-bar">
        <button
          type="button"
          className="step6-prev-btn"
          onClick={() => onGoToStep?.(5)}
          disabled={saving}
        >
          <ArrowLeft size={16} style={{ display: "inline", verticalAlign: "middle", marginRight: "6px" }} />
          Previous
        </button>

        <div className="step6-right-actions">
          <button
            type="button"
            className="step6-draft-btn"
            disabled={saving}
            onClick={onSaveDraft}
          >
            {saving ? "Saving..." : isEdit ? "Save Changes" : "Save as Draft"}
          </button>

          <button
            type="button"
            className="step6-approval-btn"
            disabled={saving || !allEligible}
            onClick={onSubmitApproval}
            title={!allEligible ? "Complete all required checklist items first" : ""}
          >
            {saving ? "Submitting..." : "Submit for Approval"}
          </button>

          <button
            type="button"
            className="step6-publish-btn"
            disabled={saving || !allEligible}
            onClick={onPublishNow}
            title={!allEligible ? "Complete all required checklist items first" : ""}
          >
            <Rocket size={16} />
            <span>{saving ? "Publishing..." : isEdit ? "Update & Publish" : "Publish Workshop"}</span>
          </button>
        </div>
      </div>
    </div>
  );
}
