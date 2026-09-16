import React from "react";
import { Users, IndianRupee, Sparkles, GraduationCap, AlertCircle, TrendingUp } from "lucide-react";

export const AUTHORITATIVE_TIERS = [
  { tier: 1, range: "1 – 10 Bookings", price: 500, label: "Early Bird Tier" },
  { tier: 2, range: "11 – 20 Bookings", price: 600, label: "Standard Tier" },
  { tier: 3, range: "21 – 30 Bookings", price: 700, label: "Peak Tier" },
  { tier: 4, range: "31+ Bookings", price: 800, label: "Final Batch Tier" },
];

export default function Step4Pricing({ form, onChange, errors }) {
  return (
    <div className="wizard-step-panel">
      <div className="wizard-section-header">
        <h2 className="wizard-section-title">Tickets & Dynamic Pricing</h2>
        <p className="wizard-section-desc">
          Ethos implements a server-authoritative 4-tier progressive pricing model. Ticket prices automatically adjust based exclusively on confirmed bookings.
        </p>
      </div>

      {/* Authoritative 4-Tier Pricing Grid */}
      <div className="pricing-tiers-card">
        <div className="pricing-tiers-header">
          <div>
            <h3 className="pricing-card-title">Authoritative 4-Tier Price Structure</h3>
            <p className="pricing-card-sub">
              Starting price is ₹500. As registrations grow, each subsequent batch of 10 attendees scales automatically.
            </p>
          </div>
          <div className="base-price-badge">
            <span className="base-label">Starting Price</span>
            <span className="base-amount">₹500</span>
          </div>
        </div>

        <div className="tiers-table-wrap">
          <table className="tiers-table">
            <thead>
              <tr>
                <th>Tier</th>
                <th>Booking Range</th>
                <th>Price Per Ticket</th>
                <th>Tier Classification</th>
              </tr>
            </thead>
            <tbody>
              {AUTHORITATIVE_TIERS.map((t) => (
                <tr key={t.tier} className={t.tier === 1 ? "tier-row-active" : ""}>
                  <td>
                    <span className="tier-number-badge">Tier {t.tier}</span>
                  </td>
                  <td className="tier-range-cell">{t.range}</td>
                  <td className="tier-price-cell">
                    <span className="rupee-sym">₹</span>{t.price}
                  </td>
                  <td>
                    <span className={`tier-class-pill tier-${t.tier}`}>{t.label}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Workshop Total Capacity */}
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
              max={500}
              placeholder="e.g. 50"
              value={form.capacity}
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
