import { useState } from "react";
import "../../styles/contact-section.css";

function ContactSection() {
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = (event) => {
    event.preventDefault();

    /*
      Backend/API integration can be connected here later.
      We don't invent an API endpoint that doesn't currently exist.
    */

    setSubmitted(true);
  };

  return (
    <section
      className="contact-section"
      id="contact"
    >
      <div className="contact-section__container">

        {/* =========================================
            HEADER
        ========================================== */}

        <div className="contact-section__header">

          <div className="contact-section__eyebrow">
            COME FIND US
          </div>

          <div className="contact-section__header-line" />

        </div>


        <div className="contact-section__intro">

          <h2>
            LET'S
            <span>CONNECT.</span>
          </h2>

          <p>
            Have a question about workshops,
            timings, studio rentals or anything
            Ethos? Reach out to us.
          </p>

        </div>


        {/* =========================================
            MAIN GRID
        ========================================== */}

        <div className="contact-section__grid">

          {/* =======================================
              LEFT — LOCATION / CONTACT
          ======================================== */}

          <div className="contact-section__left">

            {/* MAP */}

            <div className="contact-map">

              <div className="contact-map__top">

                <div className="contact-map__location">
                  <span className="contact-map__pin">
                    ◉
                  </span>

                  <strong>
                    ETHOS DANCE STUDIO
                  </strong>
                </div>

                <a
                  href="https://www.google.com/maps/search/?api=1&query=Ethos+Dance+Studio+Second+floor+1%2F2%2F49%2F1+Nizampet+Rd+Jai+Bharat+Nagar+Nagarjuna+Homes+Kukatpally+Hyderabad+Telangana+500085"
                  target="_blank"
                  rel="noreferrer"
                  className="contact-map__open"
                >
                  OPEN IN MAPS ↗
                </a>

              </div>

              <div className="contact-map__frame">

                <iframe
                  title="Ethos Dance Studio Location"
                  src="https://www.google.com/maps?q=Ethos%20Dance%20Studio%2C%20Second%20floor%2C%201%2F2%2F49%2F1%2C%20Nizampet%20Rd%2C%20Jai%20Bharat%20Nagar%2C%20Nagarjuna%20Homes%2C%20Kukatpally%2C%20Hyderabad%2C%20Telangana%20500085&output=embed"
                  loading="lazy"
                  referrerPolicy="no-referrer-when-downgrade"
                />

              </div>

            </div>


            {/* ADDRESS */}

            <div className="contact-info-card contact-info-card--address">

              <span className="contact-info-card__label">
                STUDIO ADDRESS
              </span>

              <p>
                Second floor, 1/2/49/1,
                Nizampet Rd, Jai Bharat Nagar,
                Nagarjuna Homes, Kukatpally,
                Hyderabad, Telangana 500085
              </p>

            </div>


            {/* CONTACT CARDS */}

            <div className="contact-info-grid">

              <a
                href="https://wa.me/918341701113"
                target="_blank"
                rel="noreferrer"
                className="contact-info-card contact-info-card--link"
              >

                <span className="contact-info-card__label">
                  PHONE & WHATSAPP
                </span>

                <strong>
                  +91 83417 01113
                </strong>

                <span className="contact-info-card__arrow">
                  ↗
                </span>

              </a>


              <a
                href="mailto:ethosdancestudio@gmail.com"
                className="contact-info-card contact-info-card--link"
              >

                <span className="contact-info-card__label">
                  EMAIL SUPPORT
                </span>

                <strong>
                  ethosdancestudio@gmail.com
                </strong>

                <span className="contact-info-card__arrow">
                  ↗
                </span>

              </a>

            </div>


            {/* WEBSITE */}

            <div className="contact-website">

              <span>
                WEBSITE
              </span>

              <a
                href="https://ethosdancestudio.com"
                target="_blank"
                rel="noreferrer"
              >
                ethosdancestudio.com ↗
              </a>

            </div>

          </div>


          {/* =======================================
              RIGHT — INQUIRY FORM
          ======================================== */}

          <div className="contact-form-card">

            <div className="contact-form-card__heading">

              <span>
                ETHOS DANCE STUDIO
              </span>

              <h3>
                STUDIO
                <br />
                <em>INQUIRY.</em>
              </h3>

              <p>
                Submit your query below and
                our Ethos team will reach out
                directly to you.
              </p>

            </div>


            {!submitted ? (

              <form
                className="contact-form"
                onSubmit={handleSubmit}
              >

                <label>
                  <span>YOUR FULL NAME</span>

                  <input
                    type="text"
                    name="name"
                    placeholder="e.g. Rahul Sharma"
                    required
                  />
                </label>


                <label>
                  <span>PHONE NUMBER (WHATSAPP)</span>

                  <input
                    type="tel"
                    name="phone"
                    placeholder="+91 98765 43210"
                    required
                  />
                </label>


                <label>
                  <span>EMAIL ADDRESS</span>

                  <input
                    type="email"
                    name="email"
                    placeholder="your@email.com"
                    required
                  />
                </label>


                <label>
                  <span>MESSAGE / QUERY</span>

                  <textarea
                    name="message"
                    rows="5"
                    placeholder="Ask about workshop timings, wedding sangeet packages, studio rentals..."
                    required
                  />
                </label>


                <button
                  type="submit"
                  className="contact-form__submit"
                >
                  <span>
                    SUBMIT INQUIRY
                  </span>

                  <span>
                    ↗
                  </span>
                </button>

              </form>

            ) : (

              <div className="contact-form__success">

                <div className="contact-form__success-mark">
                  ✓
                </div>

                <h4>
                  THANK YOU.
                </h4>

                <p>
                  Your inquiry has been received.
                  The Ethos team will get back to
                  you shortly.
                </p>

                <button
                  type="button"
                  onClick={() => setSubmitted(false)}
                >
                  SEND ANOTHER INQUIRY ↗
                </button>

              </div>

            )}

          </div>

        </div>

      </div>
    </section>
  );
}

export default ContactSection;
