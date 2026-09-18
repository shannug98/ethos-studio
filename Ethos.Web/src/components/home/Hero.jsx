import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { publicApi } from "../../services/publicApi";
import { getMediaUrl } from "../../utils/mediaUrl";
import "../../styles/hero.css";

import hero01 from "../../assets/hero/hero-01.jpg";
import hero02 from "../../assets/hero/hero-02.jpg";
import hero03 from "../../assets/hero/hero-03.jpg";
import hero04 from "../../assets/hero/hero-04.jpg";
import hero05 from "../../assets/hero/hero-05.jpg";
import heroVideo from "../../assets/hero/hero-video.mp4";

const DEFAULT_HERO_SLIDES = [
  {
    id: "default-hero-01",
    type: "image",
    src: hero01,
    alt: "Dancer performing at Ethos Dance Studio",
  },
  {
    id: "default-hero-02",
    type: "image",
    src: hero02,
    alt: "Dancers training together at Ethos",
  },
  {
    id: "default-hero-video",
    type: "video",
    src: heroVideo,
    alt: "Dance performance at Ethos Dance Studio",
  },
  {
    id: "default-hero-03",
    type: "image",
    src: hero03,
    alt: "Dance class at Ethos Dance Studio",
  },
  {
    id: "default-hero-04",
    type: "image",
    src: hero04,
    alt: "Dance performance at Ethos Dance Studio",
  },
  {
    id: "default-hero-05",
    type: "image",
    src: hero05,
    alt: "Ethos Dance Studio performance",
  },
];

function Hero() {
  const [slides, setSlides] = useState(DEFAULT_HERO_SLIDES);
  const [loading, setLoading] = useState(false);
  const [activeSlide, setActiveSlide] = useState(0);
  const [isPaused, setIsPaused] = useState(false);
  const navigate = useNavigate();

  const touchStartX = useRef(null);
  const touchEndX = useRef(null);
  const videoRef = useRef(null);

  // Fetch dynamic cloud media for HomepageScrolling (Images and short muted videos)
  useEffect(() => {
    let isMounted = true;
    publicApi
      .getPublicMedia({ section: "HomepageScrolling" })
      .then((data) => {
        if (isMounted && Array.isArray(data) && data.length > 0) {
          const mapped = data.map((item) => ({
            id: item.id,
            type: (item.mediaType || "Image").toLowerCase() === "video" ? "video" : "image",
            src: getMediaUrl(item.publicUrl, item.id),
            alt: item.altText || item.title || "Ethos Dance Studio",
          }));
          // Prepend cloud media to default slides so newly uploaded assets appear first
          setSlides([...mapped, ...DEFAULT_HERO_SLIDES]);
        }
      })
      .catch((err) => {
        console.warn("[Hero] Public media fetch failed:", err);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const totalSlides = slides.length;

  const nextSlide = () => {
    if (totalSlides === 0) return;
    setActiveSlide((current) =>
      current === totalSlides - 1 ? 0 : current + 1
    );
  };

  const previousSlide = () => {
    if (totalSlides === 0) return;
    setActiveSlide((current) =>
      current === 0 ? totalSlides - 1 : current - 1
    );
  };

  useEffect(() => {
    if (isPaused || totalSlides <= 1) return;

    const currentSlide = slides[activeSlide];
    const slideDuration = currentSlide?.type === "video" ? 10000 : 5000;

    const timer = setTimeout(() => {
      nextSlide();
    }, slideDuration);

    return () => {
      clearTimeout(timer);
    };
  }, [activeSlide, isPaused, totalSlides, slides]);

  useEffect(() => {
    const currentSlide = slides[activeSlide];

    if (currentSlide?.type === "video" && videoRef.current) {
      videoRef.current.currentTime = 0;
      videoRef.current.muted = true; // Strictly muted autoplay
      videoRef.current.play().catch(() => {});
    } else if (videoRef.current) {
      videoRef.current.pause();
    }
  }, [activeSlide, slides]);

  const handleTouchStart = (event) => {
    touchStartX.current = event.touches[0].clientX;
  };

  const handleTouchMove = (event) => {
    touchEndX.current = event.touches[0].clientX;
  };

  const handleTouchEnd = () => {
    if (touchStartX.current === null || touchEndX.current === null) return;
    const distance = touchStartX.current - touchEndX.current;
    const minimumSwipeDistance = 50;

    if (Math.abs(distance) >= minimumSwipeDistance) {
      if (distance > 0) nextSlide();
      else previousSlide();
    }

    touchStartX.current = null;
    touchEndX.current = null;
  };

  const goToSlide = (index) => {
    setActiveSlide(index);
  };

  return (
    <section
      id="home"
      className="ethos-hero"
      onMouseEnter={() => setIsPaused(true)}
      onMouseLeave={() => setIsPaused(false)}
      onTouchStart={handleTouchStart}
      onTouchMove={handleTouchMove}
      onTouchEnd={handleTouchEnd}
    >
      {/* MEDIA SLIDES (Rendered if cloud media exists) */}
      {totalSlides > 0 ? (
        <div className="ethos-hero__media">
          {slides.map((slide, index) => {
            const isActive = index === activeSlide;

            return (
              <div
                key={slide.id || index}
                className={`ethos-hero__slide ${
                  isActive ? "ethos-hero__slide--active" : ""
                }`}
              >
                {slide.type === "image" ? (
                  <img
                    src={slide.src}
                    alt={slide.alt}
                    className="ethos-hero__image"
                  />
                ) : (
                  <video
                    ref={isActive ? videoRef : null}
                    className="ethos-hero__video"
                    src={slide.src}
                    muted
                    loop
                    playsInline
                    autoPlay={isActive}
                    preload="metadata"
                  />
                )}
              </div>
            );
          })}
        </div>
      ) : (
        /* Intentional Sleek Background Gradient when no scrolling media is published (Safeguard 9) */
        <div className="ethos-hero__empty-backdrop" style={{ position: "absolute", inset: 0, background: "radial-gradient(circle at center, #18181b 0%, #09090b 100%)", zIndex: 0 }} />
      )}

      {/* OVERLAY & VIGNETTE */}
      <div className="ethos-hero__overlay" />
      <div className="ethos-hero__vignette" />

      {/* HERO CONTENT */}
      <div className="ethos-hero__content">
        <div className="ethos-hero__eyebrow">ETHOS DANCE STUDIO</div>

        <h1 className="ethos-hero__title">
          <span className="ethos-hero__title-line">MORE THAN</span>
          <span className="ethos-hero__title-line ethos-hero__title-line--accent">
            DANCE
          </span>
        </h1>

        <div className="ethos-hero__divider">
          <span />
        </div>

        <p className="ethos-hero__tagline">MOVE. LEARN. BELONG.</p>

        <p className="ethos-hero__description">
          A place to move with confidence, learn with passion and belong to
          something bigger.
        </p>

        {/* ACTION BUTTONS */}
        <div className="ethos-hero__actions">
          <button
            type="button"
            className="ethos-hero__button ethos-hero__button--primary"
            onClick={() => navigate("/workshops")}
          >
            EXPLORE WORKSHOPS
            <span>↗</span>
          </button>

          <button
            type="button"
            className="ethos-hero__button ethos-hero__button--secondary"
            onClick={() => {
              const el = document.getElementById("contact");
              if (el) el.scrollIntoView({ behavior: "smooth" });
            }}
          >
            BOOK ENQUIRY
          </button>
        </div>
      </div>

      {/* NAVIGATION CONTROLS (Only if multiple slides exist) */}
      {totalSlides > 1 && (
        <>
          <div className="ethos-hero__navigation">
            <button
              type="button"
              className="ethos-hero__nav-button"
              onClick={previousSlide}
              aria-label="Previous slide"
            >
              ←
            </button>

            <button
              type="button"
              className="ethos-hero__nav-button"
              onClick={nextSlide}
              aria-label="Next slide"
            >
              →
            </button>
          </div>

          <div className="ethos-hero__progress">
            {slides.map((_, index) => (
              <button
                key={index}
                type="button"
                className={`ethos-hero__progress-item ${
                  index === activeSlide ? "ethos-hero__progress-item--active" : ""
                }`}
                onClick={() => goToSlide(index)}
                aria-label={`Go to slide ${index + 1}`}
              />
            ))}
          </div>
        </>
      )}

      {/* SCROLL INDICATOR */}
      <div className="ethos-hero__scroll">
        <span className="ethos-hero__scroll-text">SCROLL TO EXPLORE</span>
        <span className="ethos-hero__scroll-line" />
      </div>
    </section>
  );
}

export default Hero;
