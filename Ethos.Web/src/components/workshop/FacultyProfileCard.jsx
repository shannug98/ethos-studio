import React, { useState, useRef, useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { X, Calendar, ChevronLeft, ChevronRight, Sparkles, ArrowRight } from "lucide-react";
import TrainerAvatar from "../common/TrainerAvatar";
import { getTrainerDisplayName, getTrainerPhotoUrl, ETHOS_MEDIA_FALLBACK_SVG } from "../../utils/mediaUrl";
import { createSlug } from "../../utils/createSlug";
import { publicApi } from "../../services/publicApi";
import "./faculty-profile-card.css";

const fallbackPoster = ETHOS_MEDIA_FALLBACK_SVG;

export default function FacultyProfileCard({
  trainer,
  allWorkshops = [],
  currentWorkshopId,
}) {
  const navigate = useNavigate();
  const [isOpen, setIsOpen] = useState(false);
  const [isHovered, setIsHovered] = useState(false);
  const [profileData, setProfileData] = useState(null);
  const [loadingProfile, setLoadingProfile] = useState(false);
  const hoverTimeoutRef = useRef(null);
  const cardRef = useRef(null);
  const carouselRef = useRef(null);

  const displayName = getTrainerDisplayName(trainer) || "Ethos Faculty";
  const slug = createSlug(displayName || trainer.trainerCode || trainer.id);

  // Lazy fetch on-demand public profile with in-memory caching
  const loadProfile = async () => {
    if (profileData || loadingProfile) return;
    setLoadingProfile(true);
    try {
      const data = await publicApi.getTrainerPublicProfile(slug);
      if (data) {
        setProfileData(data);
      }
    } catch (e) {
      console.warn("Could not fetch on-demand trainer public profile:", e);
    } finally {
      setLoadingProfile(false);
    }
  };

  // Derive styles & past workshops if API profile isn't loaded yet
  const { recentPastWorkshops, danceStylesList, totalExperienceYears } = useMemo(() => {
    if (profileData) {
      return {
        recentPastWorkshops: profileData.recentWorkshops || [],
        danceStylesList: profileData.danceStyles || (profileData.primaryDanceStyle ? [profileData.primaryDanceStyle] : ["Dance Faculty"]),
        totalExperienceYears: profileData.experienceYears || 3,
      };
    }

    const trainerId = String(trainer.trainerProfileId || trainer.id || "").toLowerCase();
    const cleanName = displayName.toLowerCase().trim();
    const now = new Date();

    const matches = (allWorkshops || []).filter((w) => {
      if (!w) return false;
      const wTrainerId = String(w.trainerProfileId || "").toLowerCase();
      const hasMatchingTrainerId = trainerId && (
        wTrainerId === trainerId ||
        (Array.isArray(w.trainers) && w.trainers.some((tr) => String(tr.trainerProfileId || tr.id || "").toLowerCase() === trainerId))
      );

      const wTrainerName = String(w.trainerName || "").toLowerCase();
      const hasMatchingTrainerName = cleanName && (
        wTrainerName.includes(cleanName) ||
        (Array.isArray(w.trainers) && w.trainers.some((tr) => (tr.name || tr.fullName || "").toLowerCase().includes(cleanName)))
      );

      return hasMatchingTrainerId || hasMatchingTrainerName;
    });

    // Strictly past / completed workshops, newest first, max 10
    const past = matches.filter((w) => {
      const d = new Date(w.workshopDate || w.startUtc || 0);
      return w.isCompleted || (!isNaN(d.getTime()) && d <= now);
    }).sort((a, b) => {
      const dateA = new Date(a.workshopDate || a.startUtc || 0).getTime();
      const dateB = new Date(b.workshopDate || b.startUtc || 0).getTime();
      return dateB - dateA;
    }).slice(0, 10);

    const rawStyles = trainer.danceStyles || trainer.DanceStyles || trainer.primaryDanceStyle || "Urban Choreography";
    const parsed = String(rawStyles)
      .split(/[,•|/]/)
      .map((s) => s.trim())
      .filter(Boolean);

    return {
      recentPastWorkshops: past,
      danceStylesList: parsed.length > 0 ? parsed : ["Urban Choreography"],
      totalExperienceYears: trainer.experienceYears || 3,
    };
  }, [trainer, displayName, allWorkshops, profileData]);

  // Desktop Hover with 180ms delay
  const handleMouseEnter = () => {
    if (window.innerWidth > 768) {
      if (hoverTimeoutRef.current) clearTimeout(hoverTimeoutRef.current);
      loadProfile();
      setIsHovered(true);
    }
  };

  const handleMouseLeave = () => {
    if (window.innerWidth > 768) {
      hoverTimeoutRef.current = setTimeout(() => {
        setIsHovered(false);
      }, 180);
    }
  };

  // Mobile Tap Toggle
  const handleChipClick = () => {
    loadProfile();
    if (window.innerWidth <= 768) {
      setIsOpen(true);
    } else {
      setIsHovered((prev) => !prev);
    }
  };

  // Carousel Arrow Controls
  const handleScrollCarousel = (direction) => {
    if (carouselRef.current) {
      carouselRef.current.scrollBy({
        left: direction * 140,
        behavior: "smooth",
      });
    }
  };

  // Close on outside click for desktop
  useEffect(() => {
    const handleOutsideClick = (e) => {
      if (cardRef.current && !cardRef.current.contains(e.target)) {
        setIsHovered(false);
      }
    };
    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, []);

  const showDesktopPopover = isHovered && typeof window !== "undefined" && window.innerWidth > 768;

  const renderProfileContent = (isMobile = false) => (
    <>
      {/* HEADER */}
      <div className="faculty-popover-header">
        <div className="faculty-popover-avatar-wrap">
          <div className="faculty-popover-avatar-ring">
            <TrainerAvatar trainer={profileData?.profilePhotoUrl || trainer} size={isMobile ? 54 : 48} />
          </div>
        </div>
        <div className="faculty-popover-info">
          <h4 className="faculty-popover-name">{displayName}</h4>
          <span className="faculty-popover-style-lead">
            {danceStylesList.slice(0, 2).join(" • ")}
          </span>
        </div>
        {isMobile && (
          <button
            type="button"
            className="faculty-popover-close-btn"
            onClick={() => setIsOpen(false)}
            aria-label="Close"
          >
            <X size={18} />
          </button>
        )}
      </div>

      {/* ABOUT */}
      <div className="faculty-popover-about">
        <div className="faculty-popover-section-title">ABOUT</div>
        <p className="faculty-popover-about-text">
          {profileData?.bio || trainer.bio || trainer.description || (
            `Resident Faculty Instructor at Ethos Dance Studio specializing in ${danceStylesList.join(", ")}, groove foundations, and high-performance stage choreography.`
          )}
        </p>
      </div>

      {/* EXPERIENCE & STYLES */}
      <div className="faculty-popover-experience">
        <div className="faculty-popover-section-title">
          <Sparkles size={11} color="#df806c" /> DANCE STYLES & EXPERIENCE
        </div>
        <div className="faculty-popover-specialties" style={{ marginBottom: "8px" }}>
          {danceStylesList.map((st, idx) => (
            <span key={idx} className="faculty-spec-tag">
              {st}
            </span>
          ))}
          {totalExperienceYears > 0 && (
            <span className="faculty-spec-tag" style={{ background: "rgba(255,255,255,0.08)", color: "#fff", borderColor: "rgba(255,255,255,0.15)" }}>
              {totalExperienceYears}+ Years Exp
            </span>
          )}
        </div>
      </div>

      {/* RECENT AT ETHOS (HORIZONTAL CAROUSEL, STRICTLY PAST WORKSHOPS, MAX 10) */}
      {recentPastWorkshops.length > 0 && (
        <div className="faculty-popover-recent">
          <div className="faculty-recent-carousel-header">
            <div className="faculty-popover-section-title" style={{ margin: 0 }}>
              RECENT AT ETHOS ({recentPastWorkshops.length})
            </div>
            {recentPastWorkshops.length > 2 && (
              <div className="faculty-carousel-arrows">
                <button
                  type="button"
                  className="faculty-carousel-arrow-btn"
                  onClick={() => handleScrollCarousel(-1)}
                  aria-label="Scroll left"
                >
                  <ChevronLeft size={14} />
                </button>
                <button
                  type="button"
                  className="faculty-carousel-arrow-btn"
                  onClick={() => handleScrollCarousel(1)}
                  aria-label="Scroll right"
                >
                  <ChevronRight size={14} />
                </button>
              </div>
            )}
          </div>

          <div className="faculty-recent-carousel" ref={carouselRef}>
            {recentPastWorkshops.map((w) => {
              const wDate = w.workshopDate
                ? new Date(w.workshopDate).toLocaleDateString("en-IN", {
                    month: "short",
                    day: "numeric",
                  })
                : "";
              const wSlug = w.slug || createSlug(w.title || w.name || w.id);
              const poster = w.posterUrl || w.imageUrl || fallbackPoster;

              return (
                <div
                  key={w.id}
                  className="faculty-recent-card"
                  onClick={() => {
                    setIsOpen(false);
                    setIsHovered(false);
                    navigate(`/workshops/${wSlug}`);
                    window.scrollTo({ top: 0, behavior: "smooth" });
                  }}
                  role="button"
                  tabIndex={0}
                  title={`${w.title} (${wDate})`}
                >
                  <img
                    src={poster}
                    alt={w.title}
                    className="faculty-recent-card-poster"
                    loading="lazy"
                    onError={(e) => {
                      e.currentTarget.onerror = null;
                      e.currentTarget.src = fallbackPoster;
                    }}
                  />
                  <div className="faculty-recent-card-body">
                    <div className="faculty-recent-card-title">{w.title}</div>
                    <div className="faculty-recent-card-style">{w.danceStyle || "Dance"}</div>
                    <div className="faculty-recent-card-date">{wDate}</div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* VIEW FULL PROFILE ACTION */}
      <button
        type="button"
        className="faculty-popover-full-profile-btn"
        onClick={() => {
          setIsOpen(false);
          setIsHovered(false);
          navigate(`/trainers/${slug}`);
          window.scrollTo({ top: 0, behavior: "smooth" });
        }}
      >
        <span>View Full Profile</span>
        <ArrowRight size={14} />
      </button>
    </>
  );

  return (
    <div
      className="faculty-chip-wrapper"
      ref={cardRef}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
    >
      {/* TRIGGER CHIP */}
      <button
        type="button"
        className={`faculty-chip-btn ${showDesktopPopover || isOpen ? "is-active" : ""}`}
        onClick={handleChipClick}
        aria-haspopup="dialog"
        aria-expanded={showDesktopPopover || isOpen}
        title={`View ${displayName}'s profile`}
      >
        <TrainerAvatar trainer={profileData?.profilePhotoUrl || trainer} size={36} />
        <div className="faculty-chip-meta">
          <strong className="faculty-chip-name">{displayName}</strong>
          <span className="faculty-chip-styles">{danceStylesList[0] || "Dance Faculty"}</span>
        </div>
        <span className="faculty-chip-indicator" aria-hidden="true">
          ↗
        </span>
      </button>

      {/* DESKTOP POPOVER */}
      {showDesktopPopover && (
        <div
          className="faculty-popover"
          role="dialog"
          aria-label={`${displayName} Faculty Profile`}
          onMouseEnter={handleMouseEnter}
          onMouseLeave={handleMouseLeave}
        >
          {renderProfileContent(false)}
        </div>
      )}

      {/* MOBILE BOTTOM SHEET MODAL */}
      {isOpen && (
        <div
          className="faculty-mobile-backdrop"
          onClick={() => setIsOpen(false)}
          role="presentation"
        >
          <div
            className="faculty-bottom-sheet"
            role="dialog"
            aria-modal="true"
            aria-label={`${displayName} Faculty Profile`}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="faculty-sheet-drag-handle" />
            {renderProfileContent(true)}
          </div>
        </div>
      )}
    </div>
  );
}
