import React from 'react';
import './AdminEmptyState.css';

export default function AdminEmptyState({
  icon = '🔍',
  title = 'No records found',
  description = 'There are no items matching your current filters or criteria.',
  action,
  className = '',
}) {
  return (
    <div className={'admin-empty-state-card ' + className}>
      <div className="empty-state-icon" aria-hidden="true">{icon}</div>
      <h3 className="empty-state-title">{title}</h3>
      {description && <p className="empty-state-desc">{description}</p>}
      {action && <div className="empty-state-action">{action}</div>}
    </div>
  );
}
