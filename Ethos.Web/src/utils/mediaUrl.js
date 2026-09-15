import { API_BASE_URL } from "../config/api";

export function getMediaUrl(path) {
  if (!path) {
    return "";
  }

  // Already an absolute URL
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  // Blob/data URLs used for local previews
  if (
    path.startsWith("blob:") ||
    path.startsWith("data:")
  ) {
    return path;
  }

  // Make sure we don't create //uploads
  const normalizedPath = path.startsWith("/")
    ? path
    : `/${path}`;

  return `${API_BASE_URL}${normalizedPath}`;
}
