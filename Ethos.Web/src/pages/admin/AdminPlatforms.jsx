import React, { useState, useEffect } from "react";
import { adminApi } from "../../services/adminApi";
import AdminKpiCard from "../../components/admin/common/AdminKpiCard";
import "./AdminPlatforms.css";

export default function AdminPlatforms() {
  const [activeCategory, setActiveCategory] = useState("all");
  const [searchQuery, setSearchQuery] = useState("");
  const [expandedPlatform, setExpandedPlatform] = useState(null);
  const [healthData, setHealthData] = useState(null);
  const [pinging, setPinging] = useState(false);
  const [lastPingTime, setLastPingTime] = useState(null);
  const [pingError, setPingError] = useState(null);

  const fetchHealth = async () => {
    setPinging(true);
    setPingError(null);
    try {
      const res = await adminApi.getDeepHealthCheck();
      setHealthData(res);
      setLastPingTime(new Date());
    } catch (err) {
      setPingError(err.message || "Failed to reach health check endpoint.");
    } finally {
      setPinging(false);
    }
  };

  useEffect(() => {
    fetchHealth();
  }, []);

  const platforms = [
    {
      id: "neon-db",
      name: "Neon Serverless PostgreSQL",
      category: "core",
      badge: "Primary Database",
      status: "operational",
      statusLabel: "Operational · Active",
      icon: "🐘",
      techStack: "PostgreSQL 16 · Serverless Autoscaling",
      provider: "Neon Inc. (Hosted on AWS ap-southeast-1)",
      latency: healthData?.database?.latencyMs != null ? `${healthData.database.latencyMs}ms` : "< 18ms",
      endpoint: "ep-tiny-violet-b3s8ui17-pooler.c-4.ap-southeast-1.aws.neon.tech",
      port: "5432 (PgBouncer Pooled)",
      role: "Central System of Record for all Studio Entities",
      description:
        "Primary relational database powering the entire studio operations. All user profiles, roles, workshops, class enrollments, payment ledgers, and immutable audit logs reside exclusively in Neon.",
      whatItDoes: [
        "Stores and manages all 15+ relational entities including Users, Roles, Trainers, Classes, Workshops, Bookings, Transactions, and Audit Logs.",
        "Zero mock data mode: cleanly initialized schema provisioned with administrative credentials only.",
        "Provides serverless auto-scaling compute that dynamically scales during high-traffic workshop release periods.",
        "Connection pooling via PgBouncer with SSL Mode 'require' and channel binding verification.",
        "Continuous automated point-in-time recovery (PITR) and instant branching for non-destructive schema migrations."
      ],
      managedEntities: [
        "users",
        "user_roles",
        "trainer_profiles",
        "classes",
        "workshops",
        "workshop_bookings",
        "class_enrollments",
        "attendance_records",
        "payments",
        "payment_timeline_events",
        "dance_packages",
        "admin_sessions",
        "admin_audit_logs",
        "incident_records"
      ],
      securitySpecs: [
        "Enforced SSL/TLS connection (sslmode=require)",
        "SCRAM-SHA-256 authenticated credentials",
        "Strict Foreign Key constraints and cascading referential integrity",
        "Separate connection pool for administrative telemetry"
      ],
      dataFlow: "ASP.NET Core EF Core DbContext ➔ Neon PgBouncer Pooler ➔ Primary Read/Write Compute Replica"
    },
    {
      id: "razorpay",
      name: "Razorpay Payment Gateway",
      category: "payments",
      badge: "Payment Rails",
      status: "operational",
      statusLabel: "Operational · Active",
      icon: "💳",
      techStack: "Razorpay Standard Checkout & Webhook Engine",
      provider: "Razorpay Software Pvt. Ltd.",
      latency: "Real-time Gateway",
      endpoint: "api.razorpay.com/v1",
      port: "443 (HTTPS)",
      role: "Checkout Orchestration, Payment Capture & Digital Receipts",
      description:
        "Processes all student payments for dance workshops and membership packages. Handles secure UPI, Cards, NetBanking, and verifies webhook signatures.",
      whatItDoes: [
        "Generates unique order IDs (`order_xxxx`) when students initiate checkout for workshops or packages.",
        "Presents secure Razorpay Checkout modal for UPI (GPay, PhonePe, Paytm), Credit/Debit Cards, and NetBanking.",
        "Verifies cryptographic payment signatures on backend using HMAC-SHA256 (`order_id|payment_id` + KeySecret).",
        "Receives real-time asynchronous webhooks (`payment.captured`, `payment.failed`, `refund.processed`) to guarantee idempotency.",
        "Issues non-GST compliant digital booking receipts with verified transaction hashes."
      ],
      managedEntities: [
        "razorpay_order_id",
        "razorpay_payment_id",
        "razorpay_signature",
        "payment_status",
        "refund_records",
        "payment_timeline_events"
      ],
      securitySpecs: [
        "HMAC-SHA256 server-side signature validation",
        "PCI-DSS Level 1 compliant card handling (no card data touches studio servers)",
        "Idempotency keys on all refund and payment resolution requests",
        "Secret webhook signature validation header (X-Razorpay-Signature)"
      ],
      dataFlow: "Student UI ➔ Razorpay Checkout ➔ Razorpay Gateway ➔ Ethos Webhook Receiver ➔ Neon Ledger"
    },
    {
      id: "cloudflare-r2",
      name: "Cloudflare R2 Object Storage",
      category: "storage",
      badge: "Media CDN",
      status: "operational",
      statusLabel: "Operational · Active",
      icon: "☁️",
      techStack: "S3-Compatible Object Store · Anycast Global CDN",
      provider: "Cloudflare, Inc.",
      latency: "Global Edge < 25ms",
      endpoint: "media.ethosdancestudio.com",
      port: "443 (HTTPS)",
      role: "High-Speed Media Hosting with Zero Egress Fees",
      description:
        "Global edge object storage for workshop promo posters, class demonstration reels, trainer portfolio videos, and student community showcase media.",
      whatItDoes: [
        "Stores high-resolution promotional artwork and banners for upcoming workshops and guest masterclasses.",
        "Hosts video demonstration clips and trainer choreography reels for student preview.",
        "Provides zero-egress cost downloads globally, eliminating cloud egress penalty charges.",
        "Generates secure presigned upload URLs for administrator and trainer media submissions.",
        "Integrates with Cloudflare global Anycast CDN for instant worldwide caching and low-latency streaming."
      ],
      managedEntities: [
        "workshop_banner_images",
        "trainer_headshots",
        "choreography_preview_videos",
        "student_gallery_items",
        "brand_logos_and_badges"
      ],
      securitySpecs: [
        "AWS S3 SigV4 authentication via Cloudflare R2 credentials",
        "Content-Type validation and file sanitization prior to ingestion",
        "Public asset delivery restricted to `media.ethosdancestudio.com` domain",
        "No direct directory listing; unique UUID-based object naming"
      ],
      dataFlow: "Admin Upload ➔ CloudflareR2MediaStorageService ➔ R2 S3 Bucket ➔ Anycast Edge CDN ➔ Browser"
    },
    {
      id: "aspnet-api",
      name: "ASP.NET Core 10 Web API",
      category: "core",
      badge: "Application Runtime",
      status: "operational",
      statusLabel: "Operational · Port 5252",
      icon: "⚡",
      techStack: "C# .NET 10.0 · Kestrel High-Throughput Engine",
      provider: "Ethos Studio Backend Host",
      latency: "< 5ms Internal",
      endpoint: "http://localhost:5252 · api.ethosdancestudio.com",
      port: "5252 (Dev) / 443 (Prod)",
      role: "Business Rules, REST API Gateway & Security Pipeline",
      description:
        "The central backend nervous system orchestrating all workflows, RBAC validations, real-time telemetry, rate limiting, and administrative mutations.",
      whatItDoes: [
        "Serves 40+ REST API endpoints across `/api/admin/*`, `/api/v1/workshops`, `/api/v1/classes`, and `/api/v1/payments`.",
        "Enforces ASP.NET Core RateLimiter middleware to protect against DDoS and brute-force credential stuffing.",
        "Applies strict CORS policy restricting API interactions to approved studio web domains.",
        "Captures structured observability logs and generates distributed correlation IDs (`X-Trace-Id`) for every HTTP request.",
        "Executes background maintenance jobs (e.g. session cleanup, expired booking release) via HostedServices."
      ],
      managedEntities: [
        "API Request Logs",
        "Trace Correlation IDs",
        "Rate Limiting Leases",
        "Global Exception Handlers",
        "CORS Security Headers"
      ],
      securitySpecs: [
        "Fixed-window sliding rate limiters per IP / identity",
        "Strict Same-Origin and CORS policy validation",
        "Anti-tamper security headers (HSTS, X-Content-Type-Options, Referrer-Policy)",
        "Asynchronous non-blocking I/O across all EF Core database calls"
      ],
      dataFlow: "Vite Client / Public Users ➔ Kestrel HTTP Pipeline ➔ Middleware ➔ Admin Controllers ➔ Domain Services"
    },
    {
      id: "auth-mfa",
      name: "HMAC-SHA256 JWT & Admin MFA Authority",
      category: "auth",
      badge: "Identity Authority",
      status: "operational",
      statusLabel: "Operational · Active",
      icon: "🛡️",
      techStack: "JSON Web Tokens · Cryptographic Salt",
      provider: "Internal Ethos Identity Engine",
      latency: "< 1ms Crypto",
      endpoint: "In-Process Token & Session Authority",
      port: "Internal",
      role: "Zero-Trust Identity, Device Fingerprinting & Session Control",
      description:
        "Authoritative security subsystem managing administrator sign-ins, phone OTP verification, 2-device concurrency limits, and token issuance.",
      whatItDoes: [
        "Issues cryptographically signed JWT tokens containing claims, role identities, and approved session IDs.",
        "Enforces strict 2-device concurrent login slots per admin partner (Hotstar-style remote eviction).",
        "Generates 6-digit cryptographic verification codes with 5-minute expiry windows.",
        "Performs real-time hardware fingerprinting (IP address, user agent, browser engine, operating system).",
        "Provides immediate remote session termination from the Admin Login Devices console."
      ],
      managedEntities: [
        "active_admin_sessions",
        "device_fingerprints",
        "verification_otps",
        "jwt_revocation_ledger",
        "security_audit_events"
      ],
      securitySpecs: [
        "HMAC-SHA256 cryptographic signature verification",
        "Exponential backoff on repeated failed login attempts",
        "Immediate invalidation on remote device revocation",
        "RBAC claims verification on every administrative action"
      ],
      dataFlow: "Admin Phone Entry ➔ Secure OTP Dispatch ➔ Token Issue ➔ Hardware Slot Verification ➔ Admin Bearer Auth"
    },
    {
      id: "transactional-email",
      name: "Transactional Email Dispatcher",
      category: "comms",
      badge: "Notification Rail",
      status: "operational",
      statusLabel: "Operational · Ready",
      icon: "✉️",
      techStack: "SMTP Relay · TLS 1.3 · Templated Engine",
      provider: "Studio SMTP & Notification Dispatcher",
      latency: "< 500ms Dispatch",
      endpoint: "Transactional SMTP Relay",
      port: "587 (TLS)",
      role: "Booking Receipts, Security Alerts & Workshop Confirmations",
      description:
        "Reliable outbound notification channel delivering critical booking receipts, registration confirmations, and security alerts to students and administrators.",
      whatItDoes: [
        "Dispatches payment receipts with verified transaction hashes and student registration IDs.",
        "Sends immediate workshop booking confirmations with venue maps and class arrival instructions.",
        "Alerts administrators when a new login occurs from an unrecognized device or unfamiliar location.",
        "Provides cancellation and refund confirmation notices to affected students.",
        "Maintains an immutable outbound dispatch log for auditability and delivery tracking."
      ],
      managedEntities: [
        "email_delivery_logs",
        "booking_receipt_templates",
        "security_alert_dispatches",
        "refund_notification_records"
      ],
      securitySpecs: [
        "Mandatory TLS 1.3 encrypted transport",
        "Anti-spoofing SPF, DKIM, and DMARC alignment",
        "Sanitized HTML template rendering preventing script injection",
        "Delivery failure retry queue with dead-letter monitoring"
      ],
      dataFlow: "API Event Trigger ➔ Email Template Renderer ➔ SMTP Dispatcher ➔ Student Inbox"
    },
    {
      id: "whatsapp-cloud",
      name: "Meta / Twilio WhatsApp Cloud API",
      category: "comms",
      badge: "Instant Messaging",
      status: "setup-needed",
      statusLabel: "Setup Incomplete · Config Ready",
      icon: "📱",
      techStack: "Meta Graph WhatsApp Cloud API / Twilio Messaging",
      provider: "Meta Platforms Inc. / Twilio Inc.",
      latency: "Mobile Push Notification",
      endpoint: "graph.facebook.com/v18.0",
      port: "443 (HTTPS)",
      role: "Instant Class Reminders & Urgent Schedule Alerts",
      description:
        "Direct mobile messaging rail designed to send timely class reminders, last-minute room adjustments, and instant workshop confirmation messages to students' phones.",
      whatItDoes: [
        "Delivers instant booking confirmation messages with pass details directly to students' WhatsApp accounts.",
        "Sends automated 2-hour reminders before class or workshop commencement to boost attendance.",
        "Broadcasts urgent weather, instructor illness, or emergency schedule adjustments directly to enrolled students.",
        "Provides inbound response tracking for student attendance confirmations.",
        "Awaiting Meta Business Manager verification to transition from sandbox to live production broadcasting."
      ],
      managedEntities: [
        "whatsapp_message_logs",
        "approved_message_templates",
        "student_phone_numbers",
        "delivery_receipt_status"
      ],
      securitySpecs: [
        "Bearer token authentication with Meta Graph API",
        "End-to-end WhatsApp platform encryption for delivered messages",
        "Opt-in consent validation to prevent unsolicited messages",
        "Encrypted webhook payload verification for delivery receipts"
      ],
      dataFlow: "Notification Event ➔ WhatsApp Service ➔ Meta Cloud API ➔ Student WhatsApp App"
    }
  ];

  const filteredPlatforms = platforms.filter((p) => {
    const matchesCategory = activeCategory === "all" || p.category === activeCategory;
    const matchesSearch =
      searchQuery.trim() === "" ||
      p.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      p.role.toLowerCase().includes(searchQuery.toLowerCase()) ||
      p.techStack.toLowerCase().includes(searchQuery.toLowerCase()) ||
      p.provider.toLowerCase().includes(searchQuery.toLowerCase()) ||
      p.description.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesCategory && matchesSearch;
  });

  const categoryCounts = {
    all: platforms.length,
    core: platforms.filter((p) => p.category === "core").length,
    payments: platforms.filter((p) => p.category === "payments").length,
    storage: platforms.filter((p) => p.category === "storage").length,
    auth: platforms.filter((p) => p.category === "auth").length,
    comms: platforms.filter((p) => p.category === "comms").length
  };

  return (
    <div className="admin-platforms-page">
      {/* Page Header */}
      <div className="admin-page-header">
        <div>
          <div className="admin-breadcrumb-tag">SYSTEM ARCHITECTURE · INTEGRATIONS</div>
          <h1 className="admin-page-title">Connected Platforms & External Services</h1>
          <p className="admin-page-subtitle">
            Comprehensive breakdown of all external databases, payment rails, cloud infrastructure, and messaging services integrated into Ethos Dance Studio.
          </p>
        </div>
        <div className="admin-header-actions">
          <button
            type="button"
            className="btn btn-secondary platform-ping-btn"
            onClick={fetchHealth}
            disabled={pinging}
          >
            <span className={`ping-indicator-dot ${pinging ? "pulse" : ""}`}></span>
            {pinging ? "Pinging Services..." : "Ping Platform Health"}
          </button>
        </div>
      </div>

      {/* KPI Overview Grid */}
      <div className="admin-kpi-grid">
        <AdminKpiCard
          label="Primary Database"
          value="Neon Serverless"
          tone="brand"
          sublabel="AWS ap-southeast-1 · 0 Mock Records"
          icon="🐘"
        />
        <AdminKpiCard
          label="Payment Gateway"
          value="Razorpay"
          tone="success"
          sublabel="Standard Checkout & Webhooks"
          icon="💳"
        />
        <AdminKpiCard
          label="Media & Object CDN"
          value="Cloudflare R2"
          tone="info"
          sublabel="Zero Egress S3 Storage"
          icon="☁️"
        />
        <AdminKpiCard
          label="API Engine"
          value="ASP.NET Core 10"
          tone="success"
          sublabel="Kestrel Host · Port 5252"
          icon="⚡"
        />
      </div>

      {pingError && <div className="alert alert-danger mb-4">{pingError}</div>}

      {/* Architecture Visual Map */}
      <div className="admin-card platforms-architecture-card">
        <div className="platforms-architecture-header">
          <div>
            <span className="platform-eyebrow">INTEGRATION TOPOLOGY</span>
            <h2>Ethos Dance Studio End-to-End Data Flow</h2>
          </div>
          {lastPingTime && (
            <span className="architecture-timestamp">
              Last Verified: {lastPingTime.toLocaleTimeString()}
            </span>
          )}
        </div>
        <div className="architecture-flow-diagram">
          <div className="flow-node flow-client">
            <div className="flow-icon">💻</div>
            <div className="flow-title">Client Tier</div>
            <div className="flow-desc">Vite React Frontend (Port 5173 / Production CDN)</div>
          </div>
          <div className="flow-arrow">➔</div>
          <div className="flow-node flow-gateway">
            <div className="flow-icon">⚡</div>
            <div className="flow-title">Application Core</div>
            <div className="flow-desc">ASP.NET Core 10 Kestrel (Rate Limiting, Auth MFA, Routing)</div>
          </div>
          <div className="flow-arrow">➔</div>
          <div className="flow-node flow-integrations">
            <div className="flow-icon">🌐</div>
            <div className="flow-title">Connected Platforms</div>
            <div className="flow-services-pills">
              <span className="service-pill pill-db">🐘 Neon PostgreSQL</span>
              <span className="service-pill pill-pay">💳 Razorpay</span>
              <span className="service-pill pill-r2">☁️ Cloudflare R2</span>
              <span className="service-pill pill-email">✉️ Transactional Email</span>
              <span className="service-pill pill-wa">📱 WhatsApp Cloud</span>
            </div>
          </div>
        </div>
      </div>

      {/* Controls: Search & Category Filter */}
      <div className="platforms-controls-bar">
        <div className="platforms-filter-tabs">
          <button
            type="button"
            className={`platform-tab ${activeCategory === "all" ? "active" : ""}`}
            onClick={() => setActiveCategory("all")}
          >
            All Platforms ({categoryCounts.all})
          </button>
          <button
            type="button"
            className={`platform-tab ${activeCategory === "core" ? "active" : ""}`}
            onClick={() => setActiveCategory("core")}
          >
            Core & Database ({categoryCounts.core})
          </button>
          <button
            type="button"
            className={`platform-tab ${activeCategory === "payments" ? "active" : ""}`}
            onClick={() => setActiveCategory("payments")}
          >
            Payments & Finance ({categoryCounts.payments})
          </button>
          <button
            type="button"
            className={`platform-tab ${activeCategory === "storage" ? "active" : ""}`}
            onClick={() => setActiveCategory("storage")}
          >
            Media Storage ({categoryCounts.storage})
          </button>
          <button
            type="button"
            className={`platform-tab ${activeCategory === "auth" ? "active" : ""}`}
            onClick={() => setActiveCategory("auth")}
          >
            Auth & Security ({categoryCounts.auth})
          </button>
          <button
            type="button"
            className={`platform-tab ${activeCategory === "comms" ? "active" : ""}`}
            onClick={() => setActiveCategory("comms")}
          >
            Communications ({categoryCounts.comms})
          </button>
        </div>

        <div className="platforms-search-box">
          <input
            type="text"
            className="form-control"
            placeholder="Search platforms, entities, tech stacks..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button
              type="button"
              className="search-clear-btn"
              onClick={() => setSearchQuery("")}
            >
              ✕
            </button>
          )}
        </div>
      </div>

      {/* Platform Cards Grid */}
      <div className="platforms-cards-grid">
        {filteredPlatforms.map((platform) => {
          const isExpanded = expandedPlatform === platform.id;
          return (
            <div
              key={platform.id}
              className={`platform-card ${platform.status === "operational" ? "is-operational" : "is-warning"}`}
            >
              {/* Card Header */}
              <div className="platform-card-header">
                <div className="platform-brand-info">
                  <div className="platform-icon-bubble">{platform.icon}</div>
                  <div>
                    <div className="platform-badge-row">
                      <span className="platform-role-tag">{platform.badge}</span>
                      <span className={`platform-status-badge status-${platform.status}`}>
                        <span className="status-dot"></span>
                        {platform.statusLabel}
                      </span>
                    </div>
                    <h3 className="platform-name">{platform.name}</h3>
                    <p className="platform-provider">{platform.provider}</p>
                  </div>
                </div>
              </div>

              {/* Card Summary */}
              <p className="platform-card-desc">{platform.description}</p>

              {/* Technical Spec Row */}
              <div className="platform-specs-grid">
                <div className="spec-item">
                  <span className="spec-label">Tech Stack</span>
                  <span className="spec-val">{platform.techStack}</span>
                </div>
                <div className="spec-item">
                  <span className="spec-label">Endpoint</span>
                  <span className="spec-val code-font">{platform.endpoint}</span>
                </div>
                <div className="spec-item">
                  <span className="spec-label">Port / Protocol</span>
                  <span className="spec-val code-font">{platform.port}</span>
                </div>
                <div className="spec-item">
                  <span className="spec-label">Latency / Response</span>
                  <span className="spec-val text-brand">{platform.latency}</span>
                </div>
              </div>

              {/* Functional Responsibilities */}
              <div className="platform-section-block">
                <h4 className="platform-block-heading">What This Platform Does:</h4>
                <ul className="platform-tasks-list">
                  {platform.whatItDoes.map((task, idx) => (
                    <li key={idx}>
                      <span className="task-bullet">✓</span>
                      <span>{task}</span>
                    </li>
                  ))}
                </ul>
              </div>

              {/* Managed Data Entities */}
              <div className="platform-section-block">
                <h4 className="platform-block-heading">Managed Entities & Tables:</h4>
                <div className="entity-tags-cloud">
                  {platform.managedEntities.map((ent) => (
                    <span key={ent} className="entity-tag">
                      {ent}
                    </span>
                  ))}
                </div>
              </div>

              {/* Toggle Deep Dive Details */}
              <div className="platform-card-footer">
                <button
                  type="button"
                  className="btn btn-outline platform-expand-btn"
                  onClick={() => setExpandedPlatform(isExpanded ? null : platform.id)}
                >
                  {isExpanded ? "Hide Technical Details ▲" : "View Security & Data Flow ▼"}
                </button>
              </div>

              {/* Expanded Deep Dive Details */}
              {isExpanded && (
                <div className="platform-deep-dive-panel">
                  <div className="deep-dive-section">
                    <h5 className="deep-dive-title">🔒 Security & Compliance Protocols</h5>
                    <ul className="deep-dive-list">
                      {platform.securitySpecs.map((spec, i) => (
                        <li key={i}>{spec}</li>
                      ))}
                    </ul>
                  </div>

                  <div className="deep-dive-section">
                    <h5 className="deep-dive-title">🔄 End-to-End Data Flow Pipeline</h5>
                    <div className="deep-dive-dataflow code-font">
                      {platform.dataFlow}
                    </div>
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {filteredPlatforms.length === 0 && (
        <div className="platforms-empty-state">
          <div className="empty-icon">🔍</div>
          <h3>No platforms matched your filter</h3>
          <p>Try searching for a different keyword or select "All Platforms".</p>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              setActiveCategory("all");
              setSearchQuery("");
            }}
          >
            Reset Filters
          </button>
        </div>
      )}
    </div>
  );
}
