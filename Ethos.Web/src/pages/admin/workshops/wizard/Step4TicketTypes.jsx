import React from "react";
import { Ticket, Plus, Trash2, CheckCircle2 } from "lucide-react";
import NumericInput from "../../../../components/common/NumericInput";

/**
 * Step 4 — Ticket Types
 * Configures the commercial ticket types (Name, Description, Quota, Pass Scope / Entitlement, Active status)
 * for the workshop.
 * - All Sessions Pass: covers all sessions in the workshop (sessionsIncluded = null)
 * - Solo Pass: attendee chooses 1 session from the schedule at booking (sessionsIncluded = 1)
 * - Multi-Session Pass: attendee chooses N sessions from the schedule at booking (sessionsIncluded = N)
 * Pricing and volume tiers are configured in Step 5.
 */
export default function Step4TicketTypes({ form, onChange, errors }) {
  const passTypes = Array.isArray(form.passTypes) ? form.passTypes : [];
  const sessions = Array.isArray(form.sessions) ? form.sessions : [];
  const totalSessionsCount = sessions.length;

  const updatePassTypes = (newPassTypes) => {
    onChange("passTypes", newPassTypes);
  };

  const handleTicketChange = (index, fieldOrObj, value) => {
    const updated = passTypes.map((p, idx) => {
      if (idx !== index) return p;
      if (typeof fieldOrObj === "object" && fieldOrObj !== null) {
        return { ...p, ...fieldOrObj };
      }
      return { ...p, [fieldOrObj]: value };
    });
    updatePassTypes(updated);
  };

  const handleAddTicket = () => {
    const defaultCapacity = Number(form?.capacity) > 0 ? Number(form.capacity) : 35;
    const defaultPrice = Number(form?.price) > 0 ? Number(form.price) : 500;

    const newTicket = {
      id: undefined,
      name: `Ticket Type ${passTypes.length + 1}`,
      description: "",
      category: "ALL_ACCESS",
      totalQuantity: defaultCapacity,
      workshopSessionId: null,
      targetSessionClientId: null,
      sessionsIncluded: null,
      price: defaultPrice,
      salesStartUtc: "",
      salesEndUtc: "",
      displayOrder: passTypes.length + 1,
      isActive: true,
      pricingTiers: [
        {
          tierNumber: 1,
          tierName: `Tier 1 (Standard)`,
          minTickets: 1,
          maxTickets: null,
          price: defaultPrice,
        },
      ],
    };

    updatePassTypes([...passTypes, newTicket]);
  };

  const handleRemoveTicket = (index) => {
    const filtered = passTypes.filter((_, idx) => idx !== index);
    const reindexed = filtered.map((p, idx) => ({ ...p, displayOrder: idx + 1 }));
    updatePassTypes(reindexed);
  };

  const getCategory = (ticket) => {
    if (ticket.category === "ALL_ACCESS" || ticket.sessionsIncluded == null) return "ALL_ACCESS";
    if (ticket.category === "SINGLE" || Number(ticket.sessionsIncluded) === 1) return "SINGLE";
    if (ticket.category === "BUNDLE" || Number(ticket.sessionsIncluded) >= 2) return "BUNDLE";
    return "ALL_ACCESS";
  };

  const totalTicketCapacity = passTypes.reduce((sum, p) => sum + (Number(p.totalQuantity) || 0), 0);

  return (
    <div className="wizard-step-panel">
      {/* Header */}
      <div className="wizard-section-header">
        <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
          <Ticket size={24} color="#FF5500" />
          <h2 className="wizard-section-title">Step 4 — Configure Ticket Types</h2>
        </div>
        <p className="wizard-section-desc">
          Define the ticket types available for this workshop (Solo, Dual, Trio, or All Workshops).
          Configure commercial quotas and pass entitlements. Pricing and progressive volume tiers will be configured next in Step 5.
        </p>
      </div>

      {/* Summary Metrics Banner */}
      <div
        className="step4-quota-bar"
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          padding: "14px 20px",
          background: "#FFFFFF",
          border: "1px solid #E2E8F0",
          borderRadius: "10px",
          marginBottom: "20px",
          boxShadow: "0 1px 2px rgba(0,0,0,0.03)",
        }}
      >
        <div style={{ display: "flex", gap: "28px" }}>
          <div>
            <span style={{ fontSize: "11px", textTransform: "uppercase", color: "#64748B", fontWeight: 700, letterSpacing: "0.5px" }}>
              Configured Ticket Types
            </span>
            <div style={{ fontSize: "20px", fontWeight: 800, color: "#172033" }}>
              {passTypes.length}
            </div>
          </div>
          <div>
            <span style={{ fontSize: "11px", textTransform: "uppercase", color: "#64748B", fontWeight: 700, letterSpacing: "0.5px" }}>
              Total Commercial Quota
            </span>
            <div style={{ fontSize: "20px", fontWeight: 800, color: "#FF5500" }}>
              {totalTicketCapacity} Tickets
            </div>
          </div>
        </div>

        <button
          type="button"
          className="btn-add-tier"
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: "6px",
            padding: "9px 16px",
            fontSize: "13px",
            fontWeight: 700,
            background: "#FF5500",
            color: "#FFFFFF",
            border: "none",
            borderRadius: "8px",
            cursor: "pointer",
            transition: "background 0.15s ease",
          }}
          onClick={handleAddTicket}
        >
          <Plus size={16} />
          <span>Add Ticket Type</span>
        </button>
      </div>

      {/* Validation Error Banner */}
      {errors?.passTypes && (
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
          {errors.passTypes}
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
          <Ticket size={40} color="#94A3B8" style={{ margin: "0 auto 12px" }} />
          <h3 style={{ fontSize: "16px", fontWeight: 700, color: "#1E293B", marginBottom: "6px" }}>
            No Ticket Types Defined Yet
          </h3>
          <p style={{ fontSize: "13px", maxWidth: "480px", margin: "0 auto 20px" }}>
            Create at least one ticket type (such as All Sessions Pass, Solo Pass, or Multi-Session Bundle) to proceed.
          </p>
          <button
            type="button"
            className="btn-add-tier"
            style={{
              padding: "10px 20px",
              fontSize: "14px",
              fontWeight: 700,
              background: "#FF5500",
              color: "#FFFFFF",
              border: "none",
              borderRadius: "8px",
              cursor: "pointer",
            }}
            onClick={handleAddTicket}
          >
            <Plus size={16} style={{ display: "inline-block", verticalAlign: "middle", marginRight: "6px" }} />
            <span>Add First Ticket Type</span>
          </button>
        </div>
      ) : (
        /* Simplified Ticket Cards List */
        <div style={{ display: "flex", flexDirection: "column", gap: "18px" }}>
          {passTypes.map((ticket, idx) => {
            const category = getCategory(ticket);

            return (
              <div
                key={ticket.id || `ticket-${idx}`}
                className="step4-ticket-card"
                style={{
                  background: "#FFFFFF",
                  border: "1px solid #E2E8F0",
                  borderRadius: "12px",
                  padding: "20px 24px",
                  boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
                }}
              >
                {/* Card Header: Badge + Title on Left, Active for Sale on Right */}
                <div
                  className="step4-card-header"
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    paddingBottom: "14px",
                    borderBottom: "1px solid #F1F5F9",
                    marginBottom: "18px",
                    flexWrap: "wrap",
                    gap: "10px",
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
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
                      Ticket #{idx + 1}
                    </span>
                    <h4 style={{ margin: 0, fontSize: "16px", fontWeight: 750, color: "#172033" }}>
                      {ticket.name || `Ticket Type ${idx + 1}`}
                    </h4>

                    {/* Scope Badge */}
                    {category === "ALL_ACCESS" && (
                      <span style={{ fontSize: "11px", fontWeight: 700, padding: "3px 8px", background: "#FEF3C7", color: "#B45309", borderRadius: "6px" }}>
                        All Workshops
                      </span>
                    )}
                    {category === "SINGLE" && (
                      <span style={{ fontSize: "11px", fontWeight: 700, padding: "3px 8px", background: "#DBEAFE", color: "#1D4ED8", borderRadius: "6px" }}>
                        Solo
                      </span>
                    )}
                    {category === "BUNDLE" && Number(ticket.sessionsIncluded) === 2 && (
                      <span style={{ fontSize: "11px", fontWeight: 700, padding: "3px 8px", background: "#F3E8FF", color: "#7E22CE", borderRadius: "6px" }}>
                        Dual
                      </span>
                    )}
                    {category === "BUNDLE" && Number(ticket.sessionsIncluded) === 3 && (
                      <span style={{ fontSize: "11px", fontWeight: 700, padding: "3px 8px", background: "#F3E8FF", color: "#7E22CE", borderRadius: "6px" }}>
                        Trio
                      </span>
                    )}
                    {category === "BUNDLE" && Number(ticket.sessionsIncluded) > 3 && (
                      <span style={{ fontSize: "11px", fontWeight: 700, padding: "3px 8px", background: "#F3E8FF", color: "#7E22CE", borderRadius: "6px" }}>
                        {ticket.sessionsIncluded}-Session Bundle
                      </span>
                    )}
                  </div>

                  {/* Active for Sale Toggle */}
                  <label
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "8px",
                      fontSize: "13px",
                      fontWeight: 650,
                      color: ticket.isActive !== false ? "#16A34A" : "#94A3B8",
                      cursor: "pointer",
                      userSelect: "none",
                    }}
                  >
                    <input
                      type="checkbox"
                      checked={ticket.isActive !== false}
                      onChange={(e) => handleTicketChange(idx, "isActive", e.target.checked)}
                      style={{
                        accentColor: "#16A34A",
                        width: "16px",
                        height: "16px",
                        cursor: "pointer",
                      }}
                    />
                    <span>{ticket.isActive !== false ? "Active for Sale" : "Hidden"}</span>
                  </label>
                </div>

                {/* Pass Scope Selector */}
                <div style={{ marginBottom: "18px" }}>
                  <label
                    className="wizard-form-label"
                    style={{
                      display: "block",
                      fontSize: "13px",
                      fontWeight: 700,
                      color: "#334155",
                      marginBottom: "8px",
                    }}
                  >
                    Pass Scope / Entitlement <span style={{ color: "#DC2626" }}>*</span>
                  </label>

                  <div
                    className="step4-scope-grid"
                    style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: "10px" }}
                  >
                    {/* SOLO */}
                    <button
                      type="button"
                      onClick={() => handleTicketChange(idx, { category: "SINGLE", sessionsIncluded: 1, workshopSessionId: null, targetSessionClientId: null })}
                      style={{ padding: "10px 14px", borderRadius: "8px", textAlign: "left", cursor: "pointer",
                        border: category === "SINGLE" ? "2px solid #2563EB" : "1.5px solid #CBD5E1",
                        background: category === "SINGLE" ? "#EFF6FF" : "#FFFFFF", transition: "all 0.15s ease" }}
                    >
                      <div style={{ fontSize: "14px", fontWeight: 800, color: category === "SINGLE" ? "#1D4ED8" : "#1E293B" }}>Solo</div>
                      <div style={{ fontSize: "11px", color: "#64748B", marginTop: "3px" }}>Attend 1 workshop</div>
                    </button>

                    {/* DUAL */}
                    <button
                      type="button"
                      onClick={() => handleTicketChange(idx, { category: "BUNDLE", sessionsIncluded: 2, workshopSessionId: null, targetSessionClientId: null })}
                      style={{ padding: "10px 14px", borderRadius: "8px", textAlign: "left", cursor: "pointer",
                        border: (category === "BUNDLE" && Number(ticket.sessionsIncluded) === 2) ? "2px solid #7C3AED" : "1.5px solid #CBD5E1",
                        background: (category === "BUNDLE" && Number(ticket.sessionsIncluded) === 2) ? "#F5F3FF" : "#FFFFFF", transition: "all 0.15s ease" }}
                    >
                      <div style={{ fontSize: "14px", fontWeight: 800, color: (category === "BUNDLE" && Number(ticket.sessionsIncluded) === 2) ? "#6D28D9" : "#1E293B" }}>Dual</div>
                      <div style={{ fontSize: "11px", color: "#64748B", marginTop: "3px" }}>Attend 2 workshops</div>
                    </button>

                    {/* TRIO */}
                    <button
                      type="button"
                      onClick={() => handleTicketChange(idx, { category: "BUNDLE", sessionsIncluded: 3, workshopSessionId: null, targetSessionClientId: null })}
                      style={{ padding: "10px 14px", borderRadius: "8px", textAlign: "left", cursor: "pointer",
                        border: (category === "BUNDLE" && Number(ticket.sessionsIncluded) === 3) ? "2px solid #7C3AED" : "1.5px solid #CBD5E1",
                        background: (category === "BUNDLE" && Number(ticket.sessionsIncluded) === 3) ? "#F5F3FF" : "#FFFFFF", transition: "all 0.15s ease" }}
                    >
                      <div style={{ fontSize: "14px", fontWeight: 800, color: (category === "BUNDLE" && Number(ticket.sessionsIncluded) === 3) ? "#6D28D9" : "#1E293B" }}>Trio</div>
                      <div style={{ fontSize: "11px", color: "#64748B", marginTop: "3px" }}>Attend 3 workshops</div>
                    </button>

                    {/* ALL WORKSHOPS */}
                    <button
                      type="button"
                      onClick={() => handleTicketChange(idx, { category: "ALL_ACCESS", sessionsIncluded: null, workshopSessionId: null, targetSessionClientId: null })}
                      style={{ padding: "10px 14px", borderRadius: "8px", textAlign: "left", cursor: "pointer",
                        border: category === "ALL_ACCESS" ? "2px solid #FF5500" : "1.5px solid #CBD5E1",
                        background: category === "ALL_ACCESS" ? "#FFF7ED" : "#FFFFFF", transition: "all 0.15s ease" }}
                    >
                      <div style={{ fontSize: "14px", fontWeight: 800, color: category === "ALL_ACCESS" ? "#C2410C" : "#1E293B" }}>All Workshops</div>
                      <div style={{ fontSize: "11px", color: "#64748B", marginTop: "3px" }}>Attend all workshops</div>
                    </button>
                  </div>

                  {/* Custom N > 3 configurator (only shown when category is BUNDLE and sessionsIncluded > 3) */}
                  {category === "BUNDLE" && Number(ticket.sessionsIncluded) > 3 && (
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "10px",
                        marginTop: "10px",
                        padding: "10px 14px",
                        background: "#F5F3FF",
                        borderRadius: "8px",
                        border: "1px solid #DDD6FE",
                        flexWrap: "wrap",
                      }}
                    >
                      <span style={{ fontSize: "13px", color: "#5B21B6", fontWeight: 650 }}>
                        Attendee selects exactly:
                      </span>
                      <NumericInput
                        min={4}
                        max={totalSessionsCount > 0 ? totalSessionsCount : 30}
                        value={Number(ticket.sessionsIncluded) || 4}
                        onChange={(val) => handleTicketChange(idx, "sessionsIncluded", Math.max(4, val || 4))}
                        className="tier-input-field text-center"
                        style={{
                          width: "70px",
                          padding: "6px 10px",
                          borderRadius: "6px",
                          border: "1px solid #C4B5FD",
                          fontSize: "14px",
                          fontWeight: 700,
                          textAlign: "center",
                          background: "#FFFFFF",
                        }}
                      />
                      <span style={{ fontSize: "12px", color: "#6B7280" }}>
                        workshops from the schedule.
                      </span>
                    </div>
                  )}
                </div>

                {/* Form Fields: Stacked & Clean */}
                <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
                  {/* 1. Ticket Name */}
                  <div>
                    <label
                      className="wizard-form-label"
                      style={{
                        display: "block",
                        fontSize: "13px",
                        fontWeight: 700,
                        color: "#334155",
                        marginBottom: "6px",
                      }}
                    >
                      Ticket Name <span style={{ color: "#DC2626" }}>*</span>
                    </label>
                    <input
                      type="text"
                      className="tier-input-field"
                      style={{
                        width: "100%",
                        padding: "10px 14px",
                        borderRadius: "8px",
                        border: !ticket.name?.trim() ? "1px solid #FCA5A5" : "1px solid #CBD5E1",
                        fontSize: "14px",
                        background: !ticket.name?.trim() ? "#FFFBFB" : "#FFFFFF",
                        boxSizing: "border-box",
                      }}
                      placeholder="e.g. Early Bird Pass, General Admission, VIP Pass, Solo Pass"
                      value={ticket.name || ""}
                      onChange={(e) => handleTicketChange(idx, "name", e.target.value)}
                    />
                    {!ticket.name?.trim() && (
                      <span style={{ fontSize: "11px", color: "#DC2626", marginTop: "4px", display: "block" }}>
                        Ticket name is required.
                      </span>
                    )}
                  </div>

                  {/* 2. Ticket Description */}
                  <div>
                    <label
                      className="wizard-form-label"
                      style={{
                        display: "block",
                        fontSize: "13px",
                        fontWeight: 700,
                        color: "#334155",
                        marginBottom: "6px",
                      }}
                    >
                      Ticket Description <span style={{ color: "#DC2626" }}>*</span>
                    </label>
                    <textarea
                      rows={2}
                      className="tier-input-field"
                      style={{
                        width: "100%",
                        padding: "10px 14px",
                        borderRadius: "8px",
                        border: !ticket.description?.trim() ? "1px solid #FCA5A5" : "1px solid #CBD5E1",
                        fontSize: "14px",
                        background: !ticket.description?.trim() ? "#FFFBFB" : "#FFFFFF",
                        resize: "vertical",
                        boxSizing: "border-box",
                        fontFamily: "inherit",
                      }}
                      placeholder="Brief description of what this ticket includes..."
                      value={ticket.description || ""}
                      onChange={(e) => handleTicketChange(idx, "description", e.target.value)}
                    />
                    {!ticket.description?.trim() && (
                      <span style={{ fontSize: "11px", color: "#DC2626", marginTop: "4px", display: "block" }}>
                        Ticket description is required.
                      </span>
                    )}
                  </div>

                  {/* 3. Quantity / Seats */}
                  <div>
                    <label
                      className="wizard-form-label"
                      style={{
                        display: "block",
                        fontSize: "13px",
                        fontWeight: 700,
                        color: "#334155",
                        marginBottom: "6px",
                      }}
                    >
                      Quantity / Seats <span style={{ color: "#DC2626" }}>*</span>
                    </label>
                    <NumericInput
                      min={1}
                      max={10000}
                      value={ticket.totalQuantity ?? (Number(form.capacity) || 35)}
                      onChange={(val) => handleTicketChange(idx, "totalQuantity", Math.max(1, val || 1))}
                      className="tier-input-field"
                      style={{
                        width: "100%",
                        maxWidth: "240px",
                        padding: "10px 14px",
                        borderRadius: "8px",
                        border: "1px solid #CBD5E1",
                        fontSize: "14px",
                        boxSizing: "border-box",
                      }}
                      placeholder="e.g. 35"
                    />
                    <span style={{ fontSize: "12px", color: "#64748B", marginTop: "5px", display: "block" }}>
                      Maximum commercial ticket quota available for this ticket type.
                    </span>
                  </div>
                </div>

                {/* Card Footer: Delete Action */}
                <div
                  style={{
                    display: "flex",
                    justifyContent: "flex-start",
                    marginTop: "18px",
                    paddingTop: "14px",
                    borderTop: "1px solid #F1F5F9",
                  }}
                >
                  <button
                    type="button"
                    onClick={() => handleRemoveTicket(idx)}
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "6px",
                      padding: "7px 14px",
                      background: "#FEF2F2",
                      color: "#DC2626",
                      border: "1px solid #FECACA",
                      borderRadius: "6px",
                      fontSize: "12px",
                      fontWeight: 650,
                      cursor: "pointer",
                      transition: "all 0.15s ease",
                    }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.background = "#FEE2E2";
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.background = "#FEF2F2";
                    }}
                  >
                    <Trash2 size={14} />
                    <span>Delete Ticket</span>
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
