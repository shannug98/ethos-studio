import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import "./TrainerApplicationTier.css";
import tierArtwork from "../../assets/trainer/ethos-tier-emblems.png";

const tierPositions = {
  SILVER: "silver",
  GOLD: "gold",
  DIAMOND: "diamond",
  PLATINUM: "platinum",
};

const tierTaglines = {
  SILVER: "BUILD YOUR FOUNDATION",
  GOLD: "EXPAND YOUR IMPACT",
  DIAMOND: "ELEVATE WITHOUT LIMITS",
  PLATINUM: "LEAD A BRIGHTER TOMORROW",
};

const tierBenefits = {
  SILVER: [
    "Teach regular classes",
    "Access to standard scheduling",
    "Build your profile and student base",
  ],

  GOLD: [
    "Teach regular & advanced classes",
    "Create and host workshops",
    "Greater visibility and growth tools",
  ],

  DIAMOND: [
    "Priority workshop scheduling",
    "Featured opportunities on Ethos",
    "Access to premium opportunities",
  ],

  PLATINUM: [
    "Full platform access",
    "Exclusive events & collaborations",
    "Highest visibility and support",
  ],
};

export default function TrainerApplicationTier() {
  const navigate = useNavigate();

  const [tiers, setTiers] = useState([]);
  const [selectedTierId, setSelectedTierId] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    async function loadData() {
      try {
        setLoading(true);
        setError("");

        const [tiersRes, appRes] = await Promise.all([
          trainerApi.getTiers(),
          trainerApi.getApplication().catch(() => null),
        ]);

        const items = Array.isArray(tiersRes) ? tiersRes : [];

        setTiers(items);

        if (appRes?.tierId || appRes?.tier?.id) {
          setSelectedTierId(appRes.tierId || appRes.tier.id);
        } else if (items.length > 0) {
          setSelectedTierId(items[0].id);
        }
      } catch (err) {
        setError(err.message || "Failed to load tier options.");
      } finally {
        setLoading(false);
      }
    }

    loadData();
  }, []);

  async function handleSelectTier(tierId) {
    if (!tierId) return;

    setSelectedTierId(tierId);
    setError("");

    try {
      setSaving(true);

      await trainerApi.updateApplication({
        tierId,
      });

      navigate("/trainer/application/payment");
    } catch (err) {
      setError(err.message || "Failed to save tier selection.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <main className="trainer-tier-page">
        <div className="trainer-tier-shell">
          <div className="trainer-tier-loading">
            Loading available tiers...
          </div>
        </div>
      </main>
    );
  }

  const activeTier = tiers.find((t) => t.id === selectedTierId) || tiers[0];
  const activeCode = (activeTier?.code || "").toUpperCase();
  const activePosition = tierPositions[activeCode] || "silver";
  const activeBenefits = tierBenefits[activeCode] || [];
  const activeTagline = tierTaglines[activeCode] || "ETHOS TRAINER PATHWAY";

  return (
    <main className="trainer-tier-page">
      <div className="trainer-tier-shell">

        {/* BACK LINK */}
        <button
          type="button"
          className="trainer-application-back"
          onClick={() => navigate("/trainer/application/introduction")}
          style={{ marginBottom: "24px", cursor: "pointer", background: "none", border: "none" }}
        >
          ← BACK TO INTRODUCTION
        </button>

        {/* 5-STEP PROGRESS BAR */}
        <div className="trainer-details-progress">
          <div className="trainer-progress-item trainer-progress-item--done">
            <span>01</span>
            <strong>PROFILE</strong>
          </div>

          <div className="trainer-progress-line trainer-progress-line--done" />

          <div className="trainer-progress-item trainer-progress-item--done">
            <span>02</span>
            <strong>INTRODUCTION</strong>
          </div>

          <div className="trainer-progress-line trainer-progress-line--done" />

          <div className="trainer-progress-item trainer-progress-item--active">
            <span>03</span>
            <strong>TIER</strong>
          </div>

          <div className="trainer-progress-line" />

          <div className="trainer-progress-item">
            <span>04</span>
            <strong>REVIEW</strong>
          </div>
        </div>

        {/* HEADER */}
        <header className="trainer-tier-header">
          <div className="trainer-tier-header-number">
            03
          </div>

          <div>
            <span className="trainer-tier-eyebrow">
              STEP 04 — TIER SELECTION
            </span>

            <h1>
              Choose your starting tier
            </h1>

            <p>
              Select your pathway to discover the benefits and application fee.
            </p>
          </div>
        </header>

        {/* ERROR */}
        {error && (
          <div className="trainer-tier-error">
            {error}
          </div>
        )}

        {/* HORIZONTAL TIER SELECTOR NAV */}
        <nav className="trainer-tier-nav" aria-label="Trainer Tier Selection">
          {tiers.map((tier, index) => {
            const isSelected = tier.id === (activeTier?.id || selectedTierId);
            return (
              <button
                key={tier.id}
                type="button"
                className={`trainer-tier-nav-item ${isSelected ? "is-active" : ""}`}
                onClick={() => {
                  setSelectedTierId(tier.id);
                  setError("");
                }}
              >
                <div className="trainer-tier-nav-top">
                  <span className="trainer-tier-nav-number">
                    {String(index + 1).padStart(2, "0")}
                  </span>
                </div>
                <div className="trainer-tier-nav-name">{tier.name}</div>
                <div className="trainer-tier-nav-bar" />
              </button>
            );
          })}
        </nav>

        {/* SELECTED TIER PANEL */}
        {activeTier && (
          <div className="trainer-tier-showcase">

            {/* TOP AREA: ARTWORK LEFT, CONTENT RIGHT */}
            <div className="trainer-tier-showcase-top">

              {/* HERO ARTWORK */}
              <div className="trainer-tier-showcase-art">
                <img
                  src={tierArtwork}
                  alt={`${activeTier.name} emblem`}
                  className={`trainer-tier-art-image trainer-tier-art-${activePosition}`}
                />
              </div>

              {/* SHOWCASE CONTENT */}
              <div className="trainer-tier-showcase-content">

                <div className="trainer-tier-showcase-header">
                  <h2>{activeTier.name}</h2>
                  <span className="trainer-tier-showcase-eyebrow">
                    {activeTagline}
                  </span>

                  <p className="trainer-tier-showcase-description">
                    {activeTier.description}
                  </p>
                </div>

                {/* BENEFITS */}
                <div className="trainer-tier-showcase-section">
                  <span className="trainer-tier-showcase-label">BENEFITS</span>
                  <div className="trainer-tier-showcase-benefits">
                    {activeBenefits.map((benefit, bIndex) => (
                      <div className="trainer-tier-showcase-benefit" key={bIndex}>
                        <span className="trainer-tier-showcase-mark" aria-hidden="true">
                          {bIndex === 0 ? "✦" : bIndex === 1 ? "□" : "▪"}
                        </span>
                        <span>{benefit}</span>
                      </div>
                    ))}
                  </div>
                </div>

              </div>

            </div>

            {/* HORIZONTAL DIVIDER */}
            <div className="trainer-tier-showcase-divider" />

            {/* BOTTOM AREA: FEE ROW */}
            <div className="trainer-tier-showcase-fee-row">
              <div className="trainer-tier-showcase-fee-left">
                <span className="trainer-tier-showcase-fee-label">APPLICATION FEE</span>
                <span className="trainer-tier-showcase-fee-note">
                  One-time application fee
                </span>
              </div>

              <div className="trainer-tier-showcase-fee-right">
                <span className="trainer-tier-showcase-price">
                  {activeTier.applicationFee !== undefined && activeTier.applicationFee !== null
                    ? `₹ ${Number(activeTier.applicationFee).toLocaleString("en-IN")}`
                    : "Included"}
                </span>
              </div>
            </div>

          </div>
        )}

        {/* ACTIONS */}
        <div className="trainer-tier-actions" style={{ display: "flex", gap: "12px" }}>

          <button
            type="button"
            className="trainer-application-button trainer-application-button--back"
            onClick={() => navigate("/trainer/application/introduction")}
            style={{
              minHeight: "56px",
              padding: "0 24px",
              border: "1px solid rgba(255, 255, 255, 0.16)",
              background: "transparent",
              color: "rgba(244, 238, 232, 0.65)",
              cursor: "pointer",
              fontSize: "9px",
              fontWeight: "700",
              letterSpacing: "0.16em",
              textTransform: "uppercase"
            }}
          >
            ← BACK
          </button>

          <button
            type="button"
            className="trainer-tier-continue"
            disabled={!selectedTierId || saving}
            onClick={() =>
              handleSelectTier(selectedTierId)
            }
            style={{ flex: 1 }}
          >
            {saving
              ? "SAVING..."
              : `PAY ₹${Number(activeTier?.applicationFee || 0).toLocaleString("en-IN")} & CONTINUE →`}
          </button>

        </div>

        {/* FOOTER */}
        <footer className="trainer-tier-footer">
          <span>ETHOS DANCE STUDIO</span>
          <span>TRAINER REGISTRATION</span>
        </footer>

      </div>
    </main>
  );
}
