import { useState } from "react";
import "../styles/events.css";

import hero01 from "../assets/hero/hero-01.jpg";
import hero03 from "../assets/hero/hero-03.jpg";
import workshop02 from "../assets/workshops/workshop-02.jpg";
import founders from "../assets/founders/founders.jpg";

function Events() {
  const [activeEvent, setActiveEvent] = useState(null);

  const events = [
    {
      id: "sangeeth",
      title: "SANGEETH",
      eyebrow: "WEDDINGS & CELEBRATIONS",
      description:
        "From family entries and couple performances to full-group Sangeeth choreography, ETHOS creates performances that feel personal, energetic and unforgettable.",
      image: hero01,
      details: [
        "Bride & Groom Entries",
        "Couple Performances",
        "Family & Friends Choreography",
        "Group Sangeeth Performances",
        "Wedding Flash Mobs",
        "Stage Show Direction",
      ],
    },
    {
      id: "corporate",
      title: "CORPORATE EVENTS",
      eyebrow: "WORK. PEOPLE. ENERGY.",
      description:
        "Bring movement and energy to your next corporate celebration, annual event, team gathering or special company occasion.",
      image: workshop02,
      details: [
        "Corporate Performances",
        "Employee Group Choreography",
        "Team Dance Experiences",
        "Annual Day Performances",
        "Company Celebrations",
        "Special Event Acts",
      ],
    },
    {
      id: "ceremonies",
      title: "CEREMONIES",
      eyebrow: "CULTURE & TRADITION",
      description:
        "ETHOS can create dance and performance experiences for Indian ceremonies, cultural celebrations and meaningful family occasions.",
      image: hero03,
      details: [
        "Traditional Ceremonies",
        "Cultural Celebrations",
        "Family Performances",
        "Special Entries",
        "Traditional & Contemporary Fusion",
        "Custom Choreography",
      ],
    },
    {
      id: "custom",
      title: "SPECIAL EVENTS",
      eyebrow: "YOUR IDEA. OUR MOVEMENT.",
      description:
        "Have something completely different in mind? Tell us what you are planning. ETHOS can build a custom dance, performance or choreography experience around your event.",
      image: founders,
      details: [
        "Custom Performances",
        "Theme-Based Choreography",
        "Opening & Closing Acts",
        "Surprise Performances",
        "Large Group Productions",
        "Bespoke Event Concepts",
      ],
    },
  ];

  return (
    <main className="events-page">

      {/* HERO */}
      <section className="events-hero">
        <div className="events-hero__glow" />

        <div className="events-hero__content">
          <p className="events-hero__eyebrow">
            ETHOS EVENTS
          </p>

          <h1>
            YOUR EVENT.
            <br />
            <em>OUR MOVEMENT.</em>
          </h1>

          <p className="events-hero__description">
            ETHOS creates choreography, performances and
            movement experiences for celebrations,
            corporate occasions, ceremonies and
            special events.
          </p>

          <a
            href="#event-types"
            className="events-hero__button"
          >
            EXPLORE EVENTS
            <span>↘</span>
          </a>
        </div>

        <div className="events-hero__side-note">
          <span>DANCE</span>
          <span>CREATE</span>
          <span>CELEBRATE</span>
        </div>
      </section>

      {/* INTRO */}
      <section className="events-intro">
        <div className="events-intro__statement">
          <p className="events-label">
            BEYOND THE STUDIO
          </p>

          <h2>
            WHEREVER
            <br />
            <em>YOU CELEBRATE.</em>
          </h2>
        </div>

        <div className="events-intro__copy">
          <p>
            A wedding. A Sangeeth. A corporate celebration.
            A cultural ceremony. A family gathering.
            Or something that has never been done before.
          </p>

          <p>
            ETHOS brings choreography, performers and
            creative direction together to turn your
            occasion into a moment people remember.
          </p>
        </div>
      </section>

      {/* EVENT TYPES */}
      <section
        className="events-types"
        id="event-types"
      >
        <div className="events-types__header">
          <div>
            <p className="events-label">
              WHAT WE DO
            </p>

            <h2>
              MADE FOR
              <br />
              <em>THE MOMENT.</em>
            </h2>
          </div>

          <p>
            Every event is different. Instead of
            forcing your celebration into a fixed
            package, we understand your requirements
            and build the experience around you.
          </p>
        </div>

        <div className="events-list">
          {events.map((event) => {
            const isActive = activeEvent === event.id;

            return (
              <article
                key={event.id}
                className={
                  isActive
                    ? "event-card event-card--active"
                    : "event-card"
                }
              >
                <button
                  type="button"
                  className="event-card__main"
                  onClick={() =>
                    setActiveEvent(
                      isActive ? null : event.id
                    )
                  }
                >
                  <div className="event-card__image">
                    <img
                      src={event.image}
                      alt={event.title}
                    />
                  </div>

                  <div className="event-card__content">
                    <p className="event-card__eyebrow">
                      {event.eyebrow}
                    </p>

                    <h3>
                      {event.title}
                    </h3>

                    <p className="event-card__description">
                      {event.description}
                    </p>
                  </div>

                  <div className="event-card__toggle">
                    {isActive ? "−" : "+"}
                  </div>
                </button>

                {isActive && (
                  <div className="event-card__details">
                    <p className="event-card__details-label">
                      POSSIBILITIES
                    </p>

                    <div className="event-card__tags">
                      {event.details.map((detail) => (
                        <span key={detail}>
                          {detail}
                        </span>
                      ))}
                    </div>

                    <div className="event-card__contact">
                      <div className="event-card__contact-copy">
                        <span>WANT TO KNOW MORE?</span>
                        <p>
                          Tell us about your event, your requirements and what
                          you have in mind. We&apos;ll help create the right
                          experience for the occasion.
                        </p>
                      </div>

                      <a
                        href="/#contact"
                        className="event-card__contact-button"
                      >
                        CONTACT ETHOS
                        <span>↗</span>
                      </a>
                    </div>
                  </div>
                )}
              </article>
            );
          })}
        </div>
      </section>

      {/* HOW IT WORKS */}
      <section className="events-process">
        <div className="events-process__heading">
          <p className="events-label">
            FROM IDEA TO PERFORMANCE
          </p>

          <h2>
            TELL US
            <br />
            <em>WHAT YOU'RE PLANNING.</em>
          </h2>
        </div>

        <div className="events-process__steps">
          <div className="events-process__step">
            <span className="events-process__symbol">
              ✦
            </span>

            <h3>SHARE THE EVENT</h3>

            <p>
              Tell us what you are celebrating,
              where it is happening and what kind
              of performance or choreography you
              have in mind.
            </p>
          </div>

          <div className="events-process__step">
            <span className="events-process__symbol">
              ✦
            </span>

            <h3>BUILD THE EXPERIENCE</h3>

            <p>
              Our team understands the occasion,
              participants, music, venue and
              performance requirements and creates
              the right approach.
            </p>
          </div>

          <div className="events-process__step">
            <span className="events-process__symbol">
              ✦
            </span>

            <h3>MAKE IT HAPPEN</h3>

            <p>
              From rehearsals and choreography to
              performance direction, ETHOS helps
              bring the final experience together.
            </p>
          </div>
        </div>
      </section>

      {/* REQUIREMENTS */}
      <section className="events-requirements">
        <div className="events-requirements__intro">
          <p className="events-label">
            EVENT ENQUIRY
          </p>

          <h2>
            WHAT DO WE
            <br />
            <em>NEED FROM YOU?</em>
          </h2>

          <p>
            There is no need to have everything figured
            out before contacting us. The more you can
            share, the better we can understand your
            event.
          </p>
        </div>

        <div className="events-requirements__list">
          <div>
            <span>EVENT TYPE</span>
            <p>
              Sangeeth, wedding, corporate,
              ceremony or any other occasion.
            </p>
          </div>

          <div>
            <span>DATE & TIME</span>
            <p>
              Event date, performance time and
              approximate duration.
            </p>
          </div>

          <div>
            <span>VENUE</span>
            <p>
              Location, indoor or outdoor setting
              and available stage or performance area.
            </p>
          </div>

          <div>
            <span>PEOPLE INVOLVED</span>
            <p>
              Approximate number of performers,
              family members, employees or guests.
            </p>
          </div>

          <div>
            <span>MUSIC & CONCEPT</span>
            <p>
              Songs, theme, mood or any creative
              direction you already have in mind.
            </p>
          </div>

          <div>
            <span>REHEARSALS</span>
            <p>
              Your preferred rehearsal location,
              schedule and available preparation time.
            </p>
          </div>

          <div>
            <span>PERFORMANCE NEEDS</span>
            <p>
              Choreography, performers, entries,
              group acts, stage direction or a
              complete custom concept.
            </p>
          </div>

          <div>
            <span>YOUR VISION</span>
            <p>
              Most importantly, tell us what you
              want people to feel when the moment happens.
            </p>
          </div>
        </div>
      </section>

      {/* CONTACT CTA */}
      <section className="events-contact">
        <div className="events-contact__inner">
          <p className="events-label">
            LET'S CREATE SOMETHING
          </p>

          <h2>
            HAVE AN
            <br />
            <em>EVENT IN MIND?</em>
          </h2>

          <p>
            Tell ETHOS what you're planning.
            We'll take it from there.
          </p>

          <a
            href="/#contact"
            className="events-contact__button"
          >
            CONTACT ETHOS
            <span>↗</span>
          </a>
        </div>
      </section>

    </main>
  );
}

export default Events;
