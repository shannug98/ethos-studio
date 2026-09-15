import React, { useState, useEffect } from "react";
import { adminApi } from "../../services/adminApi";
import AdminKpiCard from "../../components/admin/common/AdminKpiCard";
import AdminBadge from "../../components/admin/common/AdminBadge";
import AdminDataTable from "../../components/admin/common/AdminDataTable";
import AdminActionModal from "../../components/admin/common/AdminActionModal";
import OfficialCommercialReceipt from "../../components/admin/finance/OfficialCommercialReceipt";
import PaymentDetailsDrawer from "../../components/admin/finance/PaymentDetailsDrawer";
import ResolvePaymentIssueModal from "../../components/admin/finance/ResolvePaymentIssueModal";
import {
  formatAdminCurrency,
  formatAdminDateTime,
  formatAdminInteger,
} from "../../utils/adminFormatters";
import "./AdminPayments.css";

const PURPOSE_OPTIONS = [
  { value: "all", label: "All Items / Purposes" },
  { value: "1", label: "Dance Packages" },
  { value: "5", label: "Workshop Registrations" },
  { value: "2", label: "Trainer Applications" },
  { value: "3", label: "Trainer Tier Upgrades" },
];

const STATUS_FILTERS = [
  { value: "all", label: "All Payments" },
  { value: "needs_attention", label: "⚠️ Needs Attention" },
  { value: "4", label: "Successful (Paid)" },
  { value: "5", label: "Failed / Unfinished" },
  { value: "pending", label: "Pending Confirmation" },
  { value: "refunded", label: "Refunded" },
];

export default function AdminPayments() {
  const [activeTab, setActiveTab] = useState("transactions"); // 'transactions' | 'payouts'
  const [revenueSummary, setRevenueSummary] = useState(null);
  const [transactions, setTransactions] = useState([]);
  const [trainerPayouts, setTrainerPayouts] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Filters & Search
  const [search, setSearch] = useState("");
  const [purposeFilter, setPurposeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);

  // Drawer & Action Modals
  const [detailDrawerTx, setDetailDrawerTx] = useState(null);
  const [receiptModal, setReceiptModal] = useState({
    open: false,
    receipt: null,
    loading: false,
  });

  const [refundModal, setRefundModal] = useState({
    open: false,
    transaction: null,
    refundAmount: "",
    gatewayRefundId: "",
    reason: "",
    notes: "",
    submitting: false,
    error: null,
  });

  const [resolveModal, setResolveModal] = useState({
    open: false,
    transaction: null,
  });

  // Trainer Payout Modal
  const [payoutModal, setPayoutModal] = useState({
    open: false,
    trainer: null,
    amount: "",
    payoutReference: "",
    notes: "",
    submitting: false,
    error: null,
  });

  const loadRevenue = async () => {
    try {
      const rev = await adminApi.getRevenue();
      setRevenueSummary(rev);
    } catch (err) {
      console.error("Failed to load revenue summary", err);
    }
  };

  const loadTransactions = async () => {
    setLoading(true);
    setError(null);
    try {
      let params = `page=${page}&pageSize=${pageSize}`;
      if (search.trim()) {
        params += `&search=${encodeURIComponent(search.trim())}`;
      }
      if (purposeFilter !== "all") {
        params += `&purpose=${purposeFilter}`;
      }

      if (statusFilter === "needs_attention") {
        params += "&needsAttentionOnly=true";
      } else if (statusFilter === "pending") {
        params += "&status=2"; // OrderCreated
      } else if (statusFilter === "refunded") {
        params += "&status=7"; // Refunded
      } else if (statusFilter !== "all") {
        params += `&status=${statusFilter}`;
      }

      const res = await adminApi.getPayments(params);
      setTransactions(res.items || []);
      setTotalCount(res.totalCount || 0);
    } catch (err) {
      setError(err.message || "Failed to load payment transactions.");
    } finally {
      setLoading(false);
    }
  };

  const loadTrainerPayouts = async () => {
    try {
      const res = await adminApi.getTrainerPayouts();
      setTrainerPayouts(res);
    } catch (err) {
      console.error("Failed to load trainer payouts", err);
    }
  };

  const loadAllData = () => {
    loadRevenue();
    if (activeTab === "transactions") {
      loadTransactions();
    } else {
      loadTrainerPayouts();
    }
  };

  useEffect(() => {
    loadRevenue();
  }, []);

  useEffect(() => {
    if (activeTab === "transactions") {
      loadTransactions();
    } else {
      loadTrainerPayouts();
    }
  }, [activeTab, page, search, purposeFilter, statusFilter]);

  // Open Receipt Modal
  const handleViewReceipt = async (txId) => {
    setReceiptModal({ open: true, receipt: null, loading: true });
    try {
      const receipt = await adminApi.getPaymentReceipt(txId);
      setReceiptModal({ open: true, receipt, loading: false });
    } catch (err) {
      alert(err.message || "Failed to generate receipt.");
      setReceiptModal({ open: false, receipt: null, loading: false });
    }
  };

  // Open Refund Modal
  const handleOpenRefund = (tx) => {
    const defaultRefund = (tx.remainingRefundableAmount ?? tx.amount).toString();
    setRefundModal({
      open: true,
      transaction: tx,
      refundAmount: defaultRefund,
      gatewayRefundId: "",
      reason: "",
      notes: "",
      submitting: false,
      error: null,
    });
  };

  const handleConfirmRefund = async (e) => {
    e.preventDefault();
    const { transaction, refundAmount, gatewayRefundId, reason, notes } = refundModal;
    const amt = parseFloat(refundAmount);
    const maxRefund = transaction.remainingRefundableAmount ?? transaction.amount;

    if (isNaN(amt) || amt <= 0) {
      setRefundModal((prev) => ({ ...prev, error: "Refund amount must be greater than zero." }));
      return;
    }
    if (amt > maxRefund) {
      setRefundModal((prev) => ({
        ...prev,
        error: `Refund amount (₹${amt}) exceeds remaining refundable balance (₹${maxRefund}).`,
      }));
      return;
    }
    if (!gatewayRefundId.trim()) {
      setRefundModal((prev) => ({
        ...prev,
        error: "External Gateway Refund ID / Reference is strictly mandatory.",
      }));
      return;
    }
    if (!reason.trim()) {
      setRefundModal((prev) => ({
        ...prev,
        error: "A mandatory administrative reason is required.",
      }));
      return;
    }

    setRefundModal((prev) => ({ ...prev, submitting: true, error: null }));
    try {
      await adminApi.recordRefund(
        transaction.id,
        amt,
        reason.trim(),
        gatewayRefundId.trim(),
        notes.trim() || "Refund recorded via Admin Portal"
      );
      setRefundModal({
        open: false,
        transaction: null,
        refundAmount: "",
        gatewayRefundId: "",
        reason: "",
        notes: "",
        submitting: false,
        error: null,
      });
      loadAllData();
    } catch (err) {
      setRefundModal((prev) => ({
        ...prev,
        submitting: false,
        error: err.message || "Failed to record refund.",
      }));
    }
  };

  // Open Resolve Payment Issue Modal
  const handleOpenResolve = (tx) => {
    setResolveModal({
      open: true,
      transaction: tx,
    });
  };

  const handleResolutionSuccess = (result) => {
    loadAllData();
    if (detailDrawerTx && detailDrawerTx.id === resolveModal.transaction?.id) {
      // Refresh detail drawer if open
      adminApi.getPaymentById(detailDrawerTx.id).then((freshTx) => {
        if (freshTx) setDetailDrawerTx(freshTx);
      }).catch(() => {});
    }
  };

  // Trainer Payout Handlers
  const handleOpenPayout = (item) => {
    setPayoutModal({
      open: true,
      trainer: item,
      amount: item.trainerPayoutAmount.toString(),
      payoutReference: "",
      notes: "",
      submitting: false,
      error: null,
    });
  };

  const handleConfirmPayout = async (e) => {
    e.preventDefault();
    const { trainer, amount, payoutReference, notes } = payoutModal;
    const amt = parseFloat(amount);
    if (isNaN(amt) || amt <= 0) {
      setPayoutModal((prev) => ({ ...prev, error: "Payout amount must be greater than zero." }));
      return;
    }
    if (amt > trainer.trainerPayoutAmount) {
      setPayoutModal((prev) => ({
        ...prev,
        error: `Payout amount (₹${amt}) exceeds calculated share (₹${trainer.trainerPayoutAmount}).`,
      }));
      return;
    }
    if (!payoutReference.trim()) {
      setPayoutModal((prev) => ({
        ...prev,
        error: "Bank Transfer Reference / UTR is mandatory.",
      }));
      return;
    }

    setPayoutModal((prev) => ({ ...prev, submitting: true, error: null }));
    try {
      await adminApi.processTrainerPayout(
        trainer.trainerId,
        amt,
        payoutReference.trim(),
        notes.trim() || null
      );
      setPayoutModal({
        open: false,
        trainer: null,
        amount: "",
        payoutReference: "",
        notes: "",
        submitting: false,
        error: null,
      });
      loadTrainerPayouts();
      loadRevenue();
    } catch (err) {
      setPayoutModal((prev) => ({
        ...prev,
        submitting: false,
        error: err.message || "Failed to process payout.",
      }));
    }
  };

  const renderStatusBadge = (displayStatus, statusId) => {
    switch (statusId) {
      case 4:
        return <AdminBadge tone="success">Paid / Successful</AdminBadge>;
      case 5:
        return <AdminBadge tone="danger">Payment Failed</AdminBadge>;
      case 2:
        return <AdminBadge tone="info">Order Created</AdminBadge>;
      case 3:
        return <AdminBadge tone="warning">Payment Pending</AdminBadge>;
      case 6:
        return <AdminBadge tone="neutral">Cancelled</AdminBadge>;
      case 7:
        return <AdminBadge tone="neutral">Refunded</AdminBadge>;
      case 8:
        return <AdminBadge tone="warning">Partially Refunded</AdminBadge>;
      default:
        return <AdminBadge tone="neutral">{displayStatus || "Unknown"}</AdminBadge>;
    }
  };

  const columns = [
    {
      key: "customer",
      header: "Customer",
      render: (row) => (
        <div className="customer-cell">
          <span className="customer-name font-semibold">{row.userName || "Guest Attendee"}</span>
          <div className="sub-text">
            {row.userPhone || "No phone"} • {row.userCustomerCode || "Guest"}
          </div>
        </div>
      ),
    },
    {
      key: "purchase",
      header: "Purchased Item",
      render: (row) => (
        <div className="purchase-cell">
          <span className="purchase-name font-semibold">{row.itemName || "Item"}</span>
          <div className="sub-text">
            {row.displayPurpose || "Purchase"}
          </div>
        </div>
      ),
    },
    {
      key: "amount",
      header: "Amount",
      render: (row) => (
        <div className="amount-cell">
          <span className="amount-text font-semibold">{formatAdminCurrency(row.amount)}</span>
          {row.totalRefundedAmount > 0 && (
            <span className="refund-subtext text-amber">
              - {formatAdminCurrency(row.totalRefundedAmount)} refunded
            </span>
          )}
        </div>
      ),
    },
    {
      key: "outcome",
      header: "Payment Outcome",
      render: (row) => (
        <div className="outcome-cell">
          {renderStatusBadge(row.displayStatus, row.status)}
          {row.hasCustomerReportedDiscrepancy && (
            <span className="discrepancy-pill" title="Customer reports deduction">
              ⚠️ Deduction Claimed
            </span>
          )}
        </div>
      ),
    },
    {
      key: "fulfillment",
      header: "Delivery / Fulfillment",
      render: (row) => (
        <div className="fulfillment-cell">
          {row.fulfillmentStatus === "Active / Confirmed" && (
            <span className="text-success font-medium">✓ Granted</span>
          )}
          {row.fulfillmentStatus === "Pending Confirmation" && (
            <span className="text-warning font-medium">⏳ Pending</span>
          )}
          {row.fulfillmentStatus === "Not Granted (Payment Failed)" && (
            <span className="text-danger font-medium">✕ Not Granted</span>
          )}
          {!["Active / Confirmed", "Pending Confirmation", "Not Granted (Payment Failed)"].includes(
            row.fulfillmentStatus
          ) && (
            <span className="text-muted">{row.fulfillmentStatus || "—"}</span>
          )}

          {row.actionNeeded && (
            <div className="table-action-needed-chip">
              {row.actionNeeded}
            </div>
          )}
        </div>
      ),
    },
    {
      key: "date",
      header: "Date & Time",
      render: (row) => (
        <div className="date-cell">
          <span className="timestamp-text">
            {formatAdminDateTime(row.paidAt || row.createdAt)}
          </span>
        </div>
      ),
    },
    {
      key: "actions",
      header: "Actions",
      render: (row) => (
        <div className="action-buttons-cell" onClick={(e) => e.stopPropagation()}>
          <button
            type="button"
            className="admin-btn secondary small"
            onClick={(e) => {
              e.stopPropagation();
              setDetailDrawerTx(row);
            }}
            title="View customer, financial breakdown, and event history"
          >
            Details
          </button>

          {(row.status === 4 || row.status === 8) && (
            <button
              type="button"
              className="admin-btn secondary small"
              onClick={(e) => {
                e.stopPropagation();
                handleViewReceipt(row.id);
              }}
              title="Generate and view non-GST commercial receipt"
            >
              Receipt
            </button>
          )}

          {(row.status === 4 || row.status === 8) && (
            <button
              type="button"
              className="admin-btn danger small"
              onClick={(e) => {
                e.stopPropagation();
                handleOpenRefund(row);
              }}
              title="Record verified gateway refund"
            >
              Refund
            </button>
          )}

          {/* Governed Administrative Resolution */}
          {row.status !== 7 && (
            <button
              type="button"
              className={`admin-btn small ${row.status === 5 ? "primary" : "secondary"}`}
              onClick={(e) => {
                e.stopPropagation();
                handleOpenResolve(row);
              }}
              title="Resolve customer deduction report or manually confirm payment"
            >
              Resolve Issue
            </button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="admin-payments-container">
      {/* Top Header */}
      <div className="payments-header">
        <div>
          <h1>Payments & Finance</h1>
          <p className="subtitle">
            Review customer payments, resolve payment issues, issue official receipts, process refunds, and manage trainer payouts.
          </p>
        </div>
        <button
          type="button"
          className="admin-btn secondary refresh-btn"
          onClick={loadAllData}
          disabled={loading}
        >
          {loading ? "Refreshing..." : "↻ Refresh"}
        </button>
      </div>

      {/* 6 Practical Administrative KPI Cards */}
      {revenueSummary && (
        <div className="finance-kpis-grid">
          <AdminKpiCard
            title="Money Received"
            value={formatAdminCurrency(revenueSummary.totalSuccessfulRevenue)}
            subtitle={`Gross: ${formatAdminCurrency(
              revenueSummary.grossSuccessfulRevenue
            )} • Refunds: ${formatAdminCurrency(
              revenueSummary.totalRefundedRevenue
            )}`}
            tone="success"
          />
          <AdminKpiCard
            title="Payments Needing Attention"
            value={formatAdminInteger(revenueSummary.paymentsNeedingAttentionCount || 0)}
            subtitle="Unresolved issues or pending checks"
            tone={revenueSummary.paymentsNeedingAttentionCount > 0 ? "warning" : "neutral"}
          />
          <AdminKpiCard
            title="Customer Issue Reports"
            value={formatAdminInteger(revenueSummary.customerIssuesCount || 0)}
            subtitle="Claimed bank deductions to verify"
            tone={revenueSummary.customerIssuesCount > 0 ? "danger" : "neutral"}
          />
          <AdminKpiCard
            title="Successful Payments"
            value={formatAdminInteger(revenueSummary.successfulTransactionsCount)}
            subtitle="Fulfilled orders to date"
            tone="info"
          />
          <AdminKpiCard
            title="Refunds Issued"
            value={formatAdminInteger(revenueSummary.refundsIssuedCount || 0)}
            subtitle={`Total: ${formatAdminCurrency(revenueSummary.totalRefundedRevenue || 0)}`}
            tone="neutral"
          />
          <AdminKpiCard
            title="Trainer Payouts Pending"
            value={formatAdminCurrency(revenueSummary.trainerPayoutsPendingAmount || 0)}
            subtitle={`${formatAdminInteger(revenueSummary.trainerPayoutsPendingCount || 0)} trainers eligible`}
            tone="neutral"
          />
        </div>
      )}

      {/* Tab Navigation */}
      <div className="payments-tabs">
        <button
          type="button"
          className={`tab-btn ${activeTab === "transactions" ? "active" : ""}`}
          onClick={() => {
            setActiveTab("transactions");
            setPage(1);
          }}
        >
          Customer Payments & Issues
        </button>
        <button
          type="button"
          className={`tab-btn ${activeTab === "payouts" ? "active" : ""}`}
          onClick={() => setActiveTab("payouts")}
        >
          Trainer Payouts
        </button>
      </div>

      {error && <div className="error-state-banner">Error: {error}</div>}

      {/* Transactions Tab */}
      {activeTab === "transactions" && (
        <div className="transactions-view-section">
          {/* Filters Bar */}
          <div className="payments-filters-bar">
            <div className="search-input-group">
              <span className="search-icon" aria-hidden="true">🔍</span>
              <input
                type="text"
                className="admin-search-input"
                placeholder="Search by customer name, phone, customer code, or Razorpay ID..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              {search && (
                <button
                  type="button"
                  className="clear-search-btn"
                  onClick={() => setSearch("")}
                >
                  ✕
                </button>
              )}
            </div>

            <div className="filters-right-group">
              <select
                className="filter-select"
                value={purposeFilter}
                onChange={(e) => {
                  setPurposeFilter(e.target.value);
                  setPage(1);
                }}
                aria-label="Filter by Item Type"
              >
                {PURPOSE_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>

              <select
                className="filter-select"
                value={statusFilter}
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setPage(1);
                }}
                aria-label="Filter by Payment Status"
              >
                {STATUS_FILTERS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Data Table */}
          <AdminDataTable
            columns={columns}
            data={transactions}
            loading={loading}
            page={page}
            pageSize={pageSize}
            totalItems={totalCount}
            onPageChange={(newPage) => setPage(newPage)}
            emptyMessage={
              search || purposeFilter !== "all" || statusFilter !== "all"
                ? "No payment records match the selected filter criteria."
                : "No payment records available in the studio ledger."
            }
            onRowClick={(row) => {
              setDetailDrawerTx(row);
            }}
          />
        </div>
      )}

      {/* Payment Details Drawer */}
      {detailDrawerTx && (
        <PaymentDetailsDrawer
          transaction={detailDrawerTx}
          onClose={() => setDetailDrawerTx(null)}
          onOpenReceipt={(txId) => {
            setDetailDrawerTx(null);
            handleViewReceipt(txId);
          }}
          onOpenRefund={(tx) => {
            setDetailDrawerTx(null);
            handleOpenRefund(tx);
          }}
          onOpenResolve={(tx) => {
            handleOpenResolve(tx);
          }}
        />
      )}

      {/* Resolve Payment Issue Modal */}
      {resolveModal.open && (
        <ResolvePaymentIssueModal
          isOpen={resolveModal.open}
          transaction={resolveModal.transaction}
          onClose={() => setResolveModal({ open: false, transaction: null })}
          onSuccess={handleResolutionSuccess}
        />
      )}

      {/* Trainer Payouts Tab */}
      {activeTab === "payouts" && (
        <div className="payouts-view-section">
          {trainerPayouts && (
            <div className="payout-summary-banner">
              <div className="summary-stat-block">
                <span className="stat-label">Active Trainers</span>
                <span className="stat-number">{trainerPayouts.summary.totalTrainers}</span>
              </div>
              <div className="summary-stat-block">
                <span className="stat-label">Workshop Ticket Revenue</span>
                <span className="stat-number">
                  {formatAdminCurrency(trainerPayouts.summary.totalGrossRevenue)}
                </span>
              </div>
              <div className="summary-stat-block">
                <span className="stat-label">Total Trainer Share</span>
                <span className="stat-number text-green">
                  {formatAdminCurrency(trainerPayouts.summary.totalTrainerPayouts)}
                </span>
              </div>
              <div className="summary-stat-block">
                <span className="stat-label">Studio Retained Share</span>
                <span className="stat-number text-blue">
                  {formatAdminCurrency(trainerPayouts.summary.totalStudioRetention)}
                </span>
              </div>
            </div>
          )}

          <div className="table-wrapper">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Trainer</th>
                  <th>Tier & Split Policy</th>
                  <th>Workshops / Bookings</th>
                  <th>Workshop Ticket Revenue</th>
                  <th>Trainer Earnings</th>
                  <th>Studio Revenue Share</th>
                  <th>Payout Status</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {!trainerPayouts || trainerPayouts.payouts.length === 0 ? (
                  <tr>
                    <td colSpan="8" className="empty-row">
                      No trainer payout calculations available.
                    </td>
                  </tr>
                ) : (
                  trainerPayouts.payouts.map((item) => (
                    <tr key={item.trainerId}>
                      <td>
                        <strong>{item.fullName}</strong>
                        <div className="sub-text">
                          {item.trainerCode} • {item.phone || "No phone"}
                        </div>
                      </td>
                      <td>
                        <span className="badge badge-package">{item.tierName}</span>
                        <div className="sub-text split-policy-details">
                          {item.hasConfiguredCommission ? (
                            <>
                              <div>Trainer receives {item.trainerSharePercentage}%</div>
                              <div>Studio retains {item.studioSharePercentage}%</div>
                            </>
                          ) : (
                            <span className="text-amber">Unconfigured Policy</span>
                          )}
                        </div>
                      </td>
                      <td>
                        <span className="font-medium">{item.totalWorkshops} workshops</span>
                        <div className="sub-text">{item.totalBookings} confirmed bookings</div>
                      </td>
                      <td>
                        <strong>{formatAdminCurrency(item.grossRevenue)}</strong>
                      </td>
                      <td className="text-green font-semibold">
                        {formatAdminCurrency(item.trainerPayoutAmount)}
                      </td>
                      <td className="text-blue font-semibold">
                        {formatAdminCurrency(item.studioRetentionAmount)}
                      </td>
                      <td>
                        {item.trainerPayoutAmount === 0 ? (
                          <span className="badge badge-neutral">Settled</span>
                        ) : (
                          <span className="badge badge-pending">Eligible for Payout</span>
                        )}
                      </td>
                      <td>
                        {item.trainerPayoutAmount > 0 ? (
                          <button
                            type="button"
                            className="admin-btn primary small"
                            onClick={() => handleOpenPayout(item)}
                          >
                            Record Payout
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="admin-btn secondary small"
                            disabled
                          >
                            No Balance
                          </button>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Official Commercial Receipt Modal (Intact!) */}
      {receiptModal.open && (
        <OfficialCommercialReceipt
          receipt={receiptModal.receipt}
          loading={receiptModal.loading}
          onClose={() => setReceiptModal({ open: false, receipt: null, loading: false })}
        />
      )}

      {/* Record Refund Modal */}
      <AdminActionModal
        isOpen={refundModal.open}
        onClose={() =>
          setRefundModal({
            open: false,
            transaction: null,
            refundAmount: "",
            gatewayRefundId: "",
            reason: "",
            notes: "",
            submitting: false,
            error: null,
          })
        }
        title="Record Refund"
        subtitle={`Transaction: #${refundModal.transaction?.id?.substring(0, 8)} • Max Refundable: ${formatAdminCurrency(
          refundModal.transaction?.remainingRefundableAmount ?? refundModal.transaction?.amount ?? 0
        )}`}
        tone="danger"
        maxWidth="540px"
      >
        <form onSubmit={handleConfirmRefund}>
          {refundModal.error && (
            <div className="modal-error-banner">{refundModal.error}</div>
          )}

          <div className="form-group">
            <label>
              Refund Amount (₹) <span className="req">*</span>
            </label>
            <input
              type="number"
              step="0.01"
              value={refundModal.refundAmount}
              onChange={(e) =>
                setRefundModal((prev) => ({ ...prev, refundAmount: e.target.value }))
              }
              max={
                refundModal.transaction?.remainingRefundableAmount ??
                refundModal.transaction?.amount ??
                undefined
              }
              required
            />
            <small className="help-text">
              Enter amount to refund. Must not exceed remaining refundable balance.
            </small>
          </div>

          <div className="form-group">
            <label>
              Gateway Refund ID / Reference <span className="req">*</span>
            </label>
            <input
              type="text"
              placeholder="e.g. rfnd_xxxxxxxxxxxx"
              value={refundModal.gatewayRefundId}
              onChange={(e) =>
                setRefundModal((prev) => ({
                  ...prev,
                  gatewayRefundId: e.target.value,
                }))
              }
              required
            />
            <small className="help-text">
              Proof of external refund issued by Razorpay payment gateway.
            </small>
          </div>

          <div className="form-group">
            <label>
              Administrative Justification <span className="req">*</span>
            </label>
            <textarea
              placeholder="Mandatory administrative justification for this refund record..."
              rows="3"
              value={refundModal.reason}
              onChange={(e) =>
                setRefundModal((prev) => ({ ...prev, reason: e.target.value }))
              }
              required
            />
          </div>

          <div className="form-group">
            <label>Internal Audit Notes</label>
            <textarea
              placeholder="Optional administrative notes..."
              rows="2"
              value={refundModal.notes}
              onChange={(e) =>
                setRefundModal((prev) => ({ ...prev, notes: e.target.value }))
              }
            />
          </div>

          <div className="modal-form-actions">
            <button
              type="button"
              className="admin-btn secondary"
              disabled={refundModal.submitting}
              onClick={() =>
                setRefundModal({
                  open: false,
                  transaction: null,
                  refundAmount: "",
                  gatewayRefundId: "",
                  reason: "",
                  notes: "",
                  submitting: false,
                  error: null,
                })
              }
            >
              Cancel
            </button>
            <button
              type="submit"
              className="admin-btn danger"
              disabled={refundModal.submitting}
            >
              {refundModal.submitting ? "Recording Refund..." : "Confirm & Record Refund"}
            </button>
          </div>
        </form>
      </AdminActionModal>

      {/* Trainer Payout Modal */}
      <AdminActionModal
        isOpen={payoutModal.open}
        onClose={() =>
          setPayoutModal({
            open: false,
            trainer: null,
            amount: "",
            payoutReference: "",
            notes: "",
            submitting: false,
            error: null,
          })
        }
        title="Record Trainer Payout"
        subtitle={`Recipient: ${payoutModal.trainer?.fullName} (${payoutModal.trainer?.tierName} Tier)`}
        tone="primary"
        maxWidth="540px"
      >
        <form onSubmit={handleConfirmPayout}>
          {payoutModal.error && (
            <div className="modal-error-banner">{payoutModal.error}</div>
          )}

          <div className="form-group">
            <label>
              Payout Amount (₹) <span className="req">*</span>
            </label>
            <input
              type="number"
              step="0.01"
              value={payoutModal.amount}
              onChange={(e) =>
                setPayoutModal((prev) => ({ ...prev, amount: e.target.value }))
              }
              required
            />
            <small className="help-text">
              Eligible calculated share: {formatAdminCurrency(payoutModal.trainer?.trainerPayoutAmount || 0)}
            </small>
          </div>

          <div className="form-group">
            <label>
              Bank Transfer / UTR Reference <span className="req">*</span>
            </label>
            <input
              type="text"
              placeholder="e.g. UTR / IMPS / Bank Transaction Reference"
              value={payoutModal.payoutReference}
              onChange={(e) =>
                setPayoutModal((prev) => ({ ...prev, payoutReference: e.target.value }))
              }
              required
            />
          </div>

          <div className="form-group">
            <label>Administrative Notes</label>
            <textarea
              placeholder="Optional notes regarding this transfer..."
              rows="2"
              value={payoutModal.notes}
              onChange={(e) =>
                setPayoutModal((prev) => ({ ...prev, notes: e.target.value }))
              }
            />
          </div>

          <div className="modal-form-actions">
            <button
              type="button"
              className="admin-btn secondary"
              disabled={payoutModal.submitting}
              onClick={() =>
                setPayoutModal({
                  open: false,
                  trainer: null,
                  amount: "",
                  payoutReference: "",
                  notes: "",
                  submitting: false,
                  error: null,
                })
              }
            >
              Cancel
            </button>
            <button
              type="submit"
              className="admin-btn primary"
              disabled={payoutModal.submitting}
            >
              {payoutModal.submitting ? "Recording..." : "Record External Payout"}
            </button>
          </div>
        </form>
      </AdminActionModal>
    </div>
  );
}
