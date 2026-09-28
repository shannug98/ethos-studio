import { Calendar, Clock, MapPin } from "lucide-react";
import { handleMediaImgError } from "../../utils/mediaUrl";

/**
 * Unified Workshop Card Media Overlay Component
 * 
 * Enforces strict Ethos UI/UX hierarchy:
 * INSIDE / ON THE IMAGE:
 *   - Conditional ✦ OG badge (only when isEthosOriginal === true)
 *   - Optional Completed badge
 *   - Optional Hover/Click Arrow
 *   - Unified Bottom Metadata Bar: Date, Level, Time, Location
 * 
 * ZERO trainer information rendered here or anywhere on cards.
 */
export default function WorkshopCardMedia({
  image,
  title,
  isEthosOriginal = false,
  isCompleted = false,
  date,
  time,
  venue,
  level = "ALL LEVELS",
  aspectRatio = "3/4",
  showArrow = false,
  className = "",
  objectPosition = "center",
  onError = handleMediaImgError,
}) {
  const isPortrait = aspectRatio === "3/4" || aspectRatio === "3 / 4";

  return (
    <div
      className={`workshop-card-media ${isPortrait ? "workshop-card-media--portrait" : ""} ${className}`.trim()}
      style={{ aspectRatio }}
    >
      <img
        src={image}
        alt={title || "Workshop Poster"}
        className="workshop-card-img"
        style={{ objectPosition }}
        loading="lazy"
        decoding="async"
        onError={onError}
      />
      <div className="workshop-card-scrim" />

      {/* TOP BADGES */}
      <div className="workshop-card-top-badges">
        {isCompleted ? (
          <span className="workshop-card-completed-badge">✓ COMPLETED</span>
        ) : isEthosOriginal ? (
          <span className="workshop-card-og-badge">✦ OG</span>
        ) : null}
      </div>

      {/* OPTIONAL TOP-RIGHT HOVER ARROW */}
      {showArrow && (
        <span className="workshop-card-arrow-icon" aria-hidden="true">
          ↗
        </span>
      )}

      {/* UNIFIED BOTTOM METADATA OVERLAY */}
      <div className="workshop-card-meta-overlay">
        {/* ROW 1: DATE + DANCE LEVEL */}
        <div className="workshop-card-meta-row-primary">
          {date && (
            <div className="workshop-card-date-badge">
              <Calendar size={13} className="workshop-card-meta-icon" />
              <span className="workshop-card-date-text">{date}</span>
            </div>
          )}
          {level && (
            <span className="workshop-card-level-badge">{level}</span>
          )}
        </div>

        {/* ROW 2: TIME + LOCATION */}
        {(time || venue) && (
          <div className="workshop-card-meta-row-secondary">
            {time && (
              <span className="workshop-card-meta-item">
                <Clock size={12} className="workshop-card-meta-icon" />
                <span className="workshop-card-meta-text">{time}</span>
              </span>
            )}
            {time && venue && <span className="workshop-card-meta-dot">·</span>}
            {venue && (
              <span className="workshop-card-meta-item workshop-card-venue-truncate">
                <MapPin size={12} className="workshop-card-meta-icon" />
                <span className="workshop-card-meta-text" title={venue}>
                  {venue}
                </span>
              </span>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
