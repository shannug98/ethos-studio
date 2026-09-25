import React from "react";
import { Users, IndianRupee, Plus, Trash2, GraduationCap, Sparkles, Layers, Sliders } from "lucide-react";

export const DEFAULT_TIERS = [
  { tierNumber: 1, tierName: "Early Bird Tier", minTickets: 1, maxTickets: 10, price: 500 },
  { tierNumber: 2, tierName: "Standard Tier", minTickets: 11, maxTickets: 20, price: 600 },
  { tierNumber: 3, tierName: "Peak Tier", minTickets: 21, maxTickets: 30, price: 700 },
  { tierNumber: 4, tierName: "Final Batch Tier", minTickets: 31, maxTickets: null, price: 800 },
];

export default function Step4Pricing({ form, onChange, errors }) {
  // Ensure form.pricingTiers is populated
  const tiers = Array.isArray(form.pricingTiers) && form.pricingTiers.length > 0
    ? form.pricingTiers
    : DEFAULT_TIERS;

  const updateTiers = (newTiers) => {
    onChange("pricingTiers", newTiers);
    // Sync base workshop price with Tier 1
    if (newTiers.length > 0 && newTiers[0].price != null) {
      onChange("price", Number(newTiers[0].price) || 500);
    }
  };

  const handleTierChange = (index, field, value) => {
    const updated = tiers.map((t, idx) => {
      if (idx !== index) return t;
      return { ...t, [field]: value };
    });
    updateTiers(updated);
  };

  const handleAddTier = () => {
    const lastTier = tiers[tiers.length - 1];
    const prevMax = lastTier ? (lastTier.maxTickets || 30) : 0;
    const newMin = prevMax + 1;
    const newTier = {
      tierNumber: tiers.length + 1,
      tierName: `Tier ${tiers.length + 1}`,
      minTickets: newMin,
      maxTickets: newMin + 9,
      price: lastTier ? Number(lastTier.price) + 100 : 500,
    };
    updateTiers([...tiers, newTier]);
  };

  const handleRemoveTier = (index) => {
    if (tiers.length <= 1) return;
    const filtered = tiers.filter((_, idx) => idx !== index);
    // Re-index tierNumbers
    const reindexed = filtered.map((t, idx) => ({ ...t, tierNumber: idx + 1 }));
    updateTiers(reindexed);
  };

  // Preset Handlers
  const applyPreset = (presetType) => {
    const capacity = form.capacity || 40;
    if (presetType === "FLAT") {
      const baseP = form.price || 500;
      updateTiers([
        { tierNumber: 1, tierName: "Standard Admission", minTickets: 1, maxTickets: null, price: baseP }
      ]);
    } else if (presetType === "EARLY_BIRD") {
      const baseP = form.price || 400;
      updateTiers([
        { tierNumber: 1, tierName: "Early Bird Tier", minTickets: 1, maxTickets: 15, price: baseP },
        { tierNumber: 2, tierName: "Regular Admission", minTickets: 16, maxTickets: null, price: baseP + 150 }
      ]);
    } else if (presetType === "STANDARD_4") {
      const baseP = form.price || 500;
      updateTiers([
        { tierNumber: 1, tierName: "Early Bird Tier", minTickets: 1, maxTickets: 10, price: baseP },
        { tierNumber: 2, tierName: "Standard Tier", minTickets: 11, maxTickets: 20, price: baseP + 100 },
        { tierNumber: 3, tierName: "Peak Tier", minTickets: 21, maxTickets: 30, price: baseP + 200 },
        { tierNumber: 4, tierName: "Final Batch Tier", minTickets: 31, maxTickets: null, price: baseP + 300 }
      ]);
    }
  };

  const startingPrice = tiers.length > 0 ? (tiers[0].price || form.price || 500) : 500;

  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Tickets & Custom Dynamic Pricing</h2>
        <p className="wizard-section-desc">
          Customize pricing tiers, ticket prices, and seat ranges per tier for this workshop. Ticket prices adjust dynamically based on confirmed bookings.
        </p>
      </div>

      {/* Preset Quick Actions */}
      <div className="preset-bar-card">
        <div className="preset-bar-label">
          <Sliders size={16} />
          <span>Quick Tier Presets:</span>
        </div>
        <div className="preset-btn-group">
          <button type="button" className="preset-chip-btn" onClick={() => applyPreset("STANDARD_4")}>
            ⚡ Standard 4-Tier Progressive
          </button>
          <button type="button" className="preset-chip-btn" onClick={() => applyPreset("EARLY_BIRD")}>
            🌟 Early Bird + Regular (2 Tiers)
          </button>
          <button type="button" className="preset-chip-btn" onClick={() => applyPreset("FLAT")}>
            🏷️ Single Flat Price
          </button>
        </div>
      </div>

      {/* Interactive Editable Tier Pricing Grid */}
      <div className="pricing-tiers-card">
        <div className="pricing-tiers-header">
          <div>
            <h3 className="pricing-card-title">Configured Pricing Structure ({tiers.length} Tiers)</h3>
            <p className="pricing-card-sub">
              Admin customizable. Edit prices, seat limits, and names for each pricing phase.
            </p>
          </div>
          <div className="base-price-badge">
            <span className="base-label">Starting Price</span>
            <span className="base-amount">₹{startingPrice}</span>
          </div>
        </div>

        <div className="tiers-table-wrap">
          <table className="tiers-table editable-tiers-table">
            <thead>
              <tr>
                <th style={{ width: "90px" }}>Tier</th>
                <th>Classification Name</th>
                <th style={{ width: "130px" }}>Min Seat</th>
                <th style={{ width: "130px" }}>Max Seat</th>
                <th style={{ width: "150px" }}>Price Per Ticket</th>
                <th style={{ width: "60px" }}></th>
              </tr>
            </thead>
            <tbody>
              {tiers.map((t, idx) => (
                <tr key={idx} className={idx === 0 ? "tier-row-active" : ""}>
                  <td>
                    <span className="tier-number-badge">Tier {idx + 1}</span>
                  </td>
                  <td>
                    <input
                      type="text"
                      className="tier-input-field"
                      placeholder="Tier Name (e.g. Early Bird)"
                      value={t.tierName || ""}
                      onChange={(e) => handleTierChange(idx, "tierName", e.target.value)}
                    />
                  </td>
                  <td>
                    <input
                      type="number"
                      className="tier-input-field text-center"
                      min={1}
                      value={t.minTickets ?? 1}
                      onChange={(e) => handleTierChange(idx, "minTickets", parseInt(e.target.value, 10) || 1)}
                    />
                  </td>
                  <td>
                    <input
                      type="number"
                      className="tier-input-field text-center"
                      placeholder="Unlimited (+)"
                      value={t.maxTickets ?? ""}
                      onChange={(e) => {
                        const val = e.target.value.trim();
                        handleTierChange(idx, "maxTickets", val === "" ? null : parseInt(val, 10) || null);
                      }}
                    />
                  </td>
                  <td>
                    <div className="tier-price-input-wrap">
                      <span className="tier-rupee">₹</span>
                      <input
                        type="number"
                        className="tier-input-field price-input"
                        min={0}
                        placeholder="500"
                        value={t.price ?? ""}
                        onChange={(e) => handleTierChange(idx, "price", parseFloat(e.target.value) || 0)}
                      />
                    </div>
                  </td>
                  <td className="text-center">
                    {tiers.length > 1 && (
                      <button
                        type="button"
                        className="tier-delete-btn"
                        title="Remove Tier"
                        onClick={() => handleRemoveTier(idx)}
                      >
                        <Trash2 size={14} />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="add-tier-footer-row">
          <button type="button" className="btn-add-tier" onClick={handleAddTier}>
            <Plus size={15} />
            <span>Add Pricing Tier</span>
          </button>
        </div>
      </div>

      {/* Workshop Pass Types Manager (Solo, Duo, Overall Pass) */}
      <div className="pricing-tiers-card" style={{ marginTop: "24px" }}>
        <div className="pricing-tiers-header" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
          <div>
            <h3 className="pricing-card-title">
              Workshop Pass Types ({Array.isArray(form.passTypes) ? form.passTypes.length : 0})
            </h3>
            <p className="pricing-card-sub">
              Create structured pass packages (e.g., Solo 1-Session Pass, Duo 2-Session Pass, or an All-Access Pass covering all sessions).
            </p>
          </div>

          <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
            <button
              type="button"
              className="preset-chip-btn"
              onClick={() => {
                const currentPasses = Array.isArray(form.passTypes) ? form.passTypes : [];
                const baseP = Number(form.price) || 500;
                const newPass = {
                  id: undefined,
                  name: "Solo Pass",
                  description: "Access to 1 workshop session of your choice",
                  price: baseP,
                  sessionsIncluded: 1,
                  totalQuantity: 100,
                  displayOrder: currentPasses.length + 1,
                  isActive: true,
                };
                onChange("passTypes", [...currentPasses, newPass]);
              }}
            >
              + Solo Pass (1 Session)
            </button>
            <button
              type="button"
              className="preset-chip-btn"
              onClick={() => {
                const currentPasses = Array.isArray(form.passTypes) ? form.passTypes : [];
                const baseP = Number(form.price) || 500;
                const newPass = {
                  id: undefined,
                  name: "Duo Pass",
                  description: "Choose any 2 sessions from this workshop",
                  price: Math.round(baseP * 1.8),
                  sessionsIncluded: 2,
                  totalQuantity: 100,
                  displayOrder: currentPasses.length + 1,
                  isActive: true,
                };
                onChange("passTypes", [...currentPasses, newPass]);
              }}
            >
              + Duo Pass (2 Sessions)
            </button>
            <button
              type="button"
              className="preset-chip-btn"
              onClick={() => {
                const currentPasses = Array.isArray(form.passTypes) ? form.passTypes : [];
                const baseP = Number(form.price) || 500;
                const newPass = {
                  id: undefined,
                  name: "Trio Pass",
                  description: "Choose any 3 sessions from this workshop",
                  price: Math.round(baseP * 2.5),
                  sessionsIncluded: 3,
                  totalQuantity: 100,
                  displayOrder: currentPasses.length + 1,
                  isActive: true,
                };
                onChange("passTypes", [...currentPasses, newPass]);
              }}
            >
              + Trio Pass (3 Sessions)
            </button>
            <button
              type="button"
              className="preset-chip-btn"
              onClick={() => {
                const currentPasses = Array.isArray(form.passTypes) ? form.passTypes : [];
                const baseP = Number(form.price) || 500;
                const newPass = {
                  id: undefined,
                  name: "Overall Pass",
                  description: "All-access admission to all workshop sessions",
                  price: Math.round(baseP * 3.2),
                  sessionsIncluded: null,
                  totalQuantity: 100,
                  displayOrder: currentPasses.length + 1,
                  isActive: true,
                };
                onChange("passTypes", [...currentPasses, newPass]);
              }}
            >
              + Overall Pass (All Sessions)
            </button>
            <button
              type="button"
              className="preset-chip-btn"
              style={{ background: "rgba(255, 85, 0, 0.15)", color: "#FF5500", border: "1px solid rgba(255, 85, 0, 0.4)" }}
              onClick={() => {
                const baseP = Number(form.price) || 500;
                const defaultPasses = [
                  {
                    id: undefined,
                    name: "Solo Pass",
                    description: "Access to 1 workshop session of your choice",
                    price: baseP,
                    sessionsIncluded: 1,
                    totalQuantity: 100,
                    displayOrder: 1,
                    isActive: true,
                  },
                  {
                    id: undefined,
                    name: "Duo Pass",
                    description: "Choose any 2 sessions from this workshop",
                    price: Math.round(baseP * 1.8),
                    sessionsIncluded: 2,
                    totalQuantity: 100,
                    displayOrder: 2,
                    isActive: true,
                  },
                  {
                    id: undefined,
                    name: "Trio Pass",
                    description: "Choose any 3 sessions from this workshop",
                    price: Math.round(baseP * 2.5),
                    sessionsIncluded: 3,
                    totalQuantity: 100,
                    displayOrder: 3,
                    isActive: true,
                  },
                  {
                    id: undefined,
                    name: "Overall Pass",
                    description: "All-access admission to all workshop sessions",
                    price: Math.round(baseP * 3.2),
                    sessionsIncluded: null, // null indicates Overall Pass
                    totalQuantity: 100,
                    displayOrder: 4,
                    isActive: true,
                  },
                ];
                onChange("passTypes", defaultPasses);
              }}
            >
              ⚡ Auto-Generate Standard Suite
            </button>
          </div>
        </div>

        {/* Pass Types Table */}
        <div className="tiers-table-wrap">
          <table className="tiers-table editable-tiers-table">
            <thead>
              <tr>
                <th style={{ width: "160px" }}>Pass Name</th>
                <th>Description</th>
                <th style={{ width: "130px" }}>Price (₹)</th>
                <th style={{ width: "160px" }}>Sessions Included</th>
                <th style={{ width: "110px" }}>Pass Cap</th>
                <th style={{ width: "60px" }}></th>
              </tr>
            </thead>
            <tbody>
              {Array.isArray(form.passTypes) && form.passTypes.length > 0 ? (
                form.passTypes.map((p, idx) => {
                  const isOverall = p.sessionsIncluded == null;

                  return (
                    <tr key={idx}>
                      <td>
                        <input
                          type="text"
                          className="tier-input-field"
                          placeholder="e.g. Solo Pass"
                          value={p.name || ""}
                          onChange={(e) => {
                            const updated = [...form.passTypes];
                            updated[idx] = { ...p, name: e.target.value };
                            onChange("passTypes", updated);
                          }}
                        />
                      </td>
                      <td>
                        <input
                          type="text"
                          className="tier-input-field"
                          placeholder="Short description..."
                          value={p.description || ""}
                          onChange={(e) => {
                            const updated = [...form.passTypes];
                            updated[idx] = { ...p, description: e.target.value };
                            onChange("passTypes", updated);
                          }}
                        />
                      </td>
                      <td>
                        <div className="tier-price-input-wrap">
                          <span className="tier-rupee">₹</span>
                          <input
                            type="number"
                            className="tier-input-field price-input"
                            min={0}
                            placeholder="500"
                            value={p.price ?? ""}
                            onChange={(e) => {
                              const updated = [...form.passTypes];
                              updated[idx] = { ...p, price: parseFloat(e.target.value) || 0 };
                              onChange("passTypes", updated);
                            }}
                          />
                        </div>
                      </td>
                      <td>
                        <div style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
                          <label style={{ display: "inline-flex", alignItems: "center", gap: "6px", fontSize: "11px", cursor: "pointer", color: isOverall ? "#FF5500" : "inherit" }}>
                            <input
                              type="checkbox"
                              checked={isOverall}
                              onChange={(e) => {
                                const updated = [...form.passTypes];
                                updated[idx] = {
                                  ...p,
                                  sessionsIncluded: e.target.checked ? null : 1,
                                };
                                onChange("passTypes", updated);
                              }}
                            />
                            <span>All Sessions (Overall Pass)</span>
                          </label>
                          {!isOverall && (
                            <input
                              type="number"
                              min={1}
                              max={50}
                              className="tier-input-field"
                              placeholder="1"
                              value={p.sessionsIncluded ?? 1}
                              onChange={(e) => {
                                const updated = [...form.passTypes];
                                updated[idx] = {
                                  ...p,
                                  sessionsIncluded: parseInt(e.target.value, 10) || 1,
                                };
                                onChange("passTypes", updated);
                              }}
                            />
                          )}
                        </div>
                      </td>
                      <td>
                        <input
                          type="number"
                          min={1}
                          max={100000}
                          className="tier-input-field text-center"
                          value={p.totalQuantity ?? 1000}
                          onChange={(e) => {
                            const updated = [...form.passTypes];
                            updated[idx] = {
                              ...p,
                              totalQuantity: parseInt(e.target.value, 10) || 1000,
                            };
                            onChange("passTypes", updated);
                          }}
                        />
                      </td>
                      <td className="text-center">
                        <button
                          type="button"
                          className="tier-delete-btn"
                          title="Remove Pass Type"
                          onClick={() => {
                            const updated = form.passTypes.filter((_, i) => i !== idx);
                            onChange("passTypes", updated);
                          }}
                        >
                          <Trash2 size={14} />
                        </button>
                      </td>
                    </tr>
                  );
                })
              ) : (
                <tr>
                  <td colSpan={6} style={{ textAlign: "center", padding: "20px", color: "var(--text-muted, #71717a)", fontSize: "12px" }}>
                    No custom pass types configured. Click "+ Add Pass Type" or use the auto-generator above.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="add-tier-footer-row">
          <button
            type="button"
            className="btn-add-tier"
            onClick={() => {
              const currentPasses = Array.isArray(form.passTypes) ? form.passTypes : [];
              const newPass = {
                id: undefined,
                name: `Pass ${currentPasses.length + 1}`,
                description: "",
                price: Number(form.price) || 500,
                sessionsIncluded: 1,
                totalQuantity: 1000,
                displayOrder: currentPasses.length + 1,
                isActive: true,
              };
              onChange("passTypes", [...currentPasses, newPass]);
            }}
          >
            <Plus size={15} />
            <span>Add Pass Type</span>
          </button>
        </div>
      </div>

      {/* Workshop Total Capacity & Student Discount Notice */}
      <div className="wizard-form-grid-2">
        <div className="wizard-form-group">
          <label className="wizard-label">
            Total Workshop Capacity (Max Attendees) <span className="req">*</span>
          </label>
          <div className="wizard-input-wrap">
            <Users size={16} className="wizard-input-icon" />
            <input
              type="number"
              className={`wizard-input ${errors.capacity ? "has-error" : ""}`}
              min={5}
              max={5000}
              placeholder="e.g. 50"
              value={form.capacity || ""}
              onChange={(e) => onChange("capacity", parseInt(e.target.value, 10) || "")}
            />
          </div>
          {errors.capacity && <span className="wizard-error-text">{errors.capacity}</span>}
          <span className="wizard-input-hint">
            Registration automatically cuts off once capacity is reached or when workshop start time begins.
          </span>
        </div>

        {/* Student Discount Notice Card */}
        <div className="student-discount-card">
          <div className="discount-card-header">
            <GraduationCap size={20} className="discount-icon" />
            <h4 className="discount-card-title">Verified Student Concession</h4>
          </div>
          <p className="discount-card-text">
            Enrolled Ethos academy students receive an automatic <strong>₹100 discount</strong> verified and deducted server-side during the Razorpay checkout flow.
          </p>
          <div className="discount-badge-row">
            <span className="discount-status-pill">Active on Checkout</span>
            <span className="discount-rule">Student ID verification applied</span>
          </div>
        </div>
      </div>
    </div>
  );
}
