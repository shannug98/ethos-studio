import { useState } from "react";
import "../../styles/trainers.css";

import trainer01 from "../../assets/trainers/trainer-01.jpg";
import trainer02 from "../../assets/trainers/trainer-02.jpg";
import trainer03 from "../../assets/trainers/trainer-03.jpg";
import trainer04 from "../../assets/trainers/trainer-04.jpg";

import ethosEmblem from "../../assets/brand/ethos-emblem.png";

const trainers = [
  {
    id: 1,
    image: trainer01,
    role: "Co-Founder & Lead Choreographer",
    name: "Sujith Kumar",
    bio: "A movement-driven choreographer focused on creating powerful performances, developing dancers and helping every student find confidence through movement.",
    instagram: "https://instagram.com",
    youtube: "https://youtube.com",
  },
  {
    id: 2,
    image: trainer02,
    role: "Co-Founder & Executive Director",
    name: "Tejaswini",
    bio: "Bringing together creative direction, performance and community, with a focus on creating an environment where dancers can learn, grow and belong.",
    instagram: "https://instagram.com",
    youtube: "https://youtube.com",
  },
  {
    id: 3,
    image: trainer03,
    role: "Assistant Choreographer",
    name: "Ethos Crew Lead",
    bio: "A passionate performer and choreographer who brings energy, discipline and individuality into every session while helping dancers build their own movement language.",
    instagram: "https://instagram.com",
    youtube: "https://youtube.com",
  },
  {
    id: 4,
    image: trainer04,
    role: "Classical & Contemporary Trainer",
    name: "Senior Resident Faculty",
    bio: "Combining classical foundations with expressive movement, helping dancers develop technique, musicality and a deeper connection with their art.",
    instagram: "https://instagram.com",
    youtube: "https://youtube.com",
  },
];

function Trainers() {
  const [openTrainer, setOpenTrainer] = useState(null);

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
        <div className="trainers-grid">
          {trainers.map((trainer) => {
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
                  <div className="trainer-card__image-wrapper">
                    <img
                      src={trainer.image}
                      alt={trainer.name}
                      className="trainer-card__image"
                    />
                    <div className="trainer-card__image-overlay" />
                  </div>

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

                      <a
                        href={trainer.youtube}
                        target="_blank"
                        rel="noreferrer"
                        className="trainer-card__social"
                        onClick={(event) => event.stopPropagation()}
                      >
                        YouTube
                        <span>↗</span>
                      </a>
                    </div>
                  </div>
                </div>
              </article>
            );
          })}
        </div>

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
