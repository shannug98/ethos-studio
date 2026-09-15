import React from "react";
import { getLastTraceId, clearAdminAuth } from "../../services/adminApi";
import "./AdminErrorBoundary.css";

export default class AdminErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    console.error("Admin Portal Error Boundary caught error:", error, errorInfo);
  }

  handleReload = () => {
    window.location.reload();
  };

  handleGoDashboard = () => {
    window.location.href = "/admin_portal/dashboard";
  };

  handleSignOut = () => {
    clearAdminAuth();
    window.location.href = "/admin_portal/login";
  };

  render() {
    if (this.state.hasError) {
      const traceId = getLastTraceId();
      return (
        <div className="admin-error-boundary">
          <div className="admin-error-card">
            <div className="admin-error-badge">SYSTEM EXCEPTION</div>
            <h2 className="admin-error-title">Administrative Operation Error</h2>
            <p className="admin-error-message">
              {this.state.error?.message || "An unexpected error occurred in the administrative shell."}
            </p>

            {traceId && (
              <div className="admin-error-trace">
                <span className="trace-caption">CORRELATION TRACE ID:</span>
                <span className="trace-key">{traceId}</span>
              </div>
            )}

            <div className="admin-error-actions">
              <button
                type="button"
                className="btn-primary"
                onClick={this.handleGoDashboard}
              >
                Return to Command Center
              </button>
              <button
                type="button"
                className="btn-secondary"
                onClick={this.handleReload}
              >
                Retry View
              </button>
              <button
                type="button"
                className="btn-danger"
                onClick={this.handleSignOut}
              >
                Re-Authenticate
              </button>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
