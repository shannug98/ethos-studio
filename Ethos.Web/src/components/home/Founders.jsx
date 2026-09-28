import { useState, useEffect } from "react";
import "../../styles/founders.css";
import { publicApi } from "../../services/publicApi";
import { getMediaUrl, handleMediaImgError } from "../../utils/mediaUrl";
import ethosEmblem from "../../assets/brand/ethos-emblem.png";

const defaultFounders = [
  {
    order: 1,
    id: "founder-one",
    name: "Sreekanth Chirkya",
    role: "Co-Founder & Lead Choreographer & Creative Director",
    bio: "Plays an active role in shaping the studio’s creative vision, choreography programs, workshops and overall dance community. His goal is to create an environment where dancers can learn, experiment, perform and grow.",
    instagram: "https://www.instagram.com/sreekanth_chirkya",
  },
  {
    order: 2,
    id: "founder-two",
    name: "Arjun Manikanta",
    role: "Co-Founder & Lead Choreographer & Executive Director",
    bio: "Leads studio operations, choreography, workshops, events, and creative initiatives. Committed to crafting engaging dance experiences, nurturing emerging talent, and building a thriving dance community.",
    instagram: "https://www.instagram.com/i_arjunmani",
  },
];

function Founders() {
  const [founderMedia, setFounderMedia] = useState({ 1: null, 2: null });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;
    setLoading(true);

    publicApi
      .getPublicMedia({ section: "Founders" })
      .then((data) => {
        if (!isMounted) return;
        const items = Array.isArray(data) ? data : [];
        const item1 = items.find((m) => Number(m.displayOrder) === 1) || (items[0] && Number(items[0].displayOrder) !== 2 ? items[0] : null);
        const item2 = items.find((m) => Number(m.displayOrder) === 2) || (items[1] && items[1] !== item1 ? items[1] : null);

        setFounderMedia({
          1: item1 ? { ...item1, url: getMediaUrl(item1) } : null,
          2: item2 ? { ...item2, url: getMediaUrl(item2) } : null,
        });
      })
      .catch((err) => {
        console.warn("[Founders] Public media fetch failed:", err);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <section className="founders-section" id="founders">
      {/* BACKGROUND */}
      <div className="founders-section__background-word" aria-hidden="true">
        ETHOS
      </div>

      <div className="founders-section__container">
        {/* SECTION HEADER */}
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

        {/* DUAL FOUNDERS GRID (2 INDEPENDENT CARDS) */}
        <div className="founders-dual-grid">
          {defaultFounders.map((founderDef) => {
            const media = founderMedia[founderDef.order];
            const name = media?.title?.trim() || founderDef.name;
            const role = media?.caption?.trim() || founderDef.role;
            const alt = media?.altText?.trim() || `${name} - ${role}`;
            const photoUrl = media?.url || null;

            return (
              <article className="founder-card" key={founderDef.id}>
                {/* PHOTO CONTAINER */}
                <div className="founder-card__image-container">
                  {photoUrl ? (
                    <img
                      src={photoUrl}
                      alt={alt}
                      className="founder-card__image"
                      onError={(e) => handleMediaImgError(e)}
                      loading="lazy"
                    />
                  ) : (
                    <div className="founder-card__image--empty" aria-label={`Photo pending for ${name}`}>
                      <img
                        src={ethosEmblem}
                        alt="Ethos Dance Studio"
                        className="founder-card__placeholder-emblem"
                      />
                      <span className="founder-card__placeholder-text">
                        PHOTO TO BE UPLOADED
                      </span>
                    </div>
                  )}
                  <div className="founder-card__overlay" />
                </div>

                {/* INFORMATION BODY */}
                <div className="founder-card__info">
                  <span className="founder-card__role">{role}</span>
                  <h3 className="founder-card__name">{name}</h3>
                  <div className="founder-card__line" />
                  <p className="founder-card__bio">{founderDef.bio}</p>

                  <div className="founder-card__socials">
                    <a
                      href={founderDef.instagram}
                      target="_blank"
                      rel="noreferrer"
                      className="founder-card__social-link"
                    >
                      Instagram <span>↗</span>
                    </a>
                   
                  </div>
                </div>
              </article>
            );
          })}
        </div>

        {/* BOTTOM STATEMENT */}
        <div className="founders-statement">
          <div className="founders-statement__line" />

          <div className="founders-statement__content">
            <p className="founders-statement__small">OUR BEGINNING</p>

            <h3>
              MORE THAN
              <br />
              <span>A DANCE STUDIO.</span>
            </h3>

            <p className="founders-statement__text">
              A space built around movement, creativity and the belief that
              everyone deserves a place to belong.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}

export default Founders;
