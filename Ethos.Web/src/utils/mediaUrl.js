import { API_BASE_URL } from "../config/api";

export function getMediaUrl(path, id) {
  if (!path) {
    return id ? `${API_BASE_URL}/api/media/content/${id}` : "";
  }

  // If URL points to media.ethosdancestudio.com which is not yet live on DNS, fallback to API stream
  if (typeof path === "string" && path.includes("media.ethosdancestudio.com")) {
    if (id) return `${API_BASE_URL}/api/media/content/${id}`;
  }

  // Already an absolute URL
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  // Blob/data URLs used for local previews
  if (
    typeof path === "string" &&
    (path.startsWith("blob:") || path.startsWith("data:"))
  ) {
    return path;
  }

  // Make sure we don't create //uploads
  const normalizedPath = path.startsWith("/")
    ? path
    : `/${path}`;

  return `${API_BASE_URL}${normalizedPath}`;
}
