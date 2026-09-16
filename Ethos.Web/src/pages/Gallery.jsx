import { useEffect, useMemo, useState } from "react";

import workshop01 from "../assets/workshops/workshop-01.jpg";
import workshop02 from "../assets/workshops/workshop-02.jpg";
import workshop03 from "../assets/workshops/workshop-03.jpg";
import workshop04 from "../assets/workshops/workshop-04.jpg";

import aboutMain from "../assets/about/about-main.jpg";
import aboutSecondary from "../assets/about/about-secondary.jpg";
import aboutCommunity from "../assets/about/about-community.jpg";

import founders from "../assets/founders/founders.jpg";

import trainer01 from "../assets/trainers/trainer-01.jpg";
import trainer02 from "../assets/trainers/trainer-02.jpg";
import trainer03 from "../assets/trainers/trainer-03.jpg";
import trainer04 from "../assets/trainers/trainer-04.jpg";

import hero01 from "../assets/hero/hero-01.jpg";
import hero02 from "../assets/hero/hero-02.jpg";
import hero03 from "../assets/hero/hero-03.jpg";

import visualReel from "../assets/gallery/ethos-visual-reel.mp4";
import { adminApi } from "../services/adminApi";

import "../styles/gallery.css";

const categories = [
  "ALL",
  "WORKSHOPS",
  "PERFORM",
  "SANGEETH",
  "COMMUNITY",
  "BEHIND THE SCENES",
];

const galleryItems = [
  {
    id: "workshop-01",
    type: "image",
    src: workshop01,
    category: "WORKSHOPS",
    title: "Workshop Sessions",
    className: "gallery-item--tall",
  },
  {
    id: "hero-01",
    type: "image",
    src: hero01,
    category: "PERFORM",
    title: "In Motion",
    className: "gallery-item--wide",
  },
  {
    id: "founders",
    type: "image",
    src: founders,
    category: "COMMUNITY",
    title: "The People Behind Ethos",
    className: "gallery-item--standard",
  },
  {
    id: "workshop-02",
    type: "image",
    src: workshop02,
    category: "WORKSHOPS",
    title: "Movement & Energy",
    className: "gallery-item--standard",
  },
  {
    id: "about-main",
    type: "image",
    src: aboutMain,
    category: "BEHIND THE SCENES",
    title: "Inside Ethos",
    className: "gallery-item--tall",
  },
  {
    id: "trainer-01",
    type: "image",
    src: trainer01,
    category: "COMMUNITY",
    title: "Ethos Faculty",
    className: "gallery-item--standard",
  },
  {
    id: "workshop-03",
    type: "image",
    src: workshop03,
    category: "WORKSHOPS",
    title: "Learn. Move. Grow.",
    className: "gallery-item--wide",
  },
  {
    id: "hero-02",
    type: "image",
    src: hero02,
    category: "PERFORM",
    title: "Find Your Rhythm",
    className: "gallery-item--standard",
  },
  {
    id: "about-secondary",
    type: "image",
    src: aboutSecondary,
    category: "BEHIND THE SCENES",
    title: "The Ethos Space",
    className: "gallery-item--standard",
  },
  {
    id: "trainer-02",
    type: "image",
    src: trainer02,
    category: "COMMUNITY",
    title: "Faculty",
    className: "gallery-item--tall",
  },
  {
    id: "workshop-04",
    type: "image",
    src: workshop04,
    category: "WORKSHOPS",
    title: "Workshop Energy",
    className: "gallery-item--standard",
  },
  {
    id: "hero-03",
    type: "image",
    src: hero03,
    category: "PERFORM",
    title: "Move With Purpose",
    className: "gallery-item--wide",
  },
  {
    id: "about-community",
    type: "image",
    src: aboutCommunity,
    category: "COMMUNITY",
    title: "The Ethos Community",
    className: "gallery-item--standard",
  },
  {
    id: "trainer-03",
    type: "image",
    src: trainer03,
    category: "COMMUNITY",
    title: "Ethos Faculty",
    className: "gallery-item--standard",
  },
  {
    id: "trainer-04",
    type: "image",
    src: trainer04,
    category: "COMMUNITY",
    title: "Teaching Through Movement",
    className: "gallery-item--tall",
  },
];

function Gallery() {
  const [activeCategory, setActiveCategory] = useState("ALL");
  const [selectedItem, setSelectedItem] = useState(null);
  const [dynamicVideos, setDynamicVideos] = useState([]);

  useEffect(() => {
    let isMounted = true;
    adminApi
      .getPublicVideos("Gallery")
      .then((data) => {
        if (isMounted && Array.isArray(data)) {
          setDynamicVideos(data);
        }
      })
      .catch((err) => {
        console.warn("[Gallery] Unable to load dynamic gallery videos:", err);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const filteredItems = useMemo(() => {
    if (activeCategory === "ALL") {
      return galleryItems;
    }

    return galleryItems.filter(
      (item) => item.category === activeCategory
    );
  }, [activeCategory]);

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

  const openImage = (item) => {
    setSelectedItem(item);
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

            {/* COLUMN 1 — UP */}
            <div className="gallery-motion-column gallery-motion-column--up">
              <div className="gallery-motion-track">
                <img src={workshop01} alt="" />
                <img src={hero02} alt="" />
                <img src={trainer02} alt="" />
                <img src={workshop03} alt="" />
                <img src={hero01} alt="" />

                {/* Duplicate set for seamless loop */}
                <img src={workshop01} alt="" />
                <img src={hero02} alt="" />
                <img src={trainer02} alt="" />
                <img src={workshop03} alt="" />
                <img src={hero01} alt="" />
              </div>
            </div>

            {/* COLUMN 2 — DOWN */}
            <div className="gallery-motion-column gallery-motion-column--down">
              <div className="gallery-motion-track">
                <img src={founders} alt="" />
                <img src={workshop02} alt="" />
                <img src={trainer03} alt="" />
                <img src={hero03} alt="" />
                <img src={workshop04} alt="" />

                {/* Duplicate set for seamless loop */}
                <img src={founders} alt="" />
                <img src={workshop02} alt="" />
                <img src={trainer03} alt="" />
                <img src={hero03} alt="" />
                <img src={workshop04} alt="" />
              </div>
            </div>

            {/* COLUMN 3 — UP */}
            <div className="gallery-motion-column gallery-motion-column--up">
              <div className="gallery-motion-track">
                <img src={trainer01} alt="" />
                <img src={aboutMain} alt="" />
                <img src={hero01} alt="" />
                <img src={trainer04} alt="" />
                <img src={aboutSecondary} alt="" />

                {/* Duplicate set for seamless loop */}
                <img src={trainer01} alt="" />
                <img src={aboutMain} alt="" />
                <img src={hero01} alt="" />
                <img src={trainer04} alt="" />
                <img src={aboutSecondary} alt="" />
              </div>
            </div>

          </div>

        </div>

      </section>

      {/* =====================================================
          FEATURED VISUAL REEL
          ===================================================== */}

      <section className="gallery-reel">

        <div className="gallery-reel__header">

          <div>
            <p className="gallery-section-label">
              ETHOS IN MOTION
            </p>

            <h2>
              MOVEMENT,
              <br />
              <em>CAPTURED.</em>
            </h2>
          </div>

          <p className="gallery-reel__description">
            A glimpse into the energy, expression and
            stories that live inside the studio.
          </p>

        </div>

        <button
          type="button"
          className="gallery-reel__frame"
          onClick={() =>
            setSelectedItem({
              id: "visual-reel",
              type: "video",
              src: visualReel,
              title: "Ethos In Motion",
            })
          }
          aria-label="Open Ethos visual reel"
        >
          <video
            className="gallery-reel__video"
            src={visualReel}
            autoPlay
            muted
            loop
            playsInline
            preload="metadata"
          />

          <div className="gallery-reel__overlay" />

          <div className="gallery-reel__play">
            <span>PLAY</span>
            <span>↗</span>
          </div>

          <div className="gallery-reel__caption">
            <span>ETHOS VISUAL REEL</span>
            <span>IN MOTION</span>
          </div>
        </button>

      </section>

      {/* =====================================================
          DYNAMIC CLOUDFLARE R2 GALLERY VIDEO SHOWCASES
          ===================================================== */}
      {dynamicVideos.length > 0 && (
        <section className="gallery-showcases-section">
          <div className="gallery-reel__header">
            <div>
              <p className="gallery-section-label">CURATED SHOWCASES</p>
              <h2>
                FEATURED
                <br />
                <em>PERFORMANCES.</em>
              </h2>
            </div>
            <p className="gallery-reel__description">
              Handpicked full routines, masterclasses, and showcase films produced at Ethos Dance Studio.
            </p>
          </div>

          <div className="gallery-videos-grid">
            {dynamicVideos.map((video) => (
              <div
                key={video.id}
                className="gallery-video-card"
                onClick={() =>
                  setSelectedItem({
                    id: video.id,
                    type: "video",
                    src: video.publicUrl,
                    title: video.title,
                    category: "SHOWCASE",
                  })
                }
              >
                <div className="gallery-video-thumb-wrap">
                  <video
                    src={video.publicUrl}
                    className="gallery-video-thumb"
                    muted
                    preload="metadata"
                    playsInline
                  />
                  <div className="gallery-video-play-btn">▶</div>
                </div>
                <div className="gallery-video-info">
                  <h3>{video.title}</h3>
                  {video.description && <p>{video.description}</p>}
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

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

        <div className="gallery-grid">

          {filteredItems.map((item, index) => (
            <button
              key={item.id}
              type="button"
              className={`gallery-item ${item.className}`}
              onClick={() => openImage(item)}
              style={{
                "--gallery-index": index,
              }}
            >

              <img
                src={item.src}
                alt={item.title}
                loading="lazy"
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
