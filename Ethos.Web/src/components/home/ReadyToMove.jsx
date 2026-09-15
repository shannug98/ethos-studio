import { useEffect, useRef, useState } from "react";
import "../../styles/ready-to-move.css";

function ReadyToMove() {
  const sectionRef = useRef(null);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setVisible(true);
          observer.disconnect();
        }
      },
      {
        threshold: 0.2,
      }
    );

    if (sectionRef.current) {
      observer.observe(sectionRef.current);
    }

    return () => observer.disconnect();
  }, []);

  const handleGetStarted = () => {
    const contactSection = document.getElementById("contact");

    if (contactSection) {
      contactSection.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    }
  };

  return (
    <section
      ref={sectionRef}
      className={`ready-section ${
        visible ? "ready-section--visible" : ""
      }`}
      id="ready-to-move"
    >
      {/* =====================================================
          BACKGROUND
      ====================================================== */}

      <div
        className="ready-section__background"
        aria-hidden="true"
      >
        ETHOS
      </div>

      <div
        className="ready-section__light"
        aria-hidden="true"
      />

      <div
        className="ready-section__grain"
        aria-hidden="true"
      />

      {/* =====================================================
          MAIN CONTAINER
      ====================================================== */}

      <div className="ready-section__container">

        {/* ===================================================
            TOP LABEL
        ==================================================== */}

        <div className="ready-section__top">
          <span>YOUR NEXT CHAPTER</span>

          <div className="ready-section__top-line" />
        </div>

        {/* ===================================================
            MAIN CONTENT
        ==================================================== */}

        <div className="ready-section__main">

          {/* LEFT */}
          <div className="ready-section__headline">

            <div className="ready-word ready-word--one">
              READY
            </div>

            <div className="ready-word ready-word--two">
              TO
            </div>

            <div className="ready-word ready-word--three">
              MOVE<span>?</span>
            </div>

            <button
              type="button"
              className="ready-section__cta"
              onClick={handleGetStarted}
            >
              <span className="ready-section__cta-text">
                GET STARTED
              </span>

              <span className="ready-section__cta-arrow">
                ↗
              </span>
            </button>
          </div>

          {/* RIGHT */}
          <div className="ready-section__description">

            <span className="ready-section__description-label">
              FIND YOUR RHYTHM
            </span>

            <p>
              Whether you're taking your first step,
              finding your rhythm or ready to take your
              movement further — there's a place for you
              at Ethos.
            </p>

          </div>
        </div>

        {/* ===================================================
            BOTTOM MARQUEE
        ==================================================== */}

        <div className="ready-section__bottom">

          <div className="ready-section__marquee">
            <div className="ready-section__marquee-track">

              <span>WORKSHOPS</span>
              <i>✦</i>

              <span>COMMUNITY</span>
              <i>✦</i>

              <span>MOVEMENT</span>
              <i>✦</i>

              <span>CREATIVITY</span>
              <i>✦</i>

              <span>PERFORMANCE</span>
              <i>✦</i>

              <span>WORKSHOPS</span>
              <i>✦</i>

              <span>COMMUNITY</span>
              <i>✦</i>

              <span>MOVEMENT</span>
              <i>✦</i>

              <span>CREATIVITY</span>
              <i>✦</i>

              <span>PERFORMANCE</span>
              <i>✦</i>

            </div>
          </div>

        </div>
      </div>
    </section>
  );
}

export default ReadyToMove;
