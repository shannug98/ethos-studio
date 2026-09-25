import React from "react";
import { Link } from "react-router-dom";
import { ArrowRight, Sparkles } from "lucide-react";
import "./ClassesComingSoon.css";

export default function ClassesComingSoon() {
  return (
    <div className="coming-soon-page">
      <div className="coming-soon-ambient-glow" />
      <div className="coming-soon-container">
        <div className="coming-soon-badge">
          <Sparkles size={14} />
          <span>Ethos Dance Studio</span>
        </div>
        <h2 className="coming-soon-category">CLASSES</h2>
        <h1 className="coming-soon-title">COMING SOON</h1>
        <p className="coming-soon-subtitle">
          Our regular studio classes and dance curriculum are currently in preparation. Stay tuned!
        </p>
        <div className="coming-soon-actions">
          <Link to="/workshops" className="coming-soon-btn primary">
            <span>Explore Active Workshops</span>
            <ArrowRight size={16} />
          </Link>
          <Link to="/" className="coming-soon-btn secondary">
            <span>Back to Home</span>
          </Link>
        </div>
      </div>
    </div>
  );
}
