import React, { useState, useEffect } from "react";
import { adminApi } from "../../../services/adminApi";
import AdminBadge from "../common/AdminBadge";
import {
  formatAdminCurrency,
  formatAdminDateTime,
} from "../../../utils/adminFormatters";
import "./PaymentDetailsDrawer.css";

export default function PaymentDetailsDrawer({
  transaction,
  onClose,
  onOpenReceipt,
  onOpenRefund,
  onOpenResolve,
}) {
  const [timeline, setTimeline] = useState([]);
  const [loadingTimeline, setLoadingTimeline] = useState(true);
  const [timelineError, setTimelineError] = useState(null);
  const [showTechnicalDetails, setShowTechnicalDetails] = useState(false);
  const [copiedKey, setCopiedKey] = useState(null);

  useEffect(() => {
    if (!transaction?.id) return;
    let isMounted = true;
    setLoadingTimeline(true);
    setTimelineError(null);

    adminApi
      .getPaymentTimeline(transaction.id)
      .then((data) => {
        if (isMounted) {
          setTimeline(Array.isArray(data) ? data : []);
          setLoadingTimeline(false);
        }
      })
      .catch((err) => {
        if (isMounted) {
          setTimelineError(err.message || "Failed to load payment timeline.");
          setLoadingTimeline(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [transaction?.id]);

  if (!transaction) return null;

  const handleCopy = (text, key) => {
    if (!text) return;
    navigator.clipboard?.writeText(text);
    setCopiedKey(key);
    setTimeout(() => setCopiedKey(null), 2000);
  };

  const isSuccessful = transaction.status === 4;
  const isFailed = transaction.status === 5;
  const isRefunded = transaction.status === 7 || transaction.status === 8;
  const isPending = transaction.status === 2 || transaction.status === 3;

  return (
    <div className="drawer-overlay" onClick={onClose}>
      <div
        className="payment-details-drawer"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label="Payment Summary & History"
      >
        {/* Drawer Header */}
        <div className="pdd-header">
          <div className="pdd-header-titles">
            <div className="pdd-badge-row">
              <span className="pdd-ref-tag">
                Payment #{transaction.id.substring(0, 8)}
              </span>
              {transaction.hasCustomerReportedDiscrepancy && (
                <span className="pdd-alert-badge">Customer Reports Deduction</span>
              )}
            </div>
            <h2>Payment & Order Overview</h2>
            <p className="pdd-subtitle">
              Initiated on {formatAdminDateTime(transaction.createdAt)}
            </p>
          </div>
          <button
            type="button"
            className="pdd-close-btn"
            onClick={onClose}
            aria-label="Close details"
          >
            ✕
          </button>
        </div>

        {/* Drawer Scrollable Body */}
        <div className="pdd-body">
          {/* Plain-English Status Banner */}
          <div
            className={`pdd-outcome-banner ${
              isSuccessful
                ? "success"
                : isFailed
                ? "danger"
                : isRefunded
                ? "refunded"
                : "pending"
            }`}
          >
            <div className="pdd-outcome-icon">
              {isSuccessful && "✓"}
              {isFailed && "!"}
              {isRefunded && "↺"}
              {isPending && "⏳"}
            </div>
            <div className="pdd-outcome-text">
              <div className="pdd-outcome-headline">
                {isSuccessful && "Payment Received Successfully"}
                {isFailed && "Payment Not Completed (Failed)"}
                {isRefunded && "Payment Refunded"}
                {isPending && "Payment In Progress / Pending Confirmation"}
              </div>
              <div className="pdd-outcome-desc">
                {transaction.customerImpact ||
                  (isSuccessful
                    ? "Funds collected. Student granted full access to their purchase."
                    : isFailed
                    ? "Checkout was not finalized. If money left customer's bank, use Resolve Issue to verify."
                    : "Payment status recorded in studio ledger.")}
              </div>
            </div>
          </div>

          {/* Customer Card */}
          <div className="pdd-card">
            <h3 className="pdd-card-title">Customer Information</h3>
            <div className="pdd-grid-2">
              <div>
                <span className="pdd-field-label">Student / Customer</span>
                <div className="pdd-field-val font-semibold">
                  {transaction.userName || "Guest Attendee"}
                </div>
              </div>
              <div>
                <span className="pdd-field-label">Customer Code</span>
                <div className="pdd-field-val">
                  {transaction.userCustomerCode || "N/A"}
                </div>
              </div>
              <div>
                <span className="pdd-field-label">Contact Phone</span>
                <div className="pdd-field-val">
                  {transaction.userPhone ? (
                    <a href={`tel:${transaction.userPhone}`} className="pdd-link">
                      {transaction.userPhone}
                    </a>
                  ) : (
                    "No phone on file"
                  )}
                </div>
              </div>
              <div>
                <span className="pdd-field-label">Email Address</span>
                <div className="pdd-field-val">
                  {transaction.userEmail ? (
                    <a href={`mailto:${transaction.userEmail}`} className="pdd-link">
                      {transaction.userEmail}
                    </a>
                  ) : (
                    "No email on file"
                  )}
                </div>
              </div>
            </div>
          </div>

          {/* Purchase & Fulfillment Card */}
          <div className="pdd-card">
            <h3 className="pdd-card-title">Purchase & Delivery Status</h3>
            <div className="pdd-grid-2">
              <div>
                <span className="pdd-field-label">Purchased Item</span>
                <div className="pdd-field-val font-semibold">
                  {transaction.itemName || "Studio Service"}
                </div>
                <div className="pdd-field-sub">
                  Category: {transaction.displayPurpose || "Standard Purchase"}
                </div>
              </div>
              <div>
                <span className="pdd-field-label">Access / Fulfillment Result</span>
                <div className="pdd-field-val font-semibold">
                  {transaction.fulfillmentStatus === "Active / Confirmed" && (
                    <span className="text-success">✓ Active / Confirmed</span>
                  )}
                  {transaction.fulfillmentStatus === "Pending Confirmation" && (
                    <span className="text-warning">⏳ Awaiting Confirmation</span>
                  )}
                  {transaction.fulfillmentStatus === "Not Granted (Payment Failed)" && (
                    <span className="text-danger">✕ Not Granted (Payment Failed)</span>
                  )}
                  {!["Active / Confirmed", "Pending Confirmation", "Not Granted (Payment Failed)"].includes(
                    transaction.fulfillmentStatus
                  ) && (
                    <span>{transaction.fulfillmentStatus || "Not Applicable"}</span>
                  )}
                </div>
                {transaction.actionNeeded && (
                  <div className="pdd-action-needed-badge">
                    {transaction.actionNeeded}
                  </div>
                )}
              </div>
            </div>

            {transaction.itemDetails && (
              <div className="pdd-item-description-box">
                <span className="pdd-field-label">Package / Workshop Details</span>
                <p>{transaction.itemDetails}</p>
              </div>
            )}
          </div>

          {/* Financial Breakdown Card */}
          <div className="pdd-card">
            <h3 className="pdd-card-title">Financial Breakdown</h3>
            <div className="pdd-finance-grid">
              <div className="pdd-finance-item">
                <span className="pdd-field-label">Total Amount</span>
                <span className="pdd-finance-val primary">
                  {formatAdminCurrency(transaction.amount)}
                </span>
              </div>
              <div className="pdd-finance-item">
                <span className="pdd-field-label">Amount Refunded</span>
                <span
                  className={`pdd-finance-val ${
                    transaction.totalRefundedAmount > 0 ? "text-amber" : "text-muted"
                  }`}
                >
                  {formatAdminCurrency(transaction.totalRefundedAmount || 0)}
                </span>
              </div>
              <div className="pdd-finance-item">
                <span className="pdd-field-label">Remaining Refundable</span>
                <span className="pdd-finance-val">
                  {formatAdminCurrency(
                    transaction.remainingRefundableAmount ?? transaction.amount
                  )}
                </span>
              </div>
            </div>
          </div>

          {/* Payment Event Timeline */}
          <div className="pdd-card">
            <h3 className="pdd-card-title">Payment Event Timeline</h3>
            {loadingTimeline ? (
              <div className="pdd-timeline-loading">Loading event history...</div>
            ) : timelineError ? (
              <div className="pdd-timeline-error">{timelineError}</div>
            ) : timeline.length === 0 ? (
              <div className="pdd-timeline-empty">
                No intermediate gateway lifecycle events recorded for this payment.
              </div>
            ) : (
              <div className="pdd-timeline-list">
                {timeline.map((evt, idx) => (
                  <div key={idx} className="pdd-timeline-item">
                    <div
                      className={`pdd-timeline-dot ${evt.severity || "info"}`}
                    />
                    <div className="pdd-timeline-content">
                      <div className="pdd-timeline-header">
                        <span className="pdd-timeline-title font-semibold">
                          {evt.title}
                        </span>
                        <span className="pdd-timeline-time">
                          {formatAdminDateTime(evt.timestamp)}
                        </span>
                      </div>
                      <div className="pdd-timeline-desc">{evt.description}</div>
                      {evt.actor && (
                        <div className="pdd-timeline-actor">
                          Logged by: {evt.actor}
                        </div>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Expandable Technical Reference Section */}
          <div className="pdd-tech-section">
            <button
              type="button"
              className="pdd-tech-toggle"
              onClick={() => setShowTechnicalDetails(!showTechnicalDetails)}
            >
              <span>{showTechnicalDetails ? "▼" : "▶"} Gateway & Technical Identifiers</span>
              <span className="pdd-tech-toggle-sub">
                (Razorpay Order, Payment ID, Internal UUID)
              </span>
            </button>

            {showTechnicalDetails && (
              <div className="pdd-tech-body">
                <div className="pdd-tech-row">
                  <span className="pdd-tech-label">Transaction UUID:</span>
                  <span className="pdd-tech-mono">{transaction.id}</span>
                  <button
                    type="button"
                    className="pdd-copy-btn"
                    onClick={() => handleCopy(transaction.id, "tx-uuid")}
                  >
                    {copiedKey === "tx-uuid" ? "Copied" : "Copy"}
                  </button>
                </div>

                <div className="pdd-tech-row">
                  <span className="pdd-tech-label">Razorpay Order ID:</span>
                  <span className="pdd-tech-mono">
                    {transaction.razorpayOrderId || "Not Generated"}
                  </span>
                  {transaction.razorpayOrderId && (
                    <button
                      type="button"
                      className="pdd-copy-btn"
                      onClick={() =>
                        handleCopy(transaction.razorpayOrderId, "rp-order")
                      }
                    >
                      {copiedKey === "rp-order" ? "Copied" : "Copy"}
                    </button>
                  )}
                </div>

                <div className="pdd-tech-row">
                  <span className="pdd-tech-label">Razorpay Payment ID:</span>
                  <span className="pdd-tech-mono">
                    {transaction.razorpayPaymentId || "None"}
                  </span>
                  {transaction.razorpayPaymentId && (
                    <button
                      type="button"
                      className="pdd-copy-btn"
                      onClick={() =>
                        handleCopy(transaction.razorpayPaymentId, "rp-pay")
                      }
                    >
                      {copiedKey === "rp-pay" ? "Copied" : "Copy"}
                    </button>
                  )}
                </div>

                <div className="pdd-tech-row">
                  <span className="pdd-tech-label">Gateway Verified:</span>
                  <span>{transaction.isGatewayVerified ? "Yes (✓)" : "No"}</span>
                </div>

                <div className="pdd-tech-row">
                  <span className="pdd-tech-label">Admin Reconciled:</span>
                  <span>{transaction.isReconciled ? "Yes (✓)" : "No"}</span>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Drawer Footer Actions */}
        <div className="pdd-footer">
          <div className="pdd-footer-left">
            <button type="button" className="admin-btn secondary" onClick={onClose}>
              Close
            </button>
          </div>
          <div className="pdd-footer-right">
            {(transaction.status === 4 || transaction.status === 8) && (
              <button
                type="button"
                className="admin-btn secondary"
                onClick={() => onOpenReceipt(transaction.id)}
              >
                View Official Receipt
              </button>
            )}

            {(transaction.status === 4 || transaction.status === 8) && (
              <button
                type="button"
                className="admin-btn danger"
                onClick={() => onOpenRefund(transaction)}
              >
                Record Refund
              </button>
            )}

            {transaction.status !== 7 && (
              <button
                type="button"
                className="admin-btn primary"
                onClick={() => onOpenResolve(transaction)}
              >
                Resolve Payment Issue
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
