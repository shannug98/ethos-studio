import React from "react";
import { useNavigate } from "react-router-dom";
import "./LoginComingSoonModal.css";

export default function LoginComingSoonModal({ isOpen, onClose }) {
  const navigate = useNavigate();

  if (!isOpen) return null;

  const handleContact = () => {
    onClose?.();
    navigate("/", { state: { scrollTo: "contact" } });
  };

  return (
    <div className="ethos-coming-soon-overlay" onClick={onClose} role="dialog" aria-modal="true">
      <div className="ethos-coming-soon-card" onClick={(e) => e.stopPropagation()}>
        <button
          type="button"
          className="ethos-coming-soon-close"
          onClick={onClose}
          aria-label="Close"
        >
          ✕
        </button>

        <div className="ethos-coming-soon-badge">
          <span>✦</span> MEMBER SERVICES
        </div>

        <h2 className="ethos-coming-soon-title">
          Coming <em>Soon</em>
        </h2>

        <p className="ethos-coming-soon-desc">
          Login and member services are coming soon. Please contact Ethos Dance Studio for more information, admissions, or workshop reservations.
        </p>

        <div className="ethos-coming-soon-actions">
          <button
            type="button"
            className="ethos-coming-soon-btn ethos-coming-soon-btn--primary"
            onClick={handleContact}
          >
            CONTACT ETHOS →
          </button>

          <button
            type="button"
            className="ethos-coming-soon-btn ethos-coming-soon-btn--secondary"
            onClick={onClose}
          >
            CONTINUE EXPLORING
          </button>
        </div>
      </div>
    </div>
  );
}
