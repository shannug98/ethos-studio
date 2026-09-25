import React, { useState } from "react";
import {
  IndianRupee,
  Plus,
  Trash2,
  Sliders,
  CheckCircle2,
  AlertTriangle,
  Calculator,
  ChevronDown,
  ChevronUp,
  Tag,
  Sparkles,
} from "lucide-react";
import NumericInput from "../../../../components/common/NumericInput";
import {
  calculateSimulatedQuote,
  getTierValidation,
  classifyPassCategory,
} from "./pricingUxHelpers";

/**
 * Step 5 — Configure Ticket Pricing
 * Clean, intuitive pricing configurator. Supports:
 * - Simple Flat Pricing (Single rate for all buyers — recommended)
 * - Early Bird / Progressive Volume Tiers (e.g. 1–10 ₹500, 11+ ₹600)
 * - Optional Collapsible Cart Split Calculator
 */
export default function Step5TicketPricing({ form, onChange, errors }) {
  const passTypes = Array.isArray(form.passTypes) ? form.passTypes : [];
  const sessions = Array.isArray(form.sessions) ? form.sessions : [];

  // Local state for interactive test calculator per ticket index (collapsed by default)
  const [simulators, setSimulators] = useState({});

  const getSimState = (ticketIdx) => {
    return simulators[ticketIdx] || { sold: 0, qty: 2, expanded: false };
  };

  const updateSimState = (ticketIdx, patch) => {
    setSimulators((prev) => ({
      ...prev,
      [ticketIdx]: { ...getSimState(ticketIdx), ...patch },
    }));
  };

  const updatePassTypes = (newPassTypes) => {
    onChange("passTypes", newPassTypes);

    // Compute minimum starting price across all ticket types
    let minPrice = 500;
    const allPrices = newPassTypes
      .map((p) => {
        if (Array.isArray(p.pricingTiers) && p.pricingTiers.length > 0 && p.pricingTiers[0].price != null) {
          return Number(p.pricingTiers[0].price);
        }
        return Number(p.price) || 0;
      })
      .filter((pr) => pr > 0);

    if (allPrices.length > 0) {
      minPrice = Math.min(...allPrices);
    }
    onChange("price", minPrice);
  };

  const handleUpdateTicketTiers = (ticketIdx, updatedTiers) => {
    const updated = passTypes.map((ticket, idx) => {
      if (idx !== ticketIdx) return ticket;
      const basePrice = updatedTiers.length > 0 && updatedTiers[0].price != null
        ? Number(updatedTiers[0].price)
        : Number(ticket.price) || 500;

      return {
        ...ticket,
        price: basePrice,
        pricingTiers: updatedTiers,
      };
    });
    updatePassTypes(updated);
  };

  const handleTierFieldChange = (ticketIdx, tierIdx, field, value) => {
    const ticket = passTypes[ticketIdx];
    const tiers = Array.isArray(ticket?.pricingTiers) ? ticket.pricingTiers : [];

    let updatedTiers = tiers.map((t, idx) => {
      if (idx !== tierIdx) return t;
      return { ...t, [field]: value };
    });

    // Contiguity Auto-Sync: if maxTickets was changed on tierIdx, automatically update tierIdx + 1's minTickets
    if (field === "maxTickets") {
      const newMax = value != null && value !== "" ? Number(value) : null;
      if (newMax !== null && tierIdx + 1 < updatedTiers.length) {
        const nextMin = newMax + 1;
        updatedTiers = updatedTiers.map((t, idx) => {
          if (idx !== tierIdx + 1) return t;
          let nextMax = t.maxTickets;
          if (nextMax !== null && nextMax < nextMin) {
            nextMax = nextMin + 9;
          }
          return {
            ...t,
            minTickets: nextMin,
            maxTickets: nextMax,
          };
        });
      }
    }

    handleUpdateTicketTiers(ticketIdx, updatedTiers);
  };

  const handleAddTier = (ticketIdx) => {
    const ticket = passTypes[ticketIdx];
    const tiers = Array.isArray(ticket?.pricingTiers) ? ticket.pricingTiers : [];
    const lastTier = tiers[tiers.length - 1];
    const prevMax = lastTier ? (Number(lastTier.maxTickets) || 10) : 0;
    const newMin = prevMax + 1;
    const lastPrice = lastTier ? Number(lastTier.price) : Number(ticket.price) || 500;

    // If last tier was open-ended (null max), close it first before adding new tier
    let baseTiers = tiers;
    if (lastTier && lastTier.maxTickets == null) {
      baseTiers = tiers.map((t, idx) => {
        if (idx !== tiers.length - 1) return t;
        return { ...t, maxTickets: t.minTickets + 9 };
      });
    }

    const finalPrevTier = baseTiers[baseTiers.length - 1];
    const finalMin = finalPrevTier ? (Number(finalPrevTier.maxTickets) + 1) : 1;

    const newTier = {
      tierNumber: baseTiers.length + 1,
      tierName: `Tier ${baseTiers.length + 1}`,
      minTickets: finalMin,
      maxTickets: null, // open-ended for final tier
      price: lastPrice + 100,
    };

    handleUpdateTicketTiers(ticketIdx, [...baseTiers, newTier]);
  };

  const handleRemoveTier = (ticketIdx, tierIdx) => {
    const ticket = passTypes[ticketIdx];
    const tiers = Array.isArray(ticket?.pricingTiers) ? ticket.pricingTiers : [];
    if (tiers.length <= 1) return;

    const filtered = tiers.filter((_, idx) => idx !== tierIdx);

    // Reindex & reconcile contiguity
    const reindexed = filtered.map((t, idx) => {
      if (idx === 0) {
        return { ...t, tierNumber: 1, minTickets: 1 };
      }
      const prev = filtered[idx - 1];
      const prevMax = prev.maxTickets != null ? Number(prev.maxTickets) : 10;
      return {
        ...t,
        tierNumber: idx + 1,
        minTickets: prevMax + 1,
      };
    });

    handleUpdateTicketTiers(ticketIdx, reindexed);
  };

  const applyPreset = (ticketIdx, presetType) => {
    const ticket = passTypes[ticketIdx];
    const baseP = Number(ticket.price) || 500;
    const totalCap = Number(ticket.totalQuantity) || 50;

    if (presetType === "FLAT") {
      const flatTiers = [
        {
          tierNumber: 1,
          tierName: `${ticket.name} (Flat)`,
          minTickets: 1,
          maxTickets: null,
          price: baseP,
        },
      ];
      handleUpdateTicketTiers(ticketIdx, flatTiers);
    } else if (presetType === "EARLY_BIRD_2") {
      const splitPoint = Math.max(5, Math.min(Math.floor(totalCap * 0.35), totalCap - 1));
      const twoTiers = [
        {
          tierNumber: 1,
          tierName: `Early Bird`,
          minTickets: 1,
          maxTickets: splitPoint,
          price: baseP,
        },
        {
          tierNumber: 2,
          tierName: `Regular Admission`,
          minTickets: splitPoint + 1,
          maxTickets: null,
          price: baseP + 100,
        },
      ];
      handleUpdateTicketTiers(ticketIdx, twoTiers);
    } else if (presetType === "STANDARD_4") {
      const fourTiers = [
        {
          tierNumber: 1,
          tierName: `Super Early Bird`,
          minTickets: 1,
          maxTickets: 10,
          price: baseP,
        },
        {
          tierNumber: 2,
          tierName: `Early Bird`,
          minTickets: 11,
          maxTickets: 20,
          price: baseP + 100,
        },
        {
          tierNumber: 3,
          tierName: `Regular Batch`,
          minTickets: 21,
          maxTickets: 30,
          price: baseP + 200,
        },
        {
          tierNumber: 4,
          tierName: `Final Batch`,
          minTickets: 31,
          maxTickets: null,
          price: baseP + 300,
        },
      ];
      handleUpdateTicketTiers(ticketIdx, fourTiers);
    }
  };

  const getPassCategory = (p) => classifyPassCategory(p);

  return (
    <div className="wizard-step-panel">
      {/* Header */}
      <div className="wizard-section-header">
        <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
          <IndianRupee size={24} color="#FF5500" />
          <h2 className="wizard-section-title">Step 5 — Configure Ticket Pricing</h2>
        </div>
        <p className="wizard-section-desc">
          Set how much each ticket type costs. You can choose simple <strong>Flat Pricing</strong> (all attendees pay the same price)
          or <strong>Early Bird & Volume Tiers</strong> (progressive discounts for early buyers).
        </p>
      </div>

      {errors?.pricing && (
        <div
          className="wizard-field-error"
          style={{
            marginBottom: "18px",
            padding: "12px 16px",
            background: "#FEF2F2",
            borderRadius: "8px",
            border: "1px solid #FCA5A5",
            color: "#DC2626",
            fontSize: "13px",
            fontWeight: 650,
          }}
        >
          {errors.pricing}
        </div>
      )}

      {/* Empty State */}
      {passTypes.length === 0 ? (
        <div
          style={{
            padding: "48px 24px",
            textAlign: "center",
            background: "#FFFFFF",
            border: "2px dashed #CBD5E1",
            borderRadius: "12px",
            color: "#64748B",
          }}
        >
          <IndianRupee size={40} color="#94A3B8" style={{ margin: "0 auto 12px" }} />
          <h3 style={{ fontSize: "16px", fontWeight: 700, color: "#1E293B", marginBottom: "6px" }}>
            No Ticket Types Available
          </h3>
          <p style={{ fontSize: "13px", maxWidth: "440px", margin: "0 auto" }}>
            Please go back to Step 4 and create at least one ticket type before configuring pricing.
          </p>
        </div>
      ) : (
        /* Ticket Pricing Cards */
        <div style={{ display: "flex", flexDirection: "column", gap: "24px" }}>
          {passTypes.map((ticket, ticketIdx) => {
            const category = getPassCategory(ticket);
            const linkedSession = sessions.find((s) => s.id === ticket.workshopSessionId);

            const tiers = Array.isArray(ticket.pricingTiers) && ticket.pricingTiers.length > 0
              ? ticket.pricingTiers
              : [
                  {
                    tierNumber: 1,
                    tierName: `${ticket.name} (Flat)`,
                    minTickets: 1,
                    maxTickets: null,
                    price: Number(ticket.price) || 500,
                  },
                ];

            const isFlatMode = tiers.length <= 1;
            const validation = getTierValidation(tiers, ticket.totalQuantity);
            const startingPrice = tiers[0]?.price != null ? tiers[0].price : (ticket.price || 500);

            const simState = getSimState(ticketIdx);
            const simResult = calculateSimulatedQuote(tiers, ticket.totalQuantity, simState.sold, simState.qty);

            return (
              <div
                key={ticket.id || ticketIdx}
                className="pricing-tiers-card"
                style={{
                  background: "#FFFFFF",
                  border: "1px solid #E2E8F0",
                  borderRadius: "12px",
                  padding: "22px 24px",
                  boxShadow: "0 1px 3px rgba(0,0,0,0.04)",
                }}
              >
                {/* Header: Ticket Name, Badges, Starting Price */}
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    paddingBottom: "16px",
                    borderBottom: "1px solid #F1F5F9",
                    marginBottom: "18px",
                    flexWrap: "wrap",
                    gap: "12px",
                  }}
                >
                  <div>
                    <div style={{ display: "flex", alignItems: "center", gap: "10px", flexWrap: "wrap", marginBottom: "4px" }}>
                      <span
                        style={{
                          padding: "3px 9px",
                          background: "#FF5500",
                          color: "#FFFFFF",
                          fontSize: "11px",
                          fontWeight: 700,
                          borderRadius: "6px",
                          textTransform: "uppercase",
                          letterSpacing: "0.5px",
                        }}
                      >
                        Ticket #{ticketIdx + 1}
                      </span>

                      <h3 style={{ margin: 0, fontSize: "18px", fontWeight: 750, color: "#172033" }}>
                        {ticket.name || `Ticket Type ${ticketIdx + 1}`}
                      </h3>

                      {/* Scope Badge */}
                      {category === "ALL_ACCESS" && (
                        <span style={{ fontSize: "11px", fontWeight: 700, padding: "3px 8px", background: "#FEF3C7", color: "#B45309", borderRadius: "6px" }}>
                          ✦ All Sessions Pass
                        </span>
                      )}
                      {category === "SINGLE" && (
                        <span style={{ fontSize: "11px", fontWeight: 700, padding: "3px 8px", background: "#DBEAFE", color: "#1D4ED8", borderRadius: "6px" }}>
                          👤 {linkedSession ? `Single Session: ${linkedSession.title}` : "Solo Pass (1 Session)"}
                        </span>
                      )}
                      {category === "BUNDLE" && (
                        <span style={{ fontSize: "11px", fontWeight: 700, padding: "3px 8px", background: "#F3E8FF", color: "#7E22CE", borderRadius: "6px" }}>
                          📚 {ticket.sessionsIncluded || 2}-Session Pass
                        </span>
                      )}

                      <span
                        style={{
                          padding: "3px 8px",
                          background: "#F1F5F9",
                          color: "#475569",
                          fontSize: "12px",
                          fontWeight: 650,
                          borderRadius: "6px",
                        }}
                      >
                        Quota: {ticket.totalQuantity} Tickets
                      </span>
                    </div>

                    {/* Styled Ticket Description */}
                    {ticket.description?.trim() && (
                      <div style={{ fontSize: "13px", color: "#64748B", fontStyle: "italic", marginTop: "2px" }}>
                        "{ticket.description.trim()}"
                      </div>
                    )}
                  </div>

                  {/* Starting Price Pill */}
                  <div
                    style={{
                      padding: "8px 16px",
                      borderRadius: "8px",
                      background: "#FFF0EB",
                      border: "1px solid #FFD5C2",
                      textAlign: "right",
                    }}
                  >
                    <span style={{ fontSize: "11px", color: "#FF5500", fontWeight: 700, textTransform: "uppercase", display: "block" }}>
                      {isFlatMode ? "Ticket Price" : "Starting Price"}
                    </span>
                    <span style={{ fontSize: "22px", color: "#FF5500", fontWeight: 800 }}>
                      ₹{startingPrice}
                    </span>
                  </div>
                </div>

                {/* Pricing Strategy Segment Buttons */}
                <div style={{ marginBottom: "18px" }}>
                  <label
                    style={{
                      display: "block",
                      fontSize: "12px",
                      fontWeight: 700,
                      color: "#475569",
                      textTransform: "uppercase",
                      letterSpacing: "0.5px",
                      marginBottom: "8px",
                    }}
                  >
                    Select Pricing Mode
                  </label>

                  <div
                    className="step5-pricing-mode-grid"
                    style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "12px", maxWidth: "600px" }}
                  >
                    {/* Flat Price Option */}
                    <button
                      type="button"
                      onClick={() => applyPreset(ticketIdx, "FLAT")}
                      style={{
                        padding: "10px 14px",
                        borderRadius: "8px",
                        textAlign: "left",
                        cursor: "pointer",
                        border: isFlatMode ? "2px solid #FF5500" : "1px solid #CBD5E1",
                        background: isFlatMode ? "#FFF7ED" : "#FFFFFF",
                        transition: "all 0.15s ease",
                      }}
                    >
                      <div style={{ fontSize: "13px", fontWeight: 700, color: isFlatMode ? "#C2410C" : "#1E293B", display: "flex", alignItems: "center", gap: "6px" }}>
                        <Tag size={14} />
                        <span>Flat Price (Single Rate)</span>
                      </div>
                      <div style={{ fontSize: "11px", color: "#64748B", marginTop: "2px" }}>
                        Every attendee pays the exact same price
                      </div>
                    </button>

                    {/* Early Bird & Tiered Option */}
                    <button
                      type="button"
                      onClick={() => {
                        if (isFlatMode) applyPreset(ticketIdx, "EARLY_BIRD_2");
                      }}
                      style={{
                        padding: "10px 14px",
                        borderRadius: "8px",
                        textAlign: "left",
                        cursor: "pointer",
                        border: !isFlatMode ? "2px solid #FF5500" : "1px solid #CBD5E1",
                        background: !isFlatMode ? "#FFF7ED" : "#FFFFFF",
                        transition: "all 0.15s ease",
                      }}
                    >
                      <div style={{ fontSize: "13px", fontWeight: 700, color: !isFlatMode ? "#C2410C" : "#1E293B", display: "flex", alignItems: "center", gap: "6px" }}>
                        <Sparkles size={14} />
                        <span>Early Bird & Volume Tiers</span>
                      </div>
                      <div style={{ fontSize: "11px", color: "#64748B", marginTop: "2px" }}>
                        Discounts for early buyers (e.g. 1–10 ₹500, 11+ ₹600)
                      </div>
                    </button>
                  </div>
                </div>

                {/* MODE A: FLAT PRICING (SIMPLE & CLEAN) */}
                {isFlatMode ? (
                  <div
                    style={{
                      background: "#F8FAFC",
                      border: "1px solid #E2E8F0",
                      borderRadius: "10px",
                      padding: "18px 20px",
                      marginBottom: "16px",
                    }}
                  >
                    <label
                      style={{
                        display: "block",
                        fontSize: "13px",
                        fontWeight: 700,
                        color: "#1E293B",
                        marginBottom: "6px",
                      }}
                    >
                      Price per Ticket (₹ INR) <span style={{ color: "#DC2626" }}>*</span>
                    </label>

                    <div style={{ maxWidth: "240px" }}>
                      <NumericInput
                        min={1}
                        max={1000000}
                        prefix="₹"
                        value={tiers[0]?.price ?? ticket.price ?? 500}
                        onChange={(val) => {
                          const newPrice = Math.max(1, val || 1);
                          const flatTier = [
                            {
                              tierNumber: 1,
                              tierName: `${ticket.name} (Flat)`,
                              minTickets: 1,
                              maxTickets: null,
                              price: newPrice,
                            },
                          ];
                          handleUpdateTicketTiers(ticketIdx, flatTier);
                        }}
                        className="tier-input-field"
                        style={{
                          width: "100%",
                          padding: "10px 14px",
                          borderRadius: "8px",
                          border: "1.5px solid #CBD5E1",
                          fontSize: "16px",
                          fontWeight: 750,
                          color: "#1E293B",
                          background: "#FFFFFF",
                        }}
                        placeholder="e.g. 500"
                      />
                    </div>

                    <span style={{ fontSize: "12px", color: "#64748B", marginTop: "6px", display: "block" }}>
                      All attendees purchasing "{ticket.name}" will pay a flat rate of ₹{tiers[0]?.price ?? ticket.price ?? 500}.
                    </span>
                  </div>
                ) : (
                  /* MODE B: TIERED / EARLY BIRD PRICING */
                  <div style={{ marginBottom: "16px" }}>
                    {/* Quick Presets Toolbar */}
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "10px",
                        marginBottom: "14px",
                        padding: "8px 12px",
                        background: "#F8FAFC",
                        borderRadius: "8px",
                        border: "1px solid #E2E8F0",
                        flexWrap: "wrap",
                      }}
                    >
                      <span style={{ fontSize: "12px", fontWeight: 700, color: "#475569" }}>
                        Presets:
                      </span>
                      <button
                        type="button"
                        onClick={() => applyPreset(ticketIdx, "EARLY_BIRD_2")}
                        style={{
                          padding: "4px 10px",
                          borderRadius: "6px",
                          background: tiers.length === 2 ? "#EFF6FF" : "#FFFFFF",
                          border: tiers.length === 2 ? "1.5px solid #3B82F6" : "1px solid #CBD5E1",
                          fontSize: "12px",
                          fontWeight: 650,
                          color: tiers.length === 2 ? "#1D4ED8" : "#475569",
                          cursor: "pointer",
                        }}
                      >
                        🌟 2-Tier (Early Bird + Regular)
                      </button>
                      <button
                        type="button"
                        onClick={() => applyPreset(ticketIdx, "STANDARD_4")}
                        style={{
                          padding: "4px 10px",
                          borderRadius: "6px",
                          background: tiers.length === 4 ? "#EFF6FF" : "#FFFFFF",
                          border: tiers.length === 4 ? "1.5px solid #3B82F6" : "1px solid #CBD5E1",
                          fontSize: "12px",
                          fontWeight: 650,
                          color: tiers.length === 4 ? "#1D4ED8" : "#475569",
                          cursor: "pointer",
                        }}
                      >
                        ⚡ 4-Tier (Batch Progression)
                      </button>
                    </div>

                    {/* Tiers Table — 100% card width, zero horizontal scroll / slider */}
                    <div style={{ border: "1px solid #E2E8F0", borderRadius: "8px", overflow: "hidden", background: "#FFFFFF", width: "100%" }}>
                      <table style={{ width: "100%", tableLayout: "fixed", borderCollapse: "collapse", fontSize: "13px" }}>
                        <thead>
                          <tr style={{ background: "#F8FAFC", borderBottom: "1px solid #E2E8F0" }}>
                            <th style={{ padding: "10px 12px", textAlign: "left", width: "85px", color: "#64748B", fontWeight: 700, whiteSpace: "nowrap" }}>
                              Tier #
                            </th>
                            <th style={{ padding: "10px 12px", textAlign: "left", color: "#64748B", fontWeight: 700 }}>
                              Tier Label
                            </th>
                            <th style={{ padding: "10px 8px", textAlign: "center", width: "80px", color: "#64748B", fontWeight: 700, whiteSpace: "nowrap" }}>
                              From #
                            </th>
                            <th style={{ padding: "10px 8px", textAlign: "center", width: "115px", color: "#64748B", fontWeight: 700, whiteSpace: "nowrap" }}>
                              To #
                            </th>
                            <th style={{ padding: "10px 12px", textAlign: "right", width: "135px", color: "#64748B", fontWeight: 700, whiteSpace: "nowrap" }}>
                              Price (₹)
                            </th>
                            <th style={{ padding: "10px 6px", textAlign: "center", width: "36px" }}></th>
                          </tr>
                        </thead>
                        <tbody>
                          {tiers.map((t, tierIdx) => {
                            const isFirst = tierIdx === 0;
                            const isLast = tierIdx === tiers.length - 1;
                            const isPrevCheaper = tierIdx > 0 && tiers[tierIdx - 1].price != null && t.price < tiers[tierIdx - 1].price;

                            return (
                              <tr
                                key={tierIdx}
                                style={{
                                  borderBottom: isLast ? "none" : "1px solid #F1F5F9",
                                  background: isFirst ? "#FFFDFB" : "#FFFFFF",
                                }}
                              >
                                <td style={{ padding: "10px 12px", whiteSpace: "nowrap" }}>
                                  <span
                                    style={{
                                      padding: "3px 8px",
                                      borderRadius: "4px",
                                      background: "#F1F5F9",
                                      color: "#334155",
                                      fontSize: "11px",
                                      fontWeight: 700,
                                      whiteSpace: "nowrap",
                                      display: "inline-block",
                                    }}
                                  >
                                    Tier {tierIdx + 1}
                                  </span>
                                </td>

                                <td style={{ padding: "10px 12px" }}>
                                  <input
                                    type="text"
                                    className="tier-input-field"
                                    style={{ width: "100%", padding: "7px 10px", borderRadius: "6px", border: "1px solid #CBD5E1", fontSize: "13px", boxSizing: "border-box" }}
                                    placeholder="e.g. Early Bird, Regular"
                                    value={t.tierName || ""}
                                    onChange={(e) => handleTierFieldChange(ticketIdx, tierIdx, "tierName", e.target.value)}
                                  />
                                </td>

                                <td style={{ padding: "10px 8px", textAlign: "center" }}>
                                  <span style={{ fontWeight: 700, color: "#475569" }}>
                                    {t.minTickets ?? 1}
                                  </span>
                                </td>

                                <td style={{ padding: "10px 8px", textAlign: "center" }}>
                                  <NumericInput
                                    min={t.minTickets || 1}
                                    allowNull={true}
                                    emptyValue={null}
                                    placeholder="Final (+)"
                                    className="text-center"
                                    value={t.maxTickets}
                                    onChange={(val) => handleTierFieldChange(ticketIdx, tierIdx, "maxTickets", val)}
                                    style={{ width: "100%", padding: "6px 8px", borderRadius: "6px", border: "1px solid #CBD5E1", fontSize: "13px", textAlign: "center", boxSizing: "border-box" }}
                                  />
                                </td>

                                <td style={{ padding: "10px 12px", textAlign: "right" }}>
                                  <div style={{ width: "100%", display: "flex", flexDirection: "column", alignItems: "stretch" }}>
                                    <NumericInput
                                      min={1}
                                      prefix="₹"
                                      value={t.price}
                                      onChange={(val) => handleTierFieldChange(ticketIdx, tierIdx, "price", val || 0)}
                                      style={{ width: "100%", borderRadius: "6px", border: "1px solid #CBD5E1", fontSize: "13px", fontWeight: 700, textAlign: "right" }}
                                    />
                                    {isPrevCheaper && (
                                      <span style={{ fontSize: "10px", color: "#DC2626", fontWeight: 600, marginTop: "2px", textAlign: "right" }}>
                                        Must be ≥ ₹{tiers[tierIdx - 1].price}
                                      </span>
                                    )}
                                  </div>
                                </td>

                                <td style={{ padding: "10px 6px", textAlign: "center" }}>
                                  {tiers.length > 1 && (
                                    <button
                                      type="button"
                                      title="Remove Tier"
                                      onClick={() => handleRemoveTier(ticketIdx, tierIdx)}
                                      style={{ background: "none", border: "none", color: "#94A3B8", cursor: "pointer", padding: "4px" }}
                                      onMouseEnter={(e) => (e.currentTarget.style.color = "#DC2626")}
                                      onMouseLeave={(e) => (e.currentTarget.style.color = "#94A3B8")}
                                    >
                                      <Trash2 size={15} />
                                    </button>
                                  )}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>

                    {/* Tier Table Footer: Status & Add Button */}
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        marginTop: "12px",
                        flexWrap: "wrap",
                        gap: "10px",
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                        {validation.valid ? (
                          <>
                            <CheckCircle2 size={16} color="#16A34A" />
                            <span style={{ fontSize: "12px", color: "#16A34A", fontWeight: 600 }}>
                              Pricing tiers are contiguous and valid.
                            </span>
                          </>
                        ) : (
                          <>
                            <AlertTriangle size={16} color="#DC2626" />
                            <span style={{ fontSize: "12px", color: "#DC2626", fontWeight: 600 }}>
                              {validation.message}
                            </span>
                          </>
                        )}
                      </div>

                      <button
                        type="button"
                        onClick={() => handleAddTier(ticketIdx)}
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: "6px",
                          padding: "6px 12px",
                          fontSize: "12px",
                          fontWeight: 650,
                          background: "#FFFFFF",
                          border: "1px solid #CBD5E1",
                          borderRadius: "6px",
                          color: "#334155",
                          cursor: "pointer",
                        }}
                      >
                        <Plus size={14} />
                        <span>Add Tier</span>
                      </button>
                    </div>
                  </div>
                )}

                {/* Optional Collapsible Test Calculator */}
                <div
                  style={{
                    borderTop: "1px solid #F1F5F9",
                    paddingTop: "12px",
                    marginTop: "8px",
                  }}
                >
                  <button
                    type="button"
                    onClick={() => updateSimState(ticketIdx, { expanded: !simState.expanded })}
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "6px",
                      background: "none",
                      border: "none",
                      color: "#64748B",
                      fontSize: "12px",
                      fontWeight: 600,
                      cursor: "pointer",
                      padding: "4px 0",
                    }}
                  >
                    <Calculator size={14} color="#FF5500" />
                    <span>Test Price Calculator (Optional: preview multi-ticket order breakdown)</span>
                    {simState.expanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                  </button>

                  {simState.expanded && (
                    <div
                      style={{
                        marginTop: "10px",
                        padding: "14px 16px",
                        background: "#F8FAFC",
                        border: "1px solid #E2E8F0",
                        borderRadius: "8px",
                      }}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: "12px", marginBottom: "12px", flexWrap: "wrap" }}>
                        <span style={{ fontSize: "12px", fontWeight: 600, color: "#475569" }}>
                          Order Quantity to Test:
                        </span>
                        <div style={{ display: "flex", gap: "6px" }}>
                          {[1, 2, 3, 5].map((q) => (
                            <button
                              key={q}
                              type="button"
                              onClick={() => updateSimState(ticketIdx, { qty: q })}
                              style={{
                                padding: "4px 10px",
                                borderRadius: "4px",
                                fontSize: "12px",
                                fontWeight: 700,
                                background: simState.qty === q ? "#FF5500" : "#FFFFFF",
                                color: simState.qty === q ? "#FFFFFF" : "#334155",
                                border: simState.qty === q ? "1px solid #FF5500" : "1px solid #CBD5E1",
                                cursor: "pointer",
                              }}
                            >
                              {q} {q === 1 ? "Ticket" : "Tickets"}
                            </button>
                          ))}
                        </div>
                      </div>

                      {/* Result Box */}
                      <div
                        style={{
                          padding: "10px 14px",
                          background: "#FFFFFF",
                          border: "1px solid #E2E8F0",
                          borderRadius: "6px",
                          display: "flex",
                          justifyContent: "space-between",
                          alignItems: "center",
                          flexWrap: "wrap",
                          gap: "8px",
                        }}
                      >
                        <div style={{ fontSize: "12px", color: "#334155" }}>
                          <strong>{simState.qty} tickets</strong> @ average ₹{simResult.avgPrice?.toFixed(2) || "0.00"} / ticket
                        </div>
                        <div style={{ fontSize: "16px", fontWeight: 800, color: "#FF5500" }}>
                          Total: ₹{simResult.total || 0}
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
