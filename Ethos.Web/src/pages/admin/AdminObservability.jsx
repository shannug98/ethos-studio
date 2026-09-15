import React, { useState, useEffect, useRef } from "react";
import { useSearchParams, useParams } from "react-router-dom";
import { adminApi } from "../../services/adminApi";
import "./AdminObservability.css";

export default function AdminObservability() {
  const [searchParams] = useSearchParams();
  const params = useParams();
  const [activeTab, setActiveTab] = useState("explorer"); // explorer, logs, performance, health
  const [metrics, setMetrics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Trace Explorer State
  const initialTraceId = params?.traceId || searchParams.get("traceId") || "";
  const [searchTraceId, setSearchTraceId] = useState(initialTraceId);
  const [traceData, setTraceData] = useState(null);
  const [traceLoading, setTraceLoading] = useState(false);
  const [traceError, setTraceError] = useState("");
  const autoLookupDoneRef = useRef(false);

  // Logs State
  const [logs, setLogs] = useState([]);
  const [logPage, setLogPage] = useState(1);
  const [totalLogs, setTotalLogs] = useState(0);
  const [logLoading, setLogLoading] = useState(false);
  const [filterStatus, setFilterStatus] = useState("");
  const [filterMethod, setFilterMethod] = useState("");
  const [filterPath, setFilterPath] = useState("");

  // Health State
  const [healthData, setHealthData] = useState(null);
  const [healthLoading, setHealthLoading] = useState(false);

  // Promote to Incident Modal
  const [showIncidentModal, setShowIncidentModal] = useState(false);
  const [incidentTitle, setIncidentTitle] = useState("");
  const [incidentSeverity, setIncidentSeverity] = useState("HIGH");
  const [incidentSubmitting, setIncidentSubmitting] = useState(false);
  const [incidentSuccess, setIncidentSuccess] = useState("");

  const fetchMetrics = async () => {
    setLoading(true);
    try {
      const res = await adminApi.getObservabilityMetrics();
      setMetrics(res);
      setError("");
    } catch (err) {
      setError(err.message || "Failed to load telemetry metrics.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchMetrics();
    if (initialTraceId && !autoLookupDoneRef.current) {
      autoLookupDoneRef.current = true;
      setActiveTab("explorer");
      executeTraceLookup(initialTraceId.trim());
    }
  }, [initialTraceId]);

  const executeTraceLookup = async (traceIdToLookup) => {
    const tid = (traceIdToLookup || searchTraceId || "").trim();
    if (!tid) return;

    setTraceLoading(true);
    setTraceError("");
    setTraceData(null);
    try {
      const res = await adminApi.getTraceDeepDive(tid);
      setTraceData(res);
    } catch (err) {
      setTraceError(err.message || "Trace ID not found in telemetry store.");
    } finally {
      setTraceLoading(false);
    }
  };

  const handleTraceLookup = async (e) => {
    if (e) e.preventDefault();
    executeTraceLookup(searchTraceId);
  };

  const fetchLogs = async () => {
    setLogLoading(true);
    try {
      const params = new URLSearchParams({
        page: logPage.toString(),
        pageSize: "25",
      });
      if (filterStatus) params.append("statusCode", filterStatus);
      if (filterMethod) params.append("method", filterMethod);
      if (filterPath.trim()) params.append("path", filterPath.trim());

      const res = await adminApi.getObservabilityLogs(params.toString());
      setLogs(res.items || []);
      setTotalLogs(res.totalCount || 0);
    } catch (err) {
      console.error(err);
    } finally {
      setLogLoading(false);
    }
  };

  useEffect(() => {
    if (activeTab === "logs") {
      fetchLogs();
    } else if (activeTab === "health") {
      fetchHealth();
    }
  }, [activeTab, logPage, filterStatus, filterMethod]);

  const fetchHealth = async () => {
    setHealthLoading(true);
    try {
      const res = await adminApi.getDeepHealthCheck();
      setHealthData(res);
    } catch (err) {
      console.error(err);
    } finally {
      setHealthLoading(false);
    }
  };

  const handleOpenPromote = () => {
    if (!traceData) return;
    const path = traceData.request?.path || "Unknown";
    const status = traceData.request?.statusCode || 500;
    setIncidentTitle(`[Trace ${traceData.traceId}] Failure on ${path} (${status})`);
    setIncidentSeverity(status >= 500 ? "HIGH" : "MEDIUM");
    setShowIncidentModal(true);
    setIncidentSuccess("");
  };

  const handlePromoteSubmit = async (e) => {
    e.preventDefault();
    setIncidentSubmitting(true);
    try {
      await adminApi.createIncidentFromTrace({
        traceId: traceData.traceId,
        title: incidentTitle,
        severity: incidentSeverity,
      });
      setIncidentSuccess("Incident created successfully from Trace ID!");
      setTimeout(() => {
        setShowIncidentModal(false);
      }, 1500);
    } catch (err) {
      alert(err.message || "Failed to promote trace to incident.");
    } finally {
      setIncidentSubmitting(false);
    }
  };

  return (
    <div className="admin-obs-page">
      <div className="obs-header">
        <div>
          <h2>Observability & Telemetry Engine</h2>
          <p className="obs-subtitle">Real-time HTTP requests telemetry, Trace ID deep-dive, and deep health monitoring</p>
        </div>
        <button className="btn-refresh" onClick={fetchMetrics} disabled={loading}>
          🔄 Refresh Telemetry
        </button>
      </div>

      {error && <div className="obs-alert error">{error}</div>}

      {/* KPI Cards */}
      {metrics && (
        <div className="obs-kpi-grid">
          <div className="obs-kpi-card">
            <span className="kpi-label">Total Requests (All Time)</span>
            <span className="kpi-value">{metrics.totalRequests.toLocaleString()}</span>
            <span className="kpi-sub">Persisted in ring buffer</span>
          </div>
          <div className="obs-kpi-card">
            <span className="kpi-label">Requests / Min (Last 1h)</span>
            <span className="kpi-value">{metrics.requestsPerMinute}</span>
            <span className="kpi-sub">Live ingress throughput</span>
          </div>
          <div className="obs-kpi-card">
            <span className="kpi-label">Avg / p95 Latency</span>
            <span className="kpi-value">{metrics.averageDurationMs}ms <span className="kpi-p95">/ {metrics.p95Ms}ms</span></span>
            <span className="kpi-sub">p99: {metrics.p99Ms}ms</span>
          </div>
          <div className={`obs-kpi-card ${metrics.errorRatePercentage > 5 ? "warning" : ""}`}>
            <span className="kpi-label">24h Error Rate</span>
            <span className="kpi-value">{metrics.errorRatePercentage}%</span>
            <span className="kpi-sub">4xx & 5xx HTTP responses</span>
          </div>
        </div>
      )}

      {/* Navigation Tabs */}
      <div className="obs-tabs">
        <button className={`obs-tab ${activeTab === "explorer" ? "active" : ""}`} onClick={() => setActiveTab("explorer")}>
          🔍 Trace Explorer
        </button>
        <button className={`obs-tab ${activeTab === "logs" ? "active" : ""}`} onClick={() => setActiveTab("logs")}>
          📋 Request Logs
        </button>
        <button className={`obs-tab ${activeTab === "performance" ? "active" : ""}`} onClick={() => setActiveTab("performance")}>
          ⚡ Endpoints Performance
        </button>
        <button className={`obs-tab ${activeTab === "health" ? "active" : ""}`} onClick={() => setActiveTab("health")}>
          🏥 Deep Health Checks
        </button>
      </div>

      {/* TAB 1: TRACE EXPLORER */}
      {activeTab === "explorer" && (
        <div className="tab-content">
          <div className="trace-search-bar">
            <form onSubmit={handleTraceLookup}>
              <input
                type="text"
                placeholder="Enter server Trace ID (e.g. trc_78a1bf2c89...)"
                value={searchTraceId}
                onChange={(e) => setSearchTraceId(e.target.value)}
                className="trace-input"
              />
              <button type="submit" className="btn-search" disabled={traceLoading}>
                {traceLoading ? "Investigating..." : "Trace Deep Dive"}
              </button>
            </form>
          </div>

          {traceError && <div className="obs-alert error">{traceError}</div>}

          {traceData && (
            <div className="trace-timeline-panel">
              <div className="timeline-header">
                <div>
                  <h3>Unified Timeline for Trace: <code>{traceData.traceId}</code></h3>
                  <span className="timeline-meta">Correlated across 5 system ledgers</span>
                </div>
                <button className="btn-promote" onClick={handleOpenPromote}>
                  🚨 Promote to Incident
                </button>
              </div>

              {/* Step 1: HTTP Request */}
              <div className="timeline-card">
                <div className="card-badge req">HTTP INGRESS</div>
                {traceData.request ? (
                  <div className="trace-details-grid">
                    <div><strong>Method:</strong> <span className={`method-pill ${traceData.request.method}`}>{traceData.request.method}</span></div>
                    <div><strong>Path:</strong> <code>{traceData.request.path}</code></div>
                    <div><strong>Status:</strong> <span className={`status-pill s${traceData.request.statusCode}`}>{traceData.request.statusCode}</span></div>
                    <div><strong>Duration:</strong> {traceData.request.durationMs}ms</div>
                    <div><strong>IP:</strong> {traceData.request.ipAddress}</div>
                    <div><strong>Timestamp:</strong> {new Date(traceData.request.createdAt).toLocaleString()}</div>
                    {traceData.request.errorMessage && (
                      <div className="full-width error-text"><strong>Error Note:</strong> {traceData.request.errorMessage}</div>
                    )}
                  </div>
                ) : (
                  <p className="empty-text">No HTTP telemetry record captured for this trace ID.</p>
                )}
              </div>

              {/* Step 2: Unhandled Exceptions */}
              {traceData.exceptionDetails && (
                <div className="timeline-card alert-danger">
                  <div className="card-badge exc">UNHANDLED SERVER EXCEPTION</div>
                  <div><strong>Exception Type:</strong> {traceData.exceptionDetails.type}</div>
                  <div><strong>Message:</strong> <code>{traceData.exceptionDetails.message}</code></div>
                  <div><strong>Time:</strong> {new Date(traceData.exceptionDetails.timestamp).toLocaleString()}</div>
                </div>
              )}

              {/* Step 3: Admin Actions Recorded */}
              <div className="timeline-card">
                <div className="card-badge act">ADMIN AUDIT MUTATIONS ({traceData.adminActions.length})</div>
                {traceData.adminActions.length > 0 ? (
                  <ul className="sub-timeline-list">
                    {traceData.adminActions.map((act) => (
                      <li key={act.id}>
                        <span className="sub-time">{new Date(act.createdAt).toLocaleTimeString()}</span>
                        <strong>{act.actionType}</strong> by <em>{act.adminName}</em> ({act.category})
                        <div className="sub-reason">Reason: {act.reason || "None recorded"}</div>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="empty-text">Zero administrative mutations executed during this trace.</p>
                )}
              </div>

              {/* Step 4: Security Events Triggered */}
              <div className="timeline-card">
                <div className="card-badge sec">SECURITY EVENTS TRIGGERED ({traceData.securityEvents.length})</div>
                {traceData.securityEvents.length > 0 ? (
                  <ul className="sub-timeline-list">
                    {traceData.securityEvents.map((sec) => (
                      <li key={sec.id} className="sec-item">
                        <span className={`badge-sev ${sec.severity.toLowerCase()}`}>{sec.severity}</span>
                        <strong>{sec.eventType}</strong> — IP: {sec.ipAddress}
                        <div className="sub-details">{sec.detailsJson}</div>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="empty-text">Zero security violations or privilege denials triggered.</p>
                )}
              </div>
            </div>
          )}
        </div>
      )}

      {/* TAB 2: REQUEST LOGS */}
      {activeTab === "logs" && (
        <div className="tab-content">
          <div className="logs-filter-bar">
            <select value={filterStatus} onChange={(e) => setFilterStatus(e.target.value)}>
              <option value="">All Status Codes</option>
              <option value="200">200 OK</option>
              <option value="400">400 Bad Request</option>
              <option value="401">401 Unauthorized</option>
              <option value="403">403 Forbidden</option>
              <option value="404">404 Not Found</option>
              <option value="500">500 Server Error</option>
            </select>
            <select value={filterMethod} onChange={(e) => setFilterMethod(e.target.value)}>
              <option value="">All Methods</option>
              <option value="GET">GET</option>
              <option value="POST">POST</option>
              <option value="PUT">PUT</option>
              <option value="DELETE">DELETE</option>
              <option value="PATCH">PATCH</option>
            </select>
            <input
              type="text"
              placeholder="Search route path..."
              value={filterPath}
              onChange={(e) => setFilterPath(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && fetchLogs()}
            />
            <button className="btn-filter" onClick={fetchLogs}>Filter Logs</button>
          </div>

          <div className="obs-table-wrapper">
            <table className="obs-table">
              <thead>
                <tr>
                  <th>Status</th>
                  <th>Method</th>
                  <th>Path</th>
                  <th>Duration</th>
                  <th>Client IP</th>
                  <th>Trace ID</th>
                  <th>Timestamp</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {logs.map((log) => (
                  <tr key={log.id}>
                    <td><span className={`status-badge s${log.statusCode}`}>{log.statusCode}</span></td>
                    <td><span className={`method-badge ${log.method}`}>{log.method}</span></td>
                    <td className="path-cell" title={log.path}>{log.path}</td>
                    <td>{log.durationMs}ms</td>
                    <td>{log.ipAddress}</td>
                    <td><code className="trace-code">{log.traceId}</code></td>
                    <td>{new Date(log.createdAt).toLocaleTimeString()}</td>
                    <td>
                      <button
                        className="btn-mini-inspect"
                        onClick={() => {
                          setSearchTraceId(log.traceId);
                          setActiveTab("explorer");
                          setTimeout(handleTraceLookup, 100);
                        }}
                      >
                        Inspect
                      </button>
                    </td>
                  </tr>
                ))}
                {logs.length === 0 && !logLoading && (
                  <tr><td colSpan="8" className="empty-cell">No request logs matched current filters.</td></tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="pagination-bar">
            <span>Showing page {logPage} of {Math.ceil(totalLogs / 25) || 1} ({totalLogs} total requests)</span>
            <div>
              <button disabled={logPage <= 1} onClick={() => setLogPage(p => p - 1)}>Previous</button>
              <button disabled={logPage * 25 >= totalLogs} onClick={() => setLogPage(p => p + 1)}>Next</button>
            </div>
          </div>
        </div>
      )}

      {/* TAB 3: PERFORMANCE */}
      {activeTab === "performance" && metrics && (
        <div className="tab-content">
          <div className="perf-grid">
            <div className="perf-card">
              <h3>⚡ Top 10 Slowest Endpoints</h3>
              <p className="card-sub">Ranked by average execution duration</p>
              <table className="mini-table">
                <thead>
                  <tr>
                    <th>Method</th>
                    <th>Path</th>
                    <th>Avg Latency</th>
                    <th>Requests</th>
                  </tr>
                </thead>
                <tbody>
                  {metrics.slowestEndpoints.map((ep, idx) => (
                    <tr key={idx}>
                      <td><span className={`method-badge ${ep.method}`}>{ep.method}</span></td>
                      <td><code>{ep.path}</code></td>
                      <td className="lat-cell">{ep.averageDurationMs}ms</td>
                      <td>{ep.requestCount}</td>
                    </tr>
                  ))}
                  {metrics.slowestEndpoints.length === 0 && (
                    <tr><td colSpan="4">Insufficient telemetry data</td></tr>
                  )}
                </tbody>
              </table>
            </div>

            <div className="perf-card">
              <h3>⚠️ Top 10 Failing Endpoints</h3>
              <p className="card-sub">Ranked by error response volume</p>
              <table className="mini-table">
                <thead>
                  <tr>
                    <th>Method</th>
                    <th>Path</th>
                    <th>Failures</th>
                    <th>Failure Rate</th>
                  </tr>
                </thead>
                <tbody>
                  {metrics.failingEndpoints.map((ep, idx) => (
                    <tr key={idx}>
                      <td><span className={`method-badge ${ep.method}`}>{ep.method}</span></td>
                      <td><code>{ep.path}</code></td>
                      <td className="fail-cell">{ep.errorCount}</td>
                      <td>{ep.errorRatePercentage}%</td>
                    </tr>
                  ))}
                  {metrics.failingEndpoints.length === 0 && (
                    <tr><td colSpan="4">Zero failing endpoints in last 24h! 🎉</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 4: DEEP HEALTH CHECKS */}
      {activeTab === "health" && (
        <div className="tab-content">
          {healthLoading ? (
            <p>Pinging live subsystems...</p>
          ) : healthData ? (
            <div className="health-grid">
              <div className={`health-card ${healthData.database.status.toLowerCase()}`}>
                <div className="health-badge">{healthData.database.status}</div>
                <h4>{healthData.database.name}</h4>
                <div className="health-lat">Latency: {healthData.database.latencyMs}ms</div>
                <div className="health-det">{healthData.database.details}</div>
              </div>

              <div className={`health-card ${healthData.storage.status.toLowerCase()}`}>
                <div className="health-badge">{healthData.storage.status}</div>
                <h4>{healthData.storage.name}</h4>
                <div className="health-lat">Latency: {healthData.storage.latencyMs}ms</div>
                <div className="health-det">{healthData.storage.details}</div>
              </div>

              <div className={`health-card ${healthData.authentication.status.toLowerCase()}`}>
                <div className="health-badge">{healthData.authentication.status}</div>
                <h4>{healthData.authentication.name}</h4>
                <div className="health-lat">Latency: {healthData.authentication.latencyMs}ms</div>
                <div className="health-det">{healthData.authentication.details}</div>
              </div>

              <div className={`health-card ${healthData.payments.status.toLowerCase()}`}>
                <div className="health-badge">{healthData.payments.status}</div>
                <h4>{healthData.payments.name}</h4>
                <div className="health-lat">Latency: {healthData.payments.latencyMs}ms</div>
                <div className="health-det">{healthData.payments.details}</div>
              </div>

              <div className="health-card not-monitored">
                <div className="health-badge notmonitored">NotMonitored</div>
                <h4>{healthData.messaging.name}</h4>
                <div className="health-lat">—</div>
                <div className="health-det">{healthData.messaging.details}</div>
              </div>
            </div>
          ) : null}
        </div>
      )}

      {/* PROMOTE TO INCIDENT MODAL */}
      {showIncidentModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3>🚨 Promote Trace to Operational Incident</h3>
            <p className="modal-sub">Trace ID: <code>{traceData?.traceId}</code></p>
            {incidentSuccess && <div className="obs-alert success">{incidentSuccess}</div>}
            <form onSubmit={handlePromoteSubmit}>
              <div className="form-group">
                <label>Incident Title:</label>
                <input
                  type="text"
                  required
                  value={incidentTitle}
                  onChange={(e) => setIncidentTitle(e.target.value)}
                />
              </div>
              <div className="form-group">
                <label>Severity:</label>
                <select value={incidentSeverity} onChange={(e) => setIncidentSeverity(e.target.value)}>
                  <option value="CRITICAL">CRITICAL (System outage / major degradation)</option>
                  <option value="HIGH">HIGH (Feature broken / elevated error rate)</option>
                  <option value="MEDIUM">MEDIUM (Non-critical flaw or anomaly)</option>
                  <option value="LOW">LOW (Cosmetic / minor issue)</option>
                </select>
              </div>
              <div className="modal-actions">
                <button type="button" onClick={() => setShowIncidentModal(false)}>Cancel</button>
                <button type="submit" className="btn-promote" disabled={incidentSubmitting}>
                  {incidentSubmitting ? "Creating Incident..." : "Confirm & Open Incident"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}