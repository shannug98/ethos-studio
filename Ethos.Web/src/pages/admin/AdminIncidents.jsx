import React, { useState, useEffect } from "react";
import { adminApi } from "../../services/adminApi";
import "./AdminIncidents.css";

export default function AdminIncidents() {
  const [incidents, setIncidents] = useState([]);
  const [metrics, setMetrics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Filters
  const [statusFilter, setStatusFilter] = useState("");
  const [severityFilter, setSeverityFilter] = useState("");
  const [viewMode, setViewMode] = useState("board"); // "board" or "table"

  // Create Incident Modal
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newTitle, setNewTitle] = useState("");
  const [newSeverity, setNewSeverity] = useState("HIGH");
  const [newService, setNewService] = useState("API");
  const [newDescription, setNewDescription] = useState("");
  const [creating, setCreating] = useState(false);

  // Selected Incident Detail Drawer
  const [selectedIncident, setSelectedIncident] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [newUpdateMessage, setNewUpdateMessage] = useState("");
  const [postingUpdate, setPostingUpdate] = useState(false);

  // Resolution Modal
  const [showResolveModal, setShowResolveModal] = useState(false);
  const [rootCause, setRootCause] = useState("");
  const [resolutionNotes, setResolutionNotes] = useState("");
  const [resolving, setResolving] = useState(false);

  const fetchMetrics = async () => {
    try {
      const metricsRes = await adminApi.getIncidentMetrics();
      setMetrics(metricsRes);
    } catch (err) {
      console.error("Failed to load incident metrics", err);
    }
  };

  const loadIncidents = async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams();
      if (statusFilter) params.append("status", statusFilter);
      if (severityFilter) params.append("severity", severityFilter);

      const listRes = await adminApi.getIncidents(params.toString());
      setIncidents(listRes.items || []);
      setError("");
    } catch (err) {
      setError(err.message || "Failed to load incidents data.");
    } finally {
      setLoading(false);
    }
  };

  const loadData = async () => {
    await Promise.all([loadIncidents(), fetchMetrics()]);
  };

  // Load global incident metrics once on mount
  useEffect(() => {
    fetchMetrics();
  }, []);

  // Load incidents list when filters change
  useEffect(() => {
    loadIncidents();
  }, [statusFilter, severityFilter]);

  const handleCreateSubmit = async (e) => {
    e.preventDefault();
    if (!newTitle.trim()) return;

    setCreating(true);
    try {
      await adminApi.createIncident({
        title: newTitle.trim(),
        severity: newSeverity,
        affectedService: newService,
        description: newDescription.trim(),
      });
      setShowCreateModal(false);
      setNewTitle("");
      setNewDescription("");
      loadData();
    } catch (err) {
      alert(err.message || "Failed to create incident.");
    } finally {
      setCreating(false);
    }
  };

  const handleSelectIncident = async (incidentId) => {
    setDetailLoading(true);
    try {
      const fullInc = await adminApi.getIncidentById(incidentId);
      setSelectedIncident(fullInc);
    } catch (err) {
      alert(err.message || "Failed to fetch incident details.");
    } finally {
      setDetailLoading(false);
    }
  };

  const handlePostUpdate = async (e) => {
    e.preventDefault();
    if (!newUpdateMessage.trim() || !selectedIncident) return;

    setPostingUpdate(true);
    try {
      const updated = await adminApi.addIncidentUpdate(selectedIncident.id, {
        message: newUpdateMessage.trim(),
      });
      setSelectedIncident(updated);
      setNewUpdateMessage("");
      loadData();
    } catch (err) {
      alert(err.message || "Failed to post timeline update.");
    } finally {
      setPostingUpdate(false);
    }
  };

  const handleStatusTransition = async (targetStatus) => {
    if (!selectedIncident) return;

    if (targetStatus === "Resolved") {
      setShowResolveModal(true);
      return;
    }

    try {
      const updated = await adminApi.updateIncidentStatus(selectedIncident.id, {
        targetStatus,
      });
      setSelectedIncident(updated);
      loadData();
    } catch (err) {
      alert(err.message || `Failed to transition status to ${targetStatus}.`);
    }
  };

  const handleConfirmResolve = async (e) => {
    e.preventDefault();
    if (!rootCause.trim() || !resolutionNotes.trim() || !selectedIncident) {
      alert("Both Root Cause and Resolution Notes are mandatory to resolve an incident.");
      return;
    }

    setResolving(true);
    try {
      const updated = await adminApi.updateIncidentStatus(selectedIncident.id, {
        targetStatus: "Resolved",
        rootCause: rootCause.trim(),
        resolutionNotes: resolutionNotes.trim(),
      });
      setSelectedIncident(updated);
      setShowResolveModal(false);
      setRootCause("");
      setResolutionNotes("");
      loadData();
    } catch (err) {
      alert(err.message || "Failed to resolve incident.");
    } finally {
      setResolving(false);
    }
  };

  const statusColumns = [
    { key: "Investigating", label: "Investigating", color: "col-investigating" },
    { key: "Identified", label: "Identified", color: "col-identified" },
    { key: "Monitoring", label: "Monitoring", color: "col-monitoring" },
    { key: "Resolved", label: "Resolved", color: "col-resolved" },
  ];

  return (
    <div className="admin-incidents-page">
      <div className="inc-header">
        <div>
          <h2>Incident Center & Outage Management</h2>
          <p className="inc-subtitle">Canonical incident registry, lifecycle progression, MTTR metrics, and root-cause governance</p>
        </div>
        <div className="inc-header-actions">
          <div className="view-toggle">
            <button
              className={`toggle-btn ${viewMode === "board" ? "active" : ""}`}
              onClick={() => setViewMode("board")}
            >
              📋 Kanban Board
            </button>
            <button
              className={`toggle-btn ${viewMode === "table" ? "active" : ""}`}
              onClick={() => setViewMode("table")}
            >
              📑 Registry Table
            </button>
          </div>
          <button className="btn-create-inc" onClick={() => setShowCreateModal(true)}>
            + Open Incident
          </button>
          <button className="btn-refresh" onClick={loadData} disabled={loading}>
            🔄 Refresh
          </button>
        </div>
      </div>

      {error && <div className="inc-alert error">{error}</div>}

      {/* KPI Cards */}
      {metrics && (
        <div className="inc-kpi-grid">
          <div className="inc-kpi-card">
            <span className="kpi-label">Active Open Incidents</span>
            <span className="kpi-value">{metrics.openIncidentsCount}</span>
            <span className="kpi-sub">Investigating, Identified, Monitoring</span>
          </div>
          <div className={`inc-kpi-card ${metrics.criticalIncidentsCount > 0 ? "critical-kpi" : ""}`}>
            <span className="kpi-label">Critical Outages</span>
            <span className="kpi-value">{metrics.criticalIncidentsCount}</span>
            <span className="kpi-sub">P0 / Sev-1 System Issues</span>
          </div>
          <div className="inc-kpi-card">
            <span className="kpi-label">Mean Time to Resolve (MTTR)</span>
            <span className="kpi-value">{metrics.mttrMinutes} min</span>
            <span className="kpi-sub">Across all resolved incidents</span>
          </div>
          <div className="inc-kpi-card">
            <span className="kpi-label">Total Recorded Incidents</span>
            <span className="kpi-value">{metrics.totalIncidentsCount}</span>
            <span className="kpi-sub">All-time lifetime registry</span>
          </div>
        </div>
      )}

      {/* Filter Bar */}
      <div className="inc-filter-bar">
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="">All Statuses</option>
          <option value="Investigating">Investigating</option>
          <option value="Identified">Identified</option>
          <option value="Monitoring">Monitoring</option>
          <option value="Resolved">Resolved</option>
          <option value="Cancelled">Cancelled</option>
        </select>
        <select value={severityFilter} onChange={(e) => setSeverityFilter(e.target.value)}>
          <option value="">All Severities</option>
          <option value="CRITICAL">CRITICAL</option>
          <option value="HIGH">HIGH</option>
          <option value="MEDIUM">MEDIUM</option>
          <option value="LOW">LOW</option>
        </select>
      </div>

      {/* VIEW MODE: KANBAN BOARD */}
      {viewMode === "board" && (
        <div className="kanban-board">
          {statusColumns.map((col) => {
            const colIncidents = incidents.filter((i) => i.status === col.key);
            return (
              <div key={col.key} className={`kanban-column ${col.color}`}>
                <div className="kanban-col-header">
                  <h4>{col.label}</h4>
                  <span className="kanban-count-pill">{colIncidents.length}</span>
                </div>
                <div className="kanban-cards-container">
                  {colIncidents.map((inc) => (
                    <div
                      key={inc.id}
                      className="kanban-card"
                      onClick={() => handleSelectIncident(inc.id)}
                    >
                      <div className="card-top">
                        <span className="inc-num">{inc.incidentNumber}</span>
                        <span className={`badge-sev ${inc.severity.toLowerCase()}`}>
                          {inc.severity}
                        </span>
                      </div>
                      <div className="card-title">{inc.title}</div>
                      <div className="card-service">
                        <span>📦 {inc.affectedService}</span>
                        {inc.traceId && <span className="trace-tag" title={inc.traceId}>🔗 Trace</span>}
                      </div>
                      <div className="card-meta">
                        <span>{new Date(inc.createdAt).toLocaleDateString()}</span>
                        <span>{inc.assignedAdminName || "Unassigned"}</span>
                      </div>
                    </div>
                  ))}
                  {colIncidents.length === 0 && (
                    <div className="kanban-empty">No incidents in this stage</div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* VIEW MODE: TABLE */}
      {viewMode === "table" && (
        <div className="inc-table-wrapper">
          <table className="inc-table">
            <thead>
              <tr>
                <th>Incident #</th>
                <th>Severity</th>
                <th>Status</th>
                <th>Title</th>
                <th>Service</th>
                <th>Assigned To</th>
                <th>Trace Link</th>
                <th>Created</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {incidents.map((inc) => (
                <tr key={inc.id}>
                  <td><strong>{inc.incidentNumber}</strong></td>
                  <td><span className={`badge-sev ${inc.severity.toLowerCase()}`}>{inc.severity}</span></td>
                  <td><span className={`badge-status status-${inc.status.toLowerCase()}`}>{inc.status}</span></td>
                  <td className="title-cell">{inc.title}</td>
                  <td>{inc.affectedService}</td>
                  <td>{inc.assignedAdminName || "Unassigned"}</td>
                  <td>{inc.traceId ? <code>{inc.traceId.substring(0, 12)}...</code> : "—"}</td>
                  <td>{new Date(inc.createdAt).toLocaleDateString()}</td>
                  <td>
                    <button
                      className="btn-inspect-inc"
                      onClick={() => handleSelectIncident(inc.id)}
                    >
                      View
                    </button>
                  </td>
                </tr>
              ))}
              {incidents.length === 0 && !loading && (
                <tr><td colSpan="9" className="empty-cell">No incidents match the filters.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* INCIDENT DETAILS DRAWER */}
      {selectedIncident && (
        <div className="drawer-overlay" onClick={() => setSelectedIncident(null)}>
          <div className="incident-drawer" onClick={(e) => e.stopPropagation()}>
            <div className="drawer-header">
              <div>
                <span className="drawer-inc-num">{selectedIncident.incidentNumber}</span>
                <h3>{selectedIncident.title}</h3>
              </div>
              <button className="btn-close-drawer" onClick={() => setSelectedIncident(null)}>✕</button>
            </div>

            <div className="drawer-body">
              {/* Status & Action Controls */}
              <div className="drawer-action-bar">
                <div className="current-status">
                  Status: <span className={`badge-status status-${selectedIncident.status.toLowerCase()}`}>{selectedIncident.status}</span>
                  <span className={`badge-sev ${selectedIncident.severity.toLowerCase()}`} style={{ marginLeft: "8px" }}>
                    {selectedIncident.severity}
                  </span>
                </div>

                <div className="lifecycle-actions">
                  {selectedIncident.status === "Investigating" && (
                    <button className="btn-state-action" onClick={() => handleStatusTransition("Identified")}>
                      Mark Identified →
                    </button>
                  )}
                  {selectedIncident.status === "Identified" && (
                    <button className="btn-state-action" onClick={() => handleStatusTransition("Monitoring")}>
                      Move to Monitoring →
                    </button>
                  )}
                  {(selectedIncident.status === "Investigating" ||
                    selectedIncident.status === "Identified" ||
                    selectedIncident.status === "Monitoring") && (
                    <button className="btn-resolve" onClick={() => handleStatusTransition("Resolved")}>
                      ✓ Resolve Incident
                    </button>
                  )}
                  {selectedIncident.status !== "Resolved" && selectedIncident.status !== "Cancelled" && (
                    <button className="btn-cancel-inc" onClick={() => handleStatusTransition("Cancelled")}>
                      Cancel
                    </button>
                  )}
                </div>
              </div>

              {/* Incident Metadata */}
              <div className="drawer-meta-grid">
                <div><strong>Service:</strong> {selectedIncident.affectedService}</div>
                <div><strong>Created:</strong> {new Date(selectedIncident.createdAt).toLocaleString()}</div>
                <div><strong>Assigned To:</strong> {selectedIncident.assignedAdminName || "Unassigned"}</div>
                <div>
                  <strong>Trace ID:</strong> {selectedIncident.traceId ? (
                    <a
                      href={`/admin_portal/observability?traceId=${selectedIncident.traceId}`}
                      className="trace-link"
                    >
                      {selectedIncident.traceId}
                    </a>
                  ) : "None"}
                </div>
                {selectedIncident.resolvedAt && (
                  <div><strong>Resolved:</strong> {new Date(selectedIncident.resolvedAt).toLocaleString()}</div>
                )}
              </div>

              {/* Resolution Info if Resolved */}
              {selectedIncident.status === "Resolved" && (
                <div className="resolved-banner">
                  <h4>✅ Resolution Summary</h4>
                  <div><strong>Root Cause:</strong> {selectedIncident.rootCause}</div>
                  <div style={{ marginTop: "6px" }}><strong>Resolution Notes:</strong> {selectedIncident.resolutionNotes}</div>
                </div>
              )}

              {/* Evidence JSON (Sanitized) */}
              {selectedIncident.evidenceJson && (
                <div className="evidence-section">
                  <h4>Investigative Evidence (Scrubbed)</h4>
                  <pre className="evidence-box">{selectedIncident.evidenceJson}</pre>
                </div>
              )}

              {/* Timeline Updates */}
              <div className="timeline-section">
                <h4>Chronological Incident Timeline ({selectedIncident.updates?.length || 0})</h4>

                {/* Post New Update */}
                {selectedIncident.status !== "Resolved" && selectedIncident.status !== "Cancelled" && (
                  <form onSubmit={handlePostUpdate} className="post-update-form">
                    <textarea
                      required
                      rows="2"
                      placeholder="Post a progress note or mitigation update to this incident..."
                      value={newUpdateMessage}
                      onChange={(e) => setNewUpdateMessage(e.target.value)}
                    />
                    <button type="submit" className="btn-post-update" disabled={postingUpdate}>
                      {postingUpdate ? "Posting..." : "Post Update"}
                    </button>
                  </form>
                )}

                <div className="timeline-trail">
                  {selectedIncident.updates?.map((u) => (
                    <div key={u.id} className="timeline-node">
                      <div className="node-dot"></div>
                      <div className="node-content">
                        <div className="node-header">
                          <strong>{u.authorName}</strong>
                          <span className="node-time">{new Date(u.createdAt).toLocaleString()}</span>
                        </div>
                        <div className="node-msg">{u.message}</div>
                      </div>
                    </div>
                  ))}
                  {(!selectedIncident.updates || selectedIncident.updates.length === 0) && (
                    <p className="empty-text">No updates recorded yet.</p>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* RESOLUTION MODAL */}
      {showResolveModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3>✅ Resolve Incident {selectedIncident?.incidentNumber}</h3>
            <p className="modal-sub">Root Cause and Resolution Notes are strictly mandatory before marking an incident resolved.</p>
            <form onSubmit={handleConfirmResolve}>
              <div className="form-group">
                <label>Authoritative Root Cause (Required):</label>
                <textarea
                  required
                  rows="3"
                  placeholder="Explain the underlying defect or trigger..."
                  value={rootCause}
                  onChange={(e) => setRootCause(e.target.value)}
                />
              </div>
              <div className="form-group">
                <label>Resolution & Mitigation Notes (Required):</label>
                <textarea
                  required
                  rows="3"
                  placeholder="Detail the actions taken to fix and verify the issue..."
                  value={resolutionNotes}
                  onChange={(e) => setResolutionNotes(e.target.value)}
                />
              </div>
              <div className="modal-actions">
                <button type="button" onClick={() => setShowResolveModal(false)}>Cancel</button>
                <button type="submit" className="btn-resolve" disabled={resolving}>
                  {resolving ? "Resolving..." : "Confirm & Resolve Incident"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* CREATE INCIDENT MODAL */}
      {showCreateModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3>🚨 Open New Operational Incident</h3>
            <p className="modal-sub">Create an authoritative incident tracking record.</p>
            <form onSubmit={handleCreateSubmit}>
              <div className="form-group">
                <label>Incident Title:</label>
                <input
                  type="text"
                  required
                  placeholder="Brief descriptive title..."
                  value={newTitle}
                  onChange={(e) => setNewTitle(e.target.value)}
                />
              </div>
              <div className="form-row">
                <div className="form-group flex-1">
                  <label>Severity:</label>
                  <select value={newSeverity} onChange={(e) => setNewSeverity(e.target.value)}>
                    <option value="CRITICAL">CRITICAL (Sev-1 Outage)</option>
                    <option value="HIGH">HIGH (Degraded Feature)</option>
                    <option value="MEDIUM">MEDIUM (Functional Anomaly)</option>
                    <option value="LOW">LOW (Cosmetic / Minor)</option>
                  </select>
                </div>
                <div className="form-group flex-1">
                  <label>Affected Service:</label>
                  <select value={newService} onChange={(e) => setNewService(e.target.value)}>
                    <option value="API">API Gateway & Runtime</option>
                    <option value="Database">PostgreSQL Database</option>
                    <option value="Payments">Payments & Razorpay</option>
                    <option value="Bookings">Bookings & Attendance</option>
                    <option value="Classes">Classes & Workshops</option>
                    <option value="Authentication">Authentication & JWT</option>
                    <option value="Storage">Supabase Cloud Storage</option>
                  </select>
                </div>
              </div>
              <div className="form-group">
                <label>Description & Initial Evidence:</label>
                <textarea
                  rows="4"
                  placeholder="Provide context, affected users, error snippets..."
                  value={newDescription}
                  onChange={(e) => setNewDescription(e.target.value)}
                />
              </div>
              <div className="modal-actions">
                <button type="button" onClick={() => setShowCreateModal(false)}>Cancel</button>
                <button type="submit" className="btn-create-inc" disabled={creating}>
                  {creating ? "Opening..." : "Create Incident"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
