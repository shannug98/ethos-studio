/**
 * mediaPreviewPresentation.js
 * 
 * Normalized presentation resolver for Ethos Dance Studio Admin Media Library.
 * Maps any MediaItem, placement configuration, and slot into a structured
 * presentation descriptor matching the authentic public presentation of that section.
 */

export function getMediaPreviewPresentation(media, placement, slot) {
  const sec = (placement?.sectionKey || "").toLowerCase();
  const isVideo = (media?.mediaType || "").toLowerCase() === "video";
  const layout = (media?.layoutType || "").toLowerCase();

  // 1. Homepage Hero Banner (HomepageScrolling)
  if (sec === "homepagescrolling") {
    return {
      variant: "hero",
      aspectRatio: "16:9",
      badge: "HERO STAGE",
      headline: "MORE THAN DANCE",
      eyebrow: "ETHOS DANCE STUDIO",
      subtitle: "MOVE. LEARN. BELONG.",
      description: "A place to move with confidence, learn with passion and belong to something bigger.",
      title: media?.title || slot?.label || "Hero Banner",
      role: slot?.role || "Primary Headline Opening",
      isVideo,
    };
  }

  // 2. Homepage Reels (HomepageReels)
  if (sec === "homepagereels") {
    return {
      variant: "reel",
      aspectRatio: "9:16",
      badge: "DANCE REEL",
      title: media?.title || "Contemporary Routine Reel",
      caption: media?.caption || "Quick routine and student choreography in action.",
      isVideo: true,
    };
  }

  // 3. We Are Ethos (AboutEthos)
  if (sec === "aboutethos") {
    const slotOrder = slot?.order || 1;
    const ratio = slotOrder === 2 ? "16:9" : slotOrder === 3 ? "1:1" : "4:5";
    const slotClass = slotOrder === 2 ? "preview-about-landscape" : slotOrder === 3 ? "preview-about-square" : "preview-about-portrait";
    return {
      variant: "about",
      aspectRatio: ratio,
      slotClass,
      badge: "WE ARE ETHOS",
      title: media?.title || slot?.label || "Studio Narrative",
      role: slot?.role || (slotOrder === 1 ? "Main Studio Portrait (Primary)" : slotOrder === 2 ? "Movement & Technique" : "Community Energy"),
      isVideo,
    };
  }

  // 4. Founders (Founders)
  if (sec === "founders" || sec === "founder") {
    return {
      variant: "founder",
      aspectRatio: "3:4",
      badge: "CO-FOUNDERS & ARTISTIC DIRECTORS",
      title: media?.title || "Sujith Kumar & Tejaswini",
      role: "Studio Vision & Leadership",
      isVideo,
    };
  }

  // 5. Trainers (Trainers)
  if (sec === "trainers") {
    return {
      variant: "trainer",
      aspectRatio: "3:4",
      badge: "MASTER FACULTY",
      title: media?.title || slot?.label || "Faculty Choreographer",
      role: slot?.role || "Master Instructor & Choreographer",
      disciplines: "Contemporary · Hip Hop · Urban Choreography",
      isVideo,
    };
  }

  // 6. Gallery Featured Slideshow (GallerySlideshow)
  if (sec === "galleryslideshow") {
    return {
      variant: "gallery-slideshow",
      aspectRatio: "16:9",
      badge: "FEATURED SLIDESHOW",
      title: media?.title || "Studio Motion Wall Highlight",
      isVideo,
    };
  }

  // 7. Gallery Large Videos (GalleryVideos)
  if (sec === "galleryvideos") {
    return {
      variant: "gallery-video",
      aspectRatio: "16:9",
      badge: "CINEMATIC SHOWCASE · THEATER",
      title: media?.title || "Featured Movement Film",
      isVideo: true,
    };
  }

  // 8. Gallery All Photos (GalleryImages)
  if (sec === "galleryimages") {
    return {
      variant: "gallery-photo",
      aspectRatio: "natural",
      badge: `GALLERY · ${(media?.category || "COMMUNITY").toUpperCase()}`,
      title: media?.title || "Ethos Moment",
      caption: media?.caption || "",
      category: media?.category || "Community",
      isVideo,
    };
  }

  // 9. Workshop Landscape (Workshop with Landscape layout)
  if (sec === "workshop" && layout !== "portrait") {
    return {
      variant: "workshop-landscape",
      aspectRatio: "16:9",
      badge: "WORKSHOP BANNER",
      title: media?.title || "Masterclass Workshop Banner",
      role: "Widescreen Schedule & Hero Banner",
      isVideo,
    };
  }

  // 10. Workshop Portrait (Workshop with Portrait layout)
  if (sec === "workshop" && layout === "portrait") {
    return {
      variant: "workshop-portrait",
      aspectRatio: "3:4",
      badge: "WORKSHOP PASS / FLYER",
      title: media?.title || "Workshop Intensive Flyer",
      role: "Vertical Workshop Card & Poster",
      isVideo,
    };
  }

  // 11. Events (Events)
  if (sec === "events") {
    return {
      variant: "event",
      aspectRatio: isVideo ? "16:9" : "16:9",
      badge: "SPECIAL EVENT",
      title: media?.title || "Studio Showcase & Festival",
      role: "Event Feature & Community Production",
      isVideo,
    };
  }

  // Generic fallback
  return {
    variant: "generic",
    aspectRatio: isVideo ? "16:9" : "natural",
    badge: (media?.category || placement?.area || "STUDIO MEDIA").toUpperCase(),
    title: media?.title || "Studio Asset",
    role: placement?.placement || "Visual Asset",
    isVideo,
  };
}
