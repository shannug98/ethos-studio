import { useState } from "react";
import { useNavigate } from "react-router-dom";
import "../styles/ethos-echo.css";

function EthosEcho() {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();

  const handleNav = (targetPath, sectionId) => {
    setOpen(false);
    if (targetPath) {
      navigate(targetPath);
      window.scrollTo({ top: 0, behavior: "smooth" });
      return;
    }
    if (sectionId) {
      if (window.location.pathname !== "/") {
        navigate("/", { state: { scrollTo: sectionId } });
      } else {
        const element = document.getElementById(sectionId);
        if (element) {
          element.scrollIntoView({ behavior: "smooth", block: "start" });
        }
      }
    }
  };

  return (
    <>
      {/* LAUNCHER BUTTON */}
      <button
        type="button"
        className={`ethos-echo-launcher ${
          open ? "ethos-echo-launcher--open" : ""
        }`}
        onClick={() => setOpen(!open)}
        aria-label="Toggle Ethos Echo Studio Guide"
        aria-expanded={open}
      >
        <span className="ethos-echo-launcher__glow" />

        {/* EMBLEM WITH ROTATING RING */}
        <div className="ethos-echo-launcher__emblem">
          <span className="ethos-echo-launcher__ring" />
          <span className="ethos-echo-launcher__symbol">E</span>
          <span className="ethos-echo-launcher__dot" />
        </div>

        {/* TEXT LOCKUP */}
        <div className="ethos-echo-launcher__text">
          <div className="ethos-echo-launcher__title">
            <strong>ETHOS</strong>
            <span className="ethos-echo-launcher__badge">ECHO</span>
          </div>

          <span className="ethos-echo-launcher__subtitle">STUDIO GUIDE</span>
        </div>

        {/* ARROW / CLOSE BUTTON */}
        <span className="ethos-echo-launcher__action">
          {open ? "✕" : "↗"}
        </span>
      </button>

      {/* ECHO CHAT PANEL */}
      {open && (
        <div className="ethos-echo-panel">
          <div className="ethos-echo-panel__header">
            <div className="ethos-echo-panel__identity">
              <div className="ethos-echo-panel__avatar">E</div>

              <div>
                <strong>ETHOS ECHO</strong>
                <span>STUDIO GUIDE</span>
              </div>
            </div>

            <button
              type="button"
              className="ethos-echo-panel__close"
              onClick={() => setOpen(false)}
              aria-label="Close Ethos Echo"
            >
              ✕
            </button>
          </div>

          <div className="ethos-echo-panel__body">
            <div className="ethos-echo-message">
              <span className="ethos-echo-message__pulse" />
              <p>
                Hi 👋
                <br />
                I&apos;m <strong>Ethos Echo</strong>, your virtual studio guide.
                <br />
                How can I help you today?
              </p>
            </div>

            <p className="ethos-echo-prompt">Select a topic:</p>

            <div className="ethos-echo-options">
              <button
                type="button"
                onClick={() => handleNav("/workshops")}
              >
                <span>VIEW WORKSHOPS</span>
                <i>→</i>
              </button>

              <button
                type="button"
                onClick={() => handleNav("/events")}
              >
                <span>ETHOS EVENTS</span>
                <i>→</i>
              </button>

              <button
                type="button"
                onClick={() => handleNav("/gallery")}
              >
                <span>EXPLORE GALLERY</span>
                <i>→</i>
              </button>

              <button
                type="button"
                onClick={() => handleNav(null, "founders")}
              >
                <span>MEET THE TEAM</span>
                <i>→</i>
              </button>

              <button
                type="button"
                onClick={() => handleNav(null, "contact")}
              >
                <span>STUDIO LOCATION & MAP</span>
                <i>→</i>
              </button>

              <a
                href="https://wa.me/918341701113"
                target="_blank"
                rel="noreferrer"
                className="ethos-echo-options__whatsapp"
                onClick={() => setOpen(false)}
              >
                <span>CHAT ON WHATSAPP</span>
                <i>↗</i>
              </a>
            </div>
          </div>

          <div className="ethos-echo-panel__footer">
            <span>ETHOS ECHO • ALWAYS HERE TO HELP</span>
          </div>
        </div>
      )}
    </>
  );
}

export default EthosEcho;
