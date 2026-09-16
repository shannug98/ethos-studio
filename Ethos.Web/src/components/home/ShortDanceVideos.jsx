import React, { useState, useEffect, useRef } from "react";
import { adminApi } from "../../services/adminApi";
import "./ShortDanceVideos.css";

export default function ShortDanceVideos() {
  const [videos, setVideos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeModalVideo, setActiveModalVideo] = useState(null);
  const [mutedStates, setMutedStates] = useState({});

  useEffect(() => {
    let isMounted = true;
    adminApi
      .getPublicVideos("ShortVideos")
      .then((data) => {
        if (isMounted && Array.isArray(data)) {
          setVideos(data);
        }
      })
      .catch((err) => {
        console.warn("[ShortDanceVideos] Unable to fetch short videos:", err);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  // If loading or no videos yet, return null (seamless when empty)
  if (loading || videos.length === 0) {
    return null;
  }

  const toggleMute = (id, e) => {
    e.stopPropagation();
    setMutedStates((prev) => ({
      ...prev,
      [id]: !prev[id],
    }));
  };

  return (
    <section className="ethos-short-videos-section">
      <div className="short-videos-header">
        <div className="section-eyebrow-tag">QUICK CLIPS & REELS</div>
        <h2 className="section-main-heading">Short Dance Videos</h2>
        <p className="section-subtext">
          Catch the latest quick routines, freestyle jams, and student training reels in action.
        </p>
      </div>

      <div className="short-videos-scroll-container">
        <div className="short-videos-track">
          {videos.map((video) => {
            const isMuted = mutedStates[video.id] !== false; // Default muted for auto-play

            return (
              <div
                key={video.id}
                className="short-video-reel-card"
                onClick={() => setActiveModalVideo(video)}
              >
                <div className="reel-video-wrapper">
                  <video
                    src={video.publicUrl}
                    className="reel-video-element"
                    muted={isMuted}
                    loop
                    playsInline
                    autoPlay
                    preload="metadata"
                  />

                  {/* Gradient Overlay & Controls */}
                  <div className="reel-overlay-scrim"></div>

                  <button
                    type="button"
                    className="reel-sound-toggle-btn"
                    onClick={(e) => toggleMute(video.id, e)}
                    title={isMuted ? "Unmute audio" : "Mute audio"}
                  >
                    {isMuted ? "🔇" : "🔊"}
                  </button>

                  <div className="reel-meta-overlay">
                    <span className="reel-badge">DANCE REEL</span>
                    <h3 className="reel-title">{video.title}</h3>
                    {video.description && (
                      <p className="reel-desc">{video.description}</p>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Fullscreen Video Modal */}
      {activeModalVideo && (
        <div className="reel-modal-backdrop" onClick={() => setActiveModalVideo(null)}>
          <div className="reel-modal-content" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="reel-modal-close"
              onClick={() => setActiveModalVideo(null)}
            >
              ✕
            </button>
            <div className="reel-modal-player-wrap">
              <video
                src={activeModalVideo.publicUrl}
                controls
                autoPlay
                className="reel-modal-player"
              />
            </div>
            <div className="reel-modal-info">
              <h3>{activeModalVideo.title}</h3>
              {activeModalVideo.description && <p>{activeModalVideo.description}</p>}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
