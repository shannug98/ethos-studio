import { useEffect, useMemo, useState, useRef } from "react";
import { Play, Pause, ChevronLeft, ChevronRight, Maximize2 } from "lucide-react";

import { publicApi } from "../services/publicApi";
import { getMediaUrl, handleMediaImgError } from "../utils/mediaUrl";
import { buildNormalizedShowcaseVideos } from "../utils/galleryPresentation";

import "../styles/gallery.css";

const categories = [
  "ALL",
  "WORKSHOPS",
  "PERFORM",
  "SANGEETH",
  "COMMUNITY",
  "BEHIND THE SCENES",
];

function Gallery() {
  const [activeCategory, setActiveCategory] = useState("ALL");
  const [selectedItem, setSelectedItem] = useState(null);
  const [dynamicVideos, setDynamicVideos] = useState([]);
  const [slideshowMedia, setSlideshowMedia] = useState([]);
  const [cloudMedia, setCloudMedia] = useState([]);

  useEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: "instant" });
  }, []);

  useEffect(() => {
    let isMounted = true;

    // Load gallery featured slideshow (GallerySlideshow section)
    publicApi
      .getPublicMedia({ section: "GallerySlideshow" })
      .then((data) => {
        if (isMounted && Array.isArray(data)) {
          setSlideshowMedia(data);
        }
      })
      .catch((err) => {
        console.warn("[Gallery] Unable to load slideshow media:", err);
      });

    // Load gallery videos (GalleryVideos section)
    publicApi
      .getPublicMedia({ section: "GalleryVideos" })
      .then((data) => {
        if (isMounted && Array.isArray(data)) {
          setDynamicVideos(data);
        }
      })
      .catch((err) => {
        console.warn("[Gallery] Unable to load gallery videos:", err);
      });

    // Load cloud media assets for gallery grid (GalleryImages section)
    publicApi
      .getPublicMedia({ section: "GalleryImages" })
      .then((data) => {
        if (isMounted && Array.isArray(data)) {
          const mapped = data.map((m) => {
            let className = "gallery-item--standard";
            if (m.layoutType === "portrait_3_4" || m.layoutType === "Portrait") className = "gallery-item--tall";
            else if (m.layoutType === "landscape_16_9" || m.layoutType === "Landscape" || m.layoutType === "featured" || m.layoutType === "Featured") className = "gallery-item--wide";

            let cat = (m.category || "COMMUNITY").toUpperCase();
            if (cat === "GENERAL") cat = "COMMUNITY";

            return {
              id: m.id,
              type: (m.mediaType || "image").toLowerCase(),
              src: getMediaUrl(m),
              category: cat,
              title: m.title || "Ethos Moment",
              className,
            };
          });
          setCloudMedia(mapped);
        }
      })
      .catch((err) => {
        console.warn("[Gallery] Unable to load cloud media:", err);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const allItems = useMemo(() => {
    return cloudMedia;
  }, [cloudMedia]);

  const filteredItems = useMemo(() => {
    if (activeCategory === "ALL") {
      return allItems;
    }

    return allItems.filter(
      (item) => item.category === activeCategory
    );
  }, [activeCategory, allItems]);

  // Normalized video showcase presentation model (dynamic API reels)
  const normalizedShowcaseVideos = useMemo(() => {
    return buildNormalizedShowcaseVideos(dynamicVideos, []);
  }, [dynamicVideos]);

  // Dynamic image columns for top moving wall (GallerySlideshow section)
  const motionColumns = useMemo(() => {
    if (slideshowMedia && slideshowMedia.length > 0) {
      const urls = slideshowMedia.map((m) => getMediaUrl(m)).filter(Boolean);
      if (urls.length > 0) {
        const col1 = [];
        const col2 = [];
        const col3 = [];
        urls.forEach((url, i) => {
          if (i % 3 === 0) col1.push(url);
          else if (i % 3 === 1) col2.push(url);
          else col3.push(url);
        });
        const fillTrack = (arr) => {
          const base = arr.length > 0 ? arr : urls;
          let res = [...base];
          while (res.length < 8) res = res.concat(base);
          return res.slice(0, 10);
        };
        return [fillTrack(col1), fillTrack(col2), fillTrack(col3)];
      }
    }

    return [[], [], []];
  }, [slideshowMedia]);

  const [featuredIndex, setFeaturedIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(false);
  const [isMuted, setIsMuted] = useState(true);
  const featuredVideoRef = useRef(null);

  // Safely clamp featuredIndex if video list length changes
  const currentFeaturedVideo = normalizedShowcaseVideos[featuredIndex] || normalizedShowcaseVideos[0];

  // At any time, strictly at most ONE <video> element exists in the DOM.
  // When the video lightbox is open, the background featured <video> is unmounted and replaced by its poster <img>.
  const isLightboxVideoOpen = Boolean(selectedItem && selectedItem.type === "video");

  const handlePrevVideo = () => {
    setFeaturedIndex((prev) => (prev > 0 ? prev - 1 : normalizedShowcaseVideos.length - 1));
    setIsPlaying(false);
  };

  const handleNextVideo = () => {
    setFeaturedIndex((prev) => (prev < normalizedShowcaseVideos.length - 1 ? prev + 1 : 0));
    setIsPlaying(false);
  };

  const togglePlay = () => {
    if (!featuredVideoRef.current) return;
    if (isPlaying) {
      featuredVideoRef.current.pause();
      setIsPlaying(false);
    } else {
      featuredVideoRef.current.play().then(() => setIsPlaying(true)).catch(() => {});
    }
  };

  const getCarouselPosition = (index) => {
    const total = normalizedShowcaseVideos.length;
    if (total <= 1) return "active";

    let offset = index - featuredIndex;
    if (offset > total / 2) offset -= total;
    if (offset < -total / 2) offset += total;

    if (offset === 0) return "active";
    if (offset === -1) return "previous";
    if (offset === 1) return "next";
    if (offset === -2) return "previous-2";
    if (offset === 2) return "next-2";
    return "hidden";
  };

  const touchStartX = useRef(null);

  const handleTouchStart = (event) => {
    if (event.touches && event.touches[0]) {
      touchStartX.current = event.touches[0].clientX;
    }
  };

  const handleTouchEnd = (event) => {
    if (touchStartX.current == null) return;
    const touchEndX = event.changedTouches?.[0]?.clientX;
    if (touchEndX == null) return;
    const delta = touchEndX - touchStartX.current;
    touchStartX.current = null;

    if (Math.abs(delta) < 45) return;

    if (delta < 0) {
      handleNextVideo();
    } else {
      handlePrevVideo();
    }
  };

  // Pause featured video if lightbox modal opens
  useEffect(() => {
    if (selectedItem && featuredVideoRef.current) {
      featuredVideoRef.current.pause();
      setIsPlaying(false);
    }
  }, [selectedItem]);

  useEffect(() => {
    if (!selectedItem) {
      document.body.style.overflow = "";
      return;
    }

    document.body.style.overflow = "hidden";

    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        setSelectedItem(null);
      }
    };

    window.addEventListener("keydown", handleKeyDown);

    return () => {
      document.body.style.overflow = "";
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [selectedItem]);

  const openLightbox = (item) => {
    if (featuredVideoRef.current) {
      featuredVideoRef.current.pause();
    }
    setIsPlaying(false);
    setSelectedItem(item.type ? item : { ...item, type: "video" });
  };

  const closeLightbox = () => {
    setSelectedItem(null);
  };

  return (
    <main className="gallery-page">

      {/* =====================================================
          HERO WITH CONTINUOUSLY MOVING IMAGE WALL
          ===================================================== */}

      <section className="gallery-hero">

        <div className="gallery-hero__background" />

        <div className="gallery-hero__layout">

          {/* LEFT — EXISTING GALLERY INTRO */}
          <div className="gallery-hero__content">
            <p className="gallery-hero__eyebrow">
              THE ETHOS ARCHIVE
            </p>

            <h1>
              GALLERY
            </h1>

            <p className="gallery-hero__intro">
              A visual collection of movement, people,
              performances and moments that make Ethos.
            </p>
          </div>

          {/* RIGHT — MOVING IMAGE WALL */}
          <div className="gallery-motion-wall">
            {motionColumns[0].length > 0 ? (
              <>
                {/* COLUMN 1 — UP on desktop / ROW 1 — LEFT on mobile */}
                <div className="gallery-motion-column gallery-motion-column--up">
                  <div className="gallery-motion-track">
                    {motionColumns[0].concat(motionColumns[0]).map((src, i) => (
                      <img key={i} src={src} alt="" onError={(e) => handleMediaImgError(e)} />
                    ))}
                  </div>
                </div>

                {/* COLUMN 2 — DOWN on desktop / ROW 2 — RIGHT on mobile */}
                <div className="gallery-motion-column gallery-motion-column--down">
                  <div className="gallery-motion-track">
                    {motionColumns[1].concat(motionColumns[1]).map((src, i) => (
                      <img key={i} src={src} alt="" onError={(e) => handleMediaImgError(e)} />
                    ))}
                  </div>
                </div>

                {/* COLUMN 3 — UP on desktop */}
                <div className="gallery-motion-column gallery-motion-column--up">
                  <div className="gallery-motion-track">
                    {motionColumns[2].concat(motionColumns[2]).map((src, i) => (
                      <img key={i} src={src} alt="" onError={(e) => handleMediaImgError(e)} />
                    ))}
                  </div>
                </div>
              </>
            ) : (
              <div className="gallery-motion-wall__empty" />
            )}
          </div>

        </div>

      </section>

      {/* =====================================================
          FEATURED MOVEMENT — CINEMATIC VIDEO SHOWCASE
          ===================================================== */}
      <section className="gallery-showcase-section">
        <div className="gallery-showcase-header">
          <div>
            <p className="gallery-section-label">FEATURED MOVEMENT</p>
            <h2>
              MOVEMENT,
              <br />
              <em>CAPTURED.</em>
            </h2>
          </div>
          <p className="gallery-showcase-subtitle">
            A curated collection of explosive routines, masterclasses, and visual films produced at Ethos.
          </p>
        </div>

        {normalizedShowcaseVideos.length > 0 ? (
          <>
            {/* 3D Video Carousel Stage (Single-video rule strictly maintained) */}
            <div
              className="gallery-video-carousel"
              onTouchStart={handleTouchStart}
              onTouchEnd={handleTouchEnd}
            >
              {normalizedShowcaseVideos.map((video, idx) => {
                const position = getCarouselPosition(idx);
                const isActive = position === "active";

                return (
                  <div
                    key={video.id}
                    className={`gallery-carousel-card ${isActive ? "is-active" : ""}`}
                    data-position={position}
                    onClick={() => {
                      if (!isActive) {
                        setFeaturedIndex(idx);
                        setIsPlaying(false);
                      }
                    }}
                    role={isActive ? undefined : "button"}
                    tabIndex={isActive ? undefined : 0}
                    aria-label={isActive ? undefined : `Switch to ${video.title}`}
                    onKeyDown={(e) => {
                      if (!isActive && (e.key === "Enter" || e.key === " ")) {
                        e.preventDefault();
                        setFeaturedIndex(idx);
                        setIsPlaying(false);
                      }
                    }}
                  >
                    {/* Media: Active video auto-streams; preview cards show poster or muted preview */}
                    {isActive ? (
                      isLightboxVideoOpen ? (
                        <img
                          src={video.poster || video.src}
                          alt={video.title}
                          className="gallery-carousel-media"
                          onError={(e) => handleMediaImgError(e)}
                        />
                      ) : (
                        <video
                          key={video.id}
                          ref={featuredVideoRef}
                          className="gallery-carousel-media"
                          src={video.src}
                          poster={video.poster}
                          playsInline
                          autoPlay
                          loop
                          muted={isMuted}
                          preload="auto"
                          onPlay={() => setIsPlaying(true)}
                          onPause={() => setIsPlaying(false)}
                          onEnded={() => setIsPlaying(false)}
                          onClick={togglePlay}
                        />
                      )
                    ) : (
                      video.poster ? (
                        <img
                          src={video.poster}
                          alt={video.title}
                          className="gallery-carousel-media"
                          loading="lazy"
                          onError={(e) => handleMediaImgError(e)}
                        />
                      ) : (
                        <video
                          src={video.src}
                          className="gallery-carousel-media"
                          preload="metadata"
                          muted
                          playsInline
                        />
                      )
                    )}

                    {/* Center Play Button Overlay for active video (if paused) */}
                    {isActive && !isLightboxVideoOpen && !isPlaying && (
                      <button
                        type="button"
                        className="gallery-video-play"
                        onClick={togglePlay}
                        aria-label={`Play ${video.title}`}
                      >
                        <Play size={28} fill="#171513" style={{ marginLeft: "4px" }} />
                      </button>
                    )}

                    {/* Translucent Play Icon on side preview cards matching reference */}
                    {!isActive && (
                      <div className="gallery-card-preview-play">
                        <Play size={20} fill="rgba(255, 255, 255, 0.8)" style={{ marginLeft: "2px" }} />
                      </div>
                    )}

                    {/* Sound Toggle Button (active card only) */}
                    {isActive && !isLightboxVideoOpen && (
                      <button
                        type="button"
                        className="gallery-video-sound-btn"
                        onClick={(e) => {
                          e.stopPropagation();
                          const nextMuted = !isMuted;
                          setIsMuted(nextMuted);
                          if (featuredVideoRef.current) {
                            featuredVideoRef.current.muted = nextMuted;
                          }
                        }}
                        title={isMuted ? "Unmute audio" : "Mute audio"}
                        aria-label={isMuted ? "Unmute audio" : "Mute audio"}
                      >
                        {isMuted ? "🔇" : "🔊"}
                      </button>
                    )}

                    {/* Expand to Lightbox Modal (active card only) */}
                    {isActive && (
                      <button
                        type="button"
                        className="gallery-video-expand-btn"
                        onClick={() => openLightbox(video)}
                        title="Open full screen lightbox"
                        aria-label="Open full screen lightbox"
                      >
                        <Maximize2 size={16} />
                      </button>
                    )}

                    {/* Cinematic Metadata Overlay on active card */}
                    {isActive && (
                      <div className="gallery-featured-info">
                        <span className="gallery-featured-category">
                          {video.category}
                        </span>
                        <h3 className="gallery-featured-title">
                          {video.title}
                        </h3>
                        {video.description && (
                          <p className="gallery-featured-description">
                            {video.description}
                          </p>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>

            {/* Controls Bar: Counter + Pagination Dots + Circular Navigation */}
            <div className="gallery-showcase-controls-bar">
              <div className="gallery-video-counter" aria-label={`Video ${featuredIndex + 1} of ${normalizedShowcaseVideos.length}`}>
                <span className="gallery-video-counter-current">
                  {String(featuredIndex + 1).padStart(2, "0")}
                </span>
                <span className="gallery-video-counter-divider">/</span>
                <span className="gallery-video-counter-total">
                  {String(normalizedShowcaseVideos.length).padStart(2, "0")}
                </span>
              </div>

              {/* Center Pagination Dots */}
              <div className="gallery-carousel-dots" role="tablist" aria-label="Video carousel pagination">
                {normalizedShowcaseVideos.map((vid, idx) => (
                  <button
                    key={vid.id || idx}
                    type="button"
                    className={`gallery-carousel-dot ${idx === featuredIndex ? "active" : ""}`}
                    onClick={() => {
                      setFeaturedIndex(idx);
                      setIsPlaying(false);
                    }}
                    aria-label={`Go to video ${idx + 1}: ${vid.title}`}
                    role="tab"
                    aria-selected={idx === featuredIndex}
                  />
                ))}
              </div>

              <div className="gallery-video-nav">
                <button
                  type="button"
                  onClick={handlePrevVideo}
                  aria-label="Previous featured video"
                  title="Previous video"
                >
                  <ChevronLeft size={20} />
                </button>
                <button
                  type="button"
                  onClick={handleNextVideo}
                  aria-label="Next featured video"
                  title="Next video"
                >
                  <ChevronRight size={20} />
                </button>
              </div>
            </div>
          </>
        ) : (
          <div className="gallery-showcase-empty">
            <p>Cinematic video reels are being prepared for the new season.</p>
          </div>
        )}
      </section>

      {/* =====================================================
          FILTERS
          ===================================================== */}

      <section className="gallery-archive">

        <div className="gallery-archive__top">

          <div>
            <p className="gallery-section-label">
              VISUAL ARCHIVE
            </p>

            <h2>
              MOMENTS
              <br />
              <em>THAT MOVE.</em>
            </h2>
          </div>

          <p className="gallery-archive__description">
            Explore Ethos through workshops, performances,
            celebrations, community and everything between.
          </p>

        </div>

        <div className="gallery-filters">

          {categories.map((category) => (
            <button
              key={category}
              type="button"
              className={
                activeCategory === category
                  ? "gallery-filter gallery-filter--active"
                  : "gallery-filter"
              }
              onClick={() => setActiveCategory(category)}
            >
              {category}
            </button>
          ))}

        </div>

        {/* =================================================
            MASONRY
            ================================================= */}

        {filteredItems.length > 0 ? (
          <div className="gallery-grid">
            {filteredItems.map((item, index) => (
              <button
                key={item.id}
                type="button"
                className={`gallery-item ${item.className}`}
                onClick={() => openLightbox(item)}
                style={{
                  "--gallery-index": index,
                }}
              >
                <img
                  src={item.src}
                  alt={item.title}
                  loading="lazy"
                  onError={(e) => handleMediaImgError(e)}
                />

                <span className="gallery-item__shade" />

                <span className="gallery-item__info">
                  <span className="gallery-item__category">
                    {item.category}
                  </span>

                  <span className="gallery-item__title">
                    {item.title}
                  </span>
                </span>

                <span className="gallery-item__arrow">
                  ↗
                </span>
              </button>
            ))}
          </div>
        ) : (
          <div className="gallery-empty-category">
            <p>
              No photos uploaded for {activeCategory === "ALL" ? "the gallery" : `the "${activeCategory}" category`} yet.
            </p>
          </div>
        )}
      </section>

      {/* =====================================================
          LIGHTBOX
          ===================================================== */}

      {selectedItem && (
        <div
          className="gallery-lightbox"
          role="dialog"
          aria-modal="true"
          aria-label={selectedItem.title}
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              closeLightbox();
            }
          }}
        >

          <button
            type="button"
            className="gallery-lightbox__close"
            onClick={closeLightbox}
            aria-label="Close gallery"
          >
            <span />
            <span />
          </button>

          <div className="gallery-lightbox__content">

            {selectedItem.type === "video" ? (
              <video
                className="gallery-lightbox__video"
                src={selectedItem.src}
                controls
                autoPlay
                playsInline
              />
            ) : (
              <img
                src={selectedItem.src}
                alt={selectedItem.title}
                className="gallery-lightbox__image"
                onError={(e) => handleMediaImgError(e)}
              />
            )}

            <div className="gallery-lightbox__caption">
              <span>{selectedItem.title}</span>

              {selectedItem.category && (
                <span>
                  {selectedItem.category}
                </span>
              )}
            </div>

          </div>

        </div>
      )}

    </main>
  );
}

export default Gallery;
