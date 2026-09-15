import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { Calendar, MapPin, Share2 } from "lucide-react";
import { workshopsApi } from "../services/workshopsApi";
import { createSlug } from "../utils/createSlug";
import "../styles/workshops-page.css";

import workshop01 from "../assets/workshops/workshop-01.jpg";
import workshop02 from "../assets/workshops/workshop-02.jpg";
import workshop03 from "../assets/workshops/workshop-03.jpg";
import workshop04 from "../assets/workshops/workshop-04.jpg";

const months = [
  "JAN", "FEB", "MAR", "APR", "MAY", "JUN",
  "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"
];

function WorkshopsPage() {
  const [selectedYear, setSelectedYear] = useState("2026");
  const [selectedMonth, setSelectedMonth] = useState("SEP");
  const [workshops, setWorkshops] = useState([]);
  const [loading, setLoading] = useState(true);
  const [toastMessage, setToastMessage] = useState("");
  const navigate = useNavigate();

  useEffect(() => {
    window.scrollTo({ top: 0, behavior: "smooth" });
    loadBackendWorkshops();
  }, []);

  async function loadBackendWorkshops() {
    try {
      setLoading(true);
      const apiList = await workshopsApi.getApprovedWorkshops();
      if (Array.isArray(apiList) && apiList.length > 0) {
        const fallbackImgs = [workshop01, workshop02, workshop03, workshop04];
        const mapped = apiList.map((w, idx) => {
          const d = new Date(w.workshopDate);
          const mStr = months[d.getMonth()] || "SEP";
          const yStr = d.getFullYear().toString();

          return {
            id: w.id,
            year: yStr,
            month: mStr,
            date: d.toLocaleDateString("en-IN", { day: "numeric", month: "short", year: "numeric" }).toUpperCase(),
            rawDate: w.workshopDate,
            time: w.startTime ? `${w.startTime.slice(0, 5)} - ${w.endTime ? w.endTime.slice(0, 5) : ""}` : "5:00 PM",
            title: w.title,
            style: w.danceStyle || "WORKSHOP",
            level: w.level || "ALL LEVELS",
            trainer: w.trainerName || "Ethos Faculty",
            location: w.venue || "Ethos Dance Studio, Hyderabad",
            image: w.imageUrl || fallbackImgs[idx % fallbackImgs.length],
            startingPrice: w.startingPrice || w.price || 299,
            description: w.description || "Join this transformative movement session with Ethos.",
          };
        });

        setWorkshops(mapped);
      } else {
        setWorkshops([]);
      }
    } catch (err) {
      console.warn("Backend workshops fetch failed:", err);
      setWorkshops([]);
    } finally {
      setLoading(false);
    }
  }

  const handleShare = async (e, ws) => {
    e.stopPropagation();
    const url = `${window.location.origin}/workshops/${createSlug(ws.title || ws.workshopName || ws.id)}`;
    if (navigator.share) {
      try {
        await navigator.share({ title: ws.title, url });
      } catch (err) {}
    } else {
      navigator.clipboard.writeText(url);
      setToastMessage("Link copied to clipboard!");
      setTimeout(() => setToastMessage(""), 2500);
    }
  };

  const filteredWorkshops = workshops.filter(
    (item) => item.year === selectedYear && item.month === selectedMonth
  );

  return (
    <div className="workshops-page">
      {/* HERO SECTION */}
      <section className="workshops-page__hero">
        <div className="workshops-page__hero-content">
          <span className="workshops-page__eyebrow">ETHOS / WORKSHOPS</span>

          <h1 className="workshops-page__title">
            Trending
            <br />
            <span>Now.</span>
          </h1>

          <p className="workshops-page__subtitle">
            The events and masterclasses everyone in your city is booking right now.
          </p>
        </div>
      </section>

      {/* CALENDAR BAR */}
      <section className="workshops-page__calendar">
        <div className="workshops-page__calendar-inner">
          <div className="workshops-page__year-bar">
            <span className="workshops-page__year-label">SELECT YEAR:</span>
            <button
              type="button"
              className="workshops-page__year-btn is-active"
              onClick={() => setSelectedYear("2026")}
            >
              2026 (ACTIVE CALENDAR)
            </button>
          </div>

          <div className="workshops-page__months-bar">
            {months.map((m) => {
              const hasEvents = workshops.some(
                (w) => w.year === selectedYear && w.month === m
              );
              const isActive = selectedMonth === m;

              return (
                <button
                  key={m}
                  type="button"
                  className={`workshops-page__month-btn ${isActive ? "is-active" : ""} ${hasEvents ? "has-events" : ""}`}
                  onClick={() => setSelectedMonth(m)}
                >
                  <span className="month-name">{m}</span>
                  <span className="month-dot" />
                </button>
              );
            })}
          </div>
        </div>
      </section>

      {/* WORKSHOPS GRID (IMAGE 1) */}
      <section className="workshops-page__listing">
        <div className="workshops-page__listing-header">
          <h2>
            {selectedMonth} {selectedYear} EVENTS
          </h2>
          <span className="workshops-page__count">
            {filteredWorkshops.length} {filteredWorkshops.length === 1 ? "EXPERIENCE AVAILABLE" : "EXPERIENCES AVAILABLE"}
          </span>
        </div>

        {loading ? (
          <div className="workshops-page__empty">
            <p>Loading workshops...</p>
          </div>
        ) : filteredWorkshops.length > 0 ? (
          <div className="modern-events-grid">
            {filteredWorkshops.map((ws) => (
              <article 
                key={ws.id} 
                className="event-card-modern"
                onClick={() => navigate(`/workshops/${createSlug(ws.title || ws.workshopName || ws.id)}`)}
              >
                {/* IMAGE */}
                <div className="event-card-media">
                  <img src={ws.image} alt={ws.title} className="event-card-img" />
                  <span className="event-badge-og">★ OG</span>
                </div>

                {/* CONTENT */}
                <div className="event-card-body">
                  <h3 className="event-card-title">{ws.title}</h3>

                  {/* DATE & TIME CHIP */}
                  <div className="event-info-pill">
                    <Calendar size={16} className="pill-icon" />
                    <div className="pill-text">
                      <strong>{ws.date.slice(0, 6)}</strong>
                      <span>{ws.time.split("-")[0].trim()}</span>
                    </div>
                  </div>

                  {/* VENUE CHIP */}
                  <div className="event-info-pill">
                    <MapPin size={16} className="pill-icon" />
                    <div className="pill-text">
                      <strong>{ws.location.split(",")[0]}</strong>
                      <span className="truncate-venue">{ws.location}</span>
                    </div>
                  </div>

                  {/* FOOTER */}
                  <div className="event-card-actions">
                    <div className="event-price-col">
                      <span className="starting-from-txt">Starting from</span>
                      <span className="price-bold">₹{ws.startingPrice}</span>
                    </div>

                    <div className="card-btn-group">
                      <button
                        type="button"
                        className="btn-book-now-card"
                        onClick={(e) => {
                          e.stopPropagation();
                          navigate(`/workshops/${createSlug(ws.title || ws.workshopName || ws.id)}`);
                        }}
                      >
                        Book Now
                      </button>

                      <button
                        type="button"
                        className="btn-share-card"
                        onClick={(e) => handleShare(e, ws)}
                        aria-label="Share Event"
                      >
                        <Share2 size={16} />
                      </button>
                    </div>
                  </div>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <div className="workshops-page__empty">
            <h3>No workshops are currently available.</h3>
            <p>Please check back soon.</p>
          </div>
        )}
      </section>

      {toastMessage && (
        <div className="copied-toast">
          ✓ {toastMessage}
        </div>
      )}
    </div>
  );
}

export default WorkshopsPage;
