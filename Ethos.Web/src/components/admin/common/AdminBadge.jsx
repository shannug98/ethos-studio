import React from "react";
import "./AdminBadge.css";

export default function AdminBadge({
  children,
  tone = "neutral", // success, warning, danger, info, neutral, simulated, executed
  dot = false,
  size = "md", // sm, md, lg
  className = "",
  title,
}) {
  // Map common status strings to canonical tones if passed directly
  let normalizedTone = tone.toLowerCase();
  if (["executed", "delivered", "sent", "healthy", "active", "captured", "resolved", "approved"].includes(normalizedTone)) {
    normalizedTone = "success";
  } else if (["simulated", "investigating", "monitoring", "queued", "pending", "identified"].includes(normalizedTone)) {
    normalizedTone = "warning";
  } else if (["failed", "danger", "critical", "high", "error", "unhealthy", "cancelled", "revoked"].includes(normalizedTone)) {
    normalizedTone = "danger";
  } else if (["info", "medium", "low", "trace"].includes(normalizedTone)) {
    normalizedTone = "info";
  }

  return (
    <span
      className={`admin-badge badge-${normalizedTone} badge-${size} ${className}`}
      title={title}
    >
      {dot && <span className="admin-badge-dot" aria-hidden="true" />}
      <span className="admin-badge-content">{children}</span>
    </span>
  );
}
