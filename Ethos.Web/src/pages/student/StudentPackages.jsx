import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Package, ShieldCheck, CheckCircle2, AlertCircle, Sparkles, ArrowRight, RefreshCw, Calendar, Clock } from "lucide-react";
import { studentApi } from "../../services/studentApi";
import { packagesApi } from "../../services/packagesApi";
import { paymentApi } from "../../services/paymentApi";
import { useAuth } from "../../context/AuthContext";
import { studentStateSync } from "../../services/studentStateSync";
import "./StudentPackages.css";

const packageStatusMap = {
  1: "Active",
  2: "Expired",
  3: "Exhausted",
  4: "Cancelled",
};

export default function StudentPackages() {
  const navigate = useNavigate();
  const { user } = useAuth();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [successMsg, setSuccessMsg] = useState("");
  const [buyingId, setBuyingId] = useState(null);

  const [activePackage, setActivePackage] = useState(null);
  const [allPackages, setAllPackages] = useState([]);
  const [packageHistory, setPackageHistory] = useState([]);

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    setError("");
    try {
      const [activeRes, catalogRes, historyRes] = await Promise.all([
        studentApi.getActivePackage().catch(() => null),
        packagesApi.getActivePackages().catch(() => []),
        studentApi.getPackages().catch(() => []),
      ]);

      setActivePackage(activeRes || null);
      setAllPackages(Array.isArray(catalogRes) ? catalogRes : []);
      setPackageHistory(Array.isArray(historyRes) ? historyRes : []);
    } catch (err) {
      console.error("Failed to load packages data:", err);
      setError("Unable to load package passes. Please try again.");
    } finally {
      setLoading(false);
    }
  }

  function resolveStatus(status) {
    if (typeof status === "string") return status;
    if (typeof status === "number") return packageStatusMap[status] || "Active";
    if (typeof status === "object") {
      if (typeof status.name === "string") return status.name;
      if (typeof status.value === "string") return status.value;
      if (typeof status.value === "number") return packageStatusMap[status.value] || "Active";
    }
    return "Active";
  }

  function formatDate(dateStr) {
    if (!dateStr) return "—";
    try {
      return new Date(dateStr).toLocaleDateString("en-IN", {
        weekday: "short",
        day: "numeric",
        month: "short",
        year: "numeric",
      });
    } catch {
      return dateStr;
    }
  }

  const allowed = activePackage?.classesAllowed ?? null;
  const used = activePackage?.classesUsed ?? 0;
  const remaining = allowed !== null ? Math.max(0, allowed - used) : "Unlimited";
  const statusStr = resolveStatus(activePackage?.status);

  return (
    <div className="student-packages-page">
      <div className="student-packages-container">

        {/* HERO SECTION */}
        <div className="student-packages-header">
          <div className="packages-header-left">
            <span className="packages-eyebrow">MEMBERSHIP & ACCESS</span>
            <h1 className="packages-title">My Membership Packages</h1>
            <p className="packages-subtitle">
              Manage your active class passes, view credit balances, and explore studio pass tiers.
            </p>
          </div>
          <div className="packages-header-right">
            <Link to="/student/classes" className="packages-action-pill">
              Explore Regular Classes →
            </Link>
          </div>
        </div>

        {/* ALERTS */}
        {error && (
          <div className="packages-alert packages-alert--error">
            <AlertCircle size={16} />
            <span>{error}</span>
            <button type="button" onClick={() => setError("")}>✕</button>
          </div>
        )}
        {successMsg && (
          <div className="packages-alert packages-alert--success">
            <CheckCircle2 size={16} />
            <span>{successMsg}</span>
            <button type="button" onClick={() => setSuccessMsg("")}>✕</button>
          </div>
        )}

        {/* ACTIVE MEMBERSHIP HERO CARD */}
        <section className="active-pass-hero">
          <div className="pass-hero-glass">
            <div className="pass-hero-header">
              <div className="pass-badge-group">
                <span className="pass-hero-badge">CURRENT MEMBERSHIP PASS</span>
                <span className={`pass-status-indicator pass-status--${statusStr.toLowerCase()}`}>
                  ● {statusStr.toUpperCase()}
                </span>
              </div>
              {activePackage && (
                <span className="pass-expiry-tag">
                  Valid until: <strong>{formatDate(activePackage.expiryDate)}</strong>
                </span>
              )}
            </div>

            <div className="pass-hero-body">
              <div className="pass-info-left">
                <h2 className="pass-package-name">
                  {activePackage ? activePackage.packageName : "No Active Package"}
                </h2>
                <p className="pass-package-desc">
                  {activePackage
                    ? "Allows admission to regular weekly studio classes with elite choreography faculty."
                    : "You currently do not have an active membership pass. Select a package below to start enrolling in studio classes."}
                </p>

                {activePackage && (
                  <div className="pass-specs-list">
                    <div className="pass-spec-item">
                      <Calendar size={14} />
                      <span>Start Date: <strong>{formatDate(activePackage.startDate)}</strong></span>
                    </div>
                    <div className="pass-spec-item">
                      <Clock size={14} />
                      <span>Expiry Date: <strong>{formatDate(activePackage.expiryDate)}</strong></span>
                    </div>
                  </div>
                )}
              </div>

              {activePackage && (
                <div className="pass-info-right">
                  <div className="credits-counter-card">
                    <span className="credits-label">CLASSES REMAINING</span>
                    <strong className="credits-big-num">{remaining}</strong>
                    <span className="credits-sub">
                      {allowed !== null ? `${used} / ${allowed} classes used` : "Unlimited studio access"}
                    </span>
                  </div>
                </div>
              )}
            </div>

            <div className="pass-hero-footer">
              <div className="pass-perk-item">
                <ShieldCheck size={16} className="perk-icon" />
                <span>₹100 Student Masterclass Discount Included</span>
              </div>
              <div className="pass-perk-item">
                <Sparkles size={16} className="perk-icon" />
                <span>Priority Studio Reservations</span>
              </div>
            </div>
          </div>
        </section>

        {/* AVAILABLE MEMBERSHIP PACKAGES */}
        <section className="packages-catalog-section">
          <div className="section-head">
            <div>
              <span className="section-eyebrow">STUDIO PASS TIERS</span>
              <h2>Available Membership Passes</h2>
            </div>
            <span className="section-count-pill">{allPackages.length} Tiers Available</span>
          </div>

          <div className="packages-grid">
            {allPackages.map((pkg) => {
              const isCurrent = activePackage?.packageId === pkg.id || activePackage?.packageName === pkg.name;

              return (
                <div
                  key={pkg.id}
                  className={`package-card ${isCurrent ? "package-card--current" : ""}`}
                >
                  {isCurrent && (
                    <div className="package-current-flag">CURRENT ACTIVE PASS</div>
                  )}

                  <div className="package-card-header">
                    <h3 className="package-tier-name">{pkg.name}</h3>
                    <div className="package-price-wrap">
                      <span className="currency">₹</span>
                      <strong className="amount">{Number(pkg.price).toLocaleString("en-IN")}</strong>
                      <span className="duration">/ {pkg.validityDays} days</span>
                    </div>
                  </div>

                  <p className="package-desc">{pkg.description || "Full access to studio classes and student benefits."}</p>

                  <div className="package-features">
                    <div className="feature-item">
                      <CheckCircle2 size={15} className="feature-icon" />
                      <span>{pkg.classesAllowed ? `${pkg.classesAllowed} Classes Included` : "Unlimited Studio Classes"}</span>
                    </div>
                    <div className="feature-item">
                      <CheckCircle2 size={15} className="feature-icon" />
                      <span>{pkg.validityDays} Days Pass Validity</span>
                    </div>
                    <div className="feature-item">
                      <CheckCircle2 size={15} className="feature-icon" />
                      <span>Exclusive ₹100 Workshop Discount</span>
                    </div>
                    <div className="feature-item">
                      <CheckCircle2 size={15} className="feature-icon" />
                      <span>Free Schedule Rescheduling</span>
                    </div>
                  </div>

                  <div className="package-card-footer">
                    <Link
                      to="/classes"
                      className="package-cta-btn"
                    >
                      {isCurrent ? "RENEW / EXTEND PASS" : "SELECT & PURCHASE PASS"}
                    </Link>
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        {/* PACKAGE HISTORY */}
        {packageHistory.length > 0 && (
          <section className="package-history-section">
            <div className="section-head">
              <div>
                <span className="section-eyebrow">PASS RECORDS</span>
                <h2>Membership History</h2>
              </div>
            </div>

            <div className="history-table-wrap">
              <table className="packages-history-table">
                <thead>
                  <tr>
                    <th>PACKAGE</th>
                    <th>START DATE</th>
                    <th>EXPIRY DATE</th>
                    <th>CLASSES USED</th>
                    <th>STATUS</th>
                  </tr>
                </thead>
                <tbody>
                  {packageHistory.map((item) => (
                    <tr key={item.id}>
                      <td><strong>{item.packageName}</strong></td>
                      <td>{formatDate(item.startDate)}</td>
                      <td>{formatDate(item.expiryDate)}</td>
                      <td>{item.classesUsed} / {item.classesAllowed ?? "∞"}</td>
                      <td>
                        <span className={`history-status-badge status-${resolveStatus(item.status).toLowerCase()}`}>
                          {resolveStatus(item.status)}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}

      </div>
    </div>
  );
}
