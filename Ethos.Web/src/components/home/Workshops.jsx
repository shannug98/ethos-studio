import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { createSlug } from "../../utils/createSlug";
import { workshopsApi } from "../../services/workshopsApi";
import "../../styles/workshops.css";
import "../../styles/workshop-card-meta.css";
import WorkshopCardMedia from "../workshop/WorkshopCardMedia";

import { handleMediaImgError, ETHOS_MEDIA_FALLBACK_SVG } from "../../utils/mediaUrl";

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
  if (Array.isArray(w.sessions) && w.sessions.length > 0) {
    const times = w.sessions
      .map((s) => {
        const dStr = s.sessionDate || s.date || w.workshopDate;
        if (!dStr) return null;
        const datePart = dStr.split("T")[0];
        const timePart = s.endTime || w.endTime || "23:59:59";
        const d = new Date(`${datePart}T${timePart}`);
        return isNaN(d.getTime()) ? null : d.getTime();
      })
      .filter((t) => t != null);
    if (times.length > 0) {
      return new Date(Math.max(...times));
    }
  }

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
  if (Array.isArray(w.sessions) && w.sessions.length > 0) {
    const times = w.sessions
      .map((s) => {
        const dStr = s.sessionDate || s.date || w.workshopDate;
        if (!dStr) return null;
        const datePart = dStr.split("T")[0];
        const timePart = s.startTime || w.startTime || "00:00:00";
        const d = new Date(`${datePart}T${timePart}`);
        return isNaN(d.getTime()) ? null : d.getTime();
      })
      .filter((t) => t != null);
    if (times.length > 0) {
      return new Date(Math.min(...times));
    }
  }

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
          const mapped = list.map((w) => {
            const d = new Date(w.workshopDate);
            const startDateTime = getWorkshopStartDateTime(w);
            const endDateTime = getWorkshopEndDateTime(w);
            let resolvedPrice = null;
            if (Array.isArray(w.passTypes) && w.passTypes.length > 0) {
              const validPassPrices = w.passTypes
                .map((p) => Number(p.currentPrice ?? p.price))
                .filter((pr) => !isNaN(pr) && pr > 0);
              if (validPassPrices.length > 0) {
                resolvedPrice = Math.min(...validPassPrices);
              }
            }
            if (resolvedPrice == null) {
              const rawPrice = Number(w.startingPrice ?? w.currentPrice ?? w.price);
              if (!isNaN(rawPrice) && rawPrice > 0) {
                resolvedPrice = rawPrice;
              }
            }

            return {
              id: w.id,
              image: w.imageUrl || ETHOS_MEDIA_FALLBACK_SVG,
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
              isEthosOriginal: w.isEthosOriginal === true,
              level: (w.level || "ALL LEVELS").toUpperCase(),
              venue: w.venue || "Ethos Dance Studio",
              price: resolvedPrice,
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
  // 1 immediate next workshop (big featured card) + next 3 in a fixed 3-slot row below.
  const displayPool = upcomingWorkshops.slice(0, 4);
  const featured = displayPool[0] || null;
  const upcomingList = displayPool.slice(1, 4);

  // FIXED 3-SLOT LAYOUT UNDERNEATH MAIN FEATURED WORKSHOP
  const smallSlots = [
    upcomingList[0] || null,
    upcomingList[1] || null,
    upcomingList[2] || null,
  ];

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
        <div className="workshops__container">
          {/* MAIN UPCOMING WORKSHOP (BIG FEATURED CARD) */}
          {featured && (
            <div
              className="workshops__featured workshops-reveal"
              id="workshops-list"
              onClick={() => navigate(`/workshops/${createSlug(featured.title || featured.name || featured.id)}`)}
              style={{ cursor: "pointer" }}
            >
              <WorkshopCardMedia
                image={featured.image}
                title={featured.title}
                isEthosOriginal={featured.isEthosOriginal}
                date={featured.date}
                time={featured.time}
                venue={featured.venue}
                level={featured.level}
                aspectRatio="16/9"
                showArrow={true}
                className="workshops__featured-media"
              />

              {/* Below Image: Dance Style + Workshop Name */}
              <div className="workshop-card-content-below workshops__featured-below">
                <span className="workshop-card-style-eyebrow">{featured.style}</span>
                <h3 className="workshop-card-heading workshops__featured-title">{featured.title}</h3>
              </div>
            </div>
          )}

          {/* THREE FIXED SMALL WORKSHOP SLOTS UNDERNEATH */}
          <div className="workshops__list">
            {smallSlots.map((workshop, index) => {
              if (workshop) {
                return (
                  <article
                    className="workshop-card workshops-reveal"
                    key={workshop.id}
                    style={{
                      "--workshop-delay": `${index * 120}ms`,
                      cursor: "pointer",
                    }}
                    onClick={() => navigate(`/workshops/${createSlug(workshop.title || workshop.name || workshop.id)}`)}
                  >
                    <WorkshopCardMedia
                      image={workshop.image}
                      title={workshop.title}
                      isEthosOriginal={workshop.isEthosOriginal}
                      date={workshop.date}
                      time={workshop.time}
                      venue={workshop.venue}
                      level={workshop.level}
                      aspectRatio="3/4"
                      showArrow={true}
                    />

                    {/* BELOW IMAGE: Dance Style + Workshop/Session Name */}
                    <div className="workshop-card-content-below">
                      <span className="workshop-card-style-eyebrow">{workshop.style}</span>
                      <h3 className="workshop-card-heading" title={workshop.title}>
                        {workshop.title}
                      </h3>
                    </div>
                  </article>
                );
              }

              return (
                <div
                  className="workshop-card workshop-card--empty workshops-reveal"
                  key={`empty-slot-${index}`}
                  style={{
                    "--workshop-delay": `${index * 120}ms`,
                  }}
                  aria-hidden="true"
                >
                  <div className="workshop-card-media workshop-card-media--empty">
                    <div className="workshop-empty-slot-content">
                      <div className="workshop-empty-slot-icon">
                        <span>✦</span>
                      </div>
                      <span className="workshop-empty-slot-label">COMING SOON</span>
                    </div>
                  </div>
                  <div className="workshop-card-content-below workshop-card-content-below--empty">
                    <span className="workshop-empty-slot-subtext">Stay tuned for new releases</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
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

