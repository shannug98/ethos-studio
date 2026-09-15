import React, { useState, useEffect, useCallback } from "react";
import { adminApi } from "../../services/adminApi";
import AdminKpiCard from "../../components/admin/common/AdminKpiCard";
import AdminBadge from "../../components/admin/common/AdminBadge";
import AdminDataTable from "../../components/admin/common/AdminDataTable";
import AdminActionModal from "../../components/admin/common/AdminActionModal";
import AdminExportButton from "../../components/admin/common/AdminExportButton";
import "./AdminCommunications.css";

export default function AdminCommunications() {
  const [logs, setLogs] = useState([]);
  const [metrics, setMetrics] = useState(null);
  const [templates, setTemplates] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [page, setPage] = useState(1);
  const [totalItems, setTotalItems] = useState(0);

  // Filters
  const [filterChannel, setFilterChannel] = useState("");
  const [filterStatus, setFilterStatus] = useState("");
  const [filterTemplate, setFilterTemplate] = useState("");

  // Modals
  const [dispatchModalOpen, setDispatchModalOpen] = useState(false);
  const [templateCatalogOpen, setTemplateCatalogOpen] = useState(false);
  const [detailModalItem, setDetailModalItem] = useState(null);

  // Send form state
  const [sendChannel, setSendChannel] = useState("SMS");
  const [sendTemplateId, setSendTemplateId] = useState("");
  const [sendRecipient, setSendRecipient] = useState("");
  const [sendParameters, setSendParameters] = useState({});
  const [sendJustification, setSendJustification] = useState("");
  const [sendSubmitting, setSendSubmitting] = useState(false);

  // Retry state
  const [retrying, setRetrying] = useState(false);

  const fetchMetrics = useCallback(async () => {
    try {
      const res = await adminApi.getCommunicationMetrics();
      if (res?.data) setMetrics(res.data);
    } catch {
      // Keep existing metrics if unavailable
    }
  }, []);

  const fetchTemplates = useCallback(async () => {
    try {
      const res = await adminApi.getCommunicationTemplates();
      if (res?.data) {
        setTemplates(res.data);
        if (res.data.length > 0 && !sendTemplateId) {
          setSendTemplateId(res.data[0].id);
        }
      }
    } catch {
      // Ignore template load errors
    }
  }, [sendTemplateId]);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: "15",
      });
      if (filterChannel) params.append("channel", filterChannel);
      if (filterStatus) params.append("status", filterStatus);
      if (filterTemplate) params.append("templateId", filterTemplate);

      const res = await adminApi.getCommunicationLogs(params.toString());
      if (res?.data) {
        setLogs(res.data.items || []);
        setTotalItems(res.data.totalCount || 0);
      }
    } catch (err) {
      setError(err.message || "Failed to load communications ledger.");
    } finally {
      setLoading(false);
    }
  }, [page, filterChannel, filterStatus, filterTemplate]);

  useEffect(() => {
    fetchMetrics();
    fetchTemplates();
    fetchLogs();
  }, [fetchMetrics, fetchTemplates, fetchLogs]);

  const openSendModal = () => {
    setSendChannel("SMS");
    if (templates.length > 0) {
      setSendTemplateId(templates[0].id);
    }
    setSendRecipient("");
    setSendParameters({});
    setSendJustification("");
    setDispatchModalOpen(true);
  };

  const handleSend = async () => {
    if (!sendRecipient.trim()) {
      alert("Recipient phone or email is required.");
      return;
    }
    if (!sendTemplateId) {
      alert("An approved DLT template must be selected. Free-form messaging is disallowed.");
      return;
    }
    if (!sendJustification.trim()) {
      alert("Mandatory justification is required for test dispatch.");
      return;
    }

    setSendSubmitting(true);
    try {
      const idempKey = crypto.randomUUID ? crypto.randomUUID() : `msg-${Date.now()}`;
      const payload = {
        channel: sendChannel,
        recipient: sendRecipient.trim(),
        templateId: sendTemplateId,
        templateParameters: sendParameters,
        justification: sendJustification.trim(),
      };

      await adminApi.sendCommunication(payload, idempKey);
      setDispatchModalOpen(false);
      fetchLogs();
      fetchMetrics();
      alert("Message queued for dispatch successfully.");
    } catch (err) {
      alert(`Dispatch failed: ${err.message}`);
    } finally {
      setSendSubmitting(false);
    }
  };

  const handleRetry = async (logItem) => {
    if (!logItem || logItem.retryCount >= 3) {
      alert("Cannot retry: maximum permitted retries (3) reached.");
      return;
    }

    const reason = prompt("Enter justification for re-dispatching this notification:");
    if (!reason || !reason.trim()) {
      alert("Retry aborted: justification is mandatory.");
      return;
    }

    setRetrying(true);
    try {
      const idempKey = `retry-${logItem.id}-${logItem.retryCount + 1}`;
      const payload = {
        communicationId: logItem.id,
        justification: reason.trim(),
      };

      await adminApi.retryCommunication(payload, idempKey);
      fetchLogs();
      fetchMetrics();
      if (detailModalItem?.id === logItem.id) {
        setDetailModalItem(null);
      }
      alert("Message re-dispatched successfully.");
    } catch (err) {
      alert(`Retry failed: ${err.message}`);
    } finally {
      setRetrying(false);
    }
  };

  const selectedTemplateObj = templates.find((t) => t.id === sendTemplateId);

  const tableColumns = [
    {
      header: "Reference",
      key: "messageReference",
      render: (row) => (
        <span className="font-mono text-accent font-semibold">{row.messageReference}</span>
      ),
    },
    {
      header: "Channel",
      key: "channel",
      render: (row) => (
        <span className={`channel-pill channel-${row.channel?.toLowerCase()}`}>
          {row.channel === "WHATSAPP" ? "📱 WA" : row.channel === "SMS" ? "✉️ SMS" : "📧 EMAIL"}
        </span>
      ),
    },
    {
      header: "Recipient (PII Masked)",
      key: "recipient",
      render: (row) => <span className="font-mono text-xs text-secondary">{row.recipient}</span>,
    },
    {
      header: "Template ID",
      key: "templateId",
      render: (row) => (
        <span className="font-mono text-xs text-tertiary">{row.templateId}</span>
      ),
    },
    {
      header: "Status",
      key: "status",
      render: (row) => <AdminBadge tone={row.status} dot>{row.status}</AdminBadge>,
    },
    {
      header: "Provider",
      key: "provider",
      render: (row) => (
        <span className={`provider-tag ${row.provider === "SIMULATED" ? "simulated" : "live"}`}>
          {row.provider}
        </span>
      ),
    },
    {
      header: "Retries",
      key: "retryCount",
      render: (row) => (
        <span className={`font-mono text-xs ${row.retryCount >= 3 ? "text-danger" : ""}`}>
          {row.retryCount} / 3
        </span>
      ),
    },
    {
      header: "Timestamp",
      key: "createdAt",
      render: (row) => (
        <span className="text-xs text-secondary font-mono">
          {new Date(row.deliveredAt || row.createdAt).toLocaleString()}
        </span>
      ),
    },
  ];

  return (
    <div className="admin-page-container">
      {/* Header */}
      <div className="admin-page-header">
        <div className="header-titles">
          <div className="page-category">Reliability & Communications (Phase 18.16)</div>
          <h1 className="page-title">Communications & Messaging Center</h1>
          <p className="page-subtitle">
            Unified communications ledger for MSG91 SMS, WhatsApp, and transactional emails with DLT template allowlisting, PII masking, and governed retry controls.
          </p>
        </div>

        <div className="header-actions">
          <button
            type="button"
            className="admin-btn-secondary"
            onClick={() => setTemplateCatalogOpen(true)}
          >
            📋 Template Catalog ({templates.length})
          </button>
          <AdminExportButton data={logs} filename="ethos_communications_ledger" />
          <button
            type="button"
            className="admin-btn-primary"
            onClick={openSendModal}
          >
            ✉️ Dispatch Test Message
          </button>
        </div>
      </div>

      {/* KPI Banner */}
      <div className="admin-kpi-grid">
        <AdminKpiCard
          title="Total Dispatched"
          value={metrics?.totalSent ?? "—"}
          subtitle="All channels combined"
          icon="📬"
          tone="neutral"
        />
        <AdminKpiCard
          title="Delivered"
          value={metrics?.deliveredCount ?? "—"}
          subtitle={`${metrics?.deliveryRatePercent ?? 100}% delivery rate`}
          icon="✅"
          tone="success"
        />
        <AdminKpiCard
          title="Simulated / Sandbox"
          value={metrics?.simulatedCount ?? "—"}
          subtitle="Development mock dispatches"
          icon="🔬"
          tone="warning"
        />
        <AdminKpiCard
          title="Failed Dispatches"
          value={metrics?.failedCount ?? "—"}
          subtitle="Permanent or transient errors"
          icon="❌"
          tone={metrics?.failedCount > 0 ? "danger" : "neutral"}
        />
      </div>

      {/* Filter Toolbar */}
      <div className="admin-filter-bar">
        <div className="filter-group">
          <label htmlFor="filter-channel">Channel:</label>
          <select
            id="filter-channel"
            value={filterChannel}
            onChange={(e) => {
              setFilterChannel(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All Channels</option>
            <option value="SMS">SMS</option>
            <option value="WHATSAPP">WhatsApp</option>
            <option value="EMAIL">Email</option>
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
            <option value="DELIVERED">Delivered</option>
            <option value="SENT">Sent</option>
            <option value="SIMULATED">Simulated</option>
            <option value="QUEUED">Queued</option>
            <option value="FAILED">Failed</option>
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="filter-template">Template:</label>
          <select
            id="filter-template"
            value={filterTemplate}
            onChange={(e) => {
              setFilterTemplate(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All Templates</option>
            {templates.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name}
              </option>
            ))}
          </select>
        </div>

        <div className="filter-actions">
          <button
            type="button"
            className="admin-btn-secondary"
            onClick={() => {
              setFilterChannel("");
              setFilterStatus("");
              setFilterTemplate("");
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
        data={logs}
        loading={loading}
        page={page}
        pageSize={15}
        totalItems={totalItems}
        onPageChange={setPage}
        onRowClick={(row) => setDetailModalItem(row)}
        emptyMessage="No communications logs found matching your filters."
        emptyIcon="💬"
      />

      {/* Test Dispatch Modal */}
      <AdminActionModal
        isOpen={dispatchModalOpen}
        onClose={() => setDispatchModalOpen(false)}
        title="Dispatch Authoritative Message"
        subtitle="Only pre-approved DLT templates can be dispatched. Free-form messaging is blocked by policy."
        maxWidth="640px"
        primaryAction={{
          label: sendSubmitting ? "Dispatching..." : "Send Message ✉️",
          onClick: handleSend,
          disabled: sendSubmitting || !sendRecipient.trim() || !sendJustification.trim(),
          loading: sendSubmitting,
        }}
        secondaryAction={{
          label: "Cancel",
          onClick: () => setDispatchModalOpen(false),
        }}
      >
        <div className="wizard-form">
          <div className="form-group">
            <label htmlFor="dispatch-channel">Channel:</label>
            <select
              id="dispatch-channel"
              value={sendChannel}
              onChange={(e) => setSendChannel(e.target.value)}
            >
              <option value="SMS">SMS (MSG91 Transactional)</option>
              <option value="WHATSAPP">WhatsApp (Meta Business via MSG91)</option>
              <option value="EMAIL">Transactional Email</option>
            </select>
          </div>

          <div className="form-group">
            <label htmlFor="dispatch-template">Approved Template *:</label>
            <select
              id="dispatch-template"
              value={sendTemplateId}
              onChange={(e) => {
                setSendTemplateId(e.target.value);
                setSendParameters({});
              }}
            >
              {templates.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} ({t.id})
                </option>
              ))}
            </select>
            {selectedTemplateObj && (
              <div className="template-preview-box">
                <div className="preview-label">Approved DLT Body:</div>
                <div className="preview-text font-mono text-xs">{selectedTemplateObj.bodyPreview}</div>
              </div>
            )}
          </div>

          <div className="form-group">
            <label htmlFor="dispatch-recipient">
              Recipient {sendChannel === "EMAIL" ? "Email Address" : "Phone Number (+91)"} *:
            </label>
            <input
              id="dispatch-recipient"
              type="text"
              placeholder={sendChannel === "EMAIL" ? "student@example.com" : "9876543210"}
              value={sendRecipient}
              onChange={(e) => setSendRecipient(e.target.value)}
            />
            <div className="form-hint">Numbers will be sanitized and logged with PII masking.</div>
          </div>

          {selectedTemplateObj?.requiredVariables?.length > 0 && (
            <div className="template-variables-section">
              <div className="section-subtitle">Template Variables</div>
              {selectedTemplateObj.requiredVariables.map((variable) => (
                <div key={variable} className="form-group">
                  <label htmlFor={`var-${variable}`}>{variable}:</label>
                  <input
                    id={`var-${variable}`}
                    type="text"
                    placeholder={`Value for ${variable}`}
                    value={sendParameters[variable] || ""}
                    onChange={(e) =>
                      setSendParameters({ ...sendParameters, [variable]: e.target.value })
                    }
                  />
                </div>
              ))}
            </div>
          )}

          <div className="form-group">
            <label htmlFor="dispatch-justification">Mandatory Dispatch Justification *:</label>
            <textarea
              id="dispatch-justification"
              rows={2}
              placeholder="Reason for manual message dispatch..."
              value={sendJustification}
              onChange={(e) => setSendJustification(e.target.value)}
            />
          </div>
        </div>
      </AdminActionModal>

      {/* Template Catalog Drawer Modal */}
      <AdminActionModal
        isOpen={templateCatalogOpen}
        onClose={() => setTemplateCatalogOpen(false)}
        title="DLT Approved Template Catalog"
        subtitle="Authorized regulatory templates. Free-form text generation is strictly disallowed."
        maxWidth="720px"
        primaryAction={{
          label: "Close Catalog",
          onClick: () => setTemplateCatalogOpen(false),
        }}
      >
        <div className="catalog-list">
          {templates.map((tpl) => (
            <div key={tpl.id} className="catalog-item">
              <div className="catalog-item-header">
                <span className="catalog-item-name">{tpl.name}</span>
                <span className="font-mono text-xs text-accent">{tpl.id}</span>
              </div>
              <p className="catalog-item-desc">{tpl.description}</p>
              <div className="catalog-item-body font-mono text-xs">{tpl.bodyPreview}</div>
              <div className="catalog-item-vars">
                <span className="vars-label">Required Variables:</span>
                {tpl.requiredVariables?.length > 0 ? (
                  tpl.requiredVariables.map((v) => (
                    <span key={v} className="var-chip font-mono">
                      {`{${v}}`}
                    </span>
                  ))
                ) : (
                  <span className="text-tertiary text-xs">None (Static)</span>
                )}
              </div>
            </div>
          ))}
        </div>
      </AdminActionModal>

      {/* Row Detail / Retry Modal */}
      {detailModalItem && (
        <AdminActionModal
          isOpen={!!detailModalItem}
          onClose={() => setDetailModalItem(null)}
          title={`Message: ${detailModalItem.messageReference}`}
          subtitle={`${detailModalItem.channel} notification to ${detailModalItem.recipient}`}
          maxWidth="640px"
          primaryAction={
            detailModalItem.status === "FAILED" && detailModalItem.retryCount < 3
              ? {
                  label: retrying ? "Retrying..." : "Retry Dispatch ↺",
                  onClick: () => handleRetry(detailModalItem),
                  disabled: retrying,
                  loading: retrying,
                }
              : undefined
          }
          secondaryAction={{
            label: "Close",
            onClick: () => setDetailModalItem(null),
          }}
        >
          <div className="detail-modal-body">
            <div className="detail-meta-grid">
              <div>
                <span className="detail-label">Status:</span>
                <AdminBadge tone={detailModalItem.status} dot>{detailModalItem.status}</AdminBadge>
              </div>
              <div>
                <span className="detail-label">Channel:</span>
                <span>{detailModalItem.channel}</span>
              </div>
              <div>
                <span className="detail-label">Provider:</span>
                <span className="provider-tag live">{detailModalItem.provider}</span>
              </div>
              <div>
                <span className="detail-label">Retry Count:</span>
                <span className="font-mono">{detailModalItem.retryCount} / 3</span>
              </div>
              <div>
                <span className="detail-label">Trace ID:</span>
                <span className="font-mono text-xs">{detailModalItem.traceId || "—"}</span>
              </div>
              <div>
                <span className="detail-label">Provider Msg ID:</span>
                <span className="font-mono text-xs">{detailModalItem.providerMessageId || "—"}</span>
              </div>
            </div>

            <div className="detail-section">
              <div className="section-title">Message Preview (Zero-Secrets)</div>
              <pre className="font-mono text-xs bg-box">{detailModalItem.bodyPreview}</pre>
            </div>

            {detailModalItem.errorMessage && (
              <div className="detail-section">
                <div className="section-title text-danger">Provider Error Message</div>
                <pre className="font-mono text-xs bg-box error-box">{detailModalItem.errorMessage}</pre>
              </div>
            )}

            <div className="detail-section">
              <div className="section-title">Justification</div>
              <p className="justification-text">{detailModalItem.justification}</p>
            </div>
          </div>
        </AdminActionModal>
      )}
    </div>
  );
}
