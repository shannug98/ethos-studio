import React, { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { 
  Calendar, Clock, MapPin, Share2, ChevronDown, ChevronUp, ChevronLeft, ChevronRight,
  ShieldCheck, HelpCircle, FileText, CheckCircle2, AlertCircle, ArrowLeft,
  Users, Check, Sparkles
} from "lucide-react";
import { workshopsApi } from "../services/workshopsApi";
import { createSlug } from "../utils/createSlug";
import { getTrainerPhotoUrl, handleTrainerImgError, getTrainerDisplayName, DEFAULT_AVATAR_PLACEHOLDER, ETHOS_MEDIA_FALLBACK_SVG } from "../utils/mediaUrl";
import TrainerAvatar from "../components/common/TrainerAvatar";
import { getWorkshopTimingDisplay, getChronologicalGroupedSessions, getWorkshopBannerImage, getPassScopeLabel } from "../utils/workshopPresentation";
import { trackWorkshopView } from "../services/analytics";
import SelectTicketsModal from "../components/workshop/SelectTicketsModal";
import WorkshopCardMedia from "../components/workshop/WorkshopCardMedia";
import FacultyProfileCard from "../components/workshop/FacultyProfileCard";
import "../styles/workshop-card-meta.css";
import "./WorkshopDetailsPage.css";

const fallbackImage = ETHOS_MEDIA_FALLBACK_SVG;

function getRelatedUpcomingWorkshops(apiList, currentWorkshop, currentSlug) {
  if (!Array.isArray(apiList) || apiList.length === 0) return [];
  const currentId = String(currentWorkshop?.id || "").trim().toLowerCase();
  const normalizedSlug = String(currentSlug || "").trim().toLowerCase();
  const now = new Date();

  return apiList
    .filter((item) => {
      if (!item) return false;
      const itemId = String(item.id || "").trim().toLowerCase();
      if (currentId && itemId === currentId) return false;

      const itemSlug = createSlug(item.title || item.name || item.workshopName || item.id || "").toLowerCase();
      if (normalizedSlug && itemSlug === normalizedSlug) return false;

      if (item.status) {
        const st = String(item.status).toLowerCase();
        if (st === "draft" || st === "cancelled" || st === "canceled" || st === "archived") {
          return false;
        }
      }

      let isCompleted = false;
      if (item.endUtc) {
        const d = new Date(item.endUtc);
        if (!isNaN(d.getTime())) isCompleted = d <= now;
      } else {
        const datePart = item.workshopDate ? item.workshopDate.split("T")[0] : "";
        const timePart = item.endTime || "23:59:59";
        const d = new Date(`${datePart}T${timePart}`);
        if (!isNaN(d.getTime())) isCompleted = d <= now;
      }
      if (isCompleted) return false;

      return true;
    })
    .sort((a, b) => {
      const dateA = new Date(a.workshopDate || a.startUtc || 0).getTime();
      const dateB = new Date(b.workshopDate || b.startUtc || 0).getTime();
      return dateA - dateB;
    });
}


export default function WorkshopDetailsPage() {
  const { slug } = useParams();
  const navigate = useNavigate();

  const [workshop, setWorkshop] = useState(null);
  const [pricing, setPricing] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [isTicketModalOpen, setIsTicketModalOpen] = useState(false);
  const [selectedPassTypeId, setSelectedPassTypeId] = useState(null);
  const [copiedToast, setCopiedToast] = useState(false);
  const [otherWorkshops, setOtherWorkshops] = useState([]);
  const [allApprovedWorkshops, setAllApprovedWorkshops] = useState([]);
  const moreWorkshopsTrackRef = useRef(null);

  const handleScrollMore = (direction) => {
    if (moreWorkshopsTrackRef.current) {
      moreWorkshopsTrackRef.current.scrollBy({
        left: direction * 340,
        behavior: "smooth",
      });
    }
  };

  const handleBack = () => {
    if (window.history?.state?.idx > 0 || window.history?.length > 1) {
      navigate(-1);
    } else {
      navigate("/workshops");
    }
  };

  // Accordion state: Primary sections (schedule, about) open by default
  const [openSections, setOpenSections] = useState({
    schedule: true,
    about: true,
    support: false,
    terms: false,
    rules: false,
  });

  const toggleSection = (section) => {
    setOpenSections((prev) => ({ ...prev, [section]: !prev[section] }));
  };

  const [showStickyBar, setShowStickyBar] = useState(false);
  const bookingCardRef = useRef(null);

  useEffect(() => {
    const handleScroll = () => {
      if (!bookingCardRef.current) return;
      const rect = bookingCardRef.current.getBoundingClientRect();
      if (window.innerWidth <= 960) {
        setShowStickyBar(rect.bottom < 80);
      } else {
        setShowStickyBar(false);
      }
    };

    window.addEventListener("scroll", handleScroll, { passive: true });
    window.addEventListener("resize", handleScroll, { passive: true });
    handleScroll();

    return () => {
      window.removeEventListener("scroll", handleScroll);
      window.removeEventListener("resize", handleScroll);
    };
  }, []);

  useEffect(() => {
    if (showStickyBar) {
      document.body.classList.add("has-sticky-booking-bar");
    } else {
      document.body.classList.remove("has-sticky-booking-bar");
    }
    return () => {
      document.body.classList.remove("has-sticky-booking-bar");
    };
  }, [showStickyBar]);

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
          if (isMounted) {
            setAllApprovedWorkshops(apiList);
          }
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

        if (isMounted) {
          setWorkshop(matched);
          const others = getRelatedUpcomingWorkshops(apiList, matched, slug);
          setOtherWorkshops(others);
          setPricing({
            currentPrice: matched.startingPrice || matched.price || 599,
            currentTier: 1,
            currentTierName: "Early pricing",
            ticketsSold: 0,
            tierCapacity: 10,
            ticketsFilledInTier: 0,
            ticketsRemainingInTier: 10,
            nextPrice: (matched.startingPrice || matched.price || 599) + 100,
            progressPercentage: 0,
            capacity: matched.capacity || 50,
            remainingSeats: matched.capacity || 50,
            tiers: []
          });
          setLoading(false);

          // Track product analytics event
          if (matched.id) {
            trackWorkshopView(matched.id, {
              title: matched.title || matched.workshopName || "",
              price: String(matched.startingPrice || matched.price || 0),
            });
          }
        }

        // Background update for enriched details and live dynamic pricing
        try {
          const [resDetails, resPricing] = await Promise.all([
            workshopsApi.getWorkshopById(matched.id).catch(() => null),
            workshopsApi.getWorkshopPricing(matched.id).catch(() => null),
          ]);
          if (isMounted) {
            if (resDetails) setWorkshop(resDetails);
            if (resPricing) setPricing(resPricing);
          }
        } catch {
          // preserve matched
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

  const retryLoadWorkshop = async () => {
    setLoading(true);
    setError("");
    try {
      let apiList = [];
      try {
        const response = await workshopsApi.getApprovedWorkshops();
        apiList = Array.isArray(response)
          ? response
          : response?.items || response?.data || [];
        setAllApprovedWorkshops(apiList);
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
        setError("Workshop not found or currently unavailable.");
        setLoading(false);
        return;
      }

      setWorkshop(matched);
      const others = getRelatedUpcomingWorkshops(apiList, matched, slug);
      setOtherWorkshops(others);
      setPricing({
        currentPrice: matched.startingPrice || matched.price || 599,
        currentTier: 1,
        currentTierName: "Early pricing",
        ticketsSold: 0,
        tierCapacity: 10,
        ticketsFilledInTier: 0,
        ticketsRemainingInTier: 10,
        nextPrice: (matched.startingPrice || matched.price || 599) + 100,
        progressPercentage: 0,
        capacity: matched.capacity || 50,
        remainingSeats: matched.capacity || 50,
        tiers: []
      });
      setLoading(false);

      try {
        const [resDetails, resPricing] = await Promise.all([
          workshopsApi.getWorkshopById(matched.id).catch(() => null),
          workshopsApi.getWorkshopPricing(matched.id).catch(() => null),
        ]);
        if (resDetails) setWorkshop(resDetails);
        if (resPricing) setPricing(resPricing);
      } catch {
        // preserve matched
      }
    } catch (requestError) {
      console.error("Failed to retry workshop:", requestError);
      setError("Failed to load workshop. Please try again.");
    } finally {
      setLoading(false);
    }
  };

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

  const handleShareOtherWorkshop = async (e, other) => {
    e.stopPropagation();
    const otherSlug = createSlug(other.title || other.name || other.id);
    const otherUrl = `${window.location.origin}/workshops/${otherSlug}`;
    if (navigator.share) {
      try {
        await navigator.share({
          title: other.title,
          text: `Check out ${other.title} at Ethos Dance Studio!`,
          url: otherUrl,
        });
      } catch {
        // user cancelled share
      }
    } else {
      navigator.clipboard.writeText(otherUrl);
      setCopiedToast(true);
      setTimeout(() => setCopiedToast(false), 2500);
    }
  };

  const handleContinueToCheckout = (checkoutData) => {
    setIsTicketModalOpen(false);
    const targetSlug = createSlug(workshop?.title || workshop?.name || slug);
    navigate(`/workshops/${targetSlug}/checkout`, {
      state: {
        workshop,
        pricing,
        ...checkoutData,
        checkoutAttemptId: crypto.randomUUID(),
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
          {error || "Workshop currently unavailable"}
        </h2>
        <p style={{ color: "#a1a1aa", marginBottom: "24px", fontSize: "15px" }}>
          Please check back soon or try reloading.
        </p>
        <div style={{ display: "flex", gap: "12px", justifyContent: "center", flexWrap: "wrap" }}>
          <button
            type="button"
            onClick={retryLoadWorkshop}
            style={{
              background: "#FF5500",
              color: "#ffffff",
              border: 0,
              padding: "12px 28px",
              borderRadius: "10px",
              cursor: "pointer",
              fontWeight: 600,
              fontSize: "14px",
            }}
          >
            Try Again
          </button>
          <button
            type="button"
            onClick={() => navigate("/workshops")}
            style={{
              background: "transparent",
              color: "#ffffff",
              border: "1px solid rgba(255, 255, 255, 0.2)",
              padding: "12px 28px",
              borderRadius: "10px",
              cursor: "pointer",
              fontWeight: 600,
              fontSize: "14px",
            }}
          >
            Back to Workshops
          </button>
        </div>
      </section>
    );
  }

  const hasPasses = Array.isArray(workshop?.passTypes) && workshop.passTypes.length > 0;
  const hasSessions = Array.isArray(workshop?.sessions) && workshop.sessions.length > 0;
  const hasMultipleTrainers = Array.isArray(workshop?.trainers) && workshop.trainers.length > 0;

  const minPassPrice = hasPasses
    ? Math.min(...workshop.passTypes.map((p) => p.currentPrice ?? p.price))
    : pricing?.currentPrice || workshop?.price || 299;
  const displayPrice = hasPasses ? minPassPrice : (pricing?.currentPrice || workshop?.price || 299);

  const getPassSummaryLabel = () => {
    if (!hasPasses) return null;
    if (workshop.passTypes.length === 1) {
      const scope = getPassScopeLabel(workshop.passTypes[0].sessionsIncluded);
      return scope.toLowerCase().includes("pass") ? scope : `${scope} Pass`;
    }
    const uniqueScopes = [...new Set(workshop.passTypes.map((p) => getPassScopeLabel(p.sessionsIncluded)))];
    return `${workshop.passTypes.length} Pass Options • ${uniqueScopes.join(" • ")}`;
  };

  const uniqueDates = hasSessions
    ? Array.from(new Set(workshop.sessions.map((s) => s.sessionDate ? s.sessionDate.split("T")[0] : ""))).filter(Boolean).sort()
    : [];

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

  const isBookingClosed = Boolean(
    workshop?.isBookingClosed &&
    (!Array.isArray(workshop?.sessions) || workshop.sessions.length === 0 || workshop.sessions.every((s) => s.isBookingClosed))
  );

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
      {(() => {
        const rawCover = workshop.landscapeImageUrl || workshop.imageUrl;
        const safeHeroSrc = (!rawCover || (typeof rawCover === "string" && rawCover.includes("media.ethosdancestudio.com")))
          ? fallbackImage
          : rawCover;
        return (
          <div className="workshop-hero-banner">
            <div 
              className="workshop-hero-backdrop" 
              style={{ backgroundImage: `url(${safeHeroSrc})` }}
            />
            <div className="workshop-hero-overlay" />

            <div className="workshop-hero-content-wrap">
              <button 
                type="button" 
                className="workshop-back-btn"
                onClick={handleBack}
                aria-label="Go back"
              >
                <ArrowLeft size={15} /> Back
              </button>

              <div className="workshop-hero-card">
                <div 
                  className="workshop-hero-card-backdrop" 
                  style={{ backgroundImage: `url(${safeHeroSrc})` }} 
                />
                <img 
                  src={safeHeroSrc} 
                  alt={workshop.title} 
                  className="workshop-hero-img" 
                  loading="lazy"
                  decoding="async"
                  onError={(e) => {
                    e.currentTarget.onerror = null;
                    e.currentTarget.src = fallbackImage;
                  }}
                />
              </div>
            </div>
          </div>
        );
      })()}

      {/* MAIN CONTENT & PURCHASE WIDGET GRID */}
      <div className="workshop-details-container">
        <div className="workshop-details-layout">
          {/* LEFT: CONTENT & ACCORDIONS */}
          <div className="workshop-content-col">
            <div className="workshop-header-meta">
              <span className="workshop-eyebrow">WORKSHOP</span>
              <h1 className="workshop-title">{workshop.title}</h1>

              <div className="workshop-tags-bar">
                {workshop.isEthosOriginal && (
                  <span className="badge-featured">✦ ETHOS ORIGINAL</span>
                )}
                <span className="badge-tag">{workshop.danceStyle || "MUSIC"}</span>
                <span className="badge-tag">{workshop.level || "ALL LEVELS"}</span>
              </div>
            </div>

            {/* FACULTY SECTION */}
            <div className="details-trainers-section">
              <div className="trainers-section-label">
                <Users size={15} /> FACULTY
              </div>
              <div className="trainers-cards-row">
                {(Array.isArray(workshop.trainers) && workshop.trainers.length > 0
                  ? workshop.trainers
                  : [{
                      trainerProfileId: workshop.trainerProfileId,
                      name: workshop.trainerName,
                      danceStyles: workshop.danceStyle,
                    }]
                ).map((tr) => (
                  <FacultyProfileCard
                    key={tr.trainerProfileId || tr.id || tr.name}
                    trainer={tr}
                    allWorkshops={allApprovedWorkshops}
                    currentWorkshopId={workshop.id}
                  />
                ))}
              </div>
            </div>

            {/* ACCORDION: SCHEDULE & SESSIONS (IF SESSIONS CONFIGURED) */}
            {hasSessions && (
              <div className="workshop-accordion-item schedule-accordion">
                <button 
                  type="button" 
                  className="accordion-trigger"
                  onClick={() => toggleSection("schedule")}
                >
                  <span style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                    <Calendar size={16} color="#df806c" /> SCHEDULE & TIMETABLE ({workshop.sessions.length} Sessions)
                  </span>
                  {openSections.schedule ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
                </button>
                {openSections.schedule && (
                  <div className="accordion-content schedule-accordion-content">
                    {getChronologicalGroupedSessions(workshop.sessions, workshop.workshopDate).map(([dateKey, sessionsOnDate]) => {
                      return (
                        <div key={dateKey} className="schedule-date-group">
                          <h4 className="schedule-date-header">
                            <Calendar size={15} /> {formatDate(dateKey)}
                          </h4>
                          <div className="schedule-sessions-cards">
                            {sessionsOnDate.map((sess) => {
                              const trainerObj = workshop.trainers?.find(
                                (t) => (t.trainerProfileId || t.id) === sess.trainerProfileId
                              );
                              const trainerPhoto = getTrainerPhotoUrl(trainerObj);

                              return (
                                <div key={sess.id} className="schedule-session-card">
                                  <div className="schedule-session-time">
                                    <Clock size={13} /> {formatTime(sess.startTime)} - {formatTime(sess.endTime)}
                                  </div>
                                  <div className="schedule-session-main">
                                    <h5 className="schedule-session-title">{sess.title}</h5>
                                    {sess.description && <p className="schedule-session-desc">{sess.description}</p>}
                                    <div className="schedule-session-trainer">
                                      <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                                        <TrainerAvatar
                                          trainer={trainerObj}
                                          name={getTrainerDisplayName(sess.trainerName || trainerObj)}
                                          size={20}
                                        />
                                        <span className="trainer-tag">
                                          Instructor: <strong>{getTrainerDisplayName(sess.trainerName || trainerObj)}</strong>
                                        </span>
                                      </div>
                                      {sess.isBookingClosed ? (
                                        <span className="session-tag closed">Closed</span>
                                      ) : sess.remainingSeats <= 0 ? (
                                        <span className="session-tag sold-out">Full</span>
                                      ) : (
                                        <span className="session-tag open">{sess.remainingSeats} seats available</span>
                                      )}
                                    </div>
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
            )}

            {/* ACCORDION 3: ABOUT */}
            <div className="workshop-accordion-item">
              <button 
                type="button" 
                className="accordion-trigger"
                onClick={() => toggleSection("about")}
              >
                <span>ABOUT</span>
                {openSections.about ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </button>
              {openSections.about && (
                <div className="accordion-content">
                  <p className="lead-description">{workshop.description}</p>
                  <p>
                    Come dance and connect with our world-class faculty. Ethos Dance Studio masterclasses bring together passion, rhythm, and technical refinement for an unforgettable experience. Grab your passes and train with the best.
                  </p>
                </div>
              )}
            </div>

            {/* ACCORDION 4: SUPPORT */}
            <div className="workshop-accordion-item">
              <button 
                type="button" 
                className="accordion-trigger"
                onClick={() => toggleSection("support")}
              >
                <span>SUPPORT</span>
                {openSections.support ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </button>
              {openSections.support && (
                <div className="accordion-content">
                  <p>Need help with your booking or venue directions?</p>
                  <div className="support-links">
                    <a href="https://wa.me/918341701113" target="_blank" rel="noopener noreferrer" className="support-chip">
                      💬 WhatsApp Support (+91 83417 01113)
                    </a>
                    <a href="mailto:ethosdancestudio@gmail.com" className="support-chip">
                      ✉️ Email: ethosdancestudio@gmail.com
                    </a>
                  </div>
                </div>
              )}
            </div>

            {/* ACCORDION 5: TERMS AND CONDITIONS */}
            <div className="workshop-accordion-item">
              <button 
                type="button" 
                className="accordion-trigger"
                onClick={() => toggleSection("terms")}
              >
                <span>TERMS AND CONDITIONS</span>
                {openSections.terms ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </button>
              {openSections.terms && (
                <div className="accordion-content">
                  <ul className="terms-list">
                    <li>For any reason if the event is cancelled from our end, 100% of the workshop ticket amount will be refunded.</li>
                    <li>For any reason, if you are unable to make it to the event after registration, the amount will not be refunded or carried forward.</li>
                    <li>Spot registrations are strictly subject to availability. Online booking is mandatory to secure entry.</li>
                  </ul>
                </div>
              )}
            </div>

            {/* ACCORDION 6: STUDIO RULES & CONDUCT */}
            <div className="workshop-accordion-item">
              <button 
                type="button" 
                className="accordion-trigger"
                onClick={() => toggleSection("rules")}
              >
                <span>STUDIO CODE & CONDUCT</span>
                {openSections.rules ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </button>
              {openSections.rules && (
                <div className="accordion-content">
                  <p><strong>Respect the Space & People:</strong> Treat the venue, hosts, fellow participants, and staff with courtesy at all times.</p>
                  <p><strong>Sobriety & Conduct:</strong> Please arrive sober and in good spirit. Entry will not be permitted under the influence or disruptive behavior.</p>
                </div>
              )}
            </div>
          </div>

          {/* RIGHT: STICKY PURCHASE WIDGET */}
          <div className="workshop-sidebar-col">
            <div className="sticky-booking-card" ref={bookingCardRef}>
              <div className="booking-card-price-row">
                <div>
                  <span className="price-label">Price</span>
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

              {/* DYNAMIC 4-TIER PROGRESS DISPLAY OR PASS CALLOUT */}
              {hasPasses ? (
                <div className="passes-summary-sidebar-banner">
                  <Sparkles size={16} className="pass-sparkle-icon" />
                  <span>{getPassSummaryLabel()}</span>
                </div>
              ) : (
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
              )}

              {/* DATE & TIME CARD */}
              <div className="booking-info-box">
                <Calendar size={18} className="box-icon" />
                <div className="box-content">
                  {hasSessions && uniqueDates.length > 1 ? (
                    <>
                      <strong>Multi-Day Workshop ({uniqueDates.length} Days)</strong>
                      <span>{getWorkshopTimingDisplay(workshop.sessions, workshop.startTime, workshop.endTime, formatTime)}</span>
                    </>
                  ) : (
                    <>
                      <strong>{formatDate(workshop.workshopDate)}</strong>
                      <span>{getWorkshopTimingDisplay(workshop.sessions, workshop.startTime, workshop.endTime, formatTime)}</span>
                    </>
                  )}
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
              ) : isBookingClosed ? (
                <button
                  type="button"
                  className="primary-book-now-btn is-closed"
                  disabled
                  style={{
                    background: "rgba(239, 68, 68, 0.12)",
                    color: "#f87171",
                    border: "1px solid rgba(239, 68, 68, 0.3)",
                    cursor: "not-allowed",
                    boxShadow: "none",
                  }}
                >
                  ✕ Bookings Closed
                </button>
              ) : (
                <button
                  type="button"
                  className="primary-book-now-btn"
                  onClick={() => {
                    setSelectedPassTypeId(null);
                    setIsTicketModalOpen(true);
                  }}
                >
                  {hasPasses ? "SELECT PASS & BOOK" : "Book Now"}
                </button>
              )}

              <div className="booking-guarantee-note">
                {isCompleted ? (
                  <span>This session has concluded. Check our calendar for upcoming masterclasses!</span>
                ) : isBookingClosed ? (
                  <span>Bookings for this workshop are closed as the booking cutoff time has passed.</span>
                ) : (
                  <>
                    <ShieldCheck size={14} /> Instant digital QR pass on booking confirmation
                  </>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* MORE WORKSHOPS HORIZONTAL CAROUSEL */}
        {otherWorkshops.length > 0 && (
          <section className="more-workshops-section" aria-label="More Workshops">
            <div className="more-workshops-header">
              <div className="more-workshops-header-text">
                <h2 className="more-workshops-title">More Workshops</h2>
                <span className="more-workshops-sub">Discover other upcoming dance experiences and masterclasses</span>
              </div>
              <div className="more-carousel-controls" aria-label="More workshops carousel controls">
                <button
                  type="button"
                  className="more-carousel-btn prev"
                  onClick={(e) => {
                    e.preventDefault();
                    handleScrollMore(-1);
                  }}
                  aria-label="Previous workshops"
                >
                  <ChevronLeft size={20} />
                </button>
                <button
                  type="button"
                  className="more-carousel-btn next"
                  onClick={(e) => {
                    e.preventDefault();
                    handleScrollMore(1);
                  }}
                  aria-label="Next workshops"
                >
                  <ChevronRight size={20} />
                </button>
              </div>
            </div>

            <div 
              className="more-workshops-track"
              ref={moreWorkshopsTrackRef}
              role="region"
              aria-label="More workshops carousel"
            >
              {otherWorkshops.map((other) => {
                const otherSlug = createSlug(other.title || other.name || other.id);
                const safeOtherImg = other.imageUrl || getWorkshopBannerImage(other) || fallbackImage;
                const otherDate = other.workshopDate ? formatDate(other.workshopDate) : "";
                const otherTime = other.startTime ? formatTime(other.startTime) : "";

                let otherStartingPrice = null;
                if (Array.isArray(other.passTypes) && other.passTypes.length > 0) {
                  const validPassPrices = other.passTypes
                    .map((p) => Number(p.currentPrice ?? p.price))
                    .filter((pr) => !isNaN(pr) && pr > 0);
                  if (validPassPrices.length > 0) {
                    otherStartingPrice = Math.min(...validPassPrices);
                  }
                }
                if (otherStartingPrice == null) {
                  const rawPrice = Number(other.startingPrice ?? other.currentPrice ?? other.price);
                  if (!isNaN(rawPrice) && rawPrice > 0) {
                    otherStartingPrice = rawPrice;
                  }
                }

                return (
                  <article 
                    key={other.id} 
                    className="more-workshop-card"
                    onClick={() => {
                      navigate(`/workshops/${otherSlug}`);
                      window.scrollTo({ top: 0, behavior: "smooth" });
                    }}
                  >
                    {/* ON/INSIDE IMAGE: Poster, OG (conditional), Date, Time, Location, Level */}
                    <WorkshopCardMedia
                      image={safeOtherImg}
                      title={other.title}
                      isEthosOriginal={other.isEthosOriginal === true}
                      date={otherDate}
                      time={otherTime}
                      venue={other.venue || "Ethos Dance Studio"}
                      level={other.level || "ALL LEVELS"}
                      aspectRatio="3/4"
                      showArrow={false}
                    />

                    {/* BELOW IMAGE: Dance Style + Workshop Name + Actions */}
                    <div className="workshop-card-content-below more-workshop-details">
                      <span className="workshop-card-style-eyebrow">{other.danceStyle || "WORKSHOP"}</span>
                      <h3 className="workshop-card-heading more-workshop-name" title={other.title}>
                        {other.title}
                      </h3>

                      <div className="more-workshop-card-footer" style={{ marginTop: "14px" }}>
                        <div className="more-card-price">
                          {otherStartingPrice != null ? (
                            <>
                              <span className="more-price-caption">Starting from</span>
                              <span className="more-price-value">₹{otherStartingPrice}</span>
                            </>
                          ) : (
                            <span className="more-price-caption" style={{ fontStyle: "italic", color: "#94a3b8" }}>Pricing TBA</span>
                          )}
                        </div>

                        <div className="more-card-actions">
                          <button
                            type="button"
                            className="more-card-book-btn"
                            onClick={(e) => {
                              e.stopPropagation();
                              navigate(`/workshops/${otherSlug}`);
                              window.scrollTo({ top: 0, behavior: "smooth" });
                            }}
                          >
                            Book Now
                          </button>
                          <button
                            type="button"
                            className="more-card-share-btn"
                            onClick={(e) => handleShareOtherWorkshop(e, other)}
                            aria-label={`Share ${other.title}`}
                          >
                            <Share2 size={16} />
                          </button>
                        </div>
                      </div>
                    </div>
                  </article>
                );
              })}
            </div>
          </section>
        )}
      </div>

      {/* MOBILE STICKY BOTTOM ACTION BAR (APPEARS ON MOBILE WHEN TOP CARD IS SCROLLED PAST) */}
      <div className={`mobile-sticky-booking-bar ${showStickyBar ? "visible" : ""}`}>
        <div className="mobile-sticky-bar-info">
          <span className="mobile-sticky-bar-label">{hasPasses ? "Passes from" : "Starting from"}</span>
          <div className="mobile-sticky-bar-price">
            ₹{displayPrice} <span>onwards</span>
          </div>
        </div>
        <button
          type="button"
          className="mobile-sticky-bar-btn"
          disabled={isCompleted || isBookingClosed}
          onClick={() => {
            setSelectedPassTypeId(null);
            setIsTicketModalOpen(true);
          }}
        >
          {isCompleted ? "Completed" : isBookingClosed ? "Bookings Closed" : hasPasses ? "SELECT PASS & BOOK ›" : "Book Now ›"}
        </button>
      </div>

      {/* SELECT TICKETS MODAL (IMAGE 3) */}
      <SelectTicketsModal
        isOpen={isTicketModalOpen}
        onClose={() => setIsTicketModalOpen(false)}
        workshop={workshop}
        pricing={pricing}
        initialPassTypeId={selectedPassTypeId}
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

