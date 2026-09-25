import React, { useState, useEffect, useRef, useMemo } from "react";
import { Eye, X, Volume2, VolumeX, Play, Pause, AlertCircle } from "lucide-react";
import { getMediaUrl, handleMediaImgError, ETHOS_MEDIA_FALLBACK_SVG } from "../../../utils/mediaUrl";
import { getMediaPreviewPresentation } from "../../../utils/mediaPreviewPresentation";
import "./AdminMediaPreviewModal.css";

export { getMediaPreviewPresentation };

/**
 * Universal Admin Media Preview Modal
 * 
 * Single authoritative preview component used across all Media Library placements.
 * Enforces strict single-video lifecycle, HTTP 206 range streaming, and zero background media leakage.
 */
export default function AdminMediaPreviewModal({ media, placement, slot, onClose }) {
  const [videoMuted, setVideoMuted] = useState(true);
  const [videoPlaying, setVideoPlaying] = useState(true);
  const [videoError, setVideoError] = useState(null);
  const [loading, setLoading] = useState(true);
  const videoRef = useRef(null);

  const presentation = useMemo(
    () => getMediaPreviewPresentation(media, placement, slot),
    [media, placement, slot]
  );

  const mediaUrl = useMemo(() => getMediaUrl(media), [media]);

  // Robust Video Cleanup: Pause, clear src, and release
  const performVideoCleanup = () => {
    if (videoRef.current) {
      try {
        videoRef.current.pause();
        videoRef.current.removeAttribute("src");
        videoRef.current.load();
      } catch (err) {
        // Silently handle DOM unmount race
      }
    }
  };

  const handleClose = () => {
    performVideoCleanup();
    if (onClose) onClose();
  };

  // Unmount cleanup safety
  useEffect(() => {
    return () => {
      performVideoCleanup();
    };
  }, []);

  // Body scroll locking with clean restoration on unmount
  useEffect(() => {
    const originalOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = originalOverflow;
    };
  }, []);

  // Keyboard navigation: Escape key to close
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        handleClose();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  if (!media) return null;

  return (
    <div className="admin-preview-overlay" onClick={handleClose}>
      <div
        className="admin-preview-dialog"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
      >
        {/* Header */}
        <div className="admin-preview-header">
          <div className="admin-preview-header-left">
            <span className="admin-preview-badge-pill">
              <Eye size={13} />
              {presentation.badge}
            </span>
            <h3 className="admin-preview-header-title">
              {placement?.placement || placement?.area || "Media"} → {slot?.label || media.title || "Asset Preview"}
            </h3>
          </div>
          <button
            type="button"
            className="admin-preview-close-btn"
            onClick={handleClose}
            title="Close preview (ESC)"
          >
            <X size={18} />
          </button>
        </div>

        {/* Stage Viewport */}
        <div className="admin-preview-stage">
          {/* 1. Hero Stage Variant */}
          {presentation.variant === "hero" && (
            <div className="preview-variant-hero">
              {presentation.isVideo ? (
                <video
                  ref={videoRef}
                  src={mediaUrl}
                  className="preview-hero-media-layer"
                  controls
                  autoPlay
                  loop
                  playsInline
                  onLoadedData={() => setLoading(false)}
                  onError={(e) => {
                    setLoading(false);
                    setVideoError("Unable to stream Hero video. Check network connectivity.");
                  }}
                />
              ) : (
                <img
                  src={mediaUrl}
                  alt={presentation.title}
                  className="preview-hero-media-layer"
                  onLoad={() => setLoading(false)}
                  onError={(e) => {
                    setLoading(false);
                    handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG);
                  }}
                />
              )}

              <div className="preview-hero-scrim" />

              <div className="preview-hero-content">
                <span className="preview-hero-eyebrow">{presentation.eyebrow}</span>
                <h1 className="preview-hero-headline">
                  MORE THAN
                  <span>DANCE</span>
                </h1>
                <span className="preview-hero-subtitle">{presentation.subtitle}</span>
                <p className="preview-hero-desc">{presentation.description}</p>
                <div className="preview-hero-actions">
                  <span className="preview-hero-btn primary">View Workshops</span>
                  <span className="preview-hero-btn secondary">Book a Class</span>
                </div>
              </div>
            </div>
          )}

          {/* 2. Homepage Reel Variant (9:16 Vertical Phone Frame) */}
          {presentation.variant === "reel" && (
            <div className="preview-variant-reel">
              <video
                ref={videoRef}
                src={mediaUrl}
                className="preview-reel-video"
                controls
                autoPlay
                loop
                playsInline
                onLoadedData={() => setLoading(false)}
                onError={() => {
                  setLoading(false);
                  setVideoError("Failed to stream Reel video.");
                }}
              />
              <div className="preview-reel-scrim" />

              <div className="preview-reel-overlay">
                <span className="preview-reel-badge">DANCE REEL</span>
                <h4 className="preview-reel-title">{presentation.title}</h4>
                {presentation.caption && (
                  <p className="preview-reel-caption">{presentation.caption}</p>
                )}
              </div>
            </div>
          )}

          {/* 3. Trainer Master Faculty Card (3:4 Portrait) */}
          {presentation.variant === "trainer" && (
            <div className="preview-variant-trainer">
              <img
                src={mediaUrl}
                alt={presentation.title}
                className="preview-trainer-img"
                onLoad={() => setLoading(false)}
                onError={(e) => {
                  setLoading(false);
                  handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG);
                }}
              />
              <div className="preview-trainer-scrim" />
              <div className="preview-trainer-info">
                <span className="preview-trainer-role-tag">{presentation.role}</span>
                <h4 className="preview-trainer-name">{presentation.title}</h4>
                <p className="preview-trainer-disciplines">{presentation.disciplines}</p>
              </div>
            </div>
          )}

          {/* 4. We Are Ethos Trio Variant (4:5 / 16:9 / 1:1) */}
          {presentation.variant === "about" && (
            <div className={`preview-variant-about ${presentation.slotClass || "preview-about-portrait"}`}>
              <div className="preview-about-media-box">
                <img
                  src={mediaUrl}
                  alt={presentation.title}
                  className="preview-about-img"
                  onLoad={() => setLoading(false)}
                  onError={(e) => {
                    setLoading(false);
                    handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG);
                  }}
                />
              </div>
              <div className="preview-about-footer">
                <span className="preview-about-slot-role">{presentation.role}</span>
                <h4 className="preview-about-title">{presentation.title}</h4>
              </div>
            </div>
          )}

          {/* 5. Founder Co-Directors Variant (3:4) */}
          {presentation.variant === "founder" && (
            <div className="preview-variant-founder">
              <img
                src={mediaUrl}
                alt={presentation.title}
                className="preview-founder-img"
                onLoad={() => setLoading(false)}
                onError={(e) => {
                  setLoading(false);
                  handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG);
                }}
              />
              <div className="preview-founder-scrim" />
              <div className="preview-founder-meta">
                <span className="preview-founder-badge">{presentation.badge}</span>
                <h4 className="preview-founder-title">{presentation.title}</h4>
              </div>
            </div>
          )}

          {/* 6. Gallery Large Video (16:9 Cinematic Theater Player) */}
          {presentation.variant === "gallery-video" && (
            <div className="preview-variant-gallery-video">
              <video
                ref={videoRef}
                src={mediaUrl}
                className="preview-gallery-video-player"
                controls
                autoPlay
                playsInline
                onLoadedData={() => setLoading(false)}
                onError={() => {
                  setLoading(false);
                  setVideoError("Failed to stream cinematic video.");
                }}
              />
            </div>
          )}

          {/* 7. Gallery Slideshow / Photo Lightbox */}
          {(presentation.variant === "gallery-photo" || presentation.variant === "gallery-slideshow") && (
            <div className="preview-variant-gallery-photo">
              {presentation.isVideo ? (
                <video
                  ref={videoRef}
                  src={mediaUrl}
                  controls
                  autoPlay
                  playsInline
                  className="preview-generic-video"
                  onLoadedData={() => setLoading(false)}
                  onError={() => {
                    setLoading(false);
                    setVideoError("Failed to stream video.");
                  }}
                />
              ) : (
                <img
                  src={mediaUrl}
                  alt={presentation.title}
                  className="preview-gallery-photo-img"
                  onLoad={() => setLoading(false)}
                  onError={(e) => {
                    setLoading(false);
                    handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG);
                  }}
                />
              )}
            </div>
          )}

          {/* 8. Workshop Landscape */}
          {presentation.variant === "workshop-landscape" && (
            <div className="preview-variant-workshop-landscape">
              <img
                src={mediaUrl}
                alt={presentation.title}
                className="preview-workshop-banner-img"
                onLoad={() => setLoading(false)}
                onError={(e) => {
                  setLoading(false);
                  handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG);
                }}
              />
              <div className="preview-workshop-badge-strip">
                <span className="admin-preview-badge-pill">{presentation.badge}</span>
              </div>
            </div>
          )}

          {/* 9. Workshop Portrait */}
          {presentation.variant === "workshop-portrait" && (
            <div className="preview-variant-workshop-portrait">
              <img
                src={mediaUrl}
                alt={presentation.title}
                className="preview-workshop-portrait-img"
                onLoad={() => setLoading(false)}
                onError={(e) => {
                  setLoading(false);
                  handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG);
                }}
              />
            </div>
          )}

          {/* 10. Event Photos / Generic */}
          {(presentation.variant === "event" || presentation.variant === "generic") && (
            <div className="preview-variant-generic">
              {presentation.isVideo ? (
                <video
                  ref={videoRef}
                  src={mediaUrl}
                  controls
                  autoPlay
                  playsInline
                  className="preview-generic-video"
                  onLoadedData={() => setLoading(false)}
                  onError={() => {
                    setLoading(false);
                    setVideoError("Failed to stream event video.");
                  }}
                />
              ) : (
                <img
                  src={mediaUrl}
                  alt={presentation.title}
                  className="preview-generic-img"
                  onLoad={() => setLoading(false)}
                  onError={(e) => {
                    setLoading(false);
                    handleMediaImgError(e, ETHOS_MEDIA_FALLBACK_SVG);
                  }}
                />
              )}
            </div>
          )}

          {/* Error Banner */}
          {videoError && (
            <div className="preview-error-box">
              <AlertCircle size={22} style={{ marginBottom: 8 }} />
              <p style={{ margin: 0, fontWeight: 600 }}>{videoError}</p>
            </div>
          )}
        </div>

        {/* Bottom Telemetry & Metadata Bar */}
        <div className="admin-preview-tech-bar">
          <div className="admin-preview-meta-items">
            <span>
              <strong>MIME:</strong> {media.mimeType || (presentation.isVideo ? "video/mp4" : "image/jpeg")}
            </span>
            {media.fileSizeBytes > 0 && (
              <span>
                <strong>Size:</strong> {(media.fileSizeBytes / (1024 * 1024)).toFixed(2)} MB
              </span>
            )}
            <span>
              <strong>Aspect Ratio:</strong> {presentation.aspectRatio}
            </span>
            {media.durationSeconds > 0 && (
              <span>
                <strong>Duration:</strong> {media.durationSeconds}s
              </span>
            )}
            {media.category && (
              <span>
                <strong>Category:</strong> {media.category}
              </span>
            )}
            {media.objectKey && (
              <span title={media.objectKey}>
                <strong>Key:</strong>{" "}
                {media.objectKey.length > 35 ? "..." + media.objectKey.slice(-30) : media.objectKey}
              </span>
            )}
          </div>
          <button
            type="button"
            className="admin-preview-footer-btn"
            onClick={handleClose}
          >
            Close Preview (ESC)
          </button>
        </div>
      </div>
    </div>
  );
}
