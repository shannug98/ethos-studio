import React from "react";
import "./AdminKpiCard.css";

export default function AdminKpiCard({
  title,
  value,
  subtitle,
  icon,
  trend,
  trendDirection = "up",
  tone = "neutral", // neutral, success, warning, danger, info
  onClick,
  className = "",
}) {
  const isClickable = typeof onClick === "function";

  return (
    <div
      className={`admin-kpi-card tone-${tone} ${isClickable ? "clickable" : ""} ${className}`}
      onClick={isClickable ? onClick : undefined}
      role={isClickable ? "button" : "region"}
      tabIndex={isClickable ? 0 : undefined}
      aria-label={`${title}: ${value}`}
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
        <span className="admin-kpi-title">{title}</span>
        {icon && <span className="admin-kpi-icon" aria-hidden="true">{icon}</span>}
      </div>

      <div className="admin-kpi-body">
        <span className="admin-kpi-value">{value}</span>
      </div>

      {(subtitle || trend) && (
        <div className="admin-kpi-footer">
          {trend && (
            <span className={`admin-kpi-trend trend-${trendDirection}`}>
              {trendDirection === "up" ? "↑" : trendDirection === "down" ? "↓" : "•"} {trend}
            </span>
          )}
          {subtitle && <span className="admin-kpi-subtitle">{subtitle}</span>}
        </div>
      )}
    </div>
  );
}
