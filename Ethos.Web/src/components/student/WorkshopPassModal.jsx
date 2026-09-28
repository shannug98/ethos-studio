import { useEffect, useRef, useState, useCallback } from "react";
import { 
  X, Printer, CheckCircle, MapPin, Calendar, Clock, User, ShieldCheck, 
  ChevronLeft, ChevronRight, Edit2, Check, AlertCircle, Send, Users, Sparkles, MessageSquare
} from "lucide-react";
import QrCode from "../common/QrCode";
import { workshopsApi } from "../../services/workshopsApi";
import { API_BASE_URL } from "../../services/apiClient";
import "./WorkshopPassModal.css";

export default function WorkshopPassModal({ isOpen, booking, student, onClose }) {
  const modalRef = useRef(null);

  const [tickets, setTickets] = useState([]);
  const [loading, setLoading] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);

  // Edit guest state
  const [editingGuest, setEditingGuest] = useState(false);
  const [guestForm, setGuestForm] = useState({ attendeeName: "", attendeePhone: "", attendeeEmail: "" });
  const [saveLoading, setSaveLoading] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [saveSuccess, setSaveSuccess] = useState("");

  // Resend state
  const [resendLoading, setResendLoading] = useState(false);
  const [resendStatus, setResendStatus] = useState("");

  const loadTickets = useCallback(async () => {
    if (!booking?.id) return;
    try {
      setLoading(true);
      const res = await workshopsApi.getBookingTickets(booking.id);
      const data = Array.isArray(res?.data) ? res.data : (Array.isArray(res) ? res : []);
      if (data.length > 0) {
        setTickets(data);
      } else if (booking.tickets && booking.tickets.length > 0) {
        setTickets(booking.tickets);
      } else {
        // Fallback synthetic ticket
        setTickets([{
          id: booking.id,
          ticketNumber: booking.bookingReference || `ETH-WS-${(booking.id || "").replace(/-/g, "").slice(0, 8).toUpperCase()}-01`,
          attendeeName: booking.customerName || student?.fullName || "Ethos Student",
          attendeePhone: booking.customerPhone || student?.phone,
          attendeeEmail: booking.customerEmail || student?.email,
          isPrimaryAttendee: true,
          status: "Issued",
          qrToken: `ETHOS-TKT-${booking.id}`
        }]);
      }
    } catch {
      if (booking.tickets && booking.tickets.length > 0) {
        setTickets(booking.tickets);
      } else {
        setTickets([{
          id: booking.id,
          ticketNumber: booking.bookingReference || `ETH-WS-${(booking.id || "").replace(/-/g, "").slice(0, 8).toUpperCase()}-01`,
          attendeeName: booking.customerName || student?.fullName || "Ethos Student",
          attendeePhone: booking.customerPhone || student?.phone,
          attendeeEmail: booking.customerEmail || student?.email,
          isPrimaryAttendee: true,
          status: "Issued",
          qrToken: `ETHOS-TKT-${booking.id}`
        }]);
      }
    } finally {
      setLoading(false);
    }
  }, [booking, student]);

  useEffect(() => {
    function handleKeyDown(e) {
      if (e.key === "Escape") onClose();
    }
    if (isOpen) {
      document.body.style.overflow = "hidden";
      window.addEventListener("keydown", handleKeyDown);
      loadTickets();
      setActiveIndex(0);
      setEditingGuest(false);
      setSaveError("");
      setSaveSuccess("");
      setResendStatus("");
    }
    return () => {
      document.body.style.overflow = "unset";
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, onClose, loadTickets]);

  const currentTicket = tickets[activeIndex] || tickets[0];

  useEffect(() => {
    if (currentTicket) {
      setGuestForm({
        attendeeName: currentTicket.attendeeName || "",
        attendeePhone: currentTicket.attendeePhone || "",
        attendeeEmail: currentTicket.attendeeEmail || ""
      });
      setEditingGuest(false);
      setSaveError("");
      setSaveSuccess("");
      setResendStatus("");
    }
  }, [activeIndex, currentTicket]);

  if (!isOpen || !booking) return null;

  const isGroupBooking = tickets.length > 1;
  const bookingRef =
    booking.bookingReference ||
    `ETH-WS-${(booking.id || "").replace(/-/g, "").slice(0, 8).toUpperCase()}`;

  const formatDate = (iso) => {
    if (!iso) return "TBA";
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

  const formatTime = (timeStr) => {
    if (!timeStr) return "Scheduled Session";
    const parts = timeStr.split(":");
    const hours = parseInt(parts[0], 10);
    const m = parts[1] || "00";
    const ampm = hours >= 12 ? "PM" : "AM";
    const formattedHours = hours % 12 || 12;
    return `${formattedHours}:${m} ${ampm}`;
  };

  const handlePrint = () => {
    const targetId = currentTicket?.id || booking?.id;
    if (targetId) {
      const token = currentTicket?.pdfDownloadToken ? `?token=${encodeURIComponent(currentTicket.pdfDownloadToken)}` : "";
      const pdfUrl = `${API_BASE_URL}/api/workshops/tickets/${targetId}/pdf${token}`;
      window.open(pdfUrl, "_blank");
    } else {
      window.print();
    }
  };

  const handleSaveGuest = async (e) => {
    e.preventDefault();
    if (!guestForm.attendeeName.trim()) {
      setSaveError("Attendee name is required.");
      return;
    }

    try {
      setSaveLoading(true);
      setSaveError("");
      const res = await workshopsApi.updateTicketAttendee(booking.id, currentTicket.id, guestForm);
      const updated = res?.data || res;
      setTickets(prev => prev.map((t, idx) => idx === activeIndex ? { ...t, ...updated } : t));
      setSaveSuccess("Attendee details updated successfully!");
      setEditingGuest(false);
    } catch (err) {
      setSaveError(err.response?.data?.message || err.message || "Failed to update attendee details.");
    } finally {
      setSaveLoading(false);
    }
  };

  const handleResend = async () => {
    if (!currentTicket?.id) return;
    try {
      setResendLoading(true);
      setResendStatus("");
      await workshopsApi.resendTicketPass(booking.id, currentTicket.id);
      setResendStatus(`Pass #${activeIndex + 1} resent via WhatsApp to registered contact.`);
    } catch (err) {
      setResendStatus(err.response?.data?.message || err.message || "Could not resend pass.");
    } finally {
      setResendLoading(false);
    }
  };

  const isCheckedIn = currentTicket?.status === "CheckedIn" || !!currentTicket?.checkedInAt;

  return (
    <div className="workshop-pass-overlay" onClick={onClose} aria-modal="true" role="dialog">
      <div
        className="workshop-pass-container"
        onClick={(e) => e.stopPropagation()}
        ref={modalRef}
      >
        {/* CLOSE BUTTON */}
        <button
          type="button"
          className="workshop-pass-close-btn"
          onClick={onClose}
          aria-label="Close Pass"
        >
          <X size={20} />
        </button>

        {/* TOP BOOKING MODE BADGE BAR */}
        {isGroupBooking ? (
          <div className="workshop-group-header-banner">
            <div className="group-banner-top">
              <div className="group-title-badge">
                <Users size={18} className="group-icon-pulse" />
                <span>GROUP BOOKING: <strong>{tickets.length} PASSES ISSUED</strong></span>
              </div>
              <div className="group-nav-stepper">
                <button
                  type="button"
                  className="ticket-nav-btn"
                  disabled={activeIndex === 0}
                  onClick={() => setActiveIndex(prev => Math.max(0, prev - 1))}
                  title="Previous Pass"
                >
                  <ChevronLeft size={16} />
                </button>
                <span className="ticket-nav-counter">
                  Pass {activeIndex + 1} of {tickets.length}
                </span>
                <button
                  type="button"
                  className="ticket-nav-btn"
                  disabled={activeIndex === tickets.length - 1}
                  onClick={() => setActiveIndex(prev => Math.min(tickets.length - 1, prev + 1))}
                  title="Next Pass"
                >
                  <ChevronRight size={16} />
                </button>
              </div>
            </div>

            {/* QUICK PASS SELECTOR PILLS */}
            <div className="group-pass-pills-row">
              {tickets.map((t, idx) => (
                <button
                  key={t.id || idx}
                  type="button"
                  className={`pass-selector-pill ${idx === activeIndex ? "active" : ""}`}
                  onClick={() => setActiveIndex(idx)}
                >
                  <span className="pill-index">Pass {idx + 1}</span>
                  <span className="pill-name">{t.attendeeName || `Guest ${idx + 1}`}</span>
                </button>
              ))}
            </div>

            {/* WHATSAPP & PASS CONFIRMATION BANNER */}
            <div className="whatsapp-delivery-note">
              <MessageSquare size={14} className="wa-icon" />
              <span>
                Your pass has been confirmed. A separate ticket has been generated for each selected session. All {tickets.length} passes are also sent to your WhatsApp.
              </span>
            </div>
          </div>
        ) : (
          <div className="workshop-solo-header-banner">
            <div className="solo-badge-tag">
              <Sparkles size={14} />
              <span>INDIVIDUAL WORKSHOP PASS</span>
            </div>
            <div className="solo-ref-code">
              Ref: <strong>{bookingRef}</strong>
            </div>
          </div>
        )}

        {/* TICKET CARD */}
        <div className="workshop-ticket-card" id="workshop-print-ticket">
          {/* HEADER STRIP */}
          <div className="workshop-ticket-header">
            <div className="ticket-brand-badge">
              <span className="ticket-brand-emblem">✦</span>
              <div className="ticket-brand-text">
                <strong>ETHOS DANCE STUDIO</strong>
                <small>{booking.passName ? `${booking.passName.toUpperCase()}` : "OFFICIAL DIGITAL PASS"}</small>
              </div>
            </div>

            <div className={`ticket-status-pill ${isCheckedIn ? "status-checked-in" : "status-confirmed"}`}>
              <CheckCircle size={13} className="ticket-status-icon" />
              <span>{isCheckedIn ? "CHECKED IN" : "ACTIVE ENTRY PASS"}</span>
            </div>
          </div>

          {/* MAIN PASS BODY */}
          <div className="workshop-ticket-body">
            {/* WORKSHOP & SESSION TITLE */}
            <div className="ticket-title-section">
              <span className="ticket-eyebrow">
                {currentTicket?.sessionTitle 
                  ? `SESSION: ${currentTicket.sessionTitle.toUpperCase()}`
                  : (isGroupBooking ? `PASS ${activeIndex + 1} OF ${tickets.length}` : "WORKSHOP PASS")}
              </span>
              <h2 className="ticket-workshop-title">{booking.workshopTitle || "Dance Workshop"}</h2>
            </div>

            {/* DOMINANT QR CREDENTIAL SPOTLIGHT */}
            <div className="ticket-qr-spotlight">
              <div className="ticket-qr-container">
                {currentTicket?.qrToken ? (
                  <QrCode value={currentTicket.qrToken} size={180} color="#ffffff" bgColor="#0d0b0a" />
                ) : (
                  <QrCode value={currentTicket?.ticketNumber || bookingRef} size={180} color="#ffffff" bgColor="#0d0b0a" />
                )}
              </div>
              <div className="ticket-qr-caption">
                <div className="qr-ref-code">
                  TICKET: <strong>{currentTicket?.ticketNumber || bookingRef}</strong>
                </div>
                <span className="qr-scan-hint">
                  {isCheckedIn ? "Pass verified & checked in" : "Present this QR code at studio entrance"}
                </span>
              </div>
            </div>

            {/* CLEAN UNIFIED META GRID */}
            <div className="ticket-unified-grid">
              <div className="ticket-grid-col">
                <div className="ticket-meta-block">
                  <div className="meta-header-row">
                    <span className="meta-label">ATTENDEE</span>
                    {!isCheckedIn && !editingGuest && (
                      <button 
                        type="button" 
                        className="edit-guest-btn"
                        onClick={() => setEditingGuest(true)}
                        title="Edit Attendee Details"
                      >
                        <Edit2 size={11} />
                        <span>Edit</span>
                      </button>
                    )}
                  </div>
                  <strong className="meta-value-main">{currentTicket?.attendeeName || "Guest Attendee"}</strong>
                  {currentTicket?.attendeePhone && (
                    <span className="meta-sub-text">{currentTicket.attendeePhone}</span>
                  )}
                </div>

                <div className="ticket-meta-block">
                  <span className="meta-label">DATE &amp; TIME</span>
                  <strong className="meta-value-main">
                    {formatDate(currentTicket?.sessionDate || booking.workshopDate)}
                  </strong>
                  <span className="meta-sub-text">
                    {currentTicket?.sessionStartTime && currentTicket?.sessionEndTime
                      ? `${formatTime(currentTicket.sessionStartTime)} – ${formatTime(currentTicket.sessionEndTime)}`
                      : (booking.startTime && booking.endTime
                          ? `${formatTime(booking.startTime)} – ${formatTime(booking.endTime)}`
                          : "Session Time Confirmed")}
                  </span>
                </div>
              </div>

              <div className="ticket-grid-col">
                <div className="ticket-meta-block">
                  <span className="meta-label">INSTRUCTOR</span>
                  <strong className="meta-value-main">
                    {currentTicket?.sessionTrainerName || booking.trainerName || "Ethos Faculty"}
                  </strong>
                  <span className="meta-sub-text">
                    {currentTicket?.passName || booking.passName || (isGroupBooking ? `Pass ${activeIndex + 1}/${tickets.length}` : "Standard Admission")}
                  </span>
                </div>

                <div className="ticket-meta-block">
                  <span className="meta-label">VENUE</span>
                  <strong className="meta-value-main">{booking.venue || "Ethos Dance Studio"}</strong>
                  <span className="meta-sub-text">Hyderabad Arena</span>
                </div>
              </div>
            </div>

            {/* INLINE GUEST EDITOR FORM */}
            {editingGuest && (
              <form className="guest-edit-form" onSubmit={handleSaveGuest}>
                <div className="guest-form-title">
                  <span>Assign Attendee for Pass #{activeIndex + 1}</span>
                  <button type="button" className="guest-close-btn" onClick={() => setEditingGuest(false)}>
                    <X size={14} />
                  </button>
                </div>

                {saveError && <div className="guest-form-error">{saveError}</div>}

                <div className="guest-input-grid">
                  <div className="guest-input-group">
                    <label>Full Name *</label>
                    <input
                      type="text"
                      required
                      placeholder="e.g. Maya Patel"
                      value={guestForm.attendeeName}
                      onChange={e => setGuestForm(prev => ({ ...prev, attendeeName: e.target.value }))}
                    />
                  </div>
                  <div className="guest-input-group">
                    <label>Phone (Optional)</label>
                    <input
                      type="tel"
                      placeholder="e.g. +91 98765 43210"
                      value={guestForm.attendeePhone}
                      onChange={e => setGuestForm(prev => ({ ...prev, attendeePhone: e.target.value }))}
                    />
                  </div>
                  <div className="guest-input-group">
                    <label>Email (Optional)</label>
                    <input
                      type="email"
                      placeholder="e.g. maya@example.com"
                      value={guestForm.attendeeEmail}
                      onChange={e => setGuestForm(prev => ({ ...prev, attendeeEmail: e.target.value }))}
                    />
                  </div>
                </div>

                <div className="guest-form-actions">
                  <button type="button" className="guest-cancel-btn" onClick={() => setEditingGuest(false)}>
                    Cancel
                  </button>
                  <button type="submit" className="guest-submit-btn" disabled={saveLoading}>
                    {saveLoading ? "Saving..." : "Save Attendee"}
                  </button>
                </div>
              </form>
            )}

            {saveSuccess && (
              <div className="guest-success-banner">
                <Check size={14} />
                <span>{saveSuccess}</span>
              </div>
            )}
          </div>

          {/* TICKET FOOTER */}
          <div className="workshop-ticket-footer">
            <div className="ticket-footer-terms">
              <span>Non-transferable • Arrive 15 minutes prior to session start</span>
            </div>
          </div>
        </div>

        {/* FEEDBACK & RESEND STATUS */}
        {resendStatus && (
          <div className="ticket-resend-feedback">
            <AlertCircle size={14} />
            <span>{resendStatus}</span>
          </div>
        )}

        {/* MODAL ACTIONS */}
        <div className="workshop-pass-actions">
          <button
            type="button"
            className="pass-action-btn pass-action-print"
            onClick={handlePrint}
          >
            <Printer size={16} />
            <span>PRINT / SAVE PASS</span>
          </button>
          <button
            type="button"
            className="pass-action-btn pass-action-resend"
            onClick={handleResend}
            disabled={resendLoading}
          >
            <Send size={16} />
            <span>{resendLoading ? "RESENDING..." : "RESEND TO WHATSAPP"}</span>
          </button>
          <button
            type="button"
            className="pass-action-btn pass-action-close"
            onClick={onClose}
          >
            CLOSE
          </button>
        </div>
      </div>
    </div>
  );
}

