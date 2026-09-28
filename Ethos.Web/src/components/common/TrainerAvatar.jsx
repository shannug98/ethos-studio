import React, { useState, useEffect } from "react";
import { getTrainerPhotoUrl, getTrainerDisplayName, getTrainerInitials } from "../../utils/mediaUrl";

const SIZE_MAP = {
  xs: { size: 24, fontSize: 10, borderWidth: 1 },
  sm: { size: 32, fontSize: 12, borderWidth: 1.5 },
  md: { size: 40, fontSize: 14, borderWidth: 1.5 },
  lg: { size: 56, fontSize: 18, borderWidth: 2 },
  xl: { size: 72, fontSize: 22, borderWidth: 2.5 },
  "2xl": { size: 96, fontSize: 28, borderWidth: 3 },
};

/**
 * Authoritative Canonical Trainer Avatar Component.
 * Supports:
 * - Direct image rendering with meaningful accessible alt text
 * - Instant blob preview rendering
 * - Deterministic branded initials fallback on missing or broken images
 * - Dev-mode diagnostic logging for broken URLs
 */
export default function TrainerAvatar({
  trainer,
  name,
  size = "md",
  className = "",
  style = {},
  bordered = false,
  borderColor = "#FF5500",
  showName = false,
  subtext = "",
  alt,
  onClick,
}) {
  const [imgError, setImgError] = useState(false);

  // Compute display name and photo URL
  const displayName = name || (trainer ? getTrainerDisplayName(trainer) : "Trainer");
  const accessibleAlt = alt || `${displayName} profile photo`;
  const photoUrl = trainer ? getTrainerPhotoUrl(trainer, false) : "";
  const initials = getTrainerInitials(displayName || trainer);

  // Reset error state if trainer/photo changes
  useEffect(() => {
    setImgError(false);
  }, [photoUrl]);

  // Derive size parameters
  const sizeConfig = typeof size === "number"
    ? { size, fontSize: Math.round(size * 0.38), borderWidth: size > 48 ? 2 : 1.5 }
    : SIZE_MAP[size] || SIZE_MAP.md;

  const dimension = sizeConfig.size;
  const fontSize = sizeConfig.fontSize;
  const borderWidth = sizeConfig.borderWidth;

  const handleImageError = (e) => {
    if (import.meta.env?.DEV) {
      console.warn(`[TrainerAvatar] Failed to load photo for "${displayName}". URL:`, photoUrl);
    }
    setImgError(true);
  };

  const hasValidPhoto = Boolean(photoUrl && !imgError);

  const avatarCircle = (
    <div
      className={`trainer-avatar-circle ${className}`}
      style={{
        width: `${dimension}px`,
        height: `${dimension}px`,
        minWidth: `${dimension}px`,
        minHeight: `${dimension}px`,
        borderRadius: "50%",
        overflow: "hidden",
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        boxSizing: "border-box",
        border: bordered ? `${borderWidth}px solid ${borderColor}` : "1px solid rgba(255, 255, 255, 0.12)",
        background: hasValidPhoto ? "#1e293b" : "linear-gradient(135deg, #1e293b 0%, #0f172a 100%)",
        color: "#F8FAFC",
        fontWeight: 700,
        fontSize: `${fontSize}px`,
        userSelect: "none",
        cursor: onClick ? "pointer" : "inherit",
        flexShrink: 0,
        ...style,
      }}
      onClick={onClick}
    >
      {hasValidPhoto ? (
        <img
          src={photoUrl}
          alt={accessibleAlt}
          style={{
            width: "100%",
            height: "100%",
            objectFit: "cover",
            objectPosition: "center top",
            display: "block",
          }}
          onError={handleImageError}
        />
      ) : (
        <span
          style={{
            letterSpacing: "0.04em",
            textTransform: "uppercase",
            color: "#F1F5F9",
          }}
        >
          {initials}
        </span>
      )}
    </div>
  );

  if (!showName && !subtext) {
    return avatarCircle;
  }

  return (
    <div
      className="trainer-avatar-compound"
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: dimension >= 40 ? "10px" : "8px",
        cursor: onClick ? "pointer" : "inherit",
      }}
      onClick={onClick}
    >
      {avatarCircle}
      <div style={{ display: "flex", flexDirection: "column", lineHeight: 1.25 }}>
        {showName && (
          <span style={{ fontWeight: 650, fontSize: `${Math.max(12, fontSize)}px`, color: "#1E293B" }}>
            {displayName}
          </span>
        )}
        {subtext && (
          <span style={{ fontSize: "11px", color: "#64748B" }}>
            {subtext}
          </span>
        )}
      </div>
    </div>
  );
}
