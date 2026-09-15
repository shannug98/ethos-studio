import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import { useTrainerPermissions } from "../../hooks/useTrainerPermissions";
import LoadingState from "../../components/trainer/LoadingState";
import ErrorState from "../../components/trainer/ErrorState";
import ConfirmDialog from "../../components/trainer/ConfirmDialog";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import "./TrainerTier.css";

const formatDate = (value) => {
  if (!value) return "—";

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleDateString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
};

const formatFee = (value) => {
  if (value === null || value === undefined) return "—";

  return `₹${Number(value).toLocaleString("en-IN")}`;
};

const statusClass = (status) => {
  return String(status || "").toLowerCase();
};

export default function TrainerTier() {
  const mountedRef = useRef(true);
  const { canRequestUpgrade } = useTrainerPermissions();

  const [currentTier, setCurrentTier] = useState(null);
  const [tiers, setTiers] = useState([]);
  const [history, setHistory] = useState([]);
  const [requests, setRequests] = useState([]);

  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);

  const [error, setError] = useState("");
  const [actionError, setActionError] = useState("");
  const [successMessage, setSuccessMessage] = useState("");

  const [cancelRequestId, setCancelRequestId] = useState(null);
  const [confirmUpgradeTier, setConfirmUpgradeTier] = useState(null);

  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      setError("");

      const [
        currentTierResponse,
        tiersResponse,
        historyResponse,
        requestsResponse,
      ] = await Promise.all([
        trainerApi.getTier(),
        trainerApi.getTiers(),
        trainerApi.getTierHistory(),
        trainerApi.getUpgradeRequests(),
      ]);

      if (!mountedRef.current) return;

      const current =
        currentTierResponse?.data ?? currentTierResponse;

      const available =
        tiersResponse?.data ?? tiersResponse;

      const tierHistory =
        historyResponse?.data ?? historyResponse;

      const upgradeRequests =
        requestsResponse?.data ?? requestsResponse;

      setCurrentTier(current);

      setTiers(
        Array.isArray(available)
          ? [...available].sort(
              (a, b) =>
                Number(a.displayOrder) -
                Number(b.displayOrder)
            )
          : []
      );

      setHistory(
        Array.isArray(tierHistory)
          ? [...tierHistory].sort(
              (a, b) =>
                new Date(b.changedAt || b.assignedAt) -
                new Date(a.changedAt || a.assignedAt)
            )
          : []
      );

      setRequests(
        Array.isArray(upgradeRequests)
          ? [...upgradeRequests].sort(
              (a, b) =>
                new Date(b.createdAt || b.requestedAt) -
                new Date(a.createdAt || a.requestedAt)
            )
          : []
      );
    } catch (err) {
      if (!mountedRef.current) return;
      console.error("Failed to load trainer tier data:", err);

      setError(
        getApiErrorMessage(err, "Unable to load your tier information right now.")
      );
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    loadData();
    return () => {
      mountedRef.current = false;
    };
  }, [loadData]);

  useEffect(() => {
    if (!successMessage) return;

    const timer = setTimeout(() => {
      if (mountedRef.current) {
        setSuccessMessage("");
      }
    }, 4500);

    return () => clearTimeout(timer);
  }, [successMessage]);

  const currentDisplayOrder =
    Number(currentTier?.displayOrder) || 0;

  const tierOrder = useMemo(() => ["SILVER", "GOLD", "DIAMOND", "PLATINUM"], []);
  const currentTierCode = (currentTier?.code || "").toUpperCase();
  const currentTierIndex = tierOrder.indexOf(currentTierCode);
  const isTopTier =
    currentTierIndex >= 0
      ? currentTierIndex === tierOrder.length - 1
      : !tiers.some(
          (tier) =>
            Number(tier.displayOrder) > currentDisplayOrder &&
            tier.isActive !== false
        );

  const nextTier = useMemo(() => {
    if (isTopTier) return null;
    return (
      tiers.find(
        (tier) =>
          Number(tier.displayOrder) >
          currentDisplayOrder &&
          tier.isActive !== false
      ) || null
    );
  }, [tiers, currentDisplayOrder, isTopTier]);

  const hasPendingUpgrade = useMemo(() => {
    return requests.some(
      (request) =>
        request.status === "Pending" ||
        request.status === "PaymentPending" ||
        request.status === "PaymentVerified"
    );
  }, [requests]);

  const isTierRequestPending = (tierId) => {
    return requests.some(
      (request) =>
        (request.requestedTierId === tierId || request.requestedTier === tierId) &&
        (request.status === "Pending" ||
         request.status === "PaymentPending" ||
         request.status === "PaymentVerified")
    );
  };

  const handleConfirmDirectUpgrade = async () => {
    if (!confirmUpgradeTier) return;

    try {
      setActionLoading(true);
      setActionError("");

      await trainerApi.requestUpgrade({
        requestedTierId: confirmUpgradeTier.id,
      });

      if (!mountedRef.current) return;

      setSuccessMessage(
        `Your upgrade request to ${confirmUpgradeTier.name} has been submitted for admin review.`
      );

      setConfirmUpgradeTier(null);
      await loadData();
    } catch (err) {
      if (!mountedRef.current) return;
      console.error("Failed to submit tier upgrade request:", err);

      setActionError(
        getApiErrorMessage(err, "Unable to submit your upgrade request.")
      );
    } finally {
      if (mountedRef.current) {
        setActionLoading(false);
      }
    }
  };

  const executeCancelRequest = async () => {
    if (!cancelRequestId) return;

    try {
      setActionLoading(true);
      setActionError("");

      await trainerApi.cancelUpgradeRequest(cancelRequestId);
      if (!mountedRef.current) return;

      setSuccessMessage(
        "Your pending upgrade request has been cancelled."
      );
      setCancelRequestId(null);

      await loadData();
    } catch (err) {
      if (!mountedRef.current) return;
      console.error(
        "Failed to cancel upgrade request:",
        err
      );

      setActionError(
        getApiErrorMessage(err, "Unable to cancel this upgrade request.")
      );
    } finally {
      if (mountedRef.current) {
        setActionLoading(false);
      }
    }
  };

  if (loading) {
    return <LoadingState label="Loading tier information..." />;
  }

  if (error && !currentTier) {
    return (
      <ErrorState
        title="Tier information unavailable"
        description={error}
        action={
          <button
            type="button"
            className="tier-primary-button"
            onClick={loadData}
          >
            TRY AGAIN
          </button>
        }
      />
    );
  }

  return (
    <div className="trainer-tier-page">
      <div className="tier-shell">

        {/* HEADER */}

        <header className="tier-header">
          <div>
            <span className="tier-eyebrow">
              TRAINER PORTAL / TIER
            </span>

            <h1>
              Your place
              <br />
              <em>within Ethos.</em>
            </h1>

            <p>
              Your trainer pathway, progression history, and
              available opportunities for advancement.
            </p>
          </div>

          <div className="tier-header-mark">
            <span>ETHOS</span>
            <strong>
              {currentTier?.code || "—"}
            </strong>
          </div>
        </header>

        {/* SUCCESS */}

        {successMessage && (
          <div className="tier-success" role="status" aria-live="polite">
            <span>✓</span>
            {successMessage}
          </div>
        )}

        {actionError && (
          <div className="tier-action-error" role="alert">
            {actionError}
          </div>
        )}

        {/* CURRENT TIER */}

        <section className="current-tier-hero">
          <div className="current-tier-main">

            <div className="current-tier-index">
              CURRENT PATHWAY
            </div>

            <div className="current-tier-code">
              {currentTier?.code || "—"}
            </div>

            <h2>
              {currentTier?.name ||
                "Trainer Tier"}
            </h2>

            <p>
              {currentTier?.description ||
                "Your current Ethos trainer pathway."}
            </p>

            <div className="current-tier-meta">
              <div>
                <span>RANK</span>
                <strong>
                  {currentTier?.displayOrder ?? "—"}
                </strong>
              </div>

              <div>
                <span>STATUS</span>
                <strong>
                  {currentTier?.isActive
                    ? "ACTIVE"
                    : "INACTIVE"}
                </strong>
              </div>
            </div>
          </div>

          <div className="current-tier-emblem">
            <div className="tier-emblem-ring">
              <span>{currentTier?.code}</span>
            </div>

            <small>CURRENT TIER</small>
          </div>
        </section>

        {/* PROGRESSION */}

        <section className="tier-section">
          <div className="tier-section-heading">
            <div>
              <small>THE PATHWAY</small>
              <h2>Progress through the tiers.</h2>
            </div>
          </div>

          <div className="tier-progression">
            {tiers.map((tier, index) => {
              const isCurrent =
                tier.id === currentTier?.id ||
                tier.code === currentTier?.code;

              const isPast =
                Number(tier.displayOrder) <
                currentDisplayOrder;

              const isFuture =
                Number(tier.displayOrder) >
                currentDisplayOrder;

              const isPending =
                isTierRequestPending(tier.id);

              return (
                <div
                  className={`tier-step ${
                    isCurrent
                      ? "is-current"
                      : ""
                  } ${
                    isPast ? "is-past" : ""
                  } ${
                    isFuture ? "is-future" : ""
                  }`}
                  key={tier.id}
                >
                  <div className="tier-step-line">
                    <span />
                  </div>

                  <div className="tier-step-number">
                    {String(index + 1).padStart(2, "0")}
                  </div>

                  <div className="tier-step-content">
                    <div className="tier-step-top">
                      <span>{tier.code}</span>

                      {isCurrent && (
                        <b>CURRENT</b>
                      )}

                      {isPending && (
                        <b>PENDING</b>
                      )}
                    </div>

                    <h3>{tier.name}</h3>

                    <p>{tier.description}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        {/* HIGHEST TIER BANNER */}
        {isTopTier && (
          <div className="tier-top-status-banner">
            <div className="tier-top-status-content">
              <span className="tier-top-eyebrow">HIGHEST TIER</span>
              <div className="tier-top-divider" />
              <h3>YOU&apos;VE REACHED THE TOP</h3>
              <strong className="tier-top-name">
                {currentTier?.name || "Platinum Master"}
              </strong>
              <p>
                You have reached the highest Ethos trainer tier. There are no
                further pathway upgrades available.
              </p>
              <div className="tier-top-divider" />
            </div>
          </div>
        )}

        {/* UPGRADE PATHWAY */}
        {!isTopTier && (
          <section className="tier-section upgrade-pathway-section">
            <div className="tier-section-heading">
              <div>
                <small>UPGRADE PATHWAY</small>
                <h2>Ready for your next level?</h2>
              </div>
            </div>

            <div className="upgrade-pathway-card">
              <div className="upgrade-pathway-details">
                <div className="upgrade-pathway-tiers">
                  <div className="upgrade-tier-pill current">
                    <small>CURRENT</small>
                    <strong>{currentTier?.name || "Trainer"}</strong>
                  </div>
                  <span className="upgrade-tier-arrow">→</span>
                  <div className="upgrade-tier-pill next">
                    <small>NEXT</small>
                    <strong>{nextTier?.name || "Next Tier"}</strong>
                  </div>
                </div>

                <p className="upgrade-pathway-desc">
                  {nextTier?.description ||
                    `Progress to ${nextTier?.name} to unlock higher workshop privileges and scheduling priority.`}
                </p>

                {Number(nextTier?.upgradeFee) > 0 && (
                  <div className="upgrade-fee-info">
                    <span>UPGRADE REQUIREMENT</span>
                    <strong>{formatFee(nextTier?.upgradeFee)}</strong>
                  </div>
                )}
              </div>

              <div className="upgrade-pathway-actions">
                {hasPendingUpgrade ? (
                  <button
                    type="button"
                    className="tier-primary-button upgrade-pending-button"
                    disabled
                  >
                    UPGRADE REQUEST PENDING
                  </button>
                ) : (
                  <button
                    type="button"
                    className="tier-primary-button"
                    disabled={actionLoading || !nextTier}
                    onClick={() => setConfirmUpgradeTier(nextTier)}
                  >
                    REQUEST TIER UPGRADE →
                  </button>
                )}
              </div>
            </div>
          </section>
        )}

        {/* REQUEST HISTORY */}
        {!isTopTier && (
          <section className="tier-section">
            <div className="tier-section-heading">
              <div>
                <small>REQUEST HISTORY</small>
                <h2>Upgrade requests.</h2>
              </div>
            </div>

            {requests.length === 0 ? (
              <div className="tier-empty">
                <span>NO REQUESTS YET</span>
                <h3>Your upgrade requests will appear here.</h3>
                <p>
                  When you request a move to a higher trainer tier, its review
                  status will be tracked in this section.
                </p>
              </div>
            ) : (
              <div className="upgrade-request-list">
                {requests.map((request) => {
                  const reqStatus = String(request.status || "").toLowerCase();
                  const isPending = reqStatus === "pending";

                  return (
                    <article className="upgrade-request-row" key={request.id}>
                      <div className="upgrade-request-info">
                        <strong className="upgrade-request-tier-name">
                          {request.requestedTierName || request.requestedTier || "Tier Upgrade"}
                        </strong>
                        <span className="upgrade-request-date-meta">
                          Submitted — {formatDate(request.requestedAt || request.createdAt)}
                        </span>
                      </div>

                      <div className="upgrade-request-right">
                        <div className={`upgrade-status-badge status-${reqStatus}`}>
                          {reqStatus === "pending"
                            ? "PENDING REVIEW"
                            : String(request.status).toUpperCase()}
                        </div>

                        {isPending && (
                          <button
                            type="button"
                            className="cancel-request-button"
                            disabled={actionLoading}
                            onClick={() => setCancelRequestId(request.id)}
                          >
                            CANCEL
                          </button>
                        )}
                      </div>
                    </article>
                  );
                })}
              </div>
            )}
          </section>
        )}

        {/* TIER HISTORY */}

        <section className="tier-section">
          <div className="tier-section-heading">
            <div>
              <small>TIER HISTORY</small>
              <h2>Your progression.</h2>
            </div>
          </div>

          {history.length === 0 ? (
            <div className="tier-empty">
              <span>NO HISTORY</span>

              <h3>
                Your tier progression will appear here.
              </h3>

              <p>
                Tier assignments and changes are recorded
                by Ethos.
              </p>
            </div>
          ) : (
            <div className="tier-history">

              {history.map((item, index) => (
                <article
                  className="tier-history-row"
                  key={item.id}
                >
                  <div className="tier-history-marker">
                    <span>
                      {String(
                        history.length - index
                      ).padStart(2, "0")}
                    </span>
                  </div>

                  <div className="tier-history-content">
                    <div className="tier-history-date">
                      {formatDate(
                        item.changedAt ||
                          item.assignedAt
                      )}
                    </div>

                    <h3>
                      {item.newTier ||
                        item.tierName}
                    </h3>

                    <div className="tier-history-transition">
                      {item.previousTier ? (
                        <>
                          <span>
                            {item.previousTier}
                          </span>

                          <b>→</b>

                          <strong>
                            {item.newTier ||
                              item.tierName}
                          </strong>
                        </>
                      ) : (
                        <strong>
                          Initial tier assignment
                        </strong>
                      )}
                    </div>

                    {item.reason && (
                      <p>{item.reason}</p>
                    )}
                  </div>

                  <div className="tier-history-code">
                    {item.tierCode}
                  </div>
                </article>
              ))}

            </div>
          )}
        </section>

        {/* FOOTER */}

        <div className="tier-footer-nav">
          <Link to="/trainer/performance">
            ← PERFORMANCE
          </Link>

          <Link to="/trainer/dashboard">
            TRAINER DASHBOARD →
          </Link>
        </div>

      </div>

      {/* CONFIRMATION DIALOGS */}
      <ConfirmDialog
        open={Boolean(cancelRequestId)}
        title="Cancel Upgrade Request?"
        description="Are you sure you want to cancel this pending tier upgrade request?"
        confirmLabel="CANCEL REQUEST"
        cancelLabel="KEEP REQUEST"
        onConfirm={executeCancelRequest}
        onCancel={() => setCancelRequestId(null)}
        loading={actionLoading}
        destructive
      />

      <ConfirmDialog
        open={Boolean(confirmUpgradeTier)}
        title="Request Tier Upgrade"
        description={`Submit an upgrade request to ${confirmUpgradeTier?.name}? Your request will be sent to Ethos administrators for review. The upgrade requirement for this pathway is ${formatFee(confirmUpgradeTier?.upgradeFee)}.`}
        confirmLabel={actionLoading ? "SUBMITTING..." : "CONFIRM REQUEST"}
        cancelLabel="CANCEL"
        onConfirm={handleConfirmDirectUpgrade}
        onCancel={() => setConfirmUpgradeTier(null)}
        loading={actionLoading}
      />
    </div>
  );
}
