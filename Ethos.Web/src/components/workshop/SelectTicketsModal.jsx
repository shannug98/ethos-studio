import React, { useState, useEffect, useMemo } from "react";
import { X, Plus, Minus, AlertCircle, Check, Calendar, Clock, User, ShieldCheck } from "lucide-react";
import { workshopsApi } from "../../services/workshopsApi";
import { getTrainerPhotoUrl, handleTrainerImgError, getTrainerDisplayName, DEFAULT_AVATAR_PLACEHOLDER } from "../../utils/mediaUrl";
import TrainerAvatar from "../common/TrainerAvatar";
import { getPassAvailability, getChronologicalGroupedSessions } from "../../utils/workshopPresentation";
import "./SelectTicketsModal.css";

function doTimesOverlap(startA, endA, startB, endB) {
  return startA < endB && startB < endA;
}

export default function SelectTicketsModal({
  isOpen,
  onClose,
  workshop,
  pricing,
  onContinue,
  initialPassTypeId = null,
}) {
  const hasPasses = Array.isArray(workshop?.passTypes) && workshop.passTypes.length > 0;
  const activePassTypes = useMemo(() => {
    if (!hasPasses) return [];
    return [...workshop.passTypes]
      .filter((p) => p.isActive)
      .sort((a, b) => a.displayOrder - b.displayOrder || a.price - b.price);
  }, [hasPasses, workshop?.passTypes]);

  // Pass System State
  const [selectedPassId, setSelectedPassId] = useState(null);
  const [selectedSessionIds, setSelectedSessionIds] = useState([]);
  const [sessionOverlapError, setSessionOverlapError] = useState("");

  // Legacy Tier State
  const [quantity, setQuantity] = useState(1);
  const [quote, setQuote] = useState(null);
  const [loadingQuote, setLoadingQuote] = useState(false);
  const [quoteError, setQuoteError] = useState("");

  // Initialize or reset when modal opens
  useEffect(() => {
    if (isOpen) {
      if (hasPasses && activePassTypes.length > 0) {
        const defaultPass = initialPassTypeId
          ? activePassTypes.find((p) => p.id === initialPassTypeId) || activePassTypes[0]
          : activePassTypes[0];
        setSelectedPassId(defaultPass?.id || null);
        // Initially ZERO sessions selected for Solo, Dual, Trio
        setSelectedSessionIds([]);
        setSessionOverlapError("");
        setQuantity(1);
        setQuote(null);
        setQuoteError("");
      } else {
        setQuantity(1);
        setQuote(null);
        setQuoteError("");
      }
    }
  }, [isOpen, hasPasses, activePassTypes, initialPassTypeId]);

  // Selected Pass Object
  const currentPass = useMemo(() => {
    return activePassTypes.find((p) => p.id === selectedPassId) || null;
  }, [activePassTypes, selectedPassId]);

  // Active Sessions
  const availableSessions = useMemo(() => {
    if (!Array.isArray(workshop?.sessions)) return [];
    return [...workshop.sessions]
      .filter((s) => s.isActive)
      .sort((a, b) => new Date(a.sessionDate) - new Date(b.sessionDate) || a.startTime.localeCompare(b.startTime));
  }, [workshop?.sessions]);

  // Check overlaps whenever selectedSessionIds changes
  useEffect(() => {
    if (!currentPass || currentPass.isOverallPass || !currentPass.sessionsIncluded) {
      setSessionOverlapError("");
      return;
    }

    const selectedSessions = availableSessions.filter((s) => selectedSessionIds.includes(s.id));
    for (let i = 0; i < selectedSessions.length; i++) {
      for (let j = i + 1; j < selectedSessions.length; j++) {
        const s1 = selectedSessions[i];
        const s2 = selectedSessions[j];
        const d1 = s1.sessionDate.split("T")[0];
        const d2 = s2.sessionDate.split("T")[0];
        if (d1 === d2) {
          if (doTimesOverlap(s1.startTime, s1.endTime, s2.startTime, s2.endTime)) {
            setSessionOverlapError(`Conflict: "${s1.title}" and "${s2.title}" overlap in time on ${d1}.`);
            return;
          }
        }
      }
    }
    setSessionOverlapError("");
  }, [selectedSessionIds, currentPass, availableSessions]);

  // Reset selected sessions to ZERO when switching passes
  const handleSelectPass = (pass) => {
    const { isSoldOut } = getPassAvailability(pass);
    if (isSoldOut) return;
    setSelectedPassId(pass.id);
    setSelectedSessionIds([]);
    setSessionOverlapError("");
    setQuantity(1);
    setQuote(null);
    setQuoteError("");
  };

  const handleToggleSession = (sessionId) => {
    if (!currentPass || currentPass.isOverallPass) return;

    const s = availableSessions.find((x) => x.id === sessionId);
    if (s && s.remainingSeats != null && s.remainingSeats < quantity) {
      return; // Cannot select session with insufficient seats for requested quantity
    }

    const maxAllowed = currentPass.sessionsIncluded || 1;
    if (selectedSessionIds.includes(sessionId)) {
      setSelectedSessionIds((prev) => prev.filter((id) => id !== sessionId));
    } else {
      if (selectedSessionIds.length >= maxAllowed) {
        // If single session (Solo), clicking another session replaces previous selection
        if (maxAllowed === 1) {
          setSelectedSessionIds([sessionId]);
        }
        return;
      }
      setSelectedSessionIds((prev) => [...prev, sessionId]);
    }
  };

  // Pass configuration completion check
  const isPassSelectionDone = useMemo(() => {
    if (!currentPass) return false;
    if (currentPass.isOverallPass) return true;
    const requiredCount = currentPass.sessionsIncluded || 1;
    return selectedSessionIds.length === requiredCount && !sessionOverlapError;
  }, [currentPass, selectedSessionIds, sessionOverlapError]);

  // Calculate maximum quantity allowed for the selected pass (min 1, max 10)
  const maxPassQuantity = useMemo(() => {
    if (!currentPass) return 10;
    const { remainingSeats } = getPassAvailability(currentPass);
    let cap = 10;
    if (currentPass.isOverallPass) {
      const rems = availableSessions.map((x) => (x.remainingSeats != null ? x.remainingSeats : 10));
      if (rems.length > 0) cap = Math.min(cap, ...rems);
    } else if (selectedSessionIds.length > 0) {
      const rems = availableSessions
        .filter((x) => selectedSessionIds.includes(x.id))
        .map((x) => (x.remainingSeats != null ? x.remainingSeats : 10));
      if (rems.length > 0) cap = Math.min(cap, ...rems);
    }
    const maxPass = remainingSeats != null ? remainingSeats : 10;
    return Math.max(1, Math.min(10, maxPass, cap));
  }, [currentPass, availableSessions, selectedSessionIds]);

  // If selected quantity ever exceeds maxPassQuantity, automatically adjust it
  useEffect(() => {
    if (quantity > maxPassQuantity) {
      setQuantity(Math.max(1, maxPassQuantity));
    }
  }, [maxPassQuantity, quantity]);

  // Server Quote Fetching for both Passes & Legacy Tiers
  useEffect(() => {
    if (!isOpen || !workshop?.id) return;

    let isMounted = true;
    async function fetchQuote() {
      if (hasPasses) {
        if (!currentPass) {
          setQuote(null);
          return;
        }
        if (!isPassSelectionDone) {
          setQuote(null);
          setQuoteError("");
          return;
        }

        const sids = currentPass.isOverallPass
          ? []
          : selectedSessionIds;

        setLoadingQuote(true);
        setQuoteError("");
        try {
          const data = await workshopsApi.getWorkshopQuote(workshop.id, quantity, currentPass.id, sids);
          if (isMounted) {
            setQuote(data);
            setQuoteError("");
          }
        } catch (err) {
          if (isMounted) {
            console.error("Quote fetch error:", err);
            const msg = err?.data?.message || err?.message || "Failed to calculate quote.";
            setQuoteError(msg);
            setQuote(null);
          }
        } finally {
          if (isMounted) setLoadingQuote(false);
        }
      } else {
        setLoadingQuote(true);
        setQuoteError("");
        try {
          const data = await workshopsApi.getWorkshopQuote(workshop.id, quantity);
          if (isMounted) {
            setQuote(data);
            setQuoteError("");
          }
        } catch (err) {
          if (isMounted) {
            console.error("Quote fetch error:", err);
            const msg = err?.data?.message || err?.message || "Failed to calculate quote.";
            setQuoteError(msg);
            setQuote(null);
          }
        } finally {
          if (isMounted) setLoadingQuote(false);
        }
      }
    }

    fetchQuote();
    return () => { isMounted = false; };
  }, [isOpen, workshop?.id, quantity, hasPasses, currentPass, isPassSelectionDone, selectedSessionIds]);

  if (!isOpen || !workshop) return null;

  // Formatting helpers
  const formatDateFull = (iso) => {
    if (!iso) return "";
    try {
      return new Date(iso).toLocaleDateString("en-IN", {
        weekday: "long",
        day: "numeric",
        month: "long",
        year: "numeric",
      });
    } catch {
      return iso;
    }
  };

  const formatDate = (iso) => {
    if (!iso) return "";
    try {
      return new Date(iso).toLocaleDateString("en-IN", {
        weekday: "short",
        day: "numeric",
        month: "short",
      });
    } catch {
      return iso;
    }
  };

  const formatTime = (timeStr) => {
    if (!timeStr) return "";
    const [h, m] = timeStr.split(":");
    const hours = parseInt(h, 10);
    const ampm = hours >= 12 ? "PM" : "AM";
    const formatted = hours % 12 || 12;
    return `${formatted}:${m} ${ampm}`;
  };

  // Handle Submit / Continue
  const handleProceed = () => {
    if (hasPasses) {
      if (!currentPass) return;
      const selectedSessions = currentPass.isOverallPass
        ? availableSessions
        : availableSessions.filter((s) => selectedSessionIds.includes(s.id));

      const totalPayable = quote?.totalAmount ?? (Number(currentPass.currentPrice ?? currentPass.price) * quantity);

      onContinue({
        quantity,
        totalPayable,
        passTypeId: currentPass.id,
        passName: currentPass.name,
        selectedSessionIds: currentPass.isOverallPass ? [] : selectedSessionIds,
        selectedSessions,
        quote,
      });
    } else {
      onContinue({
        quantity,
        totalPayable: quote?.totalAmount ?? (currentPrice * quantity),
        quote,
      });
    }
  };

  // Check if pass configuration is valid
  const isPassValid = (() => {
    if (!hasPasses || !currentPass) return false;
    if (quantity < 1 || quantity > 10) return false;
    const { isSoldOut } = getPassAvailability(currentPass);
    if (isSoldOut) return false;
    if (quoteError) return false;
    if (!isPassSelectionDone) return false;
    return true;
  })();

  // Legacy Tier Price calculation
  const currentPrice = pricing?.currentPrice || workshop.price || 399;
  const remainingInTier = pricing?.ticketsRemainingInTier ?? 4;
  const currentTier = pricing?.currentTier || 1;
  const remainingTotal = pricing?.remainingSeats ?? workshop.capacity ?? 30;
  const maxAllowed = Math.min(10, remainingTotal);

  const totalPayableLegacy = quote?.totalAmount ?? (currentPrice * quantity);

  return (
    <div className="select-tickets-overlay" onClick={onClose}>
      <div
        className={`select-tickets-modal ${hasPasses ? "has-passes-modal" : ""}`}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
      >
        <div className="select-tickets-header">
          <div>
            <h2 className="select-tickets-title">
              {hasPasses ? "Select Workshop Pass" : "Select Tickets"}
            </h2>
            <p className="select-tickets-subtitle">{workshop.title}</p>
          </div>
          <button
            type="button"
            className="select-tickets-close-btn"
            onClick={onClose}
            aria-label="Close"
          >
            <X size={18} />
          </button>
        </div>

        <div className="select-tickets-body modal-scrollable-body">
          {hasPasses ? (
            /* MULTI-SESSION WORKSHOP PASS SELECTOR */
            <div className="pass-selector-container">
              {/* STEP 1: CHOOSE PASS TYPE */}
              <div className="pass-section-header">
                <span className="section-step-badge">1</span>
                <div>
                  <h3 className="section-heading">Choose Your Pass</h3>
                  <p className="section-hint">Select the pass that best matches how many sessions you wish to attend.</p>
                </div>
              </div>

              <div className="pass-options-grid">
                {activePassTypes.map((pass) => {
                  const isSelected = selectedPassId === pass.id;
                  const { isSoldOut, remainingSeats } = getPassAvailability(pass);

                  return (
                    <div
                      key={pass.id}
                      className={`pass-type-card ${isSelected ? "is-selected" : ""} ${isSoldOut ? "is-sold-out" : ""}`}
                      onClick={() => !isSoldOut && handleSelectPass(pass)}
                    >
                      <div className="pass-radio-container">
                        <div className={`pass-radio-outer ${isSelected ? "selected" : ""}`}>
                          {isSelected && <div className="pass-radio-inner" />}
                        </div>
                      </div>

                      <div className="pass-card-content">
                        <div className="pass-type-top">
                          <div className="pass-type-name-badge">
                            <span className="pass-type-name">{pass.name}</span>
                            {pass.isOverallPass ? (
                              <span className="pass-entitlement-pill">
                                All Workshops
                              </span>
                            ) : pass.sessionsIncluded === 1 ? (
                              <span className="pass-entitlement-pill">Solo</span>
                            ) : pass.sessionsIncluded === 2 ? (
                              <span className="pass-entitlement-pill">Dual</span>
                            ) : pass.sessionsIncluded === 3 ? (
                              <span className="pass-entitlement-pill">Trio</span>
                            ) : (
                              <span className="pass-entitlement-pill">
                                {pass.sessionsIncluded} Sessions
                              </span>
                            )}
                          </div>
                          <span className="pass-type-price">₹{Number(pass.currentPrice ?? pass.price).toLocaleString("en-IN")}</span>
                        </div>

                        {pass.description && (
                          <p className="pass-type-desc">{pass.description}</p>
                        )}

                        {pass.currentTierNumber != null && (
                          <div
                            style={{
                              marginTop: "6px",
                              padding: "2px 8px",
                              borderRadius: "6px",
                              background: "rgba(223, 128, 108, 0.12)",
                              border: "1px solid rgba(223, 128, 108, 0.3)",
                              fontSize: "11px",
                              color: "#df806c",
                              fontWeight: 600,
                              display: "inline-block",
                              alignSelf: "flex-start"
                            }}
                          >
                            Tier {pass.currentTierNumber}: ₹{Number(pass.currentPrice ?? pass.price).toLocaleString("en-IN")}
                            {pass.ticketsRemainingInCurrentTier != null && ` • ${pass.ticketsRemainingInCurrentTier} left at this price`}
                            {pass.nextTierPrice != null && `, next ₹${Number(pass.nextTierPrice).toLocaleString("en-IN")}`}
                          </div>
                        )}
                        {!isSoldOut && remainingSeats != null && remainingSeats > 0 && remainingSeats <= 10 && (
                          <span style={{ fontSize: "11px", color: "#f59e0b", fontWeight: 600, marginTop: "4px", display: "block" }}>
                            Only {remainingSeats} {remainingSeats === 1 ? "seat" : "seats"} left
                          </span>
                        )}
                      </div>

                      {isSoldOut && (
                        <span className="pass-sold-out-badge">SOLD OUT</span>
                      )}
                    </div>
                  );
                })}
              </div>

              {/* STEP 2: NUMBER OF TICKETS / QUANTITY SELECTOR */}
              {currentPass && (
                <div className="pass-quantity-step-section" style={{ marginTop: "16px", padding: "16px 20px", background: "rgba(255, 255, 255, 0.03)", border: "1px solid rgba(255, 255, 255, 0.08)", borderRadius: "14px" }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "12px" }}>
                    <div>
                      <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                        <span className="section-step-badge">2</span>
                        <h4 style={{ margin: 0, fontSize: "14px", fontWeight: 700, color: "#fff" }}>Number of Tickets</h4>
                      </div>
                      <p style={{ margin: "4px 0 0", fontSize: "12px", color: "#a1a1aa" }}>
                        ₹{Number(currentPass.currentPrice ?? currentPass.price).toLocaleString("en-IN")} per pass • {maxPassQuantity < 10 ? `Max ${maxPassQuantity} tickets available` : "Select 1 to 10 tickets per purchase"}
                      </p>
                    </div>

                    <div className="ticket-stepper">
                      <button
                        type="button"
                        className="stepper-btn"
                        onClick={() => quantity > 1 && setQuantity((q) => q - 1)}
                        disabled={quantity <= 1}
                        aria-label="Decrease quantity"
                      >
                        <Minus size={14} />
                      </button>
                      <span className="stepper-value" style={{ minWidth: "24px", textAlign: "center", fontWeight: 800 }}>{quantity}</span>
                      <button
                        type="button"
                        className="stepper-btn"
                        onClick={() => quantity < maxPassQuantity && setQuantity((q) => q + 1)}
                        disabled={quantity >= maxPassQuantity}
                        aria-label="Increase quantity"
                      >
                        <Plus size={14} />
                      </button>
                    </div>
                  </div>

                  {/* QUOTE ERROR BANNER */}
                  {quoteError && (
                    <div className="session-overlap-alert" style={{ marginTop: "12px" }}>
                      <AlertCircle size={16} />
                      <span>{quoteError}</span>
                    </div>
                  )}

                  {/* SPLIT TIER WARNING IF QUANTITY CROSSES BOUNDARY */}
                  {quote?.isSplitTier && (
                    <div className="split-tier-callout" style={{ marginTop: "12px" }}>
                      <AlertCircle size={16} className="split-icon" />
                      <div className="split-text">
                        <strong>Dynamic Pricing Notice</strong>
                        <p>{quote.splitTierMessage}</p>
                        <div className="split-breakdown-pills">
                          {quote.breakdown.map((b, idx) => (
                            <span key={idx} className="split-pill">
                              {b.quantity}x {b.tierName} @ ₹{b.unitPrice}
                            </span>
                          ))}
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* STEP 3: SESSION PICKER (FOR FINITE PASSES) OR ALL-ACCESS SUMMARY */}
              {currentPass && (
                <div className="pass-sessions-section">
                  <div className="pass-section-header" style={{ justifyContent: "space-between", alignItems: "center" }}>
                    <div style={{ display: "flex", alignItems: "flex-start", gap: "12px" }}>
                      <span className="section-step-badge">3</span>
                      <div>
                        <h3 className="section-heading">
                          {currentPass.isOverallPass
                            ? "Included Sessions (All-Access)"
                            : `Select Your ${currentPass.sessionsIncluded} Session${currentPass.sessionsIncluded > 1 ? "s" : ""}`}
                        </h3>
                        <p className="section-hint">
                          {currentPass.isOverallPass
                            ? "You have full all-access! All scheduled sessions below are included with this pass."
                            : `Choose ${currentPass.sessionsIncluded} session${currentPass.sessionsIncluded > 1 ? "s" : ""} to attend with your ${quantity} ${quantity === 1 ? "ticket" : "tickets"}.`}
                        </p>
                      </div>
                    </div>

                    {!currentPass.isOverallPass && (
                      <span className="selection-count-pill">
                        {selectedSessionIds.length} of {currentPass.sessionsIncluded || 1} selected
                      </span>
                    )}
                  </div>

                  {sessionOverlapError && (
                    <div className="session-overlap-alert">
                      <AlertCircle size={16} />
                      <span>{sessionOverlapError}</span>
                    </div>
                  )}

                  {/* SESSIONS GROUPED BY DATE */}
                  {getChronologicalGroupedSessions(availableSessions, workshop.workshopDate).map(([dateStr, sessionsOnDate]) => {
                    return (
                      <div key={dateStr} className="modal-date-sessions-group">
                        <div className="modal-date-sessions-header">
                          <div className="modal-date-left">
                            <Calendar size={14} style={{ color: "#df806c" }} />
                            <span>{formatDateFull(dateStr)}</span>
                          </div>
                          <span className="modal-date-count">
                            {sessionsOnDate.length} {sessionsOnDate.length === 1 ? "Session Available" : "Sessions Available"}
                          </span>
                        </div>

                        <div className="pass-sessions-list">
                          {sessionsOnDate.map((session) => {
                            const isSelected = selectedSessionIds.includes(session.id);
                            const isOverall = currentPass.isOverallPass;
                            const notEnoughSeats = session.remainingSeats != null && session.remainingSeats < quantity;
                            const isSessionClosed = session.isBookingClosed || session.remainingSeats <= 0;
                            const maxReached =
                              !isOverall &&
                              !isSelected &&
                              (currentPass.sessionsIncluded || 1) > 1 &&
                              selectedSessionIds.length >= (currentPass.sessionsIncluded || 1);
                            const isSessionDisabled = !isOverall && (isSessionClosed || notEnoughSeats || (maxReached && (currentPass.sessionsIncluded || 1) > 1));

                            const trainerObj = workshop.trainers?.find(
                              (t) => (t.id || t.trainerProfileId) === session.trainerProfileId
                            );
                            const sessionTrainerName = getTrainerDisplayName(session.trainerName || trainerObj);

                            return (
                              <div
                                key={session.id}
                                className={`session-picker-card ${
                                  isOverall || isSelected ? "is-active-session" : ""
                                } ${isSessionClosed ? "is-session-closed" : ""} ${
                                  isSessionDisabled && !isSessionClosed ? "is-session-disabled" : ""
                                }`}
                                onClick={() => {
                                  if (!isOverall && !isSessionDisabled) {
                                    handleToggleSession(session.id);
                                  }
                                }}
                              >
                                <div className="session-radio-container">
                                  <div className={`session-radio-outer ${isSelected || isOverall ? "selected" : ""}`}>
                                    {(isSelected || isOverall) && <div className="session-radio-inner" />}
                                  </div>
                                </div>

                                <div className="session-trainer-avatar-col">
                                  <TrainerAvatar
                                    trainer={trainerObj}
                                    name={sessionTrainerName}
                                    size={38}
                                    bordered
                                    borderColor="rgba(223, 128, 108, 0.5)"
                                  />
                                </div>

                                <div className="session-picker-info">
                                  <div className="session-time-row">
                                    <span className="session-time-text">
                                      <Clock size={13} style={{ color: "#df806c" }} />
                                      {formatTime(session.startTime)} – {formatTime(session.endTime)}
                                    </span>
                                    <span className="session-trainer-inline">
                                      <User size={13} />
                                      {sessionTrainerName}
                                    </span>
                                    <span className="session-seats-badge" style={notEnoughSeats ? { color: "#f87171", background: "rgba(239, 68, 68, 0.12)", borderColor: "rgba(239, 68, 68, 0.25)" } : {}}>
                                      {session.remainingSeats != null
                                        ? notEnoughSeats
                                          ? `Only ${session.remainingSeats} Left (Need ${quantity})`
                                          : `${session.remainingSeats} Seats Left`
                                        : "Seats Available"}
                                    </span>
                                  </div>
                                  <h4 className="session-title-text">{session.title}</h4>
                                  {session.description && (
                                    <p style={{ margin: "2px 0 0", fontSize: "12px", color: "#a1a1aa", lineHeight: 1.35 }}>
                                      {session.description}
                                    </p>
                                  )}
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          ) : (
            /* LEGACY TIER PASS SELECTOR */
            <>
              {pricing?.tiers && pricing.tiers.length > 0 ? (
                pricing.tiers.map((tier) => {
                  const isPast = tier.tierNumber < currentTier;
                  const isActive = tier.tierNumber === currentTier;

                  if (isPast) {
                    return (
                      <div key={tier.tierNumber} className="ticket-tier-row is-disabled">
                        <div className="ticket-tier-info">
                          <span className="ticket-tier-name">{tier.tierName || `Tier ${tier.tierNumber}`} Pass</span>
                          <span className="ticket-tier-price">₹{tier.price}</span>
                        </div>
                        <span className="ticket-tier-badge sold-out">SOLD OUT</span>
                      </div>
                    );
                  }

                  if (isActive) {
                    return (
                      <div key={tier.tierNumber} className="ticket-tier-row is-active">
                        <div className="ticket-tier-info">
                          <span className="ticket-tier-name">
                            {tier.tierName || pricing?.currentTierName || `Tier ${currentTier}`} Pass
                          </span>
                          <span className="ticket-tier-meta">
                            ₹{currentPrice} per ticket • {remainingInTier} left at this price
                          </span>
                        </div>
                        <div className="ticket-stepper">
                          <button
                            type="button"
                            className="stepper-btn"
                            onClick={() => quantity > 1 && setQuantity((q) => q - 1)}
                            disabled={quantity <= 1}
                            aria-label="Decrease quantity"
                          >
                            <Minus size={14} />
                          </button>
                          <span className="stepper-value">{quantity}</span>
                          <button
                            type="button"
                            className="stepper-btn"
                            onClick={() => quantity < maxAllowed && setQuantity((q) => q + 1)}
                            disabled={quantity >= maxAllowed}
                            aria-label="Increase quantity"
                          >
                            <Plus size={14} />
                          </button>
                        </div>
                      </div>
                    );
                  }

                  return null;
                })
              ) : (
                <div className="ticket-tier-row is-active">
                  <div className="ticket-tier-info">
                    <span className="ticket-tier-name">
                      {pricing?.currentTierName || `Tier ${currentTier}`} Pass
                    </span>
                    <span className="ticket-tier-meta">
                      ₹{currentPrice} per ticket • {remainingInTier} left at this price
                    </span>
                  </div>
                  <div className="ticket-stepper">
                    <button
                      type="button"
                      className="stepper-btn"
                      onClick={() => quantity > 1 && setQuantity((q) => q - 1)}
                      disabled={quantity <= 1}
                      aria-label="Decrease quantity"
                    >
                      <Minus size={14} />
                    </button>
                    <span className="stepper-value">{quantity}</span>
                    <button
                      type="button"
                      className="stepper-btn"
                      onClick={() => quantity < maxAllowed && setQuantity((q) => q + 1)}
                      disabled={quantity >= maxAllowed}
                      aria-label="Increase quantity"
                    >
                      <Plus size={14} />
                    </button>
                  </div>
                </div>
              )}

              {/* SPLIT TIER WARNING IF QUANTITY CROSSES BOUNDARY */}
              {quote?.isSplitTier && (
                <div className="split-tier-callout">
                  <AlertCircle size={16} className="split-icon" />
                  <div className="split-text">
                    <strong>Dynamic Pricing Notice</strong>
                    <p>{quote.splitTierMessage}</p>
                    <div className="split-breakdown-pills">
                      {quote.breakdown.map((b, idx) => (
                        <span key={idx} className="split-pill">
                          {b.quantity}x {b.tierName} @ ₹{b.unitPrice}
                        </span>
                      ))}
                    </div>
                  </div>
                </div>
              )}

              {/* CAPACITY NOTICE */}
              <div className="tier-capacity-hint">
                <span>{remainingTotal} total seats remaining in workshop.</span>
                <span>Max 10 tickets per booking.</span>
              </div>
            </>
          )}
        </div>

        {/* FOOTER */}
        <div className="select-tickets-footer">
          <div className="select-tickets-footer-left">
            <div className="footer-meta-col footer-pass-col">
              <span className="footer-meta-label">Selected Pass</span>
              <span className="footer-meta-value" title={currentPass ? `${currentPass.name} (${currentPass.isOverallPass ? "All Workshops" : currentPass.sessionsIncluded === 1 ? "Solo" : currentPass.sessionsIncluded === 2 ? "Dual" : currentPass.sessionsIncluded === 3 ? "Trio" : `${currentPass.sessionsIncluded} Sessions`})` : ""}>
                {hasPasses
                  ? currentPass
                    ? `${quantity}x ${currentPass.name} (${currentPass.isOverallPass ? "All Workshops" : currentPass.sessionsIncluded === 1 ? "Solo" : currentPass.sessionsIncluded === 2 ? "Dual" : currentPass.sessionsIncluded === 3 ? "Trio" : `${currentPass.sessionsIncluded} Sessions`})`
                    : "None selected"
                  : `${quantity} ${quantity === 1 ? "Ticket" : "Tickets"}`}
              </span>
            </div>

            <div className="footer-meta-divider" />

            <div className="footer-meta-col footer-total-col">
              <span className="footer-meta-label">Total</span>
              <span className="footer-total-price">
                ₹{Number(hasPasses ? (quote?.totalAmount ?? ((currentPass?.currentPrice ?? currentPass?.price ?? 0) * quantity)) : totalPayableLegacy).toLocaleString("en-IN")}
              </span>
            </div>
          </div>

          <button
            type="button"
            className="select-tickets-continue-btn"
            disabled={hasPasses ? (!isPassValid || loadingQuote || !!quoteError) : (quantity < 1 || loadingQuote || !!quoteError)}
            onClick={handleProceed}
          >
            {loadingQuote ? (
              "Calculating..."
            ) : (
              <>
                <span>Continue</span>
                <span className="continue-chevron">›</span>
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
