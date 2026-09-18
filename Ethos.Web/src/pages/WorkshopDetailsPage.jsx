import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { 
  Calendar, Clock, MapPin, Share2, ChevronDown, ChevronUp, 
  ShieldCheck, HelpCircle, FileText, CheckCircle2, AlertCircle, ArrowLeft 
} from "lucide-react";
import { workshopsApi } from "../services/workshopsApi";
import { createSlug } from "../utils/createSlug";
import SelectTicketsModal from "../components/workshop/SelectTicketsModal";
import "./WorkshopDetailsPage.css";

import fallbackImage from "../assets/workshops/workshop-01.jpg";
import workshop02 from "../assets/workshops/workshop-02.jpg";
import workshop03 from "../assets/workshops/workshop-03.jpg";
import workshop04 from "../assets/workshops/workshop-04.jpg";


export default function WorkshopDetailsPage() {
  const { slug } = useParams();
  const navigate = useNavigate();

  const [workshop, setWorkshop] = useState(null);
  const [pricing, setPricing] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [isTicketModalOpen, setIsTicketModalOpen] = useState(false);
  const [copiedToast, setCopiedToast] = useState(false);

  // Accordion state
  const [openSection, setOpenSection] = useState("about");

  const toggleSection = (section) => {
    setOpenSection((prev) => (prev === section ? "" : section));
  };

  useEffect(() => {
    let isMounted = true;
    window.scrollTo({ top: 0, behavior: "smooth" });

    async function loadWorkshop() {
      try {
        setLoading(true);
        setError("");

        let apiList = [];
        try {
          const response = await workshopsApi.getApprovedWorkshops();
          apiList = Array.isArray(response)
            ? response
            : response?.items || response?.data || [];
        } catch (e) {
          console.warn("Could not fetch API workshops:", e);
        }

        let matched = apiList.find(
          (item) =>
            createSlug(
              item.title ||
              item.name ||
              item.workshopName ||
              item.workshopTitle
            ) === slug ||
            String(item.id).toLowerCase() === String(slug).toLowerCase()
        );

        if (!matched) {
          try {
            matched = await workshopsApi.getWorkshopById(slug);
          } catch {
            matched = null;
          }
        }

        if (!matched) {
          if (isMounted) {
            setError("No workshops are currently available. Please check back soon.");
            setLoading(false);
          }
          return;
        }

        let details = matched;
        let livePricing = null;

        try {
          const [resDetails, resPricing] = await Promise.all([
            workshopsApi.getWorkshopById(matched.id).catch(() => null),
            workshopsApi.getWorkshopPricing(matched.id).catch(() => null),
          ]);
          if (resDetails) details = resDetails;
          if (resPricing) livePricing = resPricing;
        } catch {
          // preserve matched
        }

        if (isMounted) {
          setWorkshop(details);
          setPricing(
            livePricing || {
              currentPrice: details.startingPrice || details.price || 599,
              currentTier: 1,
              currentTierName: "Early pricing",
              ticketsSold: 0,
              tierCapacity: 10,
              ticketsFilledInTier: 0,
              ticketsRemainingInTier: 10,
              nextPrice: (details.startingPrice || details.price || 599) + 100,
              progressPercentage: 0,
              capacity: details.capacity || 50,
              remainingSeats: details.capacity || 50,
              tiers: []
            }
          );
        }
      } catch (requestError) {
        console.error("Failed to load workshop:", requestError);
        if (isMounted) {
          setError("No workshops are currently available. Please check back soon.");
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    loadWorkshop();

    return () => {
      isMounted = false;
    };
  }, [slug]);

  const handleShare = async () => {
    if (navigator.share) {
      try {
        await navigator.share({
          title: workshop?.title,
          text: `Join me at ${workshop?.title} at Ethos Dance Studio!`,
          url: window.location.href,
        });
      } catch (err) {
        // cancelled
      }
    } else {
      navigator.clipboard.writeText(window.location.href);
      setCopiedToast(true);
      setTimeout(() => setCopiedToast(false), 2500);
    }
  };

  const handleContinueToCheckout = ({ quantity, totalPayable, quote }) => {
    setIsTicketModalOpen(false);
    const targetSlug = createSlug(workshop?.title || workshop?.name || slug);
    navigate(`/workshops/${targetSlug}/checkout`, {
      state: {
        workshop,
        pricing,
        quantity,
        totalPayable,
        quote,
      },
    });
  };

  if (loading) {
    return (
      <div className="workshop-details-loading">
        <div className="loading-spinner" />
        <p>Loading experience details...</p>
      </div>
    );
  }

  if (error || !workshop) {
    return (
      <section className="workshop-error" style={{ textAlign: "center", padding: "120px 24px" }}>
        <h2 style={{ color: "#ffffff", marginBottom: "12px", fontSize: "24px" }}>
          {error || "No workshops are currently available."}
        </h2>
        <p style={{ color: "#a1a1aa", marginBottom: "24px", fontSize: "15px" }}>
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
          Back to Workshops
        </button>
      </section>
    );
  }

  const displayPrice = pricing?.currentPrice || workshop?.price || 299;
  const filledTickets = pricing?.ticketsFilledInTier ?? 6;
  const tierCapacity = pricing?.tierCapacity ?? 10;
  const remainingInTier = pricing?.ticketsRemainingInTier ?? 4;
  const progressPct = pricing?.progressPercentage ?? 60;
  const currentTier = pricing?.currentTier ?? 1;

  const isCompleted = (() => {
    if (!workshop) return false;
    const now = new Date();
    if (workshop.endUtc) {
      const d = new Date(workshop.endUtc);
      if (!isNaN(d.getTime())) return d <= now;
    }
    const datePart = workshop.workshopDate ? workshop.workshopDate.split("T")[0] : "";
    const timePart = workshop.endTime || "23:59:59";
    const d = new Date(`${datePart}T${timePart}`);
    if (!isNaN(d.getTime())) return d <= now;
    return false;
  })();

  const formatDate = (iso) => {
    if (!iso) return "Saturday, Sep 19, 2026";
    try {
      return new Date(iso).toLocaleDateString("en-IN", {
        weekday: "short",
        day: "numeric",
        month: "short",
        year: "numeric",
      });
    } catch {
      return iso;
    }
  };

  const formatTime = (timeStr) => {
    if (!timeStr) return "5:00 PM";
    const [h, m] = timeStr.split(":");
    const hours = parseInt(h, 10);
    const ampm = hours >= 12 ? "PM" : "AM";
    const formatted = hours % 12 || 12;
    return `${formatted}:${m} ${ampm}`;
  };

  return (
    <div className="workshop-details-page">
      {/* AMBIENT HERO BANNER */}
      <div className="workshop-hero-banner">
        <div 
          className="workshop-hero-backdrop" 
          style={{ backgroundImage: `url(${workshop.imageUrl || fallbackImage})` }}
        />
        <div className="workshop-hero-overlay" />

        <div className="workshop-hero-content-wrap">
          <button 
            type="button" 
            className="workshop-back-btn"
            onClick={() => navigate("/workshops")}
          >
            <ArrowLeft size={16} /> Back to Workshops
          </button>

          <div className="workshop-hero-card">
            <img 
              src={workshop.imageUrl || fallbackImage} 
              alt={workshop.title} 
              className="workshop-hero-img" 
            />
          </div>
        </div>
      </div>

      {/* MAIN CONTENT & PURCHASE WIDGET GRID */}
      <div className="workshop-details-container">
        <div className="workshop-details-layout">
          {/* LEFT: CONTENT & ACCORDIONS */}
          <div className="workshop-content-col">
            <span className="workshop-eyebrow">COMMUNITY EXPERIENCE</span>
            <h1 className="workshop-title">{workshop.title}</h1>

            <div className="workshop-tags-bar">
              <span className="badge-featured">✦ ETHOS ORIGINAL</span>
              <span className="badge-tag">{workshop.danceStyle || "MUSIC"}</span>
              <span className="badge-tag">{workshop.level || "ALL LEVELS"}</span>
              <span className="badge-tag">FAMILY FRIENDLY</span>
            </div>

            <div className="workshop-host-info">
              <span>Hosted By <strong>{workshop.trainerName || "Ethos Dance Studio"}</strong></span>
            </div>

            {/* ACCORDION 1: ABOUT */}
            <div className="workshop-accordion-item">
              <button 
                type="button" 
                className="accordion-trigger"
                onClick={() => toggleSection("about")}
              >
                <span>ABOUT</span>
                {openSection === "about" ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </button>
              {openSection === "about" && (
                <div className="accordion-content">
                  <p className="lead-description">{workshop.description}</p>
                  <p>
                    Come sing your heart out. Micless brings together music lovers for one unforgettable 
                    session of authentic rhythm and melody. No spectators here, only participants. 
                    Grab your tickets and be part of the loudest, most joyful community in the city.
                  </p>
                </div>
              )}
            </div>

            {/* ACCORDION 2: SUPPORT */}
            <div className="workshop-accordion-item">
              <button 
                type="button" 
                className="accordion-trigger"
                onClick={() => toggleSection("support")}
              >
                <span>SUPPORT</span>
                {openSection === "support" ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </button>
              {openSection === "support" && (
                <div className="accordion-content">
                  <p>Need help with your booking or venue directions?</p>
                  <div className="support-links">
                    <a href="https://wa.me/918466021834" target="_blank" rel="noopener noreferrer" className="support-chip">
                      💬 WhatsApp Support (+91 8466021834)
                    </a>
                    <a href="mailto:ethosdancestudio@gmail.com" className="support-chip">
                      ✉️ Email: ethosdancestudio@gmail.com
                    </a>
                  </div>
                </div>
              )}
            </div>

            {/* ACCORDION 3: TERMS AND CONDITIONS */}
            <div className="workshop-accordion-item">
              <button 
                type="button" 
                className="accordion-trigger"
                onClick={() => toggleSection("terms")}
              >
                <span>TERMS AND CONDITIONS</span>
                {openSection === "terms" ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </button>
              {openSection === "terms" && (
                <div className="accordion-content">
                  <ul className="terms-list">
                    <li>For any reason if the event is cancelled from our end, 100% of the workshop ticket amount will be refunded.</li>
                    <li>For any reason, if you are unable to make it to the event after registration, the amount will not be refunded or carried forward.</li>
                    <li>Spot registrations are strictly subject to availability. Online booking is mandatory to secure entry.</li>
                  </ul>
                </div>
              )}
            </div>

            {/* ACCORDION 4: STUDIO RULES & CONDUCT */}
            <div className="workshop-accordion-item">
              <button 
                type="button" 
                className="accordion-trigger"
                onClick={() => toggleSection("rules")}
              >
                <span>STUDIO CODE & CONDUCT</span>
                {openSection === "rules" ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </button>
              {openSection === "rules" && (
                <div className="accordion-content">
                  <p><strong>Respect the Space & People:</strong> Treat the venue, hosts, fellow participants, and staff with courtesy at all times.</p>
                  <p><strong>Sobriety & Conduct:</strong> Please arrive sober and in good spirit. Entry will not be permitted under the influence or disruptive behavior.</p>
                </div>
              )}
            </div>
          </div>

          {/* RIGHT: STICKY PURCHASE WIDGET */}
          <div className="workshop-sidebar-col">
            <div className="sticky-booking-card">
              <div className="booking-card-price-row">
                <div>
                  <span className="price-label">Starting from</span>
                  <div className="price-figure">
                    ₹{displayPrice} <span>onwards</span>
                  </div>
                </div>
                <button 
                  type="button" 
                  className="share-icon-btn" 
                  onClick={handleShare}
                  aria-label="Share Workshop"
                >
                  <Share2 size={18} />
                </button>
              </div>

              {/* DYNAMIC 4-TIER PROGRESS DISPLAY */}
              <div className="tier-progress-card">
                <div className="tier-badge-row">
                  <span className="tier-pill-name">
                    {currentTier === 1 ? "Early pricing" : currentTier === 4 ? "Final pricing tier" : `Tier ${currentTier} Pricing`}
                  </span>
                  <span className="tier-pill-price">₹{displayPrice} / ticket</span>
                </div>

                <div className="tier-progress-fill-text">
                  <span>{filledTickets} of {tierCapacity} tickets filled</span>
                  <strong>Only {remainingInTier} tickets left at this price</strong>
                </div>

                <div className="tier-progress-track">
                  <div 
                    className="tier-progress-bar" 
                    style={{ width: `${progressPct}%` }}
                  />
                </div>
                <div className="tier-progress-pct">{progressPct}% sold in current tier</div>
              </div>

              {/* DATE & TIME CARD */}
              <div className="booking-info-box">
                <Calendar size={18} className="box-icon" />
                <div className="box-content">
                  <strong>{formatDate(workshop.workshopDate)}</strong>
                  <span>{formatTime(workshop.startTime)} - {formatTime(workshop.endTime)}</span>
                </div>
              </div>

              {/* VENUE CARD */}
              <div className="booking-info-box">
                <MapPin size={18} className="box-icon" />
                <div className="box-content">
                  <strong>Ethos Studio / Venue</strong>
                  <span>{workshop.venue}</span>
                </div>
              </div>

              {/* BOOK NOW PRIMARY ACTION OR COMPLETED STATE */}
              {isCompleted ? (
                <button
                  type="button"
                  className="primary-book-now-btn is-completed"
                  disabled
                  style={{
                    background: "rgba(255, 255, 255, 0.08)",
                    color: "#a1a1aa",
                    border: "1px solid rgba(255, 255, 255, 0.15)",
                    cursor: "not-allowed",
                    boxShadow: "none",
                  }}
                >
                  ✓ Workshop Completed
                </button>
              ) : (
                <button
                  type="button"
                  className="primary-book-now-btn"
                  onClick={() => setIsTicketModalOpen(true)}
                >
                  Book Now
                </button>
              )}

              <div className="booking-guarantee-note">
                {isCompleted ? (
                  <span>This session has concluded. Check our calendar for upcoming masterclasses!</span>
                ) : (
                  <>
                    <ShieldCheck size={14} /> Instant digital QR pass on booking confirmation
                  </>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* SELECT TICKETS MODAL (IMAGE 3) */}
      <SelectTicketsModal
        isOpen={isTicketModalOpen}
        onClose={() => setIsTicketModalOpen(false)}
        workshop={workshop}
        pricing={pricing}
        onContinue={handleContinueToCheckout}
      />

      {copiedToast && (
        <div className="copied-toast">
          ✓ Link copied to clipboard!
        </div>
      )}
    </div>
  );
}
