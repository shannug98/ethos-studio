import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { createSlug } from "../../utils/createSlug";
import { workshopsApi } from "../../services/workshopsApi";
import "../../styles/workshops.css";

import workshop01 from "../../assets/workshops/workshop-01.jpg";
import workshop02 from "../../assets/workshops/workshop-02.jpg";
import workshop03 from "../../assets/workshops/workshop-03.jpg";
import workshop04 from "../../assets/workshops/workshop-04.jpg";

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
            return {
              id: w.id,
              image: w.imageUrl || fallbackImgs[idx % fallbackImgs.length],
              date: d.toLocaleDateString("en-IN", {
                day: "numeric",
                month: "short",
                year: "numeric",
              }).toUpperCase(),
              rawDate: w.workshopDate?.split("T")[0] || "2026-09-14",
              style: (w.danceStyle || "WORKSHOP").toUpperCase(),
              title: w.title,
              trainer: w.trainerName ? `With ${w.trainerName}` : "With Ethos Faculty",
              level: (w.level || "ALL LEVELS").toUpperCase(),
              price: w.startingPrice || w.price || 599,
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

  const today = new Date();
  today.setHours(0, 0, 0, 0);

  const activePool = liveWorkshops;

  const sortedWorkshops = [...activePool]
    .filter((workshop) => {
      const workshopDate = new Date(`${workshop.rawDate}T00:00:00`);
      return workshopDate >= today;
    })
    .sort((a, b) => {
      const dateA = new Date(`${a.rawDate}T00:00:00`);
      const dateB = new Date(`${b.rawDate}T00:00:00`);
      return dateA - dateB;
    })
    .slice(0, 4);

  const featured = sortedWorkshops[0];
  const upcomingList = sortedWorkshops.slice(1);

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
      { threshold: 0.15 }
    );

    elements.forEach((element) => observer.observe(element));

    return () => observer.disconnect();
  }, []);

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
          <p>Loading workshops...</p>
        </div>
      ) : sortedWorkshops.length === 0 ? (
        <div style={{ textAlign: "center", padding: "60px 20px" }}>
          <h3 style={{ color: "#ffffff", fontSize: "20px", marginBottom: "8px" }}>
            No workshops are currently available.
          </h3>
          <p style={{ color: "#a1a1aa", fontSize: "14px" }}>
            Please check back soon.
          </p>
        </div>
      ) : (
        <>
          {/* FEATURED WORKSHOP */}
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
                    <span>{featured.date}</span>
                    <span>{featured.level}</span>
                    <span>{featured.trainer}</span>
                  </div>
                </div>

                <button
                  className="workshops__featured-arrow"
                  type="button"
                  aria-label="View workshops"
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

          {/* UPCOMING WORKSHOPS */}
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
                  <div className="workshop-card__top">
                    <span>{workshop.date}</span>
                    <span>{workshop.level}</span>
                  </div>

                  <span className="workshop-card__style">{workshop.style}</span>
                  <h3>{workshop.title}</h3>
                  <p>{workshop.trainer}</p>
                </div>
              </article>
            ))}
          </div>
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
