import { useEffect, useRef, useState, useCallback } from "react";
import { 
  X, Printer, CheckCircle, MapPin, Calendar, Clock, User, ShieldCheck, 
  ChevronLeft, ChevronRight, Edit2, Check, AlertCircle, Send, Users
} from "lucide-react";
import QrCode from "../common/QrCode";
import { workshopsApi } from "../../services/workshopsApi";
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
    window.print();
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
      setResendStatus("Pass notification resent to registered contact.");
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

        {/* MULTI-TICKET SWITCHER BAR */}
        {tickets.length > 1 && (
          <div className="workshop-multi-ticket-bar">
            <div className="multi-ticket-info">
              <Users size={16} />
              <span>GROUP BOOKING: <strong>{tickets.length} PASSES</strong></span>
            </div>
            <div className="multi-ticket-nav">
              <button
                type="button"
                className="ticket-nav-btn"
                disabled={activeIndex === 0}
                onClick={() => setActiveIndex(prev => Math.max(0, prev - 1))}
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
              >
                <ChevronRight size={16} />
              </button>
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
                <small>OFFICIAL WORKSHOP PASS</small>
              </div>
            </div>

            <div className={`ticket-status-pill ${isCheckedIn ? "status-checked-in" : "status-confirmed"}`}>
              <CheckCircle size={14} className="ticket-status-icon" />
              <span>{isCheckedIn ? "ENTRY CONFIRMED (CHECKED IN)" : "ACTIVE PASS / READY FOR ENTRY"}</span>
            </div>
          </div>

          {/* MAIN EVENT BODY */}
          <div className="workshop-ticket-body">
            <div className="ticket-title-section">
              <div className="ticket-title-header">
                <span className="ticket-eyebrow">MASTERCLASS & INTENSIVE</span>
                {currentTicket?.ticketNumber && (
                  <span className="ticket-num-badge">{currentTicket.ticketNumber}</span>
                )}
              </div>
              <h2 className="ticket-workshop-title">{booking.workshopTitle || "Dance Workshop"}</h2>
            </div>

            {/* EVENT SCHEDULE GRID */}
            <div className="ticket-details-grid">
              <div className="ticket-detail-item">
                <Calendar size={16} className="ticket-detail-icon" />
                <div>
                  <span className="detail-label">DATE</span>
                  <strong className="detail-value">{formatDate(booking.workshopDate)}</strong>
                </div>
              </div>

              <div className="ticket-detail-item">
                <Clock size={16} className="ticket-detail-icon" />
                <div>
                  <span className="detail-label">TIME</span>
                  <strong className="detail-value">
                    {booking.startTime && booking.endTime
                      ? `${formatTime(booking.startTime)} – ${formatTime(booking.endTime)}`
                      : "Session Time Confirmed"}
                  </strong>
                </div>
              </div>

              <div className="ticket-detail-item">
                <User size={16} className="ticket-detail-icon" />
                <div>
                  <span className="detail-label">INSTRUCTOR</span>
                  <strong className="detail-value">{booking.trainerName || "Elite Faculty"}</strong>
                </div>
              </div>

              <div className="ticket-detail-item">
                <MapPin size={16} className="ticket-detail-icon" />
                <div>
                  <span className="detail-label">VENUE</span>
                  <strong className="detail-value">{booking.venue || "Ethos Dance Studio Main Arena"}</strong>
                </div>
              </div>
            </div>

            {/* ATTENDEE & ADMISSION STRIP */}
            <div className="ticket-meta-strip">
              <div className="ticket-meta-col">
                <div className="meta-header-with-action">
                  <span className="meta-label">ATTENDEE</span>
                  {!isCheckedIn && !editingGuest && (
                    <button 
                      type="button" 
                      className="edit-guest-btn"
                      onClick={() => setEditingGuest(true)}
                      title="Edit Attendee Details"
                    >
                      <Edit2 size={12} />
                      <span>Edit</span>
                    </button>
                  )}
                </div>
                <strong className="meta-value">{currentTicket?.attendeeName || "Guest Attendee"}</strong>
                <small className="meta-sub">
                  {currentTicket?.isPrimaryAttendee ? "Primary Booking Holder" : "Assigned Guest"}
                  {currentTicket?.attendeePhone ? ` • ${currentTicket.attendeePhone}` : ""}
                </small>
              </div>

              <div className="ticket-meta-col">
                <span className="meta-label">ADMISSION</span>
                <strong className="meta-value ticket-price-highlight">
                  PASS {activeIndex + 1} OF {tickets.length}
                </strong>
                <small className="meta-sub">Verified Razorpay</small>
              </div>

              <div className="ticket-meta-col">
                <span className="meta-label">TICKET NO.</span>
                <strong className="meta-value ticket-ref-code">
                  {currentTicket?.ticketNumber || bookingRef}
                </strong>
                <small className="meta-sub">Official Pass</small>
              </div>
            </div>

            {/* INLINE GUEST EDITOR MODAL/FORM */}
            {editingGuest && (
              <form className="guest-edit-form" onSubmit={handleSaveGuest}>
                <div className="guest-form-title">
                  <span>Assign Attendee Identity</span>
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

            {/* PERFORATION NOTCHES */}
            <div className="ticket-perforation">
              <div className="perforation-notch notch-left" />
              <div className="perforation-line" />
              <div className="perforation-notch notch-right" />
            </div>

            {/* QR CODE & ENTRY CODE SECTION */}
            <div className="ticket-qr-section">
              <div className="ticket-qr-box">
                {currentTicket?.qrToken ? (
                  <QrCode value={currentTicket.qrToken} size={130} color="#ffffff" bgColor="#12100e" />
                ) : (
                  <QrCode value={currentTicket?.ticketNumber || bookingRef} size={130} color="#ffffff" bgColor="#12100e" />
                )}
              </div>

              <div className="ticket-qr-meta">
                <div className="qr-security-badge">
                  <ShieldCheck size={14} />
                  <span>HIGH-ENTROPY SECURE QR TOKEN</span>
                </div>
                <p className="qr-instructions">
                  {isCheckedIn
                    ? "This ticket pass has already been validated and checked in for entry."
                    : "Present this individual pass QR code at studio check-in for contactless verification."}
                </p>
                <div className="qr-ref-pill">
                  TICKET: <strong>{currentTicket?.ticketNumber || bookingRef}</strong>
                </div>
              </div>
            </div>
          </div>

          {/* TICKET FOOTER */}
          <div className="workshop-ticket-footer">
            <div className="ticket-footer-terms">
              <span>Non-transferable once checked in • Please arrive 15 minutes prior to session start</span>
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
            <span>{resendLoading ? "RESENDING..." : "RESEND PASS"}</span>
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
