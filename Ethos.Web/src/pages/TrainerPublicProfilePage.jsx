import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  ArrowLeft,
  Calendar,
  Clock,
  MapPin,
  Sparkles,
  ExternalLink,
  Award,
  BookOpen,
  AlertCircle,
  Loader2,
  CheckCircle2,
} from "lucide-react";
import { publicApi } from "../services/publicApi";
import { getTrainerDisplayName, ETHOS_MEDIA_FALLBACK_SVG } from "../utils/mediaUrl";
import { createSlug } from "../utils/createSlug";
import "./TrainerPublicProfilePage.css";

function InstagramSvg() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="2" y="2" width="20" height="20" rx="5" ry="5" />
      <path d="M16 11.37A4 4 0 1 1 12.63 8 4 4 0 0 1 16 11.37z" />
      <line x1="17.5" y1="6.5" x2="17.51" y2="6.5" />
    </svg>
  );
}

function YouTubeSvg() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M22.54 6.42a2.78 2.78 0 0 0-1.94-2C18.88 4 12 4 12 4s-6.88 0-8.6.46a2.78 2.78 0 0 0-1.94 2A29 29 0 0 0 1 11.75a29 29 0 0 0 .46 5.33A2.78 2.78 0 0 0 3.4 19c1.72.46 8.6.46 8.6.46s6.88 0 8.6-.46a2.78 2.78 0 0 0 1.94-2 29 29 0 0 0 .46-5.25 29 29 0 0 0-.46-5.33z" />
      <polygon points="9.75 15.02 15.5 11.75 9.75 8.48 9.75 15.02" />
    </svg>
  );
}

const fallbackImage = ETHOS_MEDIA_FALLBACK_SVG;

export default function TrainerPublicProfilePage() {
  const { slug } = useParams();
  const navigate = useNavigate();
  const [trainer, setTrainer] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let isMounted = true;
    window.scrollTo({ top: 0, behavior: "smooth" });

    async function loadProfile() {
      try {
        setLoading(true);
        setError("");
        const data = await publicApi.getTrainerPublicProfile(slug);
        if (isMounted) {
          if (data) {
            setTrainer(data);
          } else {
            setError("Trainer profile not found or currently unavailable.");
          }
        }
      } catch (err) {
        console.error("Failed to load trainer public profile:", err);
        if (isMounted) {
          setError("Failed to load trainer profile. Please try again.");
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    }

    loadProfile();

    return () => {
      isMounted = false;
    };
  }, [slug]);

  const formatDate = (iso) => {
    if (!iso) return "";
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

  const handleBack = () => {
    if (window.history?.state?.idx > 0 || window.history?.length > 1) {
      navigate(-1);
    } else {
      navigate("/workshops");
    }
  };

  if (loading) {
    return (
      <div className="trainer-public-profile-page">
        <div className="trainer-profile-loading-screen">
          <Loader2 size={36} className="animate-spin" color="#df806c" />
          <span>Loading faculty profile...</span>
        </div>
      </div>
    );
  }

  if (error || !trainer) {
    return (
      <div className="trainer-public-profile-page">
        <div className="trainer-profile-error-screen">
          <AlertCircle size={40} color="#df806c" />
          <h2>{error || "Trainer Profile Not Found"}</h2>
          <p>The instructor you are looking for might have been moved or is not publicly listed.</p>
          <button
            type="button"
            className="trainer-profile-back-btn"
            onClick={() => navigate("/workshops")}
          >
            <ArrowLeft size={16} /> Explore All Workshops
          </button>
        </div>
      </div>
    );
  }

  const danceStyles = trainer.danceStyles && trainer.danceStyles.length > 0
    ? trainer.danceStyles
    : [trainer.primaryDanceStyle || "Contemporary / Urban"];

  return (
    <div className="trainer-public-profile-page">
      <div className="trainer-profile-container">
        {/* CONTEXTUAL BACK BUTTON ABOVE HERO */}
        <div className="trainer-back-nav-row">
          <button
            type="button"
            className="trainer-contextual-back-btn"
            onClick={handleBack}
            aria-label="Go back"
          >
            <ArrowLeft size={15} />
            <span>Back</span>
          </button>
        </div>

        {/* HERO CARD */}
        <div className="trainer-profile-hero-card">
          <div className="trainer-hero-portrait-wrap">
            <img
              src={trainer.profilePhotoUrl || fallbackImage}
              alt={trainer.fullName}
              className="trainer-hero-portrait-img"
              onError={(e) => {
                e.currentTarget.onerror = null;
                e.currentTarget.src = fallbackImage;
              }}
            />
            <div className="trainer-hero-badge-overlay">
              <span className="trainer-verified-pill">✦ ETHOS FACULTY</span>
            </div>
          </div>

          <div className="trainer-hero-details">
            <span className="trainer-eyebrow-tag">DANCE INSTRUCTOR & CHOREOGRAPHER</span>
            <h1 className="trainer-hero-name">{trainer.fullName}</h1>

            {/* FLAT DANCE STYLES LIST */}
            <div className="trainer-hero-styles-row">
              {danceStyles.map((style, idx) => (
                <span key={idx} className="trainer-style-pill">
                  {style}
                </span>
              ))}
              {trainer.experienceYears > 0 && (
                <span className="trainer-style-pill exp-pill">
                  <Award size={13} /> {trainer.experienceYears}+ Years Exp
                </span>
              )}
            </div>

            {/* BIO */}
            <div className="trainer-hero-bio">
              <p>
                {trainer.bio || (
                  `Professional resident dance instructor and choreographer at Ethos Dance Studio specializing in ${danceStyles.join(", ")}, musical precision, groove foundations, and stage performance.`
                )}
              </p>
            </div>

            {/* SOCIAL / STUDIO METADATA */}
            <div className="trainer-hero-footer-meta">
              {trainer.city && (
                <div className="trainer-meta-item">
                  <MapPin size={15} color="#df806c" />
                  <span>{trainer.city}</span>
                </div>
              )}
              {trainer.currentStudio && (
                <div className="trainer-meta-item">
                  <Sparkles size={15} color="#df806c" />
                  <span>{trainer.currentStudio}</span>
                </div>
              )}

              {/* SOCIAL LINKS */}
              {(trainer.instagramUrl || trainer.youtubeUrl) && (
                <div className="trainer-social-links-row">
                  {trainer.instagramUrl && (
                    <a
                      href={trainer.instagramUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="trainer-social-btn"
                      title="Instagram"
                    >
                      <InstagramSvg />
                      <span>Instagram</span>
                    </a>
                  )}
                  {trainer.youtubeUrl && (
                    <a
                      href={trainer.youtubeUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="trainer-social-btn"
                      title="YouTube"
                    >
                      <YouTubeSvg />
                      <span>YouTube</span>
                    </a>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* UPCOMING AT ETHOS SECTION */}
        <section className="trainer-section-block">
          <div className="trainer-section-header">
            <h2 className="trainer-section-title">
              <Calendar size={20} color="#df806c" /> UPCOMING AT ETHOS
            </h2>
            <span className="trainer-section-count">
              {trainer.upcomingWorkshops?.length || 0} scheduled
            </span>
          </div>

          {trainer.upcomingWorkshops && trainer.upcomingWorkshops.length > 0 ? (
            <div className="trainer-workshops-grid">
              {trainer.upcomingWorkshops.map((w) => {
                const wSlug = w.slug || createSlug(w.title || w.id);
                return (
                  <div
                    key={w.id}
                    className="trainer-workshop-card"
                    onClick={() => navigate(`/workshops/${wSlug}`)}
                    role="button"
                    tabIndex={0}
                  >
                    <div className="trainer-workshop-card-poster-wrap">
                      <img
                        src={w.posterUrl || fallbackImage}
                        alt={w.title}
                        className="trainer-workshop-poster-img"
                        onError={(e) => {
                          e.currentTarget.onerror = null;
                          e.currentTarget.src = fallbackImage;
                        }}
                      />
                      <span className="trainer-workshop-status-pill upcoming">
                        Upcoming
                      </span>
                    </div>
                    <div className="trainer-workshop-card-content">
                      <span className="trainer-workshop-style">{w.danceStyle || "Dance"}</span>
                      <h3 className="trainer-workshop-title">{w.title}</h3>
                      <div className="trainer-workshop-date">
                        <Calendar size={13} /> {formatDate(w.workshopDate)}
                      </div>
                      <button
                        type="button"
                        className="trainer-workshop-book-btn"
                        onClick={(e) => {
                          e.stopPropagation();
                          navigate(`/workshops/${wSlug}`);
                        }}
                      >
                        Book Workshop →
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="trainer-empty-workshops-box">
              <p>No upcoming workshops scheduled right now. Check back soon!</p>
            </div>
          )}
        </section>

        {/* RECENT AT ETHOS (STRICTLY PAST COMPLETED WORKSHOPS) */}
        {trainer.recentWorkshops && trainer.recentWorkshops.length > 0 && (
          <section className="trainer-section-block">
            <div className="trainer-section-header">
              <h2 className="trainer-section-title">
                <CheckCircle2 size={20} color="#df806c" /> RECENT AT ETHOS
              </h2>
              <span className="trainer-section-count">
                {trainer.recentWorkshops.length} completed
              </span>
            </div>

            <div className="trainer-workshops-grid">
              {trainer.recentWorkshops.map((w) => {
                const wSlug = w.slug || createSlug(w.title || w.id);
                return (
                  <div
                    key={w.id}
                    className="trainer-workshop-card past"
                    onClick={() => navigate(`/workshops/${wSlug}`)}
                    role="button"
                    tabIndex={0}
                  >
                    <div className="trainer-workshop-card-poster-wrap">
                      <img
                        src={w.posterUrl || fallbackImage}
                        alt={w.title}
                        className="trainer-workshop-poster-img"
                        onError={(e) => {
                          e.currentTarget.onerror = null;
                          e.currentTarget.src = fallbackImage;
                        }}
                      />
                      <span className="trainer-workshop-status-pill completed">
                        ✓ Completed
                      </span>
                    </div>
                    <div className="trainer-workshop-card-content">
                      <span className="trainer-workshop-style">{w.danceStyle || "Dance"}</span>
                      <h3 className="trainer-workshop-title">{w.title}</h3>
                      <div className="trainer-workshop-date">
                        <Calendar size={13} /> {formatDate(w.workshopDate)}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </section>
        )}
      </div>
    </div>
  );
}
