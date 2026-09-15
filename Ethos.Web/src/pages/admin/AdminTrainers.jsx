import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { adminApi } from "../../services/adminApi";
import AdminExportButton from "../../components/admin/common/AdminExportButton";
import AdminTrainerApplicationDetailsModal from "../../components/admin/AdminTrainerApplicationDetailsModal";
import "./AdminTrainers.css";

export default function AdminTrainers() {
  const [activeTab, setActiveTab] = useState("directory");

  // Tab 1: Directory State
  const [trainers, setTrainers] = useState([]);
  const [trainersLoading, setTrainersLoading] = useState(true);
  const [trainersError, setTrainersError] = useState(null);
  const [search, setSearch] = useState("");
  const [tierFilter, setTierFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [directoryPage, setDirectoryPage] = useState(1);
  const [totalTrainers, setTotalTrainers] = useState(0);

  // Tab 2: Applications Queue State
  const [applications, setApplications] = useState([]);
  const [appsLoading, setAppsLoading] = useState(false);
  const [appsError, setAppsError] = useState(null);
  const [selectedApp, setSelectedApp] = useState(null);
  const [detailsModalApp, setDetailsModalApp] = useState(null);
  const [reviewModalType, setReviewModalType] = useState(null); // "approve" | "reject" | "changes"
  const [reviewReason, setReviewReason] = useState("");
  const [reviewAdminNotes, setReviewAdminNotes] = useState("");
  const [reviewLoading, setReviewLoading] = useState(false);
  const [reviewError, setReviewError] = useState(null);

  // Tab 3: Upgrade Requests State
  const [upgrades, setUpgrades] = useState([]);
  const [upgradesLoading, setUpgradesLoading] = useState(false);
  const [upgradesError, setUpgradesError] = useState(null);
  const [upgradeModalType, setUpgradeModalType] = useState(null); // "approve" | "reject"
  const [selectedUpgrade, setSelectedUpgrade] = useState(null);
  const [upgradeReason, setUpgradeReason] = useState("");
  const [upgradeLoading, setUpgradeLoading] = useState(false);
  const [upgradeError, setUpgradeError] = useState(null);

  // Tab 4: Tiers & Permissions Matrix State
  const [tiers, setTiers] = useState([]);
  const [_tiersLoading, setTiersLoading] = useState(false);

  // Summary KPI Stats State
  const [stats, setStats] = useState(null);

  // Fetch KPI Stats
  const fetchStats = async () => {
    try {
      const res = await adminApi.getTrainerStats();
      setStats(res);
    } catch (err) {
      console.error("Failed to load trainer summary stats:", err);
    }
  };

  // 1. Fetch Trainers Directory
  const fetchTrainers = async () => {
    setTrainersLoading(true);
    setTrainersError(null);
    try {
      const params = new URLSearchParams();
      params.append("page", directoryPage);
      params.append("pageSize", 15);
      if (search) params.append("search", search);
      if (tierFilter) params.append("tierId", tierFilter);
      if (statusFilter) params.append("status", statusFilter);

      const res = await adminApi.getTrainers(params.toString());
      setTrainers(res.items || []);
      setTotalTrainers(res.totalCount || 0);
    } catch (err) {
      setTrainersError(err.message || "Failed to load trainers directory.");
    } finally {
      setTrainersLoading(false);
    }
  };

  // 2. Fetch Applications
  const fetchApplications = async () => {
    setAppsLoading(true);
    setAppsError(null);
    try {
      const res = await adminApi.getTrainerApplications();
      setApplications(res.items || res || []);
    } catch (err) {
      setAppsError(err.message || "Failed to load trainer applications.");
    } finally {
      setAppsLoading(false);
    }
  };

  // 3. Fetch Upgrades
  const fetchUpgrades = async () => {
    setUpgradesLoading(true);
    setUpgradesError(null);
    try {
      const res = await adminApi.getTrainerUpgrades();
      setUpgrades(res.items || res || []);
    } catch (err) {
      setUpgradesError(err.message || "Failed to load tier upgrade requests.");
    } finally {
      setUpgradesLoading(false);
    }
  };

  // 4. Fetch Tiers (Cached)
  const fetchTiers = async (force = false) => {
    if (!force && tiers && tiers.length > 0) return;
    setTiersLoading(true);
    try {
      const res = await adminApi.getTrainerTiers();
      setTiers(res || []);
    } catch (err) {
      console.error(err);
    } finally {
      setTiersLoading(false);
    }
  };

  // Initial load of tiers & stats for filters & KPI cards
  useEffect(() => {
    fetchStats();
    fetchTiers();
  }, []);

  useEffect(() => {
    if (activeTab === "directory") fetchTrainers();
    if (activeTab === "applications") fetchApplications();
    if (activeTab === "upgrades") fetchUpgrades();
    if (activeTab === "tiers") fetchTiers();
  }, [activeTab, directoryPage, tierFilter, statusFilter]);

  // Review Application Action
  const handleConfirmReview = async () => {
    if (reviewModalType === "reject" && !reviewReason.trim()) {
      setReviewError("A rejection reason is strictly mandatory.");
      return;
    }
    if (reviewModalType === "changes" && !reviewReason.trim()) {
      setReviewError("Specific requested changes notes are mandatory.");
      return;
    }

    setReviewLoading(true);
    setReviewError(null);
    try {
      if (reviewModalType === "approve") {
        await adminApi.approveTrainerApplication(selectedApp.id, reviewAdminNotes.trim());
      } else if (reviewModalType === "reject") {
        await adminApi.rejectTrainerApplication(selectedApp.id, reviewReason.trim(), reviewAdminNotes.trim());
      } else if (reviewModalType === "changes") {
        await adminApi.requestChangesTrainerApplication(selectedApp.id, reviewReason.trim());
      }

      setReviewModalType(null);
      setSelectedApp(null);
      fetchApplications();
    } catch (err) {
      setReviewError(err.message || "Failed to submit application review decision.");
    } finally {
      setReviewLoading(false);
    }
  };

  // Review Upgrade Action
  const handleConfirmUpgrade = async () => {
    if (upgradeModalType === "reject" && !upgradeReason.trim()) {
      setUpgradeError("A rejection reason is strictly mandatory.");
      return;
    }

    setUpgradeLoading(true);
    setUpgradeError(null);
    try {
      if (upgradeModalType === "approve") {
        await adminApi.approveTrainerUpgrade(selectedUpgrade.id, upgradeReason.trim());
      } else {
        await adminApi.rejectTrainerUpgrade(selectedUpgrade.id, upgradeReason.trim());
      }
      setUpgradeModalType(null);
      setSelectedUpgrade(null);
      fetchUpgrades();
    } catch (err) {
      setUpgradeError(err.message || "Failed to submit tier upgrade decision.");
    } finally {
      setUpgradeLoading(false);
    }
  };

  return (
    <div className="admin-trainers-view">
      <div className="admin-page-header">
        <div>
          <h1 className="admin-page-title">Trainer Command & Governance</h1>
          <p className="admin-page-subtitle">
            Manage studio instructor rosters, review onboarding applications, govern tier promotions, and monitor compliance.
          </p>
        </div>
      </div>

      {/* Summary KPI Banner */}
      <div className="admin-trainers-stats-grid">
        <div className="trainer-stat-card">
          <div className="stat-icon-wrap total">👥</div>
          <div className="stat-info">
            <span className="stat-label">Total Trainers</span>
            <span className="stat-val">{stats ? stats.totalTrainers : totalTrainers}</span>
          </div>
        </div>

        <div className="trainer-stat-card">
          <div className="stat-icon-wrap active">⚡</div>
          <div className="stat-info">
            <span className="stat-label">Active Trainers</span>
            <span className="stat-val">{stats ? stats.activeTrainers : "—"}</span>
          </div>
        </div>

        <div className="trainer-stat-card">
          <div className="stat-icon-wrap pending">📋</div>
          <div className="stat-info">
            <span className="stat-label">Pending Applications</span>
            <span className="stat-val">{stats ? stats.pendingApplications : applications.length}</span>
          </div>
        </div>

        <div className="trainer-stat-card">
          <div className="stat-icon-wrap upgrades">⭐</div>
          <div className="stat-info">
            <span className="stat-label">Upgrade Requests</span>
            <span className="stat-val">{stats ? stats.upgradeRequests : upgrades.length}</span>
          </div>
        </div>
      </div>

      {/* Main Tabs */}
      <div className="admin-tabs-nav">
        <button
          className={`tab-btn ${activeTab === "directory" ? "active" : ""}`}
          onClick={() => setActiveTab("directory")}
        >
          Trainers Directory ({stats ? stats.totalTrainers : totalTrainers})
        </button>
        <button
          className={`tab-btn ${activeTab === "applications" ? "active" : ""}`}
          onClick={() => setActiveTab("applications")}
        >
          Application Review Queue ({stats ? stats.pendingApplications : applications.length})
        </button>
        <button
          className={`tab-btn ${activeTab === "upgrades" ? "active" : ""}`}
          onClick={() => setActiveTab("upgrades")}
        >
          Tier Upgrade Requests ({stats ? stats.upgradeRequests : upgrades.length})
        </button>
        <button
          className={`tab-btn ${activeTab === "tiers" ? "active" : ""}`}
          onClick={() => setActiveTab("tiers")}
        >
          Tiers & Permissions Matrix
        </button>
      </div>

      {/* Tab 1: Directory */}
      {activeTab === "directory" && (
        <div>
          <div className="admin-trainers-filters">
            <form
              onSubmit={(e) => {
                e.preventDefault();
                setDirectoryPage(1);
                fetchTrainers();
              }}
              className="search-form"
            >
              <input
                type="text"
                className="input-field"
                placeholder="Search by trainer name, code, phone, or email..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              <button type="submit" className="btn-secondary">Search</button>
            </form>

            <div className="filter-selects">
              <select
                className="select-field"
                value={tierFilter}
                onChange={(e) => {
                  setTierFilter(e.target.value);
                  setDirectoryPage(1);
                }}
              >
                <option value="">All Tiers</option>
                {tiers.map((tier) => (
                  <option key={tier.id} value={tier.id}>
                    {tier.name}
                  </option>
                ))}
              </select>

              <select
                className="select-field"
                value={statusFilter}
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setDirectoryPage(1);
                }}
              >
                <option value="">All Statuses</option>
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
                <option value="Suspended">Suspended</option>
              </select>

              <AdminExportButton
                data={trainers}
                filename="ethos-trainers-directory"
                moduleName="Trainer Directory"
                allowedRoles={["ADMIN"]}
              />
            </div>
          </div>

          {trainersError && <div className="alert alert-danger">{trainersError}</div>}

          <div className="admin-trainers-table-card">
            {trainersLoading ? (
              <div className="loading-state">Loading trainers...</div>
            ) : trainers.length === 0 ? (
              <div className="empty-state">No instructors match your search criteria.</div>
            ) : (
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Trainer Code</th>
                    <th>Trainer</th>
                    <th>Contact</th>
                    <th>Tier</th>
                    <th>Status</th>
                    <th>City</th>
                    <th>Approved On</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {trainers.map((t) => (
                    <tr key={t.trainerId}>
                      <td className="trainer-code-cell">{t.trainerCode}</td>
                      <td>
                        <div className="trainer-name-cell">{t.fullName}</div>
                        {t.email && <div className="trainer-email-cell">{t.email}</div>}
                      </td>
                      <td className="trainer-contact-cell">{t.phone || "—"}</td>
                      <td>
                        <span className={`tier-badge tier-${(t.tierName || "silver").toLowerCase()}`}>
                          {t.tierName || "Silver"}
                        </span>
                      </td>
                      <td>
                        <span className={`status-pill status-${t.status?.toLowerCase()}`}>
                          {t.status}
                        </span>
                      </td>
                      <td className="trainer-city-cell">{t.city || "—"}</td>
                      <td className="trainer-date-cell">
                        {t.approvedAt ? new Date(t.approvedAt).toLocaleDateString() : "Pending"}
                      </td>
                      <td>
                        <Link
                          to={`/admin_portal/trainers/${t.trainerId}`}
                          className="btn-open-dossier"
                        >
                          Open Dossier →
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* Tab 2: Application Review Queue */}
      {activeTab === "applications" && (
        <div>
          {appsError && <div className="alert alert-danger">{appsError}</div>}

          <div className="admin-trainers-table-card">
            {appsLoading ? (
              <div className="loading-state">Loading candidate applications...</div>
            ) : applications.length === 0 ? (
              <div className="empty-state">No applications currently in queue.</div>
            ) : (
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Application ID</th>
                    <th>Applicant Name</th>
                    <th>Trainer Code</th>
                    <th>Status</th>
                    <th>Submitted At</th>
                    <th>Payment Verified</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {applications.map((app) => (
                    <tr key={app.id}>
                      <td className="trainer-code-cell">{app.id.substring(0, 8)}...</td>
                      <td>
                        <div style={{ fontWeight: 600, color: "#0f172a" }}>
                          {app.fullName || "Candidate"}
                        </div>
                        {app.phone && (
                          <div style={{ fontSize: "0.75rem", color: "#64748b" }}>
                            📱 {app.phone}
                          </div>
                        )}
                      </td>
                      <td className="trainer-code-cell">{app.trainerCode || "—"}</td>
                      <td>
                        <span className={`status-pill status-${app.status?.toLowerCase()}`}>
                          {app.status}
                        </span>
                      </td>
                      <td className="trainer-date-cell">{new Date(app.submittedAt || app.createdAt).toLocaleDateString()}</td>
                      <td>
                        {app.paymentVerifiedAt ? (
                          <span className="badge badge-success">Verified</span>
                        ) : (
                          <span className="badge badge-warning">Unverified</span>
                        )}
                      </td>
                      <td>
                        <div className="action-buttons-cell">
                          <button
                            className="btn btn-sm btn-outline-primary"
                            style={{ fontWeight: 600 }}
                            onClick={() => setDetailsModalApp(app)}
                          >
                            👁️ View Details
                          </button>
                          <button
                            className="btn btn-sm btn-success"
                            onClick={() => {
                              setSelectedApp(app);
                              setReviewModalType("approve");
                              setReviewReason("");
                              setReviewAdminNotes("");
                              setReviewError(null);
                            }}
                          >
                            Approve
                          </button>
                          <button
                            className="btn btn-sm btn-warning"
                            onClick={() => {
                              setSelectedApp(app);
                              setReviewModalType("changes");
                              setReviewReason("");
                              setReviewError(null);
                            }}
                          >
                            Request Changes
                          </button>
                          <button
                            className="btn btn-sm btn-danger"
                            onClick={() => {
                              setSelectedApp(app);
                              setReviewModalType("reject");
                              setReviewReason("");
                              setReviewError(null);
                            }}
                          >
                            Reject
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* Tab 3: Tier Upgrade Requests */}
      {activeTab === "upgrades" && (
        <div>
          {upgradesError && <div className="alert alert-danger">{upgradesError}</div>}

          <div className="admin-trainers-table-card">
            {upgradesLoading ? (
              <div className="loading-state">Loading upgrade requests...</div>
            ) : upgrades.length === 0 ? (
              <div className="empty-state">No pending tier upgrade requests.</div>
            ) : (
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Trainer</th>
                    <th>Current Tier</th>
                    <th>Requested Tier</th>
                    <th>Status</th>
                    <th>Requested Date</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {upgrades.map((u) => (
                    <tr key={u.id}>
                      <td className="trainer-name-cell">{u.trainerName || u.trainerProfileId?.substring(0, 8)}</td>
                      <td>{u.currentTierName || "Current"}</td>
                      <td>
                        <span className="tier-badge tier-gold">
                          {u.requestedTierName || "Target"}
                        </span>
                      </td>
                      <td>
                        <span className={`status-pill status-${u.status?.toString().toLowerCase()}`}>
                          {u.status}
                        </span>
                      </td>
                      <td className="trainer-date-cell">{new Date(u.createdAt).toLocaleDateString()}</td>
                      <td>
                        <div className="action-buttons-cell">
                          <button
                            className="btn btn-sm btn-success"
                            onClick={() => {
                              setSelectedUpgrade(u);
                              setUpgradeModalType("approve");
                              setUpgradeReason("");
                              setUpgradeError(null);
                            }}
                          >
                            Approve Promotion
                          </button>
                          <button
                            className="btn btn-sm btn-danger"
                            onClick={() => {
                              setSelectedUpgrade(u);
                              setUpgradeModalType("reject");
                              setUpgradeReason("");
                              setUpgradeError(null);
                            }}
                          >
                            Reject
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* Tab 4: Tiers Matrix */}
      {activeTab === "tiers" && (
        <div className="admin-trainers-tiers-container">
          <h3 className="section-title">Studio Instructor Tiers & Eligibility Matrix</h3>
          <div className="tiers-cards-grid">
            {tiers.map((tier) => (
              <div key={tier.id} className="tier-overview-card">
                <div className="tier-header">
                  <span className={`tier-badge tier-${tier.name.toLowerCase()}`}>{tier.name}</span>
                  <span className="payout-rate">{tier.baseHourlyRate ? `₹${tier.baseHourlyRate}/hr` : "Custom"}</span>
                </div>
                <p className="tier-desc">{tier.description || "Studio instructor tier level."}</p>
                <div className="tier-criteria">
                  <div className="criteria-item">
                    <span className="label">Min Experience:</span>
                    <span className="val">{tier.minExperienceYears || 0} years</span>
                  </div>
                  <div className="criteria-item">
                    <span className="label">Min Workshops:</span>
                    <span className="val">{tier.minCompletedWorkshops || 0} completed</span>
                  </div>
                  <div className="criteria-item">
                    <span className="label">Min Rating:</span>
                    <span className="val">⭐ {tier.minAverageRating || "4.0"}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Review Application Modal */}
      {reviewModalType && selectedApp && (
        <div className="modal-backdrop">
          <div className="modal-box modal-light">
            <h3>
              {reviewModalType === "approve"
                ? "Approve Trainer Application"
                : reviewModalType === "reject"
                ? "Reject Trainer Application"
                : "Request Application Changes"}
            </h3>
            <p className="modal-desc">
              {reviewModalType === "approve"
                ? "Approving this application will activate the instructor profile, assign the TRAINER role, and establish starting tier privileges."
                : reviewModalType === "reject"
                ? "Rejection requires a mandatory, audited reason that will be communicated to the candidate."
                : "Specify exact changes or document updates the candidate must provide."}
            </p>

            {reviewModalType !== "approve" && (
              <div className="form-group">
                <label className="form-label">
                  {reviewModalType === "reject" ? "Rejection Reason (Mandatory):" : "Changes Requested (Mandatory):"}
                </label>
                <textarea
                  className="textarea-field"
                  rows={3}
                  placeholder="Provide explicit operational reasoning..."
                  value={reviewReason}
                  onChange={(e) => setReviewReason(e.target.value)}
                />
              </div>
            )}

            <div className="form-group">
              <label className="form-label">Internal Administrator Notes (Optional):</label>
              <textarea
                className="textarea-field"
                rows={2}
                placeholder="Notes for the studio administrative team..."
                value={reviewAdminNotes}
                onChange={(e) => setReviewAdminNotes(e.target.value)}
              />
            </div>

            {reviewError && <div className="alert alert-danger mb-3">{reviewError}</div>}

            <div className="modal-actions">
              <button
                className="btn btn-secondary"
                onClick={() => setReviewModalType(null)}
                disabled={reviewLoading}
              >
                Cancel
              </button>
              <button
                className={`btn ${
                  reviewModalType === "approve"
                    ? "btn-success"
                    : reviewModalType === "reject"
                    ? "btn-danger"
                    : "btn-warning"
                }`}
                onClick={handleConfirmReview}
                disabled={reviewLoading}
              >
                {reviewLoading ? "Submitting..." : "Confirm Decision"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Upgrade Request Modal */}
      {upgradeModalType && selectedUpgrade && (
        <div className="modal-backdrop">
          <div className="modal-box modal-light">
            <h3>
              {upgradeModalType === "approve"
                ? "Approve Tier Promotion"
                : "Reject Tier Upgrade Request"}
            </h3>
            <p className="modal-desc">
              Candidate promotion evaluates performance snapshots, workshop attendance, and compliance.
            </p>

            <div className="form-group">
              <label className="form-label">
                {upgradeModalType === "reject" ? "Rejection Reason (Mandatory):" : "Approval Justification:"}
              </label>
              <textarea
                className="textarea-field"
                rows={3}
                placeholder="Provide explicit operational reasoning..."
                value={upgradeReason}
                onChange={(e) => setUpgradeReason(e.target.value)}
              />
            </div>

            {upgradeError && <div className="alert alert-danger mb-3">{upgradeError}</div>}

            <div className="modal-actions">
              <button
                className="btn btn-secondary"
                onClick={() => setUpgradeModalType(null)}
                disabled={upgradeLoading}
              >
                Cancel
              </button>
              <button
                className={`btn ${upgradeModalType === "approve" ? "btn-success" : "btn-danger"}`}
                onClick={handleConfirmUpgrade}
                disabled={upgradeLoading}
              >
                {upgradeLoading ? "Submitting..." : "Confirm Decision"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Full Candidate Application Review Dossier Modal */}
      {detailsModalApp && (
        <AdminTrainerApplicationDetailsModal
          isOpen={Boolean(detailsModalApp)}
          application={detailsModalApp}
          onClose={() => setDetailsModalApp(null)}
          onApplicationUpdated={() => {
            fetchApplications();
            fetchStats();
          }}
        />
      )}
    </div>
  );
}
