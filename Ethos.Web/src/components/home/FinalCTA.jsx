import "../../styles/final-cta.css";

function FinalCTA() {
  return (
    <section className="final-cta" id="get-started">
      <div className="final-cta__background" aria-hidden="true">
        <span>MOVE</span>
        <span>WITH</span>
        <span>US</span>
      </div>

      <div className="final-cta__container">

        <div className="final-cta__top">
          <span className="final-cta__eyebrow">
            YOUR NEXT CHAPTER
          </span>

          <span className="final-cta__line" />
        </div>

        <div className="final-cta__content">

          <h2 className="final-cta__title">
            READY
            <span>TO MOVE?</span>
          </h2>

          <p className="final-cta__description">
            Whether you're taking your first step,
            finding your rhythm or ready to take
            your movement further — there's a place
            for you at Ethos.
          </p>

          <a
            href="/register"
            className="final-cta__button"
          >
            <span>GET STARTED</span>

            <span className="final-cta__button-arrow">
              ↗
            </span>
          </a>

        </div>

        <div className="final-cta__bottom">

          <span>WORKSHOPS</span>
          <span>COMMUNITY</span>
          <span>MOVEMENT</span>

        </div>

      </div>
    </section>
  );
}

export default FinalCTA;
