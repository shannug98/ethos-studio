import React, { useEffect, useRef } from "react";
import "./AdminActionModal.css";

export default function AdminActionModal({
  isOpen,
  onClose,
  title,
  subtitle,
  children,
  primaryAction,
  secondaryAction,
  tone = "neutral", // neutral, danger, warning, success
  maxWidth = "640px",
}) {
  const modalRef = useRef(null);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e) => {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    // Focus modal container
    setTimeout(() => {
      modalRef.current?.focus();
    }, 50);

    return () => {
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div className="admin-modal-backdrop" onClick={onClose}>
      <div
        className={`admin-modal-container tone-${tone}`}
        style={{ maxWidth }}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="admin-modal-title"
        tabIndex={-1}
        ref={modalRef}
      >
        <div className="admin-modal-header">
          <div className="admin-modal-header-text">
            <h2 id="admin-modal-title" className="admin-modal-title">
              {title}
            </h2>
            {subtitle && <p className="admin-modal-subtitle">{subtitle}</p>}
          </div>
          <button
            type="button"
            className="admin-modal-close-btn"
            onClick={onClose}
            aria-label="Close modal"
          >
            ✕
          </button>
        </div>

        <div className="admin-modal-body">{children}</div>

        {(primaryAction || secondaryAction) && (
          <div className="admin-modal-footer">
            {secondaryAction && (
              <button
                type="button"
                className="admin-btn-secondary"
                onClick={secondaryAction.onClick || onClose}
                disabled={secondaryAction.disabled}
              >
                {secondaryAction.label || "Cancel"}
              </button>
            )}
            {primaryAction && (
              <button
                type="button"
                className={`admin-btn-primary tone-${tone}`}
                onClick={primaryAction.onClick}
                disabled={primaryAction.disabled || primaryAction.loading}
              >
                {primaryAction.loading ? "Processing..." : primaryAction.label || "Confirm"}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
