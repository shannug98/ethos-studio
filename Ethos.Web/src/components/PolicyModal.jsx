import { useEffect } from "react";
import "../styles/policy-modal.css";
import ethosEmblem from "../assets/brand/ethos-emblem.png";

function PolicyModal({ isOpen, policyType, onClose, onSelectPolicy }) {
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === "Escape" && isOpen) {
        onClose();
      }
    };

    if (isOpen) {
      document.body.style.overflow = "hidden";
      window.addEventListener("keydown", handleKeyDown);
    } else {
      document.body.style.overflow = "";
    }

    return () => {
      document.body.style.overflow = "";
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, onClose]);

  if (!isOpen || !policyType) return null;

  const handleBackdropClick = (e) => {
    if (e.target.classList.contains("ethos-policy-modal__backdrop")) {
      onClose();
    }
  };

  return (
    <div
      className="ethos-policy-modal__backdrop"
      onClick={handleBackdropClick}
      role="dialog"
      aria-modal="true"
    >
      <div className="ethos-policy-modal__card">
        <button
          type="button"
          className="ethos-policy-modal__close"
          onClick={onClose}
          aria-label="Close modal"
        >
          ✕
        </button>

        <div className="ethos-policy-modal__header-logo">
          <img src={ethosEmblem} alt="Ethos Emblem" />
        </div>

        {policyType === "refund" && (
          <div className="ethos-policy-modal__content">
            <h1 className="ethos-policy-modal__title ethos-policy-modal__title--refund">
              Cancellation and Refund Policy
            </h1>

            <section className="ethos-policy-modal__section">
              <h2>No Cancellations or Refunds</h2>
              <p>
                At ETHOS DANCE STUDIO, we strive to provide the best possible
                experience for our dancers and community members. As part of our
                commitment to transparency, we want to clarify that all
                purchases, workshop registrations, masterclass passes, and
                monthly subscription passes made on our platform are final.{" "}
                <strong className="ethos-policy-modal__text-danger">
                  We do not entertain any cancellation or refund requests.
                </strong>
              </p>
              <p>
                We encourage you to review all class schedules, dates, and
                venue details before making a purchase. If you encounter any
                unexpected scheduling conflict or emergency, our support team
                is always here to assist you with batch rescheduling or pass
                transfer options where applicable.
              </p>
            </section>

            <section className="ethos-policy-modal__section">
              <h2>Pass Rescheduling &amp; Transfer Requests</h2>
              <p>
                If a dancer is unable to attend a registered workshop due to
                medical reasons or pre-notified emergencies, pass transfer
                requests to an upcoming batch or friend may be requested up to{" "}
                <strong>24 hours prior to class start time</strong> by
                contacting our hotline on WhatsApp:{" "}
                <a
                  href="https://wa.me/918341701113"
                  target="_blank"
                  rel="noreferrer"
                  className="ethos-policy-modal__link-green"
                >
                  +91 83417 01113
                </a>
                .
              </p>
            </section>
          </div>
        )}

        {policyType === "privacy" && (
          <div className="ethos-policy-modal__content">
            <h1 className="ethos-policy-modal__title ethos-policy-modal__title--privacy">
              Privacy Policy
            </h1>

            <p className="ethos-policy-modal__intro">
              The ETHOS DANCE STUDIO website and WhatsApp groups (&quot;Platform&quot;)
              are made available to you by ETHOS DANCE STUDIO (hereinafter
              may be referred to as the &quot;Company&quot;, &quot;we&quot;, &quot;us&quot;, and &quot;our&quot;).
              We respect your privacy and are committed to protecting it through
              our compliance with this privacy policy. This policy describes:
              (i) the type of information that the Company may collect from you
              when you access or use its websites (ethosdancestudio.com), WhatsApp
              groups, and other online services (collectively referred to as the
              &quot;Services&quot;); and (ii) the Company&apos;s practices for collecting,
              using, maintaining, protecting, and disclosing that information.
              This Privacy Policy is published in accordance with the (Indian)
              Information Technology Act, 2000 and the rules/regulations framed
              thereunder, including the (Indian) Information Technology (Reasonable
              Security Practices and Procedures and Sensitive Personal Data or
              Information) Rules, 2011.
            </p>

            <section className="ethos-policy-modal__section">
              <h2>1. Application of Our Privacy Policy</h2>
              <p>
                This policy specifically addresses the Information collected
                through the Company&apos;s Services, including email, text, and other
                electronic communications associated with those Services. However,
                the policy does not extend to the information provided to or
                collected by third parties that users may use in connection with
                the Company&apos;s Services.
              </p>
            </section>

            <section className="ethos-policy-modal__section">
              <h2>2. Collection of the Information</h2>
              <p className="ethos-policy-modal__subsection-title">i. Definitions</p>
              <p>For the purposes of this privacy policy:</p>
              <ul className="ethos-policy-modal__list">
                <li>
                  <strong>&quot;Personal Information&quot;</strong> means any information
                  that relates to a natural person, which, either directly or
                  indirectly, in combination with other information available
                  with the Company, is capable of identifying the person
                  concerned.
                </li>
                <li>
                  <strong>&quot;Sensitive Personal Data or Information&quot;</strong>{" "}
                  means Personal Information of any individual relating to
                  password; financial information such as bank account or
                  credit card or debit card details; physical or physiological
                  health conditions; emergency contacts; or payment instrument
                  details.
                </li>
              </ul>

              <p className="ethos-policy-modal__subsection-title">
                ii. Information You Provide to Us
              </p>
              <p>
                We collect information directly from you when you register for
                class passes, purchase workshop tickets, join our WhatsApp
                community groups, or communicate with our support team.
              </p>
            </section>

            <section className="ethos-policy-modal__section">
              <h2>3. Use of Your Information</h2>
              <p>
                We use information that we collect about you or that you provide
                to us to present our Platform and its contents, provide you with
                dance class passes and workshop schedules, process payment
                transactions, fulfill orders, and notify you about changes to
                our Services.
              </p>
            </section>

            <section className="ethos-policy-modal__section">
              <h2>4. Data Security &amp; Storage</h2>
              <p>
                We have implemented reasonable administrative, technical, and
                physical security measures to protect your personal information
                against unauthorized access, loss, or misuse in compliance with
                IT Act 2000 rules.
              </p>
            </section>
          </div>
        )}

        {policyType === "terms" && (
          <div className="ethos-policy-modal__content">
            <h1 className="ethos-policy-modal__title ethos-policy-modal__title--terms">
              Terms and Conditions
            </h1>

            <p className="ethos-policy-modal__intro">
              <strong>This document is an electronic record</strong> in terms of
              the <em>Information Technology Act, 2000</em> and{" "}
              <em>Rule 3 of the Information Technology (Intermediaries guidelines) Rules, 2011</em>{" "}
              and the provisions pertaining to electronic records in various
              statutes as amended by the Information Technology Act, 2000.
              This electronic record is generated by a computer system and does
              not require any physical or digital signatures.
            </p>

            <p className="ethos-policy-modal__intro">
              This ETHOS USER Agreement (&quot;T&amp;C&quot;) applies to anyone&apos;s use and
              access of our websites [ethosdancestudio.com] / webpages, emails,
              WhatsApp groups and other online products and services offered by
              ETHOS (collectively, the &quot;Services&quot;) and our affiliates (&quot;ETHOS,&quot;
              &quot;we,&quot; &quot;us,&quot; or &quot;our&quot;). We are thrilled to welcome you to ETHOS,
              an innovative community-building dance space designed to bring
              individuals with shared interests together. Before you embark on
              this exciting journey of connection and choreography, we invite
              you to familiarize yourself with our Terms and Conditions of Use.
            </p>

            <section className="ethos-policy-modal__section">
              <h2>Acceptance of Terms:</h2>
              <p>
                By accessing (or even merely visiting) any part of the ETHOS
                website, you signify your{" "}
                <strong>
                  agreement to comply with and be bound by these Terms and
                  Conditions
                </strong>{" "}
                and other policies of the Company as posted on the application
                or website from time to time. If you do not agree with any part
                of these terms, we kindly request that you refrain from using
                our platform.
              </p>
            </section>

            <section className="ethos-policy-modal__section">
              <h2>Community Values:</h2>
              <p>
                ETHOS is dedicated to fostering a{" "}
                <strong>positive and inclusive environment</strong>. We encourage
                users to <strong>engage respectfully</strong>, promote{" "}
                <strong>diversity</strong>, and contribute to the overall{" "}
                <strong>well-being</strong> of the community. Any content that
                goes against these values may result in appropriate action,
                including <strong>account suspension</strong> or revocation of
                studio pass access.
              </p>
            </section>

            <section className="ethos-policy-modal__section">
              <h2>Privacy Matters:</h2>
              <p>
                Your <strong>privacy</strong> is paramount to us. Our{" "}
                <strong>Privacy Policy</strong> outlines the{" "}
                <strong>collection, use, and protection</strong> of your personal
                information. Please take a moment to review this document and
                our privacy policy on{" "}
                <button
                  type="button"
                  className="ethos-policy-modal__inline-link"
                  onClick={() => onSelectPolicy && onSelectPolicy("privacy")}
                >
                  Privacy Policy
                </button>{" "}
                to understand how your data is handled within the ETHOS platform.
              </p>
            </section>

            <section className="ethos-policy-modal__section">
              <h2>Studio &amp; Class Regulations:</h2>
              <ul className="ethos-policy-modal__list">
                <li>
                  Clean indoor footwear or bare feet required inside wooden
                  dance floors.
                </li>
                <li>
                  Students must arrive 10 minutes prior to scheduled batch times
                  for warm-up safety.
                </li>
                <li>
                  Monthly class passes are non-transferable unless authorized by
                  studio management.
                </li>
              </ul>
            </section>
          </div>
        )}
      </div>
    </div>
  );
}

export default PolicyModal;
