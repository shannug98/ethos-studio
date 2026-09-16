import React from 'react';
import { Link } from 'react-router-dom';
import './AdminPageHeader.css';

export default function AdminPageHeader({
  title,
  subtitle,
  category,
  breadcrumbs,
  badge,
  actions,
  className = '',
}) {
  return (
    <div className={'admin-page-header-unified ' + className}>
      <div className="header-titles-group">
        {breadcrumbs && breadcrumbs.length > 0 && (
          <nav className="header-breadcrumbs" aria-label="Breadcrumb">
            {breadcrumbs.map((crumb, idx) => {
              const isLast = idx === breadcrumbs.length - 1;
              return (
                <span key={idx} className="breadcrumb-item">
                  {idx > 0 && <span className="breadcrumb-sep">/</span>}
                  {typeof crumb === 'object' && crumb.to && !isLast ? (
                    <Link to={crumb.to} className="breadcrumb-link">
                      {crumb.label}
                    </Link>
                  ) : (
                    <span className={'breadcrumb-text ' + (isLast ? 'active' : '')}>
                      {typeof crumb === 'object' ? crumb.label : crumb}
                    </span>
                  )}
                </span>
              );
            })}
          </nav>
        )}

        {category && <div className="header-category-eyebrow">{category}</div>}

        <div className="header-title-row">
          <h1 className="header-page-title">{title}</h1>
          {badge && <div className="header-title-badge">{badge}</div>}
        </div>

        {subtitle && <p className="header-page-subtitle">{subtitle}</p>}
      </div>

      {actions && <div className="header-actions-group">{actions}</div>}
    </div>
  );
}
