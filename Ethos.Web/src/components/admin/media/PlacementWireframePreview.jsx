import React from "react";

/**
 * PlacementWireframePreview
 * 
 * Visual demo/representation of the public website section where the uploaded media appears.
 * Uses clean SVGs with Ethos brand accents (gold #e0a96d and deep charcoal #121216).
 */
export default function PlacementWireframePreview({ wireframeType, title }) {
  switch (wireframeType) {
    case "hero_banner":
      return (
        <div className="wireframe-preview-container">
          <div className="wireframe-header">
            <span className="wireframe-dot red" />
            <span className="wireframe-dot yellow" />
            <span className="wireframe-dot green" />
            <span className="wireframe-title">Homepage — Hero Section</span>
          </div>
          <svg viewBox="0 0 480 180" className="wireframe-svg" fill="none" xmlns="http://www.w3.org/2000/svg">
            {/* Background page */}
            <rect width="480" height="180" fill="#0d0e12" rx="4" />
            {/* Mini Navbar */}
            <rect x="16" y="8" width="448" height="14" rx="2" fill="#1b1c24" />
            <circle cx="28" cy="15" r="4" fill="#e0a96d" />
            <rect x="40" y="13" width="36" height="4" rx="1" fill="#444654" />
            <rect x="360" y="12" width="24" height="6" rx="1" fill="#444654" />
            <rect x="390" y="12" width="24" height="6" rx="1" fill="#444654" />
            <rect x="420" y="12" width="34" height="6" rx="2" fill="#e0a96d" fillOpacity="0.8" />

            {/* Glowing Hero Banner Container */}
            <rect x="16" y="28" width="448" height="110" rx="4" fill="#15161e" stroke="#e0a96d" strokeWidth="2" strokeDasharray="none" />
            {/* Soft inner glow */}
            <rect x="20" y="32" width="440" height="102" rx="3" fill="#e0a96d" fillOpacity="0.08" />
            
            {/* Banner headline & text mockup */}
            <rect x="36" y="52" width="160" height="12" rx="2" fill="#ffffff" fillOpacity="0.85" />
            <rect x="36" y="70" width="220" height="7" rx="2" fill="#9ca3af" fillOpacity="0.6" />
            <rect x="36" y="82" width="180" height="7" rx="2" fill="#9ca3af" fillOpacity="0.6" />
            {/* CTA buttons */}
            <rect x="36" y="100" width="60" height="16" rx="2" fill="#e0a96d" />
            <rect x="104" y="100" width="60" height="16" rx="2" stroke="#e0a96d" strokeWidth="1" />

            {/* Slide indicators */}
            <circle cx="220" cy="126" r="2.5" fill="#e0a96d" />
            <circle cx="230" cy="126" r="2" fill="#6b7280" />
            <circle cx="240" cy="126" r="2" fill="#6b7280" />
            <circle cx="250" cy="126" r="2" fill="#6b7280" />
            <circle cx="260" cy="126" r="2" fill="#6b7280" />
            <circle cx="270" cy="126" r="2" fill="#6b7280" />

            {/* Badge */}
            <rect x="330" y="36" width="124" height="18" rx="2" fill="#1e1b18" stroke="#e0a96d" strokeWidth="1" />
            <text x="392" y="49" fill="#e0a96d" fontSize="9" fontWeight="600" textAnchor="middle">16:9 FIXED SLOTS (01–06)</text>

            {/* Page content below hero */}
            <rect x="16" y="146" width="140" height="26" rx="3" fill="#1b1c24" />
            <rect x="164" y="146" width="140" height="26" rx="3" fill="#1b1c24" />
            <rect x="312" y="146" width="152" height="26" rx="3" fill="#1b1c24" />
          </svg>
        </div>
      );

    case "reels_carousel":
      return (
        <div className="wireframe-preview-container">
          <div className="wireframe-header">
            <span className="wireframe-dot red" />
            <span className="wireframe-dot yellow" />
            <span className="wireframe-dot green" />
            <span className="wireframe-title">Homepage — Vertical Dance Reels</span>
          </div>
          <svg viewBox="0 0 480 180" className="wireframe-svg" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect width="480" height="180" fill="#0d0e12" rx="4" />
            <rect x="36" y="14" width="120" height="10" rx="2" fill="#ffffff" fillOpacity="0.8" />
            <rect x="36" y="28" width="180" height="6" rx="1" fill="#9ca3af" fillOpacity="0.5" />

            {/* 5 vertical reels side by side (9:16 portrait) */}
            {[
              { x: 36, active: true },
              { x: 120, active: true },
              { x: 204, active: true },
              { x: 288, active: true },
              { x: 372, active: false },
            ].map((r, i) => (
              <g key={i}>
                <rect
                  x={r.x}
                  y="42"
                  width="72"
                  height="124"
                  rx="6"
                  fill={r.active ? "#1a1b24" : "#13141a"}
                  stroke={r.active ? "#e0a96d" : "#2e303d"}
                  strokeWidth={r.active ? 1.5 : 1}
                />
                {/* 9:16 Badge on first reel */}
                {i === 0 && (
                  <rect x={r.x + 8} y="50" width="34" height="12" rx="2" fill="#e0a96d" fillOpacity="0.2" />
                )}
                {/* Play icon */}
                <circle cx={r.x + 36} cy="100" r="12" fill="#000000" fillOpacity="0.5" stroke="#e0a96d" strokeWidth="1" />
                <path d={`M${r.x + 33} 94L${r.x + 42} 100L${r.x + 33} 106Z`} fill="#e0a96d" />
                {/* Reel title placeholder */}
                <rect x={r.x + 8} y="144" width="56" height="5" rx="1" fill="#ffffff" fillOpacity="0.7" />
                <rect x={r.x + 8} y="152" width="40" height="4" rx="1" fill="#9ca3af" fillOpacity="0.4" />
              </g>
            ))}
          </svg>
        </div>
      );

    case "about_trio":
      return (
        <div className="wireframe-preview-container">
          <div className="wireframe-header">
            <span className="wireframe-dot red" />
            <span className="wireframe-dot yellow" />
            <span className="wireframe-dot green" />
            <span className="wireframe-title">Homepage — We Are Ethos Trio Composition</span>
          </div>
          <svg viewBox="0 0 480 180" className="wireframe-svg" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect width="480" height="180" fill="#0d0e12" rx="4" />
            {/* Left side text */}
            <rect x="24" y="24" width="130" height="12" rx="2" fill="#ffffff" fillOpacity="0.8" />
            <rect x="24" y="42" width="160" height="6" rx="1" fill="#9ca3af" fillOpacity="0.6" />
            <rect x="24" y="52" width="140" height="6" rx="1" fill="#9ca3af" fillOpacity="0.6" />
            <rect x="24" y="62" width="150" height="6" rx="1" fill="#9ca3af" fillOpacity="0.6" />
            <rect x="24" y="80" width="80" height="16" rx="2" stroke="#e0a96d" strokeWidth="1" />

            {/* Slot 1: Main Studio (4:5 tall) */}
            <rect x="200" y="20" width="108" height="140" rx="4" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1.5" />
            <rect x="208" y="28" width="60" height="12" rx="2" fill="#e0a96d" fillOpacity="0.2" />
            <text x="212" y="37" fill="#e0a96d" fontSize="7" fontWeight="bold">SLOT 01 (4:5)</text>

            {/* Right stack: Slot 2 (16:9) & Slot 3 (1:1) */}
            <rect x="320" y="20" width="136" height="72" rx="4" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1.5" />
            <rect x="328" y="28" width="60" height="12" rx="2" fill="#e0a96d" fillOpacity="0.2" />
            <text x="332" y="37" fill="#e0a96d" fontSize="7" fontWeight="bold">SLOT 02 (16:9)</text>

            <rect x="320" y="100" width="70" height="60" rx="4" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1.5" />
            <rect x="326" y="106" width="52" height="12" rx="2" fill="#e0a96d" fillOpacity="0.2" />
            <text x="329" y="115" fill="#e0a96d" fontSize="7" fontWeight="bold">SLOT 03 (1:1)</text>

            <rect x="398" y="100" width="58" height="60" rx="4" fill="#161720" stroke="#2e303d" strokeWidth="1" />
            <circle cx="427" cy="130" r="14" fill="#e0a96d" fillOpacity="0.15" />
            <path d="M427 122V138M419 130H435" stroke="#e0a96d" strokeWidth="1.5" strokeLinecap="round" />
          </svg>
        </div>
      );

    case "founder_split":
      return (
        <div className="wireframe-preview-container">
          <div className="wireframe-header">
            <span className="wireframe-dot red" />
            <span className="wireframe-dot yellow" />
            <span className="wireframe-dot green" />
            <span className="wireframe-title">Homepage — Co-Founders Presentation</span>
          </div>
          <svg viewBox="0 0 480 180" className="wireframe-svg" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect width="480" height="180" fill="#0d0e12" rx="4" />
            {/* Left Co-Founders Portrait Slot */}
            <rect x="32" y="20" width="200" height="140" rx="4" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1.5" />
            <rect x="42" y="30" width="100" height="14" rx="2" fill="#e0a96d" fillOpacity="0.2" />
            <text x="46" y="41" fill="#e0a96d" fontSize="8" fontWeight="bold">FOUNDER SLOT 01 (16:9 / 4:3)</text>
            <circle cx="100" cy="85" r="20" fill="#303244" />
            <circle cx="150" cy="85" r="20" fill="#303244" />

            {/* Right split: Bios & Vision */}
            <rect x="250" y="28" width="110" height="12" rx="2" fill="#ffffff" fillOpacity="0.8" />
            <rect x="250" y="46" width="190" height="6" rx="1" fill="#9ca3af" fillOpacity="0.6" />
            <rect x="250" y="56" width="180" height="6" rx="1" fill="#9ca3af" fillOpacity="0.6" />
            <rect x="250" y="76" width="130" height="10" rx="2" fill="#e0a96d" fillOpacity="0.7" />
            <rect x="250" y="92" width="190" height="6" rx="1" fill="#9ca3af" fillOpacity="0.4" />
            <rect x="250" y="102" width="170" height="6" rx="1" fill="#9ca3af" fillOpacity="0.4" />
            {/* Social handles */}
            <circle cx="260" cy="132" r="8" fill="#1b1c24" stroke="#e0a96d" strokeWidth="1" />
            <circle cx="282" cy="132" r="8" fill="#1b1c24" stroke="#e0a96d" strokeWidth="1" />
          </svg>
        </div>
      );

    case "trainers_grid":
      return (
        <div className="wireframe-preview-container">
          <div className="wireframe-header">
            <span className="wireframe-dot red" />
            <span className="wireframe-dot yellow" />
            <span className="wireframe-dot green" />
            <span className="wireframe-title">Homepage — Master Faculty (4 Fixed Slots)</span>
          </div>
          <svg viewBox="0 0 480 180" className="wireframe-svg" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect width="480" height="180" fill="#0d0e12" rx="4" />
            {/* Section heading */}
            <rect x="180" y="12" width="120" height="10" rx="2" fill="#ffffff" fillOpacity="0.8" />
            
            {/* 4 Trainer Slots */}
            {[
              { x: 28, name: "Sujith Kumar", slot: "Slot 01" },
              { x: 138, name: "Tejaswini", slot: "Slot 02" },
              { x: 248, name: "Rahul Roy", slot: "Slot 03" },
              { x: 358, name: "Priya Sharma", slot: "Slot 04" },
            ].map((t, i) => (
              <g key={i}>
                <rect x={t.x} y="32" width="94" height="136" rx="4" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1.5" />
                <rect x={t.x + 6} y="38" width="46" height="12" rx="2" fill="#e0a96d" fillOpacity="0.2" />
                <text x={t.x + 9} y="47" fill="#e0a96d" fontSize="7" fontWeight="bold">{t.slot}</text>
                {/* Silhouette */}
                <circle cx={t.x + 47} cy="80" r="18" fill="#2d2f3d" />
                <path d={`M${t.x + 25} 118C${t.x + 25} 104 ${t.x + 35} 96 ${t.x + 47} 96C${t.x + 59} 96 ${t.x + 69} 104 ${t.x + 69} 118Z`} fill="#2d2f3d" />
                {/* Name */}
                <rect x={t.x + 10} y="130" width="74" height="6" rx="1" fill="#ffffff" fillOpacity="0.8" />
                <rect x={t.x + 18} y="140" width="58" height="4" rx="1" fill="#9ca3af" fillOpacity="0.5" />
              </g>
            ))}
          </svg>
        </div>
      );

    case "gallery_masonry":
      return (
        <div className="wireframe-preview-container">
          <div className="wireframe-header">
            <span className="wireframe-dot red" />
            <span className="wireframe-dot yellow" />
            <span className="wireframe-dot green" />
            <span className="wireframe-title">Gallery — Three-Column Vertical Presentation</span>
          </div>
          <svg viewBox="0 0 480 180" className="wireframe-svg" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect width="480" height="180" fill="#0d0e12" rx="4" />
            {/* Top filter tabs */}
            <rect x="120" y="10" width="36" height="12" rx="2" fill="#e0a96d" />
            <rect x="164" y="10" width="48" height="12" rx="2" fill="#1c1d27" />
            <rect x="220" y="10" width="44" height="12" rx="2" fill="#1c1d27" />
            <rect x="272" y="10" width="48" height="12" rx="2" fill="#1c1d27" />
            <rect x="328" y="10" width="40" height="12" rx="2" fill="#1c1d27" />

            {/* Column 1 */}
            <rect x="32" y="32" width="128" height="74" rx="3" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1" />
            <rect x="32" y="112" width="128" height="56" rx="3" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1" />

            {/* Column 2 */}
            <rect x="176" y="32" width="128" height="50" rx="3" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1" />
            <rect x="176" y="88" width="128" height="80" rx="3" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1" />

            {/* Column 3 */}
            <rect x="320" y="32" width="128" height="84" rx="3" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1" />
            <rect x="320" y="122" width="128" height="46" rx="3" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1" />
          </svg>
        </div>
      );

    case "gallery_video_player":
      return (
        <div className="wireframe-preview-container">
          <div className="wireframe-header">
            <span className="wireframe-dot red" />
            <span className="wireframe-dot yellow" />
            <span className="wireframe-dot green" />
            <span className="wireframe-title">Gallery — Large/Cinematic Video Section</span>
          </div>
          <svg viewBox="0 0 480 180" className="wireframe-svg" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect width="480" height="180" fill="#0d0e12" rx="4" />
            {/* Cinematic Theater Player (16:9) */}
            <rect x="32" y="16" width="310" height="148" rx="4" fill="#14151e" stroke="#e0a96d" strokeWidth="1.5" />
            <circle cx="187" cy="90" r="22" fill="#000000" fillOpacity="0.6" stroke="#e0a96d" strokeWidth="1.5" />
            <path d="M182 80L196 90L182 100Z" fill="#e0a96d" />
            <text x="187" y="130" fill="#e0a96d" fontSize="9" fontWeight="600" textAnchor="middle">CINEMATIC VIDEO (MAX 8)</text>

            {/* Video playlist sidebar */}
            <rect x="352" y="16" width="96" height="42" rx="3" fill="#1c1d27" stroke="#e0a96d" strokeWidth="1" />
            <rect x="352" y="64" width="96" height="42" rx="3" fill="#161720" stroke="#2e303d" strokeWidth="1" />
            <rect x="352" y="112" width="96" height="42" rx="3" fill="#161720" stroke="#2e303d" strokeWidth="1" />
          </svg>
        </div>
      );

    case "gallery_categorized":
      return (
        <div className="wireframe-preview-container">
          <div className="wireframe-header">
            <span className="wireframe-dot red" />
            <span className="wireframe-dot yellow" />
            <span className="wireframe-dot green" />
            <span className="wireframe-title">Gallery — All Photos by Category</span>
          </div>
          <svg viewBox="0 0 480 180" className="wireframe-svg" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect width="480" height="180" fill="#0d0e12" rx="4" />
            {/* Category pills */}
            <rect x="24" y="12" width="60" height="12" rx="2" fill="#e0a96d" />
            <rect x="90" y="12" width="60" height="12" rx="2" fill="#1c1d27" />
            <rect x="156" y="12" width="50" height="12" rx="2" fill="#1c1d27" />
            <rect x="212" y="12" width="70" height="12" rx="2" fill="#1c1d27" />
            <rect x="288" y="12" width="90" height="12" rx="2" fill="#1c1d27" />
            <rect x="384" y="12" width="50" height="12" rx="2" fill="#1c1d27" />

            {/* Grid items */}
            <rect x="24" y="32" width="98" height="66" rx="2" fill="#1c1d27" stroke="#e0a96d" strokeWidth="0.8" />
            <rect x="130" y="32" width="98" height="66" rx="2" fill="#1c1d27" stroke="#e0a96d" strokeWidth="0.8" />
            <rect x="236" y="32" width="98" height="66" rx="2" fill="#1c1d27" stroke="#e0a96d" strokeWidth="0.8" />
            <rect x="342" y="32" width="98" height="66" rx="2" fill="#1c1d27" stroke="#e0a96d" strokeWidth="0.8" />
            <rect x="24" y="104" width="98" height="64" rx="2" fill="#1c1d27" stroke="#e0a96d" strokeWidth="0.8" />
            <rect x="130" y="104" width="98" height="64" rx="2" fill="#1c1d27" stroke="#e0a96d" strokeWidth="0.8" />
            <rect x="236" y="104" width="98" height="64" rx="2" fill="#1c1d27" stroke="#e0a96d" strokeWidth="0.8" />
            <rect x="342" y="104" width="98" height="64" rx="2" fill="#1c1d27" stroke="#e0a96d" strokeWidth="0.8" />
          </svg>
        </div>
      );

    default:
      return null;
  }
}
