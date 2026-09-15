import React from "react";
import { formatAdminCurrency, formatAdminDateTime } from "../../../utils/adminFormatters";
import letterheadHeader from "../../../assets/official-letterhead-header.png";
import letterheadFooter from "../../../assets/official-letterhead-footer.png";
import "./OfficialCommercialReceipt.css";

/**
 * OfficialCommercialReceipt
 * Professional, production-ready non-GST commercial receipt
 * used identically for on-screen modal preview and high-fidelity A4 printing.
 */
export default function OfficialCommercialReceipt({ receipt, className = "", id = "official-commercial-receipt" }) {
  if (!receipt) return null;

  const isPaid = receipt.status === "PAID" || receipt.transaction?.displayStatus?.toLowerCase().includes("successful") || receipt.transaction?.displayStatus?.toLowerCase() === "paid";
  const isRefunded = receipt.status === "REFUNDED" || (receipt.totalRefundedAmount >= receipt.subtotal && receipt.totalRefundedAmount > 0);
  const isPartiallyRefunded = receipt.status === "PARTIALLY_REFUNDED" || (receipt.totalRefundedAmount > 0 && receipt.totalRefundedAmount < receipt.subtotal);
  const isFailed = receipt.status === "FAILED" || receipt.transaction?.displayStatus?.toLowerCase().includes("failed");
  const isCancelled = receipt.status === "CANCELLED" || receipt.transaction?.displayStatus?.toLowerCase().includes("cancelled");

  const getStatusBadge = () => {
    if (isRefunded) {
      return <span className="receipt-status-badge badge-refunded">FULLY REFUNDED</span>;
    }
    if (isPartiallyRefunded) {
      return <span className="receipt-status-badge badge-partial">PARTIALLY REFUNDED</span>;
    }
    if (isPaid) {
      return <span className="receipt-status-badge badge-paid">PAYMENT SUCCESSFUL</span>;
    }
    if (isFailed) {
      return <span className="receipt-status-badge badge-failed">PAYMENT FAILED</span>;
    }
    if (isCancelled) {
      return <span className="receipt-status-badge badge-cancelled">ORDER CANCELLED</span>;
    }
    return <span className="receipt-status-badge badge-pending">PAYMENT PENDING</span>;
  };

  return (
    <div className={`official-receipt-card ${className}`} id={id}>
      {/* 1. Official Letterhead Header */}
      <div className="receipt-letterhead-header">
        <img
          src={letterheadHeader}
          alt="ETHOS DANCE STUDIO"
          className="letterhead-header-img"
          loading="eager"
        />
      </div>

      {/* 2. Document Title & Category */}
      <div className="receipt-title-block">
        <div className="receipt-title-text">
          <h1>OFFICIAL COMMERCIAL PAYMENT RECEIPT</h1>
          <span className="receipt-category-tag">COMMERCIAL VOUCHER • NON-GST</span>
        </div>
        <div className="receipt-status-wrap">
          {getStatusBadge()}
        </div>
      </div>

      {/* 3. Receipt Info Strip */}
      <div className="receipt-info-strip">
        <div className="info-col">
          <span className="info-label">RECEIPT NUMBER</span>
          <span className="info-value font-mono">{receipt.receiptNumber}</span>
        </div>
        <div className="info-col">
          <span className="info-label">ISSUE DATE & TIME</span>
          <span className="info-value">{formatAdminDateTime(receipt.receiptDate)}</span>
        </div>
        <div className="info-col">
          <span className="info-label">PAYMENT METHOD</span>
          <span className="info-value">{receipt.transaction?.paymentMethod || "Online / Razorpay"}</span>
        </div>
        <div className="info-col">
          <span className="info-label">CURRENCY</span>
          <span className="info-value">{receipt.currency || "INR"} (₹)</span>
        </div>
      </div>

      {/* 4. Parties Grid: Customer & Transaction Information */}
      <div className="receipt-grid-two-col">
        {/* Customer Information */}
        <div className="receipt-section-box">
          <div className="section-box-header">
            <h3>CUSTOMER INFORMATION</h3>
          </div>
          <div className="section-box-body">
            <div className="data-row">
              <span className="data-label">Full Name:</span>
              <span className="data-value font-bold">{receipt.customer?.fullName || "Valued Customer"}</span>
            </div>
            <div className="data-row">
              <span className="data-label">Customer Code:</span>
              <span className="data-value font-mono">{receipt.customer?.customerCode || "N/A"}</span>
            </div>
            <div className="data-row">
              <span className="data-label">Primary Phone:</span>
              <span className="data-value font-mono">{receipt.customer?.phone || "—"}</span>
            </div>
            <div className="data-row">
              <span className="data-label">Email Address:</span>
              <span className="data-value">{receipt.customer?.email || "Not provided"}</span>
            </div>
          </div>
        </div>

        {/* Transaction Information */}
        <div className="receipt-section-box">
          <div className="section-box-header">
            <h3>TRANSACTION RECORD</h3>
          </div>
          <div className="section-box-body">
            <div className="data-row">
              <span className="data-label">Transaction ID:</span>
              <span className="data-value font-mono text-break">{receipt.transaction?.transactionId}</span>
            </div>
            <div className="data-row">
              <span className="data-label">Payment Purpose:</span>
              <span className="data-value">{receipt.transaction?.purposeName || "Studio Service"}</span>
            </div>
            <div className="data-row">
              <span className="data-label">Gateway Order ID:</span>
              <span className="data-value font-mono">{receipt.transaction?.razorpayOrderId || "Not available"}</span>
            </div>
            <div className="data-row">
              <span className="data-label">Gateway Payment ID:</span>
              <span className="data-value font-mono">{receipt.transaction?.razorpayPaymentId || "Not available"}</span>
            </div>
            <div className="data-row">
              <span className="data-label">Gateway Verification:</span>
              <span className="data-value">
                {receipt.transaction?.isGatewayVerified ? (
                  <span className="text-verified">✓ Gateway Verified</span>
                ) : (
                  <span className="text-unverified">Unverified / Direct Checkout</span>
                )}
              </span>
            </div>
            <div className="data-row">
              <span className="data-label">Reconciliation State:</span>
              <span className="data-value">
                {receipt.transaction?.isReconciled ? (
                  <span className="text-reconciled">✓ Reconciled & Audited</span>
                ) : (
                  <span className="text-muted">Standard Processing</span>
                )}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* 5. Service / Item Breakdown Table */}
      <div className="receipt-table-section">
        <table className="receipt-line-table">
          <thead>
            <tr>
              <th className="col-desc">DESCRIPTION & SERVICE DETAILS</th>
              <th className="col-qty text-center">QTY</th>
              <th className="col-unit text-right">UNIT AMOUNT</th>
              <th className="col-total text-right">TOTAL (INR)</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td className="col-desc">
                <div className="item-main-title">
                  {receipt.transaction?.itemName || receipt.transaction?.itemDescription || "Studio Dance Services"}
                </div>
                {receipt.transaction?.itemDetails && (
                  <div className="item-subtext">{receipt.transaction.itemDetails}</div>
                )}
              </td>
              <td className="col-qty text-center font-mono">
                {receipt.transaction?.quantity || 1}
              </td>
              <td className="col-unit text-right font-mono">
                {formatAdminCurrency(receipt.transaction?.unitAmount || receipt.subtotal)}
              </td>
              <td className="col-total text-right font-bold font-mono">
                {formatAdminCurrency(receipt.subtotal)}
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      {/* 6. Amount Breakdown Summary */}
      <div className="receipt-summary-block">
        <div className="summary-rows-container">
          <div className="summary-row">
            <span className="summary-label">Subtotal Amount:</span>
            <span className="summary-value font-mono">{formatAdminCurrency(receipt.subtotal)}</span>
          </div>

          {receipt.totalRefundedAmount > 0 && (
            <div className="summary-row refund-adjustment">
              <span className="summary-label text-danger">Recorded Refund Adjustment:</span>
              <span className="summary-value font-mono text-danger">
                - {formatAdminCurrency(receipt.totalRefundedAmount)}
              </span>
            </div>
          )}

          <div className="summary-row net-total-row">
            <span className="summary-label">Net Amount Paid:</span>
            <span className="summary-value font-mono net-amount-figure">
              {formatAdminCurrency(receipt.netAmountPaid)}
            </span>
          </div>

          {(!isPaid && !isRefunded && !isPartiallyRefunded) && (
            <div className="payment-unpaid-advisory">
              ⚠️ {isFailed ? "Payment transaction declined by gateway. No studio funds captured." : isCancelled ? "Booking order was cancelled prior to completion." : "Payment transaction remains pending gateway settlement."}
            </div>
          )}
        </div>
      </div>

      {/* 7. Official Legal Notices */}
      <div className="receipt-legal-block">
        <div className="legal-notice-item">
          <strong>Notice:</strong> {receipt.disclaimer || "Ethos Dance Studio is not registered under GST. This document serves as an official commercial payment receipt for studio services rendered."}
        </div>
        <div className="legal-notice-item secondary-notice">
          <strong>Declaration:</strong> {receipt.systemGeneratedNotice || "This is a computer-generated commercial receipt and does not require a physical signature."}
        </div>
      </div>

      {/* 8. Official Letterhead Footer */}
      <div className="receipt-letterhead-footer">
        <img
          src={letterheadFooter}
          alt="Ethos Dance Studio Contact Details"
          className="letterhead-footer-img"
          loading="eager"
        />
      </div>
    </div>
  );
}
