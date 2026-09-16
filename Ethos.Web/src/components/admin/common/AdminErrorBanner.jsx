import React from 'react';
import './AdminErrorBanner.css';

export default function AdminErrorBanner({
  title = 'Something went wrong',
  message,
  onRetry,
  className = '',
}) {
  return (
    <div className={'admin-error-banner-unified ' + className} role="alert">
      <div className="error-icon" aria-hidden="true">⚠️</div>
      <div className="error-text-content">
        <h4 className="error-title">{title}</h4>
        {message && <p className="error-desc">{message}</p>}
      </div>
      {onRetry && (
        <button type="button" className="error-retry-btn" onClick={onRetry}>
          Try Again
        </button>
      )}
    </div>
  );
}
