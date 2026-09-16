import React from "react";
import "./AdminKpiCard.css";

export default function AdminKpiCard({
  title,
  label,
  value,
  subtitle,
  sublabel,
  icon,
  trend,
  trendDirection = "up",
  tone = "neutral", // neutral, brand, success, warning, danger, info
  onClick,
  className = "",
}) {
  const isClickable = typeof onClick === "function";
  const displayTitle = title || label || "";
  const displaySubtitle = subtitle || sublabel || "";

  return (
    <div
      className={`admin-kpi-card tone-${tone} ${isClickable ? "clickable" : ""} ${className}`}
      onClick={isClickable ? onClick : undefined}
      role={isClickable ? "button" : "region"}
      tabIndex={isClickable ? 0 : undefined}
      aria-label={`${displayTitle}: ${value}`}
      onKeyDown={
        isClickable
          ? (e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                onClick();
              }
            }
          : undefined
      }
    >
      <div className="admin-kpi-header">
        <span className="admin-kpi-title">{displayTitle}</span>
        {icon && <span className="admin-kpi-icon" aria-hidden="true">{icon}</span>}
      </div>

      <div className="admin-kpi-body">
        <span className="admin-kpi-value">{value}</span>
      </div>

      {(displaySubtitle || trend) && (
        <div className="admin-kpi-footer">
          {trend && (
            <span className={`admin-kpi-trend trend-${trendDirection}`}>
              {trendDirection === "up" ? "↑" : trendDirection === "down" ? "↓" : "•"} {trend}
            </span>
          )}
          {displaySubtitle && <span className="admin-kpi-subtitle">{displaySubtitle}</span>}
        </div>
      )}
    </div>
  );
}
