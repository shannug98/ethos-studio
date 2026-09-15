import React, { useState, useEffect } from "react";
import { adminApi } from "../../services/adminApi";
import ChangePasswordModal from "../../components/admin/ChangePasswordModal";
import "./AdminSecurity.css";

export default function AdminSecurity() {
  const [activeTab, setActiveTab] = useState("fleet"); // fleet, threats, investigate, events
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);

  // Fleet State
  const [fleetData, setFleetData] = useState(null);
  const [fleetLoading, setFleetLoading] = useState(false);

  // Threats State
  const [threatsData, setThreatsData] = useState(null);
  const [threatsLoading, setThreatsLoading] = useState(false);

  // Investigation State
  const [targetType, setTargetType] = useState("IP");
  const [targetValue, setTargetValue] = useState("");
  const [investigationData, setInvestigationData] = useState(null);
  const [investigating, setInvestigating] = useState(false);

  // Events Ledger State
  const [events, setEvents] = useState([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [eventTypeFilter, setEventTypeFilter] = useState("");
  const [severityFilter, setSeverityFilter] = useState("");
  const [traceFilter, setTraceFilter] = useState("");

  // Revoke Modal
  const [revokeTarget, setRevokeTarget] = useState(null); // { type: 'session' | 'device', id, name }
  const [revokeReason, setRevokeReason] = useState("");
  const [revoking, setRevoking] = useState(false);

  // Promote Security Event to Incident
  const [promoteEvent, setPromoteEvent] = useState(null);
  const [promoteTitle, setPromoteTitle] = useState("");
  const [promoteSeverity, setPromoteSeverity] = useState("HIGH");
  const [promoting, setPromoting] = useState(false);

  useEffect(() => {
    if (activeTab === "fleet") fetchFleet();
    else if (activeTab === "threats") fetchThreats();
    else if (activeTab === "events") fetchEvents();
  }, [activeTab, page]);

  const fetchFleet = async () => {
    setFleetLoading(true);
    try {
      const res = await adminApi.getSecurityFleet();
      setFleetData(res);
      setError("");
    } catch (err) {
      setError(err.message || "Failed to load fleet security.");
    } finally {
      setFleetLoading(false);
    }
  };

  const fetchThreats = async () => {
    setThreatsLoading(true);
    try {
      const res = await adminApi.getSecurityThreats();
      setThreatsData(res);
      setError("");
    } catch (err) {
      setError(err.message || "Failed to load security threat clusters.");
    } finally {
      setThreatsLoading(false);
    }
  };

  const handleInvestigate = async (e) => {
    if (e) e.preventDefault();
    if (!targetValue.trim()) return;

    setInvestigating(true);
    setError("");
    try {
      const res = await adminApi.investigateSecurityEntity(targetType, targetValue.trim());
      setInvestigationData(res);
    } catch (err) {
      setError(err.message || "Investigation query failed.");
    } finally {
      setInvestigating(false);
    }
  };

  const fetchEvents = async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (eventTypeFilter.trim()) params.append("eventType", eventTypeFilter.trim());
      if (severityFilter) params.append("severity", severityFilter);
      if (traceFilter.trim()) params.append("traceId", traceFilter.trim());

      const res = await adminApi.getSecurityEvents(params.toString());
      setEvents(res.items || []);
      setTotalCount(res.totalCount || 0);
      setError("");
    } catch (err) {
      setError(err.message || "Failed to load security events.");
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmRevoke = async (e) => {
    e.preventDefault();
    if (!revokeTarget) return;

    setRevoking(true);
    try {
      if (revokeTarget.type === "device") {
        await adminApi.revokeSecurityDevice(revokeTarget.id, revokeReason);
      } else {
        await adminApi.revokeSecuritySession(revokeTarget.id, revokeReason);
      }
      setRevokeTarget(null);
      setRevokeReason("");
      fetchFleet();
    } catch (err) {
      alert(err.message || "Revocation failed.");
    } finally {
      setRevoking(false);
    }
  };

  const handleOpenPromote = (evt) => {
    setPromoteEvent(evt);
    setPromoteTitle(`[Security Alert] ${evt.eventType} from IP ${evt.ipAddress || "Unknown"}`);
    setPromoteSeverity(evt.severity === "CRITICAL" ? "CRITICAL" : "HIGH");
  };

  const handleConfirmPromote = async (e) => {
    e.preventDefault();
    if (!promoteEvent) return;

    setPromoting(true);
    try {
      await adminApi.createIncidentFromSecurityEvent({
        securityEventId: promoteEvent.id,
        title: promoteTitle,
        severity: promoteSeverity,
      });
      alert("Incident successfully created from security alert!");
      setPromoteEvent(null);
    } catch (err) {
      alert(err.message || "Failed to create incident from alert.");
    } finally {
      setPromoting(false);
    }
  };

  return (
    <div className="admin-security-page">
      <div className="sec-header">
        <div>
          <h2>Security Center & Threat Monitoring</h2>
          <p className="sec-subtitle">Two-device fleet management, brute force detection, risk scoring & security ledger</p>
        </div>
        <div className="sec-header-actions">
          <button className="btn-refresh" onClick={() => {
            if (activeTab === "fleet") fetchFleet();
            else if (activeTab === "threats") fetchThreats();
            else if (activeTab === "events") fetchEvents();
          }}>
            🔄 Refresh
          </button>
          <button
            type="button"
            className="btn-refresh"
            style={{ backgroundColor: "#4f46e5", color: "#ffffff", borderColor: "#4338ca" }}
            onClick={() => setChangePasswordOpen(true)}
          >
            🔑 Change Password
          </button>
        </div>
      </div>

      {error && <div className="sec-alert error">{error}</div>}

      {/* Tabs */}
      <div className="sec-tabs">
        <button className={`sec-tab ${activeTab === "fleet" ? "active" : ""}`} onClick={() => setActiveTab("fleet")}>
          💻 Fleet & Sessions
        </button>
        <button className={`sec-tab ${activeTab === "threats" ? "active" : ""}`} onClick={() => setActiveTab("threats")}>
          🚨 Threat Clusters
        </button>
        <button className={`sec-tab ${activeTab === "investigate" ? "active" : ""}`} onClick={() => setActiveTab("investigate")}>
          🔍 Risk Profiler
        </button>
        <button className={`sec-tab ${activeTab === "events" ? "active" : ""}`} onClick={() => setActiveTab("events")}>
          🛡️ Security Events Ledger
        </button>
      </div>

      {/* TAB 1: FLEET & SESSIONS */}
      {activeTab === "fleet" && (
        <div className="tab-content">
          {fleetLoading ? (
            <p>Querying active devices and sessions...</p>
          ) : fleetData ? (
            <div>
              {/* Fleet Status Bar */}
              <div className="fleet-status-banner">
                <div className="slot-badge">
                  Active Slots: <strong>{fleetData.activeSlotCount} / {fleetData.maxAllowedSlots}</strong> (Hard Cap Enforced)
                </div>
                {fleetData.activeSlotCount > fleetData.maxAllowedSlots && (
                  <div className="slot-warning">⚠️ Policy Violation: Active devices exceed 2 slots!</div>
                )}
              </div>

              <h3>Authorized Admin Hardware Slots</h3>
              <div className="device-cards-grid">
                {fleetData.activeDevices.map((dev) => (
                  <div key={dev.id} className="device-card active">
                    <div className="device-status-pill active">ACTIVE SLOT</div>
                    <h4>{dev.deviceName}</h4>
                    <div className="dev-meta">
                      <div><strong>Admin:</strong> {dev.adminFullName} ({dev.adminPhone})</div>
                      <div><strong>Registered:</strong> {new Date(dev.registeredAt).toLocaleDateString()}</div>
                      <div><strong>Last IP:</strong> {dev.lastSeenIp || "—"}</div>
                    </div>
                    <button
                      className="btn-revoke-dev"
                      onClick={() => setRevokeTarget({ type: "device", id: dev.id, name: dev.deviceName })}
                    >
                      Revoke Device
                    </button>
                  </div>
                ))}
                {fleetData.activeDevices.length === 0 && (
                  <p>No active hardware devices registered.</p>
                )}
              </div>

              <h3 style={{ marginTop: "32px" }}>Active Live Sessions ({fleetData.activeSessions.length})</h3>
              <div className="sec-table-wrapper">
                <table className="sec-table">
                  <thead>
                    <tr>
                      <th>Device</th>
                      <th>Admin</th>
                      <th>Created</th>
                      <th>Expires</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {fleetData.activeSessions.map((s) => (
                      <tr key={s.id}>
                        <td><strong>{s.deviceName}</strong></td>
                        <td>{s.adminUserId}</td>
                        <td>{new Date(s.createdAt).toLocaleTimeString()}</td>
                        <td>{new Date(s.expiresAt).toLocaleTimeString()}</td>
                        <td>
                          <button
                            className="btn-revoke-sess"
                            onClick={() => setRevokeTarget({ type: "session", id: s.id, name: s.deviceName })}
                          >
                            Terminate Session
                          </button>
                        </td>
                      </tr>
                    ))}
                    {fleetData.activeSessions.length === 0 && (
                      <tr><td colSpan="5" className="empty-cell">Zero active administrative sessions.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          ) : null}
        </div>
      )}

      {/* TAB 2: THREAT CLUSTERS */}
      {activeTab === "threats" && (
        <div className="tab-content">
          {threatsLoading ? (
            <p>Aggregating 24-hour threat telemetry...</p>
          ) : threatsData ? (
            <div className="threats-grid">
              <div className="threat-panel">
                <h3>🔨 Failed Login Clusters (By IP)</h3>
                <p className="panel-sub">Brute force & credential stuffing detection</p>
                <table className="mini-table">
                  <thead>
                    <tr>
                      <th>Client IP</th>
                      <th>Attempts</th>
                      <th>Targeted Accounts</th>
                      <th>Severity</th>
                    </tr>
                  </thead>
                  <tbody>
                    {threatsData.failedLoginClusters.map((c, i) => (
                      <tr key={i}>
                        <td><code>{c.ipAddress}</code></td>
                        <td><strong>{c.attemptCount}</strong></td>
                        <td>{c.targetedPhones.join(", ") || "—"}</td>
                        <td><span className={`badge-sev ${c.severity.toLowerCase()}`}>{c.severity}</span></td>
                      </tr>
                    ))}
                    {threatsData.failedLoginClusters.length === 0 && (
                      <tr><td colSpan="4">Zero brute force login clusters detected in 24h! 🎉</td></tr>
                    )}
                  </tbody>
                </table>
              </div>

              <div className="threat-panel">
                <h3>⛔ Authorization Denial Spikes</h3>
                <p className="panel-sub">Privilege escalation & IDOR probes</p>
                <table className="mini-table">
                  <thead>
                    <tr>
                      <th>User ID</th>
                      <th>Denied Permission</th>
                      <th>Denials</th>
                      <th>Last Attempt</th>
                    </tr>
                  </thead>
                  <tbody>
                    {threatsData.authorizationDenialClusters.map((a, i) => (
                      <tr key={i}>
                        <td><code>{a.userId ? a.userId.substring(0, 8) + "..." : "Anonymous"}</code></td>
                        <td>{a.attemptedPermission}</td>
                        <td><strong>{a.denialCount}</strong></td>
                        <td>{new Date(a.lastAttempt).toLocaleTimeString()}</td>
                      </tr>
                    ))}
                    {threatsData.authorizationDenialClusters.length === 0 && (
                      <tr><td colSpan="4">Zero authorization denials in 24h! 🎉</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          ) : null}
        </div>
      )}

      {/* TAB 3: RISK PROFILER */}
      {activeTab === "investigate" && (
        <div className="tab-content">
          <div className="investigate-search-box">
            <form onSubmit={handleInvestigate}>
              <select value={targetType} onChange={(e) => setTargetType(e.target.value)}>
                <option value="IP">Client IP Address</option>
                <option value="USER">User ID (GUID)</option>
                <option value="DEVICE">Admin Device ID (GUID)</option>
                <option value="TRACE">Server Trace ID</option>
              </select>
              <input
                type="text"
                required
                placeholder={`Enter ${targetType}...`}
                value={targetValue}
                onChange={(e) => setTargetValue(e.target.value)}
              />
              <button type="submit" className="btn-search" disabled={investigating}>
                {investigating ? "Calculating Risk..." : "Investigate Entity"}
              </button>
            </form>
          </div>

          {investigationData && (
            <div className="risk-profile-panel">
              <div className="risk-score-hero">
                <div className={`risk-dial ${investigationData.riskLevel.toLowerCase()}`}>
                  <div className="score-num">{investigationData.riskScore}</div>
                  <div className="score-label">/ 100</div>
                  <div className="risk-level-badge">{investigationData.riskLevel} RISK</div>
                </div>
                <div className="risk-hero-info">
                  <h3>Investigation Target: <code>{investigationData.targetValue}</code> ({investigationData.targetType})</h3>
                  <p className="policy-text"><strong>Deterministic Scoring Policy:</strong> {investigationData.scoringPolicy}</p>
                </div>
              </div>

              <h3>Contributing Anomaly Factors ({investigationData.contributingFactors.length})</h3>
              <div className="factors-list">
                {investigationData.contributingFactors.map((f, i) => (
                  <div key={i} className="factor-card">
                    <div className="factor-impact">+{f.scoreImpact} pts</div>
                    <div>
                      <strong>{f.factorName}</strong>
                      <p className="factor-evidence">{f.evidence}</p>
                    </div>
                  </div>
                ))}
                {investigationData.contributingFactors.length === 0 && (
                  <p className="empty-text">Pristine profile — zero contributing risk factors detected.</p>
                )}
              </div>

              <h3 style={{ marginTop: "24px" }}>Correlated Security Events ({investigationData.eventHistory.length})</h3>
              <div className="sec-table-wrapper">
                <table className="sec-table">
                  <thead>
                    <tr>
                      <th>Severity</th>
                      <th>Event Type</th>
                      <th>Trace ID</th>
                      <th>Details</th>
                      <th>Timestamp</th>
                    </tr>
                  </thead>
                  <tbody>
                    {investigationData.eventHistory.map((h) => (
                      <tr key={h.id}>
                        <td><span className={`badge-sev ${h.severity.toLowerCase()}`}>{h.severity}</span></td>
                        <td><strong>{h.eventType}</strong></td>
                        <td><code>{h.traceId}</code></td>
                        <td className="details-cell">{h.detailsJson}</td>
                        <td>{new Date(h.createdAt).toLocaleTimeString()}</td>
                      </tr>
                    ))}
                    {investigationData.eventHistory.length === 0 && (
                      <tr><td colSpan="5" className="empty-cell">Zero security events recorded for this entity.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}

      {/* TAB 4: EVENTS LEDGER */}
      {activeTab === "events" && (
        <div className="tab-content">
          <div className="events-filter-bar">
            <select value={severityFilter} onChange={(e) => setSeverityFilter(e.target.value)}>
              <option value="">All Severities</option>
              <option value="CRITICAL">CRITICAL</option>
              <option value="HIGH">HIGH</option>
              <option value="WARNING">WARNING</option>
              <option value="INFO">INFO</option>
            </select>
            <input
              type="text"
              placeholder="Filter by Event Type (e.g. ADMIN_LOGIN_FAILED)"
              value={eventTypeFilter}
              onChange={(e) => setEventTypeFilter(e.target.value)}
            />
            <input
              type="text"
              placeholder="Filter by Trace ID"
              value={traceFilter}
              onChange={(e) => setTraceFilter(e.target.value)}
            />
            <button className="btn-filter" onClick={fetchEvents}>Filter</button>
          </div>

          <div className="sec-table-wrapper">
            <table className="sec-table">
              <thead>
                <tr>
                  <th>Severity</th>
                  <th>Event Type</th>
                  <th>Actor / Phone</th>
                  <th>Client IP</th>
                  <th>Trace ID</th>
                  <th>Timestamp</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {events.map((e) => (
                  <tr key={e.id}>
                    <td><span className={`badge-sev ${e.severity.toLowerCase()}`}>{e.severity}</span></td>
                    <td><strong>{e.eventType}</strong></td>
                    <td>{e.maskedPhone || e.adminName || (e.userId ? e.userId.substring(0, 8) + "..." : "Anonymous")}</td>
                    <td>{e.ipAddress}</td>
                    <td><code>{e.traceId}</code></td>
                    <td>{new Date(e.createdAt).toLocaleTimeString()}</td>
                    <td>
                      <button className="btn-mini-promote" onClick={() => handleOpenPromote(e)}>
                        Create Incident
                      </button>
                    </td>
                  </tr>
                ))}
                {events.length === 0 && !loading && (
                  <tr><td colSpan="7" className="empty-cell">No security events found.</td></tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="pagination-bar">
            <span>Showing page {page} of {Math.ceil(totalCount / pageSize) || 1} ({totalCount} total events)</span>
            <div>
              <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</button>
              <button disabled={page * pageSize >= totalCount} onClick={() => setPage(p => p + 1)}>Next</button>
            </div>
          </div>
        </div>
      )}

      {/* REVOKE MODAL */}
      {revokeTarget && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3>⚠️ Confirm Emergency {revokeTarget.type === "device" ? "Device Revocation" : "Session Termination"}</h3>
            <p className="modal-sub">Target: <strong>{revokeTarget.name}</strong> ({revokeTarget.id})</p>
            <form onSubmit={handleConfirmRevoke}>
              <div className="form-group">
                <label>Operational Reason (Mandatory):</label>
                <textarea
                  required
                  rows="3"
                  placeholder="State reason for revocation..."
                  value={revokeReason}
                  onChange={(e) => setRevokeReason(e.target.value)}
                />
              </div>
              <div className="modal-actions">
                <button type="button" onClick={() => setRevokeTarget(null)}>Cancel</button>
                <button type="submit" className="btn-revoke-dev" disabled={revoking}>
                  {revoking ? "Revoking..." : "Confirm Immediate Revocation"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* PROMOTE SECURITY ALERT MODAL */}
      {promoteEvent && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3>🚨 Create Security Incident from Alert</h3>
            <p className="modal-sub">Event: <strong>{promoteEvent.eventType}</strong> (ID: {promoteEvent.id})</p>
            <form onSubmit={handleConfirmPromote}>
              <div className="form-group">
                <label>Incident Title:</label>
                <input
                  type="text"
                  required
                  value={promoteTitle}
                  onChange={(e) => setPromoteTitle(e.target.value)}
                />
              </div>
              <div className="form-group">
                <label>Severity:</label>
                <select value={promoteSeverity} onChange={(e) => setPromoteSeverity(e.target.value)}>
                  <option value="CRITICAL">CRITICAL</option>
                  <option value="HIGH">HIGH</option>
                  <option value="MEDIUM">MEDIUM</option>
                  <option value="LOW">LOW</option>
                </select>
              </div>
              <div className="modal-actions">
                <button type="button" onClick={() => setPromoteEvent(null)}>Cancel</button>
                <button type="submit" className="btn-promote" disabled={promoting}>
                  {promoting ? "Creating Incident..." : "Confirm Security Incident"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <ChangePasswordModal
        isOpen={changePasswordOpen}
        onClose={() => setChangePasswordOpen(false)}
      />
    </div>
  );
}