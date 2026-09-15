import React, { useState, useEffect } from "react";
import { useParams, Link } from "react-router-dom";
import { adminApi } from "../../services/adminApi";
import "./AdminStudentDossier.css";

function transformStudentIssue(issue, dossier, setActiveTab, handleCopyReminder, copyToClipboard) {
  const code = issue.code || "";
  const missing = dossier?.missingFields || [];

  if (code === "PROFILE_INCOMPLETE") {
    return {
      ...issue,
      mappedTitle: "Profile information is incomplete",
      mappedSeverityLabel: "Attention Required",
      mappedDescription: `The student has not provided: ${missing.length > 0 ? missing.join(", ") : "Date of Birth, City, or Emergency Contact"}.`,
      impact: "The student may experience delays during event check-ins or emergency notifications.",
      nextStep: "Ask the student to update their details in the Student Portal, or complete them under Personal Details.",
      actions: [
        {
          label: "Send Profile Reminder",
          variant: "primary",
          onClick: () =>
            handleCopyReminder(
              code,
              `Hi ${dossier?.fullName || "Student"}, please log in to the Ethos Student Portal to complete your profile details. Thank you!`
            ),
        },
        {
          label: "View Personal Details",
          variant: "secondary",
          onClick: () => setActiveTab("overview"),
        },
      ],
    };
  }

  if (code === "PACKAGE_EXPIRED") {
    return {
      ...issue,
      mappedTitle: "Class package has expired",
      mappedSeverityLabel: "Attention Required",
      mappedDescription: issue.description || "The student's previous dance package has passed its expiration date.",
      impact: "The student cannot reserve scheduled classes until their package is renewed.",
      nextStep: "Contact the student to confirm whether they would like to renew their package.",
      actions: [
        {
          label: "View Packages & Class Credits",
          variant: "primary",
          onClick: () => setActiveTab("packages"),
        },
        {
          label: "Send Renewal Reminder",
          variant: "secondary",
          onClick: () =>
            handleCopyReminder(
              code,
              `Hi ${dossier?.fullName || "Student"}, your Ethos dance class package has expired. Please log in to renew your package to continue booking classes!`
            ),
        },
      ],
    };
  }

  if (code === "NO_ACTIVE_PACKAGE") {
    return {
      ...issue,
      mappedTitle: "No active dance class package",
      mappedSeverityLabel: "Attention Required",
      mappedDescription: "This student does not currently hold an active class package.",
      impact: "The student can only attend drop-in workshops; they cannot reserve regular weekly dance classes.",
      nextStep: "Review package options with the student and assist them with enrollment.",
      actions: [
        {
          label: "View Packages & Class Credits",
          variant: "primary",
          onClick: () => setActiveTab("packages"),
        },
      ],
    };
  }

  if (code === "ZERO_CLASSES_REMAINING") {
    return {
      ...issue,
      mappedTitle: "All class credits have been used",
      mappedSeverityLabel: "Attention Required",
      mappedDescription: issue.description || "The student has used all available class credits on their active package.",
      impact: "The student cannot reserve additional classes without a package renewal or class top-up.",
      nextStep: "Suggest purchasing an add-on class credit or renewing the package.",
      actions: [
        {
          label: "View Packages & Class Credits",
          variant: "primary",
          onClick: () => setActiveTab("packages"),
        },
      ],
    };
  }

  if (code === "UNPAID_BOOKING") {
    return {
      ...issue,
      mappedTitle: "Workshop reservation pending payment",
      mappedSeverityLabel: "Payment Follow-up",
      mappedDescription: issue.description || "A workshop booking was initiated but payment was not confirmed.",
      impact: "A workshop seat may be held without payment confirmation.",
      nextStep: "Review payment status with the student or cancel the unconfirmed reservation to free the seat.",
      actions: [
        {
          label: "View Workshop Reservations",
          variant: "primary",
          onClick: () => setActiveTab("workshops"),
        },
        {
          label: "Check Payment History",
          variant: "secondary",
          onClick: () => setActiveTab("payments"),
        },
      ],
    };
  }

  if (code === "PAYMENT_GATEWAY_FAILURE") {
    return {
      ...issue,
      mappedTitle: "Payment transaction unsuccessful",
      mappedSeverityLabel: "Payment Issue",
      mappedDescription: issue.description || "An online payment transaction was rejected or failed at the payment gateway.",
      impact: "The student's account was not charged and the booking or package was not activated.",
      nextStep: "Check payment history and assist the student with retrying online checkout.",
      actions: [
        {
          label: "View Payment History",
          variant: "primary",
          onClick: () => setActiveTab("payments"),
        },
      ],
    };
  }

  if (code === "OTP_FAILURE") {
    return {
      ...issue,
      mappedTitle: "Student login verification difficulties",
      mappedSeverityLabel: "Login Assistance",
      mappedDescription: "Multiple failed verification code attempts were recorded in the last 24 hours.",
      impact: "The student may be locked out or having trouble receiving SMS verification codes.",
      nextStep: "Confirm the student's registered mobile number is correct and check SMS delivery.",
      actions: [
        {
          label: `Copy Phone: ${dossier?.phone || ""}`,
          variant: "primary",
          onClick: () => copyToClipboard(dossier?.phone, "phone"),
        },
      ],
    };
  }

  if (code === "AUTHORIZATION_DENIED") {
    return {
      ...issue,
      mappedTitle: "Account access notice",
      mappedSeverityLabel: "Security Notice",
      mappedDescription: issue.description || "Access requests to restricted features were blocked.",
      impact: "The student account attempted an unauthorized operation or has an irregular session.",
      nextStep: "Review account status and verify permissions.",
      actions: [
        {
          label: "Inspect Personal Details",
          variant: "secondary",
          onClick: () => setActiveTab("overview"),
        },
      ],
    };
  }

  // Fallback for any other issue
  return {
    ...issue,
    mappedTitle: issue.title || "Item Requiring Attention",
    mappedSeverityLabel: issue.severity === "CRITICAL" ? "High Priority" : "Attention Required",
    mappedDescription: issue.description || "An item requires operational review.",
    impact: "May impact student booking, payment, or class attendance.",
    nextStep: issue.recommendedAction || "Review account details and take appropriate operational action.",
    actions: [
      {
        label: "Inspect Personal Details",
        variant: "secondary",
        onClick: () => setActiveTab("overview"),
      },
    ],
  };
}

export default function AdminStudentDossier() {
  const { studentId } = useParams();
  const [dossier, setDossier] = useState(null);
  const [diagnostics, setDiagnostics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState("overview");

  // Status Action Modal
  const [statusModalOpen, setStatusModalOpen] = useState(false);
  const [targetStatus, setTargetStatus] = useState(false);
  const [statusReason, setStatusReason] = useState("");
  const [statusLoading, setStatusLoading] = useState(false);
  const [statusError, setStatusError] = useState(null);
  const [copyFeedback, setCopyFeedback] = useState(null);

  // Workshop Booking Filter & Inspect Drawer
  const [workshopSearch, setWorkshopSearch] = useState("");
  const [workshopStatusFilter, setWorkshopStatusFilter] = useState("ALL");
  const [selectedBooking, setSelectedBooking] = useState(null);

  const fetchDossierData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [dossierRes, diagRes] = await Promise.all([
        adminApi.getStudentById(studentId),
        adminApi.getStudentDiagnostics(studentId),
      ]);
      setDossier(dossierRes);
      setDiagnostics(diagRes);
    } catch (err) {
      setError(err.message || "Failed to load student dossier and diagnostics.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (studentId) {
      fetchDossierData();
    }
  }, [studentId]);

  const handleOpenStatusModal = (newStatus) => {
    setTargetStatus(newStatus);
    setStatusReason("");
    setStatusError(null);
    setStatusModalOpen(true);
  };

  const handleConfirmStatus = async () => {
    if (!statusReason.trim()) {
      setStatusError("A justification reason is mandatory.");
      return;
    }
    setStatusLoading(true);
    setStatusError(null);
    try {
      await adminApi.updateStudentStatus(studentId, targetStatus, statusReason.trim());
      setStatusModalOpen(false);
      fetchDossierData();
    } catch (err) {
      setStatusError(err.message || "Failed to update student account status.");
    } finally {
      setStatusLoading(false);
    }
  };

  const copyToClipboard = (text, key) => {
    if (!text) return;
    navigator.clipboard.writeText(text);
    setCopyFeedback(key || text);
    setTimeout(() => setCopyFeedback(null), 2000);
  };

  const handleCopyReminder = (issueCode, messageText) => {
    navigator.clipboard.writeText(messageText);
    setCopyFeedback(`reminder-${issueCode}`);
    setTimeout(() => setCopyFeedback(null), 2500);
  };

  const isTestDataWorkshop = (title) => {
    if (!title) return false;
    const lower = title.toLowerCase();
    return lower.includes("phase") || lower.includes("test") || lower.includes("tampering") || lower.includes("idor");
  };

  if (loading) {
    return <div className="dossier-loading">Loading student account and records...</div>;
  }

  if (error || !dossier) {
    return (
      <div className="dossier-error-view">
        <div className="alert alert-danger">{error || "Student record not found."}</div>
        <Link to="/admin_portal/students" className="btn btn-secondary mt-3">
          ← Back to Students Directory
        </Link>
      </div>
    );
  }

  // Aggregate all operational issues
  const rawIssues = [
    ...(diagnostics?.businessIssues || []),
    ...(diagnostics?.technicalFailures || []),
  ];

  const allIssues = rawIssues.map((issue) =>
    transformStudentIssue(issue, dossier, setActiveTab, handleCopyReminder, copyToClipboard)
  );
  const totalIssuesCount = allIssues.length;

  return (
    <div className="admin-student-dossier-view">
      {/* Top Header & Breadcrumbs */}
      <div className="dossier-breadcrumb">
        <Link to="/admin_portal/students" className="back-link">
          ← Back to Students Directory
        </Link>
      </div>

      <div className="dossier-header-bar">
        <div className="dossier-identity">
          <div className="avatar-placeholder">
            {dossier.profilePhotoUrl ? (
              <img src={dossier.profilePhotoUrl} alt={dossier.fullName} />
            ) : (
              <span>{dossier.fullName?.charAt(0) || "S"}</span>
            )}
          </div>
          <div>
            <h1 className="student-name">{dossier.fullName}</h1>
            <div className="student-meta">
              <span className="mono-code">{dossier.customerCode}</span>
              <span className="separator">•</span>
              <span>{dossier.phone}</span>
              <span className="separator">•</span>
              <span className="text-secondary">{dossier.email || "No Email on file"}</span>
            </div>
          </div>
        </div>

        <div className="dossier-header-actions">
          <span className={`status-pill ${dossier.isActive ? "status-active" : "status-inactive"}`}>
            {dossier.isActive ? "ACTIVE" : "SUSPENDED"}
          </span>

          {dossier.isActive ? (
            <button className="btn btn-sm btn-danger" onClick={() => handleOpenStatusModal(false)}>
              Suspend Account
            </button>
          ) : (
            <button className="btn btn-sm btn-success" onClick={() => handleOpenStatusModal(true)}>
              Reactivate Account
            </button>
          )}
        </div>
      </div>

      {/* Two-Level Information Architecture: Account Overview & Attention Items */}
      <div className="account-overview-card">
        <div className="overview-header-row">
          <div>
            <h2 className="overview-card-title">Student Account Overview</h2>
            <p className="overview-card-subtitle">
              Overall account status, key operational metrics, and items requiring attention.
            </p>
          </div>
          <div className="overview-health-badge-wrap">
            {totalIssuesCount === 0 ? (
              <span className="health-badge healthy">
                ✓ Account Health: No Issues Detected
              </span>
            ) : (
              <span className="health-badge attention">
                ⚠️ Items Requiring Attention ({totalIssuesCount})
              </span>
            )}
          </div>
        </div>

        {/* Operational Metric KPI Strip */}
        <div className="overview-kpis-strip">
          <div className="kpi-box">
            <span className="kpi-label">Account Status</span>
            <span className={`kpi-badge ${dossier.isActive ? "kpi-active" : "kpi-inactive"}`}>
              {dossier.isActive ? "Active" : "Suspended"}
            </span>
          </div>
          <div className="kpi-box">
            <span className="kpi-label">Profile Status</span>
            <span className={`kpi-badge ${dossier.profileCompleted ? "kpi-complete" : "kpi-incomplete"}`}>
              {dossier.profileCompleted ? "Complete" : `Incomplete (${dossier.missingFields?.length || 0})`}
            </span>
          </div>
          <div className="kpi-box">
            <span className="kpi-label">Total Bookings</span>
            <span className="kpi-number">{diagnostics?.totalBookingsCount ?? dossier.workshopBookings?.length ?? 0}</span>
          </div>
          <div className="kpi-box">
            <span className="kpi-label">Active Packages</span>
            <span className="kpi-number">{diagnostics?.activePackagesCount ?? (dossier.activePackage ? 1 : 0)}</span>
          </div>
          <div className="kpi-box">
            <span className="kpi-label">Attendance Rate</span>
            <span className="kpi-number">
              {diagnostics?.attendanceRate ?? 100}%
            </span>
            <span className="kpi-sub">
              ({diagnostics?.totalClassesAttended ?? 0} attended, {diagnostics?.totalClassesMissed ?? 0} missed)
            </span>
          </div>
          <div className={`kpi-box ${totalIssuesCount > 0 ? "attention" : ""}`}>
            <span className="kpi-label">Items Requiring Attention</span>
            <span className={`kpi-number ${totalIssuesCount > 0 ? "highlight-attn" : ""}`}>
              {totalIssuesCount}
            </span>
          </div>
        </div>

        {/* Level 1: Main View - Items Requiring Attention */}
        {totalIssuesCount > 0 ? (
          <div className="attention-required-section">
            <div className="attention-section-header">
              <h3>⚠️ Attention Required ({totalIssuesCount})</h3>
              <p>The following items need review or operational follow-up from the studio administrator.</p>
            </div>
            <div className="attention-items-list">
              {allIssues.map((issue, idx) => (
                <div key={idx} className={`attention-card sev-${(issue.severity || "warning").toLowerCase()}`}>
                  <div className="attention-card-header">
                    <div className="attention-title-col">
                      <span className="attention-icon">
                        {issue.severity === "CRITICAL" ? "🚨" : issue.severity === "WARNING" ? "⚠️" : "ℹ️"}
                      </span>
                      <h4>{issue.mappedTitle}</h4>
                    </div>
                    <span className={`attention-severity-badge ${(issue.severity || "warning").toLowerCase()}`}>
                      {issue.mappedSeverityLabel}
                    </span>
                  </div>

                  <p className="attention-description">{issue.mappedDescription}</p>

                  <div className="attention-explanation-grid">
                    <div className="explanation-block impact">
                      <strong className="block-title">Why this matters:</strong>
                      <p>{issue.impact}</p>
                    </div>
                    <div className="explanation-block next-step">
                      <strong className="block-title">Recommended next step:</strong>
                      <p>{issue.nextStep}</p>
                    </div>
                  </div>

                  {/* Direct Operational Action Buttons */}
                  <div className="attention-actions-bar">
                    {issue.actions?.map((act, actIdx) => (
                      <button
                        key={actIdx}
                        type="button"
                        className={`admin-action-btn ${act.variant || "secondary"}`}
                        onClick={act.onClick}
                      >
                        {act.label}
                      </button>
                    ))}
                    {copyFeedback === `reminder-${issue.code}` && (
                      <span className="reminder-copied-toast">✓ Reminder message copied to clipboard!</span>
                    )}
                    {copyFeedback === "phone" && (
                      <span className="reminder-copied-toast">✓ Phone number copied!</span>
                    )}
                  </div>

                  {/* Level 2: Advanced Technical Details (Expandable) */}
                  <details className="advanced-technical-details">
                    <summary className="advanced-summary">
                      <span className="summary-chevron">▸</span> Advanced Technical Details
                    </summary>
                    <div className="advanced-technical-content">
                      <div className="tech-meta-row">
                        <div><strong>Internal issue code:</strong> <code>{issue.code}</code></div>
                        <div><strong>System category:</strong> <code>{issue.category}</code></div>
                        <div><strong>Raw severity:</strong> <code>{issue.severity}</code></div>
                      </div>
                      <div className="tech-meta-row">
                        <div>
                          <strong>Support Audit Reference:</strong>{" "}
                          <code className="mono-code">{issue.traceId || diagnostics?.traceId || "N/A"}</code>
                          {(issue.traceId || diagnostics?.traceId) && (
                            <button
                              type="button"
                              className="btn-copy-mini"
                              onClick={() => copyToClipboard(issue.traceId || diagnostics?.traceId, `trace-${idx}`)}
                            >
                              {copyFeedback === `trace-${idx}` ? "✓ Copied" : "Copy Reference"}
                            </button>
                          )}
                        </div>
                        <div>
                          <strong>Detected at:</strong>{" "}
                          {issue.detectedAt || diagnostics?.generatedAt
                            ? new Date(issue.detectedAt || diagnostics?.generatedAt).toLocaleString()
                            : "Recent"}
                        </div>
                      </div>
                      {issue.evidence && (
                        <div className="tech-evidence-block">
                          <strong>Structured Evidence:</strong>
                          <pre className="evidence-json">{JSON.stringify(issue.evidence, null, 2)}</pre>
                        </div>
                      )}
                    </div>
                  </details>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <div className="account-healthy-banner">
            <div className="healthy-icon">✓</div>
            <div className="healthy-text">
              <h4>Account in Good Standing</h4>
              <p>No operational issues, payment discrepancies, or missing profile details detected for this student.</p>
            </div>
          </div>
        )}
      </div>

      {/* Tabs Navigation */}
      <div className="dossier-tabs-nav">
        <button
          className={`tab-btn ${activeTab === "overview" ? "active" : ""}`}
          onClick={() => setActiveTab("overview")}
        >
          Personal Details
        </button>
        <button
          className={`tab-btn ${activeTab === "packages" ? "active" : ""}`}
          onClick={() => setActiveTab("packages")}
        >
          Packages & Class Credits
          <span className="tab-count">{dossier.packageHistory?.length || 0}</span>
        </button>
        <button
          className={`tab-btn ${activeTab === "classes" ? "active" : ""}`}
          onClick={() => setActiveTab("classes")}
        >
          Classes & Attendance
          <span className="tab-count">{dossier.classEnrollments?.length || 0}</span>
        </button>
        <button
          className={`tab-btn ${activeTab === "attendance" ? "active" : ""}`}
          onClick={() => setActiveTab("attendance")}
        >
          Attendance History
          <span className="tab-count">{dossier.attendanceRecords?.length || 0}</span>
        </button>
        <button
          className={`tab-btn ${activeTab === "workshops" ? "active" : ""}`}
          onClick={() => setActiveTab("workshops")}
        >
          Workshop Reservations
          <span className="tab-count">{dossier.workshopBookings?.length || 0}</span>
        </button>
        <button
          className={`tab-btn ${activeTab === "payments" ? "active" : ""}`}
          onClick={() => setActiveTab("payments")}
        >
          Payment History
          <span className="tab-count">{dossier.paymentTransactions?.length || 0}</span>
        </button>
        <button
          className={`tab-btn ${activeTab === "feedback" ? "active" : ""}`}
          onClick={() => setActiveTab("feedback")}
        >
          Student Feedback
          <span className="tab-count">{dossier.submittedFeedback?.length || 0}</span>
        </button>
      </div>

      {/* Tab 1: Personal Details */}
      {activeTab === "overview" && (
        <div className="tab-pane">
          <h3 className="section-title">Personal Details & Emergency Contacts</h3>

          {dossier.missingFields && dossier.missingFields.length > 0 && (
            <div className="partner-badge-banner warning mb-4">
              ⚠️ <strong>Profile Incomplete</strong>: This student has not yet provided required details:{" "}
              {dossier.missingFields.join(", ")}.
            </div>
          )}

          <div className="profile-grid">
            <div>
              <span className="label">Date of Birth</span>
              <span className="val">{dossier.dateOfBirth ? new Date(dossier.dateOfBirth).toLocaleDateString() : "Not specified"}</span>
            </div>
            <div>
              <span className="label">Gender</span>
              <span className="val">{dossier.gender || "Not specified"}</span>
            </div>
            <div>
              <span className="label">City</span>
              <span className="val">{dossier.city || "Not specified"}</span>
            </div>
            <div>
              <span className="label">Profile Setup Status</span>
              <span className="val">
                {dossier.profileCompleted ? (
                  <span className="badge-profile-completed">✓ Completed</span>
                ) : (
                  <span className="badge-profile-incomplete">Incomplete ({dossier.missingFields?.length || 0} missing)</span>
                )}
              </span>
            </div>
            <div>
              <span className="label">Emergency Contact Name</span>
              <span className="val">{dossier.emergencyContactName || "Not specified"}</span>
            </div>
            <div>
              <span className="label">Emergency Contact Phone</span>
              <span className="val">{dossier.emergencyContactPhone || "Not specified"}</span>
            </div>
          </div>

          {dossier.bio && (
            <div className="mt-4">
              <span className="label">Student Bio</span>
              <p className="bio-text">{dossier.bio}</p>
            </div>
          )}
        </div>
      )}

      {/* Tab 2: Packages & Class Credits */}
      {activeTab === "packages" && (
        <div className="tab-pane">
          <h3 className="section-title">Dance Packages & Class Credits</h3>
          {dossier.activePackage ? (
            <div className="active-package-banner">
              <div className="active-pkg-header">
                <div>
                  <span className="badge-profile-completed">ACTIVE PACKAGE</span>
                  <h4>{dossier.activePackage.packageName}</h4>
                </div>
                <div className="expiry-box">
                  Expires: {new Date(dossier.activePackage.expiryDate).toLocaleDateString()}
                </div>
              </div>

              <div className="quota-bar-container">
                <div className="quota-labels">
                  <span>Classes Used: {dossier.activePackage.classesUsed} / {dossier.activePackage.classesAllowed ?? "Unlimited"}</span>
                  <span>
                    {dossier.activePackage.classesAllowed
                      ? `${Math.round((dossier.activePackage.classesUsed / dossier.activePackage.classesAllowed) * 100)}% Used`
                      : "Unlimited"}
                  </span>
                </div>
                <div className="progress-bar">
                  <div
                    className="progress-fill"
                    style={{
                      width: dossier.activePackage.classesAllowed
                        ? `${Math.min(100, (dossier.activePackage.classesUsed / dossier.activePackage.classesAllowed) * 100)}%`
                        : "0%",
                    }}
                  />
                </div>
              </div>
            </div>
          ) : (
            <div className="empty-state mb-4">No active dance package currently assigned to this student.</div>
          )}

          <h4 className="mt-4 mb-2 font-semibold text-dark">Package Purchase History</h4>
          {dossier.packageHistory?.length === 0 ? (
            <div className="empty-state">No package history recorded for this student.</div>
          ) : (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Package Name</th>
                  <th>Start Date</th>
                  <th>Expiry Date</th>
                  <th>Classes Allowed</th>
                  <th>Classes Used</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {dossier.packageHistory?.map((pkg) => (
                  <tr key={pkg.id}>
                    <td className="font-semibold">{pkg.packageName}</td>
                    <td>{new Date(pkg.startDate).toLocaleDateString()}</td>
                    <td>{new Date(pkg.expiryDate).toLocaleDateString()}</td>
                    <td>{pkg.classesAllowed ?? "Unlimited"}</td>
                    <td>{pkg.classesUsed}</td>
                    <td>
                      <span className={`status-pill ${pkg.isActive ? "status-active" : "status-inactive"}`}>
                        {pkg.isActive ? "ACTIVE" : "EXPIRED"}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Tab 3: Class Enrollments */}
      {activeTab === "classes" && (
        <div className="tab-pane">
          <h3 className="section-title">Class Enrollments</h3>
          {dossier.classEnrollments?.length === 0 ? (
            <div className="empty-state">No class enrollments found. This student has not yet enrolled in any regular dance classes.</div>
          ) : (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Class Name</th>
                  <th>Style</th>
                  <th>Enrollment Date</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {dossier.classEnrollments.map((e) => (
                  <tr key={e.id}>
                    <td className="font-semibold">{e.danceClassName}</td>
                    <td>{e.danceStyle}</td>
                    <td>{new Date(e.enrollmentDate).toLocaleDateString()}</td>
                    <td>
                      <span className="status-pill status-active">{e.status?.toString() || "Enrolled"}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Tab 4: Attendance History */}
      {activeTab === "attendance" && (
        <div className="tab-pane">
          <h3 className="section-title">Attendance History</h3>
          {dossier.attendanceRecords?.length === 0 ? (
            <div className="empty-state">No attendance records yet. This student has not attended or been marked present for any scheduled classes.</div>
          ) : (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Class</th>
                  <th>Dance Style</th>
                  <th>Session Date</th>
                  <th>Status</th>
                  <th>Marked At</th>
                  <th>Notes</th>
                </tr>
              </thead>
              <tbody>
                {dossier.attendanceRecords.map((att) => (
                  <tr key={att.id}>
                    <td className="font-semibold">{att.className}</td>
                    <td>{att.danceStyle}</td>
                    <td>{new Date(att.sessionDate).toLocaleDateString()}</td>
                    <td>
                      <span className={`status-pill ${att.status === "Present" ? "status-active" : "status-inactive"}`}>
                        {att.status}
                      </span>
                    </td>
                    <td>{new Date(att.markedAt).toLocaleString()}</td>
                    <td className="text-secondary">{att.notes || "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Tab 5: Workshops & Bookings */}
      {activeTab === "workshops" && (() => {
        const bookings = dossier.workshopBookings || [];
        const filteredBookings = bookings.filter((wb) => {
          const matchSearch =
            !workshopSearch.trim() ||
            wb.workshopTitle?.toLowerCase().includes(workshopSearch.toLowerCase()) ||
            wb.trainerName?.toLowerCase().includes(workshopSearch.toLowerCase()) ||
            wb.id?.toLowerCase().includes(workshopSearch.toLowerCase());

          const matchStatus =
            workshopStatusFilter === "ALL" ||
            wb.status?.toLowerCase() === workshopStatusFilter.toLowerCase() ||
            wb.attendanceStatus?.toLowerCase() === workshopStatusFilter.toLowerCase();

          return matchSearch && matchStatus;
        });

        return (
          <div className="tab-pane">
            <div className="tab-section-header">
              <div className="tab-section-title-wrap">
                <h3>Workshop Reservations</h3>
                <p>{bookings.length} total workshop reservations associated with this student.</p>
              </div>

              <div className="tab-filter-bar">
                <input
                  type="text"
                  className="input-search-small"
                  placeholder="Search workshop, trainer, or ID..."
                  value={workshopSearch}
                  onChange={(e) => setWorkshopSearch(e.target.value)}
                />
                <select
                  className="select-filter-small"
                  value={workshopStatusFilter}
                  onChange={(e) => setWorkshopStatusFilter(e.target.value)}
                >
                  <option value="ALL">All Statuses</option>
                  <option value="Confirmed">Confirmed</option>
                  <option value="Attended">Attended</option>
                  <option value="Pending Payment">Pending Payment</option>
                  <option value="Cancelled">Cancelled</option>
                  <option value="No Show">No Show</option>
                </select>
              </div>
            </div>

            {bookings.length === 0 ? (
              <div className="empty-state">
                No workshop bookings recorded. This student has not registered for any masterclasses or intensive workshops.
              </div>
            ) : filteredBookings.length === 0 ? (
              <div className="empty-state">No workshop bookings match your search filters.</div>
            ) : (
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Workshop Title</th>
                    <th>Trainer</th>
                    <th>Workshop Date</th>
                    <th>Amount</th>
                    <th>Payment</th>
                    <th>Booking Status</th>
                    <th>Attendance</th>
                    <th>Feedback</th>
                    <th>Booked At</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredBookings.map((wb) => {
                    const isTest = isTestDataWorkshop(wb.workshopTitle);
                    const bookingStatusClass =
                      wb.status === "Confirmed"
                        ? "status-confirmed"
                        : wb.status === "Attended"
                        ? "status-attended"
                        : wb.status === "Pending Payment"
                        ? "status-pending"
                        : "status-cancelled";

                    const attendanceClass =
                      wb.attendanceStatus === "Attended"
                        ? "attended"
                        : wb.attendanceStatus === "Scheduled"
                        ? "scheduled"
                        : "not-attended";

                    return (
                      <tr key={wb.id}>
                        <td>
                          <div className="font-semibold text-dark">
                            {wb.workshopTitle}
                            {isTest && <span className="badge-test-data">TEST DATA</span>}
                          </div>
                          <span className="mono-code text-secondary" style={{ fontSize: "11px" }}>
                            {wb.id.substring(0, 8)}...
                          </span>
                        </td>
                        <td>{wb.trainerName || "Staff Trainer"}</td>
                        <td>{new Date(wb.workshopDate).toLocaleDateString()}</td>
                        <td className="font-semibold">₹{wb.amount}</td>
                        <td>
                          <span className={`status-pill ${wb.paymentStatus === "Paid" ? "status-active" : "status-inactive"}`}>
                            {wb.paymentStatus || "Pending"}
                          </span>
                        </td>
                        <td>
                          <span className={`badge-booking-status ${bookingStatusClass}`}>
                            {wb.status}
                          </span>
                        </td>
                        <td>
                          <span className={`badge-attendance ${attendanceClass}`}>
                            {wb.attendanceStatus}
                          </span>
                        </td>
                        <td>
                          {wb.attendanceStatus !== "Attended" ? (
                            wb.status === "Cancelled" || wb.status === "Payment Pending" || wb.feedbackStatus === "Ineligible" ? (
                              <span className="badge-feedback-tag ineligible">—</span>
                            ) : wb.feedbackStatus === "Available after workshop" ? (
                              <span className="badge-feedback-tag scheduled" title="Feedback will be available after session completion and attendance check-in.">
                                Available after workshop
                              </span>
                            ) : (
                              <span className="badge-feedback-tag not-available" title="Feedback is available only after attendance is recorded as Attended.">
                                Not Available
                              </span>
                            )
                          ) : wb.feedbackStatus === "Submitted" && wb.feedbackRating != null ? (
                            <span className="badge-feedback-tag submitted">
                              ★ {wb.feedbackRating}.0
                            </span>
                          ) : (
                            <span className="badge-feedback-tag pending">
                              Pending
                            </span>
                          )}
                        </td>
                        <td className="text-secondary">{new Date(wb.bookedAt).toLocaleDateString()}</td>
                        <td>
                          <button
                            className="btn-inspect-booking"
                            onClick={() => setSelectedBooking(wb)}
                          >
                            Inspect
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        );
      })()}

      {/* Tab 6: Payment History */}
      {activeTab === "payments" && (
        <div className="tab-pane">
          <h3 className="section-title">Payment History</h3>
          {dossier.paymentTransactions?.length === 0 ? (
            <div className="empty-state">No payment transactions recorded. No invoices, gateway checkouts, or receipts exist for this account.</div>
          ) : (
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Transaction ID</th>
                  <th>Purpose</th>
                  <th>Amount</th>
                  <th>Razorpay Order ID</th>
                  <th>Payment ID</th>
                  <th>Status</th>
                  <th>Date</th>
                </tr>
              </thead>
              <tbody>
                {dossier.paymentTransactions.map((tx) => (
                  <tr key={tx.id}>
                    <td className="mono-code">{tx.id.substring(0, 8)}...</td>
                    <td>{tx.purpose?.toString() || "Payment"}</td>
                    <td className="font-semibold">₹{tx.amount}</td>
                    <td className="mono-code">{tx.razorpayOrderId || "—"}</td>
                    <td className="mono-code">{tx.razorpayPaymentId || "—"}</td>
                    <td>
                      <span className={`status-pill ${tx.status === 1 || tx.status === "Paid" ? "status-active" : "status-inactive"}`}>
                        {tx.status === 1 || tx.status === "Paid" ? "PAID" : "FAILED/PENDING"}
                      </span>
                    </td>
                    <td className="text-secondary">{new Date(tx.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Tab 7: Student Feedback */}
      {activeTab === "feedback" && (
        <div className="tab-pane">
          <h3 className="section-title">Student Feedback</h3>
          {dossier.submittedFeedback?.length === 0 ? (
            <div className="empty-state">No workshop or class reviews submitted by this student yet.</div>
          ) : (
            <div className="feedback-cards-grid">
              {dossier.submittedFeedback.map((fb) => (
                <div key={fb.id} className="feedback-card">
                  <div className="feedback-header">
                    <span className="font-semibold">{fb.workshopTitle}</span>
                    <span className="rating-pill">⭐ {fb.rating} / 5</span>
                  </div>
                  {fb.comment && <p className="feedback-comment">"{fb.comment}"</p>}
                  <span className="feedback-date">{new Date(fb.submittedAt).toLocaleDateString()}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Status Modal */}
      {statusModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-box">
            <h3>{targetStatus ? "Reactivate Student Account" : "Suspend Student Account"}</h3>
            <p className="modal-desc">
              Provide an operational reason for changing this student's account status. This action routes through the centralized user management service and is logged to immutable audit records.
            </p>

            <div className="form-group">
              <label className="form-label">Justification Reason (Mandatory):</label>
              <textarea
                className="textarea-field"
                rows={3}
                placeholder="e.g., Requested by student, temporary membership hold, payment delinquency..."
                value={statusReason}
                onChange={(e) => setStatusReason(e.target.value)}
              />
            </div>

            {statusError && <div className="alert alert-danger mb-3">{statusError}</div>}

            <div className="modal-actions">
              <button
                className="btn btn-secondary"
                onClick={() => setStatusModalOpen(false)}
                disabled={statusLoading}
              >
                Cancel
              </button>
              <button
                className={`btn ${targetStatus ? "btn-success" : "btn-danger"}`}
                onClick={handleConfirmStatus}
                disabled={statusLoading}
              >
                {statusLoading ? "Submitting..." : targetStatus ? "Reactivate Student" : "Suspend Student"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Workshop Booking Inspection Drawer */}
      {selectedBooking && (
        <div className="drawer-overlay" onClick={() => setSelectedBooking(null)}>
          <div className="drawer-content" onClick={(e) => e.stopPropagation()}>
            <div className="drawer-header">
              <div>
                <h2>Workshop Booking Details</h2>
                <div className="drawer-subtitle">
                  <span>Booking ID:</span>
                  <span className="mono-code">{selectedBooking.id}</span>
                  <button
                    className="btn-copy-mini"
                    onClick={() => copyToClipboard(selectedBooking.id, "bookingId")}
                    title="Copy Booking ID"
                  >
                    {copyFeedback === "bookingId" ? "✓ Copied" : "Copy"}
                  </button>
                </div>
              </div>
              <button
                className="drawer-close-btn"
                onClick={() => setSelectedBooking(null)}
                aria-label="Close drawer"
              >
                ✕
              </button>
            </div>

            <div className="drawer-body">
              {isTestDataWorkshop(selectedBooking.workshopTitle) && (
                <div className="drawer-test-warning">
                  <span style={{ fontSize: "20px" }}>⚠️</span>
                  <div>
                    <strong>Test Environment Artifact</strong>
                    <p>This booking record was generated during automated E2E test runs or privacy verification routines.</p>
                  </div>
                </div>
              )}

              {/* Workshop Information */}
              <div className="drawer-section">
                <div className="drawer-section-title">Workshop Information</div>
                <div className="drawer-grid">
                  <div className="grid-item" style={{ gridColumn: "1 / -1" }}>
                    <span className="grid-label">Workshop Title</span>
                    <span className="grid-val font-semibold text-dark">
                      {selectedBooking.workshopTitle}
                      {isTestDataWorkshop(selectedBooking.workshopTitle) && (
                        <span className="badge-test-data">TEST DATA</span>
                      )}
                    </span>
                  </div>
                  <div className="grid-item">
                    <span className="grid-label">Instructor / Trainer</span>
                    <span className="grid-val">{selectedBooking.trainerName || "Staff Trainer"}</span>
                  </div>
                  <div className="grid-item">
                    <span className="grid-label">Dance Style</span>
                    <span className="grid-val">{selectedBooking.danceStyle || "General"}</span>
                  </div>
                  <div className="grid-item">
                    <span className="grid-label">Session Date</span>
                    <span className="grid-val">
                      {selectedBooking.workshopDate
                        ? new Date(selectedBooking.workshopDate).toLocaleDateString(undefined, {
                            weekday: "short",
                            year: "numeric",
                            month: "short",
                            day: "numeric",
                          })
                        : "—"}
                    </span>
                  </div>
                  <div className="grid-item">
                    <span className="grid-label">Timing</span>
                    <span className="grid-val">
                      {selectedBooking.startTime
                        ? `${String(selectedBooking.startTime).substring(0, 5)} - ${
                            selectedBooking.endTime ? String(selectedBooking.endTime).substring(0, 5) : ""
                          }`
                        : "Schedule TBA"}
                    </span>
                  </div>
                  <div className="grid-item" style={{ gridColumn: "1 / -1" }}>
                    <span className="grid-label">Venue / Studio</span>
                    <span className="grid-val">{selectedBooking.venue || "Ethos Main Studio"}</span>
                  </div>
                </div>
              </div>

              {/* Operational & Booking Status */}
              <div className="drawer-section">
                <div className="drawer-section-title">Booking & Attendance Status</div>
                <div className="drawer-grid">
                  <div className="grid-item">
                    <span className="grid-label">Booking Status</span>
                    <div style={{ marginTop: "4px" }}>
                      <span
                        className={`badge-booking-status ${
                          selectedBooking.status === "Confirmed"
                            ? "status-confirmed"
                            : selectedBooking.status === "Attended"
                            ? "status-attended"
                            : selectedBooking.status === "Pending Payment"
                            ? "status-pending"
                            : "status-cancelled"
                        }`}
                      >
                        {selectedBooking.status}
                      </span>
                    </div>
                  </div>
                  <div className="grid-item">
                    <span className="grid-label">Attendance Verification</span>
                    <div style={{ marginTop: "4px" }}>
                      <span
                        className={`badge-attendance ${
                          selectedBooking.attendanceStatus === "Attended"
                            ? "attended"
                            : selectedBooking.attendanceStatus === "Scheduled"
                            ? "scheduled"
                            : "not-attended"
                        }`}
                      >
                        {selectedBooking.attendanceStatus}
                      </span>
                    </div>
                  </div>
                  <div className="grid-item">
                    <span className="grid-label">Booked At</span>
                    <span className="grid-val">
                      {selectedBooking.bookedAt ? new Date(selectedBooking.bookedAt).toLocaleString() : "—"}
                    </span>
                  </div>
                  <div className="grid-item">
                    <span className="grid-label">Attendance Status</span>
                    <div style={{ marginTop: "4px" }}>
                      <span className={`badge-attendance ${
                        selectedBooking.statusCode === 4 || selectedBooking.status === "Attended"
                          ? "badge-attended"
                          : selectedBooking.statusCode === 5 || selectedBooking.status === "NoShow"
                          ? "badge-noshow"
                          : selectedBooking.statusCode === 3 || selectedBooking.status === "Cancelled"
                          ? "badge-cancelled"
                          : "badge-confirmed"
                      }`}>
                        {selectedBooking.statusCode === 4 || selectedBooking.status === "Attended"
                          ? "✓ Attended"
                          : selectedBooking.statusCode === 5 || selectedBooking.status === "NoShow"
                          ? "✕ Absent (No-Show)"
                          : selectedBooking.statusCode === 3 || selectedBooking.status === "Cancelled"
                          ? "Cancelled"
                          : "Booked / Confirmed"}
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Feedback Status & Token */}
              <div className="drawer-section">
                <div className="drawer-section-title">Workshop Feedback & Token</div>
                <div className="drawer-grid">
                  <div className="grid-item">
                    <span className="grid-label">Feedback Status</span>
                    <div style={{ marginTop: "4px" }}>
                      <span
                        className={`badge-feedback-tag ${
                          selectedBooking.attendanceStatus !== "Attended"
                            ? selectedBooking.feedbackStatus === "Available after workshop"
                              ? "scheduled"
                              : selectedBooking.feedbackStatus === "Not Available"
                              ? "not-available"
                              : "ineligible"
                            : selectedBooking.feedbackStatus === "Submitted"
                            ? "submitted"
                            : "pending"
                        }`}
                      >
                        {selectedBooking.feedbackStatus || "Pending"}
                      </span>
                    </div>
                  </div>
                  <div className="grid-item">
                    <span className="grid-label">Review Rating</span>
                    <span className="grid-val" style={{ color: (selectedBooking.attendanceStatus === "Attended" && selectedBooking.feedbackRating) ? "#d97706" : "inherit" }}>
                      {selectedBooking.attendanceStatus === "Attended" && selectedBooking.feedbackRating ? `★ ${selectedBooking.feedbackRating} / 5` : "Not submitted"}
                    </span>
                  </div>
                  {selectedBooking.feedbackToken && (
                    <div className="grid-item" style={{ gridColumn: "1 / -1" }}>
                      <span className="grid-label">Single-Use Feedback Token ID</span>
                      <div className="copyable-row">
                        <span className="mono-code">{selectedBooking.feedbackToken}</span>
                        <button
                          className="btn-copy-mini"
                          onClick={() => copyToClipboard(selectedBooking.feedbackToken, "fbTok")}
                        >
                          {copyFeedback === "fbTok" ? "✓ Copied" : "Copy"}
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              </div>

              {/* Financial & Payment Details */}
              <div className="drawer-section">
                <div className="drawer-section-title">Financial & Payment Details</div>
                <div className="drawer-grid">
                  <div className="grid-item">
                    <span className="grid-label">Booking Fee</span>
                    <span className="grid-val font-semibold" style={{ fontSize: "16px", color: "#0f172a" }}>
                      ₹{selectedBooking.amount}
                    </span>
                  </div>
                  <div className="grid-item">
                    <span className="grid-label">Payment Status</span>
                    <div style={{ marginTop: "4px" }}>
                      <span
                        className={`status-pill ${
                          selectedBooking.paymentStatus === "Paid" ? "status-active" : "status-inactive"
                        }`}
                      >
                        {selectedBooking.paymentStatus || "Pending"}
                      </span>
                    </div>
                  </div>
                  <div className="grid-item" style={{ gridColumn: "1 / -1" }}>
                    <span className="grid-label">Razorpay Order ID</span>
                    <div className="copyable-row">
                      <span className="mono-code">{selectedBooking.razorpayOrderId || "N/A (Direct / Test)"}</span>
                      {selectedBooking.razorpayOrderId && (
                        <button
                          className="btn-copy-mini"
                          onClick={() => copyToClipboard(selectedBooking.razorpayOrderId, "orderId")}
                        >
                          {copyFeedback === "orderId" ? "✓ Copied" : "Copy"}
                        </button>
                      )}
                    </div>
                  </div>
                  <div className="grid-item" style={{ gridColumn: "1 / -1" }}>
                    <span className="grid-label">Razorpay Payment ID</span>
                    <div className="copyable-row">
                      <span className="mono-code">{selectedBooking.razorpayPaymentId || "N/A (Direct / Test)"}</span>
                      {selectedBooking.razorpayPaymentId && (
                        <button
                          className="btn-copy-mini"
                          onClick={() => copyToClipboard(selectedBooking.razorpayPaymentId, "payId")}
                        >
                          {copyFeedback === "payId" ? "✓ Copied" : "Copy"}
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              </div>

              {/* Trace & Audit Identifiers */}
              <div className="drawer-section">
                <div className="drawer-section-title">Audit Identifiers</div>
                <div className="drawer-grid">
                  <div className="grid-item" style={{ gridColumn: "1 / -1" }}>
                    <span className="grid-label">Workshop ID</span>
                    <div className="copyable-row">
                      <span className="mono-code">{selectedBooking.workshopId}</span>
                      <button
                        className="btn-copy-mini"
                        onClick={() => copyToClipboard(selectedBooking.workshopId, "wId")}
                      >
                        {copyFeedback === "wId" ? "✓ Copied" : "Copy"}
                      </button>
                    </div>
                  </div>
                  <div className="grid-item" style={{ gridColumn: "1 / -1" }}>
                    <span className="grid-label">Student ID</span>
                    <div className="copyable-row">
                      <span className="mono-code">{studentId}</span>
                      <button
                        className="btn-copy-mini"
                        onClick={() => copyToClipboard(studentId, "sId")}
                      >
                        {copyFeedback === "sId" ? "✓ Copied" : "Copy"}
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

