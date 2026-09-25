import { getMediaUrl } from "./mediaUrl.js";

/**
 * Normalizes video media items from Cloudflare R2 API response
 * and combines them with curated studio default video reels.
 * Guarantees deduplication by video URL (src).
 *
 * @param {Array} dynamicVideos - Media items from API (section: GalleryVideos)
 * @param {Array} defaultVideos - Curated fallback video objects
 * @returns {Array} Normalized showcase video items
 */
export function buildNormalizedShowcaseVideos(dynamicVideos = [], defaultVideos = []) {
  const fallbackList = Array.isArray(defaultVideos) ? defaultVideos : [];

  const apiVideos = (dynamicVideos || []).map((d, i) => {
    const defaultItem = fallbackList[i % (fallbackList.length || 1)] || {};
    const rawSrc = d.publicUrl || d.url || d.src;
    const src = rawSrc ? getMediaUrl(rawSrc, d.id) : (defaultItem.src || "");

    const poster = d.thumbnailUrl
      ? getMediaUrl(d.thumbnailUrl, d.id)
      : (d.posterUrl
          ? getMediaUrl(d.posterUrl, d.id)
          : (defaultItem.poster || ""));

    return {
      id: d.id || `gallery-api-video-${i}`,
      type: "video",
      src,
      poster,
      category: (d.category || "PERFORMANCE").toUpperCase(),
      title: d.title || `Performance Reel ${i + 1}`,
      description: d.description || "Curated movement showcase film produced at Ethos Dance Studio.",
    };
  });

  if (apiVideos.length === 0) {
    return fallbackList;
  }

  // Deduplicate against dynamic video URLs
  const apiUrls = new Set(apiVideos.map((v) => v.src));
  const uniqueDefaults = fallbackList.filter((v) => !apiUrls.has(v.src));

  return [...apiVideos, ...uniqueDefaults];
}
