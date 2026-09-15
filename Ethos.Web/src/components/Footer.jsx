import { useNavigate, useLocation } from "react-router-dom";
import "../styles/footer.css";
import ethosEmblem from "../assets/brand/ethos-emblem.png";
import shanmukaPhoto from "../assets/shanmuka.jpg";

function Footer({ onOpenPolicy }) {
  const navigate = useNavigate();
  const location = useLocation();

  const handleNav = (targetPath, sectionId) => {
    if (targetPath) {
      navigate(targetPath);
      window.scrollTo({ top: 0, behavior: "smooth" });
      return;
    }

    if (sectionId) {
      if (location.pathname !== "/") {
        navigate("/", { state: { scrollTo: sectionId } });
      } else {
        const element = document.getElementById(sectionId);
        if (element) {
          element.scrollIntoView({
            behavior: "smooth",
            block: "start",
          });
        }
      }
    }
  };

  const scrollToTop = () => {
    window.scrollTo({
      top: 0,
      behavior: "smooth",
    });
  };

  return (
    <footer className="site-footer">
      {/* TOP MOVING TICKER */}
      <div className="footer-ticker">
        <div className="footer-ticker__track">
          <div className="footer-ticker__group">
            <span>KUKATPALLY MAIN HALL</span>
            <i>✦</i>
            <span>DANCE</span>
            <i>✦</i>
            <span>CREATE</span>
            <i>✦</i>
            <span>INSPIRE</span>
            <i>✦</i>
            <span>PERFORM</span>
            <i>✦</i>
            <span>WORKSHOPS</span>
            <i>✦</i>
            <span>HYDERABAD</span>
            <i>✦</i>
            <span>MOVE WITH PURPOSE</span>
            <i>✦</i>
            <span>FIND YOUR RHYTHM</span>
            <i>✦</i>
            <span>BUILD YOUR CONFIDENCE</span>
            <i>✦</i>
            <span>JOIN THE COMMUNITY</span>
            <i>✦</i>
            <span>ETHOS DANCE STUDIO</span>
            <i>✦</i>
          </div>

          <div className="footer-ticker__group" aria-hidden="true">
            <span>KUKATPALLY MAIN HALL</span>
            <i>✦</i>
            <span>DANCE</span>
            <i>✦</i>
            <span>CREATE</span>
            <i>✦</i>
            <span>INSPIRE</span>
            <i>✦</i>
            <span>PERFORM</span>
            <i>✦</i>
            <span>WORKSHOPS</span>
            <i>✦</i>
            <span>HYDERABAD</span>
            <i>✦</i>
            <span>MOVE WITH PURPOSE</span>
            <i>✦</i>
            <span>FIND YOUR RHYTHM</span>
            <i>✦</i>
            <span>BUILD YOUR CONFIDENCE</span>
            <i>✦</i>
            <span>JOIN THE COMMUNITY</span>
            <i>✦</i>
            <span>ETHOS DANCE STUDIO</span>
            <i>✦</i>
          </div>
        </div>
      </div>

      {/* MAIN FOOTER (5 COLUMNS) */}
      <div className="footer-main">
        {/* BRAND */}
        <div className="footer-brand-copy">
          <span className="footer-brand-copy__eyebrow">MORE THAN DANCE</span>
          <h2>
            MOVE
            <span>WITH</span>
            US.
          </h2>
          <p>
            A space for movement, creativity, connection and everyone who wants
            to find their rhythm.
          </p>
        </div>

        {/* EXPLORE */}
        <div className="footer-column">
          <h3>EXPLORE</h3>
          <ul>
            <li>
              <button type="button" onClick={() => handleNav("/", "home")}>
                Home
              </button>
            </li>
            <li>
              <button type="button" onClick={() => handleNav("/classes")}>
                Classes
              </button>
            </li>
            <li>
              <button type="button" onClick={() => handleNav("/workshops")}>
                Workshops
              </button>
            </li>
            <li>
              <button type="button" onClick={() => handleNav("/events")}>
                Events
              </button>
            </li>
            <li>
              <button type="button" onClick={() => handleNav("/gallery")}>
                Gallery
              </button>
            </li>
            <li>
              <button type="button" onClick={() => handleNav(null, "about")}>
                Our Story
              </button>
            </li>
            <li>
              <button type="button" onClick={() => handleNav(null, "founders")}>
                Founders
              </button>
            </li>
            <li>
              <button type="button" onClick={() => handleNav(null, "trainers")}>
                Trainers
              </button>
            </li>
          </ul>
        </div>

        {/* CONNECT */}
        <div className="footer-column">
          <h3>CONNECT</h3>
          <ul>
            <li>
              <button type="button" onClick={() => handleNav(null, "contact")}>
                Contact
              </button>
            </li>
            <li>
              <button type="button" onClick={() => handleNav(null, "contact")}>
                Get Started
              </button>
            </li>
            <li>
              <button type="button" onClick={() => handleNav("/login")}>
                Login
              </button>
            </li>
          </ul>
        </div>

        {/* FOLLOW */}
        <div className="footer-column">
          <h3>FOLLOW</h3>
          <ul>
            <li>
              <a
                href="https://instagram.com"
                target="_blank"
                rel="noreferrer"
              >
                Instagram <span>↗</span>
              </a>
            </li>
            <li>
              <a
                href="https://youtube.com"
                target="_blank"
                rel="noreferrer"
              >
                YouTube <span>↗</span>
              </a>
            </li>
            <li>
              <a
                href="https://wa.me/918341701113"
                target="_blank"
                rel="noreferrer"
              >
                WhatsApp <span>↗</span>
              </a>
            </li>
          </ul>
        </div>

        {/* LEGAL */}
        <div className="footer-column">
          <h3>LEGAL</h3>
          <ul>
            <li>
              <button
                type="button"
                className="footer-policy-btn"
                onClick={() => onOpenPolicy && onOpenPolicy("privacy")}
              >
                Privacy Policy
              </button>
            </li>
            <li>
              <button
                type="button"
                className="footer-policy-btn"
                onClick={() => onOpenPolicy && onOpenPolicy("terms")}
              >
                Terms &amp; Conditions
              </button>
            </li>
            <li>
              <button
                type="button"
                className="footer-policy-btn"
                onClick={() => onOpenPolicy && onOpenPolicy("refund")}
              >
                Refund Policy
              </button>
            </li>
          </ul>
        </div>
      </div>

      {/* OVERSIZED ETHOS BRAND LOCKUP */}
      <div className="footer-brand">
        <div className="footer-brand__lockup">
          <div className="footer-brand__emblem-wrap">
            <img
              src={ethosEmblem}
              alt="Ethos Dance Studio"
              className="footer-brand__emblem"
            />
          </div>

          <div className="footer-brand__wordmark">
            <div className="footer-brand__ethos">ETHOS</div>
            <div className="footer-brand__subtitle">DANCE STUDIO</div>
          </div>
        </div>
      </div>

      {/* CREATOR / COPYRIGHT */}
      <div className="footer-bottom">
        <div className="footer-bottom__copyright">
          © 2026 ETHOS DANCE STUDIO
        </div>

        <div className="footer-bottom__creator">
          <span>Crafted with</span>
          <span className="footer-bottom__heart">♥</span>
          <span>for Ethos Dance Studio by</span>
          <div className="footer-bottom__creator-profile">
            <img src={shanmukaPhoto} alt="Shanmuka" />
            <strong>Shanmuka</strong>
          </div>
        </div>

        <button
          type="button"
          className="footer-back-top"
          onClick={scrollToTop}
        >
          BACK TO TOP
          <span className="footer-back-top__circle">↑</span>
        </button>
      </div>
    </footer>
  );
}

export default Footer;
