import { useEffect, useRef } from "react";
import "../../styles/about.css";

import aboutMain from "../../assets/about/about-main.jpg";
import aboutSecondary from "../../assets/about/about-secondary.jpg";
import aboutCommunity from "../../assets/about/about-community.jpg";

function About() {
  const sectionRef = useRef(null);

  useEffect(() => {
    const section = sectionRef.current;

    if (!section) return;

    const elements = section.querySelectorAll(".about-reveal");

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add("about-reveal--visible");
          }
        });
      },
      {
        threshold: 0.12,
      }
    );

    elements.forEach((element) => observer.observe(element));

    return () => observer.disconnect();
  }, []);

  return (
    <section
      id="about"
      className="about"
      ref={sectionRef}
    >
      {/* =====================================================
          BACKGROUND TYPOGRAPHY
          ===================================================== */}

      <div className="about__background-word">
        ETHOS
      </div>


      {/* =====================================================
          INTRO
          ===================================================== */}

      <header className="about__intro about-reveal">

        <div className="about__intro-top">
          <span className="about__eyebrow">
            OUR STORY
          </span>

          <span className="about__intro-line" />
        </div>

        <div className="about__intro-grid">

          <h2 className="about__title">
            WE ARE
            <br />
            <em>ETHOS.</em>
          </h2>

          <div className="about__intro-copy">
            <p className="about__intro-lead">
              More than a studio.
              <br />
              More than a class.
              <br />
              A place to belong.
            </p>

            <p>
              Ethos is a space built around movement,
              expression and people. A place where
              you can discover your style, challenge
              yourself and find a community that moves
              with you.
            </p>
          </div>

        </div>

      </header>


      {/* =====================================================
          STORY — IMAGE LEFT / TEXT RIGHT
          ===================================================== */}

      <article className="about-story about-story--image-left about-reveal">

        <div className="about-story__image">
          <img
            src={aboutMain}
            alt="Dancers performing at Ethos Dance Studio"
          />

          <div className="about-story__image-shade" />
        </div>

        <div className="about-story__content">

          <span className="about-story__label">
            THE BEGINNING
          </span>

          <h3>
            DANCE IS
            <br />
            <em>CONNECTION.</em>
          </h3>

          <p>
            Ethos was created with a simple belief:
            dance should feel like more than learning
            steps.
          </p>

          <p>
            It should be a space where people feel
            comfortable taking their first step, where
            experienced dancers can keep pushing their
            limits, and where everyone can find a reason
            to come back.
          </p>

          <p>
            From the first movement to the final
            performance, every experience at Ethos is
            about becoming more confident in your own
            movement.
          </p>

        </div>

      </article>


      {/* =====================================================
          STORY — TEXT LEFT / IMAGE RIGHT
          ===================================================== */}

      <article className="about-story about-story--image-right about-reveal">

        <div className="about-story__content">

          <span className="about-story__label">
            THE EXPERIENCE
          </span>

          <h3>
            FIND YOUR
            <br />
            <em>RHYTHM.</em>
          </h3>

          <p>
            There is no single way to dance.
          </p>

          <p>
            Some people find themselves through
            contemporary movement. Others through
            hip hop, jazz, freestyle or something they
            have never tried before.
          </p>

          <p>
            That's why Ethos is designed to give you
            room to explore. Learn from different
            trainers, experience different styles and
            discover what movement means to you.
          </p>

          <a
            href="#workshops"
            className="about-story__link"
          >
            EXPLORE YOUR MOVEMENT
            <span>↗</span>
          </a>

        </div>

        <div className="about-story__image">

          <img
            src={aboutSecondary}
            alt="Dancers training together at Ethos"
          />

          <div className="about-story__image-shade" />

        </div>

      </article>


      {/* =====================================================
          COMMUNITY — IMAGE LEFT / TEXT RIGHT
          ===================================================== */}

      <article className="about-story about-story--community about-reveal">

        <div className="about-story__image">

          <img
            src={aboutCommunity}
            alt="Ethos dance community"
          />

          <div className="about-story__image-shade" />

        </div>

        <div className="about-story__content">

          <span className="about-story__label">
            THE COMMUNITY
          </span>

          <h3>
            YOU DON'T
            <br />
            HAVE TO MOVE
            <br />
            <em>ALONE.</em>
          </h3>

          <p>
            The people around you become part of the
            journey.
          </p>

          <p>
            The encouragement before class. The
            laughter between rehearsals. The people
            who celebrate your progress and push you
            when you need it.
          </p>

          <p>
            That's what makes a studio a community.
            And that's what we want Ethos to be.
          </p>

          <span className="about-story__script">
            Find your people.
          </span>

        </div>

      </article>


      {/* =====================================================
          MANIFESTO
          ===================================================== */}

      <section className="about-manifesto about-reveal">

        <div className="about-manifesto__line" />

        <span className="about-manifesto__label">
          THE ETHOS
        </span>

        <h3>
          WE DON'T JUST
          <br />
          TEACH <em>DANCE.</em>
        </h3>

        <p>
          We create space for people to move,
          grow and belong.
        </p>

        <div className="about-manifesto__line" />

      </section>


      {/* =====================================================
          VALUES
          ===================================================== */}

      <section className="about-values">

        <div className="about-values__heading about-reveal">

          <span>
            WHAT WE BELIEVE
          </span>

          <h3>
            MOVE.
            <br />
            LEARN.
            <br />
            <em>BELONG.</em>
          </h3>

        </div>

        <div className="about-values__list">

          <div className="about-value about-reveal">

            <div className="about-value__title">
              MOVE
            </div>

            <p>
              Movement is freedom. Find the way
              your body wants to move and make it
              your own.
            </p>

          </div>

          <div className="about-value about-reveal">

            <div className="about-value__title">
              LEARN
            </div>

            <p>
              Every class is an opportunity to
              discover something new, build
              confidence and keep growing.
            </p>

          </div>

          <div className="about-value about-reveal">

            <div className="about-value__title">
              BELONG
            </div>

            <p>
              Dance connects people. Different
              styles, different stories, one
              community.
            </p>

          </div>

        </div>

      </section>


      {/* =====================================================
          STATS
          ===================================================== */}

      <section className="about-stats about-reveal">

        <div className="about-stat">

          <strong>
            500<span>+</span>
          </strong>

          <small>
            ACTIVE STUDENTS
          </small>

        </div>

        <div className="about-stat">

          <strong>
            20<span>+</span>
          </strong>

          <small>
            EXPERT TRAINERS
          </small>

        </div>

        <div className="about-stat">

          <strong>
            50<span>+</span>
          </strong>

          <small>
            CLASSES & WORKSHOPS
          </small>

        </div>

        <div className="about-stat">
          <strong>ONE</strong>
          <small>STRONGER COMMUNITY</small>
        </div>

      </section>

    </section>
  );
}

export default About;
