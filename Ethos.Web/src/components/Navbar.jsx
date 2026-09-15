import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import "../styles/navbar.css";
import LoginComingSoonModal from "./common/LoginComingSoonModal";

import emblem from "../assets/logo/ethos-emblem.png";

function Navbar() {
  const location = useLocation();
  const navigate = useNavigate();

  const [aboutOpen, setAboutOpen] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [mobileAboutOpen, setMobileAboutOpen] = useState(false);
  const [hidden, setHidden] = useState(false);
  const [loginModalOpen, setLoginModalOpen] = useState(false);

  const aboutRef = useRef(null);

  // --------------------------------------------------
  // Hide / show navbar while scrolling
  // --------------------------------------------------
  useEffect(() => {
    let lastScrollY = window.scrollY;

    const handleScroll = () => {
      const currentScrollY = window.scrollY;

      if (currentScrollY > 100 && currentScrollY > lastScrollY) {
        setHidden(true);
      } else {
        setHidden(false);
      }

      lastScrollY = currentScrollY;
    };

    window.addEventListener("scroll", handleScroll, { passive: true });

    return () => {
      window.removeEventListener("scroll", handleScroll);
    };
  }, []);

  // --------------------------------------------------
  // Close menus whenever route changes
  // --------------------------------------------------
  useEffect(() => {
    setAboutOpen(false);
    setMobileOpen(false);
    setMobileAboutOpen(false);
  }, [location.pathname, location.hash]);

  // --------------------------------------------------
  // Close About dropdown when clicking outside
  // --------------------------------------------------
  useEffect(() => {
    const handleOutsideClick = (event) => {
      if (
        aboutRef.current &&
        !aboutRef.current.contains(event.target)
      ) {
        setAboutOpen(false);
      }
    };

    document.addEventListener("mousedown", handleOutsideClick);

    return () => {
      document.removeEventListener("mousedown", handleOutsideClick);
    };
  }, []);

  // --------------------------------------------------
  // Section navigation
  // --------------------------------------------------
  const goToSection = (sectionId) => {
    setAboutOpen(false);
    setMobileOpen(false);
    setMobileAboutOpen(false);

    if (location.pathname === "/") {
      const section = document.getElementById(sectionId);

      if (section) {
        section.scrollIntoView({
          behavior: "smooth",
          block: "start",
        });
      }

      return;
    }

    navigate("/", {
      state: {
        scrollTo: sectionId,
      },
    });
  };

  // --------------------------------------------------
  // Home
  // --------------------------------------------------
  const handleHome = () => {
    setAboutOpen(false);
    setMobileOpen(false);
    setMobileAboutOpen(false);

    if (location.pathname === "/") {
      window.scrollTo({
        top: 0,
        behavior: "smooth",
      });
    } else {
      navigate("/");
    }
  };

  // --------------------------------------------------
  // About dropdown
  // --------------------------------------------------
  const toggleAbout = (event) => {
    event.preventDefault();
    setAboutOpen((previous) => !previous);
  };

  // --------------------------------------------------
  // Mobile menu
  // --------------------------------------------------
  const toggleMobileMenu = () => {
    setMobileOpen((previous) => !previous);

    if (mobileOpen) {
      setMobileAboutOpen(false);
    }
  };

  const toggleMobileAbout = () => {
    setMobileAboutOpen((previous) => !previous);
  };

  return (
    <header
      className={`ethos-navbar ${
        hidden ? "ethos-navbar--hidden" : ""
      }`}
    >
      <div className="ethos-navbar__inner">

        {/* ------------------------------------------ */}
        {/* BRAND */}
        {/* ------------------------------------------ */}

        <button
          type="button"
          className="ethos-navbar__brand"
          onClick={handleHome}
          aria-label="Go to Ethos home"
        >
          <img
            src={emblem}
            alt="Ethos"
            className="ethos-navbar__emblem"
          />

          <span className="ethos-navbar__brand-name">
            ETHOS
          </span>
        </button>

        {/* ------------------------------------------ */}
        {/* DESKTOP NAVIGATION */}
        {/* ------------------------------------------ */}

        <nav className="ethos-navbar__nav">

          <button
            type="button"
            className="ethos-navbar__link ethos-navbar__link--button"
            onClick={handleHome}
          >
            HOME
          </button>

          <Link
            to="/classes"
            className="ethos-navbar__link"
          >
            CLASSES
          </Link>

          <Link
            to="/workshops"
            className="ethos-navbar__link"
          >
            WORKSHOPS
          </Link>

          <Link
            to="/events"
            className="ethos-navbar__link"
          >
            EVENTS
          </Link>

          <Link
            to="/gallery"
            className="ethos-navbar__link"
          >
            GALLERY
          </Link>

          {/* ---------------------------------------- */}
          {/* ABOUT */}
          {/* ---------------------------------------- */}

          <div
            className={`ethos-navbar__about ${
              aboutOpen
                ? "ethos-navbar__about--open"
                : ""
            }`}
            ref={aboutRef}
          >
            <button
              type="button"
              className="ethos-navbar__link ethos-navbar__about-trigger"
              onClick={toggleAbout}
              aria-expanded={aboutOpen}
              aria-haspopup="true"
            >
              ABOUT
              <span className="ethos-navbar__arrow">
                {aboutOpen ? "↑" : "↓"}
              </span>
            </button>

            {aboutOpen && (
              <div className="ethos-navbar__dropdown">

                <div className="ethos-navbar__dropdown-label">
                  ABOUT
                </div>

                <div className="ethos-navbar__dropdown-divider" />

                <button
                  type="button"
                  className="ethos-navbar__dropdown-item"
                  onClick={() => goToSection("about")}
                >
                  <span className="ethos-navbar__dropdown-title">
                    OUR STORY
                  </span>

                  <span className="ethos-navbar__dropdown-description">
                    The Ethos behind the movement
                  </span>
                </button>

                <button
                  type="button"
                  className="ethos-navbar__dropdown-item"
                  onClick={() => goToSection("founders")}
                >
                  <span className="ethos-navbar__dropdown-title">
                    FOUNDERS
                  </span>

                  <span className="ethos-navbar__dropdown-description">
                    The people behind Ethos
                  </span>
                </button>

                <button
                  type="button"
                  className="ethos-navbar__dropdown-item"
                  onClick={() => goToSection("trainers")}
                >
                  <span className="ethos-navbar__dropdown-title">
                    CREW
                  </span>

                  <span className="ethos-navbar__dropdown-description">
                    Meet the Ethos trainers
                  </span>
                </button>

                <button
                  type="button"
                  className="ethos-navbar__dropdown-item"
                  onClick={() => goToSection("contact")}
                >
                  <span className="ethos-navbar__dropdown-title">
                    CONTACT
                  </span>

                  <span className="ethos-navbar__dropdown-description">
                    Start a conversation
                  </span>
                </button>

              </div>
            )}
          </div>

        </nav>

        {/* ------------------------------------------ */}
        {/* RIGHT ACTIONS */}
        {/* ------------------------------------------ */}

        <div className="ethos-navbar__actions">
          <button
            type="button"
            className="ethos-navbar__cta"
            onClick={() => setLoginModalOpen(true)}
          >
            LOGIN
          </button>
        </div>

        {/* ------------------------------------------ */}
        {/* MOBILE HAMBURGER */}
        {/* ------------------------------------------ */}

        <button
          type="button"
          className={`ethos-navbar__mobile-toggle ${
            mobileOpen
              ? "ethos-navbar__mobile-toggle--open"
              : ""
          }`}
          onClick={toggleMobileMenu}
          aria-label={
            mobileOpen
              ? "Close navigation"
              : "Open navigation"
          }
          aria-expanded={mobileOpen}
        >
          <span />
          <span />
          <span />
        </button>

      </div>

      {/* -------------------------------------------- */}
      {/* MOBILE MENU */}
      {/* -------------------------------------------- */}

      <div
        className={`ethos-navbar__mobile-menu ${
          mobileOpen
            ? "ethos-navbar__mobile-menu--open"
            : ""
        }`}
      >
        <div className="ethos-navbar__mobile-menu-inner">

          <button
            type="button"
            className="ethos-navbar__mobile-link"
            onClick={handleHome}
          >
            HOME
          </button>

          <Link
            to="/classes"
            className="ethos-navbar__mobile-link"
          >
            CLASSES
          </Link>

          <Link
            to="/workshops"
            className="ethos-navbar__mobile-link"
          >
            WORKSHOPS
          </Link>

          <Link
            to="/events"
            className="ethos-navbar__mobile-link"
          >
            EVENTS
          </Link>

          <Link
            to="/gallery"
            className="ethos-navbar__mobile-link"
          >
            GALLERY
          </Link>

          {/* MOBILE ABOUT */}

          <div className="ethos-navbar__mobile-about">

            <button
              type="button"
              className="ethos-navbar__mobile-link ethos-navbar__mobile-about-trigger"
              onClick={toggleMobileAbout}
              aria-expanded={mobileAboutOpen}
            >
              <span>ABOUT</span>

              <span
                className={`ethos-navbar__mobile-about-arrow ${
                  mobileAboutOpen
                    ? "ethos-navbar__mobile-about-arrow--open"
                    : ""
                }`}
              >
                ↓
              </span>
            </button>

            {mobileAboutOpen && (
              <div className="ethos-navbar__mobile-submenu">

                <button
                  type="button"
                  onClick={() => goToSection("about")}
                >
                  OUR STORY
                </button>

                <button
                  type="button"
                  onClick={() => goToSection("founders")}
                >
                  FOUNDERS
                </button>

                <button
                  type="button"
                  onClick={() => goToSection("trainers")}
                >
                  CREW
                </button>

                <button
                  type="button"
                  onClick={() => goToSection("contact")}
                >
                  CONTACT
                </button>

              </div>
            )}

          </div>

          <div className="ethos-navbar__mobile-divider" />

          <button
            type="button"
            className="ethos-navbar__mobile-cta"
            onClick={() => {
              setMobileOpen(false);
              setLoginModalOpen(true);
            }}
          >
            LOGIN
          </button>

        </div>
      </div>

      <LoginComingSoonModal
        isOpen={loginModalOpen}
        onClose={() => setLoginModalOpen(false)}
      />
    </header>
  );
}

export default Navbar;
