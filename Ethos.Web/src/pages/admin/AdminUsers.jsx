import React, { useState, useEffect } from "react";
import { adminApi, getAdminUser } from "../../services/adminApi";
import "./AdminUsers.css";

export default function AdminUsers() {
  const currentAdmin = getAdminUser();
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Filters & Pagination
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  // Selected User Drawer & Audit History
  const [selectedUser, setSelectedUser] = useState(null);
  const [drawerLoading, setDrawerLoading] = useState(false);
  const [auditHistory, setAuditHistory] = useState([]);
  const [statusModalOpen, setStatusModalOpen] = useState(false);
  const [targetStatus, setTargetStatus] = useState(false);
  const [statusReason, setStatusReason] = useState("");
  const [statusActionLoading, setStatusActionLoading] = useState(false);
  const [statusActionError, setStatusActionError] = useState(null);
  const [copyFeedback, setCopyFeedback] = useState(null);

  const fetchUsers = async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      params.append("page", page);
      params.append("pageSize", 15);
      if (search) params.append("search", search);
      if (role) params.append("role", role);
      if (status !== "") params.append("status", status);

      const res = await adminApi.getUsers(params.toString());
      setUsers(res.items || []);
      setTotalCount(res.totalCount || 0);
      setTotalPages(Math.ceil((res.totalCount || 0) / 15) || 1);
    } catch (err) {
      setError(err.message || "Failed to load platform users.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchUsers();
  }, [page, role, status]);

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    setPage(1);
    fetchUsers();
  };

  const openUserDrawer = async (u) => {
    setSelectedUser(u);
    setDrawerLoading(true);
    try {
      const details = await adminApi.getUserById(u.userId);
      setSelectedUser(details);
      const historyRes = await adminApi.getUserAuditHistory(u.userId);
      setAuditHistory(historyRes?.history || []);
    } catch (err) {
      console.error("Failed to load user details / history", err);
    } finally {
      setDrawerLoading(false);
    }
  };

  const handleOpenStatusModal = (newStatus) => {
    setTargetStatus(newStatus);
    setStatusReason("");
    setStatusActionError(null);
    setStatusModalOpen(true);
  };

  const handleConfirmStatusChange = async () => {
    if (!statusReason.trim()) {
      setStatusActionError("A justification reason is mandatory.");
      return;
    }
    setStatusActionLoading(true);
    setStatusActionError(null);
    try {
      await adminApi.updateUserStatus(selectedUser.userId, targetStatus, statusReason.trim());
      setStatusModalOpen(false);
      // Refresh details & users list
      const refreshed = await adminApi.getUserById(selectedUser.userId);
      setSelectedUser(refreshed);
      const historyRes = await adminApi.getUserAuditHistory(selectedUser.userId);
      setAuditHistory(historyRes?.history || []);
      fetchUsers();
    } catch (err) {
      setStatusActionError(err.message || "Failed to update account status.");
    } finally {
      setStatusActionLoading(false);
    }
  };

  const copyTrace = (traceId) => {
    if (!traceId) return;
    navigator.clipboard.writeText(traceId);
    setCopyFeedback(traceId);
    setTimeout(() => setCopyFeedback(null), 2000);
  };

  const isProtectedPartner = (u) => {
    if (!u) return false;
    const code = u.customerCode?.toUpperCase();
    const phone = u.phone;
    return (
      code === "ETHADMIN001" ||
      code === "ETHADMIN002" ||
      phone === "8019013757" ||
      phone === "8341701113"
    );
  };

  const isSelf = (u) => {
    if (!u || !currentAdmin) return false;
    return u.userId === currentAdmin.id;
  };

  return (
    <div className="admin-users-view">
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Users & Accounts</h1>
          <p className="admin-page-subtitle">
            Manage administrative identities, roles, account status, and account history.
          </p>
        </div>
        <div className="admin-header-actions">
          <span className="user-count-badge">{totalCount} Accounts Registered</span>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="admin-users-filters">
        <form onSubmit={handleSearchSubmit} className="search-form">
          <input
            type="text"
            className="input-field"
            placeholder="Search by customer code, name, phone, or email..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <button type="submit" className="btn btn-secondary">Search</button>
        </form>

        <div className="filter-selects">
          <select
            className="select-field"
            value={role}
            onChange={(e) => {
              setRole(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All Roles</option>
            <option value="STUDENT">Student</option>
            <option value="TRAINER">Trainer</option>
            <option value="ADMIN">Admin</option>
            <option value="QUARANTINE">⚠️ Quarantined (No Role)</option>
          </select>

          <select
            className="select-field"
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All Statuses</option>
            <option value="true">Active</option>
            <option value="false">Inactive / Suspended</option>
          </select>
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {/* Users Table */}
      <div className="admin-users-table-card table-container">
        {loading ? (
          <div className="loading-state">Loading user registry...</div>
        ) : users.length === 0 ? (
          <div className="empty-state">No users match the specified criteria.</div>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>Customer Code</th>
                <th>Full Name</th>
                <th>Phone</th>
                <th>Primary Role</th>
                <th>Other Roles</th>
                <th>Status</th>
                <th>Created</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.userId} className={selectedUser?.userId === u.userId ? "selected-row" : ""}>
                  <td className="user-code-cell">{u.customerCode}</td>
                  <td className="user-name-cell">{u.fullName}</td>
                  <td className="user-contact-cell">{u.phone}</td>
                  <td>
                    {u.primaryRole ? (
                      <span className={`role-badge role-${u.primaryRole.toLowerCase()}`}>
                        {u.primaryRole}
                      </span>
                    ) : (
                      <span className="badge-quarantine-required">Role Assignment Required</span>
                    )}
                  </td>
                  <td>
                    {u.otherRoles && u.otherRoles.length > 0 ? (
                      <div className="role-tags">
                        {u.otherRoles.map((r) => (
                          <span key={r} className={`role-badge role-${r.toLowerCase()}`}>
                            {r}
                          </span>
                        ))}
                      </div>
                    ) : (
                      <span className="text-secondary">—</span>
                    )}
                  </td>
                  <td>
                    {!u.primaryRole ? (
                      <span className="status-pill status-quarantined">QUARANTINED</span>
                    ) : (
                      <span className={`status-pill ${u.isActive ? "status-active" : "status-inactive"}`}>
                        {u.isActive ? "ACTIVE" : "INACTIVE"}
                      </span>
                    )}
                  </td>
                  <td className="user-date-cell">{new Date(u.createdAt).toLocaleDateString()}</td>
                  <td>
                    <button
                      className="btn-inspect-user"
                      onClick={() => openUserDrawer(u)}
                    >
                      Inspect
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="table-pagination">
            <button
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
              className="btn btn-sm btn-secondary"
            >
              Prev
            </button>
            <span className="page-indicator">
              Page {page} of {totalPages}
            </span>
            <button
              disabled={page >= totalPages}
              onClick={() => setPage((p) => p + 1)}
              className="btn btn-sm btn-secondary"
            >
              Next
            </button>
          </div>
        )}
      </div>

      {/* User Details & Audit History Drawer */}
      {selectedUser && (
        <div className="drawer-overlay" onClick={() => setSelectedUser(null)}>
          <div className="drawer-content" onClick={(e) => e.stopPropagation()}>
            <div className="drawer-header">
              <div>
                <h2>{selectedUser.fullName}</h2>
                <div className="drawer-subtitle">
                  <span className="mono-code">{selectedUser.customerCode}</span>
                  <span className="separator">•</span>
                  <span>{selectedUser.phone}</span>
                </div>
              </div>
              <button className="close-btn" onClick={() => setSelectedUser(null)}>×</button>
            </div>

            {drawerLoading ? (
              <div className="loading-state">Loading dossier...</div>
            ) : (
              <div className="drawer-body">
                {/* Partner Protection Warning */}
                {isProtectedPartner(selectedUser) && (
                  <div className="partner-badge-banner">
                    🛡️ Protected Administrative Partner — Account lifecycle deactivation is strictly locked.
                  </div>
                )}

                {isSelf(selectedUser) && (
                  <div className="partner-badge-banner warning">
                    ⚠️ Current Operator Session — You cannot deactivate your own administrative account.
                  </div>
                )}

                {/* Identity Summary Card */}
                <div className="drawer-section">
                  <h3 className="section-title">Identity & Status</h3>
                  <div className="drawer-grid">
                    <div>
                      <span className="label">Status</span>
                      <span className={`status-pill ${selectedUser.isActive ? "status-active" : "status-inactive"}`}>
                        {selectedUser.isActive ? "ACTIVE" : "INACTIVE"}
                      </span>
                    </div>
                    <div>
                      <span className="label">Email</span>
                      <span className="val">{selectedUser.email || "None"}</span>
                    </div>
                    <div>
                      <span className="label">Roles</span>
                      <div className="role-tags">
                        {selectedUser.roles?.map((r) => (
                          <span key={r} className={`role-badge role-${r.toLowerCase()}`}>
                            {r}
                          </span>
                        ))}
                      </div>
                    </div>
                    <div>
                      <span className="label">Member Since</span>
                      <span className="val">{new Date(selectedUser.createdAt).toLocaleString()}</span>
                    </div>
                  </div>

                  {/* Status Toggle Action */}
                  <div className="status-toggle-box">
                    {selectedUser.isActive ? (
                      <button
                        className="btn btn-danger btn-sm"
                        disabled={isProtectedPartner(selectedUser) || isSelf(selectedUser)}
                        onClick={() => handleOpenStatusModal(false)}
                      >
                        Deactivate / Suspend Account
                      </button>
                    ) : (
                      <button
                        className="btn btn-success btn-sm"
                        onClick={() => handleOpenStatusModal(true)}
                      >
                        Reactivate Account
                      </button>
                    )}
                  </div>
                </div>

                {/* Audit & Security Chronological Ledger */}
                <div className="drawer-section">
                  <h3 className="section-title">Account Audit & Security Trail</h3>
                  {auditHistory.length === 0 ? (
                    <div className="empty-state-sm">No recorded audit or security actions for this identity.</div>
                  ) : (
                    <div className="timeline-list">
                      {auditHistory.map((item, idx) => (
                        <div key={idx} className={`timeline-item type-${item.type?.toLowerCase()}`}>
                          <div className="timeline-dot" />
                          <div className="timeline-content">
                            <div className="timeline-header">
                              <span className={`type-tag ${item.type === "ADMIN_ACTION" ? "tag-action" : "tag-security"}`}>
                                {item.type === "ADMIN_ACTION" ? "ADMIN ACTION" : "SECURITY EVENT"}
                              </span>
                              <span className="timeline-event">{item.event}</span>
                              <span className="timeline-time">
                                {new Date(item.timestamp).toLocaleString()}
                              </span>
                            </div>

                            {item.reason && (
                              <div className="timeline-reason">
                                <strong>Reason:</strong> {item.reason}
                              </div>
                            )}

                            {item.actorName && (
                              <div className="timeline-actor">
                                Operator: <span>{item.actorName}</span>
                              </div>
                            )}

                            {item.traceId && (
                              <div className="timeline-trace">
                                <span className="trace-code">{item.traceId}</span>
                                <button
                                  className="btn-copy-trace"
                                  onClick={() => copyTrace(item.traceId)}
                                >
                                  {copyFeedback === item.traceId ? "Copied!" : "Copy Trace"}
                                </button>
                              </div>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Mandatory Reason Status Modal */}
      {statusModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-box">
            <h3>{targetStatus ? "Confirm Account Reactivation" : "Confirm Account Suspension"}</h3>
            <p className="modal-desc">
              Please provide a mandatory operational reason. This action will be permanently logged to the immutable audit subsystem with your operator identity and trace ID.
            </p>

            <div className="form-group">
              <label className="form-label">Justification Reason (Mandatory):</label>
              <textarea
                className="textarea-field"
                rows={3}
                placeholder="e.g., Requested by student, suspected credential compromise, policy violation..."
                value={statusReason}
                onChange={(e) => setStatusReason(e.target.value)}
              />
            </div>

            {statusActionError && <div className="alert alert-danger mb-3">{statusActionError}</div>}

            <div className="modal-actions">
              <button
                className="btn btn-secondary"
                onClick={() => setStatusModalOpen(false)}
                disabled={statusActionLoading}
              >
                Cancel
              </button>
              <button
                className={`btn ${targetStatus ? "btn-success" : "btn-danger"}`}
                onClick={handleConfirmStatusChange}
                disabled={statusActionLoading}
              >
                {statusActionLoading ? "Submitting..." : targetStatus ? "Reactivate Account" : "Suspend Account"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
