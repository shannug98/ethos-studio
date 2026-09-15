import { useState } from "react";
import "../styles/ethos-ai.css";

function EthosAI() {
  const [open, setOpen] = useState(false);

  const scrollTo = (id) => {
    setOpen(false);
    const element = document.getElementById(id);
    if (element) {
      element.scrollIntoView({
        behavior: "smooth",
        block: "start",
      });
    }
  };

  return (
    <>
      {/* LAUNCHER BUTTON */}
      <button
        type="button"
        className={`ethos-ai-launcher ${
          open ? "ethos-ai-launcher--open" : ""
        }`}
        onClick={() => setOpen(!open)}
        aria-label="Toggle Ethos AI Studio Guide"
        aria-expanded={open}
      >
        <span className="ethos-ai-launcher__glow" />

        {/* EMBLEM WITH ROTATING RING */}
        <div className="ethos-ai-launcher__emblem">
          <span className="ethos-ai-launcher__ring" />
          <span className="ethos-ai-launcher__symbol">E</span>
          <span className="ethos-ai-launcher__dot" />
        </div>

        {/* TEXT LOCKUP */}
        <div className="ethos-ai-launcher__text">
          <div className="ethos-ai-launcher__title">
            <strong>ETHOS</strong>
            <span className="ethos-ai-launcher__badge">AI</span>
          </div>

          <span className="ethos-ai-launcher__subtitle">
            STUDIO GUIDE
          </span>
        </div>

        {/* ARROW / CLOSE BUTTON */}
        <span className="ethos-ai-launcher__action">
          {open ? "✕" : "↗"}
        </span>
      </button>

      {/* AI CHAT PANEL */}
      {open && (
        <div className="ethos-ai-panel">
          <div className="ethos-ai-panel__header">
            <div className="ethos-ai-panel__identity">
              <div className="ethos-ai-panel__avatar">E</div>

              <div>
                <strong>ETHOS AI</strong>
                <span>STUDIO GUIDE</span>
              </div>
            </div>

            <button
              type="button"
              className="ethos-ai-panel__close"
              onClick={() => setOpen(false)}
              aria-label="Close Ethos AI"
            >
              ✕
            </button>
          </div>

          <div className="ethos-ai-panel__body">
            <div className="ethos-ai-message">
              <span className="ethos-ai-message__pulse" />
              <p>
                Hi 👋
                <br />
                I&apos;m your Ethos virtual guide.
                <br />
                How can I help you today?
              </p>
            </div>

            <p className="ethos-ai-prompt">Select a quick topic:</p>

            <div className="ethos-ai-options">
              <button type="button" onClick={() => scrollTo("workshops")}>
                <span>BOOK A WORKSHOP</span>
                <i>→</i>
              </button>

              <button type="button" onClick={() => scrollTo("workshops")}>
                <span>VIEW PACKAGES & TIMINGS</span>
                <i>→</i>
              </button>

              <button type="button" onClick={() => scrollTo("founders")}>
                <span>MEET THE TEAM</span>
                <i>→</i>
              </button>

              <button type="button" onClick={() => scrollTo("contact")}>
                <span>STUDIO LOCATION & MAP</span>
                <i>→</i>
              </button>

              <a
                href="https://wa.me/918341701113"
                target="_blank"
                rel="noreferrer"
                className="ethos-ai-options__whatsapp"
                onClick={() => setOpen(false)}
              >
                <span>CHAT ON WHATSAPP</span>
                <i>↗</i>
              </a>
            </div>
          </div>

          <div className="ethos-ai-panel__footer">
            <span>ETHOS AI • ALWAYS HERE TO HELP</span>
          </div>
        </div>
      )}
    </>
  );
}

export default EthosAI;
