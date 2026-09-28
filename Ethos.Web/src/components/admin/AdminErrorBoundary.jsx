import React from "react";
import { getLastTraceId, clearAdminAuth } from "../../services/adminApi";
import { AlertTriangle, RefreshCw, LayoutDashboard, LogOut, ChevronDown, Copy, Check } from "lucide-react";
import "./AdminErrorBoundary.css";

export default class AdminErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null, copied: false };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    console.error("Admin Portal Error Boundary caught error:", error, errorInfo);
  }

  handleReload = () => {
    this.setState({ hasError: false, error: null });
    window.location.reload();
  };

  handleGoWorkshops = () => {
    window.location.href = "/admin_portal/workshops";
  };

  handleGoDashboard = () => {
    window.location.href = "/admin_portal/dashboard";
  };

  handleSignOut = () => {
    clearAdminAuth();
    window.location.href = "/admin_portal/login";
  };

  handleCopyTrace = (traceId) => {
    if (navigator.clipboard) {
      navigator.clipboard.writeText(traceId).then(() => {
        this.setState({ copied: true });
        setTimeout(() => this.setState({ copied: false }), 2000);
      });
    }
  };

  getErrorContext = (error) => {
    const msg = String(error?.message || error || "").toLowerCase();
    
    if (msg.includes("cropper") || msg.includes("activepreset") || msg.includes("canvas") || msg.includes("crop")) {
      return {
        category: "Image Editor Interruption",
        title: "We couldn't open the image editor",
        description: "The image cropper encountered an unexpected problem while loading. Your workshop information has not been changed.",
        affectedArea: "Workshop → Photos / Image Upload",
        steps: [
          "Click Retry to re-initialize the component.",
          "If it happens again, refresh this page.",
          "Try opening the image upload dialog again with a standard JPEG, PNG, or WebP photo.",
        ],
      };
    }

    if (msg.includes("network") || msg.includes("fetch") || msg.includes("failed to fetch") || msg.includes("storage") || msg.includes("r2")) {
      return {
        category: "Service Connection Issue",
        title: "Communication with media or backend service failed",
        description: "A background connection to the studio API or cloud storage timed out. Your unsaved input remains protected.",
        affectedArea: "API & Media Storage Pipeline",
        steps: [
          "Check your internet connection.",
          "Click Retry View to re-establish the connection.",
          "Verify that System Health shows normal operational status.",
        ],
      };
    }

    return {
      category: "Administrative Operation Notice",
      title: "This view encountered an unexpected interruption",
      description: "A component failed to render cleanly. Your administrative session and existing records have not been altered.",
      affectedArea: "Admin Portal Shell",
      steps: [
        "Click Retry to reload this view.",
        "If the problem persists, refresh your browser.",
        "Return to the Workshops overview or Command Center to continue.",
      ],
    };
  };

  render() {
    if (this.state.hasError) {
      const traceId = getLastTraceId() || `trc_${Math.random().toString(36).substring(2, 10)}`;
      const context = this.getErrorContext(this.state.error);

      return (
        <div className="admin-error-boundary">
          <div className="admin-error-card">
            {/* BADGE & HEADER */}
            <div className="admin-error-top">
              <span className="admin-error-badge">
                <AlertTriangle size={13} />
                {context.category.toUpperCase()}
              </span>
              <h2 className="admin-error-title">{context.title}</h2>
              <p className="admin-error-description">{context.description}</p>
            </div>

            {/* WHAT YOU CAN DO */}
            <div className="admin-error-section">
              <span className="admin-error-section-title">WHAT YOU CAN DO</span>
              <div className="admin-error-steps-card">
                <ol className="admin-error-steps-list">
                  {context.steps.map((step, idx) => (
                    <li key={idx}>{step}</li>
                  ))}
                </ol>
              </div>
            </div>

            {/* AFFECTED AREA & TRACE */}
            <div className="admin-error-meta-row">
              <div className="admin-error-meta-block">
                <span className="admin-error-meta-label">AFFECTED AREA</span>
                <span className="admin-error-meta-value">{context.affectedArea}</span>
              </div>

              {traceId && (
                <div className="admin-error-meta-block">
                  <span className="admin-error-meta-label">CORRELATION REFERENCE</span>
                  <div className="admin-error-trace-pill">
                    <span className="admin-error-trace-id">{traceId}</span>
                    <button
                      type="button"
                      className="admin-error-copy-btn"
                      onClick={() => this.handleCopyTrace(traceId)}
                      title="Copy Reference ID"
                    >
                      {this.state.copied ? <Check size={12} color="#16a34a" /> : <Copy size={12} />}
                      <span>{this.state.copied ? "Copied" : "Copy"}</span>
                    </button>
                  </div>
                </div>
              )}
            </div>

            {/* EXPANDABLE TECHNICAL DETAILS */}
            <details className="admin-error-tech-accordion">
              <summary className="admin-error-tech-summary">
                <span>Technical details (for developers)</span>
                <ChevronDown size={14} className="accordion-arrow" />
              </summary>
              <div className="admin-error-tech-content">
                <p className="tech-error-msg">
                  {this.state.error?.name || "Error"}: {this.state.error?.message || String(this.state.error)}
                </p>
                {this.state.error?.stack && (
                  <pre className="tech-stack-trace">
                    {this.state.error.stack.split("\n").slice(0, 6).join("\n")}
                  </pre>
                )}
              </div>
            </details>

            {/* ACTION BUTTONS */}
            <div className="admin-error-actions">
              <button
                type="button"
                className="admin-error-btn primary-btn"
                onClick={this.handleReload}
              >
                <RefreshCw size={14} />
                <span>Retry View</span>
              </button>
              <button
                type="button"
                className="admin-error-btn secondary-btn"
                onClick={this.handleGoWorkshops}
              >
                Return to Workshops
              </button>
              <button
                type="button"
                className="admin-error-btn ghost-btn"
                onClick={this.handleGoDashboard}
              >
                <LayoutDashboard size={14} />
                <span>Dashboard</span>
              </button>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
