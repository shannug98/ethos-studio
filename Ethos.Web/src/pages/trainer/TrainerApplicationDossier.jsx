import React, { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { trainerApi } from "../../services/trainerApi";
import { paymentApi } from "../../services/paymentApi";
import LoadingState from "../../components/trainer/LoadingState";
import ErrorState from "../../components/trainer/ErrorState";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import "./TrainerApplication.css";

function formatDate(value) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleDateString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatFileSize(bytes) {
  if (!bytes) return "—";
  const mb = bytes / (1024 * 1024);
  if (mb >= 1) return `${mb.toFixed(1)} MB`;
  const kb = bytes / 1024;
  return `${kb.toFixed(0)} KB`;
}

function getStatusBadge(status) {
  const s = String(status || "").toLowerCase();
  switch (s) {
    case "approved":
      return { label: "APPROVED", class: "approved" };
    case "underreview":
      return { label: "UNDER REVIEW", class: "pending" };
    case "paymentverified":
      return { label: "PAYMENT VERIFIED", class: "pending" };
    case "paymentpending":
      return { label: "PAYMENT PENDING", class: "pending" };
    case "submitted":
      return { label: "SUBMITTED", class: "pending" };
    case "changesrequested":
      return { label: "CHANGES REQUESTED", class: "rejected" };
    case "rejected":
      return { label: "REJECTED", class: "rejected" };
    case "cancelled":
      return { label: "CANCELLED", class: "rejected" };
    case "draft":
    default:
      return { label: "DRAFT", class: "draft" };
  }
}

export default function TrainerApplicationDossier() {
  const mountedRef = useRef(true);

  const [application, setApplication] = useState(null);
  const [documents, setDocuments] = useState([]);
  const [paymentDetails, setPaymentDetails] = useState(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadDossier = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const [appRes] = await Promise.all([
        trainerApi.getApplication(),
      ]);

      if (!mountedRef.current) return;

      const appData = appRes?.data ?? appRes;

      setApplication(appData);
      setDocuments([]);

      if (appData?.paymentTransactionId) {
        try {
          const payRes = await paymentApi.getPayment(appData.paymentTransactionId);
          if (mountedRef.current) {
            setPaymentDetails(payRes?.data ?? payRes);
          }
        } catch {
          if (mountedRef.current) {
            setPaymentDetails(null);
          }
        }
      }
    } catch (err) {
      if (!mountedRef.current) return;
      console.error("Failed to load application dossier:", err);
      setError(
        getApiErrorMessage(err, "Unable to load your application record right now.")
      );
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    loadDossier();
    return () => {
      mountedRef.current = false;
    };
  }, [loadDossier]);

  if (loading) {
    return <LoadingState label="Loading your application dossier..." />;
  }

  if (error || !application) {
    return (
      <ErrorState
        title="Application record unavailable"
        description={error || "No application record was found for your account."}
        action={
          <button
            type="button"
            className="trainer-primary-btn"
            onClick={loadDossier}
          >
            RETRY
          </button>
        }
      />
    );
  }

  const badge = getStatusBadge(application.status);

  return (
    <main className="trainer-dossier-page">
      <div className="dossier-shell">
        {/* TOP NOTICE */}
        <div className="dossier-notice-banner">
          <div>
            <strong>HISTORICAL ONBOARDING DOSSIER</strong>
            <span>
              This is your official trainer application record and historical approval audit log.
            </span>
          </div>

          <Link
            to="/trainer/profile"
            className="dossier-secondary-btn"
          >
            MANAGE CURRENT PROFILE →
          </Link>
        </div>

        {/* HEADER HERO */}
        <section className="dossier-hero">
          <div className="dossier-hero-main">
            <div className="dossier-hero-topline">
              <span className="eyebrow">TRAINER DOSSIER / ONBOARDING</span>
              <span className={`dossier-status-tag ${badge.class}`}>
                ● {badge.label}
              </span>
            </div>

            <h1>{application.fullName || "Ethos Trainer"}</h1>
            <p className="dossier-tier-title">
              {application.tier || "Ethos Trainer Pathway"}
            </p>

            <div className="dossier-hero-meta">
              <div>
                <small>APPLICATION ID</small>
                <strong>{application.id}</strong>
              </div>

              <div>
                <small>PROFILE ID</small>
                <strong>{application.trainerProfileId}</strong>
              </div>
            </div>
          </div>

          <div className="dossier-hero-mark">
            <span>ETHOS</span>
            <strong>{application.status}</strong>
            <small>LIFECYCLE STATUS</small>
          </div>
        </section>

        {/* TIMELINE JOURNEY */}
        <section className="dossier-section">
          <div className="dossier-section-heading">
            <span>01</span>
            <div>
              <small>APPLICATION JOURNEY</small>
              <h2>Onboarding Timeline</h2>
            </div>
          </div>

          <div className="dossier-timeline">
            <div className={`timeline-step ${application.submittedAt ? "completed" : "pending"}`}>
              <div className="step-marker" />
              <div className="step-info">
                <small>STAGE 01</small>
                <strong>Application Submitted</strong>
                <span>{formatDate(application.submittedAt)}</span>
              </div>
            </div>

            <div className={`timeline-step ${application.paymentVerifiedAt ? "completed" : "pending"}`}>
              <div className="step-marker" />
              <div className="step-info">
                <small>STAGE 02</small>
                <strong>Payment Verified</strong>
                <span>{formatDate(application.paymentVerifiedAt)}</span>
              </div>
            </div>

            <div className={`timeline-step ${application.reviewedAt ? "completed" : "pending"}`}>
              <div className="step-marker" />
              <div className="step-info">
                <small>STAGE 03</small>
                <strong>Admin Decision & Review</strong>
                <span>{formatDate(application.reviewedAt)}</span>
              </div>
            </div>
          </div>
        </section>

        {/* ONBOARDING DETAILS */}
        <section className="dossier-section">
          <div className="dossier-section-heading">
            <span>02</span>
            <div>
              <small>APPLICANT RECORD</small>
              <h2>Submitted Personal Identity</h2>
            </div>
          </div>

          <div className="dossier-grid">
            <div>
              <small>FULL NAME</small>
              <strong>{application.fullName || "—"}</strong>
            </div>

            <div>
              <small>CITY</small>
              <strong>{application.city || "—"}</strong>
            </div>

            <div>
              <small>PRIMARY DANCE STYLE</small>
              <strong>{application.primaryDanceStyle || "—"}</strong>
            </div>

            <div>
              <small>SECONDARY STYLES</small>
              <strong>{application.secondaryDanceStyles || "—"}</strong>
            </div>

            <div>
              <small>TEACHING EXPERIENCE</small>
              <strong>
                {application.experienceYears !== null && application.experienceYears !== undefined
                  ? `${application.experienceYears} Years`
                  : "—"}
              </strong>
            </div>

            <div>
              <small>CURRENT STUDIO</small>
              <strong>{application.currentStudio || "—"}</strong>
            </div>

            <div className="wide">
              <small>TEACHING PHILOSOPHY & BIO</small>
              <p className="dossier-bio">{application.bio || "—"}</p>
            </div>

            <div>
              <small>INSTAGRAM REEL</small>
              <strong>{application.instagramUrl || "—"}</strong>
            </div>

            <div>
              <small>YOUTUBE / PORTFOLIO</small>
              <strong>{application.youTubeUrl || "—"}</strong>
            </div>
          </div>
        </section>

        {/* ADMIN REVIEW NOTES */}
        {(application.adminNotes || application.rejectionReason || application.applicationNotes) && (
          <section className="dossier-section">
            <div className="dossier-section-heading">
              <span>03</span>
              <div>
                <small>ADMINISTRATIVE REVIEW</small>
                <h2>Review Notes & Feedback</h2>
              </div>
            </div>

            <div className="dossier-notes-card">
              {application.adminNotes && (
                <div className="note-block admin-notes">
                  <small>ADMIN REVIEW DECISION</small>
                  <p>✓ {application.adminNotes}</p>
                </div>
              )}

              {application.rejectionReason && (
                <div className="note-block rejection-notes">
                  <small>REJECTION / CHANGES REQUESTED REASON</small>
                  <p>⚠ {application.rejectionReason}</p>
                </div>
              )}

              {application.applicationNotes && (
                <div className="note-block applicant-notes">
                  <small>APPLICANT SUBMISSION NOTES</small>
                  <p>“{application.applicationNotes}”</p>
                </div>
              )}
            </div>
          </section>
        )}

        {/* UPLOADED DOCUMENTS */}
        <section className="dossier-section">
          <div className="dossier-section-heading">
            <span>04</span>
            <div>
              <small>VERIFIED DOCUMENTS</small>
              <h2>Uploaded Onboarding Proofs</h2>
            </div>
          </div>

          {documents.length === 0 ? (
            <div className="dossier-empty">
              <span>NO DOCUMENTS ON RECORD</span>
              <p>No uploaded application documents found for this record.</p>
            </div>
          ) : (
            <div className="documents-cards-grid">
              {documents.map((doc) => (
                <article key={doc.id} className="document-dossier-card">
                  <div className="doc-card-top">
                    <span className="doc-type-badge">{doc.documentType}</span>
                    <small>{formatFileSize(doc.fileSizeBytes)}</small>
                  </div>

                  <h3 className="doc-file-name">{doc.fileName}</h3>
                  <span className="doc-date">Uploaded on {formatDate(doc.createdAt)}</span>

                  <div className="doc-card-action">
                    {doc.fileUrl ? (
                      <a
                        href={doc.fileUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="doc-view-link"
                      >
                        VIEW DOCUMENT ↗
                      </a>
                    ) : (
                      <span className="doc-unavailable">File Url Unavailable</span>
                    )}
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        {/* PAYMENT VERIFICATION */}
        <section className="dossier-section">
          <div className="dossier-section-heading">
            <span>05</span>
            <div>
              <small>PAYMENT AUDIT</small>
              <h2>Application Fee Verification</h2>
            </div>
          </div>

          <div className="payment-verification-card">
            <div className="pay-verif-header">
              <span className="pay-status-tag">● VERIFIED</span>
              <small>RAZORPAY GATEWAY AUDIT</small>
            </div>

            <div className="pay-verif-grid">
              <div>
                <small>TRANSACTION REFERENCE ID</small>
                <strong>{application.paymentTransactionId || "—"}</strong>
              </div>

              <div>
                <small>VERIFICATION DATE</small>
                <strong>{formatDate(application.paymentVerifiedAt)}</strong>
              </div>

              {paymentDetails && (
                <>
                  <div>
                    <small>STATUS</small>
                    <strong>{paymentDetails.status || "Paid"}</strong>
                  </div>
                  <div>
                    <small>RAZORPAY ORDER ID</small>
                    <strong>{paymentDetails.razorpayOrderId || "—"}</strong>
                  </div>
                </>
              )}
            </div>
          </div>
        </section>

        {/* FOOTER ACTIONS */}
        <footer className="dossier-footer-nav">
          <Link to="/trainer/dashboard">← DASHBOARD</Link>
          <Link to="/trainer/profile">MANAGE PROFILE →</Link>
        </footer>
      </div>
    </main>
  );
}
