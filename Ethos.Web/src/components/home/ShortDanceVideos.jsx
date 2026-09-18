import React, { useState, useEffect, useRef } from "react";
import { publicApi } from "../../services/publicApi";
import { getMediaUrl } from "../../utils/mediaUrl";
import visualReel from "../../assets/gallery/ethos-visual-reel.mp4";
import heroVideo from "../../assets/hero/hero-video.mp4";
import "./ShortDanceVideos.css";

const DEFAULT_SHORT_VIDEOS = [
  {
    id: "default-reel-1",
    title: "Contemporary Routine Reel",
    description: "Flow, control and musicality in our advanced routine session.",
    publicUrl: visualReel,
  },
  {
    id: "default-reel-2",
    title: "Urban Showcase Reel",
    description: "High energy routines, footwork and student combinations.",
    publicUrl: heroVideo,
  },
];

export default function ShortDanceVideos() {
  const [videos, setVideos] = useState(DEFAULT_SHORT_VIDEOS);
  const [loading, setLoading] = useState(false);
  const [activeModalVideo, setActiveModalVideo] = useState(null);
  const [mutedStates, setMutedStates] = useState({});

  useEffect(() => {
    let isMounted = true;
    publicApi
      .getPublicMedia({ section: "HomepageReels" })
      .then((data) => {
        if (isMounted && Array.isArray(data) && data.length > 0) {
          const formattedData = data.map((d) => ({
            ...d,
            publicUrl: getMediaUrl(d.publicUrl, d.id),
          }));
          // Prepend cloud reels to default studio reels
          setVideos([...formattedData, ...DEFAULT_SHORT_VIDEOS]);
        }
      })
      .catch((err) => {
        console.warn("[ShortDanceVideos] Unable to fetch HomepageReels:", err);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, []);

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
