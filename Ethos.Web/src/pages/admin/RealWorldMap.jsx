import React, { useMemo } from "react";
import {
  ComposableMap,
  Geographies,
  Geography,
  Marker,
  Graticule,
} from "react-simple-maps";
import { geoEqualEarth } from "d3-geo";
import countriesData from "world-atlas/countries-110m.json";

const projection = geoEqualEarth().scale(145).translate([400, 210]);

export function projectCoordinates(lon, lat) {
  const coords = projection([Number(lon) || 0, Number(lat) || 0]);
  if (!coords) return { x: 400, y: 210 };
  return { x: Math.round(coords[0] * 10) / 10, y: Math.round(coords[1] * 10) / 10 };
}

export function getLocationMarkerTier(count, percent) {
  const c = Number(count) || 0;
  const p = Number(percent) || 0;
  if (c >= 50 || p >= 40) return { tier: "highest", radius: 9, haloRadius: 18, strokeWidth: 2.2, label: "Highest (>50)" };
  if (c >= 21 || p >= 25) return { tier: "high", radius: 7, haloRadius: 14, strokeWidth: 1.8, label: "High (21–50)" };
  if (c >= 6 || p >= 10) return { tier: "med", radius: 5.2, haloRadius: 10, strokeWidth: 1.5, label: "Medium (6–20)" };
  return { tier: "low", radius: 3.8, haloRadius: 7, strokeWidth: 1.2, label: "Low (1–5)" };
}

const CONTINENT_MARKERS = [
  { text: "NORTH AMERICA", coordinates: [-100, 45] },
  { text: "SOUTH AMERICA", coordinates: [-60, -15] },
  { text: "EUROPE", coordinates: [15, 50] },
  { text: "AFRICA", coordinates: [20, 5] },
  { text: "ASIA", coordinates: [90, 45] },
  { text: "AUSTRALIA", coordinates: [135, -25] },
];

const OCEAN_MARKERS = [
  { text: "Pacific Ocean", coordinates: [-140, -10] },
  { text: "Atlantic Ocean", coordinates: [-30, 20] },
  { text: "Indian Ocean", coordinates: [75, -20] },
  { text: "Pacific Ocean", coordinates: [160, 0] },
];

export default function RealWorldMap({
  locations = [],
  hoveredLocation = null,
  onHoverLocation = () => {},
}) {
  // Aggregate visitors by country for country-level telemetry
  const { countryVisitorsMap, totalVisitors } = useMemo(() => {
    const map = {};
    let total = 0;
    locations.forEach((loc) => {
      const country = loc.country?.trim()?.toLowerCase();
      const count = Number(loc.visitorCount || loc.count || 0);
      total += count;
      if (country) {
        map[country] = (map[country] || 0) + count;
      }
    });
    return { countryVisitorsMap: map, totalVisitors: total };
  }, [locations]);

  // Compute screen coordinates for active hovered marker tooltip
  const tooltipCoords = useMemo(() => {
    if (!hoveredLocation) return null;
    if (hoveredLocation.x != null && hoveredLocation.y != null) {
      return { x: hoveredLocation.x, y: hoveredLocation.y };
    }
    const lon = Number(hoveredLocation.longitude) || 78.4867;
    const lat = Number(hoveredLocation.latitude) || 17.3850;
    return projectCoordinates(lon, lat);
  }, [hoveredLocation]);

  return (
    <div className="locations-map-canvas-container">
      <ComposableMap
        projection="geoEqualEarth"
        projectionConfig={{
          scale: 145,
          center: [0, 0],
        }}
        width={800}
        height={420}
        className="world-map-svg"
      >
        {/* Clean White Ocean Background */}
        <rect width={800} height={420} fill="#FFFFFF" className="map-ocean-bg" />

        {/* Graticules / Grid lines */}
        <Graticule
          stroke="rgba(220, 212, 202, 0.45)"
          strokeWidth={0.5}
          strokeDasharray="2 4"
        />

        {/* Authentic Natural Earth Country Boundaries */}
        <Geographies geography={countriesData}>
          {({ geographies }) =>
            geographies.map((geo) => {
              const countryName = geo.properties.name || "";
              const normalizedName = countryName.toLowerCase();
              const countryVisitors =
                countryVisitorsMap[normalizedName] ||
                (normalizedName === "united states of america" ? countryVisitorsMap["usa"] || countryVisitorsMap["united states"] : 0) ||
                (normalizedName === "united kingdom" ? countryVisitorsMap["uk"] : 0) ||
                (normalizedName === "united arab emirates" ? countryVisitorsMap["uae"] : 0) ||
                0;
              const hasVisitors = countryVisitors > 0;

              return (
                <Geography
                  key={geo.rsmKey}
                  geography={geo}
                  className={`rsm-geography ${hasVisitors ? "has-visitors" : ""}`}
                  fill="#FFFFFF"
                  stroke="#D6CEC3"
                  strokeWidth={0.75}
                  onMouseEnter={() => {
                    if (hasVisitors && !hoveredLocation) {
                      const share = totalVisitors > 0 ? ((countryVisitors / totalVisitors) * 100).toFixed(1) : 0;
                      onHoverLocation({
                        city: countryName,
                        country: "Aggregated Telemetry",
                        visitorCount: countryVisitors,
                        percentage: share,
                        latitude: geo.properties.label_y || 20,
                        longitude: geo.properties.label_x || 0,
                      });
                    }
                  }}
                  onMouseLeave={() => {
                    if (hoveredLocation?.country === "Aggregated Telemetry") {
                      onHoverLocation(null);
                    }
                  }}
                  style={{
                    default: {
                      fill: "#FFFFFF",
                      stroke: "#D6CEC3",
                      strokeWidth: 0.75,
                      outline: "none",
                    },
                    hover: {
                      fill: "#F5EFE6",
                      stroke: "#A89D8E",
                      strokeWidth: 0.85,
                      outline: "none",
                      cursor: hasVisitors ? "pointer" : "default",
                    },
                    pressed: {
                      fill: "#EBE3D5",
                      outline: "none",
                    },
                  }}
                />
              );
            })
          }
        </Geographies>

        {/* Continental Labels */}
        {CONTINENT_MARKERS.map((continent) => (
          <Marker key={continent.text} coordinates={continent.coordinates}>
            <text
              textAnchor="middle"
              className="map-continent-label"
              fill="#374151"
              y={2}
            >
              {continent.text}
            </text>
          </Marker>
        ))}

        {/* Ocean Labels */}
        {OCEAN_MARKERS.map((ocean, idx) => (
          <Marker key={`${ocean.text}-${idx}`} coordinates={ocean.coordinates}>
            <text
              textAnchor="middle"
              className="map-ocean-label"
              fill="#64748B"
              y={2}
            >
              {ocean.text}
            </text>
          </Marker>
        ))}

        {/* Real Telemetry Graduated Location Markers */}
        {locations.map((loc, idx) => {
          const lat = Number(loc.latitude) || 17.3850;
          const lon = Number(loc.longitude) || 78.4867;
          const count = Number(loc.visitorCount || loc.count || 0);
          const percent = Number(loc.percentage || loc.rawPercent || 0);
          const { tier, radius, haloRadius, strokeWidth } = getLocationMarkerTier(count, percent);
          const color = loc.color || (tier === "highest" ? "#FF5500" : tier === "high" ? "#EA580C" : tier === "med" ? "#FB923C" : "#FDE047");
          const isHovered = hoveredLocation?.city?.toLowerCase() === loc.city?.toLowerCase();
          const screenCoords = projectCoordinates(lon, lat);

          return (
            <Marker
              key={loc.city || idx}
              coordinates={[lon, lat]}
              onMouseEnter={() => onHoverLocation({ ...loc, x: screenCoords.x, y: screenCoords.y })}
              onMouseLeave={() => onHoverLocation(null)}
              onClick={() => onHoverLocation(isHovered ? null : { ...loc, x: screenCoords.x, y: screenCoords.y })}
              className="map-node-group"
              style={{
                default: { outline: "none", cursor: "pointer" },
                hover: { outline: "none", cursor: "pointer" },
                pressed: { outline: "none" },
              }}
              tabIndex={0}
              aria-label={`${loc.city}, ${loc.country}: ${count} visitors`}
            >
              {/* Radial Halo Circle */}
              <circle
                r={haloRadius}
                fill={color}
                fillOpacity={tier === "highest" ? 0.3 : 0.2}
                className={`map-halo-circle ${tier === "highest" ? "highest-pulse" : ""}`}
              />

              {/* Solid Radiant Core */}
              <circle
                r={isHovered ? radius + 2.5 : radius}
                fill={color}
                stroke="#FFFFFF"
                strokeWidth={strokeWidth}
                className={`map-node-core ${isHovered ? "is-hovered" : ""}`}
              />
            </Marker>
          );
        })}
      </ComposableMap>

      {/* Floating Obsidian Dark Tooltip Card */}
      {hoveredLocation && tooltipCoords && (
        <div
          className="map-floating-tooltip-obsidian"
          style={{
            left: `${(tooltipCoords.x / 800) * 100}%`,
            top: `${(tooltipCoords.y / 420) * 100}%`,
          }}
        >
          <div className="tooltip-obsidian-header">
            <span className="tooltip-obsidian-title">
              {hoveredLocation.city}
              {hoveredLocation.country && hoveredLocation.country !== "Aggregated Telemetry"
                ? `, ${hoveredLocation.country}`
                : ""}
            </span>
            <button
              type="button"
              className="tooltip-obsidian-close"
              onClick={() => onHoverLocation(null)}
              aria-label="Close tooltip"
            >
              ✕
            </button>
          </div>
          <div className="tooltip-obsidian-body">
            <div className="tooltip-metric-line">
              <span style={{ fontSize: "12px" }}>👤</span>
              <span>Visitors: <strong>{Number(hoveredLocation.visitorCount || hoveredLocation.count || 0).toLocaleString("en-IN")}</strong></span>
            </div>
            <div className="tooltip-metric-line">
              <span style={{ fontSize: "12px" }}>◔</span>
              <span>Share: <strong>{hoveredLocation.percentage || hoveredLocation.rawPercent || 0}%</strong></span>
            </div>
          </div>
          <div className="map-tooltip-notch" />
        </div>
      )}

      {/* Floating Legend Pill Box */}
      <div className="map-floating-legend-pill">
        <div className="legend-pill-item">
          <span className="legend-dot-sample low" />
          <span>Low (1–5)</span>
        </div>
        <div className="legend-pill-item">
          <span className="legend-dot-sample med" />
          <span>Medium (6–20)</span>
        </div>
        <div className="legend-pill-item">
          <span className="legend-dot-sample high" />
          <span>High (21–50)</span>
        </div>
        <div className="legend-pill-item">
          <span className="legend-dot-sample highest" />
          <span>Highest (&gt;50)</span>
        </div>
      </div>
    </div>
  );
}
