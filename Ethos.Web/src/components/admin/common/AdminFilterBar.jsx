import React from 'react';
import './AdminFilterBar.css';

export default function AdminFilterBar({
  searchValue,
  onSearchChange,
  searchPlaceholder = 'Search...',
  filters,
  actions,
  className = '',
}) {
  return (
    <div className={'admin-filter-bar-unified ' + className}>
      <div className="filter-search-group">
        <div className="search-input-wrap">
          <span className="search-icon" aria-hidden="true">🔍</span>
          <input
            type="text"
            className="filter-search-input"
            value={searchValue || ''}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder={searchPlaceholder}
          />
          {searchValue && (
            <button
              type="button"
              className="clear-search-btn"
              onClick={() => onSearchChange('')}
              aria-label="Clear search"
            >
              ✕
            </button>
          )}
        </div>
      </div>

      {filters && <div className="filter-dropdowns-group">{filters}</div>}
      {actions && <div className="filter-actions-group">{actions}</div>}
    </div>
  );
}
