import { useState, useEffect, useRef, useCallback } from "react";
import { 
  X, Camera, QrCode, CheckCircle2, AlertTriangle, XCircle, Search, 
  Users, Volume2, VolumeX, Zap, RefreshCw, ArrowRight, ShieldAlert, Check
} from "lucide-react";
import { trainerApi } from "../../services/trainerApi";
import "./TrainerQrScannerModal.css";

// Web Audio API chimes (works on all modern browsers without external audio files)
function playTone(type, isMuted) {
  if (isMuted) return;
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);

    const now = ctx.currentTime;
    if (type === "success") {
      // Pleasant double chime
      osc.frequency.setValueAtTime(587.33, now); // D5
      osc.frequency.setValueAtTime(880, now + 0.1); // A5
      gain.gain.setValueAtTime(0.3, now);
      gain.gain.exponentialRampToValueAtTime(0.01, now + 0.35);
      osc.start(now);
      osc.stop(now + 0.35);
    } else if (type === "warning") {
      // Amber warning tone
      osc.frequency.setValueAtTime(440, now);
      gain.gain.setValueAtTime(0.3, now);
      gain.gain.exponentialRampToValueAtTime(0.01, now + 0.25);
      osc.start(now);
      osc.stop(now + 0.25);
    } else if (type === "error") {
      // Low buzz error
      osc.type = "sawtooth";
      osc.frequency.setValueAtTime(150, now);
      gain.gain.setValueAtTime(0.4, now);
      gain.gain.exponentialRampToValueAtTime(0.01, now + 0.4);
      osc.start(now);
      osc.stop(now + 0.4);
    }
  } catch {
    // AudioContext blocked or not supported
  }
}

export default function TrainerQrScannerModal({ isOpen, workshop, onClose, onAttendanceChanged }) {
  const [mode, setMode] = useState("standard"); // 'standard' | 'fast'
  const [muted, setMuted] = useState(false);
  const [inputVal, setInputVal] = useState("");
  const [validating, setValidating] = useState(false);
  const [checkInLoading, setCheckInLoading] = useState(false);
  const [groupLoading, setGroupLoading] = useState(false);

  const [validationResult, setValidationResult] = useState(null);
  const [recentScans, setRecentScans] = useState([]);
  const [statusMessage, setStatusMessage] = useState("");

  const inputRef = useRef(null);

  useEffect(() => {
    if (isOpen) {
      setValidationResult(null);
      setInputVal("");
      setStatusMessage("");
      setTimeout(() => inputRef.current?.focus(), 150);
    }
  }, [isOpen]);

  const handleValidate = useCallback(async (tokenOrNumber) => {
    if (!tokenOrNumber || !workshop?.id) return;
    try {
      setValidating(true);
      setStatusMessage("");
      const res = await trainerApi.validateWorkshopTicket(workshop.id, tokenOrNumber);
      const data = res?.data || res;
      setValidationResult(data);

      if (data.isValid) {
        if (mode === "fast") {
          // In Fast Mode: Automatically check in immediately!
          playTone("success", muted);
          await executeCheckIn(data.ticketId);
        } else {
          playTone("warning", muted);
        }
      } else {
        if (data.validationStatus === "AlreadyCheckedIn") {
          playTone("warning", muted);
        } else {
          playTone("error", muted);
        }
      }
    } catch (err) {
      playTone("error", muted);
      setStatusMessage(err.response?.data?.message || err.message || "Failed to validate ticket.");
    } finally {
      setValidating(false);
    }
  }, [workshop?.id, mode, muted]);

  const executeCheckIn = async (ticketId) => {
    if (!ticketId || !workshop?.id) return;
    try {
      setCheckInLoading(true);
      const res = await trainerApi.checkInWorkshopTicket(workshop.id, ticketId, { method: mode === "fast" ? 1 : 0 });
      const data = res?.data || res;

      playTone("success", muted);
      setValidationResult(data);
      setStatusMessage(`${data.attendeeName} checked in successfully!`);

      setRecentScans(prev => [
        {
          id: data.ticketId || ticketId,
          ticketNumber: data.ticketNumber,
          name: data.attendeeName,
          time: new Date().toLocaleTimeString("en-IN", { hour: "numeric", minute: "2-digit" }),
          status: "CheckedIn"
        },
        ...prev.slice(0, 4)
      ]);

      if (onAttendanceChanged) onAttendanceChanged();

      if (mode === "fast") {
        setTimeout(() => {
          setValidationResult(null);
          setInputVal("");
          inputRef.current?.focus();
        }, 1200);
      }
    } catch (err) {
      playTone("error", muted);
      setStatusMessage(err.response?.data?.message || err.message || "Check-in failed.");
    } finally {
      setCheckInLoading(false);
    }
  };

  const executeGroupCheckIn = async (bookingId) => {
    if (!bookingId || !workshop?.id) return;
    try {
      setGroupLoading(true);
      const res = await trainerApi.groupCheckInWorkshop(workshop.id, bookingId);
      const data = res?.data || res;

      playTone("success", muted);
      setStatusMessage(`Group check-in: ${data.successfullyCheckedIn} attendee(s) admitted!`);

      if (validationResult) {
        setValidationResult(prev => ({
          ...prev,
          isValid: false,
          validationStatus: "AlreadyCheckedIn",
          message: `Group checked in (${data.successfullyCheckedIn} checked in)`
        }));
      }

      if (onAttendanceChanged) onAttendanceChanged();
    } catch (err) {
      playTone("error", muted);
      setStatusMessage(err.response?.data?.message || err.message || "Group check-in failed.");
    } finally {
      setGroupLoading(false);
    }
  };

  const handleSubmitInput = (e) => {
    e.preventDefault();
    if (inputVal.trim()) {
      handleValidate(inputVal.trim());
    }
  };

  if (!isOpen) return null;

  return (
    <div className="trainer-scanner-overlay" onClick={onClose}>
      <div className="trainer-scanner-modal" onClick={e => e.stopPropagation()}>
        {/* HEADER */}
        <div className="scanner-header">
          <div className="scanner-title-area">
            <div className="scanner-badge-icon">
              <QrCode size={20} />
            </div>
            <div>
              <h3>Workshop Check-In & Scanner</h3>
              <p>{workshop?.title || "Ethos Masterclass"}</p>
            </div>
          </div>

          <div className="scanner-header-controls">
            {/* AUDIO MUTE TOGGLE */}
            <button
              type="button"
              className={`scanner-icon-btn ${muted ? "is-muted" : ""}`}
              onClick={() => setMuted(prev => !prev)}
              title={muted ? "Unmute Audio" : "Mute Audio"}
            >
              {muted ? <VolumeX size={18} /> : <Volume2 size={18} />}
            </button>

            {/* SCAN MODE TOGGLE */}
            <div className="scanner-mode-switch">
              <button
                type="button"
                className={`mode-btn ${mode === "standard" ? "active" : ""}`}
                onClick={() => setMode("standard")}
              >
                Standard
              </button>
              <button
                type="button"
                className={`mode-btn fast-mode ${mode === "fast" ? "active" : ""}`}
                onClick={() => setMode("fast")}
              >
                <Zap size={13} />
                Fast Scan
              </button>
            </div>

            <button type="button" className="scanner-close-btn" onClick={onClose}>
              <X size={20} />
            </button>
          </div>
        </div>

        {/* MAIN BODY */}
        <div className="scanner-body">
          {/* CAMERA SIMULATION & INPUT SECTION */}
          <div className="scanner-viewfinder-box">
            <div className="viewfinder-lens">
              <div className="lens-corner top-left" />
              <div className="lens-corner top-right" />
              <div className="lens-corner bottom-left" />
              <div className="lens-corner bottom-right" />
              <div className="scanning-laser" />

              <div className="viewfinder-content">
                <Camera size={36} className="viewfinder-camera-icon" />
                <span>Camera Stream / Scanner Active</span>
                <small>Scan QR badge or enter ticket code below</small>
              </div>
            </div>

            <div className="mode-indicator-strip">
              {mode === "fast" ? (
                <span className="fast-indicator">
                  <Zap size={14} /> FAST MODE: Auto-confirms valid passes automatically
                </span>
              ) : (
                <span className="standard-indicator">
                  STANDARD MODE: Validates attendee identity before entry confirmation
                </span>
              )}
            </div>
          </div>

          {/* MANUAL TICKET SEARCH / SCAN INPUT */}
          <form className="scanner-input-bar" onSubmit={handleSubmitInput}>
            <div className="scanner-input-wrapper">
              <Search size={18} className="scanner-input-icon" />
              <input
                ref={inputRef}
                type="text"
                placeholder="Scan QR or enter ticket number (e.g. ETHOS-WKS-... or ETHOS-TKT-...)"
                value={inputVal}
                onChange={e => setInputVal(e.target.value)}
                disabled={validating || checkInLoading}
              />
            </div>
            <button
              type="submit"
              className="scanner-scan-btn"
              disabled={validating || checkInLoading || !inputVal.trim()}
            >
              {validating ? <RefreshCw size={16} className="spinning" /> : "Verify"}
            </button>
          </form>

          {/* STATUS MESSAGE */}
          {statusMessage && (
            <div className="scanner-status-toast">
              <span>{statusMessage}</span>
            </div>
          )}

          {/* VALIDATION RESULT CARD */}
          {validationResult && (
            <div className={`scanner-result-card ${validationResult.isValid ? "is-valid" : "is-invalid"}`}>
              <div className="result-header">
                {validationResult.isValid ? (
                  <div className="result-status-title status-success">
                    <CheckCircle2 size={24} />
                    <div>
                      <strong>VALID PASS READY FOR ENTRY</strong>
                      <span>{validationResult.message}</span>
                    </div>
                  </div>
                ) : validationResult.validationStatus === "AlreadyCheckedIn" ? (
                  <div className="result-status-title status-warning">
                    <AlertTriangle size={24} />
                    <div>
                      <strong>ALREADY CHECKED IN</strong>
                      <span>{validationResult.message}</span>
                    </div>
                  </div>
                ) : (
                  <div className="result-status-title status-danger">
                    <XCircle size={24} />
                    <div>
                      <strong>ENTRY REJECTED: {validationResult.validationStatus}</strong>
                      <span>{validationResult.message}</span>
                    </div>
                  </div>
                )}

                {validationResult.ticketNumber && (
                  <span className="result-ticket-code">{validationResult.ticketNumber}</span>
                )}
              </div>

              {/* ATTENDEE DETAILS */}
              <div className="result-attendee-details">
                <div className="attendee-field">
                  <span className="field-lbl">ATTENDEE</span>
                  <strong className="field-val">{validationResult.attendeeName || "Guest"}</strong>
                  {validationResult.isPrimaryAttendee && (
                    <span className="primary-pill">Primary Buyer</span>
                  )}
                </div>

                {validationResult.maskedPhone && (
                  <div className="attendee-field">
                    <span className="field-lbl">PHONE</span>
                    <strong className="field-val">{validationResult.maskedPhone}</strong>
                  </div>
                )}
              </div>

              {/* ACTION BUTTONS */}
              <div className="result-actions">
                {validationResult.isValid && (
                  <button
                    type="button"
                    className="action-confirm-entry"
                    onClick={() => executeCheckIn(validationResult.ticketId)}
                    disabled={checkInLoading}
                  >
                    <Check size={18} />
                    <span>{checkInLoading ? "Confirming..." : "Confirm Entry Check-In"}</span>
                  </button>
                )}

                {/* GROUP CHECK-IN OPTION */}
                {validationResult.groupTickets && validationResult.groupTickets.length > 1 && (
                  <div className="group-booking-box">
                    <div className="group-box-header">
                      <div className="group-title">
                        <Users size={16} />
                        <span>Booking Group ({validationResult.groupTickets.length} Passes Total)</span>
                      </div>
                      {validationResult.groupTickets.some(gt => gt.isEligibleForCheckIn) && (
                        <button
                          type="button"
                          className="group-checkin-all-btn"
                          onClick={() => executeGroupCheckIn(validationResult.workshopBookingId)}
                          disabled={groupLoading}
                        >
                          {groupLoading ? "Checking In All..." : "Check In All Remaining"}
                        </button>
                      )}
                    </div>

                    <div className="group-roster-list">
                      {validationResult.groupTickets.map(gt => (
                        <div key={gt.ticketId} className="group-roster-item">
                          <span className="roster-name">{gt.attendeeName}</span>
                          <span className={`roster-status ${gt.isCheckedIn ? "checked" : "eligible"}`}>
                            {gt.isCheckedIn ? "Checked In" : "Ready"}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* RECENT SCANS LIST */}
          {recentScans.length > 0 && (
            <div className="recent-scans-panel">
              <span className="recent-title">Recent Session Check-Ins</span>
              <div className="recent-list">
                {recentScans.map(s => (
                  <div key={s.id} className="recent-item">
                    <div className="recent-info">
                      <CheckCircle2 size={14} className="recent-check-icon" />
                      <strong>{s.name}</strong>
                      <span className="recent-code">{s.ticketNumber}</span>
                    </div>
                    <span className="recent-time">{s.time}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
