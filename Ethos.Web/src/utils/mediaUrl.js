import { API_BASE_URL } from "../config/api.js";

const isDev =
  Boolean(typeof import.meta !== "undefined" && import.meta.env?.DEV) ||
  (typeof window !== "undefined" &&
    (window.location.hostname === "localhost" ||
      window.location.hostname === "127.0.0.1"));

export function getMediaUrl(pathOrItem, explicitId) {
  let path = pathOrItem;
  let id = explicitId;

  // Handle object input (e.g. item from mediaList)
  if (pathOrItem && typeof pathOrItem === "object") {
    id = explicitId || pathOrItem.id || pathOrItem.mediaItemId;
    path =
      pathOrItem.thumbnailUrl ||
      pathOrItem.publicUrl ||
      pathOrItem.url ||
      pathOrItem.optimizedUrl ||
      pathOrItem.path ||
      "";
  }

  // Blob/data URLs used for local instant previews
  if (
    typeof path === "string" &&
    (path.startsWith("blob:") || path.startsWith("data:"))
  ) {
    return path;
  }

  // Local development CDN rewrite:
  // In development only, rewrite media.ethosdancestudio.com/trainers/... to http://localhost:5000/uploads/trainers/...
  // and media.ethosdancestudio.com/profile-photos/... to http://localhost:5000/uploads/profile-photos/...
  // Never rewrite in production.
  if (isDev && typeof path === "string") {
    if (path.includes("media.ethosdancestudio.com/trainers/")) {
      const subpath = path.split("media.ethosdancestudio.com/trainers/")[1];
      if (subpath) {
        return `${API_BASE_URL}/uploads/trainers/${subpath}`;
      }
    }
    if (path.includes("media.ethosdancestudio.com/profile-photos/")) {
      const subpath = path.split("media.ethosdancestudio.com/profile-photos/")[1];
      if (subpath) {
        return `${API_BASE_URL}/uploads/profile-photos/${subpath}`;
      }
    }
  }

  // Already an absolute URL (in production, preserves Cloudflare R2 public URL)
  if (typeof path === "string" && /^https?:\/\//i.test(path)) {
    return path;
  }

  if (!path) {
    return id ? `${API_BASE_URL}/api/media/content/${id}` : "";
  }

  // Make sure we don't create //uploads
  const normalizedPath = typeof path === "string" && path.startsWith("/")
    ? path
    : `/${path}`;

  return `${API_BASE_URL}${normalizedPath}`;
}

export const resolveMediaUrl = getMediaUrl;

export const DEFAULT_AVATAR_PLACEHOLDER = "/images/avatar-placeholder.svg";

export const ETHOS_DEFAULT_TRAINER_AVATAR = `data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100" height="100"><defs><linearGradient id="eg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="%231e293b"/><stop offset="100%" stop-color="%230f172a"/></linearGradient><linearGradient id="og" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="%23FF5500"/><stop offset="100%" stop-color="%23FF8800"/></linearGradient></defs><circle cx="50" cy="50" r="49" fill="url(%23eg)" stroke="url(%23og)" stroke-width="2"/><circle cx="50" cy="36" r="15" fill="%23cbd5e1"/><path d="M22 80 C24 60, 36 54, 50 54 C64 54, 76 60, 78 80 Z" fill="%23cbd5e1"/></svg>`;

export const ETHOS_MEDIA_FALLBACK_SVG = `data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 400" width="600" height="400"><rect width="100%" height="100%" fill="%230b0f19"/><path d="M250 160 L350 200 L250 240 Z" fill="%23f97316" opacity="0.4"/><text x="50%" y="70%" text-anchor="middle" fill="%2364748b" font-family="sans-serif" font-size="16" font-weight="600" letter-spacing="2">ETHOS DANCE STUDIO</text></svg>`;

export function getTrainerPhotoUrl(trainer, fallbackToPlaceholder = true) {
  if (!trainer) {
    return fallbackToPlaceholder ? DEFAULT_AVATAR_PLACEHOLDER : "";
  }

  if (typeof trainer === "string") {
    const resolved = resolveMediaUrl(trainer);
    return resolved || (fallbackToPlaceholder ? DEFAULT_AVATAR_PLACEHOLDER : "");
  }

  const raw =
    trainer.profilePhotoUrl ||
    trainer.photoUrl ||
    trainer.profileImageUrl ||
    trainer.imageUrl ||
    trainer.photo ||
    trainer.avatarUrl ||
    trainer.profileImage ||
    trainer.image;

  if (!raw) {
    return fallbackToPlaceholder ? DEFAULT_AVATAR_PLACEHOLDER : "";
  }

  const resolved = resolveMediaUrl(raw);
  return resolved || (fallbackToPlaceholder ? DEFAULT_AVATAR_PLACEHOLDER : "");
}

/**
 * Returns deterministic 2-letter uppercase initials for a trainer name or object.
 */
export function getTrainerInitials(nameOrTrainer) {
  const name = typeof nameOrTrainer === "string" ? nameOrTrainer : getTrainerDisplayName(nameOrTrainer);
  if (!name || name === "Unnamed Trainer") return "ED";
  const words = name.trim().split(/\s+/).filter(Boolean);
  if (words.length >= 2) {
    return `${words[0][0]}${words[1][0]}`.toUpperCase();
  }
  return (words[0] || "ED").slice(0, 2).toUpperCase();
}

/**
 * Robust two-layer error fallback handler for trainer <img> tags.
 * Falls back to DEFAULT_AVATAR_PLACEHOLDER first, and if that also fails or is already active,
 * falls back to the self-contained inline SVG ETHOS_DEFAULT_TRAINER_AVATAR.
 */
export function handleTrainerImgError(e) {
  const img = e.currentTarget;
  if (!img) return;

  if (img.src && !img.src.includes("avatar-placeholder") && !img.src.startsWith("data:image/svg+xml")) {
    img.src = DEFAULT_AVATAR_PLACEHOLDER;
  } else {
    img.onerror = null;
    img.src = ETHOS_DEFAULT_TRAINER_AVATAR;
  }
}

/**
 * Non-blocking general media image error handler.
 * Swaps to fallbackSrc (or default branded SVG) instantly without layout thrashing or unhandled exceptions.
 */
export function handleMediaImgError(e, fallbackSrc = ETHOS_MEDIA_FALLBACK_SVG) {
  const img = e.currentTarget;
  if (!img) return;
  img.onerror = null;
  img.src = fallbackSrc;
}

/**
 * Appends version or cache-busting timestamp to mutable media assets (Safeguard #4).
 */
export function getCacheBustedUrl(url, version) {
  if (!url || typeof url !== "string") return url;
  const separator = url.includes("?") ? "&" : "?";
  const v = version || Date.now();
  return `${url}${separator}v=${v}`;
}

/**
 * Canonical deterministic trainer display name resolver.
 * Priority: fullName -> name -> displayName -> user.firstName + user.lastName -> email -> "Unnamed Trainer"
 */
export function getTrainerDisplayName(t) {
  if (!t) return "Unnamed Trainer";
  if (typeof t === "string") {
    const trimmed = t.trim();
    return trimmed || "Unnamed Trainer";
  }

  if (t.fullName && t.fullName.trim()) return t.fullName.trim();
  if (t.name && t.name.trim()) return t.name.trim();
  if (t.displayName && t.displayName.trim()) return t.displayName.trim();

  if (t.user) {
    const userFirst = t.user.firstName || "";
    const userLast = t.user.lastName || "";
    const combined = `${userFirst} ${userLast}`.trim();
    if (combined) return combined;
  }

  if (t.email && t.email.trim()) return t.email.trim();

  return "Unnamed Trainer";
}
