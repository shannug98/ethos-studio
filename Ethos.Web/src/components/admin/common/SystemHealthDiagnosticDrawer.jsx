import React, { useEffect } from "react";
import "./SystemHealthDiagnosticDrawer.css";

export default function SystemHealthDiagnosticDrawer({
  isOpen,
  onClose,
  component,
  onNavigate,
}) {
  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen || !component) return null;

  const getStatusBadgeClass = (status) => {
    switch (status?.toLowerCase()) {
      case "operational":
        return "status-pill-operational";
      case "degraded":
        return "status-pill-degraded";
      case "error":
        return "status-pill-error";
      case "standby":
        return "status-pill-standby";
      case "not configured":
      default:
        return "status-pill-not-configured";
    }
  };

  const getStatusIcon = (status) => {
    switch (status?.toLowerCase()) {
      case "operational":
        return "🟢";
      case "degraded":
        return "🟡";
      case "error":
        return "🔴";
      case "standby":
        return "🔵";
      case "not configured":
      default:
        return "⚪";
    }
  };

  const formatTimestamp = (iso) => {
    if (!iso) return "None recorded";
    try {
      const d = new Date(iso);
      return d.toLocaleString("en-IN", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      });
    } catch {
      return String(iso);
    }
  };

  return (
    <div className="health-drawer-backdrop" onClick={onClose}>
      <div
        className="health-drawer-panel"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="drawer-component-title"
      >
        {/* Header */}
        <div className="health-drawer-header">
          <div className="health-drawer-title-group">
            <span className="health-drawer-category">{component.category || "Subsystem"}</span>
            <h2 id="drawer-component-title" className="health-drawer-title">
              {component.name}
            </h2>
          </div>
          <button
            type="button"
            className="health-drawer-close-btn"
            onClick={onClose}
            aria-label="Close diagnostic drawer"
          >
            ✕
          </button>
        </div>

        {/* Body Content */}
        <div className="health-drawer-body">
          {/* Status Bar */}
          <div className="health-status-banner">
            <div className="health-status-row">
              <span className="health-status-indicator">
                {getStatusIcon(component.status)}
              </span>
              <span className={`health-status-pill ${getStatusBadgeClass(component.status)}`}>
                {component.status}
              </span>
              {component.latencyMs > 0 && (
                <span className="health-latency-chip">
                  ⚡ {component.latencyMs} ms
                </span>
              )}
            </div>
            {component.errorMessage && (
              <div className="health-error-callout">
                <strong>Reported Issue:</strong>
                <div>{component.errorMessage}</div>
                {component.traceId && (
                  <div className="health-trace-id-row">
                    <span>Trace ID:</span>
                    <code>{component.traceId}</code>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Timestamps */}
          <div className="health-section">
            <h4 className="health-section-heading">Check Timestamps</h4>
            <div className="health-meta-grid">
              <div className="health-meta-item">
                <span className="health-meta-label">Last Checked:</span>
                <span className="health-meta-val">{formatTimestamp(component.lastCheckedUtc)}</span>
              </div>
              <div className="health-meta-item">
                <span className="health-meta-label">Last Successful Check:</span>
                <span className="health-meta-val">{formatTimestamp(component.lastSuccessfulCheckUtc)}</span>
              </div>
            </div>
          </div>

          {/* Affected Systems */}
          {component.affectedSystems && component.affectedSystems.length > 0 && (
            <div className="health-section">
              <h4 className="health-section-heading">Affected Studio Systems</h4>
              <ul className="health-affected-list">
                {component.affectedSystems.map((sys, idx) => (
                  <li key={idx}>• {sys}</li>
                ))}
              </ul>
            </div>
          )}

          {/* Safe Diagnostics */}
          {component.diagnostics && Object.keys(component.diagnostics).length > 0 && (
            <div className="health-section">
              <h4 className="health-section-heading">Live Diagnostics & Configuration</h4>
              <div className="health-diag-table">
                {Object.entries(component.diagnostics).map(([k, v]) => (
                  <div key={k} className="health-diag-row">
                    <span className="health-diag-key">{k}</span>
                    <span className="health-diag-value">{String(v)}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="health-drawer-footer">
          <button
            type="button"
            className="admin-btn secondary"
            onClick={onClose}
          >
            Close
          </button>
          {component.actionUrl && (
            <button
              type="button"
              className="admin-btn primary"
              onClick={() => {
                onClose();
                if (onNavigate) {
                  onNavigate(component.actionUrl);
                }
              }}
            >
              {component.actionLabel || "Inspect Component →"}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
