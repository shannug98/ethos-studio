import { useState } from "react";
import "../../styles/testimonials.css";

const testimonials = [
  {
    quote:
      "Ethos gave me a place where I could learn without feeling judged. I came in to learn dance and found a community that made me want to keep coming back.",
    name: "Ananya",
    role: "Student",
  },
  {
    quote:
      "What makes Ethos different is the way the trainers teach. They don't just teach choreography — they help you understand movement and find your own confidence.",
    name: "Rahul",
    role: "Student",
  },
  {
    quote:
      "Every workshop feels like more than a class. You meet people, you learn something new and you leave feeling completely different from when you walked in.",
    name: "Meghana",
    role: "Workshop Participant",
  },
  {
    quote:
      "I had always wanted to dance but never felt confident enough to start. Ethos made that first step feel easy.",
    name: "Arjun",
    role: "Student",
  },
];

function Testimonials() {
  const [activeIndex, setActiveIndex] = useState(0);

  const activeTestimonial = testimonials[activeIndex];

  const previousTestimonial = () => {
    setActiveIndex((current) =>
      current === 0 ? testimonials.length - 1 : current - 1
    );
  };

  const nextTestimonial = () => {
    setActiveIndex((current) =>
      current === testimonials.length - 1 ? 0 : current + 1
    );
  };

  return (
    <section className="testimonials-section" id="testimonials">
      <div
        className="testimonials-section__background-word"
        aria-hidden="true"
      >
        VOICE
      </div>

      <div className="testimonials-section__container">

        {/* =========================================
            SECTION HEADER
        ========================================== */}

        <div className="testimonials-header">
          <div className="testimonials-header__top">
            <span className="testimonials-header__eyebrow">
              THE ETHOS COMMUNITY
            </span>

            <span className="testimonials-header__line" />
          </div>

          <div className="testimonials-header__main">
            <h2 className="testimonials-header__title">
              HEAR IT
              <span>FROM THEM.</span>
            </h2>

            <p className="testimonials-header__description">
              Different journeys.
              <br />
              Different reasons.
              <br />
              One shared movement.
            </p>
          </div>
        </div>


        {/* =========================================
            TESTIMONIAL EXPERIENCE
        ========================================== */}

        <div className="testimonials-stage">

          {/* Decorative quotation */}

          <div
            className="testimonials-stage__quote-mark"
            aria-hidden="true"
          >
            “
          </div>


          {/* Main quote */}

          <div className="testimonial-content">

            <div className="testimonial-content__label">
              WHAT THEY SAY
            </div>

            <blockquote
              key={activeIndex}
              className="testimonial-content__quote"
            >
              {activeTestimonial.quote}
            </blockquote>

            <div className="testimonial-content__person">

              <div className="testimonial-content__person-line" />

              <div>
                <h3>{activeTestimonial.name}</h3>
                <span>{activeTestimonial.role}</span>
              </div>

            </div>

          </div>


          {/* =========================================
              CONTROLS
          ========================================== */}

          <div className="testimonials-controls">

            <button
              type="button"
              onClick={previousTestimonial}
              className="testimonials-control"
              aria-label="Previous testimonial"
            >
              <span>←</span>
            </button>

            <div className="testimonials-control__label">
              <span>{activeIndex + 1}</span>
              <i />
              <span>{testimonials.length}</span>
            </div>

            <button
              type="button"
              onClick={nextTestimonial}
              className="testimonials-control"
              aria-label="Next testimonial"
            >
              <span>→</span>
            </button>

          </div>

        </div>


        {/* =========================================
            COMMUNITY STATEMENT
        ========================================== */}

        <div className="testimonials-bottom">

          <div className="testimonials-bottom__line" />

          <div className="testimonials-bottom__content">

            <div className="testimonials-bottom__label">
              MORE THAN MOVEMENT
            </div>

            <h3>
              FIND YOUR
              <span>PEOPLE.</span>
            </h3>

            <p>
              Come for the dance.
              Stay for the people.
              Grow into who you are meant to become.
            </p>

          </div>

        </div>

      </div>
    </section>
  );
}

export default Testimonials;
