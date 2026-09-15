import { useEffect, useState } from "react";
import logoImg from "../assets/logo.png";
import "../styles/brand-intro.css";

function BrandIntro() {
  const [showIntro, setShowIntro] = useState(false);

  useEffect(() => {
    const navigation = performance.getEntriesByType("navigation")[0];

    const isRefresh =
      navigation?.type === "reload";

    const hasSeenIntro =
      sessionStorage.getItem("ethos-intro-seen") === "true";

    const shouldShow =
      !hasSeenIntro || isRefresh;

    if (!shouldShow) {
      setShowIntro(false);
      return;
    }

    setShowIntro(true);

    sessionStorage.setItem(
      "ethos-intro-seen",
      "true"
    );

    const timer = setTimeout(() => {
      setShowIntro(false);
    }, 4200);

    return () => clearTimeout(timer);
  }, []);

  if (!showIntro) return null;

  return (
    <div className="brand-intro">
      <div className="brand-intro__background" />

      <div className="brand-intro__content">

        <div className="brand-intro__logo">
          <img
            src={logoImg}
            alt="Ethos Logo"
            className="brand-intro__logo-img"
          />

          <div className="brand-intro__logo-text">
            <h1 className="brand-intro__title">
              ETHOS
            </h1>

            <p className="brand-intro__subtitle">
              DANCE STUDIO
            </p>
          </div>
        </div>

        <div className="brand-intro__tagline">
          <span>MOVE</span>
          <span>•</span>
          <span>LEARN</span>
          <span>•</span>
          <span>BELONG</span>
        </div>

        <div className="brand-intro__loading">
          <div className="brand-intro__loading-bar">
            <span />
          </div>
        </div>
      </div>
    </div>
  );
}

export default BrandIntro;
