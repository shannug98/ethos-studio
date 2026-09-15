import "../styles/floating-socials.css";

function FloatingSocials() {
  return (
    <div className="ethos-floating-socials">

      <a
        href="https://www.instagram.com/"
        target="_blank"
        rel="noreferrer"
        className="ethos-social ethos-social--instagram"
        aria-label="Instagram"
      >
        <span className="ethos-social__shine" />

        <svg viewBox="0 0 24 24" aria-hidden="true">
          <rect
            x="3"
            y="3"
            width="18"
            height="18"
            rx="5"
          />

          <circle
            cx="12"
            cy="12"
            r="4"
          />

          <circle
            cx="17.5"
            cy="6.5"
            r="1"
            className="ethos-social__dot"
          />
        </svg>
      </a>


      <a
        href="https://www.youtube.com/"
        target="_blank"
        rel="noreferrer"
        className="ethos-social ethos-social--youtube"
        aria-label="YouTube"
      >
        <span className="ethos-social__shine" />

        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M21.2 7.1a2.8 2.8 0 0 0-2-2C17.4 4.6 12 4.6 12 4.6s-5.4 0-7.2.5a2.8 2.8 0 0 0-2 2C2.3 8.9 2.3 12 2.3 12s0 3.1.5 4.9a2.8 2.8 0 0 0 2 2c1.8.5 7.2.5 7.2.5s5.4 0 7.2-.5a2.8 2.8 0 0 0 2-2c.5-1.8.5-4.9.5-4.9s0-3.1-.5-4.9Z" />

          <path
            d="m10 15.4 5-3.4-5-3.4v6.8Z"
            className="ethos-social__play"
          />
        </svg>
      </a>


      <a
        href="https://wa.me/918341701113"
        target="_blank"
        rel="noreferrer"
        className="ethos-social ethos-social--whatsapp"
        aria-label="WhatsApp"
      >
        <span className="ethos-social__shine" />

        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path
            d="M20.5 3.5A11.8 11.8 0 0 0 12.1 0C5.6 0 .3 5.3.3 11.8c0 2.1.6 4.2 1.7 6L.2 24l6.4-1.7a11.8 11.8 0 0 0 5.5 1.4h.1c6.5 0 11.8-5.3 11.8-11.8 0-3.1-1.2-6.1-3.5-8.4Z"
          />

          <path
            d="M17.3 14.4c-.3-.2-1.8-.9-2.1-1-.3-.1-.5-.2-.7.2-.2.3-.8 1-.9 1.2-.2.2-.3.2-.6.1-1.7-.8-2.8-1.4-3.9-3.2-.3-.5.3-.5.8-1.7.1-.2.1-.4 0-.6-.1-.2-.7-1.7-.9-2.3-.2-.6-.5-.5-.7-.5h-.6c-.2 0-.6.1-.9.4-.3.3-1.1 1.1-1.1 2.6s1.1 3 1.2 3.2c.1.2 2.1 3.3 5.2 4.6 1.9.8 2.6.9 3.5.8.6-.1 1.8-.7 2-1.3.3-.6.3-1.2.2-1.3-.1-.2-.3-.3-.6-.4Z"
            className="ethos-social__phone"
          />
        </svg>
      </a>

    </div>
  );
}

export default FloatingSocials;
