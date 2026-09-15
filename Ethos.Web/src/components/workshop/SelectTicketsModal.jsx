import React, { useState, useEffect } from "react";
import { X, Plus, Minus, AlertCircle } from "lucide-react";
import { workshopsApi } from "../../services/workshopsApi";
import "./SelectTicketsModal.css";

export default function SelectTicketsModal({
  isOpen,
  onClose,
  workshop,
  pricing,
  onContinue,
}) {
  const [quantity, setQuantity] = useState(1);
  const [quote, setQuote] = useState(null);
  const [loadingQuote, setLoadingQuote] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setQuantity(1);
    }
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen || !workshop?.id) return;

    let isMounted = true;
    async function fetchQuote() {
      setLoadingQuote(true);
      try {
        const data = await workshopsApi.getWorkshopQuote(workshop.id, quantity);
        if (isMounted) setQuote(data);
      } catch (err) {
        console.error("Quote fetch error:", err);
      } finally {
        if (isMounted) setLoadingQuote(false);
      }
    }

    fetchQuote();
    return () => { isMounted = false; };
  }, [isOpen, workshop?.id, quantity]);

  if (!isOpen || !workshop) return null;

  const currentPrice = pricing?.currentPrice || workshop.price || 399;
  const remainingInTier = pricing?.ticketsRemainingInTier ?? 4;
  const currentTier = pricing?.currentTier || 1;
  const remainingTotal = pricing?.remainingSeats ?? workshop.capacity ?? 30;
  const maxAllowed = Math.min(10, remainingTotal);

  const handleIncrement = () => {
    if (quantity < maxAllowed) {
      setQuantity((q) => q + 1);
    }
  };

  const handleDecrement = () => {
    if (quantity > 1) {
      setQuantity((q) => q - 1);
    }
  };

  const totalPayable = quote?.totalAmount ?? (currentPrice * quantity);

  return (
    <div className="select-tickets-overlay" onClick={onClose}>
      <div
        className="select-tickets-modal"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
      >
        <div className="select-tickets-header">
          <div>
            <h2 className="select-tickets-title">Select Tickets</h2>
            <p className="select-tickets-subtitle">{workshop.title}</p>
          </div>
          <button
            type="button"
            className="select-tickets-close-btn"
            onClick={onClose}
            aria-label="Close"
          >
            <X size={18} />
          </button>
        </div>

        <div className="select-tickets-body">
          {/* DYNAMIC TIER ROWS */}
          {pricing?.tiers && pricing.tiers.length > 0 ? (
            pricing.tiers.map((tier) => {
              const isPast = tier.tierNumber < currentTier;
              const isActive = tier.tierNumber === currentTier;

              if (isPast) {
                return (
                  <div key={tier.tierNumber} className="ticket-tier-row is-disabled">
                    <div className="ticket-tier-info">
                      <span className="ticket-tier-name">{tier.tierName || `Tier ${tier.tierNumber}`} Pass</span>
                      <span className="ticket-tier-price">₹{tier.price}</span>
                    </div>
                    <span className="ticket-tier-badge sold-out">SOLD OUT</span>
                  </div>
                );
              }

              if (isActive) {
                return (
                  <div key={tier.tierNumber} className="ticket-tier-row is-active">
                    <div className="ticket-tier-info">
                      <span className="ticket-tier-name">
                        {tier.tierName || pricing?.currentTierName || `Tier ${currentTier}`} Pass
                      </span>
                      <span className="ticket-tier-meta">
                        ₹{currentPrice} per ticket • {remainingInTier} left at this price
                      </span>
                    </div>
                    <div className="ticket-stepper">
                      <button
                        type="button"
                        className="stepper-btn"
                        onClick={handleDecrement}
                        disabled={quantity <= 1}
                        aria-label="Decrease quantity"
                      >
                        <Minus size={14} />
                      </button>
                      <span className="stepper-value">{quantity}</span>
                      <button
                        type="button"
                        className="stepper-btn"
                        onClick={handleIncrement}
                        disabled={quantity >= maxAllowed}
                        aria-label="Increase quantity"
                      >
                        <Plus size={14} />
                      </button>
                    </div>
                  </div>
                );
              }

              return null;
            })
          ) : (
            <div className="ticket-tier-row is-active">
              <div className="ticket-tier-info">
                <span className="ticket-tier-name">
                  {pricing?.currentTierName || `Tier ${currentTier}`} Pass
                </span>
                <span className="ticket-tier-meta">
                  ₹{currentPrice} per ticket • {remainingInTier} left at this price
                </span>
              </div>
              <div className="ticket-stepper">
                <button
                  type="button"
                  className="stepper-btn"
                  onClick={handleDecrement}
                  disabled={quantity <= 1}
                  aria-label="Decrease quantity"
                >
                  <Minus size={14} />
                </button>
                <span className="stepper-value">{quantity}</span>
                <button
                  type="button"
                  className="stepper-btn"
                  onClick={handleIncrement}
                  disabled={quantity >= maxAllowed}
                  aria-label="Increase quantity"
                >
                  <Plus size={14} />
                </button>
              </div>
            </div>
          )}

          {/* SPLIT TIER WARNING IF QUANTITY CROSSES BOUNDARY */}
          {quote?.isSplitTier && (
            <div className="split-tier-callout">
              <AlertCircle size={16} className="split-icon" />
              <div className="split-text">
                <strong>Dynamic Pricing Notice</strong>
                <p>{quote.splitTierMessage}</p>
                <div className="split-breakdown-pills">
                  {quote.breakdown.map((b, idx) => (
                    <span key={idx} className="split-pill">
                      {b.quantity}x {b.tierName} @ ₹{b.unitPrice}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* CAPACITY NOTICE */}
          <div className="tier-capacity-hint">
            <span>{remainingTotal} total seats remaining in workshop.</span>
            <span>Max 10 tickets per booking.</span>
          </div>
        </div>

        {/* FOOTER */}
        <div className="select-tickets-footer">
          <div className="select-tickets-total-info">
            <span className="total-tickets-count">
              {quantity} {quantity === 1 ? "ticket" : "tickets"}
            </span>
            <span className="total-tickets-amount">
              ₹{Number(totalPayable).toLocaleString("en-IN")}
            </span>
          </div>
          <button
            type="button"
            className="select-tickets-continue-btn"
            disabled={quantity < 1 || loadingQuote}
            onClick={() => onContinue({ quantity, totalPayable, quote })}
          >
            {loadingQuote ? "Calculating..." : "Continue ›"}
          </button>
        </div>
      </div>
    </div>
  );
}
