import { useState } from "react";
import "../../styles/inquiry.css";
import ethosEmblem from "../../assets/brand/ethos-emblem.png";

function InquirySection() {
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = (event) => {
    event.preventDefault();
    setSubmitted(true);

    setTimeout(() => {
      setSubmitted(false);
    }, 4000);
  };

  return (
    <section className="inquiry-section" id="contact">
      <div className="inquiry-section__glow inquiry-section__glow--one" />
      <div className="inquiry-section__glow inquiry-section__glow--two" />

      <div className="inquiry-section__container">

        {/* HEADER */}
        <div className="inquiry-header">
          <div className="inquiry-header__eyebrow">
            COME FIND US
          </div>

          <div className="inquiry-header__line" />

          <div className="inquiry-header__content">
            <h2>
              LET'S
              <span>CONNECT.</span>
            </h2>

            <p>
              Have a question, want to join a workshop,
              plan a performance or simply want to know
              more about Ethos?
            </p>
          </div>
        </div>


        {/* MAIN CONTENT */}
        <div className="inquiry-layout">

          {/* LEFT */}
          <div className="inquiry-info">

            <div className="inquiry-map-card">

              <div className="inquiry-map-card__top">
                <span>
                  <span className="inquiry-location-dot" />
                  ETHOS DANCE STUDIO
                </span>

                <a
                  href="https://www.google.com/maps/search/?api=1&query=Ethos+Dance+Studio+Nizampet+Road+Hyderabad"
                  target="_blank"
                  rel="noreferrer"
                >
                  OPEN IN MAPS ↗
                </a>
              </div>

              <iframe
                title="Ethos Dance Studio Location"
                src="https://www.google.com/maps?q=Ethos%20Dance%20Studio%2C%20Nizampet%20Road%2C%20Hyderabad&output=embed"
                loading="lazy"
                referrerPolicy="no-referrer-when-downgrade"
              />
            </div>


            <div className="inquiry-address">

              <span className="inquiry-label">
                STUDIO ADDRESS
              </span>

              <p>
                Second floor, 1/2/49/1, Nizampet Rd,
                Jai Bharat Nagar, Nagarjuna Homes,
                Kukatpally, Hyderabad,
                Telangana 500085
              </p>

            </div>


            <div className="inquiry-contact-grid">

              <a
                href="https://wa.me/918341701113"
                target="_blank"
                rel="noreferrer"
                className="inquiry-contact-card"
              >
                <span className="inquiry-contact-card__label">
                  PHONE & WHATSAPP
                </span>

                <strong>
                  +91 83417 01113
                </strong>

                <span className="inquiry-contact-card__arrow">
                  ↗
                </span>
              </a>


              <a
                href="mailto:ethosdancestudio@gmail.com"
                className="inquiry-contact-card"
              >
                <span className="inquiry-contact-card__label">
                  EMAIL SUPPORT
                </span>

                <strong>
                  ethosdancestudio@gmail.com
                </strong>

                <span className="inquiry-contact-card__arrow">
                  ↗
                </span>
              </a>

            </div>

          </div>


          {/* RIGHT — FORM */}
          <div className="inquiry-form-card">

            <div className="inquiry-form-card__heading">
              <span>STUDIO INQUIRY</span>

              <div className="contact-form__emblem">
                <span className="contact-form__emblem-glow" />

                <img
                  src={ethosEmblem}
                  alt="Ethos Dance Studio"
                  className="contact-form__emblem-image"
                />
              </div>
            </div>

            <h3>
              TELL US
              <span>WHAT'S ON YOUR MIND.</span>
            </h3>

            <p className="inquiry-form-card__intro">
              Submit your enquiry and our Ethos team
              will get back to you directly.
            </p>


            {submitted ? (
              <div className="inquiry-success">
                <div className="inquiry-success__icon">
                  ✓
                </div>

                <h4>
                  THANK YOU.
                </h4>

                <p>
                  Your enquiry has been received.
                  We'll be in touch soon.
                </p>
              </div>
            ) : (
              <form onSubmit={handleSubmit}>

                <div className="inquiry-field">
                  <label>
                    YOUR FULL NAME
                  </label>

                  <input
                    type="text"
                    placeholder="e.g. Rahul Sharma"
                    required
                  />
                </div>


                <div className="inquiry-field">
                  <label>
                    PHONE NUMBER
                  </label>

                  <input
                    type="tel"
                    placeholder="+91 98765 43210"
                    required
                  />
                </div>


                <div className="inquiry-field">
                  <label>
                    EMAIL ADDRESS
                  </label>

                  <input
                    type="email"
                    placeholder="your@email.com"
                  />
                </div>


                <div className="inquiry-field">
                  <label>
                    MESSAGE / QUERY
                  </label>

                  <textarea
                    rows="4"
                    placeholder="Ask about workshops, batch timings, wedding choreography, studio rentals..."
                    required
                  />
                </div>


                <button
                  type="submit"
                  className="inquiry-submit"
                >
                  <span>
                    SEND INQUIRY
                  </span>

                  <span>
                    ↗
                  </span>
                </button>

              </form>
            )}

          </div>

        </div>

      </div>
    </section>
  );
}

export default InquirySection;
