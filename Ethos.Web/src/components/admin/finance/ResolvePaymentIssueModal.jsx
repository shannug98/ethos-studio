import React, { useState } from "react";
import AdminActionModal from "../common/AdminActionModal";
import { formatAdminCurrency } from "../../../utils/adminFormatters";
import { adminApi } from "../../../services/adminApi";
import "./ResolvePaymentIssueModal.css";

export default function ResolvePaymentIssueModal({
  isOpen,
  transaction,
  onClose,
  onSuccess,
}) {
  const [actionType, setActionType] = useState("CONFIRM_AND_FULFILL");
  const [gatewayReference, setGatewayReference] = useState(
    transaction?.razorpayPaymentId || ""
  );
  const [adminReason, setAdminReason] = useState("");
  const [adminNotes, setAdminNotes] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [resolutionResult, setResolutionResult] = useState(null);
  const [copiedMessage, setCopiedMessage] = useState(false);

  if (!transaction) return null;

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!adminReason.trim()) {
      setError("A clear administrative reason is mandatory for audit logging.");
      return;
    }

    if (actionType === "CONFIRM_AND_FULFILL" && !gatewayReference.trim()) {
      setError(
        "Gateway Payment ID or Bank UTR Reference is required when confirming that payment was received."
      );
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const response = await adminApi.resolvePaymentIssue(transaction.id, {
        actionType,
        gatewayReference: gatewayReference.trim() || null,
        adminReason: adminReason.trim(),
        adminNotes: adminNotes.trim() || null,
      });

      setResolutionResult(response);
      setSubmitting(false);
      if (onSuccess) {
        onSuccess(response);
      }
    } catch (err) {
      setError(err.message || "Failed to resolve payment issue.");
      setSubmitting(false);
    }
  };

  const handleCopyCustomerMessage = () => {
    if (!resolutionResult?.customerMessage) return;
    navigator.clipboard?.writeText(resolutionResult.customerMessage);
    setCopiedMessage(true);
    setTimeout(() => setCopiedMessage(false), 2500);
  };

  return (
    <AdminActionModal
      isOpen={isOpen}
      onClose={onClose}
      title={resolutionResult ? "Payment Issue Resolved" : "Resolve Payment Issue"}
      subtitle={`Customer: ${transaction.userName || "Customer"} • Amount: ${formatAdminCurrency(
        transaction.amount
      )}`}
      tone={actionType === "CONFIRM_AND_FULFILL" ? "primary" : "warning"}
      maxWidth="600px"
    >
      {resolutionResult ? (
        <div className="resolution-success-view">
          <div className="resolution-success-banner">
            <span className="success-icon">✓</span>
            <div>
              <h4>{resolutionResult.actionSummary}</h4>
              <p>The transaction and audit logs have been updated successfully.</p>
            </div>
          </div>

          <div className="resolution-impact-box">
            <div className="impact-row">
              <span className="impact-label">New Status:</span>
              <span className="impact-val font-semibold">{resolutionResult.newStatus}</span>
            </div>
            <div className="impact-row">
              <span className="impact-label">Fulfillment:</span>
              <span className="impact-val font-semibold text-success">
                {resolutionResult.fulfillmentResult}
              </span>
            </div>
            <div className="impact-row">
              <span className="impact-label">Audit Recorded:</span>
              <span className="impact-val">{resolutionResult.auditRecorded ? "Yes (✓)" : "No"}</span>
            </div>
          </div>

          {resolutionResult.customerMessage && (
            <div className="customer-msg-box">
              <div className="customer-msg-header">
                <span className="customer-msg-title">Ready-to-Send Customer Message</span>
                <button
                  type="button"
                  className="copy-msg-btn"
                  onClick={handleCopyCustomerMessage}
                >
                  {copiedMessage ? "✓ Copied to Clipboard" : "📋 Copy Message"}
                </button>
              </div>
              <textarea
                readOnly
                className="customer-msg-textarea"
                rows={5}
                value={resolutionResult.customerMessage}
              />
              <span className="customer-msg-hint">
                You can copy and send this message to the customer via WhatsApp or SMS.
              </span>
            </div>
          )}

          <div className="modal-form-actions">
            <button type="button" className="admin-btn primary" onClick={onClose}>
              Done & Close
            </button>
          </div>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="resolve-payment-form">
          {error && <div className="modal-error-banner">{error}</div>}

          {/* Context box explaining customer situation */}
          <div className="resolve-context-card">
            <div className="resolve-context-title">What is the customer reporting?</div>
            <p className="resolve-context-desc">
              If the customer reports money was deducted from their bank/UPI but our site marked the payment failed, select <strong>Confirm Payment Received & Fulfill Access</strong> with their UTR or Razorpay ID. The system will automatically grant their package or confirm their workshop seat.
            </p>
          </div>

          <div className="form-group">
            <label>
              Action to Take <span className="req">*</span>
            </label>
            <select
              value={actionType}
              onChange={(e) => setActionType(e.target.value)}
              className="resolve-action-select"
            >
              <option value="CONFIRM_AND_FULFILL">
                ✓ Confirm Payment Received & Fulfill Access (Grant Package/Seat)
              </option>
              <option value="FLAG_DISCREPANCY">
                🚩 Customer Reports Deduction (Flag For Investigation)
              </option>
              <option value="MARK_NOT_RECEIVED">
                ✕ Payment Definitely Not Received (Advise Bank Refund)
              </option>
              <option value="RECORD_NOTE">
                📝 Record Administrative Note / Update Reference
              </option>
            </select>
          </div>

          {actionType === "CONFIRM_AND_FULFILL" && (
            <div className="form-group">
              <label>
                Razorpay Payment ID or Bank UTR <span className="req">*</span>
              </label>
              <input
                type="text"
                placeholder="e.g. pay_xxxxxxxxxxxx or 12-digit Bank UTR"
                value={gatewayReference}
                onChange={(e) => setGatewayReference(e.target.value)}
                required
              />
              <small className="help-text">
                Evidence that funds reached the studio Razorpay account or bank.
              </small>
            </div>
          )}

          <div className="form-group">
            <label>
              Administrative Justification <span className="req">*</span>
            </label>
            <textarea
              placeholder="Explain why this action is being taken (e.g. 'Customer provided bank statement showing debit at 4:32 PM, confirmed on Razorpay dashboard')..."
              rows={3}
              value={adminReason}
              onChange={(e) => setAdminReason(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label>Additional Internal Notes (Optional)</label>
            <textarea
              placeholder="Optional notes for studio audit trail..."
              rows={2}
              value={adminNotes}
              onChange={(e) => setAdminNotes(e.target.value)}
            />
          </div>

          {/* Action Impact Warning */}
          {actionType === "CONFIRM_AND_FULFILL" && (
            <div className="action-warning-box">
              <strong>Impact of this action:</strong>
              <ul>
                <li>Payment status will transition to <strong>Paid</strong>.</li>
                <li>The underlying <strong>{transaction.itemName || "Item"}</strong> will be immediately activated/confirmed.</li>
                <li>An official commercial receipt will become available.</li>
              </ul>
            </div>
          )}

          <div className="modal-form-actions">
            <button
              type="button"
              className="admin-btn secondary"
              disabled={submitting}
              onClick={onClose}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="admin-btn primary"
              disabled={submitting}
            >
              {submitting ? "Processing Action..." : "Confirm & Execute"}
            </button>
          </div>
        </form>
      )}
    </AdminActionModal>
  );
}
