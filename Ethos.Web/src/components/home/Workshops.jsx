import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { createSlug } from "../../utils/createSlug";
import { workshopsApi } from "../../services/workshopsApi";
import "../../styles/workshops.css";

import workshop01 from "../../assets/workshops/workshop-01.jpg";
import workshop02 from "../../assets/workshops/workshop-02.jpg";
import workshop03 from "../../assets/workshops/workshop-03.jpg";
import workshop04 from "../../assets/workshops/workshop-04.jpg";

function formatWorkshopTime(startTime, endTime) {
  const formatSingle = (timeStr) => {
    if (!timeStr) return "";
    const parts = timeStr.split(":");
    if (parts.length < 2) return timeStr;
    let hours = parseInt(parts[0], 10);
    const minutes = parts[1];
    const ampm = hours >= 12 ? "PM" : "AM";
    hours = hours % 12 || 12;
    return `${hours}:${minutes} ${ampm}`;
  };

  if (!startTime) return "";
  const start = formatSingle(startTime);
  if (!endTime) return start;
  const end = formatSingle(endTime);
  return `${start} – ${end}`;
}

function getWorkshopEndDateTime(w) {
  if (w.endUtc) {
    const d = new Date(w.endUtc);
    if (!isNaN(d.getTime())) return d;
  }
  const datePart = w.workshopDate ? w.workshopDate.split("T")[0] : "";
  const timePart = w.endTime || "23:59:59";
  const d = new Date(`${datePart}T${timePart}`);
  if (!isNaN(d.getTime())) return d;
  return new Date(w.workshopDate || Date.now());
}

function getWorkshopStartDateTime(w) {
  if (w.startUtc) {
    const d = new Date(w.startUtc);
    if (!isNaN(d.getTime())) return d;
  }
  const datePart = w.workshopDate ? w.workshopDate.split("T")[0] : "";
  const timePart = w.startTime || "00:00:00";
  const d = new Date(`${datePart}T${timePart}`);
  if (!isNaN(d.getTime())) return d;
  return new Date(w.workshopDate || Date.now());
}

function Workshops() {
  const sectionRef = useRef(null);
  const navigate = useNavigate();
  const [liveWorkshops, setLiveWorkshops] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    workshopsApi
      .getApprovedWorkshops()
      .then((res) => {
        const list = Array.isArray(res) ? res : res?.items || [];
        if (list.length > 0) {
          const fallbackImgs = [workshop01, workshop02, workshop03, workshop04];
          const mapped = list.map((w, idx) => {
            const d = new Date(w.workshopDate);
            const startDateTime = getWorkshopStartDateTime(w);
            const endDateTime = getWorkshopEndDateTime(w);
            return {
              id: w.id,
              image: w.imageUrl || fallbackImgs[idx % fallbackImgs.length],
              date: d.toLocaleDateString("en-IN", {
                day: "numeric",
                month: "short",
                year: "numeric",
              }).toUpperCase(),
              time: formatWorkshopTime(w.startTime, w.endTime),
              startDateTime,
              endDateTime,
              style: (w.danceStyle || "WORKSHOP").toUpperCase(),
              title: w.title,
              trainerName: w.trainerName ? w.trainerName.trim() : "Ethos Faculty",
              trainer: w.trainerName ? `With ${w.trainerName.trim()}` : "With Ethos Faculty",
              trainerPhotoUrl: w.trainerPhotoUrl,
              trainerDanceStyles: w.trainerDanceStyles,
              level: (w.level || "ALL LEVELS").toUpperCase(),
              venue: w.venue || "Ethos Dance Studio",
              price: w.currentPrice || w.startingPrice || w.price || 599,
              description: w.description || "Join this transformative movement session with Ethos.",
            };
          });
          setLiveWorkshops(mapped);
        } else {
          setLiveWorkshops([]);
        }
      })
      .catch((err) => {
        console.warn("Homepage: Live workshops fetch failed:", err);
        setLiveWorkshops([]);
      })
      .finally(() => {
        setLoading(false);
      });
  }, []);

  const now = new Date();

  // STRICT REQUIREMENT: Only workshops whose end time is in the future are shown on homepage.
  // The moment a workshop ends, it automatically drops off the homepage.
  const upcomingWorkshops = liveWorkshops
    .filter((workshop) => workshop.endDateTime && workshop.endDateTime > now)
    .sort((a, b) => a.startDateTime - b.startDateTime);

  // STRICT REQUIREMENT: Only the top 4 upcoming workshops appear on homepage:
  // 1 immediate next workshop (big featured card) + next 3 in a single row below.
  const displayPool = upcomingWorkshops.slice(0, 4);
  const featured = displayPool[0] || null;
  const upcomingList = displayPool.slice(1, 4);

  useEffect(() => {
    const section = sectionRef.current;
    if (!section) return;

    const elements = section.querySelectorAll(".workshops-reveal");

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add("workshops-reveal--visible");
          }
        });
      },
      { threshold: 0.05 }
    );

    elements.forEach((element) => observer.observe(element));

    const timer = setTimeout(() => {
      elements.forEach((el) => {
        const rect = el.getBoundingClientRect();
        if (rect.top < window.innerHeight + 100) {
          el.classList.add("workshops-reveal--visible");
        }
      });
    }, 150);

    return () => {
      clearTimeout(timer);
      observer.disconnect();
    };
  }, [loading, displayPool.length]);

  return (
    <section id="workshops" className="workshops" ref={sectionRef}>
      {/* BACKGROUND */}
      <div className="workshops__background">
        <div className="workshops__glow workshops__glow--one" />
        <div className="workshops__glow workshops__glow--two" />
      </div>

      {/* INTRO */}
      <div className="workshops__intro workshops-reveal">
        <div className="workshops__intro-left">
          <span className="workshops__eyebrow">ETHOS / WORKSHOPS</span>

          <h2 className="workshops__heading">
            MOVE
            <br />
            <span>BEYOND</span>
            <br />
            ORDINARY.
          </h2>
        </div>

        <div className="workshops__intro-right">
          <p>
            Workshops designed to push your movement, challenge your perspective
            and introduce you to something new.
          </p>

          <button
            type="button"
            className="workshops__explore-link"
            onClick={() => navigate("/workshops")}
          >
            EXPLORE WORKSHOPS
            <span>↗</span>
          </button>
        </div>
      </div>

      {/* WORKSHOPS CONTENT / EMPTY STATE */}
      {loading ? (
        <div style={{ textAlign: "center", padding: "60px 20px", color: "#a1a1aa" }}>
          <p>Loading upcoming workshops...</p>
        </div>
      ) : displayPool.length === 0 ? (
        <div style={{ textAlign: "center", padding: "60px 20px" }}>
          <h3 style={{ color: "#ffffff", fontSize: "20px", marginBottom: "8px" }}>
            No upcoming workshops scheduled at this moment.
          </h3>
          <p style={{ color: "#a1a1aa", fontSize: "14px" }}>
            Check out past workshops or explore upcoming season announcements!
          </p>
          <button
            type="button"
            className="workshops__explore-link"
            style={{ marginTop: "24px" }}
            onClick={() => navigate("/workshops")}
          >
            BROWSE ALL WORKSHOPS <span>↗</span>
          </button>
        </div>
      ) : (
        <>
          {/* MAIN UPCOMING WORKSHOP (BIG FEATURED CARD) */}
          {featured && (
            <div
              className="workshops__featured workshops-reveal"
              id="workshops-list"
              onClick={() => navigate(`/workshops/${createSlug(featured.title || featured.name || featured.id)}`)}
              style={{ cursor: "pointer" }}
            >
              <div className="workshops__featured-image">
                <img src={featured.image} alt={featured.title} />
                <div className="workshops__featured-overlay" />

                <div className="workshops__featured-content">
                  <span className="workshops__featured-style">{featured.style}</span>
                  <h3>{featured.title}</h3>

                  <div className="workshops__featured-meta">
                    <div className="hp-meta-pill hp-meta-date">
                      <span className="hp-meta-icon">📅</span>
                      <span className="hp-meta-text">{featured.date}</span>
                      {featured.time && <span className="hp-meta-sub">({featured.time})</span>}
                    </div>

                    <div className="hp-meta-pill hp-meta-trainer">
                      {featured.trainerPhotoUrl ? (
                        <img src={featured.trainerPhotoUrl} alt={featured.trainerName} className="hp-meta-avatar" />
                      ) : (
                        <span className="hp-meta-avatar-initial">{featured.trainerName.charAt(0)}</span>
                      )}
                      <span className="hp-meta-text">{featured.trainer}</span>
                    </div>

                    <div className="hp-meta-pill hp-meta-level">
                      <span>{featured.level}</span>
                      {featured.venue && <span className="hp-meta-sub">· {featured.venue}</span>}
                    </div>
                  </div>
                </div>

                <button
                  className="workshops__featured-arrow"
                  type="button"
                  aria-label="View workshop"
                  onClick={(e) => {
                    e.stopPropagation();
                    navigate(`/workshops/${createSlug(featured.title || featured.name || featured.id)}`);
                  }}
                >
                  ↗
                </button>
              </div>
            </div>
          )}

          {/* NEXT UPCOMING WORKSHOPS (MAX 3 IN ONE ROW) */}
          {upcomingList.length > 0 && (
            <div className="workshops__list">
              {upcomingList.map((workshop, index) => (
                <article
                  className="workshop-card workshops-reveal"
                  key={workshop.id}
                  style={{
                    "--workshop-delay": `${index * 120}ms`,
                    cursor: "pointer",
                  }}
                  onClick={() => navigate(`/workshops/${createSlug(workshop.title || workshop.name || workshop.id)}`)}
                >
                  <div className="workshop-card__image">
                    <img src={workshop.image} alt={workshop.title} />
                    <div className="workshop-card__overlay" />
                    <span className="workshop-card__arrow">↗</span>
                  </div>

                  <div className="workshop-card__content">
                    {/* Top Row: Prominent Date and Level Badge */}
                    <div className="workshop-card__top">
                      <div className="workshop-card__date-badge">
                        <span className="workshop-card__date-icon">📅</span>
                        <span className="workshop-card__date-text">{workshop.date}</span>
                      </div>
                      <span className="workshop-card__level-badge">{workshop.level}</span>
                    </div>

                    <span className="workshop-card__style">{workshop.style}</span>
                    <h3 className="workshop-card__title" title={workshop.title}>{workshop.title}</h3>

                    {workshop.time && (
                      <div className="workshop-card__time-row">
                        <span className="workshop-card__time-icon">🕒</span>
                        <span className="workshop-card__time-text">{workshop.time}</span>
                      </div>
                    )}

                    {/* Prominent Trainer Row with High Visibility Text & Avatar */}
                    <div className="homepage-workshop-trainer-row">
                      {workshop.trainerPhotoUrl ? (
                        <img src={workshop.trainerPhotoUrl} alt={workshop.trainer} className="hp-trainer-avatar" />
                      ) : (
                        <span className="hp-trainer-avatar-initial">{workshop.trainerName.charAt(0)}</span>
                      )}
                      <div className="hp-trainer-info">
                        <span className="hp-trainer-label">INSTRUCTOR</span>
                        <span className="hp-trainer-text">{workshop.trainer}</span>
                      </div>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </>
      )}

      {/* BOTTOM CTA */}
      <div className="workshops__bottom workshops-reveal">
        <div className="workshops__bottom-line" />

        <div className="workshops__bottom-content">
          <span>
            SOMETHING NEW IS ALWAYS
            <br />
            WAITING FOR YOU.
          </span>

          <button
            type="button"
            className="workshops__view-all-btn"
            onClick={() => navigate("/workshops")}
          >
            VIEW ALL WORKSHOPS
            <span>↗</span>
          </button>
        </div>
      </div>
    </section>
  );
}

export default Workshops;

