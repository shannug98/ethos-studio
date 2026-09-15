import { useState } from "react";
import "../../styles/founders.css";

// ============================================================
// FOUNDER IMAGE
//
// Put your two-founder image here:
//
// src/assets/founders/founders.jpg
// ============================================================

import foundersImage from "../../assets/founders/founders.jpg";

const founders = [
  {
    id: "founder-one",
    side: "left",
    name: "Founder Name",
    role: "Co-Founder & Lead Choreographer",
    bio: "A creative force behind Ethos, bringing together choreography, performance and a deep belief in the power of movement to create confidence, connection and community.",
    instagram: "#",
    youtube: "#",
  },
  {
    id: "founder-two",
    side: "right",
    name: "Founder Name",
    role: "Co-Founder & Executive Director",
    bio: "Helping shape the vision and culture of Ethos while creating an environment where dancers can learn, express themselves and feel that they truly belong.",
    instagram: "#",
    youtube: "#",
  },
];

function Founders() {
  const [activeFounder, setActiveFounder] = useState(null);

  const activeData = founders.find(
    (founder) => founder.id === activeFounder
  );

  return (
    <section className="founders-section" id="founders">

      {/* ======================================================
          BACKGROUND
      ======================================================= */}

      <div
        className="founders-section__background-word"
        aria-hidden="true"
      >
        ETHOS
      </div>


      <div className="founders-section__container">

        {/* ====================================================
            SECTION HEADER
        ===================================================== */}

        <div className="founders-header">

          <div className="founders-header__top">

            <span className="founders-header__eyebrow">
              THE PEOPLE BEHIND ETHOS
            </span>

            <span className="founders-header__line" />

          </div>


          <div className="founders-header__main">

            <h2 className="founders-header__title">
              MEET THE
              <span>FOUNDERS.</span>
            </h2>

            <p className="founders-header__description">
              Every studio begins with an idea.
              <br />
              Ethos began with a belief in people,
              <br />
              movement and belonging.
            </p>

          </div>

        </div>


        {/* ====================================================
            INTERACTIVE FOUNDER AREA
        ===================================================== */}

        <div
          className={`founders-stage ${
            activeFounder
              ? "founders-stage--active"
              : ""
          } ${
            activeData?.side === "left"
              ? "founders-stage--left-active"
              : ""
          } ${
            activeData?.side === "right"
              ? "founders-stage--right-active"
              : ""
          }`}
        >

          {/* --------------------------------------------------
              IMAGE
          --------------------------------------------------- */}

          <div className="founders-image-wrapper">

            <img
              src={foundersImage}
              alt="Ethos Dance Studio founders"
              className="founders-image"
            />

            <div className="founders-image__overlay" />

            <div className="founders-image__grain" />

          </div>


          {/* --------------------------------------------------
              INTERACTIVE ZONES
          --------------------------------------------------- */}

          <button
            type="button"
            className="founder-zone founder-zone--left"
            onMouseEnter={() =>
              setActiveFounder("founder-one")
            }
            onMouseLeave={() =>
              setActiveFounder(null)
            }
            onFocus={() =>
              setActiveFounder("founder-one")
            }
            onBlur={() =>
              setActiveFounder(null)
            }
            onClick={() =>
              setActiveFounder(
                activeFounder === "founder-one"
                  ? null
                  : "founder-one"
              )
            }
            aria-label="View first founder"
            aria-pressed={activeFounder === "founder-one"}
          >
            <span className="founder-zone__indicator">
              <span />
            </span>

            <span className="founder-zone__label">
              FOUNDER
            </span>
          </button>


          <button
            type="button"
            className="founder-zone founder-zone--right"
            onMouseEnter={() =>
              setActiveFounder("founder-two")
            }
            onMouseLeave={() =>
              setActiveFounder(null)
            }
            onFocus={() =>
              setActiveFounder("founder-two")
            }
            onBlur={() =>
              setActiveFounder(null)
            }
            onClick={() =>
              setActiveFounder(
                activeFounder === "founder-two"
                  ? null
                  : "founder-two"
              )
            }
            aria-label="View second founder"
            aria-pressed={activeFounder === "founder-two"}
          >
            <span className="founder-zone__indicator">
              <span />
            </span>

            <span className="founder-zone__label">
              FOUNDER
            </span>
          </button>


          {/* --------------------------------------------------
              DEFAULT CENTER MESSAGE
          --------------------------------------------------- */}

          <div
            className={`founders-stage__center-message ${
              activeFounder
                ? "founders-stage__center-message--hidden"
                : ""
            }`}
          >
            <span>MOVE</span>
            <span>CREATE</span>
            <span>BELONG</span>
          </div>


          {/* --------------------------------------------------
              FOUNDER INFORMATION PANEL
          --------------------------------------------------- */}

          {activeData && (
            <div
              className={`founder-profile founder-profile--${activeData.side}`}
            >

              <div className="founder-profile__top">

                <span className="founder-profile__eyebrow">
                  {activeData.role}
                </span>

                <button
                  type="button"
                  className="founder-profile__close"
                  onClick={() => setActiveFounder(null)}
                  aria-label="Close founder profile"
                >
                  ×
                </button>

              </div>


              <h3 className="founder-profile__name">
                {activeData.name}
              </h3>


              <div className="founder-profile__line" />


              <p className="founder-profile__bio">
                {activeData.bio}
              </p>


              <div className="founder-profile__socials">

                <a
                  href={activeData.instagram}
                  target="_blank"
                  rel="noreferrer"
                >
                  Instagram
                  <span>↗</span>
                </a>

                <a
                  href={activeData.youtube}
                  target="_blank"
                  rel="noreferrer"
                >
                  YouTube
                  <span>↗</span>
                </a>

              </div>

            </div>
          )}

        </div>


        {/* ====================================================
            BOTTOM STATEMENT
        ===================================================== */}

        <div className="founders-statement">

          <div className="founders-statement__line" />

          <div className="founders-statement__content">

            <p className="founders-statement__small">
              OUR BEGINNING
            </p>

            <h3>
              MORE THAN
              <br />
              <span>A DANCE STUDIO.</span>
            </h3>

            <p className="founders-statement__text">
              A space built around movement, creativity
              and the belief that everyone deserves a
              place to belong.
            </p>

          </div>

        </div>

      </div>

    </section>
  );
}

export default Founders;
