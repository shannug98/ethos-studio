import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { adminApi } from "../../services/adminApi";
import "./AdminAudit.css";

export default function AdminAudit() {
  const [logs, setLogs] = useState([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Filters
  const [category, setCategory] = useState("");
  const [actionType, setActionType] = useState("");
  const [entityType, setEntityType] = useState("");
  const [traceIdFilter, setTraceIdFilter] = useState("");
  const [expandedId, setExpandedId] = useState(null);
  const [copiedTrace, setCopiedTrace] = useState(null);

  const fetchLogs = () => {
    setLoading(true);
    setError("");

    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });

    if (category) params.append("category", category);
    if (actionType) params.append("action", actionType);
    if (entityType) params.append("entityType", entityType);
    if (traceIdFilter.trim()) params.append("traceId", traceIdFilter.trim());

    adminApi
      .getAuditLogs(params.toString())
      .then((res) => {
        setLogs(res.items || []);
        setTotalCount(res.totalCount || 0);
        setLoading(false);
      })
      .catch((err) => {
        setError(err.message || "Failed to load audit logs.");
        setLoading(false);
      });
  };

  useEffect(() => {
    fetchLogs();
  }, [page]);

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    setPage(1);
    fetchLogs();
  };

  const handleReset = () => {
    setCategory("");
    setActionType("");
    setEntityType("");
    setTraceIdFilter("");
    setPage(1);
    setTimeout(fetchLogs, 0);
  };

  const handleCopyTrace = (traceId) => {
    if (traceId) {
      navigator.clipboard.writeText(traceId);
      setCopiedTrace(traceId);
      setTimeout(() => setCopiedTrace(null), 2000);
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  return (
    <div className="admin-audit-page">
      <div className="audit-header">
        <div>
          <h1 className="audit-title">Administrative Audit Trail</h1>
          <p className="audit-subtitle">
            Immutable, append-only operational evidence records. Strict secret filtration enforced.
          </p>
        </div>
        <button
          type="button"
          className="audit-refresh-btn"
          onClick={fetchLogs}
          disabled={loading}
        >
          {loading ? "Refreshing..." : "↻ Refresh Logs"}
        </button>
      </div>

      {/* Filter Form */}
      <form className="audit-filter-card" onSubmit={handleSearchSubmit}>
        <div className="filter-group">
          <label>Category</label>
          <select value={category} onChange={(e) => setCategory(e.target.value)}>
            <option value="">All Categories</option>
            <option value="OPERATIONS">OPERATIONS</option>
            <option value="SYSTEM">SYSTEM</option>
            <option value="AUTHENTICATION">AUTHENTICATION</option>
            <option value="SECURITY">SECURITY</option>
          </select>
        </div>

        <div className="filter-group">
          <label>Action Type</label>
          <input
            type="text"
            placeholder="e.g. APPROVE_APPLICATION"
            value={actionType}
            onChange={(e) => setActionType(e.target.value)}
          />
        </div>

        <div className="filter-group">
          <label>Entity Type</label>
          <input
            type="text"
            placeholder="e.g. TRAINER, CLASS"
            value={entityType}
            onChange={(e) => setEntityType(e.target.value)}
          />
        </div>

        <div className="filter-group trace-filter-group">
          <label>Search by Trace ID</label>
          <input
            type="text"
            placeholder="trc_..."
            value={traceIdFilter}
            onChange={(e) => setTraceIdFilter(e.target.value)}
          />
        </div>

        <div className="filter-actions">
          <button type="submit" className="btn-filter-apply">
            Filter
          </button>
          <button type="button" className="btn-filter-reset" onClick={handleReset}>
            Reset
          </button>
        </div>
      </form>

      {error && (
        <div className="audit-error-banner">
          <span>⚠️ {error}</span>
          <button type="button" onClick={fetchLogs}>Retry</button>
        </div>
      )}

      {/* Audit Log Table */}
      <div className="audit-table-card">
        <table className="audit-table">
          <thead>
            <tr>
              <th>TIMESTAMP</th>
              <th>ADMIN ACTOR</th>
              <th>CATEGORY</th>
              <th>ACTION</th>
              <th>ENTITY</th>
              <th>OUTCOME</th>
              <th>TRACE ID</th>
              <th>DETAILS</th>
            </tr>
          </thead>
          <tbody>
            {logs.length === 0 ? (
              <tr>
                <td colSpan="8" className="empty-table-row">
                  {loading ? "Loading audit records..." : "No matching audit log records found."}
                </td>
              </tr>
            ) : (
              logs.map((log) => (
                <React.Fragment key={log.id}>
                  <tr>
                    <td className="monospace text-sm">
                      {new Date(log.createdAt).toLocaleString()}
                    </td>
                    <td>
                      <div className="actor-meta">
                        <span className="bold">{log.adminName}</span>
                        {log.adminCustomerCode && (
                          <span className="customer-code font-mono">{log.adminCustomerCode}</span>
                        )}
                        {log.deviceName && (
                          <span className="device-name-sub">💻 {log.deviceName}</span>
                        )}
                      </div>
                    </td>
                    <td>
                      <span className="category-pill">{log.category}</span>
                    </td>
                    <td className="bold">{log.actionType}</td>
                    <td>
                      <span className="entity-tag">{log.entityType}</span>
                    </td>
                    <td>
                      <span
                        className={`outcome-pill ${
                          log.success ? "pill-green" : "pill-red"
                        }`}
                      >
                        {log.outcomeCode || (log.success ? "SUCCESS" : "FAILURE")}
                      </span>
                    </td>
                    <td>
                      {log.traceId ? (
                        <button
                          type="button"
                          className="trace-copy-btn font-mono"
                          onClick={() => handleCopyTrace(log.traceId)}
                          title="Copy Trace ID"
                        >
                          {log.traceId.substring(0, 10)}...
                          <span>{copiedTrace === log.traceId ? "✓" : "📋"}</span>
                        </button>
                      ) : (
                        "—"
                      )}
                    </td>
                    <td>
                      <button
                        type="button"
                        className="btn-toggle-details"
                        onClick={() =>
                          setExpandedId(expandedId === log.id ? null : log.id)
                        }
                      >
                        {expandedId === log.id ? "Hide" : "Inspect"}
                      </button>
                    </td>
                  </tr>

                  {expandedId === log.id && (
                    <tr className="expanded-row">
                      <td colSpan="8">
                        <div className="audit-detail-panel">
                          <div className="detail-meta-grid">
                            <div>
                              <strong>IP Address:</strong> {log.ipAddress || "Unknown"}
                            </div>
                            <div>
                              <strong>User Agent:</strong> {log.userAgent || "Unknown"}
                            </div>
                            <div>
                              <strong>Entity ID:</strong>{" "}
                              <span className="font-mono">{log.entityId}</span>
                            </div>
                            {log.reason && (
                              <div>
                                <strong>Reason:</strong> {log.reason}
                              </div>
                            )}
                          </div>

                          <div className="metadata-container">
                            <strong>Sanitized Metadata JSON:</strong>
                            <pre className="metadata-pre">
                              {log.metadataJson
                                ? JSON.stringify(JSON.parse(log.metadataJson), null, 2)
                                : "{}"}
                            </pre>
                          </div>

                          {log.traceId && (
                            <div style={{ marginTop: "14px", display: "flex", gap: "10px", alignItems: "center" }}>
                              <Link
                                to={`/admin_portal/observability?traceId=${encodeURIComponent(log.traceId)}`}
                                className="btn-modal-action btn-modal-trace"
                                style={{
                                  display: "inline-flex",
                                  alignItems: "center",
                                  gap: "6px",
                                  padding: "6px 14px",
                                  background: "#4f46e5",
                                  color: "#ffffff",
                                  borderRadius: "6px",
                                  fontSize: "0.75rem",
                                  fontWeight: "700",
                                  textDecoration: "none",
                                }}
                              >
                                🔍 Open Trace Investigation →
                              </Link>
                              <span style={{ fontSize: "0.72rem", color: "#64748b" }}>
                                Trace ID: <code className="font-mono">{log.traceId}</code>
                              </span>
                            </div>
                          )}
                        </div>
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="audit-pagination">
        <span className="pagination-info">
          Showing {logs.length} of {totalCount} records (Page {page} of {totalPages})
        </span>
        <div className="pagination-buttons">
          <button
            type="button"
            className="page-btn"
            disabled={page <= 1}
            onClick={() => setPage(page - 1)}
          >
            ← Previous
          </button>
          <button
            type="button"
            className="page-btn"
            disabled={page >= totalPages}
            onClick={() => setPage(page + 1)}
          >
            Next →
          </button>
        </div>
      </div>
    </div>
  );
}
