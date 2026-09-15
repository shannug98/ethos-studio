import React, { useState, useEffect } from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
import { 
  ArrowLeft, Calendar, MapPin, ChevronDown, ChevronUp, AlertCircle 
} from "lucide-react";
import { workshopsApi } from "../services/workshopsApi";
import { createSlug } from "../utils/createSlug";
import { useAuth } from "../context/AuthContext";
import WorkshopPassModal from "../components/student/WorkshopPassModal";
import "./WorkshopCheckoutPage.css";

import fallbackImage from "../assets/workshops/workshop-01.jpg";

function loadRazorpayScript() {
  return new Promise((resolve) => {
    if (window.Razorpay) {
      resolve(true);
      return;
    }
    const script = document.createElement("script");
    script.src = "https://checkout.razorpay.com/v1/checkout.js";
    script.onload = () => resolve(true);
    script.onerror = () => resolve(false);
    document.body.appendChild(script);
  });
}

export default function WorkshopCheckoutPage() {
  const { slug, id: paramId } = useParams();
  const identifier = slug || paramId;
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();

  const stateData = location.state || {};
  const [workshop, setWorkshop] = useState(stateData.workshop || null);
  const [pricing, setPricing] = useState(stateData.pricing || null);
  const [quantity, setQuantity] = useState(stateData.quantity || 1);
  const [quote, setQuote] = useState(stateData.quote || null);

  const STORAGE_KEY = "ethos_booking_contact";

  // Form State
  const [fullName, setFullName] = useState(user?.fullName || "");
  const [phone, setPhone] = useState(user?.phone || "");
  const [email, setEmail] = useState(user?.email || "");
  const [rememberMe, setRememberMe] = useState(false);

  // Load saved details on page open
  useEffect(() => {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      try {
        const contact = JSON.parse(saved);
        if (contact.fullName && !user?.fullName) setFullName(contact.fullName);
        if (contact.whatsappNumber && !user?.phone) setPhone(contact.whatsappNumber);
        if (contact.email && !user?.email) setEmail(contact.email);
        setRememberMe(true);
      } catch {
        localStorage.removeItem(STORAGE_KEY);
      }
    }
    // Clean up legacy keys if any
    localStorage.removeItem("ethos_guest_name");
    localStorage.removeItem("ethos_guest_phone");
    localStorage.removeItem("ethos_guest_email");
  }, [user]);

  const handleRememberMe = (checked) => {
    setRememberMe(checked);
    if (checked) {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          fullName,
          whatsappNumber: phone,
          email,
        })
      );
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  };

  const handleFullNameChange = (val) => {
    setFullName(val);
    if (rememberMe) {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          fullName: val,
          whatsappNumber: phone,
          email,
        })
      );
    }
  };

  const handlePhoneChange = (val) => {
    const cleanPhone = val.replace(/\D/g, "").slice(0, 10);
    setPhone(cleanPhone);
    if (rememberMe) {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          fullName,
          whatsappNumber: cleanPhone,
          email,
        })
      );
    }
  };

  const handleEmailChange = (val) => {
    setEmail(val);
    if (rememberMe) {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          fullName,
          whatsappNumber: phone,
          email: val,
        })
      );
    }
  };

  // UI state
  const [showBreakdown, setShowBreakdown] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState("");
  const [confirmedBooking, setConfirmedBooking] = useState(null);
  const [isPassModalOpen, setIsPassModalOpen] = useState(false);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    window.scrollTo({ top: 0, behavior: "smooth" });
    if (!workshop && identifier) {
      async function resolveWorkshop() {
        try {
          let resolvedId = identifier;
          const list = await workshopsApi.getApprovedWorkshops().catch(() => []);
          if (Array.isArray(list)) {
            const matched = list.find(
              (item) =>
                createSlug(item.title) === identifier ||
                createSlug(item.workshopName) === identifier ||
                String(item.id).toLowerCase() === String(identifier).toLowerCase()
            );
            if (matched) resolvedId = matched.id;
            else if (list.length === 0) {
              setNotFound(true);
              return;
            }
          }

          const [ws, pr] = await Promise.all([
            workshopsApi.getWorkshopById(resolvedId),
            workshopsApi.getWorkshopPricing(resolvedId).catch(() => null),
          ]);
          if (ws) {
            setWorkshop(ws);
            if (pr && !pricing) setPricing(pr);
          } else {
            setNotFound(true);
          }
        } catch {
          setNotFound(true);
        }
      }
      resolveWorkshop();
    }
  }, [identifier, workshop, pricing, navigate]);

  const baseTotal = quote?.totalAmount || ((pricing?.currentPrice || workshop?.price || 599) * quantity);
  const platformFee = 0; // Final all-inclusive price with no added platform fees
  const netTotal = baseTotal;

  const handlePayment = async () => {
    setErrorMsg("");

    if (!fullName.trim()) {
      setErrorMsg("Please enter your Full Name.");
      return;
    }
    if (!phone.trim() || phone.trim().length < 10) {
      setErrorMsg("Please enter a valid 10-digit WhatsApp phone number.");
      return;
    }
    if (!email.trim() || !email.includes("@")) {
      setErrorMsg("Please enter a valid Email address.");
      return;
    }

    if (rememberMe) {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          fullName: fullName.trim(),
          whatsappNumber: phone.trim(),
          email: email.trim(),
        })
      );
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }

    const scriptLoaded = await loadRazorpayScript();
    if (!scriptLoaded) {
      setErrorMsg("Failed to connect to payment gateway. Please check your internet connection.");
      return;
    }

    setIsSubmitting(true);

    try {
      // 1. Authoritative order creation on backend
      const order = await workshopsApi.createWorkshopOrder(workshop.id, {
        quantity,
        fullName: fullName.trim(),
        phone: phone.trim(),
        email: email.trim(),
      });

      // 2. Open Razorpay modal
      const options = {
        key: order.razorpayKeyId,
        amount: Math.round(order.amount * 100),
        currency: order.currency || "INR",
        name: "ETHOS DANCE STUDIO",
        description: `${workshop.title} (${quantity} ${quantity === 1 ? "Ticket" : "Tickets"})`,
        order_id: order.razorpayOrderId,
        prefill: {
          name: fullName.trim(),
          contact: phone.trim(),
          email: email.trim(),
        },
        theme: {
          color: "#df806c",
        },
        handler: async function (response) {
          try {
            // 3. Verify payment signature on backend
            const bookingResult = await workshopsApi.verifyWorkshopPayment(workshop.id, {
              transactionId: order.transactionId,
              razorpayOrderId: response.razorpay_order_id,
              razorpayPaymentId: response.razorpay_payment_id,
              razorpaySignature: response.razorpay_signature,
            });

            setConfirmedBooking(bookingResult);
            setIsPassModalOpen(true);
          } catch (verErr) {
            console.error("Verification error:", verErr);
            setErrorMsg(verErr?.data?.message || "Payment verification failed. Please contact support with Payment ID.");
          } finally {
            setIsSubmitting(false);
          }
        },
        modal: {
          ondismiss: function () {
            setIsSubmitting(false);
          },
        },
      };

      const rzp = new window.Razorpay(options);
      rzp.on("payment.failed", function (resp) {
        console.error("Razorpay failure:", resp.error);
        setErrorMsg(`Payment failed: ${resp.error?.description || "Transaction declined"}`);
        setIsSubmitting(false);
      });
      rzp.open();
    } catch (err) {
      console.error("Order creation error:", err);
      setErrorMsg(err?.data?.message || err?.message || "Failed to start checkout.");
      setIsSubmitting(false);
    }
  };

  if (notFound) {
    return (
      <div className="workshop-checkout-page" style={{ textAlign: "center", padding: "120px 24px" }}>
        <h2 style={{ color: "#18181b", marginBottom: "12px", fontSize: "24px" }}>
          No workshops are currently available.
        </h2>
        <p style={{ color: "#71717a", marginBottom: "24px", fontSize: "15px" }}>
          Please check back soon.
        </p>
        <button
          type="button"
          onClick={() => navigate("/workshops")}
          style={{
            background: "#df806c",
            color: "#ffffff",
            border: 0,
            padding: "12px 28px",
            borderRadius: "10px",
            cursor: "pointer",
            fontWeight: 600,
            fontSize: "14px",
          }}
        >
          View Workshops
        </button>
      </div>
    );
  }

  if (!workshop) {
    return <div className="checkout-loading">Preparing checkout...</div>;
  }

  const formatDate = (iso) => {
    if (!iso) return "Saturday, September 19, 2026";
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

  return (
    <div className="workshop-checkout-page">
      <div className="checkout-nav-bar">
        <button 
          type="button" 
          className="checkout-back-link"
          onClick={() => navigate(-1)}
        >
          <ArrowLeft size={16} /> Back
        </button>
        <span className="checkout-header-title">Checkout</span>
      </div>

      <div className="checkout-container">
        {/* CARD 1: WORKSHOP TITLE & THUMBNAIL */}
        <div className="checkout-card top-event-card">
          <img 
            src={workshop.imageUrl || fallbackImage} 
            alt={workshop.title} 
            className="checkout-thumb"
          />
          <div className="top-event-info">
            <h2 className="top-event-title">{workshop.title}</h2>
            <span className="top-event-host">{workshop.trainerName || "Ethos Dance Studio"}</span>
          </div>
        </div>

        {/* CARD 2: BOOKING SUMMARY */}
        <div className="checkout-card">
          <h3 className="card-heading">Booking Summary</h3>

          <div className="summary-info-item">
            <MapPin size={16} className="summary-icon" />
            <span>{workshop.venue}</span>
          </div>

          <div className="summary-info-item">
            <Calendar size={16} className="summary-icon" />
            <span>{formatDate(workshop.workshopDate)} • {workshop.startTime ? workshop.startTime.slice(0, 5) : "5:00 PM"} - {workshop.endTime ? workshop.endTime.slice(0, 5) : "9:00 PM"}</span>
          </div>

          {/* SPLIT TIER BREAKDOWN IF APPLICABLE */}
          {quote?.isSplitTier ? (
            <div className="split-summary-box">
              <div className="split-summary-label">
                <AlertCircle size={14} /> Split Pricing Breakdown:
              </div>
              {quote.breakdown.map((item, idx) => (
                <div key={idx} className="summary-ticket-row split-item">
                  <span className="ticket-pill-black">{item.tierName} {item.quantity}x</span>
                  <span className="ticket-row-price">₹{item.subtotal}</span>
                </div>
              ))}
            </div>
          ) : (
            <div className="summary-ticket-row">
              <span className="ticket-pill-black">
                {pricing?.currentTierName || "Regular"} {quantity}x
              </span>
              <span className="ticket-row-price">₹{baseTotal}</span>
            </div>
          )}
        </div>

        {/* CARD 3: CONTACT DETAILS */}
        <div className="checkout-card">
          <h3 className="card-heading">Contact Details</h3>

          <div className="form-group">
            <label className="form-label">FULL NAME *</label>
            <input
              type="text"
              className="form-input"
              value={fullName}
              onChange={(e) => handleFullNameChange(e.target.value)}
              placeholder="e.g. Shanmuka Gaddam"
              required
            />
          </div>

          <div className="form-group">
            <label className="form-label">WHATSAPP NUMBER *</label>
            <div className="phone-input-wrap">
              <span className="phone-prefix">+91</span>
              <input
                type="tel"
                className="form-input phone-field"
                value={phone}
                onChange={(e) => handlePhoneChange(e.target.value)}
                placeholder="9876543210"
                required
              />
            </div>
          </div>

          <div className="form-group">
            <label className="form-label">EMAIL *</label>
            <input
              type="email"
              className="form-input"
              value={email}
              onChange={(e) => handleEmailChange(e.target.value)}
              placeholder="youremail@example.com"
              required
            />
          </div>

          <div className="communication-notice-box">
            <strong>Important:</strong> Please use the correct mobile number and email address, as booking and event-related communication will be done through these channels.
          </div>

          <label className="remember-me-checkbox">
            <input
              type="checkbox"
              checked={rememberMe}
              onChange={(e) => handleRememberMe(e.target.checked)}
            />
            <div className="checkbox-content">
              <strong>Remember Me</strong>
              <span>Save contact details on this device for future bookings.</span>
            </div>
          </label>
        </div>

        {errorMsg && (
          <div className="checkout-error-banner">
            <AlertCircle size={16} /> {errorMsg}
          </div>
        )}
      </div>

      {/* STICKY BOTTOM BAR */}
      <div className="checkout-sticky-footer">
        <div className="sticky-footer-inner">
          <div className="footer-price-col">
            <button
              type="button"
              className="toggle-breakdown-btn"
              onClick={() => setShowBreakdown((prev) => !prev)}
            >
              View details {showBreakdown ? <ChevronDown size={14} /> : <ChevronUp size={14} />}
            </button>
            <div className="footer-total-price">
              ₹{Number(netTotal).toLocaleString("en-IN")} <span>{quantity} {quantity === 1 ? "ticket" : "tickets"}</span>
            </div>
          </div>

          <button
            type="button"
            className="checkout-pay-btn"
            disabled={isSubmitting || !fullName.trim() || !phone.trim() || !email.trim()}
            onClick={handlePayment}
          >
            {isSubmitting
              ? "Processing..."
              : `Pay ₹${Number(netTotal).toLocaleString("en-IN")}`}
          </button>
        </div>

        {showBreakdown && (
          <div className="breakdown-slideup">
            <div className="breakdown-row">
              <span>Ticket Total ({quantity}x)</span>
              <span>₹{Number(baseTotal).toLocaleString("en-IN")}</span>
            </div>
            <div className="breakdown-row" style={{ color: "#16a34a" }}>
              <span>Taxes & Platform Fees</span>
              <span>Included (₹0)</span>
            </div>
            <div className="breakdown-row breakdown-total">
              <span>Final Payable</span>
              <span>₹{Number(netTotal).toLocaleString("en-IN")}</span>
            </div>
          </div>
        )}
      </div>

      {/* CONFIRMATION PASS MODAL WITH QR CODE */}
      {confirmedBooking && (
        <WorkshopPassModal
          isOpen={isPassModalOpen}
          booking={confirmedBooking}
          student={{ fullName, phone, email, customerCode: confirmedBooking.bookingReference }}
          onClose={() => {
            setIsPassModalOpen(false);
            navigate("/workshops");
          }}
        />
      )}
    </div>
  );
}
