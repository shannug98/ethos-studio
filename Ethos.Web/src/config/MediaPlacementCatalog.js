/**
 * MediaPlacementCatalog.js
 * 
 * Official authoritative catalog for all Ethos Dance Studio website media placements,
 * matching the "Ethos Dance Studio — Media Library Rebuild" specification.
 */

export const MEDIA_LIMITS = {
  IMAGE_MAX_BYTES: 5 * 1024 * 1024,          // 5 MB
  HOMEPAGE_REEL_MAX_BYTES: 100 * 1024 * 1024, // 100 MB
  HERO_VIDEO_MAX_BYTES: 100 * 1024 * 1024,    // 100 MB
  GALLERY_VIDEO_MAX_BYTES: 500 * 1024 * 1024, // 500 MB
  REELS_MAX_DURATION_SECONDS: 60,
  HERO_VIDEO_MAX_DURATION_SECONDS: 60,
};

export const GALLERY_CATEGORIES = [
  "Workshops",
  "Perform",
  "Sangeeth",
  "Community",
  "Behind the Scenes",
];

export const MEDIA_PLACEMENTS = [
  // ── 1. HOMEPAGE HERO BANNER ────────────────────────────────────────────────
  {
    id: "hero-banner",
    area: "Homepage",
    placement: "Hero Banner",
    sectionKey: "HomepageScrolling",
    type: "fixed_slots",
    slotCount: 6,
    limit: 6,
    allowedMedia: "image_video",
    allowedMediaLabel: "Image / Video",
    behavior: "Fixed slots; public auto-rotation every 10 seconds",
    aspectRatio: "16:9 Widescreen",
    cropConfig: {
      aspectRatio: "16:9",
      allowedRatios: ["16:9"],
      title: "Crop Hero Banner Slide (16:9 — 1920×1080)",
    },
    recommendedResolution: "1920 × 1080",
    maxSizeBytes: MEDIA_LIMITS.HERO_VIDEO_MAX_BYTES,
    maxImageBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    maxVideoBytes: MEDIA_LIMITS.HERO_VIDEO_MAX_BYTES,
    maxDurationSeconds: 60,
    pageRoute: "/",
    componentName: "Hero.jsx",
    wireframeType: "hero_banner",
    description: "Top of homepage above fold. Full-width widescreen rotating slideshow with studio headline, subtitle, and CTA buttons.",
    slots: [
      { order: 1, label: "Hero Slot 01", role: "Primary Headline Opening", recommended: "1920×1080 Landscape" },
      { order: 2, label: "Hero Slot 02", role: "Movement & Atmosphere", recommended: "1920×1080 Landscape" },
      { order: 3, label: "Hero Slot 03", role: "Video Showcase / Dynamic Leap", recommended: "1920×1080 Landscape Video (≤60s)" },
      { order: 4, label: "Hero Slot 04", role: "Choreography & Class Dynamics", recommended: "1920×1080 Landscape" },
      { order: 5, label: "Hero Slot 05", role: "Stage Performance / Rhythm", recommended: "1920×1080 Landscape" },
      { order: 6, label: "Hero Slot 06", role: "Studio Collective Finale", recommended: "1920×1080 Landscape" },
    ],
  },

  // ── 2. HOMEPAGE REELS ─────────────────────────────────────────────────────
  {
    id: "homepage-reels",
    area: "Homepage",
    placement: "Homepage Reels",
    sectionKey: "HomepageReels",
    type: "fixed_slots",
    slotCount: 20,
    limit: 20,
    allowedMedia: "video_only",
    allowedMediaLabel: "Video only",
    behavior: "20 Fixed slots; portrait dance reels carousel",
    aspectRatio: "9:16 Portrait",
    recommendedResolution: "1080 × 1920",
    maxSizeBytes: MEDIA_LIMITS.HOMEPAGE_REEL_MAX_BYTES,
    maxVideoBytes: MEDIA_LIMITS.HOMEPAGE_REEL_MAX_BYTES,
    maxDurationSeconds: 60,
    pageRoute: "/",
    componentName: "ShortDanceVideos.jsx",
    wireframeType: "reels_carousel",
    description: "Portrait vertical dance reels strip on homepage with modal video player, sound toggle, and smooth scrolling carousel.",
    slots: Array.from({ length: 20 }, (_, i) => ({
      order: i + 1,
      label: `Reel Slot ${String(i + 1).padStart(2, "0")}`,
      role: i === 0 ? "Featured Lead Reel" : `Studio Showcase Reel ${String(i + 1).padStart(2, "0")}`,
      recommended: "1080×1920 (9:16 Video, ≤60s)",
    })),
  },

  // ── 3. WE ARE ETHOS ───────────────────────────────────────────────────────
  {
    id: "we-are-ethos",
    area: "Homepage",
    placement: "We Are Ethos",
    sectionKey: "AboutEthos",
    type: "fixed_slots",
    slotCount: 3,
    limit: 3,
    allowedMedia: "image_only",
    allowedMediaLabel: "Image only",
    behavior: "Fixed slots",
    aspectRatio: "Slot 1: 4:5 | Slot 2: 16:9 | Slot 3: 1:1",
    cropConfig: {
      aspectRatio: "4:5",
      allowedRatios: ["4:5"],
      title: "Crop Studio Portrait (4:5 — 1200×1500)",
    },
    recommendedResolution: "1200×1500 (4:5), 1920×1080 (16:9), 1080×1080 (1:1)",
    maxSizeBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    maxImageBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    pageRoute: "/",
    componentName: "About.jsx",
    wireframeType: "about_trio",
    description: "About Ethos section on homepage. 3-photo narrative composition highlighting studio identity, movement discipline, and community spirit.",
    slots: [
      {
        order: 1,
        label: "Slot 01: Main Studio",
        role: "Main Studio Portrait (Primary)",
        recommended: "1200×1500 (4:5 Portrait)",
        cropConfig: {
          aspectRatio: "4:5",
          allowedRatios: ["4:5"],
          title: "Crop Studio Portrait (4:5 — 1200×1500)",
        },
      },
      {
        order: 2,
        label: "Slot 02: Movement",
        role: "Movement & Technique (Secondary)",
        recommended: "1920×1080 (16:9 Landscape)",
        cropConfig: {
          aspectRatio: "16:9",
          allowedRatios: ["16:9"],
          title: "Crop Movement & Technique (16:9 — 1920×1080)",
        },
      },
      {
        order: 3,
        label: "Slot 03: Community",
        role: "Community Energy (Accent)",
        recommended: "1080×1080 (1:1 Square)",
        cropConfig: {
          aspectRatio: "1:1",
          allowedRatios: ["1:1"],
          title: "Crop Community Energy (1:1 — 1080×1080)",
        },
      },
    ],
  },

  // ── 4. FOUNDER ────────────────────────────────────────────────────────────
  {
    id: "founder",
    area: "Homepage",
    placement: "Founder",
    sectionKey: "Founders",
    type: "fixed_slots",
    slotCount: 1,
    limit: 1,
    allowedMedia: "image_only",
    allowedMediaLabel: "Image only",
    behavior: "Fixed slot",
    aspectRatio: "16:9 or 4:3",
    cropConfig: {
      aspectRatio: "3:4",
      allowedRatios: ["3:4"],
      title: "Crop Co-Founders Portrait (3:4 — 900×1200)",
    },
    recommendedResolution: "1600 × 900 or 1400 × 1050",
    maxSizeBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    maxImageBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    pageRoute: "/",
    componentName: "Founders.jsx",
    wireframeType: "founder_split",
    description: "Homepage Co-Founders section. High-impact split presentation with co-founder bios, social handles, and artistic philosophy.",
    slots: [
      { order: 1, label: "Slot 01: Co-Founders", role: "Co-Founders Feature Portrait", recommended: "1600×900 (16:9) or 1400×1050 (4:3)" },
    ],
  },

  // ── 5. TRAINERS ───────────────────────────────────────────────────────────
  {
    id: "trainers",
    area: "Homepage",
    placement: "Trainers",
    sectionKey: "Trainers",
    type: "fixed_slots",
    slotCount: 4,
    limit: 4,
    allowedMedia: "image_only",
    allowedMediaLabel: "Image only",
    behavior: "Fixed slots",
    aspectRatio: "3:4 Portrait Poster",
    cropConfig: {
      aspectRatio: "3:4",
      allowedRatios: ["3:4"],
      title: "Crop Master Faculty Poster (3:4 — 900×1200)",
    },
    recommendedResolution: "900 × 1200",
    maxSizeBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    maxImageBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    pageRoute: "/",
    componentName: "Trainers.jsx",
    wireframeType: "trainers_grid",
    description: "Homepage Master Faculty grid. 4 interactive cards with expandable biographies, dance disciplines, and social channels.",
    slots: [
      { order: 1, label: "Trainer 01: Sujith Kumar", role: "Co-Founder & Lead Choreographer", recommended: "900×1200 (3:4 Portrait)" },
      { order: 2, label: "Trainer 02: Tejaswini", role: "Co-Founder & Executive Director", recommended: "900×1200 (3:4 Portrait)" },
      { order: 3, label: "Trainer 03: Rahul Roy", role: "Assistant Choreographer", recommended: "900×1200 (3:4 Portrait)" },
      { order: 4, label: "Trainer 04: Priya Sharma", role: "Urban & Foundations Instructor", recommended: "900×1200 (3:4 Portrait)" },
    ],
  },

  // ── 6. GALLERY FEATURED SLIDESHOW ─────────────────────────────────────────
  {
    id: "gallery-slideshow",
    area: "Gallery",
    placement: "Featured Slideshow",
    sectionKey: "GallerySlideshow",
    type: "fixed_slots",
    slotCount: 20,
    limit: 20,
    allowedMedia: "image_only",
    allowedMediaLabel: "Image only",
    behavior: "20 Fixed slots; three-column vertical presentation",
    aspectRatio: "16:9 Landscape",
    cropConfig: {
      aspectRatio: "16:9",
      allowedRatios: ["16:9"],
      title: "Crop Featured Slideshow (16:9 — 1600×900)",
    },
    recommendedResolution: "1600 × 900",
    maxSizeBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    maxImageBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    pageRoute: "/gallery",
    componentName: "Gallery.jsx",
    wireframeType: "gallery_masonry",
    description: "Featured public photo presentation on /gallery. Three-column vertical layout showcasing studio highlights, workshops, and performances.",
    slots: Array.from({ length: 20 }, (_, i) => ({
      order: i + 1,
      label: `Slide Slot ${String(i + 1).padStart(2, "0")}`,
      role: i === 0 ? "Featured Slideshow Lead" : `Studio Feature Highlight ${String(i + 1).padStart(2, "0")}`,
      recommended: "1600×900 (16:9 Landscape)",
    })),
  },

  // ── 7. GALLERY LARGE VIDEOS ───────────────────────────────────────────────
  {
    id: "gallery-videos",
    area: "Gallery",
    placement: "Large Videos",
    sectionKey: "GalleryVideos",
    type: "fixed_slots",
    slotCount: 8,
    limit: 8,
    allowedMedia: "video_only",
    allowedMediaLabel: "Video only",
    behavior: "8 Fixed slots; cinematic video theater playlist",
    aspectRatio: "16:9 Widescreen",
    recommendedResolution: "1920 × 1080 (60fps)",
    maxSizeBytes: MEDIA_LIMITS.GALLERY_VIDEO_MAX_BYTES,
    maxVideoBytes: MEDIA_LIMITS.GALLERY_VIDEO_MAX_BYTES,
    pageRoute: "/gallery",
    componentName: "Gallery.jsx",
    wireframeType: "gallery_video_player",
    description: "Cinematic video theater on /gallery. Featured full-width theater player with playlist queue for high-resolution productions (supports up to 500MB via R2).",
    slots: Array.from({ length: 8 }, (_, i) => ({
      order: i + 1,
      label: `Theater Slot ${String(i + 1).padStart(2, "0")}`,
      role: i === 0 ? "Theater Feature Premiere" : `Studio Production Video ${String(i + 1).padStart(2, "0")}`,
      recommended: "1920×1080 (16:9 Video, ≤500MB)",
    })),
  },

  // ── 8. GALLERY ALL PHOTOS ─────────────────────────────────────────────────
  {
    id: "gallery-all-photos",
    area: "Gallery",
    placement: "All Photos",
    sectionKey: "GalleryImages",
    type: "collection",
    limit: null,
    allowedMedia: "image_only",
    allowedMediaLabel: "Image only",
    behavior: "Admin selects category",
    aspectRatio: "Mixed (Portrait, Landscape, Square)",
    cropConfig: {
      aspectRatio: "natural",
      allowedRatios: ["natural"],
      title: "Adjust Photo (Original / Natural Aspect Ratio)",
    },
    recommendedResolution: "900×1200, 1600×900, 1080×1080",
    maxSizeBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    maxImageBytes: MEDIA_LIMITS.IMAGE_MAX_BYTES,
    pageRoute: "/gallery",
    componentName: "Gallery.jsx",
    wireframeType: "gallery_categorized",
    description: "All studio photos on /gallery filtered by category: Workshops, Perform, Sangeeth, Community, and Behind the Scenes.",
    categories: GALLERY_CATEGORIES,
  },
];

export const DEVELOPER_OWNED_ASSETS = [
  {
    id: "ethos-emblem",
    name: "Studio Brand Emblem",
    relativePath: "src/assets/brand/ethos-emblem.png",
    dimensions: "512 × 512",
    sizeBytes: 54272,
    purpose: "Studio brand emblem embedded in Navbar, Admin Header, Admin Sidebar, and invoice watermarks. Protected system asset.",
    protectionBadge: "Core Brand Asset",
  },
  {
    id: "letterhead-header",
    name: "Official Letterhead Header",
    relativePath: "src/assets/official-letterhead-header.png",
    dimensions: "1200 × 240",
    sizeBytes: 69632,
    purpose: "Canvas/PDF Receipt Generator. Hardcoded in client-side HTML5 canvas PDF engine for student receipts and ticket downloads.",
    protectionBadge: "PDF Engine Asset",
  },
  {
    id: "letterhead-footer",
    name: "Official Letterhead Footer",
    relativePath: "src/assets/official-letterhead-footer.png",
    dimensions: "1200 × 160",
    sizeBytes: 44032,
    purpose: "Canvas/PDF Receipt Generator. Hardcoded in client-side HTML5 canvas PDF engine for student receipts and ticket downloads.",
    protectionBadge: "PDF Engine Asset",
  },
  {
    id: "avatar-placeholders",
    name: "User Avatar Placeholders",
    relativePath: "public/images/avatar-placeholder.png & .svg",
    dimensions: "256 × 256",
    sizeBytes: 4352,
    purpose: "Default fallback avatars for students, guest accounts, and trainers without profile photos.",
    protectionBadge: "System UI Fallback",
  },
];

export function getPlacementById(id) {
  return MEDIA_PLACEMENTS.find((p) => p.id === id);
}

export function getPlacementsByArea(area) {
  return MEDIA_PLACEMENTS.filter((p) => p.area.toLowerCase() === area.toLowerCase());
}

/**
 * Authoritative placement crop configuration resolver.
 * Derives crop ratio, selectable ratios, and modal title directly from the MediaPlacementCatalog.
 * Supports slot-specific configs (e.g. We Are Ethos slot 1: 4:5, slot 2: 16:9, slot 3: 1:1)
 * and placement-level configs (e.g. Trainers 3:4, Gallery Slideshow 16:9, Gallery Photos natural).
 *
 * @param {string} placementId
 * @param {number|null} slotOrder
 * @returns {{ aspectRatio: string, allowedRatios: string[], title: string }}
 */
export function getPlacementCropConfig(placementId, slotOrder = null) {
  const SPECIAL_CONFIGS = {
    "workshop-portrait": { aspectRatio: "3:4", allowedRatios: ["3:4", "4:5"], title: "Crop Workshop Portrait Poster" },
    "workshop-landscape": { aspectRatio: "16:9", allowedRatios: ["16:9"], title: "Crop Workshop Landscape Banner" },
    "event-photos": { aspectRatio: "16:9", allowedRatios: ["16:9", "4:3"], title: "Crop Event Photo" },
  };
  if (SPECIAL_CONFIGS[placementId]) {
    return SPECIAL_CONFIGS[placementId];
  }

  const placement = getPlacementById(placementId) || MEDIA_PLACEMENTS[0];
  if (!placement) {
    return {
      aspectRatio: "16:9",
      allowedRatios: ["16:9"],
      title: "Crop & Adjust Image",
    };
  }

  // Slot-specific crop configuration (e.g. We Are Ethos slot 1, 2, 3)
  if (slotOrder != null && placement.slots && placement.slots.length > 0) {
    const slot = placement.slots.find((s) => s.order === slotOrder);
    if (slot && slot.cropConfig) {
      return slot.cropConfig;
    }
  }

  // Placement-level crop configuration
  if (placement.cropConfig) {
    return placement.cropConfig;
  }

  // Fallback derived strictly from placement metadata
  const isPortrait = placement.aspectRatio?.includes("3:4") || placement.aspectRatio?.includes("Portrait");
  const isSquare = placement.aspectRatio?.includes("1:1");
  const defaultRatio = isPortrait ? "3:4" : isSquare ? "1:1" : "16:9";

  return {
    aspectRatio: defaultRatio,
    allowedRatios: [defaultRatio],
    title: `Crop Image for ${placement.placement}`,
  };
}
