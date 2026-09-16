import React from 'react';
import './AdminLoadingState.css';

export default function AdminLoadingState({
  rows = 5,
  message = 'Loading data...',
  type = 'table', // 'table' or 'card'
  className = '',
}) {
  if (type === 'card') {
    return (
      <div className={'admin-loading-cards-grid ' + className}>
        {Array.from({ length: rows }).map((_, i) => (
          <div key={i} className="skeleton-card">
            <div className="skeleton-line title" />
            <div className="skeleton-line sub" />
            <div className="skeleton-line short" />
          </div>
        ))}
      </div>
    );
  }

  return (
    <div className={'admin-loading-table-wrap ' + className}>
      <div className="loading-spinner-banner">
        <span className="spinner-ring" />
        <span className="spinner-message">{message}</span>
      </div>
      <div className="skeleton-rows">
        {Array.from({ length: rows }).map((_, i) => (
          <div key={i} className="skeleton-row-strip" />
        ))}
      </div>
    </div>
  );
}
