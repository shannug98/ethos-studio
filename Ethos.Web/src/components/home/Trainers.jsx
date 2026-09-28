import { useState, useEffect } from "react";
import "../../styles/trainers.css";
import { publicApi } from "../../services/publicApi";
import { getMediaUrl, handleTrainerImgError } from "../../utils/mediaUrl";

import ethosEmblem from "../../assets/brand/ethos-emblem.png";

function getInitials(name) {
  if (!name) return "E";
  return name
    .split(" ")
    .map((part) => part[0])
    .filter(Boolean)
    .join("")
    .toUpperCase();
}

const defaultTrainers = [
  {
    id: 1,
    image: null,
    role: "Co-Founder & Lead Choreographer",
    name: "Sreekanth Chirkya",
    bio: "Sreekanth Chirkya is a trained dancer, choreographer and dance educator with a strong foundation in Hip-Hop, Tollywood and Bollywood dance styles. In 2018–19, he served as the General Secretary of LIVEWIRE, the collegiate dance crew of VNR VJIET.",
    instagram: "https://www.instagram.com/sreekanth_chirkya",
  },
  {
    id: 2,
    image: null,
    role: "Co-Founder & Executive Director",
    name: "Arjun Manikanta",
    bio: "With 10 years of experience in dance and fitness training, Arjun leads choreography, studio operations, workshops, events, and creative initiatives at Ethos Dance Studio. His work spans Tollywood, Bollywood, Hip-Hop, performance choreography, and dance fitness, with a focus on building engaging dance experiences and growing the Ethos community.",
    instagram: "https://www.instagram.com/i_arjunmani",
  },
  {
    id: 3,
    image: null,
    role: "Kuchipudi and Karakattam Dancer & Instructor",
    name: "Chethana",
    bio: "A certified Kuchipudi and Karakattam dancer with extensive stage performance experience. Winner of the “Projecting the Diversity of India” Biodiversity Event of Hyderabad for her Karakattam performance. Former Cultural Head at KL University and currently a Dance Instructor at Ethos Dance Studio.",
    instagram: "https://www.instagram.com/chethana_balineni/",
  },
  {
    id: 4,
    image: null,
    role: "Assistant Choreographer & Instructor",
    name: "Rockey",
    bio: "Rakesh Maharajj (Rockey) is a passionate choreographer and dance instructor at Ethos Dance Studio, bringing energy, precision, and artistry to every movement. He creates impactful choreography through his unique style and creative expression.",
    instagram: "https://www.instagram.com/rakesh_maharajj",
  },
];

function Trainers() {
  const [openTrainer, setOpenTrainer] = useState(null);
  const [trainerList, setTrainerList] = useState(defaultTrainers);

  useEffect(() => {
    let isMounted = true;
    publicApi
      .getPublicMedia({ section: "Trainers" })
      .then((data) => {
        if (isMounted && Array.isArray(data)) {
          setTrainerList(
            defaultTrainers.map((t, idx) => {
              const slotOrder = idx + 1;
              const cloudTrainer = data.find((m) => Number(m.displayOrder) === slotOrder);
              return cloudTrainer
                ? { ...t, image: getMediaUrl(cloudTrainer.publicUrl, cloudTrainer.id) }
                : { ...t, image: null };
            })
          );
        }
      })
      .catch((err) => {
        console.warn("[Trainers] Public media fetch failed:", err);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const toggleTrainer = (id) => {
    setOpenTrainer((current) => (current === id ? null : id));
  };

  return (
    <section className="trainers-section" id="trainers">
      <div className="trainers-section__background-word" aria-hidden="true">
        PEOPLE
      </div>

      <div className="trainers-section__container">
        {/* HEADER */}
        <div className="trainers-header">
          <div className="trainers-header__eyebrow">
            THE PEOPLE BEHIND THE MOVEMENT
          </div>
          <div className="trainers-header__line" />

          <div className="trainers-header__content">
            <h2 className="trainers-header__title">
              OUR
              <span>TRAINERS.</span>
            </h2>

            <div className="trainers-header__description">
              <p>
                People who teach.
                <br />
                People who move.
                <br />
                People who inspire.
              </p>
            </div>
          </div>
        </div>

        {/* TRAINER GRID */}
        {trainerList.length > 0 ? (
          <div className="trainers-grid">
            {trainerList.map((trainer) => {
              const isOpen = openTrainer === trainer.id;

            return (
              <article
                key={trainer.id}
                className={`trainer-card ${isOpen ? "trainer-card--open" : ""}`}
              >
                {/* IMAGE / CLICK AREA */}
                <div
                  className="trainer-card__visual"
                  role="button"
                  tabIndex={0}
                  onClick={() => toggleTrainer(trainer.id)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" || event.key === " ") {
                      event.preventDefault();
                      toggleTrainer(trainer.id);
                    }
                  }}
                  aria-expanded={isOpen}
                  aria-label={`Read bio for ${trainer.name}`}
                >
                  {trainer.image ? (
                    <div className="trainer-card__image-wrapper">
                      <img
                        src={trainer.image}
                        alt={trainer.name}
                        className="trainer-card__image"
                        onError={(e) => handleTrainerImgError(e)}
                      />
                      <div className="trainer-card__image-overlay" />
                    </div>
                  ) : (
                    <div className="trainer-card__placeholder">
                      <img
                        src={ethosEmblem}
                        alt=""
                        className="trainer-card__placeholder-emblem"
                      />
                      <span className="trainer-card__placeholder-initials">
                        {getInitials(trainer.name)}
                      </span>
                    </div>
                  )}

                  {/* EMBLEM */}
                  <div className="trainer-card__mark">
                    <img
                      src={ethosEmblem}
                      alt=""
                      className="trainer-card__mark-image"
                    />
                  </div>

                  {/* VERTICAL ROLE */}
                  <div className="trainer-card__vertical-role">
                    {trainer.role}
                  </div>

                  {/* ARROW BUTTON */}
                  <span className="trainer-card__visual-button" aria-hidden="true">
                    <span className="trainer-card__visual-arrow">
                      {isOpen ? "×" : "↗"}
                    </span>
                  </span>
                </div>

                {/* TRAINER INFO */}
                <div className="trainer-card__info">
                  <div className="trainer-card__role">{trainer.role}</div>
                  <h3 className="trainer-card__name">{trainer.name}</h3>

                  {/* READ MORE CLICKABLE BUTTON */}
                  <button
                    type="button"
                    className="trainer-card__read-more"
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleTrainer(trainer.id);
                    }}
                  >
                    <span>{isOpen ? "CLOSE BIO" : "READ MORE"}</span>
                    <span className="trainer-card__read-arrow">
                      {isOpen ? "↑" : "↗"}
                    </span>
                  </button>
                </div>

                {/* EXPANDED DETAILS */}
                <div className="trainer-card__details">
                  <div className="trainer-card__details-inner">
                    <p className="trainer-card__bio">{trainer.bio}</p>

                    <div className="trainer-card__socials">
                      <a
                        href={trainer.instagram}
                        target="_blank"
                        rel="noreferrer"
                        className="trainer-card__social"
                        onClick={(event) => event.stopPropagation()}
                      >
                        Instagram
                        <span>↗</span>
                      </a>

                     
                    </div>
                  </div>
                </div>
              </article>
            );
          })}
        </div>
      ) : (
          <div className="trainers-empty-state">
            <img src={ethosEmblem} alt="" className="trainers-empty-state__emblem" />
            <h3 className="trainers-empty-state__title">FACULTY ROSTER</h3>
            <p className="trainers-empty-state__text">
              Our faculty roster is being finalized for the upcoming season.
            </p>
          </div>
        )}

        {/* FOOTER STATEMENT */}
        <div className="trainers-footer">
          <div className="trainers-footer__line" />
          <div className="trainers-footer__content">
            <h3>
              DIFFERENT PEOPLE.
              <br />
              <span>ONE MOVEMENT.</span>
            </h3>
            <p>
              Every trainer brings their own experience, perspective and way of
              moving.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}

export default Trainers;
