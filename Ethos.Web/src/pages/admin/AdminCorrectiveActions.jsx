import React, { useState, useEffect, useCallback } from "react";
import { adminApi, getLastTraceId } from "../../services/adminApi";
import AdminKpiCard from "../../components/admin/common/AdminKpiCard";
import AdminBadge from "../../components/admin/common/AdminBadge";
import AdminDataTable from "../../components/admin/common/AdminDataTable";
import AdminActionModal from "../../components/admin/common/AdminActionModal";
import AdminExportButton from "../../components/admin/common/AdminExportButton";
import "./AdminCorrectiveActions.css";

const CANONICAL_ACTIONS = [
  { type: "RESTORE_PACKAGE_QUOTA", label: "Restore Package Quota", entity: "PACKAGE", desc: "Refunds dance credits into a student package" },
  { type: "RECONCILE_PAYMENT_GATEWAY", label: "Reconcile Payment Gateway", entity: "PAYMENT", desc: "Syncs payment transaction state with gateway events" },
  { type: "RECALCULATE_TRAINER_SNAPSHOT", label: "Recalculate Trainer Snapshot", entity: "TRAINER", desc: "Recalculates performance metrics and tier thresholds" },
  { type: "TERMINATE_CORRUPTED_SESSION", label: "Terminate Corrupted Session", entity: "SESSION", desc: "Forces immediate deactivation of an anomalous admin session" },
  { type: "RETRY_COMMUNICATION", label: "Retry Communication", entity: "COMMUNICATION", desc: "Re-dispatches a failed notification with governed retries" },
  { type: "OVERRIDE_BOOKING_STATUS", label: "Override Booking Status", entity: "BOOKING", desc: "Administrative override of a stuck booking reservation" },
];

export default function AdminCorrectiveActions() {
  const [actions, setActions] = useState([]);
  const [metrics, setMetrics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [page, setPage] = useState(1);
  const [totalItems, setTotalItems] = useState(0);
  const [filterType, setFilterType] = useState("");
  const [filterStatus, setFilterStatus] = useState("");

  // Wizard state
  const [wizardOpen, setWizardOpen] = useState(false);
  const [wizardStep, setWizardStep] = useState(1); // 1: Configure, 2: Dry Run Diff, 3: Executing/Complete
  const [selectedActionType, setSelectedActionType] = useState(CANONICAL_ACTIONS[0].type);
  const [targetEntityId, setTargetEntityId] = useState("");
  const [incidentId, setIncidentId] = useState("");
  const [justification, setJustification] = useState("");
  const [parametersJson, setParametersJson] = useState("");
  const [idempotencyKey, setIdempotencyKey] = useState("");

  // Dry run simulation response
  const [simulationResult, setSimulationResult] = useState(null);
  const [simulating, setSimulating] = useState(false);
  const [executing, setExecuting] = useState(false);
  const [executionResult, setExecutionResult] = useState(null);

  // Detail modal
  const [detailModalItem, setDetailModalItem] = useState(null);

  const fetchMetrics = useCallback(async () => {
    try {
      const res = await adminApi.getCorrectiveActionMetrics();
      if (res?.data) setMetrics(res.data);
    } catch {
      // Keep existing metrics if unavailable
    }
  }, []);

  const fetchActions = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: "15",
      });
      if (filterType) params.append("actionType", filterType);
      if (filterStatus) params.append("status", filterStatus);

      const res = await adminApi.getCorrectiveActions(params.toString());
      if (res?.data) {
        setActions(res.data.items || []);
        setTotalItems(res.data.totalCount || 0);
      }
    } catch (err) {
      setError(err.message || "Failed to load corrective actions ledger.");
    } finally {
      setLoading(false);
    }
  }, [page, filterType, filterStatus]);

  useEffect(() => {
    fetchMetrics();
    fetchActions();
  }, [fetchMetrics, fetchActions]);

  const openNewWizard = () => {
    setSelectedActionType(CANONICAL_ACTIONS[0].type);
    setTargetEntityId("");
    setIncidentId("");
    setJustification("");
    setParametersJson("");
    setIdempotencyKey(crypto.randomUUID ? crypto.randomUUID() : `idemp-${Date.now()}`);
    setSimulationResult(null);
    setExecutionResult(null);
    setWizardStep(1);
    setWizardOpen(true);
  };

  const handleRunSimulation = async () => {
    if (!targetEntityId.trim()) {
      alert("Target Entity ID is required.");
      return;
    }

    const actionDef = CANONICAL_ACTIONS.find((a) => a.type === selectedActionType);
    setSimulating(true);
    setError(null);
    try {
      const payload = {
        actionType: selectedActionType,
        targetEntityType: actionDef?.entity || "SYSTEM",
        targetEntityId: targetEntityId.trim(),
        incidentId: incidentId.trim() ? incidentId.trim() : null,
        parametersJson: parametersJson.trim() ? parametersJson.trim() : null,
        justification: justification.trim() ? justification.trim() : "Pre-execution dry run validation",
      };

      const res = await adminApi.simulateCorrectiveAction(payload);
      if (res?.data) {
        setSimulationResult(res.data);
        setWizardStep(2);
      }
    } catch (err) {
      alert(`Simulation failed: ${err.message}`);
    } finally {
      setSimulating(false);
    }
  };

  const handleExecuteLive = async () => {
    if (!justification.trim()) {
      alert("Mandatory justification is required for live execution.");
      return;
    }
    if (!simulationResult?.canExecute) {
      alert("Cannot execute an action that failed precondition checks.");
      return;
    }

    const actionDef = CANONICAL_ACTIONS.find((a) => a.type === selectedActionType);
    setExecuting(true);
    try {
      const payload = {
        actionType: selectedActionType,
        targetEntityType: actionDef?.entity || "SYSTEM",
        targetEntityId: targetEntityId.trim(),
        incidentId: incidentId.trim() ? incidentId.trim() : null,
        preconditionHash: simulationResult.preconditionHash,
        parametersJson: parametersJson.trim() ? parametersJson.trim() : null,
        justification: justification.trim(),
      };

      const res = await adminApi.executeCorrectiveAction(payload, idempotencyKey);
      if (res?.data) {
        setExecutionResult(res.data);
        setWizardStep(3);
        fetchActions();
        fetchMetrics();
      }
    } catch (err) {
      alert(`Execution failed: ${err.message}`);
    } finally {
      setExecuting(false);
    }
  };

  const tableColumns = [
    {
      header: "Action #",
      key: "actionNumber",
      render: (row) => (
        <span className="font-mono text-accent font-semibold">{row.actionNumber}</span>
      ),
    },
    {
      header: "Type",
      key: "actionType",
      render: (row) => (
        <span className="font-mono text-xs text-secondary">{row.actionType}</span>
      ),
    },
    {
      header: "Target Entity",
      key: "targetEntityType",
      render: (row) => (
        <div>
          <span className="target-pill">{row.targetEntityType}</span>
          <div className="text-xs text-tertiary font-mono">{row.targetEntityId?.substring(0, 8)}...</div>
        </div>
      ),
    },
    {
      header: "Admin / Operator",
      key: "adminName",
      render: (row) => <span className="text-sm">{row.adminName}</span>,
    },
    {
      header: "Status",
      key: "status",
      render: (row) => <AdminBadge tone={row.status} dot>{row.status}</AdminBadge>,
    },
    {
      header: "Incident Link",
      key: "incidentNumber",
      render: (row) =>
        row.incidentNumber ? (
          <span className="incident-link-badge">🚨 {row.incidentNumber}</span>
        ) : (
          <span className="text-tertiary text-xs">—</span>
        ),
    },
    {
      header: "Executed At",
      key: "createdAt",
      render: (row) => (
        <span className="text-xs text-secondary font-mono">
          {new Date(row.executedAt || row.createdAt).toLocaleString()}
        </span>
      ),
    },
  ];

  return (
    <div className="admin-page-container">
      {/* Page Header */}
      <div className="admin-page-header">
        <div className="header-titles">
          <div className="page-category">Reliability & Governance (Phase 18.15)</div>
          <h1 className="page-title">Corrective Action Engine</h1>
          <p className="page-subtitle">
            Controlled state remediation with pure-simulation dry runs, precondition hashing, idempotency keys, and audit timeline sync.
          </p>
        </div>

        <div className="header-actions">
          <AdminExportButton data={actions} filename="ethos_corrective_actions" />
          <button
            type="button"
            className="admin-btn-primary"
            onClick={openNewWizard}
          >
            ⚡ New Corrective Action
          </button>
        </div>
      </div>

      {/* KPI Cards Banner */}
      <div className="admin-kpi-grid">
        <AdminKpiCard
          title="Total Actions"
          value={metrics?.totalActions ?? "—"}
          subtitle="All-time executed & simulated"
          icon="⚡"
          tone="neutral"
        />
        <AdminKpiCard
          title="Executed (Live)"
          value={metrics?.executedActions ?? "—"}
          subtitle="Atomic business mutations"
          icon="✅"
          tone="success"
        />
        <AdminKpiCard
          title="Simulated (Dry Run)"
          value={metrics?.simulatedActions ?? "—"}
          subtitle="Zero-mutation simulations"
          icon="🔬"
          tone="warning"
        />
        <AdminKpiCard
          title="Failed / Rejected"
          value={metrics?.failedActions ?? "—"}
          subtitle="Precondition or lock halts"
          icon="⛔"
          tone={metrics?.failedActions > 0 ? "danger" : "neutral"}
        />
      </div>

      {/* Filter Toolbar */}
      <div className="admin-filter-bar">
        <div className="filter-group">
          <label htmlFor="filter-type">Action Type:</label>
          <select
            id="filter-type"
            value={filterType}
            onChange={(e) => {
              setFilterType(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All Types</option>
            {CANONICAL_ACTIONS.map((a) => (
              <option key={a.type} value={a.type}>
                {a.label}
              </option>
            ))}
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="filter-status">Status:</label>
          <select
            id="filter-status"
            value={filterStatus}
            onChange={(e) => {
              setFilterStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All Statuses</option>
            <option value="Executed">Executed</option>
            <option value="Simulated">Simulated</option>
            <option value="Failed">Failed</option>
          </select>
        </div>

        <div className="filter-actions">
          <button
            type="button"
            className="admin-btn-secondary"
            onClick={() => {
              setFilterType("");
              setFilterStatus("");
              setPage(1);
            }}
          >
            Reset Filters
          </button>
        </div>
      </div>

      {/* Ledger Table */}
      {error && <div className="admin-error-banner">{error}</div>}

      <AdminDataTable
        columns={tableColumns}
        data={actions}
        loading={loading}
        page={page}
        pageSize={15}
        totalItems={totalItems}
        onPageChange={setPage}
        onRowClick={(row) => setDetailModalItem(row)}
        emptyMessage="No corrective actions found matching your criteria."
        emptyIcon="⚡"
      />

      {/* Remediation Wizard Modal */}
      <AdminActionModal
        isOpen={wizardOpen}
        onClose={() => setWizardOpen(false)}
        title={
          wizardStep === 1
            ? "New Corrective Action: Configuration"
            : wizardStep === 2
            ? "Precondition Check & Pure Simulation Diff"
            : "Execution Complete"
        }
        subtitle={
          wizardStep === 1
            ? "Select an authorized action and specify target entity and parameters."
            : wizardStep === 2
            ? "Review projected state changes. No database mutation has occurred yet."
            : "The corrective action has been atomically applied to the database."
        }
        tone={wizardStep === 2 ? (simulationResult?.canExecute ? "warning" : "danger") : wizardStep === 3 ? "success" : "neutral"}
        maxWidth="720px"
        primaryAction={
          wizardStep === 1
            ? {
                label: simulating ? "Simulating..." : "Run Dry Run Simulation →",
                onClick: handleRunSimulation,
                disabled: simulating || !targetEntityId.trim(),
                loading: simulating,
              }
            : wizardStep === 2
            ? {
                label: executing ? "Executing..." : "Execute Business Mutation ⚡",
                onClick: handleExecuteLive,
                disabled: executing || !simulationResult?.canExecute || !justification.trim(),
                loading: executing,
              }
            : {
                label: "Close Wizard",
                onClick: () => setWizardOpen(false),
              }
        }
        secondaryAction={
          wizardStep === 2
            ? {
                label: "← Back to Edit",
                onClick: () => setWizardStep(1),
                disabled: executing,
              }
            : undefined
        }
      >
        {wizardStep === 1 && (
          <div className="wizard-form">
            <div className="form-group">
              <label htmlFor="wizard-action-type">Canonical Action:</label>
              <select
                id="wizard-action-type"
                value={selectedActionType}
                onChange={(e) => setSelectedActionType(e.target.value)}
              >
                {CANONICAL_ACTIONS.map((a) => (
                  <option key={a.type} value={a.type}>
                    {a.label} ({a.entity})
                  </option>
                ))}
              </select>
              <div className="form-hint">
                {CANONICAL_ACTIONS.find((a) => a.type === selectedActionType)?.desc}
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="wizard-target-id">Target Entity UUID *:</label>
              <input
                id="wizard-target-id"
                type="text"
                className="font-mono"
                placeholder="e.g. 550e8400-e29b-41d4-a716-446655440000"
                value={targetEntityId}
                onChange={(e) => setTargetEntityId(e.target.value)}
              />
            </div>

            <div className="form-group">
              <label htmlFor="wizard-incident-id">Associated Incident UUID (Optional):</label>
              <input
                id="wizard-incident-id"
                type="text"
                className="font-mono"
                placeholder="Attach to an active incident for automatic timeline update"
                value={incidentId}
                onChange={(e) => setIncidentId(e.target.value)}
              />
            </div>

            <div className="form-group">
              <label htmlFor="wizard-parameters">Action Parameters JSON (Optional):</label>
              <textarea
                id="wizard-parameters"
                className="font-mono"
                rows={3}
                placeholder='e.g. { "credits": 2 } or { "targetStatus": "Captured" }'
                value={parametersJson}
                onChange={(e) => setParametersJson(e.target.value)}
              />
            </div>

            <div className="form-group">
              <label htmlFor="wizard-idemp">Idempotency Key:</label>
              <div className="idemp-input-row">
                <input
                  id="wizard-idemp"
                  type="text"
                  className="font-mono text-xs"
                  readOnly
                  value={idempotencyKey}
                />
                <button
                  type="button"
                  className="admin-btn-secondary btn-sm"
                  onClick={() => setIdempotencyKey(crypto.randomUUID ? crypto.randomUUID() : `idemp-${Date.now()}`)}
                >
                  Regenerate
                </button>
              </div>
            </div>
          </div>
        )}

        {wizardStep === 2 && simulationResult && (
          <div className="wizard-simulation">
            {/* Simulation Banner */}
            <div className={`simulation-status-banner ${simulationResult.canExecute ? "status-pass" : "status-fail"}`}>
              <div className="status-banner-icon">
                {simulationResult.canExecute ? "✅" : "⚠️"}
              </div>
              <div className="status-banner-text">
                <div className="status-banner-title">
                  {simulationResult.canExecute ? "Precondition Checks Passed" : "Precondition Check Failed"}
                </div>
                <div className="status-banner-sub">
                  Precondition Hash: <code className="font-mono">{simulationResult.preconditionHash}</code>
                </div>
              </div>
            </div>

            {simulationResult.warnings?.length > 0 && (
              <div className="simulation-warnings">
                {simulationResult.warnings.map((w, i) => (
                  <div key={i} className="warning-item">⚠️ {w}</div>
                ))}
              </div>
            )}

            {/* Side by side diff */}
            <div className="simulation-diff-grid">
              <div className="diff-panel">
                <div className="diff-panel-title">Before State (Current)</div>
                <pre className="diff-json font-mono">
                  {JSON.stringify(simulationResult.diffBefore, null, 2)}
                </pre>
              </div>

              <div className="diff-panel">
                <div className="diff-panel-title">Projected After State</div>
                <pre className="diff-json font-mono projected">
                  {JSON.stringify(simulationResult.diffProjectedAfter, null, 2)}
                </pre>
              </div>
            </div>

            {/* Mandatory Justification */}
            <div className="form-group mt-4">
              <label htmlFor="wizard-exec-justification">Mandatory Execution Justification *:</label>
              <textarea
                id="wizard-exec-justification"
                rows={2}
                placeholder="Explain the technical or customer reason for applying this state change..."
                value={justification}
                onChange={(e) => setJustification(e.target.value)}
              />
              <div className="form-hint">This explanation is permanently committed into the tamper-evident audit ledger.</div>
            </div>
          </div>
        )}

        {wizardStep === 3 && executionResult && (
          <div className="wizard-complete">
            <div className="complete-banner">
              <div className="complete-icon">🎉</div>
              <div className="complete-title">Action Executed Successfully</div>
              <div className="complete-action-num font-mono">{executionResult.actionNumber}</div>
            </div>

            <div className="execution-summary-grid">
              <div className="summary-item">
                <span className="summary-label">Status:</span>
                <AdminBadge tone="Executed" dot>Executed</AdminBadge>
              </div>
              <div className="summary-item">
                <span className="summary-label">Trace ID:</span>
                <span className="font-mono text-xs">{executionResult.traceId}</span>
              </div>
              <div className="summary-item">
                <span className="summary-label">Idempotency Key:</span>
                <span className="font-mono text-xs">{executionResult.idempotencyKey}</span>
              </div>
            </div>

            <div className="execution-log-box">
              <div className="log-title">Execution Log:</div>
              <pre className="font-mono text-xs">{executionResult.executionLog}</pre>
            </div>
          </div>
        )}
      </AdminActionModal>

      {/* Row Detail Slide-Over Modal */}
      {detailModalItem && (
        <AdminActionModal
          isOpen={!!detailModalItem}
          onClose={() => setDetailModalItem(null)}
          title={`Corrective Action: ${detailModalItem.actionNumber}`}
          subtitle={`${detailModalItem.actionType} on ${detailModalItem.targetEntityType}`}
          maxWidth="700px"
        >
          <div className="detail-modal-body">
            <div className="detail-meta-grid">
              <div>
                <span className="detail-label">Status:</span>
                <AdminBadge tone={detailModalItem.status} dot>{detailModalItem.status}</AdminBadge>
              </div>
              <div>
                <span className="detail-label">Target ID:</span>
                <span className="font-mono text-xs">{detailModalItem.targetEntityId}</span>
              </div>
              <div>
                <span className="detail-label">Operator:</span>
                <span>{detailModalItem.adminName}</span>
              </div>
              <div>
                <span className="detail-label">Trace ID:</span>
                <span className="font-mono text-xs">{detailModalItem.traceId}</span>
              </div>
            </div>

            <div className="detail-section">
              <div className="section-title">Justification</div>
              <p className="justification-text">{detailModalItem.justification}</p>
            </div>

            <div className="detail-section">
              <div className="section-title">Execution Log</div>
              <pre className="font-mono text-xs bg-box">{detailModalItem.executionLog || "No logs recorded."}</pre>
            </div>

            {detailModalItem.beforeStateJson && (
              <div className="detail-section">
                <div className="section-title">State Diff (Before / After)</div>
                <div className="simulation-diff-grid">
                  <div className="diff-panel">
                    <div className="diff-panel-title">Before</div>
                    <pre className="diff-json font-mono">{detailModalItem.beforeStateJson}</pre>
                  </div>
                  <div className="diff-panel">
                    <div className="diff-panel-title">After</div>
                    <pre className="diff-json font-mono projected">{detailModalItem.afterStateJson}</pre>
                  </div>
                </div>
              </div>
            )}
          </div>
        </AdminActionModal>
      )}
    </div>
  );
}
