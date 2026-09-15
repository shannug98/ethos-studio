import { useEffect, useState } from "react";
import "../styles/floating-actions.css";

function InstagramIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <rect x="3" y="3" width="18" height="18" rx="5" />
      <circle cx="12" cy="12" r="4" />
      <circle cx="17.4" cy="6.7" r="1" className="icon-fill" />
    </svg>
  );
}

function YouTubeIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path
        d="M21.6 7.2a2.7 2.7 0 0 0-1.9-1.9C18 4.8 12 4.8 12 4.8s-6 0-7.7.5a2.7 2.7 0 0 0-1.9 1.9C1.9 8.9 1.9 12 1.9 12s0 3.1.5 4.8a2.7 2.7 0 0 0 1.9 1.9c1.7.5 7.7.5 7.7.5s6 0 7.7-.5a2.7 2.7 0 0 0 1.9-1.9c.5-1.7.5-4.8.5-4.8s0-3.1-.5-4.8Z"
      />
      <path
        d="m10 15.2 5-3.2-5-3.2v6.4Z"
        className="icon-play"
      />
    </svg>
  );
}

function WhatsAppIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path
        d="M20.5 3.5A11.8 11.8 0 0 0 12.1 0C5.5 0 .1 5.4.1 12c0 2.1.6 4.1 1.6 5.9L0 24l6.3-1.7a12 12 0 0 0 5.8 1.5h.1c6.6 0 11.9-5.4 11.9-12 0-3.2-1.3-6.1-3.6-8.3Zm-8.4 18.2h-.1c-1.8 0-3.6-.5-5.1-1.4l-.4-.2-3.7 1 1-3.6-.2-.4A9.9 9.9 0 0 1 2.1 12c0-5.5 4.5-10 10-10 2.7 0 5.2 1 7.1 2.9a9.9 9.9 0 0 1 2.9 7.1c0 5.5-4.5 9.7-10 9.7Z"
      />
      <path
        d="M17.6 14.7c-.3-.2-1.8-.9-2.1-1-.3-.1-.5-.2-.7.2-.2.3-.8 1-.9 1.2-.2.2-.3.2-.6.1-.3-.2-1.2-.4-2.3-1.4-.9-.8-1.5-1.8-1.7-2.1-.2-.3 0-.5.1-.7.1-.1.3-.3.4-.5.1-.2.2-.3.3-.5.1-.2 0-.4 0-.5-.1-.1-.7-1.7-1-2.3-.3-.6-.5-.5-.7-.5h-.6c-.2 0-.5.1-.8.4-.3.3-1 1-1 2.4s1 2.8 1.1 3c.1.2 2 3.1 4.9 4.3.7.3 1.2.5 1.6.6.7.2 1.3.2 1.8.1.5-.1 1.8-.7 2.1-1.3.3-.6.3-1.2.2-1.3-.1-.2-.3-.3-.6-.5Z"
        className="icon-fill"
      />
    </svg>
  );
}

function EthosEmblem() {
  return (
    <div className="ethos-echo__emblem">
      <span className="ethos-echo__emblem-ring" />
      <span className="ethos-echo__emblem-mark">E</span>
    </div>
  );
}

function FloatingActions() {
  const [scrolled, setScrolled] = useState(false);
  const [showLabel, setShowLabel] = useState(false);
  const [chatOpen, setChatOpen] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setScrolled(window.scrollY > 120);
    };

    const handleOpenAI = () => {
      setChatOpen(true);
    };

    handleScroll();

    window.addEventListener("scroll", handleScroll, {
      passive: true,
    });
    window.addEventListener("ethos-ai-open", handleOpenAI);

    return () => {
      window.removeEventListener("scroll", handleScroll);
      window.removeEventListener("ethos-ai-open", handleOpenAI);
    };
  }, []);

  const openEthosAI = () => {
    setChatOpen(true);
    window.dispatchEvent(new CustomEvent("ethos-ai-open"));
  };

  const scrollToSection = (id) => {
    const el = document.getElementById(id);
    if (el) {
      el.scrollIntoView({ behavior: "smooth" });
    }
  };

  return (
    <>
      {/* SOCIAL FLOATING RAIL */}
      <aside
        className={`floating-socials ${
          scrolled ? "floating-socials--scrolled" : ""
        }`}
        aria-label="Ethos social links"
      >
        <a
          href="https://www.instagram.com/"
          target="_blank"
          rel="noreferrer"
          className="floating-social floating-social--instagram"
          aria-label="Ethos Dance Studio on Instagram"
        >
          <span className="floating-social__icon">
            <InstagramIcon />
          </span>

          <span className="floating-social__shine" />
        </a>

        <a
          href="https://www.youtube.com/"
          target="_blank"
          rel="noreferrer"
          className="floating-social floating-social--youtube"
          aria-label="Ethos Dance Studio on YouTube"
        >
          <span className="floating-social__icon">
            <YouTubeIcon />
          </span>

          <span className="floating-social__shine" />
        </a>

        <a
          href="https://wa.me/918341701113"
          target="_blank"
          rel="noreferrer"
          className="floating-social floating-social--whatsapp"
          aria-label="Chat with Ethos Dance Studio on WhatsApp"
        >
          <span className="floating-social__icon">
            <WhatsAppIcon />
          </span>

          <span className="floating-social__shine" />
        </a>
      </aside>

      {/* ETHOS ECHO AI */}
      {chatOpen ? (
        <div className="ethos-concierge ethos-concierge--open">
          <div className="ethos-concierge__window">
            <div className="ethos-concierge__header">
              <div className="ethos-concierge__identity">
                <div className="ethos-concierge__avatar">E</div>
                <div>
                  <strong>ETHOS ECHO</strong>
                  <span>
                    <i />
                    Online · Studio Guide
                  </span>
                </div>
              </div>

              <button
                type="button"
                onClick={() => setChatOpen(false)}
                className="ethos-concierge__close"
              >
                ×
              </button>
            </div>

            <div className="ethos-concierge__body">
              <div className="ethos-concierge__message">
                Hi! 👋
                <br />
                I&apos;m Ethos Echo, your studio guide.
                <br />
                How can I help you today?
              </div>

              <span className="ethos-concierge__time">Just now</span>

              <div className="ethos-concierge__quick-title">
                Or select a quick option:
              </div>

              <button
                type="button"
                className="ethos-concierge__quick"
                onClick={() => scrollToSection("workshops")}
              >
                <span>Book Workshop</span>
                <span>→</span>
              </button>

              <button
                type="button"
                className="ethos-concierge__quick"
                onClick={() => scrollToSection("workshops")}
              >
                <span>View Packages</span>
                <span>→</span>
              </button>

              <button
                type="button"
                className="ethos-concierge__quick"
                onClick={() => scrollToSection("workshops")}
              >
                <span>Class Timings</span>
                <span>→</span>
              </button>

              <button
                type="button"
                className="ethos-concierge__quick"
                onClick={() => scrollToSection("contact")}
              >
                <span>Location</span>
                <span>→</span>
              </button>

              <button
                type="button"
                className="ethos-concierge__quick"
                onClick={() => scrollToSection("contact")}
              >
                <span>Talk to Ethos</span>
                <span>→</span>
              </button>
            </div>

            <div className="ethos-concierge__input">
              <input type="text" placeholder="Type a message..." />
              <button type="button">↑</button>
            </div>

            <div className="ethos-concierge__powered">
              Powered by <strong>Ethos AI</strong>
            </div>
          </div>
        </div>
      ) : (
        <button
          type="button"
          className={`ethos-echo ${
            scrolled ? "ethos-echo--scrolled" : ""
          } ${showLabel ? "ethos-echo--label" : ""}`}
          onClick={openEthosAI}
          onMouseEnter={() => setShowLabel(true)}
          onMouseLeave={() => setShowLabel(false)}
          aria-label="Open Ethos Echo AI assistant"
        >
          <span className="ethos-echo__glow" />

          <EthosEmblem />

          <span className="ethos-echo__content">
            <span className="ethos-echo__name">
              ETHOS <strong>ECHO</strong>
            </span>

            <span className="ethos-echo__status">
              <span className="ethos-echo__status-dot" />
              YOUR STUDIO GUIDE
            </span>
          </span>

          <span className="ethos-echo__arrow">↗</span>

          <span className="ethos-echo__shine" />
        </button>
      )}
    </>
  );
}

export default FloatingActions;
