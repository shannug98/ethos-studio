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
