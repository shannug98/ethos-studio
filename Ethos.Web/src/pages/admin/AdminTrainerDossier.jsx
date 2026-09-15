import React, { useState, useEffect } from "react";
import { useParams, Link } from "react-router-dom";
import { adminApi } from "../../services/adminApi";
import { getTrainerPermissionMeta } from "../../constants/trainerPermissions";
import "./AdminTrainerDossier.css";

function transformTrainerIssue(issue, dossier, setActiveTab, handleCopyReminder) {
  const code = issue.code || "";

  if (code === "DOCUMENT_NOT_SUBMITTED") {
    return {
      ...issue,
      mappedTitle: "Application verification document missing",
      mappedSeverityLabel: "Compliance Required",
      mappedDescription: issue.description || "A required instructor application or compliance document has not been uploaded.",
      impact: "The trainer profile cannot be fully verified for public workshop listings.",
      nextStep: "Ask the trainer to upload the missing document in their instructor account, or submit it to the studio.",
      actions: [
        {
          label: "View Trainer Profile & Documents",
          variant: "primary",
          onClick: () => setActiveTab("profile"),
        },
        {
          label: "Send Document Reminder",
          variant: "secondary",
          onClick: () =>
            handleCopyReminder(
              code,
              `Hi ${dossier?.fullName || "Trainer"}, please upload your pending verification document to your Ethos Trainer account. Thank you!`
            ),
        },
      ],
    };
  }

  if (code === "DOCUMENT_STORAGE_OBJECT_MISSING") {
    return {
      ...issue,
      mappedTitle: "Document file unavailable in storage",
      mappedSeverityLabel: "System Attention",
      mappedDescription: "A record exists for this document, but the file could not be retrieved from disk storage.",
      impact: "Studio administrators are unable to inspect or verify this document file.",
      nextStep: "Request the trainer to re-upload this document in their trainer portal.",
      actions: [
        {
          label: "View Documents",
          variant: "primary",
          onClick: () => setActiveTab("profile"),
        },
      ],
    };
  }

  if (code === "UNPRICED_WORKSHOPS") {
    return {
      ...issue,
      mappedTitle: "Workshop proposals awaiting pricing review",
      mappedSeverityLabel: "Action Required",
      mappedDescription: issue.description || "Trainer submitted workshop proposals that require admin pricing review.",
      impact: "Workshops cannot be published or opened for student bookings until ticket pricing is approved.",
      nextStep: "Review proposed workshop pricing and approve or set the student ticket price.",
      actions: [
        {
          label: "Review Workshop Catalog",
          variant: "primary",
          onClick: () => setActiveTab("workshops"),
        },
      ],
    };
  }

  if (code === "LOW_TRAINER_RATING") {
    return {
      ...issue,
      mappedTitle: "Student feedback quality alert",
      mappedSeverityLabel: "Quality Review",
      mappedDescription: issue.description || "Average student rating has fallen below the 3.5 studio quality baseline.",
      impact: "Student satisfaction and workshop retention may be negatively affected.",
      nextStep: "Review detailed student feedback comments with the trainer and offer pedagogical support.",
      actions: [
        {
          label: "View Student Feedback",
          variant: "primary",
          onClick: () => setActiveTab("feedback"),
        },
      ],
    };
  }

  if (code === "UPGRADE_PAYMENT_FAILURE") {
    return {
      ...issue,
      mappedTitle: "Tier upgrade payment unsuccessful",
      mappedSeverityLabel: "Payment Issue",
      mappedDescription: "A payment transaction for a tier upgrade failed or was interrupted.",
      impact: "Trainer tier benefits and updated commission rates remain unapplied.",
      nextStep: "Review payment status with the trainer and assist with completing the upgrade.",
      actions: [
        {
          label: "View Trainer Level History",
          variant: "primary",
          onClick: () => setActiveTab("tier-history"),
        },
      ],
    };
  }

  return {
    ...issue,
    mappedTitle: issue.title || "Item Requiring Attention",
    mappedSeverityLabel: issue.severity === "CRITICAL" ? "High Priority" : "Attention Required",
    mappedDescription: issue.description || "An operational item requires review.",
    impact: "May impact workshop scheduling, instructor verification, or student bookings.",
    nextStep: issue.recommendedAction || "Review instructor details and take appropriate operational action.",
    actions: [
      {
        label: "Inspect Trainer Profile",
        variant: "secondary",
        onClick: () => setActiveTab("profile"),
      },
    ],
  };
}

export function formatTrainerLevelName(tier) {
  if (!tier) return "New Trainer";
  const clean = String(tier).trim().replace(/\s*trainer$/i, "");
  const t = clean.toLowerCase();
  if (t === "silver") return "Silver Level Trainer";
  if (t === "gold") return "Gold Level Trainer";
  if (t === "diamond") return "Diamond Level Trainer";
  if (t === "platinum" || t === "platinum master") return "Platinum Master Trainer";
  if (t === "initial") return "New Trainer";
  return `${clean} Level Trainer`;
}

export function mapTrainerLevelChangeReason(rawReason) {
  if (!rawReason) return "Trainer level assigned by studio administrator";
  const r = String(rawReason).trim();
  const lower = r.toLowerCase();

  if (lower.includes("restoring back to original tier") || lower.includes("restored the previous")) {
    return "Administrator restored the previous trainer level";
  }
  if (lower.includes("testing demotion upgrade recovery") || lower.includes("changed the trainer level after review")) {
    return "Administrator changed the trainer level after review";
  }
  if (lower.includes("testing platinum top tier behavior") || lower.includes("promoted to platinum")) {
    return "Trainer promoted to Platinum Master level";
  }
  if (lower.includes("initial application approved") || lower.includes("application approved by admin")) {
    return "Trainer application approved";
  }
  if (lower.includes("tier upgrade approved") || lower.includes("upgrade approved by admin")) {
    return "Trainer level promotion approved by administrator";
  }
  if (lower.includes("performance review promotion")) {
    return "Promoted based on performance review";
  }
  if (lower.includes("special engagement rate")) {
    return "Adjusted for special engagement engagement terms";
  }

  // If reason has "[ADMIN OVERRIDE] ...", clean up the prefix for user-facing display
  const cleaned = r.replace(/^\[ADMIN OVERRIDE\]\s*/i, "").trim();
  return cleaned || "Trainer level updated by administrator";
}

export default function AdminTrainerDossier() {
  const { trainerId } = useParams();
  const [dossier, setDossier] = useState(null);
  const [diagnostics, setDiagnostics] = useState(null);
  const [performance, setPerformance] = useState(null);
  const [tiers, setTiers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState("profile");

  // Tier Modal
  const [tierModalOpen, setTierModalOpen] = useState(false);
  const [selectedTierId, setSelectedTierId] = useState("");
  const [tierReason, setTierReason] = useState("");
  const [tierActionLoading, setTierActionLoading] = useState(false);
  const [tierActionError, setTierActionError] = useState(null);

  // Status Modal
  const [statusModalOpen, setStatusModalOpen] = useState(false);
  const [selectedStatus, setSelectedStatus] = useState("Active");
  const [statusReason, setStatusReason] = useState("");
  const [statusActionLoading, setStatusActionLoading] = useState(false);
  const [statusActionError, setStatusActionError] = useState(null);

  // Permission Change Modal
  const [permModalOpen, setPermModalOpen] = useState(false);
  const [targetPerm, setTargetPerm] = useState(null);
  const [permDecisionType, setPermDecisionType] = useState("restrict"); // "restrict" | "allow" | "role_default"
  const [permReason, setPermReason] = useState("");
  const [permActionLoading, setPermActionLoading] = useState(false);
  const [permActionError, setPermActionError] = useState(null);

  const [copyFeedback, setCopyFeedback] = useState(null);

  const copyToClipboard = (text, key) => {
    if (!text) return;
    navigator.clipboard.writeText(text);
    setCopyFeedback(key || text);
    setTimeout(() => setCopyFeedback(null), 2000);
  };

  const handleCopyReminder = (issueCode, messageText) => {
    navigator.clipboard.writeText(messageText);
    setCopyFeedback(`reminder-${issueCode}`);
    setTimeout(() => setCopyFeedback(null), 2500);
  };

  const fetchDossierData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [dossierRes, diagRes, tiersRes, perfRes] = await Promise.all([
        adminApi.getTrainerById(trainerId),
        adminApi.getTrainerDiagnostics(trainerId),
        adminApi.getTrainerTiers(),
        adminApi.getTrainerPerformance(trainerId).catch((err) => {
          console.warn("Dedicated trainer performance endpoint not available:", err);
          return null;
        }),
      ]);
      setDossier(dossierRes);
      setDiagnostics(diagRes);
      setTiers(tiersRes || []);
      setPerformance(perfRes);
      if (dossierRes?.status) setSelectedStatus(dossierRes.status);
    } catch (err) {
      setError(err.message || "Failed to load instructor dossier.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (trainerId) {
      fetchDossierData();
    }
  }, [trainerId]);

  const handleConfirmTierChange = async () => {
    if (!selectedTierId) {
      setTierActionError("Please select a valid tier.");
      return;
    }
    if (!tierReason.trim()) {
      setTierActionError("A justification reason is mandatory.");
      return;
    }

    setTierActionLoading(true);
    setTierActionError(null);
    try {
      await adminApi.overrideTrainerTier(trainerId, selectedTierId, tierReason.trim());
      setTierModalOpen(false);
      fetchDossierData();
    } catch (err) {
      setTierActionError(err.message || "Failed to update tier.");
    } finally {
      setTierActionLoading(false);
    }
  };

  const handleConfirmStatusChange = async () => {
    if (!statusReason.trim()) {
      setStatusActionError("A justification reason is mandatory.");
      return;
    }

    setStatusActionLoading(true);
    setStatusActionError(null);
    try {
      await adminApi.updateTrainerStatus(trainerId, selectedStatus, statusReason.trim());
      setStatusModalOpen(false);
      fetchDossierData();
    } catch (err) {
      setStatusActionError(err.message || "Failed to update status.");
    } finally {
      setStatusActionLoading(false);
    }
  };

  const handleOpenPermModal = (perm, preferredAction = null) => {
    setTargetPerm(perm);
    const action = preferredAction || (perm.effectiveValue ? "restrict" : "allow");
    setPermDecisionType(action);
    setPermReason("");
    setPermActionError(null);
    setPermModalOpen(true);
  };

  const handleConfirmPermissionChange = async () => {
    if (!permReason.trim()) {
      setPermActionError("A justification reason is mandatory for modifying trainer permissions.");
      return;
    }

    setPermActionLoading(true);
    setPermActionError(null);
    try {
      const code = targetPerm.permissionCode || targetPerm.code;
      if (permDecisionType === "role_default") {
        await adminApi.clearTrainerPermissionOverride(trainerId, code, permReason.trim());
      } else {
        const isAllowed = permDecisionType === "allow";
        await adminApi.overrideTrainerPermission(trainerId, code, isAllowed, permReason.trim());
      }
      setPermModalOpen(false);
      await fetchDossierData();
    } catch (err) {
      setPermActionError(err.message || "Failed to update permission.");
    } finally {
      setPermActionLoading(false);
    }
  };

  if (loading) {
    return <div className="dossier-loading">Loading trainer account and records...</div>;
  }

  if (error || !dossier) {
    return (
      <div className="dossier-error-view">
        <div className="alert alert-danger">{error || "Instructor profile not found."}</div>
        <Link to="/admin_portal/trainers" className="btn btn-secondary mt-3">
          ← Back to Trainers Directory
        </Link>
      </div>
    );
  }

  // Aggregate operational issues
  const rawIssues = [
    ...(diagnostics?.businessIssues || []),
    ...(diagnostics?.technicalFailures || []),
  ];

  const allIssues = rawIssues.map((issue) =>
    transformTrainerIssue(issue, dossier, setActiveTab, handleCopyReminder)
  );
  const totalIssuesCount = allIssues.length;

  return (
    <div className="admin-trainer-dossier-view">
      {/* Breadcrumb */}
      <div className="dossier-breadcrumb">
        <Link to="/admin_portal/trainers" className="back-link">
          ← Back to Trainers Directory
        </Link>
      </div>

      {/* Header Bar */}
      <div className="dossier-header-bar">
        <div className="dossier-identity">
          <div className="avatar-placeholder trainer-avatar">
            {dossier.profilePhotoUrl ? (
              <img src={dossier.profilePhotoUrl} alt={dossier.fullName} />
            ) : (
              <span>{dossier.fullName?.charAt(0) || "T"}</span>
            )}
          </div>
          <div>
            <div className="name-tier-row">
              <h1 className="student-name">{dossier.fullName}</h1>
              <span className={`tier-badge tier-${(dossier.tierName || "silver").toLowerCase()}`}>
                {dossier.tierName || "Silver"}
              </span>
            </div>
            <div className="student-meta">
              <span className="mono-code">{dossier.trainerCode}</span>
              <span className="separator">•</span>
              <span>{dossier.phone}</span>
              <span className="separator">•</span>
              <span className="text-secondary">{dossier.city || "No City Specified"}</span>
            </div>
          </div>
        </div>

        <div className="dossier-header-actions">
          <span className={`status-pill status-${dossier.status?.toLowerCase()}`}>
            {dossier.status}
          </span>
          <button className="btn btn-sm btn-secondary" onClick={() => setTierModalOpen(true)}>
            Change Trainer Level
          </button>
          <button className="btn btn-sm btn-outline" onClick={() => setStatusModalOpen(true)}>
            Change Status
          </button>
        </div>
      </div>

      {/* Two-Level Information Architecture: Trainer Overview & Attention Items */}
      <div className="account-overview-card">
        <div className="overview-header-row">
          <div>
            <h2 className="overview-card-title">Trainer Account Overview</h2>
            <p className="overview-card-subtitle">
              Overall account standing, trainer level, performance metrics, and items requiring attention.
            </p>
          </div>
          <div className="overview-health-badge-wrap">
            {totalIssuesCount === 0 ? (
              <span className="health-badge healthy">
                ✓ Account Health: No Issues Detected
              </span>
            ) : (
              <span className="health-badge attention">
                ⚠️ Items Requiring Attention ({totalIssuesCount})
              </span>
            )}
          </div>
        </div>

        {/* Operational Metric KPI Strip */}
        <div className="overview-kpis-strip">
          <div className="kpi-box">
            <span className="kpi-label">Account Status</span>
            <span className={`kpi-badge ${dossier.status === "Active" ? "kpi-active" : "kpi-inactive"}`}>
              {dossier.status || "Active"}
            </span>
          </div>
          <div className="kpi-box">
            <span className="kpi-label">Trainer Level</span>
            <span className="kpi-number" style={{ fontSize: "15px", textTransform: "capitalize" }}>
              ⭐ {formatTrainerLevelName(dossier.tierName)}
            </span>
          </div>
          <div className="kpi-box">
            <span className="kpi-label">Average Student Rating</span>
            <span className="kpi-number">
              ⭐ {performance?.overall?.rating ? Number(performance.overall.rating).toFixed(1) : (diagnostics?.averageRating ?? "5.0")} / 5.0
            </span>
            <span className="kpi-sub">
              ({performance?.overall?.feedback ?? diagnostics?.totalFeedbackCount ?? dossier.feedback?.length ?? 0} reviews)
            </span>
          </div>
          <div className="kpi-box">
            <span className="kpi-label">Active Workshops</span>
            <span className="kpi-number">{diagnostics?.activeWorkshopsCount ?? dossier.workshops?.length ?? 0}</span>
          </div>
          <div className="kpi-box">
            <span className="kpi-label">Pending Proposals</span>
            <span className={`kpi-number ${(diagnostics?.unpricedWorkshopsCount || 0) > 0 ? "highlight-attn" : ""}`}>
              {diagnostics?.unpricedWorkshopsCount ?? 0}
            </span>
          </div>
          <div className={`kpi-box ${totalIssuesCount > 0 ? "attention" : ""}`}>
            <span className="kpi-label">Items Requiring Attention</span>
            <span className={`kpi-number ${totalIssuesCount > 0 ? "highlight-attn" : ""}`}>
              {totalIssuesCount}
            </span>
          </div>
        </div>

        {/* Level 1: Main View - Items Requiring Attention */}
        {totalIssuesCount > 0 ? (
          <div className="attention-required-section">
            <div className="attention-section-header">
              <h3>⚠️ Attention Required ({totalIssuesCount})</h3>
              <p>The following items need review or operational follow-up from the studio administrator.</p>
            </div>
            <div className="attention-items-list">
              {allIssues.map((issue, idx) => (
                <div key={idx} className={`attention-card sev-${(issue.severity || "warning").toLowerCase()}`}>
                  <div className="attention-card-header">
                    <div className="attention-title-col">
                      <span className="attention-icon">
                        {issue.severity === "CRITICAL" ? "🚨" : issue.severity === "WARNING" ? "⚠️" : "ℹ️"}
                      </span>
                      <h4>{issue.mappedTitle}</h4>
                    </div>
                    <span className={`attention-severity-badge ${(issue.severity || "warning").toLowerCase()}`}>
                      {issue.mappedSeverityLabel}
                    </span>
                  </div>

                  <p className="attention-description">{issue.mappedDescription}</p>

                  <div className="attention-explanation-grid">
                    <div className="explanation-block impact">
                      <strong className="block-title">Why this matters:</strong>
                      <p>{issue.impact}</p>
                    </div>
                    <div className="explanation-block next-step">
                      <strong className="block-title">Recommended next step:</strong>
                      <p>{issue.nextStep}</p>
                    </div>
                  </div>

                  {/* Direct Operational Action Buttons */}
                  <div className="attention-actions-bar">
                    {issue.actions?.map((act, actIdx) => (
                      <button
                        key={actIdx}
                        type="button"
                        className={`admin-action-btn ${act.variant || "secondary"}`}
                        onClick={act.onClick}
                      >
                        {act.label}
                      </button>
                    ))}
                    {copyFeedback === `reminder-${issue.code}` && (
                      <span className="reminder-copied-toast">✓ Reminder message copied to clipboard!</span>
                    )}
                  </div>

                  {/* Level 2: Advanced Technical Details (Expandable) */}
                  <details className="advanced-technical-details">
                    <summary className="advanced-summary">
                      <span className="summary-chevron">▸</span> Advanced Technical Details
                    </summary>
                    <div className="advanced-technical-content">
                      <div className="tech-meta-row">
                        <div><strong>Internal issue code:</strong> <code>{issue.code}</code></div>
                        <div><strong>System category:</strong> <code>{issue.category}</code></div>
                        <div><strong>Raw severity:</strong> <code>{issue.severity}</code></div>
                      </div>
                      <div className="tech-meta-row">
                        <div>
                          <strong>Support Audit Reference:</strong>{" "}
                          <code className="mono-code">{issue.traceId || diagnostics?.traceId || "N/A"}</code>
                          {(issue.traceId || diagnostics?.traceId) && (
                            <button
                              type="button"
                              className="btn-copy-mini"
                              onClick={() => copyToClipboard(issue.traceId || diagnostics?.traceId, `trace-${idx}`)}
                            >
                              {copyFeedback === `trace-${idx}` ? "✓ Copied" : "Copy Reference"}
                            </button>
                          )}
                        </div>
                        <div>
                          <strong>Detected at:</strong>{" "}
                          {issue.detectedAt || diagnostics?.generatedAt
                            ? new Date(issue.detectedAt || diagnostics?.generatedAt).toLocaleString()
                            : "Recent"}
                        </div>
                      </div>
                      {issue.evidence && (
                        <div className="tech-evidence-block">
                          <strong>Structured Evidence:</strong>
                          <pre className="evidence-json">{JSON.stringify(issue.evidence, null, 2)}</pre>
                        </div>
                      )}
                    </div>
                  </details>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <div className="account-healthy-banner">
            <div className="healthy-icon">✓</div>
            <div className="healthy-text">
              <h4>Account in Good Standing</h4>
              <p>No compliance discrepancies, pending proposals, or low rating alerts detected for this instructor.</p>
            </div>
          </div>
        )}
      </div>

      {/* Tabs */}
      <div className="dossier-tabs-nav">
        <button
          className={`tab-btn ${activeTab === "profile" ? "active" : ""}`}
          onClick={() => setActiveTab("profile")}
        >
          Trainer Profile & Styles
        </button>
        <button
          className={`tab-btn ${activeTab === "tier-history" ? "active" : ""}`}
          onClick={() => setActiveTab("tier-history")}
        >
          Trainer Level History ({dossier.tierHistory?.length || 0})
        </button>
        <button
          className={`tab-btn ${activeTab === "permissions" ? "active" : ""}`}
          onClick={() => setActiveTab("permissions")}
        >
          Access & Permissions ({dossier.permissions?.length || 0})
        </button>
        <button
          className={`tab-btn ${activeTab === "workshops" ? "active" : ""}`}
          onClick={() => setActiveTab("workshops")}
        >
          Workshop History ({dossier.workshops?.length || 0})
        </button>
        <button
          className={`tab-btn ${activeTab === "feedback" ? "active" : ""}`}
          onClick={() => setActiveTab("feedback")}
        >
          Student Reviews & Feedback ({dossier.feedback?.length || 0})
        </button>
      </div>

      {/* Tab 1: Profile */}
      {activeTab === "profile" && (
        <div className="tab-pane">
          <h3 className="section-title">Trainer Credentials & Portfolio</h3>
          <div className="profile-grid">
            <div>
              <span className="label">Primary Dance Style</span>
              <span className="val font-semibold">{dossier.primaryDanceStyle || "—"}</span>
            </div>
            <div>
              <span className="label">Secondary Styles</span>
              <span className="val">{dossier.secondaryDanceStyles || "—"}</span>
            </div>
            <div>
              <span className="label">Experience</span>
              <span className="val">{dossier.experienceYears ? `${dossier.experienceYears} Years` : "—"}</span>
            </div>
            <div>
              <span className="label">Affiliated Studio</span>
              <span className="val">{dossier.currentStudio || "Independent"}</span>
            </div>
            <div>
              <span className="label">Instagram</span>
              <span className="val">
                {dossier.instagramUrl ? (
                  <a href={dossier.instagramUrl} target="_blank" rel="noreferrer" className="external-link">
                    {dossier.instagramUrl}
                  </a>
                ) : (
                  "—"
                )}
              </span>
            </div>
            <div>
              <span className="label">YouTube</span>
              <span className="val">
                {dossier.youtubeUrl ? (
                  <a href={dossier.youtubeUrl} target="_blank" rel="noreferrer" className="external-link">
                    {dossier.youtubeUrl}
                  </a>
                ) : (
                  "—"
                )}
              </span>
            </div>
          </div>

          {dossier.bio && (
            <div className="mt-4">
              <span className="label">Instructor Biography</span>
              <p className="bio-text">{dossier.bio}</p>
            </div>
          )}
        </div>
      )}

      {/* Tab 2: Trainer Level History */}
      {activeTab === "tier-history" && (
        <div className="tab-pane">
          <div className="section-header-row mb-4">
            <div>
              <h3 className="section-title">Trainer Level Change History</h3>
              <p className="section-subtitle">
                Complete audit trail of trainer level promotions, restorations, and administrative assignments.
              </p>
            </div>
          </div>
          {dossier.tierHistory?.length === 0 ? (
            <div className="empty-state">No historical level changes recorded for this trainer.</div>
          ) : (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Level Change</th>
                  <th>Reason for Change</th>
                  <th>Date</th>
                </tr>
              </thead>
              <tbody>
                {dossier.tierHistory.map((th) => {
                  const prevLevel = formatTrainerLevelName(th.previousTier);
                  const newLevel = formatTrainerLevelName(th.newTier);
                  const businessReason = mapTrainerLevelChangeReason(th.reason);

                  return (
                    <tr key={th.id}>
                      <td style={{ verticalAlign: "top" }}>
                        <div className="level-transition-cell">
                          <span className="level-transition-text">
                            <span className="prev-level">{prevLevel}</span>
                            <span className="arrow-sep">→</span>
                            <strong className="new-level">{newLevel}</strong>
                          </span>
                        </div>
                      </td>
                      <td className="text-secondary" style={{ verticalAlign: "top" }}>
                        <div className="reason-primary-text font-semibold text-primary">
                          {businessReason}
                        </div>
                        <details className="advanced-technical-details mt-2">
                          <summary className="advanced-summary">
                            <span className="summary-chevron">▶</span> Technical & Audit Details
                          </summary>
                          <div className="advanced-technical-content">
                            <div className="tech-meta-row">
                              <span>
                                Raw Reason String: <code>{th.reason || "N/A"}</code>
                              </span>
                            </div>
                            <div className="tech-meta-row">
                              <span>
                                History Record ID: <code>{th.id}</code>
                              </span>
                              <button
                                type="button"
                                className="btn-copy-mini"
                                onClick={() => copyToClipboard(th.id, `tier-${th.id}`)}
                              >
                                {copyFeedback === `tier-${th.id}` ? "✓ Copied" : "Copy ID"}
                              </button>
                            </div>
                            <div className="tech-meta-row">
                              <span>
                                Exact Timestamp: <code>{new Date(th.changedAt).toISOString()}</code>
                              </span>
                              <span>
                                Previous Level Code: <code>{th.previousTier || "null (Initial Application)"}</code>
                              </span>
                              <span>
                                Target Level Code: <code>{th.newTier}</code>
                              </span>
                            </div>
                          </div>
                        </details>
                      </td>
                      <td style={{ verticalAlign: "top", whiteSpace: "nowrap" }}>
                        {new Date(th.changedAt).toLocaleString()}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Tab 3: Permissions Matrix */}
      {activeTab === "permissions" && (() => {
        const perms = dossier.permissions || [];
        const totalPerms = perms.length;
        const allowedByRole = perms.filter((p) => p.roleDefault).length;
        const restrictedByRole = perms.filter((p) => !p.roleDefault).length;
        const overrideCount = perms.filter((p) => p.override && p.override !== "None").length;

        return (
          <div className="tab-pane trainer-permissions-pane">
            <div className="section-header-row">
              <div>
                <h3 className="section-title">Trainer Access & Permissions</h3>
                <p className="section-subtitle">
                  Control which actions this trainer can perform in the trainer portal. Changes made here override the normal access provided by the trainer level.
                </p>
              </div>
            </div>

            {/* 4-Chip Metric Summary */}
            <div className="permissions-summary-bar">
              <span className="summary-chip total">
                <strong>{totalPerms}</strong> Permissions
              </span>
              <span className="summary-chip allowed-role">
                <strong>{allowedByRole}</strong> Allowed by Role
              </span>
              <span className="summary-chip restricted-role">
                <strong>{restrictedByRole}</strong> Restricted by Role
              </span>
              <span className="summary-chip admin-override">
                <strong>{overrideCount}</strong> Admin Overrides
              </span>
            </div>

            <table className="admin-table permissions-table">
              <thead>
                <tr>
                  <th style={{ width: "38%" }}>Permission</th>
                  <th style={{ width: "16%" }}>Default Access</th>
                  <th style={{ width: "18%" }}>Admin Decision</th>
                  <th style={{ width: "14%" }}>Active Access Level</th>
                  <th style={{ width: "14%", textAlign: "right" }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {perms.map((p) => {
                  const meta = getTrainerPermissionMeta(p.permissionCode);
                  const hasOverride = p.override && p.override !== "None";
                  return (
                    <tr key={p.permissionCode} className="permission-row">
                      <td>
                        <div className="permission-info-cell">
                          <span className="permission-display-name">{meta.name}</span>
                          <span className="permission-description">{meta.description}</span>

                          {/* Collapsed Technical Details Drawer */}
                          <details className="advanced-technical-details">
                            <summary className="advanced-summary">
                              <span className="summary-chevron">▸</span> Advanced technical details
                            </summary>
                            <div className="advanced-technical-content">
                              <div className="tech-meta-row">
                                <div><strong>Permission code:</strong> <code>{p.permissionCode}</code></div>
                                <div><strong>Role code:</strong> <code>TRAINER</code></div>
                                <div><strong>Permission ID:</strong> <code>{p.permissionId || p.permissionCode}</code></div>
                                <div><strong>Default value:</strong> <code>{p.roleDefault ? "Allowed" : "Restricted"}</code></div>
                                <div><strong>Override value:</strong> <code>{p.override}</code></div>
                                <div><strong>Effective value:</strong> <code>{p.effectiveValue ? "Permitted" : "Restricted"}</code></div>
                                <div><strong>Override reason:</strong> <code>{p.overrideReason || "Default trainer level policy"}</code></div>
                                <div><strong>Last modified:</strong> <code>{p.overrideModifiedAt ? new Date(p.overrideModifiedAt).toLocaleString() : "None"}</code></div>
                                <div><strong>Audit reference:</strong> <code>{hasOverride ? "Administrative override" : "Role baseline"}</code></div>
                              </div>
                            </div>
                          </details>
                        </div>
                      </td>
                      <td>
                        <span className={`badge ${p.roleDefault ? "badge-soft-success" : "badge-secondary"}`}>
                          {p.roleDefault ? "Allowed by role" : "Restricted by role"}
                        </span>
                      </td>
                      <td>
                        {!hasOverride ? (
                          <span className="admin-decision-none">No admin decision</span>
                        ) : p.override === "Allowed" ? (
                          <span className="badge badge-success">Allowed by administrator</span>
                        ) : (
                          <span className="badge badge-danger">Restricted by administrator</span>
                        )}
                      </td>
                      <td>
                        <span className={`status-pill ${p.effectiveValue ? "status-active" : "status-inactive"}`}>
                          {p.effectiveValue ? "Access granted" : "Access restricted"}
                        </span>
                      </td>
                      <td style={{ textAlign: "right" }}>
                        <button
                          type="button"
                          className={`btn btn-sm ${p.effectiveValue ? "btn-outline-danger" : "btn-outline-success"}`}
                          onClick={() => handleOpenPermModal(p, p.effectiveValue ? "restrict" : "allow")}
                        >
                          {p.effectiveValue ? "Restrict Access" : "Allow Access"}
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        );
      })()}

      {/* Tab 4: Workshop Catalog */}
      {activeTab === "workshops" && (
        <div className="tab-pane">
          <h3 className="section-title">Workshop History & Proposals</h3>
          {dossier.workshops?.length === 0 ? (
            <div className="empty-state">No workshops created by this instructor.</div>
          ) : (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Title</th>
                  <th>Date & Time</th>
                  <th>Proposed Price</th>
                  <th>Approved Price</th>
                  <th>Bookings</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {dossier.workshops.map((w) => (
                  <tr key={w.id}>
                    <td className="font-semibold">{w.title}</td>
                    <td>{new Date(w.workshopDate).toLocaleDateString()} {w.startTime}</td>
                    <td>₹{w.trainerProposedPrice ?? "—"}</td>
                    <td className="font-semibold">{w.adminApprovedPrice ? `₹${w.adminApprovedPrice}` : "Awaiting Approval"}</td>
                    <td>{w.bookedCount} / {w.capacity}</td>
                    <td>
                      <span className={`status-pill status-${w.status?.toLowerCase()}`}>
                        {w.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Tab 5: Feedback */}
      {activeTab === "feedback" && (() => {
        const feedbackList = dossier.feedback || [];
        const totalReviews = performance?.overall?.feedback ?? feedbackList.length;
        const avgRating = performance?.overall?.rating
          ? Number(performance.overall.rating).toFixed(1)
          : (totalReviews > 0
            ? (feedbackList.reduce((acc, curr) => acc + (curr.rating || 0), 0) / totalReviews).toFixed(1)
            : (dossier.performance?.averageRating ? Number(dossier.performance.averageRating).toFixed(1) : "0.0"));

        const ratingCounts = {
          5: performance?.ratingBreakdown?.fiveStars ?? feedbackList.filter((f) => f.rating === 5).length,
          4: performance?.ratingBreakdown?.fourStars ?? feedbackList.filter((f) => f.rating === 4).length,
          3: performance?.ratingBreakdown?.threeStars ?? feedbackList.filter((f) => f.rating === 3).length,
          2: performance?.ratingBreakdown?.twoStars ?? feedbackList.filter((f) => f.rating === 2).length,
          1: performance?.ratingBreakdown?.oneStar ?? feedbackList.filter((f) => f.rating === 1).length,
        };

        return (
          <div className="tab-pane trainer-feedback-pane">
            <h3 className="section-title">Student Reviews & Feedback</h3>

            {/* Aggregate Overview Card */}
            <div className="feedback-summary-panel">
              <div className="feedback-score-box">
                <div className="score-big">{avgRating}</div>
                <div className="score-stars">
                  {"★".repeat(Math.min(5, Math.max(0, Math.round(Number(avgRating)))))}
                  {"☆".repeat(Math.max(0, 5 - Math.min(5, Math.max(0, Math.round(Number(avgRating))))))}
                </div>
                <div className="score-sub">
                  Based on {totalReviews} attendee review{totalReviews !== 1 ? "s" : ""}
                </div>
              </div>

              <div className="feedback-distribution-bars">
                {[5, 4, 3, 2, 1].map((stars) => {
                  const count = ratingCounts[stars];
                  const pct = totalReviews > 0 ? Math.round((count / totalReviews) * 100) : 0;
                  return (
                    <div key={stars} className="dist-row">
                      <span className="dist-star-label">{stars} ★</span>
                      <div className="dist-bar-track">
                        <div className="dist-bar-fill" style={{ width: `${pct}%` }} />
                      </div>
                      <span className="dist-count-label">
                        {count} ({pct}%)
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Attendee Submissions List */}
            <h4 className="reviews-subheading">
              Attendee Submissions ({totalReviews})
            </h4>

            {totalReviews === 0 ? (
              <div className="empty-state">
                No student or guest reviews recorded yet for this instructor.
              </div>
            ) : (
              <div className="feedback-cards-grid">
                {feedbackList.map((f) => (
                  <div key={f.id} className="feedback-dossier-card">
                    <div className="feedback-dossier-header">
                      <div className="feedback-author-meta">
                        <span className="feedback-author-name">
                          {f.studentName || "Guest Attendee"}
                        </span>
                        <span
                          className={`feedback-badge ${
                            f.studentName && f.studentName !== "Guest Attendee"
                              ? "badge-student"
                              : "badge-guest"
                          }`}
                        >
                          {f.studentName && f.studentName !== "Guest Attendee"
                            ? "Enrolled Student"
                            : "Guest Attendee"}
                        </span>
                        {f.bookingReference && (
                          <span className="feedback-booking-ref">
                            Ref: #{f.bookingReference}
                          </span>
                        )}
                      </div>
                      <div className="feedback-rating-badge">
                        <span className="stars-gold">
                          {"★".repeat(f.rating || 0)}
                          {"☆".repeat(Math.max(0, 5 - (f.rating || 0)))}
                        </span>
                        <span className="rating-num">{(f.rating || 0).toFixed(1)}</span>
                      </div>
                    </div>

                    <div className="feedback-workshop-info">
                      <span className="workshop-pill-tag">Workshop</span>
                      <strong className="workshop-title-text">
                        {f.workshopTitle || "Dance Workshop"}
                      </strong>
                      {f.workshopDate && (
                        <span className="workshop-date-text">
                          • {new Date(f.workshopDate).toLocaleDateString()}
                        </span>
                      )}
                    </div>

                    {f.comment ? (
                      <p className="feedback-comment-quote">"{f.comment}"</p>
                    ) : (
                      <p className="feedback-comment-empty">No written comment provided.</p>
                    )}

                    <div className="feedback-footer-meta">
                      <span>Submitted: {new Date(f.createdAt).toLocaleString()}</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        );
      })()}

      {/* Trainer Level Modal */}
      {tierModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-box">
            <h3>Change Trainer Level</h3>
            <p className="modal-desc">
              Adjusting the trainer level updates default permissions, booking tiers, and default instructor payout rates.
            </p>

            <div className="form-group">
              <label className="form-label">Select Trainer Level:</label>
              <select
                className="select-field"
                value={selectedTierId}
                onChange={(e) => setSelectedTierId(e.target.value)}
              >
                <option value="">-- Select Trainer Level --</option>
                {tiers.map((tr) => (
                  <option key={tr.id} value={tr.id}>
                    {formatTrainerLevelName(tr.name)} ({tr.baseHourlyRate ? `₹${tr.baseHourlyRate}/hr base rate` : "Studio standard"})
                  </option>
                ))}
              </select>
            </div>

            <div className="form-group">
              <label className="form-label">Reason for Change (Required for audit history):</label>
              <textarea
                className="textarea-field"
                rows={3}
                placeholder="e.g., Promotion following quarterly performance review, special engagement rate adjustment..."
                value={tierReason}
                onChange={(e) => setTierReason(e.target.value)}
              />
            </div>

            {tierActionError && <div className="alert alert-danger mb-3">{tierActionError}</div>}

            <div className="modal-actions">
              <button className="btn btn-secondary" onClick={() => setTierModalOpen(false)} disabled={tierActionLoading}>
                Cancel
              </button>
              <button className="btn btn-primary" onClick={handleConfirmTierChange} disabled={tierActionLoading || !selectedTierId}>
                {tierActionLoading ? "Saving Level Change..." : "Save Level Change"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Status Modal */}
      {statusModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-box">
            <h3>Update Instructor Status</h3>
            <p className="modal-desc">
              Change instructor status between Active, Inactive, and Suspended.
            </p>

            <div className="form-group">
              <label className="form-label">Select Status:</label>
              <select
                className="select-field"
                value={selectedStatus}
                onChange={(e) => setSelectedStatus(e.target.value)}
              >
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
                <option value="Suspended">Suspended</option>
              </select>
            </div>

            <div className="form-group">
              <label className="form-label">Justification Reason (Mandatory):</label>
              <textarea
                className="textarea-field"
                rows={3}
                placeholder="e.g., Sabbatical, contract termination, conduct review..."
                value={statusReason}
                onChange={(e) => setStatusReason(e.target.value)}
              />
            </div>

            {statusActionError && <div className="alert alert-danger mb-3">{statusActionError}</div>}

            <div className="modal-actions">
              <button className="btn btn-secondary" onClick={() => setStatusModalOpen(false)} disabled={statusActionLoading}>
                Cancel
              </button>
              <button className="btn btn-primary" onClick={handleConfirmStatusChange} disabled={statusActionLoading}>
                {statusActionLoading ? "Updating..." : "Save Status"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Change Trainer Access Modal */}
      {permModalOpen && targetPerm && (() => {
        const meta = getTrainerPermissionMeta(targetPerm.permissionCode);
        const hasOverride = targetPerm.override && targetPerm.override !== "None";

        return (
          <div className="modal-backdrop">
            <div className="modal-box perm-access-modal">
              <div className="modal-header-block">
                <h3>Change Trainer Access</h3>
                <p className="modal-desc">
                  Modify the operational access permissions for this instructor in the trainer portal.
                </p>
              </div>

              <div className="perm-modal-details-card">
                <div className="perm-modal-row">
                  <span className="perm-modal-label">Permission:</span>
                  <span className="perm-modal-val font-bold">{meta.name}</span>
                </div>
                <div className="perm-modal-row">
                  <span className="perm-modal-label">What this controls:</span>
                  <span className="perm-modal-val">{meta.description}</span>
                </div>
                <div className="perm-modal-row">
                  <span className="perm-modal-label">Active Access Level:</span>
                  <span className="perm-modal-val">
                    {!hasOverride ? (
                      <span className="badge badge-secondary">
                        {targetPerm.roleDefault ? "Allowed by role" : "Restricted by role"}
                      </span>
                    ) : targetPerm.override === "Allowed" ? (
                      <span className="badge badge-success">Allowed by administrator</span>
                    ) : (
                      <span className="badge badge-danger">Restricted by administrator</span>
                    )}
                  </span>
                </div>
              </div>

              <div className="form-group mt-3">
                <label className="form-label font-semibold">Choose the new access:</label>
                <div className="perm-choice-list" role="radiogroup" aria-label="Choose the new access">
                  <label className={`perm-choice-card ${permDecisionType === "restrict" ? "selected restrict" : ""}`}>
                    <input
                      type="radio"
                      name="permDecision"
                      value="restrict"
                      checked={permDecisionType === "restrict"}
                      onChange={() => setPermDecisionType("restrict")}
                    />
                    <div className="choice-text">
                      <span className="choice-title">Restrict this action</span>
                      <span className="choice-sub">Explicitly block this instructor from performing this action</span>
                    </div>
                  </label>

                  <label className={`perm-choice-card ${permDecisionType === "allow" ? "selected allow" : ""}`}>
                    <input
                      type="radio"
                      name="permDecision"
                      value="allow"
                      checked={permDecisionType === "allow"}
                      onChange={() => setPermDecisionType("allow")}
                    />
                    <div className="choice-text">
                      <span className="choice-title">Allow this action</span>
                      <span className="choice-sub">Explicitly grant access regardless of trainer level defaults</span>
                    </div>
                  </label>

                  {hasOverride && (
                    <label className={`perm-choice-card ${permDecisionType === "role_default" ? "selected default" : ""}`}>
                      <input
                        type="radio"
                        name="permDecision"
                        value="role_default"
                        checked={permDecisionType === "role_default"}
                        onChange={() => setPermDecisionType("role_default")}
                      />
                      <div className="choice-text">
                        <span className="choice-title">Use Standard Trainer Policy</span>
                        <span className="choice-sub">
                          Remove custom administrator override and restore {targetPerm.roleDefault ? "allowed" : "restricted"} standard policy default
                        </span>
                      </div>
                    </label>
                  )}
                </div>
              </div>

              {/* Operational Consequence Advisory Callout */}
              <div className={`perm-advisory-box advisory-${permDecisionType}`}>
                <div className="advisory-title">
                  <span className="advisory-icon">
                    {permDecisionType === "restrict" && "⚠️"}
                    {permDecisionType === "allow" && "ℹ️"}
                    {permDecisionType === "role_default" && "🔄"}
                  </span>
                  <span className="advisory-heading">
                    {permDecisionType === "restrict" && "Operational Effect of Restricting"}
                    {permDecisionType === "allow" && "Operational Effect of Allowing"}
                    {permDecisionType === "role_default" && "Operational Effect of Restoring Role Access"}
                  </span>
                </div>
                <div className="advisory-body">
                  {permDecisionType === "restrict" &&
                    "This will prevent the trainer from performing this action in the trainer portal. Existing workshops, bookings, payments, and historical records will not be deleted."}
                  {permDecisionType === "allow" &&
                    "This will allow the trainer to perform this action, subject to the normal trainer role and trainer level rules."}
                  {permDecisionType === "role_default" &&
                    "This removes the administrator override. Access will be recalculated from the trainer's normal role and trainer level policy."}
                </div>
              </div>

              <div className="form-group mt-3">
                <label className="form-label font-semibold">
                  Reason for change <span className="text-danger">*</span> (Required for audit history):
                </label>
                <textarea
                  className="textarea-field"
                  rows={3}
                  placeholder="Explain why this permission is being changed (e.g., Seasonal workshop approval, policy conduct review, certification verification)..."
                  value={permReason}
                  onChange={(e) => setPermReason(e.target.value)}
                  required
                />
              </div>

              {permActionError && <div className="alert alert-danger mb-3">{permActionError}</div>}

              <div className="modal-actions">
                <button
                  className="btn btn-secondary"
                  onClick={() => setPermModalOpen(false)}
                  disabled={permActionLoading}
                >
                  Cancel
                </button>
                <button
                  className="btn btn-primary"
                  onClick={handleConfirmPermissionChange}
                  disabled={permActionLoading}
                >
                  {permActionLoading ? "Saving Change..." : "Save Access Change"}
                </button>
              </div>
            </div>
          </div>
        );
      })()}
    </div>
  );
}
