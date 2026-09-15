import { useState, useEffect } from "react";
import { studentStateSync } from "../../services/studentStateSync";
import { getMediaUrl } from "../../utils/mediaUrl";

export default function StudentAvatar({
  photoUrl,
  name = "Student",
  size = 36,
  className = "",
  style = {},
  clickable = false,
  onClick,
}) {
  const [canonicalUrl, setCanonicalUrl] = useState(() => studentStateSync.getPhotoUrl());
  const [imgError, setImgError] = useState(false);

  useEffect(() => {
    // Subscribe to canonical photo updates across portal
    const unsub = studentStateSync.onPhotoUpdated((url) => {
      setCanonicalUrl(url);
      setImgError(false);
    });

    // If canonical photo hasn't been checked yet, trigger deduplicated fetch
    if (!studentStateSync.hasCheckedPhoto()) {
      studentStateSync.loadCanonicalPhoto();
    }

    return () => unsub();
  }, []);

  useEffect(() => {
    setImgError(false);
  }, [photoUrl, canonicalUrl]);

  // Determine active display URL
  let activeUrl = null;
  if (photoUrl && (photoUrl.startsWith("blob:") || photoUrl.startsWith("data:"))) {
    activeUrl = photoUrl;
  } else if (canonicalUrl) {
    activeUrl = canonicalUrl;
  } else if (photoUrl && !photoUrl.includes("/uploads/student-profile-photos/")) {
    activeUrl = getMediaUrl(photoUrl);
  }

  const initials = name
    ? name
        .split(" ")
        .filter(Boolean)
        .map((n) => n[0])
        .join("")
        .slice(0, 2)
        .toUpperCase() || "ST"
    : "ST";

  const sizeStyle = {
    width: `${size}px`,
    height: `${size}px`,
    minWidth: `${size}px`,
    minHeight: `${size}px`,
    fontSize: `${Math.max(10, Math.round(size * 0.38))}px`,
    ...style,
  };

  const isInteractive = Boolean(activeUrl && !imgError && (clickable || onClick));

  const handleClick = (e) => {
    if (!isInteractive) return;
    if (onClick) {
      onClick(e);
    } else if (clickable) {
      e.preventDefault();
      e.stopPropagation();
      studentStateSync.openPhotoViewer(activeUrl, name);
    }
  };

  const handleKeyDown = (e) => {
    if (!isInteractive) return;
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      handleClick(e);
    }
  };

  if (activeUrl && !imgError) {
    return (
      <div
        className={`student-avatar ${isInteractive ? "student-avatar--clickable" : ""} ${className}`}
        style={{
          ...sizeStyle,
          cursor: isInteractive ? "pointer" : "inherit",
        }}
        onClick={handleClick}
        onKeyDown={handleKeyDown}
        role={isInteractive ? "button" : undefined}
        tabIndex={isInteractive ? 0 : undefined}
        title={isInteractive ? `View ${name}'s photo` : name}
        aria-label={isInteractive ? `View ${name}'s photo` : undefined}
      >
        <img
          src={activeUrl}
          alt={name}
          onError={() => setImgError(true)}
          style={{ width: "100%", height: "100%", objectFit: "cover", borderRadius: "50%" }}
        />
      </div>
    );
  }

  return (
    <div
      className={`student-avatar student-avatar-fallback ${className}`}
      style={{
        ...sizeStyle,
        borderRadius: "50%",
        background: "linear-gradient(135deg, #e97963 0%, #c55a45 100%)",
        color: "#12100e",
        fontWeight: 800,
        display: "grid",
        placeItems: "center",
        userSelect: "none",
        boxShadow: "0 2px 8px rgba(233, 121, 99, 0.25)",
      }}
      title={name}
    >
      {initials}
    </div>
  );
}
