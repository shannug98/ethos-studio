import "../styles/hero.css";

function Hero() {
  return (
    <section id="home" className="ethos-hero">

      <div className="ethos-hero__background">
        <div className="ethos-hero__glow" />
        <div className="ethos-hero__dancer-placeholder" />
      </div>


      <div className="ethos-hero__overlay" />


      <div className="ethos-hero__content">

        <div className="ethos-hero__eyebrow">
          MORE THAN DANCE
          <span />
        </div>


        <h1 className="ethos-hero__title">

          <span className="ethos-hero__title-line">
            Move
          </span>

          <span className="ethos-hero__title-line">
            Learn
          </span>

          <span className="ethos-hero__title-line ethos-hero__title-line--accent">
            Belong
          </span>

        </h1>


        <p className="ethos-hero__description">
          Dance is a journey of self-discovery.
          <br />
          Join Ethos to learn, grow and become
          <br />
          part of a passionate community.
        </p>


        <div className="ethos-hero__actions">

          <button className="ethos-hero__primary-button">
            Explore Classes
            <span>→</span>
          </button>


          <button className="ethos-hero__story-button">

            <span className="ethos-hero__play">
              <span />
            </span>

            <span>
              Watch Our Story
            </span>

          </button>

        </div>

      </div>


      <div className="ethos-hero__side-label">
        <span>Dance</span>
        <span>Your</span>
        <span>Story</span>
      </div>


      <div className="ethos-hero__pagination">

        <span className="ethos-hero__pagination-number ethos-hero__pagination-number--active">
          01
        </span>

        <span>02</span>
        <span>03</span>
        <span>04</span>

      </div>


      <div className="ethos-hero__scroll">

        <div className="ethos-hero__scroll-icon">
          <span />
        </div>

        <span>SCROLL</span>

      </div>


      <div className="ethos-hero__bottom-line" />

    </section>
  );
}

export default Hero;
