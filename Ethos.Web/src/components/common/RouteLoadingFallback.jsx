import React from "react";
import "./RouteLoadingFallback.css";

export default function RouteLoadingFallback({ message = "Loading..." }) {
  return (
    <div className="route-loading-fallback" role="status" aria-live="polite">
      <div className="route-loading-spinner" />
      <span className="route-loading-text">{message}</span>
    </div>
  );
}
